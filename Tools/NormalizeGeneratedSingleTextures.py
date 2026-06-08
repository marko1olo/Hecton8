#!/usr/bin/env python3
"""Resize selected first-party generated single texture atlases in place."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
REPORT_PATH = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeneratedSingleTextureNormalization_20260607.json"
PREVIEW_PATH = ROOT / "Temp/GeneratedSingleTextureNormalization_20260607.png"

DEFAULT_TARGETS = (
    ROOT / "Assets/_Project/Art/TEXTURES/Meshy_AI_Alien_barnacles_clust_0301230506_texture.png",
)


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def normalize(path: Path, max_size: int) -> dict:
    before_bytes = path.stat().st_size
    with Image.open(path) as source:
        before_size = source.size
        image = source.convert("RGB")
        if max(image.size) > max_size:
            image.thumbnail((max_size, max_size), Image.Resampling.LANCZOS)
            image = image.filter(ImageFilter.UnsharpMask(radius=0.55, percent=45, threshold=2))
        image.save(path, "PNG", optimize=True, compress_level=9)

    with Image.open(path) as result:
        after_size = result.size
        after_mode = result.mode

    after_bytes = path.stat().st_size
    return {
        "path": display_path(path),
        "beforeBytes": before_bytes,
        "afterBytes": after_bytes,
        "savedBytes": before_bytes - after_bytes,
        "beforeSize": list(before_size),
        "afterSize": list(after_size),
        "mode": after_mode,
    }


def write_preview(records: list[dict]) -> None:
    if not records:
        return

    thumb = 360
    label_h = 34
    gap = 12
    canvas = Image.new("RGB", (len(records) * thumb + (len(records) - 1) * gap, thumb + label_h), (8, 10, 12))
    draw = ImageDraw.Draw(canvas)

    for index, record in enumerate(records):
        path = ROOT / record["path"]
        with Image.open(path) as image:
            preview = image.convert("RGB").resize((thumb, thumb), Image.Resampling.LANCZOS)
        x = index * (thumb + gap)
        canvas.paste(preview, (x, 0))
        draw.rectangle((x, thumb, x + thumb, thumb + label_h), fill=(5, 7, 9))
        draw.text((x + 5, thumb + 8), Path(record["path"]).name[:48], fill=(220, 230, 230))

    PREVIEW_PATH.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(PREVIEW_PATH)


def run(args: argparse.Namespace) -> int:
    targets = [Path(raw) for raw in args.target] if args.target else list(DEFAULT_TARGETS)
    records: list[dict] = []
    missing: list[str] = []

    for raw_target in targets:
        path = raw_target if raw_target.is_absolute() else ROOT / raw_target
        if not path.exists():
            missing.append(display_path(path))
            continue
        records.append(normalize(path, args.max_size))

    report = {
        "schema": "hecton8.generated_single_texture_normalization.v1",
        "date": "2026-06-07",
        "operation": "resize_and_png_optimize_same_path_generated_uv_atlas",
        "maxSize": args.max_size,
        "records": records,
        "missing": missing,
        "totalBeforeBytes": sum(record["beforeBytes"] for record in records),
        "totalAfterBytes": sum(record["afterBytes"] for record in records),
        "totalSavedBytes": sum(record["savedBytes"] for record in records),
    }
    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    REPORT_PATH.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    write_preview(records)

    status = "PASS" if not missing else "PARTIAL"
    print(f"GENERATED_SINGLE_TEXTURE_NORMALIZATION_STATUS: {status}")
    print(f"textures_written={len(records)}")
    print(f"before_mb={report['totalBeforeBytes'] / 1024 / 1024:.2f}")
    print(f"after_mb={report['totalAfterBytes'] / 1024 / 1024:.2f}")
    print(f"saved_mb={report['totalSavedBytes'] / 1024 / 1024:.2f}")
    print(f"report={display_path(REPORT_PATH)}")
    print(f"preview={display_path(PREVIEW_PATH)}")
    if missing:
        print("missing=" + json.dumps(missing))
        return 2
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--target", action="append", help="Texture path to resize in place; repeatable.")
    parser.add_argument("--max-size", type=int, default=1024)
    return run(parser.parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
