#!/usr/bin/env python3
"""Audit HECTON-8 localization text for item-like lore dead ends.

The checker is intentionally conservative. It only inspects capitalized,
item-shaped phrases such as "Oxygen Tank", "Emergency O2 Canister", or
"Pressure Seal" and verifies that each phrase is defined by economy CSV data,
item assets, suit upgrade assets, localization name rows, or explicit aliases.
"""

from __future__ import annotations

import argparse
import csv
import json
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any


DEFAULT_LOCALIZATION = Path("Data/Localization/en_US.json")
DEFAULT_ITEMS_CSV = Path("Data/Economy/Items.csv")
DEFAULT_ITEMS_ROOT = Path("Assets/_Project/Data/Items")
DEFAULT_SUIT_UPGRADES_ROOT = Path("Assets/_Project/Data/Lore/SuitUpgrades")
DEFAULT_OUTPUT = Path("Docs/AgentLogs/LoreChecker_NARRATIVE_SEMANTIC_AUDITOR.json")

ITEM_NAME_CATEGORIES = {
    "item",
    "items",
    "resource",
    "resources",
    "tool",
    "tools",
    "suit_upgrade",
    "terminal_raw_text",
}

ITEM_SUFFIXES = (
    "Alloy",
    "Ampoule",
    "Analyzer",
    "Array",
    "Baffle",
    "Beacon",
    "Blade",
    "Board",
    "Canister",
    "Cartridge",
    "Cell",
    "Clumps",
    "Coil",
    "Composite",
    "Compensator",
    "Core",
    "Coral",
    "Coupler",
    "Crystal",
    "Cutter",
    "Diamond",
    "Dust",
    "Gel",
    "Graphite",
    "Kelp",
    "Lamp",
    "Lattice",
    "Lens",
    "Matrix",
    "Meteorite",
    "Module",
    "Ore",
    "Optimizer",
    "Package",
    "Paste",
    "Plate",
    "Reservoir",
    "Resin",
    "Rotor",
    "Sampler",
    "Scanner",
    "Scrap",
    "Seal",
    "Shards",
    "Shell",
    "Stabilizer",
    "Tank",
    "Tool",
    "Tissue",
    "Wire",
)

TITLE_WORD = r"(?:[A-Z][A-Za-z0-9+]*|O2|GI|UI|Atlas-6|RENDER_GI_RELAY)"
ITEM_TERM_RE = re.compile(
    r"\b((?:" + TITLE_WORD + r"[ -]+){0,5}(?:"
    + "|".join(re.escape(suffix) for suffix in sorted(ITEM_SUFFIXES, key=len, reverse=True))
    + r"))\b"
)
FALLBACK_RE = re.compile(r"^\s*fallbackText:\s*(.*?)\s*$")
STABLE_ID_RE = re.compile(r"^\s*stableId:\s*(\S+)\s*$")
UPGRADE_ID_RE = re.compile(r"^\s*upgradeId:\s*(\S+)\s*$")


@dataclass(frozen=True)
class Candidate:
    loc_id: str
    term: str
    normalized: str


def normalize_term(value: str) -> str:
    spaced = re.sub(r"(?<=[a-z0-9])(?=[A-Z])", " ", value)
    spaced = re.sub(r"[_/|:.,;()\[\]{}]+", " ", spaced)
    spaced = spaced.replace("-", " ")
    lowered = spaced.lower()
    lowered = re.sub(r"\bo2\b", "oxygen", lowered)
    lowered = re.sub(r"[^a-z0-9]+", " ", lowered)
    lowered = re.sub(r"\s+", " ", lowered).strip()
    lowered = re.sub(r"^(?:a|an|the)\s+", "", lowered)
    return lowered


def strip_yaml_scalar(value: str) -> str:
    cleaned = value.strip()
    if len(cleaned) >= 2 and cleaned[0] == cleaned[-1] and cleaned[0] in {"'", '"'}:
        cleaned = cleaned[1:-1]
    return cleaned.strip()


def humanize_id(value: str) -> str:
    cleaned = re.sub(r"^(Data|Comp|Item|Tool|SuitUpgrade)_", "", value)
    cleaned = cleaned.replace("_", " ")
    cleaned = re.sub(r"(?<=[a-z0-9])(?=[A-Z])", " ", cleaned)
    cleaned = re.sub(r"\s+", " ", cleaned)
    return cleaned.strip()


def load_localization(path: Path) -> tuple[dict[str, Any], list[dict[str, Any]]]:
    with path.open("r", encoding="utf-8") as handle:
        root = json.load(handle)
    if not isinstance(root, dict):
        raise ValueError("Localization root must be an object.")
    entries = root.get("entries")
    if not isinstance(entries, list):
        raise ValueError("Localization root must contain an entries array.")
    return root, entries


