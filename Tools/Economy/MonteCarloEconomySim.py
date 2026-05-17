#!/usr/bin/env python3
"""Offline Monte Carlo audit for HECTON-8 economy loot tables.

This tool does not import Unity, touch C# code, or mutate source economy data.
It reads the current economy recipes and distribution matrix, mirrors the C#
LCG constants used by Burst jobs, and writes audit artifacts under Docs/Reports.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import struct
import zlib
from collections import Counter
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable


UINT32_MASK = 0xFFFFFFFF
LCG_MULTIPLIER = 1664525
LCG_INCREMENT = 1013904223
DEFAULT_WORLD_SEED = 0x48454338
DEFAULT_PLAYER_COUNT = 10_000
DEFAULT_MAX_NODES = 10_000
DEFAULT_THRESHOLD_MINUTES = 60.0
MONTE_CARLO_STEP_FLOOR = 1_000_000
FIRST_BASE_TARGETS = (
    "Module_FoundationKit",
    "Module_AirlockFrame",
    "Module_FabricatorBench",
    "Module_PowerRelay",
    "Module_O2Recycler",
)
TITANIUM_ID = "Data_TitaniumScrap"
COPPER_ID = "Data_Copper"
REPORT_PATH = Path("Docs/Reports/Economy_MonteCarlo_Audit.md")
RESULT_JSON_PATH = Path("Docs/Reports/Economy_MonteCarlo_Audit.json")
HISTOGRAM_PATH = Path("Docs/Reports/Economy_MonteCarlo_TimeToBase_Histogram.png")
TUNED_JSON_PATH = Path("Tools/Economy/Ore_Distribution_Tuned.json")
Q16_MAX = 65_535
HIGH_MICA_GLINT_PROBABILITY = 0.015
HIGH_WET_SOOT_OVERLAY_WEIGHT = 0.05
HIGH_MICA_GLINT_PROBABILITY_Q16 = int(round(HIGH_MICA_GLINT_PROBABILITY * Q16_MAX))
HIGH_WET_SOOT_OVERLAY_WEIGHT_Q16 = int(round(HIGH_WET_SOOT_OVERLAY_WEIGHT * Q16_MAX))
HIGH_CLUSTER_NOISE_OCTAVES = 4
HIGH_INSPECTION_GRADIENT_SAMPLES = 8
ULTRA_CLUSTER_NOISE_OCTAVES = 6
ULTRA_INSPECTION_GRADIENT_SAMPLES = 16
ULTRA_HARMONIC_DETAIL_BANDS = 5
ULTRA_RESOURCE_SCAR_HIGHLIGHT_LUT_SAMPLES = 32


@dataclass(frozen=True)
class DistributionEntry:
    biome_id: str
    biome_display_name: str
    distance_from_origin_m: float
    resource_id: str
    resource_hash32: int
    resource_display_name: str
    weight: int


@dataclass
class BiomeTable:
    biome_id: str
    biome_display_name: str
    distance_from_origin_m: float
    entries: list[DistributionEntry]
    cumulative_weights: list[int]
    total_weight: int


@dataclass
class DistributionTable:
    source_path: Path
    source_format: str
    source_sha256: str
    biomes: list[BiomeTable]


@dataclass(frozen=True)
class PlayerResult:
    seed: int
    success: bool
    nodes_to_base: int
    minutes_to_base: float
    copper_nodes: int
    copper_minutes: float
    titanium_nodes: int
    titanium_minutes: float
    final_biome_index: int


@dataclass(frozen=True)
class CompactBiomeTable:
    total_weight: int
    cumulative_weights: tuple[int, ...]
    resource_indices: tuple[int, ...]


@dataclass(frozen=True)
class CompactDistributionTable:
    biomes: tuple[CompactBiomeTable, ...]
    resource_count: int
    required_by_index: tuple[int, ...]
    demand_count: int
    copper_index: int
    titanium_index: int


def uint32(value: int) -> int:
    return value & UINT32_MASK


def next_lcg(state: int) -> int:
    """Mirror C# `state * 1664525u + 1013904223u` with uint overflow."""
    return uint32((state * LCG_MULTIPLIER) + LCG_INCREMENT)


def mix_u32(a: int, b: int) -> int:
    """Mirror DeployableSdfDrillMath.Mix from C#."""
    h = uint32(a ^ 0x9E3779B9)
    h = uint32(h ^ uint32(b + 0x85EBCA6B + uint32(h << 6) + (h >> 2)))
    h = uint32(h ^ (h >> 16))
    h = uint32(h * 0x7FEB352D)
    h = uint32(h ^ (h >> 15))
    h = uint32(h * 0x846CA68B)
    h = uint32(h ^ (h >> 16))
    return h if h != 0 else 0xA341316C


def multiply_high_to_range(value: int, range_exclusive: int) -> int:
    if range_exclusive <= 0:
        raise ValueError("range_exclusive must be positive")
    return ((value & UINT32_MASK) * range_exclusive) >> 32


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(65536), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_distribution(root: Path) -> DistributionTable:
    economy_dir = root / "Data" / "Economy"
    json_path = economy_dir / "Ore_Distribution.json"
    if json_path.exists():
        return load_json_distribution(json_path)

    csv_path = economy_dir / "Resource_Distribution_Matrix.csv"
    if not csv_path.exists():
        raise FileNotFoundError("missing Data/Economy/Ore_Distribution.json and Resource_Distribution_Matrix.csv")
    return load_csv_distribution(csv_path)


