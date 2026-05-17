#!/usr/bin/env python3
"""Offline verifier for HECTON-8 taxonomy localization data."""

from __future__ import annotations

import json
import hashlib
import re
import struct
import sys
import zlib
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
DEFAULT_PATH = ROOT / "Data" / "Localization" / "en_US_Taxonomy.json"
BINARY_PATH = ROOT / "Data" / "Localization" / "en_US_Taxonomy.h8bin"
BINARY_MANIFEST_PATH = ROOT / "Data" / "Localization" / "en_US_Taxonomy.manifest.json"

FNV_OFFSET = 0x811C9DC5
FNV_PRIME = 0x01000193
ALIGNMENT_BYTES = 16
BINARY_MAGIC = b"H8TX"
BINARY_VERSION = 2
BINARY_ENDIAN_MARKER = 0x0102
BINARY_HEADER_FORMAT = "<4sHHIIIIIIII24s"
BINARY_RECORD_FORMAT = "<IIIIIIIIIIII"
BINARY_HEADER_SIZE = struct.calcsize(BINARY_HEADER_FORMAT)
BINARY_RECORD_SIZE = struct.calcsize(BINARY_RECORD_FORMAT)
BINARY_HEADER_FIELDS = [
    "magic",
    "version",
    "endianMarker",
    "headerBytes",
    "recordBytes",
    "recordCount",
    "stringTableOffset",
    "stringTableBytes",
    "jsonCrc32",
    "flags",
    "payloadCrc32",
    "reserved24",
]
BINARY_RECORD_FIELDS = [
    "locHash32",
    "entityHash32",
    "categoryHash32",
    "textOffset",
    "textBytes",
    "toasterOffset",
    "toasterBytes",
    "rtxOffset",
    "rtxBytes",
    "metadataPacked",
    "commonNameHash32",
    "tierPayloadHash32",
]
BINOMIAL_RE = re.compile(r"^[A-Z][a-z]+ [a-z]+$")

STERILE_TERMS = (
    "pristine",
    "sleek",
    "utopian",
    "clean sci-fi",
    "nanotech",
    "quantum",
)

INDUSTRIAL_TERMS = (
    "pressure",
    "silt",
    "brine",
    "rust",
    "hull",
    "scar",
    "tendon",
    "vent",
    "jaw",
    "bladder",
    "bone",
    "ash",
    "corridor",
    "hinge",
    "plate",
    "gill",
    "shell",
    "root",
    "tube",
    "blade",
    "sampler",
    "drill",
    "laser",
    "node",
    "heat",
    "current",
    "route",
    "lab",
    "wound",
    "mouth",
    "autopsy",
    "biopsy",
    "necropsy",
)

ALLOWED_DAMAGE_TYPES = {
    "CombatDamageTypes.Pressure",
    "CombatDamageTypes.Thermal",
    "CombatDamageTypes.Impact",
    "CombatDamageTypes.Parasite",
    "CombatDamageTypes.Radioactive",
    "CombatDamageTypes.Toxic",
    "CombatDamageTypes.Emp",
    "CombatDamageTypes.MicroFracture",
}

ALLOWED_ARMOR_CLASSES = {
    "CombatArmorClass.Suit",
    "CombatArmorClass.Shell",
    "CombatArmorClass.Structure",
    "CombatArmorClass.OrganicHeavy",
    "CombatArmorClass.Brittle",
    "CombatArmorClass.Shielded",
}

ALLOWED_TOOL_IDS = {
    "tool_survival_blade",
    "tool_harpoon_launcher",
    "tool_laser_cutter",
    "tool_stun_pistol",
    "tool_salvage_sampler",
    "tool_env_analyzer",
}

ALLOWED_FLORA_CLASSES = {
    "HarvestableTemplate.MaterialClass.Kelp",
    "HarvestableTemplate.MaterialClass.Coral",
    "HarvestableTemplate.MaterialClass.TitaniumOutcrop",
    "HarvestableTemplate.MaterialClass.Sargassum",
}

ALLOWED_FLORA_MASKS = {
    "FloraDataTemplate.VulnerabilityMask.Cut",
    "FloraDataTemplate.VulnerabilityMask.Drill",
    "FloraDataTemplate.VulnerabilityMask.Grab",
    "FloraDataTemplate.VulnerabilityMask.Stun",
    "FloraDataTemplate.VulnerabilityMask.Burn",
    "FloraDataTemplate.VulnerabilityMask.PlasmaCut",
}


