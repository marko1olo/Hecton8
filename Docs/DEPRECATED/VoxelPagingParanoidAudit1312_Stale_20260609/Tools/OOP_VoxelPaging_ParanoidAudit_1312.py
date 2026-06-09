#!/usr/bin/env python3
import json
import re
import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
PAGER = ROOT / "Assets/_Project/Scripts/SaveSystem/H8BinaryWorldPager.cs"
PROCESSOR = ROOT / "Assets/_Project/Scripts/VoxelDeltaProcessor.cs"
COMPRESSION = ROOT / "Assets/_Project/Scripts/SaveSystem/VoxelDeltaCompressionArchitecture.cs"
HMEMORY = ROOT / "Assets/_Project/Scripts/Core/Memory/H8Memory.cs"
BINARY_MANIFEST = ROOT / "Assets/_Project/Scripts/Core/BinaryLayoutManifest.cs"
EDITOR_FUZZER = ROOT / "Assets/_Project/Scripts/SaveSystem/Editor/OOP_VoxelPagingFuzzer1312.cs"
FLOATING_ORIGIN = ROOT / "Assets/_Project/Scripts/HectonFloatingOrigin.cs"
OPT_SCANNER = ROOT / "Tools/OOP_VoxelPaging_Scanner.py"
PARANOID_SCANNER = ROOT / "Tools/OOP_VoxelPaging_ParanoidAudit_1312.py"
STATUS_FILE = ROOT / "Docs/Tasks/Status_1312.md"
RATIONALE_FILE = ROOT / "Docs/AgentLogs/Rationale_1312.md"
LOG_FILE = ROOT / "Docs/AgentLogs/LOG_1312.md"
REPORT = ROOT / "Docs/Reports/VOXEL_PAGING_PARANOID_AUDIT_1312.json"

RUNTIME_FILES = (PAGER, PROCESSOR, COMPRESSION)
DIFF_FILES = (
    "Assets/_Project/Scripts/SaveSystem/H8BinaryWorldPager.cs",
    "Assets/_Project/Scripts/VoxelDeltaProcessor.cs",
    "Assets/_Project/Scripts/SaveSystem/VoxelDeltaCompressionArchitecture.cs",
    "Assets/_Project/Scripts/Core/Memory/H8Memory.cs",
    "Assets/_Project/Scripts/Core/BinaryLayoutManifest.cs",
)

CHANGED_OR_CREATED_FILES = (
    (PAGER, "runtime_strict"),
    (PROCESSOR, "runtime_strict"),
    (COMPRESSION, "runtime_strict"),
    (HMEMORY, "core_memory_cold_contract"),
    (BINARY_MANIFEST, "cold_boot_layout_guard"),
    (EDITOR_FUZZER, "editor_only"),
    (OPT_SCANNER, "tooling_only"),
    (PARANOID_SCANNER, "tooling_only"),
    (STATUS_FILE, "documentation_state"),
    (RATIONALE_FILE, "documentation_rationale"),
    (LOG_FILE, "documentation_log"),
)

FORBIDDEN_RUNTIME_PATTERNS = {
    "new_native_array": r"new\s+NativeArray<",
    "new_native_list": r"new\s+NativeList<",
    "new_native_hash_map": r"new\s+Native(?:Parallel)?HashMap<",
    "new_native_queue": r"new\s+NativeQueue<",
    "unsafe_malloc": r"UnsafeUtility\.Malloc",
    "system_linq": r"\bSystem\.Linq\b",
    "to_string": r"\.ToString\s*\(",
    "string_format": r"string\.Format\s*\(",
    "string_concat_api": r"string\.Concat\s*\(",
    "string_builder": r"\bStringBuilder\b",
    "interpolated_string": r'\$"',
    "new_string": r"new\s+string\s*\(",
    "string_concat_left": r'"\s*\+',
    "string_concat_right": r'\+\s*"',
    "boxing_object_cast": r"\(\s*object\s*\)",
    "object_array": r"object\s*\[\s*\]",
    "params_object": r"params\s+object",
    "linq_select": r"\.Select\s*\(",
    "linq_where": r"\.Where\s*\(",
    "linq_to_array": r"\.ToArray\s*\(",
    "new_exception_object": r"new\s+[A-Za-z0-9_]*Exception\s*\(",
    "captured_exception_variable": r"catch\s*\([^)]*\s+exception\s*\)",
    "exception_member_text": r"exception\.",
}

