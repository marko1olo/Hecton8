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
- Runtime binding plan coverage for unresolved IDs; no unresolved ID is approved for runtime use.
- Time-to-first-submarine recursive recipe expansion.
- Cross-file resource baseline value alignment.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
import shutil
import uuid
from pathlib import Path
from statistics import median
from typing import Any


FNV_OFFSET = 2166136261
FNV_PRIME = 16777619
UINT32_MASK = 0xFFFFFFFF
EXPECTED_RESOURCE_MATRIX_ROWS = 150
EXPECTED_BIOME_COUNT = 10
EXPECTED_RESOURCE_COUNT = 15
EXPECTED_PRIMARY_RECIPE_COUNT = 40
RESOURCE_TIER_MIN = 1
RESOURCE_TIER_MAX = 3
U16_MAX = 65535
CSV_DATA_START_ROW = 2
RATIO_ROUND_DIGITS = 3
ECONOMY_FLOAT_TOLERANCE = 0.001
ENERGY_TERM_TOLERANCE = 0.000001
RARITY_DISTANCE_NORMALIZER_M = 1000.0
FAST_SWIM_CALORIE_MULTIPLIER = 3.0
FIRST_SUB_BATCH_MIN = 5
FIRST_SUB_BATCH_MAX = 50
FIRST_SUB_UNIQUE_RECIPE_MIN = 5
NEGATIVE_MASS_DRIFT_KG = 0.25
CRAFTING_MONTE_CARLO_MIN_STEPS = 1_000_000
EXPECTED_RECIPE_CATEGORY_COUNTS = {
    "recipe.category.component": 12,
    "recipe.category.tool": 6,
    "recipe.category.base_module": 8,
    "recipe.category.submarine_upgrade": 14,
}
EXPECTED_RESOURCE_MATRIX_HEADERS = (
    "biome_id",
    "biome_hash32",
    "biome_display_name",
    "depth_min_m",
    "depth_max_m",
    "distance_from_origin_m",
    "depth_multiplier",
    "resource_id",
    "resource_hash32",
    "resource_display_name",
    "resource_tier",
    "base_value_units",
    "base_rarity",
    "rarity_formula",
    "resource_depth_bias",
    "rarity_score",
    "lcg_spawn_weight_u16",
    "laser_hits_tier1",
    "laser_hits_tier2",
    "laser_hits_tier3",
)
EXPECTED_ITEMS_CSV_HEADERS = (
    "item_id",
    "item_hash32",
    "display_name",
    "item_kind",
    "source_recipe_id",
    "source_recipe_hash32",
    "category_id",
    "category_hash32",
    "progression_tier",
    "baseline_value_units",
    "is_raw_resource",
    "resource_tier",
    "biome_count",
    "positive_spawn_biome_count",
)
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
    for byte in value.encode("utf-16le"):
        hash_value ^= byte
        hash_value = (hash_value * FNV_PRIME) & UINT32_MASK
    return hash_value


def validate_hash_contract() -> None:
    sentinels = (
        ("Data_TitaniumScrap", 3511699502),
        ("emoji_contract_probe", 2044075353),
        ("LocHashProbe_" + chr(0x1F600), 1705751833),
    )
    for value, expected_hash in sentinels:
        actual_hash = fnv1a32(value)
        require(actual_hash == expected_hash, f"hash contract drift for {ascii(value)}: got {actual_hash}, expected {expected_hash}")


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
    for row_index, row in enumerate(rows, start=CSV_DATA_START_ROW):
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
            hash_value = row[hash_key]
            if value == "" and hash_value == "":
                continue
            require(value != "" and hash_value != "", f"CSV row {row_index}.{key}/{hash_key} must both be populated or both be empty")
            try:
                current_hash = int(hash_value)
            except ValueError:
                fail(f"CSV row {row_index}.{hash_key} is not an integer hash")
            previous = pairs.get(current_hash)
            if previous is not None and previous[0] != value:
                fail(f"CSV hash collision: {previous[0]} at {previous[1]} and {value} at row {row_index}.{key}")
            pairs[current_hash] = (value, f"row {row_index}.{key}")
            collected += 1
    return collected


def validate_resource_matrix(path: Path) -> dict[str, int]:
    require(path.exists(), f"missing {path}")
    with path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        require(tuple(reader.fieldnames or ()) == EXPECTED_RESOURCE_MATRIX_HEADERS, "Resource_Distribution_Matrix.csv header mismatch")
        rows = list(reader)

    require(len(rows) == EXPECTED_RESOURCE_MATRIX_ROWS, f"Resource_Distribution_Matrix.csv must have {EXPECTED_RESOURCE_MATRIX_ROWS} data rows, got {len(rows)}")
    hash_checks = check_csv_hashes(rows)
    collision_pairs: dict[int, tuple[str, str]] = {}
    collect_csv_id_hashes(rows, collision_pairs)
    biome_ids = {row["biome_id"] for row in rows}
    resource_ids = {row["resource_id"] for row in rows}
    require(len(biome_ids) == EXPECTED_BIOME_COUNT, f"expected {EXPECTED_BIOME_COUNT} biomes, got {len(biome_ids)}")
    require(len(resource_ids) == EXPECTED_RESOURCE_COUNT, f"expected {EXPECTED_RESOURCE_COUNT} resources, got {len(resource_ids)}")
    pairs: set[tuple[str, str]] = set()
    per_biome_counts: dict[str, int] = {biome_id: 0 for biome_id in biome_ids}

    for row_index, row in enumerate(rows, start=CSV_DATA_START_ROW):
        pair = (row["biome_id"], row["resource_id"])
        require(pair not in pairs, f"CSV row {row_index} duplicate biome/resource pair {pair}")
        pairs.add(pair)
        per_biome_counts[row["biome_id"]] += 1
        depth_min = float(row["depth_min_m"])
        depth_max = float(row["depth_max_m"])
        require(depth_max > depth_min >= 0.0, f"CSV row {row_index} invalid biome depth range")
        resource_tier = int(row["resource_tier"])
        require(RESOURCE_TIER_MIN <= resource_tier <= RESOURCE_TIER_MAX, f"CSV row {row_index} invalid resource tier {resource_tier}")
        base_value = float(row["base_value_units"])
        require(base_value > 0.0, f"CSV row {row_index} base_value_units must be positive")
        weight = int(row["lcg_spawn_weight_u16"])
        require(1 <= weight <= U16_MAX, f"CSV row {row_index} invalid LCG weight {weight}")
        h1 = int(row["laser_hits_tier1"])
        h2 = int(row["laser_hits_tier2"])
        h3 = int(row["laser_hits_tier3"])
        require(h1 >= h2 >= h3 >= 1, f"CSV row {row_index} invalid laser hit tier order")
        distance = float(row["distance_from_origin_m"])
        depth_multiplier = float(row["depth_multiplier"])
        base_rarity = float(row["base_rarity"])
        depth_bias = float(row["resource_depth_bias"])
        expected_rarity = round(base_rarity + (distance / RARITY_DISTANCE_NORMALIZER_M) * depth_multiplier * depth_bias, RATIO_ROUND_DIGITS)
        actual_rarity = round(float(row["rarity_score"]), RATIO_ROUND_DIGITS)
        require(actual_rarity == expected_rarity, f"CSV row {row_index} rarity mismatch: got {actual_rarity}, expected {expected_rarity}")
    for biome_id, row_count in per_biome_counts.items():
        require(row_count == EXPECTED_RESOURCE_COUNT, f"{biome_id} must have {EXPECTED_RESOURCE_COUNT} resource rows, got {row_count}")

    return {"rows": len(rows), "biomes": len(biome_ids), "resources": len(resource_ids), "hashes": hash_checks}


