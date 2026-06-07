#!/usr/bin/env python3
"""Render a dark inventory-slot preview from an inventory icon binding map."""

from __future__ import annotations

import argparse
import json
import math
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def resolve_project_path(path: str) -> Path:
    raw = Path(path)
    if raw.is_absolute():
        return raw

    return ROOT / raw


def parse_hex_color(raw: str) -> tuple[int, int, int, int]:
    value = raw.strip().lstrip("#")
    if len(value) == 6:
        value += "ff"
    if len(value) != 8:
        raise argparse.ArgumentTypeError("color must be RRGGBB or RRGGBBAA")

    return tuple(int(value[index : index + 2], 16) for index in range(0, 8, 2))  # type: ignore[return-value]


def fit_icon(icon: Image.Image, max_size: int) -> Image.Image:
    rgba = icon.convert("RGBA")
    rgba.thumbnail((max_size, max_size), Image.Resampling.LANCZOS)
    return rgba


def load_bindings(path: Path, include_disabled: bool) -> list[dict]:
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
    bindings = payload.get("bindings", []) or []
    result: list[dict] = []
    for binding in bindings:
        enabled = bool(binding.get("enabled", False))
        if not enabled and not include_disabled:
            continue
        if not binding.get("spriteAsset"):
            continue
        result.append(binding)

    return result


def render(args: argparse.Namespace) -> int:
    map_path = resolve_project_path(args.map).resolve()
    output = resolve_project_path(args.output).resolve()
    bindings = load_bindings(map_path, args.include_disabled)
    if not bindings:
        print("INVENTORY_BINDING_MAP_PREVIEW_STATUS: FAIL no previewable bindings")
        return 1

    columns = args.columns if args.columns > 0 else min(5, len(bindings))
    rows = int(math.ceil(len(bindings) / float(columns)))
    slot = args.slot_size
    gap = args.gap
    width = columns * slot + (columns - 1) * gap
    height = rows * slot + (rows - 1) * gap
    canvas = Image.new("RGBA", (width, height), args.page_background)
    draw = ImageDraw.Draw(canvas)

    for index, binding in enumerate(bindings):
        x = (index % columns) * (slot + gap)
        y = (index // columns) * (slot + gap)
        draw.rounded_rectangle(
            (x, y, x + slot - 1, y + slot - 1),
            radius=args.corner_radius,
            fill=args.slot_background,
            outline=args.slot_outline,
            width=args.outline_width,
        )
        sprite_path = resolve_project_path(str(binding["spriteAsset"]))
        if not sprite_path.exists():
            raise FileNotFoundError(sprite_path)

        with Image.open(sprite_path) as source_icon:
            icon = fit_icon(source_icon, slot - args.icon_padding * 2)
        icon_x = x + (slot - icon.width) // 2
        icon_y = y + (slot - icon.height) // 2
        canvas.alpha_composite(icon, (icon_x, icon_y))

    output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output, "PNG", optimize=True)
    print("INVENTORY_BINDING_MAP_PREVIEW_STATUS: PASS")
    print(f"bindings={len(bindings)}")
    print(f"preview={display_path(output)}")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--map", required=True, help="InventoryIconCandidateBindingMap.json path.")
    parser.add_argument("--output", required=True, help="Preview PNG path.")
    parser.add_argument("--include-disabled", action="store_true")
    parser.add_argument("--slot-size", type=int, default=128)
    parser.add_argument("--gap", type=int, default=16)
    parser.add_argument("--columns", type=int, default=0)
    parser.add_argument("--icon-padding", type=int, default=12)
    parser.add_argument("--corner-radius", type=int, default=4)
    parser.add_argument("--outline-width", type=int, default=1)
    parser.add_argument("--page-background", type=parse_hex_color, default=parse_hex_color("00000000"))
    parser.add_argument("--slot-background", type=parse_hex_color, default=parse_hex_color("061417ff"))
    parser.add_argument("--slot-outline", type=parse_hex_color, default=parse_hex_color("1c8093ff"))
    return render(parser.parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