def fnv1a_utf16le(value: str) -> int:
    h = FNV_OFFSET
    for b in value.encode("utf-16le"):
        h ^= b
        h = (h * FNV_PRIME) & 0xFFFFFFFF
    return h


def hx(value: str) -> str:
    return f"0x{fnv1a_utf16le(value):08X}"


def fail(errors: list[str], message: str) -> None:
    errors.append(message)


def u32(value: str) -> int:
    return int(value, 16) & 0xFFFFFFFF


def collect_id_hashes(entry: dict) -> list[tuple[str, str]]:
    pairs = [(entry["LocID"], entry["Hash"])]
    if "EntityID" in entry:
        pairs.append((entry["EntityID"], entry["EntityHash"]))
    if "BaseEntityID" in entry:
        pairs.append((entry["BaseEntityID"], entry["BaseEntityHash"]))
    for biome_id, biome_hash in entry.get("BiomeHashes", {}).items():
        pairs.append((biome_id, biome_hash))
    for family_id, family_hash in entry.get("FamilyHashes", {}).items():
        pairs.append((family_id, family_hash))
    return pairs


def verify_binary_manifest(
    data: dict,
    blob: bytes,
    record_count: int,
    string_table_offset: int,
    string_table_size: int,
    json_crc: int,
    payload_crc: int,
    errors: list[str],
) -> None:
    if not BINARY_MANIFEST_PATH.exists():
        fail(errors, "binary manifest missing: Data/Localization/en_US_Taxonomy.manifest.json")
        return
    raw = BINARY_MANIFEST_PATH.read_text(encoding="utf-8")
    if "\n" in raw.strip() or "\r" in raw.strip():
        fail(errors, "binary manifest must be minified")
    manifest = json.loads(raw)
    if manifest.get("schema") != "H8.TAXONOMY.BINARY_MANIFEST.V2":
        fail(errors, "binary manifest schema mismatch")
    if manifest.get("status") != "TAXONOMY_BINARY_CACHE_LOCKED":
        fail(errors, "binary manifest status mismatch")
    if manifest.get("json") != "Data/Localization/en_US_Taxonomy.json":
        fail(errors, "binary manifest JSON path mismatch")
    if manifest.get("binary") != "Data/Localization/en_US_Taxonomy.h8bin":
        fail(errors, "binary manifest binary path mismatch")
    if manifest.get("mmapReady") is not True:
        fail(errors, "binary manifest must be mmap-ready")
    if manifest.get("endianness") != "little" or manifest.get("structPackPrefix") != "<":
        fail(errors, "binary manifest endian contract mismatch")
    if manifest.get("magic") != BINARY_MAGIC.decode("ascii") or manifest.get("version") != BINARY_VERSION:
        fail(errors, "binary manifest magic/version mismatch")
    if manifest.get("endianMarker") != f"0x{BINARY_ENDIAN_MARKER:04X}":
        fail(errors, "binary manifest endian marker mismatch")
    if manifest.get("headerStructFormat") != BINARY_HEADER_FORMAT:
        fail(errors, "binary manifest header format mismatch")
    if manifest.get("recordStructFormat") != BINARY_RECORD_FORMAT:
        fail(errors, "binary manifest record format mismatch")
    expected_struct_formats = {
        "header": BINARY_HEADER_FORMAT,
        "record": BINARY_RECORD_FORMAT,
    }
    if manifest.get("structFormats") != expected_struct_formats:
        fail(errors, "binary manifest structFormats mismatch")
    if manifest.get("headerBytes") != BINARY_HEADER_SIZE:
        fail(errors, "binary manifest header size mismatch")
    if manifest.get("recordBytes") != BINARY_RECORD_SIZE:
        fail(errors, "binary manifest record size mismatch")
    if manifest.get("recordCount") != record_count:
        fail(errors, "binary manifest record count mismatch")
    if manifest.get("fileBytes") != len(blob):
        fail(errors, "binary manifest file byte mismatch")
    if manifest.get("alignmentBytes") != ALIGNMENT_BYTES or manifest.get("fileAligned16") is not True:
        fail(errors, "binary manifest alignment mismatch")
    if manifest.get("recordAligned16") is not True:
        fail(errors, "binary manifest record alignment mismatch")
    if manifest.get("stringTableOffset") != string_table_offset:
        fail(errors, "binary manifest string table offset mismatch")
    if manifest.get("stringTableBytes") != string_table_size:
        fail(errors, "binary manifest string table size mismatch")
    if manifest.get("sha256") != hashlib.sha256(blob).hexdigest().upper():
        fail(errors, "binary manifest sha256 mismatch")
    if manifest.get("crc32") != f"0x{zlib.crc32(blob) & 0xFFFFFFFF:08X}":
        fail(errors, "binary manifest crc32 mismatch")
    if manifest.get("jsonCrc32") != f"0x{json_crc:08X}":
        fail(errors, "binary manifest JSON CRC mismatch")
    if manifest.get("payloadCrc32") != f"0x{payload_crc:08X}":
        fail(errors, "binary manifest payload CRC mismatch")
    expected_crc_scopes = {
        "crc32": "entire binary blob including header",
        "jsonCrc32": "minified UTF-8 Data/Localization/en_US_Taxonomy.json bytes",
        "payloadCrc32": "binary bytes [headerBytes:fileBytes], record table plus padding plus string table",
    }
    if manifest.get("crcScopes") != expected_crc_scopes:
        fail(errors, "binary manifest CRC scope contract mismatch")
    expected_crc_ranges = {
        "payloadCrc32Start": BINARY_HEADER_SIZE,
        "payloadCrc32End": len(blob),
    }
    if manifest.get("crcByteRanges") != expected_crc_ranges:
        fail(errors, "binary manifest CRC byte range mismatch")
    if manifest.get("rowIndexFormula") != "recordOffset=64+entryIndex*48":
        fail(errors, "binary manifest row formula mismatch")
    if manifest.get("metadataFormula") != "biomeCount | (familyCount << 8) | (categoryFlag << 16)":
        fail(errors, "binary manifest metadata formula mismatch")
    if manifest.get("headerFields") != BINARY_HEADER_FIELDS:
        fail(errors, "binary manifest header fields mismatch")
    if manifest.get("recordFields") != BINARY_RECORD_FIELDS:
        fail(errors, "binary manifest record fields mismatch")
    expected_header_constants = {
        "flags": 0,
        "reserved24Bytes": 24,
        "reserved24Fill": "0x00",
    }
    if manifest.get("headerConstants") != expected_header_constants:
        fail(errors, "binary manifest header constants mismatch")
    if data.get("binaryCache", {}).get("manifestPath") != "Data/Localization/en_US_Taxonomy.manifest.json":
        fail(errors, "JSON binaryCache manifest path mismatch")