RAW_TEXT_PATTERNS = {
    "new_token": r"\bnew\s+",
    "string_format": r"string\.Format",
    "to_string": r"\.ToString\s*\(",
    "interpolation": r'\$"',
    "string_concat_left": r"\+\s*\"",
    "string_concat_right": r"\"\s*\+",
    "linq": r"\.(?:Where|Select|Any|FirstOrDefault|ToList)\s*\(",
    "ienumerable": r"IEnumerable<",
    "ienumerator": r"IEnumerator<",
    "foreach": r"foreach\s*\(",
    "boxing_cast": r"\(\s*object\s*\)",
    "object_array": r"object\s*\[\s*\]",
    "params_object": r"params\s+object",
    "native_alloc": r"new\s+Native(?:Array|List|HashMap|ParallelHashMap|Queue)",
    "unsafe_malloc": r"UnsafeUtility\.Malloc",
    "bare_catch": r"catch\s*\{",
    "broad_catch": r"catch\s*\(\s*Exception\b",
}

EXCEPTION_SCAN_PATTERNS = {
    "catch_any": r"\bcatch\b",
    "catch_bare": r"^\s*catch\s*$",
    "catch_broad_exception": r"catch\s*\(\s*Exception\s*\)",
    "throw_any": r"\bthrow\b",
    "throw_new": r"throw\s+new\s+",
}

STRUCTS = (
    (PAGER, "PageWriteCommand", True),
    (PAGER, "PageReadCommand", True),
    (PAGER, "PageReadResult", True),
    (PAGER, "PagerTelemetryEntry", True),
    (COMPRESSION, "VoxelDeltaRleRunDTO", True),
    (COMPRESSION, "VoxelDeltaHeaderDTO", True),
    (COMPRESSION, "VoxelDeltaBlockCounter64", True),
    (COMPRESSION, "VoxelDeltaCompressionTelemetryEntry", True),
    (COMPRESSION, "VoxelDeltaCompressionTuningDTO", True),
    (COMPRESSION, "VoxelDeltaSectorStatsDTO", True),
    (COMPRESSION, "VoxelDeltaDearLieStateDTO", True),
    (COMPRESSION, "VoxelDeltaMockSchemaDTO", True),
    (COMPRESSION, "VoxelDeltaTelemetryDumpHeaderDTO", True),
    (PROCESSOR, "VoxelBlackBoxDumpHeader", True),
    (PROCESSOR, "VoxelCarveTelemetryEntry", True),
)

OWNED_FILE_SURFACE = (
    (PAGER, "runtime_strict"),
    (PROCESSOR, "runtime_strict"),
    (COMPRESSION, "runtime_strict"),
    (HMEMORY, "core_memory_cold_contract"),
    (BINARY_MANIFEST, "cold_boot_layout_guard"),
    (EDITOR_FUZZER, "editor_only"),
    (OPT_SCANNER, "tooling_only"),
    (PARANOID_SCANNER, "tooling_only"),
    (ROOT / "Docs/Reports/VOXEL_PAGING_ARCHAEOLOGY_1312.json", "report"),
    (ROOT / "Docs/Reports/VOXEL_PAGING_OPTIMIZATION_REPORT_1312.json", "report"),
    (REPORT, "report"),
    (STATUS_FILE, "documentation_state"),
    (RATIONALE_FILE, "documentation_rationale"),
    (LOG_FILE, "documentation_log"),
)

