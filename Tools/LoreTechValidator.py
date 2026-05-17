#!/usr/bin/env python3
"""Validate HECTON-8 PDA technical logs before binary packing."""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path
from typing import Any

sys.path.insert(0, str(Path(__file__).resolve().parent))
from LocToBinary import compute_loc_hash, format_hash
from PdaTechSchema import EXPECTED_ENTRY_COUNT, build_extra_data, title_from_text


DEFAULT_SOURCE = Path("Data/Lore/PdaTechnicalLogs.h8jsonl")
DEFAULT_LOCALIZATION = Path("Data/Localization/en_US.json")
MAX_TEXT_CHARS = 1500
MAX_COMPACT_CHARS = 240
REQUIRED_TIER_KEYS = ("Low", "Middle", "High", "Ultra")
LINK_RE = re.compile(r"<link=(0x[0-9A-Fa-f]{8})>")
BANNED_TERMS = (
    "magic",
    "mana",
    "spell",
    "wizard",
    "dragon",
    "warp drive",
    "hyperspace",
    "nanobot swarm",
    "clean sci-fi",
)
INDUSTRIAL_TERMS = (
    "pressure",
    "seal",
    "salt",
    "brine",
    "rust",
    "fault",
    "gauge",
    "valve",
    "pump",
    "scrub",
    "hull",
    "bracket",
    "corrosion",
    "leak",
    "black",
    "burn",
    "jam",
    "wire",
    "battery",
    "thermal",
    "sensor",
    "scanner",
    "ray",
    "drill",
    "welder",
    "weld",
    "cable",
    "mesh",
    "spline",
    "joint",
    "spark",
    "capacitor",
    "resistor",
    "power",
    "load",
    "coolant",
    "pipe",
    "sonar",
    "bearing",
    "map",
    "cache",
    "lens",
    "optics",
    "drone",
    "repair",
    "light",
    "save",
    "packet",
    "decay",
    "shader",
    "glyph",
    "frame",
    "dye",
    "grease",
    "ash",
)
REQUIRED_PHYSICS_MODELS = {
    "dalton_partial_pressure": ("dalton", "partial pressure"),
    "beer_lambert_attenuation": ("beer-lambert",),
    "sabine_rt60_reverb": ("sabine", "rt60"),
    "torricelli_ingress": ("torricelli",),
    "project_hydrostatic_scale": ("hydrostatic", "1 mpa per 100 m"),
    "scalar_flood_proxy": ("scalar",),
}


def load_jsonl(path: Path = DEFAULT_SOURCE) -> list[dict[str, Any]]:
    entries: list[dict[str, Any]] = []
    with path.open("r", encoding="utf-8") as handle:
        for index, line in enumerate(handle, 1):
            stripped = line.strip()
            if not stripped:
                continue
            try:
                payload = json.loads(stripped)
            except json.JSONDecodeError as exc:
                raise ValueError(f"Bad JSONL at {path}:{index}: {exc}") from exc
            if not isinstance(payload, dict):
                raise ValueError(f"PDA JSONL row {index} is not an object")
            entries.append(payload)
    return entries


def load_localization_entries(path: Path = DEFAULT_LOCALIZATION) -> list[dict[str, Any]]:
    root = json.loads(path.read_text(encoding="utf-8-sig"))
    entries = root.get("entries")
    if not isinstance(entries, list):
        raise ValueError("Localization JSON missing entries array")
    return entries


def _parse_hash(value: Any, field: str, loc_id: str) -> int:
    if not isinstance(value, str) or not re.fullmatch(r"0x[0-9A-F]{8}", value):
        raise ValueError(f"{loc_id} has non-canonical {field}: {value!r}")
    return int(value, 16) & 0xFFFFFFFF


