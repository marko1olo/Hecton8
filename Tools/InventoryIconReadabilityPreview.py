#!/usr/bin/env python3
"""Render multi-scale inventory icon readability previews from a binding map."""

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


def project_path(path: str) -> Path:
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


def parse_sizes(raw: str) -> list[int]:
    sizes: list[int] = []
    for part in raw.split(","):
        value = part.strip()
        if not value:
            continue

        size = int(value)
        if size < 16:
            raise argparse.ArgumentTypeError("preview sizes must be at least 16 px")
        sizes.append(size)

    if not sizes:
        raise argparse.ArgumentTypeError("at least one preview size is required")
    return sizes


def load_bindings(path: Path, include_disabled: bool) -> list[dict]:
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
    bindings = payload.get("bindings", []) or []
    selected: list[dict] = []
    for binding in bindings:
        enabled = bool(binding.get("enabled", False))
        if not enabled and not include_disabled:
            continue
        if not binding.get("spriteAsset"):
            continue

        selected.append(binding)

    return selected


def binding_is_approved(binding: dict) -> bool:
    approved = bool(binding.get("approved", False)) or str(binding.get("reviewStatus", "")).strip().upper() == "APPROVED"
    has_metadata = (
        bool(str(binding.get("reviewedBy", "")).strip())
        and bool(str(binding.get("reviewedAt", "")).strip())
        and bool(str(binding.get("reviewNote", "")).strip())
    )
    return approved and has_metadata


def fit_icon(icon: Image.Image, max_size: int) -> Image.Image:
    rgba = icon.convert("RGBA")
    rgba.thumbnail((max_size, max_size), Image.Resampling.LANCZOS)
    return rgba


def draw_slot(
    canvas: Image.Image,
    icon: Image.Image,
    x: int,
    y: int,
    size: int,
    padding: int,
    outline: tuple[int, int, int, int],
    slot_background: tuple[int, int, int, int],
) -> None:
    draw = ImageDraw.Draw(canvas)
    radius = max(2, min(6, size // 12))
    width = max(1, size // 64)
    draw.rounded_rectangle(
        (x, y, x + size - 1, y + size - 1),
        radius=radius,
        fill=slot_background,
        outline=outline,
        width=width,
    )
    fitted = fit_icon(icon, max(1, size - padding * 2))
    canvas.alpha_composite(fitted, (x + (size - fitted.width) // 2, y + (size - fitted.height) // 2))


def render(args: argparse.Namespace) -> int:
    map_path = project_path(args.map).resolve()
    output = project_path(args.output).resolve()
    sizes = parse_sizes(args.sizes)
    bindings = load_bindings(map_path, args.include_disabled)
    if not bindings:
        print("INVENTORY_ICON_READABILITY_PREVIEW_STATUS: FAIL no previewable bindings")
        return 1

    group_gap = args.group_gap
    slot_gap = args.slot_gap
    group_width = sum(sizes) + slot_gap * (len(sizes) - 1)
    group_height = max(sizes)
    columns = args.columns if args.columns > 0 else min(3, len(bindings))
    rows = int(math.ceil(len(bindings) / float(columns)))
    width = columns * group_width + (columns - 1) * group_gap
    height = rows * group_height + (rows - 1) * group_gap
    canvas = Image.new("RGBA", (width, height), args.page_background)

    for index, binding in enumerate(bindings):
        sprite_path = project_path(str(binding["spriteAsset"]))
        if not sprite_path.exists():
            raise FileNotFoundError(sprite_path)

        with Image.open(sprite_path) as opened_icon:
            icon = opened_icon.convert("RGBA")

        group_x = (index % columns) * (group_width + group_gap)
        group_y = (index // columns) * (group_height + group_gap)
        slot_x = group_x
        enabled = bool(binding.get("enabled", False))
        if not enabled:
            outline = args.disabled_outline
        elif binding_is_approved(binding):
            outline = args.approved_outline
        else:
            outline = args.pending_outline
        for size in sizes:
            slot_y = group_y + (group_height - size) // 2
            padding = max(2, int(round(size * args.padding_ratio)))
            draw_slot(
                canvas,
                icon,
                slot_x,
                slot_y,
                size,
                padding,
                outline,
                args.slot_background,
            )
            slot_x += size + slot_gap

    output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output, "PNG", optimize=True)
    print("INVENTORY_ICON_READABILITY_PREVIEW_STATUS: PASS")
    print(f"bindings={len(bindings)}")
    print(f"sizes={','.join(str(size) for size in sizes)}")
    print(f"preview={display_path(output)}")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--map", required=True, help="InventoryIconCandidateBindingMap.json path.")
    parser.add_argument("--output", required=True, help="Output preview PNG path.")
    parser.add_argument("--include-disabled", action="store_true")
    parser.add_argument("--sizes", default="128,64,32,24", help="Comma-separated preview slot sizes.")
    parser.add_argument("--columns", type=int, default=0)
    parser.add_argument("--slot-gap", type=int, default=12)
    parser.add_argument("--group-gap", type=int, default=22)
    parser.add_argument("--padding-ratio", type=float, default=0.12)
    parser.add_argument("--page-background", type=parse_hex_color, default=parse_hex_color("00000000"))
    parser.add_argument("--slot-background", type=parse_hex_color, default=parse_hex_color("061417ff"))
    parser.add_argument("--approved-outline", type=parse_hex_color, default=parse_hex_color("1c8093ff"))
    parser.add_argument("--pending-outline", type=parse_hex_color, default=parse_hex_color("c28a2cff"))
    parser.add_argument("--disabled-outline", type=parse_hex_color, default=parse_hex_color("806333ff"))
    return render(parser.parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
