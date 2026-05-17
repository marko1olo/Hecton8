#!/usr/bin/env python3
"""Deterministic potential-field steering simulator for HECTON-8 fauna.

The simulator is tooling only. Runtime implementation should port the selected
weights into Burst/NativeArray jobs and consume flow/SDF snapshots through
existing domain contracts.
"""

from __future__ import annotations

import hashlib
import json
import math
import re
import struct
import sys
import zlib
from dataclasses import dataclass
from itertools import product
from pathlib import Path
from typing import Iterable, Sequence, Tuple


EPSILON = 1.0e-6
FLOW_TEXTURE_RESOLUTION = 32
FLOW_TEXTURE_WORLD_SIZE_METERS = 100.0
FLOW_TEXTURE_CELL_SIZE_METERS = FLOW_TEXTURE_WORLD_SIZE_METERS / FLOW_TEXTURE_RESOLUTION
VECTOR_NOISE_RESOLUTION = 32
SURFACE_STORM_LAYER_DEPTH_METERS = 50.0
STORM_SURFACE_TURBULENCE_STRENGTH = 0.4
ABYSSAL_THERMOCLINE_DEPTH_METERS = 120.0
HEAT_SOURCE_CAPACITY = 8
POTENTIAL_SOLVE_HZ = 10
DT = 1.0 / POTENTIAL_SOLVE_HZ
MAX_STEPS = POTENTIAL_SOLVE_HZ * 36
TARGET_RADIUS = round(FLOW_TEXTURE_CELL_SIZE_METERS * 0.96, 3)
MAX_SPEED = 13.0
MAX_ACCEL = 30.0
TRACE_SAMPLE_STRIDE_STEPS = 20
START = (-82.0, -340.0, -64.0)
TARGET = (78.0, -340.0, 68.0)
HURRICANE_CENTER = (0.0, -340.0, 0.0)
STORM_RADIUS_METERS = FLOW_TEXTURE_WORLD_SIZE_METERS + SURFACE_STORM_LAYER_DEPTH_METERS * STORM_SURFACE_TURBULENCE_STRENGTH
STORM_PULSE_BASE = 0.75
STORM_PULSE_AMPLITUDE = 1.0 - STORM_PULSE_BASE
STORM_PULSE_HZ = 0.19
STORM_SWIRL_METERS_PER_SECOND = round(SURFACE_STORM_LAYER_DEPTH_METERS * STORM_SURFACE_TURBULENCE_STRENGTH * 0.29, 1)
STORM_UNDERTOW_METERS_PER_SECOND = round(SURFACE_STORM_LAYER_DEPTH_METERS * STORM_SURFACE_TURBULENCE_STRENGTH * 0.11, 1)
STORM_SHEAR_METERS_PER_SECOND = round(VECTOR_NOISE_RESOLUTION * STORM_SURFACE_TURBULENCE_STRENGTH * 0.09375, 1)
STORM_SHEAR_SPATIAL_FREQUENCY = 0.035
STORM_SHEAR_TEMPORAL_FREQUENCY = 0.33
STORM_VERTICAL_FLOW_METERS_PER_SECOND = round(STORM_SURFACE_TURBULENCE_STRENGTH * 0.875, 2)
STORM_VERTICAL_TEMPORAL_FREQUENCY = 0.27
STORM_TANGENT_SHEAR_LEAK = STORM_VERTICAL_FLOW_METERS_PER_SECOND
FLOW_CURRENT_COUPLING = 0.08
VELOCITY_DAMPING = 0.94
IDLE_VELOCITY_DAMPING = 0.9
IDLE_DRIFT_SECONDS = 18
SDF_INVERSE_SQUARE_MIN_DISTANCE_METERS = 0.25
SDF_PUSHOUT_CLEARANCE_METERS = 0.75
SDF_REBOUND_SCALE = 1.2
MIN_REPLAY_CLEARANCE_GUARD_METERS = 2.0
IDLE_DRIFT_MIN_DISTANCE_METERS = 10.0
IDLE_DRIFT_MIN_ALIGNMENT01 = 0.95
REACH_SCORE_BONUS = 10000.0
HARD_OBSTACLE_PENALTY_BASE = 20000.0
HARD_OBSTACLE_PENALTY_PER_METER = 1000.0
SOFT_OBSTACLE_CLEARANCE_TARGET_METERS = 4.0
SOFT_OBSTACLE_PENALTY_SCALE = 120.0
JITTER_EVENT_PENALTY = 500.0
SDF_PUSHOUT_EVENT_PENALTY = 5000.0
FINAL_DISTANCE_PENALTY_PER_METER = 30.0
PATH_LENGTH_PENALTY_PER_METER = 1.15
POSITIVE_FLOW_USE_BONUS = 4.0
FLOW_ALIGNMENT_BONUS = 1.5
TIME_STEP_PENALTY = 2.5
SOURCE_ROOT = Path(__file__).resolve().parents[1]
FLUID_ENGINE_PATH = Path("Assets/_Project/Scripts/HectonFluidEngine.cs")
BINARY_CACHE_PATH = Path("Data/AI/Navigation_Tuning.h8bin")
BINARY_MANIFEST_PATH = Path("Data/AI/Navigation_Tuning.manifest.json")
BINARY_MAGIC = b"H8AN"
BINARY_VERSION = 1
BINARY_ALIGNMENT = 16
BINARY_FLAG_LITTLE_ENDIAN = 1
BINARY_HEADER_FORMAT = "<4sHHIIIIIIII24s"
BINARY_RECORD_FORMAT = "<IHHfI"
BINARY_HEADER_SIZE = struct.calcsize(BINARY_HEADER_FORMAT)
BINARY_RECORD_SIZE = struct.calcsize(BINARY_RECORD_FORMAT)
UINT32_MASK = 0xFFFFFFFF

RECORD_KIND_FLOAT32 = 1
RECORD_KIND_UINT16 = 2
RECORD_KIND_BOOL = 3
RECORD_KIND_SECONDS = 4
RECORD_KIND_METERS = 5
RECORD_KIND_RATE = 6

SECTION_SOURCE = 1
SECTION_SEARCH = 2
SECTION_WEIGHTS = 3
SECTION_SMOOTHED_METRICS = 4
SECTION_RAW_METRICS = 5
SECTION_IDLE = 6
SECTION_PERFORMANCE = 7
SECTION_COST = 8
SECTION_TIER = 9

SOURCE_PARAMETER_SNAPSHOT = {
    "flowTextureResolution": FLOW_TEXTURE_RESOLUTION,
    "flowTextureWorldSizeMeters": FLOW_TEXTURE_WORLD_SIZE_METERS,
    "flowTextureCellSizeMeters": FLOW_TEXTURE_CELL_SIZE_METERS,
    "vectorNoiseResolution": VECTOR_NOISE_RESOLUTION,
    "surfaceStormLayerDepthMeters": SURFACE_STORM_LAYER_DEPTH_METERS,
    "stormSurfaceTurbulenceStrength": STORM_SURFACE_TURBULENCE_STRENGTH,
    "abyssalThermoclineDepthMeters": ABYSSAL_THERMOCLINE_DEPTH_METERS,
    "heatSourceCapacity": HEAT_SOURCE_CAPACITY,
    "sourceFiles": [
        "Assets/_Project/Scripts/HectonFluidEngine.cs",
        "Docs/ARCHITECTURE/FLOW_FIELD_MATH.md",
        ".agents-skills/CORE_Weather_Abyssal_FlowField_Currents.txt",
    ],
}

