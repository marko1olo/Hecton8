#!/usr/bin/env python3
"""Audit selected AppliedLore packets across authoring, publication and route handoff layers."""

from __future__ import annotations

import argparse
import csv
import json
import sys
from dataclasses import asdict, dataclass
from pathlib import Path

from AppliedLoreImporter import TARGET_LOCALES, collect_packets as collect_importer_packets, fnv1a32
from AppliedLorePageExporter import PUBLICATION_INDEX_HEADERS, publication_surface_rows, safe_text
from AppliedLoreRouteCardExporter import INPUT_HEADERS as ROUTE_CARD_HEADERS
from AppliedLoreRouteCardExporter import OUTPUT_HEADERS as ROUTE_CARD_SOURCE_HEADERS
from AppliedLoreRouteCardExporter import ROUTE_CARD_ENDING_PRESSURES
from AppliedLoreRouteCardExporter import ROUTE_CARD_PRIMARY_SURFACES
from AppliedLoreRouteCardExporter import SURFACE_MASKS
from AppliedLoreTargetedExporter import (
    AppliedLoreTargetedError,
    applied_content_base,
    configure_csv_field_limit,
    load_packet_sources,
    select_packets,
    split_selectors,
)


configure_csv_field_limit()


BINDING_MAP_HEADERS = (
    "packet_id",
    "packet_hash_hex",
    "packet_hash_uint",
    "release_set",
    "primary_component",
    "primary_field",
    "secondary_component",
    "secondary_field",
    "suggested_world_target",
    "unlock_moment",
    "notes",
)
EVIDENCE_GRAPH_HEADERS = (
    "packet_id",
    "arc_id",
    "depth_band",
    "route_moment",
    "prereq_packet_ids",
    "next_packet_ids",
    "evidence_type",
    "truth_claim",
    "player_decision",
    "spoiler_tier",
    "primary_surface",
)
PRIMARY_SURFACES = set(ROUTE_CARD_PRIMARY_SURFACES)
ENDING_PRESSURES = set(ROUTE_CARD_ENDING_PRESSURES)
DISABLED_MANIFEST_CSV_FIELDS = {
    "binding_maps": "runtime_binding_map",
    "graphs": "evidence_graph",
    "route_cards": "route_cards",
}


class AppliedLoreCoverageError(Exception):
    """Raised when selected packets are incomplete across coverage layers."""


def configure_stdout() -> None:
    """Keep localized audit failures printable on non-UTF-8 Windows consoles."""

    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except AttributeError:
        pass


@dataclass(frozen=True)
class CoverageStats:
    source_packets: int
    target_packets: int
    baked_rows: int
    publication_rows: int
    binding_rows: int
    graph_rows: int
    route_cards: int
    route_source_rows: int


@dataclass(frozen=True)
class InventoryStats:
    source_packets: int
    baked_packets: int
    unbaked_packets: int
    baked_missing_source_packets: int
    canonical_ready_unbaked_packets: int
    canonical_not_ready_unbaked_packets: int
    sample_unbaked: tuple[str, ...]
    sample_canonical_ready_unbaked: tuple[str, ...]


def packet_refs(value: str) -> tuple[str, ...]:
    return tuple(part.strip() for part in safe_text(value).split(";") if part.strip())


def read_csv_rows(path: Path, expected_headers: tuple[str, ...] | None = None) -> list[dict[str, str]]:
    if not path.exists():
        raise AppliedLoreCoverageError(f"Missing CSV: {path}")
    with path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        if expected_headers is not None and tuple(reader.fieldnames or ()) != expected_headers:
            raise AppliedLoreCoverageError(f"CSV header mismatch: {path}")
        return [{key: safe_text(value) for key, value in row.items()} for row in reader]


def disabled_manifest_csv_paths(base: Path, folder: str) -> set[Path]:
    manifest_field = DISABLED_MANIFEST_CSV_FIELDS.get(folder)
    if manifest_field is None:
        return set()

    release_dir = base / "release_sets"
    if not release_dir.exists():
        return set()

    root = base.parent.parent.parent
    disabled_paths: set[Path] = set()
    for manifest_path in sorted(release_dir.glob("*_manifest.json"), key=lambda item: item.name.lower()):
        data = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
        if data.get("canonical_importer_ready") is not False:
            continue

        artifact = safe_text(data.get(manifest_field))
        if artifact:
            disabled_paths.add((root / artifact).resolve())
    return disabled_paths


