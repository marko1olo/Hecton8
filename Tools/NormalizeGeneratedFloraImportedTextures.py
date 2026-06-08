#!/usr/bin/env python3
"""Normalize oversized first-party generated flora imported texture stacks."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
FLORA_IMPORTED_ROOT = ROOT / "Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported"
REPORT_PATH = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeneratedFloraImportedNormalization_20260607.json"
PREVIEW_PATH = ROOT / "Temp/GeneratedFloraImportedNormalization_20260607.png"

DEFAULT_FAMILIES = ("family.kelp.abyssal",)
MAP_TOKENS = ("albedo", "detail", "normal", "mask")


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def normalize_image(path: Path, max_size: int) -> dict:
    before_bytes = path.stat().st_size
    with Image.open(path) as source:
        before_size = source.size
        mode = "RGBA" if source.mode == "RGBA" else "RGB"
        image = source.convert(mode)
        if max(image.size) > max_size:
            image.thumbnail((max_size, max_size), Image.Resampling.LANCZOS)
        if path.name.startswith(("albedo___", "detail___")):
            image = image.filter(ImageFilter.UnsharpMask(radius=0.65, percent=55, threshold=2))
        path.parent.mkdir(parents=True, exist_ok=True)
        image.save(path, "PNG", optimize=True, compress_level=9)

    with Image.open(path) as result:
        after_size = result.size
        after_mode = result.mode

    after_bytes = path.stat().st_size
    return {
        "path": display_path(path),
        "beforeBytes": before_bytes,
        "afterBytes": after_bytes,
        "deltaBytes": after_bytes - before_bytes,
        "beforeSize": list(before_size),
        "afterSize": list(after_size),
        "mode": after_mode,
    }


def write_preview(records: list[dict]) -> None:
    if not records:
        return

    thumb = 220
    label_h = 34
    gap = 10
    cols = min(4, len(records))
    rows = (len(records) + cols - 1) // cols
    canvas = Image.new("RGB", (cols * thumb + (cols - 1) * gap, rows * (thumb + label_h) + (rows - 1) * gap), (8, 10, 12))
    draw = ImageDraw.Draw(canvas)

    for index, record in enumerate(records):
        path = ROOT / record["path"]
        with Image.open(path) as image:
            preview = image.convert("RGB").resize((thumb, thumb), Image.Resampling.LANCZOS)
        x = (index % cols) * (thumb + gap)
        y = (index // cols) * (thumb + label_h + gap)
        canvas.paste(preview, (x, y))
        draw.rectangle((x, y + thumb, x + thumb, y + thumb + label_h), fill=(5, 7, 9))
        draw.text((x + 5, y + thumb + 8), Path(record["path"]).name[:32], fill=(220, 230, 230))

    PREVIEW_PATH.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(PREVIEW_PATH)


def normalize(args: argparse.Namespace) -> int:
    records: list[dict] = []
    missing: list[str] = []
    families = tuple(args.family) if args.family else DEFAULT_FAMILIES

    for family_id in families:
        family_dir = FLORA_IMPORTED_ROOT / family_id
        for token in MAP_TOKENS:
            path = family_dir / f"{token}___{family_id}.png"
            if not path.exists():
                missing.append(display_path(path))
                continue
            record = normalize_image(path, args.max_size)
            record.update({"familyId": family_id, "mapToken": token})
            records.append(record)

    report = {
        "schema": "hecton8.generated_flora_imported_normalization.v1",
        "date": "2026-06-07",
        "operation": "resize_and_losslessly_png_optimize_existing_generated_flora_same_paths",
        "maxSize": args.max_size,
        "families": list(families),
        "records": records,
        "missing": missing,
        "totalBeforeBytes": sum(record["beforeBytes"] for record in records),
        "totalAfterBytes": sum(record["afterBytes"] for record in records),
    }
    report["totalSavedBytes"] = report["totalBeforeBytes"] - report["totalAfterBytes"]

    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    REPORT_PATH.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    write_preview(records)

    status = "PASS" if not missing else "PARTIAL"
    print(f"GENERATED_FLORA_IMPORTED_NORMALIZATION_STATUS: {status}")
    print(f"families={len(families)}")
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
    parser.add_argument("--family", action="append", help="Flora family id to normalize; repeatable.")
    parser.add_argument("--max-size", type=int, default=1024)
    return normalize(parser.parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
