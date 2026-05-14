#!/usr/bin/env python3
"""Bake HECTON-8 offline math look-up tables.

The generated binary files are raw little-endian float32 arrays. They contain
no headers so Burst-side readers can memcpy into fixed NativeArray<float>
buffers whose dimensions are defined in the manifest and design document.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import struct
from pathlib import Path
from typing import Any, Dict

import numpy as np


ROOT_DIR = Path(__file__).resolve().parents[1]
DEFAULT_OUTPUT_DIR = ROOT_DIR / "Data" / "Precomputed"

SABINE_VOLUME_COUNT = 40
SABINE_ABSORPTION_COUNT = 25
DALTON_DEPTH_COUNT = 2001
DALTON_COLUMN_COUNT = 5
GERSTNER_WEATHER_STATE_COUNT = 100
GERSTNER_WAVE_COUNT = 16
GERSTNER_FLOATS_PER_WAVE = 5
CAUSTICS_DEPTH_COUNT = 101
CAUSTICS_CHANNEL_COUNT = 3
ECOSYSTEM_STEPS = 1_000_000

WEATHER_RNG_SEED = 0x8EC701
FLOAT32_BYTES = struct.calcsize("<f")


def hash_to_unit_float32(seed: int, index_a: int, index_b: int, salt: int) -> float:
    """Return a deterministic [0, 1) float from integer inputs."""

    value = (
        (seed & 0xFFFFFFFF)
        ^ ((index_a + 0x9E3779B9) & 0xFFFFFFFF)
        ^ (((index_b + 0x85EBCA6B) << 13) & 0xFFFFFFFF)
        ^ ((salt * 0xC2B2AE35) & 0xFFFFFFFF)
    ) & 0xFFFFFFFF
    value ^= (value >> 16)
    value = (value * 0x7FEB352D) & 0xFFFFFFFF
    value ^= (value >> 15)
    value = (value * 0x846CA68B) & 0xFFFFFFFF
    value ^= (value >> 16)
    return (value & 0x00FFFFFF) / float(0x01000000)


def hash_to_signed_float32(seed: int, index_a: int, index_b: int, salt: int) -> float:
    """Return a deterministic [-1, 1) float from integer inputs."""

    return hash_to_unit_float32(seed, index_a, index_b, salt) * 2.0 - 1.0


def expected_bin_sizes() -> Dict[str, int]:
    """Return expected binary byte counts for every baked LUT."""

    return {
        "sabine_reverb_rt60.bin": (
            SABINE_VOLUME_COUNT * SABINE_ABSORPTION_COUNT * FLOAT32_BYTES
        ),
        "dalton_gas_toxicity.bin": DALTON_DEPTH_COUNT * DALTON_COLUMN_COUNT * FLOAT32_BYTES,
        "gerstner_wave_weather.bin": (
            GERSTNER_WEATHER_STATE_COUNT
            * GERSTNER_WAVE_COUNT
            * GERSTNER_FLOATS_PER_WAVE
            * FLOAT32_BYTES
        ),
        "caustics_dispersion_offsets.bin": (
            CAUSTICS_DEPTH_COUNT * CAUSTICS_CHANNEL_COUNT * FLOAT32_BYTES
        ),
    }


def build_sabine_reverb_lut() -> np.ndarray:
    """Build RT60 values for 40 volume bands by 25 absorption bands."""

    volumes_m3 = np.linspace(10.0, 10_000.0, SABINE_VOLUME_COUNT, dtype=np.float32)
    absorption_coefficients = np.linspace(
        0.05,
        0.95,
        SABINE_ABSORPTION_COUNT,
        dtype=np.float32,
    )

    equivalent_cube_surface_area = 6.0 * np.power(volumes_m3, 2.0 / 3.0)
    absorption_area = np.maximum(
        equivalent_cube_surface_area[:, np.newaxis] * absorption_coefficients[np.newaxis, :],
        0.001,
    )
    rt60_seconds = 0.161 * volumes_m3[:, np.newaxis] / absorption_area
    return np.clip(rt60_seconds, 0.05, 12.0).astype(np.float32)


def build_dalton_toxicity_lut() -> np.ndarray:
    """Build per-meter gas pressure and toxicity rows from 0m to 2000m."""

    depths_m = np.arange(0, DALTON_DEPTH_COUNT, dtype=np.float32)
    ambient_atm = 1.0 + depths_m / 10.1325
    oxygen_partial_atm = ambient_atm * 0.2095
    nitrogen_partial_atm = ambient_atm * 0.7808
    oxygen_toxicity_01 = np.clip((oxygen_partial_atm - 1.4) / 0.2, 0.0, 1.0)
    narcosis_multiplier = 1.0 + np.clip((nitrogen_partial_atm - 3.2) * 0.18, 0.0, 7.0)

    return np.stack(
        (
            ambient_atm,
            oxygen_partial_atm,
            nitrogen_partial_atm,
            oxygen_toxicity_01,
            narcosis_multiplier,
        ),
        axis=1,
    ).astype(np.float32)


def build_gerstner_weather_lut() -> np.ndarray:
    """Build 100 deterministic weather states with 16 Gerstner waves each."""

    table = np.zeros(
        (
            GERSTNER_WEATHER_STATE_COUNT,
            GERSTNER_WAVE_COUNT,
            GERSTNER_FLOATS_PER_WAVE,
        ),
        dtype=np.float32,
    )

    golden_angle = math.pi * (3.0 - math.sqrt(5.0))
    for state_index in range(GERSTNER_WEATHER_STATE_COUNT):
        weather01 = state_index / float(GERSTNER_WEATHER_STATE_COUNT - 1)
        storm_curve = weather01 * weather01 * (3.0 - 2.0 * weather01)
        dominant_direction = weather01 * math.tau * 0.35
        direction_spread = 0.15 + weather01 * 1.35

        for wave_index in range(GERSTNER_WAVE_COUNT):
            band01 = wave_index / float(GERSTNER_WAVE_COUNT - 1)
            speed_jitter = hash_to_signed_float32(
                WEATHER_RNG_SEED,
                state_index,
                wave_index,
                1,
            ) * 0.025
            angle_jitter = hash_to_signed_float32(
                WEATHER_RNG_SEED,
                state_index,
                wave_index,
                2,
            ) * 0.08
            angle = dominant_direction + (wave_index - 7.5) * golden_angle * direction_spread
            angle += angle_jitter * (0.25 + weather01)

            speed = 0.35 + storm_curve * 4.25 + band01 * 0.42 + speed_jitter
            amplitude = 0.015 + storm_curve * (0.22 + band01 * 0.16)
            steepness = np.clip(0.04 + storm_curve * 0.72 - band01 * 0.035, 0.02, 0.88)

            table[state_index, wave_index, 0] = np.float32(speed)
            table[state_index, wave_index, 1] = np.float32(amplitude)
            table[state_index, wave_index, 2] = np.float32(steepness)
            table[state_index, wave_index, 3] = np.float32(math.cos(angle))
            table[state_index, wave_index, 4] = np.float32(math.sin(angle))

    return table


def build_caustics_dispersion_lut() -> np.ndarray:
    """Build chromatic UV offsets for depth attenuation from 0m to -100m."""

    depths_m = np.linspace(0.0, 100.0, CAUSTICS_DEPTH_COUNT, dtype=np.float32)
    depth01 = depths_m / 100.0
    attenuation = np.exp(-depths_m / 42.0)
    dispersion = depth01 * attenuation * 0.004

    red_offset = dispersion * 1.15
    green_offset = dispersion * 0.15
    blue_offset = -dispersion * 1.25
    return np.stack((red_offset, green_offset, blue_offset), axis=1).astype(np.float32)


def simulate_lotka_volterra() -> Dict[str, Any]:
    """Run a 1,000,000-step damped predator/prey biomass simulation."""

    birth_rate = 0.030
    death_rate = 0.018
    feed_rate = 0.000006
    predator_conversion = 0.35
    carrying_capacity = 10_000.0
    dt = 0.02

    prey = 4_200.0
    predator = 680.0
    min_prey = prey
    max_prey = prey
    min_predator = predator
    max_predator = predator

    for _ in range(ECOSYSTEM_STEPS):
        prey_growth = birth_rate * prey * (1.0 - prey / carrying_capacity)
        predation_loss = feed_rate * prey * predator
        predator_growth = predator_conversion * predation_loss
        predator_loss = death_rate * predator

        prey += (prey_growth - predation_loss) * dt
        predator += (predator_growth - predator_loss) * dt

        if not math.isfinite(prey) or not math.isfinite(predator):
            raise FloatingPointError("Lotka-Volterra integration produced a non-finite value.")

        prey = min(max(prey, 0.001), carrying_capacity)
        predator = min(max(predator, 0.001), carrying_capacity)
        min_prey = min(min_prey, prey)
        max_prey = max(max_prey, prey)
        min_predator = min(min_predator, predator)
        max_predator = max(max_predator, predator)

    stable_prey = death_rate / (predator_conversion * feed_rate)
    stable_predator = birth_rate * (1.0 - stable_prey / carrying_capacity) / feed_rate

    return {
        "BirthRate": birth_rate,
        "DeathRate": death_rate,
        "FeedRate": feed_rate,
        "PredatorConversion": predator_conversion,
        "PreyCarryingCapacity": carrying_capacity,
        "IntegrationSteps": ECOSYSTEM_STEPS,
        "DeltaTimeSeconds": dt,
        "StablePreyBiomass": stable_prey,
        "StablePredatorBiomass": stable_predator,
        "FinalPreyBiomass": prey,
        "FinalPredatorBiomass": predator,
        "ObservedPreyMin": min_prey,
        "ObservedPreyMax": max_prey,
        "ObservedPredatorMin": min_predator,
        "ObservedPredatorMax": max_predator,
    }


def write_float32_bin(path: Path, table: np.ndarray) -> int:
    """Write a contiguous table as explicit little-endian float32 values."""

    contiguous = np.ascontiguousarray(table, dtype=np.float32)
    with path.open("wb") as handle:
        for value in contiguous.ravel(order="C"):
            handle.write(struct.pack("<f", float(value)))
    return contiguous.size * FLOAT32_BYTES


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


def validate_bin_sizes(output_dir: Path) -> Dict[str, Any]:
    """Validate that every generated binary matches its exact expected byte size."""

    expected = expected_bin_sizes()
    results: Dict[str, Any] = {}
    status = "PASS"

    for file_name, expected_size in expected.items():
        file_path = output_dir / file_name
        actual_size = file_path.stat().st_size if file_path.exists() else -1
        matches = actual_size == expected_size
        if not matches:
            status = "FAIL"
        results[file_name] = {
            "expectedBytes": expected_size,
            "actualBytes": actual_size,
            "matches": matches,
        }

    return {
        "status": status,
        "files": results,
    }


def generate_all(output_dir: Path = DEFAULT_OUTPUT_DIR) -> Dict[str, Any]:
    """Generate all LUTs, the manifest, and the ecosystem coefficient JSON."""

    output_dir.mkdir(parents=True, exist_ok=True)

    sabine = build_sabine_reverb_lut()
    dalton = build_dalton_toxicity_lut()
    gerstner = build_gerstner_weather_lut()
    caustics = build_caustics_dispersion_lut()
    ecosystem = simulate_lotka_volterra()

    files = {
        "sabine_reverb_rt60.bin": {
            "shape": [SABINE_VOLUME_COUNT, SABINE_ABSORPTION_COUNT],
            "columns": ["rt60Seconds"],
            "axes": {
                "volumeM3": {
                    "count": SABINE_VOLUME_COUNT,
                    "min": 10.0,
                    "max": 10_000.0,
                    "spacing": "linear",
                },
                "absorptionCoefficient": {
                    "count": SABINE_ABSORPTION_COUNT,
                    "min": 0.05,
                    "max": 0.95,
                    "spacing": "linear",
                },
            },
            "bytes": write_float32_bin(output_dir / "sabine_reverb_rt60.bin", sabine),
        },
        "dalton_gas_toxicity.bin": {
            "shape": [DALTON_DEPTH_COUNT, DALTON_COLUMN_COUNT],
            "columns": [
                "ambientPressureAtm",
                "oxygenPartialPressureAtm",
                "nitrogenPartialPressureAtm",
                "oxygenToxicity01",
                "nitrogenNarcosisMultiplier",
            ],
            "axes": {
                "depthMeters": {
                    "count": DALTON_DEPTH_COUNT,
                    "min": 0,
                    "max": 2000,
                    "step": 1,
                },
            },
            "bytes": write_float32_bin(output_dir / "dalton_gas_toxicity.bin", dalton),
        },
        "gerstner_wave_weather.bin": {
            "shape": [
                GERSTNER_WEATHER_STATE_COUNT,
                GERSTNER_WAVE_COUNT,
                GERSTNER_FLOATS_PER_WAVE,
            ],
            "columns": [
                "speed",
                "amplitude",
                "steepness",
                "directionX",
                "directionZ",
            ],
            "axes": {
                "weatherState": {
                    "count": GERSTNER_WEATHER_STATE_COUNT,
                    "min": 0,
                    "max": 99,
                    "meaning": "calm_to_hurricane",
                },
                "waveIndex": {
                    "count": GERSTNER_WAVE_COUNT,
                    "min": 0,
                    "max": 15,
                },
            },
            "bytes": write_float32_bin(output_dir / "gerstner_wave_weather.bin", gerstner),
        },
        "caustics_dispersion_offsets.bin": {
            "shape": [CAUSTICS_DEPTH_COUNT, CAUSTICS_CHANNEL_COUNT],
            "columns": ["redUvOffset", "greenUvOffset", "blueUvOffset"],
            "axes": {
                "depthMeters": {
                    "count": CAUSTICS_DEPTH_COUNT,
                    "min": 0,
                    "max": -100,
                    "step": -1,
                },
            },
            "bytes": write_float32_bin(output_dir / "caustics_dispersion_offsets.bin", caustics),
        },
    }

    coefficient_path = output_dir / "ecosystem_coefficients.json"
    coefficient_path.write_text(
        json.dumps(ecosystem, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    for file_name, file_info in files.items():
        file_info["sha256"] = file_sha256(output_dir / file_name)

    validation = validate_bin_sizes(output_dir)
    manifest = {
        "schema": "H8.MathLUTManifest.v1",
        "byteOrder": "little-endian",
        "scalarFormat": "float32",
        "scalarBytes": FLOAT32_BYTES,
        "binaryHeader": "none",
        "structPackFormat": "<f",
        "weatherRngSeed": WEATHER_RNG_SEED,
        "files": files,
        "jsonFiles": {
            "ecosystem_coefficients.json": {
                "purpose": "Predator/prey biomass constants for the Ecosystem Director.",
                "sha256": file_sha256(coefficient_path),
                "keys": [
                    "BirthRate",
                    "DeathRate",
                    "FeedRate",
                    "PredatorConversion",
                    "PreyCarryingCapacity",
                    "StablePreyBiomass",
                    "StablePredatorBiomass",
                ],
            }
        },
        "validation": validation,
    }

    manifest_path = output_dir / "math_lut_manifest.json"
    manifest_path.write_text(
        json.dumps(manifest, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )

    return {
        "outputDir": str(output_dir),
        "files": files,
        "ecosystem": ecosystem,
        "validation": validation,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Bake HECTON-8 offline math LUTs.")
    parser.add_argument(
        "--out",
        type=Path,
        default=DEFAULT_OUTPUT_DIR,
        help="Output directory for generated LUT assets.",
    )
    args = parser.parse_args()

    result = generate_all(args.out)
    print(json.dumps(result["validation"], indent=2, sort_keys=True))
    return 0 if result["validation"]["status"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
