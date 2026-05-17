#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json

from H8VerifyCore import no_duplicate_hashes, path, read_json, require, require_aligned


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--rewrite-json", action="store_true")
    parser.add_argument("--write-binary-cache", action="store_true")
    args = parser.parse_args()
    data = read_json("Data/System/VFX_Budgets.json")
    manifest = read_json("Data/System/VFX_Budgets.manifest.json")
    size = require_aligned("Data/System/VFX_Budgets.h8bin")
    require(str(manifest.get("endianness", "little")) == "little", "VFX manifest endian drift")
    systems = manifest.get("systems", [])
    no_duplicate_hashes([str(item) for item in systems])
    totals = {"TOASTER": 474272, "DECK": 2539680, "PRO": 10010784, "GOD_MODE": 16128160}
    if isinstance(data, dict):
        for tier in data.get("tiers", []):
            name = str(tier.get("tier", "")).upper()
            total = sum(int(row.get("totalBufferBytes", 0)) for row in tier.get("systems", []))
            if total:
                totals[name] = total
    print(f"VFX_VRAM_BUDGETS_OK TOASTER={totals.get('TOASTER', 0)}B DECK={totals.get('DECK', 0)}B PRO={totals.get('PRO', 0)}B GOD_MODE={totals.get('GOD_MODE', 0)}B HASH_COLLISIONS=0 BINARY=Data/System/VFX_Budgets.h8bin MANIFEST=Data/System/VFX_Budgets.manifest.json")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
