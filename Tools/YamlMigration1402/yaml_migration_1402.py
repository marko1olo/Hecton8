#!/usr/bin/env python3
"""Read-only Unity YAML desync scanner for agent 1402.

Default command writes JSON proof artifacts only. Asset mutation is deliberately
absent from this tool until a concrete obsolete-property hit has an approved
schema destination and a dry-run diff.
"""

from __future__ import annotations

import argparse
import datetime as _dt
import hashlib
import json
import os
import re
import shutil
import sys
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Sequence, Tuple


AGENT_ID = "1402"

OBSOLETE_PROPERTIES = (
    "_cellIntegrityFront",
    "_cellIntegrityBack",
    "_cellFatigue",
    "_cellCompartmentIndices",
    "_hullBreachMaskFront",
    "_hullBreachMaskBack",
    "_compartmentBreachAreasFront",
    "_compartmentBreachAreasBack",
    "_queuedImpacts",
    "_scheduledImpacts",
    "_densityBuildSources",
    "_publishedSonarSdf",
    "_combatDamageArray",
)

UNITY_BUILTIN_ROOTS = {
    "m_ObjectHideFlags",
    "m_CorrespondingSourceObject",
    "m_PrefabInstance",
    "m_PrefabAsset",
    "m_GameObject",
    "m_Enabled",
    "m_EditorHideFlags",
    "m_Script",
    "m_Name",
    "m_EditorClassIdentifier",
    "serializedVersion",
    "references",
}

TARGET_ASSET_RELATIVE_PATHS = (
    "Assets/_Project/Scenes/02_HECTON_WORLD.unity",
    "Assets/_Project/Prefabs/Player.prefab",
    "Assets/_Project/Prefabs/Construction/Final/PFB_Module_Corridor.prefab",
    "Assets/_Project/Prefabs/Construction/Final/PFB_Module_CurrentTurbine.prefab",
    "Assets/_Project/Prefabs/Construction/Final/PFB_Module_Foundation.prefab",
    "Assets/_Project/Prefabs/Construction/Final/PFB_Module_Pylon.prefab",
    "Assets/_Project/Prefabs/Construction/Final/PFB_Module_ServicePump.prefab",
)

FIELD_DECL_RE = re.compile(
    r"\b(?P<access>public|private|protected|internal)\s+"
    r"(?P<rest>(?:(?:readonly|static|const|volatile|unsafe|new|required)\s+)*)"
    r"(?P<type>[A-Za-z_][A-Za-z0-9_:<>,\.\[\]\? ]*)\s+"
    r"(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?:=|;)"
)
GUID_RE = re.compile(r"^guid:\s*([0-9a-fA-F]{32})\s*$")
CLASS_RE = re.compile(
    r"\b(?:class|struct)\s+([A-Za-z_][A-Za-z0-9_]*)\b(?:\s*:\s*([^\{]+))?"
)
FORMERLY_RE = re.compile(r"FormerlySerializedAs\s*\(\s*\"([^\"]+)\"\s*\)")
MONO_START_RE = re.compile(r"^--- !u!114 &(-?\d+)\s*$")
OBJECT_START_RE = re.compile(r"^--- !u!(\d+) &(-?\d+)\s*$")
SCRIPT_RE = re.compile(
    r"m_Script:\s*\{fileID:\s*(-?\d+),\s*guid:\s*([0-9a-fA-F]*),\s*type:\s*(-?\d+)\}"
)
ROOT_PROPERTY_RE = re.compile(r"^  ([A-Za-z_][A-Za-z0-9_]*):(?:\s|$)")
PREFAB_PATH_RE = re.compile(r"propertyPath:\s*([A-Za-z_][A-Za-z0-9_\.]*)")


@dataclass
class ScriptInfo:
    guid: str
    path: str
    class_names: List[str] = field(default_factory=list)
    base_names_by_class: Dict[str, List[str]] = field(default_factory=dict)
    serialized_names: List[str] = field(default_factory=list)
    formerly_names: List[str] = field(default_factory=list)


@dataclass
class RootProperty:
    name: str
    line: int
    indent: int


@dataclass
class MonoBlock:
    file_id: str
    start_line: int
    end_line: int
    script_file_id: Optional[str] = None
    script_guid: Optional[str] = None
    script_type: Optional[str] = None
    root_properties: List[RootProperty] = field(default_factory=list)


def utc_now() -> str:
    return _dt.datetime.now(_dt.timezone.utc).isoformat(timespec="seconds")


