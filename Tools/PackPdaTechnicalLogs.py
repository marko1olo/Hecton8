#!/usr/bin/env python3
"""Pack PDA technical logs into 16-byte aligned H8PT binaries."""

from __future__ import annotations

import argparse
import json
import struct
import sys
import zlib
from pathlib import Path
from typing import Any

sys.path.insert(0, str(Path(__file__).resolve().parent))
from LocToBinary import FNV_OFFSET_BASIS, FNV_PRIME, compute_loc_hash, format_hash
from LoreTechValidator import (
    DEFAULT_LOCALIZATION,
    DEFAULT_SOURCE,
    build_physics_audit,
    build_tone_audit,
    load_jsonl,
    load_localization_entries,
    validate,
)
from PdaTechSchema import (
    EXTRA_DERIVATION_FORMULA,
    EXTRA_DERIVATION_MODEL,
    EXTRA_SCHEMA_VERSION,
    HIGH_TIER_GRADIENT_RESOLUTION,
    PROJECT_ATLAS_DOMAIN_IDS,
    PROJECT_ATLAS_DOMAIN_NAMES,
    hydrostatic_pa_per_meter_x100,
)


DEFAULT_OUTPUT = Path("Data/Lore/PdaTechnicalLogs.h8bin")
DEFAULT_TOASTER_OUTPUT = Path("Data/Lore/PdaTechnicalLogs_Toaster.h8bin")
DEFAULT_MANIFEST = Path("Data/Lore/PdaTechnicalLogs.manifest.json")
MAGIC = b"H8PT"
VERSION = 1
ALIGNMENT = 16
HEADER_STRUCT = struct.Struct("<4sHHIIIIIIIIIIIIII")
RECORD_STRUCT = struct.Struct("<IIIIIIIIIIIIIIII")
EXTRA_VISUAL_RECORD_STRUCT = struct.Struct("<IIIIIIIIIIIIIIII")
HEADER_SIZE = HEADER_STRUCT.size
RECORD_SIZE = RECORD_STRUCT.size
EXTRA_VISUAL_RECORD_SIZE = EXTRA_VISUAL_RECORD_STRUCT.size
FLAG_UTF8_PAYLOADS = 1
FLAG_UTF16_FNV1A_KEYS = 2
FLAG_16_BYTE_ALIGNED = 4
FLAG_HAS_COMPACT_TEXT = 8
FLAG_HAS_EXTRA_VISUAL_DATA = 16
FLAG_TOASTER_COMPACT_ONLY = 32
BASIC_FLAGS = FLAG_UTF8_PAYLOADS | FLAG_UTF16_FNV1A_KEYS | FLAG_16_BYTE_ALIGNED
FLAGS = BASIC_FLAGS | FLAG_HAS_COMPACT_TEXT | FLAG_HAS_EXTRA_VISUAL_DATA
TOASTER_FLAGS = BASIC_FLAGS | FLAG_TOASTER_COMPACT_ONLY
HEADER_FIELD_NAMES = (
    "Magic",
    "Version",
    "HeaderSize",
    "EntryCount",
    "RecordSize",
    "TableOffset",
    "TextOffset",
    "CompactOffset",
    "ExtraOffset",
    "TextLength",
    "CompactLength",
    "ExtraLength",
    "Flags",
    "SourceHash",
    "Reserved0",
    "Reserved1",
    "Reserved2",
)


def align_up(value: int, alignment: int = ALIGNMENT) -> int:
    return (value + (alignment - 1)) & ~(alignment - 1)


def fnv1a_bytes(data: bytes) -> int:
    h = FNV_OFFSET_BASIS
    for byte in data:
        h ^= byte
        h = (h * FNV_PRIME) & 0xFFFFFFFF
    return h


def crc32_bytes(data: bytes) -> int:
    return zlib.crc32(data) & 0xFFFFFFFF


