#!/usr/bin/env python3
"""Focused QUEST DAG data-truth audit."""

from __future__ import annotations

import json
import re
import struct
from pathlib import Path

import QuestCompiler


ROOT = Path(__file__).resolve().parents[1]
REPORT_JSON = ROOT / "Docs/AgentLogs/VerifyQuestDagDataTruth_QUEST_LOGIC_DAG_BUILDER.json"
REPORT_MD = ROOT / "Docs/AgentLogs/VerifyQuestDagDataTruth_QUEST_LOGIC_DAG_BUILDER.md"


def read_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def check(name: str, ok: bool, detail: str) -> dict[str, object]:
    return {"name": name, "passed": bool(ok), "detail": detail}


def main() -> int:
    checks: list[dict[str, object]] = []
    graph_path = ROOT / "Data/Narrative/First_Hour_Quests.json"
    binary_path = ROOT / "Data/Narrative/First_Hour_Quests.h8qdag.bin"
    constants_path = ROOT / "Assets/_Project/Scripts/Core/Generated/H8QuestMasks.cs"

    try:
        graph, nodes, tiers, graph_hash = QuestCompiler.validate_graph(graph_path)
        checks.append(check("graph_contract", len(nodes) == 4 and graph["stateEncoding"]["maxQuests"] == 32, "nodes=4 max=32 bitsPerQuest=2"))

        blob = QuestCompiler.build_binary(nodes, graph_hash, tiers)
        actual_blob = binary_path.read_bytes()
        header = QuestCompiler.HEADER.unpack_from(actual_blob, 0)
        tier_offset = header[13] + header[11] * QuestCompiler.EDGE.size
        checks.append(check("binary_layout", actual_blob == blob and len(actual_blob) % 16 == 0, f"bytes={len(actual_blob)} tierOffset={tier_offset} endian=<"))

        constants = constants_path.read_text(encoding="utf-8")
        expected_constants = QuestCompiler.generate_csharp(nodes, graph_hash, tiers, len(blob))
        checks.append(check("generated_constants", constants == expected_constants and "UnityEngine" not in constants, f"constants={constants.count('public const')} runtimeLogic=0"))

        low = graph["scalabilityTiers"]["Low"]
        ultra = graph["scalabilityTiers"]["Ultra"]
        toaster_ok = low["stateWordCount"] == 1 and int(str(low["scannerGradientProfileHash32"]), 16) == 0
        overkill_ok = int(str(ultra["scannerGradientProfileHash32"]), 16) != 0 and int(str(ultra["radioHarmonicProfileHash32"]), 16) != 0
        checks.append(check("scalability_tiers", toaster_ok and overkill_ok, "Low stripped; Ultra gradient+harmonic extra data present"))

        labels = " ".join(node.diegetic_label for node in nodes).lower()
        checks.append(check("lore_tone", "placeholder" not in labels and any(term in labels for term in QuestCompiler.DIRTY_TERMS), "dirty_noir=true sterileTokens=0"))

        atlas = (ROOT / "Docs/PROJECT_ATLAS.md").read_text(encoding="utf-8-sig", errors="ignore")
        atlas_match = re.search(
            r"^### 85 Identified Domains\s*(?P<table>[\s\S]*?)(?:\n### |\Z)",
            atlas,
            flags=re.MULTILINE,
        )
        atlas_domain_table = atlas_match.group("table") if atlas_match else ""
        domain_count = len(re.findall(r"^\| (?:[1-9]|[1-7][0-9]|8[0-5]) \|", atlas_domain_table, flags=re.MULTILINE))
        declared = [row["ID"] for row in graph.get("atlasDomains", [])]
        checks.append(check("project_atlas_fit", domain_count == 85 and {4, 73, 75, 78}.issubset(set(declared)), f"declaredDomains={','.join(map(str, declared))} atlasDomainMap={domain_count}"))

        formats = []
        for path in (ROOT / "Tools/QuestCompiler.py", ROOT / "Tools/VerifyQuestDagBinaryIndependent.py"):
            formats.extend(re.findall(r"struct\.Struct\(\"([^\"]+)\"\)", path.read_text(encoding="utf-8")))
        checks.append(check("struct_endianness", bool(formats) and all(fmt.startswith("<") for fmt in formats), f"structFormats={len(formats)} endian=<"))

        metric_path = ROOT / "Docs/Reports/METRIC_PHI_DATA_TRUTH_AUDIT.json"
        if not metric_path.exists():
            metric_path = ROOT / "Docs/AgentLogs/VerifyMetricPhiDataTruth_QUEST_LOGIC_DAG_BUILDER.json"
        metric = read_json(metric_path)
        sweep = read_json(ROOT / "Docs/Reports/METRIC_PHI_VERIFY_SWEEP.json")
        metric_ok = metric.get("status") == "DATA_TRUTH_VERIFIED" and metric.get("summary", {}).get("failedChecks") == 0
        sweep_ok = sweep.get("status") == "VERIFY_SWEEP_PASS" and sweep.get("summary", {}).get("requiredFailures") == 0
        checks.append(check("h_phi_reports", metric_ok and sweep_ok, f"hPhiChecks={metric.get('summary', {}).get('totalChecks')} sweepCommands={sweep.get('summary', {}).get('totalCommands')}"))

        binary_count = 0
        unaligned = []
        for root in ("Data", "Assets/_Project/Data"):
            for file_path in (ROOT / root).rglob("*"):
                if file_path.is_file() and file_path.suffix.lower() in {".bin", ".h8bin"}:
                    binary_count += 1
                    if file_path.stat().st_size % 16:
                        unaligned.append(file_path.as_posix())
        checks.append(check("binary_hygiene", not unaligned, f"binaryFiles={binary_count} unaligned={len(unaligned)}"))

        seen: dict[int, str] = {}
        collision_count = 0
        for label in [graph["graphId"]] + [node.node_id for node in nodes] + [trigger.trigger_id for node in nodes for trigger in node.triggers]:
            value = QuestCompiler.fnv1a32_ascii_lower(label)
            owner = seen.get(value)
            if owner is not None and owner != label:
                collision_count += 1
            seen[value] = label
        checks.append(check("local_hash_collisions", collision_count == 0, f"questHashLabels={len(seen)} collisions={collision_count}"))
    except Exception as exc:
        checks.append(check("exception", False, str(exc)))

    failed = [row for row in checks if not row["passed"]]
    payload = {
        "schema": "H8_QUEST_DAG_DATA_TRUTH_AUDIT_V2",
        "status": "QUEST_DAG_DATA_TRUTH_VERIFIED" if not failed else "QUEST_DAG_DATA_TRUTH_FAILED",
        "summary": {
            "totalChecks": len(checks),
            "failedChecks": len(failed),
            "failedNames": [str(row["name"]) for row in failed],
        },
        "checks": checks,
        "residualRisk": [
            "CLI/static data only; Unity import, Play Mode, profiler, GCMonitor, frame-time, and player-build proof remain PENDING VERIFICATION.",
            "Quest DAG owns bitmask narrative data only; hard-science LUTs stay in owner domains.",
        ],
    }
    rendered = json.dumps(payload, indent=2, sort_keys=True) + "\n"
    REPORT_JSON.parent.mkdir(parents=True, exist_ok=True)
    if not REPORT_JSON.exists() or REPORT_JSON.read_text(encoding="utf-8") != rendered:
        REPORT_JSON.write_text(rendered, encoding="utf-8", newline="\n")
    lines = ["# QUEST_LOGIC_DAG_BUILDER Data Truth Audit", "", f"Status: {payload['status']}", "", "| Check | Status | Detail |", "|---|---:|---|"]
    for row in checks:
        lines.append(f"| `{row['name']}` | {'PASS' if row['passed'] else 'FAIL'} | {row['detail']} |")
    md = "\n".join(lines) + "\n"
    if not REPORT_MD.exists() or REPORT_MD.read_text(encoding="utf-8") != md:
        REPORT_MD.write_text(md, encoding="utf-8", newline="\n")
    if failed:
        print("QUEST_DAG_DATA_TRUTH_FAILED")
        for row in failed:
            print(f"- {row['name']}: {row['detail']}")
        return 1
    print(f"QUEST_DAG_DATA_TRUTH_VERIFIED\nchecks={len(checks)} failed=0 report={REPORT_JSON.relative_to(ROOT).as_posix()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
