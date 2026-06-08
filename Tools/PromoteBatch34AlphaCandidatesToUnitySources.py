#!/usr/bin/env python3
"""Promote reviewed Batch34 alpha candidates into Unity-visible source assets."""

from __future__ import annotations

import json
import shutil
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCE_ALPHA_MANIFEST = (
    ROOT
    / "Docs/GeneratedAssets/Gemini/Outputs/Batch34_TextureExpansion/AlphaCandidates/Batch34_SourceAtlasAlphaCandidates_Manifest.json"
)
UNITY_ROOT = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates"
UNITY_MANIFEST = UNITY_ROOT / "GeminiBatch34AlphaCandidates_Manifest.json"
ACCEPT_STATUS = "ALPHA_CANDIDATE_STATIC_REVIEW_REQUIRED"


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def project_path(raw: str) -> Path:
    path = Path(raw)
    return path if path.is_absolute() else ROOT / path


def slug(value: str) -> str:
    chars = []
    for char in value.lower():
        if char.isalnum():
            chars.append(char)
        elif chars and chars[-1] != "_":
            chars.append("_")
    return "".join(chars).strip("_") or "alpha_candidate"


def image_info(path: Path) -> tuple[int, int, str]:
    with Image.open(path) as image:
        return image.width, image.height, image.mode


def main() -> int:
    if not SOURCE_ALPHA_MANIFEST.exists():
        raise FileNotFoundError(display_path(SOURCE_ALPHA_MANIFEST))

    payload = json.loads(SOURCE_ALPHA_MANIFEST.read_text(encoding="utf-8-sig"))
    entries: list[dict] = []
    skipped: list[dict] = []
    UNITY_ROOT.mkdir(parents=True, exist_ok=True)

    for entry in payload.get("entries", []) or []:
        entry_id = str(entry.get("id", "")).strip()
        title = str(entry.get("title", "")).strip()
        status = str(entry.get("status", "")).strip()
        alpha_source = project_path(str(entry.get("alphaCandidate", "")).strip())
        if status != ACCEPT_STATUS:
            skipped.append(
                {
                    "id": entry_id,
                    "title": title,
                    "status": status,
                    "reason": "not promoted to Unity source",
                }
            )
            continue
        if not alpha_source.exists():
            skipped.append(
                {
                    "id": entry_id,
                    "title": title,
                    "status": "MISSING_ALPHA_CANDIDATE",
                    "reason": display_path(alpha_source),
                }
            )
            continue

        family = slug(str(entry.get("family", "alpha_candidate")))
        target_dir = UNITY_ROOT / family
        target_dir.mkdir(parents=True, exist_ok=True)
        target = target_dir / f"TX_{entry_id}_{slug(title)}_AlphaCandidate.png"
        shutil.copy2(alpha_source, target)
        width, height, mode = image_info(target)
        stats = entry.get("alphaStats", {}) or {}
        entries.append(
            {
                "id": entry_id,
                "title": title,
                "sourceType": entry.get("sourceType", ""),
                "family": entry.get("family", ""),
                "sourceAtlas": entry.get("source", ""),
                "alphaCandidate": display_path(target),
                "sourceAlphaCandidate": display_path(alpha_source),
                "status": "ALPHA_CANDIDATE_UNITY_SOURCE_PENDING_REVIEW",
                "productionBindingStatus": "PENDING DECAL_SPLIT_OR_UV_BINDING",
                "unityImportStatus": "PENDING UNITY IMPORT",
                "width": width,
                "height": height,
                "mode": mode,
                "alphaStats": {
                    "alphaNonZeroPct": stats.get("alphaNonZeroPct"),
                    "alphaOpaquePct": stats.get("alphaOpaquePct"),
                    "alphaMean": stats.get("alphaMean"),
                },
            }
        )

    manifest = {
        "schema": "hecton8.batch34.alpha_candidate_unity_pack.v1",
        "sourceAlphaManifest": display_path(SOURCE_ALPHA_MANIFEST),
        "sourceAtlasManifest": "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/GeminiBatch34SourceAtlases_Manifest.json",
        "unityImportStatus": "PENDING UNITY IMPORT",
        "productionBindingStatus": "PENDING DECAL_SPLIT_OR_UV_BINDING",
        "policy": "Unity-visible source alpha candidates only. Do not auto-create Lit materials.",
        "entries": entries,
        "skipped": skipped,
    }
    UNITY_MANIFEST.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")

    print("BATCH34_ALPHA_CANDIDATE_PROMOTION")
    print(f"manifest={display_path(UNITY_MANIFEST)}")
    print(f"promoted={len(entries)}")
    print(f"skipped={len(skipped)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
