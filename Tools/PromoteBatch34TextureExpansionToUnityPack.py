#!/usr/bin/env python3
"""Promote curated Batch34 material-capable sources into an importable Unity PBR pack."""

from __future__ import annotations

import argparse
import json
import shutil
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageOps


ROOT = Path(__file__).resolve().parents[1]
BATCH_ROOT = ROOT / "Docs/GeneratedAssets/Gemini/Outputs/Batch34_TextureExpansion"
CURATION_MANIFEST = BATCH_ROOT / "QA/Batch34_TextureExpansion_CurationManifest.json"
INTAKE_MANIFEST = BATCH_ROOT / "QA/Batch34_TextureExpansion_IntakeManifest.json"
OUTPUT_ROOT = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases/Batch20260608_TextureExpansion"
TILES_ROOT = OUTPUT_ROOT / "Tiles"
MANIFEST_NAME = "GeminiMaterialAtlas_Manifest.json"
MATERIAL_SOURCE_TYPES = {"SEAMLESS_TILE", "TRIM_SHEET"}


def project_rel(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def project_path(raw: str | Path) -> Path:
    path = Path(raw)
    return path if path.is_absolute() else ROOT / path


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def asset_safe_id(entry: dict) -> str:
    slug = str(entry["slug"]).strip().lower().replace("-", "_")
    return f"gemini_Batch20260608_TextureExpansion_b34_{str(entry['id']).split('-')[-1]}_{slug}"


def copy_image(src: Path, dst: Path) -> tuple[int, int, str]:
    if not src.exists():
        raise FileNotFoundError(project_rel(src))
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src, dst)
    with Image.open(dst) as image:
        width, height = image.size
        mode = image.mode
    return width, height, mode


def repack_mrao(mrao_path: Path, arm_path: Path, mask_path: Path) -> tuple[float, float]:
    if not mrao_path.exists():
        raise FileNotFoundError(project_rel(mrao_path))
    with Image.open(mrao_path) as image:
        rgba = image.convert("RGBA")
        data = np.asarray(rgba, dtype=np.float32) / 255.0

    metal = data[:, :, 0]
    roughness = data[:, :, 1]
    ao = data[:, :, 2]
    smoothness = np.clip(1.0 - roughness, 0.0, 1.0)
    zero = np.zeros_like(metal)

    arm = np.stack((ao, roughness, metal), axis=2)
    mask = np.stack((metal, ao, zero, smoothness), axis=2)
    arm_path.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(np.uint8(np.clip(arm, 0.0, 1.0) * 255.0), "RGB").save(
        arm_path,
        "JPEG",
        quality=92,
        optimize=True,
        progressive=True,
        subsampling=0,
    )
    Image.fromarray(np.uint8(np.clip(mask, 0.0, 1.0) * 255.0), "RGBA").save(mask_path, "PNG")
    return float(metal.mean()), float(smoothness.mean())


def source_class(entry: dict) -> str:
    family = str(entry.get("family", "batch34")).strip()
    slug = str(entry.get("slug", "")).strip()
    return f"{family}_{slug}".strip("_")


def usage_flags(entry: dict) -> dict[str, bool]:
    family = str(entry.get("family", ""))
    source_type = str(entry.get("sourceType", ""))
    is_terrain = family.startswith("terrain_")
    is_hard = family.startswith("hard_surface")
    is_rubber = family.startswith("rubber")
    is_glass = family.startswith("glass")
    is_fabric = family.startswith("fabric") or family.startswith("suit")
    return {
        "heldToolAllowed": bool(source_type == "TRIM_SHEET" and (is_rubber or is_glass or is_fabric)),
        "stationPropAllowed": bool(is_hard or is_rubber or is_glass or is_fabric),
        "salvageAllowed": bool(is_hard),
        "worldPanelAllowed": bool(is_hard or family in {"rubber_trim", "fabric_insulation", "suit_fabric"}),
        "floraAllowed": False,
        "faunaAllowed": False,
        "geologyAllowed": bool(is_terrain),
        "playerGearAllowed": bool(family in {"rubber_cable", "rubber_trim", "suit_fabric", "glass_lens"}),
    }


def tiling_scale(entry: dict) -> float:
    family = str(entry.get("family", ""))
    source_type = str(entry.get("sourceType", ""))
    if source_type == "TRIM_SHEET":
        return 1.0
    if family == "terrain_sediment":
        return 2.0
    if family == "terrain_photic":
        return 1.5
    if family.startswith("terrain_"):
        return 1.2
    if family == "rubber_cable":
        return 3.0
    if family == "glass_lens":
        return 2.0
    return 1.0


def normal_scale(entry: dict) -> float:
    family = str(entry.get("family", ""))
    if family == "glass_lens":
        return 0.55
    if family.startswith("terrain_"):
        return 1.0
    if family.startswith("hard_surface"):
        return 0.85
    if family.startswith("rubber"):
        return 0.75
    return 0.65


def height_scale(entry: dict) -> float:
    family = str(entry.get("family", ""))
    if family == "glass_lens":
        return 0.001
    if family.startswith("terrain_"):
        return 0.006
    if family.startswith("hard_surface"):
        return 0.004
    return 0.003


