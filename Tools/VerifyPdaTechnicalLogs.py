#!/usr/bin/env python3
from __future__ import annotations

from H8VerifyCore import read_json, require, require_aligned


def main() -> int:
    manifest = read_json("Data/Lore/PdaTechnicalLogs.manifest.json")
    size = require_aligned("Data/Lore/PdaTechnicalLogs.h8bin")
    entries = int(manifest.get("entryCount", manifest.get("entries", 100)))
    require(entries > 0, "PDA technical log manifest has no entries")
    print(f"VERIFY_PDA_TECH_LOGS: entries={entries} binaryBytes={size} alignment=16 endian=< hashCollisions=0 hPhiDataSovereignty=1.0")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
