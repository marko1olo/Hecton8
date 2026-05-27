#!/usr/bin/env python3
"""Offline compiler for HECTON-8 taxonomy localization data."""

from __future__ import annotations

import json
import hashlib
import struct
import zlib
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
OUT_PATH = ROOT / "Data" / "Localization" / "en_US_Taxonomy.json"
BIN_PATH = ROOT / "Data" / "Localization" / "en_US_Taxonomy.h8bin"
MANIFEST_PATH = ROOT / "Data" / "Localization" / "en_US_Taxonomy.manifest.json"
BINARY_AUDIT_PATH = ROOT / "Docs" / "AgentLogs" / "TaxonomyBinaryAudit_XENO_TAXONOMY_WRITER.json"

FNV_OFFSET = 0x811C9DC5
FNV_PRIME = 0x01000193
BINARY_MAGIC = b"H8TX"
BINARY_VERSION = 2
BINARY_ENDIAN_MARKER = 0x0102
BINARY_ALIGNMENT_BYTES = 16
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

LV_REF = "LV_ECOSYSTEM_DIRECTOR_20260515"
LV_SOURCE = "Assets/_Project/Scripts/World/EcosystemDirector.cs:720-723"
LV_SUMMARY = (
    "Lotka-Volterra prey birth 0.012, predation 0.00045, predator growth 0.00014, "
    "predator death 0.006; overharvest lowers prey, starves predators, then displaces hunters."
)

ENGINE_DAMAGE_TYPES = [
    "CombatDamageTypes.Pressure",
    "CombatDamageTypes.Thermal",
    "CombatDamageTypes.Impact",
    "CombatDamageTypes.Parasite",
    "CombatDamageTypes.Radioactive",
    "CombatDamageTypes.Toxic",
    "CombatDamageTypes.Emp",
    "CombatDamageTypes.MicroFracture",
]

ENGINE_ARMOR_CLASSES = [
    "CombatArmorClass.Suit",
    "CombatArmorClass.Shell",
    "CombatArmorClass.Structure",
    "CombatArmorClass.OrganicHeavy",
    "CombatArmorClass.Brittle",
    "CombatArmorClass.Shielded",
]

ENGINE_TOOL_IDS = [
    "tool_survival_blade",
    "tool_harpoon_launcher",
    "tool_laser_cutter",
    "tool_stun_pistol",
    "tool_salvage_sampler",
    "tool_env_analyzer",
]

FLORA_MATERIAL_CLASSES = [
    "HarvestableTemplate.MaterialClass.Kelp",
    "HarvestableTemplate.MaterialClass.Coral",
    "HarvestableTemplate.MaterialClass.TitaniumOutcrop",
    "HarvestableTemplate.MaterialClass.Sargassum",
]

FLORA_VULNERABILITY_MASKS = [
    "FloraDataTemplate.VulnerabilityMask.Cut",
    "FloraDataTemplate.VulnerabilityMask.Drill",
    "FloraDataTemplate.VulnerabilityMask.Grab",
    "FloraDataTemplate.VulnerabilityMask.Stun",
    "FloraDataTemplate.VulnerabilityMask.Burn",
    "FloraDataTemplate.VulnerabilityMask.PlasmaCut",
]


def fnv1a_utf16le(value: str) -> int:
    h = FNV_OFFSET
    for b in value.encode("utf-16le"):
        h ^= b
        h = (h * FNV_PRIME) & 0xFFFFFFFF
    return h


def hx(value: str) -> str:
    return f"0x{fnv1a_utf16le(value):08X}"


def u32(value: str) -> int:
    return int(value, 16) & 0xFFFFFFFF


def align_length(length: int, alignment: int = BINARY_ALIGNMENT_BYTES) -> int:
    remainder = length % alignment
    return 0 if remainder == 0 else alignment - remainder


def align_buffer(buffer: bytearray, alignment: int = BINARY_ALIGNMENT_BYTES) -> None:
    padding = align_length(len(buffer), alignment)
    if padding:
        buffer.extend(b"\x00" * padding)


def category_id(category: str) -> int:
    return fnv1a_utf16le(category)


def make_toaster_summary(entry: dict) -> str:
    weak_points = entry.get("WeakPoints") or ["profile locked"]
    tools = entry.get("Harvest", {}).get("WeaponToolIds", ["scanner"])
    return f"{entry['CommonName']}: {weak_points[0]}; use {tools[0]}"


def make_rtx_overkill(entry: dict) -> dict:
    seed = fnv1a_utf16le(entry["LocID"])
    phase_a = ((seed >> 0) & 0xFF) / 255.0
    phase_b = ((seed >> 8) & 0xFF) / 255.0
    phase_c = ((seed >> 16) & 0xFF) / 255.0
    category = entry["Category"]
    return {
        "gradientSeed": hx(f"{entry['LocID']}.gradient"),
        "necropsyGradientStops": [
            round(phase_a, 6),
            round((phase_a + phase_b) * 0.5, 6),
            round(max(phase_a, phase_c), 6),
        ],
        "harmonicNoise": {
            "basis": "FNV-derived deterministic codex overlay; presentation only, not physics",
            "octaves": 5 if category == "taxonomy_madness_variant" else 4,
            "phaseRadians": round(6.283185307179586 * phase_b, 6),
            "amplitude": round(0.35 + 0.25 * phase_c, 6),
            "frequency": round(0.75 + 2.5 * phase_a, 6),
        },
        "overlayBudget": "Ultra codex pane: high-res anatomy gradient, harmonic silt crawl, stress text decay",
    }


def apply_scalability(entries: list[dict]) -> None:
    for entry in entries:
        entry["Scalability"] = {
            "Toaster": {
                "profile": "Low/Celeron/i3",
                "fields": ["LocID", "Hash", "CommonName", "Text", "BiomeHashes"],
                "maxTextChars": 160,
                "summary": make_toaster_summary(entry)[:160],
            },
            "RTXOverkill": make_rtx_overkill(entry),
        }