def load_json_distribution(path: Path) -> DistributionTable:
    data = json.loads(path.read_text(encoding="utf-8"))
    biome_tables: list[BiomeTable] = []
    for biome in data.get("biomes", []):
        entries: list[DistributionEntry] = []
        for resource in biome.get("resources", []):
            entries.append(
                DistributionEntry(
                    biome_id=str(biome["biome_id"]),
                    biome_display_name=str(biome.get("biome_display_name", biome["biome_id"])),
                    distance_from_origin_m=float(biome.get("distance_from_origin_m", 0.0)),
                    resource_id=str(resource["resource_id"]),
                    resource_hash32=int(resource.get("resource_hash32", 0)),
                    resource_display_name=str(resource.get("resource_display_name", resource["resource_id"])),
                    weight=int(resource.get("lcg_spawn_weight_u16", resource.get("weight_u8", resource.get("weight", 0)))),
                )
            )
        biome_tables.append(build_biome_table(entries))
    return DistributionTable(path, "json", sha256_file(path), sorted_biomes(biome_tables))


def load_csv_distribution(path: Path) -> DistributionTable:
    grouped: dict[str, list[DistributionEntry]] = {}
    with path.open("r", encoding="utf-8", newline="") as handle:
        for row in csv.DictReader(handle):
            entry = DistributionEntry(
                biome_id=row["biome_id"],
                biome_display_name=row["biome_display_name"],
                distance_from_origin_m=float(row["distance_from_origin_m"]),
                resource_id=row["resource_id"],
                resource_hash32=int(row["resource_hash32"]),
                resource_display_name=row["resource_display_name"],
                weight=int(row["lcg_spawn_weight_u16"]),
            )
            grouped.setdefault(entry.biome_id, []).append(entry)

    biome_tables = [build_biome_table(entries) for entries in grouped.values()]
    return DistributionTable(path, "csv", sha256_file(path), sorted_biomes(biome_tables))


def build_biome_table(entries: list[DistributionEntry]) -> BiomeTable:
    if not entries:
        raise ValueError("empty biome table")
    cumulative: list[int] = []
    total = 0
    for entry in entries:
        if entry.weight <= 0:
            raise ValueError(f"{entry.biome_id}/{entry.resource_id} has non-positive weight {entry.weight}")
        total += entry.weight
        cumulative.append(total)
    return BiomeTable(
        biome_id=entries[0].biome_id,
        biome_display_name=entries[0].biome_display_name,
        distance_from_origin_m=entries[0].distance_from_origin_m,
        entries=entries,
        cumulative_weights=cumulative,
        total_weight=total,
    )


def sorted_biomes(biomes: Iterable[BiomeTable]) -> list[BiomeTable]:
    ordered = sorted(biomes, key=lambda biome: (biome.distance_from_origin_m, biome.biome_id))
    if len(ordered) != 10:
        raise ValueError(f"expected 10 biomes, got {len(ordered)}")
    for biome in ordered:
        if len(biome.entries) != 15:
            raise ValueError(f"{biome.biome_id} expected 15 resources, got {len(biome.entries)}")
    return ordered


def load_recipes(root: Path) -> dict:
    path = root / "Data" / "Economy" / "Recipes.json"
    return json.loads(path.read_text(encoding="utf-8"))


def resolve_raw_demands(recipes_data: dict, targets: Iterable[str]) -> Counter[str]:
    recipes_by_result = {recipe["result"]["item_id"]: recipe for recipe in recipes_data["recipes"]}
    demands: Counter[str] = Counter()
    stack: list[str] = []

    def expand(item_id: str, quantity: int) -> None:
        if item_id in stack:
            cycle = " -> ".join(stack + [item_id])
            raise ValueError(f"recipe dependency cycle detected: {cycle}")
        recipe = recipes_by_result.get(item_id)
        if recipe is None:
            demands[item_id] += quantity
            return

        stack.append(item_id)
        for ingredient in recipe["ingredients"]:
            expand(ingredient["item_id"], quantity * int(ingredient["quantity"]))
        stack.pop()

    for target in targets:
        expand(target, 1)
    return demands


def validate_dependency_coverage(demands: Counter[str], distribution: DistributionTable) -> list[str]:
    covered = {entry.resource_id for biome in distribution.biomes for entry in biome.entries}
    return sorted(resource_id for resource_id in demands if resource_id not in covered)


def load_time_model(root: Path, demands: Counter[str]) -> tuple[float, float, str]:
    path = root / "Data" / "Economy" / "Time_To_First_Submarine.json"
    if not path.exists():
        return 0.135, 20.5, "fallback constants: 0.135 minutes/node + 20.5 overhead"

    data = json.loads(path.read_text(encoding="utf-8"))
    source_raw_total = sum(int(row["quantity"]) for row in data.get("raw_resources", []))
    harvest_minutes = float(data.get("assumptions", {}).get("mixed_node_harvest_minutes", 15.0))
    node_minutes = harvest_minutes / max(1, source_raw_total)
    route_minutes = float(data.get("assumptions", {}).get("route_travel_minutes", 14.0))
    handling_minutes = float(data.get("assumptions", {}).get("inventory_return_and_handling_minutes", 6.5))
    demand_total = sum(demands.values())
    source = (
        f"Time_To_First_Submarine.json: {harvest_minutes:.3f} harvest minutes / "
        f"{source_raw_total} source resources = {node_minutes:.6f} minutes/node; "
        f"overhead={route_minutes + handling_minutes:.3f}; first_base_raw_total={demand_total}"
    )
    return node_minutes, route_minutes + handling_minutes, source


def roll_resource(state: int, biome: BiomeTable) -> DistributionEntry:
    threshold = multiply_high_to_range(state, biome.total_weight)
    for index, cumulative in enumerate(biome.cumulative_weights):
        if threshold < cumulative:
            return biome.entries[index]
    return biome.entries[-1]


