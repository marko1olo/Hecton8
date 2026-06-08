#!/usr/bin/env python3
"""Validate Gemini biome assignments into imported procedural flora texture slots."""

from __future__ import annotations

import ast
import json
from pathlib import Path

from PIL import Image, ImageChops


ROOT = Path(__file__).resolve().parents[1]
TOOL = ROOT / "Tools/ApplyGeminiBiomeToFloraImported.py"
BIOME_MANIFEST = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiBiomeMaterialIntake_20260607/GeminiBiomeMaterials_Manifest.json"
FLORA_IMPORTED_ROOT = ROOT / "Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported"
REQUIRED_MAPS = ("BaseColor", "NormalGL", "MaskMap_UnityURP", "ARM_AO_Rough_Metal", "Height")
MAX_SIZE = 1024
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


def parse_assignments() -> list[tuple[str, str]]:
    tree = ast.parse(TOOL.read_text(encoding="utf-8-sig"))
    for node in ast.walk(tree):
        if isinstance(node, ast.Assign):
            names = [target.id for target in node.targets if isinstance(target, ast.Name)]
            if "ASSIGNMENTS" not in names:
                continue
            value = ast.literal_eval(node.value)
            return [(str(family_id), str(material_id)) for family_id, material_id in value]
    return []


def transformed_source_image(source_path: Path, mode: str) -> Image.Image:
    with Image.open(source_path) as image:
        output = image.convert(mode)
        if max(output.size) > MAX_SIZE:
            output.thumbnail((MAX_SIZE, MAX_SIZE), Image.Resampling.LANCZOS)
        return output.copy()


def image_matches_source(target_path: Path, source_path: Path, mode: str) -> bool:
    expected = transformed_source_image(source_path, mode)
    with Image.open(target_path) as actual:
        actual_converted = actual.convert(mode)
        if actual_converted.size != expected.size:
            return False
        return ImageChops.difference(actual_converted, expected).getbbox() is None


def main() -> int:
    errors: list[str] = []
    warnings: list[str] = []
    assignments = parse_assignments()
    assets = load_assets()

    if not assignments:
        errors.append(f"No ASSIGNMENTS parsed from {display_path(TOOL)}")

    seen_families: set[str] = set()
    for family_id, material_id in assignments:
        if family_id in seen_families:
            errors.append(f"Duplicate flora family assignment: {family_id}")
        seen_families.add(family_id)

        family_dir = FLORA_IMPORTED_ROOT / family_id
        if not family_dir.exists():
            errors.append(f"{family_id}: missing imported flora family directory {display_path(family_dir)}")
        asset = assets.get(material_id)
        if asset is None:
            errors.append(f"{family_id}: missing biome material id {material_id}")
            continue

        if asset.get("watermarkRisk"):
            errors.append(f"{family_id}: watermark-risk material assigned: {material_id}")
        if not bool(asset.get("floraAllowed", False)):
            errors.append(f"{family_id}: assigned non-flora material {material_id}; floraAllowed=false")

        maps = asset.get("maps", {}) or {}
        for map_key in REQUIRED_MAPS:
            map_path = str(maps.get(map_key, "")).strip()
            if not map_path:
                errors.append(f"{family_id}:{material_id}: missing map key {map_key}")
                continue
            if not project_path(map_path).exists():
                errors.append(f"{family_id}:{material_id}: missing map file {map_key}: {map_path}")

        for token, map_key, mode in MAP_ROLES:
            target = family_dir / f"{token}___{family_id}.png"
            if not target.exists():
                errors.append(f"{family_id}: missing imported flora texture slot {display_path(target)}")
                continue

            source_path = project_path(str(maps.get(map_key, "")).strip())
            if not source_path.exists():
                continue
            if not image_matches_source(target, source_path, mode):
                errors.append(f"{family_id}:{token}: imported texture slot is stale or mismatched for {material_id}:{map_key}")

        seam_after = asset.get("seamScoreAfter")
        if isinstance(seam_after, (int, float)) and float(seam_after) > 1.6:
            warnings.append(f"{family_id}: high seamScoreAfter={seam_after} material={material_id}")

    print("GEMINI_BIOME_FLORA_IMPORTED_ASSIGNMENT_VALIDATOR")
    print(f"tool={display_path(TOOL)}")
    print(f"manifest={display_path(BIOME_MANIFEST)}")
    print(f"assignments={len(assignments)}")
    print(f"errors={len(errors)}")
    print(f"warnings={len(warnings)}")
    for error in errors:
        print(f"ERROR: {error}")
    for warning in warnings:
        print(f"WARN: {warning}")
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