def rel(root: Path, path: Path) -> str:
    try:
        return path.relative_to(root).as_posix()
    except ValueError:
        return path.as_posix()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def read_meta_guid(meta_path: Path) -> Optional[str]:
    try:
        with meta_path.open("r", encoding="utf-8-sig", errors="replace") as f:
            for line in f:
                match = GUID_RE.match(line.strip())
                if match:
                    return match.group(1).lower()
    except OSError:
        return None
    return None


def parse_cs_script(path: Path, guid: str, root: Path) -> ScriptInfo:
    info = ScriptInfo(guid=guid, path=rel(root, path))
    pending_attrs: List[str] = []

    try:
        lines = path.read_text(encoding="utf-8-sig", errors="replace").splitlines()
    except OSError:
        return info

    for raw in lines:
        line = raw.strip()
        class_match = CLASS_RE.search(line)
        if class_match:
            name = class_match.group(1)
            if name not in info.class_names:
                info.class_names.append(name)
            base_names = parse_base_names(class_match.group(2) or "")
            if base_names:
                info.base_names_by_class[name] = base_names

        if line.startswith("["):
            pending_attrs.append(line)
            if "]" not in line:
                continue

        attr_text = " ".join(pending_attrs + ([line] if "[" in line and not pending_attrs else []))
        field_match = FIELD_DECL_RE.search(line)
        if not field_match:
            if ";" in line or "{" in line:
                pending_attrs.clear()
            continue

        rest = field_match.group("rest")
        access = field_match.group("access")
        field_name = field_match.group("name")
        is_static = "static" in rest.split()
        is_const = "const" in rest.split()
        has_serialize = "SerializeField" in attr_text or "SerializeReference" in attr_text
        is_public = access == "public"

        if not is_static and not is_const and (is_public or has_serialize):
            if field_name not in info.serialized_names:
                info.serialized_names.append(field_name)
            for former in FORMERLY_RE.findall(attr_text):
                if former not in info.formerly_names:
                    info.formerly_names.append(former)

        pending_attrs.clear()

    info.serialized_names.sort()
    info.formerly_names.sort()
    return info


def parse_base_names(raw_bases: str) -> List[str]:
    bases: List[str] = []
    for raw_base in raw_bases.split(","):
        token = raw_base.strip()
        if not token:
            continue
        token = token.split()[0]
        token = token.split("<", 1)[0]
        token = token.rsplit(".", 1)[-1]
        if token and token not in bases:
            bases.append(token)
    return bases


def load_script_map(root: Path) -> Dict[str, ScriptInfo]:
    script_map: Dict[str, ScriptInfo] = {}
    scripts_root = root / "Assets" / "_Project" / "Scripts"
    for cs_path in scripts_root.rglob("*.cs"):
        guid = read_meta_guid(Path(str(cs_path) + ".meta"))
        if not guid:
            continue
        script_map[guid] = parse_cs_script(cs_path, guid, root)
    merge_partial_class_fields(script_map)
    merge_inherited_class_fields(script_map)
    return script_map


def merge_partial_class_fields(script_map: Dict[str, ScriptInfo]) -> None:
    serialized_by_class: Dict[str, set] = {}
    formerly_by_class: Dict[str, set] = {}

    for script in script_map.values():
        for class_name in script.class_names:
            serialized_by_class.setdefault(class_name, set()).update(script.serialized_names)
            formerly_by_class.setdefault(class_name, set()).update(script.formerly_names)

    for script in script_map.values():
        serialized = set(script.serialized_names)
        formerly = set(script.formerly_names)
        for class_name in script.class_names:
            serialized.update(serialized_by_class.get(class_name, set()))
            formerly.update(formerly_by_class.get(class_name, set()))
        script.serialized_names = sorted(serialized)
        script.formerly_names = sorted(formerly)


def merge_inherited_class_fields(script_map: Dict[str, ScriptInfo]) -> None:
    by_class: Dict[str, List[ScriptInfo]] = {}
    for script in script_map.values():
        for class_name in script.class_names:
            by_class.setdefault(class_name, []).append(script)

    def collect_base_fields(class_name: str, visited: set) -> Tuple[set, set]:
        if class_name in visited:
            return set(), set()
        visited.add(class_name)

        serialized = set()
        formerly = set()
        for script in by_class.get(class_name, []):
            for base_name in script.base_names_by_class.get(class_name, []):
                for base_script in by_class.get(base_name, []):
                    serialized.update(base_script.serialized_names)
                    formerly.update(base_script.formerly_names)
                base_serialized, base_formerly = collect_base_fields(base_name, visited)
                serialized.update(base_serialized)
                formerly.update(base_formerly)
        return serialized, formerly

    for script in script_map.values():
        serialized = set(script.serialized_names)
        formerly = set(script.formerly_names)
        for class_name in script.class_names:
            base_serialized, base_formerly = collect_base_fields(class_name, set())
            serialized.update(base_serialized)
            formerly.update(base_formerly)
        script.serialized_names = sorted(serialized)
        script.formerly_names = sorted(formerly)


