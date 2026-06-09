#!/usr/bin/env python3
"""Export checked AppliedLore route cards into DataMonolith source data."""

from __future__ import annotations

import argparse
import csv
import io
import json
from pathlib import Path

from AppliedLoreImporter import fnv1a32


INPUT_HEADERS = (
    "route_card_id",
    "phase_id",
    "depth_min_m",
    "depth_max_m",
    "packet_ids",
    "required_packet_ids",
    "primary_surface",
    "world_object_hint",
    "player_question",
    "truth_payload",
    "replay_axis",
    "ending_pressure",
)
OUTPUT_HEADERS = (
    "route_card_id",
    "route_card_hash_hex",
    "route_card_hash_uint",
    "phase_id",
    "phase_hash_hex",
    "phase_hash_uint",
    "depth_min_m",
    "depth_max_m",
    "primary_surface",
    "primary_surface_mask",
    "ending_pressure",
    "ending_pressure_hash_hex",
    "ending_pressure_hash_uint",
    "packet_ids",
    "packet_hashes_hex",
    "packet_hashes_uint",
    "required_packet_ids",
    "required_packet_hashes_hex",
    "required_packet_hashes_uint",
)
SURFACE_MASKS = {
    "title": 1 << 0,
    "scanner": 1 << 1,
    "terminal": 1 << 2,
    "audio": 1 << 3,
    "in_game_wiki": 1 << 4,
    "external_site": 1 << 5,
    "field_note": 1 << 6,
}
ROUTE_CARD_PRIMARY_SURFACES = frozenset(
    (
        "scanner",
        "terminal",
        "audio",
        "in_game_wiki",
        "external_site",
        "field_note",
    )
)
ROUTE_CARD_ENDING_PRESSURES = frozenset(
    (
        "none",
        "material",
        "truth",
        "partial_exit",
        "material_or_partial",
        "quarantine_hold",
    )
)
ROUTE_CARD_PACKET_CAPACITY = 8
ROUTE_CARD_PREREQUISITE_CAPACITY = 4


def require_cell(row: dict[str, str], field: str, location: str) -> str:
    value = row.get(field, "")
    if value == "":
        raise ValueError(f"Route card {location}: missing {field}")
    return value


def parse_depth(value: str, field: str, location: str) -> int:
    try:
        depth = int(value)
    except ValueError as exc:
        raise ValueError(f"Route card {location}: invalid {field}={value!r}") from exc
    if depth < 0:
        raise ValueError(f"Route card {location}: {field} must be non-negative")
    return depth


def packet_refs(value: str) -> list[str]:
    return [part.strip() for part in value.split(";") if part.strip()]


def hash_list(values: list[str], *, hex_format: bool) -> str:
    if hex_format:
        return ";".join(f"0x{fnv1a32(value):08X}" for value in values)
    return ";".join(str(fnv1a32(value)) for value in values)


def render_output_row(
    *,
    route_card_id: str,
    phase_id: str,
    depth_min_text: str,
    depth_max_text: str,
    primary_surface: str,
    ending_pressure: str,
    runtime_packets: list[str],
    runtime_required: list[str],
) -> dict[str, str]:
    route_hash = fnv1a32(route_card_id)
    phase_hash = fnv1a32(phase_id)
    pressure_hash = fnv1a32(ending_pressure)
    return {
        "route_card_id": route_card_id,
        "route_card_hash_hex": f"0x{route_hash:08X}",
        "route_card_hash_uint": str(route_hash),
        "phase_id": phase_id,
        "phase_hash_hex": f"0x{phase_hash:08X}",
        "phase_hash_uint": str(phase_hash),
        "depth_min_m": depth_min_text,
        "depth_max_m": depth_max_text,
        "primary_surface": primary_surface,
        "primary_surface_mask": str(SURFACE_MASKS[primary_surface]),
        "ending_pressure": ending_pressure,
        "ending_pressure_hash_hex": f"0x{pressure_hash:08X}",
        "ending_pressure_hash_uint": str(pressure_hash),
        "packet_ids": ";".join(runtime_packets),
        "packet_hashes_hex": hash_list(runtime_packets, hex_format=True),
        "packet_hashes_uint": hash_list(runtime_packets, hex_format=False),
        "required_packet_ids": ";".join(runtime_required),
        "required_packet_hashes_hex": hash_list(runtime_required, hex_format=True),
        "required_packet_hashes_uint": hash_list(runtime_required, hex_format=False),
    }