def validate_resource_value_alignment(matrix_path: Path, recipes_path: Path) -> int:
    with matrix_path.open("r", encoding="utf-8", newline="") as handle:
        rows = list(csv.DictReader(handle))
    recipes_data = load_json(recipes_path)
    item_values = {entry["item_id"]: float(entry["baseline_value_units"]) for entry in recipes_data.get("item_values", [])}
    resource_values: dict[str, float] = {}
    for row in rows:
        resource_id = row["resource_id"]
        matrix_value = float(row["base_value_units"])
        previous_value = resource_values.get(resource_id)
        if previous_value is None:
            resource_values[resource_id] = matrix_value
        else:
            require(abs(previous_value - matrix_value) <= ECONOMY_FLOAT_TOLERANCE, f"{resource_id} has inconsistent matrix base values")
    for resource_id, matrix_value in resource_values.items():
        require(resource_id in item_values, f"{resource_id} from matrix missing Recipes.item_values entry")
        require(abs(item_values[resource_id] - matrix_value) <= ECONOMY_FLOAT_TOLERANCE, f"{resource_id} matrix value {matrix_value} differs from Recipes.item_values {item_values[resource_id]}")
    return len(resource_values)


def validate_items_csv(path: Path, recipes_path: Path, matrix_path: Path) -> dict[str, int]:
    require(path.exists(), f"missing {path}")
    with path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        require(tuple(reader.fieldnames or ()) == EXPECTED_ITEMS_CSV_HEADERS, "Items.csv header mismatch")
        rows = list(reader)

    recipes_data = load_json(recipes_path)
    item_values = {entry["item_id"]: entry for entry in recipes_data.get("item_values", [])}
    recipes_by_result = {recipe["result"]["item_id"]: recipe for recipe in recipes_data.get("recipes", [])}
    resource_biomes: dict[str, set[str]] = {}
    positive_resource_biomes: dict[str, set[str]] = {}
    resource_tiers: dict[str, int] = {}
    with matrix_path.open("r", encoding="utf-8", newline="") as handle:
        for matrix_row in csv.DictReader(handle):
            resource_id = matrix_row["resource_id"]
            resource_biomes.setdefault(resource_id, set()).add(matrix_row["biome_id"])
            resource_tiers[resource_id] = int(matrix_row["resource_tier"])
            if int(matrix_row["lcg_spawn_weight_u16"]) > 0:
                positive_resource_biomes.setdefault(resource_id, set()).add(matrix_row["biome_id"])

    expected_raw_ids = set(resource_biomes)
    expected_crafted_ids = set(recipes_by_result)
    expected_item_ids = expected_raw_ids.union(expected_crafted_ids)
    require(expected_raw_ids.isdisjoint(expected_crafted_ids), "Items.csv raw resources and recipe outputs must not overlap")
    require(set(item_values) == expected_item_ids, f"Recipes.item_values set must equal raw resources plus recipe outputs: got {len(item_values)}, expected {len(expected_item_ids)}")

    seen: set[str] = set()
    hash_checks = 0
    raw_count = 0
    crafted_count = 0
    for row_index, row in enumerate(rows, start=2):
        item_id = row["item_id"]
        require(item_id not in seen, f"Items.csv row {row_index} duplicate item_id {item_id}")
        require(item_id in item_values, f"Items.csv row {row_index} item {item_id} missing Recipes.item_values entry")
        seen.add(item_id)

        expected_item_hash = fnv1a32(item_id)
        require(int(row["item_hash32"]) == expected_item_hash, f"Items.csv row {row_index} item hash mismatch")
        hash_checks += 1
        category_id = row["category_id"]
        require(category_id, f"Items.csv row {row_index} category_id is empty")
        require(int(row["category_hash32"]) == fnv1a32(category_id), f"Items.csv row {row_index} category hash mismatch")
        hash_checks += 1

        expected_value = float(item_values[item_id]["baseline_value_units"])
        require(abs(float(row["baseline_value_units"]) - expected_value) <= ECONOMY_FLOAT_TOLERANCE, f"Items.csv row {row_index} baseline value mismatch")
        source_recipe_id = row["source_recipe_id"]
        source_recipe_hash = row["source_recipe_hash32"]
        if source_recipe_id:
            require(source_recipe_hash, f"Items.csv row {row_index} source recipe hash missing")
            require(int(source_recipe_hash) == fnv1a32(source_recipe_id), f"Items.csv row {row_index} source recipe hash mismatch")
            hash_checks += 1
            require(item_id in recipes_by_result, f"Items.csv row {row_index} source recipe present but no recipe outputs {item_id}")
            recipe = recipes_by_result[item_id]
            require(recipe["recipe_id"] == source_recipe_id, f"Items.csv row {row_index} source recipe mismatch")
            require(row["item_kind"] == "crafted", f"Items.csv row {row_index} crafted item_kind mismatch")
            require(row["is_raw_resource"] == "false", f"Items.csv row {row_index} crafted is_raw_resource mismatch")
            require(int(row["progression_tier"]) == int(recipe["progression_tier"]), f"Items.csv row {row_index} crafted tier mismatch")
            crafted_count += 1
        else:
            require(not source_recipe_hash, f"Items.csv row {row_index} source recipe hash must be empty")
            require(item_id not in recipes_by_result, f"Items.csv row {row_index} crafted item missing source recipe")

        if item_id in resource_biomes:
            require(row["item_kind"] == "raw_resource", f"Items.csv row {row_index} raw item_kind mismatch")
            require(row["is_raw_resource"] == "true", f"Items.csv row {row_index} raw flag mismatch")
            require(int(row["resource_tier"]) == resource_tiers[item_id], f"Items.csv row {row_index} resource tier mismatch")
            require(int(row["biome_count"]) == len(resource_biomes[item_id]), f"Items.csv row {row_index} biome count mismatch")
            require(int(row["positive_spawn_biome_count"]) == len(positive_resource_biomes.get(item_id, set())), f"Items.csv row {row_index} positive biome count mismatch")
            raw_count += 1
        else:
            require(row["resource_tier"] == "", f"Items.csv row {row_index} non-resource tier must be empty")
            require(row["biome_count"] == "", f"Items.csv row {row_index} non-resource biome count must be empty")
            require(row["positive_spawn_biome_count"] == "", f"Items.csv row {row_index} non-resource positive biome count must be empty")

    require(len(rows) == len(expected_item_ids), f"Items.csv row count mismatch: got {len(rows)}, expected {len(expected_item_ids)}")
    require(seen == expected_item_ids, f"Items.csv item set mismatch: got {len(seen)}, expected {len(expected_item_ids)}")
    require(raw_count == len(expected_raw_ids), f"Items.csv raw count mismatch: got {raw_count}, expected {len(expected_raw_ids)}")
    require(crafted_count == len(expected_crafted_ids), f"Items.csv crafted count mismatch: got {crafted_count}, expected {len(expected_crafted_ids)}")
    return {"rows": len(rows), "hashes": hash_checks, "raw": raw_count, "crafted": crafted_count}


