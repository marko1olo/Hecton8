#!/usr/bin/env python3
"""Static gate for AGENT 1311 signal corridor work.

The scanner is intentionally conservative. Any surviving NativeQueue bridge in
SignalBusRuntime or any NativeQueue writer for a SignalBus-owned payload is RED.
"""

from __future__ import annotations

import json
import re
from pathlib import Path
from typing import Dict, Iterable, List, Tuple


ROOT = Path(__file__).resolve().parents[1]
TARGETS = [
    ROOT / "Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs",
    ROOT / "Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs",
    ROOT / "Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyWriters.cs",
]
PHASE_FILES = [
    ROOT / "Assets/_Project/Scripts/Core/SystemDispatcher.cs",
    ROOT / "Assets/_Project/Scripts/Core/Signals/SignalCorridorRuntime.cs",
    ROOT / "Assets/_Project/Scripts/Core/Signals/GlobalSignals.RuntimeLifecycle.cs",
    ROOT / "Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs",
]
CONTRACTS = [
    ROOT / "Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs",
    ROOT / "Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs",
]
ASMDEFS = [
    ROOT / "Assets/_Project/Scripts/Hecton8.Core.asmdef",
    ROOT / "Assets/_Project/Scripts/Core/Contracts/Hecton8.Core.Contracts.asmdef",
]
REPORT_JSON = ROOT / "Docs/Reports/SIGNAL_SPSC_OPTIMIZATION_REPORT_1311.json"
REPORT_MD = ROOT / "Docs/Reports/SIGNAL_SPSC_OPTIMIZATION_REPORT_1311.md"
FUZZER = ROOT / "Assets/_Project/Scripts/Core/Signals/SignalStormConcurrencyFuzzer1311.cs"

BYTE_MAP_SPECS = {
    "SignalRingCursorState": ROOT / "Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs",
    "SignalLaneDispatch": ROOT / "Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs",
    "SignalLaneTelemetry": ROOT / "Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs",
    "SignalTelemetryFrame": ROOT / "Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs",
    "SignalStormFuzzerPayload1311": FUZZER,
    "SignalStormFuzzerResult1311": FUZZER,
}

TOKEN_PATTERNS = {
    "new_expression": re.compile(r"\bnew\s+[\w<>\.:]+(?:\s*\(|\s*\[)"),
    "native_queue": re.compile(r"\bNativeQueue\s*<"),
    "native_queue_writer": re.compile(r"\bNativeQueue\s*<[^>]+>\.ParallelWriter"),
    "new_native_queue": re.compile(r"new\s+NativeQueue\s*<"),
    "new_native_array": re.compile(r"new\s+NativeArray\s*<"),
    "enqueue": re.compile(r"\.Enqueue\s*\("),
    "try_dequeue": re.compile(r"\.TryDequeue\s*\("),
    "count_property": re.compile(r"\.Count\b"),
    "string_format": re.compile(r"\bstring\.Format\s*\("),
    "to_string": re.compile(r"\.ToString\s*\("),
    "linq": re.compile(r"\bSystem\.Linq\b|\bEnumerable\."),
    "foreach": re.compile(r"\bforeach\s*\("),
    "interpolated_string": re.compile(r'\$"'),
    "debug_log": re.compile(r"\bDebug\.Log(?:Error|Warning)?\s*\("),
    "h8debug_log": re.compile(r"\bH8Debug\.Log(?:Error|Warning)?\s*\("),
    "throw": re.compile(r"\bthrow\b"),
    "full_name": re.compile(r"\.FullName\b"),
    "managed_delegate_declaration": re.compile(r"\bdelegate\s+(?!\*)"),
    "managed_static_array": re.compile(r"static\s+readonly\s+[\w<>\.]+\[\]"),
    "requested_dump_path": re.compile(r"Dump_1311_SignalCorridor\.bin"),
    "existing_signal_dump_path": re.compile(r"Dump_SIGNAL_CORRIDOR\.bin"),
}

