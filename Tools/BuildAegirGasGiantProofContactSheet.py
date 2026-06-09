#!/usr/bin/env python3
"""Build deterministic offline Aegir gas giant proof contact sheet.

This does not replace Unity/Frame Debugger proof. It provides a reproducible
source-texture proof artifact for phase, limb darkening, horizon haze, clouds,
weather-driven storm emission, and underwater-up readability while Unity
process gates are blocked. Low quality cases deliberately keep phase, limb,
and large-scale bands while shedding fine detail.
"""

from __future__ import annotations

import json
import math
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[1]

CANONICAL_BAND_TEXTURE = "Assets/_Project/Art/TEXTURES/clouds0_diff.png"
CANONICAL_DETAIL_TEXTURE = "Assets/_Project/Art/TEXTURES/Sky/oblakajip.png"
CANONICAL_STORM_TEXTURE = "Assets/_Project/Art/TEXTURES/Aegir_storms.png"
CANONICAL_BAND_GUID = "6c173d4e1a858b34ca1b7e5610aae988"
CANONICAL_DETAIL_GUID = "e1aefa60ab4517644bb884257440872b"
CANONICAL_STORM_GUID = "d9d11072e85a2b54cacd11eaad6614a8"

OUTPUT_DIR = "Docs/GeneratedAssets/AegirGasGiantProof"
OUTPUT_IMAGE = f"{OUTPUT_DIR}/AegirGasGiantProofContactSheet_20260608.png"
OUTPUT_MANIFEST = f"{OUTPUT_DIR}/AegirGasGiantProofContactSheet_20260608.json"

CELL_W = 640
CELL_H = 420
CAPTION_H = 46
GRID_COLUMNS = 3
LOW_RES_CLOUD_DIVISOR = 4
SAMPLER_BAND_SIZE = (1024, 512)
SAMPLER_DETAIL_SIZE = (512, 512)
SAMPLER_STORM_SIZE = (1024, 512)


@dataclass(frozen=True)
class ProofCase:
    view_id: str
    caption: str
    mode: str
    center_x: int
    center_y: int
    radius: int
    phase_degrees: float
    rotation: float
    fog: float
    cloud: float
    horizon: float
    underwater: float
    exposure: float
    storm_emission: float
    quality_weight: float


PROOF_CASES = (
    ProofCase("surface_clear_full", "surface view, full phase, calm bands hold", "surface", 392, 168, 142, 18.0, 0.12, 0.12, 0.05, 0.0, 0.0, 1.08, 0.82, 1.0),
    ProofCase("surface_cloud_fog_half", "surface view, half phase, cloud/fog storm lift", "surface", 376, 158, 136, 72.0, 0.34, 0.34, 0.42, 0.0, 0.0, 1.0, 1.22, 0.92),
    ProofCase("underwater_up", "underwater-up view, distortion and blue attenuation", "underwater", 332, 162, 128, 48.0, 0.58, 0.22, 0.16, 0.0, 1.0, 0.88, 1.0, 0.72),
    ProofCase("horizon_veil", "horizon view, storm bands behind atmospheric veil", "horizon", 394, 268, 226, 36.0, 0.22, 0.48, 0.20, 0.42, 0.0, 1.04, 1.48, 1.0),
    ProofCase("crescent_low_light", "late phase crescent, terminator not a flat decal", "surface", 360, 154, 135, 126.0, 0.72, 0.18, 0.08, 0.0, 0.0, 0.94, 0.92, 0.84),
    ProofCase("heavy_fog_occlusion", "low-tier cloud/fog fallback, high storm silhouette", "horizon", 366, 210, 168, 94.0, 0.44, 0.68, 0.58, 0.30, 0.0, 0.86, 1.92, 0.48),
)


def project_path(root: Path, rel_path: str) -> Path:
    return root / rel_path.replace("/", "\\")


def clamp01(value: float) -> float:
    return 0.0 if value < 0.0 else 1.0 if value > 1.0 else value


def smoothstep(edge0: float, edge1: float, value: float) -> float:
    if edge0 == edge1:
        return 1.0 if value >= edge1 else 0.0
    t = clamp01((value - edge0) / (edge1 - edge0))
    return t * t * (3.0 - 2.0 * t)


def lerp(a: float, b: float, t: float) -> float:
    return a + (b - a) * t


def normalize3(x: float, y: float, z: float) -> tuple[float, float, float]:
    length = math.sqrt(x * x + y * y + z * z)
    if length <= 1e-8 or not math.isfinite(length):
        return 0.0, 0.0, 1.0
    inv = 1.0 / length
    return x * inv, y * inv, z * inv


