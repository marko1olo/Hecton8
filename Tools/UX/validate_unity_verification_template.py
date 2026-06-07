#!/usr/bin/env python3
"""Validate the UX Unity verification template stays evidence-locked.

This script is intentionally static. It verifies that the template requires
Unity-side proof and has not been edited into a fake pass report.
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


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(65536), b""):
            digest.update(block)
    return digest.hexdigest()


def _failures(template: dict[str, Any]) -> list[str]:
    failures: list[str] = []

    if template.get("status") != "PENDING_UNITY_VERIFICATION":
        failures.append("template status must remain PENDING_UNITY_VERIFICATION")

    if template.get("owner") != "UX_ENGINEER":
        failures.append("owner must be UX_ENGINEER")

    if template.get("promptId") != "HARDWARE_ADAPTIVE_UI_BAKER":
        failures.append("promptId must be HARDWARE_ADAPTIVE_UI_BAKER")

    checks = template.get("checks")
    if not isinstance(checks, list):
        failures.append("checks must be a list")
        return failures

    seen: set[str] = set()
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

        if check.get("status") != "PENDING":
            failures.append(f"{check_id}: template status must be PENDING")

        if check.get("evidencePath") != "":
            failures.append(f"{check_id}: evidencePath must be empty in the template")

        expected = check.get("expected")
        if not isinstance(expected, str) or len(expected.strip()) < 20:
            failures.append(f"{check_id}: expected proof text is missing or too short")

    missing = REQUIRED_CHECKS.difference(seen)
    extra = seen.difference(REQUIRED_CHECKS)
    if missing:
        failures.append("missing required checks: " + ", ".join(sorted(missing)))
    if extra:
        failures.append("unexpected checks: " + ", ".join(sorted(extra)))

    acceptance_rule = template.get("acceptanceRule")
    if not isinstance(acceptance_rule, str) or "evidencePath" not in acceptance_rule:
        failures.append("acceptanceRule must explicitly require evidencePath")

    notes = template.get("notes")
    if not isinstance(notes, list) or not any("Do not edit this template" in str(note) for note in notes):
        failures.append("notes must forbid editing the template into a pass report")

    return failures


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--template",
        default="Docs/Design/HardwareAdaptiveUIScaler_UnityVerificationTemplate.json",
        help="Unity evidence template to validate.",
    )
    parser.add_argument(
        "--write-report",
        action="store_true",
        help="Write Docs/AgentLogs/UI_UnityTemplateAudit_UX_ENGINEER.json.",
    )
    args = parser.parse_args()

    template_path = Path(args.template)
    template = json.loads(template_path.read_text(encoding="utf-8-sig"))
    failures = _failures(template)

    report = {
        "schema": "hecton8.hardware_adaptive_ui_scaler.unity_template_audit.v1",
        "owner": "UX_ENGINEER",
        "promptId": "HARDWARE_ADAPTIVE_UI_BAKER",
        "status": "PASS" if not failures else "FAIL",
        "template": str(template_path),
        "templateSha256": _sha256(template_path),
        "requiredChecks": sorted(REQUIRED_CHECKS),
        "failures": failures,
    }

    if args.write_report:
        report_path = Path("Docs/AgentLogs/UI_UnityTemplateAudit_UX_ENGINEER.json")
        report_path.parent.mkdir(parents=True, exist_ok=True)
        report_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")

    if failures:
        for failure in failures:
            print(failure)
        return 1

    print("Unity verification template audit PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
