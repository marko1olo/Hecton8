#!/usr/bin/env python3
"""Clean Gemini material outputs, compress them, and build importable PBR manifests."""

from __future__ import annotations

import argparse
import io
import json
import subprocess
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
INTAKE_ROOT = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialIntake_20260607"
ATLAS_ROOT = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases"
ATLAS_BATCH = "Batch20260607_MicroPanel"
WATERMARK_PROFILES = {
    "transparent_pressure_glass_edge_wear": (0.883, 0.883, 0.16),
    "salvage_worn_repair_metal": (0.735, 0.745, 0.18),
}

INPUTS = (
    {
        "kind": "atlas",
        "path": r"C:\Users\danat\Downloads\Precision Tool Micro-Panel Atlas.png",
        "id": "precision_tool_micro_panel_atlas",
    },
    {
        "kind": "single",
        "path": r"C:\Users\danat\Downloads\Wet Service Panel Biofilm.png",
        "id": "wet_service_panel_biofilm",
        "title": "Wet Service Panel Biofilm",
        "surfaceClass": "wet_service_panel_biofilm",
        "role": "wet aged service panel material for base equipment, old modules, and damp industrial props",
        "heldToolAllowed": False,
        "stationPropAllowed": True,
        "salvageAllowed": True,
        "worldPanelAllowed": True,
        "tilingScale": 2.4,
        "metallic": 0.44,
        "smoothness": 0.26,
        "normalScale": 0.50,
        "heightScale": 0.003,
    },
    {
        "kind": "single",
        "path": r"C:\Users\danat\Downloads\Transparent Pressure Glass Edge Wear.png",
        "id": "transparent_pressure_glass_edge_wear",
        "title": "Transparent Pressure Glass Edge Wear",
        "surfaceClass": "pressure_acrylic_viewport",
        "role": "smoky cyan acrylic/glass surface for scanner lenses, small viewports, and instrument covers",
        "heldToolAllowed": True,
        "stationPropAllowed": True,
        "salvageAllowed": False,
        "worldPanelAllowed": False,
        "tilingScale": 2.8,
        "metallic": 0.0,
        "smoothness": 0.64,
        "normalScale": 0.25,
        "heightScale": 0.001,
    },
    {
        "kind": "single",
        "path": r"C:\Users\danat\Downloads\Salvage-Worn Repair Metal.png",
        "id": "salvage_worn_repair_metal",
        "title": "Salvage-Worn Repair Metal",
        "surfaceClass": "repaired_salvage_metal",
        "role": "patched salvage metal for damaged props, wreckage panels, and repairable industrial equipment",
        "heldToolAllowed": False,
        "stationPropAllowed": True,
        "salvageAllowed": True,
        "worldPanelAllowed": False,
        "tilingScale": 2.2,
        "metallic": 0.48,
        "smoothness": 0.16,
        "normalScale": 0.72,
        "heightScale": 0.006,
    },
    {
        "kind": "single",
        "path": r"C:\Users\danat\Downloads\White Ceramic Sensor Casing.png",
        "id": "white_ceramic_sensor_casing",
        "title": "White Ceramic Sensor Casing",
        "surfaceClass": "scientific_ceramic_casing",
        "role": "off-white ceramic/composite casing for scanner, analyzer, and clean science tool bodies",
        "heldToolAllowed": True,
        "stationPropAllowed": True,
        "salvageAllowed": False,
        "worldPanelAllowed": False,
        "tilingScale": 3.0,
        "metallic": 0.0,
        "smoothness": 0.42,
        "normalScale": 0.36,
        "heightScale": 0.002,
    },
    {
        "kind": "single",
        "path": r"C:\Users\danat\Downloads\Dark Anodized Tool Metal.png",
        "id": "dark_anodized_tool_metal",
        "title": "Dark Anodized Tool Metal",
        "surfaceClass": "dark_anodized_tool_metal",
        "role": "dark anodized aluminum for premium compact tool frames and inset rails",
        "heldToolAllowed": True,
        "stationPropAllowed": True,
        "salvageAllowed": False,
        "worldPanelAllowed": False,
        "tilingScale": 4.2,
        "metallic": 0.78,
        "smoothness": 0.34,
        "normalScale": 0.46,
        "heightScale": 0.002,
    },
    {
        "kind": "single",
        "path": r"C:\Users\danat\Downloads\Fine Ribbed Metal Trim.png",
        "id": "fine_ribbed_metal_trim",
        "title": "Fine Ribbed Metal Trim",
        "surfaceClass": "fine_corrugated_trim",
        "role": "small-scale ribbed trim for laser cutter, flashlight, drill, and compact equipment ribs",
        "heldToolAllowed": True,
        "stationPropAllowed": True,
        "salvageAllowed": True,
        "worldPanelAllowed": False,
        "tilingScale": 4.4,
        "metallic": 0.66,
        "smoothness": 0.28,
        "normalScale": 0.92,
        "heightScale": 0.007,
    },
    {
        "kind": "single",
        "path": r"C:\Users\danat\Downloads\Orange Safety Composite Panel.png",
        "id": "orange_safety_composite_panel",
        "title": "Orange Safety Composite Panel",
        "surfaceClass": "safety_composite_panel",
        "role": "orange-red polymer composite for readable safety accents on tools and survival equipment",
        "heldToolAllowed": True,
        "stationPropAllowed": True,
        "salvageAllowed": False,
        "worldPanelAllowed": False,
        "tilingScale": 1.15,
        "metallic": 0.0,
        "smoothness": 0.30,
        "normalScale": 0.46,
        "heightScale": 0.003,
    },
    {
        "kind": "single",
        "path": r"C:\Users\danat\Downloads\Black Waterproof Grip Rubber.png",
        "id": "black_waterproof_grip_rubber",
        "title": "Black Waterproof Grip Rubber",
        "surfaceClass": "waterproof_rubber",
        "role": "black waterproof grip rubber for handheld tool grips and pressure-sealed handles",
        "heldToolAllowed": True,
        "stationPropAllowed": True,
        "salvageAllowed": False,
        "worldPanelAllowed": False,
        "tilingScale": 5.0,
        "metallic": 0.0,
        "smoothness": 0.18,
        "normalScale": 0.68,
        "heightScale": 0.003,
    },
    {
        "kind": "single",
        "path": r"C:\Users\danat\Downloads\Clean NASA-Punk Tool Housing Metal.png",
        "id": "clean_nasa_punk_tool_housing_metal",
        "title": "Clean NASA-Punk Tool Housing Metal",
        "surfaceClass": "clean_tool_housing",
        "role": "clean blue painted pressure-rated tool housing for scanner, builder, and compact equipment shells",
        "heldToolAllowed": True,
        "stationPropAllowed": True,
        "salvageAllowed": False,
        "worldPanelAllowed": False,
        "tilingScale": 3.5,
        "metallic": 0.56,
        "smoothness": 0.36,
        "normalScale": 0.70,
        "heightScale": 0.004,
    },
)


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def clamp_rect(x0: int, y0: int, size: int, width: int, height: int) -> tuple[int, int, int, int]:
    x0 = max(0, min(x0, width - 1))
    y0 = max(0, min(y0, height - 1))
    return x0, y0, min(width, x0 + size), min(height, y0 + size)


