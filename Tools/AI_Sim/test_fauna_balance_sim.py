#!/usr/bin/env python3
"""Regression tests for the offline fauna balance simulator."""

from __future__ import annotations

import importlib.util
import json
import sys
import unittest
from pathlib import Path


sys.dont_write_bytecode = True

ROOT = Path(__file__).resolve().parents[2]
SIM_PATH = ROOT / "Tools" / "AI_Sim" / "FaunaBalanceSim.py"
CONSTANTS_PATH = ROOT / "Data" / "AI" / "Fauna_Global_Weights.json"
REPORT_PATH = ROOT / "Tools" / "AI_Sim" / "FaunaBalanceSim_Report.json"
REPLICATE_PATH = ROOT / "Tools" / "AI_Sim" / "FaunaBalanceSim_ReplicateValidation.json"
TEST_TEMP_ROOT = ROOT / "Temp" / "FaunaBalanceSimTests"


def artifact_workspace(name: str) -> Path:
    path = TEST_TEMP_ROOT / name
    path.mkdir(parents=True, exist_ok=True)
    return path


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

    def test_artifact_checker_rejects_nonfinite_numbers(self) -> None:
        temp_path = artifact_workspace("nonfinite")
        constants_path = temp_path / "constants.json"
        report_path = temp_path / "report.json"
        replicate_path = temp_path / "replicate.json"
        constants_path.write_text(CONSTANTS_PATH.read_text(encoding="utf-8"), encoding="utf-8")
        report_path.write_text(REPORT_PATH.read_text(encoding="utf-8"), encoding="utf-8")
        replicate_path.write_text(REPLICATE_PATH.read_text(encoding="utf-8"), encoding="utf-8")

        constants = json.loads(constants_path.read_text(encoding="utf-8"))
        constants["millionFrameSummary"]["score"] = float("nan")
        constants_path.write_text(json.dumps(constants), encoding="utf-8")

        check = self.sim.check_artifacts(constants_path, report_path, replicate_path)
        self.assertEqual(check["status"], "ARTIFACT_CHECK_FAILED")
        self.assertTrue(any("nonfinite number" in error for error in check["errors"]))

    def test_artifact_checker_rejects_schema_drift(self) -> None:
        temp_path = artifact_workspace("schema_drift")
        constants_path = temp_path / "constants.json"
        report_path = temp_path / "report.json"
        replicate_path = temp_path / "replicate.json"
        constants = json.loads(CONSTANTS_PATH.read_text(encoding="utf-8"))
        constants["schemaVersion"] = 2
        constants_path.write_text(json.dumps(constants), encoding="utf-8")
        report_path.write_text(REPORT_PATH.read_text(encoding="utf-8"), encoding="utf-8")
        replicate_path.write_text(REPLICATE_PATH.read_text(encoding="utf-8"), encoding="utf-8")

        check = self.sim.check_artifacts(constants_path, report_path, replicate_path)
        self.assertEqual(check["status"], "ARTIFACT_CHECK_FAILED")
        self.assertTrue(any("constants.schemaVersion" in error for error in check["errors"]))

    def test_artifact_checker_rejects_runtime_proof_drift(self) -> None:
        temp_path = artifact_workspace("runtime_proof_drift")
        constants_path = temp_path / "constants.json"
        report_path = temp_path / "report.json"
        replicate_path = temp_path / "replicate.json"
        constants = json.loads(CONSTANTS_PATH.read_text(encoding="utf-8"))
        constants["runtimeUnityProof"] = "VERIFIED"
        constants_path.write_text(json.dumps(constants), encoding="utf-8")
        report_path.write_text(REPORT_PATH.read_text(encoding="utf-8"), encoding="utf-8")
        replicate_path.write_text(REPLICATE_PATH.read_text(encoding="utf-8"), encoding="utf-8")

        check = self.sim.check_artifacts(constants_path, report_path, replicate_path)
        self.assertEqual(check["status"], "ARTIFACT_CHECK_FAILED")
        self.assertTrue(any("constants.runtimeUnityProof" in error for error in check["errors"]))

    def test_artifact_checker_rejects_report_path_drift(self) -> None:
        temp_path = artifact_workspace("report_path_drift")
        constants_path = temp_path / "constants.json"
        report_path = temp_path / "report.json"
        replicate_path = temp_path / "replicate.json"
        constants = json.loads(CONSTANTS_PATH.read_text(encoding="utf-8"))
        constants["detailedReport"] = "Tools/AI_Sim/Wrong_Report.json"
        constants["replicateValidation"]["detailedReport"] = "Tools/AI_Sim/Wrong_Replicate.json"
        constants_path.write_text(json.dumps(constants), encoding="utf-8")
        report_path.write_text(REPORT_PATH.read_text(encoding="utf-8"), encoding="utf-8")
        replicate_path.write_text(REPLICATE_PATH.read_text(encoding="utf-8"), encoding="utf-8")

        check = self.sim.check_artifacts(constants_path, report_path, replicate_path)
        self.assertEqual(check["status"], "ARTIFACT_CHECK_FAILED")
        self.assertTrue(any("constants.detailedReport" in error for error in check["errors"]))
        self.assertTrue(any("replicateValidation.detailedReport" in error for error in check["errors"]))

    def test_artifact_checker_rejects_missing_heatmap_evidence(self) -> None:
        temp_path = artifact_workspace("missing_heatmap_evidence")
        constants_path = temp_path / "constants.json"
        report_path = temp_path / "report.json"
        replicate_path = temp_path / "replicate.json"
        report = json.loads(REPORT_PATH.read_text(encoding="utf-8"))
        report["heatmapTop10"] = report["heatmapTop10"][:3]
        constants_path.write_text(CONSTANTS_PATH.read_text(encoding="utf-8"), encoding="utf-8")
        report_path.write_text(json.dumps(report), encoding="utf-8")
        replicate_path.write_text(REPLICATE_PATH.read_text(encoding="utf-8"), encoding="utf-8")

        check = self.sim.check_artifacts(constants_path, report_path, replicate_path)
        self.assertEqual(check["status"], "ARTIFACT_CHECK_FAILED")
        self.assertTrue(any("heatmapTop10" in error for error in check["errors"]))

    def test_artifact_checker_rejects_heatmap_sweet_spot_drift(self) -> None:
        temp_path = artifact_workspace("heatmap_sweet_spot_drift")
        constants_path = temp_path / "constants.json"
        report_path = temp_path / "report.json"
        replicate_path = temp_path / "replicate.json"
        report = json.loads(REPORT_PATH.read_text(encoding="utf-8"))
        report["heatmapTop10"][0]["weights"]["aggressionScalar"] = 0.5
        constants_path.write_text(CONSTANTS_PATH.read_text(encoding="utf-8"), encoding="utf-8")
        report_path.write_text(json.dumps(report), encoding="utf-8")
        replicate_path.write_text(REPLICATE_PATH.read_text(encoding="utf-8"), encoding="utf-8")

        check = self.sim.check_artifacts(constants_path, report_path, replicate_path)
        self.assertEqual(check["status"], "ARTIFACT_CHECK_FAILED")
        self.assertTrue(any("sweet spot mismatch" in error for error in check["errors"]))

    def test_artifact_checker_rejects_noise_evidence_drift(self) -> None:
        temp_path = artifact_workspace("noise_evidence_drift")
        constants_path = temp_path / "constants.json"
        report_path = temp_path / "report.json"
        replicate_path = temp_path / "replicate.json"
        report = json.loads(REPORT_PATH.read_text(encoding="utf-8"))
        report["noiseRobustness"] = report["noiseRobustness"][:-1]
        constants_path.write_text(CONSTANTS_PATH.read_text(encoding="utf-8"), encoding="utf-8")
        report_path.write_text(json.dumps(report), encoding="utf-8")
        replicate_path.write_text(REPLICATE_PATH.read_text(encoding="utf-8"), encoding="utf-8")

        check = self.sim.check_artifacts(constants_path, report_path, replicate_path)
        self.assertEqual(check["status"], "ARTIFACT_CHECK_FAILED")
        self.assertTrue(any("noiseRobustness cases mismatch" in error for error in check["errors"]))

    def test_artifact_checker_rejects_retinal_evidence_drift(self) -> None:
        temp_path = artifact_workspace("retinal_evidence_drift")
        constants_path = temp_path / "constants.json"
        report_path = temp_path / "report.json"
        replicate_path = temp_path / "replicate.json"
        report = json.loads(REPORT_PATH.read_text(encoding="utf-8"))
        report["retinalBlindnessTests"]["retinal_blind_acoustic_tracking"]["kills"]["stalker"] = 0.0
        report["retinalBlindnessTests"]["retinal_blind_acoustic_tracking"]["kills"]["alphaLeviathan"] = 0.0
        constants_path.write_text(CONSTANTS_PATH.read_text(encoding="utf-8"), encoding="utf-8")
        report_path.write_text(json.dumps(report), encoding="utf-8")
        replicate_path.write_text(REPLICATE_PATH.read_text(encoding="utf-8"), encoding="utf-8")

        check = self.sim.check_artifacts(constants_path, report_path, replicate_path)
        self.assertEqual(check["status"], "ARTIFACT_CHECK_FAILED")
        self.assertTrue(any("retinal acoustic compensation" in error for error in check["errors"]))

    def test_artifact_checker_rejects_fear_curve_evidence_drift(self) -> None:
        temp_path = artifact_workspace("fear_curve_evidence_drift")
        constants_path = temp_path / "constants.json"
        report_path = temp_path / "report.json"
        replicate_path = temp_path / "replicate.json"
        report = json.loads(REPORT_PATH.read_text(encoding="utf-8"))
        report["fearCurveComparison"]["linear_fear"]["score"] = 0.1
        constants_path.write_text(CONSTANTS_PATH.read_text(encoding="utf-8"), encoding="utf-8")
        report_path.write_text(json.dumps(report), encoding="utf-8")
        replicate_path.write_text(REPLICATE_PATH.read_text(encoding="utf-8"), encoding="utf-8")

        check = self.sim.check_artifacts(constants_path, report_path, replicate_path)
        self.assertEqual(check["status"], "ARTIFACT_CHECK_FAILED")
        self.assertTrue(any("quadratic fear curve" in error for error in check["errors"]))

    def test_artifact_checker_rejects_replicate_summary_drift(self) -> None:
        temp_path = artifact_workspace("replicate_summary_drift")
        constants_path = temp_path / "constants.json"
        report_path = temp_path / "report.json"
        replicate_path = temp_path / "replicate.json"
        constants = json.loads(CONSTANTS_PATH.read_text(encoding="utf-8"))
        constants["replicateValidation"]["framesPerReplicate"] = 12345
        constants["replicateValidation"]["replicates"] = 99
        constants_path.write_text(json.dumps(constants), encoding="utf-8")
        report_path.write_text(REPORT_PATH.read_text(encoding="utf-8"), encoding="utf-8")
        replicate_path.write_text(REPLICATE_PATH.read_text(encoding="utf-8"), encoding="utf-8")

        check = self.sim.check_artifacts(constants_path, report_path, replicate_path)
        self.assertEqual(check["status"], "ARTIFACT_CHECK_FAILED")
        self.assertTrue(any("framesPerReplicate mismatch" in error for error in check["errors"]))
        self.assertTrue(any("replicate count mismatch" in error for error in check["errors"]))

    def test_artifact_checker_rejects_replicate_weight_drift(self) -> None:
        temp_path = artifact_workspace("replicate_weight_drift")
        constants_path = temp_path / "constants.json"
        report_path = temp_path / "report.json"
        replicate_path = temp_path / "replicate.json"
        replicate = json.loads(REPLICATE_PATH.read_text(encoding="utf-8"))
        replicate["weights"]["fearScalar"] = 1.25
        constants_path.write_text(CONSTANTS_PATH.read_text(encoding="utf-8"), encoding="utf-8")
        report_path.write_text(REPORT_PATH.read_text(encoding="utf-8"), encoding="utf-8")
        replicate_path.write_text(json.dumps(replicate), encoding="utf-8")

        check = self.sim.check_artifacts(constants_path, report_path, replicate_path)
        self.assertEqual(check["status"], "ARTIFACT_CHECK_FAILED")
        self.assertTrue(any("replicate.weights.fearScalar mismatch" in error for error in check["errors"]))

    def test_artifact_checker_rejects_out_of_range_constants(self) -> None:
        temp_path = artifact_workspace("out_of_range")
        constants_path = temp_path / "constants.json"
        report_path = temp_path / "report.json"
        replicate_path = temp_path / "replicate.json"
        constants = json.loads(CONSTANTS_PATH.read_text(encoding="utf-8"))
        report = json.loads(REPORT_PATH.read_text(encoding="utf-8"))
        constants["selectedConstants"]["aggressionScalar"] = 99.0
        report["selectedConstants"]["aggressionScalar"] = 99.0
        constants_path.write_text(json.dumps(constants), encoding="utf-8")
        report_path.write_text(json.dumps(report), encoding="utf-8")
        replicate_path.write_text(REPLICATE_PATH.read_text(encoding="utf-8"), encoding="utf-8")

        check = self.sim.check_artifacts(constants_path, report_path, replicate_path)
        self.assertEqual(check["status"], "ARTIFACT_CHECK_FAILED")
        self.assertTrue(any("aggressionScalar out of range" in error for error in check["errors"]))

    def test_artifact_checker_rejects_million_frame_score_drift(self) -> None:
        temp_path = artifact_workspace("million_frame_score_drift")
        constants_path = temp_path / "constants.json"
        report_path = temp_path / "report.json"
        replicate_path = temp_path / "replicate.json"
        constants = json.loads(CONSTANTS_PATH.read_text(encoding="utf-8"))
        constants["millionFrameSummary"]["score"] = 99.0
        constants_path.write_text(json.dumps(constants), encoding="utf-8")
        report_path.write_text(REPORT_PATH.read_text(encoding="utf-8"), encoding="utf-8")
        replicate_path.write_text(REPLICATE_PATH.read_text(encoding="utf-8"), encoding="utf-8")

        check = self.sim.check_artifacts(constants_path, report_path, replicate_path)
        self.assertEqual(check["status"], "ARTIFACT_CHECK_FAILED")
        self.assertTrue(any("millionFrame score mismatch" in error for error in check["errors"]))

    def test_artifact_checker_rejects_million_frame_stability_drift(self) -> None:
        temp_path = artifact_workspace("million_frame_stability_drift")
        constants_path = temp_path / "constants.json"
        report_path = temp_path / "report.json"
        replicate_path = temp_path / "replicate.json"
        constants = json.loads(CONSTANTS_PATH.read_text(encoding="utf-8"))
        constants["millionFrameSummary"]["stability"]["extinct"] = True
        constants_path.write_text(json.dumps(constants), encoding="utf-8")
        report_path.write_text(REPORT_PATH.read_text(encoding="utf-8"), encoding="utf-8")
        replicate_path.write_text(REPLICATE_PATH.read_text(encoding="utf-8"), encoding="utf-8")

        check = self.sim.check_artifacts(constants_path, report_path, replicate_path)
        self.assertEqual(check["status"], "ARTIFACT_CHECK_FAILED")
        self.assertTrue(any("millionFrame stability mismatch" in error for error in check["errors"]))

    def test_artifact_checker_rejects_million_frame_weight_drift(self) -> None:
        temp_path = artifact_workspace("million_frame_weight_drift")
        constants_path = temp_path / "constants.json"
        report_path = temp_path / "report.json"
        replicate_path = temp_path / "replicate.json"
        report = json.loads(REPORT_PATH.read_text(encoding="utf-8"))
        report["millionFrameRun"]["weights"]["fearScalar"] = 1.25
        constants_path.write_text(CONSTANTS_PATH.read_text(encoding="utf-8"), encoding="utf-8")
        report_path.write_text(json.dumps(report), encoding="utf-8")
        replicate_path.write_text(REPLICATE_PATH.read_text(encoding="utf-8"), encoding="utf-8")

        check = self.sim.check_artifacts(constants_path, report_path, replicate_path)
        self.assertEqual(check["status"], "ARTIFACT_CHECK_FAILED")
        self.assertTrue(any("millionFrameRun.weights.fearScalar mismatch" in error for error in check["errors"]))

    def test_artifact_checker_rejects_million_frame_sample_truncation(self) -> None:
        temp_path = artifact_workspace("million_frame_sample_truncation")
        constants_path = temp_path / "constants.json"
        report_path = temp_path / "report.json"
        replicate_path = temp_path / "replicate.json"
        report = json.loads(REPORT_PATH.read_text(encoding="utf-8"))
        report["millionFrameRun"]["samples"] = report["millionFrameRun"]["samples"][:-1]
        constants_path.write_text(CONSTANTS_PATH.read_text(encoding="utf-8"), encoding="utf-8")
        report_path.write_text(json.dumps(report), encoding="utf-8")
        replicate_path.write_text(REPLICATE_PATH.read_text(encoding="utf-8"), encoding="utf-8")

        check = self.sim.check_artifacts(constants_path, report_path, replicate_path)
        self.assertEqual(check["status"], "ARTIFACT_CHECK_FAILED")
        self.assertTrue(any("samples count" in error for error in check["errors"]))

    def test_artifact_checker_rejects_million_frame_sample_order_drift(self) -> None:
        temp_path = artifact_workspace("million_frame_sample_order_drift")
        constants_path = temp_path / "constants.json"
        report_path = temp_path / "report.json"
        replicate_path = temp_path / "replicate.json"
        report = json.loads(REPORT_PATH.read_text(encoding="utf-8"))
        report["millionFrameRun"]["samples"][1]["frame"] = 0
        constants_path.write_text(CONSTANTS_PATH.read_text(encoding="utf-8"), encoding="utf-8")
        report_path.write_text(json.dumps(report), encoding="utf-8")
        replicate_path.write_text(REPLICATE_PATH.read_text(encoding="utf-8"), encoding="utf-8")

        check = self.sim.check_artifacts(constants_path, report_path, replicate_path)
        self.assertEqual(check["status"], "ARTIFACT_CHECK_FAILED")
        self.assertTrue(any("frame order" in error for error in check["errors"]))

    def test_load_selected_weights_rejects_bad_constants(self) -> None:
        temp_path = artifact_workspace("bad_load_constants")
        constants_path = temp_path / "constants.json"
        constants = json.loads(CONSTANTS_PATH.read_text(encoding="utf-8"))
        constants["selectedConstants"]["fearCurvePower"] = 99.0
        constants_path.write_text(json.dumps(constants), encoding="utf-8")
        with self.assertRaises(ValueError):
            self.sim.load_selected_weights(constants_path)

    def test_load_selected_weights_rejects_bad_header(self) -> None:
        temp_path = artifact_workspace("bad_load_header")
        constants_path = temp_path / "constants.json"
        constants = json.loads(CONSTANTS_PATH.read_text(encoding="utf-8"))
        constants["status"] = "UNVERIFIED"
        constants_path.write_text(json.dumps(constants), encoding="utf-8")
        with self.assertRaises(ValueError):
            self.sim.load_selected_weights(constants_path)


if __name__ == "__main__":
    unittest.main()
