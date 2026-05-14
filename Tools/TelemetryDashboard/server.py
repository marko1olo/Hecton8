from __future__ import annotations

import csv
import json
import math
import re
import struct
from collections import deque
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from fastapi import FastAPI
from fastapi.responses import FileResponse, JSONResponse


PROJECT_ROOT = Path(__file__).resolve().parents[2]
DASHBOARD_ROOT = Path(__file__).resolve().parent
AGENT_LOGS = PROJECT_ROOT / "Docs" / "AgentLogs"
HPHI_REPORT = PROJECT_ROOT / "Docs" / "Reports" / "HECTON_PHI_REPORT.md"
INDEX_HTML = DASHBOARD_ROOT / "index.html"

MAX_CSV_ROWS = 600
MAX_DUMP_ENTRIES = 600
MAX_DUMP_BYTES = 10 * 1024 * 1024
FRAME_SPIKE_MS = 16.6

HECTON8_MAGIC = 0x00384E4F54434548
BIOMASS_MAGIC = 0x0038424D53434548
MACRO_SWARM_MAGIC = 0x004D57534F434548
FAUNA_MUTATION_MAGIC = 0x004D55474F434548
HEADLESS_MAGIC = 0x48385142
LIVE_TELEMETRY_MAGIC = 0x4D4C4554

GENERIC_BLACKBOX_HEADER = struct.Struct("<QII")
GENERIC_BLACKBOX_ENTRY = struct.Struct("<IIfffffffIIIIIII")
DEFRAG_ENTRY_PACK1 = struct.Struct("<IIiqqqqqfiBBBB")
DEFRAG_ENTRY_ALIGNED = struct.Struct("<IIi4xqqqqqfiBBBB4x")
THERMAL_HEADER = struct.Struct("<II")
THERMAL_ENTRY_MANUAL = struct.Struct("<IIIhBBBBB")
BIOMASS_HEADER = struct.Struct("<Qiiii")
BIOMASS_ENTRY = struct.Struct("<IIiiffff")
MACRO_SWARM_ENTRY = struct.Struct("<IIiifiII")
FAUNA_MUTATION_ENTRY = struct.Struct("<IIiiiIfffII4x")
HEADLESS_HEADER = struct.Struct("<Iiii")
HEADLESS_ENTRY = struct.Struct("<IiIqqqffffffI")
LIVE_TELEMETRY_ENTRY = struct.Struct("<IIIIIfff")


app = FastAPI(title="HECTON-8 Telemetry Dashboard", version="1.0.0")


def utc_now_iso() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds")


def file_stamp(path: Path) -> dict[str, Any]:
    if not path.exists():
        return {"exists": False, "path": str(path)}
    stat = path.stat()
    return {
        "exists": True,
        "path": str(path),
        "bytes": stat.st_size,
        "modifiedUtc": datetime.fromtimestamp(stat.st_mtime, timezone.utc).isoformat(timespec="seconds"),
    }


def normalize_name(value: str) -> str:
    return re.sub(r"[^a-z0-9]+", "", value.lower())


def parse_float(value: Any) -> float | None:
    if value is None:
        return None
    text = str(value).strip()
    if not text:
        return None
    try:
        parsed = float(text)
    except ValueError:
        return None
    return parsed if math.isfinite(parsed) else None


def parse_int(value: Any) -> int | None:
    parsed = parse_float(value)
    return None if parsed is None else int(parsed)


def pick_column(row: dict[str, str], aliases: tuple[str, ...]) -> tuple[str | None, str | None]:
    if not row:
        return None, None
    normalized = {normalize_name(key): key for key in row.keys()}
    for alias in aliases:
        key = normalized.get(normalize_name(alias))
        if key is not None:
            return key, row.get(key)
    return None, None