def read_header(blob: bytes) -> dict[str, int | bytes]:
    if len(blob) < HEADER_SIZE:
        raise ValueError("PDA tech blob is smaller than its header")
    return {
        name: value
        for name, value in zip(HEADER_FIELD_NAMES, HEADER_STRUCT.unpack_from(blob, 0))
    }


def build_header_manifest(header: dict[str, int | bytes]) -> dict[str, int | str]:
    source_hash = int(header["SourceHash"])
    return {
        "SourceHash": format_hash(source_hash),
        "Flags": int(header["Flags"]),
        "EntryCount": int(header["EntryCount"]),
        "RecordSize": int(header["RecordSize"]),
        "TableOffset": int(header["TableOffset"]),
        "TextOffset": int(header["TextOffset"]),
        "CompactOffset": int(header["CompactOffset"]),
        "ExtraOffset": int(header["ExtraOffset"]),
    }


def _section_descriptor(offset: int, length: int) -> dict[str, int]:
    return {
        "Offset": offset,
        "Length": length,
        "EndOffset": offset + length,
        "Alignment": ALIGNMENT,
    }


def build_section_manifest(header: dict[str, int | bytes]) -> dict[str, dict[str, int]]:
    entry_count = int(header["EntryCount"])
    record_size = int(header["RecordSize"])
    sections = {
        "RecordTable": _section_descriptor(int(header["TableOffset"]), entry_count * record_size),
        "Text": _section_descriptor(int(header["TextOffset"]), int(header["TextLength"])),
        "CompactText": _section_descriptor(int(header["CompactOffset"]), int(header["CompactLength"])),
        "ExtraVisualRecord": _section_descriptor(int(header["ExtraOffset"]), int(header["ExtraLength"])),
    }
    sections["RecordTable"]["RecordSize"] = record_size
    sections["ExtraVisualRecord"]["RecordSize"] = EXTRA_VISUAL_RECORD_SIZE
    return sections


def build_integrity_manifest(blob: bytes, header: dict[str, int | bytes]) -> dict[str, str | int]:
    table_offset = int(header["TableOffset"])
    table_length = int(header["EntryCount"]) * int(header["RecordSize"])
    text_offset = int(header["TextOffset"])
    compact_offset = int(header["CompactOffset"])
    extra_offset = int(header["ExtraOffset"])
    text_length = int(header["TextLength"])
    compact_length = int(header["CompactLength"])
    extra_length = int(header["ExtraLength"])
    payload_end = extra_offset + extra_length
    checks = {
        "Algorithm": "CRC32",
        "BinaryCrc32": crc32_bytes(blob),
        "HeaderCrc32": crc32_bytes(blob[:HEADER_SIZE]),
        "RecordTableCrc32": crc32_bytes(blob[table_offset : table_offset + table_length]),
        "TextCrc32": crc32_bytes(blob[text_offset : text_offset + text_length]),
        "CompactTextCrc32": crc32_bytes(blob[compact_offset : compact_offset + compact_length]),
        "ExtraVisualRecordCrc32": crc32_bytes(blob[extra_offset : extra_offset + extra_length]),
        "PayloadCrc32": crc32_bytes(blob[text_offset:payload_end]),
    }
    crc_items = [
        (key, value)
        for key, value in checks.items()
        if key.endswith("Crc32") and isinstance(value, int)
    ]
    checks.update({f"{key}Hex": format_hash(value) for key, value in crc_items})
    return checks


