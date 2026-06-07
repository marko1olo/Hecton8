#!/usr/bin/env python3
"""Validate UX agent status/log files stay consistent with the batch result."""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
STATUS_PATH = ROOT / "Docs/Tasks/Status_UX_ENGINEER.md"
RATIONALE_PATH = ROOT / "Docs/AgentLogs/Rationale_UX_ENGINEER.md"
LOG_PATH = ROOT / "Docs/AgentLogs/LOG_UX_ENGINEER.md"
BLOCKER_PATH = ROOT / "Docs/AgentLogs/Blocker_UX_ENGINEER.md"
AGGREGATE_PATH = ROOT / "Docs/AgentLogs/UI_HardwareAdaptiveValidation_UX_ENGINEER.json"

CHECKED_TASK_PATTERN = re.compile(r"^- \[x\] Task\s+(\d+)\s+-", re.MULTILINE)


def _self_validation_failed(validation: object) -> bool:
    if not isinstance(validation, dict):
        return True

    return validation.get("status") != "PASS" or validation.get("failures") not in ([], None)


def validate_status_log_consistency(
    status_text: str,
    rationale_text: str,
    log_text: str,
    blocker_text: str,
    aggregate_report: dict,
) -> list[str]:
    """Return consistency failures for UX status/log artifacts."""

    failures: list[str] = []

    required_status_tokens = (
        "Prompt ID: HARDWARE_ADAPTIVE_UI_BAKER",
        "Domain: PRESENTATION & UX",
        "Task Count: 7",
        "Status: UI SCALED",
    )
    for token in required_status_tokens:
        if token not in status_text:
            failures.append(f"status missing token: {token}")

    checked_tasks = sorted(int(match) for match in CHECKED_TASK_PATTERN.findall(status_text))
    if checked_tasks != [1, 2, 3, 4, 5, 6, 7]:
        failures.append(f"status checked task set invalid: {checked_tasks}")

    if "PENDING_UNITY_VERIFICATION" not in status_text and "UNITY PENDING" not in status_text:
        failures.append("status must retain Unity pending boundary")

    for name, text in (
        ("rationale", rationale_text),
        ("log", log_text),
        ("blocker", blocker_text),
    ):
        if "HARDWARE_ADAPTIVE_UI_BAKER" not in text:
            failures.append(f"{name} missing prompt id")
        if "PENDING" not in text and "UNITY_NOT_FOUND" not in text:
            failures.append(f"{name} missing pending/runtime boundary")

    if aggregate_report.get("status") != "PASS":
        failures.append("aggregate report status must be PASS")
    if aggregate_report.get("unityRuntimeStatus") != "PENDING_UNITY_VERIFICATION":
        failures.append("aggregate unityRuntimeStatus must remain pending")
    if _self_validation_failed(aggregate_report.get("aggregateSelfValidation")):
        failures.append("aggregate self-validation must be PASS with no failures")
    if _self_validation_failed(aggregate_report.get("statusLogSelfValidation")):
        failures.append("status/log self-validation must be PASS with no failures")

    return failures


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write-report", action="store_true", help="Write status/log consistency report JSON.")
    parser.add_argument(
        "--report",
        default=str(ROOT / "Docs/AgentLogs/UI_StatusLogConsistency_UX_ENGINEER.json"),
        help="Output report path.",
    )
    args = parser.parse_args()

    failures = validate_status_log_consistency(
        STATUS_PATH.read_text(encoding="utf-8-sig"),
        RATIONALE_PATH.read_text(encoding="utf-8-sig"),
        LOG_PATH.read_text(encoding="utf-8-sig"),
        BLOCKER_PATH.read_text(encoding="utf-8-sig"),
        json.loads(AGGREGATE_PATH.read_text(encoding="utf-8-sig")),
    )

    report = {
        "schema": "hecton8.hardware_adaptive_ui_scaler.status_log_consistency.v1",
        "owner": "UX_ENGINEER",
        "promptId": "HARDWARE_ADAPTIVE_UI_BAKER",
        "status": "PASS" if not failures else "FAIL",
        "failures": failures,
    }

    if args.write_report:
        report_path = Path(args.report)
        if not report_path.is_absolute():
            report_path = ROOT / report_path
        report_path.parent.mkdir(parents=True, exist_ok=True)
        report_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")

    if failures:
        for failure in failures:
            print(failure)
        return 1

    print("Status/log consistency PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
