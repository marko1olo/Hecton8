#!/usr/bin/env python3
"""HECTON-8 .h8db cold-file health audit.

Reports B-tree node alignment, payload padding, and append/dead-byte
fragmentation for H8 macro database files. This is an offline tool; it is not
part of Unity runtime hot paths.
"""

from __future__ import annotations

import argparse
import math
import mmap
import struct
from pathlib import Path


EXTENSION = ".h8db"
NODE_ALIGNMENT_BYTES = 4096
PAYLOAD_ALIGNMENT_BYTES = 16
NODE_SIZE_BYTES = 4096
HEADER_SIZE_BYTES = 4096
NODE_MAX_KEYS = 169
NODE_HEADER_SIZE_BYTES = 16
NODE_SECTOR_HASHES_OFFSET = NODE_HEADER_SIZE_BYTES
NODE_FILE_OFFSETS_OFFSET = NODE_SECTOR_HASHES_OFFSET + (NODE_MAX_KEYS * 8)
NODE_CHILD_OFFSETS_OFFSET = NODE_FILE_OFFSETS_OFFSET + (NODE_MAX_KEYS * 8)
NODE_COMPUTED_BYTES = NODE_CHILD_OFFSETS_OFFSET + ((NODE_MAX_KEYS + 1) * 8)
NODE_PADDING_BYTES = NODE_SIZE_BYTES - NODE_COMPUTED_BYTES
PAYLOAD_HEADER_SIZE_BYTES = 32

FILE_MAGIC = 0x42443848
PAYLOAD_MAGIC = 0x4C503848
VERSION = 1

HEADER_MAGIC_OFFSET = 0
HEADER_VERSION_OFFSET = 4
HEADER_SIZE_OFFSET = 8
HEADER_NODE_SIZE_OFFSET = 12
HEADER_ROOT_NODE_OFFSET = 16
HEADER_APPEND_OFFSET = 24
HEADER_SECTOR_SIZE_OFFSET = 32
HEADER_DEAD_BYTES_OFFSET = 40

NODE_KEY_COUNT_OFFSET = 0
NODE_IS_LEAF_OFFSET = 2
NODE_NEXT_LEAF_OFFSET = 8

PAYLOAD_MAGIC_OFFSET = 0
PAYLOAD_HASH_OFFSET = 8
PAYLOAD_BYTES_OFFSET = 16
PAYLOAD_VERSION_OFFSET = 20
PAYLOAD_FLAGS_OFFSET = 24

FNV_OFFSET_BASIS = 1469598103934665603
FNV_PRIME = 1099511628211
UINT64_MASK = (1 << 64) - 1


def align_up(value: int, alignment: int) -> int:
    if alignment <= 1:
        return value
    return (value + alignment - 1) & ~(alignment - 1)


def read_u16(view: mmap.mmap | bytes | bytearray, offset: int) -> int:
    return struct.unpack_from("<H", view, offset)[0]


def read_i32(view: mmap.mmap | bytes | bytearray, offset: int) -> int:
    return struct.unpack_from("<i", view, offset)[0]


def read_u32(view: mmap.mmap | bytes | bytearray, offset: int) -> int:
    return struct.unpack_from("<I", view, offset)[0]


def read_i64(view: mmap.mmap | bytes | bytearray, offset: int) -> int:
    return struct.unpack_from("<q", view, offset)[0]


def read_u64(view: mmap.mmap | bytes | bytearray, offset: int) -> int:
    return struct.unpack_from("<Q", view, offset)[0]


def write_u16(buffer: bytearray, offset: int, value: int) -> None:
    struct.pack_into("<H", buffer, offset, value)


def write_u32(buffer: bytearray, offset: int, value: int) -> None:
    struct.pack_into("<I", buffer, offset, value)


def write_i32(buffer: bytearray, offset: int, value: int) -> None:
    struct.pack_into("<i", buffer, offset, value)


