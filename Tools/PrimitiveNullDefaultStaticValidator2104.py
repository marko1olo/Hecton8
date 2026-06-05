#!/usr/bin/env python3
"""
Static primitive/null/default material debt validator for HECTON-8 Batch21 agent 2104.

This tool is read-only for Unity project assets. It scans source YAML/text files and writes
only report artifacts requested by command-line arguments.
"""

from __future__ import annotations

import argparse
import csv
import json
import re
import sys
from collections import Counter
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Iterable


TOOL_NAME = "PrimitiveNullDefaultStaticValidator2104"
EVIDENCE_CLASS = "STATIC_SOURCE"
VISUAL_ACCEPTANCE = "PENDING VERIFICATION"
BUILTIN_RESOURCE_GUID = "0000000000000000e000000000000000"

SCANNED_EXTENSIONS = {".unity", ".prefab", ".mat"}
FORBIDDEN_OUTPUT_ROOTS = {
    "Assets",
    "Packages",
    "ProjectSettings",
    "Library",
    "Temp",
    "UserSettings",
}

PRIMITIVE_FILE_IDS = {
    "10202": "Cube",
    "10206": "Cylinder",
    "10207": "Sphere",
    "10208": "Capsule",
    "10209": "Plane",
    "10210": "Quad",
}

BASE_TEXTURE_PROPS = {
    "_BaseMap",
    "_MainTex",
    "_BaseColorMap",
    "_AlbedoMap",
    "_DiffuseMap",
}

SURFACE_TEXTURE_PROPS = {
    "_BumpMap",
    "_NormalMap",
    "_MaskMap",
    "_ORMMap",
    "_OrmMap",
    "_OcclusionRoughnessMetallicMap",
    "_MetallicGlossMap",
    "_SpecGlossMap",
    "_OcclusionMap",
    "_RoughnessMap",
    "_SmoothnessMap",
    "_DetailAlbedoMap",
    "_DetailNormalMap",
    "_EmissionMap",
}

PLACEHOLDER_TOKENS = (
    "placeholder",
    "proceduralplaceholder",
    "worldproceduralproxy",
    "proxy",
    "default-material",
    "default material",
    "fallback",
    "primitive",
    "debug",
    "mock",
    "temp",
    "todo",
    "lowpoly",
    "flatcolor",
    "flat_color",
)

PACKAGE_OR_VENDOR_TOKENS = (
    "Packages/",
    "Assets/Plugins/",
    "Assets/_ThirdParty/",
    "Assets/AstarPathfindingProject/",
    "Assets/External/",
    "Assets/Vendor/",
)

SURFACE_ROUTE_TOKENS = (
    "surface",
    "sky",
    "ocean",
    "water",
    "shore",
    "coast",
    "photic",
    "shallows",
    "moon",
    "aegir",
    "terrain",
    "rock",
    "basalt",
    "coral",
    "kelp",
    "seabed",
    "reef",
    "hero",
    "medium",
)

PRODUCT_ROUTE_TOKENS = (
    "productface",
    "product-face",
    "first20",
    "route_target",
    "final",
    "prefab",
    "player",
    "cockpit",
    "visor",
    "item_",
    "tool",
    "construction",
    "building",
)

DIAGNOSTIC_TOKENS = (
    "editor",
    "debug",
    "test",
    "gizmo",
    "prototype",
    "devonly",
    "diagnostic",
)

SEVERITY_RANK = {
    "CRITICAL": 0,
    "HIGH": 1,
    "MEDIUM": 2,
    "LOW": 3,
}

FIELDNAMES = [
    "severity",
    "status",
    "evidence_class",
    "visual_acceptance",
    "issue_type",
    "route_band",
    "path",
    "line",
    "object_hint",
    "slot_or_property",
    "file_id",
    "guid",
    "resolved_path",
    "detail",
    "recommended_next_proof",
]