def feathered_patch_mask(size: tuple[int, int]) -> Image.Image:
    width, height = size
    mask = Image.new("L", size, 0)
    draw = ImageDraw.Draw(mask)
    margin = max(4, int(min(width, height) * 0.08))
    draw.rounded_rectangle((margin, margin, width - margin, height - margin), radius=max(8, margin), fill=255)
    return mask.filter(ImageFilter.GaussianBlur(radius=max(14, int(width * 0.08))))


def candidate_score(target: Image.Image, candidate: Image.Image) -> float:
    target_np = np.asarray(target.convert("RGB"), dtype=np.float32)
    cand_np = np.asarray(candidate.convert("RGB"), dtype=np.float32)
    hsv_like = target_np / 255.0
    bright = hsv_like.max(axis=2)
    dark = hsv_like.min(axis=2)
    saturation = np.where(bright > 0.001, (bright - dark) / np.maximum(bright, 0.001), 0.0)
    keep = ~((bright > np.percentile(bright, 86)) & (saturation < 0.35))
    if keep.sum() < target_np.shape[0] * target_np.shape[1] * 0.35:
        keep = np.ones_like(bright, dtype=bool)
    target_sample = target_np[keep]
    cand_sample = cand_np[keep]
    return float(np.mean(np.abs(target_sample.mean(axis=0) - cand_sample.mean(axis=0))) + np.mean(np.abs(target_sample.std(axis=0) - cand_sample.std(axis=0))) * 0.35)


