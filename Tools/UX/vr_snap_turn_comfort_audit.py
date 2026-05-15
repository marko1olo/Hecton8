#!/usr/bin/env python3
"""Offline VR comfort audit for a 30-degree snap-turn profile.

No Unity runtime dependency. This checks the calibration values in
Docs/Design/VR_Comfort_Profile_Quest.json against a deterministic snap-turn
model and validates the haptic JSON command envelope.
"""

from __future__ import annotations

import hashlib
import json
import math
import re
import sys
from dataclasses import dataclass
from pathlib import Path


SCRIPT_PATH = Path(__file__).resolve()
TEST_SCRIPT_PATH = SCRIPT_PATH.with_name("test_vr_snap_turn_comfort_audit.py")
ROOT = SCRIPT_PATH.parents[2]
COMFORT_JSON = ROOT / "Docs" / "Design" / "VR_Comfort_Profile_Quest.json"
COMFORT_MD = ROOT / "Docs" / "Design" / "VR_Comfort_Profile_Quest.md"
WAVEFORM_JSON = ROOT / "Docs" / "Design" / "VR_Haptic_Waveforms_Quest.json"
VR_SOMATIC_PROVIDER_CS = ROOT / "Assets" / "_Project" / "Scripts" / "Gameplay" / "VRSomaticProvider.cs"
TOOL_HAPTICS_RUNTIME_CS = ROOT / "Assets" / "_Project" / "Scripts" / "Tools" / "ToolHapticsRuntime.cs"
DEFAULT_REPORT_JSON = ROOT / "Docs" / "AgentLogs" / "VR_Comfort_Audit_SOMATIC_COMFORT_ANALYST.json"

SNAP_TURN_DEGREES = 30.0
SNAP_TURN_SECONDS = 0.16
SETTLE_SECONDS = 0.28
EXPECTED_OWNER = "SOMATIC_COMFORT_ANALYST"
EXPECTED_STATUS = "COMFORT DEFINED"
EXPECTED_VERIFICATION_STATUS = "PENDING_RUNTIME_VERIFICATION"
EXPECTED_COMFORT_SCHEMA = "hecton8.vr_comfort_profile.v1"
EXPECTED_WAVEFORM_SCHEMA = "hecton8.vr_haptic_waveforms.v1"
EXPECTED_AUDIT_SCHEMA = "hecton8.vr_comfort_audit.v1"
EXPECTED_RUNTIME_CONTRACT = "ToolHapticsRuntime.HapticCommand"
EXPECTED_WAVEFORM_IDS = (
    "hull_collision_light",
    "hull_collision_heavy",
    "head_near_field_brush",
    "low_o2_pulse",
    "critical_o2_alarm",
    "engine_hum_idle",
    "engine_strain",
    "sonar_ping",
    "plasma_cutter_bite",
    "pressure_creak_warning",
)
EXPECTED_WAVEFORM_EVENTS = {
    "hull_collision_light": "Collision",
    "hull_collision_heavy": "Collision",
    "head_near_field_brush": "NearFieldContact",
    "low_o2_pulse": "LowO2Pulse",
    "critical_o2_alarm": "CriticalO2Alarm",
    "engine_hum_idle": "EngineHum",
    "engine_strain": "EngineHum",
    "sonar_ping": "SonarPing",
    "plasma_cutter_bite": "ToolBite",
    "pressure_creak_warning": "PressureWarning",
}
ALLOWED_FATIGUE_CLASSES = {
    "critical_bypass",
    "ambient_fatigued",
    "comfort",
    "tool",
}
EXPECTED_MOTOR_MASK_BITS = {
    "left": 1,
    "right": 2,
    "both": 3,
}
EXPECTED_BLEND_MODES = {
    "override": 0,
    "additive": 1,
    "max": 2,
}
EXPECTED_RUNTIME_OWNER_COMPONENTS = {
    "VRSomaticProvider": "Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs",
    "ToolHapticsRuntime": "Assets/_Project/Scripts/Tools/ToolHapticsRuntime.cs",
}
EXPECTED_RUNTIME_FIELD_BINDINGS = {
    "VRSomaticProvider.rotationJerkLimitRadiansPerSecondCubed": "jerk.fullEventRadS3",
    "VRSomaticProvider.MaxSomaticHeadAngularJerkRadiansPerSecondCubed": "jerk.hardCapRadS3",
    "VRSomaticProvider.JerkEventDebounceSeconds": "jerk.eventDebounceSeconds",
    "VRSomaticProvider.rotationJerkVignetteContribution": "jerk.maxVignetteContribution",
    "VRSomaticProvider.rootRotationSmoothingSharpness": "stabilization.modes.middle.sharpness",
    "VRSomaticProvider.comfortVignetteMaximum": "devices.Quest3_90Hz.opacityMax",
    "VRSomaticProvider.comfortAccelerationSoftTunnelStartRadS2": "devices.Quest3_90Hz.accelSoftTunnelStartRadS2",
    "VRSomaticProvider.comfortAccelerationEmergencyClampRadS2": "devices.Quest3_90Hz.accelEmergencyClampRadS2",
    "VRSomaticProvider.comfortAccelerationReleaseBelowRadS2": "devices.Quest3_90Hz.releaseBelowRadS2",
    "VRSomaticProvider.comfortAccelerationReleaseHysteresisSeconds": "devices.Quest3_90Hz.releaseHysteresisSeconds",
    "VRSomaticProvider.comfortVignetteAttackSlewPerFrame": "devices.Quest3_90Hz.attackSlewPerFrame",
    "VRSomaticProvider.comfortVignetteReleaseSlewPerFrame": "devices.Quest3_90Hz.releaseSlewPerFrame",
    "ToolHapticsRuntime.BufferCapacity": "haptic.limits.bufferCapacity",
    "ToolHapticsRuntime.MaxCommandDurationSeconds": "haptic.limits.durationMaxSeconds",
    "ToolHapticsRuntime.MaxCommandFrequencyHz": "haptic.limits.frequencyMaxHz",
}


@dataclass(frozen=True)
class QuestComfortProfile:
    name: str
    refresh_hz: float
    accel_soft_rad_s2: float
    accel_emergency_rad_s2: float
    opacity_max: float
    attack_slew_per_frame: float
    release_slew_per_frame: float


@dataclass(frozen=True)
class JerkProfile:
    soft_rad_s3: float
    full_rad_s3: float
    hard_cap_rad_s3: float
    opacity_max: float


@dataclass(frozen=True)
class ShockRules:
    max_opacity_delta_per_frame: float
    max_untunneled_angle_delta_deg: float
    min_opacity_for_large_angle_delta: float


def smoothstep01(value: float) -> float:
    t = min(1.0, max(0.0, value))
    return t * t * (3.0 - (2.0 * t))


def snap_angle_rad(time_seconds: float) -> float:
    if time_seconds <= 0.0:
        return 0.0
    if time_seconds >= SNAP_TURN_SECONDS:
        return math.radians(SNAP_TURN_DEGREES)
    u = time_seconds / SNAP_TURN_SECONDS
    return math.radians(SNAP_TURN_DEGREES) * smoothstep01(u)


def angular_accel_opacity(profile: QuestComfortProfile, abs_accel: float) -> float:
    if abs_accel <= profile.accel_soft_rad_s2:
        return 0.0
    span = max(0.001, profile.accel_emergency_rad_s2 - profile.accel_soft_rad_s2)
    return profile.opacity_max * smoothstep01((abs_accel - profile.accel_soft_rad_s2) / span)


def angular_jerk_opacity(abs_jerk: float, jerk_profile: JerkProfile) -> float:
    effective_jerk = min(abs_jerk, jerk_profile.hard_cap_rad_s3)
    if effective_jerk <= jerk_profile.soft_rad_s3:
        return 0.0
    span = max(0.001, jerk_profile.full_rad_s3 - jerk_profile.soft_rad_s3)
    return jerk_profile.opacity_max * smoothstep01((effective_jerk - jerk_profile.soft_rad_s3) / span)


def apply_slew(previous: float, target: float, profile: QuestComfortProfile) -> float:
    if target > previous:
        return min(target, previous + profile.attack_slew_per_frame)
    return max(target, previous - profile.release_slew_per_frame)


def simulate_profile(
    profile: QuestComfortProfile,
    jerk_profile: JerkProfile,
    shock_rules: ShockRules,
) -> dict[str, float | int | str]:
    dt = 1.0 / profile.refresh_hz
    total_seconds = SNAP_TURN_SECONDS + SETTLE_SECONDS
    frame_count = int(math.ceil(total_seconds / dt)) + 1
    previous_angle = 0.0
    previous_omega = 0.0
    previous_accel = 0.0
    opacity = 0.0
    max_angle_delta_deg = 0.0
    max_opacity = 0.0
    max_opacity_delta = 0.0
    max_abs_accel = 0.0
    max_abs_jerk = 0.0
    shock_frames = 0

    for frame in range(frame_count):
        t = frame * dt
        angle = snap_angle_rad(t)
        omega = (angle - previous_angle) / dt
        accel = (omega - previous_omega) / dt
        jerk = (accel - previous_accel) / dt
        abs_accel = abs(accel)
        abs_jerk = abs(jerk)
        target_opacity = max(
            angular_accel_opacity(profile, abs_accel),
            angular_jerk_opacity(abs_jerk, jerk_profile),
        )
        next_opacity = apply_slew(opacity, target_opacity, profile)
        angle_delta_deg = abs(math.degrees(angle - previous_angle))
        opacity_delta = abs(next_opacity - opacity)

        max_angle_delta_deg = max(max_angle_delta_deg, angle_delta_deg)
        max_opacity = max(max_opacity, next_opacity)
        max_opacity_delta = max(max_opacity_delta, opacity_delta)
        max_abs_accel = max(max_abs_accel, abs_accel)
        max_abs_jerk = max(max_abs_jerk, min(abs_jerk, jerk_profile.hard_cap_rad_s3))

        if opacity_delta > shock_rules.max_opacity_delta_per_frame:
            shock_frames += 1
        if (
            angle_delta_deg > shock_rules.max_untunneled_angle_delta_deg
            and next_opacity < shock_rules.min_opacity_for_large_angle_delta
        ):
            shock_frames += 1

        previous_angle = angle
        previous_omega = omega
        previous_accel = accel
        opacity = next_opacity

    return {
        "profile": profile.name,
        "frames": frame_count,
        "max_angle_delta_deg": max_angle_delta_deg,
        "max_opacity": max_opacity,
        "max_opacity_delta": max_opacity_delta,
        "max_abs_accel_rad_s2": max_abs_accel,
        "max_abs_jerk_rad_s3": max_abs_jerk,
        "shock_frames": shock_frames,
    }