def validate_acyclic_prerequisites(graph: dict[str, set[str]]) -> None:
    visiting: set[str] = set()
    visited: set[str] = set()
    stack: list[str] = []

    def visit(packet_id: str) -> None:
        if packet_id in visited:
            return
        if packet_id in visiting:
            cycle_start = stack.index(packet_id)
            cycle = stack[cycle_start:] + [packet_id]
            raise ValueError("Route-card prerequisite cycle: " + " -> ".join(cycle))

        visiting.add(packet_id)
        stack.append(packet_id)
        for prerequisite in sorted(graph.get(packet_id, ())):
            if prerequisite in graph:
                visit(prerequisite)
        stack.pop()
        visiting.remove(packet_id)
        visited.add(packet_id)

    for packet_id in sorted(graph):
        visit(packet_id)


def baked_packet_ids(root: Path) -> set[str]:
    path = root / "Assets" / "_SourceData" / "DataMonolith" / "Narrative" / "applied_lore_packets.csv"
    if not path.exists():
        raise ValueError(f"Missing AppliedLore packet CSV: {path}")

    packet_ids: set[str] = set()
    with path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        for row in reader:
            packet_id = row.get("packet_id", "")
            if packet_id:
                packet_ids.add(packet_id)
    return packet_ids


def iter_route_card_sources(root: Path) -> list[Path]:
    manifest_dir = root / "Docs" / "Lore" / "AppliedContent" / "release_sets"
    disabled_paths: set[Path] = set()
    if manifest_dir.exists():
        for manifest_path in sorted(manifest_dir.glob("*_manifest.json"), key=lambda path: path.name.lower()):
            manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
            if manifest.get("canonical_importer_ready") is not False:
                continue

            route_cards = str(manifest.get("route_cards", "")).strip()
            if route_cards:
                disabled_paths.add((root / route_cards).resolve())

    route_dir = root / "Docs" / "Lore" / "AppliedContent" / "route_cards"
    return sorted(
        (path for path in route_dir.glob("*_route_cards.csv") if path.resolve() not in disabled_paths),
        key=lambda path: path.name.lower(),
    )


def route_card_output_path(root: Path) -> Path:
    return root / "Assets" / "_SourceData" / "DataMonolith" / "Narrative" / "applied_lore_route_cards.csv"


def render_route_card_export(root: Path) -> tuple[int, str]:
    input_paths = iter_route_card_sources(root)
    if not input_paths:
        raise ValueError("Missing route-card source CSV files.")

    known_packets = baked_packet_ids(root)
    output_rows: list[dict[str, str]] = []
    seen_routes: set[str] = set()
    packet_owner_by_id: dict[str, str] = {}
    prerequisite_graph: dict[str, set[str]] = {}
    missing_packet_refs: list[str] = []
    for input_path in input_paths:
        with input_path.open("r", encoding="utf-8", newline="") as handle:
            reader = csv.DictReader(handle)
            if tuple(reader.fieldnames or ()) != INPUT_HEADERS:
                raise ValueError(f"Route-card source header mismatch: {input_path}")

            for line_number, row in enumerate(reader, start=2):
                location = f"{input_path}:{line_number}"
                route_card_id = require_cell(row, "route_card_id", location)
                if route_card_id in seen_routes:
                    raise ValueError(f"Duplicate route_card_id {route_card_id}: {location}")
                seen_routes.add(route_card_id)

                phase_id = require_cell(row, "phase_id", location)
                depth_min_text = require_cell(row, "depth_min_m", location)
                depth_max_text = require_cell(row, "depth_max_m", location)
                depth_min = parse_depth(depth_min_text, "depth_min_m", location)
                depth_max = parse_depth(depth_max_text, "depth_max_m", location)
                primary_surface = require_cell(row, "primary_surface", location)
                ending_pressure = require_cell(row, "ending_pressure", location)
                packets = packet_refs(require_cell(row, "packet_ids", location))
                required = packet_refs(row.get("required_packet_ids", ""))

                if depth_max < depth_min:
                    raise ValueError(f"Route card {location}: invalid depth bounds {depth_min}-{depth_max}")
                if primary_surface not in ROUTE_CARD_PRIMARY_SURFACES:
                    raise ValueError(f"Route card {location}: unsupported primary_surface={primary_surface}")
                if ending_pressure not in ROUTE_CARD_ENDING_PRESSURES:
                    raise ValueError(f"Route card {location}: unsupported ending_pressure={ending_pressure}")
                if not packets:
                    raise ValueError(f"Route card {location}: packet_ids is empty")
                if len(packets) > ROUTE_CARD_PACKET_CAPACITY:
                    raise ValueError(
                        f"Route card {location}: packet_ids exceeds capacity {ROUTE_CARD_PACKET_CAPACITY}"
                    )
                if len(required) > ROUTE_CARD_PREREQUISITE_CAPACITY:
                    raise ValueError(
                        "Route card "
                        f"{location}: required_packet_ids exceeds capacity {ROUTE_CARD_PREREQUISITE_CAPACITY}"
                    )
                for field in ("world_object_hint", "player_question", "truth_payload", "replay_axis"):
                    require_cell(row, field, location)
                runtime_packets = [packet_id for packet_id in packets if packet_id in known_packets]
                runtime_required = [packet_id for packet_id in required if packet_id in known_packets]

                if not runtime_packets:
                    continue

                for packet_id in runtime_packets:
                    owner = packet_owner_by_id.get(packet_id)
                    if owner is not None:
                        raise ValueError(
                            f"Route card {location}: packet_id {packet_id} already owned by {owner}"
                        )
                    packet_owner_by_id[packet_id] = route_card_id
                    prerequisite_graph.setdefault(packet_id, set())
                    for ref in runtime_required:
                        if ref == packet_id:
                            raise ValueError(
                                f"Route card {location}: packet_id {packet_id} depends on itself"
                            )
                        prerequisite_graph[packet_id].add(ref)

                output_rows.append(
                    render_output_row(
                        route_card_id=route_card_id,
                        phase_id=phase_id,
                        depth_min_text=depth_min_text,
                        depth_max_text=depth_max_text,
                        primary_surface=primary_surface,
                        ending_pressure=ending_pressure,
                        runtime_packets=runtime_packets,
                        runtime_required=runtime_required,
                    )
                )

    if missing_packet_refs:
        preview = "\n".join(missing_packet_refs[:50])
        if len(missing_packet_refs) > 50:
            preview += f"\n... {len(missing_packet_refs) - 50} more"
        raise ValueError("Route-card packet refs missing from baked AppliedLore packet CSV:\n" + preview)

    validate_acyclic_prerequisites(prerequisite_graph)

    buffer = io.StringIO(newline="")
    writer = csv.DictWriter(buffer, fieldnames=OUTPUT_HEADERS, lineterminator="\n")
    writer.writeheader()
    writer.writerows(output_rows)

    return len(output_rows), buffer.getvalue()


