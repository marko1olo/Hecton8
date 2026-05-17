"""Bake deterministic ore spawn LCG tables for HECTON-8.

The output is source JSON/CSV plus a fixed-layout little-endian binary cache.
Runtime consumers should treat the generated data as immutable table authority.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import struct
import zlib
from dataclasses import dataclass
from pathlib import Path
from typing import Any


ROOT_DIR = Path(__file__).resolve().parents[1]
MATRIX_INPUT = Path("Data/Economy/Resource_Distribution_Matrix.csv")
JSON_OUTPUT = Path("Data/Economy/Ore_Distribution.json")
HISTOGRAM_OUTPUT = Path("Data/Economy/Ore_Distribution_Histogram.csv")
BINARY_OUTPUT = Path("Data/Economy/Ore_Distribution.h8bin")
CSHARP_TEMPLATE_OUTPUT = Path("Docs/AgentLogs/OreLcgRuntimeStruct_RESOURCE_SPAWN_LCG_TABLES.md")

TABLE_VERSION_ID = "economy.ore_lcg_distribution.v1"
FNV_OFFSET = 2166136261
FNV_PRIME = 16777619
UINT32_MASK = 0xFFFFFFFF

LCG_MULTIPLIER = 1664525
LCG_INCREMENT = 1013904223
LCG_MODULUS_BITS = 32
LCG_MODULUS = 1 << LCG_MODULUS_BITS
TABLE_VERSION_HASH32 = 3968294156
DEFAULT_WORLD_SEED = 0x48454338
DEFAULT_SIMULATION_ITERATIONS = 1_000_000

SAFE_SHALLOWS_ID = "biome.safe_shallows"
TITANIUM_ID = "Data_TitaniumScrap"

MIN_DENSITY_U8 = 32
MAX_DENSITY_U8 = 224
MIN_CLUMP_U8 = 16
MAX_CLUMP_U8 = 240
SURFACE_PRESSURE_PA = 101_325
SEAWATER_DENSITY_KG_M3 = 1025
GRAVITY_M_S2 = 9.807
PASCALS_PER_METER = round(SEAWATER_DENSITY_KG_M3 * GRAVITY_M_S2)

BINARY_MAGIC = b"H8OL"
BINARY_VERSION = 1
BINARY_HEADER_FORMAT = "<4sHH14I"
BINARY_BIOME_FORMAT = "<IHHBBBBI"
BINARY_RESOURCE_FORMAT = "<IHBB"
BINARY_ULTRA_FORMAT = "<IHHHHI"
BINARY_HEADER_SIZE = struct.calcsize(BINARY_HEADER_FORMAT)
BINARY_BIOME_SIZE = struct.calcsize(BINARY_BIOME_FORMAT)
BINARY_RESOURCE_SIZE = struct.calcsize(BINARY_RESOURCE_FORMAT)
BINARY_ULTRA_SIZE = struct.calcsize(BINARY_ULTRA_FORMAT)

BIOME_ALIASES = {
    "biome.safe_shallows": "Scrap-Safe Shelf",
    "biome.kelp_karst": "Cable-Kelp Karst",
    "biome.sediment_drift": "Silt Conveyor",
    "biome.fossil_reef": "Dead Rig Reef",
    "biome.volcanic_glass": "Black Glass Foundry",
    "biome.thermal_vent": "Boiler Vent Field",
    "biome.magnetic_trench": "Compass-Kill Trench",
    "biome.brine_hadal": "Brine Coffin Shelf",
    "biome.glass_plains": "Shatter Plain",
    "biome.abyssal_rift": "Abyssal Split Mine",
}

RESOURCE_ALIASES = {
    "Data_TitaniumScrap": "Hull Scrap Titanium",
    "Data_Copper": "Wet Copper Vein",
    "Data_SilicaShards": "Cut-Glass Silica",
    "Data_FiberKelp": "Cable Fiber Kelp",
    "Data_HydrocarbonResin": "Black Resin Sludge",
    "Data_SulfurClumps": "Boiler Sulfur Clumps",
    "Data_SilverOre": "Dead-Wire Silver",
    "Data_GoldOre": "Pressure Gold Ore",
    "Data_LithiumCrystal": "Battery Lithium Crystal",
    "Data_NickelOre": "Hull Nickel Ore",
    "Data_CobaltAlloy": "Reactor Cobalt Alloy",
    "Data_ThermalGel": "Vent Thermal Gel",
    "Data_CarbonGraphite": "Seal Graphite Carbon",
    "Data_RareEarthDust": "Motor Rare-Earth Dust",
    "Data_AbyssalCrystal": "Abyssal Black Crystal",
}


@dataclass(frozen=True)
class SourceRow:
    biome_id: str
    biome_hash32: int
    biome_display_name: str
    depth_min_m: int
    depth_max_m: int
    distance_from_origin_m: int
    resource_id: str
    resource_hash32: int
    resource_display_name: str
    resource_tier: int
    source_weight_u16: int


@dataclass(frozen=True)
class BakedResource:
    resource_id: str
    resource_hash32: int
    resource_display_name: str
    industrial_alias: str
    resource_tier: int
    weight_u8: int
    cumulative_weight_u16: int


@dataclass(frozen=True)
class BakedBiome:
    biome_id: str
    biome_hash32: int
    biome_display_name: str
    industrial_alias: str
    depth_min_m: int
    depth_max_m: int
    mid_depth_m: int
    distance_from_origin_m: int
    pressure_mid_kpa: int
    pressure_mid_milli_atm: int
    base_density_u8: int
    clumping_factor_u8: int
    total_weight_u16: int
    source_total_weight_u16: int
    ultra_visual_seed_u32: int
    ultra_gradient_low_u16: int
    ultra_gradient_mid_u16: int
    ultra_gradient_high_u16: int
    ultra_harmonic_noise_u16: int
    resources: list[BakedResource]


def uint32(value: int) -> int:
    return value & UINT32_MASK


def fnv1a32(value: str) -> int:
    result = FNV_OFFSET
    for byte in value.encode("utf-16le"):
        result ^= byte
        result = uint32(result * FNV_PRIME)
    return result


def next_lcg(state: int) -> int:
    return uint32((uint32(state) * LCG_MULTIPLIER) + LCG_INCREMENT)


def multiply_high_to_range(value: int, upper_exclusive: int) -> int:
    if upper_exclusive <= 0:
        raise ValueError("upper_exclusive must be positive")
    return ((uint32(value) * int(upper_exclusive)) >> 32) & UINT32_MASK


def align16_length(value: int) -> int:
    return (value + 15) & ~15


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(65536), b""):
            digest.update(chunk)
    return digest.hexdigest()


def read_csv_rows(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8", newline="") as handle:
        return list(csv.DictReader(handle))


def load_source_matrix(root: Path) -> list[list[SourceRow]]:
    rows = read_csv_rows(root / MATRIX_INPUT)
    grouped: list[list[SourceRow]] = []
    index_by_biome: dict[str, int] = {}
    for row in rows:
        biome_id = row["biome_id"]
        source = SourceRow(
            biome_id=biome_id,
            biome_hash32=int(row["biome_hash32"]),
            biome_display_name=row["biome_display_name"],
            depth_min_m=int(row["depth_min_m"]),
            depth_max_m=int(row["depth_max_m"]),
            distance_from_origin_m=int(row["distance_from_origin_m"]),
            resource_id=row["resource_id"],
            resource_hash32=int(row["resource_hash32"]),
            resource_display_name=row["resource_display_name"],
            resource_tier=int(row["resource_tier"]),
            source_weight_u16=int(row["lcg_spawn_weight_u16"]),
        )
        if biome_id not in index_by_biome:
            index_by_biome[biome_id] = len(grouped)
            grouped.append([])
        grouped[index_by_biome[biome_id]].append(source)

    if len(grouped) != 10:
        raise ValueError(f"expected 10 biomes, got {len(grouped)}")
    for biome_rows in grouped:
        if len(biome_rows) != 15:
            raise ValueError(f"expected 15 resources for {biome_rows[0].biome_id}, got {len(biome_rows)}")
        for source in biome_rows:
            if source.biome_hash32 != fnv1a32(source.biome_id):
                raise ValueError(f"biome hash mismatch: {source.biome_id}")
            if source.resource_hash32 != fnv1a32(source.resource_id):
                raise ValueError(f"resource hash mismatch: {source.resource_id}")
    return grouped


def scale_to_byte(value: int, min_value: int, max_value: int, low: int, high: int) -> int:
    if max_value <= min_value:
        return low
    return low + ((value - min_value) * (high - low) // (max_value - min_value))


def pressure_for_mid_depth(mid_depth_m: int) -> tuple[int, int]:
    pressure_pa = SURFACE_PRESSURE_PA + (mid_depth_m * PASCALS_PER_METER)
    return pressure_pa // 1000, (pressure_pa * 1000) // SURFACE_PRESSURE_PA


def bake_safe_shallows_weights(rows: list[SourceRow]) -> list[int]:
    titanium_index = next(index for index, row in enumerate(rows) if row.resource_id == TITANIUM_ID)
    other_total = sum(row.source_weight_u16 for index, row in enumerate(rows) if index != titanium_index)
    weights: list[int] = []
    for index, row in enumerate(rows):
        if index == titanium_index:
            weights.append(255)
        else:
            weights.append(max(1, (row.source_weight_u16 * 255) // other_total))
    delta = 510 - sum(weights)
    if delta != 0:
        adjust_index = next(index for index, row in reversed(list(enumerate(rows))) if index != titanium_index and row.source_weight_u16 > 1)
        weights[adjust_index] += delta
    return weights


def ultra_values(biome_id: str, mid_depth_m: int, pressure_mid_kpa: int) -> tuple[int, int, int, int, int]:
    seed = next_lcg(fnv1a32(f"{biome_id}|ultra|{TABLE_VERSION_HASH32}"))
    low = min(65535, max(0, (mid_depth_m * 41) // 1024))
    mid = min(65535, low + max(3, pressure_mid_kpa // 600))
    high = min(65535, mid + max(4, pressure_mid_kpa // 500))
    harmonic = next_lcg(seed ^ pressure_mid_kpa) & 0xFFFF
    return seed, low, mid, high, harmonic


def bake_biomes(grouped_rows: list[list[SourceRow]]) -> list[BakedBiome]:
    source_totals = [sum(row.source_weight_u16 for row in rows) for rows in grouped_rows]
    min_total = min(source_totals)
    max_total = max(source_totals)
    mid_depths = [(rows[0].depth_min_m + rows[0].depth_max_m) // 2 for rows in grouped_rows]
    pressures = [pressure_for_mid_depth(mid_depth)[0] for mid_depth in mid_depths]
    min_pressure = min(pressures)
    max_pressure = max(pressures)

    biomes: list[BakedBiome] = []
    for biome_index, rows in enumerate(grouped_rows):
        first = rows[0]
        mid_depth = mid_depths[biome_index]
        pressure_kpa, pressure_milli_atm = pressure_for_mid_depth(mid_depth)
        base_density = scale_to_byte(source_totals[biome_index], min_total, max_total, MIN_DENSITY_U8, MAX_DENSITY_U8)
        clump = scale_to_byte(pressure_kpa, min_pressure, max_pressure, MIN_CLUMP_U8, MAX_CLUMP_U8)
        weights = bake_safe_shallows_weights(rows) if first.biome_id == SAFE_SHALLOWS_ID else [min(255, max(1, row.source_weight_u16)) for row in rows]

        cumulative = 0
        resources: list[BakedResource] = []
        for row, weight in zip(rows, weights):
            cumulative += weight
            resources.append(
                BakedResource(
                    resource_id=row.resource_id,
                    resource_hash32=row.resource_hash32,
                    resource_display_name=row.resource_display_name,
                    industrial_alias=RESOURCE_ALIASES.get(row.resource_id, row.resource_display_name),
                    resource_tier=row.resource_tier,
                    weight_u8=weight,
                    cumulative_weight_u16=cumulative,
                )
            )
        ultra_seed, ultra_low, ultra_mid, ultra_high, ultra_harmonic = ultra_values(first.biome_id, mid_depth, pressure_kpa)
        biomes.append(
            BakedBiome(
                biome_id=first.biome_id,
                biome_hash32=first.biome_hash32,
                biome_display_name=first.biome_display_name,
                industrial_alias=BIOME_ALIASES.get(first.biome_id, first.biome_display_name),
                depth_min_m=first.depth_min_m,
                depth_max_m=first.depth_max_m,
                mid_depth_m=mid_depth,
                distance_from_origin_m=first.distance_from_origin_m,
                pressure_mid_kpa=pressure_kpa,
                pressure_mid_milli_atm=pressure_milli_atm,
                base_density_u8=base_density,
                clumping_factor_u8=clump,
                total_weight_u16=cumulative,
                source_total_weight_u16=source_totals[biome_index],
                ultra_visual_seed_u32=ultra_seed,
                ultra_gradient_low_u16=ultra_low,
                ultra_gradient_mid_u16=ultra_mid,
                ultra_gradient_high_u16=ultra_high,
                ultra_harmonic_noise_u16=ultra_harmonic,
                resources=resources,
            )
        )
    return biomes


def resource_order(biomes: list[BakedBiome]) -> list[dict[str, int | str]]:
    return [
        {
            "resource_id": resource.resource_id,
            "resource_hash32": resource.resource_hash32,
            "resource_display_name": resource.resource_display_name,
            "industrial_alias": resource.industrial_alias,
            "resource_tier": resource.resource_tier,
        }
        for resource in biomes[0].resources
    ]


def flatten_weights(biomes: list[BakedBiome]) -> list[int]:
    return [resource.weight_u8 for biome in biomes for resource in biome.resources]


def flatten_cumulative(biomes: list[BakedBiome]) -> list[int]:
    return [resource.cumulative_weight_u16 for biome in biomes for resource in biome.resources]


def count_fnv_collisions(biomes: list[BakedBiome]) -> int:
    seen: dict[int, str] = {TABLE_VERSION_HASH32: TABLE_VERSION_ID}
    collisions = 0
    for biome in biomes:
        if biome.biome_hash32 in seen and seen[biome.biome_hash32] != biome.biome_id:
            collisions += 1
        seen[biome.biome_hash32] = biome.biome_id
        for resource in biome.resources:
            if resource.resource_hash32 in seen and seen[resource.resource_hash32] != resource.resource_id:
                collisions += 1
            seen[resource.resource_hash32] = resource.resource_id
    return collisions


def validate_bake(biomes: list[BakedBiome]) -> dict[str, int | bool | str]:
    safe = next(biome for biome in biomes if biome.biome_id == SAFE_SHALLOWS_ID)
    titanium = next(resource for resource in safe.resources if resource.resource_id == TITANIUM_ID)
    return {
        "status": "LCG BAKED",
        "biome_count": len(biomes),
        "resource_count_per_biome": len(safe.resources),
        "weight_range_u8": all(0 <= resource.weight_u8 <= 255 for biome in biomes for resource in biome.resources),
        "cumulative_totals_match": all(biome.resources[-1].cumulative_weight_u16 == biome.total_weight_u16 for biome in biomes),
        "safe_shallows_titanium_weight_u8": titanium.weight_u8,
        "safe_shallows_total_weight_u16": safe.total_weight_u16,
        "safe_shallows_titanium_basis_points": (titanium.weight_u8 * 10000) // safe.total_weight_u16,
        "safe_shallows_titanium_exact_50": titanium.weight_u8 * 2 == safe.total_weight_u16,
        "fnv_collision_count": count_fnv_collisions(biomes),
        "table_version_hash_matches_schema_id": TABLE_VERSION_HASH32 == fnv1a32(TABLE_VERSION_ID),
    }


def select_resource_index(state: int, biome: BakedBiome) -> int:
    threshold = multiply_high_to_range(state, biome.total_weight_u16)
    for index, resource in enumerate(biome.resources):
        if threshold < resource.cumulative_weight_u16:
            return index
    return len(biome.resources) - 1


def build_histogram_rows(biomes: list[BakedBiome], iterations: int, world_seed: int) -> list[dict[str, int | str]]:
    rows: list[dict[str, int | str]] = []
    for biome in biomes:
        counts = [0] * len(biome.resources)
        state = uint32(world_seed ^ biome.biome_hash32 ^ TABLE_VERSION_HASH32)
        for roll_index in range(iterations):
            state = next_lcg(uint32(state + roll_index))
            counts[select_resource_index(state, biome)] += 1
        for resource, count in zip(biome.resources, counts):
            expected = (resource.weight_u8 * 10000) // biome.total_weight_u16
            simulated = (count * 10000) // iterations
            rows.append(
                {
                    "biome_id": biome.biome_id,
                    "biome_hash32": biome.biome_hash32,
                    "biome_display_name": biome.biome_display_name,
                    "resource_id": resource.resource_id,
                    "resource_hash32": resource.resource_hash32,
                    "resource_display_name": resource.resource_display_name,
                    "weight_u8": resource.weight_u8,
                    "cumulative_weight_u16": resource.cumulative_weight_u16,
                    "total_weight_u16": biome.total_weight_u16,
                    "expected_basis_points": expected,
                    "simulated_count": count,
                    "simulated_basis_points": simulated,
                    "delta_basis_points": simulated - expected,
                }
            )
    return rows


def biome_to_json(biome: BakedBiome) -> dict[str, Any]:
    return {
        "biome_id": biome.biome_id,
        "biome_hash32": biome.biome_hash32,
        "biome_display_name": biome.biome_display_name,
        "industrial_alias": biome.industrial_alias,
        "depth_min_m": biome.depth_min_m,
        "depth_max_m": biome.depth_max_m,
        "mid_depth_m": biome.mid_depth_m,
        "distance_from_origin_m": biome.distance_from_origin_m,
        "pressure_mid_kpa": biome.pressure_mid_kpa,
        "pressure_mid_milli_atm": biome.pressure_mid_milli_atm,
        "base_density_u8": biome.base_density_u8,
        "clumping_factor_u8": biome.clumping_factor_u8,
        "total_weight_u16": biome.total_weight_u16,
        "ultra_visual_seed_u32": biome.ultra_visual_seed_u32,
        "ultra_gradient_low_u16": biome.ultra_gradient_low_u16,
        "ultra_gradient_mid_u16": biome.ultra_gradient_mid_u16,
        "ultra_gradient_high_u16": biome.ultra_gradient_high_u16,
        "ultra_harmonic_noise_u16": biome.ultra_harmonic_noise_u16,
        "resources": [
            {
                "resource_id": resource.resource_id,
                "resource_hash32": resource.resource_hash32,
                "resource_display_name": resource.resource_display_name,
                "industrial_alias": resource.industrial_alias,
                "resource_tier": resource.resource_tier,
                "weight_u8": resource.weight_u8,
                "cumulative_weight_u16": resource.cumulative_weight_u16,
            }
            for resource in biome.resources
        ],
    }


def build_json_payload(
    root: Path,
    biomes: list[BakedBiome],
    histogram_rows: list[dict[str, int | str]],
    iterations: int,
    world_seed: int,
    binary_info: dict[str, int | str | bool],
) -> dict[str, Any]:
    return {
        "schema_id": TABLE_VERSION_ID,
        "schema_hash32": fnv1a32(TABLE_VERSION_ID),
        "table_version_hash32": TABLE_VERSION_HASH32,
        "status": "LCG BAKED",
        "source_path": str(MATRIX_INPUT).replace("\\", "/"),
        "source_sha256": sha256_file(root / MATRIX_INPUT),
        "hash_algorithm": "FNV-1a 32-bit over UTF-16LE bytes",
        "science_basis": {
            "probability_source": "authored economy resource matrix; not a fluid, optics, gas, or acoustic LUT",
            "hydrostatic_pressure": "hydrostatic_pressure_pa = 101325 + depth_m * ((1025 kg/m^3 * 9.807 m/s^2) rounded to integer Pa/m)",
            "beer_lambert": "not used for ore authority; reserved for optics/fog tables",
            "dalton": "not used for ore authority; reserved for gas physiology tables",
            "sabine": "not used for ore authority; reserved for acoustic room response tables",
            "density_derivation": "base_density_u8 is source matrix total weight scaled across the 10 authored biomes",
            "clump_derivation": "clumping_factor_u8 is hydrostatic mid-depth pressure scaled across the 10 authored biomes",
            "byte_scale_derivation": "density floor/ceiling reserve 1/8 of the 256-step byte domain for no-spawn and overcap sentinels; clump floor/ceiling reserve 1/16 for finer Ultra vein dressing headroom",
            "density_u8_range": [MIN_DENSITY_U8, MAX_DENSITY_U8],
            "clump_u8_range": [MIN_CLUMP_U8, MAX_CLUMP_U8],
            "source_formula": "rarity_score = base_rarity + (distance_from_origin_m / 1000) * depth_multiplier * resource_depth_bias",
        },
        "lcg": {
            "multiplier_a": LCG_MULTIPLIER,
            "increment_c": LCG_INCREMENT,
            "modulus_m": LCG_MODULUS,
            "modulus_bits": LCG_MODULUS_BITS,
            "modulus_mask_hex": "0xffffffff",
            "selection_mapping": "multiply_high_to_range",
            "forbidden_mapping": "modulo",
        },
        "runtime_seed_contract": {
            "inputs": ["world_seed", "stable_chunk_or_aup_id", "table_version_hash32", "roll_index", "explicit_salt"],
            "forbidden_inputs": ["UnityEngine.Random", "System.Random", "wall_clock_time", "frame_time", "Transform.position"],
        },
        "resource_order": resource_order(biomes),
        "density_map_u8": [biome.base_density_u8 for biome in biomes],
        "clumping_factors_u8": [biome.clumping_factor_u8 for biome in biomes],
        "pressure_mid_kpa": [biome.pressure_mid_kpa for biome in biomes],
        "pressure_mid_milli_atm": [biome.pressure_mid_milli_atm for biome in biomes],
        "weight_matrix_u8_flat": flatten_weights(biomes),
        "cumulative_matrix_u16_flat": flatten_cumulative(biomes),
        "quality_tiers": {
            "minimal_toaster": {
                "fields": ["biome_hash32", "base_density_u8", "clumping_factor_u8", "total_weight_u16", "weight_matrix_u8_flat"],
                "selection": "one LCG step, multiply-high threshold, cumulative ushort compare",
            },
            "ultra_overkill": {
                "fields": [
                    "ultra_visual_seed_u32",
                    "ultra_gradient_low_u16",
                    "ultra_gradient_mid_u16",
                    "ultra_gradient_high_u16",
                    "ultra_harmonic_noise_u16",
                ],
                "authority": "visual-only vein dressing after deterministic resource selection",
            },
        },
        "biomes": [biome_to_json(biome) for biome in biomes],
        "binary_cache": binary_info,
        "simulation": {
            "iterations_per_biome": iterations,
            "world_seed": world_seed,
            "histogram_path": str(HISTOGRAM_OUTPUT).replace("\\", "/"),
            "safe_shallows_titanium_simulated_count": next(
                int(row["simulated_count"])
                for row in histogram_rows
                if row["biome_id"] == SAFE_SHALLOWS_ID and row["resource_id"] == TITANIUM_ID
            ),
        },
        "validation": validate_bake(biomes),
    }


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(payload, handle, ensure_ascii=True, separators=(",", ":"), sort_keys=False)
        handle.write("\n")


def write_histogram(path: Path, rows: list[dict[str, int | str]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    fieldnames = (
        "biome_id",
        "biome_hash32",
        "biome_display_name",
        "resource_id",
        "resource_hash32",
        "resource_display_name",
        "weight_u8",
        "cumulative_weight_u16",
        "total_weight_u16",
        "expected_basis_points",
        "simulated_count",
        "simulated_basis_points",
        "delta_basis_points",
    )
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def append_padding(buffer: bytearray) -> None:
    target = align16_length(len(buffer))
    if target > len(buffer):
        buffer.extend(b"\x00" * (target - len(buffer)))


def write_binary(path: Path, biomes: list[BakedBiome]) -> dict[str, int | str | bool]:
    path.parent.mkdir(parents=True, exist_ok=True)
    resource_count = sum(len(biome.resources) for biome in biomes)
    buffer = bytearray(b"\x00" * BINARY_HEADER_SIZE)

    biome_offset = len(buffer)
    resource_offset = biome_offset + len(biomes) * BINARY_BIOME_SIZE
    minimal_offset = resource_offset + resource_count * BINARY_RESOURCE_SIZE
    minimal_density_bytes = len(biomes)
    minimal_clump_bytes = len(biomes)
    minimal_total_weight_bytes = len(biomes) * 2
    minimal_weight_matrix_bytes = resource_count
    minimal_payload_bytes = minimal_density_bytes + minimal_clump_bytes + minimal_total_weight_bytes + minimal_weight_matrix_bytes
    minimal_size = align16_length(minimal_payload_bytes)
    ultra_offset = minimal_offset + minimal_size

    resource_cursor = 0
    for biome in biomes:
        buffer.extend(
            struct.pack(
                BINARY_BIOME_FORMAT,
                biome.biome_hash32,
                resource_cursor,
                biome.total_weight_u16,
                len(biome.resources),
                biome.base_density_u8,
                biome.clumping_factor_u8,
                0,
                biome.ultra_visual_seed_u32,
            )
        )
        resource_cursor += len(biome.resources)
    append_padding(buffer)
    if len(buffer) != resource_offset:
        raise ValueError("resource offset mismatch")

    for biome in biomes:
        for resource in biome.resources:
            buffer.extend(
                struct.pack(
                    BINARY_RESOURCE_FORMAT,
                    resource.resource_hash32,
                    resource.cumulative_weight_u16,
                    resource.weight_u8,
                    resource.resource_tier,
                )
            )
    append_padding(buffer)
    if len(buffer) != minimal_offset:
        raise ValueError("minimal offset mismatch")

    for biome in biomes:
        buffer.extend(struct.pack("<B", biome.base_density_u8))
    for biome in biomes:
        buffer.extend(struct.pack("<B", biome.clumping_factor_u8))
    for biome in biomes:
        buffer.extend(struct.pack("<H", biome.total_weight_u16))
    for biome in biomes:
        for resource in biome.resources:
            buffer.extend(struct.pack("<B", resource.weight_u8))
    append_padding(buffer)
    if len(buffer) != ultra_offset:
        raise ValueError("ultra offset mismatch")

    for biome in biomes:
        buffer.extend(
            struct.pack(
                BINARY_ULTRA_FORMAT,
                biome.ultra_visual_seed_u32,
                biome.ultra_gradient_low_u16,
                biome.ultra_gradient_mid_u16,
                biome.ultra_gradient_high_u16,
                biome.ultra_harmonic_noise_u16,
                0,
            )
        )
    append_padding(buffer)

    file_size = len(buffer)
    payload_crc = zlib.crc32(buffer[BINARY_HEADER_SIZE:]) & UINT32_MASK
    header = struct.pack(
        BINARY_HEADER_FORMAT,
        BINARY_MAGIC,
        BINARY_VERSION,
        BINARY_HEADER_SIZE,
        0x01020304,
        TABLE_VERSION_HASH32,
        LCG_MULTIPLIER,
        LCG_INCREMENT,
        LCG_MODULUS_BITS,
        len(biomes),
        resource_count,
        biome_offset,
        resource_offset,
        minimal_offset,
        ultra_offset,
        file_size,
        payload_crc,
        count_fnv_collisions(biomes),
    )
    buffer[:BINARY_HEADER_SIZE] = header
    path.write_bytes(buffer)
    return {
        "path": str(BINARY_OUTPUT).replace("\\", "/"),
        "format": "H8OL",
        "version": BINARY_VERSION,
        "endian": "<",
        "header_bytes": BINARY_HEADER_SIZE,
        "biome_record_bytes": BINARY_BIOME_SIZE,
        "resource_record_bytes": BINARY_RESOURCE_SIZE,
        "ultra_record_bytes": BINARY_ULTRA_SIZE,
        "file_size_bytes": file_size,
        "aligned_16_bytes": file_size % 16 == 0,
        "biome_offset": biome_offset,
        "resource_offset": resource_offset,
        "minimal_lod_offset": minimal_offset,
        "minimal_lod_bytes": minimal_size,
        "minimal_lod_payload_bytes": minimal_payload_bytes,
        "minimal_density_offset": minimal_offset,
        "minimal_clump_offset": minimal_offset + minimal_density_bytes,
        "minimal_total_weight_offset": minimal_offset + minimal_density_bytes + minimal_clump_bytes,
        "minimal_weight_matrix_offset": minimal_offset + minimal_density_bytes + minimal_clump_bytes + minimal_total_weight_bytes,
        "minimal_weight_matrix_bytes": minimal_weight_matrix_bytes,
        "minimal_lod_schema": "density_u8[biome_count],clump_u8[biome_count],total_weight_u16[biome_count],weight_u8[resource_count],pad16",
        "ultra_visual_offset": ultra_offset,
        "payload_crc32": payload_crc,
        "sha256": sha256_file(path),
    }


def write_csharp_template(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    text = f"""# Ore LCG Runtime Struct Template

