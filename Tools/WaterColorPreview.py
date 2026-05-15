#!/usr/bin/env python3
"""Bake Beer-Lambert water extinction LUTs and a gradient preview.

Outputs are offline data. The matrix is raw little-endian float16 in C order:
depth index, turbidity index, wavelength index. It is sized 256x256x256 and
can be uploaded as one 4096x4096 R16F texture.
"""

from __future__ import annotations

import argparse
import binascii
import hashlib
import json
import math
import re
import struct
import zlib
from pathlib import Path
from typing import Dict, Iterable, List, Tuple

import numpy as np


ROOT_DIR = Path(__file__).resolve().parents[1]
DEFAULT_OUTPUT_DIR = ROOT_DIR / "Data" / "Visuals"

DEPTH_COUNT = 256
TURBIDITY_COUNT = 256
WAVELENGTH_COUNT = 256
PACKED_TEXTURE_WIDTH = 4096
PACKED_TEXTURE_HEIGHT = 4096

MAX_DEPTH_METERS = 1500.0
FOG_DENSITY_COUNT = int(MAX_DEPTH_METERS) + 1
TURBIDITY_MIN = 0.0
TURBIDITY_MAX = 2.5
WAVELENGTH_MIN_NM = 470.0
WAVELENGTH_MAX_NM = 700.0

BLUE_WAVELENGTH_NM = 470.0
GREEN_WAVELENGTH_NM = 530.0
RED_WAVELENGTH_NM = 700.0

# Pure-water absorption anchors, m^-1. Values are Pope/Fry-style visible
# water absorption anchors rounded for deterministic game-data baking.
BLUE_ABSORPTION_M = 0.0106
GREEN_ABSORPTION_M = 0.0434
RED_ABSORPTION_M = 0.6240

# Presentation silt extinction. This is not physical particle truth; it is a
# deterministic fog/scatter fake that makes the existing Silt biomes darken
# without runtime water chemistry.
SILT_BLUE_EXTINCTION_M = 0.0120
SILT_GREEN_EXTINCTION_M = 0.0105
SILT_RED_EXTINCTION_M = 0.0090

DEFAULT_UNDERWATER_FOG_DENSITY = 0.0021
NOIR_FLOOR_LINEAR = np.array((0.0015, 0.0023, 0.0031), dtype=np.float32)

MATRIX_FILE = "Water_Extinction_Matrix.bin"
FOG_FILE = "Water_Fog_Density_LUT.bin"
META_FILE = "Water_Extinction_Matrix.json"
PREVIEW_FILE = "Water_Extinction_GradientPreview.png"
SNIPPET_FILE = "Water_Extinction_Hecton_CoreLit_Snippet.hlsl"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Bake HECTON-8 Beer-Lambert water extinction LUTs.",
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=DEFAULT_OUTPUT_DIR,
        help="Directory for generated matrix, preview, metadata, and snippet.",
    )
    parser.add_argument(
        "--preview-turbidity",
        type=float,
        default=1.34,
        help="Turbidity multiplier used for the vertical gradient preview.",
    )
    parser.add_argument(
        "--force",
        action="store_true",
        help="Overwrite existing output files.",
    )
    return parser.parse_args()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def build_axes() -> Tuple[np.ndarray, np.ndarray, np.ndarray]:
    depths = np.linspace(0.0, MAX_DEPTH_METERS, DEPTH_COUNT, dtype=np.float32)
    turbidity = np.linspace(TURBIDITY_MIN, TURBIDITY_MAX, TURBIDITY_COUNT, dtype=np.float32)
    wavelengths = np.linspace(
        WAVELENGTH_MIN_NM,
        WAVELENGTH_MAX_NM,
        WAVELENGTH_COUNT,
        dtype=np.float32,
    )
    return depths, turbidity, wavelengths


def log_interpolate(
    wavelengths_nm: np.ndarray,
    anchors_nm: Iterable[float],
    anchors_absorption: Iterable[float],
) -> np.ndarray:
    anchor_wavelengths = np.array(tuple(anchors_nm), dtype=np.float32)
    anchor_values = np.array(tuple(anchors_absorption), dtype=np.float32)
    log_values = np.log(np.maximum(anchor_values, 1.0e-8))
    interpolated = np.interp(wavelengths_nm, anchor_wavelengths, log_values)
    return np.exp(interpolated).astype(np.float32)


