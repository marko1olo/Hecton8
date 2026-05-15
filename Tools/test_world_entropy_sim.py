#!/usr/bin/env python3
"""Regression tests for the HECTON-8 world regrowth entropy harness."""

from __future__ import annotations

import sys
import unittest
from copy import deepcopy
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
REPO_ROOT = TOOLS_ROOT.parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import WorldEntropySim as entropy  # noqa: E402


class WorldEntropySimTests(unittest.TestCase):
    def setUp(self) -> None:
        self.constants = entropy.load_constants(REPO_ROOT / "Data" / "Economy" / "Regrowth_Constants.json")

    def test_total_overharvest_recovers_without_desert_lock(self) -> None:
        final, _ = entropy.run_sim(self.constants, 365, True)
        safe_day = final["firstHalfRecoveryDays"][0]
        abyss_day = final["firstHalfRecoveryDays"][3]
        final_mature_ratio = sum(final["matureByBiome"]) / sum(final["countByBiome"])
        ratio = abyss_day / safe_day

        self.assertEqual(28, safe_day)
        self.assertEqual(95, abyss_day)
        self.assertGreaterEqual(ratio, self.constants["acceptance"]["safeShallowsVsDeepAbyssMinRecoveryRatio"])
        self.assertGreaterEqual(final_mature_ratio, self.constants["acceptance"]["minFinalMatureRatio"])

    def test_simulation_is_deterministic(self) -> None:
        constants = deepcopy(self.constants)
        constants["gridWidth"] = 8
        constants["gridHeight"] = 8
        first_final, first_checkpoints = entropy.run_sim(constants, 120, True)
        second_final, second_checkpoints = entropy.run_sim(constants, 120, True)

        self.assertEqual(first_final, second_final)
        self.assertEqual(first_checkpoints, second_checkpoints)

    def test_acceptance_constants_lock_total_overharvest_mode(self) -> None:
        self.assertEqual(365, self.constants["acceptance"]["simulationDays"])
        self.assertEqual("total_overharvest", self.constants["acceptance"]["mode"])
        self.assertEqual(220, self.constants["nutrientDiffusionPermille"])


if __name__ == "__main__":
    unittest.main()
