#!/usr/bin/env python3
"""Compare flat midpoint lookup against the 64-byte H8 cache B-Tree."""

from __future__ import annotations

import json
import math
import re
import struct
import time
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
REPORT_PATH = REPO_ROOT / "Docs" / "Reports" / "MEMORY_OPTIMIZATION_REPORT.json"
CACHE_LINE_BYTES = 64
FLAT_RECORD_BYTES = 16
KEY_CAPACITY = 7
CHILD_CAPACITY = 8
LEAF_FLAG = 0x100
NODE_STRUCT = struct.Struct("<16I")
FLAT_RECORD_STRUCT = struct.Struct("<4I")
DETERMINISTIC_SEARCH_JOBS = (
    ("Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs", "ScanBTreeNodeJob"),
    ("Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs", "TraverseBTreeJob"),
    ("Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs", "DispatchBulkBTreeSearchJob"),
    ("Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs", "TraceBTreeTraversalJob"),
    ("Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs", "SpatialMortonRangeQueryJob"),
    ("Assets/_Project/Scripts/Core/Data/BabelDictionaryStore.cs", "BabelBTreeSearchKernel"),
)
SOURCE_CONTRACT_FILES = (
    "Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs",
    "Assets/_Project/Scripts/Core/Data/H8DataBaker.cs",
    "Assets/_Project/Scripts/Core/Data/StaticDataStore.cs",
    "Assets/_Project/Scripts/Core/Data/BabelDictionaryStore.cs",
    "Assets/_Project/Scripts/UI/PdaH8lrLoreStore.cs",
    "Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs",
)
RESIDUE_PATTERNS = (
    ("flatBinarySearchLoop", (r"while\s*\(\s*lo\s*<=\s*hi\s*\)", r"while\s*\(\s*low\s*<=\s*high\s*\)")),
    ("binarySearchApi", (r"\.BinarySearch\s*\(", r"Array\.BinarySearch")),
    ("managedLookupContainers", (r"\bDictionary\s*<", r"\bSortedList\s*<", r"\bHashSet\s*<")),
    ("packOneLayout", (r"Pack\s*=\s*1",)),
    ("wrappedOffsetPlus64", (r"offset\s*\+\s*64",)),
    ("mockFullOutputClear", (r"for\s*\(\s*int\s+clear\s*=\s*0\s*;\s*clear\s*<\s*OutputBytes\.Length",)),
)


def line_number(source: str, offset: int) -> int:
    return source.count("\n", 0, offset) + 1


def validate_no_source_residue() -> dict[str, object]:
    checks: dict[str, bool] = {}
    for residue_name, patterns in RESIDUE_PATTERNS:
        matches: list[str] = []
        for relative_path in SOURCE_CONTRACT_FILES:
            source_path = REPO_ROOT / relative_path
            source = source_path.read_text(encoding="utf-8")
            for pattern in patterns:
                for match in re.finditer(pattern, source):
                    line_start = source.rfind("\n", 0, match.start()) + 1
                    line_end = source.find("\n", match.start())
                    if line_end == -1:
                        line_end = len(source)
                    snippet = source[line_start:line_end].strip()
                    matches.append(f"{relative_path}:{line_number(source, match.start())}: {snippet}")
        checks[residue_name] = len(matches) == 0
        if matches:
            detail = "\n".join(matches[:16])
            raise RuntimeError(f"Source residue {residue_name} returned:\n{detail}")
    return checks