PHASE_PATTERNS = {
    "dispatcher_pre_sim_heartbeat": re.compile(r"SignalCorridorRuntime\.PreSimulationHeartbeat\s*\("),
    "dispatcher_post_sim_flush": re.compile(r"SignalCorridorRuntime\.FlushPostSimulation\s*\("),
    "dispatcher_pre_sim_flush": re.compile(r"SignalCorridorRuntime\.FlushPreSimulation\s*\("),
    "dispatcher_post_sim_clear": re.compile(r"SignalCorridorRuntime\.ClearPostSimulationSnapshots\s*\("),
    "registry_post_sim_flush": re.compile(r"SignalBusRegistry\.FlushPostSimulation\s*\("),
    "registry_pre_sim_flush": re.compile(r"SignalBusRegistry\.FlushPreSimulation\s*\("),
    "snapshot_clear_delegate": re.compile(r"SignalLaneClearDelegate|ClearRegisteredSignalLaneSnapshots|_clearDispatch"),
}

FAIL_CLOSED_PATTERNS = {
    "registration_gate_field": re.compile(r"\b_registrationGate\b"),
    "registration_gate_enter": re.compile(r"EnterRegistrationGate\s*\("),
    "registration_gate_exit": re.compile(r"ExitRegistrationGate\s*\("),
    "registration_gate_compare_exchange": re.compile(r"Interlocked\.CompareExchange\s*\(\s*ref\s+_registrationGate\s*,\s*1\s*,\s*0\s*\)"),
    "registration_gate_release": re.compile(r"Volatile\.Write\s*\(\s*ref\s+_registrationGate\s*,\s*0\s*\)"),
    "registration_returns_bool": re.compile(r"internal\s+static\s+bool\s+Register\s*\("),
    "registered_latch_from_result": re.compile(r"_registered\s*=\s*SignalBusRegistry\.Register\s*\("),
    "registration_overflow_log_once": re.compile(r"Interlocked\.Exchange\s*\(\s*ref\s+_registrationOverflow\s*,\s*1\s*\)"),
    "spsc_partial_allocation_cleanup": re.compile(r"!_buffer\.IsCreated\s*\|\|\s*!_cursor\.IsCreated"),
    "mpsc_partial_allocation_cleanup": re.compile(r"!_buffer\.IsCreated\s*\|\|\s*!_publishedTickets\.IsCreated\s*\|\|\s*!_cursor\.IsCreated"),
    "failed_ring_check": re.compile(r"if\s*\(!_ring\.IsCreated\)"),
    "ring_dispose_on_failure": re.compile(r"_ring\.Dispose\s*\(\s*\)"),
    "frame_snapshot_release_on_failure": re.compile(r"ReleaseFrameSnapshotBuffer\s*\(\s*\)"),
    "async_dump_request": re.compile(r"RequestDumpToDiskAsync\s*\("),
    "ring_clear_drop_to_tail": re.compile(r"Interlocked\.Exchange\s*\(\s*ref\s+cursor->Head\s*,\s*tail\s*\)"),
    "ring_clear_tail_reset": re.compile(r"Interlocked\.Exchange\s*\(\s*ref\s+cursor->Tail\s*,\s*0L\s*\)"),
    "ring_clear_ticket_loop": re.compile(r"_publishedTickets\.Length"),
    "fuzzer_allocation_fail_closed": re.compile(r"!ring\.IsCreated\s*\|\|\s*!seen\.IsCreated"),
    "dispatch_storage_guard": re.compile(r"!_laneDispatch\.IsCreated"),
    "dispatch_length_clamp": re.compile(r"Math\.Min\s*\(\s*Volatile\.Read\(ref _laneCount\)\s*,\s*_laneDispatch\.Length\s*\)"),
    "writer_sanitize_before_budget": re.compile(r"writerGuardCode\s*=\s*SignalPayloadFiniteGuards\.Sanitize\s*\(\s*ref\s+signal\s*\)"),
    "writer_corrupt_drop": re.compile(r"if\s*\(writerGuardCode\s*!=\s*0\)"),
}

CALLSITE_PATTERNS = {
    "native_queue_writer_field": re.compile(r"\bNativeQueue\s*<[^>]+>\.ParallelWriter\b"),
    "signalbus_ring_writer_request": re.compile(r"\bSignalBus\s*<[^>]+>\.(?:ParallelWriter|OpenParallelWriter|RingParallelWriter)\b"),
    "signalbus_try_enqueue_bounded": re.compile(r"\bSignalBus\s*<[^>]+>\.TryEnqueueBounded\s*\("),
}


