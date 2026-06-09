#!/usr/bin/env python3
"""Validate an external/Gemini PBR lit preview sheet against its manifest."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
REQUIRED_MAPS = ("BaseColor", "NormalGL", "ARM_AO_Rough_Metal")
STALE_EPSILON_SECONDS = 0.5


def project_path(raw: str | Path) -> Path:
    path = Path(raw)
    return path if path.is_absolute() else ROOT / path


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def expected_size(asset_count: int, columns: int, tile_size: int, label_height: int, gap: int) -> tuple[int, int]:
    rows = (asset_count + columns - 1) // columns
    width = columns * tile_size + (columns - 1) * gap
    height = rows * (tile_size + label_height) + (rows - 1) * gap
    return width, height


def resolve_output_path(payload: dict, explicit_output: str) -> tuple[Path | None, str]:
    if explicit_output.strip():
        return project_path(explicit_output).resolve(), explicit_output
    raw_preview = str(payload.get("preview", "")).strip()
    if raw_preview:
        return project_path(raw_preview).resolve(), raw_preview
    return None, ""


def validate(args: argparse.Namespace) -> int:
    errors: list[str] = []
    manifest_path = project_path(args.manifest).resolve()
    if not manifest_path.exists():
        errors.append(f"manifest missing: {display_path(manifest_path)}")
        payload: dict = {}
    else:
        payload = json.loads(manifest_path.read_text(encoding="utf-8-sig"))

    assets = payload.get("assets", []) or []
    if not assets:
        errors.append("manifest has no assets")

    if args.columns <= 0:
        errors.append(f"columns must be positive, got {args.columns}")
    if args.tile_size <= 0:
        errors.append(f"tile-size must be positive, got {args.tile_size}")
    if args.label_height < 0:
        errors.append(f"label-height must be non-negative, got {args.label_height}")
    if args.gap < 0:
        errors.append(f"gap must be non-negative, got {args.gap}")

    output_path, raw_output = resolve_output_path(payload, str(args.output or ""))
    if output_path is None:
        errors.append("preview output missing: pass --output or set manifest preview")

    latest_input_mtime = manifest_path.stat().st_mtime if manifest_path.exists() else 0.0
    map_count = 0
    for asset in assets:
        asset_id = str(asset.get("id", "")).strip() or "<missing-id>"
        maps = asset.get("maps", {}) or {}
        for map_key in REQUIRED_MAPS:
            raw_map = str(maps.get(map_key, "")).strip()
            if not raw_map:
                errors.append(f"{asset_id}: missing map key {map_key}")
                continue
            map_path = project_path(raw_map)
            if not map_path.exists():
                errors.append(f"{asset_id}: missing map file {map_key}: {raw_map}")
                continue
            map_count += 1
            latest_input_mtime = max(latest_input_mtime, map_path.stat().st_mtime)

    if output_path is not None:
        if not output_path.exists():
            errors.append(f"preview file missing: {raw_output}")
        else:
            with Image.open(output_path) as image:
                actual_size = image.size
                actual_mode = image.mode
            if actual_mode != "RGB":
                errors.append(f"preview mode must be RGB, got {actual_mode}: {raw_output}")
            if assets and args.columns > 0 and args.tile_size > 0:
                want_size = expected_size(len(assets), args.columns, args.tile_size, args.label_height, args.gap)
                if actual_size != want_size:
                    errors.append(f"preview size mismatch: got {actual_size[0]}x{actual_size[1]}, expected {want_size[0]}x{want_size[1]}")
            preview_mtime = output_path.stat().st_mtime
            if preview_mtime + STALE_EPSILON_SECONDS < latest_input_mtime:
                errors.append(
                    "preview is stale: output is older than manifest or source maps "
                    f"(preview={preview_mtime:.3f}, latestInput={latest_input_mtime:.3f})"
                )

    print("EXTERNAL_PBR_LIT_PREVIEW_VALIDATOR")
    print(f"manifest={display_path(manifest_path)}")
    print(f"assets={len(assets)}")
    print(f"maps={map_count}")
    print(f"preview={display_path(output_path) if output_path is not None else '<missing>'}")
    print(f"errors={len(errors)}")
    for error in errors:
        print(f"ERROR {error}")
    return 1 if errors else 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", required=True)
    parser.add_argument("--output", default="")
    parser.add_argument("--tile-size", type=int, default=220)
    parser.add_argument("--columns", type=int, default=4)
    parser.add_argument("--label-height", type=int, default=40)
    parser.add_argument("--gap", type=int, default=14)
    return validate(parser.parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
