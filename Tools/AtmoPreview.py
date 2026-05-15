#!/usr/bin/env python3
"""Bake HECTON-8 atmosphere LUTs and a deterministic sky preview image.

The generated binary payloads are raw little-endian IEEE 754 half-float values.
They contain no header. Runtime readers must use the manifest dimensions and
copy the payload into pre-sized cold-load buffers only.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import struct
import zlib
from pathlib import Path
from typing import Any, Dict, Iterable, List, Sequence, Tuple


ROOT_DIR = Path(__file__).resolve().parents[1]
DEFAULT_OUTPUT_DIR = ROOT_DIR / "Data" / "Precomputed" / "Atmosphere"
DEFAULT_REPORT_PATH = (
    ROOT_DIR / "Docs" / "AgentLogs" / "AtmoValidation_ORBITAL_ATMOSPHERE_MATHEMATICIAN.json"
)

SKY_LUT_WIDTH = 256
SKY_LUT_HEIGHT = 128
DENSITY_LAYERS = 128
RGBA_CHANNEL_COUNT = 4

ATMOSPHERE_HEIGHT_M = 100_000.0
RAYLEIGH_SCALE_HEIGHT_M = 8_000.0
MIE_SCALE_HEIGHT_M = 1_200.0
PLANET_RADIUS_M = 6_371_000.0

SUN_ELEVATION_MIN_DEG = -8.0
SUN_ELEVATION_MAX_DEG = 82.0
MIE_ANISOTROPY_G = 0.76

HALF_STRUCT_FORMAT = "<e"
HALF_FLOAT_BYTES = struct.calcsize(HALF_STRUCT_FORMAT)

SKY_LUT_FILE = "atmosphere_sky_gradient_rgba16f.bin"
DENSITY_FILE = "atmosphere_density_matrix_rgba16f.bin"
PREVIEW_FILE = "atmosphere_sky_gradient_preview.png"
MANIFEST_FILE = "atmosphere_lut_manifest.json"

GRADIENT_MAX_ADJACENT_DELTA = 0.115
SURFACE_SEAM_MAX_DELTA = 0.030
VOID_BLACK_MAX_LUMINANCE = 0.040
MIN_GOLDEN_HOUR_LUMINANCE = 0.065


def clamp(value: float, minimum: float, maximum: float) -> float:
    """Clamp a float and replace non-finite input with the lower bound."""

    if not math.isfinite(value):
        return minimum
    return min(max(value, minimum), maximum)


def saturate(value: float) -> float:
    """Clamp a value to the [0, 1] interval."""

    return clamp(value, 0.0, 1.0)


def smoothstep(edge0: float, edge1: float, value: float) -> float:
    """Return a cubic Hermite interpolation from 0 to 1."""

    if edge0 == edge1:
        return 1.0 if value >= edge1 else 0.0
    t = saturate((value - edge0) / (edge1 - edge0))
    return t * t * (3.0 - 2.0 * t)


def lerp(a: float, b: float, t: float) -> float:
    """Linearly interpolate between two finite floats."""

    return a + (b - a) * saturate(t)


def luminance(rgb: Sequence[float]) -> float:
    """Return Rec.709 luminance for a linear RGB triplet."""

    return rgb[0] * 0.2126 + rgb[1] * 0.7152 + rgb[2] * 0.0722


def safe_exp(value: float) -> float:
    """Exponentiate with bounds that avoid overflow and underflow noise."""

    return math.exp(clamp(value, -60.0, 40.0))


def finite_or_zero(value: float) -> float:
    """Return a finite value or zero for invalid math."""

    return value if math.isfinite(value) else 0.0


def rayleigh_beta_for_wavelength_nm(wavelength_nm: float) -> float:
    """Calculate Rayleigh scattering coefficient for a wavelength in nanometers."""

    wavelength_m = wavelength_nm * 1.0e-9
    refractive_index_air = 1.0003
    molecular_density = 2.545e25
    depolarization_factor = 0.035
    refractive_delta = refractive_index_air * refractive_index_air - 1.0
    king_correction = (6.0 + 3.0 * depolarization_factor) / (
        6.0 - 7.0 * depolarization_factor
    )
    numerator = 8.0 * math.pi**3 * refractive_delta * refractive_delta * king_correction
    denominator = 3.0 * molecular_density * wavelength_m**4
    return numerator / denominator


def mie_beta_for_wavelength_nm(
    wavelength_nm: float,
    turbidity: float = 1.65,
    angstrom_alpha: float = 0.82,
) -> float:
    """Calculate a deterministic Mie aerosol coefficient for a wavelength."""

    reference_beta = 3.996e-6
    wavelength_ratio = max(wavelength_nm, 1.0) / 550.0
    return reference_beta * turbidity * wavelength_ratio ** (-angstrom_alpha)


def rayleigh_phase(cos_theta: float) -> float:
    """Return the Rayleigh phase function."""

    mu = clamp(cos_theta, -1.0, 1.0)
    return (3.0 / (16.0 * math.pi)) * (1.0 + mu * mu)


def mie_phase(cos_theta: float, anisotropy_g: float = MIE_ANISOTROPY_G) -> float:
    """Return the Henyey-Greenstein style Mie phase approximation."""

    mu = clamp(cos_theta, -1.0, 1.0)
    g = clamp(anisotropy_g, -0.95, 0.95)
    g2 = g * g
    denominator_base = max(1.0 + g2 - 2.0 * g * mu, 0.0001)
    denominator = (2.0 + g2) * denominator_base * math.sqrt(denominator_base)
    return (3.0 / (8.0 * math.pi)) * ((1.0 - g2) * (1.0 + mu * mu)) / denominator


VISIBLE_WAVELENGTHS_NM: Tuple[float, float, float] = (680.0, 550.0, 440.0)
RAYLEIGH_BETA: Tuple[float, float, float] = tuple(
    rayleigh_beta_for_wavelength_nm(wavelength_nm) for wavelength_nm in VISIBLE_WAVELENGTHS_NM
)
MIE_BETA: Tuple[float, float, float] = tuple(
    mie_beta_for_wavelength_nm(wavelength_nm) for wavelength_nm in VISIBLE_WAVELENGTHS_NM
)
OZONE_ABSORPTION: Tuple[float, float, float] = (0.650e-6, 1.881e-6, 0.085e-6)
WARM_MIE_TINT: Tuple[float, float, float] = (1.0, 0.58, 0.22)
COLD_SKY_TINT: Tuple[float, float, float] = (0.42, 0.66, 1.0)
VOID_TINT: Tuple[float, float, float] = (0.0008, 0.0014, 0.0040)


def air_mass_for_sun_elevation(sun_elevation_deg: float) -> float:
    """Return a bounded Kasten-Young style optical air mass."""

    safe_elevation = max(sun_elevation_deg, -5.0)
    sin_elevation = math.sin(math.radians(safe_elevation))
    denominator = sin_elevation + 0.50572 * (safe_elevation + 6.07995) ** -1.6364
    return clamp(1.0 / max(denominator, 0.025), 0.95, 40.0)


def curvature_depth_remap(distance_m: float, max_mesh_m: float = 5_000.0) -> float:
    """Map linear mesh distance to logarithmic depth used by the curvature fake."""

    gain = 0.0022
    safe_distance = clamp(distance_m, 0.0, max_mesh_m)
    numerator = math.log2(1.0 + safe_distance * gain)
    denominator = max(math.log2(1.0 + max_mesh_m * gain), 0.0001)
    return saturate(numerator / denominator)


def fake_planet_horizon_drop_m(distance_m: float, max_mesh_m: float = 5_000.0) -> float:
    """Return a shader-side curvature drop that sells a planet on a 5000 m mesh."""

    remapped = curvature_depth_remap(distance_m, max_mesh_m)
    fake_radius_m = 280_000.0
    physical_drop = (distance_m * distance_m) / max(2.0 * fake_radius_m, 1.0)
    return physical_drop * (0.25 + 0.75 * remapped)


def expected_bin_sizes() -> Dict[str, int]:
    """Return exact expected byte counts for generated atmosphere binaries."""

    return {
        SKY_LUT_FILE: SKY_LUT_WIDTH * SKY_LUT_HEIGHT * RGBA_CHANNEL_COUNT * HALF_FLOAT_BYTES,
        DENSITY_FILE: DENSITY_LAYERS * RGBA_CHANNEL_COUNT * HALF_FLOAT_BYTES,
    }


def build_density_matrix() -> List[Tuple[float, float, float, float]]:
    """Build 128 altitude rows from 0 km to 100 km."""

    rows: List[Tuple[float, float, float, float]] = []
    beta_r_luma = luminance(RAYLEIGH_BETA)
    beta_m_luma = luminance(MIE_BETA)

    for layer_index in range(DENSITY_LAYERS):
        altitude01 = layer_index / float(DENSITY_LAYERS - 1)
        altitude_m = altitude01 * ATMOSPHERE_HEIGHT_M
        rayleigh_density = safe_exp(-altitude_m / RAYLEIGH_SCALE_HEIGHT_M)
        mie_density = safe_exp(-altitude_m / MIE_SCALE_HEIGHT_M)
        rayleigh_column = RAYLEIGH_SCALE_HEIGHT_M * rayleigh_density
        mie_column = MIE_SCALE_HEIGHT_M * mie_density
        absorption = 1.0 - safe_exp(
            -(
                rayleigh_column * beta_r_luma
                + mie_column * beta_m_luma
                + smoothstep(8_000.0, 35_000.0, altitude_m) * 0.006
            )
        )
        rows.append(
            (
                altitude_m / 1000.0,
                finite_or_zero(rayleigh_density),
                finite_or_zero(mie_density),
                finite_or_zero(saturate(absorption)),
            )
        )

    return rows


def compute_sky_sample(sun_elevation_deg: float, vertical01: float) -> Tuple[float, float, float, float]:
    """Compute one linear RGBA sky sample for the baked LUT."""

    view01 = saturate(vertical01)
    eased_altitude01 = smoothstep(0.0, 1.0, view01)
    altitude_m = eased_altitude01 * ATMOSPHERE_HEIGHT_M
    density_r = safe_exp(-altitude_m / RAYLEIGH_SCALE_HEIGHT_M)
    density_m = safe_exp(-altitude_m / MIE_SCALE_HEIGHT_M)
    column_r = RAYLEIGH_SCALE_HEIGHT_M * density_r
    column_m = MIE_SCALE_HEIGHT_M * density_m
    ozone_column = 12_000.0 * smoothstep(10_000.0, 35_000.0, altitude_m) * (
        1.0 - smoothstep(38_000.0, 70_000.0, altitude_m)
    )

    sun_rad = math.radians(sun_elevation_deg)
    air_mass = air_mass_for_sun_elevation(sun_elevation_deg)
    sun_visibility = smoothstep(-6.0, 1.5, sun_elevation_deg)
    twilight = 1.0 - smoothstep(-7.5, 7.0, sun_elevation_deg)
    golden_hour = (1.0 - smoothstep(4.0, 28.0, sun_elevation_deg)) * sun_visibility
    high_sun = smoothstep(10.0, 55.0, sun_elevation_deg)

    horizon_weight = (1.0 - view01) * (1.0 - view01)
    space_fade = smoothstep(42_000.0, 100_000.0, altitude_m)
    path_scale = air_mass * (1.0 + horizon_weight * (0.85 + golden_hour * 3.2))
    path_scale *= lerp(1.0, 0.16, space_fade)

    sky_alignment = math.sin(sun_rad) * (0.15 + 0.85 * view01)
    horizon_alignment = math.cos(sun_rad) * (1.0 - view01) * 0.62
    cos_theta = clamp(sky_alignment + horizon_alignment, -1.0, 1.0)
    ray_phase = rayleigh_phase(cos_theta)
    aerosol_phase = mie_phase(cos_theta)

    transmittance: List[float] = []
    raw_color: List[float] = []
    max_rayleigh_beta = max(RAYLEIGH_BETA)

    for channel_index in range(3):
        beta_r = RAYLEIGH_BETA[channel_index]
        beta_m = MIE_BETA[channel_index]
        extinction = (
            beta_r * column_r
            + beta_m * column_m
            + OZONE_ABSORPTION[channel_index] * ozone_column
        ) * path_scale
        trans = safe_exp(-extinction)
        transmittance.append(trans)

    average_transmittance = sum(transmittance) / 3.0
    scatter_visibility = (1.0 - average_transmittance) * (0.22 + 0.78 * sun_visibility)

    for channel_index in range(3):
        beta_r = RAYLEIGH_BETA[channel_index]
        rayleigh_color = (beta_r / max_rayleigh_beta) * COLD_SKY_TINT[channel_index]
        rayleigh_term = rayleigh_color * ray_phase * scatter_visibility * 8.5
        mie_term = (
            WARM_MIE_TINT[channel_index]
            * aerosol_phase
            * scatter_visibility
            * (0.11 + 1.85 * golden_hour)
            * (0.35 + 0.65 * horizon_weight)
        )
        sun_disk_bleed = (
            transmittance[channel_index]
            * WARM_MIE_TINT[channel_index]
            * golden_hour
            * horizon_weight
            * 0.22
        )
        day_fill = COLD_SKY_TINT[channel_index] * high_sun * (1.0 - space_fade) * 0.025
        twilight_floor = VOID_TINT[channel_index] * (0.55 + twilight * 0.45)
        color = (rayleigh_term + mie_term + sun_disk_bleed + day_fill) * (
            0.10 + 0.90 * sun_visibility
        )
        color = lerp(color, twilight_floor, twilight * 0.55)
        color = lerp(color, VOID_TINT[channel_index] * (0.35 + sun_visibility * 0.65), space_fade)
        raw_color.append(finite_or_zero(clamp(color, 0.0, 16.0)))

    alpha = finite_or_zero(clamp(average_transmittance, 0.0, 1.0))
    return raw_color[0], raw_color[1], raw_color[2], alpha


def build_sky_lut() -> Tuple[List[Tuple[float, float, float, float]], Dict[str, Any]]:
    """Build the sky LUT and return its gradient audit."""

    samples: List[Tuple[float, float, float, float]] = []
    for row in range(SKY_LUT_HEIGHT):
        vertical01 = row / float(SKY_LUT_HEIGHT - 1)
        for column in range(SKY_LUT_WIDTH):
            sun01 = column / float(SKY_LUT_WIDTH - 1)
            sun_elevation = lerp(SUN_ELEVATION_MIN_DEG, SUN_ELEVATION_MAX_DEG, sun01)
            samples.append(compute_sky_sample(sun_elevation, vertical01))

    audit = audit_sky_gradient(samples)
    return samples, audit


def audit_sky_gradient(samples: Sequence[Tuple[float, float, float, float]]) -> Dict[str, Any]:
    """Measure seam, banding, and golden-hour/void sanity for a sky LUT."""

    max_adjacent_delta = 0.0
    max_surface_seam_delta = 0.0
    non_finite_count = 0

    for row in range(SKY_LUT_HEIGHT):
        for column in range(SKY_LUT_WIDTH):
            current = samples[row * SKY_LUT_WIDTH + column]
            if not all(math.isfinite(channel) for channel in current):
                non_finite_count += 1

            if column + 1 < SKY_LUT_WIDTH:
                right = samples[row * SKY_LUT_WIDTH + column + 1]
                delta = max(abs(current[i] - right[i]) for i in range(3))
                max_adjacent_delta = max(max_adjacent_delta, delta)

            if row + 1 < SKY_LUT_HEIGHT:
                above = samples[(row + 1) * SKY_LUT_WIDTH + column]
                delta = max(abs(current[i] - above[i]) for i in range(3))
                max_adjacent_delta = max(max_adjacent_delta, delta)
                if row == 0:
                    max_surface_seam_delta = max(max_surface_seam_delta, delta)

    dark_space_sample = samples[(SKY_LUT_HEIGHT - 1) * SKY_LUT_WIDTH + 0]
    golden_column = int((0.0 - SUN_ELEVATION_MIN_DEG) / (SUN_ELEVATION_MAX_DEG - SUN_ELEVATION_MIN_DEG) * (SKY_LUT_WIDTH - 1))
    golden_column = max(0, min(SKY_LUT_WIDTH - 1, golden_column))
    golden_horizon_sample = samples[golden_column]
    void_luminance = luminance(dark_space_sample[:3])
    golden_luminance = luminance(golden_horizon_sample[:3])

    status = "PASS"
    failures: List[str] = []
    if non_finite_count:
        status = "FAIL"
        failures.append("non_finite_sample")
    if max_adjacent_delta > GRADIENT_MAX_ADJACENT_DELTA:
        status = "FAIL"
        failures.append("adjacent_gradient_delta")
    if max_surface_seam_delta > SURFACE_SEAM_MAX_DELTA:
        status = "FAIL"
        failures.append("surface_seam_delta")
    if void_luminance > VOID_BLACK_MAX_LUMINANCE:
        status = "FAIL"
        failures.append("void_black_luminance")
    if golden_luminance < MIN_GOLDEN_HOUR_LUMINANCE:
        status = "FAIL"
        failures.append("golden_hour_luminance")

    return {
        "status": status,
        "failures": failures,
        "maxAdjacentDelta": max_adjacent_delta,
        "maxSurfaceSeamDelta": max_surface_seam_delta,
        "voidBlackLuminance": void_luminance,
        "goldenHourLuminance": golden_luminance,
        "nonFiniteCount": non_finite_count,
        "thresholds": {
            "maxAdjacentDelta": GRADIENT_MAX_ADJACENT_DELTA,
            "maxSurfaceSeamDelta": SURFACE_SEAM_MAX_DELTA,
            "voidBlackMaxLuminance": VOID_BLACK_MAX_LUMINANCE,
            "goldenHourMinLuminance": MIN_GOLDEN_HOUR_LUMINANCE,
        },
    }


def flatten_rows(rows: Iterable[Sequence[float]]) -> Iterable[float]:
    """Yield row-major floats from row tuples."""

    for row in rows:
        for value in row:
            yield value


def write_half_float_bin(path: Path, rows: Iterable[Sequence[float]]) -> int:
    """Write explicit little-endian half floats and return written byte count."""

    written_scalars = 0
    with path.open("wb") as handle:
        for value in flatten_rows(rows):
            safe_value = clamp(value, 0.0, 65_504.0)
            handle.write(struct.pack(HALF_STRUCT_FORMAT, safe_value))
            written_scalars += 1
    return written_scalars * HALF_FLOAT_BYTES


def read_half_float_rows(
    path: Path,
    column_count: int,
    expected_scalar_count: int,
) -> List[Tuple[float, ...]]:
    """Read raw little-endian half floats into row tuples for cold validation."""

    payload = path.read_bytes()
    expected_bytes = expected_scalar_count * HALF_FLOAT_BYTES
    if len(payload) != expected_bytes:
        raise ValueError(f"{path.name} expected {expected_bytes} bytes, got {len(payload)}")

    rows: List[Tuple[float, ...]] = []
    row: List[float] = []
    for scalar_index in range(expected_scalar_count):
        offset = scalar_index * HALF_FLOAT_BYTES
        value = struct.unpack_from(HALF_STRUCT_FORMAT, payload, offset)[0]
        row.append(value)
        if len(row) == column_count:
            rows.append(tuple(row))
            row = []

    return rows


def audit_density_matrix(rows: Sequence[Tuple[float, ...]]) -> Dict[str, Any]:
    """Audit decoded density rows after half-float quantization."""

    status = "PASS"
    failures: List[str] = []
    non_finite_count = 0
    monotonic_failures = 0
    min_absorption = 1.0
    max_absorption = 0.0

    if len(rows) != DENSITY_LAYERS:
        status = "FAIL"
        failures.append("row_count")

    previous_altitude = -1.0
    previous_rayleigh = 2.0
    previous_mie = 2.0
    for row in rows:
        if len(row) != RGBA_CHANNEL_COUNT:
            status = "FAIL"
            failures.append("column_count")
            continue

        altitude_km, rayleigh_density, mie_density, absorption = row
        if not all(math.isfinite(value) for value in row):
            non_finite_count += 1
            continue

        if altitude_km <= previous_altitude:
            monotonic_failures += 1
        if rayleigh_density > previous_rayleigh + 0.001:
            monotonic_failures += 1
        if mie_density > previous_mie + 0.001:
            monotonic_failures += 1
        if absorption < -0.001 or absorption > 1.001:
            monotonic_failures += 1

        previous_altitude = altitude_km
        previous_rayleigh = rayleigh_density
        previous_mie = mie_density
        min_absorption = min(min_absorption, absorption)
        max_absorption = max(max_absorption, absorption)

    first_altitude = rows[0][0] if rows else -1.0
    last_altitude = rows[-1][0] if rows else -1.0
    if abs(first_altitude - 0.0) > 0.001 or abs(last_altitude - 100.0) > 0.001:
        status = "FAIL"
        failures.append("altitude_bounds")
    if non_finite_count:
        status = "FAIL"
        failures.append("non_finite_density")
    if monotonic_failures:
        status = "FAIL"
        failures.append("density_monotonicity")

    return {
        "status": status,
        "failures": failures,
        "rowCount": len(rows),
        "firstAltitudeKm": first_altitude,
        "lastAltitudeKm": last_altitude,
        "nonFiniteCount": non_finite_count,
        "monotonicFailures": monotonic_failures,
        "minAbsorption": min_absorption,
        "maxAbsorption": max_absorption,
    }


def file_sha256(path: Path) -> str:
    """Return the uppercase SHA-256 digest for a generated file."""

    digest = hashlib.sha256()
    with path.open("rb") as handle:
        while True:
            chunk = handle.read(64 * 1024)
            if not chunk:
                break
            digest.update(chunk)
    return digest.hexdigest().upper()


def linear_to_srgb_byte(value: float) -> int:
    """Map a linear HDR-ish value to an 8-bit preview channel."""

    mapped = value / (1.0 + max(value, 0.0))
    srgb = saturate(mapped) ** (1.0 / 2.2)
    return int(round(srgb * 255.0))


def png_chunk(chunk_type: bytes, payload: bytes) -> bytes:
    """Build one PNG chunk."""

    crc = zlib.crc32(chunk_type)
    crc = zlib.crc32(payload, crc) & 0xFFFFFFFF
    return struct.pack(">I", len(payload)) + chunk_type + payload + struct.pack(">I", crc)


def write_png_preview(
    path: Path,
    width: int,
    height: int,
    samples: Sequence[Tuple[float, float, float, float]],
) -> None:
    """Write a simple RGB PNG preview with space at the top and ocean at the bottom."""

    rows = bytearray()
    for source_row in range(height - 1, -1, -1):
        rows.append(0)
        row_offset = source_row * width
        for column in range(width):
            sample = samples[row_offset + column]
            rows.append(linear_to_srgb_byte(sample[0]))
            rows.append(linear_to_srgb_byte(sample[1]))
            rows.append(linear_to_srgb_byte(sample[2]))

    ihdr = struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)
    payload = (
        b"\x89PNG\r\n\x1a\n"
        + png_chunk(b"IHDR", ihdr)
        + png_chunk(b"IDAT", zlib.compress(bytes(rows), level=9))
        + png_chunk(b"IEND", b"")
    )
    path.write_bytes(payload)


def validate_existing_output(output_dir: Path = DEFAULT_OUTPUT_DIR) -> Dict[str, Any]:
    """Validate existing atmosphere outputs against size, hash, and manifest contract."""

    manifest_path = output_dir / MANIFEST_FILE
    status = "PASS"
    manifest_errors: List[str] = []
    manifest: Dict[str, Any] = {}

    if manifest_path.exists():
        try:
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        except json.JSONDecodeError as exc:
            status = "FAIL"
            manifest_errors.append(f"manifest json decode failed: {exc}")
    else:
        status = "FAIL"
        manifest_errors.append(f"{MANIFEST_FILE} missing")

    expected_header = {
        "schema": "H8.AtmosphereLUTManifest.v1",
        "byteOrder": "little-endian",
        "scalarFormat": "float16",
        "scalarBytes": HALF_FLOAT_BYTES,
        "binaryHeader": "none",
        "structPackFormat": HALF_STRUCT_FORMAT,
    }
    for key, expected_value in expected_header.items():
        if manifest.get(key) != expected_value:
            status = "FAIL"
            manifest_errors.append(f"{key} expected {expected_value!r}, got {manifest.get(key)!r}")

    files: Dict[str, Any] = {}
    decoded_payloads: Dict[str, Any] = {}
    expected_sizes = expected_bin_sizes()
    manifest_files = manifest.get("files", {}) if manifest else {}
    for file_name, expected_bytes in expected_sizes.items():
        file_path = output_dir / file_name
        actual_bytes = file_path.stat().st_size if file_path.exists() else -1
        actual_sha = file_sha256(file_path) if file_path.exists() else ""
        manifest_info = manifest_files.get(file_name, {})
        manifest_bytes = manifest_info.get("bytes")
        expected_sha = manifest_info.get("sha256", "")
        size_matches = actual_bytes == expected_bytes
        manifest_size_matches = manifest_bytes == expected_bytes
        hash_matches = bool(expected_sha) and actual_sha == expected_sha
        if not size_matches or not manifest_size_matches or not hash_matches:
            status = "FAIL"

        decoded_status: Dict[str, Any]
        if size_matches and file_path.exists():
            try:
                if file_name == SKY_LUT_FILE:
                    decoded_rows = read_half_float_rows(
                        file_path,
                        RGBA_CHANNEL_COUNT,
                        SKY_LUT_WIDTH * SKY_LUT_HEIGHT * RGBA_CHANNEL_COUNT,
                    )
                    decoded_status = audit_sky_gradient(decoded_rows)  # type: ignore[arg-type]
                else:
                    density_rows = read_half_float_rows(
                        file_path,
                        RGBA_CHANNEL_COUNT,
                        DENSITY_LAYERS * RGBA_CHANNEL_COUNT,
                    )
                    decoded_status = audit_density_matrix(density_rows)
            except (OSError, ValueError, struct.error) as exc:
                decoded_status = {
                    "status": "FAIL",
                    "failures": ["decode_exception"],
                    "error": str(exc),
                }
            if decoded_status.get("status") != "PASS":
                status = "FAIL"
        else:
            decoded_status = {
                "status": "FAIL",
                "failures": ["missing_or_wrong_size"],
            }
            status = "FAIL"

        decoded_payloads[file_name] = decoded_status
        files[file_name] = {
            "expectedBytes": expected_bytes,
            "actualBytes": actual_bytes,
            "matches": size_matches,
            "manifestBytes": manifest_bytes,
            "manifestBytesMatch": manifest_size_matches,
            "expectedSha256": expected_sha,
            "actualSha256": actual_sha,
            "hashMatches": hash_matches,
        }

    preview_path = output_dir / PREVIEW_FILE
    preview_info = manifest.get("preview", {}) if manifest else {}
    preview_sha = file_sha256(preview_path) if preview_path.exists() else ""
    preview_hash_matches = bool(preview_info.get("sha256")) and preview_sha == preview_info.get("sha256")
    if not preview_path.exists() or not preview_hash_matches:
        status = "FAIL"

    gradient_audit = manifest.get("gradientAudit", {}) if manifest else {}
    if gradient_audit.get("status") != "PASS":
        status = "FAIL"

    return {
        "status": status,
        "files": files,
        "decodedPayloads": decoded_payloads,
        "preview": {
            "exists": preview_path.exists(),
            "actualBytes": preview_path.stat().st_size if preview_path.exists() else -1,
            "expectedSha256": preview_info.get("sha256", ""),
            "actualSha256": preview_sha,
            "hashMatches": preview_hash_matches,
        },
        "gradientAudit": gradient_audit,
        "manifestErrors": manifest_errors,
    }


def generate_all(
    output_dir: Path = DEFAULT_OUTPUT_DIR,
    report_path: Path | None = DEFAULT_REPORT_PATH,
) -> Dict[str, Any]:
    """Generate atmosphere binaries, preview image, manifest, and validation report."""

    output_dir.mkdir(parents=True, exist_ok=True)
    if report_path is not None:
        report_path.parent.mkdir(parents=True, exist_ok=True)

    density_matrix = build_density_matrix()
    sky_lut, gradient_audit = build_sky_lut()

    files = {
        DENSITY_FILE: {
            "shape": [DENSITY_LAYERS, RGBA_CHANNEL_COUNT],
            "columns": [
                "altitudeKilometers",
                "rayleighDensity",
                "mieDensity",
                "absorption01",
            ],
            "axes": {
                "altitudeKilometers": {
                    "count": DENSITY_LAYERS,
                    "min": 0.0,
                    "max": 100.0,
                    "spacing": "linear",
                }
            },
            "bytes": write_half_float_bin(output_dir / DENSITY_FILE, density_matrix),
        },
        SKY_LUT_FILE: {
            "shape": [SKY_LUT_HEIGHT, SKY_LUT_WIDTH, RGBA_CHANNEL_COUNT],
            "columns": ["linearR", "linearG", "linearB", "meanTransmittance"],
            "axes": {
                "altitudeVisual": {
                    "count": SKY_LUT_HEIGHT,
                    "min": 0.0,
                    "max": 100.0,
                    "unit": "kilometers",
                    "spacing": "smoothstep_visual_remap",
                },
                "sunElevationDegrees": {
                    "count": SKY_LUT_WIDTH,
                    "min": SUN_ELEVATION_MIN_DEG,
                    "max": SUN_ELEVATION_MAX_DEG,
                    "spacing": "linear",
                },
            },
            "bytes": write_half_float_bin(output_dir / SKY_LUT_FILE, sky_lut),
        },
    }

    write_png_preview(output_dir / PREVIEW_FILE, SKY_LUT_WIDTH, SKY_LUT_HEIGHT, sky_lut)

    for file_name, file_info in files.items():
        file_info["sha256"] = file_sha256(output_dir / file_name)

    manifest = {
        "schema": "H8.AtmosphereLUTManifest.v1",
        "byteOrder": "little-endian",
        "scalarFormat": "float16",
        "scalarBytes": HALF_FLOAT_BYTES,
        "binaryHeader": "none",
        "structPackFormat": HALF_STRUCT_FORMAT,
        "files": files,
        "preview": {
            "file": PREVIEW_FILE,
            "format": "PNG RGB8",
            "width": SKY_LUT_WIDTH,
            "height": SKY_LUT_HEIGHT,
            "bytes": (output_dir / PREVIEW_FILE).stat().st_size,
            "sha256": file_sha256(output_dir / PREVIEW_FILE),
        },
        "gradientAudit": gradient_audit,
        "formulaConstants": {
            "planetRadiusMeters": PLANET_RADIUS_M,
            "atmosphereHeightMeters": ATMOSPHERE_HEIGHT_M,
            "rayleighScaleHeightMeters": RAYLEIGH_SCALE_HEIGHT_M,
            "mieScaleHeightMeters": MIE_SCALE_HEIGHT_M,
            "mieAnisotropyG": MIE_ANISOTROPY_G,
            "wavelengthsNmRGB": list(VISIBLE_WAVELENGTHS_NM),
            "rayleighBetaRGB": list(RAYLEIGH_BETA),
            "mieBetaRGB": list(MIE_BETA),
            "ozoneAbsorptionRGB": list(OZONE_ABSORPTION),
        },
        "planetCurvatureFake": {
            "distanceRemap": "log2(1 + distanceMeters * 0.0022) / log2(1 + 5000 * 0.0022)",
            "horizonDropMeters": "(distanceMeters^2 / (2 * 280000)) * (0.25 + 0.75 * remap)",
            "sample5000mDropMeters": fake_planet_horizon_drop_m(5_000.0),
        },
        "validation": {
            "files": {
                file_name: {
                    "expectedBytes": expected_bin_sizes()[file_name],
                    "actualBytes": file_info["bytes"],
                    "matches": expected_bin_sizes()[file_name] == file_info["bytes"],
                }
                for file_name, file_info in files.items()
            },
            "status": "PASS"
            if all(expected_bin_sizes()[name] == info["bytes"] for name, info in files.items())
            and gradient_audit["status"] == "PASS"
            else "FAIL",
        },
    }

    manifest_path = output_dir / MANIFEST_FILE
    manifest_path.write_text(
        json.dumps(manifest, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )

    validation = validate_existing_output(output_dir)
    result = {
        "outputDir": str(output_dir),
        "manifest": str(manifest_path),
        "validation": validation,
    }
    if report_path is not None:
        report_path.write_text(
            json.dumps(result, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
        )
    return result


def main() -> int:
    """CLI entry point."""

    parser = argparse.ArgumentParser(description="Bake HECTON-8 atmosphere LUTs.")
    parser.add_argument(
        "--out",
        type=Path,
        default=DEFAULT_OUTPUT_DIR,
        help="Output directory for generated atmosphere LUT assets.",
    )
    parser.add_argument(
        "--report",
        type=Path,
        default=DEFAULT_REPORT_PATH,
        help="Validation report JSON path. Use an empty string to skip.",
    )
    parser.add_argument(
        "--verify",
        action="store_true",
        help="Validate existing atmosphere LUT outputs without regenerating files.",
    )
    args = parser.parse_args()

    report_path = args.report
    if str(report_path) == "":
        report_path = None

    if args.verify:
        validation = validate_existing_output(args.out)
        result = {
            "outputDir": str(args.out),
            "validation": validation,
        }
        if report_path is not None:
            report_path.parent.mkdir(parents=True, exist_ok=True)
            report_path.write_text(
                json.dumps(result, indent=2, sort_keys=True) + "\n",
                encoding="utf-8",
            )
    else:
        result = generate_all(args.out, report_path)

    print(json.dumps(result["validation"], indent=2, sort_keys=True))
    return 0 if result["validation"]["status"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
