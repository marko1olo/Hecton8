#!/usr/bin/env python3
"""Validate AppliedContent production markdown release-set sources.

Evidence class: STATIC_SOURCE only. This tool does not prove native
localization, runtime placement, Unity import health, DataMonolith, or h8bin
readiness.
"""

from __future__ import annotations

import argparse
import fnmatch
import json
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

try:
    from AppliedLoreImporter import TARGET_LOCALES as IMPORTED_TARGET_LOCALES
except ModuleNotFoundError:
    from Tools.AppliedLoreImporter import TARGET_LOCALES as IMPORTED_TARGET_LOCALES


TARGET_LOCALES = tuple(IMPORTED_TARGET_LOCALES)
ENGLISH_LOCALE = "en_US"
PRODUCTION_SUFFIX = ".production.md"
RELEASE_READY_KEYS = (
    "canonical_importer_ready",
    "runtime_ready",
    "native_localization_ready",
    "data_monolith_ready",
    "h8bin_ready",
    "unity_placement_ready",
    "generated_page_ready",
    "publication_ready",
)
ANTI_AI_PATTERNS = (
    re.compile(pattern, re.IGNORECASE)
    for pattern in (
        r"\bthis entry explores\b",
        r"\bserves as a reminder\b",
        r"\ba testament to\b",
        r"\bmore than just\b",
        r"\bat its core\b",
        r"\bin a world where\b",
        r"\bboth beautiful and terrifying\b",
        r"\bnot just\b.+\bbut\b",
    )
)
READY_STATUS_TOKENS = {
    "native",
    "native_reviewed",
    "runtime_ready",
    "publication_ready",
    "published",
    "public_ready",
}
EXPECTED_DRAFT_STATUS = "draft_machine_or_llm"
PACKET_ID_RE = re.compile(r"^P\d+_[A-Z0-9_]+$")
RELEASE_ID_RE = re.compile(r"^(RS\d{3})_[A-Z0-9_]+$")
PACKET_ID_ROW_RE = re.compile(r"^\| Packet ID \| (?P<packet_id>[^|]+) \|$", re.MULTILINE)
LOCALE_ROW_RE = re.compile(r"^\| (?P<locale>[a-z]{2}_[A-Z]{2}) \| (?P<status>[^|]+?) \|", re.MULTILINE)


class GuardError(Exception):
    """Raised for invalid CLI setup, not content failures."""


@dataclass
class Counters:
    manifests: int = 0
    production_packets: int = 0
    failures: int = 0
    warnings: int = 0


@dataclass
class GuardResult:
    counters: Counters = field(default_factory=Counters)
    messages: list[str] = field(default_factory=list)

    def fail(self, message: str) -> None:
        self.counters.failures += 1
        self.messages.append(f"FAIL: {message}")

    def warn(self, message: str) -> None:
        self.counters.warnings += 1
        self.messages.append(f"WARN: {message}")


def parse_globs(value: str) -> tuple[str, ...]:
    return tuple(part.strip() for part in value.split(",") if part.strip())


def matches_any(value: str, patterns: tuple[str, ...]) -> bool:
    if not patterns:
        return True
    return any(fnmatch.fnmatchcase(value, pattern) for pattern in patterns)


def rel(root: Path, path: Path) -> str:
    try:
        return path.relative_to(root).as_posix()
    except ValueError:
        return path.as_posix()


def read_json(path: Path) -> dict[str, Any]:
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except UnicodeDecodeError as exc:
        raise GuardError(f"{path}: invalid UTF-8") from exc
    except json.JSONDecodeError as exc:
        raise GuardError(f"{path}: invalid JSON: {exc}") from exc
    if not isinstance(data, dict):
        raise GuardError(f"{path}: root JSON value must be an object")
    return data


