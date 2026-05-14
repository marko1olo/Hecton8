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
    "MasterPreimageBytes": "80",
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
    "AssertOffset<SaveFileHeaderV10>(nameof(SaveFileHeaderV10.MasterStateHashLo), SaveMasterHashV10.MasterStateHashLoOffset)",
    "AssertOffset<SaveFileHeaderV10>(nameof(SaveFileHeaderV10.MasterStateHashHi), SaveMasterHashV10.MasterStateHashHiOffset)",
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


def extract_method_body(text: str, method_name: str) -> str:
    signature = re.search(
        rf"\b(?:private|internal|public)\s+static\s+(?:[A-Za-z0-9_<>,\[\].]+\s+)+{re.escape(method_name)}\s*\(",
        text,
    )
    if signature is None:
        raise ValueError(f"Missing method declaration {method_name}")

    brace_index = text.find("{", signature.end())
    if brace_index < 0:
        raise ValueError(f"Missing method body for {method_name}")

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

    raise ValueError(f"Unterminated method body for {method_name}")


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


def require(condition: bool, message: str, errors: list[str]) -> None:
    if not condition:
        errors.append(message)


def validate() -> list[str]:
    root = Path(__file__).resolve().parents[2]
    replay_path = root / "Tools" / "Security" / "ReplayHasher.py"
    csharp_path = root / "Assets" / "_Project" / "Scripts" / "SaveSystem" / "SaveMasterHashV10.cs"
    manifest_path = root / "Assets" / "_Project" / "Scripts" / "Core" / "BinaryLayoutManifest.cs"

    replay = load_replay_hasher(replay_path)
    csharp = csharp_path.read_text(encoding="utf-8-sig")
    manifest = manifest_path.read_text(encoding="utf-8-sig")
    errors: list[str] = []

    for name, expected in EXPECTED_CONSTANTS.items():
        pattern = rf"(?:internal|private)\s+const\s+(?:ushort|int)\s+{name}\s*=\s*{re.escape(expected)};"
        require(re.search(pattern, csharp) is not None, f"constant {name} != {expected}", errors)

    master_domain = extract_byte_assignments(extract_method_body(csharp, "WriteMasterDomain"))
    require(master_domain == replay.MASTER_DOMAIN, "MASTER_DOMAIN byte mismatch", errors)

    shuffle_prefix = extract_byte_assignments(extract_method_body(csharp, "WriteShuffleDomainPrefix"))
    shuffle_lo_tail = extract_byte_assignments(extract_method_body(csharp, "WriteShuffleDomainLo"), 15)
    shuffle_hi_tail = extract_byte_assignments(extract_method_body(csharp, "WriteShuffleDomainHi"), 15)
    require(shuffle_prefix + shuffle_lo_tail == replay.SHUFFLE_DOMAIN_LO, "SHUFFLE_DOMAIN_LO byte mismatch", errors)
    require(shuffle_prefix + shuffle_hi_tail == replay.SHUFFLE_DOMAIN_HI, "SHUFFLE_DOMAIN_HI byte mismatch", errors)

    master_lo_suffix = extract_byte_assignments(extract_method_body(csharp, "WriteMasterLoSuffix"))
    master_hi_suffix = extract_byte_assignments(extract_method_body(csharp, "WriteMasterHiSuffix"))[:3]
    require(master_lo_suffix == replay.MASTER_DOMAIN_LO, "MASTER_DOMAIN_LO byte mismatch", errors)
    require(master_hi_suffix == replay.MASTER_DOMAIN_HI, "MASTER_DOMAIN_HI byte mismatch", errors)

    for line in EXPECTED_MANIFEST_LINES:
        require(line in manifest, f"missing manifest sentinel: {line}", errors)

    for forbidden in FORBIDDEN_CSHARP_PATTERNS:
        require(forbidden not in csharp, f"forbidden managed helper pattern: {forbidden}", errors)

    require(csharp.count("stackalloc byte[") >= 2, "expected stackalloc byte buffers are missing", errors)
    require("StructLayout(LayoutKind.Sequential, Pack = 1, Size = SaveMasterHashV10.HeaderSizeBytes)" in csharp,
            "SaveFileHeaderV10 packed layout missing", errors)
    require("StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)" in csharp,
            "SaveMasterHashV10Result packed layout missing", errors)

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
            0x82C250ACAADCFCEE,
            0x750FEB3BE2F001A7,
            0x32C38E7EA8C9246D,
            0x8CB2B6D20A988126,
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

    print("SAVE_MASTER_HASH_CSHARP_GUARD=PASS domains=5 constants=9 manifestSentinels=8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
