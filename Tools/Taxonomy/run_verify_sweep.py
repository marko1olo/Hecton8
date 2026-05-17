#!/usr/bin/env python3
"""Run the project Verify*.py sweep for XENO taxonomy evidence."""

from __future__ import annotations

import json
import os
import subprocess
import time
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
REPORT_JSON = ROOT / "Docs" / "AgentLogs" / "VerifySweep_XENO_TAXONOMY_WRITER.json"
REPORT_MD = ROOT / "Docs" / "AgentLogs" / "VerifySweep_XENO_TAXONOMY_WRITER.md"
TIMEOUT_SECONDS = 600
XXHASH_VERIFY_PATH = os.environ.get("H8_XXHASH_VERIFY_PATH", "").strip()
METRIC_PHI_SWEEP_INPUT = "Docs/Reports/METRIC_PHI_VERIFY_SWEEP_POST_MUTATION_FINAL.json"


def py_cmd(*args: str) -> list[str]:
    return ["python", "-B", *args]


def replay_hasher_command() -> list[str]:
    command = py_cmd("Tools/Security/VerifyReplayHasherReference.py")
    if XXHASH_VERIFY_PATH:
        command.extend(["--xxhash-path", str(Path(XXHASH_VERIFY_PATH).resolve())])
    return command

COMMANDS: list[tuple[str, list[str]]] = [
    ("VerifyNetSyncMerkleProtocol", py_cmd("Tools/Architecture/VerifyNetSyncMerkleProtocol.py")),
    ("VerifyBlueNoiseSpectrum", py_cmd("Tools/NoiseBaker/VerifyBlueNoiseSpectrum.py")),
    ("VerifyReplayHasherReference", replay_hasher_command()),
    ("verify_taxonomy", py_cmd("Tools/Taxonomy/verify_taxonomy.py", "Data/Localization/en_US_Taxonomy.json")),
    ("VerifyAiNavigationTuning", py_cmd("Tools/VerifyAiNavigationTuning.py")),
    ("VerifyBabel", py_cmd("Tools/VerifyBabel.py")),
    ("VerifyBabelDictionary", py_cmd("Tools/VerifyBabelDictionary.py")),
    ("VerifyBinaryHygiene", py_cmd("Tools/VerifyBinaryHygiene.py", "--report", "Docs/AgentLogs/BinaryHygiene_XENO_TAXONOMY_WRITER.json")),
    ("VerifyCraftingCosts", py_cmd("Tools/VerifyCraftingCosts.py")),
    ("VerifyDataInquisition", py_cmd("Tools/VerifyDataInquisition.py", "--report", "Docs/AgentLogs/DataInquisition_XENO_TAXONOMY_WRITER.json")),
    (
        "VerifyH8HashCollisions",
        [
            "python",
            "-B",
            "Tools/VerifyH8HashCollisions.py",
            "--write-json",
            "Docs/AgentLogs/H8Hash_Audit_XENO_TAXONOMY_WRITER.json",
            "--write-report",
            "Docs/AgentLogs/H8Hash_Audit_XENO_TAXONOMY_WRITER.md",
        ],
    ),
    (
        "VerifyHullStressBudget",
        [
            "python",
            "-B",
            "Tools/VerifyHullStressBudget.py",
            "--economy-json",
            "Docs/AgentLogs/EconomyMonteCarlo_XENO_TAXONOMY_WRITER_Compatible.json",
            "--write-report",
            "Docs/AgentLogs/HullStressBudget_XENO_TAXONOMY_WRITER.json",
        ],
    ),
    ("VerifyLore", py_cmd("Tools/VerifyLore.py", "--check")),
    (
        "VerifyMetricPhiDataTruth",
        [
            "python",
            "-B",
            "Tools/VerifyMetricPhiDataTruth.py",
            "--sweep-input",
            METRIC_PHI_SWEEP_INPUT,
            "--json-output",
            "Docs/AgentLogs/MetricPhiDataTruth_XENO_TAXONOMY_WRITER.json",
            "--markdown-output",
            "Docs/AgentLogs/MetricPhiDataTruth_XENO_TAXONOMY_WRITER.md",
        ],
    ),
    ("VerifyOpticsBaker", py_cmd("Tools/VerifyOpticsBaker.py", "--report", "Docs/AgentLogs/OpticsVerification_XENO_TAXONOMY_WRITER.json")),
    ("VerifyOrganicEntropy", py_cmd("Tools/VerifyOrganicEntropy.py")),
    ("VerifyPdaTechnicalLogs", py_cmd("Tools/VerifyPdaTechnicalLogs.py")),
    ("VerifyQuestDag", py_cmd("Tools/VerifyQuestDag.py", "--report", "Docs/AgentLogs/VerifyQuestDag_XENO_TAXONOMY_WRITER.json")),
    ("VerifySabineBaker", py_cmd("Tools/VerifySabineBaker.py")),
    ("VerifySnellRefractionLut", py_cmd("Tools/VerifySnellRefractionLut.py")),
    ("VerifyTideBaker", py_cmd("Tools/VerifyTideBaker.py", "--report", "Docs/AgentLogs/VerifyTideBaker_XENO_TAXONOMY_WRITER.json")),
    ("VerifyUpgradeCurveBaker", py_cmd("Tools/VerifyUpgradeCurveBaker.py")),
    ("VerifyVisualLodMatrix", py_cmd("Tools/VerifyVisualLodMatrix.py")),
    ("VerifyVramBudgets", py_cmd("Tools/VerifyVramBudgets.py")),
    ("VerifyVrComfortData", py_cmd("Tools/VerifyVrComfortData.py")),
]