def sample_rgb(image: Image.Image, u: float, v: float) -> tuple[float, float, float]:
    width, height = image.size
    u = u % 1.0
    v = clamp01(v)
    x = u * (width - 1)
    y = v * (height - 1)
    x0 = int(math.floor(x))
    y0 = int(math.floor(y))
    x1 = (x0 + 1) % width
    y1 = min(y0 + 1, height - 1)
    tx = x - x0
    ty = y - y0
    px = image.load()

    def at(ix: int, iy: int) -> tuple[float, float, float]:
        raw = px[ix, iy]
        return raw[0] / 255.0, raw[1] / 255.0, raw[2] / 255.0

    c00 = at(x0, y0)
    c10 = at(x1, y0)
    c01 = at(x0, y1)
    c11 = at(x1, y1)
    return tuple(
        lerp(lerp(c00[i], c10[i], tx), lerp(c01[i], c11[i], tx), ty)
        for i in range(3)
    )


def add_gradient_background(image: Image.Image, case: ProofCase) -> None:
    px = image.load()
    if case.mode == "underwater":
        top = (13, 54, 73)
        bottom = (3, 16, 29)
    elif case.mode == "horizon":
        top = (14, 22, 45)
        bottom = (89, 107, 121)
    else:
        top = (10, 18, 37)
        bottom = (38, 63, 93)

    for y in range(CELL_H):
        t = y / max(1, CELL_H - 1)
        for x in range(CELL_W):
            px[x, y] = tuple(int(lerp(top[i], bottom[i], t)) for i in range(3))


