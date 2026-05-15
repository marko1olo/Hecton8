#!/usr/bin/env python3
"""Unit tests for the aggregate hardware profile guard."""

from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

import ValidateAllHardwareProfiles as all_guard  # noqa: E402


class AllHardwareProfileGuardTests(unittest.TestCase):
    def test_aggregate_guard_passes_static_catalogs(self) -> None:
        errors, report = all_guard.run_guards()
        self.assertEqual([], errors)
        self.assertEqual("PASS", report["status"])
        self.assertEqual(2, report["runtimeCatalog"]["profiles"])
        self.assertEqual(4, report["systemProfile"]["profileCount"])
        self.assertEqual(4096, report["systemProfile"]["quest2TotalCommittedPlusReserveMb"])
        self.assertEqual(0, report["systemProfile"]["hotPathImpactMicroseconds"])

    def test_aggregate_report_round_trips(self) -> None:
        errors, report = all_guard.run_guards()
        self.assertEqual([], errors)
        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "aggregate_report.json"
            all_guard.write_report(path, report)
            check_errors: list[str] = []
            all_guard.check_report(path, report, check_errors)
        self.assertEqual([], check_errors)

    def test_missing_aggregate_report_fails_check(self) -> None:
        errors, report = all_guard.run_guards()
        self.assertEqual([], errors)
        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "missing_report.json"
            check_errors: list[str] = []
            all_guard.check_report(path, report, check_errors)
        self.assertEqual(1, len(check_errors))
        self.assertIn("missing aggregate report", check_errors[0])


if __name__ == "__main__":
    unittest.main()
