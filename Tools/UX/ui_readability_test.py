#!/usr/bin/env python3
"""Offline readability test for hardware-adaptive HECTON-8 UI text."""

from __future__ import annotations

import argparse
import json
import math
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont


SCRIPT_PATH = Path(__file__).resolve()
ROOT = SCRIPT_PATH.parents[2]
SPEC_PATH = ROOT / "Docs" / "Design" / "HardwareAdaptiveUIScaler.json"
DEFAULT_REPORT = ROOT / "Docs" / "AgentLogs" / "UI_Readability_UX_ENGINEER.json"
FONT_CANDIDATES = (
    ROOT / "Assets" / "TextMesh Pro" / "Fonts" / "LiberationSans.ttf",
    Path("C:/Windows/Fonts/consolab.ttf"),
    Path("C:/Windows/Fonts/arialbd.ttf"),
    Path("C:/Windows/Fonts/segoeuib.ttf"),
)


@dataclass(frozen=True)
class ReadabilityResult:
    profile: str
    contrast_delta: float
    template_correlation: float
    ink_survival: float
    status: str


def parse_hex_color(value: str) -> tuple[int, int, int, int]:
    text = value.strip().lstrip("#")
    if len(text) == 6:
        text += "FF"
    if len(text) != 8:
        raise ValueError(f"invalid color: {value}")
    return tuple(int(text[index:index + 2], 16) for index in range(0, 8, 2))  # type: ignore[return-value]


def load_font(size: int) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    for candidate in FONT_CANDIDATES:
        if candidate.exists():
            return ImageFont.truetype(str(candidate), size=size)
    return ImageFont.load_default()


def text_bbox(text: str, font: ImageFont.FreeTypeFont | ImageFont.ImageFont, stroke_width: int) -> tuple[int, int, int, int]:
    scratch = Image.new("L", (1, 1), 0)
    draw = ImageDraw.Draw(scratch)
    return draw.textbbox((0, 0), text, font=font, stroke_width=stroke_width)