def rel(path: Path) -> str:
    return str(path.relative_to(ROOT)).replace("\\", "/")


def read_lines(path: Path) -> List[str]:
    return path.read_text(encoding="utf-8-sig", errors="replace").splitlines()


def scan_lines(paths: Iterable[Path], patterns: Dict[str, re.Pattern[str]]) -> Dict[str, List[dict]]:
    hits: Dict[str, List[dict]] = {key: [] for key in patterns}
    for path in paths:
        if not path.exists():
            continue
        for number, line in enumerate(read_lines(path), start=1):
            for key, pattern in patterns.items():
                if pattern.search(line):
                    hits[key].append(
                        {
                            "file": rel(path),
                            "line": number,
                            "text": line.strip(),
                        }
                    )
    return hits


def scan_callsites() -> Dict[str, List[dict]]:
    cs_files = (ROOT / "Assets/_Project/Scripts").rglob("*.cs")
    return scan_lines(cs_files, CALLSITE_PATTERNS)


def scan_signalbus_nativequeue_writer_intersections() -> List[dict]:
    cs_files = list((ROOT / "Assets/_Project/Scripts").rglob("*.cs"))
    signalbus_types = set()
    request_pattern = re.compile(
        r"\bSignalBus\s*<\s*([^>]+?)\s*>\s*\.\s*"
        r"(?:ParallelWriter\b|OpenParallelWriter\s*\(|RingParallelWriter\b|TryEnqueueBounded\s*\()"
    )
    native_pattern = re.compile(r"\bNativeQueue\s*<\s*([^>]+?)\s*>\s*\.\s*ParallelWriter\b")

    for path in cs_files:
        for line in read_lines(path):
            for match in request_pattern.finditer(line):
                signalbus_types.add(match.group(1).strip())

    hits: List[dict] = []
    for path in cs_files:
        for number, line in enumerate(read_lines(path), start=1):
            for match in native_pattern.finditer(line):
                signal_type = match.group(1).strip()
                if signal_type not in signalbus_types:
                    continue
                hits.append(
                    {
                        "file": rel(path),
                        "line": number,
                        "type": signal_type,
                        "text": line.strip(),
                    }
                )

    return hits


def parse_field_offsets(path: Path, struct_name: str) -> List[dict]:
    lines = read_lines(path)
    start = -1
    brace_depth = 0
    in_struct = False
    fields: List[dict] = []
    struct_pattern = re.compile(rf"\bstruct\s+{re.escape(struct_name)}\b")
    field_pattern = re.compile(
        r"\[FieldOffset\((\d+)\)\]\s+"
        r"(?:public|private|internal)\s+"
        r"(?:readonly\s+)?"
        r"(.+?)\s+(\w+)\s*(?:=|;)"
    )

    for index, line in enumerate(lines):
        if not in_struct and struct_pattern.search(line):
            start = index
            in_struct = True

        if not in_struct:
            continue

        brace_depth += line.count("{")
        brace_depth -= line.count("}")
        match = field_pattern.search(line)
        if match:
            fields.append(
                {
                    "offset": int(match.group(1)),
                    "type": match.group(2),
                    "name": match.group(3),
                    "line": index + 1,
                    "file": rel(path),
                }
            )

        if start >= 0 and brace_depth <= 0 and index > start:
            break

    return sorted(fields, key=lambda item: item["offset"])


def detect_struct_size(path: Path, struct_name: str) -> int | None:
    lines = read_lines(path)
    size_pattern = re.compile(r"StructLayout\(LayoutKind\.Explicit,\s*Size\s*=\s*(\d+)\)")
    struct_pattern = re.compile(rf"\bstruct\s+{re.escape(struct_name)}\b")
    last_size: int | None = None
    for line in lines:
        size_match = size_pattern.search(line)
        if size_match:
            last_size = int(size_match.group(1))
        if struct_pattern.search(line):
            return last_size
    return None


def load_asmdef(path: Path) -> dict:
    if not path.exists():
        return {"file": rel(path), "missing": True}
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    return {
        "file": rel(path),
        "name": data.get("name"),
        "rootNamespace": data.get("rootNamespace"),
        "references": data.get("references", []),
        "allowUnsafeCode": data.get("allowUnsafeCode"),
        "noEngineReferences": data.get("noEngineReferences"),
    }


