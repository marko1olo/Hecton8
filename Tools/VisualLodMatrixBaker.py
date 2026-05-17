#!/usr/bin/env python3
"""Bake and verify the HECTON-8 visual scalability matrix.

Evidence boundary:
    OFFLINE DATA ONLY. This tool proves JSON, binary layout, endian, alignment,
    hash coverage, physics-derived optics, and tier shape. It is not Unity
    import, Frame Debugger, RenderDoc, Profiler, GCMonitor, or player proof.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import struct
import sys
from pathlib import Path
from typing import Any, Dict, Iterable, List, Sequence, Tuple

try:
    from VisualStressSim import build_report
except ModuleNotFoundError:
    from Tools.VisualStressSim import build_report


ROOT = Path(__file__).resolve().parents[1]
SOURCE_JSON = ROOT / "Data/System/Visual_Scalability_Matrix.json"
BINARY_PATH = ROOT / "Data/System/Visual_Scalability_Matrix.bin"
MANIFEST_PATH = ROOT / "Data/System/Visual_Scalability_Matrix.manifest.json"

MAGIC = b"H8VG"
VERSION_MAJOR = 1
VERSION_MINOR = 0
ENDIAN_PROBE = 0x01020304
ALIGNMENT_BYTES = 16

HEADER_FORMAT = "<4sHHIIIIIIIIII16s"
TIER_RECORD_FORMAT = "<32I"
EXTRA_RECORD_FORMAT = "<16I"
HASH_RECORD_FORMAT = "<4I"

HEADER_BYTES = struct.calcsize(HEADER_FORMAT)
TIER_RECORD_BYTES = struct.calcsize(TIER_RECORD_FORMAT)
EXTRA_RECORD_BYTES = struct.calcsize(EXTRA_RECORD_FORMAT)
HASH_RECORD_BYTES = struct.calcsize(HASH_RECORD_FORMAT)

REQUIRED_TIERS = ("TOASTER", "DECK", "PRO", "GOD_MODE")
FNV_OFFSET = 0x811C9DC5
FNV_PRIME = 0x01000193

CATEGORY_IDS = {
    "tier": 1,
    "mandate": 2,
    "feature": 3,
    "fallback": 4,
    "material": 5,
    "physics": 6,
    "consumer": 7,
}


class MatrixError(RuntimeError):
    """Raised when the visual matrix fails a deterministic data contract."""


def load_json(path: Path) -> Dict[str, Any]:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def fnv1a32(label: str) -> int:
    value = FNV_OFFSET
    for byte in label.lower().encode("ascii"):
        value ^= byte
        value = (value * FNV_PRIME) & 0xFFFFFFFF
    return value


def require(condition: bool, message: str, failures: List[str]) -> None:
    if not condition:
        failures.append(message)


def require_ascii(label: str, failures: List[str]) -> None:
    try:
        label.lower().encode("ascii")
    except UnicodeEncodeError:
        failures.append(f"hash label is not ASCII: {label!r}")


def get_nested(data: Dict[str, Any], path: str) -> Any:
    current: Any = data
    for token in path.split("."):
        if not isinstance(current, dict):
            return None
        current = current.get(token)
    return current


def tiers_by_name(data: Dict[str, Any]) -> Dict[str, Dict[str, Any]]:
    tiers: Dict[str, Dict[str, Any]] = {}
    for tier in data.get("tiers", []):
        tiers[str(tier.get("tier", ""))] = tier
    return tiers


def scale(value: float, multiplier: float) -> int:
    result = int(round(float(value) * multiplier))
    if result < 0 or result > 0xFFFFFFFF:
        raise MatrixError(f"scaled value out of uint32 range: {value} * {multiplier}")
    return result


def bool_u32(value: Any) -> int:
    return 1 if bool(value) else 0


def estimated_vram_total(tier: Dict[str, Any]) -> int:
    return int(round(sum(float(value) for value in tier.get("estimatedVramMb", {}).values())))


def collect_hash_rows(data: Dict[str, Any]) -> Tuple[List[Dict[str, Any]], Dict[int, List[str]]]:
    rows: List[Tuple[str, str]] = []
    tiers = tiers_by_name(data)

    for tier_name in data.get("tierOrder", REQUIRED_TIERS):
        rows.append(("tier", str(tier_name)))

    for mandate in data.get("mandates", []):
        rows.append(("mandate", str(mandate)))

    for tier_name in REQUIRED_TIERS:
        tier = tiers[tier_name]
        shader = tier["shaderFeatures"]
        volume = tier["volumetricScattering"]
        textures = tier["textures"]
        rows.extend(
            (
                ("feature", str(tier["hardwareClass"])),
                ("feature", str(shader["mathLod"])),
                ("feature", str(shader["caustics"])),
                ("feature", str(volume["mode"])),
                ("feature", str(volume["noise"]["source"])),
                ("feature", str(textures["normalFormat"])),
                ("feature", str(textures["maskFormat"])),
            )
        )

    fallback_map = data.get("godModeFallbacks", {})
    for key in fallback_map.keys():
        rows.append(("fallback", str(key)))
    for value in _iter_strings(tiers["GOD_MODE"]):
        if value.startswith("godModeFallbacks."):
            rows.append(("fallback", value))

    for family in data.get("textureOverrideRules", {}).get("materialFamilies", []):
        rows.append(("material", str(family.get("family", ""))))
        rows.append(("material", str(family.get("detailNormal", ""))))
        rows.append(("material", str(family.get("microDetailOrm", ""))))

    physics = data.get("physicsDerivation", {})
    for value in _iter_strings(physics):
        rows.append(("physics", value))

    consumer = data.get("consumer")
    quality_lane = data.get("adapterContract", {}).get("qualityLane")
    if consumer:
        rows.append(("consumer", str(consumer)))
    if quality_lane:
        rows.append(("consumer", str(quality_lane)))

    seen: set[Tuple[str, str]] = set()
    unique_rows: List[Tuple[str, str]] = []
    for category, label in rows:
        if not label:
            continue
        key = (category, label)
        if key not in seen:
            seen.add(key)
            unique_rows.append(key)

    unique_rows.sort(key=lambda row: (CATEGORY_IDS[row[0]], row[1].lower()))

    manifest_rows: List[Dict[str, Any]] = []
    collisions: Dict[int, List[str]] = {}
    hash_to_labels: Dict[int, List[str]] = {}
    failures: List[str] = []
    for category, label in unique_rows:
        require_ascii(label, failures)
        hash32 = fnv1a32(label)
        hash_to_labels.setdefault(hash32, []).append(label)
        manifest_rows.append(
            {
                "category": category,
                "categoryId": CATEGORY_IDS[category],
                "hash32": hash32,
                "label": label,
            }
        )

    for hash32, labels in hash_to_labels.items():
        normalized = sorted({label.lower() for label in labels})
        if len(normalized) > 1:
            collisions[hash32] = labels

    if failures:
        raise MatrixError("; ".join(failures))

    return manifest_rows, collisions


def _iter_strings(value: Any) -> Iterable[str]:
    if isinstance(value, dict):
        for item in value.values():
            yield from _iter_strings(item)
    elif isinstance(value, list):
        for item in value:
            yield from _iter_strings(item)
    elif isinstance(value, str):
        yield value


def validate_physics(data: Dict[str, Any]) -> Dict[str, Any]:
    failures: List[str] = []
    optics = data.get("physicsDerivation", {}).get("waterOptics", {})
    sigma = [float(value) for value in optics.get("sigmaExtinctionPerMeterRgb", [])]
    require(optics.get("law") == "Beer-Lambert", "water optics law must be Beer-Lambert", failures)
    require(len(sigma) == 3, "sigmaExtinctionPerMeterRgb must contain 3 channels", failures)

    if len(sigma) == 3:
        ten_meter = [round(math.exp(-coeff * 10.0), 6) for coeff in sigma]
        ninety_loss = [round(-math.log(0.1) / coeff, 6) for coeff in sigma]
        require(
            ten_meter == [round(float(value), 6) for value in optics.get("tenMeterTransmittanceRgb", [])],
            "tenMeterTransmittanceRgb must derive from exp(-sigma*10)",
            failures,
        )
        require(
            ninety_loss == [round(float(value), 6) for value in optics.get("ninetyPercentLossMetersRgb", [])],
            "ninetyPercentLossMetersRgb must derive from -ln(0.1)/sigma",
            failures,
        )
    else:
        ten_meter = []
        ninety_loss = []

    volume = data.get("physicsDerivation", {}).get("volumetricDensity", {})
    require(volume.get("mieDensityFormula") == "mieDensity = turbidityNtu * 0.002", "unexpected Mie density formula", failures)
    require(
        volume.get("rayleighDensityFormula") == "rayleighDensity = mieDensity * rayleighToMieRatio",
        "unexpected Rayleigh density formula",
        failures,
    )

    tiers = tiers_by_name(data)
    tier_inputs = volume.get("tierInputs", {})
    for tier_name in REQUIRED_TIERS:
        tier = tiers.get(tier_name, {})
        authored_volume = tier.get("volumetricScattering", {})
        inputs = tier_inputs.get(tier_name, {})
        mie = round(float(inputs.get("turbidityNtu", -1.0)) * 0.002, 6)
        rayleigh = round(mie * float(inputs.get("rayleighToMieRatio", -1.0)), 6)
        require(round(float(authored_volume.get("mieDensity", -1.0)), 6) == mie, f"{tier_name} mieDensity not derived", failures)
        require(
            round(float(authored_volume.get("rayleighDensity", -1.0)), 6) == rayleigh,
            f"{tier_name} rayleighDensity not derived",
            failures,
        )
        require(
            [round(float(value), 6) for value in authored_volume.get("extinctionRgb", [])] == [round(value, 6) for value in sigma],
            f"{tier_name} extinctionRgb must match Beer-Lambert sigma",
            failures,
        )

    lod_math = data.get("physicsDerivation", {}).get("lodMath", {})
    require(float(lod_math.get("hysteresisSeconds", 0.0)) >= 3.0, "LOD hysteresisSeconds must be >= 3", failures)
    require(float(lod_math.get("hysteresisMeters", 0.0)) >= 5.0, "LOD hysteresisMeters must be >= 5", failures)

    if failures:
        raise MatrixError("; ".join(failures))

    return {
        "law": optics.get("law"),
        "status": "PASS",
        "sigmaExtinctionPerMeterRgb": [round(value, 6) for value in sigma],
        "tenMeterTransmittanceRgb": ten_meter,
        "ninetyPercentLossMetersRgb": ninety_loss,
    }


def validate_tier_contracts(data: Dict[str, Any], stress_report: Dict[str, Any]) -> None:
    failures: List[str] = []
    tiers = tiers_by_name(data)
    require(tuple(data.get("tierOrder", [])) == REQUIRED_TIERS, "tierOrder must be TOASTER, DECK, PRO, GOD_MODE", failures)

    for tier_name in REQUIRED_TIERS:
        tier = tiers.get(tier_name)
        if tier is None:
            failures.append(f"missing tier {tier_name}")
            continue
        total_vram = estimated_vram_total(tier)
        require(total_vram <= int(tier["vramGuardMb"]), f"{tier_name} estimated VRAM {total_vram} exceeds guard", failures)
        require(float(tier["lodPolicy"]["lodHysteresisMeters"]) >= 5.0, f"{tier_name} LOD hysteresis below 5m", failures)
        require(bool(tier["particles"]["shadowCasting"]) is False, f"{tier_name} particle shadows must be false", failures)
        require(tier["loadShedSequence"], f"{tier_name} loadShedSequence missing", failures)

    toaster = tiers.get("TOASTER", {})
    if toaster:
        shader = toaster["shaderFeatures"]
        require(shader["bloom"] is False, "TOASTER Bloom must be disabled", failures)
        require(shader["ssr"]["enabled"] is False, "TOASTER SSR must be disabled", failures)
        require(shader["pom"]["enabled"] is False, "TOASTER POM must be disabled", failures)
        require(shader["screenSpaceRefractions"]["enabled"] is False, "TOASTER refraction must be stripped", failures)
        require(toaster["textures"]["detailNormalMaps"] is False, "TOASTER detail normals must be stripped", failures)
        require(toaster["textures"]["microDetailOrm"] is False, "TOASTER micro ORM must be stripped", failures)
        require(estimated_vram_total(toaster) <= int(data["selfAuditExpectations"]["toasterMaxVramMbIncludingDriverReserve"]), "TOASTER exceeds self-audit VRAM", failures)

    god = tiers.get("GOD_MODE", {})
    if god:
        shader = god["shaderFeatures"]
        require(shader["pom"]["tapCount"] >= 16, "GOD_MODE POM taps below 16", failures)
        require(shader["ssr"]["enabled"] is True and shader["ssr"]["maxSteps"] >= 48, "GOD_MODE SSR overkill missing", failures)
        require(god["volumetricScattering"]["noise"]["layers"] >= 3, "GOD_MODE harmonic noise layers below 3", failures)
        require(god["volumetricScattering"]["noise"]["octavesPerLayer"] >= 12, "GOD_MODE harmonic noise octaves below 12", failures)
        require(god["textures"]["detailNormalMaps"] is True, "GOD_MODE detail normal override missing", failures)
        require(god["textures"]["microDetailOrm"] is True, "GOD_MODE micro ORM override missing", failures)
        require(god["particles"]["totalBudget"] >= 200000, "GOD_MODE particle budget below 200000", failures)

    ratio = stress_report["selfAudit"].get("godModeDensityRatioVsPro", 0.0)
    require(float(ratio) >= float(data["selfAuditExpectations"]["godModeMinimumDensityRatioVsPro"]), "GOD_MODE density ratio below expectation", failures)
    require(stress_report["selfAudit"].get("status") == "PASS", "VisualStressSim self-audit must pass", failures)

    if failures:
        raise MatrixError("; ".join(failures))


def pack_tier_record(tier: Dict[str, Any]) -> bytes:
    shader = tier["shaderFeatures"]
    dyn = tier["dynamicResolution"]
    lod = tier["lodPolicy"]
    refraction = shader["screenSpaceRefractions"]
    values = [
        fnv1a32(tier["tier"]),
        int(tier["targetFps"]),
        scale(tier["frameBudgetMs"], 1000.0),
        int(tier["vramLimitMb"]),
        int(tier["vramGuardMb"]),
        int(tier["systemRamMinimumMb"]),
        scale(dyn["defaultRenderScale"], 1000.0),
        scale(dyn["minimumRenderScale"], 1000.0),
        scale(dyn["maximumRenderScale"], 1000.0),
        scale(dyn["pressureStepScale"], 1000.0),
        scale(dyn["restoreStepScale"], 1000.0),
        scale(lod["lodBias"], 1000.0),
        scale(lod["lodHysteresisMeters"], 100.0),
        bool_u32(lod["crossFadeDither"]),
        scale(lod["hlod2Distance"], 100.0),
        scale(lod["largeCreatureCull"], 100.0),
        scale(lod["propCull"], 100.0),
        scale(lod["floraCull"], 100.0),
        int(shader["shaderLod"]),
        fnv1a32(shader["mathLod"]),
        bool_u32(shader["pom"]["enabled"]),
        int(shader["pom"]["tapCount"]),
        bool_u32(shader["ssr"]["enabled"]),
        scale(shader["ssr"]["resolutionFraction"], 1000.0),
        int(shader["ssr"]["maxSteps"]),
        bool_u32(refraction["enabled"]),
        int(refraction["sampleCount"]),
        scale(refraction["maxOffsetNdc"], 1_000_000.0),
        bool_u32(shader["ssdo"]["enabled"]),
        int(shader["ssdo"]["tapCount"]),
        bool_u32(shader["bloom"]),
        fnv1a32(shader["caustics"]),
    ]
    return struct.pack(TIER_RECORD_FORMAT, *values)


def pack_extra_record(tier: Dict[str, Any], stress_tier: Dict[str, float]) -> bytes:
    volume = tier["volumetricScattering"]
    noise = volume["noise"]
    textures = tier["textures"]
    values = [
        fnv1a32(tier["tier"]),
        scale(stress_tier["visualDensityScore"], 1000.0),
        scale(stress_tier["gpuCyclesP95"], 1000.0),
        scale(stress_tier["gpuCyclesPeak"], 1000.0),
        int(volume["lut"]["width"]),
        int(volume["lut"]["height"]),
        int(volume["raymarchSteps"]),
        int(noise["layers"]),
        int(noise["octavesPerLayer"]),
        int(tier["particles"]["totalBudget"]),
        bool_u32(textures["detailNormalMaps"]),
        bool_u32(textures["microDetailOrm"]),
        int(round(stress_tier["estimatedVramMb"])),
        int(textures["streamingBudgetMb"]),
        scale(volume["resolutionFraction"], 1000.0),
        fnv1a32(volume["mode"]),
    ]
    return struct.pack(EXTRA_RECORD_FORMAT, *values)


def pack_hash_record(row: Dict[str, Any], ordinal: int) -> bytes:
    return struct.pack(HASH_RECORD_FORMAT, int(row["hash32"]), int(row["categoryId"]), int(ordinal), 0)


def pad_to_alignment(payload: bytearray) -> None:
    remainder = len(payload) % ALIGNMENT_BYTES
    if remainder:
        payload.extend(b"\0" * (ALIGNMENT_BYTES - remainder))


def build_binary(data: Dict[str, Any], stress_report: Dict[str, Any], hash_rows: List[Dict[str, Any]]) -> bytes:
    source_sha16 = hashlib.sha256(
        json.dumps(data, sort_keys=True, separators=(",", ":")).encode("utf-8")
    ).digest()[:16]

    tier_offset = HEADER_BYTES
    tier_count = len(REQUIRED_TIERS)
    tier_bytes = tier_count * TIER_RECORD_BYTES
    extra_offset = tier_offset + tier_bytes
    extra_count = tier_count
    extra_bytes = extra_count * EXTRA_RECORD_BYTES
    hash_offset = extra_offset + extra_bytes
    hash_count = len(hash_rows)

    header = struct.pack(
        HEADER_FORMAT,
        MAGIC,
        VERSION_MAJOR,
        VERSION_MINOR,
        ENDIAN_PROBE,
        tier_count,
        tier_offset,
        TIER_RECORD_BYTES,
        extra_count,
        extra_offset,
        EXTRA_RECORD_BYTES,
        hash_count,
        hash_offset,
        HASH_RECORD_BYTES,
        source_sha16,
    )

    payload = bytearray(header)
    tiers = tiers_by_name(data)
    for tier_name in REQUIRED_TIERS:
        payload.extend(pack_tier_record(tiers[tier_name]))
    pad_to_alignment(payload)
    if len(payload) != extra_offset:
        raise MatrixError(f"extra offset mismatch: {len(payload)} != {extra_offset}")

    for tier_name in REQUIRED_TIERS:
        payload.extend(pack_extra_record(tiers[tier_name], stress_report["tiers"][tier_name]))
    pad_to_alignment(payload)
    if len(payload) != hash_offset:
        raise MatrixError(f"hash offset mismatch: {len(payload)} != {hash_offset}")

    for ordinal, row in enumerate(hash_rows):
        payload.extend(pack_hash_record(row, ordinal))
    pad_to_alignment(payload)
    return bytes(payload)


def build_manifest(data: Dict[str, Any], stress_report: Dict[str, Any], hash_rows: List[Dict[str, Any]], collisions: Dict[int, List[str]], binary: bytes) -> Dict[str, Any]:
    return {
        "schema": "H8.VisualScalabilityMatrix.Binary.v1",
        "status": "VISUAL_LOD_MATRIX_BIN_BAKED",
        "tool": "Tools/VisualLodMatrixBaker.py",
        "sourceJson": "Data/System/Visual_Scalability_Matrix.json",
        "binaryPath": "Data/System/Visual_Scalability_Matrix.bin",
        "alignmentBytes": ALIGNMENT_BYTES,
        "fileBytes": len(binary),
        "fileAligned16": len(binary) % ALIGNMENT_BYTES == 0,
        "endianness": "little",
        "endianProbe": "0x01020304",
        "sha256": hashlib.sha256(binary).hexdigest().upper(),
        "tierOrder": list(REQUIRED_TIERS),
        "layout": {
            "headerFormat": HEADER_FORMAT,
            "headerBytes": HEADER_BYTES,
            "tierRecordFormat": TIER_RECORD_FORMAT,
            "tierRecordBytes": TIER_RECORD_BYTES,
            "extraRecordFormat": EXTRA_RECORD_FORMAT,
            "extraRecordBytes": EXTRA_RECORD_BYTES,
            "hashRecordFormat": HASH_RECORD_FORMAT,
            "hashRecordBytes": HASH_RECORD_BYTES,
        },
        "sections": {
            "tierRecords": {
                "offset": HEADER_BYTES,
                "stride": TIER_RECORD_BYTES,
                "count": len(REQUIRED_TIERS),
                "bytes": len(REQUIRED_TIERS) * TIER_RECORD_BYTES,
            },
            "extraRecords": {
                "offset": HEADER_BYTES + len(REQUIRED_TIERS) * TIER_RECORD_BYTES,
                "stride": EXTRA_RECORD_BYTES,
                "count": len(REQUIRED_TIERS),
                "bytes": len(REQUIRED_TIERS) * EXTRA_RECORD_BYTES,
            },
            "hashRecords": {
                "offset": HEADER_BYTES + len(REQUIRED_TIERS) * TIER_RECORD_BYTES + len(REQUIRED_TIERS) * EXTRA_RECORD_BYTES,
                "stride": HASH_RECORD_BYTES,
                "count": len(hash_rows),
                "bytes": len(hash_rows) * HASH_RECORD_BYTES,
            },
        },
        "fnv1a32": {
            "algorithm": "FNV-1a 32-bit ASCII-lower",
            "rowCount": len(hash_rows),
            "collisionCount": len(collisions),
            "collisions": {str(key): labels for key, labels in sorted(collisions.items())},
        },
        "hashRows": hash_rows,
        "physicsAudit": validate_physics(data),
        "stressAudit": {
            "status": stress_report["selfAudit"]["status"],
            "godModeDensityRatioVsPro": stress_report["selfAudit"].get("godModeDensityRatioVsPro"),
            "tiers": stress_report["tiers"],
        },
        "scalabilityProfiles": {
            "toaster": {
                "record": "fixed 128-byte tier row plus fixed 64-byte extra row",
                "jsonHotPathIo": False,
                "maxTransientHeapBytes": 0,
            },
            "rtx_overkill": {
                "extraDataFields": [
                    "visualDensityScore",
                    "gpuCyclesP95",
                    "gpuCyclesPeak",
                    "lutWidth",
                    "lutHeight",
                    "raymarchSteps",
                    "harmonicNoiseLayers",
                    "harmonicNoiseOctaves",
                    "particleBudget",
                    "detailNormalFlag",
                    "microDetailOrmFlag",
                ],
            },
        },
    }


def build_artifacts(source_path: Path) -> Tuple[bytes, Dict[str, Any]]:
    data = load_json(source_path)
    stress_report = build_report(data, source_path, seed=8808, frame_count=720)
    validate_tier_contracts(data, stress_report)
    hash_rows, collisions = collect_hash_rows(data)
    if collisions:
        raise MatrixError(f"FNV-1a collisions detected: {collisions}")
    binary = build_binary(data, stress_report, hash_rows)
    manifest = build_manifest(data, stress_report, hash_rows, collisions, binary)
    return binary, manifest


def write_artifacts(binary: bytes, manifest: Dict[str, Any], binary_path: Path, manifest_path: Path) -> None:
    binary_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    binary_path.write_bytes(binary)
    with manifest_path.open("w", encoding="utf-8") as handle:
        json.dump(manifest, handle, indent=2, sort_keys=True)
        handle.write("\n")


def verify_existing(source_path: Path, binary_path: Path, manifest_path: Path) -> Dict[str, Any]:
    expected_binary, expected_manifest = build_artifacts(source_path)
    actual_binary = binary_path.read_bytes()
    actual_manifest = load_json(manifest_path)

    failures: List[str] = []
    require(actual_binary == expected_binary, "binary does not match deterministic bake", failures)
    require(actual_manifest == expected_manifest, "manifest does not match deterministic bake", failures)
    require(len(actual_binary) % ALIGNMENT_BYTES == 0, "binary file is not 16-byte aligned", failures)
    require(actual_manifest.get("endianness") == "little", "manifest endianness must be little", failures)
    require(actual_manifest.get("layout", {}).get("headerFormat") == HEADER_FORMAT, "header format drift", failures)
    require(actual_manifest.get("layout", {}).get("tierRecordFormat") == TIER_RECORD_FORMAT, "tier format drift", failures)
    require(actual_manifest.get("layout", {}).get("extraRecordFormat") == EXTRA_RECORD_FORMAT, "extra format drift", failures)
    require(actual_manifest.get("layout", {}).get("hashRecordFormat") == HASH_RECORD_FORMAT, "hash format drift", failures)
    require(actual_manifest.get("fnv1a32", {}).get("collisionCount") == 0, "hash collision count must be zero", failures)

    header = struct.unpack(HEADER_FORMAT, actual_binary[:HEADER_BYTES])
    require(header[0] == MAGIC, "bad magic", failures)
    require(header[3] == ENDIAN_PROBE, "bad endian probe", failures)
    require(header[4] == len(REQUIRED_TIERS), "tier count mismatch", failures)
    require(header[6] == TIER_RECORD_BYTES, "tier stride mismatch", failures)
    require(header[9] == EXTRA_RECORD_BYTES, "extra stride mismatch", failures)
    require(header[12] == HASH_RECORD_BYTES, "hash stride mismatch", failures)

    if failures:
        raise MatrixError("; ".join(failures))

    return expected_manifest


def print_ok(manifest: Dict[str, Any]) -> None:
    stress = manifest["stressAudit"]
    print("VISUAL_LOD_MATRIX_BINARY_OK")
    print("binary=Data/System/Visual_Scalability_Matrix.bin")
    print("manifest=Data/System/Visual_Scalability_Matrix.manifest.json")
    print(f"bytes={manifest['fileBytes']}")
    print(f"endianness={manifest['endianness']} aligned16={manifest['fileAligned16']}")
    print(f"hash_collisions={manifest['fnv1a32']['collisionCount']}")
    print(f"god_mode_density_ratio_vs_pro={stress['godModeDensityRatioVsPro']:.3f}")


def parse_args(argv: Sequence[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Bake or verify the visual scalability matrix binary.")
    parser.add_argument("--source", type=Path, default=SOURCE_JSON)
    parser.add_argument("--binary", type=Path, default=BINARY_PATH)
    parser.add_argument("--manifest", type=Path, default=MANIFEST_PATH)
    parser.add_argument("--verify", action="store_true", help="Verify current artifacts instead of writing them.")
    return parser.parse_args(argv)


def main(argv: Sequence[str]) -> int:
    args = parse_args(argv)
    try:
        if args.verify:
            manifest = verify_existing(args.source, args.binary, args.manifest)
        else:
            binary, manifest = build_artifacts(args.source)
            write_artifacts(binary, manifest, args.binary, args.manifest)
            verify_existing(args.source, args.binary, args.manifest)
        print_ok(manifest)
        return 0
    except (OSError, MatrixError, KeyError, TypeError, ValueError) as exc:
        print(f"VISUAL_LOD_MATRIX_BINARY_FAIL: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
