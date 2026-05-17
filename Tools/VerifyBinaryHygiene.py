#!/usr/bin/env python3
"""Verify repository binary blobs are 16-byte aligned."""

from __future__ import annotations

import argparse
import json
import os
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
ALIGNMENT_BYTES = 16
EXCLUDED_PARTS = {"Build", "Builds", "Library", "Obj", "Temp", "__pycache__"}
EXCLUDED_PREFIXES = (".git/", ".codex-artifacts/", ".codex-build/", ".codexbuild/")


def is_excluded(relative: Path) -> bool:
    parts = set(relative.parts)
    if parts & EXCLUDED_PARTS:
        return True
    normalized = relative.as_posix()
    return any(normalized.startswith(prefix) for prefix in EXCLUDED_PREFIXES)


def collect_records(root: Path) -> list[dict[str, object]]:
    records: list[dict[str, object]] = []
    for dirpath, dirnames, filenames in os.walk(root):
        current = Path(dirpath)
        relative_dir = current.relative_to(root)
        dirnames[:] = [name for name in dirnames if name not in EXCLUDED_PARTS and not is_excluded(relative_dir / name)]
        for filename in filenames:
            path = current / filename
            if path.suffix.lower() not in {".bin", ".h8bin"} and ".bin" not in path.name.lower():
                continue
            relative = path.relative_to(root)
            if is_excluded(relative):
                continue
            size = path.stat().st_size
            remainder = size % ALIGNMENT_BYTES
            records.append({"path": relative.as_posix(), "bytes": size, "alignmentRemainder": remainder, "aligned16": remainder == 0})
    return sorted(records, key=lambda item: str(item["path"]))


def main() -> None:
    parser = argparse.ArgumentParser(description="Verify .bin/.h8bin alignment.")
    parser.add_argument("--report", default="Docs/AgentLogs/BinaryHygiene_CRAFTING_COST_BALANCER.json")
    args = parser.parse_args()
    records = collect_records(ROOT)
    misaligned = [record for record in records if not record["aligned16"]]
    report = {
        "status": "BINARY_HYGIENE_VERIFIED" if not misaligned else "BINARY_HYGIENE_FAILED",
        "alignmentBytes": ALIGNMENT_BYTES,
        "binaryCount": len(records),
        "misalignedCount": len(misaligned),
        "misaligned": misaligned,
        "records": records,
        "excludedParts": sorted(EXCLUDED_PARTS),
        "excludedPrefixes": list(EXCLUDED_PREFIXES),
    }
    report_path = ROOT / args.report
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(report, indent=2, sort_keys=True), encoding="utf-8")
    print(f"VERIFY_BINARY_HYGIENE: status={report['status']}")
    print(f"VERIFY_BINARY_HYGIENE: binaryCount={len(records)}")
    print(f"VERIFY_BINARY_HYGIENE: misalignedCount={len(misaligned)}")
    print(f"VERIFY_BINARY_HYGIENE: report={args.report.replace(chr(92), '/')}")
    if misaligned:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