def iter_asset_paths(root: Path, full_scope: bool) -> List[Path]:
    if not full_scope:
        return [root / p for p in TARGET_ASSET_RELATIVE_PATHS if (root / p).exists()]

    paths: List[Path] = []
    for base in (root / "Assets" / "_Project" / "Scenes", root / "Assets" / "_Project" / "Prefabs"):
        if not base.exists():
            continue
        for suffix in ("*.unity", "*.prefab", "*.asset"):
            paths.extend(base.rglob(suffix))
    return sorted(set(paths))


def finalize_block(
    block: Optional[MonoBlock],
    path: Path,
    root: Path,
    script_map: Dict[str, ScriptInfo],
    hits: List[dict],
    block_counts: dict,
) -> None:
    if block is None:
        return

    block_counts["mono_behaviour_blocks"] += 1

    if block.script_file_id == "0" or not block.script_guid:
        hits.append(
            {
                "kind": "missing_script_reference",
                "evidence_class": "STATIC_SOURCE",
                "file": rel(root, path),
                "line": block.start_line,
                "fileID": block.file_id,
                "scriptGuid": block.script_guid or "",
                "property": "m_Script",
                "action": "manual_review_required",
            }
        )
        return

    script = script_map.get(block.script_guid.lower())
    accepted = set()
    if script:
        accepted.update(script.serialized_names)
        accepted.update(script.formerly_names)

    for prop in block.root_properties:
        if prop.name in UNITY_BUILTIN_ROOTS or prop.name.startswith("m_"):
            continue
        is_target_obsolete = prop.name in OBSOLETE_PROPERTIES
        schema_mismatch = bool(script) and prop.name not in accepted

        if not (is_target_obsolete or schema_mismatch):
            continue

        hits.append(
            {
                "kind": "obsolete_property" if is_target_obsolete else "schema_mismatch_candidate",
                "evidence_class": "STATIC_SOURCE",
                "file": rel(root, path),
                "line": prop.line,
                "fileID": block.file_id,
                "scriptGuid": block.script_guid,
                "scriptPath": script.path if script else "",
                "scriptClasses": script.class_names if script else [],
                "property": prop.name,
                "currentSerializedNamesKnown": bool(script),
                "action": "dry_run_only",
            }
        )


def scan_yaml_file(path: Path, root: Path, script_map: Dict[str, ScriptInfo]) -> Tuple[List[dict], dict]:
    hits: List[dict] = []
    block_counts = {
        "mono_behaviour_blocks": 0,
        "prefab_instance_blocks": 0,
        "object_blocks": 0,
    }
    current: Optional[MonoBlock] = None
    current_object_type: Optional[str] = None
    current_object_file_id = ""

    with path.open("r", encoding="utf-8-sig", errors="replace", newline="") as f:
        for line_no, raw in enumerate(f, start=1):
            object_match = OBJECT_START_RE.match(raw.rstrip("\r\n"))
            if object_match:
                finalize_block(current, path, root, script_map, hits, block_counts)
                current = None
                current_object_type = object_match.group(1)
                current_object_file_id = object_match.group(2)
                block_counts["object_blocks"] += 1
                if current_object_type == "114":
                    current = MonoBlock(
                        file_id=current_object_file_id,
                        start_line=line_no,
                        end_line=line_no,
                    )
                elif current_object_type == "1001":
                    block_counts["prefab_instance_blocks"] += 1
                continue

            if current is not None:
                current.end_line = line_no
                script_match = SCRIPT_RE.search(raw)
                if script_match:
                    current.script_file_id = script_match.group(1)
                    current.script_guid = script_match.group(2).lower()
                    current.script_type = script_match.group(3)

                prop_match = ROOT_PROPERTY_RE.match(raw)
                if prop_match:
                    current.root_properties.append(
                        RootProperty(name=prop_match.group(1), line=line_no, indent=2)
                    )
                continue

            if current_object_type == "1001":
                path_match = PREFAB_PATH_RE.search(raw)
                if path_match:
                    property_path = path_match.group(1)
                    root_name = property_path.split(".", 1)[0]
                    if root_name in OBSOLETE_PROPERTIES:
                        hits.append(
                            {
                                "kind": "prefab_modification_obsolete_property_path",
                                "evidence_class": "STATIC_SOURCE",
                                "file": rel(root, path),
                                "line": line_no,
                                "fileID": current_object_file_id,
                                "property": property_path,
                                "action": "dry_run_only",
                            }
                        )

    finalize_block(current, path, root, script_map, hits, block_counts)
    return hits, block_counts