def build_lookup_contract(header: dict[str, int | bytes]) -> dict[str, int | str | list[str]]:
    entry_count = int(header["EntryCount"])
    is_toaster = bool(int(header["Flags"]) & FLAG_TOASTER_COMPACT_ONLY)
    return {
        "EvidenceClass": "CLI_TOOLING",
        "Algorithm": "uint32_binary_search_sorted_record_table",
        "RecordKey": "LocHash",
        "HashAlgorithm": "FNV1a32_UTF16LE_LocID",
        "TableOrder": "strict_ascending_uint32",
        "TableOffset": int(header["TableOffset"]),
        "EntryCount": entry_count,
        "RecordStrideBytes": int(header["RecordSize"]),
        "SearchIterationsMax": max(1, entry_count.bit_length()),
        "MissingKeyFallback": "not_found_no_exception",
        "RuntimeAllocationPolicy": "caller_owned_buffer_no_private_cache",
        "PayloadSemantic": "CompactText" if is_toaster else "Text",
        "PayloadSliceFields": (
            ["TextOffset", "TextLength"]
            if is_toaster
            else [
                "TextOffset",
                "TextLength",
                "CompactTextOffset",
                "CompactTextLength",
                "ExtraVisualOffset",
                "ExtraVisualLength",
            ]
        ),
    }


def parse_hash(value: Any) -> int:
    if not isinstance(value, str) or not value.startswith("0x"):
        raise ValueError(f"Expected canonical hash string, got {value!r}")
    return int(value, 16) & 0xFFFFFFFF


def append_payload(section: bytearray, payload: bytes) -> tuple[int, int]:
    offset = align_up(len(section))
    if len(section) < offset:
        section.extend(b"\x00" * (offset - len(section)))
    length = len(payload)
    section.extend(payload)
    aligned_length = align_up(len(section))
    if len(section) < aligned_length:
        section.extend(b"\x00" * (aligned_length - len(section)))
    return offset, length


def pack_extra_visual_record(extra: dict[str, Any]) -> bytes:
    overlay_flags = extra.get("OverlayFlags")
    if not isinstance(overlay_flags, list):
        raise ValueError("OverlayFlags must be a list")
    overlay_hash = compute_loc_hash("|".join(str(flag) for flag in overlay_flags))
    return EXTRA_VISUAL_RECORD_STRUCT.pack(
        int(extra["SchemaVersion"]) & 0xFFFFFFFF,
        compute_loc_hash(str(extra["DerivationModel"])),
        compute_loc_hash(str(extra["VisualDataAuthority"])),
        compute_loc_hash(str(extra["VisualProfile"])),
        compute_loc_hash(str(extra["GradientLut"])),
        int(extra["GradientResolution"]) & 0xFFFFFFFF,
        int(extra["GradientIndex"]) & 0xFFFFFFFF,
        int(extra["NoiseSeed"]) & 0xFFFFFFFF,
        int(extra["HarmonicOctaves"]) & 0xFFFFFFFF,
        int(extra["HarmonicHzX100"]) & 0xFFFFFFFF,
        overlay_hash,
        int(extra["OverlayIndex"]) & 0xFFFFFFFF,
        int(extra["StressState"]) & 0xFFFFFFFF,
        int(extra["HydrostaticPaPerMeterX100"]) & 0xFFFFFFFF,
        int(extra["SurfacePressurePa"]) & 0xFFFFFFFF,
        int(extra["ProjectPressureMPaPer100mX1000"]) & 0xFFFFFFFF,
    )


def validate_extra_visual_record_bytes(payload: bytes) -> None:
    if len(payload) != EXTRA_VISUAL_RECORD_SIZE:
        raise ValueError("PDA tech extra visual record size mismatch")
    fields = EXTRA_VISUAL_RECORD_STRUCT.unpack(payload)
    if fields[0] != EXTRA_SCHEMA_VERSION:
        raise ValueError("PDA tech extra visual schema version mismatch")
    if fields[5] != HIGH_TIER_GRADIENT_RESOLUTION:
        raise ValueError("PDA tech extra visual gradient resolution mismatch")
    if fields[13] != hydrostatic_pa_per_meter_x100():
        raise ValueError("PDA tech extra visual hydrostatic constant mismatch")


