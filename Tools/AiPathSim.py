#!/usr/bin/env python3
"""Deterministic potential-field steering simulator for HECTON-8 fauna.

The simulator is tooling only. Runtime implementation should port the selected
weights into Burst/NativeArray jobs and consume flow/SDF snapshots through
existing domain contracts.
"""

from __future__ import annotations

import json
import math
import sys
import time
from dataclasses import dataclass
from itertools import product
from pathlib import Path
from typing import Iterable, Sequence, Tuple


EPSILON = 1.0e-6
DT = 0.1
MAX_STEPS = 360
TARGET_RADIUS = 3.0
MAX_SPEED = 13.0
MAX_ACCEL = 30.0
START = (-82.0, -340.0, -64.0)
TARGET = (78.0, -340.0, 68.0)
HURRICANE_CENTER = (0.0, -340.0, 0.0)

SOURCE_PARAMETER_SNAPSHOT = {
    "flowTextureResolution": 32,
    "flowTextureWorldSizeMeters": 100.0,
    "flowTextureCellSizeMeters": 3.125,
    "vectorNoiseResolution": 32,
    "surfaceStormLayerDepthMeters": 50.0,
    "stormSurfaceTurbulenceStrength": 0.4,
    "abyssalThermoclineDepthMeters": 120.0,
    "heatSourceCapacity": 8,
    "sourceFiles": [
        "Assets/_Project/Scripts/HectonFluidEngine.cs",
        "Docs/ARCHITECTURE/FLOW_FIELD_MATH.md",
        ".agents-skills/CORE_Weather_Abyssal_FlowField_Currents.txt",
    ],
}


Vec3 = Tuple[float, float, float]


@dataclass(frozen=True)
class SphereObstacle:
    center: Vec3
    radius: float


@dataclass(frozen=True)
class BoxBounds:
    min_x: float
    max_x: float
    min_y: float
    max_y: float
    min_z: float
    max_z: float


@dataclass(frozen=True)
class SteeringWeights:
    attraction: float
    flow_boost: float
    flow_resistance: float
    obstacle_repulsion: float
    smoothing_alpha: float
    idle_flow_coupling: float
    wall_influence: float
    max_repulsion: float


@dataclass
class SimResult:
    reached: bool
    steps: int
    final_distance: float
    path_length: float
    min_obstacle_distance: float
    jitter_events: int
    sdf_pushout_events: int
    mean_alignment: float
    mean_flow_use: float
    score: float


OBSTACLES = (
    SphereObstacle((-30.0, -340.0, -18.0), 18.0),
    SphereObstacle((12.0, -340.0, 8.0), 24.0),
    SphereObstacle((45.0, -340.0, 35.0), 14.0),
    SphereObstacle((-6.0, -340.0, 48.0), 12.0),
)

BOUNDS = BoxBounds(-105.0, 105.0, -370.0, -310.0, -105.0, 105.0)


def add(a: Vec3, b: Vec3) -> Vec3:
    return (a[0] + b[0], a[1] + b[1], a[2] + b[2])


def sub(a: Vec3, b: Vec3) -> Vec3:
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def mul(a: Vec3, s: float) -> Vec3:
    return (a[0] * s, a[1] * s, a[2] * s)


def dot(a: Vec3, b: Vec3) -> float:
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def length_sq(a: Vec3) -> float:
    return dot(a, a)


def length(a: Vec3) -> float:
    return math.sqrt(max(length_sq(a), 0.0))


def normalize(a: Vec3, fallback: Vec3 = (0.0, 0.0, 1.0)) -> Vec3:
    len_sq = length_sq(a)
    if not math.isfinite(len_sq) or len_sq <= EPSILON:
        return fallback
    inv = 1.0 / math.sqrt(len_sq)
    return mul(a, inv)


def clamp_magnitude(v: Vec3, max_len: float) -> Vec3:
    len_sq = length_sq(v)
    max_sq = max_len * max_len
    if len_sq > max_sq and len_sq > EPSILON:
        return mul(v, max_len / math.sqrt(len_sq))
    return v