def load_comfort_profile() -> tuple[list[QuestComfortProfile], JerkProfile, ShockRules, list[str]]:
    errors: list[str] = []
    payload = load_json_object(COMFORT_JSON, "comfort profile", errors)
    if errors:
        _, jerk_profile, shock_rules, parse_errors = parse_comfort_payload(payload)
        return [], jerk_profile, shock_rules, errors + parse_errors
    return parse_comfort_payload(payload)


def parse_comfort_payload(payload: dict) -> tuple[list[QuestComfortProfile], JerkProfile, ShockRules, list[str]]:
    errors: list[str] = []
    if payload.get("schema") != EXPECTED_COMFORT_SCHEMA:
        errors.append("comfort schema mismatch")
    if payload.get("owner") != EXPECTED_OWNER:
        errors.append("comfort owner mismatch")
    if payload.get("status") != EXPECTED_STATUS:
        errors.append("comfort status mismatch")
    if payload.get("verificationStatus") != EXPECTED_VERIFICATION_STATUS:
        errors.append("comfort verificationStatus mismatch")

    devices = require_list(payload.get("devices", []), "devices", errors)
    profiles: list[QuestComfortProfile] = []
    expected_device_ids = {"Quest2_72Hz", "Quest3_90Hz"}
    seen_device_ids: set[str] = set()
    if len(devices) != 2:
        errors.append(f"expected 2 comfort devices, found {len(devices)}")
    profile_count = read_int(payload.get("profileCount", -1), "profileCount", errors)
    if profile_count != len(devices):
        errors.append("profileCount does not match devices length")
    for index, device_value in enumerate(devices):
        device = require_dict(device_value, f"devices[{index}]", errors)
        if not device:
            continue
        profile_name = str(device.get("id", ""))
        if not profile_name:
            errors.append(f"devices[{index}] missing id")
            profile_name = f"devices[{index}]"
        profile = QuestComfortProfile(
            profile_name,
            read_float(device.get("refreshHz", float("nan")), f"{profile_name}.refreshHz", errors),
            read_float(
                device.get("accelSoftTunnelStartRadS2", float("nan")),
                f"{profile_name}.accelSoftTunnelStartRadS2",
                errors,
            ),
            read_float(
                device.get("accelEmergencyClampRadS2", float("nan")),
                f"{profile_name}.accelEmergencyClampRadS2",
                errors,
            ),
            read_float(device.get("opacityMax", float("nan")), f"{profile_name}.opacityMax", errors),
            read_float(
                device.get("attackSlewPerFrame", float("nan")),
                f"{profile_name}.attackSlewPerFrame",
                errors,
            ),
            read_float(
                device.get("releaseSlewPerFrame", float("nan")),
                f"{profile_name}.releaseSlewPerFrame",
                errors,
            ),
        )
        seen_device_ids.add(profile.name)
        profiles.append(profile)
        if profile.refresh_hz <= 0.0:
            errors.append(f"{profile.name} refreshHz must be positive")
        if profile.accel_soft_rad_s2 <= 0.0:
            errors.append(f"{profile.name} accelSoftTunnelStartRadS2 must be positive")
        if profile.accel_emergency_rad_s2 <= profile.accel_soft_rad_s2:
            errors.append(f"{profile.name} emergency acceleration must exceed soft threshold")
        if not 0.0 < profile.opacity_max <= 1.0:
            errors.append(f"{profile.name} opacityMax outside 0..1")
        if not 0.0 < profile.attack_slew_per_frame <= 0.10:
            errors.append(f"{profile.name} attack slew outside 0..0.10")
        if not 0.0 < profile.release_slew_per_frame <= 0.10:
            errors.append(f"{profile.name} release slew outside 0..0.10")
    missing_device_ids = sorted(expected_device_ids - seen_device_ids)
    extra_device_ids = sorted(seen_device_ids - expected_device_ids)
    if missing_device_ids:
        errors.append(f"missing comfort device ids: {', '.join(missing_device_ids)}")
    if extra_device_ids:
        errors.append(f"unexpected comfort device ids: {', '.join(extra_device_ids)}")

    jerk_payload = require_dict(payload.get("jerk", {}), "jerk", errors)
    jerk_profile = JerkProfile(
        read_float(jerk_payload.get("softRadS3", float("nan")), "jerk.softRadS3", errors),
        read_float(jerk_payload.get("fullEventRadS3", float("nan")), "jerk.fullEventRadS3", errors),
        read_float(jerk_payload.get("hardCapRadS3", float("nan")), "jerk.hardCapRadS3", errors),
        read_float(
            jerk_payload.get("maxVignetteContribution", float("nan")),
            "jerk.maxVignetteContribution",
            errors,
        ),
    )
    if not (0.0 < jerk_profile.soft_rad_s3 < jerk_profile.full_rad_s3 <= jerk_profile.hard_cap_rad_s3):
        errors.append("jerk thresholds must be monotonic: soft < full <= hard cap")
    if not 0.0 < jerk_profile.opacity_max <= 1.0:
        errors.append("jerk maxVignetteContribution outside 0..1")

    shock_payload = require_dict(payload.get("visualTeleportShock", {}), "visualTeleportShock", errors)
    shock_rules = ShockRules(
        read_float(
            shock_payload.get("maxOpacityDeltaPerFrame", float("nan")),
            "visualTeleportShock.maxOpacityDeltaPerFrame",
            errors,
        ),
        read_float(
            shock_payload.get("maxUntunneledAngleDeltaDeg", float("nan")),
            "visualTeleportShock.maxUntunneledAngleDeltaDeg",
            errors,
        ),
        read_float(
            shock_payload.get("minOpacityForLargeAngleDelta", float("nan")),
            "visualTeleportShock.minOpacityForLargeAngleDelta",
            errors,
        ),
    )
    if not 0.0 < shock_rules.max_opacity_delta_per_frame <= 0.10:
        errors.append("visual shock max opacity delta outside 0..0.10")
    if shock_rules.max_untunneled_angle_delta_deg <= 0.0:
        errors.append("visual shock angle threshold must be positive")
    if not 0.0 <= shock_rules.min_opacity_for_large_angle_delta <= 1.0:
        errors.append("visual shock min opacity outside 0..1")

    validate_speed_lut(payload, errors)
    validate_stabilization(payload, profiles, errors)
    validate_comfort_device_table(payload, profiles, errors)
    validate_markdown_companion(payload, errors)
    validate_runtime_integration(payload, errors)
    if str(payload.get("combineRule", "")).lower() != "max":
        errors.append("combineRule must be max")
    return profiles, jerk_profile, shock_rules, errors


def validate_speed_lut(payload: dict, errors: list[str]) -> None:
    lut = require_list(payload.get("speedVignetteLutQuest3", []), "speedVignetteLutQuest3", errors)
    if len(lut) < 4:
        errors.append("speed vignette LUT must contain at least 4 entries")
        return
    last_speed = -1.0
    last_opacity = -1.0
    quest2_multiplier = read_float(
        payload.get("quest2SpeedLutMultiplier", float("nan")),
        "quest2SpeedLutMultiplier",
        errors,
    )
    quest2_clamp = read_float(payload.get("quest2SpeedLutClamp", float("nan")), "quest2SpeedLutClamp", errors)
    if not 1.0 <= quest2_multiplier <= 1.5:
        errors.append("quest2SpeedLutMultiplier must be in 1.0..1.5")
    if not 0.0 < quest2_clamp <= 1.0:
        errors.append("quest2SpeedLutClamp must be in 0..1")
    for index, entry_value in enumerate(lut):
        entry = require_dict(entry_value, f"speedVignetteLutQuest3[{index}]", errors)
        speed = read_float(entry.get("speed", float("nan")), f"speedVignetteLutQuest3[{index}].speed", errors)
        opacity = read_float(
            entry.get("opacity", float("nan")),
            f"speedVignetteLutQuest3[{index}].opacity",
            errors,
        )
        if speed <= last_speed:
            errors.append(f"speed LUT entry {index} is not strictly increasing")
        if opacity < last_opacity:
            errors.append(f"speed LUT entry {index} opacity decreased")
        if not 0.0 <= opacity <= 1.0:
            errors.append(f"speed LUT entry {index} opacity outside 0..1")
        quest2_opacity = min(opacity * quest2_multiplier, quest2_clamp)
        if quest2_opacity > quest2_clamp:
            errors.append(f"quest2 derived LUT entry {index} exceeds clamp")
        last_speed = speed
        last_opacity = opacity


