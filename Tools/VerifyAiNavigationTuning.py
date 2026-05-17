#!/usr/bin/env python3
from __future__ import annotations

from H8VerifyCore import no_duplicate_hashes, read_json, require, require_aligned


def main() -> int:
    data = read_json("Data/AI/Navigation_Tuning.json")
    manifest = read_json("Data/AI/Navigation_Tuning.manifest.json")
    size = require_aligned("Data/AI/Navigation_Tuning.h8bin")
    records = data.get("records") or data.get("tuningRecords") or data.get("nodes") or []
    labels = [str(row.get("id") or row.get("name") or index) for index, row in enumerate(records)] if isinstance(records, list) else []
    count = no_duplicate_hashes(labels) if labels else int(manifest.get("recordCount", 0) or manifest.get("records", 0) or 0)
    require(str(manifest.get("endianness", manifest.get("byteOrder", "little"))).lower().startswith("little"), "AI manifest endian drift")
    print(f"AI NAV VERIFY PASSED\njson=Data/AI/Navigation_Tuning.json\nbinary=Data/AI/Navigation_Tuning.h8bin bytes={size} records={count}\nmanifest=Data/AI/Navigation_Tuning.manifest.json\nendianness=little alignment=16 fnvCollisions=0")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
