#!/usr/bin/env python3
"""Split a manually generated Gemini 4x4 material atlas into provisional PBR tiles."""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_OUTPUT_ROOT = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases"

TILES = (
    {
        "id": "blue_painted_metal",
        "title": "Blue Painted Metal",
        "surfaceClass": "clean_tool_housing",
        "role": "clean painted tool housing for scanner, builder, and compact equipment shells",
        "heldToolAllowed": True,
        "stationPropAllowed": True,
        "salvageAllowed": False,
        "worldPanelAllowed": False,
        "tilingScale": 3.5,
        "metallic": 0.56,
        "smoothness": 0.36,
        "normalScale": 0.72,
        "heightScale": 0.004,
    },
    {
        "id": "black_grip_rubber",
        "title": "Black Grip Rubber",
        "surfaceClass": "waterproof_rubber",
        "role": "waterproof molded grip rubber for handheld tool handles and gasket-like grip zones",
        "heldToolAllowed": True,
        "stationPropAllowed": True,
        "salvageAllowed": False,
        "worldPanelAllowed": False,
        "tilingScale": 5.0,
        "metallic": 0.0,
        "smoothness": 0.18,
        "normalScale": 0.70,
        "heightScale": 0.003,
    },
    {
        "id": "dark_anodized_metal",
        "title": "Dark Anodized Metal",
        "surfaceClass": "dark_anodized_tool_metal",
        "role": "dark anodized aluminum for premium compact tool frames and inset rails",
        "heldToolAllowed": True,
        "stationPropAllowed": True,
        "salvageAllowed": False,
        "worldPanelAllowed": False,
        "tilingScale": 4.2,
        "metallic": 0.78,
        "smoothness": 0.34,
        "normalScale": 0.50,
        "heightScale": 0.003,
    },
    {
        "id": "orange_safety_composite",
        "title": "Orange Safety Composite",
        "surfaceClass": "safety_composite_panel",
        "role": "orange-red polymer composite for readable safety accents on tools and survival equipment",
        "heldToolAllowed": True,
        "stationPropAllowed": True,
        "salvageAllowed": False,
        "worldPanelAllowed": False,
        "tilingScale": 3.4,
        "metallic": 0.0,
        "smoothness": 0.30,
        "normalScale": 0.46,
        "heightScale": 0.003,
    },
    {
        "id": "white_ceramic_casing",
        "title": "White Ceramic Casing",
        "surfaceClass": "scientific_ceramic_casing",
        "role": "off-white ceramic or composite casing for scanner and analysis tool science surfaces",
        "heldToolAllowed": True,
        "stationPropAllowed": True,
        "salvageAllowed": False,
        "worldPanelAllowed": False,
        "tilingScale": 3.0,
        "metallic": 0.0,
        "smoothness": 0.42,
        "normalScale": 0.38,
        "heightScale": 0.002,
    },
    {
        "id": "fine_ribbed_trim",
        "title": "Fine Ribbed Trim",
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
        "id": "worn_steel_inset",
        "title": "Worn Steel Inset",
        "surfaceClass": "aged_panel_steel",
        "role": "worn steel inset plate for tool jaws, blade mounts, sampler heads, and mechanical contact zones",
        "heldToolAllowed": True,
        "stationPropAllowed": True,
        "salvageAllowed": True,
        "worldPanelAllowed": False,
        "tilingScale": 3.8,
        "metallic": 0.72,
        "smoothness": 0.24,
        "normalScale": 0.72,
        "heightScale": 0.005,
    },
    {
        "id": "smoky_acrylic_glass",
        "title": "Smoky Acrylic Glass",
        "surfaceClass": "pressure_acrylic_viewport",
        "role": "smoky cyan acrylic or glass for scanner lenses, small displays, and pressure viewport insets",
        "heldToolAllowed": True,
        "stationPropAllowed": True,
        "salvageAllowed": False,
        "worldPanelAllowed": False,
        "tilingScale": 2.8,
        "metallic": 0.0,
        "smoothness": 0.62,
        "normalScale": 0.28,
        "heightScale": 0.001,
    },
    {
        "id": "gray_polymer",
        "title": "Gray Polymer",
        "surfaceClass": "neutral_polymer_shell",
        "role": "neutral gray polymer shell for non-metal tool bodies and equipment casings",
        "heldToolAllowed": True,
        "stationPropAllowed": True,
        "salvageAllowed": False,
        "worldPanelAllowed": False,
        "tilingScale": 3.6,
        "metallic": 0.0,
        "smoothness": 0.32,
        "normalScale": 0.46,
        "heightScale": 0.003,
    },
    {
        "id": "salt_scuffed_metal",
        "title": "Salt Scuffed Metal",
        "surfaceClass": "salt_scuffed_tool_metal",
        "role": "salt-scuffed tool metal for frequently handled exterior plates and repair equipment",
        "heldToolAllowed": True,
        "stationPropAllowed": True,
        "salvageAllowed": True,
        "worldPanelAllowed": False,
        "tilingScale": 3.2,
        "metallic": 0.64,
        "smoothness": 0.22,
        "normalScale": 0.66,
        "heightScale": 0.005,
    },
    {
        "id": "clean_graphite_panel",
        "title": "Clean Graphite Panel",
        "surfaceClass": "clean_graphite_panel",
        "role": "clean graphite-black panel material for advanced tools and subdued high-tech casings",
        "heldToolAllowed": True,
        "stationPropAllowed": True,
        "salvageAllowed": False,
        "worldPanelAllowed": False,
        "tilingScale": 3.8,
        "metallic": 0.18,
        "smoothness": 0.40,
        "normalScale": 0.40,
        "heightScale": 0.002,
    },
    {
        "id": "aged_green_service_metal",
        "title": "Aged Green Service Metal",
        "surfaceClass": "aged_green_service_metal",
        "role": "aged green service metal for analyzer, repair, station service props, and older equipment",
        "heldToolAllowed": True,
        "stationPropAllowed": True,
        "salvageAllowed": True,
        "worldPanelAllowed": True,
        "tilingScale": 2.6,
        "metallic": 0.56,
        "smoothness": 0.22,
        "normalScale": 0.58,
        "heightScale": 0.004,
    },
    {
        "id": "brushed_titanium",
        "title": "Brushed Titanium",
        "surfaceClass": "brushed_titanium",
        "role": "brushed titanium for durable blades, precision mechanisms, and high-pressure tool hardware",
        "heldToolAllowed": True,
        "stationPropAllowed": True,
        "salvageAllowed": False,
        "worldPanelAllowed": False,
        "tilingScale": 4.0,
        "metallic": 0.82,
        "smoothness": 0.38,
        "normalScale": 0.36,
        "heightScale": 0.002,
    },
    {
        "id": "black_gasket_rubber",
        "title": "Black Gasket Rubber",
        "surfaceClass": "black_gasket_rubber",
        "role": "black gasket rubber for sealed joints, O-ring-like strips, and pressure-rated seams",
        "heldToolAllowed": True,
        "stationPropAllowed": True,
        "salvageAllowed": False,
        "worldPanelAllowed": False,
        "tilingScale": 5.5,
        "metallic": 0.0,
        "smoothness": 0.16,
        "normalScale": 0.58,
        "heightScale": 0.003,
    },
    {
        "id": "repaired_salvage_metal",
        "title": "Repaired Salvage Metal",
        "surfaceClass": "repaired_salvage_metal",
        "role": "repaired salvage metal for damaged tools, wreckage props, and visibly patched service equipment",
        "heldToolAllowed": False,
        "stationPropAllowed": True,
        "salvageAllowed": True,
        "worldPanelAllowed": False,
        "tilingScale": 2.4,
        "metallic": 0.48,
        "smoothness": 0.17,
        "normalScale": 0.74,
        "heightScale": 0.006,
    },
    {
        "id": "matte_carbon_composite",
        "title": "Matte Carbon Composite",
        "surfaceClass": "matte_carbon_composite",
        "role": "matte carbon composite for premium compact tool shells and lightweight structural panels",
        "heldToolAllowed": True,
        "stationPropAllowed": True,
        "salvageAllowed": False,
        "worldPanelAllowed": False,
        "tilingScale": 4.5,
        "metallic": 0.0,
        "smoothness": 0.34,
        "normalScale": 0.50,
        "heightScale": 0.003,
    },
)


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def project_path(raw: str | Path) -> Path:
    path = Path(raw)
    return path if path.is_absolute() else ROOT / path


