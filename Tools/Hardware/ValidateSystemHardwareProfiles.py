#!/usr/bin/env python3
"""Static guard for Data/System/Hardware_Profiles.json.

This is cold-path tooling only. It protects the H8 hardware profile bake from
silent drift in row/table parity, Quest2 RAM limits, SHI thresholds, and guard
values. It does not compile Unity code and does not imply runtime verification.
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
PROFILE_PATH = ROOT / "Data" / "System" / "Hardware_Profiles.json"
REPORT_PATH = ROOT / "Docs" / "AgentLogs" / "Hardware_Profile_Audit_H8_HARDWARE_TIER_MATRIX_BKR.json"

EXPECTED_PROFILE_IDS = (
    "PC_High",
    "SteamDeck_Mid",
    "Quest2_Low",
    "Quest3_LowPlus",
)

EXPECTED_PLATFORM_CLASSES = {
    "PC_High": "PC_DISCRETE_HIGH",
    "SteamDeck_Mid": "STEAMOS_HANDHELD_UMA",
    "Quest2_Low": "ANDROID_XR_UMA",
    "Quest3_LowPlus": "ANDROID_XR_UMA",
}

EXPECTED_PROFILE_VALUES = {
    "PC_High": {
        "TargetFps": 120,
        "SystemRamLimit": 16384,
        "SystemRamBudget": 12288,
        "SystemRamSafetyReserve": 4096,
        "VramLimit": 8192,
        "CpuLaneTokenRate": 1440,
        "RenderScaleMilli": 1000,
        "TextureMipBias": 0,
        "Begin": 0.78,
        "Critical": 0.90,
        "Emergency": 0.97,
        "ReleaseHysteresis": 0.08,
    },
    "SteamDeck_Mid": {
        "TargetFps": 60,
        "SystemRamLimit": 16384,
        "SystemRamBudget": 8192,
        "SystemRamSafetyReserve": 8192,
        "VramLimit": 4096,
        "CpuLaneTokenRate": 360,
        "RenderScaleMilli": 780,
        "TextureMipBias": 1,
        "Begin": 0.62,
        "Critical": 0.78,
        "Emergency": 0.90,
        "ReleaseHysteresis": 0.08,
    },
    "Quest2_Low": {
        "TargetFps": 72,
        "SystemRamLimit": 4096,
        "SystemRamBudget": 3840,
        "SystemRamSafetyReserve": 256,
        "VramLimit": 1024,
        "CpuLaneTokenRate": 216,
        "RenderScaleMilli": 720,
        "TextureMipBias": 2,
        "Begin": 0.52,
        "Critical": 0.68,
        "Emergency": 0.82,
        "ReleaseHysteresis": 0.10,
    },
    "Quest3_LowPlus": {
        "TargetFps": 72,
        "SystemRamLimit": 8192,
        "SystemRamBudget": 5120,
        "SystemRamSafetyReserve": 3072,
        "VramLimit": 1536,
        "CpuLaneTokenRate": 360,
        "RenderScaleMilli": 850,
        "TextureMipBias": 1,
        "Begin": 0.58,
        "Critical": 0.74,
        "Emergency": 0.88,
        "ReleaseHysteresis": 0.09,
    },
}

EXPECTED_GUARDS = {
    "VramWarningUsedRatio": 0.90,
    "VramCriticalUsedRatio": 0.95,
    "FrameThrottleMs": 25.0,
    "MainThreadHardCapMs": 12.0,
    "HotSystemSuspiciousMs": 0.10,
}

EXPECTED_STRESS_INPUTS = (
    "FrameTimeTrend",
    "VramUsedRatio",
    "SystemRamUsedRatio",
    "Thermal01",
    "BatteryPressure01",
    "CpuLaneDebtRatio",
)

STRESS_WEIGHT_KEYS = (
    "FrameTimeTrendRatioWeight",
    "VramUsedRatioWeight",
    "SystemRamUsedRatioWeight",
    "Thermal01Weight",
    "BatteryPressure01Weight",
    "CpuLaneDebtRatioWeight",
)

PROFILE_TABLE_KEYS = (
    "profileId",
    "profileStableHash32",
    "profilePlatformClass",
    "profileTargetFps",
    "profileFrameBudgetMs",
    "profileSystemRamLimitMb",
    "profileSystemRamBudgetMb",
    "profileSystemRamSafetyReserveMb",
    "profileVramLimitMb",
    "profileCpuLaneTokenRate",
    "profileRenderScaleMilli",
    "profileTextureMipBias",
    "profileVasoconstrictBeginSystemStress",
    "profileCriticalSystemStress",
    "profileEmergencySystemStress",
    "profileReleaseHysteresis",
    "profileVasoconstrictSystemStressRowMajor",
    "profileStressActionRenderScaleMilliRowMajor",
    "profileStressActionTextureMipBiasRowMajor",
)

REQUIRED_PROFILE_KEYS = (
    "ProfileId",
    "StableHash32",
    "PlatformClass",
    "TargetFps",
    "FrameBudgetMs",
    "SystemRamLimit",
    "SystemRamBudget",
    "SystemRamSafetyReserve",
    "VramLimit",
    "CpuLaneTokenRate",
    "RenderScale",
    "TextureMipBias",
    "SHIThresholds",
    "VasoconstrictSystemStressByLevel",
    "StressActions",
)

REQUIRED_OVERRIDE_KEYS = (
    "VramLimit",
    "CpuLaneTokenRate",
    "RenderScale",
    "TextureMipBias",
)


def relative_path(path: Path) -> str:
    return path.resolve().relative_to(ROOT).as_posix()


def fnv1a32_ascii(text: str) -> int:
    value = 0x811C9DC5
    for byte in text.encode("ascii"):
        value ^= byte
        value = (value * 0x01000193) & 0xFFFFFFFF
    return value


def require(condition: bool, message: str, errors: list[str]) -> None:
    if not condition:
        errors.append(message)


def close_enough(actual: Any, expected: float, tolerance: float = 0.0001) -> bool:
    try:
        return abs(float(actual) - expected) <= tolerance
    except (TypeError, ValueError):
        return False


def load_catalog(path: Path = PROFILE_PATH) -> dict[str, Any]:
    with path.open("r", encoding="utf-8-sig") as handle:
        data = json.load(handle)
    if not isinstance(data, dict):
        raise ValueError("Hardware_Profiles.json root must be an object")
    return data


def profile_rows(data: dict[str, Any], errors: list[str]) -> list[dict[str, Any]]:
    rows = data.get("profiles")
    if not isinstance(rows, list):
        errors.append("profiles must be a list")
        return []
    invalid = [index for index, row in enumerate(rows) if not isinstance(row, dict)]
    require(not invalid, f"profiles contains non-object rows: {invalid}", errors)
    if invalid:
        return []
    return rows


def get_profile_map(rows: list[dict[str, Any]], errors: list[str]) -> dict[str, dict[str, Any]]:
    result: dict[str, dict[str, Any]] = {}
    for row in rows:
        profile_id = row.get("ProfileId")
        if not isinstance(profile_id, str):
            errors.append("profile row missing string ProfileId")
            continue
        if profile_id in result:
            errors.append(f"duplicate profile row: {profile_id}")
            continue
        result[profile_id] = row
    return result


def validate_root(data: dict[str, Any], errors: list[str]) -> None:
    require(data.get("schemaVersion") == 1, "schemaVersion must be 1", errors)
    require(data.get("ownerPromptId") == "H8_HARDWARE_TIER_MATRIX_BKR", "ownerPromptId drift", errors)
    require(data.get("domain") == "Echelon1.ScalabilityDictator.Hardware", "domain drift", errors)
    require(data.get("stableHashAlgorithm") == "FNV1A32_ASCII", "stable hash algorithm drift", errors)
    require(data.get("profileCount") == len(EXPECTED_PROFILE_IDS), "profileCount drift", errors)
    units = data.get("units")
    require(isinstance(units, dict), "units block missing", errors)
    if isinstance(units, dict):
        for key in REQUIRED_OVERRIDE_KEYS:
            require(key in units, f"units missing {key}", errors)
        require(units.get("SystemStress") == "normalized_0_to_1", "SystemStress unit drift", errors)


def validate_guard_thresholds(data: dict[str, Any], errors: list[str]) -> None:
    guards = data.get("guardThresholds")
    require(isinstance(guards, dict), "guardThresholds block missing", errors)
    if not isinstance(guards, dict):
        return

    for key, expected in EXPECTED_GUARDS.items():
        require(close_enough(guards.get(key), expected), f"guardThresholds.{key} drift", errors)
    require(
        guards.get("VramCriticalAction") == "force_mip_minus_2_all_streaming_textures",
        "guardThresholds.VramCriticalAction drift",
        errors,
    )
    warning = guards.get("VramWarningUsedRatio")
    critical = guards.get("VramCriticalUsedRatio")
    if isinstance(warning, (int, float)) and isinstance(critical, (int, float)):
        require(float(warning) < float(critical), "VRAM warning must be below critical", errors)


def validate_system_health_index(data: dict[str, Any], errors: list[str]) -> float:
    shi = data.get("systemHealthIndex")
    require(isinstance(shi, dict), "systemHealthIndex block missing", errors)
    if not isinstance(shi, dict):
        return 0.0

    require(shi.get("signalContract") == "SystemHealthIndexSignal", "SHI signal contract drift", errors)
    require(shi.get("healthFormula") == "Health01 = 1.0 - SystemStress", "SHI health formula drift", errors)
    require(shi.get("pressureFormula") == "Pressure01 = SystemStress", "SHI pressure formula drift", errors)
    require(tuple(shi.get("stressInputs", ())) == EXPECTED_STRESS_INPUTS, "SHI stressInputs drift", errors)
    require(int(shi.get("restoreStableFrames", 0)) >= 180, "SHI restoreStableFrames below 180", errors)

    model = shi.get("stressModel")
    require(isinstance(model, dict), "systemHealthIndex.stressModel missing", errors)
    if not isinstance(model, dict):
        return 0.0

    weight_sum = 0.0
    for key in STRESS_WEIGHT_KEYS:
        value = model.get(key)
        require(isinstance(value, (int, float)), f"stress weight missing or non-numeric: {key}", errors)
        if isinstance(value, (int, float)):
            require(0.0 <= float(value) <= 1.0, f"stress weight out of range: {key}", errors)
            weight_sum += float(value)
    require(close_enough(weight_sum, 1.0), f"stress model weights sum {weight_sum:.6f} != 1.0", errors)
    require(close_enough(model.get("ClampMin"), 0.0), "stress model ClampMin drift", errors)
    require(close_enough(model.get("ClampMax"), 1.0), "stress model ClampMax drift", errors)
    return weight_sum


def expected_frame_budget(target_fps: int) -> float:
    return 1000.0 / float(target_fps)


def validate_profile_rows(rows: list[dict[str, Any]], errors: list[str]) -> None:
    ids = [row.get("ProfileId") for row in rows]
    require(tuple(ids) == EXPECTED_PROFILE_IDS, f"profile row order drift: {ids}", errors)
    profile_map = get_profile_map(rows, errors)

    previous_begin = 1.0
    begin_order = ("PC_High", "SteamDeck_Mid", "Quest3_LowPlus", "Quest2_Low")
    begin_values: dict[str, float] = {}

    for profile_id in EXPECTED_PROFILE_IDS:
        profile = profile_map.get(profile_id)
        require(profile is not None, f"missing profile row: {profile_id}", errors)
        if profile is None:
            continue

        for key in REQUIRED_PROFILE_KEYS:
            require(key in profile, f"{profile_id} missing {key}", errors)
        for key in REQUIRED_OVERRIDE_KEYS:
            require(key in profile, f"{profile_id} missing required override {key}", errors)

        expected = EXPECTED_PROFILE_VALUES[profile_id]
        require(profile.get("StableHash32") == fnv1a32_ascii(profile_id), f"{profile_id} StableHash32 drift", errors)
        require(profile.get("PlatformClass") == EXPECTED_PLATFORM_CLASSES[profile_id], f"{profile_id} PlatformClass drift", errors)
        require(profile.get("TargetFps") == expected["TargetFps"], f"{profile_id} TargetFps drift", errors)
        require(close_enough(profile.get("FrameBudgetMs"), expected_frame_budget(expected["TargetFps"]), 0.001), f"{profile_id} FrameBudgetMs drift", errors)
        require(profile.get("SystemRamLimit") == expected["SystemRamLimit"], f"{profile_id} SystemRamLimit drift", errors)
        require(profile.get("SystemRamBudget") == expected["SystemRamBudget"], f"{profile_id} SystemRamBudget drift", errors)
        require(profile.get("SystemRamSafetyReserve") == expected["SystemRamSafetyReserve"], f"{profile_id} SystemRamSafetyReserve drift", errors)
        require(profile.get("VramLimit") == expected["VramLimit"], f"{profile_id} VramLimit drift", errors)
        require(profile.get("CpuLaneTokenRate") == expected["CpuLaneTokenRate"], f"{profile_id} CpuLaneTokenRate drift", errors)
        require(int(round(float(profile.get("RenderScale", 0.0)) * 1000.0)) == expected["RenderScaleMilli"], f"{profile_id} RenderScale drift", errors)
        require(profile.get("TextureMipBias") == expected["TextureMipBias"], f"{profile_id} TextureMipBias drift", errors)

        budget = profile.get("SystemRamBudget")
        reserve = profile.get("SystemRamSafetyReserve")
        limit = profile.get("SystemRamLimit")
        if isinstance(budget, int) and isinstance(reserve, int) and isinstance(limit, int):
            require(budget + reserve <= limit, f"{profile_id} RAM budget+reserve exceeds limit", errors)
        if profile_id == "Quest2_Low":
            require(limit == 4096, "Quest2_Low SystemRamLimit must remain 4096 MB", errors)
            if isinstance(budget, int) and isinstance(reserve, int):
                require(budget + reserve <= 4096, "Quest2_Low total committed RAM exceeds 4GB", errors)

        thresholds = profile.get("SHIThresholds")
        require(isinstance(thresholds, dict), f"{profile_id} SHIThresholds missing", errors)
        if isinstance(thresholds, dict):
            begin = thresholds.get("VasoconstrictBeginSystemStress")
            warning = thresholds.get("WarningSystemStress")
            critical = thresholds.get("CriticalSystemStress")
            emergency = thresholds.get("EmergencySystemStress")
            hysteresis = thresholds.get("ReleaseHysteresis")
            require(close_enough(begin, expected["Begin"]), f"{profile_id} begin SystemStress drift", errors)
            require(close_enough(warning, expected["Begin"]), f"{profile_id} warning SystemStress drift", errors)
            require(close_enough(critical, expected["Critical"]), f"{profile_id} critical SystemStress drift", errors)
            require(close_enough(emergency, expected["Emergency"]), f"{profile_id} emergency SystemStress drift", errors)
            require(close_enough(hysteresis, expected["ReleaseHysteresis"]), f"{profile_id} ReleaseHysteresis drift", errors)
            if all(isinstance(value, (int, float)) for value in (begin, critical, emergency, hysteresis)):
                require(0.0 < float(begin) < float(critical) < float(emergency) < 1.0, f"{profile_id} SHI thresholds not monotonic", errors)
                require(float(hysteresis) >= 0.08, f"{profile_id} ReleaseHysteresis below 0.08", errors)
                begin_values[profile_id] = float(begin)

        levels = profile.get("VasoconstrictSystemStressByLevel")
        require(isinstance(levels, list), f"{profile_id} VasoconstrictSystemStressByLevel missing", errors)
        if isinstance(levels, list) and isinstance(thresholds, dict):
            expected_levels = [0.0, expected["Begin"], expected["Critical"], expected["Emergency"]]
            require(len(levels) == 4, f"{profile_id} VasoconstrictSystemStressByLevel length drift", errors)
            for index, expected_level in enumerate(expected_levels):
                if index < len(levels):
                    require(close_enough(levels[index], expected_level), f"{profile_id} stress level {index} drift", errors)

        actions = profile.get("StressActions")
        require(isinstance(actions, dict), f"{profile_id} StressActions missing", errors)
        if isinstance(actions, dict):
            render_scales = [
                actions.get("Level1RenderScale"),
                actions.get("Level2RenderScale"),
                actions.get("Level3RenderScale"),
            ]
            mip_biases = [
                actions.get("Level1TextureMipBias"),
                actions.get("Level2TextureMipBias"),
                actions.get("Level3TextureMipBias"),
            ]
            base_scale = float(profile.get("RenderScale", 0.0))
            base_mip = int(profile.get("TextureMipBias", 0))
            if all(isinstance(value, (int, float)) for value in render_scales):
                require(base_scale >= float(render_scales[0]) >= float(render_scales[1]) >= float(render_scales[2]) > 0.0, f"{profile_id} render-scale stress actions not descending", errors)
            if all(isinstance(value, int) for value in mip_biases):
                require(base_mip <= mip_biases[0] <= mip_biases[1] <= mip_biases[2], f"{profile_id} mip stress actions not ascending", errors)

    for profile_id in begin_order:
        begin = begin_values.get(profile_id)
        if begin is None:
            continue
        require(begin <= previous_begin, f"{profile_id} begin threshold ordering drift", errors)
        previous_begin = begin


def validate_vasoconstrict_levels(data: dict[str, Any], errors: list[str]) -> None:
    levels = data.get("vasoconstrictLevels")
    require(isinstance(levels, list), "vasoconstrictLevels missing", errors)
    if not isinstance(levels, list):
        return

    require(len(levels) == 4, "vasoconstrictLevels length drift", errors)
    previous_systems: set[str] = set()
    for index, level in enumerate(levels):
        require(isinstance(level, dict), f"vasoconstrictLevels[{index}] must be object", errors)
        if not isinstance(level, dict):
            continue
        require(level.get("level") == index, f"vasoconstrictLevels[{index}].level drift", errors)
        systems = level.get("systemsSacrificed")
        require(isinstance(systems, list), f"vasoconstrictLevels[{index}].systemsSacrificed missing", errors)
        if isinstance(systems, list):
            current_systems = set(str(item) for item in systems)
            require(previous_systems.issubset(current_systems), f"vasoconstrictLevels[{index}] is not a superset of prior level", errors)
            previous_systems = current_systems


def validate_profile_table(data: dict[str, Any], rows: list[dict[str, Any]], errors: list[str]) -> None:
    table = data.get("profileTable")
    require(isinstance(table, dict), "profileTable missing", errors)
    if not isinstance(table, dict):
        return

    profile_count = int(data.get("profileCount", 0))
    for key in PROFILE_TABLE_KEYS:
        require(key in table, f"profileTable missing {key}", errors)

    for key, value in table.items():
        if key.endswith("RowMajor"):
            continue
        if isinstance(value, list):
            require(len(value) == profile_count, f"profileTable.{key} length {len(value)} != profileCount {profile_count}", errors)

    require(len(table.get("profileVasoconstrictSystemStressRowMajor", [])) == profile_count * 4, "profile stress row-major length drift", errors)
    require(len(table.get("profileStressActionRenderScaleMilliRowMajor", [])) == profile_count * 3, "profile render-scale action row-major length drift", errors)
    require(len(table.get("profileStressActionTextureMipBiasRowMajor", [])) == profile_count * 3, "profile mip action row-major length drift", errors)

    row_map = get_profile_map(rows, errors)
    table_ids = table.get("profileId")
    require(tuple(table_ids or ()) == EXPECTED_PROFILE_IDS, f"profileTable.profileId order drift: {table_ids}", errors)

    for index, profile_id in enumerate(EXPECTED_PROFILE_IDS):
        profile = row_map.get(profile_id)
        if profile is None:
            continue
        expected = EXPECTED_PROFILE_VALUES[profile_id]
        thresholds = profile.get("SHIThresholds", {})
        actions = profile.get("StressActions", {})

        mirrors = (
            ("profileStableHash32", fnv1a32_ascii(profile_id)),
            ("profilePlatformClass", EXPECTED_PLATFORM_CLASSES[profile_id]),
            ("profileTargetFps", profile.get("TargetFps")),
            ("profileSystemRamLimitMb", profile.get("SystemRamLimit")),
            ("profileSystemRamBudgetMb", profile.get("SystemRamBudget")),
            ("profileSystemRamSafetyReserveMb", profile.get("SystemRamSafetyReserve")),
            ("profileVramLimitMb", profile.get("VramLimit")),
            ("profileCpuLaneTokenRate", profile.get("CpuLaneTokenRate")),
            ("profileRenderScaleMilli", int(round(float(profile.get("RenderScale", 0.0)) * 1000.0))),
            ("profileTextureMipBias", profile.get("TextureMipBias")),
            ("profileVasoconstrictBeginSystemStress", thresholds.get("VasoconstrictBeginSystemStress") if isinstance(thresholds, dict) else None),
            ("profileCriticalSystemStress", thresholds.get("CriticalSystemStress") if isinstance(thresholds, dict) else None),
            ("profileEmergencySystemStress", thresholds.get("EmergencySystemStress") if isinstance(thresholds, dict) else None),
            ("profileReleaseHysteresis", thresholds.get("ReleaseHysteresis") if isinstance(thresholds, dict) else None),
        )
        for key, expected_value in mirrors:
            values = table.get(key, [])
            if index >= len(values):
                continue
            actual = values[index]
            if isinstance(expected_value, float):
                require(close_enough(actual, expected_value), f"profileTable.{key}[{index}] parity drift", errors)
            else:
                require(actual == expected_value, f"profileTable.{key}[{index}] parity drift", errors)

        frame_values = table.get("profileFrameBudgetMs", [])
        if index < len(frame_values):
            require(close_enough(frame_values[index], profile.get("FrameBudgetMs"), 0.001), f"profileTable.profileFrameBudgetMs[{index}] parity drift", errors)

        stress_start = index * 4
        stress_values = table.get("profileVasoconstrictSystemStressRowMajor", [])[stress_start : stress_start + 4]
        expected_stress = [0.0, expected["Begin"], expected["Critical"], expected["Emergency"]]
        require(len(stress_values) == 4, f"{profile_id} stress row-major slice length drift", errors)
        for level_index, expected_value in enumerate(expected_stress):
            if level_index < len(stress_values):
                require(close_enough(stress_values[level_index], expected_value), f"{profile_id} row-major stress level {level_index} drift", errors)

        action_start = index * 3
        action_render = table.get("profileStressActionRenderScaleMilliRowMajor", [])[action_start : action_start + 3]
        action_mip = table.get("profileStressActionTextureMipBiasRowMajor", [])[action_start : action_start + 3]
        if isinstance(actions, dict):
            expected_render = [
                int(round(float(actions.get("Level1RenderScale", 0.0)) * 1000.0)),
                int(round(float(actions.get("Level2RenderScale", 0.0)) * 1000.0)),
                int(round(float(actions.get("Level3RenderScale", 0.0)) * 1000.0)),
            ]
            expected_mip = [
                actions.get("Level1TextureMipBias"),
                actions.get("Level2TextureMipBias"),
                actions.get("Level3TextureMipBias"),
            ]
            require(action_render == expected_render, f"{profile_id} row-major render action drift", errors)
            require(action_mip == expected_mip, f"{profile_id} row-major mip action drift", errors)


def validate_self_audit(data: dict[str, Any], rows: list[dict[str, Any]], errors: list[str]) -> None:
    audit = data.get("selfAudit")
    require(isinstance(audit, dict), "selfAudit missing", errors)
    if not isinstance(audit, dict):
        return

    columnar = audit.get("ColumnarParity")
    require(isinstance(columnar, dict), "selfAudit.ColumnarParity missing", errors)
    if isinstance(columnar, dict):
        for key in ("ProfileCountMatches", "RequiredProfileIdsPresent", "RequiredOverrideKeysPresent"):
            require(columnar.get(key) is True, f"selfAudit.ColumnarParity.{key} must be true", errors)
        require(columnar.get("RowMajorStressLevelCount") == 4, "selfAudit row-major stress level count drift", errors)
        require(columnar.get("RowMajorStressActionLevelCount") == 3, "selfAudit row-major stress action count drift", errors)
        require(columnar.get("status") == "PASS", "selfAudit.ColumnarParity status drift", errors)

    row_map = get_profile_map(rows, errors)
    quest2 = row_map.get("Quest2_Low")
    quest2_audit = audit.get("Quest2_Low")
    require(isinstance(quest2_audit, dict), "selfAudit.Quest2_Low missing", errors)
    if isinstance(quest2_audit, dict) and isinstance(quest2, dict):
        total = int(quest2.get("SystemRamBudget", 0)) + int(quest2.get("SystemRamSafetyReserve", 0))
        require(quest2_audit.get("SystemRamLimit") == quest2.get("SystemRamLimit"), "Quest2 selfAudit SystemRamLimit drift", errors)
        require(quest2_audit.get("SystemRamBudget") == quest2.get("SystemRamBudget"), "Quest2 selfAudit SystemRamBudget drift", errors)
        require(quest2_audit.get("SystemRamSafetyReserve") == quest2.get("SystemRamSafetyReserve"), "Quest2 selfAudit SystemRamSafetyReserve drift", errors)
        require(quest2_audit.get("TotalCommittedPlusReserve") == total, "Quest2 selfAudit total drift", errors)
        require(quest2_audit.get("DoesNotExceed4Gb") is True, "Quest2 selfAudit DoesNotExceed4Gb must be true", errors)
        require(quest2_audit.get("status") == "PASS", "Quest2 selfAudit status drift", errors)


def build_report(data: dict[str, Any], errors: list[str], weight_sum: float, profile_path: Path) -> dict[str, Any]:
    rows = data.get("profiles", [])
    profile_ids = [row.get("ProfileId") for row in rows if isinstance(row, dict)]
    quest2_total = 0
    for row in rows:
        if isinstance(row, dict) and row.get("ProfileId") == "Quest2_Low":
            quest2_total = int(row.get("SystemRamBudget", 0)) + int(row.get("SystemRamSafetyReserve", 0))

    return {
        "status": "PASS" if not errors else "FAIL",
        "catalogPath": relative_path(profile_path),
        "ownerPromptId": data.get("ownerPromptId"),
        "profileCount": data.get("profileCount"),
        "profileIds": profile_ids,
        "quest2TotalCommittedPlusReserveMb": quest2_total,
        "stressWeightSum": round(weight_sum, 6),
        "guardThresholds": data.get("guardThresholds", {}),
        "hotPathImpactMicroseconds": 0,
        "runtimeGcImpactBytesPerFrame": 0,
        "compileStatus": "NOT_COMPILED_STATIC_JSON_ONLY",
        "unityRuntimeStatus": "NOT_VERIFIED_STATIC_TOOL_ONLY",
        "errors": errors,
    }


def validate_data(data: dict[str, Any], profile_path: Path = PROFILE_PATH) -> tuple[list[str], dict[str, Any]]:
    errors: list[str] = []
    validate_root(data, errors)
    validate_guard_thresholds(data, errors)
    weight_sum = validate_system_health_index(data, errors)
    rows = profile_rows(data, errors)
    validate_profile_rows(rows, errors)
    validate_vasoconstrict_levels(data, errors)
    validate_profile_table(data, rows, errors)
    validate_self_audit(data, rows, errors)
    report = build_report(data, errors, weight_sum, profile_path)
    return errors, report


def write_report(path: Path, report: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")


def check_report(path: Path, report: dict[str, Any], errors: list[str]) -> None:
    if not path.exists():
        errors.append(f"missing report: {relative_path(path)}")
        return
    with path.open("r", encoding="utf-8-sig") as handle:
        stored = json.load(handle)
    if stored != report:
        errors.append(f"report drift: {relative_path(path)}")


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Validate Data/System/Hardware_Profiles.json")
    parser.add_argument("--path", type=Path, default=PROFILE_PATH, help="Profile JSON path")
    parser.add_argument("--report", type=Path, default=REPORT_PATH, help="Audit report JSON path")
    parser.add_argument("--write-report", action="store_true", help="Write deterministic audit report")
    parser.add_argument("--check-report", action="store_true", help="Require existing report to match current audit")
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(sys.argv[1:] if argv is None else argv)
    data = load_catalog(args.path)
    errors, report = validate_data(data, args.path)

    if args.write_report:
        write_report(args.report, report)
    if args.check_report:
        check_report(args.report, report, errors)

    if errors:
        print("SYSTEM_HARDWARE_PROFILE_GUARD=FAIL", file=sys.stderr)
        for error in errors:
            print(error, file=sys.stderr)
        return 1

    print(
        "SYSTEM_HARDWARE_PROFILE_GUARD=PASS "
        f"profiles={report['profileCount']} "
        f"quest2_total_mb={report['quest2TotalCommittedPlusReserveMb']} "
        f"stress_weight_sum={report['stressWeightSum']:.6f} "
        "hot_path_us=0"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
