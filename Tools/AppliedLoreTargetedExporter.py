#!/usr/bin/env python3
"""Targeted AppliedContent page export and validation.

This tool intentionally scans packet JSON files directly instead of release-set
manifests. Release-set manifests can contain production markdown sources or
stale authoring references; targeted content fixes need a narrow packet route
that still validates publication output and the surface index.
"""

from __future__ import annotations

import argparse
import csv
import io
import json
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from AppliedLoreImporter import (
    CSV_HEADERS,
    DRAFT_LOCALIZATION_PREFIX,
    LOCALIZED_TEXT_FIELDS,
    ROW_FLAG_DRAFT_LOCALIZATION,
    TARGET_LOCALES,
    localized_row_flags,
    packet_rows,
    render_csv,
    sanitize_localized_text,
)
from AppliedLorePageExporter import (
    PUBLICATION_INDEX_HEADERS,
    SURFACES,
    is_surface_enabled,
    publication_surface_rows,
    remove_generated_disabled_page,
    render_index,
    render_localization_status_index,
    render_page,
    safe_text,
)
from AppliedLoreTextIntegrity import find_text_integrity_errors


def configure_csv_field_limit() -> None:
    """Allow validation to read generated longform publication rows."""

    limit = sys.maxsize
    while True:
        try:
            csv.field_size_limit(limit)
            return
        except OverflowError:
            limit //= 10


configure_csv_field_limit()


PACKET_SOURCE_GLOB = "*.json"
REQUIRED_LOCALIZED_FIELDS = (
    "title",
    "scanner",
    "terminal",
    "audio",
    "in_game_wiki",
    "external_site",
)
OPTIONAL_LOCALIZED_FIELDS = ("field_note", "external_site_article")
RTL_LOCALES = {"ar_SA", "he_IL"}


class AppliedLoreTargetedError(Exception):
    """Raised for hard validation or configuration failures."""


@dataclass
class ExportStats:
    source_packets: int = 0
    target_packets: int = 0
    pages_written: int = 0
    pages_unchanged: int = 0
    disabled_pages_removed: int = 0
    locale_indexes_written: int = 0
    locale_indexes_unchanged: int = 0
    surface_rows_targeted: int = 0
    surface_index_written: bool = False
    status_index_written: bool = False
    refreshed_indexes: bool = False
    baked_csv_rows_targeted: int = 0
    baked_csv_written: bool = False


def applied_content_base(root: Path) -> Path:
    return root / "Docs" / "Lore" / "AppliedContent"


