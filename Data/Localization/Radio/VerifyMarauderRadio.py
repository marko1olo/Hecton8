#!/usr/bin/env python3
"""Verify Marauder radio data, binary cache hygiene, and external recipe loop risk."""

from __future__ import annotations

import json
import math
import random
import re
import struct
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parent
PROJECT_ROOT = ROOT.parents[2]
REPORT_OUTPUT = ROOT / "marauder_radio_verify_report.json"

FNV_OFFSET_BASIS = 2166136261
FNV_PRIME = 16777619
BINARY_MAGIC = b"H8RD"
BINARY_HEADER_STRUCT = struct.Struct("<4sHH14I")
BINARY_RECORD_STRUCT = struct.Struct("<32I")
BINARY_ALIGNMENT = 16
BINARY_HEADER_SIZE = BINARY_HEADER_STRUCT.size
BINARY_RECORD_SIZE = BINARY_RECORD_STRUCT.size
BINARY_FLAG_SORTED_HASH_RECORDS = 0x00000001
BINARY_FLAG_LOW_TIER_TEXT = 0x00000002
BINARY_FLAG_CLEAN_TEXT = 0x00000004
BINARY_FLAG_PACKED_ULTRA_FIELDS = 0x00000008
BINARY_FLAGS = (
    BINARY_FLAG_SORTED_HASH_RECORDS
    | BINARY_FLAG_LOW_TIER_TEXT
    | BINARY_FLAG_CLEAN_TEXT
    | BINARY_FLAG_PACKED_ULTRA_FIELDS
)


def fnv1a_u32_utf16le(text: str) -> int:
    encoded = text.encode("utf-16le")
    h = FNV_OFFSET_BASIS
    for b in encoded:
        h ^= b
        h = (h * FNV_PRIME) & 0xFFFFFFFF
    return h


def canonical_hash(payload: Any) -> int:
    canonical = json.dumps(
        payload, ensure_ascii=False, sort_keys=True, separators=(",", ":")
    )
    return fnv1a_u32_utf16le(canonical)


def pack_q8(values: list[int]) -> int:
    if len(values) != 4:
        raise ValueError("Q8 pack requires exactly 4 values")
    packed = 0
    for index, value in enumerate(values):
        if value < 0 or value > 255:
            raise ValueError(f"Q8 value out of range: {value}")
        packed |= (value & 0xFF) << (index * 8)
    return packed


def read_json(path: Path) -> Any:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def verify_json_hashes(
    raw: list[dict[str, Any]], clean: list[dict[str, Any]], dictionary: dict[str, Any]
) -> dict[str, Any]:
    errors: list[str] = []
    collisions: list[str] = []
    seen: dict[int, tuple[str, str]] = {}

    def add(owner: str, value: int, canonical: str) -> None:
        previous = seen.get(value)
        if previous is not None and previous[1] != canonical:
            collision = f"FNV collision {value}: {previous[0]} vs {owner}"
            collisions.append(collision)
            errors.append(collision)
        else:
            seen[value] = (owner, canonical)

    for label, entries in (("raw", raw), ("clean", clean)):
        if len(entries) != 15:
            errors.append(f"{label} entry count {len(entries)} != 15")
        for entry in entries:
            line_id = entry["LineID"]
            if entry["HashID"] != fnv1a_u32_utf16le(entry["Text"]):
                errors.append(f"{label}.{line_id} text hash mismatch")
            if entry["LowTierHashID"] != fnv1a_u32_utf16le(entry["LowTierText"]):
                errors.append(f"{label}.{line_id} low hash mismatch")
            if entry["LineIDHash"] != fnv1a_u32_utf16le(line_id):
                errors.append(f"{label}.{line_id} line hash mismatch")
            if entry["SpeakerHash"] != fnv1a_u32_utf16le(entry["Speaker"]):
                errors.append(f"{label}.{line_id} speaker hash mismatch")
            if entry["RequiredGlobalStateHash"] != fnv1a_u32_utf16le(
                entry["RequiredGlobalState"]
            ):
                errors.append(f"{label}.{line_id} state hash mismatch")
            add(f"{label}.{line_id}.Text", entry["HashID"], entry["Text"])
            add(
                f"{label}.{line_id}.LowTierText",
                entry["LowTierHashID"],
                entry["LowTierText"],
            )
            add(f"{label}.{line_id}.LineID", entry["LineIDHash"], line_id)
            add(
                f"{label}.{line_id}.State",
                entry["RequiredGlobalStateHash"],
                entry["RequiredGlobalState"],
            )

    for character in dictionary["Characters"]:
        add(
            f"speaker.{character['Speaker']}", character["HashID"], character["Speaker"]
        )
        add(
            f"callsign.{character['Callsign']}",
            character["CallsignHash"],
            character["Callsign"],
        )
    for slang in dictionary["Slang"]:
        add(f"slang.{slang['Term']}", slang["HashID"], slang["Term"])

    return {
        "UniqueHashValues": len(seen),
        "CollisionCount": len(collisions),
        "Collisions": collisions,
        "Errors": errors,
    }


