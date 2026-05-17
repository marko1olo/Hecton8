#!/usr/bin/env python3
"""Monte Carlo exploit audit for CRAFTING_COST_BALANCER recipes."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any

from CraftingCostsBaker import KG_TO_MILLIGRAMS, KWH_TO_MILLIWATT_HOURS, UINT32_MASK, VALUE_UNITS_TO_MILLI


ROOT = Path(__file__).resolve().parents[1]
JSON_PATH = ROOT / "Data" / "Economy" / "Crafting_Costs.json"
REPORT_PATH = ROOT / "Data" / "Economy" / "Crafting_MonteCarlo_Audit.json"
DEFAULT_STEPS = 1_000_000
DEFAULT_SEED = 0xC0FFEE15
LCG_MULTIPLIER = 1664525
LCG_INCREMENT = 1013904223
UINT32_RANGE = 1 << 32


def load_json(path: Path) -> Any:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def lcg_next(state: int) -> int:
    return (state * LCG_MULTIPLIER + LCG_INCREMENT) & UINT32_MASK


def choose_index(state: int, count: int) -> int:
    return (state * count) >> 32


def build_value_catalog(data: dict[str, Any]) -> dict[str, float]:
    catalog: dict[str, float] = {}
    for resource in data["raw_resources"]:
        catalog[resource["item_id"]] = float(resource["baseline_value_units"])
    for recipe in data["recipes"]:
        catalog[recipe["result"]["item_id"]] = float(recipe["baseline_cost_units"])
    return catalog


def run(steps: int, seed: int) -> dict[str, Any]:
    data = load_json(JSON_PATH)
    recipes = data["recipes"]
    values = build_value_catalog(data)
    state = seed & UINT32_MASK
    profit_steps = 0
    max_value_delta = -10**18
    max_mass_delta = -10**18
    max_energy_delta = -10**18
    for _step in range(steps):
        state = lcg_next(state)
        recipe = recipes[choose_index(state, len(recipes))]
        input_value_milli = int(round(sum(values[ingredient["item_id"]] * int(ingredient["quantity"]) for ingredient in recipe["ingredients"]) * VALUE_UNITS_TO_MILLI))
        reclaim_ratio = float(recipe["deconstruct_reclaim_ratio"])
        reclaimed_value_milli = int(round(float(recipe["baseline_cost_units"]) * reclaim_ratio * VALUE_UNITS_TO_MILLI))
        value_delta = reclaimed_value_milli - input_value_milli
        input_mass_mg = int(round(float(recipe["input_mass_kg"]) * KG_TO_MILLIGRAMS))
        reclaimed_mass_mg = int(round(float(recipe["output_mass_kg"]) * reclaim_ratio * KG_TO_MILLIGRAMS))
        mass_delta = reclaimed_mass_mg - input_mass_mg
        energy_delta = -int(round(float(recipe["PowerCost_kWh"]) * KWH_TO_MILLIWATT_HOURS))
        if value_delta >= 0 or mass_delta >= 0 or energy_delta >= 0:
            profit_steps += 1
        max_value_delta = max(max_value_delta, value_delta)
        max_mass_delta = max(max_mass_delta, mass_delta)
        max_energy_delta = max(max_energy_delta, energy_delta)
    report = {
        "schema_id": "economy.crafting_monte_carlo_audit.v1",
        "data_path": "Data/Economy/Crafting_Costs.json",
        "data_sha256": hashlib.sha256(JSON_PATH.read_bytes()).hexdigest(),
        "steps": steps,
        "seed": seed,
        "selection": "multiply_high_to_range",
        "recipe_count": len(recipes),
        "profit_steps": profit_steps,
        "max_value_delta_milli_units": max_value_delta,
        "max_mass_delta_mg": max_mass_delta,
        "max_energy_delta_mwh": max_energy_delta,
        "status": "NO_INFINITE_RESOURCE_OR_ENERGY_LOOP" if profit_steps == 0 else "EXPLOIT_DETECTED",
    }
    REPORT_PATH.write_text(json.dumps(report, separators=(",", ":")) + "\n", encoding="utf-8")
    return report


def main() -> None:
    parser = argparse.ArgumentParser(description="Run deterministic crafting economy Monte Carlo audit.")
    parser.add_argument("--steps", type=int, default=DEFAULT_STEPS)
    parser.add_argument("--seed", type=lambda value: int(value, 0), default=DEFAULT_SEED)
    args = parser.parse_args()
    report = run(args.steps, args.seed)
    if int(report["profit_steps"]) != 0:
        raise SystemExit("CRAFTING MONTE CARLO FAILED")
    print("CRAFTING MONTE CARLO OK")
    print(f"steps={report['steps']} profit_steps={report['profit_steps']} seed={report['seed']}")
    print(f"max_value_delta_milli_units={report['max_value_delta_milli_units']} max_mass_delta_mg={report['max_mass_delta_mg']} max_energy_delta_mwh={report['max_energy_delta_mwh']}")


if __name__ == "__main__":
    main()
