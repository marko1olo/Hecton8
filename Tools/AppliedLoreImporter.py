#!/usr/bin/env python3
"""Import AppliedContent packet JSON into Data Monolith authoring tables."""

from __future__ import annotations

import argparse
import csv
import fnmatch
import io
import json
import re
import sys
from dataclasses import dataclass
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
    r"^Draft\s+[A-Za-z]{2}(?:[-_][A-Za-z]{2})?\s+localization pending native pass\.\s*",
    re.ASCII,
)
LOCALIZED_DRAFT_PREFIXES = (
    "Draf ID menunggu native review.",
    "NL-concept wacht op native review.",
)


def safe_console_line(text: str, encoding: str | None = None) -> str:
    encoding = encoding or sys.stdout.encoding or "utf-8"
    try:
        text.encode(encoding)
        return text
    except UnicodeEncodeError:
        return text.encode(encoding, errors="backslashreplace").decode(encoding, errors="replace")


@dataclass(frozen=True)
class ImportCheckStats:
    checked_files: int
    stale_files: int
    missing_files: int
    sample_issues: tuple[str, ...]


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


def packet_matches_glob(packet_id: str, packet_glob: str) -> bool:
    if not packet_glob:
        return True

    return any(fnmatch.fnmatch(packet_id, glob.strip()) for glob in packet_glob.split(",") if glob.strip())


def collect_packets(root: Path, packet_glob: str = "") -> list[dict[str, Any]]:
    packets_by_id: dict[str, dict[str, Any]] = {}
    targeted = bool(packet_glob.strip())

    for manifest_path in iter_manifest_paths(root):
        manifest = load_json(manifest_path)
        if manifest.get("canonical_importer_ready") is False:
            continue
        release_set_id = str(manifest.get("release_set_id", ""))
        manifest_status = str(manifest.get("status", ""))
        packet_sources = manifest.get("packet_sources", [])
        if not packet_sources:
            if manifest.get("canonical_importer_ready") is True:
                packet_sources = manifest.get("canonical_importer_sources", [])
            else:
                # Authoring-only manifests can list STATIC_DOC production packets.
                # They are not canonical CSV/export packets until packet_sources exist.
                continue

        raw_manifest_packet_ids = manifest.get("packets", [])
        if not isinstance(raw_manifest_packet_ids, list) or not raw_manifest_packet_ids:
            if targeted:
                continue
            raise ValueError(f"Manifest packets must list source packet ids: {manifest_path}")

        manifest_packet_id_list = [str(packet_id).strip() for packet_id in raw_manifest_packet_ids if str(packet_id).strip()]
        if targeted:
            manifest_packet_id_list = [packet_id for packet_id in manifest_packet_id_list if packet_matches_glob(packet_id, packet_glob)]
            if not manifest_packet_id_list:
                continue

        manifest_packet_ids = set(manifest_packet_id_list)
        duplicated_manifest_ids = sorted(
            {packet_id for packet_id in manifest_packet_id_list if manifest_packet_id_list.count(packet_id) > 1}
        )
        if duplicated_manifest_ids:
            raise ValueError(f"{manifest_path}: Duplicate manifest packet ids: " + ", ".join(duplicated_manifest_ids))
        source_packet_ids: set[str] = set()

        for source in packet_sources:
            source_path = resolve_source(root, str(source))
            packet_source = load_json(source_path)
            bundle_status = str(packet_source.get("status", ""))
            if "packets" in packet_source:
                for packet in packet_source["packets"]:
                    if not isinstance(packet, dict):
                        raise ValueError(f"Packet bundle contains non-object packet: {source_path}")
                    if targeted and not packet_matches_glob(str(packet.get("packet_id", "")), packet_glob):
                        continue
                    packet = dict(packet)
                    packet.setdefault("release_set_id", release_set_id)
                    packet.setdefault("_bundle_status", bundle_status)
                    packet.setdefault("_manifest_status", manifest_status)
                    source_packet_ids.add(add_packet(packets_by_id, packet, source_path))
            else:
                if targeted and not packet_matches_glob(str(packet_source.get("packet_id", "")), packet_glob):
                    continue
                packet = dict(packet_source)
                packet.setdefault("release_set_id", release_set_id)
                packet.setdefault("_bundle_status", bundle_status)
                packet.setdefault("_manifest_status", manifest_status)
                source_packet_ids.add(add_packet(packets_by_id, packet, source_path))

        missing = sorted(manifest_packet_ids.difference(source_packet_ids))
        if missing:
            raise ValueError(f"{manifest_path}: Manifest packet ids missing from sources: " + ", ".join(missing))

        extra = sorted(source_packet_ids.difference(manifest_packet_ids))
        if extra:
            raise ValueError(f"{manifest_path}: Source packet ids not listed in manifest packets: " + ", ".join(extra))

    return [packets_by_id[key] for key in sorted(packets_by_id)]


