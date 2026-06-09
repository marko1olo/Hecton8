#!/usr/bin/env python3
"""Shared static verification helpers for HECTON-8 offline data artifacts."""

from __future__ import annotations

import ast
import hashlib
import json
import re
import struct
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Any, Iterable


class SafeTreeBuilder(ET.TreeBuilder):
    """A safe XML TreeBuilder that disables DTDs/Entities to prevent XXE attacks."""
    def doctype(self, name, pubid, system):
        raise ET.ParseError("DTDs and External Entities are not allowed")

def parse_xml_safe(source):
    """Safely parse XML by preventing DTDs and External Entities."""
    parser = ET.XMLParser(target=SafeTreeBuilder())
    return ET.parse(source, parser=parser)

def fromstring_xml_safe(text):
    """Safely parse XML string by preventing DTDs and External Entities."""
    parser = ET.XMLParser(target=SafeTreeBuilder())
    return ET.fromstring(text, parser=parser)

ROOT = Path(__file__).resolve().parents[1]
ALIGNMENT_BYTES = 16
BABEL_H8BD_RELATIVE = "Assets/_Project/Data/Localization/Babel_Dictionary.h8bin"
BABEL_H8BD_MANIFEST_RELATIVE = "Assets/_Project/Data/Localization/Babel_Dictionary.manifest.json"
H8BD_LANGUAGE_RECORD_BYTES = 32
H8BD_ENTRY_RECORD_BYTES = 32
H8BD_LAYER_NAMES = ("core", "world", "narrative")
STRUCT_FORMAT_RE = re.compile(
    r"\bstruct\.(?:pack|unpack|pack_into|unpack_from|calcsize|Struct)\(\s*(?:[rubfRUBF]*)(['\"])([^'\"]+)\1"
)
MULTIBYTE_FORMAT_CHARS = set("hHiIlLqQefd")
EXTERNAL_CONTAINER_TOKENS = (
    "PNG",
    "png",
    "IHDR",
    "IDAT",
    "IEND",
    "chunk",
    "crc",
    "zlib",
    "jpeg",
    "JPEG",
    "JPG",
    "marker",
    "SOF",
    "psd",
    "PSD",
    "8BPS",
)


def path(relative: str | Path) -> Path:
    return ROOT / relative


def read_json(relative: str | Path) -> Any:
    return json.loads(path(relative).read_text(encoding="utf-8-sig"))


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def file_size(relative: str | Path) -> int:
    p = path(relative)
    require(p.is_file(), f"missing file: {p}")
    return p.stat().st_size


def require_aligned(relative: str | Path, alignment: int = ALIGNMENT_BYTES) -> int:
    size = file_size(relative)
    require(size % alignment == 0, f"{relative} is not {alignment}-byte aligned: {size}")
    return size


def iter_binary_files(roots: Iterable[str] = ("Data", "Assets/_Project/Data", "Docs/AgentLogs")) -> list[Path]:
    files: list[Path] = []
    for root in roots:
        base = path(root)
        if not base.exists():
            continue
        for candidate in sorted(base.rglob("*")):
            if candidate.is_file() and candidate.suffix.lower() in {".bin", ".h8bin"}:
                files.append(candidate)
    return files


def binary_alignment_summary() -> tuple[int, list[str]]:
    failures: list[str] = []
    files = iter_binary_files()
    for candidate in files:
        if candidate.stat().st_size % ALIGNMENT_BYTES != 0:
            failures.append(candidate.relative_to(ROOT).as_posix())
    return len(files), failures


def fnv1a32_ascii_lower(text: str) -> int:
    value = 0x811C9DC5
    for raw in text.encode("ascii"):
        byte = raw + 32 if 65 <= raw <= 90 else raw
        value ^= byte
        value = (value * 0x01000193) & 0xFFFFFFFF
    return value


def no_duplicate_hashes(labels: Iterable[str]) -> int:
    seen: dict[int, str] = {}
    count = 0
    for label in labels:
        count += 1
        h = fnv1a32_ascii_lower(label)
        owner = seen.get(h)
        require(owner is None or owner == label, f"FNV collision: {owner} vs {label} = 0x{h:08X}")
        seen[h] = label
    return count


