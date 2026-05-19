#!/usr/bin/env python3
"""Static guard for the C# V10 save master hash helper.

Cold-path tooling only. This script does not compile Unity code; it checks that
the hand-written C# helper still matches the Python replay oracle's domain
bytes, header offsets, and layout sentinels.
"""

from __future__ import annotations

import importlib.util
import re
import sys
from pathlib import Path


EXPECTED_CONSTANTS = {
    "HeaderVersion": "0x000A",
    "HeaderSizeBytes": "72",
    "MasterStateHashLoOffset": "56",
    "MasterStateHashHiOffset": "64",
    "MasterPreimageBytes": "96",
    "MasterLoHashBytes": "MasterPreimageBytes + 3",
    "MasterHiHashBytes": "MasterPreimageBytes + 11",
    "ShuffleMaskLoBytes": "36",
    "ShuffleMaskHiBytes": "44",
}

EXPECTED_MANIFEST_LINES = (
    "AssertSize<SaveMasterHashV10Result>(32)",
    "AssertOffset<SaveMasterHashV10Result>(nameof(SaveMasterHashV10Result.PlainLo), 0)",
    "AssertOffset<SaveMasterHashV10Result>(nameof(SaveMasterHashV10Result.PlainHi), 8)",
    "AssertOffset<SaveMasterHashV10Result>(nameof(SaveMasterHashV10Result.StoredLo), 16)",
    "AssertOffset<SaveMasterHashV10Result>(nameof(SaveMasterHashV10Result.StoredHi), 24)",
    "AssertSize<SaveFileHeaderV10>(72)",
    "AssertOffset<SaveFileHeaderV10>(nameof(SaveFileHeaderV10.MagicValue), 0)",
    "AssertOffset<SaveFileHeaderV10>(nameof(SaveFileHeaderV10.Version), 4)",
    "AssertOffset<SaveFileHeaderV10>(nameof(SaveFileHeaderV10.CompatMask), 6)",
    "AssertOffset<SaveFileHeaderV10>(nameof(SaveFileHeaderV10.Flags), 7)",
    "AssertOffset<SaveFileHeaderV10>(nameof(SaveFileHeaderV10.TimestampUnixMs), 8)",
    "AssertOffset<SaveFileHeaderV10>(nameof(SaveFileHeaderV10.Checksum), 16)",
    "AssertOffset<SaveFileHeaderV10>(nameof(SaveFileHeaderV10.DeltaCount), 20)",
    "AssertOffset<SaveFileHeaderV10>(nameof(SaveFileHeaderV10.EntityCount), 24)",
    "AssertOffset<SaveFileHeaderV10>(nameof(SaveFileHeaderV10.PlayerOffset), 28)",
    "AssertOffset<SaveFileHeaderV10>(nameof(SaveFileHeaderV10.DeltaOffset), 32)",
    "AssertOffset<SaveFileHeaderV10>(nameof(SaveFileHeaderV10.EntityOffset), 36)",
    "AssertOffset<SaveFileHeaderV10>(nameof(SaveFileHeaderV10.HashPayload64), 40)",
    "AssertOffset<SaveFileHeaderV10>(nameof(SaveFileHeaderV10.HashHeader64), 48)",
    "AssertOffset<SaveFileHeaderV10>(nameof(SaveFileHeaderV10.MasterStateHashLo), SaveMasterHashV10.MasterStateHashLoOffset)",
    "AssertOffset<SaveFileHeaderV10>(nameof(SaveFileHeaderV10.MasterStateHashHi), SaveMasterHashV10.MasterStateHashHiOffset)",
)

EXPECTED_HEADER_FIELDS = (
    "public uint MagicValue;",
    "public ushort Version;",
    "public byte CompatMask;",
    "public byte Flags;",
    "public ulong TimestampUnixMs;",
    "public uint Checksum;",
    "public uint DeltaCount;",
    "public uint EntityCount;",
    "public uint PlayerOffset;",
    "public uint DeltaOffset;",
    "public uint EntityOffset;",
    "public ulong HashPayload64;",
    "public ulong HashHeader64;",
    "public ulong MasterStateHashLo;",
    "public ulong MasterStateHashHi;",
)

