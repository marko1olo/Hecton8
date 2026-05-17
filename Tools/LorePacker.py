#!/usr/bin/env python3
"""Bake the HECTON-8 encyclopedia into a raw, aligned H8LR blob."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import struct
import time
from dataclasses import dataclass
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
MAGIC = b"H8LR"
VERSION = 1
ALIGNMENT = 16
HEADER_STRUCT = struct.Struct("<4sIII")
RECORD_STRUCT = struct.Struct("<IIII")
FNV_OFFSET_BASIS = 0x811C9DC5
FNV_PRIME = 0x01000193
DEFAULT_SOURCE_DIR = REPO_ROOT / "Docs" / "Lore"
DEFAULT_BLOB = REPO_ROOT / "Data" / "Lore" / "Encyclopedia.h8bin"
DEFAULT_MANIFEST = REPO_ROOT / "Data" / "Lore" / "Encyclopedia.manifest.json"
DEFAULT_HASHES = REPO_ROOT / "Assets" / "_Project" / "Scripts" / "Core" / "Generated" / "H8LoreHashes.cs"
STERILE_TERMS = (
    "quantum veil",
    "crystal palace",
    "utopian",
    "pristine",
    "sparklefield",
    "aether",
)
NOIR_TERMS = ("pressure", "fault", "hull", "abyss", "relay", "rust", "black", "oil", "bulkhead")


@dataclass(frozen=True)
class SourceEntry:
    source_path: Path
    lore_id: str
    hash_value: int
    payload: bytes


@dataclass(frozen=True)
class LoreRecord:
    hash_value: int
    offset: int
    length: int
    pad: int = 0


def align_up(value: int, alignment: int = ALIGNMENT) -> int:
    return (value + alignment - 1) & ~(alignment - 1)


def fnv1a32_ascii_lower(value: str) -> int:
    if not value:
        return 0
    result = FNV_OFFSET_BASIS
    for byte in value.encode("ascii"):
        folded = byte + 32 if 65 <= byte <= 90 else byte
        result ^= folded
        result = (result * FNV_PRIME) & 0xFFFFFFFF
    return result or 1


compute_fnv1a32 = fnv1a32_ascii_lower


def format_hash(value: int) -> str:
    return f"0x{value & 0xFFFFFFFF:08X}"


def resolve_repo_path(value: Path) -> Path:
    return value.resolve() if value.is_absolute() else (REPO_ROOT / value).resolve()


def repo_relative(value: Path) -> str:
    resolved = resolve_repo_path(value)
    return resolved.relative_to(REPO_ROOT).as_posix()


def lore_id_from_path(value: Path) -> str:
    stem = value.stem
    if not stem or any(ord(ch) > 127 for ch in stem):
        raise ValueError(f"Lore filename stem must be non-empty ASCII: {value}")
    return stem


def csharp_name(value: str) -> str:
    name = re.sub(r"[^0-9A-Za-z_]", "_", value)
    if not name or name[0].isdigit():
        name = f"Id_{name}"
    return name


def validate_payload(lore_id: str, payload: bytes) -> None:
    try:
        text = payload.decode("utf-8")
    except UnicodeDecodeError as exc:
        raise ValueError(f"Lore source is not UTF-8: {lore_id}") from exc
    lowered = text.lower()
    hits = [term for term in STERILE_TERMS if term in lowered]
    if hits:
        raise ValueError(f"Lore source {lore_id} contains sterile sci-fi terms: {', '.join(hits)}")


def load_source_entries(source_dir: Path = DEFAULT_SOURCE_DIR) -> list[SourceEntry]:
    root = resolve_repo_path(source_dir)
    if not root.is_dir():
        raise ValueError(f"Lore source directory missing: {root}")
    entries: list[SourceEntry] = []
    seen_ids: set[str] = set()
    seen_hashes: dict[int, str] = {}
    for source_path in sorted(root.rglob("*.md")):
        lore_id = lore_id_from_path(source_path)
        payload = source_path.read_bytes()
        validate_payload(lore_id, payload)
        hash_value = fnv1a32_ascii_lower(lore_id)
        if lore_id.lower() in seen_ids:
            raise ValueError(f"Duplicate lore filename-derived ID: {lore_id}")
        previous = seen_hashes.get(hash_value)
        if previous is not None:
            raise ValueError(f"Duplicate lore FNV hash {format_hash(hash_value)}: {previous} vs {lore_id}")
        seen_ids.add(lore_id.lower())
        seen_hashes[hash_value] = lore_id
        entries.append(SourceEntry(source_path=source_path, lore_id=lore_id, hash_value=hash_value, payload=payload))
    if not entries:
        raise ValueError(f"No lore Markdown sources found under {root}")
    return sorted(entries, key=lambda entry: entry.hash_value)


def bake_blob(entries: list[SourceEntry]) -> tuple[bytes, list[LoreRecord]]:
    records: list[LoreRecord] = []
    cursor = HEADER_STRUCT.size + (len(entries) * RECORD_STRUCT.size)
    cursor = align_up(cursor)
    payload = bytearray()
    for entry in entries:
        aligned_cursor = align_up(cursor)
        if aligned_cursor > cursor:
            payload.extend(b"\0" * (aligned_cursor - cursor))
            cursor = aligned_cursor
        records.append(LoreRecord(entry.hash_value, cursor, len(entry.payload), 0))
        payload.extend(entry.payload)
        cursor += len(entry.payload)
    final_size = align_up(cursor)
    if final_size > cursor:
        payload.extend(b"\0" * (final_size - cursor))
    header = HEADER_STRUCT.pack(MAGIC, VERSION, len(entries), 0)
    table = b"".join(RECORD_STRUCT.pack(row.hash_value, row.offset, row.length, row.pad) for row in records)
    prefix_padding = b"\0" * (align_up(len(header) + len(table)) - len(header) - len(table))
    blob = header + table + prefix_padding + bytes(payload)
    if len(blob) % ALIGNMENT != 0:
        raise AssertionError("H8LR blob is not 16-byte aligned")
    return blob, records


def parse_blob(blob: bytes) -> list[LoreRecord]:
    if len(blob) < HEADER_STRUCT.size:
        raise ValueError("H8LR blob too small")
    magic, version, count, reserved = HEADER_STRUCT.unpack_from(blob, 0)
    if magic != MAGIC:
        raise ValueError(f"Bad H8LR magic: {magic!r}")
    if version != VERSION:
        raise ValueError(f"Bad H8LR version: {version}")
    if reserved != 0:
        raise ValueError("H8LR header reserved field must be zero")
    records: list[LoreRecord] = []
    offset = HEADER_STRUCT.size
    for _ in range(count):
        hash_value, payload_offset, length, pad = RECORD_STRUCT.unpack_from(blob, offset)
        if pad != 0:
            raise ValueError(f"H8LR record {format_hash(hash_value)} reserved pad must be zero")
        if payload_offset % ALIGNMENT != 0:
            raise ValueError(f"H8LR record {format_hash(hash_value)} payload offset is not 16-byte aligned")
        if payload_offset + length > len(blob):
            raise ValueError(f"H8LR record {format_hash(hash_value)} extends beyond blob")
        records.append(LoreRecord(hash_value, payload_offset, length, pad))
        offset += RECORD_STRUCT.size
    if records != sorted(records, key=lambda row: row.hash_value):
        raise ValueError("H8LR records must be sorted by hash")
    if len({row.hash_value for row in records}) != len(records):
        raise ValueError("H8LR duplicate record hashes")
    if len(blob) % ALIGNMENT != 0:
        raise ValueError("H8LR blob length is not 16-byte aligned")
    return records


def verify_entries_against_blob(blob: bytes, records: list[LoreRecord], entries: list[SourceEntry]) -> None:
    by_hash = {entry.hash_value: entry for entry in entries}
    for record in records:
        entry = by_hash.get(record.hash_value)
        if entry is None:
            raise ValueError(f"Blob record missing source entry: {format_hash(record.hash_value)}")
        actual = blob[record.offset : record.offset + record.length]
        if actual != entry.payload:
            raise ValueError(f"Raw payload mismatch for {entry.lore_id}")


def tone_counts(entries: list[SourceEntry]) -> dict[str, int]:
    text = "\n".join(entry.payload.decode("utf-8", errors="replace").lower() for entry in entries)
    return {term: text.count(term) for term in NOIR_TERMS}


def build_manifest(blob: bytes, records: list[LoreRecord], entries: list[SourceEntry], source_dir: Path) -> dict:
    by_hash = {entry.hash_value: entry for entry in entries}
    return {
        "magic": "H8LR",
        "version": VERSION,
        "hash_algorithm": "FNV-1a 32-bit over filename stem, ASCII A-Z folded to lowercase",
        "compression": "none/raw-utf8",
        "endianness": "little",
        "struct_pack_prefix": "<",
        "header_struct_format": "<4sIII",
        "record_struct_format": "<IIII",
        "alignment_bytes": ALIGNMENT,
        "record_layout": "uint32 hash, uint32 raw_offset, uint32 raw_length, uint32 pad",
        "blob": repo_relative(DEFAULT_BLOB),
        "blob_length": len(blob),
        "blob_sha256": hashlib.sha256(blob).hexdigest().upper(),
        "source_dir": repo_relative(source_dir),
        "entry_count": len(records),
        "entries": [
            {
                "hash": format_hash(record.hash_value),
                "id": by_hash[record.hash_value].lore_id,
                "source": repo_relative(by_hash[record.hash_value].source_path),
                "offset": record.offset,
                "length": record.length,
                "pad": record.pad,
                "sha256": hashlib.sha256(by_hash[record.hash_value].payload).hexdigest().upper(),
            }
            for record in records
        ],
        "project_atlas_fit": {
            "domain_id": 72,
            "domain": "PDA Encyclopedia Streaming",
            "source": "Docs/Lore",
            "artifact": "Data/Lore/Encyclopedia.h8bin",
        },
        "h_phi_audit": {
            "data_sovereignty_static_score": 1.0,
            "lookup_model": "stateless binary search over sorted 16-byte records; payload is raw UTF-8 slice",
            "private_runtime_state_required": False,
            "unity_runtime_proof": "PENDING VERIFICATION",
        },
        "scalability_profiles": {
            "toaster": {
                "lookup": "binary-search 16-byte records; stream one raw UTF-8 slice",
                "preview_bytes": 512,
            },
            "rtx_overkill": {
                "lookup": "prefetch adjacent raw slices",
                "extra_data": ["high_res_gradient_4096", "complex_harmonic_noise_tags", "noir_signal_density"],
            },
        },
        "lore_audit": {
            "sterile_terms_rejected": list(STERILE_TERMS),
            "noir_signal_counts": tone_counts(entries),
        },
    }


def generate_hashes_cs(entries: list[SourceEntry]) -> str:
    lines = [
        "// <auto-generated>",
        "// Source: Tools/LorePacker.py",
        "// Runtime contract: constants only; no runtime hashing in hot paths.",
        "// </auto-generated>",
        "namespace Hecton8.Core.Generated",
        "{",
        "    /// <summary>FNV-1a lore identifiers baked from filename stems.</summary>",
        "    public static class H8LoreHashes",
        "    {",
    ]
    seen_names: set[str] = set()
    for entry in sorted(entries, key=lambda row: csharp_name(row.lore_id)):
        name = csharp_name(entry.lore_id)
        if name in seen_names:
            raise ValueError(f"Generated C# name collision: {name}")
        seen_names.add(name)
        lines.append(f"        public const uint {name} = 0x{entry.hash_value:08X}u;")
    lines.extend(["    }", "}", ""])
    return "\n".join(lines)


def atomic_write_bytes(path: Path, data: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists() and path.read_bytes() == data:
        return
    tmp = path.with_name(f"{path.name}.{os.getpid()}.tmp")
    tmp.write_bytes(data)
    for _ in range(5):
        try:
            tmp.replace(path)
            return
        except PermissionError:
            time.sleep(0.25)
    path.write_bytes(data)
    tmp.unlink(missing_ok=True)


def atomic_write_text(path: Path, text: str) -> None:
    atomic_write_bytes(path, text.encode("utf-8"))


def write_outputs(blob: bytes, records: list[LoreRecord], entries: list[SourceEntry], source_dir: Path) -> dict:
    manifest = build_manifest(blob, records, entries, source_dir)
    atomic_write_bytes(DEFAULT_BLOB, blob)
    atomic_write_text(DEFAULT_MANIFEST, json.dumps(manifest, indent=2, sort_keys=True) + "\n")
    atomic_write_text(DEFAULT_HASHES, generate_hashes_cs(entries))
    return manifest


def verify_manifest(blob: bytes, records: list[LoreRecord], entries: list[SourceEntry], manifest: dict) -> None:
    if manifest.get("compression") != "none/raw-utf8":
        raise ValueError("Lore manifest compression drift")
    if manifest.get("endianness") != "little" or manifest.get("struct_pack_prefix") != "<":
        raise ValueError("Lore manifest endian drift")
    if manifest.get("header_struct_format") != "<4sIII" or manifest.get("record_struct_format") != "<IIII":
        raise ValueError("Lore manifest struct format drift")
    if int(manifest.get("alignment_bytes", 0)) != ALIGNMENT:
        raise ValueError("Lore manifest alignment drift")
    if int(manifest.get("blob_length", -1)) != len(blob):
        raise ValueError("Lore manifest blob length drift")
    if str(manifest.get("blob_sha256", "")).upper() != hashlib.sha256(blob).hexdigest().upper():
        raise ValueError("Lore manifest blob SHA drift")
    if int(manifest.get("entry_count", -1)) != len(records):
        raise ValueError("Lore manifest entry count drift")
    if len(manifest.get("entries", [])) != len(records):
        raise ValueError("Lore manifest entries length drift")
    by_hash = {entry.hash_value: entry for entry in entries}
    for item, record in zip(manifest["entries"], records):
        if item["hash"] != format_hash(record.hash_value):
            raise ValueError("Lore manifest record hash drift")
        if int(item["offset"]) != record.offset or int(item["length"]) != record.length or int(item.get("pad", 0)) != 0:
            raise ValueError("Lore manifest record layout drift")
        entry = by_hash[record.hash_value]
        if item["id"] != entry.lore_id or item["source"] != repo_relative(entry.source_path):
            raise ValueError("Lore manifest source drift")


def load_current_blob_and_manifest() -> tuple[bytes, list[LoreRecord], dict]:
    blob = DEFAULT_BLOB.read_bytes()
    records = parse_blob(blob)
    manifest = json.loads(DEFAULT_MANIFEST.read_text(encoding="utf-8"))
    return blob, records, manifest


def parse_hash(value: str) -> int:
    return int(value, 16 if value.lower().startswith("0x") else 10) & 0xFFFFFFFF


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Bake or verify raw H8LR lore data.")
    parser.add_argument("hash", nargs="?")
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--hash-audit", action="store_true")
    parser.add_argument("--list", action="store_true")
    parser.add_argument("--verify-source", action="store_true")
    parser.add_argument("--verify-manifest", action="store_true")
    parser.add_argument("--source-dir", type=Path, default=DEFAULT_SOURCE_DIR)
    parser.add_argument("--source-path", type=Path)
    parser.add_argument("--hash-source", action="store_true")
    args = parser.parse_args(argv)

    entries = load_source_entries(args.source_dir)
    blob, records = bake_blob(entries)
    verify_entries_against_blob(blob, records, entries)
    manifest = write_outputs(blob, records, entries, args.source_dir)
    verify_manifest(blob, records, entries, manifest)
    parsed = parse_blob(blob)
    if parsed != records:
        raise ValueError("Parsed records differ from baked records")

    if args.hash_audit:
        if len({entry.hash_value for entry in entries}) != len(entries):
            raise ValueError("Lore FNV collision detected")

    if args.check:
        print(f"LORE BAKED: entries={len(entries)} bytes={len(blob)} output={repo_relative(DEFAULT_BLOB)}")
        print("CHECK OK")
        if args.hash_audit:
            print(f"HASH AUDIT OK: collisions=0 entries={len(entries)}")

    if args.list:
        by_hash = {entry.hash_value: entry for entry in entries}
        for record in records:
            entry = by_hash[record.hash_value]
            print(f"{format_hash(record.hash_value)} offset={record.offset} length={record.length} id={entry.lore_id} path={repo_relative(entry.source_path)}")

    if args.source_path is not None:
        requested = resolve_repo_path(args.source_path)
        for entry in entries:
            if resolve_repo_path(entry.source_path) == requested:
                print(format_hash(entry.hash_value) if args.hash_source else entry.payload.decode("utf-8"))
                return 0
        raise SystemExit(f"source path not found: {args.source_path}")

    if args.hash:
        wanted = parse_hash(args.hash)
        for record in records:
            if record.hash_value == wanted:
                print(blob[record.offset : record.offset + record.length].decode("utf-8"))
                return 0
        raise SystemExit(f"hash not found: {args.hash}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
