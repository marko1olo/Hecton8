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
SHINOBU_207_SECTION = "SHINOBU_207"
SHINOBU_207_REPORT_KEYS = {
    "agent",
    "binarySearch",
    "cacheBTree",
    "delta",
    "flatRecordBytes",
    "flatTableBytes",
    "lookupCount",
    "nodeBytes",
    "notes",
    "recordCount",
    "sourceContracts",
    "treeBytes",
}
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
    ("mutatingReadAccessorNames", (
        r"public\s+ref\s+readonly\s+T\s+GetRecord\s*<",
        r"public\s+ReadOnlySpan<byte>\s+GetUtf8\s*\(",
        r"ResolveOrCreateBitIndex\s*\(",
        r"ResolveActiveUtf8\s*\(",
        r"TryResolveVault\s*\(",
        r"TryResolvePlayerContextCold\s*\(",
        r"ReadFileIntoPaddedBuffer\s*\(",
        r"TryGetTelemetryVaultBuffers\s*\(",
        r"TryGetTuningProfileVaultBuffer\s*\(",
        r"FetchRecordWithTelemetry\s*\(",
        r"FetchUtf8WithTelemetry\s*\(",
        r"GlobalDataVault\.TryGetLatestCreated\s*\(",
    )),
    ("hotScheduleVaultAllocationFacade", (
        r"ScheduleTelemetryPostSimulationFlush\s*\([^)]*IDataVault",
    )),
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


def validate_negative_patterns(source: str, label: str, patterns: tuple[str, ...]) -> None:
    matches: list[str] = []
    for pattern in patterns:
        for match in re.finditer(pattern, source):
            line_start = source.rfind("\n", 0, match.start()) + 1
            line_end = source.find("\n", match.start())
            if line_end == -1:
                line_end = len(source)
            snippet = source[line_start:line_end].strip()
            matches.append(f"{label}:{line_number(source, match.start())}: {snippet}")

    if matches:
        detail = "\n".join(matches[:16])
        raise RuntimeError(f"Forbidden source residue {label} returned:\n{detail}")


def is_inside_unity_editor_fence(source: str, offset: int) -> bool:
    last_if = source.rfind("#if UNITY_EDITOR", 0, offset)
    last_endif = source.rfind("#endif", 0, offset)
    return last_if >= 0 and last_if > last_endif


def extract_method_bodies(source: str, method_name: str) -> list[tuple[int, str]]:
    pattern = re.compile(
        rf"\b(?:public|private|internal|protected)\s+"
        rf"(?:static\s+)?(?:unsafe\s+)?(?:ref\s+readonly\s+)?"
        rf"[\w<>,\[\]\s]+\s+{re.escape(method_name)}(?:\s*<[^>]+>)?\s*\("
    )
    bodies: list[tuple[int, str]] = []
    for match in pattern.finditer(source):
        brace = source.find("{", match.end())
        if brace < 0:
            continue

        depth = 0
        for index in range(brace, len(source)):
            char = source[index]
            if char == "{":
                depth += 1
            elif char == "}":
                depth -= 1
                if depth == 0:
                    bodies.append((match.start(), source[brace + 1:index]))
                    break

    return bodies


