#!/usr/bin/env python3
"""Unit tests for the H8 system hardware profile guard."""

from __future__ import annotations

import copy
import sys
import unittest
from pathlib import Path


SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

import ValidateSystemHardwareProfiles as guard  # noqa: E402


class SystemHardwareProfileGuardTests(unittest.TestCase):
    def setUp(self) -> None:
        self.data = copy.deepcopy(guard.load_catalog())

    def assert_error_contains(self, data: dict, needle: str) -> None:
        errors, _report = guard.validate_data(data)
        self.assertTrue(errors, "expected validation errors")
        self.assertTrue(any(needle in error for error in errors), errors)

    def test_actual_profile_catalog_passes_static_guard(self) -> None:
        errors, report = guard.validate_data(self.data)
        self.assertEqual([], errors)
        self.assertEqual("PASS", report["status"])
        self.assertEqual(4096, report["quest2TotalCommittedPlusReserveMb"])
        self.assertEqual(0, report["hotPathImpactMicroseconds"])

    def test_quest2_over_4gb_budget_fails(self) -> None:
        quest2 = self.data["profiles"][2]
        quest2["SystemRamBudget"] = 4096
        quest2["SystemRamSafetyReserve"] = 1
        self.assert_error_contains(self.data, "Quest2_Low total committed RAM exceeds 4GB")

    def test_profile_table_hash_drift_fails(self) -> None:
        self.data["profileTable"]["profileStableHash32"][0] = 0
        self.assert_error_contains(self.data, "profileTable.profileStableHash32[0] parity drift")

    def test_profile_table_render_scale_parity_fails(self) -> None:
        self.data["profileTable"]["profileRenderScaleMilli"][2] = 999
        self.assert_error_contains(self.data, "profileTable.profileRenderScaleMilli[2] parity drift")

    def test_shi_threshold_order_fails(self) -> None:
        quest2 = self.data["profiles"][2]
        quest2["SHIThresholds"]["CriticalSystemStress"] = 0.50
        self.assert_error_contains(self.data, "Quest2_Low SHI thresholds not monotonic")

    def test_stress_weights_sum_fails(self) -> None:
        self.data["systemHealthIndex"]["stressModel"]["CpuLaneDebtRatioWeight"] = 0.20
        self.assert_error_contains(self.data, "stress model weights sum")


if __name__ == "__main__":
    unittest.main()
