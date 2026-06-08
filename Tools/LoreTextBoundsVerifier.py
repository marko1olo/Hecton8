#!/usr/bin/env python3
"""HECTON-8 applied lore text-bounds and hash audit.

Cold CLI tool. It does not touch Unity runtime state. Evidence class is
STATIC_SOURCE unless paired with a Unity/TMP capture outside this script.
"""

from __future__ import annotations

import argparse
import csv
import json
import math
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

SURFACE_FIELDS = (
    "title",
    "scanner",
    "terminal",
    "audio",
    "in_game_wiki",
    "external_site",
    "field_note",
)

SURFACE_BOUNDS = {
    # Conservative 720p internal-render read zones, not full screen width.
    "title": (360.0, 1, 18.0),
    "scanner": (460.0, 2, 16.0),
    "terminal": (520.0, 2, 15.0),
    "audio": (620.0, 2, 18.0),
    "in_game_wiki": (620.0, 5, 15.0),
    "external_site": (680.0, 4, 16.0),
    "field_note": (560.0, 3, 14.0),
}

FNV_OFFSET = 2166136261
FNV_PRIME = 16777619
UINT32_MASK = 0xFFFFFFFF

MOJIBAKE_MARKERS = (
    "\u00d0",
    "\u00d1",
    "\u00c3",
    "\u00c2",
    "\u00c5",
    "\u00c6",
    "\u00c7",
    "\u00d8",
    "\u00d9",
    "\u00e2\u20ac",
)

DRAFT_PREFIX_RE = re.compile(
    r"\bDraft ([A-Z]{2}(?:-[A-Z]{2})?) localization pending native pass\. ?",
    re.ASCII,
)

TITLE_LABEL_FIXES = {
    "Ru Native Review Lock": "RU Review Gate",
    "Cjk Review Lock": "CJK Review Gate",
    "Rtl Review Lock": "RTL Review Gate",
    "European Language Review Lock": "EU Loc Gate",
    "Subtitle Audio Review Lock": "Subtitle Audio Gate",
}


@dataclass(frozen=True)
class PacketRecord:
    source_path: Path
    release_set_id: str
    packet_id: str
    article_id: str
    title_key: str
    localized: dict[str, dict[str, str]]


def fnv1a_utf16(value: str) -> int:
    if not value:
        return 0
    hash_value = FNV_OFFSET
    for char in value:
        code = ord(char)
        hash_value ^= code & 0xFF
        hash_value = (hash_value * FNV_PRIME) & UINT32_MASK
        hash_value ^= (code >> 8) & 0xFF
        hash_value = (hash_value * FNV_PRIME) & UINT32_MASK
    return hash_value


def fnv1a_ascii_lower(value: str) -> int:
    if not value:
        return 0
    hash_value = FNV_OFFSET
    for char in value:
        code = ord(char)
        if 65 <= code <= 90:
            code += 32
        hash_value ^= code & 0xFF
        hash_value = (hash_value * FNV_PRIME) & UINT32_MASK
    return hash_value or 1


def read_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as handle:
        data = json.load(handle)
    if not isinstance(data, dict):
        raise ValueError(f"JSON root must be object: {path}")
    return data