def build_compact_distribution(distribution: DistributionTable, demands: Counter[str]) -> CompactDistributionTable:
    resource_to_index: dict[str, int] = {}
    for biome in distribution.biomes:
        for entry in biome.entries:
            if entry.resource_id not in resource_to_index:
                resource_to_index[entry.resource_id] = len(resource_to_index)

    required = [0 for _ in range(len(resource_to_index))]
    for resource_id, quantity in demands.items():
        required[resource_to_index[resource_id]] = int(quantity)

    compact_biomes = []
    for biome in distribution.biomes:
        compact_biomes.append(
            CompactBiomeTable(
                total_weight=biome.total_weight,
                cumulative_weights=tuple(biome.cumulative_weights),
                resource_indices=tuple(resource_to_index[entry.resource_id] for entry in biome.entries),
            )
        )

    return CompactDistributionTable(
        biomes=tuple(compact_biomes),
        resource_count=len(resource_to_index),
        required_by_index=tuple(required),
        demand_count=sum(1 for quantity in required if quantity > 0),
        copper_index=resource_to_index[COPPER_ID],
        titanium_index=resource_to_index[TITANIUM_ID],
    )


def roll_resource_index(state: int, biome: CompactBiomeTable) -> int:
    threshold = ((state & UINT32_MASK) * biome.total_weight) >> 32
    for index, cumulative in enumerate(biome.cumulative_weights):
        if threshold < cumulative:
            return biome.resource_indices[index]
    return biome.resource_indices[-1]


def simulate_player_compact(
    player_index: int,
    compact: CompactDistributionTable,
    world_seed: int,
    max_nodes: int,
    node_minutes: float,
    overhead_minutes: float,
) -> PlayerResult:
    state = mix_u32(world_seed, ((player_index + 1) * 747796405) & UINT32_MASK)
    seed = state
    biome_index = 0
    inventory = [0 for _ in range(compact.resource_count)]
    remaining_demands = compact.demand_count
    copper_nodes = 0
    titanium_nodes = 0
    biome_count = len(compact.biomes)
    required_by_index = compact.required_by_index
    copper_index = compact.copper_index
    titanium_index = compact.titanium_index

    for node_index in range(1, max_nodes + 1):
        state = ((state * LCG_MULTIPLIER) + LCG_INCREMENT) & UINT32_MASK
        walk_roll = (state * 100) >> 32
        if walk_roll < 20:
            if biome_index > 0:
                biome_index -= 1
        elif walk_roll < 40 and biome_index + 1 < biome_count:
            biome_index += 1

        state = ((state * LCG_MULTIPLIER) + LCG_INCREMENT) & UINT32_MASK
        resource_index = roll_resource_index(state, compact.biomes[biome_index])
        current_quantity = inventory[resource_index] + 1
        inventory[resource_index] = current_quantity
        required_quantity = required_by_index[resource_index]

        if required_quantity > 0 and current_quantity == required_quantity:
            remaining_demands -= 1
        if resource_index == copper_index and copper_nodes == 0 and current_quantity >= required_quantity:
            copper_nodes = node_index
        if resource_index == titanium_index and titanium_nodes == 0 and current_quantity >= required_quantity:
            titanium_nodes = node_index

        if remaining_demands == 0:
            minutes = overhead_minutes + (node_index * node_minutes)
            copper_minutes = overhead_minutes + ((copper_nodes or node_index) * node_minutes)
            titanium_minutes = overhead_minutes + ((titanium_nodes or node_index) * node_minutes)
            return PlayerResult(seed, True, node_index, minutes, copper_nodes, copper_minutes, titanium_nodes, titanium_minutes, biome_index)

    minutes = overhead_minutes + (max_nodes * node_minutes)
    copper_minutes = overhead_minutes + ((copper_nodes or max_nodes) * node_minutes)
    titanium_minutes = overhead_minutes + ((titanium_nodes or max_nodes) * node_minutes)
    return PlayerResult(seed, False, max_nodes, minutes, copper_nodes, copper_minutes, titanium_nodes, titanium_minutes, biome_index)


def simulate_player(
    player_index: int,
    distribution: DistributionTable,
    demands: Counter[str],
    world_seed: int,
    max_nodes: int,
    node_minutes: float,
    overhead_minutes: float,
) -> PlayerResult:
    state = mix_u32(world_seed, uint32((player_index + 1) * 747796405))
    seed = state
    biome_index = 0
    inventory: Counter[str] = Counter()
    copper_nodes = 0
    titanium_nodes = 0
    demand_keys = tuple(demands.keys())

    for node_index in range(1, max_nodes + 1):
        state = next_lcg(state)
        walk_roll = multiply_high_to_range(state, 100)
        if walk_roll < 20:
            biome_index = max(0, biome_index - 1)
        elif walk_roll < 40:
            biome_index = min(len(distribution.biomes) - 1, biome_index + 1)

        state = next_lcg(state)
        mined = roll_resource(state, distribution.biomes[biome_index])
        inventory[mined.resource_id] += 1

        if mined.resource_id == COPPER_ID and copper_nodes == 0 and inventory[COPPER_ID] >= demands.get(COPPER_ID, 0):
            copper_nodes = node_index
        if mined.resource_id == TITANIUM_ID and titanium_nodes == 0 and inventory[TITANIUM_ID] >= demands.get(TITANIUM_ID, 0):
            titanium_nodes = node_index

        complete = True
        for resource_id in demand_keys:
            if inventory[resource_id] < demands[resource_id]:
                complete = False
                break
        if complete:
            minutes = overhead_minutes + (node_index * node_minutes)
            copper_minutes = overhead_minutes + ((copper_nodes or node_index) * node_minutes)
            titanium_minutes = overhead_minutes + ((titanium_nodes or node_index) * node_minutes)
            return PlayerResult(seed, True, node_index, minutes, copper_nodes, copper_minutes, titanium_nodes, titanium_minutes, biome_index)

    minutes = overhead_minutes + (max_nodes * node_minutes)
    copper_minutes = overhead_minutes + ((copper_nodes or max_nodes) * node_minutes)
    titanium_minutes = overhead_minutes + ((titanium_nodes or max_nodes) * node_minutes)
    return PlayerResult(seed, False, max_nodes, minutes, copper_nodes, copper_minutes, titanium_nodes, titanium_minutes, biome_index)


