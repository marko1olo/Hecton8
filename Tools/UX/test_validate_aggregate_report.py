#!/usr/bin/env python3
"""Unit tests for the UX aggregate report validator."""

from __future__ import annotations

import copy
import json
import unittest
from pathlib import Path

from Tools.UX.validate_aggregate_report import validate_aggregate_report


ROOT = Path(__file__).resolve().parents[2]
REPORT_PATH = ROOT / "Docs/AgentLogs/UI_HardwareAdaptiveValidation_UX_ENGINEER.json"
PROBE_PATH = ROOT / "Docs/AgentLogs/UI_UnityEnvironmentProbe_UX_ENGINEER.json"


def _load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


class AggregateReportValidatorTests(unittest.TestCase):
    def test_current_aggregate_report_is_valid(self) -> None:
        self.assertEqual([], validate_aggregate_report(_load(REPORT_PATH), _load(PROBE_PATH)))

    def test_rejects_runtime_promotion(self) -> None:
        report = copy.deepcopy(_load(REPORT_PATH))
        report["unityRuntimeStatus"] = "PASS"

        failures = validate_aggregate_report(report, _load(PROBE_PATH))

        self.assertTrue(any("unityRuntimeStatus" in failure for failure in failures))

    def test_rejects_missing_command(self) -> None:
        report = copy.deepcopy(_load(REPORT_PATH))
        report["commands"] = [record for record in report["commands"] if record["name"] != "unity_environment_probe"]

        failures = validate_aggregate_report(report, _load(PROBE_PATH))

        self.assertIn("missing aggregate command: unity_environment_probe", failures)

    def test_rejects_failed_command(self) -> None:
        report = copy.deepcopy(_load(REPORT_PATH))
        report["commands"][0]["exitCode"] = 1

        failures = validate_aggregate_report(report, _load(PROBE_PATH))

        self.assertTrue(any("exitCode must be 0" in failure for failure in failures))

    def test_rejects_failed_self_validation(self) -> None:
        report = copy.deepcopy(_load(REPORT_PATH))
        report["aggregateSelfValidation"] = {"status": "FAIL", "failures": ["forced failure"]}

        failures = validate_aggregate_report(report, _load(PROBE_PATH))

        self.assertIn("aggregateSelfValidation must be PASS with no failures", failures)

    def test_rejects_failed_status_log_self_validation(self) -> None:
        report = copy.deepcopy(_load(REPORT_PATH))
        report["statusLogSelfValidation"] = {"status": "FAIL", "failures": ["forced failure"]}

        failures = validate_aggregate_report(report, _load(PROBE_PATH))

        self.assertIn("statusLogSelfValidation must be PASS with no failures", failures)


if __name__ == "__main__":
    unittest.main()
