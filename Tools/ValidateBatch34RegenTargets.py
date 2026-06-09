#!/usr/bin/env python3
"""Validate Batch34 targeted regen candidate handoff manifest."""

from __future__ import annotations

import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MANIFEST_PATH = ROOT / "Docs/GeneratedAssets/Gemini/Outputs/Batch34_TextureExpansion/RegenTargets/QA/Batch34_RegenTargets_IntakeManifest.json"
PROCESSOR_PATH = ROOT / "Tools/ProcessBatch34RegenTargets.py"
CONTACT_PATH = ROOT / "Docs/GeneratedAssets/Gemini/Outputs/Batch34_TextureExpansion/RegenTargets/QA/PREVIEW_Batch34_RegenTargets_Contact.png"

EXPECTED_VARIANTS = {
    ("B34-3409-R1", "limestone_ceiling_png_named"),
    ("B34-3409-R1", "limestone_ceiling_jpeg_timestamp"),
    ("B34-3418-R1", "viewport_glass_jpeg_timestamp"),
    ("B34-3407-R1", "iron_oxide_jpeg_timestamp"),
    ("B34-3417-R1", "amber_lens_png_named"),
    ("B34-3417-R1", "amber_lens_jpeg_timestamp"),
    ("B34-3439-V2", "spore_pods_unrequested_variant"),
}

EXPECTED_SELECTED = {
    ("B34-3409-R1", "limestone_ceiling_jpeg_timestamp"): "SELECTED_REGEN_SEAMLESS_SOURCE",
    ("B34-3418-R1", "viewport_glass_jpeg_timestamp"): "SELECTED_REGEN_ALPHA_SOURCE",
    ("B34-3417-R1", "amber_lens_png_named"): "SELECTED_CENTER_CROP_SOURCE",
}

EXPECTED_REJECTED = {
    ("B34-3409-R1", "limestone_ceiling_png_named"): "REJECT_REGEN_SEAMLESS_HERO_REPEAT",
    ("B34-3407-R1", "iron_oxide_jpeg_timestamp"): "HOLD_LOCAL_PATCH_ONLY_HERO_REPEAT",
    ("B34-3417-R1", "amber_lens_jpeg_timestamp"): "REJECT_REGEN_SEAMLESS_VERTICAL_SEAM",
    ("B34-3439-V2", "spore_pods_unrequested_variant"): "HOLD_ALTERNATE_NOT_TARGETED_EDGE_RISK",
}


def display(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def path_exists(project_path: object) -> bool:
    if not isinstance(project_path, str) or not project_path:
        return False
    return (ROOT / project_path).exists()


def load_manifest(errors: list[str]) -> dict:
    if not MANIFEST_PATH.exists():
        errors.append(f"missing regen target manifest: {display(MANIFEST_PATH)}")
        return {}
    return json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))


def validate_entry_files(entry: dict, errors: list[str]) -> None:
    key = (str(entry.get("id", "")), str(entry.get("variant", "")))
    for field in ("originalPath",):
        if not path_exists(entry.get(field)):
            errors.append(f"{key}: missing file for {field}: {entry.get(field, '')}")
    if entry.get("selected"):
        if not path_exists(entry.get("finalCandidatePath")):
            errors.append(f"{key}: selected entry missing finalCandidatePath")
    if "tilePreviewPath" in entry and not path_exists(entry.get("tilePreviewPath")):
        errors.append(f"{key}: missing tilePreviewPath")
    if "cleanedCandidatePath" in entry and not path_exists(entry.get("cleanedCandidatePath")):
        errors.append(f"{key}: missing cleanedCandidatePath")
    if "cleanedTilePreviewPath" in entry and not path_exists(entry.get("cleanedTilePreviewPath")):
        errors.append(f"{key}: missing cleanedTilePreviewPath")


