#!/usr/bin/env python3
"""Scan nested spawn windows for water level and island/shallow suitability."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from BuildWorldMacroGeologyPreview import ARTIFACT_VERSION, MIN_EXTENT_METERS, Params, combine_world_seed, evaluate_height
from CalibrateWorldWaterLevel import evaluate_level

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_OUTPUT = ROOT / "Docs" / "GeneratedAssets" / "Terrain" / "MacroGeology" / "WorldWaterLevelSpawnWindowScan.json"


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


def sample_parent(params: Params, origin_x: float, origin_z: float, parent_extent: float, resolution: int) -> list[float]:
    half = parent_extent * 0.5
    step = parent_extent / max(1, resolution - 1)
    heights: list[float] = []
    for y in range(resolution):
        z = origin_z - half + y * step
        for x_index in range(resolution):
            x = origin_x - half + x_index * step
            height, _ = evaluate_height(x, z, params)
            heights.append(height)
    return heights


def crop_window(
    parent_heights: list[float],
    parent_resolution: int,
    parent_origin_x: float,
    parent_origin_z: float,
    parent_extent: float,
    window_origin_x: float,
    window_origin_z: float,
    window_extent: float,
) -> tuple[list[float], int, float]:
    parent_step = parent_extent / max(1, parent_resolution - 1)
    parent_min_x = parent_origin_x - parent_extent * 0.5
    parent_min_z = parent_origin_z - parent_extent * 0.5
    min_x = window_origin_x - window_extent * 0.5
    min_z = window_origin_z - window_extent * 0.5
    max_x = window_origin_x + window_extent * 0.5
    max_z = window_origin_z + window_extent * 0.5
    ix0 = max(0, int(round((min_x - parent_min_x) / parent_step)))
    iz0 = max(0, int(round((min_z - parent_min_z) / parent_step)))
    ix1 = min(parent_resolution - 1, int(round((max_x - parent_min_x) / parent_step)))
    iz1 = min(parent_resolution - 1, int(round((max_z - parent_min_z) / parent_step)))
    width = ix1 - ix0 + 1
    height = iz1 - iz0 + 1
    side = min(width, height)
    if side < 8:
        return [], 0, parent_step
    values: list[float] = []
    for y in range(side):
        row = iz0 + y
        start = row * parent_resolution + ix0
        values.extend(parent_heights[start : start + side])
    return values, side, parent_step


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--seed", type=int, default=880031)
    parser.add_argument("--runtime-seed", type=int, default=0)
    parser.add_argument("--parent-extent-m", type=float, default=30_000.0)
    parser.add_argument("--window-extent-m", type=float, default=10_000.0)
    parser.add_argument("--macro-extent-m", type=float, default=30_000.0)
    parser.add_argument("--origin-x-m", type=float, default=0.0)
    parser.add_argument("--origin-z-m", type=float, default=0.0)
    parser.add_argument("--chunk-m", type=float, default=512.0)
    parser.add_argument("--resolution", type=int, default=192)
    parser.add_argument("--grid-axis", type=int, default=5)
    parser.add_argument("--level-min-m", type=float, default=250.0)
    parser.add_argument("--level-max-m", type=float, default=620.0)
    parser.add_argument("--level-step-m", type=float, default=10.0)
    parser.add_argument("--target-max-island-km2", type=float, default=1.0)
    parser.add_argument("--hard-max-island-km2", type=float, default=2.0)
    parser.add_argument("--output", default=str(DEFAULT_OUTPUT))
    args = parser.parse_args()

    parent_extent = max(args.window_extent_m, args.parent_extent_m)
    window_extent = max(500.0, args.window_extent_m)
    params = make_params(args.seed, args.runtime_seed, args.macro_extent_m, args.chunk_m)
    resolution = max(96, min(256, int(args.resolution)))
    heights = sample_parent(params, args.origin_x_m, args.origin_z_m, parent_extent, resolution)
    grid_axis = max(1, int(args.grid_axis))
    spacing = 0.0 if grid_axis == 1 else (parent_extent - window_extent) / max(1, grid_axis - 1)
    parent_min_x = args.origin_x_m - parent_extent * 0.5 + window_extent * 0.5
    parent_min_z = args.origin_z_m - parent_extent * 0.5 + window_extent * 0.5
    step = max(1.0, abs(args.level_step_m))
    results = []
    for gz in range(grid_axis):
        for gx in range(grid_axis):
            window_x = parent_min_x + gx * spacing
            window_z = parent_min_z + gz * spacing
            window_heights, window_resolution, meters_per_pixel = crop_window(
                heights,
                resolution,
                args.origin_x_m,
                args.origin_z_m,
                parent_extent,
                window_x,
                window_z,
                window_extent,
            )
            if not window_heights:
                continue
            level = min(args.level_min_m, args.level_max_m)
            max_level = max(args.level_min_m, args.level_max_m)
            best = None
            strict_best = None
            while level <= max_level + 0.0001:
                item = evaluate_level(
                    window_heights,
                    window_resolution,
                    meters_per_pixel,
                    level,
                    args.target_max_island_km2,
                    args.hard_max_island_km2,
                )
                if best is None or float(item["score"]) > float(best["score"]):
                    best = item
                if (
                    item["passesHardIslandLimit"]
                    and float(item["shallowPercent0To50m"]) >= 2.0
                    and float(item["shallowPercent0To100m"]) >= 4.0
                    and int(item["mediumIslandCount0p01To1Km2"]) >= 2
                    and (strict_best is None or float(item["score"]) > float(strict_best["score"]))
                ):
                    strict_best = item
                level += step
            chosen = strict_best if strict_best is not None else best
            if chosen is None:
                continue
            results.append(
                {
                    "originMeters": {"x": round(window_x, 2), "z": round(window_z, 2)},
                    "windowExtentMeters": window_extent,
                    "windowResolution": window_resolution,
                    "hasStrictCandidate": strict_best is not None,
                    "chosenLevel": chosen,
                    "bestLevel": best,
                    "strictBestLevel": strict_best,
                }
            )
    results.sort(key=lambda item: (not item["hasStrictCandidate"], -float(item["chosenLevel"]["score"])))
    output = {
        "artifact": "WorldWaterLevelSpawnWindowScan",
        "artifactVersion": ARTIFACT_VERSION,
        "authoringSeed": args.seed,
        "runtimeSeed": args.runtime_seed,
        "combinedSeed": params.seed,
        "parentOriginMeters": {"x": args.origin_x_m, "z": args.origin_z_m},
        "parentExtentMeters": parent_extent,
        "windowExtentMeters": window_extent,
        "parentResolution": resolution,
        "designTargets": {
            "maxSingleIslandKm2Target": args.target_max_island_km2,
            "maxSingleIslandKm2HardLimit": args.hard_max_island_km2,
            "primaryShallowBandMeters": "0-50",
            "transitionShallowBandMeters": "0-100",
            "usage": "choose spawn/local proof windows before nested meso preview generation",
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
        "rankedWindows": results,
    }
    path = Path(args.output)
    if not path.is_absolute():
        path = ROOT / path
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(output, indent=2, sort_keys=True), encoding="utf-8")
    print(json.dumps({"status": "ok", "output": str(path.relative_to(ROOT).as_posix()), "topWindows": results[:5]}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