def read_text(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8")
    except UnicodeDecodeError as exc:
        raise GuardError(f"{path}: invalid UTF-8") from exc


def manifest_id_from_file(path: Path) -> str:
    name = path.name
    if name.endswith("_manifest.json"):
        return name[: -len("_manifest.json")]
    if name.endswith(".json"):
        return name[: -len(".json")]
    return path.stem


def release_prefix(release_set_id: str) -> str:
    match = RELEASE_ID_RE.match(release_set_id)
    return match.group(1) if match else ""


def collect_manifest_records(root: Path) -> list[tuple[Path, dict[str, Any]]]:
    release_dir = root / "Docs" / "Lore" / "AppliedContent" / "release_sets"
    if not release_dir.exists():
        raise GuardError(f"missing release_sets directory: {release_dir}")

    records: list[tuple[Path, dict[str, Any]]] = []
    for path in sorted(release_dir.glob("*_manifest.json"), key=lambda item: item.name.lower()):
        records.append((path, read_json(path)))
    return records


def selected_records(
    records: list[tuple[Path, dict[str, Any]]],
    release_globs: tuple[str, ...],
) -> list[tuple[Path, dict[str, Any]]]:
    selected: list[tuple[Path, dict[str, Any]]] = []
    for path, data in records:
        release_set_id = str(data.get("release_set_id", "")).strip()
        candidates = (release_set_id, manifest_id_from_file(path), path.name)
        if any(matches_any(candidate, release_globs) for candidate in candidates):
            selected.append((path, data))
    return selected


def selected_production_sources(
    root: Path,
    manifest_data: dict[str, Any],
    packet_globs: tuple[str, ...],
) -> list[Path]:
    sources = manifest_data.get("packet_sources")
    if not isinstance(sources, list):
        return []

    selected: list[Path] = []
    for raw_source in sources:
        if not isinstance(raw_source, str):
            continue
        if not raw_source.endswith(PRODUCTION_SUFFIX):
            continue
        path = root / raw_source
        if not matches_any(path.name, packet_globs) and not matches_any(path.stem, packet_globs):
            continue
        selected.append(path)
    return selected


def validate_release_number_conflicts(
    result: GuardResult,
    all_records: list[tuple[Path, dict[str, Any]]],
    selected: list[tuple[Path, dict[str, Any]]],
    root: Path,
) -> None:
    selected_paths = {path for path, _data in selected}
    selected_prefixes = {
        release_prefix(str(data.get("release_set_id", "")).strip())
        for _path, data in selected
    }
    selected_prefixes.discard("")
    if not selected_prefixes:
        return

    prefix_to_records: dict[str, list[tuple[Path, str]]] = {}
    for path, data in all_records:
        release_set_id = str(data.get("release_set_id", "")).strip()
        prefix = release_prefix(release_set_id)
        if prefix in selected_prefixes:
            prefix_to_records.setdefault(prefix, []).append((path, release_set_id))

    for prefix, rows in sorted(prefix_to_records.items()):
        unique_ids = sorted({release_set_id for _path, release_set_id in rows})
        if len(unique_ids) <= 1:
            continue
        selected_in_conflict = [path for path, _release_set_id in rows if path in selected_paths]
        if not selected_in_conflict:
            continue
        details = ", ".join(f"{release_set_id}@{rel(root, path)}" for path, release_set_id in rows)
        result.fail(f"release number prefix {prefix} is reused by multiple release_set_id values: {details}")


def validate_manifest(
    result: GuardResult,
    root: Path,
    path: Path,
    data: dict[str, Any],
    packet_globs: tuple[str, ...],
    seen_release_ids: set[str],
    seen_packet_ids: dict[str, Path],
) -> None:
    result.counters.manifests += 1
    release_set_id = str(data.get("release_set_id", "")).strip()
    location = rel(root, path)

    if not release_set_id:
        result.fail(f"{location}: missing release_set_id")
    elif release_set_id in seen_release_ids:
        result.fail(f"{location}: duplicate selected release_set_id {release_set_id}")
    else:
        seen_release_ids.add(release_set_id)

    if release_set_id and manifest_id_from_file(path) not in {release_set_id, release_prefix(release_set_id), path.stem}:
        if not path.name.startswith(release_prefix(release_set_id)):
            result.warn(f"{location}: filename does not share release prefix with {release_set_id}")

    sources = data.get("packet_sources")
    if not isinstance(sources, list) or not sources:
        result.fail(f"{location}: packet_sources must be a non-empty list")
        return

    production_sources = selected_production_sources(root, data, packet_globs)
    if not production_sources:
        result.warn(f"{location}: no selected production packet sources")

    for key in RELEASE_READY_KEYS:
        if data.get(key) is not False:
            result.fail(f"{location}: {key} must be false for production source candidates")

    for source_path in production_sources:
        if not source_path.exists():
            result.fail(f"{location}: missing packet source {rel(root, source_path)}")
            continue
        validate_packet(result, root, source_path, seen_packet_ids)


def validate_packet(
    result: GuardResult,
    root: Path,
    path: Path,
    seen_packet_ids: dict[str, Path],
) -> None:
    result.counters.production_packets += 1
    location = rel(root, path)
    text = read_text(path)
    expected_packet_id = path.name[: -len(PRODUCTION_SUFFIX)]

    if not PACKET_ID_RE.match(expected_packet_id):
        result.fail(f"{location}: packet filename is not canonical P####_NAME.production.md")

    match = PACKET_ID_ROW_RE.search(text)
    if match is None:
        result.fail(f"{location}: missing '| Packet ID | ... |' metadata row")
        packet_id = expected_packet_id
    else:
        packet_id = match.group("packet_id").strip()
        if packet_id != expected_packet_id:
            result.fail(f"{location}: Packet ID row {packet_id} does not match filename {expected_packet_id}")

    previous = seen_packet_ids.get(packet_id)
    if previous is not None and previous != path:
        result.fail(f"{location}: duplicate selected packet_id {packet_id}; first seen at {rel(root, previous)}")
    else:
        seen_packet_ids[packet_id] = path

    if "## Locale Rows" not in text:
        result.fail(f"{location}: missing Locale Rows section")

    locale_rows = [(item.group("locale"), item.group("status").strip()) for item in LOCALE_ROW_RE.finditer(text)]
    locales = [locale for locale, _status in locale_rows]
    if len(locale_rows) != len(TARGET_LOCALES):
        result.fail(f"{location}: expected {len(TARGET_LOCALES)} locale rows, found {len(locale_rows)}")

    missing = [locale for locale in TARGET_LOCALES if locale not in locales]
    extra = [locale for locale in locales if locale not in TARGET_LOCALES]
    duplicated = sorted({locale for locale in locales if locales.count(locale) > 1})
    if missing:
        result.fail(f"{location}: missing locale rows: {', '.join(missing)}")
    if extra:
        result.fail(f"{location}: unexpected locale rows: {', '.join(extra)}")
    if duplicated:
        result.fail(f"{location}: duplicate locale rows: {', '.join(duplicated)}")

    statuses = {locale: status for locale, status in locale_rows}
    if statuses.get(ENGLISH_LOCALE) != "source_authority":
        result.fail(f"{location}: en_US status must be source_authority")
    for locale in TARGET_LOCALES:
        if locale == ENGLISH_LOCALE:
            continue
        status = statuses.get(locale, "")
        if status != EXPECTED_DRAFT_STATUS:
            result.fail(f"{location}: {locale} status must be {EXPECTED_DRAFT_STATUS}, found {status or 'MISSING'}")
        normalized = re.sub(r"[^a-z0-9]+", "_", status.lower()).strip("_")
        if normalized in READY_STATUS_TOKENS:
            result.fail(f"{location}: {locale} claims ready localization status {status}")

    lowered = text.lower()
    for pattern in ANTI_AI_PATTERNS:
        if pattern.search(lowered):
            result.fail(f"{location}: anti-AI prose pattern detected: {pattern.pattern}")

    source_boundary = text.find("## Source Brief")
    future_boundary = text.find("## Future Integration Notes")
    if source_boundary == -1:
        result.fail(f"{location}: missing Source Brief section")
    if future_boundary == -1:
        result.fail(f"{location}: missing Future Integration Notes section")
    if "Content status | source_complete_unimported" not in text:
        result.fail(f"{location}: metadata must keep Content status as source_complete_unimported")


def run_guard(root: Path, release_glob: str, packet_glob: str, json_output: bool) -> int:
    result = GuardResult()
    release_globs = parse_globs(release_glob)
    packet_globs = parse_globs(packet_glob)
    records = collect_manifest_records(root)
    selected = selected_records(records, release_globs)

    if not selected:
        raise GuardError(f"no release manifests selected by --release-glob {release_glob!r}")

    validate_release_number_conflicts(result, records, selected, root)

    seen_release_ids: set[str] = set()
    seen_packet_ids: dict[str, Path] = {}
    for path, data in selected:
        validate_manifest(result, root, path, data, packet_globs, seen_release_ids, seen_packet_ids)

    if json_output:
        print(
            json.dumps(
                {
                    "counts": vars(result.counters),
                    "messages": result.messages,
                },
                indent=2,
            )
        )
    else:
        print("AppliedLore production source guard")
        print(f"root={root.resolve()}")
        print(f"release_glob={release_glob}")
        print(f"packet_glob={packet_glob or '*'}")
        for message in result.messages:
            print(message)
        print("--- Summary ---")
        for key, value in vars(result.counters).items():
            print(f"{key}: {value}")
        print("FINAL: FAIL" if result.counters.failures else "FINAL: PASS")

    return 1 if result.counters.failures else 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=".", help="Repository root.")
    parser.add_argument(
        "--release-glob",
        required=True,
        help="Comma-separated release_set_id/file globs, e.g. RS165_*,RS166_*.",
    )
    parser.add_argument(
        "--packet-glob",
        default="",
        help="Optional comma-separated packet source filename/stem globs.",
    )
    parser.add_argument("--json", action="store_true", help="Print machine-readable JSON.")
    args = parser.parse_args()

    try:
        return run_guard(Path(args.root), args.release_glob, args.packet_glob, args.json)
    except GuardError as exc:
        print(f"AppliedLore production source guard FAILED: {exc}")
        return 1


if __name__ == "__main__":
    sys.exit(main())
