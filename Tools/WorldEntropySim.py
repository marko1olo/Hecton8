#!/usr/bin/env python3
"""365-day deterministic macro-regrowth entropy test for HECTON-8."""

from __future__ import annotations

import argparse
import json
from pathlib import Path


STAGE_TOMBSTONE = 0
STAGE_SEED = 1
STAGE_IMMATURE = 2
STAGE_MATURE = 3
UINT32_MASK = 0xFFFFFFFF
MAX_SAFE_GRID_CELLS = 1_048_576
EXPECTED_BIOME_NAMES = ("Safe Shallows", "Temperate Reef", "Thermal Vent", "Deep Abyss")


def load_constants(path: Path) -> dict:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def validate_constants(constants: dict) -> None:
    width = int(constants["gridWidth"])
    height = int(constants["gridHeight"])
    if width < 1 or height < 1:
        raise ValueError("grid dimensions must be positive")
    if width * height > MAX_SAFE_GRID_CELLS:
        raise ValueError("grid cell count exceeds safe entropy harness budget")
    if int(constants["macroSectorMeters"]) < 1:
        raise ValueError("macroSectorMeters must be positive")
    if int(constants["baseGrowthProgressPerDayQ"]) < 1 or int(constants["baseGrowthProgressPerDayQ"]) > 255:
        raise ValueError("baseGrowthProgressPerDayQ must be in 1..255")
    for key in (
        "nutrientDiffusionPermille",
        "preyGrowthPermille",
        "predationPermille",
        "predatorConversionPermille",
        "predatorMortalityPermille",
    ):
        value = int(constants[key])
        if value < 0 or value > 1000:
            raise ValueError(f"{key} must be in 0..1000")
    if int(constants["seedToMatureProgressQ"]) < 1:
        raise ValueError("seedToMatureProgressQ must be positive")
    if int(constants["tombstoneBaseDecayDays"]) < 1:
        raise ValueError("tombstoneBaseDecayDays must be positive")
    if int(constants["minApexRespawnDays"]) < 1:
        raise ValueError("minApexRespawnDays must be positive")
    if int(constants["maxApexRespawnDays"]) < int(constants["minApexRespawnDays"]):
        raise ValueError("maxApexRespawnDays must be >= minApexRespawnDays")
    acceptance = constants["acceptance"]
    if int(acceptance["simulationDays"]) < 1:
        raise ValueError("acceptance simulationDays must be positive")
    if acceptance["mode"] != "total_overharvest":
        raise ValueError("acceptance mode must be total_overharvest")
    minimum_mature_ratio = float(acceptance["minFinalMatureRatio"])
    if minimum_mature_ratio <= 0.0 or minimum_mature_ratio > 1.0:
        raise ValueError("minFinalMatureRatio must be in (0, 1]")
    if float(acceptance["safeShallowsVsDeepAbyssMinRecoveryRatio"]) <= 0.0:
        raise ValueError("safeShallowsVsDeepAbyssMinRecoveryRatio must be positive")

    biomes = constants["biomes"]
    if len(biomes) != len(EXPECTED_BIOME_NAMES):
        raise ValueError("exactly four biome constants are required")

    for index, expected_name in enumerate(EXPECTED_BIOME_NAMES):
        biome = biomes[index]
        if int(biome["id"]) != index or biome["name"] != expected_name:
            raise ValueError("biome ids and names must match runtime indices")
        if int(biome["temperatureQ"]) <= 0:
            raise ValueError("biome temperatureQ must be positive")
        if int(biome["nutrientStartQ"]) < int(constants["minimumNutrientsQ"]):
            raise ValueError("biome nutrientStartQ must not undercut minimumNutrientsQ")


def rotate_left_u32(value: int, count: int) -> int:
    value &= UINT32_MASK
    return ((value << count) | (value >> (32 - count))) & UINT32_MASK


def hash32(value: int) -> int:
    value &= UINT32_MASK
    value ^= value >> 16
    value = (value * 0x7FEB352D) & UINT32_MASK
    value ^= value >> 15
    value = (value * 0x846CA68B) & UINT32_MASK
    value ^= value >> 16
    return value & UINT32_MASK


