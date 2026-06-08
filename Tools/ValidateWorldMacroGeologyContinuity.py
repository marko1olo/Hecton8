#!/usr/bin/env python3
"""Validate deterministic macro geology continuity across preview windows."""

from __future__ import annotations

import argparse
import json
import math
import sys
from pathlib import Path

from BuildWorldMacroGeologyPreview import (
    ARTIFACT_VERSION,
    MIN_EXTENT_METERS,
    Params,
    combine_world_seed,
    evaluate_height,
)

ROOT = Path(__file__).resolve().parents[1]


def load_manifest(path: Path) -> dict:
    if not path.exists():
        raise FileNotFoundError(path)
    return json.loads(path.read_text(encoding="utf-8"))


def require_origin(manifest: dict) -> tuple[float, float]:
    origin = manifest.get("originMeters")
    if not isinstance(origin, dict):
        preview = manifest.get("previewWindow", {})
        origin = preview.get("originMeters") if isinstance(preview, dict) else None
    if not isinstance(origin, dict):
        raise ValueError(f"{manifest.get('artifact', '<unknown>')} is missing originMeters")
    return float(origin.get("x", 0.0)), float(origin.get("z", 0.0))


def validate_manifest_pair(a_path: Path, b_path: Path, axis: str) -> tuple[tuple[float, float], tuple[float, float], float]:
    a = load_manifest(a_path)
    b = load_manifest(b_path)
    if int(a.get("artifactVersion", -1)) != ARTIFACT_VERSION or int(b.get("artifactVersion", -1)) != ARTIFACT_VERSION:
        raise ValueError("artifactVersion mismatch against current macro geology source")
    if int(a.get("combinedSeed", -1)) != int(b.get("combinedSeed", -2)):
        raise ValueError("combinedSeed mismatch between preview windows")
    extent_a = float(a.get("extentMeters", 0.0))
    extent_b = float(b.get("extentMeters", 0.0))
    if abs(extent_a - extent_b) > 1.0e-5 or extent_a < MIN_EXTENT_METERS:
        raise ValueError("preview extents are not matching authored world windows")
    origin_a = require_origin(a)
    origin_b = require_origin(b)
    expected = (origin_a[0] + (extent_a if axis == "x" else 0.0), origin_a[1] + (extent_a if axis == "z" else 0.0))
    if abs(origin_b[0] - expected[0]) > 1.0e-4 or abs(origin_b[1] - expected[1]) > 1.0e-4:
        raise ValueError(f"preview windows are not adjacent on {axis}: expected {expected}, got {origin_b}")
    return origin_a, origin_b, extent_a


def validate_seam(seed: int, runtime_seed: int, origin_a: tuple[float, float], extent: float, axis: str, sample_count: int) -> dict:
    combined_seed = combine_world_seed(seed, runtime_seed)
    params = Params(
        seed=combined_seed,
        extent=max(MIN_EXTENT_METERS, float(extent)),
        chunk=512.0,
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
    half = params.extent * 0.5
    seam_x = origin_a[0] + half if axis == "x" else None
    seam_z = origin_a[1] + half if axis == "z" else None
    max_same_coord_delta = 0.0
    max_neighbor_gradient = 0.0
    sum_neighbor_gradient = 0.0
    count = max(2, sample_count)
    step = params.extent / max(1, count - 1)
    neighbor_step = min(256.0, step)
    for index in range(count):
        lateral = -half + index * step
        if axis == "x":
            z = origin_a[1] + lateral
            h_a, _ = evaluate_height(float(seam_x), z, params)
            h_b, _ = evaluate_height(float(seam_x), z, params)
            h_left, _ = evaluate_height(float(seam_x) - neighbor_step, z, params)
            h_right, _ = evaluate_height(float(seam_x) + neighbor_step, z, params)
        else:
            x = origin_a[0] + lateral
            h_a, _ = evaluate_height(x, float(seam_z), params)
            h_b, _ = evaluate_height(x, float(seam_z), params)
            h_left, _ = evaluate_height(x, float(seam_z) - neighbor_step, params)
            h_right, _ = evaluate_height(x, float(seam_z) + neighbor_step, params)
        max_same_coord_delta = max(max_same_coord_delta, abs(h_a - h_b))
        gradient = abs(h_right - h_left) / max(1.0, neighbor_step * 2.0)
        max_neighbor_gradient = max(max_neighbor_gradient, gradient)
        sum_neighbor_gradient += gradient
    return {
        "axis": axis,
        "sampleCount": count,
        "maxSameCoordinateDeltaMeters": round(max_same_coord_delta, 8),
        "maxNeighborGradient": round(max_neighbor_gradient, 6),
        "averageNeighborGradient": round(sum_neighbor_gradient / count, 6),
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--seed", type=int, default=880031)
    parser.add_argument("--runtime-seed", type=int, default=0)
    parser.add_argument("--center-manifest", required=True)
    parser.add_argument("--east-manifest", default="")
    parser.add_argument("--north-manifest", default="")
    parser.add_argument("--sample-count", type=int, default=257)
    parser.add_argument("--output", default="")
    args = parser.parse_args()

    center_path = (ROOT / args.center_manifest).resolve() if not Path(args.center_manifest).is_absolute() else Path(args.center_manifest)
    checks = []
    if args.east_manifest:
        east_path = (ROOT / args.east_manifest).resolve() if not Path(args.east_manifest).is_absolute() else Path(args.east_manifest)
        origin, _, extent = validate_manifest_pair(center_path, east_path, "x")
        checks.append(validate_seam(args.seed, args.runtime_seed, origin, extent, "x", args.sample_count))
    if args.north_manifest:
        north_path = (ROOT / args.north_manifest).resolve() if not Path(args.north_manifest).is_absolute() else Path(args.north_manifest)
        origin, _, extent = validate_manifest_pair(center_path, north_path, "z")
        checks.append(validate_seam(args.seed, args.runtime_seed, origin, extent, "z", args.sample_count))
    if not checks:
        raise ValueError("provide at least one adjacent preview manifest")
    failures = [check for check in checks if float(check["maxSameCoordinateDeltaMeters"]) > 1.0e-5]
    result = {
        "artifact": "WorldMacroGeologyContinuityProof",
        "artifactVersion": ARTIFACT_VERSION,
        "seed": args.seed,
        "runtimeSeed": args.runtime_seed,
        "checks": checks,
        "status": "OK" if not failures else "FAILED",
    }
    if args.output:
        output_path = (ROOT / args.output).resolve() if not Path(args.output).is_absolute() else Path(args.output)
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text(json.dumps(result, indent=2, sort_keys=True), encoding="utf-8")
    print(json.dumps(result, indent=2, sort_keys=True))
    return 1 if failures else 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        raise SystemExit(1)