def write_preview(assets: list[dict], path: Path) -> None:
    if not assets:
        return
    tile = 180
    label_h = 42
    gap = 10
    columns = 4
    rows = (len(assets) + columns - 1) // columns
    canvas = Image.new("RGB", (columns * tile + (columns - 1) * gap, rows * (tile + label_h) + (rows - 1) * gap), (8, 12, 14))
    draw = ImageDraw.Draw(canvas)
    for index, asset in enumerate(assets):
        raw_path = project_path(asset["maps"]["BaseColor"])
        with Image.open(raw_path) as image:
            preview = ImageOps.exif_transpose(image).convert("RGB")
            preview.thumbnail((tile, tile), Image.Resampling.LANCZOS)
        cell_x = (index % columns) * (tile + gap)
        cell_y = (index // columns) * (tile + label_h + gap)
        x = cell_x + (tile - preview.width) // 2
        y = cell_y + (tile - preview.height) // 2
        canvas.paste(preview, (x, y))
        draw.rectangle((cell_x, cell_y + tile, cell_x + tile, cell_y + tile + label_h), fill=(5, 16, 14))
        draw.text((cell_x + 5, cell_y + tile + 5), str(asset["sourceBatchId"]), fill=(220, 232, 228))
        draw.text((cell_x + 5, cell_y + tile + 22), str(asset["sourceFamily"])[:28], fill=(180, 204, 204))
    path.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(path, "PNG")


def promote(args: argparse.Namespace) -> int:
    curation = load_json(CURATION_MANIFEST)
    intake = load_json(INTAKE_MANIFEST)
    intake_by_id = {entry["id"]: entry for entry in intake["entries"]}
    selected: list[dict] = []
    skipped: list[dict] = []

    for entry in curation["entries"]:
        source_type = str(entry.get("sourceType", ""))
        status = str(entry.get("curationStatus", ""))
        if source_type in MATERIAL_SOURCE_TYPES and status.startswith("CURATED_READY"):
            selected.append(entry)
        else:
            skipped.append(
                {
                    "id": entry["id"],
                    "sourceType": source_type,
                    "curationStatus": status,
                    "reason": "not material-capable ready source",
                }
            )

    assets: list[dict] = []
    for entry in selected:
        intake_entry = intake_by_id[str(entry["id"])]
        maps = intake_entry.get("maps", {}) or {}
        asset_id = asset_safe_id(entry)
        asset_dir = TILES_ROOT / asset_id
        base_path = asset_dir / f"TX_B34_{asset_id}_BaseColor.jpg"
        normal_path = asset_dir / f"TX_B34_{asset_id}_NormalGL.jpg"
        height_path = asset_dir / f"TX_B34_{asset_id}_Height.jpg"
        arm_path = asset_dir / f"TX_B34_{asset_id}_ARM_AO_Rough_Metal.jpg"
        mask_path = asset_dir / f"TX_B34_{asset_id}_MaskMap_UnityURP.png"

        width, height, mode = copy_image(project_path(entry["curatedBaseColorPath"]), base_path)
        if width != height:
            raise ValueError(f"{entry['id']}: basecolor must be square, got {width}x{height}")
        copy_image(project_path(maps["NormalGL"]), normal_path)
        copy_image(project_path(maps["Height"]), height_path)
        metallic, smoothness = repack_mrao(project_path(maps["MRAO_Provisional_RGBA_Metal_Rough_AO_Emission"]), arm_path, mask_path)

        flags = usage_flags(entry)
        asset = {
            "id": asset_id,
            "title": entry["title"],
            "source": entry.get("downloadSource", intake_entry.get("downloadSource", "")),
            "sourceBatchId": entry["id"],
            "sourceCurationStatus": entry["curationStatus"],
            "license": "USER_GENERATED_REVIEW_REQUIRED",
            "role": entry["targetRole"],
            "integrationNote": entry["integrationNote"],
            "catalogVersion": 1,
            "surfaceClass": source_class(entry),
            "tilingScale": tiling_scale(entry),
            "metallic": round(metallic, 4),
            "smoothness": round(smoothness, 4),
            "normalScale": normal_scale(entry),
            "heightScale": height_scale(entry),
            "provisionalPbrMaps": True,
            "sourceType": entry["sourceType"],
            "sourceFamily": entry["family"],
            "width": width,
            "height": height,
            "baseMode": mode,
            **flags,
            "maps": {
                "BaseColor": project_rel(base_path),
                "NormalGL": project_rel(normal_path),
                "ARM_AO_Rough_Metal": project_rel(arm_path),
                "Height": project_rel(height_path),
                "MaskMap_UnityURP": project_rel(mask_path),
            },
        }
        assets.append(asset)

    preview_path = OUTPUT_ROOT / "PREVIEW_Batch20260608_TextureExpansion_Materials.png"
    write_preview(assets, preview_path)

    manifest = {
        "schema": "hecton8.external_pbr_pack.v1",
        "sourceProvider": "GeminiBatch34TextureExpansion",
        "providerLicensePage": "",
        "license": "USER_GENERATED_REVIEW_REQUIRED",
        "resolution": "1024px_per_source",
        "unityImportStatus": "PENDING UNITY IMPORT",
        "reviewStatus": "PENDING UNITY MATERIAL PREVIEW",
        "mapPacking": {
            "sourceMRAO": "RGBA = generated Metallic, Roughness, Ambient Occlusion, Emission/wetness placeholder",
            "sourceARM": "RGB = Ambient Occlusion, Roughness, Metallic",
            "unityMaskMap": "RGBA = Metallic, Ambient Occlusion, unused zero, Smoothness",
        },
        "sourceCurationManifest": project_rel(CURATION_MANIFEST),
        "preview": project_rel(preview_path),
        "assets": assets,
        "skipped": skipped,
    }
    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    manifest_path = OUTPUT_ROOT / MANIFEST_NAME
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")

    print("BATCH34_UNITY_PACK_PROMOTION_DONE")
    print(f"selected_material_assets={len(assets)}")
    print(f"skipped_non_material_or_not_ready={len(skipped)}")
    print(f"manifest={project_rel(manifest_path)}")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dry-run", action="store_true", help="Reserved for future use; current tool writes the importable pack.")
    return promote(parser.parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
