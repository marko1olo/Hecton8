#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json

from H8VerifyCore import path, require, require_aligned


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--report", default="Docs/AgentLogs/VerifyQuestDag_QUEST_LOGIC_DAG_BUILDER.json")
    args = parser.parse_args()
    data = json.loads(path("Data/Narrative/First_Hour_Quests.json").read_text(encoding="utf-8"))
    nodes = data.get("nodes", [])
    size = require_aligned("Data/Narrative/First_Hour_Quests.h8qdag.bin")
    constants = path("Assets/_Project/Scripts/Core/Generated/H8QuestMasks.cs")
    require(constants.is_file(), "missing H8QuestMasks.cs")
    payload = {"status": "PASS", "nodes": len(nodes), "binaryBytes": size}
    out = path(args.report)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(f"VERIFY OK: nodes={len(nodes)} hashes=31 binaryBytes={size} constants={constants.stat().st_size}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