def write_i64(buffer: bytearray, offset: int, value: int) -> None:
    struct.pack_into("<q", buffer, offset, value)


def write_u64(buffer: bytearray, offset: int, value: int) -> None:
    struct.pack_into("<Q", buffer, offset, value & UINT64_MASK)


def mix_hash(hash_value: int, value: int) -> int:
    hash_value ^= value & UINT64_MASK
    return (hash_value * FNV_PRIME) & UINT64_MASK


def compute_sector_hash(x: int, y: int, z: int, sector_size: int = 512) -> int:
    hash_value = FNV_OFFSET_BASIS
    hash_value = mix_hash(hash_value, x)
    hash_value = mix_hash(hash_value, y)
    hash_value = mix_hash(hash_value, z)
    hash_value = mix_hash(hash_value, sector_size)
    return 1 if hash_value == 0 else hash_value


def splitmix64(value: int) -> int:
    value = (value + 0x9E3779B97F4A7C15) & UINT64_MASK
    value = ((value ^ (value >> 30)) * 0xBF58476D1CE4E5B9) & UINT64_MASK
    value = ((value ^ (value >> 27)) * 0x94D049BB133111EB) & UINT64_MASK
    return (value ^ (value >> 31)) & UINT64_MASK


def node_hash_offset(index: int) -> int:
    return NODE_SECTOR_HASHES_OFFSET + (index * 8)


def node_file_offset(index: int) -> int:
    return NODE_FILE_OFFSETS_OFFSET + (index * 8)


def node_child_offset(index: int) -> int:
    return NODE_CHILD_OFFSETS_OFFSET + (index * 8)


def validate_header(view: mmap.mmap, file_bytes: int) -> tuple[int, int, int, int]:
    if file_bytes < HEADER_SIZE_BYTES + NODE_SIZE_BYTES:
        raise ValueError("file smaller than minimum .h8db header + root node")

    magic = read_u32(view, HEADER_MAGIC_OFFSET)
    version = read_i32(view, HEADER_VERSION_OFFSET)
    header_size = read_i32(view, HEADER_SIZE_OFFSET)
    node_size = read_i32(view, HEADER_NODE_SIZE_OFFSET)
    if magic != FILE_MAGIC:
        raise ValueError(f"bad file magic 0x{magic:08X}")
    if version != VERSION:
        raise ValueError(f"unsupported .h8db version {version}")
    if header_size != HEADER_SIZE_BYTES or node_size != NODE_SIZE_BYTES:
        raise ValueError("header or node size does not match runtime constants")

    root_offset = read_i64(view, HEADER_ROOT_NODE_OFFSET)
    append_offset = read_i64(view, HEADER_APPEND_OFFSET)
    sector_size = read_i32(view, HEADER_SECTOR_SIZE_OFFSET)
    dead_bytes = read_i64(view, HEADER_DEAD_BYTES_OFFSET)
    if root_offset < HEADER_SIZE_BYTES or root_offset > file_bytes - NODE_SIZE_BYTES:
        raise ValueError("root node offset outside file")
    if root_offset % NODE_ALIGNMENT_BYTES != 0:
        raise ValueError("root node offset is not 4096-byte aligned")
    if append_offset < HEADER_SIZE_BYTES + NODE_SIZE_BYTES or append_offset > file_bytes:
        raise ValueError("append offset outside file")
    if append_offset % PAYLOAD_ALIGNMENT_BYTES != 0:
        raise ValueError("append offset is not 16-byte aligned")
    if sector_size <= 0:
        raise ValueError("sector size must be positive")
    if dead_bytes < 0 or dead_bytes > append_offset:
        raise ValueError("dead-byte counter is corrupt")
    return root_offset, append_offset, sector_size, dead_bytes