def scan_struct_endianness() -> tuple[int, list[str]]:
    failures: list[str] = []
    count = 0
    for candidate in sorted(path("Tools").rglob("*.py")):
        text = candidate.read_text(encoding="utf-8", errors="ignore")
        lines = text.splitlines()
        for index, line in enumerate(lines, 1):
            context = "\n".join(lines[max(0, index - 8) : min(len(lines), index + 3)])
            for match in STRUCT_FORMAT_RE.finditer(line):
                fmt = match.group(2)
                count += 1
                if fmt.startswith("<"):
                    continue
                if fmt.startswith(">") and any(token in context or token in candidate.as_posix() for token in EXTERNAL_CONTAINER_TOKENS):
                    continue
                if fmt.startswith((">", "!")):
                    failures.append(f"{candidate.relative_to(ROOT).as_posix()}:{index}:{fmt}")
                    continue
                if fmt.startswith("@") or any(ch in MULTIBYTE_FORMAT_CHARS for ch in fmt):
                    failures.append(f"{candidate.relative_to(ROOT).as_posix()}:{index}:{fmt}")
    return count, failures


def count_atlas_domains() -> int:
    atlas = path("Docs/PROJECT_ATLAS.md").read_text(encoding="utf-8-sig", errors="ignore")
    marker = "### 85 Identified Domains"
    marker_index = atlas.find(marker)
    if marker_index < 0:
        return 0
    domain_section = atlas[marker_index:]
    next_section = domain_section.find("\n### ", len(marker))
    if next_section >= 0:
        domain_section = domain_section[:next_section]
    return len(re.findall(r"^\| (?:[1-9]|[1-7][0-9]|8[0-5]) \|", domain_section, flags=re.MULTILINE))


def assert_python_syntax(relative: str | Path) -> None:
    source = path(relative).read_text(encoding="utf-8")
    ast.parse(source, filename=str(relative))


def unpack_magic(relative: str | Path, fmt: str = "<4s") -> bytes:
    data = path(relative).read_bytes()
    require(len(data) >= struct.calcsize(fmt), f"{relative} too small for {fmt}")
    return struct.unpack_from(fmt, data, 0)[0]


def read_h8bd_header(relative: str | Path = BABEL_H8BD_RELATIVE) -> dict[str, int | bytes]:
    data = path(relative).read_bytes()
    require(len(data) >= 64, f"{relative} too small for H8BD header")
    magic = data[:4]
    require(magic == b"H8BD", f"{relative} magic mismatch: {magic!r}")
    version, header_size = struct.unpack_from("<HH", data, 4)
    source_lane_count = struct.unpack_from("<I", data, 8)[0]
    language_count, language_record_bytes = struct.unpack_from("<HH", data, 12)
    entry_count = struct.unpack_from("<I", data, 16)[0]
    entry_record_bytes, alignment_bytes = struct.unpack_from("<HH", data, 20)
    language_table_offset = struct.unpack_from("<I", data, 24)[0]
    entry_table_offset = struct.unpack_from("<I", data, 28)[0]
    payload_offset = struct.unpack_from("<I", data, 32)[0]
    payload_bytes = struct.unpack_from("<I", data, 36)[0]
    payload_hash_low = struct.unpack_from("<I", data, 40)[0]
    payload_hash_high = struct.unpack_from("<I", data, 44)[0]
    word_count = struct.unpack_from("<I", data, 48)[0]
    blob_limit_bytes = struct.unpack_from("<I", data, 52)[0]

    require(version == 1, f"{relative} version mismatch: {version}")
    require(header_size == 64, f"{relative} header size mismatch: {header_size}")
    require(alignment_bytes == ALIGNMENT_BYTES, f"{relative} alignment field mismatch: {alignment_bytes}")
    require(language_record_bytes == 32, f"{relative} language record size mismatch: {language_record_bytes}")
    require(entry_record_bytes == 32, f"{relative} entry record size mismatch: {entry_record_bytes}")
    require(language_table_offset == header_size, f"{relative} language table offset mismatch: {language_table_offset}")
    require(entry_table_offset == language_table_offset + language_count * language_record_bytes,
            f"{relative} entry table offset mismatch: {entry_table_offset}")
    require(payload_offset == entry_table_offset + entry_count * entry_record_bytes,
            f"{relative} payload offset mismatch: {payload_offset}")
    require(payload_offset + payload_bytes == len(data),
            f"{relative} payload length mismatch: {payload_offset}+{payload_bytes}!={len(data)}")

    return {
        "magic": magic,
        "version": version,
        "headerSize": header_size,
        "sourceLaneCount": source_lane_count,
        "languageCount": language_count,
        "languageRecordBytes": language_record_bytes,
        "entryCount": entry_count,
        "entryRecordBytes": entry_record_bytes,
        "alignmentBytes": alignment_bytes,
        "languageTableOffset": language_table_offset,
        "entryTableOffset": entry_table_offset,
        "payloadOffset": payload_offset,
        "payloadBytes": payload_bytes,
        "payloadHashLow": payload_hash_low,
        "payloadHashHigh": payload_hash_high,
        "wordCount": word_count,
        "blobLimitBytes": blob_limit_bytes,
    }