REF_RE = re.compile(r"\{(?P<body>[^}]*)\}")
FILE_ID_RE = re.compile(r"fileID:\s*(?P<file_id>-?\d+)")
GUID_RE = re.compile(r"guid:\s*(?P<guid>[0-9a-fA-F]{32})")
TYPE_RE = re.compile(r"type:\s*(?P<type>-?\d+)")
META_GUID_RE = re.compile(r"^guid:\s*([0-9a-fA-F]{32})\s*$", re.MULTILINE)
PROP_RE = re.compile(r"^\s*-\s+(?P<prop>[A-Za-z0-9_]+):\s*$")
NAME_RE = re.compile(r"^\s*m_Name:\s*(?P<name>.+?)\s*$")


@dataclass(frozen=True)
class Finding:
    severity: str
    status: str
    evidence_class: str
    visual_acceptance: str
    issue_type: str
    route_band: str
    path: str
    line: int
    object_hint: str
    slot_or_property: str
    file_id: str
    guid: str
    resolved_path: str
    detail: str
    recommended_next_proof: str

    def as_dict(self) -> dict[str, str | int]:
        return {
            "severity": self.severity,
            "status": self.status,
            "evidence_class": self.evidence_class,
            "visual_acceptance": self.visual_acceptance,
            "issue_type": self.issue_type,
            "route_band": self.route_band,
            "path": self.path,
            "line": self.line,
            "object_hint": self.object_hint,
            "slot_or_property": self.slot_or_property,
            "file_id": self.file_id,
            "guid": self.guid,
            "resolved_path": self.resolved_path,
            "detail": self.detail,
            "recommended_next_proof": self.recommended_next_proof,
        }


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=".", help="Project root. Default: current directory.")
    parser.add_argument(
        "--target",
        action="append",
        dest="targets",
        help="File or directory to scan. Repeatable. Defaults to active scene plus first-party prefab/material roots.",
    )
    parser.add_argument("--json", required=True, help="Output JSON report path under Docs.")
    parser.add_argument("--csv", required=True, help="Output CSV finding path under Docs.")
    parser.add_argument("--markdown", required=True, help="Output Markdown report path under Docs.")
    parser.add_argument(
        "--fail-on-critical",
        action="store_true",
        help="Exit code 2 when CRITICAL rows exist. Default is report-only exit 0.",
    )
    return parser.parse_args(argv)


def normalize_rel(path: Path, root: Path) -> str:
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return path.resolve().as_posix()


def is_under(path: Path, root: Path) -> bool:
    try:
        path.resolve().relative_to(root.resolve())
        return True
    except ValueError:
        return False


def validate_output_path(path: Path, root: Path) -> None:
    resolved = path.resolve()
    docs_root = (root / "Docs").resolve()
    if not is_under(resolved, docs_root):
        raise SystemExit(f"Refusing to write report outside Docs: {path}")
    for forbidden in FORBIDDEN_OUTPUT_ROOTS:
        forbidden_root = (root / forbidden).resolve()
        if is_under(resolved, forbidden_root):
            raise SystemExit(f"Refusing to write report under forbidden project root: {path}")


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="ignore")


def default_targets(root: Path) -> list[Path]:
    return [
        root / "Assets/_Project/Scenes/02_HECTON_WORLD.unity",
        root / "Assets/_Project/Prefabs",
        root / "Assets/_Project/Art/Materials",
        root / "Assets/_Project/Materials",
    ]


def iter_scan_files(root: Path, targets: Iterable[str] | None) -> list[Path]:
    raw_targets = [root / target for target in targets] if targets else default_targets(root)
    files: list[Path] = []
    seen: set[Path] = set()
    for target in raw_targets:
        if not target.exists():
            continue
        candidates = [target] if target.is_file() else target.rglob("*")
        for candidate in candidates:
            if not candidate.is_file():
                continue
            if candidate.suffix.lower() not in SCANNED_EXTENSIONS:
                continue
            resolved = candidate.resolve()
            if resolved in seen:
                continue
            seen.add(resolved)
            files.append(candidate)
    return sorted(files, key=lambda p: normalize_rel(p, root).lower())


