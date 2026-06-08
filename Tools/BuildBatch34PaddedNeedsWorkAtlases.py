#!/usr/bin/env python3
"""Build padded RGBA source atlases from Batch34 needs-work UV/decal atlases."""

from __future__ import annotations

import json
import math
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[1]
CURATION_MANIFEST = ROOT / "Docs/GeneratedAssets/Gemini/Outputs/Batch34_TextureExpansion/QA/Batch34_TextureExpansion_CurationManifest.json"
OUTPUT_ROOT = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34PaddedAtlasSources_20260608"
MANIFEST_PATH = OUTPUT_ROOT / "GeminiBatch34PaddedAtlasSources_Manifest.json"
PREVIEW_PATH = OUTPUT_ROOT / "PREVIEW_Batch34_PaddedAtlasSources.png"
TARGET_IDS = {"B34-3424", "B34-3438", "B34-3440", "B34-3443", "B34-3444", "B34-3447"}
CANVAS_SIZE = 1536


def display(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def estimate_background(rgb: np.ndarray) -> np.ndarray:
    border = max(8, min(rgb.shape[0], rgb.shape[1]) // 32)
    samples = np.concatenate(
        [
            rgb[:border, :, :].reshape(-1, 3),
            rgb[-border:, :, :].reshape(-1, 3),
            rgb[:, :border, :].reshape(-1, 3),
            rgb[:, -border:, :].reshape(-1, 3),
        ],
        axis=0,
    )
    return np.median(samples, axis=0).astype(np.float32)


def alpha_from_background(rgb: np.ndarray, background: np.ndarray) -> np.ndarray:
    h, w = rgb.shape[:2]
    diff = np.linalg.norm(rgb.astype(np.float32) - background.reshape(1, 1, 3), axis=2)
    hsv = cv2.cvtColor(rgb, cv2.COLOR_RGB2HSV)
    value = hsv[:, :, 2].astype(np.float32)
    background_like = ((diff < 34.0) & (value < 80.0)).astype(np.uint8)

    flood = np.zeros((h + 2, w + 2), np.uint8)
    flood_source = background_like.copy()
    for x in range(w):
        if flood_source[0, x]:
            cv2.floodFill(flood_source, flood, (x, 0), 2)
        if flood_source[h - 1, x]:
            cv2.floodFill(flood_source, flood, (x, h - 1), 2)
    for y in range(h):
        if flood_source[y, 0]:
            cv2.floodFill(flood_source, flood, (0, y), 2)
        if flood_source[y, w - 1]:
            cv2.floodFill(flood_source, flood, (w - 1, y), 2)

    alpha = np.where(flood_source == 2, 0, 255).astype(np.uint8)
    alpha = cv2.GaussianBlur(alpha, (5, 5), 0)
    _, alpha = cv2.threshold(alpha, 12, 255, cv2.THRESH_TOZERO)
    return alpha


def save_padded(source_path: Path, output_path: Path) -> dict[str, object]:
    with Image.open(source_path) as image:
        source = image.convert("RGB")
    rgb = np.array(source)
    background = estimate_background(rgb)
    alpha = alpha_from_background(rgb, background)
    rgba = Image.fromarray(np.dstack([rgb, alpha]), "RGBA")

    canvas = Image.new("RGBA", (CANVAS_SIZE, CANVAS_SIZE), (0, 0, 0, 0))
    canvas.alpha_composite(rgba, ((CANVAS_SIZE - rgba.width) // 2, (CANVAS_SIZE - rgba.height) // 2))
    output_path.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output_path, optimize=True)

    alpha_arr = np.array(canvas.getchannel("A"))
    edge = np.concatenate([alpha_arr[:16, :].ravel(), alpha_arr[-16:, :].ravel(), alpha_arr[:, :16].ravel(), alpha_arr[:, -16:].ravel()])
    return {
        "paddedAtlas": display(output_path),
        "width": canvas.width,
        "height": canvas.height,
        "sourceWidth": source.width,
        "sourceHeight": source.height,
        "alphaNonZeroPct": round(float(np.count_nonzero(alpha_arr)) * 100.0 / alpha_arr.size, 3),
        "edgeAlphaNonZeroPct": round(float(np.count_nonzero(edge)) * 100.0 / max(1, edge.size), 3),
    }


def make_preview(entries: list[dict[str, object]]) -> None:
    if not entries:
        return

    thumb = 180
    label_h = 28
    gap = 10
    cols = 3
    rows = math.ceil(len(entries) / cols)
    canvas = Image.new("RGBA", (cols * thumb + (cols - 1) * gap, rows * (thumb + label_h) + (rows - 1) * gap), (7, 10, 12, 255))
    draw = ImageDraw.Draw(canvas)
    try:
        font = ImageFont.truetype("arial.ttf", 12)
    except OSError:
        font = ImageFont.load_default()

    for index, entry in enumerate(entries):
        x = (index % cols) * (thumb + gap)
        y = (index // cols) * (thumb + label_h + gap)
        path = ROOT / str(entry["paddedAtlas"])
        with Image.open(path) as image:
            image = image.convert("RGBA")
            image.thumbnail((thumb, thumb), Image.Resampling.LANCZOS)
            draw.rectangle((x, y, x + thumb - 1, y + thumb - 1), fill=(21, 25, 28, 255), outline=(48, 56, 62))
            canvas.alpha_composite(image, (x + (thumb - image.width) // 2, y + (thumb - image.height) // 2))
        draw.text((x + 4, y + thumb + 5), str(entry["id"]), fill=(216, 230, 230), font=font)

    canvas.convert("RGB").save(PREVIEW_PATH)


def main() -> int:
    payload = json.loads(CURATION_MANIFEST.read_text(encoding="utf-8-sig"))
    entries: list[dict[str, object]] = []
    for source_entry in payload.get("entries", []) or []:
        job_id = str(source_entry.get("id", "")).strip()
        if job_id not in TARGET_IDS:
            continue
        source_path = ROOT / str(source_entry.get("baseColorCandidatePath", ""))
        if not source_path.exists():
            continue
        output_path = OUTPUT_ROOT / str(source_entry.get("sourceType", "atlas")).lower() / f"TX_{job_id}_padded_source_atlas.png"
        padded = save_padded(source_path, output_path)
        entries.append(
            {
                "id": job_id,
                "title": source_entry.get("title", ""),
                "sourceType": source_entry.get("sourceType", ""),
                "family": source_entry.get("family", ""),
                "source": display(source_path),
                "sourceCurationStatus": source_entry.get("curationStatus", ""),
                "productionBindingStatus": "PADDED_SOURCE_ATLAS_PENDING_UV_BINDING",
                **padded,
            }
        )

    manifest = {
        "schema": "hecton8.batch34.padded_atlas_sources.v1",
        "sourceCurationManifest": display(CURATION_MANIFEST),
        "unityImportStatus": "PENDING UNITY IMPORT",
        "productionBindingStatus": "PADDED_SOURCE_ATLAS_PENDING_UV_BINDING",
        "policy": "Whole-atlas RGBA sources with transparent safety border. Use for UV/decal binding; do not treat as inventory icons.",
        "canvasSize": CANVAS_SIZE,
        "entries": entries,
        "preview": display(PREVIEW_PATH),
    }
    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    MANIFEST_PATH.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    make_preview(entries)
    print("BATCH34_PADDED_ATLAS_SOURCES")
    print(f"manifest={display(MANIFEST_PATH)}")
    print(f"preview={display(PREVIEW_PATH)}")
    print(f"entries={len(entries)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
