#!/usr/bin/env python3
"""Numerical telemetry for the clean-room voxel cave artifacts.

This script intentionally reports numbers only. Beauty-render pixel statistics are
for stale/blank-frame detection, not visual acceptance; HECTON-8 visual acceptance
still requires human/multimodal inspection of the rendered image.
"""
from __future__ import annotations

import math
import struct
import sys
import zlib
from pathlib import Path
from typing import Iterator, Optional, Tuple

RGB = Tuple[int, int, int]


def _project_root() -> Path:
    return Path(__file__).resolve().parent


def _default_report_dir() -> Path:
    return _project_root() / "Docs" / "Reports" / "CleanRoom"


def _load_pixels(path: Path) -> Tuple[int, int, Iterator[RGB]]:
    try:
        from PIL import Image  # type: ignore
    except Exception:
        return _load_png_pixels_without_pillow(path)

    image = Image.open(path).convert("RGBA")
    width, height = image.size
    data = image.tobytes()

    def iterator() -> Iterator[RGB]:
        for i in range(0, len(data), 4):
            yield data[i], data[i + 1], data[i + 2]

    return width, height, iterator()


def _load_png_pixels_without_pillow(path: Path) -> Tuple[int, int, Iterator[RGB]]:
    data = path.read_bytes()
    signature = b"\x89PNG\r\n\x1a\n"
    if not data.startswith(signature):
        raise ValueError(f"{path} is not a PNG file")

    offset = len(signature)
    width = 0
    height = 0
    bit_depth = 0
    color_type = 0
    idat = bytearray()

    while offset + 8 <= len(data):
        length = struct.unpack(">I", data[offset:offset + 4])[0]
        kind = data[offset + 4:offset + 8]
        payload_start = offset + 8
        payload_end = payload_start + length
        payload = data[payload_start:payload_end]
        offset = payload_end + 4

        if kind == b"IHDR":
            width, height, bit_depth, color_type = struct.unpack(">IIBB", payload[:10])[:4]
        elif kind == b"IDAT":
            idat.extend(payload)
        elif kind == b"IEND":
            break

    if width <= 0 or height <= 0 or bit_depth != 8:
        raise ValueError(f"unsupported PNG header in {path}: {width}x{height} bit_depth={bit_depth}")

    channels_by_type = {0: 1, 2: 3, 4: 2, 6: 4}
    if color_type not in channels_by_type:
        raise ValueError(f"unsupported PNG color type {color_type} in {path}")

    channels = channels_by_type[color_type]
    row_bytes = width * channels
    raw = zlib.decompress(bytes(idat))
    expected = (row_bytes + 1) * height
    if len(raw) < expected:
        raise ValueError(f"truncated PNG data in {path}: {len(raw)} < {expected}")

    def paeth(a: int, b: int, c: int) -> int:
        p = a + b - c
        pa = abs(p - a)
        pb = abs(p - b)
        pc = abs(p - c)
        if pa <= pb and pa <= pc:
            return a
        if pb <= pc:
            return b
        return c

    def iterator() -> Iterator[RGB]:
        previous = bytearray(row_bytes)
        pos = 0
        for _ in range(height):
            filter_type = raw[pos]
            pos += 1
            row = bytearray(raw[pos:pos + row_bytes])
            pos += row_bytes
            for i in range(row_bytes):
                left = row[i - channels] if i >= channels else 0
                up = previous[i]
                up_left = previous[i - channels] if i >= channels else 0
                if filter_type == 1:
                    row[i] = (row[i] + left) & 0xFF
                elif filter_type == 2:
                    row[i] = (row[i] + up) & 0xFF
                elif filter_type == 3:
                    row[i] = (row[i] + ((left + up) >> 1)) & 0xFF
                elif filter_type == 4:
                    row[i] = (row[i] + paeth(left, up, up_left)) & 0xFF
                elif filter_type != 0:
                    raise ValueError(f"unsupported PNG filter {filter_type} in {path}")

            for x in range(width):
                base = x * channels
                if color_type == 0:
                    v = row[base]
                    yield v, v, v
                elif color_type == 4:
                    v = row[base]
                    yield v, v, v
                else:
                    yield row[base], row[base + 1], row[base + 2]
            previous = row

    return width, height, iterator()


