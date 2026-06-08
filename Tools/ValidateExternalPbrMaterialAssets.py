#!/usr/bin/env python3
"""Validate generated ExternalPBR material assets after Unity import."""

from __future__ import annotations

import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
GENERATED_ROOT = ROOT / "Assets/_Project/Art/TEXTURES/Generated"
MATERIAL_ROOT = ROOT / "Assets/_Project/Art/Materials/Generated/ExternalPBR_20260607"
STATIC_MANIFESTS = (
    (
        "PolyHaven",
        GENERATED_ROOT / "ExternalPBR_20260607/PolyHaven/PolyHavenExternalPBR_Manifest.json",
    ),
    (
        "GeminiSingles_20260607",
        GENERATED_ROOT / "GeminiMaterialIntake_20260607/GeminiSingleMaterials_Manifest.json",
    ),
    (
        "GeminiBiome_20260607",
        GENERATED_ROOT / "GeminiBiomeMaterialIntake_20260607/GeminiBiomeMaterials_Manifest.json",
    ),
)
GEMINI_ATLAS_ROOT = GENERATED_ROOT / "GeminiMaterialAtlases"
MIN_EXPECTED_MATERIALS = 51
REQUIRED_PROVIDERS = {
    "GeminiSingles_20260607",
    "GeminiBiome_20260607",
    "Gemini_Batch20260607_MicroPanel",
    "Gemini_Batch20260608_TextureExpansion",
}
TEXTURE_SCALE_PATTERN = re.compile(
    r"m_Texture:\s*\{fileID:\s*\d+,\s*guid:\s*(?P<guid>[0-9a-fA-F]+),\s*type:\s*\d+\}\s*\n"
    r"\s*m_Scale:\s*\{x:\s*(?P<x>-?[0-9.]+),\s*y:\s*(?P<y>-?[0-9.]+)\}",
    re.MULTILINE,
)
SCALE_EPSILON = 0.001


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def sanitize_provider_name(value: str) -> str:
    if not value.strip():
        return "Atlas"
    return "".join(c if c.isalnum() or c in "_-" else "_" for c in value)


def load_manifest(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def project_path(raw: str) -> Path:
    path = Path(raw)
    return path if path.is_absolute() else ROOT / path


def read_guid(asset_path: Path) -> str:
    meta_path = asset_path.with_suffix(asset_path.suffix + ".meta")
    if not meta_path.exists():
        return ""

    match = re.search(
        r"^guid:\s*([0-9a-fA-F]+)\s*$",
        meta_path.read_text(encoding="utf-8-sig"),
        re.MULTILINE,
    )
    return match.group(1) if match else ""


def expected_tiling_scale(asset: dict) -> float:
    if int(asset.get("catalogVersion", 0) or 0) <= 0:
        return 1.0
    value = float(asset.get("tilingScale", 1.0))
    return max(0.25, min(16.0, value))


def texture_scale_for_guid(material_text: str, guid: str) -> tuple[float, float] | None:
    for match in TEXTURE_SCALE_PATTERN.finditer(material_text):
        if match.group("guid").lower() == guid.lower():
            return float(match.group("x")), float(match.group("y"))
    return None


def iter_manifests() -> list[tuple[str, Path]]:
    manifests = [(provider, path) for provider, path in STATIC_MANIFESTS if path.exists()]
    if GEMINI_ATLAS_ROOT.exists():
        for path in sorted(GEMINI_ATLAS_ROOT.rglob("GeminiMaterialAtlas_Manifest.json")):
            provider = "Gemini_" + sanitize_provider_name(path.parent.name)
            manifests.append((provider, path))
    return manifests


def main() -> int:
    errors: list[str] = []
    material_count = 0
    manifests = iter_manifests()
    found_providers = {provider for provider, _ in manifests}
    for provider in sorted(REQUIRED_PROVIDERS - found_providers):
        errors.append(f"missing required generated material provider manifest: {provider}")

    for provider, manifest_path in manifests:
        payload = load_manifest(manifest_path)
        for asset in payload.get("assets", []) or []:
            asset_id = str(asset.get("id", "")).strip()
            if not asset_id:
                errors.append(f"{display_path(manifest_path)}: asset missing id")
                continue
            material_count += 1
            material_path = MATERIAL_ROOT / provider / f"MAT_EXT_{provider}_{asset_id}.mat"
            if not material_path.exists():
                errors.append(f"missing material asset: {display_path(material_path)}")
                continue

            material_text = material_path.read_text(encoding="utf-8-sig")
            maps = asset.get("maps", {}) or {}
            expected_scale = expected_tiling_scale(asset)
            for map_key in ("BaseColor", "NormalGL", "MaskMap_UnityURP", "Height"):
                map_path_raw = str(maps.get(map_key, "")).strip()
                if not map_path_raw:
                    errors.append(f"{provider}/{asset_id}: missing map key in source manifest: {map_key}")
                    continue

                guid = read_guid(project_path(map_path_raw))
                if not guid:
                    errors.append(f"{provider}/{asset_id}: missing texture .meta guid for {map_key}: {map_path_raw}")
                elif guid not in material_text:
                    errors.append(f"{provider}/{asset_id}: generated material missing {map_key} texture guid")
                else:
                    scale = texture_scale_for_guid(material_text, guid)
                    if scale is None:
                        errors.append(f"{provider}/{asset_id}: generated material missing {map_key} texture scale")
                    elif abs(scale[0] - expected_scale) > SCALE_EPSILON or abs(scale[1] - expected_scale) > SCALE_EPSILON:
                        errors.append(
                            f"{provider}/{asset_id}: generated material {map_key} scale {scale[0]:.3f},{scale[1]:.3f} expected {expected_scale:.3f}"
                        )

    if material_count < MIN_EXPECTED_MATERIALS:
        errors.append(f"generated material post-apply coverage too low: expectedAtLeast={MIN_EXPECTED_MATERIALS} actual={material_count}")

    print("EXTERNAL_PBR_MATERIAL_ASSET_VALIDATOR")
    print(f"materialRoot={display_path(MATERIAL_ROOT)}")
    print(f"providers={len(found_providers)}")
    print(f"materialsExpected={material_count}")
    print(f"errors={len(errors)}")
    for error in errors:
        print(f"ERROR {error}")
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