def lerp(a: Vec3, b: Vec3, t: float) -> Vec3:
    return (
        a[0] + (b[0] - a[0]) * t,
        a[1] + (b[1] - a[1]) * t,
        a[2] + (b[2] - a[2]) * t,
    )


def hurricane_flow(pos: Vec3, t: float) -> Vec3:
    """Analytical 3D current: rotating storm plus abyssal undertow."""
    to_center = sub(HURRICANE_CENTER, pos)
    horizontal = (to_center[0], 0.0, to_center[2])
    radius_sq = max(length_sq(horizontal), 1.0)
    inv_radius = 1.0 / math.sqrt(radius_sq)
    inward = mul(horizontal, inv_radius)
    tangent = (-inward[2], 0.0, inward[0])
    storm_band = max(0.0, 1.0 - radius_sq / (120.0 * 120.0))
    pulse = 0.75 + 0.25 * (1.0 - abs((t * 0.19) % 2.0 - 1.0))
    swirl = 5.8 * storm_band * pulse
    undertow = 2.2 * storm_band
    shear = 1.2 * math.sin((pos[0] + pos[2]) * 0.035 + t * 0.33)
    return (
        tangent[0] * swirl + inward[0] * undertow + shear,
        0.35 * storm_band * math.sin(t * 0.27),
        tangent[2] * swirl + inward[2] * undertow - shear * 0.35,
    )


def sphere_sdf(pos: Vec3, obstacle: SphereObstacle) -> Tuple[float, Vec3]:
    delta = sub(pos, obstacle.center)
    dist = length(delta)
    normal = normalize(delta, (1.0, 0.0, 0.0))
    return dist - obstacle.radius, normal


def bounds_sdf(pos: Vec3, bounds: BoxBounds) -> Tuple[float, Vec3]:
    distances = (
        pos[0] - bounds.min_x,
        bounds.max_x - pos[0],
        pos[1] - bounds.min_y,
        bounds.max_y - pos[1],
        pos[2] - bounds.min_z,
        bounds.max_z - pos[2],
    )
    index = min(range(6), key=lambda i: distances[i])
    normals = (
        (1.0, 0.0, 0.0),
        (-1.0, 0.0, 0.0),
        (0.0, 1.0, 0.0),
        (0.0, -1.0, 0.0),
        (0.0, 0.0, 1.0),
        (0.0, 0.0, -1.0),
    )
    return distances[index], normals[index]


def obstacle_repulsion(pos: Vec3, weights: SteeringWeights) -> Tuple[Vec3, float]:
    force = (0.0, 0.0, 0.0)
    min_distance = float("inf")
    for obstacle in OBSTACLES:
        dist, normal = sphere_sdf(pos, obstacle)
        min_distance = min(min_distance, dist)
        if dist < weights.wall_influence:
            safe = max(dist, 0.25)
            gain = min(weights.max_repulsion, weights.obstacle_repulsion / (safe * safe))
            force = add(force, mul(normal, gain))

    wall_dist, wall_normal = bounds_sdf(pos, BOUNDS)
    min_distance = min(min_distance, wall_dist)
    if wall_dist < weights.wall_influence:
        safe = max(wall_dist, 0.25)
        gain = min(weights.max_repulsion, weights.obstacle_repulsion / (safe * safe))
        force = add(force, mul(wall_normal, gain))
    return force, min_distance