def resolve_biome(sector_x: int, sector_z: int, seed: int, height: int) -> int:
    height = max(1, height)
    value = (sector_x & UINT32_MASK) ^ rotate_left_u32(sector_z, 13) ^ (seed & UINT32_MASK)
    band = (hash32(value) * 100) >> 32
    local_z = sector_z % height
    if sector_z < 0 and local_z != 0:
        local_z -= height
    if local_z < 0:
        local_z = -local_z
    if local_z < height // 4 or band < 24:
        return 0
    if band < 58:
        return 1
    if band < 75:
        return 2
    return 3


def build_initial_state(constants: dict, total_overharvest: bool) -> dict:
    width = int(constants["gridWidth"])
    height = int(constants["gridHeight"])
    count = width * height
    penalty = int(constants["nutrientPenaltyOnMiningQ"]) if total_overharvest else 0
    minimum_nutrients = int(constants["minimumNutrientsQ"])
    world_seed = int(constants.get("entropyTestWorldSeed", 0))
    origin_x = int(constants.get("macroSectorOriginX", 0))
    origin_z = int(constants.get("macroSectorOriginZ", 0))
    biomes = constants["biomes"]
    apex_respawn_lut = build_apex_respawn_lut(constants)
    biome_ids = [0] * count
    nutrients = [0] * count
    nutrient_scratch = [0] * count
    temperatures = [0] * count
    stages = [STAGE_MATURE] * count
    tombstone_age = [0] * count
    progress = [int(constants["seedToMatureProgressQ"])] * count
    stock = [255] * count
    prey = [255] * count
    predator = [0] * count
    respawn = [0] * count

    for z in range(height):
        for x in range(width):
            index = z * width + x
            biome_id = resolve_biome(origin_x + x, origin_z + z, world_seed, height)
            biome = biomes[biome_id]
            biome_ids[index] = biome_id
            temperatures[index] = int(biome["temperatureQ"])
            nutrients[index] = max(minimum_nutrients, int(biome["nutrientStartQ"]) - penalty)
            predator[index] = max(8, min(96, temperatures[index] >> 2))
            respawn[index] = apex_respawn_lut[(prey[index] << 8) | predator[index]]

            if total_overharvest:
                stages[index] = STAGE_TOMBSTONE
                progress[index] = 0
                stock[index] = 0
                prey[index] = 0

    return {
        "width": width,
        "height": height,
        "count": count,
        "biome_ids": biome_ids,
        "nutrients": nutrients,
        "nutrient_scratch": nutrient_scratch,
        "temperatures": temperatures,
        "stages": stages,
        "tombstone_age": tombstone_age,
        "progress": progress,
        "stock": stock,
        "prey": prey,
        "predator": predator,
        "respawn": respawn,
        "apex_respawn_lut": apex_respawn_lut,
    }


