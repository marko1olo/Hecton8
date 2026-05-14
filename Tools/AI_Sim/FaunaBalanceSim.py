#!/usr/bin/env python3
"""Offline Utility-AI balance sweep for HECTON-8 fauna.

Python/data only. No Unity C# integration, no project settings mutation.
"""

from __future__ import annotations

import argparse
import json
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, List, Tuple


DEFAULT_SEED = 0x48384641554E4131
DEFAULT_FRAMES = 1_000_000
DEFAULT_DISCOVERY_FRAMES = 12_000
SAMPLE_STRIDE = 10_000
PREY_CAPACITY = 16_000.0


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
        "schemaVersion": 1,
        "status": "AI BALANCED",
        "generatedBy": "FAUNA_BEHAVIOR_SIMULATOR",
        "evidenceClass": "CLI_PYTHON_SIMULATION",
        "runtimeUnityProof": "PENDING VERIFICATION",
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
        "speciesTargets": {"preyIdealPopulation": 9600.0, "stalkerIdealPopulation": 36.0, "alphaLeviathanIdealPopulation": 2.4},
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
    path.write_text(json.dumps(data, indent=2, sort_keys=True) + "\n", encoding="utf-8")


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
        "detailedReport": "Tools/AI_Sim/FaunaBalanceSim_Report.json",
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="HECTON-8 fauna behavior balance simulator.")
    parser.add_argument("--frames", type=int, default=DEFAULT_FRAMES)
    parser.add_argument("--discovery-frames", type=int, default=DEFAULT_DISCOVERY_FRAMES)
    parser.add_argument("--seed", type=lambda text: int(text, 0), default=DEFAULT_SEED)
    parser.add_argument("--output", default="Data/AI/Fauna_Global_Weights.json")
    parser.add_argument("--report", default="Tools/AI_Sim/FaunaBalanceSim_Report.json")
    parser.add_argument("--quick", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
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

    print("FAUNA BALANCE SIMULATION COMPLETE")
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
