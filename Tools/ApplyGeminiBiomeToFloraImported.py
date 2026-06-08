#!/usr/bin/env python3
"""Apply selected Gemini biome materials into existing procedural flora imported texture slots."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image, ImageChops


ROOT = Path(__file__).resolve().parents[1]
BIOME_MANIFEST = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiBiomeMaterialIntake_20260607/GeminiBiomeMaterials_Manifest.json"
FLORA_IMPORTED_ROOT = ROOT / "Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported"
REPORT_PATH = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiBiomeFloraIntegration_20260607.json"

ASSIGNMENTS = (
    ("family.kelp.tall", "gemini_biome_20260607_living_kelp_frond_surface"),
    ("family.kelp.patch.dense", "gemini_biome_20260607_living_kelp_frond_surface"),
    ("family.kelp.canopy", "gemini_biome_20260607_living_kelp_frond_surface"),
    ("family.coral.low", "gemini_biome_20260607_pale_tube_coral_calcium"),
    ("family.coral.plate", "gemini_biome_20260607_pale_tube_coral_calcium"),
    ("family.coral.massive", "gemini_biome_20260607_pale_tube_coral_calcium"),
    ("family.coral.branching", "gemini_biome_20260607_bioluminescent_coral_flesh"),
    ("family.coral.brittle", "gemini_biome_20260607_bioluminescent_coral_flesh"),
)

MAP_ROLES = (
    ("albedo", "BaseColor", "RGB"),
    ("detail", "Height", "RGB"),
    ("normal", "NormalGL", "RGB"),
    ("mask", "MaskMap_UnityURP", "RGBA"),
)


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def project_path(raw: str) -> Path:
    path = Path(raw)
    return path if path.is_absolute() else ROOT / path


def load_assets() -> dict[str, dict]:
    payload = json.loads(BIOME_MANIFEST.read_text(encoding="utf-8-sig"))
    return {
        str(asset.get("id", "")).strip(): asset
        for asset in payload.get("assets", []) or []
        if asset.get("id")
    }


def save_flora_png(source: Path, target: Path, mode: str, max_size: int) -> dict:
    before = target.stat().st_size if target.exists() else 0
    with Image.open(source) as image:
        output = image.convert(mode)
        if max_size > 0 and max(output.size) > max_size:
            output.thumbnail((max_size, max_size), Image.Resampling.LANCZOS)

        if target.exists():
            with Image.open(target) as existing:
                existing_converted = existing.convert(mode)
                if existing_converted.size == output.size and ImageChops.difference(existing_converted, output).getbbox() is None:
                    return {
                        "target": display_path(target),
                        "source": display_path(source),
                        "mode": mode,
                        "beforeBytes": before,
                        "afterBytes": before,
                        "deltaBytes": 0,
                        "skippedUnchanged": True,
                    }

        target.parent.mkdir(parents=True, exist_ok=True)
        output.save(target, "PNG", optimize=True, compress_level=9)

    return {
        "target": display_path(target),
        "source": display_path(source),
        "mode": mode,
        "beforeBytes": before,
        "afterBytes": target.stat().st_size,
        "deltaBytes": target.stat().st_size - before,
        "skippedUnchanged": False,
    }


def apply(args: argparse.Namespace) -> int:
    assets = load_assets()
    records: list[dict] = []
    for family_id, material_id in ASSIGNMENTS:
        asset = assets.get(material_id)
        if asset is None:
            raise KeyError(f"Missing material in biome manifest: {material_id}")

        maps = asset.get("maps", {}) or {}
        family_dir = FLORA_IMPORTED_ROOT / family_id
        for map_token, map_key, mode in MAP_ROLES:
            source = project_path(str(maps.get(map_key, "")))
            if not source.exists():
                raise FileNotFoundError(f"{material_id}:{map_key}: {source}")

            target = family_dir / f"{map_token}___{family_id}.png"
            record = save_flora_png(source, target, mode, args.max_size)
            record.update(
                {
                    "familyId": family_id,
                    "materialId": material_id,
                    "mapToken": map_token,
                    "mapKey": map_key,
                }
            )
            records.append(record)

    report = {
        "schema": "hecton8.gemini_biome_flora_integration.v1",
        "date": "2026-06-07",
        "operation": "replace_existing_imported_flora_texture_sources_same_paths",
        "maxSize": args.max_size,
        "assignments": [{"familyId": family_id, "materialId": material_id} for family_id, material_id in ASSIGNMENTS],
        "records": records,
        "totalBeforeBytes": sum(record["beforeBytes"] for record in records),
        "totalAfterBytes": sum(record["afterBytes"] for record in records),
        "skippedUnchanged": sum(1 for record in records if record.get("skippedUnchanged")),
    }
    REPORT_PATH.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")

    print("GEMINI_BIOME_FLORA_INTEGRATION_STATUS: PASS")
    print(f"families={len(ASSIGNMENTS)}")
    print(f"textures_written={len(records)}")
    print(f"before_mb={report['totalBeforeBytes'] / 1024 / 1024:.2f}")
    print(f"after_mb={report['totalAfterBytes'] / 1024 / 1024:.2f}")
    print(f"skipped_unchanged={report['skippedUnchanged']}")
    print(f"report={display_path(REPORT_PATH)}")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--max-size", type=int, default=1024)
    return apply(parser.parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