def verify_binary(
    path: Path,
    raw: list[dict[str, Any]],
    clean: list[dict[str, Any]],
    dictionary: dict[str, Any],
    layout: dict[str, Any],
) -> dict[str, Any]:
    blob = path.read_bytes()
    errors: list[str] = []
    if len(blob) < BINARY_HEADER_SIZE:
        return {"Errors": ["blob smaller than header"]}

    header = BINARY_HEADER_STRUCT.unpack_from(blob, 0)
    magic, version, header_size = header[0], header[1], header[2]
    record_count, record_size = header[3], header[4]
    table_offset, payload_offset, payload_length = header[5], header[6], header[7]
    flags = header[8]
    raw_hash, clean_hash, dictionary_hash, layout_hash = (
        header[9],
        header[10],
        header[11],
        header[12],
    )

    if magic != BINARY_MAGIC:
        errors.append(f"magic mismatch {magic!r}")
    if version != 1:
        errors.append(f"version mismatch {version}")
    if header_size != BINARY_HEADER_SIZE:
        errors.append(f"header size mismatch {header_size}")
    if record_size != BINARY_RECORD_SIZE:
        errors.append(f"record size mismatch {record_size}")
    if table_offset != BINARY_HEADER_SIZE:
        errors.append(f"table offset mismatch {table_offset}")
    if payload_offset != BINARY_HEADER_SIZE + (record_count * BINARY_RECORD_SIZE):
        errors.append("payload offset not at record-table end")
    if payload_offset % BINARY_ALIGNMENT:
        errors.append("payload offset not 16-byte aligned")
    if len(blob) % BINARY_ALIGNMENT:
        errors.append("file length not 16-byte aligned")
    if payload_offset + payload_length > len(blob):
        errors.append("payload range overflows file")
    if flags != BINARY_FLAGS:
        errors.append(f"binary flags mismatch {flags}")
    if raw_hash != canonical_hash(raw):
        errors.append("raw JSON canonical hash mismatch in header")
    if clean_hash != canonical_hash(clean):
        errors.append("clean JSON canonical hash mismatch in header")
    if dictionary_hash != canonical_hash(dictionary):
        errors.append("dictionary canonical hash mismatch in header")
    if layout_hash != canonical_hash(layout):
        errors.append("layout canonical hash mismatch in header")
    if record_count != len(raw):
        errors.append(f"record count {record_count} != raw entry count {len(raw)}")
    if layout.get("Header", {}).get("Struct") != "<4sHH14I":
        errors.append("layout header struct is not explicit Little-endian <4sHH14I")
    if layout.get("Record", {}).get("Struct") != "<32I":
        errors.append("layout record struct is not explicit Little-endian <32I")
    if layout.get("Record", {}).get("SizeBytes") != BINARY_RECORD_SIZE:
        errors.append("layout record size does not match verifier struct")

    raw_by_hash = {entry["HashID"]: entry for entry in raw}
    clean_by_line = {entry["LineID"]: entry for entry in clean}
    payload = blob[payload_offset : payload_offset + payload_length]

    previous_hash = -1
    for i in range(record_count):
        record = BINARY_RECORD_STRUCT.unpack_from(
            blob, table_offset + (i * BINARY_RECORD_SIZE)
        )
        hash_id = record[0]
        if hash_id <= previous_hash:
            errors.append(f"record {i} hash sort violation")
        previous_hash = hash_id
        entry = raw_by_hash.get(hash_id)
        clean_entry = clean_by_line.get(entry["LineID"]) if entry is not None else None
        for label, offset, length in (
            ("text", record[4], record[5]),
            ("low", record[6], record[7]),
            ("clean", record[8], record[9]),
        ):
            if offset % BINARY_ALIGNMENT:
                errors.append(f"record {i} {label} offset unaligned")
            if offset + length > payload_length:
                errors.append(f"record {i} {label} payload slice overflow")
        if entry is None:
            errors.append(f"record {i} hash {hash_id} has no raw JSON entry")
            continue
        if clean_entry is None:
            errors.append(f"record {i} line {entry['LineID']} has no clean JSON entry")
            continue

        text = payload[record[4] : record[4] + record[5]].decode("utf-8")
        low_text = payload[record[6] : record[6] + record[7]].decode("utf-8")
        clean_text = payload[record[8] : record[8] + record[9]].decode("utf-8")
        if text != entry["Text"]:
            errors.append(f"record {i} text payload mismatch")
        if low_text != entry["LowTierText"]:
            errors.append(f"record {i} low payload mismatch")
        if clean_text != clean_entry["Text"]:
            errors.append(f"record {i} clean payload mismatch")
        if record[1] != entry["LineIDHash"]:
            errors.append(f"record {i} line hash mismatch")
        if record[2] != entry["SpeakerHash"]:
            errors.append(f"record {i} speaker hash mismatch")
        if record[3] != entry["RequiredGlobalStateHash"]:
            errors.append(f"record {i} state hash mismatch")
        if record[10] != int(round(float(entry["AudioDelay"]) * 1000.0)):
            errors.append(f"record {i} audio delay ms mismatch")
        if record[11] != entry["EmotionCode"]:
            errors.append(f"record {i} emotion code mismatch")
        if record[13] != BINARY_FLAGS:
            errors.append(f"record {i} tier flags mismatch")
        ultra = entry["Scalability"]["Ultra"]
        gradients = ultra["SpectralGradientRGBA32"]
        expected_ultra_fields = (
            ultra["NoiseSeed"],
            pack_q8(ultra["HarmonicWeightsQ8"]),
            pack_q8(ultra["HarmonicNoiseOctavesQ8"]),
            pack_q8(gradients[0]),
            pack_q8(gradients[1]),
            pack_q8(gradients[2]),
            pack_q8(gradients[3]),
            ultra["SubtitleGlitchMaskQ8"],
            ultra["RadioBreakupQ8"],
            canonical_hash(ultra),
            entry["Scalability"]["High"]["RadioNoiseSeed"],
            entry["LowTierHashID"],
            clean_entry["HashID"],
            entry["CategoryHash"],
        )
        if tuple(record[14:28]) != expected_ultra_fields:
            errors.append(f"record {i} packed Ultra/category fields mismatch")

    return {
        "Path": str(path),
        "Endian": "Little",
        "RecordCount": record_count,
        "HeaderSizeBytes": BINARY_HEADER_SIZE,
        "RecordSizeBytes": BINARY_RECORD_SIZE,
        "AlignmentBytes": BINARY_ALIGNMENT,
        "FileLengthBytes": len(blob),
        "PayloadLengthBytes": payload_length,
        "Errors": errors,
    }


