#!/usr/bin/env python3
"""Validate HECTON-8 VFX compute particle budget parity."""

from __future__ import annotations

import json
import math
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
JSON_PATH = ROOT / "Assets/_Project/Data/VFX/REND_DYNAMIC_RESOLUTION_ADAPTER_compute_particle_budgets.json"
CATALOG_PATH = ROOT / "Assets/_Project/Scripts/VFX/VfxComputeParticleBudgetCatalog.cs"
RENDERER_PATH = ROOT / "Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs"
MARINE_SNOW_COMPUTE_PATH = ROOT / "Assets/_Project/Art/Shaders/Hecton_MarineSnow.compute"
HLSL_PATH = ROOT / "Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl"
HOMEOSTASIS_PATH = ROOT / "Assets/_Project/Scripts/Core/HomeostasisBrain.cs"
DRS_ADAPTER_PATH = ROOT / "Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs"

EXPECTED_SYSTEM_BITS = {
    "ParticleAdvection": 5,
    "VolumetricFogHighRes": 6,
    "NonCriticalVfx": 20,
}

TIER_CONSTANT_PREFIX = {
    "Low": "MinimumQuality",
    "Mid": "MiddleQuality",
    "High": "MaximumQuality",
    "Ultra": "OverkillQuality",
}


def require(condition: bool, message: str) -> None:
    if not condition:
        raise SystemExit(f"FAIL: {message}")


def load_json() -> dict:
    with JSON_PATH.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def require_catalog_constant(catalog: str, token: str, expected: int) -> None:
    pattern = rf"public\s+const\s+int\s+{re.escape(token)}\s*=\s*{expected}\s*;"
    require(re.search(pattern, catalog) is not None, f"catalog constant {token} != {expected}")


def require_catalog_float_constant(catalog: str, token: str, expected: float) -> None:
    formatted = f"{expected:.2f}f"
    pattern = rf"public\s+const\s+float\s+{re.escape(token)}\s*=\s*{re.escape(formatted)}\s*;"
    require(re.search(pattern, catalog) is not None, f"catalog constant {token} != {formatted}")


def validate_tier_rows(data: dict, catalog: str) -> None:
    max_threads = int(data["computeSafety"]["mx350MaxThreadsPerDispatch"])
    group_size = int(data["computeSafety"]["defaultThreadsPerGroup"])

    for tier in data["tierBudgets"]:
        tier_name = str(tier["tier"])
        constant_prefix = TIER_CONSTANT_PREFIX.get(tier_name)
        require(constant_prefix is not None, f"{tier_name} has no catalog constant prefix")
        particle_count = int(tier["particleCount"])
        marine_snow = int(tier["marineSnowCount"])
        bubbles = int(tier["bubbleCount"])
        debris = int(tier["debrisCount"])
        groups = math.ceil(particle_count / group_size)

        require(marine_snow + bubbles + debris == particle_count, f"{tier_name} pool sum mismatch")
        require(particle_count <= max_threads, f"{tier_name} exceeds MX350 max dispatch threads")
        require(groups == int(tier["dispatchGroupsAt64Threads"]), f"{tier_name} group count mismatch")

        require_catalog_constant(catalog, f"{constant_prefix}ParticleCount", particle_count)
        require_catalog_constant(catalog, f"{constant_prefix}MarineSnowCount", marine_snow)
        require_catalog_constant(catalog, f"{constant_prefix}BubbleCount", bubbles)
        require_catalog_constant(catalog, f"{constant_prefix}DebrisCount", debris)
        require_catalog_float_constant(catalog, f"{constant_prefix}StepDistanceMeters", float(tier["stepDistanceMeters"]))
        require_catalog_constant(catalog, f"{constant_prefix}ShadowTaps", int(tier["shadowTaps"]))
        require_catalog_constant(catalog, f"{constant_prefix}FlowResampleFrames", int(tier["flowResampleFrames"]))


