"""Verify ore LCG JSON/CSV/binary artifacts.

This gate imports the baker to verify writer constants against generated data.
Runtime proof remains outside this script.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import struct
import sys
import zlib
from pathlib import Path
from typing import Any


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import OreLcgBaker as baker  # noqa: E402


REPORT_PATH = Path("Docs/AgentLogs/VerifyOreLcg_RESOURCE_SPAWN_LCG_TABLES.json")
STERILE_TERMS = ("sterile", "clean sci-fi", "pristine", "utopian")


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def require(condition: bool, message: str, failures: list[str]) -> None:
    if not condition:
        failures.append(message)


def verify_payload(root: Path, payload: dict[str, Any], failures: list[str]) -> dict[str, Any]:
    science = payload.get("science_basis", {})
    validation = payload.get("validation", {})
    lcg = payload.get("lcg", {})
    quality = payload.get("quality_tiers", {})

    require(payload.get("schema_id") == baker.TABLE_VERSION_ID, "schema id mismatch", failures)
    require(payload.get("schema_hash32") == baker.fnv1a32(baker.TABLE_VERSION_ID), "schema hash mismatch", failures)
    require(payload.get("table_version_hash32") == baker.TABLE_VERSION_HASH32, "table hash mismatch", failures)
    require(payload.get("source_sha256") == baker.sha256_file(root / baker.MATRIX_INPUT), "source SHA mismatch", failures)
    require(lcg.get("multiplier_a") == baker.LCG_MULTIPLIER, "LCG multiplier mismatch", failures)
    require(lcg.get("increment_c") == baker.LCG_INCREMENT, "LCG increment mismatch", failures)
    require(lcg.get("modulus_m") == baker.LCG_MODULUS, "LCG modulus mismatch", failures)
    require(lcg.get("modulus_bits") == 32, "LCG modulus bits mismatch", failures)
    require(lcg.get("selection_mapping") == "multiply_high_to_range", "selection mapping must be multiply-high", failures)

    biomes = payload.get("biomes", [])
    weights = payload.get("weight_matrix_u8_flat", [])
    cumulative = payload.get("cumulative_matrix_u16_flat", [])
    require(len(biomes) == 10, "payload must contain 10 biomes", failures)
    require(len(weights) == 150, "weight matrix must contain 150 values", failures)
    require(len(cumulative) == 150, "cumulative matrix must contain 150 values", failures)
    require(len(payload.get("density_map_u8", [])) == 10, "density map must contain 10 values", failures)
    require(len(payload.get("clumping_factors_u8", [])) == 10, "clump map must contain 10 values", failures)
    require(all(isinstance(value, int) and 0 <= value <= 255 for value in weights), "weights must be integer u8", failures)
    require(validation.get("safe_shallows_titanium_basis_points") == 5000, "Safe Shallows Titanium must be 5000 bp", failures)
    require(validation.get("safe_shallows_titanium_exact_50") is True, "Safe Shallows Titanium exact flag missing", failures)
    require(validation.get("fnv_collision_count") == 0, "payload reports FNV collisions", failures)
    require(validation.get("table_version_hash_matches_schema_id") is True, "table hash schema flag false", failures)
    require(payload.get("simulation", {}).get("iterations_per_biome") == 1_000_000, "simulation must be 1,000,000 iterations", failures)

    require(science.get("density_u8_range") == [baker.MIN_DENSITY_U8, baker.MAX_DENSITY_U8], "density range provenance mismatch", failures)
    require(science.get("clump_u8_range") == [baker.MIN_CLUMP_U8, baker.MAX_CLUMP_U8], "clump range provenance mismatch", failures)
    require("hydrostatic_pressure_pa" in science.get("hydrostatic_pressure", ""), "hydrostatic formula missing", failures)
    require("not used for ore authority" in science.get("beer_lambert", ""), "Beer-Lambert boundary missing", failures)
    require("not used for ore authority" in science.get("dalton", ""), "Dalton boundary missing", failures)
    require("not used for ore authority" in science.get("sabine", ""), "Sabine boundary missing", failures)
    require("weight_matrix_u8_flat" in quality.get("minimal_toaster", {}).get("fields", []), "toaster weight matrix field missing", failures)
    require("ultra_harmonic_noise_u16" in quality.get("ultra_overkill", {}).get("fields", []), "ultra harmonic field missing", failures)
    return {
        "status": payload.get("status"),
        "iterationsPerBiome": payload.get("simulation", {}).get("iterations_per_biome"),
        "safeShallowsTitaniumBasisPoints": validation.get("safe_shallows_titanium_basis_points"),
        "weightCount": len(weights),
    }


def verify_binary(root: Path, payload: dict[str, Any], failures: list[str]) -> dict[str, Any]:
    binary_path = root / baker.BINARY_OUTPUT
    require(binary_path.exists(), f"missing binary: {binary_path}", failures)
    if not binary_path.exists():
        return {}

    blob = binary_path.read_bytes()
    require(len(blob) % 16 == 0, "binary length must be 16-byte aligned", failures)
    header_values = struct.unpack(baker.BINARY_HEADER_FORMAT, blob[: baker.BINARY_HEADER_SIZE])
    labels = (
        "magic",
        "version",
        "headerBytes",
        "endianMarker",
        "tableHash",
        "multiplier",
        "increment",
        "modulusBits",
        "biomeCount",
        "resourceCount",
        "biomeOffset",
        "resourceOffset",
        "minimalOffset",
        "ultraOffset",
        "fileSize",
        "payloadCrc32",
        "fnvCollisionCount",
    )
    header = dict(zip(labels, header_values))

    require(header["magic"] == baker.BINARY_MAGIC, "binary magic mismatch", failures)
    require(header["version"] == baker.BINARY_VERSION, "binary version mismatch", failures)
    require(header["headerBytes"] == baker.BINARY_HEADER_SIZE, "binary header size mismatch", failures)
    require(header["endianMarker"] == 0x01020304, "binary endian marker mismatch", failures)
    require(header["tableHash"] == baker.TABLE_VERSION_HASH32, "binary table hash mismatch", failures)
    require(header["multiplier"] == baker.LCG_MULTIPLIER, "binary multiplier mismatch", failures)
    require(header["increment"] == baker.LCG_INCREMENT, "binary increment mismatch", failures)
    require(header["modulusBits"] == baker.LCG_MODULUS_BITS, "binary modulus bits mismatch", failures)
    require(header["biomeCount"] == 10, "binary biome count mismatch", failures)
    require(header["resourceCount"] == 150, "binary resource count mismatch", failures)
    require(header["fileSize"] == len(blob), "binary file size mismatch", failures)
    require(header["fnvCollisionCount"] == 0, "binary FNV collision count nonzero", failures)

    for key in ("biomeOffset", "resourceOffset", "minimalOffset", "ultraOffset", "fileSize"):
        require(header[key] % 16 == 0, f"{key} must be 16-byte aligned", failures)
    crc = zlib.crc32(blob[baker.BINARY_HEADER_SIZE:]) & baker.UINT32_MASK
    require(header["payloadCrc32"] == crc, "binary payload CRC mismatch", failures)

    binary_cache = payload.get("binary_cache", {})
    require(binary_cache.get("endian") == "<", "binary cache endian mismatch", failures)
    require(binary_cache.get("aligned_16_bytes") is True, "binary cache alignment flag false", failures)
    require(binary_cache.get("sha256") == sha256_bytes(blob), "binary cache SHA mismatch", failures)
    minimal_payload_bytes = header["biomeCount"] + header["biomeCount"] + (header["biomeCount"] * 2) + header["resourceCount"]
    minimal_lod_bytes = baker.align16_length(minimal_payload_bytes)
    require(header["ultraOffset"] - header["minimalOffset"] == minimal_lod_bytes, "minimal LOD size mismatch", failures)
    require(binary_cache.get("minimal_lod_bytes") == minimal_lod_bytes, "minimal LOD bytes cache mismatch", failures)
    require(binary_cache.get("minimal_lod_payload_bytes") == minimal_payload_bytes, "minimal payload bytes cache mismatch", failures)
    require(binary_cache.get("minimal_weight_matrix_bytes") == header["resourceCount"], "minimal weight bytes mismatch", failures)

    biomes = payload.get("biomes", [])
    for biome_index, biome in enumerate(biomes):
        offset = header["biomeOffset"] + biome_index * baker.BINARY_BIOME_SIZE
        record = struct.unpack(baker.BINARY_BIOME_FORMAT, blob[offset : offset + baker.BINARY_BIOME_SIZE])
        require(
            record
            == (
                biome["biome_hash32"],
                biome_index * 15,
                biome["total_weight_u16"],
                15,
                biome["base_density_u8"],
                biome["clumping_factor_u8"],
                0,
                biome["ultra_visual_seed_u32"],
            ),
            f"biome binary mismatch: {biome['biome_id']}",
            failures,
        )
        for resource_index, resource in enumerate(biome["resources"]):
            flat_index = biome_index * 15 + resource_index
            resource_offset = header["resourceOffset"] + flat_index * baker.BINARY_RESOURCE_SIZE
            raw = struct.unpack(baker.BINARY_RESOURCE_FORMAT, blob[resource_offset : resource_offset + baker.BINARY_RESOURCE_SIZE])
            require(
                raw == (resource["resource_hash32"], resource["cumulative_weight_u16"], resource["weight_u8"], resource["resource_tier"]),
                f"resource binary mismatch: {biome['biome_id']}/{resource['resource_id']}",
                failures,
            )

    offset = header["minimalOffset"]
    biome_count = len(biomes)
    density = list(blob[offset : offset + biome_count])
    clump_offset = offset + biome_count
    clump = list(blob[clump_offset : clump_offset + biome_count])
    total_offset = clump_offset + biome_count
    totals = [struct.unpack("<H", blob[total_offset + i * 2 : total_offset + i * 2 + 2])[0] for i in range(biome_count)]
    weight_offset = total_offset + biome_count * 2
    minimal_weights = list(blob[weight_offset : weight_offset + header["resourceCount"]])
    require(density == payload.get("density_map_u8"), "minimal density mismatch", failures)
    require(clump == payload.get("clumping_factors_u8"), "minimal clump mismatch", failures)
    require(totals == [biome["total_weight_u16"] for biome in biomes], "minimal totals mismatch", failures)
    require(minimal_weights == payload.get("weight_matrix_u8_flat"), "minimal weight matrix mismatch", failures)

    return {
        "path": str(baker.BINARY_OUTPUT).replace("\\", "/"),
        "bytes": len(blob),
        "payloadCrc32": crc,
        "sha256": sha256_bytes(blob),
        "endianness": "<",
        "offsets": {
            "biome": header["biomeOffset"],
            "resource": header["resourceOffset"],
            "minimal": header["minimalOffset"],
            "ultra": header["ultraOffset"],
        },
        "minimalLod": {
            "bytes": minimal_lod_bytes,
            "payloadBytes": minimal_payload_bytes,
            "weightMatrixBytes": len(minimal_weights),
        },
    }


def verify_histogram(root: Path, payload: dict[str, Any], failures: list[str]) -> dict[str, Any]:
    path = root / baker.HISTOGRAM_OUTPUT
    with path.open("r", encoding="utf-8", newline="") as handle:
        rows = list(csv.DictReader(handle))
    require(len(rows) == 150, "histogram must contain 150 rows", failures)
    safe = [row for row in rows if row["biome_id"] == baker.SAFE_SHALLOWS_ID and row["resource_id"] == baker.TITANIUM_ID]
    require(len(safe) == 1, "histogram missing Safe Shallows Titanium", failures)
    if safe:
        require(safe[0]["expected_basis_points"] == "5000", "Safe Shallows Titanium histogram bp mismatch", failures)
        require(
            int(safe[0]["simulated_count"]) == payload.get("simulation", {}).get("safe_shallows_titanium_simulated_count"),
            "histogram Safe Shallows count mismatch",
            failures,
        )
    return {"path": str(baker.HISTOGRAM_OUTPUT).replace("\\", "/"), "rows": len(rows)}


def verify_hashes_and_lore(payload: dict[str, Any], failures: list[str]) -> dict[str, Any]:
    seen: dict[int, str] = {payload["table_version_hash32"]: payload["schema_id"]}
    collisions: list[str] = []
    sterile_hits: list[str] = []
    industrial_hits = 0
    for biome in payload.get("biomes", []):
        if biome["biome_hash32"] in seen and seen[biome["biome_hash32"]] != biome["biome_id"]:
            collisions.append(biome["biome_id"])
        seen[biome["biome_hash32"]] = biome["biome_id"]
        alias = biome.get("industrial_alias", "")
        industrial_hits += int(bool(alias))
        if any(term in alias.lower() for term in STERILE_TERMS):
            sterile_hits.append(alias)
        for resource in biome.get("resources", []):
            if resource["resource_hash32"] in seen and seen[resource["resource_hash32"]] != resource["resource_id"]:
                collisions.append(resource["resource_id"])
            seen[resource["resource_hash32"]] = resource["resource_id"]
            alias = resource.get("industrial_alias", "")
            industrial_hits += int(bool(alias))
            if any(term in alias.lower() for term in STERILE_TERMS):
                sterile_hits.append(alias)
    require(not collisions, f"FNV collisions detected: {collisions[:8]}", failures)
    require(not sterile_hits, f"sterile lore terms detected: {sterile_hits[:8]}", failures)
    return {"fnvCollisionCount": len(collisions), "sterileTermHits": sterile_hits, "industrialAliasHits": industrial_hits}


def build_report(root: Path) -> dict[str, Any]:
    failures: list[str] = []
    payload = read_json(root / baker.JSON_OUTPUT)
    payload_report = verify_payload(root, payload, failures)
    binary = verify_binary(root, payload, failures)
    histogram = verify_histogram(root, payload, failures)
    hashes = verify_hashes_and_lore(payload, failures)
    atlas_path = root / "Docs/PROJECT_ATLAS.md"
    hphi_path = root / "Docs/AgentLogs/HPhi_RESOURCE_SPAWN_LCG_TABLES.json"
    require(atlas_path.exists(), "PROJECT_ATLAS.md missing", failures)
    require(hphi_path.exists(), "H-Phi artifact missing", failures)
    return {
        "status": "ORE_LCG_VERIFIED_STATIC_ONLY" if not failures else "ORE_LCG_FAILED",
        "evidenceClass": "STATIC_SOURCE/STATIC_DOC/PY_TOOL",
        "runtimeProof": "PENDING_VERIFICATION",
        "payload": payload_report,
        "binary": binary,
        "histogram": histogram,
        "hashes": hashes,
        "atlasPath": str(atlas_path.relative_to(root)).replace("\\", "/") if atlas_path.exists() else None,
        "hPhiPath": str(hphi_path.relative_to(root)).replace("\\", "/") if hphi_path.exists() else None,
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
    print(f"VERIFY_ORE_LCG_STATUS: {report['status']}")
    print(f"report={path}")
    print(f"binaryBytes={report.get('binary', {}).get('bytes', 0)}")
    print(f"hashCollisions={report.get('hashes', {}).get('fnvCollisionCount', -1)}")
    print("runtimeProof=PENDING_VERIFICATION")
    return 0 if not report["failures"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
