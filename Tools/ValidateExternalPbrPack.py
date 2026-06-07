#!/usr/bin/env python3
"""Validate downloaded external PBR texture pack dimensions and required maps."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]


def project_path(raw: str) -> Path:
    path = Path(raw)
    return path if path.is_absolute() else ROOT / path


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def validate(args: argparse.Namespace) -> int:
    manifest_path = project_path(args.manifest).resolve()
    payload = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    errors: list[str] = []
    warnings: list[str] = []
    required = ("BaseColor", "NormalGL", "ARM_AO_Rough_Metal", "Height", "MaskMap_UnityURP")
    asset_count = 0
    map_count = 0

    for asset in payload.get("assets", []) or []:
        asset_id = str(asset.get("id", "")).strip()
        maps = asset.get("maps", {}) or {}
        asset_count += 1
        for key in required:
            raw_path = str(maps.get(key, "")).strip()
            if not raw_path:
                errors.append(f"{asset_id}: missing required map key {key}")
                continue

            path = project_path(raw_path)
            if not path.exists():
                errors.append(f"{asset_id}: missing file {raw_path}")
                continue

            map_count += 1
            with Image.open(path) as image:
                width, height = image.size
                mode = image.mode
            if width != height:
                errors.append(f"{asset_id}:{key}: non-square {width}x{height} {raw_path}")
            if width < args.min_size:
                errors.append(f"{asset_id}:{key}: below min size {width} {raw_path}")
            if key == "MaskMap_UnityURP" and mode != "RGBA":
                errors.append(f"{asset_id}:{key}: expected RGBA mask map, actual mode={mode}")
            if key == "NormalGL" and mode not in ("RGB", "RGBA"):
                warnings.append(f"{asset_id}:{key}: unusual normal mode={mode}")

    print("EXTERNAL_PBR_PACK_VALIDATOR")
    print(f"manifest={display_path(manifest_path)}")
    print(f"assets={asset_count}")
    print(f"maps={map_count}")
    print(f"errors={len(errors)}")
    print(f"warnings={len(warnings)}")
    for error in errors:
        print(f"ERROR {error}")
    for warning in warnings:
        print(f"WARN {warning}")
    return 1 if errors else 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--manifest",
        default="Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PolyHaven/PolyHavenExternalPBR_Manifest.json",
    )
    parser.add_argument("--min-size", type=int, default=2048)
    return validate(parser.parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
