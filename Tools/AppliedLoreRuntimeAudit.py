#!/usr/bin/env python3
"""Offline audit for the AppliedLore Data Monolith route.

Validates authoring CSV, generated hash constants, and the baked
static_data.h8bin without touching Unity Editor state.
"""

from __future__ import annotations

import argparse
import csv
import re
import struct
from dataclasses import dataclass
from pathlib import Path


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

PUBLICATION_INDEX_HEADERS = (
    "surface",
    "locale",
    "direction",
    "packet_id",
    "release_set_id",
    "article_id",
    "unlock_id",
    "localization_status",
    "localization_flags",
    "poi_tags",
    "biome_tags",
    "page_path",
    "title",
)
PUBLICATION_CLUSTER_INDEX_HEADERS = (
    "surface",
    "locale",
    "direction",
    "cluster_id",
    "cluster_order",
    "cluster_packet_id",
    "release_set_id",
    "article_id",
    "unlock_id",
    "spoiler_tier",
    "primary_surface",
    "prereq_packet_ids",
    "next_cluster_packet_ids",
    "localization_status",
    "localization_flags",
    "poi_tags",
    "biome_tags",
    "page_path",
    "title",
    "truth_payload",
    "player_question",
)
NAVIGATION_CLUSTER_GRAPH_PATH = "graphs/RS084_SITE_WIKI_NAVIGATION_CLUSTERS_evidence_graph.csv"

SURFACES = (
    ("title", 1 << 0, 24, 52),
    ("scanner", 1 << 1, 28, 56),
    ("terminal", 1 << 2, 32, 60),
    ("audio", 1 << 3, 36, 64),
    ("in_game_wiki", 1 << 4, 40, 68),
    ("external_site", 1 << 5, 44, 72),
    ("field_note", 1 << 6, 48, 76),
)

PLAYER_VISIBLE_TEXT_FIELDS = (
    "title",
    "scanner",
    "terminal",
    "audio",
    "in_game_wiki",
    "external_site",
    "field_note",
)

FORBIDDEN_LOCALIZATION_MARKERS = (
    "localization pending native pass",
    "Draf ID menunggu native review.",
    "NL-concept wacht op native review.",
)

FNV_OFFSET = 2166136261
FNV_PRIME = 16777619
TERMINAL_OS_RUNTIME_OBJECT_NAME = "__APPLIED_LORE_TERMINAL_OS_RUNTIME"
TERMINAL_OS_RUNTIME_SCRIPT_GUID = "0c18f0b5937ab1447ae790905fb2012b"
WORLD_SCENE_PATH = "Assets/_Project/Scenes/02_HECTON_WORLD.unity"
WORLD_SCENE_MAPMAGIC_MARKER = "MapMagic::MapMagic.Core.MapMagicObject"
WORLD_SCENE_CREST_MARKER = "Crest"
WORLD_SCENE_OCEAN_PREFABS = (
    "Assets/_Project/Prefabs/Ocean_Crest.prefab",
    "Assets/_Project/Prefabs/Hecton Ocean.prefab",
)