def build_guid_index(root: Path) -> dict[str, str]:
    guid_index: dict[str, str] = {}
    for base_name in ("Assets", "Packages"):
        base = root / base_name
        if not base.exists():
            continue
        for meta_path in base.rglob("*.meta"):
            try:
                text = read_text(meta_path)
            except OSError:
                continue
            match = META_GUID_RE.search(text)
            if not match:
                continue
            asset_path = meta_path.with_suffix("")
            guid_index[match.group(1).lower()] = normalize_rel(asset_path, root)
    return guid_index


def parse_ref_blob(line: str) -> tuple[str, str, str]:
    match = REF_RE.search(line)
    body = match.group("body") if match else line
    file_id_match = FILE_ID_RE.search(body)
    guid_match = GUID_RE.search(body)
    type_match = TYPE_RE.search(body)
    return (
        file_id_match.group("file_id") if file_id_match else "",
        guid_match.group("guid").lower() if guid_match else "",
        type_match.group("type") if type_match else "",
    )


def line_indent(line: str) -> int:
    return len(line) - len(line.lstrip(" "))


def nearest_object_hint(lines: list[str], index: int) -> str:
    start = max(0, index - 120)
    for line in reversed(lines[start : index + 1]):
        match = NAME_RE.match(line)
        if match:
            return match.group("name").strip()
    for line in reversed(lines[start : index + 1]):
        stripped = line.strip()
        if stripped.startswith("--- !u!"):
            return stripped
    return ""


def contains_any(text: str, tokens: Iterable[str]) -> bool:
    lower = text.lower()
    return any(token.lower() in lower for token in tokens)


def route_band_for(path: str, object_hint: str, resolved_path: str, slot: str = "") -> str:
    joined = " ".join([path, object_hint, resolved_path, slot])
    if contains_any(joined, DIAGNOSTIC_TOKENS):
        return "diagnostic_only_candidate"
    if contains_any(joined, SURFACE_ROUTE_TOKENS):
        return "surface_sky_photic_medium_product_face"
    if contains_any(joined, PRODUCT_ROUTE_TOKENS):
        return "product_face"
    if contains_any(joined, PLACEHOLDER_TOKENS):
        return "placeholder_proxy_candidate"
    return "unknown_candidate"


def next_proof_for(issue_type: str, source_kind: str) -> str:
    if source_kind == "active_scene":
        return "Unity owner must inspect active scene renderer/material binding, replace debt, then provide real scene capture and profiler artifact."
    if issue_type.startswith("UNRESOLVED"):
        return "Unity owner must resolve GUID/import state in Editor and provide importer or scene evidence; static path scan cannot close this."
    if source_kind == "material":
        return "Unity owner must inspect material role in Editor, bind authored texture/PBR replacement if needed, then capture route proof."
    return "Unity owner must inspect prefab instance and overrides in Editor before closing this static finding."


def severity_for(issue_type: str, source_kind: str, route_band: str) -> str:
    if route_band == "diagnostic_only_candidate":
        return "LOW"
    if source_kind == "active_scene" and issue_type in {
        "BUILTIN_PRIMITIVE_MESH_REF",
        "NULL_RENDERER_MATERIAL_SLOT",
        "DEFAULT_OR_PACKAGE_MATERIAL_REF",
        "PLACEHOLDER_OR_PROXY_MATERIAL_REF",
        "UNRESOLVED_MATERIAL_GUID",
    }:
        return "CRITICAL"
    if route_band == "surface_sky_photic_medium_product_face":
        if issue_type in {
            "BUILTIN_PRIMITIVE_MESH_REF",
            "NULL_RENDERER_MATERIAL_SLOT",
            "DEFAULT_OR_PACKAGE_MATERIAL_REF",
            "PLACEHOLDER_OR_PROXY_MATERIAL_REF",
            "UNRESOLVED_MATERIAL_GUID",
            "UNRESOLVED_TEXTURE_GUID",
            "EMPTY_BASE_TEXTURE_SLOT",
        }:
            return "CRITICAL"
    if route_band in {"product_face", "placeholder_proxy_candidate"}:
        return "HIGH"
    if issue_type.startswith("UNRESOLVED"):
        return "HIGH"
    return "MEDIUM"