def add_packet(packets_by_id: dict[str, dict[str, Any]], packet: dict[str, Any], source_path: Path) -> str:
    packet_id = str(packet.get("packet_id", ""))
    if not packet_id:
        raise ValueError(f"Packet without packet_id: {source_path}")

    existing = packets_by_id.get(packet_id)
    if existing is not None:
        raise ValueError(f"Duplicate packet_id {packet_id}: {source_path}")

    packet["_source_path"] = str(source_path)
    packets_by_id[packet_id] = packet
    return packet_id


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


AUDIO_SURFACE_NAMES = ("audio", "audio_subtitle", "audio_transcript")

AUTHORITY_LOCALE = "en_US"


def row_is_blocked_translation(localized: dict[str, Any]) -> bool:
    """True for a locale row that honestly declares it has no usable draft yet.

    writing.md and localization.md both list BLOCKED_TRANSLATION_DRAFT as a valid production status and
    require it INSTEAD of a fabricated translation or a copy of the English row. Such a row carries a
    blocker rather than text, so the bake needs a fallback instead of a hard failure.
    """
    status = str(localized.get("localization_status", "")).strip().upper()
    return status == "BLOCKED_TRANSLATION_DRAFT"


def packet_declares_audio_surface(packet: dict[str, Any]) -> bool:
    """True when the packet publishes to an audio surface.

    A packet with no speaker - a coral, a kelp stand, a bottom feeder - has no audio surface in its
    `surfaces` list, and requiring an audio row from it would mean fabricating a spoken line to fill a
    column. Packets that omit `surfaces` entirely keep the old strict behaviour, so nothing already in the
    corpus loosens.
    """
    surfaces = packet.get("surfaces")
    if not isinstance(surfaces, list) or not surfaces:
        return True
    return any(str(s).strip().lower() in AUDIO_SURFACE_NAMES for s in surfaces)


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


def status_marks_draft(value: Any) -> bool:
    if not isinstance(value, str):
        return False
    text = value.strip().lower()
    if not text:
        return False
    return "draft" in text or "pending_native" in text or "native_review" in text


def localized_row_flags(localized: dict[str, Any], locale: str = "", packet: dict[str, Any] | None = None) -> int:
    flags = 0
    for field in LOCALIZED_TEXT_FIELDS:
        value = localized.get(field, "")
        if isinstance(value, str):
            _text, draft = split_localization_status(value)
            if draft:
                flags |= ROW_FLAG_DRAFT_LOCALIZATION
                break
    if flags == 0 and (
        status_marks_draft(localized.get("localization_status")) or
        status_marks_draft(localized.get("status"))
    ):
        flags |= ROW_FLAG_DRAFT_LOCALIZATION
    if flags == 0 and packet is not None and locale != "en_US":
        if (
            status_marks_draft(packet.get("localization_status")) or
            status_marks_draft(packet.get("status")) or
            status_marks_draft(packet.get("_bundle_status")) or
            status_marks_draft(packet.get("_manifest_status"))
        ):
            flags |= ROW_FLAG_DRAFT_LOCALIZATION
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
        authority_row = localized_by_locale.get(AUTHORITY_LOCALE)
        for locale in TARGET_LOCALES:
            localized = localized_by_locale.get(locale)
            if not isinstance(localized, dict):
                raise ValueError(f"Missing locale: packet={packet_id} locale={locale}")

            flags = localized_row_flags(localized, locale, packet)
            # A row that honestly declares BLOCKED_TRANSLATION_DRAFT carries a blocker instead of text.
            # writing.md and localization.md both list that status as valid and REQUIRE it rather than a
            # fabricated or copied translation, but this importer demanded text in all 15 locales, so
            # law-compliant authoring could not be baked at all. Bake the authority row's text as the
            # runtime fallback and keep the draft flag, which is what localization.md's "explicit fallback
            # language and missing-string display policy" asks for. The authoring source stays honest: it
            # still says BLOCKED_TRANSLATION_DRAFT with its blocker.
            if locale != AUTHORITY_LOCALE and row_is_blocked_translation(localized) and isinstance(authority_row, dict):
                localized = {**authority_row, **{k: v for k, v in localized.items() if v}}
                flags |= ROW_FLAG_DRAFT_LOCALIZATION
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
                    # Audio is required only when the packet actually declares an audio surface. Demanding it
                    # unconditionally forced a spoken line onto entries that have no speaker - a coral, a
                    # kelp stand, a silt feeder - which is inventing content to satisfy a column. The
                    # surface list is the packet's own declaration of which surfaces it publishes to, and
                    # surface_mask already carries that to the runtime.
                    "audio": (
                        require_text(localized, "audio", packet_id, locale)
                        if packet_declares_audio_surface(packet)
                        else optional_text(localized, "audio")
                    ),
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


def render_csv(rows: list[dict[str, str]]) -> str:
    buffer = io.StringIO(newline="")
    writer = csv.DictWriter(buffer, fieldnames=CSV_HEADERS, lineterminator="\n")
    writer.writeheader()
    writer.writerows(rows)
    return buffer.getvalue()