def convert_frame_time_ms(value: float | None, key: str | None) -> float | None:
    if value is None:
        return None
    normalized = normalize_name(key or "")
    if "fps" in normalized:
        return None if value <= 0.0 else 1000.0 / value
    if "delta" in normalized and "ms" not in normalized and value < 10.0:
        return value * 1000.0
    if normalized in {"dt", "deltatime", "frameseconds"} and value < 10.0:
        return value * 1000.0
    return value


def cap_entries(entries: list[dict[str, Any]]) -> list[dict[str, Any]]:
    if len(entries) <= MAX_DUMP_ENTRIES:
        return entries
    return entries[-MAX_DUMP_ENTRIES:]


def parse_csv_file(path: Path, source_label: str) -> dict[str, Any]:
    result: dict[str, Any] = {
        **file_stamp(path),
        "source": source_label,
        "rows": [],
        "frameSeries": [],
        "ecologySeries": [],
        "latestThermal": None,
        "latestHphi": None,
        "warnings": [],
    }
    if not path.exists():
        result["warnings"].append("missing")
        return result

    rows: deque[dict[str, str]] = deque(maxlen=MAX_CSV_ROWS)
    try:
        try:
            with path.open("r", encoding="utf-8-sig", newline="") as handle:
                rows.extend(csv.DictReader(handle))
        except UnicodeDecodeError:
            with path.open("r", encoding="cp1251", newline="") as handle:
                rows.extend(csv.DictReader(handle))
    except OSError as exc:
        result["warnings"].append(f"read_failed:{exc.__class__.__name__}")
        return result

    previous_frame_ms: float | None = None
    latest_thermal: dict[str, Any] | None = None
    latest_hphi: float | None = None
    for ordinal, row in enumerate(rows):
        _, frame_raw = pick_column(row, ("frame", "frameIndex", "FrameIndex", "Day"))
        frame = parse_int(frame_raw)
        if frame is None:
            frame = ordinal

        _, time_raw = pick_column(row, ("time", "timeSeconds", "elapsedSeconds", "distanceMeters", "Day"))
        x_value = parse_float(time_raw)
        if x_value is None:
            x_value = frame

        frame_key, frame_raw = pick_column(
            row,
            ("FrameTimeMs", "frame_time_ms", "FrameMs", "frameTime", "DeltaTimeMs", "deltaTime", "dt", "avgFps", "fps"),
        )
        frame_time_ms = convert_frame_time_ms(parse_float(frame_raw), frame_key)
        _, jitter_raw = pick_column(row, ("JitterMs", "FrameJitterMs", "Jitter", "frame_jitter_ms"))
        jitter_ms = parse_float(jitter_raw)
        if jitter_ms is None and frame_time_ms is not None and previous_frame_ms is not None:
            jitter_ms = abs(frame_time_ms - previous_frame_ms)
        if frame_time_ms is not None:
            previous_frame_ms = frame_time_ms
            result["frameSeries"].append(
                {
                    "x": x_value,
                    "frame": frame,
                    "frameTimeMs": round(frame_time_ms, 4),
                    "jitterMs": round(jitter_ms or 0.0, 4),
                    "spike": frame_time_ms > FRAME_SPIKE_MS,
                    "source": source_label,
                }
            )

        _, prey_raw = pick_column(row, ("PreyBiomass", "PreyBiomassSum", "prey_biomass", "PreyBiomass01"))
        _, predator_raw = pick_column(row, ("PredatorBiomass", "PredatorBiomassSum", "predator_biomass", "PredatorBiomass01"))
        prey = parse_float(prey_raw)
        predator = parse_float(predator_raw)
        if prey is not None or predator is not None:
            result["ecologySeries"].append(
                {"x": x_value, "frame": frame, "prey": round(prey or 0.0, 6), "predator": round(predator or 0.0, 6)}
            )

        _, thermal_raw = pick_column(row, ("HardwareThermalSeverity", "ThermalSeverity", "thermalSeverity", "severity"))
        _, battery_raw = pick_column(row, ("BatteryPercent", "batteryPercent", "Battery", "battery"))
        severity = parse_int(thermal_raw)
        battery = parse_int(battery_raw)
        if severity is not None or battery is not None:
            latest_thermal = {"severity": severity, "batteryPercent": battery, "source": source_label}

        _, hphi_raw = pick_column(row, ("H-Phi", "HPhi", "HectonPhi", "hphi", "staticHPhi"))
        hphi = parse_float(hphi_raw)
        if hphi is not None:
            latest_hphi = hphi

    result["loadedRowCount"] = len(rows)
    result["rows"] = list(rows)[-20:]
    result["latestThermal"] = latest_thermal
    result["latestHphi"] = latest_hphi
    return result


