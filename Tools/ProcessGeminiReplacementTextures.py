#!/usr/bin/env python3
"""Integrate high-value Gemini replacement textures into live first-party targets."""

from __future__ import annotations

import io
import json
from dataclasses import dataclass
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageOps


ROOT = Path(__file__).resolve().parents[1]
GENERATED_ROOT = ROOT / "Assets/_Project/Art/TEXTURES/Generated"
REPORT_PATH = GENERATED_ROOT / "GeminiReplacementTextureIntegration_20260607.json"
PREVIEW_PATH = ROOT / "Temp/GeminiReplacementProcessedPreview_20260607.png"
WATERMARK_PREVIEW_PATH = ROOT / "Temp/GeminiReplacementWatermarkCrops_20260607.png"
SOURCE_OVERRIDE_ROOT = GENERATED_ROOT / "GeminiReplacementSources_20260607"
ATLAS_MANIFEST = GENERATED_ROOT / "GeminiMaterialAtlases/Batch20260607_MicroPanel/GeminiMaterialAtlas_Manifest.json"
CARBON_ID = "gemini_Batch20260607_MicroPanel_matte_carbon_composite"
CARBON_TILE_DIR = GENERATED_ROOT / f"GeminiMaterialAtlases/Batch20260607_MicroPanel/Tiles/{CARBON_ID}"
KELP_DIR = ROOT / "Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.abyssal"
BARNACLE_PATH = ROOT / "Assets/_Project/Art/TEXTURES/Meshy_AI_Alien_barnacles_clust_0301230506_texture.png"


@dataclass(frozen=True)
class PatchRegion:
    x0: float
    y0: float
    x1: float
    y1: float
    donor_dx: float
    donor_dy: float
    blur: float = 0.35
    edge_margin: float = 0.0


@dataclass(frozen=True)
class ReplacementSpec:
    asset_id: str
    source: Path
    role: str
    target: str
    patches: tuple[PatchRegion, ...]
    tileable: bool
    output_size: int = 1024
    crop_rect: tuple[float, float, float, float] | None = None
    seam_border_fraction: float = 0.065
    seam_strength: float = 0.72


SPECS = (
    ReplacementSpec(
        asset_id="matte_carbon_composite_replacement",
        source=Path(r"C:\Users\danat\Downloads\Carbon composite micro-panel.png"),
        role="clean woven carbon composite for premium compact tool shells and structural micro-panels",
        target="atlas_tile",
        patches=(
            PatchRegion(0.755, 0.760, 0.995, 0.995, -0.265, -0.015, 0.25),
            PatchRegion(0.825, 0.885, 0.995, 0.995, -0.315, -0.155, 0.30),
            PatchRegion(0.640, 0.865, 0.995, 0.995, -0.430, -0.205, 0.45),
        ),
        tileable=True,
        crop_rect=(0.210, 0.620, 0.530, 0.940),
        seam_border_fraction=0.18,
        seam_strength=1.0,
    ),
    ReplacementSpec(
        asset_id="abyssal_strap_kelp_replacement",
        source=Path(r"C:\Users\danat\Downloads\Abyssal strap kelp.png"),
        role="active family.kelp.abyssal albedo/normal/mask/detail stack for deep route kelp readability",
        target="kelp_stack",
        patches=(
            PatchRegion(0.840, 0.815, 0.995, 0.995, -0.265, -0.035, 0.25, 0.055),
            PatchRegion(0.875, 0.890, 0.995, 0.995, -0.325, -0.145, 0.30, 0.055),
        ),
        tileable=True,
    ),
    ReplacementSpec(
        asset_id="alien_barnacle_colony_replacement",
        source=Path(r"C:\Users\danat\Downloads\Alien barnacle colony.png"),
        role="active barnacle colony UV-atlas basecolor; intentionally not treated as seamless material",
        target="barnacle_uv_atlas",
        patches=(
            PatchRegion(0.845, 0.835, 0.995, 0.995, -0.285, -0.040, 0.30),
            PatchRegion(0.895, 0.895, 0.995, 0.995, -0.350, -0.170, 0.35),
            PatchRegion(0.820, 0.790, 0.995, 0.995, -0.440, -0.245, 0.40),
        ),
        tileable=False,
    ),
)


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def project_path(raw: str) -> Path:
    path = Path(raw)
    return path if path.is_absolute() else ROOT / path


def png_size(path: Path) -> int:
    return path.stat().st_size if path.exists() else 0