Evidence class: STATIC_DOC
Owner handoff: SHINOBU runtime agents
Generated by: `Tools/OreLcgBaker.py`

```csharp
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct OreLcgBiomeRecord
{{
    public uint BiomeHash32;
    public ushort ResourceOffset;
    public ushort TotalWeightU16;
    public byte ResourceCount;
    public byte BaseDensityU8;
    public byte ClumpingFactorU8;
    public byte Flags;
    public uint UltraVisualSeedU32;
}}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct OreLcgResourceRecord
{{
    public uint ResourceHash32;
    public ushort CumulativeWeightU16;
    public byte WeightU8;
    public byte ResourceTier;
}}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct OreLcgBinaryHeader
{{
    public uint Magic;
    public ushort Version;
    public ushort HeaderBytes;
    public uint EndianMarker;
    public uint TableVersionHash32;
    public uint MultiplierA;
    public uint IncrementC;
    public uint ModulusBits;
    public uint BiomeCount;
    public uint ResourceCount;
    public uint BiomeOffset;
    public uint ResourceOffset;
    public uint MinimalLodOffset;
    public uint UltraVisualOffset;
    public uint FileSizeBytes;
    public uint PayloadCrc32;
    public uint FnvCollisionCount;
}}
```

Minimal LOD section at `MinimalLodOffset`:

