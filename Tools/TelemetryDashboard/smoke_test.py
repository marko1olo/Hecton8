from __future__ import annotations

import csv
import json
import shutil
import struct
import sys
from pathlib import Path

sys.dont_write_bytecode = True

import server


SMOKE_ROOT = server.PROJECT_ROOT / "Temp" / "CodexValidation" / "BLACKBOX_TELEMETRY_VISUALIZER_SMOKE"


def main() -> int:
    root = SMOKE_ROOT
    if root.exists():
        shutil.rmtree(root)
    root.mkdir(parents=True, exist_ok=True)

    generic = server.GENERIC_BLACKBOX_HEADER.pack(server.HECTON8_MAGIC, 2, server.GENERIC_BLACKBOX_ENTRY.size)
    generic += server.GENERIC_BLACKBOX_ENTRY.pack(
        10, 3, 0.010, 1.5, 7.25, 512.0, 1.0, 2.0, 3.0, 8, 0, 2, 4, 123, 456, 9
    )
    generic += server.GENERIC_BLACKBOX_ENTRY.pack(
        11, 3, 0.020, 1.5, 7.25, 513.0, 1.0, 2.0, 3.0, 8, 0, 2, 4, 123, 456, 9
    )
    (root / "Dump_PLAYER_KINEMATICS.bin").write_bytes(generic)
    h8dump_dir = root / "persistent_copy"
    h8dump_dir.mkdir(exist_ok=True)
    h8dump_path = h8dump_dir / "BLACKBOX_CRASH.h8dump"
    h8dump_path.write_bytes(generic)
    (root / "BLACKBOX_CRASH.h8dump").write_bytes(generic)
    parsed_crash = server.parse_dump_file(h8dump_path)
    assert parsed_crash["type"] == "crash_telemetry_buffer"
    assert parsed_crash["latest"]["spike"] is True

    false_positive_generic = server.GENERIC_BLACKBOX_HEADER.pack(server.HECTON8_MAGIC, 2, server.GENERIC_BLACKBOX_ENTRY.size)
    false_positive_generic += server.GENERIC_BLACKBOX_ENTRY.pack(
        64, 3, 0.010, 0.0, 7.25, 512.0, 1.0, 2.0, 3.0, 8, 0, 2, 4, 123, 456, 9
    )
    false_positive_generic += server.GENERIC_BLACKBOX_ENTRY.pack(
        65, 3, 0.020, 0.0, 7.25, 513.0, 1.0, 2.0, 3.0, 8, 0, 2, 4, 123, 456, 9
    )
    false_positive_path = root / "Dump_FALSE_POSITIVE_GENERIC_BLACKBOX.bin"
    false_positive_path.write_bytes(false_positive_generic)
    assert server.parse_dump_file(false_positive_path)["type"] == "generic_blackbox"

    job_hash = server.compute_job_admission_state_hash(77, 0xA1100001, 0.25, 0.0, 3, 0, 0x12)
    job_admission = server.JOB_ADMISSION_BLACKBOX_HEADER.pack(server.HECTON8_MAGIC, 2, 2, 64, 0, 77, 0)
    job_admission += server.JOB_ADMISSION_BLACKBOX_ENTRY_PREFIX.pack(
        77, 0xA1100001, 0.25, 0.0, 3, 0, 1, 0x12, 0, job_hash
    )
    job_admission += bytes(64 - server.JOB_ADMISSION_BLACKBOX_ENTRY_PREFIX.size)
    job_admission += bytes(64)
    job_path = root / "Dump_SIMULATION_BUCKET_DISTRIBUTOR_JobAdmission.bin"
    job_path.write_bytes(job_admission)
    parsed_job = server.parse_dump_file(job_path)
    assert parsed_job["type"] == "job_admission_blackbox"
    assert parsed_job["version"] == 2
    assert parsed_job["latest"]["denied"] is True
    assert parsed_job["latest"]["insufficientBudget"] is True
    assert parsed_job["latest"]["stateHashOk"] is True

    legacy_hash = server.compute_job_admission_state_hash(78, 0xA1100002, 0.5, 0.0, 1, 0, 0x03)
    legacy_job = server.JOB_ADMISSION_BLACKBOX_HEADER.pack(server.HECTON8_MAGIC, 1, 1, 64, 0, 78, 0)
    legacy_job += server.JOB_ADMISSION_BLACKBOX_ENTRY_PREFIX.pack(
        78, 0xA1100002, 0.5, 0.0, 1, 0, 1, 0x03, 0, legacy_hash
    )
    legacy_job += bytes(64 - server.JOB_ADMISSION_BLACKBOX_ENTRY_PREFIX.size)
    legacy_path = root / "Dump_SIMULATION_BUCKET_DISTRIBUTOR_JobAdmission_LegacyV1.bin"
    legacy_path.write_bytes(legacy_job)
    parsed_legacy_job = server.parse_dump_file(legacy_path)
    assert parsed_legacy_job["type"] == "job_admission_blackbox"
    assert parsed_legacy_job["version"] == 1
    assert parsed_legacy_job["latest"]["legacyStarved"] is True
    assert "denied" not in parsed_legacy_job["latest"]
    assert parsed_legacy_job["latest"]["stateHashOk"] is True

    mismatched_job = server.JOB_ADMISSION_BLACKBOX_HEADER.pack(server.HECTON8_MAGIC, 2, 1, 64, 0, 79, 0)
    mismatched_job += server.JOB_ADMISSION_BLACKBOX_ENTRY_PREFIX.pack(
        79, 0xA1100004, 0.75, 0.0, 2, 0, 1, 0x02, 0, 0xBAD0BAD0
    )
    mismatched_job += bytes(64 - server.JOB_ADMISSION_BLACKBOX_ENTRY_PREFIX.size)
    parsed_mismatched_job = server.try_parse_job_admission_blackbox(mismatched_job)
    assert parsed_mismatched_job is not None
    assert parsed_mismatched_job["latest"]["stateHashOk"] is False
    assert "state_hash_mismatch" in parsed_mismatched_job["warnings"]
    invalid_cursor_job = server.JOB_ADMISSION_BLACKBOX_HEADER.pack(server.HECTON8_MAGIC, 2, 1, 64, 1, 79, 0)
    invalid_cursor_job += bytes(64)
    assert server.try_parse_job_admission_blackbox(invalid_cursor_job) is None

    old_sorted_late_job = server.JOB_ADMISSION_BLACKBOX_HEADER.pack(server.HECTON8_MAGIC, 2, 1, 64, 0, 1, 0)
    old_sorted_late_job += server.JOB_ADMISSION_BLACKBOX_ENTRY_PREFIX.pack(
        1,
        0xA1100003,
        0.1,
        0.0,
        0,
        0,
        1,
        0x01,
        0,
        server.compute_job_admission_state_hash(1, 0xA1100003, 0.1, 0.0, 0, 0, 0x01),
    )
    old_sorted_late_job += bytes(64 - server.JOB_ADMISSION_BLACKBOX_ENTRY_PREFIX.size)
    (root / "Dump_Z_SIMULATION_BUCKET_DISTRIBUTOR_JobAdmission_OldFrame.bin").write_bytes(old_sorted_late_job)

    simulation_flags = (1 << 1) | (1 << 5)
    simulation_bucket = server.SIMULATION_BUCKET_BLACKBOX_HEADER.pack(server.HECTON8_MAGIC, 1, 2, 64, 0, 88, 5)
    simulation_bucket += server.SIMULATION_BUCKET_BLACKBOX_ENTRY.pack(
        88, 1, 2, 3, 4, 5, simulation_flags, 5, 1.25, 0.04, 3.0, 1.5, 2.1, 0.75, 2, 1, 0, 0x12345678
    )
    simulation_bucket += bytes(64)
    simulation_path = root / "Dump_SIMULATION_BUCKET_DISTRIBUTOR.bin"
    simulation_path.write_bytes(simulation_bucket)
    parsed_simulation = server.parse_dump_file(simulation_path)
    assert parsed_simulation["type"] == "simulation_bucket_blackbox"
    assert parsed_simulation["version"] == 1
    assert parsed_simulation["latest"]["preSimulationOverBudget"] is True
    assert parsed_simulation["latest"]["homeostasisKillRequested"] is True
    assert parsed_simulation["latest"]["framePacingFlagLabels"] == ["pre-sim-over-budget", "homeostasis-kill"]
    assert parsed_simulation["latest"]["activeBucketLoadMs"] == 1.25
    invalid_simulation_cursor = server.SIMULATION_BUCKET_BLACKBOX_HEADER.pack(server.HECTON8_MAGIC, 1, 1, 64, 1, 89, 5)
    invalid_simulation_cursor += bytes(64)
    assert server.try_parse_simulation_bucket_blackbox(invalid_simulation_cursor) is None

    terrain_faults = (1 << 0) | (1 << 8)
    terrain_streaming = server.TERRAIN_STREAMING_HEADER.pack(server.HECTON8_MAGIC, 1305, 2, 64, terrain_faults)
    terrain_streaming += server.TERRAIN_STREAMING_PAGER_ENTRY.pack(
        100.0, 200.0, 300.0, 144, 0x1234ABCD, 8, 2, 1, 3, 0.75, 42, 128.0, terrain_faults, 1, 9
    )
    terrain_streaming += bytes(64)
    terrain_path = root / "Dump_1305_TerrainChunkPager.bin"
    terrain_path.write_bytes(terrain_streaming)
    parsed_terrain = server.parse_dump_file(terrain_path)
    assert parsed_terrain["type"] == "terrain_streaming_pager"
    assert parsed_terrain["version"] == 1305
    assert parsed_terrain["latest"]["faultLabels"] == ["missing-file", "checksum"]
    assert parsed_terrain["latest"]["activeChunks"] == 8
    assert parsed_terrain["latest"]["residencyEvalMicros"] == 42

    residency_flags = (1 << 3) | (1 << 10)
    packed_residency_flags = residency_flags | (7 << 16)
    residency_entry = server.WORLD_CHUNK_RESIDENCY_ENTRY.pack(
        123456789, -1, 2, -3, 0.5, 1.5, 2.5, 145, packed_residency_flags, 0xABCDEF01, 4, 11, 2, 1
    )
    residency = server.WORLD_CHUNK_RESIDENCY_HEADER.pack(
        server.HECTON8_MAGIC,
        server.WORLD_CHUNK_RESIDENCY_VERSION,
        2,
        server.WORLD_CHUNK_RESIDENCY_ENTRY.size,
        residency_flags,
        server.WORLD_CHUNK_RESIDENCY_LAYOUT_HASH,
        0,
    )
    residency += residency_entry
    residency += bytes(64)
    residency_path = root / "Dump_1305_WorldChunkResidency.bin"
    residency_path.write_bytes(residency)
    parsed_residency = server.parse_dump_file(residency_path)
    assert parsed_residency["type"] == "world_chunk_residency_blackbox"
    assert parsed_residency["version"] == server.WORLD_CHUNK_RESIDENCY_VERSION
    assert parsed_residency["reasonLabels"] == ["teleport", "addressables-fault"]
    assert parsed_residency["latest"]["flagLabels"] == ["teleport", "addressables-fault"]
    assert parsed_residency["latest"]["activeImpostorCount"] == 7
    assert parsed_residency["latest"]["residentCount"] == 11
    raw_residency_path = root / "Dump_1305_Streaming.bin"
    raw_residency_path.write_bytes(residency_entry + bytes(64))
    parsed_raw_residency = server.parse_dump_file(raw_residency_path)
    assert parsed_raw_residency["type"] == "world_chunk_residency_blackbox"
    assert parsed_raw_residency["latest"]["frame"] == 145

    bus_hash_offset = 64
    bus_source_offset = 512
    bus_mock_physics_offset = bus_source_offset + server.GLOBAL_TELEMETRY_BUS_SOURCE_CAPACITY * 64
    bus_mock_origin_offset = bus_mock_physics_offset + 64
    bus_frame_stride = bus_mock_origin_offset + 64
    bus_valid_frames = 2
    bus_header = bytearray(server.GLOBAL_TELEMETRY_BUS_HEADER_BYTES)
    server.GLOBAL_TELEMETRY_PREFIX.pack_into(bus_header, 0, 123456789, 22, 0x57444721)
    bus_metadata = [
        server.GLOBAL_TELEMETRY_BUS_DUMP_MAGIC,
        server.GLOBAL_TELEMETRY_BUS_DUMP_VERSION,
        server.GLOBAL_TELEMETRY_BUS_HEADER_BYTES,
        bus_valid_frames,
        bus_frame_stride,
        bus_valid_frames * bus_frame_stride,
        0xAABBCCDD,
        bus_valid_frames,
        2,
        3,
        bus_hash_offset,
        bus_source_offset,
        bus_mock_physics_offset,
        bus_mock_origin_offset,
        0x11223344,
        0x55667788,
        64,
        48,
        48,
        server.GLOBAL_TELEMETRY_BUS_SOURCE_DESCRIPTOR_METADATA_INDEX,
        server.GLOBAL_TELEMETRY_BUS_SOURCE_DESCRIPTOR_UINT_STRIDE,
        server.GLOBAL_TELEMETRY_BUS_SOURCE_CAPACITY,
    ]
    for index, value in enumerate(bus_metadata):
        struct.pack_into("<I", bus_header, server.GLOBAL_TELEMETRY_BUS_METADATA_OFFSET + index * 4, value)
    descriptor_offset = (
        server.GLOBAL_TELEMETRY_BUS_METADATA_OFFSET
        + server.GLOBAL_TELEMETRY_BUS_SOURCE_DESCRIPTOR_METADATA_INDEX * 4
    )
    struct.pack_into("<IIII", bus_header, descriptor_offset, 0xABC00001, 1, 16, 0)
    struct.pack_into("<IIII", bus_header, descriptor_offset + 16, server.SURVIVAL_BLACKBOX_SOURCE_HASH, 1, 64, 1)
    bus_frames = bytearray(bus_valid_frames * bus_frame_stride)
    for frame_index, frame_number in enumerate((200, 201)):
        frame_offset = frame_index * bus_frame_stride
        server.GLOBAL_TELEMETRY_PREFIX.pack_into(bus_frames, frame_offset, 123456800 + frame_index, frame_number, 0x57444721)
        struct.pack_into("<III", bus_frames, frame_offset + bus_hash_offset, 0x11111111, 0x22222222, 0x33333333)
        struct.pack_into("<IIII", bus_frames, frame_offset + bus_source_offset, 1, 2, 3, frame_number)
    survival_bus_flags = (
        (1 << 1)
        | (1 << 2)
        | (1 << 4)
        | (1 << 5)
        | (1 << 6)
        | (1 << 7)
        | (1 << 8)
        | (1 << 9)
        | (2 << server.SURVIVAL_BLACKBOX_DEATH_CAUSE_SHIFT)
    )
    server.SURVIVAL_BLACKBOX_SOURCE_ENTRY.pack_into(
        bus_frames,
        bus_frame_stride + bus_source_offset + server.GLOBAL_TELEMETRY_BUS_SOURCE_STRIDE_BYTES,
        server.SURVIVAL_BLACKBOX_SOURCE_HASH,
        201,
        0x504C5952,
        0.12,
        0.34,
        900.0,
        91.0,
        500.0,
        400.0,
        1.0,
        0.77,
        0.64,
        0.88,
        6.5,
        0xAABBCCDD,
        survival_bus_flags,
    )
    bus_path = root / "Dump_GLOBAL_TELEMETRY_BUS.bin"
    bus_path.write_bytes(bytes(bus_header) + bytes(bus_frames))
    parsed_bus = server.parse_dump_file(bus_path)
    assert parsed_bus["type"] == "global_telemetry_bus_blackbox"
    assert parsed_bus["version"] == server.GLOBAL_TELEMETRY_BUS_DUMP_VERSION
    assert parsed_bus["latest"]["frame"] == 201
    assert parsed_bus["latest"]["eventHashCount"] == 3
    assert parsed_bus["latest"]["sourceNonZeroCount"] == 2
    assert parsed_bus["latest"]["decodedSourceCount"] == 1
    assert parsed_bus["latest"]["survival"]["playerEntityHashHex"] == "0x504C5952"
    assert parsed_bus["latest"]["survival"]["oxygen01"] == 0.12
    assert parsed_bus["latest"]["survival"]["pressureAtm"] == 91.0
    assert parsed_bus["latest"]["survival"]["deathCauseLabel"] == "pressure-collapse"
    assert parsed_bus["latest"]["survival"]["flagLabels"] == [
        "underwater",
        "beyond-safe-depth",
        "bends",
        "fresh-physiology",
        "narcosis",
        "toxicity",
        "thermal-stress",
        "has-stats",
    ]
    assert "survival_source_warnings" in parsed_bus["warnings"]
    assert parsed_bus["sourceDescriptors"][0]["sourceHash"] == 0xABC00001
    assert parsed_bus["sourceDescriptors"][1]["sourceName"] == "survival"

    foveated = server.FOVEATED_SIMULATION_HEADER.pack(server.FOVEATED_SIMULATION_MAGIC, 3, 2)
    foveated_hash_0 = server.compute_foveated_simulation_state_hash(5, 1, 2, 2, 1)
    foveated_hash_1 = server.compute_foveated_simulation_state_hash(8, 2, 3, 2, 3)
    foveated += server.FOVEATED_SIMULATION_ENTRY.pack(
        300, 5, 1, 2, 2, 1, 10.0, 20.0, 30.0, 0.0, 0.0, 1.0, 0, foveated_hash_0
    )
    foveated += server.FOVEATED_SIMULATION_ENTRY.pack(
        301, 8, 2, 3, 2, 3, 11.0, 21.0, 31.0, 0.0, 0.0, 1.0, 1, foveated_hash_1
    )
    foveated += bytes(server.FOVEATED_SIMULATION_ENTRY.size)
    foveated_path = root / "Dump_FOVEATED_SIMULATION_DIRECTOR.bin"
    foveated_path.write_bytes(foveated)
    parsed_foveated = server.parse_dump_file(foveated_path)
    assert parsed_foveated["type"] == "foveated_simulation_blackbox"
    assert parsed_foveated["latest"]["frame"] == 301
    assert parsed_foveated["latest"]["stateHashOk"] is True
    assert parsed_foveated["latest"]["flagLabels"] == ["force-refresh"]
    renamed_foveated_path = root / "Renamed_Header_F8LD.bin"
    renamed_foveated_path.write_bytes(foveated)
    assert server.parse_dump_file(renamed_foveated_path)["type"] == "foveated_simulation_blackbox"
    mismatched_foveated = server.FOVEATED_SIMULATION_HEADER.pack(server.FOVEATED_SIMULATION_MAGIC, 1, 0)
    mismatched_foveated += server.FOVEATED_SIMULATION_ENTRY.pack(
        302, 8, 2, 3, 2, 3, 11.0, 21.0, 31.0, 0.0, 0.0, 1.0, 0, 0xBADF00D
    )
    parsed_mismatched_foveated = server.parse_foveated_simulation_blackbox(mismatched_foveated)
    assert parsed_mismatched_foveated["latest"]["stateHashOk"] is False
    assert "state_hash_mismatch" in parsed_mismatched_foveated["warnings"]

    input_entry = server.INPUT_DETERMINISM_ENTRY.pack(12.5, 400, 7, 0x00000003, 0x4B424D21, 750, 2, 1, 0x06)
    input_entry += bytes(64 - server.INPUT_DETERMINISM_ENTRY.size)
    input_path = root / "Dump_INPUT_DETERMINISM.bin"
    input_path.write_bytes(input_entry + bytes(64))
    parsed_input = server.parse_dump_file(input_path)
    assert parsed_input["type"] == "input_determinism_blackbox"
    assert parsed_input["latest"]["currentInputScheme"] == "keyboard-mouse"
    assert parsed_input["latest"]["flagLabels"] == ["delay", "nonfinite-sanitized"]
    assert "polling_time_over_500us" in parsed_input["warnings"]
    assert "nonfinite_sanitized" in parsed_input["warnings"]

    origin_flags = (1 << 2) | (1 << 4)
    origin_combined_stride = server.ORIGIN_SHIFT_BASE_ENTRY.size + server.ORIGIN_SHIFT_DETAIL_ENTRY.size
    origin_entry_count = 2
    origin_payload_bytes = origin_entry_count * origin_combined_stride
    origin = server.ORIGIN_SHIFT_HEADER.pack(
        server.ORIGIN_SHIFT_MAGIC,
        server.ORIGIN_SHIFT_VERSION,
        server.ORIGIN_SHIFT_HEADER.size,
        origin_entry_count,
        server.ORIGIN_SHIFT_BASE_ENTRY.size,
        origin_payload_bytes,
        0,
        501,
        server.ORIGIN_SHIFT_LITTLE_ENDIAN_TAG,
        server.ORIGIN_SHIFT_FLAG_HAS_DETAIL_ROWS,
        server.ORIGIN_SHIFT_DETAIL_ENTRY.size,
        origin_combined_stride,
    )
    origin += server.ORIGIN_SHIFT_BASE_ENTRY.pack(
        1.0, 0.0, -1.0, 500, 3, 44, 0xFACE0001, 80, 12, 0, 32, 0, 1 << 3
    )
    origin += server.ORIGIN_SHIFT_DETAIL_ENTRY.pack(
        1000.0, 0.0, -1000.0, 1.5, 2.5, 3.5, 0.25, 0.98, 0xFACE0001, 0x01020304, 4
    )
    origin += server.ORIGIN_SHIFT_BASE_ENTRY.pack(
        2.0, 0.0, -2.0, 501, 4, 45, 0xFACE0002, 96, 16, 32, 32, 1, origin_flags
    )
    origin += server.ORIGIN_SHIFT_DETAIL_ENTRY.pack(
        1002.0, 0.0, -1002.0, 4.5, 5.5, 6.5, 0.5, 0.91, 0xFACE0002, 0x05060708, 6
    )
    origin_path = root / "Dump_ORIGIN_SHIFT.bin"
    origin_path.write_bytes(origin)
    parsed_origin = server.parse_dump_file(origin_path)
    assert parsed_origin["type"] == "origin_shift_blackbox"
    assert parsed_origin["version"] == server.ORIGIN_SHIFT_VERSION
    assert parsed_origin["latest"]["frame"] == 501
    assert parsed_origin["latest"]["flagLabels"] == ["time-sliced", "shift-commit"]
    assert parsed_origin["latest"]["hotEntitiesShifted"] == 6
    assert parsed_origin["latest"]["rebaseComputeTimeMs"] == 0.5
    renamed_origin_path = root / "Renamed_Header_AUPD.bin"
    renamed_origin_path.write_bytes(origin)
    assert server.parse_dump_file(renamed_origin_path)["type"] == "origin_shift_blackbox"

    sentinel_type_name = "Hecton8.World.AbsoluteUniversePosition"
    sentinel_name_bytes = sentinel_type_name.encode("ascii")
    sentinel = server.BINARY_LAYOUT_SENTINEL_HEADER.pack(
        server.BINARY_LAYOUT_SENTINEL_MAGIC,
        server.BINARY_LAYOUT_SENTINEL_VERSION,
        0x4F464653,
        48,
        56,
        len(sentinel_name_bytes),
        0x12345678,
    )
    sentinel_path = root / "Dump_BINARY_LAYOUT_SENTINEL.bin"
    sentinel_path.write_bytes(sentinel + sentinel_name_bytes)
    parsed_sentinel = server.parse_dump_file(sentinel_path)
    assert parsed_sentinel["type"] == "binary_layout_sentinel"
    assert parsed_sentinel["version"] == server.BINARY_LAYOUT_SENTINEL_VERSION
    assert parsed_sentinel["latest"]["typeName"] == sentinel_type_name
    assert parsed_sentinel["latest"]["contextHash"] == 0x4F464653
    assert parsed_sentinel["latest"]["expected"] == 48
    assert parsed_sentinel["latest"]["observed"] == 56
    assert parsed_sentinel["latest"]["layoutMatches"] is False
    assert "layout_mismatch" in parsed_sentinel["warnings"]
    renamed_sentinel_path = root / "Renamed_Header_H8BL.bin"
    renamed_sentinel_path.write_bytes(sentinel + sentinel_name_bytes)
    assert server.parse_dump_file(renamed_sentinel_path)["type"] == "binary_layout_sentinel"

    terminal_faults = (1 << 1) | (1 << 2)
    terminal_os = server.TERMINAL_OS_HEADER.pack(
        server.TERMINAL_OS_MAGIC,
        server.TERMINAL_OS_VERSION,
        terminal_faults,
        2,
        4,
        2,
        server.TERMINAL_OS_ENTRY.size,
        server.TERMINAL_OS_SOURCE_HASH,
    )
    terminal_os += server.TERMINAL_OS_ENTRY.pack(
        610, 12, 3, 2, 0.25, 40.0, 80.0, 0, 0x12345678, 0x00ABCDEF, 0.75, 0.10, 12, 2, 9.0, 0.8
    )
    terminal_os += server.TERMINAL_OS_ENTRY.pack(
        611,
        13,
        4,
        3,
        0.75,
        55.0,
        120.0,
        terminal_faults,
        0x87654321,
        0x00FEDCBA,
        0.65,
        0.20,
        13,
        1,
        11.0,
        0.7,
    )
    terminal_os_path = root / "Dump_1309_TerminalOS.bin"
    terminal_os_path.write_bytes(terminal_os)
    parsed_terminal_os = server.parse_dump_file(terminal_os_path)
    assert parsed_terminal_os["type"] == "terminal_os_blackbox"
    assert parsed_terminal_os["version"] == server.TERMINAL_OS_VERSION
    assert parsed_terminal_os["latest"]["frame"] == 611
    assert parsed_terminal_os["latest"]["faultLabels"] == ["format-budget", "nonfinite"]
    assert parsed_terminal_os["latest"]["dirtyCount"] == 4
    assert parsed_terminal_os["dumpFaultLabels"] == ["format-budget", "nonfinite"]

    decryption_flags = (1 << 0) | (1 << 2) | (31 << server.TERMINAL_DECRYPTION_HOLD_FRAME_SHIFT)
    decryption_faults = 1 << 4
    terminal_decryption = server.TERMINAL_DECRYPTION_HEADER.pack(
        server.TERMINAL_DECRYPTION_MAGIC,
        server.TERMINAL_DECRYPTION_VERSION,
        decryption_faults,
        1,
        2,
        server.TERMINAL_DECRYPTION_ENTRY.size,
    )
    terminal_decryption += server.TERMINAL_DECRYPTION_ENTRY.pack(
        620, 0xDECA0001, 12.0, 0.1, 13.0, 0.2, 0.92, 80.0, 1 << 0, 0xABC10001, 0xABC20001, 0
    )
    terminal_decryption += server.TERMINAL_DECRYPTION_ENTRY.pack(
        621,
        0xDECA0002,
        14.0,
        0.3,
        14.1,
        0.35,
        0.99,
        140.0,
        decryption_flags,
        0xABC10002,
        0xABC20002,
        decryption_faults,
    )
    terminal_decryption_path = root / "Dump_1309_TerminalDecryption.bin"
    terminal_decryption_path.write_bytes(terminal_decryption)
    parsed_terminal_decryption = server.parse_dump_file(terminal_decryption_path)
    assert parsed_terminal_decryption["type"] == "terminal_decryption_blackbox"
    assert parsed_terminal_decryption["version"] == server.TERMINAL_DECRYPTION_VERSION
    assert parsed_terminal_decryption["latest"]["frame"] == 621
    assert parsed_terminal_decryption["latest"]["flagLabels"] == ["active", "initialized", "hold=31"]
    assert parsed_terminal_decryption["latest"]["holdFrames"] == 31
    assert parsed_terminal_decryption["latest"]["faultLabels"] == ["decryption-budget"]
    assert "solve_threshold_reached" in parsed_terminal_decryption["warnings"]

    terminal_projection_faults = (1 << 16) | (1 << 17)
    terminal_projection = server.TERMINAL_PROJECTION_HEADER.pack(
        server.TERMINAL_PROJECTION_MAGIC,
        server.TERMINAL_PROJECTION_VERSION,
        terminal_projection_faults,
        1,
        2,
        server.TERMINAL_PROJECTION_ENTRY_BYTES,
        server.TERMINAL_PROJECTION_INPUT_STATE_STRIDE_BYTES,
        server.TERMINAL_PROJECTION_ROLLBACK_EXCLUDED,
    )
    terminal_projection += server.TERMINAL_PROJECTION_ENTRY.pack(
        630, 12, 10, 2, 80.0, 8.5, 0.9, 0, 0xFFFFFFFF, 1, 0xABCD0001, 0.0065, 0.01, 0
    )
    terminal_projection += server.TERMINAL_PROJECTION_ENTRY.pack(
        631,
        12,
        9,
        3,
        230.0,
        9.0,
        0.75,
        terminal_projection_faults,
        0xFFFFFFFF,
        1,
        0xABCD0002,
        0.007,
        0.012,
        2,
    )
    terminal_projection_path = root / "Dump_1309_TerminalProjection.bin"
    terminal_projection_path.write_bytes(terminal_projection)
    parsed_terminal_projection = server.parse_dump_file(terminal_projection_path)
    assert parsed_terminal_projection["type"] == "terminal_projection_blackbox"
    assert parsed_terminal_projection["version"] == server.TERMINAL_PROJECTION_VERSION
    assert parsed_terminal_projection["latest"]["frame"] == 631
    assert parsed_terminal_projection["latest"]["faultLabels"] == ["projection-nonfinite", "projection-budget"]
    assert parsed_terminal_projection["latest"]["nonFiniteCount"] == 2
    assert "projection_nonfinite" in parsed_terminal_projection["warnings"]
    assert "projection_budget" in parsed_terminal_projection["warnings"]

    openxr_flags = (1 << 0) | (1 << 1) | (1 << 3) | (1 << 4)
    openxr = server.OPENXR_MANUAL_OVERRIDE_HEADER.pack(3, 1)
    openxr += server.OPENXR_MANUAL_OVERRIDE_ENTRY.pack(
        0.1, 0.2, 0.3, 0.0, 0.0, 0.0, 44.0, 85.0, 120.0, 700, 1 << 0
    )
    openxr += server.OPENXR_MANUAL_OVERRIDE_ENTRY.pack(
        0.2, 0.3, 0.4, 0.0, 0.0, 0.0, 55.0, 85.0, 130.0, 701, 1 << 3
    )
    openxr += server.OPENXR_MANUAL_OVERRIDE_ENTRY.pack(
        0.3, 0.4, 0.5, 0.0, 0.0, 0.0, 86.0, 85.0, 160.0, 702, openxr_flags
    )
    openxr_path = root / "Dump_1335_OpenXRManualOverrideLever.bin"
    openxr_path.write_bytes(openxr)
    parsed_openxr = server.parse_dump_file(openxr_path)
    assert parsed_openxr["type"] == "openxr_manual_override_blackbox"
    assert parsed_openxr["latest"]["frame"] == 702
    assert parsed_openxr["latest"]["flagLabels"] == ["grabbed", "latched", "xr-active", "projection-singular"]
    assert parsed_openxr["latest"]["latched"] is True
    assert "projection_singular" in parsed_openxr["warnings"]

    damage_flags = (1 << 0) | (1 << 2) | (1 << 4) | (1 << 5)
    vehicle_damage = server.VEHICLE_DAMAGE_HOLOGRAPHER_HEADER.pack(
        server.VEHICLE_DAMAGE_HOLOGRAPHER_MAGIC,
        2,
        server.VEHICLE_DAMAGE_HOLOGRAPHER_ENTRY_BYTES,
        1,
    )
    vehicle_damage += server.VEHICLE_DAMAGE_HOLOGRAPHER_ENTRY.pack(710, 4, 64, 0.25, 0.0, 1 << 0)
    vehicle_damage += server.VEHICLE_DAMAGE_HOLOGRAPHER_ENTRY.pack(711, 9, 128, 0.5, 0.35, damage_flags)
    vehicle_damage_path = root / "Dump_VEHICLE_SUB_OS_DAMAGE_HOLOGRAPHER.bin"
    vehicle_damage_path.write_bytes(vehicle_damage)
    parsed_vehicle_damage = server.parse_dump_file(vehicle_damage_path)
    assert parsed_vehicle_damage["type"] == "vehicle_damage_holographer_blackbox"
    assert parsed_vehicle_damage["latest"]["frame"] == 711
    assert parsed_vehicle_damage["latest"]["flagLabels"] == ["resources-ready", "active-dent", "flood", "fallback-warning"]
    assert parsed_vehicle_damage["latest"]["holoDamagePoints"] == 9
    assert "fallback_warning" in parsed_vehicle_damage["warnings"]
    assert "flood_active" in parsed_vehicle_damage["warnings"]

    pda_flags = (1 << 0) | (1 << 3) | (1 << 6)
    pda_projection = server.PDA_PROJECTION_HEADER.pack(
        server.PDA_PROJECTION_MAGIC,
        server.PDA_PROJECTION_VERSION,
        722,
        pda_flags,
        3,
        2,
        server.PDA_PROJECTION_ENTRY_BYTES,
        2 * server.PDA_PROJECTION_ENTRY_BYTES,
        2,
        0,
    )
    pda_projection += server.PDA_PROJECTION_ENTRY.pack(
        721, 1 << 0, 0x50444131, 50 * 65536, 0.42, 0.5, 0.8, 1, 0x01010101, 0x50444131, 0.18, 0.112, 1.33, 0.2, 0.75, 1 << 0
    )
    pda_projection += server.PDA_PROJECTION_ENTRY.pack(
        722,
        pda_flags,
        0x50444132,
        125 * 65536,
        0.55,
        0.9,
        0.6,
        2,
        0x02020202,
        0x50444132,
        0.2,
        0.12,
        1.45,
        0.35,
        0.6,
        pda_flags,
    )
    pda_projection_path = root / "Dump_1335_UIPresentation_PdaProjection.bin"
    pda_projection_path.write_bytes(pda_projection)
    parsed_pda_projection = server.parse_dump_file(pda_projection_path)
    assert parsed_pda_projection["type"] == "pda_projection_blackbox"
    assert parsed_pda_projection["version"] == server.PDA_PROJECTION_VERSION
    assert parsed_pda_projection["latest"]["frame"] == 722
    assert parsed_pda_projection["latest"]["flagLabels"] == ["active", "over-budget", "gpu-upload-fault"]
    assert parsed_pda_projection["latest"]["jobMicroseconds"] == 125.0
    assert "over_budget" in parsed_pda_projection["warnings"]
    assert "gpu_upload_fault" in parsed_pda_projection["warnings"]

    wrist_flags = (1 << 1) | (1 << 3) | (1 << 7)
    wrist_hud = server.WRIST_HUD_HEADER.pack(
        server.WRIST_HUD_MAGIC,
        server.WRIST_HUD_VERSION,
        732,
        wrist_flags,
        2,
        0,
        server.WRIST_HUD_ENTRY_BYTES,
        2 * server.WRIST_HUD_ENTRY_BYTES,
    )
    wrist_hud += server.WRIST_HUD_ENTRY.pack(
        731, 0x10000001, 1 << 1, 18, 12, 2, 75 * 65536, 1, 0.8, 220.0, 300.0, 0.1, 0.05, 0.75, 181.0, 1.0
    )
    wrist_hud += server.WRIST_HUD_ENTRY.pack(
        732,
        0x10000002,
        wrist_flags,
        24,
        15,
        4,
        140 * 65536,
        0,
        0.7,
        240.0,
        300.0,
        0.2,
        0.10,
        0.65,
        183.0,
        1.0,
    )
    wrist_hud_path = root / "Dump_1335_WristHologramHud.bin"
    wrist_hud_path.write_bytes(wrist_hud)
    parsed_wrist_hud = server.parse_dump_file(wrist_hud_path)
    assert parsed_wrist_hud["type"] == "wrist_hud_blackbox"
    assert parsed_wrist_hud["version"] == server.WRIST_HUD_VERSION
    assert parsed_wrist_hud["latest"]["frame"] == 732
    assert parsed_wrist_hud["latest"]["flagLabels"] == ["pda-open", "job-over-budget", "gpu-upload-fault"]
    assert parsed_wrist_hud["latest"]["jobMicroseconds"] == 140.0
    assert "job_over_budget" in parsed_wrist_hud["warnings"]
    assert "gpu_upload_fault" in parsed_wrist_hud["warnings"]

    ladder_flags = (1 << 0) | (1 << 2) | (1 << 3) | (1 << 7)
    ladder_header = server.LADDER_CLIMB_IK_HEADER.pack(
        server.LADDER_CLIMB_IK_MAGIC,
        server.LADDER_CLIMB_IK_VERSION,
        2,
        server.LADDER_CLIMB_IK_ENTRY_BYTES,
        2,
        0,
    )
    ladder_entry_0 = server.LADDER_CLIMB_IK_ENTRY_PREFIX.pack(
        0.0,
        1.0,
        2.0,
        -0.2,
        1.1,
        2.1,
        0.2,
        1.1,
        2.1,
        -0.4,
        1.0,
        2.0,
        0.4,
        1.0,
        2.0,
        1.25,
        0.9,
        4,
        5,
        740,
        0xCAFE0001,
        (1 << 0) | (1 << 5) | (1 << 6),
    )
    ladder_entry_1 = server.LADDER_CLIMB_IK_ENTRY_PREFIX.pack(
        0.0,
        1.2,
        2.2,
        -0.25,
        1.3,
        2.3,
        0.25,
        1.3,
        2.3,
        -0.45,
        1.2,
        2.2,
        0.45,
        1.2,
        2.2,
        1.75,
        0.65,
        5,
        6,
        741,
        0xCAFE0002,
        ladder_flags,
    )
    ladder_path = root / "Dump_LADDER_CLIMB_IK.bin"
    ladder_path.write_bytes(
        ladder_header
        + ladder_entry_0
        + bytes(server.LADDER_CLIMB_IK_ENTRY_BYTES - server.LADDER_CLIMB_IK_ENTRY_PREFIX.size)
        + ladder_entry_1
        + bytes(server.LADDER_CLIMB_IK_ENTRY_BYTES - server.LADDER_CLIMB_IK_ENTRY_PREFIX.size)
    )
    parsed_ladder = server.parse_dump_file(ladder_path)
    assert parsed_ladder["type"] == "ladder_climb_ik_blackbox"
    assert parsed_ladder["version"] == server.LADDER_CLIMB_IK_VERSION
    assert parsed_ladder["latest"]["frame"] == 741
    assert parsed_ladder["latest"]["flagLabels"] == ["active", "vr-grip", "slip", "unreachable"]
    assert parsed_ladder["latest"]["progressMeters"] == 1.75
    assert parsed_ladder["latest"]["stamina01"] == 0.65
    assert "slip" in parsed_ladder["warnings"]
    assert "unreachable" in parsed_ladder["warnings"]

    sonar_flags = (1 << 1) | (1 << 2) | (1 << 31)
    sonar = server.TOPOGRAPHICAL_SONAR_HEADER.pack(
        server.TOPOGRAPHICAL_SONAR_MAGIC,
        server.TOPOGRAPHICAL_SONAR_VERSION,
        2,
        server.TOPOGRAPHICAL_SONAR_ENTRY_BYTES,
        2,
        sonar_flags,
        512,
        12,
    )
    sonar += server.TOPOGRAPHICAL_SONAR_ENTRY.pack(
        10.5,
        1000.0,
        0.0,
        -1000.0,
        999.0,
        0.0,
        -1001.0,
        750,
        11,
        2000,
        256,
        200,
        (1 << 0) | (1 << 3),
        0.9,
        120.0,
        1.0,
        2.0,
        3.0,
        4.0,
        5.0,
        6.0,
        64.0,
        0.85,
        7,
        900,
    )
    sonar += server.TOPOGRAPHICAL_SONAR_ENTRY.pack(
        10.7,
        1002.0,
        0.0,
        -1002.0,
        1000.0,
        0.0,
        -1004.0,
        751,
        12,
        4000,
        512,
        384,
        sonar_flags,
        0.75,
        140.0,
        2.0,
        3.0,
        4.0,
        5.0,
        6.0,
        7.0,
        96.0,
        0.65,
        8,
        24000,
    )
    sonar_path = root / "Dump_SONAR_SYNTHESIZER.bin"
    sonar_path.write_bytes(sonar)
    parsed_sonar = server.parse_dump_file(sonar_path)
    assert parsed_sonar["type"] == "topographical_sonar_blackbox"
    assert parsed_sonar["version"] == server.TOPOGRAPHICAL_SONAR_VERSION
    assert parsed_sonar["latest"]["frame"] == 751
    assert parsed_sonar["latest"]["flagLabels"] == ["sdf-unavailable", "gpu-upload", "fault"]
    assert parsed_sonar["latest"]["computeTimeMicroseconds"] == 24000
    assert "fault" in parsed_sonar["warnings"]
    assert "sdf_unavailable" in parsed_sonar["warnings"]
    assert "gpu_upload" in parsed_sonar["warnings"]

    kinetic_flags = (1 << 0) | (1 << 2) | (1 << 6) | (1 << 31)
    kinetic_payload = server.KINETIC_CHARACTER_ENTRY.pack(
        10,
        -2,
        4,
        1.0,
        2.0,
        3.0,
        760,
        64,
        2.25,
        125.5,
        0x14030001,
        1 << 0,
        0.9,
    )
    kinetic_payload += server.KINETIC_CHARACTER_ENTRY.pack(
        10,
        -2,
        5,
        1.5,
        2.5,
        3.5,
        761,
        96,
        3.5,
        181.25,
        0x14030002,
        kinetic_flags,
        0.55,
    )
    kinetic_hash = server.fnv1a_mix_bytes((2166136261 ^ 2) & 0xFFFFFFFF, kinetic_payload)
    kinetic_hash = 2166136261 if kinetic_hash == 0 else kinetic_hash
    kinetic = server.KINETIC_CHARACTER_HEADER.pack(
        server.KINETIC_CHARACTER_MAGIC,
        server.KINETIC_CHARACTER_VERSION,
        2,
        2,
        server.KINETIC_CHARACTER_ENTRY_BYTES,
        kinetic_hash,
    )
    kinetic_path = root / "Dump_1403_KINETIC_CHARACTER.bin"
    kinetic_path.write_bytes(kinetic + kinetic_payload)
    parsed_kinetic = server.parse_dump_file(kinetic_path)
    assert parsed_kinetic["type"] == "kinetic_character_blackbox"
    assert parsed_kinetic["version"] == server.KINETIC_CHARACTER_VERSION
    assert parsed_kinetic["latest"]["frame"] == 761
    assert parsed_kinetic["latest"]["flagLabels"] == ["visible", "sdf-brace", "quality-collapsed", "invalid"]
    assert parsed_kinetic["latest"]["bonesEvaluated"] == 96
    assert parsed_kinetic["dumpHashOk"] is True
    assert "quality_collapsed" in parsed_kinetic["warnings"]
    assert "invalid" in parsed_kinetic["warnings"]

    procedural_flags = (1 << 0) | (1 << 1) | (1 << 2) | (1 << 31)
    procedural_payload = server.PROCEDURAL_BONE_ENTRY.pack(
        770,
        2,
        120,
        112,
        0.65,
        0x1403B001,
        1 << 0,
        0.95,
        1.75,
        12.5,
        0,
        1,
        4.0,
        5.0,
        6.0,
        0,
    )
    procedural_payload += server.PROCEDURAL_BONE_ENTRY.pack(
        771,
        3,
        144,
        132,
        0.95,
        0x1403B002,
        procedural_flags,
        0.5,
        2.25,
        15.5,
        3,
        2,
        4.5,
        5.5,
        6.5,
        0,
    )
    procedural_seed = (2166136261 ^ 2 ^ 2 ^ 0x414E494D) & 0xFFFFFFFF
    procedural_hash = server.fnv1a_mix_bytes(procedural_seed, procedural_payload)
    procedural_hash = 2166136261 if procedural_hash == 0 else procedural_hash
    procedural = server.PROCEDURAL_BONE_HEADER.pack(
        server.PROCEDURAL_BONE_MAGIC,
        server.PROCEDURAL_BONE_VERSION,
        2,
        2,
        server.PROCEDURAL_BONE_ENTRY_BYTES,
        procedural_hash,
    )
    procedural_path = root / "Dump_1403_PROCEDURAL_BONE.bin"
    procedural_path.write_bytes(procedural + procedural_payload)
    parsed_procedural = server.parse_dump_file(procedural_path)
    assert parsed_procedural["type"] == "procedural_bone_blackbox"
    assert parsed_procedural["version"] == server.PROCEDURAL_BONE_VERSION
    assert parsed_procedural["latest"]["frame"] == 771
    assert parsed_procedural["latest"]["flagLabels"] == ["visible", "quality-collapse", "jaw-solved", "invalid"]
    assert parsed_procedural["latest"]["invalidMathCount"] == 3
    assert parsed_procedural["dumpHashOk"] is True
    assert "quality_collapse" in parsed_procedural["warnings"]
    assert "invalid_math_count" in parsed_procedural["warnings"]
    assert "invalid" in parsed_procedural["warnings"]

    def pack_vr_somatic_entry(
        frame: int,
        flags: int,
        hand_ghost_mask: int,
        near_collision: float,
        comfort_vignette: float,
        head_angular_speed: float,
        kcc_horizon_lock: float,
    ) -> bytes:
        row = server.VR_SOMATIC_ENTRY.pack(
            frame,
            0,
            flags,
            hand_ghost_mask,
            1.0,
            2.0,
            3.0,
            0.0,
            0.0,
            0.0,
            1.0,
            near_collision,
            comfort_vignette,
            0.25,
            0.36,
            head_angular_speed,
            42,
            2.5,
            48.0,
            0.35,
            kcc_horizon_lock,
            7,
            frame - 1,
            0x534F4D41,
            0,
            0,
        )
        state_hash = server.compute_vr_somatic_state_hash(row)
        return server.VR_SOMATIC_ENTRY.pack(
            frame,
            state_hash,
            flags,
            hand_ghost_mask,
            1.0,
            2.0,
            3.0,
            0.0,
            0.0,
            0.0,
            1.0,
            near_collision,
            comfort_vignette,
            0.25,
            0.36,
            head_angular_speed,
            42,
            2.5,
            48.0,
            0.35,
            kcc_horizon_lock,
            7,
            frame - 1,
            0x534F4D41,
            0,
            0,
        )

    somatic_flags = (
        (1 << 0)
        | (1 << 2)
        | (1 << 3)
        | (1 << 6)
        | (1 << 9)
        | (1 << 10)
        | (1 << 11)
        | (1 << 12)
        | (1 << 13)
        | (1 << 14)
    )
    somatic = server.VR_SOMATIC_HEADER.pack(
        server.VR_SOMATIC_MAGIC,
        server.VR_SOMATIC_VERSION,
        server.VR_SOMATIC_FRAME_CAPACITY,
        2,
    )
    somatic += pack_vr_somatic_entry(780, 1 << 0, 0, 0.0, 0.05, 0.75, 0.0)
    somatic += pack_vr_somatic_entry(781, somatic_flags, 3, 0.7, 0.42, 4.5, 0.55)
    somatic_path = root / "Dump_1335_SomaticComfort.bin"
    somatic_path.write_bytes(somatic)
    parsed_somatic = server.parse_dump_file(somatic_path)
    assert parsed_somatic["type"] == "vr_somatic_blackbox"
    assert parsed_somatic["version"] == server.VR_SOMATIC_VERSION
    assert parsed_somatic["latest"]["frame"] == 781
    assert parsed_somatic["latest"]["stateHashOk"] is True
    assert parsed_somatic["latest"]["flagLabels"] == [
        "active",
        "left-ghost",
        "right-ghost",
        "near-collision",
        "frame-pressure",
        "protective-fallback",
        "acceleration-tunnel",
        "kcc-signal",
        "kcc-acceleration-tunnel",
        "dynamic-horizon-lock",
    ]
    assert parsed_somatic["latest"]["handGhostMask"] == 3
    assert "near_collision" in parsed_somatic["warnings"]
    assert "frame_pressure" in parsed_somatic["warnings"]
    assert "protective_fallback" in parsed_somatic["warnings"]
    assert "acceleration_tunnel" in parsed_somatic["warnings"]

    lockstep_flags = (1 << 0) | (1 << 1) | (1 << 3) | (1 << 6) | (1 << 8)
    lockstep_master_hash = 0x12345678ABCDEF01
    lockstep = server.LOCKSTEP_STATE_VALIDATOR_HEADER.pack(
        server.LOCKSTEP_STATE_VALIDATOR_MAGIC,
        server.LOCKSTEP_STATE_VALIDATOR_VERSION,
        2,
        server.LOCKSTEP_STATE_VALIDATOR_ENTRY_BYTES,
        2,
        lockstep_master_hash,
    )
    lockstep += server.LOCKSTEP_STATE_VALIDATOR_ENTRY.pack(
        790,
        0xABCDEF00,
        0x12345678,
        0x100,
        0x200,
        0x300,
        0x400,
        1 << 0,
        10,
        1,
        3,
        30,
        0,
        0,
        0,
        0,
    )
    lockstep += server.LOCKSTEP_STATE_VALIDATOR_ENTRY.pack(
        791,
        0xABCDEF01,
        0x12345678,
        0x101,
        0x201,
        0x301,
        0x401,
        lockstep_flags,
        11,
        1,
        4,
        31,
        (1 << 1) | (1 << 3),
        1 << 2,
        5,
        0,
    )
    lockstep_path = root / "Dump_1403_LOCKSTEP_STATE_VALIDATOR.bin"
    lockstep_path.write_bytes(lockstep)
    parsed_lockstep = server.parse_dump_file(lockstep_path)
    assert parsed_lockstep["type"] == "lockstep_state_validator_blackbox"
    assert parsed_lockstep["version"] == server.LOCKSTEP_STATE_VALIDATOR_VERSION
    assert parsed_lockstep["latest"]["frame"] == 791
    assert parsed_lockstep["latest"]["masterHashHex"] == "0x12345678ABCDEF01"
    assert parsed_lockstep["latestMasterHashMatchesHeader"] is True
    assert parsed_lockstep["latest"]["flagLabels"] == ["hash-executed", "missing-data", "nonfinite", "desync", "layout-invalid"]
    assert parsed_lockstep["latest"]["missingLabels"] == ["player-kinematic-state", "entity-aups"]
    assert parsed_lockstep["latest"]["nonFiniteLabels"] == ["room-water-levels"]
    assert "desync" in parsed_lockstep["warnings"]
    assert "missing_data" in parsed_lockstep["warnings"]
    assert "nonfinite" in parsed_lockstep["warnings"]
    assert "layout_invalid" in parsed_lockstep["warnings"]
    renamed_lockstep_path = root / "Renamed_Header_LSDUMP.bin"
    renamed_lockstep_path.write_bytes(lockstep)
    assert server.parse_dump_file(renamed_lockstep_path)["type"] == "lockstep_state_validator_blackbox"

    voxel_flags = (1 << 0) | (1 << 2) | (1 << 4) | (1 << 7) | (1 << 9) | (1 << 10) | (1 << 11) | (1 << 12)
    voxel_astar = server.VOXEL_ASTAR_ENTRY.pack(
        800,
        2,
        4,
        0,
        1,
        0,
        128,
        64,
        900,
        1 << 12,
        11,
        0xAAA10001,
        0.9,
        1.15,
        12,
        6,
        0,
    )
    voxel_astar += server.VOXEL_ASTAR_ENTRY.pack(
        801,
        3,
        5,
        2,
        0,
        1,
        256,
        96,
        2200,
        voxel_flags,
        12,
        0xAAA10002,
        0.55,
        1.35,
        24,
        0,
        0,
    )
    voxel_astar_path = root / "Dump_1403_VOXEL_ASTAR.bin"
    voxel_astar_path.write_bytes(voxel_astar)
    parsed_voxel_astar = server.parse_dump_file(voxel_astar_path)
    assert parsed_voxel_astar["type"] == "voxel_astar_blackbox"
    assert parsed_voxel_astar["latest"]["frame"] == 801
    assert parsed_voxel_astar["latest"]["flagLabels"] == [
        "nonfinite-input",
        "goal-out-of-bounds",
        "goal-blocked",
        "raw-path-overflow",
        "sdf-missing",
        "nan-detected",
        "time-slice-over-budget",
        "weighted-heuristic",
    ]
    assert parsed_voxel_astar["latest"]["droppedRequests"] == 2
    assert parsed_voxel_astar["latest"]["failedPaths"] == 1
    assert "nonfinite" in parsed_voxel_astar["warnings"]
    assert "time_slice_over_budget" in parsed_voxel_astar["warnings"]
    assert "dropped_requests" in parsed_voxel_astar["warnings"]
    assert "failed_paths" in parsed_voxel_astar["warnings"]
    assert "sdf_missing" in parsed_voxel_astar["warnings"]
    assert "overflow" in parsed_voxel_astar["warnings"]
    assert "out_of_bounds" in parsed_voxel_astar["warnings"]
    assert "blocked" in parsed_voxel_astar["warnings"]

    path_funnel_flags = (1 << 0) | (1 << 1)
    path_funnel = server.PATH_FUNNEL_ENTRY.pack(
        0x1111222233334444,
        0,
        810,
        1,
        22,
        0xFEED1000,
        0.25,
        0,
        12,
        4,
        0,
        0,
    )
    path_funnel += server.PATH_FUNNEL_ENTRY.pack(
        0x1111222233335555,
        0,
        811,
        3,
        23,
        0xFEED1001,
        0.85,
        0,
        13,
        5,
        2,
        path_funnel_flags,
    )
    path_funnel_path = root / "Dump_1403_PATH_FUNNEL.bin"
    path_funnel_path.write_bytes(path_funnel)
    parsed_path_funnel = server.parse_dump_file(path_funnel_path)
    assert parsed_path_funnel["type"] == "path_funnel_blackbox"
    assert parsed_path_funnel["latest"]["frame"] == 811
    assert parsed_path_funnel["latest"]["flagLabels"] == ["blackbox-dump-failed", "wfc-vault-signal-mismatch"]
    assert parsed_path_funnel["latest"]["lastSectorHashHex"] == "0x1111222233335555"
    assert parsed_path_funnel["latest"]["invalidatedPathCount"] == 2
    assert "blackbox_dump_failed" in parsed_path_funnel["warnings"]
    assert "wfc_vault_signal_mismatch" in parsed_path_funnel["warnings"]
    assert "path_invalidations" in parsed_path_funnel["warnings"]

    laser_flags = (
        (1 << 0)
        | (1 << 1)
        | (1 << 2)
        | (1 << 3)
        | (1 << 4)
        | (1 << 5)
    )
    laser_cutter = server.LASER_CUTTER_DOD_HEADER.pack(
        server.LASER_CUTTER_DOD_MAGIC,
        server.LASER_CUTTER_DOD_VERSION,
        981,
        2,
        server.LASER_CUTTER_DOD_ENTRY_BYTES,
        0,
        45,
        2 * server.LASER_CUTTER_DOD_ENTRY_BYTES,
    )
    laser_cutter += server.LASER_CUTTER_DOD_ENTRY.pack(
        1.0,
        2.0,
        3.0,
        4.0,
        5.0,
        6.0,
        0.0,
        0.0,
        1.0,
        5.5,
        0.75,
        0.80,
        980,
        44,
        0x4C435452,
        0x22500001,
        0,
        (1 << 0) | (1 << 2) | (1 << 3) | (1 << 4) | (1 << 5),
        64,
        0,
        server.LASER_CUTTER_DOD_LAYOUT_MAGIC,
        0.55,
        0x2250ABCDEF123456,
        135.0,
        18,
    )
    laser_cutter += server.LASER_CUTTER_DOD_ENTRY.pack(
        10.0,
        20.0,
        30.0,
        14.0,
        25.0,
        36.0,
        0.0,
        1.0,
        0.0,
        7.25,
        0.92,
        0.95,
        981,
        45,
        0x4C435452,
        0x22500002,
        0,
        laser_flags,
        128,
        990,
        server.LASER_CUTTER_DOD_LAYOUT_MAGIC,
        0.88,
        0x2250FEDCBA654321,
        165.0,
        24,
    )
    laser_cutter_path = root / "Dump_SHINOBU_225.bin"
    laser_cutter_path.write_bytes(laser_cutter)
    parsed_laser_cutter = server.parse_dump_file(laser_cutter_path)
    assert parsed_laser_cutter["type"] == "laser_cutter_dod_blackbox"
    assert parsed_laser_cutter["version"] == server.LASER_CUTTER_DOD_VERSION
    assert parsed_laser_cutter["latest"]["frame"] == 981
    assert parsed_laser_cutter["latest"]["requestSequence"] == 45
    assert parsed_laser_cutter["latest"]["toolHashHex"] == "0x4C435452"
    assert parsed_laser_cutter["latest"]["layoutMagicHex"] == "0x53484C43"
    assert parsed_laser_cutter["latest"]["stateHashHex"] == "0x2250FEDCBA654321"
    assert parsed_laser_cutter["latest"]["flagLabels"] == [
        "hit",
        "nonfinite",
        "shader-dent-only",
        "gpu-spark-only",
        "battery-drain-queued",
        "decal-queued",
    ]
    assert parsed_laser_cutter["latest"]["sparkCount"] == 128
    assert parsed_laser_cutter["latest"]["hit"] is True
    assert parsed_laser_cutter["latest"]["nonFinite"] is True
    assert "nonfinite" in parsed_laser_cutter["warnings"]

    wfc_flags = (1 << 0) | (1 << 2)
    wfc_laser = server.WFC_LASER_CUT_HEADER.pack(
        server.WFC_LASER_CUT_MAGIC,
        server.WFC_LASER_CUT_VERSION,
        2,
        server.WFC_LASER_CUT_ENTRY_BYTES,
        0,
        server.WFC_LASER_CUT_SOURCE_HASH,
        0,
        0,
    )
    wfc_laser += server.WFC_LASER_CUT_ENTRY.pack(
        1.0,
        2.0,
        3.0,
        4.0,
        5.0,
        6.0,
        0x2250ABCDEF000001,
        990,
        0x4C435452,
        0.40,
        0.10,
        0.70,
        0.60,
        0.20,
        1,
        12,
        0,
        0,
    )
    wfc_laser += server.WFC_LASER_CUT_ENTRY.pack(
        10.0,
        20.0,
        30.0,
        40.0,
        50.0,
        60.0,
        0x2250ABCDEF000002,
        991,
        0x4C435452,
        1.0,
        0.25,
        0.92,
        0.85,
        0.90,
        2,
        13,
        wfc_flags,
        0,
    )
    wfc_laser_path = root / "Dump_SHINOBU_225_WfcLaserCut.bin"
    wfc_laser_path.write_bytes(wfc_laser)
    parsed_wfc_laser = server.parse_dump_file(wfc_laser_path)
    assert parsed_wfc_laser["type"] == "wfc_laser_cut_blackbox"
    assert parsed_wfc_laser["version"] == server.WFC_LASER_CUT_VERSION
    assert parsed_wfc_laser["sourceHashHex"] == "0x544C5352"
    assert parsed_wfc_laser["latest"]["frame"] == 991
    assert parsed_wfc_laser["latest"]["sectorHashHex"] == "0x2250ABCDEF000002"
    assert parsed_wfc_laser["latest"]["cellIndex"] == 13
    assert parsed_wfc_laser["latest"]["progress01"] == 1.0
    assert parsed_wfc_laser["latest"]["flagLabels"] == ["completed", "stress-reduced"]
    assert parsed_wfc_laser["latest"]["completed"] is True
    assert parsed_wfc_laser["latest"]["stressReduced"] is True
    assert "stress_reduced" in parsed_wfc_laser["warnings"]
    renamed_laser_path = root / "Renamed_Header_SH25.bin"
    renamed_laser_path.write_bytes(laser_cutter)
    assert server.parse_dump_file(renamed_laser_path)["type"] == "laser_cutter_dod_blackbox"
    renamed_wfc_path = root / "Renamed_Header_WFCL.bin"
    renamed_wfc_path.write_bytes(wfc_laser)
    assert server.parse_dump_file(renamed_wfc_path)["type"] == "wfc_laser_cut_blackbox"

    tool_kinematics_flags = (1 << 1) | (1 << 7) | (1 << 8) | (1 << 13) | (1 << 16)
    tool_kinematics_entry_count = 2 * server.TOOL_KINEMATICS_BLACKBOX_CAPACITY
    tool_kinematics_payload_bytes = tool_kinematics_entry_count * server.TOOL_KINEMATICS_ENTRY_BYTES
    tool_kinematics = bytearray(
        server.TOOL_KINEMATICS_HEADER.pack(
            server.TOOL_KINEMATICS_MAGIC,
            server.TOOL_KINEMATICS_VERSION,
            tool_kinematics_entry_count,
            server.TOOL_KINEMATICS_ENTRY_BYTES,
            2,
            17,
            1201,
            tool_kinematics_payload_bytes,
        )
        + bytes(tool_kinematics_payload_bytes)
    )
    server.TOOL_KINEMATICS_ENTRY.pack_into(
        tool_kinematics,
        server.TOOL_KINEMATICS_HEADER_BYTES + (17 * server.TOOL_KINEMATICS_ENTRY_BYTES),
        1199,
        0x5343414E,
        0.10,
        0.95,
        12.50,
        24,
        6.25,
        1 << 0,
        0.10,
        0.20,
        0.30,
        1.10,
        1.20,
        1.30,
        0x524F434B,
        0,
    )
    server.TOOL_KINEMATICS_ENTRY.pack_into(
        tool_kinematics,
        server.TOOL_KINEMATICS_HEADER_BYTES
        + ((server.TOOL_KINEMATICS_BLACKBOX_CAPACITY + 17) * server.TOOL_KINEMATICS_ENTRY_BYTES),
        1201,
        0x4C435554,
        0.93,
        0.05,
        7.50,
        72,
        8.25,
        tool_kinematics_flags,
        1.0,
        2.0,
        3.0,
        4.0,
        5.0,
        6.0,
        0x4D45544C,
        0,
    )
    tool_kinematics_path = root / "Dump_TOOL_KINEMATICS.bin"
    tool_kinematics_path.write_bytes(tool_kinematics)
    parsed_tool_kinematics = server.parse_dump_file(tool_kinematics_path)
    assert parsed_tool_kinematics["type"] == "tool_kinematics_blackbox"
    assert parsed_tool_kinematics["version"] == server.TOOL_KINEMATICS_VERSION
    assert parsed_tool_kinematics["declaredEntryCount"] == 600
    assert parsed_tool_kinematics["toolCapacity"] == 2
    assert parsed_tool_kinematics["telemetryCursor"] == 17
    assert parsed_tool_kinematics["nonEmptyEntryCount"] == 2
    assert parsed_tool_kinematics["latest"]["frame"] == 1201
    assert parsed_tool_kinematics["latest"]["toolSlot"] == 1
    assert parsed_tool_kinematics["latest"]["ringSlot"] == 17
    assert parsed_tool_kinematics["latest"]["toolName"] == "laser-cutter"
    assert parsed_tool_kinematics["latest"]["materialHashHex"] == "0x4D45544C"
    assert parsed_tool_kinematics["latest"]["flagLabels"] == [
        "active",
        "fault",
        "ray-hit",
        "raymarch-budget-exceeded",
        "power-depleted-signal-queued",
    ]
    assert parsed_tool_kinematics["latest"]["fault"] is True
    assert parsed_tool_kinematics["latest"]["raymarchBudgetExceeded"] is True
    assert "fault_flag" in parsed_tool_kinematics["warnings"]
    assert "raymarch_budget_exceeded" in parsed_tool_kinematics["warnings"]
    renamed_tool_kinematics_path = root / "Renamed_Header_TKBB.bin"
    renamed_tool_kinematics_path.write_bytes(tool_kinematics)
    assert server.parse_dump_file(renamed_tool_kinematics_path)["type"] == "tool_kinematics_blackbox"

    auxiliary_flags = (1 << 2) | (1 << 29) | (1 << 31)
    auxiliary_equipment = bytearray(server.AUXILIARY_EQUIPMENT_TELEMETRY_CAPACITY * server.AUXILIARY_EQUIPMENT_ENTRY_BYTES)
    server.AUXILIARY_EQUIPMENT_ENTRY.pack_into(
        auxiliary_equipment,
        8 * server.AUXILIARY_EQUIPMENT_ENTRY_BYTES,
        1301,
        12,
        2,
        3,
        1,
        60.0,
        210.5,
        0.75,
        1 << 1,
        0x2290ABCD,
        0,
        0,
        0,
        8,
        0,
    )
    server.AUXILIARY_EQUIPMENT_ENTRY.pack_into(
        auxiliary_equipment,
        9 * server.AUXILIARY_EQUIPMENT_ENTRY_BYTES,
        1302,
        13,
        2,
        4,
        1,
        30.0,
        640.25,
        0.50,
        auxiliary_flags,
        0x2290DCBA,
        1,
        2,
        3,
        9,
        0,
    )
    auxiliary_path = root / "Dump_SHINOBU_229.bin"
    auxiliary_path.write_bytes(auxiliary_equipment)
    parsed_auxiliary = server.parse_dump_file(auxiliary_path)
    assert parsed_auxiliary["type"] == "auxiliary_equipment_blackbox"
    assert parsed_auxiliary["entrySize"] == server.AUXILIARY_EQUIPMENT_ENTRY_BYTES
    assert parsed_auxiliary["declaredEntryCount"] == server.AUXILIARY_EQUIPMENT_TELEMETRY_CAPACITY
    assert parsed_auxiliary["nonEmptyEntryCount"] == 2
    assert parsed_auxiliary["latest"]["frame"] == 1302
    assert parsed_auxiliary["latest"]["activeCount"] == 13
    assert parsed_auxiliary["latest"]["flagLabels"] == ["sensor-ping", "nonfinite-recovered", "faulted"]
    assert parsed_auxiliary["latest"]["snapshotHashHex"] == "0x2290DCBA"
    assert parsed_auxiliary["latest"]["droppedSlots"] == 1
    assert parsed_auxiliary["latest"]["droppedSignals"] == 2
    assert parsed_auxiliary["latest"]["corruptedSignals"] == 3
    assert "faulted" in parsed_auxiliary["warnings"]
    assert "nonfinite_recovered" in parsed_auxiliary["warnings"]
    assert "dropped_slots" in parsed_auxiliary["warnings"]
    assert "dropped_signals" in parsed_auxiliary["warnings"]
    assert "corrupted_signals" in parsed_auxiliary["warnings"]
    assert "cpu_over_500us" in parsed_auxiliary["warnings"]

    upgrade_matrix = bytearray(server.UPGRADE_MATRIX_TELEMETRY_CAPACITY * server.UPGRADE_MATRIX_ENTRY_BYTES)
    server.UPGRADE_MATRIX_ENTRY.pack_into(
        upgrade_matrix,
        3 * server.UPGRADE_MATRIX_ENTRY_BYTES,
        1401,
        64,
        128,
        64,
        48.25,
        0,
        server.UPGRADE_MATRIX_LAYOUT_MAGIC,
        0x2310ABCD,
        0x00000000FFFF0001,
        0x2310000000000001,
        0,
        0,
    )
    server.UPGRADE_MATRIX_ENTRY.pack_into(
        upgrade_matrix,
        4 * server.UPGRADE_MATRIX_ENTRY_BYTES,
        1402,
        96,
        384,
        96,
        142.75,
        1 << 0,
        server.UPGRADE_MATRIX_LAYOUT_MAGIC,
        0x2310DCBA,
        0x00000000FFFF0002,
        0x2310000000000002,
        0,
        0,
    )
    upgrade_matrix_path = root / "Dump_SHINOBU_231.bin"
    upgrade_matrix_path.write_bytes(upgrade_matrix)
    parsed_upgrade_matrix = server.parse_dump_file(upgrade_matrix_path)
    assert parsed_upgrade_matrix["type"] == "upgrade_matrix_blackbox"
    assert parsed_upgrade_matrix["entrySize"] == server.UPGRADE_MATRIX_ENTRY_BYTES
    assert parsed_upgrade_matrix["declaredEntryCount"] == server.UPGRADE_MATRIX_TELEMETRY_CAPACITY
    assert parsed_upgrade_matrix["nonEmptyEntryCount"] == 2
    assert parsed_upgrade_matrix["latest"]["frame"] == 1402
    assert parsed_upgrade_matrix["latest"]["evaluatedMaskCount"] == 96
    assert parsed_upgrade_matrix["latest"]["activeBitCount"] == 384
    assert parsed_upgrade_matrix["latest"]["faultLabels"] == ["burst-over-budget"]
    assert parsed_upgrade_matrix["latest"]["layoutMagicHex"] == "0x55323331"
    assert parsed_upgrade_matrix["latest"]["lastEntityHashHex"] == "0x2310DCBA"
    assert parsed_upgrade_matrix["latest"]["lastMaskHex"] == "0x00000000FFFF0002"
    assert parsed_upgrade_matrix["latest"]["stateHashHex"] == "0x2310000000000002"
    assert "fault_flags" in parsed_upgrade_matrix["warnings"]
    assert "burst_over_100us" in parsed_upgrade_matrix["warnings"]

    metabolism_flags = (
        (1 << 0)
        | (1 << 1)
        | (1 << 2)
        | (1 << 3)
        | (1 << 4)
        | (1 << 6)
        | (1 << 8)
        | (1 << 9)
        | (1 << 10)
        | (1 << 30)
        | (1 << 31)
    )
    metabolism = bytearray(
        server.METABOLISM_BLACKBOX_HEADER.pack(
            server.METABOLISM_BLACKBOX_MAGIC,
            server.METABOLISM_BLACKBOX_VERSION,
            server.METABOLISM_TELEMETRY_CAPACITY,
            server.METABOLISM_TELEMETRY_ENTRY_BYTES,
            1452,
            4,
            server.METABOLISM_DETAIL_TELEMETRY_ENTRY_BYTES,
        )
        + bytes(server.METABOLISM_TELEMETRY_CAPACITY * server.METABOLISM_TELEMETRY_ENTRY_BYTES)
        + bytes(server.METABOLISM_TELEMETRY_CAPACITY * server.METABOLISM_DETAIL_TELEMETRY_ENTRY_BYTES)
    )
    metabolism_detail_offset = server.METABOLISM_BLACKBOX_HEADER_BYTES + (
        server.METABOLISM_TELEMETRY_CAPACITY * server.METABOLISM_TELEMETRY_ENTRY_BYTES
    )
    server.METABOLISM_TELEMETRY_ENTRY.pack_into(
        metabolism,
        server.METABOLISM_BLACKBOX_HEADER_BYTES + 3 * server.METABOLISM_TELEMETRY_ENTRY_BYTES,
        0x3200000000000001,
        1451,
        5,
        36.8,
        36.1,
        0.1,
        0,
        0,
        1,
        0.1,
        65.0,
        0.9,
        1 << 8,
        0,
        1,
    )
    server.METABOLISM_TELEMETRY_ENTRY.pack_into(
        metabolism,
        server.METABOLISM_BLACKBOX_HEADER_BYTES + 4 * server.METABOLISM_TELEMETRY_ENTRY_BYTES,
        0x3200000000000002,
        1452,
        5,
        32.5,
        27.5,
        0.84,
        2,
        1,
        3,
        0.1,
        240.5,
        0.55,
        metabolism_flags,
        4,
        7,
    )
    server.METABOLISM_DETAIL_TELEMETRY_ENTRY.pack_into(
        metabolism,
        metabolism_detail_offset + 4 * server.METABOLISM_DETAIL_TELEMETRY_ENTRY_BYTES,
        10.0,
        -42.0,
        90.0,
        120.0,
        4.25,
        -8.0,
        0.72,
        -45.0,
        -0.15,
        1452,
        0x504C5952,
        (1 << 2) | (1 << 3) | (1 << 10),
        0x53554954,
    )
    metabolism_path = root / "Dump_SHINOBU_320.bin"
    metabolism_path.write_bytes(metabolism)
    parsed_metabolism = server.parse_dump_file(metabolism_path)
    assert parsed_metabolism["type"] == "metabolism_blackbox"
    assert parsed_metabolism["version"] == server.METABOLISM_BLACKBOX_VERSION
    assert parsed_metabolism["entrySize"] == server.METABOLISM_TELEMETRY_ENTRY_BYTES
    assert parsed_metabolism["detailEntrySize"] == server.METABOLISM_DETAIL_TELEMETRY_ENTRY_BYTES
    assert parsed_metabolism["nonEmptyEntryCount"] == 2
    assert parsed_metabolism["nonEmptyDetailEntryCount"] == 1
    assert parsed_metabolism["latest"]["frame"] == 1452
    assert parsed_metabolism["latest"]["flagLabels"] == [
        "starving",
        "dehydrated",
        "hypothermia",
        "toxic",
        "invalid-math",
        "thermal-sampled",
        "chemical-sampled",
        "fatigue",
        "hypoxia",
        "execution-budget-exceeded",
        "nan-detected",
    ]
    assert parsed_metabolism["latest"]["maximumToxicity"] == 0.84
    assert parsed_metabolism["latestDetail"]["playerDepthMeters"] == 120.0
    assert parsed_metabolism["latestDetail"]["entityHashHex"] == "0x504C5952"
    assert parsed_metabolism["latestDetail"]["suitProfileHashHex"] == "0x53554954"
    assert "invalid_math" in parsed_metabolism["warnings"]
    assert "nan_detected" in parsed_metabolism["warnings"]
    assert "execution_budget_exceeded" in parsed_metabolism["warnings"]
    assert "execution_over_200us" in parsed_metabolism["warnings"]
    assert "starvation" in parsed_metabolism["warnings"]
    assert "dehydration" in parsed_metabolism["warnings"]
    assert "toxicity" in parsed_metabolism["warnings"]
    assert "hypothermia" in parsed_metabolism["warnings"]
    assert "hypoxia" in parsed_metabolism["warnings"]
    renamed_metabolism_path = root / "Renamed_Header_METASRGE.bin"
    renamed_metabolism_path.write_bytes(metabolism)
    assert server.parse_dump_file(renamed_metabolism_path)["type"] == "metabolism_blackbox"

    physiology = bytearray(
        server.PHYSIOLOGY_AUTOPSY_HEADER.pack(
            server.PHYSIOLOGY_AUTOPSY_MAGIC,
            server.PHYSIOLOGY_AUTOPSY_VERSION,
            server.PHYSIOLOGY_TELEMETRY_CAPACITY,
            server.PHYSIOLOGY_TELEMETRY_ENTRY_BYTES,
            6,
            0x50485953,
            1502,
            server.PHYSIOLOGY_TELEMETRY_CAPACITY,
            server.DECOMPRESSION_TELEMETRY_ENTRY_BYTES,
            7,
            server.PHYSIOLOGY_DECOMPRESSION_RING_BUFFER,
        )
        + bytes(server.PHYSIOLOGY_TELEMETRY_CAPACITY * server.PHYSIOLOGY_TELEMETRY_ENTRY_BYTES)
        + bytes(server.PHYSIOLOGY_TELEMETRY_CAPACITY * server.DECOMPRESSION_TELEMETRY_ENTRY_BYTES)
    )
    physiology_payload_offset = server.PHYSIOLOGY_AUTOPSY_HEADER_BYTES
    decompression_payload_offset = physiology_payload_offset + (
        server.PHYSIOLOGY_TELEMETRY_CAPACITY * server.PHYSIOLOGY_TELEMETRY_ENTRY_BYTES
    )
    server.PHYSIOLOGY_TELEMETRY_ENTRY.pack_into(
        physiology,
        physiology_payload_offset + 5 * server.PHYSIOLOGY_TELEMETRY_ENTRY_BYTES,
        0x3210000000000001,
        1 << 1,
        1501,
        0,
        0.82,
        0.33,
        35.8,
        2.4,
        0.15,
        0.20,
        92.0,
        0.35,
        0,
        64.0,
    )
    physiology_status = (1 << 3) | (1 << 8) | (1 << 12)
    physiology_fatal = (1 << 4) | (1 << 12) | (1 << 16)
    server.PHYSIOLOGY_TELEMETRY_ENTRY.pack_into(
        physiology,
        physiology_payload_offset + 6 * server.PHYSIOLOGY_TELEMETRY_ENTRY_BYTES,
        0x3210000000000002,
        physiology_status,
        1502,
        physiology_fatal,
        0.03,
        0.86,
        30.5,
        8.2,
        0.72,
        0.99,
        135.0,
        0.91,
        0x00000005,
        240.0,
    )
    server.DECOMPRESSION_TELEMETRY_ENTRY.pack_into(
        physiology,
        decompression_payload_offset + 7 * server.DECOMPRESSION_TELEMETRY_ENTRY_BYTES,
        0x3210D00000000001,
        1502,
        1 << 0,
        72.5,
        8.2,
        9.8,
        7.5,
        2.3,
        0.99,
        210.0,
        0.75,
        0x00000005,
        3,
        1 << 11,
        0,
    )
    physiology_path = root / "Dump_SHINOBU_321.bin"
    physiology_path.write_bytes(physiology)
    parsed_physiology = server.parse_dump_file(physiology_path)
    assert parsed_physiology["type"] == "physiology_autopsy_blackbox"
    assert parsed_physiology["version"] == server.PHYSIOLOGY_AUTOPSY_VERSION
    assert parsed_physiology["entrySize"] == server.PHYSIOLOGY_TELEMETRY_ENTRY_BYTES
    assert parsed_physiology["decompressionEntrySize"] == server.DECOMPRESSION_TELEMETRY_ENTRY_BYTES
    assert parsed_physiology["nonEmptyEntryCount"] == 2
    assert parsed_physiology["nonEmptyDecompressionEntryCount"] == 1
    assert parsed_physiology["latest"]["frame"] == 1502
    assert parsed_physiology["latest"]["statusEffectLabels"] == [
        "oxygen-critical",
        "hypoxia",
        "fatal-gas-toxicity",
    ]
    assert parsed_physiology["latest"]["fatalFlagLabels"] == [
        "fatal-oxygen",
        "hypoxia",
        "fatal-gas-toxicity",
    ]
    assert parsed_physiology["latest"]["bloodOxygen"] == 0.03
    assert parsed_physiology["latest"]["fatalGasToxicity"] is True
    assert parsed_physiology["latestDecompression"]["frame"] == 1502
    assert parsed_physiology["latestDecompression"]["bubbleFlagLabels"] == ["fast-tissue-over-m-value"]
    assert parsed_physiology["latestDecompression"]["fatalFlagLabels"] == ["fatal-bends"]
    assert "fatal_flags" in parsed_physiology["warnings"]
    assert "fatal_oxygen" in parsed_physiology["warnings"]
    assert "hypoxia" in parsed_physiology["warnings"]
    assert "fatal_gas_toxicity" in parsed_physiology["warnings"]
    assert "fatal_bends" in parsed_physiology["warnings"]
    assert "supersaturation_fatal_threshold" in parsed_physiology["warnings"]
    assert "execution_over_200us" in parsed_physiology["warnings"]
    renamed_physiology_path = root / "Renamed_Header_SHINOBU2.bin"
    renamed_physiology_path.write_bytes(physiology)
    assert server.parse_dump_file(renamed_physiology_path)["type"] == "physiology_autopsy_blackbox"

    sensory_flags = (
        (1 << 0)
        | (1 << 1)
        | (1 << 2)
        | (1 << 3)
        | (1 << 4)
        | (1 << 5)
        | (1 << 6)
        | (1 << 7)
        | (1 << 8)
    )
    sensory = bytearray(
        server.SENSORY_IMPAIRMENT_HEADER.pack(
            server.SENSORY_IMPAIRMENT_MAGIC,
            server.SENSORY_IMPAIRMENT_VERSION,
            server.SENSORY_IMPAIRMENT_TELEMETRY_CAPACITY,
            server.SENSORY_IMPAIRMENT_ENTRY_BYTES,
            9,
            server.SENSORY_IMPAIRMENT_SOURCE_HASH,
            1552,
        )
        + bytes(server.SENSORY_IMPAIRMENT_TELEMETRY_CAPACITY * server.SENSORY_IMPAIRMENT_ENTRY_BYTES)
    )
    server.SENSORY_IMPAIRMENT_ENTRY.pack_into(
        sensory,
        server.SENSORY_IMPAIRMENT_HEADER_BYTES + 8 * server.SENSORY_IMPAIRMENT_ENTRY_BYTES,
        0x3220000000000001,
        1551,
        (1 << 0),
        0.15,
        0.0,
        0.0,
        0.16,
        0.79,
        0.004,
        24.0,
        0.0,
        0.0,
        0.9,
        22.0,
        8,
    )
    server.SENSORY_IMPAIRMENT_ENTRY.pack_into(
        sensory,
        server.SENSORY_IMPAIRMENT_HEADER_BYTES + 9 * server.SENSORY_IMPAIRMENT_ENTRY_BYTES,
        0x3220000000000002,
        1552,
        sensory_flags,
        0.82,
        0.64,
        180.0,
        0.05,
        7.2,
        0.065,
        88.0,
        0.24,
        12.5,
        0.45,
        310.0,
        9,
    )
    sensory_path = root / "Dump_SHINOBU_322.bin"
    sensory_path.write_bytes(sensory)
    parsed_sensory = server.parse_dump_file(sensory_path)
    assert parsed_sensory["type"] == "sensory_impairment_blackbox"
    assert parsed_sensory["version"] == server.SENSORY_IMPAIRMENT_VERSION
    assert parsed_sensory["entrySize"] == server.SENSORY_IMPAIRMENT_ENTRY_BYTES
    assert parsed_sensory["nonEmptyEntryCount"] == 2
    assert parsed_sensory["latest"]["frame"] == 1552
    assert parsed_sensory["latest"]["flagLabels"] == [
        "hypoxia-active",
        "narcosis-active",
        "latency-active",
        "complex-noise-admitted",
        "mock-toxicity",
        "nonfinite-sanitized",
        "over-budget",
        "csv-profile",
        "input-corrupted",
    ]
    assert parsed_sensory["latest"]["oxygenPartialPressureAtm"] == 0.05
    assert parsed_sensory["latest"]["inputLatencyMilliseconds"] == 180.0
    assert parsed_sensory["latest"]["inputCorrupted"] is True
    assert "nonfinite_sanitized" in parsed_sensory["warnings"]
    assert "over_budget" in parsed_sensory["warnings"]
    assert "hypoxia" in parsed_sensory["warnings"]
    assert "narcosis" in parsed_sensory["warnings"]
    assert "input_latency" in parsed_sensory["warnings"]
    assert "input_corrupted" in parsed_sensory["warnings"]
    assert "mock_toxicity" in parsed_sensory["warnings"]
    renamed_sensory_path = root / "Renamed_Header_S322HYPO.bin"
    renamed_sensory_path.write_bytes(sensory)
    assert server.parse_dump_file(renamed_sensory_path)["type"] == "sensory_impairment_blackbox"

    suit_flags = (1 << 0) | (1 << 1) | (1 << 2) | (1 << 3) | (1 << 4) | (1 << 5) | (1 << 8)
    suit_signal_flags = (1 << 3) | (1 << 8)
    suit = bytearray(
        server.SUIT_INTEGRITY_HEADER.pack(
            server.SUIT_INTEGRITY_MAGIC,
            server.SUIT_INTEGRITY_VERSION,
            server.SUIT_INTEGRITY_TELEMETRY_CAPACITY,
            server.SUIT_INTEGRITY_ENTRY_BYTES,
            11,
            server.SUIT_INTEGRITY_SOURCE_HASH,
            1602,
        )
        + bytes(server.SUIT_INTEGRITY_TELEMETRY_CAPACITY * server.SUIT_INTEGRITY_ENTRY_BYTES)
    )
    server.SUIT_INTEGRITY_ENTRY.pack_into(
        suit,
        server.SUIT_INTEGRITY_HEADER_BYTES + 10 * server.SUIT_INTEGRITY_ENTRY_BYTES,
        0x3230000000000001,
        1601,
        0x504C5952,
        120.0,
        13.0,
        0.15,
        0.05,
        0.82,
        0.1,
        42.0,
        1 << 0,
        0x53554954,
        0.1,
        0,
        0,
    )
    server.SUIT_INTEGRITY_ENTRY.pack_into(
        suit,
        server.SUIT_INTEGRITY_HEADER_BYTES + 11 * server.SUIT_INTEGRITY_ENTRY_BYTES,
        0x3230000000000002,
        1602,
        0x504C5952,
        900.0,
        91.0,
        1.25,
        0.88,
        0.04,
        0.75,
        135.5,
        suit_flags,
        0x53554954,
        0.1,
        suit_signal_flags,
        0,
    )
    suit_path = root / "Dump_SHINOBU_323.bin"
    suit_path.write_bytes(suit)
    parsed_suit = server.parse_dump_file(suit_path)
    assert parsed_suit["type"] == "suit_integrity_blackbox"
    assert parsed_suit["version"] == server.SUIT_INTEGRITY_VERSION
    assert parsed_suit["entrySize"] == server.SUIT_INTEGRITY_ENTRY_BYTES
    assert parsed_suit["nonEmptyEntryCount"] == 2
    assert parsed_suit["latest"]["frame"] == 1602
    assert parsed_suit["latest"]["flagLabels"] == [
        "initialized",
        "warning",
        "buckling",
        "imploded",
        "nonfinite-pressure",
        "over-budget",
        "acoustic-groan",
    ]
    assert parsed_suit["latest"]["signalFlagLabels"] == ["imploded", "acoustic-groan"]
    assert parsed_suit["latest"]["entityHashHex"] == "0x504C5952"
    assert parsed_suit["latest"]["equippedSuitHashHex"] == "0x53554954"
    assert parsed_suit["latest"]["imploded"] is True
    assert parsed_suit["latest"]["currentIntegrity01"] == 0.04
    assert "imploded" in parsed_suit["warnings"]
    assert "pressure_warning" in parsed_suit["warnings"]
    assert "buckling" in parsed_suit["warnings"]
    assert "nonfinite_pressure" in parsed_suit["warnings"]
    assert "over_budget" in parsed_suit["warnings"]
    assert "execution_over_100us" in parsed_suit["warnings"]
    assert "integrity_critical" in parsed_suit["warnings"]
    renamed_suit_path = root / "Renamed_Header_S323PRES.bin"
    renamed_suit_path.write_bytes(suit)
    assert server.parse_dump_file(renamed_suit_path)["type"] == "suit_integrity_blackbox"

    radiation_flags = (
        (1 << 0)
        | (1 << 1)
        | (1 << 4)
        | (1 << 5)
        | (1 << 6)
        | (1 << 30)
        | (1 << 31)
    )
    radiation = bytearray(
        server.RADIATION_MUTATION_HEADER.pack(
            server.RADIATION_MUTATION_MAGIC,
            server.RADIATION_MUTATION_VERSION,
            server.RADIATION_MUTATION_TELEMETRY_CAPACITY,
            server.RADIATION_MUTATION_ENTRY_BYTES,
            13,
            server.RADIATION_MUTATION_SOURCE_HASH,
            1702,
        )
        + bytes(server.RADIATION_MUTATION_TELEMETRY_CAPACITY * server.RADIATION_MUTATION_ENTRY_BYTES)
    )
    server.RADIATION_MUTATION_ENTRY.pack_into(
        radiation,
        server.RADIATION_MUTATION_HEADER_BYTES + 12 * server.RADIATION_MUTATION_ENTRY_BYTES,
        0x3240000000000001,
        1701,
        (1 << 0) | (1 << 3),
        80.0,
        1.5,
        20.0,
        0.15,
        0.03,
        0.0,
        0.85,
        38.0,
        0.0,
        0.05,
        12,
        server.RADIATION_MUTATION_SOURCE_HASH,
    )
    server.RADIATION_MUTATION_ENTRY.pack_into(
        radiation,
        server.RADIATION_MUTATION_HEADER_BYTES + 13 * server.RADIATION_MUTATION_ENTRY_BYTES,
        0x3240000000000002,
        1702,
        radiation_flags,
        910.0,
        16.5,
        320.0,
        1.0,
        0.42,
        0.75,
        0.55,
        185.0,
        0.64,
        0.90,
        13,
        server.RADIATION_MUTATION_SOURCE_HASH,
    )
    radiation_path = root / "Dump_SHINOBU_324.bin"
    radiation_path.write_bytes(radiation)
    parsed_radiation = server.parse_dump_file(radiation_path)
    assert parsed_radiation["type"] == "radiation_mutation_blackbox"
    assert parsed_radiation["version"] == server.RADIATION_MUTATION_VERSION
    assert parsed_radiation["entrySize"] == server.RADIATION_MUTATION_ENTRY_BYTES
    assert parsed_radiation["nonEmptyEntryCount"] == 2
    assert parsed_radiation["latest"]["frame"] == 1702
    assert parsed_radiation["latest"]["flagLabels"] == [
        "active",
        "critical",
        "toxic-blood-vfx",
        "complex-noise-admitted",
        "metabolic-bridge-applied",
        "nonfinite-sanitized",
        "over-budget",
    ]
    assert parsed_radiation["latest"]["sourceHashHex"] == "0x53333234"
    assert parsed_radiation["latest"]["critical"] is True
    assert parsed_radiation["latest"]["mutationSeverity01"] == 1.0
    assert "critical" in parsed_radiation["warnings"]
    assert "nonfinite_sanitized" in parsed_radiation["warnings"]
    assert "over_budget" in parsed_radiation["warnings"]
    assert "mutation_severity_max" in parsed_radiation["warnings"]
    assert "fatal_dose_reached" in parsed_radiation["warnings"]
    assert "metabolic_toxicity" in parsed_radiation["warnings"]
    renamed_radiation_path = root / "Renamed_Header_S324MUTA.bin"
    renamed_radiation_path.write_bytes(radiation)
    assert server.parse_dump_file(renamed_radiation_path)["type"] == "radiation_mutation_blackbox"

    toxic_flags = (1 << 0) | (1 << 5) | (1 << 6) | (1 << 7)
    toxic_payload_bytes = 2 * server.TOXIC_OUTGASSING_ENTRY_BYTES
    toxic = bytearray(
        server.TOXIC_OUTGASSING_HEADER.pack(
            server.TOXIC_OUTGASSING_MAGIC,
            server.TOXIC_OUTGASSING_VERSION,
            server.TOXIC_OUTGASSING_HEADER_BYTES,
            server.TOXIC_OUTGASSING_ENTRY_BYTES,
            server.TOXIC_OUTGASSING_TELEMETRY_CAPACITY,
            2,
            2,
            toxic_payload_bytes,
        )
        + bytes(toxic_payload_bytes)
    )
    server.TOXIC_OUTGASSING_ENTRY.pack_into(
        toxic,
        server.TOXIC_OUTGASSING_HEADER_BYTES,
        1.0,
        2.0,
        3.0,
        0.12,
        5.5,
        0.8,
        0.45,
        0xABCDEF01,
        1901,
        16,
        2,
        3,
        1 << 0,
        0,
        0,
    )
    server.TOXIC_OUTGASSING_ENTRY.pack_into(
        toxic,
        server.TOXIC_OUTGASSING_HEADER_BYTES + server.TOXIC_OUTGASSING_ENTRY_BYTES,
        4.0,
        5.0,
        6.0,
        0.75,
        12.5,
        0.65,
        18.25,
        0xDEAD1234,
        1902,
        32,
        5,
        7,
        toxic_flags,
        1,
        0,
    )
    toxic_path = root / "Dump_TOXIC_SURGEON.bin"
    toxic_path.write_bytes(toxic)
    parsed_toxic = server.parse_dump_file(toxic_path)
    assert parsed_toxic["type"] == "toxic_outgassing_blackbox"
    assert parsed_toxic["version"] == server.TOXIC_OUTGASSING_VERSION
    assert parsed_toxic["entrySize"] == server.TOXIC_OUTGASSING_ENTRY_BYTES
    assert parsed_toxic["declaredCapacity"] == server.TOXIC_OUTGASSING_TELEMETRY_CAPACITY
    assert parsed_toxic["declaredEntryCount"] == 2
    assert parsed_toxic["latest"]["frame"] == 1902
    assert parsed_toxic["latest"]["activeResolution"] == 32
    assert parsed_toxic["latest"]["maxDensity"] == 0.75
    assert parsed_toxic["latest"]["stateHashHex"] == "0xDEAD1234"
    assert parsed_toxic["latest"]["flagLabels"] == [
        "mock-chemistry",
        "binary-probe-failure",
        "dump-failure",
        "nan",
    ]
    assert parsed_toxic["latest"]["nanDetected"] is True
    assert "nan_detected" in parsed_toxic["warnings"]
    assert "dump_failure" in parsed_toxic["warnings"]
    assert "binary_probe_failure" in parsed_toxic["warnings"]
    assert "mock_chemistry" in parsed_toxic["warnings"]
    renamed_toxic_path = root / "Renamed_ToxicMagic.bin"
    renamed_toxic_path.write_bytes(toxic)
    assert server.parse_dump_file(renamed_toxic_path)["type"] == "toxic_outgassing_blackbox"

    gas_flags = (1 << 1) | (1 << 2)
    gas_failure_flags = server.GAS_DYNAMICS_FAILURE_FLAG | 3
    gas = bytearray(
        server.GAS_DYNAMICS_HEADER.pack(
            server.GAS_DYNAMICS_MAGIC,
            server.GAS_DYNAMICS_VERSION,
            server.GAS_DYNAMICS_ENTRY_BYTES,
            server.GAS_DYNAMICS_TELEMETRY_CAPACITY,
            2,
            99,
        )
        + bytes(server.GAS_DYNAMICS_TELEMETRY_CAPACITY * server.GAS_DYNAMICS_ENTRY_BYTES)
    )
    server.GAS_DYNAMICS_ENTRY.pack_into(
        gas,
        server.GAS_DYNAMICS_HEADER_BYTES,
        (0x00012345 << 32) | 0x00000007,
        2001,
        8,
        150.0,
        5.25,
        612.5,
        105.75,
        0x13572468,
        74420,
        7,
        12,
        0,
        33.5,
        0,
        gas_flags,
        2,
    )
    server.GAS_DYNAMICS_ENTRY.pack_into(
        gas,
        server.GAS_DYNAMICS_HEADER_BYTES + server.GAS_DYNAMICS_ENTRY_BYTES,
        (0x00012345 << 32) | 0x00000007,
        2002,
        8,
        0.0,
        0.0,
        0.0,
        0.0,
        0x24681357,
        74420,
        7,
        13,
        1,
        12.5,
        0,
        gas_failure_flags,
        0,
    )
    gas_path = root / "Dump_1324_SubmarineAtmosphere.bin"
    gas_path.write_bytes(gas)
    parsed_gas = server.parse_dump_file(gas_path)
    assert parsed_gas["type"] == "gas_dynamics_blackbox"
    assert parsed_gas["version"] == server.GAS_DYNAMICS_VERSION
    assert parsed_gas["entrySize"] == server.GAS_DYNAMICS_ENTRY_BYTES
    assert parsed_gas["declaredEntryCount"] == server.GAS_DYNAMICS_TELEMETRY_CAPACITY
    assert parsed_gas["nonEmptyEntryCount"] == 2
    assert parsed_gas["latest"]["frame"] == 2002
    assert parsed_gas["latest"]["flagLabels"] == ["failure", "state-write-lock"]
    assert parsed_gas["latest"]["failureCode"] == 3
    assert parsed_gas["latest"]["nanDetected"] is False
    assert parsed_gas["entries"][0]["flagLabels"] == ["breach", "hibernating"]
    assert parsed_gas["entries"][0]["sleepingRoomCount"] == 2
    assert "failure" in parsed_gas["warnings"]
    assert "state_write_lock" in parsed_gas["warnings"]
    assert "breach" in parsed_gas["warnings"]
    assert "hibernating" in parsed_gas["warnings"]
    assert "dropped_updates" in parsed_gas["warnings"]
    renamed_gas_path = root / "Renamed_GasDynamicsMagic.bin"
    renamed_gas_path.write_bytes(gas)
    assert server.parse_dump_file(renamed_gas_path)["type"] == "gas_dynamics_blackbox"

    atmosphere_faults = (1 << 2) | (1 << 4) | (1 << 5) | (1 << 7)
    atmosphere = bytearray(
        server.BASE_ATMOSPHERE_LOGISTICS_HEADER.pack(
            server.BASE_ATMOSPHERE_LOGISTICS_MAGIC,
            server.BASE_ATMOSPHERE_LOGISTICS_VERSION,
            server.BASE_ATMOSPHERE_LOGISTICS_TELEMETRY_CAPACITY,
        )
        + bytes(
            server.BASE_ATMOSPHERE_LOGISTICS_TELEMETRY_CAPACITY
            * server.BASE_ATMOSPHERE_LOGISTICS_ENTRY_BYTES
        )
    )
    server.BASE_ATMOSPHERE_LOGISTICS_ENTRY.pack_into(
        atmosphere,
        server.BASE_ATMOSPHERE_LOGISTICS_HEADER_BYTES,
        0x1111222233334444,
        0.2095,
        0.00042,
        0.79008,
        0.0,
        20.0,
        3001,
        12,
        34,
        3,
        2,
        640,
        9,
        0,
        12000000,
    )
    server.BASE_ATMOSPHERE_LOGISTICS_ENTRY.pack_into(
        atmosphere,
        server.BASE_ATMOSPHERE_LOGISTICS_HEADER_BYTES + server.BASE_ATMOSPHERE_LOGISTICS_ENTRY_BYTES,
        0x5555666677778888,
        0.18,
        0.045,
        0.76,
        0.115,
        31.25,
        3002,
        12,
        34,
        3,
        4,
        980,
        16,
        atmosphere_faults,
        13100000,
    )
    atmosphere_path = root / "Dump_SHINOBU_221.bin"
    atmosphere_path.write_bytes(atmosphere)
    parsed_atmosphere = server.parse_dump_file(atmosphere_path)
    assert parsed_atmosphere["type"] == "base_atmosphere_logistics_blackbox"
    assert parsed_atmosphere["version"] == server.BASE_ATMOSPHERE_LOGISTICS_VERSION
    assert parsed_atmosphere["entrySize"] == server.BASE_ATMOSPHERE_LOGISTICS_ENTRY_BYTES
    assert parsed_atmosphere["declaredEntryCount"] == server.BASE_ATMOSPHERE_LOGISTICS_TELEMETRY_CAPACITY
    assert parsed_atmosphere["nonEmptyEntryCount"] == 2
    assert parsed_atmosphere["latest"]["frame"] == 3002
    assert parsed_atmosphere["latest"]["nodeCount"] == 12
    assert parsed_atmosphere["latest"]["sourceCount"] == 4
    assert parsed_atmosphere["latest"]["averageOxygen01"] == 0.18
    assert parsed_atmosphere["latest"]["maxToxin01"] == 0.115
    assert parsed_atmosphere["latest"]["faultLabels"] == [
        "nonfinite-gas",
        "csr-overflow",
        "source-overflow",
        "nan",
    ]
    assert "nonfinite_gas" in parsed_atmosphere["warnings"]
    assert "csr_overflow" in parsed_atmosphere["warnings"]
    assert "source_overflow" in parsed_atmosphere["warnings"]
    assert "nan_detected" in parsed_atmosphere["warnings"]
    renamed_atmosphere_path = root / "Renamed_BaseAtmosphereMagic.bin"
    renamed_atmosphere_path.write_bytes(atmosphere)
    assert server.parse_dump_file(renamed_atmosphere_path)["type"] == "base_atmosphere_logistics_blackbox"

    storm_flags = (1 << 0) | (1 << 1) | (1 << 2) | (1 << 3) | (1 << 4) | (1 << 5)
    storm = bytearray(
        server.STORM_PROPAGATION_HEADER.pack(
            server.STORM_PROPAGATION_MAGIC,
            storm_flags,
            1,
            server.STORM_PROPAGATION_TELEMETRY_CAPACITY,
            server.STORM_PROPAGATION_ENTRY_BYTES,
            server.STORM_PROPAGATION_SOURCE_HASH,
            0xABC12345,
            0,
        )
        + bytes(server.STORM_PROPAGATION_TELEMETRY_CAPACITY * server.STORM_PROPAGATION_ENTRY_BYTES)
    )
    server.STORM_PROPAGATION_ENTRY.pack_into(
        storm,
        server.STORM_PROPAGATION_HEADER_BYTES,
        4001,
        (1 << 1) | (1 << 2) | (1 << 5),
        0.42,
        250.0,
        0.29,
        1.5,
        0.67,
        0.22,
        0.1,
        0.0,
        -0.2,
        0.85,
        35.5,
        0.25,
        0x13572468,
        5,
    )
    server.STORM_PROPAGATION_ENTRY.pack_into(
        storm,
        server.STORM_PROPAGATION_HEADER_BYTES + server.STORM_PROPAGATION_ENTRY_BYTES,
        4002,
        storm_flags,
        0.91,
        1400.0,
        0.73,
        2.8,
        0.44,
        0.62,
        0.4,
        0.0,
        -0.6,
        0.65,
        52.25,
        0.42,
        0x24681357,
        8,
    )
    storm_path = root / "Dump_SHINOBU_234.bin"
    storm_path.write_bytes(storm)
    parsed_storm = server.parse_dump_file(storm_path)
    assert parsed_storm["type"] == "storm_propagation_blackbox"
    assert parsed_storm["entrySize"] == server.STORM_PROPAGATION_ENTRY_BYTES
    assert parsed_storm["declaredEntryCount"] == server.STORM_PROPAGATION_TELEMETRY_CAPACITY
    assert parsed_storm["sourceHashHex"] == "0x53483234"
    assert parsed_storm["stateHashHex"] == "0xABC12345"
    assert parsed_storm["reasonFlagLabels"] == [
        "nonfinite",
        "mock-weather",
        "fog",
        "biolum",
        "audio",
        "flow",
    ]
    assert parsed_storm["nonEmptyEntryCount"] == 2
    assert parsed_storm["latest"]["frame"] == 4002
    assert parsed_storm["latest"]["flagLabels"] == [
        "nonfinite",
        "mock-weather",
        "fog",
        "biolum",
        "audio",
        "flow",
    ]
    assert parsed_storm["latest"]["noiseOctaveCount"] == 8
    assert parsed_storm["latest"]["stateHashHex"] == "0x24681357"
    assert "nonfinite" in parsed_storm["warnings"]
    assert "mock_weather" in parsed_storm["warnings"]
    assert "fog_published" in parsed_storm["warnings"]
    assert "biolum_published" in parsed_storm["warnings"]
    assert "audio_published" in parsed_storm["warnings"]
    assert "flow_published" in parsed_storm["warnings"]
    renamed_storm_path = root / "Renamed_StormPropagationMagic.bin"
    renamed_storm_path.write_bytes(storm)
    assert server.parse_dump_file(renamed_storm_path)["type"] == "storm_propagation_blackbox"

    ocean = bytearray(
        server.OCEAN_SURFACE_ATMOSPHERE_HEADER.pack(
            server.OCEAN_SURFACE_ATMOSPHERE_MAGIC,
            server.OCEAN_SURFACE_ATMOSPHERE_MARKER,
            server.OCEAN_SURFACE_ATMOSPHERE_TELEMETRY_CAPACITY,
            server.OCEAN_SURFACE_ATMOSPHERE_ENTRY_BYTES,
            0xCAFEBABE,
            2,
            0,
            0,
        )
        + bytes(
            server.OCEAN_SURFACE_ATMOSPHERE_TELEMETRY_CAPACITY
            * server.OCEAN_SURFACE_ATMOSPHERE_ENTRY_BYTES
        )
    )
    server.OCEAN_SURFACE_ATMOSPHERE_ENTRY.pack_into(
        ocean,
        server.OCEAN_SURFACE_ATMOSPHERE_HEADER_BYTES,
        5001,
        0,
        1.25,
        0.35,
        120000,
        0.9,
        4,
        0.22,
        0.11,
        0.0,
        1.0,
        0.0,
        0x11112222,
        2,
        32,
    )
    server.OCEAN_SURFACE_ATMOSPHERE_ENTRY.pack_into(
        ocean,
        server.OCEAN_SURFACE_ATMOSPHERE_HEADER_BYTES + server.OCEAN_SURFACE_ATMOSPHERE_ENTRY_BYTES,
        5002,
        1 << 0,
        3.75,
        0.88,
        server.OCEAN_SURFACE_ATMOSPHERE_DUMP_BUDGET_NS + 10,
        0.65,
        6,
        0.71,
        0.49,
        0.1,
        0.98,
        -0.12,
        0x33334444,
        6,
        48,
    )
    ocean_path = root / "Dump_SHINOBU_147.bin"
    ocean_path.write_bytes(ocean)
    parsed_ocean = server.parse_dump_file(ocean_path)
    assert parsed_ocean["type"] == "ocean_surface_atmosphere_blackbox"
    assert parsed_ocean["entrySize"] == server.OCEAN_SURFACE_ATMOSPHERE_ENTRY_BYTES
    assert parsed_ocean["declaredEntryCount"] == server.OCEAN_SURFACE_ATMOSPHERE_TELEMETRY_CAPACITY
    assert parsed_ocean["stateHashHex"] == "0xCAFEBABE"
    assert parsed_ocean["telemetryCursor"] == 2
    assert parsed_ocean["nonEmptyEntryCount"] == 2
    assert parsed_ocean["latest"]["frame"] == 5002
    assert parsed_ocean["latest"]["flagLabels"] == ["latency-or-budget"]
    assert parsed_ocean["latest"]["waveComputeTimeNs"] == server.OCEAN_SURFACE_ATMOSPHERE_DUMP_BUDGET_NS + 10
    assert parsed_ocean["latest"]["readbackLatencyFrames"] == 6
    assert parsed_ocean["latest"]["stateHashHex"] == "0x33334444"
    assert "latency_or_budget" in parsed_ocean["warnings"]
    assert "readback_latency" in parsed_ocean["warnings"]
    assert "wave_compute_over_budget" in parsed_ocean["warnings"]
    renamed_ocean_path = root / "Renamed_OceanSurfaceMagic.bin"
    renamed_ocean_path.write_bytes(ocean)
    assert server.parse_dump_file(renamed_ocean_path)["type"] == "ocean_surface_atmosphere_blackbox"

    thermo_hazard_flags = (1 << 0) | (1 << 2) | (1 << 4)
    thermo_hazard = bytearray(
        server.THERMODYNAMICS_HAZARD_HEADER.pack(
            server.THERMODYNAMICS_HAZARD_MAGIC,
            server.THERMODYNAMICS_HAZARD_TELEMETRY_CAPACITY,
            server.THERMODYNAMICS_HAZARD_ENTRY_BYTES,
            2,
        )
        + bytes(server.THERMODYNAMICS_HAZARD_TELEMETRY_CAPACITY * server.THERMODYNAMICS_HAZARD_ENTRY_BYTES)
    )
    server.THERMODYNAMICS_HAZARD_ENTRY.pack_into(
        thermo_hazard,
        server.THERMODYNAMICS_HAZARD_HEADER_BYTES,
        42.0,
        0.25,
        1.5,
        10.0,
        20.0,
        30.0,
        6001,
        8,
        3,
        0,
        0,
        0xFFFFFFFF,
        32,
        0x10101010,
        220,
        200,
        0,
        0,
    )
    server.THERMODYNAMICS_HAZARD_ENTRY.pack_into(
        thermo_hazard,
        server.THERMODYNAMICS_HAZARD_HEADER_BYTES + server.THERMODYNAMICS_HAZARD_ENTRY_BYTES,
        185.5,
        4.25,
        6.75,
        40.0,
        50.0,
        60.0,
        6002,
        9,
        7,
        thermo_hazard_flags,
        5,
        123,
        16,
        0x20202020,
        128,
        64,
        0,
        0,
    )
    thermo_hazard_path = root / "Dump_THERMODYNAMICS.bin"
    thermo_hazard_path.write_bytes(thermo_hazard)
    parsed_thermo_hazard = server.parse_dump_file(thermo_hazard_path)
    assert parsed_thermo_hazard["type"] == "thermodynamics_hazard_blackbox"
    assert parsed_thermo_hazard["entrySize"] == server.THERMODYNAMICS_HAZARD_ENTRY_BYTES
    assert parsed_thermo_hazard["declaredEntryCount"] == server.THERMODYNAMICS_HAZARD_TELEMETRY_CAPACITY
    assert parsed_thermo_hazard["telemetryCursor"] == 2
    assert parsed_thermo_hazard["nonEmptyEntryCount"] == 2
    assert parsed_thermo_hazard["latest"]["frame"] == 6002
    assert parsed_thermo_hazard["latest"]["flagLabels"] == ["nan", "rebase", "signal-drop"]
    assert parsed_thermo_hazard["latest"]["gridOriginHashHex"] == "0x20202020"
    assert parsed_thermo_hazard["latest"]["qualityPressureQ8"] == 128
    assert parsed_thermo_hazard["latest"]["healthPressureQ8"] == 64
    assert "nan_detected" in parsed_thermo_hazard["warnings"]
    assert "rebase" in parsed_thermo_hazard["warnings"]
    assert "signal_drop" in parsed_thermo_hazard["warnings"]
    renamed_thermo_hazard_path = root / "Renamed_ThermoHazardMagic.bin"
    renamed_thermo_hazard_path.write_bytes(thermo_hazard)
    assert server.parse_dump_file(renamed_thermo_hazard_path)["type"] == "thermodynamics_hazard_blackbox"
    thermo_surgeon_headered_path = root / "Dump_THERMO_SURGEON.bin"
    thermo_surgeon_headered_path.write_bytes(thermo_hazard)
    assert server.parse_dump_file(thermo_surgeon_headered_path)["type"] == "thermodynamics_hazard_blackbox"

    abyssal_flags = (1 << 0) | (1 << 1) | (1 << 2) | (1 << 3) | (1 << 4) | (1 << 5)
    abyssal = bytearray(
        bytes(server.ABYSSAL_THERMODYNAMICS_TELEMETRY_CAPACITY * server.ABYSSAL_THERMODYNAMICS_ENTRY_BYTES)
    )
    server.ABYSSAL_THERMODYNAMICS_ENTRY.pack_into(
        abyssal,
        0,
        80.0,
        10000.0,
        9990.0,
        220.0,
        10.0,
        20.0,
        30.0,
        7001,
        1 << 2,
        3,
        6,
        0xFFFFFFFF,
        32,
    )
    server.ABYSSAL_THERMODYNAMICS_ENTRY.pack_into(
        abyssal,
        server.ABYSSAL_THERMODYNAMICS_ENTRY_BYTES,
        1250.0,
        20000.0,
        20575.0,
        980.0,
        40.0,
        50.0,
        60.0,
        7002,
        abyssal_flags,
        7,
        12,
        12345,
        16,
    )
    abyssal_path = root / "Dump_SHINOBU_203.bin"
    abyssal_path.write_bytes(abyssal)
    parsed_abyssal = server.parse_dump_file(abyssal_path)
    assert parsed_abyssal["type"] == "abyssal_thermodynamics_blackbox"
    assert parsed_abyssal["entrySize"] == server.ABYSSAL_THERMODYNAMICS_ENTRY_BYTES
    assert parsed_abyssal["declaredEntryCount"] == server.ABYSSAL_THERMODYNAMICS_TELEMETRY_CAPACITY
    assert parsed_abyssal["nonEmptyEntryCount"] == 2
    assert parsed_abyssal["latest"]["frame"] == 7002
    assert parsed_abyssal["latest"]["flagLabels"] == [
        "nan",
        "shift",
        "mock-sources",
        "energy-drift",
        "divergent",
        "max-iterations",
    ]
    assert parsed_abyssal["latest"]["energyDelta"] == 575.0
    assert parsed_abyssal["latest"]["nanCellIndex"] == 12345
    assert "nan_detected" in parsed_abyssal["warnings"]
    assert "shift" in parsed_abyssal["warnings"]
    assert "mock_sources" in parsed_abyssal["warnings"]
    assert "energy_drift" in parsed_abyssal["warnings"]
    assert "divergent" in parsed_abyssal["warnings"]
    assert "max_iterations" in parsed_abyssal["warnings"]
    thermo_surgeon_headered_path.write_bytes(abyssal)
    assert server.parse_dump_file(thermo_surgeon_headered_path)["type"] == "abyssal_thermodynamics_blackbox"

    respawn_flags = (
        (1 << 0)
        | (1 << 2)
        | (1 << 4)
        | (1 << 5)
        | (1 << 6)
        | (1 << 10)
        | (3 << server.RESPAWN_RECONCILIATION_DROPPED_ITEM_SHIFT)
        | (1 << 31)
    )
    respawn = bytearray(
        server.RESPAWN_RECONCILIATION_HEADER.pack(
            server.RESPAWN_RECONCILIATION_MAGIC,
            server.RESPAWN_RECONCILIATION_VERSION,
            server.RESPAWN_RECONCILIATION_TELEMETRY_CAPACITY,
            14,
            (1 << 5) | (1 << 31),
        )
        + bytes(server.RESPAWN_RECONCILIATION_TELEMETRY_CAPACITY * server.RESPAWN_RECONCILIATION_ENTRY_BYTES)
    )
    server.RESPAWN_RECONCILIATION_ENTRY.pack_into(
        respawn,
        server.RESPAWN_RECONCILIATION_HEADER_BYTES + 13 * server.RESPAWN_RECONCILIATION_ENTRY_BYTES,
        100.0,
        200.0,
        300.0,
        110.0,
        210.0,
        310.0,
        0xDEAD0001,
        1801,
        64.5,
        (1 << 0) | (1 << 1),
    )
    server.RESPAWN_RECONCILIATION_ENTRY.pack_into(
        respawn,
        server.RESPAWN_RECONCILIATION_HEADER_BYTES + 14 * server.RESPAWN_RECONCILIATION_ENTRY_BYTES,
        101.0,
        201.0,
        301.0,
        0.0,
        0.0,
        0.0,
        0xDEAD0002,
        1802,
        155.25,
        respawn_flags,
    )
    respawn_path = root / "Dump_SHINOBU_329.bin"
    respawn_path.write_bytes(respawn)
    parsed_respawn = server.parse_dump_file(respawn_path)
    assert parsed_respawn["type"] == "respawn_reconciliation_blackbox"
    assert parsed_respawn["version"] == server.RESPAWN_RECONCILIATION_VERSION
    assert parsed_respawn["entrySize"] == server.RESPAWN_RECONCILIATION_ENTRY_BYTES
    assert parsed_respawn["nonEmptyEntryCount"] == 2
    assert parsed_respawn["reasonFlagLabels"] == ["invalid-target-aup", "nan-detected"]
    assert parsed_respawn["latest"]["frame"] == 1802
    assert parsed_respawn["latest"]["causeHashHex"] == "0xDEAD0002"
    assert parsed_respawn["latest"]["droppedItemCount"] == 3
    assert parsed_respawn["latest"]["flagLabels"] == [
        "respawn-active",
        "penalty-applied",
        "fallback-lifepod",
        "invalid-target-aup",
        "committed",
        "death-sequence-blackout-primed",
        "nan-detected",
    ]
    assert parsed_respawn["latest"]["nanDetected"] is True
    assert parsed_respawn["latest"]["fallbackLifepod"] is True
    assert "reason_flags" in parsed_respawn["warnings"]
    assert "nan_detected" in parsed_respawn["warnings"]
    assert "invalid_target_aup" in parsed_respawn["warnings"]
    assert "fallback_lifepod" in parsed_respawn["warnings"]
    assert "penalty_applied" in parsed_respawn["warnings"]
    assert "committed" in parsed_respawn["warnings"]
    assert "dropped_items" in parsed_respawn["warnings"]
    renamed_respawn_path = root / "Renamed_Header_RSPNSRGE.bin"
    renamed_respawn_path.write_bytes(respawn)
    assert server.parse_dump_file(renamed_respawn_path)["type"] == "respawn_reconciliation_blackbox"

    pda_frequency = bytearray(
        server.PDA_FREQUENCY_TUNING_HEADER.pack(server.PDA_FREQUENCY_TUNING_TELEMETRY_CAPACITY, 2)
        + bytes(server.PDA_FREQUENCY_TUNING_TELEMETRY_CAPACITY * server.PDA_FREQUENCY_TUNING_ENTRY_BYTES)
    )
    server.PDA_FREQUENCY_TUNING_ENTRY.pack_into(
        pda_frequency,
        server.PDA_FREQUENCY_TUNING_HEADER_BYTES,
        820,
        0x534F5648,
        1.25,
        0.42,
        1.22,
        0.40,
        0.08,
        250,
        0,
        1 << 0,
    )
    server.PDA_FREQUENCY_TUNING_ENTRY.pack_into(
        pda_frequency,
        server.PDA_FREQUENCY_TUNING_HEADER_BYTES + server.PDA_FREQUENCY_TUNING_ENTRY_BYTES,
        821,
        0x534F5648,
        2.10,
        0.65,
        2.10,
        0.65,
        0.01,
        1000,
        2,
        (1 << 0) | (1 << 1) | (1 << 2),
    )
    pda_frequency_path = root / "Dump_MINIGAME_FREQUENCY_TUNING.bin"
    pda_frequency_path.write_bytes(pda_frequency)
    parsed_pda_frequency = server.parse_dump_file(pda_frequency_path)
    assert parsed_pda_frequency["type"] == "pda_frequency_tuning_blackbox"
    assert parsed_pda_frequency["latest"]["frame"] == 821
    assert parsed_pda_frequency["latest"]["artifactHashHex"] == "0x534F5648"
    assert parsed_pda_frequency["latest"]["stage"] == 2
    assert parsed_pda_frequency["latest"]["holdPermille"] == 1000
    assert parsed_pda_frequency["latest"]["flagLabels"] == ["stage-0-locked", "stage-1-locked", "stage-2-locked"]
    assert parsed_pda_frequency["latest"]["allStagesLocked"] is True
    assert "all_stages_locked" in parsed_pda_frequency["warnings"]

    compass_flags = (1 << 1) | (1 << 2) | (1 << 5) | (1 << 6) | (1 << 9)
    compass = bytearray(
        server.COMPASS_GYRO_HEADER.pack(server.COMPASS_GYRO_MAGIC, server.COMPASS_GYRO_BLACKBOX_CAPACITY, 1)
        + bytes(server.COMPASS_GYRO_BLACKBOX_CAPACITY * server.COMPASS_GYRO_ENTRY_BYTES)
    )
    server.COMPASS_GYRO_ENTRY.pack_into(
        compass,
        server.COMPASS_GYRO_HEADER_BYTES,
        900,
        181.5,
        180.0,
        1.5,
        3.0,
        0.25,
        0.95,
        1 << 1,
        17,
        2,
    )
    server.COMPASS_GYRO_ENTRY.pack_into(
        compass,
        server.COMPASS_GYRO_HEADER_BYTES + server.COMPASS_GYRO_ENTRY_BYTES,
        901,
        90.0,
        102.5,
        12.5,
        8.0,
        0.92,
        0.44,
        compass_flags,
        19,
        3,
    )
    compass_path = root / "Dump_COMPASS_GYRO_STABILIZER.bin"
    compass_path.write_bytes(compass)
    parsed_compass = server.parse_dump_file(compass_path)
    assert parsed_compass["type"] == "compass_gyro_blackbox"
    assert parsed_compass["latest"]["frame"] == 901
    assert parsed_compass["latest"]["flagLabels"] == [
        "powered",
        "anomaly-unstable",
        "nonfinite-fallback",
        "reduced-quality-noise",
        "calibration-requested",
    ]
    assert parsed_compass["latest"]["calibrationCount"] == 3
    assert "nonfinite" in parsed_compass["warnings"]
    assert "anomaly_unstable" in parsed_compass["warnings"]
    assert "reduced_quality_noise" in parsed_compass["warnings"]
    assert "drift_over_max" in parsed_compass["warnings"]

    pda_encyclopedia = bytearray(
        server.PDA_ENCYCLOPEDIA_HEADER.pack(
            server.PDA_ENCYCLOPEDIA_MAGIC,
            911,
            0x54455854,
            server.PDA_ENCYCLOPEDIA_TELEMETRY_CAPACITY,
            server.PDA_ENCYCLOPEDIA_ENTRY_BYTES,
            0xAEC57EAC,
        )
        + bytes(server.PDA_ENCYCLOPEDIA_TELEMETRY_CAPACITY * server.PDA_ENCYCLOPEDIA_ENTRY_BYTES)
    )
    pda_encyclopedia_flags = 5 | (4 << 8) | server.PDA_ENCYCLOPEDIA_CANVAS_SPLIT_FLAG
    pda_encyclopedia_fields = (
        910,
        0,
        0xAEC57EAC,
        12,
        32,
        256,
        512,
        2048,
        1200,
        1500,
        pda_encyclopedia_flags,
        0x54455854,
        256,
        4096,
    )
    pda_encyclopedia_hash = server.compute_pda_encyclopedia_state_hash(pda_encyclopedia_fields)
    server.PDA_ENCYCLOPEDIA_ENTRY.pack_into(
        pda_encyclopedia,
        server.PDA_ENCYCLOPEDIA_HEADER_BYTES,
        *(
            pda_encyclopedia_fields[0],
            pda_encyclopedia_hash,
            *pda_encyclopedia_fields[2:],
        ),
    )
    pda_encyclopedia_path = root / "Dump_PDAEncyclopediaStreamer_BlackBox.bin"
    pda_encyclopedia_path.write_bytes(pda_encyclopedia)
    parsed_pda_encyclopedia = server.parse_dump_file(pda_encyclopedia_path)
    assert parsed_pda_encyclopedia["type"] == "pda_encyclopedia_blackbox"
    assert parsed_pda_encyclopedia["latest"]["frame"] == 910
    assert parsed_pda_encyclopedia["latest"]["stateHashOk"] is True
    assert parsed_pda_encyclopedia["latest"]["entryHashHex"] == "0xAEC57EAC"
    assert parsed_pda_encyclopedia["latest"]["streamStateLabel"] == "fault"
    assert parsed_pda_encyclopedia["latest"]["sourceLabel"] == "data-monolith"
    assert parsed_pda_encyclopedia["latest"]["flagLabels"] == [
        "stream-fault",
        "source-data-monolith",
        "canvas-split",
    ]
    assert "fault_hash" in parsed_pda_encyclopedia["warnings"]
    assert "stream_fault" in parsed_pda_encyclopedia["warnings"]

    habitat_flags = (1 << 0) | (1 << 1) | (1 << 3) | (1 << 4)
    habitat = bytearray(
        server.HABITAT_FLOOD_HEADER.pack(
            server.HABITAT_FLOOD_MAGIC,
            server.HABITAT_FLOOD_VERSION,
            server.HABITAT_FLOOD_BLACKBOX_CAPACITY,
            2,
            habitat_flags,
        )
        + bytes(server.HABITAT_FLOOD_BLACKBOX_CAPACITY * server.HABITAT_FLOOD_ENTRY_BYTES)
    )
    server.HABITAT_FLOOD_ENTRY.pack_into(
        habitat,
        server.HABITAT_FLOOD_HEADER_BYTES,
        920,
        8,
        10,
        1,
        0,
        0.35,
        0.40,
        12.5,
        0.20,
        1 << 1,
        0x13060001,
        4,
    )
    server.HABITAT_FLOOD_ENTRY.pack_into(
        habitat,
        server.HABITAT_FLOOD_HEADER_BYTES + server.HABITAT_FLOOD_ENTRY_BYTES,
        921,
        8,
        10,
        3,
        0,
        0.90,
        1.15,
        48.0,
        0.88,
        habitat_flags,
        0x13060002,
        5,
    )
    habitat_path = root / "Dump_1306_Construction_HabitatIntegrity.bin"
    habitat_path.write_bytes(habitat)
    parsed_habitat = server.parse_dump_file(habitat_path)
    assert parsed_habitat["type"] == "habitat_flood_blackbox"
    assert parsed_habitat["mode"] == "habitat_integrity"
    assert parsed_habitat["version"] == server.HABITAT_FLOOD_VERSION
    assert parsed_habitat["latest"]["frame"] == 921
    assert parsed_habitat["latest"]["flagLabels"] == [
        "nonfinite",
        "overflow-clamped",
        "topology-invalid",
        "module-stress-invalid",
    ]
    assert parsed_habitat["latest"]["stateHashHex"] == "0x13060002"
    assert parsed_habitat["latest"]["floodedRoomCount"] == 3
    assert "nonfinite" in parsed_habitat["warnings"]
    assert "overflow_clamped" in parsed_habitat["warnings"]
    assert "topology_invalid" in parsed_habitat["warnings"]
    assert "module_stress_invalid" in parsed_habitat["warnings"]
    assert "water_level_over_one" in parsed_habitat["warnings"]

    construction_validation_flags = (1 << 0) | (1 << 1) | (1 << 4) | (1 << 6) | (1 << 7)
    construction_validation = server.CONSTRUCTION_VALIDATION_ENTRY.pack(
        10.0,
        20.0,
        30.0,
        1,
        2,
        3,
        930,
        1 << 3,
        0.35,
        0.18,
        1,
        0,
        0x1306CAFE,
    )
    construction_validation += server.CONSTRUCTION_VALIDATION_ENTRY.pack(
        11.0,
        21.0,
        31.0,
        2,
        3,
        4,
        931,
        construction_validation_flags,
        -0.25,
        0.52,
        1,
        2,
        0x1306BEEF,
    )
    construction_validation_path = root / "Dump_1306_ConstructionValidation.bin"
    construction_validation_path.write_bytes(construction_validation)
    parsed_construction_validation = server.parse_dump_file(construction_validation_path)
    assert parsed_construction_validation["type"] == "construction_validation_blackbox"
    assert parsed_construction_validation["latest"]["frame"] == 931
    assert parsed_construction_validation["latest"]["flagLabels"] == [
        "occupied-grid-cell",
        "terrain-intersection",
        "nonfinite-input",
        "graph-capacity",
        "disconnected-wing",
    ]
    assert parsed_construction_validation["latest"]["resultHashHex"] == "0x1306BEEF"
    assert parsed_construction_validation["latest"]["graphSplices"] == 2
    assert "nonfinite" in parsed_construction_validation["warnings"]
    assert "terrain_intersection" in parsed_construction_validation["warnings"]
    assert "occupied_grid_cell" in parsed_construction_validation["warnings"]
    assert "graph_route_fault" in parsed_construction_validation["warnings"]

    construction_socket_flags = (1 << 3) | (1 << 4) | (1 << 5) | (1 << 7) | (1 << 8) | (1 << 10)
    construction_socket = server.CONSTRUCTION_SOCKET_ENTRY.pack(
        100.0,
        200.0,
        300.0,
        940,
        16,
        24,
        2,
        120.0,
        0.045,
        1 << 5,
        0x1306A001,
        0.85,
        7,
    )
    construction_socket += server.CONSTRUCTION_SOCKET_ENTRY.pack(
        101.0,
        201.0,
        301.0,
        941,
        18,
        36,
        0,
        900.0,
        9.5,
        construction_socket_flags,
        0x1306A002,
        0.35,
        8,
    )
    construction_socket_path = root / "Dump_1306_Construction_SocketTelemetry.bin"
    construction_socket_path.write_bytes(construction_socket)
    parsed_construction_socket = server.parse_dump_file(construction_socket_path)
    assert parsed_construction_socket["type"] == "construction_socket_blackbox"
    assert parsed_construction_socket["latest"]["frame"] == 941
    assert parsed_construction_socket["latest"]["flagLabels"] == [
        "collision-blocked",
        "nonfinite",
        "valid-snap",
        "topology-dirty",
        "rollback-fence",
        "capacity-exceeded",
    ]
    assert parsed_construction_socket["latest"]["resultHashHex"] == "0x1306A002"
    assert "nonfinite" in parsed_construction_socket["warnings"]
    assert "collision_blocked" in parsed_construction_socket["warnings"]
    assert "capacity_exceeded" in parsed_construction_socket["warnings"]
    assert "rollback_fence" in parsed_construction_socket["warnings"]
    assert "topology_dirty" in parsed_construction_socket["warnings"]
    assert "solver_over_500us" in parsed_construction_socket["warnings"]

    construction_holography_flags = (1 << 0) | (1 << 3) | (1 << 4) | (1 << 5) | (1 << 6) | (1 << 9)
    construction_holography = server.CONSTRUCTION_HOLOGRAPHY_ENTRY.pack(
        120.0,
        220.0,
        320.0,
        950,
        0xB1710001,
        8,
        (1 << 0) | (1 << 1) | (1 << 2),
        180.0,
        0.25,
        0x1306C001,
        0.9,
    )
    construction_holography += server.CONSTRUCTION_HOLOGRAPHY_ENTRY.pack(
        121.0,
        221.0,
        321.0,
        951,
        0xB1710002,
        8,
        construction_holography_flags,
        650.0,
        -0.15,
        0x1306C002,
        0.45,
    )
    construction_holography_path = root / "Dump_1306_Construction_Holography.bin"
    construction_holography_path.write_bytes(construction_holography)
    parsed_construction_holography = server.parse_dump_file(construction_holography_path)
    assert parsed_construction_holography["type"] == "construction_holography_blackbox"
    assert parsed_construction_holography["latest"]["frame"] == 951
    assert parsed_construction_holography["latest"]["flagLabels"] == [
        "active",
        "sdf-blocked",
        "bounds-blocked",
        "nonfinite",
        "socket-snap",
        "rollback-excluded",
    ]
    assert parsed_construction_holography["latest"]["prefabHashHex"] == "0xB1710002"
    assert parsed_construction_holography["latest"]["validationStateHashHex"] == "0x1306C002"
    assert "nonfinite" in parsed_construction_holography["warnings"]
    assert "sdf_blocked" in parsed_construction_holography["warnings"]
    assert "bounds_blocked" in parsed_construction_holography["warnings"]
    assert "rollback_excluded" in parsed_construction_holography["warnings"]
    assert "solver_over_500us" in parsed_construction_holography["warnings"]

    defrag = server.DEFRAG_ENTRY_PACK1.pack(1, 2, 3, 100, 64, 16, 32, 0, 0.25, 1, 5, 1, 0, 0)
    (root / "Dump_A_MEMORY_DEFRAGMENTATION_OVERSEER.bin").write_bytes(defrag)

    thermal = server.THERMAL_HEADER.pack(7, 0)
    thermal += server.THERMAL_ENTRY_MANUAL.pack(100, 7, 3, 430, 2, 77, 1, 3, 9)
    (root / "Dump_THERMAL_THROTTLING_DIRECTOR.bin").write_bytes(thermal)

    biomass = server.BIOMASS_HEADER.pack(server.BIOMASS_MAGIC, 1, server.BIOMASS_ENTRY.size, 0, 300)
    biomass += server.BIOMASS_ENTRY.pack(12, 99, 4, 0, 8.0, 5.0, 3.0, 0.4)
    (root / "Dump_ECOLOGICAL_BIOMASS_ENGINE.bin").write_bytes(biomass)

    macro = server.BIOMASS_HEADER.pack(server.MACRO_SWARM_MAGIC, 1, server.MACRO_SWARM_ENTRY.size, 0, 300)
    macro += server.MACRO_SWARM_ENTRY.pack(22, 101, 3, 2, 9.5, 1, 0, 0)
    (root / "Dump_SWARM_MACRO_MIGRATION_DIRECTOR.bin").write_bytes(macro)

    mutation = server.BIOMASS_HEADER.pack(server.FAUNA_MUTATION_MAGIC, 1, server.FAUNA_MUTATION_ENTRY.size, 0, 300)
    mutation += server.FAUNA_MUTATION_ENTRY.pack(23, 102, 7, 4, 3, 5, 0.25, 0.5, 0.75, 0, 0)
    (root / "Dump_ECOLOGY_MUTATION_DIRECTOR.bin").write_bytes(mutation)

    live = server.LIVE_TELEMETRY_ENTRY.pack(
        server.LIVE_TELEMETRY_MAGIC,
        2,
        server.LIVE_TELEMETRY_ENTRY.size,
        333,
        12,
        64,
        17.25,
        0.016,
        2048.0,
        3.5,
        6.25,
        0x123,
        0x40,
        0x77,
        9,
        331,
    )
    (root / "runtime_telemetry.bin").write_bytes(live)
    live_parsed = server.parse_live_telemetry(root / "runtime_telemetry.bin", live)
    assert live_parsed["entrySize"] == 64
    assert live_parsed["latest"]["latencyMs"] == 3.5
    assert live_parsed["latest"]["gpuFrameTimeMs"] == 6.25
    assert live_parsed["latest"]["systemMask"] == 0x123
    legacy_live = server.LIVE_TELEMETRY_ENTRY_V1.pack(server.LIVE_TELEMETRY_MAGIC, 1, 333, 12, 64, 17.25, 0.016, 2048.0)
    legacy_parsed = server.parse_live_telemetry(root / "legacy_runtime_telemetry.bin", legacy_live)
    assert legacy_parsed["entrySize"] == server.LIVE_TELEMETRY_ENTRY_V1.size
    assert "legacy_v1_32_byte_record" in legacy_parsed["warnings"]

    headless = server.HEADLESS_HEADER.pack(server.HEADLESS_MAGIC, 1, server.HEADLESS_ENTRY.size, 1)
    headless += server.HEADLESS_ENTRY.pack(200, 4, 55, 1, 2, 3, 0.1, 0.2, 0.3, 6.0, 2.0, 128.0, 0)
    assert server.parse_headless_blackbox(root / "Dump_HEADLESS.bin", headless)["latest"]["predator"] == 2.0
    assert len(server.cap_entries([{"i": i} for i in range(server.MAX_DUMP_ENTRIES + 1)])) == server.MAX_DUMP_ENTRIES

    memory_text = root / "Dump_CORE_DATA_VAULT_WARDEN.txt"
    memory_text.write_text(
        "H8MEMORY_ALLOCATION_TABLE\n"
        "TotalBytes=256\n"
        "ActiveAllocationCount=1\n"
        "Index=0 Ptr=4096 Bytes=64 Owner=1 Allocator=4 Flags=1\n",
        encoding="utf-8",
    )
    assert server.parse_h8memory_text(memory_text)["memoryMap"][-1]["state"] == "free"

    csv_path = root / "QA_Endurance_Log.csv"
    with csv_path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(
            handle,
            fieldnames=["frame", "avgFps", "PreyBiomass", "PredatorBiomass", "HardwareThermalSeverity", "BatteryPercent"],
        )
        writer.writeheader()
        writer.writerow(
            {
                "frame": "1",
                "avgFps": "60",
                "PreyBiomass": "4.5",
                "PredatorBiomass": "1.2",
                "HardwareThermalSeverity": "2",
                "BatteryPercent": "66",
            }
        )
    assert round(server.parse_csv_file(csv_path, "QA_Endurance_Log.csv")["frameSeries"][0]["frameTimeMs"], 3) == 16.667

    hphi_path = root / "HECTON_PHI_REPORT.md"
    hphi_path.write_text("Date: 2026-05-13\nH-Phi_static = 0.973 * 0.996 * 0.001 * 0.535 = 0.00062\n", encoding="utf-8")
    assert server.parse_hphi_report(hphi_path)["value"] == 0.00062

    missing_logs = root / "MissingAgentLogs"
    old_logs = server.AGENT_LOGS
    server.AGENT_LOGS = missing_logs
    try:
        missing_data = server.collect_dumps()
    finally:
        server.AGENT_LOGS = old_logs
    assert missing_data["files"] == []
    assert not missing_logs.exists()

    server.AGENT_LOGS = root
    original_parse_dump_file = server.parse_dump_file

    def fail_player_dump(path: Path) -> dict[str, object]:
        if path.name == "Dump_PLAYER_KINEMATICS.bin":
            raise ValueError("forced parser failure")
        return original_parse_dump_file(path)

    server.parse_dump_file = fail_player_dump
    try:
        fault_data = server.collect_dumps()
    finally:
        server.parse_dump_file = original_parse_dump_file
        server.AGENT_LOGS = old_logs
    assert any(
        file["name"] == "Dump_PLAYER_KINEMATICS.bin" and file["type"] == "parse_failed"
        for file in fault_data["files"]
    )
    assert any(file["type"] == "live_telemetry" for file in fault_data["files"])

    server.AGENT_LOGS = root
    try:
        dump_data = server.collect_dumps()
    finally:
        server.AGENT_LOGS = old_logs

    parsed_types = {file["type"] for file in dump_data["files"]}
    assert "macro_swarm" in parsed_types
    assert "fauna_mutation" in parsed_types
    assert "live_telemetry" in parsed_types
    assert "crash_telemetry_buffer" in parsed_types
    assert "simulation_bucket_blackbox" in parsed_types
    assert "terrain_streaming_pager" in parsed_types
    assert "world_chunk_residency_blackbox" in parsed_types
    assert "global_telemetry_bus_blackbox" in parsed_types
    assert "foveated_simulation_blackbox" in parsed_types
    assert "input_determinism_blackbox" in parsed_types
    assert "origin_shift_blackbox" in parsed_types
    assert "binary_layout_sentinel" in parsed_types
    assert "terminal_os_blackbox" in parsed_types
    assert "terminal_decryption_blackbox" in parsed_types
    assert "terminal_projection_blackbox" in parsed_types
    assert "openxr_manual_override_blackbox" in parsed_types
    assert "vehicle_damage_holographer_blackbox" in parsed_types
    assert "pda_projection_blackbox" in parsed_types
    assert "wrist_hud_blackbox" in parsed_types
    assert "ladder_climb_ik_blackbox" in parsed_types
    assert "topographical_sonar_blackbox" in parsed_types
    assert "kinetic_character_blackbox" in parsed_types
    assert "procedural_bone_blackbox" in parsed_types
    assert "vr_somatic_blackbox" in parsed_types
    assert "lockstep_state_validator_blackbox" in parsed_types
    assert "voxel_astar_blackbox" in parsed_types
    assert "path_funnel_blackbox" in parsed_types
    assert "laser_cutter_dod_blackbox" in parsed_types
    assert "wfc_laser_cut_blackbox" in parsed_types
    assert "tool_kinematics_blackbox" in parsed_types
    assert "auxiliary_equipment_blackbox" in parsed_types
    assert "upgrade_matrix_blackbox" in parsed_types
    assert "metabolism_blackbox" in parsed_types
    assert "physiology_autopsy_blackbox" in parsed_types
    assert "sensory_impairment_blackbox" in parsed_types
    assert "suit_integrity_blackbox" in parsed_types
    assert "radiation_mutation_blackbox" in parsed_types
    assert "respawn_reconciliation_blackbox" in parsed_types
    assert "pda_frequency_tuning_blackbox" in parsed_types
    assert "compass_gyro_blackbox" in parsed_types
    assert "pda_encyclopedia_blackbox" in parsed_types
    assert "habitat_flood_blackbox" in parsed_types
    assert "construction_validation_blackbox" in parsed_types
    assert "construction_socket_blackbox" in parsed_types
    assert "construction_holography_blackbox" in parsed_types
    assert len(dump_data["frameSeries"]) >= 3
    assert any(point["jitterMs"] == 10.0 for point in dump_data["frameSeries"])
    assert any(point["source"] == "runtime_telemetry.bin" for point in dump_data["frameSeries"])
    assert dump_data["latestThermal"]["batteryPercent"] == 77
    assert dump_data["jobAdmission"]["deniedCount"] == 1
    assert dump_data["jobAdmission"]["insufficientBudgetCount"] == 1
    assert dump_data["jobAdmission"]["stateHashMismatchCount"] == 0
    assert dump_data["jobAdmission"]["legacyStarvedCount"] == 1
    assert dump_data["jobAdmission"]["latest"]["source"] == "Dump_SIMULATION_BUCKET_DISTRIBUTOR_JobAdmission_LegacyV1.bin"
    assert dump_data["jobAdmission"]["latest"]["frame"] == 78
    assert dump_data["ecologySeries"]
    assert dump_data["memoryMaps"]
    assert dump_data["memoryMaps"][0]["name"] == "Dump_CORE_DATA_VAULT_WARDEN.txt"
    assert dump_data["memoryMaps"][0]["estimated"] is False

    index_text = (Path(__file__).with_name("index.html")).read_text(encoding="utf-8")
    for required in (
        "function normalizeSummary",
        "function normalizeMemoryMap",
        "function objectArray",
        "function updateJobAdmission",
        "crash_telemetry_buffer",
        "simulation_bucket_blackbox",
        "terrain_streaming_pager",
        "world_chunk_residency_blackbox",
        "global_telemetry_bus_blackbox",
        "foveated_simulation_blackbox",
        "input_determinism_blackbox",
        "origin_shift_blackbox",
        "binary_layout_sentinel",
        "terminal_os_blackbox",
        "terminal_decryption_blackbox",
        "terminal_projection_blackbox",
        "openxr_manual_override_blackbox",
        "vehicle_damage_holographer_blackbox",
        "pda_projection_blackbox",
        "wrist_hud_blackbox",
        "ladder_climb_ik_blackbox",
        "topographical_sonar_blackbox",
        "kinetic_character_blackbox",
        "procedural_bone_blackbox",
        "vr_somatic_blackbox",
        "lockstep_state_validator_blackbox",
        "voxel_astar_blackbox",
        "path_funnel_blackbox",
        "laser_cutter_dod_blackbox",
        "wfc_laser_cut_blackbox",
        "tool_kinematics_blackbox",
        "auxiliary_equipment_blackbox",
        "upgrade_matrix_blackbox",
        "metabolism_blackbox",
        "physiology_autopsy_blackbox",
        "sensory_impairment_blackbox",
        "suit_integrity_blackbox",
        "radiation_mutation_blackbox",
        "respawn_reconciliation_blackbox",
        "pda_frequency_tuning_blackbox",
        "compass_gyro_blackbox",
        "pda_encyclopedia_blackbox",
        "habitat_flood_blackbox",
        "construction_validation_blackbox",
        "construction_socket_blackbox",
        "construction_holography_blackbox",
        "framePacingFlagLabels",
        "faultLabels",
        "reasonLabels",
        "jobAdmissionText",
        "if (!response.ok)",
    ):
        assert required in index_text
    for forbidden in ("innerHTML", "eval(", "new Function", "document.write", "console.log", "debugger"):
        assert forbidden not in index_text

    degraded = server.build_degraded_summary(RuntimeError("forced failure"))
    assert degraded["status"] == "DASHBOARD DEGRADED"
    assert degraded["csv"]["sources"] == []
    assert degraded["dumps"]["files"] == []
    assert degraded["jobAdmission"]["deniedCount"] == 0
    assert degraded["errors"][0]["type"] == "RuntimeError"

    original_build_summary = server.build_summary

    def raise_summary() -> dict[str, object]:
        raise RuntimeError("forced route failure")

    server.build_summary = raise_summary
    try:
        response = server.api_summary()
    finally:
        server.build_summary = original_build_summary
    routed_payload = json.loads(response.body)
    assert response.status_code == 200
    assert routed_payload["status"] == "DASHBOARD DEGRADED"
    assert routed_payload["errors"][0]["type"] == "RuntimeError"
    assert response.headers["cache-control"] == "no-store, max-age=0"
    assert response.headers["pragma"] == "no-cache"
    assert response.headers["x-content-type-options"] == "nosniff"

    assert server.index().headers["cache-control"] == "no-store, max-age=0"
    health_response = server.api_health()
    health_payload = json.loads(health_response.body)
    assert health_payload["status"] == "ok"
    assert health_response.headers["x-content-type-options"] == "nosniff"

    print("telemetry dashboard smoke ok")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
