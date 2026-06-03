#!/usr/bin/env python3
"""First 20 minute survival/economy Monte Carlo audit for HECTON-8.

The simulator mirrors the ore LCG constants used by ProceduralOreSpawner and
uses the active Standard_Suit_V1 SurvivalStats asset for oxygen authority. It
does not import Unity. Generated reports are static-data evidence only.
"""

from __future__ import annotations

import argparse
import csv
import json
import math
import re
import subprocess
import sys
import time
from collections import Counter
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable


UINT32_MASK = 0xFFFFFFFF
UINT32_SCALE = 1.0 / 4294967296.0
LCG_MULTIPLIER = 1664525
LCG_INCREMENT = 1013904223
DEFAULT_WORLD_SEED = 0x48454338
DEFAULT_RUNS = 100_000
DEFAULT_DURATION_SECONDS = 1_200.0
DEFAULT_REPORT_JSON = Path("Docs/Reports/First20Balance_17-B.json")
DEFAULT_REPORT_MD = Path("Docs/Reports/First20Balance_17-B.md")

COPPER_ID = "Data_Copper"
TITANIUM_ID = "Data_TitaniumScrap"
SILICA_ID = "Data_SilicaShards"
FIBER_ID = "Data_FiberKelp"
HYDROCARBON_ID = "Data_HydrocarbonResin"
EARLY_BIOME_IDS = (
    "biome.safe_shallows",
    "biome.kelp_karst",
    "biome.sediment_drift",
)
RESOURCE_TIERS = {
    TITANIUM_ID: 1,
    COPPER_ID: 1,
    SILICA_ID: 1,
    FIBER_ID: 1,
    HYDROCARBON_ID: 1,
    "Data_SulfurClumps": 1,
    "Data_SilverOre": 1,
    "Data_GoldOre": 2,
    "Data_LithiumCrystal": 2,
    "Data_NickelOre": 2,
    "Data_CobaltAlloy": 2,
    "Data_ThermalGel": 2,
    "Data_CarbonGraphite": 3,
    "Data_RareEarthDust": 3,
    "Data_AbyssalCrystal": 3,
}


@dataclass(frozen=True)
class ResourceEntry:
    resource_id: str
    weight: int
    tier: int


@dataclass(frozen=True)
class BiomeTable:
    biome_id: str
    display_name: str
    depth_min_m: float
    depth_max_m: float
    base_density_u8: int
    entries: tuple[ResourceEntry, ...]
    cumulative_weights: tuple[int, ...]
    total_weight: int


@dataclass(frozen=True)
class Distribution:
    source_path: Path
    biomes: tuple[BiomeTable, ...]


@dataclass(frozen=True)
class SurvivalStatsAuthority:
    source_path: Path
    max_oxygen: float
    oxygen_consumption_rate: float
    safe_depth: float
    pressure_damage_rate: float
    pressure_scale_per_meter: float
    hunger_drain_rate: float
    thirst_drain_rate: float
    max_hunger: float
    max_thirst: float


@dataclass(frozen=True)
class EconomyJsonOxygen:
    source_path: Path
    tank_capacity_liters: float
    base_liters_per_second: float


@dataclass(frozen=True)
class ToolHeatProfile:
    source_path: Path
    heat_capacity_by_id: dict[str, float]


@dataclass(frozen=True)
class SessionResult:
    seed: int
    outcome: str
    wire_time_seconds: float
    copper_count: int
    total_tier1_count: int
    oxygen_remaining01: float
    deepest_depth_m: float
    longest_reward_gap_seconds: float
    dive_count: int
    nodes_mined: int


@dataclass(frozen=True)
class TuningState:
    max_oxygen: float
    oxygen_rate: float
    csv_weight_delta: dict[tuple[str, str], int]
    hunger_rate_scale: float
    thirst_rate_scale: float
    tool_heat_scale: float


def uint32(value: int) -> int:
    return value & UINT32_MASK


def next_lcg(state: int) -> int:
    return uint32((state * LCG_MULTIPLIER) + LCG_INCREMENT)


def mix_u32(a: int, b: int) -> int:
    h = uint32(a ^ 0x9E3779B9)
    h = uint32(h ^ uint32(b + 0x85EBCA6B + uint32(h << 6) + (h >> 2)))
    h = uint32(h ^ (h >> 16))
    h = uint32(h * 0x7FEB352D)
    h = uint32(h ^ (h >> 15))
    h = uint32(h * 0x846CA68B)
    h = uint32(h ^ (h >> 16))
    return h if h != 0 else 0xA341316C


def range_u32(value: int, range_exclusive: int) -> int:
    if range_exclusive <= 0:
        raise ValueError("range_exclusive must be positive")
    return ((value & UINT32_MASK) * range_exclusive) >> 32


def unit_from_state(value: int) -> float:
    return (value & UINT32_MASK) * UINT32_SCALE


def roll_unit(state: int) -> tuple[int, float]:
    state = next_lcg(state)
    return state, unit_from_state(state)


def roll_range(state: int, range_exclusive: int) -> tuple[int, int]:
    state = next_lcg(state)
    return state, range_u32(state, range_exclusive)


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


def read_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def write_text_lf(path: Path, text: str) -> None:
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        handle.write(text)


