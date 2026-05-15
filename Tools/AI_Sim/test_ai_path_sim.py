#!/usr/bin/env python3
"""Regression tests for the flow-aware potential-field simulator."""

from __future__ import annotations

import importlib.util
import json
import sys
import unittest
from pathlib import Path


sys.dont_write_bytecode = True

ROOT = Path(__file__).resolve().parents[2]
SIM_PATH = ROOT / "Tools" / "AiPathSim.py"
TUNING_PATH = ROOT / "Data" / "AI" / "Navigation_Tuning.json"


def load_sim_module():
    spec = importlib.util.spec_from_file_location("ai_path_sim", SIM_PATH)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load simulator module from {SIM_PATH}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


class AiPathSimTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.sim = load_sim_module()
        cls.tuning = json.loads(TUNING_PATH.read_text(encoding="utf-8"))
        cls.weights = cls.sim.weights_from_dict(cls.tuning["selectedWeights"])

    def test_export_status_and_source_parameters(self) -> None:
        self.assertEqual(self.tuning["status"], "NAVIGATION OPTIMIZED")
        snapshot = self.tuning["sourceParameterSnapshot"]
        self.assertEqual(snapshot["flowTextureResolution"], 32)
        self.assertEqual(snapshot["vectorNoiseResolution"], 32)
        self.assertEqual(snapshot["heatSourceCapacity"], 8)
        self.assertAlmostEqual(snapshot["flowTextureCellSizeMeters"], 3.125)
        self.assertIn("Assets/_Project/Scripts/HectonFluidEngine.cs", snapshot["sourceFiles"])

    def test_export_self_check_passes(self) -> None:
        valid, errors = self.sim.validate_export(self.tuning)
        self.assertTrue(valid, errors)

    def test_selected_weights_reach_without_sdf_pushout(self) -> None:
        result = self.sim.simulate(self.weights, use_smoothing=True)
        self.assertTrue(result.reached)
        self.assertEqual(result.sdf_pushout_events, 0)
        self.assertGreaterEqual(result.min_obstacle_distance, 2.0)
        self.assertLessEqual(result.jitter_events, 1)

    def test_smoothing_improves_or_matches_jitter(self) -> None:
        raw = self.sim.simulate(self.weights, use_smoothing=False)
        smoothed = self.sim.simulate(self.weights, use_smoothing=True)
        self.assertLessEqual(smoothed.jitter_events, raw.jitter_events)
        self.assertEqual(smoothed.sdf_pushout_events, 0)

    def test_idle_drift_follows_current(self) -> None:
        drift = self.sim.idle_drift(self.weights)
        self.assertGreater(drift["drift_distance_meters"], 10.0)
        self.assertGreater(drift["mean_velocity_flow_alignment01"], 0.95)


if __name__ == "__main__":
    unittest.main()
