#!/usr/bin/env python3
"""Export AppliedContent packet fields into localized markdown pages."""

from __future__ import annotations

import argparse
from pathlib import Path

from AppliedLoreImporter import TARGET_LOCALES, collect_packets


SURFACES = (
    ("in_game_wiki", "in_game_wiki", "In-Game Wiki"),
    ("external_site", "external_site", "External Site"),
)
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


def safe_text(value: object) -> str:
    return value if isinstance(value, str) else ""


def render_page(packet: dict, locale: str, surface_key: str, surface_title: str) -> str:
    packet_id = safe_text(packet.get("packet_id"))
    article_id = safe_text(packet.get("article_id"))
    localized = packet.get("localized", {}).get(locale, {})
    title = safe_text(localized.get("title")) or packet_id
    body = safe_text(localized.get(surface_key))
    scanner = safe_text(localized.get("scanner"))
    terminal = safe_text(localized.get("terminal"))
    audio = safe_text(localized.get("audio"))
    field_note = safe_text(localized.get("field_note"))

    lines: list[str] = [
        "---",
        f"packet_id: {packet_id}",
        f"article_id: {article_id}",
        f"locale: {locale}",
        f"surface: {surface_key}",
        "source: AppliedContent packet JSON",
        "runtime_reads_markdown: false",
        "---",
        "",
        f"# {title}",
        "",
        body,
        "",
        "## Scanner",
        "",
        scanner,
        "",
        "## Terminal",
        "",
        terminal,
    ]

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

    lines: list[str] = [
        "---",
        f"locale: {locale}",
        f"surface: {surface_key}",
        "source: AppliedContent packet JSON",
        "runtime_reads_markdown: false",
        f"direction: {direction}",
        "---",
        "",
        f"# {title}",
        "",
    ]

    for packet in sorted(packets, key=lambda item: safe_text(item.get("packet_id"))):
        packet_id = safe_text(packet.get("packet_id"))
        localized = packet.get("localized", {}).get(locale, {})
        title = safe_text(localized.get("title")) or packet_id
        lines.append(f"- [{title}]({packet_id}.md) `{packet_id}`")

    lines.extend(("", f"<!-- Generated localized index for {folder}/{locale}. -->", ""))
    return "\n".join(lines)


def export_pages(root: Path, overwrite: bool) -> tuple[int, int, int]:
    packets = collect_packets(root)
    base = root / "Docs" / "Lore" / "AppliedContent"
    written = 0
    skipped = 0
    indexes_written = 0

    for packet in packets:
        packet_id = safe_text(packet.get("packet_id"))
        localized_by_locale = packet.get("localized", {})
        if not isinstance(localized_by_locale, dict):
            raise ValueError(f"Packet localized must be object: {packet_id}")

        for locale in TARGET_LOCALES:
            localized = localized_by_locale.get(locale)
            if not isinstance(localized, dict):
                raise ValueError(f"Missing locale: packet={packet_id} locale={locale}")

            for folder, surface_key, surface_title in SURFACES:
                path = base / folder / locale / f"{packet_id}.md"
                if path.exists() and not overwrite:
                    skipped += 1
                    continue

                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(render_page(packet, locale, surface_key, surface_title), encoding="utf-8", newline="\n")
                written += 1

    for locale in TARGET_LOCALES:
        for folder, surface_key, _surface_title in SURFACES:
            path = base / folder / locale / "INDEX.md"
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(render_index(packets, locale, folder, surface_key), encoding="utf-8", newline="\n")
            indexes_written += 1

    return written, skipped, indexes_written


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=".", help="Repository root.")
    parser.add_argument("--overwrite", action="store_true", help="Overwrite existing localized markdown pages.")
    args = parser.parse_args()
    written, skipped, indexes_written = export_pages(Path(args.root).resolve(), args.overwrite)
    print(f"applied_lore_pages_written={written} skipped_existing={skipped} index_pages_written={indexes_written}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
