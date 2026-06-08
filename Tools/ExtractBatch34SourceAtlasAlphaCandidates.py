#!/usr/bin/env python3
"""Build RGBA alpha-extraction candidates for promoted Batch34 source atlases."""

from __future__ import annotations

import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageOps


ROOT = Path(__file__).resolve().parents[1]
SOURCE_MANIFEST = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/GeminiBatch34SourceAtlases_Manifest.json"
OUTPUT_ROOT = ROOT / "Docs/GeneratedAssets/Gemini/Outputs/Batch34_TextureExpansion/AlphaCandidates"
REVIEW_ROOT = ROOT / "Docs/GeneratedAssets/Gemini/Outputs/Batch34_TextureExpansion/AlphaReview"
MANIFEST_PATH = OUTPUT_ROOT / "Batch34_SourceAtlasAlphaCandidates_Manifest.json"
PREVIEW_PATH = REVIEW_ROOT / "PREVIEW_Batch34_SourceAtlasAlphaCandidates.png"


def project_rel(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def project_path(raw: str | Path) -> Path:
    path = Path(raw)
    return path if path.is_absolute() else ROOT / path


def slug_for(entry: dict) -> str:
    source = Path(str(entry.get("source", ""))).stem
    return source.replace("_BaseColorCandidate", "")


def estimate_background(rgb: Image.Image) -> tuple[int, int, int]:
    sample = 64
    width, height = rgb.size
    arr = np.asarray(rgb, dtype=np.uint8)
    border = np.concatenate(
        (
            arr[:sample, :, :].reshape(-1, 3),
            arr[height - sample :, :, :].reshape(-1, 3),
            arr[:, :sample, :].reshape(-1, 3),
            arr[:, width - sample :, :].reshape(-1, 3),
        ),
        axis=0,
    )

    # Quantized dominant border color is more stable than corner median when atlas islands touch corners.
    quantized = (border // 16).astype(np.uint16)
    keys = (quantized[:, 0] << 8) | (quantized[:, 1] << 4) | quantized[:, 2]
    unique_keys, counts = np.unique(keys, return_counts=True)
    dominant_key = unique_keys[np.argmax(counts)]
    dominant_pixels = border[keys == dominant_key]
    pixels = dominant_pixels if dominant_pixels.shape[0] >= 64 else border
    background = np.median(pixels, axis=0).astype(np.uint8)
    return int(background[0]), int(background[1]), int(background[2])


def alpha_from_background(rgb: Image.Image, background: tuple[int, int, int]) -> tuple[Image.Image, dict]:
    arr_u8 = np.asarray(rgb, dtype=np.uint8)
    arr = arr_u8.astype(np.int16)
    bg = np.asarray(background, dtype=np.int16).reshape(1, 1, 3)
    channel_delta = np.abs(arr - bg).sum(axis=2)
    luma = (arr[:, :, 0] * 77 + arr[:, :, 1] * 150 + arr[:, :, 2] * 29) >> 8
    bg_luma = (background[0] * 77 + background[1] * 150 + background[2] * 29) >> 8
    luma_delta = np.abs(luma - bg_luma)
    chroma_delta = arr.max(axis=2) - arr.min(axis=2)
    score = np.maximum.reduce((channel_delta, luma_delta * 3, chroma_delta * 2))
    alpha = np.clip(((score - 18) * 255) / 102, 0, 255).astype(np.uint8)
    rgba_arr = np.dstack((arr_u8, alpha))
    rgba = Image.fromarray(rgba_arr, "RGBA")

    total = alpha.size
    nonzero = int(np.count_nonzero(alpha))
    opaque = int(np.count_nonzero(alpha > 240))
    alpha_sum = int(alpha.sum())
    stats = {
        "background": list(background),
        "alphaNonZeroPct": round(nonzero * 100.0 / total, 3),
        "alphaOpaquePct": round(opaque * 100.0 / total, 3),
        "alphaMean": round(alpha_sum / total, 3),
    }
    return rgba, stats


def checkerboard(size: tuple[int, int], cell: int = 16) -> Image.Image:
    width, height = size
    image = Image.new("RGB", size, (42, 46, 50))
    draw = ImageDraw.Draw(image)
    for y in range(0, height, cell):
        for x in range(0, width, cell):
            if ((x // cell) + (y // cell)) % 2 == 0:
                draw.rectangle((x, y, x + cell - 1, y + cell - 1), fill=(86, 92, 96))
    return image


def write_preview(entries: list[dict]) -> None:
    tile = 176
    label_h = 50
    gap = 10
    columns = 5
    rows = (len(entries) + columns - 1) // columns
    canvas = Image.new("RGB", (columns * tile + (columns - 1) * gap, rows * (tile + label_h) + (rows - 1) * gap), (8, 12, 14))
    draw = ImageDraw.Draw(canvas)

    for index, entry in enumerate(entries):
        with Image.open(project_path(entry["alphaCandidate"])) as image:
            image = ImageOps.exif_transpose(image).convert("RGBA")
            image.thumbnail((tile, tile), Image.Resampling.LANCZOS)
            bg = checkerboard((tile, tile), 14).convert("RGBA")
            x0 = (tile - image.width) // 2
            y0 = (tile - image.height) // 2
            bg.alpha_composite(image, (x0, y0))
            preview = bg.convert("RGB")

        cell_x = (index % columns) * (tile + gap)
        cell_y = (index // columns) * (tile + label_h + gap)
        canvas.paste(preview, (cell_x, cell_y))
        draw.rectangle((cell_x, cell_y + tile, cell_x + tile, cell_y + tile + label_h), fill=(5, 12, 18))
        draw.text((cell_x + 5, cell_y + tile + 5), str(entry["id"]), fill=(220, 232, 238))
        draw.text(
            (cell_x + 5, cell_y + tile + 23),
            f"a>0 {entry['alphaStats']['alphaNonZeroPct']:.1f}%",
            fill=(180, 204, 210),
        )

    REVIEW_ROOT.mkdir(parents=True, exist_ok=True)
    canvas.save(PREVIEW_PATH, "PNG")


def extract() -> int:
    payload = json.loads(SOURCE_MANIFEST.read_text(encoding="utf-8-sig"))
    entries = list(payload.get("entries", []) or [])
    accepted: list[dict] = []
    warnings: list[str] = []

    for entry in entries:
        source = project_path(entry["source"])
        with Image.open(source) as image:
            rgb = ImageOps.exif_transpose(image).convert("RGB")

        background = estimate_background(rgb)
        rgba, stats = alpha_from_background(rgb, background)
        bucket = str(entry["sourceType"]).lower()
        out_dir = OUTPUT_ROOT / bucket
        out_dir.mkdir(parents=True, exist_ok=True)
        out_path = out_dir / f"{slug_for(entry)}_AlphaCandidate.png"
        rgba.save(out_path, "PNG", compress_level=3)

        status = "ALPHA_CANDIDATE_STATIC_REVIEW_REQUIRED"
        if stats["alphaNonZeroPct"] < 2.0:
            warnings.append(f"{entry['id']}: very low alpha coverage {stats['alphaNonZeroPct']}%")
            status = "ALPHA_CANDIDATE_REJECT_LOW_COVERAGE"
        if stats["alphaNonZeroPct"] > 92.0:
            warnings.append(f"{entry['id']}: very high alpha coverage {stats['alphaNonZeroPct']}%")
            status = "ALPHA_CANDIDATE_REJECT_HIGH_COVERAGE"

        accepted.append(
            {
                "id": entry["id"],
                "title": entry["title"],
                "sourceType": entry["sourceType"],
                "family": entry["family"],
                "source": entry["source"],
                "alphaCandidate": project_rel(out_path),
                "alphaStats": stats,
                "status": status,
            }
        )

    write_preview(accepted)
    manifest = {
        "schema": "hecton8.batch34.source_atlas_alpha_candidates.v1",
        "sourceManifest": project_rel(SOURCE_MANIFEST),
        "preview": project_rel(PREVIEW_PATH),
        "entries": accepted,
        "warnings": warnings,
        "status": "STATIC_REVIEW_REQUIRED",
    }
    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    MANIFEST_PATH.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")

    print("BATCH34_ALPHA_CANDIDATE_EXTRACTION_DONE")
    print(f"entries={len(accepted)}")
    print(f"warnings={len(warnings)}")
    print(f"manifest={project_rel(MANIFEST_PATH)}")
    print(f"preview={project_rel(PREVIEW_PATH)}")
    for warning in warnings:
        print(f"WARN {warning}")
    return 0


if __name__ == "__main__":
    raise SystemExit(extract())
