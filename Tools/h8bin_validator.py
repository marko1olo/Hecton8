#!/usr/bin/env python3
"""External HECTON-8 .h8bin schema validator.

Pure Python CI tool. It parses C# explicit layouts, validates Data Monolith
headers and section tables through mmap, and emits JSON/JUnit proof artifacts.
It deliberately does not import Unity or compiled C# assemblies.
"""

from __future__ import annotations

import argparse
import csv
import json
import math
import mmap
import os
import re
import struct
import sys
import time
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
AUP_BOUND_METERS = 100000.0
TEXT_RUNTIME_SUFFIXES = {".csv", ".json", ".xml"}
TEXT_ARTIFACT_SYMBOL_DECLARATION = re.compile(
    r"\b(?:(?:public|private|internal|protected)\s+)*"
    r"(?:const|static\s+readonly|readonly\s+static)\s+string\s+"
    r"(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*\"(?P<value>[^\"]+)\"",
    re.IGNORECASE,
)
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


def strip_csharp_comments(text: str) -> str:
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.DOTALL)
    text = re.sub(r"//.*", "", text)
    return text


def normalize_type(type_name: str) -> str:
    type_name = type_name.strip()
    if "." in type_name:
        type_name = type_name.rsplit(".", 1)[-1]
    return type_name.replace("readonly", "").strip()


def sanitize_int_expr(expr: str, constants: dict[str, int]) -> str:
    expr = expr.strip()
    expr = re.sub(r"\(\s*(?:uint|int|ushort|short|ulong|long)\s*\)", "", expr)
    expr = re.sub(r"\b(?:uint|int|ushort|short|ulong|long)\s*\)", ")", expr)
    expr = re.sub(r"(?<=\d)[uUlL]+\b", "", expr)
    for key in sorted(constants, key=len, reverse=True):
        expr = re.sub(rf"\b{re.escape(key)}\b", str(constants[key]), expr)
        bare = key.rsplit(".", 1)[-1]
        expr = re.sub(rf"\b{re.escape(bare)}\b", str(constants[key]), expr)
    if not re.fullmatch(r"[0-9xa-fA-F+\-*/%() <>&|~]+", expr):
        raise ValueError(f"unsupported integer expression: {expr!r}")
    return expr


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


def find_matching_brace(text: str, start: int) -> int:
    depth = 0
    for index in range(start, len(text)):
        char = text[index]
        if char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return index
    return -1


def extract_named_argument(attribute: str, name: str) -> str | None:
    match = re.search(rf"\b{re.escape(name)}\s*=", attribute)
    if not match:
        return None
    cursor = match.end()
    depth = 0
    value: list[str] = []
    while cursor < len(attribute):
        char = attribute[cursor]
        if char == "," and depth == 0:
            break
        if char == ")" and depth == 0:
            break
        value.append(char)
        if char == "(":
            depth += 1
        elif char == ")" and depth > 0:
            depth -= 1
        cursor += 1
    return "".join(value).strip()


def read_balanced_span(text: str, start: int, open_char: str, close_char: str) -> tuple[str, int]:
    if start >= len(text) or text[start] != open_char:
        return "", -1
    depth = 0
    cursor = start
    quote = ""
    escaped = False
    while cursor < len(text):
        char = text[cursor]
        if quote:
            if escaped:
                escaped = False
            elif char == "\\":
                escaped = True
            elif char == quote:
                quote = ""
            cursor += 1
            continue
        if char in {'"', "'"}:
            quote = char
            cursor += 1
            continue
        if char == open_char:
            depth += 1
        elif char == close_char:
            depth -= 1
            if depth == 0:
                return text[start : cursor + 1], cursor + 1
        cursor += 1
    return "", -1


def skip_csharp_whitespace(text: str, cursor: int) -> int:
    while cursor < len(text) and text[cursor].isspace():
        cursor += 1
    return cursor


def extract_attribute_call(attribute_blocks: list[str], attribute_name: str) -> str | None:
    pattern = re.compile(
        rf"(?:global::)?(?:[\w]+\.)*{re.escape(attribute_name)}(?:Attribute)?\s*\(",
        re.DOTALL,
    )
    for block in attribute_blocks:
        for match in pattern.finditer(block):
            open_paren = block.find("(", match.start())
            call_args, end = read_balanced_span(block, open_paren, "(", ")")
            if end >= 0:
                return block[match.start() : open_paren] + call_args
    return None