def material_path_flags(path: str) -> list[str]:
    flags: list[str] = []
    normalized = path.replace("\\", "/")
    lower = normalized.lower()
    if any(normalized.startswith(token) for token in PACKAGE_OR_VENDOR_TOKENS):
        flags.append("package_or_vendor")
    if contains_any(lower, PLACEHOLDER_TOKENS):
        flags.append("placeholder_or_proxy")
    name = Path(normalized).name.lower()
    if name in {"default.mat", "default-material.mat", "default material.mat"} or "default" in name:
        flags.append("default_named")
    return flags


def make_finding(
    *,
    issue_type: str,
    source_kind: str,
    path: str,
    line: int,
    object_hint: str,
    slot_or_property: str = "",
    file_id: str = "",
    guid: str = "",
    resolved_path: str = "",
    detail: str,
    status: str = "OPEN",
) -> Finding:
    route_band = route_band_for(path, object_hint, resolved_path, slot_or_property)
    severity = severity_for(issue_type, source_kind, route_band)
    return Finding(
        severity=severity,
        status=status,
        evidence_class=EVIDENCE_CLASS,
        visual_acceptance=VISUAL_ACCEPTANCE,
        issue_type=issue_type,
        route_band=route_band,
        path=path,
        line=line,
        object_hint=object_hint,
        slot_or_property=slot_or_property,
        file_id=file_id,
        guid=guid,
        resolved_path=resolved_path,
        detail=detail,
        recommended_next_proof=next_proof_for(issue_type, source_kind),
    )


def scan_scene_or_prefab(path: Path, root: Path, guid_index: dict[str, str]) -> list[Finding]:
    rel_path = normalize_rel(path, root)
    source_kind = "active_scene" if path.suffix.lower() == ".unity" else "prefab"
    text = read_text(path)
    lines = text.splitlines()
    findings: list[Finding] = []

    for index, line in enumerate(lines):
        if "m_Mesh:" in line:
            file_id, guid, _type = parse_ref_blob(line)
            if guid == BUILTIN_RESOURCE_GUID and file_id in PRIMITIVE_FILE_IDS:
                primitive_name = PRIMITIVE_FILE_IDS[file_id]
                findings.append(
                    make_finding(
                        issue_type="BUILTIN_PRIMITIVE_MESH_REF",
                        source_kind=source_kind,
                        path=rel_path,
                        line=index + 1,
                        object_hint=nearest_object_hint(lines, index),
                        slot_or_property="m_Mesh",
                        file_id=file_id,
                        guid=guid,
                        resolved_path=f"Unity built-in resource/{primitive_name}",
                        detail=f"Static YAML references Unity built-in primitive mesh {primitive_name}.",
                    )
                )

        if line.strip() != "m_Materials:":
            continue

        base_indent = line_indent(line)
        slot_index = 0
        probe = index + 1
        while probe < len(lines):
            material_line = lines[probe]
            stripped = material_line.strip()
            if stripped and line_indent(material_line) <= base_indent and not stripped.startswith("-"):
                break
            if stripped.startswith("-"):
                file_id, guid, _type = parse_ref_blob(material_line)
                slot = f"m_Materials[{slot_index}]"
                object_hint = nearest_object_hint(lines, probe)
                if file_id == "0" or (not guid and file_id in {"", "0"}):
                    findings.append(
                        make_finding(
                            issue_type="NULL_RENDERER_MATERIAL_SLOT",
                            source_kind=source_kind,
                            path=rel_path,
                            line=probe + 1,
                            object_hint=object_hint,
                            slot_or_property=slot,
                            file_id=file_id,
                            guid=guid,
                            detail="Renderer material array contains a null slot in source YAML.",
                        )
                    )
                elif guid:
                    resolved = guid_index.get(guid, "")
                    if not resolved:
                        findings.append(
                            make_finding(
                                issue_type="UNRESOLVED_MATERIAL_GUID",
                                source_kind=source_kind,
                                path=rel_path,
                                line=probe + 1,
                                object_hint=object_hint,
                                slot_or_property=slot,
                                file_id=file_id,
                                guid=guid,
                                detail="Renderer material GUID is not present in the scanned Assets/Packages meta index.",
                            )
                        )
                    else:
                        flags = material_path_flags(resolved)
                        if "package_or_vendor" in flags or "default_named" in flags:
                            findings.append(
                                make_finding(
                                    issue_type="DEFAULT_OR_PACKAGE_MATERIAL_REF",
                                    source_kind=source_kind,
                                    path=rel_path,
                                    line=probe + 1,
                                    object_hint=object_hint,
                                    slot_or_property=slot,
                                    file_id=file_id,
                                    guid=guid,
                                    resolved_path=resolved,
                                    detail=f"Renderer material resolves to {','.join(flags)} path/name.",
                                    status="CANDIDATE",
                                )
                            )
                        if "placeholder_or_proxy" in flags:
                            findings.append(
                                make_finding(
                                    issue_type="PLACEHOLDER_OR_PROXY_MATERIAL_REF",
                                    source_kind=source_kind,
                                    path=rel_path,
                                    line=probe + 1,
                                    object_hint=object_hint,
                                    slot_or_property=slot,
                                    file_id=file_id,
                                    guid=guid,
                                    resolved_path=resolved,
                                    detail=f"Renderer material path/name contains placeholder/proxy token: {resolved}",
                                    status="CANDIDATE",
                                )
                            )
                slot_index += 1
            probe += 1

    return findings