def route_card_export_current(root: Path) -> tuple[int, bool]:
    count, text = render_route_card_export(root)
    output_path = route_card_output_path(root)
    return count, output_path.exists() and output_path.read_text(encoding="utf-8") == text


def render_selected_route_card_rows(root: Path, packet_id: str) -> list[dict[str, str]]:
    if not packet_id:
        raise ValueError("--packet-id must not be empty")

    known_packets = baked_packet_ids(root)
    if packet_id not in known_packets:
        raise ValueError(f"Selected packet_id is not in baked AppliedLore packet CSV: {packet_id}")

    output_rows: list[dict[str, str]] = []
    selected_owner: str | None = None
    prerequisite_graph: dict[str, set[str]] = {}
    for input_path in iter_route_card_sources(root):
        with input_path.open("r", encoding="utf-8", newline="") as handle:
            reader = csv.DictReader(handle)
            if tuple(reader.fieldnames or ()) != INPUT_HEADERS:
                if packet_id in input_path.read_text(encoding="utf-8"):
                    raise ValueError(f"Route-card source header mismatch for selected packet: {input_path}")
                continue

            for line_number, row in enumerate(reader, start=2):
                packets = packet_refs(row.get("packet_ids", ""))
                if packet_id not in packets:
                    continue

                location = f"{input_path}:{line_number}"
                route_card_id = require_cell(row, "route_card_id", location)
                if selected_owner is not None:
                    raise ValueError(
                        f"Route card {location}: packet_id {packet_id} already owned by {selected_owner}"
                    )
                selected_owner = route_card_id

                phase_id = require_cell(row, "phase_id", location)
                depth_min_text = require_cell(row, "depth_min_m", location)
                depth_max_text = require_cell(row, "depth_max_m", location)
                depth_min = parse_depth(depth_min_text, "depth_min_m", location)
                depth_max = parse_depth(depth_max_text, "depth_max_m", location)
                primary_surface = require_cell(row, "primary_surface", location)
                ending_pressure = require_cell(row, "ending_pressure", location)
                required = packet_refs(row.get("required_packet_ids", ""))

                if depth_max < depth_min:
                    raise ValueError(f"Route card {location}: invalid depth bounds {depth_min}-{depth_max}")
                if primary_surface not in ROUTE_CARD_PRIMARY_SURFACES:
                    raise ValueError(f"Route card {location}: unsupported primary_surface={primary_surface}")
                if ending_pressure not in ROUTE_CARD_ENDING_PRESSURES:
                    raise ValueError(f"Route card {location}: unsupported ending_pressure={ending_pressure}")
                if len(packets) > ROUTE_CARD_PACKET_CAPACITY:
                    raise ValueError(
                        f"Route card {location}: packet_ids exceeds capacity {ROUTE_CARD_PACKET_CAPACITY}"
                    )
                if len(required) > ROUTE_CARD_PREREQUISITE_CAPACITY:
                    raise ValueError(
                        "Route card "
                        f"{location}: required_packet_ids exceeds capacity {ROUTE_CARD_PREREQUISITE_CAPACITY}"
                    )
                for field in ("world_object_hint", "player_question", "truth_payload", "replay_axis"):
                    require_cell(row, field, location)

                runtime_packets = [runtime_packet_id for runtime_packet_id in packets if runtime_packet_id in known_packets]
                runtime_required = [runtime_packet_id for runtime_packet_id in required if runtime_packet_id in known_packets]
                if packet_id not in runtime_packets:
                    raise ValueError(f"Route card {location}: selected packet is not runtime-baked")

                prerequisite_graph.setdefault(packet_id, set())
                for ref in runtime_required:
                    if ref == packet_id:
                        raise ValueError(f"Route card {location}: packet_id {packet_id} depends on itself")
                    prerequisite_graph[packet_id].add(ref)

                output_rows.append(
                    render_output_row(
                        route_card_id=route_card_id,
                        phase_id=phase_id,
                        depth_min_text=depth_min_text,
                        depth_max_text=depth_max_text,
                        primary_surface=primary_surface,
                        ending_pressure=ending_pressure,
                        runtime_packets=runtime_packets,
                        runtime_required=runtime_required,
                    )
                )

    if not output_rows:
        raise ValueError(f"Missing route-card source ownership for packet_id {packet_id}")

    validate_acyclic_prerequisites(prerequisite_graph)
    return output_rows