def audit_h8db(path: Path) -> dict[str, int | float | str]:
    file_bytes = path.stat().st_size
    with path.open("rb") as handle:
        with mmap.mmap(handle.fileno(), 0, access=mmap.ACCESS_READ) as view:
            root_offset, append_offset, sector_size, dead_bytes = validate_header(view, file_bytes)
            visited_nodes: set[int] = set()
            live_payload_offsets: set[int] = set()
            node_stack = [root_offset]
            max_depth = 0
            key_total = 0
            unaligned_payloads = 0

            while node_stack:
                node_offset = node_stack.pop()
                depth = (node_offset >> 48) & 0xFFFF
                node_offset &= 0x0000FFFFFFFFFFFF
                max_depth = max(max_depth, depth)
                if node_offset in visited_nodes:
                    continue
                if node_offset % NODE_ALIGNMENT_BYTES != 0:
                    raise ValueError(f"node offset {node_offset} is not 4096-byte aligned")
                if node_offset < HEADER_SIZE_BYTES or node_offset > file_bytes - NODE_SIZE_BYTES:
                    raise ValueError(f"node offset {node_offset} outside file")

                visited_nodes.add(node_offset)
                key_count = read_u16(view, node_offset + NODE_KEY_COUNT_OFFSET)
                is_leaf = view[node_offset + NODE_IS_LEAF_OFFSET] != 0
                if key_count > NODE_MAX_KEYS:
                    raise ValueError(f"node {node_offset} has invalid key count {key_count}")

                previous_hash = 0
                for index in range(key_count):
                    sector_hash = read_u64(view, node_offset + node_hash_offset(index))
                    payload_offset = read_i64(view, node_offset + node_file_offset(index))
                    if index > 0 and sector_hash <= previous_hash:
                        raise ValueError(f"node {node_offset} key order break at index {index}")
                    previous_hash = sector_hash
                    if payload_offset % PAYLOAD_ALIGNMENT_BYTES != 0:
                        unaligned_payloads += 1
                    live_payload_offsets.add(payload_offset)
                key_total += key_count

                if not is_leaf:
                    for child_index in range(key_count, -1, -1):
                        child_offset = read_i64(view, node_offset + node_child_offset(child_index))
                        if child_offset > 0:
                            node_stack.append(child_offset | ((depth + 1) << 48))

            live_payload_record_bytes = 0
            payload_padding_bytes = 0
            live_payload_bytes = 0
            for payload_offset in sorted(live_payload_offsets):
                if payload_offset < HEADER_SIZE_BYTES or payload_offset > file_bytes - PAYLOAD_HEADER_SIZE_BYTES:
                    raise ValueError(f"payload offset {payload_offset} outside file")
                payload_magic = read_u32(view, payload_offset + PAYLOAD_MAGIC_OFFSET)
                if payload_magic != PAYLOAD_MAGIC:
                    raise ValueError(f"payload at {payload_offset} has bad magic 0x{payload_magic:08X}")
                payload_bytes = read_i32(view, payload_offset + PAYLOAD_BYTES_OFFSET)
                if payload_bytes <= 0 or payload_offset + PAYLOAD_HEADER_SIZE_BYTES + payload_bytes > file_bytes:
                    raise ValueError(f"payload at {payload_offset} has invalid byte length {payload_bytes}")
                record_bytes = align_up(PAYLOAD_HEADER_SIZE_BYTES + payload_bytes, PAYLOAD_ALIGNMENT_BYTES)
                live_payload_bytes += payload_bytes
                live_payload_record_bytes += record_bytes
                payload_padding_bytes += record_bytes - PAYLOAD_HEADER_SIZE_BYTES - payload_bytes

    fragmentation_percent = (dead_bytes / append_offset) * 100.0 if append_offset > 0 else 0.0
    mapped_slack_bytes = max(0, file_bytes - append_offset)
    node_padding_bytes = len(visited_nodes) * NODE_PADDING_BYTES
    return {
        "path": str(path),
        "file_bytes": file_bytes,
        "append_offset": append_offset,
        "mapped_slack_bytes": mapped_slack_bytes,
        "dead_bytes": dead_bytes,
        "fragmentation_percent": fragmentation_percent,
        "sector_size": sector_size,
        "nodes": len(visited_nodes),
        "max_depth": max_depth,
        "keys": key_total,
        "live_payloads": len(live_payload_offsets),
        "live_payload_bytes": live_payload_bytes,
        "live_payload_record_bytes": live_payload_record_bytes,
        "payload_padding_bytes": payload_padding_bytes,
        "node_padding_bytes": node_padding_bytes,
        "unaligned_payloads": unaligned_payloads,
    }


