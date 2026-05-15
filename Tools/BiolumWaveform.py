#!/usr/bin/env python3
"""Bake HECTON-8 bioluminescence rhythm profiles.

Outputs:
- Data/Visuals/Biolum_Profiles.bin: fixed little-endian binary profile payload.
- Data/Visuals/Biolum_Profiles.json: readable metadata and safety clamps.
- Data/Visuals/Biolum_Waveforms.png: static oscilloscope sheet.
- Data/Visuals/Biolum_Waveforms.gif: animated pulse preview.
- Data/Visuals/Biolum_Verification.json: one-hour validation metrics.
- Data/Visuals/Biolum_BinarySchema.json: machine-readable binary layout.

The runtime intent is shader-side deterministic emission, not physical light
simulation. Harmonics and palette data are precomputed offline to keep hot
paths allocation-free.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import struct
import zlib
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Dict, Iterable, List, Sequence, Tuple

import numpy as np
from PIL import Image, ImageDraw


ROOT_DIR = Path(__file__).resolve().parents[1]
DEFAULT_OUTPUT_DIR = ROOT_DIR / "Data" / "Visuals"

VERSION = 1
MAX_HARMONICS = 8
CURVE_SAMPLES = 256
GOD_COLOR_COUNT = 10
TOASTER_COLOR_COUNT = 2
SAFETY_CLAMP_HZ = 15.0
VERIFY_SECONDS = 3600.0
VERIFY_SAMPLE_RATE = 60.0
DRIFT_LIMIT_01 = 0.035
ORGANIC_JERK_LIMIT = 0.22

MAGIC = b"H8BIOLUM"
HEADER_STRUCT = struct.Struct("<8sIIIIIIIIII")
PROFILE_BASE_STRUCT = struct.Struct("<IIIII15f")
HARMONIC_STRUCT = struct.Struct("<4f")
PALETTE_STRUCT = struct.Struct("<III36f")
FLOAT32 = np.float32
FLOAT32_BYTES = 4
UINT32_BYTES = 4

FLAG_SAFETY_CLAMP = 1 << 0
FLAG_ACOUSTIC_REACTIVE = 1 << 1
FLAG_PREDATOR_VISIBLE = 1 << 2
FLAG_HIGH_TIER_OVERKILL = 1 << 3


Color = Tuple[float, float, float]


@dataclass(frozen=True)
class Harmonic:
    """One sine harmonic for the master biolum phase."""

    multiplier: float
    amplitude: float
    phase_rad: float
    shape_power: float


@dataclass(frozen=True)
class AcousticResponse:
    """Reactive strobe response for AcousticPing."""

    gain: float
    decay_seconds: float
    refractory_seconds: float
    phase_kick_rad: float
    strobe_hz: float
    strobe_width_01: float


@dataclass(frozen=True)
class PulseProfile:
    """Authoring profile for one ecosystem light rhythm."""

    name: str
    biome: str
    period_seconds: float
    baseline: float
    amplitude: float
    gamma: float
    noise_scale: float
    noise_rate_hz: float
    harmonics: Tuple[Harmonic, ...]
    acoustic: AcousticResponse
    flags: int


@dataclass(frozen=True)
class BiomePalette:
    """HDR color ramps for one biome."""

    name: str
    toaster: Tuple[Color, Color]
    god_mode: Tuple[Color, ...]


def fnv1a32(text: str) -> int:
    """Stable 32-bit hash for binary records."""

    value = 0x811C9DC5
    for byte in text.lower().encode("utf-8"):
        value ^= byte
        value = (value * 0x01000193) & 0xFFFFFFFF
    return value


def h(multiplier: float, amplitude: float, phase_degrees: float, shape_power: float) -> Harmonic:
    """Build a harmonic from author-facing degrees."""

    return Harmonic(
        multiplier=float(multiplier),
        amplitude=float(amplitude),
        phase_rad=math.radians(phase_degrees),
        shape_power=float(shape_power),
    )


def acc(
    gain: float,
    decay_seconds: float,
    refractory_seconds: float,
    phase_kick_degrees: float,
    strobe_hz: float,
    strobe_width_01: float,
) -> AcousticResponse:
    """Build an AcousticPing response."""

    return AcousticResponse(
        gain=float(gain),
        decay_seconds=float(decay_seconds),
        refractory_seconds=float(refractory_seconds),
        phase_kick_rad=math.radians(phase_kick_degrees),
        strobe_hz=float(strobe_hz),
        strobe_width_01=float(strobe_width_01),
    )


def lerp_color(a: Color, b: Color, t: float) -> Color:
    """Linear HDR color interpolation."""

    inv = 1.0 - t
    return (a[0] * inv + b[0] * t, a[1] * inv + b[1] * t, a[2] * inv + b[2] * t)


def ramp10(stops: Sequence[Color]) -> Tuple[Color, ...]:
    """Expand authored HDR stops into a 10-color GOD_MODE gradient."""

    if len(stops) < 2:
        raise ValueError("A GOD_MODE ramp needs at least two color stops.")

    colors: List[Color] = []
    segment_count = len(stops) - 1
    for index in range(GOD_COLOR_COUNT):
        position = index / float(GOD_COLOR_COUNT - 1)
        scaled = position * segment_count
        segment = min(int(math.floor(scaled)), segment_count - 1)
        local_t = scaled - segment
        colors.append(lerp_color(stops[segment], stops[segment + 1], local_t))
    return tuple(colors)


def build_palettes() -> Tuple[BiomePalette, ...]:
    """Define HDR biome ramps for shader agents."""

    return (
        BiomePalette(
            "Abyssal Trench",
            ((0.00, 0.20, 0.34), (0.08, 1.55, 2.40)),
            ramp10(((0.0015, 0.0023, 0.0031), (0.00, 0.18, 0.35), (0.00, 0.88, 1.35), (0.28, 1.80, 2.60))),
        ),
        BiomePalette(
            "Hydrothermal Vent",
            ((0.18, 0.06, 0.02), (2.80, 0.72, 0.18)),
            ramp10(((0.010, 0.006, 0.004), (0.50, 0.10, 0.04), (2.10, 0.42, 0.08), (3.80, 1.35, 0.24))),
        ),
        BiomePalette(
            "Silt Cathedral",
            ((0.05, 0.10, 0.11), (0.80, 1.10, 0.62)),
            ramp10(((0.008, 0.012, 0.018), (0.12, 0.18, 0.16), (0.46, 0.72, 0.42), (1.18, 1.40, 0.72))),
        ),
        BiomePalette(
            "Coral Ruins",
            ((0.12, 0.02, 0.10), (1.75, 0.28, 1.38)),
            ramp10(((0.010, 0.004, 0.018), (0.38, 0.03, 0.42), (1.35, 0.12, 1.05), (2.25, 0.45, 1.80))),
        ),
        BiomePalette(
            "Kelp Ghost Forest",
            ((0.02, 0.16, 0.06), (0.52, 1.65, 0.34)),
            ramp10(((0.002, 0.010, 0.006), (0.04, 0.34, 0.10), (0.20, 1.10, 0.22), (0.72, 2.10, 0.48))),
        ),
        BiomePalette(
            "Wreckyard Noir",
            ((0.04, 0.07, 0.10), (1.15, 0.62, 0.24)),
            ramp10(((0.006, 0.008, 0.014), (0.10, 0.18, 0.24), (0.55, 0.28, 0.04), (1.45, 0.74, 0.26))),
        ),
        BiomePalette(
            "Alabaster Pools",
            ((0.08, 0.18, 0.18), (1.10, 2.30, 1.85)),
            ramp10(((0.004, 0.010, 0.012), (0.14, 0.42, 0.38), (0.60, 1.38, 1.15), (1.48, 2.75, 2.25))),
        ),
        BiomePalette(
            "Hadal Aurora",
            ((0.03, 0.05, 0.18), (1.30, 0.74, 2.90)),
            ramp10(((0.002, 0.004, 0.020), (0.04, 0.16, 0.72), (0.42, 0.54, 1.85), (1.50, 0.92, 3.40))),
        ),
    )


def build_profiles() -> Tuple[PulseProfile, ...]:
    """Define 20 organic pulse profiles."""

    reactive = FLAG_ACOUSTIC_REACTIVE
    overkill = FLAG_HIGH_TIER_OVERKILL
    predator = FLAG_PREDATOR_VISIBLE
    return (
        PulseProfile("Deep-Sea Aurora", "Hadal Aurora", 38.0, 0.08, 2.25, 1.55, 0.32, 0.018, (h(1.0, 0.78, 0, 1.10), h(1.7, 0.26, 64, 1.35), h(2.9, 0.16, 181, 1.70), h(4.2, 0.08, 291, 1.25)), acc(0.42, 2.8, 1.1, 18, 3.5, 0.36), reactive | overkill),
        PulseProfile("Predator Warning", "Abyssal Trench", 2.4, 0.12, 3.40, 1.20, 0.18, 0.090, (h(1.0, 0.62, 0, 1.05), h(2.0, 0.48, 37, 1.45), h(3.0, 0.32, 118, 1.80), h(5.0, 0.22, 251, 1.20), h(7.0, 0.11, 309, 1.10)), acc(1.20, 0.85, 0.42, 90, 11.0, 0.42), reactive | predator),
        PulseProfile("Mating Call", "Coral Ruins", 14.5, 0.10, 2.70, 1.42, 0.28, 0.032, (h(1.0, 0.72, 12, 1.25), h(1.5, 0.31, 96, 1.60), h(2.25, 0.20, 172, 1.35), h(3.5, 0.12, 261, 1.15), h(5.5, 0.07, 314, 1.10)), acc(0.55, 2.2, 0.9, 24, 5.0, 0.34), reactive | overkill),
        PulseProfile("Vent Communion", "Hydrothermal Vent", 9.2, 0.18, 3.10, 1.35, 0.22, 0.045, (h(1.0, 0.70, 4, 1.10), h(2.0, 0.28, 79, 1.50), h(3.25, 0.17, 151, 1.80), h(4.6, 0.10, 248, 1.20), h(6.0, 0.06, 330, 1.10)), acc(0.70, 1.4, 0.7, 35, 6.2, 0.40), reactive),
        PulseProfile("Wreck Memory", "Wreckyard Noir", 27.0, 0.06, 1.85, 1.80, 0.36, 0.014, (h(1.0, 0.82, 0, 1.35), h(1.33, 0.26, 110, 1.70), h(2.66, 0.14, 207, 1.25), h(3.9, 0.08, 303, 1.10)), acc(0.33, 3.4, 1.7, 12, 2.4, 0.28), reactive),
        PulseProfile("Abyssal Lull", "Abyssal Trench", 42.0, 0.04, 1.30, 2.10, 0.42, 0.011, (h(1.0, 0.86, 18, 1.60), h(1.8, 0.22, 141, 1.85), h(2.7, 0.11, 238, 1.30), h(4.1, 0.05, 312, 1.15)), acc(0.24, 4.5, 2.4, 8, 1.8, 0.24), reactive),
        PulseProfile("Larval Drift", "Alabaster Pools", 18.0, 0.09, 2.05, 1.48, 0.31, 0.028, (h(1.0, 0.68, 5, 1.15), h(2.4, 0.24, 82, 1.45), h(3.1, 0.19, 163, 1.60), h(5.2, 0.09, 244, 1.20), h(7.3, 0.05, 322, 1.10)), acc(0.46, 2.0, 0.8, 21, 4.2, 0.36), reactive | overkill),
        PulseProfile("Territorial Bloom", "Coral Ruins", 5.8, 0.14, 2.95, 1.26, 0.20, 0.070, (h(1.0, 0.66, 0, 1.05), h(2.0, 0.36, 58, 1.35), h(3.5, 0.23, 137, 1.70), h(5.0, 0.14, 229, 1.25), h(6.8, 0.07, 310, 1.10)), acc(0.88, 1.1, 0.52, 48, 8.5, 0.38), reactive | predator),
        PulseProfile("Stress Bleed", "Hydrothermal Vent", 3.6, 0.10, 3.25, 1.18, 0.16, 0.105, (h(1.0, 0.54, 0, 1.00), h(2.0, 0.40, 61, 1.35), h(4.0, 0.28, 149, 1.75), h(6.5, 0.16, 240, 1.20), h(9.0, 0.08, 318, 1.10)), acc(1.05, 0.72, 0.38, 72, 13.5, 0.42), reactive | predator),
        PulseProfile("Silt Ghost", "Silt Cathedral", 31.0, 0.05, 1.65, 1.95, 0.44, 0.013, (h(1.0, 0.76, 22, 1.45), h(1.6, 0.28, 104, 1.80), h(2.2, 0.16, 205, 1.25), h(3.7, 0.09, 289, 1.12)), acc(0.28, 3.8, 1.9, 15, 2.1, 0.24), reactive),
        PulseProfile("Kelp Constellation", "Kelp Ghost Forest", 21.0, 0.07, 2.15, 1.62, 0.38, 0.024, (h(1.0, 0.74, 0, 1.18), h(1.4, 0.30, 72, 1.50), h(2.8, 0.19, 153, 1.65), h(4.4, 0.10, 251, 1.22), h(6.0, 0.05, 319, 1.10)), acc(0.44, 2.7, 1.2, 28, 3.2, 0.30), reactive | overkill),
        PulseProfile("Moon-Tide Choir", "Abyssal Trench", 60.0, 0.04, 1.75, 1.70, 0.40, 0.009, (h(1.0, 0.88, 0, 1.30), h(2.0, 0.21, 123, 1.60), h(3.0, 0.12, 239, 1.25), h(5.0, 0.05, 311, 1.10)), acc(0.25, 5.0, 2.5, 6, 1.4, 0.22), reactive | overkill),
        PulseProfile("Scanner Sympathy", "Wreckyard Noir", 6.6, 0.11, 2.45, 1.32, 0.24, 0.052, (h(1.0, 0.60, 7, 1.10), h(2.0, 0.34, 88, 1.50), h(3.0, 0.21, 181, 1.30), h(4.5, 0.11, 266, 1.15), h(6.5, 0.06, 333, 1.05)), acc(0.82, 1.0, 0.5, 60, 9.0, 0.36), reactive),
        PulseProfile("Coral Sleep Cycle", "Coral Ruins", 48.0, 0.03, 1.40, 2.25, 0.46, 0.010, (h(1.0, 0.83, 14, 1.55), h(1.25, 0.27, 117, 1.90), h(2.5, 0.12, 221, 1.32), h(4.0, 0.06, 304, 1.14)), acc(0.20, 4.6, 2.2, 7, 1.6, 0.20), reactive),
        PulseProfile("Leviathan Wake", "Hadal Aurora", 11.5, 0.16, 3.60, 1.16, 0.22, 0.040, (h(1.0, 0.68, 0, 1.08), h(1.8, 0.36, 71, 1.35), h(3.0, 0.24, 167, 1.60), h(4.2, 0.15, 248, 1.25), h(5.7, 0.09, 310, 1.10), h(7.5, 0.05, 346, 1.05)), acc(0.96, 1.8, 0.7, 42, 6.8, 0.44), reactive | predator | overkill),
        PulseProfile("Harvest Invitation", "Alabaster Pools", 12.8, 0.10, 2.35, 1.44, 0.30, 0.035, (h(1.0, 0.65, 4, 1.12), h(1.7, 0.30, 91, 1.45), h(2.6, 0.19, 185, 1.60), h(4.1, 0.11, 270, 1.20), h(5.8, 0.06, 331, 1.08)), acc(0.50, 2.1, 0.9, 30, 4.6, 0.34), reactive),
        PulseProfile("Infection Pulse", "Kelp Ghost Forest", 4.8, 0.13, 2.85, 1.24, 0.26, 0.082, (h(1.0, 0.58, 0, 1.06), h(2.0, 0.37, 66, 1.40), h(3.8, 0.24, 143, 1.72), h(5.5, 0.13, 226, 1.25), h(8.5, 0.07, 314, 1.10)), acc(0.92, 0.95, 0.48, 67, 10.5, 0.40), reactive | predator),
        PulseProfile("Thermal Vent Alarm", "Hydrothermal Vent", 1.6, 0.17, 3.55, 1.14, 0.16, 0.090, (h(1.0, 0.48, 0, 1.05), h(2.4, 0.30, 73, 1.28), h(4.4, 0.20, 149, 1.42), h(7.0, 0.11, 244, 1.15), h(10.0, 0.05, 318, 1.05)), acc(1.35, 0.55, 0.32, 95, 18.0, 0.46), reactive | predator),
        PulseProfile("Colony Respiration", "Silt Cathedral", 16.5, 0.08, 2.10, 1.50, 0.34, 0.030, (h(1.0, 0.78, 0, 1.22), h(1.9, 0.25, 102, 1.52), h(2.7, 0.16, 199, 1.38), h(4.8, 0.09, 292, 1.12), h(6.2, 0.05, 341, 1.08)), acc(0.42, 2.6, 1.1, 17, 3.6, 0.30), reactive | overkill),
        PulseProfile("Emergency Beacon", "Wreckyard Noir", 1.8, 0.20, 3.95, 1.10, 0.12, 0.105, (h(1.0, 0.42, 0, 1.04), h(3.0, 0.30, 47, 1.18), h(5.5, 0.22, 115, 1.26), h(8.0, 0.13, 218, 1.12), h(11.5, 0.06, 304, 1.05)), acc(1.50, 0.45, 0.30, 110, 22.0, 0.48), reactive | predator),
    )


def hash_u32_array(values: np.ndarray, seed: int) -> np.ndarray:
    """Vectorized deterministic integer hash."""

    x = values.astype(np.uint32)
    x ^= np.uint32(seed)
    x ^= x >> np.uint32(16)
    x *= np.uint32(0x7FEB352D)
    x ^= x >> np.uint32(15)
    x *= np.uint32(0x846CA68B)
    x ^= x >> np.uint32(16)
    return x


def gradient_noise_1d(x: np.ndarray, seed: int) -> np.ndarray:
    """Deterministic 1D Perlin-style gradient noise."""

    xi = np.floor(x).astype(np.int64)
    xf = x - xi
    h0 = hash_u32_array(xi, seed).astype(np.float64) / float(0xFFFFFFFF)
    h1 = hash_u32_array(xi + 1, seed).astype(np.float64) / float(0xFFFFFFFF)
    g0 = (h0 * 2.0) - 1.0
    g1 = (h1 * 2.0) - 1.0
    d0 = g0 * xf
    d1 = g1 * (xf - 1.0)
    fade = xf * xf * xf * (xf * (xf * 6.0 - 15.0) + 10.0)
    return d0 + (d1 - d0) * fade


def effective_hz(profile: PulseProfile, harmonic: Harmonic) -> float:
    """Return clamp-safe harmonic frequency in Hz."""

    raw_hz = harmonic.multiplier / max(profile.period_seconds, 0.001)
    return min(raw_hz, SAFETY_CLAMP_HZ)


def raw_max_hz(profile: PulseProfile) -> float:
    """Return the highest authored harmonic or strobe rate."""

    harmonic_hz = [harmonic.multiplier / max(profile.period_seconds, 0.001) for harmonic in profile.harmonics]
    harmonic_hz.append(profile.acoustic.strobe_hz)
    return max(harmonic_hz)


def profile_flags(profile: PulseProfile) -> int:
    """Return derived binary flags."""

    flags = profile.flags
    if raw_max_hz(profile) > SAFETY_CLAMP_HZ:
        flags |= FLAG_SAFETY_CLAMP
    return flags


def evaluate_profile(profile: PulseProfile, seconds: np.ndarray) -> np.ndarray:
    """Evaluate profile brightness without AcousticPing overlay."""

    seed = fnv1a32(profile.name)
    phase_noise = gradient_noise_1d(seconds * profile.noise_rate_hz + 3.17, seed)
    amp_noise = gradient_noise_1d(seconds * profile.noise_rate_hz * 0.63 + 8.71, seed ^ 0xA53C9E1B)
    phase_mod = phase_noise * profile.noise_scale
    amp_mod = 1.0 + amp_noise * profile.noise_scale * 0.32

    accumulated = np.zeros_like(seconds, dtype=np.float64)
    weight_sum = 0.0
    for harmonic in profile.harmonics:
        freq_hz = effective_hz(profile, harmonic)
        sine = np.sin((math.tau * freq_hz * seconds) + harmonic.phase_rad + phase_mod)
        shaped = np.sign(sine) * np.power(np.abs(sine), harmonic.shape_power)
        accumulated += shaped * harmonic.amplitude
        weight_sum += abs(harmonic.amplitude)

    if weight_sum <= 0.0001:
        return np.full_like(seconds, profile.baseline, dtype=np.float32)

    normalized = np.clip(accumulated / weight_sum, -1.0, 1.0)
    emission01 = np.clip(0.5 + normalized * 0.5, 0.0, 1.0)
    organic = np.power(emission01, profile.gamma) * np.clip(amp_mod, 0.82, 1.18)
    brightness = profile.baseline + organic * profile.amplitude
    return np.clip(brightness, 0.0, 8.0).astype(np.float32)


def evaluate_acoustic_overlay(
    profile: PulseProfile,
    seconds: np.ndarray,
    ping_time_seconds: float,
) -> np.ndarray:
    """Evaluate the AcousticPing strobe overlay."""

    after_ping = seconds - ping_time_seconds
    active = after_ping >= 0.0
    if not np.any(active):
        return np.zeros_like(seconds, dtype=np.float32)

    strobe_hz = min(profile.acoustic.strobe_hz, SAFETY_CLAMP_HZ)
    phase = (math.tau * strobe_hz * after_ping) + profile.acoustic.phase_kick_rad
    sine_lobe = np.maximum(np.sin(phase), 0.0)
    width_power = 1.0 + (1.0 - np.clip(profile.acoustic.strobe_width_01, 0.05, 0.95)) * 5.0
    pulse = np.power(sine_lobe, width_power)
    envelope = np.exp(-np.maximum(after_ping, 0.0) / max(profile.acoustic.decay_seconds, 0.001))
    overlay = pulse * envelope * profile.acoustic.gain
    overlay = np.where(active, overlay, 0.0)
    return overlay.astype(np.float32)


def curve_samples(profile: PulseProfile) -> np.ndarray:
    """Build a fixed 256-sample normalized curve for preview/runtime LUT use."""

    sample_seconds = np.linspace(0.0, profile.period_seconds, CURVE_SAMPLES, endpoint=False)
    brightness = evaluate_profile(profile, sample_seconds)
    max_value = max(float(np.max(brightness)), 0.0001)
    return np.clip(brightness / max_value, 0.0, 1.0).astype(np.float32)


def verify_profile(profile: PulseProfile, seconds: np.ndarray) -> Dict[str, Any]:
    """Run one-hour waveform safety and drift checks."""

    brightness = evaluate_profile(profile, seconds).astype(np.float64)
    if not np.all(np.isfinite(brightness)):
        raise FloatingPointError(f"{profile.name} produced non-finite brightness.")

    centered_time = seconds.astype(np.float64) - float(np.mean(seconds))
    centered_signal = brightness - float(np.mean(brightness))
    denom = float(np.dot(centered_time, centered_time))
    slope = 0.0 if denom <= 0.0001 else float(np.dot(centered_time, centered_signal) / denom)
    drift_01 = abs(slope * VERIFY_SECONDS) / max(float(np.max(brightness)), 0.0001)
    deltas = np.diff(brightness)
    second_deltas = np.diff(deltas)
    jerk95 = float(np.percentile(np.abs(second_deltas), 95.0)) if second_deltas.size > 0 else 0.0
    safety_active = raw_max_hz(profile) > SAFETY_CLAMP_HZ
    effective_max = min(raw_max_hz(profile), SAFETY_CLAMP_HZ)

    return {
        "name": profile.name,
        "rawMaxHz": round(raw_max_hz(profile), 5),
        "effectiveMaxHz": round(effective_max, 5),
        "safetyClampHz": SAFETY_CLAMP_HZ,
        "safetyClampActive": bool(safety_active),
        "meanBrightness": round(float(np.mean(brightness)), 6),
        "minBrightness": round(float(np.min(brightness)), 6),
        "maxBrightness": round(float(np.max(brightness)), 6),
        "dcDrift01": round(drift_01, 8),
        "dcDriftPass": bool(drift_01 <= DRIFT_LIMIT_01),
        "organicJerk95": round(jerk95, 8),
        "organicPass": bool(jerk95 <= ORGANIC_JERK_LIMIT),
    }


def flatten_colors(colors: Iterable[Color]) -> List[float]:
    """Flatten color tuples for binary packing."""

    values: List[float] = []
    for color in colors:
        values.extend((float(color[0]), float(color[1]), float(color[2])))
    return values


def pack_profile(profile: PulseProfile, index: int) -> bytes:
    """Pack one fixed-size profile record."""

    harmonics = list(profile.harmonics)
    if len(harmonics) > MAX_HARMONICS:
        raise ValueError(f"{profile.name} has more than {MAX_HARMONICS} harmonics.")

    biome_hash = fnv1a32(profile.biome)
    flags = profile_flags(profile)
    raw_hz = raw_max_hz(profile)
    effective_hz_max = min(raw_hz, SAFETY_CLAMP_HZ)
    acoustic_strobe_hz = min(profile.acoustic.strobe_hz, SAFETY_CLAMP_HZ)
    base = PROFILE_BASE_STRUCT.pack(
        fnv1a32(profile.name),
        index,
        biome_hash,
        flags,
        len(harmonics),
        profile.period_seconds,
        profile.baseline,
        profile.amplitude,
        profile.gamma,
        profile.noise_scale,
        profile.noise_rate_hz,
        SAFETY_CLAMP_HZ,
        raw_hz,
        effective_hz_max,
        profile.acoustic.gain,
        profile.acoustic.decay_seconds,
        profile.acoustic.refractory_seconds,
        profile.acoustic.phase_kick_rad,
        acoustic_strobe_hz,
        profile.acoustic.strobe_width_01,
    )

    harmonic_bytes = bytearray()
    for harmonic in harmonics:
        harmonic_bytes.extend(
            HARMONIC_STRUCT.pack(
                harmonic.multiplier,
                harmonic.amplitude,
                harmonic.phase_rad,
                harmonic.shape_power,
            )
        )

    for _ in range(MAX_HARMONICS - len(harmonics)):
        harmonic_bytes.extend(HARMONIC_STRUCT.pack(0.0, 0.0, 0.0, 1.0))

    samples = curve_samples(profile).astype("<f4", copy=False).tobytes()
    return bytes(base) + bytes(harmonic_bytes) + samples


def pack_palette(palette: BiomePalette, index: int) -> bytes:
    """Pack one fixed-size palette record."""

    colors = flatten_colors(palette.toaster) + flatten_colors(palette.god_mode)
    if len(colors) != 36:
        raise ValueError(f"{palette.name} palette needs 36 float values.")
    return PALETTE_STRUCT.pack(fnv1a32(palette.name), index, 0, *colors)


def binary_payload(profiles: Sequence[PulseProfile], palettes: Sequence[BiomePalette]) -> bytes:
    """Build binary payload without header."""

    payload = bytearray()
    for index, profile in enumerate(profiles):
        payload.extend(pack_profile(profile, index))
    for index, palette in enumerate(palettes):
        payload.extend(pack_palette(palette, index))
    return bytes(payload)


def write_binary(path: Path, profiles: Sequence[PulseProfile], palettes: Sequence[BiomePalette]) -> Dict[str, Any]:
    """Write and verify the fixed binary profile file."""

    payload = binary_payload(profiles, palettes)
    profile_stride = PROFILE_BASE_STRUCT.size + (HARMONIC_STRUCT.size * MAX_HARMONICS) + (CURVE_SAMPLES * 4)
    palette_stride = PALETTE_STRUCT.size
    crc = zlib.crc32(payload) & 0xFFFFFFFF
    header = HEADER_STRUCT.pack(
        MAGIC,
        VERSION,
        len(profiles),
        len(palettes),
        MAX_HARMONICS,
        CURVE_SAMPLES,
        GOD_COLOR_COUNT,
        TOASTER_COLOR_COUNT,
        profile_stride,
        palette_stride,
        crc,
    )
    path.write_bytes(header + payload)
    return readback_binary(path)


def readback_binary(path: Path) -> Dict[str, Any]:
    """Validate header, size, and payload CRC."""

    data = path.read_bytes()
    header = data[: HEADER_STRUCT.size]
    payload = data[HEADER_STRUCT.size :]
    (
        magic,
        version,
        profile_count,
        palette_count,
        max_harmonics,
        curve_sample_count,
        god_color_count,
        toaster_color_count,
        profile_stride,
        palette_stride,
        payload_crc,
    ) = HEADER_STRUCT.unpack(header)

    expected_size = HEADER_STRUCT.size + (profile_count * profile_stride) + (palette_count * palette_stride)
    actual_crc = zlib.crc32(payload) & 0xFFFFFFFF
    if magic != MAGIC:
        raise ValueError(f"Invalid magic in {path}: {magic!r}")
    if version != VERSION:
        raise ValueError(f"Invalid version in {path}: {version}")
    if expected_size != len(data):
        raise ValueError(f"Invalid binary size: expected {expected_size}, got {len(data)}")
    if actual_crc != payload_crc:
        raise ValueError(f"Invalid payload CRC: expected {payload_crc}, got {actual_crc}")

    return {
        "path": str(path.relative_to(ROOT_DIR)),
        "bytes": len(data),
        "payloadBytes": len(payload),
        "profileCount": profile_count,
        "paletteCount": palette_count,
        "maxHarmonics": max_harmonics,
        "curveSamples": curve_sample_count,
        "godColorCount": god_color_count,
        "toasterColorCount": toaster_color_count,
        "profileStride": profile_stride,
        "paletteStride": palette_stride,
        "payloadCrc32": f"0x{payload_crc:08X}",
    }


def binary_schema(binary_info: Dict[str, Any]) -> Dict[str, Any]:
    """Return a machine-readable schema for C#/CI importers."""

    profile_base_fields = [
        ("profileHash", "uint32"),
        ("profileIndex", "uint32"),
        ("biomeHash", "uint32"),
        ("flags", "uint32"),
        ("harmonicCount", "uint32"),
        ("periodSeconds", "float32"),
        ("baseline", "float32"),
        ("amplitude", "float32"),
        ("gamma", "float32"),
        ("noiseScale", "float32"),
        ("noiseRateHz", "float32"),
        ("safetyClampHz", "float32"),
        ("rawMaxHz", "float32"),
        ("effectiveMaxHz", "float32"),
        ("acousticGain", "float32"),
        ("acousticDecaySeconds", "float32"),
        ("acousticRefractorySeconds", "float32"),
        ("acousticPhaseKickRad", "float32"),
        ("acousticStrobeHz", "float32"),
        ("acousticWidth01", "float32"),
    ]
    header_fields = [
        ("magic", "bytes8"),
        ("version", "uint32"),
        ("profileCount", "uint32"),
        ("paletteCount", "uint32"),
        ("maxHarmonics", "uint32"),
        ("curveSamples", "uint32"),
        ("godColorCount", "uint32"),
        ("toasterColorCount", "uint32"),
        ("profileStride", "uint32"),
        ("paletteStride", "uint32"),
        ("payloadCrc32", "uint32"),
    ]

    offset = 0
    header = []
    for field_name, field_type in header_fields:
        byte_count = 8 if field_type == "bytes8" else UINT32_BYTES
        header.append({"name": field_name, "type": field_type, "offset": offset, "bytes": byte_count})
        offset += byte_count

    offset = 0
    profile_base = []
    for field_name, field_type in profile_base_fields:
        byte_count = UINT32_BYTES if field_type == "uint32" else FLOAT32_BYTES
        profile_base.append({"name": field_name, "type": field_type, "offset": offset, "bytes": byte_count})
        offset += byte_count

    harmonic_offset = PROFILE_BASE_STRUCT.size
    curve_offset = harmonic_offset + (MAX_HARMONICS * HARMONIC_STRUCT.size)
    palette_payload_offset = 12

    return {
        "schema": "H8_BIOLUM_BINARY_SCHEMA",
        "version": VERSION,
        "endianness": "little",
        "binary": {
            "path": binary_info["path"],
            "bytes": binary_info["bytes"],
            "payloadCrc32": binary_info["payloadCrc32"],
        },
        "constants": {
            "headerBytes": HEADER_STRUCT.size,
            "profileBaseBytes": PROFILE_BASE_STRUCT.size,
            "harmonicBytes": HARMONIC_STRUCT.size,
            "paletteBytes": PALETTE_STRUCT.size,
            "profileStride": binary_info["profileStride"],
            "paletteStride": binary_info["paletteStride"],
            "maxHarmonics": MAX_HARMONICS,
            "curveSamples": CURVE_SAMPLES,
            "godColorCount": GOD_COLOR_COUNT,
            "toasterColorCount": TOASTER_COLOR_COUNT,
            "safetyClampHz": SAFETY_CLAMP_HZ,
        },
        "flags": {
            "SAFETY_CLAMP": FLAG_SAFETY_CLAMP,
            "ACOUSTIC_REACTIVE": FLAG_ACOUSTIC_REACTIVE,
            "PREDATOR_VISIBLE": FLAG_PREDATOR_VISIBLE,
            "HIGH_TIER_OVERKILL": FLAG_HIGH_TIER_OVERKILL,
        },
        "header": header,
        "profileRecord": {
            "base": profile_base,
            "harmonics": {
                "offset": harmonic_offset,
                "count": MAX_HARMONICS,
                "stride": HARMONIC_STRUCT.size,
                "fields": [
                    {"name": "multiplier", "type": "float32", "offset": 0, "bytes": FLOAT32_BYTES},
                    {"name": "amplitude", "type": "float32", "offset": 4, "bytes": FLOAT32_BYTES},
                    {"name": "phaseRad", "type": "float32", "offset": 8, "bytes": FLOAT32_BYTES},
                    {"name": "shapePower", "type": "float32", "offset": 12, "bytes": FLOAT32_BYTES},
                ],
            },
            "curveSamples": {
                "offset": curve_offset,
                "count": CURVE_SAMPLES,
                "type": "float32",
                "bytes": CURVE_SAMPLES * FLOAT32_BYTES,
            },
        },
        "paletteRecord": {
            "base": [
                {"name": "paletteHash", "type": "uint32", "offset": 0, "bytes": UINT32_BYTES},
                {"name": "paletteIndex", "type": "uint32", "offset": 4, "bytes": UINT32_BYTES},
                {"name": "flags", "type": "uint32", "offset": 8, "bytes": UINT32_BYTES},
            ],
            "colors": {
                "offset": palette_payload_offset,
                "type": "float32",
                "floatCount": 36,
                "toasterRgbCount": TOASTER_COLOR_COUNT,
                "godModeRgbCount": GOD_COLOR_COUNT,
            },
        },
    }


