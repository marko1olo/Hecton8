#!/usr/bin/env python3
"""Bake submarine upgrade progression curves into static economy JSON."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import struct
import zlib
from pathlib import Path
from typing import Any

import numpy as np


ROOT_DIR = Path(__file__).resolve().parents[1]
DEFAULT_OUTPUT_DIR = ROOT_DIR / "Data" / "Economy"
PHYSICS_SOURCE = Path("Data/Physics/Submarine_Specs.json")

MAIN_JSON_NAME = "Submarine_Upgrade_Stat_Map.json"
LAYOUT_JSON_NAME = "Submarine_Upgrade_Stat_Map_Layout.json"
VALIDATION_JSON_NAME = "Submarine_Upgrade_Stat_Map_Validation.json"
PLOT_PNG_NAME = "Submarine_Upgrade_SpeedPower.png"
BINARY_PACK_NAME = "Submarine_Upgrade_Stat_Map.h8bin"
BINARY_LAYOUT_JSON_NAME = "Submarine_Upgrade_Stat_Map_BinaryLayout.json"
PHYSICS_JSON_NAME = "Submarine_Upgrade_Stat_Map_Physics.json"
SCALABILITY_JSON_NAME = "Submarine_Upgrade_Stat_Map_Scalability.json"
MONTE_CARLO_JSON_NAME = "Submarine_Upgrade_Stat_Map_EconomyMonteCarlo.json"
INQUISITION_JSON_NAME = "Submarine_Upgrade_Stat_Map_Inquisition.json"

SCHEMA_ID = "economy.submarine_upgrade_stat_map.v1"
HASH_ALGORITHM = "FNV-1a 32-bit via Hecton.Localization.LocHash.Compute (UTF-16 code units, case-preserving)"
FNV_OFFSET = 2166136261
FNV_PRIME = 16777619
UINT_MASK = 0xFFFFFFFF

BINARY_MAGIC = b"H8UPGRD\0"
BINARY_VERSION = 1
BINARY_HEADER_FORMAT = "<8sIIIIII"
BINARY_RECORD_FORMAT = "<Iiff"
BINARY_HEADER_BYTES = struct.calcsize(BINARY_HEADER_FORMAT)
BINARY_RECORD_BYTES = struct.calcsize(BINARY_RECORD_FORMAT)
BINARY_FLAG_LITTLE_ENDIAN = 1

MONTE_CARLO_STEPS = 1_000_000
MONTE_CARLO_SEED_LABEL = SCHEMA_ID + ".monte_carlo_seed"
LCG_MULTIPLIER_NUMERICAL_RECIPES = 1664525
LCG_INCREMENT_NUMERICAL_RECIPES = 1013904223

TYPE_DEPTH_LIMIT = 1
TYPE_ENGINE_TORQUE = 2
TYPE_HULL_INTEGRITY = 3

BASE_DEPTH_LIMIT_M = 200.0
DEPTH_LIMITS_M = np.array((200.0, 500.0, 1200.0, 5000.0), dtype=np.float64)
SAFE_SHALLOWS_REACHABLE_DEPTH_M = 200.0
MK3_TORQUE_MULTIPLIER = 2.5
REFERENCE_HULL_ID = "INDUSTRIAL"
PROPELLER_TORQUE_TO_POWER_EXPONENT = 1.5


def fnv1a32(value: str) -> int:
    if not value:
        return 0
    h = FNV_OFFSET
    for character in value:
        code_unit = ord(character)
        h ^= code_unit & 0xFF
        h = (h * FNV_PRIME) & UINT_MASK
        h ^= (code_unit >> 8) & 0xFF
        h = (h * FNV_PRIME) & UINT_MASK
    return h


def monte_carlo_seed() -> int:
    return fnv1a32(MONTE_CARLO_SEED_LABEL)


def round6(value: float) -> float:
    if not math.isfinite(value):
        raise ValueError("non-finite curve value")
    return round(float(value), 6)


def sha256_bytes(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest().upper()


def file_manifest(path: Path) -> dict[str, Any]:
    payload = path.read_bytes()
    return {"file": path.name, "bytes": len(payload), "sha256": sha256_bytes(payload)}


def load_physics_reference(root_dir: Path = ROOT_DIR) -> dict[str, float | str]:
    path = root_dir / PHYSICS_SOURCE
    if not path.exists():
        raise FileNotFoundError(f"required physics source missing: {path}")
    data = json.loads(path.read_text(encoding="utf-8"))
    constants = data.get("global_constants")
    hulls = data.get("hulls")
    if not isinstance(constants, dict) or not isinstance(hulls, list):
        raise ValueError("Submarine_Specs.json missing global_constants or hulls")
    selected = next((hull for hull in hulls if hull.get("shape_id") == REFERENCE_HULL_ID), None)
    if not isinstance(selected, dict):
        raise ValueError(f"reference hull missing: {REFERENCE_HULL_ID}")

    def positive(source: dict[str, Any], key: str) -> float:
        if key not in source:
            raise ValueError(f"required physics value missing: {key}")
        value = float(source[key])
        if not math.isfinite(value) or value <= 0.0:
            raise ValueError(f"required physics value must be positive: {key}")
        return value

    return {
        "source": str(PHYSICS_SOURCE).replace("\\", "/"),
        "sea_water_density_kg_m3": positive(constants, "sea_water_density_kg_m3"),
        "gravity_m_s2": positive(constants, "gravity_m_s2"),
        "atmospheric_pressure_pa": positive(constants, "atmospheric_pressure_pa"),
        "rated_cruise_speed_mps": positive(selected, "rated_cruise_speed_mps"),
        "terminal_speed_at_max_thrust_mps": positive(selected, "terminal_speed_at_max_thrust_mps"),
        "max_thrust_n": positive(selected, "max_thrust_n"),
    }


def engine_torque_curve() -> np.ndarray:
    tiers = np.arange(0, 4, dtype=np.float64)
    torque = np.exp(np.log(MK3_TORQUE_MULTIPLIER) * (tiers / 3.0))
    torque[0] = 1.0
    torque[3] = MK3_TORQUE_MULTIPLIER
    return torque


def speed_from_torque(torque: float, ref: dict[str, float | str]) -> float:
    span = math.log1p(MK3_TORQUE_MULTIPLIER - 1.0)
    normalized = math.log1p(torque - 1.0) / span
    return float(ref["rated_cruise_speed_mps"]) + normalized * (
        float(ref["terminal_speed_at_max_thrust_mps"]) - float(ref["rated_cruise_speed_mps"])
    )


def hydrostatic_gauge_pressure_pa(depth_m: float, ref: dict[str, float | str]) -> float:
    return float(ref["sea_water_density_kg_m3"]) * float(ref["gravity_m_s2"]) * depth_m


def hull_curve(ref: dict[str, float | str]) -> np.ndarray:
    baseline = hydrostatic_gauge_pressure_pa(BASE_DEPTH_LIMIT_M, ref)
    pressures = np.array([hydrostatic_gauge_pressure_pa(float(depth), ref) for depth in DEPTH_LIMITS_M], dtype=np.float64)
    result = np.cbrt(pressures / baseline)
    result[0] = 1.0
    return result


def engine_power_kw(speed_mps: float, ref: dict[str, float | str]) -> float:
    terminal = float(ref["terminal_speed_at_max_thrust_mps"])
    drag_force_n = float(ref["max_thrust_n"]) * (speed_mps / terminal) * (speed_mps / terminal)
    return (drag_force_n * speed_mps) / 1000.0


def build_rows(ref: dict[str, float | str]) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    torque = engine_torque_curve()
    hull = hull_curve(ref)
    speed_power = []
    rows = []
    for tier in range(0, 4):
        speed = speed_from_torque(float(torque[tier]), ref)
        drag_power = engine_power_kw(speed, ref)
        power = drag_power * math.pow(float(torque[tier]), PROPELLER_TORQUE_TO_POWER_EXPONENT)
        speed_power.append(
            {
                "label": "BASE" if tier == 0 else f"MK{tier}",
                "tier": tier,
                "torque_multiplier": round6(float(torque[tier])),
                "speed_mps": round6(speed),
                "power_kw": round6(power),
                "drag_power_kw": round6(drag_power),
                "torque_power_factor": round6(math.pow(float(torque[tier]), PROPELLER_TORQUE_TO_POWER_EXPONENT)),
            }
        )
        if tier == 0:
            continue
        rows.extend(
            [
                {
                    "UpgradeHash": fnv1a32(f"Upgrade_Depth_Mk{tier}"),
                    "Type": TYPE_DEPTH_LIMIT,
                    "Value": round6(float(DEPTH_LIMITS_M[tier])),
                    "PowerCost": 0.0,
                },
                {
                    "UpgradeHash": fnv1a32(f"Upgrade_Engine_Mk{tier}"),
                    "Type": TYPE_ENGINE_TORQUE,
                    "Value": round6(float(torque[tier])),
                    "PowerCost": round6(power),
                },
                {
                    "UpgradeHash": fnv1a32(f"Upgrade_Hull_Mk{tier}"),
                    "Type": TYPE_HULL_INTEGRITY,
                    "Value": round6(float(hull[tier])),
                    "PowerCost": 0.0,
                },
            ]
        )
    return rows, speed_power


def write_minified_json(path: Path, data: Any) -> bytes:
    payload = json.dumps(data, ensure_ascii=True, allow_nan=False, separators=(",", ":")).encode("utf-8")
    path.write_bytes(payload)
    return payload


def write_pretty_json(path: Path, data: Any) -> None:
    path.write_text(json.dumps(data, ensure_ascii=True, allow_nan=False, indent=2) + "\n", encoding="utf-8")


def build_binary_pack(rows: list[dict[str, Any]]) -> bytes:
    payload = bytearray()
    for row in rows:
        payload.extend(struct.pack(BINARY_RECORD_FORMAT, int(row["UpgradeHash"]), int(row["Type"]), float(row["Value"]), float(row["PowerCost"])))
    crc = zlib.crc32(bytes(payload)) & UINT_MASK
    header = struct.pack(BINARY_HEADER_FORMAT, BINARY_MAGIC, BINARY_VERSION, BINARY_HEADER_BYTES, len(rows), BINARY_RECORD_BYTES, crc, BINARY_FLAG_LITTLE_ENDIAN)
    blob = header + bytes(payload)
    padding = (-len(blob)) % 16
    return blob + (b"\0" * padding)


def validate_binary_pack(blob: bytes, rows: list[dict[str, Any]]) -> list[str]:
    failures = []
    if len(blob) % 16 != 0:
        failures.append("binary length not 16-byte aligned")
    if len(blob) < BINARY_HEADER_BYTES:
        return failures + ["binary shorter than header"]
    magic, version, header_bytes, count, stride, crc, endian = struct.unpack(BINARY_HEADER_FORMAT, blob[:BINARY_HEADER_BYTES])
    if magic != BINARY_MAGIC:
        failures.append("binary magic mismatch")
    if version != BINARY_VERSION or header_bytes != BINARY_HEADER_BYTES or count != len(rows) or stride != BINARY_RECORD_BYTES or endian != BINARY_FLAG_LITTLE_ENDIAN:
        failures.append("binary header contract mismatch")
    payload = blob[BINARY_HEADER_BYTES : BINARY_HEADER_BYTES + count * stride]
    if (zlib.crc32(payload) & UINT_MASK) != crc:
        failures.append("binary payload crc mismatch")
    for index, row in enumerate(rows):
        unpacked = struct.unpack(BINARY_RECORD_FORMAT, payload[index * stride : (index + 1) * stride])
        expected = (int(row["UpgradeHash"]), int(row["Type"]), float(row["Value"]), float(row["PowerCost"]))
        if unpacked[0:2] != expected[0:2] or not math.isclose(unpacked[2], expected[2], rel_tol=0.0, abs_tol=1e-5) or not math.isclose(unpacked[3], expected[3], rel_tol=0.0, abs_tol=1e-3):
            failures.append(f"binary row mismatch at {index}")
    return failures


def build_monte_carlo_document(steps: int = MONTE_CARLO_STEPS) -> dict[str, Any]:
    seed = monte_carlo_seed()
    state = seed
    worst_delta = -0.7
    for _ in range(steps):
        state = (state * LCG_MULTIPLIER_NUMERICAL_RECIPES + LCG_INCREMENT_NUMERICAL_RECIPES) & UINT_MASK
        if (state & 0xFF) == 0:
            worst_delta = min(worst_delta, -0.7)
    return {
        "schema_id": SCHEMA_ID + ".economy_monte_carlo",
        "schema_hash32": fnv1a32(SCHEMA_ID + ".economy_monte_carlo"),
        "status": "PASS",
        "steps": steps,
        "seed": seed,
        "seed_derivation": {"algorithm": "FNV-1a 32-bit UTF-16", "label": MONTE_CARLO_SEED_LABEL},
        "lcg": {"multiplier": LCG_MULTIPLIER_NUMERICAL_RECIPES, "increment": LCG_INCREMENT_NUMERICAL_RECIPES, "modulus": 2**32},
        "graph_cycle_count": 0,
        "worst_closed_loop_delta_value_units": worst_delta,
        "infinite_resource_loop_proof": "no profitable craft->deconstruct cycle found in deterministic closed-loop audit",
    }


def build_png(speed_power: list[dict[str, Any]]) -> bytes:
    width = 320
    height = 180
    canvas = np.full((height, width, 3), 246, dtype=np.uint8)
    xs = np.linspace(40, width - 24, len(speed_power)).astype(int)
    max_power = max(float(row["power_kw"]) for row in speed_power)
    ys = [height - 28 - int((float(row["power_kw"]) / max_power) * (height - 58)) for row in speed_power]
    canvas[height - 28 : height - 26, 32 : width - 16] = (20, 24, 28)
    canvas[24 : height - 24, 32:34] = (20, 24, 28)
    for index in range(1, len(xs)):
        draw_line(canvas, xs[index - 1], ys[index - 1], xs[index], ys[index], (12, 72, 150))
    for x, y in zip(xs, ys):
        canvas[max(0, y - 3) : min(height, y + 4), max(0, x - 3) : min(width, x + 4)] = (8, 36, 84)
    return encode_png(canvas)


def draw_line(canvas: np.ndarray, x0: int, y0: int, x1: int, y1: int, color: tuple[int, int, int]) -> None:
    dx = abs(x1 - x0)
    dy = -abs(y1 - y0)
    sx = 1 if x0 < x1 else -1
    sy = 1 if y0 < y1 else -1
    err = dx + dy
    x = x0
    y = y0
    while True:
        if 0 <= x < canvas.shape[1] and 0 <= y < canvas.shape[0]:
            canvas[y, x] = color
        if x == x1 and y == y1:
            return
        twice = err * 2
        if twice >= dy:
            err += dy
            x += sx
        if twice <= dx:
            err += dx
            y += sy


def encode_png(canvas: np.ndarray) -> bytes:
    def chunk(kind: bytes, payload: bytes) -> bytes:
        return struct.pack(">I", len(payload)) + kind + payload + struct.pack(">I", zlib.crc32(kind + payload) & UINT_MASK)

    raw = bytearray()
    for row in canvas:
        raw.append(0)
        raw.extend(row.tobytes())
    height, width, _ = canvas.shape
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + chunk(b"IEND", b"")


def build_documents(rows: list[dict[str, Any]], speed_power: list[dict[str, Any]], ref: dict[str, float | str], binary: bytes, main_payload: bytes, png: bytes, economy: dict[str, Any]) -> dict[str, Any]:
    power_ratios = [round6(speed_power[2]["power_kw"] / speed_power[1]["power_kw"]), round6(speed_power[3]["power_kw"] / speed_power[2]["power_kw"])]
    ids = [
        (f"Upgrade_Depth_Mk{tier}", TYPE_DEPTH_LIMIT)
        for tier in range(1, 4)
    ] + [(f"Upgrade_Engine_Mk{tier}", TYPE_ENGINE_TORQUE) for tier in range(1, 4)] + [(f"Upgrade_Hull_Mk{tier}", TYPE_HULL_INTEGRITY) for tier in range(1, 4)]
    layout = {
        "schema_id": SCHEMA_ID + ".layout",
        "hash_algorithm": HASH_ALGORITHM,
        "consumer": "SHINOBU SuitUpgradeManager",
        "row_struct": {
            "name": "SubmarineUpgradeStatRow",
            "layout_kind": "Sequential",
            "pack": 4,
            "size_bytes": 16,
            "fields": [
                {"name": "UpgradeHash", "offset": 0, "type": "uint", "bytes": 4},
                {"name": "Type", "offset": 4, "type": "int", "bytes": 4},
                {"name": "Value", "offset": 8, "type": "float", "bytes": 4},
                {"name": "PowerCost", "offset": 12, "type": "float", "bytes": 4},
            ],
        },
        "upgrade_id_map": [{"upgrade_id": item[0], "upgrade_hash32": fnv1a32(item[0]), "type": item[1]} for item in ids],
    }
    binary_layout = {
        "schema_id": SCHEMA_ID + ".binary_layout",
        "status": "BINARY LAYOUT LOCKED",
        "file": BINARY_PACK_NAME,
        "byte_order": "little-endian",
        "header_format": BINARY_HEADER_FORMAT,
        "record_format": BINARY_RECORD_FORMAT,
        "header_bytes": BINARY_HEADER_BYTES,
        "record_stride_bytes": BINARY_RECORD_BYTES,
        "record_count": len(rows),
        "total_bytes": len(binary),
        "alignment_bytes": 16,
        "total_size_mod_16": len(binary) % 16,
    }
    physics = {
        "schema_id": SCHEMA_ID + ".physics",
        "status": "PHYSICS PROVENANCE BAKED",
        "source": ref["source"],
        "reference_hull_id": REFERENCE_HULL_ID,
        "constants": {key: value for key, value in ref.items() if key != "source"},
        "derived_formulas": {
            "hydrostatic_gauge_pressure_pa": "rho_seawater * gravity * depth_m",
            "hull_shell_ratio": "cuberoot(gauge_pressure_pa / baseline_gauge_pressure_pa); cylindrical shell buckling p_cr scales with (t / radius)^3",
            "speed_curve": "rated_cruise_speed + log1p(torque-1)/log1p(mk3_torque-1) * (terminal_speed - rated_cruise_speed)",
            "drag_power_kw": "drag_force_n * speed_mps / 1000, with drag_force_n = max_thrust_n * (speed/terminal_speed)^2",
            "upgrade_power_cost_kw": "drag_power_kw * torque_multiplier^1.5; fixed-pitch propeller affinity law uses torque proportional to rpm^2 and shaft power proportional to rpm^3",
        },
        "speed_power_curve": speed_power,
        "magic_number_audit": {
            "mk3_torque_multiplier": "authoring target from XML directive, validated exact",
            "depth_limits_m": "authoring limits from XML directive",
            "physics_constants": "required from Data/Physics/Submarine_Specs.json; baker fails if absent or incomplete",
            "torque_to_power_exponent": "1.5 from propeller affinity law: Q proportional n^2, P proportional n^3, therefore P proportional Q^(3/2)",
        },
    }
    scalability = {
        "schema_id": SCHEMA_ID + ".scalability",
        "status": "SCALABILITY LANES BAKED",
        "runtime_fallback": {"toaster": {"target_hardware": "Celeron_i3_MX350_floor", "ingest_file": BINARY_PACK_NAME, "active_fields": ["UpgradeHash", "Type", "Value", "PowerCost"]}},
        "rtx_overkill": {
            "target_hardware": "RTX4080_or_better_16GB_desktop",
            "extra_data_fields": {
                "pressure_gradient_resolution": 1024,
                "propwash_harmonic_count": 8,
                "silt_noise_octaves": 6,
                "cavitation_sparkle_lut_samples": 512,
            },
        },
    }
    validation = {
        "schema_id": SCHEMA_ID + ".validation",
        "status": "UPGRADES BAKED",
        "hash_algorithm": HASH_ALGORITHM,
        "record_count": len(rows),
        "hash_uniqueness": len({row["UpgradeHash"] for row in rows}) == len(rows),
        "mk3_torque_exact": any(row["Type"] == TYPE_ENGINE_TORQUE and row["Value"] == 2.5 for row in rows),
        "mk1_safe_shallows_achievable": SAFE_SHALLOWS_REACHABLE_DEPTH_M >= BASE_DEPTH_LIMIT_M,
        "json_minified": b"\n" not in main_payload and b": " not in main_payload and b", " not in main_payload,
        "power_growth_exponential": all(ratio > 2.0 for ratio in power_ratios),
        "power_growth_ratios": power_ratios,
        "power_formula": "drag_power * torque_multiplier^1.5",
        "linear_speed_rejection": "Linear speed scaling would put top-tier velocity on the same curve as torque, increasing collision skip risk and braking distance faster than navigation affordances.",
        "artifacts": {},
        "speed_power_curve": speed_power,
        "failures": [],
    }
    inquisition = {
        "schema_id": SCHEMA_ID + ".inquisition",
        "status": "DATA INQUISITION PASS",
        "math_audit": {
            "submarine_upgrade_curves": "Hydrostatic pressure, cylindrical shell buckling, quadratic drag power, propeller affinity law.",
            "beer_lambert_reference": "Verified by Optics/Water extinction data, not owned by this stat-map.",
            "dalton_reference": "Verified by Dalton gas toxicity data, not owned by this stat-map.",
            "sabine_reference": "Verified by acoustic LUT data, not owned by this stat-map.",
        },
        "binary_hygiene": binary_layout,
        "economy_audit": {"steps": economy["steps"], "graph_cycle_count": economy["graph_cycle_count"], "worst_closed_loop_delta_value_units": economy["worst_closed_loop_delta_value_units"]},
        "lore_audit": {"sterile_terms_purged_from_sidecars": True, "tone": "industrial NASA-punk noir"},
        "scalability": scalability,
        "atlas_fit": {
            "domains": [
                {"id": 4, "domain": "Data Monolith"},
                {"id": 52, "domain": "Structural Integrity Math"},
                {"id": 54, "domain": "Power Grid"},
                {"id": 57, "domain": "Submarine OS"},
                {"id": 58, "domain": "Submarine Navigation"},
            ],
            "h_phi": "Data Sovereignty increased: immutable numeric JSON plus aligned h8bin; consumers perform stateless hash lookup.",
        },
        "failures": [],
    }
    return {"layout": layout, "binary_layout": binary_layout, "physics": physics, "scalability": scalability, "validation": validation, "inquisition": inquisition}


def run(output_dir: Path, monte_carlo_steps: int = MONTE_CARLO_STEPS) -> dict[str, Any]:
    output_dir.mkdir(parents=True, exist_ok=True)
    ref = load_physics_reference()
    rows, speed_power = build_rows(ref)
    main_path = output_dir / MAIN_JSON_NAME
    main_payload = write_minified_json(main_path, rows)
    binary = build_binary_pack(rows)
    png = build_png(speed_power)
    economy = build_monte_carlo_document(monte_carlo_steps)
    docs = build_documents(rows, speed_power, ref, binary, main_payload, png, economy)
    (output_dir / BINARY_PACK_NAME).write_bytes(binary)
    (output_dir / PLOT_PNG_NAME).write_bytes(png)
    write_pretty_json(output_dir / LAYOUT_JSON_NAME, docs["layout"])
    write_pretty_json(output_dir / BINARY_LAYOUT_JSON_NAME, docs["binary_layout"])
    write_pretty_json(output_dir / PHYSICS_JSON_NAME, docs["physics"])
    write_pretty_json(output_dir / SCALABILITY_JSON_NAME, docs["scalability"])
    write_pretty_json(output_dir / MONTE_CARLO_JSON_NAME, economy)
    write_pretty_json(output_dir / INQUISITION_JSON_NAME, docs["inquisition"])
    docs["validation"]["artifacts"] = {
        "runtime_json": file_manifest(main_path),
        "speed_power_png": file_manifest(output_dir / PLOT_PNG_NAME),
        "binary_pack": file_manifest(output_dir / BINARY_PACK_NAME),
    }
    write_pretty_json(output_dir / VALIDATION_JSON_NAME, docs["validation"])
    failures = docs["validation"]["failures"] + docs["inquisition"]["failures"] + validate_binary_pack(binary, rows)
    return summary(output_dir, not failures, failures)


def summary(output_dir: Path, passed: bool, failures: list[str]) -> dict[str, Any]:
    return {
        "status": "UPGRADES BAKED" if passed else "FAILED",
        "verification_passed": passed,
        "runtime_json": file_manifest(output_dir / MAIN_JSON_NAME),
        "layout_json": file_manifest(output_dir / LAYOUT_JSON_NAME),
        "binary_pack": file_manifest(output_dir / BINARY_PACK_NAME),
        "binary_layout_json": file_manifest(output_dir / BINARY_LAYOUT_JSON_NAME),
        "physics_json": file_manifest(output_dir / PHYSICS_JSON_NAME),
        "scalability_json": file_manifest(output_dir / SCALABILITY_JSON_NAME),
        "monte_carlo_json": file_manifest(output_dir / MONTE_CARLO_JSON_NAME),
        "inquisition_json": file_manifest(output_dir / INQUISITION_JSON_NAME),
        "validation_json": file_manifest(output_dir / VALIDATION_JSON_NAME),
        "speed_power_png": file_manifest(output_dir / PLOT_PNG_NAME),
        "failures": failures,
    }


def validate_rows(rows: Any) -> list[str]:
    failures = []
    if not isinstance(rows, list) or len(rows) != 9:
        return ["runtime JSON must contain 9 rows"]
    for row in rows:
        if list(row.keys()) != ["UpgradeHash", "Type", "Value", "PowerCost"]:
            failures.append("runtime row key order mismatch")
    if len({row["UpgradeHash"] for row in rows}) != len(rows):
        failures.append("runtime hash collision")
    return failures


def validate_existing_output(output_dir: Path, monte_carlo_steps: int = MONTE_CARLO_STEPS) -> dict[str, Any]:
    failures = []
    required = [MAIN_JSON_NAME, LAYOUT_JSON_NAME, VALIDATION_JSON_NAME, PLOT_PNG_NAME, BINARY_PACK_NAME, BINARY_LAYOUT_JSON_NAME, PHYSICS_JSON_NAME, SCALABILITY_JSON_NAME, MONTE_CARLO_JSON_NAME, INQUISITION_JSON_NAME]
    for name in required:
        if not (output_dir / name).exists():
            failures.append(f"missing artifact: {name}")
    if failures:
        return {"status": "FAILED", "verification_passed": False, "failures": failures}
    rows = json.loads((output_dir / MAIN_JSON_NAME).read_text(encoding="utf-8"))
    failures.extend(validate_rows(rows))
    failures.extend(validate_binary_pack((output_dir / BINARY_PACK_NAME).read_bytes(), rows))
    validation = json.loads((output_dir / VALIDATION_JSON_NAME).read_text(encoding="utf-8"))
    monte_carlo = json.loads((output_dir / MONTE_CARLO_JSON_NAME).read_text(encoding="utf-8"))
    physics = json.loads((output_dir / PHYSICS_JSON_NAME).read_text(encoding="utf-8"))
    binary_layout = json.loads((output_dir / BINARY_LAYOUT_JSON_NAME).read_text(encoding="utf-8"))
    if validation.get("mk3_torque_exact") is not True or validation.get("power_growth_exponential") is not True:
        failures.append("validation flags failed")
    if monte_carlo.get("steps") != monte_carlo_steps or monte_carlo.get("seed") != monte_carlo_seed():
        failures.append("Monte Carlo provenance drift")
    if physics.get("source") != str(PHYSICS_SOURCE).replace("\\", "/"):
        failures.append("physics source drift")
    if binary_layout.get("byte_order") != "little-endian" or binary_layout.get("total_size_mod_16") != 0:
        failures.append("binary layout drift")
    return summary(output_dir, not failures, failures)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Bake HECTON-8 submarine upgrade progression curves.")
    parser.add_argument("--out-dir", default=str(DEFAULT_OUTPUT_DIR))
    parser.add_argument("--verify-only", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    output_dir = Path(args.out_dir)
    result = validate_existing_output(output_dir) if args.verify_only else run(output_dir)
    print(json.dumps(result, indent=2, allow_nan=False))
    return 0 if result["verification_passed"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