def validate_handoff_contract(data: dict, catalog: str, renderer: str, homeostasis: str, drs_adapter: str) -> None:
    require(data.get("targetConsumer") == "REND_DYNAMIC_RESOLUTION_ADAPTER", "wrong target consumer")
    require("ThermalDynamicResolutionAdapter" in drs_adapter, "DRS adapter class missing")
    require("IDynamicResolutionRuntime" in drs_adapter, "DRS adapter runtime contract missing")
    require("TelemetryCapacity = 300" in drs_adapter, "DRS adapter must retain 300-frame telemetry ring")
    require("DumpFileName" in drs_adapter, "DRS adapter dump file route missing")
    require("DumpBlackBoxOnce" in drs_adapter, "DRS adapter black-box dump method missing")

    binding_rows = {str(row["name"]): row for row in data["systemBitBindings"]}
    for name, bit_index in EXPECTED_SYSTEM_BITS.items():
        require(name in binding_rows, f"JSON systemBitBindings missing {name}")
        row = binding_rows[name]
        expected_mask = 1 << bit_index
        require(int(row["bitIndex"]) == bit_index, f"{name} bitIndex drift")
        require(int(str(row["bitHex"]), 16) == expected_mask, f"{name} bitHex drift")
        enum_pattern = rf"{re.escape(name)}\s*=\s*1UL\s*<<\s*{bit_index}\b"
        enum_direct = re.search(enum_pattern, homeostasis) is not None
        enum_alias = False
        alias_match = re.search(rf"{re.escape(name)}\s*=\s*(\w+)\b", homeostasis)
        if alias_match is not None:
            alias_name = alias_match.group(1)
            alias_pattern = rf"{re.escape(alias_name)}\s*=\s*1UL\s*<<\s*{bit_index}\b"
            enum_alias = re.search(alias_pattern, homeostasis) is not None
        require(enum_direct or enum_alias, f"Homeostasis SystemBit drift for {name}")
        require(
            f"public const ulong {name}Mask = (ulong)SystemBit.{name};" in catalog,
            f"catalog mask binding missing {name}",
        )

    pressure_rows = {int(row["pressureLevel"]): row for row in data["pressureGatePolicy"]}
    expected_policy_bits = {
        1: ("ParticleAdvection",),
        2: ("ParticleAdvection", "VolumetricFogHighRes", "NonCriticalVfx"),
        3: ("ParticleAdvection", "VolumetricFogHighRes", "NonCriticalVfx"),
    }
    for pressure_level, bit_names in expected_policy_bits.items():
        expected_mask = 0
        for bit_name in bit_names:
            expected_mask |= 1 << EXPECTED_SYSTEM_BITS[bit_name]
        require(
            int(str(pressure_rows[pressure_level]["disableMaskHex"]), 16) == expected_mask,
            f"pressure {pressure_level} disableMaskHex drift",
        )
        require(
            f"PressureLevel{pressure_level}DisableMask" in catalog,
            f"catalog missing PressureLevel{pressure_level}DisableMask",
        )

    require("ResolvePolicyKillSwitchMask" in catalog, "catalog missing policy mask resolver")
    require("ResolvePolicyKillSwitchMask" in renderer, "renderer missing policy mask resolver binding")


def validate_pressure_gates(data: dict, catalog: str, renderer: str, marine_snow_compute: str) -> None:
    pressure_rows = {int(row["pressureLevel"]): row for row in data["pressureGatePolicy"]}
    require(pressure_rows[1]["forceBudgetTier"] == "Mid", "pressure level 1 must force Mid cap")
    require(pressure_rows[2]["forceBudgetTier"] == "Low", "pressure level 2 must force Low cap")
    require(pressure_rows[3]["forceBudgetTier"] == "Low", "pressure level 3 must force Low cap")
    require(float(pressure_rows[3]["emergencyMarineSnowMultiplier"]) == 0.5, "emergency snow multiplier must be 0.5")

    required_catalog_terms = (
        "ResolveContinuousBudget",
        "math.smoothstep(0f, 0.45f, q)",
        "math.smoothstep(0.35f, 0.85f, q)",
        "math.smoothstep(0.72f, 1f, q)",
        "pressureLevel >= 2",
        "pressureLevel == 1",
        "NonCriticalVfxMask",
        "EmergencyMarineSnowMultiplierPermille",
        "fluidType == VFXEmissionProfile.FluidType.Bubble",
        "fluidType == VFXEmissionProfile.FluidType.Debris",
        "ResolvePolicyKillSwitchMask",
        "PressureLevel2DisableMask",
        "PressureLevel3DisableMask",
        "pressureLevel >= 3",
    )
    for term in required_catalog_terms:
        require(term in catalog, f"catalog missing pressure behavior: {term}")

    required_renderer_terms = (
        "HomeostasisBrain.PressureLevel",
        "HomeostasisBrain.CurrentKillSwitchMask",
        "BuildContinuousPressureBudget",
        "BuildContinuousScalabilityParams",
        "ResolveContinuousPoolCapacity",
        "ApplyKillSwitchCount",
        "VolumetricFogHighResMask",
        "ResolveEffectiveShadowTaps",
        "ResolvePolicyKillSwitchMask",
    )
    for term in required_renderer_terms:
        require(term in renderer, f"renderer missing pressure behavior: {term}")
    require(
        re.search(
            r"ApplyKillSwitchCount\(\s*resolvedCount,\s*fluidType,\s*killSwitchMask,\s*pressureLevel\s*\)",
            renderer,
        )
        is not None,
        "renderer must pass pressureLevel into ApplyKillSwitchCount",
    )

    runtime_consumers = set(str(path) for path in data["runtimeConsumers"])
    require(
        "Assets/_Project/Art/Shaders/Hecton_MarineSnow.compute" in runtime_consumers,
        "JSON runtimeConsumers missing marine-snow compute kernel",
    )

    required_compute_terms = (
        "float scalabilityQuality = saturate(_MarineSnowScalabilityParams.x * 0.5);",
        "float highDetailWeight = smoothstep(0.5, 1.0, scalabilityQuality);",
        "bool flowAdvectionEnabled = scalabilityQuality > EPSILON",
        "if (flowAdvectionEnabled)",
        "EvaluateShallowWaterFieldData(particle.Pos)",
        "particle.Vel.xz *= saturate(1.0 - dt * 2.0);",
    )
    for term in required_compute_terms:
        require(term in marine_snow_compute, f"marine-snow compute missing advection gate: {term}")