def write_schema(path: Path, binary_info: Dict[str, Any]) -> None:
    """Write the binary schema sidecar."""

    write_json(path, binary_schema(binary_info))


def verify_binary_records(path: Path) -> Dict[str, Any]:
    """Validate every profile, harmonic, curve, and palette record."""

    binary_info = readback_binary(path)
    data = path.read_bytes()
    offset = HEADER_STRUCT.size
    safety_clamped_records = 0
    max_curve_sample = 0.0

    for expected_index in range(binary_info["profileCount"]):
        record_offset = offset + (expected_index * binary_info["profileStride"])
        base = PROFILE_BASE_STRUCT.unpack_from(data, record_offset)
        (
            _profile_hash,
            profile_index,
            _biome_hash,
            flags,
            harmonic_count,
        ) = base[:5]
        floats = base[5:]

        if profile_index != expected_index:
            raise ValueError(f"Profile index mismatch: expected {expected_index}, got {profile_index}")
        if harmonic_count < 1 or harmonic_count > MAX_HARMONICS:
            raise ValueError(f"Profile {expected_index} has invalid harmonic count {harmonic_count}")
        if not all(math.isfinite(value) for value in floats):
            raise ValueError(f"Profile {expected_index} contains non-finite base float.")

        period_seconds = floats[0]
        safety_clamp_hz = floats[6]
        raw_max_hz = floats[7]
        effective_max_hz = floats[8]
        acoustic_strobe_hz = floats[13]
        if period_seconds <= 0.0:
            raise ValueError(f"Profile {expected_index} has non-positive period.")
        if abs(safety_clamp_hz - SAFETY_CLAMP_HZ) > 0.0001:
            raise ValueError(f"Profile {expected_index} safety clamp mismatch.")
        if effective_max_hz > safety_clamp_hz + 0.0001:
            raise ValueError(f"Profile {expected_index} effective frequency exceeds safety clamp.")
        if acoustic_strobe_hz > safety_clamp_hz + 0.0001:
            raise ValueError(f"Profile {expected_index} acoustic strobe exceeds safety clamp.")
        if raw_max_hz > safety_clamp_hz and (flags & FLAG_SAFETY_CLAMP) == 0:
            raise ValueError(f"Profile {expected_index} needs safety flag but does not have it.")
        if (flags & FLAG_SAFETY_CLAMP) != 0:
            safety_clamped_records += 1

        harmonic_offset = record_offset + PROFILE_BASE_STRUCT.size
        for harmonic_index in range(MAX_HARMONICS):
            harmonic = HARMONIC_STRUCT.unpack_from(
                data,
                harmonic_offset + (harmonic_index * HARMONIC_STRUCT.size),
            )
            if not all(math.isfinite(value) for value in harmonic):
                raise ValueError(f"Profile {expected_index} harmonic {harmonic_index} is non-finite.")
            if harmonic_index < harmonic_count:
                multiplier, amplitude, _phase_rad, shape_power = harmonic
                if multiplier <= 0.0 or amplitude <= 0.0 or shape_power <= 0.0:
                    raise ValueError(
                        f"Profile {expected_index} harmonic {harmonic_index} has invalid positive fields."
                    )

        curve_offset = harmonic_offset + (MAX_HARMONICS * HARMONIC_STRUCT.size)
        curve = np.frombuffer(data, dtype="<f4", count=CURVE_SAMPLES, offset=curve_offset)
        if not np.all(np.isfinite(curve)):
            raise ValueError(f"Profile {expected_index} curve has non-finite samples.")
        if float(np.min(curve)) < -0.0001 or float(np.max(curve)) > 1.0001:
            raise ValueError(f"Profile {expected_index} curve samples exceed normalized range.")
        max_curve_sample = max(max_curve_sample, float(np.max(curve)))

    palette_offset = offset + (binary_info["profileCount"] * binary_info["profileStride"])
    for expected_index in range(binary_info["paletteCount"]):
        record_offset = palette_offset + (expected_index * binary_info["paletteStride"])
        palette_record = PALETTE_STRUCT.unpack_from(data, record_offset)
        _palette_hash, palette_index, _flags = palette_record[:3]
        color_values = palette_record[3:]
        if palette_index != expected_index:
            raise ValueError(f"Palette index mismatch: expected {expected_index}, got {palette_index}")
        if not all(math.isfinite(value) for value in color_values):
            raise ValueError(f"Palette {expected_index} has non-finite color values.")
        if min(color_values) < 0.0:
            raise ValueError(f"Palette {expected_index} has negative HDR color values.")

    return {
        "profileCount": binary_info["profileCount"],
        "paletteCount": binary_info["paletteCount"],
        "profileStride": binary_info["profileStride"],
        "paletteStride": binary_info["paletteStride"],
        "safetyClampedRecords": safety_clamped_records,
        "maxCurveSample": round(max_curve_sample, 6),
        "payloadCrc32": binary_info["payloadCrc32"],
    }


