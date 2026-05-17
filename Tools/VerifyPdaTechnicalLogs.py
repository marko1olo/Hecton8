#!/usr/bin/env python3
"""Independently verify PDA technical log source, H8PT binaries, and manifest."""

from __future__ import annotations

import argparse
import json
import struct
import sys
import zlib
from pathlib import Path
from typing import Any

sys.path.insert(0, str(Path(__file__).resolve().parent))
import LoreTechValidator
from LocToBinary import compute_loc_hash, format_hash
from PdaTechSchema import (
    EXTRA_SCHEMA_VERSION,
    EXPECTED_ENTRY_COUNT,
    HIGH_TIER_GRADIENT_RESOLUTION,
    PROJECT_ATLAS_DOMAIN_IDS,
    PROJECT_ATLAS_DOMAIN_NAMES,
    hydrostatic_pa_per_meter_x100,
)


DEFAULT_BINARY = Path("Data/Lore/PdaTechnicalLogs.h8bin")
DEFAULT_TOASTER_BINARY = Path("Data/Lore/PdaTechnicalLogs_Toaster.h8bin")
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
FLAGS = 31
TOASTER_FLAGS = 39
HEADER_FIELDS = (
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


def fnv1a_bytes(data: bytes) -> int:
    value = 0x811C9DC5
    for byte in data:
        value ^= byte
        value = (value * 0x01000193) & 0xFFFFFFFF
    return value


def crc32_bytes(data: bytes) -> int:
    return zlib.crc32(data) & 0xFFFFFFFF


def read_header(blob: bytes) -> dict[str, int | bytes]:
    if len(blob) < HEADER_SIZE:
        raise ValueError("PDA H8PT blob is smaller than header")
    return {
        name: value
        for name, value in zip(HEADER_FIELDS, HEADER_STRUCT.unpack_from(blob, 0))
    }


def parse_hash(value: Any) -> int:
    if not isinstance(value, str) or not value.startswith("0x"):
        raise ValueError(f"Bad hash string: {value!r}")
    return int(value, 16) & 0xFFFFFFFF


def header_manifest(header: dict[str, int | bytes]) -> dict[str, int | str]:
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


def section_descriptor(offset: int, length: int) -> dict[str, int]:
    return {
        "Offset": offset,
        "Length": length,
        "EndOffset": offset + length,
        "Alignment": ALIGNMENT,
    }


def section_manifest(header: dict[str, int | bytes]) -> dict[str, dict[str, int]]:
    sections = {
        "RecordTable": section_descriptor(
            int(header["TableOffset"]),
            int(header["EntryCount"]) * int(header["RecordSize"]),
        ),
        "Text": section_descriptor(int(header["TextOffset"]), int(header["TextLength"])),
        "CompactText": section_descriptor(int(header["CompactOffset"]), int(header["CompactLength"])),
        "ExtraVisualRecord": section_descriptor(int(header["ExtraOffset"]), int(header["ExtraLength"])),
    }
    sections["RecordTable"]["RecordSize"] = int(header["RecordSize"])
    sections["ExtraVisualRecord"]["RecordSize"] = EXTRA_VISUAL_RECORD_SIZE
    return sections


def integrity_manifest(blob: bytes, header: dict[str, int | bytes]) -> dict[str, int | str]:
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


def lookup_contract(header: dict[str, int | bytes]) -> dict[str, int | str | list[str]]:
    is_toaster = int(header["Flags"]) == TOASTER_FLAGS
    return {
        "EvidenceClass": "CLI_TOOLING",
        "Algorithm": "uint32_binary_search_sorted_record_table",
        "RecordKey": "LocHash",
        "HashAlgorithm": "FNV1a32_UTF16LE_LocID",
        "TableOrder": "strict_ascending_uint32",
        "TableOffset": int(header["TableOffset"]),
        "EntryCount": int(header["EntryCount"]),
        "RecordStrideBytes": int(header["RecordSize"]),
        "SearchIterationsMax": max(1, int(header["EntryCount"]).bit_length()),
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


def decode_records(blob: bytes, header: dict[str, int | bytes]) -> list[tuple[int, ...]]:
    entry_count = int(header["EntryCount"])
    table_offset = int(header["TableOffset"])
    records: list[tuple[int, ...]] = []
    last_hash = -1
    for index in range(entry_count):
        offset = table_offset + index * RECORD_SIZE
        record = RECORD_STRUCT.unpack_from(blob, offset)
        if record[0] <= last_hash:
            raise ValueError("PDA H8PT record hashes are not strictly sorted")
        last_hash = record[0]
        records.append(record)
    if len({record[0] for record in records}) != len(records):
        raise ValueError("PDA H8PT record hash collision")
    return records


def verify_header(blob: bytes, expected_flags: int) -> dict[str, int | bytes]:
    if len(blob) % ALIGNMENT:
        raise ValueError("PDA H8PT blob is not 16-byte aligned")
    header = read_header(blob)
    if header["Magic"] != MAGIC:
        raise ValueError("PDA H8PT magic mismatch")
    if int(header["Version"]) != VERSION:
        raise ValueError("PDA H8PT version mismatch")
    if int(header["HeaderSize"]) != HEADER_SIZE:
        raise ValueError("PDA H8PT header size mismatch")
    if int(header["RecordSize"]) != RECORD_SIZE:
        raise ValueError("PDA H8PT record size mismatch")
    if int(header["TableOffset"]) != HEADER_SIZE:
        raise ValueError("PDA H8PT table offset mismatch")
    if int(header["Flags"]) != expected_flags:
        raise ValueError(f"PDA H8PT flags mismatch: {int(header['Flags'])}")
    if int(header["Reserved0"]) or int(header["Reserved1"]) or int(header["Reserved2"]):
        raise ValueError("PDA H8PT reserved fields must be zero")
    expected_text_offset = int(header["TableOffset"]) + int(header["EntryCount"]) * int(header["RecordSize"])
    if int(header["TextOffset"]) != expected_text_offset:
        raise ValueError("PDA H8PT text offset mismatch")
    if int(header["CompactOffset"]) != int(header["TextOffset"]) + int(header["TextLength"]):
        raise ValueError("PDA H8PT compact offset mismatch")
    if int(header["ExtraOffset"]) != int(header["CompactOffset"]) + int(header["CompactLength"]):
        raise ValueError("PDA H8PT extra offset mismatch")
    payload_end = int(header["ExtraOffset"]) + int(header["ExtraLength"])
    if payload_end > len(blob):
        raise ValueError("PDA H8PT payload overruns blob")
    if any(blob[payload_end:]):
        raise ValueError("PDA H8PT nonzero trailer padding")
    for key in ("TextOffset", "CompactOffset", "ExtraOffset"):
        if int(header[key]) % ALIGNMENT:
            raise ValueError(f"PDA H8PT {key} is not 16-byte aligned")
    return header


def record_text(blob: bytes, section_offset: int, offset: int, length: int) -> str:
    if offset % ALIGNMENT:
        raise ValueError("PDA H8PT record payload offset is not aligned")
    return blob[section_offset + offset : section_offset + offset + length].decode("utf-8")


def verify_full_binary(
    blob: bytes,
    entries_by_hash: dict[int, dict[str, Any]],
) -> dict[str, int | bytes]:
    header = verify_header(blob, FLAGS)
    if int(header["EntryCount"]) != EXPECTED_ENTRY_COUNT:
        raise ValueError("PDA full H8PT entry count mismatch")
    records = decode_records(blob, header)
    if set(record[0] for record in records) != set(entries_by_hash):
        raise ValueError("PDA full H8PT hash set mismatch")
    text_offset = int(header["TextOffset"])
    compact_offset = int(header["CompactOffset"])
    extra_offset = int(header["ExtraOffset"])
    for record in records:
        entry = entries_by_hash[record[0]]
        if record[1] != compute_loc_hash(str(entry["Category"])):
            raise ValueError(f"{entry['LocID']} category hash mismatch")
        if record[8] != parse_hash(entry["LinkHash"]):
            raise ValueError(f"{entry['LocID']} link hash mismatch")
        if record_text(blob, text_offset, record[2], record[3]) != entry["Text"]:
            raise ValueError(f"{entry['LocID']} full text payload mismatch")
        if record_text(blob, compact_offset, record[4], record[5]) != entry["CompactText"]:
            raise ValueError(f"{entry['LocID']} compact text payload mismatch")
        if record[7] != EXTRA_VISUAL_RECORD_SIZE:
            raise ValueError(f"{entry['LocID']} extra visual size mismatch")
        fields = EXTRA_VISUAL_RECORD_STRUCT.unpack_from(blob, extra_offset + record[6])
        extra = entry["ExtraData"]
        if fields[0] != EXTRA_SCHEMA_VERSION:
            raise ValueError(f"{entry['LocID']} extra schema mismatch")
        if fields[5] != HIGH_TIER_GRADIENT_RESOLUTION:
            raise ValueError(f"{entry['LocID']} gradient resolution mismatch")
        if fields[7] != int(extra["NoiseSeed"]):
            raise ValueError(f"{entry['LocID']} noise seed mismatch")
        if fields[8] != int(extra["HarmonicOctaves"]):
            raise ValueError(f"{entry['LocID']} harmonic octave mismatch")
        if fields[9] != int(extra["HarmonicHzX100"]):
            raise ValueError(f"{entry['LocID']} harmonic frequency mismatch")
        if fields[12] != int(extra["StressState"]):
            raise ValueError(f"{entry['LocID']} stress state mismatch")
        if fields[13] != hydrostatic_pa_per_meter_x100():
            raise ValueError(f"{entry['LocID']} hydrostatic constant mismatch")
    return header


def verify_toaster_binary(
    blob: bytes,
    entries_by_hash: dict[int, dict[str, Any]],
) -> dict[str, int | bytes]:
    header = verify_header(blob, TOASTER_FLAGS)
    if int(header["EntryCount"]) != EXPECTED_ENTRY_COUNT:
        raise ValueError("PDA toaster H8PT entry count mismatch")
    if int(header["CompactLength"]) or int(header["ExtraLength"]):
        raise ValueError("PDA toaster H8PT must strip compact and extra sections")
    records = decode_records(blob, header)
    if set(record[0] for record in records) != set(entries_by_hash):
        raise ValueError("PDA toaster H8PT hash set mismatch")
    text_offset = int(header["TextOffset"])
    for record in records:
        entry = entries_by_hash[record[0]]
        if record_text(blob, text_offset, record[2], record[3]) != entry["CompactText"]:
            raise ValueError(f"{entry['LocID']} toaster compact payload mismatch")
        if any(record[index] for index in (4, 5, 6, 7, 9, 12, 13, 14, 15)):
            raise ValueError(f"{entry['LocID']} toaster visual fields were not stripped")
        if record[8] != parse_hash(entry["LinkHash"]):
            raise ValueError(f"{entry['LocID']} toaster link hash mismatch")
        if record[11] != int(entry["ExtraData"]["StressState"]):
            raise ValueError(f"{entry['LocID']} toaster stress state mismatch")
    return header


def verify_manifest(
    manifest: dict[str, Any],
    source_path: Path,
    source_bytes: bytes,
    full_blob: bytes,
    full_header: dict[str, int | bytes],
    toaster_blob: bytes,
    toaster_header: dict[str, int | bytes],
) -> None:
    source_hash = fnv1a_bytes(source_bytes)
    if manifest.get("Magic") != "H8PT":
        raise ValueError("PDA manifest magic mismatch")
    if manifest.get("Endian") != "Little":
        raise ValueError("PDA manifest endian mismatch")
    if manifest.get("Alignment") != ALIGNMENT:
        raise ValueError("PDA manifest alignment mismatch")
    if manifest.get("HashCollisions") != 0:
        raise ValueError("PDA manifest reports hash collisions")
    source = manifest.get("Source")
    if not isinstance(source, dict) or source.get("Path") != str(source_path).replace("\\", "/"):
        raise ValueError("PDA manifest source path mismatch")
    if source.get("HashUInt32") != source_hash or source.get("Hash") != format_hash(source_hash):
        raise ValueError("PDA manifest source hash mismatch")
    if manifest.get("Bytes") != len(full_blob):
        raise ValueError("PDA manifest full byte count mismatch")
    if manifest.get("Header") != header_manifest(full_header):
        raise ValueError("PDA manifest full header mismatch")
    if manifest.get("Sections") != section_manifest(full_header):
        raise ValueError("PDA manifest full section mismatch")
    if manifest.get("Integrity") != integrity_manifest(full_blob, full_header):
        raise ValueError("PDA manifest full integrity mismatch")
    if manifest.get("LookupContract") != lookup_contract(full_header):
        raise ValueError("PDA manifest full lookup contract mismatch")
    payload_end = int(full_header["ExtraOffset"]) + int(full_header["ExtraLength"])
    if manifest.get("PayloadEnd") != payload_end:
        raise ValueError("PDA manifest full payload end mismatch")
    if manifest.get("TrailerPaddingBytes") != len(full_blob) - payload_end:
        raise ValueError("PDA manifest full trailer padding mismatch")

    toaster = manifest.get("ToasterBinary")
    if not isinstance(toaster, dict):
        raise ValueError("PDA manifest missing ToasterBinary")
    if toaster.get("Bytes") != len(toaster_blob):
        raise ValueError("PDA manifest toaster byte count mismatch")
    if toaster.get("Header") != header_manifest(toaster_header):
        raise ValueError("PDA manifest toaster header mismatch")
    if toaster.get("Sections") != section_manifest(toaster_header):
        raise ValueError("PDA manifest toaster section mismatch")
    if toaster.get("Integrity") != integrity_manifest(toaster_blob, toaster_header):
        raise ValueError("PDA manifest toaster integrity mismatch")
    if toaster.get("LookupContract") != lookup_contract(toaster_header):
        raise ValueError("PDA manifest toaster lookup mismatch")
    size_reduction = len(full_blob) - len(toaster_blob)
    if toaster.get("SizeReductionBytes") != size_reduction:
        raise ValueError("PDA manifest toaster reduction mismatch")
    if toaster.get("SizeReductionPercentX100") != int(round((size_reduction / len(full_blob)) * 10000)):
        raise ValueError("PDA manifest toaster reduction percent mismatch")

    atlas = manifest.get("ProjectAtlasFit")
    if not isinstance(atlas, dict):
        raise ValueError("PDA manifest missing ProjectAtlasFit")
    if tuple(atlas.get("DomainIds", ())) != PROJECT_ATLAS_DOMAIN_IDS:
        raise ValueError("PDA manifest atlas domain IDs mismatch")
    expected_names = [PROJECT_ATLAS_DOMAIN_NAMES[domain_id] for domain_id in PROJECT_ATLAS_DOMAIN_IDS]
    if atlas.get("DomainNames") != expected_names:
        raise ValueError("PDA manifest atlas domain names mismatch")
    atlas_text = Path("Docs/PROJECT_ATLAS.md").read_text(encoding="utf-8")
    for domain_id, domain_name in zip(PROJECT_ATLAS_DOMAIN_IDS, expected_names):
        if f"| {domain_id} |" not in atlas_text or domain_name not in atlas_text:
            raise ValueError(f"PROJECT_ATLAS missing PDA domain {domain_id}")

    h_phi = manifest.get("HPhiAudit")
    if not isinstance(h_phi, dict):
        raise ValueError("PDA manifest missing HPhiAudit")
    if h_phi.get("PrivateRuntimeStateRequired") is not False:
        raise ValueError("PDA manifest H-Phi private state mismatch")
    if h_phi.get("RuntimeAllocationPolicy") != "caller_owned_buffer_no_private_cache":
        raise ValueError("PDA manifest H-Phi allocation policy mismatch")


def verify(
    source_path: Path = LoreTechValidator.DEFAULT_SOURCE,
    localization_path: Path = LoreTechValidator.DEFAULT_LOCALIZATION,
    binary_path: Path = DEFAULT_BINARY,
    manifest_path: Path = DEFAULT_MANIFEST,
    toaster_binary_path: Path = DEFAULT_TOASTER_BINARY,
) -> tuple[int, int, int]:
    entries = LoreTechValidator.load_jsonl(source_path)
    localization_entries = LoreTechValidator.load_localization_entries(localization_path)
    LoreTechValidator.validate(entries, localization_entries)
    source_bytes = source_path.read_bytes()
    source_hashes = [parse_hash(entry["Hash"]) for entry in entries]
    if len(source_hashes) != len(set(source_hashes)):
        raise ValueError("PDA source hash collision")
    entries_by_hash = {parse_hash(entry["Hash"]): entry for entry in entries}

    full_blob = binary_path.read_bytes()
    toaster_blob = toaster_binary_path.read_bytes()
    full_header = verify_full_binary(full_blob, entries_by_hash)
    toaster_header = verify_toaster_binary(toaster_blob, entries_by_hash)
    if int(full_header["SourceHash"]) != fnv1a_bytes(source_bytes):
        raise ValueError("PDA full H8PT source hash mismatch")
    if int(toaster_header["SourceHash"]) != int(full_header["SourceHash"]):
        raise ValueError("PDA toaster H8PT source hash mismatch")
    if int(toaster_header["TextLength"]) != int(full_header["CompactLength"]):
        raise ValueError("PDA toaster text length must match full compact section")
    if len(toaster_blob) >= len(full_blob):
        raise ValueError("PDA toaster H8PT is not smaller than full H8PT")

    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    verify_manifest(
        manifest,
        source_path,
        source_bytes,
        full_blob,
        full_header,
        toaster_blob,
        toaster_header,
    )
    return len(entries), len(full_blob), len(toaster_blob)


def build_arg_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Verify PDA technical log data contract.")
    parser.add_argument("--source", default=str(LoreTechValidator.DEFAULT_SOURCE))
    parser.add_argument("--localization", default=str(LoreTechValidator.DEFAULT_LOCALIZATION))
    parser.add_argument("--binary", default=str(DEFAULT_BINARY))
    parser.add_argument("--manifest", default=str(DEFAULT_MANIFEST))
    parser.add_argument("--toaster-binary", default=str(DEFAULT_TOASTER_BINARY))
    return parser


def main(argv: list[str]) -> int:
    args = build_arg_parser().parse_args(argv)
    try:
        entries, binary_bytes, toaster_bytes = verify(
            Path(args.source),
            Path(args.localization),
            Path(args.binary),
            Path(args.manifest),
            Path(args.toaster_binary),
        )
    except (OSError, ValueError) as exc:
        raise SystemExit(f"VerifyPdaTechnicalLogs: {exc}") from exc
    print(
        "VERIFY_PDA_TECH_LOGS: "
        f"entries={entries} binaryBytes={binary_bytes} toasterBytes={toaster_bytes} "
        f"alignment={ALIGNMENT} endian=< hashCollisions=0 hPhiDataSovereignty=1.0"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