def iter_source_csvs(base: Path, folder: str, pattern: str) -> list[Path]:
    path = base / folder
    if not path.exists():
        raise AppliedLoreCoverageError(f"Missing AppliedContent folder: {path}")
    disabled_paths = disabled_manifest_csv_paths(base, folder)
    return sorted(
        (item for item in path.glob(pattern) if item.resolve() not in disabled_paths),
        key=lambda item: item.name.lower(),
    )


def parse_uint(value: str, field: str, row_label: str) -> int:
    try:
        return int(safe_text(value), 0)
    except ValueError as exc:
        raise AppliedLoreCoverageError(f"{row_label}: invalid {field}={value!r}") from exc


def hash_list(values: tuple[str, ...], *, hex_format: bool) -> str:
    if hex_format:
        return ";".join(f"0x{fnv1a32(value):08X}" for value in values)
    return ";".join(str(fnv1a32(value)) for value in values)


def require_cell(row: dict[str, str], field: str, row_label: str, errors: list[str]) -> str:
    value = safe_text(row.get(field))
    if not value:
        errors.append(f"{row_label}: missing {field}")
    return value


def validate_baked_rows(root: Path, selected_ids: set[str], errors: list[str]) -> int:
    path = root / "Assets" / "_SourceData" / "DataMonolith" / "Narrative" / "applied_lore_packets.csv"
    rows = read_csv_rows(path)
    locales_by_packet: dict[str, list[str]] = {packet_id: [] for packet_id in selected_ids}
    for index, row in enumerate(rows, start=2):
        packet_id = safe_text(row.get("packet_id"))
        if packet_id not in selected_ids:
            continue
        locale = require_cell(row, "locale", f"{path}:{index}", errors)
        if locale:
            locales_by_packet[packet_id].append(locale)

    count = 0
    required_locales = set(TARGET_LOCALES)
    for packet_id in sorted(selected_ids):
        locales = locales_by_packet.get(packet_id, [])
        count += len(locales)
        missing = sorted(required_locales.difference(locales))
        extra = sorted(set(locales).difference(required_locales))
        duplicates = sorted(locale for locale in set(locales) if locales.count(locale) > 1)
        if missing:
            errors.append(f"{packet_id}: baked CSV missing locales {', '.join(missing)}")
        if extra:
            errors.append(f"{packet_id}: baked CSV unsupported locales {', '.join(extra)}")
        if duplicates:
            errors.append(f"{packet_id}: baked CSV duplicate locales {', '.join(duplicates)}")
    return count


def baked_packet_ids(root: Path) -> set[str]:
    path = root / "Assets" / "_SourceData" / "DataMonolith" / "Narrative" / "applied_lore_packets.csv"
    rows = read_csv_rows(path)
    return {safe_text(row.get("packet_id")) for row in rows if safe_text(row.get("packet_id"))}


def canonical_ready_manifest_packet_ids(root: Path) -> set[str]:
    try:
        packets = collect_importer_packets(root)
    except ValueError as exc:
        raise AppliedLoreCoverageError(f"Canonical ready manifest/source mismatch: {exc}") from exc

    packet_ids: set[str] = set()
    for packet in packets:
        packet_ids.add(safe_text(packet.get("packet_id")))
    packet_ids.discard("")
    return packet_ids


def inventory_sources(
    root: Path,
    *,
    explicit_packet_sources: tuple[Path, ...] = (),
    sample_limit: int = 20,
) -> InventoryStats:
    base = applied_content_base(root)
    source_packets = load_packet_sources(base, explicit_packet_sources)
    source_ids = {safe_text(packet.get("packet_id")) for packet in source_packets}
    source_ids.discard("")
    baked_ids = baked_packet_ids(root)
    unbaked = sorted(source_ids.difference(baked_ids))
    baked_missing_source = sorted(baked_ids.difference(source_ids))
    ready_unbaked = sorted(canonical_ready_manifest_packet_ids(root).intersection(unbaked))
    not_ready_unbaked = sorted(set(unbaked).difference(ready_unbaked))
    return InventoryStats(
        source_packets=len(source_ids),
        baked_packets=len(baked_ids),
        unbaked_packets=len(unbaked),
        baked_missing_source_packets=len(baked_missing_source),
        canonical_ready_unbaked_packets=len(ready_unbaked),
        canonical_not_ready_unbaked_packets=len(not_ready_unbaked),
        sample_unbaked=tuple(unbaked[:sample_limit]),
        sample_canonical_ready_unbaked=tuple(ready_unbaked[:sample_limit]),
    )