BLOB_MAGIC = 0x4D443848
FORMAT_VERSION = 2
HEADER_SIZE = 64
DIRECTORY_SIZE = 64
SCHEMA_HASH = 0x33313332
SECTION_TABLE_ENTRY_SIZE = 16
SECTION_ID_LOCALIZATION_UTF8 = 23
SECTION_ID_APPLIED_LORE = 27
SECTION_ID_APPLIED_LORE_ROUTES = 28
APPLIED_LORE_RECORD_SIZE = 128
APPLIED_LORE_ROUTE_RECORD_SIZE = 128
APPLIED_LORE_ROUTE_PACKET_CAPACITY = 8
APPLIED_LORE_ROUTE_PREREQUISITE_CAPACITY = 4
UINT_MAX = 0xFFFFFFFF
AUTHORING_BINDING_FIELDS = (
    "appliedLorePacketHash",
    "appliedLoreQuarterPacketHash",
    "appliedLoreHalfPacketHash",
    "appliedLoreFinalPacketHash",
    "AppliedLoreHash",
)
SCENE_BINDING_TARGET_HEADERS = (
    "packet_id",
    "packet_hash_hex",
    "packet_hash_decimal",
    "authoring_component",
    "serialized_field",
    "primary_target_candidates",
    "secondary_target_candidates",
    "unity_safe_action",
    "notes",
)
SCENE_BINDING_TARGET_FIELDS = {
    "NarrativeDiscovery": {"appliedLorePacketHash"},
    "MessageTerminal": {"appliedLorePacketHash"},
    "ScannableFragment": {
        "appliedLoreQuarterPacketHash",
        "appliedLoreHalfPacketHash",
        "appliedLoreFinalPacketHash",
    },
}
MANUAL_BINDING_POLICY_HEADERS = (
    "packet_id",
    "packet_hash_hex",
    "packet_hash_decimal",
    "manual_policy",
    "required_anchor_type",
    "approved_template_prefab",
    "authoring_component",
    "serialized_field",
    "discovery_id",
    "placement_rule",
    "reason",
)
SCENE_PLACEMENT_PLAN_HEADERS = (
    "packet_id",
    "packet_hash_hex",
    "packet_hash_decimal",
    "scene_path",
    "placement_root",
    "object_name",
    "source_prefab",
    "authoring_component",
    "serialized_field",
    "discovery_id",
    "display_name",
    "local_position",
    "local_euler",
    "local_scale",
    "depth_band",
    "zone_tag",
    "placement_note",
)
MANUAL_BINDING_POLICIES = {
    "terminal_anchor_required",
    "discovery_world_prop_required",
}
TERMINAL_ANCHOR_PREFAB = "Assets/_Project/Prefabs/Narrative/AppliedLore/PFB_AppliedLore_MessageTerminalAnchor.prefab"
TERMINAL_POLICY_PREFAB_FOLDER = "Assets/_Project/Prefabs/Narrative/AppliedLore/Terminals"
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
EVIDENCE_GRAPH_PRIMARY_SURFACES = {
    "scanner",
    "terminal",
    "in_game_wiki",
    "external_site",
    "audio",
    "field_note",
}
ROUTE_CARD_HEADERS = (
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
ROUTE_CARD_PRIMARY_SURFACES = EVIDENCE_GRAPH_PRIMARY_SURFACES
ROUTE_CARD_ENDING_PRESSURES = {
    "none",
    "material",
    "truth",
    "partial_exit",
    "material_or_partial",
}
ROUTE_CARD_SOURCE_HEADERS = (
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
ROUTE_CARD_SURFACE_MASKS = {
    "title": 1 << 0,
    "scanner": 1 << 1,
    "terminal": 1 << 2,
    "audio": 1 << 3,
    "in_game_wiki": 1 << 4,
    "external_site": 1 << 5,
    "field_note": 1 << 6,
}


class AuditFailure(Exception):
    pass


@dataclass(frozen=True)
class CsvPacketRow:
    packet_id: str
    locale: str
    release_set_id: str
    article_id: str
    unlock_id: str
    surface_mask: int
    fields: dict[str, str]
    poi_tags: tuple[str, ...]
    biome_tags: tuple[str, ...]
    flags: int
    line_number: int

    @property
    def key(self) -> tuple[int, int]:
        return fnv1a32(self.packet_id), fnv1a32(self.locale)


@dataclass(frozen=True)
class SectionEntry:
    section_id: int
    record_size: int
    count: int
    offset: int


@dataclass(frozen=True)
class AppliedLoreRecord:
    values: tuple[int, ...]
    index: int

    @property
    def key(self) -> tuple[int, int]:
        return self.values[0], self.values[1]

    def value_at(self, offset: int) -> int:
        return self.values[offset // 4]


@dataclass(frozen=True)
class RouteSourceRecord:
    route_card_id: str
    route_card_hash: int
    phase_hash: int
    depth_min_m: float
    depth_max_m: float
    primary_surface_mask: int
    ending_pressure_hash: int
    packet_hashes: tuple[int, ...]
    required_packet_hashes: tuple[int, ...]
    flags: int
    line_number: int


@dataclass(frozen=True)
class AppliedLoreRouteRecord:
    route_card_hash: int
    phase_hash: int
    depth_min_m: float
    depth_max_m: float
    primary_surface_mask: int
    ending_pressure_hash: int
    packet_count: int
    required_packet_count: int
    packet_hash_slots: tuple[int, ...]
    required_packet_hash_slots: tuple[int, ...]
    flags: int
    record_index: int
    reserved: tuple[int, ...]
    index: int


@dataclass
class SceneBindingTargetStats:
    rows: int = 0
    prefab_candidate_rows: int = 0
    auto_prefab_rows: int = 0
    manual_rows: int = 0
    existing_candidate_paths: int = 0


@dataclass
class ManualBindingPolicyStats:
    rows: int = 0
    terminal_rows: int = 0
    discovery_rows: int = 0
    template_prefab_rows: int = 0
    generated_terminal_prefab_rows: int = 0


@dataclass
class ScenePlacementPlanStats:
    rows: int = 0
    terminal_rows: int = 0
    discovery_rows: int = 0


@dataclass
class SceneTerminalOsRuntimeStats:
    rows: int = 0
    renderer_slots: int = 0
    transform_slots: int = 0
    verified_slots: int = 0


@dataclass
class SceneWorldCoreStats:
    bytes: int = 0
    roots: int = 0
    mapmagic_rows: int = 0
    terrain_rows: int = 0
    terrain_collider_rows: int = 0
    dependency_warnings: int = 0
    crest_markers: int = 0
    ocean_prefab_assets: int = 0
    ocean_prefab_scene_refs: int = 0


@dataclass
class SerializedBindingStats:
    scene_bindings: int = 0
    prefab_bindings: int = 0
    asset_bindings: int = 0

    @property
    def total(self) -> int:
        return self.scene_bindings + self.prefab_bindings + self.asset_bindings


def fail(message: str) -> None:
    raise AuditFailure(message)


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


def sanitize_constant_name(value: str) -> str:
    safe = re.sub(r"[^0-9A-Za-z_]+", "_", value).strip("_")
    if not safe:
        return "EMPTY"
    if safe[0].isdigit():
        safe = "_" + safe
    return safe


def parse_int(value: str, field: str, line_number: int) -> int:
    try:
        return int(value, 0)
    except ValueError as exc:
        raise AuditFailure(f"CSV line {line_number}: bad integer {field}={value!r}") from exc


def parse_tag_pair(value: str) -> tuple[str, ...]:
    if not value:
        return ()
    return tuple(part for part in value.split(";")[:2] if part)


def parse_packet_refs(value: str) -> tuple[str, ...]:
    if not value:
        return ()
    return tuple(part.strip() for part in value.split(";") if part.strip())


def validate_acyclic_packet_dependencies(graph: dict[str, set[str]], context: str) -> None:
    visiting: set[str] = set()
    visited: set[str] = set()
    stack: list[str] = []

    def visit(packet_id: str) -> None:
        if packet_id in visiting:
            try:
                start = stack.index(packet_id)
            except ValueError:
                start = 0
            cycle = stack[start:] + [packet_id]
            fail(f"{context} prerequisite cycle: " + " -> ".join(cycle))

        if packet_id in visited:
            return

        visiting.add(packet_id)
        stack.append(packet_id)
        for dependency in sorted(graph.get(packet_id, ())):
            visit(dependency)
        stack.pop()
        visiting.remove(packet_id)
        visited.add(packet_id)

    for packet_id in sorted(graph):
        visit(packet_id)


def iter_applied_content_csv_sources(root: Path, folder: str, pattern: str) -> list[Path]:
    base = root / "Docs" / "Lore" / "AppliedContent" / folder
    paths = sorted(base.glob(pattern), key=lambda path: path.name.lower())
    if not paths:
        fail(f"Missing AppliedLore {folder} CSV sources matching {pattern}: {base}")
    return paths


def load_csv(path: Path) -> list[CsvPacketRow]:
    if not path.exists():
        fail(f"Missing AppliedLore CSV: {path}")

    with path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        if tuple(reader.fieldnames or ()) != CSV_HEADERS:
            fail("AppliedLore CSV header mismatch.")

        rows: list[CsvPacketRow] = []
        seen: set[tuple[str, str]] = set()
        for line_number, row in enumerate(reader, start=2):
            packet_id = require_cell(row, "packet_id", line_number)
            locale = require_cell(row, "locale", line_number)
            pair = (packet_id, locale)
            if pair in seen:
                fail(f"Duplicate CSV packet/locale pair at line {line_number}: {pair}")
            seen.add(pair)

            surface_mask = parse_int(require_cell(row, "surface_mask", line_number), "surface_mask", line_number)
            flags = parse_int(require_cell(row, "flags", line_number), "flags", line_number)

            fields = {
                "title": require_cell(row, "title", line_number),
                "scanner": require_cell(row, "scanner", line_number),
                "terminal": require_cell(row, "terminal", line_number),
                "audio": require_cell(row, "audio", line_number),
                "in_game_wiki": require_cell(row, "in_game_wiki", line_number),
                "external_site": require_cell(row, "external_site", line_number),
                "field_note": row.get("field_note", ""),
            }

            rows.append(
                CsvPacketRow(
                    packet_id=packet_id,
                    locale=locale,
                    release_set_id=require_cell(row, "release_set_id", line_number),
                    article_id=require_cell(row, "article_id", line_number),
                    unlock_id=require_cell(row, "unlock_id", line_number),
                    surface_mask=surface_mask,
                    fields=fields,
                    poi_tags=parse_tag_pair(row.get("poi_tags", "")),
                    biome_tags=parse_tag_pair(row.get("biome_tags", "")),
                    flags=flags,
                    line_number=line_number,
                )
            )

    validate_locale_matrix(rows)
    return rows


def require_cell(row: dict[str, str], field: str, line_number: int) -> str:
    value = row.get(field, "")
    if value == "":
        fail(f"CSV line {line_number}: missing {field}")
    return value


def validate_locale_matrix(rows: list[CsvPacketRow]) -> None:
    by_packet: dict[str, set[str]] = {}
    for row in rows:
        by_packet.setdefault(row.packet_id, set()).add(row.locale)

    required = set(TARGET_LOCALES)
    for packet_id, locales in sorted(by_packet.items()):
        missing = sorted(required.difference(locales))
        extra = sorted(locales.difference(required))
        if missing:
            fail(f"Packet {packet_id} missing locales: {', '.join(missing)}")
        if extra:
            fail(f"Packet {packet_id} has unsupported locales: {', '.join(extra)}")


def has_forbidden_localization_marker(value: str) -> bool:
    for marker in FORBIDDEN_LOCALIZATION_MARKERS:
        if marker in value:
            return True
    return False


def validate_no_visible_localization_markers(root: Path, rows: list[CsvPacketRow]) -> tuple[int, int]:
    hits: list[str] = []
    csv_fields_scanned = 0
    pages_scanned = 0
    for row in rows:
        for field in PLAYER_VISIBLE_TEXT_FIELDS:
            csv_fields_scanned += 1
            value = row.fields.get(field, "")
            if has_forbidden_localization_marker(value):
                hits.append(f"csv:{row.packet_id}/{row.locale}/{field}")
                if len(hits) >= 12:
                    break
        if len(hits) >= 12:
            break

    publication_base = root / "Docs" / "Lore" / "AppliedContent"
    for folder in ("in_game_wiki", "external_site"):
        page_root = publication_base / folder
        if not page_root.exists():
            continue

        for path in sorted(page_root.glob("*/*.md"), key=lambda item: str(item).lower()):
            pages_scanned += 1
            try:
                text = path.read_text(encoding="utf-8")
            except UnicodeDecodeError:
                fail(f"AppliedLore publication page is not UTF-8: {path}")

            if has_forbidden_localization_marker(text):
                hits.append(f"page:{path.relative_to(root).as_posix()}")
                if len(hits) >= 12:
                    break

        if len(hits) >= 12:
            break

    if hits:
        fail("Player-visible localization draft markers leaked: " + "; ".join(hits))

    return csv_fields_scanned, pages_scanned


def localization_status_from_flags(flags: int) -> str:
    return "draft_native_pass_pending" if (flags & 1) != 0 else "source_ready"


def direction_for_locale(locale: str) -> str:
    return "rtl" if locale in {"ar_SA", "he_IL"} else "ltr"


def cluster_id_from_route_moment(route_moment: str) -> str:
    return route_moment[6:] if route_moment.startswith("first_") else route_moment


def format_tag_tuple(tags: tuple[str, ...]) -> str:
    return ";".join(tags)


def require_page_line(text: str, path: Path, expected_line: str) -> None:
    if expected_line not in text:
        fail(f"Publication page {path} missing frontmatter line: {expected_line}")


def parse_generated_constants(path: Path) -> dict[str, int]:
    if not path.exists():
        fail(f"Missing generated hash constants: {path}")

    text = path.read_text(encoding="utf-8")
    constants: dict[str, int] = {}
    hex_matches = re.finditer(r"public const uint ([A-Za-z0-9_]+) = 0x([0-9A-Fa-f]{8})u;", text)
    for match in hex_matches:
        constants[match.group(1)] = int(match.group(2), 16)

    shift_matches = re.finditer(r"public const uint ([A-Za-z0-9_]+) = 1u << ([0-9]+);", text)
    for match in shift_matches:
        constants[match.group(1)] = 1 << int(match.group(2))

    if not constants:
        fail(f"No uint constants found in {path}")
    return constants


def validate_generated_hashes(rows: list[CsvPacketRow], constants: dict[str, int]) -> None:
    packet_ids = sorted({row.packet_id for row in rows})
    for packet_id in packet_ids:
        name = sanitize_constant_name(packet_id)
        expected = fnv1a32(packet_id)
        actual = constants.get(name)
        if actual != expected:
            fail(f"Generated packet hash mismatch: {name} expected=0x{expected:08X} actual={format_hash(actual)}")

    for locale in TARGET_LOCALES:
        name = "Locale_" + sanitize_constant_name(locale)
        expected = fnv1a32(locale)
        actual = constants.get(name)
        if actual != expected:
            fail(f"Generated locale hash mismatch: {name} expected=0x{expected:08X} actual={format_hash(actual)}")

    expected_masks = {
        "SurfaceMaskTitle": 1 << 0,
        "SurfaceMaskScanner": 1 << 1,
        "SurfaceMaskTerminal": 1 << 2,
        "SurfaceMaskAudio": 1 << 3,
        "SurfaceMaskInGameWiki": 1 << 4,
        "SurfaceMaskExternalSite": 1 << 5,
        "SurfaceMaskFieldNote": 1 << 6,
    }
    for name, expected in expected_masks.items():
        actual = constants.get(name)
        if actual != expected:
            fail(f"Generated surface mask mismatch: {name} expected={expected} actual={actual}")


def format_hash(value: int | None) -> str:
    return "missing" if value is None else f"0x{value:08X}"


def read_u32(data: bytes, offset: int) -> int:
    return struct.unpack_from("<I", data, offset)[0]


def read_u16(data: bytes, offset: int) -> int:
    return struct.unpack_from("<H", data, offset)[0]


def read_f32(data: bytes, offset: int) -> float:
    return struct.unpack_from("<f", data, offset)[0]


def parse_blob(path: Path) -> tuple[bytes, dict[int, SectionEntry], SectionEntry, SectionEntry, SectionEntry]:
    if not path.exists():
        fail(f"Missing Data Monolith blob: {path}")

    data = path.read_bytes()
    if len(data) < HEADER_SIZE + DIRECTORY_SIZE:
        fail(f"Data Monolith blob too small: {len(data)} bytes")

    header_magic = read_u32(data, 0)
    header_format = read_u16(data, 4)
    header_bytes = read_u16(data, 6)
    blob_bytes = read_u32(data, 16)
    directory_offset = read_u32(data, 20)
    directory_bytes = read_u32(data, 24)
    section_table_offset = read_u32(data, 28)
    section_count = read_u32(data, 32)
    schema_hash = read_u32(data, 48)

    if header_magic != BLOB_MAGIC:
        fail(f"Bad blob magic: 0x{header_magic:08X}")
    if header_format != FORMAT_VERSION or header_bytes != HEADER_SIZE:
        fail(f"Bad blob format/header: version={header_format} header={header_bytes}")
    if blob_bytes != len(data):
        fail(f"Header blob byte count mismatch: header={blob_bytes} actual={len(data)}")
    if directory_offset != HEADER_SIZE or directory_bytes != DIRECTORY_SIZE:
        fail(f"Bad directory placement: offset={directory_offset} bytes={directory_bytes}")
    if schema_hash != SCHEMA_HASH:
        fail(f"Schema hash mismatch: 0x{schema_hash:08X}")

    directory_magic = read_u32(data, directory_offset)
    directory_format = read_u16(data, directory_offset + 4)
    directory_section_count = read_u16(data, directory_offset + 6)
    directory_table_offset = read_u32(data, directory_offset + 8)
    directory_table_bytes = read_u32(data, directory_offset + 12)
    directory_blob_bytes = read_u32(data, directory_offset + 16)
    localization_offset = read_u32(data, directory_offset + 24)
    localization_bytes = read_u32(data, directory_offset + 28)

    if directory_magic != BLOB_MAGIC or directory_format != FORMAT_VERSION:
        fail("Directory magic/version mismatch.")
    if directory_blob_bytes != len(data):
        fail(f"Directory blob byte count mismatch: directory={directory_blob_bytes} actual={len(data)}")
    if directory_section_count != section_count:
        fail(f"Header/directory section count mismatch: header={section_count} directory={directory_section_count}")
    if directory_table_offset != section_table_offset:
        fail("Header/directory section table offset mismatch.")
    if directory_table_bytes != section_count * SECTION_TABLE_ENTRY_SIZE:
        fail(f"Bad section table byte count: {directory_table_bytes}")
    if section_count < SECTION_ID_APPLIED_LORE_ROUTES:
        fail(f"Section count too small for AppliedLore route section: {section_count}")

    end_table = section_table_offset + directory_table_bytes
    if end_table > len(data):
        fail("Section table exceeds blob bounds.")

    entries: dict[int, SectionEntry] = {}
    for index in range(section_count):
        offset = section_table_offset + index * SECTION_TABLE_ENTRY_SIZE
        section_id, record_size, count, section_offset = struct.unpack_from("<IIII", data, offset)
        if section_id in entries:
            fail(f"Duplicate section id in blob: {section_id}")
        entries[section_id] = SectionEntry(section_id, record_size, count, section_offset)

    localization = require_section(entries, SECTION_ID_LOCALIZATION_UTF8)
    applied = require_section(entries, SECTION_ID_APPLIED_LORE)
    routes = require_section(entries, SECTION_ID_APPLIED_LORE_ROUTES)

    if localization.record_size != 1:
        fail(f"Localization record size mismatch: {localization.record_size}")
    if applied.record_size != APPLIED_LORE_RECORD_SIZE:
        fail(f"AppliedLore record size mismatch: {applied.record_size}")
    if routes.record_size != APPLIED_LORE_ROUTE_RECORD_SIZE:
        fail(f"AppliedLoreRoutes record size mismatch: {routes.record_size}")
    if localization.offset != localization_offset or localization.count != localization_bytes:
        fail("Directory localization pointer does not match section table.")

    validate_section_bounds(data, localization)
    validate_section_bounds(data, applied)
    validate_section_bounds(data, routes)
    return data, entries, localization, applied, routes


def require_section(entries: dict[int, SectionEntry], section_id: int) -> SectionEntry:
    entry = entries.get(section_id)
    if entry is None:
        fail(f"Missing Data Monolith section id {section_id}")
    return entry


def validate_section_bounds(data: bytes, entry: SectionEntry) -> None:
    byte_count = entry.count * entry.record_size
    if byte_count == 0:
        fail(f"Section {entry.section_id} is empty.")
    if entry.offset == 0 or entry.offset + byte_count > len(data):
        fail(f"Section {entry.section_id} exceeds blob bounds.")


def parse_applied_records(data: bytes, section: SectionEntry) -> list[AppliedLoreRecord]:
    records: list[AppliedLoreRecord] = []
    for index in range(section.count):
        offset = section.offset + index * APPLIED_LORE_RECORD_SIZE
        values = struct.unpack_from("<32I", data, offset)
        records.append(AppliedLoreRecord(values, index))
    return records


def parse_applied_route_records(data: bytes, section: SectionEntry) -> list[AppliedLoreRouteRecord]:
    records: list[AppliedLoreRouteRecord] = []
    for index in range(section.count):
        offset = section.offset + index * APPLIED_LORE_ROUTE_RECORD_SIZE
        packet_hash_slots = tuple(read_u32(data, offset + 32 + i * 4) for i in range(APPLIED_LORE_ROUTE_PACKET_CAPACITY))
        required_hash_slots = tuple(read_u32(data, offset + 64 + i * 4) for i in range(APPLIED_LORE_ROUTE_PREREQUISITE_CAPACITY))
        reserved = tuple(read_u32(data, offset + 88 + i * 4) for i in range(10))
        records.append(
            AppliedLoreRouteRecord(
                route_card_hash=read_u32(data, offset + 0),
                phase_hash=read_u32(data, offset + 4),
                depth_min_m=read_f32(data, offset + 8),
                depth_max_m=read_f32(data, offset + 12),
                primary_surface_mask=read_u32(data, offset + 16),
                ending_pressure_hash=read_u32(data, offset + 20),
                packet_count=read_u32(data, offset + 24),
                required_packet_count=read_u32(data, offset + 28),
                packet_hash_slots=packet_hash_slots,
                required_packet_hash_slots=required_hash_slots,
                flags=read_u32(data, offset + 80),
                record_index=read_u32(data, offset + 84),
                reserved=reserved,
                index=index,
            )
        )
    return records


def validate_applied_records(
    rows: list[CsvPacketRow],
    data: bytes,
    localization: SectionEntry,
    applied: SectionEntry,
) -> None:
    expected_by_key = {row.key: row for row in rows}
    if applied.count != len(rows):
        fail(f"AppliedLore count mismatch: blob={applied.count} csv={len(rows)}")

    records = parse_applied_records(data, applied)
    previous_key: tuple[int, int] | None = None
    seen: set[tuple[int, int]] = set()
    for record in records:
        if previous_key is not None and record.key <= previous_key:
            fail(f"AppliedLore records are not strictly sorted at index {record.index}")
        previous_key = record.key
        if record.key in seen:
            fail(f"Duplicate AppliedLore record key at index {record.index}: {record.key}")
        seen.add(record.key)

        row = expected_by_key.get(record.key)
        if row is None:
            fail(f"Blob contains AppliedLore pair not present in CSV at index {record.index}: {record.key}")
        validate_record_hashes(record, row)
        validate_record_text(record, row, data, localization)

    missing = sorted(set(expected_by_key).difference(seen))
    if missing:
        fail(f"CSV pairs missing from blob: {len(missing)}")


def validate_record_hashes(record: AppliedLoreRecord, row: CsvPacketRow) -> None:
    expected_values = {
        "PacketHash": fnv1a32(row.packet_id),
        "LocaleHash": fnv1a32(row.locale),
        "ArticleHash": fnv1a32(row.article_id),
        "UnlockHash": fnv1a32(row.unlock_id),
        "SurfaceMask": row.surface_mask,
        "ReleaseSetHash": fnv1a32(row.release_set_id),
        "PoiTagHash0": fnv1a32(row.poi_tags[0]) if len(row.poi_tags) > 0 else 0,
        "PoiTagHash1": fnv1a32(row.poi_tags[1]) if len(row.poi_tags) > 1 else 0,
        "BiomeTagHash0": fnv1a32(row.biome_tags[0]) if len(row.biome_tags) > 0 else 0,
        "BiomeTagHash1": fnv1a32(row.biome_tags[1]) if len(row.biome_tags) > 1 else 0,
        "Flags": row.flags,
        "RecordIndex": record.index,
    }
    offsets = {
        "PacketHash": 0,
        "LocaleHash": 4,
        "ArticleHash": 8,
        "UnlockHash": 12,
        "SurfaceMask": 16,
        "ReleaseSetHash": 20,
        "PoiTagHash0": 80,
        "PoiTagHash1": 84,
        "BiomeTagHash0": 88,
        "BiomeTagHash1": 92,
        "Flags": 96,
        "RecordIndex": 100,
    }
    for name, expected in expected_values.items():
        actual = record.value_at(offsets[name])
        if actual != expected:
            fail(
                f"Record {record.index} {row.packet_id}/{row.locale} {name} mismatch: "
                f"expected=0x{expected:08X} actual=0x{actual:08X}"
            )


def validate_record_text(
    record: AppliedLoreRecord,
    row: CsvPacketRow,
    data: bytes,
    localization: SectionEntry,
) -> None:
    localization_start = localization.offset
    localization_end = localization.offset + localization.count

    for field, bit, offset_offset, length_offset in SURFACES:
        offset = record.value_at(offset_offset)
        length = record.value_at(length_offset)
        expected_text = row.fields[field]
        expected_bytes = expected_text.encode("utf-8")
        surface_enabled = (row.surface_mask & bit) != 0

        if surface_enabled and length == 0:
            fail(f"Record {record.index} {row.packet_id}/{row.locale} empty enabled surface {field}")
        if length == 0:
            if offset != UINT_MAX:
                fail(f"Record {record.index} {field} zero length has bad offset: 0x{offset:08X}")
            continue
        if offset == UINT_MAX:
            fail(f"Record {record.index} {field} has data length but UINT_MAX offset")
        if length != len(expected_bytes):
            fail(
                f"Record {record.index} {row.packet_id}/{row.locale} {field} length mismatch: "
                f"csv={len(expected_bytes)} blob={length}"
            )

        start = localization_start + offset
        end = start + length
        if start < localization_start or end > localization_end:
            fail(f"Record {record.index} {field} localization slice exceeds pool bounds")
        actual_bytes = data[start:end]
        if actual_bytes != expected_bytes:
            fail(f"Record {record.index} {row.packet_id}/{row.locale} {field} text bytes mismatch")
        if end < localization_end and data[end] != 0:
            fail(f"Record {record.index} {field} text slice is not NUL-terminated in pool")


def extract_csharp_method_body(text: str, method_name: str) -> str:
    signature = f"public static void {method_name}("
    signature_index = text.find(signature)
    if signature_index < 0:
        fail(f"AppliedLore editor bridge missing method: {method_name}")

    brace_index = text.find("{", signature_index)
    if brace_index < 0:
        fail(f"AppliedLore editor bridge method has no body: {method_name}")

    depth = 0
    for index in range(brace_index, len(text)):
        char = text[index]
        if char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return text[brace_index : index + 1]

    fail(f"AppliedLore editor bridge method body is not closed: {method_name}")


def validate_source_route(root: Path) -> None:
    required_symbols = {
        "Tools/AppliedLoreImporter.py": (
            "TARGET_LOCALES",
            "applied_lore_packets.csv",
            "H8AppliedLoreHashes.cs",
        ),
        "Tools/AppliedLorePageExporter.py": (
            "collect_packets",
            "in_game_wiki",
            "external_site",
        ),
        "Tools/AppliedLoreRouteCardExporter.py": (
            "applied_lore_route_cards.csv",
            "route_card_hash_uint",
            "required_packet_hashes_uint",
        ),
        "Assets/_Project/Scripts/Core/Generated/H8AppliedLoreHashes.cs": (
            "public static class H8AppliedLoreHashes",
            "Locale_en_US",
            "SurfaceMaskInGameWiki",
        ),
        "Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs": (
            "AppliedLorePackets = 27u",
            "AppliedLoreRoutes = 28u",
            "public enum H8AppliedLoreSurface",
            "public struct H8AppliedLorePacketRecord",
            "public struct H8AppliedLoreRouteRecord",
        ),
        "Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithCompiler.cs": (
            "TryRunAppliedLoreImporter",
            "TryRunAppliedLoreRouteCardExporter",
            "ParseAppliedLorePacket",
            "ParseAppliedLoreRoute",
            "CompareAppliedLoreRecords",
            "CompareAppliedLoreRouteRecords",
        ),
        "Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs": (
            "TryFindAppliedLorePacket",
            "TryGetAppliedLoreUtf8",
            "TryFindAppliedLoreRoute",
            "GetAppliedLoreRouteCount",
            "CompareAppliedLoreKey",
        ),
        "Assets/_Project/Scripts/Data/Monolith/H8AppliedLoreRuntime.cs": (
            "TryGetUtf8",
            "TryFindRoute",
            "TryFindRouteForPacket",
            "TryRaisePacketUnlocked",
            "SignalBus<LoreFragmentScannedSignal>.TryPushTracked",
        ),
        "Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs": (
            "LoreFragmentScannedSignal",
            "SeedDataMonolithAppliedLoreMetadata",
            "TryGetAppliedLoreUtf8",
        ),
        "Assets/_Project/Scripts/ScannableTarget.cs": (
            "TryWriteLoreEntityTitle",
            "H8AppliedLoreRuntime.TryWriteTitleUtf16",
        ),
        "Assets/_Project/Scripts/Gameplay/MessageTerminal.cs": (
            "appliedLorePacketHash",
            "terminalOsPreviewIndex",
            "AppliedLoreTerminalPreviewSignal",
            "PublishAppliedLorePacketUnlock",
            "PublishAppliedLoreTerminalPreview",
            "AppliedLoreTerminalSourceHash",
        ),
        "Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs": (
            "ApplyTerminalAppliedLoreLine",
            "ConsumeAppliedLoreTerminalPreviewSignals",
            "SignalBus<AppliedLoreTerminalPreviewSignal>",
            "H8AppliedLoreRuntime.TryGetUtf8",
        ),
        "Assets/_Project/Scripts/NarrativeDiscovery.cs": (
            "appliedLorePacketHash",
            "PublishAppliedLorePacketUnlock",
            "AppliedLoreHash = appliedLorePacketHash",
        ),
        "Assets/_Project/Scripts/Gameplay/ScannableFragment.cs": (
            "appliedLoreQuarterPacketHash",
            "appliedLoreHalfPacketHash",
            "appliedLoreFinalPacketHash",
            "ResolveAppliedLoreStagePacketHash",
        ),
        "Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs": (
            "public uint AppliedLoreHash",
            "NarrativeSpatialTriggerAuthoring",
        ),
        "Assets/_Project/Scripts/Narrative/HectonNarrativeDirector_PoiTriggers.cs": (
            "AppliedLoreHash",
            "H8AppliedLoreRuntime.TryRaisePacketUnlocked",
            "NarrativePoiPresentationDTO",
        ),
        "Assets/_Project/Scripts/Editor/Narrative/H8AppliedLoreBindingCatalogWindow.cs": (
            "Applied Lore Binding Catalog",
            "Docs/Lore/AppliedContent/binding_maps",
            "*_scene_binding_targets.csv",
            "RS001_RS010_manual_binding_policy.csv",
            "RS001_RS010_scene_placement_plan.csv",
            "ManualBindingPolicyPath",
            "ScenePlacementPlanPath",
            "Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv",
            "Assign Existing",
            "Add + Assign",
            "Apply Applied Lore Target Backlog To Prefabs",
            "Create Applied Lore Terminal Anchor Prefab",
            "CreateAppliedLoreTerminalAnchorPrefab",
            "Generate Applied Lore Terminal Policy Prefabs",
            "GenerateAppliedLoreTerminalPolicyPrefabs",
            "PFB_AppliedLore_Terminal_",
            "PFB_AppliedLore_MessageTerminalAnchor.prefab",
            "Apply Applied Lore Scene Placement Plan",
            "ApplyScenePlacementPlanToOpenScene",
            "terminalOsPreviewIndex",
            "TerminalOsRuntimeObjectName",
            "EnsureTerminalOsRuntimeForScene",
            "TrySetTerminalOsRuntimeArrays",
            "statusLightRenderer",
            "terminal_os_missing_renderers",
            "TerminalOsBlitComputePath",
            "TerminalOsRuntimeTerminals +=",
            "LoadScenePlacementRows",
            "ApplyTargetBacklogToPrefabs",
            "CollectExistingPrefabCandidates",
            "CollectGeneratedTerminalPolicyPrefabPaths",
            "already has different packet hash",
            "PrefabUtility.InstantiatePrefab",
            "PrefabUtility.LoadPrefabContents",
            "PrefabUtility.SaveAsPrefabAsset",
            "EditorSceneManager.SaveScene",
            "Validate Applied Lore Bindings",
            "CollectPrefabCandidatePaths",
            "ref BindingValidationReport report",
            "SerializedObject",
            "appliedLorePacketHash",
            "appliedLoreQuarterPacketHash",
            "appliedLoreHalfPacketHash",
            "appliedLoreFinalPacketHash",
        ),
    }

    for relative_path, symbols in required_symbols.items():
        path = root / relative_path
        if not path.exists():
            fail(f"Missing AppliedLore source route file: {relative_path}")
        text = path.read_text(encoding="utf-8")
        for symbol in symbols:
            if symbol not in text:
                fail(f"Missing AppliedLore route symbol {symbol!r} in {relative_path}")

        if (
            relative_path == "Assets/_Project/Scripts/Editor/Narrative/H8AppliedLoreBindingCatalogWindow.cs"
            and 'AssetDatabase.FindAssets("t:Prefab"' in text
        ):
            fail("AppliedLore binding catalog must validate backlog candidates, not scan every project prefab")

        if relative_path == "Assets/_Project/Scripts/Editor/Narrative/H8AppliedLoreBindingCatalogWindow.cs":
            for method_name in (
                "ValidateBindingsMenu",
                "ApplyTargetBacklogToPrefabsMenu",
                "CreateAppliedLoreTerminalAnchorPrefabMenu",
                "GenerateAppliedLoreTerminalPolicyPrefabsMenu",
                "ApplyScenePlacementPlanMenu",
            ):
                method_body = extract_csharp_method_body(text, method_name)
                if "EditorUtility.DisplayDialog" in method_body:
                    fail(f"AppliedLore menu method must be MCP-safe/log-only: {method_name}")


def count_serialized_authoring_bindings(root: Path) -> SerializedBindingStats:
    assets_root = root / "Assets"
    if not assets_root.exists():
        return SerializedBindingStats()

    binding_pattern = re.compile(
        r"^\s*("
        + "|".join(re.escape(field) for field in AUTHORING_BINDING_FIELDS)
        + r"):\s*([0-9]+)\s*$",
        re.MULTILINE,
    )
    stats = SerializedBindingStats()
    extension_to_field = {
        ".unity": "scene_bindings",
        ".prefab": "prefab_bindings",
        ".asset": "asset_bindings",
    }
    for extension, field_name in extension_to_field.items():
        for path in assets_root.rglob(f"*{extension}"):
            try:
                text = path.read_text(encoding="utf-8")
            except UnicodeDecodeError:
                continue
            count = 0
            for match in binding_pattern.finditer(text):
                if int(match.group(2)) != 0:
                    count += 1
            if count:
                setattr(stats, field_name, getattr(stats, field_name) + count)
    return stats


def validate_binding_map(root: Path, rows: list[CsvPacketRow]) -> int:
    packet_ids = sorted({row.packet_id for row in rows})
    expected = set(packet_ids)
    seen: set[str] = set()
    for path in iter_applied_content_csv_sources(root, "binding_maps", "*_runtime_binding_map.csv"):
        with path.open("r", encoding="utf-8", newline="") as handle:
            reader = csv.DictReader(handle)
            required_columns = {
                "packet_id",
                "packet_hash_hex",
                "packet_hash_uint",
                "primary_component",
                "primary_field",
                "suggested_world_target",
                "unlock_moment",
            }
            missing_columns = sorted(required_columns.difference(reader.fieldnames or ()))
            if missing_columns:
                fail(f"Binding map missing columns in {path}: " + ", ".join(missing_columns))

            for line_number, row in enumerate(reader, start=2):
                packet_id = require_cell(row, "packet_id", line_number)
                if packet_id in seen:
                    fail(f"Binding map duplicate packet_id at {path}:{line_number}: {packet_id}")
                seen.add(packet_id)
                if packet_id not in expected:
                    fail(f"Binding map packet is not in baked CSV at {path}:{line_number}: {packet_id}")

                expected_hash = fnv1a32(packet_id)
                actual_hex = parse_int(require_cell(row, "packet_hash_hex", line_number), "packet_hash_hex", line_number)
                actual_uint = parse_int(require_cell(row, "packet_hash_uint", line_number), "packet_hash_uint", line_number)
                if actual_hex != expected_hash or actual_uint != expected_hash:
                    fail(
                        f"Binding map hash mismatch at {path}:{line_number}: {packet_id} "
                        f"expected=0x{expected_hash:08X} hex=0x{actual_hex:08X} uint={actual_uint}"
                    )

                require_cell(row, "primary_component", line_number)
                require_cell(row, "primary_field", line_number)
                require_cell(row, "suggested_world_target", line_number)
                require_cell(row, "unlock_moment", line_number)

    missing_packets = sorted(expected.difference(seen))
    if missing_packets:
        fail("Binding map missing packets: " + ", ".join(missing_packets))
    return len(seen)


def parse_candidate_paths(value: str) -> tuple[str, ...]:
    if not value:
        return ()
    return tuple(part.strip() for part in value.split(";") if part.strip())


def validate_scene_binding_targets(root: Path, rows: list[CsvPacketRow]) -> SceneBindingTargetStats:
    packet_ids = {row.packet_id for row in rows}
    base = root / "Docs" / "Lore" / "AppliedContent" / "binding_maps"
    paths = sorted(base.glob("*_scene_binding_targets.csv"), key=lambda path: path.name.lower())
    if not paths:
        return SceneBindingTargetStats()

    seen: set[str] = set()
    stats = SceneBindingTargetStats()
    for path in paths:
        with path.open("r", encoding="utf-8", newline="") as handle:
            reader = csv.DictReader(handle)
            if tuple(reader.fieldnames or ()) != SCENE_BINDING_TARGET_HEADERS:
                fail(f"Scene binding targets header mismatch in {path}")

            for line_number, row in enumerate(reader, start=2):
                packet_id = require_cell(row, "packet_id", line_number)
                if packet_id in seen:
                    fail(f"Scene binding target duplicate packet_id at {path}:{line_number}: {packet_id}")
                seen.add(packet_id)
                if packet_id not in packet_ids:
                    fail(f"Scene binding target packet is not in baked CSV at {path}:{line_number}: {packet_id}")

                expected_hash = fnv1a32(packet_id)
                actual_hex = parse_int(require_cell(row, "packet_hash_hex", line_number), "packet_hash_hex", line_number)
                actual_decimal = parse_int(
                    require_cell(row, "packet_hash_decimal", line_number),
                    "packet_hash_decimal",
                    line_number,
                )
                if actual_hex != expected_hash or actual_decimal != expected_hash:
                    fail(
                        f"Scene binding target hash mismatch at {path}:{line_number}: {packet_id} "
                        f"expected=0x{expected_hash:08X} hex=0x{actual_hex:08X} decimal={actual_decimal}"
                    )

                component = require_cell(row, "authoring_component", line_number)
                field = require_cell(row, "serialized_field", line_number)
                allowed_fields = SCENE_BINDING_TARGET_FIELDS.get(component)
                if allowed_fields is None:
                    fail(f"Scene binding target unsupported component at {path}:{line_number}: {component}")
                if field not in allowed_fields:
                    fail(f"Scene binding target unsupported field at {path}:{line_number}: {component}.{field}")

                primary_candidates = require_cell(row, "primary_target_candidates", line_number)
                secondary_candidates = row.get("secondary_target_candidates", "")
                require_cell(row, "unity_safe_action", line_number)

                candidate_paths = parse_candidate_paths(primary_candidates) + parse_candidate_paths(secondary_candidates)
                if not candidate_paths:
                    fail(f"Scene binding target has no candidates at {path}:{line_number}: {packet_id}")

                has_prefab_candidate = False
                for candidate_path in candidate_paths:
                    absolute_candidate = root / candidate_path
                    if not absolute_candidate.exists():
                        fail(f"Scene binding target candidate missing at {path}:{line_number}: {candidate_path}")

                    stats.existing_candidate_paths += 1
                    if candidate_path.lower().endswith(".prefab"):
                        has_prefab_candidate = True

                stats.rows += 1
                if has_prefab_candidate:
                    stats.prefab_candidate_rows += 1

                if component == "ScannableFragment" and has_prefab_candidate:
                    stats.auto_prefab_rows += 1
                else:
                    stats.manual_rows += 1

    return stats


def load_scene_binding_target_rows(root: Path) -> dict[str, dict[str, str]]:
    base = root / "Docs" / "Lore" / "AppliedContent" / "binding_maps"
    paths = sorted(base.glob("*_scene_binding_targets.csv"), key=lambda path: path.name.lower())
    targets: dict[str, dict[str, str]] = {}
    for path in paths:
        with path.open("r", encoding="utf-8", newline="") as handle:
            reader = csv.DictReader(handle)
            if tuple(reader.fieldnames or ()) != SCENE_BINDING_TARGET_HEADERS:
                fail(f"Scene binding targets header mismatch in {path}")

            for line_number, row in enumerate(reader, start=2):
                packet_id = require_cell(row, "packet_id", line_number)
                if packet_id in targets:
                    fail(f"Scene binding target duplicate packet_id at {path}:{line_number}: {packet_id}")
                targets[packet_id] = row

    return targets


def collect_manual_scene_binding_targets(root: Path) -> dict[str, dict[str, str]]:
    manual: dict[str, dict[str, str]] = {}
    for packet_id, row in load_scene_binding_target_rows(root).items():
        component = row.get("authoring_component", "")
        candidates = parse_candidate_paths(row.get("primary_target_candidates", "")) + parse_candidate_paths(
            row.get("secondary_target_candidates", "")
        )
        has_prefab_candidate = any(candidate.lower().endswith(".prefab") for candidate in candidates)
        if component != "ScannableFragment" or not has_prefab_candidate:
            manual[packet_id] = row
    return manual


def validate_manual_binding_policy(root: Path, rows: list[CsvPacketRow]) -> ManualBindingPolicyStats:
    packet_ids = {row.packet_id for row in rows}
    manual_targets = collect_manual_scene_binding_targets(root)
    path = root / "Docs" / "Lore" / "AppliedContent" / "binding_maps" / "RS001_RS010_manual_binding_policy.csv"
    if not path.exists():
        fail(f"Missing AppliedLore manual binding policy: {path}")

    seen: set[str] = set()
    stats = ManualBindingPolicyStats()
    with path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        if tuple(reader.fieldnames or ()) != MANUAL_BINDING_POLICY_HEADERS:
            fail(f"Manual binding policy header mismatch in {path}")

        for line_number, row in enumerate(reader, start=2):
            packet_id = require_cell(row, "packet_id", line_number)
            if packet_id in seen:
                fail(f"Manual binding policy duplicate packet_id at {path}:{line_number}: {packet_id}")
            seen.add(packet_id)
            if packet_id not in packet_ids:
                fail(f"Manual binding policy packet is not in baked CSV at {path}:{line_number}: {packet_id}")

            target = manual_targets.get(packet_id)
            if target is None:
                fail(f"Manual binding policy points at auto-bound or unknown packet at {path}:{line_number}: {packet_id}")

            expected_hash = fnv1a32(packet_id)
            actual_hex = parse_int(require_cell(row, "packet_hash_hex", line_number), "packet_hash_hex", line_number)
            actual_decimal = parse_int(
                require_cell(row, "packet_hash_decimal", line_number),
                "packet_hash_decimal",
                line_number,
            )
            if actual_hex != expected_hash or actual_decimal != expected_hash:
                fail(
                    f"Manual binding policy hash mismatch at {path}:{line_number}: {packet_id} "
                    f"expected=0x{expected_hash:08X} hex=0x{actual_hex:08X} decimal={actual_decimal}"
                )

            policy = require_cell(row, "manual_policy", line_number)
            if policy not in MANUAL_BINDING_POLICIES:
                fail(f"Manual binding policy unsupported policy at {path}:{line_number}: {policy}")

            component = require_cell(row, "authoring_component", line_number)
            field = require_cell(row, "serialized_field", line_number)
            if component != target.get("authoring_component", "") or field != target.get("serialized_field", ""):
                fail(f"Manual binding policy component mismatch at {path}:{line_number}: {packet_id}")

            require_cell(row, "required_anchor_type", line_number)
            require_cell(row, "placement_rule", line_number)
            require_cell(row, "reason", line_number)

            template_prefab = row.get("approved_template_prefab", "")
            if component == "MessageTerminal":
                if policy != "terminal_anchor_required":
                    fail(f"MessageTerminal manual policy must require terminal anchor at {path}:{line_number}: {packet_id}")
                if template_prefab != TERMINAL_ANCHOR_PREFAB:
                    fail(f"MessageTerminal manual policy must use terminal anchor prefab at {path}:{line_number}: {packet_id}")
                if not (root / template_prefab).exists():
                    fail(f"Manual binding policy terminal anchor prefab missing: {template_prefab}")
                generated_prefab = root / TERMINAL_POLICY_PREFAB_FOLDER / f"PFB_AppliedLore_Terminal_{packet_id}.prefab"
                if generated_prefab.exists():
                    try:
                        prefab_text = generated_prefab.read_text(encoding="utf-8")
                    except UnicodeDecodeError as exc:
                        raise AuditFailure(f"Manual binding policy terminal prefab is not text-readable: {generated_prefab}") from exc
                    if "Hecton8.Gameplay.MessageTerminal" not in prefab_text:
                        fail(f"Manual binding policy terminal prefab has no MessageTerminal: {generated_prefab}")
                    if f"appliedLorePacketHash: {expected_hash}" not in prefab_text:
                        fail(f"Manual binding policy terminal prefab has wrong hash: {generated_prefab}")
                    stats.generated_terminal_prefab_rows += 1
                stats.terminal_rows += 1
                stats.template_prefab_rows += 1
            elif component == "NarrativeDiscovery":
                if policy != "discovery_world_prop_required":
                    fail(f"NarrativeDiscovery manual policy must require marked world prop at {path}:{line_number}: {packet_id}")
                if template_prefab:
                    fail(f"NarrativeDiscovery manual policy must not use a generic prefab template at {path}:{line_number}: {packet_id}")
                require_cell(row, "discovery_id", line_number)
                stats.discovery_rows += 1
            else:
                fail(f"Manual binding policy unsupported component at {path}:{line_number}: {component}")

            stats.rows += 1

    missing = sorted(set(manual_targets).difference(seen))
    if missing:
        fail("Manual binding policy missing packets: " + ", ".join(missing))
    return stats


def load_manual_binding_policy_rows(root: Path) -> dict[str, dict[str, str]]:
    path = root / "Docs" / "Lore" / "AppliedContent" / "binding_maps" / "RS001_RS010_manual_binding_policy.csv"
    if not path.exists():
        fail(f"Missing AppliedLore manual binding policy: {path}")

    rows: dict[str, dict[str, str]] = {}
    with path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        if tuple(reader.fieldnames or ()) != MANUAL_BINDING_POLICY_HEADERS:
            fail(f"Manual binding policy header mismatch in {path}")

        for line_number, row in enumerate(reader, start=2):
            packet_id = require_cell(row, "packet_id", line_number)
            if packet_id in rows:
                fail(f"Manual binding policy duplicate packet_id at {path}:{line_number}: {packet_id}")
            rows[packet_id] = row

    return rows


def parse_vector3_pipe(value: str, field: str, line_number: int, *, positive: bool = False) -> tuple[float, float, float]:
    parts = value.split("|")
    if len(parts) != 3:
        fail(f"Scene placement plan line {line_number}: {field} must use x|y|z format")
    try:
        result = (float(parts[0]), float(parts[1]), float(parts[2]))
    except ValueError as exc:
        raise AuditFailure(f"Scene placement plan line {line_number}: bad vector {field}={value!r}") from exc
    if positive and any(part <= 0.0 for part in result):
        fail(f"Scene placement plan line {line_number}: {field} components must be positive")
    return result


def validate_scene_placement_plan(root: Path, rows: list[CsvPacketRow]) -> ScenePlacementPlanStats:
    packet_ids = {row.packet_id for row in rows}
    manual_policy_rows = load_manual_binding_policy_rows(root)
    path = root / "Docs" / "Lore" / "AppliedContent" / "binding_maps" / "RS001_RS010_scene_placement_plan.csv"
    if not path.exists():
        fail(f"Missing AppliedLore scene placement plan: {path}")

    seen: set[str] = set()
    stats = ScenePlacementPlanStats()
    with path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        if tuple(reader.fieldnames or ()) != SCENE_PLACEMENT_PLAN_HEADERS:
            fail(f"Scene placement plan header mismatch in {path}")

        for line_number, row in enumerate(reader, start=2):
            packet_id = require_cell(row, "packet_id", line_number)
            if packet_id in seen:
                fail(f"Scene placement plan duplicate packet_id at {path}:{line_number}: {packet_id}")
            seen.add(packet_id)
            if packet_id not in packet_ids:
                fail(f"Scene placement plan packet is not in baked CSV at {path}:{line_number}: {packet_id}")

            policy = manual_policy_rows.get(packet_id)
            if policy is None:
                fail(f"Scene placement plan must cover only manual policy packets at {path}:{line_number}: {packet_id}")

            expected_hash = fnv1a32(packet_id)
            actual_hex = parse_int(require_cell(row, "packet_hash_hex", line_number), "packet_hash_hex", line_number)
            actual_decimal = parse_int(
                require_cell(row, "packet_hash_decimal", line_number),
                "packet_hash_decimal",
                line_number,
            )
            if actual_hex != expected_hash or actual_decimal != expected_hash:
                fail(
                    f"Scene placement plan hash mismatch at {path}:{line_number}: {packet_id} "
                    f"expected=0x{expected_hash:08X} hex=0x{actual_hex:08X} decimal={actual_decimal}"
                )

            scene_path = require_cell(row, "scene_path", line_number)
            if not (root / scene_path).exists():
                fail(f"Scene placement plan scene missing at {path}:{line_number}: {scene_path}")
            require_cell(row, "placement_root", line_number)
            require_cell(row, "object_name", line_number)
            source_prefab = require_cell(row, "source_prefab", line_number)
            if not (root / source_prefab).exists():
                fail(f"Scene placement plan source prefab missing at {path}:{line_number}: {source_prefab}")

            component = require_cell(row, "authoring_component", line_number)
            field = require_cell(row, "serialized_field", line_number)
            if component != policy.get("authoring_component", "") or field != policy.get("serialized_field", ""):
                fail(f"Scene placement plan component mismatch at {path}:{line_number}: {packet_id}")
            allowed_fields = SCENE_BINDING_TARGET_FIELDS.get(component)
            if allowed_fields is None or field not in allowed_fields:
                fail(f"Scene placement plan unsupported target field at {path}:{line_number}: {component}.{field}")

            require_cell(row, "display_name", line_number)
            parse_vector3_pipe(require_cell(row, "local_position", line_number), "local_position", line_number)
            parse_vector3_pipe(require_cell(row, "local_euler", line_number), "local_euler", line_number)
            parse_vector3_pipe(require_cell(row, "local_scale", line_number), "local_scale", line_number, positive=True)
            require_cell(row, "depth_band", line_number)
            require_cell(row, "zone_tag", line_number)
            require_cell(row, "placement_note", line_number)

            if component == "MessageTerminal":
                if not source_prefab.startswith(TERMINAL_POLICY_PREFAB_FOLDER + "/PFB_AppliedLore_Terminal_"):
                    fail(f"Scene placement plan terminal row must use generated terminal prefab at {path}:{line_number}: {packet_id}")
                try:
                    prefab_text = (root / source_prefab).read_text(encoding="utf-8")
                except UnicodeDecodeError as exc:
                    raise AuditFailure(f"Scene placement plan terminal prefab is not text-readable: {source_prefab}") from exc
                if "Hecton8.Gameplay.MessageTerminal" not in prefab_text:
                    fail(f"Scene placement plan terminal prefab has no MessageTerminal: {source_prefab}")
                if f"appliedLorePacketHash: {expected_hash}" not in prefab_text:
                    fail(f"Scene placement plan terminal prefab has wrong hash: {source_prefab}")
                stats.terminal_rows += 1
            elif component == "NarrativeDiscovery":
                discovery_id = require_cell(row, "discovery_id", line_number)
                if discovery_id != policy.get("discovery_id", ""):
                    fail(f"Scene placement plan discovery_id mismatch at {path}:{line_number}: {packet_id}")
                stats.discovery_rows += 1
            else:
                fail(f"Scene placement plan unsupported component at {path}:{line_number}: {component}")

            stats.rows += 1

    missing = sorted(set(manual_policy_rows).difference(seen))
    if missing:
        fail("Scene placement plan missing manual packets: " + ", ".join(missing))
    extra = sorted(seen.difference(manual_policy_rows))
    if extra:
        fail("Scene placement plan has non-manual packets: " + ", ".join(extra))
    return stats


def count_serialized_scene_placement_rows(root: Path) -> int:
    path = root / "Docs" / "Lore" / "AppliedContent" / "binding_maps" / "RS001_RS010_scene_placement_plan.csv"
    if not path.exists():
        return 0

    scene_cache: dict[str, str] = {}
    count = 0
    with path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        if tuple(reader.fieldnames or ()) != SCENE_PLACEMENT_PLAN_HEADERS:
            fail(f"Scene placement plan header mismatch in {path}")

        for line_number, row in enumerate(reader, start=2):
            scene_path = require_cell(row, "scene_path", line_number)
            scene_text = scene_cache.get(scene_path)
            if scene_text is None:
                absolute_scene = root / scene_path
                if not absolute_scene.exists():
                    scene_cache[scene_path] = ""
                    scene_text = ""
                else:
                    try:
                        scene_text = absolute_scene.read_text(encoding="utf-8")
                    except UnicodeDecodeError:
                        scene_text = ""
                    scene_cache[scene_path] = scene_text

            packet_hash = parse_int(require_cell(row, "packet_hash_decimal", line_number), "packet_hash_decimal", line_number)
            object_name = require_cell(row, "object_name", line_number)
            serialized_field = require_cell(row, "serialized_field", line_number)
            if object_name not in scene_text:
                continue
            if f"{serialized_field}: {packet_hash}" not in scene_text:
                continue

            if row.get("authoring_component", "") == "NarrativeDiscovery":
                discovery_id = require_cell(row, "discovery_id", line_number)
                if f"discoveryId: {discovery_id}" not in scene_text:
                    continue

            count += 1

    return count


def count_scene_placement_covered_rows(root: Path) -> int:
    path = root / "Docs" / "Lore" / "AppliedContent" / "binding_maps" / "RS001_RS010_scene_placement_plan.csv"
    if not path.exists():
        return 0

    scene_cache: dict[str, str] = {}
    prefab_cache: dict[str, str] = {}
    count = 0
    with path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        if tuple(reader.fieldnames or ()) != SCENE_PLACEMENT_PLAN_HEADERS:
            fail(f"Scene placement plan header mismatch in {path}")

        for line_number, row in enumerate(reader, start=2):
            scene_path = require_cell(row, "scene_path", line_number)
            scene_text = scene_cache.get(scene_path)
            if scene_text is None:
                absolute_scene = root / scene_path
                if not absolute_scene.exists():
                    scene_cache[scene_path] = ""
                    scene_text = ""
                else:
                    try:
                        scene_text = absolute_scene.read_text(encoding="utf-8")
                    except UnicodeDecodeError:
                        scene_text = ""
                    scene_cache[scene_path] = scene_text

            object_name = require_cell(row, "object_name", line_number)
            if object_name not in scene_text:
                continue

            packet_hash = parse_int(require_cell(row, "packet_hash_decimal", line_number), "packet_hash_decimal", line_number)
            serialized_field = require_cell(row, "serialized_field", line_number)
            scene_has_hash = f"{serialized_field}: {packet_hash}" in scene_text

            source_prefab = require_cell(row, "source_prefab", line_number)
            prefab_text = prefab_cache.get(source_prefab)
            if prefab_text is None:
                absolute_prefab = root / source_prefab
                if not absolute_prefab.exists():
                    prefab_cache[source_prefab] = ""
                    prefab_text = ""
                else:
                    try:
                        prefab_text = absolute_prefab.read_text(encoding="utf-8")
                    except UnicodeDecodeError:
                        prefab_text = ""
                    prefab_cache[source_prefab] = prefab_text

            prefab_has_hash = f"{serialized_field}: {packet_hash}" in prefab_text
            if not scene_has_hash and not prefab_has_hash:
                continue

            if row.get("authoring_component", "") == "NarrativeDiscovery":
                discovery_id = require_cell(row, "discovery_id", line_number)
                if f"discoveryId: {discovery_id}" not in scene_text:
                    continue

            count += 1

    return count


def terminal_os_hash_index(index: int) -> int:
    hash_value = 0x5445524D
    hash_value = ((hash_value ^ (index + 1)) * FNV_PRIME) & 0xFFFFFFFF
    hash_value = ((hash_value ^ (index << 16)) * FNV_PRIME) & 0xFFFFFFFF
    return hash_value or 1


def scene_has_serialized_scalar(scene_text: str, property_name: str, value: int) -> bool:
    direct = f"{property_name}: {value}"
    if direct in scene_text:
        return True

    escaped_property = re.escape(property_name)
    escaped_value = re.escape(str(value))
    return re.search(
        rf"propertyPath:\s+{escaped_property}\s*\r?\n\s*value:\s*{escaped_value}\b",
        scene_text,
    ) is not None


def try_parse_prefab_modification_value(lines: list[str], start_index: int) -> int | None:
    for line in lines[start_index + 1 : min(start_index + 4, len(lines))]:
        stripped = line.strip()
        if not stripped.startswith("value:"):
            continue

        value_text = stripped[6:].strip()
        if not value_text:
            return None

        try:
            return int(value_text, 10)
        except ValueError:
            return None

    return None


def collect_terminal_preview_pairs(scene_text: str) -> list[tuple[int, int]]:
    lines = scene_text.splitlines()
    pairs: list[tuple[int, int]] = []
    pending_hash: int | None = None
    for index, line in enumerate(lines):
        if "propertyPath: terminalOsPreviewHash" in line:
            pending_hash = try_parse_prefab_modification_value(lines, index)
            continue

        if "propertyPath: terminalOsPreviewIndex" not in line:
            continue

        preview_index = try_parse_prefab_modification_value(lines, index)
        if pending_hash is None or preview_index is None:
            pending_hash = None
            continue

        pairs.append((preview_index, pending_hash))
        pending_hash = None

    return pairs


def collect_terminal_runtime_scene_refs(
    root: Path,
    scene_text: str,
    scene_path: str,
) -> dict[int, tuple[int, int]]:
    guid_to_prefab = build_prefab_guid_index(root)
    stripped_refs = collect_stripped_scene_refs(scene_text)
    refs: dict[int, tuple[int, int]] = {}
    for match in re.finditer(r"(?ms)^--- !u!1001 &(\d+)\nPrefabInstance:\n(.*?)(?=^--- !u!|\Z)", scene_text):
        prefab_instance_id = int(match.group(1))
        block = match.group(2)
        if "propertyPath: terminalOsPreviewIndex" not in block:
            continue

        source_match = re.search(r"m_SourcePrefab:\s+\{fileID:\s*100100000,\s*guid:\s*([0-9a-fA-F]+),", block)
        if source_match is None:
            fail(f"Terminal prefab instance has no source guid in {scene_path}: {prefab_instance_id}")

        preview_match = re.search(
            r"propertyPath:\s+terminalOsPreviewIndex\s*\r?\n\s*value:\s*(\d+)\b",
            block,
        )
        if preview_match is None:
            fail(f"Terminal prefab instance has no preview index in {scene_path}: {prefab_instance_id}")

        preview_index = int(preview_match.group(1))
        source_guid = source_match.group(1).lower()
        prefab_path = guid_to_prefab.get(source_guid)
        if prefab_path is None:
            fail(f"Terminal prefab guid is missing from project in {scene_path}: {source_guid}")

        root_transform_fileid, status_renderer_fileid = read_terminal_prefab_runtime_source_refs(prefab_path)
        transform_scene_ref = stripped_refs.get((prefab_instance_id, source_guid, root_transform_fileid, 4))
        renderer_scene_ref = stripped_refs.get((prefab_instance_id, source_guid, status_renderer_fileid, 23))
        if transform_scene_ref is None:
            fail(
                f"Terminal transform stripped reference is missing in {scene_path}: "
                f"index={preview_index} source={source_guid}"
            )
        if renderer_scene_ref is None:
            fail(
                f"Terminal renderer stripped reference is missing in {scene_path}: "
                f"index={preview_index} source={source_guid}"
            )

        if preview_index in refs:
            fail(f"Duplicate TerminalOS runtime preview index in {scene_path}: {preview_index}")

        refs[preview_index] = (renderer_scene_ref, transform_scene_ref)

    return refs


def build_prefab_guid_index(root: Path) -> dict[str, Path]:
    prefabs_root = root / "Assets" / "_Project" / "Prefabs"
    result: dict[str, Path] = {}
    for meta_path in prefabs_root.rglob("*.prefab.meta"):
        try:
            meta_text = meta_path.read_text(encoding="utf-8")
        except UnicodeDecodeError:
            continue

        match = re.search(r"^guid:\s*([0-9a-fA-F]+)\s*$", meta_text, re.MULTILINE)
        if match is None:
            continue

        prefab_path = meta_path.with_suffix("")
        if prefab_path.suffix != ".prefab":
            continue

        result[match.group(1).lower()] = prefab_path

    return result


def read_terminal_prefab_runtime_source_refs(prefab_path: Path) -> tuple[int, int]:
    try:
        text = prefab_path.read_text(encoding="utf-8")
    except UnicodeDecodeError:
        fail(f"Cannot read terminal prefab as UTF-8: {prefab_path}")

    transform_match = re.search(
        r"(?ms)^--- !u!4 &(\d+)\nTransform:\n.*?^\s*m_Father:\s+\{fileID:\s*0\}",
        text,
    )
    renderer_match = re.search(r"^\s*statusLightRenderer:\s+\{fileID:\s*(\d+)\}", text, re.MULTILINE)
    if transform_match is None:
        fail(f"Terminal prefab root transform reference is missing: {prefab_path}")
    if renderer_match is None:
        fail(f"Terminal prefab statusLightRenderer reference is missing: {prefab_path}")

    return int(transform_match.group(1)), int(renderer_match.group(1))


def collect_stripped_scene_refs(scene_text: str) -> dict[tuple[int, str, int, int], int]:
    refs: dict[tuple[int, str, int, int], int] = {}
    pattern = re.compile(
        r"(?ms)^--- !u!(4|23) &(\d+) stripped\n"
        r"(?:Transform|MeshRenderer):\n"
        r"\s*m_CorrespondingSourceObject:\s+\{fileID:\s*(\d+),\s*guid:\s*([0-9a-fA-F]+),\s*type:\s*3\}\n"
        r"\s*m_PrefabInstance:\s+\{fileID:\s*(\d+)\}",
    )
    for match in pattern.finditer(scene_text):
        unity_type = int(match.group(1))
        scene_fileid = int(match.group(2))
        source_fileid = int(match.group(3))
        source_guid = match.group(4).lower()
        prefab_instance_id = int(match.group(5))
        refs[(prefab_instance_id, source_guid, source_fileid, unity_type)] = scene_fileid

    return refs


def count_scene_terminal_preview_rows(root: Path) -> int:
    path = root / "Docs" / "Lore" / "AppliedContent" / "binding_maps" / "RS001_RS010_scene_placement_plan.csv"
    if not path.exists():
        return 0

    scene_cache: dict[str, str] = {}
    preview_pair_cache: dict[str, list[tuple[int, int]]] = {}
    count = 0
    terminal_index = 0
    with path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        if tuple(reader.fieldnames or ()) != SCENE_PLACEMENT_PLAN_HEADERS:
            fail(f"Scene placement plan header mismatch in {path}")

        for line_number, row in enumerate(reader, start=2):
            if row.get("authoring_component", "") != "MessageTerminal":
                continue

            expected_index = terminal_index
            terminal_index += 1
            scene_path = require_cell(row, "scene_path", line_number)
            scene_text = scene_cache.get(scene_path)
            if scene_text is None:
                absolute_scene = root / scene_path
                if not absolute_scene.exists():
                    scene_cache[scene_path] = ""
                    scene_text = ""
                else:
                    try:
                        scene_text = absolute_scene.read_text(encoding="utf-8")
                    except UnicodeDecodeError:
                        scene_text = ""
                    scene_cache[scene_path] = scene_text

            object_name = require_cell(row, "object_name", line_number)
            if object_name not in scene_text:
                continue

            expected_hash = terminal_os_hash_index(expected_index)
            preview_pairs = preview_pair_cache.get(scene_path)
            if preview_pairs is None:
                preview_pairs = collect_terminal_preview_pairs(scene_text)
                preview_pair_cache[scene_path] = preview_pairs
                preview_indices: set[int] = set()
                for preview_index, preview_hash in preview_pairs:
                    if preview_index in preview_indices:
                        fail(f"Duplicate terminalOsPreviewIndex in {scene_path}: {preview_index}")
                    preview_indices.add(preview_index)
                    expected_preview_hash = terminal_os_hash_index(preview_index)
                    if preview_hash != expected_preview_hash:
                        fail(
                            f"terminalOsPreviewHash mismatch in {scene_path}: "
                            f"index={preview_index} expected={expected_preview_hash} actual={preview_hash}"
                        )

            if (expected_index, expected_hash) not in preview_pairs:
                continue

            count += 1

    return count


def count_scene_terminal_os_runtime_rows(root: Path, expected_terminal_rows: int) -> SceneTerminalOsRuntimeStats:
    path = root / "Docs" / "Lore" / "AppliedContent" / "binding_maps" / "RS001_RS010_scene_placement_plan.csv"
    if not path.exists():
        return SceneTerminalOsRuntimeStats()

    terminal_scene_paths: set[str] = set()
    with path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        if tuple(reader.fieldnames or ()) != SCENE_PLACEMENT_PLAN_HEADERS:
            fail(f"Scene placement plan header mismatch in {path}")

        for line_number, row in enumerate(reader, start=2):
            if row.get("authoring_component", "") != "MessageTerminal":
                continue

            terminal_scene_paths.add(require_cell(row, "scene_path", line_number))

    stats = SceneTerminalOsRuntimeStats()
    for scene_path in sorted(terminal_scene_paths):
        absolute_scene = root / scene_path
        if not absolute_scene.exists():
            continue

        try:
            scene_text = absolute_scene.read_text(encoding="utf-8")
        except UnicodeDecodeError:
            continue

        has_runtime_object = TERMINAL_OS_RUNTIME_OBJECT_NAME in scene_text
        has_runtime_script = TERMINAL_OS_RUNTIME_SCRIPT_GUID in scene_text
        if has_runtime_object and not has_runtime_script:
            fail(
                f"Scene TerminalOS runtime object is missing TerminalOsRuntime script guid: "
                f"{scene_path}"
            )

        if has_runtime_object and has_runtime_script:
            runtime_block = extract_terminal_os_runtime_block(scene_text, scene_path)
            renderer_refs = collect_yaml_reference_array(runtime_block, "terminalRenderers", scene_path)
            transform_refs = collect_yaml_reference_array(runtime_block, "terminalTransforms", scene_path)
            renderer_slots = len(renderer_refs)
            transform_slots = len(transform_refs)
            if renderer_slots != expected_terminal_rows:
                fail(
                    f"Scene TerminalOS renderer slot count mismatch in {scene_path}: "
                    f"expected={expected_terminal_rows} actual={renderer_slots}"
                )
            if transform_slots != expected_terminal_rows:
                fail(
                    f"Scene TerminalOS transform slot count mismatch in {scene_path}: "
                    f"expected={expected_terminal_rows} actual={transform_slots}"
                )

            expected_refs = collect_terminal_runtime_scene_refs(root, scene_text, scene_path)
            if len(expected_refs) != expected_terminal_rows:
                fail(
                    f"Scene TerminalOS terminal reference count mismatch in {scene_path}: "
                    f"expected={expected_terminal_rows} actual={len(expected_refs)}"
                )

            verified_slots = 0
            for preview_index in range(expected_terminal_rows):
                expected = expected_refs.get(preview_index)
                if expected is None:
                    fail(f"Scene TerminalOS missing expected terminal preview index in {scene_path}: {preview_index}")

                expected_renderer, expected_transform = expected
                if renderer_refs[preview_index] != expected_renderer:
                    fail(
                        f"Scene TerminalOS renderer slot mismatch in {scene_path}: "
                        f"slot={preview_index} expected={expected_renderer} actual={renderer_refs[preview_index]}"
                    )
                if transform_refs[preview_index] != expected_transform:
                    fail(
                        f"Scene TerminalOS transform slot mismatch in {scene_path}: "
                        f"slot={preview_index} expected={expected_transform} actual={transform_refs[preview_index]}"
                    )

                verified_slots += 1

            stats.rows += 1
            stats.renderer_slots += renderer_slots
            stats.transform_slots += transform_slots
            stats.verified_slots += verified_slots

    return stats


def validate_world_scene_core(root: Path) -> SceneWorldCoreStats:
    scene_path = root / WORLD_SCENE_PATH
    if not scene_path.exists():
        fail(f"World scene missing: {WORLD_SCENE_PATH}")

    try:
        scene_bytes = scene_path.stat().st_size
        scene_text = scene_path.read_text(encoding="utf-8")
    except UnicodeDecodeError as exc:
        raise AuditFailure(f"World scene must be text-serializable for offline proof: {WORLD_SCENE_PATH}") from exc

    stats = SceneWorldCoreStats()
    stats.bytes = scene_bytes
    stats.mapmagic_rows = scene_text.count(WORLD_SCENE_MAPMAGIC_MARKER)
    stats.terrain_rows = len(re.findall(r"(?m)^Terrain:\s*$", scene_text))
    stats.terrain_collider_rows = len(re.findall(r"(?m)^TerrainCollider:\s*$", scene_text))
    stats.crest_markers = scene_text.count(WORLD_SCENE_CREST_MARKER)
    for prefab_path in WORLD_SCENE_OCEAN_PREFABS:
        prefab = root / prefab_path
        if not prefab.exists():
            continue

        stats.ocean_prefab_assets += 1
        guid = read_unity_meta_guid(prefab.with_suffix(prefab.suffix + ".meta"))
        if guid:
            stats.ocean_prefab_scene_refs += scene_text.count(guid)

    roots_match = re.search(
        r"(?m)^\s+m_Roots:\s*\r?\n(?P<roots>(?:^\s+-\s+\{fileID:\s*-?\d+[^\r\n]*\r?\n?)+)",
        scene_text,
    )
    if roots_match:
        stats.roots = len(re.findall(r"\{fileID:\s*-?\d+\}", roots_match.group("roots")))

    if stats.roots <= 0:
        fail(f"World scene root list missing or empty: {WORLD_SCENE_PATH}")
    if stats.mapmagic_rows <= 0:
        stats.dependency_warnings += 1
    if stats.terrain_rows <= 0:
        stats.dependency_warnings += 1
    if stats.terrain_collider_rows <= 0:
        stats.dependency_warnings += 1

    return stats


def read_unity_meta_guid(path: Path) -> str:
    if not path.exists():
        return ""

    try:
        with path.open("r", encoding="utf-8") as handle:
            for line in handle:
                if line.startswith("guid: "):
                    return line.strip().split(" ", 1)[1]
    except UnicodeDecodeError:
        return ""

    return ""


def extract_terminal_os_runtime_block(scene_text: str, scene_path: str) -> str:
    script_index = scene_text.find(TERMINAL_OS_RUNTIME_SCRIPT_GUID)
    if script_index < 0:
        fail(f"Scene TerminalOS runtime script block is missing in {scene_path}")

    block_start = scene_text.rfind("--- !u!114", 0, script_index)
    if block_start < 0:
        fail(f"Scene TerminalOS runtime MonoBehaviour block is malformed in {scene_path}")

    block_end = scene_text.find("\n--- !u!", script_index)
    if block_end < 0:
        block_end = len(scene_text)

    return scene_text[block_start:block_end]


def collect_yaml_reference_array(block: str, field_name: str, scene_path: str) -> list[int]:
    marker = f"\n  {field_name}:\n"
    start = block.find(marker)
    if start < 0:
        fail(f"Scene TerminalOS runtime array is missing in {scene_path}: {field_name}")

    refs: list[int] = []
    for line in block[start + len(marker):].splitlines():
        if line.startswith("  - "):
            match = re.search(r"\{fileID:\s*(-?\d+)\}", line)
            if match is None:
                fail(f"Scene TerminalOS runtime array entry is malformed in {scene_path}: {field_name}")

            file_id = int(match.group(1))
            if file_id == 0:
                fail(f"Scene TerminalOS runtime array contains a null reference in {scene_path}: {field_name}")
            refs.append(file_id)
            continue
        if line.startswith("  "):
            break

    return refs


def validate_evidence_graph(root: Path, rows: list[CsvPacketRow]) -> int:
    expected = {row.packet_id for row in rows}
    seen: set[str] = set()
    prerequisite_graph: dict[str, set[str]] = {packet_id: set() for packet_id in expected}
    for path in iter_applied_content_csv_sources(root, "graphs", "*_evidence_graph.csv"):
        with path.open("r", encoding="utf-8", newline="") as handle:
            reader = csv.DictReader(handle)
            if tuple(reader.fieldnames or ()) != EVIDENCE_GRAPH_HEADERS:
                fail(f"Evidence graph header mismatch: {path}")

            for line_number, row in enumerate(reader, start=2):
                packet_id = require_cell(row, "packet_id", line_number)
                if packet_id in seen:
                    fail(f"Evidence graph duplicate packet_id at {path}:{line_number}: {packet_id}")
                seen.add(packet_id)
                if packet_id not in expected:
                    fail(f"Evidence graph packet is not in baked CSV at {path}:{line_number}: {packet_id}")

                for field in (
                    "arc_id",
                    "depth_band",
                    "route_moment",
                    "evidence_type",
                    "truth_claim",
                    "player_decision",
                    "primary_surface",
                ):
                    require_cell(row, field, line_number)

                spoiler_tier = parse_int(require_cell(row, "spoiler_tier", line_number), "spoiler_tier", line_number)
                if spoiler_tier < 0 or spoiler_tier > 3:
                    fail(f"Evidence graph {path}:{line_number}: spoiler_tier out of range: {spoiler_tier}")

                primary_surface = require_cell(row, "primary_surface", line_number)
                if primary_surface not in EVIDENCE_GRAPH_PRIMARY_SURFACES:
                    fail(f"Evidence graph {path}:{line_number}: unsupported primary_surface={primary_surface!r}")

                for ref_field in ("prereq_packet_ids", "next_packet_ids"):
                    for ref in parse_packet_refs(row.get(ref_field, "")):
                        if ref == packet_id:
                            fail(f"Evidence graph {path}:{line_number}: {packet_id} references itself in {ref_field}")
                        if ref not in expected:
                            fail(f"Evidence graph {path}:{line_number}: unknown {ref_field} ref {ref!r}")
                        if ref_field == "prereq_packet_ids":
                            prerequisite_graph[packet_id].add(ref)

    missing_packets = sorted(expected.difference(seen))
    if missing_packets:
        fail("Evidence graph missing packets: " + ", ".join(missing_packets))
    validate_acyclic_packet_dependencies(prerequisite_graph, "Evidence graph")
    return len(seen)


def validate_publication_pages(root: Path, rows: list[CsvPacketRow]) -> tuple[int, int, int, int]:
    base = root / "Docs" / "Lore" / "AppliedContent"
    packet_ids = sorted({row.packet_id for row in rows})
    locales = sorted({row.locale for row in rows})
    rows_by_key = {(row.packet_id, row.locale): row for row in rows}
    counts: list[int] = []
    index_count = 0
    frontmatter_count = 0

    for folder in ("in_game_wiki", "external_site"):
        count = 0
        for locale in locales:
            index_path = base / folder / locale / "INDEX.md"
            if not index_path.exists():
                fail(f"Missing publication index: {index_path}")
            index_text = index_path.read_text(encoding="utf-8")
            if not index_text:
                fail(f"Empty publication index: {index_path}")
            for packet_id in packet_ids:
                if packet_id not in index_text:
                    fail(f"Publication index {index_path} missing packet id {packet_id}")
            index_count += 1

            for packet_id in packet_ids:
                path = base / folder / locale / f"{packet_id}.md"
                if not path.exists():
                    fail(f"Missing publication page: {path}")
                if path.stat().st_size <= 0:
                    fail(f"Empty publication page: {path}")
                row = rows_by_key.get((packet_id, locale))
                if row is None:
                    fail(f"Publication page has no CSV row: {path}")

                page_text = path.read_text(encoding="utf-8")
                require_page_line(page_text, path, f"packet_id: {packet_id}")
                require_page_line(page_text, path, f"release_set_id: {row.release_set_id}")
                require_page_line(page_text, path, f"article_id: {row.article_id}")
                require_page_line(page_text, path, f"unlock_id: {row.unlock_id}")
                require_page_line(page_text, path, f"poi_tags: {format_tag_tuple(row.poi_tags)}")
                require_page_line(page_text, path, f"biome_tags: {format_tag_tuple(row.biome_tags)}")
                require_page_line(page_text, path, f"locale: {locale}")
                require_page_line(page_text, path, f"surface: {folder}")
                require_page_line(page_text, path, "runtime_reads_markdown: false")
                require_page_line(page_text, path, f"direction: {direction_for_locale(locale)}")
                require_page_line(page_text, path, f"localization_status: {localization_status_from_flags(row.flags)}")
                require_page_line(page_text, path, f"localization_flags: {row.flags}")
                frontmatter_count += 1
                count += 1
        counts.append(count)

    return counts[0], counts[1], index_count, frontmatter_count


def validate_publication_surface_index(root: Path, rows: list[CsvPacketRow]) -> int:
    path = root / "Docs" / "Lore" / "AppliedContent" / "Publication_Surface_Index.csv"
    if not path.exists():
        fail(f"Missing publication surface index: {path}")

    source_by_key = {(row.packet_id, row.locale): row for row in rows}
    expected_count = len(rows) * 2
    seen: set[tuple[str, str, str]] = set()
    count = 0

    with path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        if tuple(reader.fieldnames or ()) != PUBLICATION_INDEX_HEADERS:
            fail(f"Publication surface index header mismatch: {path}")

        for line_number, item in enumerate(reader, start=2):
            surface = require_cell(item, "surface", line_number)
            locale = require_cell(item, "locale", line_number)
            packet_id = require_cell(item, "packet_id", line_number)
            if surface not in {"in_game_wiki", "external_site"}:
                fail(f"Publication surface index line {line_number}: unsupported surface={surface!r}")

            key = (surface, locale, packet_id)
            if key in seen:
                fail(f"Publication surface index line {line_number}: duplicate row {key}")
            seen.add(key)

            source = source_by_key.get((packet_id, locale))
            if source is None:
                fail(f"Publication surface index line {line_number}: no CSV row for {packet_id}/{locale}")

            if require_cell(item, "direction", line_number) != direction_for_locale(locale):
                fail(f"Publication surface index line {line_number}: direction mismatch")
            if require_cell(item, "release_set_id", line_number) != source.release_set_id:
                fail(f"Publication surface index line {line_number}: release_set_id mismatch")
            if require_cell(item, "article_id", line_number) != source.article_id:
                fail(f"Publication surface index line {line_number}: article_id mismatch")
            if require_cell(item, "unlock_id", line_number) != source.unlock_id:
                fail(f"Publication surface index line {line_number}: unlock_id mismatch")
            if require_cell(item, "localization_status", line_number) != localization_status_from_flags(source.flags):
                fail(f"Publication surface index line {line_number}: localization_status mismatch")
            if require_cell(item, "localization_flags", line_number) != str(source.flags):
                fail(f"Publication surface index line {line_number}: localization_flags mismatch")
            if item.get("poi_tags", "") != format_tag_tuple(source.poi_tags):
                fail(f"Publication surface index line {line_number}: poi_tags mismatch")
            if item.get("biome_tags", "") != format_tag_tuple(source.biome_tags):
                fail(f"Publication surface index line {line_number}: biome_tags mismatch")

            page_path = require_cell(item, "page_path", line_number)
            expected_page_path = f"{surface}/{locale}/{packet_id}.md"
            if page_path != expected_page_path:
                fail(f"Publication surface index line {line_number}: page_path mismatch")
            if not (root / "Docs" / "Lore" / "AppliedContent" / page_path).exists():
                fail(f"Publication surface index line {line_number}: missing page {page_path}")

            require_cell(item, "title", line_number)
            count += 1

    if count != expected_count:
        fail(f"Publication surface index row count mismatch: expected={expected_count} actual={count}")
    return count


def load_navigation_cluster_graph(root: Path, expected_packet_ids: set[str]) -> list[dict[str, str]]:
    path = root / "Docs" / "Lore" / "AppliedContent" / NAVIGATION_CLUSTER_GRAPH_PATH
    if not path.exists():
        fail(f"Missing navigation cluster graph: {path}")

    graph_rows: list[dict[str, str]] = []
    seen_packets: set[str] = set()
    with path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        if tuple(reader.fieldnames or ()) != EVIDENCE_GRAPH_HEADERS:
            fail(f"Navigation cluster graph header mismatch: {path}")

        for line_number, row in enumerate(reader, start=2):
            packet_id = require_cell(row, "packet_id", line_number)
            if packet_id in seen_packets:
                fail(f"Navigation cluster graph line {line_number}: duplicate packet_id={packet_id}")
            seen_packets.add(packet_id)
            if packet_id not in expected_packet_ids:
                fail(f"Navigation cluster graph line {line_number}: unknown packet_id={packet_id}")
            if require_cell(row, "arc_id", line_number) != "site_wiki_navigation_clusters":
                fail(f"Navigation cluster graph line {line_number}: unexpected arc_id")
            if require_cell(row, "primary_surface", line_number) != "external_site":
                fail(f"Navigation cluster graph line {line_number}: primary_surface must stay external_site")

            spoiler_tier = parse_int(require_cell(row, "spoiler_tier", line_number), "spoiler_tier", line_number)
            if spoiler_tier < 0 or spoiler_tier > 3:
                fail(f"Navigation cluster graph line {line_number}: spoiler_tier out of range")

            for field in (
                "depth_band",
                "route_moment",
                "evidence_type",
                "truth_claim",
                "player_decision",
            ):
                require_cell(row, field, line_number)

            for ref_field in ("prereq_packet_ids", "next_packet_ids"):
                for ref in parse_packet_refs(row.get(ref_field, "")):
                    if ref not in expected_packet_ids:
                        fail(f"Navigation cluster graph line {line_number}: unknown {ref_field} ref {ref!r}")

            graph_rows.append({key: row.get(key, "") for key in EVIDENCE_GRAPH_HEADERS})

    if len(graph_rows) != 5:
        fail(f"Navigation cluster graph row count mismatch: expected=5 actual={len(graph_rows)}")
    return graph_rows


def validate_publication_cluster_index(root: Path, rows: list[CsvPacketRow]) -> int:
    path = root / "Docs" / "Lore" / "AppliedContent" / "Publication_Cluster_Index.csv"
    if not path.exists():
        fail(f"Missing publication cluster index: {path}")

    locales = sorted({row.locale for row in rows})
    expected_packet_ids = {row.packet_id for row in rows}
    graph_rows = load_navigation_cluster_graph(root, expected_packet_ids)
    graph_by_packet = {row["packet_id"]: row for row in graph_rows}
    source_by_key = {(row.packet_id, row.locale): row for row in rows}
    expected_count = len(graph_rows) * len(locales) * 2
    seen: set[tuple[str, str, str]] = set()
    count = 0

    with path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        if tuple(reader.fieldnames or ()) != PUBLICATION_CLUSTER_INDEX_HEADERS:
            fail(f"Publication cluster index header mismatch: {path}")

        for line_number, item in enumerate(reader, start=2):
            surface = require_cell(item, "surface", line_number)
            locale = require_cell(item, "locale", line_number)
            packet_id = require_cell(item, "cluster_packet_id", line_number)
            if surface not in {"in_game_wiki", "external_site"}:
                fail(f"Publication cluster index line {line_number}: unsupported surface={surface!r}")

            key = (surface, locale, packet_id)
            if key in seen:
                fail(f"Publication cluster index line {line_number}: duplicate row {key}")
            seen.add(key)

            graph = graph_by_packet.get(packet_id)
            if graph is None:
                fail(f"Publication cluster index line {line_number}: packet is not a navigation cluster={packet_id}")

            source = source_by_key.get((packet_id, locale))
            if source is None:
                fail(f"Publication cluster index line {line_number}: no CSV row for {packet_id}/{locale}")

            graph_order = graph_rows.index(graph)
            if require_cell(item, "direction", line_number) != direction_for_locale(locale):
                fail(f"Publication cluster index line {line_number}: direction mismatch")
            if require_cell(item, "cluster_id", line_number) != cluster_id_from_route_moment(graph["route_moment"]):
                fail(f"Publication cluster index line {line_number}: cluster_id mismatch")
            if require_cell(item, "cluster_order", line_number) != str(graph_order):
                fail(f"Publication cluster index line {line_number}: cluster_order mismatch")
            if require_cell(item, "release_set_id", line_number) != source.release_set_id:
                fail(f"Publication cluster index line {line_number}: release_set_id mismatch")
            if require_cell(item, "article_id", line_number) != source.article_id:
                fail(f"Publication cluster index line {line_number}: article_id mismatch")
            if require_cell(item, "unlock_id", line_number) != source.unlock_id:
                fail(f"Publication cluster index line {line_number}: unlock_id mismatch")
            if require_cell(item, "spoiler_tier", line_number) != graph["spoiler_tier"]:
                fail(f"Publication cluster index line {line_number}: spoiler_tier mismatch")
            if require_cell(item, "primary_surface", line_number) != graph["primary_surface"]:
                fail(f"Publication cluster index line {line_number}: primary_surface mismatch")
            if item.get("prereq_packet_ids", "") != graph.get("prereq_packet_ids", ""):
                fail(f"Publication cluster index line {line_number}: prereq_packet_ids mismatch")
            if item.get("next_cluster_packet_ids", "") != graph.get("next_packet_ids", ""):
                fail(f"Publication cluster index line {line_number}: next_cluster_packet_ids mismatch")
            if require_cell(item, "localization_status", line_number) != localization_status_from_flags(source.flags):
                fail(f"Publication cluster index line {line_number}: localization_status mismatch")
            if require_cell(item, "localization_flags", line_number) != str(source.flags):
                fail(f"Publication cluster index line {line_number}: localization_flags mismatch")
            if item.get("poi_tags", "") != format_tag_tuple(source.poi_tags):
                fail(f"Publication cluster index line {line_number}: poi_tags mismatch")
            if item.get("biome_tags", "") != format_tag_tuple(source.biome_tags):
                fail(f"Publication cluster index line {line_number}: biome_tags mismatch")
            if require_cell(item, "truth_payload", line_number) != graph["truth_claim"]:
                fail(f"Publication cluster index line {line_number}: truth_payload mismatch")
            if require_cell(item, "player_question", line_number) != graph["player_decision"]:
                fail(f"Publication cluster index line {line_number}: player_question mismatch")

            page_path = require_cell(item, "page_path", line_number)
            expected_page_path = f"{surface}/{locale}/{packet_id}.md"
            if page_path != expected_page_path:
                fail(f"Publication cluster index line {line_number}: page_path mismatch")
            if not (root / "Docs" / "Lore" / "AppliedContent" / page_path).exists():
                fail(f"Publication cluster index line {line_number}: missing page {page_path}")

            require_cell(item, "title", line_number)
            count += 1

    if count != expected_count:
        fail(f"Publication cluster index row count mismatch: expected={expected_count} actual={count}")
    return count


def validate_route_cards(root: Path, rows: list[CsvPacketRow]) -> int:
    expected = {row.packet_id for row in rows}
    seen_cards: set[str] = set()
    covered_packets: set[str] = set()
    prerequisite_graph: dict[str, set[str]] = {packet_id: set() for packet_id in expected}
    for path in iter_applied_content_csv_sources(root, "route_cards", "*_route_cards.csv"):
        with path.open("r", encoding="utf-8", newline="") as handle:
            reader = csv.DictReader(handle)
            if tuple(reader.fieldnames or ()) != ROUTE_CARD_HEADERS:
                fail(f"Route cards header mismatch: {path}")

            for line_number, row in enumerate(reader, start=2):
                route_card_id = require_cell(row, "route_card_id", line_number)
                if route_card_id in seen_cards:
                    fail(f"Route cards duplicate route_card_id at {path}:{line_number}: {route_card_id}")
                seen_cards.add(route_card_id)

                require_cell(row, "phase_id", line_number)
                packet_refs = parse_packet_refs(require_cell(row, "packet_ids", line_number))
                if not packet_refs:
                    fail(f"Route cards {path}:{line_number}: packet_ids is empty")
                for packet_id in packet_refs:
                    if packet_id not in expected:
                        fail(f"Route cards {path}:{line_number}: unknown packet_id {packet_id!r}")
                    covered_packets.add(packet_id)

                for ref in parse_packet_refs(row.get("required_packet_ids", "")):
                    if ref not in expected:
                        fail(f"Route cards {path}:{line_number}: unknown required_packet_ids ref {ref!r}")
                    for packet_id in packet_refs:
                        prerequisite_graph[packet_id].add(ref)

                depth_min = parse_int(require_cell(row, "depth_min_m", line_number), "depth_min_m", line_number)
                depth_max = parse_int(require_cell(row, "depth_max_m", line_number), "depth_max_m", line_number)
                if depth_min < 0 or depth_max < depth_min:
                    fail(f"Route cards {path}:{line_number}: invalid depth bounds {depth_min}-{depth_max}")

                primary_surface = require_cell(row, "primary_surface", line_number)
                if primary_surface not in ROUTE_CARD_PRIMARY_SURFACES:
                    fail(f"Route cards {path}:{line_number}: unsupported primary_surface={primary_surface!r}")

                ending_pressure = require_cell(row, "ending_pressure", line_number)
                if ending_pressure not in ROUTE_CARD_ENDING_PRESSURES:
                    fail(f"Route cards {path}:{line_number}: unsupported ending_pressure={ending_pressure!r}")

                for field in ("world_object_hint", "player_question", "truth_payload", "replay_axis"):
                    require_cell(row, field, line_number)

    missing_packets = sorted(expected.difference(covered_packets))
    if missing_packets:
        fail("Route cards missing packets: " + ", ".join(missing_packets))
    validate_acyclic_packet_dependencies(prerequisite_graph, "Route cards")
    return len(seen_cards)


def format_hash_list(packet_ids: tuple[str, ...], *, hex_format: bool) -> str:
    if hex_format:
        return ";".join(f"0x{fnv1a32(packet_id):08X}" for packet_id in packet_ids)
    return ";".join(str(fnv1a32(packet_id)) for packet_id in packet_ids)


def validate_route_card_source_export(root: Path, rows: list[CsvPacketRow]) -> int:
    export_path = root / "Assets" / "_SourceData" / "DataMonolith" / "Narrative" / "applied_lore_route_cards.csv"
    if not export_path.exists():
        fail(f"Missing AppliedLore route-card source export: {export_path}")

    expected_packet_ids = {row.packet_id for row in rows}
    expected_by_route: dict[str, dict[str, str]] = {}
    for source_path in iter_applied_content_csv_sources(root, "route_cards", "*_route_cards.csv"):
        with source_path.open("r", encoding="utf-8", newline="") as handle:
            reader = csv.DictReader(handle)
            if tuple(reader.fieldnames or ()) != ROUTE_CARD_HEADERS:
                fail(f"Route cards header mismatch: {source_path}")
            for line_number, row in enumerate(reader, start=2):
                route_card_id = require_cell(row, "route_card_id", line_number)
                if route_card_id in expected_by_route:
                    fail(f"Route cards duplicate route_card_id at {source_path}:{line_number}: {route_card_id}")
                expected_by_route[route_card_id] = row

    seen: set[str] = set()
    with export_path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        if tuple(reader.fieldnames or ()) != ROUTE_CARD_SOURCE_HEADERS:
            fail("Route-card source export header mismatch.")

        for line_number, row in enumerate(reader, start=2):
            route_card_id = require_cell(row, "route_card_id", line_number)
            source = expected_by_route.get(route_card_id)
            if source is None:
                fail(f"Route-card source export line {line_number}: unknown route {route_card_id}")
            if route_card_id in seen:
                fail(f"Route-card source export line {line_number}: duplicate route {route_card_id}")
            seen.add(route_card_id)

            phase_id = require_cell(row, "phase_id", line_number)
            primary_surface = require_cell(row, "primary_surface", line_number)
            ending_pressure = require_cell(row, "ending_pressure", line_number)
            packet_ids = parse_packet_refs(require_cell(row, "packet_ids", line_number))
            required_packet_ids = parse_packet_refs(row.get("required_packet_ids", ""))

            if phase_id != source.get("phase_id", ""):
                fail(f"Route-card source export line {line_number}: phase mismatch")
            if require_cell(row, "depth_min_m", line_number) != source.get("depth_min_m", ""):
                fail(f"Route-card source export line {line_number}: depth_min mismatch")
            if require_cell(row, "depth_max_m", line_number) != source.get("depth_max_m", ""):
                fail(f"Route-card source export line {line_number}: depth_max mismatch")
            if primary_surface != source.get("primary_surface", ""):
                fail(f"Route-card source export line {line_number}: primary_surface mismatch")
            if ending_pressure != source.get("ending_pressure", ""):
                fail(f"Route-card source export line {line_number}: ending_pressure mismatch")
            if tuple(packet_ids) != parse_packet_refs(source.get("packet_ids", "")):
                fail(f"Route-card source export line {line_number}: packet_ids mismatch")
            if tuple(required_packet_ids) != parse_packet_refs(source.get("required_packet_ids", "")):
                fail(f"Route-card source export line {line_number}: required_packet_ids mismatch")

            for packet_id in packet_ids + required_packet_ids:
                if packet_id not in expected_packet_ids:
                    fail(f"Route-card source export line {line_number}: unknown packet id {packet_id}")

            route_hash = fnv1a32(route_card_id)
            phase_hash = fnv1a32(phase_id)
            pressure_hash = fnv1a32(ending_pressure)
            if parse_int(require_cell(row, "route_card_hash_hex", line_number), "route_card_hash_hex", line_number) != route_hash:
                fail(f"Route-card source export line {line_number}: bad route_card_hash_hex")
            if parse_int(require_cell(row, "route_card_hash_uint", line_number), "route_card_hash_uint", line_number) != route_hash:
                fail(f"Route-card source export line {line_number}: bad route_card_hash_uint")
            if parse_int(require_cell(row, "phase_hash_hex", line_number), "phase_hash_hex", line_number) != phase_hash:
                fail(f"Route-card source export line {line_number}: bad phase_hash_hex")
            if parse_int(require_cell(row, "phase_hash_uint", line_number), "phase_hash_uint", line_number) != phase_hash:
                fail(f"Route-card source export line {line_number}: bad phase_hash_uint")
            if parse_int(require_cell(row, "ending_pressure_hash_hex", line_number), "ending_pressure_hash_hex", line_number) != pressure_hash:
                fail(f"Route-card source export line {line_number}: bad ending_pressure_hash_hex")
            if parse_int(require_cell(row, "ending_pressure_hash_uint", line_number), "ending_pressure_hash_uint", line_number) != pressure_hash:
                fail(f"Route-card source export line {line_number}: bad ending_pressure_hash_uint")

            expected_surface_mask = ROUTE_CARD_SURFACE_MASKS.get(primary_surface)
            actual_surface_mask = parse_int(require_cell(row, "primary_surface_mask", line_number), "primary_surface_mask", line_number)
            if actual_surface_mask != expected_surface_mask:
                fail(f"Route-card source export line {line_number}: bad primary_surface_mask")
            if require_cell(row, "packet_hashes_hex", line_number) != format_hash_list(tuple(packet_ids), hex_format=True):
                fail(f"Route-card source export line {line_number}: packet_hashes_hex mismatch")
            if require_cell(row, "packet_hashes_uint", line_number) != format_hash_list(tuple(packet_ids), hex_format=False):
                fail(f"Route-card source export line {line_number}: packet_hashes_uint mismatch")
            if row.get("required_packet_hashes_hex", "") != format_hash_list(tuple(required_packet_ids), hex_format=True):
                fail(f"Route-card source export line {line_number}: required_packet_hashes_hex mismatch")
            if row.get("required_packet_hashes_uint", "") != format_hash_list(tuple(required_packet_ids), hex_format=False):
                fail(f"Route-card source export line {line_number}: required_packet_hashes_uint mismatch")

    missing_routes = sorted(set(expected_by_route).difference(seen))
    if missing_routes:
        fail("Route-card source export missing routes: " + ", ".join(missing_routes))
    return len(seen)


def load_route_source_records(root: Path) -> list[RouteSourceRecord]:
    export_path = root / "Assets" / "_SourceData" / "DataMonolith" / "Narrative" / "applied_lore_route_cards.csv"
    if not export_path.exists():
        fail(f"Missing AppliedLore route-card source export: {export_path}")

    records: list[RouteSourceRecord] = []
    seen: set[str] = set()
    with export_path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        if tuple(reader.fieldnames or ()) != ROUTE_CARD_SOURCE_HEADERS:
            fail("Route-card source export header mismatch.")

        for line_number, row in enumerate(reader, start=2):
            route_card_id = require_cell(row, "route_card_id", line_number)
            if route_card_id in seen:
                fail(f"Route-card source export line {line_number}: duplicate route {route_card_id}")
            seen.add(route_card_id)

            packet_ids = parse_packet_refs(require_cell(row, "packet_ids", line_number))
            required_packet_ids = parse_packet_refs(row.get("required_packet_ids", ""))
            records.append(
                RouteSourceRecord(
                    route_card_id=route_card_id,
                    route_card_hash=fnv1a32(route_card_id),
                    phase_hash=fnv1a32(require_cell(row, "phase_id", line_number)),
                    depth_min_m=float(require_cell(row, "depth_min_m", line_number)),
                    depth_max_m=float(require_cell(row, "depth_max_m", line_number)),
                    primary_surface_mask=parse_int(require_cell(row, "primary_surface_mask", line_number), "primary_surface_mask", line_number),
                    ending_pressure_hash=fnv1a32(require_cell(row, "ending_pressure", line_number)),
                    packet_hashes=tuple(fnv1a32(packet_id) for packet_id in packet_ids),
                    required_packet_hashes=tuple(fnv1a32(packet_id) for packet_id in required_packet_ids),
                    flags=parse_int(row.get("flags", "0") or "0", "flags", line_number),
                    line_number=line_number,
                )
            )

    return records


def validate_applied_route_records(root: Path, data: bytes, routes: SectionEntry) -> int:
    expected_records = load_route_source_records(root)
    expected_by_hash = {record.route_card_hash: record for record in expected_records}
    if len(expected_by_hash) != len(expected_records):
        fail("Route-card source export contains duplicate route hashes.")
    if routes.count != len(expected_records):
        fail(f"AppliedLoreRoutes count mismatch: blob={routes.count} source={len(expected_records)}")

    records = parse_applied_route_records(data, routes)
    previous_hash: int | None = None
    seen: set[int] = set()
    for record in records:
        if previous_hash is not None and record.route_card_hash <= previous_hash:
            fail(f"AppliedLoreRoutes records are not strictly sorted at index {record.index}")
        previous_hash = record.route_card_hash
        if record.route_card_hash in seen:
            fail(f"Duplicate AppliedLoreRoutes record hash at index {record.index}: 0x{record.route_card_hash:08X}")
        seen.add(record.route_card_hash)

        expected = expected_by_hash.get(record.route_card_hash)
        if expected is None:
            fail(f"Blob contains AppliedLoreRoutes record not present in source export at index {record.index}")

        if record.record_index != record.index:
            fail(f"AppliedLoreRoutes record_index mismatch at index {record.index}: {record.record_index}")
        if record.phase_hash != expected.phase_hash:
            fail(f"AppliedLoreRoutes phase hash mismatch for {expected.route_card_id}")
        if abs(record.depth_min_m - expected.depth_min_m) > 0.001 or abs(record.depth_max_m - expected.depth_max_m) > 0.001:
            fail(f"AppliedLoreRoutes depth mismatch for {expected.route_card_id}")
        if record.primary_surface_mask != expected.primary_surface_mask:
            fail(f"AppliedLoreRoutes surface mask mismatch for {expected.route_card_id}")
        if record.ending_pressure_hash != expected.ending_pressure_hash:
            fail(f"AppliedLoreRoutes ending pressure hash mismatch for {expected.route_card_id}")
        if record.flags != expected.flags:
            fail(f"AppliedLoreRoutes flags mismatch for {expected.route_card_id}")

        if record.packet_count != len(expected.packet_hashes):
            fail(f"AppliedLoreRoutes packet count mismatch for {expected.route_card_id}")
        if record.required_packet_count != len(expected.required_packet_hashes):
            fail(f"AppliedLoreRoutes required packet count mismatch for {expected.route_card_id}")
        if record.packet_count > APPLIED_LORE_ROUTE_PACKET_CAPACITY:
            fail(f"AppliedLoreRoutes packet count exceeds capacity for {expected.route_card_id}")
        if record.required_packet_count > APPLIED_LORE_ROUTE_PREREQUISITE_CAPACITY:
            fail(f"AppliedLoreRoutes prerequisite count exceeds capacity for {expected.route_card_id}")

        packet_count = int(record.packet_count)
        required_count = int(record.required_packet_count)
        if record.packet_hash_slots[:packet_count] != expected.packet_hashes:
            fail(f"AppliedLoreRoutes packet hashes mismatch for {expected.route_card_id}")
        if record.required_packet_hash_slots[:required_count] != expected.required_packet_hashes:
            fail(f"AppliedLoreRoutes prerequisite hashes mismatch for {expected.route_card_id}")
        if any(value != 0 for value in record.packet_hash_slots[packet_count:]):
            fail(f"AppliedLoreRoutes unused packet slots are not zero for {expected.route_card_id}")
        if any(value != 0 for value in record.required_packet_hash_slots[required_count:]):
            fail(f"AppliedLoreRoutes unused prerequisite slots are not zero for {expected.route_card_id}")
        if any(value != 0 for value in record.reserved):
            fail(f"AppliedLoreRoutes reserved fields are not zero for {expected.route_card_id}")

    missing = sorted(set(expected_by_hash).difference(seen))
    if missing:
        fail("AppliedLoreRoutes source records missing from blob: " + ", ".join(f"0x{value:08X}" for value in missing))
    return len(records)


def run(root: Path, *, source_only: bool = False) -> str:
    csv_path = root / "Assets" / "_SourceData" / "DataMonolith" / "Narrative" / "applied_lore_packets.csv"
    constants_path = root / "Assets" / "_Project" / "Scripts" / "Core" / "Generated" / "H8AppliedLoreHashes.cs"
    blob_path = root / "Assets" / "StreamingAssets" / "Hecton8" / "DataMonolith" / "static_data.h8bin"

    rows = load_csv(csv_path)
    marker_csv_fields, marker_pages = validate_no_visible_localization_markers(root, rows)
    constants = parse_generated_constants(constants_path)
    validate_generated_hashes(rows, constants)
    validate_source_route(root)
    binding_map_rows = validate_binding_map(root, rows)
    target_backlog_stats = validate_scene_binding_targets(root, rows)
    manual_policy_stats = validate_manual_binding_policy(root, rows)
    scene_placement_plan_stats = validate_scene_placement_plan(root, rows)
    graph_rows = validate_evidence_graph(root, rows)
    route_cards = validate_route_cards(root, rows)
    route_source_rows = validate_route_card_source_export(root, rows)
    wiki_pages, site_pages, index_pages, frontmatter_pages = validate_publication_pages(root, rows)
    publication_surface_rows = validate_publication_surface_index(root, rows)
    publication_cluster_rows = validate_publication_cluster_index(root, rows)
    serialized_bindings = count_serialized_authoring_bindings(root)
    scene_placement_serialized_rows = count_serialized_scene_placement_rows(root)
    scene_placement_covered_rows = count_scene_placement_covered_rows(root)
    scene_terminal_preview_rows = count_scene_terminal_preview_rows(root)
    scene_terminal_os_runtime_stats = count_scene_terminal_os_runtime_rows(
        root,
        scene_placement_plan_stats.terminal_rows,
    )
    scene_world_core_stats = validate_world_scene_core(root)

    packets = len({row.packet_id for row in rows})
    locales = len({row.locale for row in rows})
    if source_only:
        return (
            "AppliedLore source audit OK: "
            f"packets={packets} locales={locales} rows={len(rows)} "
            f"visible_marker_csv_fields={marker_csv_fields} "
            f"visible_marker_pages={marker_pages} "
            "source_route=ok "
            f"binding_map_rows={binding_map_rows} "
            f"target_backlog_rows={target_backlog_stats.rows} "
            f"target_prefab_candidate_rows={target_backlog_stats.prefab_candidate_rows} "
            f"target_auto_prefab_rows={target_backlog_stats.auto_prefab_rows} "
            f"target_manual_rows={target_backlog_stats.manual_rows} "
            f"target_candidate_paths={target_backlog_stats.existing_candidate_paths} "
            f"manual_policy_rows={manual_policy_stats.rows} "
            f"manual_terminal_policy_rows={manual_policy_stats.terminal_rows} "
            f"manual_discovery_policy_rows={manual_policy_stats.discovery_rows} "
            f"manual_template_prefab_rows={manual_policy_stats.template_prefab_rows} "
            f"manual_terminal_prefab_rows={manual_policy_stats.generated_terminal_prefab_rows} "
            f"placement_plan_rows={scene_placement_plan_stats.rows} "
            f"placement_terminal_rows={scene_placement_plan_stats.terminal_rows} "
            f"placement_discovery_rows={scene_placement_plan_stats.discovery_rows} "
            f"scene_placement_serialized_rows={scene_placement_serialized_rows} "
            f"scene_placement_covered_rows={scene_placement_covered_rows} "
            f"scene_terminal_preview_rows={scene_terminal_preview_rows} "
            f"scene_terminal_os_runtime_rows={scene_terminal_os_runtime_stats.rows} "
            f"scene_terminal_os_runtime_renderer_slots={scene_terminal_os_runtime_stats.renderer_slots} "
            f"scene_terminal_os_runtime_transform_slots={scene_terminal_os_runtime_stats.transform_slots} "
            f"scene_terminal_os_runtime_verified_slots={scene_terminal_os_runtime_stats.verified_slots} "
            f"scene_world_bytes={scene_world_core_stats.bytes} "
            f"scene_world_roots={scene_world_core_stats.roots} "
            f"scene_world_mapmagic_rows={scene_world_core_stats.mapmagic_rows} "
            f"scene_world_terrain_rows={scene_world_core_stats.terrain_rows} "
            f"scene_world_terrain_collider_rows={scene_world_core_stats.terrain_collider_rows} "
            f"scene_world_dependency_warnings={scene_world_core_stats.dependency_warnings} "
            f"scene_world_crest_markers={scene_world_core_stats.crest_markers} "
            f"scene_world_ocean_prefab_assets={scene_world_core_stats.ocean_prefab_assets} "
            f"scene_world_ocean_prefab_refs={scene_world_core_stats.ocean_prefab_scene_refs} "
            f"graph_rows={graph_rows} "
            f"route_cards={route_cards} route_source_rows={route_source_rows} "
            f"wiki_pages={wiki_pages} site_pages={site_pages} index_pages={index_pages} "
            f"publication_frontmatter_pages={frontmatter_pages} "
            f"publication_surface_rows={publication_surface_rows} "
            f"publication_cluster_rows={publication_cluster_rows} "
            f"scene_bindings={serialized_bindings.scene_bindings} "
            f"prefab_bindings={serialized_bindings.prefab_bindings} "
            f"asset_bindings={serialized_bindings.asset_bindings} "
            f"authoring_bindings={serialized_bindings.total}"
        )

    blob, _entries, localization, applied, routes = parse_blob(blob_path)
    validate_applied_records(rows, blob, localization, applied)
    applied_routes = validate_applied_route_records(root, blob, routes)
    return (
        "AppliedLore audit OK: "
        f"packets={packets} locales={locales} rows={len(rows)} "
        f"visible_marker_csv_fields={marker_csv_fields} "
        f"visible_marker_pages={marker_pages} "
        f"blob_bytes={len(blob)} localization_bytes={localization.count} "
        f"applied_records={applied.count} applied_routes={applied_routes} source_route=ok "
        f"binding_map_rows={binding_map_rows} "
        f"target_backlog_rows={target_backlog_stats.rows} "
        f"target_prefab_candidate_rows={target_backlog_stats.prefab_candidate_rows} "
        f"target_auto_prefab_rows={target_backlog_stats.auto_prefab_rows} "
        f"target_manual_rows={target_backlog_stats.manual_rows} "
        f"target_candidate_paths={target_backlog_stats.existing_candidate_paths} "
        f"manual_policy_rows={manual_policy_stats.rows} "
        f"manual_terminal_policy_rows={manual_policy_stats.terminal_rows} "
        f"manual_discovery_policy_rows={manual_policy_stats.discovery_rows} "
        f"manual_template_prefab_rows={manual_policy_stats.template_prefab_rows} "
        f"manual_terminal_prefab_rows={manual_policy_stats.generated_terminal_prefab_rows} "
        f"placement_plan_rows={scene_placement_plan_stats.rows} "
        f"placement_terminal_rows={scene_placement_plan_stats.terminal_rows} "
        f"placement_discovery_rows={scene_placement_plan_stats.discovery_rows} "
        f"scene_placement_serialized_rows={scene_placement_serialized_rows} "
        f"scene_placement_covered_rows={scene_placement_covered_rows} "
        f"scene_terminal_preview_rows={scene_terminal_preview_rows} "
        f"scene_terminal_os_runtime_rows={scene_terminal_os_runtime_stats.rows} "
        f"scene_terminal_os_runtime_renderer_slots={scene_terminal_os_runtime_stats.renderer_slots} "
        f"scene_terminal_os_runtime_transform_slots={scene_terminal_os_runtime_stats.transform_slots} "
        f"scene_terminal_os_runtime_verified_slots={scene_terminal_os_runtime_stats.verified_slots} "
        f"scene_world_bytes={scene_world_core_stats.bytes} "
        f"scene_world_roots={scene_world_core_stats.roots} "
        f"scene_world_mapmagic_rows={scene_world_core_stats.mapmagic_rows} "
        f"scene_world_terrain_rows={scene_world_core_stats.terrain_rows} "
        f"scene_world_terrain_collider_rows={scene_world_core_stats.terrain_collider_rows} "
        f"scene_world_dependency_warnings={scene_world_core_stats.dependency_warnings} "
        f"scene_world_crest_markers={scene_world_core_stats.crest_markers} "
        f"scene_world_ocean_prefab_assets={scene_world_core_stats.ocean_prefab_assets} "
        f"scene_world_ocean_prefab_refs={scene_world_core_stats.ocean_prefab_scene_refs} "
        f"graph_rows={graph_rows} "
        f"route_cards={route_cards} route_source_rows={route_source_rows} "
        f"wiki_pages={wiki_pages} site_pages={site_pages} index_pages={index_pages} "
        f"publication_frontmatter_pages={frontmatter_pages} "
        f"publication_surface_rows={publication_surface_rows} "
        f"publication_cluster_rows={publication_cluster_rows} "
        f"scene_bindings={serialized_bindings.scene_bindings} "
        f"prefab_bindings={serialized_bindings.prefab_bindings} "
        f"asset_bindings={serialized_bindings.asset_bindings} "
        f"authoring_bindings={serialized_bindings.total}"
    )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=".", help="Repository root.")
    parser.add_argument(
        "--source-only",
        action="store_true",
        help="Validate AppliedLore source/export/page coverage without requiring static_data.h8bin to be freshly baked.",
    )
    args = parser.parse_args()
    root = Path(args.root).resolve()
    try:
        print(run(root, source_only=args.source_only))
        return 0
    except AuditFailure as exc:
        print(f"AppliedLore audit FAILED: {exc}")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