def _pack_records(
    records: list[dict[str, int | str]],
    table: bytearray,
) -> None:
    for record in records:
        table.extend(
            RECORD_STRUCT.pack(
                int(record["Hash"]),
                int(record["CategoryHash"]),
                int(record["TextOffset"]),
                int(record["TextLength"]),
                int(record["CompactOffset"]),
                int(record["CompactLength"]),
                int(record["ExtraOffset"]),
                int(record["ExtraLength"]),
                int(record["LinkHash"]),
                int(record["NoiseSeed"]),
                int(record["Flags"]),
                int(record["StressState"]),
                int(record["GradientHash"]),
                int(record["HarmonicOctaves"]),
                int(record["HarmonicHzX100"]),
                int(record["OverlayHash"]),
            )
        )


def build_blob(entries: list[dict[str, Any]], source_bytes: bytes) -> tuple[bytes, list[dict[str, int | str]]]:
    text_section = bytearray()
    compact_section = bytearray()
    extra_section = bytearray()
    records: list[dict[str, int | str]] = []

    for entry in entries:
        loc_id = str(entry["LocID"])
        category = str(entry["Category"])
        extra = entry["ExtraData"]
        text_offset, text_length = append_payload(text_section, str(entry["Text"]).encode("utf-8"))
        compact_offset, compact_length = append_payload(
            compact_section,
            str(entry["CompactText"]).encode("utf-8"),
        )
        extra_payload = pack_extra_visual_record(extra)
        extra_offset, extra_length = append_payload(extra_section, extra_payload)
        overlay_hash = compute_loc_hash("|".join(str(flag) for flag in extra["OverlayFlags"]))
        records.append(
            {
                "LocID": loc_id,
                "Hash": parse_hash(entry["Hash"]),
                "CategoryHash": compute_loc_hash(category),
                "TextOffset": text_offset,
                "TextLength": text_length,
                "CompactOffset": compact_offset,
                "CompactLength": compact_length,
                "ExtraOffset": extra_offset,
                "ExtraLength": extra_length,
                "LinkHash": parse_hash(entry["LinkHash"]),
                "NoiseSeed": int(extra["NoiseSeed"]) & 0xFFFFFFFF,
                "Flags": 1 if category == "habitat_integrity_corruption" else 0,
                "StressState": int(extra["StressState"]) & 0xFFFFFFFF,
                "GradientHash": compute_loc_hash(str(extra["GradientLut"])),
                "HarmonicOctaves": int(extra["HarmonicOctaves"]) & 0xFFFFFFFF,
                "HarmonicHzX100": int(extra["HarmonicHzX100"]) & 0xFFFFFFFF,
                "OverlayHash": overlay_hash,
            }
        )

    records.sort(key=lambda record: int(record["Hash"]))
    table_offset = HEADER_SIZE
    text_offset = align_up(table_offset + (len(records) * RECORD_SIZE))
    compact_offset = text_offset + len(text_section)
    extra_offset = compact_offset + len(compact_section)
    header = HEADER_STRUCT.pack(
        MAGIC,
        VERSION,
        HEADER_SIZE,
        len(records),
        RECORD_SIZE,
        table_offset,
        text_offset,
        compact_offset,
        extra_offset,
        len(text_section),
        len(compact_section),
        len(extra_section),
        FLAGS,
        fnv1a_bytes(source_bytes),
        0,
        0,
        0,
    )
    blob = bytearray(header)
    _pack_records(records, blob)
    if len(blob) < text_offset:
        blob.extend(b"\x00" * (text_offset - len(blob)))
    blob.extend(text_section)
    blob.extend(compact_section)
    blob.extend(extra_section)
    aligned_length = align_up(len(blob))
    if len(blob) < aligned_length:
        blob.extend(b"\x00" * (aligned_length - len(blob)))
    verify_blob(bytes(blob))
    return bytes(blob), records


