#!/usr/bin/env python3
"""Static intake audit for Gemini/AI-assisted texture candidates.

This tool does not edit Unity assets. It scans downloaded candidate images,
computes basic material-source QA, writes a CSV/Markdown report, and creates
2x2 tile previews for seam inspection.
"""

from __future__ import annotations

import argparse
import csv
import math
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageChops, ImageStat


IMAGE_EXTS = {".png", ".jpg", ".jpeg", ".webp", ".tif", ".tiff"}
DEFAULT_ROOT = "Docs/GeneratedAssets/Gemini"
DEFAULT_OUT_DIR = "Docs/GeneratedAssets/Gemini/QA"


@dataclass
class Finding:
    path: str
    width: int
    height: int
    mode: str
    role: str
    verdict: str
    issues: str
    warnings: str
    seam_lr_mean: float
    seam_tb_mean: float
    seam_lr_band_mean: float
    seam_tb_band_mean: float
    luminance_mean: float
    luminance_min: int
    luminance_max: int
    luminance_zero_pct: float
    luminance_full_pct: float
    channel_saturation_pct: float
    aspect: str
    preview_path: str


def rel(path: Path, root: Path) -> str:
    try:
        return path.relative_to(root).as_posix()
    except ValueError:
        return path.as_posix()


def classify_role(path: Path) -> str:
    name = path.stem.lower()
    if "albedo" in name:
        return "Albedo"
    if any(token in name for token in ("normal", "_nrm", "_n_")):
        return "Normal"
    if any(token in name for token in ("mrao", "orm", "arm", "mask", "rough", "ao", "height")):
        return "Mask"
    if any(token in name for token in ("emiss", "glow", "biolum")):
        return "Emission"
    return "Albedo"


def mean_abs_delta(left: Image.Image, right: Image.Image) -> float:
    diff = ImageChops.difference(left.convert("RGB"), right.convert("RGB"))
    stat = ImageStat.Stat(diff)
    return float(sum(stat.mean) / len(stat.mean))


def luminance_stats(image: Image.Image) -> tuple[float, int, int]:
    gray = image.convert("L")
    stat = ImageStat.Stat(gray)
    extrema = gray.getextrema()
    return float(stat.mean[0]), int(extrema[0]), int(extrema[1])


