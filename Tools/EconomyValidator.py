#!/usr/bin/env python3
"""Validate HECTON-8 static economy data.

Checks:
- Project LocHash-compatible FNV-1a 32-bit hashes for every *_id field with a sibling *_hash32 field.
- Resource matrix shape: 10 biomes x 15 resources.
- Positive deterministic LCG weights and laser cutter hit tiers.
- Recipe count, duplicate IDs, dependency cycles, and baseline value math.
- Result value parity and deconstruction no-profit checks.
- Three-to-one tier progression medians.
- Survival O2/calorie table integrity, including fast swim = 3x slow swim.
- Runtime binding review summary for economy-defined IDs.
"""

from __future__ import annotations

import argparse
import csv
import json
import re
from pathlib import Path
from statistics import median
from typing import Any


FNV_OFFSET = 2166136261
FNV_PRIME = 16777619
EXPECTED_RECIPE_CATEGORY_COUNTS = {
    "recipe.category.component": 12,
    "recipe.category.tool": 6,
    "recipe.category.base_module": 8,
    "recipe.category.submarine_upgrade": 14,
}
ASSET_SCAN_RELATIVE_DIRS = (
    "Assets/_Project/Data/Items",
    "Assets/_Project/Data/Construction",
    "Assets/_Project/Data/Lore/SuitUpgrades",
    "Assets/_Project/Data/Tools",
    "Assets/_Project/Data/Crafting/Recipes",
)
ASSET_ID_KEYS = ("stableId", "upgradeId", "m_Name")


def fnv1a32(value: str) -> int:
    """Match Hecton.Localization.LocHash.Compute as an unsigned 32-bit value."""
    hash_value = FNV_OFFSET
    for char in value:
        code_unit = ord(char)
        hash_value ^= code_unit & 0xFF
        hash_value = (hash_value * FNV_PRIME) & 0xFFFFFFFF
        hash_value ^= (code_unit >> 8) & 0xFF
        hash_value = (hash_value * FNV_PRIME) & 0xFFFFFFFF
    return hash_value


def fail(message: str) -> None:
    raise SystemExit(f"ECONOMY VALIDATION FAILED: {message}")


def require(condition: bool, message: str) -> None:
    if not condition:
        fail(message)


def load_json(path: Path) -> Any:
    try:
        with path.open("r", encoding="utf-8") as handle:
            return json.load(handle)
    except json.JSONDecodeError as exc:
        fail(f"{path} JSON syntax error: {exc}")


def check_hash_pairs(node: Any, path: str) -> int:
    checked = 0
    if isinstance(node, dict):
        for key, value in node.items():
            if key.endswith("_id"):
                hash_key = key[:-3] + "_hash32"
                require(hash_key in node, f"{path}.{key} missing sibling {hash_key}")
                if value is None:
                    require(node[hash_key] is None, f"{path}.{hash_key} must be null for null id")
                else:
                    require(isinstance(value, str), f"{path}.{key} must be a string or null")
                    expected = fnv1a32(value)
                    require(node[hash_key] == expected, f"{path}.{key} hash mismatch: got {node[hash_key]}, expected {expected}")
                    checked += 1
            checked += check_hash_pairs(value, f"{path}.{key}")
    elif isinstance(node, list):
        for index, value in enumerate(node):
            checked += check_hash_pairs(value, f"{path}[{index}]")
    return checked


def check_csv_hashes(rows: list[dict[str, str]]) -> int:
    checked = 0
    for row_index, row in enumerate(rows, start=2):
        for key, value in row.items():
            if not key.endswith("_id"):
                continue
            hash_key = key[:-3] + "_hash32"
            require(hash_key in row, f"CSV row {row_index} missing {hash_key}")
            expected = fnv1a32(value)
            actual = int(row[hash_key])
            require(actual == expected, f"CSV row {row_index} {key} hash mismatch: got {actual}, expected {expected}")
            checked += 1
    return checked


def collect_json_id_hashes(node: Any, path: str, pairs: dict[int, tuple[str, str]]) -> int:
    collected = 0
    if isinstance(node, dict):
        for key, value in node.items():
            if key.endswith("_id") and value is not None:
                hash_key = key[:-3] + "_hash32"
                if hash_key in node:
                    current_hash = int(node[hash_key])
                    previous = pairs.get(current_hash)
                    if previous is not None and previous[0] != value:
                        fail(f"hash collision: {previous[0]} at {previous[1]} and {value} at {path}.{key}")
                    pairs[current_hash] = (value, f"{path}.{key}")
                    collected += 1
            collected += collect_json_id_hashes(value, f"{path}.{key}", pairs)
    elif isinstance(node, list):
        for index, value in enumerate(node):
            collected += collect_json_id_hashes(value, f"{path}[{index}]", pairs)
    return collected


