#!/usr/bin/env python3
"""Corruption tests for Tools/h8bin_validator.py."""

from __future__ import annotations

import json
import math
import struct
import subprocess
import sys
import tempfile
import unittest
import defusedxml.ElementTree as ET
import argparse
from pathlib import Path

import h8bin_validator


TOOL_DIR = Path(__file__).resolve().parent
VALIDATOR = TOOL_DIR / "h8bin_validator.py"
MAGIC = 0x4D443848
H8VB_MAGIC = 0x42563848
FNV_OFFSET = 2166136261
FNV_PRIME = 16777619


SCHEMA_TEMPLATE = """
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Data
{{
    public static class H8DataLayoutConstants
    {{
        public const uint BlobMagic = 0x4D443848u;
        public const ushort FormatVersion = 1;
        public const int HeaderSizeBytes = 16;
        public const int DirectorySizeBytes = 64;
        public const int SectionAlignmentBytes = {section_alignment};
        public const int ItemRecordSize = 16;
        {extra_constants}
    }}

    public enum H8DataSectionId : uint
    {{
        Items = 1u,
        NarrativeTriggers = 2u
    }}

    [StructLayout(LayoutKind.Explicit, Size = H8DataLayoutConstants.HeaderSizeBytes)]
    public struct H8DataBlobHeader
    {{
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public ushort FormatVersion;
        [FieldOffset(6)] public ushort HeaderBytes;
        [FieldOffset(8)] public ulong Checksum64;
    }}

    [StructLayout(LayoutKind.Explicit, Size = H8DataLayoutConstants.DirectorySizeBytes)]
    public struct H8DataBlobDirectory
    {{
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public ushort FormatVersion;
        [FieldOffset(6)] public ushort SectionCount;
        [FieldOffset(8)] public uint SectionTableOffset;
        [FieldOffset(12)] public uint SectionTableBytes;
        [FieldOffset(16)] public uint BlobBytes;
        [FieldOffset(20)] public uint DataStartOffset;
        [FieldOffset(24)] public uint LocalizationOffset;
        [FieldOffset(28)] public uint LocalizationBytes;
        [FieldOffset(32)] public uint Flags;
        [FieldOffset(36)] public uint WorldSeed;
        [FieldOffset(40)] public uint AppVersionHash;
        [FieldOffset(44)] public uint Reserved0;
        [FieldOffset(48)] public uint Reserved1;
        [FieldOffset(52)] public uint Reserved2;
        [FieldOffset(56)] public uint Reserved3;
        [FieldOffset(60)] public uint Reserved4;
    }}

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct H8DataSectionEntry
    {{
        [FieldOffset(0)] public uint SectionId;
        [FieldOffset(4)] public uint RecordSize;
        [FieldOffset(8)] public uint Count;
        [FieldOffset(12)] public uint OffsetBytes;
    }}

    [StructLayout(LayoutKind.Explicit, Size = H8DataLayoutConstants.ItemRecordSize)]
    public struct H8ItemRecord
    {{
        [FieldOffset(0)] public uint HashId;
        [FieldOffset(4)] public uint RecordIndex;
        [FieldOffset(8)] public uint Cost;
        [FieldOffset(12)] public uint Reserved1;
    }}

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct H8NarrativeTriggerRecord
    {{
        [FieldOffset(0)] public double AupX;
        [FieldOffset(8)] public double AupY;
        [FieldOffset(16)] public double AupZ;
        [FieldOffset(24)] public uint TriggerHash;
        [FieldOffset(28)] public float RadiusMeters;
    }}

    {extra_struct}

    public static class H8DataLayoutAudit
    {{
        public static uint GetExpectedRecordSize(H8DataSectionId sectionId)
        {{
            switch (sectionId)
            {{
                case H8DataSectionId.Items: return H8DataLayoutConstants.ItemRecordSize;
                case H8DataSectionId.NarrativeTriggers: return (uint)UnsafeUtility.SizeOf<H8NarrativeTriggerRecord>();
                default: return 0u;
            }}
        }}
    }}
}}
"""


BAD_LAYOUT_STRUCT = """
[StructLayout(LayoutKind.Explicit, Size = 16)]
public struct H8BadMisalignedRecord
{
    [FieldOffset(1)] public double AupX;
}
"""

PACK_ONE_STRUCT = """
[StructLayout(LayoutKind.Explicit, Size = 16, Pack = 1)]
public struct H8PackOneRecord
{
    [FieldOffset(0)] public double AupX;
    [FieldOffset(8)] public ulong HashId;
}
"""

BAD_SIZE_STRUCT = """
[StructLayout(LayoutKind.Explicit, Size = 10)]
public struct H8BadSizeRecord
{
    [FieldOffset(0)] public double AupX;
}
"""

NEGATIVE_OFFSET_STRUCT = """
[StructLayout(LayoutKind.Explicit, Size = 16)]
public struct H8NegativeOffsetRecord
{
    [FieldOffset(-8)] public double AupX;
}
"""

VARIANT_SYNTAX_STRUCT = """
[System.Runtime.InteropServices.StructLayoutAttribute(
    LayoutKind.Explicit,
    Pack = (int)8,
    Size = (int)16)]
[System.Serializable]
public partial readonly struct H8VariantSyntaxRecord
{
    [System.Runtime.InteropServices.FieldOffsetAttribute((int)1)]
    [System.NonSerialized]
    public readonly double AupX;

    [global::System.Runtime.InteropServices.FieldOffset(8)]
    internal readonly uint HashId;
}
"""