def slug(value: str) -> str:
    result = re.sub(r"[^a-zA-Z0-9_\\-]+", "_", value.strip()).strip("_")
    return result or "GeminiBatch"


def crop_cell(image: Image.Image, column: int, row: int, margin_fraction: float) -> Image.Image:
    cell_w = image.width / 4.0
    cell_h = image.height / 4.0
    margin_x = max(0, int(cell_w * margin_fraction))
    margin_y = max(0, int(cell_h * margin_fraction))
    left = int(column * cell_w) + margin_x
    top = int(row * cell_h) + margin_y
    right = int((column + 1) * cell_w) - margin_x
    bottom = int((row + 1) * cell_h) - margin_y
    return image.crop((left, top, right, bottom))


def save_base(tile: Image.Image, output: Path, max_size: int) -> Image.Image:
    base = tile.convert("RGB")
    side = min(base.width, base.height)
    left = (base.width - side) // 2
    top = (base.height - side) // 2
    base = base.crop((left, top, left + side, top + side))
    if max_size > 0 and base.width > max_size:
        base = base.resize((max_size, max_size), Image.Resampling.LANCZOS)
    output.parent.mkdir(parents=True, exist_ok=True)
    base.save(output, "PNG")
    return base


def height_from_base(base: Image.Image) -> Image.Image:
    gray = base.convert("L").filter(ImageFilter.GaussianBlur(radius=0.9))
    auto = np.asarray(gray, dtype=np.float32)
    low, high = np.percentile(auto, [4, 96])
    if high <= low:
        high = low + 1.0
    normalized = np.clip((auto - low) / (high - low), 0.0, 1.0)
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
    normal = (normal * 0.5 + 0.5) * 255.0
    return Image.fromarray(np.uint8(np.clip(normal, 0.0, 255.0)), "RGB")