def validate_read_accessor_purity() -> dict[str, bool]:
    forbidden_tokens = (
        "EnsureVaultBuffersCold(",
        "TryColdBootstrap(",
        "GetGenerationHandle<",
        "GetBuffer<",
        "GlobalRegistry.",
        "GlobalDataVault.TryGetLatestCreated",
        "File.",
        "Directory.",
        "RecordTelemetry(",
        "DumpBlackBox(",
        "DumpBTreeTelemetry(",
        "SignalBus<",
        ".TryPush(",
        ".Complete(",
        "DispatcherJobFence.TryComplete",
        "RefreshFrameLookupCounters(",
        "TryPublishLinkedAudio(",
        "CaptureLookupStats(",
        "new PdaH8lrLoreStore",
    )
    method_targets = (
        ("Assets/_Project/Scripts/Core/Data/StaticDataStore.cs", "FetchRecord"),
        ("Assets/_Project/Scripts/Core/Data/StaticDataStore.cs", "TryGetLookupEntry"),
        ("Assets/_Project/Scripts/Core/Data/BabelDictionaryStore.cs", "FetchUtf8"),
        ("Assets/_Project/Scripts/Core/Data/BabelDictionaryStore.cs", "TryFindIndex"),
        ("Assets/_Project/Scripts/UI/PdaH8lrLoreStore.cs", "TryGetUtf8"),
        ("Assets/_Project/Scripts/UI/PdaH8lrLoreStore.cs", "TryGetRecord"),
        ("Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs", "TryGetH8lrUtf8"),
        ("Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs", "TryGetMockUtf8"),
        ("Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs", "TryFindBitIndex"),
    )
    checks: dict[str, bool] = {}
    for relative_path, method_name in method_targets:
        source_path = REPO_ROOT / relative_path
        source = source_path.read_text(encoding="utf-8")
        bodies = extract_method_bodies(source, method_name)
        if not bodies:
            raise RuntimeError(f"Missing read-accessor purity target {relative_path}:{method_name}")

        for offset, body in bodies:
            key = f"{relative_path}:{method_name}:{line_number(source, offset)}"
            bad_tokens = [token for token in forbidden_tokens if token in body]
            checks[key] = not bad_tokens
            if bad_tokens:
                raise RuntimeError(
                    f"Read-accessor purity violation in {key}: {', '.join(bad_tokens)}"
                )

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

    h8lr_source = (REPO_ROOT / "Assets/_Project/Scripts/UI/PdaH8lrLoreStore.cs").read_text(encoding="utf-8")
    h8lr_mutable_read_counters_removed = all(
        token not in h8lr_source
        for token in ("_lastTreeDepth", "_lastTreeKeysProcessed", "_lastPrefetchTouchCount")
    )
    if not h8lr_mutable_read_counters_removed:
        raise RuntimeError("PdaH8lrLoreStore.TryGetUtf8 still mutates per-read tree counters")

    babel_source = (REPO_ROOT / "Assets/_Project/Scripts/Core/Data/BabelDictionaryStore.cs").read_text(encoding="utf-8")
    validate_negative_patterns(
        babel_source,
        "BabelDictionaryStore.cs",
        (
            r"\.\s*GetBuffer\s*<\s*byte\s*>\s*\(\s*BufferID\.BabelDictionaryMappedBytes",
            r"\.\s*TryGetBuffer\s*<\s*byte\s*>\s*\(\s*BufferID\.BabelDictionaryMappedBytes",
            r"SourceBytes\s*=\s*_basePointer",
            r"_basePointer\s*\+\s*entry\.ByteOffset",
        ),
    )
    babel_mirror_generation_guard = all(
        token in babel_source
        for token in (
            "private VaultGenerationHandle<byte> _mappedBytesHandle;",
            "_mappedBytesHandle = vault.GetGenerationHandle<byte>(",
            "BufferID.BabelDictionaryMappedBytes",
            "vault.TryResolveHandle(in _mappedBytesHandle",
            "LoadFileIntoPaddedBufferCold",
        )
    ) and "ReadFileIntoPaddedBuffer" not in babel_source
    if not babel_mirror_generation_guard:
        raise RuntimeError("Babel padded dictionary mirror is not generation-handle guarded")

    babel_readable_view_resolve_guard = all(
        token in babel_source
        for token in (
            "private bool TryResolveReadableView(out byte* basePointer, out BabelIndexDTO* indexPointer, out long mappedBytes)",
            "TryResolveMappedBytes(out basePointer, out mappedBytes)",
            "_mappedBytesHandle.BufferID == 0u",
            "vault.TryResolveHandle(in _mappedBytesHandle",
            "private bool TryResolveMappedBytesView(out NativeArray<byte> mappedBytesView)",
            "return new ReadOnlySpan<byte>(basePointer + entry.ByteOffset",
            "if (_ownedFallbackPointer != null)",
            "TryResolveMappedBytesView(out NativeArray<byte> sourceBytes)",
            "BabelLoreXorDecryptJob job = new BabelLoreXorDecryptJob",
            "SourceBytes = sourceBytes",
            "else",
            "BabelLoreXorDecryptPointerJob job = new BabelLoreXorDecryptPointerJob",
            "SourceBytes = basePointer",
            "SourceByteLength = mappedBytes",
        )
    ) and "_basePointer + entry.ByteOffset" not in babel_source and "SourceBytes = _basePointer" not in babel_source
    if not babel_readable_view_resolve_guard:
        raise RuntimeError("Babel lookup/decrypt paths do not resolve the Vault mirror view before payload dereference")

    validate_negative_patterns(
        h8lr_source,
        "PdaH8lrLoreStore.cs",
        (
            r"\bOpen(?:Default)?\s*\([^)]*NativeArray\s*<\s*byte\s*>",
            r"\bTryOpenVaultMirror\s*\([^)]*NativeArray\s*<\s*byte\s*>",
            r"\.\s*GetBuffer\s*<\s*byte\s*>\s*\(",
            r"\.\s*TryGetBuffer\s*<\s*byte\s*>\s*\(",
        ),
    )
    h8lr_mirror_generation_guard = all(
        token in h8lr_source
        for token in (
            "private VaultGenerationHandle<byte> _vaultMirrorHandle;",
            "OpenDefault(IDataVault vault, in VaultGenerationHandle<byte> vaultMirrorHandle)",
            "Open(string path, IDataVault vault, in VaultGenerationHandle<byte> vaultMirrorHandle)",
            "vault.TryResolveHandle(in vaultMirrorHandle",
            "TryResolveReadableBasePointer",
            "_vault.TryResolveHandle(in _vaultMirrorHandle",
        )
    ) and "OpenDefault(NativeArray<byte>" not in h8lr_source and "Open(string path, NativeArray<byte>" not in h8lr_source
    if not h8lr_mirror_generation_guard:
        raise RuntimeError("H8LR Vault mirror is not generation-handle guarded")

    editor_method_names = (
        "EditorTrySnapshot",
        "EditorUnlockAll",
        "EditorLockAll",
        "EditorSelectEntry",
        "EditorIngestCsv",
        "EditorTryWriteRawUtf8Hex",
    )
    editor_fence_checks: dict[str, bool] = {}
    for method_name in editor_method_names:
        match = re.search(rf"public\s+(?:bool|void)\s+{method_name}\s*\(", pda_source)
        if match is None:
            raise RuntimeError(f"Missing PDA editor facade {method_name}")

        fenced = is_inside_unity_editor_fence(pda_source, match.start())
        editor_fence_checks[method_name] = fenced
        if not fenced:
            raise RuntimeError(f"PDA editor facade {method_name} is not UNITY_EDITOR fenced")

    csv_ingest_method_names = (
        "TryIngestLoreMetadataCsvFromProject",
        "TryIngestLoreMetadataCsv",
    )
    csv_ingest_fence_checks: dict[str, bool] = {}
    for method_name in csv_ingest_method_names:
        match = re.search(rf"public\s+bool\s+{method_name}\s*\(", pda_source)
        if match is None:
            raise RuntimeError(f"Missing PDA CSV ingest bridge {method_name}")

        fenced = is_inside_unity_editor_fence(pda_source, match.start())
        csv_ingest_fence_checks[method_name] = fenced
        if not fenced:
            raise RuntimeError(f"PDA CSV ingest bridge {method_name} is not UNITY_EDITOR fenced")

    baker_source = (REPO_ROOT / "Assets/_Project/Scripts/Core/Data/H8DataBaker.cs").read_text(encoding="utf-8")
    baker_match = re.search(r"public\s+static\s+unsafe\s+class\s+H8DataBaker\b", baker_source)
    if baker_match is None:
        raise RuntimeError("Missing H8DataBaker editor bake bridge")
    h8_data_baker_fenced = is_inside_unity_editor_fence(baker_source, baker_match.start())
    if not h8_data_baker_fenced:
        raise RuntimeError("H8DataBaker is not UNITY_EDITOR fenced")

    manifest_match = re.search(r"public\s+static\s+H8DataBakeResult\s+GenerateHashManifest\s*\(", contracts_source)
    if manifest_match is None:
        raise RuntimeError("Missing H8DataHashTool.GenerateHashManifest editor bridge")
    hash_manifest_fenced = is_inside_unity_editor_fence(contracts_source, manifest_match.start())
    if not hash_manifest_fenced:
        raise RuntimeError("H8DataHashTool.GenerateHashManifest is not UNITY_EDITOR fenced")

    return {
        "deterministicSearchJobs": checks,
        "btreeNodeExplicit64": btree_node_64,
        "pdaMockFlatIndexScanRemoved": pda_mock_flat_scan_removed,
        "h8lrMutableReadCountersRemoved": h8lr_mutable_read_counters_removed,
        "babelMirrorGenerationGuard": babel_mirror_generation_guard,
        "babelReadableViewResolveGuard": babel_readable_view_resolve_guard,
        "h8lrMirrorGenerationGuard": h8lr_mirror_generation_guard,
        "pdaEditorFacadesFenced": editor_fence_checks,
        "pdaCsvIngestBridgesFenced": csv_ingest_fence_checks,
        "editorOnlyDesignerBridges": {
            "H8DataBaker": h8_data_baker_fenced,
            "H8DataHashTool.GenerateHashManifest": hash_manifest_fenced,
        },
        "sourceResidueClean": validate_no_source_residue(),
        "readAccessorPurity": validate_read_accessor_purity(),
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


def merge_shared_report(report: dict[str, object]) -> dict[str, object]:
    def new_shared_payload() -> dict[str, object]:
        return {
            "reportOwner": "shared",
            "sections": [SHINOBU_207_SECTION],
            SHINOBU_207_SECTION: report,
        }

    if not REPORT_PATH.exists():
        return new_shared_payload()

    try:
        existing = json.loads(REPORT_PATH.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return new_shared_payload()

    if not isinstance(existing, dict):
        return new_shared_payload()

    merged = dict(existing)
    if existing.get("agent") == SHINOBU_207_SECTION:
        for key in SHINOBU_207_REPORT_KEYS:
            merged.pop(key, None)

    merged[SHINOBU_207_SECTION] = report

    sections = merged.get("sections")
    if isinstance(sections, list):
        section_names = [item for item in sections if isinstance(item, str)]
    else:
        section_names = []

    if SHINOBU_207_SECTION not in section_names:
        section_names.insert(0, SHINOBU_207_SECTION)

    for key in existing:
        if key.startswith("SHINOBU_") and key not in section_names:
            section_names.append(key)

    if len(section_names) > 1:
        merged["reportOwner"] = "shared"
        merged["sections"] = section_names

    return merged


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
    REPORT_PATH.write_text(json.dumps(merge_shared_report(report), indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(f"MEMORY OPTIMIZATION REPORT: {REPORT_PATH.relative_to(REPO_ROOT)}")
    print(f"binary={binary_ns_per_lookup:.2f} ns btree={btree_ns_per_lookup:.2f} ns cacheLinesSaved={binary_cache_lines - btree_cache_lines:.2f}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
