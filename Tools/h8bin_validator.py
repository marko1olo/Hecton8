#!/usr/bin/env python3
"""External HECTON-8 .h8bin schema validator.

Pure Python CI tool. It parses C# explicit layouts, validates Data Monolith
headers and section tables through mmap, and emits JSON/JUnit proof artifacts.
It deliberately does not import Unity or compiled C# assemblies.
"""

from __future__ import annotations

import argparse
import concurrent.futures
import csv
import itertools
import json
import math
import mmap
import os
import re
import struct
import sys
import time
import tempfile
import threading
import xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Iterable, Iterator


TOOL_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = TOOL_DIR.parent
if str(TOOL_DIR) not in sys.path:
    sys.path.insert(0, str(TOOL_DIR))

try:
    from Security.ReplayHasher import xxh3_64
except Exception as exc:  # pragma: no cover - fatal CI setup failure.
    raise SystemExit(f"[h8bin_validator] failed to import pure-Python XXH3 oracle: {exc}") from exc


MASK32 = 0xFFFFFFFF
DEFAULT_MAGIC = 0x4D443848  # H8DM, little-endian bytes 48 38 44 4D.
DEFAULT_FORMAT_VERSION = 1
DEFAULT_HEADER_BYTES = 16
DEFAULT_DIRECTORY_BYTES = 64
DEFAULT_SECTION_ALIGNMENT = 16
DEFAULT_STATIC_DATA_RELATIVE = Path("Hecton8/DataMonolith/static_data.h8bin")
DEFAULT_RUNTIME_MAX_BYTES = 256 * 1024 * 1024
BINARY_SCHEMA_AUDIT_REPORT = "Docs/Reports/BINARY_SCHEMA_AUDIT_REPORT.json"
METRIC_PHI_BINARY_SCHEMA_ROWS = "Docs/Reports/METRIC_PHI_DATA_TRUTH_AUDIT.json"
AST_CACHE_RELATIVE = Path("Docs/Reports/.h8bin_validator_ast_cache_SHINOBU_358.json")
AST_CACHE_SCHEMA = "h8bin_validator.ast_cache.v3"
WATCHDOG_SECONDS = 10.0
AUP_BOUND_METERS = 100000.0
TEXT_RUNTIME_SUFFIXES = {".csv", ".json", ".xml"}
VOCAL_BANK_MAGIC = 0x42563848  # H8VB, little-endian bytes 48 38 56 42.
VOCAL_BANK_VERSION = 1
VOCAL_BANK_HEADER_SIZE = 64
VOCAL_BANK_RECORD_SIZE = 32
VOCAL_BANK_PAYLOAD_ALIGNMENT = 16
VOCAL_BANK_ENDIAN = 0xFEFF
VOCAL_BANK_BLOCK_SAMPLES = 64
VOCAL_BANK_CODEC_PCM16 = 0
VOCAL_BANK_CODEC_H8ADPCM = 1
VOCAL_BANK_CODEC_VORBIS = 2
VOCAL_BANK_SUPPORTED_CODECS = {VOCAL_BANK_CODEC_PCM16, VOCAL_BANK_CODEC_H8ADPCM}
VOCAL_BANK_KNOWN_CODECS = VOCAL_BANK_SUPPORTED_CODECS | {VOCAL_BANK_CODEC_VORBIS}
FNV_OFFSET = 2166136261
FNV_PRIME = 16777619
RUNTIME_TEXT_LOADER_TOKENS = (
    "path.combine",
    "application.streamingassetspath",
    "file.",
    "directory.",
    "streamreader",
    "assets/streamingassets",
    "assets\\streamingassets",
)
REFERENCE_SCAN_ROOTS = (
    PROJECT_ROOT / "Assets/_Project/Scripts",
    PROJECT_ROOT / "Docs/ARCHITECTURE",
)
_REFERENCE_TEXT_CACHE: list[tuple[str, list[str]]] | None = None

MIGRATION_ROUTE_RULES: tuple[dict[str, Any], ...] = (
    {
        "tokens": ("signal_tuning", "signal_corridor", "signalwardenruntime"),
        "owner": "Core/Signals",
        "source_root": "Assets/_SourceData/Signals",
        "runtime_route": "Data Monolith section or signal-domain .h8bin loaded into Vault at boot",
    },
    {
        "tokens": ("auxiliary_equipment", "auxiliaryequipment"),
        "owner": "Equipment/Auxiliary",
        "source_root": "Assets/_SourceData/Equipment/Auxiliary",
        "runtime_route": "equipment-domain .h8bin or Data Monolith equipment section loaded into Vault at boot",
    },
    {
        "tokens": ("hadal_structure", "hadalgraphs", "hadalstructure"),
        "owner": "World/OfflineHadalArchBaker",
        "source_root": "Assets/_SourceData/HadalGraphs",
        "runtime_route": "baked hadal graph/mesh binary artifact, never runtime CSV",
    },
    {
        "tokens": ("storm_depth", "weather_profiles", "beaufort", "stormpropagation", "oceansurfaceatmosphere"),
        "owner": "Atmosphere",
        "source_root": "Assets/_SourceData/Atmosphere",
        "runtime_route": "atmosphere-domain .h8bin or Data Monolith atmosphere section loaded at boot",
    },
    {
        "tokens": ("aup_constants", "auporiginshift"),
        "owner": "Core/Origin",
        "source_root": "Assets/_SourceData/Core/Origin",
        "runtime_route": "core bootstrap binary constants loaded before AUP runtime activation",
    },
    {
        "tokens": ("leviathan_rig", "faunakinematics"),
        "owner": "Fauna",
        "source_root": "Assets/_SourceData/Fauna",
        "runtime_route": "fauna-domain .h8bin or Data Monolith creature/rig section loaded into Vault",
    },
    {
        "tokens": ("logistics_components", "shinobulogisticsrouter"),
        "owner": "Power/Logistics",
        "source_root": "Assets/_SourceData/Power",
        "runtime_route": "power-domain .h8bin loaded into logistics Vault buffers at boot",
    },
    {
        "tokens": ("heat_source_profiles", "hazard_profiles", "thermodynamics"),
        "owner": "Thermodynamics",
        "source_root": "Assets/_SourceData/Thermodynamics",
        "runtime_route": "thermodynamics-domain .h8bin loaded into thermal/hazard Vault buffers",
    },
    {
        "tokens": ("terminal_layouts", "terminalosruntime"),
        "owner": "UI/TerminalOS",
        "source_root": "Assets/_SourceData/UI/TerminalOS",
        "runtime_route": "HudLayouts section in static_data.h8bin or UI-domain binary table",
    },
    {
        "tokens": ("locomotion_environment", "kccenvironmentprofile", "hydrodynamickcc"),
        "owner": "Physics/KCC",
        "source_root": "Assets/_SourceData/Physics/KCC",
        "runtime_route": "physics-domain .h8bin or Data Monolith locomotion section loaded into KCC Vault buffers",
    },
)


PRIMITIVE_TYPES: dict[str, tuple[int, int, str]] = {
    "byte": (1, 1, "B"),
    "sbyte": (1, 1, "b"),
    "bool": (1, 1, "?"),
    "short": (2, 2, "h"),
    "ushort": (2, 2, "H"),
    "int": (4, 4, "i"),
    "uint": (4, 4, "I"),
    "long": (8, 8, "q"),
    "ulong": (8, 8, "Q"),
    "float": (4, 4, "f"),
    "double": (8, 8, "d"),
}

CS_NUMERIC_CAST_TYPES = {
    "byte",
    "sbyte",
    "short",
    "ushort",
    "int",
    "uint",
    "long",
    "ulong",
}

CS_FIELD_MODIFIERS = {
    "public",
    "private",
    "protected",
    "internal",
    "readonly",
    "volatile",
    "unsafe",
    "new",
    "extern",
}

CS_STATIC_FIELD_MARKERS = {
    "static",
    "const",
}

ARM64_ALIGNMENT_CODES = {
    "FIELD_ALIGNMENT",
    "FIELD_NEGATIVE_OFFSET",
    "FIELD_OUT_OF_STRUCT",
    "FIELD_OVERLAP",
    "STRUCT_SIZE_ALIGNMENT",
    "PACK_1_FORBIDDEN",
    "STRUCT_UNRESOLVED_FIELD",
    "STRUCT_LAYOUT_MISSING",
    "STRUCT_SIZE_MISSING",
    "STRUCT_SIZE_UNRESOLVED",
    "FIELD_OFFSET_MISSING",
    "FIELD_OFFSET_UNRESOLVED",
    "FIELD_DECL_UNPARSED",
}

PROPERTY_BAN_CODES = {
    "STRUCT_PROPERTY_BANNED",
}

SCHEMA_MISMATCH_CODES = {
    "RECORD_SIZE_MISMATCH",
    "RECORD_SIZE_UNDER_STRUCT",
    "RECORD_SIZE_ZERO",
    "SECTION_RECORD_SIZE",
    "SECTION_COUNT",
    "SECTION_ID",
    "SECTION_OUT_OF_FILE",
    "SECTION_OVERLAP",
    "CSV_TO_BIN_VALUE_MISMATCH",
    "CSV_TO_BIN_MISSING_HASHES",
}

VECTOR_TYPES: dict[str, tuple[int, int]] = {
    "float2": (8, 4),
    "float3": (12, 4),
    "float4": (16, 4),
    "double2": (16, 8),
    "double3": (24, 8),
    "double4": (32, 8),
    "int2": (8, 4),
    "int3": (12, 4),
    "int4": (16, 4),
    "uint2": (8, 4),
    "uint3": (12, 4),
    "uint4": (16, 4),
    "long2": (16, 8),
    "long3": (24, 8),
    "long4": (32, 8),
    "ulong2": (16, 8),
    "ulong3": (24, 8),
    "ulong4": (32, 8),
    "float4x4": (64, 4),
}

HASH_FIELD_NAMES = {
    "HashId",
    "ItemHash",
    "SpeciesHash",
    "BiomeHash",
    "NodeHash",
    "FromNodeHash",
    "ToNodeHash",
    "TableHash",
    "VoxelHash",
    "EventHash",
    "EffectHash",
    "ToolHash",
    "PartHash",
    "TriggerHash",
    "SurfaceHash",
    "ModuleHash",
    "CellHash",
    "EntityHash",
    "ErrorHash",
    "ElementHash",
    "SectorHash",
    "OutputHash",
}

REFERENCE_FIELD_NAMES = {
    "OutputHash",
    "IngredientHash0",
    "IngredientHash1",
    "IngredientHash2",
    "IngredientHash3",
    "ItemHash",
}
CSV_DIFF_HASH_KEYS = ("hash32", "hash", "hash_id", "id", "item_id", "name")
CSV_ITEM_FIELD_ALIASES: dict[str, str] = {
    "cost": "Cost",
    "stackmax": "MaxStack",
    "stack_max": "MaxStack",
    "maxstack": "MaxStack",
    "max_stack": "MaxStack",
    "masskg": "MassKg",
    "mass_kg": "MassKg",
    "volumem3": "VolumeM3",
    "volume_m3": "VolumeM3",
    "basequality": "BaseQuality",
    "base_quality": "BaseQuality",
    "heatcapacity": "HeatCapacity",
    "heat_capacity": "HeatCapacity",
    "categoryhash": "CategoryHash",
    "category_hash": "CategoryHash",
    "yieldhash": "YieldHash",
    "yield_hash": "YieldHash",
}

SECTION_STRUCT_HINTS = {
    "Items": "H8ItemRecord",
    "Creatures": "H8CreatureTraitRecord",
    "Biomes": "H8BiomeRecord",
    "Economy": "H8EconomyRecord",
    "PhysicsConstants": "H8PhysicsConstantsRecord",
}


class FailFastAbort(Exception):
    pass


@dataclass
class Finding:
    severity: str
    code: str
    message: str
    path: str = ""
    offset: int | None = None
    hexdump: str = ""
    layout: str = ""
    remediation: str = ""
    references: list[str] = field(default_factory=list)

    def to_json(self) -> dict[str, Any]:
        data: dict[str, Any] = {
            "severity": self.severity,
            "code": self.code,
            "message": self.message,
        }
        if self.path:
            data["path"] = self.path
        if self.offset is not None:
            data["offset"] = self.offset
        if self.hexdump:
            data["hexdump"] = self.hexdump
        if self.layout:
            data["layout"] = self.layout
        if self.remediation:
            data["remediation"] = self.remediation
        if self.references:
            data["references"] = self.references
        return data


@dataclass
class FieldLayout:
    name: str
    type_name: str
    offset: int
    size: int = 0
    alignment: int = 1
    struct_format: str = ""
    line: int = 0

    @property
    def end(self) -> int:
        return self.offset + self.size


@dataclass
class StructLayout:
    name: str
    size_expr: str
    size: int
    source_path: Path
    attribute: str
    line: int = 0
    fields: list[FieldLayout] = field(default_factory=list)
    largest_alignment: int = 1
    final_padding: int = 0
    payload_padding: int = 0
    resolved: bool = False

    def describe(self) -> str:
        lines = [f"{self.name} size={self.size} align={self.largest_alignment}"]
        for item in sorted(self.fields, key=lambda f: f.offset):
            lines.append(
                f"  +0x{item.offset:02X} {item.type_name} {item.name} size={item.size} align={item.alignment}"
            )
        lines.append(f"  final_padding={self.final_padding} payload_padding={self.payload_padding}")
        return "\n".join(lines)


@dataclass(frozen=True, slots=True)
class CSharpToken:
    value: str
    line: int


@dataclass
class SectionSchema:
    section_id: int
    name: str
    expected_record_size: int
    struct_name: str = ""


@dataclass
class FileMetrics:
    path: str
    bytes: int = 0
    sections: int = 0
    sampled_records: int = 0
    status: str = "NOT_CHECKED"


@dataclass
class SchemaModel:
    constants: dict[str, int]
    enum_sections: dict[str, int]
    section_order: list[str]
    structs: dict[str, StructLayout]
    expected_sizes: dict[str, int]
    section_structs: dict[str, str]

    def constant(self, name: str, default: int) -> int:
        return self.constants.get(name, self.constants.get(f"H8DataLayoutConstants.{name}", default))


@dataclass
class ValidationState:
    args: argparse.Namespace
    schema: SchemaModel
    findings: list[Finding] = field(default_factory=list)
    files: list[FileMetrics] = field(default_factory=list)
    bytes_processed: int = 0
    start_time: float = field(default_factory=time.perf_counter)

    def add_error(
        self,
        code: str,
        message: str,
        path: str = "",
        offset: int | None = None,
        hexdump: str = "",
        layout: str = "",
        remediation: str = "",
        references: list[str] | None = None,
    ) -> None:
        self.findings.append(
            Finding(
                "ERROR",
                code,
                message,
                path,
                offset,
                hexdump,
                layout,
                remediation,
                references or [],
            )
        )
        if self.args.fail_fast:
            raise FailFastAbort(message)

    def add_info(self, code: str, message: str, path: str = "") -> None:
        self.findings.append(Finding("INFO", code, message, path))

    @property
    def ok(self) -> bool:
        return not any(f.severity == "ERROR" for f in self.findings)


def resolve_path(value: str | Path) -> Path:
    path = Path(value)
    if path.is_absolute():
        return path
    project_path = PROJECT_ROOT / path
    if project_path.exists() or not path.exists():
        return project_path
    return path.resolve()


def normalize_type(type_name: str) -> str:
    type_name = type_name.strip()
    if "." in type_name:
        type_name = type_name.rsplit(".", 1)[-1]
    return type_name.replace("readonly", "").strip()


def sanitize_int_expr(expr: str, constants: dict[str, int]) -> str:
    tokens = tokenize_csharp_bytes(expr.encode("utf-8", errors="ignore"))
    sanitized: list[str] = []
    index = 0
    while index < len(tokens):
        value = tokens[index].value
        if (
            value == "("
            and index + 2 < len(tokens)
            and tokens[index + 1].value in CS_NUMERIC_CAST_TYPES
            and tokens[index + 2].value == ")"
        ):
            index += 3
            continue
        if is_identifier_value(value):
            parts = [value]
            cursor = index
            while cursor + 2 < len(tokens) and tokens[cursor + 1].value == "." and is_identifier_value(tokens[cursor + 2].value):
                parts.extend([".", tokens[cursor + 2].value])
                cursor += 2
            qualified = "".join(parts)
            if qualified in constants:
                sanitized.append(str(constants[qualified]))
                index = cursor + 1
                continue
            bare = qualified.rsplit(".", 1)[-1]
            if bare in constants:
                sanitized.append(str(constants[bare]))
                index = cursor + 1
                continue
            raise ValueError(f"unsupported integer symbol: {qualified!r}")
        if value and (value[0].isdigit() or value.startswith("0x") or value.startswith("0X")):
            sanitized.append(strip_numeric_suffix(value))
            index += 1
            continue
        if value in {"+", "-", "*", "/", "%", "(", ")", "<<", ">>", "<", ">", "&", "|", "~"}:
            sanitized.append(value)
            index += 1
            continue
        raise ValueError(f"unsupported integer expression token: {value!r}")
    return " ".join(sanitized)


def eval_int_expr(expr: str, constants: dict[str, int]) -> int:
    sanitized = sanitize_int_expr(expr, constants)
    value = eval(sanitized, {"__builtins__": {}}, {})  # noqa: S307 - sanitized arithmetic only.
    return int(value)


def collect_csharp_files(paths: Iterable[Path]) -> list[Path]:
    files: list[Path] = []
    for root in paths:
        if root.is_file() and root.suffix.lower() == ".cs":
            files.append(root)
        elif root.exists():
            files.extend(sorted(root.rglob("*.cs")))
    return sorted(set(files))


def is_identifier_start(byte_value: int) -> bool:
    return byte_value == 95 or 65 <= byte_value <= 90 or 97 <= byte_value <= 122 or byte_value >= 128


def is_identifier_part(byte_value: int) -> bool:
    return is_identifier_start(byte_value) or 48 <= byte_value <= 57


def is_identifier_value(value: str) -> bool:
    if not value:
        return False
    first = value[0]
    return first == "_" or first == "@" or first.isalpha()


def decode_token(data: bytes | mmap.mmap, start: int, end: int) -> str:
    return bytes(data[start:end]).decode("utf-8", errors="ignore")


def skip_quoted_literal(data: bytes | mmap.mmap, index: int, line: int, quote: int) -> tuple[int, int]:
    index += 1
    escaped = False
    length = len(data)
    while index < length:
        char = data[index]
        if char == 10:
            line += 1
        if escaped:
            escaped = False
        elif char == 92:
            escaped = True
        elif char == quote:
            return index + 1, line
        index += 1
    return index, line


def skip_verbatim_string(data: bytes | mmap.mmap, index: int, line: int) -> tuple[int, int]:
    index += 2
    length = len(data)
    while index < length:
        char = data[index]
        if char == 10:
            line += 1
        if char == 34:
            if index + 1 < length and data[index + 1] == 34:
                index += 2
                continue
            return index + 1, line
        index += 1
    return index, line


