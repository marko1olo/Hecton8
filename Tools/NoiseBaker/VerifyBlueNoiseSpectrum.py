#!/usr/bin/env python3
"""Independent verifier for HECTON-8 baked blue noise assets."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any

import numpy as np
from PIL import Image


BLUE_SIZE = 256
FLOW_SIZE = 128
DC_POWER_MAX = 0.0001
LOW_MEAN_TO_MID_MEAN_MAX = 0.12
LOW_PEAK_TO_MID_MEAN_MAX = 0.5
MAX_SEAM_RATIO_MAX = 1.35


def repository_root() -> Path:
    return Path(__file__).resolve().parents[2]


def artifact_path(path: Path) -> str:
    resolved = path.resolve()
    root = repository_root().resolve()
    try:
        return resolved.relative_to(root).as_posix()
    except ValueError:
        return str(resolved)


def frac(value: np.ndarray) -> np.ndarray:
    return value - np.floor(value)


def quantize_unorm(value: np.ndarray) -> np.ndarray:
    return np.clip(np.rint(value * 255.0), 0.0, 255.0).astype(np.uint8)


def expected_ign(width: int, height: int) -> np.ndarray:
    y, x = np.mgrid[0:height, 0:width]
    inner = frac((x.astype(np.float64) * 0.06711056) + (y.astype(np.float64) * 0.00583715))
    return quantize_unorm(frac(52.9829189 * inner))


def read_rgba(path: Path) -> np.ndarray:
    with Image.open(path) as image:
        return np.array(image.convert("RGBA"), dtype=np.uint8)


def radial_spectrum_metrics(channel: np.ndarray) -> dict[str, float]:
    values = channel.astype(np.float32) / 255.0
    values -= float(np.mean(values))
    power = np.abs(np.fft.fftshift(np.fft.fft2(values))) ** 2
    height, width = values.shape
    center_y = height // 2
    center_x = width // 2
    y, x = np.mgrid[0:height, 0:width]
    radius = np.sqrt(((x - center_x) ** 2) + ((y - center_y) ** 2))
    low = (radius >= 1.0) & (radius <= 6.0)
    mid = (radius >= 16.0) & (radius <= 64.0)
    high = radius >= 80.0
    mid_mean = max(float(np.mean(power[mid])), 0.000000001)
    return {
        "dc_power": float(power[center_y, center_x]),
        "low_mean": float(np.mean(power[low])),
        "low_peak": float(np.max(power[low])),
        "mid_mean": float(np.mean(power[mid])),
        "high_mean": float(np.mean(power[high])),
        "low_mean_to_mid_mean": float(np.mean(power[low]) / mid_mean),
        "low_peak_to_mid_mean": float(np.max(power[low]) / mid_mean),
    }


def seam_metrics(channel: np.ndarray) -> dict[str, float]:
    values = channel.astype(np.float32)
    horizontal = np.abs(values - np.roll(values, shift=-1, axis=1))
    vertical = np.abs(values - np.roll(values, shift=-1, axis=0))
    edge_horizontal = np.abs(values[:, 0] - values[:, -1])
    edge_vertical = np.abs(values[0, :] - values[-1, :])
    return {
        "horizontal_wrap_to_neighbor_ratio": float(np.mean(edge_horizontal) / max(float(np.mean(horizontal)), 0.000001)),
        "vertical_wrap_to_neighbor_ratio": float(np.mean(edge_vertical) / max(float(np.mean(vertical)), 0.000001)),
        "max_wrap_delta": float(max(np.max(edge_horizontal), np.max(edge_vertical))),
    }


def verify_noise(noise: np.ndarray) -> dict[str, Any]:
    if noise.shape != (BLUE_SIZE, BLUE_SIZE, 4):
        return {"passed": False, "error": f"expected {BLUE_SIZE}x{BLUE_SIZE} RGBA, got {noise.shape}"}
    ign_delta = np.abs(noise[:, :, 1].astype(np.int16) - expected_ign(BLUE_SIZE, BLUE_SIZE).astype(np.int16))
    blue_spectrum = radial_spectrum_metrics(noise[:, :, 0])
    channel_seams = [seam_metrics(noise[:, :, channel]) for channel in range(4)]
    max_seam_ratio = max(
        max(item["horizontal_wrap_to_neighbor_ratio"], item["vertical_wrap_to_neighbor_ratio"])
        for item in channel_seams
    )
    passed = (
        int(np.max(ign_delta)) == 0
        and blue_spectrum["dc_power"] <= DC_POWER_MAX
        and blue_spectrum["low_mean_to_mid_mean"] <= LOW_MEAN_TO_MID_MEAN_MAX
        and blue_spectrum["low_peak_to_mid_mean"] <= LOW_PEAK_TO_MID_MEAN_MAX
        and max_seam_ratio <= MAX_SEAM_RATIO_MAX
    )
    return {
        "passed": bool(passed),
        "ign_max_quantized_delta": int(np.max(ign_delta)),
        "blue_spectrum": blue_spectrum,
        "max_seam_ratio": float(max_seam_ratio),
        "channel_seams": channel_seams,
        "thresholds": {
            "dc_power_max": DC_POWER_MAX,
            "low_mean_to_mid_mean_max": LOW_MEAN_TO_MID_MEAN_MAX,
            "low_peak_to_mid_mean_max": LOW_PEAK_TO_MID_MEAN_MAX,
            "max_seam_ratio_max": MAX_SEAM_RATIO_MAX,
        },
    }


def verify_assets(noise_path: Path, flow_path: Path) -> dict[str, Any]:
    noise = verify_noise(read_rgba(noise_path))
    flow = read_rgba(flow_path)
    flow_passed = flow.shape == (FLOW_SIZE, FLOW_SIZE, 4)
    return {
        "passed": bool(noise["passed"] and flow_passed),
        "noise_path": artifact_path(noise_path),
        "flow_path": artifact_path(flow_path),
        "noise": noise,
        "flow": {"passed": bool(flow_passed), "shape": list(flow.shape), "bytes": int(flow_path.stat().st_size)},
    }


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def parse_args() -> argparse.Namespace:
    root = repository_root()
    parser = argparse.ArgumentParser(description="Verify HECTON-8 baked noise assets.")
    parser.add_argument("--noise", type=Path, default=root / "Data" / "Textures" / "BlueNoise_RGBA.png")
    parser.add_argument("--flow", type=Path, default=root / "Data" / "Textures" / "AbyssalFlowField_LowTier_RGBA.png")
    parser.add_argument("--metrics", type=Path, default=root / "Data" / "Textures" / "NoiseBakeMetrics.verify.json")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    metrics = verify_assets(args.noise.resolve(), args.flow.resolve())
    write_json(args.metrics.resolve(), metrics)
    print(json.dumps(metrics, indent=2, sort_keys=True))
    return 0 if metrics["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