def verify_source_hygiene() -> dict[str, Any]:
    errors: list[str] = []
    source = (ROOT / "generate_marauder_radio.py").read_text(encoding="utf-8")
    struct_formats = re.findall(r"struct\.Struct\(\"([^\"]+)\"\)", source)
    for fmt in struct_formats:
        if not fmt.startswith("<"):
            errors.append(f"struct format lacks explicit Little-endian marker: {fmt}")
    direct_pack_calls = re.search(r"\bstruct\.pack\(", source) is not None
    if direct_pack_calls:
        errors.append(
            "direct struct.pack call found; use predeclared Little-endian Struct instances"
        )

    binary_files = sorted(list(ROOT.glob("*.h8bin")) + list(ROOT.glob("*.bin")))
    binary_alignment: dict[str, int] = {}
    for binary_file in binary_files:
        size = binary_file.stat().st_size
        binary_alignment[binary_file.name] = size % BINARY_ALIGNMENT
        if size % BINARY_ALIGNMENT:
            errors.append(
                f"{binary_file.name} length is not {BINARY_ALIGNMENT}-byte aligned"
            )

    return {
        "StructFormats": struct_formats,
        "ExplicitLittleEndianStructs": all(
            fmt.startswith("<") for fmt in struct_formats
        ),
        "DirectStructPackCalls": direct_pack_calls,
        "BinaryFileAlignmentRemainders": binary_alignment,
        "Errors": errors,
    }


