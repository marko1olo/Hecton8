#!/usr/bin/env python3
"""Safely update one check in the UX Unity verification report."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from Tools.UX.validate_unity_verification_report import REQUIRED_CHECKS, _check_failures


REPORT_PATH = ROOT / "Docs/AgentLogs/UnityVerification_UX_ENGINEER.json"
AUDIT_PATH = ROOT / "Docs/AgentLogs/UI_UnityReportAudit_UX_ENGINEER.json"

ALLOWED_STATUSES = {"PENDING", "PASS", "FAIL", "BLOCKED"}


def _find_check(report: dict[str, Any], check_id: str) -> dict[str, Any]:
    checks = report.get("checks")
    if not isinstance(checks, list):
        raise ValueError("report checks must be a list")

    for check in checks:
        if isinstance(check, dict) and check.get("id") == check_id:
            return check

    raise ValueError(f"check not found: {check_id}")


def _write_audit(report_path: Path, report: dict[str, Any]) -> None:
    failures = _check_failures(report)
    audit = {
        "schema": "hecton8.hardware_adaptive_ui_scaler.unity_report_update_audit.v1",
        "owner": "UX_ENGINEER",
        "promptId": "HARDWARE_ADAPTIVE_UI_BAKER",
        "status": "PASS" if not failures else "FAIL",
        "report": str(report_path),
        "failures": failures,
    }
    AUDIT_PATH.parent.mkdir(parents=True, exist_ok=True)
    AUDIT_PATH.write_text(json.dumps(audit, indent=2) + "\n", encoding="utf-8")


def _resolve_path(path_text: str) -> Path:
    path = Path(path_text)
    if path.is_absolute():
        return path
    return ROOT / path


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--report", default=str(REPORT_PATH), help="Unity verification report to update.")
    parser.add_argument("--check", required=True, choices=sorted(REQUIRED_CHECKS), help="Check ID to update.")
    parser.add_argument("--status", required=True, choices=sorted(ALLOWED_STATUSES), help="New check status.")
    parser.add_argument("--evidence", default="", help="Evidence path. Required for PASS.")
    parser.add_argument("--actual", default="", help="Short factual note recorded on the check.")
    parser.add_argument("--top-status", default="", choices=("", "PENDING_UNITY_VERIFICATION", "PASS", "FAIL", "BLOCKED"), help="Optional top-level report status.")
    parser.add_argument("--write-audit", action="store_true", help="Write report update audit JSON.")
    args = parser.parse_args()

    report_path = _resolve_path(args.report)
    report = json.loads(report_path.read_text(encoding="utf-8"))

    evidence = args.evidence.strip()
    evidence_path = _resolve_path(evidence) if evidence else None
    if args.status == "PASS":
        if not evidence:
            raise SystemExit("PASS requires --evidence")
        if evidence_path is None or not evidence_path.exists():
            raise SystemExit(f"PASS evidence path does not exist: {evidence}")

    check = _find_check(report, args.check)
    check["status"] = args.status
    check["evidencePath"] = evidence
    if args.actual:
        check["actual"] = args.actual

    if args.top_status:
        report["status"] = args.top_status

    failures = _check_failures(report)
    if report.get("status") == "PASS" and failures:
        raise SystemExit("top-level PASS rejected: " + "; ".join(failures))

    report_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if args.write_audit:
        _write_audit(report_path, report)

    print(f"Updated {args.check} -> {args.status}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