def extract_first_argument(attribute_call: str) -> str | None:
    open_paren = attribute_call.find("(")
    if open_paren < 0:
        return None
    cursor = open_paren + 1
    depth = 0
    value: list[str] = []
    while cursor < len(attribute_call):
        char = attribute_call[cursor]
        if char == "," and depth == 0:
            break
        if char == ")" and depth == 0:
            break
        value.append(char)
        if char == "(":
            depth += 1
        elif char == ")" and depth > 0:
            depth -= 1
        cursor += 1
    result = "".join(value).strip()
    return result or None


def parse_field_declaration(statement: str) -> tuple[str, str] | None:
    compact = " ".join(statement.strip().split())
    compact = re.sub(r"\s*=\s*.*$", "", compact).strip()
    fixed_match = re.search(
        r"\bfixed\s+(?P<type>[\w\.]+)\s+(?P<name>\w+)\s*\[[^\]]+\]\s*$",
        compact,
    )
    if fixed_match:
        return fixed_match.group("type"), fixed_match.group("name")
    field_match = re.search(r"(?P<type>[\w\.]+)\s+(?P<name>\w+)\s*$", compact)
    if not field_match:
        return None
    return field_match.group("type"), field_match.group("name")


def parse_fieldoffset_fields(body: str, constants: dict[str, int], path: Path, struct_name: str, state: ValidationState | None) -> list[FieldLayout]:
    fields: list[FieldLayout] = []
    pending_attributes: list[str] = []
    cursor = 0
    while cursor < len(body):
        cursor = skip_csharp_whitespace(body, cursor)
        if cursor >= len(body):
            break
        if body[cursor] == "[":
            attribute, end = read_balanced_span(body, cursor, "[", "]")
            if end < 0:
                break
            pending_attributes.append(attribute)
            cursor = end
            continue
        semicolon = body.find(";", cursor)
        open_brace = body.find("{", cursor)
        if semicolon < 0:
            break
        if open_brace >= 0 and open_brace < semicolon:
            pending_attributes = []
            cursor = open_brace + 1
            continue
        field_offset_attr = extract_attribute_call(pending_attributes, "FieldOffset")
        statement = body[cursor:semicolon]
        if field_offset_attr:
            parsed_decl = parse_field_declaration(statement)
            if not parsed_decl:
                if state:
                    state.add_error("FIELD_DECL_UNPARSED", f"{struct_name} field declaration could not be parsed: {statement.strip()}", str(path))
                pending_attributes = []
                cursor = semicolon + 1
                continue
            offset_expr = extract_first_argument(field_offset_attr)
            if not offset_expr:
                if state:
                    state.add_error("FIELD_OFFSET_MISSING", f"{struct_name}.{parsed_decl[1]} FieldOffset has no argument", str(path))
                pending_attributes = []
                cursor = semicolon + 1
                continue
            try:
                offset = eval_int_expr(offset_expr, constants)
            except Exception as exc:
                if state:
                    state.add_error("FIELD_OFFSET_UNRESOLVED", f"{struct_name}.{parsed_decl[1]} offset failed: {exc}", str(path))
                pending_attributes = []
                cursor = semicolon + 1
                continue
            fields.append(FieldLayout(name=parsed_decl[1], type_name=normalize_type(parsed_decl[0]), offset=offset))
        pending_attributes = []
        cursor = semicolon + 1
    return fields


def parse_constants(files: list[Path]) -> dict[str, int]:
    raw: dict[str, str] = {}
    class_re = re.compile(r"\b(?:public|internal|private)?\s*(?:static\s+)?class\s+(\w+)\s*\{")
    const_re = re.compile(
        r"\bpublic\s+const\s+(?:uint|int|ushort|short|ulong|long)\s+(\w+)\s*=\s*([^;]+);"
    )
    for path in files:
        text = strip_csharp_comments(path.read_text(encoding="utf-8-sig", errors="ignore"))
        for class_match in class_re.finditer(text):
            class_name = class_match.group(1)
            open_brace = text.find("{", class_match.end() - 1)
            close_brace = find_matching_brace(text, open_brace)
            if close_brace < 0:
                continue
            body = text[open_brace + 1 : close_brace]
            for match in const_re.finditer(body):
                name = match.group(1)
                expr = match.group(2)
                raw[f"{class_name}.{name}"] = expr
                raw.setdefault(name, expr)
    return resolve_constants(raw)


