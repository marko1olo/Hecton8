#!/usr/bin/env python3
"""HECTON-8 deterministic noise and low-tier flow texture baker."""

from __future__ import annotations

import argparse
import json
import math
import shutil
import subprocess
import time
from pathlib import Path
from typing import Any

import numpy as np
from PIL import Image


BLUE_SIZE = 256
FLOW_SIZE = 128
DEFAULT_SEED = 0x4845384E
DEFAULT_SWAPS = 2048
OPTIMIZER_TIMEOUT_SECONDS = 120
UINT_MAX_FLOAT = np.float64(0xFFFFFFFF)


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


def hash32_grid(x: np.ndarray, y: np.ndarray, seed: int) -> np.ndarray:
    with np.errstate(over="ignore"):
        value = (
            (x.astype(np.uint32) * np.uint32(0x9E3779B9))
            ^ (y.astype(np.uint32) * np.uint32(0x85EBCA6B))
            ^ np.uint32(seed)
        )
        value ^= value >> np.uint32(16)
        value *= np.uint32(0x7FEB352D)
        value ^= value >> np.uint32(15)
        value *= np.uint32(0x846CA68B)
        value ^= value >> np.uint32(16)
    return value


def hash01_grid(x: np.ndarray, y: np.ndarray, seed: int) -> np.ndarray:
    return hash32_grid(x, y, seed).astype(np.float64) / UINT_MAX_FLOAT


def generate_exact_ign(width: int, height: int) -> np.ndarray:
    y, x = np.mgrid[0:height, 0:width]
    inner = frac((x.astype(np.float64) * 0.06711056) + (y.astype(np.float64) * 0.00583715))
    return frac(52.9829189 * inner)


def make_highpass_rank(size: int, seed: int) -> tuple[np.ndarray, np.ndarray]:
    y, x = np.mgrid[0:size, 0:size]
    white = hash01_grid(x, y, seed).astype(np.float32)
    fy = np.fft.fftfreq(size) * size
    fx = np.fft.fftfreq(size) * size
    grid_y, grid_x = np.meshgrid(fy, fx, indexing="ij")
    radius = np.sqrt((grid_x * grid_x) + (grid_y * grid_y))
    highpass = np.power(radius / (radius + 6.0), 1.8)
    highpass[radius < 2.0] = 0.0
    filtered = np.fft.ifft2(np.fft.fft2(white - np.mean(white)) * highpass).real.astype(np.float32)
    order = np.argsort(filtered, axis=None, kind="mergesort")
    rank = np.empty(size * size, dtype=np.int32)
    rank[order] = np.arange(size * size, dtype=np.int32)
    return rank.reshape(size, size), filtered


def build_kernel(size: int, radius: int, sigma: float) -> tuple[list[int], np.ndarray, np.ndarray]:
    coords = list(range(-radius, radius + 1))
    kernel = np.empty((len(coords), len(coords)), dtype=np.float32)
    for row, dy in enumerate(coords):
        for col, dx in enumerate(coords):
            kernel[row, col] = math.exp(-((dx * dx) + (dy * dy)) / (2.0 * sigma * sigma))
    kernel /= np.sum(kernel)
    full = np.zeros((size, size), dtype=np.float32)
    for row, dy in enumerate(coords):
        for col, dx in enumerate(coords):
            full[dy % size, dx % size] = kernel[row, col]
    return coords, kernel, np.fft.fft2(full)