def tokenize_csharp_bytes(data: bytes | mmap.mmap) -> list[CSharpToken]:
    tokens: list[CSharpToken] = []
    index = 0
    line = 1
    length = len(data)
    while index < length:
        char = data[index]
        if char in (9, 11, 12, 13, 32):
            index += 1
            continue
        if char == 10:
            line += 1
            index += 1
            continue
        if char == 47 and index + 1 < length:
            nxt = data[index + 1]
            if nxt == 47:
                index += 2
                while index < length and data[index] != 10:
                    index += 1
                continue
            if nxt == 42:
                index += 2
                while index + 1 < length:
                    if data[index] == 10:
                        line += 1
                    if data[index] == 42 and data[index + 1] == 47:
                        index += 2
                        break
                    index += 1
                continue
        if char == 36 and index + 2 < length and data[index + 1] == 64 and data[index + 2] == 34:
            index, line = skip_verbatim_string(data, index + 1, line)
            tokens.append(CSharpToken('""', line))
            continue
        if char == 64 and index + 1 < length and data[index + 1] == 34:
            index, line = skip_verbatim_string(data, index, line)
            tokens.append(CSharpToken('""', line))
            continue
        if char == 36 and index + 1 < length and data[index + 1] == 34:
            index, line = skip_quoted_literal(data, index + 1, line, 34)
            tokens.append(CSharpToken('""', line))
            continue
        if char in (34, 39):
            index, line = skip_quoted_literal(data, index, line, char)
            tokens.append(CSharpToken('""', line))
            continue
        if is_identifier_start(char) or (char == 64 and index + 1 < length and is_identifier_start(data[index + 1])):
            start = index
            index += 1
            while index < length and is_identifier_part(data[index]):
                index += 1
            tokens.append(CSharpToken(decode_token(data, start, index), line))
            continue
        if 48 <= char <= 57:
            start = index
            index += 1
            while index < length:
                nxt = data[index]
                if is_identifier_part(nxt) or nxt in (46,):
                    index += 1
                    continue
                break
            tokens.append(CSharpToken(decode_token(data, start, index), line))
            continue
        if char == 61 and index + 1 < length and data[index + 1] == 62:
            tokens.append(CSharpToken("=>", line))
            index += 2
            continue
        if char == 58 and index + 1 < length and data[index + 1] == 58:
            tokens.append(CSharpToken("::", line))
            index += 2
            continue
        if char in (60, 62) and index + 1 < length and data[index + 1] == char:
            tokens.append(CSharpToken(chr(char) * 2, line))
            index += 2
            continue
        tokens.append(CSharpToken(chr(char), line))
        index += 1
    return tokens


def tokenize_csharp_file(path: Path) -> list[CSharpToken]:
    if not path.exists() or path.stat().st_size == 0:
        return []
    with path.open("rb") as handle:
        with mmap.mmap(handle.fileno(), 0, access=mmap.ACCESS_READ) as mm_obj:
            return tokenize_csharp_bytes(mm_obj)


def file_contains_any(path: Path, needles: tuple[bytes, ...]) -> bool:
    try:
        if not path.exists() or path.stat().st_size == 0:
            return False
        with path.open("rb") as handle:
            with mmap.mmap(handle.fileno(), 0, access=mmap.ACCESS_READ) as mm_obj:
                for needle in needles:
                    if mm_obj.find(needle) >= 0:
                        return True
    except OSError:
        return False
    return False


def file_contains_all_groups(path: Path, groups: tuple[tuple[bytes, ...], ...]) -> bool:
    try:
        if not path.exists() or path.stat().st_size == 0:
            return False
        with path.open("rb") as handle:
            with mmap.mmap(handle.fileno(), 0, access=mmap.ACCESS_READ) as mm_obj:
                for group in groups:
                    if not any(mm_obj.find(needle) >= 0 for needle in group):
                        return False
    except OSError:
        return False
    return True


def filter_files_by_any(files: list[Path], needles: tuple[bytes, ...]) -> list[Path]:
    if not files:
        return []
    worker_count = max(1, min(32, (os.cpu_count() or 1) + 4, len(files)))
    selected: list[Path] = []
    with concurrent.futures.ThreadPoolExecutor(max_workers=worker_count, thread_name_prefix="h8bin-prefilter") as executor:
        future_map = {executor.submit(file_contains_any, path, needles): path for path in files}
        for future in concurrent.futures.as_completed(future_map):
            if future.result():
                selected.append(future_map[future])
    return sorted(selected)


def is_ident_byte(byte_value: int) -> bool:
    return byte_value == 95 or 48 <= byte_value <= 57 or 65 <= byte_value <= 90 or 97 <= byte_value <= 122


def is_word_at(data: bytes | mmap.mmap, index: int, word: bytes) -> bool:
    end = index + len(word)
    if index < 0 or end > len(data) or data[index:end] != word:
        return False
    if index > 0 and is_ident_byte(data[index - 1]):
        return False
    if end < len(data) and is_ident_byte(data[end]):
        return False
    return True


def find_matching_brace_bytes(data: bytes | mmap.mmap, start: int) -> int:
    depth = 0
    index = start
    length = len(data)
    quote = 0
    escaped = False
    verbatim = False
    while index < length:
        char = data[index]
        if quote:
            if verbatim:
                if char == 34:
                    if index + 1 < length and data[index + 1] == 34:
                        index += 2
                        continue
                    quote = 0
                    verbatim = False
            elif escaped:
                escaped = False
            elif char == 92:
                escaped = True
            elif char == quote:
                quote = 0
            index += 1
            continue
        if char == 47 and index + 1 < length:
            nxt = data[index + 1]
            if nxt == 47:
                index += 2
                while index < length and data[index] != 10:
                    index += 1
                continue
            if nxt == 42:
                index += 2
                while index + 1 < length and not (data[index] == 42 and data[index + 1] == 47):
                    index += 1
                index += 2
                continue
        if char == 64 and index + 1 < length and data[index + 1] == 34:
            quote = 34
            verbatim = True
            index += 2
            continue
        if char in (34, 39):
            quote = char
            index += 1
            continue
        if char == 123:
            depth += 1
        elif char == 125:
            depth -= 1
            if depth == 0:
                return index
        index += 1
    return -1


def body_contains_direct_word(data: bytes | mmap.mmap, start: int, end: int, word: bytes) -> bool:
    depth = 0
    index = start
    quote = 0
    escaped = False
    verbatim = False
    length = min(end, len(data))
    while index < length:
        char = data[index]
        if quote:
            if verbatim:
                if char == 34:
                    if index + 1 < length and data[index + 1] == 34:
                        index += 2
                        continue
                    quote = 0
                    verbatim = False
            elif escaped:
                escaped = False
            elif char == 92:
                escaped = True
            elif char == quote:
                quote = 0
            index += 1
            continue
        if char == 47 and index + 1 < length:
            nxt = data[index + 1]
            if nxt == 47:
                index += 2
                while index < length and data[index] != 10:
                    index += 1
                continue
            if nxt == 42:
                index += 2
                while index + 1 < length and not (data[index] == 42 and data[index + 1] == 47):
                    index += 1
                index += 2
                continue
        if char == 64 and index + 1 < length and data[index + 1] == 34:
            quote = 34
            verbatim = True
            index += 2
            continue
        if char in (34, 39):
            quote = char
            index += 1
            continue
        if char == 123:
            depth += 1
            index += 1
            continue
        if char == 125:
            if depth > 0:
                depth -= 1
            index += 1
            continue
        if depth == 0 and is_word_at(data, index, word):
            return True
        index += 1
    return False


def find_attribute_span_start(data: bytes | mmap.mmap, struct_index: int) -> int:
    search_start = max(0, struct_index - 2048)
    struct_layout_start = data.rfind(b"[", search_start, struct_index)
    while struct_layout_start >= 0:
        if data.find(b"StructLayout", struct_layout_start, struct_index) >= 0:
            return struct_layout_start
        struct_layout_start = data.rfind(b"[", search_start, struct_layout_start)
    cursor = struct_index
    while cursor > 0 and data[cursor - 1] in b" \t\r\n":
        cursor -= 1
    while cursor > 0 and data[cursor - 1] == 93:
        depth = 0
        index = cursor - 1
        while index >= 0:
            char = data[index]
            if char == 93:
                depth += 1
            elif char == 91:
                depth -= 1
                if depth == 0:
                    cursor = index
                    while cursor > 0 and data[cursor - 1] in b" \t\r\n":
                        cursor -= 1
                    break
            index -= 1
        else:
            break
    return cursor


def extract_struct_candidate_spans(path: Path) -> list[tuple[bytes, int]]:
    spans: list[tuple[bytes, int]] = []
    if not path.exists() or path.stat().st_size == 0:
        return spans
    with path.open("rb") as handle:
        with mmap.mmap(handle.fileno(), 0, access=mmap.ACCESS_READ) as mm_obj:
            cursor = 0
            line = 1
            while True:
                index = mm_obj.find(b"struct", cursor)
                if index < 0:
                    break
                line += mm_obj[cursor:index].count(b"\n")
                cursor = index + 6
                if not is_word_at(mm_obj, index, b"struct"):
                    continue
                line_start = mm_obj.rfind(b"\n", 0, index) + 1
                if mm_obj.find(b"//", line_start, index) >= 0:
                    continue
                name_cursor = index + 6
                length = len(mm_obj)
                while name_cursor < length and mm_obj[name_cursor] in b" \t\r\n":
                    name_cursor += 1
                if name_cursor >= length:
                    break
                name_char = mm_obj[name_cursor + 1] if mm_obj[name_cursor] == 64 and name_cursor + 1 < length else mm_obj[name_cursor]
                if not is_identifier_start(name_char):
                    continue
                open_brace = mm_obj.find(b"{", name_cursor)
                if open_brace < 0:
                    break
                close_brace = find_matching_brace_bytes(mm_obj, open_brace)
                if close_brace < 0:
                    cursor = open_brace + 1
                    continue
                start = find_attribute_span_start(mm_obj, index)
                if (
                    mm_obj.find(b"LayoutKind.Explicit", start, open_brace) >= 0
                    or body_contains_direct_word(mm_obj, open_brace + 1, close_brace, b"FieldOffset")
                ):
                    base_line = max(1, line - mm_obj[start:index].count(b"\n"))
                    spans.append((bytes(mm_obj[start : close_brace + 1]), base_line))
                cursor = index + 6
    return spans


def tokenize_csharp_span_fast(data: bytes, base_line: int = 1) -> list[CSharpToken]:
    tokens = tokenize_csharp_bytes(data)
    if base_line <= 1:
        return tokens
    line_delta = base_line - 1
    return [CSharpToken(token.value, token.line + line_delta) for token in tokens]


def token_values(tokens: list[CSharpToken]) -> list[str]:
    return [token.value for token in tokens]


def tokens_to_text(tokens: list[CSharpToken]) -> str:
    return " ".join(token.value for token in tokens)


def find_matching_token(tokens: list[CSharpToken], start: int, open_value: str, close_value: str) -> int:
    depth = 0
    for index in range(start, len(tokens)):
        value = tokens[index].value
        if value == open_value:
            depth += 1
        elif value == close_value:
            depth -= 1
            if depth == 0:
                return index
    return -1


def attribute_name_matches(value: str, target: str) -> bool:
    if value.startswith("@"):
        value = value[1:]
    return value == target or value == f"{target}Attribute"


def extract_attribute_args(attribute_blocks: list[list[CSharpToken]], attribute_name: str) -> list[CSharpToken] | None:
    for block in attribute_blocks:
        index = 0
        while index < len(block):
            if attribute_name_matches(block[index].value, attribute_name):
                if index + 1 < len(block) and block[index + 1].value == "(":
                    end = find_matching_token(block, index + 1, "(", ")")
                    if end >= 0:
                        return block[index + 2 : end]
            index += 1
    return None


def attribute_tokens_text(attribute_blocks: list[list[CSharpToken]], attribute_name: str) -> str:
    args = extract_attribute_args(attribute_blocks, attribute_name)
    if args is None:
        return ""
    return f"{attribute_name}({tokens_to_text(args)})"


def args_contain_layout_explicit(args: list[CSharpToken]) -> bool:
    for index, token in enumerate(args):
        if token.value == "Explicit":
            if index >= 2 and args[index - 2].value == "LayoutKind" and args[index - 1].value == ".":
                return True
            if index == 0 or args[index - 1].value in {"(", ",", "="}:
                return True
    return False


def split_top_level_arguments(tokens: list[CSharpToken]) -> list[list[CSharpToken]]:
    args: list[list[CSharpToken]] = []
    start = 0
    depth = 0
    for index, token in enumerate(tokens):
        value = token.value
        if value in {"(", "[", "{"}:
            depth += 1
        elif value in {")",
            "]",
            "}",
        } and depth > 0:
            depth -= 1
        elif value == "," and depth == 0:
            args.append(tokens[start:index])
            start = index + 1
    args.append(tokens[start:])
    return [arg for arg in args if arg]


def extract_named_argument_tokens(args: list[CSharpToken], name: str) -> list[CSharpToken] | None:
    for arg in split_top_level_arguments(args):
        if len(arg) >= 3 and arg[0].value == name and arg[1].value == "=":
            return arg[2:]
    return None


def extract_first_argument_tokens(args: list[CSharpToken]) -> list[CSharpToken] | None:
    split_args = split_top_level_arguments(args)
    if not split_args:
        return None
    first = split_args[0]
    if len(first) >= 2 and first[1].value == "=":
        return None
    return first


def expr_from_tokens(tokens: list[CSharpToken]) -> str:
    return " ".join(token.value for token in tokens)


def strip_numeric_suffix(value: str) -> str:
    if not value:
        return value
    end = len(value)
    while end > 0 and value[end - 1] in "uUlLfFdDmM":
        end -= 1
    return value[:end] or value


def resolve_constants(raw_constants: dict[str, str]) -> dict[str, int]:
    constants: dict[str, int] = {}
    pending = dict(raw_constants)
    for _ in range(max(1, len(pending) + 4)):
        progressed = False
        for key, expr in list(pending.items()):
            try:
                constants[key] = eval_int_expr(expr, constants)
                constants[key.rsplit(".", 1)[-1]] = constants[key]
                del pending[key]
                progressed = True
            except Exception:
                continue
        if not pending or not progressed:
            break
    return constants


class LazyConstants(dict[str, int]):
    def __init__(self, raw_constants: dict[str, str]) -> None:
        super().__init__()
        self._raw = raw_constants
        self._resolving: set[str] = set()

    def __contains__(self, key: object) -> bool:
        if not isinstance(key, str):
            return False
        return dict.__contains__(self, key) or key in self._raw or key.rsplit(".", 1)[-1] in self._raw

    def __getitem__(self, key: str) -> int:
        if dict.__contains__(self, key):
            return dict.__getitem__(self, key)
        return self._resolve(key)

    def get(self, key: str, default: Any = None) -> Any:
        try:
            return self[key]
        except Exception:
            return default

    def _resolve(self, key: str) -> int:
        bare = key.rsplit(".", 1)[-1]
        raw_key = key if key in self._raw else bare
        if raw_key not in self._raw:
            raise KeyError(key)
        if raw_key in self._resolving:
            raise ValueError(f"cyclic constant reference: {raw_key}")
        self._resolving.add(raw_key)
        try:
            value = eval_int_expr(self._raw[raw_key], self)
        finally:
            self._resolving.remove(raw_key)
        dict.__setitem__(self, raw_key, value)
        dict.__setitem__(self, bare, value)
        if "." in key:
            dict.__setitem__(self, key, value)
        return value


def add_parse_error(
    findings: list[Finding],
    code: str,
    message: str,
    path: Path,
    line: int = 0,
    layout: str = "",
    offset: int | None = None,
) -> None:
    rendered = f"line {line}: {message}" if line > 0 else message
    findings.append(Finding("ERROR", code, rendered, str(path), offset, layout=layout))


def strip_statement_initializer(tokens: list[CSharpToken]) -> list[CSharpToken]:
    depth = 0
    for index, token in enumerate(tokens):
        value = token.value
        if value in {"(", "[", "{"}:
            depth += 1
        elif value in {")",
            "]",
            "}",
        } and depth > 0:
            depth -= 1
        elif value == "=" and depth == 0:
            return tokens[:index]
    return tokens


def statement_contains_arrow_expression(tokens: list[CSharpToken]) -> int:
    for index, token in enumerate(tokens):
        if token.value == "=>":
            return token.line
        if token.value == "=" and index + 1 < len(tokens) and tokens[index + 1].value == ">":
            return token.line
    return 0


def parse_qualified_type(tokens: list[CSharpToken], start: int) -> tuple[str, int]:
    parts: list[str] = []
    index = start
    if index + 1 < len(tokens) and tokens[index].value == "global" and tokens[index + 1].value == "::":
        index += 2
    while index < len(tokens):
        if is_identifier_value(tokens[index].value):
            parts.append(tokens[index].value.lstrip("@"))
            index += 1
            if index < len(tokens) and tokens[index].value == ".":
                parts.append(".")
                index += 1
                continue
            break
        break
    return "".join(parts), index


def parse_field_tokens(tokens: list[CSharpToken]) -> tuple[str, str] | None:
    if statement_contains_arrow_expression(tokens):
        return None
    tokens = strip_statement_initializer(tokens)
    if not tokens:
        return None
    values = token_values(tokens)
    if any(value in CS_STATIC_FIELD_MARKERS for value in values):
        return None
    if "(" in values:
        return None
    index = 0
    while index < len(tokens) and tokens[index].value in CS_FIELD_MODIFIERS:
        index += 1
    if index < len(tokens) and tokens[index].value == "fixed":
        type_name, cursor = parse_qualified_type(tokens, index + 1)
        if not type_name or cursor >= len(tokens) or not is_identifier_value(tokens[cursor].value):
            return None
        name = tokens[cursor].value.lstrip("@")
        if cursor + 2 < len(tokens) and tokens[cursor + 1].value == "[":
            close_bracket = find_matching_token(tokens, cursor + 1, "[", "]")
            if close_bracket > cursor + 2:
                count_expr = expr_from_tokens(tokens[cursor + 2 : close_bracket])
                if count_expr:
                    return f"{type_name}[{count_expr}]", name
        return type_name, name
    clean = [token for token in tokens[index:] if token.value not in {"[", "]", ","}]
    identifier_indexes = [idx for idx, token in enumerate(clean) if is_identifier_value(token.value)]
    if len(identifier_indexes) < 2:
        return None
    name_index = identifier_indexes[-1]
    name = clean[name_index].value.lstrip("@")
    type_tokens = clean[:name_index]
    while type_tokens and type_tokens[0].value in CS_FIELD_MODIFIERS:
        type_tokens = type_tokens[1:]
    if not type_tokens:
        return None
    type_name = "".join(token.value for token in type_tokens if token.value not in {"<", ">", ","})
    return type_name, name


def contains_field_offset_attribute(attribute_blocks: list[list[CSharpToken]]) -> bool:
    return extract_attribute_args(attribute_blocks, "FieldOffset") is not None


def body_contains_fieldoffset(tokens: list[CSharpToken]) -> bool:
    depth = 0
    for token in tokens:
        if token.value == "{":
            depth += 1
            continue
        if token.value == "}":
            if depth > 0:
                depth -= 1
            continue
        if depth == 0 and attribute_name_matches(token.value, "FieldOffset"):
            return True
    return False


