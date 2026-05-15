#!/usr/bin/env python3
"""Offline icon baker for HECTON-8 UI.

Generates 32, 128, and 512 pixel variants with deterministic transparent-border
trim, square centering, and small-icon alpha snapping.
"""

from __future__ import annotations

import argparse
import json
import tempfile
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw


SCRIPT_PATH = Path(__file__).resolve()
ROOT = SCRIPT_PATH.parents[1]
DEFAULT_SIZES = (32, 128, 512)
IMAGE_EXTENSIONS = {".png", ".tga", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp"}


@dataclass(frozen=True)
class BakeResult:
    source: str
    outputs: tuple[str, ...]


def display_path(path: Path) -> str:
    try:
        return str(path.relative_to(ROOT)).replace("\\", "/")
    except ValueError:
        return str(path)


def trim_to_content(image: Image.Image, alpha_threshold: int) -> Image.Image:
    rgba = image.convert("RGBA")
    alpha = rgba.getchannel("A")
    if alpha_threshold > 0:
        alpha = alpha.point(lambda value: 255 if value >= alpha_threshold else 0)
    bbox = alpha.getbbox()
    if bbox is None:
        return rgba
    return rgba.crop(bbox)


def center_square(image: Image.Image, padding_ratio: float) -> Image.Image:
    width, height = image.size
    side = max(width, height)
    padded_side = max(1, int(round(side * (1.0 + max(0.0, padding_ratio) * 2.0))))
    square = Image.new("RGBA", (padded_side, padded_side), (0, 0, 0, 0))
    offset = ((padded_side - width) // 2, (padded_side - height) // 2)
    square.alpha_composite(image, offset)
    return square


def snap_alpha(image: Image.Image, alpha_threshold: int) -> Image.Image:
    rgba = image.convert("RGBA")
    red, green, blue, alpha = rgba.split()
    snapped_alpha = alpha.point(lambda value: 255 if value >= alpha_threshold else 0)
    return Image.merge("RGBA", (red, green, blue, snapped_alpha))


def bake_icon(source: Path, output_dir: Path, sizes: tuple[int, ...], padding_ratio: float, alpha_threshold: int) -> BakeResult:
    with Image.open(source) as image:
        prepared = center_square(trim_to_content(image, alpha_threshold), padding_ratio)
        outputs: list[str] = []
        for size in sizes:
            resample = Image.Resampling.LANCZOS if size > 32 else Image.Resampling.BICUBIC
            resized = prepared.resize((size, size), resample=resample)
            if size <= 32:
                resized = snap_alpha(resized, alpha_threshold)
            output_path = output_dir / f"{source.stem}_{size}.png"
            output_path.parent.mkdir(parents=True, exist_ok=True)
            resized.save(output_path, "PNG", optimize=True)
            outputs.append(display_path(output_path))
        return BakeResult(display_path(source), tuple(outputs))


def iter_icon_sources(source: Path) -> list[Path]:
    if source.is_file():
        return [source] if source.suffix.lower() in IMAGE_EXTENSIONS else []
    if not source.exists():
        return []
    return sorted(
        path
        for path in source.rglob("*")
        if path.is_file() and path.suffix.lower() in IMAGE_EXTENSIONS and ".meta" not in path.name
    )


def write_manifest(results: list[BakeResult], path: Path) -> None:
    payload = {
        "schema": "hecton8.icon_bake_manifest.v1",
        "tool": "Tools/IconBaker.py",
        "iconCount": len(results),
        "icons": [
            {
                "source": result.source,
                "outputs": list(result.outputs),
            }
            for result in results
        ],
    }
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def create_self_test_icon(path: Path) -> None:
    image = Image.new("RGBA", (96, 64), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle((12, 8, 84, 56), radius=4, fill=(0, 255, 210, 255))
    draw.rectangle((24, 24, 72, 40), fill=(0, 12, 10, 255))
    draw.rectangle((36, 16, 60, 48), fill=(255, 176, 46, 255))
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path, "PNG")


def run(args: argparse.Namespace) -> int:
    sizes = tuple(sorted(set(max(1, int(size)) for size in args.sizes)))
    source = Path(args.source).resolve()
    output = Path(args.output).resolve()
    manifest = Path(args.manifest).resolve() if args.manifest else output / "IconBakeManifest.json"

    if args.self_test:
        with tempfile.TemporaryDirectory() as temp_dir:
            source = Path(temp_dir) / "h8_self_test_icon.png"
            create_self_test_icon(source)
            results = [bake_icon(source, output, sizes, args.padding_ratio, args.alpha_threshold)]
    else:
        results = [
            bake_icon(path.resolve(), output, sizes, args.padding_ratio, args.alpha_threshold)
            for path in iter_icon_sources(source)
        ]

    if not results:
        print(f"ICON_BAKER_STATUS: FAIL no icons found under {source}")
        return 1

    write_manifest(results, manifest)
    print("ICON_BAKER_STATUS: PASS")
    print(f"icons={len(results)}")
    print(f"sizes={','.join(str(size) for size in sizes)}")
    print(f"manifest={manifest}")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description="Bake HECTON-8 UI icons into fixed pixel sizes.")
    parser.add_argument("--source", default="Assets/_Project/Art/Icons", help="Source icon file or directory.")
    parser.add_argument("--output", default="Assets/_Project/Art/Icons/Baked", help="Output directory.")
    parser.add_argument("--manifest", default="", help="Optional manifest JSON path.")
    parser.add_argument("--sizes", nargs="+", type=int, default=list(DEFAULT_SIZES), help="Output sizes.")
    parser.add_argument("--padding-ratio", type=float, default=0.08, help="Transparent padding ratio around trimmed content.")
    parser.add_argument("--alpha-threshold", type=int, default=8, help="Alpha threshold for trim and 32px snapping.")
    parser.add_argument("--self-test", action="store_true", help="Bake a generated icon instead of project sources.")
    return run(parser.parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