def enforce_sdf_clearance(pos: Vec3, vel: Vec3, clearance: float = 0.75) -> Tuple[Vec3, Vec3, int]:
    pushouts = 0
    adjusted_pos = pos
    adjusted_vel = vel

    for obstacle in OBSTACLES:
        dist, normal = sphere_sdf(adjusted_pos, obstacle)
        if dist < clearance:
            pushouts += 1
            adjusted_pos = add(adjusted_pos, mul(normal, clearance - dist))
            inward_speed = dot(adjusted_vel, normal)
            if inward_speed < 0.0:
                adjusted_vel = sub(adjusted_vel, mul(normal, inward_speed * 1.2))

    wall_dist, wall_normal = bounds_sdf(adjusted_pos, BOUNDS)
    if wall_dist < clearance:
        pushouts += 1
        adjusted_pos = add(adjusted_pos, mul(wall_normal, clearance - wall_dist))
        inward_speed = dot(adjusted_vel, wall_normal)
        if inward_speed < 0.0:
            adjusted_vel = sub(adjusted_vel, mul(wall_normal, inward_speed * 1.2))

    return adjusted_pos, adjusted_vel, pushouts


def steering_force(pos: Vec3, prev_steer: Vec3, t: float, weights: SteeringWeights) -> Tuple[Vec3, float, float]:
    to_target = sub(TARGET, pos)
    target_dir = normalize(to_target)
    attraction = mul(target_dir, weights.attraction)

    flow = hurricane_flow(pos, t)
    flow_speed_sq = length_sq(flow)
    flow_dir = normalize(flow, (0.0, 0.0, 1.0))
    alignment = dot(flow_dir, target_dir)
    flow_gain = weights.flow_boost if alignment >= 0.0 else -weights.flow_resistance
    flow_bias = mul(flow_dir, math.sqrt(max(flow_speed_sq, 0.0)) * flow_gain)

    repulsion, _ = obstacle_repulsion(pos, weights)
    soft_intent = clamp_magnitude(add(attraction, flow_bias), MAX_ACCEL)
    smoothed_intent = lerp(prev_steer, soft_intent, weights.smoothing_alpha)
    # SDF repulsion is not EWMA-smoothed; delaying wall response causes clipping.
    return clamp_magnitude(add(smoothed_intent, repulsion), MAX_ACCEL), alignment, max(0.0, alignment)


def simulate(weights: SteeringWeights, use_smoothing: bool) -> SimResult:
    pos = START
    vel = (0.0, 0.0, 0.0)
    prev_steer = (0.0, 0.0, 1.0)
    prev_dir = (0.0, 0.0, 1.0)
    path_length = 0.0
    min_obstacle_distance = float("inf")
    jitter_events = 0
    sdf_pushout_events = 0
    alignment_sum = 0.0
    flow_use_sum = 0.0

    active_weights = weights if use_smoothing else SteeringWeights(
        weights.attraction,
        weights.flow_boost,
        weights.flow_resistance,
        weights.obstacle_repulsion,
        1.0,
        weights.idle_flow_coupling,
        weights.wall_influence,
        weights.max_repulsion,
    )

    for step in range(MAX_STEPS):
        t = step * DT
        steer, alignment, flow_use = steering_force(pos, prev_steer, t, active_weights)
        flow = hurricane_flow(pos, t)
        accel = add(steer, mul(flow, 0.08))
        vel = clamp_magnitude(add(mul(vel, 0.94), mul(accel, DT)), MAX_SPEED)
        next_pos = add(pos, mul(vel, DT))
        segment = sub(next_pos, pos)
        path_length += length(segment)
        pos, vel, pushouts = enforce_sdf_clearance(next_pos, vel)
        sdf_pushout_events += pushouts

        _, obstacle_distance = obstacle_repulsion(pos, active_weights)
        min_obstacle_distance = min(min_obstacle_distance, obstacle_distance)
        direction = normalize(vel, prev_dir)
        if dot(direction, prev_dir) < 0.75:
            jitter_events += 1
        prev_dir = direction
        prev_steer = steer
        alignment_sum += alignment
        flow_use_sum += flow_use

        distance_to_target = length(sub(TARGET, pos))
        if distance_to_target <= TARGET_RADIUS:
            score = score_result(True, step + 1, distance_to_target, path_length, min_obstacle_distance, jitter_events, sdf_pushout_events, alignment_sum, flow_use_sum)
            return SimResult(True, step + 1, distance_to_target, path_length, min_obstacle_distance, jitter_events, sdf_pushout_events, alignment_sum / (step + 1), flow_use_sum / (step + 1), score)

    final_distance = length(sub(TARGET, pos))
    score = score_result(False, MAX_STEPS, final_distance, path_length, min_obstacle_distance, jitter_events, sdf_pushout_events, alignment_sum, flow_use_sum)
    return SimResult(False, MAX_STEPS, final_distance, path_length, min_obstacle_distance, jitter_events, sdf_pushout_events, alignment_sum / MAX_STEPS, flow_use_sum / MAX_STEPS, score)


