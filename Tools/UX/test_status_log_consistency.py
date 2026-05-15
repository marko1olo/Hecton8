#!/usr/bin/env python3
"""Unit tests for UX status/log consistency validation."""

from __future__ import annotations

import copy
import json
import unittest
from pathlib import Path

from Tools.UX.validate_status_log_consistency import validate_status_log_consistency


ROOT = Path(__file__).resolve().parents[2]
STATUS_PATH = ROOT / "Docs/Tasks/Status_UX_ENGINEER.md"
RATIONALE_PATH = ROOT / "Docs/AgentLogs/Rationale_UX_ENGINEER.md"
LOG_PATH = ROOT / "Docs/AgentLogs/LOG_UX_ENGINEER.md"
BLOCKER_PATH = ROOT / "Docs/AgentLogs/Blocker_UX_ENGINEER.md"
AGGREGATE_PATH = ROOT / "Docs/AgentLogs/UI_HardwareAdaptiveValidation_UX_ENGINEER.json"


def _current_inputs() -> tuple[str, str, str, str, dict]:
    return (
        STATUS_PATH.read_text(encoding="utf-8"),
        RATIONALE_PATH.read_text(encoding="utf-8"),
        LOG_PATH.read_text(encoding="utf-8"),
        BLOCKER_PATH.read_text(encoding="utf-8"),
        json.loads(AGGREGATE_PATH.read_text(encoding="utf-8")),
    )


class StatusLogConsistencyTests(unittest.TestCase):
    def test_current_status_logs_are_consistent(self) -> None:
        self.assertEqual([], validate_status_log_consistency(*_current_inputs()))

    def test_rejects_missing_checked_task(self) -> None:
        status, rationale, log, blocker, aggregate = _current_inputs()
        status = status.replace("- [x] Task 7", "- [ ] Task 7", 1)

        failures = validate_status_log_consistency(status, rationale, log, blocker, aggregate)

        self.assertTrue(any("checked task set" in failure for failure in failures))

    def test_rejects_runtime_promotion_in_aggregate(self) -> None:
        status, rationale, log, blocker, aggregate = _current_inputs()
        promoted = copy.deepcopy(aggregate)
        promoted["unityRuntimeStatus"] = "PASS"

        failures = validate_status_log_consistency(status, rationale, log, blocker, promoted)

        self.assertIn("aggregate unityRuntimeStatus must remain pending", failures)


if __name__ == "__main__":
    unittest.main()