```text
byte   BaseDensityU8[BiomeCount]
byte   ClumpingFactorU8[BiomeCount]
ushort TotalWeightU16[BiomeCount]
byte   WeightMatrixU8[ResourceCount]
byte   Pad16[...]
```

Runtime selection shape:

```csharp
uint state = previousState * {LCG_MULTIPLIER}u + {LCG_INCREMENT}u;
uint threshold = (uint)(((ulong)state * totalWeightU16) >> 32);
```

No managed references. No floating cumulative weights. No modulo range mapping.
Binary cache is little-endian only and every section offset is 16-byte aligned.
"""
    path.write_text(text, encoding="utf-8")


def bake(root: Path, iterations: int, world_seed: int) -> dict[str, Any]:
    grouped = load_source_matrix(root)
    biomes = bake_biomes(grouped)
    histogram_rows = build_histogram_rows(biomes, iterations, world_seed)
    binary_info = write_binary(root / BINARY_OUTPUT, biomes)
    payload = build_json_payload(root, biomes, histogram_rows, iterations, world_seed, binary_info)
    write_json(root / JSON_OUTPUT, payload)
    write_histogram(root / HISTOGRAM_OUTPUT, histogram_rows)
    write_csharp_template(root / CSHARP_TEMPLATE_OUTPUT)
    return payload


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=str(ROOT_DIR), help="Repository root.")
    parser.add_argument("--iterations", type=int, default=DEFAULT_SIMULATION_ITERATIONS, help="Spawn iterations per biome.")
    parser.add_argument("--world-seed", type=lambda value: int(value, 0), default=DEFAULT_WORLD_SEED, help="World seed.")
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    payload = bake(Path(args.root).resolve(), int(args.iterations), uint32(int(args.world_seed)))
    print("STATUS: LCG BAKED")
    print(f"json={JSON_OUTPUT.as_posix()}")
    print(f"binaryBytes={payload['binary_cache']['file_size_bytes']}")
    print(f"safeShallowsTitaniumBp={payload['validation']['safe_shallows_titanium_basis_points']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