def parse_hphi_report(path: Path = HPHI_REPORT) -> dict[str, Any]:
    result = {"value": None, "status": "missing", "source": str(path), "evidenceClass": "STATIC_DOC"}
    if not path.exists():
        return result
    try:
        text = path.read_text(encoding="utf-8", errors="replace")
    except OSError as exc:
        result["status"] = f"read_failed:{exc.__class__.__name__}"
        return result
    for line in text.splitlines():
        if "h-phi" not in line.lower() and "hphi" not in line.lower():
            continue
        candidate = line.rsplit("=", 1)[-1] if "=" in line else line
        match = re.search(r"([0-9]*\.[0-9]+|[0-9]+)", candidate)
        if match is None:
            continue
        result["value"] = float(match.group(1))
        result["status"] = "static-report"
        break
    else:
        result["status"] = "not_found"
    return result


def parse_generic_blackbox(path: Path, data: bytes) -> dict[str, Any]:
    if len(data) < GENERIC_BLACKBOX_HEADER.size:
        return {"type": "generic_blackbox", "entries": [], "warnings": ["truncated_header"]}
    magic, entry_count, struct_size = GENERIC_BLACKBOX_HEADER.unpack_from(data, 0)
    if magic != HECTON8_MAGIC or struct_size != GENERIC_BLACKBOX_ENTRY.size:
        return {"type": "generic_blackbox", "entries": [], "warnings": ["invalid_header"]}
    readable = min(entry_count, (len(data) - GENERIC_BLACKBOX_HEADER.size) // struct_size)
    entries = []
    offset = GENERIC_BLACKBOX_HEADER.size
    for _ in range(readable):
        fields = GENERIC_BLACKBOX_ENTRY.unpack_from(data, offset)
        offset += struct_size
        entries.append(
            {
                "frame": fields[0],
                "systemMask": fields[1],
                "deltaTimeMs": round(fields[2] * 1000.0, 4),
                "latencyMs": round(fields[3], 4),
                "gpuFrameTimeMs": round(fields[4], 4),
                "memoryUsedMb": round(fields[5], 4),
                "player": {"x": fields[6], "y": fields[7], "z": fields[8]},
                "activeChunkCount": fields[9],
                "errorFlags": fields[10],
                "exportReason": fields[11],
                "aupShiftSequence": fields[12],
                "payload0": fields[13],
                "payload1": fields[14],
                "lastOriginShiftFrame": fields[15],
            }
        )
    latest = entries[-1] if entries else None
    capped = cap_entries(entries)
    return {
        "type": "generic_blackbox",
        "entrySize": struct_size,
        "declaredEntryCount": entry_count,
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": ["payload_truncated"] if readable < entry_count else [],
    }


def parse_defrag_dump(path: Path, data: bytes) -> dict[str, Any]:
    entry_struct = DEFRAG_ENTRY_ALIGNED if len(data) % DEFRAG_ENTRY_ALIGNED.size == 0 else DEFRAG_ENTRY_PACK1
    if len(data) < entry_struct.size:
        return {"type": "memory_defrag", "entries": [], "memoryMap": [], "warnings": ["truncated_payload"]}
    entries = []
    offset = 0
    for _ in range(min(300, len(data) // entry_struct.size)):
        fields = entry_struct.unpack_from(data, offset)
        offset += entry_struct.size
        if not any(fields):
            continue
        entries.append(
            {
                "sequence": fields[0],
                "vaultGenerationId": fields[1],
                "blockCount": fields[2],
                "totalFreeSpaceBytes": fields[3],
                "largestContiguousBlockBytes": fields[4],
                "lastMovedBytes": fields[5],
                "totalMovedBytes": fields[6],
                "pendingMassiveMoveBytes": fields[7],
                "heapFragmentationRatio": round(fields[8], 6),
                "watchdogBreaches": fields[9],
                "flags": fields[10],
                "isFragmented": bool(fields[11]),
                "watchdogExceeded": bool(fields[12]),
            }
        )
    latest = entries[-1] if entries else None
    capped = cap_entries(entries)
    return {
        "type": "memory_defrag",
        "entrySize": entry_struct.size,
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "memoryMap": build_defrag_memory_map(latest),
        "warnings": [],
    }


def build_defrag_memory_map(latest: dict[str, Any] | None) -> list[dict[str, Any]]:
    if not latest:
        return []
    free = max(0, int(latest.get("totalFreeSpaceBytes") or 0))
    largest = max(0, int(latest.get("largestContiguousBlockBytes") or 0))
    moved = max(0, int(latest.get("totalMovedBytes") or 0))
    block_count = max(0, int(latest.get("blockCount") or 0))
    occupied = max(moved, largest, 1 if block_count > 0 else 0)
    blocks = []
    if occupied > 0:
        blocks.append({"state": "occupied", "bytes": occupied, "label": "occupied-estimate", "estimated": True})
    if largest > 0:
        blocks.append({"state": "free", "bytes": largest, "label": "largest-free", "estimated": True})
    remaining = max(0, free - largest)
    if remaining > 0:
        blocks.append({"state": "free-fragmented", "bytes": remaining, "label": "fragmented-free", "estimated": True})
    return blocks


def parse_thermal_dump(path: Path, data: bytes) -> dict[str, Any]:
    if len(data) < THERMAL_HEADER.size:
        return {"type": "thermal", "entries": [], "warnings": ["truncated_header"]}
    sequence, cursor = THERMAL_HEADER.unpack_from(data, 0)
    entries = []
    offset = THERMAL_HEADER.size
    for _ in range((len(data) - THERMAL_HEADER.size) // THERMAL_ENTRY_MANUAL.size):
        fields = THERMAL_ENTRY_MANUAL.unpack_from(data, offset)
        offset += THERMAL_ENTRY_MANUAL.size
        if not any(fields):
            continue
        entries.append(
            {
                "frame": fields[0],
                "sequence": fields[1],
                "actionMask": fields[2],
                "temperatureTenthsCelsius": fields[3],
                "severity": fields[4],
                "batteryPercent": fields[5],
                "batteryStatus": fields[6],
                "thermalStatus": fields[7],
                "flags": fields[8],
            }
        )
    latest = entries[-1] if entries else None
    capped = cap_entries(entries)
    return {
        "type": "thermal",
        "sequence": sequence,
        "cursor": cursor,
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": [],
    }


def parse_headered_entries(
    data: bytes,
    expected_magic: int,
    entry_struct: struct.Struct,
    parser_type: str,
    mapper: Any,
) -> dict[str, Any]:
    if len(data) < BIOMASS_HEADER.size:
        return {"type": parser_type, "entries": [], "warnings": ["truncated_header"]}
    magic, entry_count, entry_size, oldest_index, capacity = BIOMASS_HEADER.unpack_from(data, 0)
    if magic != expected_magic or entry_size != entry_struct.size:
        return {"type": parser_type, "entries": [], "warnings": ["invalid_header"]}
    readable = min(entry_count, (len(data) - BIOMASS_HEADER.size) // entry_size)
    entries = []
    offset = BIOMASS_HEADER.size
    for _ in range(readable):
        fields = entry_struct.unpack_from(data, offset)
        offset += entry_size
        entries.append(mapper(fields))
    latest = entries[-1] if entries else None
    capped = cap_entries(entries)
    return {
        "type": parser_type,
        "entrySize": entry_size,
        "entryCount": entry_count,
        "oldestIndex": oldest_index,
        "capacity": capacity,
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": ["payload_truncated"] if readable < entry_count else [],
    }


def parse_biomass_dump(path: Path, data: bytes) -> dict[str, Any]:
    return parse_headered_entries(
        data,
        BIOMASS_MAGIC,
        BIOMASS_ENTRY,
        "biomass",
        lambda fields: {
            "frame": fields[0],
            "stateHash": fields[1],
            "activeCellCount": fields[2],
            "flags": fields[3],
            "global": round(fields[4], 6),
            "prey": round(fields[5], 6),
            "predator": round(fields[6], 6),
            "floraOvergrowth01": round(fields[7], 6),
        },
    )


def parse_macro_swarm_dump(path: Path, data: bytes) -> dict[str, Any]:
    return parse_headered_entries(
        data,
        MACRO_SWARM_MAGIC,
        MACRO_SWARM_ENTRY,
        "macro_swarm",
        lambda fields: {
            "frame": fields[0],
            "stateHash": fields[1],
            "activeMacroSwarms": fields[2],
            "arrivalCount": fields[3],
            "biomass": round(fields[4], 6),
            "flags": fields[5],
            "reserved0": fields[6],
            "reserved1": fields[7],
        },
    )


def parse_fauna_mutation_dump(path: Path, data: bytes) -> dict[str, Any]:
    return parse_headered_entries(
        data,
        FAUNA_MUTATION_MAGIC,
        FAUNA_MUTATION_ENTRY,
        "fauna_mutation",
        lambda fields: {
            "frame": fields[0],
            "stateHash": fields[1],
            "totalMutatedEntities": fields[2],
            "headlessMutatedCount": fields[3],
            "macroSwarmMutatedCount": fields[4],
            "lastMutationFlags": fields[5],
            "lastRadiationRads": round(fields[6], 6),
            "lastToxicity01": round(fields[7], 6),
            "lastBrineDepth01": round(fields[8], 6),
            "reserved0": fields[9],
            "reserved1": fields[10],
        },
    )


def parse_headless_blackbox(path: Path, data: bytes) -> dict[str, Any]:
    if len(data) < HEADLESS_HEADER.size:
        return {"type": "headless_blackbox", "entries": [], "warnings": ["truncated_header"]}
    magic, entry_count, entry_size, cursor = HEADLESS_HEADER.unpack_from(data, 0)
    if magic != HEADLESS_MAGIC or entry_size != HEADLESS_ENTRY.size:
        return {"type": "headless_blackbox", "entries": [], "warnings": ["invalid_header"]}
    readable = min(entry_count, (len(data) - HEADLESS_HEADER.size) // entry_size)
    entries = []
    offset = HEADLESS_HEADER.size
    for _ in range(readable):
        fields = HEADLESS_ENTRY.unpack_from(data, offset)
        offset += entry_size
        entries.append(
            {
                "frame": fields[0],
                "day": fields[1],
                "stateHash": fields[2],
                "grid": {"x": fields[3], "y": fields[4], "z": fields[5]},
                "local": {"x": fields[6], "y": fields[7], "z": fields[8]},
                "prey": round(fields[9], 6),
                "predator": round(fields[10], 6),
                "nativeBytesMb": round(fields[11], 4),
                "flags": fields[12],
            }
        )
    latest = entries[-1] if entries else None
    capped = cap_entries(entries)
    return {
        "type": "headless_blackbox",
        "entrySize": entry_size,
        "entryCount": entry_count,
        "cursor": cursor,
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": ["payload_truncated"] if readable < entry_count else [],
    }


def parse_live_telemetry(path: Path, data: bytes) -> dict[str, Any]:
    if len(data) < LIVE_TELEMETRY_ENTRY.size:
        return {"type": "live_telemetry", "entries": [], "warnings": ["truncated_payload"]}
    fields = LIVE_TELEMETRY_ENTRY.unpack_from(data, 0)
    if fields[0] != LIVE_TELEMETRY_MAGIC:
        return {"type": "live_telemetry", "entries": [], "warnings": ["invalid_header"]}
    latest = {
        "frame": fields[2],
        "version": fields[1],
        "activeChunkCount": fields[3],
        "gcAllocBytes": fields[4],
        "cpuFrameTimeMs": round(fields[5], 4),
        "deltaTimeMs": round(fields[6] * 1000.0, 4),
        "reservedMemoryMb": round(fields[7], 4),
    }
    return {
        "type": "live_telemetry",
        "entrySize": LIVE_TELEMETRY_ENTRY.size,
        "returnedEntryCount": 1,
        "entries": [latest],
        "latest": latest,
        "warnings": [],
    }


def parse_h8memory_text(path: Path) -> dict[str, Any]:
    result = {"type": "h8memory_text", "records": [], "memoryMap": [], "warnings": []}
    try:
        lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
    except OSError as exc:
        return {"type": "h8memory_text", "records": [], "memoryMap": [], "warnings": [f"read_failed:{exc.__class__.__name__}"]}
    total_bytes = 0
    records = []
    for line in lines:
        if line.startswith("TotalBytes="):
            total_bytes = parse_int(line.split("=", 1)[1]) or 0
            continue
        match = re.search(
            r"Index=(?P<index>-?\d+)\s+Ptr=(?P<ptr>-?\d+)\s+Bytes=(?P<bytes>-?\d+)\s+Owner=(?P<owner>-?\d+)\s+Allocator=(?P<allocator>-?\d+)\s+Flags=(?P<flags>-?\d+)",
            line,
        )
        if match:
            records.append({key: int(value) for key, value in match.groupdict().items()})
    used = sum(max(0, record["bytes"]) for record in records)
    blocks = [
        {
            "state": "occupied",
            "bytes": max(0, record["bytes"]),
            "label": f"owner {record['owner']}",
            "owner": record["owner"],
            "index": record["index"],
        }
        for record in records
    ]
    if total_bytes > used:
        blocks.append({"state": "free", "bytes": total_bytes - used, "label": "untracked-free", "estimated": True})
    result["totalBytes"] = total_bytes
    result["records"] = records
    result["memoryMap"] = blocks
    return result


def parse_dump_file(path: Path) -> dict[str, Any]:
    base: dict[str, Any] = {**file_stamp(path), "name": path.name}
    if path.suffix.lower() == ".txt":
        return {**base, **parse_h8memory_text(path)}
    if path.suffix.lower() == ".json":
        try:
            parsed_json = json.loads(path.read_text(encoding="utf-8", errors="replace"))
        except (OSError, json.JSONDecodeError) as exc:
            return {**base, "type": "json_manifest", "warnings": [f"read_failed:{exc.__class__.__name__}"]}
        return {**base, "type": "json_manifest", "manifest": parsed_json, "warnings": []}
    if path.suffix.lower() not in {".bin", ".h8dump"}:
        return {**base, "type": "unsupported", "warnings": ["unsupported_extension"]}
    if path.stat().st_size > MAX_DUMP_BYTES:
        return {**base, "type": "unsupported", "warnings": ["dump_over_size_cap"]}
    try:
        data = path.read_bytes()
    except OSError as exc:
        return {**base, "type": "unsupported", "warnings": [f"read_failed:{exc.__class__.__name__}"]}

    name = path.name.upper()
    if len(data) >= GENERIC_BLACKBOX_HEADER.size:
        magic64 = struct.unpack_from("<Q", data, 0)[0]
        if magic64 == HECTON8_MAGIC:
            return {**base, **parse_generic_blackbox(path, data)}
        if magic64 == BIOMASS_MAGIC:
            return {**base, **parse_biomass_dump(path, data)}
        if magic64 == MACRO_SWARM_MAGIC:
            return {**base, **parse_macro_swarm_dump(path, data)}
        if magic64 == FAUNA_MUTATION_MAGIC:
            return {**base, **parse_fauna_mutation_dump(path, data)}
    if len(data) >= HEADLESS_HEADER.size and struct.unpack_from("<I", data, 0)[0] == HEADLESS_MAGIC:
        return {**base, **parse_headless_blackbox(path, data)}
    if len(data) >= LIVE_TELEMETRY_ENTRY.size and struct.unpack_from("<I", data, 0)[0] == LIVE_TELEMETRY_MAGIC:
        return {**base, **parse_live_telemetry(path, data)}
    if "THERMAL" in name:
        return {**base, **parse_thermal_dump(path, data)}
    if "MEMORY_DEFRAGMENTATION" in name or "VAULT_MEMORY" in name or "PHI_VOD" in name:
        return {**base, **parse_defrag_dump(path, data)}
    return {**base, "type": "unknown_binary", "warnings": ["unrecognized_binary_layout"]}


def collect_dumps() -> dict[str, Any]:
    AGENT_LOGS.mkdir(parents=True, exist_ok=True)
    candidate_paths = {path for path in AGENT_LOGS.glob("Dump_*")}
    candidate_paths.update(AGENT_LOGS.glob("*.h8dump"))
    for file_name in ("BLACKBOX_CRASH.bin", "BLACKBOX_CRASH.h8dump", "runtime_telemetry.bin"):
        candidate = AGENT_LOGS / file_name
        if candidate.exists():
            candidate_paths.add(candidate)
    dumps = [parse_dump_file(path) for path in sorted(candidate_paths) if path.is_file()]

    memory_maps = []
    thermal_latest = None
    ecology_series = []
    frame_series = []
    for dump in dumps:
        if dump.get("memoryMap"):
            blocks = dump["memoryMap"]
            memory_maps.append(
                {
                    "name": dump["name"],
                    "blocks": blocks,
                    "latest": dump.get("latest"),
                    "estimated": all(bool(block.get("estimated")) for block in blocks),
                    "sourceType": dump.get("type"),
                }
            )
        if dump.get("type") == "thermal" and dump.get("latest"):
            thermal_latest = {**dump["latest"], "source": dump["name"]}
        if dump.get("type") in {"biomass", "headless_blackbox"}:
            for entry in dump.get("entries", []):
                ecology_series.append(
                    {
                        "x": entry.get("frame") or entry.get("day") or 0,
                        "frame": entry.get("frame"),
                        "prey": entry.get("prey", 0.0),
                        "predator": entry.get("predator", 0.0),
                        "source": dump["name"],
                    }
                )
        if dump.get("type") == "generic_blackbox":
            previous_frame_ms = None
            for entry in dump.get("entries", []):
                frame_time_ms = entry.get("deltaTimeMs")
                if frame_time_ms is None:
                    continue
                jitter_ms = 0.0 if previous_frame_ms is None else abs(frame_time_ms - previous_frame_ms)
                previous_frame_ms = frame_time_ms
                frame_series.append(
                    {
                        "x": entry.get("frame", 0),
                        "frame": entry.get("frame", 0),
                        "frameTimeMs": round(frame_time_ms, 4),
                        "jitterMs": round(jitter_ms, 4),
                        "spike": frame_time_ms > FRAME_SPIKE_MS,
                        "source": dump["name"],
                    }
                )
        if dump.get("type") == "live_telemetry" and dump.get("latest"):
            entry = dump["latest"]
            frame_time_ms = entry.get("cpuFrameTimeMs") or entry.get("deltaTimeMs")
            if frame_time_ms is not None:
                frame_series.append(
                    {
                        "x": entry.get("frame", 0),
                        "frame": entry.get("frame", 0),
                        "frameTimeMs": round(frame_time_ms, 4),
                        "jitterMs": 0.0,
                        "spike": frame_time_ms > FRAME_SPIKE_MS,
                        "source": dump["name"],
                    }
                )
    memory_maps.sort(key=lambda item: (item["estimated"], item["name"]))
    return {
        "files": dumps,
        "memoryMaps": memory_maps,
        "latestThermal": thermal_latest,
        "ecologySeries": ecology_series[-MAX_CSV_ROWS:],
        "frameSeries": frame_series[-MAX_CSV_ROWS:],
    }


def collect_csv() -> dict[str, Any]:
    sources = [
        (AGENT_LOGS / "QA_Endurance_Log.csv", "QA_Endurance_Log.csv"),
        (AGENT_LOGS / "HeadlessSimulationDaily_HEADLESS_SIMULATION_RUNNER.csv", "HeadlessSimulationDaily_HEADLESS_SIMULATION_RUNNER.csv"),
    ]
    parsed = [parse_csv_file(path, label) for path, label in sources]
    frame_series = []
    ecology_series = []
    latest_thermal = None
    latest_hphi = None
    for source in parsed:
        frame_series.extend(source["frameSeries"])
        ecology_series.extend(source["ecologySeries"])
        if source["latestThermal"] is not None:
            latest_thermal = source["latestThermal"]
        if source["latestHphi"] is not None:
            latest_hphi = source["latestHphi"]
    return {
        "sources": parsed,
        "frameSeries": frame_series[-MAX_CSV_ROWS:],
        "ecologySeries": ecology_series[-MAX_CSV_ROWS:],
        "latestThermal": latest_thermal,
        "latestHphi": latest_hphi,
    }


def build_summary() -> dict[str, Any]:
    csv_data = collect_csv()
    dump_data = collect_dumps()
    hphi = parse_hphi_report()
    if csv_data["latestHphi"] is not None:
        hphi["value"] = csv_data["latestHphi"]
        hphi["status"] = "csv-latest"
        hphi["evidenceClass"] = "FILE_IO"

    latest_thermal = csv_data["latestThermal"] or dump_data["latestThermal"]
    ecology_series = csv_data["ecologySeries"] or dump_data["ecologySeries"]
    frame_series = csv_data["frameSeries"] or dump_data["frameSeries"]

    return {
        "status": "DASHBOARD OPERATIONAL",
        "generatedUtc": utc_now_iso(),
        "projectRoot": str(PROJECT_ROOT),
        "agentLogs": str(AGENT_LOGS),
        "frameSpikeMs": FRAME_SPIKE_MS,
        "csv": csv_data,
        "dumps": dump_data,
        "hphi": hphi,
        "thermal": latest_thermal,
        "frameSeries": frame_series[-MAX_CSV_ROWS:],
        "ecologySeries": ecology_series[-MAX_CSV_ROWS:],
        "evidence": {
            "runtimeUnityVerified": False,
            "class": "FILE_IO + STATIC_SOURCE",
            "note": "Dashboard parses files on disk. It does not prove Unity runtime health.",
        },
    }


@app.get("/")
def index() -> FileResponse:
    return FileResponse(INDEX_HTML)


@app.get("/api/summary")
def api_summary() -> JSONResponse:
    return JSONResponse(build_summary())


@app.get("/api/health")
def api_health() -> dict[str, Any]:
    return {
        "status": "ok",
        "generatedUtc": utc_now_iso(),
        "projectRoot": str(PROJECT_ROOT),
        "agentLogsExists": AGENT_LOGS.exists(),
    }
