#!/usr/bin/env python3
"""Validate terrain surface material bindings used by macro geology material classes."""

from __future__ import annotations

import argparse
import csv
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_BINDINGS = ROOT / "Assets/_SourceData/Terrain/terrain_surface_material_bindings.csv"
GEMINI_MANIFEST = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases/Batch20260608_TextureExpansion/GeminiMaterialAtlas_Manifest.json"
REQUIRED_CLASSES = {
    "ShellSand",
    "LimestoneShelf",
    "ClaySilt",
    "HardRock",
    "BrineSaltCrust",
    "ManganeseNodulePlain",
    "ReefRubble",
    "SeepCrust",
}


def asset_exists(asset_path: str) -> bool:
    if not asset_path:
        return False
    normalized = asset_path.replace("\\", "/").strip()
    if not normalized.startswith("Assets/"):
        return False
    return (ROOT / normalized).is_file()


def load_gemini_material_ids() -> set[str]:
    if not GEMINI_MANIFEST.is_file():
        return set()
    manifest = json.loads(GEMINI_MANIFEST.read_text(encoding="utf-8"))
    return {
        str(asset.get("id", ""))
        for asset in manifest.get("assets", [])
        if isinstance(asset, dict) and asset.get("geologyAllowed") is True
    }


def validate(path: Path) -> int:
    errors: list[str] = []
    rows: list[dict[str, str]] = []
    material_ids = load_gemini_material_ids()

    with path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        for row in reader:
            rows.append({key: (value or "").strip() for key, value in row.items()})

    classes = [row.get("material_class", "") for row in rows]
    missing = sorted(REQUIRED_CLASSES - set(classes))
    if missing:
        errors.append("missing material classes: " + ", ".join(missing))
    duplicates = sorted({material for material in classes if classes.count(material) > 1})
    if duplicates:
        errors.append("duplicate material classes: " + ", ".join(duplicates))

    for row in rows:
        material_class = row.get("material_class", "")
        primary_layer = row.get("primary_terrain_layer", "")
        primary_id = row.get("primary_material_id", "")
        fallback_layer = row.get("fallback_terrain_layer", "")
        if material_class not in REQUIRED_CLASSES:
            errors.append(f"{material_class}: unknown material_class")
        if primary_layer and not primary_layer.endswith(".terrainlayer"):
            errors.append(f"{material_class}: primary_terrain_layer must be a TerrainLayer asset")
        if primary_layer and primary_id not in material_ids:
            errors.append(f"{material_class}: primary_material_id missing or not geologyAllowed in Gemini manifest")
        if primary_id and primary_id != "fallback_existing" and not primary_layer:
            errors.append(f"{material_class}: primary_material_id set without primary_terrain_layer")
        if not asset_exists(fallback_layer):
            errors.append(f"{material_class}: fallback_terrain_layer missing: {fallback_layer}")
        for numeric_key in ("tile_size_m", "min_depth_m", "max_depth_m", "min_slope01", "max_slope01", "blend_softness"):
            try:
                value = float(row.get(numeric_key, "nan"))
            except ValueError:
                errors.append(f"{material_class}: {numeric_key} is not numeric")
                continue
            if numeric_key == "tile_size_m" and value <= 0.0:
                errors.append(f"{material_class}: tile_size_m must be positive")
            if numeric_key == "blend_softness" and not (0.0 <= value <= 1.0):
                errors.append(f"{material_class}: blend_softness must be 0..1")
            if numeric_key.endswith("slope01") and not (0.0 <= value <= 1.0):
                errors.append(f"{material_class}: {numeric_key} must be 0..1")
        try:
            if float(row.get("min_depth_m", "0")) > float(row.get("max_depth_m", "0")):
                errors.append(f"{material_class}: min_depth_m exceeds max_depth_m")
            if float(row.get("min_slope01", "0")) > float(row.get("max_slope01", "0")):
                errors.append(f"{material_class}: min_slope01 exceeds max_slope01")
        except ValueError:
            pass

    if errors:
        for error in errors:
            print("ERROR:", error)
        return 1

    print(
        json.dumps(
            {
                "status": "ok",
                "bindings": str(path.relative_to(ROOT).as_posix()),
                "materialClassCount": len(rows),
                "geminiTerrainMaterialCount": len(material_ids),
                "fallbackOnly": [
                    row["material_class"]
                    for row in rows
                    if row.get("primary_material_id") == "fallback_existing"
                ],
            },
            indent=2,
        )
    )
    return 0


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("bindings", nargs="?", default=str(DEFAULT_BINDINGS))
    return parser.parse_args()


if __name__ == "__main__":
    args = parse_args()
    raise SystemExit(validate(Path(args.bindings).resolve()))
