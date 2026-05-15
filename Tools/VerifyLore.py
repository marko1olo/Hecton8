#!/usr/bin/env python3
"""Bake and inspect the HECTON-8 lore encyclopedia blob.

Binary layout, little-endian:
    Header  : 4s magic "H8LR", u16 version, u16 header_size,
              u32 entry_count, u32 record_size, u32 table_offset,
              u32 table_bytes, u32 payload_offset, u32 flags
    Records : entry_count * (u32 hash, u64 file_offset, u32 compressed_length)
    Payload : zlib-compressed Markdown bytes. Every payload starts on a
              16-byte boundary. Record offsets are absolute file offsets.

Hash contract matches H8DataHash.ComputeFnv1A32 for authored ASCII IDs:
FNV-1a 32-bit, ASCII A-Z folded to lowercase, zero remapped to one.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import struct
import sys
import zlib
from dataclasses import dataclass
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
FNV_OFFSET_BASIS = 2166136261
FNV_PRIME = 16777619
MAGIC = b"H8LR"
VERSION = 1
ALIGNMENT = 16
HEADER_STRUCT = struct.Struct("<4sHHIIIIII")
RECORD_STRUCT = struct.Struct("<IQI")
HEADER_SIZE = HEADER_STRUCT.size
RECORD_SIZE = RECORD_STRUCT.size
FLAG_FNV1A_ASCII_LOWER = 1
FLAG_ZLIB = 2
FLAG_UTF8_MARKDOWN = 4
FLAG_ABSOLUTE_OFFSETS = 8
FLAGS = (
    FLAG_FNV1A_ASCII_LOWER
    | FLAG_ZLIB
    | FLAG_UTF8_MARKDOWN
    | FLAG_ABSOLUTE_OFFSETS
)
HASH_ALGORITHM = "FNV-1a 32-bit over repository-relative ASCII path, A-Z folded to lowercase"
COMPRESSION = "zlib"
RECORD_LAYOUT = "uint32 hash, uint64 absolute_offset, uint32 compressed_length"
DEFAULT_BLOB_RELATIVE = Path("Data/Lore/Encyclopedia.h8bin")
DEFAULT_MANIFEST_RELATIVE = Path("Data/Lore/Encyclopedia.manifest.json")
PRIMARY_SOURCE_DIR_RELATIVE = Path("Docs/Lore")
DEFAULT_BLOB = REPO_ROOT / DEFAULT_BLOB_RELATIVE
DEFAULT_MANIFEST = REPO_ROOT / DEFAULT_MANIFEST_RELATIVE
PRIMARY_SOURCE_DIR = REPO_ROOT / PRIMARY_SOURCE_DIR_RELATIVE


@dataclass(frozen=True)
class LoreRecord:
    hash_value: int
    offset: int
    length: int


@dataclass(frozen=True)
class SourceEntry:
    source_path: Path
    canonical_id: str
    hash_value: int
    payload: bytes


def align_up(value: int, alignment: int = ALIGNMENT) -> int:
    return (value + (alignment - 1)) & ~(alignment - 1)


def compute_fnv1a32(value: str) -> int:
    if not value:
        return 0

    hash_value = FNV_OFFSET_BASIS
    for char in value:
        code = ord(char)
        if 65 <= code <= 90:
            code += 32
        hash_value ^= code & 0xFF
        hash_value = (hash_value * FNV_PRIME) & 0xFFFFFFFF
    return hash_value if hash_value != 0 else 1


def parse_hash(value: str) -> int:
    stripped = value.strip()
    try:
        if stripped.lower().startswith("0x"):
            parsed = int(stripped, 16)
        else:
            parsed = int(stripped, 10)
    except ValueError as exc:
        raise ValueError(f"Invalid hash value: {value}") from exc

    if parsed < 0 or parsed > 0xFFFFFFFF:
        raise ValueError(f"Hash value out of uint32 range: {value}")
    return parsed


def format_hash(value: int) -> str:
    return f"0x{value & 0xFFFFFFFF:08X}"


def resolve_repo_path(path: Path) -> Path:
    expanded = path.expanduser()
    if expanded.is_absolute():
        return expanded.resolve()
    return (REPO_ROOT / expanded).resolve()


def format_repo_path(path: Path) -> str:
    resolved = resolve_repo_path(path)
    try:
        return resolved.relative_to(REPO_ROOT).as_posix()
    except ValueError:
        return str(resolved)


def canonicalize_path(path: Path) -> str:
    resolved = resolve_repo_path(path)
    try:
        relative = resolved.relative_to(REPO_ROOT)
    except ValueError:
        raise ValueError(f"Lore source path must be inside the repository root: {resolved}")

    canonical = relative.as_posix()
    if any(ord(char) > 127 for char in canonical):
        raise ValueError(f"Non-ASCII lore source path cannot use H8DataHash contract: {canonical}")
    return canonical


def require_utf8_markdown(canonical_id: str, payload: bytes) -> None:
    try:
        payload.decode("utf-8")
    except UnicodeDecodeError as exc:
        raise ValueError(f"Markdown source is not valid UTF-8: {canonical_id}") from exc


def validate_source_entries(entries: list[SourceEntry]) -> None:
    seen_hashes: dict[int, str] = {}
    seen_ids: set[str] = set()

    for entry in entries:
        if not entry.canonical_id:
            raise ValueError("Lore source canonical id is empty.")
        if entry.canonical_id in seen_ids:
            raise ValueError(f"Duplicate lore source canonical id: {entry.canonical_id}")
        seen_ids.add(entry.canonical_id)

        expected_hash = compute_fnv1a32(entry.canonical_id)
        if entry.hash_value != expected_hash:
            raise ValueError(
                f"Hash mismatch for {entry.canonical_id}: "
                f"{format_hash(entry.hash_value)} != {format_hash(expected_hash)}"
            )

        owner = seen_hashes.get(entry.hash_value)
        if owner is not None:
            raise ValueError(
                f"FNV-1a collision: {owner} and {entry.canonical_id} both use {format_hash(entry.hash_value)}"
            )
        seen_hashes[entry.hash_value] = entry.canonical_id
        require_utf8_markdown(entry.canonical_id, entry.payload)


def discover_markdown_sources(source_dir: Path) -> list[Path]:
    resolved_source_dir = resolve_repo_path(source_dir)
    if resolved_source_dir.exists():
        paths = sorted(
            [path for path in resolved_source_dir.rglob("*.md") if path.is_file()],
            key=lambda path: format_repo_path(path).lower(),
        )
        if paths:
            return paths

    return []


def atomic_write_bytes(path: Path, payload: bytes) -> None:
    resolved = resolve_repo_path(path)
    temp_path = resolved.with_name(resolved.name + ".tmp")
    try:
        resolved.parent.mkdir(parents=True, exist_ok=True)
        temp_path.write_bytes(payload)
        temp_path.replace(resolved)
    except OSError as exc:
        try:
            if temp_path.exists():
                temp_path.unlink()
        except OSError:
            pass
        raise ValueError(f"Cannot write file atomically: {format_repo_path(resolved)}") from exc


def atomic_write_text(path: Path, payload: str) -> None:
    atomic_write_bytes(path, payload.encode("utf-8"))


def load_source_entries(source_dir: Path) -> list[SourceEntry]:
    source_paths = discover_markdown_sources(source_dir)
    entries: list[SourceEntry] = []

    for path in source_paths:
        canonical_id = canonicalize_path(path)
        hash_value = compute_fnv1a32(canonical_id)
        payload = path.read_bytes()
        entries.append(
            SourceEntry(
                source_path=path,
                canonical_id=canonical_id,
                hash_value=hash_value,
                payload=payload,
            )
        )

    validate_source_entries(entries)
    return entries


def bake_blob(entries: list[SourceEntry]) -> bytes:
    if not entries:
        raise ValueError("No Markdown lore sources found.")

    validate_source_entries(entries)
    sorted_entries = sorted(entries, key=lambda entry: entry.hash_value)
    table_offset = HEADER_SIZE
    table_bytes = len(sorted_entries) * RECORD_SIZE
    payload_offset = align_up(table_offset + table_bytes)

    payload = bytearray()
    records: list[LoreRecord] = []
    for entry in sorted_entries:
        absolute_offset = payload_offset + align_up(len(payload))
        padding = absolute_offset - (payload_offset + len(payload))
        if padding > 0:
            payload.extend(b"\x00" * padding)

        compressed = zlib.compress(entry.payload, level=9)
        records.append(LoreRecord(entry.hash_value, absolute_offset, len(compressed)))
        payload.extend(compressed)

    blob = bytearray()
    blob.extend(
        HEADER_STRUCT.pack(
            MAGIC,
            VERSION,
            HEADER_SIZE,
            len(records),
            RECORD_SIZE,
            table_offset,
            table_bytes,
            payload_offset,
            FLAGS,
        )
    )

    for record in records:
        blob.extend(RECORD_STRUCT.pack(record.hash_value, record.offset, record.length))

    if len(blob) < payload_offset:
        blob.extend(b"\x00" * (payload_offset - len(blob)))
    blob.extend(payload)
    return bytes(blob)


def read_blob(path: Path) -> tuple[bytes, list[LoreRecord]]:
    blob = resolve_repo_path(path).read_bytes()
    return blob, parse_blob(blob)


def parse_blob(blob: bytes) -> list[LoreRecord]:
    if len(blob) < HEADER_SIZE:
        raise ValueError("Blob is smaller than the fixed header.")

    (
        magic,
        version,
        header_size,
        entry_count,
        record_size,
        table_offset,
        table_bytes,
        payload_offset,
        flags,
    ) = HEADER_STRUCT.unpack_from(blob, 0)

    if magic != MAGIC:
        raise ValueError(f"Bad magic: {magic!r}")
    if version != VERSION:
        raise ValueError(f"Unsupported version: {version}")
    if header_size != HEADER_SIZE or record_size != RECORD_SIZE:
        raise ValueError("Header or record size mismatch.")
    if (header_size % ALIGNMENT) != 0 or (table_offset % ALIGNMENT) != 0:
        raise ValueError("Header/table offset is not 16-byte aligned.")
    if table_offset != HEADER_SIZE:
        raise ValueError("Unexpected table offset.")
    if table_bytes != entry_count * RECORD_SIZE:
        raise ValueError("Table byte count does not match entry count.")
    if table_offset + table_bytes > len(blob):
        raise ValueError("Record table points beyond file length.")
    if payload_offset != align_up(table_offset + table_bytes):
        raise ValueError("Payload offset does not match aligned table end.")
    if payload_offset > len(blob):
        raise ValueError("Payload offset points beyond file length.")
    if flags != FLAGS:
        raise ValueError(f"Unsupported flags: 0x{flags:08X}")

    records: list[LoreRecord] = []
    previous_hash = -1
    for index in range(entry_count):
        record_offset = table_offset + (index * RECORD_SIZE)
        hash_value, payload_file_offset, compressed_length = RECORD_STRUCT.unpack_from(blob, record_offset)
        if hash_value <= previous_hash:
            raise ValueError("Record table is not strictly sorted by hash.")
        previous_hash = hash_value
        if (payload_file_offset % ALIGNMENT) != 0:
            raise ValueError(f"Payload offset for {format_hash(hash_value)} is not aligned.")
        if compressed_length <= 0:
            raise ValueError(f"Payload length for {format_hash(hash_value)} is empty.")
        if payload_file_offset < payload_offset or payload_file_offset + compressed_length > len(blob):
            raise ValueError(f"Payload slice for {format_hash(hash_value)} is outside file bounds.")
        records.append(LoreRecord(hash_value, payload_file_offset, compressed_length))

    intervals = sorted(records, key=lambda record: record.offset)
    previous_end = payload_offset
    for record in intervals:
        if record.offset < previous_end:
            raise ValueError(f"Payload slice for {format_hash(record.hash_value)} overlaps a previous record.")
        if any(blob[index] != 0 for index in range(previous_end, record.offset)):
            raise ValueError(f"Payload padding before {format_hash(record.hash_value)} is not zeroed.")
        previous_end = record.offset + record.length

    if previous_end != len(blob):
        raise ValueError("Blob contains trailing bytes after the last payload.")

    return records


def find_record(records: list[LoreRecord], hash_value: int) -> LoreRecord | None:
    low = 0
    high = len(records) - 1
    while low <= high:
        mid = low + ((high - low) >> 1)
        candidate = records[mid]
        if candidate.hash_value == hash_value:
            return candidate
        if candidate.hash_value < hash_value:
            low = mid + 1
        else:
            high = mid - 1
    return None


def extract_payload(blob: bytes, record: LoreRecord) -> bytes:
    compressed = blob[record.offset : record.offset + record.length]
    try:
        return zlib.decompress(compressed)
    except zlib.error as exc:
        raise ValueError(f"Payload decompression failed for {format_hash(record.hash_value)}") from exc


def verify_entries_against_blob(blob: bytes, records: list[LoreRecord], entries: list[SourceEntry]) -> None:
    validate_source_entries(entries)
    if len(entries) != len(records):
        raise ValueError(
            f"Source/blob count mismatch: sources={len(entries)} blob={len(records)}"
        )

    for entry in entries:
        record = find_record(records, entry.hash_value)
        if record is None:
            raise ValueError(f"Missing blob record for {entry.canonical_id} {format_hash(entry.hash_value)}")
        extracted = extract_payload(blob, record)
        if extracted != entry.payload:
            raise ValueError(f"Payload mismatch for {entry.canonical_id} {format_hash(entry.hash_value)}")


def verify_against_sources(blob_path: Path, source_dir: Path) -> None:
    blob, records = read_blob(blob_path)
    verify_entries_against_blob(blob, records, load_source_entries(source_dir))


def write_manifest(blob_path: Path, manifest_path: Path, source_dir: Path) -> None:
    blob, records = read_blob(blob_path)
    entries = load_source_entries(source_dir)
    manifest = build_manifest_data(format_repo_path(blob_path), blob, records, entries, format_repo_path(source_dir))

    atomic_write_text(manifest_path, json.dumps(manifest, indent=2) + "\n")


def build_manifest_data(
    blob_label: str,
    blob: bytes,
    records: list[LoreRecord],
    entries: list[SourceEntry],
    source_dir_label: str,
) -> dict[str, object]:
    validate_source_entries(entries)
    if len(entries) != len(records):
        raise ValueError(
            f"Cannot write manifest. Source/blob count mismatch: sources={len(entries)} blob={len(records)}"
        )

    entry_by_hash = {entry.hash_value: entry for entry in entries}

    manifest_entries = []
    for record in records:
        entry = entry_by_hash.get(record.hash_value)
        if entry is None:
            raise ValueError(f"Cannot write manifest. Missing source for {format_hash(record.hash_value)}")

        payload = extract_payload(blob, record)
        if payload != entry.payload:
            raise ValueError(
                f"Cannot write manifest. Payload mismatch for {entry.canonical_id} {format_hash(record.hash_value)}"
            )
        manifest_entries.append(
            {
                "hash": format_hash(record.hash_value),
                "canonical_id": entry.canonical_id,
                "offset": record.offset,
                "compressed_length": record.length,
                "decompressed_length": len(payload),
                "sha256": hashlib.sha256(payload).hexdigest().upper(),
            }
        )

    manifest = {
        "magic": MAGIC.decode("ascii"),
        "version": VERSION,
        "hash_algorithm": HASH_ALGORITHM,
        "compression": COMPRESSION,
        "alignment_bytes": ALIGNMENT,
        "record_layout": RECORD_LAYOUT,
        "blob": blob_label,
        "blob_length": len(blob),
        "blob_sha256": hashlib.sha256(blob).hexdigest().upper(),
        "source_dir": source_dir_label,
        "entry_count": len(manifest_entries),
        "entries": manifest_entries,
    }

    return manifest


def verify_manifest(blob_path: Path, manifest_path: Path, source_dir: Path) -> None:
    blob, records = read_blob(blob_path)
    entries = load_source_entries(source_dir)
    manifest_text = resolve_repo_path(manifest_path).read_text(encoding="utf-8")
    verify_manifest_data(
        blob,
        records,
        entries,
        json.loads(manifest_text),
        expected_blob_label=format_repo_path(blob_path),
        expected_source_dir_label=format_repo_path(source_dir),
    )


def verify_manifest_data(
    blob: bytes,
    records: list[LoreRecord],
    entries: list[SourceEntry],
    manifest: object,
    expected_blob_label: str | None = None,
    expected_source_dir_label: str | None = None,
) -> None:
    validate_source_entries(entries)
    record_by_hash = {record.hash_value: record for record in records}
    entry_by_hash = {entry.hash_value: entry for entry in entries}

    if not isinstance(manifest, dict):
        raise ValueError("Manifest root must be an object.")
    if manifest.get("magic") != MAGIC.decode("ascii"):
        raise ValueError("Manifest magic mismatch.")
    if manifest.get("version") != VERSION:
        raise ValueError("Manifest version mismatch.")
    if manifest.get("hash_algorithm") != HASH_ALGORITHM:
        raise ValueError("Manifest hash algorithm mismatch.")
    if manifest.get("compression") != COMPRESSION:
        raise ValueError("Manifest compression mismatch.")
    if manifest.get("alignment_bytes") != ALIGNMENT:
        raise ValueError("Manifest alignment mismatch.")
    if manifest.get("record_layout") != RECORD_LAYOUT:
        raise ValueError("Manifest record layout mismatch.")
    if expected_blob_label is not None and manifest.get("blob") != expected_blob_label:
        raise ValueError("Manifest blob path mismatch.")
    if expected_source_dir_label is not None and manifest.get("source_dir") != expected_source_dir_label:
        raise ValueError("Manifest source directory mismatch.")
    if manifest.get("entry_count") != len(records):
        raise ValueError("Manifest entry count mismatch.")
    if int(manifest.get("blob_length", -1)) != len(blob):
        raise ValueError("Manifest blob length mismatch.")
    if manifest.get("blob_sha256") != hashlib.sha256(blob).hexdigest().upper():
        raise ValueError("Manifest blob SHA-256 mismatch.")

    manifest_entries = manifest.get("entries")
    if not isinstance(manifest_entries, list):
        raise ValueError("Manifest entries must be a list.")
    if len(manifest_entries) != len(records):
        raise ValueError("Manifest entry list length mismatch.")

    seen_hashes: set[int] = set()
    for item in manifest_entries:
        if not isinstance(item, dict):
            raise ValueError("Manifest entry must be an object.")

        hash_value = parse_hash(str(item.get("hash", "")))
        if hash_value in seen_hashes:
            raise ValueError(f"Duplicate manifest hash: {format_hash(hash_value)}")
        seen_hashes.add(hash_value)

        record = record_by_hash.get(hash_value)
        entry = entry_by_hash.get(hash_value)
        if record is None or entry is None:
            raise ValueError(f"Manifest hash missing in blob or source: {format_hash(hash_value)}")

        if item.get("canonical_id") != entry.canonical_id:
            raise ValueError(f"Manifest canonical id mismatch for {format_hash(hash_value)}")
        if int(item.get("offset", -1)) != record.offset:
            raise ValueError(f"Manifest offset mismatch for {format_hash(hash_value)}")
        if int(item.get("compressed_length", -1)) != record.length:
            raise ValueError(f"Manifest compressed length mismatch for {format_hash(hash_value)}")

        payload = extract_payload(blob, record)
        if payload != entry.payload:
            raise ValueError(f"Manifest payload mismatch against source for {format_hash(hash_value)}")
        if int(item.get("decompressed_length", -1)) != len(payload):
            raise ValueError(f"Manifest decompressed length mismatch for {format_hash(hash_value)}")

        payload_hash = hashlib.sha256(payload).hexdigest().upper()
        if item.get("sha256") != payload_hash:
            raise ValueError(f"Manifest SHA-256 mismatch for {format_hash(hash_value)}")

    if len(seen_hashes) != len(records):
        raise ValueError("Manifest did not cover every blob record.")


def print_record_list(records: list[LoreRecord]) -> None:
    for record in records:
        print(f"{format_hash(record.hash_value)} offset={record.offset} length={record.length}")


def build_arg_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Bake or verify HECTON-8 lore encyclopedia h8bin.")
    parser.add_argument("hash", nargs="?", help="Hash to extract, e.g. 0x1234ABCD.")
    parser.add_argument("--blob", default=str(DEFAULT_BLOB_RELATIVE), help="Path to Encyclopedia.h8bin.")
    parser.add_argument("--manifest", default=str(DEFAULT_MANIFEST_RELATIVE), help="Path to write the source manifest during bake.")
    parser.add_argument("--source-dir", default=str(PRIMARY_SOURCE_DIR_RELATIVE), help="Primary Markdown source directory.")
    parser.add_argument("--bake", action="store_true", help="Bake Markdown sources into the blob path.")
    parser.add_argument("--list", action="store_true", help="List hash records in the blob.")
    parser.add_argument("--check", action="store_true", help="Run source and manifest verification in one packaging check.")
    parser.add_argument("--verify-source", action="store_true", help="Compare every source file with extracted blob payloads.")
    parser.add_argument("--verify-manifest", action="store_true", help="Validate the manifest sidecar against source files and the blob.")
    parser.add_argument("--hash-source", action="store_true", help="Print source path hashes without reading the blob.")
    parser.add_argument("--source-path", help="Extract by repository-relative Markdown source path instead of numeric hash.")
    parser.add_argument("--no-manifest", action="store_true", help="Do not write the manifest sidecar during bake.")
    parser.add_argument("--output-text", help="Write extracted Markdown to this file instead of stdout.")
    return parser


def main(argv: list[str]) -> int:
    parser = build_arg_parser()
    args = parser.parse_args(argv)

    try:
        return run_command(args, parser)
    except ValueError as exc:
        parser.error(str(exc))
    return 2


def run_command(args: argparse.Namespace, parser: argparse.ArgumentParser) -> int:
    blob_path = resolve_repo_path(Path(args.blob))
    manifest_path = resolve_repo_path(Path(args.manifest))
    source_dir = resolve_repo_path(Path(args.source_dir))

    if args.hash and args.source_path:
        parser.error("Use either a numeric hash or --source-path, not both.")

    if args.hash_source:
        entries = load_source_entries(source_dir)
        for entry in sorted(entries, key=lambda item: item.hash_value):
            print(f"{format_hash(entry.hash_value)} {entry.canonical_id}")
        return 0

    if args.bake:
        entries = load_source_entries(source_dir)
        blob = bake_blob(entries)
        atomic_write_bytes(blob_path, blob)
        print(f"LORE BAKED: entries={len(entries)} bytes={len(blob)} output={format_repo_path(blob_path)}")
        for entry in sorted(entries, key=lambda item: item.hash_value):
            print(f"{format_hash(entry.hash_value)} {entry.canonical_id}")
        if not args.no_manifest:
            write_manifest(blob_path, manifest_path, source_dir)
            print(f"MANIFEST WRITTEN: {format_repo_path(manifest_path)}")

    if args.verify_source:
        verify_against_sources(blob_path, source_dir)
        print(f"VERIFY OK: {format_repo_path(blob_path)}")

    if args.verify_manifest:
        verify_manifest(blob_path, manifest_path, source_dir)
        print(f"MANIFEST VERIFY OK: {format_repo_path(manifest_path)}")

    if args.check:
        verify_against_sources(blob_path, source_dir)
        verify_manifest(blob_path, manifest_path, source_dir)
        _, records = read_blob(blob_path)
        print(
            f"CHECK OK: entries={len(records)} "
            f"blob={format_repo_path(blob_path)} manifest={format_repo_path(manifest_path)}"
        )

    if args.list:
        _, records = read_blob(blob_path)
        print_record_list(records)

    if args.hash or args.source_path:
        if args.source_path:
            hash_value = compute_fnv1a32(canonicalize_path(Path(args.source_path)))
        else:
            hash_value = parse_hash(args.hash)
        blob, records = read_blob(blob_path)
        record = find_record(records, hash_value)
        if record is None:
            raise ValueError(f"Hash not found: {format_hash(hash_value)}")
        payload = extract_payload(blob, record)
        if args.output_text:
            atomic_write_bytes(Path(args.output_text), payload)
        else:
            sys.stdout.buffer.write(payload)
            if not payload.endswith(b"\n"):
                sys.stdout.buffer.write(b"\n")

    if not (
        args.bake
        or args.verify_source
        or args.verify_manifest
        or args.check
        or args.list
        or args.hash
        or args.source_path
        or args.hash_source
    ):
        parser.print_help()

    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