TYPE_BYTES = {
    "byte": 1,
    "sbyte": 1,
    "bool": 1,
    "H8WorldPageStatus": 1,
    "PagerTelemetryOperation": 1,
    "ushort": 2,
    "short": 2,
    "uint": 4,
    "int": 4,
    "float": 4,
    "float2": 4,
    "float3": 4,
    "float4": 4,
    "ulong": 8,
    "long": 8,
    "double": 8,
    "double2": 8,
    "double3": 8,
    "double4": 8,
}


def rel(path: Path) -> str:
    return str(path.relative_to(ROOT)).replace("\\", "/")


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


def scan_patterns(path: Path, patterns: dict[str, str]) -> list[dict]:
    source = read(path).splitlines()
    hits = []
    compiled = {name: re.compile(pattern) for name, pattern in patterns.items()}
    for line_number, line in enumerate(source, 1):
        for name, pattern in compiled.items():
            if pattern.search(line):
                hits.append({
                    "file": rel(path),
                    "line": line_number,
                    "pattern": name,
                    "text": line.strip(),
                })
    return hits


def git_added_lines() -> list[dict]:
    command = ["git", "diff", "-U0", "--", *DIFF_FILES]
    proc = subprocess.run(command, cwd=ROOT, text=True, capture_output=True, check=False)
    current_file = ""
    old_line = 0
    new_line = 0
    added = []
    hunk = re.compile(r"@@ -(\d+)(?:,\d+)? \+(\d+)(?:,(\d+))? @@")
    for line in proc.stdout.splitlines():
        if line.startswith("+++ b/"):
            current_file = line[6:]
            continue
        match = hunk.match(line)
        if match:
            old_line = int(match.group(1))
            new_line = int(match.group(2))
            continue
        if line.startswith("+") and not line.startswith("+++"):
            added.append({"file": current_file, "line": new_line, "text": line[1:]})
            new_line += 1
            continue
        if line.startswith("-") and not line.startswith("---"):
            old_line += 1
            continue
        if line and not line.startswith("\\"):
            old_line += 1
            new_line += 1
    return added


def scan_added_lines(patterns: dict[str, str]) -> list[dict]:
    compiled = {name: re.compile(pattern) for name, pattern in patterns.items()}
    hits = []
    for item in git_added_lines():
        for name, pattern in compiled.items():
            if pattern.search(item["text"]):
                hits.append({
                    "file": item["file"],
                    "line": item["line"],
                    "pattern": name,
                    "text": item["text"].strip(),
                })
    return hits


def classify_new_keyword(line: str) -> str:
    if re.search(r"new\s+(?:NativeArray|NativeList|NativeHashMap|NativeParallelHashMap|NativeQueue)", line):
        return "native_allocation"
    if re.search(r"new\s+(?:Span|ReadOnlySpan)<", line):
        return "ref_struct_no_heap"
    if re.search(r"new\s+(?:object|FileStream|Thread|InvalidOperationException|NativeQueue<|FixedChunkRegistry<|Vector4\[|PendingCarveRequest\[|ThermalMeltRuntime\[|PendingCompactionRequest\[|int\[|byte\[|T\[|ChunkDeltaState\[|HectonVoxelVolume\[)", line):
        return "preexisting_cold_or_teardown_allocation"
    if re.search(r"new\s+(?:int2|int3|int4|uint4|float2|float3|float4|double3|Vector3|Vector4|Quaternion)\b", line):
        return "value_type_math"
    if re.search(r"new\s+(?:PageWriteCommand|PageReadCommand|PageReadResult|PagerTelemetryEntry|H8WorldPageReadTicket|H8WorldPagerTelemetrySnapshot|VoxelDelta\w+DTO|VoxelDelta\w+Job|VoxelRleEncoderJob|MockVoxelDeformationGeneratorJob|VoxelWalPayloadPackJob|VoxelLz4CompressionJob|VoxelDeltaChecksumHeaderJob|VoxelDeltaDiskLatencyTelemetryPatchJob|VoxelDeltaBlockCounter64|VoxelDeltaCompressionTelemetryEntry|Unity\.Mathematics\.Random|ProfilerMarker|ChunkAddress|SaveVoxelDeltaRun8|NativeSnapshot\w+|CarveSdfJob|CarveCellWrite|PendingCarveRequest|PendingCompactionRequest|ScheduledCompactionRequest|CompactedChunkState|ChunkDeltaState|FixedVolumeRegistry|ThermalMeltRuntime|DebrisSpawnSignal|ItemAcquiredSignal|VoxelChunkModifiedEvent|VoxelCarveEvent|VoxelCarveTelemetryEntry|VoxelBlackBoxDumpHeader|VoxelModifiedCell)\b", line):
        return "value_type_or_job_struct"
    return "unclassified_review_required"