def write_json(path: Path, payload: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def load_manifest_paths(root: Path, release_set: str | None) -> list[Path]:
    release_dir = root / "Docs" / "Lore" / "AppliedContent" / "release_sets"
    if release_set:
        path = release_dir / f"{release_set}_manifest.json"
        if not path.exists():
            raise FileNotFoundError(f"Release-set manifest missing: {path}")
        return [path]
    return sorted(release_dir.glob("*_manifest.json"), key=lambda p: p.name.lower())


def resolve_source(root: Path, source: str) -> Path:
    source_path = Path(source)
    return source_path if source_path.is_absolute() else root / source_path


def collect_packets(root: Path, release_set: str | None) -> tuple[list[PacketRecord], list[str]]:
    packets: list[PacketRecord] = []
    warnings: list[str] = []
    seen: set[str] = set()
    for manifest_path in load_manifest_paths(root, release_set):
        manifest = read_json(manifest_path)
        if release_set is None and manifest.get("canonical_importer_ready") is False:
            warnings.append(f"manifest-noncanonical-skipped:{manifest_path.as_posix()}")
            continue
        release_id = str(manifest.get("release_set_id", "")).strip()
        sources = manifest.get("packet_sources", [])
        if not sources and manifest.get("canonical_importer_ready") is True:
            sources = manifest.get("canonical_importer_sources", [])
        if not isinstance(sources, list) or not sources:
            warnings.append(f"manifest-no-packet-source:{manifest_path.as_posix()}")
            continue

        for source in sources:
            source_path = resolve_source(root, str(source))
            if source_path.suffix.lower() != ".json":
                warnings.append(f"manifest-non-json-packet-source:{manifest_path.as_posix()}:{source_path.as_posix()}")
                continue
            bundle = read_json(source_path)
            source_packets = bundle.get("packets")
            if not isinstance(source_packets, list):
                source_packets = [bundle]
            for packet in source_packets:
                if not isinstance(packet, dict):
                    raise ValueError(f"Packet must be object: {source_path}")
                packet_id = str(packet.get("packet_id", "")).strip()
                if not packet_id:
                    raise ValueError(f"Packet without id: {source_path}")
                if packet_id in seen:
                    raise ValueError(f"Duplicate packet id: {packet_id}")
                seen.add(packet_id)
                localized = packet.get("localized", {})
                if not isinstance(localized, dict):
                    raise ValueError(f"Packet localized must be object: {packet_id}")
                packets.append(
                    PacketRecord(
                        source_path=source_path,
                        release_set_id=str(packet.get("release_set_id", release_id)).strip(),
                        packet_id=packet_id,
                        article_id=str(packet.get("article_id", "")).strip(),
                        title_key=str(packet.get("title_key", "")).strip(),
                        localized=normalize_localized(localized),
                    )
                )
    return packets, warnings


def normalize_localized(localized: dict[str, Any]) -> dict[str, dict[str, str]]:
    result: dict[str, dict[str, str]] = {}
    for locale, fields in localized.items():
        if not isinstance(fields, dict):
            continue
        result[str(locale)] = {
            field: str(value)
            for field, value in fields.items()
            if isinstance(value, str)
        }
    return result


def char_em_width(char: str) -> float:
    code = ord(char)
    if char.isspace():
        return 0.34
    if 0x4E00 <= code <= 0x9FFF or 0x3040 <= code <= 0x30FF or 0xAC00 <= code <= 0xD7AF:
        return 1.0
    if 0x0590 <= code <= 0x05FF or 0x0600 <= code <= 0x06FF:
        return 0.68
    if 0x0400 <= code <= 0x04FF:
        return 0.64 if char.isupper() else 0.58
    if "A" <= char <= "Z":
        return 0.66
    if "a" <= char <= "z":
        if char in "il":
            return 0.32
        if char in "mw":
            return 0.82
        return 0.56
    if "0" <= char <= "9":
        return 0.56
    if char in ".,;:'`!|":
        return 0.26
    if char in "-/\\()[]{}":
        return 0.34
    return 0.62


def estimated_width_px(text: str, font_px: float) -> float:
    return sum(char_em_width(char) for char in text) * font_px


def estimate_wrapped_lines(text: str, max_width: float, font_px: float) -> tuple[int, float, float]:
    if not text:
        return 1, 0.0, 0.0
    lines = 1
    current_width = 0.0
    max_line_width = 0.0
    max_word_width = 0.0
    words = re.split(r"(\s+)", text.strip())
    for token in words:
        token_width = estimated_width_px(token, font_px)
        if token.strip():
            max_word_width = max(max_word_width, token_width)
        if "\n" in token:
            lines += token.count("\n")
            current_width = 0.0
            continue
        if current_width > 0 and current_width + token_width > max_width and token.strip():
            max_line_width = max(max_line_width, current_width)
            lines += 1
            current_width = estimated_width_px(token.lstrip(), font_px)
        else:
            current_width += token_width
    max_line_width = max(max_line_width, current_width)
    return lines, max_line_width, max_word_width


def likely_mojibake(text: str) -> bool:
    if not text:
        return False
    marker_hits = sum(1 for marker in MOJIBAKE_MARKERS if marker in text)
    return marker_hits >= 1 and any(ord(ch) > 0x7F for ch in text)


def audit_packets(packets: list[PacketRecord]) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    rows: list[dict[str, Any]] = []
    issues: list[dict[str, Any]] = []
    english_widths: dict[tuple[str, str], float] = {}

    for packet in packets:
        en_fields = packet.localized.get("en_US", {})
        for field in SURFACE_FIELDS:
            max_width, _, font_px = SURFACE_BOUNDS[field]
            english_widths[(packet.packet_id, field)] = estimated_width_px(en_fields.get(field, ""), font_px)

    for packet in packets:
        for locale in TARGET_LOCALES:
            localized = packet.localized.get(locale)
            if localized is None:
                issue = issue_payload(packet, locale, "", "MISSING_LOCALE", "Locale absent from packet source.")
                issues.append(issue)
                continue
            for field in SURFACE_FIELDS:
                text = localized.get(field, "")
                max_width, max_lines, font_px = SURFACE_BOUNDS[field]
                line_count, max_line_width, max_word_width = estimate_wrapped_lines(text, max_width, font_px)
                total_width = estimated_width_px(text, font_px)
                en_width = english_widths.get((packet.packet_id, field), 0.0)
                expansion = total_width / en_width if en_width > 0.0 else 1.0
                field_issues: list[str] = []
                if not text and field != "field_note":
                    field_issues.append("MISSING_FIELD")
                if line_count > max_lines:
                    field_issues.append("TEXT_OVERFLOW_LINES")
                if max_word_width > max_width:
                    field_issues.append("UNBREAKABLE_WORD_OVER_WIDTH")
                if locale != "en_US" and field in ("title", "scanner", "terminal", "audio") and expansion > 1.35:
                    field_issues.append("TEXT_EXPANSION_GT_1_35")
                if DRAFT_PREFIX_RE.search(text):
                    field_issues.append("DRAFT_PREFIX_LONG")
                if likely_mojibake(text):
                    field_issues.append("ENCODING_MOJIBAKE_RISK")

                row = {
                    "release_set_id": packet.release_set_id,
                    "packet_id": packet.packet_id,
                    "locale": locale,
                    "field": field,
                    "chars": len(text),
                    "estimated_px": round(total_width, 2),
                    "max_line_px": round(max_line_width, 2),
                    "max_word_px": round(max_word_width, 2),
                    "bound_px": max_width,
                    "lines": line_count,
                    "max_lines": max_lines,
                    "expansion_vs_en": round(expansion, 3),
                    "issues": ";".join(field_issues),
                    "source": packet.source_path.as_posix(),
                }
                rows.append(row)
                for issue_code in field_issues:
                    issues.append(issue_payload(packet, locale, field, issue_code, text[:180]))
    return rows, issues


def issue_payload(packet: PacketRecord, locale: str, field: str, code: str, detail: str) -> dict[str, Any]:
    return {
        "release_set_id": packet.release_set_id,
        "packet_id": packet.packet_id,
        "locale": locale,
        "field": field,
        "code": code,
        "detail": detail,
        "source": packet.source_path.as_posix(),
    }


def check_hash_collisions(packets: list[PacketRecord]) -> list[dict[str, Any]]:
    records: list[tuple[str, str, int, str]] = []
    for packet in packets:
        records.append(("applied_lore_packet_ascii_lower", packet.packet_id, fnv1a_ascii_lower(packet.packet_id), packet.source_path.as_posix()))
        if packet.article_id:
            records.append(("article_id_utf16", packet.article_id, fnv1a_utf16(packet.article_id), packet.source_path.as_posix()))
        if packet.title_key:
            records.append(("title_key_utf16", packet.title_key, fnv1a_utf16(packet.title_key), packet.source_path.as_posix()))
    for locale in TARGET_LOCALES:
        records.append(("locale_ascii_lower", locale, fnv1a_ascii_lower(locale), "Tools/AppliedLoreImporter.py"))

    by_mode_hash: dict[tuple[str, int], tuple[str, str]] = {}
    collisions: list[dict[str, Any]] = []
    for mode, value, hash_value, source in records:
        key = (mode, hash_value)
        previous = by_mode_hash.get(key)
        if previous and previous[0] != value:
            collisions.append(
                {
                    "mode": mode,
                    "hash": f"0x{hash_value:08X}",
                    "first": previous[0],
                    "first_source": previous[1],
                    "second": value,
                    "second_source": source,
                }
            )
        else:
            by_mode_hash[key] = (value, source)
    return collisions


def write_csv_report(path: Path, rows: list[dict[str, Any]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(
            handle,
            fieldnames=[
                "release_set_id",
                "packet_id",
                "locale",
                "field",
                "chars",
                "estimated_px",
                "max_line_px",
                "max_word_px",
                "bound_px",
                "lines",
                "max_lines",
                "expansion_vs_en",
                "issues",
                "source",
            ],
            lineterminator="\n",
        )
        writer.writeheader()
        writer.writerows(rows)


def rewrite_draft_prefixes(packets: list[PacketRecord]) -> list[dict[str, str]]:
    touched: list[dict[str, str]] = []
    paths = sorted({packet.source_path for packet in packets})
    for path in paths:
        original = path.read_text(encoding="utf-8")
        updated = DRAFT_PREFIX_RE.sub("", original)
        if updated == original:
            continue
        path.write_text(updated, encoding="utf-8", newline="\n")
        touched.append({"source": path.as_posix(), "change": "removed player-visible draft native-pass prefix"})
    return touched


def rewrite_title_labels(packets: list[PacketRecord]) -> list[dict[str, str]]:
    touched: list[dict[str, str]] = []
    paths = sorted({packet.source_path for packet in packets})
    for path in paths:
        original_lines = path.read_text(encoding="utf-8").splitlines()
        updated_lines: list[str] = []
        changed = False
        for line in original_lines:
            if '"title":' in line:
                updated_line = line
                for old, new in TITLE_LABEL_FIXES.items():
                    updated_line = updated_line.replace(old, new)
                if updated_line != line:
                    changed = True
                updated_lines.append(updated_line)
            else:
                updated_lines.append(line)
        if not changed:
            continue
        path.write_text("\n".join(updated_lines) + "\n", encoding="utf-8", newline="\n")
        touched.append({"source": path.as_posix(), "change": "shortened title review-lock labels"})
    return touched


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=".", help="Project root.")
    parser.add_argument("--release-set", help="Optional release set id, e.g. RS056_NATIVE_LOCALIZATION_REVIEW_PACK.")
    parser.add_argument("--json-report", default="Docs/Reports/LORE_TEXT_BOUNDS_17-A.json")
    parser.add_argument("--csv-report", default="Docs/Reports/LORE_TEXT_BOUNDS_17-A.csv")
    parser.add_argument("--write-draft-prefix-fixes", action="store_true")
    parser.add_argument("--write-title-label-fixes", action="store_true")
    parser.add_argument("--fail-on-risk", action="store_true")
    parser.add_argument("--no-write-reports", action="store_true", help="Run audit without writing JSON/CSV report files.")
    args = parser.parse_args()

    root = Path(args.root).resolve()
    packets, warnings = collect_packets(root, args.release_set)
    rewrites: list[dict[str, str]] = []
    if args.write_draft_prefix_fixes:
        rewrites.extend(rewrite_draft_prefixes(packets))
        packets, warnings = collect_packets(root, args.release_set)
    if args.write_title_label_fixes:
        rewrites.extend(rewrite_title_labels(packets))
        packets, warnings = collect_packets(root, args.release_set)

    rows, issues = audit_packets(packets)
    collisions = check_hash_collisions(packets)
    issue_counts: dict[str, int] = {}
    for issue in issues:
        code = str(issue["code"])
        issue_counts[code] = issue_counts.get(code, 0) + 1

    report = {
        "schema": "H8.LORE_TEXT_BOUNDS_AUDIT.V1",
        "agent": "17-A",
        "evidence_class": "STATIC_SOURCE",
        "runtime_claim": "PENDING_VERIFICATION",
        "release_set_filter": args.release_set or "ALL",
        "locale_count": len(TARGET_LOCALES),
        "locales": list(TARGET_LOCALES),
        "packet_count": len(packets),
        "surface_count": len(rows),
        "issue_counts": dict(sorted(issue_counts.items())),
        "issue_sample": issues[:200],
        "hash_collision_count": len(collisions),
        "hash_collisions": collisions,
        "warnings": warnings,
        "rewrites": rewrites,
        "bounds_model": {
            field: {
                "width_px": SURFACE_BOUNDS[field][0],
                "max_lines": SURFACE_BOUNDS[field][1],
                "font_px": SURFACE_BOUNDS[field][2],
            }
            for field in SURFACE_FIELDS
        },
    }

    json_report = root / args.json_report
    csv_report = root / args.csv_report
    if not args.no_write_reports:
        write_json(json_report, report)
        write_csv_report(csv_report, rows)

    print(
        "lore_text_bounds packets={0} surfaces={1} issues={2} collisions={3} warnings={4} rewrites={5}".format(
            len(packets),
            len(rows),
            len(issues),
            len(collisions),
            len(warnings),
            len(rewrites),
        )
    )
    if args.no_write_reports:
        print("reports=skipped")
    else:
        print(f"json={json_report.relative_to(root).as_posix()}")
        print(f"csv={csv_report.relative_to(root).as_posix()}")
    if args.fail_on_risk and (issues or collisions):
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