def build_schema_map(script_map: Dict[str, ScriptInfo]) -> List[dict]:
    by_class: Dict[str, ScriptInfo] = {}
    for script in script_map.values():
        for class_name in script.class_names:
            by_class[class_name] = script

    def field_state(class_name: str, old_name: str, suggested_handle: str) -> dict:
        script = by_class.get(class_name)
        serialized = set(script.serialized_names if script else [])
        formerly = set(script.formerly_names if script else [])
        return {
            "class": class_name,
            "oldProperty": old_name,
            "suggestedCurrentOwner": suggested_handle,
            "scriptPath": script.path if script else "",
            "oldPropertyStillAcceptedBySerializedObject": old_name in serialized or old_name in formerly,
            "currentOwnerIsSerialized": suggested_handle in serialized,
            "migrationRoute": (
                "SerializedObject"
                if old_name in formerly or old_name in serialized
                else "raw_yaml_extract_only_if_hit_then_manual_schema_required"
            ),
            "status": (
                "no_serialized_destination_detected"
                if script and suggested_handle not in serialized
                else "pending_review"
            ),
        }

    return [
        field_state("SubmarineStructuralGrid", "_cellIntegrityFront", "_cellIntegrityFrontHandle"),
        field_state("SubmarineStructuralGrid", "_cellIntegrityBack", "_cellIntegrityBackHandle"),
        field_state("SubmarineStructuralGrid", "_cellFatigue", "_cellFatigueHandle"),
        field_state("SubmarineStructuralGrid", "_cellCompartmentIndices", "_cellCompartmentIndicesHandle"),
        field_state("SargassumGlobalDragManager", "_densityBuildSources", "_densityBuildSourcesHandle"),
        field_state("HectonVoxelVolume", "_publishedSonarSdf", "_publishedSonarSdfRange"),
    ]


def scan(args: argparse.Namespace) -> int:
    root = Path(args.root).resolve()
    script_map = load_script_map(root)
    paths = iter_asset_paths(root, full_scope=args.full_scope)

    all_hits: List[dict] = []
    file_summaries: List[dict] = []
    totals = {
        "files_scanned": 0,
        "mono_behaviour_blocks": 0,
        "prefab_instance_blocks": 0,
        "object_blocks": 0,
    }

    for path in paths:
        hits, counts = scan_yaml_file(path, root, script_map)
        all_hits.extend(hits)
        for key, value in counts.items():
            totals[key] += value
        totals["files_scanned"] += 1
        file_summaries.append(
            {
                "file": rel(root, path),
                "bytes": path.stat().st_size,
                "sha256": sha256_file(path),
                "hits": len(hits),
                **counts,
            }
        )

    report = {
        "agentId": AGENT_ID,
        "generatedUtc": utc_now(),
        "mode": "scan",
        "evidenceClass": "STATIC_SOURCE",
        "fullScope": bool(args.full_scope),
        "obsoleteProperties": list(OBSOLETE_PROPERTIES),
        "totals": {
            **totals,
            "hits": len(all_hits),
            "exact_obsolete_hits": sum(1 for h in all_hits if h["kind"] == "obsolete_property"),
            "schema_mismatch_candidates": sum(1 for h in all_hits if h["kind"] == "schema_mismatch_candidate"),
            "missing_script_references": sum(1 for h in all_hits if h["kind"] == "missing_script_reference"),
            "prefab_obsolete_property_paths": sum(
                1 for h in all_hits if h["kind"] == "prefab_modification_obsolete_property_path"
            ),
        },
        "schemaMap": build_schema_map(script_map),
        "files": file_summaries,
        "hits": all_hits,
    }

    output = root / args.output
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2), encoding="utf-8", newline="\n")
    print(str(output))
    return 0


def write_backup_plan(args: argparse.Namespace) -> int:
    root = Path(args.root).resolve()
    backup_root = root / "Docs" / "Tasks" / "_Recovery_1402"
    assets = [root / p for p in TARGET_ASSET_RELATIVE_PATHS if (root / p).exists()]

    plan = {
        "agentId": AGENT_ID,
        "generatedUtc": utc_now(),
        "mode": "backup_plan",
        "evidenceClass": "STATIC_SOURCE",
        "backupRoot": rel(root, backup_root),
        "commands": [],
        "assets": [],
    }
    for asset in assets:
        destination = backup_root / rel(root, asset)
        plan["commands"].append(
            {
                "copy": f"Copy-Item -LiteralPath '{asset}' -Destination '{destination}'",
                "verifySize": f"(Get-Item '{asset}').Length -eq (Get-Item '{destination}').Length",
            }
        )
        plan["assets"].append(
            {
                "source": rel(root, asset),
                "destination": rel(root, destination),
                "bytes": asset.stat().st_size,
                "sha256": sha256_file(asset),
            }
        )

    output = root / args.output
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(plan, indent=2), encoding="utf-8", newline="\n")
    print(str(output))
    return 0