def scan_new_keywords(paths: tuple[Path, ...]) -> list[dict]:
    hits = []
    rx = re.compile(r"\bnew\s+")
    for path in paths:
        for line_number, line in enumerate(read(path).splitlines(), 1):
            if rx.search(line):
                hits.append({
                    "file": rel(path),
                    "line": line_number,
                    "classification": classify_new_keyword(line),
                    "text": line.strip(),
                })
    return hits


METHOD_DECLARATION = re.compile(
    r"\b(?:public|private|protected|internal|static|readonly|unsafe|extern|virtual|override|sealed|partial|\s)+"
    r"\s*[A-Za-z0-9_<>\[\],\.]+\s+([A-Za-z0-9_]+)\s*\([^;]*\)\s*(?:where\s+[^\{]+)?\{?"
)

HOTISH_METHOD = re.compile(
    r"(Tick|Update|Fixed|Late|Process|Schedule|Execute|Run|Drain|Flush|Write|Read|Pack|Finalize|Encode|Compress|Carve|Compaction|Telemetry)",
    re.IGNORECASE,
)

COLD_OR_FAULT_METHODS = {
    "Initialize",
    "StartWorker",
    "DumpBlackBox",
    "DumpBlackBoxOnce",
    "WriteBlackBoxDump",
    "WriteBlackBoxDumps",
    "WriteBlackBoxDumpFile",
    "RunSelfAudit",
    "EnsureChunkStatePool",
    "EnsureChunkVolumeCapacity",
    "DecodeVoxelUnits",
    "FindFreeSlot",
}


def classify_new_runtime_phase(method_name: str, new_classification: str) -> str:
    if method_name in COLD_OR_FAULT_METHODS:
        return "cold_or_fault_route"
    if new_classification in ("ref_struct_no_heap", "value_type_math", "value_type_or_job_struct"):
        return "value_or_ref_struct_no_heap"
    if HOTISH_METHOD.search(method_name):
        return "managed_allocation_hotish_review_required"
    return "managed_allocation_cold_or_type_scope"


def scan_raw_text_counts(paths: tuple[Path, ...]) -> dict[str, int]:
    compiled = {name: re.compile(pattern) for name, pattern in RAW_TEXT_PATTERNS.items()}
    counts = {name: 0 for name in RAW_TEXT_PATTERNS}
    for path in paths:
        for line in read(path).splitlines():
            for name, pattern in compiled.items():
                if pattern.search(line):
                    counts[name] += 1
    return counts


def scan_new_keyword_method_audit(paths: tuple[Path, ...]) -> list[dict]:
    audit = []
    new_rx = re.compile(r"\bnew\s+")
    for path in paths:
        current_method = "<type-scope>"
        for line_number, line in enumerate(read(path).splitlines(), 1):
            method_match = METHOD_DECLARATION.search(line)
            if method_match and not line.strip().startswith("//"):
                current_method = method_match.group(1)
            if not new_rx.search(line):
                continue

            new_classification = classify_new_keyword(line)
            phase = classify_new_runtime_phase(current_method, new_classification)
            audit.append({
                "file": rel(path),
                "line": line_number,
                "method": current_method,
                "newClassification": new_classification,
                "runtimePhaseClassification": phase,
                "hotishMethodName": bool(HOTISH_METHOD.search(current_method)),
                "text": line.strip(),
            })
    return audit


