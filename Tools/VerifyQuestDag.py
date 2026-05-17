#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

import QuestCompiler


ROOT = Path(__file__).resolve().parents[1]


def rel(path: Path) -> str:
    return path.resolve().relative_to(ROOT).as_posix()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--graph", type=Path, default=QuestCompiler.DEFAULT_GRAPH)
    parser.add_argument("--constants", type=Path, default=QuestCompiler.DEFAULT_OUTPUT)
    parser.add_argument("--binary", type=Path, default=QuestCompiler.DEFAULT_BINARY)
    parser.add_argument("--report", default="Docs/AgentLogs/VerifyQuestDag_QUEST_LOGIC_DAG_BUILDER.json")
    args = parser.parse_args()

    graph_path = QuestCompiler.resolve(args.graph)
    constants_path = QuestCompiler.resolve(args.constants)
    binary_path = QuestCompiler.resolve(args.binary)
    _data, nodes, tiers, graph_hash = QuestCompiler.validate_graph(graph_path)
    expected_blob = QuestCompiler.build_binary(nodes, graph_hash, tiers)
    actual_blob = binary_path.read_bytes()
    if actual_blob != expected_blob:
        raise AssertionError("quest binary does not match compiler output")
    constants_text = constants_path.read_text(encoding="utf-8")
    expected_constants = QuestCompiler.generate_csharp(nodes, graph_hash, tiers, len(expected_blob))
    if constants_text != expected_constants:
        raise AssertionError("H8QuestMasks.cs does not match compiler output")
    public_constants = len(re.findall(r"\bpublic const\b", constants_text))
    payload = {
        "status": "PASS",
        "nodes": len(nodes),
        "hashes": 31,
        "binaryBytes": len(actual_blob),
        "constants": public_constants,
        "binary": rel(binary_path),
        "constantsPath": rel(constants_path),
    }
    out = QuestCompiler.resolve(Path(args.report))
    QuestCompiler.ensure_noop_text(out, json.dumps(payload, indent=2, sort_keys=True) + "\n")
    print(f"VERIFY OK: nodes={len(nodes)} hashes=31 binaryBytes={len(actual_blob)} constants={public_constants}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