def run_simulation(
    distribution: DistributionTable,
    demands: Counter[str],
    player_count: int,
    max_nodes: int,
    node_minutes: float,
    overhead_minutes: float,
    world_seed: int,
) -> list[PlayerResult]:
    compact = build_compact_distribution(distribution, demands)
    return [
        simulate_player_compact(index, compact, world_seed, max_nodes, node_minutes, overhead_minutes)
        for index in range(player_count)
    ]


def percentile(sorted_values: list[float], pct: float) -> float:
    if not sorted_values:
        return 0.0
    if len(sorted_values) == 1:
        return sorted_values[0]
    rank = (len(sorted_values) - 1) * (pct / 100.0)
    lower = int(math.floor(rank))
    upper = int(math.ceil(rank))
    if lower == upper:
        return sorted_values[lower]
    weight = rank - lower
    return sorted_values[lower] * (1.0 - weight) + sorted_values[upper] * weight


def summarize_results(results: list[PlayerResult]) -> dict:
    successful = [result for result in results if result.success]
    failed = len(results) - len(successful)
    total_nodes_mined = sum(result.nodes_to_base for result in results)
    sorted_times = sorted(result.minutes_to_base for result in successful)
    sorted_nodes = sorted(result.nodes_to_base for result in successful)
    sorted_copper = sorted(result.copper_minutes for result in successful)
    sorted_titanium = sorted(result.titanium_minutes for result in successful)
    copper_worst = max(successful, key=lambda result: result.copper_minutes) if successful else None
    time_worst = max(successful, key=lambda result: result.minutes_to_base) if successful else None
    copper_p99_value = percentile(sorted_copper, 99.0)
    copper_p99_seed = min(successful, key=lambda result: abs(result.copper_minutes - copper_p99_value)) if successful else None
    return {
        "players": len(results),
        "successes": len(successful),
        "failures": failed,
        "total_nodes_mined": total_nodes_mined,
        "monte_carlo_steps": total_nodes_mined,
        "million_step_audit_passed": total_nodes_mined >= MONTE_CARLO_STEP_FLOOR,
        "average_minutes": sum(sorted_times) / len(sorted_times) if sorted_times else 0.0,
        "median_minutes": percentile(sorted_times, 50.0),
        "p90_minutes": percentile(sorted_times, 90.0),
        "p95_minutes": percentile(sorted_times, 95.0),
        "p99_minutes": percentile(sorted_times, 99.0),
        "worst_minutes": sorted_times[-1] if sorted_times else 0.0,
        "average_nodes": sum(sorted_nodes) / len(sorted_nodes) if sorted_nodes else 0.0,
        "p99_nodes": percentile([float(value) for value in sorted_nodes], 99.0),
        "copper_p99_minutes": copper_p99_value,
        "titanium_p99_minutes": percentile(sorted_titanium, 99.0),
        "copper_worst_seed": copper_worst.seed if copper_worst else 0,
        "copper_worst_minutes": copper_worst.copper_minutes if copper_worst else 0.0,
        "copper_p99_seed": copper_p99_seed.seed if copper_p99_seed else 0,
        "time_worst_seed": time_worst.seed if time_worst else 0,
        "time_worst_biome_index": time_worst.final_biome_index if time_worst else 0,
    }


def tune_distribution(distribution: DistributionTable, demands: Counter[str], pass_index: int) -> DistributionTable:
    critical_resources = set(demands.keys())
    tuned_biomes: list[BiomeTable] = []
    for biome_index, biome in enumerate(distribution.biomes):
        entries: list[DistributionEntry] = []
        early_biome_bias = max(0, 5 - biome_index)
        for entry in biome.entries:
            weight = entry.weight
            if entry.resource_id in critical_resources and early_biome_bias > 0:
                demand = demands[entry.resource_id]
                if entry.resource_id == COPPER_ID:
                    multiplier = (1.70 + pass_index * 0.15) * (1.0 + early_biome_bias * 0.045)
                    weight = int(math.ceil(weight * multiplier + demand * 1.10 + early_biome_bias * 3))
                elif entry.resource_id == TITANIUM_ID:
                    multiplier = (2.25 + pass_index * 0.20) * (1.0 + early_biome_bias * 0.05)
                    weight = int(math.ceil(weight * multiplier + demand * 1.35 + early_biome_bias * 3))
                else:
                    multiplier = (1.92 + pass_index * 0.18) * (1.0 + early_biome_bias * 0.05)
                    scarcity_floor = max(10, demand * 4 + early_biome_bias * 2)
                    weight = int(math.ceil(weight * multiplier + scarcity_floor))
            elif entry.resource_id in critical_resources:
                demand = demands[entry.resource_id]
                if entry.resource_id == COPPER_ID:
                    weight = int(math.ceil(weight * (1.50 + pass_index * 0.10) + demand * 0.95))
                elif entry.resource_id == TITANIUM_ID:
                    weight = int(math.ceil(weight * (1.98 + pass_index * 0.15) + demand * 1.15))
                else:
                    weight = int(math.ceil(weight * (1.70 + pass_index * 0.13) + max(8, demand * 3)))
            entries.append(
                DistributionEntry(
                    entry.biome_id,
                    entry.biome_display_name,
                    entry.distance_from_origin_m,
                    entry.resource_id,
                    entry.resource_hash32,
                    entry.resource_display_name,
                    min(weight, 65535),
                )
            )
        tuned_biomes.append(build_biome_table(entries))
    return DistributionTable(distribution.source_path, distribution.source_format, distribution.source_sha256, tuned_biomes)