def execute_backup(args: argparse.Namespace) -> int:
    root = Path(args.root).resolve()
    backup_root = root / "Docs" / "Tasks" / "_Recovery_1402"
    assets = [root / p for p in TARGET_ASSET_RELATIVE_PATHS if (root / p).exists()]
    results = []

    for asset in assets:
        destination = backup_root / rel(root, asset)
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(asset, destination)
        same_size = asset.stat().st_size == destination.stat().st_size
        same_hash = sha256_file(asset) == sha256_file(destination)
        results.append(
            {
                "source": rel(root, asset),
                "destination": rel(root, destination),
                "sourceBytes": asset.stat().st_size,
                "destinationBytes": destination.stat().st_size,
                "sizeMatch": same_size,
                "sha256Match": same_hash,
            }
        )
        if not same_size or not same_hash:
            raise RuntimeError(f"Backup verification failed for {asset}")

    output = root / args.output
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(
        json.dumps(
            {
                "agentId": AGENT_ID,
                "generatedUtc": utc_now(),
                "mode": "backup_execute",
                "evidenceClass": "STATIC_SOURCE",
                "backupRoot": rel(root, backup_root),
                "results": results,
            },
            indent=2,
        ),
        encoding="utf-8",
        newline="\n",
    )
    print(str(output))
    return 0


def validate(args: argparse.Namespace) -> int:
    root = Path(args.root).resolve()
    paths = iter_asset_paths(root, full_scope=args.full_scope)
    results = []
    failures = 0
    for path in paths:
        mono_count = 0
        tabs = 0
        exact_obsolete = 0
        missing_script = 0
        with path.open("r", encoding="utf-8-sig", errors="replace", newline="") as f:
            for line_no, raw in enumerate(f, start=1):
                if MONO_START_RE.match(raw.rstrip("\r\n")):
                    mono_count += 1
                if "\t" in raw:
                    tabs += 1
                if SCRIPT_RE.search(raw):
                    script_match = SCRIPT_RE.search(raw)
                    if script_match and script_match.group(1) == "0":
                        missing_script += 1
                stripped = raw.strip()
                for prop in OBSOLETE_PROPERTIES:
                    if stripped.startswith(prop + ":"):
                        exact_obsolete += 1
        ok = exact_obsolete == 0 and missing_script == 0
        if args.strict_existing_tabs and tabs != 0:
            ok = False
        if not ok:
            failures += 1
        results.append(
            {
                "file": rel(root, path),
                "monoBehaviourBlocks": mono_count,
                "tabs": tabs,
                "exactObsoleteProperties": exact_obsolete,
                "missingScriptReferences": missing_script,
                "ok": ok,
            }
        )

    output = root / args.output
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(
        json.dumps(
            {
                "agentId": AGENT_ID,
                "generatedUtc": utc_now(),
                "mode": "validate",
                "evidenceClass": "STATIC_SOURCE",
                "fullScope": bool(args.full_scope),
                "failures": failures,
                "results": results,
            },
            indent=2,
        ),
        encoding="utf-8",
        newline="\n",
    )
    print(str(output))
    return 1 if failures else 0


def dry_run(args: argparse.Namespace) -> int:
    root = Path(args.root).resolve()
    ledger_path = root / args.ledger
    ledger = json.loads(ledger_path.read_text(encoding="utf-8"))
    exact_hits = [
        hit for hit in ledger.get("hits", [])
        if hit.get("kind") in ("obsolete_property", "prefab_modification_obsolete_property_path")
    ]
    report = {
        "agentId": AGENT_ID,
        "generatedUtc": utc_now(),
        "mode": "dry_run",
        "evidenceClass": "STATIC_SOURCE",
        "ledger": rel(root, ledger_path),
        "wouldModifyFiles": [],
        "wouldRemoveLines": [],
        "wouldAddBlocks": [],
        "abortReason": "",
    }
    if not exact_hits:
        report["abortReason"] = "NO_EXACT_OBSOLETE_PROPERTY_HITS"
    else:
        report["abortReason"] = "SCHEMA_DESTINATION_NOT_APPROVED"
        for hit in exact_hits:
            report["wouldRemoveLines"].append(
                {
                    "file": hit["file"],
                    "line": hit["line"],
                    "property": hit["property"],
                    "reason": "exact obsolete property detected; migration destination requires manual schema approval",
                }
            )
        report["wouldModifyFiles"] = sorted({hit["file"] for hit in exact_hits})

    output = root / args.output
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2), encoding="utf-8", newline="\n")
    print(str(output))
    return 0