def profile_to_json(profile: PulseProfile, verification: Dict[str, Any]) -> Dict[str, Any]:
    """Serialize profile metadata."""

    return {
        "idHash": f"0x{fnv1a32(profile.name):08X}",
        "name": profile.name,
        "biome": profile.biome,
        "periodSeconds": profile.period_seconds,
        "baseline": profile.baseline,
        "amplitude": profile.amplitude,
        "gamma": profile.gamma,
        "noiseScale": profile.noise_scale,
        "noiseRateHz": profile.noise_rate_hz,
        "flags": profile_flags(profile),
        "safety": {
            "rawMaxHz": verification["rawMaxHz"],
            "effectiveMaxHz": verification["effectiveMaxHz"],
            "safetyClampHz": SAFETY_CLAMP_HZ,
            "safetyClampActive": verification["safetyClampActive"],
            "policy": "Any harmonic or AcousticPing strobe above 15Hz is clamped before export and preview.",
        },
        "harmonics": [
            {
                "multiplier": harmonic.multiplier,
                "rawHz": round(harmonic.multiplier / max(profile.period_seconds, 0.001), 5),
                "effectiveHz": round(effective_hz(profile, harmonic), 5),
                "amplitude": harmonic.amplitude,
                "phaseRad": harmonic.phase_rad,
                "shapePower": harmonic.shape_power,
            }
            for harmonic in profile.harmonics
        ],
        "acousticPing": {
            "gain": profile.acoustic.gain,
            "decaySeconds": profile.acoustic.decay_seconds,
            "refractorySeconds": profile.acoustic.refractory_seconds,
            "phaseKickRad": profile.acoustic.phase_kick_rad,
            "rawStrobeHz": profile.acoustic.strobe_hz,
            "effectiveStrobeHz": min(profile.acoustic.strobe_hz, SAFETY_CLAMP_HZ),
            "strobeWidth01": profile.acoustic.strobe_width_01,
            "transport": "EnvironmentSignal/AcousticPing scalar input; no string event names.",
        },
    }


