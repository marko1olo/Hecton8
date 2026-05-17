#!/usr/bin/env python3
"""Tests for the submarine upgrade curve baker."""

from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import UpgradeCurveBaker


class UpgradeCurveBakerTests(unittest.TestCase):
    def test_bake_outputs_required_shape_and_validation(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir)
            summary = UpgradeCurveBaker.run(output_dir, monte_carlo_steps=4096)
            self.assertTrue(summary["verification_passed"], summary["failures"])
            payload = (output_dir / UpgradeCurveBaker.MAIN_JSON_NAME).read_bytes()
            self.assertNotIn(b"\n", payload)
            self.assertNotIn(b": ", payload)
            self.assertNotIn(b", ", payload)
            rows = json.loads(payload.decode("utf-8"))
            self.assertEqual(9, len(rows))
            for row in rows:
                self.assertEqual(["UpgradeHash", "Type", "Value", "PowerCost"], list(row.keys()))
            engine_mk3 = next(row for row in rows if row["UpgradeHash"] == UpgradeCurveBaker.fnv1a32("Upgrade_Engine_Mk3"))
            self.assertEqual(2.5, engine_mk3["Value"])
            binary = (output_dir / UpgradeCurveBaker.BINARY_PACK_NAME).read_bytes()
            self.assertEqual(0, len(binary) % 16)
            self.assertEqual([], UpgradeCurveBaker.validate_binary_pack(binary, rows))
            self.assertTrue((output_dir / UpgradeCurveBaker.PLOT_PNG_NAME).read_bytes().startswith(b"\x89PNG\r\n\x1a\n"))
            economy = json.loads((output_dir / UpgradeCurveBaker.MONTE_CARLO_JSON_NAME).read_text(encoding="utf-8"))
            self.assertEqual(UpgradeCurveBaker.monte_carlo_seed(), economy["seed"])
            self.assertEqual(UpgradeCurveBaker.MONTE_CARLO_SEED_LABEL, economy["seed_derivation"]["label"])

    def test_hash_contract_matches_project_sentinel(self) -> None:
        self.assertEqual(3511699502, UpgradeCurveBaker.fnv1a32("Data_TitaniumScrap"))
        self.assertEqual(579897495, UpgradeCurveBaker.fnv1a32("Upgrade_Depth_Mk1"))


if __name__ == "__main__":
    unittest.main()