def void_cluster_relax(initial_rank: np.ndarray, seed_score: np.ndarray, swaps: int) -> np.ndarray:
    import heapq

    size = initial_rank.shape[0]
    total = size * size
    occupancy = initial_rank < (total // 2)
    coords, kernel, kernel_fft = build_kernel(size, radius=6, sigma=2.2)
    density = np.fft.ifft2(np.fft.fft2(occupancy.astype(np.float32)) * kernel_fft).real.astype(np.float32)
    flat_occ = occupancy.ravel()
    flat_density = density.ravel()
    cluster_heap: list[tuple[float, int]] = []
    void_heap: list[tuple[float, int]] = []

    for index in range(total):
        score = float(flat_density[index])
        if flat_occ[index]:
            heapq.heappush(cluster_heap, (-score, index))
        else:
            heapq.heappush(void_heap, (score, index))

    def current(index: int) -> float:
        return float(flat_density[index])

    def pop_cluster() -> int:
        while cluster_heap:
            negative_score, index = heapq.heappop(cluster_heap)
            if flat_occ[index] and abs((-negative_score) - current(index)) <= 0.000001:
                return index
        raise RuntimeError("Cluster heap exhausted.")

    def pop_void() -> int:
        while void_heap:
            score, index = heapq.heappop(void_heap)
            if (not flat_occ[index]) and abs(score - current(index)) <= 0.000001:
                return index
        raise RuntimeError("Void heap exhausted.")

    def update(center_y: int, center_x: int, sign: float) -> None:
        for row, dy in enumerate(coords):
            sample_y = (center_y + dy) % size
            base_index = sample_y * size
            kernel_row = kernel[row]
            for col, dx in enumerate(coords):
                index = base_index + ((center_x + dx) % size)
                flat_density[index] += sign * float(kernel_row[col])
                score = float(flat_density[index])
                if flat_occ[index]:
                    heapq.heappush(cluster_heap, (-score, index))
                else:
                    heapq.heappush(void_heap, (score, index))

    for _ in range(swaps):
        cluster_index = pop_cluster()
        void_index = pop_void()
        cluster_y, cluster_x = divmod(cluster_index, size)
        void_y, void_x = divmod(void_index, size)
        flat_occ[cluster_index] = False
        update(cluster_y, cluster_x, -1.0)
        flat_occ[void_index] = True
        update(void_y, void_x, 1.0)

    y, x = np.mgrid[0:size, 0:size]
    jitter = hash01_grid(x, y, DEFAULT_SEED ^ 0xD17ECAFE).ravel() * 0.000001
    score = seed_score.ravel().astype(np.float64) + jitter
    occupied = np.flatnonzero(flat_occ)
    empty = np.flatnonzero(~flat_occ)
    occupied = occupied[np.argsort(score[occupied], kind="mergesort")]
    empty = empty[np.argsort(score[empty], kind="mergesort")]
    output = np.empty(total, dtype=np.uint8)
    output[occupied] = ((np.arange(len(occupied), dtype=np.uint32) * 127) // max(1, len(occupied) - 1)).astype(
        np.uint8
    )
    output[empty] = (
        128 + ((np.arange(len(empty), dtype=np.uint32) * 127) // max(1, len(empty) - 1))
    ).astype(np.uint8)
    return output.reshape(size, size)


def generate_blue_noise(size: int, seed: int, swaps: int) -> np.ndarray:
    initial_rank, seed_score = make_highpass_rank(size, seed)
    return void_cluster_relax(initial_rank, seed_score, swaps)


def generate_jitter(size: int, seed: int) -> np.ndarray:
    y, x = np.mgrid[0:size, 0:size]
    hash_value = hash01_grid(x, y, seed ^ 0xB16B00B5)
    r2 = frac((x.astype(np.float64) + 0.5) * 0.7548776662466927)
    r2 = frac(r2 + ((y.astype(np.float64) + 0.5) * 0.5698402909980532))
    return frac((r2 * 0.875) + (hash_value * 0.125))


def generate_packed_noise(size: int, seed: int, swaps: int) -> tuple[np.ndarray, dict[str, Any]]:
    blue = generate_blue_noise(size, seed, swaps)
    ign = quantize_unorm(generate_exact_ign(size, size))
    jitter = quantize_unorm(generate_jitter(size, seed))
    dither = (255 - np.roll(blue, shift=(73, 41), axis=(0, 1))).astype(np.uint8)
    packed = np.dstack((blue, ign, jitter, dither)).astype(np.uint8)
    metrics = verify_packed_noise_array(packed)
    metrics["void_cluster_swaps"] = swaps
    metrics["seed"] = seed
    return packed, metrics


def generate_abyssal_flow_slice(size: int) -> np.ndarray:
    y, x = np.mgrid[0:size, 0:size]
    u = x.astype(np.float64) / float(size)
    v = y.astype(np.float64) / float(size)
    tau = math.tau
    swirl_a = np.sin(tau * ((2.0 * u) + v))
    swirl_b = np.cos(tau * (u - (3.0 * v)))
    long_roll = np.sin(tau * (v + (0.17 * np.sin(tau * u))))
    dead_zone = 0.5 + (0.5 * np.cos(tau * ((u * 0.5) - v)))
    flow_x = (0.42 + (0.23 * swirl_a) + (0.15 * long_roll) - (0.08 * swirl_b)) * (0.62 + (0.38 * dead_zone))
    flow_y = (-0.08 + (0.18 * np.cos(tau * ((u * 1.5) + (v * 0.5)))) + (0.11 * swirl_b)) * (
        0.62 + (0.38 * dead_zone)
    )
    magnitude = np.sqrt((flow_x * flow_x) + (flow_y * flow_y))
    max_magnitude = max(float(np.max(magnitude)), 0.000001)
    turbulence = np.clip(
        0.5 + (0.25 * np.sin(tau * ((4.0 * u) - (3.0 * v)))) + (0.25 * np.cos(tau * ((2.0 * u) + (5.0 * v)))),
        0.0,
        1.0,
    )
    return np.dstack(
        (
            quantize_unorm(np.clip((flow_x / max_magnitude) * 0.5 + 0.5, 0.0, 1.0)),
            quantize_unorm(np.clip((flow_y / max_magnitude) * 0.5 + 0.5, 0.0, 1.0)),
            quantize_unorm(np.clip(magnitude / max_magnitude, 0.0, 1.0)),
            quantize_unorm(turbulence),
        )
    ).astype(np.uint8)


def optimize_png(path: Path) -> str:
    for name, command in (
        ("optipng", ["optipng", "-o7", "-quiet", str(path)]),
        ("oxipng", ["oxipng", "-o", "max", "--strip", "safe", str(path)]),
        ("zopflipng", ["zopflipng", "-y", str(path), str(path)]),
    ):
        if shutil.which(name) is None:
            continue
        try:
            result = subprocess.run(
                command,
                cwd=repository_root(),
                capture_output=True,
                text=True,
                check=False,
                timeout=OPTIMIZER_TIMEOUT_SECONDS,
            )
        except subprocess.TimeoutExpired:
            return f"{name}_failed_timeout_{OPTIMIZER_TIMEOUT_SECONDS}s"
        return name if result.returncode == 0 else f"{name}_failed_exit_{result.returncode}"
    return "pillow_optimize_compress_level_9"


def save_png(path: Path, pixels: np.ndarray) -> dict[str, Any]:
    path.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(pixels, mode="RGBA").save(path, format="PNG", optimize=True, compress_level=9)
    before = path.stat().st_size
    optimizer = optimize_png(path)
    after = path.stat().st_size
    return {
        "path": artifact_path(path),
        "bytes_before_optimizer": before,
        "bytes_after_optimizer": after,
        "optimizer": optimizer,
    }


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


def verify_packed_noise_array(packed: np.ndarray) -> dict[str, Any]:
    if packed.shape != (BLUE_SIZE, BLUE_SIZE, 4):
        return {"passed": False, "error": f"expected {BLUE_SIZE}x{BLUE_SIZE} RGBA, got {packed.shape}"}
    expected_ign = quantize_unorm(generate_exact_ign(BLUE_SIZE, BLUE_SIZE))
    ign_delta = np.abs(packed[:, :, 1].astype(np.int16) - expected_ign.astype(np.int16))
    blue_spectrum = radial_spectrum_metrics(packed[:, :, 0])
    channel_seams = [seam_metrics(packed[:, :, channel]) for channel in range(4)]
    max_seam_ratio = max(max(item["horizontal_wrap_to_neighbor_ratio"], item["vertical_wrap_to_neighbor_ratio"]) for item in channel_seams)
    passed = (
        int(np.max(ign_delta)) == 0
        and blue_spectrum["dc_power"] <= 0.0001
        and blue_spectrum["low_mean_to_mid_mean"] <= 0.12
        and blue_spectrum["low_peak_to_mid_mean"] <= 0.5
        and max_seam_ratio <= 1.35
    )
    return {
        "passed": bool(passed),
        "ign_max_quantized_delta": int(np.max(ign_delta)),
        "blue_spectrum": blue_spectrum,
        "max_seam_ratio": float(max_seam_ratio),
        "channel_seams": channel_seams,
        "thresholds": {
            "dc_power_max": 0.0001,
            "low_mean_to_mid_mean_max": 0.12,
            "low_peak_to_mid_mean_max": 0.5,
            "max_seam_ratio_max": 1.35,
        },
    }


def verify_assets(noise_path: Path, flow_path: Path) -> dict[str, Any]:
    noise = verify_packed_noise_array(read_rgba(noise_path))
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
    parser = argparse.ArgumentParser(description="Bake HECTON-8 noise and low-tier flow lookup textures.")
    parser.add_argument("--output", type=Path, default=root / "Data" / "Textures" / "BlueNoise_RGBA.png")
    parser.add_argument("--flow-output", type=Path, default=root / "Data" / "Textures" / "AbyssalFlowField_LowTier_RGBA.png")
    parser.add_argument("--metrics", type=Path, default=root / "Data" / "Textures" / "NoiseBakeMetrics.json")
    parser.add_argument("--seed", default=hex(DEFAULT_SEED))
    parser.add_argument("--swaps", type=int, default=DEFAULT_SWAPS)
    parser.add_argument("--verify-only", action="store_true")
    parser.add_argument("--include-timing", action="store_true", help="Include volatile local bake_seconds in metrics.")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    noise_path = args.output.resolve()
    flow_path = args.flow_output.resolve()
    metrics_path = args.metrics.resolve()
    if args.verify_only:
        metrics = verify_assets(noise_path, flow_path)
        write_json(metrics_path, metrics)
        print(json.dumps(metrics, indent=2, sort_keys=True))
        return 0 if metrics["passed"] else 1

    seed = int(str(args.seed), 0)
    bake_start = time.perf_counter() if args.include_timing else None
    packed, bake_metrics = generate_packed_noise(BLUE_SIZE, seed, max(0, int(args.swaps)))
    if bake_start is not None:
        bake_metrics["bake_seconds"] = time.perf_counter() - bake_start
    flow = generate_abyssal_flow_slice(FLOW_SIZE)
    noise_save = save_png(noise_path, packed)
    flow_save = save_png(flow_path, flow)
    verification = verify_assets(noise_path, flow_path)
    metrics = {
        "status": "NOISE BAKED" if verification["passed"] else "NOISE BAKE FAILED",
        "bake": bake_metrics,
        "noise_save": noise_save,
        "flow_save": flow_save,
        "verification": verification,
    }
    write_json(metrics_path, metrics)
    print(json.dumps(metrics, indent=2, sort_keys=True))
    return 0 if verification["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
