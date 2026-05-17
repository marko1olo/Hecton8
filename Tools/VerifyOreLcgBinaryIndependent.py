"""Independent binary verifier for Ore_Distribution.h8bin.

This verifier does not import `OreLcgBaker.py`; it checks public binary layout
against JSON and CSV artifacts with locally declared constants.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import struct
import zlib
from pathlib import Path
from typing import Any


FNV_OFFSET = 2166136261
FNV_PRIME = 16777619
UINT32_MASK = 0xFFFFFFFF
TABLE_VERSION_ID = "economy.ore_lcg_distribution.v1"
TABLE_VERSION_HASH32 = 3968294156
LCG_MULTIPLIER = 1664525
LCG_INCREMENT = 1013904223
LCG_MODULUS_BITS = 32
SAFE_SHALLOWS_ID = "biome.safe_shallows"
TITANIUM_ID = "Data_TitaniumScrap"

JSON_PATH = Path("Data/Economy/Ore_Distribution.json")
BINARY_PATH = Path("Data/Economy/Ore_Distribution.h8bin")
HISTOGRAM_PATH = Path("Data/Economy/Ore_Distribution_Histogram.csv")
SOURCE_MATRIX_PATH = Path("Data/Economy/Resource_Distribution_Matrix.csv")
REPORT_PATH = Path("Docs/AgentLogs/VerifyOreLcgBinaryIndependent_RESOURCE_SPAWN_LCG_TABLES.json")

HEADER_FORMAT = "<4sHH14I"
BIOME_FORMAT = "<IHHBBBBI"
RESOURCE_FORMAT = "<IHBB"
ULTRA_FORMAT = "<IHHHHI"
HEADER_SIZE = struct.calcsize(HEADER_FORMAT)
BIOME_SIZE = struct.calcsize(BIOME_FORMAT)
RESOURCE_SIZE = struct.calcsize(RESOURCE_FORMAT)
ULTRA_SIZE = struct.calcsize(ULTRA_FORMAT)
MAGIC = b"H8OL"
ENDIAN_MARKER = 0x01020304


def fnv1a32(value: str) -> int:
    h = FNV_OFFSET
    for byte in value.encode("utf-16le"):
        h ^= byte
        h = (h * FNV_PRIME) & UINT32_MASK
    return h


def align16(value: int) -> int:
    return (value + 15) & ~15


def require(condition: bool, message: str, failures: list[str]) -> None:
    if not condition:
        failures.append(message)


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(65536), b""):
            digest.update(chunk)
    return digest.hexdigest()


def verify_source_matrix(root: Path, payload: dict[str, Any], failures: list[str]) -> dict[str, Any]:
    path = root / SOURCE_MATRIX_PATH
    with path.open("r", encoding="utf-8", newline="") as handle:
        rows = list(csv.DictReader(handle))
    biomes: list[str] = []
    resources: set[str] = set()
    hash_failures: list[str] = []
    for row in rows:
        if row["biome_id"] not in biomes:
            biomes.append(row["biome_id"])
        resources.add(row["resource_id"])
        if int(row["biome_hash32"]) != fnv1a32(row["biome_id"]):
            hash_failures.append(row["biome_id"])
        if int(row["resource_hash32"]) != fnv1a32(row["resource_id"]):
            hash_failures.append(row["resource_id"])
    require(len(rows) == 150, f"source matrix row count expected 150 got {len(rows)}", failures)
    require(len(biomes) == 10, f"source matrix biome count expected 10 got {len(biomes)}", failures)
    require(len(resources) == 15, f"source matrix resource count expected 15 got {len(resources)}", failures)
    require(not hash_failures, f"source FNV mismatches: {hash_failures[:8]}", failures)
    require(payload.get("source_sha256") == sha256_file(path), "source SHA mismatch", failures)
    return {"rows": len(rows), "biomeCount": len(biomes), "resourceCount": len(resources), "hashFailures": hash_failures}


def unpack_header(blob: bytes, failures: list[str]) -> dict[str, Any]:
    require(len(blob) >= HEADER_SIZE, "binary shorter than header", failures)
    values = struct.unpack(HEADER_FORMAT, blob[:HEADER_SIZE])
    keys = (
        "magic",
        "version",
        "headerBytes",
        "endianMarker",
        "tableVersionHash32",
        "multiplierA",
        "incrementC",
        "modulusBits",
        "biomeCount",
        "resourceCount",
        "biomeOffset",
        "resourceOffset",
        "minimalOffset",
        "ultraOffset",
        "fileSizeBytes",
        "payloadCrc32",
        "fnvCollisionCount",
    )
    return dict(zip(keys, values))


def verify_header(blob: bytes, payload: dict[str, Any], header: dict[str, Any], failures: list[str]) -> None:
    require(header["magic"] == MAGIC, "magic must be H8OL", failures)
    require(header["version"] == 1, "version must be 1", failures)
    require(header["headerBytes"] == HEADER_SIZE == 64, "header must be 64 bytes", failures)
    require(header["endianMarker"] == ENDIAN_MARKER, "endian marker mismatch", failures)
    require(header["tableVersionHash32"] == TABLE_VERSION_HASH32, "table version hash mismatch", failures)
    require(header["tableVersionHash32"] == fnv1a32(TABLE_VERSION_ID), "table version hash not FNV schema hash", failures)
    require(header["multiplierA"] == LCG_MULTIPLIER, "LCG multiplier mismatch", failures)
    require(header["incrementC"] == LCG_INCREMENT, "LCG increment mismatch", failures)
    require(header["modulusBits"] == LCG_MODULUS_BITS, "LCG modulus bits mismatch", failures)
    require(header["biomeCount"] == 10, "biome count mismatch", failures)
    require(header["resourceCount"] == 150, "resource count mismatch", failures)
    require(header["fileSizeBytes"] == len(blob), "file size mismatch", failures)
    require(header["fnvCollisionCount"] == 0, "header reports FNV collisions", failures)
    require(len(blob) % 16 == 0, "binary file size not 16-byte aligned", failures)
    for key in ("biomeOffset", "resourceOffset", "minimalOffset", "ultraOffset", "fileSizeBytes"):
        require(header[key] % 16 == 0, f"{key} is not 16-byte aligned", failures)
    require(header["payloadCrc32"] == (zlib.crc32(blob[HEADER_SIZE:]) & UINT32_MASK), "payload CRC mismatch", failures)
    require(payload.get("binary_cache", {}).get("sha256") == hashlib.sha256(blob).hexdigest(), "payload SHA mismatch", failures)


def verify_biome_records(blob: bytes, payload: dict[str, Any], header: dict[str, Any], failures: list[str]) -> int:
    checked = 0
    for biome_index, biome in enumerate(payload.get("biomes", [])):
        offset = header["biomeOffset"] + biome_index * BIOME_SIZE
        raw = struct.unpack(BIOME_FORMAT, blob[offset : offset + BIOME_SIZE])
        require(raw[0] == biome["biome_hash32"], f"biome hash mismatch {biome['biome_id']}", failures)
        require(raw[1] == biome_index * 15, f"resource offset mismatch {biome['biome_id']}", failures)
        require(raw[2] == biome["total_weight_u16"], f"total weight mismatch {biome['biome_id']}", failures)
        require(raw[3] == 15, f"resource count mismatch {biome['biome_id']}", failures)
        require(raw[4] == biome["base_density_u8"], f"density mismatch {biome['biome_id']}", failures)
        require(raw[5] == biome["clumping_factor_u8"], f"clump mismatch {biome['biome_id']}", failures)
        require(raw[6] == 0, f"flags must be zero {biome['biome_id']}", failures)
        require(raw[7] == biome["ultra_visual_seed_u32"], f"ultra seed mismatch {biome['biome_id']}", failures)
        checked += 1
    return checked


def verify_resource_records(blob: bytes, payload: dict[str, Any], header: dict[str, Any], failures: list[str]) -> int:
    checked = 0
    for biome_index, biome in enumerate(payload.get("biomes", [])):
        last_cumulative = 0
        for resource_index, resource in enumerate(biome["resources"]):
            flat_index = biome_index * 15 + resource_index
            offset = header["resourceOffset"] + flat_index * RESOURCE_SIZE
            raw = struct.unpack(RESOURCE_FORMAT, blob[offset : offset + RESOURCE_SIZE])
            expected = (
                resource["resource_hash32"],
                resource["cumulative_weight_u16"],
                resource["weight_u8"],
                resource["resource_tier"],
            )
            require(raw == expected, f"resource record mismatch {biome['biome_id']}/{resource['resource_id']}", failures)
            require(resource["cumulative_weight_u16"] > last_cumulative, f"cumulative weights not increasing {biome['biome_id']}", failures)
            last_cumulative = resource["cumulative_weight_u16"]
            checked += 1
        require(last_cumulative == biome["total_weight_u16"], f"last cumulative != total {biome['biome_id']}", failures)
    return checked


def verify_minimal_section(blob: bytes, payload: dict[str, Any], header: dict[str, Any], failures: list[str]) -> dict[str, Any]:
    offset = header["minimalOffset"]
    biome_count = len(payload.get("biomes", []))
    resource_count = int(header["resourceCount"])
    density = list(blob[offset : offset + biome_count])
    clump_offset = offset + biome_count
    clump = list(blob[clump_offset : clump_offset + biome_count])
    total_offset = clump_offset + biome_count
    totals = [struct.unpack("<H", blob[total_offset + i * 2 : total_offset + i * 2 + 2])[0] for i in range(biome_count)]
    weight_offset = total_offset + biome_count * 2
    weights = list(blob[weight_offset : weight_offset + resource_count])
    payload_bytes = biome_count + biome_count + biome_count * 2 + resource_count
    section_bytes = align16(payload_bytes)
    cache = payload.get("binary_cache", {})
    require(density == payload.get("density_map_u8"), "minimal density section mismatch", failures)
    require(clump == payload.get("clumping_factors_u8"), "minimal clump section mismatch", failures)
    require(totals == [biome["total_weight_u16"] for biome in payload.get("biomes", [])], "minimal total weights mismatch", failures)
    require(weights == payload.get("weight_matrix_u8_flat"), "minimal weight matrix mismatch", failures)
    require(header["ultraOffset"] - offset == section_bytes, "minimal section byte count mismatch", failures)
    require(cache.get("minimal_lod_bytes") == section_bytes, "binary cache minimal LOD bytes mismatch", failures)
    require(cache.get("minimal_lod_payload_bytes") == payload_bytes, "binary cache minimal payload bytes mismatch", failures)
    require(cache.get("minimal_weight_matrix_bytes") == resource_count, "binary cache minimal weight byte count mismatch", failures)
    return {"density": density, "clump": clump, "totalWeightU16": totals, "weightMatrixCount": len(weights), "payloadBytes": payload_bytes, "sectionBytes": section_bytes}


def verify_ultra_section(blob: bytes, payload: dict[str, Any], header: dict[str, Any], failures: list[str]) -> int:
    checked = 0
    for biome_index, biome in enumerate(payload.get("biomes", [])):
        offset = header["ultraOffset"] + biome_index * ULTRA_SIZE
        raw = struct.unpack(ULTRA_FORMAT, blob[offset : offset + ULTRA_SIZE])
        expected = (
            biome["ultra_visual_seed_u32"],
            biome["ultra_gradient_low_u16"],
            biome["ultra_gradient_mid_u16"],
            biome["ultra_gradient_high_u16"],
            biome["ultra_harmonic_noise_u16"],
            0,
        )
        require(raw == expected, f"ultra record mismatch {biome['biome_id']}", failures)
        require(raw[1] <= raw[2] <= raw[3], f"ultra gradient order mismatch {biome['biome_id']}", failures)
        checked += 1
    return checked


def verify_histogram(root: Path, payload: dict[str, Any], failures: list[str]) -> dict[str, Any]:
    with (root / HISTOGRAM_PATH).open("r", encoding="utf-8", newline="") as handle:
        rows = list(csv.DictReader(handle))
    require(len(rows) == 150, f"histogram rows expected 150 got {len(rows)}", failures)
    safe = [row for row in rows if row["biome_id"] == SAFE_SHALLOWS_ID and row["resource_id"] == TITANIUM_ID]
    require(len(safe) == 1, "histogram missing Safe Shallows Titanium", failures)
    if safe:
        require(safe[0]["expected_basis_points"] == "5000", "Safe Shallows Titanium histogram bp mismatch", failures)
        require(
            int(safe[0]["simulated_count"]) == payload.get("simulation", {}).get("safe_shallows_titanium_simulated_count"),
            "Safe Shallows Titanium simulated count mismatch",
            failures,
        )
    return {"rows": len(rows), "safeShallowsTitaniumRows": len(safe)}


def verify_payload_policy(payload: dict[str, Any], failures: list[str]) -> dict[str, Any]:
    science = payload.get("science_basis", {})
    quality = payload.get("quality_tiers", {})
    validation = payload.get("validation", {})
    require(payload.get("simulation", {}).get("iterations_per_biome") == 1_000_000, "simulation metadata not 1,000,000", failures)
    require(validation.get("safe_shallows_titanium_basis_points") == 5000, "Safe Shallows Titanium not 5000 bp", failures)
    require(validation.get("fnv_collision_count") == 0, "payload reports FNV collisions", failures)
    require("hydrostatic_pressure_pa" in science.get("hydrostatic_pressure", ""), "hydrostatic formula label missing", failures)
    require("not used for ore authority" in science.get("beer_lambert", ""), "Beer-Lambert boundary missing", failures)
    require("not used for ore authority" in science.get("dalton", ""), "Dalton boundary missing", failures)
    require("not used for ore authority" in science.get("sabine", ""), "Sabine boundary missing", failures)
    require("weight_matrix_u8_flat" in quality.get("minimal_toaster", {}).get("fields", []), "minimal toaster weight matrix field missing", failures)
    require(quality.get("ultra_overkill", {}).get("authority", "").startswith("visual-only"), "ultra authority must be visual-only", failures)
    return {"iterationsPerBiome": payload.get("simulation", {}).get("iterations_per_biome"), "safeShallowsTitaniumBasisPoints": validation.get("safe_shallows_titanium_basis_points")}


def build_report(root: Path) -> dict[str, Any]:
    failures: list[str] = []
    payload = read_json(root / JSON_PATH)
    blob = (root / BINARY_PATH).read_bytes()
    header = unpack_header(blob, failures)
    verify_header(blob, payload, header, failures)
    source = verify_source_matrix(root, payload, failures)
    biome_records = verify_biome_records(blob, payload, header, failures)
    resource_checked = verify_resource_records(blob, payload, header, failures)
    minimal = verify_minimal_section(blob, payload, header, failures)
    ultra_checked = verify_ultra_section(blob, payload, header, failures)
    histogram = verify_histogram(root, payload, failures)
    policy = verify_payload_policy(payload, failures)
    return {
        "status": "ORE_LCG_BINARY_INDEPENDENT_VERIFIED_STATIC_ONLY" if not failures else "ORE_LCG_BINARY_INDEPENDENT_FAILED",
        "evidenceClass": "STATIC_SOURCE/STATIC_DOC/PY_TOOL",
        "runtimeProof": "PENDING_VERIFICATION",
        "binary": {
            "path": str(BINARY_PATH).replace("\\", "/"),
            "bytes": len(blob),
            "endian": "<",
            "header": {key: (value.decode("ascii") if isinstance(value, bytes) else value) for key, value in header.items()},
            "sha256": hashlib.sha256(blob).hexdigest(),
        },
        "sourceMatrix": source,
        "biomeRecordsChecked": biome_records,
        "resourceRecordsChecked": resource_checked,
        "minimalSection": minimal,
        "ultraRecordsChecked": ultra_checked,
        "histogram": histogram,
        "policy": policy,
        "failures": failures,
    }


def write_report(root: Path, report: dict[str, Any]) -> Path:
    path = root / REPORT_PATH
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return path


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=str(Path(__file__).resolve().parents[1]), help="Repository root.")
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    root = Path(args.root).resolve()
    report = build_report(root)
    path = write_report(root, report)
    print(f"VERIFY_ORE_LCG_BINARY_INDEPENDENT_STATUS: {report['status']}")
    print(f"report={path}")
    print(f"binaryBytes={report.get('binary', {}).get('bytes', 0)}")
    print(f"resourceRecordsChecked={report.get('resourceRecordsChecked', 0)}")
    print("runtimeProof=PENDING_VERIFICATION")
    return 0 if not report["failures"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