def load_extra_text_entries(paths: list[Path]) -> list[dict[str, str]]:
    entries: list[dict[str, str]] = []
    for path in paths:
        if not path.exists():
            raise FileNotFoundError(f"Extra text path not found: {path}")
        text = path.read_text(encoding="utf-8")
        entries.append(
            {
                "LocID": f"EXTRA_TEXT::{path.as_posix()}",
                "Category": "extra_text",
                "Text": text,
            }
        )
    return entries


def add_term(terms: dict[str, str], term: str, source: str) -> None:
    cleaned = term.strip()
    if not cleaned:
        return
    normalized = normalize_term(cleaned)
    if not normalized:
        return
    terms.setdefault(normalized, source)


def add_default_aliases(terms: dict[str, str]) -> None:
    aliases = {
        "Oxygen Tank": "alias:suit_oxygen_t1_aux_reservoir",
        "Oxygen I Auxiliary Reservoir": "alias:suit_oxygen_t1_aux_reservoir",
        "Oxygen I - Auxiliary Reservoir": "alias:suit_oxygen_t1_aux_reservoir",
        "Auxiliary Reservoir": "alias:suit_oxygen_t1_aux_reservoir",
        "High Capacity Tank": "alias:SuitUpgrades.HighCapacityTank",
        "High-Capacity Tank": "alias:SuitUpgrades.HighCapacityTank",
        "Emergency O2 Canister": "alias:Data_EmergencyO2Canister",
        "O2 Canister": "alias:Data_EmergencyO2Canister",
        "O2 Reserve": "alias:suit_oxygen_t1_aux_reservoir",
        "Alpha Leviathan": "alias:narrative_entity",
        "GI Relay": "alias:render_gi_relay",
    }
    for term, source in aliases.items():
        add_term(terms, term, source)


def load_catalog_terms(
    localization_entries: list[dict[str, Any]],
    items_csv: Path,
    items_root: Path,
    suit_upgrades_root: Path,
) -> dict[str, str]:
    terms: dict[str, str] = {}
    add_default_aliases(terms)

    if items_csv.exists():
        with items_csv.open("r", encoding="utf-8", newline="") as handle:
            reader = csv.DictReader(handle)
            for row in reader:
                display_name = (row.get("display_name") or "").strip()
                item_id = (row.get("item_id") or "").strip()
                add_term(terms, display_name, f"csv:{items_csv.as_posix()}")
                add_term(terms, humanize_id(item_id), f"csv:{items_csv.as_posix()}")

    for root in (items_root, suit_upgrades_root):
        if not root.exists():
            continue
        for path in sorted(root.rglob("*.asset"), key=lambda item: item.as_posix().lower()):
            try:
                lines = path.read_text(encoding="utf-8").splitlines()
            except UnicodeDecodeError:
                lines = path.read_text(encoding="utf-8-sig").splitlines()
            for line in lines:
                stable_match = STABLE_ID_RE.match(line)
                if stable_match:
                    add_term(terms, humanize_id(stable_match.group(1)), f"asset:{path.as_posix()}")
                    continue
                upgrade_match = UPGRADE_ID_RE.match(line)
                if upgrade_match:
                    upgrade_id = upgrade_match.group(1)
                    add_term(terms, humanize_id(upgrade_id), f"asset:{path.as_posix()}")
                    continue
                fallback_match = FALLBACK_RE.match(line)
                if fallback_match:
                    fallback = strip_yaml_scalar(fallback_match.group(1))
                    if 1 <= len(fallback) <= 64 and "." not in fallback and fallback.lower() not in {"take", "collect", "harvest"}:
                        add_term(terms, fallback, f"asset:{path.as_posix()}")

    for entry in localization_entries:
        if not isinstance(entry, dict):
            continue
        loc_id = entry.get("LocID")
        text = entry.get("Text")
        category = str(entry.get("Category", "")).lower()
        if not isinstance(loc_id, str) or not isinstance(text, str):
            continue
        if ":" in text:
            leading_name = text.split(":", 1)[0].strip()
            if 1 <= len(leading_name) <= 64:
                add_term(terms, leading_name, f"loc-leading:{loc_id}")
        if loc_id.endswith("_NAME") or category in ITEM_NAME_CATEGORIES:
            add_term(terms, text, f"loc:{loc_id}")

    return terms