def parse_enum_sections(files: list[Path]) -> dict[str, int]:
    result: dict[str, int] = {}
    enum_re = re.compile(r"\benum\s+H8DataSectionId\s*(?::\s*\w+)?\s*\{(?P<body>.*?)\}", re.DOTALL)
    item_re = re.compile(r"\b(\w+)\s*=\s*([^,\n]+)")
    for path in files:
        text = strip_csharp_comments(path.read_text(encoding="utf-8-sig", errors="ignore"))
        match = enum_re.search(text)
        if not match:
            continue
        constants = {}
        for item in item_re.finditer(match.group("body")):
            result[item.group(1)] = eval_int_expr(item.group(2), constants)
        break
    return result


def parse_section_order(files: list[Path], enum_sections: dict[str, int]) -> list[str]:
    order_re = re.compile(
        r"SectionOrder\s*=\s*\{(?P<body>.*?)\};",
        re.DOTALL,
    )
    item_re = re.compile(r"H8DataSectionId\.(\w+)")
    for path in files:
        text = strip_csharp_comments(path.read_text(encoding="utf-8-sig", errors="ignore"))
        match = order_re.search(text)
        if not match:
            continue
        order = item_re.findall(match.group("body"))
        if order:
            return order
    return [name for name, _ in sorted(enum_sections.items(), key=lambda kv: kv[1])]


def parse_structs(files: list[Path], constants: dict[str, int], state: ValidationState | None = None) -> dict[str, StructLayout]:
    structs: dict[str, StructLayout] = {}
    struct_decl_re = re.compile(
        r"\b(?:(?:public|internal|private|protected|readonly|partial|unsafe|new)\s+)*struct\s+(?P<name>\w+)\b",
        re.DOTALL,
    )
    for path in files:
        text = strip_csharp_comments(path.read_text(encoding="utf-8-sig", errors="ignore"))
        pending_attributes: list[str] = []
        cursor = 0
        while cursor < len(text):
            cursor = skip_csharp_whitespace(text, cursor)
            if cursor >= len(text):
                break
            if text[cursor] == "[":
                attribute, end = read_balanced_span(text, cursor, "[", "]")
                if end < 0:
                    break
                pending_attributes.append(attribute)
                cursor = end
                continue
            if not (text[cursor].isalpha() or text[cursor] == "_"):
                pending_attributes = []
                cursor += 1
                continue
            open_brace = text.find("{", cursor)
            semicolon = text.find(";", cursor)
            if open_brace < 0 and semicolon < 0:
                break
            if open_brace >= 0 and (semicolon < 0 or open_brace < semicolon):
                declaration = text[cursor:open_brace]
                struct_match = struct_decl_re.search(declaration)
                struct_layout_attr = extract_attribute_call(pending_attributes, "StructLayout")
                pending_attributes = []
                if not struct_match or not struct_layout_attr or "LayoutKind.Explicit" not in struct_layout_attr:
                    if re.search(r"\benum\b", declaration):
                        close_brace = find_matching_brace(text, open_brace)
                        cursor = close_brace + 1 if close_brace >= 0 else open_brace + 1
                        continue
                    cursor = open_brace + 1
                    continue
                close_brace = find_matching_brace(text, open_brace)
                if close_brace < 0:
                    if state:
                        state.add_error("STRUCT_BRACE_ERROR", f"{struct_match.group('name')} body is not closed", str(path))
                    cursor = open_brace + 1
                    continue
                attribute = " ".join(struct_layout_attr.split())
                name = struct_match.group("name")
                body = text[open_brace + 1 : close_brace]
                cursor = close_brace + 1
            else:
                pending_attributes = []
                cursor = semicolon + 1
                continue
            size_expr = extract_named_argument(attribute, "Size")
            if not size_expr:
                if state:
                    state.add_error("STRUCT_SIZE_MISSING", f"{name} has explicit layout without Size", str(path))
                continue
            try:
                size = eval_int_expr(size_expr, constants)
            except Exception as exc:
                if state:
                    state.add_error("STRUCT_SIZE_UNRESOLVED", f"{name} size expression failed: {exc}", str(path))
                continue
            layout = StructLayout(name, size_expr, size, path, attribute)
            pack_expr = extract_named_argument(attribute, "Pack")
            if pack_expr:
                try:
                    pack_value = eval_int_expr(pack_expr, constants)
                except Exception:
                    pack_value = 0
                if pack_value == 1 and state:
                    state.add_error("PACK_1_FORBIDDEN", f"{name} uses Pack=1", str(path), layout=attribute)
            layout.fields.extend(parse_fieldoffset_fields(body, constants, path, name, state))
            structs[name] = layout
    return structs