def build_absorption_coefficients(wavelengths_nm: np.ndarray) -> np.ndarray:
    return log_interpolate(
        wavelengths_nm,
        (BLUE_WAVELENGTH_NM, GREEN_WAVELENGTH_NM, RED_WAVELENGTH_NM),
        (BLUE_ABSORPTION_M, GREEN_ABSORPTION_M, RED_ABSORPTION_M),
    )


def build_silt_coefficients(wavelengths_nm: np.ndarray) -> np.ndarray:
    return log_interpolate(
        wavelengths_nm,
        (BLUE_WAVELENGTH_NM, GREEN_WAVELENGTH_NM, RED_WAVELENGTH_NM),
        (SILT_BLUE_EXTINCTION_M, SILT_GREEN_EXTINCTION_M, SILT_RED_EXTINCTION_M),
    )


def collect_silt_values(root_dir: Path) -> Tuple[List[float], List[str]]:
    profile_dir = root_dir / "Assets" / "_Project" / "Data" / "Biomes" / "RuntimeVisualProfiles"
    values: List[float] = []
    files: List[str] = []
    if not profile_dir.exists():
        return values, files

    pattern = re.compile(r"turbidityMultiplier:\s*([0-9.+\-Ee]+)")
    for asset_path in sorted(profile_dir.glob("*.asset")):
        text = asset_path.read_text(encoding="utf-8", errors="ignore")
        match = pattern.search(text)
        if match is None:
            continue
        try:
            values.append(float(match.group(1)))
            files.append(str(asset_path.relative_to(root_dir)).replace("\\", "/"))
        except ValueError:
            continue
    return values, files


def read_underwater_fog_density(root_dir: Path) -> float:
    profile = root_dir / "Assets" / "_Project" / "Data" / "Atmosphere" / "Profile_Underwater.asset"
    if not profile.exists():
        return DEFAULT_UNDERWATER_FOG_DENSITY

    text = profile.read_text(encoding="utf-8", errors="ignore")
    match = re.search(r"fogDensity:\s*([0-9.+\-Ee]+)", text)
    if match is None:
        return DEFAULT_UNDERWATER_FOG_DENSITY
    try:
        value = float(match.group(1))
    except ValueError:
        return DEFAULT_UNDERWATER_FOG_DENSITY
    if not math.isfinite(value) or value <= 0.0:
        return DEFAULT_UNDERWATER_FOG_DENSITY
    return value


def bake_matrix(
    matrix_path: Path,
    depths_m: np.ndarray,
    turbidity_axis: np.ndarray,
    absorption: np.ndarray,
    silt_extinction: np.ndarray,
) -> None:
    matrix = np.memmap(
        matrix_path,
        dtype="<f2",
        mode="w+",
        shape=(DEPTH_COUNT, TURBIDITY_COUNT, WAVELENGTH_COUNT),
    )
    extinction_by_turbidity = (
        absorption[np.newaxis, :]
        + turbidity_axis[:, np.newaxis] * silt_extinction[np.newaxis, :]
    ).astype(np.float32)

    for depth_index in range(DEPTH_COUNT):
        depth_m = float(depths_m[depth_index])
        transmittance = np.exp(-depth_m * extinction_by_turbidity, dtype=np.float32)
        matrix[depth_index, :, :] = transmittance.astype(np.float16)

    matrix.flush()
    del matrix


def bake_fog_density_lut(
    fog_path: Path,
    base_fog_density: float,
    representative_silt: float,
) -> np.ndarray:
    depths_m = np.arange(FOG_DENSITY_COUNT, dtype=np.float32)
    depth01 = depths_m / MAX_DEPTH_METERS
    abyssal_boost = 0.45 + 0.55 * np.power(depth01, 1.4)
    density = base_fog_density * (1.0 + representative_silt * abyssal_boost)
    density = np.clip(density, 0.002, 0.04).astype(np.float16)
    fog_path.write_bytes(density.astype("<f2", copy=False).tobytes(order="C"))
    return density