def scan_material(path: Path, root: Path, guid_index: dict[str, str]) -> list[Finding]:
    rel_path = normalize_rel(path, root)
    text = read_text(path)
    lines = text.splitlines()
    findings: list[Finding] = []

    flags = material_path_flags(rel_path)
    if flags:
        issue_type = "PLACEHOLDER_OR_PROXY_MATERIAL_ASSET" if "placeholder_or_proxy" in flags else "DEFAULT_OR_PACKAGE_MATERIAL_ASSET"
        findings.append(
            make_finding(
                issue_type=issue_type,
                source_kind="material",
                path=rel_path,
                line=1,
                object_hint=Path(rel_path).stem,
                slot_or_property="asset_path",
                resolved_path=rel_path,
                detail=f"Material asset path/name matches static debt tokens: {','.join(flags)}.",
                status="CANDIDATE",
            )
        )

    for index, line in enumerate(lines):
        prop_match = PROP_RE.match(line)
        if not prop_match:
            continue
        prop = prop_match.group("prop")
        if prop not in BASE_TEXTURE_PROPS and prop not in SURFACE_TEXTURE_PROPS:
            continue
        for probe in range(index + 1, min(index + 9, len(lines))):
            if "m_Texture:" not in lines[probe]:
                continue
            file_id, guid, _type = parse_ref_blob(lines[probe])
            if file_id == "0" and prop in BASE_TEXTURE_PROPS:
                findings.append(
                    make_finding(
                        issue_type="EMPTY_BASE_TEXTURE_SLOT",
                        source_kind="material",
                        path=rel_path,
                        line=probe + 1,
                        object_hint=Path(rel_path).stem,
                        slot_or_property=prop,
                        file_id=file_id,
                        detail="Primary/base texture property is empty in material source.",
                    )
                )
            elif guid and guid != BUILTIN_RESOURCE_GUID and guid not in guid_index:
                findings.append(
                    make_finding(
                        issue_type="UNRESOLVED_TEXTURE_GUID",
                        source_kind="material",
                        path=rel_path,
                        line=probe + 1,
                        object_hint=Path(rel_path).stem,
                        slot_or_property=prop,
                        file_id=file_id,
                        guid=guid,
                        detail="Texture GUID is not present in the scanned Assets/Packages meta index.",
                    )
                )
            break

    return findings


def sort_findings(findings: list[Finding]) -> list[Finding]:
    return sorted(
        findings,
        key=lambda row: (
            SEVERITY_RANK.get(row.severity, 99),
            row.path.lower(),
            row.line,
            row.issue_type,
            row.slot_or_property,
        ),
    )