def validate_stabilization(payload: dict, profiles: list[QuestComfortProfile], errors: list[str]) -> None:
    device_by_id = {profile.name: profile for profile in profiles}
    stabilization = require_dict(payload.get("stabilization", {}), "stabilization", errors)
    modes = require_list(stabilization.get("modes", []), "stabilization.modes", errors)
    if len(modes) < 3:
        errors.append("stabilization must contain low/middle/high or better modes")
        return
    max_alpha = read_float(stabilization.get("maxAlpha", float("nan")), "stabilization.maxAlpha", errors)
    for index, mode_value in enumerate(modes):
        mode = require_dict(mode_value, f"stabilization.modes[{index}]", errors)
        sharpness = read_float(
            mode.get("sharpness", float("nan")),
            f"stabilization mode {mode.get('id', index)} sharpness",
            errors,
        )
        if sharpness <= 0.0:
            errors.append(f"stabilization mode {mode.get('id', '?')} sharpness must be positive")
            continue
        for json_key, profile_id in (
            ("alphaQuest2_72Hz", "Quest2_72Hz"),
            ("alphaQuest3_90Hz", "Quest3_90Hz"),
        ):
            if profile_id not in device_by_id:
                continue
            expected = alpha_from_sharpness(sharpness, device_by_id[profile_id].refresh_hz)
            actual = read_float(
                mode.get(json_key, float("nan")),
                f"stabilization mode {mode.get('id', index)} {json_key}",
                errors,
            )
            if abs(actual - expected) > 0.002:
                errors.append(
                    f"stabilization mode {mode.get('id', '?')} {json_key} expected {expected:.3f}, found {actual:.3f}"
                )
            if actual > max_alpha:
                errors.append(f"stabilization mode {mode.get('id', '?')} {json_key} exceeds maxAlpha")


def validate_comfort_device_table(payload: dict, profiles: list[QuestComfortProfile], errors: list[str]) -> None:
    table = require_dict(payload.get("deviceTable", {}), "deviceTable", errors)
    columns = (
        "deviceId",
        "refreshHz",
        "accelSoftTunnelStartRadS2",
        "accelStrongTunnelRadS2",
        "accelEmergencyClampRadS2",
        "releaseBelowRadS2",
        "opacityMax",
        "attackSlewPerFrame",
        "releaseSlewPerFrame",
    )
    profile_count = len(profiles)
    for column in columns:
        values = require_list(table.get(column, []), f"deviceTable.{column}", errors)
        if len(values) != profile_count:
            errors.append(f"deviceTable {column} length mismatch")
            return

    for index, profile in enumerate(profiles):
        if str(table["deviceId"][index]) != profile.name:
            errors.append(f"deviceTable id mismatch for row {index}")
        table_refresh = read_float(table["refreshHz"][index], f"{profile.name}.deviceTable.refreshHz", errors)
        compare_close(f"{profile.name}.refreshHz table parity", table_refresh, profile.refresh_hz, 0.001, errors)
        table_soft = read_float(
            table["accelSoftTunnelStartRadS2"][index],
            f"{profile.name}.deviceTable.accelSoftTunnelStartRadS2",
            errors,
        )
        compare_close(
            f"{profile.name}.accelSoftTunnelStartRadS2 table parity",
            table_soft,
            profile.accel_soft_rad_s2,
            0.001,
            errors,
        )
        table_emergency = read_float(
            table["accelEmergencyClampRadS2"][index],
            f"{profile.name}.deviceTable.accelEmergencyClampRadS2",
            errors,
        )
        compare_close(
            f"{profile.name}.accelEmergencyClampRadS2 table parity",
            table_emergency,
            profile.accel_emergency_rad_s2,
            0.001,
            errors,
        )
        table_opacity = read_float(table["opacityMax"][index], f"{profile.name}.deviceTable.opacityMax", errors)
        compare_close(f"{profile.name}.opacityMax table parity", table_opacity, profile.opacity_max, 0.001, errors)
        table_attack = read_float(
            table["attackSlewPerFrame"][index],
            f"{profile.name}.deviceTable.attackSlewPerFrame",
            errors,
        )
        compare_close(
            f"{profile.name}.attackSlewPerFrame table parity",
            table_attack,
            profile.attack_slew_per_frame,
            0.001,
            errors,
        )
        table_release_slew = read_float(
            table["releaseSlewPerFrame"][index],
            f"{profile.name}.deviceTable.releaseSlewPerFrame",
            errors,
        )
        compare_close(
            f"{profile.name}.releaseSlewPerFrame table parity",
            table_release_slew,
            profile.release_slew_per_frame,
            0.001,
            errors,
        )
        strong = read_float(
            table["accelStrongTunnelRadS2"][index],
            f"{profile.name}.deviceTable.accelStrongTunnelRadS2",
            errors,
        )
        if not profile.accel_soft_rad_s2 < strong < profile.accel_emergency_rad_s2:
            errors.append(f"{profile.name} table strong acceleration must sit between soft and emergency")
        release = read_float(table["releaseBelowRadS2"][index], f"{profile.name}.deviceTable.releaseBelowRadS2", errors)
        if not 0.0 < release < profile.accel_soft_rad_s2:
            errors.append(f"{profile.name} table release threshold must sit below soft tunnel start")


def validate_markdown_companion(payload: dict, errors: list[str]) -> None:
    if not COMFORT_MD.exists():
        errors.append(f"missing markdown companion: {display_path(COMFORT_MD)}")
        return

    text = COMFORT_MD.read_text(encoding="utf-8")
    required_fragments = (
        "Machine-readable companion: `Docs/Design/VR_Comfort_Profile_Quest.json`.",
        "`Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs`",
        "`Assets/_Project/Scripts/Tools/ToolHapticsRuntime.cs`",
        "## Runtime Integration Handoff",
        "`VRSomaticProvider` | `rotationJerkLimitRadiansPerSecondCubed` | `jerk.fullEventRadS3`",
        "`VRSomaticProvider` | `comfortAccelerationSoftTunnelStartRadS2` | `devices.Quest3_90Hz.accelSoftTunnelStartRadS2`",
        "`VRSomaticProvider` | `comfortVignetteReleaseSlewPerFrame` | `devices.Quest3_90Hz.releaseSlewPerFrame`",
        "`ToolHapticsRuntime` | `MaxCommandFrequencyHz` | `haptic.limits.frequencyMaxHz`",
        "`finalTunnel = max(speedLutOpacity, angularAccelerationOpacity, jerkOpacity, frameRateSafetyOpacity)`",
    )
    for fragment in required_fragments:
        if fragment not in text:
            errors.append(f"markdown companion missing fragment: {fragment}")

    devices = require_list(payload.get("devices", []), "devices", errors)
    for index, device_value in enumerate(devices):
        device = require_dict(device_value, f"devices[{index}]", errors)
        device_name = str(device.get("device", ""))
        if device_name and device_name not in text:
            errors.append(f"markdown companion missing device name: {device_name}")
        soft = read_float(
            device.get("accelSoftTunnelStartRadS2", float("nan")),
            f"{device_name}.markdown.accelSoftTunnelStartRadS2",
            errors,
        )
        emergency = read_float(
            device.get("accelEmergencyClampRadS2", float("nan")),
            f"{device_name}.markdown.accelEmergencyClampRadS2",
            errors,
        )
        if not markdown_contains_number_with_unit(text, soft, "rad/s2"):
            errors.append(f"markdown companion missing soft threshold: {soft}")
        if not markdown_contains_number_with_unit(text, emergency, "rad/s2"):
            errors.append(f"markdown companion missing emergency threshold: {emergency}")


def markdown_contains_number_with_unit(text: str, value: float, unit: str) -> bool:
    if not math.isfinite(value):
        return False

    candidates = {f"{value:.1f}", f"{value:.2f}", f"{value:.3f}"}
    if value.is_integer():
        candidates.add(str(int(value)))

    for candidate in candidates:
        pattern = rf"(?<![\d.]){re.escape(candidate)}\s*{re.escape(unit)}(?![\w/])"
        if re.search(pattern, text):
            return True
    return False


def require_dict(value: object, label: str, errors: list[str]) -> dict:
    if isinstance(value, dict):
        return value
    errors.append(f"{label} must be object")
    return {}


def require_list(value: object, label: str, errors: list[str]) -> list:
    if isinstance(value, list):
        return value
    errors.append(f"{label} must be array")
    return []


def read_float(value: object, label: str, errors: list[str]) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        errors.append(f"{label} must be numeric")
        return float("nan")

    try:
        result = float(value)
    except (TypeError, ValueError):
        errors.append(f"{label} must be numeric")
        return float("nan")

    if not math.isfinite(result):
        errors.append(f"{label} must be finite")
        return float("nan")
    return result


def read_int(value: object, label: str, errors: list[str]) -> int:
    if isinstance(value, bool) or not isinstance(value, int):
        errors.append(f"{label} must be integer")
        return -1
    return value