def collect_csv_id_hashes(rows: list[dict[str, str]], pairs: dict[int, tuple[str, str]]) -> int:
    collected = 0
    for row_index, row in enumerate(rows, start=2):
        for key, value in row.items():
            if not key.endswith("_id"):
                continue
            hash_key = key[:-3] + "_hash32"
            if hash_key not in row:
                continue
            current_hash = int(row[hash_key])
            previous = pairs.get(current_hash)
            if previous is not None and previous[0] != value:
                fail(f"CSV hash collision: {previous[0]} at {previous[1]} and {value} at row {row_index}.{key}")
            pairs[current_hash] = (value, f"row {row_index}.{key}")
            collected += 1
    return collected


def validate_resource_matrix(path: Path) -> dict[str, int]:
    require(path.exists(), f"missing {path}")
    with path.open("r", encoding="utf-8", newline="") as handle:
        rows = list(csv.DictReader(handle))

    require(len(rows) == 150, f"Resource_Distribution_Matrix.csv must have 150 data rows, got {len(rows)}")
    hash_checks = check_csv_hashes(rows)
    collision_pairs: dict[int, tuple[str, str]] = {}
    collect_csv_id_hashes(rows, collision_pairs)
    biome_ids = {row["biome_id"] for row in rows}
    resource_ids = {row["resource_id"] for row in rows}
    require(len(biome_ids) == 10, f"expected 10 biomes, got {len(biome_ids)}")
    require(len(resource_ids) == 15, f"expected 15 resources, got {len(resource_ids)}")
    pairs: set[tuple[str, str]] = set()
    per_biome_counts: dict[str, int] = {biome_id: 0 for biome_id in biome_ids}

    for row_index, row in enumerate(rows, start=2):
        pair = (row["biome_id"], row["resource_id"])
        require(pair not in pairs, f"CSV row {row_index} duplicate biome/resource pair {pair}")
        pairs.add(pair)
        per_biome_counts[row["biome_id"]] += 1
        weight = int(row["lcg_spawn_weight_u16"])
        require(1 <= weight <= 65535, f"CSV row {row_index} invalid LCG weight {weight}")
        h1 = int(row["laser_hits_tier1"])
        h2 = int(row["laser_hits_tier2"])
        h3 = int(row["laser_hits_tier3"])
        require(h1 >= h2 >= h3 >= 1, f"CSV row {row_index} invalid laser hit tier order")
        distance = float(row["distance_from_origin_m"])
        depth_multiplier = float(row["depth_multiplier"])
        base_rarity = float(row["base_rarity"])
        depth_bias = float(row["resource_depth_bias"])
        expected_rarity = round(base_rarity + (distance / 1000.0) * depth_multiplier * depth_bias, 3)
        actual_rarity = round(float(row["rarity_score"]), 3)
        require(actual_rarity == expected_rarity, f"CSV row {row_index} rarity mismatch: got {actual_rarity}, expected {expected_rarity}")
    for biome_id, row_count in per_biome_counts.items():
        require(row_count == 15, f"{biome_id} must have 15 resource rows, got {row_count}")

    return {"rows": len(rows), "biomes": len(biome_ids), "resources": len(resource_ids), "hashes": hash_checks}


def validate_global_hash_collisions(economy_dir: Path) -> int:
    pairs: dict[int, tuple[str, str]] = {}
    with (economy_dir / "Resource_Distribution_Matrix.csv").open("r", encoding="utf-8", newline="") as handle:
        collect_csv_id_hashes(list(csv.DictReader(handle)), pairs)
    collect_json_id_hashes(load_json(economy_dir / "Recipes.json"), "Recipes", pairs)
    collect_json_id_hashes(load_json(economy_dir / "Survival_Stats.json"), "Survival", pairs)
    binding_path = economy_dir / "Runtime_Binding_Review.json"
    if binding_path.exists():
        collect_json_id_hashes(load_json(binding_path), "RuntimeBinding", pairs)
    return len(pairs)


def collect_recipe_runtime_ids(recipes_data: dict[str, Any]) -> set[str]:
    ids: set[str] = set()
    for entry in recipes_data.get("item_values", []):
        ids.add(entry["item_id"])
    for recipe in recipes_data.get("recipes", []):
        ids.add(recipe["result"]["item_id"])
        for ingredient in recipe["ingredients"]:
            ids.add(ingredient["item_id"])
    return ids


