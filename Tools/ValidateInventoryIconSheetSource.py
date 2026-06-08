#!/usr/bin/env python3
"""Validate a fresh Gemini inventory-icon source sheet before offline baking."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import re

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
ALLOWED_EXTENSIONS = {".png", ".jpg", ".jpeg", ".webp"}
SHEET_HINTS = ("icon", "inventory", "tool", "sheet", "batch", "atlas", "grid")
STRONG_SHEET_HINTS = ("icon", "inventory", "sheet", "batch")
MATERIAL_HINTS = (
    "material",
    "texture",
    "panel",
    "metal",
    "basalt",
    "coral",
    "kelp",
    "barnacle",
    "foamless",
    "rock",
    "glass",
    "rubber",
    "composite",
    "ceramic",
    "casing",
    "biofilm",
    "jelly",
    "membrane",
    "hide",
    "crust",
    "frond",
)


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def positive_int(raw: str) -> int:
    value = int(raw)
    if value <= 0:
        raise argparse.ArgumentTypeError("must be positive")
    return value


def project_or_absolute_path(raw_path: str) -> Path:
    path = Path(raw_path)
    return path if path.is_absolute() else ROOT / path


def normalized_name(path: Path) -> str:
    return re.sub(r"[^a-z0-9]+", " ", path.stem.lower()).strip()


def load_spec_count(spec_path: Path | None) -> int:
    if spec_path is None:
        return 0
    payload = json.loads(spec_path.read_text(encoding="utf-8-sig"))
    items = payload.get("items", []) if isinstance(payload, dict) else []
    return len(items) if isinstance(items, list) else 0


def validate_source(args: argparse.Namespace) -> tuple[list[str], list[str], dict[str, object]]:
    errors: list[str] = []
    warnings: list[str] = []
    source = project_or_absolute_path(args.source).resolve()
    spec_path = project_or_absolute_path(args.spec_json).resolve() if args.spec_json else None

    if not source.exists():
        errors.append(f"source image missing: {display_path(source)}")
        return errors, warnings, {"source": display_path(source)}

    extension = source.suffix.lower()
    if extension not in ALLOWED_EXTENSIONS:
        errors.append(f"unsupported source extension: {extension}")

    size_bytes = source.stat().st_size
    if size_bytes > args.max_bytes:
        warnings.append(f"large source image bytes={size_bytes} maxBytes={args.max_bytes}; bake may be slow")

    name = normalized_name(source)
    has_sheet_hint = any(hint in name for hint in SHEET_HINTS)
    has_strong_sheet_hint = any(hint in name for hint in STRONG_SHEET_HINTS)
    has_material_hint = any(hint in name for hint in MATERIAL_HINTS)
    if has_material_hint and not has_strong_sheet_hint and not args.allow_suspicious_name:
        errors.append(
            "source filename looks like a material/texture, not an inventory icon sheet: "
            f"{source.name}; pass -AllowSuspiciousSource only after visual inspection"
        )
    elif not has_sheet_hint:
        warnings.append(f"source filename has no inventory-icon sheet hint: {source.name}")

    with Image.open(source) as opened:
        width, height = opened.size

    if args.grid_rows * args.grid_columns <= 0:
        errors.append("grid rows/columns must be positive")
    else:
        cell_width = width / float(args.grid_columns)
        cell_height = height / float(args.grid_rows)
        if cell_width < args.min_cell_px or cell_height < args.min_cell_px:
            errors.append(
                f"grid cell too small for clean alpha bake: cell={cell_width:.1f}x{cell_height:.1f} min={args.min_cell_px}"
            )

    spec_count = load_spec_count(spec_path)
    grid_count = args.grid_rows * args.grid_columns
    if spec_count and spec_count != grid_count:
        errors.append(f"spec/grid count mismatch: specItems={spec_count} gridCells={grid_count}")

    if abs((width / max(1.0, float(height))) - args.expected_aspect) > args.aspect_tolerance:
        warnings.append(
            f"source aspect differs from expected sheet aspect: actual={width}:{height} expectedRatio={args.expected_aspect:.3f}"
        )

    meta: dict[str, object] = {
        "source": display_path(source),
        "width": width,
        "height": height,
        "bytes": size_bytes,
        "gridRows": args.grid_rows,
        "gridColumns": args.grid_columns,
        "specItems": spec_count,
    }
    return errors, warnings, meta


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True)
    parser.add_argument("--spec-json", default="")
    parser.add_argument("--grid-rows", type=positive_int, default=3)
    parser.add_argument("--grid-columns", type=positive_int, default=4)
    parser.add_argument("--min-cell-px", type=positive_int, default=256)
    parser.add_argument("--max-bytes", type=positive_int, default=26_214_400)
    parser.add_argument("--expected-aspect", type=float, default=1.0)
    parser.add_argument("--aspect-tolerance", type=float, default=0.85)
    parser.add_argument("--allow-suspicious-name", action="store_true")
    args = parser.parse_args()

    errors, warnings, meta = validate_source(args)
    print("INVENTORY_ICON_SHEET_SOURCE_VALIDATOR")
    for key, value in meta.items():
        print(f"{key}={value}")
    print(f"errors={len(errors)}")
    print(f"warnings={len(warnings)}")
    for error in errors:
        print(f"ERROR {error}")
    for warning in warnings:
        print(f"WARN {warning}")
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
