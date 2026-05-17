#!/usr/bin/env python3
"""PDA technical-log schema constants and deterministic extra-data derivation."""

from __future__ import annotations

from typing import Any

from LocToBinary import compute_loc_hash


EXPECTED_ENTRY_COUNT = 100
EXTRA_SCHEMA_VERSION = 2
EXTRA_DERIVATION_MODEL = "PDA_TECH_EXTRA_V2_HASH_TEXT_PHYSICS"
EXTRA_DERIVATION_FORMULA = (
    "NoiseSeed=LocHash(SeedInput); "
    "HydrostaticPaPerMeterX100=round(1025*9.80665*100); "
    "HarmonicHzX100=6000+((NoiseSeed>>20)%6001)"
)
VISUAL_DATA_AUTHORITY = "presentation_only_no_simulation_authority"
VISUAL_PROFILE = "industrial_fault_manual"
HABITAT_VISUAL_PROFILE = "habitat_stress_corruption"
HIGH_TIER_GRADIENT_RESOLUTION = 4096
SURFACE_PRESSURE_PA = 101325
PROJECT_PRESSURE_MPA_PER_100M_X1000 = 1000
SEAWATER_DENSITY_KG_M3 = 1025.0
STANDARD_GRAVITY_M_S2 = 9.80665

GRADIENT_LUTS = (
    "PDA_TECH_4096_BRINE_AMBER",
    "PDA_TECH_4096_OXIDE_GREEN",
    "PDA_TECH_4096_PRESSURE_RUST",
    "PDA_TECH_4096_COLD_CYAN",
    "PDA_TECH_4096_BLACKBOX_RED",
)
OVERLAY_FLAG_SETS = (
    ("salt_scanline", "oil_smear"),
    ("pressure_bloom", "gasket_shadow"),
    ("oxide_crawl", "relay_flicker"),
    ("crt_shear", "dead_pixel_rain"),
    ("blackbox_burn", "stress_jitter"),
)

PROJECT_ATLAS_DOMAIN_IDS = (69, 70, 72, 73)
PROJECT_ATLAS_DOMAIN_NAMES = {
    69: "Zero-GC Subtitles (Babel)",
    70: "Diegetic Terminals (3D UI)",
    72: "PDA Encyclopedia Streaming",
    73: "AUP Narrative Triggers",
}


def hydrostatic_pa_per_meter_x100() -> int:
    return round(SEAWATER_DENSITY_KG_M3 * STANDARD_GRAVITY_M_S2 * 100.0)


def title_from_text(text: str) -> str:
    if " // " not in text:
        raise ValueError("PDA technical text must use 'Title // body' format")
    return text.split(" // ", 1)[0]


def build_extra_data(loc_id: str, category: str, title: str) -> dict[str, Any]:
    seed_input = f"{loc_id}:{title}:PDA_EXTRA"
    noise_seed = compute_loc_hash(seed_input)
    gradient_index = noise_seed % len(GRADIENT_LUTS)
    overlay_index = (noise_seed >> 8) % len(OVERLAY_FLAG_SETS)
    stress_state = 0
    if category == "habitat_integrity_corruption":
        stress_state = max(0, min(4, int(loc_id.rsplit("_", 1)[1]) - 16))
    return {
        "SchemaVersion": EXTRA_SCHEMA_VERSION,
        "DerivationModel": EXTRA_DERIVATION_MODEL,
        "DerivationFormula": EXTRA_DERIVATION_FORMULA,
        "SeedInput": seed_input,
        "VisualDataAuthority": VISUAL_DATA_AUTHORITY,
        "VisualProfile": HABITAT_VISUAL_PROFILE if category == "habitat_integrity_corruption" else VISUAL_PROFILE,
        "GradientLut": GRADIENT_LUTS[gradient_index],
        "GradientResolution": HIGH_TIER_GRADIENT_RESOLUTION,
        "GradientIndex": gradient_index,
        "NoiseSeed": noise_seed,
        "HarmonicOctaves": 4 + ((noise_seed >> 16) % 4),
        "HarmonicHzX100": 6000 + ((noise_seed >> 20) % 6001),
        "OverlayFlags": list(OVERLAY_FLAG_SETS[overlay_index]),
        "OverlayIndex": overlay_index,
        "StressState": stress_state,
        "HydrostaticPaPerMeterX100": hydrostatic_pa_per_meter_x100(),
        "SurfacePressurePa": SURFACE_PRESSURE_PA,
        "ProjectPressureMPaPer100mX1000": PROJECT_PRESSURE_MPA_PER_100M_X1000,
    }