def load_distribution(root: Path, override_weights: dict[tuple[str, str], int] | None = None) -> Distribution:
    path = root / "Data" / "Economy" / "Ore_Distribution.json"
    data = read_json(path)
    biomes: list[BiomeTable] = []
    override_weights = override_weights or {}
    for biome in data["biomes"]:
        entries: list[ResourceEntry] = []
        cumulative: list[int] = []
        total = 0
        biome_id = str(biome["biome_id"])
        for resource in biome["resources"]:
            resource_id = str(resource["resource_id"])
            base_weight = int(resource.get("weight_u8", resource.get("lcg_spawn_weight_u16", 0)))
            weight = max(1, base_weight + override_weights.get((biome_id, resource_id), 0))
            total += weight
            cumulative.append(total)
            entries.append(
                ResourceEntry(
                    resource_id=resource_id,
                    weight=weight,
                    tier=int(resource.get("resource_tier", RESOURCE_TIERS.get(resource_id, 1))),
                )
            )
        biomes.append(
            BiomeTable(
                biome_id=biome_id,
                display_name=str(biome.get("biome_display_name", biome_id)),
                depth_min_m=float(biome.get("depth_min_m", 0.0)),
                depth_max_m=float(biome.get("depth_max_m", 0.0)),
                base_density_u8=int(biome.get("base_density_u8", 128)),
                entries=tuple(entries),
                cumulative_weights=tuple(cumulative),
                total_weight=total,
            )
        )
    return Distribution(path, tuple(biomes))


def parse_asset_float(text: str, key: str, fallback: float) -> float:
    match = re.search(rf"^\s*{re.escape(key)}:\s*([-+]?\d+(?:\.\d+)?)\s*$", text, re.MULTILINE)
    return float(match.group(1)) if match else fallback


def load_survival_stats(
    root: Path,
    oxygen_rate_override: float | None = None,
    max_oxygen_override: float | None = None,
) -> SurvivalStatsAuthority:
    path = root / "Assets" / "_Project" / "Data" / "Survival" / "Standard_Suit_V1.asset"
    text = path.read_text(encoding="utf-8")
    rate = parse_asset_float(text, "oxygenConsumptionRate", 0.5)
    return SurvivalStatsAuthority(
        source_path=path,
        max_oxygen=max_oxygen_override if max_oxygen_override is not None else parse_asset_float(text, "maxOxygen", 100.0),
        oxygen_consumption_rate=oxygen_rate_override if oxygen_rate_override is not None else rate,
        safe_depth=parse_asset_float(text, "safeDepth", 50.0),
        pressure_damage_rate=parse_asset_float(text, "pressureDamageRate", 2.0),
        pressure_scale_per_meter=parse_asset_float(text, "pressureScalePerMeter", 0.02),
        hunger_drain_rate=parse_asset_float(text, "hungerDrainRate", 0.1),
        thirst_drain_rate=parse_asset_float(text, "thirstDrainRate", 0.15),
        max_hunger=parse_asset_float(text, "maxHunger", 100.0),
        max_thirst=parse_asset_float(text, "maxThirst", 100.0),
    )


def load_economy_oxygen(root: Path) -> EconomyJsonOxygen:
    path = root / "Data" / "Economy" / "Survival_Stats.json"
    data = read_json(path)
    oxygen = data.get("oxygen", {})
    return EconomyJsonOxygen(
        source_path=path,
        tank_capacity_liters=float(oxygen.get("tank_capacity_liters", 0.0)),
        base_liters_per_second=float(oxygen.get("base_liters_per_second", 0.0)),
    )


def load_tool_heat(root: Path) -> ToolHeatProfile:
    path = root / "Data" / "Balance" / "ToolHeat.csv"
    values: dict[str, float] = {}
    with path.open("r", encoding="utf-8", newline="") as handle:
        for row in csv.DictReader(handle):
            values[row["id"]] = float(row["heat_capacity"])
    return ToolHeatProfile(path, values)


def select_resource(state: int, biome: BiomeTable) -> ResourceEntry:
    threshold = range_u32(state, biome.total_weight)
    for index, cumulative in enumerate(biome.cumulative_weights):
        if threshold < cumulative:
            return biome.entries[index]
    return biome.entries[-1]


def resolve_biome_index(
    state: int,
    current_index: int,
    elapsed_seconds: float,
    aggression01: float,
    biome_count: int,
) -> tuple[int, int]:
    state, roll = roll_unit(state)
    advance_pressure = max(0.0, (elapsed_seconds - 180.0) / 900.0)
    forward_probability = min(0.28, 0.03 + aggression01 * 0.16 + advance_pressure * 0.08)
    backtrack_probability = max(0.015, 0.12 - aggression01 * 0.08)
    early_limit = min(biome_count - 1, 2)
    if roll < backtrack_probability and current_index > 0:
        return state, current_index - 1
    if roll < backtrack_probability + forward_probability and current_index < early_limit:
        return state, current_index + 1
    return state, current_index