def parse_hex_u32(value: Any, label: str) -> int:
    if isinstance(value, int):
        parsed = value
    else:
        text = str(value).strip()
        parsed = int(text, 16) if text.lower().startswith("0x") else int(text, 10)
    require(0 <= parsed <= 0xFFFFFFFF, f"{label} outside uint32 range: {value!r}")
    return parsed


def read_h8bd_tables(
    relative: str | Path = BABEL_H8BD_RELATIVE,
    header: dict[str, int | bytes] | None = None,
) -> dict[str, Any]:
    data = path(relative).read_bytes()
    header = header or read_h8bd_header(relative)
    language_count = int(header["languageCount"])
    entry_count = int(header["entryCount"])
    language_offset = int(header["languageTableOffset"])
    entry_offset = int(header["entryTableOffset"])
    payload_bytes = int(header["payloadBytes"])

    languages: list[dict[str, Any]] = []
    for index in range(language_count):
        offset = language_offset + index * H8BD_LANGUAGE_RECORD_BYTES
        require(offset + H8BD_LANGUAGE_RECORD_BYTES <= entry_offset,
                f"{relative} language record {index} exceeds language table")
        locale_hash = struct.unpack_from("<I", data, offset)[0]
        locale_bytes = data[offset + 4:offset + 12].split(b"\0", 1)[0]
        try:
            locale = locale_bytes.decode("ascii")
        except UnicodeDecodeError as exc:
            raise AssertionError(f"{relative} language record {index} has non-ASCII locale bytes") from exc
        packed_script_font = struct.unpack_from("<I", data, offset + 12)[0]
        entry_count_for_language = struct.unpack_from("<I", data, offset + 16)[0]
        payload_budget = struct.unpack_from("<I", data, offset + 20)[0]
        fallback_hash = struct.unpack_from("<I", data, offset + 24)[0]
        reserved = struct.unpack_from("<I", data, offset + 28)[0]
        require(reserved == 0, f"{relative} language record {index} reserved field is non-zero")
        languages.append({
            "locale": locale,
            "hash": locale_hash,
            "scriptFlags": packed_script_font >> 16,
            "fontWeightMask": packed_script_font & 0xFFFF,
            "entryCount": entry_count_for_language,
            "payloadBytesPadded": payload_budget,
            "fallbackHash": fallback_hash,
        })

    language_entry_counts = [0] * language_count
    layer_counts: dict[str, int] = {name: 0 for name in H8BD_LAYER_NAMES}
    source_hashes: set[int] = set()
    max_payload_extent = 0
    for index in range(entry_count):
        offset = entry_offset + index * H8BD_ENTRY_RECORD_BYTES
        require(offset + H8BD_ENTRY_RECORD_BYTES <= int(header["payloadOffset"]),
                f"{relative} entry record {index} exceeds entry table")
        _, _, payload_offset, payload_length, payload_padded_length, packed_layer_language, source_hash, reserved = struct.unpack_from(
            "<IIIIIIII",
            data,
            offset,
        )
        language_index = packed_layer_language & 0xFFFF
        layer_index = packed_layer_language >> 16
        require(reserved == 0, f"{relative} entry record {index} reserved field is non-zero")
        require(language_index < language_count,
                f"{relative} entry record {index} language index out of range: {language_index}")
        require(layer_index < len(H8BD_LAYER_NAMES),
                f"{relative} entry record {index} layer index out of range: {layer_index}")
        require(payload_padded_length >= payload_length,
                f"{relative} entry record {index} padded length is smaller than payload length")
        require(payload_padded_length % ALIGNMENT_BYTES == 0,
                f"{relative} entry record {index} padded length is not {ALIGNMENT_BYTES}-byte aligned")
        require(payload_offset % ALIGNMENT_BYTES == 0,
                f"{relative} entry record {index} payload offset is not {ALIGNMENT_BYTES}-byte aligned")
        require(payload_offset + payload_padded_length <= payload_bytes,
                f"{relative} entry record {index} payload range exceeds payload blob")
        language_entry_counts[language_index] += 1
        layer_counts[H8BD_LAYER_NAMES[layer_index]] += 1
        source_hashes.add(source_hash)
        max_payload_extent = max(max_payload_extent, payload_offset + payload_padded_length)

    require(max_payload_extent == payload_bytes,
            f"{relative} entry payload extent mismatch: {max_payload_extent}!={payload_bytes}")
    return {
        "languages": languages,
        "entryLanguageCounts": language_entry_counts,
        "entryLayerCounts": layer_counts,
        "sourceHashCount": len(source_hashes),
        "maxPayloadExtent": max_payload_extent,
    }