def trim(text: str) -> str:
    if len(text) <= 8000:
        return text
    return text[:4000] + "\n...[trimmed]...\n" + text[-4000:]


def run_command(name: str, command: list[str]) -> dict:
    start = time.perf_counter()
    try:
        completed = subprocess.run(
            command,
            cwd=ROOT,
            text=True,
            capture_output=True,
            timeout=TIMEOUT_SECONDS,
        )
        elapsed_ms = int((time.perf_counter() - start) * 1000)
        return {
            "name": name,
            "command": command,
            "returnCode": completed.returncode,
            "elapsedMs": elapsed_ms,
            "stdout": trim(completed.stdout),
            "stderr": trim(completed.stderr),
            "passed": completed.returncode == 0,
            "timedOut": False,
        }
    except subprocess.TimeoutExpired as exc:
        elapsed_ms = int((time.perf_counter() - start) * 1000)
        return {
            "name": name,
            "command": command,
            "returnCode": None,
            "elapsedMs": elapsed_ms,
            "stdout": trim(exc.stdout or ""),
            "stderr": trim(exc.stderr or ""),
            "passed": False,
            "timedOut": True,
        }


def build_markdown(report: dict) -> str:
    lines = [
        "# Verify Sweep - XENO_TAXONOMY_WRITER",
        "",
        f"Status: {report['status']}",
        f"Passed: {report['passedCount']}/{report['totalCount']}",
        "",
        "| Script | Result | ms |",
        "|---|---:|---:|",
    ]
    for item in report["results"]:
        result = "PASS" if item["passed"] else "FAIL"
        if item["timedOut"]:
            result = "TIMEOUT"
        lines.append(f"| `{item['name']}` | {result} | {item['elapsedMs']} |")
    lines.append("")
    lines.append("Full stdout/stderr is in the JSON report.")
    return "\n".join(lines) + "\n"


def main() -> int:
    results = [run_command(name, command) for name, command in COMMANDS]
    passed_count = sum(1 for result in results if result["passed"])
    report = {
        "agent": "XENO_TAXONOMY_WRITER",
        "schema": "H8.VERIFY_SWEEP.V1",
        "totalCount": len(results),
        "passedCount": passed_count,
        "failedCount": len(results) - passed_count,
        "status": "PASS" if passed_count == len(results) else "FAIL",
        "results": results,
    }
    REPORT_JSON.parent.mkdir(parents=True, exist_ok=True)
    REPORT_JSON.write_text(json.dumps(report, indent=2, sort_keys=True), encoding="utf-8")
    REPORT_MD.write_text(build_markdown(report), encoding="utf-8")
    print(f"VERIFY SWEEP {report['status']} passed={passed_count}/{len(results)} report={REPORT_JSON}")
    return 0 if passed_count == len(results) else 1


if __name__ == "__main__":
    raise SystemExit(main())
