#!/usr/bin/env python3
import json
import pathlib
import re
import sys


ROOT = pathlib.Path(__file__).resolve().parents[1]
SCRIPTS_ROOT = ROOT / "Assets/_Project/Scripts"
VAULT_PATH = ROOT / "Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs"
H8MEMORY_PATH = ROOT / "Assets/_Project/Scripts/Core/Memory/H8Memory.cs"
FUZZER_PATH = ROOT / "Assets/_Project/Scripts/Editor/Memory/OOP_MemorySentryConcurrentRelocationFuzzer.cs"
JACOBIAN_FOAM_PATH = ROOT / "Assets/_Project/Scripts/VFX/JacobianFoam/JacobianFoamGpuRuntime.cs"
REPORT_PATH = ROOT / "Docs/Reports/MEMORY_SENTRY_OPTIMIZATION_REPORT_1310.json"

STRUCT_TARGETS = [
    (VAULT_PATH, [
        "VaultRelocationRecord",
        "VaultBufferMeta",
        "VaultTelemetrySnapshot",
        "VaultMemoryBudgetEntry",
        "VaultMemoryBlockSnapshot",
        "VaultArenaBlock",
        "MemoryDefragTelemetryEntry",
        "MacroDatabasePayloadCacheEntry",
    ]),
    (H8MEMORY_PATH, [
        "BlockDescriptor",
        "H8AllocationRecord",
        "H8MemoryTelemetryEntry",
    ]),
]

TYPE_SIZES = {
    "long": 8,
    "ulong": 8,
    "double": 8,
    "IntPtr": 8,
    "void*": 8,
    "MacroDatabasePayloadHandle": 40,
    "int": 4,
    "uint": 4,
    "float": 4,
    "Allocator": 4,
    "ushort": 2,
    "short": 2,
    "SystemID": 2,
    "byte": 1,
    "sbyte": 1,
}

HOT_METHOD_KEYS = [
    "TryAcquireWriteLock",
    "ReleaseWriteLock",
    "TryLockBuffer",
    "TryUnlockBuffer",
    "TryOpenAliasBuffer",
    "TryResolveHandle",
    "TryReadHandle",
    "TryReadOnlyHandle",
    "TryResolveSlice",
    "TryRunLiveCompactionSlice",
    "TryMoveOccupiedBlockLeft",
    "RollbackWriterLockUnlocked",
    "RollbackBufferPinUnlocked",
    "RollbackAliasPublicationLocked",
    "MarkExternalView",
    "MarkExternalViewLocked",
    "MarkAliasReader",
    "MarkAliasReaderLocked",
    "HasPinnedExternalViews",
    "RecordDefragBlackBox",
    "RequestMemorySentryDump",
    "TryGrowArenaForBytes",
    "TryGrowArena",
    "TryReallocateBlockLocked",
    "TryFreeBlock",
    "TryFreeBlockRollback",
    "TryFreeBlockUnderOwnedFence",
    "FreeBlockLocked",
]

FORBIDDEN_HOT_PATTERNS = [
    ("new_class_or_container", re.compile(r"\bnew\s+(?!VaultBufferMeta\b|VaultArenaBlock\b|MemoryDefragTelemetryEntry\b|VaultRelocationRecord\b|VaultMemoryBlockSnapshot\b|VaultTelemetrySnapshot\b|VaultMemoryBudgetEntry\b|MacroDatabasePayloadHandle\b|MacroDatabasePayloadCacheEntry\b|GenerateMockVaultRelocationJob\b)")),
    ("boxing_or_linq_surface", re.compile(r"\bIEnumerable\b|System\.Linq|\.Where\s*\(|\.Select\s*\(|\.ToList\s*\(|\.FirstOrDefault\s*\(|\.Any\s*\(")),
    ("string_formatting", re.compile(r"\.ToString\s*\(|string\.Format|\$\"|\+\s*\"|\"\s*\+")),
    ("foreach", re.compile(r"\bforeach\s*\(")),
    ("managed_io_or_thread", re.compile(r"\bnew\s+Thread\b|\bFileStream\b|\bBinaryWriter\b|\bStreamWriter\b|\bDirectory\.|\bFile\.")),
]


def read_text(path):
    return path.read_text(encoding="utf-8-sig")


def find_method(source, signature):
    index = source.find(signature)
    if index < 0:
        raise ValueError("method signature not found: " + signature)
    brace = source.find("{", index)
    if brace < 0:
        raise ValueError("method body not found: " + signature)
    depth = 0
    for cursor in range(brace, len(source)):
        char = source[cursor]
        if char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return source[index:cursor + 1]
    raise ValueError("method body not closed: " + signature)


def line_number(source, needle):
    index = source.find(needle)
    if index < 0:
        return -1
    return line_number_at(source, index)


def line_number_at(source, index):
    return source.count("\n", 0, index) + 1


def first_statement_contains(body, token):
    brace = body.find("{")
    if brace < 0:
        return False
    tail = body[brace + 1:]
    for raw_line in tail.splitlines():
        line = raw_line.strip()
        if not line or line.startswith("//") or line.startswith("/*") or line.startswith("*"):
            continue
        if line == "buffer = default;":
            continue
        return token in line
    return False


def before(body, earlier, later):
    earlier_index = body.find(earlier)
    later_index = body.find(later)
    return earlier_index >= 0 and later_index >= 0 and earlier_index < later_index


def count_occurrences(body, token):
    return body.count(token)


def has_post_create_fence_recheck(body, create_token):
    create_index = body.find(create_token)
    if create_index < 0:
        return False

    barrier_index = body.find("Thread.MemoryBarrier()", create_index)
    if barrier_index < 0:
        return False

    fence_index = body.find("Volatile.Read(ref _compactionFence)", barrier_index)
    if fence_index < 0:
        return False

    return True


def extract_explicit_struct(path, source, name):
    pattern = re.compile(
        r"\[StructLayout\(LayoutKind\.Explicit,\s*Size\s*=\s*(\d+)\)\]\s*"
        r"(?:public|internal|private)?\s*struct\s+" + re.escape(name) + r"\s*\{(?P<body>.*?)\n\s*\}",
        re.S,
    )
    match = pattern.search(source)
    if not match:
        raise ValueError("explicit struct not found: " + name)

    body = match.group("body")
    fields = []
    for field in re.finditer(r"\[FieldOffset\((\d+)\)\]\s+(?:public|internal|private)\s+([A-Za-z0-9_<>\*]+)\s+([A-Za-z0-9_]+)\s*;", body):
        offset = int(field.group(1))
        type_name = field.group(2)
        field_name = field.group(3)
        size = TYPE_SIZES.get(type_name, -1)
        fields.append({
            "name": field_name,
            "type": type_name,
            "offset": offset,
            "sizeBytes": size,
            "alignmentOk": size <= 0 or size == 1 or (offset % min(size, 8)) == 0,
        })

    size_bytes = int(match.group(1))
    known_fields = [field for field in fields if field["sizeBytes"] > 0]
    declared_sizes = [field["sizeBytes"] for field in known_fields]
    return {
        "name": name,
        "source": str(path.relative_to(ROOT)).replace("\\", "/"),
        "sizeBytes": size_bytes,
        "multipleOf8": size_bytes % 8 == 0,
        "declaredLargestToSmallest": all(declared_sizes[i] >= declared_sizes[i + 1] for i in range(len(declared_sizes) - 1)),
        "misalignedFields": [field for field in known_fields if not field["alignmentOk"]],
        "unknownSizeFields": [field for field in fields if field["sizeBytes"] < 0],
        "fields": fields,
    }


