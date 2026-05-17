#!/usr/bin/env python3
"""Verify submarine upgrade stat-map artifacts."""

from __future__ import annotations

import json
import struct
import sys
from pathlib import Path
from typing import Any

sys.path.insert(0, str(Path(__file__).resolve().parent))

import UpgradeCurveBaker


ROOT = Path(__file__).resolve().parents[1]
ECONOMY_DIR = ROOT / "Data" / "Economy"
ATLAS_PATH = ROOT / "Docs" / "PROJECT_ATLAS.md"
FORBIDDEN_TONE_TERMS = ("sterile", "clean sci-fi", "pristine", "utopian", "sleek", "seamless chrome")


def load_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def verify_artifacts() -> dict[str, Any]:
    failures: list[str] = []
    summary = UpgradeCurveBaker.validate_existing_output(ECONOMY_DIR)
    failures.extend(summary.get("failures", []))

    rows = load_json(ECONOMY_DIR / UpgradeCurveBaker.MAIN_JSON_NAME)
    validation = load_json(ECONOMY_DIR / UpgradeCurveBaker.VALIDATION_JSON_NAME)
    monte_carlo = load_json(ECONOMY_DIR / UpgradeCurveBaker.MONTE_CARLO_JSON_NAME)
    inquisition = load_json(ECONOMY_DIR / UpgradeCurveBaker.INQUISITION_JSON_NAME)
    physics = load_json(ECONOMY_DIR / UpgradeCurveBaker.PHYSICS_JSON_NAME)
    scalability = load_json(ECONOMY_DIR / UpgradeCurveBaker.SCALABILITY_JSON_NAME)
    binary_layout = load_json(ECONOMY_DIR / UpgradeCurveBaker.BINARY_LAYOUT_JSON_NAME)
    binary = (ECONOMY_DIR / UpgradeCurveBaker.BINARY_PACK_NAME).read_bytes()
    inquisition_text = (ECONOMY_DIR / UpgradeCurveBaker.INQUISITION_JSON_NAME).read_text(encoding="utf-8").lower()

    if validation.get("mk3_torque_exact") is not True:
        failures.append("Mk3 torque exact audit failed")
    if validation.get("power_growth_exponential") is not True:
        failures.append("power growth audit failed")
    if len({row["UpgradeHash"] for row in rows}) != len(rows):
        failures.append("runtime hash collision")
    if binary_layout.get("byte_order") != "little-endian" or binary_layout.get("total_size_mod_16") != 0:
        failures.append("binary byte order/alignment drift")
    if not str(binary_layout.get("header_format", "")).startswith("<") or not str(binary_layout.get("record_format", "")).startswith("<"):
        failures.append("binary struct format must be explicitly little-endian")
    if binary_layout.get("header_bytes") != struct.calcsize(UpgradeCurveBaker.BINARY_HEADER_FORMAT):
        failures.append("binary header size drift")
    if binary_layout.get("record_stride_bytes") != struct.calcsize(UpgradeCurveBaker.BINARY_RECORD_FORMAT):
        failures.append("binary record stride drift")
    if monte_carlo.get("steps") != UpgradeCurveBaker.MONTE_CARLO_STEPS:
        failures.append("Monte Carlo step count drift")
    if monte_carlo.get("graph_cycle_count") != 0 or monte_carlo.get("worst_closed_loop_delta_value_units", 1.0) > 0.0:
        failures.append("profitable economy loop detected")
    if physics.get("source") != "Data/Physics/Submarine_Specs.json":
        failures.append("physics source must be project submarine spec")
    if "rho_seawater * gravity * depth_m" not in physics.get("derived_formulas", {}).get("hydrostatic_gauge_pressure_pa", ""):
        failures.append("hydrostatic formula missing")
    if scalability.get("runtime_fallback", {}).get("toaster", {}).get("ingest_file") != UpgradeCurveBaker.BINARY_PACK_NAME:
        failures.append("toaster binary fallback missing")
    if scalability.get("rtx_overkill", {}).get("extra_data_fields", {}).get("propwash_harmonic_count", 0) < 8:
        failures.append("RTX harmonic overkill field too low")
    tone_audit = inquisition.get("tone_audit", {})
    if tone_audit.get("forbidden_phrase_hits") != 0:
        failures.append("tone audit reports forbidden phrase hits")
    for term in FORBIDDEN_TONE_TERMS:
        if term in inquisition_text:
            failures.append(f"forbidden tone phrase leaked into sidecar: {term}")
    atlas_text = ATLAS_PATH.read_text(encoding="utf-8", errors="replace")
    domains = [int(row["id"]) for row in inquisition.get("atlas_fit", {}).get("domains", [])]
    for domain_id in domains:
        if f"| {domain_id} |" not in atlas_text:
            failures.append(f"atlas domain id missing: {domain_id}")

    return {
        "status": "PASS" if not failures else "FAILED",
        "runtime_rows": len(rows),
        "binary_bytes": len(binary),
        "binary_mod16": len(binary) % 16,
        "monte_carlo_steps": monte_carlo.get("steps"),
        "power_growth_ratios": validation.get("power_growth_ratios"),
        "atlas_domains": domains,
        "failures": failures,
    }


def main() -> int:
    result = verify_artifacts()
    print(json.dumps(result, indent=2, allow_nan=False))
    return 0 if result["status"] == "PASS" else 2


if __name__ == "__main__":
    raise SystemExit(main())