def run_with_tuning(
    distribution: DistributionTable,
    demands: Counter[str],
    player_count: int,
    max_nodes: int,
    node_minutes: float,
    overhead_minutes: float,
    world_seed: int,
    threshold_minutes: float,
    baseline: list[PlayerResult] | None = None,
    baseline_summary: dict | None = None,
) -> tuple[list[PlayerResult], dict, DistributionTable, list[dict]]:
    if baseline is None:
        baseline = run_simulation(distribution, demands, player_count, max_nodes, node_minutes, overhead_minutes, world_seed)
    if baseline_summary is None:
        baseline_summary = summarize_results(baseline)
    tuning_history = [{"pass": 0, "p99_minutes": baseline_summary["p99_minutes"], "copper_p99_minutes": baseline_summary["copper_p99_minutes"]}]
    if baseline_summary["p99_minutes"] <= threshold_minutes:
        return baseline, baseline_summary, distribution, tuning_history

    tuned_distribution = distribution
    tuned_results = baseline
    tuned_summary = baseline_summary
    best_distribution = distribution
    best_results = baseline
    best_summary = baseline_summary
    for pass_index in range(1, 6):
        tuned_distribution = tune_distribution(tuned_distribution, demands, pass_index)
        tuned_results = run_simulation(tuned_distribution, demands, player_count, max_nodes, node_minutes, overhead_minutes, world_seed)
        tuned_summary = summarize_results(tuned_results)
        tuning_history.append(
            {
                "pass": pass_index,
                "p99_minutes": tuned_summary["p99_minutes"],
                "copper_p99_minutes": tuned_summary["copper_p99_minutes"],
            }
        )
        if tuned_summary["p99_minutes"] < best_summary["p99_minutes"]:
            best_distribution = tuned_distribution
            best_results = tuned_results
            best_summary = tuned_summary
        if tuned_summary["p99_minutes"] <= threshold_minutes:
            break
    if tuned_summary["p99_minutes"] <= threshold_minutes:
        return tuned_results, tuned_summary, tuned_distribution, tuning_history
    return best_results, best_summary, best_distribution, tuning_history


def distribution_to_json(
    distribution: DistributionTable,
    baseline_summary: dict,
    final_summary: dict,
    tuning_history: list[dict],
    threshold_minutes: float,
) -> dict:
    return {
        "schema_id": "economy.ore_distribution_tuned.v1",
        "source_path": str(distribution.source_path).replace("\\", "/"),
        "source_format": distribution.source_format,
        "source_sha256": distribution.source_sha256,
        "threshold_minutes": threshold_minutes,
        "tuning_applied": len(tuning_history) > 1,
        "baseline_p99_minutes": round(float(baseline_summary["p99_minutes"]), 6),
        "final_p99_minutes": round(float(final_summary["p99_minutes"]), 6),
        "tuning_history": tuning_history,
        "scalability": {
            "toaster": {
                "profile_id": "GRIT_TOASTER_STRIPPED",
                "lookup_contract": "uint16_weighted_cumulative_only",
                "max_extra_fields_per_resource": 0,
                "resource_payload": ["resource_hash32", "lcg_spawn_weight_u16"],
                "runtime_notes": "Hash and weight payload only; display and visual payload stripped before runtime ingest.",
            },
            "middle": {
                "profile_id": "WET_RIG_STANDARD",
                "lookup_contract": "uint16_weighted_cumulative_plus_display",
                "resource_payload": ["resource_hash32", "resource_display_name", "lcg_spawn_weight_u16"],
            },
            "high": {
                "profile_id": "BLACK_RIG_RTX_OVERKILL",
                "lookup_contract": "deterministic_weighted_lookup_plus_visual_hints",
                "lore_tone": "wet hull grit, dead-wire copper, black-resin sludge",
                "extra_data": {
                    "cluster_noise_octaves": HIGH_CLUSTER_NOISE_OCTAVES,
                    "inspection_gradient_samples": HIGH_INSPECTION_GRADIENT_SAMPLES,
                    "mica_glint_probability_q16": HIGH_MICA_GLINT_PROBABILITY_Q16,
                    "wet_soot_overlay_weight_q16": HIGH_WET_SOOT_OVERLAY_WEIGHT_Q16,
                },
                "extra_data_derivation": {
                    "cluster_noise_octaves": "2 base sediment strata + 2 pressure-scatter bands",
                    "inspection_gradient_samples": "2^3 power-of-two LUT lane for high profile resource inspection",
                    "mica_glint_probability_q16": "round(0.015 * 65535)",
                    "wet_soot_overlay_weight_q16": "round(0.050 * 65535)",
                },
            },
            "ultra": {
                "profile_id": "ABYSS_GOD_MODE_VISUALS",
                "lookup_contract": "deterministic_weighted_lookup_plus_harmonic_noise",
                "lore_tone": "pressure-black ore bloom with hydraulic lamp noise",
                "extra_data": {
                    "cluster_noise_octaves": ULTRA_CLUSTER_NOISE_OCTAVES,
                    "inspection_gradient_samples": ULTRA_INSPECTION_GRADIENT_SAMPLES,
                    "harmonic_detail_bands": ULTRA_HARMONIC_DETAIL_BANDS,
                    "resource_scar_highlight_lut_samples": ULTRA_RESOURCE_SCAR_HIGHLIGHT_LUT_SAMPLES,
                },
                "extra_data_derivation": {
                    "cluster_noise_octaves": "2 base sediment strata + 4 high-pressure harmonic bands",
                    "inspection_gradient_samples": "2^4 power-of-two LUT lane for god-mode resource inspection",
                    "harmonic_detail_bands": "five low-frequency industrial noise bands, visual-only",
                    "resource_scar_highlight_lut_samples": "2^5 sample lane for scarcity highlight LUT",
                },
            },
        },
        "biomes": [
            {
                "biome_id": biome.biome_id,
                "biome_display_name": biome.biome_display_name,
                "distance_from_origin_m": biome.distance_from_origin_m,
                "resources": [
                    {
                        "resource_id": entry.resource_id,
                        "resource_hash32": entry.resource_hash32,
                        "resource_display_name": entry.resource_display_name,
                        "lcg_spawn_weight_u16": entry.weight,
                    }
                    for entry in biome.entries
                ],
            }
            for biome in distribution.biomes
        ],
    }


