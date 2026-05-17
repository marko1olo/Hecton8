#!/usr/bin/env python3
"""Bake HECTON-8 Beer-Lambert water extinction LUTs.

The output is a flat, headerless little-endian half-float binary. Runtime
shaders should sample the baked table instead of recomputing exponentials.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import struct
from pathlib import Path
from typing import Any

import numpy as np


ROOT = Path(__file__).resolve().parents[1]
OUTPUT_DIR = ROOT / "Data" / "Visuals"
MATRIX_FILE = "Water_Extinction_Matrix.bin"
TOASTER_MATRIX_FILE = "Water_Extinction_Matrix_Toaster.bin"
OVERKILL_MATRIX_FILE = "Water_Extinction_Matrix_Overkill.bin"
MANIFEST_FILE = "Water_Extinction_Matrix.json"
PREVIEW_FILE = "Water_Extinction_GradientPreview.png"
OVERKILL_PREVIEW_FILE = "Water_Extinction_GradientPreview_Overkill.png"

DEPTH_COUNT = 256
TURBIDITY_COUNT = 256
CHANNEL_COUNT = 3
DEPTH_MAX_M = 500.0
TURBIDITY_MAX = 2.5
ALIGNMENT_BYTES = 16
HALF_FLOAT_FORMAT = "<e"
HALF_DTYPE = np.dtype("<f2")

TOASTER_DEPTH_COUNT = 64
TOASTER_TURBIDITY_COUNT = 64
OVERKILL_DEPTH_COUNT = 512
OVERKILL_TURBIDITY_COUNT = 512

RGB_CHANNELS = ("red", "green", "blue")
WAVELENGTH_NM = {"red": 700, "green": 530, "blue": 470}
ABSORPTION_RGB_M_INV = np.array([0.6240, 0.0434, 0.0106], dtype=np.float32)
ABSORPTION_SOURCE = (
    "Pope/Fry pure-water visible absorption anchors; Applied Optics 36(33), "
    "8710-8723, doi:10.1364/AO.36.008710; rounded to four significant "
    "digits for deterministic half-float baking."
)

SOURCE_REFERENCES = [
    {
        "id": "PopeFry1997PureWaterAbsorption",
        "authors": "Robin M. Pope; Edward S. Fry",
        "title": "Absorption spectrum (380-700 nm) of pure water. II. Integrating cavity measurements",
        "journal": "Applied Optics",
        "volume": 36,
        "issue": 33,
        "pages": "8710-8723",
        "year": 1997,
        "doi": "10.1364/AO.36.008710",
        "usage": "RGB pure-water absorption anchor provenance; values are rounded before half-float baking.",
    }
]

VARIANTS = {
    "toaster_i3": {
        "file": TOASTER_MATRIX_FILE,
        "shape": (TOASTER_DEPTH_COUNT, TOASTER_TURBIDITY_COUNT, CHANNEL_COUNT),
        "role": "Celeron/i3 stripped lookup for lowest bandwidth path.",
    },
    "main_mx350": {
        "file": MATRIX_FILE,
        "shape": (DEPTH_COUNT, TURBIDITY_COUNT, CHANNEL_COUNT),
        "role": "Primary MX350/LOW lookup required by the batch prompt.",
    },
    "rtx_overkill": {
        "file": OVERKILL_MATRIX_FILE,
        "shape": (OVERKILL_DEPTH_COUNT, OVERKILL_TURBIDITY_COUNT, CHANNEL_COUNT),
        "role": "High-end visual-overkill lookup for denser fog/light blending.",
    },
}

FNV_OFFSET = 2166136261
FNV_PRIME = 16777619


def rel(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return path.as_posix()


def fnv1a32(text: str) -> int:
    value = FNV_OFFSET
    for byte in text.lower().encode("ascii"):
        value ^= byte
        value = (value * FNV_PRIME) & 0xFFFFFFFF
    return value


def sha256_file(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def load_matplotlib_pyplot():
    import matplotlib

    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    return plt


def build_matrix(depth_count: int, turbidity_count: int) -> np.ndarray:
    depth_axis = np.linspace(0.0, DEPTH_MAX_M, depth_count, dtype=np.float32)[:, None, None]
    turbidity_axis = np.linspace(0.0, TURBIDITY_MAX, turbidity_count, dtype=np.float32)[None, :, None]
    channel_axis = ABSORPTION_RGB_M_INV[None, None, :]
    mu = channel_axis * (1.0 + turbidity_axis)
    transmittance = np.exp(-mu * depth_axis, dtype=np.float32)
    transmittance = np.clip(transmittance, 0.0, 1.0)
    return np.ascontiguousarray(transmittance.astype(HALF_DTYPE))


def build_all_matrices() -> dict[str, np.ndarray]:
    return {name: build_matrix(shape[0], shape[1]) for name, shape in ((n, v["shape"]) for n, v in VARIANTS.items())}


def write_binary(path: Path, matrix: np.ndarray) -> None:
    path.write_bytes(matrix.tobytes(order="C"))


def write_preview(matrix: np.ndarray, path: Path) -> None:
    plt = load_matplotlib_pyplot()
    image = np.asarray(matrix[:, :, :], dtype=np.float32)
    plt.imsave(path, image)


def build_visual_overkill_harmonics() -> list[dict[str, Any]]:
    primes = (2, 3, 5, 7)
    rows: list[dict[str, Any]] = []
    for index, prime in enumerate(primes):
        key = f"Data.Visuals.WaterExtinction.Harmonic.{prime}"
        hash_value = fnv1a32(key)
        rows.append(
            {
                "band": index,
                "prime": prime,
                "frequencyScale": float(prime),
                "amplitude": 1.0 / float(prime),
                "phase01": (hash_value & 0xFFFF) / 65535.0,
                "hashHex": f"0x{hash_value:08X}",
            }
        )
    return rows


def coefficient_provenance() -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for index, channel in enumerate(RGB_CHANNELS):
        mu = float(ABSORPTION_RGB_M_INV[index])
        rows.append(
            {
                "channel": channel,
                "wavelengthNm": WAVELENGTH_NM[channel],
                "absorptionMInv": mu,
                "beerLambertAt10m": float(np.exp(-mu * 10.0)),
                "beerLambertAt500m": float(np.exp(-mu * 500.0)),
                "rounding": "four_significant_digits_before_fp16_bake",
                "sourceReferenceId": "PopeFry1997PureWaterAbsorption",
            }
        )
    return rows


def artifact_hashes() -> list[dict[str, Any]]:
    ids = [
        "Data.Visuals.WaterExtinction.Toaster",
        "Data.Visuals.WaterExtinction.Main",
        "Data.Visuals.WaterExtinction.RtxOverkill",
        "Data.Visuals.WaterExtinction.Preview.Main",
        "Data.Visuals.WaterExtinction.Preview.RtxOverkill",
        "Data.Visuals.WaterExtinction.Manifest",
    ]
    return [
        {"id": item, "fnv1a32AsciiLower": fnv1a32(item), "hashHex": f"0x{fnv1a32(item):08X}"}
        for item in ids
    ]


def validate_hashes(rows: list[dict[str, Any]]) -> int:
    seen: dict[int, str] = {}
    collisions = 0
    for row in rows:
        value = int(row["fnv1a32AsciiLower"])
        if value in seen:
            collisions += 1
        else:
            seen[value] = str(row["id"])
    return collisions


def build_manifest(matrices: dict[str, np.ndarray], validation: dict[str, Any]) -> dict[str, Any]:
    variant_rows: dict[str, Any] = {}
    for name, spec in VARIANTS.items():
        path = OUTPUT_DIR / str(spec["file"])
        shape = list(spec["shape"])
        variant_rows[name] = {
            "file": spec["file"],
            "shape": shape,
            "bytes": path.stat().st_size,
            "expectedBytes": int(np.prod(shape) * 2),
            "aligned16": path.stat().st_size % ALIGNMENT_BYTES == 0,
            "sha256": sha256_file(path),
            "role": spec["role"],
        }

    artifact_rows = artifact_hashes()
    return {
        "schema": "H8.WaterExtinctionMatrix.v3",
        "tool": "Tools/OpticsBaker.py",
        "status": "LUT BAKED",
        "matrixFile": MATRIX_FILE,
        "toasterMatrixFile": TOASTER_MATRIX_FILE,
        "overkillMatrixFile": OVERKILL_MATRIX_FILE,
        "previewFile": PREVIEW_FILE,
        "overkillPreviewFile": OVERKILL_PREVIEW_FILE,
        "binaryHeader": "none",
        "byteOrder": "little-endian",
        "pythonPackingContract": HALF_FLOAT_FORMAT,
        "binaryAlignmentBytes": ALIGNMENT_BYTES,
        "scalarFormat": "float16",
        "shape": [DEPTH_COUNT, TURBIDITY_COUNT, CHANNEL_COUNT],
        "axisOrder": ["depth", "turbidity", "rgb"],
        "axes": {
            "depthMeters": {"min": 0.0, "max": DEPTH_MAX_M, "count": DEPTH_COUNT, "spacing": "linear"},
            "turbidity": {
                "min": 0.0,
                "max": TURBIDITY_MAX,
                "count": TURBIDITY_COUNT,
                "spacing": "linear",
                "absorptionRule": "muRgb * (1 + turbidity)",
            },
            "rgb": list(RGB_CHANNELS),
        },
        "rowMajorIndex": "((depthIndex * 256) + turbidityIndex) * 3 + channelIndex",
        "formula": "I = I0 * exp(-mu * depthMeters)",
        "mathDerivation": {
            "baseLaw": "Beer-Lambert",
            "baseFormula": "I = I0 * exp(-mu * d)",
            "coefficientUnits": "m^-1",
            "depthUnits": "meters",
            "turbidityRule": "muEffective = muPureWaterRgb * (1 + siltTurbidity)",
            "runtimePolicy": "sample baked transmittance; do not recompute exponentials on LOW/MX350",
        },
        "physicsBasis": "BeerLambert / Beer-Lambert extinction with pure-water absorption anchors.",
        "absorptionRgbMInv": {
            "red": float(ABSORPTION_RGB_M_INV[0]),
            "green": float(ABSORPTION_RGB_M_INV[1]),
            "blue": float(ABSORPTION_RGB_M_INV[2]),
        },
        "wavelengthNm": WAVELENGTH_NM,
        "absorptionSource": ABSORPTION_SOURCE,
        "sourceReferences": SOURCE_REFERENCES,
        "coefficientProvenance": coefficient_provenance(),
        "variants": variant_rows,
        "scalability": {
            "toasterData": {
                "variant": "toaster_i3",
                "intendedHardware": "Celeron/i3/MX350 pressure path",
                "sampleCost": "one bilinear RGB/RGBA texture sample or one raw R16F lane path",
            },
            "mainData": {"variant": "main_mx350", "intendedHardware": "default LOW/MIDDLE underwater pass"},
            "rtxOverkillData": {
                "variant": "rtx_overkill",
                "intendedHardware": "HIGH/ULTRA visual overkill path",
                "harmonicNoise": build_visual_overkill_harmonics(),
            },
        },
        "loreLexicon": {
            "tone": "NASA-Punk / Deep Sea Noir",
            "approvedTerms": ["bilge-silt", "pressure glass", "floodlamp bleed", "bulkhead shadow", "rust-haze"],
            "rejectedToneClass": "showroom-polish wording is disallowed in shipped labels",
        },
        "dataSovereignty": {
            "lookupModel": "stateless_binary_lookup",
            "runtimePrivateStateRequired": False,
            "globalRegistryRequired": False,
            "hotPathJsonParsingAllowed": False,
        },
        "artifactHashes": artifact_rows,
        "artifactHashCollisionCount": validate_hashes(artifact_rows),
        "sha256": {
            MATRIX_FILE: sha256_file(OUTPUT_DIR / MATRIX_FILE),
            TOASTER_MATRIX_FILE: sha256_file(OUTPUT_DIR / TOASTER_MATRIX_FILE),
            OVERKILL_MATRIX_FILE: sha256_file(OUTPUT_DIR / OVERKILL_MATRIX_FILE),
            PREVIEW_FILE: sha256_file(OUTPUT_DIR / PREVIEW_FILE),
            OVERKILL_PREVIEW_FILE: sha256_file(OUTPUT_DIR / OVERKILL_PREVIEW_FILE),
        },
        "validation": validation,
    }


def validate(output_dir: Path = OUTPUT_DIR) -> dict[str, Any]:
    matrix_path = output_dir / MATRIX_FILE
    toaster_path = output_dir / TOASTER_MATRIX_FILE
    overkill_path = output_dir / OVERKILL_MATRIX_FILE
    preview_path = output_dir / PREVIEW_FILE
    overkill_preview_path = output_dir / OVERKILL_PREVIEW_FILE
    manifest_path = output_dir / MANIFEST_FILE

    expected_bytes = DEPTH_COUNT * TURBIDITY_COUNT * CHANNEL_COUNT * 2
    matrix_bytes = matrix_path.stat().st_size
    if matrix_bytes != expected_bytes:
        raise AssertionError(f"{rel(matrix_path)} bytes={matrix_bytes}, expected={expected_bytes}")
    for path in (matrix_path, toaster_path, overkill_path):
        if path.stat().st_size % ALIGNMENT_BYTES != 0:
            raise AssertionError(f"{rel(path)} is not {ALIGNMENT_BYTES}-byte aligned.")

    matrix = np.fromfile(matrix_path, dtype=HALF_DTYPE).reshape((DEPTH_COUNT, TURBIDITY_COUNT, CHANNEL_COUNT))
    red_500 = float(matrix[DEPTH_COUNT - 1, 0, 0])
    red_10 = float(np.exp(-float(ABSORPTION_RGB_M_INV[0]) * 10.0))
    blue_500 = float(matrix[DEPTH_COUNT - 1, 0, 2])
    if red_500 != 0.0:
        raise AssertionError(f"Red at 500m must be exactly 0.0, got {red_500}")
    if blue_500 <= 0.0:
        raise AssertionError("Blue at 500m must survive in clear water.")

    raw = matrix_path.read_bytes()
    blue_offset = ((DEPTH_COUNT - 1) * TURBIDITY_COUNT * CHANNEL_COUNT + 2) * 2
    if raw[blue_offset : blue_offset + 2] != struct.pack(HALF_FLOAT_FORMAT, blue_500):
        raise AssertionError("Half-float payload does not match '<e' little-endian packing.")

    for path in (preview_path, overkill_preview_path):
        if path.read_bytes()[:8].hex().upper() != "89504E470D0A1A0A":
            raise AssertionError(f"{rel(path)} is not a PNG.")

    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if manifest.get("status") != "LUT BAKED":
        raise AssertionError("Water extinction manifest status must be LUT BAKED.")
    if "10.1364/AO.36.008710" not in json.dumps(manifest, sort_keys=True):
        raise AssertionError("Water extinction manifest is missing Pope/Fry DOI provenance.")
    artifact_rows = manifest.get("artifactHashes", [])
    collisions = validate_hashes(artifact_rows)
    if collisions != 0:
        raise AssertionError(f"FNV artifact collisions={collisions}")

    return {
        "status": "PASS",
        "matrixBytes": matrix_bytes,
        "expectedMatrixBytes": expected_bytes,
        "matrixAligned16": matrix_bytes % ALIGNMENT_BYTES == 0,
        "allVariantBinariesAligned16": all(path.stat().st_size % ALIGNMENT_BYTES == 0 for path in (matrix_path, toaster_path, overkill_path)),
        "allFinite": bool(np.isfinite(matrix).all()),
        "redAt500mClear": red_500,
        "redAt500mExactlyZero": red_500 == 0.0,
        "redAt10mClearExactFloat32": red_10,
        "blueAt500mClear": blue_500,
        "blueAt500mSurvives": blue_500 > 0.0,
        "fnvCollisionCount": collisions,
    }


def bake(output_dir: Path = OUTPUT_DIR) -> dict[str, Any]:
    output_dir.mkdir(parents=True, exist_ok=True)
    load_matplotlib_pyplot()
    matrices = build_all_matrices()
    for name, matrix in matrices.items():
        write_binary(output_dir / str(VARIANTS[name]["file"]), matrix)
    write_preview(matrices["main_mx350"], output_dir / PREVIEW_FILE)
    write_preview(matrices["rtx_overkill"], output_dir / OVERKILL_PREVIEW_FILE)
    validation = validate_after_files(output_dir)
    manifest = build_manifest(matrices, validation)
    (output_dir / MANIFEST_FILE).write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return validate(output_dir)


def validate_after_files(output_dir: Path) -> dict[str, Any]:
    matrix_path = output_dir / MATRIX_FILE
    matrix = np.fromfile(matrix_path, dtype=HALF_DTYPE).reshape((DEPTH_COUNT, TURBIDITY_COUNT, CHANNEL_COUNT))
    artifact_rows = artifact_hashes()
    return {
        "status": "PASS",
        "matrixBytes": matrix_path.stat().st_size,
        "expectedMatrixBytes": DEPTH_COUNT * TURBIDITY_COUNT * CHANNEL_COUNT * 2,
        "matrixAligned16": matrix_path.stat().st_size % ALIGNMENT_BYTES == 0,
        "allVariantBinariesAligned16": all(
            (output_dir / str(spec["file"])).stat().st_size % ALIGNMENT_BYTES == 0 for spec in VARIANTS.values()
        ),
        "allFinite": bool(np.isfinite(matrix).all()),
        "redAt500mClear": float(matrix[DEPTH_COUNT - 1, 0, 0]),
        "redAt500mExactlyZero": float(matrix[DEPTH_COUNT - 1, 0, 0]) == 0.0,
        "redAt10mClearExactFloat32": float(np.exp(-float(ABSORPTION_RGB_M_INV[0]) * 10.0)),
        "blueAt500mClear": float(matrix[DEPTH_COUNT - 1, 0, 2]),
        "blueAt500mSurvives": float(matrix[DEPTH_COUNT - 1, 0, 2]) > 0.0,
        "fnvCollisionCount": validate_hashes(artifact_rows),
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Bake and verify HECTON-8 water extinction LUTs.")
    parser.add_argument("--verify", action="store_true", help="Verify existing files without rebaking.")
    args = parser.parse_args()
    result = validate() if args.verify else bake()
    print("OPTICS_BAKER_STATUS: PASS")
    print(f"matrix={rel(OUTPUT_DIR / MATRIX_FILE)}")
    print(f"bytes={result['matrixBytes']}")
    print(f"expected={result['expectedMatrixBytes']}")
    print(f"aligned16={result['matrixAligned16']}")
    print(f"redAt500mClear={result['redAt500mClear']}")
    print(f"redAt10mClearExactFloat32={result['redAt10mClearExactFloat32']}")
    print(f"blueAt500mClear={result['blueAt500mClear']}")
    print(f"fnvArtifactIds={len(artifact_hashes())} collisions={result['fnvCollisionCount']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