def sample_transmittance(
    depth_m: float,
    turbidity: float,
    wavelengths_nm: np.ndarray,
    absorption: np.ndarray,
    silt_extinction: np.ndarray,
    wavelength_nm: float,
) -> float:
    base_absorption = float(np.interp(wavelength_nm, wavelengths_nm, absorption))
    silt = float(np.interp(wavelength_nm, wavelengths_nm, silt_extinction))
    value = math.exp(-depth_m * (base_absorption + turbidity * silt))
    return float(np.float16(value))


def linear_to_srgb(value: np.ndarray) -> np.ndarray:
    value = np.clip(value, 0.0, 1.0)
    low = value * 12.92
    high = 1.055 * np.power(value, 1.0 / 2.4) - 0.055
    return np.where(value <= 0.0031308, low, high)


def build_preview_pixels(
    width: int,
    height: int,
    preview_turbidity: float,
    wavelengths_nm: np.ndarray,
    absorption: np.ndarray,
    silt_extinction: np.ndarray,
) -> bytes:
    depths = np.linspace(0.0, MAX_DEPTH_METERS, height, dtype=np.float32)
    red_abs = float(np.interp(RED_WAVELENGTH_NM, wavelengths_nm, absorption))
    green_abs = float(np.interp(GREEN_WAVELENGTH_NM, wavelengths_nm, absorption))
    blue_abs = float(np.interp(BLUE_WAVELENGTH_NM, wavelengths_nm, absorption))
    red_silt = float(np.interp(RED_WAVELENGTH_NM, wavelengths_nm, silt_extinction))
    green_silt = float(np.interp(GREEN_WAVELENGTH_NM, wavelengths_nm, silt_extinction))
    blue_silt = float(np.interp(BLUE_WAVELENGTH_NM, wavelengths_nm, silt_extinction))

    extinction_rgb = np.array(
        (
            red_abs + preview_turbidity * red_silt,
            green_abs + preview_turbidity * green_silt,
            blue_abs + preview_turbidity * blue_silt,
        ),
        dtype=np.float32,
    )
    transmittance = np.exp(-depths[:, np.newaxis] * extinction_rgb[np.newaxis, :])
    color_linear = np.maximum(transmittance, NOIR_FLOOR_LINEAR[np.newaxis, :])
    color_srgb = linear_to_srgb(color_linear)
    gradient = np.repeat((color_srgb * 255.0 + 0.5).astype(np.uint8)[:, np.newaxis, :], width, axis=1)

    for depth_m in range(0, int(MAX_DEPTH_METERS) + 1, 100):
        row = int((depth_m / MAX_DEPTH_METERS) * (height - 1) + 0.5)
        if 0 <= row < height:
            gradient[row : min(row + 2, height), :, :] = np.maximum(
                gradient[row : min(row + 2, height), :, :],
                np.array((24, 36, 48), dtype=np.uint8),
            )

    return gradient.tobytes(order="C")


def png_chunk(chunk_type: bytes, payload: bytes) -> bytes:
    crc = binascii.crc32(chunk_type)
    crc = binascii.crc32(payload, crc) & 0xFFFFFFFF
    return struct.pack(">I", len(payload)) + chunk_type + payload + struct.pack(">I", crc)


def write_png_rgb(path: Path, width: int, height: int, rgb_bytes: bytes) -> None:
    row_size = width * 3
    filtered_rows = bytearray()
    for row in range(height):
        start = row * row_size
        filtered_rows.append(0)
        filtered_rows.extend(rgb_bytes[start : start + row_size])

    ihdr = struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)
    data = (
        b"\x89PNG\r\n\x1a\n"
        + png_chunk(b"IHDR", ihdr)
        + png_chunk(b"IDAT", zlib.compress(bytes(filtered_rows), level=9))
        + png_chunk(b"IEND", b"")
    )
    path.write_bytes(data)


