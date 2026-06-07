#!/usr/bin/env python3
"""Download selected Poly Haven CC0 PBR textures and repack them for Unity URP."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from urllib.request import Request, urlopen

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PolyHaven"
USER_AGENT = "Hecton8TextureIntake/20260607 (local project asset import)"

ASSETS = {
    "metal_plate_02": {
        "title": "Metal Plate 02",
        "source": "https://polyhaven.com/a/metal_plate_02",
        "role": "worn corroded steel plate for prop body panels and industrial trim",
    },
    "blue_metal_plate": {
        "title": "Blue Metal Plate",
        "source": "https://polyhaven.com/a/blue_metal_plate",
        "role": "scuffed painted steel for pressure-rated tool housings",
    },
    "worn_shutter": {
        "title": "Worn Shutter",
        "source": "https://polyhaven.com/a/worn_shutter",
        "role": "dark corrugated worn metal for ribs, grips, cartridge shells",
    },
    "factory_wall": {
        "title": "Factory Wall",
        "source": "https://polyhaven.com/a/factory_wall",
        "role": "green corrugated factory metal for diegetic equipment panels",
    },
    "rubber_tiles": {
        "title": "Rubber Tiles",
        "source": "https://polyhaven.com/a/rubber_tiles",
        "role": "smooth dark waterproof rubber with seams for gaskets, grips, and equipment feet",
    },
    "corrugated_iron_02": {
        "title": "Corrugated Iron 02",
        "source": "https://polyhaven.com/a/corrugated_iron_02",
        "role": "weathered corrugated iron for industrial pressure hull skins and ribbed panels",
    },
    "rusty_metal_03": {
        "title": "Rusty Metal 03",
        "source": "https://polyhaven.com/a/rusty_metal_03",
        "role": "rich rusted steel for damaged salvage, wreckage, and repair props",
    },
    "box_profile_metal_sheet": {
        "title": "Box Profile Metal Sheet",
        "source": "https://polyhaven.com/a/box_profile_metal_sheet",
        "role": "profiled metal sheet for modular equipment casings and trim strips",
    },
    "metal_plate": {
        "title": "Metal Plate",
        "source": "https://polyhaven.com/a/metal_plate",
        "role": "painted diamond-plate steel for deck plates, tool insets, and worn industrial trims",
    },
    "corrugated_iron": {
        "title": "Corrugated Iron",
        "source": "https://polyhaven.com/a/corrugated_iron",
        "role": "broad weathered galvanized steel ridges for hull panels and station service cladding",
    },
    "rusty_corrugated_iron": {
        "title": "Rusty Corrugated Iron",
        "source": "https://polyhaven.com/a/rusty_corrugated_iron",
        "role": "oxidized corrugated sheet for wreckage, salvage scrap, and damaged external casings",
    },
    "corrugated_iron_03": {
        "title": "Corrugated Iron 03",
        "source": "https://polyhaven.com/a/corrugated_iron_03",
        "role": "narrow galvanized corrugation for compact equipment ribs and small panel strips",
    },
    "green_metal_rust": {
        "title": "Green Metal Rust",
        "source": "https://polyhaven.com/a/green_metal_rust",
        "role": "old painted green industrial metal for aged bulkheads and repaired service modules",
    },
    "painted_metal_shutter": {
        "title": "Painted Metal Shutter",
        "source": "https://polyhaven.com/a/painted_metal_shutter",
        "role": "painted shutter slats for tool ribs, locker fronts, and modular equipment housings",
    },
    "container_side": {
        "title": "Container Side",
        "source": "https://polyhaven.com/a/container_side",
        "role": "shipping-container steel side for larger equipment casings and service bay props",
    },
    "metal_grate_rusty": {
        "title": "Metal Grate Rusty",
        "source": "https://polyhaven.com/a/metal_grate_rusty",
        "role": "rusted open grate surface for drains, vents, and salvage deck details",
    },
}

MAP_SUFFIXES = {
    "diff": "BaseColor",
    "nor_gl": "NormalGL",
    "arm": "ARM_AO_Rough_Metal",
    "disp": "Height",
}


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def source_url(asset_id: str, map_suffix: str, resolution: str) -> str:
    return (
        f"https://dl.polyhaven.org/file/ph-assets/Textures/jpg/{resolution}/"
        f"{asset_id}/{asset_id}_{map_suffix}_{resolution}.jpg"
    )


def download(url: str, target: Path) -> None:
    if target.exists() and target.stat().st_size > 0:
        return

    target.parent.mkdir(parents=True, exist_ok=True)
    request = Request(url, headers={"User-Agent": USER_AGENT})
    with urlopen(request, timeout=120) as response:
        target.write_bytes(response.read())


def repack_mask_map(arm_path: Path, output: Path) -> None:
    with Image.open(arm_path) as image:
        arm = image.convert("RGB")
    ao, roughness, metal = arm.split()
    zero = Image.new("L", arm.size, 0)
    smoothness = roughness.point(lambda value: 255 - value)
    mask = Image.merge("RGBA", (metal, ao, zero, smoothness))
    output.parent.mkdir(parents=True, exist_ok=True)
    mask.save(output, "PNG")


def make_preview(asset_dirs: list[Path], output: Path) -> None:
    thumb = 240
    gap = 12
    columns = 4
    rows = len(asset_dirs)
    canvas = Image.new("RGB", (columns * thumb + (columns - 1) * gap, rows * thumb + (rows - 1) * gap), (8, 12, 14))
    names = ("BaseColor", "NormalGL", "MaskMap", "Height")
    for row, asset_dir in enumerate(asset_dirs):
        for column, name in enumerate(names):
            candidates = sorted(asset_dir.glob(f"*_{name}*"))
            if not candidates:
                continue
            with Image.open(candidates[0]) as image:
                preview = image.convert("RGB")
            preview.thumbnail((thumb, thumb), Image.Resampling.LANCZOS)
            x = column * (thumb + gap) + (thumb - preview.width) // 2
            y = row * (thumb + gap) + (thumb - preview.height) // 2
            canvas.paste(preview, (x, y))
    output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output, "PNG")


def run(args: argparse.Namespace) -> int:
    resolution = args.resolution
    output_root = (ROOT / args.output).resolve() if not args.output.is_absolute() else args.output.resolve()
    manifest_assets = []
    asset_dirs = []

    for asset_id, meta in ASSETS.items():
        asset_dir = output_root / asset_id
        asset_dirs.append(asset_dir)
        maps = {}
        for suffix, label in MAP_SUFFIXES.items():
            url = source_url(asset_id, suffix, resolution)
            target = asset_dir / f"TX_PH_{asset_id}_{label}_{resolution}.jpg"
            download(url, target)
            maps[label] = display_path(target)

        mask = asset_dir / f"TX_PH_{asset_id}_MaskMap_UnityURP_{resolution}.png"
        repack_mask_map(asset_dir / f"TX_PH_{asset_id}_ARM_AO_Rough_Metal_{resolution}.jpg", mask)
        maps["MaskMap_UnityURP"] = display_path(mask)
        manifest_assets.append(
            {
                "id": asset_id,
                "title": meta["title"],
                "source": meta["source"],
                "license": "CC0",
                "role": meta["role"],
                "maps": maps,
            }
        )

    preview = output_root / f"PREVIEW_PolyHavenExternalPBR_{resolution}.png"
    make_preview(asset_dirs, preview)
    manifest = {
        "schema": "hecton8.external_pbr_pack.v1",
        "sourceProvider": "Poly Haven",
        "providerLicensePage": "https://polyhaven.com/license",
        "license": "CC0",
        "resolution": resolution,
        "unityImportStatus": "PENDING UNITY IMPORT",
        "mapPacking": {
            "sourceARM": "RGB = Ambient Occlusion, Roughness, Metal",
            "unityMaskMap": "RGBA = Metal, Ambient Occlusion, unused zero, Smoothness",
        },
        "assets": manifest_assets,
        "preview": display_path(preview),
    }
    manifest_path = output_root / "PolyHavenExternalPBR_Manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")

    print("POLYHAVEN_EXTERNAL_PBR_PACK_STATUS: PASS")
    print(f"output={display_path(output_root)}")
    print(f"assets={len(manifest_assets)}")
    print(f"resolution={resolution}")
    print(f"preview={display_path(preview)}")
    print(f"manifest={display_path(manifest_path)}")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--resolution", default="2k", choices=("1k", "2k", "4k"))
    parser.add_argument("--output", type=Path, default=OUTPUT)
    return run(parser.parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