EXPECTED_RESULT_CONSTRUCTOR_ASSIGNMENTS = (
    "PlainLo = plainLo;",
    "PlainHi = plainHi;",
    "StoredLo = storedLo;",
    "StoredHi = storedHi;",
)

EXPECTED_HEADER_FORWARDING_FIELDS = (
    "header.MagicValue",
    "header.Version",
    "header.CompatMask",
    "header.Flags",
    "header.TimestampUnixMs",
    "header.Checksum",
    "header.DeltaCount",
    "header.EntityCount",
    "header.PlayerOffset",
    "header.DeltaOffset",
    "header.EntityOffset",
    "header.HashPayload64",
)

EXPECTED_PREIMAGE_WRITE_SEQUENCE = (
    "domain",
    "u32:magicValue",
    "u16:version",
    "u8:compatMask",
    "u8:flags",
    "u64:timestampUnixMs",
    "u32:checksum",
    "u32:deltaCount",
    "u32:entityCount",
    "u32:playerOffset",
    "u32:deltaOffset",
    "u32:entityOffset",
    "u64:hashPayload64",
    "u64:unchecked((ulong)worldSeed)",
    "u64:unchecked((ulong)sectorHash)",
    "u64:HectonContractVersion.HashLo",
    "u64:HectonContractVersion.HashHi",
)

EXPECTED_PREIMAGE_CURSOR_OPERATIONS = (
    "domain:16",
    "write:u32:magicValue",
    "advance:4",
    "write:u16:version",
    "advance:2",
    "writepost:u8:compatMask",
    "writepost:u8:flags",
    "write:u64:timestampUnixMs",
    "advance:8",
    "write:u32:checksum",
    "advance:4",
    "write:u32:deltaCount",
    "advance:4",
    "write:u32:entityCount",
    "advance:4",
    "write:u32:playerOffset",
    "advance:4",
    "write:u32:deltaOffset",
    "advance:4",
    "write:u32:entityOffset",
    "advance:4",
    "write:u64:hashPayload64",
    "advance:8",
    "write:u64:unchecked((ulong)worldSeed)",
    "advance:8",
    "write:u64:unchecked((ulong)sectorHash)",
    "advance:8",
    "write:u64:HectonContractVersion.HashLo",
    "advance:8",
    "write:u64:HectonContractVersion.HashHi",
)

EXPECTED_SHUFFLE_MASK_OPERATIONS = (
    "lo-domain:20",
    "write:u64:unchecked((ulong)worldSeed)",
    "advance:8",
    "write:u64:unchecked((ulong)sectorHash)",
    "lo-hash:ShuffleMaskLoBytes",
    "hi-domain:20",
    "write:u64:unchecked((ulong)sectorHash)",
    "advance:8",
    "write:u64:unchecked((ulong)worldSeed)",
    "advance:8",
    "write:u64:maskLo",
    "hi-hash:ShuffleMaskHiBytes",
)

EXPECTED_ENDIAN_WRITERS = {
    "WriteU16": (
        "target[0] = (byte)value;",
        "target[1] = (byte)(value >> 8);",
    ),
    "WriteU32": (
        "target[0] = (byte)value;",
        "target[1] = (byte)(value >> 8);",
        "target[2] = (byte)(value >> 16);",
        "target[3] = (byte)(value >> 24);",
    ),
    "WriteU64": (
        "target[0] = (byte)value;",
        "target[1] = (byte)(value >> 8);",
        "target[2] = (byte)(value >> 16);",
        "target[3] = (byte)(value >> 24);",
        "target[4] = (byte)(value >> 32);",
        "target[5] = (byte)(value >> 40);",
        "target[6] = (byte)(value >> 48);",
        "target[7] = (byte)(value >> 56);",
    ),
}

