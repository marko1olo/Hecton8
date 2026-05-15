#!/usr/bin/env python3
"""Unit tests for the UX aggregate report validator."""

from __future__ import annotations

import copy
import json
import os
import unittest
from pathlib import Path

from Tools.UX.validate_aggregate_report import (
    EXPECTED_ARTIFACT_HASHES,
    EXPECTED_COMMANDS,
    EXPECTED_UNIT_HARNESS_TESTS,
    validate_aggregate_report,
)


ROOT = Path(__file__).resolve().parents[2]
REPORT_PATH = ROOT / "Docs/AgentLogs/UI_HardwareAdaptiveValidation_UX_ENGINEER.json"
PROBE_PATH = ROOT / "Docs/AgentLogs/UI_UnityEnvironmentProbe_UX_ENGINEER.json"


def _load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def _valid_report_fixture() -> dict:
    commands = [
        {
            "name": name,
            "command": ["python", name],
            "exitCode": 0,
            "elapsedMs": 1.0,
            "stdoutTail": "",
            "stderrTail": "",
        }
        for name in EXPECTED_COMMANDS
    ]
    for command in commands:
        if command["name"] == "unit_harness":
            command["stderrTail"] = f"Ran {EXPECTED_UNIT_HARNESS_TESTS} tests in 1.000s\n\nOK\n"

    return {
        "schema": "hecton8.hardware_adaptive_ui_scaler.aggregate_validation.v2",
        "owner": "UX_ENGINEER",
        "promptId": "HARDWARE_ADAPTIVE_UI_BAKER",
        "status": "PASS",
        "unityRuntimeStatus": "PENDING_UNITY_VERIFICATION",
        "commandCount": len(commands),
        "commands": commands,
        "missingArtifacts": [],
        "artifactHashCount": EXPECTED_ARTIFACT_HASHES,
        "artifactSha256": {f"artifact_{index:02d}": "0" * 64 for index in range(EXPECTED_ARTIFACT_HASHES)},
        "unitHarnessTestCount": EXPECTED_UNIT_HARNESS_TESTS,
        "pythonCacheCountAfter": 0,
        "aggregateSelfValidation": {"status": "PASS", "failures": []},
        "statusLogSelfValidation": {"status": "PASS", "failures": []},
    }


class AggregateReportValidatorTests(unittest.TestCase):
    def test_current_aggregate_report_is_valid(self) -> None:
        if os.environ.get("H8_UX_AGGREGATE_RUNNING") == "1":
            self.skipTest("aggregate runner validates the candidate report after command execution")
        self.assertEqual([], validate_aggregate_report(_load(REPORT_PATH), _load(PROBE_PATH)))

    def test_rejects_runtime_promotion(self) -> None:
        report = _valid_report_fixture()
        report["unityRuntimeStatus"] = "PASS"

        failures = validate_aggregate_report(report, _load(PROBE_PATH))

        self.assertTrue(any("unityRuntimeStatus" in failure for failure in failures))

    def test_rejects_missing_command(self) -> None:
        report = _valid_report_fixture()
        report["commands"] = [record for record in report["commands"] if record["name"] != "unity_environment_probe"]
        report["commandCount"] = len(report["commands"])

        failures = validate_aggregate_report(report, _load(PROBE_PATH))

        self.assertIn("missing aggregate command: unity_environment_probe", failures)

    def test_rejects_cleanup_not_last(self) -> None:
        report = _valid_report_fixture()
        report["commands"][-1], report["commands"][-2] = report["commands"][-2], report["commands"][-1]

        failures = validate_aggregate_report(report, _load(PROBE_PATH))

        self.assertIn("python_cache_cleanup must be the final aggregate command", failures)

    def test_rejects_failed_command(self) -> None:
        report = _valid_report_fixture()
        report["commands"][0]["exitCode"] = 1

        failures = validate_aggregate_report(report, _load(PROBE_PATH))

        self.assertTrue(any("exitCode must be 0" in failure for failure in failures))

    def test_rejects_failed_self_validation(self) -> None:
        report = _valid_report_fixture()
        report["aggregateSelfValidation"] = {"status": "FAIL", "failures": ["forced failure"]}

        failures = validate_aggregate_report(report, _load(PROBE_PATH))

        self.assertIn("aggregateSelfValidation must be PASS with no failures", failures)

    def test_rejects_failed_status_log_self_validation(self) -> None:
        report = _valid_report_fixture()
        report["statusLogSelfValidation"] = {"status": "FAIL", "failures": ["forced failure"]}

        failures = validate_aggregate_report(report, _load(PROBE_PATH))

        self.assertIn("statusLogSelfValidation must be PASS with no failures", failures)

    def test_rejects_unit_harness_count_drift(self) -> None:
        report = _valid_report_fixture()
        report["unitHarnessTestCount"] = EXPECTED_UNIT_HARNESS_TESTS - 1

        failures = validate_aggregate_report(report, _load(PROBE_PATH))

        self.assertIn("unitHarnessTestCount must match unit_harness output", failures)
        self.assertIn(f"unitHarnessTestCount must be {EXPECTED_UNIT_HARNESS_TESTS}", failures)

    def test_rejects_artifact_hash_count_drift(self) -> None:
        report = _valid_report_fixture()
        report["artifactHashCount"] = EXPECTED_ARTIFACT_HASHES - 1

        failures = validate_aggregate_report(report, _load(PROBE_PATH))

        self.assertIn("artifactHashCount must match artifactSha256 length", failures)
        self.assertIn(f"artifactHashCount must be {EXPECTED_ARTIFACT_HASHES}", failures)

    def test_rejects_generated_python_cache_left_after_aggregate(self) -> None:
        report = _valid_report_fixture()
        report["pythonCacheCountAfter"] = 1

        failures = validate_aggregate_report(report, _load(PROBE_PATH))

        self.assertIn("pythonCacheCountAfter must be 0", failures)


if __name__ == "__main__":
    unittest.main()
