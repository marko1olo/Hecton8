#!/usr/bin/env python3
"""Import AppliedContent packet JSON into Data Monolith authoring tables."""

from __future__ import annotations

import argparse
import csv
import io
import json
import re
from pathlib import Path
from typing import Any


TARGET_LOCALES = (
    "en_US",
    "ru_RU",
    "ja_JP",
    "zh_CN",
    "fr_FR",
    "es_ES",
    "de_DE",
    "pl_PL",
    "uk_UA",
    "ar_SA",
    "id_ID",
    "ko_KR",
    "he_IL",
    "pt_BR",
    "nl_NL",
)

CSV_HEADERS = (
    "packet_id",
    "locale",
    "release_set_id",
    "article_id",
    "unlock_id",
    "surface_mask",
    "title",
    "scanner",
    "terminal",
    "audio",
    "in_game_wiki",
    "external_site",
    "field_note",
    "poi_tags",
    "biome_tags",
    "flags",
)

FNV_OFFSET = 2166136261
FNV_PRIME = 16777619
ROW_FLAG_DRAFT_LOCALIZATION = 1 << 0
SURFACE_MASKS = {
    "title": 1 << 0,
    "scanner": 1 << 1,
    "terminal": 1 << 2,
    "audio": 1 << 3,
    "audio_subtitle": 1 << 3,
    "in_game_wiki": 1 << 4,
    "external_site": 1 << 5,
    "field_note": 1 << 6,
}
DEFAULT_SURFACE_MASK = (
    SURFACE_MASKS["title"]
    | SURFACE_MASKS["scanner"]
    | SURFACE_MASKS["terminal"]
    | SURFACE_MASKS["audio"]
    | SURFACE_MASKS["in_game_wiki"]
    | SURFACE_MASKS["external_site"]
    | SURFACE_MASKS["field_note"]
)

LOCALIZED_TEXT_FIELDS = (
    "title",
    "scanner",
    "terminal",
    "audio",
    "in_game_wiki",
    "external_site",
    "field_note",
)

DRAFT_LOCALIZATION_PREFIX = re.compile(
    r"^Draft\s+[A-Z]{2}(?:-[A-Z]{2})?\s+localization pending native pass\.\s*",
    re.ASCII,
)
LOCALIZED_DRAFT_PREFIXES = (
    "Draf ID menunggu native review.",
    "NL-concept wacht op native review.",
)


def fnv1a32(value: str) -> int:
    if not value:
        return 0

    hash_value = FNV_OFFSET
    for char in value:
        code = ord(char)
        if 65 <= code <= 90:
            code += 32
        hash_value ^= code & 0xFF
        hash_value = (hash_value * FNV_PRIME) & 0xFFFFFFFF
    return hash_value or 1


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as handle:
        data = json.load(handle)
    if not isinstance(data, dict):
        raise ValueError(f"JSON root must be object: {path}")
    return data


def sanitize_constant_name(value: str) -> str:
    safe = re.sub(r"[^0-9A-Za-z_]+", "_", value).strip("_")
    if not safe:
        return "EMPTY"
    if safe[0].isdigit():
        safe = "_" + safe
    return safe


def iter_manifest_paths(root: Path) -> list[Path]:
    release_dir = root / "Docs" / "Lore" / "AppliedContent" / "release_sets"
    return sorted(release_dir.glob("*_manifest.json"), key=lambda path: path.name.lower())


def resolve_source(root: Path, source: str) -> Path:
    path = Path(source)
    if path.is_absolute():
        return path
    return root / path


def collect_packets(root: Path) -> list[dict[str, Any]]:
    packets_by_id: dict[str, dict[str, Any]] = {}
    manifest_packet_ids: set[str] = set()

    for manifest_path in iter_manifest_paths(root):
        manifest = load_json(manifest_path)
        if manifest.get("canonical_importer_ready") is False:
            continue
        release_set_id = str(manifest.get("release_set_id", ""))
        packet_sources = manifest.get("packet_sources", [])
        if not packet_sources:
            if manifest.get("canonical_importer_ready") is True:
                packet_sources = manifest.get("canonical_importer_sources", [])
            else:
                # Authoring-only manifests can list STATIC_DOC production packets.
                # They are not canonical CSV/export packets until packet_sources exist.
                continue

        for packet_id in manifest.get("packets", []):
            manifest_packet_ids.add(str(packet_id))

        for source in packet_sources:
            source_path = resolve_source(root, str(source))
            packet_source = load_json(source_path)
            if "packets" in packet_source:
                for packet in packet_source["packets"]:
                    if not isinstance(packet, dict):
                        raise ValueError(f"Packet bundle contains non-object packet: {source_path}")
                    packet = dict(packet)
                    packet.setdefault("release_set_id", release_set_id)
                    add_packet(packets_by_id, packet, source_path)
            else:
                packet = dict(packet_source)
                packet.setdefault("release_set_id", release_set_id)
                add_packet(packets_by_id, packet, source_path)

    missing = sorted(manifest_packet_ids.difference(packets_by_id.keys()))
    if missing:
        raise ValueError("Manifest packet ids missing from sources: " + ", ".join(missing))

    return [packets_by_id[key] for key in sorted(packets_by_id)]