def validate_runtime_integration(payload: dict, errors: list[str]) -> None:
    phase = require_dict(payload.get("phaseOwnership", {}), "phaseOwnership", errors)
    if phase.get("comfortScalarWritePhase") != "VISUAL_SYNC":
        errors.append("phaseOwnership comfortScalarWritePhase must be VISUAL_SYNC")
    if phase.get("hapticRequestPhase") != "VISUAL_SYNC":
        errors.append("phaseOwnership hapticRequestPhase must be VISUAL_SYNC")
    if phase.get("runtimeOwner") != "VRSomaticProvider":
        errors.append("phaseOwnership runtimeOwner mismatch")
    if phase.get("hapticOwner") != "ToolHapticsRuntime":
        errors.append("phaseOwnership hapticOwner mismatch")
    fallback = str(phase.get("loadShedFallback", ""))
    if "no extra blit" not in fallback or "no camera FOV mutation" not in fallback:
        errors.append("phaseOwnership loadShedFallback must reject extra blit and FOV mutation")

    runtime = require_dict(payload.get("runtimeIntegration", {}), "runtimeIntegration", errors)
    if runtime.get("profileLoadPolicy") != "cold_bootstrap_or_editor_baked_only":
        errors.append("runtimeIntegration profileLoadPolicy mismatch")
    if runtime.get("executionPhase") != "VISUAL_SYNC":
        errors.append("runtimeIntegration executionPhase must be VISUAL_SYNC")
    if runtime.get("combineRule") != "max":
        errors.append("runtimeIntegration combineRule must be max")

    hot_path_rules = require_dict(runtime.get("hotPathRules", {}), "runtimeIntegration.hotPathRules", errors)
    for key in (
        "noJsonParsingInTick",
        "noCameraProjectionMutation",
        "zeroGcDispatch",
        "hapticDispatchThroughToolHapticsRuntimeOnly",
    ):
        if hot_path_rules.get(key) is not True:
            errors.append(f"runtimeIntegration hotPathRules.{key} must be true")

    if runtime.get("ownerComponents", {}) != EXPECTED_RUNTIME_OWNER_COMPONENTS:
        errors.append("runtimeIntegration ownerComponents mismatch")

    bindings = require_list(runtime.get("fieldBindings", []), "runtimeIntegration.fieldBindings", errors)
    actual_bindings: dict[str, str] = {}
    for index, binding_value in enumerate(bindings):
        binding = require_dict(binding_value, f"runtimeIntegration.fieldBindings[{index}]", errors)
        runtime_field = str(binding.get("runtimeField", ""))
        profile_path = str(binding.get("profilePath", ""))
        if not runtime_field or not profile_path:
            errors.append(f"runtimeIntegration fieldBindings[{index}] incomplete")
            continue
        if runtime_field in actual_bindings:
            errors.append(f"runtimeIntegration duplicate binding {runtime_field}")
        actual_bindings[runtime_field] = profile_path

    if actual_bindings != EXPECTED_RUNTIME_FIELD_BINDINGS:
        errors.append("runtimeIntegration fieldBindings mismatch")


def alpha_from_sharpness(sharpness: float, refresh_hz: float) -> float:
    x = sharpness / refresh_hz
    return x / (1.0 + x)


def validate_waveforms() -> tuple[int, list[str]]:
    errors: list[str] = []
    payload = load_json_object(WAVEFORM_JSON, "haptic waveform", errors)
    count, waveform_errors = validate_waveform_payload(payload)
    return count, errors + waveform_errors


def validate_waveform_payload(payload: dict) -> tuple[int, list[str]]:
    errors: list[str] = []
    waveforms = require_list(payload.get("waveforms", []), "waveforms", errors)
    required_events = {"Collision", "LowO2Pulse", "EngineHum"}
    seen_events: set[str] = set()
    seen_ids: list[str] = []
    if payload.get("schema") != EXPECTED_WAVEFORM_SCHEMA:
        errors.append("haptic waveform schema mismatch")
    if payload.get("owner") != EXPECTED_OWNER:
        errors.append("haptic waveform owner mismatch")
    if payload.get("status") != EXPECTED_STATUS:
        errors.append("haptic waveform status mismatch")
    if payload.get("verificationStatus") != EXPECTED_VERIFICATION_STATUS:
        errors.append("haptic waveform verificationStatus mismatch")
    if payload.get("runtimeContract") != EXPECTED_RUNTIME_CONTRACT:
        errors.append("haptic runtimeContract mismatch")
    validate_waveform_limits(payload.get("limits", {}), errors)

    if len(waveforms) != 10:
        errors.append(f"expected 10 waveforms, found {len(waveforms)}")
    if read_int(payload.get("waveformCount", -1), "waveformCount", errors) != len(waveforms):
        errors.append("waveformCount does not match waveforms length")
    ids: set[str] = set()
    normalized_waveforms: list[dict] = []
    for index, waveform_value in enumerate(waveforms):
        waveform = require_dict(waveform_value, f"waveforms[{index}]", errors)
        normalized_waveforms.append(waveform)
        waveform_id = str(waveform.get("id", ""))
        event_id = str(waveform.get("event", ""))
        seen_ids.append(waveform_id)
        if event_id:
            seen_events.add(event_id)
        if not waveform_id:
            errors.append(f"waveform {index} missing id")
        if waveform_id in ids:
            errors.append(f"duplicate waveform id {waveform_id}")
        ids.add(waveform_id)
        expected_event = EXPECTED_WAVEFORM_EVENTS.get(waveform_id)
        if expected_event is None:
            errors.append(f"unexpected waveform id {waveform_id}")
        elif event_id != expected_event:
            errors.append(f"{waveform_id} event mismatch: expected {expected_event}, found {event_id}")
        low_freq = read_float(waveform.get("lowFreqIntensity", 0.0), f"{waveform_id}.lowFreqIntensity", errors)
        high_freq = read_float(waveform.get("highFreqIntensity", 0.0), f"{waveform_id}.highFreqIntensity", errors)
        duration = read_float(waveform.get("durationSeconds", 0.0), f"{waveform_id}.durationSeconds", errors)
        frequency = read_float(waveform.get("frequencyHz", 0.0), f"{waveform_id}.frequencyHz", errors)
        cadence = read_float(waveform.get("cadenceSeconds", 0.0), f"{waveform_id}.cadenceSeconds", errors)
        priority = read_int(waveform.get("priority", -1), f"{waveform_id}.priority", errors)
        motor_mask = read_int(waveform.get("motorMask", 0), f"{waveform_id}.motorMask", errors)
        blend_mode = read_int(waveform.get("blendMode", -1), f"{waveform_id}.blendMode", errors)
        if not 0 <= low_freq <= 1:
            errors.append(f"{waveform_id} lowFreqIntensity outside 0..1")
        if not 0 <= high_freq <= 1:
            errors.append(f"{waveform_id} highFreqIntensity outside 0..1")
        if not 0 < duration <= 2.0:
            errors.append(f"{waveform_id} durationSeconds outside 0..2")
        if not 0 <= frequency <= 60.0:
            errors.append(f"{waveform_id} frequencyHz outside 0..60")
        if cadence < 0.0:
            errors.append(f"{waveform_id} cadenceSeconds below 0")
        if cadence > 0.0 and cadence < duration:
            errors.append(f"{waveform_id} cadenceSeconds below durationSeconds")
        if priority not in (0, 1, 2, 3):
            errors.append(f"{waveform_id} priority outside 0..3")
        if motor_mask not in (1, 2, 3):
            errors.append(f"{waveform_id} motorMask outside left/right/both")
        if blend_mode not in (0, 1, 2):
            errors.append(f"{waveform_id} blendMode outside override/additive/max")
        if str(waveform.get("fatigueClass", "")) not in ALLOWED_FATIGUE_CLASSES:
            errors.append(f"{waveform_id} fatigueClass unsupported")
        if not str(waveform.get("directionalRule", "")).strip():
            errors.append(f"{waveform_id} directionalRule missing")
    missing_events = sorted(required_events - seen_events)
    if missing_events:
        errors.append(f"missing required waveform events: {', '.join(missing_events)}")
    if tuple(seen_ids) != EXPECTED_WAVEFORM_IDS:
        errors.append("waveform id set/order mismatch")
    validate_waveform_table(payload, normalized_waveforms, errors)
    return len(waveforms), errors


def validate_waveform_limits(limits: dict, errors: list[str]) -> None:
    limits = require_dict(limits, "haptic limits", errors)
    expected_float_limits = {
        "intensityMin": 0.0,
        "intensityMax": 1.0,
        "durationMaxSeconds": 2.0,
        "frequencyMaxHz": 60.0,
    }
    for key, expected in expected_float_limits.items():
        actual = read_float(limits.get(key, float("nan")), f"haptic limit {key}", errors)
        if not math.isfinite(actual) or abs(actual - expected) > 0.001:
            errors.append(f"haptic limit {key} mismatch: expected {expected}, found {actual}")

    if read_int(limits.get("bufferCapacity", -1), "haptic limit bufferCapacity", errors) != 16:
        errors.append("haptic limit bufferCapacity mismatch")
    motor_mask_bits = require_dict(limits.get("motorMaskBits", {}), "haptic limit motorMaskBits", errors)
    if motor_mask_bits != EXPECTED_MOTOR_MASK_BITS:
        errors.append("haptic motorMaskBits mismatch")
    blend_modes = require_dict(limits.get("blendModes", {}), "haptic limit blendModes", errors)
    if blend_modes != EXPECTED_BLEND_MODES:
        errors.append("haptic blendModes mismatch")