def normalize_rgb(image: Image.Image) -> Image.Image:
    return ImageOps.exif_transpose(image).convert("RGB")


def feather_mask(size: tuple[int, int], edge_margin: float) -> Image.Image:
    width, height = size
    mask = Image.new("L", size, 0)
    draw = ImageDraw.Draw(mask)
    margin = max(0, int(min(width, height) * edge_margin))
    draw.rounded_rectangle(
        (margin, margin, width - margin, height - margin),
        radius=max(8, int(max(margin * 2, min(width, height) * 0.05))),
        fill=255,
    )
    blur_radius = max(5, int(min(width, height) * (0.060 if edge_margin > 0.0 else 0.018)))
    return mask.filter(ImageFilter.GaussianBlur(radius=blur_radius))


def apply_patch_region(image: Image.Image, region: PatchRegion) -> Image.Image:
    base = image.convert("RGB")
    width, height = base.size
    x0 = int(width * region.x0)
    y0 = int(height * region.y0)
    x1 = int(width * region.x1)
    y1 = int(height * region.y1)
    patch_width = max(4, x1 - x0)
    patch_height = max(4, y1 - y0)
    sx0 = max(0, min(width - patch_width, x0 + int(width * region.donor_dx)))
    sy0 = max(0, min(height - patch_height, y0 + int(height * region.donor_dy)))
    donor = base.crop((sx0, sy0, sx0 + patch_width, sy0 + patch_height)).filter(ImageFilter.GaussianBlur(region.blur))
    mask = feather_mask((patch_width, patch_height), region.edge_margin)
    output = base.copy()
    output.paste(donor, (x0, y0), mask)
    return output


def repair_watermark(image: Image.Image, spec: ReplacementSpec) -> Image.Image:
    repaired = normalize_rgb(image)
    for patch in spec.patches:
        repaired = apply_patch_region(repaired, patch)
    return repaired


def crop_spec_region(image: Image.Image, spec: ReplacementSpec) -> Image.Image:
    if spec.crop_rect is None:
        return image.convert("RGB")
    width, height = image.size
    x0 = int(width * spec.crop_rect[0])
    y0 = int(height * spec.crop_rect[1])
    x1 = int(width * spec.crop_rect[2])
    y1 = int(height * spec.crop_rect[3])
    return image.crop((x0, y0, x1, y1)).convert("RGB")


def save_jpeg_capped(image: Image.Image, path: Path, max_size: int, quality: int = 91, max_bytes: int = 1_350_000) -> Image.Image:
    output = image.convert("RGB")
    if max(output.size) > max_size:
        output.thumbnail((max_size, max_size), Image.Resampling.LANCZOS)
    path.parent.mkdir(parents=True, exist_ok=True)
    best: bytes | None = None
    for subsampling in (0, 1, 2):
        for current_quality in range(quality, 70, -3):
            buffer = io.BytesIO()
            output.save(buffer, "JPEG", quality=current_quality, optimize=True, progressive=True, subsampling=subsampling)
            blob = buffer.getvalue()
            best = blob
            if len(blob) <= max_bytes:
                path.write_bytes(blob)
                return output
    if best is not None:
        path.write_bytes(best)
    else:
        output.save(path, "JPEG", quality=quality, optimize=True, progressive=True, subsampling=1)
    return output


def save_png(image: Image.Image, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path, "PNG", optimize=True, compress_level=9)


def resized_square(image: Image.Image, size: int) -> Image.Image:
    output = image.convert("RGB")
    if output.size != (size, size):
        output = output.resize((size, size), Image.Resampling.LANCZOS)
    return output