def detect_property_tokens(tokens: list[CSharpToken]) -> int:
    for index, token in enumerate(tokens):
        if token.value in {"get", "set", "init"} and index + 1 < len(tokens) and tokens[index + 1].value in {";", "{", "=>"}:
            return token.line
    return 0


def is_method_or_nested_type_header(tokens: list[CSharpToken]) -> bool:
    values = token_values(tokens)
    if "(" in values:
        return True
    return any(value in {"class", "struct", "interface", "enum", "record"} for value in values)


def parse_fieldoffset_fields(
    body_tokens: list[CSharpToken],
    constants: dict[str, int],
    path: Path,
    struct_name: str,
    explicit_layout: bool,
    attribute_text: str,
    findings: list[Finding],
) -> list[FieldLayout]:
    fields: list[FieldLayout] = []
    pending_attributes: list[list[CSharpToken]] = []
    cursor = 0
    statement_start = 0
    while cursor < len(body_tokens):
        value = body_tokens[cursor].value
        if value == "[" and cursor == statement_start:
            end = find_matching_token(body_tokens, cursor, "[", "]")
            if end < 0:
                break
            pending_attributes.append(body_tokens[cursor + 1 : end])
            cursor = end + 1
            statement_start = cursor
            continue
        if value == "{":
            close_brace = find_matching_token(body_tokens, cursor, "{", "}")
            statement_head = body_tokens[statement_start:cursor]
            property_line = 0
            if not is_method_or_nested_type_header(statement_head):
                property_line = detect_property_tokens(body_tokens[cursor + 1 : close_brace if close_brace >= 0 else len(body_tokens)])
            if property_line:
                add_parse_error(
                    findings,
                    "STRUCT_PROPERTY_BANNED",
                    f"{struct_name} contains get/set property tokens inside unmanaged layout contract",
                    path,
                    property_line,
                    attribute_text,
                )
            pending_attributes = []
            cursor = close_brace + 1 if close_brace >= 0 else cursor + 1
            statement_start = cursor
            continue
        if value != ";":
            cursor += 1
            continue
        statement = body_tokens[statement_start:cursor]
        property_line = statement_contains_arrow_expression(statement)
        if property_line and "(" not in token_values(statement):
            add_parse_error(
                findings,
                "STRUCT_PROPERTY_BANNED",
                f"{struct_name} contains expression-bodied property inside unmanaged layout contract",
                path,
                property_line,
                attribute_text,
            )
            pending_attributes = []
            cursor += 1
            statement_start = cursor
            continue
        field_offset_args = extract_attribute_args(pending_attributes, "FieldOffset")
        if field_offset_args is not None:
            parsed_decl = parse_field_tokens(statement)
            if not parsed_decl:
                line = statement[0].line if statement else body_tokens[cursor].line
                add_parse_error(
                    findings,
                    "FIELD_DECL_UNPARSED",
                    f"{struct_name} field declaration could not be parsed: {tokens_to_text(statement)}",
                    path,
                    line,
                    attribute_text,
                )
                pending_attributes = []
                cursor += 1
                statement_start = cursor
                continue
            offset_tokens = extract_first_argument_tokens(field_offset_args)
            if not offset_tokens:
                add_parse_error(
                    findings,
                    "FIELD_OFFSET_MISSING",
                    f"{struct_name}.{parsed_decl[1]} FieldOffset has no argument",
                    path,
                    statement[0].line if statement else body_tokens[cursor].line,
                    attribute_text,
                )
                pending_attributes = []
                cursor += 1
                statement_start = cursor
                continue
            try:
                offset = eval_int_expr(expr_from_tokens(offset_tokens), constants)
            except Exception as exc:
                add_parse_error(
                    findings,
                    "FIELD_OFFSET_UNRESOLVED",
                    f"{struct_name}.{parsed_decl[1]} offset failed: {exc}",
                    path,
                    statement[0].line if statement else body_tokens[cursor].line,
                    attribute_text,
                )
                pending_attributes = []
                cursor += 1
                statement_start = cursor
                continue
            fields.append(
                FieldLayout(
                    name=parsed_decl[1],
                    type_name=normalize_type(parsed_decl[0]),
                    offset=offset,
                    line=statement[0].line if statement else body_tokens[cursor].line,
                )
            )
        elif explicit_layout:
            parsed_decl = parse_field_tokens(statement)
            if parsed_decl:
                add_parse_error(
                    findings,
                    "FIELD_OFFSET_MISSING",
                    f"{struct_name}.{parsed_decl[1]} is inside explicit layout but has no FieldOffset",
                    path,
                    statement[0].line if statement else body_tokens[cursor].line,
                    attribute_text,
                )
        pending_attributes = []
        cursor += 1
        statement_start = cursor
    return fields


def parse_constants(files: list[Path]) -> dict[str, int]:
    raw: dict[str, str] = {}
    for path in filter_files_by_any(files, (b"const",)):
        try:
            if not path.exists() or path.stat().st_size == 0:
                continue
            handle = path.open("rb")
        except OSError:
            continue
        with handle:
            with mmap.mmap(handle.fileno(), 0, access=mmap.ACCESS_READ) as mm_obj:
                cursor = 0
                while True:
                    const_index = mm_obj.find(b"const", cursor)
                    if const_index < 0:
                        break
                    cursor = const_index + 5
                    if not is_word_at(mm_obj, const_index, b"const"):
                        continue
                    line_start = mm_obj.rfind(b"\n", 0, const_index) + 1
                    if mm_obj.find(b"//", line_start, const_index) >= 0:
                        continue
                    line_end = mm_obj.find(b"\n", const_index)
                    if line_end < 0:
                        line_end = len(mm_obj)
                    scan = bytes(mm_obj[line_start:line_end]).split(b"//", 1)[0]
                    after = scan[const_index - line_start :].strip()
                    parts = after.split(None, 3)
                    if len(parts) < 4:
                        continue
                    keyword = parts[0].decode("ascii", errors="ignore")
                    type_name = parts[1].decode("ascii", errors="ignore")
                    if keyword != "const" or type_name not in CS_NUMERIC_CAST_TYPES:
                        continue
                    name = parts[2].decode("ascii", errors="ignore").strip()
                    expr_part = parts[3]
                    equals = expr_part.find(b"=")
                    semicolon = expr_part.find(b";")
                    if equals < 0 or semicolon <= equals:
                        continue
                    expr = expr_part[equals + 1 : semicolon].decode("utf-8", errors="ignore").strip()
                    raw.setdefault(name, expr)
    return LazyConstants(raw)


def parse_enum_sections(files: list[Path]) -> dict[str, int]:
    result: dict[str, int] = {}
    for path in filter_files_by_any(files, (b"H8DataSectionId",)):
        tokens = tokenize_csharp_file(path)
        index = 0
        while index < len(tokens):
            if tokens[index].value != "enum" or index + 1 >= len(tokens) or tokens[index + 1].value != "H8DataSectionId":
                index += 1
                continue
            open_brace = index + 2
            while open_brace < len(tokens) and tokens[open_brace].value != "{":
                open_brace += 1
            close_brace = find_matching_token(tokens, open_brace, "{", "}") if open_brace < len(tokens) else -1
            if close_brace < 0:
                return result
            body = tokens[open_brace + 1 : close_brace]
            cursor = 0
            local_constants: dict[str, int] = {}
            next_value = 0
            while cursor < len(body):
                if not is_identifier_value(body[cursor].value):
                    cursor += 1
                    continue
                name = body[cursor].value.lstrip("@")
                cursor += 1
                if cursor < len(body) and body[cursor].value == "=":
                    expr_start = cursor + 1
                    expr_end = expr_start
                    while expr_end < len(body) and body[expr_end].value not in {",", "}"}:
                        expr_end += 1
                    value = eval_int_expr(expr_from_tokens(body[expr_start:expr_end]), local_constants)
                    cursor = expr_end
                else:
                    value = next_value
                result[name] = value
                local_constants[name] = value
                next_value = value + 1
                if cursor < len(body) and body[cursor].value == ",":
                    cursor += 1
            return result
            continue
    return result


def parse_section_order(files: list[Path], enum_sections: dict[str, int]) -> list[str]:
    for path in filter_files_by_any(files, (b"SectionOrder",)):
        tokens = tokenize_csharp_file(path)
        order: list[str] = []
        index = 0
        while index < len(tokens):
            if tokens[index].value != "SectionOrder":
                index += 1
                continue
            cursor = index + 1
            while cursor < len(tokens) and tokens[cursor].value != "{":
                cursor += 1
            close = find_matching_token(tokens, cursor, "{", "}") if cursor < len(tokens) else -1
            if close < 0:
                index += 1
                continue
            body = tokens[cursor + 1 : close]
            body_index = 0
            while body_index + 2 < len(body):
                if body[body_index].value == "H8DataSectionId" and body[body_index + 1].value == ".":
                    order.append(body[body_index + 2].value)
                    body_index += 3
                    continue
                body_index += 1
            if order:
                return order
            index = close + 1
        if order:
            return order
    return [name for name, _ in sorted(enum_sections.items(), key=lambda kv: kv[1])]


def parse_structs_from_tokens(
    path: Path,
    tokens: list[CSharpToken],
    constants: dict[str, int],
    first_struct_only: bool = False,
) -> tuple[list[StructLayout], list[Finding]]:
    structs: list[StructLayout] = []
    findings: list[Finding] = []
    pending_attributes: list[list[CSharpToken]] = []
    cursor = 0
    while cursor < len(tokens):
        value = tokens[cursor].value
        if value == "[":
            end = find_matching_token(tokens, cursor, "[", "]")
            if end < 0:
                break
            pending_attributes.append(tokens[cursor + 1 : end])
            cursor = end + 1
            continue
        if value == ";":
            pending_attributes = []
            cursor += 1
            continue
        if value != "struct":
            cursor += 1
            continue
        if cursor + 1 >= len(tokens) or not is_identifier_value(tokens[cursor + 1].value):
            pending_attributes = []
            cursor += 1
            continue
        name = tokens[cursor + 1].value.lstrip("@")
        open_brace = cursor + 2
        while open_brace < len(tokens) and tokens[open_brace].value != "{":
            open_brace += 1
        if open_brace >= len(tokens):
            pending_attributes = []
            break
        close_brace = find_matching_token(tokens, open_brace, "{", "}")
        if close_brace < 0:
            add_parse_error(findings, "STRUCT_BRACE_ERROR", f"{name} body is not closed", path, tokens[cursor].line)
            pending_attributes = []
            cursor = open_brace + 1
            continue
        body_tokens = tokens[open_brace + 1 : close_brace]
        struct_layout_args = extract_attribute_args(pending_attributes, "StructLayout")
        explicit_layout = struct_layout_args is not None and args_contain_layout_explicit(struct_layout_args)
        has_fieldoffset = body_contains_fieldoffset(body_tokens)
        if not explicit_layout and not has_fieldoffset:
            pending_attributes = []
            cursor = close_brace + 1
            continue
        attribute_text = attribute_tokens_text(pending_attributes, "StructLayout")
        if has_fieldoffset and not explicit_layout:
            add_parse_error(
                findings,
                "STRUCT_LAYOUT_MISSING",
                f"{name} contains FieldOffset fields but is not marked StructLayout(LayoutKind.Explicit)",
                path,
                tokens[cursor].line,
                attribute_text,
            )
        if struct_layout_args is None:
            pending_attributes = []
            cursor = close_brace + 1
            continue
        size_tokens = extract_named_argument_tokens(struct_layout_args, "Size")
        if not size_tokens:
            add_parse_error(
                findings,
                "STRUCT_SIZE_MISSING",
                f"{name} has explicit layout without Size",
                path,
                tokens[cursor].line,
                attribute_text,
            )
            pending_attributes = []
            cursor = close_brace + 1
            continue
        size_expr = expr_from_tokens(size_tokens)
        try:
            size = eval_int_expr(size_expr, constants)
        except Exception as exc:
            add_parse_error(
                findings,
                "STRUCT_SIZE_UNRESOLVED",
                f"{name} size expression failed: {exc}",
                path,
                tokens[cursor].line,
                attribute_text,
            )
            pending_attributes = []
            cursor = close_brace + 1
            continue
        pack_tokens = extract_named_argument_tokens(struct_layout_args, "Pack")
        if pack_tokens:
            try:
                pack_value = eval_int_expr(expr_from_tokens(pack_tokens), constants)
            except Exception:
                pack_value = 0
            if pack_value == 1:
                add_parse_error(findings, "PACK_1_FORBIDDEN", f"{name} uses Pack=1", path, tokens[cursor].line, attribute_text)
        layout = StructLayout(name, size_expr, size, path, attribute_text, line=tokens[cursor].line)
        layout.fields.extend(
            parse_fieldoffset_fields(body_tokens, constants, path, name, explicit_layout, attribute_text, findings)
        )
        structs.append(layout)
        if first_struct_only:
            return structs, findings
        pending_attributes = []
        cursor = close_brace + 1
    return structs, findings


def parse_csharp_file(path: Path, constants: dict[str, int]) -> tuple[list[StructLayout], list[Finding]]:
    parsed: list[StructLayout] = []
    findings: list[Finding] = []
    for span, base_line in extract_struct_candidate_spans(path):
        structs, span_findings = parse_structs_from_tokens(
            path,
            tokenize_csharp_span_fast(span, base_line),
            constants,
            first_struct_only=True,
        )
        parsed.extend(structs)
        findings.extend(span_findings)
    return parsed, findings


def parse_csharp_file_worker(payload: tuple[str, dict[str, int]]) -> tuple[str, list[StructLayout], list[Finding], str]:
    path_text, constants = payload
    path = Path(path_text)
    try:
        parsed_structs, findings = parse_csharp_file(path, constants)
    except Exception as exc:
        return path_text, [], [], str(exc)
    return path_text, parsed_structs, findings, ""


def ast_cache_path() -> Path:
    return PROJECT_ROOT / AST_CACHE_RELATIVE


def file_cache_key(path: Path) -> str:
    try:
        return path.relative_to(PROJECT_ROOT).as_posix()
    except ValueError:
        return str(path)


def file_cache_stamp(path: Path) -> dict[str, int]:
    stat = path.stat()
    return {"size": int(stat.st_size), "mtime_ns": int(stat.st_mtime_ns)}


def serialize_field_layout(field_layout: FieldLayout) -> dict[str, Any]:
    return {
        "name": field_layout.name,
        "type_name": field_layout.type_name,
        "offset": field_layout.offset,
        "line": field_layout.line,
    }


def deserialize_field_layout(value: dict[str, Any]) -> FieldLayout:
    return FieldLayout(
        name=str(value.get("name", "")),
        type_name=str(value.get("type_name", "")),
        offset=int(value.get("offset", 0)),
        line=int(value.get("line", 0)),
    )


def serialize_struct_layout(layout: StructLayout) -> dict[str, Any]:
    return {
        "name": layout.name,
        "size_expr": layout.size_expr,
        "size": layout.size,
        "attribute": layout.attribute,
        "line": layout.line,
        "fields": [serialize_field_layout(field_layout) for field_layout in layout.fields],
    }


def deserialize_struct_layout(value: dict[str, Any], path: Path) -> StructLayout:
    layout = StructLayout(
        name=str(value.get("name", "")),
        size_expr=str(value.get("size_expr", "")),
        size=int(value.get("size", 0)),
        source_path=path,
        attribute=str(value.get("attribute", "")),
        line=int(value.get("line", 0)),
    )
    raw_fields = value.get("fields", [])
    if isinstance(raw_fields, list):
        layout.fields = [deserialize_field_layout(item) for item in raw_fields if isinstance(item, dict)]
    return layout


def serialize_finding(finding: Finding) -> dict[str, Any]:
    return finding.to_json()


def deserialize_finding(value: dict[str, Any]) -> Finding:
    references = value.get("references", [])
    return Finding(
        severity=str(value.get("severity", "ERROR")),
        code=str(value.get("code", "")),
        message=str(value.get("message", "")),
        path=str(value.get("path", "")),
        offset=value.get("offset") if isinstance(value.get("offset"), int) else None,
        hexdump=str(value.get("hexdump", "")),
        layout=str(value.get("layout", "")),
        remediation=str(value.get("remediation", "")),
        references=[str(item) for item in references] if isinstance(references, list) else [],
    )


def load_ast_cache() -> dict[str, Any]:
    path = ast_cache_path()
    if not path.exists():
        return {"schema": AST_CACHE_SCHEMA, "files": {}}
    try:
        with path.open("r", encoding="utf-8-sig") as handle:
            payload = json.load(handle)
    except Exception:
        return {"schema": AST_CACHE_SCHEMA, "files": {}}
    if not isinstance(payload, dict) or payload.get("schema") != AST_CACHE_SCHEMA or not isinstance(payload.get("files"), dict):
        return {"schema": AST_CACHE_SCHEMA, "files": {}}
    return payload


def save_ast_cache(cache: dict[str, Any]) -> None:
    path = ast_cache_path()
    temp_path = path.with_name(f".{path.name}.tmp.{os.getpid()}")
    try:
        path.parent.mkdir(parents=True, exist_ok=True)
        with temp_path.open("w", encoding="utf-8") as handle:
            json.dump(cache, handle, separators=(",", ":"))
            handle.write("\n")
        os.replace(temp_path, path)
    except OSError:
        try:
            temp_path.unlink()
        except OSError:
            pass


def try_read_cached_parse(
    cache: dict[str, Any],
    path: Path,
) -> tuple[list[StructLayout], list[Finding]] | None:
    files = cache.get("files")
    if not isinstance(files, dict):
        return None
    key = file_cache_key(path)
    entry = files.get(key)
    if not isinstance(entry, dict):
        return None
    try:
        stamp = file_cache_stamp(path)
    except OSError:
        return None
    if entry.get("stamp") != stamp:
        return None
    raw_structs = entry.get("structs", [])
    raw_findings = entry.get("findings", [])
    if not isinstance(raw_structs, list) or not isinstance(raw_findings, list):
        return None
    structs = [deserialize_struct_layout(item, path) for item in raw_structs if isinstance(item, dict)]
    findings = [deserialize_finding(item) for item in raw_findings if isinstance(item, dict)]
    return structs, findings


def write_cached_parse(
    cache: dict[str, Any],
    path: Path,
    structs: list[StructLayout],
    findings: list[Finding],
) -> None:
    files = cache.setdefault("files", {})
    if not isinstance(files, dict):
        cache["files"] = {}
        files = cache["files"]
    files[file_cache_key(path)] = {
        "stamp": file_cache_stamp(path),
        "structs": [serialize_struct_layout(layout) for layout in structs],
        "findings": [serialize_finding(finding) for finding in findings],
    }