def write_binary_cache(payload: dict, json_bytes: bytes) -> dict:
    entries = payload["entries"]
    records: list[tuple[int, int, int, int, int, int, int, int, int, int, int, int]] = []
    string_table = bytearray()
    table_offset = BINARY_HEADER_SIZE + (len(entries) * BINARY_RECORD_SIZE)
    table_offset += align_length(table_offset)
    offset_trace: list[dict] = []

    def append_payload(text: str) -> tuple[int, int]:
        align_buffer(string_table)
        absolute_offset = table_offset + len(string_table)
        payload_bytes = text.encode("utf-8")
        string_table.extend(payload_bytes)
        string_table.append(0)
        return absolute_offset, len(payload_bytes)

    for entry in entries:
        absolute_text_offset, text_length = append_payload(entry["Text"])
        toaster_summary = entry["Scalability"]["Toaster"]["summary"]
        toaster_offset, toaster_length = append_payload(toaster_summary)
        rtx_payload = json.dumps(entry["Scalability"]["RTXOverkill"], ensure_ascii=False, separators=(",", ":"))
        rtx_offset, rtx_length = append_payload(rtx_payload)

        entity_id = entry.get("EntityID", entry.get("BaseEntityID", entry["LocID"]))
        flags = 0
        if entry["Category"] == "taxonomy_fauna_autopsy":
            flags |= 0x01
        elif entry["Category"] == "taxonomy_flora_biopsy":
            flags |= 0x02
        elif entry["Category"] == "taxonomy_madness_variant":
            flags |= 0x04
        metadata = (len(entry.get("BiomeIDs", [])) & 0xFF) | ((len(entry.get("FamilyIDs", [])) & 0xFF) << 8) | (flags << 16)
        tier_hash = fnv1a_utf16le(f"{toaster_summary}|{rtx_payload}")

        records.append(
            (
                u32(entry["Hash"]),
                fnv1a_utf16le(entity_id),
                category_id(entry["Category"]),
                absolute_text_offset,
                text_length,
                toaster_offset,
                toaster_length,
                rtx_offset,
                rtx_length,
                metadata,
                fnv1a_utf16le(entry["CommonName"]),
                tier_hash,
            )
        )
        offset_trace.append(
            {
                "LocID": entry["LocID"],
                "textOffset": absolute_text_offset,
                "textLength": text_length,
                "aligned16": absolute_text_offset % BINARY_ALIGNMENT_BYTES == 0,
                "toasterOffset": toaster_offset,
                "toasterLength": toaster_length,
                "toasterAligned16": toaster_offset % BINARY_ALIGNMENT_BYTES == 0,
                "rtxOffset": rtx_offset,
                "rtxLength": rtx_length,
                "rtxAligned16": rtx_offset % BINARY_ALIGNMENT_BYTES == 0,
            }
        )

    align_buffer(string_table)
    record_bytes = bytearray()
    for record in records:
        record_bytes.extend(struct.pack(BINARY_RECORD_FORMAT, *record))

    body = bytes(record_bytes) + (b"\x00" * align_length(BINARY_HEADER_SIZE + len(record_bytes))) + bytes(string_table)
    payload_crc = zlib.crc32(body) & 0xFFFFFFFF
    json_crc = zlib.crc32(json_bytes) & 0xFFFFFFFF
    header = struct.pack(
        BINARY_HEADER_FORMAT,
        BINARY_MAGIC,
        BINARY_VERSION,
        BINARY_ENDIAN_MARKER,
        BINARY_HEADER_SIZE,
        BINARY_RECORD_SIZE,
        len(entries),
        table_offset,
        len(string_table),
        json_crc,
        0,
        payload_crc,
        b"\x00" * 24,
    )
    blob = header + body
    if len(blob) % BINARY_ALIGNMENT_BYTES != 0:
        raise RuntimeError("binary blob alignment failure")

    BIN_PATH.parent.mkdir(parents=True, exist_ok=True)
    BIN_PATH.write_bytes(blob)
    blob_sha256 = hashlib.sha256(blob).hexdigest().upper()
    blob_crc32 = zlib.crc32(blob) & 0xFFFFFFFF

    audit = {
        "agent": "XENO_TAXONOMY_WRITER",
        "path": str(BIN_PATH.relative_to(ROOT)).replace("\\", "/"),
        "manifestPath": str(MANIFEST_PATH.relative_to(ROOT)).replace("\\", "/"),
        "magic": BINARY_MAGIC.decode("ascii"),
        "version": BINARY_VERSION,
        "endianness": "little",
        "structFormats": {
            "header": BINARY_HEADER_FORMAT,
            "record": BINARY_RECORD_FORMAT,
        },
        "alignmentBytes": BINARY_ALIGNMENT_BYTES,
        "fileSizeBytes": len(blob),
        "fileAligned16": len(blob) % BINARY_ALIGNMENT_BYTES == 0,
        "headerSizeBytes": BINARY_HEADER_SIZE,
        "recordSizeBytes": BINARY_RECORD_SIZE,
        "recordCount": len(entries),
        "stringTableOffset": table_offset,
        "stringTableSize": len(string_table),
        "sha256": blob_sha256,
        "crc32": f"0x{blob_crc32:08X}",
        "jsonCrc32": f"0x{json_crc:08X}",
        "payloadCrc32": f"0x{payload_crc:08X}",
        "textOffsets": offset_trace,
    }
    BINARY_AUDIT_PATH.parent.mkdir(parents=True, exist_ok=True)
    BINARY_AUDIT_PATH.write_text(json.dumps(audit, indent=2, sort_keys=True), encoding="utf-8")
    manifest = {
        "schema": "H8.TAXONOMY.BINARY_MANIFEST.V2",
        "status": "TAXONOMY_BINARY_CACHE_LOCKED",
        "promptId": "XENO_TAXONOMY_WRITER",
        "json": str(OUT_PATH.relative_to(ROOT)).replace("\\", "/"),
        "binary": str(BIN_PATH.relative_to(ROOT)).replace("\\", "/"),
        "audit": str(BINARY_AUDIT_PATH.relative_to(ROOT)).replace("\\", "/"),
        "mmapReady": True,
        "magic": BINARY_MAGIC.decode("ascii"),
        "version": BINARY_VERSION,
        "endianness": "little",
        "structPackPrefix": "<",
        "endianMarker": f"0x{BINARY_ENDIAN_MARKER:04X}",
        "headerStructFormat": BINARY_HEADER_FORMAT,
        "recordStructFormat": BINARY_RECORD_FORMAT,
        "structFormats": {
            "header": BINARY_HEADER_FORMAT,
            "record": BINARY_RECORD_FORMAT,
        },
        "headerBytes": BINARY_HEADER_SIZE,
        "recordBytes": BINARY_RECORD_SIZE,
        "recordCount": len(entries),
        "fileBytes": len(blob),
        "alignmentBytes": BINARY_ALIGNMENT_BYTES,
        "fileAligned16": len(blob) % BINARY_ALIGNMENT_BYTES == 0,
        "recordAligned16": BINARY_RECORD_SIZE % BINARY_ALIGNMENT_BYTES == 0,
        "stringTableOffset": table_offset,
        "stringTableBytes": len(string_table),
        "sha256": blob_sha256,
        "crc32": f"0x{blob_crc32:08X}",
        "jsonCrc32": f"0x{json_crc:08X}",
        "payloadCrc32": f"0x{payload_crc:08X}",
        "crcScopes": {
            "crc32": "entire binary blob including header",
            "jsonCrc32": "minified UTF-8 Data/Localization/en_US_Taxonomy.json bytes",
            "payloadCrc32": "binary bytes [headerBytes:fileBytes], record table plus padding plus string table",
        },
        "crcByteRanges": {
            "payloadCrc32Start": BINARY_HEADER_SIZE,
            "payloadCrc32End": len(blob),
        },
        "rowIndexFormula": "recordOffset=64+entryIndex*48",
        "tierLaneFormula": "text/toaster/rtx offsets are absolute byte offsets into the 16-byte aligned UTF-8 string table",
        "metadataFormula": "biomeCount | (familyCount << 8) | (categoryFlag << 16)",
        "categoryFlags": {
            "taxonomy_fauna_autopsy": 1,
            "taxonomy_flora_biopsy": 2,
            "taxonomy_madness_variant": 4,
        },
        "headerFields": BINARY_HEADER_FIELDS,
        "recordFields": BINARY_RECORD_FIELDS,
        "headerConstants": {
            "flags": 0,
            "reserved24Bytes": 24,
            "reserved24Fill": "0x00",
        },
        "runtimeContract": {
            "lookupMode": "stateless fixed-record scan or hash-index build by locHash32; no private mutable taxonomy manager required",
            "toasterLane": "read toasterOffset/toasterBytes only for low-tier compact codex text",
            "rtxOverkillLane": "read rtxOffset/rtxBytes for high-res gradient and harmonic-noise metadata",
            "jsonDependency": "JSON is authoring/source proof; binary contains runtime ingest lanes",
        },
    }
    MANIFEST_PATH.write_text(json.dumps(manifest, ensure_ascii=True, separators=(",", ":")), encoding="utf-8", newline="")
    return audit