def palette_to_json(palette: BiomePalette) -> Dict[str, Any]:
    """Serialize palette metadata."""

    return {
        "idHash": f"0x{fnv1a32(palette.name):08X}",
        "name": palette.name,
        "toaster": [[round(c, 6) for c in color] for color in palette.toaster],
        "godMode": [[round(c, 6) for c in color] for color in palette.god_mode],
    }


def color_for_profile(profile: PulseProfile, palette_lookup: Dict[str, BiomePalette]) -> Tuple[int, int, int]:
    """Pick display color from the GOD_MODE ramp."""

    palette = palette_lookup[profile.biome]
    color = palette.god_mode[min(6, len(palette.god_mode) - 1)]
    scale = max(color)
    if scale <= 0.001:
        return (40, 120, 150)
    return (
        int(np.clip(color[0] / scale, 0.0, 1.0) * 255),
        int(np.clip(color[1] / scale, 0.0, 1.0) * 255),
        int(np.clip(color[2] / scale, 0.0, 1.0) * 255),
    )


def draw_waveform_png(path: Path, profiles: Sequence[PulseProfile], palettes: Sequence[BiomePalette]) -> None:
    """Draw a 20-profile oscilloscope sheet without matplotlib."""

    width = 1800
    height = 1200
    columns = 4
    rows = 5
    margin = 34
    cell_w = (width - margin * 2) // columns
    cell_h = (height - margin * 2) // rows
    image = Image.new("RGB", (width, height), (4, 6, 8))
    draw = ImageDraw.Draw(image)
    palette_lookup = {palette.name: palette for palette in palettes}

    for index, profile in enumerate(profiles):
        column = index % columns
        row = index // columns
        x0 = margin + column * cell_w
        y0 = margin + row * cell_h
        x1 = x0 + cell_w - 18
        y1 = y0 + cell_h - 18
        draw.rectangle((x0, y0, x1, y1), outline=(36, 58, 66))
        draw.text((x0 + 8, y0 + 8), f"{index:02d} {profile.name}", fill=(190, 218, 220))
        draw.text((x0 + 8, y0 + 25), profile.biome, fill=(112, 140, 148))

        seconds = np.linspace(0.0, min(60.0, profile.period_seconds * 2.0), 360, endpoint=False)
        wave = evaluate_profile(profile, seconds)
        ping = evaluate_acoustic_overlay(profile, seconds, ping_time_seconds=min(12.0, seconds[-1] * 0.35))
        combined = np.clip(wave + ping, 0.0, 8.0)
        local_max = max(float(np.max(combined)), 0.001)
        local_min = float(np.min(combined))
        denom = max(local_max - local_min, 0.001)
        plot_top = y0 + 52
        plot_bottom = y1 - 12
        plot_w = x1 - x0 - 22
        points: List[Tuple[int, int]] = []
        for sample_index, value in enumerate(combined):
            px = x0 + 10 + int((sample_index / float(len(combined) - 1)) * plot_w)
            normalized = (float(value) - local_min) / denom
            py = plot_bottom - int(normalized * (plot_bottom - plot_top))
            points.append((px, py))
        draw.line(points, fill=color_for_profile(profile, palette_lookup), width=2)
        raw_hz = raw_max_hz(profile)
        clamp_text = "CLAMP" if raw_hz > SAFETY_CLAMP_HZ else "SAFE"
        draw.text((x1 - 112, y0 + 8), clamp_text, fill=(255, 175, 86) if clamp_text == "CLAMP" else (120, 220, 180))

    image.save(path)


