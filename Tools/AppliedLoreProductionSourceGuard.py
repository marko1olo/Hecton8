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
import tempfile
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
READY_CLAIM_RE = re.compile(
    r"(?<![a-z0-9])(?:"
    r"canonical_importer_ready|route_card_exported|exported_pages|generated_binding_targets|"
    r"generated_page_ready|runtime_ready|publication_ready|native_localization_ready|native_ready|"
    r"data_monolith_ready|h8bin_ready|unity_placement_ready"
    r")(?![a-z0-9])",
    re.IGNORECASE,
)
EXPECTED_DRAFT_STATUS = "draft_machine_or_llm"
PACKET_ID_RE = re.compile(r"^P\d+_[A-Z0-9_]+$")
RELEASE_ID_RE = re.compile(r"^(RS\d{3})_[A-Z0-9_]+$")
RELEASE_PREFIX_IN_NAME_RE = re.compile(r"^(RS\d{3})_")
PACKET_ID_ROW_RE = re.compile(r"^\| Packet ID \| (?P<packet_id>[^|]+) \|$", re.MULTILINE)
CONNECTED_PACKETS_ROW_RE = re.compile(r"^\| Connected packets \| (?P<packet_ids>[^|]+) \|$", re.MULTILINE)
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
        data = json.loads(path.read_text(encoding="utf-8-sig"))
    except UnicodeDecodeError as exc:
        raise GuardError(f"{path}: invalid UTF-8") from exc
    except json.JSONDecodeError as exc:
        raise GuardError(f"{path}: invalid JSON: {exc}") from exc
    if not isinstance(data, dict):
        raise GuardError(f"{path}: root JSON value must be an object")
    return data


