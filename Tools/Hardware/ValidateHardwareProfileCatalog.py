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

PROFILE_FIELD_CONSTANTS = (
    ("profileStableHash32", "StableHash32"),
    ("profileGraphicsBudgetMb", "GraphicsBudgetMegabytes"),
    ("profileTextureBudgetMb", "TextureBudgetMegabytes"),
    ("profileRenderTargetBudgetMb", "RenderTargetBudgetMegabytes"),
    ("profileTargetFps", "TargetFps"),
    ("profileJobWorkerBudget", "JobWorkerBudget"),
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


def validate_call_sites(catalog: str, detector: str, enforcer: str, errors: list[str]) -> None:
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


def validate() -> list[str]:
    root = Path(__file__).resolve().parents[2]
    data_path = root / "Data" / "Hardware" / "Profiles.json"
    catalog_path = root / "Assets" / "_Project" / "Scripts" / "Core" / "HardwareProfileCatalog.cs"
    detector_path = root / "Assets" / "_Project" / "Scripts" / "Core" / "HardwareTierDetector.cs"
    enforcer_path = root / "Assets" / "_Project" / "Scripts" / "Optimization" / "VRAMEnforcer.cs"

    data = load_json(data_path)
    catalog = catalog_path.read_text(encoding="utf-8-sig")
    detector = detector_path.read_text(encoding="utf-8-sig")
    enforcer = enforcer_path.read_text(encoding="utf-8-sig")
    constants = extract_constants(catalog)

    errors: list[str] = []
    validate_flat_layout(data, errors)
    validate_hashes(data, errors)
    validate_catalog_constants(data, constants, errors)
    validate_phase_methods(data, catalog, errors)
    validate_call_sites(catalog, detector, enforcer, errors)
    return errors


def main() -> int:
    errors = validate()
    if errors:
        print("HARDWARE_PROFILE_CATALOG_GUARD=FAIL", file=sys.stderr)
        for error in errors:
            print(error, file=sys.stderr)
        return 1

    print("HARDWARE_PROFILE_CATALOG_GUARD=PASS profiles=2 phases=4 masks=4 constants=19")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