def render_text_mask(text: str, font_size: int, stroke_width: int) -> Image.Image:
    font = load_font(font_size)
    left, top, right, bottom = text_bbox(text, font, stroke_width)
    width = max(1, right - left + 32)
    height = max(1, bottom - top + 28)
    mask = Image.new("L", (width, height), 0)
    draw = ImageDraw.Draw(mask)
    draw.text(
        ((width - (right - left)) // 2 - left, (height - (bottom - top)) // 2 - top),
        text,
        fill=255,
        font=font,
        stroke_width=stroke_width,
        stroke_fill=255,
    )
    return mask


def degrade(image: Image.Image, blur_radius: float, downsample_scale: float) -> Image.Image:
    blurred = image.filter(ImageFilter.GaussianBlur(radius=max(0.0, blur_radius)))
    scale = min(1.0, max(0.08, downsample_scale))
    small_size = (
        max(1, int(round(blurred.width * scale))),
        max(1, int(round(blurred.height * scale))),
    )
    small = blurred.resize(small_size, Image.Resampling.BILINEAR)
    return small.resize(blurred.size, Image.Resampling.BILINEAR)


def compose(mask: Image.Image, foreground: tuple[int, int, int, int], background: tuple[int, int, int, int]) -> Image.Image:
    base = Image.new("RGBA", mask.size, background)
    ink = Image.new("RGBA", mask.size, foreground)
    base.paste(ink, (0, 0), mask)
    return base.convert("L")


def mean(values: list[float]) -> float:
    return sum(values) / max(1, len(values))


def pearson(a: list[float], b: list[float]) -> float:
    if len(a) != len(b) or not a:
        return 0.0
    mean_a = mean(a)
    mean_b = mean(b)
    numerator = 0.0
    denom_a = 0.0
    denom_b = 0.0
    for index in range(len(a)):
        da = a[index] - mean_a
        db = b[index] - mean_b
        numerator += da * db
        denom_a += da * da
        denom_b += db * db
    denom = math.sqrt(max(1e-9, denom_a * denom_b))
    return numerator / denom


def evaluate_profile(profile: dict, contrast: dict, thresholds: dict, sample_text: str) -> ReadabilityResult:
    stroke_width = int(profile["readabilityStrokePx"])
    mask = render_text_mask(sample_text, int(profile["readabilityFontPx"]), stroke_width)
    clean = compose(mask, parse_hex_color(contrast["primary"]), parse_hex_color(contrast["background"]))
    degraded = degrade(clean, float(profile["blurRadiusPx"]), float(profile["downsampleScale"]))

    clean_values = [value / 255.0 for value in mask.getdata()]
    degraded_values = [value / 255.0 for value in degraded.getdata()]
    inside: list[float] = []
    outside: list[float] = []
    for alpha, luminance in zip(clean_values, degraded_values):
        if alpha > 0.5:
            inside.append(luminance)
        elif alpha < 0.02:
            outside.append(luminance)

    inside_mean = mean(inside)
    outside_mean = mean(outside)
    contrast_delta = abs(inside_mean - outside_mean)
    template_correlation = pearson(clean_values, degraded_values)
    ink_survival = sum(1 for value in inside if abs(value - outside_mean) > 0.18) / max(1, len(inside))

    passed = (
        contrast_delta >= float(thresholds["minimumContrastDelta"])
        and template_correlation >= float(thresholds["minimumTemplateCorrelation"])
        and ink_survival >= float(thresholds["minimumInkSurvival"])
    )
    return ReadabilityResult(
        str(profile["id"]),
        round(contrast_delta, 5),
        round(template_correlation, 5),
        round(ink_survival, 5),
        "PASS" if passed else "FAIL",
    )


def build_report(spec_path: Path) -> dict:
    spec = json.loads(spec_path.read_text(encoding="utf-8"))
    sample_text = str(spec.get("sampleText", "O2 LOW"))
    thresholds = spec["readability"]
    contrast_by_id = {entry["id"]: entry for entry in spec["contrastProfiles"]}
    toaster_contrast = contrast_by_id["TOASTER"]
    results = [
        evaluate_profile(profile, toaster_contrast, thresholds, sample_text)
        for profile in spec["sdfProfiles"]
    ]
    errors = [
        f"{result.profile} readability failed"
        for result in results
        if result.status != "PASS"
    ]
    return {
        "schema": "hecton8.ui_readability_report.v1",
        "owner": "UX_ENGINEER",
        "promptId": "HARDWARE_ADAPTIVE_UI_BAKER",
        "status": "PASS" if not errors else "FAIL",
        "spec": str(spec_path.relative_to(ROOT)).replace("\\", "/"),
        "sampleText": sample_text,
        "results": [result.__dict__ for result in results],
        "errors": errors,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Render and blur-test HECTON-8 UI text readability.")
    parser.add_argument("--spec", default=str(SPEC_PATH), help="Hardware adaptive UI scaler JSON.")
    parser.add_argument("--write-report", nargs="?", const=str(DEFAULT_REPORT), default="", help="Optional report path.")
    args = parser.parse_args()

    spec_path = Path(args.spec).resolve()
    report = build_report(spec_path)
    print("UI_READABILITY_TEST")
    for result in report["results"]:
        print(
            "{profile}: contrast={contrast_delta:.3f} corr={template_correlation:.3f} "
            "ink={ink_survival:.3f} status={status}".format(**result)
        )

    if args.write_report:
        report_path = Path(args.write_report).resolve()
        report_path.parent.mkdir(parents=True, exist_ok=True)
        report_path.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        print(f"report={report_path}")

    if report["errors"]:
        print("STATUS: FAIL")
        for error in report["errors"]:
            print(f"ERROR: {error}")
        return 1

    print("STATUS: PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