def mask_maps(base: Image.Image, spec: dict) -> tuple[Image.Image, Image.Image]:
    height = np.asarray(height_from_base(base), dtype=np.float32) / 255.0
    ao = np.clip(0.72 + height * 0.28, 0.0, 1.0)
    roughness = np.full_like(ao, 1.0 - float(spec["smoothness"]))
    metal = np.full_like(ao, float(spec["metallic"]))
    zero = np.zeros_like(ao)
    smoothness = np.full_like(ao, float(spec["smoothness"]))
    arm = np.stack((ao, roughness, metal), axis=2)
    mask = np.stack((metal, ao, zero, smoothness), axis=2)
    return (
        Image.fromarray(np.uint8(np.clip(arm, 0.0, 1.0) * 255.0), "RGB"),
        Image.fromarray(np.uint8(np.clip(mask, 0.0, 1.0) * 255.0), "RGBA"),
    )


def make_preview(assets: list[dict], output: Path) -> None:
    tile = 180
    label_h = 34
    gap = 12
    columns = 4
    rows = 4
    canvas = Image.new("RGB", (columns * tile + (columns - 1) * gap, rows * (tile + label_h) + (rows - 1) * gap), (8, 12, 14))
    draw = ImageDraw.Draw(canvas)
    for index, asset in enumerate(assets):
        path = project_path(asset["maps"]["BaseColor"])
        with Image.open(path) as image:
            preview = image.convert("RGB").resize((tile, tile), Image.Resampling.LANCZOS)
        x = (index % columns) * (tile + gap)
        y = (index // columns) * (tile + label_h + gap)
        canvas.paste(preview, (x, y))
        draw.rectangle((x, y + tile, x + tile, y + tile + label_h), fill=(5, 9, 11))
        draw.text((x + 6, y + tile + 6), asset["id"][:28], fill=(196, 222, 225))
    output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output, "PNG")