EXPECTED_FULL_HASH64_HELPERS = 2
EXPECTED_STACKALLOC_BUFFERS = (
    "byte* preimage = stackalloc byte[MasterHiHashBytes];",
    "byte* buffer = stackalloc byte[ShuffleMaskHiBytes];",
)
EXPECTED_INTERNAL_DECLARATIONS = (
    "internal readonly struct SaveMasterHashV10Result",
    "internal struct SaveFileHeaderV10",
    "internal static unsafe class SaveMasterHashV10",
)
EXPECTED_ACTIVE_STORAGE_SENTINELS = (
    "internal const ushort CurrentVersion = 0x000B;",
    "internal const int CurrentHeaderSize = 56;",
    "private const ushort AlignedSectionHeaderVersion = 0x000B;",
)
EXPECTED_BLITTABLE_ATTRIBUTES = (
    (
        "SaveMasterHashV10Result",
        r"\[BinaryBlittableSafe\]\s*\[StructLayout\(LayoutKind\.Sequential,\s*Size\s*=\s*32\)\]\s*internal\s+readonly\s+struct\s+SaveMasterHashV10Result",
    ),
    (
        "SaveFileHeaderV10",
        r"\[BinaryBlittableSafe\]\s*\[StructLayout\(LayoutKind\.Sequential,\s*Size\s*=\s*SaveMasterHashV10\.HeaderSizeBytes\)\]\s*internal\s+struct\s+SaveFileHeaderV10",
    ),
)

FORBIDDEN_CSHARP_PATTERNS = (
    "new byte[",
    "new List<",
    "new Dictionary<",
    "BitConverter",
    "Encoding.",
    "GetBytes(",
    "string ",
    "System.Text",
)