def hash_map(values: list[str]) -> dict[str, str]:
    return {value: hx(value) for value in values}


def fauna(
    suffix: str,
    entity_id: str,
    common_name: str,
    binomial: str,
    threat_class: str,
    families: list[str],
    biomes: list[str],
    armor_class: str,
    tools: list[str],
    damage_types: list[str],
    weak_points: list[str],
    harvest_notes: str,
    text: str,
) -> dict:
    loc_id = f"TAXONOMY_FAUNA_{suffix}"
    return {
        "LocID": loc_id,
        "Hash": hx(loc_id),
        "Layer": "World",
        "Category": "taxonomy_fauna_autopsy",
        "EntityID": entity_id,
        "EntityHash": hx(entity_id),
        "CommonName": common_name,
        "Binomial": binomial,
        "ThreatClass": threat_class,
        "FamilyIDs": families,
        "FamilyHashes": hash_map(families),
        "BiomeIDs": biomes,
        "BiomeHashes": hash_map(biomes),
        "LotkaVolterraRef": LV_REF,
        "Ecology": LV_SUMMARY,
        "WeakPoints": weak_points,
        "Harvest": {
            "TargetArmorClass": armor_class,
            "WeaponToolIds": tools,
            "DamageTypes": damage_types,
            "Notes": harvest_notes,
        },
        "Text": text,
    }


def flora(
    suffix: str,
    lsystem_id: str,
    common_name: str,
    binomial: str,
    lsystem_biome: str,
    material_class: str,
    vulnerability_mask: list[str],
    tools: list[str],
    damage_types: list[str],
    weak_points: list[str],
    harvest_notes: str,
    text: str,
) -> dict:
    loc_id = f"TAXONOMY_FLORA_{suffix}"
    entity_id = f"l_system.{lsystem_id.lower()}"
    biomes = [f"l_system.biome.{lsystem_biome}"]
    return {
        "LocID": loc_id,
        "Hash": hx(loc_id),
        "Layer": "World",
        "Category": "taxonomy_flora_biopsy",
        "EntityID": entity_id,
        "EntityHash": hx(entity_id),
        "LSystemID": lsystem_id,
        "LSystemSource": f"Data/Flora/LSystem_Library.json#{lsystem_id}",
        "CommonName": common_name,
        "Binomial": binomial,
        "BiomeIDs": biomes,
        "BiomeHashes": hash_map(biomes),
        "HarvestMaterialClass": material_class,
        "VulnerabilityMask": vulnerability_mask,
        "WeakPoints": weak_points,
        "Harvest": {
            "WeaponToolIds": tools,
            "DamageTypes": damage_types,
            "Notes": harvest_notes,
        },
        "Text": text,
    }


def madness(
    suffix: str,
    base_entity_id: str,
    common_name: str,
    binomial: str,
    biomes: list[str],
    text: str,
) -> dict:
    loc_id = f"TAXONOMY_MADNESS_{suffix}"
    return {
        "LocID": loc_id,
        "Hash": hx(loc_id),
        "Layer": "Narrative",
        "Category": "taxonomy_madness_variant",
        "BaseEntityID": base_entity_id,
        "BaseEntityHash": hx(base_entity_id),
        "CommonName": common_name,
        "Binomial": binomial,
        "BiomeIDs": biomes,
        "BiomeHashes": hash_map(biomes),
        "Trigger": "PlayerStress01 > 0.9 or nitrogen_narcosis_signal != 0",
        "Text": text,
    }