SOURCE_CONSTANTS = {
    "flowTextureResolution": ("AbyssalFlowTextureResolution", FLOW_TEXTURE_RESOLUTION),
    "flowTextureWorldSizeMeters": ("AbyssalFlowTextureWorldSizeMeters", FLOW_TEXTURE_WORLD_SIZE_METERS),
    "vectorNoiseResolution": ("VectorNoiseResolution", VECTOR_NOISE_RESOLUTION),
    "surfaceStormLayerDepthMeters": ("SurfaceStormLayerDepthMeters", SURFACE_STORM_LAYER_DEPTH_METERS),
    "stormSurfaceTurbulenceStrength": ("StormSurfaceTurbulenceStrength", STORM_SURFACE_TURBULENCE_STRENGTH),
    "abyssalThermoclineDepthMeters": ("AbyssalFlowThermoclineDepthMeters", ABYSSAL_THERMOCLINE_DEPTH_METERS),
    "heatSourceCapacity": ("MaxAbyssalHeatSourceCount", HEAT_SOURCE_CAPACITY),
}

SOURCE_CONTRACTS = {
    "flowField": "AbyssalFlowField analytical current sample; no GPU readback",
    "obstacle": "Voxel SDF / hybrid nav clearance snapshot; 1/d^2 repulsion",
    "target": "player/prey biomass pull vector from cognition input",
    "phase": "SIMULATION for steering solve; POST_SIMULATION for blackbox write; VISUAL_SYNC only consumes output",
}

FORMULA_CONTRACT = {
    "targetAttraction": "normalize(target - position) * targetAttractionWeight",
    "flowBoostResistance": "flowDir * flowSpeed * (dot(flowDir,targetDir) >= 0 ? boost : -resistance)",
    "obstacleRepulsion": "normal * min(maxRepulsion, sdfObstacleRepulsionWeight / max(distanceMeters,0.25)^2)",
    "ewma": "intent = lerp(previousSteering, targetAttraction + flowBoostResistance, ewmaSteeringAlpha); steering = intent + immediateSdfRepulsion",
    "idleDrift": "idleVelocity += flowVector * idleFlowCoupling * dt",
}

BLACK_BOX_TELEMETRY_SCHEMA = {
    "capacityFrames": 300,
    "storage": "NativeArray<AiPotentialFieldTelemetryEntry> circular buffer",
    "dumpPath": "Docs/AgentLogs/Dump_AI_POTENTIAL_FIELD_NAVIGATOR.bin",
    "dumpTriggers": [
        "nonFinitePosition",
        "nonFiniteVelocity",
        "negativeSdfClearance",
        "sourceParameterDrift",
    ],
    "fields": [
        {"name": "frameIndex", "type": "uint"},
        {"name": "entityId", "type": "uint"},
        {"name": "positionAupCell", "type": "int3"},
        {"name": "positionLocalMeters", "type": "float3"},
        {"name": "velocityMetersPerSecond", "type": "float3"},
        {"name": "targetDistanceMeters", "type": "float"},
        {"name": "flowAlignmentSigned", "type": "float"},
        {"name": "sdfClearanceMeters", "type": "float"},
        {"name": "stateFlags", "type": "uint"},
        {"name": "stateHash", "type": "uint"},
    ],
    "finiteGuards": [
        "positionLocalMeters",
        "velocityMetersPerSecond",
        "targetDistanceMeters",
        "flowAlignmentSigned",
        "sdfClearanceMeters",
    ],
}

TIER_HYSTERESIS = {
    "distanceMeters": 5.0,
    "timeSeconds": 3.0,
    "rule": "Tier/scalability changes require both bands before switching to prevent AI LOD flip-flop.",
}

HARD_SCIENCE_AUDIT = {
    "domain": "Potential-field steering; not a fluid physics solver.",
    "targetAttraction": {
        "basis": "unit pull vector from resolved cognition target",
        "formula": FORMULA_CONTRACT["targetAttraction"],
        "derivedFrom": "predator/prey intent, not a hand-authored force field",
    },
    "flowBoostResistance": {
        "basis": "AbyssalFlowField vector as a steering hint",
        "formula": FORMULA_CONTRACT["flowBoostResistance"],
        "derivedFrom": "dot(flowDir,targetDir) determines boost versus resistance",
    },
    "obstacleRepulsion": {
        "basis": "inverse-square SDF boundary pressure",
        "formula": FORMULA_CONTRACT["obstacleRepulsion"],
        "safeDistanceClampMeters": 0.25,
        "derivedFrom": "1/d^2 repulsion with divide guard; SDF term is immediate, not EWMA delayed",
    },
    "ewma": {
        "basis": "discrete exponential weighted moving average for intent jitter suppression",
        "formula": FORMULA_CONTRACT["ewma"],
        "rejection": "SDF wall term is excluded from smoothing because delayed walls clip predators through boundaries.",
    },
    "sourceScenarioDerivation": {
        "stormRadiusMeters": "flowTextureWorldSizeMeters + surfaceStormLayerDepthMeters * stormSurfaceTurbulenceStrength",
        "swirlScalar": "surfaceStormLayerDepthMeters * stormSurfaceTurbulenceStrength * 0.29",
        "undertowScalar": "surfaceStormLayerDepthMeters * stormSurfaceTurbulenceStrength * 0.11",
        "shearScalar": "vectorNoiseResolution * stormSurfaceTurbulenceStrength * 0.09375",
        "verticalScalar": "stormSurfaceTurbulenceStrength * 0.875",
        "tangentShearLeak": "same magnitude as vertical flow scalar; presentation-current asymmetry only",
        "pulseBand": "deterministic triangle wave, not random noise",
        "hurricaneCenter": "scenario midpoint at the predator crossing depth",
    },
}

