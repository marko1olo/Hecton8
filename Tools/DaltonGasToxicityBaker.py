#!/usr/bin/env python3
"""Verify the HECTON-8 Dalton gas toxicity binary contract.

The manifest is the authority for constants. This tool deliberately validates
the existing baked data instead of re-authoring hard-science constants from
undocumented literals.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import struct
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
MANIFEST_PATH = ROOT / "Data" / "Precomputed" / "dalton_gas_toxicity_manifest.json"
ALIGNMENT_BYTES = 16
FNV_OFFSET_32 = 2166136261
FNV_PRIME_32 = 16777619
FLOAT_TOLERANCE = 0.00025
CURVE_TOLERANCE = 0.0015


class DaltonValidationError(RuntimeError):
    """Raised when the baked Dalton data violates its manifest contract."""


def rel(path: Path) -> str:
    return path.resolve().relative_to(ROOT).as_posix()


def require(condition: bool, message: str) -> None:
    if not condition:
        raise DaltonValidationError(message)


def fnv1a32_ascii_lower(text: str) -> int:
    value = FNV_OFFSET_32
    for raw in text.encode("ascii"):
        byte = raw + 32 if 65 <= raw <= 90 else raw
        value ^= byte
        value = (value * FNV_PRIME_32) & 0xFFFFFFFF
    return value


def read_manifest(path: Path) -> dict[str, Any]:
    require(path.is_file(), f"missing manifest: {path}")
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def aligned_size(path: Path) -> int:
    require(path.is_file(), f"missing binary: {path}")
    size = path.stat().st_size
    require(size % ALIGNMENT_BYTES == 0, f"{rel(path)} is not {ALIGNMENT_BYTES}-byte aligned: {size}")
    return size


def smoothstep01(value: float) -> float:
    x = min(1.0, max(0.0, value))
    return x * x * (3.0 - (2.0 * x))


def unpack_row(data: bytes, offset: int, row_struct: struct.Struct) -> tuple[float, ...]:
    return row_struct.unpack_from(data, offset)


def approx_equal(actual: float, expected: float, tolerance: float, label: str) -> None:
    delta = abs(actual - expected)
    require(delta <= tolerance, f"{label}: actual={actual:.9f} expected={expected:.9f} delta={delta:.9f}")


def verify_hash_rows(manifest: dict[str, Any]) -> int:
    seen: dict[int, str] = {}
    rows = manifest.get("hashes")
    require(isinstance(rows, list) and rows, "manifest hashes are missing")
    for row in rows:
        label = str(row.get("id", ""))
        require(label, "hash row missing id")
        expected = fnv1a32_ascii_lower(label)
        actual = int(row.get("fnv1a32", -1))
        require(actual == expected, f"FNV mismatch for {label}: 0x{actual:08X} != 0x{expected:08X}")
        previous = seen.get(actual)
        require(previous is None, f"FNV collision: {previous} vs {label} = 0x{actual:08X}")
        seen[actual] = label
    return len(rows)


def verify_formats(manifest: dict[str, Any]) -> None:
    require(manifest.get("endianness") == "little", "manifest endianness must be little")
    for key in ("headerFormat", "rowFormat", "floatFormat"):
        value = str(manifest.get(key, ""))
        require(value.startswith("<"), f"{key} must use explicit little-endian format")
    require(struct.calcsize(str(manifest["headerFormat"])) == int(manifest["headerBytes"]), "header format/bytes mismatch")
    require(struct.calcsize(str(manifest["rowFormat"])) == int(manifest["rowBytes"]), "row format/bytes mismatch")
    for tier_name, tier in dict(manifest.get("tierBinaries", {})).items():
        row_format = str(tier.get("rowFormat", ""))
        require(row_format.startswith("<"), f"{tier_name} rowFormat must be little-endian")
        require(struct.calcsize(row_format) == int(tier.get("rowBytes", -1)), f"{tier_name} row format/bytes mismatch")


def verify_binary_metadata(manifest: dict[str, Any]) -> dict[str, Any]:
    main_path = ROOT / str(manifest["binary"])
    main_size = aligned_size(main_path)
    expected_main_size = int(manifest["headerBytes"]) + (int(manifest["depthCount"]) * int(manifest["rowBytes"]))
    require(main_size == int(manifest["fileBytes"]), "main file size does not match manifest fileBytes")
    require(main_size == expected_main_size, "main file size does not match header + rows")
    require(sha256_file(main_path) == str(manifest["sha256"]).upper(), "main sha256 mismatch")

    data = main_path.read_bytes()
    header_struct = struct.Struct(str(manifest["headerFormat"]))
    header = header_struct.unpack_from(data, 0)
    magic = header[0].decode("ascii")
    require(magic == str(manifest["binaryHeader"])[:4], f"main magic mismatch: {magic}")
    require(int(header[1]) == 2, "main binary version must be 2")
    require(int(header[2]) == int(manifest["headerBytes"]), "main header byte count mismatch")
    require(int(header[3]) == int(manifest["rowBytes"]), "main row byte count mismatch")
    require(int(header[4]) == int(manifest["depthCount"]), "main row count mismatch")
    require(int(header[5]) == int(manifest["columnCount"]), "main column count mismatch")

    tier_summary: dict[str, Any] = {}
    for tier_name, tier in dict(manifest.get("tierBinaries", {})).items():
        tier_path = ROOT / str(tier["binary"])
        tier_size = aligned_size(tier_path)
        expected_tier_size = int(tier["headerBytes"]) + (int(tier["rowCount"]) * int(tier["rowBytes"]))
        require(tier_size == int(tier["fileBytes"]), f"{tier_name} fileBytes mismatch")
        require(tier_size == expected_tier_size, f"{tier_name} header + rows mismatch")
        require(sha256_file(tier_path) == str(tier["sha256"]).upper(), f"{tier_name} sha256 mismatch")
        tier_data = tier_path.read_bytes()
        tier_magic = struct.unpack_from("<4s", tier_data, 0)[0].decode("ascii")
        require(tier_magic == str(tier["magic"]), f"{tier_name} magic mismatch: {tier_magic}")
        tier_summary[tier_name] = {
            "binary": rel(tier_path),
            "bytes": tier_size,
            "sha256": str(tier["sha256"]).upper(),
            "rowCount": int(tier["rowCount"]),
            "rowBytes": int(tier["rowBytes"]),
        }

    return {
        "mainBinary": rel(main_path),
        "mainBytes": main_size,
        "mainMagic": magic,
        "tierBinaries": tier_summary,
    }


def verify_physics_rows(manifest: dict[str, Any]) -> list[dict[str, Any]]:
    constants = manifest["constants"]
    thresholds = constants["thresholds"]
    fractions = constants["dryAirNormalizedFractions"]
    binary_path = ROOT / str(manifest["binary"])
    data = binary_path.read_bytes()
    row_struct = struct.Struct(str(manifest["rowFormat"]))
    header_bytes = int(manifest["headerBytes"])
    row_bytes = int(manifest["rowBytes"])
    depth_count = int(manifest["depthCount"])
    sample_indices = (0, min(10, depth_count - 1), min(100, depth_count - 1), depth_count // 2, depth_count - 1)
    rows: list[dict[str, Any]] = []

    pressure_atm_pa = float(constants["atmosphericPressurePa"])
    rho = float(constants["seawaterDensityKgM3"])
    gravity = float(constants["standardGravityMps2"])
    kpa_per_atm = float(constants["kPaPerAtm"])
    ppm_to_fraction = float(thresholds["ppmToFraction"])

    for index in sample_indices:
        row = unpack_row(data, header_bytes + (index * row_bytes), row_struct)
        depth_m = float(row[0])
        pressure_pa = pressure_atm_pa + (rho * gravity * depth_m)
        pressure_kpa = pressure_pa / 1000.0
        pressure_atm = pressure_pa / pressure_atm_pa
        oxygen_fraction = float(fractions["oxygen"])
        nitrogen_fraction = float(fractions["nitrogenEquivalent"])
        co2_fraction = float(fractions["carbonDioxide"])

        expected_o2_atm = oxygen_fraction * pressure_atm
        expected_o2_kpa = oxygen_fraction * pressure_kpa
        expected_n2_atm = nitrogen_fraction * pressure_atm
        expected_n2_kpa = nitrogen_fraction * pressure_kpa
        expected_co2_kpa = co2_fraction * pressure_kpa

        hypoxia = smoothstep01((float(thresholds["hypoxiaSoftKPa"]) - expected_o2_kpa) / (float(thresholds["hypoxiaSoftKPa"]) - float(thresholds["hypoxiaFatalKPa"])))
        oxygen_cns = smoothstep01((expected_o2_atm - float(thresholds["oxygenCnsSoftAtm"])) / (float(thresholds["oxygenCnsFullAtm"]) - float(thresholds["oxygenCnsSoftAtm"])))
        nitrogen = smoothstep01((expected_n2_atm - float(thresholds["nitrogenNarcosisSoftAtm"])) / (float(thresholds["nitrogenNarcosisFullAtm"]) - float(thresholds["nitrogenNarcosisSoftAtm"])))
        co2 = smoothstep01((expected_co2_kpa - float(thresholds["co2RelTwaKPa"])) / (float(thresholds["co2IdlhKPa"]) - float(thresholds["co2RelTwaKPa"])))
        composite = max(hypoxia, oxygen_cns, nitrogen, co2)

        approx_equal(float(row[1]), pressure_atm, FLOAT_TOLERANCE, f"row {index} ambientPressureAtm")
        approx_equal(float(row[2]), pressure_kpa, FLOAT_TOLERANCE * kpa_per_atm, f"row {index} ambientPressureKPa")
        approx_equal(float(row[3]), oxygen_fraction, FLOAT_TOLERANCE, f"row {index} oxygenFraction")
        approx_equal(float(row[4]), expected_o2_atm, FLOAT_TOLERANCE, f"row {index} oxygenPartialPressureAtm")
        approx_equal(float(row[5]), expected_o2_kpa, FLOAT_TOLERANCE * kpa_per_atm, f"row {index} oxygenPartialPressureKPa")
        approx_equal(float(row[6]), nitrogen_fraction, FLOAT_TOLERANCE, f"row {index} nitrogenEquivalentFraction")
        approx_equal(float(row[7]), expected_n2_atm, FLOAT_TOLERANCE, f"row {index} nitrogenEquivalentPartialPressureAtm")
        approx_equal(float(row[8]), expected_n2_kpa, FLOAT_TOLERANCE * kpa_per_atm, f"row {index} nitrogenEquivalentPartialPressureKPa")
        approx_equal(float(row[9]), co2_fraction, FLOAT_TOLERANCE, f"row {index} carbonDioxideFraction")
        approx_equal(float(row[10]), expected_co2_kpa, FLOAT_TOLERANCE * kpa_per_atm, f"row {index} carbonDioxidePartialPressureKPa")
        approx_equal(float(row[11]), hypoxia, CURVE_TOLERANCE, f"row {index} hypoxia01")
        approx_equal(float(row[12]), oxygen_cns, CURVE_TOLERANCE, f"row {index} oxygenCnsToxicity01")
        approx_equal(float(row[13]), nitrogen, CURVE_TOLERANCE, f"row {index} nitrogenNarcosis01")
        approx_equal(float(row[14]), co2, CURVE_TOLERANCE, f"row {index} carbonDioxideToxicity01")
        approx_equal(float(row[15]), composite, CURVE_TOLERANCE, f"row {index} compositeDanger01")

        rows.append(
            {
                "index": index,
                "depthMeters": depth_m,
                "ambientPressureAtm": float(row[1]),
                "oxygenCnsToxicity01": float(row[12]),
                "nitrogenNarcosis01": float(row[13]),
                "carbonDioxideToxicity01": float(row[14]),
                "compositeDanger01": float(row[15]),
            }
        )

    rel_twa_kpa = float(thresholds["co2RelTwaPpm"]) * ppm_to_fraction * kpa_per_atm
    stel_kpa = float(thresholds["co2StelPpm"]) * ppm_to_fraction * kpa_per_atm
    idlh_kpa = float(thresholds["co2IdlhPpm"]) * ppm_to_fraction * kpa_per_atm
    approx_equal(float(thresholds["co2RelTwaKPa"]), rel_twa_kpa, FLOAT_TOLERANCE, "co2RelTwaKPa")
    approx_equal(float(thresholds["co2StelKPa"]), stel_kpa, FLOAT_TOLERANCE, "co2StelKPa")
    approx_equal(float(thresholds["co2IdlhKPa"]), idlh_kpa, FLOAT_TOLERANCE, "co2IdlhKPa")

    physics = json.dumps(manifest.get("physics", {}), sort_keys=True)
    for token in ("daltonLaw", "hydrostaticPressure", "co2KPaDerivation", "compositeDanger"):
        require(token in physics, f"physics manifest token missing: {token}")

    return rows


def verify_manifest_contract(manifest: dict[str, Any]) -> None:
    require(manifest.get("schema") == "H8.DaltonGasToxicity.v2", "schema mismatch")
    require(manifest.get("status") == "DALTON_GAS_TOXICITY_BAKED", "status mismatch")
    require(manifest.get("rowAligned16") is True, "rowAligned16 must be true")
    require(manifest.get("fileAligned16") is True, "fileAligned16 must be true")
    require(int(manifest.get("fileAlignmentBytes", 0)) == ALIGNMENT_BYTES, "fileAlignmentBytes mismatch")
    require(manifest.get("runtimeContract", {}).get("dataSovereignty") == "stateless aligned binary lookup; no per-system private solver state", "data sovereignty contract mismatch")
    tiers = {str(row.get("id")) for row in manifest.get("qualityTiers", [])}
    require("toaster_i3" in tiers, "toaster_i3 quality tier missing")
    require("rtx_overkill" in tiers, "rtx_overkill quality tier missing")
    overkill = manifest.get("tierBinaries", {}).get("rtx_overkill", {})
    extra_columns = set(overkill.get("columns", []))
    for column in ("highResolutionDangerGradientPerMeter", "complexHarmonicNoiseSeed01", "dirtyRegulatorDistortionGain"):
        require(column in extra_columns, f"rtx_overkill extra column missing: {column}")


def verify(manifest_path: Path = MANIFEST_PATH) -> dict[str, Any]:
    manifest = read_manifest(manifest_path)
    verify_manifest_contract(manifest)
    verify_formats(manifest)
    hash_count = verify_hash_rows(manifest)
    binary_summary = verify_binary_metadata(manifest)
    sample_rows = verify_physics_rows(manifest)
    return {
        "status": "DALTON_GAS_TOXICITY_VERIFY_PASS",
        "evidenceClass": "CLI_STATIC_DATA",
        "manifest": rel(manifest_path),
        "binary": binary_summary["mainBinary"],
        "bytes": binary_summary["mainBytes"],
        "aligned16": True,
        "endianness": "<",
        "headerFormat": manifest["headerFormat"],
        "rowFormat": manifest["rowFormat"],
        "fnvRecords": hash_count,
        "fnvCollisions": 0,
        "sampleRows": sample_rows,
        "tierBinaries": binary_summary["tierBinaries"],
        "sourceReferences": [row.get("id") for row in manifest.get("sourceReferences", [])],
        "dataSovereignty": manifest["runtimeContract"]["dataSovereignty"],
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verify", action="store_true", help="Validate manifest and baked binaries.")
    parser.add_argument("--check", action="store_true", help="Alias for --verify.")
    parser.add_argument("--manifest", type=Path, default=MANIFEST_PATH, help="Manifest path to verify.")
    parser.add_argument("--write-report", type=Path, default=None, help="Optional JSON report path.")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        result = verify(args.manifest.resolve())
    except DaltonValidationError as exc:
        print(f"DALTON_GAS_TOXICITY_VERIFY_FAIL: {exc}")
        return 1

    output = json.dumps(result, indent=2, sort_keys=True)
    print(output)
    if args.write_report is not None:
        report_path = args.write_report.resolve()
        report_path.parent.mkdir(parents=True, exist_ok=True)
        report_path.write_text(output + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
