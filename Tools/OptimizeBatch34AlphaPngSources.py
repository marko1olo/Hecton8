#!/usr/bin/env python3
"""Losslessly optimize Batch34 RGBA PNG alpha/padded source files."""

from __future__ import annotations

import json
import os
from pathlib import Path

from PIL import Image, ImageChops


ROOT_PATH = Path(__file__).resolve().parents[1]
ROOT = ROOT_PATH
ALPHA_MANIFEST = (
    ROOT
    / "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/GeminiBatch34AlphaCandidates_Manifest.json"
)
PADDED_MANIFEST = (
    ROOT
    / "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34PaddedAtlasSources_20260608/GeminiBatch34PaddedAtlasSources_Manifest.json"
)


class ToolError(Exception):
    pass


def display(path: Path) -> str:
    try:
        return str(path.relative_to(ROOT_PATH)).replace("\\", "/")
    except ValueError:
        return str(path).replace("\\", "/")


def project_path(raw: str) -> Path:
    path = Path(raw)
    return path if path.is_absolute() else ROOT / path


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def iter_targets() -> list[tuple[str, Path]]:
    targets: list[tuple[str, Path]] = []
    if ALPHA_MANIFEST.exists():
        payload = load_json(ALPHA_MANIFEST)
        for entry in payload.get("entries", []) or []:
            entry_id = str(entry.get("id", "")).strip()
            raw = str(entry.get("alphaCandidate", "")).strip()
            if entry_id and raw:
                targets.append((entry_id, project_path(raw)))

    if PADDED_MANIFEST.exists():
        payload = load_json(PADDED_MANIFEST)
        for entry in payload.get("entries", []) or []:
            entry_id = str(entry.get("id", "")).strip()
            raw = str(entry.get("paddedAtlas", "")).strip()
            if entry_id and raw:
                targets.append((f"{entry_id}:padded", project_path(raw)))
    return targets


def optimize_png(entry_id: str, path: Path) -> tuple[str, int, int]:
    if not path.exists():
        return "missing", 0, 0
    if path.suffix.lower() != ".png":
        return "skipped-non-png", path.stat().st_size, path.stat().st_size

    before = path.stat().st_size
    tmp = path.with_suffix(path.suffix + ".tmp")
    with Image.open(path) as source:
        source_rgba = source.convert("RGBA")
        source_rgba.save(tmp, "PNG", optimize=True, compress_level=9)

    after = tmp.stat().st_size
    if after >= before:
        tmp.unlink(missing_ok=True)
        return "kept", before, before

    with Image.open(path) as original, Image.open(tmp) as candidate:
        original_rgba = original.convert("RGBA")
        candidate_rgba = candidate.convert("RGBA")
        if original_rgba.size != candidate_rgba.size or ImageChops.difference(original_rgba, candidate_rgba).getbbox() is not None:
            tmp.unlink(missing_ok=True)
            return "rejected-pixel-diff", before, before

    os.replace(tmp, path)
    return "optimized", before, after


def main() -> int:
    targets = iter_targets()
    optimized = 0
    kept = 0
    skipped = 0
    missing = 0
    before_total = 0
    after_total = 0

    print("BATCH34_ALPHA_PNG_SOURCE_OPTIMIZER")
    for entry_id, path in targets:
        status, before, after = optimize_png(entry_id, path)
        before_total += before
        after_total += after
        if status == "optimized":
            optimized += 1
            saved_kb = (before - after) / 1024
            print(f"OPTIMIZED {entry_id} savedKB={saved_kb:.1f} path={display(path)}")
        elif status == "kept":
            kept += 1
        elif status == "missing":
            missing += 1
            print(f"MISSING {entry_id} path={display(path)}")
        else:
            skipped += 1
            print(f"SKIPPED {entry_id} status={status} path={display(path)}")

    saved_mb = (before_total - after_total) / (1024 * 1024)
    print(f"targets={len(targets)} optimized={optimized} kept={kept} skipped={skipped} missing={missing} savedMB={saved_mb:.3f}")
    return 1 if missing else 0


if __name__ == "__main__":
    raise SystemExit(main())