def read_text(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8-sig")
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


def release_prefix_from_name(name: str) -> str:
    match = RELEASE_PREFIX_IN_NAME_RE.match(name)
    return match.group(1) if match else ""


def release_number(prefix: str) -> int | None:
    if not re.match(r"^RS\d{3}$", prefix):
        return None
    return int(prefix[2:])


def normalize_release_slug(value: str) -> str:
    slug = re.sub(r"[^A-Za-z0-9]+", "_", value.upper()).strip("_")
    slug = re.sub(r"_+", "_", slug)
    if not slug:
        return "UNTITLED_RELEASE"
    slug = re.sub(r"^RS\d{3}_?", "", slug)
    return slug or "UNTITLED_RELEASE"


def collect_manifest_records(root: Path) -> list[tuple[Path, dict[str, Any]]]:
    release_dir = root / "Docs" / "Lore" / "AppliedContent" / "release_sets"
    if not release_dir.exists():
        raise GuardError(f"missing release_sets directory: {release_dir}")

    records: list[tuple[Path, dict[str, Any]]] = []
    for path in sorted(release_dir.glob("*_manifest.json"), key=lambda item: item.name.lower()):
        records.append((path, read_json(path)))
    return records


def collect_used_release_numbers(records: list[tuple[Path, dict[str, Any]]]) -> set[int]:
    used: set[int] = set()
    for path, data in records:
        prefixes = {
            release_prefix(str(data.get("release_set_id", "")).strip()),
            release_prefix_from_name(path.name),
        }
        for prefix in prefixes:
            number = release_number(prefix)
            if number is not None:
                used.add(number)
    return used


def next_release_set_id(root: Path, slug: str, start_number: int) -> tuple[str, int, int]:
    if start_number < 0 or start_number > 999:
        raise GuardError("--start-release-number must be between 0 and 999")
    records = collect_manifest_records(root)
    used = collect_used_release_numbers(records)
    high_water = max((number for number in used if number >= start_number), default=start_number - 1)
    candidate = high_water + 1
    if candidate > 999:
        raise GuardError("no RS### release id remains after current high-water mark")
    return f"RS{candidate:03d}_{normalize_release_slug(slug)}", candidate, high_water


def packet_id_from_production_source(path: Path) -> str:
    return path.name[: -len(PRODUCTION_SUFFIX)]


def collect_known_packet_ids(root: Path) -> set[str]:
    known: set[str] = set()
    packet_dir = root / "Docs" / "Lore" / "AppliedContent" / "production_packets"
    if packet_dir.exists():
        for path in packet_dir.glob(f"*{PRODUCTION_SUFFIX}"):
            packet_id = packet_id_from_production_source(path)
            if PACKET_ID_RE.match(packet_id):
                known.add(packet_id)

    bundle_dir = root / "Docs" / "Lore" / "AppliedContent" / "packets"
    if bundle_dir.exists():
        for path in bundle_dir.glob("*.json"):
            if PACKET_ID_RE.match(path.stem):
                known.add(path.stem)
            try:
                data = json.loads(path.read_text(encoding="utf-8-sig"))
            except (UnicodeDecodeError, json.JSONDecodeError):
                continue
            if isinstance(data, dict):
                packets = data.get("packets")
                if isinstance(packets, list):
                    for packet in packets:
                        if not isinstance(packet, dict):
                            continue
                        packet_id = str(packet.get("packet_id", "")).strip()
                        if PACKET_ID_RE.match(packet_id):
                            known.add(packet_id)

    applied_root = root / "Docs" / "Lore" / "AppliedContent"
    if applied_root.exists():
        for path in applied_root.glob("*/*/*.md"):
            packet_id = path.stem
            if PACKET_ID_RE.match(packet_id):
                known.add(packet_id)

    return known


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


def validate_manifest_packet_source_ids(
    result: GuardResult,
    root: Path,
    path: Path,
    data: dict[str, Any],
    production_sources: list[Path],
    packet_globs: tuple[str, ...],
) -> None:
    location = rel(root, path)
    raw_packets = data.get("packets")
    if not isinstance(raw_packets, list) or not raw_packets:
        result.fail(f"{location}: packets must list production packet ids")
        return

    manifest_ids: list[str] = []
    for raw_packet_id in raw_packets:
        packet_id = str(raw_packet_id).strip()
        if not PACKET_ID_RE.match(packet_id):
            result.fail(f"{location}: manifest packet id is not canonical: {packet_id or 'EMPTY'}")
            continue
        manifest_ids.append(packet_id)

    duplicated_manifest_ids = sorted({packet_id for packet_id in manifest_ids if manifest_ids.count(packet_id) > 1})
    if duplicated_manifest_ids:
        result.fail(f"{location}: duplicate manifest packet ids: {', '.join(duplicated_manifest_ids)}")

    manifest_id_set = set(manifest_ids)
    source_ids = [packet_id_from_production_source(source_path) for source_path in production_sources]
    for source_id in source_ids:
        if source_id not in manifest_id_set:
            result.fail(f"{location}: production source {source_id} is not listed in manifest packets")

    if packet_globs:
        return

    source_id_set = set(source_ids)
    missing_sources = sorted(manifest_id_set.difference(source_id_set))
    if missing_sources:
        result.fail(f"{location}: manifest packet ids missing production sources: {', '.join(missing_sources)}")


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
    known_packet_ids: set[str],
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
        return

    validate_manifest_packet_source_ids(result, root, path, data, production_sources, packet_globs)

    for key in RELEASE_READY_KEYS:
        if data.get(key) is not False:
            result.fail(f"{location}: {key} must be false for production source candidates")

    status = data.get("status")
    if isinstance(status, str) and READY_CLAIM_RE.search(status):
        result.fail(f"{location}: status must not claim ready/exported runtime state: {status}")

    for source_path in production_sources:
        if not source_path.exists():
            result.fail(f"{location}: missing packet source {rel(root, source_path)}")
            continue
        validate_packet(result, root, source_path, seen_packet_ids, known_packet_ids)


def validate_packet(
    result: GuardResult,
    root: Path,
    path: Path,
    seen_packet_ids: dict[str, Path],
    known_packet_ids: set[str],
) -> None:
    result.counters.production_packets += 1
    location = rel(root, path)
    text = read_text(path)
    expected_packet_id = packet_id_from_production_source(path)

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

    connected_match = CONNECTED_PACKETS_ROW_RE.search(text)
    if connected_match is not None:
        connected_packet_ids = [
            packet_id.strip()
            for packet_id in re.split(r"[;,]", connected_match.group("packet_ids"))
            if packet_id.strip()
        ]
        for connected_packet_id in connected_packet_ids:
            if not PACKET_ID_RE.match(connected_packet_id):
                result.fail(f"{location}: connected packet id is not canonical: {connected_packet_id}")
            elif connected_packet_id not in known_packet_ids:
                result.fail(f"{location}: connected packet id is not present in AppliedContent sources: {connected_packet_id}")

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


def run_guard(root: Path, release_glob: str, packet_glob: str, json_output: bool, quiet: bool = False) -> int:
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
    known_packet_ids = collect_known_packet_ids(root)
    for path, data in selected:
        validate_manifest(result, root, path, data, packet_globs, seen_release_ids, seen_packet_ids, known_packet_ids)

    if quiet:
        pass
    elif json_output:
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


def write_json(path: Path, data: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")


def write_valid_production_packet(path: Path, packet_id: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    rows: list[str] = []
    for locale in TARGET_LOCALES:
        status = "source_authority" if locale == ENGLISH_LOCALE else EXPECTED_DRAFT_STATUS
        rows.append(f"| {locale} | {status} | {packet_id} text for {locale}. |")
    path.write_text(
        "\n".join(
            [
                f"# {packet_id}",
                "",
                "| Packet ID | " + packet_id + " |",
                "| Content status | source_complete_unimported |",
                "",
                "## Source Brief",
                "A source-limited production packet for guard self-test.",
                "",
                "## Locale Rows",
                *rows,
                "",
                "## Future Integration Notes",
                "Importer, page export, binding, native localization, and runtime placement remain separate gates.",
            ]
        )
        + "\n",
        encoding="utf-8",
    )


def manifest(release_set_id: str, packet_sources: list[str], *, ready: bool) -> dict[str, Any]:
    data: dict[str, Any] = {
        "schema": "H8.APPLIED_LORE_RELEASE_SET.V0",
        "release_set_id": release_set_id,
        "status": "self_test_static_source",
        "evidence_class": "STATIC_SOURCE",
        "packets": ["P999_GUARD_SELF_TEST"],
        "packet_sources": packet_sources,
    }
    for key in RELEASE_READY_KEYS:
        data[key] = ready
    return data


def run_self_test() -> int:
    with tempfile.TemporaryDirectory(prefix="h8_applied_lore_guard_") as raw:
        root = Path(raw)
        release_dir = root / "Docs" / "Lore" / "AppliedContent" / "release_sets"
        packets_dir = root / "Docs" / "Lore" / "AppliedContent" / "packets"
        production_dir = root / "Docs" / "Lore" / "AppliedContent" / "production_packets"

        write_json(
            release_dir / "RS998_JSON_READY_manifest.json",
            manifest(
                "RS998_JSON_READY",
                ["Docs/Lore/AppliedContent/packets/RS998_JSON_READY.packets.json"],
                ready=True,
            ),
        )
        write_json(packets_dir / "RS998_JSON_READY.packets.json", {"packets": []})
        if run_guard(root, "RS998_*", "", json_output=False, quiet=True) != 0:
            print("APPLIED_LORE_PRODUCTION_SOURCE_GUARD_SELFTEST=FAIL")
            print("- packet-JSON release without .production.md source failed")
            return 1

        packet_path = production_dir / "P999_GUARD_SELF_TEST.production.md"
        write_valid_production_packet(packet_path, "P999_GUARD_SELF_TEST")
        packet_source = "Docs/Lore/AppliedContent/production_packets/P999_GUARD_SELF_TEST.production.md"

        write_json(
            release_dir / "RS999_BAD_READY_manifest.json",
            manifest("RS999_BAD_READY", [packet_source], ready=True),
        )
        if run_guard(root, "RS999_BAD_READY", "", json_output=False, quiet=True) == 0:
            print("APPLIED_LORE_PRODUCTION_SOURCE_GUARD_SELFTEST=FAIL")
            print("- production source ready-claim fixture passed")
            return 1

        write_json(
            release_dir / "RS997_GOOD_SOURCE_manifest.json",
            manifest("RS997_GOOD_SOURCE", [packet_source], ready=False),
        )
        if run_guard(root, "RS997_GOOD_SOURCE", "", json_output=False, quiet=True) != 0:
            print("APPLIED_LORE_PRODUCTION_SOURCE_GUARD_SELFTEST=FAIL")
            print("- valid production source fixture failed")
            return 1

    print("APPLIED_LORE_PRODUCTION_SOURCE_GUARD_SELFTEST=PASS")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=".", help="Repository root.")
    parser.add_argument(
        "--release-glob",
        default="",
        help="Comma-separated release_set_id/file globs, e.g. RS165_*,RS166_*.",
    )
    parser.add_argument(
        "--packet-glob",
        default="",
        help="Optional comma-separated packet source filename/stem globs.",
    )
    parser.add_argument("--json", action="store_true", help="Print machine-readable JSON.")
    parser.add_argument("--self-test", action="store_true", help="run internal positive and negative fixtures")
    parser.add_argument(
        "--next-release-id",
        default="",
        help="Suggest next high-water RS###_<SLUG> release_set_id without writing files.",
    )
    parser.add_argument(
        "--start-release-number",
        type=int,
        default=0,
        help="Lower bound for --next-release-id allocation.",
    )
    args = parser.parse_args()

    try:
        if args.next_release_id:
            release_set_id, number, high_water = next_release_set_id(
                Path(args.root),
                args.next_release_id,
                args.start_release_number,
            )
            if args.json:
                print(
                    json.dumps(
                        {
                            "release_set_id": release_set_id,
                            "release_number": number,
                            "high_water_release_number": high_water,
                        },
                        indent=2,
                    )
                )
            else:
                print(release_set_id)
            return 0
        if args.self_test:
            return run_self_test()
        if not args.release_glob:
            raise GuardError("--release-glob is required unless --self-test or --next-release-id is used")
        return run_guard(Path(args.root), args.release_glob, args.packet_glob, args.json)
    except GuardError as exc:
        print(f"AppliedLore production source guard FAILED: {exc}")
        return 1


if __name__ == "__main__":
    sys.exit(main())