def validate_publication_rows(base: Path, selected_packets: list[dict[str, object]], errors: list[str]) -> int:
    path = base / "Publication_Surface_Index.csv"
    rows = read_csv_rows(path, PUBLICATION_INDEX_HEADERS)
    expected_rows = publication_surface_rows(base, selected_packets)
    expected_by_key = {(row["surface"], row["locale"], row["packet_id"]): row for row in expected_rows}
    selected_ids = {safe_text(packet.get("packet_id")) for packet in selected_packets}
    actual_by_key: dict[tuple[str, str, str], dict[str, str]] = {}

    for index, row in enumerate(rows, start=2):
        packet_id = safe_text(row.get("packet_id"))
        if packet_id not in selected_ids:
            continue
        key = (safe_text(row.get("surface")), safe_text(row.get("locale")), packet_id)
        if key in actual_by_key:
            errors.append(f"{path}:{index}: duplicate publication row {'/'.join(key)}")
        actual_by_key[key] = row

    for key, expected in sorted(expected_by_key.items()):
        actual = actual_by_key.get(key)
        if actual is None:
            errors.append("Missing publication row: " + "/".join(key))
            continue
        for column in PUBLICATION_INDEX_HEADERS:
            if safe_text(actual.get(column)) != safe_text(expected.get(column)):
                errors.append(
                    f"Publication row {'/'.join(key)} {column}: {actual.get(column)!r}, expected {expected.get(column)!r}"
                )
                break
        page_path = base / expected["page_path"]
        if not page_path.exists():
            errors.append(f"Publication page missing for {'/'.join(key)}: {expected['page_path']}")

    extra = sorted(set(actual_by_key).difference(expected_by_key))
    if extra:
        errors.append("Extra selected publication rows: " + ", ".join("/".join(key) for key in extra[:20]))
    return len(expected_rows)


def validate_binding_maps(base: Path, selected_ids: set[str], errors: list[str]) -> int:
    rows_by_packet: dict[str, list[tuple[Path, int, dict[str, str]]]] = {packet_id: [] for packet_id in selected_ids}
    for path in iter_source_csvs(base, "binding_maps", "*_runtime_binding_map.csv"):
        rows = read_csv_rows(path, BINDING_MAP_HEADERS)
        for index, row in enumerate(rows, start=2):
            packet_id = safe_text(row.get("packet_id"))
            if packet_id in selected_ids:
                rows_by_packet[packet_id].append((path, index, row))

    count = 0
    for packet_id in sorted(selected_ids):
        rows = rows_by_packet.get(packet_id, [])
        count += len(rows)
        if len(rows) != 1:
            errors.append(f"{packet_id}: binding map row count {len(rows)}, expected 1")
            continue
        path, index, row = rows[0]
        label = f"{path}:{index}"
        expected_hash = fnv1a32(packet_id)
        if safe_text(row.get("packet_hash_hex")) != f"0x{expected_hash:08X}":
            errors.append(f"{label}: packet_hash_hex mismatch for {packet_id}")
        if parse_uint(row.get("packet_hash_uint", ""), "packet_hash_uint", label) != expected_hash:
            errors.append(f"{label}: packet_hash_uint mismatch for {packet_id}")
        for field in ("release_set", "primary_component", "primary_field", "suggested_world_target", "unlock_moment"):
            require_cell(row, field, label, errors)
    return count


