#!/usr/bin/env python3
"""Bake and recursively validate HECTON-8 acoustic reverb LUT data."""

from __future__ import annotations

import argparse
import math
import struct
import sys
import time
import zlib
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, List, Sequence, Tuple

import numpy as np


ROOT_DIR = Path(__file__).resolve().parents[1]
DEFAULT_OUTPUT_PATH = ROOT_DIR / "Data" / "Precomputed" / "Reverb_LUT.bin"

MAGIC = b"H8RVLUT1"
VERSION = 1
HEADER_BYTES = 256
VOLUME_COUNT = 256
ABSORPTION_COUNT = 256
MATERIAL_COUNT = 4
DAMPING_BAND_COUNT = 4
FLOAT32_BYTES = 4
PAYLOAD_BYTES = VOLUME_COUNT * ABSORPTION_COUNT * FLOAT32_BYTES
EXPECTED_FILE_BYTES = HEADER_BYTES + PAYLOAD_BYTES

SABINE_COEFFICIENT = 0.161
VOLUME_MIN_M3 = 10.0
VOLUME_MAX_M3 = 100_000.0
ABSORPTION_MIN = 0.01
ABSORPTION_MAX = 0.99
RT60_MIN_SECONDS = 0.05
RT60_MAX_SECONDS = 12.0
DAMPING_FREQUENCY_BANDS_HZ = (500.0, 2_000.0, 8_000.0, 16_000.0)
MATERIALS = ("Steel", "Rock", "Coral", "Water")


@dataclass(frozen=True)
class EdgeCase:
    """Validation case addressed by exact LUT indices."""

    name: str
    volume_index: int
    absorption_index: int
    max_relative_error_percent: float


EDGE_CASES = (
    EdgeCase("Small locker", 0, 255, 0.01),
    EdgeCase("Crew compartment", 92, 180, 0.01),
    EdgeCase("Pressurized corridor", 128, 128, 0.01),
    EdgeCase("Mega-Cave", 255, 255, 0.01),
    EdgeCase("Giant Void", 255, 0, 0.01),
)


def build_axes() -> Tuple[np.ndarray, np.ndarray]:
    """Return log-volume and linear-absorption axes as float32 arrays."""

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


def compute_cube_surface_area(volume_m3: np.ndarray | float) -> np.ndarray | float:
    """Approximate room boundary surface from an equal-volume cube."""

    return 6.0 * np.power(volume_m3, 2.0 / 3.0)


def compute_rt60_seconds(volume_m3: np.ndarray | float, absorption: np.ndarray | float) -> np.ndarray | float:
    """Compute clamped Sabine RT60 from volume, surface proxy, and absorption."""

    surface_area = compute_cube_surface_area(volume_m3)
    equivalent_absorption_area = np.maximum(surface_area * absorption, 0.001)
    rt60 = SABINE_COEFFICIENT * volume_m3 / equivalent_absorption_area
    return np.clip(rt60, RT60_MIN_SECONDS, RT60_MAX_SECONDS)


def build_reverb_matrix() -> np.ndarray:
    """Build the 256 x 256 row-major RT60 payload."""

    volumes, absorption = build_axes()
    matrix = compute_rt60_seconds(volumes[:, np.newaxis], absorption[np.newaxis, :])
    return np.asarray(matrix, dtype="<f4", order="C")


def calculate_material_damping_curves() -> np.ndarray:
    """Return material x frequency-band high-frequency damping ratios."""

    material_terms = (
        (0.06, 0.10, 0.30),  # Steel: bright reflections, modest water coupling.
        (0.18, 0.22, 0.45),  # Rock: rough boundary, stronger high-band loss.
        (0.28, 0.38, 0.55),  # Coral: porous organic scatter.
        (0.12, 0.68, 1.00),  # Water: volumetric high-frequency absorption dominates.
    )
    curves = np.zeros((MATERIAL_COUNT, DAMPING_BAND_COUNT), dtype="<f4")
    for material_index, (base_absorption, hf_slope, water_coupling) in enumerate(material_terms):
        for band_index, frequency_hz in enumerate(DAMPING_FREQUENCY_BANDS_HZ):
            normalized_frequency = math.pow(frequency_hz / DAMPING_FREQUENCY_BANDS_HZ[-1], 0.65)
            seawater_loss = (1.0 - math.exp(-frequency_hz / 12_000.0)) * 0.42
            total_absorption = base_absorption + hf_slope * normalized_frequency
            total_absorption += seawater_loss * water_coupling
            curves[material_index, band_index] = np.float32(
                min(0.98, max(0.05, 1.0 - total_absorption))
            )
    return curves