def validate_structure(entries: list[dict[str, Any]]) -> None:
    if len(entries) != EXPECTED_ENTRY_COUNT:
        raise ValueError(f"Expected {EXPECTED_ENTRY_COUNT} PDA entries, found {len(entries)}")
    seen_hashes: dict[int, str] = {}
    seen_ids: set[str] = set()
    for index, entry in enumerate(entries, 1):
        loc_id = entry.get("LocID")
        if loc_id != f"TECH_{index:02d}" and loc_id != f"TECH_{index}":
            raise ValueError(f"PDA LocID sequence mismatch at row {index}: {loc_id!r}")
        if loc_id in seen_ids:
            raise ValueError(f"Duplicate PDA LocID: {loc_id}")
        seen_ids.add(str(loc_id))
        if entry.get("Layer") != "Narrative":
            raise ValueError(f"{loc_id} layer must be Narrative")
        if entry.get("Category") not in ("pda_technical_log", "habitat_integrity_corruption"):
            raise ValueError(f"{loc_id} category is invalid: {entry.get('Category')!r}")
        text = entry.get("Text")
        compact = entry.get("CompactText")
        if not isinstance(text, str) or not text:
            raise ValueError(f"{loc_id} missing Text")
        if not isinstance(compact, str) or not compact:
            raise ValueError(f"{loc_id} missing CompactText")
        if len(text) > MAX_TEXT_CHARS:
            raise ValueError(f"{loc_id} exceeds {MAX_TEXT_CHARS} text chars")
        if len(compact) > MAX_COMPACT_CHARS:
            raise ValueError(f"{loc_id} exceeds {MAX_COMPACT_CHARS} compact chars")
        title_from_text(text)
        loc_hash = _parse_hash(entry.get("Hash"), "Hash", str(loc_id))
        if loc_hash != compute_loc_hash(str(loc_id)):
            raise ValueError(f"{loc_id} hash mismatch: {format_hash(loc_hash)}")
        owner = seen_hashes.get(loc_hash)
        if owner is not None:
            raise ValueError(f"PDA FNV collision: {owner} and {loc_id} -> {format_hash(loc_hash)}")
        seen_hashes[loc_hash] = str(loc_id)
        _parse_hash(entry.get("LinkHash"), "LinkHash", str(loc_id))
        tier = entry.get("TierData")
        if not isinstance(tier, dict) or tuple(tier.keys()) != REQUIRED_TIER_KEYS:
            raise ValueError(f"{loc_id} TierData must contain Low/Middle/High/Ultra")
        if any(not isinstance(tier[key], str) or not tier[key] for key in REQUIRED_TIER_KEYS):
            raise ValueError(f"{loc_id} TierData values must be non-empty strings")


def validate_links(entries: list[dict[str, Any]]) -> None:
    hashes = {_parse_hash(entry["Hash"], "Hash", str(entry["LocID"])) for entry in entries}
    for entry in entries:
        loc_id = str(entry["LocID"])
        link_hash = _parse_hash(entry["LinkHash"], "LinkHash", loc_id)
        if link_hash not in hashes:
            raise ValueError(f"{loc_id} LinkHash target missing: {format_hash(link_hash)}")
        text_links = LINK_RE.findall(str(entry["Text"]))
        compact_links = LINK_RE.findall(str(entry["CompactText"]))
        if format_hash(link_hash) not in [value.upper().replace("0X", "0x") for value in text_links + compact_links]:
            raise ValueError(f"{loc_id} LinkHash does not appear in Text/CompactText")
        for raw_hash in text_links + compact_links:
            parsed = int(raw_hash, 16) & 0xFFFFFFFF
            if parsed not in hashes:
                raise ValueError(f"{loc_id} has broken PDA link: {format_hash(parsed)}")


def validate_science_and_tone(entries: list[dict[str, Any]]) -> None:
    joined = "\n".join(str(entry["Text"]).lower() for entry in entries)
    for term in BANNED_TERMS:
        if term in joined:
            raise ValueError(f"Banned PDA technical term found: {term}")
    audit = build_tone_audit(entries)
    if audit["WeakEntries"]:
        raise ValueError(f"Industrial tone gap: {audit['WeakEntries']}")
    physics = build_physics_audit(entries)
    missing = [
        key
        for key, value in physics.items()
        if int(value["Count"]) <= 0
    ]
    if missing:
        raise ValueError(f"Missing required PDA physics models: {missing}")


