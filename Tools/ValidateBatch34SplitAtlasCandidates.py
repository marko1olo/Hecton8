#!/usr/bin/env python3
"""Validate Batch34 split atlas transparent island candidates."""

from __future__ import annotations

import json
from pathlib import Path

import numpy as np
from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SplitAtlasCandidates_20260608/GeminiBatch34SplitAtlasCandidates_Manifest.json"
CURATION_MANIFEST = ROOT / "Docs/GeneratedAssets/Gemini/Outputs/Batch34_TextureExpansion/QA/Batch34_TextureExpansion_CurationManifest.json"
EXPECTED_IDS = {"B34-3424", "B34-3438", "B34-3440", "B34-3443", "B34-3444", "B34-3447"}
EXPECTED_SOURCE_STATUSES = {"PAD_OR_SPLIT_BEFORE_IMPORT", "MANUAL_SPLIT_BEFORE_IMPORT"}
PCT_EPSILON = 0.01


def display(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def project_path(raw: str) -> Path:
    path = Path(raw)
    return path if path.is_absolute() else ROOT / path


def manifest_float(value: object, default: float = -1.0) -> float:
    if value is None:
        return default
    try:
        return float(value)
    except (TypeError, ValueError):
        return default


def manifest_int(value: object, default: int = -1) -> int:
    if value is None:
        return default
    try:
        return int(value)
    except (TypeError, ValueError):
        return default


def load_curation(errors: list[str]) -> dict[str, dict]:
    if not CURATION_MANIFEST.exists():
        errors.append(f"missing curation manifest: {display(CURATION_MANIFEST)}")
        return {}

    payload = json.loads(CURATION_MANIFEST.read_text(encoding="utf-8-sig"))
    if payload.get("schema") != "hecton8.batch34.texture_expansion_curation.v1":
        errors.append(f"unexpected curation schema: {payload.get('schema')}")

    entries: dict[str, dict] = {}
    for index, entry in enumerate(payload.get("entries", []) or []):
        entry_id = str(entry.get("id", "")).strip()
        if not entry_id:
            errors.append(f"curation entry[{index}] missing id")
            continue
        if entry_id in entries:
            errors.append(f"duplicate curation id: {entry_id}")
            continue
        entries[entry_id] = entry
    return entries


def require_needs_work_curation(entry: dict, curation_entries: dict[str, dict], errors: list[str]) -> None:
    job_id = str(entry.get("id", "")).strip()
    curation_entry = curation_entries.get(job_id)
    if curation_entry is None:
        errors.append(f"{job_id}: missing source curation entry")
        return

    for key in ("title", "sourceType", "family"):
        value = str(entry.get(key, "")).strip()
        curation_value = str(curation_entry.get(key, "")).strip()
        if value != curation_value:
            errors.append(f"{job_id}: {key} disagrees with curation: manifest={value} curation={curation_value}")

    status = str(entry.get("sourceCurationStatus", "")).strip()
    curation_status = str(curation_entry.get("curationStatus", "")).strip()
    if status != curation_status:
        errors.append(
            f"{job_id}: sourceCurationStatus disagrees with curation: "
            f"manifest={status} curation={curation_status}"
        )
    if curation_status not in EXPECTED_SOURCE_STATUSES:
        errors.append(f"{job_id}: split source must be needs-work curation, got {curation_status}")

    source = str(entry.get("source", "")).strip()
    curation_source = str(curation_entry.get("baseColorCandidatePath", "")).strip()
    if source != curation_source:
        errors.append(f"{job_id}: source path disagrees with curation baseColorCandidatePath")


def main() -> int:
    errors: list[str] = []
    warnings: list[str] = []
    total_islands = 0

    if not MANIFEST.exists():
        errors.append(f"missing split manifest: {display(MANIFEST)}")
        payload = {}
    else:
        payload = json.loads(MANIFEST.read_text(encoding="utf-8-sig"))
        if payload.get("schema") != "hecton8.batch34.split_atlas_candidates.v1":
            errors.append(f"unexpected schema: {payload.get('schema')}")
        if payload.get("productionBindingStatus") != "SPLIT_ISLAND_CANDIDATE_PENDING_UV_BINDING":
            errors.append(f"unexpected productionBindingStatus: {payload.get('productionBindingStatus')}")
        if project_path(str(payload.get("sourceCurationManifest", ""))).resolve() != CURATION_MANIFEST.resolve():
            errors.append(
                "split atlas manifest must point at Batch34 curation manifest: "
                f"actual={payload.get('sourceCurationManifest', '')}"
            )
        preview_raw = str(payload.get("preview", "")).strip()
        if not preview_raw:
            errors.append("split manifest missing preview")
        else:
            preview = project_path(preview_raw)
            if not preview.exists():
                errors.append(f"missing split preview: {display(preview)}")

    entries = payload.get("entries", []) if isinstance(payload, dict) else []
    ids = {str(entry.get("id", "")).strip() for entry in entries}
    missing = sorted(EXPECTED_IDS - ids)
    if missing:
        errors.append(f"missing expected split atlas entries: {missing}")
    unexpected = sorted(ids - EXPECTED_IDS)
    if unexpected:
        errors.append(f"unexpected split atlas entries: {unexpected}")

    curation_entries = load_curation(errors)

    for entry in entries:
        job_id = str(entry.get("id", "")).strip()
        require_needs_work_curation(entry, curation_entries, errors)
        islands = entry.get("islands", []) or []
        if not islands:
            errors.append(f"{job_id}: no split islands produced")
            continue
        total_islands += len(islands)
        declared_count = int(entry.get("islandCount", 0) or 0)
        if declared_count != len(islands):
            errors.append(f"{job_id}: islandCount={declared_count} but manifest has {len(islands)} islands")
        if len(islands) > 40:
            warnings.append(f"{job_id}: high island count {len(islands)}")

        seen_paths: set[str] = set()
        for expected_index, island in enumerate(islands):
            rel_path = str(island.get("path", "")).strip()
            if not rel_path:
                errors.append(f"{job_id}: island missing path")
                continue
            index = manifest_int(island.get("index"))
            if index != expected_index:
                errors.append(f"{job_id}: island index drift at {rel_path}: expected={expected_index} actual={index}")
            if rel_path in seen_paths:
                errors.append(f"{job_id}: duplicate island path {rel_path}")
            seen_paths.add(rel_path)

            path = project_path(rel_path)
            if not path.exists():
                errors.append(f"{job_id}: missing island image {rel_path}")
                continue
            with Image.open(path) as image:
                if image.mode != "RGBA":
                    errors.append(f"{rel_path}: expected RGBA, got {image.mode}")
                if image.width != image.height:
                    errors.append(f"{rel_path}: expected square padded output, got {image.width}x{image.height}")
                if image.width < 256 or image.width > 1024:
                    errors.append(f"{rel_path}: unexpected size {image.width}")
                if manifest_int(island.get("width"), 0) != image.width or manifest_int(island.get("height"), 0) != image.height:
                    errors.append(
                        f"{rel_path}: manifest size stale: manifest={island.get('width')}x{island.get('height')} "
                        f"actual={image.width}x{image.height}"
                    )
                alpha = np.array(image.convert("RGBA").getchannel("A"))

            nonzero_pct = float(np.count_nonzero(alpha)) * 100.0 / max(1, alpha.size)
            manifest_alpha_pct = manifest_float(island.get("alphaNonZeroPct"))
            if abs(manifest_alpha_pct - nonzero_pct) > PCT_EPSILON:
                errors.append(
                    f"{rel_path}: alphaNonZeroPct stale: manifest={manifest_alpha_pct:.3f} actual={nonzero_pct:.3f}"
                )
            if nonzero_pct < 0.25:
                errors.append(f"{rel_path}: nearly empty alpha {nonzero_pct:.3f}%")

            edge = np.concatenate([alpha[:8, :].ravel(), alpha[-8:, :].ravel(), alpha[:, :8].ravel(), alpha[:, -8:].ravel()])
            edge_pct = float(np.count_nonzero(edge)) * 100.0 / max(1, edge.size)
            manifest_edge_pct = manifest_float(island.get("edgeAlphaNonZeroPct"))
            if abs(manifest_edge_pct - edge_pct) > PCT_EPSILON:
                errors.append(
                    f"{rel_path}: edgeAlphaNonZeroPct stale: manifest={manifest_edge_pct:.3f} actual={edge_pct:.3f}"
                )
            if edge_pct > 2.0:
                errors.append(f"{rel_path}: alpha touches padded edge {edge_pct:.3f}%")

            source_x = manifest_int(island.get("sourceX"))
            source_y = manifest_int(island.get("sourceY"))
            source_w = manifest_int(island.get("sourceW"), 0)
            source_h = manifest_int(island.get("sourceH"), 0)
            if source_x < 0 or source_y < 0 or source_w <= 0 or source_h <= 0:
                errors.append(f"{rel_path}: invalid source bounds")
            elif source_x + source_w > 1024 or source_y + source_h > 1024:
                errors.append(f"{rel_path}: source bounds exceed 1024 source atlas")

    print("BATCH34_SPLIT_ATLAS_CANDIDATES_VALIDATOR")
    print(f"manifest={display(MANIFEST)}")
    print(f"entries={len(entries)}")
    print(f"islands={total_islands}")
    print(f"errors={len(errors)}")
    print(f"warnings={len(warnings)}")
    for error in errors:
        print(f"ERROR {error}")
    for warning in warnings:
        print(f"WARN {warning}")
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
