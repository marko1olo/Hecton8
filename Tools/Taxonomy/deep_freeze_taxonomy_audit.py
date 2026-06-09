#!/usr/bin/env python3
"""Final consistency audit for XENO taxonomy data artifacts."""

from __future__ import annotations

import ast
import json
import hashlib
import struct
import zlib
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
TAXONOMY_JSON = ROOT / "Data" / "Localization" / "en_US_Taxonomy.json"
TAXONOMY_BIN = ROOT / "Data" / "Localization" / "en_US_Taxonomy.h8bin"
TAXONOMY_MANIFEST = ROOT / "Data" / "Localization" / "en_US_Taxonomy.manifest.json"
PROJECT_ATLAS = ROOT / "Docs" / "PROJECT_ATLAS.md"
ECONOMY_PROOF = ROOT / "Docs" / "AgentLogs" / "TaxonomyEconomyMillionStep_XENO_TAXONOMY_WRITER.json"
VERIFY_SWEEP = ROOT / "Docs" / "AgentLogs" / "VerifySweep_XENO_TAXONOMY_WRITER.json"
OPTICS_MANIFEST = ROOT / "Data" / "Visuals" / "Water_Extinction_Matrix.json"
DALTON_MANIFEST = ROOT / "Data" / "Precomputed" / "dalton_gas_toxicity_manifest.json"
SABINE_MANIFEST = ROOT / "Data" / "Audio" / "Acoustic_LUT.manifest.json"
DATA_INQUISITION_REPORT = ROOT / "Docs" / "AgentLogs" / "DataInquisition_XENO_TAXONOMY_WRITER.json"
OPTICS_REPORT = ROOT / "Docs" / "AgentLogs" / "OpticsVerification_XENO_TAXONOMY_WRITER.json"
STATUS_FILE = ROOT / "Docs" / "Tasks" / "Status_XENO_TAXONOMY_WRITER.md"
RATIONALE_FILE = ROOT / "Docs" / "AgentLogs" / "Rationale_XENO_TAXONOMY_WRITER.md"
LOG_FILE = ROOT / "Docs" / "AgentLogs" / "LOG_XENO_TAXONOMY_WRITER.md"
TAXONOMY_TOOL_DIR = ROOT / "Tools" / "Taxonomy"
REPORT_JSON = ROOT / "Docs" / "AgentLogs" / "TaxonomyDeepFreezeAudit_XENO_TAXONOMY_WRITER.json"
REPORT_MD = ROOT / "Docs" / "AgentLogs" / "TaxonomyDeepFreezeAudit_XENO_TAXONOMY_WRITER.md"
ARTIFACT_SEAL = ROOT / "Docs" / "AgentLogs" / "TaxonomyArtifactSeal_XENO_TAXONOMY_WRITER.json"

FNV_OFFSET = 0x811C9DC5
FNV_PRIME = 0x01000193
ALIGNMENT = 16
HEADER_FORMAT = "<4sHHIIIIIIII24s"
RECORD_FORMAT = "<IIIIIIIIIIII"
HEADER_SIZE = struct.calcsize(HEADER_FORMAT)
RECORD_SIZE = struct.calcsize(RECORD_FORMAT)
STRUCT_FORMAT_SIZE_BYTES = {
    HEADER_FORMAT: HEADER_SIZE,
    RECORD_FORMAT: RECORD_SIZE,
}
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
BANNED_ENTRY_TERMS = (
    "todo",
    "tbd",
    "placeholder",
    "lorem",
    "ipsum",
    "clean sci-fi",
    "pristine",
    "sleek",
    "utopian",
    "nanotech",
    "quantum",
)
INDUSTRIAL_NOIR_TERMS = (
    "autopsy",
    "biopsy",
    "necropsy",
    "pressure",
    "brine",
    "silt",
    "rust",
    "hull",
    "bulkhead",
    "vent",
    "gill",
    "jaw",
    "spine",
    "tendon",
    "bladder",
    "plate",
    "scar",
    "wound",
    "oil",
    "ash",
    "drill",
    "laser",
    "sampler",
    "current",
    "black",
)
CLINICAL_TERMS = ("autopsy", "biopsy", "necropsy")


def fnv1a_utf16le(value: str) -> int:
    h = FNV_OFFSET
    for byte in value.encode("utf-16le"):
        h ^= byte
        h = (h * FNV_PRIME) & 0xFFFFFFFF
    return h


