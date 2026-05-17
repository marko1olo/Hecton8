#!/usr/bin/env python3
"""One-command offline gate for HECTON-8 NET_SYNC protocol artifacts."""

from __future__ import annotations

import argparse
import ast
import io
import json
import pathlib
import sys
import unittest
from dataclasses import dataclass
from typing import Dict, List, Tuple


TOOLS_ROOT = pathlib.Path(__file__).resolve().parent
REPO_ROOT = TOOLS_ROOT.parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

NET_CACHE_PREFIXES = ("NetJitterSim", "test_net_jitter_sim", "NetProtocolGate")


def cleanup_net_pycache() -> List[str]:
    failures: List[str] = []
    cache_dir = TOOLS_ROOT / "__pycache__"
    if not cache_dir.exists():
        return failures
    for path in tuple(cache_dir.iterdir()):
        if not path.name.startswith(NET_CACHE_PREFIXES):
            continue
        for _attempt in range(20):
            try:
                path.unlink()
                break
            except OSError as exc:
                last_error = exc
        else:
            failures.append(f"NET_SYNC pycache locked: {path.name} ({last_error})")
    return failures


STARTUP_CACHE_FAILURES = cleanup_net_pycache()

import NetJitterSim as net_sim  # noqa: E402
import test_net_jitter_sim as net_tests  # noqa: E402


@dataclass(frozen=True)
class Scenario:
    name: str
    clients: int
    input_delay_ticks: int
    report_path: pathlib.Path


SCENARIOS = (
    Scenario("baseline", 2, 16, REPO_ROOT / "Docs" / "Reports" / "NetJitterSim_Report.json"),
    Scenario("rollback_stress", 2, 8, REPO_ROOT / "Docs" / "Reports" / "NetJitterSim_RollbackStress_Report.json"),
    Scenario("four_client", 4, 16, REPO_ROOT / "Docs" / "Reports" / "NetJitterSim_4Client_Report.json"),
)

DOC_PATH = REPO_ROOT / "Docs" / "Modding" / "Net_Protocol_v1.md"
GATE_REPORT_PATH = REPO_ROOT / "Docs" / "Reports" / "Net_Protocol_Gate_Report.md"
STATUS_READY = "NETWORK PROTOCOL READY"
REQUIRED_DOC_TERMS = (
    "## Packet Envelope",
    "## InputState Packet",
    "## WorldDelta Packet",
    "## AUP64 Packing",
    "## Merkle Tree Hashing",
    "## Tick Reconciliation",
    "test_aup64_round_trips_boundaries_and_flags_overflow",
    "test_merkle_diff_indices_localize_changed_leaves",
    "Unit tests: 8",
    "STATUS: NETWORK PROTOCOL READY",
    "BINARY_PAYLOADS_ALIGNED=46",
    "binary_files=46",
    "CACHE_FILES_LEFT=0",
    "PYCACHE_DIRS_LEFT=0",
)
EXPECTED_UNIT_TESTS = 8


def build_args(scenario: Scenario) -> argparse.Namespace:
    jitter_ms = 80 if scenario.name == "rollback_stress" else 40
    loss_percent = "8" if scenario.name == "rollback_stress" else "5"
    rollback_ticks = 96 if scenario.name == "rollback_stress" else 64
    redundancy = 24 if scenario.name == "rollback_stress" else 16
    return argparse.Namespace(
        latency_ms=200,
        jitter_ms=jitter_ms,
        loss_bps=net_sim.parse_percent_bps(loss_percent),
        tick_ms=20,
        ticks=600,
        clients=scenario.clients,
        input_delay_ticks=scenario.input_delay_ticks,
        rollback_ticks=rollback_ticks,
        redundancy=redundancy,
        seed=0x4E455431,
        report=scenario.report_path,
    )


def run_scenarios() -> Tuple[List[Dict[str, object]], List[str]]:
    reports: List[Dict[str, object]] = []
    failures: List[str] = []

    for scenario in SCENARIOS:
        report = net_sim.simulate(build_args(scenario))
        reports.append({"name": scenario.name, "path": str(scenario.report_path), "report": report})
        scenario.report_path.parent.mkdir(parents=True, exist_ok=True)
        scenario.report_path.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        validate_sim_report(scenario.name, report, failures)

    return reports, failures


def validate_sim_report(name: str, report: Dict[str, object], failures: List[str]) -> None:
    if report.get("status") != STATUS_READY:
        failures.append(f"{name}: status != {STATUS_READY}")
        return

    verification = report.get("verification", {})
    if verification.get("master_state_hash_mismatches") != 0:
        failures.append(f"{name}: master_state_hash_mismatches != 0")
    if verification.get("input_ring_mismatches") != 0:
        failures.append(f"{name}: input_ring_mismatches != 0")
    if verification.get("missing_actual_inputs") != 0:
        failures.append(f"{name}: missing_actual_inputs != 0")
    float_audit = verification.get("float_hash_audit", {})
    if float_audit.get("status") != "PASS":
        failures.append(f"{name}: float_hash_audit != PASS")