def scan_hot_managed_hits(methods):
    hits = []
    for method_name in HOT_METHOD_KEYS:
        body = methods.get(method_name)
        if body is None:
            hits.append({
                "method": method_name,
                "line": -1,
                "pattern": "missing_method",
                "text": "method not available to scanner",
            })
            continue

        lines = body.splitlines()
        method_line_offset = line_number(read_text(VAULT_PATH), body.splitlines()[0])
        for index, line in enumerate(lines):
            stripped = line.strip()
            if stripped.startswith("//"):
                continue
            for pattern_name, pattern in FORBIDDEN_HOT_PATTERNS:
                if pattern.search(stripped):
                    hits.append({
                        "method": method_name,
                        "line": method_line_offset + index,
                        "pattern": pattern_name,
                        "text": stripped,
                    })
    return hits


def scan_cold_managed_sites(source, file_name):
    site_patterns = [
        ("cold_auto_reset_event", re.compile(r"new\s+AutoResetEvent\(")),
        ("cold_thread_spawn", re.compile(r"new\s+Thread\(")),
        ("cold_file_stream", re.compile(r"new\s+FileStream\(")),
        ("cold_stream_writer", re.compile(r"new\s+StreamWriter\(")),
        ("legacy_binary_writer", re.compile(r"new\s+BinaryWriter\(")),
        ("fatal_managed_exception", re.compile(r"new\s+FatalMemoryException\(")),
        ("directory_file_io", re.compile(r"\bDirectory\.|\bFile\.")),
    ]
    sites = []
    for line_index, line in enumerate(source.splitlines(), start=1):
        stripped = line.strip()
        for name, pattern in site_patterns:
            if pattern.search(stripped):
                sites.append({
                    "file": file_name,
                    "line": line_index,
                    "pattern": name,
                    "text": stripped,
                    "classification": "cold_forensic_or_legacy_dump_path",
                })
    return sites


def strip_comments(source):
    source = re.sub(r"/\*.*?\*/", "", source, flags=re.S)
    return re.sub(r"//.*", "", source)


def parse_call_arguments(source, start):
    open_paren = source.find("(", start)
    if open_paren < 0:
        return None, -1

    depth = 0
    in_string = False
    escape = False
    for cursor in range(open_paren, len(source)):
        char = source[cursor]
        if in_string:
            if escape:
                escape = False
            elif char == "\\":
                escape = True
            elif char == '"':
                in_string = False
            continue

        if char == '"':
            in_string = True
        elif char == "(":
            depth += 1
        elif char == ")":
            depth -= 1
            if depth == 0:
                return source[open_paren + 1:cursor], cursor

    return None, -1


def count_top_level_args(arguments):
    if arguments is None or not arguments.strip():
        return 0

    depth = 0
    commas = 0
    in_string = False
    escape = False
    for char in arguments:
        if in_string:
            if escape:
                escape = False
            elif char == "\\":
                escape = True
            elif char == '"':
                in_string = False
            continue

        if char == '"':
            in_string = True
        elif char in "([{":
            depth += 1
        elif char in ")]}":
            depth -= 1
        elif char == "," and depth == 0:
            commas += 1

    return commas + 1


def scan_single_arg_lock_calls():
    all_hits = []
    production_hits = []
    for path in SCRIPTS_ROOT.rglob("*.cs"):
        source = path.read_text(encoding="utf-8-sig", errors="ignore")
        stripped_source = strip_comments(source)
        lines = stripped_source.splitlines()
        for method in ("TryLockBuffer", "TryUnlockBuffer"):
            cursor = 0
            needle = method + "("
            while True:
                index = stripped_source.find(needle, cursor)
                if index < 0:
                    break

                arguments, end = parse_call_arguments(stripped_source, index + len(method))
                if end < 0:
                    cursor = index + len(needle)
                    continue

                argument_count = count_top_level_args(arguments)
                line = line_number_at(stripped_source, index)
                line_text = lines[line - 1].strip() if line - 1 < len(lines) else ""
                if argument_count == 1:
                    entry = {
                        "file": str(path.relative_to(ROOT)).replace("\\", "/"),
                        "line": line,
                        "method": method,
                        "args": arguments.strip(),
                        "text": line_text,
                    }
                    all_hits.append(entry)
                    if "bool " + method + "(BufferID bufferId" not in line_text and "public bool " + method + "(BufferID bufferId" not in line_text:
                        production_hits.append(entry)
                cursor = end + 1

    return {
        "singleArgCount": len(all_hits),
        "productionSingleArgCount": len(production_hits),
        "singleArgCalls": all_hits,
        "productionSingleArgCalls": production_hits,
    }


def scan_persistent_alias_calls():
    all_hits = []
    production_hits = []
    core_memory_files = {VAULT_PATH.resolve(), H8MEMORY_PATH.resolve()}
    for path in SCRIPTS_ROOT.rglob("*.cs"):
        source = path.read_text(encoding="utf-8-sig", errors="ignore")
        stripped_source = strip_comments(source)
        lines = stripped_source.splitlines()
        for method in ("PinReadOnlyAlias", "CreateAlias"):
            cursor = 0
            needle = method + "("
            while True:
                index = stripped_source.find(needle, cursor)
                if index < 0:
                    break

                arguments, end = parse_call_arguments(stripped_source, index + len(method))
                if end < 0:
                    cursor = index + len(needle)
                    continue

                line = line_number_at(stripped_source, index)
                line_text = lines[line - 1].strip() if line - 1 < len(lines) else ""
                entry = {
                    "file": str(path.relative_to(ROOT)).replace("\\", "/"),
                    "line": line,
                    "method": method,
                    "args": arguments.strip(),
                    "text": line_text,
                }
                all_hits.append(entry)
                if path.resolve() not in core_memory_files:
                    production_hits.append(entry)
                cursor = end + 1

    return {
        "aliasCallCount": len(all_hits),
        "productionAliasCallCount": len(production_hits),
        "aliasCalls": all_hits,
        "productionAliasCalls": production_hits,
    }


def scan_raw_compaction_fence_reads(source):
    findings = []
    for line_index, line in enumerate(source.splitlines(), start=1):
        stripped = line.strip()
        if "_compactionFence" not in stripped:
            continue
        if "private int _compactionFence" in stripped:
            continue
        if stripped == "_compactionFence = 0;":
            continue
        if "Volatile.Read(ref _compactionFence)" in stripped:
            continue
        if "Volatile.Write(ref _compactionFence" in stripped:
            continue
        if "Interlocked.Exchange(ref _compactionFence" in stripped:
            continue
        if "Interlocked.CompareExchange(ref _compactionFence" in stripped:
            continue

        findings.append({
            "line": line_index,
            "text": stripped,
        })
    return findings