def resolve_depth_m(state: int, biome: BiomeTable, aggression01: float, elapsed_seconds: float) -> tuple[int, float]:
    state, roll = roll_unit(state)
    route_progress01 = min(1.0, elapsed_seconds / DEFAULT_DURATION_SECONDS)
    biome_span = max(4.0, biome.depth_max_m - biome.depth_min_m)
    shallow_band = biome.depth_min_m + biome_span * 0.08
    deep_band = biome.depth_min_m + biome_span * (0.30 + 0.30 * aggression01 + 0.18 * route_progress01)
    jitter = (roll - 0.5) * biome_span * 0.12
    depth = max(2.0, min(biome.depth_max_m * 0.82, shallow_band + (deep_band - shallow_band) * aggression01 + jitter))
    return state, depth


def resolve_activity_scale(speed_mps: float) -> float:
    if speed_mps < 0.2:
        return 1.0
    cruise_speed = 2.4
    move01 = min(1.0, (speed_mps * speed_mps) / (cruise_speed * cruise_speed))
    return 1.0 + (1.55 - 1.0) * move01


def resolve_stress_scale(stress01: float) -> float:
    stress01 = min(1.0, max(0.0, stress01))
    return 1.0 + (0.50 * stress01 * stress01)


def resolve_carry_scale(total_tier1_count: int) -> float:
    mass_kg = total_tier1_count * 0.48
    if mass_kg <= 18.0:
        return 1.0
    load01 = min(1.0, (mass_kg - 18.0) / 72.0)
    return 1.0 + 0.22 * load01


def oxygen_drain_rate(
    stats: SurvivalStatsAuthority,
    depth_m: float,
    speed_mps: float,
    stress01: float,
    total_tier1_count: int,
) -> float:
    pressure_factor = min(16.0, max(1.0, 1.0 + max(0.0, depth_m) * 0.1))
    movement_factor = resolve_activity_scale(speed_mps)
    stress_factor = resolve_stress_scale(stress01)
    carry_factor = resolve_carry_scale(total_tier1_count)
    return stats.oxygen_consumption_rate * pressure_factor * movement_factor * stress_factor * carry_factor


def drain_oxygen(
    stats: SurvivalStatsAuthority,
    oxygen: float,
    seconds: float,
    depth_m: float,
    speed_mps: float,
    stress01: float,
    total_tier1_count: int,
) -> float:
    return oxygen - oxygen_drain_rate(stats, depth_m, speed_mps, stress01, total_tier1_count) * seconds


def simulate_session(
    run_index: int,
    distribution: Distribution,
    stats: SurvivalStatsAuthority,
    world_seed: int,
    duration_seconds: float,
) -> SessionResult:
    state = mix_u32(world_seed, uint32((run_index + 1) * 747796405))
    seed = state
    state, aggression01 = roll_unit(state)
    state, discipline01 = roll_unit(state)
    state, local_search01 = roll_unit(state)

    aggression01 = max(0.0, min(1.0, aggression01))
    discipline01 = max(0.0, min(1.0, discipline01))
    local_search01 = max(0.0, min(1.0, local_search01))

    oxygen = stats.max_oxygen
    elapsed = 0.0
    biome_index = 0
    inventory: Counter[str] = Counter()
    copper_count = 0
    total_tier1 = 0
    nodes_mined = 0
    dive_count = 1
    wire_time = 0.0
    deepest_depth = 0.0
    last_reward_time = 0.0
    longest_reward_gap = 0.0
    copper_clump_remaining = 0
    previous_resource_id = ""

    return_threshold = max(0.06, min(0.36, 0.31 - aggression01 * 0.20 + discipline01 * 0.06))
    greed_overstay = max(0.0, aggression01 - discipline01)

    while elapsed < duration_seconds:
        biome = distribution.biomes[biome_index]
        state, depth_m = resolve_depth_m(state, biome, aggression01, elapsed)
        deepest_depth = max(deepest_depth, depth_m)

        oxygen01 = oxygen / stats.max_oxygen
        if oxygen01 < return_threshold:
            speed_mps = 1.0 + aggression01 * 1.8
            ascent_seconds = 8.0 + depth_m / max(0.5, speed_mps)
            average_ascent_depth = depth_m * 0.5
            stress01 = min(1.0, 0.25 + greed_overstay * 0.55 + (return_threshold - oxygen01) * 1.4)
            oxygen = drain_oxygen(stats, oxygen, ascent_seconds, average_ascent_depth, speed_mps, stress01, total_tier1)
            elapsed += ascent_seconds
            if oxygen <= 0.0:
                outcome = "suffocated_before_wire" if copper_count < 2 else "suffocated_after_wire"
                return SessionResult(seed, outcome, wire_time, copper_count, total_tier1, 0.0, deepest_depth, longest_reward_gap, dive_count, nodes_mined)
            refill_seconds = (stats.max_oxygen - oxygen) / 15.0
            elapsed += max(0.0, refill_seconds)
            oxygen = stats.max_oxygen
            dive_count += 1
            state, biome_index = resolve_biome_index(state, biome_index, elapsed, aggression01, len(distribution.biomes))
            continue

        state, biome_index = resolve_biome_index(state, biome_index, elapsed, aggression01, len(distribution.biomes))
        biome = distribution.biomes[biome_index]
        density01 = max(0.0, min(1.0, (biome.base_density_u8 - 32) / 192.0))
        state, jitter01 = roll_unit(state)
        interval = (44.0 - 21.0 * aggression01) * (1.08 - 0.36 * density01) * (0.82 + 0.36 * jitter01)
        mine_seconds = max(8.0, interval)
        speed_mps = 0.55 + aggression01 * 3.15 + jitter01 * 0.95
        oxygen01 = oxygen / stats.max_oxygen
        stress01 = min(1.0, 0.10 + aggression01 * 0.48 + max(0.0, 0.22 - oxygen01) * 1.8)
        oxygen = drain_oxygen(stats, oxygen, mine_seconds, depth_m, speed_mps, stress01, total_tier1)
        elapsed += mine_seconds
        if oxygen <= 0.0:
            outcome = "suffocated_before_wire" if copper_count < 2 else "suffocated_after_wire"
            return SessionResult(seed, outcome, wire_time, copper_count, total_tier1, 0.0, deepest_depth, longest_reward_gap, dive_count, nodes_mined)

        use_clump_bias = previous_resource_id == COPPER_ID and copper_clump_remaining > 0
        if use_clump_bias:
            state, clump_roll = roll_range(state, 100)
            if clump_roll < 85:
                resource = ResourceEntry(COPPER_ID, 1, 1)
            else:
                state = next_lcg(state)
                resource = select_resource(state, biome)
            copper_clump_remaining -= 1
        else:
            state = next_lcg(state)
            resource = select_resource(state, biome)

        previous_resource_id = resource.resource_id
        inventory[resource.resource_id] += 1
        nodes_mined += 1
        if resource.tier <= 1:
            total_tier1 += 1
        if resource.resource_id == COPPER_ID:
            copper_count += 1
            if copper_count == 1:
                state, clump_span = roll_range(state, 3)
                if local_search01 > 0.18:
                    copper_clump_remaining = 1 + clump_span
            if copper_count >= 2 and wire_time <= 0.0:
                wire_time = elapsed

        if resource.resource_id == COPPER_ID or resource.tier >= 2:
            gap = elapsed - last_reward_time
            longest_reward_gap = max(longest_reward_gap, gap)
            last_reward_time = elapsed

    oxygen_remaining01 = max(0.0, min(1.0, oxygen / stats.max_oxygen))
    if copper_count >= 2:
        if copper_count >= 8 and total_tier1 >= 22 and oxygen_remaining01 >= 0.25:
            outcome = "survival_devalued_excess"
        else:
            outcome = "wire_success"
    else:
        outcome = "wire_starved_alive"

    return SessionResult(
        seed,
        outcome,
        wire_time,
        copper_count,
        total_tier1,
        oxygen_remaining01,
        deepest_depth,
        longest_reward_gap,
        dive_count,
        nodes_mined,
    )


