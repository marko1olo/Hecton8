#!/usr/bin/env python3
"""Calibrate water level against macro geology height fields.

This is a design/validation tool, not the runtime water system. It samples the
deterministic macro geology source and reports island/shallow suitability for a
30 km proof window or smaller nested windows.
"""

from __future__ import annotations

import argparse
import json
import math
from collections import deque
from pathlib import Path

from BuildWorldMacroGeologyPreview import ARTIFACT_VERSION, MIN_EXTENT_METERS, Params, combine_world_seed, evaluate_height

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_OUTPUT = ROOT / "Docs" / "GeneratedAssets" / "Terrain" / "MacroGeology"


def make_params(seed: int, runtime_seed: int, macro_extent: float, chunk: float) -> Params:
    return Params(
        seed=combine_world_seed(seed, runtime_seed),
        extent=max(MIN_EXTENT_METERS, float(macro_extent)),
        chunk=max(64.0, float(chunk)),
        water_y=0.0,
        shelf_depth=90.0,
        abyss_depth=2950.0,
        hadal_depth=4600.0,
        shelf_break_width=5200.0,
        ridge_height=1550.0,
        ridge_width=2350.0,
        trench_depth=900.0,
        trench_width=2200.0,
        basin_depth=620.0,
        detail_probe=120.0,
    )


def connected_components(mask: list[bool], resolution: int) -> list[int]:
    visited = [False] * len(mask)
    sizes: list[int] = []
    for start, active in enumerate(mask):
        if not active or visited[start]:
            continue
        visited[start] = True
        queue: deque[int] = deque([start])
        size = 0
        while queue:
            index = queue.popleft()
            size += 1
            y = index // resolution
            x = index - y * resolution
            for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                if nx < 0 or ny < 0 or nx >= resolution or ny >= resolution:
                    continue
                ni = ny * resolution + nx
                if mask[ni] and not visited[ni]:
                    visited[ni] = True
                    queue.append(ni)
        sizes.append(size)
    sizes.sort(reverse=True)
    return sizes


def sample_heights(params: Params, origin_x: float, origin_z: float, sample_extent: float, resolution: int) -> list[float]:
    half = sample_extent * 0.5
    step = sample_extent / max(1, resolution - 1)
    heights: list[float] = []
    for y in range(resolution):
        z = origin_z - half + y * step
        for x_index in range(resolution):
            x = origin_x - half + x_index * step
            height, _ = evaluate_height(x, z, params)
            heights.append(height)
    return heights


