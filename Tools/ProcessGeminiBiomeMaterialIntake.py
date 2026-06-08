#!/usr/bin/env python3
"""Clean Gemini biome material outputs, compress them, and build an importable PBR manifest."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image

from ProcessGeminiMaterialIntake import (
    ROOT,
    display_path,
    make_preview,
    prepare_tileable_base,
    repair_watermark,
    save_jpeg,
    save_maps,
)


BIOME_ROOT = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiBiomeMaterialIntake_20260607"
DEFAULT_WATERMARK_PROFILE = (0.944, 0.944, 0.18)
WATERMARK_PROFILES = {
    "living_kelp_frond_surface": (0.875, 0.875, 0.18),
    "pale_tube_coral_calcium": None,
}

INPUTS = (
    {
        "path": r"C:\Users\danat\Downloads\Aegir Surface Foamless Wet Rock.png",
        "id": "aegir_surface_foamless_wet_rock",
        "title": "Aegir Surface Foamless Wet Rock",
        "materialFamily": "geology",
        "surfaceClass": "aegir_wet_surface_rock",
        "role": "foamless wet shoreline rock for Aegir surface route, tide-slick outcrops, and shallow cave lips",
        "worldPanelAllowed": True,
        "geologyAllowed": True,
        "tilingScale": 2.1,
        "metallic": 0.0,
        "smoothness": 0.44,
        "normalScale": 0.86,
        "heightScale": 0.009,
    },
    {
        "path": r"C:\Users\danat\Downloads\Pressure Suit Fabric Composite.png",
        "id": "pressure_suit_fabric_composite",
        "title": "Pressure Suit Fabric Composite",
        "materialFamily": "playerGear",
        "surfaceClass": "pressure_suit_fabric_composite",
        "role": "woven pressure suit composite for player gear panels, glove inserts, soft seals, and emergency packs",
        "stationPropAllowed": True,
        "playerGearAllowed": True,
        "tilingScale": 4.8,
        "metallic": 0.0,
        "smoothness": 0.24,
        "normalScale": 0.74,
        "heightScale": 0.004,
    },
    {
        "path": r"C:\Users\danat\Downloads\Creature Bone Plate Material.png",
        "id": "creature_bone_plate_material",
        "title": "Creature Bone Plate Material",
        "materialFamily": "fauna",
        "surfaceClass": "creature_bone_plate",
        "role": "aged calcium bone plate for large fauna shells, harvested fragments, and pressure-worn creature armor",
        "faunaAllowed": True,
        "salvageAllowed": True,
        "tilingScale": 2.6,
        "metallic": 0.0,
        "smoothness": 0.31,
        "normalScale": 0.62,
        "heightScale": 0.007,
    },
    {
        "path": r"C:\Users\danat\Downloads\Pale Tube Coral Calcium.png",
        "id": "pale_tube_coral_calcium",
        "title": "Pale Tube Coral Calcium",
        "materialFamily": "flora",
        "surfaceClass": "pale_tube_coral_calcium",
        "role": "pale tube coral calcium for reef clusters, cave lip colonies, and bleached shallow formations",
        "floraAllowed": True,
        "worldPanelAllowed": True,
        "tilingScale": 2.0,
        "metallic": 0.0,
        "smoothness": 0.36,
        "normalScale": 0.92,
        "heightScale": 0.014,
    },
    {
        "path": r"C:\Users\danat\Downloads\Wet Basalt Cave Wall.png",
        "id": "wet_basalt_cave_wall",
        "title": "Wet Basalt Cave Wall",
        "materialFamily": "geology",
        "surfaceClass": "wet_basalt_cave_wall",
        "role": "wet black basalt and green biofilm for early cave walls, tunnel lips, and damp overhangs",
        "worldPanelAllowed": True,
        "geologyAllowed": True,
        "tilingScale": 1.8,
        "metallic": 0.0,
        "smoothness": 0.48,
        "normalScale": 0.95,
        "heightScale": 0.011,
    },
    {
        "path": r"C:\Users\danat\Downloads\Hydrothermal Vent Mineral Crust.png",
        "id": "hydrothermal_vent_mineral_crust",
        "title": "Hydrothermal Vent Mineral Crust",
        "materialFamily": "geology",
        "surfaceClass": "hydrothermal_vent_mineral_crust",
        "role": "sulfur and iron mineral crust for hydrothermal vents, hot fissures, and pressure-stained cave props",
        "worldPanelAllowed": True,
        "geologyAllowed": True,
        "tilingScale": 2.4,
        "metallic": 0.06,
        "smoothness": 0.22,
        "normalScale": 1.10,
        "heightScale": 0.016,
    },
    {
        "path": r"C:\Users\danat\Downloads\Soft Jelly Membrane.png",
        "id": "soft_jelly_membrane",
        "title": "Soft Jelly Membrane",
        "materialFamily": "fauna",
        "surfaceClass": "soft_jelly_membrane",
        "role": "translucent jelly membrane for soft fauna bodies, egg sacs, and living pressure membranes",
        "faunaAllowed": True,
        "tilingScale": 2.2,
        "metallic": 0.0,
        "smoothness": 0.72,
        "normalScale": 0.32,
        "heightScale": 0.003,
        "translucencyCandidate": True,
        "emissiveCandidate": True,
    },
    {
        "path": r"C:\Users\danat\Downloads\Abyssal Predator Hide.png",
        "id": "abyssal_predator_hide",
        "title": "Abyssal Predator Hide",
        "materialFamily": "fauna",
        "surfaceClass": "abyssal_predator_hide",
        "role": "dark scarred predator hide for large hostile fauna, pressure armor, and close-range creature reads",
        "faunaAllowed": True,
        "tilingScale": 2.8,
        "metallic": 0.0,
        "smoothness": 0.27,
        "normalScale": 0.84,
        "heightScale": 0.010,
    },
    {
        "path": r"C:\Users\danat\Downloads\Bioluminescent Coral Flesh.png",
        "id": "bioluminescent_coral_flesh",
        "title": "Bioluminescent Coral Flesh",
        "materialFamily": "flora",
        "surfaceClass": "bioluminescent_coral_flesh",
        "role": "blue bioluminescent coral flesh for readable night/depth flora clusters and cave route highlights",
        "floraAllowed": True,
        "worldPanelAllowed": True,
        "tilingScale": 2.1,
        "metallic": 0.0,
        "smoothness": 0.43,
        "normalScale": 0.78,
        "heightScale": 0.009,
        "emissiveCandidate": True,
    },
    {
        "path": r"C:\Users\danat\Downloads\Living Kelp Frond Surface.png",
        "id": "living_kelp_frond_surface",
        "title": "Living Kelp Frond Surface",
        "materialFamily": "flora",
        "surfaceClass": "living_kelp_frond_surface",
        "role": "living kelp frond surface for near-surface vegetation, harvestable leaves, and swaying habitat silhouettes",
        "floraAllowed": True,
        "tilingScale": 2.7,
        "metallic": 0.0,
        "smoothness": 0.50,
        "normalScale": 0.52,
        "heightScale": 0.005,
        "translucencyCandidate": True,
    },
)


def with_usage_defaults(spec: dict) -> dict:
    output = dict(spec)
    for key in (
        "heldToolAllowed",
        "stationPropAllowed",
        "salvageAllowed",
        "worldPanelAllowed",
        "floraAllowed",
        "faunaAllowed",
        "geologyAllowed",
        "playerGearAllowed",
    ):
        output.setdefault(key, False)
    output.setdefault("translucencyCandidate", False)
    output.setdefault("emissiveCandidate", False)
    return output


def process(args: argparse.Namespace) -> int:
    material_root = BIOME_ROOT / "Materials"
    source_root = BIOME_ROOT / "SourceCleaned"
    manifest_assets: list[dict] = []
    processed = 0

    for raw_spec in INPUTS:
        spec = with_usage_defaults(raw_spec)
        source = Path(spec["path"])
        if not source.exists():
            raise FileNotFoundError(str(source))

        with Image.open(source) as image:
            watermark_profile = WATERMARK_PROFILES.get(spec["id"], DEFAULT_WATERMARK_PROFILE)
            repaired = image.convert("RGB") if watermark_profile is None else repair_watermark(image, watermark_profile)

        cleaned_source = source_root / f"TX_GB_{spec['id']}_Cleaned_2k.jpg"
        save_jpeg(repaired, cleaned_source, 2048, args.source_quality, args.source_max_mb * 1024 * 1024)
        processed += 1

        asset_id = f"gemini_biome_20260607_{spec['id']}"
        asset_dir = material_root / asset_id
        base_path = asset_dir / f"TX_GB_{asset_id}_BaseColor.jpg"
        tileable, seam_before, seam_after, seam_repaired = prepare_tileable_base(repaired, args.seam_threshold)
        base = save_jpeg(tileable, base_path, args.material_size, args.base_quality, args.base_max_mb * 1024 * 1024)
        maps = {"BaseColor": display_path(base_path)}
        maps.update(save_maps(base, spec, asset_dir, asset_id))

        manifest_assets.append(
            {
                "id": asset_id,
                "title": spec["title"],
                "source": str(source),
                "license": "USER_GENERATED_REVIEW_REQUIRED",
                "role": spec["role"],
                "catalogVersion": 1,
                "materialFamily": spec["materialFamily"],
                "surfaceClass": spec["surfaceClass"],
                "heldToolAllowed": spec["heldToolAllowed"],
                "stationPropAllowed": spec["stationPropAllowed"],
                "salvageAllowed": spec["salvageAllowed"],
                "worldPanelAllowed": spec["worldPanelAllowed"],
                "floraAllowed": spec["floraAllowed"],
                "faunaAllowed": spec["faunaAllowed"],
                "geologyAllowed": spec["geologyAllowed"],
                "playerGearAllowed": spec["playerGearAllowed"],
                "tilingScale": spec["tilingScale"],
                "metallic": spec["metallic"],
                "smoothness": spec["smoothness"],
                "normalScale": spec["normalScale"],
                "heightScale": spec["heightScale"],
                "translucencyCandidate": spec["translucencyCandidate"],
                "emissiveCandidate": spec["emissiveCandidate"],
                "provisionalPbrMaps": True,
                "watermarkRepaired": watermark_profile is not None,
                "watermarkRepairSkippedReason": "faint_low_risk_user_accepted" if watermark_profile is None else "",
                "seamScoreBefore": round(seam_before, 4),
                "seamScoreAfter": round(seam_after, 4),
                "seamRepaired": seam_repaired,
                "maps": maps,
            }
        )

    preview = BIOME_ROOT / "PREVIEW_GeminiBiomeMaterials_20260607.png"
    make_preview(manifest_assets, [], preview)
    manifest = {
        "schema": "hecton8.external_pbr_pack.v1",
        "sourceProvider": "GeminiManualBiomeMaterials",
        "providerLicensePage": "",
        "license": "USER_GENERATED_REVIEW_REQUIRED",
        "resolution": f"{args.material_size}px",
        "unityImportStatus": "PENDING UNITY IMPORT",
        "reviewStatus": "PENDING VISUAL REVIEW",
        "intendedUse": "Natural geology, flora, fauna, player gear fabric, and early-route biome material library.",
        "mapPacking": {
            "sourceARM": "RGB = generated Ambient Occlusion, Roughness, Metal",
            "unityMaskMap": "RGBA = Metal, Ambient Occlusion, unused zero, Smoothness",
        },
        "assets": manifest_assets,
        "preview": display_path(preview),
    }
    manifest_path = BIOME_ROOT / "GeminiBiomeMaterials_Manifest.json"
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")

    print("GEMINI_BIOME_MATERIAL_INTAKE_STATUS: PASS")
    print(f"processed_images={processed}")
    print(f"accepted_biome_materials={len(manifest_assets)}")
    print(f"manifest={display_path(manifest_path)}")
    print(f"preview={display_path(preview)}")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--material-size", type=int, default=1024)
    parser.add_argument("--source-quality", type=int, default=90)
    parser.add_argument("--base-quality", type=int, default=86)
    parser.add_argument("--source-max-mb", type=float, default=1.5)
    parser.add_argument("--base-max-mb", type=float, default=0.75)
    parser.add_argument("--seam-threshold", type=float, default=2.8)
    return process(parser.parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
