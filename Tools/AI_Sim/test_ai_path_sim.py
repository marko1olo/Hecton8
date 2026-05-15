#!/usr/bin/env python3
"""Regression tests for the flow-aware potential-field simulator."""

from __future__ import annotations

import copy
import contextlib
import io
import importlib.util
import json
import subprocess
import sys
import tempfile
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
        cls.best_weights, cls.best_smoothed, cls.best_raw, cls.best_evaluated, cls.best_reached = cls.sim.find_best_candidate()

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

    def test_malformed_numeric_fields_fail_closed(self) -> None:
        bad_tuning = copy.deepcopy(self.tuning)
        bad_tuning["tierProfiles"]["Low"]["hysteresis"]["distanceMeters"] = "invalid"
        valid, errors = self.sim.validate_export(bad_tuning)
        self.assertFalse(valid)
        self.assertTrue(any("hysteresis distance" in error for error in errors))

        bad_snapshot = copy.deepcopy(self.tuning["sourceParameterSnapshot"])
        bad_snapshot["flowTextureWorldSizeMeters"] = "invalid"
        valid, errors = self.sim.validate_source_constants(bad_snapshot, self.sim.SOURCE_ROOT)
        self.assertFalse(valid)
        self.assertTrue(any("non-finite exported source parameter" in error for error in errors))

    def test_invalid_json_and_non_object_roots_fail_closed(self) -> None:
        valid, errors = self.sim.validate_export([])
        self.assertFalse(valid)
        self.assertIn("export root must be a JSON object", errors)

        with tempfile.NamedTemporaryFile("w", suffix=".json", delete=False, encoding="utf-8") as handle:
            temp_path = Path(handle.name)
            handle.write("{invalid-json")
        try:
            output = io.StringIO()
            with contextlib.redirect_stdout(output):
                self.assertEqual(self.sim.check_export(temp_path), 1)
            self.assertIn("CHECK FAILED", output.getvalue())
            self.assertIn("invalid JSON export", output.getvalue())
        finally:
            temp_path.unlink(missing_ok=True)

    def test_sample_cost_model_is_deterministic(self) -> None:
        self.assertEqual(self.sim.deterministic_sample_cost_model(), self.tuning["sampleCostModel"])
        self.assertNotIn("pythonMicroBenchmark", self.tuning)
        self.assertFalse(any(key.endswith("ElapsedMs") for key in self.tuning["sampleCostModel"]))

    def test_export_regeneration_is_deterministic(self) -> None:
        before = TUNING_PATH.read_bytes()
        subprocess.run([sys.executable, str(SIM_PATH)], cwd=ROOT, check=True, stdout=subprocess.DEVNULL)
        after = TUNING_PATH.read_bytes()
        self.assertEqual(before, after)

    def test_blackbox_telemetry_contract_is_exported(self) -> None:
        telemetry = self.tuning["blackBoxTelemetry"]
        self.assertEqual(telemetry["capacityFrames"], 300)
        self.assertEqual(telemetry["dumpPath"], "Docs/AgentLogs/Dump_AI_POTENTIAL_FIELD_NAVIGATOR.bin")
        field_names = {field["name"] for field in telemetry["fields"]}
        self.assertIn("positionAupCell", field_names)
        self.assertIn("sdfClearanceMeters", field_names)
        self.assertIn("stateHash", field_names)
        self.assertIn("sdfClearanceMeters", telemetry["finiteGuards"])

    def test_tier_profiles_have_hysteresis(self) -> None:
        for tier_name in ("Low", "Middle", "High", "Ultra"):
            tier = self.tuning["tierProfiles"][tier_name]
            hysteresis = tier["hysteresis"]
            self.assertGreaterEqual(hysteresis["distanceMeters"], 3.0)
            self.assertLessEqual(hysteresis["distanceMeters"], 5.0)
            self.assertGreaterEqual(hysteresis["timeSeconds"], 2.0)
            self.assertLessEqual(hysteresis["timeSeconds"], 3.0)
            self.assertIn("flip-flop", hysteresis["rule"])

    def test_source_constants_match_export(self) -> None:
        valid, errors = self.sim.validate_source_constants(self.tuning["sourceParameterSnapshot"], self.sim.SOURCE_ROOT)
        self.assertTrue(valid, errors)

    def test_duplicate_source_constants_are_all_checked(self) -> None:
        source_path = self.sim.SOURCE_ROOT / self.sim.FLUID_ENGINE_PATH
        source_text = source_path.read_text(encoding="utf-8")
        surface_values = self.sim.extract_source_constants(source_text, "SurfaceStormLayerDepthMeters")
        turbulence_values = self.sim.extract_source_constants(source_text, "StormSurfaceTurbulenceStrength")
        self.assertGreaterEqual(len(surface_values), 2)
        self.assertGreaterEqual(len(turbulence_values), 2)
        self.assertTrue(all(value == 50.0 for value in surface_values))
        self.assertTrue(all(value == 0.4 for value in turbulence_values))

    def test_selected_weights_reach_without_sdf_pushout(self) -> None:
        result = self.sim.simulate(self.weights, use_smoothing=True)
        self.assertTrue(result.reached)
        self.assertEqual(result.sdf_pushout_events, 0)
        self.assertGreaterEqual(result.min_obstacle_distance, 2.0)
        self.assertLessEqual(result.jitter_events, 1)

    def test_exported_metrics_match_replay(self) -> None:
        self.assertEqual(self.sim.weights_to_dict(self.best_weights), self.tuning["selectedWeights"])
        self.assertEqual(self.sim.result_to_dict(self.best_smoothed), self.tuning["smoothedMetrics"])
        self.assertEqual(self.sim.result_to_dict(self.best_raw), self.tuning["rawNoSmoothingMetrics"])
        self.assertEqual(self.sim.idle_drift(self.weights), self.tuning["idleDriftVerification"])
        self.assertEqual(self.best_evaluated, self.tuning["search"]["candidatesEvaluated"])
        self.assertEqual(self.best_reached, self.tuning["search"]["candidatesReachedTarget"])

    def test_path_trace_matches_replay(self) -> None:
        trace = self.tuning["pathTrace"]
        self.assertEqual(self.sim.trace_path(self.weights, use_smoothing=True), trace)
        self.assertTrue(trace["reached"])
        self.assertEqual(trace["sdfPushoutEvents"], 0)
        self.assertGreaterEqual(trace["minObstacleClearanceMeters"], 2.0)
        self.assertEqual(trace["samples"][0]["step"], 0)
        self.assertEqual(trace["samples"][-1]["step"], self.tuning["smoothedMetrics"]["steps"])
        self.assertLessEqual(trace["samples"][-1]["distanceToTargetMeters"], 3.0)

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
