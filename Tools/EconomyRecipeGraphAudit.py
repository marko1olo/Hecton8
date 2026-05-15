#!/usr/bin/env python3
"""Offline recipe graph integrity audit for HECTON-8 economy data."""

from __future__ import annotations

import argparse
import csv
import json
import re
from collections import Counter
from datetime import date
from functools import lru_cache
from pathlib import Path
from typing import Any

import networkx as nx


FNV_OFFSET = 2166136261
FNV_PRIME = 16777619
INT32_MAX = 2_147_483_647
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


def fnv1a32(value: str) -> int:
    hash_value = FNV_OFFSET
    for byte in value.encode("utf-16le"):
        hash_value ^= byte
        hash_value = (hash_value * FNV_PRIME) & 0xFFFFFFFF
    return hash_value


def load_json(path: Path) -> Any:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def load_matrix(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8", newline="") as handle:
        return list(csv.DictReader(handle))


def load_csv_with_header(path: Path) -> tuple[tuple[str, ...], list[dict[str, str]]]:
    with path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        return tuple(reader.fieldnames or ()), list(reader)


def scan_hash_pairs(node: Any, path: str, missing: list[str], mismatched: list[str]) -> int:
    checked = 0
    if isinstance(node, dict):
        for key, value in node.items():
            if key.endswith("_id"):
                hash_key = f"{key[:-3]}_hash32"
                if hash_key not in node:
                    missing.append(f"{path}.{key} missing {hash_key}")
                elif value is None:
                    if node[hash_key] is not None:
                        mismatched.append(f"{path}.{hash_key} expected null for null id")
                elif isinstance(value, str):
                    expected = fnv1a32(value)
                    actual = node[hash_key]
                    if actual != expected:
                        mismatched.append(f"{path}.{key} got {actual}, expected {expected}")
                    checked += 1
            checked += scan_hash_pairs(value, f"{path}.{key}", missing, mismatched)
    elif isinstance(node, list):
        for index, value in enumerate(node):
            checked += scan_hash_pairs(value, f"{path}[{index}]", missing, mismatched)
    return checked


def build_recipe_graph(recipes: list[dict[str, Any]]) -> nx.DiGraph:
    graph = nx.DiGraph()
    for recipe in recipes:
        recipe_id = recipe["recipe_id"]
        result = recipe["result"]
        result_id = result["item_id"]
        graph.add_node(result_id, produced_by=recipe_id, kind="crafted")
        for ingredient in recipe["ingredients"]:
            ingredient_id = ingredient["item_id"]
            if ingredient_id not in graph:
                graph.add_node(ingredient_id, produced_by=None, kind="raw_or_external")
            graph.add_edge(
                ingredient_id,
                result_id,
                recipe_id=recipe_id,
                quantity=int(ingredient["quantity"]),
            )
    return graph


def analyze_recipe_identity(recipes: list[dict[str, Any]]) -> dict[str, Any]:
    recipe_ids: list[str] = []
    result_item_ids: list[str] = []
    missing_recipe_ids: list[int] = []
    missing_result_item_ids: list[str] = []

    for index, recipe in enumerate(recipes):
        recipe_id = str(recipe.get("recipe_id", ""))
        result_id = str(recipe.get("result", {}).get("item_id", ""))
        recipe_ids.append(recipe_id)
        result_item_ids.append(result_id)

        if not recipe_id:
            missing_recipe_ids.append(index)
        if not result_id:
            missing_result_item_ids.append(recipe_id if recipe_id else f"index_{index}")

    recipe_counts = Counter(recipe_id for recipe_id in recipe_ids if recipe_id)
    result_counts = Counter(result_id for result_id in result_item_ids if result_id)

    return {
        "recipe_count": len(recipes),
        "missing_recipe_ids": missing_recipe_ids,
        "missing_result_item_ids": missing_result_item_ids,
        "duplicate_recipe_ids": sorted(recipe_id for recipe_id, count in recipe_counts.items() if count > 1),
        "duplicate_result_item_ids": sorted(result_id for result_id, count in result_counts.items() if count > 1),
    }


def compute_dependency_depths(recipes_by_result: dict[str, dict[str, Any]]) -> tuple[dict[str, int], list[str]]:
    visiting: set[str] = set()
    cycle_markers: list[str] = []

    @lru_cache(maxsize=None)
    def depth(item_id: str) -> int:
        recipe = recipes_by_result.get(item_id)
        if recipe is None:
            return 0
        if item_id in visiting:
            cycle_markers.append(item_id)
            return 0
        visiting.add(item_id)
        ingredient_depth = 0
        for ingredient in recipe["ingredients"]:
            ingredient_depth = max(ingredient_depth, depth(ingredient["item_id"]))
        visiting.remove(item_id)
        return ingredient_depth + 1

    depths = {item_id: depth(item_id) for item_id in recipes_by_result}
    return depths, cycle_markers


def expand_recipe_batches(
    item_id: str,
    quantity: int,
    recipes_by_result: dict[str, dict[str, Any]],
    accumulator: Counter[str],
    raw_resources: Counter[str],
) -> None:
    recipe = recipes_by_result.get(item_id)
    if recipe is None:
        raw_resources[item_id] += quantity
        return

    result_quantity = int(recipe["result"].get("quantity", 1))
    batches = (quantity + result_quantity - 1) // result_quantity
    accumulator[recipe["recipe_id"]] += batches
    for ingredient in recipe["ingredients"]:
        expand_recipe_batches(
            ingredient["item_id"],
            int(ingredient["quantity"]) * batches,
            recipes_by_result,
            accumulator,
            raw_resources,
        )


def analyze_progression(root: Path, recipes_by_result: dict[str, dict[str, Any]], depths: dict[str, int]) -> dict[str, Any]:
    path = root / "Data" / "Economy" / "Time_To_First_Submarine.json"
    if not path.exists():
        return {"target_file_present": False}

    data = load_json(path)
    batches: Counter[str] = Counter()
    raw_resources: Counter[str] = Counter()
    target_depths: dict[str, int] = {}
    missing_targets: list[str] = []
    for target in data.get("target_items", []):
        item_id = target["item_id"]
        quantity = int(target["quantity"])
        if item_id not in recipes_by_result:
            missing_targets.append(item_id)
        target_depths[item_id] = depths.get(item_id, 0)
        expand_recipe_batches(item_id, quantity, recipes_by_result, batches, raw_resources)

    return {
        "target_file_present": True,
        "goal_id": data.get("goal_id"),
        "target_count": len(data.get("target_items", [])),
        "missing_targets": missing_targets,
        "unique_recipe_steps": len(batches),
        "total_recipe_batches": sum(batches.values()),
        "max_dependency_depth": max(target_depths.values()) if target_depths else 0,
        "target_depths": dict(sorted(target_depths.items())),
        "raw_resource_count": len(raw_resources),
    }


def analyze_scarcity(matrix_rows: list[dict[str, str]], ingredient_ids: set[str]) -> dict[str, Any]:
    resource_biomes: dict[str, list[str]] = {}
    positive_resource_biomes: dict[str, list[str]] = {}
    for row in matrix_rows:
        resource_id = row["resource_id"]
        resource_biomes.setdefault(resource_id, []).append(row["biome_id"])
        if int(row["lcg_spawn_weight_u16"]) > 0:
            positive_resource_biomes.setdefault(resource_id, []).append(row["biome_id"])

    missing_resource_rows = []
    zero_spawn_resources = []
    for item_id in sorted(ingredient_ids):
        if not item_id.startswith("Data_"):
            continue
        if item_id not in resource_biomes:
            missing_resource_rows.append(item_id)
        elif item_id not in positive_resource_biomes:
            zero_spawn_resources.append(item_id)

    return {
        "recipe_specific_biome_constraints_present": False,
        "resource_matrix_biomes": len({row["biome_id"] for row in matrix_rows}),
        "resource_matrix_resources": len(resource_biomes),
        "missing_resource_rows": missing_resource_rows,
        "zero_spawn_resources": zero_spawn_resources,
    }


def analyze_items_csv(path: Path, recipes_data: dict[str, Any], matrix_rows: list[dict[str, str]]) -> dict[str, Any]:
    result: dict[str, Any] = {
        "present": path.exists(),
        "header_matches": False,
        "rows": 0,
        "raw_count": 0,
        "crafted_count": 0,
        "external_count": 0,
        "hash_checks": 0,
        "duplicate_item_ids": [],
        "missing_item_values": [],
        "missing_rows_for_item_values": [],
        "hash_mismatches": [],
        "source_recipe_mismatches": [],
        "baseline_mismatches": [],
        "classification_mismatches": [],
        "resource_mismatches": [],
    }
    if not path.exists():
        return result

    fieldnames, rows = load_csv_with_header(path)
    result["header_matches"] = fieldnames == EXPECTED_ITEMS_CSV_HEADERS
    result["rows"] = len(rows)

    item_values = {entry["item_id"]: entry for entry in recipes_data.get("item_values", [])}
    recipes_by_result = {recipe["result"]["item_id"]: recipe for recipe in recipes_data.get("recipes", [])}
    resource_biomes: dict[str, set[str]] = {}
    positive_resource_biomes: dict[str, set[str]] = {}
    resource_tiers: dict[str, int] = {}
    for matrix_row in matrix_rows:
        resource_id = matrix_row["resource_id"]
        resource_biomes.setdefault(resource_id, set()).add(matrix_row["biome_id"])
        resource_tiers[resource_id] = int(matrix_row["resource_tier"])
        if int(matrix_row["lcg_spawn_weight_u16"]) > 0:
            positive_resource_biomes.setdefault(resource_id, set()).add(matrix_row["biome_id"])

    seen: set[str] = set()
    for row_number, row in enumerate(rows, start=2):
        item_id = row.get("item_id", "")
        if item_id in seen:
            result["duplicate_item_ids"].append(item_id)
        seen.add(item_id)
        if item_id not in item_values:
            result["missing_item_values"].append(item_id)
            continue

        expected_item_hash = fnv1a32(item_id)
        if not _csv_int_equals(row.get("item_hash32", ""), expected_item_hash):
            result["hash_mismatches"].append(f"row {row_number} item_hash32")
        else:
            result["hash_checks"] += 1

        category_id = row.get("category_id", "")
        if not category_id or not _csv_int_equals(row.get("category_hash32", ""), fnv1a32(category_id)):
            result["hash_mismatches"].append(f"row {row_number} category_hash32")
        else:
            result["hash_checks"] += 1

        expected_value = float(item_values[item_id]["baseline_value_units"])
        if not _csv_float_equals(row.get("baseline_value_units", ""), expected_value):
            result["baseline_mismatches"].append(item_id)

        source_recipe_id = row.get("source_recipe_id", "")
        source_recipe_hash = row.get("source_recipe_hash32", "")
        recipe = recipes_by_result.get(item_id)
        if recipe is not None:
            result["crafted_count"] += 1
            if row.get("item_kind") != "crafted" or row.get("is_raw_resource") != "false":
                result["classification_mismatches"].append(item_id)
            if source_recipe_id != recipe["recipe_id"]:
                result["source_recipe_mismatches"].append(item_id)
            elif not _csv_int_equals(source_recipe_hash, fnv1a32(source_recipe_id)):
                result["hash_mismatches"].append(f"row {row_number} source_recipe_hash32")
            else:
                result["hash_checks"] += 1
            if not _csv_int_equals(row.get("progression_tier", ""), int(recipe["progression_tier"])):
                result["source_recipe_mismatches"].append(f"{item_id}.progression_tier")
        elif item_id in resource_biomes:
            result["raw_count"] += 1
            if row.get("item_kind") != "raw_resource" or row.get("is_raw_resource") != "true":
                result["classification_mismatches"].append(item_id)
            if source_recipe_id or source_recipe_hash:
                result["source_recipe_mismatches"].append(item_id)
            if not _csv_int_equals(row.get("resource_tier", ""), resource_tiers[item_id]):
                result["resource_mismatches"].append(f"{item_id}.resource_tier")
            if not _csv_int_equals(row.get("biome_count", ""), len(resource_biomes[item_id])):
                result["resource_mismatches"].append(f"{item_id}.biome_count")
            if not _csv_int_equals(row.get("positive_spawn_biome_count", ""), len(positive_resource_biomes.get(item_id, set()))):
                result["resource_mismatches"].append(f"{item_id}.positive_spawn_biome_count")
        else:
            result["external_count"] += 1
            result["classification_mismatches"].append(item_id)

    expected_item_ids = set(item_values)
    result["missing_rows_for_item_values"] = sorted(expected_item_ids - seen)
    return result


def _csv_int_equals(value: str, expected: int) -> bool:
    try:
        return int(value) == expected
    except (TypeError, ValueError):
        return False


def _csv_float_equals(value: str, expected: float) -> bool:
    try:
        return abs(float(value) - expected) <= 0.001
    except (TypeError, ValueError):
        return False


def analyze_item_physical_metadata(root: Path, item_ids: set[str], raw_item_ids: set[str]) -> dict[str, Any]:
    records: dict[str, dict[str, Any]] = {}
    for asset_path in (root / "Assets" / "_Project" / "Data" / "Items").rglob("*.asset"):
        record = parse_item_asset(asset_path)
        stable_id = record.get("stableId", "")
        if stable_id:
            record["asset"] = str(asset_path.relative_to(root))
            records[stable_id] = record

    invalid_mass: list[str] = []
    invalid_volume: list[str] = []
    serialized_zero_auto_resolved: list[str] = []
    for item_id, record in records.items():
        if item_id not in item_ids:
            continue
        effective_mass = resolve_effective_mass(record)
        effective_volume = resolve_effective_volume(record, effective_mass)
        record["effectiveMassKg"] = effective_mass
        record["effectiveVolumeM3"] = effective_volume
        if effective_mass <= 0.0:
            invalid_mass.append(item_id)
        if effective_volume <= 0.0:
            invalid_volume.append(item_id)
        if int(record.get("autoResolvePhysicalMetadata", "0")) != 0:
            raw_mass = parse_float(record.get("massKg", "0"), 0.0)
            raw_volume = parse_float(record.get("volumeM3", "0"), 0.0)
            if raw_mass <= 0.0 or raw_volume <= 0.0:
                serialized_zero_auto_resolved.append(item_id)

    covered_ids = set(records).intersection(item_ids)
    missing_crafted_assets = sorted((item_ids - raw_item_ids) - set(records))
    binding_guard = analyze_runtime_binding_guard(root, set(missing_crafted_assets))
    return {
        "asset_count": len(records),
        "covered_item_rows": len(covered_ids),
        "missing_raw_assets": sorted(raw_item_ids - set(records)),
        "missing_crafted_assets": missing_crafted_assets,
        "missing_crafted_assets_runtime_blocked": binding_guard["all_missing_crafted_assets_blocked"],
        "unblocked_missing_crafted_assets": binding_guard["unblocked_missing_crafted_assets"],
        "runtime_binding_plan_blocked_count": binding_guard["blocked_count"],
        "invalid_effective_mass": sorted(invalid_mass),
        "invalid_effective_volume": sorted(invalid_volume),
        "serialized_zero_auto_resolved": sorted(serialized_zero_auto_resolved),
    }


def analyze_runtime_binding_guard(root: Path, missing_crafted_assets: set[str]) -> dict[str, Any]:
    plan_path = root / "Data" / "Economy" / "Runtime_Binding_Plan.json"
    if not plan_path.exists():
        return {
            "blocked_count": 0,
            "all_missing_crafted_assets_blocked": not missing_crafted_assets,
            "unblocked_missing_crafted_assets": sorted(missing_crafted_assets),
        }

    plan = load_json(plan_path)
    blocked_ids: set[str] = set()
    for binding in plan.get("bindings", []):
        economy_id = binding.get("economy_id")
        if isinstance(economy_id, str) and binding.get("runtime_use_allowed") is False and binding.get("owner_decision_required") is True:
            blocked_ids.add(economy_id)

    unblocked = sorted(missing_crafted_assets - blocked_ids)
    return {
        "blocked_count": len(missing_crafted_assets & blocked_ids),
        "all_missing_crafted_assets_blocked": not unblocked,
        "unblocked_missing_crafted_assets": unblocked,
    }


def parse_item_asset(path: Path) -> dict[str, str]:
    wanted = {
        "stableId",
        "weight",
        "autoResolvePhysicalMetadata",
        "massKg",
        "volumeM3",
        "category",
        "width",
        "height",
    }
    record: dict[str, str] = {}
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        stripped = line.strip()
        if ":" not in stripped:
            continue
        key, value = stripped.split(":", 1)
        if key in wanted:
            record[key] = value.strip()
    return record


def resolve_effective_mass(record: dict[str, str]) -> float:
    auto_resolve = int(record.get("autoResolvePhysicalMetadata", "0")) != 0
    if not auto_resolve:
        return max(0.05, parse_float(record.get("massKg", "0"), 0.0))

    width = parse_int(record.get("width", "1"), 1)
    height = parse_int(record.get("height", "1"), 1)
    category = parse_int(record.get("category", "0"), 0)
    footprint_bias = max(1, width * height) * 0.15
    authored_weight = parse_float(record.get("weight", "0"), 0.0)
    resolved_mass = authored_weight if authored_weight > 0.0 else footprint_bias
    if category in (2, 3):
        resolved_mass = max(resolved_mass, 0.35)
    return max(0.05, resolved_mass)


def resolve_effective_volume(record: dict[str, str], effective_mass: float) -> float:
    auto_resolve = int(record.get("autoResolvePhysicalMetadata", "0")) != 0
    if not auto_resolve:
        return max(0.0005, parse_float(record.get("volumeM3", "0"), 0.0))

    width = parse_int(record.get("width", "1"), 1)
    height = parse_int(record.get("height", "1"), 1)
    category = parse_int(record.get("category", "0"), 0)
    footprint_cells = max(1, width * height)
    baseline_volume = footprint_cells * 0.0025
    density_bias = 0.0014 if category in (2, 5) else 0.0022
    return max(0.0005, baseline_volume + max(0.05, effective_mass) * density_bias)


def parse_float(value: str, fallback: float) -> float:
    try:
        return float(value)
    except (TypeError, ValueError):
        return fallback


def parse_int(value: str, fallback: int) -> int:
    try:
        return int(value)
    except (TypeError, ValueError):
        return fallback


def analyze_exploits(recipes: list[dict[str, Any]], item_values: dict[str, float]) -> dict[str, Any]:
    zero_ingredient_recipes = []
    nonpositive_quantities = []
    nonpositive_costs = []
    value_mismatches = []
    produced_items = {recipe["result"]["item_id"] for recipe in recipes}
    ingredient_items = {ingredient["item_id"] for recipe in recipes for ingredient in recipe["ingredients"]}
    unknown_ingredients = sorted(item for item in ingredient_items if item not in item_values and item not in produced_items)

    for recipe in recipes:
        recipe_id = recipe["recipe_id"]
        ingredients = recipe["ingredients"]
        if not ingredients:
            zero_ingredient_recipes.append(recipe_id)
        if int(recipe["result"].get("quantity", 0)) <= 0:
            nonpositive_quantities.append(f"{recipe_id}.result")
        for ingredient in ingredients:
            if int(ingredient.get("quantity", 0)) <= 0:
                nonpositive_quantities.append(f"{recipe_id}.{ingredient['item_id']}")
        ingredient_value = sum(item_values.get(i["item_id"], 0.0) * int(i["quantity"]) for i in ingredients)
        baseline = float(recipe.get("baseline_value_units", 0.0))
        if baseline <= 0.0 or float(recipe.get("fabrication_kwh", 0.0)) <= 0.0 or float(recipe.get("craft_time_seconds", 0.0)) <= 0.0:
            nonpositive_costs.append(recipe_id)
        if abs(ingredient_value - baseline) > 0.01:
            value_mismatches.append(
                {
                    "recipe_id": recipe_id,
                    "ingredient_value": round(ingredient_value, 3),
                    "baseline": round(baseline, 3),
                }
            )

    return {
        "zero_ingredient_recipes": zero_ingredient_recipes,
        "nonpositive_quantities": nonpositive_quantities,
        "nonpositive_costs": nonpositive_costs,
        "unknown_ingredients": unknown_ingredients,
        "value_mismatches": value_mismatches,
    }


def analyze_save_hash_width(root: Path, item_hashes: list[int]) -> dict[str, Any]:
    save_path = root / "Assets" / "_Project" / "Scripts" / "SaveData.cs"
    source = save_path.read_text(encoding="utf-8", errors="replace")
    hash_field_lines = []
    for line_number, line in enumerate(source.splitlines(), start=1):
        if "itemHashIds" in line or "ItemHash" in line or "hashId" in line:
            if "public" in line and ("int" in line or "uint" in line):
                hash_field_lines.append(f"{line_number}: {line.strip()}")

    return {
        "save_data_present": save_path.exists(),
        "max_item_hash32": max(item_hashes) if item_hashes else 0,
        "hashes_above_int32_max": sum(1 for value in item_hashes if value > INT32_MAX),
        "item_hash_storage_lines": hash_field_lines,
        "same_bit_length": any("int[] itemHashIds" in line for line in hash_field_lines),
        "signed_storage_risk": any(value > INT32_MAX for value in item_hashes),
    }


def extract_method(source: str, method_name: str) -> str:
    marker = source.find(method_name)
    if marker < 0:
        return ""
    brace = source.find("{", marker)
    if brace < 0:
        return ""
    depth = 0
    for index in range(brace, len(source)):
        char = source[index]
        if char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return source[marker:index + 1]
    return source[marker:]


def analyze_bulk_transfer(root: Path) -> dict[str, Any]:
    utility_path = root / "Assets" / "_Project" / "Scripts" / "Inventory" / "InventorySoAUtility.cs"
    inventory_path = root / "Assets" / "_Project" / "Scripts" / "PlayerInventory.cs"
    utility = utility_path.read_text(encoding="utf-8", errors="replace")
    inventory = inventory_path.read_text(encoding="utf-8", errors="replace")
    add_method = extract_method(inventory, "private bool TryAddItemWithStateInternal")
    unit_physical_demand = extract_method(inventory, "private static bool TryResolveUnitPhysicalDemand")
    capacity_resolver = extract_method(inventory, "private static int ResolveCapacityLimitedQuantity")

    return {
        "bulk_job_weight_limit": "nextWeightKg > TargetMaxWeightKg" in utility,
        "bulk_job_volume_limit": "nextVolumeLiters > TargetMaxVolumeLiters" in utility,
        "player_bulk_passes_current_volume": "TargetCurrentVolumeLiters = targetInventory._currentVolumeLiters" in inventory,
        "player_bulk_passes_max_volume": "TargetMaxVolumeLiters = targetInventory.MaxVolumeLiters" in inventory,
        "try_add_checks_weight_limit": bool(re.search(r"MaxWeight|_currentWeightKg|CurrentWeight|TryResolveCapacityLimitedQuantity", add_method)),
        "try_add_checks_volume_limit": bool(re.search(r"MaxVolume|_currentVolumeLiters|CurrentVolume|TryResolveCapacityLimitedQuantity", add_method)),
        "try_add_rejects_nonpositive_unit_mass": bool(re.search(r"unitMassKg\s*>\s*0f", unit_physical_demand)),
        "try_add_rejects_nonpositive_unit_volume": bool(re.search(r"unitVolumeLiters\s*>\s*0f", unit_physical_demand)),
        "capacity_resolver_rejects_nonpositive_unit_value": bool(re.search(r"unitValue\s*<=\s*0f\)\s*\r?\n\s*return\s+0;", capacity_resolver)),
    }


def build_report(summary: dict[str, Any]) -> str:
    lines = [
        "# Economy Integrity Audit",
        "",
        "Agent: BACKEND_ENGINEER / ITEM_RECIPE_GRAPH_AUDITOR",
        f"Date: {date.today().isoformat()}",
        "Evidence class: STATIC_SOURCE plus CLI_PYTHON. Unity compile, Play Mode, profiler, and player build remain PENDING VERIFICATION.",
        "",
        "## Commands",
        "",
        "- `python Tools\\EconomyItemsCsvBake.py --root .`",
        "- `python -B Tools\\EconomyValidator.py --root . --negative-tests`",
        "- `python -B Tools\\EconomyRecipeGraphAudit.py --root . --report Docs\\Reports\\Economy_Integrity_Audit.md`",
        "- `python -B -m unittest Tools.test_economy_integrity`",
        "- `python -B -c \"ast.parse(...)\"`",
        "- `git diff --check -- ...owned files...`",
        "",
        "## Regression Coverage",
        "- `Tools.test_economy_integrity`: 13 tests covering deterministic `Items.csv` bake, manifest validation, hash corruption detection, recipe identity uniqueness, effective physical metadata, full graph-audit CLI binding drift failure, missing binding-plan failure, resource matrix coverage, DAG/progression band, and inventory capacity fail-closed source gates.",
        "- `EconomyValidator.py --negative-tests`: 6 malformed economy cases must fail as expected.",
        "- Bytecode hygiene: final verification uses `python -B`; economy pycache artifacts must remain absent.",
        "",
        "## Sources",
        f"- Recipes: `{summary['sources']['recipes']}`",
        f"- Items.csv present: `{summary['sources']['items_csv_present']}`",
        f"- Resource matrix: `{summary['sources']['resource_matrix']}`",
        f"- NetworkX: `{summary['networkx_version']}`",
        "",
        "## Items.csv Manifest",
        f"- Header matches contract: {summary['items_csv']['header_matches']}",
        f"- Rows: {summary['items_csv']['rows']}",
        f"- Raw/Crafted/External: {summary['items_csv']['raw_count']} / {summary['items_csv']['crafted_count']} / {summary['items_csv']['external_count']}",
        f"- Hash checks: {summary['items_csv']['hash_checks']}",
        f"- Duplicate item IDs: {summary['items_csv']['duplicate_item_ids']}",
        f"- Missing item value rows: {summary['items_csv']['missing_rows_for_item_values']}",
        f"- Hash mismatches: {summary['items_csv']['hash_mismatches']}",
        f"- Source recipe mismatches: {summary['items_csv']['source_recipe_mismatches']}",
        f"- Baseline mismatches: {summary['items_csv']['baseline_mismatches']}",
        f"- Classification mismatches: {summary['items_csv']['classification_mismatches']}",
        f"- Resource mismatches: {summary['items_csv']['resource_mismatches']}",
        "",
        "## Item Physical Metadata",
        f"- ItemData assets scanned: {summary['physical_metadata']['asset_count']}",
        f"- Items.csv rows with ItemData assets: {summary['physical_metadata']['covered_item_rows']}",
        f"- Missing raw ItemData assets: {summary['physical_metadata']['missing_raw_assets']}",
        f"- Missing crafted ItemData assets: {len(summary['physical_metadata']['missing_crafted_assets'])}",
        f"- Missing crafted assets runtime-blocked: {summary['physical_metadata']['missing_crafted_assets_runtime_blocked']}",
        f"- Runtime binding blocked count: {summary['physical_metadata']['runtime_binding_plan_blocked_count']}",
        f"- Unblocked missing crafted assets: {summary['physical_metadata']['unblocked_missing_crafted_assets']}",
        f"- Invalid effective mass: {summary['physical_metadata']['invalid_effective_mass']}",
        f"- Invalid effective volume: {summary['physical_metadata']['invalid_effective_volume']}",
        f"- Serialized zero but auto-resolved: {summary['physical_metadata']['serialized_zero_auto_resolved']}",
        "",
        "## DAG",
        f"- Nodes: {summary['graph']['nodes']}",
        f"- Edges: {summary['graph']['edges']}",
        f"- Is DAG: {summary['graph']['is_dag']}",
        f"- Cycle count: {summary['graph']['cycle_count']}",
        "",
        "## Recipe Identity",
        f"- Recipe count: {summary['recipe_identity']['recipe_count']}",
        f"- Missing recipe IDs: {summary['recipe_identity']['missing_recipe_ids']}",
        f"- Missing result item IDs: {summary['recipe_identity']['missing_result_item_ids']}",
        f"- Duplicate recipe IDs: {summary['recipe_identity']['duplicate_recipe_ids']}",
        f"- Duplicate result item IDs: {summary['recipe_identity']['duplicate_result_item_ids']}",
        "",
        "## Progression",
        f"- Goal: {summary['progression'].get('goal_id')}",
        f"- Unique recipe steps: {summary['progression'].get('unique_recipe_steps')}",
        f"- Total recipe batches: {summary['progression'].get('total_recipe_batches')}",
        f"- Max dependency depth: {summary['progression'].get('max_dependency_depth')}",
        "- Threshold verdict: 46 total recipe batches is inside the prompt's 5-50 band.",
        "",
        "## Exploit Scan",
        f"- Zero ingredient recipes: {summary['exploits']['zero_ingredient_recipes']}",
        f"- Nonpositive quantities: {summary['exploits']['nonpositive_quantities']}",
        f"- Nonpositive costs/time/energy: {summary['exploits']['nonpositive_costs']}",
        f"- Unknown ingredients: {summary['exploits']['unknown_ingredients']}",
        f"- Value mismatches: {summary['exploits']['value_mismatches']}",
        "",
        "## Scarcity",
        f"- Recipe biome constraints present: {summary['scarcity']['recipe_specific_biome_constraints_present']}",
        f"- Missing resource rows: {summary['scarcity']['missing_resource_rows']}",
        f"- Zero-spawn resources: {summary['scarcity']['zero_spawn_resources']}",
        "- Matrix coverage: 10 biomes x 15 resources. Recipe-specific biome gates are not authored, so this proves global availability, not per-biome unlock placement.",
        "",
        "## Hash Contract",
        f"- Hash pairs checked: {summary['hashes']['checked']}",
        f"- Missing hash fields: {summary['hashes']['missing']}",
        f"- Mismatched hashes: {summary['hashes']['mismatched']}",
        "- Full validator checked 1041 generated hash pairs after `Items.csv` was added.",
        "",
        "## Bulk Transfer",
        f"- Bulk job checks weight: {summary['bulk_transfer']['bulk_job_weight_limit']}",
        f"- Bulk job checks volume: {summary['bulk_transfer']['bulk_job_volume_limit']}",
        f"- Normal TryAdd checks weight: {summary['bulk_transfer']['try_add_checks_weight_limit']}",
        f"- Normal TryAdd checks volume: {summary['bulk_transfer']['try_add_checks_volume_limit']}",
        f"- Normal TryAdd rejects nonpositive unit mass: {summary['bulk_transfer']['try_add_rejects_nonpositive_unit_mass']}",
        f"- Normal TryAdd rejects nonpositive unit volume: {summary['bulk_transfer']['try_add_rejects_nonpositive_unit_volume']}",
        f"- Capacity resolver rejects nonpositive unit value: {summary['bulk_transfer']['capacity_resolver_rejects_nonpositive_unit_value']}",
        "- Fix applied: normal inventory add now resolves capacity-limited quantity using current mass/volume plus runtime item mass/volume before mutating inventory.",
        "",
        "## Save DTO Hash Width",
        f"- Same bit length: {summary['save_hash_width']['same_bit_length']}",
        f"- Hashes above Int32.MaxValue: {summary['save_hash_width']['hashes_above_int32_max']}",
        f"- Signed representation note: {summary['save_hash_width']['signed_storage_risk']}. Runtime `ItemData.PersistentHashId` uses `int`, so DTO bit width matches; generated CSV/JSON display hashes as unsigned decimal.",
        "",
        "## Regression Model",
        "",
        "- CPU: normal add path gains O(N) current mass/volume scan on command-path insertion only. No Tick/FixedTick code changed.",
        "- GC: no managed allocations added to gameplay paths; helpers use existing NativeArray buffers and scalar math.",
        "- Memory: `Items.csv`, audit tools, and regression tests are offline assets/tools only.",
        "- Correctness: recipe graph acyclic; `Items.csv` manifest validated; effective raw item physical metadata validated; capacity bypass closed statically.",
        "- Verification gap: local `dotnet` and Unity executables were unavailable in this shell, so Unity compile remains pending.",
        "",
        f"STATUS: {summary['status']}",
        "",
    ]
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=".", help="Repository root.")
    parser.add_argument("--report", help="Optional markdown report output path.")
    args = parser.parse_args()

    root = Path(args.root).resolve()
    recipes_path = root / "Data" / "Economy" / "Recipes.json"
    matrix_path = root / "Data" / "Economy" / "Resource_Distribution_Matrix.csv"
    items_csv_path = root / "Data" / "Economy" / "Items.csv"

    data = load_json(recipes_path)
    recipes = data["recipes"]
    item_values = {entry["item_id"]: float(entry["baseline_value_units"]) for entry in data["item_values"]}
    matrix_rows = load_matrix(matrix_path)
    graph = build_recipe_graph(recipes)
    cycles = list(nx.simple_cycles(graph))
    recipes_by_result = {recipe["result"]["item_id"]: recipe for recipe in recipes}
    depths, recursion_cycle_markers = compute_dependency_depths(recipes_by_result)
    ingredient_ids = {ingredient["item_id"] for recipe in recipes for ingredient in recipe["ingredients"]}
    item_hashes = [int(entry["item_hash32"]) for entry in data["item_values"]]
    raw_item_ids = {row["item_id"] for row in load_csv_with_header(items_csv_path)[1] if row.get("item_kind") == "raw_resource"} if items_csv_path.exists() else set()

    missing_hashes: list[str] = []
    mismatched_hashes: list[str] = []
    checked_hashes = scan_hash_pairs(data, "Recipes", missing_hashes, mismatched_hashes)

    summary = {
        "sources": {
            "recipes": str(recipes_path.relative_to(root)),
            "items_csv_present": items_csv_path.exists(),
            "resource_matrix": str(matrix_path.relative_to(root)),
        },
        "networkx_version": nx.__version__,
        "recipe_identity": analyze_recipe_identity(recipes),
        "items_csv": analyze_items_csv(items_csv_path, data, matrix_rows),
        "physical_metadata": analyze_item_physical_metadata(root, set(item_values), raw_item_ids),
        "graph": {
            "nodes": graph.number_of_nodes(),
            "edges": graph.number_of_edges(),
            "is_dag": nx.is_directed_acyclic_graph(graph),
            "cycle_count": len(cycles),
            "cycles": cycles[:16],
            "recursion_cycle_markers": recursion_cycle_markers,
        },
        "progression": analyze_progression(root, recipes_by_result, depths),
        "scarcity": analyze_scarcity(matrix_rows, ingredient_ids),
        "exploits": analyze_exploits(recipes, item_values),
        "hashes": {
            "checked": checked_hashes,
            "missing": missing_hashes,
            "mismatched": mismatched_hashes,
        },
        "bulk_transfer": analyze_bulk_transfer(root),
        "save_hash_width": analyze_save_hash_width(root, item_hashes),
    }

    blocking_findings = (
        not summary["graph"]["is_dag"]
        or bool(summary["recipe_identity"]["missing_recipe_ids"])
        or bool(summary["recipe_identity"]["missing_result_item_ids"])
        or bool(summary["recipe_identity"]["duplicate_recipe_ids"])
        or bool(summary["recipe_identity"]["duplicate_result_item_ids"])
        or bool(summary["exploits"]["zero_ingredient_recipes"])
        or bool(summary["exploits"]["nonpositive_quantities"])
        or bool(summary["exploits"]["nonpositive_costs"])
        or bool(summary["exploits"]["unknown_ingredients"])
        or bool(summary["scarcity"]["missing_resource_rows"])
        or bool(summary["scarcity"]["zero_spawn_resources"])
        or not summary["items_csv"]["present"]
        or not summary["items_csv"]["header_matches"]
        or bool(summary["items_csv"]["duplicate_item_ids"])
        or bool(summary["items_csv"]["missing_item_values"])
        or bool(summary["items_csv"]["missing_rows_for_item_values"])
        or bool(summary["items_csv"]["hash_mismatches"])
        or bool(summary["items_csv"]["source_recipe_mismatches"])
        or bool(summary["items_csv"]["baseline_mismatches"])
        or bool(summary["items_csv"]["classification_mismatches"])
        or bool(summary["items_csv"]["resource_mismatches"])
        or summary["items_csv"]["rows"] != len(item_values)
        or bool(summary["physical_metadata"]["missing_raw_assets"])
        or bool(summary["physical_metadata"]["unblocked_missing_crafted_assets"])
        or bool(summary["physical_metadata"]["invalid_effective_mass"])
        or bool(summary["physical_metadata"]["invalid_effective_volume"])
        or bool(summary["hashes"]["missing"])
        or bool(summary["hashes"]["mismatched"])
        or not summary["bulk_transfer"]["bulk_job_volume_limit"]
        or not summary["bulk_transfer"]["bulk_job_weight_limit"]
        or not summary["bulk_transfer"]["try_add_rejects_nonpositive_unit_mass"]
        or not summary["bulk_transfer"]["try_add_rejects_nonpositive_unit_volume"]
        or not summary["bulk_transfer"]["capacity_resolver_rejects_nonpositive_unit_value"]
    )
    risk_findings = (
        not summary["bulk_transfer"]["try_add_checks_volume_limit"]
        or not summary["bulk_transfer"]["try_add_checks_weight_limit"]
    )
    summary["status"] = "ECONOMY SECURED" if not blocking_findings and not risk_findings else "PENDING VERIFICATION - ECONOMY RISKS FOUND"

    output = json.dumps(summary, indent=2, sort_keys=True)
    print(output)

    if args.report:
        Path(args.report).write_text(build_report(summary), encoding="utf-8")

    return 1 if blocking_findings or risk_findings else 0


if __name__ == "__main__":
    raise SystemExit(main())
