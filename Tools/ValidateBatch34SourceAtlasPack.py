#!/usr/bin/env python3
"""Validate the promoted Batch34 source-atlas Unity pack."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image, ImageOps


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_MANIFEST = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/GeminiBatch34SourceAtlases_Manifest.json"
CURATION_MANIFEST = ROOT / "Docs/GeneratedAssets/Gemini/Outputs/Batch34_TextureExpansion/QA/Batch34_TextureExpansion_CurationManifest.json"
ALLOWED_TYPES = {"DECAL_ATLAS", "UV_ATLAS", "PICKUP_ATLAS"}
READY_STATUSES = {"CURATED_READY_STATIC", "CURATED_READY_ALPHA_SOURCE"}
EXPECTED_IDS = {f"B34-{index}" for index in range(3401, 3451)}


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def project_path(raw: str | Path) -> Path:
    path = Path(raw)
    return path if path.is_absolute() else ROOT / path


def expected_bucket(entry: dict) -> str:
    if entry.get("curationStatus") == "CURATED_READY_ALPHA_SOURCE":
        return "AlphaMaskSources"

    source_type = entry.get("sourceType")
    if source_type == "DECAL_ATLAS":
        return "DecalAtlases"
    if source_type == "UV_ATLAS":
        return "UvAtlases"
    if source_type == "PICKUP_ATLAS":
        return "PickupAtlases"
    return ""


def load_curation(errors: list[str]) -> dict[str, dict]:
    if not CURATION_MANIFEST.exists():
        errors.append(f"missing curation manifest: {display_path(CURATION_MANIFEST)}")
        return {}

    payload = json.loads(CURATION_MANIFEST.read_text(encoding="utf-8-sig"))
    if payload.get("schema") != "hecton8.batch34.texture_expansion_curation.v1":
        errors.append(f"unexpected curation schema: {payload.get('schema')}")

    entries_by_id: dict[str, dict] = {}
    duplicate_ids: set[str] = set()
    for index, entry in enumerate(payload.get("entries", []) or []):
        entry_id = str(entry.get("id", "")).strip()
        if not entry_id:
            errors.append(f"curation entry[{index}] missing id")
            continue
        if entry_id in entries_by_id:
            duplicate_ids.add(entry_id)
            continue
        entries_by_id[entry_id] = entry

    for entry_id in sorted(duplicate_ids):
        errors.append(f"duplicate curation id: {entry_id}")

    missing = sorted(EXPECTED_IDS - set(entries_by_id))
    extra = sorted(set(entries_by_id) - EXPECTED_IDS)
    if missing:
        errors.append(f"curation manifest missing Batch34 ids: {', '.join(missing)}")
    if extra:
        errors.append(f"curation manifest has unexpected ids: {', '.join(extra)}")

    return entries_by_id


def validate_curation_link(
    entry: dict,
    curation_entries: dict[str, dict],
    index: int,
    errors: list[str],
) -> None:
    entry_id = str(entry.get("id", "")).strip()
    if not entry_id:
        return

    curation_entry = curation_entries.get(entry_id)
    if curation_entry is None:
        errors.append(f"entry[{index}] missing source curation entry: id={entry_id}")
        return

    source_type = str(entry.get("sourceType", "")).strip()
    curation_type = str(curation_entry.get("sourceType", "")).strip()
    if source_type != curation_type:
        errors.append(
            f"entry[{index}] sourceType disagrees with curation: id={entry_id} "
            f"pack={source_type} curation={curation_type}"
        )

    status = str(entry.get("curationStatus", "")).strip()
    curation_status = str(curation_entry.get("curationStatus", "")).strip()
    if status != curation_status:
        errors.append(
            f"entry[{index}] curationStatus disagrees with curation: id={entry_id} "
            f"pack={status} curation={curation_status}"
        )

    curated_path = str(curation_entry.get("curatedBaseColorPath", "")).strip()
    if not curated_path:
        errors.append(f"entry[{index}] curation missing curatedBaseColorPath: id={entry_id}")
        return

    source_name = Path(str(entry.get("source", ""))).name
    curated_name = Path(curated_path).name
    if source_name != curated_name:
        errors.append(
            f"entry[{index}] promoted source filename disagrees with curation: "
            f"id={entry_id} pack={source_name} curation={curated_name}"
        )


def validate_skipped_link(
    skipped_entry: dict,
    curation_entries: dict[str, dict],
    index: int,
    errors: list[str],
) -> None:
    skipped_id = str(skipped_entry.get("id", "")).strip()
    if not skipped_id:
        errors.append(f"skipped[{index}] missing id")
        return

    curation_entry = curation_entries.get(skipped_id)
    if curation_entry is None:
        errors.append(f"skipped[{index}] missing source curation entry: id={skipped_id}")
        return

    for key in ("sourceType", "curationStatus"):
        skipped_value = str(skipped_entry.get(key, "")).strip()
        curation_value = str(curation_entry.get(key, "")).strip()
        if skipped_value != curation_value:
            errors.append(
                f"skipped[{index}] {key} disagrees with curation: id={skipped_id} "
                f"pack={skipped_value} curation={curation_value}"
            )

    source_type = str(curation_entry.get("sourceType", "")).strip()
    status = str(curation_entry.get("curationStatus", "")).strip()
    if source_type in ALLOWED_TYPES and status in READY_STATUSES:
        errors.append(f"curated-ready source atlas was skipped: id={skipped_id}")


def validate(args: argparse.Namespace) -> int:
    manifest_path = project_path(args.manifest).resolve()
    errors: list[str] = []
    warnings: list[str] = []

    if not manifest_path.exists():
        print("BATCH34_SOURCE_ATLAS_PACK_VALIDATOR")
        print(f"manifest={display_path(manifest_path)}")
        print("errors=1")
        print("ERROR missing manifest")
        return 1

    payload = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    pack_root = manifest_path.parent
    entries = list(payload.get("entries", []) or [])
    skipped = list(payload.get("skipped", []) or [])
    seen_ids: set[str] = set()
    skipped_ids: set[str] = set()
    curation_entries = load_curation(errors)

    if payload.get("schema") != "hecton8.batch34.source_atlas_unity_pack.v1":
        errors.append(f"unexpected schema: {payload.get('schema')}")

    curation_path = project_path(str(payload.get("sourceCurationManifest", ""))).resolve()
    if curation_path != CURATION_MANIFEST.resolve():
        errors.append(
            "source atlas pack must point at Batch34 curation manifest: "
            f"actual={display_path(curation_path)}"
        )

    if payload.get("productionBindingStatus") != "PENDING SPLIT_OR_ALPHA_EXTRACTION":
        errors.append("source atlas pack must stay source-only until split/alpha extraction")

    preview = project_path(str(payload.get("preview", "")))
    if not preview.exists():
        errors.append(f"missing preview: {display_path(preview)}")

    for index, entry in enumerate(entries):
        entry_id = str(entry.get("id", "")).strip()
        source_type = str(entry.get("sourceType", "")).strip()
        status = str(entry.get("curationStatus", "")).strip()
        source = project_path(str(entry.get("source", "")))

        if not entry_id:
            errors.append(f"entry[{index}] missing id")
        elif entry_id in seen_ids:
            errors.append(f"entry[{index}] duplicate id: {entry_id}")
        seen_ids.add(entry_id)
        validate_curation_link(entry, curation_entries, index, errors)

        if source_type not in ALLOWED_TYPES:
            errors.append(f"entry[{index}] unsupported sourceType: id={entry_id} sourceType={source_type}")

        if status not in READY_STATUSES:
            errors.append(f"entry[{index}] non-ready curationStatus in Unity pack: id={entry_id} status={status}")

        if not source.exists():
            errors.append(f"entry[{index}] missing source: id={entry_id} source={display_path(source)}")
            continue

        try:
            source.relative_to(pack_root)
        except ValueError:
            errors.append(f"entry[{index}] source outside pack root: id={entry_id} source={display_path(source)}")

        bucket = expected_bucket(entry)
        if bucket and source.parent.name != bucket:
            errors.append(
                f"entry[{index}] wrong bucket: id={entry_id} expected={bucket} actual={source.parent.name}"
            )

        with Image.open(source) as image:
            normalized = ImageOps.exif_transpose(image)
            width, height = normalized.size
            mode = normalized.mode

        if width != height:
            errors.append(f"entry[{index}] source is not square: id={entry_id} size={width}x{height}")
        if width < args.min_size or height < args.min_size:
            errors.append(f"entry[{index}] source below min size: id={entry_id} size={width}x{height}")
        if mode not in {"RGB", "RGBA", "L"}:
            warnings.append(f"entry[{index}] unusual image mode: id={entry_id} mode={mode}")

        size_mb = source.stat().st_size / (1024 * 1024)
        if size_mb > args.max_mb:
            errors.append(f"entry[{index}] compressed source too large: id={entry_id} mb={size_mb:.2f}")

    skipped_hits: list[str] = []
    for index, skipped_entry in enumerate(skipped):
        skipped_id = str(skipped_entry.get("id", "")).strip()
        if not skipped_id:
            continue
        if skipped_id in skipped_ids:
            errors.append(f"duplicate skipped id: {skipped_id}")
        skipped_ids.add(skipped_id)
        validate_skipped_link(skipped_entry, curation_entries, index, errors)
        token = skipped_id.replace("-", "_")
        if list(pack_root.rglob(f"*{token}*")):
            skipped_hits.append(skipped_id)

    for skipped_id in sorted(set(skipped_hits)):
        errors.append(f"skipped entry was promoted into source atlas pack: {skipped_id}")

    overlap = sorted(seen_ids & skipped_ids)
    if overlap:
        errors.append(f"ids appear in both entries and skipped: {', '.join(overlap)}")

    if curation_entries:
        covered_ids = seen_ids | skipped_ids
        missing_from_pack = sorted(set(curation_entries) - covered_ids)
        extra_in_pack = sorted(covered_ids - set(curation_entries))
        if missing_from_pack:
            errors.append(f"curation ids missing from source atlas manifest: {', '.join(missing_from_pack)}")
        if extra_in_pack:
            errors.append(f"source atlas manifest has ids absent from curation: {', '.join(extra_in_pack)}")

    print("BATCH34_SOURCE_ATLAS_PACK_VALIDATOR")
    print(f"manifest={display_path(manifest_path)}")
    print(f"entries={len(entries)}")
    print(f"skipped={len(skipped)}")
    print(f"minSize={args.min_size}")
    print(f"maxMb={args.max_mb}")
    print(f"errors={len(errors)}")
    print(f"warnings={len(warnings)}")
    for error in errors:
        print(f"ERROR {error}")
    for warning in warnings:
        print(f"WARN {warning}")
    return 1 if errors else 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", default=str(DEFAULT_MANIFEST.relative_to(ROOT)))
    parser.add_argument("--min-size", type=int, default=1024)
    parser.add_argument("--max-mb", type=float, default=1.5)
    return validate(parser.parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
