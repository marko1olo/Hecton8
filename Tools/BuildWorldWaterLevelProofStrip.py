#!/usr/bin/env python3
"""Build a multi-point water-level proof strip from deterministic macro geology.

This does not author terrain and does not bake water into a heightmap. It reads a
calibrated world water level, samples the same macro-geology preview field used
by the calibration tool, and writes a small SVG/JSON proof for shallow shore,
playable shallows, transition, upper saturated water, and deep transition points.
"""

from __future__ import annotations

import argparse
import json
import math
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from BuildWorldMacroGeologyPreview import ARTIFACT_VERSION, MIN_EXTENT_METERS, Params, evaluate_height, write_png_rgb
from CalibrateWorldWaterLevel import make_params


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_CALIBRATION = (
    ROOT
    / "Docs"
    / "GeneratedAssets"
    / "Terrain"
    / "MacroGeology"
    / "WorldWaterLevelCalibration_Extent30000m_Res192.json"
)
DEFAULT_OUTPUT = ROOT / "Docs" / "GeneratedAssets" / "Water"


@dataclass(frozen=True)
class TargetPoint:
    key: str
    title: str
    target_depth_m: float
    description: str
    requires_shoreline: bool = False
    preferred_slope01: float = 0.0
    min_separation_m: float = 650.0


@dataclass(frozen=True)
class GridSample:
    x: float
    z: float
    height: float
    depth: float
    slope01: float
    exposed_neighbor: bool


@dataclass(frozen=True)
class SelectedPoint:
    target: TargetPoint
    sample: GridSample
    score: float


TARGETS = (
    TargetPoint(
        "shallow_shore",
        "Shallow shore",
        5.0,
        "Readable contact band for foam, shore VFX, and player entry.",
        requires_shoreline=True,
        preferred_slope01=0.035,
        min_separation_m=500.0,
    ),
    TargetPoint(
        "underwater_slope",
        "Underwater slope",
        25.0,
        "Early descent where seabed strata should stay visible under the surface.",
        preferred_slope01=0.065,
        min_separation_m=650.0,
    ),
    TargetPoint(
        "playable_50m",
        "Playable 50 m",
        50.0,
        "0-50 m gameplay band for readable traversal, UI depth, VFX, and audio.",
        preferred_slope01=0.04,
        min_separation_m=800.0,
    ),
    TargetPoint(
        "transition_100m",
        "Transition 100 m",
        100.0,
        "0-100 m band where fog and absorption should transition, not black out.",
        preferred_slope01=0.035,
        min_separation_m=900.0,
    ),
    TargetPoint(
        "upper_saturated_500m",
        "Upper saturated 500 m",
        500.0,
        "0-500 m saturated route zone for silhouette, pressure, and audio staging.",
        preferred_slope01=0.025,
        min_separation_m=1100.0,
    ),
    TargetPoint(
        "deep_transition",
        "Deep transition",
        900.0,
        "Below upper saturated water where strata, pressure, and visibility diverge.",
        preferred_slope01=0.02,
        min_separation_m=1300.0,
    ),
)


def clamp(value: float, low: float, high: float) -> float:
    return max(low, min(high, value))


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def choose_water_level(calibration_path: Path, explicit_water_level: float | None) -> tuple[float, str, dict[str, Any] | None]:
    if explicit_water_level is not None:
        return explicit_water_level, "explicit-arg", None
    if not calibration_path.exists():
        return 14.02, "fallback-no-calibration-json", None

    data = read_json(calibration_path)
    for lane in ("strictCandidateLevels", "bestLevels", "allLevels"):
        levels = data.get(lane)
        if isinstance(levels, list) and levels:
            first = levels[0]
            if isinstance(first, dict) and "waterLevelMeters" in first:
                return float(first["waterLevelMeters"]), f"{calibration_path.as_posix()}:{lane}[0]", data
    return 14.02, "fallback-empty-calibration-json", data


def resolve_calibration_arg(value: str) -> Path:
    if not value:
        return DEFAULT_CALIBRATION
    path = Path(value)
    if not path.is_absolute():
        path = ROOT / path
    return path