def parse_structs(
    files: list[Path],
    constants: dict[str, int],
    state: ValidationState | None = None,
    prefiltered: bool = False,
) -> dict[str, StructLayout]:
    structs: dict[str, StructLayout] = {}
    if not files:
        return structs
    if not prefiltered:
        files = filter_files_by_any(files, (b"StructLayout", b"FieldOffset", b"LayoutKind.Explicit"))
    if not files:
        return structs
    use_cache = prefiltered and all(path.is_relative_to(PROJECT_ROOT) for path in files)
    cache = load_ast_cache() if use_cache else {}
    parsed_since_save = 0
    missed_files: list[Path] = []

    def record_parsed(path: Path, parsed_structs: list[StructLayout], findings: list[Finding]) -> None:
        for finding in findings:
            if state:
                state.findings.append(finding)
                if state.args.fail_fast and finding.severity == "ERROR":
                    raise FailFastAbort(finding.message)
        for layout in parsed_structs:
            structs[layout.name] = layout

    for path in files:
        try:
            cached = try_read_cached_parse(cache, path) if use_cache else None
            if cached is not None:
                parsed_structs, findings = cached
                record_parsed(path, parsed_structs, findings)
            else:
                missed_files.append(path)
        except Exception as exc:
            if state:
                state.add_error("CS_PARSE_ERROR", f"{path} parse failed: {exc}", str(path))
            continue
    if missed_files:
        use_process_pool = (
            len(missed_files) >= 16
            and os.environ.get("H8BIN_VALIDATOR_USE_PROCESS_POOL") == "1"
        )
        if use_process_pool:
            worker_count = max(1, min(8, max(1, (os.cpu_count() or 2) - 1), len(missed_files)))
            with concurrent.futures.ProcessPoolExecutor(max_workers=worker_count) as executor:
                payloads = ((str(path), constants) for path in missed_files)
                for path_text, parsed_structs, findings, error in executor.map(parse_csharp_file_worker, payloads, chunksize=4):
                    path = Path(path_text)
                    if error:
                        if state:
                            state.add_error("CS_PARSE_ERROR", f"{path} parse failed: {error}", str(path))
                        continue
                    record_parsed(path, parsed_structs, findings)
                    if use_cache:
                        write_cached_parse(cache, path, parsed_structs, findings)
                        parsed_since_save += 1
                        if parsed_since_save >= 64:
                            save_ast_cache(cache)
                            parsed_since_save = 0
        else:
            for path in missed_files:
                try:
                    parsed_structs, findings = parse_csharp_file(path, constants)
                except Exception as exc:
                    if state:
                        state.add_error("CS_PARSE_ERROR", f"{path} parse failed: {exc}", str(path))
                    continue
                record_parsed(path, parsed_structs, findings)
                if use_cache:
                    write_cached_parse(cache, path, parsed_structs, findings)
                    parsed_since_save += 1
                    if parsed_since_save >= 64:
                        save_ast_cache(cache)
                        parsed_since_save = 0
    if use_cache and parsed_since_save:
        save_ast_cache(cache)
    return structs


def resolve_fixed_buffer_layout(type_name: str, constants: dict[str, int]) -> tuple[int, int, str] | None:
    close = type_name.rfind("]")
    open_bracket = type_name.rfind("[", 0, close)
    if close != len(type_name) - 1 or open_bracket <= 0:
        return None
    base = normalize_type(type_name[:open_bracket])
    primitive = PRIMITIVE_TYPES.get(base)
    if not primitive:
        return None
    count_expr = type_name[open_bracket + 1 : close].strip()
    if not count_expr:
        return None
    try:
        count = eval_int_expr(count_expr, constants)
    except Exception:
        return None
    if count <= 0:
        return None
    element_size, element_alignment, _ = primitive
    return element_size * count, element_alignment, ""


def resolve_type_layout(type_name: str, structs: dict[str, StructLayout], constants: dict[str, int]) -> tuple[int, int, str] | None:
    base = normalize_type(type_name)
    fixed_buffer = resolve_fixed_buffer_layout(base, constants)
    if fixed_buffer:
        return fixed_buffer
    if base in PRIMITIVE_TYPES:
        return PRIMITIVE_TYPES[base]
    if base in VECTOR_TYPES:
        size, alignment = VECTOR_TYPES[base]
        return size, alignment, ""
    nested = structs.get(base)
    if nested and nested.resolved:
        return nested.size, nested.largest_alignment, ""
    return None


def validate_struct_layouts(structs: dict[str, StructLayout], state: ValidationState) -> None:
    unresolved = set(structs)
    for _ in range(max(1, len(structs) + 1)):
        progressed = False
        for name in list(unresolved):
            layout = structs[name]
            fields_ready = True
            largest_alignment = 1
            occupied: dict[int, str] = {}
            payload_bytes = 0
            max_end = 0
            for item in layout.fields:
                resolved = resolve_type_layout(item.type_name, structs, state.schema.constants)
                if not resolved:
                    fields_ready = False
                    break
                item.size, item.alignment, item.struct_format = resolved
                largest_alignment = max(largest_alignment, item.alignment)
                payload_bytes += item.size
                max_end = max(max_end, item.end)
                if item.offset < 0:
                    state.add_error(
                        "FIELD_NEGATIVE_OFFSET",
                        f"{name}.{item.name} offset {item.offset} is negative",
                        str(layout.source_path),
                        item.offset,
                        layout=layout.attribute,
                    )
                    continue
                if item.offset % item.alignment != 0:
                    state.add_error(
                        "FIELD_ALIGNMENT",
                        f"{name}.{item.name} offset {item.offset} violates {item.alignment}-byte alignment",
                        str(layout.source_path),
                        item.offset,
                        layout=layout.attribute,
                    )
                if item.end > layout.size:
                    state.add_error(
                        "FIELD_OUT_OF_STRUCT",
                        f"{name}.{item.name} ends at {item.end}, struct size is {layout.size}",
                        str(layout.source_path),
                        item.offset,
                        layout=layout.attribute,
                    )
                for byte_index in range(item.offset, min(item.end, layout.size)):
                    owner = occupied.get(byte_index)
                    if owner:
                        state.add_error(
                            "FIELD_OVERLAP",
                            f"{name}.{item.name} overlaps {owner} at byte {byte_index}",
                            str(layout.source_path),
                            byte_index,
                            layout=layout.attribute,
                        )
                        break
                    occupied[byte_index] = item.name
            if not fields_ready:
                continue
            layout.largest_alignment = largest_alignment
            layout.final_padding = max(0, layout.size - max_end)
            layout.payload_padding = max(0, layout.size - payload_bytes)
            layout.resolved = True
            unresolved.remove(name)
            progressed = True
            required_struct_alignment = max(8, largest_alignment)
            if layout.size % required_struct_alignment != 0:
                state.add_error(
                    "STRUCT_SIZE_ALIGNMENT",
                    f"{name} size {layout.size} is not a multiple of required {required_struct_alignment}-byte ARM64 alignment",
                    str(layout.source_path),
                    layout=layout.describe(),
                )
        if not unresolved or not progressed:
            break
    for name in sorted(unresolved):
        state.add_error("STRUCT_UNRESOLVED_FIELD", f"{name} has unresolved field types", str(structs[name].source_path))


def is_aup_field_name(name: str) -> bool:
    lowered = name.lower()
    return (
        lowered in {"aupx", "aupy", "aupz"}
        or lowered.endswith("aupx")
        or lowered.endswith("aupy")
        or lowered.endswith("aupz")
        or "absoluteuniverse" in lowered
        or "worldposition" in lowered
        or "absoluteposition" in lowered
        or "universeposition" in lowered
    )


def validate_aup_precision(structs: dict[str, StructLayout], state: ValidationState) -> None:
    for layout in structs.values():
        fields_by_name = {field.name: field for field in layout.fields}
        for field_layout in layout.fields:
            type_name = normalize_type(field_layout.type_name)
            if not is_aup_field_name(field_layout.name):
                continue
            if type_name in {"float", "float2", "float3", "float4", "Vector3", "Vector2", "Vector4"}:
                state.add_error(
                    "AUP_FLOAT_FIELD",
                    f"{layout.name}.{field_layout.name} stores AUP/world position as {type_name}; binary schema requires double3 or contiguous double lanes",
                    str(layout.source_path),
                    field_layout.offset,
                    layout=layout.describe(),
                )
        for prefix in ("Aup", "AUP", "WorldPosition", "AbsolutePosition", "UniversePosition"):
            x = fields_by_name.get(f"{prefix}X")
            y = fields_by_name.get(f"{prefix}Y")
            z = fields_by_name.get(f"{prefix}Z")
            if not (x and y and z):
                continue
            if not (
                normalize_type(x.type_name) == "double"
                and normalize_type(y.type_name) == "double"
                and normalize_type(z.type_name) == "double"
                and y.offset == x.offset + 8
                and z.offset == y.offset + 8
            ):
                state.add_error(
                    "AUP_DOUBLE3_CONTIGUITY",
                    f"{layout.name}.{prefix}X/Y/Z are not contiguous double lanes at +0,+8,+16",
                    str(layout.source_path),
                    x.offset,
                    layout=layout.describe(),
                )


def scan_aup_float_casts(files: list[Path], state: ValidationState) -> None:
    write_tokens = {"Write", "WriteBytes", "WriteRecord", "Serialize", "BinaryWriter", "FileStream"}
    aup_tokens = {"Aup", "AUP", "AbsoluteUniversePosition", "WorldPosition", "AbsolutePosition", "UniversePosition"}
    candidate_files = [
        path
        for path in filter_files_by_any(files, (b"float3", b"Vector3"))
        if file_contains_all_groups(
            path,
            (
                (b"Aup", b"AUP", b"AbsoluteUniverse", b"WorldPosition", b"AbsolutePosition", b"UniversePosition"),
                (b"BinaryWriter", b"FileStream", b".h8bin", b"WriteRecord", b"WriteBytes"),
            ),
        )
    ]
    for path in candidate_files:
        try:
            lines = path.read_text(encoding="utf-8-sig", errors="ignore").splitlines()
        except OSError as exc:
            state.add_error("AUP_CAST_SCAN_READ_ERROR", f"{path} could not be scanned: {exc}", str(path))
            continue
        for line_index, raw in enumerate(lines, 1):
            scan = raw.split("//", 1)[0]
            if "float3" not in scan and "Vector3" not in scan:
                continue
            if not any(token in scan for token in aup_tokens):
                continue
            window_start = max(0, line_index - 3)
            window_end = min(len(lines), line_index + 4)
            window = "\n".join(lines[window_start:window_end])
            if any(token in window for token in write_tokens):
                type_name = "float3" if "float3" in scan else "Vector3"
                state.add_error(
                    "AUP_FLOAT_CAST_BEFORE_WRITE",
                    f"line {line_index}: {path} casts or stages AUP/world position through {type_name} near serialization/write tokens",
                    str(path),
                )


def find_sizeof_struct_name(tokens: list[CSharpToken]) -> str | None:
    index = 0
    while index < len(tokens):
        if tokens[index].value != "SizeOf":
            index += 1
            continue
        cursor = index + 1
        if cursor >= len(tokens) or tokens[cursor].value != "<":
            index += 1
            continue
        if cursor + 2 < len(tokens) and is_identifier_value(tokens[cursor + 1].value) and tokens[cursor + 2].value == ">":
            return tokens[cursor + 1].value.lstrip("@")
        index += 1
    return None


def replace_sizeof_tokens_with_zero(tokens: list[CSharpToken]) -> list[CSharpToken]:
    output: list[CSharpToken] = []
    index = 0
    while index < len(tokens):
        if tokens[index].value == "SizeOf" and index + 4 < len(tokens) and tokens[index + 1].value == "<":
            close = index + 2
            while close < len(tokens) and tokens[close].value != ">":
                close += 1
            cursor = close + 1
            if close < len(tokens) and cursor + 1 < len(tokens) and tokens[cursor].value == "(" and tokens[cursor + 1].value == ")":
                output.append(CSharpToken("0", tokens[index].line))
                index = cursor + 2
                continue
        output.append(tokens[index])
        index += 1
    return output


def parse_expected_record_sizes(
    files: list[Path],
    constants: dict[str, int],
    structs: dict[str, StructLayout],
) -> tuple[dict[str, int], dict[str, str]]:
    expected: dict[str, int] = {}
    section_structs: dict[str, str] = {}
    candidates = [
        path
        for path in filter_files_by_any(files, (b"H8DataSectionId",))
        if file_contains_all_groups(path, ((b"case",), (b"return",)))
    ]
    for path in candidates:
        tokens = tokenize_csharp_file(path)
        cursor = 0
        while cursor < len(tokens):
            if tokens[cursor].value != "case":
                cursor += 1
                continue
            if cursor + 4 >= len(tokens):
                break
            if tokens[cursor + 1].value != "H8DataSectionId" or tokens[cursor + 2].value != ".":
                cursor += 1
                continue
            section_name = tokens[cursor + 3].value
            colon = cursor + 4
            while colon < len(tokens) and tokens[colon].value != ":":
                colon += 1
            if colon >= len(tokens):
                cursor += 1
                continue
            ret = colon + 1
            while ret < len(tokens) and tokens[ret].value != "return":
                if tokens[ret].value == "case":
                    break
                ret += 1
            if ret >= len(tokens) or tokens[ret].value != "return":
                cursor = colon + 1
                continue
            end = ret + 1
            while end < len(tokens) and tokens[end].value != ";":
                end += 1
            expr_tokens = tokens[ret + 1 : end]
            struct_name = find_sizeof_struct_name(expr_tokens)
            if struct_name and struct_name in structs:
                expected[section_name] = structs[struct_name].size
                section_structs[section_name] = struct_name
            else:
                try:
                    expected[section_name] = eval_int_expr(expr_from_tokens(replace_sizeof_tokens_with_zero(expr_tokens)), constants)
                except Exception:
                    pass
            cursor = end + 1
    for section_name, size in list(expected.items()):
        if section_name in section_structs:
            continue
        hinted = SECTION_STRUCT_HINTS.get(section_name)
        if hinted in structs and structs[hinted].size == size:
            section_structs[section_name] = hinted
            continue
        candidates = [name for name, layout in structs.items() if layout.size == size and section_name.rstrip("s") in name]
        if len(candidates) == 1:
            section_structs[section_name] = candidates[0]
    return expected, section_structs


def parse_csharp_schema(cs_roots: list[Path], args: argparse.Namespace) -> SchemaModel:
    files = collect_csharp_files(cs_roots)
    layout_candidate_files = filter_files_by_any(files, (b"StructLayout", b"FieldOffset", b"LayoutKind.Explicit"))
    placeholder = SchemaModel({}, {}, [], {}, {}, {})
    schema_args = argparse.Namespace(**vars(args))
    schema_args.fail_fast = False
    state = ValidationState(schema_args, placeholder)
    if not files:
        roots = ", ".join(str(root) for root in cs_roots)
        state.add_error("CS_SOURCE_EMPTY", f"no C# source files found under: {roots}", roots)
    constants = parse_constants(layout_candidate_files)
    if constants.get("SectionAlignmentBytes", DEFAULT_SECTION_ALIGNMENT) < DEFAULT_SECTION_ALIGNMENT:
        state.add_error(
            "SECTION_ALIGNMENT_CONTRACT",
            f"SectionAlignmentBytes {constants.get('SectionAlignmentBytes')} is below the 16-byte H8DM floor",
            "",
        )
    enum_sections = parse_enum_sections(layout_candidate_files)
    if not enum_sections:
        enum_sections = parse_enum_sections(files)
    section_order = parse_section_order(layout_candidate_files, enum_sections)
    if not section_order:
        section_order = parse_section_order(files, enum_sections)
    schema = SchemaModel(constants, enum_sections, section_order, {}, {}, {})
    state.schema = schema
    structs = parse_structs(layout_candidate_files, constants, state, prefiltered=True)
    schema.structs = structs
    validate_struct_layouts(structs, state)
    validate_aup_precision(structs, state)
    scan_aup_float_casts(layout_candidate_files, state)
    expected, section_structs = parse_expected_record_sizes(layout_candidate_files, constants, structs)
    if not expected:
        expected, section_structs = parse_expected_record_sizes(files, constants, structs)
    schema.expected_sizes = expected
    schema.section_structs = section_structs
    for primary in ("H8DataBlobHeader", "H8DataBlobDirectory", "H8DataSectionEntry"):
        if primary not in structs:
            state.add_error("PRIMARY_STRUCT_MISSING", f"required C# struct {primary} was not parsed", "")
    if not enum_sections:
        state.add_error("SECTION_ENUM_MISSING", "H8DataSectionId enum was not parsed from C# source", "")
    if not section_order:
        state.add_error("SECTION_ORDER_MISSING", "no Data Monolith section order was parsed from C# source", "")
    if state.findings:
        args._schema_findings = state.findings
    return schema


def effective_section_alignment(schema: SchemaModel) -> int:
    return max(DEFAULT_SECTION_ALIGNMENT, schema.constant("SectionAlignmentBytes", DEFAULT_SECTION_ALIGNMENT))


def align_up(value: int, alignment: int) -> int:
    if alignment <= 1:
        return value
    return (value + alignment - 1) & ~(alignment - 1)


def format_hexdump(mm_obj: mmap.mmap, center: int, radius: int = 16) -> str:
    start = max(0, center - radius)
    end = min(len(mm_obj), center + radius)
    lines: list[str] = []
    cursor = start
    while cursor < end:
        chunk = mm_obj[cursor : min(cursor + 16, end)]
        hex_bytes = " ".join(f"{byte:02X}" for byte in chunk)
        lines.append(f"0x{cursor:08X}: {hex_bytes}")
        cursor += 16
    return "\n".join(lines)


def sanitize_runtime_artifacts(target_dir: Path, state: ValidationState) -> None:
    if not target_dir.exists():
        state.add_error("TARGET_DIR_MISSING", f"target directory does not exist: {target_dir}", str(target_dir))
        return
    for path in sorted(target_dir.rglob("*")):
        if path.is_file() and path.suffix.lower() in TEXT_RUNTIME_SUFFIXES:
            remediation, references = build_artifact_remediation(path)
            state.add_error(
                "UNBAKED_ARTIFACT",
                f"Unbaked Artifact in runtime target: {path.relative_to(PROJECT_ROOT) if path.is_relative_to(PROJECT_ROOT) else path}",
                str(path),
                remediation=remediation,
                references=references,
            )


def build_artifact_remediation(path: Path) -> tuple[str, list[str]]:
    name = path.name
    lower = name.lower()
    if "signal" in lower:
        remediation = (
            "Move the human CSV to Assets/_SourceData/Signals or Data/Balance/Schemas, "
            "bake it into static_data.h8bin or a domain .h8bin section, and make runtime cold boot read binary/Vault bytes. "
            "Editor hot reload may read CSV only behind UNITY_EDITOR."
        )
    elif "hadal" in lower or "graph" in lower:
        remediation = (
            "Move the authoring CSV to Assets/_SourceData/HadalGraphs and keep it as editor-only Forge input. "
            "Runtime output must be a baked mesh/binary payload, never StreamingAssets CSV."
        )
    else:
        remediation = (
            "Move the text file out of runtime StreamingAssets into a source-data folder and add an explicit baker "
            "that emits binary payload bytes before build."
        )
    return remediation, []