def verify_binary_cache(data: dict, json_bytes: bytes, errors: list[str]) -> None:
    if not BINARY_PATH.exists():
        fail(errors, "binary cache missing: Data/Localization/en_US_Taxonomy.h8bin")
        return

    blob = BINARY_PATH.read_bytes()
    if len(blob) % ALIGNMENT_BYTES != 0:
        fail(errors, f"binary file size {len(blob)} not 16-byte aligned")
    if len(blob) < BINARY_HEADER_SIZE:
        fail(errors, "binary cache shorter than header")
        return

    header = struct.unpack(BINARY_HEADER_FORMAT, blob[:BINARY_HEADER_SIZE])
    (
        magic,
        version,
        endian_marker,
        header_size,
        record_size,
        record_count,
        string_table_offset,
        string_table_size,
        json_crc,
        flags,
        payload_crc,
        _reserved,
    ) = header

    if magic != BINARY_MAGIC:
        fail(errors, f"binary magic mismatch: {magic!r}")
    if version != BINARY_VERSION:
        fail(errors, f"binary version mismatch: {version}")
    if endian_marker != BINARY_ENDIAN_MARKER:
        fail(errors, "binary endian marker mismatch; expected little-endian < struct unpack")
    if header_size != BINARY_HEADER_SIZE:
        fail(errors, "binary header size mismatch")
    if record_size != BINARY_RECORD_SIZE:
        fail(errors, "binary record size mismatch")
    if record_size % ALIGNMENT_BYTES != 0:
        fail(errors, "binary record size is not 16-byte aligned")
    if string_table_offset % ALIGNMENT_BYTES != 0:
        fail(errors, "binary string table offset is not 16-byte aligned")
    if record_count != len(data.get("entries", [])):
        fail(errors, "binary record count does not match JSON entries")
    if flags != 0:
        fail(errors, "binary header flags must be zero for V2")
    if json_crc != (zlib.crc32(json_bytes) & 0xFFFFFFFF):
        fail(errors, "binary JSON CRC mismatch")

    records_end = header_size + (record_count * record_size)
    if records_end > len(blob):
        fail(errors, "binary record table exceeds file size")
        return
    if string_table_offset < records_end:
        fail(errors, "binary string table overlaps record table")
        return
    if string_table_offset + string_table_size > len(blob):
        fail(errors, "binary string table exceeds file size")
        return
    if payload_crc != (zlib.crc32(blob[BINARY_HEADER_SIZE:]) & 0xFFFFFFFF):
        fail(errors, "binary payload CRC mismatch")

    entries = data.get("entries", [])
    for index, entry in enumerate(entries):
        start = header_size + (index * record_size)
        record = struct.unpack(BINARY_RECORD_FORMAT, blob[start : start + record_size])
        (
            loc_hash,
            entity_hash,
            category_hash,
            text_offset,
            text_length,
            toaster_offset,
            toaster_length,
            rtx_offset,
            rtx_length,
            metadata,
            common_hash,
            tier_hash,
        ) = record
        entity_id = entry.get("EntityID", entry.get("BaseEntityID", entry["LocID"]))
        biome_count = metadata & 0xFF
        family_count = (metadata >> 8) & 0xFF
        record_flags = (metadata >> 16) & 0xFFFF
        toaster_summary = entry.get("Scalability", {}).get("Toaster", {}).get("summary", "")
        rtx_payload = json.dumps(entry.get("Scalability", {}).get("RTXOverkill", {}), ensure_ascii=False, separators=(",", ":"))

        def check_payload(label: str, offset: int, byte_length: int, expected_text: str) -> None:
            if offset % ALIGNMENT_BYTES != 0:
                fail(errors, f"{entry['LocID']}: binary {label} offset not aligned")
            if offset < string_table_offset or offset + byte_length > string_table_offset + string_table_size:
                fail(errors, f"{entry['LocID']}: binary {label} outside string table")
                return
            expected_bytes = expected_text.encode("utf-8")
            actual_bytes = blob[offset : offset + byte_length]
            if actual_bytes != expected_bytes:
                fail(errors, f"{entry['LocID']}: binary {label} payload mismatch")
            terminator_index = offset + byte_length
            if terminator_index >= len(blob) or blob[terminator_index] != 0:
                fail(errors, f"{entry['LocID']}: binary {label} missing null terminator")

        if loc_hash != u32(entry["Hash"]):
            fail(errors, f"{entry['LocID']}: binary loc hash mismatch")
        if entity_hash != fnv1a_utf16le(entity_id):
            fail(errors, f"{entry['LocID']}: binary entity hash mismatch")
        if category_hash != fnv1a_utf16le(entry["Category"]):
            fail(errors, f"{entry['LocID']}: binary category hash mismatch")
        check_payload("text", text_offset, text_length, entry["Text"])
        check_payload("toaster", toaster_offset, toaster_length, toaster_summary)
        check_payload("rtx", rtx_offset, rtx_length, rtx_payload)
        if biome_count != len(entry.get("BiomeIDs", [])):
            fail(errors, f"{entry['LocID']}: binary biome count mismatch")
        if family_count != len(entry.get("FamilyIDs", [])):
            fail(errors, f"{entry['LocID']}: binary family count mismatch")
        if common_hash != fnv1a_utf16le(entry["CommonName"]):
            fail(errors, f"{entry['LocID']}: binary common hash mismatch")
        if tier_hash != fnv1a_utf16le(f"{toaster_summary}|{rtx_payload}"):
            fail(errors, f"{entry['LocID']}: binary tier payload hash mismatch")

        expected_flags = 0x01 if entry["Category"] == "taxonomy_fauna_autopsy" else 0x02 if entry["Category"] == "taxonomy_flora_biopsy" else 0x04
        if record_flags != expected_flags:
            fail(errors, f"{entry['LocID']}: binary flags mismatch")

    verify_binary_manifest(
        data,
        blob,
        record_count,
        string_table_offset,
        string_table_size,
        json_crc,
        payload_crc,
        errors,
    )


