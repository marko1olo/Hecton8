#!/usr/bin/env python3
"""Export AppliedContent packet fields into localized markdown pages."""

from __future__ import annotations

import argparse
import csv
import io
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

def localization_frontmatter_matches(existing: str, rendered: str) -> bool:
    return frontmatter_localization_state(existing) == frontmatter_localization_state(rendered)


def render_page(base: Path, packet: dict, locale: str, surface_key: str, surface_title: str, cluster_spoiler_tiers: dict) -> str:
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
    flags = localized_row_flags(localized) if isinstance(localized, dict) else 0
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
        if isinstance(localized, dict) and (localized_row_flags(localized) & ROW_FLAG_DRAFT_LOCALIZATION) != 0:
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
                flags = localized_row_flags(localized) if isinstance(localized, dict) else 0
                page_path = base / folder / locale / f"{packet_id}.md"
                rows.append(
                    {
                        "surface": surface_key,
                        "locale": locale,
                        "direction": direction,
                        "packet_id": packet_id,
                        "release_set_id": release_set_id,
                        "article_id": article_id,
                        "unlock_id": safe_text(unlock.get("primary")),
                        "localization_status": localization_status_from_flags(flags, locale),
                        "localization_flags": str(flags),
                        "poi_tags": metadata_list(unlock.get("poi_tags")),
                        "biome_tags": metadata_list(unlock.get("biome_tags")),
                        "page_path": page_path.relative_to(base).as_posix(),
                        "title": clean_text(localized.get("title")) if isinstance(localized, dict) else packet_id,
                    }
                )
    return rows


def write_publication_surface_index(base: Path, packets: list[dict]) -> None:
    buffer = io.StringIO(newline="")
    writer = csv.DictWriter(buffer, fieldnames=PUBLICATION_INDEX_HEADERS, lineterminator="\n")
    writer.writeheader()
    writer.writerows(publication_surface_rows(base, packets))
    write_text_if_changed(base / "Publication_Surface_Index.csv", buffer.getvalue())


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
            raise ValueError(f"Navigation cluster packet missing from AppliedLore packets: {packet_id}")

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
                flags = localized_row_flags(localized) if isinstance(localized, dict) else 0
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


def write_publication_cluster_index(base: Path, packets: list[dict]) -> None:
    buffer = io.StringIO(newline="")
    writer = csv.DictWriter(buffer, fieldnames=PUBLICATION_CLUSTER_INDEX_HEADERS, lineterminator="\n")
    writer.writeheader()
    writer.writerows(publication_cluster_rows(base, packets))
    write_text_if_changed(base / "Publication_Cluster_Index.csv", buffer.getvalue())


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


def export_pages(root: Path, overwrite: bool, packet_glob: str = "") -> tuple[int, int, int, int]:
    import fnmatch
    packets = collect_packets(root)
    base = root / "Docs" / "Lore" / "AppliedContent"
    written = 0
    skipped = 0
    removed_disabled = 0
    indexes_written = 0

    cluster_spoiler_tiers = {}
    try:
        cluster_graph = navigation_cluster_graph_rows(base)
        for cluster in cluster_graph:
            pid = safe_text(cluster.get("packet_id"))
            if pid:
                cluster_spoiler_tiers[pid] = safe_text(cluster.get("spoiler_tier"))
    except Exception:
        pass

    for packet in packets:
        packet_id = safe_text(packet.get("packet_id"))

        if packet_glob and packet_id:
            matched = False
            for glob in packet_glob.split(','):
                if fnmatch.fnmatch(packet_id, glob.strip()):
                    matched = True
                    break
            if not matched:
                continue

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

                if path.exists() and not overwrite:
                    rendered = render_page(base, packet, locale, surface_key, surface_title, cluster_spoiler_tiers)
                    existing = path.read_text(encoding="utf-8")
                    if not localization_frontmatter_matches(existing, rendered):
                        path.write_text(rendered, encoding="utf-8", newline="\n")
                        written += 1
                        continue

                    skipped += 1
                    continue

                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(render_page(base, packet, locale, surface_key, surface_title, cluster_spoiler_tiers), encoding="utf-8", newline="\n")
                written += 1

    for locale in TARGET_LOCALES:
        for folder, surface_key, _surface_title in SURFACES:
            path = base / folder / locale / "INDEX.md"
            surface_packets = [packet for packet in packets if is_surface_enabled(packet, surface_key)]
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(render_index(surface_packets, locale, folder, surface_key), encoding="utf-8", newline="\n")
            indexes_written += 1

    write_localization_status_index(base, packets)
    write_publication_surface_index(base, packets)
    write_publication_cluster_index(base, packets)
    return written, skipped, removed_disabled, indexes_written


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=".", help="Repository root.")
    parser.add_argument("--overwrite", action="store_true", help="Overwrite existing localized markdown pages.")
    parser.add_argument("--packet-glob", default="", help="Glob to filter packets to export")
    args = parser.parse_args()
    written, skipped, removed_disabled, indexes_written = export_pages(Path(args.root).resolve(), args.overwrite, args.packet_glob)
    print(
        f"applied_lore_pages_written={written} skipped_existing={skipped} "
        f"removed_disabled={removed_disabled} index_pages_written={indexes_written}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