def byte_map() -> Dict[str, dict]:
    maps: Dict[str, Tuple[Path, int | None]] = {
        name: (path, detect_struct_size(path, name))
        for name, path in BYTE_MAP_SPECS.items()
    }
    result: Dict[str, dict] = {}
    for name, (path, size) in maps.items():
        fields = parse_field_offsets(path, name)
        order_violations = field_order_violations(fields)
        result[name] = {
            "file": rel(path),
            "declaredSizeBytes": size,
            "multipleOf8": bool(size and size % 8 == 0),
            "multipleOf64": bool(size and size % 64 == 0),
            "largestToSmallestFieldOrder": not order_violations,
            "fieldOrderViolations": order_violations,
            "fields": fields,
        }
    return result


def field_size_bytes(type_name: str) -> int:
    type_name = type_name.rstrip("*")
    if type_name.startswith("delegate*"):
        return 8
    if type_name in {"long", "ulong", "double", "IntPtr", "UIntPtr"}:
        return 8
    if type_name in {"int", "uint", "float"}:
        return 4
    if type_name in {"short", "ushort"}:
        return 2
    if type_name in {"byte", "sbyte"}:
        return 1
    return 0


def field_order_violations(fields: List[dict]) -> List[dict]:
    violations: List[dict] = []
    largest_seen_floor = 8
    for field in fields:
        size = field_size_bytes(field["type"])
        if size == 0:
            continue
        if size > largest_seen_floor:
            violations.append(
                {
                    "file": field["file"],
                    "line": field["line"],
                    "field": field["name"],
                    "type": field["type"],
                    "sizeBytes": size,
                    "reason": "larger field appears after smaller field",
                }
            )
        if size < largest_seen_floor:
            largest_seen_floor = size
    return violations


def classify_new_expression(hit: dict) -> dict:
    text = hit["text"]
    classification = "unknown"
    hot_path = "unknown"
    managed_heap = "unknown"
    if "SignalLaneDisposeDelegate[" in text or "SignalLaneTelemetryDelegate[" in text or "SignalLaneDispatch[" in text:
        classification = "cold_static_registry_array"
        hot_path = "no"
        managed_heap = "yes"
    elif "new SignalLaneDispatch" in text:
        classification = "value_type_dispatch_record"
        hot_path = "registration"
        managed_heap = "no"
    elif "new global::Hecton8.Core.MpscSignalRingBuffer" in text:
        classification = "native_container_struct_construction"
        hot_path = "lane_bootstrap"
        managed_heap = "no"
    elif "new ReadOnlySpan" in text:
        classification = "ref_struct_view"
        hot_path = "read_accessor"
        managed_heap = "no"
    elif "new float3" in text:
        classification = "value_type_float3"
        hot_path = "sanitize_default"
        managed_heap = "no"
    elif "new ParallelWriter" in text:
        classification = "value_type_parallel_writer"
        hot_path = "writer_open"
        managed_heap = "no"
    return {
        "file": hit["file"],
        "line": hit["line"],
        "text": text,
        "classification": classification,
        "hotPath": hot_path,
        "managedHeap": managed_heap,
    }


def cursor_alignment_ok(struct_map: dict) -> bool:
    fields = struct_map.get("fields", [])
    head = next((field for field in fields if field["name"] == "Head"), None)
    tail = next((field for field in fields if field["name"] == "Tail"), None)
    return (
        struct_map.get("declaredSizeBytes") == 128
        and head is not None
        and tail is not None
        and head["offset"] == 0
        and head["type"] == "long"
        and tail["offset"] == 64
        and tail["type"] == "long"
    )


