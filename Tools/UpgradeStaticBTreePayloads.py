#!/usr/bin/env python3
"""Insert 64-byte cache-conscious B-Trees into current Core/Data baked payloads."""

from __future__ import annotations

import argparse
import hashlib
import json
import struct
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parent.parent
STATIC_PATH = REPO_ROOT / "Data" / "Balance" / "Baked" / "H8StaticData.bin"
BABEL_PATH = REPO_ROOT / "Data" / "Balance" / "Baked" / "Babel_Dictionary.h8bin"
STATIC_MANIFEST_PATH = STATIC_PATH.with_suffix(".manifest.json")
BABEL_MANIFEST_PATH = BABEL_PATH.with_suffix(".manifest.json")

STATIC_MAGIC = 0x44533848
BABEL_MAGIC = 0x42413848
LITTLE_ENDIAN_FLAG = 1
CACHE_BTREE_FLAG = 1 << 8
CACHE_LINE_BYTES = 64
STATIC_RECORD_ALIGNMENT = 64
BTREE_KEY_CAPACITY = 7
BTREE_CHILD_CAPACITY = 8
BTREE_LEAF_FLAG = 1 << 8

STATIC_HEADER = struct.Struct("<IHHHH13I")
BABEL_HEADER = struct.Struct("<IHH6I")
STATIC_LOOKUP = struct.Struct("<IHHq")
BABEL_INDEX = struct.Struct("<IIII")
BTREE_NODE = struct.Struct("<16I")


def align_up(value: int, alignment: int) -> int:
    return (value + alignment - 1) & ~(alignment - 1)


def crc32_hecton(data: bytes) -> int:
    crc = 0xFFFFFFFF
    for byte in data:
        crc ^= byte
        for _ in range(8):
            mask = 0 - (crc & 1)
            crc = ((crc >> 1) ^ (0xEDB88320 & mask)) & 0xFFFFFFFF
    return (~crc) & 0xFFFFFFFF


def atomic_write_bytes(path: Path, data: bytes) -> None:
    temp_path = path.with_name(path.name + ".tmp")
    if path.exists() and path.stat().st_size == len(data) and path.read_bytes() == data:
        return

    temp_path.write_bytes(data)
    if path.exists() and path.stat().st_size == len(data) and path.read_bytes() == data:
        return

    temp_path.replace(path)


def write_json(path: Path, data: dict) -> None:
    atomic_write_bytes(path, (json.dumps(data, indent=2, sort_keys=True) + "\n").encode("utf-8"))


def update_static_manifest(path: Path, blob: bytes, header: tuple[int, ...]) -> None:
    manifest = json.loads(path.read_text(encoding="utf-8"))
    manifest["binaryBytes"] = len(blob)
    manifest["sha256"] = hashlib.sha256(blob).hexdigest().upper()
    manifest["payloadCrc32"] = f"0x{header[6]:08X}"
    manifest["babelCrc32"] = f"0x{header[12]:08X}"
    manifest["flags"] = f"0x{header[13]:X}"
    manifest["cacheBTree"] = {
        "enabled": True,
        "treeOffset": align_up(header[9] + (header[7] * STATIC_LOOKUP.size), CACHE_LINE_BYTES),
        "treeEndOffset": header[10],
        "treeBytes": header[10] - align_up(header[9] + (header[7] * STATIC_LOOKUP.size), CACHE_LINE_BYTES),
        "nodeBytes": CACHE_LINE_BYTES,
    }
    manifest["recordAlignmentBytes"] = STATIC_RECORD_ALIGNMENT
    manifest["recordLayout"] = "lookup table rows stay 16-byte records; payload records keep 48-byte DTO ABI but each payload offset is 64-byte aligned after the B-Tree section"
    manifest["status"] = "BALANCE_STATIC_BTREE_VERIFIED_PENDING_UNITY_PROOF"
    write_json(path, manifest)


def update_babel_manifest(path: Path, blob: bytes, header: tuple[int, ...]) -> None:
    manifest = json.loads(path.read_text(encoding="utf-8"))
    manifest["binaryBytes"] = len(blob)
    manifest["sha256"] = hashlib.sha256(blob).hexdigest().upper()
    manifest["payloadCrc32"] = f"0x{header[7]:08X}"
    manifest["flags"] = f"0x{header[8]:X}"
    manifest["cacheBTree"] = {
        "enabled": True,
        "treeOffset": align_up(header[4] + (header[3] * BABEL_INDEX.size), CACHE_LINE_BYTES),
        "treeEndOffset": header[5],
        "treeBytes": header[5] - align_up(header[4] + (header[3] * BABEL_INDEX.size), CACHE_LINE_BYTES),
        "nodeBytes": CACHE_LINE_BYTES,
    }
    manifest["status"] = "BALANCE_BABEL_BTREE_VERIFIED_PENDING_UNITY_PROOF"
    write_json(path, manifest)