def validate_source_contracts() -> dict[str, object]:
    checks: dict[str, bool] = {}
    for relative_path, job_name in DETERMINISTIC_SEARCH_JOBS:
        source_path = REPO_ROOT / relative_path
        source = source_path.read_text(encoding="utf-8")
        match = re.search(rf"\[BurstCompile\(([^\]]+)\)\]\s+public\s+(?:unsafe\s+)?struct\s+{job_name}\b", source)
        if match is None:
            raise RuntimeError(f"Missing BurstCompile contract for {job_name}")

        deterministic = "FloatMode = FloatMode.Deterministic" in match.group(1)
        checks[job_name] = deterministic
        if not deterministic:
            raise RuntimeError(f"{job_name} is not deterministic")

    contracts_source = (REPO_ROOT / "Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs").read_text(encoding="utf-8")
    btree_node_64 = re.search(
        r"\[StructLayout\(LayoutKind\.Explicit,\s*Size\s*=\s*64\)\]\s+public\s+struct\s+BTreeNodeDTO\b",
        contracts_source,
    ) is not None
    if not btree_node_64:
        raise RuntimeError("BTreeNodeDTO is not explicit 64-byte layout")

    pda_source = (REPO_ROOT / "Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs").read_text(encoding="utf-8")
    pda_mock_flat_scan_removed = "for (int i = 0; i < Index.Length; i++)" not in pda_source
    if not pda_mock_flat_scan_removed:
        raise RuntimeError("PDA mock lookup still scans Index.Length linearly")

    return {
        "deterministicSearchJobs": checks,
        "btreeNodeExplicit64": btree_node_64,
        "pdaMockFlatIndexScanRemoved": pda_mock_flat_scan_removed,
        "sourceResidueClean": validate_no_source_residue(),
    }


def align_up(value: int, alignment: int) -> int:
    return (value + alignment - 1) & ~(alignment - 1)


def pack_node(keys: list[int], children: list[int], meta: int) -> bytes:
    lanes = [0] * 16
    for index, value in enumerate(keys[:KEY_CAPACITY]):
        lanes[index] = value & 0xFFFFFFFF
    for index, value in enumerate(children[:CHILD_CAPACITY]):
        lanes[7 + index] = value & 0xFFFFFFFF
    lanes[15] = meta & 0xFFFFFFFF
    return NODE_STRUCT.pack(*lanes)


def build_tree(records: list[tuple[int, int]], tree_offset: int) -> bytes:
    nodes: list[bytes] = []
    level: list[tuple[int, int]] = []
    cursor = 0
    while cursor < len(records):
        chunk = records[cursor : cursor + KEY_CAPACITY]
        nodes.append(pack_node([key for key, _ in chunk], [value for _, value in chunk], LEAF_FLAG | len(chunk)))
        level.append((len(nodes) - 1, chunk[-1][0]))
        cursor += len(chunk)

    while len(level) > 1:
        next_level: list[tuple[int, int]] = []
        cursor = 0
        while cursor < len(level):
            chunk = level[cursor : cursor + CHILD_CAPACITY]
            offsets = [tree_offset + node_index * CACHE_LINE_BYTES for node_index, _ in chunk]
            nodes.append(pack_node([max_key for _, max_key in chunk[:-1]], offsets, len(chunk) - 1))
            next_level.append((len(nodes) - 1, chunk[-1][1]))
            cursor += len(chunk)
        level = next_level

    return b"".join(nodes)


def btree_find(blob: bytes, tree_offset: int, tree_end: int, target: int) -> tuple[int, int, int]:
    offset = tree_end - CACHE_LINE_BYTES
    depth = 0
    keys_processed = 0
    while depth < 32:
        lanes = NODE_STRUCT.unpack_from(blob, offset)
        keys = lanes[:KEY_CAPACITY]
        children = lanes[7:15]
        meta = lanes[15]
        key_count = meta & 0x7
        depth += 1
        keys_processed += key_count
        if meta & LEAF_FLAG:
            for index in range(key_count):
                if keys[index] == target:
                    return children[index], depth, keys_processed
            return -1, depth, keys_processed

        child_index = key_count
        for index in range(key_count):
            if target <= keys[index]:
                child_index = index
                break
        offset = children[child_index]

    return -1, depth, keys_processed


def build_flat_table(records: list[tuple[int, int]]) -> bytes:
    table = bytearray(len(records) * FLAT_RECORD_BYTES)
    for index, (key, value) in enumerate(records):
        FLAT_RECORD_STRUCT.pack_into(table, index * FLAT_RECORD_BYTES, key, value, 0, 0)
    return bytes(table)