def score_result(
    reached: bool,
    steps: int,
    final_distance: float,
    path_length: float,
    min_obstacle_distance: float,
    jitter_events: int,
    sdf_pushout_events: int,
    alignment_sum: float,
    flow_use_sum: float,
) -> float:
    reach_bonus = 10000.0 if reached else 0.0
    obstacle_penalty = (
        20000.0 + abs(min_obstacle_distance) * 1000.0
        if min_obstacle_distance < 0.75
        else max(0.0, 4.0 - min_obstacle_distance) * 120.0
    )
    jitter_penalty = jitter_events * 500.0
    pushout_penalty = sdf_pushout_events * 5000.0
    distance_penalty = final_distance * 30.0
    path_penalty = path_length * 1.15
    flow_bonus = flow_use_sum * 4.0 + alignment_sum * 1.5
    time_penalty = steps * 2.5
    return reach_bonus + flow_bonus - obstacle_penalty - jitter_penalty - pushout_penalty - distance_penalty - path_penalty - time_penalty


def idle_drift(weights: SteeringWeights) -> dict:
    pos = (-18.0, -340.0, -34.0)
    vel = (0.0, 0.0, 0.0)
    drift_length = 0.0
    alignment_sum = 0.0
    last_flow = normalize(hurricane_flow(pos, 0.0))
    for step in range(180):
        t = step * DT
        flow = hurricane_flow(pos, t)
        flow_dir = normalize(flow, last_flow)
        vel = clamp_magnitude(add(mul(vel, 0.9), mul(flow, weights.idle_flow_coupling * DT)), 4.0)
        next_pos = add(pos, mul(vel, DT))
        drift_length += length(sub(next_pos, pos))
        pos = next_pos
        alignment_sum += dot(normalize(vel, flow_dir), flow_dir)
        last_flow = flow_dir
    return {
        "duration_seconds": 18.0,
        "drift_distance_meters": round(drift_length, 4),
        "mean_velocity_flow_alignment01": round(max(0.0, alignment_sum / 180), 4),
        "final_position": [round(pos[0], 4), round(pos[1], 4), round(pos[2], 4)],
    }


def candidate_weights() -> Iterable[SteeringWeights]:
    attractions = (8.5, 11.0)
    boosts = (0.18, 0.28)
    resistances = (0.06, 0.12)
    repulsions = (144.0, 220.0)
    smoothing = (0.18, 0.28, 0.42)
    for attraction, boost, resistance, repulsion, alpha in product(attractions, boosts, resistances, repulsions, smoothing):
        yield SteeringWeights(
            attraction=attraction,
            flow_boost=boost,
            flow_resistance=resistance,
            obstacle_repulsion=repulsion,
            smoothing_alpha=alpha,
            idle_flow_coupling=0.32,
            wall_influence=20.0,
            max_repulsion=44.0,
        )


def export_float(value: float) -> float:
    return round(value, 6)


def weights_to_dict(weights: SteeringWeights) -> dict:
    return {
        "targetAttractionWeight": export_float(weights.attraction),
        "flowAlignmentBoostWeight": export_float(weights.flow_boost),
        "flowResistanceWeight": export_float(weights.flow_resistance),
        "sdfObstacleRepulsionWeight": export_float(weights.obstacle_repulsion),
        "ewmaSteeringAlpha": export_float(weights.smoothing_alpha),
        "idleFlowCoupling": export_float(weights.idle_flow_coupling),
        "wallInfluenceMeters": export_float(weights.wall_influence),
        "maxRepulsionAcceleration": export_float(weights.max_repulsion),
    }


