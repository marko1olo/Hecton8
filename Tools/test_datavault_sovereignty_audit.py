#!/usr/bin/env python3
"""Unit tests for DataVault sovereignty regression drilldown."""

from __future__ import annotations

import unittest
from pathlib import Path

from DataVaultSovereigntyAudit import (
    BASELINE_SCHEMA,
    aggregate_regression_details,
    build_report_payload,
    collect_regression_details,
    extract_domain,
)


def make_payload() -> dict:
    return {
        "schema": "hecton8.datavault_sovereignty_audit.v2",
        "sourceRoot": "Assets/_Project/Scripts",
        "pattern": "new NativeArray",
        "declarationPattern": "NativeArray field",
        "allowedPathSuffixes": [],
        "declarationAllowedPathSuffixes": [],
        "totalDirectConstructors": 2,
        "allowedDirectConstructors": 0,
        "forbiddenDirectConstructors": 2,
        "forbiddenFileCount": 1,
        "totalNativeArrayDeclarations": 4,
        "allowedNativeArrayDeclarations": 0,
        "forbiddenNativeArrayDeclarations": 4,
        "declarationFileCount": 2,
        "findingCount": 1,
        "findings": [
            {
                "path": "Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs",
                "count": 2,
                "lines": [10, 20],
                "allowed": False,
            }
        ],
        "declarationFindings": [
            {
                "path": "Assets/_Project/Scripts/Construction/SumpPumpPipeGridJobs.cs",
                "count": 3,
                "lines": [30, 31, 32],
                "allowed": False,
            },
            {
                "path": "Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs",
                "count": 1,
                "lines": [40],
                "allowed": False,
            },
        ],
    }


class DataVaultRegressionDrilldownTests(unittest.TestCase):
    def test_extract_domain_handles_root_and_nested_paths(self) -> None:
        self.assertEqual(extract_domain("Assets/_Project/Scripts/PlayerInventory.cs"), "Root")
        self.assertEqual(
            extract_domain("Assets/_Project/Scripts/Construction/SumpPumpPipeGridJobs.cs"),
            "Construction",
        )
        self.assertEqual(extract_domain("Packages/Vendor/File.cs"), "External")

    def test_collect_regression_details_groups_exact_file_deltas(self) -> None:
        payload = make_payload()
        baseline = {
            "schema": BASELINE_SCHEMA,
            "forbiddenDirectConstructors": 1,
            "forbiddenNativeArrayDeclarations": 2,
            "forbiddenByFile": {
                "Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs": 1,
            },
            "forbiddenDeclarationsByFile": {
                "Assets/_Project/Scripts/Construction/SumpPumpPipeGridJobs.cs": 1,
            },
        }

        errors, details = collect_regression_details(payload, baseline)
        by_domain = aggregate_regression_details(details)

        self.assertTrue(any("Forbidden direct NativeArray constructors increased" in e for e in errors))
        self.assertEqual(len(details), 3)
        self.assertEqual(by_domain[0]["domain"], "Construction")
        self.assertEqual(by_domain[0]["fieldDeclarationDelta"], 2)
        self.assertEqual(by_domain[1]["domain"], "Core")
        self.assertEqual(by_domain[2]["domain"], "World")

    def test_build_report_payload_keeps_machine_readable_regression_data(self) -> None:
        payload = make_payload()
        baseline = {
            "schema": BASELINE_SCHEMA,
            "forbiddenDirectConstructors": 2,
            "forbiddenNativeArrayDeclarations": 3,
            "forbiddenByFile": {
                "Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs": 2,
            },
            "forbiddenDeclarationsByFile": {
                "Assets/_Project/Scripts/Construction/SumpPumpPipeGridJobs.cs": 3,
            },
        }

        errors, details = collect_regression_details(payload, baseline)
        report = build_report_payload(payload, Path("baseline.json"), baseline, errors, details)

        self.assertEqual(report["schema"], "hecton8.datavault_sovereignty_audit_report.v1")
        self.assertEqual(report["regressionDetails"][0]["domain"], "Core")
        self.assertEqual(report["regressionByDomain"][0]["domain"], "Core")

    def test_missing_baseline_fails_closed_without_fake_details(self) -> None:
        errors, details = collect_regression_details(make_payload(), None)

        self.assertEqual(errors, ["Baseline missing; no-regression gate fails closed."])
        self.assertEqual(details, [])


if __name__ == "__main__":
    unittest.main()