def read_json_object(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as handle:
        data = json.load(handle)
    if not isinstance(data, dict):
        raise AppliedLoreTargetedError(f"JSON root must be object: {path}")
    return data


def add_packet(packets_by_id: dict[str, dict[str, Any]], packet: dict[str, Any], source_path: Path) -> None:
    packet_id = safe_text(packet.get("packet_id"))
    if not packet_id:
        raise AppliedLoreTargetedError(f"Packet without packet_id: {source_path}")
    if packet_id in packets_by_id:
        previous = packets_by_id[packet_id].get("_source_path", "")
        raise AppliedLoreTargetedError(f"Duplicate packet_id {packet_id}: {source_path} and {previous}")

    packet_copy = dict(packet)
    packet_copy["_source_path"] = str(source_path.resolve())
    packets_by_id[packet_id] = packet_copy


def iter_packet_source_paths(base: Path, explicit_paths: tuple[Path, ...] = ()) -> list[Path]:
    if explicit_paths:
        return sorted((path.resolve() for path in explicit_paths), key=lambda item: str(item).lower())

    packet_dir = base / "packets"
    if not packet_dir.exists():
        raise AppliedLoreTargetedError(f"Missing AppliedContent packet directory: {packet_dir}")
    return sorted(packet_dir.glob(PACKET_SOURCE_GLOB), key=lambda item: item.name.lower())


def release_set_by_packet_source(base: Path) -> dict[Path, dict[str, object]]:
    release_dir = base / "release_sets"
    mapped: dict[Path, dict[str, str]] = {}
    if not release_dir.exists():
        return mapped

    for manifest_path in sorted(release_dir.glob("*_manifest.json"), key=lambda item: item.name.lower()):
        data = read_json_object(manifest_path)
        release_set_id = safe_text(data.get("release_set_id"))
        if not release_set_id:
            continue
        manifest_status = safe_text(data.get("status"))
        packet_sources = data.get("packet_sources")
        if not isinstance(packet_sources, list):
            continue
        for raw_source in packet_sources:
            if not isinstance(raw_source, str):
                continue
            source_path = Path(raw_source)
            if not source_path.is_absolute():
                source_path = base.parent.parent.parent / source_path
            resolved_source = source_path.resolve()
            existing = mapped.get(resolved_source)
            canonical_importer_ready = data.get("canonical_importer_ready")
            if existing is not None and existing.get("canonical_importer_ready") is not False:
                continue
            if existing is not None and canonical_importer_ready is False:
                continue

            mapped[resolved_source] = {
                "release_set_id": release_set_id,
                "manifest_status": manifest_status,
                "canonical_importer_ready": canonical_importer_ready,
            }

    return mapped


def load_packet_sources(base: Path, explicit_paths: tuple[Path, ...] = ()) -> list[dict[str, Any]]:
    packets_by_id: dict[str, dict[str, Any]] = {}
    release_sets_by_source = release_set_by_packet_source(base)

    for source_path in iter_packet_source_paths(base, explicit_paths):
        data = read_json_object(source_path)
        source_metadata = release_sets_by_source.get(source_path.resolve(), {})
        if not explicit_paths and source_metadata.get("canonical_importer_ready") is False:
            continue

        source_release_set = safe_text(source_metadata.get("release_set_id"))
        manifest_status = safe_text(source_metadata.get("manifest_status"))
        bundle_release_set = safe_text(data.get("release_set_id")) or source_release_set
        bundle_status = safe_text(data.get("status"))
        if isinstance(data.get("packets"), list):
            for item in data["packets"]:
                if not isinstance(item, dict):
                    raise AppliedLoreTargetedError(f"Packet bundle contains non-object packet: {source_path}")
                packet = dict(item)
                if bundle_release_set:
                    packet.setdefault("release_set_id", bundle_release_set)
                packet.setdefault("_bundle_status", bundle_status)
                packet.setdefault("_manifest_status", manifest_status)
                add_packet(packets_by_id, packet, source_path)
        else:
            packet = dict(data)
            if bundle_release_set:
                packet.setdefault("release_set_id", bundle_release_set)
            packet.setdefault("_bundle_status", bundle_status)
            packet.setdefault("_manifest_status", manifest_status)
            add_packet(packets_by_id, packet, source_path)

    return [packets_by_id[key] for key in sorted(packets_by_id)]


def merge_packets_by_id(*packet_groups: list[dict[str, Any]]) -> list[dict[str, Any]]:
    packets_by_id: dict[str, dict[str, Any]] = {}
    for packets in packet_groups:
        for packet in packets:
            packet_id = safe_text(packet.get("packet_id"))
            if packet_id:
                packets_by_id[packet_id] = packet
    return [packets_by_id[key] for key in sorted(packets_by_id)]


def split_selectors(values: list[str]) -> tuple[str, ...]:
    selectors: list[str] = []
    for value in values:
        for part in value.split(","):
            part = part.strip()
            if part:
                selectors.append(part)
    return tuple(dict.fromkeys(selectors))


def select_packets(packets: list[dict[str, Any]], selectors: tuple[str, ...], include_all: bool) -> list[dict[str, Any]]:
    if include_all:
        return packets
    if not selectors:
        raise AppliedLoreTargetedError("Select at least one --packet-id/--packet-glob, or pass --all.")

    import fnmatch

    selected: list[dict[str, Any]] = []
    matched: set[str] = set()
    for packet in packets:
        packet_id = safe_text(packet.get("packet_id"))
        for selector in selectors:
            if packet_id == selector or fnmatch.fnmatchcase(packet_id, selector):
                selected.append(packet)
                matched.add(selector)
                break

    missing = [selector for selector in selectors if selector not in matched and not any("*" in c or "?" in c for c in selector)]
    if missing:
        raise AppliedLoreTargetedError("Selected packet ids not found: " + ", ".join(missing))
    if not selected:
        raise AppliedLoreTargetedError("Packet selection matched no packets.")
    return selected


def direction_for_locale(locale: str) -> str:
    return "rtl" if locale in RTL_LOCALES else "ltr"


def write_text_if_changed_count(path: Path, text: str, dry_run: bool) -> bool:
    if path.exists() and path.read_text(encoding="utf-8") == text:
        return False
    if dry_run:
        return True
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8", newline="\n")
    return True


def validate_packet_source(packets: list[dict[str, Any]]) -> None:
    errors: list[str] = []
    required_locales = set(TARGET_LOCALES)

    for packet in packets:
        packet_id = safe_text(packet.get("packet_id")) or "<missing_packet_id>"
        localized_by_locale = packet.get("localized")
        if not isinstance(localized_by_locale, dict):
            errors.append(f"{packet_id}: localized must be object")
            continue

        actual_locales = set(str(locale) for locale in localized_by_locale)
        missing = sorted(required_locales.difference(actual_locales))
        extra = sorted(actual_locales.difference(required_locales))
        if missing:
            errors.append(f"{packet_id}: missing locales {', '.join(missing)}")
        if extra:
            errors.append(f"{packet_id}: unsupported locales {', '.join(extra)}")

        for locale in TARGET_LOCALES:
            row = localized_by_locale.get(locale)
            if not isinstance(row, dict):
                continue
            row_flags = localized_row_flags(row, locale, packet)
            if locale != "en_US" and (row_flags & ROW_FLAG_DRAFT_LOCALIZATION) == 0:
                errors.append(f"{packet_id}/{locale}: missing draft localization status")

            for field in REQUIRED_LOCALIZED_FIELDS:
                value = row.get(field)
                if not isinstance(value, str) or not value.strip():
                    errors.append(f"{packet_id}/{locale}/{field}: missing text")
                    continue
                validate_localized_field(errors, packet_id, locale, field, value, required=True)

            for field in OPTIONAL_LOCALIZED_FIELDS:
                value = row.get(field, "")
                if value:
                    if not isinstance(value, str):
                        errors.append(f"{packet_id}/{locale}/{field}: non-string text")
                        continue
                    validate_localized_field(errors, packet_id, locale, field, value, required=False)

            known_fields = LOCALIZED_TEXT_FIELDS + OPTIONAL_LOCALIZED_FIELDS + ("external_site_article_path",)
            unknown_fields = sorted(set(row).difference(known_fields))
            for field in unknown_fields:
                value = row.get(field)
                if isinstance(value, str):
                    for issue in find_text_integrity_errors(sanitize_localized_text(value)):
                        errors.append(f"{packet_id}/{locale}/{field}: {issue}")

    if errors:
        sample = "\n".join(errors[:40])
        remaining = "" if len(errors) <= 40 else f"\n... {len(errors) - 40} more"
        raise AppliedLoreTargetedError("Packet source validation failed:\n" + sample + remaining)


def validate_localized_field(
    errors: list[str],
    packet_id: str,
    locale: str,
    field: str,
    value: str,
    *,
    required: bool,
) -> None:
    has_draft_prefix = DRAFT_LOCALIZATION_PREFIX.match(value.strip()) is not None
    if locale == "en_US" and has_draft_prefix:
        errors.append(f"{packet_id}/{locale}/{field}: English row contains draft marker")

    cleaned = sanitize_localized_text(value)
    if not cleaned.strip():
        errors.append(f"{packet_id}/{locale}/{field}: text empty after marker strip")
    if "localization pending native pass" in cleaned:
        errors.append(f"{packet_id}/{locale}/{field}: draft marker leaked after sanitize")
    for issue in find_text_integrity_errors(cleaned):
        errors.append(f"{packet_id}/{locale}/{field}: {issue}")


def render_selected_pages(base: Path, packets: list[dict[str, Any]], dry_run: bool) -> tuple[int, int, int]:
    cluster_spoiler_tiers = {}
    try:
        from AppliedLorePageExporter import navigation_cluster_graph_rows

        for cluster in navigation_cluster_graph_rows(base):
            packet_id = safe_text(cluster.get("packet_id"))
            if packet_id:
                cluster_spoiler_tiers[packet_id] = safe_text(cluster.get("spoiler_tier"))
    except Exception:
        cluster_spoiler_tiers = {}

    written = 0
    unchanged = 0
    removed_disabled = 0
    for packet in packets:
        packet_id = safe_text(packet.get("packet_id"))
        localized_by_locale = packet.get("localized", {})
        if not isinstance(localized_by_locale, dict):
            raise AppliedLoreTargetedError(f"Packet localized must be object: {packet_id}")

        for locale in TARGET_LOCALES:
            row = localized_by_locale.get(locale)
            if not isinstance(row, dict):
                raise AppliedLoreTargetedError(f"Missing locale: packet={packet_id} locale={locale}")

            for folder, surface_key, surface_title in SURFACES:
                path = base / folder / locale / f"{packet_id}.md"
                if not is_surface_enabled(packet, surface_key):
                    if not dry_run and remove_generated_disabled_page(path, packet_id, surface_key):
                        removed_disabled += 1
                    continue

                rendered = render_page(base, packet, locale, surface_key, surface_title, cluster_spoiler_tiers)
                if write_text_if_changed_count(path, rendered, dry_run):
                    written += 1
                else:
                    unchanged += 1

    return written, unchanged, removed_disabled


def read_publication_surface_index(path: Path) -> list[dict[str, str]]:
    if not path.exists():
        return []
    with path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        if tuple(reader.fieldnames or ()) != PUBLICATION_INDEX_HEADERS:
            raise AppliedLoreTargetedError(f"Publication surface index header mismatch: {path}")
        return [{key: safe_text(row.get(key)) for key in PUBLICATION_INDEX_HEADERS} for row in reader]


def write_publication_surface_index(path: Path, rows: list[dict[str, str]], dry_run: bool) -> bool:
    buffer = io.StringIO(newline="")
    writer = csv.DictWriter(buffer, fieldnames=PUBLICATION_INDEX_HEADERS, lineterminator="\n")
    writer.writeheader()
    writer.writerows(rows)
    return write_text_if_changed_count(path, buffer.getvalue(), dry_run)


def applied_lore_packet_csv_path(root: Path) -> Path:
    return root / "Assets" / "_SourceData" / "DataMonolith" / "Narrative" / "applied_lore_packets.csv"


def read_baked_lore_packet_rows(path: Path) -> list[dict[str, str]]:
    if not path.exists():
        raise AppliedLoreTargetedError(f"Missing AppliedLore baked CSV: {path}")
    with path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        if tuple(reader.fieldnames or ()) != CSV_HEADERS:
            raise AppliedLoreTargetedError(f"AppliedLore baked CSV header mismatch: {path}")
        return [{key: safe_text(row.get(key)) for key in CSV_HEADERS} for row in reader]


def validate_selected_baked_csv(root: Path, packets: list[dict[str, Any]]) -> int:
    path = applied_lore_packet_csv_path(root)
    expected_rows = packet_rows(packets)
    selected_ids = {safe_text(packet.get("packet_id")) for packet in packets}
    selected_ids.discard("")
    actual_rows = [
        row for row in read_baked_lore_packet_rows(path)
        if safe_text(row.get("packet_id")) in selected_ids
    ]

    errors: list[str] = []
    expected_by_key: dict[tuple[str, str], dict[str, str]] = {}
    actual_by_key: dict[tuple[str, str], dict[str, str]] = {}

    for row in expected_rows:
        key = (row["packet_id"], row["locale"])
        if key in expected_by_key:
            errors.append(f"source duplicate row packet={key[0]} locale={key[1]}")
        expected_by_key[key] = {header: safe_text(row.get(header)) for header in CSV_HEADERS}

    for row in actual_rows:
        key = (row["packet_id"], row["locale"])
        if key in actual_by_key:
            errors.append(f"baked CSV duplicate row packet={key[0]} locale={key[1]}")
        actual_by_key[key] = row

    expected_keys = set(expected_by_key)
    actual_keys = set(actual_by_key)
    for packet_id, locale in sorted(expected_keys.difference(actual_keys)):
        errors.append(f"baked CSV missing row packet={packet_id} locale={locale}")
    for packet_id, locale in sorted(actual_keys.difference(expected_keys)):
        errors.append(f"baked CSV extra row packet={packet_id} locale={locale}")

    for key in sorted(expected_keys.intersection(actual_keys)):
        expected = expected_by_key[key]
        actual = actual_by_key[key]
        for header in CSV_HEADERS:
            if actual[header] != expected[header]:
                errors.append(
                    f"baked CSV mismatch packet={key[0]} locale={key[1]} field={header}: "
                    f"expected={expected[header]!r} actual={actual[header]!r}"
                )

    if errors:
        sample = "\n".join(errors[:40])
        remaining = "" if len(errors) <= 40 else f"\n... {len(errors) - 40} more"
        raise AppliedLoreTargetedError("Selected AppliedLore baked CSV validation failed:\n" + sample + remaining)

    return len(expected_rows)


def merge_baked_lore_packet_rows(root: Path, packets: list[dict[str, Any]], dry_run: bool) -> tuple[int, bool]:
    path = applied_lore_packet_csv_path(root)
    existing_rows = read_baked_lore_packet_rows(path)
    expected_rows = [{header: safe_text(row.get(header)) for header in CSV_HEADERS} for row in packet_rows(packets)]
    selected_ids = {safe_text(packet.get("packet_id")) for packet in packets}
    selected_ids.discard("")

    first_selected_index: int | None = None
    preserved_rows: list[dict[str, str]] = []
    for row in existing_rows:
        if safe_text(row.get("packet_id")) in selected_ids:
            if first_selected_index is None:
                first_selected_index = len(preserved_rows)
            continue
        preserved_rows.append(row)

    insert_at = first_selected_index if first_selected_index is not None else len(preserved_rows)
    merged_rows = preserved_rows[:insert_at] + expected_rows + preserved_rows[insert_at:]
    changed = write_text_if_changed_count(path, render_csv(merged_rows), dry_run)
    return len(expected_rows), changed


def merge_publication_surface_rows(base: Path, selected_packets: list[dict[str, Any]], dry_run: bool) -> tuple[int, bool]:
    path = base / "Publication_Surface_Index.csv"
    existing_rows = read_publication_surface_index(path)
    selected_ids = {safe_text(packet.get("packet_id")) for packet in selected_packets}
    selected_ids.discard("")
    expected_rows = publication_surface_rows(base, selected_packets)
    expected_by_key = {
        (row["surface"], row["locale"], row["packet_id"]): row
        for row in expected_rows
    }

    existing_selected_keys = [
        (row["surface"], row["locale"], row["packet_id"])
        for row in existing_rows
        if safe_text(row.get("packet_id")) in selected_ids
    ]
    existing_selected_by_key = {
        (row["surface"], row["locale"], row["packet_id"]): row
        for row in existing_rows
        if safe_text(row.get("packet_id")) in selected_ids
    }
    if (
        len(existing_selected_keys) == len(set(existing_selected_keys))
        and set(existing_selected_by_key) == set(expected_by_key)
        and all(existing_selected_by_key[key] == expected_by_key[key] for key in expected_by_key)
    ):
        return len(expected_rows), False

    emitted: set[tuple[str, str, str]] = set()
    merged: list[dict[str, str]] = []
    for row in existing_rows:
        packet_id = safe_text(row.get("packet_id"))
        if packet_id not in selected_ids:
            merged.append(row)
            continue

        key = (row["surface"], row["locale"], row["packet_id"])
        expected = expected_by_key.get(key)
        if expected is None or key in emitted:
            continue
        merged.append(expected)
        emitted.add(key)

    missing_expected = [row for key, row in expected_by_key.items() if key not in emitted]
    locale_order = {locale: index for index, locale in enumerate(TARGET_LOCALES)}
    missing_expected.sort(
        key=lambda row: (
            safe_text(row.get("surface")),
            locale_order.get(safe_text(row.get("locale")), 999),
            safe_text(row.get("packet_id")),
        )
    )
    merged.extend(missing_expected)

    changed = write_publication_surface_index(path, merged, dry_run)
    return len(expected_rows), changed


def refresh_locale_indexes(base: Path, all_packets: list[dict[str, Any]], dry_run: bool) -> tuple[int, int]:
    written = 0
    unchanged = 0
    for locale in TARGET_LOCALES:
        for folder, surface_key, _surface_title in SURFACES:
            surface_packets = [packet for packet in all_packets if is_surface_enabled(packet, surface_key)]
            path = base / folder / locale / "INDEX.md"
            rendered = render_index(surface_packets, locale, folder, surface_key)
            if write_text_if_changed_count(path, rendered, dry_run):
                written += 1
            else:
                unchanged += 1
    return written, unchanged


def refresh_localization_status_index(base: Path, all_packets: list[dict[str, Any]], dry_run: bool) -> bool:
    rendered = render_localization_status_index(all_packets)
    return write_text_if_changed_count(base / "Localization_Status_Index.md", rendered, dry_run)


def parse_frontmatter(markdown: str) -> tuple[dict[str, str], str]:
    if not markdown.startswith("---"):
        return {}, markdown
    match = re.match(r"\A---[ \t]*\r?\n(?P<meta>.*?)\r?\n---[ \t]*(?:\r?\n)?(?P<body>.*)\Z", markdown, re.DOTALL)
    if match is None:
        return {}, markdown
    metadata: dict[str, str] = {}
    for line in match.group("meta").splitlines():
        if ":" not in line:
            continue
        key, value = line.split(":", 1)
        metadata[key.strip()] = value.strip().strip("\"'")
    return metadata, match.group("body")


def validate_selected_pages(base: Path, packets: list[dict[str, Any]]) -> None:
    errors: list[str] = []
    for packet in packets:
        packet_id = safe_text(packet.get("packet_id"))
        localized_by_locale = packet.get("localized", {})
        if not isinstance(localized_by_locale, dict):
            continue

        for locale in TARGET_LOCALES:
            localized = localized_by_locale.get(locale, {})
            flags = localized_row_flags(localized, locale, packet) if isinstance(localized, dict) else 0
            expected_status = "source_authority" if locale == "en_US" else "draft_machine_or_llm"
            for folder, surface_key, _surface_title in SURFACES:
                path = base / folder / locale / f"{packet_id}.md"
                if not is_surface_enabled(packet, surface_key):
                    continue
                if not path.exists():
                    errors.append(f"{path}: missing generated page")
                    continue
                text = path.read_text(encoding="utf-8")
                metadata, _body = parse_frontmatter(text)
                expected = {
                    "packet_id": packet_id,
                    "locale": locale,
                    "surface": surface_key,
                    "direction": direction_for_locale(locale),
                    "localization_status": expected_status,
                    "localization_flags": str(flags),
                    "runtime_reads_markdown": "false",
                }
                for key, value in expected.items():
                    if metadata.get(key) != value:
                        errors.append(f"{path}: {key}={metadata.get(key)!r}, expected {value!r}")
                if "Draft " in text or "localization pending native pass" in text:
                    errors.append(f"{path}: player-visible draft marker leaked")
                for issue in find_text_integrity_errors(text):
                    errors.append(f"{path}: {issue}")

    if errors:
        sample = "\n".join(errors[:40])
        remaining = "" if len(errors) <= 40 else f"\n... {len(errors) - 40} more"
        raise AppliedLoreTargetedError("Generated page validation failed:\n" + sample + remaining)


def validate_publication_surface_index(base: Path, packets: list[dict[str, Any]]) -> None:
    path = base / "Publication_Surface_Index.csv"
    existing_rows = read_publication_surface_index(path)
    selected_ids = {safe_text(packet.get("packet_id")) for packet in packets}
    expected_rows = publication_surface_rows(base, packets)
    expected_by_key = {
        (row["surface"], row["locale"], row["packet_id"]): row
        for row in expected_rows
    }
    actual_by_key: dict[tuple[str, str, str], dict[str, str]] = {}
    duplicates: list[str] = []
    for row in existing_rows:
        if safe_text(row.get("packet_id")) not in selected_ids:
            continue
        key = (row["surface"], row["locale"], row["packet_id"])
        if key in actual_by_key:
            duplicates.append("/".join(key))
        actual_by_key[key] = row

    errors: list[str] = []
    if duplicates:
        errors.append("Duplicate selected surface rows: " + ", ".join(duplicates[:12]))
    missing = sorted(set(expected_by_key).difference(actual_by_key))
    extra = sorted(set(actual_by_key).difference(expected_by_key))
    if missing:
        errors.append("Missing surface rows: " + ", ".join("/".join(key) for key in missing[:12]))
    if extra:
        errors.append("Extra selected surface rows: " + ", ".join("/".join(key) for key in extra[:12]))

    for key, expected in expected_by_key.items():
        actual = actual_by_key.get(key)
        if actual is None:
            continue
        for column in PUBLICATION_INDEX_HEADERS:
            if safe_text(actual.get(column)) != safe_text(expected.get(column)):
                errors.append(
                    f"Surface row {'/'.join(key)} column {column}: "
                    f"{actual.get(column)!r}, expected {expected.get(column)!r}"
                )
                break
        page_path = base / expected["page_path"]
        if not page_path.exists():
            errors.append(f"Surface row {'/'.join(key)} page missing: {expected['page_path']}")

    if errors:
        sample = "\n".join(errors[:40])
        remaining = "" if len(errors) <= 40 else f"\n... {len(errors) - 40} more"
        raise AppliedLoreTargetedError("Publication surface index validation failed:\n" + sample + remaining)


def validate_locale_indexes(base: Path, packets: list[dict[str, Any]]) -> None:
    errors: list[str] = []
    for locale in TARGET_LOCALES:
        for folder, surface_key, _surface_title in SURFACES:
            path = base / folder / locale / "INDEX.md"
            if not path.exists():
                errors.append(f"{path}: missing locale index")
                continue
            text = path.read_text(encoding="utf-8")
            metadata, _body = parse_frontmatter(text)
            if metadata.get("locale") != locale:
                errors.append(f"{path}: locale frontmatter mismatch")
            if metadata.get("surface") != surface_key:
                errors.append(f"{path}: surface frontmatter mismatch")
            if metadata.get("direction") != direction_for_locale(locale):
                errors.append(f"{path}: direction frontmatter mismatch")
            for packet in packets:
                packet_id = safe_text(packet.get("packet_id"))
                if is_surface_enabled(packet, surface_key) and f"({packet_id}.md) `{packet_id}`" not in text:
                    errors.append(f"{path}: missing selected packet link {packet_id}")

    status_path = base / "Localization_Status_Index.md"
    if not status_path.exists():
        errors.append(f"{status_path}: missing localization status index")
    else:
        status_text = status_path.read_text(encoding="utf-8")
        for locale in TARGET_LOCALES:
            if f"`{locale}`:" not in status_text or f"direction={direction_for_locale(locale)}" not in status_text:
                errors.append(f"{status_path}: missing status line or direction for {locale}")

    if errors:
        sample = "\n".join(errors[:40])
        remaining = "" if len(errors) <= 40 else f"\n... {len(errors) - 40} more"
        raise AppliedLoreTargetedError("Locale index validation failed:\n" + sample + remaining)


def export_targeted(
    root: Path,
    selectors: tuple[str, ...],
    *,
    include_all: bool,
    explicit_packet_sources: tuple[Path, ...],
    dry_run: bool,
    validate_only: bool,
    source_only: bool,
    refresh_indexes: bool,
    validate_baked_csv: bool = False,
    update_baked_csv: bool = False,
) -> ExportStats:
    base = applied_content_base(root)
    all_packets = load_packet_sources(base, explicit_packet_sources)
    selected_packets = select_packets(all_packets, selectors, include_all)

    validate_packet_source(selected_packets)

    stats = ExportStats(source_packets=len(all_packets), target_packets=len(selected_packets))
    if update_baked_csv:
        stats.baked_csv_rows_targeted, stats.baked_csv_written = merge_baked_lore_packet_rows(
            root,
            selected_packets,
            dry_run,
        )
        if not dry_run:
            stats.baked_csv_rows_targeted = validate_selected_baked_csv(root, selected_packets)
    elif validate_baked_csv:
        stats.baked_csv_rows_targeted = validate_selected_baked_csv(root, selected_packets)
    if source_only:
        return stats
    if not validate_only:
        stats.pages_written, stats.pages_unchanged, stats.disabled_pages_removed = render_selected_pages(
            base,
            selected_packets,
            dry_run,
        )
        stats.surface_rows_targeted, stats.surface_index_written = merge_publication_surface_rows(
            base,
            selected_packets,
            dry_run,
        )
        if refresh_indexes:
            stats.refreshed_indexes = True
            index_packets = all_packets
            if explicit_packet_sources:
                index_packets = merge_packets_by_id(load_packet_sources(base, ()), all_packets)
            stats.locale_indexes_written, stats.locale_indexes_unchanged = refresh_locale_indexes(base, index_packets, dry_run)
            stats.status_index_written = refresh_localization_status_index(base, index_packets, dry_run)

    validate_selected_pages(base, selected_packets)
    validate_publication_surface_index(base, selected_packets)
    validate_locale_indexes(base, selected_packets)
    return stats


def format_stats(stats: ExportStats, *, dry_run: bool, validate_only: bool, source_only: bool) -> str:
    mode = "source_only" if source_only else "validate_only" if validate_only else "dry_run" if dry_run else "write"
    return (
        f"mode={mode} source_packets={stats.source_packets} target_packets={stats.target_packets} "
        f"pages_written={stats.pages_written} pages_unchanged={stats.pages_unchanged} "
        f"disabled_pages_removed={stats.disabled_pages_removed} surface_rows_targeted={stats.surface_rows_targeted} "
        f"surface_index_written={int(stats.surface_index_written)} "
        f"baked_csv_rows_targeted={stats.baked_csv_rows_targeted} "
        f"baked_csv_written={int(stats.baked_csv_written)} "
        f"refreshed_indexes={int(stats.refreshed_indexes)} "
        f"locale_indexes_written={stats.locale_indexes_written} "
        f"locale_indexes_unchanged={stats.locale_indexes_unchanged} "
        f"status_index_written={int(stats.status_index_written)} validation_errors=0"
    )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=".", help="Repository root.")
    parser.add_argument(
        "--packet-id",
        action="append",
        default=[],
        help="Packet id or comma-separated packet ids/globs. May be repeated.",
    )
    parser.add_argument(
        "--packet-glob",
        action="append",
        default=[],
        help="Packet glob or comma-separated globs. May be repeated.",
    )
    parser.add_argument(
        "--packet-source",
        action="append",
        default=[],
        help="Explicit packet JSON source path. Defaults to all AppliedContent packets/*.json.",
    )
    parser.add_argument("--all", action="store_true", help="Target every loaded packet.")
    parser.add_argument("--dry-run", action="store_true", help="Report writes without changing files.")
    parser.add_argument("--source-only", action="store_true", help="Validate selected packet JSON sources only.")
    parser.add_argument("--validate-only", action="store_true", help="Validate selected packet publication output only.")
    parser.add_argument(
        "--validate-baked-csv",
        action="store_true",
        help=(
            "Also validate selected rows in "
            "Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv match packet source exactly."
        ),
    )
    parser.add_argument(
        "--update-baked-csv",
        action="store_true",
        help=(
            "Replace selected rows in "
            "Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv from packet source, preserving unrelated rows."
        ),
    )
    parser.add_argument(
        "--refresh-indexes",
        action="store_true",
        help="Also regenerate locale INDEX.md files and Localization_Status_Index.md from all loaded packet JSON.",
    )
    args = parser.parse_args()

    root = Path(args.root).resolve()
    if args.validate_only and args.update_baked_csv:
        print("AppliedLore targeted export FAILED: --update-baked-csv cannot be combined with --validate-only")
        return 1
    selectors = split_selectors(args.packet_id + args.packet_glob)
    explicit_sources = tuple((root / value).resolve() for value in args.packet_source)
    try:
        stats = export_targeted(
            root,
            selectors,
            include_all=args.all,
            explicit_packet_sources=explicit_sources,
            dry_run=args.dry_run,
            validate_only=args.validate_only,
            source_only=args.source_only,
            refresh_indexes=args.refresh_indexes,
            validate_baked_csv=args.validate_baked_csv,
            update_baked_csv=args.update_baked_csv,
        )
    except AppliedLoreTargetedError as exc:
        print(f"AppliedLore targeted export FAILED: {exc}")
        return 1

    print(format_stats(stats, dry_run=args.dry_run, validate_only=args.validate_only, source_only=args.source_only))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