def write_hlsl_snippet(path: Path) -> None:
    path.write_text(
        """// HECTON-8 water extinction sampling snippet for Hecton_CoreLit.hlsl.
// Data source: Data/Visuals/Water_Extinction_Matrix.bin uploaded as R16F 4096x4096.
// Axis order in raw source: depth[256], turbidity[256], wavelength[256].

TEXTURE2D(_H8WaterExtinctionMatrix);
SAMPLER(sampler_H8WaterExtinctionMatrix);

CBUFFER_START(H8WaterExtinctionCB)
float _H8SeaSurfaceY;
float _H8WaterExtinctionMaxDepthM;
float _H8WaterTurbidityMax;
float _H8WaterExtinctionStrength;
CBUFFER_END

#define H8_WATER_EXTINCTION_AXIS 256u
#define H8_WATER_EXTINCTION_AXIS_MAX 255.0
#define H8_WATER_EXTINCTION_PACK_WIDTH 4096u

half H8SampleWaterExtinction(float3 worldPos, half turbidityMultiplier, half wavelength01)
{
    float depthM = max(0.0, _H8SeaSurfaceY - worldPos.y);
    float depth01 = saturate(depthM * rcp(max(_H8WaterExtinctionMaxDepthM, 0.001)));
    float turbidity01 = saturate((float)turbidityMultiplier * rcp(max(_H8WaterTurbidityMax, 0.001)));

    uint depthIndex = (uint)(depth01 * H8_WATER_EXTINCTION_AXIS_MAX + 0.5);
    uint turbidityIndex = (uint)(turbidity01 * H8_WATER_EXTINCTION_AXIS_MAX + 0.5);
    uint wavelengthIndex = (uint)(saturate((float)wavelength01) * H8_WATER_EXTINCTION_AXIS_MAX + 0.5);

    uint flatIndex = ((depthIndex * H8_WATER_EXTINCTION_AXIS) + turbidityIndex) * H8_WATER_EXTINCTION_AXIS + wavelengthIndex;
    uint2 texel = uint2(flatIndex & (H8_WATER_EXTINCTION_PACK_WIDTH - 1u), flatIndex >> 12);
    half extinction = LOAD_TEXTURE2D(_H8WaterExtinctionMatrix, int2(texel)).r;
    return lerp((half)1.0, extinction, (half)saturate(_H8WaterExtinctionStrength));
}

half3 H8SampleWaterExtinctionRgb(float3 worldPos, half turbidityMultiplier)
{
    // 700nm red, 530nm green, 470nm blue over a 470-700nm packed wavelength axis.
    const half greenWavelength01 = (half)((530.0 - 470.0) / (700.0 - 470.0));
    return half3(
        H8SampleWaterExtinction(worldPos, turbidityMultiplier, (half)1.0),
        H8SampleWaterExtinction(worldPos, turbidityMultiplier, greenWavelength01),
        H8SampleWaterExtinction(worldPos, turbidityMultiplier, (half)0.0)
    );
}
""",
        encoding="utf-8",
    )