def scan_aup_tokens(paths):
    token_pattern = re.compile(r"\b(double3|float3|Vector3|AUP|position|Position|Distance|Force|Collider|Collision)\b")
    direct_cast_pattern = re.compile(r"\(float3\)\s*[A-Za-z0-9_\.]+|new\s+float3\s*\(\s*[A-Za-z0-9_\.]*double")
    findings = []
    direct_casts = []
    for path in paths:
        text = read_text(path)
        for line_index, line in enumerate(text.splitlines(), start=1):
            if token_pattern.search(line):
                findings.append({
                    "file": str(path.relative_to(ROOT)).replace("\\", "/"),
                    "line": line_index,
                    "text": line.strip(),
                })
            if direct_cast_pattern.search(line):
                direct_casts.append({
                    "file": str(path.relative_to(ROOT)).replace("\\", "/"),
                    "line": line_index,
                    "text": line.strip(),
                })
    return findings, direct_casts


def collect_using_lines(path):
    text = read_text(path)
    using_pattern = re.compile(r"^using\s+(?:static\s+)?[A-Za-z0-9_\.]+(?:\s*=\s*[A-Za-z0-9_\.]+)?;")
    return [line.strip() for line in text.splitlines() if using_pattern.match(line.strip())]


def nearest_asmdef(path):
    current = path.parent
    asset_root = ROOT / "Assets"
    while current != current.parent and asset_root in [current, *current.parents]:
        asmdefs = sorted(current.glob("*.asmdef"))
        if asmdefs:
            return str(asmdefs[0].relative_to(ROOT)).replace("\\", "/")
        current = current.parent
    return None


def enum_duplicate_values(source, enum_name):
    values = enum_values(source, enum_name)
    seen = {}
    duplicates = []
    for item in values:
        current_value = item["value"]
        if current_value in seen:
            duplicates.append({
                "value": current_value,
                "firstName": seen[current_value]["name"],
                "firstLine": seen[current_value]["line"],
                "duplicateName": item["name"],
                "duplicateLine": item["line"],
            })
        else:
            seen[current_value] = item

    return duplicates


def enum_values(source, enum_name):
    match = re.search(r"(?:public|internal|private)\s+enum\s+" + re.escape(enum_name) + r"(?:\s*:\s*\w+)?\s*\{", source)
    if not match:
        raise ValueError("enum not found: " + enum_name)

    start = match.end()
    depth = 1
    cursor = start
    while cursor < len(source) and depth:
        if source[cursor] == "{":
            depth += 1
        elif source[cursor] == "}":
            depth -= 1
        cursor += 1

    body = source[start:cursor - 1]
    values = []
    current_value = -1
    for item in re.finditer(r"^\s*([A-Za-z_][A-Za-z0-9_]*)\s*(?:=\s*(\d+))?\s*,", body, re.M):
        if item.group(2):
            current_value = int(item.group(2))
        else:
            current_value += 1

        line = line_number_at(source, start + item.start())
        values.append({"name": item.group(1), "value": current_value, "line": line})

    return values


def fuzzer_buffer_id_audit(fuzzer_source, h8_source, vault_source):
    constants = {}
    for name in ("StableBufferBase", "StableBufferCount", "ChurnBufferBase", "ChurnBufferCount"):
        match = re.search(r"private\s+const\s+int\s+" + name + r"\s*=\s*(\d+)\s*;", fuzzer_source)
        if not match:
            raise ValueError("fuzzer constant not found: " + name)
        constants[name] = int(match.group(1))

    capacity_match = re.search(r"private\s+const\s+int\s+MaxGenerationHandleCapacity\s*=\s*(\d+)\s*;", vault_source)
    max_generation_capacity = int(capacity_match.group(1)) if capacity_match else 100000
    declared_values = {item["value"]: item for item in enum_values(h8_source, "BufferID")}
    fuzzer_values = (
        list(range(constants["StableBufferBase"], constants["StableBufferBase"] + constants["StableBufferCount"])) +
        list(range(constants["ChurnBufferBase"], constants["ChurnBufferBase"] + constants["ChurnBufferCount"]))
    )
    collisions = [
        {
            "value": value,
            "bufferName": declared_values[value]["name"],
            "bufferLine": declared_values[value]["line"],
        }
        for value in fuzzer_values
        if value in declared_values
    ]
    out_of_range = [value for value in fuzzer_values if value <= 0 or value >= max_generation_capacity]
    return {
        "constants": constants,
        "maxGenerationHandleCapacity": max_generation_capacity,
        "fuzzerValuesMin": min(fuzzer_values),
        "fuzzerValuesMax": max(fuzzer_values),
        "collisionCount": len(collisions),
        "collisions": collisions,
        "outOfRange": out_of_range,
    }


def check(name, passed, evidence, severity="critical"):
    return {
        "name": name,
        "passed": bool(passed),
        "severity": severity,
        "evidence": evidence,
    }