def run_unittests() -> Tuple[int, str]:
    stream = io.StringIO()
    suite = unittest.defaultTestLoader.loadTestsFromModule(net_tests)
    result = unittest.TextTestRunner(stream=stream, verbosity=2).run(suite)
    return result.testsRun if result.wasSuccessful() else -result.testsRun, stream.getvalue()


def validate_doc() -> List[str]:
    failures: List[str] = []
    text = DOC_PATH.read_text(encoding="utf-8")
    for term in REQUIRED_DOC_TERMS:
        if term not in text:
            failures.append(f"doc missing term: {term}")
    return failures


def validate_ast() -> List[str]:
    failures: List[str] = []
    for path in (TOOLS_ROOT / "NetJitterSim.py", TOOLS_ROOT / "test_net_jitter_sim.py", pathlib.Path(__file__)):
        try:
            ast.parse(path.read_text(encoding="utf-8"), filename=str(path))
        except SyntaxError as exc:
            failures.append(f"AST parse failed: {path}: {exc}")
    return failures


def validate_packet_schema() -> List[str]:
    return [f"packet schema: {error}" for error in net_sim.validate_packet_schema()]


def validate_pycache() -> List[str]:
    failures = list(STARTUP_CACHE_FAILURES)
    failures.extend(cleanup_net_pycache())
    cache_dir = TOOLS_ROOT / "__pycache__"
    if not cache_dir.exists():
        return failures
    offenders = [
        path.name
        for path in cache_dir.iterdir()
        if path.name.startswith(NET_CACHE_PREFIXES)
    ]
    failures.extend(f"NET_SYNC pycache present: {name}" for name in offenders)
    return failures


def write_gate_report(
    scenario_reports: List[Dict[str, object]],
    unittest_count: int,
    unittest_output: str,
    failures: List[str],
) -> None:
    lines: List[str] = [
        "# NET Protocol Gate Report",
        "",
        "Date: 2026-05-15",
        "Evidence class: OFFLINE_SIM / CLI_COMPILE / STATIC_DOC / STATIC_SOURCE",
        f"Status: {'NETWORK PROTOCOL READY' if not failures else 'NETWORK PROTOCOL BLOCKED'}",
        "",
        "## Scenario Results",
        "",
        "| Scenario | Status | Sent | Lost | Hash mismatches | Ring mismatches | Float audit |",
        "|---|---|---:|---:|---:|---:|---|",
    ]

    for entry in scenario_reports:
        report = entry["report"]
        network = report["network"]
        verification = report["verification"]
        float_audit = verification["float_hash_audit"]
        lines.append(
            "| {name} | {status} | {sent} | {lost} | {hash_mismatch} | {ring_mismatch} | {float_status} |".format(
                name=entry["name"],
                status=report["status"],
                sent=network["sent_packets"],
                lost=network["lost_packets"],
                hash_mismatch=verification["master_state_hash_mismatches"],
                ring_mismatch=verification["input_ring_mismatches"],
                float_status=float_audit["status"],
            )
        )

    lines.extend(
        [
            "",
            "## Unit Tests",
            "",
            f"Result: {'PASS' if unittest_count > 0 else 'FAIL'}",
            f"Tests: {abs(unittest_count)}",
            "",
            "```text",
            unittest_output.strip(),
            "```",
            "",
            "## Failures",
            "",
        ]
    )

    if failures:
        for failure in failures:
            lines.append(f"- {failure}")
    else:
        lines.append("- None")

    lines.extend(
        [
            "",
            "## Verification Boundary",
            "",
            "This gate does not prove Unity import, Play Mode, profiler, GCMonitor, player build, scene wiring, or platform transport readiness.",
            "Runtime network ownership remains pending.",
            "",
        ]
    )

    GATE_REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    GATE_REPORT_PATH.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    scenario_reports, failures = run_scenarios()
    unittest_count, unittest_output = run_unittests()
    if unittest_count < 0:
        failures.append("unittest gate failed")
    elif unittest_count != EXPECTED_UNIT_TESTS:
        failures.append(f"unittest count {unittest_count} != expected {EXPECTED_UNIT_TESTS}")
    failures.extend(validate_doc())
    failures.extend(validate_ast())
    failures.extend(validate_packet_schema())
    failures.extend(validate_pycache())

    write_gate_report(scenario_reports, unittest_count, unittest_output, failures)

    summary = {
        "status": "NETWORK PROTOCOL READY" if not failures else "NETWORK PROTOCOL BLOCKED",
        "failures": failures,
        "report": str(GATE_REPORT_PATH),
        "scenarios": [entry["name"] for entry in scenario_reports],
        "unit_tests": abs(unittest_count),
    }
    print(json.dumps(summary, indent=2, sort_keys=True))
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