def evaluate_level(
    heights: list[float],
    resolution: int,
    meters_per_pixel: float,
    water_level: float,
    target_max_island_km2: float,
    hard_max_island_km2: float,
) -> dict:
    exposed_mask = [height >= water_level for height in heights]
    shallow_50 = 0
    shallow_100 = 0
    shelf_500 = 0
    steep_shore = 0
    shore = 0
    for index, height in enumerate(heights):
        depth = water_level - height
        if 0.0 < depth <= 50.0:
            shallow_50 += 1
        if 0.0 < depth <= 100.0:
            shallow_100 += 1
        if 0.0 < depth <= 500.0:
            shelf_500 += 1
        if not exposed_mask[index]:
            continue
        y = index // resolution
        x = index - y * resolution
        local_shore = False
        local_drop = 0.0
        for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if nx < 0 or ny < 0 or nx >= resolution or ny >= resolution:
                continue
            ni = ny * resolution + nx
            if not exposed_mask[ni]:
                local_shore = True
                local_drop = max(local_drop, height - heights[ni])
        if local_shore:
            shore += 1
            if local_drop > 90.0:
                steep_shore += 1
    island_pixels = connected_components(exposed_mask, resolution)
    pixel_area_km2 = (meters_per_pixel * meters_per_pixel) / 1_000_000.0
    island_areas = [round(size * pixel_area_km2, 4) for size in island_pixels]
    medium_islands = sum(1 for area in island_areas if 0.01 <= area <= 1.0)
    tiny_islands = sum(1 for area in island_areas if 0.001 <= area < 0.01)
    oversized_islands = sum(1 for area in island_areas if area > 1.0)
    hard_oversized_islands = sum(1 for area in island_areas if area > hard_max_island_km2)
    largest_island = island_areas[0] if island_areas else 0.0
    total = len(heights)
    exposed_percent = sum(1 for active in exposed_mask if active) * 100.0 / max(1, total)
    shallow_50_percent = shallow_50 * 100.0 / max(1, total)
    shallow_100_percent = shallow_100 * 100.0 / max(1, total)
    shelf_500_percent = shelf_500 * 100.0 / max(1, total)
    steep_shore_percent = steep_shore * 100.0 / max(1, shore)
    score = (
        min(16, medium_islands) * 1.4
        + min(24, tiny_islands) * 0.25
        + min(12.0, shallow_50_percent) * 1.8
        + min(22.0, shallow_100_percent) * 0.7
        + min(38.0, shelf_500_percent) * 0.18
        - oversized_islands * 4.0
        - hard_oversized_islands * 8.0
        - max(0.0, largest_island - target_max_island_km2) * 4.5
        - max(0.0, exposed_percent - 8.0) * 0.9
        - max(0.0, steep_shore_percent - 58.0) * 0.12
    )
    return {
        "waterLevelMeters": round(water_level, 2),
        "score": round(score, 4),
        "exposedPercent": round(exposed_percent, 4),
        "shallowPercent0To50m": round(shallow_50_percent, 4),
        "shallowPercent0To100m": round(shallow_100_percent, 4),
        "shelfPercent0To500m": round(shelf_500_percent, 4),
        "islandCount": len(island_areas),
        "tinyIslandCount0p001To0p01Km2": tiny_islands,
        "mediumIslandCount0p01To1Km2": medium_islands,
        "oversizedIslandCountOver1Km2": oversized_islands,
        "hardOversizedIslandCount": hard_oversized_islands,
        "targetMaxIslandKm2": target_max_island_km2,
        "hardMaxIslandKm2": hard_max_island_km2,
        "largestIslandKm2": largest_island,
        "topIslandAreasKm2": island_areas[:12],
        "steepShorePercent": round(steep_shore_percent, 4),
        "passesIslandSizeTarget": largest_island <= target_max_island_km2,
        "passesHardIslandLimit": largest_island <= hard_max_island_km2,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--seed", type=int, default=880031)
    parser.add_argument("--runtime-seed", type=int, default=0)
    parser.add_argument("--extent-m", type=float, default=30_000.0)
    parser.add_argument("--macro-extent-m", type=float, default=30_000.0)
    parser.add_argument("--origin-x-m", type=float, default=0.0)
    parser.add_argument("--origin-z-m", type=float, default=0.0)
    parser.add_argument("--chunk-m", type=float, default=512.0)
    parser.add_argument("--resolution", type=int, default=192)
    parser.add_argument("--level-min-m", type=float, default=-120.0)
    parser.add_argument("--level-max-m", type=float, default=180.0)
    parser.add_argument("--level-step-m", type=float, default=10.0)
    parser.add_argument("--target-max-island-km2", type=float, default=1.0)
    parser.add_argument("--hard-max-island-km2", type=float, default=2.0)
    parser.add_argument("--output", default="")
    args = parser.parse_args()

    sample_extent = max(100.0, float(args.extent_m))
    params = make_params(args.seed, args.runtime_seed, args.macro_extent_m, args.chunk_m)
    resolution = max(64, min(384, int(args.resolution)))
    heights = sample_heights(params, args.origin_x_m, args.origin_z_m, sample_extent, resolution)
    meters_per_pixel = sample_extent / max(1, resolution - 1)
    levels = []
    level = min(args.level_min_m, args.level_max_m)
    max_level = max(args.level_min_m, args.level_max_m)
    step = max(1.0, abs(args.level_step_m))
    while level <= max_level + 0.0001:
        levels.append(
            evaluate_level(
                heights,
                resolution,
                meters_per_pixel,
                level,
                max(0.01, float(args.target_max_island_km2)),
                max(0.01, float(args.hard_max_island_km2)),
            )
        )
        level += step
    levels.sort(key=lambda item: float(item["score"]), reverse=True)
    strict_levels = [
        item
        for item in levels
        if item["passesHardIslandLimit"]
        and float(item["shallowPercent0To50m"]) >= 2.0
        and float(item["shallowPercent0To100m"]) >= 4.0
        and int(item["mediumIslandCount0p01To1Km2"]) >= 2
    ]
    output = {
        "artifact": "WorldWaterLevelCalibration",
        "artifactVersion": ARTIFACT_VERSION,
        "authoringSeed": args.seed,
        "runtimeSeed": args.runtime_seed,
        "combinedSeed": params.seed,
        "originMeters": {"x": args.origin_x_m, "z": args.origin_z_m},
        "sampleExtentMeters": sample_extent,
        "macroExtentMeters": params.extent,
        "resolution": resolution,
        "metersPerPixel": meters_per_pixel,
        "designTargets": {
            "islandArea": "prefer many small islands below 1km2 in spawn windows",
            "shallows": "keep meaningful 0-50m and 0-100m bands around islands",
            "shoreSlope": "mix smooth, terraced, and steep shore approaches; avoid all-cliff islands",
            "terrainSource": "water level calibration must not alter macro geology height",
            "targetMaxIslandKm2": max(0.01, float(args.target_max_island_km2)),
            "hardMaxIslandKm2": max(0.01, float(args.hard_max_island_km2)),
        },
        "runtimeDetailContract": {
            "source": "Assets/_Project/Scripts/World/WorldTerrainDetailContracts.cs",
            "chunkSizeMeters": 512,
            "nearPlayable": {"heightResolution": 513, "samplePitchMeters": 1, "maxDistanceMeters": 768},
            "midTraversal": {"heightResolution": 257, "samplePitchMeters": 2, "maxDistanceMeters": 2048},
            "farSilhouette": {"heightResolution": 129, "samplePitchMeters": 4, "maxDistanceMeters": 6144},
            "distantHlod": {"heightResolution": 65, "samplePitchMeters": 8, "maxDistanceMeters": 24576},
            "nestedProofWindowsMeters": [10000, 1000, 512, 100],
        },
        "strictCandidateLevels": strict_levels[:12],
        "bestLevels": levels[:12],
        "allLevels": levels,
    }
    if args.output:
        path = Path(args.output)
        if not path.is_absolute():
            path = ROOT / path
    else:
        suffix = ""
        if abs(args.origin_x_m) > 1.0e-5 or abs(args.origin_z_m) > 1.0e-5:
            suffix = f"_OriginX{int(args.origin_x_m)}_Z{int(args.origin_z_m)}"
        path = DEFAULT_OUTPUT / f"WorldWaterLevelCalibration_Extent{int(sample_extent)}m_Res{resolution}{suffix}.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(output, indent=2, sort_keys=True), encoding="utf-8")
    print(
        json.dumps(
            {
                "status": "ok",
                "output": str(path.relative_to(ROOT).as_posix()),
                "strictCandidateLevels": strict_levels[:5],
                "bestLevels": levels[:5],
            },
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
