#!/usr/bin/env python3
"""Shared static verification helpers for HECTON-8 offline data artifacts."""

from __future__ import annotations

import ast
import json
import re
import struct
from pathlib import Path
from typing import Any, Iterable


ROOT = Path(__file__).resolve().parents[1]
ALIGNMENT_BYTES = 16
STRUCT_FORMAT_RE = re.compile(
    r"\bstruct\.(?:pack|unpack|pack_into|unpack_from|calcsize|Struct)\(\s*(?:[rubfRUBF]*)(['\"])([^'\"]+)\1"
)
MULTIBYTE_FORMAT_CHARS = set("hHiIlLqQefd")
EXTERNAL_CONTAINER_TOKENS = ("PNG", "png", "IHDR", "IDAT", "IEND", "chunk", "crc", "zlib")


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
    return len(re.findall(r"^\| (?:[1-9]|[1-7][0-9]|8[0-5]) \|", atlas, flags=re.MULTILINE))


def assert_python_syntax(relative: str | Path) -> None:
    source = path(relative).read_text(encoding="utf-8")
    ast.parse(source, filename=str(relative))


def unpack_magic(relative: str | Path, fmt: str = "<4s") -> bytes:
    data = path(relative).read_bytes()
    require(len(data) >= struct.calcsize(fmt), f"{relative} too small for {fmt}")
    return struct.unpack_from(fmt, data, 0)[0]