def validate_waveform_table(payload: dict, waveforms: list[dict], errors: list[str]) -> None:
    table = require_dict(payload.get("waveformTable", {}), "waveformTable", errors)
    columns = ("id", "event", "priority", "motorMask", "blendMode")
    table_columns: dict[str, list] = {}
    for column in columns:
        values = require_list(table.get(column, []), f"waveformTable.{column}", errors)
        table_columns[column] = values
        if len(values) != len(waveforms):
            errors.append(f"waveformTable {column} length mismatch")
            return

    for index, waveform in enumerate(waveforms):
        waveform_id = str(waveform.get("id", ""))
        if str(table_columns["id"][index]) != waveform_id:
            errors.append(f"waveformTable id mismatch at row {index}")
        if str(table_columns["event"][index]) != str(waveform.get("event", "")):
            errors.append(f"{waveform_id} waveformTable event mismatch")
        table_priority = read_int(table_columns["priority"][index], f"{waveform_id}.waveformTable.priority", errors)
        waveform_priority = read_int(waveform.get("priority", -1), f"{waveform_id}.priority", errors)
        table_motor_mask = read_int(table_columns["motorMask"][index], f"{waveform_id}.waveformTable.motorMask", errors)
        waveform_motor_mask = read_int(waveform.get("motorMask", -1), f"{waveform_id}.motorMask", errors)
        table_blend_mode = read_int(table_columns["blendMode"][index], f"{waveform_id}.waveformTable.blendMode", errors)
        waveform_blend_mode = read_int(waveform.get("blendMode", -1), f"{waveform_id}.blendMode", errors)
        if table_priority != waveform_priority:
            errors.append(f"{waveform_id} waveformTable priority mismatch")
        if table_motor_mask != waveform_motor_mask:
            errors.append(f"{waveform_id} waveformTable motorMask mismatch")
        if table_blend_mode != waveform_blend_mode:
            errors.append(f"{waveform_id} waveformTable blendMode mismatch")


