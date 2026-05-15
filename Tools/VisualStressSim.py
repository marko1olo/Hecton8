#!/usr/bin/env python3
"""Offline visual scalability stress estimator for HECTON-8.

Evidence boundary:
    STATIC_CONFIG / PYTHON_OFFLINE only. This is not Unity Profiler,
    Memory Profiler, Frame Debugger, RenderDoc, Play Mode, or player-build proof.
"""

from __future__ import annotations

import argparse
import json
import math
import random
import statistics
import sys
from pathlib import Path
from typing import Any, Dict, Iterable, List, Tuple


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_MATRIX_PATH = ROOT / "Data/System/Visual_Scalability_Matrix.json"
DEFAULT_REPORT_PATH = ROOT / "Docs/AgentLogs/VisualStressSim_VISUAL_LOD_GRADE_ARCHITECT.json"
REQUIRED_TIERS = ("TOASTER", "DECK", "PRO", "GOD_MODE")
REQUIRED_TOP_LEVEL_PATHS = (
    "selfAuditExpectations.toasterMaxVramMbIncludingDriverReserve",
    "selfAuditExpectations.godModeMinimumDensityRatioVsPro",
    "godModeFallbacks",
)
REQUIRED_TIER_PATHS = (
    "vramGuardMb",
    "estimatedVramMb",
    "dynamicResolution.defaultRenderScale",
    "shaderFeatures.pom.tapCount",
    "shaderFeatures.ssr.enabled",
    "shaderFeatures.ssr.maxSteps",
    "shaderFeatures.ssr.resolutionFraction",
    "shaderFeatures.screenSpaceRefractions.enabled",
    "shaderFeatures.screenSpaceRefractions.sampleCount",
    "shaderFeatures.ssdo.tapCount",
    "shaderFeatures.waterSurfaceSubdivisions",
    "shaderFeatures.shadowPcfTaps",
    "shaderFeatures.dynamicShadowCasters",
    "shaderFeatures.bloom",
    "volumetricScattering.raymarchSteps",
    "volumetricScattering.resolutionFraction",
    "volumetricScattering.noise.layers",
    "volumetricScattering.noise.octavesPerLayer",
    "particles.totalBudget",
    "particles.gpuParticles",
    "textures.detailNormalMaps",
    "textures.microDetailOrm",
    "textures.streamingBudgetMb",
)
REQUIRED_GOD_FALLBACK_KEYS = (
    "renderScale",
    "pom",
    "ssr",
    "screenSpaceRefractions",
    "volumetricScattering",
    "volumetricNoise",
    "particleBudget",
    "textureOverrides",
    "shadowQuality",
    "postProcessing",
)
REQUIRED_GOD_FALLBACK_REFS = (
    "shaderFeatures.pom.fallback",
    "shaderFeatures.ssr.fallback",
    "shaderFeatures.screenSpaceRefractions.fallback",
    "shaderFeatures.fallback",
    "volumetricScattering.fallback",
    "volumetricScattering.noise.fallback",
    "particles.fallback",
    "textures.fallback",
)


def require(condition: bool, message: str, failures: List[str]) -> None:
    if not condition:
        failures.append(message)


def load_matrix(path: Path) -> Dict[str, Any]:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def tiers_by_name(data: Dict[str, Any]) -> Dict[str, Dict[str, Any]]:
    tiers = {}
    for tier in data.get("tiers", []):
        tiers[str(tier.get("tier", ""))] = tier
    return tiers


def estimated_vram_mb(tier: Dict[str, Any]) -> float:
    values = tier.get("estimatedVramMb", {})
    total = 0.0
    for value in values.values():
        total += float(value)
    return total


def get_nested(data: Dict[str, Any], path: str) -> Any:
    current: Any = data
    for token in path.split("."):
        if not isinstance(current, dict):
            return None
        current = current.get(token)
    return current