def build_header(payload_crc32: int, damping_curves: np.ndarray) -> bytes:
    """Build the fixed-size binary header."""

    header = bytearray(HEADER_BYTES)
    struct.pack_into("<8sIIIIIIII", header, 0, MAGIC, VERSION, HEADER_BYTES, VOLUME_COUNT,
                     ABSORPTION_COUNT, MATERIAL_COUNT, DAMPING_BAND_COUNT, PAYLOAD_BYTES,
                     payload_crc32)
    struct.pack_into(
        "<fffffff",
        header,
        40,
        VOLUME_MIN_M3,
        VOLUME_MAX_M3,
        ABSORPTION_MIN,
        ABSORPTION_MAX,
        RT60_MIN_SECONDS,
        RT60_MAX_SECONDS,
        SABINE_COEFFICIENT,
    )
    struct.pack_into("<4f", header, 72, *DAMPING_FREQUENCY_BANDS_HZ)
    struct.pack_into("<16f", header, 88, *damping_curves.reshape(-1).astype("<f4").tolist())
    return bytes(header)


def bake_reverb_lut(output_path: Path) -> dict:
    """Bake `Reverb_LUT.bin` and return deterministic metadata."""

    started = time.perf_counter()
    output_path.parent.mkdir(parents=True, exist_ok=True)
    matrix = build_reverb_matrix()
    payload = matrix.tobytes(order="C")
    payload_crc32 = zlib.crc32(payload) & 0xFFFFFFFF
    damping_curves = calculate_material_damping_curves()
    header = build_header(payload_crc32, damping_curves)
    output_path.write_bytes(header + payload)
    elapsed_us = (time.perf_counter() - started) * 1_000_000.0
    return {
        "bytes": output_path.stat().st_size,
        "payloadCrc32": f"{payload_crc32:08X}",
        "elapsedUs": elapsed_us,
    }


def read_header(file_bytes: bytes) -> dict:
    """Decode the fixed header fields."""

    if len(file_bytes) < HEADER_BYTES:
        raise ValueError("file shorter than header")
    fields = struct.unpack_from("<8sIIIIIIII", file_bytes, 0)
    axis_fields = struct.unpack_from("<fffffff", file_bytes, 40)
    bands = struct.unpack_from("<4f", file_bytes, 72)
    damping_values = struct.unpack_from("<16f", file_bytes, 88)
    return {
        "magic": fields[0],
        "version": fields[1],
        "headerBytes": fields[2],
        "volumeCount": fields[3],
        "absorptionCount": fields[4],
        "materialCount": fields[5],
        "dampingBandCount": fields[6],
        "payloadBytes": fields[7],
        "payloadCrc32": fields[8],
        "volumeMinM3": axis_fields[0],
        "volumeMaxM3": axis_fields[1],
        "absorptionMin": axis_fields[2],
        "absorptionMax": axis_fields[3],
        "rt60MinSeconds": axis_fields[4],
        "rt60MaxSeconds": axis_fields[5],
        "sabineCoefficient": axis_fields[6],
        "dampingBandsHz": bands,
        "dampingValues": damping_values,
    }


def load_payload_matrix(file_bytes: bytes) -> np.ndarray:
    """Read the RT60 payload as a row-major float32 matrix."""

    payload = file_bytes[HEADER_BYTES:]
    matrix = np.frombuffer(payload, dtype="<f4")
    return matrix.reshape((VOLUME_COUNT, ABSORPTION_COUNT))


def validate_edge_case(matrix: np.ndarray, volumes: np.ndarray, absorption: np.ndarray, edge: EdgeCase) -> str:
    """Validate one edge case and return a compact report line."""

    volume = float(volumes[edge.volume_index])
    absorption_value = float(absorption[edge.absorption_index])
    manual_rt60 = float(compute_rt60_seconds(volume, absorption_value))
    lut_rt60 = float(matrix[edge.volume_index, edge.absorption_index])
    denominator = max(abs(manual_rt60), 0.000001)
    relative_error_percent = abs(lut_rt60 - manual_rt60) / denominator * 100.0
    if relative_error_percent > edge.max_relative_error_percent:
        raise ValueError(
            f"{edge.name} relative error {relative_error_percent:.8f}% exceeds "
            f"{edge.max_relative_error_percent:.8f}%"
        )
    return (
        f"{edge.name}: volume={volume:.6f}m3 absorption={absorption_value:.6f} "
        f"manual={manual_rt60:.8f}s lut={lut_rt60:.8f}s error={relative_error_percent:.8f}%"
    )