def make_leaf_meta(key_count: int) -> int:
    return BTREE_LEAF_FLAG | (key_count & 0x7)


def make_internal_meta(key_count: int) -> int:
    return key_count & 0x7


def pack_node(keys: list[int], children: list[int], meta: int) -> bytes:
    lanes = [0] * 16
    for index, value in enumerate(keys[:BTREE_KEY_CAPACITY]):
        lanes[index] = value & 0xFFFFFFFF
    for index, value in enumerate(children[:BTREE_CHILD_CAPACITY]):
        lanes[7 + index] = value & 0xFFFFFFFF
    lanes[15] = meta & 0xFFFFFFFF
    return BTREE_NODE.pack(*lanes)


def build_btree(records: list[tuple[int, int]], tree_offset: int) -> bytes:
    if tree_offset & (CACHE_LINE_BYTES - 1):
        raise ValueError("B-Tree offset must be 64-byte aligned")

    sorted_records = sorted(records, key=lambda row: row[0])
    if not sorted_records:
        return pack_node([], [], make_leaf_meta(0))

    nodes: list[bytes] = []
    level: list[tuple[int, int]] = []
    for start in range(0, len(sorted_records), BTREE_KEY_CAPACITY):
        chunk = sorted_records[start:start + BTREE_KEY_CAPACITY]
        node_index = len(nodes)
        nodes.append(
            pack_node(
                [row[0] for row in chunk],
                [row[1] for row in chunk],
                make_leaf_meta(len(chunk)),
            )
        )
        level.append((node_index, chunk[-1][0]))

    while len(level) > 1:
        next_level: list[tuple[int, int]] = []
        for start in range(0, len(level), BTREE_CHILD_CAPACITY):
            chunk = level[start:start + BTREE_CHILD_CAPACITY]
            node_index = len(nodes)
            children = [tree_offset + (child_index * CACHE_LINE_BYTES) for child_index, _ in chunk]
            keys = [max_key for _, max_key in chunk[:-1]]
            nodes.append(pack_node(keys, children, make_internal_meta(len(chunk) - 1)))
            next_level.append((node_index, chunk[-1][1]))
        level = next_level

    return b"".join(nodes)


def find_btree_value(blob: bytes, tree_offset: int, tree_end: int, target_hash: int) -> int | None:
    if tree_end <= tree_offset or (tree_offset | tree_end) & (CACHE_LINE_BYTES - 1):
        return None

    current = tree_end - CACHE_LINE_BYTES
    for _ in range(32):
        if current < tree_offset or current + CACHE_LINE_BYTES > tree_end or current & (CACHE_LINE_BYTES - 1):
            return None

        lanes = BTREE_NODE.unpack_from(blob, current)
        keys = lanes[:BTREE_KEY_CAPACITY]
        children = lanes[7:15]
        meta = lanes[15]
        key_count = meta & 0x7
        is_leaf = (meta & BTREE_LEAF_FLAG) != 0
        if key_count > BTREE_KEY_CAPACITY:
            return None

        if is_leaf:
            for index in range(key_count):
                if keys[index] == target_hash:
                    return children[index]
            return None

        child_index = key_count
        for index in range(key_count):
            if target_hash <= keys[index]:
                child_index = index
                break
        current = children[child_index]

    return None


def validate_static_btree(blob: bytes, header: tuple[int, ...]) -> None:
    lookup_count = header[7]
    lookup_offset = header[9]
    tree_offset = align_up(lookup_offset + (lookup_count * STATIC_LOOKUP.size), CACHE_LINE_BYTES)
    tree_end = header[10]
    for index in range(lookup_count):
        hash_value, _record_type, byte_size, record_offset = STATIC_LOOKUP.unpack_from(blob, lookup_offset + (index * STATIC_LOOKUP.size))
        if record_offset & (STATIC_RECORD_ALIGNMENT - 1):
            raise ValueError(f"Static record offset is not 64-byte aligned at index {index}: {record_offset}")
        if record_offset < tree_end or record_offset + byte_size > len(blob):
            raise ValueError(f"Static record offset out of range at index {index}: {record_offset}")
        resolved = find_btree_value(blob, tree_offset, tree_end, hash_value)
        if resolved != index:
            raise ValueError(f"Static B-Tree lookup mismatch at index {index}: {resolved}")