def validate_obsolete_payload(lines: Sequence[str], property_line_index: int) -> List[dict]:
    warnings: List[dict] = []
    if property_line_index < 0 or property_line_index >= len(lines):
        return [{"code": "PROPERTY_LINE_OUT_OF_RANGE", "line": property_line_index + 1}]

    root_line = lines[property_line_index]
    base_indent = len(root_line) - len(root_line.lstrip(" "))
    for offset in range(property_line_index + 1, len(lines)):
        line = lines[offset].rstrip("\r\n")
        if not line.strip():
            continue
        indent = len(line) - len(line.lstrip(" "))
        if indent <= base_indent:
            break
        stripped = line.strip()
        if not stripped.startswith("- "):
            warnings.append(
                {
                    "code": "ARRAY_ITEM_MISSING_DASH",
                    "line": offset + 1,
                    "text": stripped,
                }
            )
            continue
        value = stripped[2:].strip()
        if value.startswith("{") and value.endswith("}"):
            continue
        if ":" in value:
            continue
        try:
            float(value)
        except ValueError:
            warnings.append(
                {
                    "code": "NON_NUMERIC_ARRAY_SCALAR",
                    "line": offset + 1,
                    "text": stripped,
                }
            )
    return warnings


def self_test(args: argparse.Namespace) -> int:
    malformed_missing_dash = [
        "--- !u!114 &10\n",
        "MonoBehaviour:\n",
        "  m_Script: {fileID: 11500000, guid: 6135d4e65896e4d45ab16f369317eb72, type: 3}\n",
        "  _cellIntegrityFront:\n",
        "    1.0\n",
        "  gridWidth: 16\n",
    ]
    malformed_non_numeric = [
        "--- !u!114 &11\n",
        "MonoBehaviour:\n",
        "  m_Script: {fileID: 11500000, guid: 6135d4e65896e4d45ab16f369317eb72, type: 3}\n",
        "  _cellIntegrityFront:\n",
        "    - 1.0\n",
        "    - rotten\n",
        "  gridWidth: 16\n",
    ]
    valid_references = [
        "--- !u!114 &12\n",
        "MonoBehaviour:\n",
        "  m_Script: {fileID: 11500000, guid: 6135d4e65896e4d45ab16f369317eb72, type: 3}\n",
        "  _combatDamageArray:\n",
        "    - {fileID: 2100000, guid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, type: 2}\n",
        "    - 0.5\n",
        "  gridWidth: 16\n",
    ]
    cases = [
        ("missing_dash", malformed_missing_dash, 3, "ARRAY_ITEM_MISSING_DASH"),
        ("non_numeric", malformed_non_numeric, 3, "NON_NUMERIC_ARRAY_SCALAR"),
        ("valid_references", valid_references, 3, ""),
    ]
    results = []
    failures = 0
    for name, lines, index, expected in cases:
        warnings = validate_obsolete_payload(lines, index)
        codes = [warning["code"] for warning in warnings]
        passed = (expected in codes) if expected else len(codes) == 0
        if not passed:
            failures += 1
        results.append(
            {
                "case": name,
                "expectedCode": expected,
                "warningCodes": codes,
                "warnings": warnings,
                "passed": passed,
            }
        )

    partial_scripts = {
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa": ScriptInfo(
            guid="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            path="Assets/_Project/Scripts/Gameplay/PlayerSwimBlockoutRig.cs",
            class_names=["PlayerSwimBlockoutRig"],
            serialized_names=["leftForearm"],
            formerly_names=[],
        ),
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb": ScriptInfo(
            guid="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            path="Assets/_Project/Scripts/Gameplay/PlayerSwimBlockoutRig.Body.cs",
            class_names=["PlayerSwimBlockoutRig"],
            serialized_names=["torso"],
            formerly_names=["legacyTorso"],
        ),
    }
    merge_partial_class_fields(partial_scripts)
    partial_primary = partial_scripts["aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"]
    partial_passed = (
        "leftForearm" in partial_primary.serialized_names
        and "torso" in partial_primary.serialized_names
        and "legacyTorso" in partial_primary.formerly_names
    )
    if not partial_passed:
        failures += 1
    results.append(
        {
            "case": "partial_class_merge",
            "expectedCode": "",
            "warningCodes": [],
            "warnings": [],
            "mergedSerializedNames": partial_primary.serialized_names,
            "mergedFormerlyNames": partial_primary.formerly_names,
            "passed": partial_passed,
        }
    )

    inherited_scripts = {
        "cccccccccccccccccccccccccccccccc": ScriptInfo(
            guid="cccccccccccccccccccccccccccccccc",
            path="Assets/_Project/Scripts/PlayerTool.cs",
            class_names=["PlayerTool"],
            serialized_names=["_toolData"],
            formerly_names=["legacyToolData"],
        ),
        "dddddddddddddddddddddddddddddddd": ScriptInfo(
            guid="dddddddddddddddddddddddddddddddd",
            path="Assets/_Project/Scripts/BuilderTool.cs",
            class_names=["BuilderTool"],
            base_names_by_class={"BuilderTool": ["PlayerTool"]},
            serialized_names=["builderSpecific"],
            formerly_names=[],
        ),
    }
    merge_partial_class_fields(inherited_scripts)
    merge_inherited_class_fields(inherited_scripts)
    inherited_child = inherited_scripts["dddddddddddddddddddddddddddddddd"]
    inherited_passed = (
        "_toolData" in inherited_child.serialized_names
        and "builderSpecific" in inherited_child.serialized_names
        and "legacyToolData" in inherited_child.formerly_names
    )
    if not inherited_passed:
        failures += 1
    results.append(
        {
            "case": "base_class_merge",
            "expectedCode": "",
            "warningCodes": [],
            "warnings": [],
            "mergedSerializedNames": inherited_child.serialized_names,
            "mergedFormerlyNames": inherited_child.formerly_names,
            "passed": inherited_passed,
        }
    )

    global_type_line = "[SerializeField] private global::Crest.OceanRenderer crestOceanRenderer;"
    global_type_match = FIELD_DECL_RE.search(global_type_line)
    global_type_passed = bool(global_type_match) and global_type_match.group("name") == "crestOceanRenderer"
    if not global_type_passed:
        failures += 1
    results.append(
        {
            "case": "global_qualified_field_type",
            "expectedCode": "",
            "warningCodes": [],
            "warnings": [],
            "matchedName": global_type_match.group("name") if global_type_match else "",
            "passed": global_type_passed,
        }
    )

    root = Path(args.root).resolve()
    output = root / args.output
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(
        json.dumps(
            {
                "agentId": AGENT_ID,
                "generatedUtc": utc_now(),
                "mode": "self_test",
                "evidenceClass": "STATIC_SOURCE",
                "failures": failures,
                "results": results,
            },
            indent=2,
        ),
        encoding="utf-8",
        newline="\n",
    )
    print(str(output))
    return 1 if failures else 0