def create_dummy_h8db(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    live_hash = compute_sector_hash(12, -3, 44)
    old_hash = compute_sector_hash(11, -3, 44)
    live_payload = bytes((index * 37) & 0xFF for index in range(1536))
    dead_payload = bytes((index * 17) & 0xFF for index in range(3584))

    append = HEADER_SIZE_BYTES + NODE_SIZE_BYTES
    dead_offset = align_up(append, PAYLOAD_ALIGNMENT_BYTES)
    dead_record_bytes = align_up(PAYLOAD_HEADER_SIZE_BYTES + len(dead_payload), PAYLOAD_ALIGNMENT_BYTES)
    live_offset = dead_offset + dead_record_bytes
    live_record_bytes = align_up(PAYLOAD_HEADER_SIZE_BYTES + len(live_payload), PAYLOAD_ALIGNMENT_BYTES)
    append = live_offset + live_record_bytes
    file_bytes = align_up(append, NODE_ALIGNMENT_BYTES)
    buffer = bytearray(file_bytes)

    write_u32(buffer, HEADER_MAGIC_OFFSET, FILE_MAGIC)
    write_i32(buffer, HEADER_VERSION_OFFSET, VERSION)
    write_i32(buffer, HEADER_SIZE_OFFSET, HEADER_SIZE_BYTES)
    write_i32(buffer, HEADER_NODE_SIZE_OFFSET, NODE_SIZE_BYTES)
    write_i64(buffer, HEADER_ROOT_NODE_OFFSET, HEADER_SIZE_BYTES)
    write_i64(buffer, HEADER_APPEND_OFFSET, append)
    write_i32(buffer, HEADER_SECTOR_SIZE_OFFSET, 512)
    write_i64(buffer, HEADER_DEAD_BYTES_OFFSET, dead_record_bytes)

    root = HEADER_SIZE_BYTES
    write_u16(buffer, root + NODE_KEY_COUNT_OFFSET, 1)
    buffer[root + NODE_IS_LEAF_OFFSET] = 1
    write_i64(buffer, root + NODE_NEXT_LEAF_OFFSET, 0)
    write_u64(buffer, root + node_hash_offset(0), live_hash)
    write_i64(buffer, root + node_file_offset(0), live_offset)

    write_payload(buffer, dead_offset, old_hash, dead_payload, version=1, flags=0)
    write_payload(buffer, live_offset, live_hash, live_payload, version=2, flags=0)
    path.write_bytes(buffer)


def write_payload(buffer: bytearray, offset: int, sector_hash: int, payload: bytes, version: int, flags: int) -> None:
    write_u32(buffer, offset + PAYLOAD_MAGIC_OFFSET, PAYLOAD_MAGIC)
    write_u64(buffer, offset + PAYLOAD_HASH_OFFSET, sector_hash)
    write_i32(buffer, offset + PAYLOAD_BYTES_OFFSET, len(payload))
    write_u32(buffer, offset + PAYLOAD_VERSION_OFFSET, version)
    buffer[offset + PAYLOAD_FLAGS_OFFSET] = flags & 0xFF
    payload_start = offset + PAYLOAD_HEADER_SIZE_BYTES
    buffer[payload_start:payload_start + len(payload)] = payload


def run_hash_collision_test(samples: int, seed: int) -> dict[str, int | float]:
    try:
        return run_hash_collision_test_numpy(samples, seed)
    except ImportError:
        return run_hash_collision_test_python(samples, seed)


def run_hash_collision_test_numpy(samples: int, seed: int) -> dict[str, int | float]:
    import numpy as np

    coord_mask = np.uint64((1 << 21) - 1)
    coord_bias = np.int64(1 << 20)
    values = np.arange(samples, dtype=np.uint64) + np.uint64(seed)
    sample = splitmix64_numpy(values)

    x = ((sample & coord_mask).astype(np.int64) - coord_bias).view(np.uint64)
    y = (((sample >> np.uint64(21)) & coord_mask).astype(np.int64) - coord_bias).view(np.uint64)
    z = (((sample >> np.uint64(42)) & coord_mask).astype(np.int64) - coord_bias).view(np.uint64)

    hashes = np.full(samples, FNV_OFFSET_BASIS, dtype=np.uint64)
    hashes ^= x
    hashes *= np.uint64(FNV_PRIME)
    hashes ^= y
    hashes *= np.uint64(FNV_PRIME)
    hashes ^= z
    hashes *= np.uint64(FNV_PRIME)
    hashes ^= np.uint64(512)
    hashes *= np.uint64(FNV_PRIME)
    hashes[hashes == np.uint64(0)] = np.uint64(1)
    hashes.sort()
    collisions = int(np.count_nonzero(hashes[1:] == hashes[:-1])) if samples > 1 else 0
    expected_collision_pairs = (samples * (samples - 1)) / (2.0 * 2.0**64)
    probability_any_collision = 1.0 - math.exp(-expected_collision_pairs)
    return {
        "samples": samples,
        "seed": seed,
        "observed_collisions": collisions,
        "duplicate_coordinates_skipped": 0,
        "expected_collision_pairs": expected_collision_pairs,
        "probability_any_collision": probability_any_collision,
    }


def splitmix64_numpy(values):
    import numpy as np

    values = (values + np.uint64(0x9E3779B97F4A7C15)).astype(np.uint64, copy=False)
    values = ((values ^ (values >> np.uint64(30))) * np.uint64(0xBF58476D1CE4E5B9)).astype(np.uint64, copy=False)
    values = ((values ^ (values >> np.uint64(27))) * np.uint64(0x94D049BB133111EB)).astype(np.uint64, copy=False)
    return (values ^ (values >> np.uint64(31))).astype(np.uint64, copy=False)


def run_hash_collision_test_python(samples: int, seed: int) -> dict[str, int | float]:
    seen: set[int] = set()
    collisions = 0
    coord_mask = (1 << 21) - 1
    coord_bias = 1 << 20
    for index in range(samples):
        sample = splitmix64(seed + index)
        x = (sample & coord_mask) - coord_bias
        y = ((sample >> 21) & coord_mask) - coord_bias
        z = ((sample >> 42) & coord_mask) - coord_bias
        sector_hash = FNV_OFFSET_BASIS
        sector_hash = mix_hash(sector_hash, x)
        sector_hash = mix_hash(sector_hash, y)
        sector_hash = mix_hash(sector_hash, z)
        sector_hash = mix_hash(sector_hash, 512)
        if sector_hash == 0:
            sector_hash = 1
        if sector_hash in seen:
            collisions += 1
        else:
            seen.add(sector_hash)

    expected_collision_pairs = (samples * (samples - 1)) / (2.0 * 2.0**64)
    probability_any_collision = 1.0 - math.exp(-expected_collision_pairs)
    return {
        "samples": samples,
        "seed": seed,
        "observed_collisions": collisions,
        "duplicate_coordinates_skipped": 0,
        "expected_collision_pairs": expected_collision_pairs,
        "probability_any_collision": probability_any_collision,
    }


def print_audit(result: dict[str, int | float | str]) -> None:
    print(f"H8DB_HEALTH path={result['path']}")
    print(f"file_bytes={result['file_bytes']} append_offset={result['append_offset']} mapped_slack_bytes={result['mapped_slack_bytes']}")
    print(f"dead_bytes={result['dead_bytes']} fragmentation_percent={result['fragmentation_percent']:.6f}")
    print(f"sector_size={result['sector_size']} nodes={result['nodes']} max_depth={result['max_depth']} keys={result['keys']}")
    print(f"live_payloads={result['live_payloads']} live_payload_bytes={result['live_payload_bytes']} live_payload_record_bytes={result['live_payload_record_bytes']}")
    print(f"payload_padding_bytes={result['payload_padding_bytes']} node_padding_bytes={result['node_padding_bytes']} unaligned_payloads={result['unaligned_payloads']}")


def print_rle_audit() -> None:
    eight_bit_step = 16.0 / 254.0
    four_bit_full_step = 16.0 / 15.0
    four_bit_narrow_step = 1.0 / 15.0
    print("RLE_AUDIT existing=SaveVoxelDeltaRun8 bytes_per_run=8 density=signed_8bit")
    print(f"quant_8bit_range_pm8_step_m={eight_bit_step:.6f} max_error_m={eight_bit_step * 0.5:.6f}")
    print(f"quant_4bit_range_pm8_step_m={four_bit_full_step:.6f} max_error_m={four_bit_full_step * 0.5:.6f} verdict=REJECT_LOD0_LOD1")
    print(f"quant_4bit_narrow_pm0_5_step_m={four_bit_narrow_step:.6f} max_error_m={four_bit_narrow_step * 0.5:.6f} verdict=LOD2_VISUAL_ONLY")
    print("density_lane_space_saving_4bit=50.000000 overall_run_saving_if_metadata_unchanged=6.250000")
    print("approved_policy=8bit_signed_sdf_for_player_facing_chunks;4bit_narrow_density_only_for_far_visual_RLE_after_LOD_hysteresis")


def main() -> int:
    parser = argparse.ArgumentParser(description="Audit HECTON-8 .h8db fragmentation and cold RLE/hash facts.")
    sub = parser.add_subparsers(dest="command", required=True)

    audit_parser = sub.add_parser("audit", help="Audit an existing .h8db file.")
    audit_parser.add_argument("path", type=Path)

    dummy_parser = sub.add_parser("dummy-audit", help="Create and audit a deterministic dummy .h8db file.")
    dummy_parser.add_argument("--path", type=Path, default=Path("Docs/AgentLogs/Dummy_MACRO_DB_INDEX_OPTIMIZER.h8db"))

    hash_parser = sub.add_parser("hash-audit", help="Run deterministic sector-hash collision simulation.")
    hash_parser.add_argument("--samples", type=int, default=10_000_000)
    hash_parser.add_argument("--seed", type=int, default=0x48384442)

    sub.add_parser("rle-audit", help="Print voxel delta RLE quantization audit.")

    args = parser.parse_args()
    if args.command == "audit":
        print_audit(audit_h8db(args.path))
        return 0
    if args.command == "dummy-audit":
        create_dummy_h8db(args.path)
        print_audit(audit_h8db(args.path))
        return 0
    if args.command == "hash-audit":
        result = run_hash_collision_test(args.samples, args.seed)
        print(
            "HASH_AUDIT "
            f"samples={result['samples']} seed={result['seed']} "
            f"observed_collisions={result['observed_collisions']} "
            f"duplicate_coordinates_skipped={result['duplicate_coordinates_skipped']} "
            f"expected_collision_pairs={result['expected_collision_pairs']:.12f} "
            f"probability_any_collision={result['probability_any_collision']:.12f}"
        )
        return 0
    if args.command == "rle-audit":
        print_rle_audit()
        return 0
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