def collect_discovered_asset_ids(root: Path) -> set[str]:
    discovered: set[str] = set()
    for relative_dir in ASSET_SCAN_RELATIVE_DIRS:
        scan_root = root / relative_dir
        if not scan_root.exists():
            continue
        for asset_path in scan_root.rglob("*.asset"):
            text = asset_path.read_text(encoding="utf-8", errors="ignore")
            for key in ASSET_ID_KEYS:
                pattern = rf"^\s*{re.escape(key)}:\s*(\S+)\s*$"
                for match in re.finditer(pattern, text, re.MULTILINE):
                    discovered.add(match.group(1).strip())
    return discovered


def detect_cycles(graph: dict[str, list[str]]) -> list[list[str]]:
    visiting: set[str] = set()
    visited: set[str] = set()
    stack: list[str] = []
    cycles: list[list[str]] = []

    def visit(node: str) -> None:
        if node in visited:
            return
        if node in visiting:
            cycle_start = stack.index(node) if node in stack else 0
            cycles.append(stack[cycle_start:] + [node])
            return
        visiting.add(node)
        stack.append(node)
        for neighbor in graph.get(node, []):
            visit(neighbor)
        stack.pop()
        visiting.remove(node)
        visited.add(node)

    for graph_node in graph:
        visit(graph_node)
    return cycles


def validate_recipes(path: Path) -> dict[str, Any]:
    require(path.exists(), f"missing {path}")
    data = load_json(path)
    hash_checks = check_hash_pairs(data, "Recipes")
    collision_pairs: dict[int, tuple[str, str]] = {}
    collect_json_id_hashes(data, "Recipes", collision_pairs)
    recipes = data.get("recipes", [])
    require(len(recipes) == 40, f"Recipes.json must contain 40 recipes, got {len(recipes)}")

    item_values: dict[str, float] = {}
    for entry in data.get("item_values", []):
        item_id = entry["item_id"]
        require(item_id not in item_values, f"duplicate item_values entry {item_id}")
        value = float(entry["baseline_value_units"])
        require(value > 0.0, f"item value for {item_id} must be positive")
        item_values[item_id] = value
    require(item_values, "Recipes.json item_values is empty")
    deconstruction = data["deconstruction"]
    normal_reclaim_ratio = float(deconstruction["normal_reclaim_ratio"])
    degraded_reclaim_ratio = float(deconstruction["degraded_reclaim_ratio"])
    require(0.0 <= degraded_reclaim_ratio <= normal_reclaim_ratio < 1.0, "deconstruction reclaim ratios must satisfy 0 <= degraded <= normal < 1")

    recipe_ids: set[str] = set()
    output_to_recipe: dict[str, str] = {}
    category_counts: dict[str, int] = {}
    tier_costs: dict[int, list[float]] = {1: [], 2: [], 3: []}
    dependency_graph: dict[str, list[str]] = {}

    for recipe in recipes:
        recipe_id = recipe["recipe_id"]
        require(recipe_id not in recipe_ids, f"duplicate recipe_id {recipe_id}")
        recipe_ids.add(recipe_id)
        category_id = recipe["category_id"]
        require(category_id in EXPECTED_RECIPE_CATEGORY_COUNTS, f"{recipe_id} invalid category_id {category_id}")
        category_counts[category_id] = category_counts.get(category_id, 0) + 1
        tier = int(recipe["progression_tier"])
        require(tier in tier_costs, f"{recipe_id} invalid progression_tier {tier}")
        require(float(recipe["craft_time_seconds"]) > 0.0, f"{recipe_id} craft time must be positive")
        require(float(recipe["fabrication_kwh"]) > 0.0, f"{recipe_id} fabrication kWh must be positive")
        result = recipe["result"]
        result_id = result["item_id"]
        require(result_id not in output_to_recipe, f"duplicate recipe output {result_id}")
        output_to_recipe[result_id] = recipe_id
        result_quantity = int(result["quantity"])
        require(result_quantity > 0, f"{recipe_id} result quantity must be positive")
        require(result_id in item_values, f"{recipe_id} result {result_id} missing item value")
        computed_cost = 0.0
        for ingredient in recipe["ingredients"]:
            item_id = ingredient["item_id"]
            quantity = int(ingredient["quantity"])
            require(quantity > 0, f"{recipe_id} ingredient {item_id} quantity must be positive")
            require(item_id in item_values, f"{recipe_id} ingredient {item_id} missing item value")
            computed_cost += item_values[item_id] * quantity
        actual_cost = round(float(recipe["baseline_value_units"]), 3)
        require(abs(actual_cost - round(computed_cost, 3)) <= 0.001, f"{recipe_id} baseline cost mismatch")
        result_value = round(item_values[result_id] * result_quantity, 3)
        require(abs(result_value - actual_cost) <= 0.001, f"{recipe_id} result value mismatch: got {result_value}, expected {actual_cost}")
        require(round(result_value * normal_reclaim_ratio, 3) < actual_cost, f"{recipe_id} normal deconstruction can profit or break even")
        require(round(result_value * degraded_reclaim_ratio, 3) < actual_cost, f"{recipe_id} degraded deconstruction can profit or break even")
        tier_costs[tier].append(actual_cost)

    for category_id, expected_count in EXPECTED_RECIPE_CATEGORY_COUNTS.items():
        actual_count = category_counts.get(category_id, 0)
        require(actual_count == expected_count, f"{category_id} count mismatch: got {actual_count}, expected {expected_count}")

    # Build graph after every output is known.
    dependency_graph = {}
    for recipe in recipes:
        result_id = recipe["result"]["item_id"]
        dependency_graph[result_id] = [ingredient["item_id"] for ingredient in recipe["ingredients"] if ingredient["item_id"] in output_to_recipe]
    cycles = detect_cycles(dependency_graph)
    if cycles:
        fail(f"recipe dependency cycle detected: {cycles[0]}")

    rule = data["progression_rule"]
    tier1 = median(tier_costs[1])
    tier2 = median(tier_costs[2])
    tier3 = median(tier_costs[3])
    ratio_2_to_1 = round(tier2 / tier1, 3)
    ratio_3_to_2 = round(tier3 / tier2, 3)
    min_ratio = float(rule["accepted_ratio_min"])
    max_ratio = float(rule["accepted_ratio_max"])
    require(min_ratio <= ratio_2_to_1 <= max_ratio, f"tier2/tier1 ratio {ratio_2_to_1} outside [{min_ratio}, {max_ratio}]")
    require(min_ratio <= ratio_3_to_2 <= max_ratio, f"tier3/tier2 ratio {ratio_3_to_2} outside [{min_ratio}, {max_ratio}]")
    return {"recipes": len(recipes), "hashes": hash_checks, "tier_ratios": [ratio_2_to_1, ratio_3_to_2]}