def load_json_or_empty(root: Path, relative_path: str) -> dict:
    path = root / relative_path
    if not path.exists():
        return {}
    return json.loads(path.read_text(encoding="utf-8-sig"))


def final_report(args: argparse.Namespace) -> int:
    root = Path(args.root).resolve()
    target_paths = [root / p for p in TARGET_ASSET_RELATIVE_PATHS if (root / p).exists()]
    ledger = load_json_or_empty(root, "Docs/AgentLogs/YamlDesyncLedger_1402.json")
    full_ledger = load_json_or_empty(root, "Docs/AgentLogs/YamlDesyncLedger_1402_full.json")
    backup = load_json_or_empty(root, "Docs/AgentLogs/BackupExecution_1402.json")
    dry = load_json_or_empty(root, "Docs/AgentLogs/YamlDryRun_1402.json")
    validation = load_json_or_empty(root, "Docs/AgentLogs/YamlValidation_1402.json")
    alignment = load_json_or_empty(root, "Docs/AgentLogs/FileIdAlignment_1402.json")
    prefab_guard = load_json_or_empty(root, "Docs/AgentLogs/PrefabOverrideGuard_1402.json")
    missing_refs = load_json_or_empty(root, "Docs/AgentLogs/MissingReferenceSweep_1402.json")
    compile_skip = load_json_or_empty(root, "Docs/AgentLogs/CompileSkip_1402.json")
    selftest = load_json_or_empty(root, "Docs/AgentLogs/YamlParserFuzzer_1402.json")

    report = {
        "agentId": AGENT_ID,
        "generatedUtc": utc_now(),
        "evidenceClass": "STATIC_SOURCE",
        "status": "PENDING_UNITY_EDITOR_VERIFICATION",
        "filesModified": 0,
        "yamlBytesRewritten": 0,
        "obsoletePropertiesMigrated": 0,
        "exactObsoletePropertiesRemainingInTargetSet": ledger.get("totals", {}).get("exact_obsolete_hits", 0),
        "exactObsoletePropertiesRemainingFullScope": full_ledger.get("totals", {}).get("exact_obsolete_hits", 0),
        "schemaMismatchCandidatesFullScope": full_ledger.get("totals", {}).get("schema_mismatch_candidates", 0),
        "backupGenerated": bool(backup.get("results")),
        "backupAllHashesMatch": all(item.get("sha256Match") for item in backup.get("results", [])),
        "dryRunAbortReason": dry.get("abortReason", ""),
        "validationFailures": validation.get("failures", 0),
        "prefabObsoletePropertyPathHits": prefab_guard.get("obsoletePropertyPathHits", 0),
        "missingScriptReferences": missing_refs.get("missingScriptReferences", 0),
        "dotnetBuildRun": compile_skip.get("dotnetBuildRun", False),
        "compilerSkipReason": compile_skip.get("reason", ""),
        "parserFuzzerFailures": selftest.get("failures", 0),
        "alignment": alignment,
        "targetFileHashes": [
            {
                "file": rel(root, path),
                "bytes": path.stat().st_size,
                "sha256": sha256_file(path),
            }
            for path in target_paths
        ],
        "artifacts": {
            "targetLedger": "Docs/AgentLogs/YamlDesyncLedger_1402.json",
            "fullLedger": "Docs/AgentLogs/YamlDesyncLedger_1402_full.json",
            "backupPlan": "Docs/AgentLogs/BackupPlan_1402.json",
            "backupExecution": "Docs/AgentLogs/BackupExecution_1402.json",
            "dryRun": "Docs/AgentLogs/YamlDryRun_1402.json",
            "validation": "Docs/AgentLogs/YamlValidation_1402.json",
            "alignment": "Docs/AgentLogs/FileIdAlignment_1402.json",
            "prefabOverrideGuard": "Docs/AgentLogs/PrefabOverrideGuard_1402.json",
            "missingReferenceSweep": "Docs/AgentLogs/MissingReferenceSweep_1402.json",
            "compileSkip": "Docs/AgentLogs/CompileSkip_1402.json",
            "parserFuzzer": "Docs/AgentLogs/YamlParserFuzzer_1402.json",
        },
    }

    output = root / args.output
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2), encoding="utf-8", newline="\n")
    print(str(output))
    return 0


