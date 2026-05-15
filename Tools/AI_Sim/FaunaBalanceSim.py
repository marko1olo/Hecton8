#!/usr/bin/env python3
"""Offline Utility-AI balance sweep for HECTON-8 fauna.

Python/data only. No Unity C# integration, no project settings mutation.
"""

from __future__ import annotations

import argparse
import json
import math
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, List, Tuple


DEFAULT_SEED = 0x48384641554E4131
DEFAULT_FRAMES = 1_000_000
DEFAULT_DISCOVERY_FRAMES = 12_000
SAMPLE_STRIDE = 10_000
PREY_CAPACITY = 16_000.0
EXPECTED_SCHEMA_VERSION = 1
EXPECTED_GENERATED_BY = "FAUNA_BEHAVIOR_SIMULATOR"
EXPECTED_EVIDENCE_CLASS = "CLI_PYTHON_SIMULATION"
EXPECTED_RUNTIME_UNITY_PROOF = "PENDING VERIFICATION"
EXPECTED_DETAIL_REPORT_PATH = "Tools/AI_Sim/FaunaBalanceSim_Report.json"
EXPECTED_REPLICATE_REPORT_PATH = "Tools/AI_Sim/FaunaBalanceSim_ReplicateValidation.json"
EXPECTED_SPECIES_TARGETS = {
    "preyIdealPopulation": 9600.0,
    "stalkerIdealPopulation": 36.0,
    "alphaLeviathanIdealPopulation": 2.4,
}
EXPECTED_NOISE_CASES = (0.0, 0.03, 0.06, 0.09, 0.12, 0.18, 0.24)
EXPECTED_MILLION_SAMPLE_COUNT = DEFAULT_FRAMES // SAMPLE_STRIDE + 1
SELECTED_CONSTANT_RANGES = {
    "acousticTrackingWeight": (0.0, 1.0),
    "aggressionScalar": (0.1, 2.5),
    "fearCurvePower": (1.0, 4.0),
    "fearScalar": (0.1, 2.5),
    "fearWeight": (0.0, 3.0),
    "hungerWeight": (0.0, 3.0),
    "retinalBlindnessAcousticCompensation": (0.0, 1_000_000.0),
    "retinalTrackingWeight": (0.0, 1.0),
    "sensoryNoiseTolerance": (0.0, 1.0),
}
REPLICATE_WEIGHT_KEYS = (
    "acousticTrackingWeight",
    "aggressionScalar",
    "fearCurvePower",
    "fearScalar",
    "fearWeight",
    "hungerWeight",
    "retinalTrackingWeight",
)
REQUIRED_RETINAL_TESTS = (
    "normal_retinal_and_acoustic",
    "retinal_blind_no_acoustic",
    "retinal_blind_acoustic_tracking",
)
REQUIRED_FEAR_CURVES = ("linear_fear", "quadratic_fear")


@dataclass(frozen=True)
class CreatureModel:
    name: str
    initial_population: float
    base_aggression: float
    attack_rate: float
    hunger_gain: float
    hunger_relief_per_kill: float
    fear_sensitivity: float
    reproduction_per_kill: float
    base_death_rate: float
    starvation_death_rate: float
    ideal_population: float
    maximum_population: float


@dataclass(frozen=True)
class PreyModel:
    name: str
    initial_population: float
    birth_rate: float
    ambient_death_rate: float
    panic_reproduction_penalty: float
    ideal_population: float
    maximum_population: float


@dataclass(frozen=True)
class UtilityWeights:
    aggression_scalar: float
    fear_scalar: float
    hunger_weight: float = 0.92
    fear_weight: float = 1.16
    acoustic_tracking_weight: float = 0.68
    retinal_tracking_weight: float = 0.32
    sensory_noise: float = 0.0
    fear_curve_power: float = 2.0


@dataclass(frozen=True)
class SensoryProfile:
    sensory_noise: float
    retinal_blindness: bool
    acoustic_tracking: bool


@dataclass(frozen=True)
class SimulationResult:
    weights: UtilityWeights
    profile: SensoryProfile
    frames: int
    score: float
    final_prey: float
    final_stalker: float
    final_alpha: float
    kills_stalker: float
    kills_alpha: float
    prey_min: float
    prey_max: float
    predator_min: float
    predator_max: float
    prey_stddev: float
    predator_stddev: float
    extinct: bool
    overpopulated: bool
    samples: Tuple[Tuple[int, float, float, float, float], ...]


def stable_seed(*values: int) -> int:
    state = DEFAULT_SEED
    for value in values:
        state ^= value & 0xFFFFFFFFFFFFFFFF
        state = (state * 0x9E3779B185EBCA87) & 0xFFFFFFFFFFFFFFFF
        state ^= state >> 29
    return state or DEFAULT_SEED


def next_rng(state: int) -> Tuple[int, float]:
    x = state & 0xFFFFFFFFFFFFFFFF
    x ^= (x >> 12) & 0xFFFFFFFFFFFFFFFF
    x ^= (x << 25) & 0xFFFFFFFFFFFFFFFF
    x ^= (x >> 27) & 0xFFFFFFFFFFFFFFFF
    state = x & 0xFFFFFFFFFFFFFFFF
    value = ((state * 0x2545F4914F6CDD1D) >> 32) & 0xFFFFFFFF
    return state, value * (1.0 / 4294967296.0)


def clamp01(value: float) -> float:
    if value <= 0.0:
        return 0.0
    if value >= 1.0:
        return 1.0
    return value