def visual_density_score(tier: Dict[str, Any]) -> float:
    shader = tier["shaderFeatures"]
    volume = tier["volumetricScattering"]
    noise = volume["noise"]
    particles = tier["particles"]
    textures = tier["textures"]

    particle_density = float(particles["totalBudget"]) / 1000.0
    raymarch_density = (
        float(volume["raymarchSteps"])
        * max(1.0, float(noise["layers"]))
        * max(1.0, float(noise["octavesPerLayer"]))
        * 0.90
    )
    pom_density = float(shader["pom"]["tapCount"]) * 6.0
    ssr_density = float(shader["ssr"]["maxSteps"]) * float(shader["ssr"]["resolutionFraction"]) * 2.0
    refraction_density = float(shader["screenSpaceRefractions"]["sampleCount"]) * 20.0
    ssdo_density = float(shader["ssdo"]["tapCount"]) * 8.0
    water_density = float(shader["waterSurfaceSubdivisions"]) * 0.30
    shadow_density = float(shader["shadowPcfTaps"]) * 5.0
    texture_density = 0.0
    if bool(textures["detailNormalMaps"]):
        texture_density += 180.0
    if bool(textures["microDetailOrm"]):
        texture_density += 180.0

    return (
        particle_density
        + raymarch_density
        + pom_density
        + ssr_density
        + refraction_density
        + ssdo_density
        + water_density
        + shadow_density
        + texture_density
    )


def estimate_gpu_cycles(tier: Dict[str, Any], scenario: Dict[str, float]) -> float:
    shader = tier["shaderFeatures"]
    volume = tier["volumetricScattering"]
    noise = volume["noise"]
    particles = tier["particles"]
    textures = tier["textures"]
    dyn = tier["dynamicResolution"]

    scale = float(dyn["defaultRenderScale"])
    area_scale = scale * scale
    particle_factor = 0.45 if bool(particles["gpuParticles"]) else 0.18
    particle_cycles = float(particles["totalBudget"]) / 1000.0 * particle_factor * scenario["particleStorm"]
    volume_cycles = (
        float(volume["raymarchSteps"])
        * max(1.0, float(noise["layers"]))
        * max(1.0, float(noise["octavesPerLayer"]))
        * float(volume["resolutionFraction"])
        * float(volume["resolutionFraction"])
        * 2.4
        * scenario["fogLoad"]
    )
    pom_cycles = float(shader["pom"]["tapCount"]) * 7.5 * scenario["heroSurface"]
    ssr_cycles = (
        float(shader["ssr"]["maxSteps"])
        * float(shader["ssr"]["resolutionFraction"])
        * 5.5
        * scenario["reflectiveLoad"]
    )
    refraction_cycles = float(shader["screenSpaceRefractions"]["sampleCount"]) * 14.0 * scenario["reflectiveLoad"]
    ssdo_cycles = float(shader["ssdo"]["tapCount"]) * 10.0 * scenario["caveLoad"]
    shadow_cycles = float(shader["shadowPcfTaps"]) * max(1.0, float(shader["dynamicShadowCasters"])) * 0.65
    water_cycles = float(shader["waterSurfaceSubdivisions"]) * 0.18
    texture_cycles = float(textures["streamingBudgetMb"]) * 0.012 * scenario["heroSurface"]
    post_cycles = 35.0 if bool(shader["bloom"]) else 8.0

    return area_scale * (
        120.0
        + particle_cycles
        + volume_cycles
        + pom_cycles
        + ssr_cycles
        + refraction_cycles
        + ssdo_cycles
        + shadow_cycles
        + water_cycles
        + texture_cycles
        + post_cycles
    )


def deterministic_stress_frames(seed: int, frame_count: int) -> Iterable[Dict[str, float]]:
    rng = random.Random(seed)
    for frame in range(frame_count):
        pulse = 0.5 + 0.5 * math.sin(frame * 0.071)
        storm = 1.0
        if frame % 127 in range(0, 22):
            storm = 1.75
        cave = 1.0 + (0.65 if frame % 191 in range(40, 95) else 0.0)
        hero = 1.0 + (0.85 if frame % 83 in range(8, 29) else 0.0)
        reflective = 1.0 + (0.75 if frame % 149 in range(60, 103) else 0.0)
        fog = 1.0 + pulse * 0.35
        jitter = 0.96 + rng.random() * 0.08
        yield {
            "particleStorm": storm * jitter,
            "caveLoad": cave * jitter,
            "heroSurface": hero * jitter,
            "reflectiveLoad": reflective * jitter,
            "fogLoad": fog * jitter,
        }


def percentile(values: List[float], pct: float) -> float:
    if not values:
        return 0.0
    ordered = sorted(values)
    index = int(math.ceil((pct / 100.0) * len(ordered))) - 1
    index = max(0, min(index, len(ordered) - 1))
    return ordered[index]


