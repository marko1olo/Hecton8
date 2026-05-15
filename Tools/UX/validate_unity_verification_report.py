#!/usr/bin/env python3
"""Validate the filled UX Unity verification report.

The report may stay pending while Unity evidence is missing. It may only be
marked PASS when every required Unity check is PASS and has an evidence path.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any


REQUIRED_CHECKS = {
    "UNITY_IMPORT",
    "GC_STEADY_STATE",
    "FRAME_DEBUGGER_SHADER_SAMPLES",
    "QUEST2_FOV_LAYOUT",
    "QUEST3_FOV_LAYOUT",
    "O2_LOW_READABILITY_CAPTURE",
    "MX350_LOW_TIER_CAPTURE",
    "GOD_MODE_CAPTURE",
}

ALLOWED_PENDING_STATUSES = {"PENDING", "FAIL", "BLOCKED"}


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(65536), b""):
            digest.update(block)
    return digest.hexdigest()


def _check_failures(report: dict[str, Any]) -> list[str]:
    failures: list[str] = []

    if report.get("owner") != "UX_ENGINEER":
        failures.append("owner must be UX_ENGINEER")

    if report.get("promptId") != "HARDWARE_ADAPTIVE_UI_BAKER":
        failures.append("promptId must be HARDWARE_ADAPTIVE_UI_BAKER")

    top_status = report.get("status")
    if top_status not in {"PENDING_UNITY_VERIFICATION", "PASS", "FAIL", "BLOCKED"}:
        failures.append("top-level status must be PENDING_UNITY_VERIFICATION, PASS, FAIL, or BLOCKED")

    checks = report.get("checks")
    if not isinstance(checks, list):
        failures.append("checks must be a list")
        return failures

    seen: set[str] = set()
    pass_ready = top_status == "PASS"
    for index, check in enumerate(checks):
        if not isinstance(check, dict):
            failures.append(f"checks[{index}] must be an object")
            continue

        check_id = check.get("id")
        if not isinstance(check_id, str) or not check_id:
            failures.append(f"checks[{index}].id must be non-empty")
            continue

        if check_id in seen:
            failures.append(f"duplicate check id: {check_id}")
        seen.add(check_id)

        if check.get("required") is not True:
            failures.append(f"{check_id}: required must be true")

        status = check.get("status")
        evidence_path = check.get("evidencePath")
        if not isinstance(status, str):
            failures.append(f"{check_id}: status must be a string")
            continue

        if not isinstance(evidence_path, str):
            failures.append(f"{check_id}: evidencePath must be a string")
            continue

        if pass_ready:
            if status != "PASS":
                failures.append(f"{check_id}: top-level PASS requires check status PASS")
            if not evidence_path.strip():
                failures.append(f"{check_id}: top-level PASS requires evidencePath")
        else:
            if status == "PASS" and not evidence_path.strip():
                failures.append(f"{check_id}: PASS check requires evidencePath")
            if status not in ALLOWED_PENDING_STATUSES and status != "PASS":
                failures.append(f"{check_id}: invalid status {status}")

    missing = REQUIRED_CHECKS.difference(seen)
    extra = seen.difference(REQUIRED_CHECKS)
    if missing:
        failures.append("missing required checks: " + ", ".join(sorted(missing)))
    if extra:
        failures.append("unexpected checks: " + ", ".join(sorted(extra)))

    if top_status == "PASS" and failures:
        failures.append("runtime PASS rejected because required Unity evidence is incomplete")

    return failures


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--report",
        default="Docs/AgentLogs/UnityVerification_UX_ENGINEER.json",
        help="Filled Unity verification report to validate.",
    )
    parser.add_argument(
        "--write-audit",
        action="store_true",
        help="Write Docs/AgentLogs/UI_UnityReportAudit_UX_ENGINEER.json.",
    )
    args = parser.parse_args()

    report_path = Path(args.report)
    report = json.loads(report_path.read_text(encoding="utf-8"))
    failures = _check_failures(report)

    audit = {
        "schema": "hecton8.hardware_adaptive_ui_scaler.unity_report_audit.v1",
        "owner": "UX_ENGINEER",
        "promptId": "HARDWARE_ADAPTIVE_UI_BAKER",
        "status": "PASS" if not failures else "FAIL",
        "report": str(report_path),
        "reportSha256": _sha256(report_path),
        "requiredChecks": sorted(REQUIRED_CHECKS),
        "failures": failures,
    }

    if args.write_audit:
        audit_path = Path("Docs/AgentLogs/UI_UnityReportAudit_UX_ENGINEER.json")
        audit_path.parent.mkdir(parents=True, exist_ok=True)
        audit_path.write_text(json.dumps(audit, indent=2) + "\n", encoding="utf-8")

    if failures:
        for failure in failures:
            print(failure)
        return 1

    print("Unity verification report audit PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