def repair_watermark(image: Image.Image, profile: tuple[float, float, float] | None = None) -> Image.Image:
    base = image.convert("RGB")
    width, height = base.size
    center_u, center_v, size_fraction = profile or (0.944, 0.944, 0.125)
    size = int(min(width, height) * size_fraction)
    center_x = int(width * center_u)
    center_y = int(height * center_v)
    x0, y0, x1, y1 = clamp_rect(center_x - size // 2, center_y - size // 2, size, width, height)
    patch_size = (x1 - x0, y1 - y0)
    target = base.crop((x0, y0, x1, y1))

    offsets = (
        (-size - int(width * 0.035), 0),
        (-size * 2, 0),
        (0, -size - int(height * 0.035)),
        (-size - int(width * 0.035), -size - int(height * 0.035)),
        (-int(width * 0.18), -int(height * 0.08)),
    )
    best_patch = None
    best_score = float("inf")
    for dx, dy in offsets:
        sx0 = max(0, min(width - patch_size[0], x0 + dx))
        sy0 = max(0, min(height - patch_size[1], y0 + dy))
        candidate = base.crop((sx0, sy0, sx0 + patch_size[0], sy0 + patch_size[1]))
        score = candidate_score(target, candidate)
        if score < best_score:
            best_score = score
            best_patch = candidate

    if best_patch is None:
        return base

    donor = best_patch.filter(ImageFilter.GaussianBlur(radius=0.35))
    mask = feathered_patch_mask(patch_size)
    repaired = base.copy()
    repaired.paste(donor, (x0, y0), mask)
    return repaired


def save_jpeg(image: Image.Image, path: Path, size: int, quality: int, max_bytes: int | None = None) -> Image.Image:
    output = image.convert("RGB")
    if size > 0 and max(output.size) > size:
        output.thumbnail((size, size), Image.Resampling.LANCZOS)
    path.parent.mkdir(parents=True, exist_ok=True)
    if max_bytes is None:
        output.save(path, "JPEG", quality=quality, optimize=True, progressive=True, subsampling=0)
        return output

    best_blob: bytes | None = None
    for subsampling in (0, 1, 2):
        for current_quality in range(quality, 70, -4):
            buffer = io.BytesIO()
            output.save(
                buffer,
                "JPEG",
                quality=current_quality,
                optimize=True,
                progressive=True,
                subsampling=subsampling,
            )
            blob = buffer.getvalue()
            best_blob = blob
            if len(blob) <= max_bytes:
                path.write_bytes(blob)
                return output

    if best_blob is None:
        output.save(path, "JPEG", quality=quality, optimize=True, progressive=True, subsampling=0)
    else:
        path.write_bytes(best_blob)
    return output


def seam_score(image: Image.Image, strip: int = 18) -> float:
    rgb = image.convert("RGB")
    data = np.asarray(rgb, dtype=np.float32) / 255.0
    height, width, _ = data.shape
    strip = max(4, min(strip, width // 8, height // 8))
    left = data[:, :strip, :]
    right = data[:, -strip:, :][:, ::-1, :]
    top = data[:strip, :, :]
    bottom = data[-strip:, :, :][::-1, :, :]
    lr = float(np.mean(np.abs(left - right)))
    tb = float(np.mean(np.abs(top - bottom)))
    return (lr + tb) * 50.0


def repair_tile_seams(
    image: Image.Image,
    border_fraction: float = 0.075,
    strength_multiplier: float = 1.0,
) -> Image.Image:
    rgb = image.convert("RGB")
    data = np.asarray(rgb, dtype=np.float32).copy()
    height, width, _ = data.shape
    margin = max(8, min(int(min(width, height) * border_fraction), min(width, height) // 4))

    for index in range(margin):
        strength = ((1.0 - (index / float(margin))) ** 2) * strength_multiplier
        left_index = index
        right_index = width - 1 - index
        average = (data[:, left_index, :] + data[:, right_index, :]) * 0.5
        data[:, left_index, :] = data[:, left_index, :] * (1.0 - strength) + average * strength
        data[:, right_index, :] = data[:, right_index, :] * (1.0 - strength) + average * strength

    for index in range(margin):
        strength = ((1.0 - (index / float(margin))) ** 2) * strength_multiplier
        top_index = index
        bottom_index = height - 1 - index
        average = (data[top_index, :, :] + data[bottom_index, :, :]) * 0.5
        data[top_index, :, :] = data[top_index, :, :] * (1.0 - strength) + average * strength
        data[bottom_index, :, :] = data[bottom_index, :, :] * (1.0 - strength) + average * strength

    return Image.fromarray(np.uint8(np.clip(data, 0.0, 255.0)), "RGB")


def prepare_tileable_base(image: Image.Image, threshold: float) -> tuple[Image.Image, float, float, bool]:
    before = seam_score(image)
    target = min(threshold, 1.55)
    if before <= target:
        return image.convert("RGB"), before, before, False

    repaired = repair_tile_seams(image)
    after = seam_score(repaired)
    best = repaired
    best_after = after
    if best_after > target:
        for border_fraction in (0.10, 0.125, 0.15):
            candidate = repair_tile_seams(repaired, border_fraction=border_fraction, strength_multiplier=1.0)
            candidate_score_value = seam_score(candidate)
            if candidate_score_value < best_after:
                best = candidate
                best_after = candidate_score_value
            if best_after <= target:
                break

    return best, before, best_after, True


def height_from_base(base: Image.Image) -> Image.Image:
    gray = base.convert("L").filter(ImageFilter.GaussianBlur(radius=0.85))
    values = np.asarray(gray, dtype=np.float32)
    low, high = np.percentile(values, [4, 96])
    if high <= low:
        high = low + 1.0
    normalized = np.clip((values - low) / (high - low), 0.0, 1.0)
    return Image.fromarray(np.uint8(normalized * 255.0), "L")


def normal_from_height(height: Image.Image, strength: float) -> Image.Image:
    data = np.asarray(height, dtype=np.float32) / 255.0
    dx = np.zeros_like(data)
    dy = np.zeros_like(data)
    dx[:, 1:-1] = data[:, 2:] - data[:, :-2]
    dy[1:-1, :] = data[2:, :] - data[:-2, :]
    nx = -dx * strength
    ny = -dy * strength
    nz = np.ones_like(data)
    length = np.maximum(np.sqrt(nx * nx + ny * ny + nz * nz), 0.0001)
    normal = np.stack((nx / length, ny / length, nz / length), axis=2)
    return Image.fromarray(np.uint8(np.clip((normal * 0.5 + 0.5) * 255.0, 0.0, 255.0)), "RGB")


def save_maps(base: Image.Image, spec: dict, asset_dir: Path, asset_id: str) -> dict[str, str]:
    height = height_from_base(base)
    normal = normal_from_height(height, float(spec["normalScale"]) * 2.2)
    height_values = np.asarray(height, dtype=np.float32) / 255.0
    ao = np.clip(0.72 + height_values * 0.28, 0.0, 1.0)
    roughness = np.full_like(ao, 1.0 - float(spec["smoothness"]))
    metal = np.full_like(ao, float(spec["metallic"]))
    zero = np.zeros_like(ao)
    smoothness = np.full_like(ao, float(spec["smoothness"]))

    normal_path = asset_dir / f"TX_GM_{asset_id}_NormalGL.jpg"
    arm_path = asset_dir / f"TX_GM_{asset_id}_ARM_AO_Rough_Metal.jpg"
    height_path = asset_dir / f"TX_GM_{asset_id}_Height.jpg"
    mask_path = asset_dir / f"TX_GM_{asset_id}_MaskMap_UnityURP.png"

    normal.save(normal_path, "JPEG", quality=90, optimize=True, progressive=True, subsampling=0)
    Image.fromarray(np.uint8(np.stack((ao, roughness, metal), axis=2) * 255.0), "RGB").save(
        arm_path, "JPEG", quality=92, optimize=True, progressive=True, subsampling=0
    )
    height.save(height_path, "JPEG", quality=88, optimize=True, progressive=True)
    Image.fromarray(np.uint8(np.stack((metal, ao, zero, smoothness), axis=2) * 255.0), "RGBA").save(mask_path, "PNG")

    return {
        "NormalGL": display_path(normal_path),
        "ARM_AO_Rough_Metal": display_path(arm_path),
        "Height": display_path(height_path),
        "MaskMap_UnityURP": display_path(mask_path),
    }


def make_preview(assets: list[dict], rejected: list[dict], output: Path) -> None:
    entries = [("ACCEPT", asset["id"], asset["maps"]["BaseColor"]) for asset in assets]
    entries.extend(("REJECT", item["id"], item["cleanedPath"]) for item in rejected)
    tile = 180
    label_h = 36
    gap = 12
    columns = 5
    rows = max(1, (len(entries) + columns - 1) // columns)
    canvas = Image.new("RGB", (columns * tile + (columns - 1) * gap, rows * (tile + label_h) + (rows - 1) * gap), (8, 12, 14))
    draw = ImageDraw.Draw(canvas)
    for index, (state, label, raw_path) in enumerate(entries):
        with Image.open(ROOT / raw_path if not Path(raw_path).is_absolute() else Path(raw_path)) as image:
            preview = image.convert("RGB")
            preview.thumbnail((tile, tile), Image.Resampling.LANCZOS)
        x = (index % columns) * (tile + gap) + (tile - preview.width) // 2
        y = (index // columns) * (tile + label_h + gap) + (tile - preview.height) // 2
        base_x = (index % columns) * (tile + gap)
        base_y = (index // columns) * (tile + label_h + gap)
        canvas.paste(preview, (x, y))
        fill = (38, 10, 10) if state == "REJECT" else (5, 9, 11)
        draw.rectangle((base_x, base_y + tile, base_x + tile, base_y + tile + label_h), fill=fill)
        draw.text((base_x + 5, base_y + tile + 6), f"{state} {label}"[:29], fill=(210, 225, 225))
    output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output, "PNG")


def process(args: argparse.Namespace) -> int:
    singles_root = INTAKE_ROOT / "Singles"
    source_root = INTAKE_ROOT / "SourceCleaned"
    rejected_root = INTAKE_ROOT / "Rejected"
    manifest_assets: list[dict] = []
    rejected: list[dict] = []
    processed = 0

    for spec in INPUTS:
        source = Path(spec["path"])
        if not source.exists():
            raise FileNotFoundError(str(source))
        with Image.open(source) as image:
            repaired = repair_watermark(image, WATERMARK_PROFILES.get(spec["id"]))

        cleaned_source = source_root / f"TX_GM_{spec['id']}_Cleaned_2k.jpg"
        save_jpeg(repaired, cleaned_source, 2048, args.source_quality, args.source_max_mb * 1024 * 1024)
        processed += 1

        if spec["kind"] == "atlas":
            atlas_source_dir = ATLAS_ROOT / ATLAS_BATCH / "Source"
            atlas_source = atlas_source_dir / f"TX_GM_{ATLAS_BATCH}_Cleaned_2k.jpg"
            save_jpeg(repaired, atlas_source, 2048, args.source_quality)
            cmd = [
                sys.executable,
                "-B",
                str(ROOT / "Tools/SplitGeminiMaterialAtlas.py"),
                "--image",
                str(atlas_source),
                "--batch",
                ATLAS_BATCH,
                "--output-root",
                str(ATLAS_ROOT),
                "--max-tile-size",
                str(args.atlas_tile_size),
                "--margin-fraction",
                "0.0",
            ]
            subprocess.run(cmd, cwd=str(ROOT), check=True)
            continue

        if spec["kind"] == "reject":
            reject_path = rejected_root / f"TX_GM_{spec['id']}_Rejected_Cleaned_2k.jpg"
            save_jpeg(repaired, reject_path, 2048, args.source_quality, args.source_max_mb * 1024 * 1024)
            rejected.append(
                {
                    "id": spec["id"],
                    "reason": spec["reason"],
                    "source": str(source),
                    "cleanedPath": display_path(reject_path),
                }
            )
            continue

        asset_id = f"gemini_20260607_{spec['id']}"
        asset_dir = singles_root / asset_id
        base_path = asset_dir / f"TX_GM_{asset_id}_BaseColor.jpg"
        tileable, seam_before, seam_after, seam_repaired = prepare_tileable_base(repaired, args.seam_threshold)
        base = save_jpeg(tileable, base_path, args.single_size, args.base_quality, args.base_max_mb * 1024 * 1024)
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
                "surfaceClass": spec["surfaceClass"],
                "heldToolAllowed": spec["heldToolAllowed"],
                "stationPropAllowed": spec["stationPropAllowed"],
                "salvageAllowed": spec["salvageAllowed"],
                "worldPanelAllowed": spec["worldPanelAllowed"],
                "tilingScale": spec["tilingScale"],
                "metallic": spec["metallic"],
                "smoothness": spec["smoothness"],
                "normalScale": spec["normalScale"],
                "heightScale": spec["heightScale"],
                "provisionalPbrMaps": True,
                "watermarkRepaired": True,
                "seamScoreBefore": round(seam_before, 4),
                "seamScoreAfter": round(seam_after, 4),
                "seamRepaired": seam_repaired,
                "maps": maps,
            }
        )

    preview = INTAKE_ROOT / "PREVIEW_GeminiSingleMaterials_20260607.png"
    make_preview(manifest_assets, rejected, preview)
    manifest = {
        "schema": "hecton8.external_pbr_pack.v1",
        "sourceProvider": "GeminiManualSingleMaterials",
        "providerLicensePage": "",
        "license": "USER_GENERATED_REVIEW_REQUIRED",
        "resolution": f"{args.single_size}px",
        "unityImportStatus": "PENDING UNITY IMPORT",
        "reviewStatus": "PENDING VISUAL REVIEW",
        "mapPacking": {
            "sourceARM": "RGB = generated Ambient Occlusion, Roughness, Metal",
            "unityMaskMap": "RGBA = Metal, Ambient Occlusion, unused zero, Smoothness",
        },
        "assets": manifest_assets,
        "rejected": rejected,
        "preview": display_path(preview),
    }
    manifest_path = INTAKE_ROOT / "GeminiSingleMaterials_Manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")

    print("GEMINI_MATERIAL_INTAKE_STATUS: PASS")
    print(f"processed_images={processed}")
    print(f"accepted_single_materials={len(manifest_assets)}")
    print(f"rejected={len(rejected)}")
    print(f"single_manifest={display_path(manifest_path)}")
    print(f"single_preview={display_path(preview)}")
    print(f"atlas_batch={ATLAS_BATCH}")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--single-size", type=int, default=1024)
    parser.add_argument("--atlas-tile-size", type=int, default=512)
    parser.add_argument("--source-quality", type=int, default=90)
    parser.add_argument("--base-quality", type=int, default=86)
    parser.add_argument("--source-max-mb", type=float, default=1.5)
    parser.add_argument("--base-max-mb", type=float, default=0.75)
    parser.add_argument("--seam-threshold", type=float, default=2.8)
    return process(parser.parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