def validate_survival(path: Path) -> dict[str, Any]:
    require(path.exists(), f"missing {path}")
    data = load_json(path)
    hash_checks = check_hash_pairs(data, "Survival")
    collision_pairs: dict[int, tuple[str, str]] = {}
    collect_json_id_hashes(data, "Survival", collision_pairs)
    oxygen = data["oxygen"]
    require(float(oxygen["base_liters_per_second"]) > 0.0, "base O2 rate must be positive")
    require(len(oxygen["player_stress01_bands"]) == 5, "expected 5 stress O2 bands")
    require(len(oxygen["depth_pressure_mpa_bands"]) == 5, "expected 5 depth O2 bands")
    validate_numeric_bands(oxygen["player_stress01_bands"], "stress_min", "stress_max", "multiplier", "O2 stress")
    validate_numeric_bands(oxygen["depth_pressure_mpa_bands"], "pressure_min_mpa", "pressure_max_mpa", "multiplier", "O2 depth")
    validate_numeric_bands(oxygen["activity_bands"], "kcc_velocity_min_mps", "kcc_velocity_max_mps", "multiplier", "O2 activity")

    velocity_bands = data["caloric_burn"]["velocity_bands"]
    validate_numeric_bands(velocity_bands, "kcc_velocity_min_mps", "kcc_velocity_max_mps", "calories_per_minute", "calorie velocity")
    validate_numeric_bands(data["caloric_burn"]["load_bands"], "inventory_load01_min", "inventory_load01_max", "multiplier", "calorie load")
    validate_numeric_bands(data["caloric_burn"]["temperature_bands"], "internal_temp_min_c", "internal_temp_max_c", None, "calorie temperature")
    by_id = {entry["band_id"]: entry for entry in velocity_bands}
    slow = float(by_id["survival.calories.velocity.slow_swim"]["calories_per_minute"])
    fast = float(by_id["survival.calories.velocity.fast_swim"]["calories_per_minute"])
    require(abs(fast - slow * 3.0) <= 0.001, f"fast swim calories must be exactly 3x slow swim, got {fast}/{slow}")
    require(float(by_id["survival.calories.velocity.sprint_swim"]["calories_per_minute"]) > fast, "sprint swim must exceed fast swim calories")
    return {"hashes": hash_checks, "velocity_bands": len(velocity_bands)}