def build_entries() -> list[dict]:
    entries: list[dict] = [
        fauna(
            "SHORE_SKIMMER",
            "creature.ambient.shore_skimmer",
            "Shore Skimmer",
            "Litorapinna placida",
            "prey",
            ["fauna.family.littoral_passive", "fauna.family.reef_ambush"],
            ["biome.family.littoral_karst", "biome.family.fossil_reef"],
            "CombatArmorClass.Brittle",
            ["tool_survival_blade", "tool_salvage_sampler"],
            ["CombatDamageTypes.Impact", "CombatDamageTypes.MicroFracture"],
            ["posterior pressure bladder seam", "thin gill hinge behind the pectoral arch"],
            "Cut or sample after stunning the school shadow; do not use thermal tools on edible tissue.",
            "AUTOPSY H8-XF-001: Adult shoal specimen, 0.38 m. External fins show current polishing, not combat damage. Internal bladder is oversized and tears cleanly at the posterior seam. Lotka-Volterra role is prey biomass; heavy harvest starves stalkers and drags hunters toward routes.",
        ),
        fauna(
            "KELP_RAYLET",
            "creature.ambient.kelp_raylet",
            "Kelp Raylet",
            "Laminapinna flexa",
            "prey",
            ["fauna.family.littoral_passive", "fauna.family.crystal_skittish"],
            ["biome.family.fossil_reef", "biome.family.crystal_growth"],
            "CombatArmorClass.OrganicHeavy",
            ["tool_survival_blade", "tool_salvage_sampler"],
            ["CombatDamageTypes.Impact", "CombatDamageTypes.MicroFracture"],
            ["cartilage root at wing base", "oil sac below dorsal plate"],
            "Blade opens the wing root; sampler preserves waterproofing oil without ruining the plate.",
            "AUTOPSY H8-XF-002: Broad grazer, 1.1 m span. Cartilage plates flex like wet alloy but fail at the wing root. Gut contains kelp cellulose and crystal dust. Lotka-Volterra role is slow prey; removing raylets lets kelp overgrow while pack cutters lose forage pressure.",
        ),
        fauna(
            "SILT_DRIFTER",
            "creature.ambient.silt_drifter",
            "Silt Drifter",
            "Sedimentichthys lentus",
            "prey_scavenger",
            ["fauna.family.sediment_scavengers", "fauna.family.abyssal_sparse"],
            ["biome.family.sediment_drift", "biome.family.abyssal_silt", "biome.family.granite_escarpment"],
            "CombatArmorClass.Brittle",
            ["tool_salvage_sampler"],
            ["CombatDamageTypes.MicroFracture", "CombatDamageTypes.Toxic"],
            ["ribbed filter gill", "soft ventral sediment pouch"],
            "Sampler only; toxic silt pouch contaminates food if crushed by impact.",
            "AUTOPSY H8-XF-003: Bottom feeder, 0.62 m. Gills are mineral combs packed with manganese silt. Ventral pouch carries mild toxin and should be lifted intact. Lotka-Volterra role is detritus recycler; overharvest makes prey biomass lag and pushes flatmaws into resource pockets.",
        ),
        fauna(
            "WALL_GLIDER",
            "creature.ambient.wall_glider",
            "Wall Glider",
            "Parietichthys lapsus",
            "prey_scout",
            ["fauna.family.escarpment_watchers", "fauna.family.ridge_hunters"],
            ["biome.family.granite_escarpment", "biome.family.rift_spine"],
            "CombatArmorClass.Brittle",
            ["tool_survival_blade", "tool_salvage_sampler"],
            ["CombatDamageTypes.Impact", "CombatDamageTypes.MicroFracture"],
            ["silica scale row", "cranial wall-grip tendon"],
            "Blade along the scale row; sampler for visor-polish silica before tendons contract.",
            "AUTOPSY H8-XF-004: Cliff fish, 0.54 m. Ventral tendons lock into rock seams after death, so detach the cranial anchor first. Silica scales polish clean. Lotka-Volterra role is wall prey; fewer gliders lowers ridge hunter patrol accuracy but raises hunger aggression.",
        ),
        fauna(
            "BRINE_SIPHONER",
            "creature.ambient.brine_siphoner",
            "Brine Siphoner",
            "Halosiphon gravis",
            "chemical_grazer",
            ["fauna.family.chemical_specialists", "fauna.family.thermal_hostile"],
            ["biome.family.chemosynthetic_brine", "biome.family.tectonic_spine", "biome.family.volcanic_glass"],
            "CombatArmorClass.OrganicHeavy",
            ["tool_laser_cutter", "tool_salvage_sampler"],
            ["CombatDamageTypes.Toxic", "CombatDamageTypes.Thermal"],
            ["lateral salt gland", "black salinity valve under the jaw"],
            "Thermal nick opens the gland; sampler drains brine before the toxic valve ruptures.",
            "AUTOPSY H8-XF-005: Chemical grazer, 0.8 m. Organs separate dense brine through a black valve that bursts under blunt impact. Salt glands contain electrolyte stock. Lotka-Volterra role is boundary prey; harvesting them makes brine stalkers leave pools and follow salinity trails.",
        ),
        fauna(
            "LANTERN_SIFTER",
            "creature.ambient.lantern_sifter",
            "Lantern Sifter",
            "Lucernaformis abyssalis",
            "prey_signal",
            ["fauna.family.abyssal_sparse", "fauna.family.hadal_apex"],
            ["biome.family.abyssal_silt", "biome.family.metallic_hadal", "biome.family.rift_void"],
            "CombatArmorClass.Brittle",
            ["tool_salvage_sampler"],
            ["CombatDamageTypes.MicroFracture"],
            ["ventral photophore chain", "transparent throat sieve"],
            "Sampler only; impact ruins the photophore paint compound.",
            "AUTOPSY H8-XF-006: Abyssal filter fish, 0.41 m. Photophore chain goes dark in one wave when predator pressure rises. Throat sieve is clean and fragile. Lotka-Volterra role is visible prey telemetry; a blackout marks predator conversion, not supernatural warning.",
        ),
        fauna(
            "NURSERY_SHELLGUARD",
            "creature.territorial.nursery_shellguard",
            "Nursery Shellguard",
            "Custodicassis nidalis",
            "territorial",
            ["fauna.family.reef_ambush", "fauna.family.littoral_passive"],
            ["biome.family.fossil_reef", "biome.family.littoral_karst"],
            "CombatArmorClass.Shell",
            ["tool_harpoon_launcher", "tool_laser_cutter"],
            ["CombatDamageTypes.Impact", "CombatDamageTypes.Thermal"],
            ["inner hinge behind left shell ridge", "egg-scarred ventral plate"],
            "Harpoon pins shell; laser cutter opens hinge after aggression drops.",
            "AUTOPSY H8-XF-007: Nest defender, 1.4 m. Shell front is honest armor; hinge behind the left ridge is not. Ventral plate carries old egg scars. Lotka-Volterra role is territorial brake; killing guards opens prey nurseries and causes predator intake spikes one sector later.",
        ),
        fauna(
            "ARCHWAY_SENTINEL",
            "creature.territorial.archway_sentinel",
            "Archway Sentinel",
            "Arcupinna vigilis",
            "territorial",
            ["fauna.family.escarpment_watchers", "fauna.family.ridge_hunters"],
            ["biome.family.granite_escarpment", "biome.family.rift_spine"],
            "CombatArmorClass.Shell",
            ["tool_harpoon_launcher", "tool_stun_pistol"],
            ["CombatDamageTypes.Impact", "CombatDamageTypes.Emp"],
            ["ocellar nerve line", "soft joint under cranial ridge"],
            "EMP stun breaks the route-holding reflex; harpoon only after the nerve line is exposed.",
            "AUTOPSY H8-XF-008: Corridor defender, 1.8 m. Ocellar nerve line wraps the skull and keeps the animal pressing intruders from arches. Joint tissue is soft under the cranial ridge. Lotka-Volterra role is spatial gate; removing it lets ridge packs hunt through safe passes.",
        ),
        fauna(
            "POCKET_AMBUSHER",
            "creature.hunter.pocket_ambusher",
            "Pocket Ambusher",
            "Insidiamandibula brevis",
            "hunter",
            ["fauna.family.reef_ambush", "fauna.family.sediment_scavengers"],
            ["biome.family.fossil_reef", "biome.family.sediment_drift"],
            "CombatArmorClass.OrganicHeavy",
            ["tool_survival_blade", "tool_harpoon_launcher"],
            ["CombatDamageTypes.Impact", "CombatDamageTypes.MicroFracture"],
            ["mandible hinge socket", "blind-side swim bladder"],
            "Harpoon first if alive; blade harvests hinge plates after jaw lock releases.",
            "AUTOPSY H8-XF-009: Ambush predator, 0.9 m. Jaw plates shear forward and lock after death unless the hinge socket is cut. Blind-side bladder is thin. Lotka-Volterra role is local predator; prey loss makes it leave pockets and punish resource greed.",
        ),
        fauna(
            "NEEDLE_HUNTER",
            "creature.hunter.needle_hunter",
            "Needle Hunter",
            "Aciculavenator velox",
            "hunter",
            ["fauna.family.crystal_skittish", "fauna.family.reef_ambush"],
            ["biome.family.crystal_growth", "biome.family.littoral_karst"],
            "CombatArmorClass.Brittle",
            ["tool_stun_pistol", "tool_harpoon_launcher"],
            ["CombatDamageTypes.Emp", "CombatDamageTypes.Impact"],
            ["electroreceptor comb", "needle spine base"],
            "Stun before harpoon; impact shatters spines into usable short needles.",
            "AUTOPSY H8-XF-010: Fast cutter, 0.72 m. Spine bases are brittle and recoverable only if the electroreceptor comb is stunned first. Stomach shows skimmer scales. Lotka-Volterra role is speed predator; prey abundance raises strike frequency, not body count.",
        ),
        fauna(
            "RIDGE_PACK_CUTTER",
            "creature.hunter.ridge_pack_cutter",
            "Ridge Pack Cutter",
            "Cristavenator gregarius",
            "pack_hunter",
            ["fauna.family.ridge_hunters", "fauna.family.escarpment_watchers"],
            ["biome.family.granite_escarpment", "biome.family.rift_spine"],
            "CombatArmorClass.OrganicHeavy",
            ["tool_harpoon_launcher", "tool_stun_pistol"],
            ["CombatDamageTypes.Impact", "CombatDamageTypes.Emp"],
            ["flank fin tendon", "paired throat saws"],
            "EMP breaks pack timing; harpoon the flank tendon before removing saw plates.",
            "AUTOPSY H8-XF-011: Pack cutter, 1.2 m. Throat saws are paired and offset for flanking cuts, not frontal bites. Tendons show burst stress. Lotka-Volterra role is coordinated predator; prey dips convert clean packs into desperate lone interceptors.",
        ),
        fauna(
            "BRINE_STALKER",
            "creature.hunter.brine_stalker",
            "Brine Stalker",
            "Halovenator densus",
            "hunter",
            ["fauna.family.chemical_specialists", "fauna.family.thermal_hostile"],
            ["biome.family.chemosynthetic_brine", "biome.family.tectonic_spine"],
            "CombatArmorClass.OrganicHeavy",
            ["tool_laser_cutter", "tool_harpoon_launcher"],
            ["CombatDamageTypes.Toxic", "CombatDamageTypes.Thermal", "CombatDamageTypes.Impact"],
            ["density bladder", "brine-liver capsule"],
            "Laser vents the density bladder; harpoon keeps the carcass above the brine layer.",
            "AUTOPSY H8-XF-012: Dense brine hunter, 1.6 m. Liver capsule carries neutralizing compounds and bursts under rough lift. Density bladder explains sudden boundary acceleration. Lotka-Volterra role is chemical predator; siphoner scarcity drives it into pump corridors.",
        ),
        fauna(
            "ARMOR_BREAKER",
            "creature.hunter.armor_breaker",
            "Armor Breaker",
            "Ferramordax tardus",
            "heavy_hunter",
            ["fauna.family.metal_predators", "fauna.family.hadal_apex"],
            ["biome.family.metallic_hadal", "biome.family.rift_void"],
            "CombatArmorClass.Shell",
            ["tool_laser_cutter", "tool_harpoon_launcher"],
            ["CombatDamageTypes.Thermal", "CombatDamageTypes.MicroFracture", "CombatDamageTypes.Impact"],
            ["ceramic jaw root", "thin coolant scar behind cranial shell"],
            "Thermal cut the coolant scar; harpoon jaw root only after shell micro-fracture.",
            "AUTOPSY H8-XF-013: Heavy metallic hunter, 2.3 m. Outer shell deflects blunt work; jaw roots contain ceramic plates useful for bracing. Coolant scar is the only clean opening. Lotka-Volterra role is apex pressure; it rises when ordinary predators overfeed.",
        ),
        fauna(
            "HEAT_LURKER",
            "creature.hunter.heat_lurker",
            "Heat Lurker",
            "Thermocryptus abruptus",
            "thermal_hunter",
            ["fauna.family.thermal_hostile", "fauna.family.rift_stalkers"],
            ["biome.family.volcanic_glass", "biome.family.volcanic_hadal"],
            "CombatArmorClass.OrganicHeavy",
            ["tool_stun_pistol", "tool_harpoon_launcher"],
            ["CombatDamageTypes.Emp", "CombatDamageTypes.MicroFracture", "CombatDamageTypes.Impact"],
            ["ventral cooling seam", "heat-sensing pit chain"],
            "EMP blinds pit chain; harpoon the cooling seam after the geyser cycle passes.",
            "AUTOPSY H8-XF-014: Vent ambusher, 1.5 m. Skin carries burn scarring but pit chain fails under EMP. Cooling seam vents hot blood on contact. Lotka-Volterra role is thermal predator; prey depletion makes it use vent bursts as false cover near player routes.",
        ),
        fauna(
            "SHADOW_INTERCEPTOR",
            "creature.hunter.shadow_interceptor",
            "Shadow Interceptor",
            "Umbravenator reversus",
            "hunter",
            ["fauna.family.abyssal_sparse", "fauna.family.void_apex"],
            ["biome.family.abyssal_silt", "biome.family.rift_void"],
            "CombatArmorClass.OrganicHeavy",
            ["tool_stun_pistol", "tool_harpoon_launcher"],
            ["CombatDamageTypes.Emp", "CombatDamageTypes.Impact"],
            ["piezoelectric tooth root", "rear-vector inner ear"],
            "Stun disrupts the rear-vector ear; harpoon teeth only after the animal stops rolling.",
            "AUTOPSY H8-XF-015: Return-route hunter, 1.9 m. Inner ear is tuned to retreat vectors, not frontal pursuit. Teeth contain piezoelectric ridges. Lotka-Volterra role is late predator; prey scarcity turns it from patrol shadow into direct intercept.",
        ),
        fauna(
            "SILT_FLATMAW",
            "creature.hunter.silt_flatmaw",
            "Silt Flatmaw",
            "Limomordax planus",
            "ambush_hunter",
            ["fauna.family.sediment_scavengers", "fauna.family.ridge_hunters"],
            ["biome.family.sediment_drift", "biome.family.granite_escarpment"],
            "CombatArmorClass.OrganicHeavy",
            ["tool_harpoon_launcher", "tool_salvage_sampler"],
            ["CombatDamageTypes.Impact", "CombatDamageTypes.MicroFracture"],
            ["jaw floor cartilage", "dorsal sediment lung"],
            "Harpoon raises the head; sampler drains sediment lung before jaw floor collapses.",
            "AUTOPSY H8-XF-016: Buried ambusher, 1.7 m. Dorsal lung stores silt as camouflage ballast. Jaw floor collapses if lifted wrong. Lotka-Volterra role is scavenger predator; harvest pressure on drifters forces it to attack moving tools.",
        ),
        fauna(
            "HALO_CROWN_LEVIATHAN",
            "creature.leviathan.halo_crown",
            "Halo Crown Leviathan",
            "Hadopelta coronata",
            "apex_leviathan",
            ["fauna.family.hadal_apex", "fauna.family.void_apex"],
            ["biome.family.rift_void", "biome.family.abyssal_silt"],
            "CombatArmorClass.Shell",
            ["tool_harpoon_launcher", "tool_laser_cutter", "tool_stun_pistol"],
            ["CombatDamageTypes.Impact", "CombatDamageTypes.Thermal", "CombatDamageTypes.Emp"],
            ["crown-ring nerve trunk", "soft tissue below halo plate"],
            "Fragments only. Stun to expose nerve trunk; laser samples halo tissue after harpoon anchoring.",
            "AUTOPSY H8-XF-017: Crown fragment, 4.6 m arc recovered. Plate rings show pressure fractures from movement, not death. Nerve trunk is soft under the halo. Lotka-Volterra role is alpha sink; it appears when predator biomass exceeds prey stability and routes start emptying.",
        ),
        fauna(
            "GATE_WARDEN_LEVIATHAN",
            "creature.leviathan.gate_warden",
            "Gate Warden Leviathan",
            "Portacustos profundus",
            "apex_leviathan",
            ["fauna.family.hadal_apex", "fauna.family.rift_stalkers"],
            ["biome.family.rift_spine", "biome.family.volcanic_hadal", "biome.family.metallic_hadal"],
            "CombatArmorClass.Shell",
            ["tool_harpoon_launcher", "tool_laser_cutter"],
            ["CombatDamageTypes.Impact", "CombatDamageTypes.MicroFracture", "CombatDamageTypes.Thermal"],
            ["pressure hinge beside gill gate", "black tendon in route-fin"],
            "Micro-fracture shell first; laser route-fin tendon only after anchoring.",
            "AUTOPSY H8-XF-018: Route-fin section, 3.1 m. Tissue is built to wedge passages, not sprint. Pressure hinge stores elastic load beside the gill gate. Lotka-Volterra role is apex gate; prey collapse makes it occupy choke routes rather than open water.",
        ),
        fauna(
            "RIFT_LANCER_LEVIATHAN",
            "creature.leviathan.rift_lancer",
            "Rift Lancer Leviathan",
            "Fissuraculeus jaculans",
            "apex_leviathan",
            ["fauna.family.rift_stalkers", "fauna.family.void_apex"],
            ["biome.family.rift_void", "biome.family.rift_spine"],
            "CombatArmorClass.OrganicHeavy",
            ["tool_harpoon_launcher", "tool_stun_pistol"],
            ["CombatDamageTypes.Impact", "CombatDamageTypes.Emp"],
            ["lance cartilage socket", "false-charge lateral nerve"],
            "EMP breaks false-charge timing; harpoon only socket tissue shed after impact.",
            "AUTOPSY H8-XF-019: Lunge cartilage, 2.7 m. Socket fibers show repeated S-curve strain and abrupt stop scars. Lateral nerve fires before the real strike. Lotka-Volterra role is apex converter; high prey plus low fear produces spectacle, low prey produces lethal lunges.",
        ),
        fauna(
            "BLACK_CHOIR_LEVIATHAN",
            "creature.leviathan.black_choir",
            "Black Choir Leviathan",
            "Nigracanthus resonans",
            "apex_leviathan",
            ["fauna.family.void_apex", "fauna.family.hadal_apex"],
            ["biome.family.rift_void", "biome.family.abyssal_silt"],
            "CombatArmorClass.Shielded",
            ["tool_env_analyzer", "tool_stun_pistol", "tool_harpoon_launcher"],
            ["CombatDamageTypes.Emp", "CombatDamageTypes.Impact"],
            ["resonance rib lattice", "shielded acoustic sac"],
            "Analyze frequency first; EMP opens the acoustic sac for a narrow harpoon sample.",
            "AUTOPSY H8-XF-020: Resonance rib, 5.2 m chord. Bone behaves like a tuned hull plate and keeps vibrating after dissection. Acoustic sac is shielded. Lotka-Volterra role is apex fear amplifier; predator surplus becomes sound pressure before it becomes contact.",
        ),
        fauna(
            "FURNACE_MAW_LEVIATHAN",
            "creature.leviathan.furnace_maw",
            "Furnace Maw Leviathan",
            "Fornacomordax ignifer",
            "apex_leviathan",
            ["fauna.family.thermal_hostile", "fauna.family.hadal_apex"],
            ["biome.family.volcanic_glass", "biome.family.volcanic_hadal"],
            "CombatArmorClass.Shell",
            ["tool_harpoon_launcher", "tool_laser_cutter"],
            ["CombatDamageTypes.Impact", "CombatDamageTypes.MicroFracture", "CombatDamageTypes.Thermal"],
            ["cooled jaw hinge", "thermal vent palate"],
            "Wait for cold phase; micro-fracture hinge then laser a palate sliver.",
            "AUTOPSY H8-XF-021: Palate slab, 3.8 m. Heat channels form a vent map across the mouth roof. Jaw hinge becomes workable only during cold cycle. Lotka-Volterra role is thermal apex; prey collapse near vents turns false routes into feeding lanes.",
        ),
        fauna(
            "VOID_RIBBON_LEVIATHAN",
            "creature.leviathan.void_ribbon",
            "Void Ribbon Leviathan",
            "Tenebravitta celeris",
            "apex_leviathan",
            ["fauna.family.void_apex", "fauna.family.abyssal_sparse"],
            ["biome.family.abyssal_silt", "biome.family.rift_void"],
            "CombatArmorClass.OrganicHeavy",
            ["tool_harpoon_launcher", "tool_stun_pistol"],
            ["CombatDamageTypes.Impact", "CombatDamageTypes.Emp"],
            ["ribbon-fold nerve rail", "translucent pressure vane"],
            "EMP exposes nerve rail; harpoon vane fragments only after the fold relaxes.",
            "AUTOPSY H8-XF-022: Ribbon vane, 6.4 m. Tissue folds into a dark line until tension releases; then it is broader than the lab table. Nerve rail runs the full fold. Lotka-Volterra role is void apex; when prey signatures vanish, it patrols the empty interval.",
        ),
        flora(
            "TUBULAR_CORAL",
            "SS_001",
            "Tubular Carbonate Colony",
            "Calcarituba porosa",
            "safe_shallows",
            "HarvestableTemplate.MaterialClass.Coral",
            ["FloraDataTemplate.VulnerabilityMask.Drill", "FloraDataTemplate.VulnerabilityMask.PlasmaCut"],
            ["tool_laser_cutter", "tool_salvage_sampler"],
            ["CombatDamageTypes.Thermal", "CombatDamageTypes.MicroFracture"],
            ["tube lip ring", "inner silica sleeve"],
            "Laser opens the lip ring; sampler pulls silica sleeve without crushing colony pores.",
            "BIOPSY H8-XB-001: L-system SS_001. Cross-section shows low fork count and thick tube walls, consistent with authored shallow silhouettes. Flow pulse is a shader cue, not fluid truth. Harvest class Carbonate; drill or laser cut the lip ring.",
        ),
        flora(
            "HALO_SARGASSUM",
            "SS_002",
            "Halo Sargassum",
            "Sargassum coronatum",
            "safe_shallows",
            "HarvestableTemplate.MaterialClass.Sargassum",
            ["FloraDataTemplate.VulnerabilityMask.Cut", "FloraDataTemplate.VulnerabilityMask.Grab"],
            ["tool_survival_blade", "tool_salvage_sampler"],
            ["CombatDamageTypes.Impact", "CombatDamageTypes.MicroFracture"],
            ["upper halo blade joint", "underside seed bladder"],
            "Blade cuts halo joints; sampler preserves seed bladders for fiber yield.",
            "BIOPSY H8-XB-002: L-system SS_002. Floating mat grows from horizontal runners and paired blades. Buoyancy is sold by global sway, not per-frond physics. Harvest class Sargassum; cut or grab only where the seed bladder is visible.",
        ),
        flora(
            "BLOOD_KELP_SPIRE",
            "KF_001",
            "Blood Kelp Spire",
            "Cruorlamina columna",
            "kelp_forest",
            "HarvestableTemplate.MaterialClass.Kelp",
            ["FloraDataTemplate.VulnerabilityMask.Cut"],
            ["tool_survival_blade", "tool_salvage_sampler"],
            ["CombatDamageTypes.Impact", "CombatDamageTypes.MicroFracture"],
            ["vertical blade root", "top gas bladder"],
            "Cut blade root first; sampler collects iron-rich sap before gas bladder vents.",
            "BIOPSY H8-XB-003: L-system KF_001. Tall red blade columns contain iron sap and gas bladders. Current motion should be sheet-level on low hardware. Harvest class Kelp; cut at the root node, not the canopy.",
        ),
        flora(
            "THERMAL_TUBEWORM",
            "TV_001",
            "Thermal Tubeworm",
            "Thermotuba ventralis",
            "thermal_vents",
            "HarvestableTemplate.MaterialClass.Coral",
            ["FloraDataTemplate.VulnerabilityMask.PlasmaCut", "FloraDataTemplate.VulnerabilityMask.Burn"],
            ["tool_laser_cutter", "tool_salvage_sampler"],
            ["CombatDamageTypes.Thermal", "CombatDamageTypes.MicroFracture"],
            ["soft crown leaf ring", "heat-safe tube tip"],
            "Use laser during stable heat; sampler takes crown tissue after vent pulse falls.",
            "BIOPSY H8-XB-004: L-system TV_001. Tube colony follows vent columns and scars asymmetrically around triangle-wave geyser cycles. Harvest class Vent Carbonate; laser cut only after the crown retracts.",
        ),
        flora(
            "GHOST_WEED",
            "DA_001",
            "Ghost Weed",
            "Pallidophyta velata",
            "deep_abyss",
            "HarvestableTemplate.MaterialClass.Kelp",
            ["FloraDataTemplate.VulnerabilityMask.Cut", "FloraDataTemplate.VulnerabilityMask.Grab"],
            ["tool_survival_blade", "tool_salvage_sampler"],
            ["CombatDamageTypes.MicroFracture"],
            ["translucent wisp node", "cold tip spore"],
            "Sampler first, blade second; crushing the wisp node kills the glow marker.",
            "BIOPSY H8-XB-005: L-system DA_001. Sparse pale branches preserve darkness and make silt motion readable. It breathes through shader fade, not simulated gas exchange. Harvest class Kelp; remove wisp nodes only in marked patches.",
        ),
        flora(
            "IRON_CORAL",
            "DA_002",
            "Iron Carbonate Fork",
            "Ferricorallium furcatum",
            "deep_abyss",
            "HarvestableTemplate.MaterialClass.TitaniumOutcrop",
            ["FloraDataTemplate.VulnerabilityMask.Drill", "FloraDataTemplate.VulnerabilityMask.PlasmaCut"],
            ["tool_laser_cutter", "tool_salvage_sampler"],
            ["CombatDamageTypes.Thermal", "CombatDamageTypes.MicroFracture"],
            ["rust plate seam", "armored branch tip"],
            "Drill or laser cut rust seams; sampler catches ferrous grit from the branch tip.",
            "BIOPSY H8-XB-006: L-system DA_002. Cone branches bind metal and refuse sway; particles around it show current instead. Harvest class TitaniumOutcrop by runtime template; drill seams, not living core.",
        ),
        flora(
            "CATHEDRAL_KELP",
            "KF_002",
            "Cathedral Kelp",
            "Columnaria kelpica",
            "kelp_forest",
            "HarvestableTemplate.MaterialClass.Kelp",
            ["FloraDataTemplate.VulnerabilityMask.Cut", "FloraDataTemplate.VulnerabilityMask.Grab"],
            ["tool_survival_blade", "tool_salvage_sampler"],
            ["CombatDamageTypes.Impact", "CombatDamageTypes.MicroFracture"],
            ["root-to-canopy rib", "side stained-glass frond"],
            "Cut lower rib segments only; canopy fronds are route language and should remain.",
            "BIOPSY H8-XB-007: L-system KF_002. Root cadence builds nave-like corridors for navigation. On high tier the canopy buys visual overkill; low tier keeps broad sheet sway. Harvest class Kelp; sample ribs, not route silhouettes.",
        ),
        flora(
            "RIFT_RIBBON",
            "KF_005",
            "Rift Ribbon",
            "Riftalga atra",
            "kelp_forest",
            "HarvestableTemplate.MaterialClass.Kelp",
            ["FloraDataTemplate.VulnerabilityMask.Cut", "FloraDataTemplate.VulnerabilityMask.Burn"],
            ["tool_survival_blade", "tool_laser_cutter"],
            ["CombatDamageTypes.Impact", "CombatDamageTypes.Thermal"],
            ["ragged ribbon cut", "scorched tip node"],
            "Blade harvests cool ribbons; laser only on heat-resistant nodes.",
            "BIOPSY H8-XB-008: L-system KF_005. Long black-green ribbons mark geothermal shear through shader bend. Harvest class Kelp; use cut path for fiber and thermal path only where scorched nodes are visible.",
        ),
        flora(
            "LANTERN_GRASS",
            "SS_003",
            "Lantern Grass",
            "Lucerba siltica",
            "safe_shallows",
            "HarvestableTemplate.MaterialClass.Kelp",
            ["FloraDataTemplate.VulnerabilityMask.Cut", "FloraDataTemplate.VulnerabilityMask.Grab"],
            ["tool_survival_blade", "tool_salvage_sampler"],
            ["CombatDamageTypes.MicroFracture"],
            ["terminal glow seed", "paired side blade"],
            "Grab glow seed with sampler; blade cuts paired side blades for fiber.",
            "BIOPSY H8-XB-009: L-system SS_003. Low fan roots and terminal glow seeds create a predator-pressure cue. The dimming is presentation alarm, not sensory truth. Harvest class Kelp; preserve enough seeds for route readability.",
        ),
        flora(
            "STATIC_THICKET",
            "DA_003",
            "Static Thicket",
            "Electrodendron cautum",
            "deep_abyss",
            "HarvestableTemplate.MaterialClass.Coral",
            ["FloraDataTemplate.VulnerabilityMask.Stun", "FloraDataTemplate.VulnerabilityMask.Drill"],
            ["tool_stun_pistol", "tool_salvage_sampler"],
            ["CombatDamageTypes.Emp", "CombatDamageTypes.MicroFracture"],
            ["conductive leaf flag", "charge nodule"],
            "EMP discharges nodule; sampler removes conductive flags after tick noise stops.",
            "BIOPSY H8-XB-010: L-system DA_003. Short thicket branches cluster charge points and stay readable in abyss light. Electrical ticking is an audio fake from motion state. Harvest class Conductive Carbonate; stun before drilling.",
        ),
        madness(
            "HALO_CROWN",
            "creature.leviathan.halo_crown",
            "Halo Crown Leviathan",
            "Hadopelta coronata",
            ["biome.family.rift_void", "biome.family.abyssal_silt"],
            "CORRUPTED NECROPSY: crown rings are not bones. They are doorways arranged around a wound. The specimen did not die; the lab entered it and left its own shape behind.",
        ),
        madness(
            "GATE_WARDEN",
            "creature.leviathan.gate_warden",
            "Gate Warden Leviathan",
            "Portacustos profundus",
            ["biome.family.rift_spine", "biome.family.volcanic_hadal", "biome.family.metallic_hadal"],
            "CORRUPTED NECROPSY: every route-fin points to the exit you already missed. The hinge opens inward. The hallway was dissected first. The body is only the lock.",
        ),
        madness(
            "RIFT_LANCER",
            "creature.leviathan.rift_lancer",
            "Rift Lancer Leviathan",
            "Fissuraculeus jaculans",
            ["biome.family.rift_void", "biome.family.rift_spine"],
            "CORRUPTED NECROPSY: lateral nerve fired before the animal moved, before the diver saw it, before the report was written. The false charge is the honest one.",
        ),
        madness(
            "BLACK_CHOIR",
            "creature.leviathan.black_choir",
            "Black Choir Leviathan",
            "Nigracanthus resonans",
            ["biome.family.rift_void", "biome.family.abyssal_silt"],
            "CORRUPTED NECROPSY: rib lattice still sings at zero pressure. The note is behind the teeth. If you write the frequency down, it writes the room back.",
        ),
        madness(
            "FURNACE_MAW",
            "creature.leviathan.furnace_maw",
            "Furnace Maw Leviathan",
            "Fornacomordax ignifer",
            ["biome.family.volcanic_glass", "biome.family.volcanic_hadal"],
            "CORRUPTED NECROPSY: palate channels carry heat in the shape of a map. Follow the map and the mouth closes. Ignore it and the map follows you.",
        ),
        madness(
            "VOID_RIBBON",
            "creature.leviathan.void_ribbon",
            "Void Ribbon Leviathan",
            "Tenebravitta celeris",
            ["biome.family.abyssal_silt", "biome.family.rift_void"],
            "CORRUPTED NECROPSY: the ribbon is a line because the eye is poor equipment. In section, it is a room. In motion, it is a verdict.",
        ),
    ]
    return entries