def validate_global_hash_collisions(economy_dir: Path) -> int:
    pairs: dict[int, tuple[str, str]] = {}
    with (economy_dir / "Resource_Distribution_Matrix.csv").open("r", encoding="utf-8", newline="") as handle:
        collect_csv_id_hashes(list(csv.DictReader(handle)), pairs)
    items_path = economy_dir / "Items.csv"
    if items_path.exists():
        with items_path.open("r", encoding="utf-8", newline="") as handle:
            collect_csv_id_hashes(list(csv.DictReader(handle)), pairs)
    collect_json_id_hashes(load_json(economy_dir / "Recipes.json"), "Recipes", pairs)
    collect_json_id_hashes(load_json(economy_dir / "Survival_Stats.json"), "Survival", pairs)
    binding_path = economy_dir / "Runtime_Binding_Review.json"
    if binding_path.exists():
        collect_json_id_hashes(load_json(binding_path), "RuntimeBinding", pairs)
    binding_plan_path = economy_dir / "Runtime_Binding_Plan.json"
    if binding_plan_path.exists():
        collect_json_id_hashes(load_json(binding_plan_path), "RuntimeBindingPlan", pairs)
    first_sub_path = economy_dir / "Time_To_First_Submarine.json"
    if first_sub_path.exists():
        collect_json_id_hashes(load_json(first_sub_path), "FirstSubmarine", pairs)
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
    require(len(recipes) == EXPECTED_PRIMARY_RECIPE_COUNT, f"Recipes.json must contain {EXPECTED_PRIMARY_RECIPE_COUNT} recipes, got {len(recipes)}")

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
        actual_cost = round(float(recipe["baseline_value_units"]), RATIO_ROUND_DIGITS)
        require(abs(actual_cost - round(computed_cost, RATIO_ROUND_DIGITS)) <= ECONOMY_FLOAT_TOLERANCE, f"{recipe_id} baseline cost mismatch")
        result_value = round(item_values[result_id] * result_quantity, RATIO_ROUND_DIGITS)
        require(abs(result_value - actual_cost) <= ECONOMY_FLOAT_TOLERANCE, f"{recipe_id} result value mismatch: got {result_value}, expected {actual_cost}")
        require(round(result_value * normal_reclaim_ratio, RATIO_ROUND_DIGITS) < actual_cost, f"{recipe_id} normal deconstruction can profit or break even")
        require(round(result_value * degraded_reclaim_ratio, RATIO_ROUND_DIGITS) < actual_cost, f"{recipe_id} degraded deconstruction can profit or break even")
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
    ratio_2_to_1 = round(tier2 / tier1, RATIO_ROUND_DIGITS)
    ratio_3_to_2 = round(tier3 / tier2, RATIO_ROUND_DIGITS)
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
    require(abs(fast - slow * FAST_SWIM_CALORIE_MULTIPLIER) <= ECONOMY_FLOAT_TOLERANCE, f"fast swim calories must be exactly {FAST_SWIM_CALORIE_MULTIPLIER:g}x slow swim, got {fast}/{slow}")
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


