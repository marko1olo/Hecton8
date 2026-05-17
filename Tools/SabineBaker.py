#!/usr/bin/env python3
"""Bake the HECTON-8 raw Sabine RT60 and pressure damping LUT."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import struct
import sys
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, Sequence

import numpy as np


ROOT_DIR = Path(__file__).resolve().parents[1]
DEFAULT_OUTPUT_PATH = ROOT_DIR / "Data" / "Audio" / "Acoustic_LUT.bin"
DEFAULT_MANIFEST_PATH = ROOT_DIR / "Data" / "Audio" / "Acoustic_LUT.manifest.json"
DEFAULT_MATERIAL_PROFILE_PATH = ROOT_DIR / "Data" / "Audio" / "Acoustic_Material_Profiles.json"

PACK_FORMAT = "<ff"
RECORD_STRUCT = struct.Struct(PACK_FORMAT)
SIMD_GROUP_FORMAT = "<ffff"
SIMD_GROUP_STRUCT = struct.Struct(SIMD_GROUP_FORMAT)
VOLUME_COUNT = 256
ABSORPTION_COUNT = 256
RECORD_FLOAT_COUNT = 2
FLOAT32_BYTES = 4
RECORD_BYTES = RECORD_FLOAT_COUNT * FLOAT32_BYTES
SIMD_GROUP_BYTES = 16
EXPECTED_RECORDS = VOLUME_COUNT * ABSORPTION_COUNT
EXPECTED_FILE_BYTES = EXPECTED_RECORDS * RECORD_BYTES

SABINE_COEFFICIENT = 0.161
VOLUME_MIN_M3 = 10.0
VOLUME_MAX_M3 = 100_000.0
ABSORPTION_MIN = 0.01
ABSORPTION_MAX = 0.99
RT60_MAX_SECONDS = 10.0
EQUIVALENT_ABSORPTION_EPSILON = 0.0001

FNV_OFFSET_32 = 2166136261
FNV_PRIME_32 = 16777619
UINT_MASK_32 = 0xFFFFFFFF

STANDARD_GRAVITY_MPS2 = 9.80665
SEAWATER_DENSITY_KG_M3 = 1025.0
ATMOSPHERIC_PRESSURE_PA = 101_325.0
SEAWATER_BULK_MODULUS_PA = 2_200_000_000.0
PROJECT_MAX_DEPTH_M = 1_500.0
HF_REFERENCE_FREQUENCY_KHZ = 16.0
THORP_BORIC_ACID_COEFFICIENT = 0.11
THORP_MAGNESIUM_SULFATE_COEFFICIENT = 44.0
THORP_MAGNESIUM_SULFATE_DENOMINATOR = 4_100.0
THORP_PURE_WATER_COEFFICIENT = 0.000275
THORP_OFFSET_DB_PER_KM = 0.003
PRESSURE_MIN_BAR = ATMOSPHERIC_PRESSURE_PA / 100_000.0
PRESSURE_MAX_BAR = (ATMOSPHERIC_PRESSURE_PA + (SEAWATER_DENSITY_KG_M3 * STANDARD_GRAVITY_MPS2 * PROJECT_MAX_DEPTH_M)) / 100_000.0
MOCK_ROOM_WIDTH_M = 50.0
MOCK_ROOM_DEPTH_M = 50.0
MOCK_ROOM_HEIGHT_M = 5.0
MOCK_DEPTH_M = 500.0
MOCK_PRESSURE_BAR = (ATMOSPHERIC_PRESSURE_PA + (SEAWATER_DENSITY_KG_M3 * STANDARD_GRAVITY_MPS2 * MOCK_DEPTH_M)) / 100_000.0

STATUS_OK = "ACOUSTICS BAKED"


@dataclass(frozen=True)
class MaterialPreset:
    """Offline material absorption preset used by simulation and documentation."""

    name: str
    alpha: float
    profile_id: str
    source_file: str
    source_key: str
    provenance: str


@dataclass(frozen=True)
class MockRoomResult:
    """Sabine and damping result for the fixed metal-room smoke test."""

    width_m: float
    depth_m: float
    height_m: float
    volume_m3: float
    surface_m2: float
    alpha: float
    pressure_bar: float
    rt60_seconds: float
    damping: float


MATERIAL_PRESET_SOURCES: tuple[tuple[str, str, str], ...] = (
    ("Rock", "basalt_rock", "Basalt rock face: hard stone wall control class."),
    ("Metal", "steel_hull", "Riveted steel hull: hard industrial bulkhead control class."),
    ("Sand", "sand_silt", "Suspended sand and silt: soft sediment control class."),
    ("Coral", "coral_calcified", "Calcified coral: porous biological reef control class."),
)

QUALITY_TIERS = (
    {
        "id": "toaster_i3",
        "lookup": "nearest",
        "volumeStride": 2,
        "absorptionStride": 2,
        "maxControlHz": 8,
        "reflectionTaps": 0,
        "extraData": {
            "gradientBands": 0,
            "harmonicNoiseOctaves": 0,
            "convolutionTail": "disabled",
            "dirtyResonanceLayers": 0,
        },
        "notes": "Stripped lookup path for Celeron/i3. Use every other LUT row/column and Schroeder tail only.",
    },
    {
        "id": "middle",
        "lookup": "nearest_or_cold_bilerp",
        "volumeStride": 1,
        "absorptionStride": 1,
        "maxControlHz": 10,
        "reflectionTaps": 6,
        "extraData": {
            "gradientBands": 4,
            "harmonicNoiseOctaves": 1,
            "convolutionTail": "short_tail",
            "dirtyResonanceLayers": 1,
        },
        "notes": "Full LUT authority; bilerp only during control updates, never per sample.",
    },
    {
        "id": "high",
        "lookup": "cold_bilerp",
        "volumeStride": 1,
        "absorptionStride": 1,
        "maxControlHz": 20,
        "reflectionTaps": 12,
        "extraData": {
            "gradientBands": 8,
            "harmonicNoiseOctaves": 3,
            "convolutionTail": "medium_tail",
            "dirtyResonanceLayers": 2,
        },
        "notes": "Spend saved CPU on early metal-bulkhead slap and cave wall taps.",
    },
    {
        "id": "rtx_overkill",
        "lookup": "cold_bicubic_authoring_preview_only",
        "volumeStride": 1,
        "absorptionStride": 1,
        "maxControlHz": 30,
        "reflectionTaps": 24,
        "extraData": {
            "gradientBands": 16,
            "harmonicNoiseOctaves": 6,
            "convolutionTail": "full_offline_ir_bank",
            "dirtyResonanceLayers": 4,
        },
        "notes": "God-mode visuals/audio path: gradient-aware sends, convolution tail, and dirty industrial resonance layers.",
    },
)

CONSTANT_PROVENANCE = {
    "sabineCoefficient": {
        "source": "Sabine metric-room coefficient",
        "formula": "RT60 = 0.161 * V / (S * alpha)",
        "reason": "Prompt-mandated RT60 authority in seconds for m3/m2 units.",
    },
    "volumeAxis": {
        "source": "SABINE XML directive",
        "formula": "logspace(10m3, 100000m3, 256)",
        "reason": "Log spacing preserves low-room detail while keeping the raw blob at 512 KiB.",
    },
    "absorptionAxis": {
        "source": "SABINE XML directive",
        "formula": "linspace(0.01, 0.99, 256)",
        "reason": "Generic alpha axis stays material-agnostic and avoids runtime string lookups.",
    },
    "equivalentAbsorptionEpsilon": {
        "source": "finite division guard",
        "formula": "max(S * alpha, 0.0001)",
        "reason": "Below the valid axis minimum, so it cannot alter prompt-range cells.",
    },
    "hydrostaticPressure": {
        "source": "seawater static pressure",
        "formula": "P = P0 + rho * g * depth",
        "reason": "Pressure is derived from depth, not authored as a curve.",
    },
    "seawaterConstants": {
        "source": "offline acoustic reference constants",
        "formula": "rho=1025kg/m3, g=9.80665m/s2, P0=101325Pa, bulk=2.2e9Pa",
        "reason": "Kept in the manifest so SHINOBU sees the scalar lineage without parsing the binary.",
    },
    "thorpAbsorption": {
        "source": "Thorp seawater absorption equation",
        "formula": "0.11*f2/(1+f2)+44*f2/(4100+f2)+0.000275*f2+0.003",
        "reason": "High-frequency water loss is derived at 16kHz before Beer-Lambert retention.",
    },
    "mockRoom": {
        "source": "SABINE XML smoke test plus project pressure envelope",
        "formula": "50m * 50m * 5m at hydrostatic pressure for 500m depth",
        "reason": "The prompt gives the footprint; height and pressure are explicit test assumptions.",
    },
}

VERIFY_SAMPLES: tuple[tuple[int, int], ...] = (
    (0, 0),
    (0, ABSORPTION_COUNT - 1),
    (VOLUME_COUNT // 2, ABSORPTION_COUNT // 2),
    (VOLUME_COUNT - 1, 0),
    (VOLUME_COUNT - 1, ABSORPTION_COUNT - 1),
)


def clamp(value: float, minimum: float, maximum: float) -> float:
    """Clamp a scalar without external runtime dependencies."""

    return max(minimum, min(maximum, value))


def load_material_presets(profile_path: Path = DEFAULT_MATERIAL_PROFILE_PATH) -> tuple[MaterialPreset, ...]:
    """Load prompt material presets from the DATA/AUDIO material profile authority."""

    payload = json.loads(profile_path.read_text(encoding="utf-8"))
    profile_rows = payload.get("materialProfiles")
    if not isinstance(profile_rows, list):
        raise ValueError(f"material profile source missing materialProfiles: {profile_path}")
    profile_by_id: dict[str, dict[str, object]] = {}
    for row in profile_rows:
        if not isinstance(row, dict):
            continue
        profile_id = str(row.get("id", ""))
        if profile_id:
            profile_by_id[profile_id] = row

    source_file = profile_path.relative_to(ROOT_DIR).as_posix()
    presets: list[MaterialPreset] = []
    for name, profile_id, provenance in MATERIAL_PRESET_SOURCES:
        row = profile_by_id.get(profile_id)
        if row is None:
            raise ValueError(f"missing material profile for {name}: {profile_id}")
        coefficients = row.get("coefficients")
        if not isinstance(coefficients, dict):
            raise ValueError(f"material profile missing coefficients: {profile_id}")
        alpha = float(coefficients.get("absorption"))
        if not math.isfinite(alpha) or alpha < ABSORPTION_MIN or alpha > ABSORPTION_MAX:
            raise ValueError(f"material profile absorption outside LUT axis: {profile_id}={alpha}")
        presets.append(
            MaterialPreset(
                name=name,
                alpha=alpha,
                profile_id=profile_id,
                source_file=source_file,
                source_key=f"materialProfiles[{profile_id}].coefficients.absorption",
                provenance=provenance,
            )
        )
    return tuple(presets)


MATERIAL_PRESETS: tuple[MaterialPreset, ...] = load_material_presets()


def material_alpha(material_name: str) -> float:
    """Return a material absorption coefficient from the fixed preset table."""

    for preset in MATERIAL_PRESETS:
        if preset.name == material_name:
            return preset.alpha
    raise ValueError(f"unknown material preset: {material_name}")


def build_axes() -> tuple[np.ndarray, np.ndarray]:
    """Build volume and absorption axes for the raw row-major matrix."""

    volumes = np.logspace(
        math.log10(VOLUME_MIN_M3),
        math.log10(VOLUME_MAX_M3),
        VOLUME_COUNT,
        dtype=np.float64,
    ).astype("<f4")
    absorption = np.linspace(
        ABSORPTION_MIN,
        ABSORPTION_MAX,
        ABSORPTION_COUNT,
        dtype=np.float64,
    ).astype("<f4")
    return volumes, absorption


def fnv1a_ascii_lower(value: str) -> int:
    """Compute H8DataHash-style FNV-1a over lower-folded ASCII."""

    if not value:
        return 0
    hash_value = FNV_OFFSET_32
    for char in value:
        code = ord(char)
        if 65 <= code <= 90:
            code += 32
        hash_value ^= code & 0xFF
        hash_value = (hash_value * FNV_PRIME_32) & UINT_MASK_32
    return hash_value if hash_value != 0 else 1


def equal_volume_cube_surface_area(volume_m3: np.ndarray | float) -> np.ndarray | float:
    """Approximate boundary area when no authored room dimensions exist."""

    return 6.0 * np.power(volume_m3, 2.0 / 3.0)


def equal_volume_cube_path_length(volume_m3: np.ndarray | float) -> np.ndarray | float:
    """Return a single-bounce path proxy from the same volume authority as Sabine."""

    return np.cbrt(volume_m3)


def sabine_rt60_seconds(
    volume_m3: np.ndarray | float,
    surface_m2: np.ndarray | float,
    alpha: np.ndarray | float,
) -> np.ndarray | float:
    """Evaluate `RT60 = 0.161 * V / (S * alpha)` with finite guards."""

    equivalent_absorption = np.maximum(surface_m2 * alpha, EQUIVALENT_ABSORPTION_EPSILON)
    rt60 = SABINE_COEFFICIENT * volume_m3 / equivalent_absorption
    return np.minimum(rt60, RT60_MAX_SECONDS)


def depth_from_volume_axis() -> np.ndarray:
    """Map the volume row to the project depth envelope forced by the 2D prompt."""

    return np.linspace(0.0, PROJECT_MAX_DEPTH_M, VOLUME_COUNT, dtype=np.float64).astype("<f4")


def hydrostatic_pressure_pa(depth_m: np.ndarray | float) -> np.ndarray | float:
    """Return absolute seawater pressure from depth using rho*g*h."""

    depth_safe = np.maximum(depth_m, 0.0)
    return ATMOSPHERIC_PRESSURE_PA + (SEAWATER_DENSITY_KG_M3 * STANDARD_GRAVITY_MPS2 * depth_safe)


def thorp_absorption_db_per_km(frequency_khz: float) -> float:
    """Return seawater acoustic absorption using the Thorp empirical equation."""

    frequency_squared = frequency_khz * frequency_khz
    boric_acid = (THORP_BORIC_ACID_COEFFICIENT * frequency_squared) / (1.0 + frequency_squared)
    magnesium_sulfate = (THORP_MAGNESIUM_SULFATE_COEFFICIENT * frequency_squared) / (
        THORP_MAGNESIUM_SULFATE_DENOMINATOR + frequency_squared
    )
    pure_water = THORP_PURE_WATER_COEFFICIENT * frequency_squared
    return boric_acid + magnesium_sulfate + pure_water + THORP_OFFSET_DB_PER_KM


def pressure_bulk_modulus_factor(pressure_pa: np.ndarray | float) -> np.ndarray | float:
    """Return a small pressure correction from seawater compressibility."""

    pressure_delta = np.maximum(pressure_pa - ATMOSPHERIC_PRESSURE_PA, 0.0)
    return 1.0 + (pressure_delta / SEAWATER_BULK_MODULUS_PA)


def beer_lambert_retention(absorption_db_per_km: np.ndarray | float, path_m: np.ndarray | float) -> np.ndarray | float:
    """Return amplitude retention using Beer-Lambert in dB/km form."""

    path_km = np.maximum(path_m, 0.0) * 0.001
    attenuation_db = absorption_db_per_km * path_km
    return np.power(10.0, -attenuation_db / 20.0)


def high_frequency_damping_scalar(pressure_bar: float, absorption: float, path_m: float) -> float:
    """Return high-frequency retention from material absorption and seawater loss."""

    pressure_safe = max(pressure_bar * 100_000.0, ATMOSPHERIC_PRESSURE_PA)
    absorption_safe = clamp(absorption, ABSORPTION_MIN, ABSORPTION_MAX)
    material_amplitude_retention = math.sqrt(max(0.0, 1.0 - absorption_safe))
    absorption_db_per_km = thorp_absorption_db_per_km(HF_REFERENCE_FREQUENCY_KHZ)
    pressure_factor = float(pressure_bulk_modulus_factor(pressure_safe))
    water_retention = float(beer_lambert_retention(absorption_db_per_km * pressure_factor, max(path_m, 0.0)))
    return clamp(material_amplitude_retention * water_retention, 0.0, 1.0)


def build_pressure_damping_matrix(absorption_axis: np.ndarray) -> np.ndarray:
    """Build Beer-Lambert/Thorp high-frequency damping values as float32."""

    volumes, _ = build_axes()
    path_m = equal_volume_cube_path_length(volumes.astype(np.float64))[:, np.newaxis]
    pressure_pa = hydrostatic_pressure_pa(depth_from_volume_axis().astype(np.float64))[:, np.newaxis]
    pressure_factor = pressure_bulk_modulus_factor(pressure_pa)
    water_absorption = thorp_absorption_db_per_km(HF_REFERENCE_FREQUENCY_KHZ) * pressure_factor
    water_retention = beer_lambert_retention(water_absorption, path_m)
    material_retention = np.sqrt(np.maximum(0.0, 1.0 - absorption_axis[np.newaxis, :].astype(np.float64)))
    damping = material_retention * water_retention
    return np.asarray(np.clip(damping, 0.0, 1.0), dtype="<f4", order="C")


def build_lut_pairs() -> np.ndarray:
    """Build the row-major `float2(rt60Seconds, highFrequencyDamping)` matrix."""

    volumes, absorption = build_axes()
    surface = equal_volume_cube_surface_area(volumes[:, np.newaxis])
    rt60 = sabine_rt60_seconds(volumes[:, np.newaxis], surface, absorption[np.newaxis, :])
    damping = build_pressure_damping_matrix(absorption)
    pairs = np.empty((VOLUME_COUNT, ABSORPTION_COUNT, RECORD_FLOAT_COUNT), dtype="<f4", order="C")
    pairs[:, :, 0] = np.asarray(rt60, dtype="<f4")
    pairs[:, :, 1] = damping
    return pairs


def write_lut(output_path: Path) -> dict[str, float | int | str]:
    """Write `Acoustic_LUT.bin` and return deterministic metadata."""

    started = time.perf_counter()
    output_path.parent.mkdir(parents=True, exist_ok=True)
    pairs = build_lut_pairs()
    output_path.write_bytes(pairs.tobytes(order="C"))
    elapsed_us = (time.perf_counter() - started) * 1_000_000.0
    return {
        "records": EXPECTED_RECORDS,
        "bytes": output_path.stat().st_size,
        "format": PACK_FORMAT,
        "elapsedUs": elapsed_us,
    }


def build_manifest(output_path: Path, binary_sha256: str) -> dict[str, object]:
    """Build deterministic build-time metadata; runtime consumes the raw binary."""

    semantic_ids = [
        "h8.audio.acoustic_lut",
        "h8.audio.acoustic_lut.rt60_seconds",
        "h8.audio.acoustic_lut.high_frequency_damping",
        "h8.audio.acoustic_lut.toaster_i3",
        "h8.audio.acoustic_lut.middle",
        "h8.audio.acoustic_lut.high",
        "h8.audio.acoustic_lut.rtx_overkill",
    ]
    semantic_ids.extend(f"h8.audio.material.{preset.name.lower()}" for preset in MATERIAL_PRESETS)
    hash_rows = [
        {
            "id": semantic_id,
            "fnv1a32": fnv1a_ascii_lower(semantic_id),
            "hex": f"0x{fnv1a_ascii_lower(semantic_id):08X}",
        }
        for semantic_id in semantic_ids
    ]
    return {
        "status": STATUS_OK,
        "domain": "DATA/AUDIO",
        "binary": output_path.relative_to(ROOT_DIR).as_posix(),
        "sha256": binary_sha256,
        "endianness": "little",
        "recordFormat": PACK_FORMAT,
        "recordBytes": RECORD_BYTES,
        "simdGroupFormat": SIMD_GROUP_FORMAT,
        "simdGroupBytes": SIMD_GROUP_BYTES,
        "fileBytes": EXPECTED_FILE_BYTES,
        "fileAlignmentBytes": SIMD_GROUP_BYTES,
        "volumeCount": VOLUME_COUNT,
        "absorptionCount": ABSORPTION_COUNT,
        "volumeMinM3": VOLUME_MIN_M3,
        "volumeMaxM3": VOLUME_MAX_M3,
        "absorptionMin": ABSORPTION_MIN,
        "absorptionMax": ABSORPTION_MAX,
        "rt60MaxSeconds": RT60_MAX_SECONDS,
        "sabineCoefficient": SABINE_COEFFICIENT,
        "physics": {
            "surfaceProxy": "S = 6 * V^(2/3)",
            "waterAbsorption": "Thorp seawater absorption at 16kHz",
            "beerLambert": "amplitude = 10^(-(dB_per_km * km) / 20)",
            "hydrostaticPressure": "P = P0 + rho*g*h",
            "pressure": "P = P0 + rho*g*h",
            "pressureCorrection": "1 + (P - P0) / seawaterBulkModulus",
            "seawaterDensityKgM3": SEAWATER_DENSITY_KG_M3,
            "gravityMps2": STANDARD_GRAVITY_MPS2,
            "atmosphericPressurePa": ATMOSPHERIC_PRESSURE_PA,
            "seawaterBulkModulusPa": SEAWATER_BULK_MODULUS_PA,
            "projectMaxDepthM": PROJECT_MAX_DEPTH_M,
            "hfReferenceFrequencyKhz": HF_REFERENCE_FREQUENCY_KHZ,
            "thorpAbsorptionDbPerKm": thorp_absorption_db_per_km(HF_REFERENCE_FREQUENCY_KHZ),
            "thorpEquation": CONSTANT_PROVENANCE["thorpAbsorption"]["formula"],
            "thorpCoefficients": {
                "boricAcidCoefficient": THORP_BORIC_ACID_COEFFICIENT,
                "magnesiumSulfateCoefficient": THORP_MAGNESIUM_SULFATE_COEFFICIENT,
                "magnesiumSulfateDenominator": THORP_MAGNESIUM_SULFATE_DENOMINATOR,
                "pureWaterCoefficient": THORP_PURE_WATER_COEFFICIENT,
                "offsetDbPerKm": THORP_OFFSET_DB_PER_KM,
            },
        },
        "constantProvenance": CONSTANT_PROVENANCE,
        "materialProfileSource": DEFAULT_MATERIAL_PROFILE_PATH.relative_to(ROOT_DIR).as_posix(),
        "materialPresets": [
            {
                "name": preset.name,
                "alpha": preset.alpha,
                "profileId": preset.profile_id,
                "sourceFile": preset.source_file,
                "sourceKey": preset.source_key,
                "provenance": preset.provenance,
            }
            for preset in MATERIAL_PRESETS
        ],
        "mockRoomContract": {
            "widthM": MOCK_ROOM_WIDTH_M,
            "depthM": MOCK_ROOM_DEPTH_M,
            "heightM": MOCK_ROOM_HEIGHT_M,
            "pressureDepthM": MOCK_DEPTH_M,
            "pressureBar": MOCK_PRESSURE_BAR,
            "pressureFormula": "P = P0 + rho*g*pressureDepthM",
        },
        "qualityTiers": list(QUALITY_TIERS),
        "hashes": hash_rows,
        "runtimeContract": {
            "audioThreadLookup": "forbidden",
            "snapshotCadence": "block_or_control_update_only",
            "dataSovereignty": "stateless raw binary lookup; no private runtime coefficient solver",
            "atlasFamily": "Audio",
            "atlasAssemblies": [
                "Hecton8.Audio.Propagation",
                "Hecton8.Audio.Synthesis",
                "Hecton8.Audio.Echolocation",
                "Hecton8.Audio.Virtualization",
            ],
        },
    }


def write_manifest(output_path: Path, manifest_path: Path) -> None:
    """Write deterministic tooling metadata for audits and build ingestion."""

    binary_sha256 = hashlib.sha256(output_path.read_bytes()).hexdigest().upper()
    manifest = build_manifest(output_path, binary_sha256)
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8", newline="\n")


def read_lut_pairs(output_path: Path) -> np.ndarray:
    """Read the raw binary as `VOLUME_COUNT x ABSORPTION_COUNT x float2`."""

    file_bytes = output_path.read_bytes()
    if len(file_bytes) != EXPECTED_FILE_BYTES:
        raise ValueError(f"byte size mismatch: expected {EXPECTED_FILE_BYTES}, got {len(file_bytes)}")
    values = np.frombuffer(file_bytes, dtype="<f4")
    expected_float_count = EXPECTED_RECORDS * RECORD_FLOAT_COUNT
    if values.size != expected_float_count:
        raise ValueError(f"float count mismatch: expected {expected_float_count}, got {values.size}")
    return values.reshape((VOLUME_COUNT, ABSORPTION_COUNT, RECORD_FLOAT_COUNT))


def validate_struct_sample(file_bytes: bytes, pairs: np.ndarray, volume_index: int, absorption_index: int) -> str:
    """Validate one `<ff` record against the in-memory float pair."""

    record_index = (volume_index * ABSORPTION_COUNT) + absorption_index
    byte_offset = record_index * RECORD_BYTES
    rt60_raw, damping_raw = RECORD_STRUCT.unpack_from(file_bytes, byte_offset)
    rt60_matrix = float(pairs[volume_index, absorption_index, 0])
    damping_matrix = float(pairs[volume_index, absorption_index, 1])
    if rt60_raw != rt60_matrix or damping_raw != damping_matrix:
        raise ValueError(f"struct unpack mismatch at v={volume_index} a={absorption_index}")
    return f"sample[{volume_index},{absorption_index}] rt60={rt60_raw:.8f} damping={damping_raw:.8f}"


def validate_samples_recursive(
    file_bytes: bytes,
    pairs: np.ndarray,
    samples: Sequence[tuple[int, int]],
    index: int,
    report: list[str],
) -> None:
    """Recursively verify `<ff` packing for representative records."""

    if index >= len(samples):
        return
    volume_index, absorption_index = samples[index]
    report.append(validate_struct_sample(file_bytes, pairs, volume_index, absorption_index))
    validate_samples_recursive(file_bytes, pairs, samples, index + 1, report)


def validate_lut(output_path: Path) -> list[str]:
    """Validate size, finite values, clamps, damping range, formula samples, and `<ff` packing."""

    file_bytes = output_path.read_bytes()
    if RECORD_STRUCT.size != RECORD_BYTES:
        raise ValueError(f"{PACK_FORMAT} size mismatch: expected {RECORD_BYTES}, got {RECORD_STRUCT.size}")
    if SIMD_GROUP_STRUCT.size != SIMD_GROUP_BYTES:
        raise ValueError(f"{SIMD_GROUP_FORMAT} size mismatch: expected {SIMD_GROUP_BYTES}, got {SIMD_GROUP_STRUCT.size}")
    if len(file_bytes) % SIMD_GROUP_BYTES != 0:
        raise ValueError(f"file size is not {SIMD_GROUP_BYTES}-byte aligned")
    pairs = read_lut_pairs(output_path)
    rt60 = pairs[:, :, 0]
    damping = pairs[:, :, 1]
    if not np.isfinite(pairs).all():
        raise ValueError("Acoustic_LUT.bin contains non-finite values")
    if float(rt60.min()) <= 0.0 or float(rt60.max()) > RT60_MAX_SECONDS:
        raise ValueError("RT60 clamp range violated")
    if float(damping.min()) < 0.0 or float(damping.max()) > 1.0:
        raise ValueError("damping range violated")

    volumes, absorption = build_axes()
    report: list[str] = [
        f"byteSize={len(file_bytes)} expected={EXPECTED_FILE_BYTES}",
        f"records={EXPECTED_RECORDS} format={PACK_FORMAT}",
        f"simdGroupBytes={SIMD_GROUP_BYTES} fileAligned={len(file_bytes) % SIMD_GROUP_BYTES == 0}",
        f"rt60Range={float(rt60.min()):.8f}..{float(rt60.max()):.8f}",
        f"dampingRange={float(damping.min()):.8f}..{float(damping.max()):.8f}",
    ]
    for volume_index, absorption_index in VERIFY_SAMPLES:
        volume = float(volumes[volume_index])
        alpha = float(absorption[absorption_index])
        surface = float(equal_volume_cube_surface_area(volume))
        expected_rt60 = float(sabine_rt60_seconds(volume, surface, alpha))
        actual_rt60 = float(rt60[volume_index, absorption_index])
        if abs(expected_rt60 - actual_rt60) > 0.00001:
            raise ValueError(f"formula mismatch at v={volume_index} a={absorption_index}")
    validate_samples_recursive(file_bytes, pairs, VERIFY_SAMPLES, 0, report)
    return report


def simulate_metal_room(mock_height_m: float) -> MockRoomResult:
    """Run the required Python smoke test for a 50x50m metal room."""

    if mock_height_m <= 0.0 or not math.isfinite(mock_height_m):
        raise ValueError("mock room height must be finite and positive")
    width = MOCK_ROOM_WIDTH_M
    depth = MOCK_ROOM_DEPTH_M
    height = mock_height_m
    volume = width * depth * height
    surface = 2.0 * ((width * depth) + (width * height) + (depth * height))
    alpha = material_alpha("Metal")
    rt60 = float(sabine_rt60_seconds(volume, surface, alpha))
    damping = high_frequency_damping_scalar(MOCK_PRESSURE_BAR, alpha, float(equal_volume_cube_path_length(volume)))
    return MockRoomResult(
        width_m=width,
        depth_m=depth,
        height_m=height,
        volume_m3=volume,
        surface_m2=surface,
        alpha=alpha,
        pressure_bar=MOCK_PRESSURE_BAR,
        rt60_seconds=rt60,
        damping=damping,
    )


def print_material_presets() -> None:
    """Print fixed material absorption presets."""

    for preset in MATERIAL_PRESETS:
        print(f"MATERIAL: {preset.name} alpha={preset.alpha:.6f} source={preset.profile_id}")


def parse_args(argv: Iterable[str]) -> argparse.Namespace:
    """Parse CLI arguments."""

    parser = argparse.ArgumentParser(description="Bake the raw HECTON-8 Sabine RT60+damping acoustic LUT.")
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT_PATH)
    parser.add_argument("--manifest", type=Path, default=DEFAULT_MANIFEST_PATH)
    parser.add_argument("--verify-only", action="store_true")
    parser.add_argument("--mock-height", type=float, default=MOCK_ROOM_HEIGHT_M)
    return parser.parse_args(list(argv))


def main(argv: Iterable[str]) -> int:
    """CLI entry point."""

    args = parse_args(argv)
    if not args.verify_only:
        bake = write_lut(args.output)
        write_manifest(args.output, args.manifest)
        print(
            "BAKE: "
            f"output={args.output} records={bake['records']} bytes={bake['bytes']} "
            f"format={bake['format']} elapsedUs={bake['elapsedUs']:.2f}"
        )
        print(f"MANIFEST: output={args.manifest}")
    print_material_presets()
    mock = simulate_metal_room(args.mock_height)
    print(
        "MOCK: "
        f"metalRoom={mock.width_m:.1f}x{mock.depth_m:.1f}x{mock.height_m:.1f}m "
        f"volume={mock.volume_m3:.3f}m3 surface={mock.surface_m2:.3f}m2 "
        f"alpha={mock.alpha:.6f} pressureBar={mock.pressure_bar:.3f} "
        f"rt60={mock.rt60_seconds:.8f}s damping={mock.damping:.8f}"
    )
    for line in validate_lut(args.output):
        print(f"VERIFY: {line}")
    print(f"STATUS: {STATUS_OK}")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main(sys.argv[1:]))
    except Exception as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        sys.exit(1)
