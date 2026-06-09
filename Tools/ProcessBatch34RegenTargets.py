#!/usr/bin/env python3
"""Process Batch34 targeted regen texture candidates into a durable handoff manifest."""

from __future__ import annotations

import hashlib
import json
import math
import shutil
from dataclasses import dataclass
from pathlib import Path

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageOps


ROOT = Path(__file__).resolve().parents[1]
DOWNLOADS = Path(r"C:\Users\danat\Downloads")
OUTPUT_ROOT = ROOT / "Docs/GeneratedAssets/Gemini/Outputs/Batch34_TextureExpansion/RegenTargets"
ORIGINALS_DIR = OUTPUT_ROOT / "Originals"
CLEANED_DIR = OUTPUT_ROOT / "SourceCleaned"
DERIVED_DIR = OUTPUT_ROOT / "Derived"
QA_DIR = OUTPUT_ROOT / "QA"
TILE_PREVIEW_DIR = OUTPUT_ROOT / "TilePreviews"
IMAGE_EXTS = {".jpg", ".jpeg", ".png", ".webp"}


@dataclass(frozen=True)
class CandidateSpec:
    regen_id: str
    source_id: str
    variant: str
    source_type: str
    source_globs: tuple[str, ...]
    priority: str
    decision: str
    selected: bool
    broad_seamless_accepted: bool
    note: str
    crop_box: tuple[int, int, int, int] | None = None
    metallic: float = 0.0
    roughness: float = 0.7
    normal_strength: float = 1.0


CANDIDATES: tuple[CandidateSpec, ...] = (
    CandidateSpec(
        "B34-3409-R1",
        "B34-3409",
        "limestone_ceiling_png_named",
        "SEAMLESS_TILE",
        ("B34-3409-R1_Limestone_Ceiling_Mineral_Drip.png",),
        "REQUIRED",
        "REJECT_REGEN_SEAMLESS_HERO_REPEAT",
        False,
        False,
        "Strong limestone material, but 2x2 still repeats large drip/green landmark shapes. Keep as local reference only.",
    ),
    CandidateSpec(
        "B34-3409-R1",
        "B34-3409",
        "limestone_ceiling_jpeg_timestamp",
        "SEAMLESS_TILE",
        ("Limestone_cave_ceiling_material*_202606082255.jpeg",),
        "REQUIRED",
        "SELECTED_REGEN_SEAMLESS_SOURCE",
        True,
        True,
        "Best limestone regen result: lower landmarking than the named PNG and acceptable static 2x2 repeat for cave-ceiling material handoff.",
        metallic=0.0,
        roughness=0.74,
        normal_strength=2.2,
    ),
    CandidateSpec(
        "B34-3418-R1",
        "B34-3418",
        "viewport_glass_jpeg_timestamp",
        "DECAL_ATLAS",
        ("Decal_atlas_for_viewport_glass_202606082255.jpeg",),
        "REQUIRED",
        "SELECTED_REGEN_ALPHA_SOURCE",
        True,
        False,
        "Best targeted regen result: isolated decal islands, clean neutral matte, no edge contact. Use for alpha extraction/matte cleanup route.",
    ),
    CandidateSpec(
        "B34-3407-R1",
        "B34-3407",
        "iron_oxide_jpeg_timestamp",
        "SEAMLESS_TILE",
        ("Iron-oxide_seep_crust_material_202606082255.jpeg",),
        "OPTIONAL_BACKUP",
        "HOLD_LOCAL_PATCH_ONLY_HERO_REPEAT",
        False,
        False,
        "Visually useful iron seep crust, but 2x2 repeats large rust mats. Keep as local patch/reference, not broad terrain replacement.",
    ),
    CandidateSpec(
        "B34-3417-R1",
        "B34-3417",
        "amber_lens_png_named",
        "SEAMLESS_TILE",
        ("B34-3417-R1_Amber_Emergency_Lens_Material.png",),
        "OPTIONAL_BACKUP",
        "SELECTED_CENTER_CROP_SOURCE",
        True,
        False,
        "Best amber lens source after cropping away hard frame. Use as lamp/lens material source, not broad seamless terrain.",
        (96, 112, 928, 912),
        metallic=0.0,
        roughness=0.28,
        normal_strength=1.35,
    ),
    CandidateSpec(
        "B34-3417-R1",
        "B34-3417",
        "amber_lens_jpeg_timestamp",
        "SEAMLESS_TILE",
        ("Amber_emergency_light_lens_material_202606082255.jpeg",),
        "OPTIONAL_BACKUP",
        "REJECT_REGEN_SEAMLESS_VERTICAL_SEAM",
        False,
        False,
        "Clear vertical seam and rib-density break in 2x2. Keep only as rejected comparison against the PNG crop.",
    ),
    CandidateSpec(
        "B34-3439-V2",
        "B34-3439",
        "spore_pods_unrequested_variant",
        "UV_ATLAS",
        ("Alien_spore_pods_seed_sacs_202606082255.jpeg",),
        "UNREQUESTED_VARIANT",
        "HOLD_ALTERNATE_NOT_TARGETED_EDGE_RISK",
        False,
        False,
        "Strong visual alternate, but outside targeted regen set and high edge content. Keep as alternate reference only.",
    ),
)