def validate_babel_btree(blob: bytes, header: tuple[int, ...]) -> None:
    entry_count = header[3]
    index_offset = header[4]
    tree_offset = align_up(index_offset + (entry_count * BABEL_INDEX.size), CACHE_LINE_BYTES)
    tree_end = header[5]
    for index in range(entry_count):
        hash_value = BABEL_INDEX.unpack_from(blob, index_offset + (index * BABEL_INDEX.size))[0]
        resolved = find_btree_value(blob, tree_offset, tree_end, hash_value)
        if resolved != index:
            raise ValueError(f"Babel B-Tree lookup mismatch at index {index}: {resolved}")


def validate_lookup_sorted(rows: list[tuple[int, int]]) -> None:
    previous = -1
    for hash_value, _ in rows:
        if hash_value <= previous:
            raise ValueError("Lookup hashes must be strictly ascending")
        previous = hash_value


def patch_static(path: Path, babel_crc32: int | None = None) -> bool:
    blob = path.read_bytes()
    if len(blob) < STATIC_HEADER.size:
        raise ValueError("H8StaticData.bin too small")

    header = list(STATIC_HEADER.unpack_from(blob, 0))
    if header[0] != STATIC_MAGIC:
        raise ValueError("Bad static data magic")

    flags = header[13]
    lookup_count = header[7]
    lookup_offset = header[9]
    records_offset = header[10]
    record_bytes = header[11]
    if lookup_offset + (lookup_count * STATIC_LOOKUP.size) > records_offset:
        raise ValueError("Static lookup table overlaps record bytes")
    if records_offset + record_bytes != len(blob):
        raise ValueError("Static record byte range does not match file length")

    table_end = lookup_offset + (lookup_count * STATIC_LOOKUP.size)
    tree_offset = align_up(table_end, CACHE_LINE_BYTES)
    existing_records_aligned = True
    rows: list[tuple[int, int]] = []
    lookup_rows: list[tuple[int, int, int, int]] = []

    for index in range(lookup_count):
        hash_value, record_type, byte_size, old_offset = STATIC_LOOKUP.unpack_from(blob, lookup_offset + (index * STATIC_LOOKUP.size))
        if old_offset < records_offset or old_offset + byte_size > len(blob):
            raise ValueError(f"Static record offset out of range at index {index}: {old_offset}")
        if old_offset & (STATIC_RECORD_ALIGNMENT - 1):
            existing_records_aligned = False
        rows.append((hash_value, index))
        lookup_rows.append((hash_value, record_type, byte_size, old_offset))

    if flags & CACHE_BTREE_FLAG and existing_records_aligned:
        if babel_crc32 is not None and header[12] != babel_crc32:
            patched = bytearray(blob)
            header[12] = babel_crc32
            STATIC_HEADER.pack_into(patched, 0, *header)
            atomic_write_bytes(path, bytes(patched))
            update_static_manifest(STATIC_MANIFEST_PATH, bytes(patched), tuple(header))
            print(f"STATIC BABEL CRC SYNCED: {path} babelCrc=0x{babel_crc32:08X}")
            return True

        update_static_manifest(STATIC_MANIFEST_PATH, blob, tuple(header))
        print(f"STATIC B-TREE PRESENT: {path}")
        return False

    btree = build_btree(rows, tree_offset)
    new_records_offset = align_up(tree_offset + len(btree), STATIC_RECORD_ALIGNMENT)
    patched_lookup = bytearray()
    record_payload = bytearray()
    for hash_value, record_type, byte_size, old_offset in lookup_rows:
        next_offset = new_records_offset + len(record_payload)
        aligned_offset = align_up(next_offset, STATIC_RECORD_ALIGNMENT)
        if aligned_offset > next_offset:
            record_payload.extend(b"\0" * (aligned_offset - next_offset))
        patched_lookup.extend(STATIC_LOOKUP.pack(hash_value, record_type, byte_size, aligned_offset))
        record_payload.extend(blob[old_offset:old_offset + byte_size])

    prefix = bytearray(blob[:STATIC_HEADER.size])
    prefix.extend(blob[STATIC_HEADER.size:lookup_offset])
    prefix.extend(patched_lookup)
    prefix.extend(b"\0" * (tree_offset - len(prefix)))
    prefix.extend(btree)
    prefix.extend(b"\0" * (new_records_offset - len(prefix)))
    prefix.extend(record_payload)

    header[5] = len(prefix)
    header[6] = crc32_hecton(bytes(prefix[STATIC_HEADER.size:]))
    header[10] = new_records_offset
    header[11] = len(prefix) - new_records_offset
    if babel_crc32 is not None:
        header[12] = babel_crc32
    header[13] = flags | CACHE_BTREE_FLAG | LITTLE_ENDIAN_FLAG
    STATIC_HEADER.pack_into(prefix, 0, *header)
    atomic_write_bytes(path, bytes(prefix))
    update_static_manifest(STATIC_MANIFEST_PATH, bytes(prefix), tuple(header))
    print(f"STATIC B-TREE PATCHED: {path} bytes={len(prefix)} treeOffset={tree_offset} treeBytes={len(btree)} recordsOffset={new_records_offset} recordAlignment={STATIC_RECORD_ALIGNMENT}")
    return True