def validate_evidence_graphs(base: Path, selected_ids: set[str], known_packet_ids: set[str], errors: list[str]) -> int:
    rows_by_packet: dict[str, list[tuple[Path, int, dict[str, str]]]] = {packet_id: [] for packet_id in selected_ids}
    for path in iter_source_csvs(base, "graphs", "*_evidence_graph.csv"):
        rows = read_csv_rows(path, EVIDENCE_GRAPH_HEADERS)
        for index, row in enumerate(rows, start=2):
            packet_id = safe_text(row.get("packet_id"))
            if packet_id in selected_ids:
                rows_by_packet[packet_id].append((path, index, row))

    count = 0
    for packet_id in sorted(selected_ids):
        rows = rows_by_packet.get(packet_id, [])
        count += len(rows)
        if len(rows) != 1:
            errors.append(f"{packet_id}: evidence graph row count {len(rows)}, expected 1")
            continue
        path, index, row = rows[0]
        label = f"{path}:{index}"
        for field in (
            "arc_id",
            "depth_band",
            "route_moment",
            "evidence_type",
            "truth_claim",
            "player_decision",
            "primary_surface",
        ):
            require_cell(row, field, label, errors)
        primary_surface = safe_text(row.get("primary_surface"))
        if primary_surface and primary_surface not in PRIMARY_SURFACES:
            errors.append(f"{label}: unsupported primary_surface={primary_surface!r}")
        spoiler_tier = parse_uint(row.get("spoiler_tier", ""), "spoiler_tier", label)
        if spoiler_tier > 3:
            errors.append(f"{label}: spoiler_tier out of range")
        for ref_field in ("prereq_packet_ids", "next_packet_ids"):
            for ref in packet_refs(row.get(ref_field, "")):
                if ref == packet_id:
                    errors.append(f"{label}: self reference in {ref_field}")
                if ref not in known_packet_ids:
                    errors.append(f"{label}: unknown {ref_field} ref {ref}")
    return count


def validate_route_cards(
    root: Path,
    base: Path,
    selected_ids: set[str],
    known_packet_ids: set[str],
    errors: list[str],
) -> tuple[int, int]:
    owner_by_packet: dict[str, tuple[Path, int, dict[str, str]]] = {}
    for path in iter_source_csvs(base, "route_cards", "*_route_cards.csv"):
        rows = read_csv_rows(path, ROUTE_CARD_HEADERS)
        for index, row in enumerate(rows, start=2):
            packet_ids = packet_refs(row.get("packet_ids", ""))
            owns_selected_packet = any(packet_id in selected_ids for packet_id in packet_ids)
            if not owns_selected_packet:
                continue

            for packet_id in packet_ids:
                if packet_id not in selected_ids:
                    continue
                if packet_id in owner_by_packet:
                    previous_path, previous_index, previous_row = owner_by_packet[packet_id]
                    errors.append(
                        f"{path}:{index}: packet {packet_id} already owned by "
                        f"{previous_row.get('route_card_id')} at {previous_path}:{previous_index}"
                    )
                owner_by_packet[packet_id] = (path, index, row)
    for packet_id in sorted(selected_ids):
        if packet_id not in owner_by_packet:
            errors.append(f"{packet_id}: missing route-card ownership")

    source_rows = read_csv_rows(
        root / "Assets" / "_SourceData" / "DataMonolith" / "Narrative" / "applied_lore_route_cards.csv",
        ROUTE_CARD_SOURCE_HEADERS,
    )
    source_by_route = {safe_text(row.get("route_card_id")): row for row in source_rows}
    route_ids_seen: set[str] = set()
    route_source_ids_seen: set[str] = set()
    route_source_count = 0
    for packet_id, (path, index, route_row) in sorted(owner_by_packet.items()):
        route_card_id = require_cell(route_row, "route_card_id", f"{path}:{index}", errors)
        if not route_card_id:
            continue
        route_ids_seen.add(route_card_id)
        source = source_by_route.get(route_card_id)
        if source is None:
            errors.append(f"{route_card_id}: missing route-card source export row")
            continue
        if route_card_id not in route_source_ids_seen:
            route_source_count += 1
            route_source_ids_seen.add(route_card_id)
        validate_route_source_row(route_row, source, route_card_id, known_packet_ids, errors)
        if packet_id not in packet_refs(source.get("packet_ids", "")):
            errors.append(f"{route_card_id}: route source export no longer owns {packet_id}")

    return len(route_ids_seen), route_source_count