def build_toaster_blob(
    entries: list[dict[str, Any]],
    source_bytes: bytes,
) -> tuple[bytes, list[dict[str, int | str]]]:
    text_section = bytearray()
    records: list[dict[str, int | str]] = []
    for entry in entries:
        loc_id = str(entry["LocID"])
        category = str(entry["Category"])
        extra = entry["ExtraData"]
        text_offset, text_length = append_payload(text_section, str(entry["CompactText"]).encode("utf-8"))
        records.append(
            {
                "LocID": loc_id,
                "Hash": parse_hash(entry["Hash"]),
                "CategoryHash": compute_loc_hash(category),
                "TextOffset": text_offset,
                "TextLength": text_length,
                "CompactOffset": 0,
                "CompactLength": 0,
                "ExtraOffset": 0,
                "ExtraLength": 0,
                "LinkHash": parse_hash(entry["LinkHash"]),
                "NoiseSeed": 0,
                "Flags": 1 if category == "habitat_integrity_corruption" else 0,
                "StressState": int(extra["StressState"]) & 0xFFFFFFFF,
                "GradientHash": 0,
                "HarmonicOctaves": 0,
                "HarmonicHzX100": 0,
                "OverlayHash": 0,
            }
        )
    records.sort(key=lambda record: int(record["Hash"]))
    table_offset = HEADER_SIZE
    text_offset = align_up(table_offset + (len(records) * RECORD_SIZE))
    compact_offset = text_offset + len(text_section)
    header = HEADER_STRUCT.pack(
        MAGIC,
        VERSION,
        HEADER_SIZE,
        len(records),
        RECORD_SIZE,
        table_offset,
        text_offset,
        compact_offset,
        compact_offset,
        len(text_section),
        0,
        0,
        TOASTER_FLAGS,
        fnv1a_bytes(source_bytes),
        0,
        0,
        0,
    )
    blob = bytearray(header)
    _pack_records(records, blob)
    if len(blob) < text_offset:
        blob.extend(b"\x00" * (text_offset - len(blob)))
    blob.extend(text_section)
    aligned_length = align_up(len(blob))
    if len(blob) < aligned_length:
        blob.extend(b"\x00" * (aligned_length - len(blob)))
    verify_blob(bytes(blob))
    return bytes(blob), records