def load_replay_hasher(script_path: Path):
    spec = importlib.util.spec_from_file_location("ReplayHasher", script_path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load {script_path}")

    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def extract_body_from_brace(text: str, brace_index: int, label: str) -> str:
    if brace_index < 0:
        raise ValueError(f"Missing method body for {label}")

    depth = 0
    for index in range(brace_index, len(text)):
        char = text[index]
        if char == "{":
            depth += 1
            continue
        if char == "}":
            depth -= 1
            if depth == 0:
                return text[brace_index + 1 : index]

    raise ValueError(f"Unterminated method body for {label}")


def extract_method_body(text: str, method_name: str) -> str:
    signature = re.search(
        rf"\b(?:private|internal|public)\s+static\s+(?:[A-Za-z0-9_<>,\[\].]+\s+)+{re.escape(method_name)}\s*\(",
        text,
    )
    if signature is None:
        raise ValueError(f"Missing method declaration {method_name}")

    brace_index = text.find("{", signature.end())
    return extract_body_from_brace(text, brace_index, method_name)


def extract_method_body_by_pattern(text: str, signature_pattern: str, label: str) -> str:
    signature = re.search(signature_pattern, text, re.MULTILINE | re.DOTALL)
    if signature is None:
        raise ValueError(f"Missing method declaration {label}")

    brace_index = text.find("{", signature.end())
    return extract_body_from_brace(text, brace_index, label)


def extract_struct_body(text: str, struct_name: str) -> str:
    signature = re.search(
        rf"\b(?:private|internal|public)\s+(?:readonly\s+)?struct\s+{re.escape(struct_name)}\s*",
        text,
    )
    if signature is None:
        raise ValueError(f"Missing struct declaration {struct_name}")

    brace_index = text.find("{", signature.end())
    if brace_index < 0:
        raise ValueError(f"Missing struct body for {struct_name}")

    depth = 0
    for index in range(brace_index, len(text)):
        char = text[index]
        if char == "{":
            depth += 1
            continue
        if char == "}":
            depth -= 1
            if depth == 0:
                return text[brace_index + 1 : index]

    raise ValueError(f"Unterminated struct body for {struct_name}")


def extract_constructor_body(text: str, type_name: str) -> str:
    signature = re.search(
        rf"\bpublic\s+{re.escape(type_name)}\s*\(",
        text,
    )
    if signature is None:
        raise ValueError(f"Missing constructor declaration {type_name}")

    brace_index = text.find("{", signature.end())
    return extract_body_from_brace(text, brace_index, type_name)


def extract_byte_assignments(body: str, expected_start: int = 0) -> bytes:
    assignments: dict[int, int] = {}
    for match in re.finditer(r"target\[(\d+)\]\s*=\s*\(byte\)'(.)';", body):
        assignments[int(match.group(1))] = ord(match.group(2))

    if not assignments:
        raise ValueError("No byte assignments found")

    unexpected = [index for index in assignments if index < expected_start]
    if unexpected:
        raise ValueError(f"Unexpected byte assignment indexes before {expected_start}: {unexpected}")

    expected_indexes = list(range(expected_start, max(assignments) + 1))
    missing = [index for index in expected_indexes if index not in assignments]
    if missing:
        raise ValueError(f"Missing byte assignment indexes: {missing}")

    return bytes(assignments[index] for index in expected_indexes)


def extract_preimage_write_sequence(body: str) -> tuple[str, ...]:
    sequence: list[str] = []
    for raw_line in body.splitlines():
        line = raw_line.strip()
        if line.startswith("int cursor = WriteMasterDomain("):
            sequence.append("domain")
            continue

        if line.startswith("target[cursor++] = "):
            value = line[len("target[cursor++] = ") :].removesuffix(";")
            sequence.append(f"u8:{value}")
            continue

        for prefix, lane in (
            ("WriteU16(", "u16"),
            ("WriteU32(", "u32"),
            ("WriteU64(", "u64"),
        ):
            if not line.startswith(prefix):
                continue

            value = line.rsplit(",", 1)[1].removesuffix(");").strip()
            sequence.append(f"{lane}:{value}")
            break

    return tuple(sequence)


def extract_preimage_cursor_operations(body: str) -> tuple[str, ...]:
    operations: list[str] = []
    for raw_line in body.splitlines():
        line = raw_line.strip()
        if line.startswith("int cursor = WriteMasterDomain("):
            operations.append("domain:16")
            continue

        advance = re.fullmatch(r"cursor\s+\+=\s+(\d+);", line)
        if advance is not None:
            operations.append(f"advance:{advance.group(1)}")
            continue

        if line.startswith("target[cursor++] = "):
            value = line[len("target[cursor++] = ") :].removesuffix(";")
            operations.append(f"writepost:u8:{value}")
            continue

        for prefix, lane in (
            ("WriteU16(", "u16"),
            ("WriteU32(", "u32"),
            ("WriteU64(", "u64"),
        ):
            if not line.startswith(prefix):
                continue

            value = line.rsplit(",", 1)[1].removesuffix(");").strip()
            operations.append(f"write:{lane}:{value}")
            break

    return tuple(operations)


def preimage_end_offset(operations: tuple[str, ...]) -> int:
    cursor = 0
    max_written_end = 0
    lane_sizes = {"u8": 1, "u16": 2, "u32": 4, "u64": 8}

    for operation in operations:
        if operation.startswith("domain:"):
            cursor = int(operation.split(":", 1)[1])
            max_written_end = max(max_written_end, cursor)
            continue

        if operation.startswith("advance:"):
            cursor += int(operation.split(":", 1)[1])
            continue

        if operation.startswith("writepost:"):
            _, lane, _ = operation.split(":", 2)
            size = lane_sizes[lane]
            max_written_end = max(max_written_end, cursor + size)
            cursor += size
            continue

        if operation.startswith("write:"):
            _, lane, _ = operation.split(":", 2)
            max_written_end = max(max_written_end, cursor + lane_sizes[lane])

    return max_written_end


def extract_shuffle_mask_operations(body: str) -> tuple[str, ...]:
    operations: list[str] = []
    for raw_line in body.splitlines():
        line = raw_line.strip()
        if line == "int cursor = WriteShuffleDomainLo(buffer);":
            operations.append("lo-domain:20")
            continue

        if line == "cursor = WriteShuffleDomainHi(buffer);":
            operations.append("hi-domain:20")
            continue

        advance = re.fullmatch(r"cursor\s+\+=\s+(\d+);", line)
        if advance is not None:
            operations.append(f"advance:{advance.group(1)}")
            continue

        if line.startswith("WriteU64(buffer + cursor, "):
            value = line.rsplit(",", 1)[1].removesuffix(");").strip()
            operations.append(f"write:u64:{value}")
            continue

        if line == "maskLo = Hash64(buffer, ShuffleMaskLoBytes);":
            operations.append("lo-hash:ShuffleMaskLoBytes")
            continue

        if line == "maskHi = Hash64(buffer, ShuffleMaskHiBytes);":
            operations.append("hi-hash:ShuffleMaskHiBytes")

    return tuple(operations)


def shuffle_hash_end_offsets(operations: tuple[str, ...]) -> tuple[int, int]:
    cursor = 0
    lo_end = 0
    hi_end = 0
    active_lane = ""

    for operation in operations:
        if operation == "lo-domain:20":
            active_lane = "lo"
            cursor = 20
            continue

        if operation == "hi-domain:20":
            active_lane = "hi"
            cursor = 20
            continue

        if operation.startswith("advance:"):
            cursor += int(operation.split(":", 1)[1])
            continue

        if operation.startswith("write:u64:"):
            if active_lane == "lo":
                lo_end = max(lo_end, cursor + 8)
            elif active_lane == "hi":
                hi_end = max(hi_end, cursor + 8)

    return lo_end, hi_end


def validate_rotate_body(body: str, expected_lines: tuple[str, ...], label: str, errors: list[str]) -> None:
    for line in expected_lines:
        require(line in body, f"{label} formula drift: {line}", errors)


def validate_endian_writer(body: str, expected_lines: tuple[str, ...], label: str, errors: list[str]) -> None:
    observed_lines = tuple(
        line.strip()
        for line in body.splitlines()
        if line.strip().startswith("target[")
    )
    require(observed_lines == expected_lines, f"{label} little-endian writer drift", errors)


def require(condition: bool, message: str, errors: list[str]) -> None:
    if not condition:
        errors.append(message)


def validate() -> list[str]:
    root = Path(__file__).resolve().parents[2]
    replay_path = root / "Tools" / "Security" / "ReplayHasher.py"
    csharp_path = root / "Assets" / "_Project" / "Scripts" / "SaveSystem" / "SaveMasterHashV10.cs"
    manifest_path = root / "Assets" / "_Project" / "Scripts" / "Core" / "BinaryLayoutManifest.cs"
    storage_path = root / "Assets" / "_Project" / "Scripts" / "SaveBinaryStorage.cs"

    replay = load_replay_hasher(replay_path)
    csharp = csharp_path.read_text(encoding="utf-8-sig")
    manifest = manifest_path.read_text(encoding="utf-8-sig")
    storage = storage_path.read_text(encoding="utf-8-sig")
    errors: list[str] = []

    for name, expected in EXPECTED_CONSTANTS.items():
        pattern = rf"(?:internal|private)\s+const\s+(?:ushort|int)\s+{name}\s*=\s*{re.escape(expected)};"
        require(re.search(pattern, csharp) is not None, f"constant {name} != {expected}", errors)

    for declaration in EXPECTED_INTERNAL_DECLARATIONS:
        require(declaration in csharp, f"V10 helper declaration drift: {declaration}", errors)
    require("public struct SaveFileHeaderV10" not in csharp, "SaveFileHeaderV10 must not become public API in this batch", errors)
    require("public static unsafe class SaveMasterHashV10" not in csharp, "SaveMasterHashV10 must not become public API in this batch", errors)

    master_domain = extract_byte_assignments(extract_method_body(csharp, "WriteMasterDomain"))
    require(master_domain == replay.MASTER_DOMAIN, "MASTER_DOMAIN byte mismatch", errors)

    shuffle_prefix = extract_byte_assignments(extract_method_body(csharp, "WriteShuffleDomainPrefix"))
    shuffle_lo_tail = extract_byte_assignments(extract_method_body(csharp, "WriteShuffleDomainLo"), 15)
    shuffle_hi_tail = extract_byte_assignments(extract_method_body(csharp, "WriteShuffleDomainHi"), 15)
    require(shuffle_prefix + shuffle_lo_tail == replay.SHUFFLE_DOMAIN_LO, "SHUFFLE_DOMAIN_LO byte mismatch", errors)
    require(shuffle_prefix + shuffle_hi_tail == replay.SHUFFLE_DOMAIN_HI, "SHUFFLE_DOMAIN_HI byte mismatch", errors)

    master_lo_suffix = extract_byte_assignments(extract_method_body(csharp, "WriteMasterLoSuffix"))
    master_hi_body = extract_method_body(csharp, "WriteMasterHiSuffix")
    master_hi_suffix = extract_byte_assignments(master_hi_body)
    require(master_lo_suffix == replay.MASTER_DOMAIN_LO, "MASTER_DOMAIN_LO byte mismatch", errors)
    require(master_hi_suffix == replay.MASTER_DOMAIN_HI, "MASTER_DOMAIN_HI byte mismatch", errors)
    require("WriteU64(target + 3, plainLo);" in master_hi_body, "plain_lo is not bound into master_hi preimage", errors)

    for line in EXPECTED_MANIFEST_LINES:
        require(line in manifest, f"missing manifest sentinel: {line}", errors)

    result_ctor_body = extract_constructor_body(csharp, "SaveMasterHashV10Result")
    observed_result_ctor_assignments = tuple(
        line.strip()
        for line in result_ctor_body.splitlines()
        if line.strip().endswith(";")
    )
    require(
        observed_result_ctor_assignments == EXPECTED_RESULT_CONSTRUCTOR_ASSIGNMENTS,
        "SaveMasterHashV10Result constructor assignment drift",
        errors,
    )

    header_body = extract_struct_body(csharp, "SaveFileHeaderV10")
    observed_header_fields = [
        line.strip()
        for line in header_body.splitlines()
        if line.strip().startswith("public ")
    ]
    require(
        tuple(observed_header_fields) == EXPECTED_HEADER_FIELDS,
        "SaveFileHeaderV10 field order drift",
        errors,
    )

    header_compute_body = extract_method_body_by_pattern(
        csharp,
        r"\binternal\s+static\s+SaveMasterHashV10Result\s+Compute\s*\(\s*in\s+SaveFileHeaderV10\s+header\s*,\s*long\s+worldSeed\s*,\s*long\s+sectorHash\s*\)",
        "SaveFileHeaderV10 Compute overload",
    )
    observed_forwarding = tuple(re.findall(r"header\.[A-Za-z0-9_]+", header_compute_body))
    require(
        observed_forwarding == EXPECTED_HEADER_FORWARDING_FIELDS,
        "SaveFileHeaderV10 Compute forwarding order drift",
        errors,
    )
    require("header.HashHeader64" not in header_compute_body, "HashHeader64 must stay excluded from MasterStateHash", errors)
    require("header.MasterStateHash" not in header_compute_body, "MasterStateHash result fields must stay excluded from preimage", errors)

    with_hash_body = extract_method_body(csharp, "WithComputedMasterHash")
    require("copy.MasterStateHashLo = hash.StoredLo;" in with_hash_body, "WithComputedMasterHash must store shuffled low lane", errors)
    require("copy.MasterStateHashHi = hash.StoredHi;" in with_hash_body, "WithComputedMasterHash must store shuffled high lane", errors)

    matches_body = extract_method_body(csharp, "MatchesStoredMasterHash")
    require("hash.StoredLo == header.MasterStateHashLo" in matches_body, "MatchesStoredMasterHash must compare stored low lane", errors)
    require("hash.StoredHi == header.MasterStateHashHi" in matches_body, "MatchesStoredMasterHash must compare stored high lane", errors)
    require("hash.Plain" not in matches_body, "MatchesStoredMasterHash must not compare plain lanes", errors)
    compute_body = extract_method_body_by_pattern(
        csharp,
        r"\binternal\s+static\s+SaveMasterHashV10Result\s+Compute\s*\(\s*uint\s+magicValue\s*,",
        "raw field Compute overload",
    )
    require(
        "return new SaveMasterHashV10Result(plainLo, plainHi, storedLo, storedHi);" in compute_body,
        "SaveMasterHashV10 Compute result construction drift",
        errors,
    )

    preimage_body = extract_method_body(csharp, "BuildMasterPreimage")
    require(
        extract_preimage_write_sequence(preimage_body) == EXPECTED_PREIMAGE_WRITE_SEQUENCE,
        "BuildMasterPreimage write sequence drift",
        errors,
    )
    preimage_operations = extract_preimage_cursor_operations(preimage_body)
    require(
        preimage_operations == EXPECTED_PREIMAGE_CURSOR_OPERATIONS,
        "BuildMasterPreimage cursor operation drift",
        errors,
    )
    require(preimage_end_offset(preimage_operations) == 96, "BuildMasterPreimage final byte offset drift", errors)

    shuffle_body = extract_method_body(csharp, "DeriveShuffleMask")
    shuffle_operations = extract_shuffle_mask_operations(shuffle_body)
    require(
        shuffle_operations == EXPECTED_SHUFFLE_MASK_OPERATIONS,
        "DeriveShuffleMask byte operation drift",
        errors,
    )
    require(
        shuffle_hash_end_offsets(shuffle_operations) == (36, 44),
        "DeriveShuffleMask final byte offsets drift",
        errors,
    )

    for writer_name, expected_lines in EXPECTED_ENDIAN_WRITERS.items():
        validate_endian_writer(
            extract_method_body(csharp, writer_name),
            expected_lines,
            writer_name,
            errors,
        )

    validate_rotate_body(
        extract_method_body(csharp, "Rotl128"),
        (
            "shift &= 127;",
            "if (shift == 0)",
            "if (shift == 64)",
            "outLo = hi;",
            "outHi = lo;",
            "int inverse = 64 - shift;",
            "outLo = (lo << shift) | (hi >> inverse);",
            "outHi = (hi << shift) | (lo >> inverse);",
            "int laneShift = shift - 64;",
            "int laneInverse = 64 - laneShift;",
            "outLo = (hi << laneShift) | (lo >> laneInverse);",
            "outHi = (lo << laneShift) | (hi >> laneInverse);",
        ),
        "Rotl128",
        errors,
    )
    validate_rotate_body(
        extract_method_body(csharp, "Rotr128"),
        (
            "shift &= 127;",
            "if (shift == 0)",
            "if (shift == 64)",
            "outLo = hi;",
            "outHi = lo;",
            "int inverse = 64 - shift;",
            "outLo = (lo >> shift) | (hi << inverse);",
            "outHi = (hi >> shift) | (lo << inverse);",
            "int laneShift = shift - 64;",
            "int laneInverse = 64 - laneShift;",
            "outLo = (hi >> laneShift) | (lo << laneInverse);",
            "outHi = (lo >> laneShift) | (hi << laneInverse);",
        ),
        "Rotr128",
        errors,
    )

    for forbidden in FORBIDDEN_CSHARP_PATTERNS:
        require(forbidden not in csharp, f"forbidden managed helper pattern: {forbidden}", errors)

    observed_stackalloc_buffers = tuple(
        line.strip()
        for line in csharp.splitlines()
        if "stackalloc byte[" in line
    )
    require(
        observed_stackalloc_buffers == EXPECTED_STACKALLOC_BUFFERS,
        "stackalloc byte buffer contract drift",
        errors,
    )
    require("StructLayout(LayoutKind.Sequential, Size = SaveMasterHashV10.HeaderSizeBytes)" in csharp,
            "SaveFileHeaderV10 sequential layout missing", errors)
    require("StructLayout(LayoutKind.Sequential, Size = 32)" in csharp,
            "SaveMasterHashV10Result sequential layout missing", errors)
    for type_name, pattern in EXPECTED_BLITTABLE_ATTRIBUTES:
        require(
            re.search(pattern, csharp, re.MULTILINE) is not None,
            f"{type_name} BinaryBlittableSafe attribute drift",
            errors,
        )
    require("return ((ulong)hash.y << 32) | hash.x;" in extract_method_body(csharp, "Hash64"),
            "Unity.Mathematics xxHash3 uint2 lane assembly drift", errors)
    require("return ((ulong)hash.y << 32) | hash.x;" in extract_method_body(storage, "Hash64"),
            "SaveBinaryStorage.Hash64 full-lane convention drift", errors)
    for sentinel in EXPECTED_ACTIVE_STORAGE_SENTINELS:
        require(sentinel in storage, f"active SaveBinaryStorage sentinel drift: {sentinel}", errors)
    require("SaveMasterHashV10" not in storage, "SaveBinaryStorage must not wire V10 helper before verified integration", errors)
    require("SaveFileHeaderV10" not in storage, "SaveBinaryStorage must not wire V10 header before verified integration", errors)

    sample_preimage = replay.build_master_preimage(
        0x48454354,
        0x000A,
        0x07,
        0x0C,
        0x0000018F3D123456,
        0xDEADBEEF,
        37,
        1024,
        72,
        4096,
        8192,
        0x0123456789ABCDEF,
        123456789,
        -987654321,
    )
    require(len(sample_preimage) == 96, "ReplayHasher master preimage length drift", errors)

    master = replay.compute_master_state_hash(
        0x48454354,
        0x000A,
        0x07,
        0x0C,
        0x0000018F3D123456,
        0xDEADBEEF,
        37,
        1024,
        72,
        4096,
        8192,
        0x0123456789ABCDEF,
        123456789,
        -987654321,
    )
    require(
        master == (
            0x8FE1E070164A96D5,
            0x9BB3607B11A8BCF9,
            0x064D4D0C5A908C82,
            0x36409BD1C7FA745E,
        ),
        "ReplayHasher master vector drift",
        errors,
    )

    return errors


def main() -> int:
    errors = validate()
    if errors:
        print("SAVE_MASTER_HASH_CSHARP_GUARD=FAIL", file=sys.stderr)
        for error in errors:
            print(error, file=sys.stderr)
        return 1

    print(
        "SAVE_MASTER_HASH_CSHARP_GUARD=PASS "
        f"domains=5 constants=9 manifestSentinels={len(EXPECTED_MANIFEST_LINES)} "
        f"headerForwarding={len(EXPECTED_HEADER_FORWARDING_FIELDS)} "
        f"preimageWrites={len(EXPECTED_PREIMAGE_WRITE_SEQUENCE)} "
        f"preimageOps={len(EXPECTED_PREIMAGE_CURSOR_OPERATIONS)} "
        f"preimageEnd=96 "
        f"shuffleOps={len(EXPECTED_SHUFFLE_MASK_OPERATIONS)} "
        f"shuffleEnds=36/44 "
        f"rotGuards=2 "
        f"endianWriters={len(EXPECTED_ENDIAN_WRITERS)} "
        f"hash64Helpers={EXPECTED_FULL_HASH64_HELPERS} "
        f"stackallocBuffers={len(EXPECTED_STACKALLOC_BUFFERS)} "
        f"internalTypes={len(EXPECTED_INTERNAL_DECLARATIONS)} "
        f"activeWriterSentinels={len(EXPECTED_ACTIVE_STORAGE_SENTINELS)} "
        f"resultCtor={len(EXPECTED_RESULT_CONSTRUCTOR_ASSIGNMENTS)} "
        f"blitAttrs={len(EXPECTED_BLITTABLE_ATTRIBUTES)}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