def validate_runtime_binding_plan(path: Path, review_path: Path) -> dict[str, int]:
    require(path.exists(), f"missing {path}")
    data = load_json(path)
    review = load_json(review_path)
    hash_checks = check_hash_pairs(data, "RuntimeBindingPlan")

    require(isinstance(data, dict), "binding plan root must be an object")
    require(isinstance(review, dict), "runtime binding review root must be an object")
    require(data.get("source_review_file") == "Data/Economy/Runtime_Binding_Review.json", "binding plan source_review_file mismatch")
    require(data.get("status_id") == "economy.binding_plan.pending_owner_decision", "binding plan status must remain pending owner decision")
    require(data.get("blocked_status_id") == "economy.binding.blocked_until_owner_confirmation", "binding plan blocked status drift")
    require(data.get("new_asset_status_id") == "economy.binding.requires_new_authoring_asset", "binding plan new-asset status drift")

    unresolved_bindings = review.get("unresolved_bindings")
    plan_entries = data.get("bindings")
    summary = data.get("summary")
    require(isinstance(unresolved_bindings, list) and unresolved_bindings, "runtime binding review unresolved_bindings must be a non-empty list")
    require(isinstance(plan_entries, list) and plan_entries, "binding plan bindings must be a non-empty list")
    require(isinstance(summary, dict), "binding plan summary must be an object")
    review_entries = {entry["economy_id"]: entry for entry in unresolved_bindings}
    require(len(review_entries) == len(unresolved_bindings), "runtime binding review has duplicate economy IDs")
    require(len(plan_entries) == int(summary["unresolved_economy_defined_ids"]), "binding plan summary unresolved count mismatch")
    require(len(plan_entries) == len(review_entries), "binding plan does not cover runtime binding review")

    seen: set[str] = set()
    candidate_count = 0
    author_count = 0
    runtime_allowed_count = 0
    owner_required_count = 0
    for entry in plan_entries:
        economy_id = entry["economy_id"]
        require(economy_id not in seen, f"duplicate binding plan entry {economy_id}")
        seen.add(economy_id)
        require(economy_id in review_entries, f"binding plan entry {economy_id} is not in Runtime_Binding_Review.json")
        review_entry = review_entries[economy_id]
        require(entry["economy_hash32"] == review_entry["economy_hash32"], f"{economy_id} plan hash does not match review")
        require(entry["owner_decision_required"] is True, f"{economy_id} must require owner decision")
        require(entry["runtime_use_allowed"] is False, f"{economy_id} must not be approved for runtime use")
        if entry["runtime_use_allowed"]:
            runtime_allowed_count += 1
        if entry["owner_decision_required"]:
            owner_required_count += 1

        suggested_id = review_entry["suggested_existing_id"]
        suggested_hash = review_entry["suggested_existing_hash32"]
        if suggested_id is None:
            author_count += 1
            require(entry["binding_strategy_id"] == "economy.binding_strategy.author_asset_required", f"{economy_id} must require new authoring")
            require(entry["candidate_existing_id"] is None, f"{economy_id} must not invent a candidate id")
            require(entry["candidate_existing_hash32"] is None, f"{economy_id} must not invent a candidate hash")
        else:
            candidate_count += 1
            require(entry["binding_strategy_id"] == "economy.binding_strategy.candidate_alias_pending_owner", f"{economy_id} must remain candidate-only")
            require(entry["candidate_existing_id"] == suggested_id, f"{economy_id} candidate id drift")
            require(entry["candidate_existing_hash32"] == suggested_hash, f"{economy_id} candidate hash drift")

    require(seen == set(review_entries), "binding plan entries do not match runtime binding review IDs")
    require(candidate_count == int(summary["candidate_alias_pending_owner_count"]), "binding plan candidate count mismatch")
    require(author_count == int(summary["author_asset_required_count"]), "binding plan authoring count mismatch")
    require(runtime_allowed_count == int(summary["runtime_use_allowed_count"]), "binding plan runtime-use count mismatch")
    require(owner_required_count == int(summary["owner_decision_required_count"]), "binding plan owner-decision count mismatch")
    require(runtime_allowed_count == 0, "binding plan must not approve unresolved IDs for runtime use")
    return {
        "hashes": hash_checks,
        "blocked": len(plan_entries),
        "candidates": candidate_count,
        "author_required": author_count,
    }


def expand_recipe_closure(item_id: str, quantity: int, recipes_by_output: dict[str, Any], raw: dict[str, int], recipe_batches: dict[str, int]) -> None:
    require(quantity > 0, f"recipe closure quantity for {item_id} must be positive")
    recipe = recipes_by_output.get(item_id)
    if recipe is None:
        raw[item_id] = raw.get(item_id, 0) + quantity
        return

    result_quantity = int(recipe["result"]["quantity"])
    require(result_quantity > 0, f"{recipe['recipe_id']} result quantity must be positive")
    batches = (quantity + result_quantity - 1) // result_quantity
    recipe_id = recipe["recipe_id"]
    recipe_batches[recipe_id] = recipe_batches.get(recipe_id, 0) + batches
    for ingredient in recipe["ingredients"]:
        expand_recipe_closure(ingredient["item_id"], int(ingredient["quantity"]) * batches, recipes_by_output, raw, recipe_batches)