def summarize(findings: list[Finding], scanned_files: list[Path], root: Path) -> dict[str, object]:
    return {
        "tool": TOOL_NAME,
        "evidence_class": EVIDENCE_CLASS,
        "visual_acceptance": VISUAL_ACCEPTANCE,
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "scanned_file_count": len(scanned_files),
        "scanned_files": [normalize_rel(path, root) for path in scanned_files],
        "total_findings": len(findings),
        "by_severity": dict(Counter(row.severity for row in findings)),
        "by_issue_type": dict(Counter(row.issue_type for row in findings)),
        "by_route_band": dict(Counter(row.route_band for row in findings)),
        "active_scene_findings": sum(1 for row in findings if row.path.endswith(".unity")),
        "notes": [
            "Static source evidence only.",
            "Visual acceptance, runtime binding, importer state, prefab override behavior, profiler cost, and build safety remain pending.",
            "Rows marked CANDIDATE require Unity-owner inspection before closure.",
        ],
    }


def write_csv(path: Path, findings: list[Finding]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=FIELDNAMES)
        writer.writeheader()
        for finding in findings:
            writer.writerow(finding.as_dict())


def write_json(path: Path, summary: dict[str, object], findings: list[Finding]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    payload = dict(summary)
    payload["findings"] = [finding.as_dict() for finding in findings]
    path.write_text(json.dumps(payload, indent=2, sort_keys=True), encoding="utf-8")


def markdown_table(findings: list[Finding], limit: int = 80) -> str:
    rows = [
        "| Severity | Issue | Path:Line | Hint | Slot/Property | Detail |",
        "| --- | --- | --- | --- | --- | --- |",
    ]
    for finding in findings[:limit]:
        hint = finding.object_hint.replace("|", "/")[:80]
        detail = finding.detail.replace("|", "/")[:110]
        rows.append(
            f"| {finding.severity} | {finding.issue_type} | `{finding.path}:{finding.line}` | "
            f"{hint} | `{finding.slot_or_property}` | {detail} |"
        )
    if len(findings) > limit:
        rows.append(f"| INFO | TRUNCATED | Full CSV |  |  | {len(findings) - limit} additional rows omitted from Markdown table. |")
    return "\n".join(rows)


def write_markdown(path: Path, summary: dict[str, object], findings: list[Finding], csv_path: Path, json_path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    by_severity = summary["by_severity"]
    by_issue_type = summary["by_issue_type"]
    by_route_band = summary["by_route_band"]
    lines = [
        "# 2104 Primitive Null Default Static Validator",
        "",
        "## Evidence Boundary",
        "",
        f"- Evidence class: `{EVIDENCE_CLASS}`.",
        f"- Visual acceptance: `{VISUAL_ACCEPTANCE}`.",
        "- This report is static text/YAML evidence only.",
        "- It does not prove runtime binding, import state, prefab override application, route visuals, frame cost, player safety, or build safety.",
        "- Do not close visual debt from this report alone.",
        "",
        "## Scope",
        "",
        f"- Scanned files: `{summary['scanned_file_count']}`.",
        f"- Total findings: `{summary['total_findings']}`.",
        f"- Active scene findings: `{summary['active_scene_findings']}`.",
        f"- CSV detail: `{csv_path.as_posix()}`.",
        f"- JSON detail: `{json_path.as_posix()}`.",
        "",
        "## Check Matrix",
        "",
        "| Check | Evidence | Closure rule |",
        "| --- | --- | --- |",
        "| Built-in primitive mesh refs | `m_Mesh` source references to Unity built-in primitive fileIDs | Replace with authored mesh/prefab, then Unity-owner route proof |",
        "| Null renderer material slots | `m_Materials` source entries with `fileID: 0` | Bind authored material or remove renderer slot, then Unity-owner route proof |",
        "| Default/package/proxy materials | Renderer or material asset paths with default/vendor/proxy tokens | Replace with route-owned authored material, then inspect active scene overrides |",
        "| Unresolved material GUIDs | Renderer material GUID absent from scanned meta index | Resolve import/meta state in Unity owner pass |",
        "| Unresolved texture GUIDs | Material texture GUID absent from scanned meta index | Resolve source texture/import state in Unity owner pass |",
        "| Empty base texture slots | Base/albedo texture property is null | Confirm material design intent or bind authored texture before route acceptance |",
        "",
        "## Summary",
        "",
        f"- By severity: `{json.dumps(by_severity, sort_keys=True)}`.",
        f"- By issue type: `{json.dumps(by_issue_type, sort_keys=True)}`.",
        f"- By route band: `{json.dumps(by_route_band, sort_keys=True)}`.",
        "",
        "## Top Findings",
        "",
        markdown_table(findings),
        "",
        "## Severity Rules",
        "",
        "- `CRITICAL`: active scene debt, or surface/sky/ocean/photic/medium/product-face route debt that can violate the visual floor.",
        "- `HIGH`: first-party product-face, placeholder/proxy, or unresolved source debt outside the active scene.",
        "- `MEDIUM`: first-party prefab/material debt without enough route tokens for higher routing.",
        "- `LOW`: diagnostic/editor/test candidates only.",
        "",
        "## Scalability Consequences",
        "",
        "- Low: remove primitives/nulls first; use authored low-cost meshes/materials that still preserve the route visual floor.",
        "- Middle: bind stable PBR roles and first-pass LODs so debt does not reappear through scene overrides.",
        "- High: upgrade surface/photic/medium route assets with richer material response after static debt is cleared.",
        "- Ultra: spend recovered cost on premium route detail only after authored assets and runtime proof exist.",
        "",
        "## Excluded Checks",
        "",
        "- No Unity Editor execution.",
        "- No import, Play Mode, profiler, scene mutation, prefab mutation, material mutation, or build command.",
        "- No screenshot, capture, or visual quality acceptance.",
        "- No claim that a path exists in source equals a bound runtime asset.",
        "",
        "## Unity Owner Handoff",
        "",
        "- Address `CRITICAL` active scene rows first.",
        "- Inspect scene overrides because source prefabs can pass while active scene instances still contain primitives or null/default slots.",
        "- Use the CSV as the queue; keep rows open until the Unity owner produces real scene/import/visual/profiler evidence.",
    ]
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main(argv: list[str]) -> int:
    args = parse_args(argv)
    root = Path(args.root).resolve()
    if not root.exists():
        raise SystemExit(f"Project root does not exist: {root}")

    json_path = (root / args.json).resolve()
    csv_path = (root / args.csv).resolve()
    markdown_path = (root / args.markdown).resolve()
    for output_path in (json_path, csv_path, markdown_path):
        validate_output_path(output_path, root)

    guid_index = build_guid_index(root)
    scan_files = iter_scan_files(root, args.targets)
    findings: list[Finding] = []
    for file_path in scan_files:
        suffix = file_path.suffix.lower()
        if suffix in {".unity", ".prefab"}:
            findings.extend(scan_scene_or_prefab(file_path, root, guid_index))
        elif suffix == ".mat":
            findings.extend(scan_material(file_path, root, guid_index))

    findings = sort_findings(findings)
    summary = summarize(findings, scan_files, root)
    write_csv(csv_path, findings)
    write_json(json_path, summary, findings)
    write_markdown(markdown_path, summary, findings, Path(args.csv), Path(args.json))

    print(
        json.dumps(
            {
                "tool": TOOL_NAME,
                "evidence_class": EVIDENCE_CLASS,
                "visual_acceptance": VISUAL_ACCEPTANCE,
                "scanned_file_count": len(scan_files),
                "total_findings": len(findings),
                "by_severity": summary["by_severity"],
                "outputs": {
                    "csv": args.csv,
                    "json": args.json,
                    "markdown": args.markdown,
                },
            },
            sort_keys=True,
        )
    )
    if args.fail_on_critical and any(row.severity == "CRITICAL" for row in findings):
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