def resolve_apex_respawn(prey_q: int, predator_q: int, constants: dict) -> int:
    prey_growth = int(constants["preyGrowthPermille"])
    predation = int(constants["predationPermille"])
    conversion = int(constants["predatorConversionPermille"])
    mortality = int(constants["predatorMortalityPermille"])
    min_days = int(constants["minApexRespawnDays"])
    max_days = int(constants["maxApexRespawnDays"])

    prey_delta = ((prey_q * prey_growth) - ((prey_q * predator_q * predation) // 255)) // 1000
    next_prey = max(0, min(255, prey_q + prey_delta))
    predator_delta = (((predator_q * next_prey * conversion) // 255) - (predator_q * mortality)) // 1000
    next_predator = max(0, min(255, predator_q + predator_delta))
    span = max_days - min_days
    delay = max_days - ((span * next_prey) // 255) + ((next_predator * 12) // 255)
    return max(min_days, min(max_days, delay))


def build_apex_respawn_lut(constants: dict) -> list[int]:
    table = [0] * (256 * 256)
    for prey_q in range(256):
        base = prey_q << 8
        for predator_q in range(256):
            table[base | predator_q] = resolve_apex_respawn(prey_q, predator_q, constants)
    return table


def step_day(state: dict, constants: dict) -> None:
    width = state["width"]
    height = state["height"]
    count = state["count"]
    nutrients = state["nutrients"]
    next_nutrients = state["nutrient_scratch"]
    stages = state["stages"]
    tombstone_age = state["tombstone_age"]
    progress = state["progress"]
    stock = state["stock"]
    prey = state["prey"]
    predator = state["predator"]
    respawn = state["respawn"]
    apex_respawn_lut = state["apex_respawn_lut"]
    temperatures = state["temperatures"]

    diffusion = int(constants["nutrientDiffusionPermille"])
    passive_recovery = int(constants["passiveNutrientRecoveryPerDayQ"])
    minimum_nutrients = int(constants["minimumNutrientsQ"])
    base_growth = int(constants["baseGrowthProgressPerDayQ"])
    mature_threshold = int(constants["seedToMatureProgressQ"])
    tombstone_decay = int(constants["tombstoneBaseDecayDays"])
    predator_conversion = int(constants["predatorConversionPermille"])
    predator_mortality = int(constants["predatorMortalityPermille"])

    last_x = width - 1
    last_z = height - 1
    for z in range(height):
        row = z * width
        row_up = row - width
        row_down = row + width
        for x in range(width):
            index = row + x
            total = 0
            neighbor_count = 0
            if x > 0:
                total += nutrients[index - 1]
                neighbor_count += 1
            if x < last_x:
                total += nutrients[index + 1]
                neighbor_count += 1
            if z > 0:
                total += nutrients[row_up + x]
                neighbor_count += 1
            if z < last_z:
                total += nutrients[row_down + x]
                neighbor_count += 1

            current_nutrient = nutrients[index]
            average = total // neighbor_count if neighbor_count else current_nutrient
            next_nutrient = current_nutrient + ((average - current_nutrient) * diffusion) // 1000 + passive_recovery
            if stages[index] == STAGE_TOMBSTONE:
                next_nutrient -= 1
            next_nutrients[index] = max(minimum_nutrients, min(255, next_nutrient))

    state["nutrients"] = next_nutrients
    state["nutrient_scratch"] = nutrients
    nutrients = next_nutrients

    for index in range(count):
        stage = stages[index]
        temperature = temperatures[index]
        if stage == STAGE_TOMBSTONE:
            tombstone_age[index] += 1
            decay_days = max(1, ((tombstone_decay * 255) + temperature - 1) // max(1, temperature))
            if tombstone_age[index] >= decay_days:
                stages[index] = STAGE_SEED
                tombstone_age[index] = 0
                progress[index] = 0
            respawn[index] = apex_respawn_lut[(prey[index] << 8) | predator[index]]
            continue

        growth = (base_growth * nutrients[index] * temperature) // (255 * 255)
        growth = max(1, growth)
        if stage == STAGE_SEED or stage == STAGE_IMMATURE:
            next_progress = progress[index] + growth
            if next_progress >= mature_threshold:
                stages[index] = STAGE_MATURE
                progress[index] = mature_threshold
                stock[index] = 255
                prey[index] = 255
            else:
                stages[index] = STAGE_IMMATURE
                progress[index] = next_progress
                stock[index] = min(180, (next_progress * 255) // mature_threshold)
                prey[index] = stock[index]
        else:
            refill = max(1, growth >> 1)
            stock[index] = min(255, stock[index] + refill)
            prey[index] = stock[index]

        predator_delta = (((predator[index] * prey[index] * predator_conversion) // 255) - (predator[index] * predator_mortality)) // 1000
        predator[index] = max(0, min(255, predator[index] + predator_delta))
        respawn[index] = apex_respawn_lut[(prey[index] << 8) | predator[index]]


def summarize(state: dict, day: int, first_half_recovery: list[int | None]) -> dict:
    biomes = 4
    mature_by_biome = [0] * biomes
    count_by_biome = [0] * biomes
    nutrient_by_biome = [0] * biomes
    respawn_by_biome = [0] * biomes
    for index, biome_id in enumerate(state["biome_ids"]):
        count_by_biome[biome_id] += 1
        nutrient_by_biome[biome_id] += state["nutrients"][index]
        respawn_by_biome[biome_id] += state["respawn"][index]
        if state["stages"][index] == STAGE_MATURE:
            mature_by_biome[biome_id] += 1

    for biome_id in range(biomes):
        if (
            count_by_biome[biome_id] > 0
            and first_half_recovery[biome_id] is None
            and mature_by_biome[biome_id] * 2 >= count_by_biome[biome_id]
        ):
            first_half_recovery[biome_id] = day

    return {
        "day": day,
        "matureByBiome": mature_by_biome,
        "countByBiome": count_by_biome,
        "avgNutrientsByBiome": [nutrient_by_biome[i] // max(1, count_by_biome[i]) for i in range(biomes)],
        "avgApexRespawnDaysByBiome": [respawn_by_biome[i] // max(1, count_by_biome[i]) for i in range(biomes)],
        "firstHalfRecoveryDays": first_half_recovery[:],
    }


def run_sim(constants: dict, days: int, total_overharvest: bool) -> tuple[dict, list[dict]]:
    if days < 1:
        raise ValueError("days must be >= 1")

    validate_constants(constants)
    state = build_initial_state(constants, total_overharvest)
    first_half_recovery: list[int | None] = [None, None, None, None]
    checkpoints: list[dict] = []
    checkpoint_days = {1, 28, 95, 180, 365, days}
    final_summary: dict | None = None
    for day in range(1, days + 1):
        step_day(state, constants)
        needs_recovery_scan = any(value is None for value in first_half_recovery)
        if day in checkpoint_days or needs_recovery_scan:
            final_summary = summarize(state, day, first_half_recovery)
            if day in checkpoint_days:
                checkpoints.append(final_summary)

    if final_summary is None or final_summary["day"] != days:
        final_summary = summarize(state, days, first_half_recovery)

    return final_summary, checkpoints


def calculate_balance(final: dict, constants: dict, total_overharvest: bool) -> tuple[bool, float, float]:
    safe_day = final["firstHalfRecoveryDays"][0]
    abyss_day = final["firstHalfRecoveryDays"][3]
    has_required_recovery = safe_day is not None and abyss_day is not None and safe_day > 0
    ratio = (abyss_day / safe_day) if has_required_recovery else 0.0
    final_mature_total = sum(final["matureByBiome"])
    final_count_total = sum(final["countByBiome"])
    final_mature_ratio = final_mature_total / max(1, final_count_total)
    required_ratio = float(constants["acceptance"]["safeShallowsVsDeepAbyssMinRecoveryRatio"])
    required_mature = float(constants["acceptance"]["minFinalMatureRatio"])
    if total_overharvest:
        balanced = has_required_recovery and ratio >= required_ratio and final_mature_ratio >= required_mature
    else:
        balanced = final_mature_ratio >= required_mature

    return balanced, ratio, final_mature_ratio


def main() -> int:
    parser = argparse.ArgumentParser(description="Run the HECTON-8 world regrowth entropy test.")
    parser.add_argument("--constants", default="Data/Economy/Regrowth_Constants.json")
    parser.add_argument("--days", type=int, default=365)
    parser.add_argument("--mode", choices=("total_overharvest", "baseline"), default="total_overharvest")
    args = parser.parse_args()
    if args.days < 1:
        parser.error("--days must be >= 1")

    constants = load_constants(Path(args.constants))
    final, checkpoints = run_sim(constants, args.days, args.mode == "total_overharvest")

    safe_day = final["firstHalfRecoveryDays"][0]
    abyss_day = final["firstHalfRecoveryDays"][3]
    balanced, ratio, final_mature_ratio = calculate_balance(
        final,
        constants,
        args.mode == "total_overharvest",
    )

    print("WORLD ENTROPY SIM")
    print(f"days={args.days} mode={args.mode}")
    for checkpoint in checkpoints:
        print(
            "checkpoint "
            f"day={checkpoint['day']} "
            f"mature={checkpoint['matureByBiome']} "
            f"nutrients={checkpoint['avgNutrientsByBiome']} "
            f"apexRespawn={checkpoint['avgApexRespawnDaysByBiome']}"
        )
    print(f"safe_shallows_half_recovery_days={safe_day}")
    print(f"deep_abyss_half_recovery_days={abyss_day}")
    print(f"safe_to_abyss_recovery_ratio={ratio:.3f}")
    print(f"final_mature_ratio={final_mature_ratio:.3f}")
    print(f"STATUS={'ENTROPY BALANCED' if balanced else 'ENTROPY FAILED'}")
    return 0 if balanced else 2


if __name__ == "__main__":
    raise SystemExit(main())