def release_expression_can_be_true(expression: str) -> bool:
    """Return True when a C# preprocessor expression can be active in a player release."""
    expression = re.sub(r"\bdefined\s*\(\s*([A-Za-z_][A-Za-z0-9_]*)\s*\)", r"\1", expression)
    symbols = sorted(set(re.findall(r"\b[A-Za-z_][A-Za-z0-9_]*\b", expression)))
    fixed = {
        "UNITY_EDITOR": False,
        "DEVELOPMENT_BUILD": False,
    }
    variable_symbols = [symbol for symbol in symbols if symbol not in fixed]
    if len(variable_symbols) > 12:
        # Keep the validator conservative for platform-heavy expressions.
        variable_symbols = variable_symbols[:12]

    python_expr = expression
    python_expr = python_expr.replace("&&", " and ")
    python_expr = python_expr.replace("||", " or ")
    python_expr = re.sub(r"!(?!=)", " not ", python_expr)

    for values in itertools.product((False, True), repeat=len(variable_symbols)):
        env = dict(fixed)
        env.update(zip(variable_symbols, values))
        try:
            if bool(eval(python_expr, {"__builtins__": {}}, env)):
                return True
        except Exception:
            return True
    return False


def release_active_line_mask(lines: list[str]) -> list[bool]:
    """Approximate C# preprocessor activity for any non-editor, non-development player target."""
    mask: list[bool] = []
    stack: list[dict[str, bool]] = []

    def current_active() -> bool:
        return all(frame["active"] for frame in stack)

    for line in lines:
        stripped = line.strip()
        if stripped.startswith("#if "):
            parent_active = current_active()
            active = release_expression_can_be_true(stripped[4:].strip())
            stack.append(
                {
                    "parent": parent_active,
                    "active": parent_active and active,
                    "taken": active,
                }
            )
            mask.append(False)
            continue

        if stripped.startswith("#elif ") and stack:
            frame = stack[-1]
            branch_active = release_expression_can_be_true(stripped[6:].strip())
            frame["active"] = frame["parent"] and not frame["taken"] and branch_active
            frame["taken"] = frame["taken"] or branch_active
            mask.append(False)
            continue

        if stripped == "#else" and stack:
            frame = stack[-1]
            frame["active"] = frame["parent"] and not frame["taken"]
            frame["taken"] = True
            mask.append(False)
            continue

        if stripped == "#endif" and stack:
            stack.pop()
            mask.append(False)
            continue

        mask.append(current_active())

    return mask


def get_reference_text_cache() -> list[tuple[str, list[str]]]:
    global _REFERENCE_TEXT_CACHE
    if _REFERENCE_TEXT_CACHE is not None:
        return _REFERENCE_TEXT_CACHE

    cache: list[tuple[str, list[str]]] = []
    for root in REFERENCE_SCAN_ROOTS:
        if not root.exists():
            continue
        for candidate in sorted(root.rglob("*")):
            if not candidate.is_file() or candidate.suffix.lower() not in {".cs", ".md", ".txt"}:
                continue
            try:
                lines = candidate.read_text(encoding="utf-8-sig", errors="ignore").splitlines()
            except OSError:
                continue
            try:
                rel = candidate.relative_to(PROJECT_ROOT).as_posix()
            except ValueError:
                rel = str(candidate)
            cache.append((rel, lines))
    _REFERENCE_TEXT_CACHE = cache
    return cache


def find_artifact_references(file_name: str, limit: int = 12) -> list[str]:
    references: list[str] = []
    for rel, lines in get_reference_text_cache():
        for index, line in enumerate(lines, 1):
            if file_name in line:
                references.append(f"{rel}:{index}:{line.strip()[:180]}")
                if len(references) >= limit:
                    return references
    return references


def default_runtime_source_roots() -> list[Path]:
    return [PROJECT_ROOT / "Assets/_Project/Scripts"]


def is_editor_source_path(path: Path) -> bool:
    return any(part.lower() == "editor" or part.lower().endswith(".editor") for part in path.parts)


def extract_first_csharp_string_literal(text: str) -> str:
    start = text.find('"')
    if start < 0:
        return ""
    value: list[str] = []
    escaped = False
    for char in text[start + 1 :]:
        if escaped:
            value.append(char)
            escaped = False
            continue
        if char == "\\":
            escaped = True
            continue
        if char == '"':
            return "".join(value)
        value.append(char)
    return ""


def collect_text_artifact_symbols(lines: list[str]) -> dict[str, tuple[str, str]]:
    symbols: dict[str, tuple[str, str]] = {}
    for line in lines:
        stripped = line.strip()
        if stripped.startswith("//"):
            continue
        scan = stripped.split("//", 1)[0]
        if "=" not in scan or "string" not in scan or '"' not in scan:
            continue
        left, right = scan.split("=", 1)
        left_parts = left.replace("\t", " ").split()
        if "string" not in left_parts:
            continue
        if "const" not in left_parts and not ("static" in left_parts and "readonly" in left_parts):
            continue
        string_index = left_parts.index("string")
        if string_index + 1 >= len(left_parts):
            continue
        name = left_parts[string_index + 1].lstrip("@").strip()
        if not name or not is_identifier_value(name):
            continue
        value = extract_first_csharp_string_literal(right)
        if any(value.lower().endswith(suffix) for suffix in TEXT_RUNTIME_SUFFIXES):
            symbols[name.lower()] = (name, value)
    return symbols


def contains_identifier_word(text: str, word: str) -> bool:
    cursor = text.find(word)
    while cursor >= 0:
        left_ok = cursor == 0 or not (text[cursor - 1].isalnum() or text[cursor - 1] == "_")
        end = cursor + len(word)
        right_ok = end >= len(text) or not (text[end].isalnum() or text[end] == "_")
        if left_ok and right_ok:
            return True
        cursor = text.find(word, cursor + 1)
    return False


def referenced_text_artifact_symbol(lower_line: str, symbols: dict[str, tuple[str, str]]) -> tuple[str, str] | None:
    for lower_name, symbol in symbols.items():
        if contains_identifier_word(lower_line, lower_name):
            return symbol
    return None


def scan_runtime_text_loaders(source_roots: list[Path], state: ValidationState) -> None:
    for root in source_roots:
        if not root.exists():
            continue
        for path in iter_runtime_csharp_files(root):
            try:
                raw = path.read_bytes()
            except OSError:
                continue
            raw_lower = raw.lower()
            if b"streamingassets" not in raw_lower:
                continue
            if not any(suffix.encode("ascii") in raw_lower for suffix in TEXT_RUNTIME_SUFFIXES):
                continue
            lines = raw.decode("utf-8-sig", errors="ignore").splitlines()
            active_lines = release_active_line_mask(lines)
            text_symbols = collect_text_artifact_symbols(lines)
            for index, line in enumerate(lines, 1):
                if index - 1 >= len(active_lines) or not active_lines[index - 1]:
                    continue
                lower = line.lower()
                if "streamingassets" not in lower:
                    continue
                has_literal_text_suffix = any(suffix in lower for suffix in TEXT_RUNTIME_SUFFIXES)
                referenced_symbol = referenced_text_artifact_symbol(lower, text_symbols)
                if not has_literal_text_suffix and referenced_symbol is None:
                    continue
                if not any(token in lower for token in RUNTIME_TEXT_LOADER_TOKENS):
                    continue
                try:
                    rel = path.relative_to(PROJECT_ROOT).as_posix()
                except ValueError:
                    rel = str(path)
                snippet = line.strip()[:180]
                symbol_context = ""
                if referenced_symbol is not None:
                    symbol_context = f" via text artifact symbol {referenced_symbol[0]}={referenced_symbol[1]}"
                state.add_error(
                    "RUNTIME_TEXT_STREAMINGASSETS_LOAD",
                    f"Runtime C# source loads text from StreamingAssets{symbol_context}: {rel}:{index}: {snippet}",
                    str(path),
                    remediation=(
                        "Move the human-readable source file to editor/source-data ownership, bake it into "
                        "static_data.h8bin or a domain .h8bin payload, and make runtime cold boot read binary/Vault bytes."
                    ),
                    references=[f"{rel}:{index}:{snippet}"],
                )


def iter_runtime_csharp_files(root: Path) -> Iterable[Path]:
    for current_root, dirs, files in os.walk(root):
        dirs[:] = [
            item
            for item in dirs
            if item.lower() != "editor" and not item.lower().endswith(".editor")
        ]
        current = Path(current_root)
        if is_editor_source_path(current):
            continue
        for file_name in files:
            if file_name.endswith(".cs"):
                yield current / file_name


def collect_h8bin_files(target_dir: Path) -> list[Path]:
    if not target_dir.exists():
        return []
    return sorted(path for path in target_dir.rglob("*.h8bin") if path.is_file())


def read_u32(mm_obj: mmap.mmap, offset: int) -> int:
    return struct.unpack_from("<I", mm_obj, offset)[0]


def read_floating(mm_obj: mmap.mmap, offset: int, type_name: str) -> float:
    if type_name == "float":
        return struct.unpack_from("<f", mm_obj, offset)[0]
    return struct.unpack_from("<d", mm_obj, offset)[0]


def read_uint(mm_obj: mmap.mmap, offset: int, type_name: str) -> int:
    fmt = PRIMITIVE_TYPES[type_name][2]
    return struct.unpack_from("<" + fmt, mm_obj, offset)[0]


def read_integer(mm_obj: mmap.mmap, offset: int, type_name: str) -> int:
    fmt = PRIMITIVE_TYPES[type_name][2]
    return int(struct.unpack_from("<" + fmt, mm_obj, offset)[0])


def compute_payload_checksum(mm_obj: mmap.mmap, header_size: int) -> int:
    payload_view = memoryview(mm_obj)[header_size:]
    try:
        return xxh3_64(payload_view)
    finally:
        payload_view.release()


def fnv1a32_view(mm_obj: mmap.mmap, start: int, end: int) -> int:
    payload_view = memoryview(mm_obj)[start:end]
    try:
        value = FNV_OFFSET
        for byte in payload_view:
            value = ((value ^ int(byte)) * FNV_PRIME) & MASK32
        return value
    finally:
        payload_view.release()


def align_vocal_payload_cursor(value: int) -> int:
    remainder = value % VOCAL_BANK_PAYLOAD_ALIGNMENT
    if remainder == 0:
        return value
    return value + (VOCAL_BANK_PAYLOAD_ALIGNMENT - remainder)


def validate_vocal_bank_h8bin(file_path: Path, mm_obj: mmap.mmap, state: ValidationState, metric: FileMetrics) -> None:
    errors_before = sum(1 for finding in state.findings if finding.severity == "ERROR")
    metric.sections = 1
    if len(mm_obj) < VOCAL_BANK_HEADER_SIZE:
        state.add_error("H8VB_FILE_TOO_SMALL", "H8VB file is smaller than 64-byte vocal bank header", str(file_path), 0)
        metric.status = "ERROR"
        return

    if len(mm_obj) % VOCAL_BANK_PAYLOAD_ALIGNMENT != 0:
        state.add_error(
            "H8VB_FILE_ALIGNMENT",
            f"H8VB file length {len(mm_obj)} is not {VOCAL_BANK_PAYLOAD_ALIGNMENT}-byte aligned",
            str(file_path),
            len(mm_obj),
            format_hexdump(mm_obj, max(0, len(mm_obj) - 1)),
        )

    (
        magic,
        version,
        header_size,
        record_size,
        record_count,
        flags,
        payload_offset,
        payload_bytes,
        sample_rate,
        default_codec,
        default_channels,
        endian,
        bank_hash,
        block_samples,
        _created_unix_seconds,
        reserved0,
    ) = struct.unpack_from("<IIIIIIQQIBBHIIII", mm_obj, 0)

    header_shape_valid = True
    payload_range_valid = True
    record_count_valid = True
    if magic != VOCAL_BANK_MAGIC:
        state.add_error("H8VB_MAGIC", f"H8VB magic 0x{magic:08X} expected 0x{VOCAL_BANK_MAGIC:08X}", str(file_path), 0)
        header_shape_valid = False
    if version != VOCAL_BANK_VERSION:
        state.add_error("H8VB_VERSION", f"H8VB version {version} expected {VOCAL_BANK_VERSION}", str(file_path), 4)
    if header_size != VOCAL_BANK_HEADER_SIZE:
        state.add_error("H8VB_HEADER_SIZE", f"H8VB header size {header_size} expected {VOCAL_BANK_HEADER_SIZE}", str(file_path), 8)
        header_shape_valid = False
    if record_size != VOCAL_BANK_RECORD_SIZE:
        state.add_error("H8VB_RECORD_SIZE", f"H8VB record size {record_size} expected {VOCAL_BANK_RECORD_SIZE}", str(file_path), 12)
        header_shape_valid = False
    if record_count <= 0:
        state.add_error("H8VB_RECORD_COUNT", "H8VB vocal bank must contain at least one record", str(file_path), 16)
        record_count_valid = False
    if record_count > 1_000_000:
        state.add_error("H8VB_RECORD_COUNT_CAP", f"H8VB record count {record_count} exceeds validation cap", str(file_path), 16)
        record_count_valid = False
    if endian != VOCAL_BANK_ENDIAN:
        state.add_error("H8VB_ENDIAN", f"H8VB endian marker 0x{endian:04X} expected 0x{VOCAL_BANK_ENDIAN:04X}", str(file_path), 46)
    if block_samples != VOCAL_BANK_BLOCK_SAMPLES:
        state.add_error("H8VB_BLOCK_SAMPLES", f"H8VB block samples {block_samples} expected {VOCAL_BANK_BLOCK_SAMPLES}", str(file_path), 52)
    if sample_rate < 8000 or sample_rate > 96000:
        state.add_error("H8VB_SAMPLE_RATE", f"H8VB sample rate {sample_rate} outside 8000..96000", str(file_path), 40)
    if default_codec not in VOCAL_BANK_KNOWN_CODECS:
        state.add_error("H8VB_DEFAULT_CODEC", f"H8VB default codec {default_codec} is unknown", str(file_path), 44)
    elif default_codec not in VOCAL_BANK_SUPPORTED_CODECS:
        state.add_error("H8VB_DEFAULT_CODEC_UNSUPPORTED", f"H8VB default codec {default_codec} is not supported by runtime playback", str(file_path), 44)
    if default_channels != 1:
        state.add_error("H8VB_DEFAULT_CHANNELS", f"H8VB default channels {default_channels} expected mono", str(file_path), 45)
    if flags != 0:
        state.add_error("H8VB_FLAGS_UNSUPPORTED", f"H8VB flags 0x{flags:08X} are unsupported", str(file_path), 20)
    if reserved0 != 0:
        state.add_error("H8VB_RESERVED_NONZERO", f"H8VB reserved word is {reserved0}, expected 0", str(file_path), 60)

    index_bytes = record_count * VOCAL_BANK_RECORD_SIZE
    minimum_payload_offset = VOCAL_BANK_HEADER_SIZE + index_bytes
    if payload_offset != minimum_payload_offset:
        state.add_error(
            "H8VB_PAYLOAD_OFFSET",
            f"H8VB payload offset {payload_offset} expected exact index end {minimum_payload_offset}",
            str(file_path),
            24,
        )
    if payload_offset % VOCAL_BANK_PAYLOAD_ALIGNMENT != 0:
        state.add_error("H8VB_PAYLOAD_ALIGNMENT", f"H8VB payload offset {payload_offset} is not {VOCAL_BANK_PAYLOAD_ALIGNMENT}-byte aligned", str(file_path), 24)
    if payload_offset > len(mm_obj) or payload_bytes > len(mm_obj) or payload_offset + payload_bytes > len(mm_obj):
        state.add_error(
            "H8VB_PAYLOAD_RANGE",
            f"H8VB payload range {payload_offset}+{payload_bytes} exceeds file bytes {len(mm_obj)}",
            str(file_path),
            24,
        )
        payload_range_valid = False
    elif payload_offset + payload_bytes != len(mm_obj):
        state.add_error(
            "H8VB_TRAILING_BYTES",
            f"H8VB payload end {payload_offset + payload_bytes} expected file bytes {len(mm_obj)}",
            str(file_path),
            32,
        )
        payload_range_valid = False

    if not header_shape_valid or not record_count_valid or minimum_payload_offset > len(mm_obj):
        metric.status = "ERROR"
        return

    if payload_range_valid:
        computed_bank_hash = fnv1a32_view(mm_obj, VOCAL_BANK_HEADER_SIZE, int(payload_offset + payload_bytes))
        if bank_hash != computed_bank_hash:
            state.add_error(
                "H8VB_BANK_HASH",
                f"H8VB bank hash stored=0x{bank_hash:08X} computed=0x{computed_bank_hash:08X}",
                str(file_path),
                48,
            )

    previous_hash = -1
    expected_payload_cursor = int(payload_offset)
    for index in range(record_count):
        record_offset = VOCAL_BANK_HEADER_SIZE + index * VOCAL_BANK_RECORD_SIZE
        if record_offset + VOCAL_BANK_RECORD_SIZE > len(mm_obj):
            state.add_error("H8VB_RECORD_RANGE", f"H8VB record {index} exceeds file bytes", str(file_path), record_offset)
            break

        (
            hash_id,
            byte_length,
            byte_offset,
            total_samples,
            record_sample_rate,
            codec,
            channels,
            priority,
            radio,
            record_flags,
        ) = struct.unpack_from("<IIQIIBBBBI", mm_obj, record_offset)
        if hash_id == 0:
            state.add_error("H8VB_RECORD_HASH_ZERO", f"H8VB record {index} has zero hash", str(file_path), record_offset)
        if hash_id <= previous_hash:
            state.add_error("H8VB_RECORD_SORT", f"H8VB record {index} hash order is not strictly ascending", str(file_path), record_offset)
        previous_hash = hash_id
        if byte_length <= 0:
            state.add_error("H8VB_RECORD_LENGTH", f"H8VB record {index} has empty payload", str(file_path), record_offset + 4)
        if byte_offset % VOCAL_BANK_PAYLOAD_ALIGNMENT != 0:
            state.add_error("H8VB_RECORD_PAYLOAD_ALIGNMENT", f"H8VB record {index} payload offset {byte_offset} is not {VOCAL_BANK_PAYLOAD_ALIGNMENT}-byte aligned", str(file_path), record_offset + 8)
        if byte_offset != expected_payload_cursor:
            state.add_error(
                "H8VB_RECORD_CONTIGUITY",
                f"H8VB record {index} payload offset {byte_offset} expected contiguous cursor {expected_payload_cursor}",
                str(file_path),
                record_offset + 8,
            )
        if byte_offset < payload_offset or byte_offset + byte_length > payload_offset + payload_bytes or byte_offset + byte_length > len(mm_obj):
            state.add_error("H8VB_RECORD_PAYLOAD_RANGE", f"H8VB record {index} payload range is outside H8VB payload", str(file_path), record_offset + 8)
            continue
        raw_payload_end = int(byte_offset + byte_length)
        aligned_payload_end = align_vocal_payload_cursor(raw_payload_end)
        if aligned_payload_end > payload_offset + payload_bytes or aligned_payload_end > len(mm_obj):
            state.add_error("H8VB_RECORD_PADDING_RANGE", f"H8VB record {index} aligned payload end exceeds H8VB payload", str(file_path), record_offset + 8)
        else:
            padding_cursor = raw_payload_end
            while padding_cursor < aligned_payload_end:
                if mm_obj[padding_cursor] != 0:
                    state.add_error("H8VB_RECORD_PADDING_NONZERO", f"H8VB record {index} padding byte is non-zero", str(file_path), padding_cursor)
                    break
                padding_cursor += 1
        expected_payload_cursor = aligned_payload_end
        if total_samples <= 0:
            state.add_error("H8VB_RECORD_SAMPLE_COUNT", f"H8VB record {index} has zero samples", str(file_path), record_offset + 16)
        if record_sample_rate != sample_rate or record_sample_rate < 8000 or record_sample_rate > 96000:
            state.add_error("H8VB_RECORD_SAMPLE_RATE", f"H8VB record {index} sample rate {record_sample_rate} mismatches header {sample_rate}", str(file_path), record_offset + 20)
        if codec not in VOCAL_BANK_KNOWN_CODECS:
            state.add_error("H8VB_RECORD_CODEC", f"H8VB record {index} codec {codec} is unknown", str(file_path), record_offset + 24)
        elif codec not in VOCAL_BANK_SUPPORTED_CODECS:
            state.add_error("H8VB_RECORD_CODEC_UNSUPPORTED", f"H8VB record {index} codec {codec} is not supported by runtime playback", str(file_path), record_offset + 24)
        if channels != 1:
            state.add_error("H8VB_RECORD_CHANNELS", f"H8VB record {index} channels {channels} expected mono", str(file_path), record_offset + 25)
        if priority > 255 or radio > 255:
            state.add_error("H8VB_RECORD_SCALAR_RANGE", f"H8VB record {index} priority/radio byte is out of range", str(file_path), record_offset + 26)
        if record_flags != 0:
            state.add_error("H8VB_RECORD_FLAGS_UNSUPPORTED", f"H8VB record {index} flags 0x{record_flags:08X} are unsupported", str(file_path), record_offset + 28)
        if codec == VOCAL_BANK_CODEC_PCM16 and byte_length != total_samples * 2:
            state.add_error("H8VB_PCM16_LENGTH", f"H8VB record {index} PCM16 bytes {byte_length} expected {total_samples * 2}", str(file_path), record_offset + 4)
        if codec == VOCAL_BANK_CODEC_H8ADPCM:
            expected_blocks = (total_samples + VOCAL_BANK_BLOCK_SAMPLES - 1) // VOCAL_BANK_BLOCK_SAMPLES
            expected_bytes = expected_blocks * 36
            if byte_length != expected_bytes:
                state.add_error("H8VB_ADPCM_LENGTH", f"H8VB record {index} ADPCM bytes {byte_length} expected {expected_bytes}", str(file_path), record_offset + 4)
            else:
                block_start = int(byte_offset)
                block_end = int(byte_offset + byte_length)
                while block_start < block_end:
                    step = mm_obj[block_start + 2]
                    reserved = mm_obj[block_start + 3]
                    if step < 1 or step > 127:
                        state.add_error("H8VB_ADPCM_STEP", f"H8VB record {index} ADPCM block step {step} outside 1..127", str(file_path), block_start + 2)
                        break
                    if reserved != 0:
                        state.add_error("H8VB_ADPCM_RESERVED", f"H8VB record {index} ADPCM block reserved byte {reserved} expected 0", str(file_path), block_start + 3)
                        break
                    block_start += 36
    if payload_range_valid and expected_payload_cursor != payload_offset + payload_bytes:
        state.add_error(
            "H8VB_PAYLOAD_CONTIGUITY",
            f"H8VB contiguous record payload end {expected_payload_cursor} expected {payload_offset + payload_bytes}",
            str(file_path),
            int(payload_offset),
        )

    metric.sampled_records = record_count
    errors_after = sum(1 for finding in state.findings if finding.severity == "ERROR")
    if errors_after == errors_before:
        metric.status = "OK"
        message = f"H8VB Audio/VocalBank validated records={record_count} bytes={len(mm_obj)}"
        state.add_info("H8VB_SCHEMA_VALIDATED", message, str(file_path))
    else:
        metric.status = "ERROR"