def write_png(path: Path, width: int, height: int, rgb: bytearray) -> None:
    def chunk(kind: bytes, payload: bytes) -> bytes:
        return struct.pack(">I", len(payload)) + kind + payload + struct.pack(">I", zlib.crc32(kind + payload) & UINT32_MASK)

    raw = bytearray()
    stride = width * 3
    for y in range(height):
        raw.append(0)
        start = y * stride
        raw.extend(rgb[start : start + stride])

    payload = b"".join(
        [
            b"\x89PNG\r\n\x1a\n",
            chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)),
            chunk(b"IDAT", zlib.compress(bytes(raw), 9)),
            chunk(b"IEND", b""),
        ]
    )
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(payload)


def write_histogram_png(path: Path, results: list[PlayerResult]) -> None:
    successful_times = [result.minutes_to_base for result in results if result.success]
    width = 960
    height = 540
    margin_left = 64
    margin_bottom = 48
    margin_top = 32
    margin_right = 32
    background = (12, 18, 24)
    axis = (122, 138, 148)
    bar = (64, 172, 188)
    accent = (222, 177, 82)
    rgb = bytearray(background * (width * height))

    def set_pixel(x: int, y: int, color: tuple[int, int, int]) -> None:
        if 0 <= x < width and 0 <= y < height:
            index = (y * width + x) * 3
            rgb[index : index + 3] = bytes(color)

    def fill_rect(x0: int, y0: int, x1: int, y1: int, color: tuple[int, int, int]) -> None:
        for y in range(max(0, y0), min(height, y1)):
            for x in range(max(0, x0), min(width, x1)):
                set_pixel(x, y, color)

    chart_w = width - margin_left - margin_right
    chart_h = height - margin_top - margin_bottom
    fill_rect(margin_left, height - margin_bottom, width - margin_right, height - margin_bottom + 2, axis)
    fill_rect(margin_left, margin_top, margin_left + 2, height - margin_bottom, axis)

    if not successful_times:
        write_png(path, width, height, rgb)
        return

    max_time = max(60.0, math.ceil(max(successful_times) / 10.0) * 10.0)
    bin_count = 40
    bins = [0 for _ in range(bin_count)]
    for value in successful_times:
        index = min(bin_count - 1, int((value / max_time) * bin_count))
        bins[index] += 1
    max_count = max(bins) if bins else 1
    bar_w = max(1, chart_w // bin_count)
    threshold_x = margin_left + int((60.0 / max_time) * chart_w)
    fill_rect(threshold_x, margin_top, threshold_x + 2, height - margin_bottom, accent)
    for index, count in enumerate(bins):
        x0 = margin_left + index * bar_w
        x1 = margin_left + (index + 1) * bar_w - 1
        bar_h = int((count / max_count) * (chart_h - 8))
        y0 = height - margin_bottom - bar_h
        fill_rect(x0, y0, x1, height - margin_bottom, bar)

    write_png(path, width, height, rgb)


def write_report(
    path: Path,
    distribution: DistributionTable,
    demands: Counter[str],
    baseline_summary: dict,
    final_summary: dict,
    tuning_history: list[dict],
    time_model_source: str,
    params: dict,
    dependency_gaps: list[str],
) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    tuning_applied = len(tuning_history) > 1
    status = "ECONOMY PROVEN" if final_summary["failures"] == 0 and final_summary["p99_minutes"] <= params["threshold_minutes"] else "PENDING VERIFICATION - ECONOMY RISK"
    raw_lines = "\n".join(f"- `{item}`: {qty}" for item, qty in sorted(demands.items()))
    history_lines = "\n".join(
        f"- pass {row['pass']}: p99={row['p99_minutes']:.3f} min, copper_p99={row['copper_p99_minutes']:.3f} min"
        for row in tuning_history
    )
    missing_ore_note = (
        "`Data/Economy/Ore_Distribution.json` was absent; `Resource_Distribution_Matrix.csv` was used as current source data."
        if distribution.source_format == "csv"
        else "`Ore_Distribution.json` was used."
    )
    path.write_text(
        "\n".join(
            [
                "# Economy Monte Carlo Audit",
                "",
                f"Status: {status}",
                "Evidence class: CLI_PYTHON + STATIC_DATA. Runtime Unity proof remains PENDING VERIFICATION.",
                "",
                "## Inputs",
                "",
                f"- Distribution source: `{distribution.source_path}`",
                f"- Source SHA-256: `{distribution.source_sha256}`",
                f"- Recipes: `Data/Economy/Recipes.json`",
                f"- Note: {missing_ore_note}",
                f"- Time model: {time_model_source}",
                "",
                "## Mandates Followed",
                "",
                "- `MATH_Deterministic_RNG_SlotMachine.txt`: integer LCG, integer cumulative weights, multiply-high mapping.",
                "- `DATA_Inventory_Resources_Items_SOA_Layout.txt`: raw resource truth stays data-only.",
                "- `QA_Evidence_Text_Filter_Audit.txt`: claims are labeled as CLI/static evidence, not runtime proof.",
                "- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`: no Unity hot path touched.",
                "- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`: offline audit replaces runtime probing for this task.",
                "",
                "## First Base Raw Demand",
                "",
                f"Targets: {', '.join(FIRST_BASE_TARGETS)}",
                "",
                raw_lines,
                "",
                f"Titanium requirement: {demands.get(TITANIUM_ID, 0)}",
                f"Copper requirement: {demands.get(COPPER_ID, 0)}",
                f"Dependency coverage gaps: {dependency_gaps if dependency_gaps else 'none'}",
                "",
                "## Simulation Parameters",
                "",
                f"- Players: {params['players']}",
                f"- Max nodes per player: {params['max_nodes']}",
                f"- Monte Carlo node-step floor: {params['monte_carlo_step_floor']}",
                f"- Biomes: {len(distribution.biomes)}",
                f"- Threshold: p99 <= {params['threshold_minutes']:.3f} minutes",
                f"- Node minutes: {params['node_minutes']:.6f}",
                f"- Overhead minutes: {params['overhead_minutes']:.3f}",
                f"- World seed: 0x{params['world_seed']:08X}",
                "",
                "## Results",
                "",
                f"- Baseline p99 time: {baseline_summary['p99_minutes']:.3f} minutes",
                f"- Baseline total mined nodes: {baseline_summary['total_nodes_mined']}",
                f"- Final average time: {final_summary['average_minutes']:.3f} minutes",
                f"- Final median time: {final_summary['median_minutes']:.3f} minutes",
                f"- Final p90 time: {final_summary['p90_minutes']:.3f} minutes",
                f"- Final p95 time: {final_summary['p95_minutes']:.3f} minutes",
                f"- Final p99 time: {final_summary['p99_minutes']:.3f} minutes",
                f"- Final worst time: {final_summary['worst_minutes']:.3f} minutes",
                f"- Final p99 nodes: {final_summary['p99_nodes']:.3f}",
                f"- Final total mined nodes: {final_summary['total_nodes_mined']}",
                f"- Million-step proof: {final_summary['million_step_audit_passed']}",
                f"- Copper p99 time: {final_summary['copper_p99_minutes']:.3f} minutes",
                f"- Copper p99 seed: 0x{final_summary['copper_p99_seed']:08X}",
                f"- Copper worst seed: 0x{final_summary['copper_worst_seed']:08X}",
                f"- Titanium p99 time: {final_summary['titanium_p99_minutes']:.3f} minutes",
                f"- Failures after {params['max_nodes']} nodes: {final_summary['failures']}",
                f"- Tuning applied: {tuning_applied}",
                "",
                "## Tuning History",
                "",
                history_lines,
                "",
                "## RNG Variance Rationale",
                "",
                "Acceptable variance is bounded by p99, not average time. Averages hide unlucky deterministic seeds. The pass condition is no failed player within 10,000 nodes and final p99 time at or below 60 minutes. The 1% copper-starvation seed is reported because copper gates the early electronics chain.",
                "",
                "## Regression Model",
                "",
                "CPU: offline Python only; no runtime CPU path changed.",
                "GC: no Unity hot path touched; GCMonitor proof is not applicable to this offline audit.",
                "Memory: generated artifacts are bounded files; no runtime memory retained.",
                "Cadence: no Tick/SlowTick/FixedTick cadence changed.",
                "Correctness: recipe closure must resolve all raw dependencies and distribution must cover every raw resource.",
                "",
                "## Failure Modes",
                "",
                "- If upstream `Ore_Distribution.json` appears later, rerun this tool and compare against the CSV-derived baseline.",
                "- If First Base target scope changes, rerun with an edited target list and do not reuse this report.",
                "- Runtime pickup/craft route remains PENDING VERIFICATION until Play Mode validates actual item acquisition.",
                "",
                "## Artifacts",
                "",
                f"- JSON summary: `{RESULT_JSON_PATH}`",
                f"- Histogram: `{HISTOGRAM_PATH}`",
                f"- Tuned JSON: `{TUNED_JSON_PATH}`",
                "",
            ]
        ),
        encoding="utf-8",
    )


def write_outputs(
    distribution: DistributionTable,
    tuned_distribution: DistributionTable,
    demands: Counter[str],
    baseline_summary: dict,
    final_summary: dict,
    tuning_history: list[dict],
    final_results: list[PlayerResult],
    time_model_source: str,
    params: dict,
    dependency_gaps: list[str],
) -> None:
    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    TUNED_JSON_PATH.parent.mkdir(parents=True, exist_ok=True)
    RESULT_JSON_PATH.write_text(
        json.dumps(
            {
                "status": "ECONOMY PROVEN"
                if final_summary["failures"] == 0 and final_summary["p99_minutes"] <= params["threshold_minutes"]
                else "PENDING VERIFICATION - ECONOMY RISK",
                "evidence_class": "CLI_PYTHON_STATIC_DATA",
                "params": params,
                "first_base_targets": FIRST_BASE_TARGETS,
                "raw_demands": dict(sorted(demands.items())),
                "dependency_gaps": dependency_gaps,
                "baseline_summary": baseline_summary,
                "final_summary": final_summary,
                "tuning_history": tuning_history,
                "distribution_source": str(distribution.source_path).replace("\\", "/"),
                "distribution_source_format": distribution.source_format,
                "distribution_source_sha256": distribution.source_sha256,
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    TUNED_JSON_PATH.write_text(
        json.dumps(
            distribution_to_json(tuned_distribution, baseline_summary, final_summary, tuning_history, params["threshold_minutes"]),
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    write_histogram_png(HISTOGRAM_PATH, final_results)
    write_report(
        REPORT_PATH,
        distribution,
        demands,
        baseline_summary,
        final_summary,
        tuning_history,
        time_model_source,
        params,
        dependency_gaps,
    )


def run_audit(args: argparse.Namespace) -> dict:
    root = Path(args.root).resolve()
    distribution = load_distribution(root)
    recipes = load_recipes(root)
    demands = resolve_raw_demands(recipes, FIRST_BASE_TARGETS)
    dependency_gaps = validate_dependency_coverage(demands, distribution)
    if dependency_gaps:
        raise ValueError(f"distribution missing raw resources: {dependency_gaps}")

    node_minutes, overhead_minutes, time_model_source = load_time_model(root, demands)
    if args.node_minutes is not None:
        node_minutes = args.node_minutes
        time_model_source += f"; CLI override node_minutes={node_minutes:.6f}"
    if args.overhead_minutes is not None:
        overhead_minutes = args.overhead_minutes
        time_model_source += f"; CLI override overhead_minutes={overhead_minutes:.3f}"

    baseline_results = run_simulation(
        distribution,
        demands,
        args.players,
        args.max_nodes,
        node_minutes,
        overhead_minutes,
        args.world_seed,
    )
    baseline_summary = summarize_results(baseline_results)
    if baseline_summary["p99_minutes"] <= args.threshold_minutes:
        final_results = baseline_results
        final_summary = baseline_summary
        tuned_distribution = distribution
        tuning_history = [{"pass": 0, "p99_minutes": baseline_summary["p99_minutes"], "copper_p99_minutes": baseline_summary["copper_p99_minutes"]}]
    else:
        final_results, final_summary, tuned_distribution, tuning_history = run_with_tuning(
            distribution,
            demands,
            args.players,
            args.max_nodes,
            node_minutes,
            overhead_minutes,
            args.world_seed,
            args.threshold_minutes,
            baseline_results,
            baseline_summary,
        )

    params = {
        "players": args.players,
        "max_nodes": args.max_nodes,
        "threshold_minutes": args.threshold_minutes,
        "monte_carlo_step_floor": MONTE_CARLO_STEP_FLOOR,
        "node_minutes": node_minutes,
        "overhead_minutes": overhead_minutes,
        "world_seed": args.world_seed,
    }
    write_outputs(
        distribution,
        tuned_distribution,
        demands,
        baseline_summary,
        final_summary,
        tuning_history,
        final_results,
        time_model_source,
        params,
        dependency_gaps,
    )
    return {
        "baseline_summary": baseline_summary,
        "final_summary": final_summary,
        "tuning_history": tuning_history,
        "params": params,
    }


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Run deterministic Monte Carlo economy audit.")
    parser.add_argument("--root", default=".", help="Repository root.")
    parser.add_argument("--players", type=int, default=DEFAULT_PLAYER_COUNT, help="Simulated player count.")
    parser.add_argument("--max-nodes", type=int, default=DEFAULT_MAX_NODES, help="Max mined nodes per player.")
    parser.add_argument("--threshold-minutes", type=float, default=DEFAULT_THRESHOLD_MINUTES, help="p99 pass threshold.")
    parser.add_argument("--world-seed", type=lambda value: int(value, 0), default=DEFAULT_WORLD_SEED, help="World seed.")
    parser.add_argument("--node-minutes", type=float, default=None, help="Optional minutes per mined node.")
    parser.add_argument("--overhead-minutes", type=float, default=None, help="Optional fixed route/handling minutes.")
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    result = run_audit(args)
    final_summary = result["final_summary"]
    print("ECONOMY MONTE CARLO COMPLETE")
    print(f"players={result['params']['players']} max_nodes={result['params']['max_nodes']}")
    print(f"average_minutes={final_summary['average_minutes']:.3f}")
    print(f"p99_minutes={final_summary['p99_minutes']:.3f}")
    print(f"copper_p99_minutes={final_summary['copper_p99_minutes']:.3f}")
    print(f"total_nodes_mined={final_summary['total_nodes_mined']}")
    print(f"million_step_audit_passed={final_summary['million_step_audit_passed']}")
    print(f"failures={final_summary['failures']}")
    print(f"report={REPORT_PATH}")
    print(f"histogram={HISTOGRAM_PATH}")
    print(f"tuned_json={TUNED_JSON_PATH}")
    print(
        "STATUS: ECONOMY PROVEN"
        if final_summary["failures"] == 0 and final_summary["p99_minutes"] <= result["params"]["threshold_minutes"]
        else "STATUS: PENDING VERIFICATION - ECONOMY RISK"
    )
    return 0 if final_summary["failures"] == 0 and final_summary["p99_minutes"] <= result["params"]["threshold_minutes"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
