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
HLSL_PATH = ROOT / "Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl"


def require(condition: bool, message: str) -> None:
    if not condition:
        raise SystemExit(f"FAIL: {message}")


def load_json() -> dict:
    with JSON_PATH.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def require_catalog_constant(catalog: str, token: str, expected: int) -> None:
    pattern = rf"public\s+const\s+int\s+{re.escape(token)}\s*=\s*{expected}\s*;"
    require(re.search(pattern, catalog) is not None, f"catalog constant {token} != {expected}")


def validate_tier_rows(data: dict, catalog: str) -> None:
    max_threads = int(data["computeSafety"]["mx350MaxThreadsPerDispatch"])
    group_size = int(data["computeSafety"]["defaultThreadsPerGroup"])

    for tier in data["tierBudgets"]:
        tier_name = str(tier["tier"])
        particle_count = int(tier["particleCount"])
        marine_snow = int(tier["marineSnowCount"])
        bubbles = int(tier["bubbleCount"])
        debris = int(tier["debrisCount"])
        groups = math.ceil(particle_count / group_size)

        require(marine_snow + bubbles + debris == particle_count, f"{tier_name} pool sum mismatch")
        require(particle_count <= max_threads, f"{tier_name} exceeds MX350 max dispatch threads")
        require(groups == int(tier["dispatchGroupsAt64Threads"]), f"{tier_name} group count mismatch")

        require_catalog_constant(catalog, f"{tier_name}ParticleCount", particle_count)
        require_catalog_constant(catalog, f"{tier_name}MarineSnowCount", marine_snow)
        require_catalog_constant(catalog, f"{tier_name}BubbleCount", bubbles)
        require_catalog_constant(catalog, f"{tier_name}DebrisCount", debris)


def validate_pressure_gates(data: dict, catalog: str, renderer: str) -> None:
    pressure_rows = {int(row["pressureLevel"]): row for row in data["pressureGatePolicy"]}
    require(pressure_rows[1]["forceBudgetTier"] == "Mid", "pressure level 1 must force Mid cap")
    require(pressure_rows[2]["forceBudgetTier"] == "Low", "pressure level 2 must force Low cap")
    require(pressure_rows[3]["forceBudgetTier"] == "Low", "pressure level 3 must force Low cap")
    require(float(pressure_rows[3]["emergencyMarineSnowMultiplier"]) == 0.5, "emergency snow multiplier must be 0.5")

    required_catalog_terms = (
        "pressureLevel >= 2",
        "pressureLevel == 1",
        "NonCriticalVfxMask",
        "EmergencyMarineSnowMultiplierPermille",
        "fluidType == VFXEmissionProfile.FluidType.Bubble",
        "fluidType == VFXEmissionProfile.FluidType.Debris",
    )
    for term in required_catalog_terms:
        require(term in catalog, f"catalog missing pressure behavior: {term}")

    required_renderer_terms = (
        "HomeostasisBrain.PressureLevel",
        "HomeostasisBrain.CurrentKillSwitchMask",
        "ResolveBudgetForPressure",
        "ApplyKillSwitchCount",
        "VolumetricFogHighResMask",
        "ResolveEffectiveShadowTaps",
    )
    for term in required_renderer_terms:
        require(term in renderer, f"renderer missing pressure behavior: {term}")


def validate_renderer_binding(renderer: str) -> None:
    required_bindings = (
        "Mx350MarineSnowParticleCapacity = VfxComputeParticleBudgetCatalog.LowMarineSnowCount",
        "MidMarineSnowParticleCapacity = VfxComputeParticleBudgetCatalog.MidMarineSnowCount",
        "HighMarineSnowParticleCapacity = VfxComputeParticleBudgetCatalog.HighMarineSnowCount",
        "UltraMarineSnowParticleCapacity = VfxComputeParticleBudgetCatalog.UltraMarineSnowCount",
        "VfxComputeParticleBudgetCatalog.ApplyKillSwitchCount",
        "HomeostasisBrain.CurrentKillSwitchMask",
        "HomeostasisBrain.PressureLevel",
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
    hlsl = HLSL_PATH.read_text(encoding="utf-8")

    require(data.get("status") == "VFX BUDGETED", "JSON status is not VFX BUDGETED")
    validate_tier_rows(data, catalog)
    validate_pressure_gates(data, catalog, renderer)
    validate_renderer_binding(renderer)
    validate_hlsl_blue_noise(data, hlsl)
    print("VFX_PARTICLE_BUDGET_CATALOG_OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
