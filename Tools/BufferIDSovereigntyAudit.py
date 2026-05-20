#!/usr/bin/env python3
"""Static audit for GlobalDataVault BufferID aliasing hazards."""

from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable


REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_SOURCE_ROOT = REPO_ROOT / "Assets" / "_Project" / "Scripts"
DEFAULT_H8_MEMORY = DEFAULT_SOURCE_ROOT / "Core" / "Memory" / "H8Memory.cs"
DEFAULT_REPORT_PATH = REPO_ROOT / "Docs" / "AgentLogs" / "BufferIDSovereigntyAudit_HFI_AUDIT.md"
DEFAULT_JSON_PATH = REPO_ROOT / "Docs" / "AgentLogs" / "BufferIDSovereigntyAudit_HFI_AUDIT.json"
AUDIT_SCHEMA = "hecton8.bufferid_sovereignty_audit.v1"

BUFFER_ENUM_RE = re.compile(
    r"public\s+enum\s+BufferID\s*:\s*int\s*\{(?P<body>.*?)^\s*\}",
    re.DOTALL | re.MULTILINE,
)
ENUM_ENTRY_RE = re.compile(
    r"^\s*(?P<name>[A-Za-z_]\w*)\s*(?:=\s*(?P<value>-?(?:0x[0-9A-Fa-f_]+|\d[\d_]*)))?\s*,?"
)
BUFFER_CAST_RE = re.compile(
    r"\(\s*BufferID\s*\)\s*(?P<value>-?(?:0x[0-9A-Fa-f_]+|\d[\d_]*))(?:[uUlL]*)?"
)

SKIP_DIR_NAMES = {
    ".git",
    ".vs",
    "__pycache__",
    "bin",
    "obj",
    "Library",
    "Temp",
}


@dataclass(frozen=True)
class BufferEntry:
    name: str
    value: int
    line: int


@dataclass(frozen=True)
class CastFinding:
    path: str
    line: int
    value: int
    enum_names: tuple[str, ...]
    text: str


def normalize_path(path: Path, repo_root: Path = REPO_ROOT) -> str:
    try:
        return path.resolve().relative_to(repo_root.resolve()).as_posix()
    except ValueError:
        return path.as_posix()


def parse_int_literal(value: str) -> int:
    token = value.replace("_", "")
    if token.lower().startswith("-0x"):
        return -int(token[3:], 16)
    if token.lower().startswith("0x"):
        return int(token[2:], 16)
    return int(token, 10)


def strip_line_comment(line: str) -> str:
    return line.split("//", 1)[0]


def should_skip(path: Path) -> bool:
    return any(part in SKIP_DIR_NAMES for part in path.parts)


def parse_buffer_enum(path: Path) -> list[BufferEntry]:
    text = path.read_text(encoding="utf-8", errors="ignore")
    match = BUFFER_ENUM_RE.search(text)
    if not match:
        raise ValueError(f"BufferID enum not found in {path}")

    body_start_line = text[: match.start("body")].count("\n") + 1
    entries: list[BufferEntry] = []
    current = -1
    for offset, raw_line in enumerate(match.group("body").splitlines(), 0):
        line = strip_line_comment(raw_line).strip()
        if not line or line.startswith("["):
            continue

        entry_match = ENUM_ENTRY_RE.match(line)
        if not entry_match:
            continue

        name = entry_match.group("name")
        literal = entry_match.group("value")
        if literal is not None:
            current = parse_int_literal(literal)
        else:
            current += 1

        entries.append(BufferEntry(name=name, value=current, line=body_start_line + offset))

    return entries


def group_duplicates(entries: Iterable[BufferEntry]) -> dict[int, list[BufferEntry]]:
    by_value: dict[int, list[BufferEntry]] = {}
    for entry in entries:
        by_value.setdefault(entry.value, []).append(entry)
    return {value: items for value, items in by_value.items() if len(items) > 1}


def scan_buffer_casts(
    source_root: Path,
    enum_by_value: dict[int, tuple[str, ...]],
    h8_memory_path: Path,
) -> list[CastFinding]:
    findings: list[CastFinding] = []
    for path in sorted(source_root.rglob("*.cs")):
        relative_scan_path = path.relative_to(source_root)
        if should_skip(relative_scan_path):
            continue
        if path.resolve() == h8_memory_path.resolve():
            continue

        lines = path.read_text(encoding="utf-8", errors="ignore").splitlines()
        for line_number, line in enumerate(lines, 1):
            code = strip_line_comment(line)
            for match in BUFFER_CAST_RE.finditer(code):
                value = parse_int_literal(match.group("value"))
                findings.append(
                    CastFinding(
                        path=normalize_path(path),
                        line=line_number,
                        value=value,
                        enum_names=enum_by_value.get(value, ()),
                        text=line.strip(),
                    )
                )
    return findings