def edge_band_delta(rgb: Image.Image, band_width: int = 8) -> tuple[float, float]:
    width, height = rgb.size
    band = max(1, min(band_width, width // 8, height // 8))
    left = rgb.crop((0, 0, band, height))
    right = rgb.crop((width - band, 0, width, height)).transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    top = rgb.crop((0, 0, width, band))
    bottom = rgb.crop((0, height - band, width, height)).transpose(Image.Transpose.FLIP_TOP_BOTTOM)
    return mean_abs_delta(left, right), mean_abs_delta(top, bottom)


def clipping_stats(image: Image.Image) -> tuple[float, float, float]:
    rgb = image.convert("RGB")
    gray_values = list(rgb.convert("L").getdata())
    pixel_count = max(1, len(gray_values))
    zero_pct = sum(1 for value in gray_values if value <= 1) * 100.0 / pixel_count
    full_pct = sum(1 for value in gray_values if value >= 254) * 100.0 / pixel_count

    saturated_channels = 0
    for pixel in rgb.getdata():
        saturated_channels += sum(1 for channel in pixel if channel <= 1 or channel >= 254)
    channel_saturation_pct = saturated_channels * 100.0 / max(1, pixel_count * 3)
    return zero_pct, full_pct, channel_saturation_pct


def make_tile_preview(image: Image.Image, out_path: Path, max_tile: int) -> None:
    source = image.convert("RGB")
    width, height = source.size
    scale = min(1.0, max_tile / float(max(width, height)))
    if scale < 1.0:
        source = source.resize((max(1, int(width * scale)), max(1, int(height * scale))), Image.Resampling.LANCZOS)
    tile = Image.new("RGB", (source.width * 2, source.height * 2))
    tile.paste(source, (0, 0))
    tile.paste(source, (source.width, 0))
    tile.paste(source, (0, source.height))
    tile.paste(source, (source.width, source.height))
    out_path.parent.mkdir(parents=True, exist_ok=True)
    tile.save(out_path)


def audit_image(path: Path, project_root: Path, out_dir: Path, max_tile: int) -> Finding:
    role = classify_role(path)
    issues: list[str] = []
    warnings: list[str] = []

    with Image.open(path) as image:
        image.load()
        width, height = image.size
        mode = image.mode
        rgb = image.convert("RGB")

        if width != height:
            issues.append("not_square")
        if width < 1024 or height < 1024:
            warnings.append("below_1024_source")
        if width & (width - 1) != 0 or height & (height - 1) != 0:
            warnings.append("not_power_of_two")
        if path.suffix.lower() in {".jpg", ".jpeg"}:
            warnings.append("lossy_source")

        left = rgb.crop((0, 0, 1, height))
        right = rgb.crop((width - 1, 0, width, height))
        top = rgb.crop((0, 0, width, 1))
        bottom = rgb.crop((0, height - 1, width, height))
        seam_lr = mean_abs_delta(left, right)
        seam_tb = mean_abs_delta(top, bottom)
        seam_lr_band, seam_tb_band = edge_band_delta(rgb)

        if seam_lr > 18.0:
            issues.append("left_right_edge_mismatch")
        elif seam_lr > 10.0:
            warnings.append("left_right_edge_warning")
        if seam_tb > 18.0:
            issues.append("top_bottom_edge_mismatch")
        elif seam_tb > 10.0:
            warnings.append("top_bottom_edge_warning")
        if seam_lr_band > 22.0:
            issues.append("left_right_band_mismatch")
        elif seam_lr_band > 14.0:
            warnings.append("left_right_band_warning")
        if seam_tb_band > 22.0:
            issues.append("top_bottom_band_mismatch")
        elif seam_tb_band > 14.0:
            warnings.append("top_bottom_band_warning")

        lum_mean, lum_min, lum_max = luminance_stats(rgb)
        lum_zero_pct, lum_full_pct, channel_sat_pct = clipping_stats(rgb)
        if role == "Albedo":
            if lum_mean < 45.0:
                issues.append("albedo_too_dark_for_surface_shallows")
            if lum_max > 252 and lum_min < 3:
                warnings.append("possible_crushed_range_or_baked_lighting")
            if lum_zero_pct > 5.0:
                issues.append("albedo_clipped_black_pixels")
            elif lum_zero_pct > 1.0:
                warnings.append("albedo_black_clip_warning")
            if lum_full_pct > 1.0:
                issues.append("albedo_clipped_white_pixels")
            elif lum_full_pct > 0.25:
                warnings.append("albedo_white_clip_warning")
            if channel_sat_pct > 12.0:
                issues.append("albedo_channel_saturation")
            elif channel_sat_pct > 4.0:
                warnings.append("albedo_channel_saturation_warning")
            if lum_max - lum_min < 45:
                warnings.append("low_luminance_contrast")
        elif role == "Normal":
            if lum_mean < 85.0 or lum_mean > 175.0:
                warnings.append("normal_map_luminance_unusual")
        else:
            if lum_max - lum_min < 24:
                warnings.append("low_mask_channel_variation")

        preview_name = f"{path.stem}_tile2x2.png"
        preview_path = out_dir / "tile_previews" / preview_name
        make_tile_preview(rgb, preview_path, max_tile)

    verdict = "REJECT" if issues else ("REVIEW" if warnings else "PASS_STATIC")
    aspect = "square" if width == height else f"{width}:{height}"
    return Finding(
        path=rel(path, project_root),
        width=width,
        height=height,
        mode=mode,
        role=role,
        verdict=verdict,
        issues=";".join(issues),
        warnings=";".join(warnings),
        seam_lr_mean=round(seam_lr, 3),
        seam_tb_mean=round(seam_tb, 3),
        seam_lr_band_mean=round(seam_lr_band, 3),
        seam_tb_band_mean=round(seam_tb_band, 3),
        luminance_mean=round(lum_mean, 3),
        luminance_min=lum_min,
        luminance_max=lum_max,
        luminance_zero_pct=round(lum_zero_pct, 3),
        luminance_full_pct=round(lum_full_pct, 3),
        channel_saturation_pct=round(channel_sat_pct, 3),
        aspect=aspect,
        preview_path=rel(preview_path, project_root),
    )


def iter_images(root: Path) -> list[Path]:
    if root.is_file() and root.suffix.lower() in IMAGE_EXTS:
        return [root]
    if not root.exists():
        return []
    result: list[Path] = []
    for path in root.rglob("*"):
        if path.suffix.lower() not in IMAGE_EXTS:
            continue
        if "/QA/" in path.as_posix() or "\\QA\\" in str(path):
            continue
        result.append(path)
    return sorted(result)


def write_csv(path: Path, findings: list[Finding]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=list(Finding.__dataclass_fields__.keys()))
        writer.writeheader()
        for finding in findings:
            writer.writerow(finding.__dict__)


def write_markdown(path: Path, findings: list[Finding], scanned_root: Path, project_root: Path) -> None:
    counts: dict[str, int] = {}
    for finding in findings:
        counts[finding.verdict] = counts.get(finding.verdict, 0) + 1

    lines = [
        "# Gemini Texture Intake Audit",
        "",
        "Evidence class: STATIC_IMAGE_QA.",
        "Unity was not run. No Assets were edited.",
        "",
        f"Scanned root: `{rel(scanned_root, project_root)}`",
        f"Images scanned: {len(findings)}",
        f"PASS_STATIC: {counts.get('PASS_STATIC', 0)}",
        f"REVIEW: {counts.get('REVIEW', 0)}",
        f"REJECT: {counts.get('REJECT', 0)}",
        "",
        "## Rules",
        "",
        "- `REJECT` means at least one hard static issue exists: non-square source, severe seam/band mismatch, too-dark albedo, clipped albedo, or saturated channel data.",
        "- `REVIEW` means no hard static issue, but source is lossy, low-res, not power-of-two, has moderate seams, or has suspicious luminance/channel behavior.",
        "- `PASS_STATIC` is still not Unity acceptance. It only means this intake gate found no static blocker.",
        "- Every accepted candidate still needs PBR channel manifest, import settings, material binding, 2x2 visual review, and Unity screenshot proof.",
        "",
        "## Findings",
        "",
        "| Verdict | Role | Size | LR seam | TB seam | LR band | TB band | Lum mean | Clip 0/255 | Sat ch | Path | Preview |",
        "|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---|---|",
    ]
    for finding in findings:
        lines.append(
            f"| {finding.verdict} | {finding.role} | {finding.width}x{finding.height} | "
            f"{finding.seam_lr_mean:.3f} | {finding.seam_tb_mean:.3f} | "
            f"{finding.seam_lr_band_mean:.3f} | {finding.seam_tb_band_mean:.3f} | "
            f"{finding.luminance_mean:.3f} | "
            f"{finding.luminance_zero_pct:.2f}%/{finding.luminance_full_pct:.2f}% | "
            f"{finding.channel_saturation_pct:.2f}% | "
            f"`{finding.path}` | `{finding.preview_path}` |"
        )
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def build_contact_sheet(project_root: Path, out_dir: Path, findings: list[Finding], max_thumb: int) -> None:
    preview_paths = [project_root / finding.preview_path for finding in findings if finding.preview_path]
    previews = []
    for path in preview_paths:
        try:
            with Image.open(path) as image:
                thumb = image.convert("RGB")
                thumb.thumbnail((max_thumb, max_thumb), Image.Resampling.LANCZOS)
                previews.append((path, thumb.copy()))
        except OSError:
            continue
    if not previews:
        return
    columns = min(4, len(previews))
    rows = int(math.ceil(len(previews) / columns))
    cell = max_thumb + 12
    sheet = Image.new("RGB", (columns * cell, rows * cell), (20, 20, 20))
    for index, (_, image) in enumerate(previews):
        x = (index % columns) * cell + 6
        y = (index // columns) * cell + 6
        sheet.paste(image, (x, y))
    sheet.save(out_dir / "GeminiTextureIntake_contact_sheet.png")


def main() -> int:
    parser = argparse.ArgumentParser(description="Audit Gemini texture candidates before Unity import.")
    parser.add_argument("--project-root", default=".", help="Project root.")
    parser.add_argument("--root", default=DEFAULT_ROOT, help="Image or directory to scan.")
    parser.add_argument("--out-dir", default=DEFAULT_OUT_DIR, help="Output directory.")
    parser.add_argument("--max-tile-preview", type=int, default=512, help="Max source tile size for 2x2 previews.")
    parser.add_argument("--max-contact-thumb", type=int, default=256, help="Contact sheet thumbnail size.")
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    scan_root = (project_root / args.root).resolve()
    out_dir = (project_root / args.out_dir).resolve()

    findings = [audit_image(path, project_root, out_dir, args.max_tile_preview) for path in iter_images(scan_root)]
    write_csv(out_dir / "GeminiTextureIntakeAudit.csv", findings)
    write_markdown(out_dir / "GeminiTextureIntakeAudit.md", findings, scan_root, project_root)
    build_contact_sheet(project_root, out_dir, findings, args.max_contact_thumb)

    reject_count = sum(1 for finding in findings if finding.verdict == "REJECT")
    review_count = sum(1 for finding in findings if finding.verdict == "REVIEW")
    print(
        "GEMINI_TEXTURE_INTAKE_AUDIT_DONE "
        f"images={len(findings)} reject={reject_count} review={review_count} "
        f"out={rel(out_dir, project_root)}"
    )
    return 1 if reject_count else 0


if __name__ == "__main__":
    raise SystemExit(main())