def validate_renderer_binding(renderer: str) -> None:
    required_bindings = (
        "MinimumMarineSnowParticleCapacity = VfxComputeParticleBudgetCatalog.MinimumQualityMarineSnowCount",
        "OverkillMarineSnowParticleCapacity = VfxComputeParticleBudgetCatalog.OverkillQualityMarineSnowCount",
        "MaxMarineSnowParticleCapacity = OverkillMarineSnowParticleCapacity",
        "VfxComputeParticleBudgetCatalog.ApplyKillSwitchCount",
        "HomeostasisBrain.CurrentKillSwitchMask",
        "HomeostasisBrain.PressureLevel",
        "BuildContinuousPressureBudget",
        "ResolveContinuousPoolCapacity",
    )
    for binding in required_bindings:
        require(binding in renderer, f"renderer missing binding: {binding}")

    forbidden_literals = (
        "private const int Mx350MarineSnowParticleCapacity = 32768",
        "private const int MidMarineSnowParticleCapacity = 65536",
        "private const int HighMarineSnowParticleCapacity = 100000",
        "up to 100000 * 64B",
    )
    for literal in forbidden_literals:
        require(literal not in renderer, f"renderer still contains stale literal: {literal}")


def validate_hlsl_blue_noise(data: dict, hlsl: str) -> None:
    matrix = data["hlslReadyBlueNoise4x4"]["normalizedThresholdMatrix"]
    snippet = str(data["hlslReadyBlueNoise4x4"].get("hlslSnippet", ""))
    for row in matrix:
        for value in row:
            formatted = f"{float(value):.5f}".rstrip("0").rstrip(".")
            require(formatted in hlsl, f"HLSL missing blue-noise value {formatted}")
            require(formatted in snippet, f"JSON HLSL snippet missing blue-noise value {formatted}")
    require("HectonCoreLitBlueNoise4x4" in hlsl, "HLSL missing 4x4 blue-noise fallback function")
    require("static const half blueNoise4x4[16]" not in hlsl, "HLSL fallback must not use dynamic local array indexing")
    require("switch (index)" in hlsl, "HLSL fallback must use switch-based literal return path")
    require("HectonCoreLitBlueNoise4x4" in snippet, "JSON HLSL snippet missing fallback function name")
    require("switch (index)" in snippet, "JSON HLSL snippet must use switch-based literal return path")
    require("static const half" not in snippet, "JSON HLSL snippet must not advertise static array fallback")


def main() -> int:
    data = load_json()
    catalog = CATALOG_PATH.read_text(encoding="utf-8")
    renderer = RENDERER_PATH.read_text(encoding="utf-8")
    marine_snow_compute = MARINE_SNOW_COMPUTE_PATH.read_text(encoding="utf-8")
    hlsl = HLSL_PATH.read_text(encoding="utf-8")
    homeostasis = HOMEOSTASIS_PATH.read_text(encoding="utf-8")
    drs_adapter = DRS_ADAPTER_PATH.read_text(encoding="utf-8")

    require(data.get("status") == "VFX BUDGETED", "JSON status is not VFX BUDGETED")
    require(
        "Unity runtime and GPU profiler verification are still pending" in str(data.get("statusNote", "")),
        "JSON must not imply runtime/GPU readiness",
    )
    validate_tier_rows(data, catalog)
    validate_handoff_contract(data, catalog, renderer, homeostasis, drs_adapter)
    validate_pressure_gates(data, catalog, renderer, marine_snow_compute)
    validate_renderer_binding(renderer)
    validate_hlsl_blue_noise(data, hlsl)
    print("VFX_PARTICLE_BUDGET_CATALOG_OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