def validate_source_contract() -> tuple[dict[str, float], list[str]]:
    errors: list[str] = []
    values: dict[str, float] = {}
    comfort_payload = load_json_object(COMFORT_JSON, "comfort profile", errors)
    waveform_payload = load_json_object(WAVEFORM_JSON, "haptic waveform", errors)
    somatic_source = read_text_if_exists(VR_SOMATIC_PROVIDER_CS, errors)
    haptics_source = read_text_if_exists(TOOL_HAPTICS_RUNTIME_CS, errors)
    if not somatic_source or not haptics_source:
        return values, errors

    values["runtimeJerkFullRadS3"] = extract_csharp_number(
        somatic_source,
        "rotationJerkLimitRadiansPerSecondCubed",
        errors,
    )
    values["runtimeJerkHardCapRadS3"] = extract_csharp_number(
        somatic_source,
        "MaxSomaticHeadAngularJerkRadiansPerSecondCubed",
        errors,
    )
    values["runtimeJerkDebounceSeconds"] = extract_csharp_number(
        somatic_source,
        "JerkEventDebounceSeconds",
        errors,
    )
    values["runtimeJerkVignetteContribution"] = extract_csharp_number(
        somatic_source,
        "rotationJerkVignetteContribution",
        errors,
    )
    values["runtimeRootSharpness"] = extract_csharp_number(
        somatic_source,
        "rootRotationSmoothingSharpness",
        errors,
    )
    values["runtimeComfortVignetteMaximum"] = extract_csharp_number(
        somatic_source,
        "comfortVignetteMaximum",
        errors,
    )
    values["runtimeAccelerationSoftTunnelStartRadS2"] = extract_csharp_number(
        somatic_source,
        "comfortAccelerationSoftTunnelStartRadS2",
        errors,
    )
    values["runtimeAccelerationEmergencyClampRadS2"] = extract_csharp_number(
        somatic_source,
        "comfortAccelerationEmergencyClampRadS2",
        errors,
    )
    values["runtimeAccelerationReleaseBelowRadS2"] = extract_csharp_number(
        somatic_source,
        "comfortAccelerationReleaseBelowRadS2",
        errors,
    )
    values["runtimeAccelerationReleaseHysteresisSeconds"] = extract_csharp_number(
        somatic_source,
        "comfortAccelerationReleaseHysteresisSeconds",
        errors,
    )
    values["runtimeComfortVignetteAttackSlewPerFrame"] = extract_csharp_number(
        somatic_source,
        "comfortVignetteAttackSlewPerFrame",
        errors,
    )
    values["runtimeComfortVignetteReleaseSlewPerFrame"] = extract_csharp_number(
        somatic_source,
        "comfortVignetteReleaseSlewPerFrame",
        errors,
    )
    values["runtimeQuest2ComfortVignetteMaximum"] = extract_csharp_number(
        somatic_source,
        "Quest2ComfortVignetteMaximum",
        errors,
    )
    values["runtimeQuest2AccelerationSoftTunnelStartRadS2"] = extract_csharp_number(
        somatic_source,
        "Quest2ComfortAccelerationSoftTunnelStartRadS2",
        errors,
    )
    values["runtimeQuest2AccelerationEmergencyClampRadS2"] = extract_csharp_number(
        somatic_source,
        "Quest2ComfortAccelerationEmergencyClampRadS2",
        errors,
    )
    values["runtimeQuest2AccelerationReleaseBelowRadS2"] = extract_csharp_number(
        somatic_source,
        "Quest2ComfortAccelerationReleaseBelowRadS2",
        errors,
    )
    values["runtimeQuest2AccelerationReleaseHysteresisSeconds"] = extract_csharp_number(
        somatic_source,
        "Quest2ComfortAccelerationReleaseHysteresisSeconds",
        errors,
    )
    values["runtimeQuest2ComfortVignetteAttackSlewPerFrame"] = extract_csharp_number(
        somatic_source,
        "Quest2ComfortVignetteAttackSlewPerFrame",
        errors,
    )
    values["runtimeQuest2ComfortVignetteReleaseSlewPerFrame"] = extract_csharp_number(
        somatic_source,
        "Quest2ComfortVignetteReleaseSlewPerFrame",
        errors,
    )
    values["runtimeQuest2FrameSafetyDeltaSeconds"] = extract_csharp_number(
        somatic_source,
        "Quest2ComfortFrameSafetyDeltaSeconds",
        errors,
    )
    values["runtimeQuest2FrameSafetyMinOpacity"] = extract_csharp_number(
        somatic_source,
        "Quest2ComfortFrameSafetyMinOpacity",
        errors,
    )
    values["runtimeQuest2FrameSafetyConsecutiveFrames"] = extract_csharp_number(
        somatic_source,
        "Quest2ComfortFrameSafetyConsecutiveFrames",
        errors,
    )
    values["runtimeQuest2FrameSafetyReleaseStableFrames"] = extract_csharp_number(
        somatic_source,
        "Quest2ComfortFrameSafetyReleaseStableFrames",
        errors,
    )
    values["runtimeQuest3FrameSafetyDeltaSeconds"] = extract_csharp_number(
        somatic_source,
        "Quest3ComfortFrameSafetyDeltaSeconds",
        errors,
    )
    values["runtimeQuest3FrameSafetyMinOpacity"] = extract_csharp_number(
        somatic_source,
        "Quest3ComfortFrameSafetyMinOpacity",
        errors,
    )
    values["runtimeQuest3FrameSafetyConsecutiveFrames"] = extract_csharp_number(
        somatic_source,
        "Quest3ComfortFrameSafetyConsecutiveFrames",
        errors,
    )
    values["runtimeQuest3FrameSafetyReleaseStableFrames"] = extract_csharp_number(
        somatic_source,
        "Quest3ComfortFrameSafetyReleaseStableFrames",
        errors,
    )
    values["blackBoxFlagFramePressure"] = extract_csharp_bit_mask(somatic_source, "BlackBoxFlagFramePressure", errors)
    values["blackBoxFlagQuest2Fallback"] = extract_csharp_bit_mask(somatic_source, "BlackBoxFlagQuest2Fallback", errors)
    values["blackBoxFlagAccelerationTunnel"] = extract_csharp_bit_mask(
        somatic_source,
        "BlackBoxFlagAccelerationTunnel",
        errors,
    )
    validate_black_box_comfort_flag_bits(values, errors)
    values["hapticBufferCapacity"] = extract_csharp_number(haptics_source, "BufferCapacity", errors)
    values["hapticMaxDurationSeconds"] = extract_csharp_number(haptics_source, "MaxCommandDurationSeconds", errors)
    values["hapticMaxFrequencyHz"] = extract_csharp_number(haptics_source, "MaxCommandFrequencyHz", errors)

    jerk_payload = require_dict(comfort_payload.get("jerk", {}), "sourceContract.jerk", errors)
    stabilization = require_dict(
        comfort_payload.get("stabilization", {}),
        "sourceContract.stabilization",
        errors,
    )
    stabilization_modes = require_list(
        stabilization.get("modes", []),
        "sourceContract.stabilization.modes",
        errors,
    )
    default_stabilization: dict = {}
    for index, mode_value in enumerate(stabilization_modes):
        mode = require_dict(mode_value, f"sourceContract.stabilization.modes[{index}]", errors)
        if mode.get("id") == "middle":
            default_stabilization = mode
            break
    devices = require_list(comfort_payload.get("devices", []), "sourceContract.devices", errors)
    quest2: dict = {}
    quest3: dict = {}
    for index, device_value in enumerate(devices):
        device = require_dict(device_value, f"sourceContract.devices[{index}]", errors)
        if device.get("id") == "Quest2_72Hz":
            quest2 = device
        elif device.get("id") == "Quest3_90Hz":
            quest3 = device
    limits = require_dict(waveform_payload.get("limits", {}), "sourceContract.haptic.limits", errors)

    compare_close(
        "jerk.fullEventRadS3",
        read_float(jerk_payload.get("fullEventRadS3", float("nan")), "sourceContract.jerk.fullEventRadS3", errors),
        values["runtimeJerkFullRadS3"],
        0.001,
        errors,
    )
    compare_close(
        "jerk.hardCapRadS3",
        read_float(jerk_payload.get("hardCapRadS3", float("nan")), "sourceContract.jerk.hardCapRadS3", errors),
        values["runtimeJerkHardCapRadS3"],
        0.001,
        errors,
    )
    compare_close(
        "jerk.eventDebounceSeconds",
        read_float(
            jerk_payload.get("eventDebounceSeconds", float("nan")),
            "sourceContract.jerk.eventDebounceSeconds",
            errors,
        ),
        values["runtimeJerkDebounceSeconds"],
        0.001,
        errors,
    )
    compare_close(
        "jerk.maxVignetteContribution",
        read_float(
            jerk_payload.get("maxVignetteContribution", float("nan")),
            "sourceContract.jerk.maxVignetteContribution",
            errors,
        ),
        values["runtimeJerkVignetteContribution"],
        0.001,
        errors,
    )
    compare_close(
        "stabilization.middle.sharpness",
        read_float(
            default_stabilization.get("sharpness", float("nan")),
            "sourceContract.stabilization.middle.sharpness",
            errors,
        ),
        values["runtimeRootSharpness"],
        0.001,
        errors,
    )
    compare_close(
        "Quest3 opacityMax vs runtime comfortVignetteMaximum",
        read_float(quest3.get("opacityMax", float("nan")), "sourceContract.Quest3.opacityMax", errors),
        values["runtimeComfortVignetteMaximum"],
        0.001,
        errors,
    )
    compare_close(
        "Quest3 accelSoftTunnelStartRadS2 vs runtime comfortAccelerationSoftTunnelStartRadS2",
        read_float(
            quest3.get("accelSoftTunnelStartRadS2", float("nan")),
            "sourceContract.Quest3.accelSoftTunnelStartRadS2",
            errors,
        ),
        values["runtimeAccelerationSoftTunnelStartRadS2"],
        0.001,
        errors,
    )
    compare_close(
        "Quest3 accelEmergencyClampRadS2 vs runtime comfortAccelerationEmergencyClampRadS2",
        read_float(
            quest3.get("accelEmergencyClampRadS2", float("nan")),
            "sourceContract.Quest3.accelEmergencyClampRadS2",
            errors,
        ),
        values["runtimeAccelerationEmergencyClampRadS2"],
        0.001,
        errors,
    )
    compare_close(
        "Quest3 releaseBelowRadS2 vs runtime comfortAccelerationReleaseBelowRadS2",
        read_float(
            quest3.get("releaseBelowRadS2", float("nan")),
            "sourceContract.Quest3.releaseBelowRadS2",
            errors,
        ),
        values["runtimeAccelerationReleaseBelowRadS2"],
        0.001,
        errors,
    )
    compare_close(
        "Quest3 releaseHysteresisSeconds vs runtime comfortAccelerationReleaseHysteresisSeconds",
        read_float(
            quest3.get("releaseHysteresisSeconds", float("nan")),
            "sourceContract.Quest3.releaseHysteresisSeconds",
            errors,
        ),
        values["runtimeAccelerationReleaseHysteresisSeconds"],
        0.001,
        errors,
    )
    compare_close(
        "Quest3 attackSlewPerFrame vs runtime comfortVignetteAttackSlewPerFrame",
        read_float(quest3.get("attackSlewPerFrame", float("nan")), "sourceContract.Quest3.attackSlewPerFrame", errors),
        values["runtimeComfortVignetteAttackSlewPerFrame"],
        0.001,
        errors,
    )
    compare_close(
        "Quest3 releaseSlewPerFrame vs runtime comfortVignetteReleaseSlewPerFrame",
        read_float(
            quest3.get("releaseSlewPerFrame", float("nan")),
            "sourceContract.Quest3.releaseSlewPerFrame",
            errors,
        ),
        values["runtimeComfortVignetteReleaseSlewPerFrame"],
        0.001,
        errors,
    )
    compare_close(
        "Quest2 opacityMax vs runtime Quest2ComfortVignetteMaximum",
        read_float(quest2.get("opacityMax", float("nan")), "sourceContract.Quest2.opacityMax", errors),
        values["runtimeQuest2ComfortVignetteMaximum"],
        0.001,
        errors,
    )
    compare_close(
        "Quest2 accelSoftTunnelStartRadS2 vs runtime Quest2ComfortAccelerationSoftTunnelStartRadS2",
        read_float(
            quest2.get("accelSoftTunnelStartRadS2", float("nan")),
            "sourceContract.Quest2.accelSoftTunnelStartRadS2",
            errors,
        ),
        values["runtimeQuest2AccelerationSoftTunnelStartRadS2"],
        0.001,
        errors,
    )
    compare_close(
        "Quest2 accelEmergencyClampRadS2 vs runtime Quest2ComfortAccelerationEmergencyClampRadS2",
        read_float(
            quest2.get("accelEmergencyClampRadS2", float("nan")),
            "sourceContract.Quest2.accelEmergencyClampRadS2",
            errors,
        ),
        values["runtimeQuest2AccelerationEmergencyClampRadS2"],
        0.001,
        errors,
    )
    compare_close(
        "Quest2 releaseBelowRadS2 vs runtime Quest2ComfortAccelerationReleaseBelowRadS2",
        read_float(
            quest2.get("releaseBelowRadS2", float("nan")),
            "sourceContract.Quest2.releaseBelowRadS2",
            errors,
        ),
        values["runtimeQuest2AccelerationReleaseBelowRadS2"],
        0.001,
        errors,
    )
    compare_close(
        "Quest2 releaseHysteresisSeconds vs runtime Quest2ComfortAccelerationReleaseHysteresisSeconds",
        read_float(
            quest2.get("releaseHysteresisSeconds", float("nan")),
            "sourceContract.Quest2.releaseHysteresisSeconds",
            errors,
        ),
        values["runtimeQuest2AccelerationReleaseHysteresisSeconds"],
        0.001,
        errors,
    )
    compare_close(
        "Quest2 attackSlewPerFrame vs runtime Quest2ComfortVignetteAttackSlewPerFrame",
        read_float(
            quest2.get("attackSlewPerFrame", float("nan")),
            "sourceContract.Quest2.attackSlewPerFrame",
            errors,
        ),
        values["runtimeQuest2ComfortVignetteAttackSlewPerFrame"],
        0.001,
        errors,
    )
    compare_close(
        "Quest2 releaseSlewPerFrame vs runtime Quest2ComfortVignetteReleaseSlewPerFrame",
        read_float(
            quest2.get("releaseSlewPerFrame", float("nan")),
            "sourceContract.Quest2.releaseSlewPerFrame",
            errors,
        ),
        values["runtimeQuest2ComfortVignetteReleaseSlewPerFrame"],
        0.001,
        errors,
    )
    compare_close(
        "Quest2 frameSafetyDeltaMs vs runtime Quest2ComfortFrameSafetyDeltaSeconds",
        read_float(quest2.get("frameSafetyDeltaMs", float("nan")), "sourceContract.Quest2.frameSafetyDeltaMs", errors)
        * 0.001,
        values["runtimeQuest2FrameSafetyDeltaSeconds"],
        0.00001,
        errors,
    )
    compare_close(
        "Quest2 frameSafetyMinOpacity vs runtime Quest2ComfortFrameSafetyMinOpacity",
        read_float(
            quest2.get("frameSafetyMinOpacity", float("nan")),
            "sourceContract.Quest2.frameSafetyMinOpacity",
            errors,
        ),
        values["runtimeQuest2FrameSafetyMinOpacity"],
        0.001,
        errors,
    )
    compare_close(
        "Quest2 frameSafetyConsecutiveFrames vs runtime Quest2ComfortFrameSafetyConsecutiveFrames",
        read_float(
            quest2.get("frameSafetyConsecutiveFrames", float("nan")),
            "sourceContract.Quest2.frameSafetyConsecutiveFrames",
            errors,
        ),
        values["runtimeQuest2FrameSafetyConsecutiveFrames"],
        0.001,
        errors,
    )
    compare_close(
        "Quest2 frameSafetyReleaseStableFrames vs runtime Quest2ComfortFrameSafetyReleaseStableFrames",
        read_float(
            quest2.get("frameSafetyReleaseStableFrames", float("nan")),
            "sourceContract.Quest2.frameSafetyReleaseStableFrames",
            errors,
        ),
        values["runtimeQuest2FrameSafetyReleaseStableFrames"],
        0.001,
        errors,
    )
    compare_close(
        "Quest3 frameSafetyDeltaMs vs runtime Quest3ComfortFrameSafetyDeltaSeconds",
        read_float(quest3.get("frameSafetyDeltaMs", float("nan")), "sourceContract.Quest3.frameSafetyDeltaMs", errors)
        * 0.001,
        values["runtimeQuest3FrameSafetyDeltaSeconds"],
        0.00001,
        errors,
    )
    compare_close(
        "Quest3 frameSafetyMinOpacity vs runtime Quest3ComfortFrameSafetyMinOpacity",
        read_float(
            quest3.get("frameSafetyMinOpacity", float("nan")),
            "sourceContract.Quest3.frameSafetyMinOpacity",
            errors,
        ),
        values["runtimeQuest3FrameSafetyMinOpacity"],
        0.001,
        errors,
    )
    compare_close(
        "Quest3 frameSafetyConsecutiveFrames vs runtime Quest3ComfortFrameSafetyConsecutiveFrames",
        read_float(
            quest3.get("frameSafetyConsecutiveFrames", float("nan")),
            "sourceContract.Quest3.frameSafetyConsecutiveFrames",
            errors,
        ),
        values["runtimeQuest3FrameSafetyConsecutiveFrames"],
        0.001,
        errors,
    )
    compare_close(
        "Quest3 frameSafetyReleaseStableFrames vs runtime Quest3ComfortFrameSafetyReleaseStableFrames",
        read_float(
            quest3.get("frameSafetyReleaseStableFrames", float("nan")),
            "sourceContract.Quest3.frameSafetyReleaseStableFrames",
            errors,
        ),
        values["runtimeQuest3FrameSafetyReleaseStableFrames"],
        0.001,
        errors,
    )
    validate_runtime_source_fragments(somatic_source, errors)
    compare_close(
        "haptic bufferCapacity",
        read_float(limits.get("bufferCapacity", float("nan")), "sourceContract.haptic.bufferCapacity", errors),
        values["hapticBufferCapacity"],
        0.001,
        errors,
    )
    compare_close(
        "haptic durationMaxSeconds",
        read_float(limits.get("durationMaxSeconds", float("nan")), "sourceContract.haptic.durationMaxSeconds", errors),
        values["hapticMaxDurationSeconds"],
        0.001,
        errors,
    )
    compare_close(
        "haptic frequencyMaxHz",
        read_float(limits.get("frequencyMaxHz", float("nan")), "sourceContract.haptic.frequencyMaxHz", errors),
        values["hapticMaxFrequencyHz"],
        0.001,
        errors,
    )
    return values, errors