SIMULATION_CONSTANT_AUDIT = {
    "dtSeconds": {"value": DT, "derivation": "1 / 10Hz potential solve cadence from prompt"},
    "maxSteps": {"value": MAX_STEPS, "derivation": "36 second stress replay at 10Hz"},
    "targetRadiusMeters": {
        "value": TARGET_RADIUS,
        "derivation": "0.96 * 3.125m flow texture cell; sub-cell reach tolerance",
    },
    "sdfSafeDistanceClampMeters": {"value": SDF_INVERSE_SQUARE_MIN_DISTANCE_METERS, "derivation": "inverse-square divide guard"},
    "sdfPushoutClearanceMeters": {"value": SDF_PUSHOUT_CLEARANCE_METERS, "derivation": "post-integrate replay safety skin"},
    "velocityDamping": {"value": VELOCITY_DAMPING, "derivation": "predator inertia fake; prevents stop-start thrash"},
    "idleVelocityDamping": {"value": IDLE_VELOCITY_DAMPING, "derivation": "idle drift fake; keeps fauna coupled to flow"},
    "flowCurrentCoupling": {"value": FLOW_CURRENT_COUPLING, "derivation": "visual-belief current bleed into predator velocity"},
    "minReplayClearanceGuardMeters": {"value": MIN_REPLAY_CLEARANCE_GUARD_METERS, "derivation": "selection guard for no-clipping path acceptance"},
    "idleDriftMinDistanceMeters": {"value": IDLE_DRIFT_MIN_DISTANCE_METERS, "derivation": "idle-current-belief acceptance floor"},
    "idleDriftMinAlignment01": {"value": IDLE_DRIFT_MIN_ALIGNMENT01, "derivation": "idle-current-belief acceptance floor"},
    "scoringWeights": {
        "reachBonus": REACH_SCORE_BONUS,
        "hardObstaclePenaltyBase": HARD_OBSTACLE_PENALTY_BASE,
        "hardObstaclePenaltyPerMeter": HARD_OBSTACLE_PENALTY_PER_METER,
        "softObstacleClearanceTargetMeters": SOFT_OBSTACLE_CLEARANCE_TARGET_METERS,
        "softObstaclePenaltyScale": SOFT_OBSTACLE_PENALTY_SCALE,
        "jitterEventPenalty": JITTER_EVENT_PENALTY,
        "sdfPushoutEventPenalty": SDF_PUSHOUT_EVENT_PENALTY,
        "finalDistancePenaltyPerMeter": FINAL_DISTANCE_PENALTY_PER_METER,
        "pathLengthPenaltyPerMeter": PATH_LENGTH_PENALTY_PER_METER,
        "positiveFlowUseBonus": POSITIVE_FLOW_USE_BONUS,
        "flowAlignmentBonus": FLOW_ALIGNMENT_BONUS,
        "timeStepPenalty": TIME_STEP_PENALTY,
        "derivation": "selection-objective weights; not physical coefficients or runtime physics truth",
    },
    "performancePopulation": {"value": 100, "derivation": "prompt requirement"},
    "performanceCadenceHz": {"value": POTENTIAL_SOLVE_HZ, "derivation": "prompt requirement"},
}

DATA_SOVEREIGNTY_AUDIT = {
    "runtimeLookupContract": "stateless FNV-1a record lookup into DataVault-owned NativeArray records; no private managed dictionaries in Tick.",
    "stateOwnership": "AI movement owner consumes read-only flow/SDF/cognition snapshots and writes steering output plus black-box telemetry.",
    "dependencyBoundary": "no concrete fluid, voxel, player, or economy class references in hot path",
    "hPhiImpact": "increases sovereignty by moving tuning into fixed binary records with standalone manifest metadata",
}

TOASTER_DATA = {
    "profile": "Low/MX350/Celeron-i3",
    "records": [
        "selectedWeights",
        "search cadence",
        "smoothed metrics",
        "tier hysteresis",
    ],
    "runtimeShape": "one flow sample, one nearest SDF/bounds sample, 10Hz solve, no strings in hot path",
    "maxBinaryBytes": 1280,
}

