#!/usr/bin/env python3
"""Compile the first-hour QUEST DAG into zero-runtime bitmask data.

This is an offline data compiler. It validates the JSON DAG, checks lore/hash
drift, emits generated constants, and writes a fixed little-endian binary cache.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import struct
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_GRAPH = Path("Data/Narrative/First_Hour_Quests.json")
DEFAULT_OUTPUT = Path("Assets/_Project/Scripts/Core/Generated/H8QuestMasks.cs")
DEFAULT_BINARY = Path("Data/Narrative/First_Hour_Quests.h8qdag.bin")
DEFAULT_REPORT = Path("Docs/AgentLogs/QuestCompiler_QUEST_LOGIC_DAG_BUILDER.json")

MAGIC = b"H8QG"
VERSION = 1
ALIGNMENT = 16
MAX_NODES = 32
BITS_PER_QUEST = 2
FLAG_LITTLE_ENDIAN = 1
FLAG_FNV_ASCII_LOWER = 2
FLAG_TWO_BIT_STATE = 4
FLAG_SCALABILITY_TIERS = 8

HEADER = struct.Struct("<4sHHIIIIIIIIIIIIQ")
NODE = struct.Struct("<IIQQHHBBH")
TRIGGER = struct.Struct("<IIQ")
EDGE = struct.Struct("<IIQ")
TIER = struct.Struct("<IIIIIIIIHHHHII")

TIER_ORDER = ("Low", "Middle", "High", "Ultra")
TIER_FLAG_HASH_ONLY = 1
TIER_FLAG_LORE = 2
TIER_FLAG_TRIGGER = 4
TIER_FLAG_VFX = 8
TIER_FLAG_GRADIENT = 16
TIER_FLAG_HARMONIC = 32

DIRTY_TERMS = ("oil", "rust", "coffin", "drowned", "scar", "locker", "radio", "bus")
STERILE_TERMS = ("placeholder", "todo", "clean sci-fi", "hyperspace", "warp", "magic")


class QuestCompilerError(RuntimeError):
    pass


@dataclass(frozen=True)
class TriggerDef:
    trigger_id: str
    trigger_hash: int
    type_name: str
    type_hash: int


@dataclass(frozen=True)
class QuestNode:
    node_id: str
    slot: int
    node_hash: int
    lore_id: str
    lore_hash: int
    diegetic_label: str
    prerequisites: tuple[str, ...]
    triggers: tuple[TriggerDef, ...]

    @property
    def shift(self) -> int:
        return self.slot * BITS_PER_QUEST

    @property
    def state_mask(self) -> int:
        return 0x3 << self.shift

    @property
    def active_mask(self) -> int:
        return 0x1 << self.shift

    @property
    def done_mask(self) -> int:
        return 0x2 << self.shift


@dataclass(frozen=True)
class TierDef:
    name: str
    tier_hash: int
    target_hash: int
    marker_hash: int
    scanner_visual_hash: int
    radio_noise_hash: int
    scanner_gradient_hash: int
    radio_harmonic_hash: int
    payload_flags: int
    max_nodes_per_signal: int
    state_word_count: int
    harmonic_bands: int
    index: int


def repo_path(path: Path) -> str:
    return path.resolve().relative_to(ROOT).as_posix()


def resolve(path: Path) -> Path:
    return path if path.is_absolute() else ROOT / path


def read_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def parse_hex(value: Any, field: str) -> int:
    if isinstance(value, int):
        return value & 0xFFFFFFFF
    if not isinstance(value, str) or not value.startswith("0x"):
        raise QuestCompilerError(f"{field} must be a 0x-prefixed uint32 hex string")
    parsed = int(value, 16)
    if parsed < 0 or parsed > 0xFFFFFFFF:
        raise QuestCompilerError(f"{field} is outside uint32 range: {value}")
    return parsed


def fmt_hash(value: int) -> str:
    return f"0x{value & 0xFFFFFFFF:08X}"


def fnv1a32_ascii_lower(text: str) -> int:
    value = 0x811C9DC5
    for raw in text.encode("ascii"):
        byte = raw + 32 if 65 <= raw <= 90 else raw
        value ^= byte
        value = (value * 0x01000193) & 0xFFFFFFFF
    return value if value != 0 else 1


def require(condition: bool, message: str) -> None:
    if not condition:
        raise QuestCompilerError(message)


def ensure_noop_text(path: Path, payload: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists() and path.read_text(encoding="utf-8") == payload:
        return
    path.write_text(payload, encoding="utf-8", newline="\n")


def ensure_noop_bytes(path: Path, payload: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists() and path.read_bytes() == payload:
        return
    path.write_bytes(payload)


def class_name(node_id: str) -> str:
    parts = [part for part in re.split(r"[^0-9A-Za-z]+", node_id) if part]
    return "".join(part[:1].upper() + part[1:] for part in parts)


def load_lore_hashes(path: Path) -> dict[str, int]:
    manifest = read_json(path)
    result: dict[str, int] = {}
    for entry in manifest.get("entries", []):
        entry_hash = parse_hex(entry.get("hash"), "lore hash")
        for key_name in ("canonical_id", "source", "id"):
            key = entry.get(key_name)
            if isinstance(key, str) and key:
                result[key.replace("\\", "/")] = entry_hash
    return result


def validate_layout(data: dict[str, Any]) -> None:
    layout = data.get("binaryLayout", {})
    require(layout.get("magic") == "H8QG", "binaryLayout.magic must be H8QG")
    require(layout.get("version") == VERSION, "binaryLayout.version mismatch")
    require(layout.get("endianness") == "little", "binaryLayout.endianness must be little")
    require(layout.get("alignmentBytes") == ALIGNMENT, "binaryLayout.alignmentBytes mismatch")
    require(layout.get("headerBytes") == HEADER.size, "binaryLayout.headerBytes mismatch")
    require(layout.get("nodeRecordBytes") == NODE.size, "binaryLayout.nodeRecordBytes mismatch")
    require(layout.get("triggerRecordBytes") == TRIGGER.size, "binaryLayout.triggerRecordBytes mismatch")
    require(layout.get("edgeRecordBytes") == EDGE.size, "binaryLayout.edgeRecordBytes mismatch")
    require(layout.get("scalabilityTierRecordBytes") == TIER.size, "binaryLayout.scalabilityTierRecordBytes mismatch")
    require(layout.get("scalabilityTierCount") == len(TIER_ORDER), "binaryLayout.scalabilityTierCount mismatch")

    state = data.get("stateEncoding", {})
    require(state.get("storage") == "ulong", "stateEncoding.storage must be ulong")
    require(state.get("bitsPerQuest") == BITS_PER_QUEST, "stateEncoding.bitsPerQuest mismatch")
    require(state.get("maxQuests") == MAX_NODES, "stateEncoding.maxQuests mismatch")
    require(state.get("inactive") == 0 and state.get("active") == 1 and state.get("done") == 2, "state values mismatch")


def parse_nodes(data: dict[str, Any], lore_hashes: dict[str, int]) -> list[QuestNode]:
    raw_nodes = data.get("nodes")
    require(isinstance(raw_nodes, list), "nodes must be a list")
    require(0 < len(raw_nodes) <= MAX_NODES, f"Quest graph defines {len(raw_nodes)} nodes. Maximum is {MAX_NODES}.")

    nodes: list[QuestNode] = []
    seen_ids: set[str] = set()
    seen_slots: set[int] = set()
    for index, raw in enumerate(raw_nodes):
        require(isinstance(raw, dict), f"nodes[{index}] must be an object")
        node_id = str(raw.get("ID", ""))
        require(node_id, f"nodes[{index}].ID is empty")
        require(node_id not in seen_ids, f"duplicate node ID: {node_id}")
        seen_ids.add(node_id)

        slot = int(raw.get("Slot", -1))
        require(0 <= slot < MAX_NODES, f"{node_id} slot out of range")
        require(slot not in seen_slots, f"duplicate slot: {slot}")
        seen_slots.add(slot)
        require(slot == index, f"{node_id} slot must be contiguous and match index {index}")

        node_hash = parse_hex(raw.get("Hash32"), f"{node_id}.Hash32")
        require(node_hash == fnv1a32_ascii_lower(node_id), f"{node_id} hash drift: {fmt_hash(node_hash)}")

        label = str(raw.get("DiegeticLabel", ""))
        lowered_label = label.lower()
        require(not any(term in lowered_label for term in STERILE_TERMS), f"{node_id} label contains sterile term")
        require(any(term in lowered_label for term in DIRTY_TERMS), f"{node_id} label lacks NASA-punk noir grime")

        lore_id = str(raw.get("LoreCanonicalID", ""))
        lore_hash = parse_hex(raw.get("LoreHash32"), f"{node_id}.LoreHash32")
        require(lore_id in lore_hashes, f"{node_id} lore id missing from manifest: {lore_id}")
        require(lore_hash == lore_hashes[lore_id], f"{node_id} lore hash drift: {fmt_hash(lore_hash)} != {fmt_hash(lore_hashes[lore_id])}")

        prerequisites = tuple(str(item) for item in raw.get("Prerequisites", []))
        triggers: list[TriggerDef] = []
        for trig_index, trig in enumerate(raw.get("CompletionTriggers", [])):
            trigger_id = str(trig.get("ID", ""))
            trigger_type = str(trig.get("Type", ""))
            trigger_hash = parse_hex(trig.get("Hash32"), f"{node_id}.CompletionTriggers[{trig_index}].Hash32")
            require(trigger_hash == fnv1a32_ascii_lower(trigger_id), f"{trigger_id} hash drift")
            triggers.append(
                TriggerDef(
                    trigger_id=trigger_id,
                    trigger_hash=trigger_hash,
                    type_name=trigger_type,
                    type_hash=fnv1a32_ascii_lower(trigger_type),
                )
            )
        require(triggers, f"{node_id} must have at least one completion trigger")

        nodes.append(
            QuestNode(
                node_id=node_id,
                slot=slot,
                node_hash=node_hash,
                lore_id=lore_id,
                lore_hash=lore_hash,
                diegetic_label=label,
                prerequisites=prerequisites,
                triggers=tuple(triggers),
            )
        )
    return nodes


def validate_edges(nodes: list[QuestNode]) -> list[QuestNode]:
    by_id = {node.node_id: node for node in nodes}
    for node in nodes:
        for prerequisite in node.prerequisites:
            require(prerequisite in by_id, f"{node.node_id} references missing prerequisite {prerequisite}")

    visiting: set[str] = set()
    visited: set[str] = set()
    ordered: list[QuestNode] = []

    def visit(node: QuestNode) -> None:
        if node.node_id in visited:
            return
        require(node.node_id not in visiting, f"cycle detected at {node.node_id}")
        visiting.add(node.node_id)
        for prerequisite in node.prerequisites:
            visit(by_id[prerequisite])
        visiting.remove(node.node_id)
        visited.add(node.node_id)
        ordered.append(node)

    for node in nodes:
        visit(node)

    active = {nodes[0].node_id}
    done: set[str] = set()
    progressed = True
    while progressed:
        progressed = False
        for node in nodes:
            if node.node_id in done:
                continue
            if all(item in done for item in node.prerequisites):
                done.add(node.node_id)
                active.add(node.node_id)
                progressed = True
    require(len(done) == len(nodes), f"softlock risk: unreachable nodes {sorted(set(by_id) - done)}")
    return ordered


def payload_flags(raw: dict[str, Any]) -> int:
    marker = str(raw.get("markerPayload", ""))
    flags = 0
    if marker == "hash_only":
        flags |= TIER_FLAG_HASH_ONLY
    if "lore" in marker:
        flags |= TIER_FLAG_LORE
    if "trigger" in marker:
        flags |= TIER_FLAG_TRIGGER
    visual_hashes = (
        parse_hex(raw.get("scannerVisualProfileHash32", "0x00000000"), "scannerVisualProfileHash32"),
        parse_hex(raw.get("radioNoiseProfileHash32", "0x00000000"), "radioNoiseProfileHash32"),
    )
    if any(visual_hashes) or "vfx" in marker:
        flags |= TIER_FLAG_VFX
    if parse_hex(raw.get("scannerGradientProfileHash32", "0x00000000"), "scannerGradientProfileHash32"):
        flags |= TIER_FLAG_GRADIENT
    if parse_hex(raw.get("radioHarmonicProfileHash32", "0x00000000"), "radioHarmonicProfileHash32"):
        flags |= TIER_FLAG_HARMONIC
    return flags


def parse_tiers(data: dict[str, Any], node_count: int) -> list[TierDef]:
    raw_tiers = data.get("scalabilityTiers", {})
    require(isinstance(raw_tiers, dict), "scalabilityTiers must be an object")
    result: list[TierDef] = []
    for index, name in enumerate(TIER_ORDER):
        raw = raw_tiers.get(name)
        require(isinstance(raw, dict), f"missing scalability tier {name}")
        target = str(raw.get("target", ""))
        marker = str(raw.get("markerPayload", ""))
        max_nodes = int(raw.get("maxEvaluatedNodesPerSignal", 0))
        state_words = int(raw.get("stateWordCount", 0))
        require(state_words == 1, f"{name} tier must stay in one state word")
        require(max_nodes >= node_count and max_nodes <= MAX_NODES, f"{name} maxEvaluatedNodesPerSignal invalid")
        tier = TierDef(
            name=name,
            tier_hash=fnv1a32_ascii_lower(name),
            target_hash=fnv1a32_ascii_lower(target),
            marker_hash=fnv1a32_ascii_lower(marker),
            scanner_visual_hash=parse_hex(raw.get("scannerVisualProfileHash32", "0x00000000"), f"{name}.scannerVisualProfileHash32"),
            radio_noise_hash=parse_hex(raw.get("radioNoiseProfileHash32", "0x00000000"), f"{name}.radioNoiseProfileHash32"),
            scanner_gradient_hash=parse_hex(raw.get("scannerGradientProfileHash32", "0x00000000"), f"{name}.scannerGradientProfileHash32"),
            radio_harmonic_hash=parse_hex(raw.get("radioHarmonicProfileHash32", "0x00000000"), f"{name}.radioHarmonicProfileHash32"),
            payload_flags=payload_flags(raw),
            max_nodes_per_signal=max_nodes,
            state_word_count=state_words,
            harmonic_bands=int(raw.get("harmonicBands", 0)),
            index=index,
        )
        if name in ("Low", "Middle"):
            require(tier.scanner_gradient_hash == 0 and tier.radio_harmonic_hash == 0, f"{name} tier must be stripped")
        if name in ("High", "Ultra"):
            require(tier.scanner_gradient_hash != 0 and tier.radio_harmonic_hash != 0, f"{name} tier missing overkill extra data")
        result.append(tier)
    return result


def prerequisite_mask(node: QuestNode, by_id: dict[str, QuestNode]) -> int:
    mask = 0
    for prerequisite in node.prerequisites:
        mask |= by_id[prerequisite].done_mask
    return mask


def all_done_mask(nodes: list[QuestNode]) -> int:
    mask = 0
    for node in nodes:
        mask |= node.done_mask
    return mask


def build_binary(nodes: list[QuestNode], graph_hash: int, tiers: list[TierDef]) -> bytes:
    by_id = {node.node_id: node for node in nodes}
    trigger_count = sum(len(node.triggers) for node in nodes)
    edge_count = sum(len(node.prerequisites) for node in nodes)
    node_offset = HEADER.size
    trigger_offset = node_offset + len(nodes) * NODE.size
    edge_offset = trigger_offset + trigger_count * TRIGGER.size
    tier_offset = edge_offset + edge_count * EDGE.size
    total_bytes = tier_offset + len(tiers) * TIER.size
    require(total_bytes % ALIGNMENT == 0, f"binary byte count not aligned: {total_bytes}")

    parts = [
        HEADER.pack(
            MAGIC,
            VERSION,
            HEADER.size,
            FLAG_LITTLE_ENDIAN | FLAG_FNV_ASCII_LOWER | FLAG_TWO_BIT_STATE | FLAG_SCALABILITY_TIERS,
            graph_hash,
            len(nodes),
            MAX_NODES,
            node_offset,
            trigger_count,
            TRIGGER.size,
            trigger_offset,
            edge_count,
            EDGE.size,
            edge_offset,
            total_bytes,
            all_done_mask(nodes),
        )
    ]
    for topo_index, node in enumerate(nodes):
        parts.append(
            NODE.pack(
                node.node_hash,
                node.lore_hash,
                prerequisite_mask(node, by_id),
                node.state_mask,
                node.slot,
                len(node.triggers),
                topo_index,
                len(node.prerequisites),
                0,
            )
        )
    for node in nodes:
        for trigger in node.triggers:
            parts.append(TRIGGER.pack(trigger.trigger_hash, trigger.type_hash, node.done_mask))
    for node in nodes:
        for prerequisite in node.prerequisites:
            parts.append(EDGE.pack(by_id[prerequisite].node_hash, node.node_hash, by_id[prerequisite].done_mask))
    for tier in tiers:
        parts.append(
            TIER.pack(
                tier.tier_hash,
                tier.target_hash,
                tier.marker_hash,
                tier.scanner_visual_hash,
                tier.radio_noise_hash,
                tier.scanner_gradient_hash,
                tier.radio_harmonic_hash,
                tier.payload_flags,
                tier.max_nodes_per_signal,
                tier.state_word_count,
                tier.harmonic_bands,
                tier.index,
                0,
                0,
            )
        )
    blob = b"".join(parts)
    require(len(blob) == total_bytes, "binary length mismatch")
    return blob


def generate_csharp(nodes: list[QuestNode], graph_hash: int, tiers: list[TierDef], binary_size: int) -> str:
    by_id = {node.node_id: node for node in nodes}
    tier_offset = HEADER.size + (len(nodes) * NODE.size) + (sum(len(node.triggers) for node in nodes) * TRIGGER.size) + (sum(len(node.prerequisites) for node in nodes) * EDGE.size)
    lines = [
        "// AUTO-GENERATED. DO NOT EDIT.",
        "// Source: Tools/QuestCompiler.py --graph Data/Narrative/First_Hour_Quests.json",
        "namespace Hecton8.Core.Generated",
        "{",
        "    public static class H8QuestMasks",
        "    {",
        f"        public const int NodeCount = {len(nodes)};",
        f"        public const int MaxNodes = {MAX_NODES};",
        f"        public const int BitsPerQuest = {BITS_PER_QUEST};",
        "        public const ulong StateBitsMask = 0x3UL;",
        "        public const ulong InactiveState = 0x0UL;",
        "        public const ulong ActiveState = 0x1UL;",
        "        public const ulong DoneState = 0x2UL;",
        f"        public const uint GraphHash32 = {fmt_hash(graph_hash)}u;",
        "        public const uint BinaryMagic = 0x47513848u;",
        f"        public const int BinaryVersion = {VERSION};",
        f"        public const int BinaryAlignmentBytes = {ALIGNMENT};",
        f"        public const int BinaryHeaderBytes = {HEADER.size};",
        f"        public const int BinaryNodeRecordBytes = {NODE.size};",
        f"        public const int BinaryTriggerRecordBytes = {TRIGGER.size};",
        f"        public const int BinaryEdgeRecordBytes = {EDGE.size};",
        f"        public const int BinaryScalabilityTierRecordBytes = {TIER.size};",
        f"        public const int BinaryScalabilityTierOffset = {tier_offset};",
        f"        public const int BinaryScalabilityTierCount = {len(tiers)};",
        f"        public const int BinaryBlobBytes = {binary_size};",
        f"        public const uint TierFlagHashOnly = {TIER_FLAG_HASH_ONLY}u;",
        f"        public const uint TierFlagLorePayload = {TIER_FLAG_LORE}u;",
        f"        public const uint TierFlagTriggerPayload = {TIER_FLAG_TRIGGER}u;",
        f"        public const uint TierFlagVfxPayload = {TIER_FLAG_VFX}u;",
        f"        public const uint TierFlagHighResGradient = {TIER_FLAG_GRADIENT}u;",
        f"        public const uint TierFlagComplexHarmonicNoise = {TIER_FLAG_HARMONIC}u;",
        f"        public const ulong AllQuestStateMask = 0x{sum(node.state_mask for node in nodes):016X}UL;",
        f"        public const ulong AllDoneMask = 0x{all_done_mask(nodes):016X}UL;",
        "",
    ]
    for tier in tiers:
        lines.extend(
            [
                f"        public static class {tier.name}Tier",
                "        {",
                f"            public const int Index = {tier.index};",
                f"            public const uint TierHash32 = {fmt_hash(tier.tier_hash)}u;",
                f"            public const uint TargetHash32 = {fmt_hash(tier.target_hash)}u;",
                f"            public const uint MarkerPayloadHash32 = {fmt_hash(tier.marker_hash)}u;",
                f"            public const uint ScannerVisualProfileHash32 = {fmt_hash(tier.scanner_visual_hash)}u;",
                f"            public const uint RadioNoiseProfileHash32 = {fmt_hash(tier.radio_noise_hash)}u;",
                f"            public const uint ScannerGradientProfileHash32 = {fmt_hash(tier.scanner_gradient_hash)}u;",
                f"            public const uint RadioHarmonicProfileHash32 = {fmt_hash(tier.radio_harmonic_hash)}u;",
                f"            public const uint PayloadFlags = {tier.payload_flags}u;",
                f"            public const int StateWordCount = {tier.state_word_count};",
                f"            public const int MaxEvaluatedNodesPerSignal = {tier.max_nodes_per_signal};",
                f"            public const int HarmonicBands = {tier.harmonic_bands};",
                "        }",
                "",
            ]
        )
    for node in nodes:
        lines.extend(
            [
                f"        public static class {class_name(node.node_id)}",
                "        {",
                f"            public const int Slot = {node.slot};",
                f"            public const int Shift = {node.shift};",
                f"            public const uint NodeHash32 = {fmt_hash(node.node_hash)}u;",
                f"            public const uint LoreHash32 = {fmt_hash(node.lore_hash)}u;",
                f"            public const int PrerequisiteCount = {len(node.prerequisites)};",
                f"            public const int TriggerCount = {len(node.triggers)};",
                f"            public const ulong StateMask = 0x{node.state_mask:016X}UL;",
                f"            public const ulong ActiveMask = 0x{node.active_mask:016X}UL;",
                f"            public const ulong DoneMask = 0x{node.done_mask:016X}UL;",
                f"            public const ulong PrerequisiteDoneMask = 0x{prerequisite_mask(node, by_id):016X}UL;",
            ]
        )
        for index, trigger in enumerate(node.triggers):
            lines.append(f"            public const uint Trigger{index}Hash32 = {fmt_hash(trigger.trigger_hash)}u;")
            lines.append(f"            public const uint Trigger{index}TypeHash32 = {fmt_hash(trigger.type_hash)}u;")
        lines.extend(["        }", ""])
    lines.extend(["    }", "}", ""])
    return "\n".join(lines)


def validate_graph(graph_path: Path) -> tuple[dict[str, Any], list[QuestNode], list[TierDef], int]:
    data = read_json(graph_path)
    require(data.get("schema") == "H8_QUEST_DAG_BITMASK_V1", "schema mismatch")
    graph_id = str(data.get("graphId", ""))
    graph_hash = parse_hex(data.get("graphHash32"), "graphHash32")
    require(graph_hash == fnv1a32_ascii_lower(graph_id), f"graph hash drift: {fmt_hash(graph_hash)}")
    validate_layout(data)
    lore_path = resolve(Path(str(data.get("loreManifest", "Data/Lore/Encyclopedia.manifest.json"))))
    nodes = parse_nodes(data, load_lore_hashes(lore_path))
    validate_edges(nodes)
    tiers = parse_tiers(data, len(nodes))
    labels: list[str] = [graph_id]
    for node in nodes:
        labels.append(node.node_id)
        labels.append(node.lore_id)
        for trigger in node.triggers:
            labels.append(trigger.trigger_id)
            labels.append(trigger.type_name)
    for tier in tiers:
        labels.append(tier.name)
    seen: dict[int, str] = {}
    for label in labels:
        value = fnv1a32_ascii_lower(label)
        owner = seen.get(value)
        require(owner is None or owner == label, f"FNV collision: {owner} vs {label} = {fmt_hash(value)}")
        seen[value] = label
    return data, nodes, tiers, graph_hash


def write_report(path: Path, nodes: list[QuestNode], tiers: list[TierDef], binary_path: Path, blob: bytes) -> None:
    payload = {
        "status": "DAG COMPILED",
        "nodeCount": len(nodes),
        "maxNodes": MAX_NODES,
        "bitsPerQuest": BITS_PER_QUEST,
        "topology": [node.node_id for node in nodes],
        "allDoneMask": f"0x{all_done_mask(nodes):016X}",
        "binary": {
            "path": repo_path(binary_path),
            "bytes": len(blob),
            "sha256": hashlib.sha256(blob).hexdigest().upper(),
            "aligned16": len(blob) % ALIGNMENT == 0,
            "endianness": "<",
            "tierOffset": HEADER.size + len(nodes) * NODE.size + sum(len(node.triggers) for node in nodes) * TRIGGER.size + sum(len(node.prerequisites) for node in nodes) * EDGE.size,
        },
        "scalabilityTiers": [tier.name for tier in tiers],
        "dataSovereignty": "stateless aligned binary lookup; no runtime private quest graph state",
        "runtimeProof": "PENDING_VERIFICATION",
    }
    ensure_noop_text(path, json.dumps(payload, indent=2, sort_keys=True) + "\n")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Compile HECTON-8 first-hour quest DAG.")
    parser.add_argument("--graph", type=Path, default=DEFAULT_GRAPH)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--binary", type=Path, default=None)
    parser.add_argument("--report", type=Path, default=DEFAULT_REPORT)
    parser.add_argument("--check-only", action="store_true")
    args = parser.parse_args(argv)

    graph_path = resolve(args.graph)
    output_path = resolve(args.output)
    report_path = resolve(args.report)
    data, nodes, tiers, graph_hash = validate_graph(graph_path)
    binary_path = resolve(args.binary) if args.binary else resolve(Path(str(data.get("compiledBinary", DEFAULT_BINARY))))
    blob = build_binary(nodes, graph_hash, tiers)
    if not args.check_only:
        ensure_noop_text(output_path, generate_csharp(nodes, graph_hash, tiers, len(blob)))
        ensure_noop_bytes(binary_path, blob)
    write_report(report_path, nodes, tiers, binary_path, blob)
    print(
        "DAG COMPILED: "
        f"{len(nodes)} nodes, 2-bit slots, topology={len(nodes)}, "
        f"output={repo_path(output_path)}, binary={repo_path(binary_path)} bytes={len(blob)}"
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except QuestCompilerError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        raise SystemExit(1)
