from __future__ import annotations

import json
import math
import os
import random
import re
import shutil
import subprocess
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont


WIDTH = 1920
HEIGHT = 1080
FPS = 30
DURATION_SEC = 5.0
FRAME_COUNT = int(FPS * DURATION_SEC)

OUT_DIR = Path(__file__).resolve().parent
FFMPEG = shutil.which("ffmpeg")

BG = (4, 8, 11)
DEEP = (6, 13, 18)
GRID = (43, 85, 90)
TEAL = (103, 138, 135)
CREAM = (240, 242, 230)
CYAN = (0, 220, 220)
RED = (255, 24, 74)
AMBER = (174, 106, 36)

TITLE = "TENI GAMES"
SUBTITLE = "\u30c6\u30cb\u30b2\u30fc\u30e0\u30b9"
MAIN_KANJI = "\u5929\u8863"
BOTTOM_KANJI = "\u5929\u610f"
FOOTER = "SUBMERGE | PRESSURE | BLACK WATER"
SCRAMBLE = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789@#$%&*+<>?"


def smoothstep(edge0: float, edge1: float, x: float) -> float:
    if edge0 == edge1:
        return 1.0 if x >= edge1 else 0.0
    v = max(0.0, min(1.0, (x - edge0) / (edge1 - edge0)))
    return v * v * (3.0 - 2.0 * v)


def ease_out_cubic(x: float) -> float:
    x = max(0.0, min(1.0, x))
    return 1.0 - pow(1.0 - x, 3.0)


def font(path: str, size: int) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(path, size=size)


def first_existing(paths: list[str]) -> str:
    for item in paths:
        if Path(item).exists():
            return item
    raise FileNotFoundError(paths[0])


FONT_TITLE_PATH = first_existing(
    [
        "C:/Windows/Fonts/Poppins-Bold.ttf",
        "C:/Windows/Fonts/arialbd.ttf",
    ]
)
FONT_JP_PATH = first_existing(
    [
        "C:/Windows/Fonts/YuGothB.ttc",
        "C:/Windows/Fonts/msgothic.ttc",
        "C:/Windows/Fonts/msyhbd.ttc",
    ]
)
FONT_MONO_PATH = first_existing(
    [
        "C:/Windows/Fonts/CascadiaMono.ttf",
        "C:/Windows/Fonts/consola.ttf",
    ]
)

FONT_TITLE = font(FONT_TITLE_PATH, 76)
FONT_TITLE_SMALL = font(FONT_TITLE_PATH, 34)
FONT_SUB = font(FONT_JP_PATH, 38)
FONT_KANJI = font(FONT_JP_PATH, 350)
FONT_BOTTOM = font(FONT_JP_PATH, 66)
FONT_FOOTER = font(FONT_MONO_PATH, 27)
FONT_LABEL = font(FONT_MONO_PATH, 24)


def centered_text(draw: ImageDraw.ImageDraw, text: str, used_font: ImageFont.FreeTypeFont,
                  center: tuple[float, float], fill: tuple[int, int, int, int],
                  stroke_width: int = 0,
                  stroke_fill: tuple[int, int, int, int] | None = None) -> None:
    bbox = draw.textbbox((0, 0), text, font=used_font, stroke_width=stroke_width)
    tw = bbox[2] - bbox[0]
    th = bbox[3] - bbox[1]
    x = center[0] - tw * 0.5 - bbox[0]
    y = center[1] - th * 0.5 - bbox[1]
    draw.text((x, y), text, font=used_font, fill=fill,
              stroke_width=stroke_width, stroke_fill=stroke_fill)


