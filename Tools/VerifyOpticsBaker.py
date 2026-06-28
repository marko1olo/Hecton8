#!/usr/bin/env python3
"""Independent static verifier for the water extinction LUT."""

from __future__ import annotations

import argparse
import json
import struct
from pathlib import Path
from typing import Any

import numpy as np


ROOT = Path(__file__).resolve().parents[1]
DATA_DIR = ROOT / "Data" / "Visuals"
DEFAULT_REPORT = ROOT / "Docs" / "AgentLogs" / "OpticsVerification_OPTICAL_EXTINCTION_LUT_BAKER.json"
MATRIX = DATA_DIR / "Water_Extinction_Matrix.bin"
MANIFEST = DATA_DIR / "Water_Extinction_Matrix.json"
HALF_DTYPE = np.dtype("<f2")
HALF_PACK = "<e"
SHAPE = (256, 256, 256)
EXPECTED_BYTES = 393216


def rel(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return path.as_posix()


def verify() -> dict[str, Any]:
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    matrix_bytes = MATRIX.stat().st_size
    if matrix_bytes != EXPECTED_BYTES:
        raise AssertionError(f"{rel(MATRIX)} bytes={matrix_bytes} expected={EXPECTED_BYTES}")
    matrix = np.fromfile(MATRIX, dtype=HALF_DTYPE).reshape((256, 256, 3))
    red_500 = float(matrix[255, 0, 0])
    blue_500 = float(matrix[255, 0, 2])
    if red_500 != 0.0:
        raise AssertionError("red at 500m is not exactly 0.0")
    if blue_500 <= 0.0:
        raise AssertionError("blue at 500m did not survive")
    raw = MATRIX.read_bytes()
    blue_offset = ((255 * 256 * 3) + 2) * 2
    if raw[blue_offset : blue_offset + 2] != struct.pack(HALF_PACK, blue_500):
        raise AssertionError("matrix byte packing is not little-endian <e")
    hashes = manifest.get("artifactHashes", [])
    values = [int(row.get("fnv1a32AsciiLower", -1)) for row in hashes]
    collisions = len(values) - len(set(values))
    if collisions:
        raise AssertionError(f"FNV collisions={collisions}")
    if manifest.get("dataSovereignty", {}).get("lookupModel") != "stateless_binary_lookup":
        raise AssertionError("dataSovereignty.lookupModel must be stateless_binary_lookup")
    if "10.1364/AO.36.008710" not in json.dumps(manifest, sort_keys=True):
        raise AssertionError("missing Pope/Fry DOI provenance")
    return {
        "status": "OPTICS_LUT_VERIFIED",
        "matrix": rel(MATRIX),
        "matrixBytes": matrix_bytes,
        "expectedBytes": EXPECTED_BYTES,
        "aligned16": matrix_bytes % 16 == 0,
        "byteOrder": "little-endian",
        "pack": HALF_PACK,
        "shape": list(SHAPE),
        "redAt500mClear": red_500,
        "blueAt500mClear": blue_500,
        "fnvCollisions": collisions,
        "dataSovereignty": manifest["dataSovereignty"]["lookupModel"],
        "sourceReferences": manifest.get("sourceReferences", []),
        "coefficientProvenance": manifest.get("coefficientProvenance", []),
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Verify HECTON-8 water extinction LUT.")
    parser.add_argument("--report", default=str(DEFAULT_REPORT))
    args = parser.parse_args()
    report = verify()
    report_path = Path(args.report)
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(f"VERIFY_OPTICS: status={report['status']}")
    print(f"VERIFY_OPTICS: matrixBytes={report['matrixBytes']}")
    print(f"VERIFY_OPTICS: aligned16={report['aligned16']}")
    print(f"VERIFY_OPTICS: byteOrder={report['byteOrder']}")
    print(f"VERIFY_OPTICS: pack={report['pack']}")
    print(f"VERIFY_OPTICS: fnvCollisions={report['fnvCollisions']}")
    print(f"VERIFY_OPTICS: dataSovereignty={report['dataSovereignty']}")
    print(f"VERIFY_OPTICS: report={rel(report_path)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
