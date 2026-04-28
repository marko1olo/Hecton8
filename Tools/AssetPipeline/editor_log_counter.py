#!/usr/bin/env python3
import argparse
import json
from pathlib import Path


PATTERNS = {
    "material_location_obsolete": "MaterialLocation.External is obsolete",
    "missing_normals_tangent_warning": "doesn't contain normals. Can't calculate tangents",
    "missing_prefab_variant_parent": "Missing Prefab Variant parent",
    "bakery_warning": "[Bakery warning]",
    "self_intersecting_polygon": "self-intersecting",
}


def scan_log(log_path: Path) -> dict:
    text = log_path.read_text(encoding="utf-8", errors="ignore")
    counts = {}
    for key, pattern in PATTERNS.items():
        counts[key] = text.count(pattern)
    counts["path"] = str(log_path)
    return counts


def main() -> int:
    parser = argparse.ArgumentParser(description="Count targeted HECTON-8 asset-pipeline warning patterns in a Unity editor log.")
    parser.add_argument("log_path", type=Path, help="Path to Editor.log or captured Unity log.")
    parser.add_argument("--json", action="store_true", dest="emit_json", help="Emit JSON instead of plain text.")
    args = parser.parse_args()

    counts = scan_log(args.log_path)
    if args.emit_json:
        print(json.dumps(counts, indent=2))
        return 0

    print(f"Editor log scan: {counts['path']}")
    for key in PATTERNS:
        print(f"{key}={counts[key]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