def validate_route_source_row(
    route_row: dict[str, str],
    source: dict[str, str],
    route_card_id: str,
    known_packet_ids: set[str],
    errors: list[str],
) -> None:
    row_label = f"route_source:{route_card_id}"
    phase_id = safe_text(route_row.get("phase_id"))
    primary_surface = safe_text(route_row.get("primary_surface"))
    ending_pressure = safe_text(route_row.get("ending_pressure"))
    packets = tuple(packet_id for packet_id in packet_refs(route_row.get("packet_ids", "")) if packet_id in known_packet_ids)
    required = tuple(
        packet_id for packet_id in packet_refs(route_row.get("required_packet_ids", "")) if packet_id in known_packet_ids
    )

    if primary_surface not in PRIMARY_SURFACES:
        errors.append(f"{route_card_id}: unsupported primary_surface={primary_surface!r}")
    if ending_pressure not in ENDING_PRESSURES:
        errors.append(f"{route_card_id}: unsupported ending_pressure={ending_pressure!r}")
    for field in ("phase_id", "depth_min_m", "depth_max_m", "primary_surface", "ending_pressure"):
        if safe_text(source.get(field)) != safe_text(route_row.get(field)):
            errors.append(f"{row_label}: {field} mismatch")

    if packet_refs(source.get("packet_ids", "")) != packets:
        errors.append(f"{row_label}: packet_ids mismatch")
    if packet_refs(source.get("required_packet_ids", "")) != required:
        errors.append(f"{row_label}: required_packet_ids mismatch")

    expected_surface_mask = SURFACE_MASKS.get(primary_surface)
    if expected_surface_mask is not None and parse_uint(source.get("primary_surface_mask", ""), "primary_surface_mask", row_label) != expected_surface_mask:
        errors.append(f"{row_label}: primary_surface_mask mismatch")

    hash_checks = (
        ("route_card_hash_hex", fnv1a32(route_card_id)),
        ("route_card_hash_uint", fnv1a32(route_card_id)),
        ("phase_hash_hex", fnv1a32(phase_id)),
        ("phase_hash_uint", fnv1a32(phase_id)),
        ("ending_pressure_hash_hex", fnv1a32(ending_pressure)),
        ("ending_pressure_hash_uint", fnv1a32(ending_pressure)),
    )
    for field, expected in hash_checks:
        if parse_uint(source.get(field, ""), field, row_label) != expected:
            errors.append(f"{row_label}: {field} mismatch")
    if safe_text(source.get("packet_hashes_hex")) != hash_list(packets, hex_format=True):
        errors.append(f"{row_label}: packet_hashes_hex mismatch")
    if safe_text(source.get("packet_hashes_uint")) != hash_list(packets, hex_format=False):
        errors.append(f"{row_label}: packet_hashes_uint mismatch")
    if safe_text(source.get("required_packet_hashes_hex")) != hash_list(required, hex_format=True):
        errors.append(f"{row_label}: required_packet_hashes_hex mismatch")
    if safe_text(source.get("required_packet_hashes_uint")) != hash_list(required, hex_format=False):
        errors.append(f"{row_label}: required_packet_hashes_uint mismatch")