def validate_manifest(data: dict, errors: list[str]) -> None:
    if data.get("schema") != "hecton8.batch34.regen_targets.intake.v2":
        errors.append(f"unexpected schema: {data.get('schema', '')}")
    if data.get("operatorPrompt") != "Docs/GeneratedAssets/Gemini/Prompts/Batch34/3406_TEXTURE_SOURCE_REGEN_TARGETS_20260608.md":
        errors.append("operatorPrompt must point at 3406 targeted regen prompt file")
    if data.get("contactSheet") != display(CONTACT_PATH):
        errors.append("contactSheet must use canonical regen target preview path")
    if not CONTACT_PATH.exists():
        errors.append(f"missing regen target contact sheet: {display(CONTACT_PATH)}")
    if not PROCESSOR_PATH.exists():
        errors.append(f"missing regen target processor: {display(PROCESSOR_PATH)}")

    entries = list(data.get("entries", []))
    keys = [(str(entry.get("id", "")), str(entry.get("variant", ""))) for entry in entries]
    if set(keys) != EXPECTED_VARIANTS:
        errors.append(f"regen target variants mismatch: expected={sorted(EXPECTED_VARIANTS)} actual={sorted(keys)}")
    if len(keys) != len(set(keys)):
        errors.append("regen target manifest contains duplicate id/variant entries")
    if any(entry.get("missing") for entry in entries):
        errors.append("regen target manifest contains missing download candidates")

    entries_by_key = {key: entry for key, entry in zip(keys, entries)}
    selected = {
        key: str(entry.get("decision", ""))
        for key, entry in entries_by_key.items()
        if bool(entry.get("selected"))
    }
    if selected != EXPECTED_SELECTED:
        errors.append(f"selected regen targets mismatch: expected={EXPECTED_SELECTED} actual={selected}")

    for key, expected_decision in EXPECTED_REJECTED.items():
        entry = entries_by_key.get(key)
        if entry is None:
            continue
        if entry.get("selected"):
            errors.append(f"{key}: rejected/hold variant must not be selected")
        if entry.get("decision") != expected_decision:
            errors.append(f"{key}: expected decision {expected_decision}, found {entry.get('decision', '')}")

    limestone = entries_by_key.get(("B34-3409-R1", "limestone_ceiling_jpeg_timestamp"), {})
    if limestone.get("broadSeamlessAccepted") is not True:
        errors.append("B34-3409 timestamp JPEG must be the only broad seamless accepted regen candidate")
    if not isinstance(limestone.get("seamMetrics"), dict):
        errors.append("B34-3409 timestamp JPEG missing seam metrics")

    viewport = entries_by_key.get(("B34-3418-R1", "viewport_glass_jpeg_timestamp"), {})
    edge = viewport.get("edgeContent", {})
    if not isinstance(edge, dict) or float(edge.get("edgeContentPct", 999.0)) > 1.0:
        errors.append("B34-3418 viewport atlas must keep edge content below 1%")

    amber = entries_by_key.get(("B34-3417-R1", "amber_lens_png_named"), {})
    if amber.get("broadSeamlessAccepted") is not False:
        errors.append("B34-3417 amber crop is a selected source but must not claim broad seamless acceptance")
    if "center_crop" not in str(amber.get("finalCandidatePath", "")):
        errors.append("B34-3417 selected source must use cleaned center crop final candidate")

    for entry in entries:
        validate_entry_files(entry, errors)

    selected_final = list(data.get("selectedFinalCandidates", []))
    selected_final_keys = {
        (str(entry.get("id", "")), str(entry.get("variant", ""))) for entry in selected_final
    }
    if selected_final_keys != set(EXPECTED_SELECTED.keys()):
        errors.append("selectedFinalCandidates does not mirror selected entries")


def main() -> int:
    errors: list[str] = []
    data = load_manifest(errors)
    if data:
        validate_manifest(data, errors)

    entries = list(data.get("entries", [])) if data else []
    selected = [entry for entry in entries if entry.get("selected")]
    print("BATCH34_REGEN_TARGETS_VALIDATOR")
    print(f"manifest={display(MANIFEST_PATH)}")
    print(f"entries={len(entries)}")
    print(f"selected={len(selected)}")
    print(f"errors={len(errors)}")
    for error in errors:
        print(f"ERROR {error}")
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