def resolve_type_layout(type_name: str, structs: dict[str, StructLayout]) -> tuple[int, int, str] | None:
    base = normalize_type(type_name)
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
                resolved = resolve_type_layout(item.type_name, structs)
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
            if layout.size % largest_alignment != 0:
                state.add_error(
                    "STRUCT_SIZE_ALIGNMENT",
                    f"{name} size {layout.size} is not a multiple of largest field alignment {largest_alignment}",
                    str(layout.source_path),
                    layout=layout.describe(),
                )
        if not unresolved or not progressed:
            break
    for name in sorted(unresolved):
        state.add_error("STRUCT_UNRESOLVED_FIELD", f"{name} has unresolved field types", str(structs[name].source_path))


def parse_expected_record_sizes(
    files: list[Path],
    constants: dict[str, int],
    structs: dict[str, StructLayout],
) -> tuple[dict[str, int], dict[str, str]]:
    expected: dict[str, int] = {}
    section_structs: dict[str, str] = {}
    case_re = re.compile(r"case\s+H8DataSectionId\.(\w+)\s*:\s*return\s+([^;]+);")
    sizeof_re = re.compile(r"SizeOf\s*<\s*(\w+)\s*>\s*\(\s*\)")
    for path in files:
        text = strip_csharp_comments(path.read_text(encoding="utf-8-sig", errors="ignore"))
        for match in case_re.finditer(text):
            section_name = match.group(1)
            expr = match.group(2).strip()
            size_match = sizeof_re.search(expr)
            if size_match:
                struct_name = size_match.group(1)
                if struct_name in structs:
                    expected[section_name] = structs[struct_name].size
                    section_structs[section_name] = struct_name
                    continue
            cleaned = re.sub(r"UnsafeUtility\.SizeOf\s*<\s*\w+\s*>\s*\(\s*\)", "0", expr)
            try:
                expected[section_name] = eval_int_expr(cleaned, constants)
            except Exception:
                continue
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
    placeholder = SchemaModel({}, {}, [], {}, {}, {})
    schema_args = argparse.Namespace(**vars(args))
    schema_args.fail_fast = False
    state = ValidationState(schema_args, placeholder)
    if not files:
        roots = ", ".join(str(root) for root in cs_roots)
        state.add_error("CS_SOURCE_EMPTY", f"no C# source files found under: {roots}", roots)
    constants = parse_constants(files)
    if constants.get("SectionAlignmentBytes", DEFAULT_SECTION_ALIGNMENT) < DEFAULT_SECTION_ALIGNMENT:
        state.add_error(
            "SECTION_ALIGNMENT_CONTRACT",
            f"SectionAlignmentBytes {constants.get('SectionAlignmentBytes')} is below the 16-byte H8DM floor",
            "",
        )
    enum_sections = parse_enum_sections(files)
    section_order = parse_section_order(files, enum_sections)
    schema = SchemaModel(constants, enum_sections, section_order, {}, {}, {})
    state.schema = schema
    structs = parse_structs(files, constants, state)
    schema.structs = structs
    validate_struct_layouts(structs, state)
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
    return remediation, find_artifact_references(name)


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


def collect_text_artifact_symbols(lines: list[str]) -> dict[str, tuple[str, str]]:
    symbols: dict[str, tuple[str, str]] = {}
    for line in lines:
        stripped = line.strip()
        if stripped.startswith("//"):
            continue
        match = TEXT_ARTIFACT_SYMBOL_DECLARATION.search(stripped)
        if not match:
            continue
        value = match.group("value")
        if any(value.lower().endswith(suffix) for suffix in TEXT_RUNTIME_SUFFIXES):
            name = match.group("name")
            symbols[name.lower()] = (name, value)
    return symbols