def binary_find(flat_table: bytes, record_count: int, target: int) -> tuple[int, int]:
    lo = 0
    hi = record_count - 1
    probes = 0
    while lo <= hi:
        probes += 1
        mid = (lo + hi) >> 1
        key, value, _, _ = FLAT_RECORD_STRUCT.unpack_from(flat_table, mid * FLAT_RECORD_BYTES)
        if key == target:
            return value, probes
        if key < target:
            lo = mid + 1
        else:
            hi = mid - 1
    return -1, probes


def generate_records(count: int) -> list[tuple[int, int]]:
    return [((0x811C9DC5 + (index * 0x01000193)) & 0xFFFFFFFF, index) for index in range(count)]


def main() -> int:
    source_contracts = validate_source_contracts()
    record_count = 16384
    lookup_count = 100000
    records = sorted(generate_records(record_count), key=lambda item: item[0])
    flat_table = build_flat_table(records)
    tree_offset = align_up(len(flat_table), CACHE_LINE_BYTES)
    tree = build_tree(records, tree_offset)
    blob = b"\0" * tree_offset + tree
    tree_end = tree_offset + len(tree)

    requests: list[int] = []
    state = 0xA2075EED
    for _ in range(lookup_count):
        state = (1664525 * state + 1013904223) & 0xFFFFFFFF
        requests.append(records[state % record_count][0])

    start = time.perf_counter_ns()
    binary_probes = 0
    binary_checksum = 0
    for key in requests:
        value, probes = binary_find(flat_table, record_count, key)
        binary_checksum ^= value
        binary_probes += probes
    binary_ns = time.perf_counter_ns() - start

    start = time.perf_counter_ns()
    btree_depth = 0
    btree_keys = 0
    btree_checksum = 0
    for key in requests:
        value, depth, keys_processed = btree_find(blob, tree_offset, tree_end, key)
        btree_checksum ^= value
        btree_depth += depth
        btree_keys += keys_processed
    btree_ns = time.perf_counter_ns() - start

    if binary_checksum != btree_checksum:
        raise RuntimeError("B-Tree benchmark checksum mismatch")

    binary_ns_per_lookup = binary_ns / lookup_count
    btree_ns_per_lookup = btree_ns / lookup_count
    binary_cache_lines = binary_probes / lookup_count
    btree_cache_lines = btree_depth / lookup_count
    report = {
        "agent": "SHINOBU_207",
        "recordCount": record_count,
        "lookupCount": lookup_count,
        "nodeBytes": CACHE_LINE_BYTES,
        "flatRecordBytes": FLAT_RECORD_BYTES,
        "flatTableBytes": len(flat_table),
        "treeBytes": len(tree),
        "sourceContracts": source_contracts,
        "binarySearch": {
            "nsPerLookup": binary_ns_per_lookup,
            "avgTableProbes": binary_cache_lines,
            "theoreticalCacheLineFetchesPerLookup": binary_cache_lines,
        },
        "cacheBTree": {
            "nsPerLookup": btree_ns_per_lookup,
            "avgDepth": btree_cache_lines,
            "avgKeysProcessed": btree_keys / lookup_count,
            "theoreticalCacheLineFetchesPerLookup": btree_cache_lines,
        },
        "delta": {
            "nsPerLookupSaved": binary_ns_per_lookup - btree_ns_per_lookup,
            "theoreticalCacheLinesSavedPerLookup": binary_cache_lines - btree_cache_lines,
            "theoreticalBytesSavedPerLookup": (binary_cache_lines - btree_cache_lines) * CACHE_LINE_BYTES,
        },
        "notes": [
            "Python timing is static scanner evidence, not Unity Burst profiler proof.",
            "Both legacy binary search and B-Tree traversal read packed byte blobs through struct.unpack_from.",
            "The cache-line delta is topology-derived and independent of Python interpreter overhead.",
        ],
    }

    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    REPORT_PATH.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(f"MEMORY OPTIMIZATION REPORT: {REPORT_PATH.relative_to(REPO_ROOT)}")
    print(f"binary={binary_ns_per_lookup:.2f} ns btree={btree_ns_per_lookup:.2f} ns cacheLinesSaved={binary_cache_lines - btree_cache_lines:.2f}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
