#!/usr/bin/env python3
"""Build a compact visual preview from downloaded external PBR basecolor maps."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
MANIFESTS = (
    ROOT / "Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PolyHaven/PolyHavenExternalPBR_Manifest.json",
)


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def project_path(raw: str) -> Path:
    path = Path(raw)
    return path if path.is_absolute() else ROOT / path


def collect_assets() -> list[dict]:
    result: list[dict] = []
    for manifest_path in MANIFESTS:
        if not manifest_path.exists():
            continue
        payload = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
        provider = str(payload.get("sourceProvider", manifest_path.parent.name))
        for asset in payload.get("assets", []) or []:
            maps = asset.get("maps", {}) or {}
            base = maps.get("BaseColor", "")
            if base:
                result.append(
                    {
                        "provider": provider,
                        "id": asset.get("id", ""),
                        "title": asset.get("title", asset.get("id", "")),
                        "role": asset.get("role", ""),
                        "source": asset.get("source", ""),
                        "baseColor": base,
                    }
                )
    return result


def crop_square(image: Image.Image) -> Image.Image:
    side = min(image.width, image.height)
    x = (image.width - side) // 2
    y = (image.height - side) // 2
    return image.crop((x, y, x + side, y + side))


def render(args: argparse.Namespace) -> int:
    assets = collect_assets()
    if not assets:
        raise RuntimeError("No external PBR assets found.")

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
        with Image.open(project_path(asset["baseColor"])) as image:
            preview = crop_square(image.convert("RGB")).resize((tile, tile), Image.Resampling.LANCZOS)
        canvas.paste(preview, (x, y))
        draw.rectangle((x, y + tile, x + tile, y + tile + label_h), fill=(5, 9, 11))
        text = f"{asset['provider']} / {asset['id']}"
        draw.text((x + 6, y + tile + 6), text[:36], fill=(190, 214, 218))

    output = project_path(args.output).resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output, "PNG")
    print("EXTERNAL_PBR_BEAUTY_PREVIEW_STATUS: PASS")
    print(f"assets={len(assets)}")
    print(f"preview={display_path(output)}")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--output",
        default="Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PREVIEW_ExternalPBR_BaseColorBeauty.png",
    )
    parser.add_argument("--tile-size", type=int, default=220)
    parser.add_argument("--columns", type=int, default=4)
    return render(parser.parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
