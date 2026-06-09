#!/usr/bin/env python3
"""Pack HECTON-8 structured localization JSON into a binary UTF-8 blob.

Binary layout, little-endian:
    Header  : 4s magic "H8LB", u16 version, u16 header_size,
              u32 entry_count, u32 record_size, u32 table_offset,
              u32 data_offset, u32 data_length, u32 flags
    Records : entry_count * (u32 hash, u32 offset, u32 length)
    Payload : concatenated UTF-8 text bytes

Record offsets are relative to the start of the UTF-8 payload. Consumers that
want file-absolute offsets add `data_offset`.
"""

from __future__ import annotations

import argparse
import json
import struct
import sys
from pathlib import Path
from typing import Any


FNV_OFFSET_BASIS = 2166136261
FNV_PRIME = 16777619
MAGIC = b"H8LB"
VERSION = 1
HEADER_STRUCT = struct.Struct("<4sHHIIIIII")
RECORD_STRUCT = struct.Struct("<III")
HEADER_SIZE = HEADER_STRUCT.size
RECORD_SIZE = RECORD_STRUCT.size
FLAG_UTF16_FNV1A = 1
FLAG_OFFSET_RELATIVE_TO_PAYLOAD = 2
FLAGS = FLAG_UTF16_FNV1A | FLAG_OFFSET_RELATIVE_TO_PAYLOAD


def compute_loc_hash(loc_id: str) -> int:
    """Match Hecton.Localization.LocHash.Compute for authored keys."""
    if not loc_id:
        return 0

    encoded = loc_id.encode("utf-16le")
    h = FNV_OFFSET_BASIS
    for b in encoded:
        h ^= b
        h = (h * FNV_PRIME) & 0xFFFFFFFF
    return h


def parse_hash(value: Any) -> int | None:
    if value is None:
        return None
    if isinstance(value, int):
        return value & 0xFFFFFFFF
    if isinstance(value, str):
        stripped = value.strip()
        if not stripped or stripped.upper() == "AUTO":
            return None
        base = 16 if stripped.lower().startswith("0x") else 10
        return int(stripped, base) & 0xFFFFFFFF
    raise ValueError(f"Unsupported hash value type: {type(value).__name__}")


def format_hash(value: int) -> str:
    return f"0x{value & 0xFFFFFFFF:08X}"


def load_entries(path: Path) -> tuple[dict[str, Any], list[dict[str, Any]]]:
    with path.open("r", encoding="utf-8") as f:
        root = json.load(f)

    if not isinstance(root, dict):
        raise ValueError("Localization root must be an object.")
    entries = root.get("entries")
    if not isinstance(entries, list):
        raise ValueError("Localization JSON must contain an `entries` array.")
    return root, entries


def normalize_entries(entries: list[dict[str, Any]], rewrite_hashes: bool) -> list[dict[str, Any]]:
    seen_ids: set[str] = set()
    seen_hashes: dict[int, str] = {}

    for index, entry in enumerate(entries):
        if not isinstance(entry, dict):
            raise ValueError(f"Entry {index} is not an object.")

        loc_id = entry.get("LocID")
        text = entry.get("Text")
        if not isinstance(loc_id, str) or not loc_id:
            raise ValueError(f"Entry {index} missing non-empty LocID.")
        if not isinstance(text, str):
            raise ValueError(f"Entry {loc_id} missing string Text.")

        if loc_id in seen_ids:
            raise ValueError(f"Duplicate LocID: {loc_id}")
        seen_ids.add(loc_id)

        computed = compute_loc_hash(loc_id)
        expected = parse_hash(entry.get("Hash"))
        if expected is not None and expected != computed and not rewrite_hashes:
            raise ValueError(
                f"Hash mismatch for {loc_id}: JSON {format_hash(expected)} "
                f"!= computed {format_hash(computed)}"
            )

        owner = seen_hashes.get(computed)
        if owner is not None:
            raise ValueError(
                f"FNV-1a collision: {owner} and {loc_id} both use {format_hash(computed)}"
            )
        seen_hashes[computed] = loc_id

        entry["Hash"] = format_hash(computed)

    return entries


def write_normalized_json(root: dict[str, Any], path: Path) -> None:
    with path.open("w", encoding="utf-8", newline="\n") as f:
        json.dump(root, f, ensure_ascii=False, indent=2)
        f.write("\n")


