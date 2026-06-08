#!/usr/bin/env python3
"""Split Batch34 needs-work dark-background atlases into padded RGBA island candidates."""

from __future__ import annotations

import json
import math
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[1]
CURATION_MANIFEST = ROOT / "Docs/GeneratedAssets/Gemini/Outputs/Batch34_TextureExpansion/QA/Batch34_TextureExpansion_CurationManifest.json"
OUTPUT_ROOT = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SplitAtlasCandidates_20260608"
MANIFEST_PATH = OUTPUT_ROOT / "GeminiBatch34SplitAtlasCandidates_Manifest.json"
PREVIEW_PATH = OUTPUT_ROOT / "PREVIEW_Batch34_SplitAtlasCandidates.png"

TARGET_IDS = {
    "B34-3424",
    "B34-3438",
    "B34-3440",
    "B34-3443",
    "B34-3444",
    "B34-3447",
}

MIN_COMPONENT_AREA = {
    "B34-3424": 260,
    "B34-3438": 520,
    "B34-3440": 520,
    "B34-3443": 900,
    "B34-3444": 620,
    "B34-3447": 520,
}

MAX_COMPONENTS = {
    "B34-3424": 36,
    "B34-3438": 28,
    "B34-3440": 28,
    "B34-3443": 24,
    "B34-3444": 34,
    "B34-3447": 28,
}


def display(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def slug(value: str) -> str:
    result = []
    previous_underscore = False
    for ch in value.lower():
        if ch.isalnum():
            result.append(ch)
            previous_underscore = False
        elif not previous_underscore:
            result.append("_")
            previous_underscore = True
    return "".join(result).strip("_")


def next_power_of_two(value: int) -> int:
    value = max(1, value)
    return 1 << (value - 1).bit_length()


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


def build_foreground_mask(rgb: np.ndarray, background: np.ndarray) -> np.ndarray:
    diff = np.linalg.norm(rgb.astype(np.float32) - background.reshape(1, 1, 3), axis=2)
    hsv = cv2.cvtColor(rgb, cv2.COLOR_RGB2HSV)
    saturation = hsv[:, :, 1].astype(np.float32)
    value = hsv[:, :, 2].astype(np.float32)
    mask = ((diff > 28.0) & (value > 18.0)) | ((saturation > 44.0) & (diff > 14.0) & (value > 18.0))
    mask = mask.astype(np.uint8) * 255
    mask = cv2.medianBlur(mask, 3)
    kernel = np.ones((3, 3), np.uint8)
    mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, kernel, iterations=2)
    mask = cv2.dilate(mask, kernel, iterations=2)
    return mask


def local_alpha_mask(crop_rgb: np.ndarray, background: np.ndarray) -> np.ndarray:
    h, w = crop_rgb.shape[:2]
    diff = np.linalg.norm(crop_rgb.astype(np.float32) - background.reshape(1, 1, 3), axis=2)
    hsv = cv2.cvtColor(crop_rgb, cv2.COLOR_RGB2HSV)
    value = hsv[:, :, 2].astype(np.float32)
    background_like = ((diff < 30.0) & (value < 72.0)).astype(np.uint8)

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
    _, alpha = cv2.threshold(alpha, 16, 255, cv2.THRESH_TOZERO)
    return alpha


def component_boxes(mask: np.ndarray, job_id: str) -> list[tuple[int, int, int, int, int]]:
    count, labels, stats, _ = cv2.connectedComponentsWithStats(mask, 8)
    boxes: list[tuple[int, int, int, int, int]] = []
    min_area = MIN_COMPONENT_AREA[job_id]
    for label in range(1, count):
        x = int(stats[label, cv2.CC_STAT_LEFT])
        y = int(stats[label, cv2.CC_STAT_TOP])
        w = int(stats[label, cv2.CC_STAT_WIDTH])
        h = int(stats[label, cv2.CC_STAT_HEIGHT])
        area = int(stats[label, cv2.CC_STAT_AREA])
        if area < min_area or w < 10 or h < 10:
            continue
        boxes.append((x, y, x + w, y + h, area))

    boxes.sort(key=lambda item: item[4], reverse=True)
    return boxes[: MAX_COMPONENTS[job_id]]


def padded_box(box: tuple[int, int, int, int, int], width: int, height: int) -> tuple[int, int, int, int]:
    x0, y0, x1, y1, _ = box
    pad = max(18, int(max(x1 - x0, y1 - y0) * 0.14))
    return max(0, x0 - pad), max(0, y0 - pad), min(width, x1 + pad), min(height, y1 + pad)