def add_packet(packets_by_id: dict[str, dict[str, Any]], packet: dict[str, Any], source_path: Path) -> None:
    packet_id = str(packet.get("packet_id", ""))
    if not packet_id:
        raise ValueError(f"Packet without packet_id: {source_path}")

    existing = packets_by_id.get(packet_id)
    if existing is not None:
        raise ValueError(f"Duplicate packet_id {packet_id}: {source_path}")

    packet["_source_path"] = str(source_path)
    packets_by_id[packet_id] = packet


def require_text(localized: dict[str, Any], field: str, packet_id: str, locale: str) -> str:
    value = localized.get(field, "")
    if not isinstance(value, str) or not value:
        raise ValueError(f"Missing localized field {field}: packet={packet_id} locale={locale}")
    value = sanitize_localized_text(value)
    if not value:
        raise ValueError(f"Localized field stripped to empty {field}: packet={packet_id} locale={locale}")
    return value


def optional_text(localized: dict[str, Any], field: str) -> str:
    value = localized.get(field, "")
    return sanitize_localized_text(value) if isinstance(value, str) else ""


def split_localization_status(value: str) -> tuple[str, bool]:
    text = value.strip()
    draft = False
    while text:
        match = DRAFT_LOCALIZATION_PREFIX.match(text)
        if match is not None:
            draft = True
            text = text[match.end():].lstrip()
            continue

        stripped = False
        for prefix in LOCALIZED_DRAFT_PREFIXES:
            if text.startswith(prefix):
                draft = True
                text = text[len(prefix):].lstrip()
                stripped = True
                break

        if not stripped:
            break

    return text, draft


def sanitize_localized_text(value: str) -> str:
    text, _draft = split_localization_status(value)
    return text


def localized_row_flags(localized: dict[str, Any]) -> int:
    flags = 0
    for field in LOCALIZED_TEXT_FIELDS:
        value = localized.get(field, "")
        if isinstance(value, str):
            _text, draft = split_localization_status(value)
            if draft:
                flags |= ROW_FLAG_DRAFT_LOCALIZATION
                break
    return flags


def first_list(value: Any, limit: int = 2) -> list[str]:
    if not isinstance(value, list):
        return []
    return [str(item) for item in value[:limit] if str(item)]


def resolve_packet_surface_mask(packet: dict[str, Any]) -> int:
    """Resolve runtime-visible surfaces from packet authoring metadata.

    Title is always included so field-note-only production cards still have a
    readable PDA/terminal label without exposing scanner/wiki/site prose.
    """

    explicit_mask = packet.get("surface_mask")
    if explicit_mask is not None:
        try:
            mask = int(explicit_mask, 0) if isinstance(explicit_mask, str) else int(explicit_mask)
        except (TypeError, ValueError) as exc:
            packet_id = str(packet.get("packet_id", ""))
            raise ValueError(f"Bad surface_mask for packet {packet_id}: {explicit_mask!r}") from exc
        return mask | SURFACE_MASKS["title"]

    surfaces = packet.get("surfaces")
    if isinstance(surfaces, list):
        mask = SURFACE_MASKS["title"]
        for surface in surfaces:
            bit = SURFACE_MASKS.get(str(surface))
            if bit is not None:
                mask |= bit
        return mask

    return DEFAULT_SURFACE_MASK