def run_simulation(
    distribution: Distribution,
    stats: SurvivalStatsAuthority,
    runs: int,
    world_seed: int,
    duration_seconds: float,
) -> list[SessionResult]:
    return [simulate_session(index, distribution, stats, world_seed, duration_seconds) for index in range(runs)]


def summarize(results: list[SessionResult], elapsed_wall_seconds: float) -> dict:
    count = len(results)
    outcomes = Counter(result.outcome for result in results)
    wire_times = sorted(result.wire_time_seconds for result in results if result.wire_time_seconds > 0.0)
    copper_counts = sorted(float(result.copper_count) for result in results)
    tier1_counts = sorted(float(result.total_tier1_count) for result in results)
    oxygen_remaining = sorted(result.oxygen_remaining01 for result in results)
    reward_gaps = sorted(result.longest_reward_gap_seconds for result in results)
    deepest_depths = sorted(result.deepest_depth_m for result in results)
    nodes_mined = sorted(float(result.nodes_mined) for result in results)
    suffocated_before = outcomes["suffocated_before_wire"]
    suffocated_after = outcomes["suffocated_after_wire"]
    wire_successish = count - suffocated_before - outcomes["wire_starved_alive"]
    return {
        "runs": count,
        "outcomes": dict(sorted(outcomes.items())),
        "prob_suffocated_before_wire": suffocated_before / count if count else 0.0,
        "prob_suffocated_after_wire": suffocated_after / count if count else 0.0,
        "prob_wire_starved_alive": outcomes["wire_starved_alive"] / count if count else 0.0,
        "prob_survival_devalued_excess": outcomes["survival_devalued_excess"] / count if count else 0.0,
        "prob_wire_collected": wire_successish / count if count else 0.0,
        "wire_time_seconds": {
            "p50": percentile(wire_times, 50.0),
            "p75": percentile(wire_times, 75.0),
            "p90": percentile(wire_times, 90.0),
            "p95": percentile(wire_times, 95.0),
            "p99": percentile(wire_times, 99.0),
        },
        "copper_count": {
            "p50": percentile(copper_counts, 50.0),
            "p90": percentile(copper_counts, 90.0),
            "p95": percentile(copper_counts, 95.0),
            "p99": percentile(copper_counts, 99.0),
        },
        "total_tier1_count": {
            "p50": percentile(tier1_counts, 50.0),
            "p90": percentile(tier1_counts, 90.0),
            "p95": percentile(tier1_counts, 95.0),
            "p99": percentile(tier1_counts, 99.0),
        },
        "oxygen_remaining01": {
            "p10": percentile(oxygen_remaining, 10.0),
            "p50": percentile(oxygen_remaining, 50.0),
            "p90": percentile(oxygen_remaining, 90.0),
        },
        "longest_reward_gap_seconds": {
            "p90": percentile(reward_gaps, 90.0),
            "p95": percentile(reward_gaps, 95.0),
            "p99": percentile(reward_gaps, 99.0),
        },
        "deepest_depth_m": {
            "p50": percentile(deepest_depths, 50.0),
            "p95": percentile(deepest_depths, 95.0),
        },
        "nodes_mined": {
            "p50": percentile(nodes_mined, 50.0),
            "p95": percentile(nodes_mined, 95.0),
        },
        "sim_wall_seconds": elapsed_wall_seconds,
        "sim_microseconds_per_session": (elapsed_wall_seconds * 1_000_000.0 / count) if count else 0.0,
    }