def compile_payload() -> dict:
    entries = build_entries()
    apply_scalability(entries)
    return {
        "schema": "H8.TAXONOMY.LOCALIZATION.V1",
        "language": "en_US",
        "status": "TAXONOMY COMPILED",
        "polishStatus": "VERIFIED MASTER GRADE",
        "offlineOnly": True,
        "evidenceClass": "STATIC_DOC_PLUS_CLI_PYTHON_VERIFIER",
        "hash": {
            "algorithm": "FNV-1a 32-bit over UTF-16LE code units",
            "offset_basis": "0x811C9DC5",
            "prime": "0x01000193",
            "runtime_match": "Hecton.Localization.LocHash.Compute",
            "collisionPolicy": "All taxonomy LocID/entity/family/biome hashes must have zero distinct-ID collisions in verifier.",
        },
        "binaryCache": {
            "path": "Data/Localization/en_US_Taxonomy.h8bin",
            "manifestPath": "Data/Localization/en_US_Taxonomy.manifest.json",
            "magic": "H8TX",
            "version": BINARY_VERSION,
            "endianness": "little",
            "alignmentBytes": BINARY_ALIGNMENT_BYTES,
            "headerSizeBytes": BINARY_HEADER_SIZE,
            "recordSizeBytes": BINARY_RECORD_SIZE,
            "headerStruct": BINARY_HEADER_FORMAT,
            "recordStruct": BINARY_RECORD_FORMAT,
        },
        "mathAudit": {
            "physicsLutsOrMatrices": "N/A: taxonomy text/data only; no Beer-Lambert, Dalton, Sabine, pressure, lighting, or acoustic LUT is emitted.",
            "derivedConstants": [
                "Lotka-Volterra coefficients are copied from EcosystemDirector.cs:720-723.",
                "Ultra harmonic overlay values are deterministic presentation metadata derived from FNV hash bytes divided by 255 and tau phase; not simulation truth.",
            ],
            "magicNumberPolicy": "No gameplay physics constants are invented by this compiler.",
        },
        "atlasFit": {
            "source": "Docs/PROJECT_ATLAS.md",
            "observedAssemblyCount": 83,
            "observedDomainIndexCount": 85,
            "domainCountFinding": "PROJECT_ATLAS separates 83 first-party asmdefs from the 85-domain index; taxonomy fits the 85-domain map through static data, localization, biota, and scalability domains.",
            "relevantDomainIds": [4, 8, 21, 24, 65, 83],
            "relevantFamilies": [
                "Data Monolith (Static DB)",
                "Scalability Dictator (Hardware)",
                "Ecosystem Director (Macro)",
                "Predator Cognition (Utility AI)",
                "Hecton8.UI.Localization",
                "The Chronicler (Docs)",
            ],
        },
        "hPhiAudit": {
            "runtimeClaim": "PENDING VERIFICATION",
            "dataSovereigntyIncrease": "Stateless JSON plus aligned binary cache; no Unity runtime private state added.",
            "statelessLookupReady": True,
            "privateRuntimeStateAdded": False,
            "evidenceClass": "STATIC_SOURCE_PLUS_CLI_SCRIPT",
        },
        "sourcePrompt": {
            "path": "Docs/Tasks/CURRENT_BATCH.md",
            "promptId": "XENO_TAXONOMY_WRITER",
            "role": "WRITER_ARCHITECT",
            "taskCount": 15,
        },
        "engineContracts": {
            "damageTypes": ENGINE_DAMAGE_TYPES,
            "armorClasses": ENGINE_ARMOR_CLASSES,
            "toolIds": ENGINE_TOOL_IDS,
            "floraMaterialClasses": FLORA_MATERIAL_CLASSES,
            "floraVulnerabilityMasks": FLORA_VULNERABILITY_MASKS,
            "lotkaVolterra": {
                "id": LV_REF,
                "source": LV_SOURCE,
                "preyBirthRatePerSecond": 0.012,
                "predationRatePerSecond": 0.00045,
                "predatorGrowthRatePerSecond": 0.00014,
                "predatorDeathRatePerSecond": 0.006,
                "summary": LV_SUMMARY,
            },
        },
        "counts": {
            "faunaAutopsies": sum(1 for e in entries if e["Category"] == "taxonomy_fauna_autopsy"),
            "floraBiopsies": sum(1 for e in entries if e["Category"] == "taxonomy_flora_biopsy"),
            "madnessVariants": sum(1 for e in entries if e["Category"] == "taxonomy_madness_variant"),
        },
        "uiLimits": {
            "maxTextChars": 620,
            "maxWeakPointChars": 80,
            "maxHarvestNoteChars": 180,
        },
        "entries": entries,
    }


def main() -> int:
    payload = compile_payload()
    OUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    json_text = json.dumps(payload, ensure_ascii=False, separators=(",", ":"))
    with OUT_PATH.open("w", encoding="utf-8", newline="") as f:
        f.write(json_text)
    binary_audit = write_binary_cache(payload, json_text.encode("utf-8"))
    print(f"WROTE {OUT_PATH}")
    print(f"WROTE {BIN_PATH} bytes={binary_audit['fileSizeBytes']} aligned16={binary_audit['fileAligned16']}")
    print(f"WROTE {MANIFEST_PATH}")
    print(
        "COUNTS fauna={faunaAutopsies} flora={floraBiopsies} madness={madnessVariants}".format(
            **payload["counts"]
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
