#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import struct
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
HEADER = struct.Struct("<4sHHIIIIIIIIIIIIQ")
NODE = struct.Struct("<IIQQHHBBH")
TRIGGER = struct.Struct("<IIQ")
EDGE = struct.Struct("<IIQ")
TIER = struct.Struct("<IIIIIIIIHHHHII")


def fnv1a32_ascii_lower(text: str) -> int:
    value = 0x811C9DC5
    for raw in text.encode("ascii"):
        byte = raw + 32 if 65 <= raw <= 90 else raw
        value ^= byte
        value = (value * 0x01000193) & 0xFFFFFFFF
    return value if value != 0 else 1


def parse_hex(value: str) -> int:
    return int(value, 16) & 0xFFFFFFFF


def resolve(path: Path) -> Path:
    return path if path.is_absolute() else ROOT / path


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--graph", type=Path, default=Path("Data/Narrative/First_Hour_Quests.json"))
    parser.add_argument("--binary", type=Path, default=Path("Data/Narrative/First_Hour_Quests.h8qdag.bin"))
    parser.add_argument("--report", type=Path, default=Path("Docs/AgentLogs/VerifyQuestDagBinaryIndependent_QUEST_LOGIC_DAG_BUILDER.json"))
    args = parser.parse_args()

    graph = json.loads(resolve(args.graph).read_text(encoding="utf-8-sig"))
    nodes = graph["nodes"]
    blob = resolve(args.binary).read_bytes()
    if len(blob) % 16 != 0:
        raise AssertionError(f"binary is not 16-byte aligned: {len(blob)}")
    header = HEADER.unpack_from(blob, 0)
    if header[0] != b"H8QG" or header[1] != 1 or header[2] != HEADER.size:
        raise AssertionError(f"bad header: {header[:3]}")
    graph_hash = parse_hex(graph["graphHash32"])
    if header[4] != graph_hash or header[5] != len(nodes) or header[6] != 32:
        raise AssertionError("header graph/node fields mismatch")
    node_offset = header[7]
    trigger_count = header[8]
    trigger_offset = header[10]
    edge_count = header[11]
    edge_offset = header[13]
    total_bytes = header[14]
    if total_bytes != len(blob):
        raise AssertionError("header total byte count mismatch")
    tier_offset = edge_offset + edge_count * EDGE.size
    if tier_offset != 304:
        raise AssertionError(f"tier offset mismatch: {tier_offset}")

    by_id = {node["ID"]: node for node in nodes}
    trigger_index = 0
    edge_index = 0
    for index, node in enumerate(nodes):
        record = NODE.unpack_from(blob, node_offset + index * NODE.size)
        node_hash = parse_hex(node["Hash32"])
        lore_hash = parse_hex(node["LoreHash32"])
        shift = int(node["Slot"]) * 2
        state_mask = 0x3 << shift
        prereq_mask = 0
        for prereq in node["Prerequisites"]:
            prereq_slot = int(by_id[prereq]["Slot"])
            prereq_mask |= 0x2 << (prereq_slot * 2)
        expected = (node_hash, lore_hash, prereq_mask, state_mask, int(node["Slot"]), len(node["CompletionTriggers"]), index, len(node["Prerequisites"]), 0)
        if record != expected:
            raise AssertionError(f"node record mismatch at {index}: {record} != {expected}")
        for trigger in node["CompletionTriggers"]:
            trigger_record = TRIGGER.unpack_from(blob, trigger_offset + trigger_index * TRIGGER.size)
            expected_trigger = (parse_hex(trigger["Hash32"]), fnv1a32_ascii_lower(trigger["Type"]), 0x2 << shift)
            if trigger_record != expected_trigger:
                raise AssertionError(f"trigger record mismatch at {trigger_index}")
            trigger_index += 1
        for prereq in node["Prerequisites"]:
            edge_record = EDGE.unpack_from(blob, edge_offset + edge_index * EDGE.size)
            expected_edge = (parse_hex(by_id[prereq]["Hash32"]), node_hash, 0x2 << (int(by_id[prereq]["Slot"]) * 2))
            if edge_record != expected_edge:
                raise AssertionError(f"edge record mismatch at {edge_index}")
            edge_index += 1
    if trigger_index != trigger_count or edge_index != edge_count:
        raise AssertionError("trigger/edge count mismatch")
    for tier_index in range(4):
        tier = TIER.unpack_from(blob, tier_offset + tier_index * TIER.size)
        if tier[11] != tier_index:
            raise AssertionError(f"tier index mismatch at {tier_index}")
    report = {
        "status": "INDEPENDENT_BINARY_VERIFY_OK",
        "nodes": len(nodes),
        "bytes": len(blob),
        "tierOffset": tier_offset,
        "magic": "H8QG",
    }
    report_path = resolve(args.report)
    report_path.parent.mkdir(parents=True, exist_ok=True)
    rendered = json.dumps(report, indent=2, sort_keys=True) + "\n"
    if not report_path.exists() or report_path.read_text(encoding="utf-8") != rendered:
        report_path.write_text(rendered, encoding="utf-8", newline="\n")
    print(f"INDEPENDENT BINARY VERIFY OK: nodes={len(nodes)} bytes={len(blob)} tierOffset={tier_offset} magic=b'H8QG'")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