def validate_first_submarine_path(path: Path, recipes_path: Path) -> dict[str, Any]:
    require(path.exists(), f"missing {path}")
    data = load_json(path)
    hash_checks = check_hash_pairs(data, "FirstSubmarine")
    recipes_data = load_json(recipes_path)
    recipes = recipes_data.get("recipes", [])
    recipes_by_output = {recipe["result"]["item_id"]: recipe for recipe in recipes}
    recipes_by_id = {recipe["recipe_id"]: recipe for recipe in recipes}

    raw: dict[str, int] = {}
    recipe_batches: dict[str, int] = {}
    target_items = data["target_items"]
    require(len(target_items) == 7, "first submarine path must have 7 target items")
    seen_targets: set[str] = set()
    for target in target_items:
        target_id = target["item_id"]
        require(target_id not in seen_targets, f"duplicate first submarine target {target_id}")
        seen_targets.add(target_id)
        target_quantity = int(target["quantity"])
        require(target_quantity > 0, f"first submarine target {target_id} quantity must be positive")
        expand_recipe_closure(target_id, target_quantity, recipes_by_output, raw, recipe_batches)

    recorded_raw: dict[str, int] = {}
    for entry in data["raw_resources"]:
        item_id = entry["item_id"]
        quantity = int(entry["quantity"])
        require(quantity > 0, f"first submarine raw resource {item_id} quantity must be positive")
        require(item_id not in recorded_raw, f"duplicate first submarine raw resource {item_id}")
        recorded_raw[item_id] = quantity
    require(recorded_raw == raw, "first submarine raw resource expansion mismatch")

    recorded_batches: dict[str, int] = {}
    for entry in data["recipe_batches"]:
        recipe_id = entry["recipe_id"]
        batches = int(entry["batches"])
        require(batches > 0, f"first submarine recipe batch {recipe_id} must be positive")
        require(recipe_id not in recorded_batches, f"duplicate first submarine recipe batch {recipe_id}")
        require(recipe_id in recipes_by_id, f"first submarine recipe batch {recipe_id} not found in Recipes.json")
        recipe = recipes_by_id[recipe_id]
        result_id = recipe["result"]["item_id"]
        require(entry["result_item_id"] == result_id, f"first submarine recipe batch {recipe_id} result item mismatch")
        recorded_batches[recipe_id] = batches
    require(recorded_batches == recipe_batches, "first submarine recipe batch expansion mismatch")

    top_level_kwh = 0.0
    top_level_seconds = 0.0
    for target in target_items:
        recipe = recipes_by_output.get(target["item_id"])
        require(recipe is not None, f"first submarine target {target['item_id']} has no recipe")
        quantity = int(target["quantity"])
        result_quantity = int(recipe["result"]["quantity"])
        require(result_quantity > 0, f"{recipe['recipe_id']} result quantity must be positive")
        batches = (quantity + result_quantity - 1) // result_quantity
        top_level_kwh += float(recipe["fabrication_kwh"]) * batches
        top_level_seconds += float(recipe["craft_time_seconds"]) * batches

    recursive_kwh = 0.0
    recursive_seconds = 0.0
    for recipe_id, batches in recipe_batches.items():
        recipe = recipes_by_id[recipe_id]
        recursive_kwh += float(recipe["fabrication_kwh"]) * batches
        recursive_seconds += float(recipe["craft_time_seconds"]) * batches

    assumptions = data["assumptions"]
    generator_kw = float(assumptions["generator_power_kw"])
    require(generator_kw > 0.0, "first submarine generator power must be positive")
    static_minutes = float(assumptions["static_path_minutes_excluding_energy_wait"])
    expected_top_wait = top_level_kwh / generator_kw * 60.0
    expected_recursive_wait = recursive_kwh / generator_kw * 60.0
    expected_literal_total = static_minutes + expected_recursive_wait
    totals = data["totals"]
    total_recursive_batches = sum(recipe_batches.values())
    unique_recursive_recipes = len(recipe_batches)
    require(abs(float(totals["top_level_target_only_fabrication_kwh"]) - round(top_level_kwh, RATIO_ROUND_DIGITS)) <= ECONOMY_FLOAT_TOLERANCE, "first submarine top-level kWh mismatch")
    require(abs(float(totals["top_level_target_only_craft_time_seconds"]) - round(top_level_seconds, RATIO_ROUND_DIGITS)) <= ECONOMY_FLOAT_TOLERANCE, "first submarine top-level craft time mismatch")
    require(abs(float(totals["top_level_energy_wait_minutes_at_30kw"]) - round(expected_top_wait, RATIO_ROUND_DIGITS)) <= ECONOMY_FLOAT_TOLERANCE, "first submarine top-level energy wait mismatch")
    require(int(totals["recursive_recipe_batches"]) == total_recursive_batches, "first submarine recursive batch count mismatch")
    require(int(totals["unique_recursive_recipes"]) == unique_recursive_recipes, "first submarine unique recipe count mismatch")
    require(FIRST_SUB_BATCH_MIN <= total_recursive_batches <= FIRST_SUB_BATCH_MAX, f"first submarine total recipe batches {total_recursive_batches} outside [{FIRST_SUB_BATCH_MIN}, {FIRST_SUB_BATCH_MAX}]")
    require(unique_recursive_recipes >= FIRST_SUB_UNIQUE_RECIPE_MIN, f"first submarine unique recipe count {unique_recursive_recipes} below {FIRST_SUB_UNIQUE_RECIPE_MIN}")
    require(abs(float(totals["recursive_fabrication_kwh"]) - round(recursive_kwh, RATIO_ROUND_DIGITS)) <= ECONOMY_FLOAT_TOLERANCE, "first submarine recursive kWh mismatch")
    require(abs(float(totals["recursive_craft_time_seconds"]) - round(recursive_seconds, RATIO_ROUND_DIGITS)) <= ECONOMY_FLOAT_TOLERANCE, "first submarine recursive craft time mismatch")
    require(abs(float(totals["recursive_energy_wait_minutes_at_30kw"]) - round(expected_recursive_wait, RATIO_ROUND_DIGITS)) <= ECONOMY_FLOAT_TOLERANCE, "first submarine recursive energy wait mismatch")
    require(abs(float(totals["literal_total_minutes_at_30kw"]) - round(expected_literal_total, RATIO_ROUND_DIGITS)) <= ECONOMY_FLOAT_TOLERANCE, "first submarine literal total mismatch")
    require(data["status_id"] == "economy.path.requires_literal_energy_rebalance", "first submarine status must flag literal kWh rebalance")
    return {"hashes": hash_checks, "recursive_kwh": round(recursive_kwh, 3), "literal_minutes": round(expected_literal_total, 3)}


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
            require(abs(min_value - previous_max) <= ECONOMY_FLOAT_TOLERANCE, f"{label} band gap/overlap before {entry.get('band_id', '<unknown>')}")
        if value_key is not None:
            current_value = float(entry[value_key])
            require(current_value > 0.0, f"{label} band {entry.get('band_id', '<unknown>')} has non-positive {value_key}")
            if previous_value is not None:
                require(current_value >= previous_value, f"{label} band {entry.get('band_id', '<unknown>')} regresses {value_key}")
            previous_value = current_value
        previous_max = max_value


def write_json(path: Path, data: Any) -> None:
    path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")


def copy_economy_files(source_dir: Path, target_dir: Path) -> None:
    for source in source_dir.iterdir():
        if source.is_file():
            shutil.copy2(source, target_dir / source.name)


def choose_distinct_negative_item_id(original_id: str) -> str:
    for candidate_id in ("Data_TitaniumScrap", "Data_CopperOre", "Data_SilicaShards"):
        if candidate_id != original_id:
            return candidate_id
    fail("negative result item mismatch test requires a distinct replacement ID")