def sample_grid(
    params: Params,
    water_level_y: float,
    origin_x: float,
    origin_z: float,
    scan_extent_m: float,
    resolution: int,
) -> list[GridSample]:
    half = scan_extent_m * 0.5
    step = scan_extent_m / max(1, resolution - 1)
    heights: list[float] = [0.0] * (resolution * resolution)
    for row in range(resolution):
        z = origin_z - half + row * step
        base = row * resolution
        for column in range(resolution):
            x = origin_x - half + column * step
            heights[base + column] = evaluate_height(x, z, params)[0]

    samples: list[GridSample] = []
    for row in range(resolution):
        z = origin_z - half + row * step
        base = row * resolution
        for column in range(resolution):
            x = origin_x - half + column * step
            index = base + column
            height = heights[index]
            left = heights[base + max(0, column - 1)]
            right = heights[base + min(resolution - 1, column + 1)]
            up = heights[max(0, row - 1) * resolution + column]
            down = heights[min(resolution - 1, row + 1) * resolution + column]
            slope01 = clamp(max(abs(right - left), abs(down - up)) / max(1.0, step * 2.0), 0.0, 1.0)
            exposed_neighbor = False
            for nx, nz in ((column - 1, row), (column + 1, row), (column, row - 1), (column, row + 1)):
                if nx < 0 or nz < 0 or nx >= resolution or nz >= resolution:
                    continue
                if heights[nz * resolution + nx] >= water_level_y:
                    exposed_neighbor = True
                    break
            samples.append(
                GridSample(
                    x=x,
                    z=z,
                    height=height,
                    depth=water_level_y - height,
                    slope01=slope01,
                    exposed_neighbor=exposed_neighbor,
                )
            )
    return samples


def separation_penalty(sample: GridSample, selected: list[SelectedPoint], min_separation_m: float) -> float:
    penalty = 0.0
    for point in selected:
        dx = sample.x - point.sample.x
        dz = sample.z - point.sample.z
        distance = math.sqrt(dx * dx + dz * dz)
        if distance < min_separation_m:
            penalty += (min_separation_m - distance) * 0.35
    return penalty


def score_target(target: TargetPoint, sample: GridSample, selected: list[SelectedPoint]) -> float:
    if sample.depth <= 0.0:
        return 1.0e9
    score = abs(sample.depth - target.target_depth_m)
    if target.requires_shoreline and not sample.exposed_neighbor:
        score += 180.0
    if target.preferred_slope01 > 0.0:
        score += abs(sample.slope01 - target.preferred_slope01) * 750.0
    score += separation_penalty(sample, selected, target.min_separation_m)
    if sample.depth > target.target_depth_m * 2.5 + 40.0:
        score += 50.0
    return score


def build_sample_at(params: Params, water_level_y: float, x: float, z: float, probe_m: float) -> GridSample:
    height = evaluate_height(x, z, params)[0]
    left = evaluate_height(x - probe_m, z, params)[0]
    right = evaluate_height(x + probe_m, z, params)[0]
    up = evaluate_height(x, z - probe_m, params)[0]
    down = evaluate_height(x, z + probe_m, params)[0]
    slope01 = clamp(max(abs(right - left), abs(down - up)) / max(1.0, probe_m * 2.0), 0.0, 1.0)
    exposed_neighbor = (
        left >= water_level_y
        or right >= water_level_y
        or up >= water_level_y
        or down >= water_level_y
    )
    return GridSample(
        x=x,
        z=z,
        height=height,
        depth=water_level_y - height,
        slope01=slope01,
        exposed_neighbor=exposed_neighbor,
    )


def refine_selected_sample(
    params: Params,
    water_level_y: float,
    target: TargetPoint,
    coarse_sample: GridSample,
    selected: list[SelectedPoint],
) -> tuple[GridSample, float]:
    best_sample = coarse_sample
    best_score = score_target(target, coarse_sample, selected)
    depth = max(1.0, target.target_depth_m)
    radius = 520.0
    if depth >= 500.0:
        radius = 1500.0
    elif depth >= 100.0:
        radius = 900.0
    elif depth >= 50.0:
        radius = 700.0

    for pass_index, divisions in enumerate((10, 10, 8)):
        step = radius / max(1, divisions)
        probe = max(12.0, step * 0.6)
        center_x = best_sample.x
        center_z = best_sample.z
        half = divisions // 2
        for zi in range(-half, half + 1):
            z = center_z + zi * step
            for xi in range(-half, half + 1):
                x = center_x + xi * step
                candidate = build_sample_at(params, water_level_y, x, z, probe)
                score = score_target(target, candidate, selected)
                if score < best_score:
                    best_score = score
                    best_sample = candidate
        radius *= 0.32 if pass_index == 0 else 0.26

    return best_sample, best_score


