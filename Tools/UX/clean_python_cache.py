#!/usr/bin/env python3
"""Remove Python __pycache__ directories under the repo Tools tree."""

from __future__ import annotations

import argparse
import json
import shutil
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
DEFAULT_REPORT = ROOT / "Docs/AgentLogs/UI_PythonCacheCleanup_UX_ENGINEER.json"


def find_pycache_dirs(root: Path) -> list[Path]:
    """Return sorted __pycache__ directories under root."""

    if not root.exists():
        return []

    return sorted(path for path in root.rglob("__pycache__") if path.is_dir())


def _is_within(path: Path, root: Path) -> bool:
    try:
        path.resolve().relative_to(root.resolve())
        return True
    except ValueError:
        return False


def remove_pycache_dirs(root: Path) -> list[str]:
    """Remove __pycache__ directories under root and return removed paths."""

    root = root.resolve()
    removed: list[str] = []
    for cache_dir in find_pycache_dirs(root):
        resolved = cache_dir.resolve()
        if not _is_within(resolved, root):
            raise RuntimeError(f"refusing to remove outside root: {resolved}")
        shutil.rmtree(resolved)
        removed.append(str(resolved.relative_to(root)))
    return removed


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=str(ROOT / "Tools"), help="Root directory to scan.")
    parser.add_argument("--write-report", action="store_true", help="Write cleanup report JSON.")
    parser.add_argument("--report", default=str(DEFAULT_REPORT), help="Output report path.")
    args = parser.parse_args()

    root = Path(args.root)
    if not root.is_absolute():
        root = ROOT / root

    if not _is_within(root, ROOT):
        raise SystemExit(f"root must stay inside repo: {root}")

    removed = remove_pycache_dirs(root)
    report = {
        "schema": "hecton8.hardware_adaptive_ui_scaler.python_cache_cleanup.v1",
        "owner": "UX_ENGINEER",
        "promptId": "HARDWARE_ADAPTIVE_UI_BAKER",
        "status": "PASS",
        "root": str(root),
        "removedCount": len(removed),
        "removed": removed,
    }

    if args.write_report:
        report_path = Path(args.report)
        if not report_path.is_absolute():
            report_path = ROOT / report_path
        report_path.parent.mkdir(parents=True, exist_ok=True)
        report_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")

    print(f"Python cache cleanup PASS: removed {len(removed)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