def rebuild_first_submarine_report(data: dict[str, Any], recipes_data: dict[str, Any]) -> None:
    recipes = recipes_data.get("recipes", [])
    recipes_by_output = {recipe["result"]["item_id"]: recipe for recipe in recipes}
    recipes_by_id = {recipe["recipe_id"]: recipe for recipe in recipes}
    raw: dict[str, int] = {}
    recipe_batches: dict[str, int] = {}
    for target in data["target_items"]:
        expand_recipe_closure(target["item_id"], int(target["quantity"]), recipes_by_output, raw, recipe_batches)

    data["raw_resources"] = [
        {
            "item_id": item_id,
            "item_hash32": fnv1a32(item_id),
            "quantity": quantity,
        }
        for item_id, quantity in sorted(raw.items())
    ]
    data["recipe_batches"] = [
        {
            "recipe_id": recipe_id,
            "recipe_hash32": fnv1a32(recipe_id),
            "result_item_id": recipes_by_id[recipe_id]["result"]["item_id"],
            "result_item_hash32": fnv1a32(recipes_by_id[recipe_id]["result"]["item_id"]),
            "batches": batches,
        }
        for recipe_id, batches in sorted(recipe_batches.items())
    ]

    top_level_kwh = 0.0
    top_level_seconds = 0.0
    for target in data["target_items"]:
        recipe = recipes_by_output[target["item_id"]]
        quantity = int(target["quantity"])
        result_quantity = int(recipe["result"]["quantity"])
        batches = (quantity + result_quantity - 1) // result_quantity
        top_level_kwh += float(recipe["fabrication_kwh"]) * batches
        top_level_seconds += float(recipe["craft_time_seconds"]) * batches

    recursive_kwh = 0.0
    recursive_seconds = 0.0
    for recipe_id, batches in recipe_batches.items():
        recipe = recipes_by_id[recipe_id]
        recursive_kwh += float(recipe["fabrication_kwh"]) * batches
        recursive_seconds += float(recipe["craft_time_seconds"]) * batches

    generator_kw = float(data["assumptions"]["generator_power_kw"])
    static_minutes = float(data["assumptions"]["static_path_minutes_excluding_energy_wait"])
    totals = data["totals"]
    totals["top_level_target_only_fabrication_kwh"] = round(top_level_kwh, 3)
    totals["top_level_target_only_craft_time_seconds"] = round(top_level_seconds, 3)
    totals["top_level_energy_wait_minutes_at_30kw"] = round(top_level_kwh / generator_kw * 60.0, 3)
    totals["recursive_recipe_batches"] = sum(recipe_batches.values())
    totals["unique_recursive_recipes"] = len(recipe_batches)
    totals["recursive_fabrication_kwh"] = round(recursive_kwh, 3)
    totals["recursive_craft_time_seconds"] = round(recursive_seconds, 3)
    totals["recursive_energy_wait_minutes_at_30kw"] = round(recursive_kwh / generator_kw * 60.0, 3)
    totals["literal_total_minutes_at_30kw"] = round(static_minutes + recursive_kwh / generator_kw * 60.0, 3)


def mutate_first_sub_result_item(tmp_dir: Path) -> None:
    path = tmp_dir / "Time_To_First_Submarine.json"
    data = load_json(path)
    recipe_batches = data.get("recipe_batches")
    require(isinstance(recipe_batches, list) and recipe_batches, "negative result item mismatch test requires recipe batch rows")
    replacement_id = choose_distinct_negative_item_id(recipe_batches[0]["result_item_id"])
    recipe_batches[0]["result_item_id"] = replacement_id
    recipe_batches[0]["result_item_hash32"] = fnv1a32(replacement_id)
    write_json(path, data)


def mutate_first_sub_duplicate_raw(tmp_dir: Path) -> None:
    path = tmp_dir / "Time_To_First_Submarine.json"
    data = load_json(path)
    raw_resources = data.get("raw_resources")
    require(isinstance(raw_resources, list) and raw_resources, "negative duplicate raw resource test requires raw resource rows")
    raw_resources.append(dict(raw_resources[0]))
    write_json(path, data)


def mutate_first_sub_batch_band_overflow(tmp_dir: Path) -> None:
    path = tmp_dir / "Time_To_First_Submarine.json"
    recipes_path = tmp_dir / "Recipes.json"
    data = load_json(path)
    target_items = data.get("target_items")
    require(isinstance(target_items, list) and target_items, "negative first-sub batch band test requires target rows")
    for target in target_items:
        target["quantity"] = int(target["quantity"]) + 1
    rebuild_first_submarine_report(data, load_json(recipes_path))
    write_json(path, data)


def mutate_matrix_value_drift(tmp_dir: Path) -> None:
    path = tmp_dir / "Resource_Distribution_Matrix.csv"
    with path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        rows = list(reader)
        fieldnames = reader.fieldnames
    require(rows, "negative matrix value drift test requires matrix rows")
    require(fieldnames is not None, "negative matrix value drift test requires CSV headers")
    rows[0]["base_value_units"] = str(float(rows[0]["base_value_units"]) + 0.5)
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)


def mutate_items_missing_source_recipe(tmp_dir: Path) -> None:
    path = tmp_dir / "Items.csv"
    with path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        rows = list(reader)
        fieldnames = reader.fieldnames
    require(rows, "negative items source recipe test requires item rows")
    require(fieldnames is not None, "negative items source recipe test requires CSV headers")
    for row in rows:
        if row["source_recipe_id"]:
            row["source_recipe_id"] = ""
            row["source_recipe_hash32"] = ""
            break
    else:
        fail("negative items source recipe test requires a crafted item row")
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)


def mutate_binding_plan_runtime_allowed(tmp_dir: Path) -> None:
    path = tmp_dir / "Runtime_Binding_Plan.json"
    data = load_json(path)
    bindings = data.get("bindings")
    require(isinstance(bindings, list) and bindings, "negative binding plan test requires binding rows")
    bindings[0]["runtime_use_allowed"] = True
    data["summary"]["runtime_use_allowed_count"] = 1
    write_json(path, data)


def mutate_crafting_output_mass(tmp_dir: Path) -> None:
    path = tmp_dir / "Crafting_Costs.json"
    data = load_json(path)
    recipes = data.get("recipes")
    require(isinstance(recipes, list) and recipes, "negative crafting mass test requires recipes")
    recipes[0]["output_mass_kg"] = round(float(recipes[0]["output_mass_kg"]) + 0.25, 3)
    write_json(path, data)


