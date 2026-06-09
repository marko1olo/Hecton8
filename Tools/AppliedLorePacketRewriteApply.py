#!/usr/bin/env python3
"""Apply a localized AppliedLore packet rewrite map to a packet JSON file."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path


DEFAULT_FIELDS = ("title", "scanner", "field_note", "terminal", "audio", "in_game_wiki", "external_site")


def configure_console_encoding() -> None:
    for stream in (sys.stdout, sys.stderr):
        reconfigure = getattr(stream, "reconfigure", None)
        if reconfigure is None:
            continue
        try:
            reconfigure(encoding="utf-8", errors="replace")
        except (OSError, ValueError):
            continue


def fail(message: str) -> None:
    raise SystemExit(f"AppliedLore packet rewrite failed: {message}")


def load_json(path: Path) -> object:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        fail(f"{path}: bad JSON at line {exc.lineno} column {exc.colno}: {exc.msg}")


def validate_update_shape(updates: object, fields: tuple[str, ...]) -> dict[str, dict[str, dict[str, str]]]:
    if not isinstance(updates, dict):
        fail("update map root must be an object keyed by packet_id")

    normalized: dict[str, dict[str, dict[str, str]]] = {}
    for packet_id, locales in updates.items():
        if not isinstance(packet_id, str) or not packet_id:
            fail("update map contains an empty or non-string packet_id")
        if not isinstance(locales, dict):
            fail(f"{packet_id}: locales must be an object")

        normalized_locales: dict[str, dict[str, str]] = {}
        for locale, values in locales.items():
            if not isinstance(locale, str) or not locale:
                fail(f"{packet_id}: locale key must be a non-empty string")
            if not isinstance(values, dict):
                fail(f"{packet_id}/{locale}: value must be an object")

            missing = [field for field in fields if field not in values]
            if missing:
                fail(f"{packet_id}/{locale}: missing fields: {', '.join(missing)}")

            normalized_values: dict[str, str] = {}
            for field in fields:
                value = values[field]
                if not isinstance(value, str) or value == "":
                    fail(f"{packet_id}/{locale}/{field}: value must be non-empty text")
                normalized_values[field] = value
            normalized_locales[locale] = normalized_values
        normalized[packet_id] = normalized_locales
    return normalized


def apply_rewrites(packet_path: Path, update_path: Path, fields: tuple[str, ...]) -> tuple[int, int, int]:
    packet_data = load_json(packet_path)
    updates = validate_update_shape(load_json(update_path), fields)

    if not isinstance(packet_data, dict) or not isinstance(packet_data.get("packets"), list):
        fail(f"{packet_path}: expected packet bundle object with packets array")

    packets_by_id = {
        packet.get("packet_id"): packet
        for packet in packet_data["packets"]
        if isinstance(packet, dict) and isinstance(packet.get("packet_id"), str)
    }

    changed = 0
    locale_rows = 0
    field_writes = 0
    for packet_id, locales in updates.items():
        packet = packets_by_id.get(packet_id)
        if packet is None:
            fail(f"{packet_path}: packet not found: {packet_id}")
        localized = packet.get("localized")
        if not isinstance(localized, dict):
            fail(f"{packet_id}: localized must be an object")

        for locale, values in locales.items():
            row = localized.get(locale)
            if not isinstance(row, dict):
                fail(f"{packet_id}: locale not found or not object: {locale}")
            locale_rows += 1
            for field in fields:
                if row.get(field) != values[field]:
                    row[field] = values[field]
                    changed += 1
                field_writes += 1

    if changed:
        packet_path.write_text(json.dumps(packet_data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")

    return changed, locale_rows, field_writes


def main() -> int:
    configure_console_encoding()
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--packet-file", required=True, help="AppliedContent *.packets.json file to update.")
    parser.add_argument("--rewrite-map", required=True, help="JSON map keyed by packet_id, then locale, then localized fields.")
    parser.add_argument("--fields", default=",".join(DEFAULT_FIELDS), help="Comma-separated localized fields required and applied.")
    args = parser.parse_args()

    fields = tuple(field.strip() for field in args.fields.split(",") if field.strip())
    if not fields:
        fail("--fields resolved to an empty field list")

    changed, locale_rows, field_writes = apply_rewrites(Path(args.packet_file), Path(args.rewrite_map), fields)
    print(f"applied_lore_packet_rewrite changed_fields={changed} locale_rows={locale_rows} field_writes={field_writes}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