def stress_tier(tier: Dict[str, Any], seed: int, frame_count: int) -> Dict[str, float]:
    cycles = [estimate_gpu_cycles(tier, scenario) for scenario in deterministic_stress_frames(seed, frame_count)]
    return {
        "visualDensityScore": round(visual_density_score(tier), 3),
        "estimatedVramMb": round(estimated_vram_mb(tier), 3),
        "gpuCyclesMean": round(statistics.fmean(cycles), 3),
        "gpuCyclesP95": round(percentile(cycles, 95), 3),
        "gpuCyclesPeak": round(max(cycles), 3),
    }


def required_shape_failures(data: Dict[str, Any], tiers: Dict[str, Dict[str, Any]]) -> List[str]:
    failures: List[str] = []
    for path in REQUIRED_TOP_LEVEL_PATHS:
        require(get_nested(data, path) is not None, f"missing top-level field {path}", failures)

    for tier_name in REQUIRED_TIERS:
        tier = tiers.get(tier_name)
        if tier is None:
            failures.append(f"missing tier {tier_name}")
            continue
        for path in REQUIRED_TIER_PATHS:
            require(get_nested(tier, path) is not None, f"missing tier field {tier_name}.{path}", failures)

    god = tiers.get("GOD_MODE")
    if god is not None:
        for path in REQUIRED_GOD_FALLBACK_REFS:
            require(get_nested(god, path) is not None, f"missing tier field GOD_MODE.{path}", failures)

    return failures


def validate_matrix(data: Dict[str, Any], report: Dict[str, Any]) -> List[str]:
    failures: List[str] = []
    tiers = tiers_by_name(data)
    expectations = data["selfAuditExpectations"]

    for tier in REQUIRED_TIERS:
        require(tier in tiers, f"missing tier {tier}", failures)

    if failures:
        return failures

    require(data.get("status") == "SCALABILITY CRYSTALLIZED", "status must be SCALABILITY CRYSTALLIZED", failures)
    require(tiers["TOASTER"]["particles"]["totalBudget"] == 5000, "TOASTER particle budget must be 5000", failures)
    require(tiers["GOD_MODE"]["particles"]["totalBudget"] == 200000, "GOD_MODE particle budget must be 200000", failures)
    require(tiers["GOD_MODE"]["shaderFeatures"]["pom"]["tapCount"] == 16, "GOD_MODE POM taps must be 16", failures)
    require(bool(tiers["GOD_MODE"]["shaderFeatures"]["ssr"]["enabled"]), "GOD_MODE SSR must be enabled", failures)
    require(
        bool(tiers["GOD_MODE"]["shaderFeatures"]["screenSpaceRefractions"]["enabled"]),
        "GOD_MODE screen-space refractions must be enabled",
        failures,
    )
    require(tiers["GOD_MODE"]["volumetricScattering"]["noise"]["layers"] == 3, "GOD_MODE noise layers must be 3", failures)
    require(bool(tiers["GOD_MODE"]["textures"]["detailNormalMaps"]), "GOD_MODE detail normals must be enabled", failures)
    require(bool(tiers["GOD_MODE"]["textures"]["microDetailOrm"]), "GOD_MODE micro-detail ORM must be enabled", failures)

    toaster_vram = report["tiers"]["TOASTER"]["estimatedVramMb"]
    max_toaster_vram = float(expectations["toasterMaxVramMbIncludingDriverReserve"])
    require(toaster_vram <= max_toaster_vram, f"TOASTER VRAM {toaster_vram} exceeds {max_toaster_vram}", failures)

    god_density = report["tiers"]["GOD_MODE"]["visualDensityScore"]
    pro_density = report["tiers"]["PRO"]["visualDensityScore"]
    density_ratio = god_density / max(0.001, pro_density)
    report["selfAudit"]["godModeDensityRatioVsPro"] = round(density_ratio, 3)
    require(
        density_ratio >= float(expectations["godModeMinimumDensityRatioVsPro"]),
        f"GOD_MODE density ratio {density_ratio:.3f} below required {expectations['godModeMinimumDensityRatioVsPro']}",
        failures,
    )

    fallback_map = data.get("godModeFallbacks", {})
    for key in REQUIRED_GOD_FALLBACK_KEYS:
        require(key in fallback_map and bool(fallback_map[key]), f"missing GOD_MODE fallback key {key}", failures)

    god = tiers["GOD_MODE"]
    for path in REQUIRED_GOD_FALLBACK_REFS:
        value = get_nested(god, path)
        require(isinstance(value, str) and value.startswith("godModeFallbacks."), f"GOD_MODE missing fallback ref {path}", failures)
        if isinstance(value, str) and value.startswith("godModeFallbacks."):
            fallback_key = value.split(".", 1)[1]
            require(fallback_key in fallback_map, f"GOD_MODE fallback ref {path} points to missing key {fallback_key}", failures)

    return failures


