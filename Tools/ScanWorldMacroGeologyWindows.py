#!/usr/bin/env python3
"""Scan macro geology preview windows for large-scale world variety."""

from __future__ import annotations

import argparse
import json
import math

from BuildWorldMacroGeologyPreview import (
    ARTIFACT_VERSION,
    DEPTH_BANDS_METERS,
    DEPTH_STRATA,
    MIN_EXTENT_METERS,
    Params,
    combine_world_seed,
    evaluate_height,
    resolve_zone,
)

ZONE_NAMES = {
    0: "Unknown",
    1: "PhoticShelf",
    2: "ShelfBreak",
    3: "FaultRidge",
    4: "BrineTrench",
    5: "AbyssalPlain",
    6: "SedimentFan",
    7: "ColdSeepField",
    8: "HadalBasin",
}


def classify_stratum(depth: float) -> str:
    for name, low, high in DEPTH_STRATA:
        if low <= depth < high:
            return str(name)
    return "Hadal"


def make_params(seed: int, runtime_seed: int, extent: float, chunk: float) -> Params:
    return Params(
        seed=combine_world_seed(seed, runtime_seed),
        extent=max(MIN_EXTENT_METERS, float(extent)),
        chunk=max(128.0, float(chunk)),
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


def score_window(origin_x: float, origin_z: float, params: Params, resolution: int) -> dict:
    half = params.extent * 0.5
    step = params.extent / max(1, resolution - 1)
    zone_counts = {name: 0 for name in ZONE_NAMES.values()}
    strata_counts: dict[str, int] = {}
    band_counts = {name: 0 for name, _, _ in DEPTH_BANDS_METERS}
    min_height = 1.0e9
    max_height = -1.0e9
    sum_slope = 0.0
    previous_row: list[float] | None = None
    current_row: list[float] = [0.0] * resolution
    heights: list[list[float]] = []
    for y in range(resolution):
        z = origin_z - half + y * step
        row: list[float] = []
        for x_index in range(resolution):
            x = origin_x - half + x_index * step
            height, masks = evaluate_height(x, z, params)
            depth = max(0.0, params.water_y - height)
            min_height = min(min_height, height)
            max_height = max(max_height, height)
            zone = resolve_zone(depth, masks, sediment=0.45, seep=max(masks.fault * 0.45, masks.basin * 0.25), slope01=0.35)
            zone_counts[ZONE_NAMES[zone]] += 1
            stratum = classify_stratum(depth)
            strata_counts[stratum] = strata_counts.get(stratum, 0) + 1
            for band_name, band_low, band_high in DEPTH_BANDS_METERS:
                if band_low <= depth < band_high:
                    band_counts[band_name] += 1
                    break
            row.append(height)
        heights.append(row)
        if previous_row is not None:
            for x_index in range(1, resolution):
                dx = row[x_index] - row[x_index - 1]
                dz = row[x_index] - previous_row[x_index]
                sum_slope += math.sqrt(dx * dx + dz * dz) / max(1.0, step)
        previous_row = row
    total = resolution * resolution
    photic = zone_counts["PhoticShelf"] + zone_counts["ShelfBreak"]
    shallow = band_counts["0-500"]
    deep = zone_counts["AbyssalPlain"] + zone_counts["HadalBasin"] + zone_counts["BrineTrench"]
    ridge = zone_counts["FaultRidge"]
    zone_diversity = sum(1 for value in zone_counts.values() if value > total * 0.01)
    height_range = max_height - min_height
    score = (
        zone_diversity * 1.4
        + min(0.32, photic / total) * 7.0
        + min(0.42, shallow / total) * 5.5
        + min(0.45, ridge / total) * 4.0
        + min(0.70, deep / total) * 1.5
        + min(1.0, height_range / 3600.0) * 2.4
        + min(1.0, sum_slope / max(1, (resolution - 1) * (resolution - 1)) * 2.5) * 1.2
    )
    return {
        "originMeters": {"x": round(origin_x, 2), "z": round(origin_z, 2)},
        "score": round(score, 4),
        "heightRangeMeters": round(height_range, 2),
        "zoneDiversity": zone_diversity,
        "zoneCounts": zone_counts,
        "depthBandCountsMeters": band_counts,
        "depthStrataCounts": strata_counts,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--seed", type=int, default=880031)
    parser.add_argument("--runtime-seed", type=int, default=0)
    parser.add_argument("--extent-m", type=float, default=30_000.0)
    parser.add_argument("--chunk-m", type=float, default=512.0)
    parser.add_argument("--resolution", type=int, default=48)
    parser.add_argument("--grid-radius", type=int, default=2)
    parser.add_argument("--output", default="")
    args = parser.parse_args()

    params = make_params(args.seed, args.runtime_seed, args.extent_m, args.chunk_m)
    resolution = max(24, min(96, int(args.resolution)))
    results = []
    for gz in range(-args.grid_radius, args.grid_radius + 1):
        for gx in range(-args.grid_radius, args.grid_radius + 1):
            results.append(score_window(gx * params.extent, gz * params.extent, params, resolution))
    results.sort(key=lambda item: float(item["score"]), reverse=True)
    output = {
        "artifact": "WorldMacroGeologyWindowScan",
        "artifactVersion": ARTIFACT_VERSION,
        "seed": args.seed,
        "runtimeSeed": args.runtime_seed,
        "extentMeters": params.extent,
        "resolution": resolution,
        "bestWindows": results[: min(12, len(results))],
        "sampledWindowCount": len(results),
    }
    if args.output:
        from pathlib import Path

        path = Path(args.output)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(output, indent=2, sort_keys=True), encoding="utf-8")
    print(json.dumps(output, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
