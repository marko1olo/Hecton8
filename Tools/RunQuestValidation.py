#!/usr/bin/env python3
"""Run the full QUEST_STATE_GRAPH_VALIDATOR evidence chain.

The script is a thin orchestrator around the existing stress tester and report
gate. It intentionally keeps all Unity asset mutation out of scope.
"""

from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path


DEFAULT_SEQUENCES = 1_000_000
DEFAULT_SEQUENCE_LENGTH = 24
DEFAULT_AGENT_ID = "QUEST_STATE_GRAPH_VALIDATOR"


def _repo_root() -> Path:
    return Path(__file__).resolve().parents[1]


def _run(command: list[str], cwd: Path) -> int:
    print("RUN", " ".join(command), flush=True)
    completed = subprocess.run(command, cwd=str(cwd), check=False)
    return completed.returncode


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Export, stress-test, and gate the HECTON-8 quest graph."
    )
    parser.add_argument("--sequences", type=int, default=DEFAULT_SEQUENCES)
    parser.add_argument("--sequence-length", type=int, default=DEFAULT_SEQUENCE_LENGTH)
    parser.add_argument("--agent-id", default=DEFAULT_AGENT_ID)
    parser.add_argument(
        "--graph",
        type=Path,
        default=Path("Data/Narrative/Quest_Graph.json"),
        help="Quest graph JSON path relative to the repo root unless absolute.",
    )
    parser.add_argument(
        "--report",
        type=Path,
        default=None,
        help="Stress-test JSON report path. Defaults to Docs/AgentLogs/QuestStressTest_<agent>.json.",
    )
    args = parser.parse_args()

    root = _repo_root()
    graph_path = args.graph if args.graph.is_absolute() else root / args.graph
    report_path = args.report
    if report_path is None:
        report_path = root / "Docs" / "AgentLogs" / f"QuestStressTest_{args.agent_id}.json"
    elif not report_path.is_absolute():
        report_path = root / report_path

    graph_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.parent.mkdir(parents=True, exist_ok=True)

    stress_script = root / "Tools" / "QuestStressTest.py"
    gate_script = root / "Tools" / "QuestStressReportGate.py"

    export_code = _run(
        [
            sys.executable,
            str(stress_script),
            "--export-graph",
            str(graph_path),
            "--sequences",
            "1",
            "--sequence-length",
            "1",
            "--json-output",
            str(report_path.with_name(report_path.stem + "_export_probe.json")),
        ],
        root,
    )
    if export_code != 0:
        return export_code

    stress_code = _run(
        [
            sys.executable,
            str(stress_script),
            "--sequences",
            str(args.sequences),
            "--sequence-length",
            str(args.sequence_length),
            "--json-output",
            str(report_path),
        ],
        root,
    )
    if stress_code != 0:
        return stress_code

    return _run(
        [
            sys.executable,
            str(gate_script),
            str(report_path),
            "--min-sequences",
            str(args.sequences),
        ],
        root,
    )


if __name__ == "__main__":
    raise SystemExit(main())
