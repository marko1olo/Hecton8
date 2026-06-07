#!/usr/bin/env python3
"""Bake isolated inventory-object sheets into transparent draft icon sources.

This is an offline source-prep tool. It does not import into Unity, edit .meta
files, create SpriteAtlases, or prove runtime/UI binding.
"""

from __future__ import annotations

import argparse
import json
import math
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any

import cv2
import numpy as np
from PIL import Image, ImageDraw


SCRIPT_PATH = Path(__file__).resolve()
ROOT = SCRIPT_PATH.parents[1]


BATCH30_ISOLATED_OBJECTS: tuple[dict[str, Any], ...] = (
    {"name": "OxygenMicroTank", "rect": (105, 30, 545, 640)},
    {"name": "PressureRegulatorModule", "rect": (665, 20, 1370, 645)},
    {"name": "PressureSalvageCaseA", "rect": (1425, 20, 2215, 650)},
    {"name": "FoldedMultitoolA", "rect": (2215, 40, 2795, 480)},
    {"name": "FoldedMultitoolB", "rect": (55, 555, 760, 1060)},
    {"name": "PowerCoilCartridge", "rect": (980, 515, 1735, 1042)},
    {"name": "ThermalControlModule", "rect": (2045, 510, 2790, 1070)},
    {"name": "BeaconPuck", "rect": (0, 1025, 760, 1536)},
    {"name": "SampleVialRack", "rect": (1115, 925, 1950, 1536), "component_floor_scale": 0.006},
    {"name": "AlloyIngot", "rect": (2115, 1060, 2650, 1505), "note": "generator mark excluded by crop"},
)


def parse_names(raw: str, expected: int) -> list[str]:
    names = [part.strip() for part in raw.split(",") if part.strip()] if raw else []
    while len(names) < expected:
        names.append(f"Item{len(names) + 1:02d}")
    return names[:expected]


@dataclass(frozen=True)
class BakedItem:
    index: int
    name: str
    rect: tuple[int, int, int, int]
    raw: str
    alpha512: str
    coverage: float
    removed_foreground_coverage: float
    removed_bottom_band_coverage: float
    matte_touches_crop_edge: bool
    matte_touches_source_edge_margin: bool
    source_edge_margin_px: int
    status: str
    note: str


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def positive_int(raw: str) -> int:
    value = int(raw)
    if value <= 0:
        raise argparse.ArgumentTypeError("must be a positive integer")
    return value


def non_negative_float(raw: str) -> float:
    value = float(raw)
    if value < 0.0:
        raise argparse.ArgumentTypeError("must be non-negative")
    return value


def minimum_int(minimum: int):
    def parse(raw: str) -> int:
        value = int(raw)
        if value < minimum:
            raise argparse.ArgumentTypeError(f"must be at least {minimum}")
        return value

    return parse


def load_item_specs(args: argparse.Namespace) -> list[dict[str, Any]]:
    if args.preset:
        if args.preset != "batch30-isolated-objects":
            raise ValueError(f"unknown preset: {args.preset}")
        return [dict(item) for item in BATCH30_ISOLATED_OBJECTS]

    if args.grid_rows > 0 or args.grid_columns > 0:
        if args.grid_rows <= 0 or args.grid_columns <= 0:
            raise ValueError("--grid-rows and --grid-columns must be used together")

        with Image.open(Path(args.source).resolve()) as source:
            width, height = source.size
        count = args.grid_rows * args.grid_columns
        names = parse_names(args.names, count)
        inset_ratio = max(0.0, float(args.cell_inset_ratio))
        specs: list[dict[str, Any]] = []
        cell_width = width / float(args.grid_columns)
        cell_height = height / float(args.grid_rows)
        for row in range(args.grid_rows):
            for column in range(args.grid_columns):
                index = row * args.grid_columns + column
                inset_x = cell_width * inset_ratio
                inset_y = cell_height * inset_ratio
                rect = (
                    int(round(column * cell_width + inset_x)),
                    int(round(row * cell_height + inset_y)),
                    int(round((column + 1) * cell_width - inset_x)),
                    int(round((row + 1) * cell_height - inset_y)),
                )
                specs.append({"name": names[index], "rect": rect, "note": "grid cell source crop"})
        return specs

    spec_path = Path(args.spec_json).resolve()
    payload = json.loads(spec_path.read_text(encoding="utf-8"))
    items = payload["items"] if isinstance(payload, dict) else payload
    specs: list[dict[str, Any]] = []
    for item in items:
        name = str(item["name"]).strip()
        rect = tuple(int(value) for value in item["rect"])
        if len(rect) != 4:
            raise ValueError(f"{name}: rect must have four integers")
        specs.append({**item, "name": name, "rect": rect})
    return specs