def draw_waveform_gif(path: Path, profiles: Sequence[PulseProfile], palettes: Sequence[BiomePalette]) -> None:
    """Draw an animated 4x5 pulse grid."""

    width = 960
    height = 720
    columns = 4
    rows = 5
    frames: List[Image.Image] = []
    palette_lookup = {palette.name: palette for palette in palettes}
    seconds = np.linspace(0.0, 16.0, 48, endpoint=False)
    for frame_index, second in enumerate(seconds):
        image = Image.new("RGB", (width, height), (4, 6, 8))
        draw = ImageDraw.Draw(image)
        for index, profile in enumerate(profiles):
            column = index % columns
            row = index // columns
            cx = int((column + 0.5) * width / columns)
            cy = int((row + 0.5) * height / rows)
            radius = 26
            brightness = float(evaluate_profile(profile, np.array([second], dtype=np.float64))[0])
            pulse01 = np.clip(brightness / max(profile.baseline + profile.amplitude, 0.001), 0.0, 1.0)
            color = color_for_profile(profile, palette_lookup)
            scaled_color = tuple(int(channel * (0.20 + 0.80 * pulse01)) for channel in color)
            glow_radius = int(radius + pulse01 * 34.0)
            draw.ellipse((cx - glow_radius, cy - glow_radius, cx + glow_radius, cy + glow_radius), outline=scaled_color, width=2)
            draw.ellipse((cx - radius, cy - radius, cx + radius, cy + radius), fill=scaled_color)
            draw.text((cx - 88, cy + 46), profile.name[:24], fill=(180, 206, 212))
        frames.append(image)

    frames[0].save(path, save_all=True, append_images=frames[1:], duration=80, loop=0)