def deterministic_sample_indices(count: int, sample_count: int, salt: int) -> Iterator[int]:
    yield 0
    if sample_count == 1:
        return
    yield count - 1
    remaining = sample_count - 2
    if remaining <= 0:
        return
    step = (salt | 1) % count
    if step == 0:
        step = 1
    while math.gcd(step, count) != 1:
        step += 2
        if step >= count:
            step = 1
    cursor = salt % count
    emitted = 0
    while emitted < remaining:
        cursor = (cursor + step) % count
        if cursor == 0 or cursor == count - 1:
            continue
        yield cursor
        emitted += 1


def sample_indices(count: int, percent: float, thorough: bool, salt: int) -> Iterable[int]:
    if count <= 0:
        return ()
    if thorough:
        return range(count)
    sample_count = max(1, math.ceil(count * max(0.0, min(percent, 100.0)) / 100.0))
    if sample_count >= count:
        return range(count)
    sample_count = max(sample_count, min(2, count))
    return deterministic_sample_indices(count, sample_count, salt & MASK32)


def get_field(layout: StructLayout, name: str) -> FieldLayout | None:
    for item in layout.fields:
        if item.name == name:
            return item
    return None


def section_layout_for(schema: SchemaModel, section_name: str) -> StructLayout | None:
    struct_name = schema.section_structs.get(section_name, "")
    if not struct_name:
        return None
    return schema.structs.get(struct_name)


def gather_item_hashes(mm_obj: mmap.mmap, entries: dict[str, tuple[int, int, int]], schema: SchemaModel) -> set[int]:
    item_hashes: set[int] = set()
    item_entry = entries.get("Items")
    if not item_entry:
        return item_hashes
    layout = section_layout_for(schema, "Items")
    if layout is None:
        return item_hashes
    field_layout = get_field(layout, "HashId")
    record_size, count, offset = item_entry
    if not field_layout or record_size < field_layout.end or record_size < layout.size:
        return item_hashes
    for index in range(count):
        item_hash = read_u32(mm_obj, offset + index * record_size + field_layout.offset)
        if item_hash:
            item_hashes.add(item_hash)
    return item_hashes


def validate_record_sample(
    mm_obj: mmap.mmap,
    file_path: Path,
    section: SectionSchema,
    record_size: int,
    count: int,
    offset: int,
    state: ValidationState,
    item_hashes: set[int],
) -> int:
    if not section.struct_name:
        return 0
    layout = state.schema.structs.get(section.struct_name)
    if not layout:
        return 0
    if record_size == 0:
        state.add_error(
            "RECORD_SIZE_ZERO",
            f"{section.name} declares {count} records with zero record size",
            str(file_path),
            offset,
            format_hexdump(mm_obj, offset),
            layout.describe(),
        )
        return 0
    if record_size < layout.size:
        state.add_error(
            "RECORD_SIZE_UNDER_STRUCT",
            f"{section.name} record size {record_size} is smaller than {section.struct_name} size {layout.size}",
            str(file_path),
            offset,
            format_hexdump(mm_obj, offset),
            layout.describe(),
        )
        return 0
    samples = sample_indices(count, state.args.sample_percent, state.args.thorough, section.section_id ^ count ^ len(mm_obj))
    sampled = 0
    for record_index in samples:
        record_base = offset + record_index * record_size
        sampled += 1
        for item in layout.fields:
            if item.end > record_size:
                state.add_error(
                    "FIELD_EXCEEDS_RECORD_SIZE",
                    f"{section.name}[{record_index}].{item.name} end {item.end} exceeds record size {record_size}",
                    str(file_path),
                    record_base + item.offset,
                    format_hexdump(mm_obj, record_base + item.offset),
                    layout.describe(),
                )
                continue
            field_offset = record_base + item.offset
            type_name = normalize_type(item.type_name)
            if type_name in {"float", "double"}:
                value = read_floating(mm_obj, field_offset, type_name)
                if not math.isfinite(value):
                    state.add_error(
                        "FLOAT_NOT_FINITE",
                        f"{section.name}[{record_index}].{item.name} is {value}",
                        str(file_path),
                        field_offset,
                        format_hexdump(mm_obj, field_offset),
                        layout.describe(),
                    )
                if "Aup" in item.name or "AUP" in item.name:
                    if abs(value) > AUP_BOUND_METERS:
                        state.add_error(
                            "AUP_OUT_OF_BOUNDS",
                            f"{section.name}[{record_index}].{item.name}={value} exceeds +/-{AUP_BOUND_METERS}",
                            str(file_path),
                            field_offset,
                            format_hexdump(mm_obj, field_offset),
                            layout.describe(),
                        )
            elif type_name in VECTOR_TYPES and type_name.startswith("double"):
                component_count = VECTOR_TYPES[type_name][0] // 8
                for component in range(component_count):
                    component_offset = field_offset + component * 8
                    value = struct.unpack_from("<d", mm_obj, component_offset)[0]
                    if not math.isfinite(value) or abs(value) > AUP_BOUND_METERS:
                        state.add_error(
                            "AUP_VECTOR_INVALID",
                            f"{section.name}[{record_index}].{item.name}[{component}]={value}",
                            str(file_path),
                            component_offset,
                            format_hexdump(mm_obj, component_offset),
                            layout.describe(),
                        )
            elif type_name in {"uint", "ulong"} and item.name in HASH_FIELD_NAMES:
                value = read_uint(mm_obj, field_offset, type_name)
                if value == 0:
                    state.add_error(
                        "ZERO_HASH",
                        f"{section.name}[{record_index}].{item.name} is zero",
                        str(file_path),
                        field_offset,
                        format_hexdump(mm_obj, field_offset),
                        layout.describe(),
                    )
            elif type_name in {"int", "uint", "long", "ulong"} and ("Aup" in item.name or "AUP" in item.name):
                value = read_integer(mm_obj, field_offset, type_name)
                if abs(value) > AUP_BOUND_METERS:
                    state.add_error(
                        "AUP_INTEGER_OUT_OF_BOUNDS",
                        f"{section.name}[{record_index}].{item.name}={value} exceeds +/-{AUP_BOUND_METERS}",
                        str(file_path),
                        field_offset,
                        format_hexdump(mm_obj, field_offset),
                        layout.describe(),
                    )
            if item.name in REFERENCE_FIELD_NAMES and type_name == "uint" and section.name in {"Recipes", "LootCdf"}:
                value = read_u32(mm_obj, field_offset)
                if value and not item_hashes:
                    state.add_error(
                        "REFERENCE_MASTER_EMPTY",
                        f"{section.name}[{record_index}].{item.name}=0x{value:08X} cannot be verified because Items.HashId master set is empty",
                        str(file_path),
                        field_offset,
                        format_hexdump(mm_obj, field_offset),
                        layout.describe(),
                    )
                elif value and value not in item_hashes:
                    state.add_error(
                        "BROKEN_FOREIGN_KEY",
                        f"{section.name}[{record_index}].{item.name}=0x{value:08X} is not present in Items.HashId",
                        str(file_path),
                        field_offset,
                        format_hexdump(mm_obj, field_offset),
                        layout.describe(),
                    )
    return sampled


def validate_rle_probe(mm_obj: mmap.mmap, file_path: Path, offset: int, byte_count: int, state: ValidationState) -> None:
    if byte_count < 8:
        state.add_error("RLE_HEADER_TOO_SMALL", "RLE section is smaller than its 8-byte probe header", str(file_path), offset)
        return
    uncompressed_size, encoded_bytes = struct.unpack_from("<II", mm_obj, offset)
    if encoded_bytes + 8 > byte_count:
        state.add_error(
            "RLE_RANGE_INVALID",
            f"RLE encoded byte count {encoded_bytes} exceeds section bytes {byte_count}",
            str(file_path),
            offset,
            format_hexdump(mm_obj, offset),
        )
        return
    if encoded_bytes % 2 != 0:
        state.add_error(
            "RLE_ODD_ENCODED_BYTES",
            f"RLE encoded byte count {encoded_bytes} is not divisible by count/value pairs",
            str(file_path),
            offset + 4,
            format_hexdump(mm_obj, offset),
        )
        return
    cursor = offset + 8
    end = cursor + encoded_bytes
    expanded = 0
    while cursor + 2 <= end:
        run_count = mm_obj[cursor]
        if run_count == 0:
            state.add_error(
                "RLE_ZERO_RUN",
                "RLE probe contains a zero-length run",
                str(file_path),
                cursor,
                format_hexdump(mm_obj, cursor),
            )
            return
        cursor += 2
        expanded += run_count
        if expanded > uncompressed_size:
            state.add_error("RLE_EXPANDS_TOO_FAR", "RLE probe expands beyond declared uncompressed size", str(file_path), cursor)
            return
    if expanded != uncompressed_size:
        state.add_error(
            "RLE_UNPACKED_SIZE_MISMATCH",
            f"RLE probe expanded {expanded} bytes, expected {uncompressed_size}",
            str(file_path),
            offset,
        )


def load_known_hashes(path: str | None, state: ValidationState) -> set[int]:
    if not path:
        return set()
    resolved = resolve_path(path)
    if not resolved.exists():
        state.add_error("KNOWN_HASH_LIST_MISSING", f"known hash list missing: {resolved}", str(resolved))
        return set()
    hashes: set[int] = set()
    for line_number, raw in enumerate(resolved.read_text(encoding="utf-8-sig", errors="ignore").splitlines(), 1):
        text = raw.strip()
        if not text or text.startswith("#"):
            continue
        try:
            value = int(text, 0) & MASK32
        except ValueError:
            state.add_error(
                "KNOWN_HASH_LIST_INVALID",
                f"known hash list has invalid integer at line {line_number}: {text}",
                str(resolved),
            )
            continue
        if value == 0:
            state.add_error(
                "KNOWN_HASH_LIST_ZERO",
                f"known hash list contains zero hash at line {line_number}",
                str(resolved),
            )
            continue
        hashes.add(value)
    return hashes


def extract_item_hashes_from_h8bin(file_path: Path, schema: SchemaModel, args: argparse.Namespace) -> tuple[set[int], list[Finding]]:
    probe_args = argparse.Namespace(**vars(args))
    probe_args.fail_fast = False
    probe_state = ValidationState(probe_args, schema)
    item_hashes: set[int] = set()
    header_size = schema.constant("HeaderSizeBytes", DEFAULT_HEADER_BYTES)
    directory_size = schema.constant("DirectorySizeBytes", DEFAULT_DIRECTORY_BYTES)
    section_alignment = effective_section_alignment(schema)
    section_entry_size = schema.structs.get("H8DataSectionEntry", StructLayout("", "16", 16, file_path, "")).size
    if not file_path.exists():
        probe_state.add_error("ITEM_HASH_FILE_MISSING", f"binary file missing: {file_path}", str(file_path))
        return item_hashes, probe_state.findings
    with file_path.open("rb") as handle:
        if os.fstat(handle.fileno()).st_size == 0:
            probe_state.add_error("ITEM_HASH_FILE_EMPTY", f"binary file is empty: {file_path}", str(file_path))
            return item_hashes, probe_state.findings
        with mmap.mmap(handle.fileno(), 0, access=mmap.ACCESS_READ) as mm_obj:
            if len(mm_obj) < header_size + directory_size:
                probe_state.add_error("ITEM_HASH_FILE_TOO_SMALL", f"binary file too small: {file_path}", str(file_path))
                return item_hashes, probe_state.findings
    validate_h8bin_file(file_path, probe_state, set())
    if any(finding.severity == "ERROR" for finding in probe_state.findings):
        return item_hashes, probe_state.findings
    with file_path.open("rb") as handle:
        with mmap.mmap(handle.fileno(), 0, access=mmap.ACCESS_READ) as mm_obj:
            section_count = struct.unpack_from("<H", mm_obj, header_size + 6)[0]
            section_table_offset = struct.unpack_from("<I", mm_obj, header_size + 8)[0]
            data_start_offset = struct.unpack_from("<I", mm_obj, header_size + 20)[0]
            table_rows = min(section_count, len(schema.section_order))
            entries_by_name: dict[str, tuple[int, int, int]] = {}
            for index in range(table_rows):
                entry_offset = section_table_offset + index * section_entry_size
                if entry_offset + 16 > len(mm_obj):
                    probe_state.add_error("ITEM_HASH_SECTION_RANGE", f"section entry {index} exceeds file", str(file_path), entry_offset)
                    break
                section_name = schema.section_order[index]
                record_size, count, offset = struct.unpack_from("<IIII", mm_obj, entry_offset)[1:]
                if section_name != "Items" or count == 0:
                    continue
                if record_size == 0:
                    probe_state.add_error("ITEM_HASH_RECORD_SIZE_ZERO", "Items section has zero record size", str(file_path), entry_offset + 4)
                    break
                byte_count = record_size * count
                end = offset + byte_count
                if offset < data_start_offset or end > len(mm_obj) or offset % section_alignment != 0:
                    probe_state.add_error("ITEM_HASH_SECTION_RANGE", "Items section range is not readable for CSV diff", str(file_path), entry_offset + 12)
                    break
                entries_by_name[section_name] = (record_size, count, offset)
            item_hashes = gather_item_hashes(mm_obj, entries_by_name, schema)
    return item_hashes, probe_state.findings


def extract_item_records_from_h8bin(
    file_path: Path,
    schema: SchemaModel,
    args: argparse.Namespace,
    field_names: set[str],
) -> tuple[set[int], dict[int, dict[str, int | float]], list[Finding]]:
    item_hashes, findings = extract_item_hashes_from_h8bin(file_path, schema, args)
    records: dict[int, dict[str, int | float]] = {}
    if any(finding.severity == "ERROR" for finding in findings) or not field_names:
        return item_hashes, records, findings
    layout = section_layout_for(schema, "Items")
    if layout is None:
        return item_hashes, records, findings
    hash_field = get_field(layout, "HashId")
    if hash_field is None:
        return item_hashes, records, findings
    requested_fields = [item for item in layout.fields if item.name in field_names]
    if not requested_fields:
        return item_hashes, records, findings
    header_size = schema.constant("HeaderSizeBytes", DEFAULT_HEADER_BYTES)
    section_entry_size = schema.structs.get("H8DataSectionEntry", StructLayout("", "16", 16, file_path, "")).size
    with file_path.open("rb") as handle:
        with mmap.mmap(handle.fileno(), 0, access=mmap.ACCESS_READ) as mm_obj:
            section_count = struct.unpack_from("<H", mm_obj, header_size + 6)[0]
            section_table_offset = struct.unpack_from("<I", mm_obj, header_size + 8)[0]
            table_rows = min(section_count, len(schema.section_order))
            for index in range(table_rows):
                section_name = schema.section_order[index]
                if section_name != "Items":
                    continue
                entry_offset = section_table_offset + index * section_entry_size
                record_size, count, offset = struct.unpack_from("<IIII", mm_obj, entry_offset)[1:]
                if record_size < layout.size:
                    return item_hashes, records, findings
                for record_index in range(count):
                    record_base = offset + record_index * record_size
                    item_hash = read_u32(mm_obj, record_base + hash_field.offset)
                    if not item_hash:
                        continue
                    values: dict[str, int | float] = {}
                    for field_layout in requested_fields:
                        type_name = normalize_type(field_layout.type_name)
                        field_offset = record_base + field_layout.offset
                        if type_name in {"float", "double"}:
                            values[field_layout.name] = read_floating(mm_obj, field_offset, type_name)
                        elif type_name in PRIMITIVE_TYPES:
                            values[field_layout.name] = read_integer(mm_obj, field_offset, type_name)
                    records[item_hash] = values
                break
    return item_hashes, records, findings


