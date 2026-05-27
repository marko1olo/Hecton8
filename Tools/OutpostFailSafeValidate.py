#!/usr/bin/env python3
"""Static validator for the Outpost fail-safe mission contract.

Evidence class: STATIC_DOC / STATIC_SOURCE.
This tool intentionally does not prove Unity import, runtime wiring, Play Mode,
Profiler, or GC behavior. It verifies the authored handoff contract only.
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
HANDOFF_PATH = ROOT / "Docs/Design/Missions/Outpost_FailSafe_Handoff.json"
MISSION_DOC_PATH = ROOT / "Docs/Design/Missions/Outpost_Failure_Modes.md"
VALIDATOR_PATH = ROOT / "Assets/_Project/Scripts/Editor/OutpostFailSafeHandoffValidator.cs"
GAS_SOLVER_PATH = ROOT / "Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs"
GLOBAL_CONTRACTS_PATH = ROOT / "Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs"
SURVIVAL_CONTRACT_PATH = ROOT / "Assets/_Project/Scripts/Core/Contracts/HectonSurvivalContract.cs"

EXPECTED_AGENT = "OUTPOST_FAILSAFE_STATIC_CONTRACT"
EXPECTED_ROLE = "DESIGN_MISSIONS"
EXPECTED_SCHEMA = "H8.OUTPOST.FAILSAFE.HANDOFF.V1"
EXPECTED_SOURCE_AUTHORITY_STATUS = "STATIC_MISSION_CONTRACT"
EXPECTED_SOURCE_BATCH = "Docs/Design/Missions/Outpost_Failure_Modes.md"
EXPECTED_REQUESTED_BATCH = "NONE_STATIC_CONTRACT"
EXPECTED_LOC_TABLE = "Assets/_Project/Scripts/English.json"
EXPECTED_FLAG_COUNT = 32
EXPECTED_TOOLTIP_COUNT = 10
EXPECTED_LOG_COUNT = 5
EXPECTED_FALLBACK_COUNT = 3
EXPECTED_TOOLTIP_LOC_IDS = (
    "TIP_OUTPOST_REPAIR_01_EXIT",
    "TIP_OUTPOST_REPAIR_02_BROWNOUT",
    "TIP_OUTPOST_REPAIR_03_TRACE",
    "TIP_OUTPOST_REPAIR_04_COUPLER",
    "TIP_OUTPOST_REPAIR_05_INSTALL",
    "TIP_OUTPOST_REPAIR_06_GHOST_POWER",
    "TIP_OUTPOST_REPAIR_07_DOOR",
    "TIP_OUTPOST_REPAIR_08_AIR",
    "TIP_OUTPOST_REPAIR_09_LOG",
    "TIP_OUTPOST_REPAIR_10_LEAVE",
)
EXPECTED_LOG_LOC_IDS = (
    "OUTPOST_LOG_POWER_01",
    "OUTPOST_LOG_AIR_02",
    "OUTPOST_LOG_FIRE_03",
    "OUTPOST_LOG_BREACH_04",
    "OUTPOST_LOG_EXIT_05",
)
EXPECTED_FALLBACK_RISKS = (
    "no_generator_room",
    "critical_item_lost",
    "unsafe_air_route",
)

EXPECTED_GAS = {
    "oxygenStandardKpa": 21.22,
    "playerOxygenDrainKpaPerSecond": 0.012,
    "playerCo2ProductionKpaPerSecond": 0.010,
    "fireOxygenDrainKpaPerSecond": 0.080,
    "scrubberCo2RemovalKpaPerSecond": 0.055,
    "unpoweredSealedCriticalReadCapSeconds": 90.0,
}

EXPECTED_SOLVER_NEEDLES = (
    "StandardOxygenKPa = HectonSurvivalContract.StandardOxygenKPa",
    "DefaultPlayerO2KPaPerSecond = HectonSurvivalContract.DefaultPlayerOxygenKPaPerSecond",
    "DefaultPlayerCO2KPaPerSecond = HectonSurvivalContract.DefaultPlayerCarbonDioxideKPaPerSecond",
    "DefaultFireO2KPaPerSecond = HectonSurvivalContract.DefaultFireOxygenKPaPerSecond",
    "DefaultScrubberKPaPerSecond = HectonSurvivalContract.DefaultScrubberKPaPerSecond",
)

EXPECTED_SURVIVAL_CONTRACT_NEEDLES = (
    "StandardOxygenKPa = 21.22f",
    "DefaultPlayerOxygenKPaPerSecond = 0.012f",
    "DefaultPlayerCarbonDioxideKPaPerSecond = 0.010f",
    "DefaultFireOxygenKPaPerSecond = 0.080f",
    "DefaultScrubberKPaPerSecond = 0.055f",
)

EXPECTED_GAS_FLAGS = (
    "InternalFire",
    "Breached",
    "ScrubberInstalled",
    "Occupied",
)

ALLOWED_GAS_REFS = {
    "GasDynamicsRoomFlags.InternalFire",
    "GasDynamicsRoomFlags.Breached",
    "GasDynamicsRoomFlags.ScrubberInstalled",
    "GasDynamicsRoomFlags.Occupied",
}
OUTPOST_REF_RE = re.compile(r"\boutpost\.[a-z0-9_]+\b")

STALE_TEXT = (
    "The active prompt was extracted from",
    "Historical extraction source:",
    "ACTIVE_BATCH",
    "CURRENT_BATCH",
    "MISSION_FAIL_SAFE",
    "SCENARIO_",
    "roomflag.",
    "GasDynamicsRoomFlags.Submerged",
    "Submerged on critical room",
    '"fireOxygenDrainKpaPerSecond": 0.4',
)

TRAP_TEXT = (
    "foreach",
    ".Where(",
    ".Select(",
    ".Any(",
    ".FirstOrDefault(",
    ".ToList(",
    "Update(",
    "FixedUpdate(",
    "LateUpdate(",
    "StartCoroutine",
    "FindObjectOfType",
    "GameObject.Find",
    "Resources.Load",
    "SendMessage",
    "BroadcastMessage",
)


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def fnv1a_utf16le(text: str) -> int:
    value = 2166136261
    for byte in text.encode("utf-16le"):
        value ^= byte
        value = (value * 16777619) & 0xFFFFFFFF
    return value


def add_error(errors: list[str], message: str) -> None:
    errors.append(message)


def validate_outpost_refs(
    text: str,
    declared: set[str],
    context: str,
    errors: list[str],
) -> None:
    for ref in sorted(set(OUTPOST_REF_RE.findall(text or ""))):
        if ref not in declared:
            add_error(errors, f"{context} unknown outpost ref {ref}")


def validate_identity(data: dict, errors: list[str]) -> None:
    if data.get("schema") != EXPECTED_SCHEMA:
        add_error(errors, "schema mismatch")
    if data.get("agent") != EXPECTED_AGENT:
        add_error(errors, "agent mismatch")
    if data.get("evidenceClass") != "STATIC_DOC":
        add_error(errors, "evidenceClass mismatch")
    if data.get("sourceBatch") != EXPECTED_SOURCE_BATCH:
        add_error(errors, "sourceBatch mismatch")
    if data.get("requestedBatch") != EXPECTED_REQUESTED_BATCH:
        add_error(errors, "requestedBatch mismatch")
    if data.get("requestedBatchPresent") is not False:
        add_error(errors, "requestedBatchPresent must be false")


def validate_source_authority(data: dict, errors: list[str]) -> str:
    source = data.get("sourceAuthority") or {}
    if source.get("status") != EXPECTED_SOURCE_AUTHORITY_STATUS:
        add_error(errors, "sourceAuthority.status mismatch")
    if source.get("expectedPromptId") != EXPECTED_AGENT:
        add_error(errors, "sourceAuthority.expectedPromptId mismatch")
    if source.get("expectedPromptRole") != EXPECTED_ROLE:
        add_error(errors, "sourceAuthority.expectedPromptRole mismatch")
    if source.get("activeBatchContainsPrompt") is not False:
        add_error(errors, "sourceAuthority.activeBatchContainsPrompt must be false")
    if not (source.get("policy") or "").strip():
        add_error(errors, "sourceAuthority.policy missing")
    source_path = ROOT / EXPECTED_SOURCE_BATCH
    if not source_path.exists():
        add_error(errors, f"source contract missing: {EXPECTED_SOURCE_BATCH}")
    return str(source.get("status") or "")


def validate_flags(data: dict, errors: list[str]) -> set[str]:
    flags = data.get("missionFlags") or []
    if len(flags) != EXPECTED_FLAG_COUNT:
        add_error(errors, f"missionFlags count mismatch: {len(flags)}")

    declared: set[str] = set()
    hashes: dict[int, str] = {}
    for index, item in enumerate(flags):
        flag = item.get("flag", "")
        if not flag.startswith("outpost."):
            add_error(errors, f"missionFlags[{index}] invalid prefix")
        if flag in declared:
            add_error(errors, f"duplicate flag {flag}")
        declared.add(flag)
        hashed = fnv1a_utf16le(flag)
        if hashed in hashes:
            add_error(errors, f"flag hash collision {flag} / {hashes[hashed]}")
        hashes[hashed] = flag

    topological = data.get("topologicalOrder") or []
    if len(topological) != EXPECTED_FLAG_COUNT:
        add_error(errors, f"topologicalOrder count mismatch: {len(topological)}")
    if set(topological) != declared:
        add_error(errors, "topologicalOrder does not cover declared flags exactly")
    if topological and topological[0] != "outpost.generated":
        add_error(errors, "topologicalOrder must start at outpost.generated")
    if topological and topological[-1] != "outpost.mission_complete":
        add_error(errors, "topologicalOrder must end at outpost.mission_complete")
    return declared


def validate_localization(data: dict, declared: set[str], errors: list[str]) -> None:
    entries = data.get("localizationEntries") or []
    tooltips: list[str] = []
    logs: list[str] = []
    hashes: dict[int, str] = {}

    for index, entry in enumerate(entries):
        loc_id = entry.get("locId", "")
        if not loc_id:
            add_error(errors, f"localizationEntries[{index}] missing locId")
            continue

        hashed = fnv1a_utf16le(loc_id)
        expected_hash = f"0x{hashed:08X}"
        if entry.get("hash") != expected_hash:
            add_error(errors, f"{loc_id} hash mismatch")
        if hashed in hashes:
            add_error(errors, f"localization hash collision {loc_id} / {hashes[hashed]}")
        hashes[hashed] = loc_id

        if entry.get("layer") != "Narrative":
            add_error(errors, f"{loc_id} layer mismatch")
        text = entry.get("text", "")
        if not isinstance(text, str) or not text.strip():
            add_error(errors, f"{loc_id} text missing")

        category = entry.get("category")
        if category == "outpost_tooltip":
            tooltips.append(loc_id)
            trigger = entry.get("triggerFlag", "")
            suppress = entry.get("suppressFlag", "")
            if trigger not in declared:
                add_error(errors, f"{loc_id} triggerFlag invalid")
            if suppress not in declared:
                add_error(errors, f"{loc_id} suppressFlag invalid")
        elif category == "outpost_log":
            logs.append(loc_id)
            commit = entry.get("commitFlag", "")
            required = entry.get("requiredState", "")
            if commit not in declared:
                add_error(errors, f"{loc_id} commitFlag invalid")
            if not isinstance(required, str) or not required.strip():
                add_error(errors, f"{loc_id} requiredState missing")
            validate_outpost_refs(required, declared, f"{loc_id}.requiredState", errors)
        else:
            add_error(errors, f"{loc_id} unsupported category")

    if len(tooltips) != EXPECTED_TOOLTIP_COUNT:
        add_error(errors, f"tooltip count mismatch: {len(tooltips)}")
    if len(logs) != EXPECTED_LOG_COUNT:
        add_error(errors, f"log count mismatch: {len(logs)}")
    if tuple(tooltips) != EXPECTED_TOOLTIP_LOC_IDS:
        add_error(errors, "tooltip locId set/order mismatch")
    if tuple(logs) != EXPECTED_LOG_LOC_IDS:
        add_error(errors, "log locId set/order mismatch")


def validate_fallbacks(data: dict, declared: set[str], errors: list[str]) -> None:
    fallbacks = data.get("fallbacks") or []
    if len(fallbacks) != EXPECTED_FALLBACK_COUNT:
        add_error(errors, f"fallback count mismatch: {len(fallbacks)}")
    risks: list[str] = []
    for index, fallback in enumerate(fallbacks):
        risk = fallback.get("risk", "")
        trigger = fallback.get("trigger", "")
        set_flag = fallback.get("setFlag", "")
        constraint = fallback.get("constraint", "")
        risks.append(risk)
        if set_flag not in declared:
            add_error(errors, f"fallbacks[{index}] setFlag invalid")
        if not isinstance(trigger, str) or not trigger.strip():
            add_error(errors, f"fallbacks[{index}] trigger missing")
        if not isinstance(constraint, str) or not constraint.strip():
            add_error(errors, f"fallbacks[{index}] constraint missing")
        validate_outpost_refs(trigger, declared, f"fallbacks[{index}].trigger", errors)
        validate_outpost_refs(constraint, declared, f"fallbacks[{index}].constraint", errors)
    if tuple(risks) != EXPECTED_FALLBACK_RISKS:
        add_error(errors, "fallback risk set/order mismatch")


def validate_gas(data: dict, json_text: str, errors: list[str]) -> None:
    gas = data.get("gasConstraints") or {}
    for key, expected in EXPECTED_GAS.items():
        actual = float(gas.get(key, -999999.0))
        if abs(actual - expected) > 0.0001:
            add_error(errors, f"{key} mismatch: {actual} != {expected}")
    if "does not create oxygen" not in (gas.get("rule") or ""):
        add_error(errors, "gasConstraints.rule missing oxygen-production guard")

    gas_refs = set(re.findall(r"GasDynamicsRoomFlags\.[A-Za-z0-9_]+", json_text))
    for ref in gas_refs:
        if ref not in ALLOWED_GAS_REFS:
            add_error(errors, f"unsupported gas ref {ref}")

    solver_text = read_text(GAS_SOLVER_PATH)
    for needle in EXPECTED_SOLVER_NEEDLES:
        if needle not in solver_text:
            add_error(errors, f"GasDynamicsSolver constant missing: {needle}")

    survival_contract_text = read_text(SURVIVAL_CONTRACT_PATH)
    for needle in EXPECTED_SURVIVAL_CONTRACT_NEEDLES:
        if needle not in survival_contract_text:
            add_error(errors, f"HectonSurvivalContract constant missing: {needle}")

    contracts_text = read_text(GLOBAL_CONTRACTS_PATH)
    enum_start = contracts_text.find("public enum GasDynamicsRoomFlags")
    enum_end = contracts_text.find("public enum GasDynamicsMathLod", enum_start)
    if enum_start < 0 or enum_end < 0:
        add_error(errors, "GasDynamicsRoomFlags enum block not found")
        enum_text = ""
    else:
        enum_text = contracts_text[enum_start:enum_end]

    for flag in EXPECTED_GAS_FLAGS:
        if flag not in enum_text:
            add_error(errors, f"GasDynamicsRoomFlags missing {flag}")
    if "Submerged" in enum_text:
        add_error(errors, "GasDynamicsRoomFlags unexpectedly contains Submerged")


def validate_prose_and_validator(
    json_text: str,
    declared: set[str],
    errors: list[str],
) -> None:
    mission_doc = read_text(MISSION_DOC_PATH)
    validator = read_text(VALIDATOR_PATH)
    combined = json_text + "\n" + mission_doc

    for stale in STALE_TEXT:
        if stale in combined:
            add_error(errors, f"stale text present: {stale}")

    if EXPECTED_SOURCE_AUTHORITY_STATUS not in mission_doc:
        add_error(errors, "mission doc missing STATIC_MISSION_CONTRACT")

    validate_outpost_refs(json_text, declared, "handoff json", errors)
    validate_outpost_refs(mission_doc, declared, "mission doc", errors)

    trap_hits = [needle for needle in TRAP_TEXT if needle in validator]
    if trap_hits:
        add_error(errors, "validator trap hits: " + ",".join(trap_hits))

    required_validator_needles = (
        "ValidateSourceAuthority",
        "ValidateMissionDocSourceAuthority",
        "ValidateFloatEquals",
        'ExpectedSourceAuthorityStatus = "STATIC_MISSION_CONTRACT"',
        "ExpectedFireOxygenDrainKpaPerSecond = HectonSurvivalContract.DefaultFireOxygenKPaPerSecond",
    )
    for needle in required_validator_needles:
        if needle not in validator:
            add_error(errors, f"validator missing {needle}")


def validate_runtime_decision(data: dict, errors: list[str]) -> None:
    runtime = data.get("runtimeAssetDecision") or {}
    if runtime.get("mutatedRuntimeLocalizationAssets") is not False:
        add_error(errors, "runtimeAssetDecision.mutatedRuntimeLocalizationAssets mismatch")
    if runtime.get("activeEnglishTableObserved") != EXPECTED_LOC_TABLE:
        add_error(errors, "runtimeAssetDecision.activeEnglishTableObserved mismatch")
    if not (runtime.get("reason") or "").strip():
        add_error(errors, "runtimeAssetDecision.reason missing")

    hash_contract = data.get("hashContract") or {}
    if hash_contract.get("algorithm") != "FNV-1a 32-bit over UTF-16LE code units":
        add_error(errors, "hashContract.algorithm mismatch")
    if hash_contract.get("runtimeMatch") != "Hecton.Localization.LocHash.Compute":
        add_error(errors, "hashContract.runtimeMatch mismatch")
    if hash_contract.get("offsetBasis") != "0x811C9DC5":
        add_error(errors, "hashContract.offsetBasis mismatch")
    if hash_contract.get("prime") != "0x01000193":
        add_error(errors, "hashContract.prime mismatch")


def validate_trailing_whitespace(errors: list[str]) -> None:
    for path in (
        HANDOFF_PATH,
        MISSION_DOC_PATH,
        VALIDATOR_PATH,
    ):
        for index, line in enumerate(read_text(path).splitlines(), 1):
            if line.endswith(" ") or line.endswith("\t"):
                add_error(errors, f"trailing whitespace {path.relative_to(ROOT)}:{index}")


def main() -> int:
    errors: list[str] = []
    json_text = read_text(HANDOFF_PATH)
    data = json.loads(json_text)

    validate_identity(data, errors)
    source_status = validate_source_authority(data, errors)
    validate_runtime_decision(data, errors)
    declared = validate_flags(data, errors)
    validate_fallbacks(data, declared, errors)
    validate_localization(data, declared, errors)
    validate_gas(data, json_text, errors)
    validate_prose_and_validator(json_text, declared, errors)
    validate_trailing_whitespace(errors)

    if errors:
        for error in errors:
            print("OUTPOST_FAIL_SAFE_ERROR: " + error)
        print(
            "OUTPOST_FAIL_SAFE_STATIC_VALIDATION FAIL "
            f"errors={len(errors)} sourceAuthority={source_status}"
        )
        return 1

    print(
        "OUTPOST_FAIL_SAFE_STATIC_VALIDATION PASS "
        f"flags={len(data['missionFlags'])} "
        f"topo={len(data['topologicalOrder'])} "
        f"loc={len(data['localizationEntries'])} "
        f"fallbacks={len(data['fallbacks'])} "
        f"sourceAuthority={source_status}"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