def seam_score(image: Image.Image, strip: int = 20) -> float:
    rgb = image.convert("RGB")
    data = np.asarray(rgb, dtype=np.float32) / 255.0
    height, width, _ = data.shape
    strip = max(4, min(strip, width // 8, height // 8))
    left = data[:, :strip, :]
    right = data[:, -strip:, :][:, ::-1, :]
    top = data[:strip, :, :]
    bottom = data[-strip:, :, :][::-1, :, :]
    return float((np.mean(np.abs(left - right)) + np.mean(np.abs(top - bottom))) * 50.0)


def repair_tile_seams(image: Image.Image, border_fraction: float, strength_multiplier: float) -> Image.Image:
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


def prepare_tileable(image: Image.Image, spec: ReplacementSpec) -> tuple[Image.Image, float | None, float | None, bool]:
    enabled = spec.tileable
    if not enabled:
        return image.convert("RGB"), None, None, False
    before = seam_score(image)
    if before <= 2.45:
        return image.convert("RGB"), before, before, False
    repaired = repair_tile_seams(image, spec.seam_border_fraction, spec.seam_strength)
    return repaired, before, seam_score(repaired), True


def height_from_base(base: Image.Image) -> Image.Image:
    gray = base.convert("L").filter(ImageFilter.GaussianBlur(radius=0.75))
    values = np.asarray(gray, dtype=np.float32)
    low, high = np.percentile(values, [3, 97])
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


def detail_from_base(base: Image.Image) -> Image.Image:
    gray = base.convert("L")
    blur = gray.filter(ImageFilter.GaussianBlur(radius=4.0))
    high = np.asarray(gray, dtype=np.float32) - np.asarray(blur, dtype=np.float32)
    detail = np.clip((high * 1.65) + 128.0, 0.0, 255.0)
    channel = Image.fromarray(np.uint8(detail), "L").filter(ImageFilter.UnsharpMask(radius=1.0, percent=95, threshold=2))
    return Image.merge("RGB", (channel, channel, channel))


def kelp_mask_from_base(base: Image.Image, height: Image.Image) -> Image.Image:
    rgb = np.asarray(base.convert("RGB"), dtype=np.float32) / 255.0
    h = np.asarray(height, dtype=np.float32) / 255.0
    red = rgb[:, :, 0]
    green = rgb[:, :, 1]
    blue = rgb[:, :, 2]
    brightness = np.maximum.reduce((red, green, blue))
    cyan_bias = np.clip(((green + blue) * 0.5 - red * 0.82) * 2.2, 0.0, 1.0)
    thickness = np.clip(h * 0.74 + brightness * 0.26, 0.0, 1.0)
    biolum_hint = np.clip(cyan_bias * (0.35 + brightness * 0.65), 0.0, 1.0)
    wet_ao = np.clip(0.58 + h * 0.42, 0.0, 1.0)
    packed = np.stack((thickness, biolum_hint, wet_ao), axis=2)
    return Image.fromarray(np.uint8(packed * 255.0), "RGB")


def save_carbon_maps(base: Image.Image, report: dict) -> dict[str, str]:
    height = height_from_base(base)
    normal = normal_from_height(height, 1.10)
    height_values = np.asarray(height, dtype=np.float32) / 255.0
    ao = np.clip(0.76 + height_values * 0.24, 0.0, 1.0)
    roughness = np.full_like(ao, 0.66)
    metal = np.zeros_like(ao)
    smoothness = np.full_like(ao, 0.34)
    zero = np.zeros_like(ao)

    paths = {
        "BaseColor": CARBON_TILE_DIR / f"TX_GM_{CARBON_ID}_BaseColor.png",
        "NormalGL": CARBON_TILE_DIR / f"TX_GM_{CARBON_ID}_NormalGL.png",
        "ARM_AO_Rough_Metal": CARBON_TILE_DIR / f"TX_GM_{CARBON_ID}_ARM_AO_Rough_Metal.png",
        "Height": CARBON_TILE_DIR / f"TX_GM_{CARBON_ID}_Height.png",
        "MaskMap_UnityURP": CARBON_TILE_DIR / f"TX_GM_{CARBON_ID}_MaskMap_UnityURP.png",
    }
    before = {key: png_size(path) for key, path in paths.items()}
    save_png(base, paths["BaseColor"])
    save_png(normal, paths["NormalGL"])
    save_png(Image.fromarray(np.uint8(np.stack((ao, roughness, metal), axis=2) * 255.0), "RGB"), paths["ARM_AO_Rough_Metal"])
    save_png(height, paths["Height"])
    save_png(Image.fromarray(np.uint8(np.stack((metal, ao, zero, smoothness), axis=2) * 255.0), "RGBA"), paths["MaskMap_UnityURP"])
    after = {key: png_size(path) for key, path in paths.items()}
    report["mapBytesBefore"] = before
    report["mapBytesAfter"] = after
    return {key: display_path(path) for key, path in paths.items()}


def save_kelp_stack(base: Image.Image, report: dict) -> dict[str, str]:
    height = height_from_base(base)
    normal = normal_from_height(height, 1.35)
    detail = detail_from_base(base)
    mask = kelp_mask_from_base(base, height)
    paths = {
        "albedo": KELP_DIR / "albedo___family.kelp.abyssal.png",
        "normal": KELP_DIR / "normal___family.kelp.abyssal.png",
        "mask": KELP_DIR / "mask___family.kelp.abyssal.png",
        "detail": KELP_DIR / "detail___family.kelp.abyssal.png",
    }
    before = {key: png_size(path) for key, path in paths.items()}
    save_png(base, paths["albedo"])
    save_png(normal, paths["normal"])
    save_png(mask, paths["mask"])
    save_png(detail, paths["detail"])
    after = {key: png_size(path) for key, path in paths.items()}
    report["mapBytesBefore"] = before
    report["mapBytesAfter"] = after
    return {key: display_path(path) for key, path in paths.items()}


def save_barnacle(base: Image.Image, report: dict) -> dict[str, str]:
    before = png_size(BARNACLE_PATH)
    save_png(base, BARNACLE_PATH)
    report["bytesBefore"] = before
    report["bytesAfter"] = png_size(BARNACLE_PATH)
    return {"BaseColor": display_path(BARNACLE_PATH)}


def update_atlas_manifest(carbon_maps: dict[str, str], source_path: Path, seam_before: float | None, seam_after: float | None, seam_repaired: bool) -> None:
    payload = json.loads(ATLAS_MANIFEST.read_text(encoding="utf-8-sig"))
    for asset in payload.get("assets", []) or []:
        if asset.get("id") != CARBON_ID:
            continue
        asset["source"] = display_path(source_path)
        asset["rawSource"] = r"C:\Users\danat\Downloads\Carbon composite micro-panel.png"
        asset["role"] = "clean woven carbon composite for premium compact tool shells and lightweight structural panels"
        asset["watermarkRisk"] = False
        asset["watermarkRepaired"] = True
        asset["replacementDate"] = "2026-06-07"
        asset["replacementStatus"] = "STATIC_VISUAL_REVIEWED_PENDING_UNITY_IMPORT"
        asset["resolutionOverride"] = "1024px_replacement_from_dedicated_gemini_source"
        asset["seamScoreBefore"] = seam_before
        asset["seamScoreAfter"] = seam_after
        asset["seamRepaired"] = seam_repaired
        asset["maps"] = carbon_maps
        break
    else:
        raise RuntimeError(f"missing manifest asset id: {CARBON_ID}")
    ATLAS_MANIFEST.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def make_previews(records: list[dict], watermark_entries: list[tuple[str, Image.Image, Image.Image]]) -> None:
    tiles: list[tuple[str, Path]] = []
    for record in records:
        if record["target"] == "atlas_tile":
            tiles.extend((f"carbon {key}", project_path(path)) for key, path in record["outputs"].items())
        elif record["target"] == "kelp_stack":
            tiles.extend((f"kelp {key}", project_path(path)) for key, path in record["outputs"].items())
        elif record["target"] == "barnacle_uv_atlas":
            tiles.extend((f"barnacle {key}", project_path(path)) for key, path in record["outputs"].items())

    tile_size = 190
    label_h = 34
    gap = 12
    cols = 5
    rows = max(1, (len(tiles) + cols - 1) // cols)
    canvas = Image.new("RGB", (cols * tile_size + (cols - 1) * gap, rows * (tile_size + label_h) + (rows - 1) * gap), (8, 12, 14))
    draw = ImageDraw.Draw(canvas)
    for index, (label, path) in enumerate(tiles):
        with Image.open(path) as image:
            thumb = image.convert("RGB")
            thumb.thumbnail((tile_size, tile_size), Image.Resampling.LANCZOS)
        x0 = (index % cols) * (tile_size + gap)
        y0 = (index // cols) * (tile_size + label_h + gap)
        canvas.paste(thumb, (x0 + (tile_size - thumb.width) // 2, y0 + (tile_size - thumb.height) // 2))
        draw.rectangle((x0, y0 + tile_size, x0 + tile_size, y0 + tile_size + label_h), fill=(5, 9, 11))
        draw.text((x0 + 5, y0 + tile_size + 7), label[:30], fill=(210, 225, 225))
    PREVIEW_PATH.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(PREVIEW_PATH, "PNG")

    crop = 360
    cols = 2
    rows = len(watermark_entries)
    wm = Image.new("RGB", (cols * tile_size + gap, rows * (tile_size + label_h) + (rows - 1) * gap), (8, 12, 14))
    draw = ImageDraw.Draw(wm)
    for row, (label, raw, repaired) in enumerate(watermark_entries):
        for col, (state, source) in enumerate((("raw", raw), ("clean", repaired))):
            sample = source.convert("RGB").crop((source.width - crop, source.height - crop, source.width, source.height))
            sample = sample.resize((tile_size, tile_size), Image.Resampling.LANCZOS)
            x0 = col * (tile_size + gap)
            y0 = row * (tile_size + label_h + gap)
            wm.paste(sample, (x0, y0))
            draw.rectangle((x0, y0 + tile_size, x0 + tile_size, y0 + tile_size + label_h), fill=(5, 9, 11))
            draw.text((x0 + 5, y0 + tile_size + 7), f"{label} {state}"[:30], fill=(210, 225, 225))
    wm.save(WATERMARK_PREVIEW_PATH, "PNG")


def process() -> int:
    records: list[dict] = []
    watermark_entries: list[tuple[str, Image.Image, Image.Image]] = []
    carbon_maps: dict[str, str] | None = None
    carbon_source: Path | None = None
    carbon_seam_before: float | None = None
    carbon_seam_after: float | None = None
    carbon_seam_repaired = False

    for spec in SPECS:
        if not spec.source.exists():
            raise FileNotFoundError(str(spec.source))
        with Image.open(spec.source) as raw_image:
            raw = normalize_rgb(raw_image)
        repaired_full = repair_watermark(raw, spec)
        watermark_entries.append((spec.asset_id.replace("_replacement", ""), raw, repaired_full))
        repaired = crop_spec_region(repaired_full, spec)
        cleaned_source = SOURCE_OVERRIDE_ROOT / f"TX_GR_{spec.asset_id}_Cleaned_2k.jpg"
        save_jpeg_capped(repaired, cleaned_source, 2048)
        tileable, seam_before, seam_after, seam_repaired = prepare_tileable(repaired, spec)
        base = resized_square(tileable, spec.output_size)
        record = {
            "assetId": spec.asset_id,
            "role": spec.role,
            "target": spec.target,
            "rawSource": str(spec.source),
            "cleanedSource": display_path(cleaned_source),
            "inputBytes": png_size(spec.source),
            "cleanedSourceBytes": png_size(cleaned_source),
            "inputDimensions": list(raw.size),
            "outputDimensions": [spec.output_size, spec.output_size],
            "watermarkRepair": "bottom_right_feathered_donor_patch",
            "sourceCropRect": list(spec.crop_rect) if spec.crop_rect else None,
            "tileable": spec.tileable,
            "seamScoreBefore": seam_before,
            "seamScoreAfter": seam_after,
            "seamRepaired": seam_repaired,
            "seamBorderFraction": spec.seam_border_fraction if spec.tileable else None,
            "seamStrength": spec.seam_strength if spec.tileable else None,
        }
        if spec.target == "atlas_tile":
            carbon_maps = save_carbon_maps(base, record)
            carbon_source = cleaned_source
            carbon_seam_before = seam_before
            carbon_seam_after = seam_after
            carbon_seam_repaired = seam_repaired
            record["outputs"] = carbon_maps
        elif spec.target == "kelp_stack":
            record["outputs"] = save_kelp_stack(base, record)
        elif spec.target == "barnacle_uv_atlas":
            record["outputs"] = save_barnacle(base, record)
        else:
            raise RuntimeError(f"unknown target: {spec.target}")
        records.append(record)

    if carbon_maps is None or carbon_source is None:
        raise RuntimeError("carbon replacement did not produce maps")
    update_atlas_manifest(carbon_maps, carbon_source, carbon_seam_before, carbon_seam_after, carbon_seam_repaired)
    make_previews(records, watermark_entries)

    report = {
        "schema": "hecton8.gemini_replacement_texture_integration.v1",
        "date": "2026-06-07",
        "status": "STATIC_SOURCE_INTEGRATED_PENDING_UNITY_IMPORT",
        "first20RouteMoment": "first-tool readability and abyssal route material upgrade",
        "unityImportStatus": "PENDING UNITY IMPORT",
        "visualProof": [display_path(PREVIEW_PATH), display_path(WATERMARK_PREVIEW_PATH)],
        "records": records,
    }
    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    REPORT_PATH.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")

    print("GEMINI_REPLACEMENT_TEXTURE_INTEGRATION: PASS")
    print(f"report={display_path(REPORT_PATH)}")
    print(f"preview={display_path(PREVIEW_PATH)}")
    print(f"watermarkPreview={display_path(WATERMARK_PREVIEW_PATH)}")
    for record in records:
        print(f"{record['assetId']}: target={record['target']} seam={record['seamScoreBefore']}->{record['seamScoreAfter']} bytes={record['inputBytes']}->{record['cleanedSourceBytes']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(process())