def validate_h8bin_file(file_path: Path, state: ValidationState, known_hashes: set[int]) -> None:
    metric = FileMetrics(str(file_path))
    state.files.append(metric)
    header_size = state.schema.constant("HeaderSizeBytes", DEFAULT_HEADER_BYTES)
    directory_size = state.schema.constant("DirectorySizeBytes", DEFAULT_DIRECTORY_BYTES)
    section_alignment = effective_section_alignment(state.schema)
    expected_magic = state.schema.constant("BlobMagic", DEFAULT_MAGIC)
    expected_version = state.schema.constant("FormatVersion", DEFAULT_FORMAT_VERSION)
    section_entry_size = state.schema.structs.get("H8DataSectionEntry", StructLayout("", "16", 16, file_path, "")).size
    with file_path.open("rb") as handle:
        if os.fstat(handle.fileno()).st_size == 0:
            state.add_error("FILE_EMPTY", "h8bin file is empty", str(file_path))
            metric.status = "ERROR"
            return
        with mmap.mmap(handle.fileno(), 0, access=mmap.ACCESS_READ) as mm_obj:
            metric.bytes = len(mm_obj)
            state.bytes_processed += len(mm_obj)
            if len(mm_obj) < 4:
                state.add_error("FILE_TOO_SMALL", f"{file_path} is smaller than a 4-byte magic", str(file_path), 0)
                metric.status = "ERROR"
                return
            initial_magic = struct.unpack_from("<I", mm_obj, 0)[0]
            if initial_magic == VOCAL_BANK_MAGIC:
                validate_vocal_bank_h8bin(file_path, mm_obj, state, metric)
                return
            if len(mm_obj) < header_size + directory_size:
                state.add_error("FILE_TOO_SMALL", f"{file_path} is smaller than header+directory", str(file_path), 0)
                metric.status = "ERROR"
                return
            if len(mm_obj) > state.args.max_bytes:
                state.add_error("FILE_TOO_LARGE", f"{file_path} exceeds runtime byte cap {state.args.max_bytes}", str(file_path), 0)
            if len(mm_obj) % section_alignment != 0:
                state.add_error(
                    "FILE_ALIGNMENT",
                    f"{file_path} length {len(mm_obj)} is not {section_alignment}-byte aligned",
                    str(file_path),
                    len(mm_obj),
                    format_hexdump(mm_obj, max(0, len(mm_obj) - 1)),
            )
            header_magic, header_version, header_bytes, checksum = struct.unpack_from("<IHHQ", mm_obj, 0)
            if header_magic != expected_magic:
                state.add_error(
                    "HEADER_MAGIC",
                    f"Header magic 0x{header_magic:08X} expected 0x{expected_magic:08X}",
                    str(file_path),
                    0,
                    format_hexdump(mm_obj, 0),
                )
            if header_version != expected_version:
                state.add_error("HEADER_VERSION", f"Header version {header_version} expected {expected_version}", str(file_path), 4)
            if header_bytes != header_size:
                state.add_error("HEADER_BYTES", f"Header byte-count {header_bytes} expected {header_size}", str(file_path), 6)
            computed_checksum = compute_payload_checksum(mm_obj, header_size)
            if checksum != computed_checksum:
                state.add_error(
                    "XXHASH3_MISMATCH",
                    f"stored=0x{checksum:016X} computed=0x{computed_checksum:016X}",
                    str(file_path),
                    8,
                    format_hexdump(mm_obj, 0),
                )

            directory_format = "<IHH" + ("I" * 14)
            directory_values = struct.unpack_from(directory_format, mm_obj, header_size)
            directory_magic = directory_values[0]
            directory_version = directory_values[1]
            section_count = directory_values[2]
            section_table_offset = directory_values[3]
            section_table_bytes = directory_values[4]
            blob_bytes = directory_values[5]
            data_start_offset = directory_values[6]
            localization_offset = directory_values[7]
            localization_bytes = directory_values[8]
            directory_flags = directory_values[9]
            if directory_magic != expected_magic:
                state.add_error("DIRECTORY_MAGIC", f"Directory magic 0x{directory_magic:08X} expected 0x{expected_magic:08X}", str(file_path), header_size)
            if directory_version != expected_version:
                state.add_error("DIRECTORY_VERSION", f"Directory version {directory_version} expected {expected_version}", str(file_path), header_size + 4)
            if blob_bytes != len(mm_obj):
                state.add_error("BLOB_BYTES", f"Directory BlobBytes {blob_bytes} expected {len(mm_obj)}", str(file_path), header_size + 16)
            if section_count != len(state.schema.section_order):
                state.add_error("SECTION_COUNT", f"SectionCount {section_count} expected {len(state.schema.section_order)}", str(file_path), header_size + 6)
            expected_table_offset = header_size + directory_size
            expected_table_bytes = section_count * section_entry_size
            if section_table_offset != expected_table_offset:
                state.add_error("SECTION_TABLE_OFFSET", f"SectionTableOffset {section_table_offset} expected {expected_table_offset}", str(file_path), header_size + 8)
            if section_table_bytes != expected_table_bytes:
                state.add_error("SECTION_TABLE_BYTES", f"SectionTableBytes {section_table_bytes} expected {expected_table_bytes}", str(file_path), header_size + 12)
            expected_data_start_offset = align_up(section_table_offset + section_table_bytes, section_alignment)
            if data_start_offset != expected_data_start_offset or data_start_offset % section_alignment != 0:
                state.add_error(
                    "DATA_START",
                    f"DataStartOffset {data_start_offset} expected aligned table end {expected_data_start_offset}",
                    str(file_path),
                    header_size + 20,
                )
            if section_table_offset + section_table_bytes > len(mm_obj):
                state.add_error("SECTION_TABLE_RANGE", "Section table exceeds blob length", str(file_path), section_table_offset)

            entries_by_name: dict[str, tuple[int, int, int]] = {}
            occupied_ranges: list[tuple[int, int, str]] = [(0, min(len(mm_obj), data_start_offset), "fixed_header_directory_table")]
            localization_entry: tuple[int, int, int] | None = None
            table_rows = min(section_count, len(state.schema.section_order))
            for index in range(table_rows):
                entry_offset = section_table_offset + index * section_entry_size
                if entry_offset + 16 > len(mm_obj):
                    state.add_error("SECTION_ENTRY_RANGE", f"Section entry {index} exceeds blob", str(file_path), entry_offset)
                    continue
                entry_dump = format_hexdump(mm_obj, entry_offset)
                section_id, record_size, count, offset = struct.unpack_from("<IIII", mm_obj, entry_offset)
                section_name = state.schema.section_order[index]
                expected_id = state.schema.enum_sections.get(section_name, index + 1)
                expected_record_size = state.schema.expected_sizes.get(section_name)
                struct_name = state.schema.section_structs.get(section_name, "")
                section_schema = SectionSchema(expected_id, section_name, expected_record_size or 0, struct_name)
                if section_id != expected_id:
                    state.add_error(
                        "SECTION_ORDER",
                        f"section index {index} id {section_id} expected {expected_id}",
                        str(file_path),
                        entry_offset,
                        entry_dump,
                    )
                if expected_record_size is not None and record_size != expected_record_size:
                    state.add_error(
                        "RECORD_SIZE",
                        f"{section_name} record size {record_size} expected {expected_record_size}",
                        str(file_path),
                        entry_offset + 4,
                        entry_dump,
                    )
                if count == 0:
                    if offset != 0:
                        state.add_error(
                            "EMPTY_SECTION_OFFSET",
                            f"{section_name} is empty but offset is {offset}",
                            str(file_path),
                            entry_offset + 12,
                            entry_dump,
                        )
                    entries_by_name[section_name] = (record_size, count, offset)
                    continue
                section_layout = section_layout_for(state.schema, section_name)
                section_payload_valid = True
                if record_size == 0:
                    state.add_error(
                        "RECORD_SIZE_ZERO",
                        f"{section_name} declares {count} records with zero record size",
                        str(file_path),
                        entry_offset + 4,
                        entry_dump,
                        section_layout.describe() if section_layout is not None else "",
                    )
                    section_payload_valid = False
                elif section_layout is not None and record_size < section_layout.size:
                    state.add_error(
                        "RECORD_SIZE_UNDER_STRUCT",
                        f"{section_name} record size {record_size} is smaller than {section_layout.name} size {section_layout.size}",
                        str(file_path),
                        entry_offset + 4,
                        entry_dump,
                        section_layout.describe(),
                    )
                    section_payload_valid = False
                if expected_record_size is not None and record_size != expected_record_size:
                    section_payload_valid = False
                byte_count = record_size * count
                end = offset + byte_count
                if offset % section_alignment != 0:
                    state.add_error(
                        "SECTION_ALIGNMENT",
                        f"{section_name} offset {offset} is not {section_alignment}-byte aligned",
                        str(file_path),
                        entry_offset + 12,
                        entry_dump,
                    )
                section_range_valid = True
                if offset < data_start_offset:
                    state.add_error(
                        "SECTION_OVER_FIXED_RANGE",
                        f"{section_name} offset {offset} overlaps fixed area",
                        str(file_path),
                        entry_offset + 12,
                        entry_dump,
                    )
                    section_range_valid = False
                if end > len(mm_obj):
                    state.add_error(
                        "SECTION_OUT_OF_FILE",
                        f"{section_name} end {end} exceeds file length {len(mm_obj)}",
                        str(file_path),
                        entry_offset + 12,
                        entry_dump,
                    )
                    section_range_valid = False
                for range_start, range_end, owner in occupied_ranges:
                    if offset < range_end and end > range_start:
                        state.add_error(
                            "SECTION_OVERLAP",
                            f"{section_name} [{offset},{end}) overlaps {owner} [{range_start},{range_end})",
                            str(file_path),
                            entry_offset + 12,
                            entry_dump,
                        )
                        section_range_valid = False
                        break
                if section_range_valid:
                    occupied_ranges.append((offset, end, section_name))
                    if section_payload_valid:
                        entries_by_name[section_name] = (record_size, count, offset)
                        if section_name == "LocalizationUtf8":
                            localization_entry = (record_size, count, offset)
            if localization_entry:
                _, loc_count, loc_offset = localization_entry
                if localization_offset != loc_offset or localization_bytes != loc_count:
                    state.add_error(
                        "LOCALIZATION_DIRECTORY",
                        f"Directory localization {localization_offset}/{localization_bytes} does not match section {loc_offset}/{loc_count}",
                        str(file_path),
                        header_size + 24,
                    )
            elif localization_offset or localization_bytes:
                state.add_error("LOCALIZATION_ORPHAN", "Directory declares localization bytes but section is empty", str(file_path), header_size + 24)

            rle_flag = state.schema.constants.get("RleDirectoryFlag", 0)
            little_endian_flag = state.schema.constants.get("BlobFlagLittleEndian", 0)
            supported_flags = rle_flag | little_endian_flag
            unsupported_flags = directory_flags & ~supported_flags
            if unsupported_flags:
                state.add_error(
                    "DIRECTORY_FLAGS_UNSUPPORTED",
                    f"Directory Flags contains unsupported bits 0x{unsupported_flags:08X}",
                    str(file_path),
                    header_size + 32,
                    format_hexdump(mm_obj, header_size + 32),
                )

            item_hashes = gather_item_hashes(mm_obj, entries_by_name, state.schema) | known_hashes
            sampled_total = 0
            for index in range(table_rows):
                section_name = state.schema.section_order[index]
                entry = entries_by_name.get(section_name)
                if not entry:
                    continue
                record_size, count, offset = entry
                if count == 0:
                    continue
                section_id = state.schema.enum_sections.get(section_name, index + 1)
                section_schema = SectionSchema(
                    section_id,
                    section_name,
                    state.schema.expected_sizes.get(section_name, record_size),
                    state.schema.section_structs.get(section_name, ""),
                )
                sampled_total += validate_record_sample(
                    mm_obj,
                    file_path,
                    section_schema,
                    record_size,
                    count,
                    offset,
                    state,
                    item_hashes,
                )
                if rle_flag and (directory_flags & rle_flag):
                    validate_rle_probe(mm_obj, file_path, offset, record_size * count, state)
            metric.sections = table_rows
            metric.sampled_records = sampled_total
            metric.status = "PASS" if state.ok else "ERROR"


def fnv1a32_ascii_lower(text: str) -> int:
    value = 0x811C9DC5
    for raw in text.encode("utf-8"):
        byte = raw + 32 if 65 <= raw <= 90 else raw
        value ^= byte
        value = (value * 0x01000193) & MASK32
    return value


def csv_to_bin_diff(csv_path: Path, h8bin_path: Path, state: ValidationState) -> None:
    if not csv_path.exists():
        state.add_error("CSV_DIFF_SOURCE_MISSING", f"CSV source missing: {csv_path}", str(csv_path))
        return
    if not h8bin_path.exists():
        state.add_error("CSV_DIFF_BIN_MISSING", f"Binary target missing: {h8bin_path}", str(h8bin_path))
        return
    item_layout = section_layout_for(state.schema, "Items")
    item_fields = {field.name: field for field in item_layout.fields} if item_layout is not None else {}
    csv_hashes: set[int] = set()
    csv_value_rows: dict[int, dict[str, int | float]] = {}
    rows_seen = 0
    with csv_path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        if not reader.fieldnames:
            state.add_error("CSV_DIFF_EMPTY_SOURCE", f"CSV source has no header: {csv_path}", str(csv_path))
            return
        for row in reader:
            rows_seen += 1
            candidate = ""
            for raw_key, raw_value in row.items():
                key = raw_key.strip().lower() if raw_key else ""
                if key in CSV_DIFF_HASH_KEYS and raw_value and raw_value.strip():
                    candidate = raw_value.strip()
                    break
            if not candidate:
                continue
            parsed_hash: int
            try:
                parsed = int(candidate, 0) & MASK32
                if parsed == 0:
                    state.add_error("CSV_DIFF_ZERO_HASH", f"CSV source contains zero hash in {csv_path}", str(csv_path))
                    continue
                parsed_hash = parsed
            except ValueError:
                parsed_hash = fnv1a32_ascii_lower(candidate)
            csv_hashes.add(parsed_hash)
            row_values: dict[str, int | float] = {}
            for raw_key, raw_value in row.items():
                key = raw_key.strip().lower() if raw_key else ""
                if not raw_value or not raw_value.strip():
                    continue
                field_name = CSV_ITEM_FIELD_ALIASES.get(key)
                if not field_name:
                    continue
                field_layout = item_fields.get(field_name)
                if field_layout is None:
                    state.add_error(
                        "CSV_TO_BIN_FIELD_UNMAPPED",
                        f"CSV column {raw_key} maps to {field_name}, but H8ItemRecord has no matching field",
                        str(csv_path),
                    )
                    continue
                type_name = normalize_type(field_layout.type_name)
                text_value = raw_value.strip()
                try:
                    if type_name in {"float", "double"}:
                        parsed_value: int | float = float(text_value)
                    elif type_name in PRIMITIVE_TYPES:
                        parsed_value = int(text_value, 0)
                    else:
                        continue
                except ValueError:
                    state.add_error(
                        "CSV_TO_BIN_FIELD_PARSE",
                        f"CSV column {raw_key} value {text_value!r} cannot be parsed as {type_name}",
                        str(csv_path),
                    )
                    continue
                row_values[field_name] = parsed_value
            if row_values:
                csv_value_rows[parsed_hash] = row_values
    if not csv_hashes:
        state.add_error(
            "CSV_DIFF_NO_HASH_ROWS",
            f"CSV source produced zero hash rows from {rows_seen} data rows; expected one of {', '.join(CSV_DIFF_HASH_KEYS)}",
            str(csv_path),
        )
        return
    value_field_names = {field_name for values in csv_value_rows.values() for field_name in values}
    if value_field_names:
        item_hashes, item_records, probe_findings = extract_item_records_from_h8bin(h8bin_path, state.schema, state.args, value_field_names)
    else:
        item_hashes, probe_findings = extract_item_hashes_from_h8bin(h8bin_path, state.schema, state.args)
        item_records = {}
    probe_failed = False
    for finding in probe_findings:
        if finding.severity == "ERROR":
            state.findings.append(finding)
            probe_failed = True
    if probe_failed and state.args.fail_fast:
        raise FailFastAbort("csv diff binary probe failed")
    if probe_failed:
        return
    missing = sorted(csv_hashes - item_hashes) if item_hashes else sorted(csv_hashes)
    extra = sorted(item_hashes - csv_hashes) if csv_hashes else []
    if missing:
        state.add_error(
            "CSV_TO_BIN_MISSING_HASHES",
            f"{len(missing)} source hashes are absent from Items.HashId; first=0x{missing[0]:08X}",
            str(csv_path),
        )
    if extra:
        state.add_info("CSV_TO_BIN_EXTRA_HASHES", f"{len(extra)} binary item hashes are not present in CSV source", str(h8bin_path))
    value_mismatch = False
    for item_hash, expected_values in sorted(csv_value_rows.items()):
        if item_hash not in item_hashes:
            continue
        actual_values = item_records.get(item_hash, {})
        for field_name, expected_value in sorted(expected_values.items()):
            if field_name not in actual_values:
                value_mismatch = True
                state.add_error(
                    "CSV_TO_BIN_FIELD_UNREADABLE",
                    f"H8ItemRecord field {field_name} for hash 0x{item_hash:08X} was not readable from binary",
                    str(h8bin_path),
                )
                continue
            actual_value = actual_values[field_name]
            if isinstance(expected_value, float) or isinstance(actual_value, float):
                actual_float = float(actual_value)
                expected_float = float(expected_value)
                tolerance = max(1.0e-5, abs(expected_float) * 1.0e-5)
                mismatch = (not math.isfinite(actual_float)) or abs(actual_float - expected_float) > tolerance
            else:
                mismatch = int(actual_value) != int(expected_value)
            if mismatch:
                value_mismatch = True
                state.add_error(
                    "CSV_TO_BIN_VALUE_MISMATCH",
                    f"hash 0x{item_hash:08X} field {field_name}: csv={expected_value} binary={actual_value}",
                    str(csv_path),
                )
    if not missing and not value_mismatch:
        state.add_info("CSV_TO_BIN_DIFF_OK", f"CSV hashes/fields matched binary Items records: {len(csv_hashes)}", str(csv_path))