def packet_rows(packets: list[dict[str, Any]]) -> list[dict[str, str]]:
    rows: list[dict[str, str]] = []
    for packet in packets:
        packet_id = str(packet["packet_id"])
        release_set_id = str(packet.get("release_set_id", ""))
        article_id = str(packet.get("article_id", ""))
        unlock = packet.get("unlock", {})
        if not isinstance(unlock, dict):
            unlock = {}

        unlock_id = str(unlock.get("primary", ""))
        poi_tags = ";".join(first_list(unlock.get("poi_tags")))
        biome_tags = ";".join(first_list(unlock.get("biome_tags")))
        localized_by_locale = packet.get("localized", {})
        if not isinstance(localized_by_locale, dict):
            raise ValueError(f"Packet localized must be object: {packet_id}")

        surface_mask = str(resolve_packet_surface_mask(packet))
        for locale in TARGET_LOCALES:
            localized = localized_by_locale.get(locale)
            if not isinstance(localized, dict):
                raise ValueError(f"Missing locale: packet={packet_id} locale={locale}")

            flags = localized_row_flags(localized)
            rows.append(
                {
                    "packet_id": packet_id,
                    "locale": locale,
                    "release_set_id": release_set_id,
                    "article_id": article_id,
                    "unlock_id": unlock_id,
                    "surface_mask": surface_mask,
                    "title": require_text(localized, "title", packet_id, locale),
                    "scanner": require_text(localized, "scanner", packet_id, locale),
                    "terminal": require_text(localized, "terminal", packet_id, locale),
                    "audio": require_text(localized, "audio", packet_id, locale),
                    "in_game_wiki": require_text(localized, "in_game_wiki", packet_id, locale),
                    "external_site": require_text(localized, "external_site", packet_id, locale),
                    "field_note": optional_text(localized, "field_note"),
                    "poi_tags": poi_tags,
                    "biome_tags": biome_tags,
                    "flags": str(flags),
                }
            )
    return rows


def write_text_if_changed(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists() and path.read_text(encoding="utf-8") == text:
        return

    path.write_text(text, encoding="utf-8", newline="")


def write_csv(path: Path, rows: list[dict[str, str]]) -> None:
    buffer = io.StringIO(newline="")
    writer = csv.DictWriter(buffer, fieldnames=CSV_HEADERS, lineterminator="\n")
    writer.writeheader()
    writer.writerows(rows)
    write_text_if_changed(path, buffer.getvalue())


def write_hash_constants(path: Path, packets: list[dict[str, Any]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    lines: list[str] = [
        "// <auto-generated>",
        "// Source: Tools/AppliedLoreImporter.py",
        "// Runtime contract: constants only; no runtime content hashing in hot paths.",
        "// </auto-generated>",
        "namespace Hecton8.Core.Generated",
        "{",
        "    public static class H8AppliedLoreHashes",
        "    {",
    ]

    for packet in packets:
        packet_id = str(packet["packet_id"])
        lines.append(f"        public const uint {sanitize_constant_name(packet_id)} = 0x{fnv1a32(packet_id):08X}u;")

    lines.append("")
    for locale in TARGET_LOCALES:
        lines.append(f"        public const uint Locale_{sanitize_constant_name(locale)} = 0x{fnv1a32(locale):08X}u;")

    lines.extend(
        [
            "",
            "        public const uint SurfaceMaskTitle = 1u << 0;",
            "        public const uint SurfaceMaskScanner = 1u << 1;",
            "        public const uint SurfaceMaskTerminal = 1u << 2;",
            "        public const uint SurfaceMaskAudio = 1u << 3;",
            "        public const uint SurfaceMaskInGameWiki = 1u << 4;",
            "        public const uint SurfaceMaskExternalSite = 1u << 5;",
            "        public const uint SurfaceMaskFieldNote = 1u << 6;",
            "",
            "        public const uint RowFlagDraftLocalization = 1u << 0;",
            "    }",
            "}",
            "",
        ]
    )
    write_text_if_changed(path, "\n".join(lines))


def import_applied_lore(root: Path) -> tuple[int, int, int]:
    packets = collect_packets(root)
    rows = packet_rows(packets)
    write_csv(root / "Assets" / "_SourceData" / "DataMonolith" / "Narrative" / "applied_lore_packets.csv", rows)
    write_hash_constants(root / "Assets" / "_Project" / "Scripts" / "Core" / "Generated" / "H8AppliedLoreHashes.cs", packets)
    draft_rows = 0
    for row in rows:
        if int(row["flags"]) & ROW_FLAG_DRAFT_LOCALIZATION:
            draft_rows += 1
    return len(packets), len(rows), draft_rows


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=".", help="Repository root.")
    args = parser.parse_args()
    root = Path(args.root).resolve()
    packet_count, row_count, draft_rows = import_applied_lore(root)
    print(f"applied_lore_packets={packet_count} localized_rows={row_count} draft_localization_rows={draft_rows}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
