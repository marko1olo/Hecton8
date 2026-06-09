#!/usr/bin/env python3
"""Promote curated Batch34 non-material source atlases into a Unity-visible source pack."""

from __future__ import annotations

import json
import shutil
from pathlib import Path

from PIL import Image, ImageDraw, ImageOps


ROOT = Path(__file__).resolve().parents[1]
BATCH_ROOT = ROOT / "Docs/GeneratedAssets/Gemini/Outputs/Batch34_TextureExpansion"
CURATION_MANIFEST = BATCH_ROOT / "QA/Batch34_TextureExpansion_CurationManifest.json"
OUTPUT_ROOT = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608"
SOURCE_TYPES = {"DECAL_ATLAS", "UV_ATLAS", "PICKUP_ATLAS"}


def project_rel(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def project_path(raw: str | Path) -> Path:
    path = Path(raw)
    return path if path.is_absolute() else ROOT / path


def load_curation() -> list[dict]:
    payload = json.loads(CURATION_MANIFEST.read_text(encoding="utf-8"))
    return list(payload["entries"])


def bucket_for(entry: dict) -> str:
    status = str(entry["curationStatus"])
    source_type = str(entry["sourceType"])
    if status == "CURATED_READY_ALPHA_SOURCE":
        return "AlphaMaskSources"
    if source_type == "DECAL_ATLAS":
        return "DecalAtlases"
    if source_type == "UV_ATLAS":
        return "UvAtlases"
    if source_type == "PICKUP_ATLAS":
        return "PickupAtlases"
    return "OtherSources"


def downstream_route(entry: dict) -> str:
    source_type = str(entry["sourceType"])
    status = str(entry["curationStatus"])
    if status == "CURATED_READY_ALPHA_SOURCE":
        return "extract alpha/matte mask before material or decal binding"
    if source_type == "DECAL_ATLAS":
        return "split/pad decal islands, then bind through decal/material owner"
    if source_type == "UV_ATLAS":
        return "split islands or assign to mesh UV source; no Lit material auto-create"
    if source_type == "PICKUP_ATLAS":
        return "bind to small 3D pickup mesh UVs; not inventory icons"
    return "source-only review"


def copy_source(entry: dict) -> dict:
    src = project_path(entry["curatedBaseColorPath"])
    if not src.exists():
        raise FileNotFoundError(project_rel(src))
    bucket = bucket_for(entry)
    dst_dir = OUTPUT_ROOT / bucket
    dst_dir.mkdir(parents=True, exist_ok=True)
    dst = dst_dir / src.name
    shutil.copy2(src, dst)
    with Image.open(dst) as image:
        width, height = image.size
        mode = image.mode
    return {
        "id": entry["id"],
        "title": entry["title"],
        "sourceType": entry["sourceType"],
        "family": entry["family"],
        "curationStatus": entry["curationStatus"],
        "targetRole": entry["targetRole"],
        "integrationNote": entry["integrationNote"],
        "regenTargetId": entry.get("regenTargetId", ""),
        "regenTargetVariant": entry.get("regenTargetVariant", ""),
        "regenTargetDecision": entry.get("regenTargetDecision", ""),
        "regenTargetManifest": entry.get("regenTargetManifest", ""),
        "downstreamRoute": downstream_route(entry),
        "source": project_rel(dst),
        "width": width,
        "height": height,
        "mode": mode,
        "unityImportStatus": "PENDING UNITY IMPORT",
        "productionBindingStatus": "PENDING SPLIT_OR_ALPHA_EXTRACTION",
    }


def write_preview(entries: list[dict], path: Path) -> None:
    if not entries:
        return
    tile = 180
    label_h = 48
    gap = 10
    columns = 5
    rows = (len(entries) + columns - 1) // columns
    canvas = Image.new("RGB", (columns * tile + (columns - 1) * gap, rows * (tile + label_h) + (rows - 1) * gap), (8, 12, 14))
    draw = ImageDraw.Draw(canvas)
    for index, entry in enumerate(entries):
        with Image.open(project_path(entry["source"])) as image:
            preview = ImageOps.exif_transpose(image).convert("RGB")
            preview.thumbnail((tile, tile), Image.Resampling.LANCZOS)
        cell_x = (index % columns) * (tile + gap)
        cell_y = (index // columns) * (tile + label_h + gap)
        x = cell_x + (tile - preview.width) // 2
        y = cell_y + (tile - preview.height) // 2
        canvas.paste(preview, (x, y))
        draw.rectangle((cell_x, cell_y + tile, cell_x + tile, cell_y + tile + label_h), fill=(5, 12, 18))
        draw.text((cell_x + 5, cell_y + tile + 5), str(entry["id"]), fill=(220, 232, 238))
        draw.text((cell_x + 5, cell_y + tile + 22), str(entry["sourceType"])[:28], fill=(180, 204, 210))
    path.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(path, "PNG")


def promote() -> int:
    accepted: list[dict] = []
    skipped: list[dict] = []
    for entry in load_curation():
        status = str(entry["curationStatus"])
        source_type = str(entry["sourceType"])
        if source_type in SOURCE_TYPES and status.startswith("CURATED_READY"):
            accepted.append(copy_source(entry))
        else:
            skipped.append(
                {
                    "id": entry["id"],
                    "sourceType": source_type,
                    "curationStatus": status,
                    "reason": "not curated-ready source atlas",
                }
            )

    preview = OUTPUT_ROOT / "PREVIEW_Batch34_SourceAtlases.png"
    write_preview(accepted, preview)
    manifest = {
        "schema": "hecton8.batch34.source_atlas_unity_pack.v1",
        "sourceProvider": "GeminiBatch34TextureExpansion",
        "sourceCurationManifest": project_rel(CURATION_MANIFEST),
        "unityImportStatus": "PENDING UNITY IMPORT",
        "productionBindingStatus": "PENDING SPLIT_OR_ALPHA_EXTRACTION",
        "policy": "These are source atlases only. Do not auto-create Lit materials from them.",
        "preview": project_rel(preview),
        "entries": accepted,
        "skipped": skipped,
    }
    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    manifest_path = OUTPUT_ROOT / "GeminiBatch34SourceAtlases_Manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print("BATCH34_SOURCE_ATLAS_PROMOTION_DONE")
    print(f"accepted_source_atlases={len(accepted)}")
    print(f"skipped={len(skipped)}")
    print(f"manifest={project_rel(manifest_path)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(promote())