def verify(path: Path) -> int:
    errors: list[str] = []
    raw = path.read_text(encoding="utf-8")
    json_bytes = raw.encode("utf-8")
    if "\n" in raw or "\r" in raw:
        fail(errors, "JSON_MINIFY failed: file contains newline characters")

    data = json.loads(raw)
    entries = data.get("entries", [])
    limits = data.get("uiLimits", {})
    max_text = int(limits.get("maxTextChars", 620))
    max_weak = int(limits.get("maxWeakPointChars", 80))
    max_note = int(limits.get("maxHarvestNoteChars", 180))

    if data.get("schema") != "H8.TAXONOMY.LOCALIZATION.V1":
        fail(errors, "schema mismatch")
    if data.get("status") != "TAXONOMY COMPILED":
        fail(errors, "status must be TAXONOMY COMPILED")
    if data.get("polishStatus") != "VERIFIED MASTER GRADE":
        fail(errors, "polishStatus must be VERIFIED MASTER GRADE")
    if data.get("offlineOnly") is not True:
        fail(errors, "offlineOnly must be true")
    if data.get("sourcePrompt", {}).get("taskCount") != 15:
        fail(errors, "sourcePrompt.taskCount must be 15")

    counts = {
        "taxonomy_fauna_autopsy": 0,
        "taxonomy_flora_biopsy": 0,
        "taxonomy_madness_variant": 0,
    }
    loc_ids: set[str] = set()
    hash_owners: dict[str, set[str]] = {}

    for entry in entries:
        loc_id = entry.get("LocID", "")
        category = entry.get("Category", "")
        loc_ids.add(loc_id)
        if category in counts:
            counts[category] += 1

        for id_value, hash_value in collect_id_hashes(entry):
            hash_owners.setdefault(hash_value, set()).add(id_value)

        if entry.get("Hash") != hx(loc_id):
            fail(errors, f"{loc_id}: hash mismatch")

        binomial = entry.get("Binomial", "")
        if not BINOMIAL_RE.match(binomial):
            fail(errors, f"{loc_id}: invalid binomial '{binomial}'")

        text = entry.get("Text", "")
        if len(text) > max_text:
            fail(errors, f"{loc_id}: Text length {len(text)} exceeds {max_text}")
        lowered_text = text.lower()
        for term in STERILE_TERMS:
            if term in lowered_text:
                fail(errors, f"{loc_id}: sterile lore term '{term}' present")
        if not any(term in lowered_text for term in INDUSTRIAL_TERMS):
            fail(errors, f"{loc_id}: lore tone lacks industrial/noir anatomy lexeme")

        scalability = entry.get("Scalability", {})
        toaster = scalability.get("Toaster", {})
        rtx = scalability.get("RTXOverkill", {})
        if not toaster or toaster.get("profile") != "Low/Celeron/i3":
            fail(errors, f"{loc_id}: missing toaster scalability data")
        if len(toaster.get("summary", "")) > int(toaster.get("maxTextChars", 160)):
            fail(errors, f"{loc_id}: toaster summary exceeds cap")
        if not rtx or "harmonicNoise" not in rtx or "necropsyGradientStops" not in rtx:
            fail(errors, f"{loc_id}: missing RTX overkill visual data")

        if category in ("taxonomy_fauna_autopsy", "taxonomy_flora_biopsy"):
            weak_points = entry.get("WeakPoints", [])
            if not weak_points:
                fail(errors, f"{loc_id}: missing WeakPoints")
            for weak in weak_points:
                if len(weak) > max_weak:
                    fail(errors, f"{loc_id}: weak point too long")

            harvest = entry.get("Harvest", {})
            note = harvest.get("Notes", "")
            if len(note) > max_note:
                fail(errors, f"{loc_id}: harvest note exceeds {max_note}")
            for tool in harvest.get("WeaponToolIds", []):
                if tool not in ALLOWED_TOOL_IDS:
                    fail(errors, f"{loc_id}: unknown tool {tool}")
            for damage in harvest.get("DamageTypes", []):
                if damage not in ALLOWED_DAMAGE_TYPES:
                    fail(errors, f"{loc_id}: unknown damage type {damage}")

        if category == "taxonomy_fauna_autopsy":
            entity_id = entry.get("EntityID", "")
            if entry.get("EntityHash") != hx(entity_id):
                fail(errors, f"{loc_id}: entity hash mismatch")
            if entry.get("LotkaVolterraRef") != "LV_ECOSYSTEM_DIRECTOR_20260515":
                fail(errors, f"{loc_id}: missing Lotka-Volterra reference")
            if "Lotka-Volterra" not in entry.get("Ecology", ""):
                fail(errors, f"{loc_id}: ecology text lacks Lotka-Volterra mention")
            armor = entry.get("Harvest", {}).get("TargetArmorClass", "")
            if armor not in ALLOWED_ARMOR_CLASSES:
                fail(errors, f"{loc_id}: unknown armor class {armor}")

        if category == "taxonomy_flora_biopsy":
            material = entry.get("HarvestMaterialClass", "")
            if material not in ALLOWED_FLORA_CLASSES:
                fail(errors, f"{loc_id}: unknown flora material class {material}")
            for mask in entry.get("VulnerabilityMask", []):
                if mask not in ALLOWED_FLORA_MASKS:
                    fail(errors, f"{loc_id}: unknown flora vulnerability {mask}")

        if category == "taxonomy_madness_variant":
            base_id = entry.get("BaseEntityID", "")
            if entry.get("BaseEntityHash") != hx(base_id):
                fail(errors, f"{loc_id}: base entity hash mismatch")
            if "CORRUPTED NECROPSY" not in text:
                fail(errors, f"{loc_id}: missing corrupted necropsy marker")

        for biome_id, expected in entry.get("BiomeHashes", {}).items():
            if expected != hx(biome_id):
                fail(errors, f"{loc_id}: biome hash mismatch for {biome_id}")
        for family_id, expected in entry.get("FamilyHashes", {}).items():
            if expected != hx(family_id):
                fail(errors, f"{loc_id}: family hash mismatch for {family_id}")

    if len(loc_ids) != len(entries):
        fail(errors, "duplicate LocID")
    collisions = {hash_value: sorted(values) for hash_value, values in hash_owners.items() if len(values) > 1}
    if collisions:
        fail(errors, f"FNV-1a distinct-ID collision(s): {collisions}")
    if counts["taxonomy_fauna_autopsy"] < 20:
        fail(errors, "fauna autopsy count below 20")
    if counts["taxonomy_fauna_autopsy"] != 22:
        fail(errors, "all-fauna count must be 22 current templates")
    if counts["taxonomy_flora_biopsy"] < 10:
        fail(errors, "flora biopsy count below 10")
    if counts["taxonomy_madness_variant"] < 2:
        fail(errors, "madness variant count below plural requirement")

    recorded = data.get("counts", {})
    if recorded.get("faunaAutopsies") != counts["taxonomy_fauna_autopsy"]:
        fail(errors, "recorded fauna count mismatch")
    if recorded.get("floraBiopsies") != counts["taxonomy_flora_biopsy"]:
        fail(errors, "recorded flora count mismatch")
    if recorded.get("madnessVariants") != counts["taxonomy_madness_variant"]:
        fail(errors, "recorded madness count mismatch")

    binary = data.get("binaryCache", {})
    if binary.get("endianness") != "little":
        fail(errors, "binaryCache.endianness must be little")
    if binary.get("headerStruct") != BINARY_HEADER_FORMAT or binary.get("recordStruct") != BINARY_RECORD_FORMAT:
        fail(errors, "binary struct formats must use explicit little-endian <")
    if binary.get("alignmentBytes") != ALIGNMENT_BYTES:
        fail(errors, "binary alignment must be 16 bytes")

    math_audit = data.get("mathAudit", {})
    if "N/A" not in math_audit.get("physicsLutsOrMatrices", ""):
        fail(errors, "mathAudit must explicitly classify physics LUT/matrix usage")
    if data.get("atlasFit", {}).get("observedAssemblyCount") != 83:
        fail(errors, "atlasFit.observedAssemblyCount must match current PROJECT_ATLAS.md static count 83")
    if data.get("atlasFit", {}).get("observedDomainIndexCount") != 85:
        fail(errors, "atlasFit.observedDomainIndexCount must match PROJECT_ATLAS.md 85-domain index")
    if data.get("hPhiAudit", {}).get("statelessLookupReady") is not True:
        fail(errors, "hPhiAudit must expose stateless lookup readiness")
    if data.get("hPhiAudit", {}).get("privateRuntimeStateAdded") is not False:
        fail(errors, "hPhiAudit must not add runtime private state")

    verify_binary_cache(data, json_bytes, errors)

    if errors:
        print("TAXONOMY VERIFY FAILED")
        for error in errors:
            print(f"- {error}")
        return 1

    print(
        "TAXONOMY VERIFY PASS "
        f"fauna={counts['taxonomy_fauna_autopsy']} "
        f"flora={counts['taxonomy_flora_biopsy']} "
        f"madness={counts['taxonomy_madness_variant']} "
        f"hashCollisions=0 "
        f"binaryAligned16=yes "
        "status=TAXONOMY COMPILED polishStatus=VERIFIED MASTER GRADE"
    )
    return 0


def main(argv: list[str]) -> int:
    path = Path(argv[1]).resolve() if len(argv) > 1 else DEFAULT_PATH
    return verify(path)


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