def derive_tuning(summary: dict, current: TuningState) -> tuple[TuningState, list[str]]:
    notes: list[str] = []
    max_oxygen = current.max_oxygen
    oxygen_rate = current.oxygen_rate
    deltas = dict(current.csv_weight_delta)
    hunger_scale = current.hunger_rate_scale
    thirst_scale = current.thirst_rate_scale
    tool_heat_scale = current.tool_heat_scale

    suffocation = summary["prob_suffocated_before_wire"]
    starvation = summary["prob_wire_starved_alive"]
    excess = summary["prob_survival_devalued_excess"]
    wire_p95 = summary["wire_time_seconds"]["p95"]
    wire_p50 = summary["wire_time_seconds"]["p50"]
    oxygen_p50 = summary["oxygen_remaining01"]["p50"]

    if suffocation > 0.50:
        oxygen_rate *= 0.55
        hunger_scale *= 0.94
        thirst_scale *= 0.94
        notes.append(f"oxygen_rate_hard_down: suffocation_before_wire={suffocation:.4f}")
    elif suffocation > 0.20:
        oxygen_rate *= 0.70
        hunger_scale *= 0.95
        thirst_scale *= 0.95
        notes.append(f"oxygen_rate_down: suffocation_before_wire={suffocation:.4f}")
    elif suffocation > 0.055:
        if oxygen_rate <= 0.016:
            max_oxygen *= 1.18
            notes.append(f"oxygen_capacity_up: suffocation_before_wire={suffocation:.4f}")
        else:
            oxygen_rate *= 0.85
            hunger_scale *= 0.96
            thirst_scale *= 0.96
            notes.append(f"oxygen_rate_fine_down: suffocation_before_wire={suffocation:.4f}")
    elif suffocation < 0.006 and excess > 0.18 and oxygen_p50 > 0.42:
        oxygen_rate *= 1.035
        notes.append(f"oxygen_rate_up: low_suffocation={suffocation:.4f} excess={excess:.4f}")

    if starvation > 0.07 or wire_p95 > 720.0:
        for biome_id, copper_delta, titanium_delta, silica_delta in (
            ("biome.safe_shallows", 10, -7, -3),
            ("biome.kelp_karst", 8, -5, -3),
            ("biome.sediment_drift", 5, -3, -2),
        ):
            deltas[(biome_id, COPPER_ID)] = deltas.get((biome_id, COPPER_ID), 0) + copper_delta
            deltas[(biome_id, TITANIUM_ID)] = deltas.get((biome_id, TITANIUM_ID), 0) + titanium_delta
            deltas[(biome_id, SILICA_ID)] = deltas.get((biome_id, SILICA_ID), 0) + silica_delta
        notes.append(f"early_copper_up: starvation={starvation:.4f} wire_p95={wire_p95:.1f}s")
    elif excess > 0.22 and wire_p50 < 260.0:
        for biome_id, copper_delta, fiber_delta, hydrocarbon_delta in (
            ("biome.safe_shallows", -7, 4, 3),
            ("biome.kelp_karst", -6, 3, 3),
            ("biome.sediment_drift", -4, 2, 2),
        ):
            deltas[(biome_id, COPPER_ID)] = deltas.get((biome_id, COPPER_ID), 0) + copper_delta
            deltas[(biome_id, FIBER_ID)] = deltas.get((biome_id, FIBER_ID), 0) + fiber_delta
            deltas[(biome_id, HYDROCARBON_ID)] = deltas.get((biome_id, HYDROCARBON_ID), 0) + hydrocarbon_delta
        tool_heat_scale *= 0.98
        notes.append(f"early_copper_down: excess={excess:.4f} wire_p50={wire_p50:.1f}s")

    max_oxygen = max(80.0, min(180.0, max_oxygen))
    oxygen_rate = max(0.015, min(0.55, oxygen_rate))
    hunger_scale = max(0.70, min(1.15, hunger_scale))
    thirst_scale = max(0.70, min(1.15, thirst_scale))
    tool_heat_scale = max(0.90, min(1.10, tool_heat_scale))
    return TuningState(max_oxygen, oxygen_rate, deltas, hunger_scale, thirst_scale, tool_heat_scale), notes