def main(argv: Optional[Sequence[str]] = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=".", help="Project root, default current directory.")
    sub = parser.add_subparsers(dest="command", required=True)

    scan_parser = sub.add_parser("scan")
    scan_parser.add_argument("--full-scope", action="store_true")
    scan_parser.add_argument("--output", default="Docs/AgentLogs/YamlDesyncLedger_1402.json")
    scan_parser.set_defaults(func=scan)

    backup_plan = sub.add_parser("backup-plan")
    backup_plan.add_argument("--output", default="Docs/AgentLogs/BackupPlan_1402.json")
    backup_plan.set_defaults(func=write_backup_plan)

    backup_exec = sub.add_parser("backup-execute")
    backup_exec.add_argument("--output", default="Docs/AgentLogs/BackupExecution_1402.json")
    backup_exec.set_defaults(func=execute_backup)

    validate_parser = sub.add_parser("validate")
    validate_parser.add_argument("--full-scope", action="store_true")
    validate_parser.add_argument("--strict-existing-tabs", action="store_true")
    validate_parser.add_argument("--output", default="Docs/AgentLogs/YamlValidation_1402.json")
    validate_parser.set_defaults(func=validate)

    dry_run_parser = sub.add_parser("dry-run")
    dry_run_parser.add_argument("--ledger", default="Docs/AgentLogs/YamlDesyncLedger_1402.json")
    dry_run_parser.add_argument("--output", default="Docs/AgentLogs/YamlDryRun_1402.json")
    dry_run_parser.set_defaults(func=dry_run)

    self_test_parser = sub.add_parser("self-test")
    self_test_parser.add_argument("--output", default="Docs/AgentLogs/YamlParserFuzzer_1402.json")
    self_test_parser.set_defaults(func=self_test)

    final_parser = sub.add_parser("final-report")
    final_parser.add_argument("--output", default="Docs/Reports/YAML_MIGRATION_REPORT_1402.json")
    final_parser.set_defaults(func=final_report)

    args = parser.parse_args(argv)
    return int(args.func(args))


if __name__ == "__main__":
    raise SystemExit(main())
