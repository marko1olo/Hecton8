#!/usr/bin/env python3
from __future__ import annotations

import sys

from VisualLodMatrixBaker import BINARY_PATH, MANIFEST_PATH, SOURCE_JSON, MatrixError, verify_existing


def main() -> int:
    try:
        manifest = verify_existing(SOURCE_JSON, BINARY_PATH, MANIFEST_PATH)
    except (OSError, MatrixError, KeyError, TypeError, ValueError) as exc:
        print(f"VERIFY_VISUAL_LOD_MATRIX_FAIL: {exc}", file=sys.stderr)
        return 1

    tiers = manifest["sections"]["tierRecords"]["count"]
    extra_records = manifest["sections"]["extraRecords"]["count"]
    print("VERIFY_VISUAL_LOD_MATRIX_OK")
    print("binary=Data/System/Visual_Scalability_Matrix.bin")
    print("manifest=Data/System/Visual_Scalability_Matrix.manifest.json")
    print(f"bytes={manifest['fileBytes']}")
    print(f"endianness={manifest['endianness']}")
    print(f"aligned16={manifest['fileAligned16']}")
    print(f"hash_collisions={manifest['fnv1a32']['collisionCount']}")
    print(f"tiers={tiers}")
    print(f"extra_records={extra_records}")
    print(f"god_mode_density_ratio_vs_pro={manifest['stressAudit']['godModeDensityRatioVsPro']:.3f}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