def apply_csv_weight_deltas(root: Path, deltas: dict[tuple[str, str], int]) -> list[dict]:
    if not deltas:
        return []
    path = root / "Data" / "Economy" / "Resource_Distribution_Matrix.csv"
    with path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        fieldnames = reader.fieldnames or []
        rows = list(reader)
    changes: list[dict] = []
    for row in rows:
        key = (row["biome_id"], row["resource_id"])
        delta = deltas.get(key, 0)
        if delta == 0:
            continue
        old = int(row["lcg_spawn_weight_u16"])
        new = max(1, old + delta)
        row["lcg_spawn_weight_u16"] = str(new)
        changes.append({"biome_id": key[0], "resource_id": key[1], "old": old, "new": new, "delta": new - old})
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)
    return changes


def replace_asset_float(text: str, key: str, value: float) -> str:
    return re.sub(
        rf"(^\s*{re.escape(key)}:\s*)[-+]?\d+(?:\.\d+)?(\s*$)",
        rf"\g<1>{value:.4f}\2",
        text,
        flags=re.MULTILINE,
    )


def apply_survival_asset_tuning(root: Path, state: TuningState, original: SurvivalStatsAuthority) -> list[dict]:
    path = original.source_path
    text = path.read_text(encoding="utf-8")
    changes: list[dict] = []
    if abs(original.max_oxygen - state.max_oxygen) > 0.0001:
        text = replace_asset_float(text, "maxOxygen", state.max_oxygen)
        changes.append(
            {
                "path": path.as_posix(),
                "field": "maxOxygen",
                "old": original.max_oxygen,
                "new": state.max_oxygen,
            }
        )
    if abs(original.oxygen_consumption_rate - state.oxygen_rate) > 0.0001:
        text = replace_asset_float(text, "oxygenConsumptionRate", state.oxygen_rate)
        changes.append(
            {
                "path": path.as_posix(),
                "field": "oxygenConsumptionRate",
                "old": original.oxygen_consumption_rate,
                "new": state.oxygen_rate,
            }
        )
    if changes:
        write_text_lf(path, text)
    return changes


def apply_survival_json_tuning(root: Path, state: TuningState, asset_stats: SurvivalStatsAuthority) -> list[dict]:
    path = root / "Data" / "Economy" / "Survival_Stats.json"
    data = read_json(path)
    oxygen = data.setdefault("oxygen", {})
    old_base = float(oxygen.get("base_liters_per_second", 0.0))
    old_capacity = float(oxygen.get("tank_capacity_liters", 0.0))
    target_capacity = round(state.max_oxygen * 6.0, 4)
    target_base = round(state.oxygen_rate * 6.0, 4)
    changes: list[dict] = []
    if abs(old_base - target_base) > 0.0001:
        oxygen["base_liters_per_second"] = target_base
        changes.append({"path": path.as_posix(), "field": "oxygen.base_liters_per_second", "old": old_base, "new": target_base})
    if old_capacity > 0.0 and abs(old_capacity - target_capacity) > 0.0001:
        oxygen["tank_capacity_liters"] = target_capacity
        changes.append({"path": path.as_posix(), "field": "oxygen.tank_capacity_liters", "old": old_capacity, "new": target_capacity})
    if changes:
        path.write_text(json.dumps(data, indent=2, ensure_ascii=True) + "\n", encoding="utf-8")
    return changes


def apply_metabolism_csv_tuning(root: Path, state: TuningState) -> list[dict]:
    path = root / "biological_metabolism_profiles.csv"
    with path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        fieldnames = reader.fieldnames or []
        rows = list(reader)
    changes: list[dict] = []
    for row in rows:
        if row.get("species") != "Player":
            continue
        old_cal = float(row["baseCalorieDrainPerSecond"])
        old_hyd = float(row["baseHydrationDrainPerSecond"])
        new_cal = round(old_cal * state.hunger_rate_scale, 6)
        new_hyd = round(old_hyd * state.thirst_rate_scale, 6)
        if abs(old_cal - new_cal) > 0.0000005:
            row["baseCalorieDrainPerSecond"] = f"{new_cal:.6f}".rstrip("0").rstrip(".")
            changes.append({"path": path.as_posix(), "field": "Player.baseCalorieDrainPerSecond", "old": old_cal, "new": new_cal})
        if abs(old_hyd - new_hyd) > 0.0000005:
            row["baseHydrationDrainPerSecond"] = f"{new_hyd:.6f}".rstrip("0").rstrip(".")
            changes.append({"path": path.as_posix(), "field": "Player.baseHydrationDrainPerSecond", "old": old_hyd, "new": new_hyd})
    if changes:
        with path.open("w", encoding="utf-8", newline="") as handle:
            writer = csv.DictWriter(handle, fieldnames=fieldnames)
            writer.writeheader()
            writer.writerows(rows)
    return changes


def apply_tool_heat_tuning(root: Path, state: TuningState) -> list[dict]:
    if abs(state.tool_heat_scale - 1.0) < 0.0001:
        return []
    path = root / "Data" / "Balance" / "ToolHeat.csv"
    with path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        fieldnames = reader.fieldnames or []
        rows = list(reader)
    changes: list[dict] = []
    for row in rows:
        if row["id"] not in {"repair_laser", "seam_cutter"}:
            continue
        old = float(row["heat_capacity"])
        new = round(max(1.0, old * state.tool_heat_scale), 3)
        row["heat_capacity"] = f"{new:g}"
        changes.append({"path": path.as_posix(), "field": row["id"] + ".heat_capacity", "old": old, "new": new})
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)
    return changes