def build_report(state: ValidationState) -> dict[str, Any]:
    elapsed = time.perf_counter() - state.start_time
    agent_id = getattr(state.args, "agent_id", "SHINOBU_358")
    primary_structs = {
        name: {
            "size": layout.size,
            "largest_alignment": layout.largest_alignment,
            "final_padding": layout.final_padding,
            "payload_padding": layout.payload_padding,
            "fields": [
                {
                    "name": item.name,
                    "type": item.type_name,
                    "offset": item.offset,
                    "size": item.size,
                    "alignment": item.alignment,
                }
                for item in sorted(layout.fields, key=lambda f: f.offset)
            ],
        }
        for name, layout in sorted(state.schema.structs.items())
        if name in {"H8DataBlobHeader", "H8DataBlobDirectory", "H8DataSectionEntry"}
    }
    self_audit = {
        "agent_id": agent_id,
        "tool_origin_agent_id": "SHINOBU_358",
        "legacy_tool_owner": "SHINOBU_258",
        "task_count": 20,
        "standalone_python": True,
        "unity_runtime_dependency": False,
        "mmap_file_access": True,
        "watchdog_seconds": WATCHDOG_SECONDS,
        "checksum": "Unity.Collections.xxHash3.Hash64 pure-Python oracle over bytes[headerBytes..end)",
        "csharp_parser": {
            "mode": "deterministic mmap token stream; no regex in StructLayout/FieldOffset AST path",
            "struct": "attribute token blocks -> StructLayout token call -> LayoutKind.Explicit token -> Size token expression -> struct body brace span",
            "field": "field attribute token blocks -> FieldOffset token call -> declaration token statement -> byte offset expression",
            "combined_attributes": "supports combined attribute lists and fully qualified Attribute suffix calls",
            "property_gate": "get/set/init accessors and top-level expression-bodied properties inside unmanaged layout contracts emit STRUCT_PROPERTY_BANNED and exit code 3",
            "fixed_buffer_gate": "fixed primitive buffers are resolved as elementSize * declared constant length without treating [] as attributes",
            "nested_scope_gate": "nested explicit structs are parsed through their own candidate spans; outer job/queue structs do not inherit nested FieldOffset tokens",
            "cache_schema": AST_CACHE_SCHEMA,
            "section_order": "SectionOrder = { H8DataSectionId.* }",
            "record_size": "case H8DataSectionId.*: return ...;",
        },
        "compile_guard": "No dotnet or Unity build invoked by this tool.",
        "dear_lie": "Payload validation samples deterministic 5% by default after full header/table proof; --thorough validates all records.",
        "section_alignment_floor": "C# SectionAlignmentBytes must be >=16; effective section/file/data-start alignment never drops below 16 bytes.",
        "record_stride_gate": "Non-empty sections must declare a non-zero stride large enough to contain the parsed C# explicit-layout struct before hash extraction or sampling.",
        "directory_flag_contract": "Supported H8DM directory flags are BlobFlagLittleEndian and RleDirectoryFlag when defined by C#; unknown bits fail as DIRECTORY_FLAGS_UNSUPPORTED.",
        "csv_diff_field_gate": "--csv-diff compares known numeric H8ItemRecord fields such as Cost/MassKg/VolumeM3 when present in the source CSV, not only Items.HashId.",
        "integer_aup_gate": "Integer AUP fields named Aup*/AUP* are checked against +/-100000 just like floating AUP coordinates.",
        "reference_master_gate": "Recipe/Loot references fail as REFERENCE_MASTER_EMPTY when Items.HashId cannot be proven, avoiding false BROKEN_FOREIGN_KEY proof.",
        "mmap_release_on_fail_fast": "Checksum memoryview is scoped to compute_payload_checksum and released in finally before fail-fast findings can unwind.",
        "sidecar_schema_guard": "H8VB Audio/VocalBank payloads are routed by magic before H8DM parsing and validated through the source-backed 64-byte header, 32-byte index, FNV bank hash, 16-byte aligned payload ranges with zeroed inter-record padding, supported codec set, and ADPCM block headers.",
        "global_quality_weight": "Not consumed. This CI validator never changes gameplay truth ownership, DTO layout, save identity, or authority route.",
        "vault_status": "No Unity NativeArray/GlobalDataVault allocations. OS mmap only.",
        "job_graph": "No Burst jobs, no JobHandle, no Complete. External CI process only.",
        "migration_summary": "Report groups runtime text artifacts and loader sites by required route owner.",
    }
    return {
        "schema": "h8bin_validator.report.v2",
        "agent_id": agent_id,
        "tool_origin_agent_id": "SHINOBU_358",
        "legacy_tool_owner": "SHINOBU_258",
        "status": "PASS" if state.ok else "FAIL",
        "elapsed_seconds": round(elapsed, 6),
        "bytes_processed": state.bytes_processed,
        "mb_processed": round(state.bytes_processed / (1024 * 1024), 6),
        "files_checked": len(state.files),
        "structs_parsed": len(state.schema.structs),
        "sections_expected": [
            {
                "index": index,
                "name": name,
                "id": state.schema.enum_sections.get(name),
                "record_size": state.schema.expected_sizes.get(name),
                "struct": state.schema.section_structs.get(name, ""),
            }
            for index, name in enumerate(state.schema.section_order)
        ],
        "primary_struct_layouts": primary_structs,
        "files": [metric.__dict__ for metric in state.files],
        "findings": [finding.to_json() for finding in state.findings],
        "migration_summary": build_migration_summary(state.findings),
        "self_audit": self_audit,
    }


def build_migration_summary(findings: list[Finding]) -> dict[str, Any]:
    counts: dict[str, int] = {}
    owners: dict[str, dict[str, Any]] = {}
    for finding in findings:
        if finding.severity != "ERROR":
            continue
        counts[finding.code] = counts.get(finding.code, 0) + 1
        if finding.code not in {"UNBAKED_ARTIFACT", "RUNTIME_TEXT_STREAMINGASSETS_LOAD"}:
            continue
        route = classify_migration_route(finding)
        owner = route["owner"]
        entry = owners.setdefault(
            owner,
            {
                "owner": owner,
                "source_root": route["source_root"],
                "runtime_route": route["runtime_route"],
                "artifacts": [],
                "loader_sites": [],
            },
        )
        if finding.code == "UNBAKED_ARTIFACT":
            append_unique(entry["artifacts"], normalize_report_path(finding.path))
        elif finding.references:
            append_unique(entry["loader_sites"], finding.references[0])
        else:
            append_unique(entry["loader_sites"], finding.message)

    owner_rows = []
    for owner in sorted(owners):
        row = owners[owner]
        row["artifact_count"] = len(row["artifacts"])
        row["loader_site_count"] = len(row["loader_sites"])
        row["next_action"] = (
            "Move human-readable sources to the listed source_root, bake an aligned binary payload, "
            "then remove runtime StreamingAssets text loaders or isolate editor-only facades."
        )
        owner_rows.append(row)

    return {
        "schema": "h8bin_validator.migration_summary.v1",
        "active_runtime_payload": f"Assets/StreamingAssets/{DEFAULT_STATIC_DATA_RELATIVE.as_posix()}",
        "blocking_counts": {key: counts[key] for key in sorted(counts)},
        "owner_count": len(owner_rows),
        "owners": owner_rows,
    }


def classify_migration_route(finding: Finding) -> dict[str, str]:
    text = " ".join([finding.message, finding.path, " ".join(finding.references)]).lower()
    for rule in MIGRATION_ROUTE_RULES:
        if any(token in text for token in rule["tokens"]):
            return {
                "owner": rule["owner"],
                "source_root": rule["source_root"],
                "runtime_route": rule["runtime_route"],
            }
    return {
        "owner": "Unclassified",
        "source_root": "Assets/_SourceData/<owner>",
        "runtime_route": "owner-domain .h8bin or static_data.h8bin section loaded before runtime use",
    }


def append_unique(values: list[str], value: str) -> None:
    if value and value not in values:
        values.append(value)


def normalize_report_path(path_text: str) -> str:
    if not path_text:
        return ""
    normalized = Path(path_text)
    try:
        return normalized.relative_to(PROJECT_ROOT).as_posix()
    except ValueError:
        return path_text.replace("\\", "/")


def write_json_report(path: Path, report: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temp_path = path.with_name(f".{path.name}.tmp.{os.getpid()}")
    with temp_path.open("w", encoding="utf-8") as handle:
        json.dump(report, handle, indent=2)
        handle.write("\n")
    replace_or_copy_report(temp_path, path)


def write_junit_report(path: Path, report: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    errors = [finding for finding in report["findings"] if finding["severity"] == "ERROR"]
    suite = ET.Element(
        "testsuite",
        {
            "name": "h8bin_validator",
            "tests": str(max(1, len(report["files"]))),
            "failures": str(len(errors)),
            "time": str(report["elapsed_seconds"]),
        },
    )
    if not report["files"]:
        ET.SubElement(suite, "testcase", {"name": "no_files"})
    emitted_errors: set[int] = set()
    for file_metric in report["files"]:
        case = ET.SubElement(suite, "testcase", {"name": file_metric["path"], "time": "0"})
        for index, finding in enumerate(errors):
            if finding.get("path") != file_metric["path"]:
                continue
            failure = ET.SubElement(case, "failure", {"type": finding["code"], "message": finding["message"]})
            failure.text = finding.get("hexdump", "")
            emitted_errors.add(index)
    for index, finding in enumerate(errors):
        if index in emitted_errors:
            continue
        case_name = finding.get("path") or finding["code"]
        case = ET.SubElement(suite, "testcase", {"name": case_name, "time": "0"})
        failure = ET.SubElement(case, "failure", {"type": finding["code"], "message": finding["message"]})
        failure.text = finding.get("hexdump", "")
    suite.set("tests", str(len(suite.findall("testcase"))))
    ET.ElementTree(suite).write(path, encoding="utf-8", xml_declaration=True)


def append_metrics(path: Path, report: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    warning = " PERF_WARN" if report["elapsed_seconds"] > 0.5 else ""
    stamp = time.strftime("%Y-%m-%d")
    line = (
        f"{stamp} {report['agent_id']} status={report['status']} "
        f"mb={report['mb_processed']} files={report['files_checked']} "
        f"structs={report['structs_parsed']} seconds={report['elapsed_seconds']}{warning}\n"
    )
    with path.open("a", encoding="utf-8") as handle:
        handle.write(line)


def append_metric_phi_data_truth_row(path: Path, report: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    row = {
        "agentId": report["agent_id"],
        "tool": "h8bin_validator",
        "status": report["status"],
        "filesScanned": report["files_checked"],
        "structsValidated": report["structs_parsed"],
        "arm64Violations": sum(1 for item in report["findings"] if item["code"] in ARM64_ALIGNMENT_CODES),
        "propertyViolations": sum(1 for item in report["findings"] if item["code"] in PROPERTY_BAN_CODES),
        "schemaMismatches": sum(1 for item in report["findings"] if item["code"] in SCHEMA_MISMATCH_CODES),
        "executionTimeMs": round(float(report["elapsed_seconds"]) * 1000.0, 3),
        "performanceRegressionWarning": float(report["elapsed_seconds"]) > 0.5,
    }
    payload: dict[str, Any]
    if path.exists():
        try:
            with path.open("r", encoding="utf-8-sig") as handle:
                existing = json.load(handle)
        except Exception:
            existing = None
        if isinstance(existing, dict) and isinstance(existing.get("rows"), list):
            rows = existing["rows"]
        elif isinstance(existing, list):
            rows = existing
        elif existing is None:
            rows = []
        else:
            rows = [{"legacyPayload": existing}]
    else:
        rows = []
    rows.append(row)
    payload = {
        "schema": "hecton8.metric_phi.binary_schema_ast_rows.v1",
        "rows": rows[-300:],
    }
    temp_path = path.with_name(f".{path.name}.tmp.{os.getpid()}")
    with temp_path.open("w", encoding="utf-8") as handle:
        json.dump(payload, handle, indent=2)
        handle.write("\n")
    replace_or_copy_report(temp_path, path)


def replace_or_copy_report(temp_path: Path, path: Path) -> None:
    try:
        os.replace(temp_path, path)
        return
    except OSError:
        pass

    try:
        path.write_text(temp_path.read_text(encoding="utf-8"), encoding="utf-8")
    finally:
        try:
            temp_path.unlink()
        except OSError:
            pass


def exit_code_for_findings(findings: list[Finding]) -> int:
    codes = {finding.code for finding in findings if finding.severity == "ERROR"}
    if codes & PROPERTY_BAN_CODES:
        return 3
    if codes & ARM64_ALIGNMENT_CODES:
        return 2
    if codes & SCHEMA_MISMATCH_CODES:
        return 4
    return 1 if codes else 0


def default_cs_roots() -> list[Path]:
    return [
        PROJECT_ROOT / "Assets/_Project/Scripts",
    ]


def generate_mock_csharp_structs(root: Path) -> Path:
    source = root / "Scripts"
    source.mkdir(parents=True, exist_ok=True)
    (source / "MockBinarySchemas.cs").write_text(
        """
using System.Runtime.InteropServices;
using Unity.Mathematics;

public static class H8DataLayoutConstants
{
    public const int HeaderSizeBytes = 16;
}

[StructLayout(LayoutKind.Explicit, Size = 16)]
public struct H8MockValidRecord
{
    [FieldOffset(0)] public double AupX;
    [FieldOffset(8)] public ulong HashId;
}

[StructLayout(LayoutKind.Explicit, Size = 16)]
public struct H8MockUnalignedRecord
{
    [FieldOffset(1)] public double AupX;
}

[StructLayout(LayoutKind.Explicit, Size = 16)]
public struct H8MockPropertyRecord
{
    public int SlowCopy { get; set; }
    [FieldOffset(0)] public int RawValue;
}

[StructLayout(LayoutKind.Explicit, Size = 12)]
public struct H8MockBadSizeRecord
{
    [FieldOffset(0)] public double AupX;
}

[StructLayout(LayoutKind.Explicit, Size = 16)]
public struct H8MockFloatAupRecord
{
    [FieldOffset(0)] public float3 WorldPosition;
}
""".strip()
        + "\n",
        encoding="utf-8",
    )
    return source


def run_self_tests() -> int:
    with tempfile.TemporaryDirectory() as raw:
        source = generate_mock_csharp_structs(Path(raw))
        args = argparse.Namespace(fail_fast=False)
        placeholder = SchemaModel({}, {}, [], {}, {}, {})
        state = ValidationState(args, placeholder)
        files = collect_csharp_files([source])
        constants = parse_constants(files)
        structs = parse_structs(files, constants, state)
        state.schema = SchemaModel(constants, {}, [], structs, {}, {})
        validate_struct_layouts(structs, state)
        validate_aup_precision(structs, state)
        codes = {finding.code for finding in state.findings if finding.severity == "ERROR"}
        required = {
            "FIELD_ALIGNMENT",
            "STRUCT_PROPERTY_BANNED",
            "STRUCT_SIZE_ALIGNMENT",
            "AUP_FLOAT_FIELD",
        }
        missing = sorted(required - codes)
        if missing:
            print(f"SELF_TEST_FAIL missing={','.join(missing)}")
            return 1
        print(f"SELF_TEST_PASS structs={len(structs)} codes={','.join(sorted(codes))}")
        return 0


def start_watchdog() -> threading.Timer:
    timer = threading.Timer(WATCHDOG_SECONDS, lambda: os._exit(1))
    timer.daemon = True
    timer.start()
    return timer


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Validate HECTON-8 .h8bin Data Monolith payloads")
    parser.add_argument("--target-dir", default="Assets/StreamingAssets", help="directory containing runtime .h8bin files")
    parser.add_argument(
        "--cs-source-dir",
        action="append",
        default=None,
        help="C# source root to parse; can be supplied multiple times",
    )
    parser.add_argument("--report-json", default=BINARY_SCHEMA_AUDIT_REPORT)
    parser.add_argument("--agent-id", default="SHINOBU_358", help="agent id to stamp into the generated report")
    parser.add_argument("--report-junit", default="")
    parser.add_argument("--metrics-log", default="Docs/Reports/CI_BINARY_VALIDATION.log")
    parser.add_argument("--metric-phi-json", default=METRIC_PHI_BINARY_SCHEMA_ROWS)
    parser.add_argument("--known-hash-list", default="", help="optional newline-separated master hash allow-list")
    parser.add_argument("--fail-fast", action="store_true")
    parser.add_argument("--thorough", action="store_true", help="validate every sampled-capable record")
    parser.add_argument("--sample-percent", type=float, default=5.0)
    parser.add_argument("--max-bytes", type=int, default=DEFAULT_RUNTIME_MAX_BYTES)
    parser.add_argument("--no-require-static-data", action="store_true")
    parser.add_argument("--allow-unbaked-artifacts", action="store_true")
    parser.add_argument("--allow-runtime-text-loaders", action="store_true")
    parser.add_argument(
        "--runtime-source-dir",
        action="append",
        default=None,
        help="runtime C# root scanned for forbidden text StreamingAssets loaders; can be supplied multiple times",
    )
    parser.add_argument("--csv-diff", nargs=2, metavar=("SOURCE_CSV", "GENERATED_H8BIN"))
    parser.add_argument("--test", action="store_true", help="run SHINOBU_358 self-tests and exit")
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    if args.test:
        return run_self_tests()
    watchdog = start_watchdog()
    cs_roots = [resolve_path(value) for value in args.cs_source_dir] if args.cs_source_dir else default_cs_roots()
    runtime_roots = (
        [resolve_path(value) for value in args.runtime_source_dir]
        if args.runtime_source_dir
        else default_runtime_source_roots()
    )
    target_dir = resolve_path(args.target_dir)
    try:
        schema = parse_csharp_schema(cs_roots, args)
        state = ValidationState(args, schema)
        for finding in getattr(args, "_schema_findings", []):
            state.findings.append(finding)
        if args.fail_fast and not state.ok:
            raise FailFastAbort("schema validation failed")
        if not args.allow_unbaked_artifacts:
            sanitize_runtime_artifacts(target_dir, state)
        if not args.allow_runtime_text_loaders:
            scan_runtime_text_loaders(runtime_roots, state)
        files = collect_h8bin_files(target_dir)
        required_static = target_dir / DEFAULT_STATIC_DATA_RELATIVE
        if not args.no_require_static_data and not required_static.exists():
            state.add_error(
                "STATIC_DATA_MISSING",
                f"required Data Monolith payload is absent: {required_static}",
                str(required_static),
            )
        if not files:
            state.add_error("NO_H8BIN_FILES", f"no .h8bin files found under {target_dir}", str(target_dir))
        known_hashes = load_known_hashes(args.known_hash_list, state)
        if args.fail_fast and not state.ok:
            raise FailFastAbort("known hash list validation failed")
        for file_path in files:
            validate_h8bin_file(file_path, state, known_hashes)
        if args.csv_diff:
            csv_to_bin_diff(resolve_path(args.csv_diff[0]), resolve_path(args.csv_diff[1]), state)
    except FailFastAbort:
        pass
    finally:
        watchdog.cancel()
    report = build_report(state)
    write_json_report(resolve_path(args.report_json), report)
    if args.report_junit:
        write_junit_report(resolve_path(args.report_junit), report)
    append_metrics(resolve_path(args.metrics_log), report)
    append_metric_phi_data_truth_row(resolve_path(args.metric_phi_json), report)
    print(
        f"h8bin_validator status={report['status']} files={report['files_checked']} "
        f"structs={report['structs_parsed']} mb={report['mb_processed']} seconds={report['elapsed_seconds']}"
    )
    exit_code = exit_code_for_findings(state.findings)
    if exit_code:
        red = "\033[91m"
        reset = "\033[0m"
        for finding in state.findings[:12]:
            if finding.severity == "ERROR":
                print(f"{red}ERROR {finding.code}: {finding.message}{reset}", file=sys.stderr)
        return exit_code
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