def pack(entries: list[dict[str, Any]]) -> tuple[bytes, list[tuple[int, int, int, str]]]:
    payload = bytearray()
    records: list[tuple[int, int, int, str]] = []

    for entry in entries:
        loc_id = entry["LocID"]
        h = parse_hash(entry["Hash"])
        if h is None:
            raise ValueError(f"Entry {loc_id} has no hash after normalization.")

        encoded = entry["Text"].encode("utf-8")
        offset = len(payload)
        length = len(encoded)
        payload.extend(encoded)
        records.append((h, offset, length, loc_id))

    records.sort(key=lambda r: r[0])
    data_offset = HEADER_SIZE + (len(records) * RECORD_SIZE)
    header = HEADER_STRUCT.pack(
        MAGIC,
        VERSION,
        HEADER_SIZE,
        len(records),
        RECORD_SIZE,
        HEADER_SIZE,
        data_offset,
        len(payload),
        FLAGS,
    )

    blob = bytearray(header)
    for h, offset, length, _ in records:
        blob.extend(RECORD_STRUCT.pack(h, offset, length))
    blob.extend(payload)
    # Pad blob size to a multiple of 16
    remainder = len(blob) % 16
    if remainder != 0:
        blob.extend(b"\x00" * (16 - remainder))
    return bytes(blob), records


def verify_blob(blob: bytes, records: list[tuple[int, int, int, str]]) -> None:
    if len(blob) < HEADER_SIZE:
        raise ValueError("Blob is smaller than header.")

    magic, version, header_size, entry_count, record_size, table_offset, data_offset, data_length, flags = HEADER_STRUCT.unpack_from(blob, 0)
    if magic != MAGIC:
        raise ValueError("Bad magic.")
    if version != VERSION:
        raise ValueError(f"Unsupported version: {version}")
    if header_size != HEADER_SIZE or record_size != RECORD_SIZE:
        raise ValueError("Header or record size mismatch.")
    if entry_count != len(records):
        raise ValueError("Entry count mismatch.")
    if table_offset != HEADER_SIZE:
        raise ValueError("Unexpected table offset.")
    if data_offset != HEADER_SIZE + (entry_count * RECORD_SIZE):
        raise ValueError("Unexpected data offset.")
    padding_bytes = len(blob) - (data_offset + data_length)
    if padding_bytes < 0 or padding_bytes >= 16:
        raise ValueError(f"Payload length mismatch: data_offset={data_offset}, data_length={data_length}, blob_len={len(blob)}")
    if len(blob) % 16 != 0:
        raise ValueError("Blob is not 16-byte aligned.")
    if flags != FLAGS:
        raise ValueError("Flag mismatch.")

    last_hash = -1
    for i in range(entry_count):
        h, offset, length = RECORD_STRUCT.unpack_from(blob, table_offset + (i * RECORD_SIZE))
        if h <= last_hash:
            raise ValueError("Record table is not strictly sorted by hash.")
        last_hash = h
        if offset > data_length or length > data_length - offset:
            raise ValueError(f"Bad slice at record {i}.")


def build_arg_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Pack HECTON-8 localization JSON to binary.")
    parser.add_argument("--input", default="Data/Localization/en_US.json", help="Input structured JSON path.")
    parser.add_argument("--output", default="Data/Localization/en_US.bin", help="Output binary path.")
    parser.add_argument("--normalize", action="store_true", help="Rewrite JSON with computed FNV hashes.")
    parser.add_argument("--verify-only", action="store_true", help="Validate JSON without writing a binary.")
    return parser


def main(argv: list[str]) -> int:
    parser = build_arg_parser()
    args = parser.parse_args(argv)

    input_path = Path(args.input)
    output_path = Path(args.output)

    root, entries = load_entries(input_path)
    normalize_entries(entries, args.normalize)

    if args.normalize:
        write_normalized_json(root, input_path)

    blob, records = pack(entries)
    verify_blob(blob, records)

    if not args.verify_only:
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_bytes(blob)

    print(
        f"LORE COMPILED: entries={len(records)} bytes={len(blob)} "
        f"payload={len(blob) - (HEADER_SIZE + len(records) * RECORD_SIZE)} "
        f"output={output_path}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
