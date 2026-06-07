#!/usr/bin/env python3
"""Render a simple lit contact sheet for the curated external PBR pack."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PolyHaven/PolyHavenExternalPBR_Manifest.json"
DEFAULT_OUTPUT = ROOT / "Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PREVIEW_ExternalPBR_LitMaterialSheet.png"


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def project_path(raw: str) -> Path:
    path = Path(raw)
    return path if path.is_absolute() else ROOT / path


def crop_square(image: Image.Image) -> Image.Image:
    side = min(image.width, image.height)
    left = (image.width - side) // 2
    top = (image.height - side) // 2
    return image.crop((left, top, left + side, top + side))


def load_rgb(path: Path, size: int) -> np.ndarray:
    with Image.open(path) as image:
        cropped = crop_square(image.convert("RGB")).resize((size, size), Image.Resampling.LANCZOS)
    return np.asarray(cropped, dtype=np.float32) / 255.0


def render_material(asset: dict, tile_size: int) -> Image.Image:
    maps = asset["maps"]
    base = load_rgb(project_path(maps["BaseColor"]), tile_size)
    normal = load_rgb(project_path(maps["NormalGL"]), tile_size) * 2.0 - 1.0
    arm = load_rgb(project_path(maps["ARM_AO_Rough_Metal"]), tile_size)

    normal_len = np.maximum(np.linalg.norm(normal, axis=2, keepdims=True), 0.0001)
    normal = normal / normal_len

    ao = arm[:, :, 0:1]
    roughness = arm[:, :, 1:2]
    metal = arm[:, :, 2:3]

    light = np.array([-0.42, -0.34, 0.84], dtype=np.float32)
    light = light / np.linalg.norm(light)
    view = np.array([0.0, 0.0, 1.0], dtype=np.float32)
    half_vec = light + view
    half_vec = half_vec / np.linalg.norm(half_vec)

    ndotl = np.clip(np.sum(normal * light, axis=2, keepdims=True), 0.0, 1.0)
    ndoth = np.clip(np.sum(normal * half_vec, axis=2, keepdims=True), 0.0, 1.0)

    diffuse = base * (0.22 + ndotl * 0.88) * (0.42 + ao * 0.58)
    spec_power = np.clip(96.0 * (1.0 - roughness) + 8.0, 8.0, 96.0)
    specular = np.power(ndoth, spec_power) * (0.04 + metal * 0.34) * (1.0 - roughness * 0.55)
    color = np.clip(diffuse + specular, 0.0, 1.0)

    return Image.fromarray(np.uint8(color * 255.0), "RGB")


def render(args: argparse.Namespace) -> int:
    if not MANIFEST.exists():
        raise FileNotFoundError(display_path(MANIFEST))

    manifest = json.loads(MANIFEST.read_text(encoding="utf-8-sig"))
    assets = manifest.get("assets", []) or []
    if not assets:
        raise RuntimeError("Manifest has no assets.")

    columns = args.columns
    tile = args.tile_size
    label_h = 40
    gap = 14
    rows = (len(assets) + columns - 1) // columns
    width = columns * tile + (columns - 1) * gap
    height = rows * (tile + label_h) + (rows - 1) * gap
    canvas = Image.new("RGB", (width, height), (8, 12, 14))
    draw = ImageDraw.Draw(canvas)

    for index, asset in enumerate(assets):
        x = (index % columns) * (tile + gap)
        y = (index // columns) * (tile + label_h + gap)
        preview = render_material(asset, tile)
        canvas.paste(preview, (x, y))
        draw.rectangle((x, y + tile, x + tile, y + tile + label_h), fill=(5, 9, 11))
        draw.text((x + 6, y + tile + 6), f"Poly Haven / {asset['id']}"[:38], fill=(196, 222, 225))

    output = project_path(str(args.output)).resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output, "PNG")
    print("EXTERNAL_PBR_LIT_PREVIEW_STATUS: PASS")
    print(f"assets={len(assets)}")
    print(f"preview={display_path(output)}")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--tile-size", type=int, default=220)
    parser.add_argument("--columns", type=int, default=4)
    return render(parser.parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
