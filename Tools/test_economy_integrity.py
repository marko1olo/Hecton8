#!/usr/bin/env python3
"""Regression tests for the HECTON-8 economy integrity audit."""

from __future__ import annotations

import csv
import contextlib
import io
import json
import re
import shutil
import sys
import tempfile
import unittest
from collections import Counter
from pathlib import Path
from unittest import mock

import networkx as nx


TOOLS_ROOT = Path(__file__).resolve().parent
REPO_ROOT = TOOLS_ROOT.parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import EconomyItemsCsvBake as items_bake  # noqa: E402
import EconomyRecipeGraphAudit as graph_audit  # noqa: E402
import EconomyValidator as validator  # noqa: E402


class EconomyIntegrityTests(unittest.TestCase):
    def test_items_csv_bake_is_deterministic(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_items_bake_test_") as temp_dir:
            temp_root = Path(temp_dir)
            source_dir = REPO_ROOT / "Data" / "Economy"
            target_dir = temp_root / "Data" / "Economy"
            shutil.copytree(source_dir, target_dir)

            generated_path = target_dir / "Items.csv"
            generated_path.unlink()
            with contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(0, items_bake.bake(temp_root))

            self.assertEqual((source_dir / "Items.csv").read_bytes(), generated_path.read_bytes())

    def test_items_csv_validator_contract(self) -> None:
        economy_dir = REPO_ROOT / "Data" / "Economy"
        result = validator.validate_items_csv(
            economy_dir / "Items.csv",
            economy_dir / "Recipes.json",
            economy_dir / "Resource_Distribution_Matrix.csv",
        )

        self.assertEqual({"rows": 55, "hashes": 150, "raw": 15, "crafted": 40}, result)

    def test_graph_audit_validates_items_csv_manifest(self) -> None:
        economy_dir = REPO_ROOT / "Data" / "Economy"
        recipes_data = graph_audit.load_json(economy_dir / "Recipes.json")
        matrix_rows = graph_audit.load_matrix(economy_dir / "Resource_Distribution_Matrix.csv")

        result = graph_audit.analyze_items_csv(economy_dir / "Items.csv", recipes_data, matrix_rows)

        self.assertTrue(result["present"])
        self.assertTrue(result["header_matches"])
        self.assertEqual(55, result["rows"])
        self.assertEqual(15, result["raw_count"])
        self.assertEqual(40, result["crafted_count"])
        self.assertEqual(0, result["external_count"])
        self.assertEqual(150, result["hash_checks"])
        self.assertEqual([], result["duplicate_item_ids"])
        self.assertEqual([], result["missing_item_values"])
        self.assertEqual([], result["missing_rows_for_item_values"])
        self.assertEqual([], result["hash_mismatches"])
        self.assertEqual([], result["source_recipe_mismatches"])
        self.assertEqual([], result["baseline_mismatches"])
        self.assertEqual([], result["classification_mismatches"])
        self.assertEqual([], result["resource_mismatches"])

    def test_graph_audit_detects_items_csv_hash_corruption(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_items_corruption_test_") as temp_dir:
            temp_root = Path(temp_dir)
            source_dir = REPO_ROOT / "Data" / "Economy"
            target_dir = temp_root / "Data" / "Economy"
            shutil.copytree(source_dir, target_dir)

            items_path = target_dir / "Items.csv"
            with items_path.open("r", encoding="utf-8", newline="") as handle:
                reader = csv.DictReader(handle)
                rows = list(reader)
                fieldnames = reader.fieldnames
            self.assertIsNotNone(fieldnames)
            rows[0]["item_hash32"] = "0"
            with items_path.open("w", encoding="utf-8", newline="") as handle:
                writer = csv.DictWriter(handle, fieldnames=fieldnames, lineterminator="\n")
                writer.writeheader()
                writer.writerows(rows)

            recipes_data = graph_audit.load_json(target_dir / "Recipes.json")
            matrix_rows = graph_audit.load_matrix(target_dir / "Resource_Distribution_Matrix.csv")
            result = graph_audit.analyze_items_csv(items_path, recipes_data, matrix_rows)

            self.assertIn("row 2 item_hash32", result["hash_mismatches"])

    def test_effective_item_physical_metadata_blocks_zero_capacity_exploits(self) -> None:
        economy_dir = REPO_ROOT / "Data" / "Economy"
        recipes_data = graph_audit.load_json(economy_dir / "Recipes.json")
        with (economy_dir / "Items.csv").open("r", encoding="utf-8", newline="") as handle:
            item_rows = list(csv.DictReader(handle))
        item_ids = {entry["item_id"] for entry in recipes_data["item_values"]}
        raw_item_ids = {row["item_id"] for row in item_rows if row["item_kind"] == "raw_resource"}

        result = graph_audit.analyze_item_physical_metadata(REPO_ROOT, item_ids, raw_item_ids)

        self.assertEqual([], result["missing_raw_assets"])
        self.assertTrue(result["missing_crafted_assets_runtime_blocked"])
        self.assertEqual([], result["unblocked_missing_crafted_assets"])
        self.assertEqual(22, result["runtime_binding_plan_blocked_count"])
        self.assertEqual([], result["invalid_effective_mass"])
        self.assertEqual([], result["invalid_effective_volume"])
        self.assertIn("Data_AbyssalCrystal", result["serialized_zero_auto_resolved"])

    def test_missing_crafted_asset_runtime_allowed_binding_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_binding_guard_test_") as temp_dir:
            temp_root = Path(temp_dir)
            target_dir = temp_root / "Data" / "Economy"
            target_dir.mkdir(parents=True)
            source_path = REPO_ROOT / "Data" / "Economy" / "Runtime_Binding_Plan.json"
            plan = graph_audit.load_json(source_path)
            missing_ids = {binding["economy_id"] for binding in plan["bindings"]}

            first_binding = plan["bindings"][0]
            unblocked_id = first_binding["economy_id"]
            first_binding["runtime_use_allowed"] = True
            with (target_dir / "Runtime_Binding_Plan.json").open("w", encoding="utf-8") as handle:
                json.dump(plan, handle, indent=2)

            result = graph_audit.analyze_runtime_binding_guard(temp_root, missing_ids)

            self.assertFalse(result["all_missing_crafted_assets_blocked"])
            self.assertEqual([unblocked_id], result["unblocked_missing_crafted_assets"])
            self.assertEqual(len(missing_ids) - 1, result["blocked_count"])

    def test_graph_audit_cli_fails_when_missing_crafted_binding_is_runtime_allowed(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_graph_audit_cli_test_") as temp_dir:
            temp_root = Path(temp_dir)
            copy_graph_audit_fixture(temp_root)

            plan_path = temp_root / "Data" / "Economy" / "Runtime_Binding_Plan.json"
            plan = graph_audit.load_json(plan_path)
            unblocked_id = plan["bindings"][0]["economy_id"]
            plan["bindings"][0]["runtime_use_allowed"] = True
            with plan_path.open("w", encoding="utf-8") as handle:
                json.dump(plan, handle, indent=2)

            with contextlib.redirect_stdout(io.StringIO()) as stdout:
                with mock.patch.object(sys, "argv", ["EconomyRecipeGraphAudit.py", "--root", str(temp_root)]):
                    exit_code = graph_audit.main()

            summary = json.loads(stdout.getvalue())
            self.assertEqual(1, exit_code)
            self.assertEqual("PENDING VERIFICATION - ECONOMY RISKS FOUND", summary["status"])
            self.assertEqual([unblocked_id], summary["physical_metadata"]["unblocked_missing_crafted_assets"])
            self.assertFalse(summary["physical_metadata"]["missing_crafted_assets_runtime_blocked"])

    def test_graph_audit_cli_fails_when_runtime_binding_plan_is_missing(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_graph_audit_missing_plan_test_") as temp_dir:
            temp_root = Path(temp_dir)
            copy_graph_audit_fixture(temp_root)
            (temp_root / "Data" / "Economy" / "Runtime_Binding_Plan.json").unlink()

            with contextlib.redirect_stdout(io.StringIO()) as stdout:
                with mock.patch.object(sys, "argv", ["EconomyRecipeGraphAudit.py", "--root", str(temp_root)]):
                    exit_code = graph_audit.main()

            summary = json.loads(stdout.getvalue())
            self.assertEqual(1, exit_code)
            self.assertEqual("PENDING VERIFICATION - ECONOMY RISKS FOUND", summary["status"])
            self.assertEqual(0, summary["physical_metadata"]["runtime_binding_plan_blocked_count"])
            self.assertEqual(22, len(summary["physical_metadata"]["unblocked_missing_crafted_assets"]))
            self.assertFalse(summary["physical_metadata"]["missing_crafted_assets_runtime_blocked"])

    def test_graph_audit_cli_fails_on_duplicate_recipe_identity(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_graph_audit_duplicate_result_test_") as temp_dir:
            temp_root = Path(temp_dir)
            copy_graph_audit_fixture(temp_root)

            recipes_path = temp_root / "Data" / "Economy" / "Recipes.json"
            data = graph_audit.load_json(recipes_path)
            duplicate_recipe_id = data["recipes"][0]["recipe_id"]
            duplicate_result_id = data["recipes"][0]["result"]["item_id"]
            data["recipes"][2]["recipe_id"] = duplicate_recipe_id
            data["recipes"][2]["recipe_hash32"] = graph_audit.fnv1a32(duplicate_recipe_id)
            data["recipes"][1]["result"]["item_id"] = duplicate_result_id
            data["recipes"][1]["result"]["item_hash32"] = graph_audit.fnv1a32(duplicate_result_id)
            with recipes_path.open("w", encoding="utf-8") as handle:
                json.dump(data, handle, indent=2)

            with contextlib.redirect_stdout(io.StringIO()) as stdout:
                with mock.patch.object(sys, "argv", ["EconomyRecipeGraphAudit.py", "--root", str(temp_root)]):
                    exit_code = graph_audit.main()

            summary = json.loads(stdout.getvalue())
            self.assertEqual(1, exit_code)
            self.assertEqual("PENDING VERIFICATION - ECONOMY RISKS FOUND", summary["status"])
            self.assertEqual([duplicate_recipe_id], summary["recipe_identity"]["duplicate_recipe_ids"])
            self.assertEqual([duplicate_result_id], summary["recipe_identity"]["duplicate_result_item_ids"])

    def test_negative_economy_cases_are_rejected(self) -> None:
        economy_dir = REPO_ROOT / "Data" / "Economy"
        results = validator.run_negative_tests(economy_dir)

        self.assertEqual(
            [
                "first_sub_result_item_mismatch",
                "first_sub_duplicate_raw_resource",
                "first_sub_batch_band_overflow",
                "matrix_recipe_value_drift",
                "items_missing_source_recipe",
                "binding_plan_runtime_allowed",
            ],
            results,
        )

    def test_recipe_graph_is_acyclic_and_progression_band_is_locked(self) -> None:
        recipes_data = graph_audit.load_json(REPO_ROOT / "Data" / "Economy" / "Recipes.json")
        identity = graph_audit.analyze_recipe_identity(recipes_data["recipes"])
        graph = graph_audit.build_recipe_graph(recipes_data["recipes"])
        recipes_by_result = {recipe["result"]["item_id"]: recipe for recipe in recipes_data["recipes"]}
        depths, recursion_markers = graph_audit.compute_dependency_depths(recipes_by_result)
        progression = graph_audit.analyze_progression(REPO_ROOT, recipes_by_result, depths)

        self.assertEqual(40, identity["recipe_count"])
        self.assertEqual([], identity["missing_recipe_ids"])
        self.assertEqual([], identity["missing_result_item_ids"])
        self.assertEqual([], identity["duplicate_recipe_ids"])
        self.assertEqual([], identity["duplicate_result_item_ids"])
        self.assertTrue(nx.is_directed_acyclic_graph(graph))
        self.assertEqual([], list(nx.simple_cycles(graph)))
        self.assertEqual([], recursion_markers)
        self.assertEqual(55, graph.number_of_nodes())
        self.assertEqual(122, graph.number_of_edges())
        self.assertEqual(17, progression["unique_recipe_steps"])
        self.assertEqual(46, progression["total_recipe_batches"])
        self.assertGreaterEqual(progression["total_recipe_batches"], 5)
        self.assertLessEqual(progression["total_recipe_batches"], 50)

    def test_resource_matrix_covers_all_raw_recipe_inputs(self) -> None:
        economy_dir = REPO_ROOT / "Data" / "Economy"
        recipes_data = graph_audit.load_json(economy_dir / "Recipes.json")
        with (economy_dir / "Resource_Distribution_Matrix.csv").open("r", encoding="utf-8", newline="") as handle:
            matrix_rows = list(csv.DictReader(handle))

        ingredient_ids = {
            ingredient["item_id"]
            for recipe in recipes_data["recipes"]
            for ingredient in recipe["ingredients"]
        }
        scarcity = graph_audit.analyze_scarcity(matrix_rows, ingredient_ids)
        rows_by_resource = Counter(row["resource_id"] for row in matrix_rows)

        self.assertEqual([], scarcity["missing_resource_rows"])
        self.assertEqual([], scarcity["zero_spawn_resources"])
        self.assertEqual(10, scarcity["resource_matrix_biomes"])
        self.assertEqual(15, scarcity["resource_matrix_resources"])
        self.assertTrue(all(row_count == 10 for row_count in rows_by_resource.values()))

    def test_player_inventory_capacity_gate_precedes_mutation(self) -> None:
        source_path = REPO_ROOT / "Assets" / "_Project" / "Scripts" / "PlayerInventory.cs"
        source = source_path.read_text(encoding="utf-8")
        method = extract_csharp_method(source, "private bool TryAddItemWithStateInternal")

        capacity_gate = method.find("TryResolveCapacityLimitedQuantity")
        stack_mutation = method.find("TryStackQuantityWithState")
        slot_mutation = method.find("_grid.TryAddItem")

        self.assertGreaterEqual(capacity_gate, 0)
        self.assertGreater(stack_mutation, capacity_gate)
        self.assertGreater(slot_mutation, capacity_gate)
        self.assertIn("InventoryEvents.NotifyInventoryFull(itemHashId);", method)
        self.assertRegex(source, r"private bool CanAcceptAdditionalPhysicalCapacity\s*\(")
        self.assertRegex(source, r"private bool TryResolveAdditionalPhysicalDemand\s*\(")
        self.assertRegex(source, r"private bool TryResolveCurrentPhysicalTotals\s*\(")
        self.assertRegex(source, r"unitMassKg\s*>\s*0f")
        self.assertRegex(source, r"unitVolumeLiters\s*>\s*0f")
        self.assertRegex(source, r"private static int ResolveCapacityLimitedQuantity\s*\(")
        capacity_resolver = extract_csharp_method(source, "private static int ResolveCapacityLimitedQuantity")
        self.assertRegex(capacity_resolver, r"unitValue\s*<=\s*0f\)\s*\r?\n\s*return\s+0;")

        bulk = graph_audit.analyze_bulk_transfer(REPO_ROOT)
        self.assertTrue(bulk["try_add_rejects_nonpositive_unit_mass"])
        self.assertTrue(bulk["try_add_rejects_nonpositive_unit_volume"])
        self.assertTrue(bulk["capacity_resolver_rejects_nonpositive_unit_value"])


def extract_csharp_method(source: str, signature: str) -> str:
    match = re.search(re.escape(signature), source)
    if match is None:
        raise AssertionError(f"missing method signature: {signature}")

    brace_index = source.find("{", match.start())
    if brace_index < 0:
        raise AssertionError(f"missing method body: {signature}")

    depth = 0
    for index in range(brace_index, len(source)):
        char = source[index]
        if char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return source[match.start():index + 1]
    raise AssertionError(f"unterminated method body: {signature}")


def copy_graph_audit_fixture(temp_root: Path) -> None:
    shutil.copytree(REPO_ROOT / "Data" / "Economy", temp_root / "Data" / "Economy")
    shutil.copytree(REPO_ROOT / "Assets" / "_Project" / "Data" / "Items", temp_root / "Assets" / "_Project" / "Data" / "Items")

    scripts_root = temp_root / "Assets" / "_Project" / "Scripts"
    scripts_root.mkdir(parents=True)
    shutil.copy2(REPO_ROOT / "Assets" / "_Project" / "Scripts" / "PlayerInventory.cs", scripts_root / "PlayerInventory.cs")
    shutil.copy2(REPO_ROOT / "Assets" / "_Project" / "Scripts" / "SaveData.cs", scripts_root / "SaveData.cs")

    inventory_root = scripts_root / "Inventory"
    inventory_root.mkdir()
    shutil.copy2(
        REPO_ROOT / "Assets" / "_Project" / "Scripts" / "Inventory" / "InventorySoAUtility.cs",
        inventory_root / "InventorySoAUtility.cs",
    )


if __name__ == "__main__":
    unittest.main()