def center_square(image: Image.Image, size: int, padding_ratio: float) -> Image.Image:
    rgba = image.convert("RGBA")
    bbox = rgba.getchannel("A").point(lambda value: 255 if value > 8 else 0).getbbox()
    if bbox:
        pad = max(4, int(max(bbox[2] - bbox[0], bbox[3] - bbox[1]) * 0.025))
        bbox = (
            max(0, bbox[0] - pad),
            max(0, bbox[1] - pad),
            min(rgba.width, bbox[2] + pad),
            min(rgba.height, bbox[3] + pad),
        )
        rgba = rgba.crop(bbox)

    side = max(rgba.width, rgba.height)
    canvas_side = max(1, int(round(side * (1.0 + max(0.0, padding_ratio) * 2.0))))
    canvas = Image.new("RGBA", (canvas_side, canvas_side), (0, 0, 0, 0))
    canvas.alpha_composite(rgba, ((canvas_side - rgba.width) // 2, (canvas_side - rgba.height) // 2))
    return canvas.resize((size, size), Image.Resampling.LANCZOS)


def clean_components(mask: np.ndarray, name: str, floor_scale: float | None) -> np.ndarray:
    label_count, labels, stats, centers = cv2.connectedComponentsWithStats((mask > 0).astype(np.uint8), 8)
    if label_count <= 1:
        return mask

    areas = stats[1:, cv2.CC_STAT_AREA]
    largest_area = int(areas.max()) if areas.size else 0
    if largest_area <= 0:
        return np.zeros(labels.shape, dtype=np.uint8)

    main_idx = 1 + int(np.argmax(areas))
    x, y, width, height, _ = stats[main_idx]
    expanded = (
        max(0, x - 90),
        max(0, y - 90),
        min(labels.shape[1], x + width + 90),
        min(labels.shape[0], y + height + 90),
    )

    if floor_scale is None:
        if name == "SampleVialRack":
            floor_scale = 0.006
        elif name.startswith("FoldedMultitool"):
            floor_scale = 0.012
        else:
            floor_scale = 0.018
    floor = max(220, int(largest_area * floor_scale))

    keep = np.zeros(labels.shape, dtype=np.uint8)
    for component in range(1, label_count):
        cx, cy = centers[component]
        area = int(stats[component, cv2.CC_STAT_AREA])
        if area < floor:
            continue
        near_main = expanded[0] <= cx <= expanded[2] and expanded[1] <= cy <= expanded[3]
        if component == main_idx or near_main or area > largest_area * 0.08:
            keep[labels == component] = 255
    return keep


def make_alpha(
    crop: Image.Image,
    item: dict[str, Any],
    iterations: int,
    segmentation_max_side: int,
) -> tuple[Image.Image, np.ndarray, float, float]:
    full_rgb = np.array(crop.convert("RGB"))
    height, width = full_rgb.shape[:2]
    segment_rgb = full_rgb
    segment_width = width
    segment_height = height
    scale = 1.0
    if segmentation_max_side > 0 and max(width, height) > segmentation_max_side:
        scale = segmentation_max_side / float(max(width, height))
        segment_width = max(1, int(round(width * scale)))
        segment_height = max(1, int(round(height * scale)))
        segment = crop.convert("RGB").resize((segment_width, segment_height), Image.Resampling.LANCZOS)
        segment_rgb = np.array(segment)

    border = max(8, min(24, segment_width // 16, segment_height // 16))
    rect = (
        border,
        border,
        max(1, segment_width - 2 * border),
        max(1, segment_height - 2 * border),
    )
    bgr = cv2.cvtColor(segment_rgb, cv2.COLOR_RGB2BGR)

    mask = np.zeros((segment_height, segment_width), np.uint8)
    bg_model = np.zeros((1, 65), np.float64)
    fg_model = np.zeros((1, 65), np.float64)
    cv2.grabCut(bgr, mask, rect, bg_model, fg_model, iterations, cv2.GC_INIT_WITH_RECT)
    hard = np.where((mask == cv2.GC_FGD) | (mask == cv2.GC_PR_FGD), 255, 0).astype(np.uint8)

    if scale != 1.0:
        hard_image = Image.fromarray(hard, "L").resize((width, height), Image.Resampling.NEAREST)
        hard = np.array(hard_image)

    arr = full_rgb.astype(np.float32)
    luma = arr[:, :, 0] * 0.2126 + arr[:, :, 1] * 0.7152 + arr[:, :, 2] * 0.0722
    chroma = arr.max(axis=2) - arr.min(axis=2)
    interior = np.zeros_like(hard)
    interior[border : height - border, border : width - border] = 1
    rescue = (((luma > 105.0) | (chroma > 42.0)) & (interior > 0)).astype(np.uint8) * 255
    hard = cv2.bitwise_or(hard, rescue)

    hard = cv2.morphologyEx(hard, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8), iterations=1)
    hard = cv2.morphologyEx(hard, cv2.MORPH_CLOSE, np.ones((5, 5), np.uint8), iterations=1)
    preclean = hard.copy()
    hard = clean_components(hard, str(item["name"]), item.get("component_floor_scale"))
    removed = cv2.bitwise_and(preclean, cv2.bitwise_not(hard))
    removed_coverage = float((removed > 0).sum()) / float(removed.size)
    bottom_band_start = int(round(removed.shape[0] * 0.72))
    bottom_band = removed[bottom_band_start:, :]
    removed_bottom_band_coverage = float((bottom_band > 0).sum()) / float(bottom_band.size)

    alpha = cv2.GaussianBlur(hard, (0, 0), sigmaX=0.75, sigmaY=0.75)
    rgba = np.array(crop.convert("RGBA"))
    rgba[:, :, 3] = np.clip(alpha, 0, 255).astype(np.uint8)
    return Image.fromarray(rgba, "RGBA"), hard, removed_coverage, removed_bottom_band_coverage


def checker(size: int) -> Image.Image:
    tile = 16
    image = Image.new("RGBA", (size, size), (26, 26, 26, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, size, tile):
        for x in range(0, size, tile):
            value = 52 if ((x // tile + y // tile) & 1) == 0 else 29
            draw.rectangle((x, y, x + tile - 1, y + tile - 1), fill=(value, value, value, 255))
    return image


def mask_touches_edge(mask: np.ndarray, margin_px: int) -> bool:
    if mask.size == 0:
        return False

    edge = max(1, min(int(margin_px), mask.shape[0] // 2, mask.shape[1] // 2))
    return bool(
        (mask[:edge, :] > 0).any()
        or (mask[-edge:, :] > 0).any()
        or (mask[:, :edge] > 0).any()
        or (mask[:, -edge:] > 0).any()
    )


def write_contact(paths: list[Path], items: list[BakedItem], output: Path, thumb_size: int, columns: int) -> None:
    rows = math.ceil(len(paths) / columns)
    sheet = Image.new("RGBA", (columns * thumb_size, rows * thumb_size), (8, 8, 8, 255))
    for index, path in enumerate(paths):
        base = checker(thumb_size)
        with Image.open(path) as opened_icon:
            icon = opened_icon.convert("RGBA")
        icon.thumbnail((thumb_size - 22, thumb_size - 22), Image.Resampling.LANCZOS)
        base.alpha_composite(icon, ((thumb_size - icon.width) // 2, (thumb_size - icon.height) // 2))
        status = items[index].status
        if status.startswith("REVIEW") or status.startswith("REJECT"):
            color = (255, 190, 70, 255) if status.startswith("REVIEW") else (255, 70, 50, 255)
            ImageDraw.Draw(base).rectangle((1, 1, thumb_size - 2, thumb_size - 2), outline=color, width=4)
        sheet.alpha_composite(base, ((index % columns) * thumb_size, (index // columns) * thumb_size))
    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output, "PNG", optimize=True)


def scale_rect(rect: tuple[int, int, int, int], scale: float) -> tuple[int, int, int, int]:
    return tuple(int(round(value * scale)) for value in rect)  # type: ignore[return-value]


def write_source_margin_preview(
    source_image: Image.Image,
    items: list[BakedItem],
    output: Path,
    max_side: int,
) -> None:
    rgba = source_image.convert("RGBA")
    scale = 1.0
    if max_side > 0 and max(rgba.width, rgba.height) > max_side:
        scale = max_side / float(max(rgba.width, rgba.height))
        size = (max(1, int(round(rgba.width * scale))), max(1, int(round(rgba.height * scale))))
        rgba = rgba.resize(size, Image.Resampling.LANCZOS)

    draw = ImageDraw.Draw(rgba)
    width = max(1, int(round(4 * scale)))
    margin_width = max(1, int(round(2 * scale)))
    for item in items:
        color = (76, 230, 165, 255)
        if item.status.startswith("REVIEW"):
            color = (255, 190, 70, 255)
        elif item.status.startswith("REJECT"):
            color = (255, 70, 50, 255)

        rect = scale_rect(item.rect, scale)
        draw.rectangle(rect, outline=color, width=width)

        margin = int(round(item.source_edge_margin_px * scale))
        if margin > 0:
            inner = (
                rect[0] + margin,
                rect[1] + margin,
                rect[2] - margin,
                rect[3] - margin,
            )
            if inner[2] > inner[0] and inner[3] > inner[1]:
                draw.rectangle(inner, outline=(255, 210, 90, 255), width=margin_width)

    output.parent.mkdir(parents=True, exist_ok=True)
    rgba.save(output, "PNG", optimize=True)


def bake(args: argparse.Namespace) -> int:
    source = Path(args.source).resolve()
    output = Path(args.output).resolve()
    raw_dir = output / "RawCrops"
    alpha_dir = output / "Alpha512"
    raw_dir.mkdir(parents=True, exist_ok=True)
    alpha_dir.mkdir(parents=True, exist_ok=True)

    with Image.open(source) as opened_source:
        source_image = opened_source.convert("RGBA")
    specs = load_item_specs(args)
    baked_items: list[BakedItem] = []
    alpha_paths: list[Path] = []

    for index, item in enumerate(specs, start=1):
        name = str(item["name"])
        rect = tuple(int(value) for value in item["rect"])
        crop = source_image.crop(rect).convert("RGBA")
        stem = f"{args.stem_prefix}_{index:02d}_{name}"
        raw_path = raw_dir / f"RAW_{stem}.png"
        alpha_path = alpha_dir / f"{stem}_Alpha512.png"
        crop.save(raw_path, "PNG", optimize=True)

        alpha_image, hard_mask, removed_coverage, removed_bottom_band_coverage = make_alpha(
            crop,
            item,
            args.grabcut_iterations,
            args.segmentation_max_side,
        )
        touches_edge = mask_touches_edge(hard_mask, 4)
        touches_source_edge_margin = mask_touches_edge(hard_mask, args.source_edge_margin_px)
        coverage = float((hard_mask > 0).sum()) / float(hard_mask.size)
        status = "OK"
        notes = [str(item.get("note", "static alpha candidate; Unity import/proof not run"))]
        if touches_edge:
            status = "REJECT_MATTE_TOUCHES_CROP_EDGE"
            notes.append("foreground matte touches crop edge")
        elif touches_source_edge_margin:
            status = "REVIEW_SOURCE_CELL_EDGE_MARGIN"
            notes.append(
                f"foreground enters reserved {args.source_edge_margin_px}px source-cell edge margin; "
                "regenerate sheet with more empty padding to avoid clipped tools/resources"
            )
        if coverage < args.min_coverage:
            status = "REVIEW_LOW_ALPHA_COVERAGE"
            notes.append("low detected foreground coverage")
        if removed_coverage > args.max_removed_foreground_coverage:
            status = "REVIEW_REMOVED_FOREGROUND_DEBRIS"
            notes.append("separate foreground debris removed; inspect for text, labels, watermark fragments, or cut object parts")
        if removed_bottom_band_coverage > args.max_removed_bottom_band_coverage:
            status = "REVIEW_REMOVED_BOTTOM_BAND_FOREGROUND"
            notes.append("removed foreground in lower caption band; inspect for text, labels, watermark fragments, or over-low object crop")

        center_square(alpha_image, args.size, args.padding_ratio).save(alpha_path, "PNG", optimize=True)
        alpha_paths.append(alpha_path)
        baked_items.append(
            BakedItem(
                index=index,
                name=name,
                rect=rect,
                raw=display_path(raw_path),
                alpha512=display_path(alpha_path),
                coverage=round(coverage, 4),
                removed_foreground_coverage=round(removed_coverage, 4),
                removed_bottom_band_coverage=round(removed_bottom_band_coverage, 4),
                matte_touches_crop_edge=touches_edge,
                matte_touches_source_edge_margin=touches_source_edge_margin,
                source_edge_margin_px=int(args.source_edge_margin_px),
                status=status,
                note="; ".join(note for note in notes if note),
            )
        )

    contact_path = output / "IsolatedInventory_CleanContactSheet.png"
    write_contact(alpha_paths, baked_items, contact_path, args.contact_thumb_size, args.contact_columns)
    source_margin_preview_path = output / "InventorySourceGridMarginPreview.png"
    write_source_margin_preview(source_image, baked_items, source_margin_preview_path, args.source_preview_max_side)
    manifest_path = output / "InventoryIsolatedObjectBakeManifest.json"
    review_count = sum(1 for item in baked_items if item.status != "OK")
    payload = {
        "schema": "hecton8.inventory_isolated_object_bake.v1",
        "tool": "Tools/InventoryIsolatedObjectBaker.py",
        "source": display_path(source),
        "output": display_path(output),
        "contactSheet": display_path(contact_path),
        "sourceGridMarginPreview": display_path(source_margin_preview_path),
        "evidenceClass": "STATIC_SOURCE_DRAFT_NO_UNITY_IMPORT",
        "reviewCount": review_count,
        "items": [asdict(item) for item in baked_items],
    }
    manifest_path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")

    if review_count > 0 and not args.allow_review:
        print("INVENTORY_ISOLATED_OBJECT_BAKER_STATUS: FAIL_REVIEW_ITEMS")
        print(f"source={display_path(source)}")
        print(f"items={len(baked_items)}")
        print(f"reviewCount={review_count}")
        print(f"contact={display_path(contact_path)}")
        print(f"sourceMarginPreview={display_path(source_margin_preview_path)}")
        print(f"manifest={display_path(manifest_path)}")
        return 2

    print("INVENTORY_ISOLATED_OBJECT_BAKER_STATUS: PASS")
    print(f"source={display_path(source)}")
    print(f"items={len(baked_items)}")
    print(f"reviewCount={review_count}")
    print(f"contact={display_path(contact_path)}")
    print(f"sourceMarginPreview={display_path(source_margin_preview_path)}")
    print(f"manifest={display_path(manifest_path)}")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--preset", default="", help="Supported: batch30-isolated-objects.")
    parser.add_argument("--spec-json", default="", help="JSON list or object with items[{name, rect}].")
    parser.add_argument("--grid-rows", type=int, default=0)
    parser.add_argument("--grid-columns", type=int, default=0)
    parser.add_argument("--names", default="", help="Comma-separated names for grid/spec output order.")
    parser.add_argument("--cell-inset-ratio", type=non_negative_float, default=0.0)
    parser.add_argument("--stem-prefix", default="DRAFT_TX_B30_IsolatedInventory")
    parser.add_argument("--size", type=positive_int, default=512)
    parser.add_argument("--padding-ratio", type=non_negative_float, default=0.13)
    parser.add_argument("--grabcut-iterations", type=positive_int, default=6)
    parser.add_argument("--segmentation-max-side", type=positive_int, default=512)
    parser.add_argument("--source-edge-margin-px", type=positive_int, default=24)
    parser.add_argument("--source-preview-max-side", type=positive_int, default=2048)
    parser.add_argument("--min-coverage", type=non_negative_float, default=0.035)
    parser.add_argument("--max-removed-foreground-coverage", type=non_negative_float, default=0.018)
    parser.add_argument("--max-removed-bottom-band-coverage", type=non_negative_float, default=0.002)
    parser.add_argument("--allow-review", action="store_true")
    parser.add_argument("--contact-thumb-size", type=minimum_int(32), default=256)
    parser.add_argument("--contact-columns", type=positive_int, default=5)
    args = parser.parse_args()
    if not args.preset and not args.spec_json and (args.grid_rows <= 0 or args.grid_columns <= 0):
        parser.error("provide --preset, --spec-json, or --grid-rows/--grid-columns")
    return bake(args)


if __name__ == "__main__":
    raise SystemExit(main())