def write_csv(path: Path, rows: list[dict[str, str]]) -> None:
    write_text_if_changed(path, render_csv(rows))


def render_hash_constants(packets: list[dict[str, Any]]) -> str:
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
    return "\n".join(lines)


def write_hash_constants(path: Path, packets: list[dict[str, Any]]) -> None:
    write_text_if_changed(path, render_hash_constants(packets))


def compact_issue_value(value: str, limit: int = 160) -> str:
    compacted = value.replace("\r", "\\r").replace("\n", "\\n")
    if len(compacted) <= limit:
        return compacted
    return compacted[:limit] + "..."


def describe_applied_lore_csv_diff(relative: str, expected_text: str, current_text: str) -> list[str]:
    expected_rows = list(csv.DictReader(io.StringIO(expected_text)))
    current_rows = list(csv.DictReader(io.StringIO(current_text)))

    for row_number, (expected_row, current_row) in enumerate(zip(expected_rows, current_rows), start=2):
        if expected_row == current_row:
            continue

        details = [
            (
                f"first_diff: {relative}: row={row_number} "
                f"expected_packet={expected_row.get('packet_id', '')} "
                f"expected_locale={expected_row.get('locale', '')} "
                f"current_packet={current_row.get('packet_id', '')} "
                f"current_locale={current_row.get('locale', '')}"
            )
        ]
        for header in CSV_HEADERS:
            expected_value = expected_row.get(header, "")
            current_value = current_row.get(header, "")
            if expected_value != current_value:
                details.append(
                    (
                        f"first_diff_field: {relative}: field={header} "
                        f"expected={compact_issue_value(expected_value)} "
                        f"current={compact_issue_value(current_value)}"
                    )
                )
                break
        return details

    if len(expected_rows) != len(current_rows):
        return [
            (
                f"first_diff: {relative}: row_count "
                f"expected={len(expected_rows)} current={len(current_rows)}"
            )
        ]

    return [f"first_diff: {relative}: text differs outside parsed CSV rows"]


def data_monolith_csv_path(root: Path) -> Path:
    return root / "Assets" / "_SourceData" / "DataMonolith" / "Narrative" / "applied_lore_packets.csv"


def hash_constants_path(root: Path) -> Path:
    return root / "Assets" / "_Project" / "Scripts" / "Core" / "Generated" / "H8AppliedLoreHashes.cs"


def compare_expected_file(
    root: Path,
    path: Path,
    rendered: str,
    issues: list[str],
    counts: dict[str, int],
) -> None:
    counts["checked"] += 1
    relative = path.relative_to(root).as_posix()
    if not path.exists():
        counts["missing"] += 1
        issues.append(f"missing: {relative}")
        return

    current_text = path.read_text(encoding="utf-8")
    if current_text != rendered:
        counts["stale"] += 1
        issues.append(f"stale: {relative}")
        if path.name == "applied_lore_packets.csv":
            issues.extend(describe_applied_lore_csv_diff(relative, rendered, current_text))


def check_import_outputs(root: Path) -> ImportCheckStats:
    packets = collect_packets(root)
    rows = packet_rows(packets)
    counts = {"checked": 0, "stale": 0, "missing": 0}
    issues: list[str] = []

    compare_expected_file(root, data_monolith_csv_path(root), render_csv(rows), issues, counts)
    compare_expected_file(root, hash_constants_path(root), render_hash_constants(packets), issues, counts)
    return ImportCheckStats(
        checked_files=counts["checked"],
        stale_files=counts["stale"],
        missing_files=counts["missing"],
        sample_issues=tuple(issues[:20]),
    )


def import_applied_lore(root: Path) -> tuple[int, int, int]:
    packets = collect_packets(root)
    rows = packet_rows(packets)
    write_csv(data_monolith_csv_path(root), rows)
    write_hash_constants(hash_constants_path(root), packets)
    draft_rows = 0
    for row in rows:
        if int(row["flags"]) & ROW_FLAG_DRAFT_LOCALIZATION:
            draft_rows += 1
    return len(packets), len(rows), draft_rows


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=".", help="Repository root.")
    parser.add_argument("--check", action="store_true", help="Fail if generated import outputs are stale or missing.")
    args = parser.parse_args()
    root = Path(args.root).resolve()
    if args.check:
        stats = check_import_outputs(root)
        print(
            f"applied_lore_import_check checked_files={stats.checked_files} "
            f"stale_files={stats.stale_files} missing_files={stats.missing_files}"
        )
        if stats.sample_issues:
            print("sample_issues:")
            for issue in stats.sample_issues:
                print(safe_console_line(f"  {issue}"))
        if stats.stale_files or stats.missing_files:
            return 1
        return 0

    packet_count, row_count, draft_rows = import_applied_lore(root)
    print(f"applied_lore_packets={packet_count} localized_rows={row_count} draft_localization_rows={draft_rows}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