def _analyze_image(path: Path, xray: bool) -> dict[str, float | int | str]:
    if not path.exists():
        raise FileNotFoundError(path)

    width, height, pixels = _load_pixels(path)
    count = width * height
    sums = [0, 0, 0]
    sums_sq = [0, 0, 0]
    min_rgb = [255, 255, 255]
    max_rgb = [0, 0, 0]
    black = 0
    white = 0
    red = 0

    for r, g, b in pixels:
        sums[0] += r
        sums[1] += g
        sums[2] += b
        sums_sq[0] += r * r
        sums_sq[1] += g * g
        sums_sq[2] += b * b
        min_rgb[0] = min(min_rgb[0], r)
        min_rgb[1] = min(min_rgb[1], g)
        min_rgb[2] = min(min_rgb[2], b)
        max_rgb[0] = max(max_rgb[0], r)
        max_rgb[1] = max(max_rgb[1], g)
        max_rgb[2] = max(max_rgb[2], b)
        if r <= 8 and g <= 8 and b <= 8:
            black += 1
        if r >= 247 and g >= 247 and b >= 247:
            white += 1
        if r >= 192 and g <= 64 and b <= 64:
            red += 1

    inv = 1.0 / max(count, 1)
    avg = [s * inv for s in sums]
    var = [max(0.0, sums_sq[i] * inv - avg[i] * avg[i]) for i in range(3)]
    luminance_avg = avg[0] * 0.2126 + avg[1] * 0.7152 + avg[2] * 0.0722
    luminance_var = var[0] * 0.2126 + var[1] * 0.7152 + var[2] * 0.0722
    solid_or_void = black + white
    xray_black_ratio = black / solid_or_void if xray and solid_or_void > 0 else math.nan

    return {
        "path": str(path),
        "width": width,
        "height": height,
        "avg_r": avg[0],
        "avg_g": avg[1],
        "avg_b": avg[2],
        "var_r": var[0],
        "var_g": var[1],
        "var_b": var[2],
        "luminance_avg": luminance_avg,
        "luminance_var": luminance_var,
        "min_r": min_rgb[0],
        "min_g": min_rgb[1],
        "min_b": min_rgb[2],
        "max_r": max_rgb[0],
        "max_g": max_rgb[1],
        "max_b": max_rgb[2],
        "black_pixels": black,
        "white_pixels": white,
        "red_pixels": red,
        "black_ratio_total": black * inv,
        "white_ratio_total": white * inv,
        "red_ratio_total": red * inv,
        "xray_black_vs_white_ratio": xray_black_ratio,
    }


def _read_cave_volume_ratio(path: Path) -> Optional[float]:
    if not path.exists():
        return None
    for line in path.read_text(encoding="utf-8-sig", errors="replace").splitlines():
        if line.startswith("CaveVolumeRatio="):
            try:
                return float(line.split("=", 1)[1])
            except ValueError:
                return None
    return None


def _format_block(title: str, stats: dict[str, float | int | str]) -> str:
    lines = [f"[{title}]", f"Path={stats['path']}", f"Size={stats['width']}x{stats['height']}"]
    lines.append(f"AverageRGB={stats['avg_r']:.6f},{stats['avg_g']:.6f},{stats['avg_b']:.6f}")
    lines.append(f"VarianceRGB={stats['var_r']:.6f},{stats['var_g']:.6f},{stats['var_b']:.6f}")
    lines.append(f"LuminanceAverage={stats['luminance_avg']:.6f}")
    lines.append(f"LuminanceVariance={stats['luminance_var']:.6f}")
    lines.append(f"MinRGB={stats['min_r']},{stats['min_g']},{stats['min_b']}")
    lines.append(f"MaxRGB={stats['max_r']},{stats['max_g']},{stats['max_b']}")
    lines.append(f"BlackPixels={stats['black_pixels']}")
    lines.append(f"WhitePixels={stats['white_pixels']}")
    lines.append(f"RedBoundaryPixels={stats['red_pixels']}")
    lines.append(f"BlackRatioTotal={stats['black_ratio_total']:.6f}")
    lines.append(f"WhiteRatioTotal={stats['white_ratio_total']:.6f}")
    lines.append(f"RedRatioTotal={stats['red_ratio_total']:.6f}")
    ratio = stats.get("xray_black_vs_white_ratio")
    if isinstance(ratio, float) and math.isfinite(ratio):
        lines.append(f"XRayBlackVsWhiteRatio={ratio:.6f}")
    return "\n".join(lines)


def main(argv: list[str]) -> int:
    report_dir = Path(argv[1]).resolve() if len(argv) > 1 else _default_report_dir()
    beauty = report_dir / "Cave_Beauty.png"
    xray = report_dir / "Cave_SDF_Slice_XRay.png"
    telemetry = report_dir / "Cave_Voxel_Telemetry.txt"
    output = report_dir / "Cave_Render_Analysis.txt"

    beauty_stats = _analyze_image(beauty, xray=False)
    xray_stats = _analyze_image(xray, xray=True)
    cave_volume_ratio = _read_cave_volume_ratio(telemetry)

    blocks = [
        "BeautyDiagnosticOnly=True",
        _format_block("Beauty", beauty_stats),
        _format_block("XRay", xray_stats),
    ]
    if cave_volume_ratio is not None:
        blocks.append(f"CaveVolumeRatio3D={cave_volume_ratio:.6f}")
        blocks.append(f"CaveVolumeTargetPass={str(0.15 <= cave_volume_ratio <= 0.25)}")

    xray_ratio = xray_stats["xray_black_vs_white_ratio"]
    if isinstance(xray_ratio, float) and math.isfinite(xray_ratio):
        blocks.append(f"XRayHardPass={str(0.001 <= xray_ratio <= 0.40)}")
        blocks.append(f"XRayTargetBand={str(0.15 <= xray_ratio <= 0.25)}")

    text = "\n".join(blocks) + "\n"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(text, encoding="utf-8")
    print(text, end="")

    if cave_volume_ratio is None:
        return 4
    if cave_volume_ratio < 0.15 or cave_volume_ratio > 0.25:
        return 2
    if isinstance(xray_ratio, float) and math.isfinite(xray_ratio) and (xray_ratio < 0.001 or xray_ratio > 0.40):
        return 3
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