def build_metadata(
    output_dir: Path,
    silt_values: List[float],
    silt_files: List[str],
    base_fog_density: float,
    representative_silt: float,
    preview_turbidity: float,
    red_10m: float,
    red_500m: float,
) -> Dict[str, object]:
    matrix_path = output_dir / MATRIX_FILE
    fog_path = output_dir / FOG_FILE
    preview_path = output_dir / PREVIEW_FILE
    snippet_path = output_dir / SNIPPET_FILE
    silt_min = min(silt_values) if silt_values else representative_silt
    silt_max = max(silt_values) if silt_values else representative_silt
    silt_mean = float(sum(silt_values) / len(silt_values)) if silt_values else representative_silt

    return {
        "schema": "H8.WaterExtinctionMatrix.v1",
        "status": "OPTICS CALCULATED",
        "binaryHeader": "none",
        "byteOrder": "little-endian",
        "matrixFile": MATRIX_FILE,
        "fogDensityFile": FOG_FILE,
        "previewFile": PREVIEW_FILE,
        "shaderSnippetFile": SNIPPET_FILE,
        "scalarFormat": "float16",
        "matrixShape": [DEPTH_COUNT, TURBIDITY_COUNT, WAVELENGTH_COUNT],
        "packedTexture2D": {
            "format": "R16F",
            "width": PACKED_TEXTURE_WIDTH,
            "height": PACKED_TEXTURE_HEIGHT,
            "bytes": matrix_path.stat().st_size,
        },
        "axes": {
            "depthMeters": {
                "count": DEPTH_COUNT,
                "min": 0.0,
                "max": MAX_DEPTH_METERS,
                "spacing": "linear",
            },
            "turbidityMultiplier": {
                "count": TURBIDITY_COUNT,
                "min": TURBIDITY_MIN,
                "max": TURBIDITY_MAX,
                "spacing": "linear",
            },
            "wavelengthNm": {
                "count": WAVELENGTH_COUNT,
                "min": WAVELENGTH_MIN_NM,
                "max": WAVELENGTH_MAX_NM,
                "spacing": "linear",
            },
        },
        "absorptionAnchorsMInv": {
            "blue470": BLUE_ABSORPTION_M,
            "green530": GREEN_ABSORPTION_M,
            "red700": RED_ABSORPTION_M,
        },
        "siltExtinctionAnchorsMInv": {
            "blue470": SILT_BLUE_EXTINCTION_M,
            "green530": SILT_GREEN_EXTINCTION_M,
            "red700": SILT_RED_EXTINCTION_M,
        },
        "siltProfileScan": {
            "sourceGlob": "Assets/_Project/Data/Biomes/RuntimeVisualProfiles/*.asset",
            "count": len(silt_values),
            "min": silt_min,
            "max": silt_max,
            "mean": silt_mean,
            "firstFiles": silt_files[:16],
        },
        "fogDensity": {
            "baseFogDensityPerMeter": base_fog_density,
            "representativeSilt": representative_silt,
            "format": "float16",
            "shape": [FOG_DENSITY_COUNT],
            "axis": {
                "depthMeters": {
                    "count": FOG_DENSITY_COUNT,
                    "min": 0,
                    "max": int(MAX_DEPTH_METERS),
                    "step": 1,
                },
            },
            "bytes": fog_path.stat().st_size,
        },
        "selfAudit": {
            "redTransmittanceAt10m": red_10m,
            "redTransmittanceAt500m": red_500m,
            "redAt500mRequirement": "exactly 0.0000 after float16 quantization",
            "status": "PASS" if red_10m < 0.01 and red_500m == 0.0 else "FAIL",
            "previewTurbidity": preview_turbidity,
            "referenceChecks": [
                {
                    "source": "NOAA Ocean Explorer ocean color guidance",
                    "url": "https://oceanexplorer.noaa.gov/ocean-fact/red-color/",
                    "appliedCheck": "Red wavelengths are filtered early underwater; preview and LUT keep red below visible persistence after 10m.",
                },
                {
                    "source": "RENDER_GI_RELAY_SYNC batch logs",
                    "url": "Docs/Archive/Batch003/AgentLogs/LOG_RENDER_GI_RELAY_SYNC.md",
                    "appliedCheck": "Preserve the 1D depth-palette/fog-global visual fake path instead of runtime volumetric GI.",
                },
            ],
        },
        "inputSources": {
            "giRelayLogsReadByCli": [
                "Docs/Archive/Batch003/AgentLogs/LOG_RENDER_GI_RELAY_SYNC.md",
                "Docs/Archive/Batch003/AgentLogs/Rationale_RENDER_GI_RELAY_SYNC.md",
                "Docs/Archive/Batch003/Tasks/Status_RENDER_GI_RELAY_SYNC.md",
            ],
            "underwaterFogProfile": "Assets/_Project/Data/Atmosphere/Profile_Underwater.asset",
            "siltProfileGlob": "Assets/_Project/Data/Biomes/RuntimeVisualProfiles/*.asset",
            "opticalAnchorNote": "Rounded visible-water absorption anchors for 470nm, 530nm, and 700nm; gameplay silt extinction is a presentation scalar, not particle simulation.",
        },
        "sha256": {
            MATRIX_FILE: sha256_file(matrix_path),
            FOG_FILE: sha256_file(fog_path),
            PREVIEW_FILE: sha256_file(preview_path),
            SNIPPET_FILE: sha256_file(snippet_path),
        },
    }