def validate_localization_entries(entries: list[dict[str, Any]]) -> list[str]:
    problems: list[str] = []
    seen: set[str] = set()
    for index, entry in enumerate(entries):
        if not isinstance(entry, dict):
            problems.append(f"Entry {index} is not an object.")
            continue
        loc_id = entry.get("LocID")
        text = entry.get("Text")
        if not isinstance(loc_id, str) or not loc_id:
            problems.append(f"Entry {index} missing non-empty LocID.")
            continue
        if loc_id in seen:
            problems.append(f"Duplicate LocID: {loc_id}")
        seen.add(loc_id)
        if not isinstance(text, str):
            problems.append(f"Entry {loc_id} missing string Text.")
    return problems


def extract_candidates(entries: list[dict[str, Any]]) -> list[Candidate]:
    candidates: list[Candidate] = []
    seen: set[tuple[str, str]] = set()
    for entry in entries:
        if not isinstance(entry, dict):
            continue
        loc_id = entry.get("LocID")
        text = entry.get("Text")
        if not isinstance(loc_id, str) or not isinstance(text, str):
            continue
        for match in ITEM_TERM_RE.finditer(text):
            term = match.group(1).strip(" -")
            term = re.sub(r"^(?:A|An|The)\s+", "", term)
            normalized = normalize_term(term)
            if len(normalized.split()) < 2:
                continue
            key = (loc_id, normalized)
            if not normalized or key in seen:
                continue
            seen.add(key)
            candidates.append(Candidate(loc_id=loc_id, term=term, normalized=normalized))
    return candidates


def is_known_candidate(candidate: Candidate, terms: dict[str, str]) -> bool:
    if candidate.normalized in terms:
        return True
    token_count = len(candidate.normalized.split())
    if token_count >= 2:
        suffix = " " + candidate.normalized
        for known in terms:
            known_token_count = len(known.split())
            if known.endswith(suffix):
                return True
            if known_token_count >= 2 and candidate.normalized.endswith(" " + known):
                return True
    return False


def build_report(
    localization: Path,
    items_csv: Path,
    items_root: Path,
    suit_upgrades_root: Path,
    extra_text_paths: list[Path] | None = None,
) -> dict[str, Any]:
    _, entries = load_localization(localization)
    structural_problems = validate_localization_entries(entries)
    terms = load_catalog_terms(entries, items_csv, items_root, suit_upgrades_root)
    extra_entries = load_extra_text_entries(extra_text_paths or [])
    candidates = extract_candidates(entries + extra_entries)
    unresolved = [candidate for candidate in candidates if not is_known_candidate(candidate, terms)]

    return {
        "status": "PASS" if not structural_problems and not unresolved else "FAIL",
        "localization": localization.as_posix(),
        "extra_text_paths": [path.as_posix() for path in (extra_text_paths or [])],
        "entries": len(entries),
        "catalog_terms": len(terms),
        "item_like_mentions": len(candidates),
        "structural_problems": structural_problems,
        "unresolved_item_like_mentions": [
            {
                "loc_id": candidate.loc_id,
                "term": candidate.term,
                "normalized": candidate.normalized,
            }
            for candidate in unresolved
        ],
    }


def write_report(report: dict[str, Any], output: Path) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")


def build_arg_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Scan localization for item-like lore dead ends.")
    parser.add_argument("--localization", default=str(DEFAULT_LOCALIZATION))
    parser.add_argument("--items-csv", default=str(DEFAULT_ITEMS_CSV))
    parser.add_argument("--items-root", default=str(DEFAULT_ITEMS_ROOT))
    parser.add_argument("--suit-upgrades-root", default=str(DEFAULT_SUIT_UPGRADES_ROOT))
    parser.add_argument("--output", default=str(DEFAULT_OUTPUT))
    parser.add_argument(
        "--extra-text",
        action="append",
        default=[],
        help="Optional plain text or Markdown file to scan in addition to en_US.json.",
    )
    parser.add_argument("--report-only", action="store_true", help="Write report and return 0 even if dead ends are found.")
    return parser


def main(argv: list[str]) -> int:
    parser = build_arg_parser()
    args = parser.parse_args(argv)
    report = build_report(
        Path(args.localization),
        Path(args.items_csv),
        Path(args.items_root),
        Path(args.suit_upgrades_root),
        [Path(path) for path in args.extra_text],
    )
    write_report(report, Path(args.output))
    unresolved_count = len(report["unresolved_item_like_mentions"])
    problem_count = len(report["structural_problems"])
    print(
        "LORE CHECK: "
        f"status={report['status']} entries={report['entries']} "
        f"item_like_mentions={report['item_like_mentions']} unresolved={unresolved_count} "
        f"structural_problems={problem_count} output={args.output}"
    )
    if report["status"] != "PASS" and not args.report_only:
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