COMBINED_ATTRIBUTE_STRUCT = """
[System.Serializable, StructLayout(LayoutKind.Explicit, Size = 16, Pack = 8)]
public unsafe partial struct H8CombinedAttributeRecord
{
    [System.NonSerialized, FieldOffset(0)]
    public ulong HashId;

    [FieldOffset(8), System.Obsolete]
    private uint Count;

    [FieldOffset(12)]
    private uint Reserved0;
}
"""

EXPRESSION_PROPERTY_STRUCT = """
[StructLayout(LayoutKind.Explicit, Size = 16)]
public readonly struct H8ExpressionPropertyRecord
{
    [FieldOffset(0)] public readonly ulong HashId;
    [FieldOffset(8)] public readonly uint Flags;
    [FieldOffset(12)] private readonly uint _pad0;

    public bool IsValid => Flags != 0u;
}
"""

FIXED_BUFFER_STRUCT = """
[StructLayout(LayoutKind.Explicit, Size = 16)]
public unsafe struct H8FixedBufferRecord
{
    [FieldOffset(0)] public uint HashId;
    [FieldOffset(4)] public fixed byte Payload[12];
}
"""

NESTED_EXPLICIT_STRUCT = """
public unsafe struct H8OuterJobLikeStruct
{
    public int Count;

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct H8NestedExplicitRecord
    {
        [FieldOffset(0)] public ulong HashId;
        [FieldOffset(8)] public ulong Value;
    }
}
"""


def write_schema(
    path: Path,
    bad_layout: bool = False,
    extra_struct: str = "",
    extra_constants: str = "",
    section_alignment: int = 16,
) -> None:
    path.mkdir(parents=True, exist_ok=True)
    extra = ""
    if bad_layout:
        extra += BAD_LAYOUT_STRUCT
    if extra_struct:
        extra += extra_struct
    (path / "H8DataMonolithTypes.cs").write_text(
        SCHEMA_TEMPLATE.format(
            extra_struct=extra,
            extra_constants=extra_constants,
            section_alignment=section_alignment,
        ),
        encoding="utf-8",
    )


def make_blob(path: Path, *, magic: int = MAGIC, checksum_delta: int = 0, aup_x: float = 1.0) -> None:
    header_size = 16
    directory_size = 64
    section_count = 2
    section_entry_size = 16
    section_table_offset = header_size + directory_size
    section_table_bytes = section_count * section_entry_size
    data_start = section_table_offset + section_table_bytes
    item_offset = data_start
    trigger_offset = item_offset + 16
    blob_bytes = trigger_offset + 32
    directory = struct.pack(
        "<IHH" + ("I" * 14),
        MAGIC,
        1,
        section_count,
        section_table_offset,
        section_table_bytes,
        blob_bytes,
        data_start,
        0,
        0,
        0,
        123,
        0,
        0,
        0,
        0,
        0,
        0,
    )
    table = b"".join(
        (
            struct.pack("<IIII", 1, 16, 1, item_offset),
            struct.pack("<IIII", 2, 32, 1, trigger_offset),
        )
    )
    item_record = struct.pack("<IIII", 0xAABBCCDD, 0, 0, 0)
    trigger_record = struct.pack("<dddIf", aup_x, 2.0, 3.0, 0xCAFE1234, 4.0)
    blob = bytearray(b"\x00" * header_size + directory + table + item_record + trigger_record)
    checksum = h8bin_validator.xxh3_64(memoryview(blob)[header_size:]) ^ checksum_delta
    blob[0:header_size] = struct.pack("<IHHQ", magic, 1, header_size, checksum)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(blob)


def fnv1a(data: bytes | bytearray) -> int:
    value = FNV_OFFSET
    for item in data:
        value = ((value ^ int(item)) * FNV_PRIME) & 0xFFFFFFFF
    return value


def make_h8vb(
    path: Path,
    *,
    corrupt_payload_alignment: bool = False,
    bank_hash_delta: int = 0,
    codec: int = 1,
    byte_length_delta: int = 0,
    unsorted: bool = False,
    duplicate_hash: bool = False,
    force_two_records: bool = False,
    adpcm_blocks: int = 4,
    adpcm_step: int = 9,
    adpcm_reserved: int = 0,
) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    record_count = 2 if unsorted or duplicate_hash or force_two_records else 1
    payload_offset = 64 + record_count * 32
    if corrupt_payload_alignment:
        payload_offset += 1

    if codec == 0:
        total_samples = 128
        base_payload = bytearray(b"\x00\x00" * total_samples)
    elif codec == 2:
        total_samples = 128
        base_payload = bytearray(b"OggS\x00vorbis" + bytes(117))
    else:
        safe_blocks = max(1, adpcm_blocks)
        total_samples = safe_blocks * 64
        base_payload = bytearray()
        for _ in range(safe_blocks):
            base_payload += struct.pack("<hBB", 0, adpcm_step, adpcm_reserved)
            base_payload += bytes(32)

    hashes = [0x05203E88]
    if record_count == 2:
        hashes = [0x05203F00, 0x05204000]
        if unsorted:
            hashes = [0x05204000, 0x05203F00]
        if duplicate_hash:
            hashes = [0x05203F00, 0x05203F00]

    payload = bytearray()
    records = bytearray()
    for index in range(record_count):
        while len(payload) % 16 != 0:
            payload.append(0)
        cursor = payload_offset + len(payload)
        declared_length = len(base_payload)
        if index == 0:
            declared_length += byte_length_delta
        records += struct.pack(
            "<IIQIIBBBBI",
            hashes[index],
            declared_length,
            cursor,
            total_samples,
            24000,
            codec,
            1,
            16,
            96,
            0,
        )
        payload += base_payload
    while len(payload) % 16 != 0:
        payload.append(0)
    payload_bytes = len(payload)
    bank_hash = fnv1a(records + payload) ^ bank_hash_delta
    header = struct.pack(
        "<IIIIIIQQIBBHIIII",
        H8VB_MAGIC,
        1,
        64,
        32,
        record_count,
        0,
        payload_offset,
        payload_bytes,
        24000,
        1,
        1,
        0xFEFF,
        bank_hash,
        64,
        0,
        0,
    )
    path.write_bytes(header + records + payload)