def write_json(path: Path, data: Dict[str, Any]) -> None:
    """Write stable pretty JSON."""

    path.write_text(json.dumps(data, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def sha256_file(path: Path) -> str:
    """Return SHA-256 for a generated artifact."""

    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def write_manifest(path: Path, artifact_paths: Sequence[Path], verification: Dict[str, Any]) -> None:
    """Write an integrity manifest for generated biolum artifacts."""

    artifacts = []
    for artifact_path in artifact_paths:
        artifacts.append(
            {
                "path": str(artifact_path.relative_to(ROOT_DIR)),
                "bytes": artifact_path.stat().st_size,
                "sha256": sha256_file(artifact_path),
            }
        )

    manifest = {
        "schema": "H8_BIOLUM_MANIFEST",
        "version": VERSION,
        "status": verification["status"],
        "payloadCrc32": verification["binary"]["payloadCrc32"],
        "profileCount": verification["summary"]["profileCount"],
        "paletteCount": verification["summary"]["paletteCount"],
        "safetyClampProfiles": verification["summary"]["safetyClampProfiles"],
        "maxDcDrift01": verification["summary"]["maxDcDrift01"],
        "maxOrganicJerk95": verification["summary"]["maxOrganicJerk95"],
        "artifacts": artifacts,
    }
    write_json(path, manifest)


def verify_manifest(manifest_path: Path) -> Dict[str, Any]:
    """Verify manifest artifact sizes, hashes, and binary CRC."""

    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    artifact_count = 0
    for artifact in manifest["artifacts"]:
        artifact_path = ROOT_DIR / artifact["path"]
        if not artifact_path.exists():
            raise FileNotFoundError(f"Manifest artifact missing: {artifact['path']}")
        actual_bytes = artifact_path.stat().st_size
        if actual_bytes != artifact["bytes"]:
            raise ValueError(
                f"Manifest byte mismatch for {artifact['path']}: "
                f"expected {artifact['bytes']}, got {actual_bytes}"
            )
        actual_sha = sha256_file(artifact_path)
        if actual_sha != artifact["sha256"]:
            raise ValueError(f"Manifest SHA-256 mismatch for {artifact['path']}")
        artifact_count += 1

    binary_path = ROOT_DIR / "Data" / "Visuals" / "Biolum_Profiles.bin"
    for artifact in manifest["artifacts"]:
        if artifact["path"].replace("\\", "/").endswith("Biolum_Profiles.bin"):
            binary_path = ROOT_DIR / artifact["path"]
            break

    binary_info = readback_binary(binary_path)
    if binary_info["payloadCrc32"] != manifest["payloadCrc32"]:
        raise ValueError(
            f"Manifest CRC mismatch: expected {manifest['payloadCrc32']}, "
            f"got {binary_info['payloadCrc32']}"
        )

    return {
        "status": manifest["status"],
        "artifactCount": artifact_count,
        "payloadCrc32": manifest["payloadCrc32"],
        "profileCount": manifest["profileCount"],
        "paletteCount": manifest["paletteCount"],
    }


def verify_generated_package(output_dir: Path) -> Dict[str, Any]:
    """Verify every generated biolum sidecar without rebuilding artifacts."""

    manifest_info = verify_manifest(output_dir / "Biolum_Manifest.json")
    binary_info = verify_binary_records(output_dir / "Biolum_Profiles.bin")
    schema = json.loads((output_dir / "Biolum_BinarySchema.json").read_text(encoding="utf-8"))
    profiles_json = json.loads((output_dir / "Biolum_Profiles.json").read_text(encoding="utf-8"))
    verification_json = json.loads((output_dir / "Biolum_Verification.json").read_text(encoding="utf-8"))

    if profiles_json["status"] != "RHYTHMS COMPOSED":
        raise ValueError(f"Profile JSON status mismatch: {profiles_json['status']}")
    if verification_json["status"] != "RHYTHMS COMPOSED":
        raise ValueError(f"Verification JSON status mismatch: {verification_json['status']}")
    if manifest_info["status"] != verification_json["status"]:
        raise ValueError("Manifest status does not match verification JSON.")
    if manifest_info["payloadCrc32"] != binary_info["payloadCrc32"]:
        raise ValueError("Manifest CRC does not match binary record validator.")
    if profiles_json["binary"]["payloadCrc32"] != binary_info["payloadCrc32"]:
        raise ValueError("Profile JSON binary CRC does not match binary validator.")
    if verification_json["binary"]["payloadCrc32"] != binary_info["payloadCrc32"]:
        raise ValueError("Verification JSON binary CRC does not match binary validator.")

    profile_count = len(profiles_json["profiles"])
    palette_count = len(profiles_json["palettes"])
    safety_count = sum(
        1 for profile in profiles_json["profiles"] if profile["safety"]["safetyClampActive"]
    )
    if profile_count != manifest_info["profileCount"] or profile_count != binary_info["profileCount"]:
        raise ValueError("Profile count mismatch across manifest, binary, and profile JSON.")
    if palette_count != manifest_info["paletteCount"] or palette_count != binary_info["paletteCount"]:
        raise ValueError("Palette count mismatch across manifest, binary, and profile JSON.")
    if safety_count != binary_info["safetyClampedRecords"]:
        raise ValueError("Safety-clamp count mismatch between profile JSON and binary records.")
    if verification_json["summary"]["safetyClampProfiles"] != safety_count:
        raise ValueError("Safety-clamp count mismatch in verification summary.")
    if schema["constants"]["profileStride"] != binary_info["profileStride"]:
        raise ValueError("Schema profile stride does not match binary header.")
    if schema["constants"]["paletteStride"] != binary_info["paletteStride"]:
        raise ValueError("Schema palette stride does not match binary header.")
    expected_curve_offset = PROFILE_BASE_STRUCT.size + (MAX_HARMONICS * HARMONIC_STRUCT.size)
    if schema["profileRecord"]["curveSamples"]["offset"] != expected_curve_offset:
        raise ValueError("Schema curve offset does not match packed profile record layout.")

    return {
        "status": verification_json["status"],
        "artifactCount": manifest_info["artifactCount"],
        "profileCount": profile_count,
        "paletteCount": palette_count,
        "safetyClampedRecords": safety_count,
        "payloadCrc32": binary_info["payloadCrc32"],
    }


def run(output_dir: Path) -> Dict[str, Any]:
    """Generate all artifacts and return verification summary."""

    output_dir.mkdir(parents=True, exist_ok=True)
    profiles = build_profiles()
    palettes = build_palettes()
    if len(profiles) != 20:
        raise ValueError(f"Expected 20 profiles, got {len(profiles)}")

    verify_seconds = np.arange(0.0, VERIFY_SECONDS, 1.0 / VERIFY_SAMPLE_RATE, dtype=np.float64)
    verification = [verify_profile(profile, verify_seconds) for profile in profiles]
    if not all(item["dcDriftPass"] for item in verification):
        failed = [item["name"] for item in verification if not item["dcDriftPass"]]
        raise ValueError(f"DC offset drift failed for: {', '.join(failed)}")
    if not all(item["organicPass"] for item in verification):
        failed = [item["name"] for item in verification if not item["organicPass"]]
        raise ValueError(f"Organic waveform jerk failed for: {', '.join(failed)}")

    binary_info = write_binary(output_dir / "Biolum_Profiles.bin", profiles, palettes)
    profile_json = {
        "schema": "H8_BIOLUM_PROFILES",
        "version": VERSION,
        "status": "RHYTHMS COMPOSED",
        "binary": binary_info,
        "runtimeContract": {
            "masterPhaseName": "_BiolumMasterPhase",
            "qualityTiers": {
                "TOASTER": "2-color biome lerp, fixed 256-sample profile curve, no dynamic light spam.",
                "LOW": "2-color lerp plus one harmonic phase scalar per material family.",
                "MIDDLE": "5 sampled GOD_MODE colors and AcousticPing overlay.",
                "HIGH": "10-color GOD_MODE ramp, full harmonic reconstruction in shader.",
                "ULTRA": "10-color ramp plus richer SSGI/volumetric consumers in VISUAL_SYNC only.",
            },
        },
        "palettes": [palette_to_json(palette) for palette in palettes],
        "profiles": [
            profile_to_json(profile, verification[index]) for index, profile in enumerate(profiles)
        ],
    }
    profiles_json_path = output_dir / "Biolum_Profiles.json"
    verification_json_path = output_dir / "Biolum_Verification.json"
    png_path = output_dir / "Biolum_Waveforms.png"
    gif_path = output_dir / "Biolum_Waveforms.gif"
    manifest_path = output_dir / "Biolum_Manifest.json"
    binary_path = output_dir / "Biolum_Profiles.bin"
    schema_path = output_dir / "Biolum_BinarySchema.json"

    write_schema(schema_path, binary_info)
    write_json(profiles_json_path, profile_json)

    verification_json = {
        "schema": "H8_BIOLUM_VERIFICATION",
        "version": VERSION,
        "status": "RHYTHMS COMPOSED",
        "simulatedSeconds": VERIFY_SECONDS,
        "sampleRateHz": VERIFY_SAMPLE_RATE,
        "driftLimit01": DRIFT_LIMIT_01,
        "organicJerkLimit": ORGANIC_JERK_LIMIT,
        "binary": binary_info,
        "profiles": verification,
        "summary": {
            "profileCount": len(profiles),
            "paletteCount": len(palettes),
            "safetyClampProfiles": sum(1 for item in verification if item["safetyClampActive"]),
            "maxDcDrift01": round(max(float(item["dcDrift01"]) for item in verification), 8),
            "maxOrganicJerk95": round(max(float(item["organicJerk95"]) for item in verification), 8),
        },
    }
    write_json(verification_json_path, verification_json)
    draw_waveform_png(png_path, profiles, palettes)
    draw_waveform_gif(gif_path, profiles, palettes)
    write_manifest(
        manifest_path,
        (
            binary_path,
            profiles_json_path,
            verification_json_path,
            png_path,
            gif_path,
            schema_path,
        ),
        verification_json,
    )
    return verification_json


def main() -> int:
    """CLI entry point."""

    parser = argparse.ArgumentParser(description="Bake HECTON-8 biolum rhythm profiles.")
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=DEFAULT_OUTPUT_DIR,
        help="Artifact directory. Defaults to Data/Visuals.",
    )
    parser.add_argument(
        "--verify-manifest",
        action="store_true",
        help="Verify Data/Visuals/Biolum_Manifest.json without regenerating artifacts.",
    )
    parser.add_argument(
        "--verify-binary",
        action="store_true",
        help="Verify Biolum_Profiles.bin record structure without regenerating artifacts.",
    )
    parser.add_argument(
        "--verify-all",
        action="store_true",
        help="Verify manifest, binary records, schema, and JSON sidecars without regenerating artifacts.",
    )
    args = parser.parse_args()

    if args.verify_all:
        package_info = verify_generated_package(args.output_dir)
        print("BIOLUM PACKAGE VERIFIED")
        print(f"status={package_info['status']}")
        print(f"artifactCount={package_info['artifactCount']}")
        print(f"profiles={package_info['profileCount']}")
        print(f"palettes={package_info['paletteCount']}")
        print(f"safetyClampedRecords={package_info['safetyClampedRecords']}")
        print(f"payloadCrc32={package_info['payloadCrc32']}")
        return 0

    if args.verify_manifest:
        manifest_info = verify_manifest(args.output_dir / "Biolum_Manifest.json")
        print("BIOLUM MANIFEST VERIFIED")
        print(f"status={manifest_info['status']}")
        print(f"artifactCount={manifest_info['artifactCount']}")
        print(f"profiles={manifest_info['profileCount']}")
        print(f"palettes={manifest_info['paletteCount']}")
        print(f"payloadCrc32={manifest_info['payloadCrc32']}")
        return 0

    if args.verify_binary:
        binary_info = verify_binary_records(args.output_dir / "Biolum_Profiles.bin")
        print("BIOLUM BINARY VERIFIED")
        print(f"profiles={binary_info['profileCount']}")
        print(f"palettes={binary_info['paletteCount']}")
        print(f"safetyClampedRecords={binary_info['safetyClampedRecords']}")
        print(f"maxCurveSample={binary_info['maxCurveSample']}")
        print(f"payloadCrc32={binary_info['payloadCrc32']}")
        return 0

    summary = run(args.output_dir)
    print("RHYTHMS COMPOSED")
    print(f"profiles={summary['summary']['profileCount']}")
    print(f"palettes={summary['summary']['paletteCount']}")
    print(f"safetyClampProfiles={summary['summary']['safetyClampProfiles']}")
    print(f"maxDcDrift01={summary['summary']['maxDcDrift01']}")
    print(f"maxOrganicJerk95={summary['summary']['maxOrganicJerk95']}")
    print(f"binary={summary['binary']['path']} bytes={summary['binary']['bytes']} crc={summary['binary']['payloadCrc32']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