def verify_blob(blob: bytes) -> None:
    if len(blob) < HEADER_SIZE:
        raise ValueError("PDA tech blob is smaller than its header")
    if len(blob) % ALIGNMENT != 0:
        raise ValueError("PDA tech blob length is not 16-byte aligned")
    header = read_header(blob)
    if header["Magic"] != MAGIC:
        raise ValueError("PDA tech blob magic mismatch")
    if int(header["Version"]) != VERSION:
        raise ValueError("PDA tech blob version mismatch")
    if int(header["HeaderSize"]) != HEADER_SIZE or int(header["RecordSize"]) != RECORD_SIZE:
        raise ValueError("PDA tech blob struct size mismatch")
    if int(header["TableOffset"]) != HEADER_SIZE:
        raise ValueError("PDA tech blob table offset mismatch")
    if int(header["Reserved0"]) or int(header["Reserved1"]) or int(header["Reserved2"]):
        raise ValueError("PDA tech reserved fields must be zero")
    flags = int(header["Flags"])
    if flags not in (FLAGS, TOASTER_FLAGS):
        raise ValueError("PDA tech blob flags mismatch")
    is_toaster = bool(flags & FLAG_TOASTER_COMPACT_ONLY)
    has_compact = bool(flags & FLAG_HAS_COMPACT_TEXT)
    has_extra = bool(flags & FLAG_HAS_EXTRA_VISUAL_DATA)
    if is_toaster and (has_compact or has_extra):
        raise ValueError("PDA tech toaster flags cannot include compact/extra")
    if not is_toaster and (not has_compact or not has_extra):
        raise ValueError("PDA tech full blob must include compact/extra")
    expected_text_offset = align_up(int(header["TableOffset"]) + int(header["EntryCount"]) * RECORD_SIZE)
    if int(header["TextOffset"]) != expected_text_offset:
        raise ValueError("PDA tech text offset mismatch")
    if int(header["CompactOffset"]) != int(header["TextOffset"]) + int(header["TextLength"]):
        raise ValueError("PDA tech compact offset mismatch")
    if int(header["ExtraOffset"]) != int(header["CompactOffset"]) + int(header["CompactLength"]):
        raise ValueError("PDA tech extra offset mismatch")
    payload_end = int(header["ExtraOffset"]) + int(header["ExtraLength"])
    if payload_end > len(blob):
        raise ValueError("PDA tech payload overruns blob")
    if any(value != 0 for value in blob[payload_end:]):
        raise ValueError("PDA tech trailing padding must be zero")
    for key in ("TextOffset", "CompactOffset", "ExtraOffset"):
        if int(header[key]) % ALIGNMENT:
            raise ValueError(f"PDA tech {key} is not aligned")

    last_hash = -1
    seen_hashes: set[int] = set()
    for index in range(int(header["EntryCount"])):
        record_offset = int(header["TableOffset"]) + index * RECORD_SIZE
        record = RECORD_STRUCT.unpack_from(blob, record_offset)
        loc_hash = record[0]
        if loc_hash <= last_hash:
            raise ValueError("PDA tech record table is not strictly sorted")
        if loc_hash in seen_hashes:
            raise ValueError(f"PDA tech duplicate hash: {format_hash(loc_hash)}")
        seen_hashes.add(loc_hash)
        last_hash = loc_hash
        text_slice_offset, text_slice_length = record[2], record[3]
        compact_slice_offset, compact_slice_length = record[4], record[5]
        extra_slice_offset, extra_slice_length = record[6], record[7]
        if text_slice_offset % ALIGNMENT or compact_slice_offset % ALIGNMENT or extra_slice_offset % ALIGNMENT:
            raise ValueError("PDA tech per-record payload offset is not aligned")
        if text_slice_offset > int(header["TextLength"]) or text_slice_length > int(header["TextLength"]) - text_slice_offset:
            raise ValueError(f"PDA tech bad text slice at record {index}")
        if has_compact:
            if compact_slice_offset > int(header["CompactLength"]) or compact_slice_length > int(header["CompactLength"]) - compact_slice_offset:
                raise ValueError(f"PDA tech bad compact slice at record {index}")
        elif compact_slice_offset or compact_slice_length:
            raise ValueError(f"PDA tech compact slice must be stripped at record {index}")
        if has_extra:
            if extra_slice_offset > int(header["ExtraLength"]) or extra_slice_length > int(header["ExtraLength"]) - extra_slice_offset:
                raise ValueError(f"PDA tech bad extra slice at record {index}")
            payload = blob[
                int(header["ExtraOffset"]) + extra_slice_offset:
                int(header["ExtraOffset"]) + extra_slice_offset + extra_slice_length
            ]
            validate_extra_visual_record_bytes(payload)
        elif extra_slice_offset or extra_slice_length:
            raise ValueError(f"PDA tech extra slice must be stripped at record {index}")


