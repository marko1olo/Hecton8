#!/usr/bin/env python3
"""Deterministic offline battle simulator for the Alpha Leviathan utility brain.

Python/data only. No Unity scene, prefab, project setting, or C# public API mutation.
"""

from __future__ import annotations

import argparse
import json
import math
import re
import statistics
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Iterable, List, Mapping, MutableMapping, Sequence, Tuple


DEFAULT_SEED = 0x48384C5649425241
DEFAULT_ENCOUNTERS = 10_000
MAX_SECONDS = 180
TIME_STEP_SECONDS = 2
EXPECTED_STATUS = "INSTINCTS DEFINED"
EXPECTED_UTILITY_SCORE_COUNT = 50
EXPECTED_BEHAVIORS = ("Circle", "Hide", "Breach", "FalseCharge", "RealAttack")
EPSILON = 1.0e-9
BUFFER_SOURCE_RELATIVE_PATH = Path("Assets/_Project/Scripts/Core/Memory/H8Memory.cs")
FALLBACK_GLOBAL_DATA_VAULT_BUFFERS = {
    "Unknown",
    "Silt",
    "RigidbodyAUPs",
    "RigidbodyCullingState",
    "RigidbodyAwakeResults",
    "RigidbodyCullingCommands",
    "RigidbodyDistanceSq",
    "PhysicsCullingTelemetry",
    "DispatcherRaycastHits",
    "H8Time",
    "TerrainSeamHeightmap",
    "PlayerKinematicState",
    "RoomWaterLevels",
    "EntityAUPs",
    "VoxelSdfTexture3D",
    "RoomVolumes",
    "RoomLocalAUPs",
    "OceanGerstnerWaves",
    "OceanGerstnerWaveMeta",
    "WfcOutpostGrid",
    "LoreEntityAUPs",
    "LoreEntityHashes",
    "SubmarineBallastFill01",
    "SubmarineBallastTankLocalPositions",
    "SubmarineBallastPidOutput",
    "SubmarineDynamicFloodMassOutput",
    "SubmarinePidTelemetry",
    "CarveDebris",
    "CarveDebrisVelocity",
    "EntityFlags",
    "EntityVelocities",
    "EntityItemHashes",
    "EntityQuantities",
    "EntityLootMagnetTelemetry",
}


@dataclass(frozen=True)
class PlayerProfile:
    name: str
    movement: float
    sound: float
    light: float
    discipline: float
    dodge: float
    decoy: float


@dataclass
class EncounterState:
    distance: float
    player_hp: float
    terror: float
    attack_cooldown: float
    false_charge_cooldown: float
    hysteresis: float
    current_behavior: str
    elapsed: int
    rng: int


@dataclass(frozen=True)
class EncounterResult:
    index: int
    profile: str
    tier: str
    pack_count: int
    killed: bool
    escaped: bool
    timeout: bool
    kill_time: int
    final_hp: float
    max_terror: float
    mean_terror: float
    behavior_counts: Mapping[str, int]
    context_counts: Mapping[str, int]


PLAYER_PROFILES = (
    PlayerProfile("quiet_drift", 0.18, 0.10, 0.05, 0.92, 0.62, 0.00),
    PlayerProfile("panic_swim", 0.86, 0.72, 0.08, 0.28, 0.28, 0.00),
    PlayerProfile("flashlight_track", 0.42, 0.30, 0.88, 0.56, 0.44, 0.00),
    PlayerProfile("decoy_ping", 0.36, 0.64, 0.22, 0.68, 0.58, 0.70),
    PlayerProfile("engine_sprint", 0.94, 0.86, 0.18, 0.34, 0.34, 0.00),
    PlayerProfile("disciplined_escape", 0.58, 0.34, 0.18, 0.84, 0.66, 0.10),
)
TIERS = ("low", "middle", "high", "ultra")
PACK_DIRECTIONS = (
    (1.0, 0.0),
    (0.70710678, 0.70710678),
    (0.0, 1.0),
    (-0.70710678, 0.70710678),
    (-1.0, 0.0),
    (-0.70710678, -0.70710678),
    (0.0, -1.0),
    (0.70710678, -0.70710678),
)


def clamp01(value: float) -> float:
    if value <= 0.0:
        return 0.0
    if value >= 1.0:
        return 1.0
    return value


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


def rsqrt(value: float) -> float:
    return 1.0 / math.sqrt(max(value, EPSILON))


def normalized_dot_2d(ax: float, az: float, bx: float, bz: float) -> float:
    inv_a = rsqrt(ax * ax + az * az)
    inv_b = rsqrt(bx * bx + bz * bz)
    return (ax * inv_a * bx * inv_b) + (az * inv_a * bz * inv_b)


def find_nonfinite(value: object, path: str, errors: List[str]) -> None:
    if isinstance(value, float):
        if not math.isfinite(value):
            errors.append(f"nonfinite number at {path}")
    elif isinstance(value, dict):
        for key, child in value.items():
            find_nonfinite(child, f"{path}.{key}", errors)
    elif isinstance(value, list):
        for index, child in enumerate(value):
            find_nonfinite(child, f"{path}[{index}]", errors)