def build_report(data: Dict[str, Any], matrix_path: Path, seed: int, frame_count: int) -> Dict[str, Any]:
    tiers = tiers_by_name(data)
    expectations = data.get("selfAuditExpectations", {})
    report = {
        "tool": "Tools/VisualStressSim.py",
        "matrixPath": str(matrix_path.as_posix()),
        "evidenceBoundary": "PYTHON_OFFLINE_NOT_RUNTIME_PROOF",
        "seed": seed,
        "frameCount": frame_count,
        "tiers": {},
        "selfAudit": {
            "toasterMaxVramMbIncludingDriverReserve": expectations.get("toasterMaxVramMbIncludingDriverReserve"),
            "godModeMinimumDensityRatioVsPro": expectations.get("godModeMinimumDensityRatioVsPro"),
            "status": "PENDING"
        }
    }
    shape_failures = required_shape_failures(data, tiers)
    if shape_failures:
        report["selfAudit"]["failures"] = shape_failures
        report["selfAudit"]["status"] = "FAIL"
        return report

    for name in REQUIRED_TIERS:
        report["tiers"][name] = stress_tier(tiers[name], seed + len(name), frame_count)
    failures = validate_matrix(data, report)
    report["selfAudit"]["failures"] = failures
    report["selfAudit"]["status"] = "PASS" if not failures else "FAIL"
    return report


def print_summary(report: Dict[str, Any]) -> None:
    print("VISUAL STRESS SIMULATION")
    print(f"matrix={report['matrixPath']}")
    print(f"evidence={report['evidenceBoundary']}")
    for name in REQUIRED_TIERS:
        tier = report["tiers"].get(name)
        if tier is None:
            print(f"{name}: MISSING")
            continue
        print(
            f"{name}: vram={tier['estimatedVramMb']:.1f}MiB "
            f"density={tier['visualDensityScore']:.3f} "
            f"gpuCyclesP95={tier['gpuCyclesP95']:.3f} "
            f"gpuCyclesPeak={tier['gpuCyclesPeak']:.3f}"
        )
    density_ratio = report["selfAudit"].get("godModeDensityRatioVsPro")
    if density_ratio is not None:
        print(f"GOD_MODE/PRO density ratio={density_ratio:.3f}")
    print(f"STATUS={report['selfAudit']['status']}")
    if report["selfAudit"]["failures"]:
        for failure in report["selfAudit"]["failures"]:
            print(f"FAIL: {failure}")


def parse_args(argv: List[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Stress-estimate HECTON-8 visual scalability matrix.")
    parser.add_argument("--matrix", type=Path, default=DEFAULT_MATRIX_PATH, help="Path to Visual_Scalability_Matrix.json.")
    parser.add_argument("--frames", type=int, default=720, help="Offline stress frame count.")
    parser.add_argument("--seed", type=int, default=8808, help="Deterministic stress seed.")
    parser.add_argument("--write-report", action="store_true", help="Write JSON report to Docs/AgentLogs.")
    parser.add_argument("--report", type=Path, default=DEFAULT_REPORT_PATH, help="Report path when --write-report is set.")
    return parser.parse_args(argv)


def main(argv: List[str]) -> int:
    args = parse_args(argv)
    data = load_matrix(args.matrix)
    report = build_report(data, args.matrix, args.seed, args.frames)
    print_summary(report)

    if args.write_report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        with args.report.open("w", encoding="utf-8") as handle:
            json.dump(report, handle, indent=2)
            handle.write("\n")
        print(f"report={args.report.as_posix()}")

    return 0 if report["selfAudit"]["status"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
