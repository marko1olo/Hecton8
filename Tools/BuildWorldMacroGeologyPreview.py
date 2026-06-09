#!/usr/bin/env python3
"""Build deterministic 30 km macro-geology preview maps before touching Unity terrain.

This tool mirrors the runtime WorldMacroGeologyFields contract closely enough for
cheap visual review: height/bathymetry, geology zones, slope, curvature, erosion,
and source masks. It writes PNG fields plus a manifest with seed, scale, hashes,
and summary statistics.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import struct
import zlib
from concurrent.futures import ProcessPoolExecutor
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_OUTPUT = ROOT / "Docs/GeneratedAssets/Terrain/MacroGeology"
MIN_EXTENT_METERS = 30_000.0
ARTIFACT_VERSION = 6


@dataclass(frozen=True)
class Params:
    seed: int
    extent: float
    chunk: float
    water_y: float
    shelf_depth: float
    abyss_depth: float
    hadal_depth: float
    shelf_break_width: float
    ridge_height: float
    ridge_width: float
    trench_depth: float
    trench_width: float
    basin_depth: float
    detail_probe: float


@dataclass(frozen=True)
class Masks:
    shelf: float
    shelf_break: float
    ridge: float
    trench: float
    basin: float
    fault: float


@dataclass(frozen=True)
class Sample:
    height: float
    depth: float
    masks: Masks
    sediment: float
    seep: float
    zone: int


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


ZONE_COLORS = {
    0: (20, 20, 22),
    1: (190, 182, 132),
    2: (120, 138, 119),
    3: (58, 91, 78),
    4: (72, 91, 104),
    5: (82, 88, 91),
    6: (141, 126, 96),
    7: (139, 91, 56),
    8: (35, 46, 62),
}


DEPTH_STRATA = (
    ("Photic", 0.0, 200.0),
    ("Mesophotic", 200.0, 900.0),
    ("Bathyal", 900.0, 2000.0),
    ("Abyssal", 2000.0, 4000.0),
    ("Hadal", 4000.0, 1.0e9),
)

DEPTH_BANDS_METERS = (
    ("0-500", 0.0, 500.0),
    ("500-1000", 500.0, 1000.0),
    ("1000-2000", 1000.0, 2000.0),
    ("2000-3000", 2000.0, 3000.0),
    ("3000-4000", 3000.0, 4000.0),
    ("4000+", 4000.0, 1.0e9),
)

WATER_LEVEL_SWEEP_METERS = (-100.0, 0.0, 100.0)


def clamp01(value: float) -> float:
    return max(0.0, min(1.0, value))


def smoothstep(edge0: float, edge1: float, value: float) -> float:
    if edge1 == edge0:
        return 1.0 if value >= edge1 else 0.0
    t = clamp01((value - edge0) / (edge1 - edge0))
    return t * t * (3.0 - 2.0 * t)


def lerp(a: float, b: float, t: float) -> float:
    return a + (b - a) * t


def hash32(x: int, y: int, seed: int) -> int:
    value = (x * 0x8DA6B343) & 0xFFFFFFFF
    value ^= (y * 0xD8163841) & 0xFFFFFFFF
    value ^= (seed + 0x9E3779B9 + ((value << 6) & 0xFFFFFFFF) + (value >> 2)) & 0xFFFFFFFF
    value ^= value >> 16
    value = (value * 0x7FEB352D) & 0xFFFFFFFF
    value ^= value >> 15
    value = (value * 0x846CA68B) & 0xFFFFFFFF
    value ^= value >> 16
    return value & 0xFFFFFFFF


def hash01(x: int, y: int, seed: int) -> float:
    return (hash32(x, y, seed) & 0x00FFFFFF) / 16_777_215.0


def hash_to_unit(value: int) -> float:
    value &= 0xFFFFFFFF
    value ^= value >> 16
    value = (value * 0x7FEB352D) & 0xFFFFFFFF
    value ^= value >> 15
    value = (value * 0x846CA68B) & 0xFFFFFFFF
    value ^= value >> 16
    return (value & 0x00FFFFFF) / 16_777_215.0


def value_noise(x: float, z: float, seed: int) -> float:
    ix = math.floor(x)
    iz = math.floor(z)
    lx = x - ix
    lz = z - iz
    sx = lx * lx * (3.0 - 2.0 * lx)
    sz = lz * lz * (3.0 - 2.0 * lz)
    a = hash01(ix, iz, seed)
    b = hash01(ix + 1, iz, seed)
    c = hash01(ix, iz + 1, seed)
    d = hash01(ix + 1, iz + 1, seed)
    return lerp(lerp(a, b, sx), lerp(c, d, sx), sz)


def fractal_noise(x: float, z: float, seed: int, octaves: int = 5) -> float:
    amplitude = 0.5
    frequency = 1.0
    total = 0.0
    norm = 0.0
    for octave in range(octaves):
        total += value_noise(x * frequency, z * frequency, (seed + octave * 0x9E3779B9) & 0xFFFFFFFF) * amplitude
        norm += amplitude
        amplitude *= 0.5
        frequency *= 2.02
    return total / max(0.0001, norm)


def cellular_edge(x: float, z: float, seed: int) -> float:
    bx = math.floor(x)
    bz = math.floor(z)
    first = 1.0e9
    second = 1.0e9
    for dz in range(-1, 2):
        for dx in range(-1, 2):
            cx = bx + dx
            cz = bz + dz
            fx = cx + hash01(cx, cz, seed)
            fz = cz + hash01(cx, cz, seed ^ 0xA511E9B3)
            dist = math.hypot(x - fx, z - fz)
            if dist < first:
                second = first
                first = dist
            elif dist < second:
                second = dist
    return 1.0 - smoothstep(0.04, 0.42, max(0.0, second - first))


def ellipse_mask(x: float, z: float, cx: float, cz: float, rx: float, rz: float, rotation: float) -> float:
    s = math.sin(rotation)
    c = math.cos(rotation)
    dx = x - cx
    dz = z - cz
    ex = (dx * c - dz * s) / max(1.0, rx)
    ez = (dx * s + dz * c) / max(1.0, rz)
    return 1.0 - smoothstep(0.28, 1.0, math.hypot(ex, ez))


def combine_world_seed(authoring_seed: int, runtime_seed: int) -> int:
    return hash32(authoring_seed, runtime_seed, 0x6D2B79F5)


def zigzag32(value: int) -> int:
    return ((value << 1) ^ (value >> 31)) & 0xFFFFFFFF


def mix64(value: int) -> int:
    value &= 0xFFFFFFFFFFFFFFFF
    value ^= value >> 30
    value = (value * 0xBF58476D1CE4E5B9) & 0xFFFFFFFFFFFFFFFF
    value ^= value >> 27
    value = (value * 0x94D049BB133111EB) & 0xFFFFFFFFFFFFFFFF
    value ^= value >> 31
    return value & 0xFFFFFFFFFFFFFFFF


def build_chunk_artifact_id(chunk_x: int, chunk_z: int, seed: int, artifact_version: int, chunk_size_m: float) -> str:
    value = ((seed & 0xFFFFFFFF) << 32) ^ ((artifact_version & 0xFFFF) << 16)
    value ^= (int(round(max(128.0, chunk_size_m))) & 0xFFFF) * 0xD6E8FEB86659FD93
    value ^= (zigzag32(chunk_x) * 0x9E3779B97F4A7C15) & 0xFFFFFFFFFFFFFFFF
    value ^= (zigzag32(chunk_z) * 0xC2B2AE3D27D4EB4F) & 0xFFFFFFFFFFFFFFFF
    return f"{mix64(value):016X}"


def evaluate_height(x: float, z: float, params: Params) -> tuple[float, Masks]:
    extent = max(MIN_EXTENT_METERS, params.extent)
    half = extent * 0.5
    nx = x / extent
    nz = z / extent
    low_warp = (fractal_noise(nx * 2.0 + 11.7, nz * 2.0 - 3.9, params.seed ^ 0xB5297A4D) * 2.0 - 1.0) * 980.0
    mid_warp = (fractal_noise(nx * 4.4 - 2.1, nz * 4.4 + 8.6, params.seed ^ 0x4CF5AD43) * 2.0 - 1.0) * 520.0
    high_warp = (fractal_noise(nx * 7.2 - 17.2, nz * 7.2 + 29.3, params.seed ^ 0x68E31DA4) * 2.0 - 1.0) * 240.0

    shelf_curve = (
        -half * 0.30
        + math.sin(nx * 6.0 + hash_to_unit(params.seed) * math.tau) * 1150.0
        + math.sin(nx * 13.5 + hash_to_unit(params.seed ^ 0xBADC0FFE) * math.tau) * 420.0
        + low_warp * 0.62
        + mid_warp * 0.18
    )
    shelf_distance = z - shelf_curve
    shelf = 1.0 - smoothstep(-params.shelf_break_width * 0.45, params.shelf_break_width * 1.70, shelf_distance)
    shelf_break = 1.0 - smoothstep(0.0, params.shelf_break_width * 0.85, abs(shelf_distance))
    descent = smoothstep(-params.shelf_break_width * 0.65, params.shelf_break_width * 2.45, shelf_distance)

    depth = lerp(params.shelf_depth, params.abyss_depth, descent)
    depth += clamp01((z - shelf_curve) / max(1.0, half)) * 820.0

    terrace_target = round(depth / 260.0) * 260.0
    terrace_noise = (fractal_noise(nx * 16.5 - 4.0, nz * 16.5 + 9.0, params.seed ^ 0xD1B54A32) * 2.0 - 1.0) * 58.0
    terrace_weight = clamp01(shelf * 0.08 + shelf_break * 0.22)
    depth = lerp(depth, terrace_target + terrace_noise, terrace_weight)

    canyon = 0.0
    for i in range(5):
        h = hash32(params.seed, i, 0xBA5EBA11)
        cx = lerp(-half * 0.88, half * 0.88, hash_to_unit(h))
        phase = hash_to_unit(h ^ 0x9E3779B9) * math.tau
        width = lerp(620.0, 1850.0, hash_to_unit(h ^ 0x27D4EB2F))
        center_x = cx + math.sin(z * 0.00034 + phase) * 860.0 + low_warp * 0.08 + mid_warp * 0.04
        gate = smoothstep(-params.shelf_break_width * 0.95, params.shelf_break_width * 0.18, shelf_distance)
        gate *= 1.0 - smoothstep(params.shelf_break_width * 2.50, params.shelf_break_width * 5.40, shelf_distance)
        cut = 1.0 - smoothstep(width * 0.34, width * 1.95, abs(x - center_x))
        canyon = max(canyon, cut * gate)
    depth += canyon * lerp(150.0, 560.0, descent)
    shelf_break = max(shelf_break, canyon * 0.36)

    rolling_hills = (
        fractal_noise(nx * 9.0 + 31.1, nz * 9.0 - 12.2, params.seed ^ 0xA53F9E21) * 2.0 - 1.0
    )
    depth += rolling_hills * 175.0 * clamp01(shelf * 0.45 + shelf_break * 0.40 + (1.0 - descent) * 0.18)

    n_x, n_z = 0.72, -0.69
    n_len = math.hypot(n_x, n_z)
    n_x, n_z = n_x / n_len, n_z / n_len
    t_x, t_z = -n_z, n_x
    along = x * t_x + z * t_z
    across = x * n_x + z * n_z
    fault_wander = (
        math.sin(along * 0.00031 + hash_to_unit(params.seed ^ 0x9E3779B9) * math.tau) * 980.0
        + (fractal_noise(along * 0.00014, 3.71, params.seed ^ 0xC2B2AE35) * 2.0 - 1.0) * 1420.0
    )
    trench_distance = abs(across - fault_wander + 1700.0 + high_warp * 0.42)
    trench = 1.0 - smoothstep(params.trench_width * 0.62, params.trench_width * 2.05, trench_distance)
    fault = 1.0 - smoothstep(params.trench_width * 0.95, params.trench_width * 4.20, trench_distance)

    n2_x, n2_z = -0.38, -0.925
    n2_len = math.hypot(n2_x, n2_z)
    n2_x, n2_z = n2_x / n2_len, n2_z / n2_len
    t2_x, t2_z = -n2_z, n2_x
    along2 = x * t2_x + z * t2_z
    across2 = x * n2_x + z * n2_z
    fault_wander2 = (
        math.sin(along2 * 0.00026 + hash_to_unit(params.seed ^ 0x51ED270B) * math.tau) * 1450.0
        + (fractal_noise(along2 * 0.00011, -9.37, params.seed ^ 0xA24BAED5) * 2.0 - 1.0) * 1180.0
    )
    secondary_distance = abs(across2 - fault_wander2 - 900.0 + mid_warp * 0.35)
    secondary_fault = 1.0 - smoothstep(params.trench_width * 1.10, params.trench_width * 4.60, secondary_distance)
    secondary_depression = 1.0 - smoothstep(params.trench_width * 0.55, params.trench_width * 2.70, abs(across2 - fault_wander2 + 3100.0))

    ridge_distance_a = abs(across - fault_wander - 4300.0)
    ridge_distance_b = abs(across - fault_wander + 6900.0)
    ridge_distance_c = abs(across2 - fault_wander2 - 2600.0)
    ridge_a = 1.0 - smoothstep(params.ridge_width * 0.34, params.ridge_width * 2.35, ridge_distance_a)
    ridge_b = 1.0 - smoothstep(params.ridge_width * 0.38, params.ridge_width * 2.45, ridge_distance_b)
    ridge_c = 1.0 - smoothstep(params.ridge_width * 0.45, params.ridge_width * 2.80, ridge_distance_c)
    ridge = clamp01(max(ridge_a, ridge_b, ridge_c) + fault * 0.04 + secondary_fault * 0.05)

    basin_a = ellipse_mask(x, z, -7400.0, 6500.0, 6900.0, 4300.0, 0.34)
    basin_b = ellipse_mask(x, z, 8400.0, -5600.0, 5600.0, 8200.0, -0.58)
    basin_c = ellipse_mask(x, z, 1400.0, 9800.0, 8500.0, 4800.0, -0.18)
    basin = max(basin_a, basin_b, basin_c)

    ridge_chain_hills = 0.0
    for i in range(4):
        h = hash32(params.seed, i, 0x7FEB352D)
        angle = lerp(-1.05, 0.85, hash_to_unit(h ^ 0x846CA68B))
        chain_nx = math.cos(angle)
        chain_nz = math.sin(angle)
        chain_tx = -chain_nz
        chain_tz = chain_nx
        chain_along = x * chain_tx + z * chain_tz
        chain_across = x * chain_nx + z * chain_nz
        center = lerp(-half * 0.62, half * 0.62, hash_to_unit(h ^ 0x27D4EB2F))
        width = lerp(760.0, 2100.0, hash_to_unit(h ^ 0xC2B2AE35))
        wander = math.sin(chain_along * 0.00031 + hash_to_unit(h ^ 0xB5297A4D) * math.tau) * lerp(520.0, 1450.0, hash_to_unit(h ^ 0xA53F9E21))
        band = 1.0 - smoothstep(width * 0.34, width * 1.86, abs(chain_across - center - wander))
        bead = fractal_noise(chain_along * 0.00042 + 17.0, chain_across * 0.00020 - 6.0, h ^ 0x735A2D97)
        intermittent = smoothstep(0.42, 0.86, bead)
        ridge_chain_hills = max(ridge_chain_hills, band * intermittent)

    province_edge = cellular_edge(x * 0.000075 - 42.6, z * 0.000075 + 18.4, params.seed ^ 0x8B4C2F91)
    province_texture = fractal_noise(x * 0.000061 - 3.4, z * 0.000061 + 15.1, params.seed ^ 0x21DA7F47)
    province_relief = smoothstep(0.36, 0.92, province_edge * 0.72 + province_texture * 0.28)
    regional_warp_a = (fractal_noise(x * 0.000026 + 8.1, z * 0.000026 - 5.3, params.seed ^ 0xD8E4C2A7) * 2.0 - 1.0) * 5200.0
    regional_warp_b = (fractal_noise(x * 0.000019 - 14.4, z * 0.000019 + 22.8, params.seed ^ 0xA1B35F19) * 2.0 - 1.0) * 6800.0
    network_warp_x = (fractal_noise(x * 0.000041 + 27.2, z * 0.000041 - 11.8, params.seed ^ 0xB77A91C3) * 2.0 - 1.0) * 4300.0
    network_warp_z = (fractal_noise(x * 0.000037 - 9.6, z * 0.000037 + 33.1, params.seed ^ 0x69D4F2A5) * 2.0 - 1.0) * 3900.0
    network_x = x + regional_warp_a * 0.48 + network_warp_x
    network_z = z + regional_warp_b * 0.38 + network_warp_z
    network_edge_a = cellular_edge(network_x * 0.000052 + 3.2, network_z * 0.000052 - 8.7, params.seed ^ 0xC35F19AB)
    network_edge_b = cellular_edge(
        (network_x * 0.72 + network_z * 0.20) * 0.000044 - 16.4,
        (network_z * 0.88 - network_x * 0.12) * 0.000044 + 5.9,
        params.seed ^ 0x7E2C4D55,
    )
    network_activity = smoothstep(0.26, 0.78, fractal_noise(network_x * 0.000082 + 4.4, network_z * 0.000082 - 19.1, params.seed ^ 0x92E4B77D))
    braided_network = max(
        smoothstep(0.28, 0.86, network_edge_a),
        smoothstep(0.34, 0.90, network_edge_b) * 0.74,
    )
    network_texture = fractal_noise(network_x * 0.000115 - 2.7, network_z * 0.000115 + 8.9, params.seed ^ 0xCA97D1F3)
    tectonic_network = clamp01(
        braided_network
        * (0.44 + network_activity * 0.50)
        * (0.78 + network_texture * 0.22)
        + province_relief * 0.06
    )
    network_node = smoothstep(0.72, 0.96, network_edge_a) * smoothstep(0.52, 0.90, network_edge_b) * network_activity
    highland_plate = smoothstep(0.56, 0.84, fractal_noise(x * 0.000047 - 31.6, z * 0.000047 + 7.3, params.seed ^ 0xE35A9217))
    shallow_province = smoothstep(
        0.50,
        0.80,
        fractal_noise(
            x * 0.000030 + regional_warp_a * 0.000010 + 44.0,
            z * 0.000030 + regional_warp_b * 0.000010 - 28.0,
            params.seed ^ 0x4B6D13F7,
        ),
    )
    rise_wave_a = 0.5 + 0.5 * math.sin((x * 0.34 + z * 0.94 + regional_warp_a) * 0.00019 + hash_to_unit(params.seed ^ 0x6E624EB7) * math.tau)
    rise_wave_b = 0.5 + 0.5 * math.sin((x * -0.87 + z * 0.49 + regional_warp_b) * 0.000145 + hash_to_unit(params.seed ^ 0xC7425A31) * math.tau)
    directed_uplift = max(smoothstep(0.64, 0.96, rise_wave_a) * 0.38, smoothstep(0.68, 0.97, rise_wave_b) * 0.30)
    distributed_highs_noise = fractal_noise(
        x * 0.000033 + regional_warp_a * 0.000006 - 71.0,
        z * 0.000033 + regional_warp_b * 0.000006 + 39.0,
        params.seed ^ 0x18F3A9C5,
    )
    distributed_highs_wave = rise_wave_a * 0.58 + rise_wave_b * 0.42
    distributed_highs = max(
        smoothstep(0.52, 0.82, distributed_highs_noise),
        smoothstep(0.70, 0.96, distributed_highs_wave) * 0.72,
    ) * (0.54 + network_activity * 0.32 + province_relief * 0.18)
    uplift_network = clamp01(max(max(max(max(tectonic_network * 0.62, highland_plate * 0.48), shallow_province * 0.58), directed_uplift), distributed_highs * 0.58) + province_relief * 0.04 + ridge_chain_hills * 0.12 + network_node * 0.12)
    basin_wave = 0.5 + 0.5 * math.sin((x * 0.68 - z * 0.74 + regional_warp_b * 0.72) * 0.00012 + hash_to_unit(params.seed ^ 0x53C9E2B1) * math.tau)
    recurring_basin = clamp01(smoothstep(0.72, 0.98, basin_wave) * (1.0 - uplift_network * 0.62))
    fracture_noise = cellular_edge(x * 0.00018 + 19.3, z * 0.00018 - 7.1, params.seed ^ 0x51633E2D)
    fracture = clamp01(fracture_noise * 0.09 + tectonic_network * 0.16 + network_node * 0.10 + province_relief * 0.06 + fault * 0.48 + secondary_fault * 0.24 + canyon * 0.04)
    depth += trench * params.trench_depth * smoothstep(0.24, 1.0, descent)
    depth += secondary_depression * 520.0 * smoothstep(0.18, 0.95, descent)
    basin = max(basin, recurring_basin * 0.34)
    ridge = max(ridge, uplift_network * 0.38, tectonic_network * 0.48)
    depth += basin * params.basin_depth
    depth -= ridge * params.ridge_height * smoothstep(0.16, 0.90, descent)
    depth -= ridge_chain_hills * lerp(260.0, 980.0, clamp01(shelf * 0.18 + shelf_break * 0.24 + ridge * 0.58))
    regional_breakup = fractal_noise(nx * 5.5 - 4.2, nz * 5.5 + 12.9, params.seed ^ 0x41C64E6D) * 2.0 - 1.0
    regional_swell = math.sin(nx * 11.3 + nz * 5.7 + hash_to_unit(params.seed ^ 0xDEADBEEF) * math.tau)
    depth += regional_breakup * 320.0 * clamp01(0.25 + descent * 0.65 + shelf_break * 0.12)
    depth += regional_swell * 170.0 * clamp01(0.20 + shelf * 0.35 + descent * 0.45)
    depth += province_relief * 145.0 * clamp01(0.18 + descent * 0.50 + shelf_break * 0.18 + ridge * 0.18)
    depth -= uplift_network * lerp(260.0, 1480.0, clamp01(descent * 0.82 + ridge * 0.16 + shelf_break * 0.10))
    depth -= highland_plate * lerp(120.0, 720.0, clamp01(descent * 0.75 + shelf_break * 0.12))
    depth -= shallow_province * lerp(220.0, 1850.0, clamp01(descent * 0.88 + shelf_break * 0.10))
    depth -= distributed_highs * lerp(180.0, 1150.0, clamp01(descent * 0.90 + shelf_break * 0.08 + ridge * 0.08))
    depth += recurring_basin * 430.0 * clamp01(0.24 + descent * 0.56)
    relief_gate = clamp01(0.22 + shelf_break * 0.24 + ridge * 0.28 + fault * 0.24 + canyon * 0.18 + basin * 0.08)
    macro_breakup = fractal_noise(nx * 18.0 + 7.7, nz * 18.0 + 41.3, params.seed ^ 0x91E83B37) * 2.0 - 1.0
    meso_breakup = fractal_noise(nx * 48.0 - 23.1, nz * 48.0 + 5.6, params.seed ^ 0x6C8E9CF5) * 2.0 - 1.0
    depth += macro_breakup * 210.0 * relief_gate
    depth += meso_breakup * 82.0 * clamp01(relief_gate + shelf * 0.12 + shelf_break * 0.16)
    depth += fracture * 84.0
    if depth < -260.0:
        depth = -260.0 + (depth + 260.0) * 0.42
    depth = max(-620.0, min(params.hadal_depth, depth))
    masks = Masks(clamp01(shelf), clamp01(shelf_break), clamp01(ridge), clamp01(trench), clamp01(basin), clamp01(fracture))
    return params.water_y - depth, masks


def evaluate_sample(x: float, z: float, params: Params) -> dict[str, float | int | Masks]:
    height, masks = evaluate_height(x, z, params)
    probe = max(8.0, params.detail_probe)
    west, _ = evaluate_height(x - probe, z, params)
    east, _ = evaluate_height(x + probe, z, params)
    south, _ = evaluate_height(x, z - probe, params)
    north, _ = evaluate_height(x, z + probe, params)
    dx = (east - west) / max(0.001, probe * 2.0)
    dz = (north - south) / max(0.001, probe * 2.0)
    slope = math.sqrt(max(0.0, dx * dx + dz * dz))
    curvature = (west + east + south + north - height * 4.0) / max(0.001, probe * probe)
    slope01 = clamp01(slope / 1.25)
    curvature01 = clamp01(abs(curvature) * 280.0)
    positive_curvature = clamp01(max(0.0, curvature) * 280.0)
    negative_curvature = clamp01(max(0.0, -curvature) * 280.0)
    depth = max(0.0, params.water_y - height)
    sediment = clamp01((1.0 - slope01) * 0.58 + masks.basin * 0.42 + masks.shelf * 0.16 - masks.ridge * 0.28 - masks.trench * 0.16)
    seep = clamp01(masks.fault * 0.45 + masks.basin * 0.25 + masks.trench * 0.18)
    basin_flow = clamp01(masks.basin * 0.48 + masks.shelf_break * 0.22 + (1.0 - slope01) * 0.18)
    fault_flow = clamp01(masks.fault * 0.35 + masks.trench * 0.32)
    erosion = clamp01(basin_flow + fault_flow + fractal_noise(x * 0.00038, z * 0.00038, params.seed ^ 0xA511E9B3) * 0.12)
    deep_plain = clamp01((depth - 1600.0) / 1800.0)
    shallow_reef_band = clamp01(1.0 - abs(depth - 90.0) / 520.0)
    terrace = clamp01(masks.shelf_break * 0.36 + masks.shelf * 0.14 + positive_curvature * 0.20 + erosion * 0.16 - masks.trench * 0.16)
    slump = clamp01(masks.shelf_break * 0.30 + negative_curvature * 0.42 + slope01 * 0.28 - masks.ridge * 0.18)
    tributary = clamp01(erosion * 0.48 + masks.fault * 0.24 + masks.shelf_break * 0.20 + negative_curvature * 0.26)
    nodule = clamp01(sediment * 0.52 + (1.0 - slope01) * 0.28 + deep_plain * 0.20 - masks.ridge * 0.35 - masks.trench * 0.20)
    reef = clamp01(masks.shelf * 0.52 + shallow_reef_band * 0.34 + (1.0 - slope01) * 0.18 - masks.trench * 0.25)
    hard_rock = clamp01(masks.ridge * 0.45 + masks.fault * 0.28 + slope01 * 0.30 - sediment * 0.24)
    voxel_seam = clamp01(masks.fault * 0.35 + curvature01 * 0.40 + slope01 * 0.22 + masks.trench * 0.16)
    zone = resolve_zone(depth, masks, sediment, seep, slope01)
    return {
        "height": height,
        "depth": depth,
        "masks": masks,
        "slope01": slope01,
        "curvature01": curvature01,
        "positiveCurvature01": positive_curvature,
        "negativeCurvature01": negative_curvature,
        "erosion": erosion,
        "sediment": sediment,
        "seep": seep,
        "zone": zone,
        "terrace": terrace,
        "slump": slump,
        "tributary": tributary,
        "nodule": nodule,
        "reef": reef,
        "hardRock": hard_rock,
        "voxelSeam": voxel_seam,
    }


def resolve_zone(depth: float, masks: Masks, sediment: float, seep: float, slope01: float) -> int:
    if (masks.trench > 0.92 and depth > 500.0) or depth > 4450.0:
        return 4

    if masks.shelf > 0.68 and depth < 260.0:
        return 1

    if masks.shelf_break > 0.35 and 150.0 < depth < 2400.0:
        return 2

    if (
        seep > 0.40
        and 500.0 < depth < 4300.0
        and masks.trench < 0.62
        and masks.shelf < 0.82
        and masks.ridge < 0.88
        and (masks.fault > 0.28 or masks.basin > 0.38)
    ):
        return 7

    if masks.basin > 0.54 and sediment > 0.50 and slope01 < 0.48:
        return 6

    if masks.ridge > 0.72 or (masks.fault > 0.82 and slope01 > 0.48):
        return 3

    if depth > 3900.0 and masks.trench < 0.76:
        return 8

    return 5


def evaluate_row_task(task: tuple[int, int, float, float, float, float, Params]) -> tuple[int, list[float], list[float], list[Masks]]:
    y, resolution, half, step, origin_x, origin_z, params = task
    z = origin_z - half + y * step
    row_heights: list[float] = []
    row_depths: list[float] = []
    row_masks: list[Masks] = []
    for x_index in range(resolution):
        x = origin_x - half + x_index * step
        height, masks = evaluate_height(x, z, params)
        row_heights.append(height)
        row_depths.append(max(0.0, params.water_y - height))
        row_masks.append(masks)
    return y, row_heights, row_depths, row_masks


def format_origin_token(value: float) -> str:
    sign = "N" if value < 0.0 else "P"
    magnitude = int(round(abs(value)))
    return f"{sign}{magnitude}"


def write_png_rgb(path: Path, width: int, height: int, pixels: list[tuple[int, int, int]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    raw_rows = []
    for y in range(height):
        row = bytearray([0])
        start = y * width
        for r, g, b in pixels[start : start + width]:
            row.extend((r & 255, g & 255, b & 255))
        raw_rows.append(bytes(row))
    raw = b"".join(raw_rows)

    def chunk(tag: bytes, data: bytes) -> bytes:
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    payload = (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0))
        + chunk(b"IDAT", zlib.compress(raw, 6))
        + chunk(b"IEND", b"")
    )
    path.write_bytes(payload)


def shade_height(depth: float, min_depth: float, max_depth: float) -> tuple[int, int, int]:
    t = clamp01((depth - min_depth) / max(1.0, max_depth - min_depth))
    if t < 0.28:
        k = t / 0.28
        return (
            int(190 - 52 * k),
            int(186 - 43 * k),
            int(138 - 18 * k),
        )
    if t < 0.72:
        k = (t - 0.28) / 0.44
        return (
            int(138 - 62 * k),
            int(143 - 53 * k),
            int(120 - 28 * k),
        )
    k = (t - 0.72) / 0.28
    return (
        int(76 - 39 * k),
        int(90 - 42 * k),
        int(92 - 28 * k),
    )


def shade_signed_height(height: float, min_height: float, max_height: float) -> tuple[int, int, int]:
    if height > 0.0:
        t = clamp01(height / max(1.0, max_height))
        return (
            int(184 + 32 * t),
            int(176 + 28 * t),
            int(126 + 42 * t),
        )

    depth = -height
    return shade_height(depth, 0.0, max(1.0, -min_height))


def shade_raw_elevation(height: float, min_height: float, max_height: float) -> tuple[int, int, int]:
    t = clamp01((height - min_height) / max(1.0, max_height - min_height))
    if t < 0.34:
        k = t / 0.34
        return (
            int(34 + 42 * k),
            int(39 + 45 * k),
            int(43 + 39 * k),
        )
    if t < 0.72:
        k = (t - 0.34) / 0.38
        return (
            int(76 + 66 * k),
            int(84 + 56 * k),
            int(82 + 31 * k),
        )
    k = (t - 0.72) / 0.28
    return (
        int(142 + 78 * k),
        int(140 + 68 * k),
        int(113 + 52 * k),
    )


def shade_hillshade(
    height: float,
    min_height: float,
    max_height: float,
    slope01: float,
    curvature01: float,
) -> tuple[int, int, int]:
    base = shade_raw_elevation(height, min_height, max_height)
    shade = 0.70 + slope01 * 0.42 - curvature01 * 0.24
    return tuple(max(0, min(255, int(c * shade + 0.5))) for c in base)


def shade_terrain_relief_proof(
    height: float,
    min_height: float,
    max_height: float,
    slope01: float,
    curvature01: float,
    erosion01: float,
) -> tuple[int, int, int]:
    base = shade_raw_elevation(height, min_height, max_height)
    relief = clamp01(slope01 * 0.54 + curvature01 * 0.34 + erosion01 * 0.12)
    shade = 0.62 + relief * 0.66
    return (
        max(0, min(255, int(base[0] * shade + 14 * relief + 0.5))),
        max(0, min(255, int(base[1] * shade + 9 * relief + 0.5))),
        max(0, min(255, int(base[2] * shade + 3 * relief + 0.5))),
    )


def shade_local_relief(slope01: float, curvature01: float, erosion01: float) -> tuple[int, int, int]:
    relief = clamp01(slope01 * 0.52 + curvature01 * 0.34 + erosion01 * 0.14)
    return (
        int(42 + 180 * relief),
        int(45 + 150 * relief),
        int(42 + 92 * relief),
    )


def gray(value: float) -> tuple[int, int, int]:
    c = int(clamp01(value) * 255.0 + 0.5)
    return c, c, c


def heat(value: float) -> tuple[int, int, int]:
    t = clamp01(value)
    return int(42 + 205 * t), int(58 + 126 * (1.0 - abs(t - 0.5) * 2.0)), int(82 + 108 * (1.0 - t))


def blend(a: tuple[int, int, int], b: tuple[int, int, int], t: float) -> tuple[int, int, int]:
    u = clamp01(t)
    return tuple(int(a[i] + (b[i] - a[i]) * u + 0.5) for i in range(3))


def shade_depth_strata(depth: float) -> tuple[int, int, int]:
    if depth < 200.0:
        return 184, 176, 126
    if depth < 900.0:
        return 127, 143, 128
    if depth < 2000.0:
        return 91, 111, 118
    if depth < 4000.0:
        return 61, 75, 86
    return 38, 48, 66


SURFACE_MATERIAL_COLORS = {
    "shellSand": (185, 178, 132),
    "limestoneShelf": (156, 164, 142),
    "claySilt": (104, 116, 118),
    "hardRock": (52, 84, 78),
    "brineSaltCrust": (176, 188, 190),
    "manganeseNodulePlain": (54, 50, 48),
    "reefRubble": (126, 150, 109),
    "seepCrust": (151, 92, 54),
}


def coarse_value_noise(x: float, z: float, seed: int, cell_size_m: float) -> float:
    inv_cell = 1.0 / max(1.0, cell_size_m)
    sx = x * inv_cell
    sz = z * inv_cell
    ix = math.floor(sx)
    iz = math.floor(sz)
    lx = sx - ix
    lz = sz - iz
    fx = lx * lx * (3.0 - 2.0 * lx)
    fz = lz * lz * (3.0 - 2.0 * lz)
    a = hash01(ix, iz, seed)
    b = hash01(ix + 1, iz, seed)
    c = hash01(ix, iz + 1, seed)
    d = hash01(ix + 1, iz + 1, seed)
    return lerp(lerp(a, b, fx), lerp(c, d, fx), fz)


def resolve_surface_material_weights(
    x: float,
    z: float,
    seed: int,
    depth: float,
    masks: Masks,
    sediment: float,
    seep: float,
    slope01: float,
    erosion: float,
    terrace: float,
    slump: float,
    tributary: float,
    nodule: float,
    reef: float,
    hard_rock: float,
) -> dict[str, float]:
    slope = clamp01(slope01)
    flat = 1.0 - slope
    shallow = 1.0 - clamp01((depth - 40.0) / 420.0)
    upper_water = 1.0 - clamp01((depth - 500.0) / 1250.0)
    abyss = clamp01((depth - 1200.0) / 2300.0)
    province_jitter = coarse_value_noise(x, z, seed ^ 0x51A7E531, 900.0)
    local_patch = coarse_value_noise(x, z, seed ^ 0xB34ACE21, 240.0)
    weights = {
        "shellSand": clamp01(masks.shelf * shallow * flat * 0.62 + terrace * shallow * 0.20 + (local_patch - 0.42) * 0.08),
        "limestoneShelf": clamp01(masks.shelf * (0.35 + masks.shelf_break * 0.28) + masks.ridge * shallow * 0.20 + terrace * 0.22),
        "claySilt": clamp01(sediment * (0.58 + masks.basin * 0.30) * (1.0 - hard_rock * 0.42) + slump * 0.18 + flat * abyss * 0.16),
        "hardRock": clamp01(hard_rock * 0.72 + masks.ridge * 0.44 + slope * 0.36 + masks.fault * 0.24 - sediment * 0.22 - reef * 0.12 - seep * 0.16),
        "brineSaltCrust": clamp01(masks.trench * (0.46 + abyss * 0.34) + (masks.basin * 0.12 if depth > 2500.0 else 0.0)),
        "manganeseNodulePlain": clamp01(nodule * abyss * flat * (0.72 + province_jitter * 0.24) * (1.0 - masks.trench * 0.55)),
        "reefRubble": clamp01(reef * shallow * upper_water * (0.82 + local_patch * 0.38) * (1.0 - masks.trench * 0.72)),
        "seepCrust": clamp01(seep * (0.72 + tributary * 0.32 + erosion * 0.24) * (1.0 - masks.shelf * 0.24)),
    }
    total = sum(weights.values())
    if total <= 0.0001 or not math.isfinite(total):
        rock = clamp01(slope)
        silt = clamp01((1.0 - rock) * sediment)
        sand = clamp01((1.0 - rock) * (1.0 - silt))
        total = max(0.0001, sand + silt + rock)
        return {"shellSand": sand / total, "claySilt": silt / total, "hardRock": rock / total}
    return {key: value / total for key, value in weights.items()}


def fold_surface_material_to_control(weights: dict[str, float]) -> tuple[float, float, float, float]:
    rock = clamp01(
        weights.get("hardRock", 0.0)
        + weights.get("limestoneShelf", 0.0) * 0.78
        + weights.get("reefRubble", 0.0) * 0.30
        + weights.get("seepCrust", 0.0) * 0.34
    )
    sand = clamp01(
        weights.get("shellSand", 0.0)
        + weights.get("reefRubble", 0.0) * 0.52
        + weights.get("limestoneShelf", 0.0) * 0.18
    )
    silt = clamp01(
        weights.get("claySilt", 0.0)
        + weights.get("manganeseNodulePlain", 0.0) * 0.62
        + weights.get("brineSaltCrust", 0.0) * 0.22
    )
    deposition = clamp01(
        weights.get("brineSaltCrust", 0.0) * 0.62
        + weights.get("seepCrust", 0.0) * 0.46
        + weights.get("manganeseNodulePlain", 0.0) * 0.22
    )
    total = max(0.0001, rock + sand + silt + deposition)
    return rock / total, sand / total, silt / total, deposition / total


def shade_material_region(weights: dict[str, float]) -> tuple[int, int, int]:
    r = 0.0
    g = 0.0
    b = 0.0
    total = 0.0
    for key, weight in weights.items():
        color = SURFACE_MATERIAL_COLORS.get(key)
        if color is None:
            continue
        w = clamp01(weight)
        r += color[0] * w
        g += color[1] * w
        b += color[2] * w
        total += w
    if total <= 0.0001:
        return 76, 82, 84
    return int(r / total + 0.5), int(g / total + 0.5), int(b / total + 0.5)


def shade_surface_control(weights: dict[str, float]) -> tuple[int, int, int]:
    rock, sand, silt, deposition = fold_surface_material_to_control(weights)
    return int(42 + 205 * rock), int(42 + 205 * sand), int(42 + 205 * max(silt, deposition))


def shade_waterline_sweep(height: float) -> tuple[int, int, int]:
    if height >= 100.0:
        return 214, 199, 137
    if height >= 0.0:
        return 171, 171, 126
    if height >= -100.0:
        return 103, 143, 139
    if height >= -500.0:
        return 72, 111, 125
    if height >= -1500.0:
        return 56, 82, 101
    if height >= -3000.0:
        return 42, 62, 84
    return 30, 42, 64


def shade_relief(height: float, min_height: float, max_height: float, slope01: float, curvature01: float) -> tuple[int, int, int]:
    base = shade_signed_height(height, min_height, max_height)
    shade = 0.82 + slope01 * 0.26 - curvature01 * 0.18
    return tuple(max(0, min(255, int(c * shade + 0.5))) for c in base)


def build_preview(args: argparse.Namespace) -> int:
    combined_seed = combine_world_seed(args.seed, args.runtime_seed)
    params = Params(
        seed=combined_seed,
        extent=max(MIN_EXTENT_METERS, float(args.extent_m)),
        chunk=max(128.0, float(args.chunk_m)),
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
        detail_probe=max(8.0, float(args.detail_probe_m)),
    )
    origin_x = float(args.origin_x_m)
    origin_z = float(args.origin_z_m)
    resolution = max(96, min(2048, int(args.resolution)))
    analysis_resolution_arg = int(args.analysis_resolution)
    if 0 < analysis_resolution_arg < resolution:
        resolution = max(96, min(resolution, analysis_resolution_arg))
    analysis_resolution = resolution
    half = params.extent * 0.5
    step = params.extent / max(1, resolution - 1)
    analysis_step = params.extent / max(1, analysis_resolution - 1)

    heights: list[float] = [0.0] * (analysis_resolution * analysis_resolution)
    depths: list[float] = [0.0] * (analysis_resolution * analysis_resolution)
    masks: list[Masks] = [Masks(0.0, 0.0, 0.0, 0.0, 0.0, 0.0)] * (analysis_resolution * analysis_resolution)
    zones = [0] * (resolution * resolution)
    zone_counts: dict[str, int] = {name: 0 for name in ZONE_NAMES.values()}

    worker_count = max(1, int(args.workers))
    if worker_count > 1:
        worker_count = min(worker_count, os.cpu_count() or worker_count)
        tasks = [(y, analysis_resolution, half, analysis_step, origin_x, origin_z, params) for y in range(analysis_resolution)]
        try:
            with ProcessPoolExecutor(max_workers=worker_count) as executor:
                for y, row_heights, row_depths, row_masks in executor.map(evaluate_row_task, tasks, chunksize=1):
                    start = y * analysis_resolution
                    heights[start : start + analysis_resolution] = row_heights
                    depths[start : start + analysis_resolution] = row_depths
                    masks[start : start + analysis_resolution] = row_masks
        except (OSError, PermissionError) as exc:
            print(f"WARNING: multiprocessing disabled, falling back to single worker: {exc}")
            worker_count = 1

    if worker_count == 1:
        for y in range(analysis_resolution):
            z = origin_z - half + y * analysis_step
            for x_index in range(analysis_resolution):
                x = origin_x - half + x_index * analysis_step
                i = y * analysis_resolution + x_index
                h, m = evaluate_height(x, z, params)
                heights[i] = h
                depths[i] = max(0.0, params.water_y - h)
                masks[i] = m

    min_depth = min(depths)
    max_depth = max(depths)
    min_height = min(heights)
    max_height = max(heights)

    slope_values = [0.0] * (resolution * resolution)
    curvature_values = [0.0] * (resolution * resolution)
    erosion_values = [0.0] * (resolution * resolution)
    sediment_values = [0.0] * (resolution * resolution)
    seep_values = [0.0] * (resolution * resolution)

    def h_at(ix: int, iy: int) -> float:
        ix = max(0, min(analysis_resolution - 1, ix))
        iy = max(0, min(analysis_resolution - 1, iy))
        return heights[iy * analysis_resolution + ix]

    slope_values = [0.0] * (analysis_resolution * analysis_resolution)
    curvature_values = [0.0] * (analysis_resolution * analysis_resolution)
    erosion_values = [0.0] * (analysis_resolution * analysis_resolution)
    sediment_values = [0.0] * (analysis_resolution * analysis_resolution)
    seep_values = [0.0] * (analysis_resolution * analysis_resolution)
    terrace_values = [0.0] * (analysis_resolution * analysis_resolution)
    slump_values = [0.0] * (analysis_resolution * analysis_resolution)
    tributary_values = [0.0] * (analysis_resolution * analysis_resolution)
    nodule_values = [0.0] * (analysis_resolution * analysis_resolution)
    reef_values = [0.0] * (analysis_resolution * analysis_resolution)
    hard_rock_values = [0.0] * (analysis_resolution * analysis_resolution)
    voxel_seam_values = [0.0] * (analysis_resolution * analysis_resolution)
    surface_material_pixels = [(0, 0, 0)] * (analysis_resolution * analysis_resolution)
    terrain_control_pixels = [(0, 0, 0)] * (analysis_resolution * analysis_resolution)
    surface_material_counts: dict[str, int] = {name: 0 for name in SURFACE_MATERIAL_COLORS}
    zones = [0] * (analysis_resolution * analysis_resolution)
    zone_counts = {name: 0 for name in ZONE_NAMES.values()}
    for y in range(analysis_resolution):
        for x_index in range(analysis_resolution):
            i = y * analysis_resolution + x_index
            wx = origin_x - half + x_index * analysis_step
            wz = origin_z - half + y * analysis_step
            sample = evaluate_sample(wx, wz, params)
            slope01 = float(sample["slope01"])
            curvature01 = float(sample["curvature01"])
            erosion = float(sample["erosion"])
            sediment = float(sample["sediment"])
            seep = float(sample["seep"])
            zone = int(sample["zone"])
            zones[i] = zone
            zone_counts[ZONE_NAMES[zone]] += 1
            slope_values[i] = slope01
            curvature_values[i] = curvature01
            erosion_values[i] = erosion
            sediment_values[i] = sediment
            seep_values[i] = seep
            terrace_values[i] = float(sample["terrace"])
            slump_values[i] = float(sample["slump"])
            tributary_values[i] = float(sample["tributary"])
            nodule_values[i] = float(sample["nodule"])
            reef_values[i] = float(sample["reef"])
            hard_rock_values[i] = float(sample["hardRock"])
            voxel_seam_values[i] = float(sample["voxelSeam"])
            material_weights = resolve_surface_material_weights(
                wx,
                wz,
                params.seed,
                depths[i],
                masks[i],
                sediment,
                seep,
                slope01,
                erosion,
                float(sample["terrace"]),
                float(sample["slump"]),
                float(sample["tributary"]),
                float(sample["nodule"]),
                float(sample["reef"]),
                float(sample["hardRock"]),
            )
            surface_material_pixels[i] = shade_material_region(material_weights)
            terrain_control_pixels[i] = shade_surface_control(material_weights)
            dominant_material = max(material_weights, key=material_weights.get)
            surface_material_counts[dominant_material] = surface_material_counts.get(dominant_material, 0) + 1

    curvature_shaded = curvature_values

    depth_strata_counts: dict[str, int] = {name: 0 for name, _, _ in DEPTH_STRATA}
    for depth in depths:
        for name, low, high in DEPTH_STRATA:
            if low <= depth < high:
                depth_strata_counts[name] += 1
                break
    depth_band_counts: dict[str, int] = {name: 0 for name, _, _ in DEPTH_BANDS_METERS}
    for depth in depths:
        for name, low, high in DEPTH_BANDS_METERS:
            if low <= depth < high:
                depth_band_counts[name] += 1
                break

    water_level_sweep = {}
    for water_level in WATER_LEVEL_SWEEP_METERS:
        exposed = 0
        shallow = 0
        for height in heights:
            depth_at_level = water_level - height
            if height >= water_level:
                exposed += 1
            elif depth_at_level <= 30.0:
                shallow += 1
        water_level_sweep[str(int(water_level))] = {
            "exposedPixels": exposed,
            "shallowPixels0To30m": shallow,
            "exposedPercent": round(exposed / max(1, len(heights)) * 100.0, 4),
            "shallowPercent0To30m": round(shallow / max(1, len(heights)) * 100.0, 4),
        }

    def world_coord(index: int) -> tuple[float, float]:
        iy = index // resolution
        ix = index - iy * resolution
        return origin_x - half + ix * step, origin_z - half + iy * step

    def select_waypoint(
        waypoint_id: str,
        label: str,
        predicate,
        score,
    ) -> dict[str, object] | None:
        best_index = -1
        best_score = -1.0e30
        for index, depth in enumerate(depths):
            zone = zones[index]
            m = masks[index]
            if not predicate(index, depth, zone, m):
                continue
            s = score(index, depth, zone, m)
            if s > best_score:
                best_score = s
                best_index = index

        if best_index < 0:
            return None

        x, z = world_coord(best_index)
        zone = zones[best_index]
        m = masks[best_index]
        return {
            "id": waypoint_id,
            "label": label,
            "worldX": round(x, 2),
            "worldZ": round(z, 2),
            "heightMeters": round(heights[best_index], 2),
            "depthMeters": round(depths[best_index], 2),
            "zone": ZONE_NAMES[zone],
            "slope01": round(slope_values[best_index], 4),
            "curvature01": round(curvature_shaded[best_index], 4),
            "erosionFlow01": round(erosion_values[best_index], 4),
            "sediment01": round(sediment_values[best_index], 4),
            "seep01": round(seep_values[best_index], 4),
            "masks": {
                "shelf": round(m.shelf, 4),
                "shelfBreak": round(m.shelf_break, 4),
                "ridge": round(m.ridge, 4),
                "trench": round(m.trench, 4),
                "basin": round(m.basin, 4),
                "fault": round(m.fault, 4),
            },
        }

    waypoint_specs = [
        (
            "shelf_approach",
            "Player-scale photic shelf approach",
            lambda i, d, z, m: z == 1 and d < 220.0,
            lambda i, d, z, m: m.shelf * 2.0 + (1.0 - slope_values[i]),
        ),
        (
            "shallow_reef_candidate",
            "Shallow coral reef approach candidate",
            lambda i, d, z, m: d <= 25.0 and heights[i] > -18.0 and m.shelf > 0.40,
            lambda i, d, z, m: heights[i] + m.shelf * 20.0 - slope_values[i] * 5.0,
        ),
        (
            "shelf_break_descent",
            "Shelf break descent corridor",
            lambda i, d, z, m: z == 2 and 180.0 < d < 1600.0,
            lambda i, d, z, m: m.shelf_break * 1.6 + slope_values[i] + erosion_values[i] * 0.4,
        ),
        (
            "canyon_sediment_fan",
            "Canyon mouth into sediment fan",
            lambda i, d, z, m: 500.0 < d < 3600.0 and erosion_values[i] > 0.38 and sediment_values[i] > 0.48,
            lambda i, d, z, m: erosion_values[i] * 1.5 + sediment_values[i] + m.basin * 0.6,
        ),
        (
            "ridge_flank",
            "Fault-controlled ridge flank traversal",
            lambda i, d, z, m: z == 3 and d > 650.0,
            lambda i, d, z, m: m.ridge * 1.5 + slope_values[i] + curvature_shaded[i] * 0.4,
        ),
        (
            "trench_floor",
            "Brine trench floor",
            lambda i, d, z, m: z == 4,
            lambda i, d, z, m: d - slope_values[i] * 600.0 + m.trench * 800.0,
        ),
        (
            "basin_floor",
            "Low-slope basin floor",
            lambda i, d, z, m: z in (6, 8, 5) and d > 1900.0 and slope_values[i] < 0.35,
            lambda i, d, z, m: m.basin * 1.4 + sediment_values[i] + d / max_depth,
        ),
        (
            "voxel_seam_candidate",
            "High-curvature voxel overlay seam candidate",
            lambda i, d, z, m: z != 4 and 600.0 < d < 3900.0 and (m.fault > 0.38 or curvature_shaded[i] > 0.45),
            lambda i, d, z, m: curvature_shaded[i] * 1.2 + m.fault + slope_values[i] * 0.4,
        ),
    ]
    proof_waypoints = []
    for spec in waypoint_specs:
        waypoint = select_waypoint(*spec)
        if waypoint is not None:
            proof_waypoints.append(waypoint)

    field_pixels: dict[str, list[tuple[int, int, int]]] = {
        "terrain_relief_proof": [
            shade_terrain_relief_proof(heights[i], min_height, max_height, slope_values[i], curvature_shaded[i], erosion_values[i])
            for i in range(len(depths))
        ],
        "raw_elevation": [shade_raw_elevation(h, min_height, max_height) for h in heights],
        "hillshade": [
            shade_hillshade(heights[i], min_height, max_height, slope_values[i], curvature_shaded[i])
            for i in range(len(depths))
        ],
        "local_relief": [
            shade_local_relief(slope_values[i], curvature_shaded[i], erosion_values[i])
            for i in range(len(depths))
        ],
        "slope": [heat(v) for v in slope_values],
        "curvature": [blend((47, 79, 116), (201, 179, 133), v) for v in curvature_shaded],
        "erosion_flow": [blend((35, 52, 62), (196, 224, 218), v) for v in erosion_values],
        "material_regions": [
            surface_material_pixels[i]
            for i in range(len(depths))
        ],
        "terrain_control_rgba_fold": [terrain_control_pixels[i] for i in range(len(depths))],
        "geology_zones": [ZONE_COLORS[z] for z in zones],
        "ridge_fault_mask": [blend(gray(m.ridge), (94, 166, 142), m.fault * 0.55) for m in masks],
        "sediment_seep_mask": [blend((80, 72, 58), (182, 150, 98), sediment_values[i]) for i in range(len(sediment_values))],
        "meso_terrain_controls": [
            (
                int(42 + 190 * terrace_values[i]),
                int(38 + 176 * slump_values[i]),
                int(44 + 184 * tributary_values[i]),
            )
            for i in range(len(depths))
        ],
        "scatter_eligibility": [
            (
                int(38 + 185 * nodule_values[i]),
                int(42 + 180 * reef_values[i]),
                int(44 + 180 * hard_rock_values[i]),
            )
            for i in range(len(depths))
        ],
        "voxel_seam_mask": [heat(v) for v in voxel_seam_values],
        "shelf_mask": [gray(m.shelf) for m in masks],
        "trench_mask": [gray(m.trench) for m in masks],
        "depth_strata": [shade_depth_strata(d) for d in depths],
        "waterline_sweep": [shade_waterline_sweep(h) for h in heights],
        "height_bathymetry_legacy": [shade_signed_height(h, min_height, max_height) for h in heights],
        "relief_shading_legacy": [
            shade_relief(heights[i], min_height, max_height, slope_values[i], curvature_shaded[i])
            for i in range(len(depths))
        ],
    }

    origin_suffix = ""
    if abs(origin_x) > 1.0e-5 or abs(origin_z) > 1.0e-5:
        origin_suffix = f"_OriginX{format_origin_token(origin_x)}_Z{format_origin_token(origin_z)}"
    run_name = f"Seed{args.seed:08d}_Runtime{args.runtime_seed}_Extent{int(params.extent)}m_Res{resolution}{origin_suffix}"
    out_dir = Path(args.output) if args.output else DEFAULT_OUTPUT / run_name
    out_dir.mkdir(parents=True, exist_ok=True)

    files: dict[str, str] = {}
    hashes: dict[str, str] = {}
    for name, pixels in field_pixels.items():
        path = out_dir / f"MG_{name}.png"
        write_png_rgb(path, resolution, resolution, pixels)
        files[name] = str(path.relative_to(ROOT).as_posix())
        hashes[name] = hashlib.sha256(path.read_bytes()).hexdigest()

    panel = max(192, min(384, resolution))
    contact_columns = 4
    contact_rows = 3
    contact_width = panel * contact_columns
    contact_height = panel * contact_rows
    contact_pixels: list[tuple[int, int, int]] = [(0, 0, 0)] * (contact_width * contact_height)
    names = list(field_pixels.keys())
    for index, name in enumerate(names[: contact_columns * contact_rows]):
        source = field_pixels[name]
        ox = (index % contact_columns) * panel
        oy = (index // contact_columns) * panel
        for py in range(panel):
            sy = int(py * resolution / panel)
            for px in range(panel):
                sx = int(px * resolution / panel)
                contact_pixels[(oy + py) * contact_width + ox + px] = source[sy * resolution + sx]
    contact_path = out_dir / "MG_contact_sheet_4x3.png"
    write_png_rgb(contact_path, contact_width, contact_height, contact_pixels)
    files["contact_sheet"] = str(contact_path.relative_to(ROOT).as_posix())
    hashes["contact_sheet"] = hashlib.sha256(contact_path.read_bytes()).hexdigest()

    terrain_contact_names = [
        "terrain_relief_proof",
        "raw_elevation",
        "hillshade",
        "local_relief",
        "slope",
        "curvature",
        "erosion_flow",
        "height_bathymetry_legacy",
        "meso_terrain_controls",
        "scatter_eligibility",
        "relief_shading_legacy",
        "sediment_seep_mask",
    ]
    terrain_rows = 3
    terrain_width = panel * contact_columns
    terrain_height = panel * terrain_rows
    terrain_contact_pixels: list[tuple[int, int, int]] = [(0, 0, 0)] * (terrain_width * terrain_height)
    for index, name in enumerate(terrain_contact_names):
        source = field_pixels[name]
        ox = (index % contact_columns) * panel
        oy = (index // contact_columns) * panel
        for py in range(panel):
            sy = int(py * resolution / panel)
            for px in range(panel):
                sx = int(px * resolution / panel)
                terrain_contact_pixels[(oy + py) * terrain_width + ox + px] = source[sy * resolution + sx]
    terrain_contact_path = out_dir / "MG_contact_sheet_terrain_4x3.png"
    write_png_rgb(terrain_contact_path, terrain_width, terrain_height, terrain_contact_pixels)
    files["terrain_contact_sheet"] = str(terrain_contact_path.relative_to(ROOT).as_posix())
    hashes["terrain_contact_sheet"] = hashlib.sha256(terrain_contact_path.read_bytes()).hexdigest()

    window_min_x = origin_x - half
    window_min_z = origin_z - half
    window_max_x = origin_x + half
    window_max_z = origin_z + half
    chunk_min_x = math.floor(window_min_x / params.chunk)
    chunk_max_x = math.floor((window_max_x - 0.001) / params.chunk)
    chunk_min_z = math.floor(window_min_z / params.chunk)
    chunk_max_z = math.floor((window_max_z - 0.001) / params.chunk)
    chunk_count_x = chunk_max_x - chunk_min_x + 1
    chunk_count_z = chunk_max_z - chunk_min_z + 1
    chunk_count_axis = max(chunk_count_x, chunk_count_z)
    chunk_entries = []
    for world_chunk_z in range(chunk_min_z, chunk_max_z + 1):
        min_z = world_chunk_z * params.chunk
        max_z = min_z + params.chunk
        center_z = (min_z + max_z) * 0.5
        for world_chunk_x in range(chunk_min_x, chunk_max_x + 1):
            min_x = world_chunk_x * params.chunk
            max_x = min_x + params.chunk
            center_x = (min_x + max_x) * 0.5
            ix = max(0, min(analysis_resolution - 1, int(round((center_x - origin_x + half) / analysis_step))))
            iy = max(0, min(analysis_resolution - 1, int(round((center_z - origin_z + half) / analysis_step))))
            sample_index = iy * analysis_resolution + ix
            distance_from_origin = math.hypot(center_x - origin_x, center_z - origin_z)
            proof_lod_hint = 0 if distance_from_origin < 3072.0 else 1 if distance_from_origin < 8192.0 else 2
            chunk_entries.append(
                {
                    "artifactId": build_chunk_artifact_id(world_chunk_x, world_chunk_z, combined_seed, ARTIFACT_VERSION, params.chunk),
                    "worldChunkX": world_chunk_x,
                    "worldChunkZ": world_chunk_z,
                    "chunkSizeMeters": params.chunk,
                    "boundsMinX": round(min_x, 2),
                    "boundsMinZ": round(min_z, 2),
                    "boundsMaxX": round(max_x, 2),
                    "boundsMaxZ": round(max_z, 2),
                    "proofLodHint": proof_lod_hint,
                    "dominantZone": ZONE_NAMES[zones[sample_index]],
                    "centerHeightMeters": round(heights[sample_index], 2),
                    "centerDepthMeters": round(depths[sample_index], 2),
                    "terrainControls": {
                        "terrace": round(terrace_values[sample_index], 4),
                        "slump": round(slump_values[sample_index], 4),
                        "tributary": round(tributary_values[sample_index], 4),
                        "nodule": round(nodule_values[sample_index], 4),
                        "reef": round(reef_values[sample_index], 4),
                        "hardRock": round(hard_rock_values[sample_index], 4),
                        "voxelSeam": round(voxel_seam_values[sample_index], 4),
                    },
                }
            )

    chunk_manifest = {
        "artifact": "WorldMacroGeologyChunkManifest",
        "artifactVersion": ARTIFACT_VERSION,
        "sourceManifest": "WorldMacroGeologyPreviewManifest.json",
        "combinedSeed": combined_seed,
        "originMeters": {"x": origin_x, "z": origin_z},
        "chunkSizeMeters": params.chunk,
        "chunkCountAxis": chunk_count_axis,
        "chunkCountX": chunk_count_x,
        "chunkCountZ": chunk_count_z,
        "chunkCount": len(chunk_entries),
        "runtimeChunkRange": {
            "minX": chunk_min_x,
            "maxX": chunk_max_x,
            "minZ": chunk_min_z,
            "maxZ": chunk_max_z,
        },
        "storageContract": {
            "keySource": "artifactId = hash(seed, artifactVersion, chunkSizeMeters, worldChunkX, worldChunkZ)",
            "runtimeMissPolicy": "load baked chunk when present; otherwise generate deterministic chunk from WorldMacroGeologyFields and enqueue bake/write-back",
            "saveLoadPolicy": "save references seed/artifactVersion/chunk artifact ids; do not serialize full macro field into player save",
            "stalePolicy": "mismatched seed or artifactVersion invalidates chunk artifacts and forces deterministic rebuild",
        },
        "chunks": chunk_entries,
    }
    chunk_manifest_path = out_dir / "WorldMacroGeologyChunkManifest.json"
    chunk_manifest_path.write_text(json.dumps(chunk_manifest, indent=2, sort_keys=True), encoding="utf-8")
    files["chunk_manifest"] = str(chunk_manifest_path.relative_to(ROOT).as_posix())
    hashes["chunk_manifest"] = hashlib.sha256(chunk_manifest_path.read_bytes()).hexdigest()

    manifest = {
        "artifact": "WorldMacroGeologyPreview",
        "artifactVersion": ARTIFACT_VERSION,
        "status": "PREVIEW_SOURCE_OF_TRUTH_CANDIDATE",
        "authoringSeed": args.seed,
        "runtimeSeed": args.runtime_seed,
        "combinedSeed": combined_seed,
        "originMeters": {"x": origin_x, "z": origin_z},
        "extentMeters": params.extent,
        "areaSquareKilometers": round((params.extent * params.extent) / 1_000_000.0, 3),
        "chunkSizeMeters": params.chunk,
        "chunkCountAxis": chunk_count_axis,
        "chunkCountX": chunk_count_x,
        "chunkCountZ": chunk_count_z,
        "resolution": resolution,
        "workers": worker_count,
        "metersPerPixel": params.extent / resolution,
        "previewWindow": {
            "meaning": "minimum authored proof window, not the procedural world edge",
            "proceduralContinuationBeyondWindow": True,
            "coordinateSpace": "AUP/world XZ meters",
            "originMeters": {"x": origin_x, "z": origin_z},
            "boundsMinMeters": {"x": origin_x - half, "z": origin_z - half},
            "boundsMaxMeters": {"x": origin_x + half, "z": origin_z + half},
        },
        "designConstraints": {
            "typicalDepthMeters": "relative relief target; water level and exposed island count are calibrated later",
            "maxDepthMeters": params.hadal_depth,
            "reefAndCoralReefUse": "future eligibility/material/scatter masks only; not a circular height driver",
            "macroForms": [
                "continental or volcanic shelves",
                "soft and broken shelf breaks",
                "fault-controlled ridge chains",
                "reef-capable shelves as masks, not height blobs",
                "rolling hills",
                "sediment basins",
                "localized brine trenches",
            ],
            "gameplayCameraTruth": "bird-eye is proof only; player-scale approach waypoints drive scene cameras later",
        },
        "heightMeters": {"min": min_height, "max": max_height},
        "depthMeters": {"min": min_depth, "max": max_depth},
        "depthStrataCounts": depth_strata_counts,
        "depthBandCountsMeters": depth_band_counts,
        "waterLevelSweepMeters": water_level_sweep,
        "zoneCounts": zone_counts,
        "surfaceMaterialContract": {
            "version": 1,
            "source": "WorldTerrainSurfaceMaterialResolver",
            "logicalClasses": list(SURFACE_MATERIAL_COLORS.keys()),
            "dominantCounts": surface_material_counts,
            "terrainControlFold": "R=rock, G=sand, B=silt, A=deposition; preview stores RGB where blue=max(B,A)",
            "runtimePath": "WorldProceduralTerrainSlopeCavitySplatmapJob -> HectonTerrainSplatmapMapMagicNode -> TerrainMaster _TerrainControlRGBA",
        },
        "playerApproachProofWaypoints": proof_waypoints,
        "runtimeContract": {
            "owner": "WorldMacroGeologyFields",
            "readers": [
                "TerrainChunkPagerRuntime",
                "MapMagicRuntimeBridge",
                "WorldProceduralFieldSampler",
                "WorldProceduralScatterDirector",
                "VoxelTerrainSeamBinder",
                "VoxelSurfaceNets",
                "PDAMapTab",
                "SaveManager seed validation",
                "Crash/telemetry proof paths",
            ],
            "failurePaths": [
                "missing artifact",
                "mismatched seed",
                "mismatched artifactVersion",
                "duplicate terrain owner",
                "stale chunk handle",
                "interrupted bake",
                "bad dimensions",
                "voxel overlay conflict",
            ],
        },
        "files": files,
        "sha256": hashes,
    }
    manifest_path = out_dir / "WorldMacroGeologyPreviewManifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2, sort_keys=True), encoding="utf-8")
    print(
        json.dumps(
            {
                "manifest": str(manifest_path.relative_to(ROOT).as_posix()),
                "terrainContact": files["terrain_contact_sheet"],
                "chunkManifest": files["chunk_manifest"],
                "areaSquareKilometers": manifest["areaSquareKilometers"],
                "chunkCountAxis": chunk_count_axis,
                "chunkCount": chunk_manifest["chunkCount"],
            },
            indent=2,
        )
    )
    return 0


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--seed", type=int, default=880031)
    parser.add_argument("--runtime-seed", type=int, default=0)
    parser.add_argument("--extent-m", type=float, default=30_000.0)
    parser.add_argument("--origin-x-m", type=float, default=0.0)
    parser.add_argument("--origin-z-m", type=float, default=0.0)
    parser.add_argument("--chunk-m", type=float, default=512.0)
    parser.add_argument("--resolution", type=int, default=192)
    parser.add_argument("--analysis-resolution", type=int, default=0)
    parser.add_argument("--workers", type=int, default=1)
    parser.add_argument("--detail-probe-m", type=float, default=120.0)
    parser.add_argument("--output", default="")
    return parser.parse_args()


if __name__ == "__main__":
    raise SystemExit(build_preview(parse_args()))
