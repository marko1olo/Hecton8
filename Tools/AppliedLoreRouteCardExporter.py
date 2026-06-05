#!/usr/bin/env python3
"""Export checked AppliedLore route cards into DataMonolith source data."""

from __future__ import annotations

import argparse
import csv
import io
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


def require_cell(row: dict[str, str], field: str, line_number: int) -> str:
    value = row.get(field, "")
    if value == "":
        raise ValueError(f"Route card line {line_number}: missing {field}")
    return value


def packet_refs(value: str) -> list[str]:
    return [part.strip() for part in value.split(";") if part.strip()]


def hash_list(values: list[str], *, hex_format: bool) -> str:
    if hex_format:
        return ";".join(f"0x{fnv1a32(value):08X}" for value in values)
    return ";".join(str(fnv1a32(value)) for value in values)


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
    route_dir = root / "Docs" / "Lore" / "AppliedContent" / "route_cards"
    return sorted(route_dir.glob("*_route_cards.csv"), key=lambda path: path.name.lower())


def export_route_cards(root: Path) -> int:
    output_path = root / "Assets" / "_SourceData" / "DataMonolith" / "Narrative" / "applied_lore_route_cards.csv"
    input_paths = iter_route_card_sources(root)
    if not input_paths:
        raise ValueError("Missing route-card source CSV files.")

    known_packets = baked_packet_ids(root)
    output_rows: list[dict[str, str]] = []
    seen_routes: set[str] = set()
    packet_owner_by_id: dict[str, str] = {}
    for input_path in input_paths:
        with input_path.open("r", encoding="utf-8", newline="") as handle:
            reader = csv.DictReader(handle)
            if tuple(reader.fieldnames or ()) != INPUT_HEADERS:
                raise ValueError(f"Route-card source header mismatch: {input_path}")

            for line_number, row in enumerate(reader, start=2):
                route_card_id = require_cell(row, "route_card_id", line_number)
                if route_card_id in seen_routes:
                    raise ValueError(f"Duplicate route_card_id {route_card_id}: {input_path}:{line_number}")
                seen_routes.add(route_card_id)

                phase_id = require_cell(row, "phase_id", line_number)
                primary_surface = require_cell(row, "primary_surface", line_number)
                ending_pressure = require_cell(row, "ending_pressure", line_number)
                packets = packet_refs(require_cell(row, "packet_ids", line_number))
                required = packet_refs(row.get("required_packet_ids", ""))

                if primary_surface not in SURFACE_MASKS:
                    raise ValueError(f"Route card line {line_number}: unsupported primary_surface={primary_surface}")
                for packet_id in packets + required:
                    if packet_id not in known_packets:
                        raise ValueError(f"Route card line {line_number}: unknown packet id {packet_id}")
                for packet_id in packets:
                    owner = packet_owner_by_id.get(packet_id)
                    if owner is not None:
                        raise ValueError(
                            f"Route card line {line_number}: packet_id {packet_id} already owned by {owner}"
                        )
                    packet_owner_by_id[packet_id] = route_card_id

                route_hash = fnv1a32(route_card_id)
                phase_hash = fnv1a32(phase_id)
                pressure_hash = fnv1a32(ending_pressure)
                output_rows.append(
                    {
                        "route_card_id": route_card_id,
                        "route_card_hash_hex": f"0x{route_hash:08X}",
                        "route_card_hash_uint": str(route_hash),
                        "phase_id": phase_id,
                        "phase_hash_hex": f"0x{phase_hash:08X}",
                        "phase_hash_uint": str(phase_hash),
                        "depth_min_m": require_cell(row, "depth_min_m", line_number),
                        "depth_max_m": require_cell(row, "depth_max_m", line_number),
                        "primary_surface": primary_surface,
                        "primary_surface_mask": str(SURFACE_MASKS[primary_surface]),
                        "ending_pressure": ending_pressure,
                        "ending_pressure_hash_hex": f"0x{pressure_hash:08X}",
                        "ending_pressure_hash_uint": str(pressure_hash),
                        "packet_ids": ";".join(packets),
                        "packet_hashes_hex": hash_list(packets, hex_format=True),
                        "packet_hashes_uint": hash_list(packets, hex_format=False),
                        "required_packet_ids": ";".join(required),
                        "required_packet_hashes_hex": hash_list(required, hex_format=True),
                        "required_packet_hashes_uint": hash_list(required, hex_format=False),
                    }
                )

    buffer = io.StringIO(newline="")
    writer = csv.DictWriter(buffer, fieldnames=OUTPUT_HEADERS, lineterminator="\n")
    writer.writeheader()
    writer.writerows(output_rows)

    text = buffer.getvalue()
    output_path.parent.mkdir(parents=True, exist_ok=True)
    if not output_path.exists() or output_path.read_text(encoding="utf-8") != text:
        output_path.write_text(text, encoding="utf-8", newline="")

    return len(output_rows)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=".", help="Repository root.")
    args = parser.parse_args()
    count = export_route_cards(Path(args.root).resolve())
    print(f"applied_lore_route_cards={count}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