def select_points(params: Params, water_level_y: float, samples: list[GridSample]) -> list[SelectedPoint]:
    selected: list[SelectedPoint] = []
    for target in TARGETS:
        best_sample: GridSample | None = None
        best_score = 1.0e9
        for sample in samples:
            score = score_target(target, sample, selected)
            if score < best_score:
                best_score = score
                best_sample = sample
        if best_sample is None:
            continue
        best_sample, best_score = refine_selected_sample(params, water_level_y, target, best_sample, selected)
        selected.append(SelectedPoint(target=target, sample=best_sample, score=best_score))
    return selected


def gradient_direction(params: Params, x: float, z: float, probe_m: float) -> tuple[float, float]:
    left = evaluate_height(x - probe_m, z, params)[0]
    right = evaluate_height(x + probe_m, z, params)[0]
    up = evaluate_height(x, z - probe_m, params)[0]
    down = evaluate_height(x, z + probe_m, params)[0]
    dx = right - left
    dz = down - up
    length = math.sqrt(dx * dx + dz * dz)
    if length < 0.0001:
        return 1.0, 0.0
    return dx / length, dz / length


def build_transect(
    params: Params,
    point: SelectedPoint,
    water_level_y: float,
    samples: int,
) -> list[dict[str, float]]:
    target_depth = point.target.target_depth_m
    span = 1600.0
    if target_depth >= 500.0:
        span = 5200.0
    elif target_depth >= 100.0:
        span = 3200.0
    elif target_depth >= 50.0:
        span = 2200.0
    dx, dz = gradient_direction(params, point.sample.x, point.sample.z, max(20.0, span / 80.0))
    rows: list[dict[str, float]] = []
    for index in range(samples):
        t = index / max(1, samples - 1)
        offset = (t - 0.5) * span
        x = point.sample.x + dx * offset
        z = point.sample.z + dz * offset
        height = evaluate_height(x, z, params)[0]
        rows.append(
            {
                "x": round(x, 3),
                "z": round(z, 3),
                "height": round(height, 3),
                "depth": round(water_level_y - height, 3),
            }
        )
    return rows