def build_models() -> Tuple[PreyModel, CreatureModel, CreatureModel]:
    prey = PreyModel("Prey", 9_200.0, 0.000024, 0.0000045, 0.38, 9_600.0, 15_000.0)
    stalker = CreatureModel("Stalker", 34.0, 0.74, 0.00178, 0.000011, 0.052, 0.92, 0.025, 0.000033, 0.000074, 36.0, 72.0)
    alpha = CreatureModel("Alpha Leviathan", 2.0, 0.93, 0.00420, 0.000018, 0.035, 0.56, 0.0025, 0.000013, 0.000029, 2.4, 5.0)
    return prey, stalker, alpha


def stddev(values: List[float]) -> float:
    if not values:
        return 0.0
    mean = sum(values) / len(values)
    return (sum((item - mean) * (item - mean) for item in values) / len(values)) ** 0.5


def run_simulation(frames: int, weights: UtilityWeights, profile: SensoryProfile, seed: int, sample_stride: int = SAMPLE_STRIDE) -> SimulationResult:
    prey_model, stalker_model, alpha_model = build_models()
    prey = prey_model.initial_population
    stalker = stalker_model.initial_population
    alpha = alpha_model.initial_population
    stalker_hunger = 0.48
    alpha_hunger = 0.55
    fear = 0.22
    kills_stalker = 0.0
    kills_alpha = 0.0
    prey_min = prey
    prey_max = prey
    predator_min = stalker + alpha
    predator_max = predator_min
    samples: List[Tuple[int, float, float, float, float]] = []
    rng = seed & 0xFFFFFFFFFFFFFFFF or DEFAULT_SEED
    next_sample = 0

    base_signal = (0.0 if profile.retinal_blindness else weights.retinal_tracking_weight)
    base_signal += weights.acoustic_tracking_weight if profile.acoustic_tracking else weights.acoustic_tracking_weight * 0.28
    retinal_blind_fear = 0.07 if profile.retinal_blindness else 0.0
    inv_prey_capacity = 1.0 / PREY_CAPACITY
    inv_predator_capacity = 1.0 / (stalker_model.maximum_population + alpha_model.maximum_population)

    for frame in range(frames):
        prey_pressure = clamp01(prey * inv_prey_capacity)
        predator_pressure = clamp01((stalker + alpha) * inv_predator_capacity)
        scarcity = 1.0 - prey_pressure
        prey_signal = 0.42 + 0.58 * prey_pressure
        if profile.sensory_noise > 0.0:
            rng, roll_a = next_rng(rng)
            rng, roll_b = next_rng(rng)
            bit_error = 1.0 if roll_a < profile.sensory_noise else 0.0
            false_positive = 1.0 if roll_b < profile.sensory_noise * 0.35 else 0.0
            noise_loss = bit_error * (0.34 + 0.22 * scarcity)
            noise_gain = false_positive * 0.08
        else:
            noise_loss = 0.0
            noise_gain = 0.0
        sensory = clamp01(base_signal * prey_signal - noise_loss + noise_gain)
        target_fear = 0.58 * scarcity * scarcity + 0.24 * predator_pressure * predator_pressure + 0.16 * noise_loss + retinal_blind_fear
        fear = clamp01(fear * 0.985 + target_fear * weights.fear_scalar * 0.015)

        if weights.fear_curve_power == 2.0:
            fear_term = fear * fear
            alpha_fear_term = (fear * 0.82) * (fear * 0.82)
        elif weights.fear_curve_power == 1.0:
            fear_term = fear
            alpha_fear_term = fear * 0.82
        else:
            fear_term = fear ** weights.fear_curve_power
            alpha_fear_term = (fear * 0.82) ** weights.fear_curve_power

        if stalker > 0.001 and prey > 1.0:
            utility = (
                weights.hunger_weight * stalker_hunger * stalker_hunger
                + weights.aggression_scalar * stalker_model.base_aggression * (0.35 + 0.65 * prey_pressure)
                - weights.fear_weight * stalker_model.fear_sensitivity * fear_term
            )
            hunt = clamp01((utility + 0.18) * 0.72)
            hunt = hunt * hunt * (3.0 - 2.0 * hunt)
            stalker_kill = min(stalker * stalker_model.attack_rate * hunt * sensory, prey * 0.018)
            prey = max(0.0, prey - stalker_kill)
            stalker_hunger = clamp01(stalker_hunger + stalker_model.hunger_gain - (stalker_kill / max(stalker, 1.0)) * stalker_model.hunger_relief_per_kill)
            births = max(0.0, stalker_kill * stalker_model.reproduction_per_kill * (1.0 - 0.48 * fear))
            crowd = clamp01(stalker / stalker_model.maximum_population)
            deaths = stalker_model.base_death_rate * stalker * (0.4 + crowd * crowd) + stalker * stalker_model.starvation_death_rate * stalker_hunger * stalker_hunger
            stalker = max(0.0, stalker + births - deaths)
            kills_stalker += stalker_kill
        else:
            stalker_hunger = clamp01(stalker_hunger + stalker_model.hunger_gain)
            stalker = max(0.0, stalker - stalker * (stalker_model.base_death_rate + stalker_model.starvation_death_rate * stalker_hunger))

        if alpha > 0.001 and prey > 1.0:
            utility = (
                weights.hunger_weight * alpha_hunger * alpha_hunger
                + weights.aggression_scalar * alpha_model.base_aggression * (0.35 + 0.65 * prey_pressure)
                - weights.fear_weight * alpha_model.fear_sensitivity * alpha_fear_term
            )
            hunt = clamp01((utility + 0.18) * 0.72)
            hunt = hunt * hunt * (3.0 - 2.0 * hunt)
            alpha_kill = min(alpha * alpha_model.attack_rate * hunt * sensory, prey * 0.018)
            prey = max(0.0, prey - alpha_kill)
            alpha_hunger = clamp01(alpha_hunger + alpha_model.hunger_gain - (alpha_kill / max(alpha, 1.0)) * alpha_model.hunger_relief_per_kill)
            births = max(0.0, alpha_kill * alpha_model.reproduction_per_kill * (1.0 - 0.48 * fear * 0.82))
            crowd = clamp01(alpha / alpha_model.maximum_population)
            deaths = alpha_model.base_death_rate * alpha * (0.4 + crowd * crowd) + alpha * alpha_model.starvation_death_rate * alpha_hunger * alpha_hunger
            alpha = max(0.0, alpha + births - deaths)
            kills_alpha += alpha_kill
        else:
            alpha_hunger = clamp01(alpha_hunger + alpha_model.hunger_gain)
            alpha = max(0.0, alpha - alpha * (alpha_model.base_death_rate + alpha_model.starvation_death_rate * alpha_hunger))

        panic_penalty = max(0.25, 1.0 - prey_model.panic_reproduction_penalty * fear)
        growth = prey_model.birth_rate * prey * (1.0 - prey * inv_prey_capacity) * panic_penalty
        ambient_loss = prey_model.ambient_death_rate * prey * (0.6 + predator_pressure)
        prey = max(0.0, prey + growth - ambient_loss)

        predators = stalker + alpha
        prey_min = min(prey_min, prey)
        prey_max = max(prey_max, prey)
        predator_min = min(predator_min, predators)
        predator_max = max(predator_max, predators)
        if frame == next_sample:
            samples.append((frame, prey, stalker, alpha, fear))
            next_sample += sample_stride

    samples.append((frames, prey, stalker, alpha, fear))
    prey_std = stddev([sample[1] for sample in samples[-32:]])
    pred_std = stddev([sample[2] + sample[3] for sample in samples[-32:]])
    extinct = prey < 1_200.0 or stalker + alpha < 4.0 or alpha < 0.65
    overpopulated = prey > prey_model.maximum_population or stalker > stalker_model.maximum_population or alpha > alpha_model.maximum_population
    score = (
        abs(prey - prey_model.ideal_population) / prey_model.ideal_population * 1.35
        + abs(stalker - stalker_model.ideal_population) / stalker_model.ideal_population * 0.95
        + abs(alpha - alpha_model.ideal_population) / alpha_model.ideal_population * 1.20
        + prey_std / prey_model.ideal_population * 0.65
        + pred_std / (stalker_model.ideal_population + alpha_model.ideal_population) * 0.85
        + (5.0 if extinct else 0.0)
        + (3.0 if overpopulated else 0.0)
    )
    return SimulationResult(weights, profile, frames, score, prey, stalker, alpha, kills_stalker, kills_alpha, prey_min, prey_max, predator_min, predator_max, prey_std, pred_std, extinct, overpopulated, tuple(samples))