def audit_selected_packets(
    root: Path,
    selectors: tuple[str, ...],
    *,
    include_all: bool = False,
    explicit_packet_sources: tuple[Path, ...] = (),
) -> CoverageStats:
    base = applied_content_base(root)
    all_packets = load_packet_sources(base, explicit_packet_sources)
    if include_all and not explicit_packet_sources:
        selected_packets = collect_importer_packets(root)
    else:
        selected_packets = select_packets(all_packets, selectors, include_all)
    selected_ids = {safe_text(packet.get("packet_id")) for packet in selected_packets}
    selected_ids.discard("")
    known_packet_ids = {safe_text(packet.get("packet_id")) for packet in all_packets}
    known_packet_ids.discard("")
    runtime_packet_ids = baked_packet_ids(root)

    errors: list[str] = []
    if include_all and not explicit_packet_sources:
        extra_baked_packets = sorted(runtime_packet_ids.difference(selected_ids))
        if extra_baked_packets:
            errors.append(
                "Baked CSV has packets outside canonical importer selection: "
                + ", ".join(extra_baked_packets[:20])
            )
    baked_rows = validate_baked_rows(root, selected_ids, errors)
    publication_rows = validate_publication_rows(base, selected_packets, errors)
    binding_rows = validate_binding_maps(base, selected_ids, errors)
    graph_rows = validate_evidence_graphs(base, selected_ids, known_packet_ids, errors)
    route_cards, route_source_rows = validate_route_cards(root, base, selected_ids, runtime_packet_ids, errors)

    if errors:
        sample = "\n".join(errors[:80])
        remaining = "" if len(errors) <= 80 else f"\n... {len(errors) - 80} more"
        raise AppliedLoreCoverageError("AppliedLore packet coverage failed:\n" + sample + remaining)

    return CoverageStats(
        source_packets=len(all_packets),
        target_packets=len(selected_packets),
        baked_rows=baked_rows,
        publication_rows=publication_rows,
        binding_rows=binding_rows,
        graph_rows=graph_rows,
        route_cards=route_cards,
        route_source_rows=route_source_rows,
    )


def format_stats(stats: CoverageStats) -> str:
    return (
        "AppliedLore packet coverage OK: "
        f"source_packets={stats.source_packets} target_packets={stats.target_packets} "
        f"baked_rows={stats.baked_rows} publication_rows={stats.publication_rows} "
        f"binding_rows={stats.binding_rows} graph_rows={stats.graph_rows} "
        f"route_cards={stats.route_cards} route_source_rows={stats.route_source_rows}"
    )


def format_inventory(stats: InventoryStats) -> str:
    lines = [
        "AppliedLore packet inventory:",
        f"source_packets={stats.source_packets}",
        f"baked_packets={stats.baked_packets}",
        f"unbaked_packets={stats.unbaked_packets}",
        f"baked_missing_source_packets={stats.baked_missing_source_packets}",
        f"canonical_ready_unbaked_packets={stats.canonical_ready_unbaked_packets}",
        f"canonical_not_ready_unbaked_packets={stats.canonical_not_ready_unbaked_packets}",
    ]
    if stats.sample_canonical_ready_unbaked:
        lines.append("sample_canonical_ready_unbaked=" + ",".join(stats.sample_canonical_ready_unbaked))
    elif stats.sample_unbaked:
        lines.append("sample_unbaked=" + ",".join(stats.sample_unbaked))
    return "\n".join(lines)


def main() -> int:
    configure_stdout()
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=".", help="Repository root.")
    parser.add_argument("--packet-id", action="append", default=[], help="Packet id or comma-separated ids/globs.")
    parser.add_argument("--packet-glob", action="append", default=[], help="Packet glob or comma-separated globs.")
    parser.add_argument("--packet-source", action="append", default=[], help="Explicit packet JSON source path.")
    parser.add_argument("--all", action="store_true", help="Audit every loaded packet.")
    parser.add_argument("--inventory", action="store_true", help="Compare packet JSON sources against baked packet CSV.")
    parser.add_argument("--sample-limit", type=int, default=20, help="Maximum inventory sample packet ids.")
    parser.add_argument("--json", action="store_true", help="Print machine-readable stats.")
    args = parser.parse_args()

    selectors = split_selectors(args.packet_id + args.packet_glob)
    root = Path(args.root).resolve()
    explicit_sources = tuple(Path(path).resolve() for path in args.packet_source)
    if args.inventory:
        stats = inventory_sources(root, explicit_packet_sources=explicit_sources, sample_limit=max(args.sample_limit, 0))
        if args.json:
            print(json.dumps(asdict(stats), indent=2, sort_keys=True))
        else:
            print(format_inventory(stats))
        return 0

    try:
        stats = audit_selected_packets(
            root,
            selectors,
            include_all=args.all,
            explicit_packet_sources=explicit_sources,
        )
    except (AppliedLoreCoverageError, AppliedLoreTargetedError) as exc:
        print(str(exc))
        return 1
    if args.json:
        print(json.dumps(asdict(stats), indent=2, sort_keys=True))
    else:
        print(format_stats(stats))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