def weights_from_dict(data: dict) -> SteeringWeights:
    return SteeringWeights(
        attraction=float(data["targetAttractionWeight"]),
        flow_boost=float(data["flowAlignmentBoostWeight"]),
        flow_resistance=float(data["flowResistanceWeight"]),
        obstacle_repulsion=float(data["sdfObstacleRepulsionWeight"]),
        smoothing_alpha=float(data["ewmaSteeringAlpha"]),
        idle_flow_coupling=float(data["idleFlowCoupling"]),
        wall_influence=float(data["wallInfluenceMeters"]),
        max_repulsion=float(data["maxRepulsionAcceleration"]),
    )


def result_to_dict(result: SimResult) -> dict:
    return {
        "reached": result.reached,
        "steps": result.steps,
        "seconds": round(result.steps * DT, 4),
        "finalDistanceMeters": round(result.final_distance, 4),
        "pathLengthMeters": round(result.path_length, 4),
        "minObstacleClearanceMeters": round(result.min_obstacle_distance, 4),
        "jitterEvents": result.jitter_events,
        "sdfPushoutEvents": result.sdf_pushout_events,
        "meanCurrentTargetAlignment": round(result.mean_alignment, 4),
        "meanPositiveFlowUse": round(result.mean_flow_use, 4),
        "score": round(result.score, 4),
    }


def performance_model() -> dict:
    predators = 100
    hz = 10
    samples_per_second = predators * hz
    samples_per_frame_at_60 = samples_per_second / 60.0
    ops_per_sample_low = 210
    ops_per_sample_high = 430
    return {
        "predators": predators,
        "cadenceHz": hz,
        "samplesPerSecond": samples_per_second,
        "samplesPerFrameAt60Hz": round(samples_per_frame_at_60, 4),
        "estimatedScalarOpsPerSampleLow": ops_per_sample_low,
        "estimatedScalarOpsPerSampleHigh": ops_per_sample_high,
        "estimatedScalarOpsPerFrameLow": round(samples_per_frame_at_60 * ops_per_sample_low, 2),
        "estimatedScalarOpsPerFrameHigh": round(samples_per_frame_at_60 * ops_per_sample_high, 2),
        "runtimeEvidence": "STATIC_MODEL_ONLY - requires Burst profiler/GCMonitor for acceptance",
        "mx350BudgetStatus": "PENDING VERIFICATION; designed below 0.1ms by cadence and O(1) samples",
    }


def run_micro_benchmark(weights: SteeringWeights) -> dict:
    iterations = 1000
    pos = START
    prev = (0.0, 0.0, 1.0)
    start = time.perf_counter()
    for i in range(iterations):
        force, _, _ = steering_force(pos, prev, i * DT, weights)
        prev = force
        pos = add(pos, mul(force, 0.001))
    elapsed = time.perf_counter() - start
    return {
        "pythonIterations": iterations,
        "pythonElapsedMs": round(elapsed * 1000.0, 4),
        "pythonMicrosecondsPerSample": round(elapsed * 1_000_000.0 / iterations, 4),
        "runtimeEvidence": "PYTHON_TOOL_ONLY - not Unity runtime proof",
    }


