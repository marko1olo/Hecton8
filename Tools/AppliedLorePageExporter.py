#!/usr/bin/env python3
"""Export AppliedContent packet fields into localized markdown pages."""

from __future__ import annotations

import argparse
import csv
import fnmatch
import io
from dataclasses import dataclass
from pathlib import Path

from AppliedLoreImporter import (
    ROW_FLAG_DRAFT_LOCALIZATION,
    SURFACE_MASKS,
    TARGET_LOCALES,
    collect_packets,
    localized_row_flags,
    resolve_packet_surface_mask,
    sanitize_localized_text,
    write_text_if_changed,
)
from AppliedLoreTextIntegrity import find_text_integrity_errors


SURFACES = (
    ("in_game_wiki", "in_game_wiki", "In-Game Wiki"),
    ("external_site", "external_site", "External Site"),
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
    "source_voice",
    "spoiler_tier",
    "spoiler_warning",
    "packet_json_path",
    "status_bucket",
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
INDEX_TITLES = {
    "en_US": ("HECTON-8 Codex Index", "HECTON-8 Field Archive"),
    "ru_RU": ("\u0418\u043d\u0434\u0435\u043a\u0441 \u043a\u043e\u0434\u0435\u043a\u0441\u0430 HECTON-8", "\u041f\u043e\u043b\u0435\u0432\u043e\u0439 \u0430\u0440\u0445\u0438\u0432 HECTON-8"),
    "ja_JP": ("HECTON-8 \u30b3\u30fc\u30c7\u30c3\u30af\u30b9\u7d22\u5f15", "HECTON-8 \u30d5\u30a3\u30fc\u30eb\u30c9\u30a2\u30fc\u30ab\u30a4\u30d6"),
    "zh_CN": ("HECTON-8 \u56fe\u9274\u7d22\u5f15", "HECTON-8 \u73b0\u573a\u6863\u6848"),
    "fr_FR": ("Index du codex HECTON-8", "Archive de terrain HECTON-8"),
    "es_ES": ("\u00cdndice del c\u00f3dice HECTON-8", "Archivo de campo HECTON-8"),
    "de_DE": ("HECTON-8 Kodexindex", "HECTON-8 Feldarchiv"),
    "pl_PL": ("Indeks kodeksu HECTON-8", "Archiwum terenowe HECTON-8"),
    "uk_UA": ("\u0406\u043d\u0434\u0435\u043a\u0441 \u043a\u043e\u0434\u0435\u043a\u0441\u0443 HECTON-8", "\u041f\u043e\u043b\u044c\u043e\u0432\u0438\u0439 \u0430\u0440\u0445\u0456\u0432 HECTON-8"),
    "ar_SA": ("\u0641\u0647\u0631\u0633 \u0645\u0648\u0633\u0648\u0639\u0629 HECTON-8", "\u0623\u0631\u0634\u064a\u0641 HECTON-8 \u0627\u0644\u0645\u064a\u062f\u0627\u0646\u064a"),
    "id_ID": ("Indeks Kodeks HECTON-8", "Arsip Lapangan HECTON-8"),
    "ko_KR": ("HECTON-8 \ucf54\ub371\uc2a4 \uc0c9\uc778", "HECTON-8 \ud604\uc7a5 \uae30\ub85d \ubcf4\uad00\uc18c"),
    "he_IL": ("\u05d0\u05d9\u05e0\u05d3\u05e7\u05e1 \u05d4\u05e7\u05d5\u05d3\u05e7\u05e1 \u05e9\u05dc HECTON-8", "\u05d0\u05e8\u05db\u05d9\u05d5\u05df \u05d4\u05e9\u05d8\u05d7 \u05e9\u05dc HECTON-8"),
    "pt_BR": ("\u00cdndice do c\u00f3dice HECTON-8", "Arquivo de campo HECTON-8"),
    "nl_NL": ("HECTON-8 codexindex", "HECTON-8 veldarchief"),
}
RTL_LOCALES = {"ar_SA", "he_IL"}
SOURCE_AUTHORITY_STATUS = "source_authority"
DRAFT_MACHINE_OR_LLM_STATUS = "draft_machine_or_llm"
CONTENT_CLASS_IN_WORLD = "in_world_artifact"
CONTENT_CLASS_PRODUCTION = "production_metadata"
CONTENT_CLASS_ABSENT = "ABSENT"


@dataclass(frozen=True)
class PublicationCheckStats:
    checked_files: int
    stale_files: int
    missing_files: int
    disabled_generated_pages: int
    integrity_issues: int
    sample_issues: tuple[str, ...]
    denied_pages: int = 0
    denied_pages_present: int = 0
    undeclared_content_class_packets: tuple[str, ...] = ()


def is_surface_enabled(packet: dict, surface_key: str) -> bool:
    return (resolve_packet_surface_mask(packet) & SURFACE_MASKS.get(surface_key, 0)) != 0


def publication_page_count(packets: list[dict]) -> int:
    count = 0
    for packet in packets:
        for _folder, surface_key, _surface_title in SURFACES:
            if is_surface_enabled(packet, surface_key):
                count += 1
    return count


def safe_text(value: object) -> str:
    return value if isinstance(value, str) else ""


def clean_text(value: object) -> str:
    return sanitize_localized_text(value) if isinstance(value, str) else ""


def metadata_list(value: object, limit: int = 2) -> str:
    if not isinstance(value, list):
        return ""

    items: list[str] = []
    for item in value:
        text = safe_text(item)
        if text:
            items.append(text)
            if len(items) >= limit:
                break
    return ";".join(items)


def cluster_id_from_route_moment(route_moment: str) -> str:
    return route_moment[6:] if route_moment.startswith("first_") else route_moment


def localization_status_from_flags(flags: int, locale: str) -> str:
    if locale == "en_US":
        return SOURCE_AUTHORITY_STATUS

    return DRAFT_MACHINE_OR_LLM_STATUS


def status_bucket_from_status(status: str) -> str:
    value = status.lower().replace(" ", "_").replace("-", "_")
    if not value:
        return "missing"
    if "blocked" in value:
        return "blocked"
    if "draft" in value or "pending" in value or "machine_or_llm" in value:
        return "draft"
    ready_terms = ("ready", "reviewed", "approved", "passed", "baked", "locked", "authority", "applied", "pilot")
    if any(term in value for term in ready_terms):
        return "ready"
    return "other"


def read_article_body(base: Path, article_path: object) -> str:
    relative_path = safe_text(article_path)
    if not relative_path:
        return ""

    resolved_base = base.resolve()
    resolved_path = (base / relative_path).resolve()
    if not resolved_path.is_relative_to(resolved_base):
        raise ValueError(f"AppliedLore article path escapes content root: {relative_path}")
    if not resolved_path.exists():
        raise ValueError(f"AppliedLore article path missing: {relative_path}")

    return resolved_path.read_text(encoding="utf-8").strip()


def localized_surface_body(base: Path, localized: dict, surface_key: str) -> tuple[str, bool]:
    if surface_key == "external_site":
        article_body = read_article_body(base, localized.get("external_site_article_path"))
        if article_body:
            return article_body, True

        article_body = clean_text(localized.get("external_site_article"))
        if article_body:
            return article_body, True

    return clean_text(localized.get(surface_key)), False


def frontmatter_localization_state(markdown: str) -> tuple[str, str]:
    status = ""
    flags = ""
    for line in markdown.splitlines():
        if line == "---" and (status or flags):
            break
        if line.startswith("localization_status:"):
            status = line.split(":", 1)[1].strip()
        elif line.startswith("localization_flags:"):
            flags = line.split(":", 1)[1].strip()
    return status, flags


def resolve_source_voice(packet: dict, surface_key: str) -> str:
    metadata = packet.get("metadata", {})
    if isinstance(metadata, dict):
        by_surface = metadata.get("source_voice_by_surface", {})
        if isinstance(by_surface, dict):
            surface_voice = safe_text(by_surface.get(surface_key))
            if surface_voice:
                return surface_voice
    packet_by_surface = packet.get("source_voice_by_surface", {})
    if isinstance(packet_by_surface, dict):
        surface_voice = safe_text(packet_by_surface.get(surface_key))
        if surface_voice:
            return surface_voice
    if isinstance(metadata, dict) and "source_voice" in metadata:
        return safe_text(metadata["source_voice"])
    explicit = safe_text(packet.get("source_voice"))
    if explicit:
        return explicit
    if surface_key == "external_site":
        return "Website Public"
    return "Neutral Reference"

def resolve_spoiler_tier(packet: dict, cluster_spoiler_tiers: dict) -> str:
    packet_id = safe_text(packet.get("packet_id"))
    metadata = packet.get("metadata", {})
    if isinstance(metadata, dict) and "spoiler_tier" in metadata:
        return safe_text(metadata["spoiler_tier"])
    explicit = safe_text(packet.get("spoiler_tier"))
    if explicit:
        return explicit
    if packet_id in cluster_spoiler_tiers:
        return cluster_spoiler_tiers[packet_id]
    return "0"

def resolve_content_class(packet: dict) -> str:
    metadata = packet.get("metadata", {})
    if isinstance(metadata, dict) and "content_class" in metadata:
        declared = safe_text(metadata["content_class"]).strip()
        if declared:
            return declared
    explicit = safe_text(packet.get("content_class")).strip()
    if explicit:
        return explicit
    return CONTENT_CLASS_ABSENT


def is_in_world_content_class(content_class: str) -> bool:
    return content_class == CONTENT_CLASS_IN_WORLD


def is_production_content_class(content_class: str) -> bool:
    return content_class == CONTENT_CLASS_PRODUCTION


def is_undeclared_content_class(content_class: str) -> bool:
    return not is_in_world_content_class(content_class) and not is_production_content_class(content_class)


def has_player_surface(packet: dict) -> bool:
    return any(is_surface_enabled(packet, surface_key) for _folder, surface_key, _surface_title in SURFACES)


def is_page_publish_allowed(content_class: str, path: Path) -> bool:
    """DEFAULT-DENY publish gate for player-facing surfaces.

    A packet must positively declare `content_class: in_world_artifact` to reach
    in_game_wiki or external_site. `production_metadata` is always denied. An
    absent or unrecognized class is denied for a page that does not exist yet and
    left alone when the page is already on disk, so the gate can never delete the
    existing corpus on its first run; removal is a separate reviewable step.
    """

    if is_in_world_content_class(content_class):
        return True
    if is_production_content_class(content_class):
        return False
    return path.exists()


def undeclared_content_class_label(packet: dict, content_class: str) -> str:
    return f"{safe_text(packet.get('packet_id'))} content_class={content_class}"


def print_undeclared_content_class_warnings(labels: tuple[str, ...] | list[str], limit: int = 25) -> None:
    if not labels:
        return

    labels = list(labels)
    print(
        f"WARNING: content_class not declared on {len(labels)} packet(s) with a player surface; "
        "new pages denied, existing pages left in place."
    )
    for label in labels[:limit]:
        print(f"WARNING: undeclared publish class: {label}")
    remaining = len(labels) - limit
    if remaining > 0:
        print(f"WARNING: +{remaining} more undeclared packet(s); run --report-classification for the full list.")


def content_class_report_rows(base: Path, packets: list[dict]) -> list[dict[str, str]]:
    rows: list[dict[str, str]] = []
    for packet in sorted(packets, key=lambda item: safe_text(item.get("packet_id"))):
        packet_id = safe_text(packet.get("packet_id"))
        content_class = resolve_content_class(packet)
        surfaces: list[str] = []
        slots = 0
        on_disk = 0
        publish_slots = 0
        deny_slots = 0
        deny_present = 0

        for folder, surface_key, _surface_title in SURFACES:
            if not is_surface_enabled(packet, surface_key):
                continue

            surfaces.append(surface_key)
            for locale in TARGET_LOCALES:
                path = base / folder / locale / f"{packet_id}.md"
                exists = path.exists()
                slots += 1
                on_disk += 1 if exists else 0
                if is_page_publish_allowed(content_class, path):
                    publish_slots += 1
                    continue

                deny_slots += 1
                deny_present += 1 if exists else 0

        if not surfaces:
            decision = "no_player_surface"
        elif deny_slots == 0:
            decision = "publish"
        elif publish_slots == 0:
            decision = "deny"
        else:
            decision = "mixed"

        if is_in_world_content_class(content_class):
            reason = "declared_in_world_artifact"
        elif is_production_content_class(content_class):
            reason = "production_metadata_denied"
        else:
            reason = "undeclared_default_deny_new_pages"

        rows.append(
            {
                "packet_id": packet_id,
                "content_class": content_class,
                "decision": decision,
                "reason": reason,
                "surfaces": ";".join(surfaces),
                "page_slots": str(slots),
                "pages_on_disk": str(on_disk),
                "publish_slots": str(publish_slots),
                "deny_slots": str(deny_slots),
                "denied_present": str(deny_present),
            }
        )
    return rows


def render_content_class_report(base: Path, packets: list[dict]) -> str:
    rows = content_class_report_rows(base, packets)
    id_width = max((len(row["packet_id"]) for row in rows), default=len("packet_id"))
    class_width = max((len(row["content_class"]) for row in rows), default=len("content_class"))
    lines = [
        f"applied_lore_content_class_report packets={len(rows)}",
        "gate: only content_class=in_world_artifact publishes to in_game_wiki/external_site; "
        "production_metadata is denied; an undeclared class denies a new page and retains an existing one.",
        "",
    ]

    for row in rows:
        lines.append(
            f"{row['packet_id']:<{id_width}}  content_class={row['content_class']:<{class_width}}  "
            f"decision={row['decision']:<17} reason={row['reason']:<32} "
            f"page_slots={row['page_slots']:>3} on_disk={row['pages_on_disk']:>3} "
            f"publish={row['publish_slots']:>3} deny={row['deny_slots']:>3} "
            f"denied_present={row['denied_present']:>3} surfaces={row['surfaces'] or 'none'}"
        )

    in_world = sum(1 for row in rows if row["reason"] == "declared_in_world_artifact")
    production = sum(1 for row in rows if row["reason"] == "production_metadata_denied")
    undeclared = sum(1 for row in rows if row["reason"] == "undeclared_default_deny_new_pages")
    lines.extend(
        (
            "",
            f"totals_content_class: in_world_artifact={in_world} production_metadata={production} undeclared={undeclared}",
            "totals_packet_decision: "
            + " ".join(
                f"{decision}={sum(1 for row in rows if row['decision'] == decision)}"
                for decision in ("publish", "deny", "mixed", "no_player_surface")
            ),
            f"totals_page_slots: total={sum(int(row['page_slots']) for row in rows)} "
            f"publish={sum(int(row['publish_slots']) for row in rows)} "
            f"deny={sum(int(row['deny_slots']) for row in rows)} "
            f"denied_present={sum(int(row['denied_present']) for row in rows)} "
            f"on_disk={sum(int(row['pages_on_disk']) for row in rows)}",
            "mode: report only; no files written and no pages deleted.",
            "",
        )
    )
    return "\n".join(lines)


def localization_frontmatter_matches(existing: str, rendered: str) -> bool:
    return frontmatter_localization_state(existing) == frontmatter_localization_state(rendered)


def render_page(base: Path, packet: dict, locale: str, surface_key: str, surface_title: str,
                cluster_spoiler_tiers: dict, crosslinks: dict | None = None) -> str:
    packet_id = safe_text(packet.get("packet_id"))
    release_set_id = safe_text(packet.get("release_set_id"))
    article_id = safe_text(packet.get("article_id"))
    unlock = packet.get("unlock", {})
    if not isinstance(unlock, dict):
        unlock = {}

    unlock_id = safe_text(unlock.get("primary"))
    poi_tags = metadata_list(unlock.get("poi_tags"))
    biome_tags = metadata_list(unlock.get("biome_tags"))
    localized = packet.get("localized", {}).get(locale, {})
    flags = localized_row_flags(localized, locale, packet) if isinstance(localized, dict) else 0
    status = localization_status_from_flags(flags, locale)
    direction = "rtl" if locale in RTL_LOCALES else "ltr"
    title = clean_text(localized.get("title")) or packet_id
    body, has_publication_article = localized_surface_body(base, localized, surface_key)
    scanner = clean_text(localized.get("scanner"))
    terminal = clean_text(localized.get("terminal"))
    audio = clean_text(localized.get("audio"))
    field_note = clean_text(localized.get("field_note"))
    source_voice = resolve_source_voice(packet, surface_key)
    spoiler_tier = resolve_spoiler_tier(packet, cluster_spoiler_tiers)

    lines: list[str] = [
        "---",
        f"packet_id: {packet_id}",
        f"release_set_id: {release_set_id}",
        f"article_id: {article_id}",
        f"unlock_id: {unlock_id}",
        f"poi_tags: {poi_tags}",
        f"biome_tags: {biome_tags}",
        f"locale: {locale}",
        f"surface: {surface_key}",
        f"source_voice: {source_voice}",
        f"spoiler_tier: {spoiler_tier}",
        f"title: \"{title}\"",
        "source: AppliedContent packet JSON",
        "runtime_reads_markdown: false",
        f"direction: {direction}",
        f"localization_status: {status}",
        f"localization_flags: {flags}",
    ]

    # Crosslink edges. The graph was being read for spoiler_tier only and its prereq/next columns were
    # parsed and discarded, so even the authored edges linked nothing. Emitted as frontmatter rather than
    # prose because a runtime codex reads fields, and because injecting a "Related" paragraph would change
    # authored page bodies that the validator and the writers have just been driven to zero findings.
    if crosslinks:
        edges = crosslinks.get(packet_id)
        if edges:
            prereq_ids, next_ids = edges
            if prereq_ids:
                lines.append(f"prereq_packet_ids: {prereq_ids}")
            if next_ids:
                lines.append(f"next_packet_ids: {next_ids}")

    if surface_key == "external_site" and str(spoiler_tier).isdigit() and int(spoiler_tier) >= 3:
        lines.append("spoiler_warning: archive_spoilers")

    lines.extend([
        "---",
        "",
        f"# {title}",
        "",
        body,
    ])

    if not has_publication_article:
        lines.extend((
            "",
            "## Scanner",
            "",
            scanner,
            "",
            "## Terminal",
            "",
            terminal,
        ))

        if audio:
            lines.extend(("", "## Audio", "", audio))
        if field_note:
            lines.extend(("", "## Field Note", "", field_note))

    lines.extend(("", f"<!-- {surface_title}; generated from {packet_id}/{locale}. -->", ""))
    return "\n".join(lines)


def render_index(packets: list[dict], locale: str, folder: str, surface_key: str) -> str:
    titles = INDEX_TITLES.get(locale, INDEX_TITLES["en_US"])
    title = titles[0] if folder == "in_game_wiki" else titles[1]
    direction = "rtl" if locale in RTL_LOCALES else "ltr"
    draft_marker_count = count_draft_rows(packets, locale)
    machine_draft_count = count_machine_draft_rows(packets, locale)

    lines: list[str] = [
        "---",
        f"locale: {locale}",
        f"surface: {surface_key}",
        "source: AppliedContent packet JSON",
        "runtime_reads_markdown: false",
        f"direction: {direction}",
        f"localized_pages: {len(packets)}",
        f"draft_machine_or_llm_pages: {machine_draft_count}",
        f"draft_marker_pages: {draft_marker_count}",
        "---",
        "",
        f"# {title}",
        "",
    ]

    for packet in sorted(packets, key=lambda item: safe_text(item.get("packet_id"))):
        packet_id = safe_text(packet.get("packet_id"))
        localized = packet.get("localized", {}).get(locale, {})
        title = clean_text(localized.get("title")) or packet_id
        lines.append(f"- [{title}]({packet_id}.md) `{packet_id}`")

    lines.extend(("", f"<!-- Generated localized index for {folder}/{locale}. -->", ""))
    return "\n".join(lines)


def count_draft_rows(packets: list[dict], locale: str) -> int:
    count = 0
    for packet in packets:
        localized = packet.get("localized", {}).get(locale, {})
        if isinstance(localized, dict) and (localized_row_flags(localized, locale, packet) & ROW_FLAG_DRAFT_LOCALIZATION) != 0:
            count += 1
    return count


def count_machine_draft_rows(packets: list[dict], locale: str) -> int:
    if locale == "en_US":
        return 0

    return len(packets)


def render_localization_status_index(packets: list[dict]) -> str:
    exported_pages_per_locale = publication_page_count(packets)
    lines: list[str] = [
        "# AppliedLore Localization Status",
        "",
        "Generated from AppliedContent release-set manifests and packet JSON sources.",
        "The game reads baked CSV/blob records; markdown is publication output only.",
        "",
        "Status meanings:",
        "- `source_authority`: English authority row for current AppliedContent export.",
        "- `draft_machine_or_llm`: non-English generated draft row; native/fluent review not proven.",
        "- Reviewed states (`fluent_reviewed`, `native_reviewed`, `runtime_ready`) require explicit per-locale proof and are not inferred from packet presence.",
        "",
        "Locale rows:",
    ]

    for locale in TARGET_LOCALES:
        source_authority = len(packets) if locale == "en_US" else 0
        machine_draft = count_machine_draft_rows(packets, locale)
        draft_markers = count_draft_rows(packets, locale)
        direction = "rtl" if locale in RTL_LOCALES else "ltr"
        lines.append(
            f"- `{locale}`: source_authority={source_authority}, draft_machine_or_llm={machine_draft}, "
            f"draft_marker_rows={draft_markers}, packet_rows={len(packets)}, "
            f"exported_pages={exported_pages_per_locale}, direction={direction}"
        )

    lines.extend(
        [
            "",
            "Operational rule: do not encode native-review state inside player-visible prose.",
            "Use `flags`/frontmatter/status index for routing, QA and publication gates.",
            "Canonical packet JSON and publication pages are source/export evidence only; native/fluent review, route cards, h8bin bake, Unity placement, and runtime readiness require separate proof.",
            "",
        ]
    )
    return "\n".join(lines)


def write_localization_status_index(base: Path, packets: list[dict]) -> None:
    write_text_if_changed(base / "Localization_Status_Index.md", render_localization_status_index(packets))


def publication_surface_rows(base: Path, packets: list[dict]) -> list[dict[str, str]]:
    rows: list[dict[str, str]] = []
    cluster_spoiler_tiers = {}
    try:
        cluster_graph = navigation_cluster_graph_rows(base)
        for cluster in cluster_graph:
            pid = safe_text(cluster.get("packet_id"))
            if pid:
                cluster_spoiler_tiers[pid] = safe_text(cluster.get("spoiler_tier"))
    except Exception:
        pass

    for folder, surface_key, _surface_title in SURFACES:
        for locale in TARGET_LOCALES:
            direction = "rtl" if locale in RTL_LOCALES else "ltr"
            for packet in sorted(packets, key=lambda item: safe_text(item.get("packet_id"))):
                if not is_surface_enabled(packet, surface_key):
                    continue

                packet_id = safe_text(packet.get("packet_id"))
                release_set_id = safe_text(packet.get("release_set_id"))
                article_id = safe_text(packet.get("article_id"))
                unlock = packet.get("unlock", {})
                if not isinstance(unlock, dict):
                    unlock = {}

                localized = packet.get("localized", {}).get(locale, {})
                flags = localized_row_flags(localized, locale, packet) if isinstance(localized, dict) else 0
                page_path = base / folder / locale / f"{packet_id}.md"

                status = localization_status_from_flags(flags, locale)
                source_voice = resolve_source_voice(packet, surface_key)
                spoiler_tier = resolve_spoiler_tier(packet, cluster_spoiler_tiers)
                spoiler_warning = "archive_spoilers" if surface_key == "external_site" and str(spoiler_tier).isdigit() and int(spoiler_tier) >= 3 else ""

                try:
                    packet_json_path = Path(packet.get("_source_path", "")).relative_to(base).as_posix()
                except Exception:
                    packet_json_path = ""

                rows.append(
                    {
                        "surface": surface_key,
                        "locale": locale,
                        "direction": direction,
                        "packet_id": packet_id,
                        "release_set_id": release_set_id,
                        "article_id": article_id,
                        "unlock_id": safe_text(unlock.get("primary")),
                        "localization_status": status,
                        "localization_flags": str(flags),
                        "poi_tags": metadata_list(unlock.get("poi_tags")),
                        "biome_tags": metadata_list(unlock.get("biome_tags")),
                        "page_path": page_path.relative_to(base).as_posix(),
                        "title": clean_text(localized.get("title")) if isinstance(localized, dict) else packet_id,
                        "source_voice": source_voice,
                        "spoiler_tier": spoiler_tier,
                        "spoiler_warning": spoiler_warning,
                        "packet_json_path": packet_json_path,
                        "status_bucket": status_bucket_from_status(status),
                    }
                )
    return rows


def render_csv(headers: tuple[str, ...], rows: list[dict[str, str]]) -> str:
    buffer = io.StringIO(newline="")
    writer = csv.DictWriter(buffer, fieldnames=headers, lineterminator="\n")
    writer.writeheader()
    writer.writerows(rows)
    return buffer.getvalue()


def write_markdown_if_changed(path: Path, rendered: str, force: bool = False) -> bool:
    path.parent.mkdir(parents=True, exist_ok=True)
    if not force and path.exists() and path.read_text(encoding="utf-8") == rendered:
        return False

    path.write_text(rendered, encoding="utf-8", newline="\n")
    return True


def render_publication_surface_index(base: Path, packets: list[dict]) -> str:
    return render_csv(PUBLICATION_INDEX_HEADERS, publication_surface_rows(base, packets))


def write_publication_surface_index(base: Path, packets: list[dict]) -> None:
    write_text_if_changed(base / "Publication_Surface_Index.csv", render_publication_surface_index(base, packets))


def crosslink_edge_map(base: Path) -> dict:
    """packet_id -> (prereq_packet_ids, next_packet_ids) as authored in the navigation graph.

    The graph was previously read only for spoiler_tier; its prereq/next columns were parsed and thrown
    away, so even the five hand-authored edges linked nothing and 689 packets had no relationship recorded
    anywhere. Tools/AppliedLoreCrosslinkGraphBuilder.py now derives the graph from unlock/poi/biome tags,
    611 rows covering 95 percent of in-world packets, and this is the consuming side.
    """
    out: dict = {}
    try:
        for row in navigation_cluster_graph_rows(base):
            pid = safe_text(row.get("packet_id"))
            if not pid:
                continue
            prereq = safe_text(row.get("prereq_packet_ids"))
            nxt = safe_text(row.get("next_packet_ids"))
            if prereq or nxt:
                out[pid] = (prereq, nxt)
    except Exception:
        # Same fail-open posture the spoiler-tier read already uses: a malformed graph must not stop a
        # publish, it just means pages ship without crosslink frontmatter.
        return {}
    return out


def navigation_cluster_graph_rows(base: Path) -> list[dict[str, str]]:
    path = base / NAVIGATION_CLUSTER_GRAPH_PATH
    rows: list[dict[str, str]] = []
    with path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        if tuple(reader.fieldnames or ()) != NAVIGATION_CLUSTER_GRAPH_HEADERS:
            raise ValueError(f"Navigation cluster graph header mismatch: {path}")

        for row in reader:
            rows.append({key: safe_text(row.get(key)) for key in NAVIGATION_CLUSTER_GRAPH_HEADERS})

    if not rows:
        raise ValueError(f"Navigation cluster graph is empty: {path}")
    return rows


def publication_cluster_rows(base: Path, packets: list[dict]) -> list[dict[str, str]]:
    rows: list[dict[str, str]] = []
    packet_by_id = {safe_text(packet.get("packet_id")): packet for packet in packets}
    cluster_graph = navigation_cluster_graph_rows(base)

    for cluster_order, cluster in enumerate(cluster_graph):
        packet_id = safe_text(cluster.get("packet_id"))
        packet = packet_by_id.get(packet_id)
        if packet is None:
            continue

        release_set_id = safe_text(packet.get("release_set_id"))
        article_id = safe_text(packet.get("article_id"))
        unlock = packet.get("unlock", {})
        if not isinstance(unlock, dict):
            unlock = {}

        cluster_id = cluster_id_from_route_moment(safe_text(cluster.get("route_moment")))
        for folder, surface_key, _surface_title in SURFACES:
            if not is_surface_enabled(packet, surface_key):
                continue

            for locale in TARGET_LOCALES:
                direction = "rtl" if locale in RTL_LOCALES else "ltr"
                localized = packet.get("localized", {}).get(locale, {})
                flags = localized_row_flags(localized, locale, packet) if isinstance(localized, dict) else 0
                page_path = base / folder / locale / f"{packet_id}.md"
                rows.append(
                    {
                        "surface": surface_key,
                        "locale": locale,
                        "direction": direction,
                        "cluster_id": cluster_id,
                        "cluster_order": str(cluster_order),
                        "cluster_packet_id": packet_id,
                        "release_set_id": release_set_id,
                        "article_id": article_id,
                        "unlock_id": safe_text(unlock.get("primary")),
                        "spoiler_tier": safe_text(cluster.get("spoiler_tier")),
                        "primary_surface": safe_text(cluster.get("primary_surface")),
                        "prereq_packet_ids": safe_text(cluster.get("prereq_packet_ids")),
                        "next_cluster_packet_ids": safe_text(cluster.get("next_packet_ids")),
                        "localization_status": localization_status_from_flags(flags, locale),
                        "localization_flags": str(flags),
                        "poi_tags": metadata_list(unlock.get("poi_tags")),
                        "biome_tags": metadata_list(unlock.get("biome_tags")),
                        "page_path": page_path.relative_to(base).as_posix(),
                        "title": clean_text(localized.get("title")) if isinstance(localized, dict) else packet_id,
                        "truth_payload": safe_text(cluster.get("truth_claim")),
                        "player_question": safe_text(cluster.get("player_decision")),
                    }
                )
    return rows


def render_publication_cluster_index(base: Path, packets: list[dict]) -> str:
    return render_csv(PUBLICATION_CLUSTER_INDEX_HEADERS, publication_cluster_rows(base, packets))


def write_publication_cluster_index(base: Path, packets: list[dict]) -> None:
    write_text_if_changed(base / "Publication_Cluster_Index.csv", render_publication_cluster_index(base, packets))


def remove_generated_disabled_page(path: Path, packet_id: str, surface_key: str) -> bool:
    if not path.exists():
        return False

    text = path.read_text(encoding="utf-8")
    generated_markers = (
        f"packet_id: {packet_id}" in text,
        f"surface: {surface_key}" in text,
        "source: AppliedContent packet JSON" in text,
        "runtime_reads_markdown: false" in text,
    )
    if not all(generated_markers):
        return False

    path.unlink()
    return True


def is_generated_disabled_page(path: Path, packet_id: str, surface_key: str) -> bool:
    if not path.exists():
        return False

    text = path.read_text(encoding="utf-8")
    generated_markers = (
        f"packet_id: {packet_id}" in text,
        f"surface: {surface_key}" in text,
        "source: AppliedContent packet JSON" in text,
        "runtime_reads_markdown: false" in text,
    )
    return all(generated_markers)


def is_generated_publication_page(path: Path, surface_key: str) -> bool:
    if not path.exists() or path.name == "INDEX.md":
        return False

    text = path.read_text(encoding="utf-8")
    generated_markers = (
        f"surface: {surface_key}" in text,
        "source: AppliedContent packet JSON" in text,
        "runtime_reads_markdown: false" in text,
        "packet_id:" in text,
    )
    return all(generated_markers)


def iter_generated_orphan_pages(base: Path, expected_paths: set[Path]) -> list[Path]:
    orphans: list[Path] = []
    for folder, surface_key, _surface_title in SURFACES:
        for locale in TARGET_LOCALES:
            surface_dir = base / folder / locale
            if not surface_dir.exists():
                continue
            for path in sorted(surface_dir.glob("*.md"), key=lambda item: item.name.lower()):
                resolved = path.resolve()
                if resolved in expected_paths:
                    continue
                if is_generated_publication_page(path, surface_key):
                    orphans.append(path)
    return orphans


def packet_matches_glob(packet_id: str, packet_glob: str) -> bool:
    if not packet_glob:
        return True

    return any(fnmatch.fnmatch(packet_id, glob.strip()) for glob in packet_glob.split(",") if glob.strip())


def cluster_spoiler_tier_map(base: Path) -> dict[str, str]:
    cluster_spoiler_tiers: dict[str, str] = {}
    try:
        cluster_graph = navigation_cluster_graph_rows(base)
        for cluster in cluster_graph:
            pid = safe_text(cluster.get("packet_id"))
            if pid:
                cluster_spoiler_tiers[pid] = safe_text(cluster.get("spoiler_tier"))
    except Exception:
        pass
    return cluster_spoiler_tiers


def compare_expected_file(
    root: Path,
    path: Path,
    rendered: str,
    issues: list[str],
    counts: dict[str, int],
    *,
    check_integrity: bool = False,
    include_placeholder_markers: bool = True,
) -> None:
    counts["checked"] += 1
    relative = path.relative_to(root).as_posix()
    if check_integrity:
        for issue in find_text_integrity_errors(
            rendered,
            include_placeholder_markers=include_placeholder_markers,
        ):
            counts["integrity"] += 1
            issues.append(f"integrity: {relative}: {issue}")
    if not path.exists():
        counts["missing"] += 1
        issues.append(f"missing: {relative}")
        return

    if path.read_text(encoding="utf-8") != rendered:
        counts["stale"] += 1
        issues.append(f"stale: {relative}")


def check_publication_freshness(
    root: Path,
    packet_glob: str = "",
    *,
    include_placeholder_markers: bool = True,
) -> PublicationCheckStats:
    packets = collect_packets(root, packet_glob)
    base = root / "Docs" / "Lore" / "AppliedContent"
    selected_packets = [packet for packet in packets if packet_matches_glob(safe_text(packet.get("packet_id")), packet_glob)]
    cluster_spoiler_tiers = cluster_spoiler_tier_map(base)
    crosslinks = crosslink_edge_map(base)
    counts = {"checked": 0, "stale": 0, "missing": 0, "disabled": 0, "integrity": 0, "denied": 0, "denied_present": 0}
    issues: list[str] = []
    expected_paths: set[Path] = set()
    undeclared_labels: list[str] = []

    for packet in selected_packets:
        packet_id = safe_text(packet.get("packet_id"))
        content_class = resolve_content_class(packet)
        if is_undeclared_content_class(content_class) and has_player_surface(packet):
            undeclared_labels.append(undeclared_content_class_label(packet, content_class))

        localized_by_locale = packet.get("localized", {})
        if not isinstance(localized_by_locale, dict):
            raise ValueError(f"Packet localized must be object: {packet_id}")

        for locale in TARGET_LOCALES:
            localized = localized_by_locale.get(locale)
            if not isinstance(localized, dict):
                raise ValueError(f"Missing locale: packet={packet_id} locale={locale}")

            for folder, surface_key, surface_title in SURFACES:
                path = base / folder / locale / f"{packet_id}.md"
                if not is_surface_enabled(packet, surface_key):
                    if is_generated_disabled_page(path, packet_id, surface_key):
                        counts["disabled"] += 1
                        issues.append(f"disabled-generated: {path.relative_to(root).as_posix()}")
                    continue

                if not is_page_publish_allowed(content_class, path):
                    # Gate denial. Stay in expected_paths so the orphan pass never treats a
                    # retained page as deletable; page removal is a separate reviewable step.
                    counts["denied"] += 1
                    if path.exists():
                        counts["denied_present"] += 1
                    expected_paths.add(path.resolve())
                    continue

                rendered = render_page(base, packet, locale, surface_key, surface_title, cluster_spoiler_tiers, crosslinks)
                expected_paths.add(path.resolve())
                compare_expected_file(
                    root,
                    path,
                    rendered,
                    issues,
                    counts,
                    check_integrity=True,
                    include_placeholder_markers=include_placeholder_markers,
                )

    if not packet_glob:
        for path in iter_generated_orphan_pages(base, expected_paths):
            counts["disabled"] += 1
            issues.append(f"orphan-generated: {path.relative_to(root).as_posix()}")

    if not packet_glob:
        for locale in TARGET_LOCALES:
            for folder, surface_key, _surface_title in SURFACES:
                path = base / folder / locale / "INDEX.md"
                surface_packets = [packet for packet in packets if is_surface_enabled(packet, surface_key)]
                compare_expected_file(root, path, render_index(surface_packets, locale, folder, surface_key), issues, counts)

        compare_expected_file(root, base / "Localization_Status_Index.md", render_localization_status_index(packets), issues, counts)
        compare_expected_file(root, base / "Publication_Surface_Index.csv", render_publication_surface_index(base, packets), issues, counts)
        compare_expected_file(root, base / "Publication_Cluster_Index.csv", render_publication_cluster_index(base, packets), issues, counts)

    return PublicationCheckStats(
        checked_files=counts["checked"],
        stale_files=counts["stale"],
        missing_files=counts["missing"],
        disabled_generated_pages=counts["disabled"],
        integrity_issues=counts["integrity"],
        sample_issues=tuple(issues[:40]),
        denied_pages=counts["denied"],
        denied_pages_present=counts["denied_present"],
        undeclared_content_class_packets=tuple(undeclared_labels),
    )


def export_pages(
    root: Path,
    overwrite: bool,
    packet_glob: str = "",
    gate_stats: dict[str, object] | None = None,
    purge_denied: bool = False,
) -> tuple[int, int, int, int]:
    packets = collect_packets(root, packet_glob)
    base = root / "Docs" / "Lore" / "AppliedContent"
    written = 0
    skipped = 0
    removed_disabled = 0
    indexes_written = 0
    denied_pages = 0
    denied_pages_present = 0
    purged_pages = 0
    cluster_spoiler_tiers = cluster_spoiler_tier_map(base)
    crosslinks = crosslink_edge_map(base)
    expected_paths: set[Path] = set()
    undeclared_labels: list[str] = []

    for packet in packets:
        packet_id = safe_text(packet.get("packet_id"))

        if not packet_matches_glob(packet_id, packet_glob):
            continue

        content_class = resolve_content_class(packet)
        if is_undeclared_content_class(content_class) and has_player_surface(packet):
            undeclared_labels.append(undeclared_content_class_label(packet, content_class))

        localized_by_locale = packet.get("localized", {})
        if not isinstance(localized_by_locale, dict):
            raise ValueError(f"Packet localized must be object: {packet_id}")

        for locale in TARGET_LOCALES:
            localized = localized_by_locale.get(locale)
            if not isinstance(localized, dict):
                raise ValueError(f"Missing locale: packet={packet_id} locale={locale}")

            for folder, surface_key, surface_title in SURFACES:
                path = base / folder / locale / f"{packet_id}.md"
                if not is_surface_enabled(packet, surface_key):
                    if remove_generated_disabled_page(path, packet_id, surface_key):
                        removed_disabled += 1
                    continue

                if not is_page_publish_allowed(content_class, path):
                    # Gate denial: no write. Stay in expected_paths so the orphan sweep below
                    # never deletes a retained page; page removal is a separate reviewable step.
                    denied_pages += 1
                    if path.exists():
                        denied_pages_present += 1
                        if purge_denied:
                            # Explicit opt-in cleanup. Removing a denied page must go through the
                            # exporter rather than a shell delete, because the page is generated output
                            # and expected_paths is what keeps --check honest about what should exist.
                            path.unlink()
                            purged_pages += 1
                            continue
                    if not purge_denied:
                        expected_paths.add(path.resolve())
                    continue

                rendered = render_page(base, packet, locale, surface_key, surface_title, cluster_spoiler_tiers, crosslinks)
                expected_paths.add(path.resolve())
                if path.exists() and not overwrite:
                    if write_markdown_if_changed(path, rendered):
                        written += 1
                        continue

                    skipped += 1
                    continue

                write_markdown_if_changed(path, rendered, force=overwrite)
                written += 1

    if not packet_glob:
        for path in iter_generated_orphan_pages(base, expected_paths):
            path.unlink()
            removed_disabled += 1

    if not packet_glob:
        for locale in TARGET_LOCALES:
            for folder, surface_key, _surface_title in SURFACES:
                path = base / folder / locale / "INDEX.md"
                surface_packets = [packet for packet in packets if is_surface_enabled(packet, surface_key)]
                if write_markdown_if_changed(path, render_index(surface_packets, locale, folder, surface_key)):
                    indexes_written += 1

        write_localization_status_index(base, packets)
        write_publication_surface_index(base, packets)
        write_publication_cluster_index(base, packets)

    if gate_stats is not None:
        gate_stats["denied_pages"] = denied_pages
        gate_stats["denied_pages_present"] = denied_pages_present
        gate_stats["purged_pages"] = purged_pages
        gate_stats["undeclared_content_class_packets"] = tuple(undeclared_labels)
    return written, skipped, removed_disabled, indexes_written


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=".", help="Repository root.")
    parser.add_argument("--overwrite", action="store_true", help="Overwrite existing localized markdown pages.")
    parser.add_argument("--packet-glob", default="", help="Glob to filter packets to export")
    parser.add_argument("--check", action="store_true", help="Fail if generated publication files are stale or missing.")
    parser.add_argument(
        "--purge-denied",
        action="store_true",
        help=(
            "Delete already-published pages belonging to production_metadata packets. Opt-in cleanup for "
            "content that the gate denies but that predates the gate. Removal goes through the exporter so "
            "expected_paths stays consistent and --check does not then report the pages as missing."
        ),
    )
    parser.add_argument(
        "--report-classification",
        action="store_true",
        help="List each packet content_class and its publish/deny decision without writing anything.",
    )
    args = parser.parse_args()
    if args.report_classification:
        root = Path(args.root).resolve()
        packets = collect_packets(root, args.packet_glob)
        selected_packets = [
            packet for packet in packets if packet_matches_glob(safe_text(packet.get("packet_id")), args.packet_glob)
        ]
        print(render_content_class_report(root / "Docs" / "Lore" / "AppliedContent", selected_packets), end="")
        return 0

    if args.check:
        stats = check_publication_freshness(Path(args.root).resolve(), args.packet_glob)
        print_undeclared_content_class_warnings(stats.undeclared_content_class_packets)
        print(
            f"applied_lore_publication_check checked_files={stats.checked_files} "
            f"stale_files={stats.stale_files} missing_files={stats.missing_files} "
            f"disabled_generated_pages={stats.disabled_generated_pages} "
            f"integrity_issues={stats.integrity_issues}"
        )
        print(
            f"applied_lore_publish_gate denied_pages={stats.denied_pages} "
            f"denied_pages_present={stats.denied_pages_present} "
            f"undeclared_packets={len(stats.undeclared_content_class_packets)}"
        )
        if stats.sample_issues:
            print("sample_issues:")
            for issue in stats.sample_issues:
                print(f"  {issue}")
        if stats.stale_files or stats.missing_files or stats.disabled_generated_pages or stats.integrity_issues:
            return 1
        return 0

    gate_stats: dict[str, object] = {}
    written, skipped, removed_disabled, indexes_written = export_pages(
        Path(args.root).resolve(),
        args.overwrite,
        args.packet_glob,
        gate_stats,
        purge_denied=args.purge_denied,
    )
    print_undeclared_content_class_warnings(gate_stats.get("undeclared_content_class_packets", ()))
    print(
        f"applied_lore_pages_written={written} skipped_existing={skipped} "
        f"removed_disabled={removed_disabled} index_pages_written={indexes_written}"
    )
    print(
        f"applied_lore_publish_gate denied_pages={gate_stats.get('denied_pages', 0)} "
        f"denied_pages_present={gate_stats.get('denied_pages_present', 0)} "
        f"undeclared_packets={len(gate_stats.get('undeclared_content_class_packets', ()))}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