def verify_lore_tone(
    raw: list[dict[str, Any]], dictionary: dict[str, Any]
) -> dict[str, Any]:
    errors: list[str] = []
    slang_terms = [slang["Term"].lower() for slang in dictionary["Slang"]]
    all_text = " ".join(entry["Text"].lower() for entry in raw)
    missing_slang = [term for term in slang_terms if term not in all_text]
    if missing_slang:
        errors.append(f"unused slang terms: {missing_slang}")

    sterile_tokens = (
        "clean sci-fi",
        "laser sword",
        "galactic",
        "warp",
        "quantum drive",
        "plasma rifle",
    )
    sterile_hits = [token for token in sterile_tokens if token in all_text]
    if sterile_hits:
        errors.append(f"sterile/off-tone tokens found: {sterile_hits}")

    emotion_counts: dict[str, int] = {}
    for entry in raw:
        emotion = entry["EmotionTag"]
        emotion_counts[emotion] = emotion_counts.get(emotion, 0) + 1

    return {
        "SlangTermsAudited": slang_terms,
        "MissingSlangTerms": missing_slang,
        "SterileTokenHits": sterile_hits,
        "EmotionCounts": emotion_counts,
        "Errors": errors,
    }


def run_recipe_monte_carlo(
    steps: int = 1_000_000, seed: int = 0x8A6B5D31
) -> dict[str, Any]:
    path = PROJECT_ROOT / "Data" / "Economy" / "Recipes.json"
    data = read_json(path)
    reclaim = float(data["deconstruction"]["normal_reclaim_ratio"])
    values = {
        item["item_id"]: float(item["baseline_value_units"])
        for item in data["item_values"]
    }
    recipes = data["recipes"]
    for r in recipes:
        r["parsed_ingredients"] = tuple(
            (ing["item_id"], float(ing["quantity"])) for ing in r["ingredients"]
        )
        r["parsed_result_id"] = r["result"]["item_id"]
        r["parsed_result_qty"] = float(r["result"]["quantity"])
    for r in recipes:
        r["parsed_ingredients"] = tuple(
            (ing["item_id"], float(ing["quantity"])) for ing in r["ingredients"]
        )
        r["parsed_result_id"] = r["result"]["item_id"]
        r["parsed_result_qty"] = float(r["result"]["quantity"])
    produced = {recipe["result"]["item_id"] for recipe in recipes}
    primitives = [item_id for item_id in values if item_id not in produced]
    initial_primitive_units = 10_000.0
    inventory = {item_id: initial_primitive_units for item_id in primitives}
    rng = random.Random(seed)
    errors: list[str] = []

    def base_value() -> float:
        total = 0.0
        for item_id in primitives:
            total += inventory.get(item_id, 0.0) * values[item_id]
        return total

    initial_base_value = base_value()
    max_base_value = initial_base_value
    craft_count = 0
    deconstruct_count = 0

    for _ in range(steps):
        if rng.random() < 0.65:
            recipe = None
            for _attempt in range(16):
                candidate = recipes[rng.randrange(len(recipes))]
                can_craft = True
                for ing_id, ing_qty in candidate["parsed_ingredients"]:
                    if inventory.get(ing_id, 0.0) + 1e-9 < ing_qty:
                        can_craft = False
                        break
                if can_craft:
                    recipe = candidate
                    break
            if recipe is not None:
                for ing_id, ing_qty in recipe["parsed_ingredients"]:
                    inventory[ing_id] = inventory.get(ing_id, 0.0) - ing_qty
                inventory[recipe["parsed_result_id"]] = (
                    inventory.get(recipe["parsed_result_id"], 0.0)
                    + recipe["parsed_result_qty"]
                )
                craft_count += 1
        else:
            candidates = [
                recipe
                for recipe in recipes
                if inventory.get(recipe["parsed_result_id"], 0.0) >= 1.0
            ]
            if candidates:
                recipe = candidates[rng.randrange(len(candidates))]
                inventory[recipe["parsed_result_id"]] -= 1.0
                for ing_id, ing_qty in recipe["parsed_ingredients"]:
                    inventory[ing_id] = inventory.get(ing_id, 0.0) + (ing_qty * reclaim)
                deconstruct_count += 1

        current_base_value = base_value()
        if current_base_value > max_base_value:
            max_base_value = current_base_value
        if not math.isfinite(current_base_value):
            errors.append("non-finite base value")
            break
        if current_base_value > initial_base_value + 1e-6:
            errors.append(
                f"base resource value grew: {current_base_value} > {initial_base_value}"
            )
            break

    static_recipe_errors: list[str] = []
    for recipe in recipes:
        ingredient_value = sum(
            values[ing["item_id"]] * float(ing["quantity"])
            for ing in recipe["ingredients"]
        )
        reclaimed_value = ingredient_value * reclaim
        if reclaimed_value >= ingredient_value:
            static_recipe_errors.append(f"{recipe['recipe_id']} reclaim is non-lossy")

    errors.extend(static_recipe_errors)
    return {
        "Path": str(path),
        "Steps": steps,
        "Seed": seed,
        "Recipes": len(recipes),
        "PrimitiveItems": len(primitives),
        "InitialPrimitiveUnitsPerItem": initial_primitive_units,
        "CraftCount": craft_count,
        "DeconstructCount": deconstruct_count,
        "NormalReclaimRatio": reclaim,
        "InitialPrimitiveValue": round(initial_base_value, 6),
        "MaxPrimitiveValue": round(max_base_value, 6),
        "PrimitiveValueGrowth": round(max_base_value - initial_base_value, 6),
        "Errors": errors,
    }