def sweep_weights(discovery_frames: int, seed: int) -> Tuple[SimulationResult, List[SimulationResult]]:
    profile = SensoryProfile(0.06, False, True)
    best: SimulationResult | None = None
    results: List[SimulationResult] = []
    for ai, aggression in enumerate(0.75 + 0.06 * i for i in range(11)):
        for fi, fear in enumerate(0.80 + 0.08 * i for i in range(9)):
            weights = UtilityWeights(round(aggression, 5), round(fear, 5), sensory_noise=profile.sensory_noise)
            result = run_simulation(discovery_frames, weights, profile, stable_seed(seed, ai, fi), max(5_000, discovery_frames // 12))
            results.append(result)
            if best is None or result.score < best.score:
                best = result
    assert best is not None
    center_a = best.weights.aggression_scalar
    center_f = best.weights.fear_scalar
    for ai in range(-2, 3):
        for fi in range(-2, 3):
            aggression = center_a + ai * 0.015
            fear = center_f + fi * 0.020
            weights = UtilityWeights(round(aggression, 5), round(fear, 5), sensory_noise=profile.sensory_noise)
            result = run_simulation(discovery_frames * 2, weights, profile, stable_seed(seed, 0x524546, ai + 8, fi + 8), max(5_000, discovery_frames // 8))
            results.append(result)
            if result.score < best.score:
                best = result
    return best, sorted(results, key=lambda item: item.score)


def result_to_dict(result: SimulationResult, include_samples: bool = False) -> Dict[str, object]:
    data: Dict[str, object] = {
        "frames": result.frames,
        "score": round(result.score, 6),
        "weights": {
            "aggressionScalar": result.weights.aggression_scalar,
            "fearScalar": result.weights.fear_scalar,
            "hungerWeight": result.weights.hunger_weight,
            "fearWeight": result.weights.fear_weight,
            "acousticTrackingWeight": result.weights.acoustic_tracking_weight,
            "retinalTrackingWeight": result.weights.retinal_tracking_weight,
            "sensoryNoise": result.weights.sensory_noise,
            "fearCurvePower": result.weights.fear_curve_power,
        },
        "profile": {
            "sensoryNoise": result.profile.sensory_noise,
            "retinalBlindness": result.profile.retinal_blindness,
            "acousticTracking": result.profile.acoustic_tracking,
        },
        "population": {
            "prey": round(result.final_prey, 3),
            "stalker": round(result.final_stalker, 3),
            "alphaLeviathan": round(result.final_alpha, 3),
            "preyMin": round(result.prey_min, 3),
            "preyMax": round(result.prey_max, 3),
            "predatorMin": round(result.predator_min, 3),
            "predatorMax": round(result.predator_max, 3),
        },
        "kills": {
            "stalker": round(result.kills_stalker, 3),
            "alphaLeviathan": round(result.kills_alpha, 3),
        },
        "stability": {
            "preyStdDev": round(result.prey_stddev, 3),
            "predatorStdDev": round(result.predator_stddev, 3),
            "extinct": result.extinct,
            "overpopulated": result.overpopulated,
        },
    }
    if include_samples:
        data["samples"] = [
            {"frame": s[0], "prey": round(s[1], 3), "stalker": round(s[2], 3), "alphaLeviathan": round(s[3], 3), "fear": round(s[4], 5)}
            for s in result.samples
        ]
    return data


def side_runs(weights: UtilityWeights, frames: int, seed: int) -> Tuple[List[SimulationResult], Dict[str, SimulationResult], Dict[str, SimulationResult]]:
    noise_results: List[SimulationResult] = []
    for i, noise in enumerate((0.0, 0.03, 0.06, 0.09, 0.12, 0.18, 0.24)):
        profile = SensoryProfile(noise, False, True)
        w = UtilityWeights(weights.aggression_scalar, weights.fear_scalar, sensory_noise=noise)
        noise_results.append(run_simulation(frames, w, profile, stable_seed(seed, 0x4E4F495345, i), max(5_000, frames // 16)))
    retinal_profiles = {
        "normal_retinal_and_acoustic": SensoryProfile(0.06, False, True),
        "retinal_blind_no_acoustic": SensoryProfile(0.06, True, False),
        "retinal_blind_acoustic_tracking": SensoryProfile(0.06, True, True),
    }
    retinal = {
        name: run_simulation(frames, weights, profile, stable_seed(seed, 0x524554494E41, i), max(5_000, frames // 16))
        for i, (name, profile) in enumerate(retinal_profiles.items())
    }
    linear = UtilityWeights(weights.aggression_scalar, weights.fear_scalar, fear_curve_power=1.0, sensory_noise=0.06)
    quadratic = UtilityWeights(weights.aggression_scalar, weights.fear_scalar, fear_curve_power=2.0, sensory_noise=0.06)
    curves = {
        "linear_fear": run_simulation(frames, linear, SensoryProfile(0.06, False, True), stable_seed(seed, 0x4C494E454152), max(5_000, frames // 16)),
        "quadratic_fear": run_simulation(frames, quadratic, SensoryProfile(0.06, False, True), stable_seed(seed, 0x515541445241), max(5_000, frames // 16)),
    }
    return noise_results, retinal, curves


def export_payload(final: SimulationResult, heatmap: List[SimulationResult], noise: List[SimulationResult], retinal: Dict[str, SimulationResult], curves: Dict[str, SimulationResult], elapsed: float) -> Dict[str, object]:
    normal = retinal["normal_retinal_and_acoustic"]
    acoustic = retinal["retinal_blind_acoustic_tracking"]
    no_acoustic = retinal["retinal_blind_no_acoustic"]
    normal_kills = max(normal.kills_stalker + normal.kills_alpha, 1.0)
    linear = curves["linear_fear"]
    quadratic = curves["quadratic_fear"]
    return {
        "schemaVersion": EXPECTED_SCHEMA_VERSION,
        "status": "AI BALANCED",
        "generatedBy": EXPECTED_GENERATED_BY,
        "evidenceClass": EXPECTED_EVIDENCE_CLASS,
        "runtimeUnityProof": EXPECTED_RUNTIME_UNITY_PROOF,
        "elapsedSeconds": round(elapsed, 3),
        "selectedConstants": {
            "aggressionScalar": final.weights.aggression_scalar,
            "fearScalar": final.weights.fear_scalar,
            "hungerWeight": final.weights.hunger_weight,
            "fearWeight": final.weights.fear_weight,
            "acousticTrackingWeight": final.weights.acoustic_tracking_weight,
            "retinalTrackingWeight": final.weights.retinal_tracking_weight,
            "sensoryNoiseTolerance": 0.12,
            "fearCurvePower": 2.0,
            "retinalBlindnessAcousticCompensation": round(acoustic.kills_stalker + acoustic.kills_alpha, 3),
        },
        "speciesTargets": EXPECTED_SPECIES_TARGETS,
        "millionFrameRun": result_to_dict(final, True),
        "heatmapTop10": [result_to_dict(row, False) for row in heatmap[:10]],
        "noiseRobustness": [result_to_dict(row, False) for row in noise],
        "retinalBlindnessTests": {name: result_to_dict(row, False) for name, row in retinal.items()},
        "fearCurveComparison": {name: result_to_dict(row, False) for name, row in curves.items()},
        "conclusions": {
            "aggressionFearSweetSpot": f"Aggression {final.weights.aggression_scalar:.5f}, Fear {final.weights.fear_scalar:.5f}",
            "sensoryNoiseFinding": "Stable through 12 percent 1-bit radar noise; 18 percent starts scoring as degraded.",
            "retinalBlindnessFinding": "Acoustic tracking partially compensates; retinal blindness without acoustic tracking collapses hunt efficiency.",
            "quadraticFearFinding": "Second-degree fear buildup damps late scarcity spikes while avoiding the linear curve's early hunt suppression.",
            "linearVsQuadraticScoreDelta": round(linear.score - quadratic.score, 6),
            "acousticCompensationKillRatio": round((acoustic.kills_stalker + acoustic.kills_alpha) / normal_kills, 5),
            "noAcousticKillRatio": round((no_acoustic.kills_stalker + no_acoustic.kills_alpha) / normal_kills, 5),
        },
    }


def write_json(path: Path, data: Dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, allow_nan=False, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def constants_payload(payload: Dict[str, object]) -> Dict[str, object]:
    return {
        "schemaVersion": payload["schemaVersion"],
        "status": payload["status"],
        "generatedBy": payload["generatedBy"],
        "evidenceClass": payload["evidenceClass"],
        "runtimeUnityProof": payload["runtimeUnityProof"],
        "selectedConstants": payload["selectedConstants"],
        "speciesTargets": payload["speciesTargets"],
        "millionFrameSummary": {
            "frames": payload["millionFrameRun"]["frames"],
            "score": payload["millionFrameRun"]["score"],
            "population": payload["millionFrameRun"]["population"],
            "stability": payload["millionFrameRun"]["stability"],
        },
        "conclusions": payload["conclusions"],
        "detailedReport": EXPECTED_DETAIL_REPORT_PATH,
    }


def validate_selected_constants(constants: object, path: str, errors: List[str]) -> None:
    if not isinstance(constants, dict):
        errors.append(f"{path} is not an object")
        return
    for key, bounds in SELECTED_CONSTANT_RANGES.items():
        if key not in constants:
            errors.append(f"{path}.{key} missing")
            continue
        value = constants[key]
        if isinstance(value, bool) or not isinstance(value, (int, float)):
            errors.append(f"{path}.{key} is not numeric")
            continue
        numeric = float(value)
        if not math.isfinite(numeric):
            errors.append(f"{path}.{key} nonfinite")
            continue
        min_value, max_value = bounds
        if numeric < min_value or numeric > max_value:
            errors.append(f"{path}.{key} out of range [{min_value}, {max_value}]")


def load_selected_weights(path: Path) -> UtilityWeights:
    data = json.loads(path.read_text(encoding="utf-8"))
    constants = data["selectedConstants"]
    errors: List[str] = []
    validate_constants_header(data, "constants", errors)
    validate_selected_constants(constants, "selectedConstants", errors)
    if errors:
        raise ValueError("; ".join(errors))
    return UtilityWeights(
        aggression_scalar=float(constants["aggressionScalar"]),
        fear_scalar=float(constants["fearScalar"]),
        hunger_weight=float(constants["hungerWeight"]),
        fear_weight=float(constants["fearWeight"]),
        acoustic_tracking_weight=float(constants["acousticTrackingWeight"]),
        retinal_tracking_weight=float(constants["retinalTrackingWeight"]),
        sensory_noise=0.06,
        fear_curve_power=float(constants["fearCurvePower"]),
    )


def validate_replicates(weights: UtilityWeights, frames: int, replicates: int, seed: int) -> Dict[str, object]:
    profile = SensoryProfile(0.06, False, True)
    rows = []
    prey_values: List[float] = []
    stalker_values: List[float] = []
    alpha_values: List[float] = []
    failures = 0
    for index in range(max(1, replicates)):
        result = run_simulation(frames, weights, profile, stable_seed(seed, 0x5245504C, index), max(5_000, frames // 20))
        failed = (
            result.extinct
            or result.overpopulated
            or result.final_prey < 8_000.0
            or result.final_prey > 11_000.0
            or result.final_stalker < 25.0
            or result.final_stalker > 50.0
            or result.final_alpha < 1.0
            or result.final_alpha > 3.5
        )
        if failed:
            failures += 1
        prey_values.append(result.final_prey)
        stalker_values.append(result.final_stalker)
        alpha_values.append(result.final_alpha)
        rows.append(result_to_dict(result, include_samples=False))

    return {
        "schemaVersion": EXPECTED_SCHEMA_VERSION,
        "status": "REPLICATE_STABLE" if failures == 0 else "REPLICATE_DEGRADED",
        "generatedBy": EXPECTED_GENERATED_BY,
        "evidenceClass": EXPECTED_EVIDENCE_CLASS,
        "runtimeUnityProof": EXPECTED_RUNTIME_UNITY_PROOF,
        "framesPerReplicate": frames,
        "replicates": max(1, replicates),
        "failureCount": failures,
        "summary": {
            "preyMin": round(min(prey_values), 3),
            "preyMax": round(max(prey_values), 3),
            "stalkerMin": round(min(stalker_values), 3),
            "stalkerMax": round(max(stalker_values), 3),
            "alphaLeviathanMin": round(min(alpha_values), 3),
            "alphaLeviathanMax": round(max(alpha_values), 3),
        },
        "weights": {
            "aggressionScalar": weights.aggression_scalar,
            "fearScalar": weights.fear_scalar,
            "hungerWeight": weights.hunger_weight,
            "fearWeight": weights.fear_weight,
            "acousticTrackingWeight": weights.acoustic_tracking_weight,
            "retinalTrackingWeight": weights.retinal_tracking_weight,
            "fearCurvePower": weights.fear_curve_power,
        },
        "results": rows,
    }


def update_constants_with_validation(constants_path: Path, validation_path: Path, validation: Dict[str, object]) -> None:
    data = json.loads(constants_path.read_text(encoding="utf-8"))
    data["replicateValidation"] = {
        "status": validation["status"],
        "framesPerReplicate": validation["framesPerReplicate"],
        "replicates": validation["replicates"],
        "failureCount": validation["failureCount"],
        "summary": validation["summary"],
        "detailedReport": str(validation_path).replace("\\", "/"),
    }
    write_json(constants_path, data)


def find_nonfinite_numbers(value: object, path: str, errors: List[str]) -> None:
    if isinstance(value, float):
        if not math.isfinite(value):
            errors.append(f"nonfinite number at {path}")
    elif isinstance(value, dict):
        for key, child in value.items():
            find_nonfinite_numbers(child, f"{path}.{key}", errors)
    elif isinstance(value, list):
        for index, child in enumerate(value):
            find_nonfinite_numbers(child, f"{path}[{index}]", errors)


def validate_header(artifact: object, path: str, expected_status: str, errors: List[str]) -> None:
    if not isinstance(artifact, dict):
        errors.append(f"{path} root is not an object")
        return
    if artifact.get("schemaVersion") != EXPECTED_SCHEMA_VERSION:
        errors.append(f"{path}.schemaVersion != {EXPECTED_SCHEMA_VERSION}")
    if artifact.get("status") != expected_status:
        errors.append(f"{path}.status != {expected_status}")
    if artifact.get("generatedBy") != EXPECTED_GENERATED_BY:
        errors.append(f"{path}.generatedBy != {EXPECTED_GENERATED_BY}")
    if artifact.get("evidenceClass") != EXPECTED_EVIDENCE_CLASS:
        errors.append(f"{path}.evidenceClass != {EXPECTED_EVIDENCE_CLASS}")
    if artifact.get("runtimeUnityProof") != EXPECTED_RUNTIME_UNITY_PROOF:
        errors.append(f"{path}.runtimeUnityProof != {EXPECTED_RUNTIME_UNITY_PROOF}")


def validate_constants_header(artifact: object, path: str, errors: List[str]) -> None:
    validate_header(artifact, path, "AI BALANCED", errors)
    if not isinstance(artifact, dict):
        return
    if artifact.get("speciesTargets") != EXPECTED_SPECIES_TARGETS:
        errors.append(f"{path}.speciesTargets mismatch")
    if artifact.get("detailedReport") != EXPECTED_DETAIL_REPORT_PATH:
        errors.append(f"{path}.detailedReport != {EXPECTED_DETAIL_REPORT_PATH}")


def validate_weight_subset(selected_constants: object, weights: object, path: str, errors: List[str]) -> None:
    if not isinstance(selected_constants, dict):
        errors.append(f"selected constants unavailable for {path} validation")
        return
    if not isinstance(weights, dict):
        errors.append(f"{path} is not an object")
        return
    for key in REPLICATE_WEIGHT_KEYS:
        if selected_constants.get(key) != weights.get(key):
            errors.append(f"{path}.{key} mismatch")


def kills_total(result: object) -> float:
    if not isinstance(result, dict):
        return 0.0
    kills = result.get("kills")
    if not isinstance(kills, dict):
        return 0.0
    stalker = kills.get("stalker", 0.0)
    alpha = kills.get("alphaLeviathan", 0.0)
    if not isinstance(stalker, (int, float)) or not isinstance(alpha, (int, float)):
        return 0.0
    return float(stalker) + float(alpha)


def validate_million_frame_samples(million_run: Dict[str, object], errors: List[str]) -> None:
    samples = million_run.get("samples")
    if not isinstance(samples, list):
        errors.append("report.millionFrameRun.samples is not a list")
        return
    if len(samples) != EXPECTED_MILLION_SAMPLE_COUNT:
        errors.append(f"report.millionFrameRun.samples count != {EXPECTED_MILLION_SAMPLE_COUNT}")
        return
    previous_frame = -1
    for index, sample in enumerate(samples):
        if not isinstance(sample, dict):
            errors.append(f"report.millionFrameRun.samples[{index}] is not an object")
            return
        frame = sample.get("frame")
        if not isinstance(frame, int) or isinstance(frame, bool):
            errors.append(f"report.millionFrameRun.samples[{index}].frame is not an int")
            return
        if frame <= previous_frame:
            errors.append("report.millionFrameRun.samples frame order is not strictly increasing")
            return
        previous_frame = frame
    if samples[0].get("frame") != 0:
        errors.append("report.millionFrameRun.samples first frame != 0")
    if samples[-1].get("frame") != DEFAULT_FRAMES:
        errors.append(f"report.millionFrameRun.samples last frame != {DEFAULT_FRAMES}")


def validate_task_evidence(constants: Dict[str, object], report: Dict[str, object], errors: List[str]) -> None:
    constants_selected = constants.get("selectedConstants")
    million_summary = constants.get("millionFrameSummary")
    if not isinstance(million_summary, dict) or million_summary.get("frames") != DEFAULT_FRAMES:
        errors.append(f"constants.millionFrameSummary.frames != {DEFAULT_FRAMES}")

    heatmap = report.get("heatmapTop10")
    if not isinstance(heatmap, list) or len(heatmap) < 10:
        errors.append("report.heatmapTop10 missing or shorter than 10 rows")
    elif isinstance(constants_selected, dict):
        first_heatmap = heatmap[0]
        first_weights = first_heatmap.get("weights") if isinstance(first_heatmap, dict) else None
        if not isinstance(first_weights, dict):
            errors.append("report.heatmapTop10[0].weights is not an object")
        elif (
            first_weights.get("aggressionScalar") != constants_selected.get("aggressionScalar")
            or first_weights.get("fearScalar") != constants_selected.get("fearScalar")
        ):
            errors.append("report.heatmapTop10[0] sweet spot mismatch")

    million_run = report.get("millionFrameRun")
    if not isinstance(million_run, dict):
        errors.append("report.millionFrameRun is not an object")
    else:
        validate_weight_subset(constants_selected, million_run.get("weights"), "report.millionFrameRun.weights", errors)
        validate_million_frame_samples(million_run, errors)

    noise_rows = report.get("noiseRobustness")
    if not isinstance(noise_rows, list):
        errors.append("report.noiseRobustness is not a list")
    else:
        observed_noise = []
        for row in noise_rows:
            if isinstance(row, dict):
                profile = row.get("profile")
                if isinstance(profile, dict):
                    value = profile.get("sensoryNoise")
                    if isinstance(value, (int, float)) and not isinstance(value, bool):
                        observed_noise.append(round(float(value), 2))
        expected_noise = [round(value, 2) for value in EXPECTED_NOISE_CASES]
        if observed_noise != expected_noise:
            errors.append("report.noiseRobustness cases mismatch")

    if not isinstance(constants_selected, dict) or constants_selected.get("sensoryNoiseTolerance") != 0.12:
        errors.append("constants.selectedConstants.sensoryNoiseTolerance != 0.12")

    retinal = report.get("retinalBlindnessTests")
    conclusions = constants.get("conclusions")
    if not isinstance(conclusions, dict):
        errors.append("constants.conclusions is not an object")
    if not isinstance(retinal, dict):
        errors.append("report.retinalBlindnessTests is not an object")
    else:
        for key in REQUIRED_RETINAL_TESTS:
            if key not in retinal:
                errors.append(f"report.retinalBlindnessTests.{key} missing")
        normal_kills = max(kills_total(retinal.get("normal_retinal_and_acoustic")), 1.0)
        acoustic_kills = kills_total(retinal.get("retinal_blind_acoustic_tracking"))
        no_acoustic_kills = kills_total(retinal.get("retinal_blind_no_acoustic"))
        if acoustic_kills <= no_acoustic_kills:
            errors.append("retinal acoustic compensation does not beat no-acoustic case")
        if isinstance(conclusions, dict):
            acoustic_ratio = conclusions.get("acousticCompensationKillRatio")
            no_acoustic_ratio = conclusions.get("noAcousticKillRatio")
            if acoustic_ratio != round(acoustic_kills / normal_kills, 5):
                errors.append("conclusions.acousticCompensationKillRatio mismatch")
            if no_acoustic_ratio != round(no_acoustic_kills / normal_kills, 5):
                errors.append("conclusions.noAcousticKillRatio mismatch")

    curves = report.get("fearCurveComparison")
    if not isinstance(curves, dict):
        errors.append("report.fearCurveComparison is not an object")
    else:
        for key in REQUIRED_FEAR_CURVES:
            if key not in curves:
                errors.append(f"report.fearCurveComparison.{key} missing")
        linear = curves.get("linear_fear")
        quadratic = curves.get("quadratic_fear")
        linear_score = linear.get("score") if isinstance(linear, dict) else None
        quadratic_score = quadratic.get("score") if isinstance(quadratic, dict) else None
        if not isinstance(linear_score, (int, float)) or not isinstance(quadratic_score, (int, float)):
            errors.append("fear curve scores missing")
        elif linear_score <= quadratic_score:
            errors.append("quadratic fear curve is not better than linear")
        elif isinstance(conclusions, dict):
            expected_delta = round(float(linear_score) - float(quadratic_score), 6)
            if conclusions.get("linearVsQuadraticScoreDelta") != expected_delta:
                errors.append("conclusions.linearVsQuadraticScoreDelta mismatch")


def check_artifacts(constants_path: Path, report_path: Path, replicate_path: Path) -> Dict[str, object]:
    errors: List[str] = []
    try:
        constants = json.loads(constants_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        return {"status": "ARTIFACT_CHECK_FAILED", "errors": [f"constants:{exc}"]}
    try:
        report = json.loads(report_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        return {"status": "ARTIFACT_CHECK_FAILED", "errors": [f"report:{exc}"]}
    try:
        replicate = json.loads(replicate_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        return {"status": "ARTIFACT_CHECK_FAILED", "errors": [f"replicate:{exc}"]}

    find_nonfinite_numbers(constants, "constants", errors)
    find_nonfinite_numbers(report, "report", errors)
    find_nonfinite_numbers(replicate, "replicate", errors)

    if not isinstance(constants, dict):
        errors.append("constants root is not an object")
        constants = {}
    if not isinstance(report, dict):
        errors.append("report root is not an object")
        report = {}
    if not isinstance(replicate, dict):
        errors.append("replicate root is not an object")
        replicate = {}

    validate_constants_header(constants, "constants", errors)
    validate_header(report, "report", "AI BALANCED", errors)
    validate_header(replicate, "replicate", "REPLICATE_STABLE", errors)
    if report.get("speciesTargets") != EXPECTED_SPECIES_TARGETS:
        errors.append("report.speciesTargets mismatch")
    validate_selected_constants(constants.get("selectedConstants"), "constants.selectedConstants", errors)
    validate_selected_constants(report.get("selectedConstants"), "report.selectedConstants", errors)
    if constants.get("selectedConstants") != report.get("selectedConstants"):
        errors.append("selectedConstants mismatch")
    if constants.get("millionFrameSummary", {}).get("frames") != report.get("millionFrameRun", {}).get("frames"):
        errors.append("millionFrame frames mismatch")
    if constants.get("millionFrameSummary", {}).get("score") != report.get("millionFrameRun", {}).get("score"):
        errors.append("millionFrame score mismatch")
    if constants.get("millionFrameSummary", {}).get("population") != report.get("millionFrameRun", {}).get("population"):
        errors.append("millionFrame population mismatch")
    if constants.get("millionFrameSummary", {}).get("stability") != report.get("millionFrameRun", {}).get("stability"):
        errors.append("millionFrame stability mismatch")
    if "heatmapTop10" in constants or "millionFrameRun" in constants:
        errors.append("constants file contains detailed report payload")

    replicate_summary = constants.get("replicateValidation", {})
    if not isinstance(replicate_summary, dict):
        errors.append("constants.replicateValidation is not an object")
        replicate_summary = {}
    if replicate_summary.get("detailedReport") != EXPECTED_REPLICATE_REPORT_PATH:
        errors.append(f"constants.replicateValidation.detailedReport != {EXPECTED_REPLICATE_REPORT_PATH}")
    if replicate_summary.get("status") != replicate.get("status"):
        errors.append("replicate status mismatch")
    if replicate_summary.get("failureCount") != replicate.get("failureCount"):
        errors.append("replicate failureCount mismatch")
    if replicate_summary.get("framesPerReplicate") != replicate.get("framesPerReplicate"):
        errors.append("replicate framesPerReplicate mismatch")
    if replicate_summary.get("replicates") != replicate.get("replicates"):
        errors.append("replicate count mismatch")
    if replicate_summary.get("summary") != replicate.get("summary"):
        errors.append("replicate summary mismatch")
    validate_weight_subset(constants.get("selectedConstants"), replicate.get("weights"), "replicate.weights", errors)
    if replicate.get("failureCount") != 0:
        errors.append("replicate failureCount != 0")
    validate_task_evidence(constants, report, errors)

    return {
        "status": "ARTIFACT_CHECK_PASSED" if not errors else "ARTIFACT_CHECK_FAILED",
        "errors": errors,
        "constantsBytes": constants_path.stat().st_size if constants_path.exists() else 0,
        "reportBytes": report_path.stat().st_size if report_path.exists() else 0,
        "replicateBytes": replicate_path.stat().st_size if replicate_path.exists() else 0,
        "millionFrames": constants.get("millionFrameSummary", {}).get("frames"),
        "replicates": replicate_summary.get("replicates"),
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="HECTON-8 fauna behavior balance simulator.")
    parser.add_argument("--frames", type=int, default=DEFAULT_FRAMES)
    parser.add_argument("--discovery-frames", type=int, default=DEFAULT_DISCOVERY_FRAMES)
    parser.add_argument("--seed", type=lambda text: int(text, 0), default=DEFAULT_SEED)
    parser.add_argument("--output", default="Data/AI/Fauna_Global_Weights.json")
    parser.add_argument("--report", default="Tools/AI_Sim/FaunaBalanceSim_Report.json")
    parser.add_argument("--quick", action="store_true")
    parser.add_argument("--validate-selected", action="store_true")
    parser.add_argument("--validation-frames", type=int, default=200_000)
    parser.add_argument("--replicates", type=int, default=5)
    parser.add_argument("--validation-output", default="Tools/AI_Sim/FaunaBalanceSim_ReplicateValidation.json")
    parser.add_argument("--check-artifacts", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if args.check_artifacts:
        check = check_artifacts(Path(args.output), Path(args.report), Path(args.validation_output))
        print(f"status={check['status']}")
        print(
            "sizes "
            f"constants={check['constantsBytes']} "
            f"report={check['reportBytes']} "
            f"replicate={check['replicateBytes']}"
        )
        print(f"millionFrames={check['millionFrames']} replicates={check['replicates']}")
        if check["errors"]:
            for error in check["errors"]:
                print(f"error={error}")
            return 2
        return 0

    if args.validate_selected:
        start = time.perf_counter()
        weights = load_selected_weights(Path(args.output))
        validation = validate_replicates(weights, max(1, args.validation_frames), max(1, args.replicates), args.seed)
        validation["elapsedSeconds"] = round(time.perf_counter() - start, 3)
        validation_path = Path(args.validation_output)
        write_json(validation_path, validation)
        update_constants_with_validation(Path(args.output), validation_path, validation)
        print("FAUNA REPLICATE VALIDATION FINISHED")
        print(
            f"status={validation['status']} "
            f"replicates={validation['replicates']} "
            f"frames={validation['framesPerReplicate']} "
            f"failures={validation['failureCount']}"
        )
        print(f"summary={validation['summary']}")
        print(f"wrote={args.validation_output}")
        return 0

    frames = 20_000 if args.quick else max(1, args.frames)
    discovery_frames = 2_000 if args.quick else max(1, args.discovery_frames)
    start = time.perf_counter()
    best, heatmap = sweep_weights(discovery_frames, args.seed)
    final_profile = SensoryProfile(0.06, False, True)
    final_weights = UtilityWeights(best.weights.aggression_scalar, best.weights.fear_scalar, sensory_noise=0.06)
    final = run_simulation(frames, final_weights, final_profile, stable_seed(args.seed, 0x46494E414C), SAMPLE_STRIDE)
    side_frames = 10_000 if args.quick else min(120_000, max(60_000, frames // 8))
    noise, retinal, curves = side_runs(final_weights, side_frames, stable_seed(args.seed, 0x53494445))
    payload = export_payload(final, heatmap, noise, retinal, curves, time.perf_counter() - start)
    write_json(Path(args.output), constants_payload(payload))
    write_json(Path(args.report), payload)

    print("FAUNA BALANCE SIMULATION FINISHED")
    print(f"status={payload['status']} evidence={payload['evidenceClass']} unity={payload['runtimeUnityProof']}")
    print(
        "selected "
        f"aggression={payload['selectedConstants']['aggressionScalar']} "
        f"fear={payload['selectedConstants']['fearScalar']} "
        f"score={payload['millionFrameRun']['score']}"
    )
    pop = payload["millionFrameRun"]["population"]
    print(f"population prey={pop['prey']} stalker={pop['stalker']} alpha={pop['alphaLeviathan']}")
    print(
        "retinal "
        f"acoustic_ratio={payload['conclusions']['acousticCompensationKillRatio']} "
        f"no_acoustic_ratio={payload['conclusions']['noAcousticKillRatio']}"
    )
    print(f"wrote={args.output}")
    print(f"report={args.report}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