def write_manifest(
    path: Path,
    source_path: Path,
    output_path: Path,
    blob: bytes,
    records: list[dict[str, int | str]],
    source_entries: list[dict[str, Any]],
    toaster_output_path: Path,
    toaster_blob: bytes,
    toaster_records: list[dict[str, int | str]],
) -> None:
    header = read_header(blob)
    toaster_header = read_header(toaster_blob)
    source_hash = int(header["SourceHash"])
    lookup_contract = build_lookup_contract(header)
    toaster_lookup_contract = build_lookup_contract(toaster_header)
    payload_end = int(header["ExtraOffset"]) + int(header["ExtraLength"])
    toaster_payload_end = int(toaster_header["ExtraOffset"]) + int(toaster_header["ExtraLength"])
    tone_audit = build_tone_audit(source_entries)
    size_reduction_bytes = len(blob) - len(toaster_blob)
    manifest = {
        "Magic": MAGIC.decode("ascii"),
        "Version": VERSION,
        "Endian": "Little",
        "Structs": {
            "Header": HEADER_STRUCT.format,
            "HeaderSize": HEADER_SIZE,
            "Record": RECORD_STRUCT.format,
            "RecordSize": RECORD_SIZE,
            "ExtraVisualRecord": EXTRA_VISUAL_RECORD_STRUCT.format,
            "ExtraVisualRecordSize": EXTRA_VISUAL_RECORD_SIZE,
        },
        "Alignment": ALIGNMENT,
        "Entries": len(records),
        "Bytes": len(blob),
        "HashCollisions": 0,
        "Source": {
            "Path": str(source_path).replace("\\", "/"),
            "HashAlgorithm": "FNV1a32",
            "HashInput": "raw_minified_jsonl_bytes",
            "Hash": format_hash(source_hash),
            "HashUInt32": source_hash,
        },
        "Output": str(output_path).replace("\\", "/"),
        "Header": build_header_manifest(header),
        "Sections": build_section_manifest(header),
        "Integrity": build_integrity_manifest(blob, header),
        "LookupContract": lookup_contract,
        "PayloadEnd": payload_end,
        "TrailerPaddingBytes": len(blob) - payload_end,
        "ToasterBinary": {
            "Profile": "toaster_i3_compact_only",
            "Output": str(toaster_output_path).replace("\\", "/"),
            "Entries": len(toaster_records),
            "Bytes": len(toaster_blob),
            "SizeReductionBytes": size_reduction_bytes,
            "SizeReductionPercentX100": int(round((size_reduction_bytes / len(blob)) * 10000)),
            "Header": build_header_manifest(toaster_header),
            "Sections": build_section_manifest(toaster_header),
            "Integrity": build_integrity_manifest(toaster_blob, toaster_header),
            "LookupContract": toaster_lookup_contract,
            "PayloadEnd": toaster_payload_end,
            "TrailerPaddingBytes": len(toaster_blob) - toaster_payload_end,
            "TierPayloads": ["CompactText"],
            "PayloadSource": "CompactText packed into Text section",
            "BinaryExtraPayloadEncoding": "none_stripped_for_toaster",
            "RuntimeAllocationPolicy": toaster_lookup_contract["RuntimeAllocationPolicy"],
            "UnityRuntimeProof": "PENDING_VERIFICATION",
        },
        "PhysicsAudit": {
            "EvidenceClass": "STATIC_SOURCE",
            "RuntimeSimulationAuthority": "none",
            "RequiredModels": build_physics_audit(source_entries),
        },
        "ToneAudit": {
            "EvidenceClass": "STATIC_SOURCE",
            "MinimumIndustrialHitsPerEntry": tone_audit["MinimumHitsPerEntry"],
            "WeakEntries": tone_audit["WeakEntries"],
            "PerEntryIndustrialHits": tone_audit["PerEntry"],
        },
        "TierPayloads": ["Text", "CompactText", "ExtraVisualRecord"],
        "AuthoringOnlyFields": ["ExtraData"],
        "BinaryExtraPayloadEncoding": "fixed_little_endian_uint32_record_no_json",
        "ExtraDataDerivation": {
            "SchemaVersion": EXTRA_SCHEMA_VERSION,
            "Model": EXTRA_DERIVATION_MODEL,
            "Formula": EXTRA_DERIVATION_FORMULA,
            "HydrostaticPaPerMeterX100": hydrostatic_pa_per_meter_x100(),
            "GradientResolution": HIGH_TIER_GRADIENT_RESOLUTION,
            "Authority": "presentation_only_no_simulation_authority",
            "BinaryRecordFields": [
                "SchemaVersion",
                "DerivationModelHash",
                "VisualDataAuthorityHash",
                "VisualProfileHash",
                "GradientLutHash",
                "GradientResolution",
                "GradientIndex",
                "NoiseSeed",
                "HarmonicOctaves",
                "HarmonicHzX100",
                "OverlayFlagsHash",
                "OverlayIndex",
                "StressState",
                "HydrostaticPaPerMeterX100",
                "SurfacePressurePa",
                "ProjectPressureMPaPer100mX1000",
            ],
        },
        "ProjectAtlasFit": {
            "Families": ["UI", "Narrative and prologue"],
            "DomainIds": list(PROJECT_ATLAS_DOMAIN_IDS),
            "DomainNames": [PROJECT_ATLAS_DOMAIN_NAMES[domain_id] for domain_id in PROJECT_ATLAS_DOMAIN_IDS],
            "Domain": "LORE/TEXT offline data artifact",
            "RuntimeAuthority": "none",
            "AsmdefAdded": False,
            "ProjectAtlasSource": "Docs/PROJECT_ATLAS.md",
        },
        "HPhiAudit": {
            "DataSovereigntyStaticScore": 1.0,
            "LookupModel": lookup_contract["Algorithm"],
            "LookupContract": "LookupContract",
            "ToasterLookupContract": "ToasterBinary.LookupContract",
            "PrivateRuntimeStateRequired": False,
            "RuntimeAllocationPolicy": lookup_contract["RuntimeAllocationPolicy"],
            "UnityRuntimeProof": "PENDING VERIFICATION",
        },
    }
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp_path = path.with_suffix(path.suffix + ".tmp")
    tmp_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    tmp_path.replace(path)