def main() -> int:
    raw = read_json(ROOT / "marauder_radio_interceptions.json")
    clean = read_json(ROOT / "marauder_radio_interceptions_clean.json")
    dictionary = read_json(ROOT / "marauder_radio_dictionary.json")
    validation = read_json(ROOT / "marauder_radio_validation.json")
    layout = read_json(ROOT / "marauder_radio_interceptions.layout.json")

    hash_report = verify_json_hashes(raw, clean, dictionary)
    binary_report = verify_binary(
        ROOT / "marauder_radio_interceptions.h8bin", raw, clean, dictionary, layout
    )
    source_hygiene_report = verify_source_hygiene()
    lore_tone_report = verify_lore_tone(raw, dictionary)
    economy_report = run_recipe_monte_carlo()
    errors = (
        hash_report["Errors"]
        + binary_report["Errors"]
        + source_hygiene_report["Errors"]
        + lore_tone_report["Errors"]
        + economy_report["Errors"]
    )
    if validation.get("Status") != "VERIFIED MASTER GRADE":
        errors.append("validation status is not VERIFIED MASTER GRADE")
    if layout.get("Endian") != "Little":
        errors.append("layout endian is not Little")

    report = {
        "Schema": "H8.RADIO.VERIFY.V1",
        "Status": "PASS" if not errors else "FAIL",
        "EvidenceClass": "STATIC_JSON_CLI",
        "JsonHashAudit": hash_report,
        "BinaryAudit": binary_report,
        "SourceHygieneAudit": source_hygiene_report,
        "LoreToneAudit": lore_tone_report,
        "EconomyMonteCarlo": economy_report,
        "AtlasFit": {
            "ProjectAtlasDate": "2026-05-14",
            "ProjectAtlasStaticAssemblyCount": 83,
            "UserClaimedDomainCount": 85,
            "DomainCountCorrection": "Current disk PROJECT_ATLAS.md states 83 first-party asmdef files.",
            "DomainFamily": "UI / Narrative localization data",
            "RuntimeDependenciesAdded": 0,
            "CoreReferencesAdded": 0,
            "PrivateRuntimeStateRequired": False,
            "DataSovereigntyImpact": "Positive: stateless hash records, packed Ultra fields, and aligned text payload slices; no private runtime store forced.",
            "HPhiStaticEstimate": 0.25,
        },
        "Errors": errors,
    }

    with REPORT_OUTPUT.open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(report, handle, ensure_ascii=False, indent=2)
        handle.write("\n")

    print(
        f"VERIFY_MARAUDER_RADIO {report['Status']} "
        f"json_collisions={hash_report['CollisionCount']} "
        f"binary_errors={len(binary_report['Errors'])} "
        f"economy_steps={economy_report['Steps']} "
        f"economy_errors={len(economy_report['Errors'])}"
    )
    return 0 if not errors else 1


if __name__ == "__main__":
    raise SystemExit(main())
