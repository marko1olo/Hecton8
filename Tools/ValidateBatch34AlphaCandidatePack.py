#!/usr/bin/env python3
"""Validate promoted Batch34 Unity-visible alpha candidate source pack."""

from __future__ import annotations

import json
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
MANIFEST_PATH = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/GeminiBatch34AlphaCandidates_Manifest.json"
SOURCE_ATLAS_MANIFEST = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/GeminiBatch34SourceAtlases_Manifest.json"
SOURCE_ALPHA_MANIFEST = ROOT / "Docs/GeneratedAssets/Gemini/Outputs/Batch34_TextureExpansion/AlphaCandidates/Batch34_SourceAtlasAlphaCandidates_Manifest.json"
MAX_BYTES = int(2.25 * 1024 * 1024)
REJECTED_HIGH_COVERAGE = {"B34-3437", "B34-3449"}
ACCEPT_STATUS = "ALPHA_CANDIDATE_STATIC_REVIEW_REQUIRED"


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def project_path(raw: str) -> Path:
    path = Path(raw)
    return path if path.is_absolute() else ROOT / path


def load_manifest_entries(path: Path, schema: str, errors: list[str], label: str) -> tuple[dict[str, dict], dict]:
    if not path.exists():
        errors.append(f"missing {label} manifest: {display_path(path)}")
        return {}, {}

    payload = json.loads(path.read_text(encoding="utf-8-sig"))
    if payload.get("schema") != schema:
        errors.append(f"unexpected {label} schema: {payload.get('schema')}")

    entries: dict[str, dict] = {}
    duplicates: set[str] = set()
    for index, entry in enumerate(payload.get("entries", []) or []):
        entry_id = str(entry.get("id", "")).strip()
        if not entry_id:
            errors.append(f"{label} entry[{index}] missing id")
            continue
        if entry_id in entries:
            duplicates.add(entry_id)
            continue
        entries[entry_id] = entry

    for entry_id in sorted(duplicates):
        errors.append(f"{label} manifest duplicate id: {entry_id}")

    return entries, payload