def referenced_text_artifact_symbol(lower_line: str, symbols: dict[str, tuple[str, str]]) -> tuple[str, str] | None:
    for lower_name, symbol in symbols.items():
        if re.search(rf"\b{re.escape(lower_name)}\b", lower_line):
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
            text_symbols = collect_text_artifact_symbols(lines)
            for index, line in enumerate(lines, 1):
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
            if data_start_offset != section_table_offset + section_table_bytes or data_start_offset % section_alignment != 0:
                state.add_error("DATA_START", f"DataStartOffset {data_start_offset} is not table-end aligned", str(file_path), header_size + 20)
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
            unsupported_flags = directory_flags & ~rle_flag
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
        "agent_id": "SHINOBU_258",
        "task_count": 20,
        "standalone_python": True,
        "unity_runtime_dependency": False,
        "mmap_file_access": True,
        "checksum": "Unity.Collections.xxHash3.Hash64 pure-Python oracle over bytes[16..end)",
        "csharp_parser": {
            "mode": "lightweight syntax tree scanner: comments stripped, attribute blocks balanced, declaration bodies matched by braces",
            "struct": r"leading attribute blocks -> StructLayout(Attribute)(...LayoutKind.Explicit..., Size=...) -> modifiers struct Name { ... }",
            "field": r"field attribute blocks -> FieldOffset(Attribute)(X) -> modifiers type Name;",
            "combined_attributes": "supports [Serializable, StructLayout(...)] and [NonSerialized, FieldOffset(...)] lists",
            "section_order": "SectionOrder = { H8DataSectionId.* }",
            "record_size": "case H8DataSectionId.*: return ...;",
        },
        "compile_guard": "No dotnet or Unity build invoked by this tool.",
        "dear_lie": "Payload validation samples deterministic 5% by default after full header/table proof; --thorough validates all records.",
        "section_alignment_floor": "C# SectionAlignmentBytes must be >=16; effective section/file/data-start alignment never drops below 16 bytes.",
        "record_stride_gate": "Non-empty sections must declare a non-zero stride large enough to contain the parsed C# explicit-layout struct before hash extraction or sampling.",
        "rle_flag_contract": "RLE probe runs only when C# defines RleDirectoryFlag and the directory sets that bit; unknown directory flags fail as DIRECTORY_FLAGS_UNSUPPORTED.",
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
        "schema": "h8bin_validator.report.v1",
        "agent_id": "SHINOBU_258",
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
    path.write_text(json.dumps(report, indent=2, sort_keys=True), encoding="utf-8")


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
    warning = " PERF_WARN" if report["elapsed_seconds"] > 15.0 else ""
    stamp = time.strftime("%Y-%m-%d")
    line = (
        f"{stamp} SHINOBU_258 status={report['status']} "
        f"mb={report['mb_processed']} files={report['files_checked']} "
        f"structs={report['structs_parsed']} seconds={report['elapsed_seconds']}{warning}\n"
    )
    with path.open("a", encoding="utf-8") as handle:
        handle.write(line)


def default_cs_roots() -> list[Path]:
    return [
        PROJECT_ROOT / "Assets/_Project/Scripts/Data/Monolith",
        PROJECT_ROOT / "Assets/_Project/Scripts/Editor/DataMonolith",
    ]


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Validate HECTON-8 .h8bin Data Monolith payloads")
    parser.add_argument("--target-dir", default="Assets/StreamingAssets", help="directory containing runtime .h8bin files")
    parser.add_argument(
        "--cs-source-dir",
        action="append",
        default=None,
        help="C# source root to parse; can be supplied multiple times",
    )
    parser.add_argument("--report-json", default="Docs/Reports/SHINOBU_258_h8bin_validation.json")
    parser.add_argument("--report-junit", default="")
    parser.add_argument("--metrics-log", default="Docs/Reports/CI_BINARY_VALIDATION.log")
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
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    cs_roots = [resolve_path(value) for value in args.cs_source_dir] if args.cs_source_dir else default_cs_roots()
    runtime_roots = (
        [resolve_path(value) for value in args.runtime_source_dir]
        if args.runtime_source_dir
        else default_runtime_source_roots()
    )
    target_dir = resolve_path(args.target_dir)
    schema = parse_csharp_schema(cs_roots, args)
    state = ValidationState(args, schema)
    for finding in getattr(args, "_schema_findings", []):
        state.findings.append(finding)
    try:
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
    report = build_report(state)
    write_json_report(resolve_path(args.report_json), report)
    if args.report_junit:
        write_junit_report(resolve_path(args.report_junit), report)
    append_metrics(resolve_path(args.metrics_log), report)
    print(
        f"h8bin_validator status={report['status']} files={report['files_checked']} "
        f"structs={report['structs_parsed']} mb={report['mb_processed']} seconds={report['elapsed_seconds']}"
    )
    if not state.ok:
        for finding in state.findings[:12]:
            if finding.severity == "ERROR":
                print(f"ERROR {finding.code}: {finding.message}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