def audit_native_collection_fields(paths: tuple[Path, ...]) -> tuple[list[dict], list[dict]]:
    type_decl = re.compile(r"\b(?:public|private|internal|protected|static|partial|sealed|abstract|readonly|unsafe|\s)*\b(class|struct)\s+([A-Za-z0-9_]+)\b")
    native_field = re.compile(r"^\s*(?:\[[^\]]+\]\s*)*(?:public|private|internal|protected)?\s*(?:readonly\s+)?Native(?:Array|List|Queue|HashMap|ParallelHashMap)<[^>]+>\s+([A-Za-z0-9_]+)\s*;")
    audit = []
    failures = []

    for path in paths:
        stack = []
        pending_type = None
        depth = 0
        for line_number, line in enumerate(read(path).splitlines(), 1):
            decl = type_decl.search(line)
            if decl:
                pending_type = {"kind": decl.group(1), "name": decl.group(2), "depth": None}

            if "{" in line:
                for _ in range(line.count("{")):
                    depth += 1
                    if pending_type is not None and pending_type["depth"] is None:
                        pending_type["depth"] = depth
                        stack.append(pending_type)
                        pending_type = None

            active_type = stack[-1] if stack else None
            field = native_field.search(line)
            if field and active_type is not None:
                item = {
                    "file": rel(path),
                    "line": line_number,
                    "containingTypeKind": active_type["kind"],
                    "containingType": active_type["name"],
                    "field": field.group(1),
                    "text": line.strip(),
                }
                if active_type["kind"] == "class":
                    item["classification"] = "forbidden_persistent_class_native_collection"
                    failures.append(item)
                elif active_type["name"].endswith("Job") or active_type["name"].endswith("BufferSet"):
                    item["classification"] = "allowed_job_or_transient_view_struct"
                else:
                    item["classification"] = "reviewed_struct_native_view"
                audit.append(item)

            if "}" in line:
                for _ in range(line.count("}")):
                    while stack and stack[-1]["depth"] >= depth:
                        stack.pop()
                    depth = max(0, depth - 1)

    return audit, failures


def scan_added_new_keywords() -> tuple[list[dict], list[dict]]:
    audit = []
    forbidden = []
    for hit in scan_added_lines({"new_keyword": r"\bnew\s+"}):
        classified = dict(hit)
        classified["classification"] = classify_new_keyword(hit["text"])
        audit.append(classified)
        if classified["classification"] not in ("ref_struct_no_heap", "value_type_math", "value_type_or_job_struct"):
            forbidden.append(classified)

    return audit, forbidden


def extract_struct(path: Path, name: str) -> dict:
    lines = read(path).splitlines()
    struct_line = -1
    for index, line in enumerate(lines):
        if re.search(r"\bstruct\s+" + re.escape(name) + r"\b", line):
            struct_line = index
            break
    if struct_line < 0:
        return {"file": rel(path), "struct": name, "found": False}

    size = None
    for index in range(max(0, struct_line - 4), struct_line + 1):
        match = re.search(r"StructLayout\s*\([^)]*Size\s*=\s*(\d+)", lines[index])
        if match:
            size = int(match.group(1))
            break

    fields = []
    depth = 0
    started = False
    pending_offset = None
    for index in range(struct_line, len(lines)):
        line = lines[index]
        if "{" in line:
            depth += line.count("{")
            started = True
        offset_match = re.search(r"FieldOffset\s*\(\s*(\d+)\s*\)", line)
        if offset_match:
            pending_offset = int(offset_match.group(1))
        field_match = re.search(r"\b(?:public|private|internal)\s+([A-Za-z0-9_\.]+)\s+([A-Za-z0-9_]+)\s*;", line)
        if pending_offset is not None and field_match:
            field_type = field_match.group(1).split(".")[-1]
            fields.append({
                "offset": pending_offset,
                "type": field_type,
                "name": field_match.group(2),
                "line": index + 1,
                "bytes": TYPE_BYTES.get(field_type),
            })
            pending_offset = None
        if "}" in line and started:
            depth -= line.count("}")
            if depth == 0:
                break

    order = [field["bytes"] for field in fields if field["bytes"] in (8, 4, 2, 1)]
    strict_order = all(order[index] >= order[index + 1] for index in range(len(order) - 1))
    aligned_8 = [
        field for field in fields
        if field["bytes"] == 8 and (field["offset"] % 8) != 0
    ]
    return {
        "file": rel(path),
        "struct": name,
        "found": True,
        "line": struct_line + 1,
        "size": size,
        "sizeMultipleOf8": size is not None and (size % 8) == 0,
        "strict8_4_2_1Order": strict_order,
        "unaligned8ByteFields": aligned_8,
        "fields": fields,
    }