def selected_route_card_export_current(root: Path, packet_id: str) -> tuple[int, bool]:
    expected_rows = render_selected_route_card_rows(root, packet_id)
    output_path = route_card_output_path(root)
    if not output_path.exists():
        return len(expected_rows), False

    with output_path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        if tuple(reader.fieldnames or ()) != OUTPUT_HEADERS:
            return len(expected_rows), False
        actual_by_route: dict[str, dict[str, str]] = {}
        for row in reader:
            route_card_id = row.get("route_card_id", "")
            if route_card_id in actual_by_route:
                return len(expected_rows), False
            actual_by_route[route_card_id] = row

    current = all(actual_by_route.get(row["route_card_id"]) == row for row in expected_rows)
    return len(expected_rows), current


def check_selected_route_card_export(root: Path, packet_id: str) -> int:
    count, current = selected_route_card_export_current(root, packet_id)
    if not current:
        raise ValueError(
            f"AppliedLore route-card export is stale for packet_id {packet_id}: {route_card_output_path(root)}"
        )
    return count


def check_route_card_export(root: Path) -> int:
    count, current = route_card_export_current(root)
    if not current:
        raise ValueError(f"AppliedLore route-card export is stale: {route_card_output_path(root)}")
    return count


def export_route_cards(root: Path, *, dry_run: bool = False) -> int:
    count, text = render_route_card_export(root)
    output_path = route_card_output_path(root)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    if not dry_run and (not output_path.exists() or output_path.read_text(encoding="utf-8") != text):
        output_path.write_text(text, encoding="utf-8", newline="")

    return count


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=".", help="Repository root.")
    parser.add_argument("--check", action="store_true", help="Fail if the generated route-card export is stale.")
    parser.add_argument("--dry-run", action="store_true", help="Validate and compare without writing the export.")
    parser.add_argument(
        "--packet-id",
        help="Validate and compare only the route-card source/export row that owns this packet.",
    )
    args = parser.parse_args()
    root = Path(args.root).resolve()
    try:
        if args.packet_id:
            if args.check:
                count = check_selected_route_card_export(root, args.packet_id)
                print(f"applied_lore_route_cards_selected={count} packet_id={args.packet_id} export_current=1")
                return 0
            count, current = selected_route_card_export_current(root, args.packet_id)
            print(
                f"applied_lore_route_cards_selected={count} packet_id={args.packet_id} "
                f"export_current={int(current)} would_write=0"
            )
            return 0
        if args.check:
            count = check_route_card_export(root)
            print(f"applied_lore_route_cards={count} export_current=1")
            return 0
        if args.dry_run:
            count, current = route_card_export_current(root)
            print(f"applied_lore_route_cards={count} export_current={int(current)} would_write={int(not current)}")
            return 0

        count = export_route_cards(root)
        print(f"applied_lore_route_cards={count}")
        return 0
    except ValueError as exc:
        print("AppliedLore route-card export failed:")
        print(exc)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