def validate_extra_data(entries: list[dict[str, Any]]) -> None:
    for entry in entries:
        loc_id = str(entry["LocID"])
        category = str(entry["Category"])
        title = title_from_text(str(entry["Text"]))
        expected = build_extra_data(loc_id, category, title)
        if entry.get("ExtraData") != expected:
            raise ValueError(f"{loc_id} ExtraData derivation drift")
    stress_entries = [
        entry
        for entry in entries
        if entry.get("Category") == "habitat_integrity_corruption"
    ]
    if [entry["LocID"] for entry in stress_entries] != ["TECH_16", "TECH_17", "TECH_18", "TECH_19", "TECH_20"]:
        raise ValueError("Habitat Integrity corruption entries must be TECH_16..TECH_20")
    states = [entry["ExtraData"]["StressState"] for entry in stress_entries]
    if states != [0, 1, 2, 3, 4]:
        raise ValueError(f"Habitat Integrity stress states must be 0..4, got {states}")


def validate_localization_sync(
    entries: list[dict[str, Any]],
    localization_entries: list[dict[str, Any]],
) -> None:
    localized = {
        str(entry.get("LocID")): entry
        for entry in localization_entries
        if str(entry.get("LocID", "")).startswith("TECH_")
    }
    if len(localized) != len(entries):
        raise ValueError(f"Localization TECH count mismatch: {len(localized)}")
    for entry in entries:
        loc_id = str(entry["LocID"])
        target = localized.get(loc_id)
        if not isinstance(target, dict):
            raise ValueError(f"Localization missing {loc_id}")
        for key in ("Hash", "Layer", "Category", "Text"):
            if target.get(key) != entry.get(key):
                raise ValueError(f"Localization {loc_id} field drift: {key}")
        for forbidden in ("CompactText", "TierData", "ExtraData", "LinkHash"):
            if forbidden in target:
                raise ValueError(f"Localization {loc_id} leaks PDA-only field: {forbidden}")


def build_physics_audit(entries: list[dict[str, Any]]) -> dict[str, dict[str, Any]]:
    audit: dict[str, dict[str, Any]] = {}
    for key, terms in REQUIRED_PHYSICS_MODELS.items():
        loc_ids: list[str] = []
        for entry in entries:
            text = str(entry["Text"]).lower()
            if all(term in text for term in terms):
                loc_ids.append(str(entry["LocID"]))
        audit[key] = {"Terms": list(terms), "Count": len(loc_ids), "LocIDs": loc_ids}
    return audit


def build_tone_audit(entries: list[dict[str, Any]]) -> dict[str, Any]:
    per_entry: list[dict[str, Any]] = []
    weak: list[str] = []
    for entry in entries:
        text = str(entry["Text"]).lower()
        hits = sum(1 for term in INDUSTRIAL_TERMS if term in text)
        loc_id = str(entry["LocID"])
        per_entry.append({"LocID": loc_id, "IndustrialHits": hits})
        if hits < 1:
            weak.append(loc_id)
    return {
        "MinimumHitsPerEntry": 1,
        "WeakEntries": weak,
        "PerEntry": per_entry,
    }


def validate(
    entries: list[dict[str, Any]],
    localization_entries: list[dict[str, Any]] | None = None,
) -> None:
    validate_structure(entries)
    validate_links(entries)
    validate_science_and_tone(entries)
    validate_extra_data(entries)
    if localization_entries is not None:
        validate_localization_sync(entries, localization_entries)


def build_arg_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Validate PDA technical log JSONL.")
    parser.add_argument("--source", default=str(DEFAULT_SOURCE))
    parser.add_argument("--localization", default=str(DEFAULT_LOCALIZATION))
    return parser


def main(argv: list[str]) -> int:
    args = build_arg_parser().parse_args(argv)
    entries = load_jsonl(Path(args.source))
    localization_entries = load_localization_entries(Path(args.localization))
    validate(entries, localization_entries)
    print(
        "PDA TECH VALIDATOR OK: "
        f"entries={len(entries)} maxText={MAX_TEXT_CHARS} "
        f"maxCompact={MAX_COMPACT_CHARS} hashCollisions=0"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