def project_rel(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def ensure_dirs() -> None:
    for path in (ORIGINALS_DIR, CLEANED_DIR, DERIVED_DIR, QA_DIR, TILE_PREVIEW_DIR):
        path.mkdir(parents=True, exist_ok=True)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        while True:
            chunk = handle.read(1024 * 1024)
            if not chunk:
                break
            digest.update(chunk)
    return digest.hexdigest().upper()


def discover(spec: CandidateSpec) -> Path | None:
    matches: list[Path] = []
    for pattern in spec.source_globs:
        matches.extend(path for path in DOWNLOADS.glob(pattern) if path.suffix.lower() in IMAGE_EXTS)
    if not matches:
        return None
    return sorted(set(matches), key=lambda path: (path.stat().st_mtime, path.name.lower()))[-1]


def mean_abs_delta(left: Image.Image, right: Image.Image) -> float:
    diff = ImageChops.difference(left.convert("RGB"), right.convert("RGB"))
    arr = np.asarray(diff, dtype=np.float32)
    return float(arr.mean())


def seam_metrics(image: Image.Image) -> dict[str, float]:
    rgb = image.convert("RGB")
    width, height = rgb.size
    band = max(1, min(16, width // 8, height // 8))
    return {
        "edgeLR": round(mean_abs_delta(rgb.crop((0, 0, 1, height)), rgb.crop((width - 1, 0, width, height))), 4),
        "edgeTB": round(mean_abs_delta(rgb.crop((0, 0, width, 1)), rgb.crop((0, height - 1, width, height))), 4),
        "bandLR": round(
            mean_abs_delta(
                rgb.crop((0, 0, band, height)),
                rgb.crop((width - band, 0, width, height)).transpose(Image.Transpose.FLIP_LEFT_RIGHT),
            ),
            4,
        ),
        "bandTB": round(
            mean_abs_delta(
                rgb.crop((0, 0, width, band)),
                rgb.crop((0, height - band, width, height)).transpose(Image.Transpose.FLIP_TOP_BOTTOM),
            ),
            4,
        ),
    }


def clipping_stats(image: Image.Image) -> dict[str, float]:
    arr = np.asarray(image.convert("RGB"), dtype=np.uint8)
    return {
        "blackPct": round(float((arr <= 5).all(axis=2).mean() * 100.0), 3),
        "whitePct": round(float((arr >= 250).all(axis=2).mean() * 100.0), 3),
    }


def edge_content_stats(image: Image.Image) -> dict[str, object]:
    rgb = np.asarray(image.convert("RGB"), dtype=np.float32)
    height, width, _ = rgb.shape
    corner = 32
    corners = np.concatenate(
        [
            rgb[:corner, :corner].reshape(-1, 3),
            rgb[:corner, -corner:].reshape(-1, 3),
            rgb[-corner:, :corner].reshape(-1, 3),
            rgb[-corner:, -corner:].reshape(-1, 3),
        ],
        axis=0,
    )
    background = np.median(corners, axis=0)
    distance = np.abs(rgb - background).mean(axis=2)
    margin = max(8, min(32, width // 16, height // 16))
    edge_mask = np.zeros((height, width), dtype=bool)
    edge_mask[:margin, :] = True
    edge_mask[-margin:, :] = True
    edge_mask[:, :margin] = True
    edge_mask[:, -margin:] = True
    content = distance > 18.0
    return {
        "backgroundRgb": [round(float(value), 2) for value in background],
        "contentPct": round(float(content.mean() * 100.0), 3),
        "edgeContentPct": round(float((content & edge_mask).sum() / max(1, edge_mask.sum()) * 100.0), 3),
    }


def save_2x2_preview(image: Image.Image, name: str) -> str:
    rgb = image.convert("RGB")
    preview = Image.new("RGB", (rgb.width * 2, rgb.height * 2))
    for y in (0, rgb.height):
        for x in (0, rgb.width):
            preview.paste(rgb, (x, y))
    if max(preview.size) > 1800:
        preview.thumbnail((1800, 1800), Image.Resampling.LANCZOS)
    path = TILE_PREVIEW_DIR / f"{name}_2x2.png"
    preview.save(path)
    return project_rel(path)


def copy_original(src: Path, spec: CandidateSpec) -> Path:
    suffix = src.suffix.lower()
    dst = ORIGINALS_DIR / f"{spec.regen_id}_{spec.variant}{suffix}"
    shutil.copy2(src, dst)
    return dst


def cleaned_candidate(image: Image.Image, spec: CandidateSpec) -> tuple[Path | None, dict[str, float] | None, str | None]:
    if spec.crop_box is None:
        return None, None, None
    crop = image.convert("RGB").crop(spec.crop_box).resize((1024, 1024), Image.Resampling.LANCZOS)
    dst = CLEANED_DIR / f"{spec.regen_id}_{spec.variant}_center_crop.png"
    crop.save(dst)
    preview = save_2x2_preview(crop, f"{spec.regen_id}_{spec.variant}_center_crop")
    return dst, seam_metrics(crop), preview


def height_from_base(base: Image.Image) -> Image.Image:
    gray = base.convert("L").filter(ImageFilter.GaussianBlur(radius=0.85))
    values = np.asarray(gray, dtype=np.float32)
    low, high = np.percentile(values, [4, 96])
    if high <= low:
        high = low + 1.0
    normalized = np.clip((values - low) / (high - low), 0.0, 1.0)
    return Image.fromarray(np.uint8(normalized * 255.0), "L")


def normal_from_height(height: Image.Image, strength: float) -> Image.Image:
    data = np.asarray(height, dtype=np.float32) / 255.0
    dx = np.zeros_like(data)
    dy = np.zeros_like(data)
    dx[:, 1:-1] = data[:, 2:] - data[:, :-2]
    dy[1:-1, :] = data[2:, :] - data[:-2, :]
    nx = -dx * strength
    ny = -dy * strength
    nz = np.ones_like(data)
    length = np.maximum(np.sqrt(nx * nx + ny * ny + nz * nz), 0.0001)
    normal = np.stack((nx / length, ny / length, nz / length), axis=2)
    return Image.fromarray(np.uint8(np.clip((normal * 0.5 + 0.5) * 255.0, 0, 255)), "RGB")


def derive_material_maps(base: Image.Image, spec: CandidateSpec) -> dict[str, str]:
    derived_dir = DERIVED_DIR / f"{spec.regen_id}_{spec.variant}"
    derived_dir.mkdir(parents=True, exist_ok=True)
    height = height_from_base(base)
    normal = normal_from_height(height, spec.normal_strength)
    height_values = np.asarray(height, dtype=np.float32) / 255.0
    ao = np.clip(0.70 + height_values * 0.30, 0.0, 1.0)
    metallic = np.full_like(ao, spec.metallic)
    roughness = np.full_like(ao, spec.roughness)
    emission = np.zeros_like(ao)

    normal_path = derived_dir / f"TX_{spec.regen_id}_{spec.variant}_NormalGL.jpg"
    height_path = derived_dir / f"TX_{spec.regen_id}_{spec.variant}_Height.jpg"
    mrao_path = derived_dir / f"TX_{spec.regen_id}_{spec.variant}_MRAO_Provisional.png"
    normal.save(normal_path, "JPEG", quality=90, optimize=True, progressive=True, subsampling=0)
    height.save(height_path, "JPEG", quality=88, optimize=True, progressive=True)
    Image.fromarray(np.uint8(np.stack((metallic, roughness, ao, emission), axis=2) * 255.0), "RGBA").save(mrao_path, "PNG")
    return {
        "NormalGL": project_rel(normal_path),
        "Height": project_rel(height_path),
        "MRAO_Provisional_RGBA_Metal_Rough_AO_Emission": project_rel(mrao_path),
    }


def process_candidate(spec: CandidateSpec) -> dict[str, object]:
    src = discover(spec)
    entry: dict[str, object] = {
        "id": spec.regen_id,
        "sourceId": spec.source_id,
        "variant": spec.variant,
        "sourceType": spec.source_type,
        "priority": spec.priority,
        "decision": spec.decision,
        "selected": spec.selected,
        "broadSeamlessAccepted": spec.broad_seamless_accepted,
        "note": spec.note,
    }
    if src is None:
        entry["missing"] = True
        entry["sourceGlobs"] = list(spec.source_globs)
        return entry

    original = copy_original(src, spec)
    image = Image.open(src)
    entry.update(
        {
            "downloadSource": str(src),
            "originalPath": project_rel(original),
            "width": image.width,
            "height": image.height,
            "mode": image.mode,
            "bytes": src.stat().st_size,
            "sha256": sha256(src),
            "clipping": clipping_stats(image),
        }
    )

    if spec.source_type == "SEAMLESS_TILE":
        entry["seamMetrics"] = seam_metrics(image)
        entry["tilePreviewPath"] = save_2x2_preview(image, f"{spec.regen_id}_{spec.variant}")
    else:
        entry["edgeContent"] = edge_content_stats(image)

    cleaned, cleaned_seams, cleaned_preview = cleaned_candidate(image, spec)
    if cleaned is not None:
        entry["cleanedCandidatePath"] = project_rel(cleaned)
        entry["cleanedSeamMetrics"] = cleaned_seams
        entry["cleanedTilePreviewPath"] = cleaned_preview
        entry["finalCandidatePath"] = project_rel(cleaned)
        final_image = Image.open(cleaned)
    elif spec.selected:
        entry["finalCandidatePath"] = project_rel(original)
        final_image = image
    else:
        final_image = None

    if spec.selected and spec.source_type == "SEAMLESS_TILE" and final_image is not None:
        entry["maps"] = derive_material_maps(final_image, spec)

    return entry


def contact_sheet(entries: list[dict[str, object]]) -> str:
    cards: list[Image.Image] = []
    for entry in entries:
        original = entry.get("originalPath")
        if not original:
            continue
        image = Image.open(ROOT / str(original)).convert("RGB")
        thumb = ImageOps.contain(image, (360, 330), Image.Resampling.LANCZOS)
        card = Image.new("RGB", (380, 470), (24, 24, 24))
        card.paste(thumb, ((380 - thumb.width) // 2, 10))
        draw = ImageDraw.Draw(card)
        label = (
            f"{entry['id']}\n"
            f"{entry['variant']}\n"
            f"{entry['decision']}\n"
            f"selected={entry['selected']}"
        )
        if "seamMetrics" in entry:
            seam = entry["seamMetrics"]
            assert isinstance(seam, dict)
            label += f"\nraw seam {seam['bandLR']}/{seam['bandTB']}"
        if "cleanedSeamMetrics" in entry:
            seam = entry["cleanedSeamMetrics"]
            assert isinstance(seam, dict)
            label += f"\nclean seam {seam['bandLR']}/{seam['bandTB']}"
        if "edgeContent" in entry:
            edge = entry["edgeContent"]
            assert isinstance(edge, dict)
            label += f"\nedgeContent {edge['edgeContentPct']}%"
        draw.multiline_text((12, 350), label, fill=(235, 235, 235), spacing=3)
        cards.append(card)

    columns = 3
    rows = max(1, math.ceil(len(cards) / columns))
    sheet = Image.new("RGB", (columns * 380, rows * 470), (10, 10, 10))
    for index, card in enumerate(cards):
        sheet.paste(card, ((index % columns) * 380, (index // columns) * 470))
    path = QA_DIR / "PREVIEW_Batch34_RegenTargets_Contact.png"
    sheet.save(path)
    return project_rel(path)


def main() -> int:
    ensure_dirs()
    entries = [process_candidate(spec) for spec in CANDIDATES]
    selected = [entry for entry in entries if entry.get("selected")]
    contact = contact_sheet(entries)
    manifest = {
        "schema": "hecton8.batch34.regen_targets.intake.v2",
        "outputRoot": project_rel(OUTPUT_ROOT),
        "operatorPrompt": "Docs/GeneratedAssets/Gemini/Prompts/Batch34/3406_TEXTURE_SOURCE_REGEN_TARGETS_20260608.md",
        "entries": entries,
        "selectedFinalCandidates": [
            {
                "id": entry["id"],
                "sourceId": entry["sourceId"],
                "variant": entry["variant"],
                "decision": entry["decision"],
                "finalCandidatePath": entry.get("finalCandidatePath", ""),
                "broadSeamlessAccepted": entry["broadSeamlessAccepted"],
            }
            for entry in selected
        ],
        "contactSheet": contact,
    }
    manifest_path = QA_DIR / "Batch34_RegenTargets_IntakeManifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print("BATCH34_REGEN_TARGETS_PROCESSOR")
    print(f"manifest={project_rel(manifest_path)}")
    print(f"contact={contact}")
    print(f"entries={len(entries)}")
    print(f"selected={len(selected)}")
    missing = [entry for entry in entries if entry.get("missing")]
    print(f"missing={len(missing)}")
    for entry in entries:
        print(f"{entry['id']} {entry['variant']} {entry['decision']} selected={entry['selected']}")
    return 1 if missing else 0


if __name__ == "__main__":
    raise SystemExit(main())