def main():
    source = read_text(VAULT_PATH)
    h8_source = read_text(H8MEMORY_PATH)
    fuzzer_source = read_text(FUZZER_PATH)
    jacobian_source = read_text(JACOBIAN_FOAM_PATH)
    h8_complete_helper = find_method(h8_source, "private static void TryCompleteOwnerJobHandle")
    buffer_id_duplicates = enum_duplicate_values(h8_source, "BufferID")
    fuzzer_buffer_ids = fuzzer_buffer_id_audit(fuzzer_source, h8_source, source)
    methods = {
        "Create": find_method(source, "public static GlobalDataVault Create"),
        "Initialize": find_method(source, "public void Initialize"),
        "HasInitializedCriticalNativeStorage": find_method(source, "private bool HasInitializedCriticalNativeStorage()"),
        "AbortInitialize": find_method(source, "private void AbortInitialize()"),
        "TryAcquireWriteLock": find_method(source, "public bool TryAcquireWriteLock<T>"),
        "ReleaseWriteLock": find_method(source, "public bool ReleaseWriteLock<T>"),
        "TryLockBuffer": find_method(source, "public bool TryLockBuffer(BufferID bufferId, SystemID lockOwner)"),
        "TryUnlockBuffer": find_method(source, "public bool TryUnlockBuffer(BufferID bufferId, SystemID lockOwner)"),
        "TryOpenAliasBuffer": find_method(source, "private bool TryOpenAliasBuffer<T>"),
        "PinReadOnlyAlias": find_method(source, "public NativeArray<T>.ReadOnly PinReadOnlyAlias<T>"),
        "TryResolveHandle": find_method(source, "public bool TryResolveHandle<T>"),
        "TryReadHandle": find_method(source, "public bool TryReadHandle<T>"),
        "TryReadOnlyHandle": find_method(source, "public bool TryReadOnlyHandle<T>"),
        "TryResolveSlice": find_method(source, "public bool TryResolveSlice<T>"),
        "TryRunLiveCompactionSlice": find_method(source, "private bool TryRunLiveCompactionSlice"),
        "TryMoveOccupiedBlockLeft": find_method(source, "private bool TryMoveOccupiedBlockLeft"),
        "RollbackWriterLockUnlocked": find_method(source, "private bool RollbackWriterLockUnlocked"),
        "RollbackBufferPinUnlocked": find_method(source, "private bool RollbackBufferPinUnlocked"),
        "RollbackAliasPublicationLocked": find_method(source, "private void RollbackAliasPublicationLocked"),
        "RestorePinOwnerMetadataLocked": find_method(source, "private void RestorePinOwnerMetadataLocked"),
        "MarkExternalView": find_method(source, "private bool MarkExternalView(int key"),
        "MarkExternalViewLocked": find_method(source, "private bool MarkExternalViewLocked"),
        "MarkAliasReader": find_method(source, "private bool MarkAliasReader"),
        "MarkAliasReaderLocked": find_method(source, "private bool MarkAliasReaderLocked"),
        "HasPinnedExternalViews": find_method(source, "private bool HasPinnedExternalViews"),
        "RecordDefragBlackBox": find_method(source, "private void RecordDefragBlackBox"),
        "RequestMemorySentryDump": find_method(source, "private void RequestMemorySentryDump"),
        "StartMemorySentryDumpWorker": find_method(source, "private void StartMemorySentryDumpWorker"),
        "Dispose": find_method(source, "public void Dispose()"),
        "TryGrowArenaForBytes": find_method(source, "private bool TryGrowArenaForBytes"),
        "TryGrowArena": find_method(source, "private bool TryGrowArena(long newArenaBytes)"),
        "TryReallocateBlockLocked": find_method(source, "private bool TryReallocateBlockLocked"),
        "TryFreeBlock": find_method(source, "private bool TryFreeBlock(int blockIndex"),
        "TryFreeBlockRollback": find_method(source, "private bool TryFreeBlockRollback"),
        "TryFreeBlockUnderOwnedFence": find_method(source, "private bool TryFreeBlockUnderOwnedFence"),
        "FreeBlockLocked": find_method(source, "private bool FreeBlockLocked"),
        "TryReleaseOrphanedBuffer": find_method(source, "private bool TryReleaseOrphanedBuffer"),
    }
    jacobian_methods = {
        "LateFrameTick": find_method(jacobian_source, "public void LateFrameTick()"),
        "RecordTelemetry": find_method(jacobian_source, "private void RecordTelemetry"),
        "FlushDeferredTelemetryDump": find_method(jacobian_source, "public bool FlushDeferredTelemetryDump()"),
        "TryAcquireWriteBuffer": find_method(jacobian_source, "private bool TryAcquireWriteBuffer<T>"),
        "TryAcquireReadPin": find_method(jacobian_source, "private bool TryAcquireReadPin<T>"),
        "TrySeedDefaultTuning": find_method(jacobian_source, "private bool TrySeedDefaultTuning"),
    }

    layout_maps = []
    for struct_path, struct_names in STRUCT_TARGETS:
        struct_source = source if struct_path == VAULT_PATH else h8_source
        for struct_name in struct_names:
            layout_maps.append(extract_explicit_struct(struct_path, struct_source, struct_name))
    layout_failures = [
        item for item in layout_maps
        if not item["multipleOf8"] or not item["declaredLargestToSmallest"] or item["misalignedFields"] or item["unknownSizeFields"]
    ]
    hot_managed_hits = scan_hot_managed_hits(methods)
    cold_managed_sites = (
        scan_cold_managed_sites(source, "GlobalDataVault.cs") +
        scan_cold_managed_sites(h8_source, "H8Memory.cs")
    )
    fatal_call_sites = [
        {
            "file": "GlobalDataVault.cs",
            "line": line_number_at(source, match.start()),
            "text": source[source.rfind("\n", 0, match.start()) + 1:source.find("\n", match.start())].strip(),
        }
        for match in re.finditer(r"FatalMemoryException\.Throw", source)
    ] + [
        {
            "file": "H8Memory.cs",
            "line": line_number_at(h8_source, match.start()),
            "text": h8_source[h8_source.rfind("\n", 0, match.start()) + 1:h8_source.find("\n", match.start())].strip(),
        }
        for match in re.finditer(r"FatalMemoryException\.Throw", h8_source)
    ]
    legacy_fatal_constructor_sites = [
        {
            "file": "H8Memory.cs",
            "line": line_number_at(h8_source, match.start()),
            "text": h8_source[h8_source.rfind("\n", 0, match.start()) + 1:h8_source.find("\n", match.start())].strip(),
        }
        for match in re.finditer(r"throw\s+new\s+FatalMemoryException", h8_source)
    ]
    raw_fence_reads = scan_raw_compaction_fence_reads(source)
    single_arg_lock_audit = scan_single_arg_lock_calls()
    persistent_alias_audit = scan_persistent_alias_calls()
    aup_findings, aup_direct_casts = scan_aup_tokens([VAULT_PATH, H8MEMORY_PATH, JACOBIAN_FOAM_PATH])
    using_report = {
        "GlobalDataVault.cs": collect_using_lines(VAULT_PATH),
        "H8Memory.cs": collect_using_lines(H8MEMORY_PATH),
        "OOP_MemorySentryConcurrentRelocationFuzzer.cs": collect_using_lines(FUZZER_PATH),
        "nearestFuzzerAsmdef": nearest_asmdef(FUZZER_PATH),
    }

    reallocate_hits = [
        line_number_at(source, match.start())
        for match in re.finditer(r"H8Memory\.ReallocateRaw", source)
    ]
    try_grow_arena_line = line_number(source, "private bool TryGrowArena(long newArenaBytes)")
    try_grow_arena_end = try_grow_arena_line + methods["TryGrowArena"].count("\n")
    realloc_outside_grow = [
        line for line in reallocate_hits
        if line < try_grow_arena_line or line > try_grow_arena_end
    ]

    checks = [
        check(
            "TryAcquireWriteLock_first_line_compaction_fence",
            first_statement_contains(methods["TryAcquireWriteLock"], "Volatile.Read(ref _compactionFence)"),
            "first executable statement after out-default must reject active compaction",
        ),
        check(
            "TryLockBuffer_first_line_compaction_fence",
            first_statement_contains(methods["TryLockBuffer"], "Volatile.Read(ref _compactionFence)"),
            "first executable statement must reject active compaction",
        ),
        check(
            "TryAcquireWriteLock_publishes_active_bit_inside_mutation_gate",
            before(methods["TryAcquireWriteLock"], "if (!TryEnterBlockMutationGate())", "SetActiveLockBit(activeLockBit)") and
            before(methods["TryAcquireWriteLock"], "SetActiveLockBit(activeLockBit)", "Interlocked.CompareExchange(ref activeWriter"),
            "active bit is published only after the block mutation gate is held and before writer id publication",
        ),
        check(
            "TryLockBuffer_publishes_active_bit_inside_mutation_gate",
            before(methods["TryLockBuffer"], "if (!TryEnterBlockMutationGate())", "SetActiveLockBit(activeLockBit)") and
            before(methods["TryLockBuffer"], "SetActiveLockBit(activeLockBit)", "block.Reserved1++"),
            "pin bit is published inside the mutation gate before the private block pin is incremented",
        ),
        check(
            "ReleaseWriteLock_uses_volatile_and_interlocked_release",
            "Volatile.Read(ref activeWriter)" in methods["ReleaseWriteLock"] and
            "Interlocked.CompareExchange(ref activeWriter, 0" in methods["ReleaseWriteLock"],
            "writer release path must use volatile read and interlocked zeroing",
        ),
        check(
            "ReleaseWriteLock_clears_writer_and_active_bit_inside_mutation_gate",
            before(methods["ReleaseWriteLock"], "if (!TryEnterBlockMutationGate())", "ReleaseWriterBlockLockUnlocked") and
            before(methods["ReleaseWriteLock"], "ReleaseWriterBlockLockUnlocked", "Interlocked.CompareExchange(ref activeWriter, 0") and
            before(methods["ReleaseWriteLock"], "Interlocked.CompareExchange(ref activeWriter, 0", "ClearActiveLockBitIfUnusedLocked(activeLockBit)") and
            before(methods["ReleaseWriteLock"], "ClearActiveLockBitIfUnusedLocked(activeLockBit)", "ReleaseBlockMutationGate()") and
            "ClearActiveLockBitIfUnused(activeLockBit)" not in methods["ReleaseWriteLock"],
            "writer release must clear private block lock, writer id, and active bit while the same mutation gate is still held",
        ),
        check(
            "TryAcquireWriteLock_partial_rollback_is_under_existing_gate",
            count_occurrences(methods["TryAcquireWriteLock"], "RollbackWriterLockUnlocked(") >= 2 and
            before(methods["TryAcquireWriteLock"], "if (!TryEnterBlockMutationGate())", "RollbackWriterLockUnlocked(") and
            "TryAcquireBlockMutationGate()" not in methods["RollbackWriterLockUnlocked"] and
            "ReleaseWriterBlockLockUnlocked(bufferKey, offsetBytes)" in methods["RollbackWriterLockUnlocked"] and
            "ClearActiveLockBitIfUnusedLocked(activeLockBit)" in methods["RollbackWriterLockUnlocked"],
            "unpublished writer lock rollback must occur while the caller still owns the mutation gate; no bounded second-gate failure path",
        ),
        check(
            "TryUnlockBuffer_clears_active_bit_under_mutation_gate",
            "ClearActiveLockBitIfUnusedLocked(ResolveActiveLockBit(bufferId))" in methods["TryUnlockBuffer"],
            "pin unlock clears active bit while the block gate is still held",
        ),
        check(
            "TryOpenAliasBuffer_marks_external_view_before_native_view",
            before(methods["TryOpenAliasBuffer"], "TryEnterBlockMutationGate()", "MarkExternalViewLocked(key, meta.OffsetBytes)") and
            before(methods["TryOpenAliasBuffer"], "MarkExternalViewLocked(key, meta.OffsetBytes)", "MarkAliasReaderLocked(key, requester)") and
            before(methods["TryOpenAliasBuffer"], "MarkAliasReaderLocked(key, requester)", "H8Memory.CreateNativeArrayView<T>") and
            before(methods["TryOpenAliasBuffer"], "H8Memory.CreateNativeArrayView<T>", "ReleaseBlockMutationGate()"),
            "read-only alias must publish the external-view relocation pin and alias requester under one mutation gate before constructing a NativeArray view",
        ),
        check(
            "TryOpenAliasBuffer_rolls_back_unpublished_alias_pin_on_failure",
            "RollbackAliasPublicationLocked(key, lockedOffsetBytes, hadExternalView, previousAliasRequester)" in methods["TryOpenAliasBuffer"] and
            "RollbackAliasPublicationLocked" in methods["TryOpenAliasBuffer"] and
            "BlockFlagExternalView" in methods["RollbackAliasPublicationLocked"] and
            "meta.LastAliasRequester = previousAliasRequester;" in methods["RollbackAliasPublicationLocked"] and
            "UpdateH8Descriptor(in block)" in methods["RollbackAliasPublicationLocked"] and
            "MarkAliasReader((int)bufferId, requester)" not in methods["PinReadOnlyAlias"],
            "alias open must not leave BlockFlagExternalView/LastAliasRequester pinned when no alias is returned",
            severity="critical",
        ),
        check(
            "TryOpenAliasBuffer_rejects_existing_persistent_alias",
            "if (hadExternalView || meta.LastAliasRequester != SystemID.Unknown)" in methods["TryOpenAliasBuffer"] and
            before(methods["TryOpenAliasBuffer"], "if (hadExternalView || meta.LastAliasRequester != SystemID.Unknown)", "MarkExternalViewLocked(key, meta.OffsetBytes)"),
            "persistent read aliases have no release token, so a second alias must fail closed instead of overwriting LastAliasRequester",
            severity="critical",
        ),
        check(
            "MarkExternalView_uses_compaction_aware_mutation_gate",
            "TryEnterBlockMutationGate()" in methods["MarkExternalView"] and
            before(methods["MarkExternalView"], "TryEnterBlockMutationGate()", "MarkExternalViewLocked") and
            before(methods["MarkExternalView"], "MarkExternalViewLocked", "ReleaseBlockMutationGate()"),
            "external-view pin publication must be serialized with compaction and block metadata mutation",
        ),
        check(
            "MarkAliasReader_uses_mutation_gate_and_requires_external_view",
            "TryEnterBlockMutationGate()" in methods["MarkAliasReader"] and
            before(methods["MarkAliasReader"], "TryEnterBlockMutationGate()", "MarkAliasReaderLocked") and
            before(methods["MarkAliasReader"], "MarkAliasReaderLocked", "ReleaseBlockMutationGate()") and
            "BlockFlagExternalView" in methods["MarkAliasReaderLocked"] and
            before(methods["MarkAliasReaderLocked"], "BlockFlagExternalView", "WriteMetadata(key, in meta)"),
            "alias-reader metadata updates must be serialized with relocation metadata and only apply to already-pinned external views",
        ),
        check(
            "Pure_transient_read_views_recheck_fence_after_view_creation",
            has_post_create_fence_recheck(methods["TryResolveHandle"], "H8Memory.CreateNativeArrayView<T>") and
            has_post_create_fence_recheck(methods["TryReadHandle"], "H8Memory.CreateNativeArrayView<T>") and
            has_post_create_fence_recheck(methods["TryReadOnlyHandle"], "H8Memory.CreateReadOnlyNativeArrayView<T>") and
            has_post_create_fence_recheck(methods["TryResolveSlice"], "full.GetSubArray"),
            "pure read accessors must not pin relocation metadata, but they must reject if compaction/growth starts while a transient view is being created",
            severity="major",
        ),
        check(
            "Read_alias_path_does_not_sanitize_or_mutate_payload",
            "SanitizeFinitePayload" not in methods["TryOpenAliasBuffer"] and
            "SanitizeFinitePayload" not in methods["PinReadOnlyAlias"] and
            "SanitizeFinitePayload" not in methods["TryResolveHandle"] and
            "SanitizeFinitePayload" not in methods["TryReadHandle"] and
            "SanitizeFinitePayload" not in methods["TryReadOnlyHandle"] and
            "SanitizeFinitePayload" not in methods["TryResolveSlice"],
            "read and read-alias routes must not repair or rewrite payload data; finite sanitization belongs only to owner allocation/resize paths",
            severity="critical",
        ),
        check(
            "TryLockBuffer_partial_pin_rollback_is_under_existing_gate",
            "RollbackBufferPinUnlocked(key, lockedOffsetBytes, activeLockBit, previousAliasRequester)" in methods["TryLockBuffer"] and
            before(methods["TryLockBuffer"], "if (!TryEnterBlockMutationGate())", "RollbackBufferPinUnlocked(") and
            "TryAcquireBlockMutationGate()" not in methods["RollbackBufferPinUnlocked"] and
            "ClearActiveLockBitIfUnusedLocked(activeLockBit)" in methods["RollbackBufferPinUnlocked"] and
            "RestorePinOwnerMetadataLocked(bufferKey, offsetBytes, previousAliasRequester)" in methods["RollbackBufferPinUnlocked"],
            "unpublished buffer pin rollback must occur before releasing the mutation gate; no bounded second-gate failure path",
        ),
        check(
            "TryLockBuffer_owner_bound_pin_and_unlock",
            "lockOwner == SystemID.Unknown" in methods["TryLockBuffer"] and
            "lockOwner == SystemID.Unknown" in methods["TryUnlockBuffer"] and
            "meta.LastAliasRequester != SystemID.Unknown && meta.LastAliasRequester != lockOwner" in methods["TryLockBuffer"] and
            "meta.LastAliasRequester = lockOwner;" in methods["TryLockBuffer"] and
            "postLockMeta.LastAliasRequester != lockOwner" in methods["TryLockBuffer"] and
            "meta.LastAliasRequester != lockOwner" in methods["TryUnlockBuffer"] and
            "meta.LastAliasRequester = SystemID.Unknown;" in methods["TryUnlockBuffer"] and
            "WriteMetadata(key, in meta)" in methods["TryUnlockBuffer"] and
            "meta.LastAliasRequester = previousAliasRequester;" in methods["RestorePinOwnerMetadataLocked"],
            "read pins must be owner-bound so one domain cannot unlock another domain's live NativeArray pin",
            severity="critical",
        ),
        check(
            "ExternalView_blocks_writer_and_read_pin_admission",
            "meta.LastAliasRequester != SystemID.Unknown" in methods["TryAcquireWriteLock"] and
            before(methods["TryAcquireWriteLock"], "BlockFlagExternalView", "SetActiveLockBit(activeLockBit)") and
            before(methods["TryLockBuffer"], "BlockFlagExternalView", "SetActiveLockBit(activeLockBit)") and
            before(methods["TryUnlockBuffer"], "BlockFlagExternalView", "meta.LastAliasRequester = SystemID.Unknown;"),
            "persistent external alias views have no release token, so writer locks and owner read pins must fail closed while BlockFlagExternalView is published; unlock must not erase alias requester metadata while an external view remains",
            severity="critical",
        ),
        check(
            "No_production_single_arg_buffer_pin_calls",
            single_arg_lock_audit["productionSingleArgCount"] == 0,
            "one-arg TryLockBuffer/TryUnlockBuffer overloads fail closed; production call count must stay zero",
            severity="critical",
        ),
        check(
            "No_production_persistent_read_alias_calls",
            persistent_alias_audit["productionAliasCallCount"] == 0,
            "PinReadOnlyAlias/CreateAlias have no release token and must stay unused outside Core.Memory; use owner-tagged TryLockBuffer/TryUnlockBuffer for releasable read pins",
            severity="critical",
        ),
        check(
            "TryRunLiveCompactionSlice_fail_closed_gate",
            "TryAcquireBlockMutationGate()" in methods["TryRunLiveCompactionSlice"] and
            count_occurrences(methods["TryRunLiveCompactionSlice"], "HasActiveBurstLocks(activeBurstLockMask)") >= 2,
            "compaction slice must own the block gate and reject active local/external lock masks",
        ),
        check(
            "TryReleaseOrphanedBuffer_frees_under_owned_fence_before_metadata_tombstone",
            "TryFreeBlockUnderOwnedFence(blockIndex, clearPayload: true)" in methods["TryReleaseOrphanedBuffer"] and
            "WriteMetadata(key, in tombstone)" not in methods["TryReleaseOrphanedBuffer"] and
            before(methods["TryReleaseOrphanedBuffer"], "TryFreeBlockUnderOwnedFence(blockIndex, clearPayload: true)", "_buffers.Remove(key)") and
            "TryAcquireBlockMutationGate()" in methods["TryFreeBlockUnderOwnedFence"] and
            "TryEnterBlockMutationGate()" not in methods["TryFreeBlockUnderOwnedFence"] and
            "FreeBlockLocked(blockIndex, clearPayload)" in methods["TryFreeBlockUnderOwnedFence"] and
            "return TryFreeBlockUnderOwnedFence(blockIndex, clearPayload);" in methods["TryFreeBlockRollback"],
            "orphan sweep owns the relocation fence, so reclamation must use the raw block gate and must not tombstone metadata before the block is actually freed",
            severity="critical",
        ),
        check(
            "TryMoveOccupiedBlockLeft_rechecks_before_memmove",
            before(methods["TryMoveOccupiedBlockLeft"], "HasActiveBurstLocks(activeBurstLockMask)", "UnsafeUtility.MemMove") and
            before(methods["TryMoveOccupiedBlockLeft"], "BlockFlagLocked", "UnsafeUtility.MemMove") and
            before(methods["TryMoveOccupiedBlockLeft"], "Reserved1", "UnsafeUtility.MemMove"),
            "move helper must recheck active masks and private block locks before MemMove",
        ),
        check(
            "TryGrowArenaForBytes_blocks_active_locks_before_growth",
            before(methods["TryGrowArenaForBytes"], "HasActiveBurstLocks(0u)", "return TryGrowArena(desiredBytes)") and
            "QueueDeferredArenaGrowth(requiredContiguousBytes)" in methods["TryGrowArenaForBytes"],
            "arena growth request must fail closed and queue deferred bytes under active locks",
        ),
        check(
            "TryGrowArena_blocks_active_locks_before_reallocate",
            before(methods["TryGrowArena"], "HasActiveBurstLocks(0u)", "H8Memory.ReallocateRaw") and
            before(methods["TryGrowArena"], "TryAcquireBlockMutationGate()", "H8Memory.ReallocateRaw"),
            "raw arena relocation must be behind active-lock and block-gate checks",
        ),
        check(
            "ArenaGrowth_blocks_external_views_before_reallocate",
            before(methods["TryGrowArenaForBytes"], "HasPinnedExternalViews()", "return TryGrowArena(desiredBytes)") and
            before(methods["TryGrowArena"], "HasPinnedExternalViews()", "H8Memory.ReallocateRaw") and
            "BlockFlagExternalView" in methods["HasPinnedExternalViews"],
            "arena growth must fail closed when any unmanaged external view can hold a stale pointer into the arena",
        ),
        check(
            "TryGrowArena_raises_compaction_fence_around_reallocate",
            before(methods["TryGrowArena"], "Interlocked.CompareExchange(ref _compactionFence, 1, 0)", "H8Memory.ReallocateRaw") and
            before(methods["TryGrowArena"], "H8Memory.ReallocateRaw", "Interlocked.Exchange(ref _compactionFence, 0)"),
            "arena growth must publish the relocation fence before raw realloc and clear it only in the finalizer after pointer/metadata publication",
        ),
        check(
            "No_ReallocateRaw_outside_TryGrowArena",
            len(realloc_outside_grow) == 0,
            "H8Memory.ReallocateRaw line hits outside TryGrowArena: " + ",".join(str(x) for x in realloc_outside_grow),
        ),
        check(
            "RecordDefragBlackBox_pointer_write",
            "NativeArrayUnsafeUtility.GetUnsafePtr(_defragBlackBox)" in methods["RecordDefragBlackBox"] and
            "UnsafeUtility.MemCpy" in methods["RecordDefragBlackBox"] and
            "Volatile.Write(ref _defragBlackBoxCursor" in methods["RecordDefragBlackBox"],
            "telemetry ring must use unmanaged pointer write and volatile cursor publication",
        ),
        check(
            "MemorySentry_background_dump_route",
            "MemorySentryDumpPath" in source and
            "Dump_1310_MemorySentry.bin" in source and
            "new Thread(MemorySentryDumpWorkerLoop)" in methods["StartMemorySentryDumpWorker"] and
            "signal.Set()" in methods["RequestMemorySentryDump"] and
            "new Thread" not in methods["RequestMemorySentryDump"],
            "1310 dump route must use a cold-started background worker; hot request path only sets an atomic flag and signal",
        ),
        check(
            "MemorySentry_dispose_rechecks_dump_state_after_worker_stop",
            before(methods["Dispose"], "WaitForMemorySentryDumpFlushOnDispose()", "StopMemorySentryDumpWorker()") and
            before(methods["Dispose"], "StopMemorySentryDumpWorker()", "Volatile.Read(ref _memorySentryDumpInFlight) == 0") and
            before(methods["Dispose"], "Volatile.Read(ref _memorySentryDumpInFlight) == 0", "H8Memory.Release(ref _defragBlackBox"),
            "dispose must re-check dump in-flight state after stopping the worker before deciding to leak or release the telemetry ring",
            severity="major",
        ),
        check(
            "Telemetry_reserved_cursor_removed",
            "ReservedCursorBytes" not in source and "LockedSkipCount" in source,
            "skip telemetry must use LockedSkipCount and no stale ReservedCursorBytes field",
            severity="major",
        ),
        check(
            "TryFreeBlock_normal_release_respects_compaction_fence",
            "TryEnterBlockMutationGate()" in methods["TryFreeBlock"] and
            "return TryFreeBlockUnderOwnedFence(blockIndex, clearPayload);" in methods["TryFreeBlockRollback"] and
            "TryAcquireBlockMutationGate()" in methods["TryFreeBlockUnderOwnedFence"],
            "normal release uses compaction-aware gate; rollback and owned-fence cleanup use the raw gate only when the caller already owns exclusion",
        ),
        check(
            "FreeBlockLocked_rejects_locked_or_external_view_blocks",
            before(methods["FreeBlockLocked"], "BlockFlagExternalView", "UnsafeUtility.MemClear") and
            "block.Reserved1 != 0" in methods["FreeBlockLocked"],
            "free must fail closed for writer pins, buffer pins, and external alias views",
        ),
        check(
            "TryReallocateBlockLocked_rejects_external_views",
            before(methods["TryReallocateBlockLocked"], "BlockFlagExternalView", "resizedPointer = (IntPtr)((byte*)_arenaBase") and
            "block.Reserved1 != 0" in methods["TryReallocateBlockLocked"],
            "per-buffer resize must not mutate storage while an external alias can hold the old pointer",
        ),
        check(
            "Release_paths_free_before_metadata_removal",
            before(methods["FreeBlockLocked"], "VaultArenaBlock block = _blocks[blockIndex]", "CoalesceFreeBlocksAround") and
            "if (!TryFreeBlock(blockIndex, clearPayload: true))" in source,
            "normal release paths must prove block free before deleting metadata/routes",
        ),
        check(
            "Telemetry_snapshot_uses_volatile_cursor_reads",
            "Volatile.Read(ref _defragBlackBoxRecordedCount)" in find_method(source, "public bool TryGetVaultTelemetrySnapshot") and
            "Volatile.Read(ref _defragBlackBoxCursor)" in find_method(source, "public bool TryGetVaultTelemetrySnapshot"),
            "read accessor observes telemetry cursor/count with volatile reads",
            severity="major",
        ),
        check(
            "GlobalDataVault_compaction_fence_reads_are_volatile",
            len(raw_fence_reads) == 0,
            "raw _compactionFence reads outside initialization/reset: " + str(len(raw_fence_reads)),
            severity="critical",
        ),
        check(
            "H8Memory_no_BinaryWriter_dump_path",
            "BinaryWriter" not in h8_source,
            "H8Memory fatal leak dump must use FileStream + stackalloc little-endian writes, not BinaryWriter",
            severity="major",
        ),
        check(
            "Hot_runtime_zero_gc_scan",
            len(hot_managed_hits) == 0,
            "hot allocator/lock/compaction methods forbidden managed hits: " + str(len(hot_managed_hits)),
        ),
        check(
            "Core_memory_runtime_fatal_call_sites_removed",
            len(fatal_call_sites) == 0,
            "GlobalDataVault/H8Memory normal runtime routes must fail closed instead of calling FatalMemoryException.Throw; call sites: " + str(len(fatal_call_sites)),
        ),
        check(
            "GlobalDataVault_initialize_fail_closed_on_missing_native_storage",
            before(methods["Initialize"], "H8Memory.Allocate<VaultBufferMeta>", "if (!_metadataByBufferId.IsCreated)") and
            before(methods["Initialize"], "H8Memory.Allocate<MemoryDefragTelemetryEntry>", "if (!_defragBlackBox.IsCreated)") and
            before(methods["Initialize"], "H8Memory.Allocate<VaultRelocationRecord>", "if (!_lastRelocationRecords.IsCreated)") and
            before(methods["Initialize"], "H8Memory.Allocate<VaultMemoryBudgetEntry>", "if (!_memoryBudgetEntries.IsCreated)") and
            before(methods["Initialize"], "if (!HasInitializedCriticalNativeStorage())", "_arenaBase = H8Memory.AllocateRaw") and
            all(token in methods["HasInitializedCriticalNativeStorage"] for token in (
                "_buffers.IsCreated",
                "_metadata.IsCreated",
                "_metadataByBufferId.IsCreated",
                "_keys.IsCreated",
                "_blocks.IsCreated",
                "_defragBlackBox.IsCreated",
                "_lastRelocationRecords.IsCreated",
                "_memoryBudgetEntries.IsCreated",
                "_macroDatabasePayloadCache.IsCreated",
                "_macroDatabasePayloadAccessTicks.IsCreated",
                "_macroDatabasePayloadKeys.IsCreated",
            )) and
            before(methods["AbortInitialize"], "_initialized = true;", "Dispose();") and
            before(methods["Create"], "if (vault._initialized)", "_latestCreated = vault"),
            "vault bootstrap must abort before metadata loops, dump worker start, arena publication, or latest-created publication when any critical native storage allocation failed",
            severity="critical",
        ),
        check(
            "Runtime_dto_arm64_layout_scan",
            len(layout_failures) == 0,
            "explicit DTO maps must be multiple-of-8, aligned, and declared largest-to-smallest",
        ),
        check(
            "AUP_direct_float_cast_scan",
            len(aup_direct_casts) == 0,
            "memory sentry does not compute positions/distances/forces; no direct absolute double3->float3 casts found",
            severity="major",
        ),
        check(
            "Assembly_isolation_scan",
            using_report["nearestFuzzerAsmdef"] in (None, "Assets/_Project/Scripts/Editor/Hecton8.Editor.asmdef") and
            "using Hecton8.Core.Memory;" in using_report["OOP_MemorySentryConcurrentRelocationFuzzer.cs"] and
            all("Hecton8." not in item or item in ("using Hecton8.Core.Contracts;", "using Hecton8.Core.Memory;") for item in using_report["GlobalDataVault.cs"] + using_report["OOP_MemorySentryConcurrentRelocationFuzzer.cs"]),
            "no new asmdef and no direct neighbor-domain using; editor fuzzer is in existing Hecton8.Editor assembly and depends on Core.Memory only",
            severity="major",
        ),
        check(
            "H8Memory_no_core_dispatcher_fence_cycle",
            "DispatcherJobFence" not in h8_source and "using Hecton8.Core;" not in h8_source,
            "Core.Memory must not depend back on Hecton8.Core DispatcherJobFence because Hecton8.Core already references Core.Memory",
            severity="critical",
        ),
        check(
            "H8Memory_owner_job_complete_is_cold_teardown_only",
            h8_source.count(".Complete(") == 1 and
            "ownerHandle.Complete();" in h8_complete_helper and
            "ownerHandle = default;" in h8_complete_helper and
            h8_source.count("TryCompleteOwnerJobHandle(ref ownerHandle)") == 2,
            "direct JobHandle.Complete must remain only in the annotated owner teardown helper",
            severity="major",
        ),
        check(
            "H8Memory_BufferID_has_no_duplicate_values",
            len(buffer_id_duplicates) == 0,
            "BufferID enum values must be globally unique; duplicate count: " + str(len(buffer_id_duplicates)),
            severity="critical",
        ),
        check(
            "Fuzzer_buffer_ids_do_not_collide_with_runtime_BufferID",
            fuzzer_buffer_ids["collisionCount"] == 0 and not fuzzer_buffer_ids["outOfRange"],
            "editor fuzzer IDs must use a reserved free range inside GlobalDataVault metadata capacity",
            severity="major",
        ),
        check(
            "JacobianFoam_vault_nativearray_views_are_locked_or_pinned",
            "ResolveParamsArray" not in jacobian_source and
            "ResolveTuningArray" not in jacobian_source and
            "ResolveWakeArray" not in jacobian_source and
            "Tuning = ResolveTuningArray()" not in jacobian_source and
            "TryAcquireWriteBuffer(" in jacobian_methods["LateFrameTick"] and
            "TryAcquireReadPin(" in jacobian_methods["LateFrameTick"] and
            "ReleaseWriteBuffer(in _paramsHandle)" in jacobian_methods["LateFrameTick"] and
            "ReleaseReadPin(BufferID.JacobianFoamWakeImpacts)" in jacobian_methods["LateFrameTick"] and
            "TryAcquireWriteBuffer(" in jacobian_methods["RecordTelemetry"] and
            "ReleaseWriteBuffer(in _telemetryHandle)" in jacobian_methods["RecordTelemetry"] and
            "TryAcquireReadPin(" in jacobian_methods["FlushDeferredTelemetryDump"] and
            "ReleaseReadPin(BufferID.JacobianFoamTelemetryRing)" in jacobian_methods["FlushDeferredTelemetryDump"] and
            "_vault.TryAcquireWriteLock" in jacobian_methods["TryAcquireWriteBuffer"] and
            "_vault.TryLockBuffer" in jacobian_methods["TryAcquireReadPin"] and
            before(jacobian_methods["TryAcquireReadPin"], "_vault.TryLockBuffer", "TryResolveHandle(in handle"),
            "JacobianFoam was the local unpinned NativeArray consumer found during 1310 cross-domain audit; mutable views must use write locks and read/dump views must use buffer pins before resolving",
            severity="critical",
        ),
    ]

    failed = [item for item in checks if not item["passed"]]
    report = {
        "schema": "hecton8.memory_sentry_optimization_report.v1",
        "agent": "1310",
        "source": str(VAULT_PATH.relative_to(ROOT)).replace("\\", "/"),
        "status": "PASS" if not failed else "FAIL",
        "checkCount": len(checks),
        "failedCount": len(failed),
        "reallocateRawLines": reallocate_hits,
        "byteOffsetMap": layout_maps,
        "hotManagedLogicScan": {
            "status": "PASS" if not hot_managed_hits else "FAIL",
            "hitCount": len(hot_managed_hits),
            "hits": hot_managed_hits,
        },
        "coldManagedSites": cold_managed_sites,
        "singleArgBufferPinAudit": single_arg_lock_audit,
        "persistentAliasAudit": persistent_alias_audit,
        "rawCompactionFenceReads": raw_fence_reads,
        "aupScan": {
            "directAbsoluteFloatCastCount": len(aup_direct_casts),
            "directAbsoluteFloatCasts": aup_direct_casts,
            "tokenFindings": aup_findings,
            "conclusion": "Core memory does not compute AUP deltas, distances, forces, or collisions. JacobianFoam visual scroll uses double AUP composition for tiling and contains no direct absolute double3->float3 cast.",
        },
        "fatalExceptionAudit": {
            "runtimeFatalCallSiteCount": len(fatal_call_sites),
            "runtimeFatalCallSites": fatal_call_sites,
            "legacyFatalConstructorCount": len(legacy_fatal_constructor_sites),
            "legacyFatalConstructors": legacy_fatal_constructor_sites,
            "conclusion": "Target runtime call sites were converted to fail-closed returns/blackbox records. The legacy exception type remains for existing neighboring-domain callers outside 1310 authority.",
        },
        "assemblyIsolation": using_report,
        "h8MemoryJobCompletion": {
            "directCompleteLineCount": h8_source.count(".Complete("),
            "dispatcherFenceTokenPresent": "DispatcherJobFence" in h8_source,
            "helperLine": line_number(h8_source, "private static void TryCompleteOwnerJobHandle"),
            "callSites": [
                line_number_at(h8_source, match.start())
                for match in re.finditer(r"TryCompleteOwnerJobHandle\(ref ownerHandle\)", h8_source)
            ],
        },
        "h8MemoryBufferIdDuplicates": buffer_id_duplicates,
        "fuzzerBufferIdAudit": fuzzer_buffer_ids,
        "crossDomainNativeArrayAudit": {
            "jacobianFoamPath": str(JACOBIAN_FOAM_PATH.relative_to(ROOT)).replace("\\", "/"),
            "directUnpinnedResolveHelperTokensRemoved": all(
                token not in jacobian_source
                for token in ("ResolveParamsArray", "ResolveTuningArray", "ResolveWakeArray")
            ),
            "tryResolveHandleCallSites": [
                line_number_at(jacobian_source, match.start())
                for match in re.finditer(r"TryResolveHandle", jacobian_source)
            ],
        },
        "failClosedSummary": {
            "lockDuringCompaction": "TryAcquireWriteLock/TryLockBuffer return false on first volatile fence gate.",
            "externalViewAdmission": "Writer locks and owner read pins reject BlockFlagExternalView so persistent read aliases cannot mix with mutable or releasable pin routes.",
            "persistentAliasAdmission": "TryOpenAliasBuffer rejects any existing BlockFlagExternalView or LastAliasRequester because persistent aliases have no release token.",
            "lockedBlockDuringCompaction": "Compaction skips and records LockedSkipCount plus AliasBlocked flag.",
            "arenaGrowthDuringActiveAlias": "Growth returns false and queues _deferredArenaGrowthBytes when active locks or external views exist.",
            "telemetryFault": "Fault paths request raw 1310 dump; hot ring writes stay unmanaged.",
        },
        "overengineeringReview": {
            "physicsSimulation": "none",
            "mathIterations": "linear block scan only; no physical simulation or spatial solver added",
            "dearLie": "skip-and-record, mask gates, and fixed telemetry ring instead of waiting or simulating allocator pressure",
        },
        "checks": checks,
    }

    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    REPORT_PATH.write_text(json.dumps(report, indent=2, sort_keys=False) + "\n", encoding="utf-8")
    print(json.dumps({"status": report["status"], "failedCount": len(failed), "report": str(REPORT_PATH)}, sort_keys=True))
    return 0 if not failed else 1


if __name__ == "__main__":
    sys.exit(main())
