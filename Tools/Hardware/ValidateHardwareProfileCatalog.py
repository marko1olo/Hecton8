#!/usr/bin/env python3
"""Static guard for hardware profile JSON and the C# runtime catalog.

Cold-path tooling only. This script does not compile Unity code; it verifies
that Data/Hardware/Profiles.json and HardwareProfileCatalog.cs still describe
the same generated profile constants and phase budgets.
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path


PROFILE_PREFIXES = {
    "QUEST_3": "Quest3",
    "STEAM_DECK_LCD": "SteamDeckLcd",
}

PHASE_ENUM_ORDER = (
    "PreSimulation",
    "Simulation",
    "PostSimulation",
    "VisualSync",
)

PRESSURE_CONSTANTS = {
    "CLEAR": "ClearPressureMask",
    "SACRIFICE_LEVEL_1": "SacrificeLevel1Mask",
    "SACRIFICE_LEVEL_2": "SacrificeLevel2Mask",
    "EMERGENCY_LEVEL_3": "EmergencyLevel3Mask",
}

SPLIT_PROFILE_FILES = {
    "QUEST_3": "HARDWARE_TIER_QUEST_3.json",
    "STEAM_DECK_LCD": "HARDWARE_TIER_STEAM_DECK_LCD.json",
}

PROFILE_FIELD_CONSTANTS = (
    ("profileStableHash32", "StableHash32"),
    ("profileGraphicsBudgetMb", "GraphicsBudgetMegabytes"),
    ("profileTextureBudgetMb", "TextureBudgetMegabytes"),
    ("profileRenderTargetBudgetMb", "RenderTargetBudgetMegabytes"),
    ("profileTargetFps", "TargetFps"),
    ("profileBaselineRenderScaleMilli", "BaselineRenderScaleMilli"),
    ("profileJobWorkerBudget", "JobWorkerBudget"),
)

SPLIT_PROFILE_FIELDS = (
    "profileStableHash32",
    "profileDisplayName",
    "profilePlatformClass",
    "profileGpuClass",
    "profileTierIndex",
    "profileTargetFps",
    "profileTargetFpsKind",
    "profileRefreshHzNominal",
    "profileRefreshHzMax",
    "profileFrameBudgetMs",
    "profileCpuPhysicalCores",
    "profileCpuCoreCountKind",
    "profileCpuHardwareThreads",
    "profileCpuHardwareThreadKind",
    "profileJobWorkerBudget",
    "profileDefaultComputeGroupThreads",
    "profileDedicatedVramMb",
    "profileUnifiedMemoryMb",
    "profileGraphicsBudgetMb",
    "profileTextureBudgetMb",
    "profileRenderTargetBudgetMb",
    "profileMemoryBandwidthGBs",
    "profileMemoryBandwidthKind",
    "profileMemoryBandwidthFormula",
    "profileMemoryBusBits",
    "profileMemoryDataRateMTs",
    "profileVrsAllowed",
    "profileFixedFoveationAllowed",
    "profileDynamicResolutionAllowed",
    "profileBaselineRenderScaleMilli",
)

SPLIT_SACRIFICE_FIELDS = (
    ("sacrificeLevel1MaskHex", "profileSacrificeLevel1MaskHex"),
    ("sacrificeLevel2MaskHex", "profileSacrificeLevel2MaskHex"),
    ("sacrificeLevel3MaskHex", "profileSacrificeLevel3MaskHex"),
    ("sacrificeLevel1FrameMs", "profileSacrificeLevel1FrameMs"),
    ("sacrificeLevel2FrameMs", "profileSacrificeLevel2FrameMs"),
    ("sacrificeLevel3FrameMs", "profileSacrificeLevel3FrameMs"),
    ("sacrificeLevel1MemoryRatio", "profileSacrificeLevel1MemoryRatio"),
    ("sacrificeLevel2MemoryRatio", "profileSacrificeLevel2MemoryRatio"),
    ("sacrificeLevel1BatteryRatio", "profileSacrificeLevel1BatteryRatio"),
    ("sacrificeLevel1Thermal01", "profileSacrificeLevel1Thermal01"),
)

FORBIDDEN_CATALOG_PATTERNS = (
    "new ",
    "List<",
    "Dictionary<",
    "IEnumerable",
    "System.Linq",
    "FindObject",
    "GameObject.Find",
    "Resources.Load",
    "Update()",
    "FixedUpdate()",
    "LateUpdate()",
)


def parse_int_literal(value: str) -> int:
    cleaned = value.strip().replace("_", "")
    while cleaned and cleaned[-1] in "uUlLfF":
        cleaned = cleaned[:-1]
    return int(cleaned, 16 if cleaned.lower().startswith("0x") else 10)


def fnv1a32_ascii(text: str) -> int:
    value = 0x811C9DC5
    for byte in text.encode("ascii"):
        value ^= byte
        value = (value * 0x01000193) & 0xFFFFFFFF
    return value


def require(condition: bool, message: str, errors: list[str]) -> None:
    if not condition:
        errors.append(message)


def load_json(path: Path) -> dict:
    with path.open("r", encoding="utf-8-sig") as handle:
        data = json.load(handle)
    if not isinstance(data, dict):
        raise ValueError("Profiles.json root must be an object")
    return data


def extract_constants(csharp: str) -> dict[str, int]:
    constants: dict[str, int] = {}
    pattern = re.compile(
        r"public\s+const\s+(?:int|uint|ulong)\s+([A-Za-z0-9_]+)\s*=\s*([^;]+);"
    )
    for match in pattern.finditer(csharp):
        constants[match.group(1)] = parse_int_literal(match.group(2))
    return constants


def extract_method_body(text: str, method_name: str) -> str:
    signature = re.search(
        rf"\b(?:private|internal|public)\s+static\s+(?:[A-Za-z0-9_<>,\[\].]+\s+)+{re.escape(method_name)}\s*\(",
        text,
    )
    if signature is None:
        raise ValueError(f"Missing method declaration {method_name}")

    brace_index = text.find("{", signature.end())
    if brace_index < 0:
        raise ValueError(f"Missing method body for {method_name}")

    depth = 0
    for index in range(brace_index, len(text)):
        char = text[index]
        if char == "{":
            depth += 1
            continue
        if char == "}":
            depth -= 1
            if depth == 0:
                return text[brace_index + 1 : index]

    raise ValueError(f"Unterminated method body for {method_name}")


def extract_phase_returns(csharp: str, method_name: str) -> dict[str, float]:
    body = extract_method_body(csharp, method_name)
    returns: dict[str, float] = {}
    pattern = re.compile(
        r"case\s+HardwareProfilePhase\.([A-Za-z0-9_]+):\s*return\s+([0-9.]+)f;",
        re.MULTILINE,
    )
    for match in pattern.finditer(body):
        returns[match.group(1)] = float(match.group(2))
    return returns


def validate_flat_layout(data: dict, errors: list[str]) -> None:
    for key, value in data.items():
        require(not isinstance(value, dict), f"nested object is not flat: {key}", errors)
        if isinstance(value, list):
            nested = [index for index, item in enumerate(value) if isinstance(item, (dict, list))]
            require(not nested, f"nested list/object entries in {key}: {nested}", errors)

    declared_prefixes = (
        ("phase", "phaseCount"),
        ("profile", "profileCount"),
        ("tier", "tierCount"),
        ("pressure", "pressureLevelCount"),
        ("killSwitchBit", "killSwitchBitCount"),
        ("reference", "referenceDeviceCount"),
        ("source", "sourceCount"),
    )
    row_major_keys = {
        "profilePhaseBudgetMsRowMajor",
        "tierPhaseBudgetMsRowMajor",
    }

    for prefix, count_key in declared_prefixes:
        count = int(data[count_key])
        for key, value in data.items():
            if key in row_major_keys or key == count_key:
                continue
            if key.startswith(prefix) and isinstance(value, list):
                require(len(value) == count, f"{key} length {len(value)} != {count_key} {count}", errors)

    phase_count = int(data["phaseCount"])
    profile_count = int(data["profileCount"])
    tier_count = int(data["tierCount"])
    require(
        len(data["profilePhaseBudgetMsRowMajor"]) == profile_count * phase_count,
        "profilePhaseBudgetMsRowMajor length mismatch",
        errors,
    )
    require(
        len(data["tierPhaseBudgetMsRowMajor"]) == tier_count * phase_count,
        "tierPhaseBudgetMsRowMajor length mismatch",
        errors,
    )


def validate_hashes(data: dict, errors: list[str]) -> None:
    require(data["stableHashAlgorithm"] == "FNV1A32_ASCII", "unexpected stable hash algorithm", errors)
    hash_pairs = (
        ("phaseName", "phaseStableHash32"),
        ("profileId", "profileStableHash32"),
        ("tierName", "tierStableHash32"),
        ("referenceDeviceId", "referenceDeviceStableHash32"),
    )
    for names_key, hashes_key in hash_pairs:
        for name, expected in zip(data[names_key], data[hashes_key]):
            require(fnv1a32_ascii(name) == int(expected), f"{hashes_key} drift for {name}", errors)


def validate_catalog_constants(data: dict, constants: dict[str, int], errors: list[str]) -> None:
    require(constants.get("ProfileCount") == data["profileCount"], "ProfileCount drift", errors)
    require(constants.get("PhaseCount") == data["phaseCount"], "PhaseCount drift", errors)

    profile_ids = data["profileId"]
    require(profile_ids == list(PROFILE_PREFIXES.keys()), f"unexpected generated profile order: {profile_ids}", errors)

    for profile_index, profile_id in enumerate(profile_ids):
        prefix = PROFILE_PREFIXES[profile_id]
        for json_key, constant_suffix in PROFILE_FIELD_CONSTANTS:
            constant_name = f"{prefix}{constant_suffix}"
            require(
                constants.get(constant_name) == int(data[json_key][profile_index]),
                f"{constant_name} drift from {json_key}",
                errors,
            )

    default_threads = data["profileDefaultComputeGroupThreads"]
    require(len(set(default_threads)) == 1, "profileDefaultComputeGroupThreads are not uniform", errors)
    require(
        constants.get("DefaultComputeGroupThreads") == int(default_threads[0]),
        "DefaultComputeGroupThreads drift",
        errors,
    )

    for name, mask_hex in zip(data["pressureLevelName"], data["pressureMaskHex"]):
        constant_name = PRESSURE_CONSTANTS.get(name)
        require(constant_name is not None, f"unmapped pressure level: {name}", errors)
        if constant_name is not None:
            require(
                constants.get(constant_name) == parse_int_literal(mask_hex),
                f"{constant_name} drift from pressureMaskHex",
                errors,
            )


def validate_phase_methods(data: dict, csharp: str, errors: list[str]) -> None:
    phase_count = int(data["phaseCount"])
    budgets = data["profilePhaseBudgetMsRowMajor"]
    profile_methods = (
        ("QUEST_3", "ResolveQuest3PhaseBudgetMilliseconds"),
        ("STEAM_DECK_LCD", "ResolveSteamDeckPhaseBudgetMilliseconds"),
    )
    for profile_index, (profile_id, method_name) in enumerate(profile_methods):
        returns = extract_phase_returns(csharp, method_name)
        require(set(returns) == set(PHASE_ENUM_ORDER), f"{method_name} phase return coverage drift", errors)
        start = profile_index * phase_count
        expected_row = budgets[start : start + phase_count]
        for phase_index, phase_name in enumerate(PHASE_ENUM_ORDER):
            expected = float(expected_row[phase_index])
            actual = returns.get(phase_name)
            require(actual is not None, f"{method_name} missing {phase_name}", errors)
            if actual is not None:
                require(abs(actual - expected) <= 0.0001, f"{profile_id} {phase_name} budget drift", errors)


def validate_split_profile_jsons(root: Path, data: dict, errors: list[str]) -> None:
    for profile_id, file_name in SPLIT_PROFILE_FILES.items():
        split_path = root / "Data" / "Hardware" / file_name
        require(split_path.exists(), f"missing split profile JSON: {file_name}", errors)
        if not split_path.exists():
            continue

        split = load_json(split_path)
        for key, value in split.items():
            require(not isinstance(value, dict), f"{file_name} nested object is not flat: {key}", errors)
            if isinstance(value, list):
                nested = [index for index, item in enumerate(value) if isinstance(item, (dict, list))]
                require(not nested, f"{file_name} nested list/object entries in {key}: {nested}", errors)

        require(split.get("schema") == "H8_HARDWARE_TIER_PROFILE_V1", f"{file_name} schema drift", errors)
        require(split.get("sourceCatalog") == "Data/Hardware/Profiles.json", f"{file_name} sourceCatalog drift", errors)
        require(split.get("stableHashAlgorithm") == data["stableHashAlgorithm"], f"{file_name} hash algorithm drift", errors)
        require(split.get("profileId") == profile_id, f"{file_name} profileId drift", errors)

        if profile_id not in data["profileId"]:
            errors.append(f"catalog missing profile row for {profile_id}")
            continue

        profile_index = data["profileId"].index(profile_id)
        for key in SPLIT_PROFILE_FIELDS:
            expected = data[key][profile_index]
            actual = split.get(key)
            if actual is None:
                errors.append(f"{file_name} {key} missing")
                continue
            if isinstance(expected, float):
                require(abs(float(actual) - expected) <= 0.0001, f"{file_name} {key} drift", errors)
            else:
                require(actual == expected, f"{file_name} {key} drift", errors)

        phase_count = int(data["phaseCount"])
        phase_start = profile_index * phase_count
        expected_phase_budget = data["profilePhaseBudgetMsRowMajor"][phase_start : phase_start + phase_count]
        require(split.get("phaseIds") == data["phaseName"], f"{file_name} phaseIds drift", errors)
        actual_phase_budget = split.get("phaseBudgetMs")
        require(isinstance(actual_phase_budget, list), f"{file_name} phaseBudgetMs missing", errors)
        if isinstance(actual_phase_budget, list):
            require(len(actual_phase_budget) == phase_count, f"{file_name} phaseBudgetMs length drift", errors)
            for index, expected in enumerate(expected_phase_budget):
                if index < len(actual_phase_budget):
                    require(abs(float(actual_phase_budget[index]) - expected) <= 0.0001, f"{file_name} phaseBudgetMs[{index}] drift", errors)

        for split_key, catalog_key in SPLIT_SACRIFICE_FIELDS:
            expected = data[catalog_key][profile_index]
            actual = split.get(split_key)
            if actual is None:
                errors.append(f"{file_name} {split_key} missing")
                continue
            if isinstance(expected, float):
                require(abs(float(actual) - expected) <= 0.0001, f"{file_name} {split_key} drift", errors)
            else:
                require(actual == expected, f"{file_name} {split_key} drift", errors)


def validate_call_sites(
    catalog: str,
    detector: str,
    enforcer: str,
    governor: str,
    bootstrapper: str,
    budget_thresholds: str,
    vram_monitor: str,
    pressure_monitor: str,
    errors: list[str],
) -> None:
    for forbidden in FORBIDDEN_CATALOG_PATTERNS:
        require(forbidden not in catalog, f"forbidden runtime catalog pattern: {forbidden}", errors)

    require(
        "ResolveSharedMemoryGraphicsBudgetMegabytes(" in detector,
        "HardwareTierDetector does not call shared-memory graphics budget resolver",
        errors,
    )
    require(
        "GenericSharedMemoryVramBudgetMegabytes" in detector,
        "HardwareTierDetector fallback budget not visible at resolver call-site",
        errors,
    )
    require(
        "ResolveSharedMemoryTextureBudgetMegabytes(" in enforcer,
        "VRAMEnforcer does not call shared-memory texture budget resolver",
        errors,
    )
    require(
        "SteamDeckLcdTextureBudgetMegabytes" in enforcer,
        "VRAMEnforcer no longer gates Steam Deck texture budget",
        errors,
    )
    require(
        "HardwareProfileCatalog.Quest3BaselineRenderScaleMilli" in governor,
        "PlatformAdaptiveBudgetGovernor is missing Quest 3 catalog render-scale split",
        errors,
    )
    require(
        "HardwareTierDetector.IsQuest3Like" in governor,
        "PlatformAdaptiveBudgetGovernor does not route Quest 3 shared-memory render scale",
        errors,
    )
    require(
        "HardwareProfileCatalog.SteamDeckLcdBaselineRenderScaleMilli" in governor,
        "PlatformAdaptiveBudgetGovernor is missing Steam Deck catalog render-scale split",
        errors,
    )
    require(
        "ResolveTargetFrameTimeMs" in governor and "HardwareProfileCatalog.Quest3TargetFps" in governor,
        "PlatformAdaptiveBudgetGovernor does not derive Quest 3 frame pressure from target FPS",
        errors,
    )
    require(
        "ResolveTargetFrameTimeMs" in governor and "HardwareProfileCatalog.SteamDeckLcdTargetFps" in governor,
        "PlatformAdaptiveBudgetGovernor does not derive Steam Deck frame pressure from target FPS",
        errors,
    )
    require(
        "HardwareProfileCatalog.Quest3TargetFps" in bootstrapper,
        "GameBootstrapper does not route Quest 3 target FPS from catalog",
        errors,
    )
    require(
        "HardwareProfileCatalog.SteamDeckLcdTargetFps" in bootstrapper,
        "GameBootstrapper does not route Steam Deck target FPS from catalog",
        errors,
    )
    require(
        "HardwareProfileCatalog.Quest3TextureBudgetMegabytes" in bootstrapper,
        "GameBootstrapper does not route Quest 3 streaming mip budget from catalog",
        errors,
    )
    require(
        "HardwareProfileCatalog.SteamDeckLcdTextureBudgetMegabytes" in bootstrapper,
        "GameBootstrapper does not route Steam Deck streaming mip budget from catalog",
        errors,
    )
    require(
        "HardwareProfileCatalog.Quest3JobWorkerBudget" in bootstrapper,
        "GameBootstrapper does not route Quest 3 job worker budget from catalog",
        errors,
    )
    require(
        "HardwareProfileCatalog.SteamDeckLcdJobWorkerBudget" in bootstrapper,
        "GameBootstrapper does not route Steam Deck job worker budget from catalog",
        errors,
    )
    require(
        "VRAMBudgetThresholds RuntimeDefault" in budget_thresholds,
        "VRAMBudgetThresholds is missing profile-aware runtime defaults",
        errors,
    )
    require(
        "HardwareProfileCatalog.Quest3RenderTargetBudgetMegabytes" in budget_thresholds,
        "VRAMBudgetThresholds does not consume Quest 3 RT budget",
        errors,
    )
    require(
        "HardwareProfileCatalog.SteamDeckLcdRenderTargetBudgetMegabytes" in budget_thresholds,
        "VRAMBudgetThresholds does not consume Steam Deck RT budget",
        errors,
    )
    require(
        "VRAMBudgetThresholds.ResolveRuntimeBudget(_budgetThresholds)" in vram_monitor,
        "VRAMMonitor does not preserve custom thresholds while applying runtime profile defaults",
        errors,
    )
    require(
        "IsUnsetBudget(current) || IsDefaultBudget(current)" in budget_thresholds,
        "VRAMBudgetThresholds does not recover unset serialized budgets",
        errors,
    )
    require(
        "VRAMBudgetThresholds.RuntimeDefault" in pressure_monitor,
        "VRAMPressureMonitor does not cache profile-aware runtime thresholds",
        errors,
    )


def validate() -> tuple[list[str], dict[str, int]]:
    root = Path(__file__).resolve().parents[2]
    data_path = root / "Data" / "Hardware" / "Profiles.json"
    catalog_path = root / "Assets" / "_Project" / "Scripts" / "Core" / "HardwareProfileCatalog.cs"
    detector_path = root / "Assets" / "_Project" / "Scripts" / "Core" / "HardwareTierDetector.cs"
    enforcer_path = root / "Assets" / "_Project" / "Scripts" / "Optimization" / "VRAMEnforcer.cs"
    governor_path = root / "Assets" / "_Project" / "Scripts" / "Core" / "PlatformAdaptiveBudgetGovernor.cs"
    bootstrapper_path = root / "Assets" / "_Project" / "Scripts" / "Bootstrap" / "GameBootstrapper.cs"
    budget_thresholds_path = root / "Assets" / "_Project" / "Scripts" / "Optimization" / "VRAMBudgetThresholds.cs"
    vram_monitor_path = root / "Assets" / "_Project" / "Scripts" / "Optimization" / "VRAMMonitor.cs"
    pressure_monitor_path = root / "Assets" / "_Project" / "Scripts" / "Optimization" / "VRAMPressureMonitor.cs"

    data = load_json(data_path)
    catalog = catalog_path.read_text(encoding="utf-8-sig")
    detector = detector_path.read_text(encoding="utf-8-sig")
    enforcer = enforcer_path.read_text(encoding="utf-8-sig")
    governor = governor_path.read_text(encoding="utf-8-sig")
    bootstrapper = bootstrapper_path.read_text(encoding="utf-8-sig")
    budget_thresholds = budget_thresholds_path.read_text(encoding="utf-8-sig")
    vram_monitor = vram_monitor_path.read_text(encoding="utf-8-sig")
    pressure_monitor = pressure_monitor_path.read_text(encoding="utf-8-sig")
    constants = extract_constants(catalog)

    errors: list[str] = []
    validate_flat_layout(data, errors)
    validate_hashes(data, errors)
    validate_catalog_constants(data, constants, errors)
    validate_phase_methods(data, catalog, errors)
    validate_split_profile_jsons(root, data, errors)
    validate_call_sites(
        catalog,
        detector,
        enforcer,
        governor,
        bootstrapper,
        budget_thresholds,
        vram_monitor,
        pressure_monitor,
        errors)
    stats = {
        "profiles": int(data["profileCount"]),
        "phases": int(data["phaseCount"]),
        "masks": int(data["pressureLevelCount"]),
        "constants": len(constants),
        "split_jsons": len(SPLIT_PROFILE_FILES),
    }
    return errors, stats


def main() -> int:
    errors, stats = validate()
    if errors:
        print("HARDWARE_PROFILE_CATALOG_GUARD=FAIL", file=sys.stderr)
        for error in errors:
            print(error, file=sys.stderr)
        return 1

    print(
        "HARDWARE_PROFILE_CATALOG_GUARD=PASS "
        f"profiles={stats['profiles']} "
        f"phases={stats['phases']} "
        f"masks={stats['masks']} "
        f"constants={stats['constants']} "
        f"split_jsons={stats['split_jsons']}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
