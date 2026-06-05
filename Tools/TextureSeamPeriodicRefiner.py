#!/usr/bin/env python3
"""Offline periodic texture refinement for AI/generated material candidates.

This tool does not touch Unity assets. It writes refined source candidates under
Docs/GeneratedAssets by default, so Unity import loops are not triggered.
"""

from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
from PIL import Image, ImageStat


def rel(path: Path, root: Path) -> str:
    try:
        return path.relative_to(root).as_posix()
    except ValueError:
        return path.as_posix()


def periodic_component(channel: np.ndarray) -> np.ndarray:
    """Return the periodic component from periodic-plus-smooth decomposition."""
    height, width = channel.shape
    boundary = np.zeros_like(channel, dtype=np.float32)

    lr = channel[:, 0] - channel[:, -1]
    boundary[:, 0] += lr
    boundary[:, -1] -= lr

    tb = channel[0, :] - channel[-1, :]
    boundary[0, :] += tb
    boundary[-1, :] -= tb

    x = np.arange(width, dtype=np.float32)
    y = np.arange(height, dtype=np.float32)
    denom = (
        2.0 * np.cos((2.0 * np.pi * x)[None, :] / width)
        + 2.0 * np.cos((2.0 * np.pi * y)[:, None] / height)
        - 4.0
    )
    denom[0, 0] = 1.0

    smooth_hat = np.fft.fft2(boundary) / denom
    smooth_hat[0, 0] = 0.0
    smooth = np.fft.ifft2(smooth_hat).real.astype(np.float32)
    return channel - smooth


def light_albedo_guard(rgb: np.ndarray, enabled: bool) -> np.ndarray:
    if not enabled:
        return rgb
    image = Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8), "RGB")
    lum_mean = float(ImageStat.Stat(image.convert("L")).mean[0])
    if lum_mean >= 95.0:
        return rgb
    lift = min(18.0, 95.0 - lum_mean)
    return np.clip(rgb + lift, 0, 255)


def preserve_channel_mean(source: np.ndarray, refined: np.ndarray) -> np.ndarray:
    source_mean = source.reshape(-1, source.shape[2]).mean(axis=0)
    refined_mean = refined.reshape(-1, refined.shape[2]).mean(axis=0)
    return refined + (source_mean - refined_mean)[None, None, :]


def pin_outer_edges(rgb: np.ndarray) -> np.ndarray:
    """Make the exact wrap edge pixels identical after periodic refinement."""
    pinned = rgb.copy()
    lr = (pinned[:, 0, :] + pinned[:, -1, :]) * 0.5
    pinned[:, 0, :] = lr
    pinned[:, -1, :] = lr

    tb = (pinned[0, :, :] + pinned[-1, :, :]) * 0.5
    pinned[0, :, :] = tb
    pinned[-1, :, :] = tb
    return pinned


def refine(input_path: Path, output_path: Path, albedo_lift: bool, edge_pin: bool, keep_mean: bool) -> None:
    with Image.open(input_path) as image:
        image.load()
        rgba = image.convert("RGBA")

    data = np.asarray(rgba).astype(np.float32)
    rgb = data[:, :, :3]
    alpha = data[:, :, 3:4]

    refined_channels = [rgb[:, :, channel] + periodic_component(rgb[:, :, channel]) for channel in range(3)]
    refined = np.stack(refined_channels, axis=2)
    if keep_mean:
        refined = preserve_channel_mean(rgb, refined)
    refined = light_albedo_guard(refined, albedo_lift)
    if edge_pin:
        refined = pin_outer_edges(refined)

    result = np.concatenate([np.clip(refined, 0, 255), alpha], axis=2).astype(np.uint8)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(result, "RGBA").save(output_path)


def main() -> int:
    parser = argparse.ArgumentParser(description="Make a generated texture candidate edge-periodic before intake QA.")
    parser.add_argument("--project-root", default=".", help="Project root.")
    parser.add_argument("--input", required=True, help="Input image path, relative to project root unless absolute.")
    parser.add_argument("--output", required=True, help="Output image path, relative to project root unless absolute.")
    parser.add_argument(
        "--albedo-lift",
        action="store_true",
        help="Apply a small non-directional luminance lift for too-dark surface albedo candidates.",
    )
    parser.add_argument(
        "--edge-pin",
        action="store_true",
        help="Average exact first/last rows and columns after refinement. Diagnostic only; this can hide edge-only seam metrics.",
    )
    parser.add_argument(
        "--no-keep-mean",
        action="store_true",
        help="Skip restoring the source image channel means after periodic refinement.",
    )
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    input_path = Path(args.input)
    output_path = Path(args.output)
    if not input_path.is_absolute():
        input_path = project_root / input_path
    if not output_path.is_absolute():
        output_path = project_root / output_path

    refine(input_path, output_path, args.albedo_lift, args.edge_pin, not args.no_keep_mean)
    print(f"TEXTURE_SEAM_PERIODIC_REFINER_DONE input={rel(input_path, project_root)} output={rel(output_path, project_root)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