def recompute_checksum(blob: bytearray) -> None:
    payload = memoryview(blob)[16:]
    try:
        checksum = h8bin_validator.xxh3_64(payload)
    finally:
        payload.release()
    struct.pack_into("<Q", blob, 8, checksum)


def patch_blob(path: Path, offset: int, fmt: str, *values: int) -> None:
    blob = bytearray(path.read_bytes())
    struct.pack_into(fmt, blob, offset, *values)
    recompute_checksum(blob)
    path.write_bytes(blob)


class H8BinValidatorTests(unittest.TestCase):
    def run_validator(self, tmp: Path, *extra_args: str) -> subprocess.CompletedProcess[str]:
        report = tmp / "report.json"
        metrics = tmp / "ci.log"
        command = [
            sys.executable,
            str(VALIDATOR),
            "--target-dir",
            str(tmp / "StreamingAssets"),
            "--cs-source-dir",
            str(tmp / "Scripts"),
            "--runtime-source-dir",
            str(tmp / "Scripts"),
            "--no-require-static-data",
            "--report-json",
            str(report),
            "--metrics-log",
            str(metrics),
            *extra_args,
        ]
        return subprocess.run(command, text=True, capture_output=True, timeout=30)

    def read_codes(self, tmp: Path) -> set[str]:
        return {item["code"] for item in self.read_findings(tmp) if item["severity"] == "ERROR"}

    def read_all_codes(self, tmp: Path) -> set[str]:
        return {item["code"] for item in self.read_findings(tmp)}

    def read_findings(self, tmp: Path) -> list[dict[str, object]]:
        report = json.loads((tmp / "report.json").read_text(encoding="utf-8"))
        return report["findings"]

    def test_valid_blob_passes(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            make_blob(tmp / "StreamingAssets/sample.h8bin")
            result = self.run_validator(tmp)
            self.assertEqual(result.returncode, 0, result.stderr)

    def test_bad_magic_fails(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            make_blob(tmp / "StreamingAssets/sample.h8bin", magic=0x4E423848)
            result = self.run_validator(tmp)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("HEADER_MAGIC", self.read_codes(tmp))

    def test_known_foreign_h8bin_schema_fails_closed_without_h8dm_cascade(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            make_blob(tmp / "StreamingAssets/vocal_banks.h8bin", magic=0x42563848)
            result = self.run_validator(tmp)
            self.assertNotEqual(result.returncode, 0)
            codes = self.read_codes(tmp)
            self.assertIn("H8VB_HEADER_SIZE", codes)
            self.assertNotIn("DIRECTORY_MAGIC", codes)
            self.assertNotIn("SECTION_COUNT", codes)

    def test_valid_h8vb_owner_sidecar_schema_passes_without_h8dm_cascade(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            make_h8vb(tmp / "StreamingAssets/vocal_banks.h8bin")
            result = self.run_validator(tmp)
            self.assertEqual(result.returncode, 0, result.stderr)
            codes = self.read_all_codes(tmp)
            self.assertIn("H8VB_SCHEMA_VALIDATED", codes)
            self.assertNotIn("FOREIGN_H8BIN_SCHEMA_UNVALIDATED", codes)
            self.assertNotIn("DIRECTORY_MAGIC", codes)

    def test_valid_h8vb_multi_record_padding_passes(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            make_h8vb(tmp / "StreamingAssets/vocal_banks.h8bin", force_two_records=True, adpcm_blocks=1)
            result = self.run_validator(tmp)
            self.assertEqual(result.returncode, 0, result.stderr)
            codes = self.read_all_codes(tmp)
            self.assertIn("H8VB_SCHEMA_VALIDATED", codes)
            self.assertNotIn("H8VB_RECORD_CONTIGUITY", codes)

    def test_h8vb_payload_alignment_fails(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            make_h8vb(tmp / "StreamingAssets/vocal_banks.h8bin", corrupt_payload_alignment=True)
            result = self.run_validator(tmp)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("H8VB_PAYLOAD_ALIGNMENT", self.read_codes(tmp))

    def test_h8vb_bank_hash_fails(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            make_h8vb(tmp / "StreamingAssets/vocal_banks.h8bin", bank_hash_delta=1)
            result = self.run_validator(tmp)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("H8VB_BANK_HASH", self.read_codes(tmp))

    def test_h8vb_adpcm_declared_length_fails(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            make_h8vb(tmp / "StreamingAssets/vocal_banks.h8bin", byte_length_delta=-36)
            result = self.run_validator(tmp)
            self.assertNotEqual(result.returncode, 0)
            codes = self.read_codes(tmp)
            self.assertIn("H8VB_ADPCM_LENGTH", codes)
            self.assertIn("H8VB_PAYLOAD_CONTIGUITY", codes)

    def test_h8vb_unsupported_vorbis_codec_fails(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            make_h8vb(tmp / "StreamingAssets/vocal_banks.h8bin", codec=2)
            result = self.run_validator(tmp)
            self.assertNotEqual(result.returncode, 0)
            codes = self.read_codes(tmp)
            self.assertIn("H8VB_RECORD_CODEC_UNSUPPORTED", codes)

    def test_h8vb_unsorted_records_fail(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            make_h8vb(tmp / "StreamingAssets/vocal_banks.h8bin", unsorted=True)
            result = self.run_validator(tmp)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("H8VB_RECORD_SORT", self.read_codes(tmp))

    def test_h8vb_adpcm_block_header_fails(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            make_h8vb(tmp / "StreamingAssets/vocal_banks.h8bin", adpcm_step=0, adpcm_reserved=1)
            result = self.run_validator(tmp)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("H8VB_ADPCM_STEP", self.read_codes(tmp))

    def test_bad_checksum_fails(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            make_blob(tmp / "StreamingAssets/sample.h8bin", checksum_delta=1)
            result = self.run_validator(tmp)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("XXHASH3_MISMATCH", self.read_codes(tmp))

    def test_fail_fast_checksum_mismatch_closes_mmap(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            make_blob(tmp / "StreamingAssets/sample.h8bin", checksum_delta=1)
            result = self.run_validator(tmp, "--fail-fast")
            self.assertNotEqual(result.returncode, 0)
            self.assertNotIn("BufferError", result.stderr)
            self.assertNotIn("Traceback", result.stderr)
            self.assertIn("XXHASH3_MISMATCH", self.read_codes(tmp))

    def test_misaligned_csharp_field_fails(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts", bad_layout=True)
            make_blob(tmp / "StreamingAssets/sample.h8bin")
            result = self.run_validator(tmp)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("FIELD_ALIGNMENT", self.read_codes(tmp))

    def test_nan_aup_fails(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            make_blob(tmp / "StreamingAssets/sample.h8bin", aup_x=math.nan)
            result = self.run_validator(tmp)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("FLOAT_NOT_FINITE", self.read_codes(tmp))

    def test_aup_out_of_bounds_fails(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            make_blob(tmp / "StreamingAssets/sample.h8bin", aup_x=100001.0)
            result = self.run_validator(tmp)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("AUP_OUT_OF_BOUNDS", self.read_codes(tmp))

    def test_pack_one_struct_layout_fails(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts", extra_struct=PACK_ONE_STRUCT)
            make_blob(tmp / "StreamingAssets/sample.h8bin")
            result = self.run_validator(tmp)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("PACK_1_FORBIDDEN", self.read_codes(tmp))

    def test_struct_size_alignment_fails(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts", extra_struct=BAD_SIZE_STRUCT)
            make_blob(tmp / "StreamingAssets/sample.h8bin")
            result = self.run_validator(tmp)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("STRUCT_SIZE_ALIGNMENT", self.read_codes(tmp))

    def test_negative_field_offset_fails(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts", extra_struct=NEGATIVE_OFFSET_STRUCT)
            make_blob(tmp / "StreamingAssets/sample.h8bin")
            result = self.run_validator(tmp)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("FIELD_NEGATIVE_OFFSET", self.read_codes(tmp))

    def test_fail_fast_schema_error_still_writes_report(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts", bad_layout=True)
            (tmp / "StreamingAssets").mkdir()
            result = self.run_validator(tmp, "--fail-fast")
            self.assertNotEqual(result.returncode, 0)
            self.assertTrue((tmp / "report.json").exists())
            self.assertNotIn("Traceback", result.stderr)
            self.assertIn("FIELD_ALIGNMENT", self.read_codes(tmp))

    def test_missing_csharp_schema_fails_with_report(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            (tmp / "Scripts").mkdir()
            make_blob(tmp / "StreamingAssets/sample.h8bin")
            result = self.run_validator(tmp)
            self.assertNotEqual(result.returncode, 0)
            self.assertNotIn("Traceback", result.stderr)
            codes = self.read_codes(tmp)
            self.assertIn("CS_SOURCE_EMPTY", codes)
            self.assertIn("PRIMARY_STRUCT_MISSING", codes)

    def test_csharp_parser_accepts_variant_attribute_syntax(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts", extra_struct=VARIANT_SYNTAX_STRUCT)
            make_blob(tmp / "StreamingAssets/sample.h8bin")
            result = self.run_validator(tmp)
            self.assertNotEqual(result.returncode, 0)
            report = json.loads((tmp / "report.json").read_text(encoding="utf-8"))
            self.assertEqual(report["structs_parsed"], 6)
            self.assertIn("FIELD_ALIGNMENT", self.read_codes(tmp))

    def test_csharp_parser_accepts_combined_attribute_lists(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts", extra_struct=COMBINED_ATTRIBUTE_STRUCT)
            make_blob(tmp / "StreamingAssets/sample.h8bin")
            result = self.run_validator(tmp)
            self.assertEqual(result.returncode, 0, result.stderr)
            report = json.loads((tmp / "report.json").read_text(encoding="utf-8"))
            self.assertEqual(report["structs_parsed"], 6)
            layout = report["primary_struct_layouts"]["H8DataBlobHeader"]
            self.assertEqual(layout["size"], 16)

    def test_struct_candidate_scan_ignores_comment_struct_text(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            source = tmp / "Scripts"
            source.mkdir()
            path = source / "CommentNoise.cs"
            path.write_text(
                "// struct. This comment must not become a declaration span.\n"
                "public sealed class LargeManagedClass\n"
                "{\n"
                "    public void Run() { if (true) { } }\n"
                "}\n"
                "[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 16)]\n"
                "public struct H8OnlyRealStruct\n"
                "{\n"
                "    [System.Runtime.InteropServices.FieldOffset(0)] public ulong HashId;\n"
                "    [System.Runtime.InteropServices.FieldOffset(8)] public ulong Value;\n"
                "}\n",
                encoding="utf-8",
            )
            spans = h8bin_validator.extract_struct_candidate_spans(path)
            parsed, findings = h8bin_validator.parse_csharp_file(path, {})

        self.assertEqual(len(spans), 1)
        self.assertEqual([item.name for item in parsed], ["H8OnlyRealStruct"])
        self.assertEqual([item.code for item in findings], [])

    def test_expression_bodied_property_is_banned_not_missing_fieldoffset(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            source = tmp / "Scripts"
            source.mkdir()
            path = source / "ExpressionProperty.cs"
            path.write_text(EXPRESSION_PROPERTY_STRUCT, encoding="utf-8")
            parsed, findings = h8bin_validator.parse_csharp_file(path, {})

        self.assertEqual([item.name for item in parsed], ["H8ExpressionPropertyRecord"])
        codes = [item.code for item in findings]
        self.assertIn("STRUCT_PROPERTY_BANNED", codes)
        self.assertNotIn("FIELD_OFFSET_MISSING", codes)

    def test_fixed_buffer_field_uses_declared_byte_span(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            source = tmp / "Scripts"
            source.mkdir()
            path = source / "FixedBuffer.cs"
            path.write_text(FIXED_BUFFER_STRUCT, encoding="utf-8")
            parsed, findings = h8bin_validator.parse_csharp_file(path, {})
            schema = h8bin_validator.SchemaModel({}, {}, [], {item.name: item for item in parsed}, {}, {})
            args = argparse.Namespace(fail_fast=False)
            state = h8bin_validator.ValidationState(args, schema)
            h8bin_validator.validate_struct_layouts(schema.structs, state)

        self.assertEqual([item.name for item in parsed], ["H8FixedBufferRecord"])
        self.assertEqual([item.code for item in findings], [])
        self.assertEqual(schema.structs["H8FixedBufferRecord"].fields[1].type_name, "byte[12]")
        self.assertEqual(schema.structs["H8FixedBufferRecord"].fields[1].size, 12)
        self.assertNotIn("FIELD_DECL_UNPARSED", [item.code for item in state.findings])
        self.assertNotIn("FIELD_OUT_OF_STRUCT", [item.code for item in state.findings])

    def test_nested_explicit_struct_does_not_mark_outer_layout_missing(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            source = tmp / "Scripts"
            source.mkdir()
            path = source / "NestedExplicit.cs"
            path.write_text(NESTED_EXPLICIT_STRUCT, encoding="utf-8")
            spans = h8bin_validator.extract_struct_candidate_spans(path)
            parsed, findings = h8bin_validator.parse_csharp_file(path, {})

        self.assertEqual(len(spans), 1)
        self.assertEqual([item.name for item in parsed], ["H8NestedExplicitRecord"])
        self.assertNotIn("STRUCT_LAYOUT_MISSING", [item.code for item in findings])

    def test_h8bin_validator_source_has_no_python_regex_dependency(self) -> None:
        source = VALIDATOR.read_text(encoding="utf-8")
        self.assertNotIn("import re", source)
        self.assertNotIn("re.compile", source)
        self.assertNotIn("re.search", source)

    def test_thorough_sampling_uses_lazy_range(self) -> None:
        indices = h8bin_validator.sample_indices(1_000_000_000, 5.0, True, 123)
        self.assertIsInstance(indices, range)
        self.assertEqual(indices[0], 0)
        self.assertEqual(indices[2], 2)

    def test_default_sampling_uses_lazy_iterator_with_edges(self) -> None:
        indices = h8bin_validator.sample_indices(1_000_000, 5.0, False, 123)
        self.assertNotIsInstance(indices, list)
        iterator = iter(indices)
        self.assertEqual(next(iterator), 0)
        self.assertEqual(next(iterator), 999_999)

    def test_section_overlap_fails(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            binary = tmp / "StreamingAssets/sample.h8bin"
            make_blob(binary)
            patch_blob(binary, 96 + 12, "<I", 112)
            result = self.run_validator(tmp)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("SECTION_OVERLAP", self.read_codes(tmp))

    def test_section_out_of_file_fails_without_traceback(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            binary = tmp / "StreamingAssets/sample.h8bin"
            make_blob(binary)
            patch_blob(binary, 96 + 8, "<I", 2)
            result = self.run_validator(tmp)
            self.assertNotEqual(result.returncode, 0)
            self.assertNotIn("Traceback", result.stderr)
            self.assertIn("SECTION_OUT_OF_FILE", self.read_codes(tmp))
            finding = next(item for item in self.read_findings(tmp) if item["code"] == "SECTION_OUT_OF_FILE")
            self.assertIn("0x", str(finding.get("hexdump", "")))

    def test_section_alignment_fails(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            binary = tmp / "StreamingAssets/sample.h8bin"
            make_blob(binary)
            patch_blob(binary, 80 + 12, "<I", 120)
            result = self.run_validator(tmp)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("SECTION_ALIGNMENT", self.read_codes(tmp))
            finding = next(item for item in self.read_findings(tmp) if item["code"] == "SECTION_ALIGNMENT")
            self.assertEqual(finding["offset"], 92)
            self.assertIn("0x", str(finding.get("hexdump", "")))

    def test_section_alignment_constant_below_floor_fails(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts", section_alignment=8)
            make_blob(tmp / "StreamingAssets/sample.h8bin")
            result = self.run_validator(tmp)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("SECTION_ALIGNMENT_CONTRACT", self.read_codes(tmp))

    def test_record_size_zero_fails_without_sampling_traceback(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            binary = tmp / "StreamingAssets/sample.h8bin"
            make_blob(binary)
            patch_blob(binary, 96 + 4, "<I", 0)
            result = self.run_validator(tmp)
            self.assertNotEqual(result.returncode, 0)
            self.assertNotIn("Traceback", result.stderr)
            self.assertIn("RECORD_SIZE_ZERO", self.read_codes(tmp))

    def test_record_size_under_struct_fails_before_field_read(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            binary = tmp / "StreamingAssets/sample.h8bin"
            make_blob(binary)
            patch_blob(binary, 96 + 4, "<I", 24)
            result = self.run_validator(tmp)
            self.assertNotEqual(result.returncode, 0)
            self.assertNotIn("Traceback", result.stderr)
            self.assertIn("RECORD_SIZE_UNDER_STRUCT", self.read_codes(tmp))

    def test_rle_probe_fails_malformed_section(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts", extra_constants="public const uint RleDirectoryFlag = 1u;")
            binary = tmp / "StreamingAssets/sample.h8bin"
            make_blob(binary)
            patch_blob(binary, 16 + 32, "<I", 1)
            result = self.run_validator(tmp)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("RLE_UNPACKED_SIZE_MISMATCH", self.read_codes(tmp))

    def test_rle_probe_rejects_odd_encoded_byte_count(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts", extra_constants="public const uint RleDirectoryFlag = 1u;")
            binary = tmp / "StreamingAssets/sample.h8bin"
            make_blob(binary)
            patch_blob(binary, 16 + 32, "<I", 1)
            patch_blob(binary, 112, "<II", 1, 1)
            result = self.run_validator(tmp)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("RLE_ODD_ENCODED_BYTES", self.read_codes(tmp))

    def test_rle_probe_rejects_zero_length_run(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts", extra_constants="public const uint RleDirectoryFlag = 1u;")
            binary = tmp / "StreamingAssets/sample.h8bin"
            make_blob(binary)
            patch_blob(binary, 16 + 32, "<I", 1)
            patch_blob(binary, 112, "<IIBB", 1, 2, 0, 0x7F)
            result = self.run_validator(tmp)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("RLE_ZERO_RUN", self.read_codes(tmp))

    def test_directory_unsupported_flag_fails_without_source_backed_rle_flag(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            binary = tmp / "StreamingAssets/sample.h8bin"
            make_blob(binary)
            patch_blob(binary, 16 + 32, "<I", 1)
            result = self.run_validator(tmp)
            self.assertNotEqual(result.returncode, 0)
            self.assertNotIn("Traceback", result.stderr)
            codes = self.read_codes(tmp)
            self.assertIn("DIRECTORY_FLAGS_UNSUPPORTED", codes)
            self.assertNotIn("RLE_UNPACKED_SIZE_MISMATCH", codes)

    def test_integer_aup_bounds_fail(self) -> None:
        layout = h8bin_validator.StructLayout("H8SectorPageRecord", "32", 32, Path("synthetic.cs"), "")
        layout.fields = [
            h8bin_validator.FieldLayout("AupX", "long", 0),
            h8bin_validator.FieldLayout("AupZ", "long", 8),
        ]
        schema = h8bin_validator.SchemaModel({}, {}, [], {"H8SectorPageRecord": layout}, {}, {})
        args = argparse.Namespace(sample_percent=100.0, thorough=True, fail_fast=False)
        state = h8bin_validator.ValidationState(args, schema)
        h8bin_validator.validate_struct_layouts(schema.structs, state)
        with tempfile.TemporaryDirectory() as raw:
            path = Path(raw) / "record.bin"
            path.write_bytes(struct.pack("<qqIIII", 100001, 0, 0, 0, 0, 0))
            with path.open("rb") as handle:
                with h8bin_validator.mmap.mmap(handle.fileno(), 0, access=h8bin_validator.mmap.ACCESS_READ) as mm_obj:
                    h8bin_validator.validate_record_sample(
                        mm_obj,
                        path,
                        h8bin_validator.SectionSchema(1, "SectorPageDirectory", 32, "H8SectorPageRecord"),
                        32,
                        1,
                        0,
                        state,
                        set(),
                    )
        self.assertIn("AUP_INTEGER_OUT_OF_BOUNDS", {finding.code for finding in state.findings})

    def test_reference_master_empty_fails_before_broken_fk(self) -> None:
        layout = h8bin_validator.StructLayout("H8RecipeRecord", "16", 16, Path("synthetic.cs"), "")
        layout.fields = [h8bin_validator.FieldLayout("OutputHash", "uint", 0)]
        schema = h8bin_validator.SchemaModel({}, {}, [], {"H8RecipeRecord": layout}, {}, {})
        args = argparse.Namespace(sample_percent=100.0, thorough=True, fail_fast=False)
        state = h8bin_validator.ValidationState(args, schema)
        h8bin_validator.validate_struct_layouts(schema.structs, state)
        with tempfile.TemporaryDirectory() as raw:
            path = Path(raw) / "record.bin"
            path.write_bytes(struct.pack("<IIII", 0xAABBCCDD, 0, 0, 0))
            with path.open("rb") as handle:
                with h8bin_validator.mmap.mmap(handle.fileno(), 0, access=h8bin_validator.mmap.ACCESS_READ) as mm_obj:
                    h8bin_validator.validate_record_sample(
                        mm_obj,
                        path,
                        h8bin_validator.SectionSchema(1, "Recipes", 16, "H8RecipeRecord"),
                        16,
                        1,
                        0,
                        state,
                        set(),
                    )
        codes = {finding.code for finding in state.findings}
        self.assertIn("REFERENCE_MASTER_EMPTY", codes)
        self.assertNotIn("BROKEN_FOREIGN_KEY", codes)

    def test_unbaked_csv_fails(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            make_blob(tmp / "StreamingAssets/sample.h8bin")
            (tmp / "StreamingAssets/raw_balance.csv").write_text("id,value\nx,1\n", encoding="utf-8")
            result = self.run_validator(tmp)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("UNBAKED_ARTIFACT", self.read_codes(tmp))
            report = json.loads((tmp / "report.json").read_text(encoding="utf-8"))
            artifact = next(item for item in report["findings"] if item["code"] == "UNBAKED_ARTIFACT")
            self.assertIn("Move the text file out of runtime StreamingAssets", artifact["remediation"])

    def test_runtime_streamingassets_text_loader_fails(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            make_blob(tmp / "StreamingAssets/sample.h8bin")
            runtime_dir = tmp / "Scripts/Runtime"
            runtime_dir.mkdir(parents=True, exist_ok=True)
            (runtime_dir / "BadLoader.cs").write_text(
                'using System.IO;\n'
                "public static class BadLoader\n"
                "{\n"
                '    public static string PathFor(string dataPath) => Path.Combine(dataPath, "StreamingAssets", "raw_balance.csv");\n'
                "}\n",
                encoding="utf-8",
            )
            result = self.run_validator(tmp)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("RUNTIME_TEXT_STREAMINGASSETS_LOAD", self.read_codes(tmp))

    def test_runtime_streamingassets_text_loader_symbol_fails(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            make_blob(tmp / "StreamingAssets/sample.h8bin")
            runtime_dir = tmp / "Scripts/Runtime"
            runtime_dir.mkdir(parents=True, exist_ok=True)
            (runtime_dir / "BadSymbolLoader.cs").write_text(
                'using System.IO;\n'
                "public static class BadSymbolLoader\n"
                "{\n"
                '    private const string RulesCsvName = "director_spawn_rules.csv";\n'
                "    public static string PathFor() => Path.Combine(UnityEngine.Application.streamingAssetsPath, RulesCsvName);\n"
                "}\n",
                encoding="utf-8",
            )
            result = self.run_validator(tmp)
            self.assertNotEqual(result.returncode, 0)
            report = json.loads((tmp / "report.json").read_text(encoding="utf-8"))
            messages = [item["message"] for item in report["findings"] if item["code"] == "RUNTIME_TEXT_STREAMINGASSETS_LOAD"]
            self.assertTrue(any("RulesCsvName=director_spawn_rules.csv" in message for message in messages))

    def test_migration_summary_groups_route_owners(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            make_blob(tmp / "StreamingAssets/sample.h8bin")
            (tmp / "StreamingAssets/signal_tuning_profiles.csv").write_text("platform,min,max\nmx350,1,2\n", encoding="utf-8")
            runtime_dir = tmp / "Scripts/Core/Signals"
            runtime_dir.mkdir(parents=True, exist_ok=True)
            (runtime_dir / "SignalWardenRuntime.cs").write_text(
                'using System.IO;\n'
                "public static class SignalWardenRuntime\n"
                "{\n"
                '    public static string PathFor(string dataPath) => Path.Combine(dataPath, "StreamingAssets", "signal_tuning_profiles.csv");\n'
                "}\n",
                encoding="utf-8",
            )
            result = self.run_validator(tmp)
            self.assertNotEqual(result.returncode, 0)
            report = json.loads((tmp / "report.json").read_text(encoding="utf-8"))
            summary = report["migration_summary"]
            self.assertEqual(summary["blocking_counts"]["UNBAKED_ARTIFACT"], 1)
            self.assertEqual(summary["blocking_counts"]["RUNTIME_TEXT_STREAMINGASSETS_LOAD"], 1)
            owner = next(item for item in summary["owners"] if item["owner"] == "Core/Signals")
            self.assertEqual(owner["source_root"], "Assets/_SourceData/Signals")
            self.assertEqual(owner["artifact_count"], 1)
            self.assertEqual(owner["loader_site_count"], 1)

    def test_migration_summary_groups_kcc_locomotion_profiles(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            make_blob(tmp / "StreamingAssets/sample.h8bin")
            (tmp / "StreamingAssets/Hecton8/locomotion_environment_profiles.csv").parent.mkdir(parents=True, exist_ok=True)
            (tmp / "StreamingAssets/Hecton8/locomotion_environment_profiles.csv").write_text(
                "profile,maxSlopeAngle,currentAdvectionScalar,frictionCoefficient,exhaustionPenaltyMax\n"
                "default_abyssal_kcc,48,1,0.85,0.35\n",
                encoding="utf-8",
            )
            result = self.run_validator(tmp)
            self.assertNotEqual(result.returncode, 0)
            report = json.loads((tmp / "report.json").read_text(encoding="utf-8"))
            owner = next(item for item in report["migration_summary"]["owners"] if item["owner"] == "Physics/KCC")
            self.assertEqual(owner["source_root"], "Assets/_SourceData/Physics/KCC")
            self.assertEqual(owner["artifact_count"], 1)

    def test_editor_streamingassets_text_loader_is_ignored(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            make_blob(tmp / "StreamingAssets/sample.h8bin")
            editor_dir = tmp / "Scripts/Editor"
            editor_dir.mkdir(parents=True, exist_ok=True)
            (editor_dir / "EditorLoader.cs").write_text(
                'using System.IO;\n'
                "public static class EditorLoader\n"
                "{\n"
                '    public static string PathFor(string dataPath) => Path.Combine(dataPath, "StreamingAssets", "raw_balance.csv");\n'
                "}\n",
                encoding="utf-8",
            )
            result = self.run_validator(tmp)
            self.assertEqual(result.returncode, 0, result.stderr)

    def test_csv_to_bin_diff_fails_missing_hash(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            binary = tmp / "StreamingAssets/sample.h8bin"
            make_blob(binary)
            source_csv = tmp / "source.csv"
            source_csv.write_text("hash32\n0x01020304\n", encoding="utf-8")
            result = self.run_validator(tmp, "--csv-diff", str(source_csv), str(binary))
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("CSV_TO_BIN_MISSING_HASHES", self.read_codes(tmp))

    def test_csv_to_bin_diff_fails_without_hash_rows(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            binary = tmp / "StreamingAssets/sample.h8bin"
            make_blob(binary)
            source_csv = tmp / "source.csv"
            source_csv.write_text("display,value\nFabricator,1\n", encoding="utf-8")
            result = self.run_validator(tmp, "--csv-diff", str(source_csv), str(binary))
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("CSV_DIFF_NO_HASH_ROWS", self.read_codes(tmp))

    def test_csv_to_bin_diff_accepts_case_insensitive_hash_header(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            binary = tmp / "StreamingAssets/sample.h8bin"
            make_blob(binary)
            source_csv = tmp / "source.csv"
            source_csv.write_text("Hash32\n0xAABBCCDD\n", encoding="utf-8")
            result = self.run_validator(tmp, "--csv-diff", str(source_csv), str(binary))
            self.assertEqual(result.returncode, 0, result.stderr)

    def test_csv_to_bin_diff_accepts_matching_item_field(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            binary = tmp / "StreamingAssets/sample.h8bin"
            make_blob(binary)
            source_csv = tmp / "source.csv"
            source_csv.write_text("Hash32,Cost\n0xAABBCCDD,0\n", encoding="utf-8")
            result = self.run_validator(tmp, "--csv-diff", str(source_csv), str(binary))
            self.assertEqual(result.returncode, 0, result.stderr)

    def test_csv_to_bin_diff_detects_item_field_mismatch(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            binary = tmp / "StreamingAssets/sample.h8bin"
            make_blob(binary)
            source_csv = tmp / "source.csv"
            source_csv.write_text("Hash32,Cost\n0xAABBCCDD,99\n", encoding="utf-8")
            result = self.run_validator(tmp, "--csv-diff", str(source_csv), str(binary))
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("CSV_TO_BIN_VALUE_MISMATCH", self.read_codes(tmp))

    def test_csv_diff_fail_fast_probe_error_still_writes_report(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            make_blob(tmp / "StreamingAssets/sample.h8bin")
            source_csv = tmp / "source.csv"
            source_csv.write_text("Hash32\n0xAABBCCDD\n", encoding="utf-8")
            broken_binary = tmp / "broken.h8bin"
            broken_binary.write_bytes(b"")
            result = self.run_validator(tmp, "--fail-fast", "--csv-diff", str(source_csv), str(broken_binary))
            self.assertNotEqual(result.returncode, 0)
            self.assertNotIn("Traceback", result.stderr)
            self.assertIn("ITEM_HASH_FILE_EMPTY", self.read_codes(tmp))

    def test_csv_diff_corrupt_probe_stops_before_missing_hash_noise(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            make_blob(tmp / "StreamingAssets/sample.h8bin")
            source_csv = tmp / "source.csv"
            source_csv.write_text("Hash32\n0xAABBCCDD\n", encoding="utf-8")
            broken_binary = tmp / "broken.h8bin"
            make_blob(broken_binary)
            patch_blob(broken_binary, 80 + 12, "<I", 10000)
            result = self.run_validator(tmp, "--csv-diff", str(source_csv), str(broken_binary))
            self.assertNotEqual(result.returncode, 0)
            self.assertNotIn("Traceback", result.stderr)
            codes = self.read_codes(tmp)
            self.assertIn("SECTION_OUT_OF_FILE", codes)
            self.assertNotIn("CSV_TO_BIN_MISSING_HASHES", codes)

    def test_known_hash_list_missing_fails_with_report(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            make_blob(tmp / "StreamingAssets/sample.h8bin")
            result = self.run_validator(tmp, "--known-hash-list", str(tmp / "missing_hashes.txt"))
            self.assertNotEqual(result.returncode, 0)
            self.assertNotIn("Traceback", result.stderr)
            self.assertIn("KNOWN_HASH_LIST_MISSING", self.read_codes(tmp))

    def test_known_hash_list_invalid_fails_with_report(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            make_blob(tmp / "StreamingAssets/sample.h8bin")
            hashes = tmp / "hashes.txt"
            hashes.write_text("0xAABBCCDD\nnot_a_hash\n0\n", encoding="utf-8")
            result = self.run_validator(tmp, "--known-hash-list", str(hashes))
            self.assertNotEqual(result.returncode, 0)
            codes = self.read_codes(tmp)
            self.assertIn("KNOWN_HASH_LIST_INVALID", codes)
            self.assertIn("KNOWN_HASH_LIST_ZERO", codes)

    def test_junit_records_missing_payload_failures(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            tmp = Path(raw)
            write_schema(tmp / "Scripts")
            (tmp / "StreamingAssets").mkdir()
            report = tmp / "report.json"
            junit = tmp / "report.junit.xml"
            metrics = tmp / "ci.log"
            command = [
                sys.executable,
                str(VALIDATOR),
                "--target-dir",
                str(tmp / "StreamingAssets"),
                "--cs-source-dir",
                str(tmp / "Scripts"),
                "--runtime-source-dir",
                str(tmp / "Scripts"),
                "--report-json",
                str(report),
                "--report-junit",
                str(junit),
                "--metrics-log",
                str(metrics),
            ]
            result = subprocess.run(command, text=True, capture_output=True, timeout=30)
            self.assertNotEqual(result.returncode, 0)
            text = junit.read_text(encoding="utf-8")
            self.assertIn("STATIC_DATA_MISSING", text)
            self.assertIn("NO_H8BIN_FILES", text)
            suite = ET.parse(junit).getroot()
            self.assertEqual(suite.attrib["tests"], str(len(suite.findall("testcase"))))


if __name__ == "__main__":
    unittest.main()
