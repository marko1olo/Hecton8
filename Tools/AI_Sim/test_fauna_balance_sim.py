#!/usr/bin/env python3
"""Regression tests for the offline fauna balance simulator."""

from __future__ import annotations

import importlib.util
import json
import sys
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SIM_PATH = ROOT / "Tools" / "AI_Sim" / "FaunaBalanceSim.py"
CONSTANTS_PATH = ROOT / "Data" / "AI" / "Fauna_Global_Weights.json"
REPORT_PATH = ROOT / "Tools" / "AI_Sim" / "FaunaBalanceSim_Report.json"
REPLICATE_PATH = ROOT / "Tools" / "AI_Sim" / "FaunaBalanceSim_ReplicateValidation.json"


def load_sim_module():
    spec = importlib.util.spec_from_file_location("fauna_balance_sim", SIM_PATH)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load simulator module from {SIM_PATH}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


class FaunaBalanceSimTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.sim = load_sim_module()
        cls.constants = json.loads(CONSTANTS_PATH.read_text(encoding="utf-8"))
        cls.report = json.loads(REPORT_PATH.read_text(encoding="utf-8"))
        cls.replicate = json.loads(REPLICATE_PATH.read_text(encoding="utf-8"))

    def test_constants_match_detailed_report(self) -> None:
        self.assertEqual(self.constants["status"], "AI BALANCED")
        self.assertEqual(self.constants["selectedConstants"], self.report["selectedConstants"])
        self.assertEqual(
            self.constants["millionFrameSummary"]["frames"],
            self.report["millionFrameRun"]["frames"],
        )
        self.assertEqual(
            self.constants["millionFrameSummary"]["population"],
            self.report["millionFrameRun"]["population"],
        )
        self.assertNotIn("heatmapTop10", self.constants)
        self.assertNotIn("millionFrameRun", self.constants)

    def test_replicate_summary_matches_report(self) -> None:
        replicate_summary = self.constants["replicateValidation"]
        self.assertEqual(replicate_summary["status"], self.replicate["status"])
        self.assertEqual(replicate_summary["failureCount"], self.replicate["failureCount"])
        self.assertEqual(replicate_summary["summary"], self.replicate["summary"])
        self.assertEqual(self.replicate["status"], "REPLICATE_STABLE")
        self.assertEqual(self.replicate["failureCount"], 0)

    def test_selected_weights_short_run_stays_bounded(self) -> None:
        weights = self.sim.load_selected_weights(CONSTANTS_PATH)
        profile = self.sim.SensoryProfile(0.06, False, True)
        result = self.sim.run_simulation(
            5_000,
            weights,
            profile,
            self.sim.stable_seed(self.sim.DEFAULT_SEED, 0x54455354),
            sample_stride=1_000,
        )
        self.assertFalse(result.extinct)
        self.assertFalse(result.overpopulated)
        self.assertGreater(result.final_prey, 9_000.0)
        self.assertGreater(result.final_stalker, 30.0)
        self.assertGreater(result.final_alpha, 1.5)

    def test_short_replicate_validation_is_stable(self) -> None:
        weights = self.sim.load_selected_weights(CONSTANTS_PATH)
        validation = self.sim.validate_replicates(
            weights,
            frames=2_000,
            replicates=2,
            seed=self.sim.stable_seed(self.sim.DEFAULT_SEED, 0x554E4954),
        )
        self.assertEqual(validation["status"], "REPLICATE_STABLE")
        self.assertEqual(validation["failureCount"], 0)
        self.assertEqual(validation["replicates"], 2)

    def test_artifact_checker_passes_current_outputs(self) -> None:
        check = self.sim.check_artifacts(CONSTANTS_PATH, REPORT_PATH, REPLICATE_PATH)
        self.assertEqual(check["status"], "ARTIFACT_CHECK_PASSED")
        self.assertEqual(check["errors"], [])
        self.assertEqual(check["millionFrames"], 1_000_000)
        self.assertEqual(check["replicates"], 5)


if __name__ == "__main__":
    unittest.main()
