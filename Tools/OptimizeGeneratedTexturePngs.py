#!/usr/bin/env python3
"""Losslessly optimize first-party generated PNG texture artifacts in place."""

from __future__ import annotations

import argparse
import json
import tempfile
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_TARGETS = (
    ROOT / "Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported",
    ROOT / "Assets/_Project/Art/TEXTURES/Meshy_AI_Alien_barnacles_clust_0301230506_texture.png",
)
MANIFEST_PATH = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeneratedTextureOptimization_20260607.json"


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def iter_pngs(target: Path) -> list[Path]:
    if target.is_file() and target.suffix.lower() == ".png":
        return [target]
    if target.is_dir():
        return sorted(target.rglob("*.png"))
    return []


def optimize_png(path: Path, min_saved_bytes: int) -> dict:
    before = path.stat().st_size
    with Image.open(path) as image:
        mode = image.mode
        size = image.size
        output = image.copy()

    with tempfile.NamedTemporaryFile(suffix=".png", delete=False) as handle:
        temp_path = Path(handle.name)

    try:
        output.save(temp_path, "PNG", optimize=True, compress_level=9)
        after = temp_path.stat().st_size
        saved = before - after
        if saved >= min_saved_bytes and after > 0:
            temp_path.replace(path)
            changed = True
        else:
            temp_path.unlink(missing_ok=True)
            after = before
            saved = 0
            changed = False
    finally:
        if temp_path.exists():
            temp_path.unlink(missing_ok=True)

    return {
        "path": display_path(path),
        "mode": mode,
        "width": size[0],
        "height": size[1],
        "beforeBytes": before,
        "afterBytes": after,
        "savedBytes": saved,
        "changed": changed,
    }


def run(args: argparse.Namespace) -> int:
    targets = [Path(raw) for raw in args.targets] if args.targets else list(DEFAULT_TARGETS)
    paths: list[Path] = []
    for target in targets:
        target_path = target if target.is_absolute() else ROOT / target
        paths.extend(iter_pngs(target_path.resolve()))

    seen: set[Path] = set()
    unique_paths: list[Path] = []
    for path in paths:
        resolved = path.resolve()
        if resolved in seen:
            continue
        seen.add(resolved)
        unique_paths.append(resolved)

    records = [optimize_png(path, args.min_saved_bytes) for path in unique_paths]
    changed = [record for record in records if record["changed"]]
    manifest = {
        "schema": "hecton8.generated_texture_optimization.v1",
        "date": "2026-06-07",
        "operation": "lossless_png_optimize_same_path",
        "scope": [display_path((target if target.is_absolute() else ROOT / target).resolve()) for target in targets],
        "filesScanned": len(records),
        "filesChanged": len(changed),
        "savedBytes": sum(record["savedBytes"] for record in changed),
        "records": records,
    }
    MANIFEST_PATH.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")

    print("GENERATED_TEXTURE_PNG_OPTIMIZATION_STATUS: PASS")
    print(f"files_scanned={len(records)}")
    print(f"files_changed={len(changed)}")
    print(f"saved_mb={sum(record['savedBytes'] for record in changed) / 1024 / 1024:.2f}")
    print(f"manifest={display_path(MANIFEST_PATH)}")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("targets", nargs="*", help="PNG file or directory targets. Defaults to known first-party generated texture roots.")
    parser.add_argument("--min-saved-bytes", type=int, default=4096)
    return run(parser.parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