def validate_export(data: dict) -> Tuple[bool, list[str]]:
    errors: list[str] = []

    def expect(condition: bool, message: str) -> None:
        if not condition:
            errors.append(message)

    expect(data.get("schema") == "H8.NavigationTuning.PotentialField.v1", "schema mismatch")
    expect(data.get("status") == "NAVIGATION OPTIMIZED", "status is not NAVIGATION OPTIMIZED")
    expect(data.get("evidenceClass") == "PY_SIM_STATIC_MODEL", "evidence class mismatch")

    snapshot = data.get("sourceParameterSnapshot")
    if not isinstance(snapshot, dict):
        errors.append("missing sourceParameterSnapshot")
    else:
        for key, expected in SOURCE_PARAMETER_SNAPSHOT.items():
            actual = snapshot.get(key)
            expect(actual == expected, f"source parameter mismatch: {key}")

    try:
        weights = weights_from_dict(data["selectedWeights"])
    except (KeyError, TypeError, ValueError) as exc:
        errors.append(f"invalid selectedWeights: {exc}")
        return False, errors

    for name, value in weights_to_dict(weights).items():
        expect(math.isfinite(float(value)), f"non-finite weight: {name}")

    replay = simulate(weights, use_smoothing=True)
    raw = simulate(weights, use_smoothing=False)
    expect(replay.reached, "selected weights do not reach target")
    expect(replay.sdf_pushout_events == 0, "selected weights require SDF pushout")
    expect(replay.min_obstacle_distance >= 2.0, "selected weights violate 2m clearance guard")
    expect(replay.jitter_events <= 1, "selected weights exceed jitter guard")
    expect(replay.jitter_events <= raw.jitter_events, "EWMA increases jitter")

    drift = idle_drift(weights)
    expect(drift["drift_distance_meters"] > 10.0, "idle drift distance too low")
    expect(drift["mean_velocity_flow_alignment01"] > 0.95, "idle drift does not follow current")

    perf = data.get("performanceModel")
    if not isinstance(perf, dict):
        errors.append("missing performanceModel")
    else:
        expect(perf.get("predators") == 100, "performance predator count mismatch")
        expect(perf.get("cadenceHz") == 10, "performance cadence mismatch")
        expect(perf.get("samplesPerSecond") == 1000, "performance samples/sec mismatch")
        expect(perf.get("samplesPerFrameAt60Hz") == 16.6667, "performance samples/frame mismatch")

    return len(errors) == 0, errors


def check_export(output_path: Path) -> int:
    if not output_path.exists():
        print(f"CHECK FAILED: missing {output_path.as_posix()}", file=sys.stderr)
        return 1

    data = json.loads(output_path.read_text(encoding="utf-8"))
    valid, errors = validate_export(data)
    if not valid:
        print("CHECK FAILED")
        for error in errors:
            print(f"- {error}")
        return 1

    print("CHECK PASSED")
    print(f"artifact={output_path.as_posix()}")
    return 0


def build_tiers(best: SteeringWeights) -> dict:
    low = SteeringWeights(best.attraction, best.flow_boost * 0.75, best.flow_resistance, best.obstacle_repulsion * 0.85, max(best.smoothing_alpha, 0.46), 0.26, 6.0, 10.0)
    middle = best
    high = SteeringWeights(best.attraction * 1.05, best.flow_boost * 1.15, best.flow_resistance * 0.85, best.obstacle_repulsion, min(best.smoothing_alpha, 0.34), 0.34, 8.0, 14.0)
    ultra = SteeringWeights(best.attraction * 1.08, best.flow_boost * 1.25, best.flow_resistance * 0.75, best.obstacle_repulsion * 1.1, min(best.smoothing_alpha, 0.28), 0.38, 10.0, 16.0)
    return {
        "Low": {
            "weights": weights_to_dict(low),
            "mathLod": "one flow sample, one nearest SDF/bounds repulsion, strongest current axis only",
            "cadenceHz": 10,
        },
        "Middle": {
            "weights": weights_to_dict(middle),
            "mathLod": "one flow sample, obstacle loop capped to local SDF proxies, EWMA final steering",
            "cadenceHz": 10,
        },
        "High": {
            "weights": weights_to_dict(high),
            "mathLod": "flow alignment plus richer local SDF gradient, tighter smoothing for pursuit",
            "cadenceHz": 15,
        },
        "Ultra": {
            "weights": weights_to_dict(ultra),
            "mathLod": "adds local vortex interest and visual-overkill path curvature while retaining O(1) steering",
            "cadenceHz": 20,
        },
    }