def save_island(
    source_rgb: np.ndarray,
    background: np.ndarray,
    box: tuple[int, int, int, int],
    output_path: Path,
) -> dict[str, int | float | str]:
    x0, y0, x1, y1 = box
    crop = source_rgb[y0:y1, x0:x1, :]
    alpha = local_alpha_mask(crop, background)
    rgba = np.dstack([crop, alpha])
    pil = Image.fromarray(rgba, "RGBA")

    target_margin = 32
    size = next_power_of_two(max(pil.width, pil.height, 256) + target_margin * 2)
    size = min(max(size, 256), 1024)
    content_limit = max(1, size - target_margin * 2)
    if pil.width > content_limit or pil.height > content_limit:
        scale = min(content_limit / pil.width, content_limit / pil.height)
        pil = pil.resize((max(1, int(pil.width * scale)), max(1, int(pil.height * scale))), Image.Resampling.LANCZOS)

    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    canvas.alpha_composite(pil, ((size - pil.width) // 2, (size - pil.height) // 2))
    output_path.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output_path, optimize=True)

    alpha_arr = np.array(canvas.getchannel("A"))
    nonzero = int(np.count_nonzero(alpha_arr))
    edge = np.concatenate([alpha_arr[:8, :].ravel(), alpha_arr[-8:, :].ravel(), alpha_arr[:, :8].ravel(), alpha_arr[:, -8:].ravel()])
    return {
        "path": display(output_path),
        "width": canvas.width,
        "height": canvas.height,
        "sourceX": x0,
        "sourceY": y0,
        "sourceW": x1 - x0,
        "sourceH": y1 - y0,
        "alphaNonZeroPct": round(nonzero * 100.0 / alpha_arr.size, 3),
        "edgeAlphaNonZeroPct": round(float(np.count_nonzero(edge)) * 100.0 / max(1, edge.size), 3),
    }


def make_preview(entries: list[dict[str, object]]) -> None:
    items: list[tuple[Path, str]] = []
    for entry in entries:
        for island in entry.get("islands", [])[:8]:
            items.append((ROOT / str(island["path"]), f"{entry['id']}#{island['index']:02d}"))

    if not items:
        return

    thumb = 128
    label_h = 22
    gap = 8
    cols = 8
    rows = math.ceil(len(items) / cols)
    canvas = Image.new("RGBA", (cols * thumb + (cols - 1) * gap, rows * (thumb + label_h) + (rows - 1) * gap), (7, 10, 12, 255))
    draw = ImageDraw.Draw(canvas)
    try:
        font = ImageFont.truetype("arial.ttf", 11)
    except OSError:
        font = ImageFont.load_default()

    for index, (path, label) in enumerate(items):
        x = (index % cols) * (thumb + gap)
        y = (index // cols) * (thumb + label_h + gap)
        with Image.open(path) as image:
            image = image.convert("RGBA")
            image.thumbnail((thumb, thumb), Image.Resampling.LANCZOS)
            draw.rectangle((x, y, x + thumb - 1, y + thumb - 1), fill=(21, 25, 28, 255))
            canvas.alpha_composite(image, (x + (thumb - image.width) // 2, y + (thumb - image.height) // 2))
            draw.rectangle((x, y, x + thumb - 1, y + thumb - 1), outline=(48, 56, 62))
        draw.text((x + 3, y + thumb + 4), label, fill=(216, 230, 230), font=font)

    PREVIEW_PATH.parent.mkdir(parents=True, exist_ok=True)
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

        with Image.open(source_path) as image:
            rgb = np.array(image.convert("RGB"))

        background = estimate_background(rgb)
        mask = build_foreground_mask(rgb, background)
        boxes = component_boxes(mask, job_id)
        islands = []
        output_dir = OUTPUT_ROOT / str(source_entry.get("sourceType", "atlas")).lower() / job_id
        for island_index, box in enumerate(boxes):
            expanded = padded_box(box, rgb.shape[1], rgb.shape[0])
            out = output_dir / f"TX_{job_id}_island_{island_index:02d}.png"
            island = save_island(rgb, background, expanded, out)
            island["index"] = island_index
            islands.append(island)

        entries.append(
            {
                "id": job_id,
                "title": source_entry.get("title", ""),
                "sourceType": source_entry.get("sourceType", ""),
                "family": source_entry.get("family", ""),
                "source": display(source_path),
                "sourceCurationStatus": source_entry.get("curationStatus", ""),
                "productionBindingStatus": "SPLIT_ISLAND_CANDIDATE_PENDING_UV_BINDING",
                "islandCount": len(islands),
                "islands": islands,
            }
        )

    manifest = {
        "schema": "hecton8.batch34.split_atlas_candidates.v1",
        "sourceCurationManifest": display(CURATION_MANIFEST),
        "unityImportStatus": "PENDING UNITY IMPORT",
        "productionBindingStatus": "SPLIT_ISLAND_CANDIDATE_PENDING_UV_BINDING",
        "policy": "Transparent padded island candidates for UV/decal binding. Do not treat these as inventory icons.",
        "entries": entries,
        "preview": display(PREVIEW_PATH),
    }
    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    MANIFEST_PATH.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    make_preview(entries)
    print("BATCH34_SPLIT_ATLAS_CANDIDATES")
    print(f"manifest={display(MANIFEST_PATH)}")
    print(f"preview={display(PREVIEW_PATH)}")
    print(f"entries={len(entries)}")
    print(f"islands={sum(int(entry['islandCount']) for entry in entries)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