def validate_edge_cases_recursive(
    matrix: np.ndarray,
    volumes: np.ndarray,
    absorption: np.ndarray,
    edge_cases: Sequence[EdgeCase],
    index: int,
    report_lines: List[str],
) -> None:
    """Recursively validate every acoustic edge case."""

    if index >= len(edge_cases):
        return
    report_lines.append(validate_edge_case(matrix, volumes, absorption, edge_cases[index]))
    validate_edge_cases_recursive(matrix, volumes, absorption, edge_cases, index + 1, report_lines)


def validate_reverb_lut(output_path: Path) -> Tuple[bool, List[str]]:
    """Validate binary size, header, payload, damping curves, and edge cases."""

    report: List[str] = []
    file_bytes = output_path.read_bytes()
    actual_size = len(file_bytes)
    if actual_size != EXPECTED_FILE_BYTES:
        raise ValueError(f"byte size mismatch: expected {EXPECTED_FILE_BYTES}, got {actual_size}")

    header = read_header(file_bytes)
    expected_header = {
        "magic": MAGIC,
        "version": VERSION,
        "headerBytes": HEADER_BYTES,
        "volumeCount": VOLUME_COUNT,
        "absorptionCount": ABSORPTION_COUNT,
        "materialCount": MATERIAL_COUNT,
        "dampingBandCount": DAMPING_BAND_COUNT,
        "payloadBytes": PAYLOAD_BYTES,
    }
    for key, expected_value in expected_header.items():
        actual_value = header[key]
        if actual_value != expected_value:
            raise ValueError(f"header {key} mismatch: expected {expected_value}, got {actual_value}")

    payload = file_bytes[HEADER_BYTES:]
    payload_crc32 = zlib.crc32(payload) & 0xFFFFFFFF
    if payload_crc32 != header["payloadCrc32"]:
        raise ValueError("payload CRC32 mismatch")

    matrix = load_payload_matrix(file_bytes)
    if not np.isfinite(matrix).all():
        raise ValueError("payload contains non-finite values")
    if float(matrix.min()) < RT60_MIN_SECONDS or float(matrix.max()) > RT60_MAX_SECONDS:
        raise ValueError("payload RT60 clamp range violated")

    damping = np.asarray(header["dampingValues"], dtype=np.float32).reshape((MATERIAL_COUNT, DAMPING_BAND_COUNT))
    if not np.isfinite(damping).all():
        raise ValueError("damping curves contain non-finite values")
    if float(damping.min()) < 0.05 or float(damping.max()) > 0.98:
        raise ValueError("damping ratio range violated")

    volumes, absorption = build_axes()
    validate_edge_cases_recursive(matrix, volumes, absorption, EDGE_CASES, 0, report)
    report.insert(0, f"byteSize={actual_size} expected={EXPECTED_FILE_BYTES}")
    report.insert(1, f"payloadCrc32={payload_crc32:08X}")
    for material_index, material_name in enumerate(MATERIALS):
        values = " ".join(f"{float(value):.6f}" for value in damping[material_index])
        report.append(f"{material_name} damping[{values}]")
    return True, report


def parse_args(argv: Iterable[str]) -> argparse.Namespace:
    """Parse command-line arguments."""

    parser = argparse.ArgumentParser(description="Bake and validate HECTON-8 acoustic reverb LUTs.")
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT_PATH)
    parser.add_argument("--verify-only", action="store_true")
    return parser.parse_args(list(argv))


def main(argv: Iterable[str]) -> int:
    """CLI entry point."""

    args = parse_args(argv)
    if not args.verify_only:
        bake_info = bake_reverb_lut(args.output)
        print(
            "BAKE: "
            f"bytes={bake_info['bytes']} payloadCrc32={bake_info['payloadCrc32']} "
            f"elapsedUs={bake_info['elapsedUs']:.2f}"
        )

    _, report = validate_reverb_lut(args.output)
    for line in report:
        print(f"VALIDATE: {line}")
    print("STATUS: ACOUSTICS BAKED")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