def classify_status(
    token_hits: Dict[str, List[dict]],
    callsite_hits: Dict[str, List[dict]],
    signalbus_nativequeue_writer_hits: List[dict],
    maps: Dict[str, dict],
    dump_path_hits: Dict[str, List[dict]],
    phase_hits: Dict[str, List[dict]],
    fail_closed_hits: Dict[str, List[dict]],
) -> Tuple[str, List[str]]:
    reasons: List[str] = []
    if not cursor_alignment_ok(maps["SignalRingCursorState"]):
        reasons.append("SignalRingCursorState cursor layout is not 128B/head@0/tail@64 long.")
    if token_hits["new_native_queue"]:
        reasons.append("SignalBusRuntime still allocates a NativeQueue legacy bridge.")
    if token_hits["native_queue_writer"]:
        reasons.append("SignalBusRuntime still exposes NativeQueue<T>.ParallelWriter APIs.")
    if signalbus_nativequeue_writer_hits:
        reasons.append("Project still has NativeQueue<T>.ParallelWriter fields for types also using SignalBus<T> writer APIs.")
    if token_hits["string_format"] or token_hits["to_string"] or token_hits["linq"] or token_hits["interpolated_string"]:
        reasons.append("Managed formatting/LINQ/interpolation token found in target runtime files.")
    if token_hits["managed_delegate_declaration"] or token_hits["managed_static_array"]:
        reasons.append("Managed delegate declaration or static managed array remains in target runtime files.")
    for name, data in maps.items():
        if not data.get("multipleOf8"):
            reasons.append(f"{name} declared size is not multiple of 8.")
        if data.get("fieldOrderViolations"):
            reasons.append(f"{name} violates largest-to-smallest ARM64 field order.")
    if not dump_path_hits["requested_dump_path"]:
        reasons.append("Requested Dump_1311_SignalCorridor.bin path is absent from scanned signal corridor files.")
    if phase_hits["dispatcher_pre_sim_flush"] or phase_hits["registry_pre_sim_flush"]:
        reasons.append("A PRE_SIMULATION signal-corridor flush call remains active.")
    if phase_hits["dispatcher_post_sim_clear"] or phase_hits["snapshot_clear_delegate"]:
        reasons.append("A post-simulation snapshot clear route remains in the active registry path.")
    if not phase_hits["dispatcher_post_sim_flush"] or not phase_hits["registry_post_sim_flush"]:
        reasons.append("POST_SIMULATION signal-corridor flush route is incomplete.")
    required_fail_closed = (
        "registration_gate_compare_exchange",
        "registration_gate_release",
        "registration_returns_bool",
        "registered_latch_from_result",
        "registration_overflow_log_once",
        "spsc_partial_allocation_cleanup",
        "mpsc_partial_allocation_cleanup",
        "failed_ring_check",
        "ring_dispose_on_failure",
        "frame_snapshot_release_on_failure",
        "async_dump_request",
        "ring_clear_drop_to_tail",
        "fuzzer_allocation_fail_closed",
        "dispatch_storage_guard",
        "dispatch_length_clamp",
        "writer_sanitize_before_budget",
        "writer_corrupt_drop",
    )
    for key in required_fail_closed:
        if not fail_closed_hits[key]:
            reasons.append(f"Fail-closed proof missing: {key}.")
    if fail_closed_hits["ring_clear_tail_reset"]:
        reasons.append("Ring Clear resets Tail; unsafe for concurrent MPSC producers.")
    if fail_closed_hits["ring_clear_ticket_loop"]:
        reasons.append("Ring Clear still loops over publication tickets instead of O(1) drop-by-head.")
    if not FUZZER.exists():
        reasons.append("Signal storm concurrency fuzzer source file is absent.")
    if not reasons:
        return "GREEN_STATIC_ONLY", ["No red token found by static scan. Runtime/profiler proof still absent."]
    return "RED", reasons


def aup_scan() -> List[dict]:
    patterns = {
        "aup": re.compile(r"AbsoluteUniversePosition|AbsoluteUniversePositionBlit|double3|float3|Vector3|Transform\.position")
    }
    return scan_lines(TARGETS, patterns)["aup"]