def main(argv: Sequence[str] | None = None) -> int:
    args = tuple(sys.argv[1:] if argv is None else argv)
    output_path = Path("Data/AI/Navigation_Tuning.json")
    if args == ("--check",):
        return check_export(output_path)
    if args:
        print("Usage: Tools/AiPathSim.py [--check]", file=sys.stderr)
        return 2

    best_weights = None
    best_result = None
    best_raw = None
    evaluated = 0
    reached_count = 0

    for weights in candidate_weights():
        raw = simulate(weights, use_smoothing=False)
        smoothed = simulate(weights, use_smoothing=True)
        evaluated += 1
        if smoothed.reached:
            reached_count += 1
        if best_result is None or smoothed.score > best_result.score:
            best_weights = weights
            best_result = smoothed
            best_raw = raw

    if best_weights is None or best_result is None or best_raw is None:
        raise RuntimeError("No steering candidates evaluated")

    output = {
        "schema": "H8.NavigationTuning.PotentialField.v1",
        "status": "NAVIGATION OPTIMIZED",
        "evidenceClass": "PY_SIM_STATIC_MODEL",
        "promptId": "AI_POTENTIAL_FIELD_NAVIGATOR",
        "sourceContracts": {
            "flowField": "AbyssalFlowField analytical current sample; no GPU readback",
            "obstacle": "Voxel SDF / hybrid nav clearance snapshot; 1/d^2 repulsion",
            "target": "player/prey biomass pull vector from cognition input",
            "phase": "SIMULATION for steering solve; POST_SIMULATION for blackbox write; VISUAL_SYNC only consumes output",
        },
        "sourceParameterSnapshot": SOURCE_PARAMETER_SNAPSHOT,
        "formula": {
            "targetAttraction": "normalize(target - position) * targetAttractionWeight",
            "flowBoostResistance": "flowDir * flowSpeed * (dot(flowDir,targetDir) >= 0 ? boost : -resistance)",
            "obstacleRepulsion": "normal * min(maxRepulsion, sdfObstacleRepulsionWeight / max(distanceMeters,0.25)^2)",
            "ewma": "intent = lerp(previousSteering, targetAttraction + flowBoostResistance, ewmaSteeringAlpha); steering = intent + immediateSdfRepulsion",
            "idleDrift": "idleVelocity += flowVector * idleFlowCoupling * dt",
        },
        "search": {
            "candidatesEvaluated": evaluated,
            "candidatesReachedTarget": reached_count,
            "scenario": "predator crosses a bounded 3D hurricane current with four SDF obstacle proxies",
            "dtSeconds": DT,
            "maxSteps": MAX_STEPS,
        },
        "selectedWeights": weights_to_dict(best_weights),
        "rawNoSmoothingMetrics": result_to_dict(best_raw),
        "smoothedMetrics": result_to_dict(best_result),
        "idleDriftVerification": idle_drift(best_weights),
        "performanceModel": performance_model(),
        "pythonMicroBenchmark": run_micro_benchmark(best_weights),
        "tierProfiles": build_tiers(best_weights),
        "failureModes": [
            "No finite flow sample: use target attraction plus SDF repulsion only.",
            "Inside solid or negative SDF clearance: override with strongest positive SDF gradient and suppress attack thrust.",
            "JitterEvents rising over 3-frame window: increase EWMA alpha toward Low tier and cap flow boost.",
            "Frame budget pressure: reduce cadence to 5Hz and share flow samples by 16m goal cluster.",
        ],
    }

    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(output, indent=2, sort_keys=True) + "\n", encoding="utf-8")

    print("NAVIGATION OPTIMIZED")
    print(f"candidates={evaluated} reached={reached_count}")
    print(f"raw_jitter={best_raw.jitter_events} smoothed_jitter={best_result.jitter_events}")
    print(f"smoothed_seconds={best_result.steps * DT:.2f} final_distance={best_result.final_distance:.3f}")
    print(f"output={output_path.as_posix()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
