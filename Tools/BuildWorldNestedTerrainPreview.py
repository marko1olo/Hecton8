#!/usr/bin/env python3
"""Build nested terrain detail previews from the macro geology source.

The macro generator owns kilometer-scale form. This tool previews smaller proof
windows in the same world coordinates and adds deterministic meso controls for
terraces, slump scars, tributaries, talus, rubble shelves, and shallow roughness.
It is a proof/bake design tool; runtime must mirror accepted detail math in C# jobs.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path

from BuildWorldMacroGeologyPreview import (
    ARTIFACT_VERSION,
    MIN_EXTENT_METERS,
    Params,
    blend,
    clamp01,
    combine_world_seed,
    evaluate_height,
    fractal_noise,
    gray,
    heat,
    shade_hillshade,
    shade_local_relief,
    shade_raw_elevation,
    shade_terrain_relief_proof,
    smoothstep,
    write_png_rgb,
)

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_OUTPUT = ROOT / "Docs" / "GeneratedAssets" / "Terrain" / "NestedDetail"
DETAIL_ARTIFACT_VERSION = 1


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


def shade_delta(delta: float, max_abs: float) -> tuple[int, int, int]:
    t = clamp01((delta / max(0.001, max_abs) + 1.0) * 0.5)
    if t < 0.5:
        return blend((31, 56, 84), (93, 103, 94), t * 2.0)
    return blend((93, 103, 94), (201, 164, 98), (t - 0.5) * 2.0)


def sample_fields(params: Params, origin_x: float, origin_z: float, extent: float, resolution: int) -> dict:
    half = extent * 0.5
    step = extent / max(1, resolution - 1)
    base_heights: list[float] = []
    masks = []
    for y in range(resolution):
        z = origin_z - half + y * step
        for x_index in range(resolution):
            x = origin_x - half + x_index * step
            height, mask = evaluate_height(x, z, params)
            base_heights.append(height)
            masks.append(mask)
    return {"heights": base_heights, "masks": masks, "step": step}


def derive_slopes_curvature(heights: list[float], resolution: int, step: float) -> tuple[list[float], list[float], list[float]]:
    slopes = [0.0] * len(heights)
    curvature = [0.0] * len(heights)
    erosion = [0.0] * len(heights)
    max_slope = 0.0001
    max_curv = 0.0001
    for y in range(resolution):
        for x in range(resolution):
            i = y * resolution + x
            west = heights[y * resolution + max(0, x - 1)]
            east = heights[y * resolution + min(resolution - 1, x + 1)]
            south = heights[max(0, y - 1) * resolution + x]
            north = heights[min(resolution - 1, y + 1) * resolution + x]
            center = heights[i]
            dx = (east - west) / max(1.0, step * 2.0)
            dz = (north - south) / max(1.0, step * 2.0)
            slope = math.hypot(dx, dz)
            curv = abs((west + east + south + north - center * 4.0) / max(1.0, step * step))
            slopes[i] = slope
            curvature[i] = curv
            max_slope = max(max_slope, slope)
            max_curv = max(max_curv, curv)
    for i in range(len(heights)):
        slopes[i] = clamp01(slopes[i] / max_slope)
        curvature[i] = clamp01(curvature[i] / max_curv)
        erosion[i] = clamp01(slopes[i] * 0.42 + curvature[i] * 0.34)
    return slopes, curvature, erosion


def meso_delta(x: float, z: float, base_height: float, mask, slope01: float, curvature01: float, extent: float, seed: int) -> tuple[float, dict[str, float]]:
    depth = max(0.0, -base_height)
    detail_gate = clamp01((10_000.0 / max(100.0, extent)) ** 0.45)
    shelf_break = mask.shelf_break
    shelf = mask.shelf
    fault = mask.fault
    ridge = mask.ridge
    basin = mask.basin
    trench = mask.trench
    sediment = clamp01((1.0 - slope01) * 0.54 + basin * 0.36 + shelf * 0.18 - ridge * 0.24 - trench * 0.18)
    terrace = clamp01(shelf_break * 0.45 + shelf * 0.12 + curvature01 * 0.16 - trench * 0.18)
    slump = clamp01(shelf_break * 0.35 + slope01 * 0.35 + basin * 0.10 - ridge * 0.15)
    tributary = clamp01(fault * 0.22 + shelf_break * 0.24 + curvature01 * 0.30 + sediment * 0.12)
    talus = clamp01(slope01 * 0.42 + ridge * 0.24 + fault * 0.18 - basin * 0.12)
    rubble = clamp01(shelf * 0.20 + shelf_break * 0.22 + talus * 0.32 + curvature01 * 0.16)
    reef_detail = clamp01(shelf * 0.45 + (1.0 - slope01) * 0.24 - trench * 0.40)

    terrace_step = 18.0 + shelf_break * 48.0 + ridge * 24.0
    terrace_target = round(base_height / max(1.0, terrace_step)) * terrace_step
    terrace_delta = (terrace_target - base_height) * terrace * 0.18 * detail_gate

    channel_noise = fractal_noise(x * 0.0042 + 18.4, z * 0.0042 - 9.1, seed ^ 0x58B9D13D)
    channel_lines = smoothstep(0.62, 0.92, channel_noise) * tributary
    channel_delta = -channel_lines * (4.0 + 20.0 * detail_gate) * clamp01(0.40 + shelf_break + fault * 0.45)

    slump_noise = fractal_noise(x * 0.0021 - 31.7, z * 0.0021 + 7.9, seed ^ 0x711CE4A9)
    slump_lobes = smoothstep(0.55, 0.86, slump_noise) * slump
    slump_delta = -slump_lobes * (5.0 + 34.0 * detail_gate)

    talus_noise = fractal_noise(x * 0.0100 + 4.2, z * 0.0100 - 19.5, seed ^ 0xA9C3EF17)
    talus_delta = (talus_noise - 0.5) * (4.0 + 16.0 * detail_gate) * talus

    rubble_noise = fractal_noise(x * 0.0220 - 12.3, z * 0.0220 + 28.1, seed ^ 0xC361A27F)
    rubble_delta = (rubble_noise - 0.5) * (2.0 + 8.0 * detail_gate) * rubble

    reef_noise = fractal_noise(x * 0.0350 + 51.0, z * 0.0350 - 37.0, seed ^ 0x91D4C0DE)
    reef_delta = (reef_noise - 0.5) * (1.2 + 4.8 * detail_gate) * reef_detail * smoothstep(0.0, 120.0, 120.0 - depth)

    total = terrace_delta + channel_delta + slump_delta + talus_delta + rubble_delta + reef_delta
    max_delta = 70.0 if extent >= 1000.0 else 24.0 if extent >= 512.0 else 7.0
    total = max(-max_delta, min(max_delta, total))
    controls = {
        "terrace": terrace,
        "slump": slump,
        "tributary": tributary,
        "talus": talus,
        "rubble": rubble,
        "reefDetail": reef_detail,
        "sediment": sediment,
    }
    return total, controls


def build_preview(args: argparse.Namespace) -> int:
    params = make_params(args.seed, args.runtime_seed, args.macro_extent_m, args.chunk_m)
    extent = max(50.0, float(args.extent_m))
    resolution = max(64, min(768, int(args.resolution)))
    sampled = sample_fields(params, args.origin_x_m, args.origin_z_m, extent, resolution)
    base_heights = sampled["heights"]
    masks = sampled["masks"]
    step = sampled["step"]
    base_slope, base_curv, base_erosion = derive_slopes_curvature(base_heights, resolution, step)
    half = extent * 0.5
    deltas: list[float] = []
    controls_list: list[dict[str, float]] = []
    detailed_heights: list[float] = []
    for y in range(resolution):
        z = args.origin_z_m - half + y * step
        for x_index in range(resolution):
            x = args.origin_x_m - half + x_index * step
            i = y * resolution + x_index
            delta, controls = meso_delta(x, z, base_heights[i], masks[i], base_slope[i], base_curv[i], extent, params.seed)
            deltas.append(delta)
            controls_list.append(controls)
            detailed_heights.append(base_heights[i] + delta)
    detail_slope, detail_curv, detail_erosion = derive_slopes_curvature(detailed_heights, resolution, step)
    min_base = min(base_heights)
    max_base = max(base_heights)
    min_detail = min(detailed_heights)
    max_detail = max(detailed_heights)
    max_delta = max(1.0, max(abs(value) for value in deltas))
    fields = {
        "base_height": [shade_raw_elevation(value, min_base, max_base) for value in base_heights],
        "detailed_height": [shade_raw_elevation(value, min_detail, max_detail) for value in detailed_heights],
        "detail_delta": [shade_delta(value, max_delta) for value in deltas],
        "detailed_relief": [
            shade_terrain_relief_proof(detailed_heights[i], min_detail, max_detail, detail_slope[i], detail_curv[i], detail_erosion[i])
            for i in range(len(detailed_heights))
        ],
        "detailed_hillshade": [
            shade_hillshade(detailed_heights[i], min_detail, max_detail, detail_slope[i], detail_curv[i])
            for i in range(len(detailed_heights))
        ],
        "local_relief": [shade_local_relief(detail_slope[i], detail_curv[i], detail_erosion[i]) for i in range(len(detailed_heights))],
        "terrace_slump_tributary": [
            (
                int(36 + 196 * controls_list[i]["terrace"]),
                int(40 + 184 * controls_list[i]["slump"]),
                int(44 + 176 * controls_list[i]["tributary"]),
            )
            for i in range(len(detailed_heights))
        ],
        "talus_rubble_reef": [
            (
                int(42 + 174 * controls_list[i]["talus"]),
                int(40 + 180 * controls_list[i]["rubble"]),
                int(46 + 176 * controls_list[i]["reefDetail"]),
            )
            for i in range(len(detailed_heights))
        ],
        "sediment": [gray(controls["sediment"]) for controls in controls_list],
        "slope": [heat(value) for value in detail_slope],
        "curvature": [heat(value) for value in detail_curv],
        "erosion": [heat(value) for value in detail_erosion],
    }
    suffix = f"OriginX{int(round(args.origin_x_m))}_Z{int(round(args.origin_z_m))}_Extent{int(round(extent))}m_Res{resolution}"
    out_dir = Path(args.output) if args.output else DEFAULT_OUTPUT / suffix
    out_dir.mkdir(parents=True, exist_ok=True)
    files: dict[str, str] = {}
    hashes: dict[str, str] = {}
    for name, pixels in fields.items():
        path = out_dir / f"ND_{name}.png"
        write_png_rgb(path, resolution, resolution, pixels)
        files[name] = str(path.relative_to(ROOT).as_posix())
        hashes[name] = hashlib.sha256(path.read_bytes()).hexdigest()
    panel = max(192, min(384, resolution))
    columns = 4
    rows = 3
    sheet_pixels = [(0, 0, 0)] * (panel * columns * panel * rows)
    names = list(fields.keys())[: columns * rows]
    for index, name in enumerate(names):
        source = fields[name]
        ox = (index % columns) * panel
        oy = (index // columns) * panel
        for py in range(panel):
            sy = int(py * resolution / panel)
            for px in range(panel):
                sx = int(px * resolution / panel)
                sheet_pixels[(oy + py) * (panel * columns) + ox + px] = source[sy * resolution + sx]
    contact_path = out_dir / "ND_contact_sheet_4x3.png"
    write_png_rgb(contact_path, panel * columns, panel * rows, sheet_pixels)
    files["contact_sheet"] = str(contact_path.relative_to(ROOT).as_posix())
    hashes["contact_sheet"] = hashlib.sha256(contact_path.read_bytes()).hexdigest()
    manifest = {
        "artifact": "WorldNestedTerrainDetailPreview",
        "artifactVersion": DETAIL_ARTIFACT_VERSION,
        "macroArtifactVersion": ARTIFACT_VERSION,
        "authoringSeed": args.seed,
        "runtimeSeed": args.runtime_seed,
        "combinedSeed": params.seed,
        "originMeters": {"x": args.origin_x_m, "z": args.origin_z_m},
        "extentMeters": extent,
        "resolution": resolution,
        "metersPerPixel": step,
        "heightMeters": {"baseMin": min_base, "baseMax": max_base, "detailMin": min_detail, "detailMax": max_detail},
        "detailDeltaMeters": {"min": min(deltas), "max": max(deltas)},
        "runtimeContract": {
            "source": "WorldMacroGeologyFields + future WorldTerrainMesoDetailFields",
            "chunkSizeMeters": 512,
            "nearPlayableHeightResolution": 513,
            "midTraversalHeightResolution": 257,
            "microHeightPolicy": "100m proof may preview height detail, but final centimeter detail belongs to materials/decals/scatter unless accepted into a chunk-local bake",
        },
        "files": files,
        "sha256": hashes,
    }
    manifest_path = out_dir / "WorldNestedTerrainDetailPreviewManifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2, sort_keys=True), encoding="utf-8")
    print(
        json.dumps(
            {
                "status": "ok",
                "manifest": str(manifest_path.relative_to(ROOT).as_posix()),
                "contactSheet": files["contact_sheet"],
                "extentMeters": extent,
                "metersPerPixel": step,
                "detailDeltaMeters": manifest["detailDeltaMeters"],
            },
            indent=2,
        )
    )
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--seed", type=int, default=880031)
    parser.add_argument("--runtime-seed", type=int, default=0)
    parser.add_argument("--origin-x-m", type=float, default=10_000.0)
    parser.add_argument("--origin-z-m", type=float, default=-10_000.0)
    parser.add_argument("--extent-m", type=float, default=1_000.0)
    parser.add_argument("--macro-extent-m", type=float, default=30_000.0)
    parser.add_argument("--chunk-m", type=float, default=512.0)
    parser.add_argument("--resolution", type=int, default=257)
    parser.add_argument("--output", default="")
    return build_preview(parser.parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