def main() -> int:
    token_hits = scan_lines(TARGETS, TOKEN_PATTERNS)
    dump_path_hits = scan_lines(TARGETS + CONTRACTS, {
        "requested_dump_path": TOKEN_PATTERNS["requested_dump_path"],
        "existing_signal_dump_path": TOKEN_PATTERNS["existing_signal_dump_path"],
    })
    phase_hits = scan_lines(PHASE_FILES, PHASE_PATTERNS)
    fail_closed_hits = scan_lines(TARGETS + CONTRACTS + [FUZZER], FAIL_CLOSED_PATTERNS)
    callsite_hits = scan_callsites()
    signalbus_nativequeue_writer_hits = scan_signalbus_nativequeue_writer_intersections()
    maps = byte_map()
    status, reasons = classify_status(
        token_hits,
        callsite_hits,
        signalbus_nativequeue_writer_hits,
        maps,
        dump_path_hits,
        phase_hits,
        fail_closed_hits)
    new_expression_classifications = [
        classify_new_expression(hit)
        for hit in token_hits["new_expression"]
    ]

    report = {
        "agent": 1311,
        "role": "SIGNAL_CORRIDOR_SPSC_ARCHITECT",
        "status": status,
        "statusReasons": reasons,
        "targetFiles": [rel(path) for path in TARGETS],
        "byteOffsetMaps": maps,
        "tokenHits": token_hits,
        "newExpressionClassifications": new_expression_classifications,
        "phaseHits": phase_hits,
        "failClosedHits": fail_closed_hits,
        "dumpPathHits": dump_path_hits,
        "projectCallsiteHits": callsite_hits,
        "signalbusNativeQueueWriterHits": signalbus_nativequeue_writer_hits,
        "fuzzer": {
            "file": rel(FUZZER),
            "exists": FUZZER.exists(),
        },
        "aupHitsInTargetFiles": aup_scan(),
        "asmdefs": [load_asmdef(path) for path in ASMDEFS],
        "hardFacts": {
            "cursorAlignment": "SignalRingCursorState is GREEN only if declared 128B, Head long at 0, Tail long at 64.",
            "nativeQueue": "Any NativeQueue hit in SignalBusRuntime or NativeQueue writer for a SignalBus-owned payload is RED.",
            "runtimeProof": "No Unity, Burst, IL2CPP, profiler, or GCMonitor proof is produced by this scanner.",
        },
    }

    REPORT_JSON.parent.mkdir(parents=True, exist_ok=True)
    REPORT_JSON.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")

    md_lines = [
        "# SIGNAL SPSC OPTIMIZATION REPORT 1311",
        "",
        f"Status: {status}",
        "",
        "## Status Reasons",
    ]
    for reason in reasons:
        md_lines.append(f"- {reason}")
    md_lines.extend(["", "## Byte Offset Maps"])
    for name, data in maps.items():
        md_lines.append(f"- {name}: size={data['declaredSizeBytes']} multiple8={data['multipleOf8']} file={data['file']}")
        for field in data["fields"]:
            md_lines.append(
                f"  - offset {field['offset']}: {field['type']} {field['name']} ({field['file']}:{field['line']})"
            )
    md_lines.extend(["", "## NativeQueue Red Zone"])
    for hit in token_hits["native_queue"]:
        md_lines.append(f"- {hit['file']}:{hit['line']} {hit['text']}")
    md_lines.extend(["", "## Project Writer Callsite Count"])
    for key, hits in callsite_hits.items():
        md_lines.append(f"- {key}: {len(hits)}")
    md_lines.append(f"- signalbus_nativequeue_writer_hits: {len(signalbus_nativequeue_writer_hits)}")
    md_lines.extend(["", "## Phase Route"])
    for key, hits in phase_hits.items():
        md_lines.append(f"- {key}: {len(hits)}")
        for hit in hits:
            md_lines.append(f"  - {hit['file']}:{hit['line']} {hit['text']}")
    md_lines.extend(["", "## Fail-Closed Proof"])
    for key, hits in fail_closed_hits.items():
        md_lines.append(f"- {key}: {len(hits)}")
        for hit in hits:
            md_lines.append(f"  - {hit['file']}:{hit['line']} {hit['text']}")
    md_lines.extend(["", "## New Expression Classification"])
    for item in new_expression_classifications:
        md_lines.append(
            f"- {item['file']}:{item['line']} managedHeap={item['managedHeap']} hotPath={item['hotPath']} "
            f"class={item['classification']} text={item['text']}"
        )
    md_lines.append("")
    md_lines.append("## Fuzzer")
    md_lines.append(f"- {rel(FUZZER)} exists={FUZZER.exists()}")
    md_lines.append("")
    REPORT_MD.write_text("\n".join(md_lines), encoding="utf-8")

    print(json.dumps({"status": status, "json": rel(REPORT_JSON), "markdown": rel(REPORT_MD)}, sort_keys=True))
    return 0 if status.startswith("GREEN") else 2


if __name__ == "__main__":
    raise SystemExit(main())
