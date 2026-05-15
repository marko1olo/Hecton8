#!/usr/bin/env python3
"""Regression tests for the Alpha Leviathan battle simulator artifacts."""

from __future__ import annotations

import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path


sys.dont_write_bytecode = True

ROOT = Path(__file__).resolve().parents[1]
SIM_PATH = ROOT / "Tools" / "AiBattleSim.py"
BRAIN_PATH = ROOT / "Data" / "AI" / "Leviathan_Brain.json"
REPORT_PATH = ROOT / "Tools" / "AiBattleSim_Report.json"


def load_sim_module():
    spec = importlib.util.spec_from_file_location("ai_battle_sim", SIM_PATH)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load simulator module from {SIM_PATH}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


class AiBattleSimTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.sim = load_sim_module()
        cls.brain = json.loads(BRAIN_PATH.read_text(encoding="utf-8"))
        cls.report = json.loads(REPORT_PATH.read_text(encoding="utf-8"))

    def write_temp_report(self, name: str, report: dict) -> Path:
        temp_dir = tempfile.TemporaryDirectory(prefix=f"AiBattleSim_{name}_")
        self.addCleanup(temp_dir.cleanup)
        report_path = Path(temp_dir.name) / "report.json"
        report_path.write_text(json.dumps(report), encoding="utf-8")
        return report_path

    def test_brain_contract_has_50_even_utility_rows(self) -> None:
        validation = self.sim.validate_brain(self.brain)
        self.assertEqual(validation["status"], "BRAIN_VALIDATION_PASSED")
        self.assertEqual(validation["errors"], [])
        self.assertEqual(validation["utilityScoreCount"], 50)
        self.assertEqual(
            validation["behaviorCounts"],
            {
                "Circle": 10,
                "Hide": 10,
                "Breach": 10,
                "FalseCharge": 10,
                "RealAttack": 10,
            },
        )

    def test_current_report_passes_artifact_checker(self) -> None:
        check = self.sim.check_artifacts(BRAIN_PATH, REPORT_PATH, 10_000)
        self.assertEqual(check["status"], "ARTIFACT_CHECK_PASSED")
        self.assertEqual(check["errors"], [])
        self.assertEqual(check["encounters"], 10_000)
        self.assertEqual(check["under30KillRate"], 0.0)

    def test_buffer_id_validation_reads_current_h8memory_source(self) -> None:
        validation = self.sim.validate_brain(self.brain)
        self.assertEqual(validation["knownBufferSourceStatus"], "SOURCE_PARSED")
        self.assertGreaterEqual(validation["knownBufferCount"], 30)
        buffers, _, status = self.sim.load_known_global_data_vault_buffers()
        self.assertEqual(status, "SOURCE_PARSED")
        self.assertIn("PlayerKinematicState", buffers)
        self.assertIn("EntityAUPs", buffers)
        self.assertNotIn("CameraMain", buffers)

    def test_short_run_stays_inside_frustration_contract(self) -> None:
        results, aggression, passes, lowered = self.sim.execute_simulation(
            self.brain,
            encounters=300,
            seed=self.sim.stable_seed(self.sim.DEFAULT_SEED, 0x554E4954),
            initial_aggression=1.0,
        )
        report = self.sim.build_report(
            self.brain,
            self.sim.validate_brain(self.brain),
            results,
            elapsed=0.0,
            aggression=aggression,
            passes=passes,
            lowered=lowered,
            seed=self.sim.DEFAULT_SEED,
        )
        self.assertEqual(report["status"], "INSTINCTS DEFINED")
        self.assertTrue(report["frustrationAudit"]["terrorNotFrustrationPassed"])
        self.assertLessEqual(report["summary"]["under30KillRate"], 0.35)

    def test_unknown_vault_buffer_is_rejected(self) -> None:
        brain = json.loads(BRAIN_PATH.read_text(encoding="utf-8"))
        brain["globalDataVaultFeeds"][0]["bufferIds"][0] = "CameraMain"
        validation = self.sim.validate_brain(brain)
        self.assertEqual(validation["status"], "BRAIN_VALIDATION_FAILED")
        self.assertTrue(any("unknown GlobalDataVault BufferID" in error for error in validation["errors"]))

    def test_missing_score_row_is_rejected(self) -> None:
        brain = json.loads(BRAIN_PATH.read_text(encoding="utf-8"))
        brain["utilityScores"] = brain["utilityScores"][:-1]
        validation = self.sim.validate_brain(brain)
        self.assertEqual(validation["status"], "BRAIN_VALIDATION_FAILED")
        self.assertTrue(any("utilityScores count" in error for error in validation["errors"]))

    def test_subgroup_guard_failure_is_rejected(self) -> None:
        report = json.loads(REPORT_PATH.read_text(encoding="utf-8"))
        report["frustrationAudit"]["subgroupGuardPassed"] = False
        report["frustrationAudit"]["terrorNotFrustrationPassed"] = False
        report_path = self.write_temp_report("subgroup_guard", report)
        check = self.sim.check_artifacts(BRAIN_PATH, report_path, 10_000)
        self.assertEqual(check["status"], "ARTIFACT_CHECK_FAILED")
        self.assertTrue(any("subgroupGuardPassed" in error for error in check["errors"]))

    def test_target_kill_rate_failure_is_rejected(self) -> None:
        report = json.loads(REPORT_PATH.read_text(encoding="utf-8"))
        report["summary"]["killRate"] = 0.99
        report_path = self.write_temp_report("target_rate", report)
        check = self.sim.check_artifacts(BRAIN_PATH, report_path, 10_000)
        self.assertEqual(check["status"], "ARTIFACT_CHECK_FAILED")
        self.assertTrue(any("summary.killRate" in error for error in check["errors"]))

    def test_stale_report_validation_is_rejected(self) -> None:
        report = json.loads(REPORT_PATH.read_text(encoding="utf-8"))
        report["validation"]["knownBufferCount"] = 0
        report_path = self.write_temp_report("stale_validation", report)
        check = self.sim.check_artifacts(BRAIN_PATH, report_path, 10_000)
        self.assertEqual(check["status"], "ARTIFACT_CHECK_FAILED")
        self.assertTrue(any("knownBufferCount" in error for error in check["errors"]))


if __name__ == "__main__":
    unittest.main()