def spaced_text(draw: ImageDraw.ImageDraw, text: str, used_font: ImageFont.FreeTypeFont,
                center_x: float, top_y: float, fill: tuple[int, int, int, int],
                spacing: int, offsets: list[tuple[float, float]] | None = None) -> None:
    widths: list[int] = []
    heights: list[int] = []
    for ch in text:
        bbox = draw.textbbox((0, 0), ch, font=used_font)
        widths.append(bbox[2] - bbox[0])
        heights.append(bbox[3] - bbox[1])
    total_w = sum(widths) + spacing * max(0, len(text) - 1)
    x = center_x - total_w * 0.5
    for idx, ch in enumerate(text):
        dx = 0.0
        dy = 0.0
        if offsets is not None and idx < len(offsets):
            dx, dy = offsets[idx]
        draw.text((x + dx, top_y + dy), ch, font=used_font, fill=fill)
        x += widths[idx] + spacing


def make_particles() -> list[dict[str, float]]:
    rng = random.Random(8157)
    particles: list[dict[str, float]] = []
    for _ in range(190):
        particles.append(
            {
                "x": rng.random() * WIDTH,
                "y": rng.random() * HEIGHT,
                "r": rng.uniform(0.8, 2.4),
                "speed": rng.uniform(12.0, 42.0),
                "phase": rng.random() * math.tau,
                "alpha": rng.uniform(0.12, 0.55),
            }
        )
    return particles


def make_sticks() -> list[dict[str, float]]:
    rng = random.Random(4491)
    sticks: list[dict[str, float]] = []
    for _ in range(96):
        angle = rng.random() * math.tau
        sticks.append(
            {
                "angle": angle,
                "dist": rng.uniform(520.0, 980.0),
                "rot": rng.random() * math.tau,
                "length": rng.uniform(34.0, 160.0),
                "width": rng.choice([2.0, 2.0, 3.0, 5.0]),
                "delay": rng.uniform(0.12, 0.9),
                "duration": rng.uniform(0.85, 1.22),
            }
        )
    return sticks


PARTICLES = make_particles()
STICKS = make_sticks()


def make_vignette() -> np.ndarray:
    y, x = np.ogrid[0:HEIGHT, 0:WIDTH]
    nx = (x - WIDTH * 0.5) / (WIDTH * 0.52)
    ny = (y - HEIGHT * 0.5) / (HEIGHT * 0.56)
    dist = np.sqrt(nx * nx + ny * ny)
    factor = 1.0 - np.clip((dist - 0.42) / 0.58, 0.0, 1.0) * 0.64
    return factor.astype(np.float32)[..., None]


VIGNETTE = make_vignette()


def draw_background(img: Image.Image, t: float, frame: int) -> None:
    draw = ImageDraw.Draw(img, "RGBA")
    grid_alpha = int(46 * smoothstep(0.0, 1.25, t))
    spacing = 58
    offset = int((t * 10.0) % spacing)
    for x in range(-spacing + offset, WIDTH + spacing, spacing):
        draw.line([(x, 0), (x, HEIGHT)], fill=(*GRID, grid_alpha), width=1)
    for y in range(-spacing + offset, HEIGHT + spacing, spacing):
        draw.line([(0, y), (WIDTH, y)], fill=(*GRID, grid_alpha), width=1)

    wave_alpha = int(84 * smoothstep(0.55, 2.0, t))
    for layer, amp, freq, speed, base_y, color_mul in [
        (0, 28.0, 2.2, 0.42, HEIGHT * 0.49, 1.0),
        (1, 18.0, 3.1, -0.28, HEIGHT * 0.53, 0.72),
        (2, 12.0, 4.7, 0.18, HEIGHT * 0.57, 0.45),
    ]:
        pts = []
        for i in range(0, WIDTH + 12, 12):
            x = i
            phase = (i / WIDTH) * math.tau * freq + t * math.tau * speed + layer
            y = base_y + math.sin(phase) * amp + math.sin(phase * 0.41) * amp * 0.42
            pts.append((x, y))
        alpha = int(wave_alpha * color_mul)
        draw.line(pts, fill=(*TEAL, alpha), width=2)

    part_alpha_scale = smoothstep(0.35, 1.6, t)
    for p in PARTICLES:
        x = (p["x"] + math.sin(t * 0.7 + p["phase"]) * 24.0) % WIDTH
        y = (p["y"] + p["speed"] * t) % HEIGHT
        pulse = 0.55 + 0.45 * math.sin(t * 2.0 + p["phase"])
        alpha = int(72 * p["alpha"] * pulse * part_alpha_scale)
        r = p["r"]
        draw.ellipse((x - r, y - r, x + r, y + r), fill=(115, 160, 156, alpha))


