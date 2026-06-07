#!/usr/bin/env python3
"""Unit tests for UX status/log consistency validation."""

from __future__ import annotations

import copy
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from Tools.UX.validate_status_log_consistency import validate_status_log_consistency


def _valid_inputs() -> tuple[str, str, str, str, dict]:
    status = "\n".join(
        (
            "Prompt ID: HARDWARE_ADAPTIVE_UI_BAKER",
            "Domain: PRESENTATION & UX",
            "Task Count: 7",
            "Status: UI SCALED",
            "Unity: PENDING_UNITY_VERIFICATION",
            "- [x] Task 1 - static validation",
            "- [x] Task 2 - static validation",
            "- [x] Task 3 - static validation",
            "- [x] Task 4 - static validation",
            "- [x] Task 5 - static validation",
            "- [x] Task 6 - static validation",
            "- [x] Task 7 - static validation",
        )
    )
    rationale = "HARDWARE_ADAPTIVE_UI_BAKER\nPENDING_UNITY_VERIFICATION retained."
    log = "HARDWARE_ADAPTIVE_UI_BAKER\nPENDING static-only validation."
    blocker = "HARDWARE_ADAPTIVE_UI_BAKER\nUNITY_NOT_FOUND / PENDING runtime proof."
    aggregate = {
        "status": "PASS",
        "unityRuntimeStatus": "PENDING_UNITY_VERIFICATION",
        "aggregateSelfValidation": {"status": "PASS", "failures": []},
        "statusLogSelfValidation": {"status": "PASS", "failures": []},
    }

    return status, rationale, log, blocker, aggregate


class StatusLogConsistencyTests(unittest.TestCase):
    def test_valid_status_log_fixture_is_consistent(self) -> None:
        self.assertEqual([], validate_status_log_consistency(*_valid_inputs()))

    def test_rejects_missing_checked_task(self) -> None:
        status, rationale, log, blocker, aggregate = _valid_inputs()
        status = status.replace("- [x] Task 7", "- [ ] Task 7", 1)

        failures = validate_status_log_consistency(status, rationale, log, blocker, aggregate)

        self.assertTrue(any("checked task set" in failure for failure in failures))

    def test_rejects_runtime_promotion_in_aggregate(self) -> None:
        status, rationale, log, blocker, aggregate = _valid_inputs()
        promoted = copy.deepcopy(aggregate)
        promoted["unityRuntimeStatus"] = "PASS"

        failures = validate_status_log_consistency(status, rationale, log, blocker, promoted)

        self.assertIn("aggregate unityRuntimeStatus must remain pending", failures)

    def test_rejects_aggregate_self_validation_failures(self) -> None:
        status, rationale, log, blocker, aggregate = _valid_inputs()
        aggregate["aggregateSelfValidation"] = {"status": "PASS", "failures": ["forced failure"]}

        failures = validate_status_log_consistency(status, rationale, log, blocker, aggregate)

        self.assertIn("aggregate self-validation must be PASS with no failures", failures)

    def test_rejects_status_log_self_validation_failure(self) -> None:
        status, rationale, log, blocker, aggregate = _valid_inputs()
        aggregate["statusLogSelfValidation"] = {"status": "FAIL", "failures": []}

        failures = validate_status_log_consistency(status, rationale, log, blocker, aggregate)

        self.assertIn("status/log self-validation must be PASS with no failures", failures)


if __name__ == "__main__":
    unittest.main()