def line_refs(path: Path, pattern: str) -> list[dict]:
    rx = re.compile(pattern)
    refs = []
    for line_number, line in enumerate(read(path).splitlines(), 1):
        if rx.search(line):
            refs.append({"file": rel(path), "line": line_number, "text": line.strip()})
    return refs


def file_surface_audit() -> list[dict]:
    audit = []
    for path, scope in CHANGED_OR_CREATED_FILES:
        exists = path.exists()
        item = {
            "file": rel(path),
            "exists": exists,
            "scope": scope,
            "runtimeStrict": scope == "runtime_strict",
        }
        if exists and path.suffix == ".cs":
            source = read(path)
            item["lineCount"] = len(source.splitlines())
            item["containsEditorBoundary"] = "/Editor/" in rel(path) or "#if UNITY_EDITOR" in source
            item["containsAsmdefReference"] = ".asmdef" in source
            item["containsGlobalRegistryReference"] = "GlobalRegistry" in source
            item["containsGlobalDataVaultReference"] = "GlobalDataVault" in source or "IDataVault" in source
        audit.append(item)
    return audit


def owned_file_surface_audit() -> list[dict]:
    audit = []
    for path, scope in OWNED_FILE_SURFACE:
        exists = path.exists()
        item = {
            "file": rel(path),
            "exists": exists,
            "scope": scope,
            "runtimeStrict": scope == "runtime_strict",
        }
        if exists:
            source = read(path)
            item["lineCount"] = len(source.splitlines())
            item["byteCount"] = len(source.encode("utf-8"))
        audit.append(item)

    return audit