def audit_manifest_source_hashes(manifest: dict[str, Any]) -> tuple[list[str], list[str]]:
    missing: list[str] = []
    mismatched: list[str] = []
    hashes = manifest.get("sourceHashesSha256")
    require(isinstance(hashes, dict) and hashes, "Babel manifest missing sourceHashesSha256 ledger")
    for relative, expected in sorted(hashes.items()):
        source = path(relative)
        if not source.is_file():
            missing.append(str(relative))
            continue
        actual = hashlib.sha256(source.read_bytes()).hexdigest()
        if actual.lower() != str(expected).lower():
            mismatched.append(str(relative))
    return missing, mismatched


def verify_h8bd_manifest(hash_audit: bool = False) -> dict[str, Any]:
    manifest = read_json(BABEL_H8BD_MANIFEST_RELATIVE)
    header = read_h8bd_header(BABEL_H8BD_RELATIVE)
    tables = read_h8bd_tables(BABEL_H8BD_RELATIVE, header)
    size = require_aligned(BABEL_H8BD_RELATIVE)

    languages = manifest.get("languages")
    require(isinstance(languages, dict) and languages, "Babel manifest missing languages")
    manifest_payload_budget = 0
    language_entry_sum = 0
    for locale, info in languages.items():
        require(isinstance(info, dict), f"Babel manifest malformed language entry: {locale}")
        manifest_payload_budget += int(info.get("payloadBytesPadded", 0))
        language_entry_sum += int(info.get("entryCount", 0))

    require(size == int(manifest.get("blobBytes", -1)), "Babel blobBytes does not match binary size")
    require(header["entryCount"] == int(manifest.get("entryCount", -1)), "Babel entryCount mismatch")
    require(header["wordCount"] == int(manifest.get("wordCount", -1)), "Babel wordCount mismatch")
    require(header["blobLimitBytes"] == int(manifest.get("blobLimitBytes", -1)), "Babel blobLimitBytes mismatch")
    require(header["languageCount"] == len(languages), "Babel language count mismatch")
    require(header["entryCount"] == language_entry_sum, "Babel language entry count sum mismatch")
    require(len(tables["languages"]) == len(languages), "Babel binary language table count mismatch")
    for index, (locale, info) in enumerate(languages.items()):
        require(isinstance(info, dict), f"Babel manifest malformed language entry: {locale}")
        binary_language = tables["languages"][index]
        require(binary_language["locale"] == locale,
                f"Babel language record order mismatch at {index}: {binary_language['locale']} != {locale}")
        require(binary_language["hash"] == parse_hex_u32(info.get("hash"), f"Babel {locale} hash"),
                f"Babel language hash mismatch for {locale}")
        require(binary_language["entryCount"] == int(info.get("entryCount", -1)),
                f"Babel language entry count mismatch for {locale}")
        require(binary_language["payloadBytesPadded"] == int(info.get("payloadBytesPadded", -1)),
                f"Babel language payload budget mismatch for {locale}")
        require(binary_language["scriptFlags"] == int(info.get("scriptFlags", -1)),
                f"Babel language script flags mismatch for {locale}")
        require(binary_language["fontWeightMask"] == int(info.get("fontWeightMask", -1)),
                f"Babel language font weight mask mismatch for {locale}")
        require(tables["entryLanguageCounts"][index] == int(info.get("entryCount", -1)),
                f"Babel entry table language count mismatch for {locale}")

    manifest_layer_counts = manifest.get("layerCounts")
    require(isinstance(manifest_layer_counts, dict), "Babel manifest missing layerCounts")
    for layer_name in H8BD_LAYER_NAMES:
        require(tables["entryLayerCounts"][layer_name] == int(manifest_layer_counts.get(layer_name, -1)),
                f"Babel layer count mismatch for {layer_name}")

    missing: list[str] = []
    mismatched: list[str] = []
    if hash_audit:
        missing, mismatched = audit_manifest_source_hashes(manifest)
        if missing or mismatched:
            missing_sample = ", ".join(missing[:8]) if missing else "none"
            mismatched_sample = ", ".join(mismatched[:8]) if mismatched else "none"
            raise AssertionError(
                "Babel manifest source ledger drift: "
                f"missing={len(missing)} [{missing_sample}] "
                f"mismatched={len(mismatched)} [{mismatched_sample}]"
            )

    return {
        "manifest": manifest,
        "header": header,
        "size": size,
        "languageCount": len(languages),
        "manifestPayloadBudget": manifest_payload_budget,
        "tableProof": tables,
        "sourceCount": len(manifest.get("sources", [])),
        "sourceMissing": missing,
        "sourceMismatched": mismatched,
    }