def draw_background_clouds(image: Image.Image, detail: Image.Image, case: ProofCase) -> None:
    if case.cloud <= 0.001 and case.fog <= 0.001:
        return

    low_w = max(1, CELL_W // LOW_RES_CLOUD_DIVISOR)
    low_h = max(1, CELL_H // LOW_RES_CLOUD_DIVISOR)
    low = image.resize((low_w, low_h), Image.Resampling.BILINEAR)
    px = low.load()
    for y in range(low_h):
        v = y / low_h
        for x in range(low_w):
            u = x / low_w
            n0 = sample_rgb(detail, u * 0.55 + case.rotation * 0.21, v * 0.36 + 0.08)
            n1 = sample_rgb(detail, u * 1.1 + 0.37, v * 0.72 + case.rotation * 0.13)
            cloud = smoothstep(0.52, 0.86, (n0[0] + n1[1]) * 0.5)
            fog_band = smoothstep(0.20, 0.92, v) * case.fog
            amount = clamp01(cloud * case.cloud * 0.35 + fog_band * 0.46)
            if amount <= 0.001:
                continue
            tint = (128, 151, 160) if case.mode != "underwater" else (62, 118, 129)
            current = px[x, y]
            px[x, y] = tuple(int(lerp(current[i], tint[i], amount)) for i in range(3))
    image.paste(low.resize((CELL_W, CELL_H), Image.Resampling.BICUBIC))


def draw_horizon(image: Image.Image, detail: Image.Image, case: ProofCase) -> None:
    if case.horizon <= 0.001:
        return

    draw = ImageDraw.Draw(image, "RGBA")
    horizon_y = int(CELL_H * (0.66 - case.horizon * 0.12))
    draw.rectangle((0, horizon_y, CELL_W, CELL_H), fill=(17, 32, 45, 205))
    draw.rectangle((0, horizon_y - 16, CELL_W, horizon_y + 34), fill=(137, 151, 154, int(130 * case.fog + 54)))
    px = image.load()
    for y in range(max(0, horizon_y - 22), min(CELL_H, horizon_y + 52)):
        v = y / CELL_H
        for x in range(CELL_W):
            n = sample_rgb(detail, x / CELL_W * 1.4 + 0.12, v * 2.1 + case.rotation)
            amount = smoothstep(0.36, 0.8, n[0]) * 0.28
            if amount <= 0.001:
                continue
            current = px[x, y]
            px[x, y] = tuple(int(lerp(current[i], (161, 170, 165)[i], amount)) for i in range(3))


def draw_planet_halo(image: Image.Image, case: ProofCase) -> None:
    halo = Image.new("RGBA", image.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(halo, "RGBA")
    for expand, alpha in ((34, 34), (18, 44), (7, 54)):
        box = (
            case.center_x - case.radius - expand,
            case.center_y - case.radius - expand,
            case.center_x + case.radius + expand,
            case.center_y + case.radius + expand,
        )
        draw.ellipse(box, outline=(143, 175, 188, int(alpha * (0.5 + case.fog))), width=max(2, expand // 8))
    halo = halo.filter(ImageFilter.GaussianBlur(12))
    image.paste(Image.alpha_composite(image.convert("RGBA"), halo).convert("RGB"))


def render_planet(image: Image.Image, band: Image.Image, detail: Image.Image, storm: Image.Image, case: ProofCase) -> None:
    draw_planet_halo(image, case)
    px = image.load()
    cx = case.center_x
    cy = case.center_y
    radius = case.radius
    phase = math.radians(case.phase_degrees)
    lx, ly, lz = normalize3(math.sin(phase), 0.18, math.cos(phase))
    atmosphere = (0.57, 0.71, 0.78)
    warm = (1.06, 0.94, 0.78)
    quality = clamp01(case.quality_weight)
    detail_weight = lerp(0.42, 1.0, quality)
    storm_response = min(4.0, max(0.0, case.storm_emission)) * lerp(0.68, 1.0, quality)

    min_x = max(0, cx - radius - 2)
    max_x = min(CELL_W - 1, cx + radius + 2)
    min_y = max(0, cy - radius - 2)
    max_y = min(CELL_H - 1, cy + radius + 2)

    for y in range(min_y, max_y + 1):
        ny = (y - cy) / radius
        for x in range(min_x, max_x + 1):
            nx = (x - cx) / radius
            d2 = nx * nx + ny * ny
            if d2 > 1.0:
                continue

            z = math.sqrt(max(0.0, 1.0 - d2))
            normal_x, normal_y, normal_z = normalize3(nx, -ny, z)
            latitude = math.asin(max(-1.0, min(1.0, normal_y)))
            longitude = math.atan2(normal_x, normal_z)
            v = 0.5 - latitude / math.pi
            u = 0.5 + longitude / (2.0 * math.pi)
            band_warp = lerp(0.012, 0.018, quality) * math.sin(normal_y * 24.0 + case.rotation * 11.0)
            shear = lerp(0.006, 0.010, quality) * math.sin(v * 54.0 + case.rotation * 7.0)
            u = u + case.rotation + band_warp + shear

            base = sample_rgb(band, u, v)
            fine = sample_rgb(detail, u * 1.8 + 0.17, v * 1.1 + case.rotation)
            storm_mask = sample_rgb(storm, u + 0.04, v)
            storm_intensity = smoothstep(0.22, 0.72, max(storm_mask)) * storm_response
            detail_gain = 0.96 + (0.16 * fine[1] - 0.04) * detail_weight
            color = [base[i] * detail_gain for i in range(3)]
            color = [lerp(color[i], min(1.0, color[i] * warm[i] + 0.16), clamp01(storm_intensity * 0.38)) for i in range(3)]

            ndotl = normal_x * lx + normal_y * ly + normal_z * lz
            terminator = smoothstep(-0.08, 0.18, ndotl)
            limb = math.sqrt(max(0.0, z))
            limb_darken = lerp(0.56, 1.0, limb)
            shade = (0.075 + 0.925 * terminator) * limb_darken * case.exposure
            color = [c * shade for c in color]

            rim = smoothstep(0.68, 1.0, math.sqrt(d2))
            scatter = rim * (0.36 + case.fog * 0.34)
            color = [lerp(color[i], atmosphere[i], scatter) for i in range(3)]
            color[2] += storm_intensity * 0.025

            alpha = 1.0 - smoothstep(0.985, 1.0, d2)
            current = px[x, y]
            out = tuple(
                int(lerp(current[i] / 255.0, clamp01(color[i]), alpha) * 255.0)
                for i in range(3)
            )
            px[x, y] = out


def apply_underwater_distortion(image: Image.Image, detail: Image.Image, case: ProofCase) -> Image.Image:
    if case.underwater <= 0.001:
        return image

    source = image.copy()
    src = source.load()
    dst = image.load()
    for y in range(CELL_H):
        row_wave = math.sin(y * 0.074 + case.rotation * 14.0) * 4.5
        for x in range(CELL_W):
            n = sample_rgb(detail, x / CELL_W * 1.5, y / CELL_H * 1.9 + case.rotation)
            shift_x = int(row_wave + (n[1] - 0.5) * 6.0)
            shift_y = int(math.sin(x * 0.038 + y * 0.017) * 1.2)
            sx = min(CELL_W - 1, max(0, x + shift_x))
            sy = min(CELL_H - 1, max(0, y + shift_y))
            r, g, b = src[sx, sy]
            depth = y / CELL_H
            blue_fog = smoothstep(0.05, 0.95, depth) * 0.54
            caustic = smoothstep(0.76, 0.94, math.sin(x * 0.053 + y * 0.021 + case.rotation * 19.0) * 0.5 + 0.5) * 0.08
            out = (
                int(lerp(r, 12, blue_fog) + caustic * 22),
                int(lerp(g, 83, blue_fog) + caustic * 30),
                int(lerp(b, 108, blue_fog) + caustic * 42),
            )
            dst[x, y] = tuple(max(0, min(255, channel)) for channel in out)
    return image


def draw_caption(image: Image.Image, case: ProofCase) -> None:
    draw = ImageDraw.Draw(image, "RGBA")
    draw.rectangle((0, CELL_H - CAPTION_H, CELL_W, CELL_H), fill=(1, 6, 12, 184))
    draw.text((18, CELL_H - CAPTION_H + 10), case.caption, fill=(224, 233, 232, 255))
    draw.text(
        (18, CELL_H - CAPTION_H + 27),
        f"phase={case.phase_degrees:.0f} fog={case.fog:.2f} cloud={case.cloud:.2f} storm={case.storm_emission:.2f} q={case.quality_weight:.2f}",
        fill=(150, 177, 184, 255),
    )


def render_case(band: Image.Image, detail: Image.Image, storm: Image.Image, case: ProofCase) -> Image.Image:
    image = Image.new("RGB", (CELL_W, CELL_H), (0, 0, 0))
    add_gradient_background(image, case)
    draw_background_clouds(image, detail, case)
    render_planet(image, band, detail, storm, case)
    draw_horizon(image, detail, case)
    apply_underwater_distortion(image, detail, case)
    draw_caption(image, case)
    return image


def build_contact_sheet(cells: Iterable[Image.Image]) -> Image.Image:
    cells = tuple(cells)
    rows = int(math.ceil(len(cells) / GRID_COLUMNS))
    sheet = Image.new("RGB", (CELL_W * GRID_COLUMNS, CELL_H * rows), (0, 0, 0))
    for index, cell in enumerate(cells):
        x = (index % GRID_COLUMNS) * CELL_W
        y = (index // GRID_COLUMNS) * CELL_H
        sheet.paste(cell, (x, y))
    return sheet


def write_manifest(root: Path, image_rel: str, manifest_rel: str) -> None:
    payload = {
        "status": "AEGIR_GAS_GIANT_PROOF_CONTACT_SHEET_BUILT",
        "image": image_rel,
        "sourceTextures": [
            {"role": "bands", "path": CANONICAL_BAND_TEXTURE, "guid": CANONICAL_BAND_GUID},
            {"role": "detail", "path": CANONICAL_DETAIL_TEXTURE, "guid": CANONICAL_DETAIL_GUID},
            {"role": "storms", "path": CANONICAL_STORM_TEXTURE, "guid": CANONICAL_STORM_GUID},
        ],
        "views": [
            {
                "id": case.view_id,
                "mode": case.mode,
                "phaseDegrees": case.phase_degrees,
                "fog": case.fog,
                "cloud": case.cloud,
                "underwater": case.underwater,
                "horizonOcclusion": case.horizon,
                "stormEmissionMultiplier": case.storm_emission,
                "qualityWeight": case.quality_weight,
            }
            for case in PROOF_CASES
        ],
        "contract": {
            "unityRuntimeProof": False,
            "offlineProof": True,
            "covers": [
                "surface view",
                "underwater-up view",
                "horizon view",
                "different phase angles",
                "cloud and fog occlusion",
                "canonical band texture sampling",
                "weather-driven storm emission",
                "quality-tier fallback",
            ],
        },
    }
    manifest_path = project_path(root, manifest_rel)
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def build(root: Path = ROOT) -> tuple[Path, Path]:
    band = Image.open(project_path(root, CANONICAL_BAND_TEXTURE)).convert("RGB").resize(SAMPLER_BAND_SIZE, Image.Resampling.LANCZOS)
    detail = Image.open(project_path(root, CANONICAL_DETAIL_TEXTURE)).convert("RGB").resize(SAMPLER_DETAIL_SIZE, Image.Resampling.LANCZOS)
    storm = Image.open(project_path(root, CANONICAL_STORM_TEXTURE)).convert("RGB").resize(SAMPLER_STORM_SIZE, Image.Resampling.LANCZOS)
    cells = [render_case(band, detail, storm, case) for case in PROOF_CASES]
    sheet = build_contact_sheet(cells)
    image_path = project_path(root, OUTPUT_IMAGE)
    image_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(image_path, optimize=True)
    write_manifest(root, OUTPUT_IMAGE, OUTPUT_MANIFEST)
    return image_path, project_path(root, OUTPUT_MANIFEST)


def main() -> int:
    image_path, manifest_path = build(ROOT)
    print(f"AEGIR_GAS_GIANT_PROOF_CONTACT_SHEET_BUILT image={image_path} manifest={manifest_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
