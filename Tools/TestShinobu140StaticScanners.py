#!/usr/bin/env python3
from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from H8VerifyCore import parse_xml_safe

import argparse
import json
import tempfile
import defusedxml.ElementTree as ET
from datetime import datetime
from pathlib import Path

import RunShinobu140StaticScanners as scanner


def assert_true(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def scanner_by_name(results: list[scanner.ScanResult], name: str) -> scanner.ScanResult:
    for result in results:
        if result.scanner == name:
            return result
    raise AssertionError(f"Missing scanner result: {name}")


def test_regression_attribution_excludes_self_gate() -> None:
    with tempfile.TemporaryDirectory(prefix="shinobu140_regression_") as temp_dir:
        baseline_path = Path(temp_dir) / "baseline.json"
        baseline_path.write_text(
            json.dumps(
                {
                    "scanners": {
                        "Burst_Job_Directives": {"criticalCount": 0, "warningCount": 0},
                    }
                }
            ),
            encoding="utf-8",
        )

        burst = scanner.ScanResult("Burst_Job_Directives")
        burst.files_scanned = 1
        burst.add("Assets/_Project/Scripts/World/FakeBurstDebt.cs", 12, "BURST_FLAGS", "fixture", 2)

        self_gate = scanner.ScanResult("Static_Gate_Regression")
        self_gate.files_scanned = 1
        self_gate.add(baseline_path, 1, "STATIC_GATE_CRITICAL_REGRESSION", "fixture", 2)

        attribution = scanner.build_regression_attribution([burst, self_gate], baseline_path)
        regressions = attribution["regressions"]
        assert_true(len(regressions) == 1, "Static_Gate_Regression must not attribute itself.")
        assert_true(regressions[0]["scanner"] == "Burst_Job_Directives", "Burst regression must be preserved.")
        assert_true(regressions[0]["criticalDelta"] == 1, "Burst critical delta must be exact.")


def test_core_violation_fixture_trips_scanners() -> None:
    source = """
using Unity.Burst;
using Unity.Jobs;

public sealed class Shinobu140Fixture
{
    public void Tick(float dt)
    {
        var service = GlobalRegistry.DataVault;
        var handle = default(JobHandle);
        handle.Complete();
        ResolveLiveService();
        CompleteHiddenJob();
    }

    private void ResolveLiveService()
    {
        var hidden = GlobalRegistry.Audio;
    }

    private void CompleteHiddenJob()
    {
        var hiddenHandle = default(JobHandle);
        hiddenHandle.Complete();
    }
}

public struct MissingBurstFixtureJob : IJob
{
    public void Execute() { }
}

[BurstCompile]
public struct IncompleteBurstFixtureJob : IJob
{
    public void Execute() { }
}

public struct RuntimePayloadFixture
{
    public int Value { get; set; }
}
"""
    with tempfile.TemporaryDirectory(prefix="shinobu140_fixture_") as temp_dir:
        fixture_path = Path(temp_dir) / "Shinobu140ScannerFixture.cs"
        fixture_path.write_text(source, encoding="utf-8")
        results = scanner.run_combined_cs_scans([fixture_path])

    hot_registry = scanner_by_name(results, "Hot_Registry_Polling")
    hot_helper_registry = scanner_by_name(results, "Hot_Helper_Registry_Polling")
    mid_complete = scanner_by_name(results, "Mid_Frame_Complete")
    hot_helper_complete = scanner_by_name(results, "Hot_Helper_Complete")
    burst = scanner_by_name(results, "Burst_Job_Directives")
    layout = scanner_by_name(results, "Runtime_Struct_Layout")

    assert_true(hot_registry.critical_count >= 1, "Hot GlobalRegistry poll fixture was not detected.")
    assert_true(hot_helper_registry.critical_count >= 1, "Hot helper GlobalRegistry poll fixture was not detected.")
    assert_true(mid_complete.critical_count >= 1, "Mid-frame JobHandle.Complete fixture was not detected.")
    assert_true(hot_helper_complete.critical_count >= 1, "Hot helper JobHandle.Complete fixture was not detected.")
    assert_true(burst.critical_count >= 2, "Missing/incomplete Burst directive fixtures were not detected.")
    assert_true(layout.critical_count >= 1, "Struct property defensive-copy fixture was not detected.")


def test_live_self_audit_contract_stays_parseable() -> None:
    result = scanner.scan_self_audit()
    assert_true(result.critical_count == 0, "SHINOBU_140 self-audit XML contract is invalid.")


def test_live_self_audit_counts_match_static_summary() -> None:
    summary = scanner.load_json_object(scanner.path("Docs/Reports/SHINOBU_140_STATIC_GATE_SUMMARY.json"))
    assert_true(bool(summary), "Static gate summary JSON is missing or invalid.")
    root = parse_xml_safe(scanner.path("Docs/Reports/SHINOBU_140_SELF_AUDIT.xml")).getroot()
    hygiene = root.find("Hygiene")
    assert_true(hygiene is not None, "Self-audit Hygiene row is missing.")
    scanners = summary.get("scanners", {})
    assert_true(isinstance(scanners, dict), "Static gate summary scanner map is invalid.")
    assert_true(hygiene.attrib.get("totalCritical") == str(summary.get("totalCritical")), "Self-audit totalCritical drifted from summary.")
    assert_true(hygiene.attrib.get("totalWarnings") == str(summary.get("totalWarnings")), "Self-audit totalWarnings drifted from summary.")
    assert_true(hygiene.attrib.get("scannerCount") == str(len(scanners)), "Self-audit scannerCount drifted from summary.")
    task_rows = {task.attrib.get("id", ""): task for task in root.findall("./TaskReconciliation/Task")}
    task_scanner_map = {
        "01": ("Hot_Registry_Polling", "Hot_Helper_Registry_Polling"),
        "02": ("Mid_Frame_Complete", "Hot_Helper_Complete"),
        "07": ("Vault_Sovereignty",),
        "09": ("Signal_Bus_Topology",),
        "11": ("Rollback_Fence_Compliance",),
        "12": ("AUP_Compliance",),
        "14": ("Compile_Wall",),
    }
    for task_id, scanner_names in task_scanner_map.items():
        task = task_rows.get(task_id)
        assert_true(task is not None, f"Self-audit task {task_id} is missing.")
        expected_critical = 0
        for scanner_name in scanner_names:
            scanner_row = scanners.get(scanner_name, {})
            assert_true(isinstance(scanner_row, dict), f"Summary scanner {scanner_name} is missing.")
            expected_critical += int(scanner_row.get("criticalCount"))
        assert_true(
            task.attrib.get("criticalDebt") == str(expected_critical),
            f"Self-audit task {task_id} criticalDebt drifted from scanner sum.",
        )


def write_report(report_path: Path, rows: list[dict]) -> None:
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(
        json.dumps(
            {
                "agent": "SHINOBU_140",
                "evidence": "PY_TOOL/CI_FALLBACK_SELF_TEST",
                "generatedAt": datetime.now().isoformat(timespec="seconds"),
                "status": "PASS",
                "tests": rows,
            },
            indent=2,
            sort_keys=True,
        )
        + "\n",
        encoding="utf-8",
    )


def main() -> int:
    parser = argparse.ArgumentParser(description="Run executable self-tests for SHINOBU_140 static scanner gates.")
    parser.add_argument("--report", default="Docs/Reports/SHINOBU_140_SCANNER_SELF_TESTS.json")
    args = parser.parse_args()
    tests = [
        test_regression_attribution_excludes_self_gate,
        test_core_violation_fixture_trips_scanners,
        test_live_self_audit_contract_stays_parseable,
        test_live_self_audit_counts_match_static_summary,
    ]
    rows = []
    for test in tests:
        test()
        rows.append({"name": test.__name__, "status": "PASS"})
        print(f"PASS {test.__name__}")
    write_report(scanner.path(args.report), rows)
    print(f"WROTE {args.report}")
    print("SHINOBU_140_SCANNER_SELF_TESTS=PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