def mutate_crafting_tier2_tool_gate(tmp_dir: Path) -> None:
    path = tmp_dir / "Crafting_Costs.json"
    data = load_json(path)
    recipes = data.get("recipes")
    require(isinstance(recipes, list) and recipes, "negative crafting tier gate test requires recipes")
    for recipe in recipes:
        if int(recipe["progression_tier"]) == 2:
            recipe["required_tools"] = []
            write_json(path, data)
            return
    fail("negative crafting tier gate test requires a tier 2 recipe")


def mutate_crafting_time_zero(tmp_dir: Path) -> None:
    path = tmp_dir / "Crafting_Costs.json"
    data = load_json(path)
    recipes = data.get("recipes")
    require(isinstance(recipes, list) and recipes, "negative crafting time test requires recipes")
    recipes[0]["FabricationTimeSeconds"] = 0
    write_json(path, data)


def mutate_crafting_monte_carlo_profit(tmp_dir: Path) -> None:
    path = tmp_dir / "Crafting_MonteCarlo_Audit.json"
    data = load_json(path)
    data["profit_steps"] = 1
    data["status"] = "NEGATIVE_TEST_PROFIT_LOOP"
    write_json(path, data)


def validate_crafting_costs(economy_dir: Path) -> dict[str, Any]:
    import VerifyCraftingCosts as crafting_verify

    path = economy_dir / "Crafting_Costs.json"
    data = crafting_verify.load_json(path)
    recipes = data["recipes"]
    catalog = crafting_verify.build_catalog(data)
    power_model = data["power_model"]
    hash_checks = len(crafting_verify.collect_id_hashes(data))
    require(len(recipes) == crafting_verify.TARGET_RECIPE_COUNT, "Crafting_Costs.json must contain 50 recipes")
    require(data["status_id"] == "economy.crafting_costs.balanced", "Crafting_Costs status mismatch")
    for recipe in recipes:
        recipe_id = recipe["recipe_id"]
        input_mass = sum(float(ingredient["total_mass_kg"]) for ingredient in recipe["ingredients"])
        require(abs(float(recipe["input_mass_kg"]) - input_mass) <= 0.001, f"{recipe_id} crafting input mass mismatch")
        require(abs(float(recipe["output_mass_kg"]) - input_mass) <= 0.001, f"{recipe_id} crafting output mass mismatch")
        require(float(recipe["PowerCost_kWh"]) > 0.0, f"{recipe_id} crafting power must be positive")
        require(float(recipe["FabricationTimeSeconds"]) > 0.0, f"{recipe_id} crafting time must be positive")
        require(float(recipe["deconstruct_reclaim_ratio"]) < 1.0, f"{recipe_id} crafting reclaim must stay below break-even")
        if int(recipe["progression_tier"]) == 2:
            has_tier1_tool = any(int(catalog[tool["item_id"]]["tier"]) == 1 for tool in recipe["required_tools"])
            require(has_tier1_tool, f"{recipe_id} tier 2 recipe missing tier 1 tool")
        energy_terms = crafting_verify.compute_energy_terms(recipe, catalog, power_model)
        for key, expected in energy_terms.items():
            tolerance = 0.001 if key == "total_kwh" else 0.000001
            require(abs(float(recipe["physical_energy_terms_kwh"][key]) - expected) <= tolerance, f"{recipe_id} crafting energy term mismatch {key}")
    full_binary = crafting_verify.verify_full_binary(data)
    toaster_binary = crafting_verify.verify_toaster_binary(data)
    monte_carlo_path = economy_dir / "Crafting_MonteCarlo_Audit.json"
    monte_carlo = load_json(monte_carlo_path)
    require(monte_carlo["data_sha256"] == hashlib.sha256(path.read_bytes()).hexdigest(), "Crafting Monte Carlo report is stale")
    require(int(monte_carlo["steps"]) >= 1_000_000, "Crafting Monte Carlo must run at least 1,000,000 steps")
    require(int(monte_carlo["profit_steps"]) == 0, "Crafting Monte Carlo found profit steps")
    require(str(monte_carlo["status"]) == "NO_INFINITE_RESOURCE_OR_ENERGY_LOOP", "Crafting Monte Carlo status mismatch")
    medians = data["tier_medians"]
    return {
        "recipes": len(data["recipes"]),
        "hashes": hash_checks,
        "tier_cost_ratios": [float(medians["tier2_to_tier1_cost_ratio"]), float(medians["tier3_to_tier2_cost_ratio"])],
        "tier_power_ratios": [float(medians["tier2_to_tier1_power_ratio"]), float(medians["tier3_to_tier2_power_ratio"])],
        "starter_o2_minutes": float(data["starter_edge_guard"]["total_minutes"]),
        "binary_bytes": int(full_binary["bytes"]),
        "toaster_binary_bytes": int(toaster_binary["bytes"]),
        "monte_carlo_steps": int(monte_carlo["steps"]),
    }