def load_json(path: Path) -> MutableMapping[str, object]:
    with path.open("r", encoding="utf-8") as stream:
        data = json.load(stream)
    if not isinstance(data, dict):
        raise ValueError(f"{path} root is not an object")
    return data


def write_json(path: Path, payload: Mapping[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def project_root() -> Path:
    return Path(__file__).resolve().parents[1]


def parse_buffer_ids_from_source(source_path: Path) -> Tuple[set[str], str]:
    try:
        text = source_path.read_text(encoding="utf-8")
    except OSError:
        return set(FALLBACK_GLOBAL_DATA_VAULT_BUFFERS), "FALLBACK_SOURCE_MISSING"

    match = re.search(r"public\s+enum\s+BufferID\s*:\s*int\s*\{(?P<body>.*?)\n\s*\}", text, re.DOTALL)
    if match is None:
        return set(FALLBACK_GLOBAL_DATA_VAULT_BUFFERS), "FALLBACK_ENUM_NOT_FOUND"

    names: set[str] = set()
    for raw_line in match.group("body").splitlines():
        line = raw_line.split("//", 1)[0].strip()
        if not line:
            continue
        name = line.split("=", 1)[0].split(",", 1)[0].strip()
        if re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", name):
            names.add(name)

    if not names:
        return set(FALLBACK_GLOBAL_DATA_VAULT_BUFFERS), "FALLBACK_ENUM_EMPTY"
    return names, "SOURCE_PARSED"


def load_known_global_data_vault_buffers() -> Tuple[set[str], str, str]:
    source_path = project_root() / BUFFER_SOURCE_RELATIVE_PATH
    buffers, status = parse_buffer_ids_from_source(source_path)
    return buffers, str(BUFFER_SOURCE_RELATIVE_PATH).replace("\\", "/"), status


def validate_brain(brain: Mapping[str, object], known_buffers: set[str] | None = None) -> Dict[str, object]:
    errors: List[str] = []
    if known_buffers is None:
        known_buffers, buffer_source, buffer_source_status = load_known_global_data_vault_buffers()
    else:
        buffer_source = str(BUFFER_SOURCE_RELATIVE_PATH).replace("\\", "/")
        buffer_source_status = "CALLER_PROVIDED"
    find_nonfinite(brain, "brain", errors)

    if brain.get("schemaVersion") != 1:
        errors.append("schemaVersion != 1")
    if brain.get("status") != EXPECTED_STATUS:
        errors.append(f"status != {EXPECTED_STATUS}")

    behavior_order = brain.get("behaviorOrder")
    if tuple(behavior_order) != EXPECTED_BEHAVIORS:
        errors.append("behaviorOrder drift")

    feed_rows = brain.get("globalDataVaultFeeds")
    feature_names = set()
    if not isinstance(feed_rows, list) or not feed_rows:
        errors.append("globalDataVaultFeeds missing")
    else:
        for index, row in enumerate(feed_rows):
            if not isinstance(row, dict):
                errors.append(f"globalDataVaultFeeds[{index}] is not an object")
                continue
            feature = row.get("feature")
            if not isinstance(feature, str) or not feature:
                errors.append(f"globalDataVaultFeeds[{index}].feature invalid")
            else:
                feature_names.add(feature)
            buffer_ids = row.get("bufferIds")
            if not isinstance(buffer_ids, list) or not buffer_ids:
                errors.append(f"globalDataVaultFeeds[{index}].bufferIds missing")
                continue
            for buffer_id in buffer_ids:
                if buffer_id not in known_buffers:
                    errors.append(f"unknown GlobalDataVault BufferID {buffer_id}")

    contexts = brain.get("contexts")
    context_names = set()
    if not isinstance(contexts, list):
        errors.append("contexts missing")
    else:
        for context in contexts:
            if isinstance(context, dict) and isinstance(context.get("id"), str):
                context_names.add(str(context["id"]))

    utility_scores = brain.get("utilityScores")
    behavior_counts = {behavior: 0 for behavior in EXPECTED_BEHAVIORS}
    if not isinstance(utility_scores, list):
        errors.append("utilityScores missing")
    else:
        if len(utility_scores) != EXPECTED_UTILITY_SCORE_COUNT:
            errors.append(f"utilityScores count != {EXPECTED_UTILITY_SCORE_COUNT}")
        seen_ids = set()
        for index, row in enumerate(utility_scores):
            if not isinstance(row, dict):
                errors.append(f"utilityScores[{index}] is not an object")
                continue
            row_id = row.get("id")
            if not isinstance(row_id, int) or row_id in seen_ids:
                errors.append(f"utilityScores[{index}].id invalid")
            seen_ids.add(row_id)
            behavior = row.get("behavior")
            if behavior not in EXPECTED_BEHAVIORS:
                errors.append(f"utilityScores[{index}].behavior invalid")
            else:
                behavior_counts[str(behavior)] += 1
            context = row.get("context")
            if context not in context_names:
                errors.append(f"utilityScores[{index}].context invalid")
            score = row.get("score")
            if not isinstance(score, (int, float)) or isinstance(score, bool) or score < 0.0 or score > 1.25:
                errors.append(f"utilityScores[{index}].score out of range")
            inputs = row.get("inputs")
            if not isinstance(inputs, list) or not inputs:
                errors.append(f"utilityScores[{index}].inputs missing")
            else:
                for item in inputs:
                    if item not in feature_names:
                        errors.append(f"utilityScores[{index}] input {item} has no GlobalDataVault feed")

    for behavior, count in behavior_counts.items():
        if count != 10:
            errors.append(f"{behavior} utility row count != 10")

    sensory = brain.get("sensoryWeights")
    if isinstance(sensory, dict):
        total = 0.0
        for key in ("sound", "light", "movement"):
            value = sensory.get(key)
            if not isinstance(value, (int, float)) or isinstance(value, bool):
                errors.append(f"sensoryWeights.{key} invalid")
            else:
                total += float(value)
        if abs(total - 1.0) > 0.0001:
            errors.append("sensoryWeights sound/light/movement sum != 1")
    else:
        errors.append("sensoryWeights missing")

    return {
        "status": "BRAIN_VALIDATION_PASSED" if not errors else "BRAIN_VALIDATION_FAILED",
        "errors": errors,
        "utilityScoreCount": len(utility_scores) if isinstance(utility_scores, list) else 0,
        "behaviorCounts": behavior_counts,
        "globalDataVaultFeedCount": len(feature_names),
        "knownBufferCount": len(known_buffers),
        "knownBufferSource": buffer_source,
        "knownBufferSourceStatus": buffer_source_status,
    }


def build_score_lookup(brain: Mapping[str, object]) -> Dict[Tuple[str, str], float]:
    lookup: Dict[Tuple[str, str], float] = {}
    rows = brain["utilityScores"]
    assert isinstance(rows, list)
    for row in rows:
        assert isinstance(row, dict)
        lookup[(str(row["context"]), str(row["behavior"]))] = float(row["score"])
    return lookup


def behavior_parameters(brain: Mapping[str, object], behavior: str) -> Mapping[str, object]:
    params = brain["behaviorParameters"]
    assert isinstance(params, dict)
    row = params[behavior]
    assert isinstance(row, dict)
    return row


def resolve_context(distance: float, sound: float, light: float, movement: float, pack: float, cooldown: float, tier: str) -> str:
    if cooldown > 0.0:
        return "post_strike_cooldown"
    signal = max(sound, light, movement)
    if tier == "ultra" and distance < 125.0 and signal > 0.66 and pack > 0.25:
        return "visual_overkill_ultra"
    if pack > 0.64 and distance < 135.0:
        return "pack_flank_ready"
    if distance > 120.0:
        return "far_lit_scan" if light > 0.44 else "far_silent_dark"
    if distance > 70.0:
        return "mid_panic_movement" if movement > 0.45 or sound > 0.55 else "mid_soft_noise"
    if light > 0.55:
        return "close_light_stare"
    if sound > 0.62 or movement > 0.64:
        return "close_loud_engine"
    return "close_quiet_drift"


def profile_for(index: int, seed: int) -> PlayerProfile:
    choice = stable_seed(seed, index, 0x50524F46) % len(PLAYER_PROFILES)
    return PLAYER_PROFILES[choice]


def tier_for(index: int, seed: int) -> str:
    choice = stable_seed(seed, index, 0x54494552) % len(TIERS)
    return TIERS[choice]


def pack_count_for(index: int, seed: int) -> int:
    value = stable_seed(seed, index, 0x5041434B) & 0xFF
    if value < 86:
        return 0
    if value < 176:
        return 1
    if value < 232:
        return 2
    return 3


def compute_pack_synergy(index: int, elapsed: int, pack_count: int, player_forward_x: float, player_forward_z: float, seed: int) -> float:
    if pack_count <= 0:
        return 0.0

    best = 0.0
    for slot in range(pack_count):
        phase_seed = stable_seed(seed, index, elapsed // 5, slot, 0x464C414E4B)
        direction = PACK_DIRECTIONS[phase_seed & 7]
        radius = 18.0 + ((phase_seed >> 10) & 31)
        stalker_x = direction[0] * radius
        stalker_z = direction[1] * radius
        velocity_x = -stalker_z
        velocity_z = stalker_x
        rear_dot = max(0.0, normalized_dot_2d(stalker_x, stalker_z, -player_forward_x, -player_forward_z))
        lane_dot = max(0.0, normalized_dot_2d(velocity_x, velocity_z, player_forward_x, player_forward_z))
        range_gate = 1.0 if 12.0 <= radius <= 42.0 else 0.0
        best = max(best, rear_dot * lane_dot * range_gate)

    if pack_count >= 2:
        spread_seed = stable_seed(seed, index, elapsed // 7, 0x535052454144)
        if (spread_seed & 7) >= 5:
            best = min(1.0, best + 0.18)
    return clamp01(best)


def compute_signals(profile: PlayerProfile, state: EncounterState, index: int, seed: int) -> Tuple[float, float, float, float]:
    state.rng, pulse_a = next_rng(state.rng)
    state.rng, pulse_b = next_rng(state.rng)
    terror_noise = clamp01(state.terror * 0.42)
    discipline_damp = 0.32 * profile.discipline
    movement = clamp01(profile.movement + pulse_a * 0.12 + terror_noise - discipline_damp * 0.18)
    sound = clamp01(profile.sound + movement * 0.38 + profile.decoy * 0.22 + pulse_b * 0.08)
    light = clamp01(profile.light + (0.10 if profile.name == "flashlight_track" and (state.elapsed % 11) < 6 else 0.0))
    silt = 0.20 + ((stable_seed(seed, index, 0x53494C54) & 255) * (0.45 / 255.0))
    line_of_sight = clamp01(0.92 - silt + light * 0.18 - state.terror * 0.05)
    return sound, light, movement, line_of_sight


def score_behaviors(
    brain: Mapping[str, object],
    lookup: Mapping[Tuple[str, str], float],
    context: str,
    state: EncounterState,
    sound: float,
    light: float,
    movement: float,
    line_of_sight: float,
    pack: float,
    aggression_scalar: float,
) -> Dict[str, float]:
    sensory = brain["sensoryWeights"]
    assert isinstance(sensory, dict)
    weighted_aggression = clamp01(
        (sound * float(sensory["sound"]) + light * float(sensory["light"]) + movement * float(sensory["movement"]) + pack * float(sensory["packSynergyMultiplier"]))
        * line_of_sight
    )
    close01 = clamp01((135.0 - state.distance) / 120.0)
    attack_ready = 1.0 if state.attack_cooldown <= 0.0 else 0.0
    false_ready = 1.0 if state.false_charge_cooldown <= 0.0 else 0.0

    scores: Dict[str, float] = {}
    for behavior in EXPECTED_BEHAVIORS:
        base = lookup[(context, behavior)]
        score = base * (0.78 + weighted_aggression * 0.36)
        if behavior == "Circle":
            score += (1.0 - close01) * 0.10 + line_of_sight * 0.08
        elif behavior == "Hide":
            score += (1.0 - line_of_sight) * 0.28 + (1.0 - attack_ready) * 0.22
        elif behavior == "Breach":
            score += light * 0.14 + sound * 0.10 + close01 * 0.08
        elif behavior == "FalseCharge":
            score += (movement + sound) * 0.12 + close01 * 0.10 + false_ready * 0.12
            score -= (1.0 - false_ready) * 0.30
        elif behavior == "RealAttack":
            score += close01 * 0.48 + pack * 0.30 + sound * 0.22 + movement * 0.18
            score *= attack_ready
            score *= aggression_scalar
        scores[behavior] = max(0.0, score)
    return scores


def choose_behavior(scores: Mapping[str, float], state: EncounterState, hysteresis_seconds: float) -> str:
    winner = max(EXPECTED_BEHAVIORS, key=lambda behavior: scores[behavior])
    if state.hysteresis > 0.0 and winner != state.current_behavior:
        old_score = scores.get(state.current_behavior, 0.0)
        if scores[winner] < old_score + 0.14:
            return state.current_behavior
    if winner != state.current_behavior:
        state.hysteresis = hysteresis_seconds
    return winner


def apply_behavior(brain: Mapping[str, object], behavior: str, profile: PlayerProfile, state: EncounterState, sound: float, light: float, movement: float, line_of_sight: float, pack: float) -> None:
    params = behavior_parameters(brain, behavior)
    terror_gain = float(params["terror"]) * (0.05 + 0.05 * max(sound, light, movement))
    state.terror = clamp01(state.terror * 0.965 + terror_gain)
    player_escape = 1.8 + profile.movement * 3.7 + profile.discipline * 0.9 - state.terror * 1.9
    if profile.decoy > 0.0 and behavior in ("FalseCharge", "RealAttack"):
        player_escape += profile.decoy * 2.1
    player_escape = max(0.6, player_escape)
    state.distance += player_escape

    pull = float(params["distancePullMeters"])
    if behavior == "Circle":
        if state.distance < 86.0:
            state.distance += 4.8
        elif state.distance > 118.0:
            state.distance += pull
        else:
            state.distance += pull * 0.25
    elif behavior == "Hide":
        state.distance += min(4.0, float(params["distancePullMeters"]) * 0.35)
    elif behavior == "Breach":
        state.distance += pull
        if state.distance < 58.0:
            state.player_hp -= 2.0 + 4.0 * max(light, sound)
    elif behavior == "FalseCharge":
        state.distance += pull
        near_miss_damage = float(params["damage"]) * (0.35 + movement * 0.45 + sound * 0.20) * (1.0 - profile.dodge * 0.45)
        if state.distance < 48.0 and state.false_charge_cooldown <= 0.0:
            state.player_hp -= max(0.0, near_miss_damage)
            state.false_charge_cooldown = float(params["cooldownSeconds"])
        state.distance += 5.0 + profile.discipline * 2.0
    elif behavior == "RealAttack":
        state.distance += pull
        if state.attack_cooldown <= 0.0:
            hit_quality = clamp01((74.0 - state.distance) / 50.0 + pack * 0.28 + sound * 0.18 + movement * 0.16)
            dodge_reduction = 1.0 - profile.dodge * 0.38 - profile.discipline * 0.14
            damage = float(params["damage"]) * max(0.35, hit_quality) * max(0.34, dodge_reduction)
            if light > 0.75:
                damage *= 0.72
            state.player_hp -= damage
            state.attack_cooldown = float(params["cooldownSeconds"])
            state.false_charge_cooldown = max(state.false_charge_cooldown, 4.0)
            state.distance += 10.0 + profile.discipline * 5.0
            if light > 0.75:
                state.distance += 4.0

    if light > 0.75 and line_of_sight > 0.55 and behavior != "RealAttack":
        state.distance += 6.0
    state.distance = max(18.0, min(245.0, state.distance))


def run_encounter(brain: Mapping[str, object], lookup: Mapping[Tuple[str, str], float], index: int, seed: int, aggression_scalar: float) -> EncounterResult:
    profile = profile_for(index, seed)
    tier = tier_for(index, seed)
    pack_count = pack_count_for(index, seed)
    start_hash = stable_seed(seed, index, 0x5354415254)
    start_distance = 62.0 + (start_hash & 127) * (82.0 / 127.0)
    state = EncounterState(
        distance=start_distance,
        player_hp=100.0,
        terror=0.0,
        attack_cooldown=((start_hash >> 8) & 7) * 0.5,
        false_charge_cooldown=((start_hash >> 12) & 3) * 0.5,
        hysteresis=0.0,
        current_behavior="Hide",
        elapsed=0,
        rng=stable_seed(seed, index, 0x52554E47),
    )
    behavior_counts = {behavior: 0 for behavior in EXPECTED_BEHAVIORS}
    context_counts: Dict[str, int] = {}
    terror_samples: List[float] = []
    player_forward_x = 0.0
    player_forward_z = 1.0
    hysteresis_seconds = float(brain.get("decisionCadence", {}).get("hysteresisSeconds", 2.5)) if isinstance(brain.get("decisionCadence"), dict) else 2.5

    for second in range(0, MAX_SECONDS, TIME_STEP_SECONDS):
        state.elapsed = second
        state.attack_cooldown = max(0.0, state.attack_cooldown - TIME_STEP_SECONDS)
        state.false_charge_cooldown = max(0.0, state.false_charge_cooldown - TIME_STEP_SECONDS)
        state.hysteresis = max(0.0, state.hysteresis - TIME_STEP_SECONDS)
        sound, light, movement, line_of_sight = compute_signals(profile, state, index, seed)
        pack = compute_pack_synergy(index, second, pack_count, player_forward_x, player_forward_z, seed)
        context = resolve_context(state.distance, sound, light, movement, pack, state.attack_cooldown, tier)
        scores = score_behaviors(brain, lookup, context, state, sound, light, movement, line_of_sight, pack, aggression_scalar)
        behavior = choose_behavior(scores, state, hysteresis_seconds)
        state.current_behavior = behavior
        behavior_counts[behavior] += 1
        context_counts[context] = context_counts.get(context, 0) + 1
        apply_behavior(brain, behavior, profile, state, sound, light, movement, line_of_sight, pack)
        terror_samples.append(state.terror)
        if state.player_hp <= 0.0:
            return EncounterResult(
                index=index,
                profile=profile.name,
                tier=tier,
                pack_count=pack_count,
                killed=True,
                escaped=False,
                timeout=False,
                kill_time=second + TIME_STEP_SECONDS,
                final_hp=0.0,
                max_terror=max(terror_samples),
                mean_terror=sum(terror_samples) / len(terror_samples),
                behavior_counts=behavior_counts,
                context_counts=context_counts,
            )
        if second >= 45 and state.distance >= 222.0 and state.terror < 0.78:
            return EncounterResult(
                index=index,
                profile=profile.name,
                tier=tier,
                pack_count=pack_count,
                killed=False,
                escaped=True,
                timeout=False,
                kill_time=0,
                final_hp=round(state.player_hp, 3),
                max_terror=max(terror_samples),
                mean_terror=sum(terror_samples) / len(terror_samples),
                behavior_counts=behavior_counts,
                context_counts=context_counts,
            )

    return EncounterResult(
        index=index,
        profile=profile.name,
        tier=tier,
        pack_count=pack_count,
        killed=False,
        escaped=False,
        timeout=True,
        kill_time=0,
        final_hp=round(state.player_hp, 3),
        max_terror=max(terror_samples) if terror_samples else 0.0,
        mean_terror=sum(terror_samples) / len(terror_samples) if terror_samples else 0.0,
        behavior_counts=behavior_counts,
        context_counts=context_counts,
    )


def merge_counts(results: Iterable[EncounterResult], attr_name: str) -> Dict[str, int]:
    merged: Dict[str, int] = {}
    for result in results:
        counts = getattr(result, attr_name)
        for key, value in counts.items():
            merged[key] = merged.get(key, 0) + int(value)
    return dict(sorted(merged.items()))


def summarize_results(results: Sequence[EncounterResult]) -> Dict[str, object]:
    kills = [result for result in results if result.killed]
    kill_times = [result.kill_time for result in kills]
    under30 = [value for value in kill_times if value < 30]
    encounters = max(1, len(results))
    max_terror_values = [result.max_terror for result in results]
    mean_terror_values = [result.mean_terror for result in results]
    return {
        "encounters": len(results),
        "kills": len(kills),
        "killRate": round(len(kills) / encounters, 5),
        "survivals": encounters - len(kills),
        "escaped": sum(1 for result in results if result.escaped),
        "timeouts": sum(1 for result in results if result.timeout),
        "under30Kills": len(under30),
        "under30KillRate": round(len(under30) / encounters, 5),
        "under30AmongKillsRate": round(len(under30) / max(1, len(kills)), 5),
        "averageKillTimeSeconds": round(sum(kill_times) / max(1, len(kill_times)), 3) if kill_times else 0.0,
        "medianKillTimeSeconds": round(statistics.median(kill_times), 3) if kill_times else 0.0,
        "meanMaxTerror": round(sum(max_terror_values) / encounters, 5),
        "meanTerror": round(sum(mean_terror_values) / encounters, 5),
        "behaviorCounts": merge_counts(results, "behavior_counts"),
        "contextCounts": merge_counts(results, "context_counts"),
    }


def breakdown_by(results: Sequence[EncounterResult], key_name: str) -> Dict[str, Dict[str, object]]:
    groups: Dict[str, List[EncounterResult]] = {}
    for result in results:
        key = str(getattr(result, key_name))
        groups.setdefault(key, []).append(result)
    return {key: summarize_results(rows) for key, rows in sorted(groups.items())}


def execute_simulation(brain: Mapping[str, object], encounters: int, seed: int, initial_aggression: float) -> Tuple[List[EncounterResult], float, int, bool]:
    lookup = build_score_lookup(brain)
    aggression = initial_aggression
    lowered = False
    passes = 0
    while True:
        passes += 1
        results = [run_encounter(brain, lookup, index, seed, aggression) for index in range(encounters)]
        summary = summarize_results(results)
        all_fast_kills = summary["kills"] == encounters and summary["under30Kills"] == encounters
        too_many_fast_kills = float(summary["under30KillRate"]) > float(brain["selfAudit"]["frustrationGuardUnder30KillRateMax"])
        if not all_fast_kills and not too_many_fast_kills:
            return results, aggression, passes, lowered
        aggression *= 0.82
        lowered = True
        if passes >= 8:
            return results, aggression, passes, lowered


def result_sample(result: EncounterResult) -> Dict[str, object]:
    return {
        "index": result.index,
        "profile": result.profile,
        "tier": result.tier,
        "packCount": result.pack_count,
        "killed": result.killed,
        "escaped": result.escaped,
        "timeout": result.timeout,
        "killTime": result.kill_time,
        "finalHp": round(result.final_hp, 3),
        "maxTerror": round(result.max_terror, 5),
        "meanTerror": round(result.mean_terror, 5),
    }


def build_report(brain: Mapping[str, object], validation: Mapping[str, object], results: Sequence[EncounterResult], elapsed: float, aggression: float, passes: int, lowered: bool, seed: int) -> Dict[str, object]:
    summary = summarize_results(results)
    profile_breakdown = breakdown_by(results, "profile")
    tier_breakdown = breakdown_by(results, "tier")
    pack_count_breakdown = breakdown_by(results, "pack_count")
    self_audit = brain["selfAudit"]
    assert isinstance(self_audit, dict)
    target_min = float(self_audit["targetKillRateMin"])
    target_max = float(self_audit["targetKillRateMax"])
    fast_max = float(self_audit["frustrationGuardUnder30KillRateMax"])
    subgroup_max = float(self_audit.get("subgroupKillRateMax", target_max))
    kill_rate = float(summary["killRate"])
    under30_rate = float(summary["under30KillRate"])
    max_profile_kill_rate = max((float(row["killRate"]) for row in profile_breakdown.values()), default=0.0)
    max_tier_kill_rate = max((float(row["killRate"]) for row in tier_breakdown.values()), default=0.0)
    max_pack_kill_rate = max((float(row["killRate"]) for row in pack_count_breakdown.values()), default=0.0)
    frustration_passed = summary["kills"] != summary["encounters"] or summary["under30Kills"] != summary["encounters"]
    target_passed = target_min <= kill_rate <= target_max
    fast_guard_passed = under30_rate <= fast_max
    subgroup_guard_passed = max_profile_kill_rate <= subgroup_max and max_tier_kill_rate <= subgroup_max and max_pack_kill_rate <= subgroup_max
    status = EXPECTED_STATUS if validation["status"] == "BRAIN_VALIDATION_PASSED" and frustration_passed and target_passed and fast_guard_passed and subgroup_guard_passed else "PENDING_VERIFICATION"
    return {
        "schemaVersion": 1,
        "status": status,
        "generatedBy": "AI_BEHAVIOR_BIOMIMETIC_DESIGNER",
        "evidenceClass": "CLI_PYTHON_BATTLE_SIMULATION",
        "runtimeUnityProof": "PENDING VERIFICATION",
        "brainPath": "Data/AI/Leviathan_Brain.json",
        "seed": seed,
        "elapsedSeconds": round(elapsed, 3),
        "calibration": {
            "initialAggressionScalar": 1.0,
            "finalAggressionScalar": round(aggression, 6),
            "passes": passes,
            "aggressionLowered": lowered
        },
        "validation": validation,
        "summary": summary,
        "profileBreakdown": profile_breakdown,
        "tierBreakdown": tier_breakdown,
        "packCountBreakdown": pack_count_breakdown,
        "frustrationAudit": {
            "allKillsUnder30": summary["kills"] == summary["encounters"] and summary["under30Kills"] == summary["encounters"],
            "targetKillRateRange": [target_min, target_max],
            "targetKillRatePassed": target_passed,
            "under30KillRateMax": fast_max,
            "under30GuardPassed": fast_guard_passed,
            "subgroupKillRateMax": subgroup_max,
            "maxProfileKillRate": round(max_profile_kill_rate, 5),
            "maxTierKillRate": round(max_tier_kill_rate, 5),
            "maxPackKillRate": round(max_pack_kill_rate, 5),
            "subgroupGuardPassed": subgroup_guard_passed,
            "terrorNotFrustrationPassed": frustration_passed and fast_guard_passed and subgroup_guard_passed
        },
        "globalDataVaultAudit": {
            "status": "PASSED" if validation["status"] == "BRAIN_VALIDATION_PASSED" else "FAILED",
            "knownBufferSource": validation.get("knownBufferSource", str(BUFFER_SOURCE_RELATIVE_PATH).replace("\\", "/")),
            "knownBufferSourceStatus": validation.get("knownBufferSourceStatus", "UNKNOWN"),
            "knownBufferCount": validation.get("knownBufferCount", 0),
            "feedCount": validation["globalDataVaultFeedCount"],
            "utilityScoreCount": validation["utilityScoreCount"]
        },
        "samples": [result_sample(result) for result in results[:20]]
    }


def check_artifacts(brain_path: Path, report_path: Path, expected_encounters: int) -> Dict[str, object]:
    errors: List[str] = []
    try:
        brain = load_json(brain_path)
    except (OSError, json.JSONDecodeError, ValueError) as exc:
        return {"status": "ARTIFACT_CHECK_FAILED", "errors": [f"brain:{exc}"]}
    try:
        report = load_json(report_path)
    except (OSError, json.JSONDecodeError, ValueError) as exc:
        return {"status": "ARTIFACT_CHECK_FAILED", "errors": [f"report:{exc}"]}

    validation = validate_brain(brain)
    if validation["errors"]:
        errors.extend(f"brain:{error}" for error in validation["errors"])
    if validation.get("knownBufferSourceStatus") != "SOURCE_PARSED":
        errors.append("GlobalDataVault BufferID source was not parsed")

    self_audit = brain.get("selfAudit")
    target_min = 0.0
    target_max = 1.0
    fast_max = 1.0
    subgroup_max = 1.0
    if isinstance(self_audit, dict):
        target_min = float(self_audit.get("targetKillRateMin", target_min))
        target_max = float(self_audit.get("targetKillRateMax", target_max))
        fast_max = float(self_audit.get("frustrationGuardUnder30KillRateMax", fast_max))
        subgroup_max = float(self_audit.get("subgroupKillRateMax", target_max))
    else:
        errors.append("brain.selfAudit missing")

    if report.get("status") != EXPECTED_STATUS:
        errors.append(f"report.status != {EXPECTED_STATUS}")

    report_validation = report.get("validation")
    if not isinstance(report_validation, dict):
        errors.append("report.validation missing")
    else:
        if report_validation.get("status") != validation.get("status"):
            errors.append("report.validation status mismatch")
        if report_validation.get("utilityScoreCount") != validation.get("utilityScoreCount"):
            errors.append("report.validation utilityScoreCount mismatch")
        if report_validation.get("globalDataVaultFeedCount") != validation.get("globalDataVaultFeedCount"):
            errors.append("report.validation globalDataVaultFeedCount mismatch")
        if report_validation.get("knownBufferCount") != validation.get("knownBufferCount"):
            errors.append("report.validation knownBufferCount mismatch")
        if report_validation.get("knownBufferSourceStatus") != validation.get("knownBufferSourceStatus"):
            errors.append("report.validation knownBufferSourceStatus mismatch")

    summary = report.get("summary")
    if not isinstance(summary, dict):
        errors.append("report.summary missing")
        summary = {}
    if summary.get("encounters") != expected_encounters:
        errors.append(f"summary.encounters != {expected_encounters}")
    if summary.get("kills") == expected_encounters and summary.get("under30Kills") == expected_encounters:
        errors.append("frustration audit failed: all encounters killed under 30 seconds")
    kill_rate = summary.get("killRate")
    if not isinstance(kill_rate, (int, float)) or isinstance(kill_rate, bool) or not target_min <= float(kill_rate) <= target_max:
        errors.append("summary.killRate outside target range")
    under30_rate = summary.get("under30KillRate")
    if not isinstance(under30_rate, (int, float)) or isinstance(under30_rate, bool) or float(under30_rate) > fast_max:
        errors.append("summary.under30KillRate exceeds guard")

    audit = report.get("globalDataVaultAudit")
    if not isinstance(audit, dict) or audit.get("status") != "PASSED":
        errors.append("globalDataVaultAudit not passed")
    elif audit.get("knownBufferSourceStatus") != "SOURCE_PARSED":
        errors.append("globalDataVaultAudit source was not parsed")
    elif audit.get("knownBufferSourceStatus") != validation.get("knownBufferSourceStatus"):
        errors.append("globalDataVaultAudit knownBufferSourceStatus mismatch")
    elif audit.get("knownBufferCount") != validation.get("knownBufferCount"):
        errors.append("globalDataVaultAudit knownBufferCount mismatch")
    elif audit.get("feedCount") != validation.get("globalDataVaultFeedCount"):
        errors.append("globalDataVaultAudit feedCount mismatch")
    elif audit.get("utilityScoreCount") != validation.get("utilityScoreCount"):
        errors.append("globalDataVaultAudit utilityScoreCount mismatch")

    frustration = report.get("frustrationAudit")
    if not isinstance(frustration, dict) or not frustration.get("terrorNotFrustrationPassed"):
        errors.append("terrorNotFrustrationPassed is false")
    if isinstance(frustration, dict) and not frustration.get("subgroupGuardPassed", False):
        errors.append("subgroupGuardPassed is false")
    if isinstance(frustration, dict):
        for field in ("maxProfileKillRate", "maxTierKillRate", "maxPackKillRate"):
            value = frustration.get(field)
            if not isinstance(value, (int, float)) or isinstance(value, bool) or float(value) > subgroup_max:
                errors.append(f"{field} exceeds subgroupKillRateMax")
    find_nonfinite(report, "report", errors)
    return {
        "status": "ARTIFACT_CHECK_PASSED" if not errors else "ARTIFACT_CHECK_FAILED",
        "errors": errors,
        "brainBytes": brain_path.stat().st_size if brain_path.exists() else 0,
        "reportBytes": report_path.stat().st_size if report_path.exists() else 0,
        "encounters": summary.get("encounters"),
        "killRate": summary.get("killRate"),
        "under30KillRate": summary.get("under30KillRate"),
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="HECTON-8 Alpha Leviathan utility battle simulator.")
    parser.add_argument("--brain", default="Data/AI/Leviathan_Brain.json")
    parser.add_argument("--report", default="Tools/AiBattleSim_Report.json")
    parser.add_argument("--encounters", type=int, default=DEFAULT_ENCOUNTERS)
    parser.add_argument("--seed", type=lambda text: int(text, 0), default=DEFAULT_SEED)
    parser.add_argument("--aggression", type=float, default=1.0)
    parser.add_argument("--check-artifacts", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    brain_path = Path(args.brain)
    report_path = Path(args.report)
    encounters = max(1, args.encounters)

    if args.check_artifacts:
        check = check_artifacts(brain_path, report_path, encounters)
        print(f"status={check['status']}")
        print(f"sizes brain={check['brainBytes']} report={check['reportBytes']}")
        print(f"encounters={check['encounters']} killRate={check['killRate']} under30KillRate={check['under30KillRate']}")
        for error in check["errors"]:
            print(f"error={error}")
        return 0 if check["status"] == "ARTIFACT_CHECK_PASSED" else 2

    start = time.perf_counter()
    brain = load_json(brain_path)
    validation = validate_brain(brain)
    if validation["errors"]:
        for error in validation["errors"]:
            print(f"validation_error={error}")
        return 2
    results, aggression, passes, lowered = execute_simulation(brain, encounters, args.seed, max(0.05, args.aggression))
    report = build_report(brain, validation, results, time.perf_counter() - start, aggression, passes, lowered, args.seed)
    write_json(report_path, report)
    summary = report["summary"]
    print("LEVIATHAN BATTLE SIMULATION FINISHED")
    print(f"status={report['status']} evidence={report['evidenceClass']} unity={report['runtimeUnityProof']}")
    print(
        f"encounters={summary['encounters']} kills={summary['kills']} "
        f"killRate={summary['killRate']} under30KillRate={summary['under30KillRate']}"
    )
    print(
        f"avgKillTime={summary['averageKillTimeSeconds']} meanTerror={summary['meanTerror']} "
        f"aggression={report['calibration']['finalAggressionScalar']} passes={passes} lowered={lowered}"
    )
    print(f"wrote={report_path.as_posix()}")
    return 0 if report["status"] == EXPECTED_STATUS else 2


if __name__ == "__main__":
    raise SystemExit(main())
