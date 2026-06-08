#!/usr/bin/env python3
"""Validate AppliedLore authoring fields against runtime/publication bridges."""

from __future__ import annotations

import argparse
import csv
import json
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any

TOOL_DIR = Path(__file__).resolve().parent
if str(TOOL_DIR) not in sys.path:
    sys.path.insert(0, str(TOOL_DIR))

from AppliedLoreImporter import (  # noqa: E402
    LOCALIZED_TEXT_FIELDS,
    SURFACE_MASKS,
    TARGET_LOCALES,
    collect_packets,
    resolve_packet_surface_mask,
    sanitize_localized_text,
)


RUNTIME_SURFACE_ALIASES = {
    "audio_subtitle": "audio",
}
AUTHORING_ONLY_SURFACES = {
    "environmental_label",
    "evidence_caption",
    "image_brief",
    "spoiler_policy",
    "string_pool_key",
}
AUTHORING_SURFACE_ALIASES = {
    "codex": "in_game_wiki",
    "pda_codex": "in_game_wiki",
    "pda_log": "field_note",
    "scanner_entry": "scanner",
    "terminal_note": "terminal",
    "website_article": "external_site",
    "wiki_article": "in_game_wiki",
}
LOCALIZED_FIELD_ALIASES = {
    "external_site": ("website_article",),
    "field_note": ("pda_log",),
    "in_game_wiki": ("codex", "pda_codex", "wiki_article"),
    "scanner": ("scanner_entry",),
    "terminal": ("terminal_note",),
}
PUBLICATION_ONLY_FIELDS = {
    "external_site_article",
    "external_site_article_path",
}
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
MOJIBAKE_CODEPOINTS = {
    "\ufffd",
}
CYRILLIC_MOJIBAKE_LEADS = {"\u00d0", "\u00d1"}
LATIN_MOJIBAKE_LEADS = {"\u00c2", "\u00c3"}
TEXT_MARKERS = (
    "LOC HOLD",
    "same as English",
    "placeholder",
)
NAVIGATION_CLUSTER_GRAPH_HEADERS = (
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
NAVIGATION_CLUSTER_GRAPH_PATH = "graphs/RS084_SITE_WIKI_NAVIGATION_CLUSTERS_evidence_graph.csv"
PUBLICATION_SURFACES = (
    ("in_game_wiki", "in_game_wiki"),
    ("external_site", "external_site"),
)


class AppliedLoreAuthoringBridgeError(RuntimeError):
    pass


@dataclass(frozen=True)
class BridgeStats:
    packets: int
    localized_rows: int
    runtime_fields: int
    publication_article_fields: int
    issues: tuple[str, ...]


def pascal_to_snake(value: str) -> str:
    text = re.sub(r"(.)([A-Z][a-z]+)", r"\1_\2", value)
    text = re.sub(r"([a-z0-9])([A-Z])", r"\1_\2", text)
    return text.lower()


def is_relative_to(path: Path, base: Path) -> bool:
    try:
        path.relative_to(base)
        return True
    except ValueError:
        return False


def parse_packet_refs(value: str) -> tuple[str, ...]:
    if not value:
        return ()
    return tuple(part.strip() for part in value.split(";") if part.strip())


def extract_csharp_applied_lore_surfaces(path: Path) -> set[str]:
    if not path.exists():
        raise AppliedLoreAuthoringBridgeError(f"Missing H8DataMonolithTypes source: {path}")

    text = path.read_text(encoding="utf-8")
    match = re.search(r"public\s+enum\s+H8AppliedLoreSurface\s*:\s*byte\s*\{(?P<body>.*?)\}", text, re.S)
    if match is None:
        raise AppliedLoreAuthoringBridgeError(f"Missing H8AppliedLoreSurface enum: {path}")

    surfaces: set[str] = set()
    for raw_line in match.group("body").splitlines():
        line = raw_line.split("//", 1)[0].strip().rstrip(",")
        if not line:
            continue
        name = line.split("=", 1)[0].strip()
        if not name or not re.match(r"^[A-Za-z_][A-Za-z0-9_]*$", name):
            continue
        surfaces.add(pascal_to_snake(name))
    return surfaces


def importer_runtime_surfaces() -> set[str]:
    return set(LOCALIZED_TEXT_FIELDS)


def validate_runtime_surface_contract(root: Path, issues: list[str]) -> None:
    enum_path = root / "Assets" / "_Project" / "Scripts" / "Data" / "Monolith" / "H8DataMonolithTypes.cs"
    enum_surfaces = extract_csharp_applied_lore_surfaces(enum_path)
    importer_surfaces = importer_runtime_surfaces()
    mask_surfaces = {name for name in SURFACE_MASKS if name not in RUNTIME_SURFACE_ALIASES}

    if enum_surfaces != importer_surfaces:
        issues.append(
            "runtime/importer surface mismatch: "
            f"enum_only={sorted(enum_surfaces - importer_surfaces)} "
            f"importer_only={sorted(importer_surfaces - enum_surfaces)}"
        )
    if not importer_surfaces.issubset(mask_surfaces):
        issues.append(
            "importer localized fields missing surface masks: "
            + ", ".join(sorted(importer_surfaces - mask_surfaces))
        )


def validate_publication_exporter_contract(root: Path, issues: list[str]) -> None:
    exporter_path = root / "Tools" / "AppliedLorePageExporter.py"
    if not exporter_path.exists():
        issues.append(f"missing publication exporter: {exporter_path}")
        return

    text = exporter_path.read_text(encoding="utf-8")
    for field in sorted(PUBLICATION_ONLY_FIELDS):
        if field not in text:
            issues.append(f"publication-only field is not read by AppliedLorePageExporter: {field}")


def collect_source_only_packet_ids(root: Path, issues: list[str]) -> set[str]:
    release_dir = root / "Docs" / "Lore" / "AppliedContent" / "release_sets"
    packet_ids: set[str] = set()
    for manifest_path in sorted(release_dir.glob("*_manifest.json"), key=lambda item: item.name.lower()):
        try:
            manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
        except (UnicodeDecodeError, json.JSONDecodeError) as exc:
            issues.append(f"source-only manifest unreadable for navigation staging: {manifest_path}: {exc}")
            continue
        if not isinstance(manifest, dict):
            continue
        if manifest.get("canonical_importer_ready") is not False:
            continue
        if any(manifest.get(key) is True for key in RELEASE_READY_KEYS):
            continue

        manifest_packets = manifest.get("packets", [])
        if isinstance(manifest_packets, list):
            for raw_packet_id in manifest_packets:
                packet_id = str(raw_packet_id).strip()
                if packet_id:
                    packet_ids.add(packet_id)

        sources = manifest.get("packet_sources", [])
        if not isinstance(sources, list):
            continue
        for raw_source in sources:
            if not isinstance(raw_source, str) or not raw_source.endswith(".json"):
                continue
            source_path = root / raw_source
            try:
                source_data = json.loads(source_path.read_text(encoding="utf-8-sig"))
            except (OSError, UnicodeDecodeError, json.JSONDecodeError):
                continue
            packets = source_data.get("packets")
            if not isinstance(packets, list):
                packets = [source_data]
            for packet in packets:
                if not isinstance(packet, dict):
                    continue
                packet_id = str(packet.get("packet_id", "")).strip()
                if packet_id:
                    packet_ids.add(packet_id)
    return packet_ids


def validate_publication_navigation_contract(root: Path, packets: list[dict[str, Any]], issues: list[str]) -> None:
    base = root / "Docs" / "Lore" / "AppliedContent"
    graph_path = base / NAVIGATION_CLUSTER_GRAPH_PATH
    if not graph_path.exists():
        issues.append(f"missing publication navigation cluster graph: {NAVIGATION_CLUSTER_GRAPH_PATH}")
        return

    packet_ids = {str(packet.get("packet_id", "")).strip() for packet in packets}
    source_only_packet_ids = collect_source_only_packet_ids(root, issues)
    authoring_packet_ids = packet_ids.union(source_only_packet_ids)
    seen_packet_ids: set[str] = set()
    rows = 0
    with graph_path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        if tuple(reader.fieldnames or ()) != NAVIGATION_CLUSTER_GRAPH_HEADERS:
            issues.append(f"publication navigation cluster graph header mismatch: {NAVIGATION_CLUSTER_GRAPH_PATH}")
            return

        for row in reader:
            rows += 1
            packet_id = str(row.get("packet_id", "")).strip()
            if not packet_id:
                issues.append(f"publication navigation cluster graph row {rows}: missing packet_id")
                continue
            if packet_id in seen_packet_ids:
                issues.append(
                    f"{packet_id}: duplicate navigation cluster packet in {NAVIGATION_CLUSTER_GRAPH_PATH}"
                )
                continue
            seen_packet_ids.add(packet_id)

            packet_is_canonical = packet_id in packet_ids
            if packet_id not in authoring_packet_ids:
                issues.append(
                    f"{packet_id}: navigation cluster packet missing from canonical AppliedLore packet set"
                )
                continue

            if str(row.get("arc_id", "")).strip() != "site_wiki_navigation_clusters":
                issues.append(f"{packet_id}: navigation cluster graph arc_id must be site_wiki_navigation_clusters")
            if str(row.get("primary_surface", "")).strip() != "external_site":
                issues.append(f"{packet_id}: navigation cluster graph primary_surface must be external_site")

            for field in (
                "depth_band",
                "route_moment",
                "evidence_type",
                "truth_claim",
                "player_decision",
            ):
                if not str(row.get(field, "")).strip():
                    issues.append(f"{packet_id}: navigation cluster graph missing {field}")

            spoiler_tier = str(row.get("spoiler_tier", "")).strip()
            try:
                parsed_tier = int(spoiler_tier)
            except ValueError:
                issues.append(f"{packet_id}: navigation cluster graph bad spoiler_tier={spoiler_tier!r}")
            else:
                if parsed_tier < 0 or parsed_tier > 3:
                    issues.append(f"{packet_id}: navigation cluster graph spoiler_tier out of range={parsed_tier}")

            for ref_field in ("prereq_packet_ids", "next_packet_ids"):
                for ref in parse_packet_refs(str(row.get(ref_field, ""))):
                    if packet_is_canonical and ref in source_only_packet_ids:
                        issues.append(
                            f"{packet_id}: runtime-active navigation cluster {ref_field} "
                            f"points at staged packet {ref}"
                        )
                    elif ref not in (packet_ids if packet_is_canonical else authoring_packet_ids):
                        issues.append(
                            f"{packet_id}: navigation cluster graph unknown {ref_field} ref {ref}"
                        )

    if rows == 0:
        issues.append(f"publication navigation cluster graph is empty: {NAVIGATION_CLUSTER_GRAPH_PATH}")


def validate_publication_page_artifacts(root: Path, packets: list[dict[str, Any]], issues: list[str]) -> None:
    base = root / "Docs" / "Lore" / "AppliedContent"
    for packet in packets:
        packet_id = str(packet.get("packet_id", "")).strip()
        if not packet_id:
            continue
        try:
            surface_mask = resolve_packet_surface_mask(packet)
        except ValueError:
            continue

        for folder, surface_key in PUBLICATION_SURFACES:
            if (surface_mask & SURFACE_MASKS[surface_key]) == 0:
                continue
            for locale in TARGET_LOCALES:
                path = base / folder / locale / f"{packet_id}.md"
                if not path.exists():
                    issues.append(
                        f"publication_page:{packet_id}:{surface_key}:{locale}: "
                        "missing generated page"
                    )
                    continue

                text = path.read_text(encoding="utf-8")
                required_markers = (
                    (f"packet_id: {packet_id}", "wrong or missing packet_id marker"),
                    (f"locale: {locale}", "wrong or missing locale marker"),
                    (f"surface: {surface_key}", "wrong or missing surface marker"),
                    ("source: AppliedContent packet JSON", "missing generated source marker"),
                    ("runtime_reads_markdown: false", "missing runtime markdown exclusion marker"),
                )
                for marker, message in required_markers:
                    if marker not in text:
                        issues.append(f"publication_page:{packet_id}:{surface_key}:{locale}: {message}")


def publication_text_issues(text: str) -> list[str]:
    found: list[str] = []
    for marker in TEXT_MARKERS:
        if marker.lower() in text.lower():
            found.append(f"marker {marker!r}")
    bad_chars = sorted(
        {
            f"U+{ord(char):04X}"
            for char in text
            if char in MOJIBAKE_CODEPOINTS or 0x80 <= ord(char) <= 0x9F
        }
    )
    if bad_chars:
        found.append("mojibake codepoints " + ",".join(bad_chars))
    if has_utf8_mojibake_sequence(text):
        found.append("mojibake sequence")
    if "????" in text:
        found.append("question-mark replacement run")
    return found


def has_utf8_mojibake_sequence(text: str) -> bool:
    for index, char in enumerate(text[:-1]):
        next_codepoint = ord(text[index + 1])
        if char in CYRILLIC_MOJIBAKE_LEADS and 0x80 <= next_codepoint <= 0xBF:
            return True
        if char in LATIN_MOJIBAKE_LEADS and 0x80 <= next_codepoint <= 0xBF:
            return True
    return False


def iter_player_facing_localized_fields() -> tuple[str, ...]:
    fields: set[str] = set(LOCALIZED_TEXT_FIELDS)
    fields.update(PUBLICATION_ONLY_FIELDS)
    for aliases in LOCALIZED_FIELD_ALIASES.values():
        fields.update(aliases)
    return tuple(sorted(fields))


def validate_strict_localized_text(packet_id: str, locale: str, row: dict[str, Any], issues: list[str]) -> None:
    for field in iter_player_facing_localized_fields():
        value = row.get(field)
        if not isinstance(value, str) or not value.strip():
            continue
        for issue in publication_text_issues(value):
            issues.append(f"localized_text:{packet_id}:{field}:{locale}: {issue}")


def has_runtime_text(localized: dict[str, Any], field: str) -> bool:
    for candidate in (field, *LOCALIZED_FIELD_ALIASES.get(field, ())):
        value = localized.get(candidate, "")
        if isinstance(value, str) and sanitize_localized_text(value).strip():
            return True
    return False


def read_publication_article_path(root: Path, relative_path: str) -> str:
    content_base = (root / "Docs" / "Lore" / "AppliedContent").resolve()
    path = (content_base / relative_path).resolve()
    if not is_relative_to(path, content_base):
        raise AppliedLoreAuthoringBridgeError(f"external_site_article_path escapes AppliedContent: {relative_path}")
    if not path.exists():
        raise AppliedLoreAuthoringBridgeError(f"external_site_article_path missing: {relative_path}")
    return path.read_text(encoding="utf-8")


def normalize_authoring_surface(surface: str) -> str:
    alias = AUTHORING_SURFACE_ALIASES.get(surface, surface)
    return RUNTIME_SURFACE_ALIASES.get(alias, alias)


def resolve_bridge_surface_mask(packet: dict[str, Any], packet_id: str, issues: list[str]) -> int:
    surfaces = packet.get("surfaces")
    if not isinstance(surfaces, list):
        try:
            return resolve_packet_surface_mask(packet)
        except ValueError as exc:
            issues.append(str(exc))
            return 0

    surface_mask = 0
    for surface in surfaces:
        raw_name = str(surface)
        if raw_name in AUTHORING_ONLY_SURFACES:
            continue
        if raw_name in PUBLICATION_ONLY_FIELDS:
            issues.append(f"surface_contract:{raw_name}:{packet_id}: publication-only field listed as runtime surface")
            continue

        resolved = normalize_authoring_surface(raw_name)
        if resolved in AUTHORING_ONLY_SURFACES:
            continue
        mask = SURFACE_MASKS.get(resolved)
        if mask is None:
            issues.append(f"surface_contract:{raw_name}:{packet_id}: unknown authoring surface")
            continue
        surface_mask |= mask
    return surface_mask


def validate_packet_bridge(
    root: Path,
    packet: dict[str, Any],
    issues: list[str],
    *,
    strict_localized_text: bool = False,
) -> tuple[int, int]:
    packet_id = str(packet.get("packet_id", ""))
    localized_rows = 0
    publication_article_fields = 0

    surface_mask = resolve_bridge_surface_mask(packet, packet_id, issues)
    external_site_enabled = (surface_mask & SURFACE_MASKS["external_site"]) != 0
    localized = packet.get("localized", {})
    if not isinstance(localized, dict):
        issues.append(f"{packet_id}: localized must be an object")
        return localized_rows, publication_article_fields

    missing_locales = [locale for locale in TARGET_LOCALES if locale not in localized]
    if missing_locales:
        issues.append(f"{packet_id}: missing locales for bridge scan: {', '.join(missing_locales[:8])}")

    publication_article_locales: set[str] = set()
    for locale, row in sorted(localized.items()):
        if not isinstance(row, dict):
            issues.append(f"{packet_id}/{locale}: localized row must be an object")
            continue
        localized_rows += 1
        if strict_localized_text:
            validate_strict_localized_text(packet_id, locale, row, issues)

        for field in LOCALIZED_TEXT_FIELDS:
            if (surface_mask & SURFACE_MASKS[field]) == 0:
                continue
            if not has_runtime_text(row, field):
                issues.append(
                    f"runtime_localized:{packet_id}:{field}:{locale}: "
                    "surface enabled but localized field missing"
                )

        article_text = row.get("external_site_article")
        alias_article_text = row.get("website_article")
        article_path = row.get("external_site_article_path")
        if isinstance(article_text, str) and article_text.strip():
            publication_article_fields += 1
            publication_article_locales.add(locale)
            if not external_site_enabled:
                issues.append(
                    f"publication_surface:{packet_id}:{locale}: "
                    "external_site_article present but external_site surface disabled"
                )
            for issue in publication_text_issues(article_text):
                issues.append(f"{packet_id}/{locale}/external_site_article: {issue}")

        if isinstance(alias_article_text, str) and alias_article_text.strip():
            publication_article_fields += 1
            publication_article_locales.add(locale)
            if not external_site_enabled:
                issues.append(
                    f"publication_surface:{packet_id}:{locale}: "
                    "website_article present but external_site surface disabled"
                )
            for issue in publication_text_issues(alias_article_text):
                issues.append(f"{packet_id}/{locale}/website_article: {issue}")

        if isinstance(article_path, str) and article_path.strip():
            publication_article_fields += 1
            publication_article_locales.add(locale)
            if not external_site_enabled:
                issues.append(
                    f"publication_surface:{packet_id}:{locale}: "
                    "external_site_article_path present but external_site surface disabled"
                )
            try:
                path_text = read_publication_article_path(root, article_path)
            except AppliedLoreAuthoringBridgeError as exc:
                issues.append(f"{packet_id}/{locale}/external_site_article_path: {exc}")
                continue
            for issue in publication_text_issues(path_text):
                issues.append(f"{packet_id}/{locale}/external_site_article_path: {issue}")

    if external_site_enabled and publication_article_locales:
        missing_article_locales = [
            locale
            for locale in TARGET_LOCALES
            if locale in localized and locale not in publication_article_locales
        ]
        if missing_article_locales:
            issues.append(
                f"{packet_id}: publication article body/path present for some locales "
                f"but missing for locales: {', '.join(missing_article_locales[:8])}"
            )

    return localized_rows, publication_article_fields


def format_sample_list(values: list[str], *, limit: int = 8) -> str:
    unique_values = sorted(set(values))
    samples = ",".join(unique_values[:limit])
    more = "" if len(unique_values) <= limit else f"; +{len(unique_values) - limit} more"
    return f"count={len(unique_values)} samples={samples}{more}"


def compress_repeated_bridge_issues(issues: list[str]) -> list[str]:
    surface_grouped: dict[tuple[str, str], list[str]] = {}
    publication_grouped: dict[tuple[str, str], list[str]] = {}
    publication_page_grouped: dict[tuple[str, str, str], list[str]] = {}
    runtime_grouped: dict[tuple[str, str, str], list[str]] = {}
    localized_text_grouped: dict[tuple[str, str, str], list[str]] = {}
    retained: list[str] = []
    for issue in issues:
        if issue.startswith("surface_contract:"):
            _prefix, surface, packet_id, message = issue.split(":", 3)
            message = message.strip()
            surface_grouped.setdefault((surface, message), []).append(packet_id)
            continue

        if issue.startswith("publication_surface:"):
            _prefix, packet_id, locale, message = issue.split(":", 3)
            message = message.strip()
            publication_grouped.setdefault((packet_id, message), []).append(locale)
            continue

        if issue.startswith("publication_page:"):
            _prefix, packet_id, surface_key, locale, message = issue.split(":", 4)
            message = message.strip()
            publication_page_grouped.setdefault((packet_id, surface_key, message), []).append(locale)
            continue

        if issue.startswith("runtime_localized:"):
            _prefix, packet_id, field, locale, message = issue.split(":", 4)
            message = message.strip()
            runtime_grouped.setdefault((packet_id, field, message), []).append(locale)
            continue

        if issue.startswith("localized_text:"):
            _prefix, packet_id, field, locale, message = issue.split(":", 4)
            message = message.strip()
            localized_text_grouped.setdefault((packet_id, field, message), []).append(locale)
            continue

        retained.append(issue)

    for (surface, message), packet_ids in sorted(surface_grouped.items()):
        retained.append(
            f"{message}: surface={surface!r} packets "
            f"{format_sample_list(packet_ids)}"
        )

    for (packet_id, message), locales in sorted(publication_grouped.items()):
        retained.append(f"{packet_id}: {message}: locales {format_sample_list(locales)}")

    for (packet_id, surface_key, message), locales in sorted(publication_page_grouped.items()):
        retained.append(
            f"{packet_id}/{surface_key}: {message}: locales {format_sample_list(locales)}"
        )

    for (packet_id, field, message), locales in sorted(runtime_grouped.items()):
        retained.append(f"{packet_id}/{field}: {message}: locales {format_sample_list(locales)}")

    for (packet_id, field, message), locales in sorted(localized_text_grouped.items()):
        retained.append(f"{packet_id}/{field}: localized text {message}: locales {format_sample_list(locales)}")

    return retained


def load_packet_json_scope(root: Path) -> list[dict[str, Any]]:
    packet_dir = root / "Docs" / "Lore" / "AppliedContent" / "packets"
    packets: list[dict[str, Any]] = []
    for path in sorted(packet_dir.glob("*.json"), key=lambda item: item.name.lower()):
        with path.open("r", encoding="utf-8") as handle:
            data = json.load(handle)
        if not isinstance(data, dict):
            raise AppliedLoreAuthoringBridgeError(f"Packet JSON root must be object: {path}")

        if isinstance(data.get("packets"), list):
            for packet in data["packets"]:
                if not isinstance(packet, dict):
                    raise AppliedLoreAuthoringBridgeError(f"Packet bundle contains non-object packet: {path}")
                packet = dict(packet)
                packet.setdefault("_source_path", str(path))
                packets.append(packet)
        else:
            packet = dict(data)
            packet.setdefault("_source_path", str(path))
            packets.append(packet)
    return packets


def validate_authoring_bridge(
    root: Path,
    max_issues: int = 80,
    *,
    include_all_packet_json: bool = False,
    strict_localized_text: bool = False,
) -> BridgeStats:
    issues: list[str] = []
    validate_runtime_surface_contract(root, issues)
    validate_publication_exporter_contract(root, issues)

    packets = load_packet_json_scope(root) if include_all_packet_json else collect_packets(root)
    if not include_all_packet_json:
        validate_publication_navigation_contract(root, packets, issues)
        validate_publication_page_artifacts(root, packets, issues)

    localized_rows = 0
    publication_article_fields = 0
    for packet in packets:
        row_count, article_count = validate_packet_bridge(
            root,
            packet,
            issues,
            strict_localized_text=strict_localized_text,
        )
        localized_rows += row_count
        publication_article_fields += article_count

    issues = compress_repeated_bridge_issues(issues)
    if len(issues) > max_issues:
        issues = issues[:max_issues] + [f"... {len(issues) - max_issues} more issues"]

    return BridgeStats(
        packets=len(packets),
        localized_rows=localized_rows,
        runtime_fields=len(LOCALIZED_TEXT_FIELDS),
        publication_article_fields=publication_article_fields,
        issues=tuple(issues),
    )


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=".", help="Repository root")
    parser.add_argument("--max-issues", type=int, default=80)
    parser.add_argument(
        "--include-all-packet-json",
        action="store_true",
        help="Scan every packet JSON, including authoring backlog not selected by canonical importer manifests.",
    )
    parser.add_argument(
        "--strict-localized-text",
        action="store_true",
        help="Fail on LOC HOLD, placeholder text, and mojibake in any player-facing localized field.",
    )
    args = parser.parse_args(argv)

    root = Path(args.root).resolve()
    try:
        stats = validate_authoring_bridge(
            root,
            max_issues=args.max_issues,
            include_all_packet_json=args.include_all_packet_json,
            strict_localized_text=args.strict_localized_text,
        )
    except AppliedLoreAuthoringBridgeError as exc:
        print(f"AppliedLore authoring bridge failed:\n- {exc}", file=sys.stderr)
        return 1

    if stats.issues:
        print(
            "AppliedLore authoring bridge failed: "
            f"packets={stats.packets} rows={stats.localized_rows} "
            f"publication_article_fields={stats.publication_article_fields}",
            file=sys.stderr,
        )
        for issue in stats.issues:
            print(f"- {issue}", file=sys.stderr)
        return 1

    print(
        "AppliedLore authoring bridge OK: "
        f"packets={stats.packets} rows={stats.localized_rows} "
        f"runtime_fields={stats.runtime_fields} "
        f"publication_article_fields={stats.publication_article_fields}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
