#!/usr/bin/env python3
"""Audit a Unity import/compile log for UX hardware-adaptive UI verification."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path
from typing import Iterable


DEFAULT_LOG_PATH = Path("Docs/AgentLogs/UnityImport_UX_ENGINEER.log")
DEFAULT_REPORT_PATH = Path("Docs/AgentLogs/UI_UnityCompileLogAudit_UX_ENGINEER.json")

FAILURE_PATTERNS = (
    re.compile(r"\berror\s+CS\d+\s*:", re.IGNORECASE),
    re.compile(r"\bShader error\b", re.IGNORECASE),
    re.compile(r"\bCompiler errors?\b", re.IGNORECASE),
    re.compile(r"\bScript compilation failed\b", re.IGNORECASE),
    re.compile(r"\bRefresh completed with errors\b", re.IGNORECASE),
    re.compile(r"\bAborting batchmode due to failure\b", re.IGNORECASE),
    re.compile(r"\bBuild completed with a result of ['\"]?Failed['\"]?", re.IGNORECASE),
)

WARNING_PATTERNS = (
    re.compile(r"\bwarning\s+CS\d+\s*:", re.IGNORECASE),
    re.compile(r"\bShader warning\b", re.IGNORECASE),
)


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(65536), b""):
            digest.update(block)
    return digest.hexdigest()


def _collect_matches(lines: Iterable[str], patterns: tuple[re.Pattern[str], ...], limit: int) -> list[str]:
    matches: list[str] = []
    for line in lines:
        if any(pattern.search(line) for pattern in patterns):
            matches.append(line.strip())
            if len(matches) >= limit:
                break
    return matches


def audit_log_text(log_text: str, match_limit: int = 20) -> dict[str, object]:
    """Return a deterministic pass/fail summary for Unity compile/import text."""

    lines = log_text.splitlines()
    failures = _collect_matches(lines, FAILURE_PATTERNS, match_limit)
    warnings = _collect_matches(lines, WARNING_PATTERNS, match_limit)
    return {
        "status": "FAIL" if failures else "PASS",
        "failureCountCapped": len(failures),
        "warningCountCapped": len(warnings),
        "failures": failures,
        "warnings": warnings,
    }


def _read_log_text(log_path: Path) -> str:
    data = log_path.read_bytes()
    if data.startswith(b"\xff\xfe") or data.startswith(b"\xfe\xff"):
        return data.decode("utf-16", errors="replace")

    return data.decode("utf-8-sig", errors="replace")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--log", default=str(DEFAULT_LOG_PATH), help="Unity batchmode/import log to audit.")
    parser.add_argument("--write-report", action="store_true", help="Write the compile-log audit JSON report.")
    parser.add_argument("--report", default=str(DEFAULT_REPORT_PATH), help="Output report path.")
    args = parser.parse_args()

    log_path = Path(args.log)
    if not log_path.exists():
        report = {
            "schema": "hecton8.hardware_adaptive_ui_scaler.unity_compile_log_audit.v1",
            "owner": "UX_ENGINEER",
            "promptId": "HARDWARE_ADAPTIVE_UI_BAKER",
            "status": "MISSING_LOG",
            "log": str(log_path),
            "failures": [f"Unity log not found: {log_path}"],
        }
        if args.write_report:
            report_path = Path(args.report)
            report_path.parent.mkdir(parents=True, exist_ok=True)
            report_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
        print(report["failures"][0])
        return 2

    log_text = _read_log_text(log_path)
    audit = audit_log_text(log_text)
    report = {
        "schema": "hecton8.hardware_adaptive_ui_scaler.unity_compile_log_audit.v1",
        "owner": "UX_ENGINEER",
        "promptId": "HARDWARE_ADAPTIVE_UI_BAKER",
        "status": audit["status"],
        "log": str(log_path),
        "logSha256": _sha256(log_path),
        "audit": audit,
    }

    if args.write_report:
        report_path = Path(args.report)
        report_path.parent.mkdir(parents=True, exist_ok=True)
        report_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")

    if audit["status"] != "PASS":
        print("Unity compile log audit FAIL")
        return 1

    print("Unity compile log audit PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