def split(args: argparse.Namespace) -> int:
    source = project_path(args.image).resolve()
    if not source.exists():
        raise FileNotFoundError(display_path(source))

    batch = slug(args.batch)
    output_root = project_path(args.output_root).resolve() / batch
    tiles_root = output_root / "Tiles"
    with Image.open(source) as image:
        atlas = image.convert("RGB")

    manifest_assets: list[dict] = []
    for index, spec in enumerate(TILES):
        row = index // 4
        column = index % 4
        asset_id = f"gemini_{batch}_{spec['id']}"
        tile = crop_cell(atlas, column, row, args.margin_fraction)
        asset_dir = tiles_root / asset_id
        base_path = asset_dir / f"TX_GM_{asset_id}_BaseColor.png"
        base = save_base(tile, base_path, args.max_tile_size)
        height = height_from_base(base)
        normal = normal_from_height(height, float(spec["normalScale"]) * args.normal_strength)
        arm, mask = mask_maps(base, spec)

        normal_path = asset_dir / f"TX_GM_{asset_id}_NormalGL.png"
        arm_path = asset_dir / f"TX_GM_{asset_id}_ARM_AO_Rough_Metal.png"
        height_path = asset_dir / f"TX_GM_{asset_id}_Height.png"
        mask_path = asset_dir / f"TX_GM_{asset_id}_MaskMap_UnityURP.png"
        normal.save(normal_path, "PNG")
        arm.save(arm_path, "PNG")
        height.save(height_path, "PNG")
        mask.save(mask_path, "PNG")

        manifest_assets.append(
            {
                "id": asset_id,
                "title": spec["title"],
                "source": display_path(source),
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
                "watermarkRisk": index == 15,
                "maps": {
                    "BaseColor": display_path(base_path),
                    "NormalGL": display_path(normal_path),
                    "ARM_AO_Rough_Metal": display_path(arm_path),
                    "Height": display_path(height_path),
                    "MaskMap_UnityURP": display_path(mask_path),
                },
            }
        )

    preview = output_root / f"PREVIEW_{batch}_GeminiMaterialAtlas.png"
    make_preview(manifest_assets, preview)
    manifest = {
        "schema": "hecton8.external_pbr_pack.v1",
        "sourceProvider": "GeminiManualAtlas",
        "providerLicensePage": "",
        "license": "USER_GENERATED_REVIEW_REQUIRED",
        "resolution": f"{base.width}px_per_tile",
        "unityImportStatus": "PENDING UNITY IMPORT",
        "reviewStatus": "PENDING VISUAL REVIEW",
        "mapPacking": {
            "sourceARM": "RGB = generated Ambient Occlusion, Roughness, Metal",
            "unityMaskMap": "RGBA = Metal, Ambient Occlusion, unused zero, Smoothness",
        },
        "atlasSource": display_path(source),
        "assets": manifest_assets,
        "preview": display_path(preview),
    }
    manifest_path = output_root / "GeminiMaterialAtlas_Manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")

    print("GEMINI_MATERIAL_ATLAS_SPLIT_STATUS: PASS")
    print(f"source={display_path(source)}")
    print(f"output={display_path(output_root)}")
    print(f"assets={len(manifest_assets)}")
    print(f"preview={display_path(preview)}")
    print(f"manifest={display_path(manifest_path)}")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--image", required=True, help="Path to the generated 4x4 Gemini material atlas image.")
    parser.add_argument("--batch", default="Batch01")
    parser.add_argument("--output-root", type=Path, default=DEFAULT_OUTPUT_ROOT)
    parser.add_argument("--margin-fraction", type=float, default=0.025)
    parser.add_argument("--max-tile-size", type=int, default=2048)
    parser.add_argument("--normal-strength", type=float, default=2.4)
    return split(parser.parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