def build_payload(entries: list[BufferEntry], casts: list[CastFinding], h8_memory_path: Path) -> dict[str, object]:
    duplicates = group_duplicates(entries)
    enum_by_value = {}
    for entry in entries:
        enum_by_value.setdefault(entry.value, []).append(entry.name)

    return {
        "schema": AUDIT_SCHEMA,
        "bufferEnumPath": normalize_path(h8_memory_path),
        "bufferIdCount": len(entries),
        "duplicateValueCount": len(duplicates),
        "duplicateEntries": [
            {
                "value": value,
                "entries": [
                    {"name": item.name, "line": item.line}
                    for item in items
                ],
            }
            for value, items in sorted(duplicates.items())
        ],
        "localNumericCastCount": len(casts),
        "localNumericCastFileCount": len({finding.path for finding in casts}),
        "localNumericCasts": [
            {
                "path": finding.path,
                "line": finding.line,
                "value": finding.value,
                "enumNames": list(finding.enum_names),
                "text": finding.text,
            }
            for finding in casts
        ],
    }


def write_json(path: Path, payload: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def write_markdown(path: Path, payload: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    lines: list[str] = [
        "# BufferID Sovereignty Audit",
        "",
        "Evidence class: STATIC_SOURCE. No Unity import, compile, Play Mode, profiler, or player build was executed.",
        "",
        f"- Schema: `{payload['schema']}`",
        f"- Buffer enum: `{payload['bufferEnumPath']}`",
        f"- BufferID entries: `{payload['bufferIdCount']}`",
        f"- Duplicate numeric values: `{payload['duplicateValueCount']}`",
        f"- Local numeric casts outside `H8Memory.cs`: `{payload['localNumericCastCount']}`",
        f"- Local numeric cast files: `{payload['localNumericCastFileCount']}`",
        "",
        "## Duplicate Values",
        "",
    ]

    duplicate_entries = payload["duplicateEntries"]
    if isinstance(duplicate_entries, list) and duplicate_entries:
        lines.append("| Value | Entries |")
        lines.append("|---:|---|")
        for item in duplicate_entries:
            if not isinstance(item, dict):
                continue
            entries = item.get("entries", [])
            entry_text = ", ".join(
                f"{entry.get('name')}@{entry.get('line')}"
                for entry in entries
                if isinstance(entry, dict)
            )
            lines.append(f"| {item.get('value')} | {entry_text} |")
    else:
        lines.append("No duplicate numeric `BufferID` values found.")

    lines.extend(["", "## Local Numeric Casts", ""])
    casts = payload["localNumericCasts"]
    if isinstance(casts, list) and casts:
        lines.append("| Path | Line | Value | Existing enum names |")
        lines.append("|---|---:|---:|---|")
        for item in casts[:300]:
            if not isinstance(item, dict):
                continue
            enum_names = item.get("enumNames") or []
            enum_text = ", ".join(enum_names) if isinstance(enum_names, list) and enum_names else "-"
            lines.append(f"| `{item.get('path')}` | {item.get('line')} | {item.get('value')} | {enum_text} |")
        if len(casts) > 300:
            lines.append(f"| ... | ... | ... | truncated; see JSON for {len(casts) - 300} more |")
    else:
        lines.append("No local numeric `(BufferID)N` casts found outside `H8Memory.cs`.")

    lines.extend(
        [
            "",
            "## Interpretation",
            "",
            "- Duplicate numeric values are hard blockers for DataVault sovereignty until the owning systems are reconciled.",
            "- Local numeric casts are migration debt. They may be intentional temporary reservations, but every retained cast needs owner/range/lifetime documentation or promotion into the central enum/ledger.",
            "- This audit does not auto-renumber. Renumbering without route-card and owner coordination can corrupt existing save/runtime assumptions.",
            "",
        ]
    )
    path.write_text("\n".join(lines), encoding="utf-8")


def run(args: argparse.Namespace) -> int:
    h8_memory_path = Path(args.h8_memory)
    source_root = Path(args.source_root)

    entries = parse_buffer_enum(h8_memory_path)
    enum_by_value: dict[int, list[str]] = {}
    for entry in entries:
        enum_by_value.setdefault(entry.value, []).append(entry.name)

    casts = scan_buffer_casts(
        source_root=source_root,
        enum_by_value={value: tuple(names) for value, names in enum_by_value.items()},
        h8_memory_path=h8_memory_path,
    )
    payload = build_payload(entries, casts, h8_memory_path)

    if args.json_path:
        write_json(Path(args.json_path), payload)
    if args.report_path:
        write_markdown(Path(args.report_path), payload)

    duplicate_count = int(payload["duplicateValueCount"])
    cast_count = int(payload["localNumericCastCount"])
    print(
        "BufferID sovereignty audit: "
        f"duplicates={duplicate_count}, localCasts={cast_count}, "
        f"castFiles={payload['localNumericCastFileCount']}"
    )

    if args.fail_on_duplicates and duplicate_count > 0:
        return 1
    if args.fail_on_local_casts and cast_count > 0:
        return 1
    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", default=str(DEFAULT_SOURCE_ROOT))
    parser.add_argument("--h8-memory", default=str(DEFAULT_H8_MEMORY))
    parser.add_argument("--report-path", default=str(DEFAULT_REPORT_PATH))
    parser.add_argument("--json-path", default=str(DEFAULT_JSON_PATH))
    parser.add_argument("--fail-on-duplicates", action="store_true")
    parser.add_argument("--fail-on-local-casts", action="store_true")
    return parser


def main() -> int:
    return run(build_parser().parse_args())


if __name__ == "__main__":
    sys.exit(main())