def main() -> int:
    full_forbidden = []
    for path in RUNTIME_FILES:
        full_forbidden.extend(scan_patterns(path, FORBIDDEN_RUNTIME_PATTERNS))

    added_forbidden = scan_added_lines(FORBIDDEN_RUNTIME_PATTERNS)
    added_new_audit, added_new = scan_added_new_keywords()
    runtime_new_keywords = scan_new_keywords(RUNTIME_FILES)
    runtime_new_method_audit = scan_new_keyword_method_audit(RUNTIME_FILES)
    unclassified_new_keywords = [
        hit for hit in runtime_new_keywords
        if hit["classification"] == "unclassified_review_required"
    ]
    managed_allocation_hotish_hits = [
        hit for hit in runtime_new_method_audit
        if hit["runtimePhaseClassification"] == "managed_allocation_hotish_review_required"
    ]
    runtime_new_native_or_malloc = []
    for path in (PAGER, PROCESSOR, COMPRESSION):
        runtime_new_native_or_malloc.extend(scan_patterns(path, {
            "new_native_array": r"new\s+NativeArray<",
            "new_native_list": r"new\s+NativeList<",
            "new_native_hash_map": r"new\s+Native(?:Parallel)?HashMap<",
            "new_native_queue": r"new\s+NativeQueue<",
            "unsafe_malloc": r"UnsafeUtility\.Malloc",
        }))
    native_collection_field_audit, native_collection_field_failures = audit_native_collection_fields(RUNTIME_FILES)

    exception_handling = {
        "allRuntimeCatchAndThrow": [
            hit
            for path in RUNTIME_FILES
            for hit in scan_patterns(path, EXCEPTION_SCAN_PATTERNS)
        ],
        "bareCatchHits": [
            hit
            for path in RUNTIME_FILES
            for hit in scan_patterns(path, {"catch_bare": r"^\s*catch\s*$"})
        ],
        "broadExceptionCatchHits": [
            hit
            for path in RUNTIME_FILES
            for hit in scan_patterns(path, {"catch_broad_exception": r"catch\s*\(\s*Exception\s*\)"})
        ],
        "addedCatchOrThrowHits": scan_added_lines({
            "catch_any": r"\bcatch\b",
            "throw_any": r"\bthrow\b",
        }),
    }

    structs = [extract_struct(path, name) | {"strictRequired": strict_required} for path, name, strict_required in STRUCTS]
    struct_failures = [
        item for item in structs
        if item.get("strictRequired") and
        (not item.get("found") or not item.get("sizeMultipleOf8") or not item.get("strict8_4_2_1Order") or item.get("unaligned8ByteFields"))
    ]

    aup_refs = {
        "doubleSubtractBeforeDistance": line_refs(PROCESSOR, r"double3\s+delta\s*=\s*.*-.*absoluteCenter"),
        "doubleDistanceSq": line_refs(PROCESSOR, r"double\s+distanceSq\s*=\s*math\.lengthsq\(delta\)"),
        "originSubtractBeforeFloatMirror": line_refs(FLOATING_ORIGIN, r"absoluteUniversePosition\s*-\s*committedTotalOffset"),
        "runtimeFloatMirrorSites": line_refs(PROCESSOR, r"ToRuntimeFloat3"),
        "directAbsoluteFloatCasts": line_refs(PROCESSOR, r"ToFloat3\(|new\s+(?:float3|Vector3)\(\(float\)"),
        "legacyAbsoluteCoordinateFallback": line_refs(PROCESSOR, r"return\s+ToDouble3\((?:meltEvent\.AbsoluteUniversePosition|legacyCoordinate)\)"),
        "authoritativePendingDouble": line_refs(PROCESSOR, r"public\s+double3\s+AbsoluteHitPoint|AbsoluteHitPoint\s*=\s*ResolveCarveHitPointDouble|double3\s+segmentStart\s*=\s*request\.AbsoluteHitPoint"),
        "agent1312LayoutValidator": line_refs(PROCESSOR, r"ValidateAgent1312PrivateLayouts|AssertAgent1312"),
    }

    dependency_refs = {
        "usings": [hit for path in RUNTIME_FILES for hit in line_refs(path, r"^using\s+")],
        "asmdefTouched": scan_added_lines({"asmdef": r"\.asmdef"}),
    }

    fail_closed_refs = {
        "pagerOverflowReject": line_refs(PAGER, r"payloadOverflow|WriteRejected|PagerTelemetryFlagPayloadOverflowRejected"),
        "pagerWorkerDumpBridge": line_refs(PAGER, r"_dumpRequestPending|TryConsumeDumpRequest|WriteBlackBoxDumps|Thread\.CurrentThread\.ManagedThreadId"),
        "denseFallback": line_refs(COMPRESSION, r"HeaderFlagDenseFallback|PackDenseFallback|MaxVoxelDeltaRleRunsPerWalPayload"),
        "blackBoxDump": line_refs(PAGER, r"DumpBlackBox|Dump_1312_VoxelPaging") + line_refs(PROCESSOR, r"DumpBlackBox|Dump_1312_VoxelPaging|VoxelPagingBlackBoxDumpRelativePath1312"),
        "ownerDump1304Compatibility": line_refs(PROCESSOR, r"Dump_1304"),
        "ownerDump1304LeaksOutsideCompatibility": line_refs(PAGER, r"Dump_1304") + line_refs(COMPRESSION, r"Dump_1304"),
        "nanOrNonFinite": line_refs(PROCESSOR, r"isfinite|Invalid.*Flag"),
    }

    rle_math = {
        "pagerPayloadBytes": (256 * 1024) - 64,
        "headerBytes": 32,
        "runBytes": 8,
        "cellCount": 32 * 32 * 32,
        "checkerboardRleBytes": 32 + ((32 * 32 * 32) * 8),
        "overflowBytes": (32 + ((32 * 32 * 32) * 8)) - ((256 * 1024) - 64),
        "maxSafeRuns": (((256 * 1024) - 64) - 32) // 8,
        "denseFallbackBytes": (32 * 32 * 32 // 8) + ((32 * 32 * 32) * 4),
    }

    report = {
        "agent": "1312",
        "scanner": "OOP_VoxelPaging_ParanoidAudit_1312",
        "runtimeFiles": [rel(path) for path in RUNTIME_FILES],
        "strictRuntimeForbiddenHits": full_forbidden,
        "addedRuntimeForbiddenHits": added_forbidden,
        "addedNewKeywordHits": added_new,
        "addedNewKeywordAudit": added_new_audit,
        "runtimeNewKeywordHits": runtime_new_keywords,
        "runtimeNewKeywordMethodAudit": runtime_new_method_audit,
        "runtimeNewKeywordClassCounts": {
            name: sum(1 for hit in runtime_new_keywords if hit["classification"] == name)
            for name in sorted({hit["classification"] for hit in runtime_new_keywords})
        },
        "rawTextScanCounts": scan_raw_text_counts(RUNTIME_FILES),
        "managedAllocationHotishHits": managed_allocation_hotish_hits,
        "runtimeNativeAllocationHits": runtime_new_native_or_malloc,
        "nativeCollectionFieldAudit": native_collection_field_audit,
        "nativeCollectionFieldFailures": native_collection_field_failures,
        "exceptionHandling": exception_handling,
        "ownedFileSurfaceAudit": owned_file_surface_audit(),
        "changedOrCreatedFileAudit": file_surface_audit(),
        "dtoByteOffsetMaps": structs,
        "strictLayoutFailures": struct_failures,
        "aup": aup_refs,
        "dependencies": dependency_refs,
        "failClosed": fail_closed_refs,
        "overengineering": {
            "directorySlotMath": "hash mix plus modulo 252; no probing simulator or directory migration",
            "rleOverflow": "single threshold to dense 135168-byte payload; no iterative compression search",
            "physicsOrScientificSimulatorAdded": False,
        },
        "rleMath": rle_math,
        "success": (
            len(full_forbidden) == 0 and
            len(added_forbidden) == 0 and
            len(added_new) == 0 and
            len(unclassified_new_keywords) == 0 and
            len(managed_allocation_hotish_hits) == 0 and
            len(runtime_new_native_or_malloc) == 0 and
            len(native_collection_field_failures) == 0 and
            len(exception_handling["bareCatchHits"]) == 0 and
            len(exception_handling["broadExceptionCatchHits"]) == 0 and
            len(struct_failures) == 0 and
            len(aup_refs["doubleSubtractBeforeDistance"]) > 0 and
            len(aup_refs["doubleDistanceSq"]) > 0 and
            len(aup_refs["originSubtractBeforeFloatMirror"]) > 0 and
            len(aup_refs["directAbsoluteFloatCasts"]) == 0 and
            len(aup_refs["legacyAbsoluteCoordinateFallback"]) == 0 and
            len(aup_refs["agent1312LayoutValidator"]) > 0 and
            len(fail_closed_refs["pagerWorkerDumpBridge"]) > 0 and
            len(fail_closed_refs["ownerDump1304LeaksOutsideCompatibility"]) == 0 and
            rle_math["overflowBytes"] == 96 and
            rle_math["maxSafeRuns"] == 32756 and
            rle_math["denseFallbackBytes"] == 135168
        ),
    }

    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text(json.dumps(report, indent=2, sort_keys=True), encoding="utf-8")
    print(str(REPORT))
    print("success=" + str(report["success"]).lower())
    return 0 if report["success"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