def main() -> int:
    errors: list[str] = []
    warnings: list[str] = []
    entry_count = 0
    skipped_count = 0
    promoted_ids: set[str] = set()
    skipped_ids: set[str] = set()
    source_entries, _ = load_manifest_entries(
        SOURCE_ATLAS_MANIFEST,
        "hecton8.batch34.source_atlas_unity_pack.v1",
        errors,
        "source atlas",
    )
    source_alpha_entries, _ = load_manifest_entries(
        SOURCE_ALPHA_MANIFEST,
        "hecton8.batch34.source_atlas_alpha_candidates.v1",
        errors,
        "source alpha",
    )

    if not MANIFEST_PATH.exists():
        errors.append(f"missing alpha candidate manifest: {display_path(MANIFEST_PATH)}")
    else:
        payload = json.loads(MANIFEST_PATH.read_text(encoding="utf-8-sig"))
        if payload.get("schema") != "hecton8.batch34.alpha_candidate_unity_pack.v1":
            errors.append(f"unexpected Unity alpha schema: {payload.get('schema')}")
        if project_path(str(payload.get("sourceAlphaManifest", ""))).resolve() != SOURCE_ALPHA_MANIFEST.resolve():
            errors.append(
                "Unity alpha pack must point at source alpha manifest: "
                f"actual={payload.get('sourceAlphaManifest', '')}"
            )
        if project_path(str(payload.get("sourceAtlasManifest", ""))).resolve() != SOURCE_ATLAS_MANIFEST.resolve():
            errors.append(
                "Unity alpha pack must point at source atlas manifest: "
                f"actual={payload.get('sourceAtlasManifest', '')}"
            )
        if payload.get("productionBindingStatus") != "PENDING DECAL_SPLIT_OR_UV_BINDING":
            errors.append("Unity alpha pack must stay source-only until decal split or UV binding")

        for entry in payload.get("entries", []) or []:
            entry_count += 1
            entry_id = str(entry.get("id", "")).strip()
            if not entry_id:
                errors.append(f"entry[{entry_count - 1}] missing id")
                continue
            if entry_id in promoted_ids:
                errors.append(f"{entry_id}: duplicate promoted alpha candidate")
            promoted_ids.add(entry_id)

            source_entry = source_entries.get(entry_id)
            if source_entry is None:
                errors.append(f"{entry_id}: missing source atlas manifest entry")
            source_alpha_entry = source_alpha_entries.get(entry_id)
            if source_alpha_entry is None:
                errors.append(f"{entry_id}: missing source alpha manifest entry")
            elif str(source_alpha_entry.get("status", "")).strip() != ACCEPT_STATUS:
                errors.append(
                    f"{entry_id}: promoted alpha has non-accepted source status "
                    f"{source_alpha_entry.get('status', '')}"
                )

            source_atlas = str(entry.get("sourceAtlas", "")).strip()
            source_alpha = str(entry.get("sourceAlphaCandidate", "")).strip()
            if source_entry is not None and source_atlas != str(source_entry.get("source", "")).strip():
                errors.append(f"{entry_id}: sourceAtlas does not match source atlas manifest")
            if source_alpha_entry is not None:
                if source_atlas != str(source_alpha_entry.get("source", "")).strip():
                    errors.append(f"{entry_id}: sourceAtlas does not match source alpha manifest")
                if source_alpha != str(source_alpha_entry.get("alphaCandidate", "")).strip():
                    errors.append(f"{entry_id}: sourceAlphaCandidate does not match source alpha manifest")

            if source_atlas and not project_path(source_atlas).exists():
                errors.append(f"{entry_id}: missing source atlas file: {source_atlas}")
            if source_alpha and not project_path(source_alpha).exists():
                errors.append(f"{entry_id}: missing source alpha candidate file: {source_alpha}")

            alpha_path = project_path(str(entry.get("alphaCandidate", "")).strip())
            if not alpha_path.exists():
                errors.append(f"{entry_id}: missing alpha candidate file: {entry.get('alphaCandidate', '')}")
                continue
            if alpha_path.stat().st_size > MAX_BYTES:
                mb = alpha_path.stat().st_size / (1024 * 1024)
                warnings.append(f"{entry_id}: alpha candidate source is {mb:.2f} MB")
            with Image.open(alpha_path) as image:
                if image.width != image.height or image.width < 1024:
                    errors.append(f"{entry_id}: alpha candidate must be square and at least 1024, got {image.width}x{image.height}")
                if image.mode != "RGBA":
                    errors.append(f"{entry_id}: alpha candidate must be RGBA, got {image.mode}")
            stats = entry.get("alphaStats", {}) or {}
            alpha_non_zero = stats.get("alphaNonZeroPct")
            if isinstance(alpha_non_zero, (int, float)) and float(alpha_non_zero) > 95.0:
                errors.append(f"{entry_id}: alpha coverage too high for promoted candidate: {alpha_non_zero}")
            if str(entry.get("productionBindingStatus", "")).strip() != "PENDING DECAL_SPLIT_OR_UV_BINDING":
                warnings.append(f"{entry_id}: unexpected productionBindingStatus={entry.get('productionBindingStatus', '')}")

        for skipped in payload.get("skipped", []) or []:
            skipped_count += 1
            skipped_id = str(skipped.get("id", "")).strip()
            if not skipped_id:
                errors.append(f"skipped[{skipped_count - 1}] missing id")
                continue
            if skipped_id in skipped_ids:
                errors.append(f"{skipped_id}: duplicate skipped alpha candidate")
            skipped_ids.add(skipped_id)
            source_alpha_entry = source_alpha_entries.get(skipped_id)
            if source_alpha_entry is None:
                errors.append(f"{skipped_id}: skipped entry missing source alpha manifest entry")
            elif str(source_alpha_entry.get("status", "")).strip() == ACCEPT_STATUS:
                errors.append(f"{skipped_id}: accepted source alpha candidate was skipped")
        for rejected_id in REJECTED_HIGH_COVERAGE:
            if rejected_id in promoted_ids:
                errors.append(f"{rejected_id}: high-coverage reject was promoted")
            if source_alpha_entries and rejected_id not in skipped_ids:
                errors.append(f"{rejected_id}: high-coverage reject missing from skipped set")

    overlap = sorted(promoted_ids & skipped_ids)
    if overlap:
        errors.append(f"alpha ids appear in both entries and skipped: {', '.join(overlap)}")

    if source_alpha_entries:
        covered_ids = promoted_ids | skipped_ids
        missing = sorted(set(source_alpha_entries) - covered_ids)
        extra = sorted(covered_ids - set(source_alpha_entries))
        if missing:
            errors.append(f"source alpha ids missing from Unity alpha manifest: {', '.join(missing)}")
        if extra:
            errors.append(f"Unity alpha manifest has ids absent from source alpha manifest: {', '.join(extra)}")

    print("BATCH34_ALPHA_CANDIDATE_PACK_VALIDATOR")
    print(f"manifest={display_path(MANIFEST_PATH)}")
    print(f"entries={entry_count}")
    print(f"skipped={skipped_count}")
    print(f"errors={len(errors)}")
    print(f"warnings={len(warnings)}")
    for error in errors:
        print(f"ERROR {error}")
    for warning in warnings:
        print(f"WARN {warning}")
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
