#!/usr/bin/env python3
"""Regression tests for the HECTON-8 world regrowth entropy harness."""

from __future__ import annotations

import sys
import unittest
from copy import deepcopy
from io import StringIO
from pathlib import Path
from unittest.mock import patch


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

        self.assertEqual(
            self.constants["biomes"][0]["expectedHalfRecoveryDaysUnderTotalOverharvest"],
            safe_day,
        )
        self.assertEqual(
            self.constants["biomes"][3]["expectedHalfRecoveryDaysUnderTotalOverharvest"],
            abyss_day,
        )
        self.assertGreaterEqual(ratio, self.constants["acceptance"]["safeShallowsVsDeepAbyssMinRecoveryRatio"])
        self.assertGreaterEqual(final_mature_ratio, self.constants["acceptance"]["minFinalMatureRatio"])

    def test_seeded_biome_layout_matches_csharp_resolver_snapshot(self) -> None:
        state = entropy.build_initial_state(self.constants, False)
        counts = [0, 0, 0, 0]
        for biome_id in state["biome_ids"]:
            counts[biome_id] += 1

        self.assertEqual([1729, 996, 564, 807], counts)
        self.assertEqual(0, entropy.resolve_biome(2, -9, 0, 8))

    def test_simulation_is_deterministic(self) -> None:
        constants = deepcopy(self.constants)
        constants["gridWidth"] = 8
        constants["gridHeight"] = 8
        first_final, first_checkpoints = entropy.run_sim(constants, 120, True)
        second_final, second_checkpoints = entropy.run_sim(constants, 120, True)

        self.assertEqual(first_final, second_final)
        self.assertEqual(first_checkpoints, second_checkpoints)

    def test_acceptance_constants_lock_total_overharvest_mode(self) -> None:
        self.assertEqual("ENTROPY BALANCED", self.constants["status"])
        self.assertEqual("PENDING_UNITY_VERIFICATION", self.constants["unityVerificationStatus"])
        self.assertEqual(365, self.constants["acceptance"]["simulationDays"])
        self.assertEqual("total_overharvest", self.constants["acceptance"]["mode"])
        self.assertEqual(220, self.constants["nutrientDiffusionPermille"])
        expected_days = [
            biome["expectedHalfRecoveryDaysUnderTotalOverharvest"]
            for biome in self.constants["biomes"]
        ]
        final, _ = entropy.run_sim(self.constants, 365, True)
        self.assertEqual(expected_days, final["firstHalfRecoveryDays"])

    def test_exported_constants_match_csharp_fast_path_bounds(self) -> None:
        self.assertGreater(self.constants["gridWidth"], 0)
        self.assertGreater(self.constants["gridHeight"], 0)
        self.assertGreater(self.constants["macroSectorMeters"], 0)
        self.assertGreater(self.constants["baseGrowthProgressPerDayQ"], 0)
        self.assertLessEqual(self.constants["baseGrowthProgressPerDayQ"], 255)
        for key in (
            "nutrientDiffusionPermille",
            "preyGrowthPermille",
            "predationPermille",
            "predatorConversionPermille",
            "predatorMortalityPermille",
        ):
            self.assertGreaterEqual(self.constants[key], 0)
            self.assertLessEqual(self.constants[key], 1000)

        self.assertGreater(self.constants["seedToMatureProgressQ"], 0)
        self.assertGreater(self.constants["tombstoneBaseDecayDays"], 0)
        self.assertGreater(self.constants["minApexRespawnDays"], 0)
        self.assertGreaterEqual(self.constants["maxApexRespawnDays"], self.constants["minApexRespawnDays"])
        for biome in self.constants["biomes"]:
            self.assertGreater(biome["temperatureQ"], 0)
            self.assertGreaterEqual(biome["nutrientStartQ"], self.constants["minimumNutrientsQ"])

    def test_cli_rejects_non_positive_day_count(self) -> None:
        with patch.object(sys, "argv", ["WorldEntropySim.py", "--days", "0"]), patch("sys.stderr", StringIO()):
            with self.assertRaises(SystemExit) as context:
                entropy.main()

        self.assertEqual(2, context.exception.code)

    def test_run_sim_rejects_non_positive_day_count(self) -> None:
        with self.assertRaises(ValueError):
            entropy.run_sim(self.constants, 0, True)

    def test_absent_biome_cannot_pass_total_overharvest_acceptance(self) -> None:
        constants = deepcopy(self.constants)
        constants["gridWidth"] = 1
        constants["gridHeight"] = 1
        constants["macroSectorOriginX"] = 0
        constants["macroSectorOriginZ"] = 0

        final, _ = entropy.run_sim(constants, 365, True)
        balanced, ratio, final_mature_ratio = entropy.calculate_balance(final, constants, True)

        self.assertEqual(1.0, final_mature_ratio)
        self.assertIsNone(final["firstHalfRecoveryDays"][3])
        self.assertEqual(0.0, ratio)
        self.assertFalse(balanced)


if __name__ == "__main__":
    unittest.main()