def run_ore_baker(root: Path, iterations: int, world_seed: int) -> dict:
    command = [
        sys.executable,
        str(root / "Tools" / "OreLcgBaker.py"),
        "--root",
        str(root),
        "--iterations",
        str(iterations),
        "--world-seed",
        hex(world_seed),
    ]
    completed = subprocess.run(command, cwd=root, text=True, capture_output=True, check=False)
    return {
        "command": command,
        "returncode": completed.returncode,
        "stdout": completed.stdout.strip(),
        "stderr": completed.stderr.strip(),
    }


def write_report(root: Path, payload: dict, report_json: Path, report_md: Path) -> None:
    json_path = root / report_json
    md_path = root / report_md
    json_path.parent.mkdir(parents=True, exist_ok=True)
    md_path.parent.mkdir(parents=True, exist_ok=True)
    json_path.write_text(json.dumps(payload, indent=2, ensure_ascii=True) + "\n", encoding="utf-8")
    baseline = payload["baseline"]["summary"]
    final = payload["final"]["summary"]
    lines = [
        "# First 20 Minute Balance Monte Carlo - 17-B",
        "",
        "Evidence class: OFFLINE_PYTHON_STATIC_DATA. Runtime Unity proof remains PENDING VERIFICATION.",
        "",
        "## Authority",
        f"- Ore table: `{payload['authority']['ore_distribution']}`",
        f"- Survival asset: `{payload['authority']['survival_asset']}`",
        f"- Economy survival JSON: `{payload['authority']['economy_survival_json']}`",
        f"- Tool heat CSV: `{payload['authority']['tool_heat_csv']}`",
        "",
        "## Baseline",
        f"- runs: {baseline['runs']}",
        f"- suffocated before wire: {baseline['prob_suffocated_before_wire']:.6f}",
        f"- wire starved alive: {baseline['prob_wire_starved_alive']:.6f}",
        f"- survival devalued excess: {baseline['prob_survival_devalued_excess']:.6f}",
        f"- wire p95 seconds: {baseline['wire_time_seconds']['p95']:.3f}",
        f"- oxygen p50 remaining: {baseline['oxygen_remaining01']['p50']:.6f}",
        "",
        "## Final",
        f"- runs: {final['runs']}",
        f"- suffocated before wire: {final['prob_suffocated_before_wire']:.6f}",
        f"- wire starved alive: {final['prob_wire_starved_alive']:.6f}",
        f"- survival devalued excess: {final['prob_survival_devalued_excess']:.6f}",
        f"- wire p95 seconds: {final['wire_time_seconds']['p95']:.3f}",
        f"- oxygen p50 remaining: {final['oxygen_remaining01']['p50']:.6f}",
        "",
        "## Applied Changes",
    ]
    if payload["applied_changes"]:
        for change in payload["applied_changes"]:
            lines.append(f"- `{change.get('path', 'Data/Economy/Resource_Distribution_Matrix.csv')}` {change['field']}: {change['old']} -> {change['new']}")
    else:
        lines.append("- none")
    lines.extend(
        [
            "",
            "## Scalability",
            "- low: same authoritative tables, lower session count in CI smoke.",
            "- middle: 100k-session validation used here.",
            "- high: 1M-session audit acceptable when machine is idle.",
            "- ultra: more seeds only; gameplay authority and DTO layout unchanged.",
        ]
    )
    md_path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def run_cycle(args: argparse.Namespace) -> dict:
    root = Path(args.root).resolve()
    report_json = Path(args.report_json)
    report_md = Path(args.report_md)
    baseline_seed = uint32(int(args.world_seed))
    final_seed = uint32(int(args.validation_seed))
    duration = float(args.duration_seconds)
    runs = int(args.runs)
    passes = max(1, int(args.tuning_passes))
    tuning_runs = max(1_000, int(args.tuning_runs))

    economy_oxygen = load_economy_oxygen(root)
    tool_heat = load_tool_heat(root)
    source_stats = load_survival_stats(root)
    state = TuningState(source_stats.max_oxygen, source_stats.oxygen_consumption_rate, {}, 1.0, 1.0, 1.0)

    baseline_distribution = load_distribution(root)
    started = time.perf_counter()
    baseline_results = run_simulation(baseline_distribution, source_stats, runs, baseline_seed, duration)
    baseline_summary = summarize(baseline_results, time.perf_counter() - started)

    pass_reports = []
    current_summary = baseline_summary
    for pass_index in range(1, passes + 1):
        next_state, notes = derive_tuning(current_summary, state)
        if next_state == state:
            pass_reports.append({"pass": pass_index, "notes": ["no_change"], "summary": current_summary})
            break
        state = next_state
        candidate_distribution = load_distribution(root, state.csv_weight_delta)
        candidate_stats = load_survival_stats(root, state.oxygen_rate, state.max_oxygen)
        candidate_seed = uint32(baseline_seed + pass_index * 0x9E3779B9)
        started = time.perf_counter()
        candidate_results = run_simulation(candidate_distribution, candidate_stats, min(runs, tuning_runs), candidate_seed, duration)
        current_summary = summarize(candidate_results, time.perf_counter() - started)
        pass_reports.append({"pass": pass_index, "notes": notes, "summary": current_summary})

    applied_changes: list[dict] = []
    baker_result = None
    if args.apply:
        csv_changes = apply_csv_weight_deltas(root, state.csv_weight_delta)
        applied_changes.extend({"field": f"{item['biome_id']}/{item['resource_id']}", **item} for item in csv_changes)
        applied_changes.extend(apply_survival_asset_tuning(root, state, source_stats))
        applied_changes.extend(apply_survival_json_tuning(root, state, source_stats))
        applied_changes.extend(apply_metabolism_csv_tuning(root, state))
        applied_changes.extend(apply_tool_heat_tuning(root, state))
        if csv_changes:
            baker_result = run_ore_baker(root, int(args.baker_iterations), final_seed)
            if baker_result["returncode"] != 0:
                raise RuntimeError("OreLcgBaker failed: " + baker_result["stderr"])

    final_distribution = load_distribution(root)
    final_stats = load_survival_stats(root)
    started = time.perf_counter()
    final_results = run_simulation(final_distribution, final_stats, runs, final_seed, duration)
    final_summary = summarize(final_results, time.perf_counter() - started)

    payload = {
        "schema_id": "hecton8.first20_balance_monte_carlo.v1",
        "agent_id": "17-B",
        "evidence": "OFFLINE_PYTHON_STATIC_DATA",
        "runtime_proof_status": "PENDING_VERIFICATION",
        "parameters": {
            "runs": runs,
            "duration_seconds": duration,
            "baseline_seed": baseline_seed,
            "validation_seed": final_seed,
            "tuning_passes": passes,
            "tuning_runs": tuning_runs,
            "apply": bool(args.apply),
        },
        "authority": {
            "ore_distribution": baseline_distribution.source_path.as_posix(),
            "survival_asset": source_stats.source_path.as_posix(),
            "economy_survival_json": economy_oxygen.source_path.as_posix(),
            "tool_heat_csv": tool_heat.source_path.as_posix(),
            "runtime_oxygen_asset_max": source_stats.max_oxygen,
            "runtime_oxygen_asset_rate": source_stats.oxygen_consumption_rate,
            "economy_json_oxygen_capacity": economy_oxygen.tank_capacity_liters,
            "economy_json_oxygen_base_rate": economy_oxygen.base_liters_per_second,
        },
        "model_notes": [
            "Oxygen authority uses Standard_Suit_V1.asset because HectonSurvivalSystem reads SurvivalStats at runtime.",
            "Surface loop uses HectonSurvivalSystem surfaceOxygenRefillRate=15 units/sec.",
            "Pressure factor mirrors ResolveOxygenPressureScale: clamp(1 + depth*0.1, 1, 16).",
            "Copper clump bias mirrors ProceduralOreSpawner 85 percent repeat chance after copper, approximated as a short local-search streak.",
        ],
        "baseline": {"summary": baseline_summary},
        "tuning_passes": pass_reports,
        "final_tuning_state": {
            "max_oxygen": state.max_oxygen,
            "oxygen_rate": state.oxygen_rate,
            "csv_weight_delta": {f"{key[0]}/{key[1]}": value for key, value in sorted(state.csv_weight_delta.items()) if value != 0},
            "hunger_rate_scale": state.hunger_rate_scale,
            "thirst_rate_scale": state.thirst_rate_scale,
            "tool_heat_scale": state.tool_heat_scale,
        },
        "applied_changes": applied_changes,
        "ore_baker": baker_result,
        "final": {"summary": final_summary},
    }
    write_report(root, payload, report_json, report_md)
    return payload


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=str(Path(__file__).resolve().parents[2]))
    parser.add_argument("--runs", type=int, default=DEFAULT_RUNS)
    parser.add_argument("--duration-seconds", type=float, default=DEFAULT_DURATION_SECONDS)
    parser.add_argument("--world-seed", type=lambda value: int(value, 0), default=DEFAULT_WORLD_SEED)
    parser.add_argument("--validation-seed", type=lambda value: int(value, 0), default=0x17B17B17)
    parser.add_argument("--tuning-passes", type=int, default=5)
    parser.add_argument("--tuning-runs", type=int, default=10_000)
    parser.add_argument("--baker-iterations", type=int, default=200_000)
    parser.add_argument("--report-json", default=str(DEFAULT_REPORT_JSON))
    parser.add_argument("--report-md", default=str(DEFAULT_REPORT_MD))
    parser.add_argument("--apply", action="store_true", help="Write bounded tuning changes to CSV/JSON/asset files.")
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    payload = run_cycle(args)
    baseline = payload["baseline"]["summary"]
    final = payload["final"]["summary"]
    print(f"baseline_suffocated_before_wire={baseline['prob_suffocated_before_wire']:.6f}")
    print(f"baseline_wire_p95_seconds={baseline['wire_time_seconds']['p95']:.3f}")
    print(f"final_suffocated_before_wire={final['prob_suffocated_before_wire']:.6f}")
    print(f"final_wire_p95_seconds={final['wire_time_seconds']['p95']:.3f}")
    print(f"report_json={args.report_json}")
    print(f"report_md={args.report_md}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