def patch_babel(path: Path) -> int:
    blob = path.read_bytes()
    if len(blob) < BABEL_HEADER.size:
        raise ValueError("Babel dictionary too small")

    header = list(BABEL_HEADER.unpack_from(blob, 0))
    if header[0] != BABEL_MAGIC:
        raise ValueError("Bad Babel magic")

    flags = header[8]
    if flags & CACHE_BTREE_FLAG:
        update_babel_manifest(BABEL_MANIFEST_PATH, blob, tuple(header))
        print(f"BABEL B-TREE PRESENT: {path}")
        return header[7]

    entry_count = header[3]
    index_offset = header[4]
    data_offset = header[5]
    if index_offset + (entry_count * BABEL_INDEX.size) > data_offset:
        raise ValueError("Babel index overlaps text bytes")
    if header[6] != len(blob):
        raise ValueError("Babel file length header mismatch")

    rows: list[tuple[int, int]] = []
    table_end = index_offset + (entry_count * BABEL_INDEX.size)
    tree_offset = align_up(table_end, CACHE_LINE_BYTES)
    btree = build_btree([(BABEL_INDEX.unpack_from(blob, index_offset + (i * BABEL_INDEX.size))[0], i) for i in range(entry_count)], tree_offset)
    new_data_offset = align_up(tree_offset + len(btree), 16)
    delta = new_data_offset - data_offset

    patched_index = bytearray()
    for index in range(entry_count):
        hash_value, old_offset, byte_length, pad = BABEL_INDEX.unpack_from(blob, index_offset + (index * BABEL_INDEX.size))
        rows.append((hash_value, index))
        patched_index.extend(BABEL_INDEX.pack(hash_value, old_offset + delta, byte_length, pad))

    validate_lookup_sorted(rows)
    prefix = bytearray(blob[:BABEL_HEADER.size])
    prefix.extend(blob[BABEL_HEADER.size:index_offset])
    prefix.extend(patched_index)
    prefix.extend(b"\0" * (tree_offset - len(prefix)))
    prefix.extend(btree)
    prefix.extend(b"\0" * (new_data_offset - len(prefix)))
    prefix.extend(blob[data_offset:])

    header[5] = new_data_offset
    header[6] = len(prefix)
    header[7] = crc32_hecton(bytes(prefix[BABEL_HEADER.size:]))
    header[8] = flags | CACHE_BTREE_FLAG | LITTLE_ENDIAN_FLAG
    BABEL_HEADER.pack_into(prefix, 0, *header)
    atomic_write_bytes(path, bytes(prefix))
    update_babel_manifest(BABEL_MANIFEST_PATH, bytes(prefix), tuple(header))
    print(f"BABEL B-TREE PATCHED: {path} bytes={len(prefix)} treeOffset={tree_offset} treeBytes={len(btree)} dataOffset={new_data_offset}")
    return header[7]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="Validate after patching.")
    args = parser.parse_args()

    babel_crc32 = patch_babel(BABEL_PATH)
    patch_static(STATIC_PATH, babel_crc32)

    if args.check:
        static_blob = STATIC_PATH.read_bytes()
        static_header = STATIC_HEADER.unpack_from(static_blob, 0)
        if (static_header[13] & CACHE_BTREE_FLAG) == 0:
            raise ValueError("Static payload still lacks CacheBTreeFlag")
        if static_header[12] != babel_crc32:
            raise ValueError("Static payload Babel CRC does not match patched Babel dictionary")
        if crc32_hecton(static_blob[STATIC_HEADER.size:]) != static_header[6]:
            raise ValueError("Static payload CRC mismatch")
        validate_static_btree(static_blob, static_header)

        babel_blob = BABEL_PATH.read_bytes()
        babel_header = BABEL_HEADER.unpack_from(babel_blob, 0)
        if (babel_header[8] & CACHE_BTREE_FLAG) == 0:
            raise ValueError("Babel payload still lacks CacheBTreeFlag")
        if crc32_hecton(babel_blob[BABEL_HEADER.size:]) != babel_header[7]:
            raise ValueError("Babel payload CRC mismatch")
        validate_babel_btree(babel_blob, babel_header)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