def run_negative_tests(economy_dir: Path) -> list[str]:
    tests = (
        (
            "first_sub_result_item_mismatch",
            mutate_first_sub_result_item,
            lambda tmp_dir: validate_first_submarine_path(tmp_dir / "Time_To_First_Submarine.json", tmp_dir / "Recipes.json"),
            "result item mismatch",
        ),
        (
            "first_sub_duplicate_raw_resource",
            mutate_first_sub_duplicate_raw,
            lambda tmp_dir: validate_first_submarine_path(tmp_dir / "Time_To_First_Submarine.json", tmp_dir / "Recipes.json"),
            "duplicate first submarine raw resource",
        ),
        (
            "first_sub_batch_band_overflow",
            mutate_first_sub_batch_band_overflow,
            lambda tmp_dir: validate_first_submarine_path(tmp_dir / "Time_To_First_Submarine.json", tmp_dir / "Recipes.json"),
            "outside [5, 50]",
        ),
        (
            "matrix_recipe_value_drift",
            mutate_matrix_value_drift,
            lambda tmp_dir: validate_resource_value_alignment(tmp_dir / "Resource_Distribution_Matrix.csv", tmp_dir / "Recipes.json"),
            "inconsistent matrix base values",
        ),
        (
            "items_missing_source_recipe",
            mutate_items_missing_source_recipe,
            lambda tmp_dir: validate_items_csv(tmp_dir / "Items.csv", tmp_dir / "Recipes.json", tmp_dir / "Resource_Distribution_Matrix.csv"),
            "crafted item missing source recipe",
        ),
        (
            "binding_plan_runtime_allowed",
            mutate_binding_plan_runtime_allowed,
            lambda tmp_dir: validate_runtime_binding_plan(tmp_dir / "Runtime_Binding_Plan.json", tmp_dir / "Runtime_Binding_Review.json"),
            "must not be approved for runtime use",
        ),
        (
            "crafting_output_mass_mismatch",
            mutate_crafting_output_mass,
            lambda tmp_dir: validate_crafting_costs(tmp_dir),
            "crafting output mass mismatch",
        ),
        (
            "crafting_tier2_missing_tool",
            mutate_crafting_tier2_tool_gate,
            lambda tmp_dir: validate_crafting_costs(tmp_dir),
            "tier 2 recipe missing tier 1 tool",
        ),
        (
            "crafting_zero_time",
            mutate_crafting_time_zero,
            lambda tmp_dir: validate_crafting_costs(tmp_dir),
            "crafting time must be positive",
        ),
        (
            "crafting_monte_carlo_profit",
            mutate_crafting_monte_carlo_profit,
            lambda tmp_dir: validate_crafting_costs(tmp_dir),
            "Crafting Monte Carlo found profit steps",
        ),
    )
    results: list[str] = []
    scratch_root = economy_dir.parents[1] / "Temp" / "EconomyValidatorNegative"
    scratch_root.mkdir(parents=True, exist_ok=True)
    for name, mutate, validate, expected_fragment in tests:
        temp_dir = scratch_root / f"{name}_{uuid.uuid4().hex}"
        temp_dir.mkdir(parents=True)
        try:
            copy_economy_files(economy_dir, temp_dir)
            mutate(temp_dir)
            try:
                validate(temp_dir)
            except SystemExit as exc:
                message = str(exc)
                require(expected_fragment in message, f"{name} failed with unexpected message: {message}")
                results.append(name)
                continue
            fail(f"{name} unexpectedly passed")
        finally:
            shutil.rmtree(temp_dir, ignore_errors=True)
    return results


def main() -> None:
    parser = argparse.ArgumentParser(description="Validate HECTON-8 economy JSON/CSV data.")
    parser.add_argument("--root", default=".", help="Repository root. Default: current directory.")
    parser.add_argument("--negative-tests", action="store_true", help="Run temporary-copy malformed data rejection tests.")
    args = parser.parse_args()
    root = Path(args.root)
    economy_dir = root / "Data" / "Economy"

    validate_hash_contract()
    matrix_result = validate_resource_matrix(economy_dir / "Resource_Distribution_Matrix.csv")
    recipe_result = validate_recipes(economy_dir / "Recipes.json")
    crafting_result = validate_crafting_costs(economy_dir)
    items_result = validate_items_csv(economy_dir / "Items.csv", economy_dir / "Recipes.json", economy_dir / "Resource_Distribution_Matrix.csv")
    aligned_resources = validate_resource_value_alignment(economy_dir / "Resource_Distribution_Matrix.csv", economy_dir / "Recipes.json")
    survival_result = validate_survival(economy_dir / "Survival_Stats.json")
    binding_result = validate_runtime_binding_review(economy_dir / "Runtime_Binding_Review.json", economy_dir / "Recipes.json", root)
    binding_plan_result = validate_runtime_binding_plan(economy_dir / "Runtime_Binding_Plan.json", economy_dir / "Runtime_Binding_Review.json")
    first_sub_result = validate_first_submarine_path(economy_dir / "Time_To_First_Submarine.json", economy_dir / "Recipes.json")
    unique_hashes = validate_global_hash_collisions(economy_dir)
    total_hashes = matrix_result["hashes"] + recipe_result["hashes"] + crafting_result["hashes"] + items_result["hashes"] + survival_result["hashes"] + binding_result["hashes"] + binding_plan_result["hashes"] + first_sub_result["hashes"]
    negative_results = run_negative_tests(economy_dir) if args.negative_tests else []
    print("ECONOMY VALIDATION OK")
    print(f"matrix_rows={matrix_result['rows']} biomes={matrix_result['biomes']} resources={matrix_result['resources']}")
    print(f"items_rows={items_result['rows']} raw={items_result['raw']} crafted={items_result['crafted']}")
    print(f"matrix_recipe_value_aligned_resources={aligned_resources}")
    print(f"recipes={recipe_result['recipes']} tier_ratios={recipe_result['tier_ratios']}")
    print(f"crafting_costs={crafting_result['recipes']} cost_ratios={crafting_result['tier_cost_ratios']} power_ratios={crafting_result['tier_power_ratios']} starter_o2_minutes={crafting_result['starter_o2_minutes']} binary_bytes={crafting_result['binary_bytes']} toaster_binary_bytes={crafting_result['toaster_binary_bytes']} monte_carlo_steps={crafting_result['monte_carlo_steps']}")
    print(f"survival_velocity_bands={survival_result['velocity_bands']}")
    print(f"binding_unresolved_ids={binding_result['unresolved']}")
    print(f"binding_plan_blocked_ids={binding_plan_result['blocked']} candidates={binding_plan_result['candidates']} author_required={binding_plan_result['author_required']}")
    print(f"first_sub_recursive_kwh={first_sub_result['recursive_kwh']} literal_minutes={first_sub_result['literal_minutes']}")
    print(f"hash_pairs_checked={total_hashes}")
    print(f"unique_id_hashes={unique_hashes}")
    for negative_result in negative_results:
        print(f"negative_test_{negative_result}=FAILED_AS_EXPECTED")
    if negative_results:
        print(f"negative_cases={len(negative_results)}")
    print("energy_pacing_warning=literal_30kw_requires_owner_decision")
    print("STATUS: ECONOMY BALANCED")


if __name__ == "__main__":
    main()
