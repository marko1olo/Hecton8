#!/usr/bin/env python3
"""Validate the UX aggregate static-validation report."""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
DEFAULT_REPORT = ROOT / "Docs/AgentLogs/UI_HardwareAdaptiveValidation_UX_ENGINEER.json"
DEFAULT_ENVIRONMENT_PROBE = ROOT / "Docs/AgentLogs/UI_UnityEnvironmentProbe_UX_ENGINEER.json"

EXPECTED_COMMANDS = (
    "readability",
    "shader_sample_audit",
    "icon_baker_self_test",
    "unity_template_audit",
    "unity_report_audit",
    "unity_environment_probe",
    "unit_harness",
    "python_cache_cleanup",
)

EXPECTED_UNIT_HARNESS_TESTS = 38
EXPECTED_ARTIFACT_HASHES = 30

ALLOWED_UNITY_PROBE_STATUSES = {
    "UNITY_NOT_FOUND",
    "UNITY_REQUIRED_VERSION_FOUND",
    "UNITY_VERSION_MISMATCH",
    "UNITY_AVAILABLE_VERSION_UNKNOWN",
}


def _load_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def validate_aggregate_report(report: dict[str, Any], environment_probe: dict[str, Any]) -> list[str]:
    """Return aggregate report validation failures."""

    failures: list[str] = []

    if report.get("schema") != "hecton8.hardware_adaptive_ui_scaler.aggregate_validation.v2":
        failures.append("aggregate schema must be v2")
    if report.get("owner") != "UX_ENGINEER":
        failures.append("owner must be UX_ENGINEER")
    if report.get("promptId") != "HARDWARE_ADAPTIVE_UI_BAKER":
        failures.append("promptId must be HARDWARE_ADAPTIVE_UI_BAKER")
    if report.get("status") != "PASS":
        failures.append("aggregate status must be PASS")
    if report.get("unityRuntimeStatus") != "PENDING_UNITY_VERIFICATION":
        failures.append("unityRuntimeStatus must stay PENDING_UNITY_VERIFICATION")
    if report.get("missingArtifacts") != []:
        failures.append("missingArtifacts must be empty")

    self_validation = report.get("aggregateSelfValidation")
    if self_validation is not None:
        if not isinstance(self_validation, dict):
            failures.append("aggregateSelfValidation must be an object")
        elif self_validation.get("status") != "PASS" or self_validation.get("failures") not in ([], None):
            failures.append("aggregateSelfValidation must be PASS with no failures")

    status_log_validation = report.get("statusLogSelfValidation")
    if status_log_validation is not None:
        if not isinstance(status_log_validation, dict):
            failures.append("statusLogSelfValidation must be an object")
        elif status_log_validation.get("status") != "PASS" or status_log_validation.get("failures") not in ([], None):
            failures.append("statusLogSelfValidation must be PASS with no failures")

    command_records = report.get("commands")
    if not isinstance(command_records, list):
        failures.append("commands must be a list")
        command_records = []
    elif report.get("commandCount") != len(command_records):
        failures.append("commandCount must match commands length")
    elif report.get("commandCount") != len(EXPECTED_COMMANDS):
        failures.append(f"commandCount must be {len(EXPECTED_COMMANDS)}")

    command_names = [record.get("name") for record in command_records if isinstance(record, dict)]
    for expected_command in EXPECTED_COMMANDS:
        if expected_command not in command_names:
            failures.append(f"missing aggregate command: {expected_command}")

    for record in command_records:
        if not isinstance(record, dict):
            failures.append("command record must be an object")
            continue
        if record.get("exitCode") != 0:
            failures.append(f"{record.get('name', '<unknown>')}: exitCode must be 0")

    unit_record = next((record for record in command_records if isinstance(record, dict) and record.get("name") == "unit_harness"), None)
    if isinstance(unit_record, dict):
        combined_output = str(unit_record.get("stdoutTail", "")) + "\n" + str(unit_record.get("stderrTail", ""))
        match = re.search(r"Ran\s+(\d+)\s+tests", combined_output)
        if not match:
            failures.append("unit_harness output must include test count")
        else:
            test_count = int(match.group(1))
            if test_count != EXPECTED_UNIT_HARNESS_TESTS:
                failures.append(f"unit_harness must run exactly {EXPECTED_UNIT_HARNESS_TESTS} tests")
            if report.get("unitHarnessTestCount") != test_count:
                failures.append("unitHarnessTestCount must match unit_harness output")
            if report.get("unitHarnessTestCount") != EXPECTED_UNIT_HARNESS_TESTS:
                failures.append(f"unitHarnessTestCount must be {EXPECTED_UNIT_HARNESS_TESTS}")

    artifact_hashes = report.get("artifactSha256")
    if not isinstance(artifact_hashes, dict):
        failures.append("artifactSha256 must contain the owned artifact hash set")
    else:
        if len(artifact_hashes) != EXPECTED_ARTIFACT_HASHES:
            failures.append(f"artifactSha256 must contain exactly {EXPECTED_ARTIFACT_HASHES} owned artifact hashes")
        if report.get("artifactHashCount") != len(artifact_hashes):
            failures.append("artifactHashCount must match artifactSha256 length")
        if report.get("artifactHashCount") != EXPECTED_ARTIFACT_HASHES:
            failures.append(f"artifactHashCount must be {EXPECTED_ARTIFACT_HASHES}")

    if environment_probe.get("schema") != "hecton8.hardware_adaptive_ui_scaler.unity_environment_probe.v1":
        failures.append("environment probe schema mismatch")
    if environment_probe.get("requiredUnityVersion") != "6000.4.1f1":
        failures.append("environment probe must require Unity 6000.4.1f1")
    if environment_probe.get("runtimeVerificationStatus") != "PENDING_UNITY_VERIFICATION":
        failures.append("environment probe runtime status must stay pending")
    if environment_probe.get("status") not in ALLOWED_UNITY_PROBE_STATUSES:
        failures.append("environment probe status is invalid")

    return failures


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--report", default=str(DEFAULT_REPORT), help="Aggregate report path.")
    parser.add_argument("--environment-probe", default=str(DEFAULT_ENVIRONMENT_PROBE), help="Unity environment probe report path.")
    args = parser.parse_args()

    report_path = Path(args.report)
    if not report_path.is_absolute():
        report_path = ROOT / report_path
    probe_path = Path(args.environment_probe)
    if not probe_path.is_absolute():
        probe_path = ROOT / probe_path

    failures = validate_aggregate_report(_load_json(report_path), _load_json(probe_path))
    if failures:
        for failure in failures:
            print(failure)
        return 1

    print("Aggregate report validation PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
