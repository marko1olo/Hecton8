#!/usr/bin/env python3
"""Generate an inventory icon binding map from gap-audit/spec targets and baked Alpha512 names."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import subprocess
import sys


ROOT = Path(__file__).resolve().parents[1]


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def run_gap_spec(binding_map: Path, limit: int) -> dict:
    command = [
        sys.executable,
        "-B",
        str(ROOT / "Tools/InventoryIconGapAudit.py"),
        "--binding-map",
        str(binding_map),
        "--limit",
        str(limit),
        "--format",
        "spec",
    ]
    result = subprocess.run(command, cwd=ROOT, check=True, capture_output=True, text=True)
    return json.loads(result.stdout)


def load_spec(args: argparse.Namespace) -> tuple[dict, str]:
    if args.spec_json:
        spec_path = args.spec_json.resolve()
        return json.loads(spec_path.read_text(encoding="utf-8-sig")), display_path(spec_path)

    if args.previous_binding_map is None:
        raise ValueError("Either --spec-json or --previous-binding-map is required.")

    previous_binding_map = args.previous_binding_map.resolve()
    return run_gap_spec(previous_binding_map, args.limit), ""


def generate(args: argparse.Namespace) -> int:
    previous_binding_map = args.previous_binding_map.resolve() if args.previous_binding_map else None
    alpha_root = args.alpha_root.resolve()
    output = args.output.resolve()
    spec, source_spec_json = load_spec(args)

    bindings = []
    missing: list[str] = []
    for item in spec.get("items", []):
        index = int(item["index"])
        safe_name = str(item["safeName"])
        sprite_path = alpha_root / f"{args.stem_prefix}_{index:02d}_{safe_name}_Alpha512.png"
        if not sprite_path.exists():
            missing.append(display_path(sprite_path))

        bindings.append(
            {
                "enabled": True,
                "approved": False,
                "reviewStatus": "PENDING_VISUAL_REVIEW",
                "reviewedBy": "",
                "reviewedAt": "",
                "reviewNote": "",
                "persistentId": item["persistentId"],
                "itemAsset": item["asset"],
                "spriteAsset": display_path(sprite_path),
                "spriteName": "",
                "note": f"generated from {'frozen gap spec' if source_spec_json else 'live gap audit'}; {item['promptPhrase']}",
            }
        )

    payload = {
        "schema": "hecton8.inventory_icon_candidate_binding_map.v1",
        "evidenceClass": "STATIC_SOURCE_DRAFT_NO_UNITY_IMPORT",
        "source": "Tools/InventoryIconBindingMapFromGapAudit.py",
        "previousBindingMap": display_path(previous_binding_map) if previous_binding_map else "",
        "sourceSpecJson": source_spec_json,
        "alphaRoot": display_path(alpha_root),
        "bindings": bindings,
    }
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

    print("INVENTORY_ICON_BINDING_MAP_GENERATOR")
    print(f"output={display_path(output)}")
    print(f"bindings={len(bindings)}")
    print(f"missingSprites={len(missing)}")
    for path in missing:
        print(f"MISSING {path}")
    return 1 if missing and args.require_sprites else 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--previous-binding-map", type=Path)
    parser.add_argument("--spec-json", type=Path)
    parser.add_argument("--alpha-root", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--stem-prefix", required=True)
    parser.add_argument("--limit", type=int, default=12)
    parser.add_argument("--require-sprites", action="store_true")
    return generate(parser.parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