def build_arg_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Pack PDA technical logs into H8PT binaries.")
    parser.add_argument("--source", default=str(DEFAULT_SOURCE))
    parser.add_argument("--localization", default=str(DEFAULT_LOCALIZATION))
    parser.add_argument("--output", default=str(DEFAULT_OUTPUT))
    parser.add_argument("--toaster-output", default=str(DEFAULT_TOASTER_OUTPUT))
    parser.add_argument("--manifest", default=str(DEFAULT_MANIFEST))
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--list", action="store_true")
    return parser


def main(argv: list[str]) -> int:
    args = build_arg_parser().parse_args(argv)
    source_path = Path(args.source)
    output_path = Path(args.output)
    toaster_output_path = Path(args.toaster_output)
    manifest_path = Path(args.manifest)
    try:
        source_bytes = source_path.read_bytes()
        source_entries = load_jsonl(source_path)
        localization_entries = load_localization_entries(Path(args.localization))
        validate(source_entries, localization_entries)
        blob, records = build_blob(source_entries, source_bytes)
        toaster_blob, toaster_records = build_toaster_blob(source_entries, source_bytes)
        if args.check:
            existing = output_path.read_bytes()
            existing_toaster = toaster_output_path.read_bytes()
            verify_blob(existing)
            verify_blob(existing_toaster)
            if existing != blob:
                raise ValueError(f"PDA tech binary is stale: {output_path}")
            if existing_toaster != toaster_blob:
                raise ValueError(f"PDA tech toaster binary is stale: {toaster_output_path}")
        else:
            output_path.parent.mkdir(parents=True, exist_ok=True)
            tmp_output = output_path.with_suffix(output_path.suffix + ".tmp")
            tmp_output.write_bytes(blob)
            tmp_output.replace(output_path)
            toaster_output_path.parent.mkdir(parents=True, exist_ok=True)
            tmp_toaster = toaster_output_path.with_suffix(toaster_output_path.suffix + ".tmp")
            tmp_toaster.write_bytes(toaster_blob)
            tmp_toaster.replace(toaster_output_path)
            write_manifest(
                manifest_path,
                source_path,
                output_path,
                blob,
                records,
                source_entries,
                toaster_output_path,
                toaster_blob,
                toaster_records,
            )
    except (OSError, ValueError) as exc:
        raise SystemExit(f"PackPdaTechnicalLogs: {exc}") from exc
    print(
        "PDA TECH BINARY OK: "
        f"entries={len(records)} bytes={len(blob)} output={output_path} "
        f"toasterBytes={len(toaster_blob)} toasterOutput={toaster_output_path} "
        f"alignment={ALIGNMENT} endian=<"
    )
    if args.list:
        for record in records:
            print(f"{format_hash(int(record['Hash']))} {record['LocID']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