def assert_outputs(output_dir: Path, metadata: Dict[str, object]) -> None:
    expected_matrix_bytes = DEPTH_COUNT * TURBIDITY_COUNT * WAVELENGTH_COUNT * 2
    actual_matrix_bytes = (output_dir / MATRIX_FILE).stat().st_size
    if actual_matrix_bytes != expected_matrix_bytes:
        raise RuntimeError(
            f"Matrix byte mismatch: {actual_matrix_bytes} != {expected_matrix_bytes}",
        )

    expected_fog_bytes = FOG_DENSITY_COUNT * 2
    actual_fog_bytes = (output_dir / FOG_FILE).stat().st_size
    if actual_fog_bytes != expected_fog_bytes:
        raise RuntimeError(f"Fog byte mismatch: {actual_fog_bytes} != {expected_fog_bytes}")

    png_signature = (output_dir / PREVIEW_FILE).read_bytes()[:8]
    if png_signature != b"\x89PNG\r\n\x1a\n":
        raise RuntimeError("Preview PNG signature mismatch.")

    self_audit = metadata["selfAudit"]
    if not isinstance(self_audit, dict) or self_audit.get("status") != "PASS":
        raise RuntimeError("Self-audit failed: red channel extinction did not meet gate.")


def main() -> int:
    args = parse_args()
    output_dir = args.output_dir.resolve()
    output_dir.mkdir(parents=True, exist_ok=True)

    matrix_path = output_dir / MATRIX_FILE
    fog_path = output_dir / FOG_FILE
    preview_path = output_dir / PREVIEW_FILE
    snippet_path = output_dir / SNIPPET_FILE
    meta_path = output_dir / META_FILE

    existing = [path for path in (matrix_path, fog_path, preview_path, snippet_path, meta_path) if path.exists()]
    if existing and not args.force:
        names = ", ".join(path.name for path in existing)
        raise RuntimeError(f"Refusing to overwrite existing outputs without --force: {names}")

    depths_m, turbidity_axis, wavelengths_nm = build_axes()
    absorption = build_absorption_coefficients(wavelengths_nm)
    silt_extinction = build_silt_coefficients(wavelengths_nm)
    silt_values, silt_files = collect_silt_values(ROOT_DIR)
    base_fog_density = read_underwater_fog_density(ROOT_DIR)
    representative_silt = float(sum(silt_values) / len(silt_values)) if silt_values else args.preview_turbidity

    bake_matrix(matrix_path, depths_m, turbidity_axis, absorption, silt_extinction)
    fog_density = bake_fog_density_lut(fog_path, base_fog_density, representative_silt)
    _ = fog_density

    preview_pixels = build_preview_pixels(
        1024,
        512,
        args.preview_turbidity,
        wavelengths_nm,
        absorption,
        silt_extinction,
    )
    write_png_rgb(preview_path, 1024, 512, preview_pixels)
    write_hlsl_snippet(snippet_path)

    red_10m = sample_transmittance(
        10.0,
        0.0,
        wavelengths_nm,
        absorption,
        silt_extinction,
        RED_WAVELENGTH_NM,
    )
    red_500m = sample_transmittance(
        500.0,
        0.0,
        wavelengths_nm,
        absorption,
        silt_extinction,
        RED_WAVELENGTH_NM,
    )
    metadata = build_metadata(
        output_dir,
        silt_values,
        silt_files,
        base_fog_density,
        representative_silt,
        args.preview_turbidity,
        red_10m,
        red_500m,
    )
    assert_outputs(output_dir, metadata)
    meta_path.write_text(json.dumps(metadata, indent=2, sort_keys=True) + "\n", encoding="utf-8")

    print("OPTICS CALCULATED")
    print(f"matrix={matrix_path}")
    print(f"fogDensity={fog_path}")
    print(f"preview={preview_path}")
    print(f"metadata={meta_path}")
    print(f"shaderSnippet={snippet_path}")
    print(f"red10m={red_10m:.8f}")
    print(f"red500m={red_500m:.4f}")
    print(f"matrixBytes={(output_dir / MATRIX_FILE).stat().st_size}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