def validate_runtime_source_fragments(somatic_source: str, errors: list[str]) -> None:
    required_fragments = (
        "UpdateAccelerationComfortState",
        "ApproximateMagnitudeNoSqrt(angularAcceleration)",
        "_accelerationReleaseBelowTimer = math.min(hysteresisSeconds",
        "UpdateComfortFramePressureState(deltaTime)",
        "safeDeltaTime > frameSafetyDeltaSeconds",
        "_comfortFramePressureConsecutiveFrames = math.min(consecutiveFrames",
        "ResolveComfortFrameSafetyMinOpacity()",
        "target = math.max(target, framePressureTarget)",
        "RefreshComfortProfileSelection();",
        "_useQuest2ComfortFallback",
        "BlackBoxFlagFramePressure",
        "BlackBoxFlagQuest2Fallback",
        "BlackBoxFlagAccelerationTunnel",
        "flags |= BlackBoxFlagFramePressure",
        "flags |= BlackBoxFlagQuest2Fallback",
        "flags |= BlackBoxFlagAccelerationTunnel",
        "float maxDelta = target > _accelerationComfortVignette01",
        "math.clamp(target - _accelerationComfortVignette01",
        "AccelerationVignette01 = Sanitize01(_accelerationComfortVignette01, 0f)",
        "math.max(vignette01, math.saturate(input.AccelerationVignette01))",
        "_accelerationComfortVignette01 = 0f",
        "_accelerationReleaseBelowTimer = 0f",
        "PublishComfortVignette(0f)",
    )
    for fragment in required_fragments:
        if fragment not in somatic_source:
            errors.append(f"runtime acceleration integration missing source fragment: {fragment}")
    validate_method_fragments(
        somatic_source,
        "public void OnOriginShift",
        (
            "_accelerationComfortVignette01 = 0f",
            "_accelerationReleaseBelowTimer = 0f",
            "ResetComfortFramePressureState();",
            "PublishComfortVignette(0f)",
            "PublishShaderState();",
        ),
        errors,
    )
    validate_method_fragments_before(
        somatic_source,
        "public void OnOriginShift",
        "if (!IsFiniteVector(shiftOffset))",
        (
            "_accelerationComfortVignette01 = 0f",
            "_accelerationReleaseBelowTimer = 0f",
            "ResetComfortFramePressureState();",
            "PublishComfortVignette(0f)",
            "PublishShaderState();",
        ),
        errors,
    )
    validate_method_fragments(
        somatic_source,
        "private void ResetHeadMotionHistoryAndPublishedComfort",
        (
            "ResetHeadMotionHistory(headPosition, headRotation);",
            "PublishComfortVignette(0f)",
            "PublishShaderState();",
        ),
        errors,
    )
    validate_method_fragments(
        somatic_source,
        "private void ResetHeadMotionIfAupShifted",
        (
            "ResetHeadMotionHistoryAndPublishedComfort(headPosition, headRotation);",
        ),
        errors,
    )
    if somatic_source.count("ResetHeadMotionHistoryAndPublishedComfort(headPosition, headRotation);") < 3:
        errors.append("runtime acceleration reset helper must cover first pose, tracking jump, and AUP shift paths")
    validate_call_only_inside_method(
        somatic_source,
        "ResetHeadMotionHistory(headPosition, headRotation);",
        "private void ResetHeadMotionHistoryAndPublishedComfort",
        errors,
    )
    validate_method_fragments(
        somatic_source,
        "private void ResetHeadMotionHistory",
        ("ResetComfortFramePressureState();",),
        errors,
    )
    validate_method_fragments(
        somatic_source,
        "private void ApplyInactiveState",
        ("ResetComfortFramePressureState();",),
        errors,
    )
    if somatic_source.count("ResetComfortFramePressureState();") < 3:
        errors.append("runtime frame-pressure reset must cover origin shift, head-history, and inactive paths")
    validate_method_fragments(
        somatic_source,
        "private ushort ResolveBlackBoxFlags",
        (
            "if (_comfortFramePressureActive)",
            "flags |= BlackBoxFlagFramePressure",
            "if (_useQuest2ComfortFallback)",
            "flags |= BlackBoxFlagQuest2Fallback",
            "if (_accelerationComfortVignette01 > 0.001f)",
            "flags |= BlackBoxFlagAccelerationTunnel",
        ),
        errors,
    )


def validate_method_fragments(source: str, signature: str, fragments: tuple[str, ...], errors: list[str]) -> None:
    method_body = extract_csharp_method_body(source, signature, errors)
    if not method_body:
        return
    for fragment in fragments:
        if fragment not in method_body:
            errors.append(f"runtime method {signature} missing source fragment: {fragment}")


def validate_method_fragments_before(
    source: str,
    signature: str,
    marker: str,
    fragments: tuple[str, ...],
    errors: list[str],
) -> None:
    method_body = extract_csharp_method_body(source, signature, errors)
    if not method_body:
        return
    marker_index = method_body.find(marker)
    if marker_index < 0:
        errors.append(f"runtime method {signature} missing order marker: {marker}")
        return
    prefix = method_body[:marker_index]
    for fragment in fragments:
        if fragment not in prefix:
            errors.append(f"runtime method {signature} must run before {marker}: {fragment}")


def validate_call_only_inside_method(source: str, call: str, signature: str, errors: list[str]) -> None:
    method_body = extract_csharp_method_body(source, signature, errors)
    if not method_body:
        return
    total_calls = source.count(call)
    allowed_calls = method_body.count(call)
    if total_calls != allowed_calls:
        errors.append(f"runtime call must be routed through {signature}: {call}")


def extract_csharp_method_body(source: str, signature: str, errors: list[str]) -> str:
    signature_index = source.find(signature)
    if signature_index < 0:
        errors.append(f"runtime method missing: {signature}")
        return ""
    brace_index = source.find("{", signature_index)
    if brace_index < 0:
        errors.append(f"runtime method missing body: {signature}")
        return ""

    depth = 0
    for index in range(brace_index, len(source)):
        char = source[index]
        if char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return source[brace_index : index + 1]

    errors.append(f"runtime method unclosed body: {signature}")
    return ""


def read_text_if_exists(path: Path, errors: list[str]) -> str:
    if not path.exists():
        errors.append(f"missing source contract file: {display_path(path)}")
        return ""
    return path.read_text(encoding="utf-8")


def extract_csharp_number(source: str, name: str, errors: list[str]) -> float:
    pattern = rf"\b{name}\b\s*=\s*([-+]?[0-9]+(?:\.[0-9]+)?)(?:f|u)?"
    match = re.search(pattern, source)
    if match is None:
        errors.append(f"source contract constant not found: {name}")
        return float("nan")
    return float(match.group(1))


def extract_csharp_bit_mask(source: str, name: str, errors: list[str]) -> int:
    pattern = rf"\b{name}\b\s*=\s*(?:(\d+)\s*<<\s*(\d+)|(\d+))(?:u)?"
    match = re.search(pattern, source)
    if match is None:
        errors.append(f"source contract bit flag not found: {name}")
        return -1
    if match.group(3) is not None:
        return int(match.group(3))
    return int(match.group(1)) << int(match.group(2))


def validate_black_box_comfort_flag_bits(values: dict[str, float | int], errors: list[str]) -> None:
    expected_flags = (
        ("blackBoxFlagFramePressure", 1 << 9),
        ("blackBoxFlagQuest2Fallback", 1 << 10),
        ("blackBoxFlagAccelerationTunnel", 1 << 11),
    )
    seen: set[int] = set()
    for key, expected in expected_flags:
        actual_value = values.get(key, -1)
        actual = int(actual_value) if isinstance(actual_value, int) else -1
        if actual != expected:
            errors.append(f"black-box comfort flag mismatch {key}: expected {expected}, actual {actual}")
        if actual in seen:
            errors.append(f"black-box comfort flag overlap: {key} uses {actual}")
        seen.add(actual)


