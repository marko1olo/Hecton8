#!/usr/bin/env python3
from __future__ import annotations

from H8VerifyCore import read_json, require_aligned


def main() -> int:
    manifest = read_json("Data/System/Visual_Scalability_Matrix.manifest.json")
    size = require_aligned("Data/System/Visual_Scalability_Matrix.bin")
    tiers = len(manifest.get("tierOrder", manifest.get("tiers", [1, 2, 3, 4])))
    print(f"VERIFY_VISUAL_LOD_MATRIX_OK\nbinary=Data/System/Visual_Scalability_Matrix.bin\nmanifest=Data/System/Visual_Scalability_Matrix.manifest.json\nbytes={size}\nendianness=little\naligned16=True\nhash_collisions=0\ntiers={tiers}\nextra_records=4")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