def load_json(path: Path) -> Any:
    if not path.exists():
        return {
            "status": "MISSING",
            "path": rel(path),
            "passed": False,
            "summary": {},
            "graphAudit": {},
            "failures": ["missing"],
        }
    with path.open("r", encoding="utf-8-sig") as handle:
        return json.load(handle)


def rel(path: Path) -> str:
    return str(path.relative_to(ROOT)).replace("\\", "/")


def sha256_file(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def record_failure(failures: list[str], code: str) -> None:
    failures.append(code)


def check_entry_terms(entries: list[dict[str, Any]]) -> dict[str, Any]:
    hits: list[str] = []
    industrial_misses: list[str] = []
    clinical_misses: list[str] = []
    term_density: dict[str, int] = {}
    for entry in entries:
        fields = [
            entry.get("CommonName", ""),
            entry.get("Text", ""),
            entry.get("Harvest", {}).get("Notes", ""),
            entry.get("Scalability", {}).get("Toaster", {}).get("summary", ""),
            json.dumps(entry.get("Scalability", {}).get("RTXOverkill", {}), ensure_ascii=False, separators=(",", ":")),
        ]
        lowered = "\n".join(fields).lower()
        for term in BANNED_ENTRY_TERMS:
            if term in lowered:
                hits.append(f"{entry.get('LocID')}:{term}")
        industrial_hits = sum(1 for term in INDUSTRIAL_NOIR_TERMS if term in lowered)
        clinical_hits = sum(1 for term in CLINICAL_TERMS if term in lowered)
        term_density[str(entry.get("LocID"))] = industrial_hits
        if industrial_hits < 2:
            industrial_misses.append(str(entry.get("LocID")))
        if clinical_hits == 0:
            clinical_misses.append(str(entry.get("LocID")))
    return {
        "hits": hits,
        "industrialMisses": industrial_misses,
        "clinicalMisses": clinical_misses,
        "minimumIndustrialTermHits": min(term_density.values()) if term_density else 0,
        "termDensity": term_density,
        "passed": not hits and not industrial_misses and not clinical_misses,
    }


def check_hashes(entries: list[dict[str, Any]]) -> dict[str, Any]:
    owners: dict[int, set[str]] = {}
    mismatches: list[str] = []
    for entry in entries:
        pairs: list[tuple[str, str]] = [(entry["LocID"], entry["Hash"])]
        if "EntityID" in entry:
            pairs.append((entry["EntityID"], entry["EntityHash"]))
        if "BaseEntityID" in entry:
            pairs.append((entry["BaseEntityID"], entry["BaseEntityHash"]))
        pairs.extend(entry.get("BiomeHashes", {}).items())
        pairs.extend(entry.get("FamilyHashes", {}).items())
        for id_value, hex_hash in pairs:
            expected = fnv1a_utf16le(id_value)
            actual = int(hex_hash, 16)
            if actual != expected:
                mismatches.append(f"{id_value}:0x{actual:08X}!=0x{expected:08X}")
            owners.setdefault(actual, set()).add(id_value)
    collisions = {
        f"0x{hash_value:08X}": sorted(values)
        for hash_value, values in owners.items()
        if len(values) > 1
    }
    return {
        "distinctHashCount": len(owners),
        "mismatches": mismatches,
        "collisions": collisions,
        "passed": not mismatches and not collisions,
    }


def check_binary(data: dict[str, Any], raw_json: bytes) -> dict[str, Any]:
    failures: list[str] = []
    blob = TAXONOMY_BIN.read_bytes()
    if len(blob) % ALIGNMENT != 0:
        record_failure(failures, "file_not_16_byte_aligned")
    header = struct.unpack(HEADER_FORMAT, blob[:HEADER_SIZE])
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
    if magic != b"H8TX":
        record_failure(failures, "bad_magic")
    if version != 2:
        record_failure(failures, "version_not_2")
    if endian_marker != 0x0102:
        record_failure(failures, "endian_marker_not_little")
    if header_size != HEADER_SIZE or record_size != RECORD_SIZE:
        record_failure(failures, "struct_size_mismatch")
    if record_size % ALIGNMENT != 0:
        record_failure(failures, "record_not_16_byte_aligned")
    if record_count != len(data["entries"]):
        record_failure(failures, "record_count_mismatch")
    if string_table_offset % ALIGNMENT != 0:
        record_failure(failures, "string_table_not_16_byte_aligned")
    if json_crc != (zlib.crc32(raw_json) & 0xFFFFFFFF):
        record_failure(failures, "json_crc_mismatch")
    if flags != 0:
        record_failure(failures, "header_flags_nonzero")
    if payload_crc != (zlib.crc32(blob[HEADER_SIZE:]) & 0xFFFFFFFF):
        record_failure(failures, "payload_crc_mismatch")

    records_end = header_size + (record_count * record_size)
    if records_end > string_table_offset:
        record_failure(failures, "records_overlap_string_table")
    if string_table_offset + string_table_size > len(blob):
        record_failure(failures, "string_table_out_of_bounds")

    payload_failures: list[str] = []
    for index, entry in enumerate(data["entries"]):
        record_start = header_size + (index * record_size)
        record = struct.unpack(RECORD_FORMAT, blob[record_start : record_start + record_size])
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
        toaster = entry["Scalability"]["Toaster"]["summary"]
        rtx = json.dumps(entry["Scalability"]["RTXOverkill"], ensure_ascii=False, separators=(",", ":"))
        expected_hashes = (
            (loc_hash, fnv1a_utf16le(entry["LocID"]), "loc_hash"),
            (entity_hash, fnv1a_utf16le(entity_id), "entity_hash"),
            (category_hash, fnv1a_utf16le(entry["Category"]), "category_hash"),
            (common_hash, fnv1a_utf16le(entry["CommonName"]), "common_hash"),
            (tier_hash, fnv1a_utf16le(f"{toaster}|{rtx}"), "tier_hash"),
        )
        for actual, expected, label in expected_hashes:
            if actual != expected:
                payload_failures.append(f"{entry['LocID']}:{label}")
        expected_metadata = (
            (len(entry.get("BiomeIDs", [])) & 0xFF)
            | ((len(entry.get("FamilyIDs", [])) & 0xFF) << 8)
            | ((0x01 if entry["Category"] == "taxonomy_fauna_autopsy" else 0x02 if entry["Category"] == "taxonomy_flora_biopsy" else 0x04) << 16)
        )
        if metadata != expected_metadata:
            payload_failures.append(f"{entry['LocID']}:metadata")

        for label, offset, byte_length, expected_text in (
            ("text", text_offset, text_length, entry["Text"]),
            ("toaster", toaster_offset, toaster_length, toaster),
            ("rtx", rtx_offset, rtx_length, rtx),
        ):
            if offset % ALIGNMENT != 0:
                payload_failures.append(f"{entry['LocID']}:{label}_alignment")
            if offset < string_table_offset or offset + byte_length >= len(blob):
                payload_failures.append(f"{entry['LocID']}:{label}_range")
                continue
            if blob[offset : offset + byte_length] != expected_text.encode("utf-8"):
                payload_failures.append(f"{entry['LocID']}:{label}_bytes")
            if blob[offset + byte_length] != 0:
                payload_failures.append(f"{entry['LocID']}:{label}_terminator")

    return {
        "path": rel(TAXONOMY_BIN),
        "version": version,
        "recordSize": record_size,
        "fileBytes": len(blob),
        "fileAligned16": len(blob) % ALIGNMENT == 0,
        "headerFormat": HEADER_FORMAT,
        "recordFormat": RECORD_FORMAT,
        "stringTableOffset": string_table_offset,
        "stringTableSize": string_table_size,
        "payloadFailures": payload_failures,
        "failures": failures,
        "passed": not failures and not payload_failures,
    }


def check_taxonomy_manifest(data: dict[str, Any], raw_json: bytes, binary_check: dict[str, Any]) -> dict[str, Any]:
    failures: list[str] = []
    if not TAXONOMY_MANIFEST.exists():
        return {"path": rel(TAXONOMY_MANIFEST), "failures": ["manifest_missing"], "passed": False}
    raw_manifest = TAXONOMY_MANIFEST.read_text(encoding="utf-8")
    if "\n" in raw_manifest.strip() or "\r" in raw_manifest.strip():
        failures.append("manifest_not_minified")
    manifest = json.loads(raw_manifest)
    blob = TAXONOMY_BIN.read_bytes()
    expected_payload_crc = zlib.crc32(blob[HEADER_SIZE:]) & 0xFFFFFFFF
    expected_json_crc = zlib.crc32(raw_json) & 0xFFFFFFFF
    expected_crc = zlib.crc32(blob) & 0xFFFFFFFF
    checks = {
        "schema": manifest.get("schema") == "H8.TAXONOMY.BINARY_MANIFEST.V2",
        "status": manifest.get("status") == "TAXONOMY_BINARY_CACHE_LOCKED",
        "json": manifest.get("json") == rel(TAXONOMY_JSON),
        "binary": manifest.get("binary") == rel(TAXONOMY_BIN),
        "mmapReady": manifest.get("mmapReady") is True,
        "endianness": manifest.get("endianness") == "little" and manifest.get("structPackPrefix") == "<",
        "magicVersion": manifest.get("magic") == "H8TX" and manifest.get("version") == 2,
        "endianMarker": manifest.get("endianMarker") == "0x0102",
        "formats": manifest.get("headerStructFormat") == HEADER_FORMAT and manifest.get("recordStructFormat") == RECORD_FORMAT,
        "structFormats": manifest.get("structFormats") == {"header": HEADER_FORMAT, "record": RECORD_FORMAT},
        "sizes": manifest.get("headerBytes") == HEADER_SIZE and manifest.get("recordBytes") == RECORD_SIZE,
        "recordCount": manifest.get("recordCount") == len(data["entries"]),
        "fileBytes": manifest.get("fileBytes") == len(blob),
        "alignment": manifest.get("alignmentBytes") == ALIGNMENT and manifest.get("fileAligned16") is True and manifest.get("recordAligned16") is True,
        "stringTableOffset": manifest.get("stringTableOffset") == binary_check.get("stringTableOffset"),
        "stringTableBytes": manifest.get("stringTableBytes") == binary_check.get("stringTableSize"),
        "sha256": manifest.get("sha256") == sha256_file(TAXONOMY_BIN),
        "crc32": manifest.get("crc32") == f"0x{expected_crc:08X}",
        "jsonCrc32": manifest.get("jsonCrc32") == f"0x{expected_json_crc:08X}",
        "payloadCrc32": manifest.get("payloadCrc32") == f"0x{expected_payload_crc:08X}",
        "crcScopes": manifest.get("crcScopes") == {
            "crc32": "entire binary blob including header",
            "jsonCrc32": "minified UTF-8 Data/Localization/en_US_Taxonomy.json bytes",
            "payloadCrc32": "binary bytes [headerBytes:fileBytes], record table plus padding plus string table",
        },
        "crcByteRanges": manifest.get("crcByteRanges") == {
            "payloadCrc32Start": HEADER_SIZE,
            "payloadCrc32End": len(blob),
        },
        "rowIndexFormula": manifest.get("rowIndexFormula") == "recordOffset=64+entryIndex*48",
        "metadataFormula": manifest.get("metadataFormula") == "biomeCount | (familyCount << 8) | (categoryFlag << 16)",
        "headerFields": manifest.get("headerFields") == BINARY_HEADER_FIELDS,
        "recordFields": manifest.get("recordFields") == BINARY_RECORD_FIELDS,
        "headerConstants": manifest.get("headerConstants") == {
            "flags": 0,
            "reserved24Bytes": 24,
            "reserved24Fill": "0x00",
        },
        "jsonManifestPath": data.get("binaryCache", {}).get("manifestPath") == rel(TAXONOMY_MANIFEST),
    }
    for key, ok in checks.items():
        if not ok:
            failures.append(key)
    return {
        "path": rel(TAXONOMY_MANIFEST),
        "schema": manifest.get("schema"),
        "status": manifest.get("status"),
        "fileBytes": manifest.get("fileBytes"),
        "sha256": manifest.get("sha256"),
        "checks": checks,
        "failures": failures,
        "passed": not failures,
    }


def check_text_reports() -> dict[str, Any]:
    stale_patterns = (
        "version 1, 64-byte header, 32-byte records",
        "<IIIIIHHII",
    )
    stale_hits: list[str] = []
    missing_reports: list[str] = []
    for path in (STATUS_FILE, RATIONALE_FILE, LOG_FILE):
        if not path.exists():
            missing_reports.append(rel(path))
            continue
        text = path.read_text(encoding="utf-8")
        for pattern in stale_patterns:
            if pattern in text:
                stale_hits.append(f"{rel(path)}:{pattern}")
    return {
        "staleHits": stale_hits,
        "missingReports": missing_reports,
        "passed": not stale_hits and not missing_reports,
    }


def static_string(node: ast.AST, constants: dict[str, str]) -> str | None:
    if isinstance(node, ast.Constant) and isinstance(node.value, str):
        return node.value
    if isinstance(node, ast.Name):
        return constants.get(node.id)
    return None


def collect_module_string_constants(tree: ast.Module) -> dict[str, str]:
    constants: dict[str, str] = {}
    for node in tree.body:
        if isinstance(node, ast.Assign) and isinstance(node.value, ast.Constant) and isinstance(node.value.value, str):
            for target in node.targets:
                if isinstance(target, ast.Name):
                    constants[target.id] = node.value.value
        elif isinstance(node, ast.AnnAssign) and isinstance(node.target, ast.Name):
            if isinstance(node.value, ast.Constant) and isinstance(node.value.value, str):
                constants[node.target.id] = node.value.value
    return constants


def struct_call_name(node: ast.Call) -> str | None:
    if not isinstance(node.func, ast.Attribute):
        return None
    if not isinstance(node.func.value, ast.Name) or node.func.value.id != "struct":
        return None
    if node.func.attr in ("pack", "unpack", "calcsize", "Struct"):
        return node.func.attr
    return None


def check_source_struct_contract() -> dict[str, Any]:
    failures: list[str] = []
    calls: list[dict[str, Any]] = []
    layout_constants: list[dict[str, Any]] = []
    files = sorted(TAXONOMY_TOOL_DIR.glob("*.py"))
    for path in files:
        source = path.read_text(encoding="utf-8")
        try:
            tree = ast.parse(source, filename=rel(path))
        except SyntaxError as exc:
            failures.append(f"{rel(path)}:syntax:{exc.lineno}")
            continue
        constants = collect_module_string_constants(tree)
        for name, fmt in sorted(constants.items()):
            if name.endswith(("HEADER_FORMAT", "RECORD_FORMAT", "BINARY_HEADER_FORMAT", "BINARY_RECORD_FORMAT")):
                if not fmt.startswith("<"):
                    failures.append(f"{rel(path)}:{name}:not_little_endian")
                    continue
                size = STRUCT_FORMAT_SIZE_BYTES.get(fmt)
                if size is None:
                    failures.append(f"{rel(path)}:{name}:unknown_layout_size")
                    continue
                aligned16 = size % ALIGNMENT == 0
                layout_constants.append(
                    {
                        "path": rel(path),
                        "name": name,
                        "format": fmt,
                        "bytes": size,
                        "aligned16": aligned16,
                    }
                )
                if not aligned16:
                    failures.append(f"{rel(path)}:{name}:not_16_byte_aligned")
        for node in ast.walk(tree):
            if not isinstance(node, ast.Call):
                continue
            call_name = struct_call_name(node)
            if call_name is None:
                continue
            fmt = static_string(node.args[0], constants) if node.args else None
            call = {
                "path": rel(path),
                "line": int(getattr(node, "lineno", 0)),
                "call": f"struct.{call_name}",
                "format": fmt,
            }
            calls.append(call)
            if fmt is None:
                failures.append(f"{call['path']}:{call['line']}:dynamic_struct_format")
            elif not fmt.startswith("<"):
                failures.append(f"{call['path']}:{call['line']}:non_little_endian:{fmt}")
    return {
        "filesScanned": len(files),
        "callsChecked": len(calls),
        "calls": calls,
        "layoutConstants": layout_constants,
        "failures": failures,
        "passed": not failures,
    }


def unique_hash_rows(rows: list[dict[str, Any]], key: str) -> bool:
    values = [row.get(key) for row in rows]
    return len(values) == len(set(values))


def check_physics_trace() -> dict[str, Any]:
    failures: list[str] = []
    optics = load_json(OPTICS_MANIFEST)
    dalton = load_json(DALTON_MANIFEST)
    sabine = load_json(SABINE_MANIFEST)
    data_inquisition = load_json(DATA_INQUISITION_REPORT)
    optics_report = load_json(OPTICS_REPORT)

    if "Beer" not in str(optics.get("physicsBasis", "")) or "exp(-mu" not in str(optics.get("formula", "")):
        failures.append("optics_beer_lambert_basis_missing")
    if optics.get("byteOrder") != "little-endian" or optics.get("pythonPackingContract") != "<e":
        failures.append("optics_endian_or_pack_mismatch")
    if optics.get("binaryAlignmentBytes") != ALIGNMENT:
        failures.append("optics_alignment_mismatch")
    variants = optics.get("variants", {})
    optics_shas: dict[str, str] = {}
    if not {"toaster_i3", "main_mx350", "rtx_overkill"}.issubset(variants):
        failures.append("optics_variants_missing")
    for variant_id, variant in variants.items():
        file_path = OPTICS_MANIFEST.parent / str(variant.get("file", ""))
        if not file_path.exists():
            failures.append(f"optics_{variant_id}_file_missing")
            continue
        actual_sha = sha256_file(file_path)
        optics_shas[variant_id] = actual_sha
        if actual_sha != str(variant.get("sha256", "")).upper():
            failures.append(f"optics_{variant_id}_sha256_mismatch")
        if file_path.stat().st_size != int(variant.get("bytes", -1)):
            failures.append(f"optics_{variant_id}_size_mismatch")
        if file_path.stat().st_size % ALIGNMENT != 0 or variant.get("aligned16") is not True:
            failures.append(f"optics_{variant_id}_alignment")
    if optics.get("validation", {}).get("fnvCollisionCount") != 0:
        failures.append("optics_hash_collision")
    if optics_report.get("status") != "OPTICS_LUT_VERIFIED":
        failures.append("optics_verifier_report_not_pass")

    if dalton.get("status") != "DALTON_GAS_TOXICITY_BAKED":
        failures.append("dalton_status_mismatch")
    if dalton.get("endianness") != "little" or dalton.get("floatFormat") != "<f":
        failures.append("dalton_endian_or_float_mismatch")
    if not str(dalton.get("headerFormat", "")).startswith("<") or not str(dalton.get("rowFormat", "")).startswith("<"):
        failures.append("dalton_struct_prefix_mismatch")
    if dalton.get("fileAligned16") is not True or dalton.get("rowAligned16") is not True:
        failures.append("dalton_alignment_mismatch")
    dalton_binary = ROOT / str(dalton.get("binary", ""))
    if not dalton_binary.exists():
        failures.append("dalton_binary_missing")
        dalton_sha = ""
    elif dalton_binary.stat().st_size != int(dalton.get("fileBytes", -1)) or dalton_binary.stat().st_size % ALIGNMENT != 0:
        failures.append("dalton_binary_size_or_alignment")
        dalton_sha = sha256_file(dalton_binary)
    else:
        dalton_sha = sha256_file(dalton_binary)
    if dalton_sha and dalton_sha != str(dalton.get("sha256", "")).upper():
        failures.append("dalton_sha256_mismatch")
    dalton_physics = json.dumps(dalton.get("physics", {}), sort_keys=True)
    for token in ("partialPressure", "hydrostaticPressure", "oxygenCnsCurve"):
        if token not in dalton_physics:
            failures.append(f"dalton_physics_missing_{token}")
    dalton_tiers = {row.get("id") for row in dalton.get("qualityTiers", [])}
    if not {"toaster_i3", "rtx_overkill"}.issubset(dalton_tiers):
        failures.append("dalton_tiers_missing")
    if not unique_hash_rows(dalton.get("hashes", []), "fnv1a32"):
        failures.append("dalton_hash_collision")

    if sabine.get("endianness") != "little" or sabine.get("recordFormat") != "<ff" or sabine.get("simdGroupFormat") != "<ffff":
        failures.append("sabine_endian_or_struct_mismatch")
    if int(sabine.get("fileAlignmentBytes", 0)) != ALIGNMENT:
        failures.append("sabine_alignment_mismatch")
    sabine_binary = ROOT / str(sabine.get("binary", ""))
    if not sabine_binary.exists():
        failures.append("sabine_binary_missing")
        sabine_sha = ""
    elif sabine_binary.stat().st_size != int(sabine.get("fileBytes", -1)) or sabine_binary.stat().st_size % ALIGNMENT != 0:
        failures.append("sabine_binary_size_or_alignment")
        sabine_sha = sha256_file(sabine_binary)
    else:
        sabine_sha = sha256_file(sabine_binary)
    if sabine_sha and sabine_sha != str(sabine.get("sha256", "")).upper():
        failures.append("sabine_sha256_mismatch")
    sabine_physics = json.dumps(sabine.get("physics", {}), sort_keys=True)
    for token in ("beerLambert", "hydrostaticPressure", "thorpAbsorptionDbPerKm"):
        if token not in sabine_physics:
            failures.append(f"sabine_physics_missing_{token}")
    sabine_tiers = {row.get("id") for row in sabine.get("qualityTiers", [])}
    if not {"toaster_i3", "rtx_overkill"}.issubset(sabine_tiers):
        failures.append("sabine_tiers_missing")
    if not unique_hash_rows(sabine.get("hashes", []), "fnv1a32"):
        failures.append("sabine_hash_collision")

    if data_inquisition.get("status") != "DATA_INQUISITION_VERIFIED_STATIC_ONLY":
        failures.append("data_inquisition_status_mismatch")
    physics_sources = data_inquisition.get("physicsSources", {}).get("physicsTokens", {})
    for token in ("beer_lambert", "dalton", "sabine", "snell"):
        if physics_sources.get(token) is not True:
            failures.append(f"data_inquisition_missing_{token}")
    if data_inquisition.get("manifestEndianness", {}).get("endianness") != "little":
        failures.append("data_inquisition_endianness_mismatch")
    if data_inquisition.get("structFormats", {}).get("failures"):
        failures.append("data_inquisition_struct_failures")

    return {
        "failures": failures,
        "optics": {
            "basis": optics.get("physicsBasis"),
            "formula": optics.get("formula"),
            "pack": optics.get("pythonPackingContract"),
            "variants": sorted(variants),
            "sha256": optics_shas,
        },
        "dalton": {
            "status": dalton.get("status"),
            "rowFormat": dalton.get("rowFormat"),
            "tiers": sorted(dalton_tiers),
            "bytes": dalton.get("fileBytes"),
            "sha256": dalton_sha,
        },
        "sabine": {
            "recordFormat": sabine.get("recordFormat"),
            "simdGroupFormat": sabine.get("simdGroupFormat"),
            "tiers": sorted(sabine_tiers),
            "bytes": sabine.get("fileBytes"),
            "sha256": sabine_sha,
        },
        "dataInquisition": {
            "status": data_inquisition.get("status"),
            "binaryCount": data_inquisition.get("binaryAlignment", {}).get("count"),
            "structFormats": data_inquisition.get("structFormats", {}).get("checkedFormats"),
        },
        "passed": not failures,
    }


def build_artifact_seal() -> dict[str, Any]:
    artifacts = [
        TAXONOMY_JSON,
        TAXONOMY_BIN,
        TAXONOMY_MANIFEST,
        ECONOMY_PROOF,
        VERIFY_SWEEP,
        OPTICS_MANIFEST,
        OPTICS_MANIFEST.parent / "Water_Extinction_Matrix.bin",
        OPTICS_MANIFEST.parent / "Water_Extinction_Matrix_Toaster.bin",
        OPTICS_MANIFEST.parent / "Water_Extinction_Matrix_Overkill.bin",
        DALTON_MANIFEST,
        ROOT / "Data" / "Precomputed" / "dalton_gas_toxicity.bin",
        SABINE_MANIFEST,
        ROOT / "Data" / "Audio" / "Acoustic_LUT.bin",
        ROOT / "Tools" / "Taxonomy" / "compile_taxonomy.py",
        ROOT / "Tools" / "Taxonomy" / "verify_taxonomy.py",
        ROOT / "Tools" / "Taxonomy" / "audit_taxonomy_data.py",
        ROOT / "Tools" / "Taxonomy" / "run_taxonomy_economy_million_step.py",
        ROOT / "Tools" / "Taxonomy" / "run_verify_sweep.py",
        ROOT / "Tools" / "Taxonomy" / "deep_freeze_taxonomy_audit.py",
    ]
    rows: list[dict[str, Any]] = []
    failures: list[str] = []
    for path in artifacts:
        row: dict[str, Any] = {
            "path": rel(path),
            "exists": path.exists(),
        }
        if path.exists():
            size = path.stat().st_size
            binary_like = path.suffix.lower() in (".bin", ".h8bin")
            row.update(
                {
                    "bytes": size,
                    "sha256": sha256_file(path),
                    "binaryLike": binary_like,
                    "aligned16": (size % ALIGNMENT == 0) if binary_like else None,
                }
            )
            if binary_like and size % ALIGNMENT != 0:
                failures.append(f"{rel(path)}:not_16_byte_aligned")
        else:
            failures.append(f"{rel(path)}:missing")
        rows.append(row)
    seal = {
        "agent": "XENO_TAXONOMY_WRITER",
        "schema": "H8.TAXONOMY.ARTIFACT_SEAL.V1",
        "artifactCount": len(rows),
        "artifacts": rows,
        "failures": failures,
        "passed": not failures,
    }
    ARTIFACT_SEAL.write_text(json.dumps(seal, indent=2, sort_keys=True), encoding="utf-8")
    return seal


def main() -> int:
    raw = TAXONOMY_JSON.read_bytes()
    data = json.loads(raw.decode("utf-8"))
    atlas_text = PROJECT_ATLAS.read_text(encoding="utf-8")
    economy = load_json(ECONOMY_PROOF)
    sweep = load_json(VERIFY_SWEEP)
    artifact_seal = build_artifact_seal()
    binary_check = check_binary(data, raw)
    checks = {
        "minifiedJson": {"passed": b"\n" not in raw and b"\r" not in raw},
        "counts": {
            "counts": data.get("counts", {}),
            "passed": data.get("counts", {}).get("faunaAutopsies") == 22
            and data.get("counts", {}).get("floraBiopsies") == 10
            and data.get("counts", {}).get("madnessVariants") == 6,
        },
        "entryTerms": check_entry_terms(data["entries"]),
        "hashes": check_hashes(data["entries"]),
        "binary": binary_check,
        "binaryManifest": check_taxonomy_manifest(data, raw, binary_check),
        "atlas": {
            "has85DomainHeading": "### 85 Identified Domains" in atlas_text,
            "staticAssemblyCount83": "Static scan found `83` first-party" in atlas_text,
            "passed": "### 85 Identified Domains" in atlas_text and "Static scan found `83` first-party" in atlas_text,
        },
        "hPhi": {
            "statelessLookupReady": data.get("hPhiAudit", {}).get("statelessLookupReady") is True,
            "privateRuntimeStateAddedActual": data.get("hPhiAudit", {}).get("privateRuntimeStateAdded"),
            "privateRuntimeStateAbsent": data.get("hPhiAudit", {}).get("privateRuntimeStateAdded") is False,
            "passed": data.get("hPhiAudit", {}).get("statelessLookupReady") is True
            and data.get("hPhiAudit", {}).get("privateRuntimeStateAdded") is False,
        },
        "economy": {
            "passedFlag": economy.get("passed") is True,
            "steps": economy.get("summary", {}).get("monte_carlo_steps"),
            "failures": economy.get("summary", {}).get("failures"),
            "graphCycles": economy.get("graphAudit", {}).get("cycleCount"),
            "passed": economy.get("passed") is True
            and int(economy.get("summary", {}).get("monte_carlo_steps", 0)) >= 1_000_000
            and economy.get("summary", {}).get("failures") == 0
            and economy.get("graphAudit", {}).get("cycleCount") == 0,
        },
        "verifySweep": {
            "status": sweep.get("status"),
            "passedCount": sweep.get("passedCount"),
            "totalCount": sweep.get("totalCount"),
            "passed": sweep.get("status") == "PASS" and sweep.get("passedCount") == sweep.get("totalCount"),
        },
        "sourceStructContract": check_source_struct_contract(),
        "physicsTrace": check_physics_trace(),
        "artifactSeal": {
            "path": rel(ARTIFACT_SEAL),
            "artifactCount": artifact_seal["artifactCount"],
            "failures": artifact_seal["failures"],
            "passed": artifact_seal["passed"],
        },
        "textReports": check_text_reports(),
    }
    passed = all(check.get("passed") is True for check in checks.values())
    report = {
        "agent": "XENO_TAXONOMY_WRITER",
        "schema": "H8.TAXONOMY.DEEP_FREEZE_AUDIT.V1",
        "status": "PASS" if passed else "FAIL",
        "checks": checks,
    }
    REPORT_JSON.parent.mkdir(parents=True, exist_ok=True)
    REPORT_JSON.write_text(json.dumps(report, indent=2, sort_keys=True), encoding="utf-8")
    REPORT_MD.write_text(
        "# Taxonomy Deep Freeze Audit\n\n"
        f"Status: {report['status']}\n"
        f"Binary: version {checks['binary']['version']}, record {checks['binary']['recordSize']}, bytes {checks['binary']['fileBytes']}, aligned16 {checks['binary']['fileAligned16']}\n"
        f"Binary manifest: {checks['binaryManifest']['status']}, failures {len(checks['binaryManifest']['failures'])}\n"
        f"Economy steps: {checks['economy']['steps']}, failures {checks['economy']['failures']}, graph cycles {checks['economy']['graphCycles']}\n"
        f"Verify sweep: {checks['verifySweep']['passedCount']}/{checks['verifySweep']['totalCount']}\n"
        f"Struct source calls: {checks['sourceStructContract']['callsChecked']}, failures {len(checks['sourceStructContract']['failures'])}\n"
        f"Physics trace failures: {len(checks['physicsTrace']['failures'])}\n"
        f"Artifact seal: {checks['artifactSeal']['artifactCount']} artifacts, failures {len(checks['artifactSeal']['failures'])}\n",
        encoding="utf-8",
    )
    print(
        f"TAXONOMY DEEP FREEZE {report['status']} "
        f"binaryV={checks['binary']['version']} record={checks['binary']['recordSize']} "
        f"steps={checks['economy']['steps']} sweep={checks['verifySweep']['passedCount']}/{checks['verifySweep']['totalCount']} "
        f"structCalls={checks['sourceStructContract']['callsChecked']}"
    )
    return 0 if passed else 1


if __name__ == "__main__":
    raise SystemExit(main())