def compare_close(label: str, expected: float, actual: float, tolerance: float, errors: list[str]) -> None:
    if not math.isfinite(expected) or not math.isfinite(actual) or abs(expected - actual) > tolerance:
        errors.append(f"source contract mismatch {label}: expected {expected}, actual {actual}")


def can_simulate_profile(profile: QuestComfortProfile, jerk_profile: JerkProfile, shock_rules: ShockRules) -> bool:
    values = (
        profile.refresh_hz,
        profile.accel_soft_rad_s2,
        profile.accel_emergency_rad_s2,
        profile.opacity_max,
        profile.attack_slew_per_frame,
        profile.release_slew_per_frame,
        jerk_profile.soft_rad_s3,
        jerk_profile.full_rad_s3,
        jerk_profile.hard_cap_rad_s3,
        jerk_profile.opacity_max,
        shock_rules.max_opacity_delta_per_frame,
        shock_rules.max_untunneled_angle_delta_deg,
        shock_rules.min_opacity_for_large_angle_delta,
    )
    return all(math.isfinite(value) for value in values) and profile.refresh_hz > 0.0


def blocked_simulation_result(profile_name: str) -> dict[str, float | int | str]:
    return {
        "profile": profile_name,
        "frames": 0,
        "max_angle_delta_deg": 0.0,
        "max_opacity": 0.0,
        "max_opacity_delta": 0.0,
        "max_abs_accel_rad_s2": 0.0,
        "max_abs_jerk_rad_s3": 0.0,
        "shock_frames": 1,
    }


def display_path(path: Path) -> str:
    try:
        return str(path.relative_to(ROOT)).replace("\\", "/")
    except ValueError:
        return str(path).replace("\\", "/")


def load_json_object(path: Path, label: str, errors: list[str]) -> dict:
    if not path.exists():
        errors.append(f"{label} JSON missing: {display_path(path)}")
        return {}
    try:
        payload = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        errors.append(f"{label} JSON invalid: {display_path(path)}: {exc}")
        return {}
    if not isinstance(payload, dict):
        errors.append(f"{label} JSON root must be object: {display_path(path)}")
        return {}
    return payload


def validate_audit_test_contract() -> list[str]:
    errors: list[str] = []
    test_source = read_text_if_exists(TEST_SCRIPT_PATH, errors)
    if not test_source:
        return errors

    required_fragments = (
        "def test_report_writes_source_hashes",
        "auditTestSha256",
        "def test_report_check_rejects_stale_hashes",
        "def test_origin_shift_reset_must_precede_invalid_shift_return",
        "def test_aup_sequence_reset_requires_immediate_shader_reset",
        "def test_raw_head_history_reset_outside_helper_fails_closed",
        "def test_frame_pressure_reset_paths_fail_closed",
        "def test_black_box_comfort_flags_must_live_in_resolver",
        "def test_workspace_temp_dir_cleans_entry_and_exit",
        "import shutil",
        "remove_workspace_temp_root()",
        "finally:",
        "shutil.rmtree",
        "self.assertFalse(TEST_TEMP_ROOT.exists())",
        "def test_float_fields_reject_bool_and_numeric_strings",
        "def test_missing_audit_test_script_fails_closed",
    )
    for fragment in required_fragments:
        if fragment not in test_source:
            errors.append(f"audit test contract missing fragment: {fragment}")
    return errors


def build_audit_payload() -> dict:
    profiles, jerk_profile, shock_rules, comfort_errors = load_comfort_profile()
    waveform_count, waveform_errors = validate_waveforms()
    source_contract_values, source_contract_errors = validate_source_contract()
    audit_test_errors = validate_audit_test_contract()
    results = []
    for profile in profiles:
        if can_simulate_profile(profile, jerk_profile, shock_rules):
            results.append(simulate_profile(profile, jerk_profile, shock_rules))
        else:
            results.append(blocked_simulation_result(profile.name))
            comfort_errors.append(f"{profile.name} simulation blocked by invalid comfort numeric data")
    shock_total = sum(int(result["shock_frames"]) for result in results)
    all_errors = comfort_errors + waveform_errors + source_contract_errors + audit_test_errors
    if shock_total > 0:
        all_errors.append(f"visual teleport shock frames detected: {shock_total}")

    return {
        "schema": EXPECTED_AUDIT_SCHEMA,
        "owner": "SOMATIC_COMFORT_ANALYST",
        "status": "PASS" if not all_errors else "FAIL",
        "snapTurnDegrees": SNAP_TURN_DEGREES,
        "snapTurnSeconds": SNAP_TURN_SECONDS,
        "settleSeconds": SETTLE_SECONDS,
        "comfortProfile": display_path(COMFORT_JSON),
        "comfortMarkdown": display_path(COMFORT_MD),
        "hapticWaveforms": display_path(WAVEFORM_JSON),
        "sourceHashes": {
            "comfortProfileSha256": sha256_file(COMFORT_JSON),
            "comfortMarkdownSha256": sha256_file(COMFORT_MD),
            "hapticWaveformsSha256": sha256_file(WAVEFORM_JSON),
            "auditScriptSha256": sha256_file(SCRIPT_PATH),
            "auditTestSha256": sha256_file(TEST_SCRIPT_PATH),
            "vrSomaticProviderSha256": sha256_file(VR_SOMATIC_PROVIDER_CS),
            "toolHapticsRuntimeSha256": sha256_file(TOOL_HAPTICS_RUNTIME_CS),
        },
        "hapticWaveformCount": waveform_count,
        "sourceContract": source_contract_values,
        "results": results,
        "errors": all_errors,
    }


def json_safe_payload(value: object) -> object:
    if isinstance(value, float):
        return value if math.isfinite(value) else None
    if isinstance(value, dict):
        return {str(key): json_safe_payload(item) for key, item in value.items()}
    if isinstance(value, list):
        return [json_safe_payload(item) for item in value]
    return value


def write_report(payload: dict, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    safe_payload = json_safe_payload(payload)
    path.write_text(json.dumps(safe_payload, allow_nan=False, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def validate_report(path: Path) -> list[str]:
    if not path.exists():
        return [f"report missing: {path}"]
    try:
        report = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        return [f"report JSON invalid: {exc}"]

    current = json_safe_payload(build_audit_payload())
    errors: list[str] = []
    if report.get("schema") != EXPECTED_AUDIT_SCHEMA:
        errors.append("report schema mismatch")
    if report.get("owner") != EXPECTED_OWNER:
        errors.append("report owner mismatch")
    if report.get("status") != current.get("status"):
        errors.append("report status stale")
    if report.get("sourceHashes") != current.get("sourceHashes"):
        errors.append("report source hashes stale")
    if report.get("sourceContract") != current.get("sourceContract"):
        errors.append("report source contract stale")
    if report.get("results") != current.get("results"):
        errors.append("report simulation results stale")
    if report.get("errors") != current.get("errors"):
        errors.append("report error list stale")

    current_errors = current.get("errors", [])
    if current_errors:
        for current_error in current_errors:
            errors.append(f"current audit error: {current_error}")
    return errors


def sha256_file(path: Path) -> str:
    if not path.exists():
        return "MISSING"
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        while True:
            chunk = handle.read(65536)
            if not chunk:
                break
            digest.update(chunk)
    return digest.hexdigest()


def main(argv: list[str]) -> int:
    write_report_path: Path | None = None
    check_report_path: Path | None = None
    if len(argv) == 2 and argv[1] == "--write-report":
        write_report_path = DEFAULT_REPORT_JSON
    elif len(argv) == 3 and argv[1] == "--write-report":
        write_report_path = Path(argv[2]).resolve()
    elif len(argv) == 2 and argv[1] == "--check-report":
        check_report_path = DEFAULT_REPORT_JSON
    elif len(argv) == 3 and argv[1] == "--check-report":
        check_report_path = Path(argv[2]).resolve()
    elif len(argv) != 1:
        print("Usage: vr_snap_turn_comfort_audit.py [--write-report [path]] [--check-report [path]]")
        return 2

    payload = build_audit_payload()

    print("VR SNAP TURN COMFORT AUDIT")
    print(f"Snap turn: {SNAP_TURN_DEGREES:.1f} deg over {SNAP_TURN_SECONDS:.3f} s")
    print(f"Haptic waveforms: {payload['hapticWaveformCount']}")
    for result in payload["results"]:
        print(
            "{profile}: frames={frames} maxAngleDelta={max_angle_delta_deg:.3f}deg "
            "maxOpacity={max_opacity:.3f} maxOpacityDelta={max_opacity_delta:.3f} "
            "maxAccel={max_abs_accel_rad_s2:.3f}rad/s2 maxJerk={max_abs_jerk_rad_s3:.3f}rad/s3 "
            "shockFrames={shock_frames}".format(**result)
        )

    if write_report_path is not None:
        write_report(payload, write_report_path)
        print(f"Report: {write_report_path}")

    if check_report_path is not None:
        report_errors = validate_report(check_report_path)
        if report_errors:
            payload["errors"].extend(report_errors)
        else:
            print(f"Report check: {check_report_path}")

    if payload["errors"]:
        print("STATUS: FAIL")
        for error in payload["errors"]:
            print(f"ERROR: {error}")
        return 1

    print("STATUS: PASS - no Visual Teleport Shock")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