RTX_OVERKILL_DATA = {
    "profile": "Ultra/God-Mode visuals",
    "extraDataFields": [
        "harmonicFlowBand0",
        "harmonicFlowBand1",
        "harmonicFlowBand2",
        "sdfGradientSample8",
        "bankingCurvatureSamples",
        "wakeRibbonSamples",
        "currentAnomalySonarPulse",
    ],
    "runtimeBoundary": "presentation-only visual overkill; gameplay steering truth stays the fixed potential-field record set",
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


@dataclass(frozen=True)
class BinaryRecord:
    label: str
    section_id: int
    kind: int
    value: float
    flags: int = 0


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
    storm_band = max(0.0, 1.0 - radius_sq / (STORM_RADIUS_METERS * STORM_RADIUS_METERS))
    pulse = STORM_PULSE_BASE + STORM_PULSE_AMPLITUDE * (1.0 - abs((t * STORM_PULSE_HZ) % 2.0 - 1.0))
    swirl = STORM_SWIRL_METERS_PER_SECOND * storm_band * pulse
    undertow = STORM_UNDERTOW_METERS_PER_SECOND * storm_band
    shear = STORM_SHEAR_METERS_PER_SECOND * math.sin(
        (pos[0] + pos[2]) * STORM_SHEAR_SPATIAL_FREQUENCY + t * STORM_SHEAR_TEMPORAL_FREQUENCY
    )
    return (
        tangent[0] * swirl + inward[0] * undertow + shear,
        STORM_VERTICAL_FLOW_METERS_PER_SECOND * storm_band * math.sin(t * STORM_VERTICAL_TEMPORAL_FREQUENCY),
        tangent[2] * swirl + inward[2] * undertow - shear * STORM_TANGENT_SHEAR_LEAK,
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
            safe = max(dist, SDF_INVERSE_SQUARE_MIN_DISTANCE_METERS)
            gain = min(weights.max_repulsion, weights.obstacle_repulsion / (safe * safe))
            force = add(force, mul(normal, gain))

    wall_dist, wall_normal = bounds_sdf(pos, BOUNDS)
    min_distance = min(min_distance, wall_dist)
    if wall_dist < weights.wall_influence:
        safe = max(wall_dist, SDF_INVERSE_SQUARE_MIN_DISTANCE_METERS)
        gain = min(weights.max_repulsion, weights.obstacle_repulsion / (safe * safe))
        force = add(force, mul(wall_normal, gain))
    return force, min_distance


def enforce_sdf_clearance(pos: Vec3, vel: Vec3, clearance: float = SDF_PUSHOUT_CLEARANCE_METERS) -> Tuple[Vec3, Vec3, int]:
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
                adjusted_vel = sub(adjusted_vel, mul(normal, inward_speed * SDF_REBOUND_SCALE))

    wall_dist, wall_normal = bounds_sdf(adjusted_pos, BOUNDS)
    if wall_dist < clearance:
        pushouts += 1
        adjusted_pos = add(adjusted_pos, mul(wall_normal, clearance - wall_dist))
        inward_speed = dot(adjusted_vel, wall_normal)
        if inward_speed < 0.0:
            adjusted_vel = sub(adjusted_vel, mul(wall_normal, inward_speed * SDF_REBOUND_SCALE))

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
        accel = add(steer, mul(flow, FLOW_CURRENT_COUPLING))
        vel = clamp_magnitude(add(mul(vel, VELOCITY_DAMPING), mul(accel, DT)), MAX_SPEED)
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
    reach_bonus = REACH_SCORE_BONUS if reached else 0.0
    obstacle_penalty = (
        HARD_OBSTACLE_PENALTY_BASE + abs(min_obstacle_distance) * HARD_OBSTACLE_PENALTY_PER_METER
        if min_obstacle_distance < SDF_PUSHOUT_CLEARANCE_METERS
        else max(0.0, SOFT_OBSTACLE_CLEARANCE_TARGET_METERS - min_obstacle_distance) * SOFT_OBSTACLE_PENALTY_SCALE
    )
    jitter_penalty = jitter_events * JITTER_EVENT_PENALTY
    pushout_penalty = sdf_pushout_events * SDF_PUSHOUT_EVENT_PENALTY
    distance_penalty = final_distance * FINAL_DISTANCE_PENALTY_PER_METER
    path_penalty = path_length * PATH_LENGTH_PENALTY_PER_METER
    flow_bonus = flow_use_sum * POSITIVE_FLOW_USE_BONUS + alignment_sum * FLOW_ALIGNMENT_BONUS
    time_penalty = steps * TIME_STEP_PENALTY
    return reach_bonus + flow_bonus - obstacle_penalty - jitter_penalty - pushout_penalty - distance_penalty - path_penalty - time_penalty


def idle_drift(weights: SteeringWeights) -> dict:
    pos = (-18.0, -340.0, -34.0)
    vel = (0.0, 0.0, 0.0)
    drift_length = 0.0
    alignment_sum = 0.0
    last_flow = normalize(hurricane_flow(pos, 0.0))
    idle_steps = IDLE_DRIFT_SECONDS * POTENTIAL_SOLVE_HZ
    for step in range(idle_steps):
        t = step * DT
        flow = hurricane_flow(pos, t)
        flow_dir = normalize(flow, last_flow)
        vel = clamp_magnitude(add(mul(vel, IDLE_VELOCITY_DAMPING), mul(flow, weights.idle_flow_coupling * DT)), 4.0)
        next_pos = add(pos, mul(vel, DT))
        drift_length += length(sub(next_pos, pos))
        pos = next_pos
        alignment_sum += dot(normalize(vel, flow_dir), flow_dir)
        last_flow = flow_dir
    return {
        "duration_seconds": float(IDLE_DRIFT_SECONDS),
        "drift_distance_meters": round(drift_length, 4),
        "mean_velocity_flow_alignment01": round(max(0.0, alignment_sum / idle_steps), 4),
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


def extract_source_constants(source_text: str, constant_name: str) -> list[float]:
    pattern = re.compile(
        rf"\bconst\s+(?:int|float)\s+{re.escape(constant_name)}\s*=\s*"
        r"(?P<value>-?\d+(?:\.\d+)?)(?:f)?\s*;"
    )
    return [float(match.group("value")) for match in pattern.finditer(source_text)]


def finite_float(value: object) -> float | None:
    if isinstance(value, bool):
        return None
    try:
        result = float(value)
    except (TypeError, ValueError):
        return None
    if not math.isfinite(result):
        return None
    return result


def validate_source_constants(snapshot: dict, root: Path | None = None) -> Tuple[bool, list[str]]:
    errors: list[str] = []
    project_root = SOURCE_ROOT if root is None else root

    source_files = snapshot.get("sourceFiles")
    if not isinstance(source_files, list) or not source_files:
        errors.append("missing sourceFiles")
    else:
        for source_file in source_files:
            if not isinstance(source_file, str) or not source_file:
                errors.append("invalid source file reference")
                continue
            relative_path = Path(source_file)
            if relative_path.is_absolute():
                errors.append(f"absolute source file reference forbidden: {source_file}")
                continue
            if not (project_root / relative_path).exists():
                errors.append(f"missing source contract file: {source_file}")

    source_path = project_root / FLUID_ENGINE_PATH
    if not source_path.exists():
        return False, [f"missing source file: {FLUID_ENGINE_PATH.as_posix()}"]

    source_text = source_path.read_text(encoding="utf-8")
    for snapshot_key, (constant_name, expected_value) in SOURCE_CONSTANTS.items():
        actual_values = extract_source_constants(source_text, constant_name)
        if not actual_values:
            errors.append(f"missing source constant: {constant_name}")
            continue

        exported = snapshot.get(snapshot_key)
        if exported is None:
            errors.append(f"missing exported source parameter: {snapshot_key}")
            continue
        exported_number = finite_float(exported)
        if exported_number is None:
            errors.append(f"non-finite exported source parameter: {snapshot_key}")
            continue

        for actual in actual_values:
            if abs(actual - exported_number) > 0.00001:
                errors.append(f"source/export drift: {constant_name}")

            if abs(actual - float(expected_value)) > 0.00001:
                errors.append(f"source/default drift: {constant_name}")

    resolutions = extract_source_constants(source_text, "AbyssalFlowTextureResolution")
    world_sizes = extract_source_constants(source_text, "AbyssalFlowTextureWorldSizeMeters")
    exported_cell = snapshot.get("flowTextureCellSizeMeters")
    if resolutions and world_sizes and exported_cell is not None:
        exported_cell_number = finite_float(exported_cell)
        if exported_cell_number is None:
            errors.append("non-finite exported source parameter: flowTextureCellSizeMeters")
            return False, errors
        cell_size = world_sizes[0] / max(resolutions[0], EPSILON)
        if abs(cell_size - exported_cell_number) > 0.00001:
            errors.append("source/export drift: AbyssalFlowTextureCellSizeMeters")

    return len(errors) == 0, errors


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


def rounded_vec(v: Vec3) -> list[float]:
    return [round(v[0], 4), round(v[1], 4), round(v[2], 4)]


def trace_path(weights: SteeringWeights, use_smoothing: bool = True) -> dict:
    pos = START
    vel = (0.0, 0.0, 0.0)
    prev_steer = (0.0, 0.0, 1.0)
    min_obstacle_distance = float("inf")
    sdf_pushout_events = 0

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

    _, start_clearance = obstacle_repulsion(pos, active_weights)
    start_alignment = dot(normalize(hurricane_flow(pos, 0.0)), normalize(sub(TARGET, pos)))
    samples = [
        {
            "step": 0,
            "timeSeconds": 0.0,
            "position": rounded_vec(pos),
            "distanceToTargetMeters": round(length(sub(TARGET, pos)), 4),
            "clearanceMeters": round(start_clearance, 4),
            "flowAlignmentSigned": round(start_alignment, 4),
        }
    ]

    reached = False
    final_distance = length(sub(TARGET, pos))
    steps = 0
    for step in range(MAX_STEPS):
        t = step * DT
        steer, alignment, _ = steering_force(pos, prev_steer, t, active_weights)
        flow = hurricane_flow(pos, t)
        accel = add(steer, mul(flow, FLOW_CURRENT_COUPLING))
        vel = clamp_magnitude(add(mul(vel, VELOCITY_DAMPING), mul(accel, DT)), MAX_SPEED)
        next_pos = add(pos, mul(vel, DT))
        pos, vel, pushouts = enforce_sdf_clearance(next_pos, vel)
        sdf_pushout_events += pushouts
        _, obstacle_distance = obstacle_repulsion(pos, active_weights)
        min_obstacle_distance = min(min_obstacle_distance, obstacle_distance)
        prev_steer = steer
        steps = step + 1
        final_distance = length(sub(TARGET, pos))
        reached = final_distance <= TARGET_RADIUS

        if steps % TRACE_SAMPLE_STRIDE_STEPS == 0 or reached:
            samples.append(
                {
                    "step": steps,
                    "timeSeconds": round(steps * DT, 4),
                    "position": rounded_vec(pos),
                    "distanceToTargetMeters": round(final_distance, 4),
                    "clearanceMeters": round(obstacle_distance, 4),
                    "flowAlignmentSigned": round(alignment, 4),
                }
            )

        if reached:
            break

    return {
        "scenario": "selected smoothed predator path through hurricane current",
        "sampleStrideSteps": TRACE_SAMPLE_STRIDE_STEPS,
        "reached": reached,
        "steps": steps,
        "finalDistanceMeters": round(final_distance, 4),
        "minObstacleClearanceMeters": round(min_obstacle_distance, 4),
        "sdfPushoutEvents": sdf_pushout_events,
        "samples": samples,
    }


def find_best_candidate() -> Tuple[SteeringWeights, SimResult, SimResult, int, int]:
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

    return best_weights, best_result, best_raw, evaluated, reached_count


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


def deterministic_sample_cost_model() -> dict:
    return {
        "model": "STATIC_SCALAR_OP_ESTIMATE",
        "flowSamplesPerSolve": 1,
        "sdfProxyChecksPerSolve": len(OBSTACLES) + 1,
        "normalizationsPerSolve": 5,
        "sqrtOrRsqrtSitesPerSolve": 5,
        "branchClampSitesPerSolve": 7,
        "runtimeEvidence": "DETERMINISTIC_MODEL_ONLY - requires Burst profiler/GCMonitor for acceptance",
    }


def canonical_json_bytes(data: object) -> bytes:
    return (json.dumps(data, indent=2, sort_keys=True) + "\n").encode("utf-8")


def sha256_hex(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest().upper()


def fnv1a32(text: str) -> int:
    value = 2166136261
    for byte in text.lower().encode("ascii"):
        value ^= byte
        value = (value * 16777619) & UINT32_MASK
    return value


def add_binary_record(records: list[BinaryRecord], label: str, section_id: int, kind: int, value: object, flags: int = 0) -> None:
    number = finite_float(value)
    if number is None:
        raise ValueError(f"non-finite binary record value: {label}")
    records.append(BinaryRecord(label, section_id, kind, float(number), flags))


def collect_binary_records(data: dict) -> list[BinaryRecord]:
    records: list[BinaryRecord] = []
    source = data["sourceParameterSnapshot"]
    search = data["search"]
    selected = data["selectedWeights"]
    smoothed = data["smoothedMetrics"]
    raw = data["rawNoSmoothingMetrics"]
    idle = data["idleDriftVerification"]
    performance = data["performanceModel"]
    cost = data["sampleCostModel"]
    tiers = data["tierProfiles"]

    for key in (
        "flowTextureResolution",
        "flowTextureWorldSizeMeters",
        "flowTextureCellSizeMeters",
        "vectorNoiseResolution",
        "surfaceStormLayerDepthMeters",
        "stormSurfaceTurbulenceStrength",
        "abyssalThermoclineDepthMeters",
        "heatSourceCapacity",
    ):
        add_binary_record(records, f"sourceParameterSnapshot.{key}", SECTION_SOURCE, RECORD_KIND_FLOAT32, source[key])

    for key in ("candidatesEvaluated", "candidatesReachedTarget", "dtSeconds", "maxSteps"):
        kind = RECORD_KIND_SECONDS if key == "dtSeconds" else RECORD_KIND_UINT16
        add_binary_record(records, f"search.{key}", SECTION_SEARCH, kind, search[key])

    for key in (
        "targetAttractionWeight",
        "flowAlignmentBoostWeight",
        "flowResistanceWeight",
        "sdfObstacleRepulsionWeight",
        "ewmaSteeringAlpha",
        "idleFlowCoupling",
        "wallInfluenceMeters",
        "maxRepulsionAcceleration",
    ):
        add_binary_record(records, f"selectedWeights.{key}", SECTION_WEIGHTS, RECORD_KIND_FLOAT32, selected[key])

    for key in (
        "steps",
        "seconds",
        "finalDistanceMeters",
        "pathLengthMeters",
        "minObstacleClearanceMeters",
        "jitterEvents",
        "sdfPushoutEvents",
        "meanCurrentTargetAlignment",
        "meanPositiveFlowUse",
        "score",
    ):
        add_binary_record(records, f"smoothedMetrics.{key}", SECTION_SMOOTHED_METRICS, RECORD_KIND_FLOAT32, smoothed[key])

    for key in ("steps", "finalDistanceMeters", "jitterEvents", "sdfPushoutEvents"):
        add_binary_record(records, f"rawNoSmoothingMetrics.{key}", SECTION_RAW_METRICS, RECORD_KIND_FLOAT32, raw[key])

    add_binary_record(records, "idleDriftVerification.duration_seconds", SECTION_IDLE, RECORD_KIND_SECONDS, idle["duration_seconds"])
    add_binary_record(records, "idleDriftVerification.drift_distance_meters", SECTION_IDLE, RECORD_KIND_METERS, idle["drift_distance_meters"])
    add_binary_record(records, "idleDriftVerification.mean_velocity_flow_alignment01", SECTION_IDLE, RECORD_KIND_FLOAT32, idle["mean_velocity_flow_alignment01"])

    for key in (
        "predators",
        "cadenceHz",
        "samplesPerSecond",
        "samplesPerFrameAt60Hz",
        "estimatedScalarOpsPerSampleLow",
        "estimatedScalarOpsPerSampleHigh",
        "estimatedScalarOpsPerFrameLow",
        "estimatedScalarOpsPerFrameHigh",
    ):
        add_binary_record(records, f"performanceModel.{key}", SECTION_PERFORMANCE, RECORD_KIND_FLOAT32, performance[key])

    for key in (
        "flowSamplesPerSolve",
        "sdfProxyChecksPerSolve",
        "normalizationsPerSolve",
        "sqrtOrRsqrtSitesPerSolve",
        "branchClampSitesPerSolve",
    ):
        add_binary_record(records, f"sampleCostModel.{key}", SECTION_COST, RECORD_KIND_UINT16, cost[key])

    for tier_name in ("Low", "Middle", "High", "Ultra"):
        tier = tiers[tier_name]
        weights = tier["weights"]
        hysteresis = tier["hysteresis"]
        prefix = f"tierProfiles.{tier_name}"
        add_binary_record(records, f"{prefix}.cadenceHz", SECTION_TIER, RECORD_KIND_RATE, tier["cadenceHz"])
        add_binary_record(records, f"{prefix}.hysteresis.distanceMeters", SECTION_TIER, RECORD_KIND_METERS, hysteresis["distanceMeters"])
        add_binary_record(records, f"{prefix}.hysteresis.timeSeconds", SECTION_TIER, RECORD_KIND_SECONDS, hysteresis["timeSeconds"])
        add_binary_record(records, f"{prefix}.weights.targetAttractionWeight", SECTION_TIER, RECORD_KIND_FLOAT32, weights["targetAttractionWeight"])
        add_binary_record(records, f"{prefix}.weights.flowAlignmentBoostWeight", SECTION_TIER, RECORD_KIND_FLOAT32, weights["flowAlignmentBoostWeight"])
        add_binary_record(records, f"{prefix}.weights.ewmaSteeringAlpha", SECTION_TIER, RECORD_KIND_FLOAT32, weights["ewmaSteeringAlpha"])

    add_binary_record(records, "tierProfiles.Low.weights.maxRepulsionAcceleration", SECTION_TIER, RECORD_KIND_FLOAT32, tiers["Low"]["weights"]["maxRepulsionAcceleration"])
    add_binary_record(records, "tierProfiles.Ultra.weights.maxRepulsionAcceleration", SECTION_TIER, RECORD_KIND_FLOAT32, tiers["Ultra"]["weights"]["maxRepulsionAcceleration"])
    return records


def sorted_binary_records(records: list[BinaryRecord]) -> list[tuple[int, BinaryRecord]]:
    keyed = [(fnv1a32(record.label), record) for record in records]
    keyed.sort(key=lambda item: (item[0], item[1].label))
    return keyed


def build_binary_blob(data: dict) -> tuple[bytes, list[tuple[int, BinaryRecord]], int, int]:
    records = collect_binary_records(data)
    keyed_records = sorted_binary_records(records)
    payload = bytearray()
    for semantic_hash, record in keyed_records:
        payload.extend(
            struct.pack(
                BINARY_RECORD_FORMAT,
                semantic_hash,
                record.section_id,
                record.kind,
                float(record.value),
                record.flags,
            )
        )

    payload_crc32 = zlib.crc32(payload) & UINT32_MASK
    semantic_bytes = b"\0".join(record.label.lower().encode("ascii") for _, record in keyed_records)
    semantic_hash_crc32 = zlib.crc32(semantic_bytes) & UINT32_MASK
    file_bytes = BINARY_HEADER_SIZE + len(payload)
    header = struct.pack(
        BINARY_HEADER_FORMAT,
        BINARY_MAGIC,
        BINARY_VERSION,
        BINARY_HEADER_SIZE,
        file_bytes,
        len(keyed_records),
        BINARY_RECORD_SIZE,
        BINARY_HEADER_SIZE,
        len(payload),
        payload_crc32,
        semantic_hash_crc32,
        BINARY_FLAG_LITTLE_ENDIAN,
        bytes(24),
    )
    blob = header + bytes(payload)
    if len(blob) % BINARY_ALIGNMENT != 0:
        raise ValueError("AI navigation binary is not 16-byte aligned")
    return blob, keyed_records, payload_crc32, semantic_hash_crc32


def binary_sections(record_count: int) -> dict:
    return {
        "Header": {
            "offsetBytes": 0,
            "sizeBytes": BINARY_HEADER_SIZE,
            "alignmentBytes": BINARY_ALIGNMENT,
        },
        "RecordPayload": {
            "offsetBytes": BINARY_HEADER_SIZE,
            "sizeBytes": record_count * BINARY_RECORD_SIZE,
            "alignmentBytes": BINARY_ALIGNMENT,
            "recordCount": record_count,
            "recordStrideBytes": BINARY_RECORD_SIZE,
        },
    }


def binary_hash_audit(keyed_records: list[tuple[int, BinaryRecord]]) -> dict:
    hashes = [semantic_hash for semantic_hash, _ in keyed_records]
    labels = [record.label for _, record in keyed_records]
    return {
        "algorithm": "FNV-1a32 ASCII-lower semantic IDs",
        "collisionCount": len(hashes) - len(set(hashes)),
        "recordCount": len(keyed_records),
        "sortedHashes": hashes == sorted(hashes),
        "semanticIdsUnique": len(labels) == len(set(labels)),
    }


def build_binary_cache_info(data: dict) -> tuple[bytes, dict, list[tuple[int, BinaryRecord]]]:
    blob, keyed_records, payload_crc32, semantic_hash_crc32 = build_binary_blob(data)
    header_crc32 = zlib.crc32(blob[:BINARY_HEADER_SIZE]) & UINT32_MASK
    header_values = struct.unpack(BINARY_HEADER_FORMAT, blob[:BINARY_HEADER_SIZE])
    cache = {
        "path": BINARY_CACHE_PATH.as_posix(),
        "manifestPath": BINARY_MANIFEST_PATH.as_posix(),
        "magic": BINARY_MAGIC.decode("ascii"),
        "version": BINARY_VERSION,
        "endianness": "little",
        "alignmentBytes": BINARY_ALIGNMENT,
        "headerBytes": BINARY_HEADER_SIZE,
        "fileBytes": len(blob),
        "recordCount": len(keyed_records),
        "recordStrideBytes": BINARY_RECORD_SIZE,
        "payloadOffsetBytes": BINARY_HEADER_SIZE,
        "payloadBytes": len(blob) - BINARY_HEADER_SIZE,
        "headerStruct": BINARY_HEADER_FORMAT,
        "recordStruct": BINARY_RECORD_FORMAT,
        "sections": binary_sections(len(keyed_records)),
        "headerAudit": {
            "headerCrc32": f"0x{header_crc32:08X}",
            "payloadCrc32": f"0x{payload_crc32:08X}",
            "semanticHashCrc32": f"0x{semantic_hash_crc32:08X}",
            "reservedZero": header_values[-1] == bytes(24),
            "flags": header_values[-2],
        },
        "hashAudit": binary_hash_audit(keyed_records),
        "sha256": sha256_hex(blob),
        "runtimeLookupContract": DATA_SOVEREIGNTY_AUDIT["runtimeLookupContract"],
    }
    return blob, cache, keyed_records


def build_standalone_manifest(data: dict, json_bytes: bytes, blob: bytes, cache: dict, keyed_records: list[tuple[int, BinaryRecord]]) -> dict:
    return {
        "schema": "H8.NavigationTuning.BinaryManifest.v1",
        "status": "NAVIGATION_BINARY_VERIFIED",
        "promptId": "AI_POTENTIAL_FIELD_NAVIGATOR",
        "sourceJson": "Data/AI/Navigation_Tuning.json",
        "sourceJsonSha256": sha256_hex(json_bytes),
        "runtimeContract": DATA_SOVEREIGNTY_AUDIT["runtimeLookupContract"],
        "atlasDomains": [19, 25],
        "binary": cache,
        "hashAudit": binary_hash_audit(keyed_records),
        "toasterData": TOASTER_DATA,
        "rtxOverkillData": RTX_OVERKILL_DATA,
        "dataSovereigntyAudit": DATA_SOVEREIGNTY_AUDIT,
        "binarySha256": sha256_hex(blob),
    }


def validate_binary_cache(data: dict, errors: list[str]) -> None:
    try:
        expected_blob, expected_cache, keyed_records = build_binary_cache_info(data)
    except (KeyError, TypeError, ValueError, struct.error) as exc:
        errors.append(f"binary cache build failed: {exc}")
        return

    if expected_cache.get("recordCount") != 76:
        errors.append(f"binary record count mismatch: {expected_cache.get('recordCount')}")
    if expected_cache["hashAudit"]["collisionCount"] != 0:
        errors.append("binary FNV collision detected")
    if not expected_cache["hashAudit"]["sortedHashes"]:
        errors.append("binary hashes are not sorted")
    if not expected_cache["hashAudit"]["semanticIdsUnique"]:
        errors.append("binary semantic ids are not unique")
    if not expected_cache["headerAudit"]["reservedZero"]:
        errors.append("binary reserved header bytes are not zero")

    exported_cache = data.get("binaryCache")
    if exported_cache != expected_cache:
        errors.append("binaryCache metadata mismatch")

    if not BINARY_CACHE_PATH.exists():
        errors.append(f"missing binary cache: {BINARY_CACHE_PATH.as_posix()}")
        return
    try:
        actual_blob = BINARY_CACHE_PATH.read_bytes()
    except OSError as exc:
        errors.append(f"cannot read binary cache: {exc}")
        return

    if len(actual_blob) % BINARY_ALIGNMENT != 0:
        errors.append("binary cache file is not 16-byte aligned")
    if actual_blob != expected_blob:
        errors.append("binary cache bytes do not match regenerated payload")

    if not BINARY_MANIFEST_PATH.exists():
        errors.append(f"missing binary manifest: {BINARY_MANIFEST_PATH.as_posix()}")
        return
    try:
        manifest = json.loads(BINARY_MANIFEST_PATH.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        errors.append(f"cannot read binary manifest: {exc}")
        return
    expected_manifest = build_standalone_manifest(
        data,
        canonical_json_bytes(data),
        expected_blob,
        expected_cache,
        keyed_records,
    )
    if manifest != expected_manifest:
        errors.append("binary manifest mismatch")


def validate_export(data: object) -> Tuple[bool, list[str]]:
    errors: list[str] = []

    def expect(condition: bool, message: str) -> None:
        if not condition:
            errors.append(message)

    if not isinstance(data, dict):
        return False, ["export root must be a JSON object"]

    expect(data.get("schema") == "H8.NavigationTuning.PotentialField.v1", "schema mismatch")
    expect(data.get("status") == "NAVIGATION OPTIMIZED", "status is not NAVIGATION OPTIMIZED")
    expect(data.get("evidenceClass") == "PY_SIM_STATIC_MODEL", "evidence class mismatch")
    expect(data.get("promptId") == "AI_POTENTIAL_FIELD_NAVIGATOR", "promptId mismatch")
    expect(data.get("sourceContracts") == SOURCE_CONTRACTS, "source contracts mismatch")
    expect(data.get("formula") == FORMULA_CONTRACT, "formula contract mismatch")
    math_audit = data.get("mathAudit")
    if not isinstance(math_audit, dict):
        errors.append("missing mathAudit")
    else:
        expect(math_audit.get("hardScience") == HARD_SCIENCE_AUDIT, "mathAudit hard-science contract mismatch")
        expect(math_audit.get("simulationConstants") == SIMULATION_CONSTANT_AUDIT, "mathAudit simulation constants mismatch")

    expect(data.get("dataSovereigntyAudit") == DATA_SOVEREIGNTY_AUDIT, "data sovereignty audit mismatch")
    expect(data.get("toasterData") == TOASTER_DATA, "toaster data mismatch")
    expect(data.get("rtxOverkillData") == RTX_OVERKILL_DATA, "RTX overkill data mismatch")

    snapshot = data.get("sourceParameterSnapshot")
    if not isinstance(snapshot, dict):
        errors.append("missing sourceParameterSnapshot")
    else:
        for key, expected in SOURCE_PARAMETER_SNAPSHOT.items():
            actual = snapshot.get(key)
            expect(actual == expected, f"source parameter mismatch: {key}")
        valid_source, source_errors = validate_source_constants(snapshot)
        if not valid_source:
            errors.extend(source_errors)

    try:
        weights = weights_from_dict(data["selectedWeights"])
    except (KeyError, TypeError, ValueError) as exc:
        errors.append(f"invalid selectedWeights: {exc}")
        return False, errors

    for name, value in weights_to_dict(weights).items():
        expect(math.isfinite(float(value)), f"non-finite weight: {name}")

    expected_weights, replay, raw, evaluated, reached_count = find_best_candidate()
    expect(weights_to_dict(weights) == weights_to_dict(expected_weights), "selected weights are not deterministic best")
    expect(replay.reached, "selected weights do not reach target")
    expect(replay.sdf_pushout_events == 0, "selected weights require SDF pushout")
    expect(replay.min_obstacle_distance >= MIN_REPLAY_CLEARANCE_GUARD_METERS, "selected weights violate 2m clearance guard")
    expect(replay.jitter_events <= 1, "selected weights exceed jitter guard")
    expect(replay.jitter_events <= raw.jitter_events, "EWMA increases jitter")
    expect(data.get("smoothedMetrics") == result_to_dict(replay), "smoothed metrics do not match replay")
    expect(data.get("rawNoSmoothingMetrics") == result_to_dict(raw), "raw metrics do not match replay")

    drift = idle_drift(weights)
    expect(drift["drift_distance_meters"] > IDLE_DRIFT_MIN_DISTANCE_METERS, "idle drift distance too low")
    expect(drift["mean_velocity_flow_alignment01"] > IDLE_DRIFT_MIN_ALIGNMENT01, "idle drift does not follow current")
    expect(data.get("idleDriftVerification") == drift, "idle drift metrics do not match replay")
    expected_trace = trace_path(weights, use_smoothing=True)
    exported_trace = data.get("pathTrace")
    expect(exported_trace == expected_trace, "path trace does not match replay")
    if isinstance(exported_trace, dict):
        samples = exported_trace.get("samples")
        if not isinstance(samples, list) or len(samples) < 5:
            errors.append("path trace has too few samples")
        else:
            expect(samples[0].get("step") == 0, "path trace missing start sample")
            expect(exported_trace.get("reached") is True, "path trace does not reach target")
            expect(exported_trace.get("sdfPushoutEvents") == 0, "path trace requires SDF pushout")
            trace_clearance = finite_float(exported_trace.get("minObstacleClearanceMeters"))
            expect(
                trace_clearance is not None and trace_clearance >= MIN_REPLAY_CLEARANCE_GUARD_METERS,
                "path trace clearance below guard",
            )

    search = data.get("search")
    if not isinstance(search, dict):
        errors.append("missing search metrics")
    else:
        expect(search.get("candidatesEvaluated") == evaluated, "candidate evaluation count mismatch")
        expect(search.get("candidatesReachedTarget") == reached_count, "candidate reach count mismatch")
        expect(search.get("dtSeconds") == DT, "search dt mismatch")
        expect(search.get("maxSteps") == MAX_STEPS, "search max step mismatch")

    perf = data.get("performanceModel")
    if not isinstance(perf, dict):
        errors.append("missing performanceModel")
    else:
        expect(perf.get("predators") == 100, "performance predator count mismatch")
        expect(perf.get("cadenceHz") == 10, "performance cadence mismatch")
        expect(perf.get("samplesPerSecond") == 1000, "performance samples/sec mismatch")
        expect(perf.get("samplesPerFrameAt60Hz") == 16.6667, "performance samples/frame mismatch")

    cost = data.get("sampleCostModel")
    if not isinstance(cost, dict):
        errors.append("missing sampleCostModel")
    else:
        expect(cost == deterministic_sample_cost_model(), "sample cost model mismatch")

    validate_binary_cache(data, errors)

    telemetry = data.get("blackBoxTelemetry")
    if not isinstance(telemetry, dict):
        errors.append("missing blackBoxTelemetry")
    else:
        expect(telemetry.get("capacityFrames") == 300, "blackbox capacity mismatch")
        expect(
            telemetry.get("dumpPath") == "Docs/AgentLogs/Dump_AI_POTENTIAL_FIELD_NAVIGATOR.bin",
            "blackbox dump path mismatch",
        )
        fields = telemetry.get("fields")
        if not isinstance(fields, list):
            errors.append("blackbox fields missing")
        else:
            field_names = {field.get("name") for field in fields if isinstance(field, dict)}
            required = {
                "frameIndex",
                "entityId",
                "positionAupCell",
                "positionLocalMeters",
                "velocityMetersPerSecond",
                "targetDistanceMeters",
                "flowAlignmentSigned",
                "sdfClearanceMeters",
                "stateFlags",
                "stateHash",
            }
            missing = required.difference(field_names)
            if missing:
                errors.append("blackbox fields incomplete")

        guards = telemetry.get("finiteGuards")
        if not isinstance(guards, list) or "sdfClearanceMeters" not in guards:
            errors.append("blackbox finite guards incomplete")

    tiers = data.get("tierProfiles")
    if not isinstance(tiers, dict):
        errors.append("missing tierProfiles")
    else:
        for tier_name in ("Low", "Middle", "High", "Ultra"):
            tier = tiers.get(tier_name)
            if not isinstance(tier, dict):
                errors.append(f"missing tier profile: {tier_name}")
                continue
            hysteresis = tier.get("hysteresis")
            if not isinstance(hysteresis, dict):
                errors.append(f"missing hysteresis: {tier_name}")
                continue
            distance = finite_float(hysteresis.get("distanceMeters"))
            seconds = finite_float(hysteresis.get("timeSeconds"))
            expect(distance is not None and 3.0 <= distance <= 5.0, f"hysteresis distance out of mandate range: {tier_name}")
            expect(seconds is not None and 2.0 <= seconds <= 3.0, f"hysteresis time out of mandate range: {tier_name}")

    return len(errors) == 0, errors


def check_export(output_path: Path) -> int:
    if not output_path.exists():
        print(f"CHECK FAILED: missing {output_path.as_posix()}", file=sys.stderr)
        return 1

    try:
        data = json.loads(output_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        print("CHECK FAILED")
        print(f"- invalid JSON export: {exc}")
        return 1

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
            "hysteresis": TIER_HYSTERESIS,
        },
        "Middle": {
            "weights": weights_to_dict(middle),
            "mathLod": "one flow sample, obstacle loop capped to local SDF proxies, EWMA final steering",
            "cadenceHz": 10,
            "hysteresis": TIER_HYSTERESIS,
        },
        "High": {
            "weights": weights_to_dict(high),
            "mathLod": "flow alignment plus richer local SDF gradient, tighter smoothing for pursuit",
            "cadenceHz": 15,
            "hysteresis": TIER_HYSTERESIS,
        },
        "Ultra": {
            "weights": weights_to_dict(ultra),
            "mathLod": "adds local vortex interest and visual-overkill path curvature while retaining O(1) steering",
            "cadenceHz": 20,
            "hysteresis": TIER_HYSTERESIS,
        },
    }


def main(argv: Sequence[str] | None = None) -> int:
    args = tuple(sys.argv[1:] if argv is None else argv)
    output_path = Path("Data/AI/Navigation_Tuning.json")
    if args and args[0] == "--check":
        if len(args) > 2:
            print("Usage: Tools/AiPathSim.py [--check [path]]", file=sys.stderr)
            return 2
        return check_export(output_path if len(args) == 1 else Path(args[1]))
    if args:
        print("Usage: Tools/AiPathSim.py [--check [path]]", file=sys.stderr)
        return 2

    best_weights, best_result, best_raw, evaluated, reached_count = find_best_candidate()

    output = {
        "schema": "H8.NavigationTuning.PotentialField.v1",
        "status": "NAVIGATION OPTIMIZED",
        "evidenceClass": "PY_SIM_STATIC_MODEL",
        "promptId": "AI_POTENTIAL_FIELD_NAVIGATOR",
        "sourceContracts": SOURCE_CONTRACTS,
        "sourceParameterSnapshot": SOURCE_PARAMETER_SNAPSHOT,
        "formula": FORMULA_CONTRACT,
        "mathAudit": {
            "hardScience": HARD_SCIENCE_AUDIT,
            "simulationConstants": SIMULATION_CONSTANT_AUDIT,
        },
        "dataSovereigntyAudit": DATA_SOVEREIGNTY_AUDIT,
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
        "pathTrace": trace_path(best_weights, use_smoothing=True),
        "performanceModel": performance_model(),
        "sampleCostModel": deterministic_sample_cost_model(),
        "blackBoxTelemetry": BLACK_BOX_TELEMETRY_SCHEMA,
        "tierProfiles": build_tiers(best_weights),
        "toasterData": TOASTER_DATA,
        "rtxOverkillData": RTX_OVERKILL_DATA,
        "failureModes": [
            "No finite flow sample: use target attraction plus SDF repulsion only.",
            "Inside solid or negative SDF clearance: override with strongest positive SDF gradient and suppress attack thrust.",
            "JitterEvents rising over 3-frame window: increase EWMA alpha toward Low tier and cap flow boost.",
            "Frame budget pressure: reduce cadence to 5Hz and share flow samples by 16m goal cluster.",
        ],
    }

    output_path.parent.mkdir(parents=True, exist_ok=True)
    binary_blob, binary_cache, keyed_records = build_binary_cache_info(output)
    output["binaryCache"] = binary_cache
    output_bytes = canonical_json_bytes(output)
    output_path.write_bytes(output_bytes)

    manifest = build_standalone_manifest(output, output_bytes, binary_blob, binary_cache, keyed_records)
    BINARY_CACHE_PATH.parent.mkdir(parents=True, exist_ok=True)
    BINARY_CACHE_PATH.write_bytes(binary_blob)
    BINARY_MANIFEST_PATH.write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")

    print("NAVIGATION OPTIMIZED")
    print(f"candidates={evaluated} reached={reached_count}")
    print(f"raw_jitter={best_raw.jitter_events} smoothed_jitter={best_result.jitter_events}")
    print(f"smoothed_seconds={best_result.steps * DT:.2f} final_distance={best_result.final_distance:.3f}")
    print(f"binary={BINARY_CACHE_PATH.as_posix()} bytes={len(binary_blob)} records={binary_cache['recordCount']}")
    print(f"manifest={BINARY_MANIFEST_PATH.as_posix()}")
    print(f"output={output_path.as_posix()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
