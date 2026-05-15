#!/usr/bin/env python3
"""Regression tests for Tools/LoreChecker.py."""

from __future__ import annotations

import contextlib
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import LoreChecker


class LoreCheckerTests(unittest.TestCase):
    @contextlib.contextmanager
    def make_temp_dir(self):
        with tempfile.TemporaryDirectory(prefix="h8_lore_checker_") as temp_dir:
            yield Path(temp_dir)

    def write_localization(self, root: Path, entries: list[dict[str, str]]) -> Path:
        path = root / "Data" / "Localization" / "en_US.json"
        path.parent.mkdir(parents=True)
        path.write_text(json.dumps({"locale": "en_US", "entries": entries}, indent=2), encoding="utf-8")
        return path

    def write_items_csv(self, root: Path) -> Path:
        path = root / "Data" / "Economy" / "Items.csv"
        path.parent.mkdir(parents=True)
        path.write_text(
            "item_id,item_hash32,display_name,item_kind,source_recipe_id,source_recipe_hash32,category_id,category_hash32,progression_tier,baseline_value_units,is_raw_resource,resource_tier,biome_count,positive_spawn_biome_count\n"
            "Data_EmergencyO2Canister,0x00000001,Emergency O2 Canister,processed,,,,,1,10,false,1,1,1\n",
            encoding="utf-8",
        )
        return path

    def test_oxygen_tank_alias_and_csv_item_pass(self) -> None:
        with self.make_temp_dir() as root:
            localization = self.write_localization(
                root,
                [
                    {
                        "LocID": "TEST_OXYGEN",
                        "Hash": "AUTO",
                        "Category": "audit",
                        "Text": "Oxygen Tank resolves to High Capacity Tank. Carry one Emergency O2 Canister.",
                    }
                ],
            )
            items_csv = self.write_items_csv(root)

            report = LoreChecker.build_report(
                localization,
                items_csv,
                root / "Assets/_Project/Data/Items",
                root / "Assets/_Project/Data/Lore/SuitUpgrades",
            )

            self.assertEqual(report["status"], "PASS")
            self.assertEqual(report["unresolved_item_like_mentions"], [])

    def test_unknown_item_like_term_fails_closed(self) -> None:
        with self.make_temp_dir() as root:
            localization = self.write_localization(
                root,
                [
                    {
                        "LocID": "TEST_UNKNOWN",
                        "Hash": "AUTO",
                        "Category": "audit",
                        "Text": "The route marker demands a Phantom Coil that no catalog defines.",
                    }
                ],
            )
            items_csv = self.write_items_csv(root)

            report = LoreChecker.build_report(
                localization,
                items_csv,
                root / "Assets/_Project/Data/Items",
                root / "Assets/_Project/Data/Lore/SuitUpgrades",
            )

            self.assertEqual(report["status"], "FAIL")
            self.assertEqual(report["unresolved_item_like_mentions"][0]["term"], "Phantom Coil")

    def test_duplicate_loc_id_fails_structural_validation(self) -> None:
        with self.make_temp_dir() as root:
            localization = self.write_localization(
                root,
                [
                    {"LocID": "DUPLICATE", "Hash": "AUTO", "Category": "audit", "Text": "Oxygen Tank."},
                    {"LocID": "DUPLICATE", "Hash": "AUTO", "Category": "audit", "Text": "Emergency O2 Canister."},
                ],
            )
            items_csv = self.write_items_csv(root)

            report = LoreChecker.build_report(
                localization,
                items_csv,
                root / "Assets/_Project/Data/Items",
                root / "Assets/_Project/Data/Lore/SuitUpgrades",
            )

            self.assertEqual(report["status"], "FAIL")
            self.assertIn("Duplicate LocID: DUPLICATE", report["structural_problems"])

    def test_extra_text_path_is_scanned_for_dead_ends(self) -> None:
        with self.make_temp_dir() as root:
            localization = self.write_localization(
                root,
                [
                    {
                        "LocID": "KNOWN",
                        "Hash": "AUTO",
                        "Category": "audit",
                        "Text": "Emergency O2 Canister is defined.",
                    }
                ],
            )
            items_csv = self.write_items_csv(root)
            extra_text = root / "Docs" / "Lore" / "Lore_Bible.md"
            extra_text.parent.mkdir(parents=True)
            extra_text.write_text("A Phantom Coil is mentioned in the bible.\n", encoding="utf-8")

            report = LoreChecker.build_report(
                localization,
                items_csv,
                root / "Assets/_Project/Data/Items",
                root / "Assets/_Project/Data/Lore/SuitUpgrades",
                [extra_text],
            )

            self.assertEqual(report["status"], "FAIL")
            self.assertEqual(report["extra_text_paths"], [extra_text.as_posix()])
            self.assertEqual(report["unresolved_item_like_mentions"][0]["loc_id"], f"EXTRA_TEXT::{extra_text.as_posix()}")
            self.assertEqual(report["unresolved_item_like_mentions"][0]["term"], "Phantom Coil")


if __name__ == "__main__":
    unittest.main()