def xml_escape(value: str) -> str:
    return (
        value.replace("&", "&amp;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
        .replace('"', "&quot;")
    )


def depth_fill(depth: float) -> str:
    if depth <= 50.0:
        return "#55d0c7"
    if depth <= 100.0:
        return "#31aebd"
    if depth <= 500.0:
        return "#1d6689"
    return "#132d49"


def rgb(value: str) -> tuple[int, int, int]:
    value = value.lstrip("#")
    return int(value[0:2], 16), int(value[2:4], 16), int(value[4:6], 16)


def terrain_fill(depth: float) -> str:
    if depth <= 12.0:
        return "#c0b17a"
    if depth <= 100.0:
        return "#7c866d"
    if depth <= 500.0:
        return "#465f61"
    return "#253546"


def polyline(points: list[tuple[float, float]]) -> str:
    return " ".join(f"{x:.1f},{y:.1f}" for x, y in points)


def render_svg(
    selected: list[SelectedPoint],
    transects: dict[str, list[dict[str, float]]],
    water_level_y: float,
    water_level_source: str,
    scan_extent_m: float,
    resolution: int,
) -> str:
    width = 980
    panel_h = 168
    gap = 18
    top = 82
    left = 28
    plot_left = 224
    plot_width = width - plot_left - 42
    height = top + len(selected) * (panel_h + gap) + 44
    lines: list[str] = [
        '<?xml version="1.0" encoding="UTF-8"?>',
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">',
        '<rect width="100%" height="100%" fill="#08111a"/>',
        '<style>text{font-family:Inter,Segoe UI,Arial,sans-serif}.small{font-size:12px;fill:#a9bdc8}.label{font-size:14px;fill:#d8e6e8}.title{font-size:22px;font-weight:700;fill:#edf7f5}.mono{font-size:11px;fill:#9eb2bd}</style>',
        '<text class="title" x="28" y="34">HECTON-8 water-level proof strip</text>',
        f'<text class="small" x="28" y="56">waterY={water_level_y:.2f} m, source={xml_escape(water_level_source)}, scan={scan_extent_m:.0f} m @ {resolution}x{resolution}</text>',
        '<text class="small" x="28" y="72">terrain source is sampled only; water is a movable surface above terrain</text>',
    ]
    for index, point in enumerate(selected):
        y0 = top + index * (panel_h + gap)
        plot_top = y0 + 16
        plot_h = panel_h - 32
        water_y = plot_top + 42
        water_h = plot_h - 48
        land_h = 38
        rows = transects[point.target.key]
        max_depth = max(max(0.0, float(row["depth"])) for row in rows)
        max_above = max(max(0.0, -float(row["depth"])) for row in rows)
        depth_scale = max(point.target.target_depth_m * 1.28 + 16.0, max_depth * 1.06, 60.0)
        above_scale = max(35.0, max_above * 1.08)
        terrain_points: list[tuple[float, float]] = []
        for sample_index, row in enumerate(rows):
            x = plot_left + plot_width * sample_index / max(1, len(rows) - 1)
            depth = float(row["depth"])
            if depth >= 0.0:
                y = water_y + clamp(depth / depth_scale, 0.0, 1.0) * water_h
            else:
                y = water_y - clamp((-depth) / above_scale, 0.0, 1.0) * land_h
            terrain_points.append((x, y))
        selected_x = plot_left + plot_width * 0.5
        selected_depth = point.sample.depth
        selected_y = water_y + clamp(max(0.0, selected_depth) / depth_scale, 0.0, 1.0) * water_h
        water_color = depth_fill(point.target.target_depth_m)
        bed_color = terrain_fill(point.target.target_depth_m)
        terrain_polygon = [(plot_left, plot_top + plot_h)] + terrain_points + [(plot_left + plot_width, plot_top + plot_h)]
        lines.extend(
            [
                f'<rect x="{left}" y="{y0}" width="{width - 56}" height="{panel_h}" fill="#0c1b25" stroke="#1d3946" stroke-width="1"/>',
                f'<text class="label" x="{left + 14}" y="{y0 + 28}">{xml_escape(point.target.title)}</text>',
                f'<text class="small" x="{left + 14}" y="{y0 + 48}">target {point.target.target_depth_m:.0f} m, sampled {selected_depth:.1f} m</text>',
                f'<text class="mono" x="{left + 14}" y="{y0 + 68}">x {point.sample.x:.1f}, z {point.sample.z:.1f}</text>',
                f'<text class="mono" x="{left + 14}" y="{y0 + 84}">height {point.sample.height:.1f}</text>',
                f'<text class="mono" x="{left + 14}" y="{y0 + 100}">slope01 {point.sample.slope01:.3f}</text>',
                f'<text class="small" x="{left + 14}" y="{y0 + 124}">{xml_escape(point.target.description)}</text>',
                f'<rect x="{plot_left}" y="{water_y}" width="{plot_width}" height="{plot_h - (water_y - plot_top)}" fill="{water_color}" opacity="0.52"/>',
                f'<rect x="{plot_left}" y="{plot_top}" width="{plot_width}" height="{water_y - plot_top}" fill="#18242a" opacity="0.95"/>',
                f'<polygon points="{polyline(terrain_polygon)}" fill="{bed_color}" opacity="0.95"/>',
                f'<polyline points="{polyline(terrain_points)}" fill="none" stroke="#d0c080" stroke-width="1.2" opacity="0.9"/>',
                f'<line x1="{plot_left}" y1="{water_y}" x2="{plot_left + plot_width}" y2="{water_y}" stroke="#8cf4ee" stroke-width="2" opacity="0.92"/>',
                f'<line x1="{selected_x}" y1="{plot_top + 2}" x2="{selected_x}" y2="{plot_top + plot_h}" stroke="#f6f0b4" stroke-width="1" stroke-dasharray="4 4"/>',
                f'<circle cx="{selected_x}" cy="{selected_y}" r="4.5" fill="#fff4af" stroke="#08111a" stroke-width="1"/>',
                f'<text class="mono" x="{plot_left + 6}" y="{water_y - 7}">surface</text>',
                f'<text class="mono" x="{plot_left + plot_width - 104}" y="{plot_top + plot_h - 8}">transect</text>',
            ]
        )
    lines.append("</svg>")
    return "\n".join(lines)


def fill_rect(
    pixels: list[tuple[int, int, int]],
    width: int,
    height: int,
    x0: int,
    y0: int,
    x1: int,
    y1: int,
    color: tuple[int, int, int],
) -> None:
    x0 = max(0, min(width, x0))
    x1 = max(0, min(width, x1))
    y0 = max(0, min(height, y0))
    y1 = max(0, min(height, y1))
    if x1 <= x0 or y1 <= y0:
        return
    for y in range(y0, y1):
        base = y * width
        for x in range(x0, x1):
            pixels[base + x] = color


def draw_line(
    pixels: list[tuple[int, int, int]],
    width: int,
    height: int,
    x0: int,
    y0: int,
    x1: int,
    y1: int,
    color: tuple[int, int, int],
) -> None:
    dx = abs(x1 - x0)
    sx = 1 if x0 < x1 else -1
    dy = -abs(y1 - y0)
    sy = 1 if y0 < y1 else -1
    err = dx + dy
    while True:
        if 0 <= x0 < width and 0 <= y0 < height:
            pixels[y0 * width + x0] = color
        if x0 == x1 and y0 == y1:
            break
        e2 = 2 * err
        if e2 >= dy:
            err += dy
            x0 += sx
        if e2 <= dx:
            err += dx
            y0 += sy


def draw_circle(
    pixels: list[tuple[int, int, int]],
    width: int,
    height: int,
    cx: int,
    cy: int,
    radius: int,
    color: tuple[int, int, int],
) -> None:
    r2 = radius * radius
    for y in range(cy - radius, cy + radius + 1):
        if y < 0 or y >= height:
            continue
        for x in range(cx - radius, cx + radius + 1):
            if x < 0 or x >= width:
                continue
            dx = x - cx
            dy = y - cy
            if dx * dx + dy * dy <= r2:
                pixels[y * width + x] = color


def render_png_pixels(
    selected: list[SelectedPoint],
    transects: dict[str, list[dict[str, float]]],
) -> tuple[int, int, list[tuple[int, int, int]]]:
    width = 980
    panel_h = 150
    gap = 18
    top = 28
    left = 28
    plot_left = 224
    plot_width = width - plot_left - 42
    height = top + len(selected) * (panel_h + gap) + 28
    pixels = [(8, 17, 26)] * (width * height)
    fill_rect(pixels, width, height, 0, 0, width, height, (8, 17, 26))
    for index, point in enumerate(selected):
        y0 = top + index * (panel_h + gap)
        plot_top = y0 + 16
        plot_h = panel_h - 32
        water_y = plot_top + 42
        water_h = plot_h - 48
        land_h = 38
        rows = transects[point.target.key]
        max_depth = max(max(0.0, float(row["depth"])) for row in rows)
        max_above = max(max(0.0, -float(row["depth"])) for row in rows)
        depth_scale = max(point.target.target_depth_m * 1.28 + 16.0, max_depth * 1.06, 60.0)
        above_scale = max(35.0, max_above * 1.08)
        water_color = rgb(depth_fill(point.target.target_depth_m))
        bed_color = rgb(terrain_fill(point.target.target_depth_m))
        fill_rect(pixels, width, height, left, y0, width - left, y0 + panel_h, (12, 27, 37))
        fill_rect(pixels, width, height, plot_left, water_y, plot_left + plot_width, plot_top + plot_h, water_color)
        fill_rect(pixels, width, height, plot_left, plot_top, plot_left + plot_width, water_y, (24, 36, 42))
        surface_points: list[tuple[int, int]] = []
        for sample_index, row in enumerate(rows):
            x = int(round(plot_left + plot_width * sample_index / max(1, len(rows) - 1)))
            depth = float(row["depth"])
            if depth >= 0.0:
                y = int(round(water_y + clamp(depth / depth_scale, 0.0, 1.0) * water_h))
            else:
                y = int(round(water_y - clamp((-depth) / above_scale, 0.0, 1.0) * land_h))
            surface_points.append((x, y))
        for sample_index, (x, y) in enumerate(surface_points):
            next_x = surface_points[min(len(surface_points) - 1, sample_index + 1)][0]
            fill_rect(pixels, width, height, x, y, max(x + 1, next_x + 1), plot_top + plot_h, bed_color)
        for a, b in zip(surface_points, surface_points[1:]):
            draw_line(pixels, width, height, a[0], a[1], b[0], b[1], (208, 192, 128))
        draw_line(pixels, width, height, plot_left, water_y, plot_left + plot_width, water_y, (140, 244, 238))
        marker_x = plot_left + plot_width // 2
        marker_depth = max(0.0, point.sample.depth)
        marker_y = int(round(water_y + clamp(marker_depth / depth_scale, 0.0, 1.0) * water_h))
        for dash_y in range(plot_top, plot_top + plot_h, 10):
            draw_line(pixels, width, height, marker_x, dash_y, marker_x, min(plot_top + plot_h, dash_y + 5), (246, 240, 180))
        draw_circle(pixels, width, height, marker_x, marker_y, 5, (255, 244, 175))
        fill_rect(pixels, width, height, left + 14, y0 + 22, left + 94, y0 + 30, rgb(depth_fill(point.target.target_depth_m)))
    return width, height, pixels


def output_prefix(args: argparse.Namespace, water_level_y: float) -> Path:
    if args.output_prefix:
        path = Path(args.output_prefix)
        if not path.is_absolute():
            path = ROOT / path
        ValidateOutputPrefixMatchesWaterLevel(path, water_level_y)
        return path
    return DEFAULT_OUTPUT / f"WorldWaterLevelProof_Seed{args.seed}_WaterY{int(round(water_level_y))}"


def ValidateOutputPrefixMatchesWaterLevel(path: Path, water_level_y: float) -> None:
    match = re.search(r"WaterY(-?\d+)", path.name)
    if not match:
        return

    token_water_level_y = int(match.group(1))
    actual_water_level_y = int(round(water_level_y))
    if token_water_level_y != actual_water_level_y:
        raise ValueError(
            "Output prefix water-level token WaterY"
            + str(token_water_level_y)
            + " does not match resolved water level "
            + str(actual_water_level_y)
            + ". Pass --water-level-m explicitly or rename --output-prefix."
        )


def build_manifest(
    args: argparse.Namespace,
    calibration_data: dict[str, Any] | None,
    calibration_path: Path,
    water_level_y: float,
    water_level_source: str,
    selected: list[SelectedPoint],
    transects: dict[str, list[dict[str, float]]],
    svg_path: Path,
) -> dict[str, Any]:
    return {
        "artifact": "WorldWaterLevelProofStrip",
        "artifactVersion": 1,
        "macroGeologyPreviewArtifactVersion": ARTIFACT_VERSION,
        "seed": args.seed,
        "runtimeSeed": args.runtime_seed,
        "combinedSeedSource": "BuildWorldMacroGeologyPreview.combine_world_seed via CalibrateWorldWaterLevel.make_params",
        "waterLevelY": round(water_level_y, 3),
        "waterLevelSource": water_level_source,
        "calibrationPath": str(calibration_path.relative_to(ROOT).as_posix()) if calibration_path.exists() else str(calibration_path),
        "scan": {
            "originX": float(args.origin_x_m),
            "originZ": float(args.origin_z_m),
            "extentMeters": float(args.scan_extent_m),
            "resolution": int(args.resolution),
            "transectSamples": int(args.transect_samples),
        },
        "proofSvg": str(svg_path.relative_to(ROOT).as_posix()),
        "terrainMutation": False,
        "terrainSourceContract": "Sample BuildWorldMacroGeologyPreview.evaluate_height only; water is resolved as a movable Crest/root level above terrain.",
        "runtimeOwnerContract": {
            "authoring": "WorldWaterLevelCalibrationAuthoring",
            "surfaceOwner": "Crest.OceanRenderer.Root transform Y",
            "runtimeReader": "Crest4KinematicsAdapter.SeaLevel -> OceanKinematicsRuntimeService.ActiveProvider",
            "visualReader": "HectonUnderwaterVisuals.ResolveWaterLevel",
        },
        "failureCoverage": [
            "missing calibration json falls back to explicit/default waterY",
            "bad/empty calibration levels fall back without mutating terrain",
            "selected points report coordinates/depth so stale water-level data is visible",
            "proof uses seed/world coords and can be regenerated after service replacement or calibration changes",
        ],
        "calibrationSummary": {
            "artifactVersion": calibration_data.get("artifactVersion") if calibration_data else None,
            "sampleExtentMeters": calibration_data.get("sampleExtentMeters") if calibration_data else None,
            "bestLevel0": (calibration_data.get("bestLevels") or [{}])[0] if calibration_data else None,
            "strictCandidate0": (calibration_data.get("strictCandidateLevels") or [{}])[0] if calibration_data else None,
        },
        "points": [
            {
                "key": point.target.key,
                "title": point.target.title,
                "targetDepthMeters": point.target.target_depth_m,
                "sampledDepthMeters": round(point.sample.depth, 3),
                "heightMeters": round(point.sample.height, 3),
                "worldX": round(point.sample.x, 3),
                "worldZ": round(point.sample.z, 3),
                "slope01": round(point.sample.slope01, 5),
                "exposedNeighbor": point.sample.exposed_neighbor,
                "score": round(point.score, 5),
                "transect": transects[point.target.key],
            }
            for point in selected
        ],
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--seed", type=int, default=880031)
    parser.add_argument("--runtime-seed", type=int, default=0)
    parser.add_argument("--macro-extent-m", type=float, default=MIN_EXTENT_METERS)
    parser.add_argument("--scan-extent-m", type=float, default=30_000.0)
    parser.add_argument("--origin-x-m", type=float, default=0.0)
    parser.add_argument("--origin-z-m", type=float, default=0.0)
    parser.add_argument("--chunk-m", type=float, default=512.0)
    parser.add_argument("--resolution", type=int, default=128)
    parser.add_argument("--transect-samples", type=int, default=96)
    parser.add_argument("--water-level-m", type=float, default=None)
    parser.add_argument("--calibration", default=str(DEFAULT_CALIBRATION.relative_to(ROOT).as_posix()))
    parser.add_argument("--output-prefix", default="")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    args.resolution = max(64, min(384, int(args.resolution)))
    args.transect_samples = max(32, min(256, int(args.transect_samples)))
    args.scan_extent_m = max(1000.0, float(args.scan_extent_m))
    args.macro_extent_m = max(MIN_EXTENT_METERS, float(args.macro_extent_m))

    calibration_path = resolve_calibration_arg(args.calibration)
    water_level_y, water_level_source, calibration_data = choose_water_level(calibration_path, args.water_level_m)
    params = make_params(args.seed, args.runtime_seed, args.macro_extent_m, args.chunk_m)

    grid = sample_grid(
        params,
        water_level_y,
        float(args.origin_x_m),
        float(args.origin_z_m),
        float(args.scan_extent_m),
        int(args.resolution),
    )
    selected = select_points(params, water_level_y, grid)
    if len(selected) != len(TARGETS):
        raise RuntimeError(f"expected {len(TARGETS)} proof points, selected {len(selected)}")

    transects = {
        point.target.key: build_transect(params, point, water_level_y, int(args.transect_samples)) for point in selected
    }

    prefix = output_prefix(args, water_level_y)
    prefix.parent.mkdir(parents=True, exist_ok=True)
    svg_path = prefix.with_suffix(".svg")
    png_path = prefix.with_suffix(".png")
    json_path = prefix.with_suffix(".json")
    svg = render_svg(selected, transects, water_level_y, water_level_source, float(args.scan_extent_m), int(args.resolution))
    svg_path.write_text(svg, encoding="utf-8", newline="\n")
    png_width, png_height, pixels = render_png_pixels(selected, transects)
    write_png_rgb(png_path, png_width, png_height, pixels)
    manifest = build_manifest(args, calibration_data, calibration_path, water_level_y, water_level_source, selected, transects, svg_path)
    manifest["proofPng"] = str(png_path.relative_to(ROOT).as_posix())
    json_path.write_text(json.dumps(manifest, indent=2, sort_keys=True), encoding="utf-8", newline="\n")
    print(
        json.dumps(
            {
                "status": "ok",
                "svg": str(svg_path.relative_to(ROOT).as_posix()),
                "png": str(png_path.relative_to(ROOT).as_posix()),
                "json": str(json_path.relative_to(ROOT).as_posix()),
                "waterLevelY": round(water_level_y, 3),
                "points": [
                    {
                        "key": point.target.key,
                        "depth": round(point.sample.depth, 2),
                        "x": round(point.sample.x, 1),
                        "z": round(point.sample.z, 1),
                    }
                    for point in selected
                ],
            },
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
