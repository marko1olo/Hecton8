#!/usr/bin/env python3
"""Apply explicit visual-review decisions to inventory icon binding maps."""

from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def parse_ids(values: list[str] | None) -> set[str]:
    result: set[str] = set()
    if not values:
        return result

    for raw in values:
        for part in raw.split(","):
            value = part.strip()
            if value:
                result.add(value)
    return result


def approve_binding(binding: dict, reviewer: str, reason: str, reviewed_at: str) -> None:
    binding["approved"] = True
    binding["reviewStatus"] = "APPROVED"
    binding["reviewedBy"] = reviewer
    binding["reviewedAt"] = reviewed_at
    binding["reviewNote"] = reason


def reject_binding(binding: dict, reviewer: str, reason: str, reviewed_at: str) -> None:
    binding["enabled"] = False
    binding["approved"] = False
    binding["reviewStatus"] = "REJECTED"
    binding["reviewedBy"] = reviewer
    binding["reviewedAt"] = reviewed_at
    binding["reviewNote"] = reason
    binding["persistentId"] = ""
    binding["itemAsset"] = ""


def apply_reviews(args: argparse.Namespace) -> int:
    map_path = args.map.resolve()
    payload = json.loads(map_path.read_text(encoding="utf-8-sig"))
    bindings = payload.get("bindings", []) or []
    approve_ids = parse_ids(args.approve_persistent_id)
    reject_ids = parse_ids(args.reject_persistent_id)
    reviewed_at = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    changed = 0
    approved = 0
    rejected = 0

    for binding in bindings:
        persistent_id = str(binding.get("persistentId", "")).strip()
        if persistent_id and (args.approve_all_enabled or persistent_id in approve_ids):
            if not binding.get("enabled", False):
                raise RuntimeError(f"Refusing to approve disabled binding: {persistent_id}")
            approve_binding(binding, args.reviewer, args.reason, reviewed_at)
            changed += 1
            approved += 1
            continue

        if persistent_id and persistent_id in reject_ids:
            reject_binding(binding, args.reviewer, args.reason, reviewed_at)
            changed += 1
            rejected += 1

    if changed == 0:
        raise RuntimeError("No bindings matched review request.")

    map_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    print("INVENTORY_ICON_REVIEW_MAP_STATUS: PASS")
    print(f"map={display_path(map_path)}")
    print(f"approved={approved}")
    print(f"rejected={rejected}")
    print(f"reviewedAt={reviewed_at}")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--map", required=True, type=Path)
    parser.add_argument("--approve-all-enabled", action="store_true")
    parser.add_argument("--approve-persistent-id", action="append", default=[])
    parser.add_argument("--reject-persistent-id", action="append", default=[])
    parser.add_argument("--reviewer", default="codex-static-visual-review")
    parser.add_argument("--reason", required=True)
    args = parser.parse_args()
    if not args.approve_all_enabled and not args.approve_persistent_id and not args.reject_persistent_id:
        parser.error("provide --approve-all-enabled, --approve-persistent-id, or --reject-persistent-id")
    return apply_reviews(args)


if __name__ == "__main__":
    raise SystemExit(main())