def validate_runtime_binding_review(path: Path, recipes_path: Path, root: Path) -> dict[str, int]:
    require(path.exists(), f"missing {path}")
    data = load_json(path)
    hash_checks = check_hash_pairs(data, "RuntimeBinding")
    recipes_data = load_json(recipes_path)
    recipe_ids = collect_recipe_runtime_ids(recipes_data)
    discovered_asset_ids = collect_discovered_asset_ids(root)
    matched_ids = recipe_ids.intersection(discovered_asset_ids)
    unresolved_ids = recipe_ids.difference(discovered_asset_ids)

    summary = data["scan_summary"]
    require(int(summary["recipe_unique_ids"]) == len(recipe_ids), "binding review recipe_unique_ids mismatch")
    require(int(summary["discovered_existing_asset_ids"]) == len(discovered_asset_ids), "binding review discovered_existing_asset_ids mismatch")
    require(int(summary["matched_existing_ids"]) == len(matched_ids), "binding review matched_existing_ids mismatch")
    require(int(summary["unresolved_economy_defined_ids"]) == len(unresolved_ids), "binding review unresolved count mismatch")
    require(int(summary["matched_existing_ids"]) + int(summary["unresolved_economy_defined_ids"]) == int(summary["recipe_unique_ids"]), "binding review matched+unresolved count mismatch")

    unresolved = data["unresolved_bindings"]
    require(len(unresolved) == int(summary["unresolved_economy_defined_ids"]), "binding review unresolved_bindings length mismatch")
    seen: set[str] = set()
    for entry in unresolved:
        economy_id = entry["economy_id"]
        require(economy_id not in seen, f"duplicate unresolved binding {economy_id}")
        seen.add(economy_id)
        require(economy_id in recipe_ids, f"unresolved binding {economy_id} is not referenced by Recipes.json")
        require(economy_id in unresolved_ids, f"unresolved binding {economy_id} now matches an asset id; update binding review")
        require(entry["binding_status_id"] == "economy.binding.requires_authoring_or_mapping", f"{economy_id} invalid binding status")
    require(seen == unresolved_ids, "binding review unresolved_bindings does not match current asset scan")
    return {"hashes": hash_checks, "unresolved": len(unresolved)}


def validate_numeric_bands(entries: list[dict[str, Any]], min_key: str, max_key: str, value_key: str | None, label: str) -> None:
    require(entries, f"{label} bands are empty")
    ordered = sorted(entries, key=lambda entry: float(entry[min_key]))
    previous_max: float | None = None
    previous_value: float | None = None
    for entry in ordered:
        min_value = float(entry[min_key])
        max_value = float(entry[max_key])
        require(max_value > min_value, f"{label} band {entry.get('band_id', '<unknown>')} has invalid range")
        if previous_max is not None:
            require(abs(min_value - previous_max) <= 0.001, f"{label} band gap/overlap before {entry.get('band_id', '<unknown>')}")
        if value_key is not None:
            current_value = float(entry[value_key])
            require(current_value > 0.0, f"{label} band {entry.get('band_id', '<unknown>')} has non-positive {value_key}")
            if previous_value is not None:
                require(current_value >= previous_value, f"{label} band {entry.get('band_id', '<unknown>')} regresses {value_key}")
            previous_value = current_value
        previous_max = max_value


def main() -> None:
    parser = argparse.ArgumentParser(description="Validate HECTON-8 economy JSON/CSV data.")
    parser.add_argument("--root", default=".", help="Repository root. Default: current directory.")
    args = parser.parse_args()
    root = Path(args.root)
    economy_dir = root / "Data" / "Economy"

    matrix_result = validate_resource_matrix(economy_dir / "Resource_Distribution_Matrix.csv")
    recipe_result = validate_recipes(economy_dir / "Recipes.json")
    survival_result = validate_survival(economy_dir / "Survival_Stats.json")
    binding_result = validate_runtime_binding_review(economy_dir / "Runtime_Binding_Review.json", economy_dir / "Recipes.json", root)
    unique_hashes = validate_global_hash_collisions(economy_dir)
    total_hashes = matrix_result["hashes"] + recipe_result["hashes"] + survival_result["hashes"] + binding_result["hashes"]
    print("ECONOMY VALIDATION OK")
    print(f"matrix_rows={matrix_result['rows']} biomes={matrix_result['biomes']} resources={matrix_result['resources']}")
    print(f"recipes={recipe_result['recipes']} tier_ratios={recipe_result['tier_ratios']}")
    print(f"survival_velocity_bands={survival_result['velocity_bands']}")
    print(f"binding_unresolved_ids={binding_result['unresolved']}")
    print(f"hash_pairs_checked={total_hashes}")
    print(f"unique_id_hashes={unique_hashes}")
    print("STATUS: ECONOMY BALANCED")


if __name__ == "__main__":
    main()
