#!/usr/bin/env python3
"""Pack transparent inventory icon candidates into fixed-cell atlas PNGs.

This is an offline source-prep tool. It does not import into Unity, edit .meta
files, create SpriteAtlases, or bind ItemData icons.
"""

from __future__ import annotations

import argparse
import json
import math
from dataclasses import asdict, dataclass
from pathlib import Path

from PIL import Image


SCRIPT_PATH = Path(__file__).resolve()
ROOT = SCRIPT_PATH.parents[1]
IMAGE_EXTENSIONS = {".png", ".webp", ".tga", ".tif", ".tiff"}


@dataclass(frozen=True)
class AtlasEntry:
    index: int
    name: str
    source: str
    atlas_rect_px: tuple[int, int, int, int]
    content_bounds_in_cell_px: tuple[int, int, int, int] | None
    coverage: float
    touches_cell_edge: bool


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


def iter_sources(source: Path) -> list[Path]:
    if source.is_file() and source.suffix.lower() in IMAGE_EXTENSIONS:
        return [source]
    if not source.exists():
        return []
    return sorted(
        path
        for path in source.rglob("*")
        if path.is_file() and path.suffix.lower() in IMAGE_EXTENSIONS and ".meta" not in path.name
    )


def fit_to_cell(image: Image.Image, cell_size: int) -> Image.Image:
    rgba = image.convert("RGBA")
    if rgba.size == (cell_size, cell_size):
        return rgba

    rgba.thumbnail((cell_size, cell_size), Image.Resampling.LANCZOS)
    cell = Image.new("RGBA", (cell_size, cell_size), (0, 0, 0, 0))
    cell.alpha_composite(rgba, ((cell_size - rgba.width) // 2, (cell_size - rgba.height) // 2))
    return cell


def analyze_cell(cell: Image.Image, edge_margin_px: int) -> tuple[tuple[int, int, int, int] | None, float, bool]:
    alpha = cell.getchannel("A")
    solid = alpha.point(lambda value: 255 if value > 8 else 0)
    bbox = solid.getbbox()
    data = solid.getdata()
    coverage = sum(1 for value in data if value > 0) / float(cell.width * cell.height)
    edge = max(1, min(edge_margin_px, cell.width // 2, cell.height // 2))
    touches_edge = bool(
        any(solid.crop((0, 0, cell.width, edge)).getdata())
        or any(solid.crop((0, cell.height - edge, cell.width, cell.height)).getdata())
        or any(solid.crop((0, 0, edge, cell.height)).getdata())
        or any(solid.crop((cell.width - edge, 0, cell.width, cell.height)).getdata())
    )
    return bbox, coverage, touches_edge


def write_scaled_atlas(atlas: Image.Image, output: Path, cell_size: int, scaled_cell_size: int) -> Path:
    scale = scaled_cell_size / float(cell_size)
    width = max(1, int(round(atlas.width * scale)))
    height = max(1, int(round(atlas.height * scale)))
    scaled = atlas.resize((width, height), Image.Resampling.LANCZOS)
    scaled_path = output.with_name(f"{output.stem}_{scaled_cell_size}xCells{output.suffix}")
    scaled.save(scaled_path, "PNG", optimize=True)
    return scaled_path


def bake(args: argparse.Namespace) -> int:
    source = Path(args.source).resolve()
    output_dir = Path(args.output).resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    sources = iter_sources(source)
    if not sources:
        print(f"INVENTORY_ATLAS_BAKER_STATUS: FAIL no image sources under {display_path(source)}")
        return 1

    columns = int(args.columns)
    cell_size = int(args.cell_size)
    rows = math.ceil(len(sources) / columns)
    atlas = Image.new("RGBA", (columns * cell_size, rows * cell_size), (0, 0, 0, 0))
    entries: list[AtlasEntry] = []

    for index, path in enumerate(sources):
        with Image.open(path) as source_image:
            cell = fit_to_cell(source_image, cell_size)
        x = (index % columns) * cell_size
        y = (index // columns) * cell_size
        atlas.alpha_composite(cell, (x, y))
        bbox, coverage, touches_edge = analyze_cell(cell, args.edge_margin_px)
        entries.append(
            AtlasEntry(
                index=index,
                name=path.stem,
                source=display_path(path),
                atlas_rect_px=(x, y, cell_size, cell_size),
                content_bounds_in_cell_px=bbox,
                coverage=round(coverage, 4),
                touches_cell_edge=touches_edge,
            )
        )

    atlas_path = output_dir / f"{args.name}_{cell_size}xCells.png"
    atlas.save(atlas_path, "PNG", optimize=True)

    scaled_paths: list[Path] = []
    for scaled_cell_size in args.scaled_cell_sizes:
        scaled_cell_size = int(scaled_cell_size)
        if scaled_cell_size == cell_size:
            continue
        scaled_paths.append(write_scaled_atlas(atlas, atlas_path, cell_size, scaled_cell_size))

    review_count = sum(1 for entry in entries if entry.touches_cell_edge or entry.coverage < args.min_coverage)
    manifest_path = output_dir / f"{args.name}_Manifest.json"
    source_bake_manifest = ""
    if args.source_bake_manifest:
        source_bake_manifest_path = Path(args.source_bake_manifest).resolve()
        if not source_bake_manifest_path.exists():
            print(f"INVENTORY_ATLAS_BAKER_STATUS: FAIL missing source bake manifest {display_path(source_bake_manifest_path)}")
            return 1
        source_bake_manifest = display_path(source_bake_manifest_path)

    payload = {
        "schema": "hecton8.inventory_atlas_bake.v1",
        "tool": "Tools/InventoryAtlasBaker.py",
        "source": display_path(source),
        "sourceBakeManifest": source_bake_manifest,
        "atlas": display_path(atlas_path),
        "scaledAtlases": [display_path(path) for path in scaled_paths],
        "cellSizePx": cell_size,
        "columns": columns,
        "rows": rows,
        "reviewCount": review_count,
        "evidenceClass": "STATIC_SOURCE_DRAFT_NO_UNITY_IMPORT",
        "entries": [asdict(entry) for entry in entries],
    }
    manifest_path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")

    if review_count > 0 and not args.allow_review:
        print("INVENTORY_ATLAS_BAKER_STATUS: FAIL_REVIEW_ITEMS")
        print(f"sources={len(sources)}")
        print(f"reviewCount={review_count}")
        print(f"atlas={display_path(atlas_path)}")
        print(f"manifest={display_path(manifest_path)}")
        return 2

    print("INVENTORY_ATLAS_BAKER_STATUS: PASS")
    print(f"sources={len(sources)}")
    print(f"reviewCount={review_count}")
    print(f"atlas={display_path(atlas_path)}")
    print(f"manifest={display_path(manifest_path)}")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True, help="Transparent icon source file or directory.")
    parser.add_argument("--output", required=True, help="Output atlas directory.")
    parser.add_argument("--name", default="TX_InventoryCandidateAtlas")
    parser.add_argument("--cell-size", type=positive_int, default=512)
    parser.add_argument("--columns", type=positive_int, default=5)
    parser.add_argument("--scaled-cell-sizes", nargs="*", type=positive_int, default=[256])
    parser.add_argument("--edge-margin-px", type=positive_int, default=12)
    parser.add_argument("--min-coverage", type=float, default=0.03)
    parser.add_argument("--source-bake-manifest", default="", help="Optional upstream InventoryIsolatedObjectBaker manifest.")
    parser.add_argument("--allow-review", action="store_true")
    return bake(parser.parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