def draw_stick(draw: ImageDraw.ImageDraw, cx: float, cy: float, length: float,
               width: int, rot: float, fill: tuple[int, int, int, int]) -> None:
    dx = math.cos(rot) * length * 0.5
    dy = math.sin(rot) * length * 0.5
    draw.line([(cx - dx, cy - dy), (cx + dx, cy + dy)], fill=fill, width=width)


def draw_sticks(img: Image.Image, t: float) -> None:
    layer = Image.new("RGBA", (WIDTH, HEIGHT), (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer, "RGBA")
    cx = WIDTH * 0.5
    cy = HEIGHT * 0.53

    for s in STICKS:
        p = (t - s["delay"]) / s["duration"]
        if 0.0 <= p <= 1.0:
            e = ease_out_cubic(p)
            sx = math.cos(s["angle"]) * s["dist"]
            sy = math.sin(s["angle"]) * s["dist"] * 0.62
            x = cx + sx * (1.0 - e)
            y = cy + sy * (1.0 - e)
            alpha = int(230 * math.sin(math.pi * p))
            length = s["length"] * (1.0 - 0.75 * e)
            draw_stick(draw, x, y, length, int(s["width"]), s["rot"], (*CREAM, alpha))

        q = (t - 4.18 - (s["delay"] * 0.2)) / 0.72
        if 0.0 <= q <= 1.0:
            e = q * q
            sx = math.cos(s["angle"]) * s["dist"] * 0.68
            sy = math.sin(s["angle"]) * s["dist"] * 0.42
            x = cx + sx * e
            y = cy + sy * e
            alpha = int(165 * (1.0 - q))
            length = s["length"] * (0.35 + q)
            draw_stick(draw, x, y, length, int(s["width"]), s["rot"], (*TEAL, alpha))

    glow = layer.filter(ImageFilter.GaussianBlur(6))
    img.alpha_composite(glow)
    img.alpha_composite(layer)


def draw_title(img: Image.Image, t: float, frame: int) -> None:
    draw = ImageDraw.Draw(img, "RGBA")
    widths = []
    for ch in TITLE:
        bbox = draw.textbbox((0, 0), ch, font=FONT_TITLE)
        widths.append(bbox[2] - bbox[0])
    spacing = 12
    total_w = sum(widths) + spacing * (len(TITLE) - 1)
    x = WIDTH * 0.5 - total_w * 0.5
    y = 348
    for i, ch in enumerate(TITLE):
        start = 0.92 + i * 0.055
        p = smoothstep(start, start + 0.42, t)
        if p <= 0.0:
            x += widths[i] + spacing
            continue
        if ch == " ":
            x += widths[i] + spacing
            continue
        if p < 1.0:
            glyph = SCRAMBLE[(frame + i * 13) % len(SCRAMBLE)]
            color = CYAN if ((frame + i) % 3 == 0) else TEAL
        else:
            glyph = ch
            color = CREAM
        alpha = int(255 * p)
        draw.text((x, y + (1.0 - p) * 16.0), glyph, font=FONT_TITLE, fill=(*color, alpha))
        x += widths[i] + spacing


def draw_glitch_kanji(img: Image.Image, t: float, frame: int) -> None:
    if not (2.42 <= t <= 2.82):
        return
    rng = random.Random(1000 + frame)
    for color, xoff in [(RED, -12), (CYAN, 12)]:
        text_layer = Image.new("RGBA", (WIDTH, HEIGHT), (0, 0, 0, 0))
        d = ImageDraw.Draw(text_layer, "RGBA")
        centered_text(d, MAIN_KANJI, FONT_KANJI, (WIDTH * 0.5 + xoff, HEIGHT * 0.535),
                      (*color, 150))
        mask = Image.new("L", (WIDTH, HEIGHT), 0)
        md = ImageDraw.Draw(mask)
        for _ in range(12):
            y = rng.randint(360, 720)
            h = rng.randint(10, 34)
            md.rectangle((0, y, WIDTH, y + h), fill=rng.randint(90, 220))
        text_layer.putalpha(mask)
        img.alpha_composite(text_layer)


def draw_logo(img: Image.Image, t: float, frame: int) -> None:
    draw = ImageDraw.Draw(img, "RGBA")
    top_p = smoothstep(0.82, 1.55, t)
    line_w = int(1160 * top_p)
    draw.line([(WIDTH * 0.5 - line_w * 0.5, 312), (WIDTH * 0.5 + line_w * 0.5, 312)],
              fill=(*GRID, int(210 * top_p)), width=2)

    draw_title(img, t, frame)

    sub_alpha = int(205 * smoothstep(1.72, 2.46, t))
    centered_text(draw, SUBTITLE, FONT_SUB, (WIDTH * 0.5, 443), (*TEAL, sub_alpha))

    reveal = smoothstep(1.08, 2.54, t)
    fill_p = smoothstep(2.24, 2.74, t)
    text_layer = Image.new("RGBA", (WIDTH, HEIGHT), (0, 0, 0, 0))
    td = ImageDraw.Draw(text_layer, "RGBA")
    centered_text(td, MAIN_KANJI, FONT_KANJI, (WIDTH * 0.5, HEIGHT * 0.535),
                  (*CREAM, int(235 * fill_p)),
                  stroke_width=4,
                  stroke_fill=(*CREAM, int(220 * max(0.2, reveal))))
    if reveal < 1.0:
        mask = Image.new("L", (WIDTH, HEIGHT), 0)
        md = ImageDraw.Draw(mask)
        limit = int(WIDTH * (0.5 - 0.25 + reveal * 0.5))
        for y in range(315, 725, 14):
            jitter = int(math.sin(y * 0.075 + frame * 0.2) * 40)
            md.rectangle((0, y, limit + jitter, y + 10), fill=255)
        text_layer.putalpha(mask)
    glow = text_layer.filter(ImageFilter.GaussianBlur(11))
    img.alpha_composite(glow)
    img.alpha_composite(text_layer)

    draw_glitch_kanji(img, t, frame)

    flash = 1.0 - abs(t - 2.64) / 0.18
    if flash > 0.0:
        flash = flash * flash
        fl = Image.new("RGBA", (WIDTH, HEIGHT), (0, 0, 0, 0))
        fd = ImageDraw.Draw(fl, "RGBA")
        r = 28 + flash * 600
        alpha = int(180 * flash)
        fd.ellipse((WIDTH * 0.5 - r, HEIGHT * 0.535 - r, WIDTH * 0.5 + r, HEIGHT * 0.535 + r),
                   fill=(255, 255, 245, alpha))
        fl = fl.filter(ImageFilter.GaussianBlur(28))
        img.alpha_composite(fl)

    box_p = smoothstep(2.76, 3.32, t)
    bw = int(760 * box_p)
    bh = int(82 * box_p)
    by = 746
    bx0 = int(WIDTH * 0.5 - bw * 0.5)
    bx1 = int(WIDTH * 0.5 + bw * 0.5)
    draw.line([(bx0, by), (bx1, by)], fill=(*TEAL, int(190 * box_p)), width=2)
    draw.line([(bx0, by + 82), (bx1, by + 82)], fill=(*TEAL, int(190 * box_p)), width=2)
    draw.line([(bx0, by + 41 - bh * 0.5), (bx0, by + 41 + bh * 0.5)],
              fill=(*TEAL, int(190 * box_p)), width=2)
    draw.line([(bx1, by + 41 - bh * 0.5), (bx1, by + 41 + bh * 0.5)],
              fill=(*TEAL, int(190 * box_p)), width=2)
    bottom_alpha = int(245 * smoothstep(3.0, 3.52, t))
    centered_text(draw, BOTTOM_KANJI, FONT_BOTTOM, (WIDTH * 0.5, by + 43),
                  (*CREAM, bottom_alpha))

    bottom_p = smoothstep(3.12, 3.72, t)
    lw = int(1160 * bottom_p)
    draw.line([(WIDTH * 0.5 - lw * 0.5, 862), (WIDTH * 0.5 + lw * 0.5, 862)],
              fill=(*GRID, int(190 * bottom_p)), width=2)

    footer_p = smoothstep(3.35, 4.05, t)
    spaced_text(draw, FOOTER, FONT_FOOTER, WIDTH * 0.5, 900,
                (*TEAL, int(220 * footer_p)), 7)


def apply_mirror_center(img: Image.Image, t: float, frame: int) -> Image.Image:
    assemble = smoothstep(0.22, 1.84, t)
    disassemble = smoothstep(3.55, 4.76, t)
    strength = assemble * (1.0 - disassemble)
    motion = max(0.0, 1.0 - abs(t - 1.1) / 0.85) + max(0.0, 1.0 - abs(t - 4.18) / 0.6)
    if strength <= 0.01 and motion <= 0.01:
        return img

    arr = np.array(img.convert("RGB"))
    out = arr.copy()
    cx = WIDTH // 2
    band = int((140 + 420 * strength + 180 * motion) * max(strength, motion * 0.5))
    band = max(0, min(cx - 2, band))
    if band > 4:
        region = out[:, cx - band:cx + band].copy()
        mirrored = region[:, ::-1].copy()
        alpha = min(0.92, 0.28 + 0.62 * strength + 0.2 * motion)
        out[:, cx - band:cx + band] = (region * (1.0 - alpha) + mirrored * alpha).astype(np.uint8)

    if motion > 0.02:
        rng = random.Random(2220 + frame)
        direction = -1.0 if t < 2.5 else 1.0
        for _ in range(24):
            y0 = rng.randint(180, HEIGHT - 180)
            h = rng.randint(8, 28)
            shift = int(direction * rng.choice([-1, 1]) * rng.randint(10, 64) * motion)
            out[y0:y0 + h, :] = np.roll(out[y0:y0 + h, :], shift, axis=1)

    result = Image.fromarray(out, "RGB").convert("RGBA")
    seam = Image.new("RGBA", (WIDTH, HEIGHT), (0, 0, 0, 0))
    sd = ImageDraw.Draw(seam, "RGBA")
    seam_alpha = int(210 * max(strength, motion * 0.75))
    sd.line([(cx, 110), (cx, HEIGHT - 110)], fill=(*CYAN, seam_alpha), width=3)
    sd.line([(cx - 8, 190), (cx - 8, HEIGHT - 190)], fill=(*CREAM, int(seam_alpha * 0.26)), width=1)
    sd.line([(cx + 8, 190), (cx + 8, HEIGHT - 190)], fill=(*RED, int(seam_alpha * 0.32)), width=1)
    seam = seam.filter(ImageFilter.GaussianBlur(0.4))
    result.alpha_composite(seam)
    return result


def draw_mirror_seam_overlay(img: Image.Image, t: float, frame: int) -> None:
    assemble = smoothstep(0.22, 1.84, t)
    disassemble = smoothstep(3.55, 4.76, t)
    strength = assemble * (1.0 - disassemble)
    motion = max(0.0, 1.0 - abs(t - 1.1) / 0.85) + max(0.0, 1.0 - abs(t - 4.18) / 0.6)
    seam_power = max(strength, motion * 0.8)
    if seam_power <= 0.01:
        return

    cx = WIDTH // 2
    layer = Image.new("RGBA", (WIDTH, HEIGHT), (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer, "RGBA")
    alpha = int(190 * seam_power)
    draw.line([(cx, 118), (cx, HEIGHT - 118)], fill=(*CYAN, alpha), width=2)
    draw.line([(cx - 7, 190), (cx - 7, HEIGHT - 190)], fill=(*CREAM, int(alpha * 0.22)), width=1)
    draw.line([(cx + 7, 190), (cx + 7, HEIGHT - 190)], fill=(*RED, int(alpha * 0.28)), width=1)

    rng = random.Random(5100 + frame)
    for _ in range(18):
        if rng.random() > seam_power:
            continue
        y = rng.randint(260, 830)
        half = rng.randint(22, 180)
        off = rng.randint(-18, 18)
        local_alpha = rng.randint(30, 110)
        draw.line([(cx - half + off, y), (cx + half + off, y)],
                  fill=(*TEAL, local_alpha), width=rng.choice([1, 1, 2]))

    layer = layer.filter(ImageFilter.GaussianBlur(0.35))
    img.alpha_composite(layer)


def post_process(img: Image.Image, t: float, frame: int) -> Image.Image:
    arr = np.array(img.convert("RGB"), dtype=np.float32)
    rng = np.random.default_rng(7000 + frame)
    grain = rng.normal(0.0, 3.2, (HEIGHT, WIDTH, 1)).astype(np.float32)
    dark_weight = 1.0 - np.clip(arr.mean(axis=2, keepdims=True) / 255.0, 0.0, 1.0)
    arr += grain * (0.45 + dark_weight * 0.85)
    arr[::4, :, :] *= 0.78
    arr[1::4, :, :] *= 0.93
    arr = arr * VIGNETTE + np.array(BG, dtype=np.float32) * (1.0 - VIGNETTE)

    scan_y = int(((t * 0.22) % 1.0) * (HEIGHT + 220)) - 110
    if 0 <= scan_y < HEIGHT:
        y0 = max(0, scan_y - 2)
        y1 = min(HEIGHT, scan_y + 4)
        arr[y0:y1, :, 1] += 42
        arr[y0:y1, :, 2] += 30

    fade_in = smoothstep(0.0, 0.18, t)
    fade_out = 1.0 - smoothstep(4.72, 5.0, t)
    fade = fade_in * fade_out
    arr = arr * fade + np.array(DEEP, dtype=np.float32) * (1.0 - fade)
    arr = np.clip(arr, 0, 255).astype(np.uint8)
    return Image.fromarray(arr, "RGB")


def render_frame(t: float, frame: int, variant: str) -> Image.Image:
    img = Image.new("RGBA", (WIDTH, HEIGHT), (*BG, 255))
    draw_background(img, t, frame)
    draw_sticks(img, t)
    if variant == "mirror":
        img = apply_mirror_center(img, t, frame)
    draw_logo(img, t, frame)
    if variant == "mirror":
        draw_mirror_seam_overlay(img, t, frame)
    return post_process(img, t, frame)


def encode_mp4(variant: str) -> Path:
    if not re.match(r"^[a-zA-Z0-9_-]+$", variant):
        raise ValueError(f"Invalid variant: {variant}")
    if FFMPEG is None:
        raise RuntimeError("ffmpeg not found")
    out = OUT_DIR / f"tenigames_splash_{variant}_1080p.mp4"
    if os.environ.get("H8_FORCE_RENDER") != "1" and out.exists() and out.stat().st_size > 1024:
        return out
    cmd = [
        FFMPEG,
        "-y",
        "-f",
        "rawvideo",
        "-vcodec",
        "rawvideo",
        "-pix_fmt",
        "rgb24",
        "-s",
        f"{WIDTH}x{HEIGHT}",
        "-r",
        str(FPS),
        "-i",
        "-",
        "-an",
        "-c:v",
        "libx264",
        "-preset",
        "medium",
        "-crf",
        "18",
        "-pix_fmt",
        "yuv420p",
        "-movflags",
        "+faststart",
        str(out),
    ]
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    assert proc.stdin is not None
    for frame in range(FRAME_COUNT):
        t = frame / FPS
        proc.stdin.write(render_frame(t, frame, variant).tobytes())
    proc.stdin.close()
    code = proc.wait()
    if code != 0:
        raise RuntimeError(f"ffmpeg mp4 encode failed for {variant}: {code}")
    return out


def encode_gif(mp4: Path, variant: str) -> Path:
    if not re.match(r"^[a-zA-Z0-9_-]+$", variant):
        raise ValueError(f"Invalid variant: {variant}")
    gif = OUT_DIR / f"tenigames_splash_{variant}_960w.gif"
    if os.environ.get("H8_FORCE_RENDER") != "1" and gif.exists() and gif.stat().st_size > 1024:
        return gif
    del mp4
    frames: list[Image.Image] = []
    gif_fps = 10
    gif_count = int(DURATION_SEC * gif_fps)
    for index in range(gif_count):
        t = index / gif_fps
        source_frame = int(t * FPS)
        frame = render_frame(t, source_frame, variant)
        frame = frame.resize((960, 540), Image.Resampling.LANCZOS)
        frame = frame.convert("P", palette=Image.Palette.ADAPTIVE, colors=128, dither=Image.Dither.FLOYDSTEINBERG)
        frames.append(frame)
    frames[0].save(
        gif,
        save_all=True,
        append_images=frames[1:],
        duration=int(1000 / gif_fps),
        loop=0,
        optimize=True,
        disposal=2,
    )
    return gif


def make_contact_sheet() -> Path:
    times = [0.35, 1.25, 2.55, 3.45, 4.55]
    thumb_w = 384
    thumb_h = 216
    label_h = 44
    sheet = Image.new("RGB", (thumb_w * len(times), (thumb_h + label_h) * 2), BG)
    draw = ImageDraw.Draw(sheet, "RGBA")
    for row, variant in enumerate(["normal", "mirror"]):
        for col, t in enumerate(times):
            frame = int(t * FPS)
            im = render_frame(t, frame, variant).resize((thumb_w, thumb_h), Image.Resampling.LANCZOS)
            x = col * thumb_w
            y = row * (thumb_h + label_h)
            sheet.paste(im, (x, y + label_h))
            label = f"{variant.upper()}  t={t:.2f}s"
            draw.text((x + 12, y + 10), label, font=FONT_LABEL, fill=(*TEAL, 235))
    out = OUT_DIR / "tenigames_splash_comparison_contact_sheet.png"
    sheet.save(out, optimize=True)
    return out


def bytes_mb(path: Path) -> float:
    return path.stat().st_size / (1024.0 * 1024.0)


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    outputs: dict[str, dict[str, object]] = {}
    for variant in ["normal", "mirror"]:
        mp4 = encode_mp4(variant)
        gif = encode_gif(mp4, variant)
        outputs[variant] = {
            "mp4": str(mp4),
            "mp4_mb": round(bytes_mb(mp4), 2),
            "gif": str(gif),
            "gif_mb": round(bytes_mb(gif), 2),
        }
    sheet = make_contact_sheet()
    report = {
        "width": WIDTH,
        "height": HEIGHT,
        "fps": FPS,
        "duration_sec": DURATION_SEC,
        "font_title": FONT_TITLE_PATH,
        "font_jp": FONT_JP_PATH,
        "font_mono": FONT_MONO_PATH,
        "ffmpeg": FFMPEG,
        "outputs": outputs,
        "comparison": str(sheet),
        "comparison_mb": round(bytes_mb(sheet), 2),
        "notes": [
            "offline deterministic raster render",
            "no Unity runtime code generated",
            "GIF exports are intentionally 960w/10fps for practical preview size",
        ],
    }
    (OUT_DIR / "render_report.json").write_text(json.dumps(report, indent=2, ensure_ascii=True), encoding="utf-8")
    print(json.dumps(report, indent=2, ensure_ascii=True))


if __name__ == "__main__":
    main()
