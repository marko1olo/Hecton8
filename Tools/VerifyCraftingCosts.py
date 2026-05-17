#!/usr/bin/env python3
"""Verify SHINOBU-ready crafting-cost data and binaries."""

from __future__ import annotations

import hashlib
import json
import zlib
from pathlib import Path
from typing import Any

from CraftingCostsBaker import (
    BINARY_ALIGNMENT_BYTES,
    BINARY_PATH,
    ENDIAN_PROBE,
    GODMODE_VISUAL_STRUCT,
    HEADER_STRUCT,
    INGREDIENT_STRUCT,
    KG_TO_GRAMS,
    KWH_TO_MILLIWATT_HOURS,
    MAGIC,
    MANIFEST_PATH,
    RATIO_TO_BASIS_POINTS,
    RECIPE_STRUCT,
    ROUNDING_EPSILON,
    SECONDS_TO_DECISECONDS,
    TARGET_RECIPE_COUNT,
    TOOL_STRUCT,
    TOASTER_BINARY_PATH,
    TOASTER_BINARY_SCHEMA_ID,
    TOASTER_HEADER_STRUCT,
    TOASTER_MAGIC,
    TOASTER_MANIFEST_PATH,
    TOASTER_PROFILE_ID,
    TOASTER_RECORD_STRUCT,
    TOASTER_VERSION,
    UINT32_MASK,
    VALUE_UNITS_TO_MILLI,
    VERSION,
    align16,
    build_catalog,
    fnv1a32,
    pack_gradient_visual,
    pack_harmonic_noise,
    pack_recipe_visual,
    pack_toaster_meta,
)


ROOT = Path(__file__).resolve().parents[1]
JSON_PATH = ROOT / "Data" / "Economy" / "Crafting_Costs.json"
HASH_AUDIT_PATH = ROOT / "Data" / "Economy" / "Crafting_Hash_Audit.json"
SOVEREIGNTY_AUDIT_PATH = ROOT / "Data" / "Economy" / "Crafting_DataSovereignty_Audit.json"
PROJECT_ATLAS_PATH = ROOT / "Docs" / "PROJECT_ATLAS.md"


def fail(message: str) -> None:
    raise SystemExit(f"CRAFTING COST VERIFY FAILED: {message}")


def require(condition: bool, message: str) -> None:
    if not condition:
        fail(message)


def load_json(path: Path) -> Any:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def collect_id_hashes(data: Any) -> dict[int, str]:
    pairs: dict[int, str] = {}

    def visit(node: Any, path: str) -> None:
        if isinstance(node, dict):
            for key, value in node.items():
                if key.endswith("_id") and isinstance(value, str):
                    hash_key = key[:-3] + "_hash32"
                    require(hash_key in node, f"{path}.{key} missing {hash_key}")
                    expected = fnv1a32(value)
                    actual = int(node[hash_key])
                    require(actual == expected, f"{path}.{key} hash mismatch")
                    previous = pairs.get(actual)
                    require(previous is None or previous == value, f"FNV collision: {previous} and {value}")
                    pairs[actual] = value
                visit(value, f"{path}.{key}")
        elif isinstance(node, list):
            for index, value in enumerate(node):
                visit(value, f"{path}[{index}]")

    visit(data, "CraftingCosts")
    return pairs


def round6(value: float) -> float:
    return round(value + ROUNDING_EPSILON, 6)


def compute_energy_terms(recipe: dict[str, Any], catalog: dict[str, dict[str, Any]], power_model: dict[str, Any]) -> dict[str, float]:
    thermal_kwh = 0.0
    surface_kwh = 0.0
    feed_kwh = 0.0
    vacuum_kwh = 0.0
    tier = int(recipe["progression_tier"])
    for ingredient in recipe["ingredients"]:
        source = catalog[ingredient["item_id"]]
        profile = source["physical_profile"]
        mass_kg = float(ingredient["total_mass_kg"])
        reactivation_fraction = 1.0 if source["source"] == "raw" else float(power_model["thermal_reactivation_fraction"])
        sensible_kj_per_kg = float(profile["specific_heat_kj_per_kg_k"]) * float(profile["process_delta_k"])
        phase_kj_per_kg = float(profile["latent_heat_kj_per_kg"]) * float(profile["phase_change_fraction"])
        thermal_kwh += mass_kg * (sensible_kj_per_kg + phase_kj_per_kg) / float(power_model["kilojoules_per_kwh"]) / float(profile["thermal_efficiency"]) * reactivation_fraction
        volume_m3 = mass_kg / float(profile["density_kg_per_m3"])
        surface_area_m2 = float(power_model["surface_area_sphere_coefficient"]) * (volume_m3 ** float(power_model["surface_area_volume_exponent"])) * (1.0 + float(power_model["surface_complexity_per_tier"]) * tier)
        surface_kwh += surface_area_m2 * float(profile["surface_join_kj_per_m2"]) / float(power_model["kilojoules_per_kwh"]) / float(power_model["arc_surface_coupling_efficiency"])
        feed_kwh += mass_kg * float(power_model["standard_gravity_m_per_s2"]) * float(power_model["fabricator_feed_lift_m"]) / float(power_model["joules_per_kwh"]) / float(power_model["servo_drive_efficiency"])
        vacuum_kwh += float(power_model["chamber_vacuum_delta_pa"]) * float(power_model["chamber_void_m3_per_kg"]) * mass_kg / float(power_model["joules_per_kwh"]) / float(power_model["vacuum_pump_efficiency"])
    return {
        "thermal_conditioning_kwh": round6(thermal_kwh),
        "surface_joining_kwh": round6(surface_kwh),
        "feed_motion_kwh": round6(feed_kwh),
        "chamber_vacuum_kwh": round6(vacuum_kwh),
        "total_kwh": round(thermal_kwh + surface_kwh + feed_kwh + vacuum_kwh, 3),
    }


def verify_full_binary(data: dict[str, Any]) -> dict[str, Any]:
    manifest = load_json(MANIFEST_PATH)
    binary = BINARY_PATH.read_bytes()
    require(HEADER_STRUCT.format.startswith("<"), "header struct is not little-endian")
    require(RECIPE_STRUCT.format.startswith("<"), "recipe struct is not little-endian")
    require(INGREDIENT_STRUCT.format.startswith("<"), "ingredient struct is not little-endian")
    require(TOOL_STRUCT.format.startswith("<"), "tool struct is not little-endian")
    require(GODMODE_VISUAL_STRUCT.format.startswith("<"), "godmode struct is not little-endian")
    require(len(binary) % BINARY_ALIGNMENT_BYTES == 0, "binary file size is not 16-byte aligned")
    require(hashlib.sha256(binary).hexdigest() == manifest["binary_sha256"], "binary sha mismatch")
    require(hashlib.sha256(JSON_PATH.read_bytes()).hexdigest() == manifest["json_sha256"], "json sha mismatch")
    header = HEADER_STRUCT.unpack_from(binary, 0)
    (
        magic,
        version,
        endian_probe,
        header_bytes,
        recipe_count,
        recipe_stride,
        ingredient_count,
        ingredient_stride,
        tool_count,
        tool_stride,
        godmode_visual_count,
        godmode_visual_stride,
        recipe_offset,
        ingredient_offset,
        tool_offset,
        godmode_visual_offset,
        file_size,
        payload_crc32,
        reserved0,
        reserved1,
    ) = header
    require(magic == MAGIC and version == VERSION and endian_probe == ENDIAN_PROBE, "full binary header identity mismatch")
    require(header_bytes == HEADER_STRUCT.size and recipe_stride == RECIPE_STRUCT.size, "full binary stride mismatch")
    require(ingredient_stride == INGREDIENT_STRUCT.size and tool_stride == TOOL_STRUCT.size and godmode_visual_stride == GODMODE_VISUAL_STRUCT.size, "full binary table stride mismatch")
    require(file_size == len(binary), "full binary file size mismatch")
    require((reserved0, reserved1) == (0, 0), "full binary reserved fields not zero")
    require((zlib.crc32(binary[recipe_offset:file_size]) & UINT32_MASK) == payload_crc32, "full binary crc mismatch")
    recipes = data["recipes"]
    require(recipe_count == len(recipes), "full binary recipe count mismatch")
    require(ingredient_count == sum(len(recipe["ingredients"]) for recipe in recipes), "full binary ingredient count mismatch")
    require(tool_count == sum(len(recipe["required_tools"]) for recipe in recipes), "full binary tool count mismatch")
    require(godmode_visual_count == len(recipes), "full binary godmode count mismatch")
    catalog = build_catalog(data)
    ingredient_cursor = 0
    tool_cursor = 0
    for recipe_index, recipe in enumerate(recipes):
        record = RECIPE_STRUCT.unpack_from(binary, recipe_offset + recipe_index * recipe_stride)
        god_mode = recipe["scalability_profiles"]["god_mode"]
        expected_record = (
            int(recipe["recipe_hash32"]),
            int(recipe["result"]["item_hash32"]),
            int(recipe["category_hash32"]),
            1 if bool(recipe.get("starter_item", False)) else 0,
            int(recipe["progression_tier"]),
            len(recipe["ingredients"]),
            len(recipe["required_tools"]),
            int(recipe["scalability_profiles"]["toaster"]["ui_cost_class_u8"]),
            ingredient_cursor,
            tool_cursor,
            int(round(float(recipe["input_mass_kg"]) * KG_TO_GRAMS)),
            int(round(float(recipe["output_mass_kg"]) * KG_TO_GRAMS)),
            int(round(float(recipe["baseline_cost_units"]) * VALUE_UNITS_TO_MILLI)),
            int(round(float(recipe["PowerCost_kWh"]) * KWH_TO_MILLIWATT_HOURS)),
            int(round(float(recipe["FabricationTimeSeconds"]) * SECONDS_TO_DECISECONDS)),
            int(round(float(recipe["deconstruct_reclaim_ratio"]) * RATIO_TO_BASIS_POINTS)),
            int(god_mode["fabricator_fx_hash32"]),
            pack_recipe_visual(god_mode),
        )
        require(record == expected_record, f"recipe binary mismatch {recipe['recipe_id']}")
        visual_record = GODMODE_VISUAL_STRUCT.unpack_from(binary, godmode_visual_offset + recipe_index * godmode_visual_stride)
        require(visual_record == (int(god_mode["arc_gradient_lut_hash32"]), int(god_mode["harmonic_noise_seed_hash32"]), pack_gradient_visual(god_mode), pack_harmonic_noise(god_mode)), f"godmode binary mismatch {recipe['recipe_id']}")
        for ingredient in recipe["ingredients"]:
            expected_ingredient = (int(ingredient["item_hash32"]), int(ingredient["quantity"]), 0, int(round(float(ingredient["unit_mass_kg"]) * KG_TO_GRAMS)), int(round(float(ingredient["total_mass_kg"]) * KG_TO_GRAMS)))
            require(INGREDIENT_STRUCT.unpack_from(binary, ingredient_offset + ingredient_cursor * ingredient_stride) == expected_ingredient, f"ingredient binary mismatch {recipe['recipe_id']}")
            ingredient_cursor += 1
        for tool in recipe["required_tools"]:
            tool_catalog = catalog[tool["item_id"]]
            require(TOOL_STRUCT.unpack_from(binary, tool_offset + tool_cursor * tool_stride) == (int(tool["item_hash32"]), int(tool_catalog["tier"]), int(tool_catalog["category_hash32"]), 0), f"tool binary mismatch {recipe['recipe_id']}")
            tool_cursor += 1
    return {"bytes": len(binary), "payload_crc32": payload_crc32, "recipe_count": recipe_count, "ingredient_count": ingredient_count, "tool_count": tool_count, "godmode_visual_count": godmode_visual_count, "sha256": hashlib.sha256(binary).hexdigest()}


def verify_toaster_binary(data: dict[str, Any]) -> dict[str, Any]:
    manifest = load_json(TOASTER_MANIFEST_PATH)
    binary = TOASTER_BINARY_PATH.read_bytes()
    require(TOASTER_HEADER_STRUCT.format.startswith("<"), "toaster header is not little-endian")
    require(TOASTER_RECORD_STRUCT.format.startswith("<"), "toaster record is not little-endian")
    require(len(binary) % BINARY_ALIGNMENT_BYTES == 0, "toaster binary file size is not 16-byte aligned")
    require(hashlib.sha256(binary).hexdigest() == manifest["binary_sha256"], "toaster sha mismatch")
    require(hashlib.sha256(JSON_PATH.read_bytes()).hexdigest() == manifest["json_sha256"], "toaster json sha mismatch")
    header = TOASTER_HEADER_STRUCT.unpack_from(binary, 0)
    magic, version, endian_probe, header_bytes, record_count, record_stride, record_offset, file_size, payload_crc32, binary_schema_hash, profile_hash, reserved0, reserved1, reserved2, reserved3, reserved4 = header
    require(magic == TOASTER_MAGIC and version == TOASTER_VERSION and endian_probe == ENDIAN_PROBE, "toaster header identity mismatch")
    require(header_bytes == TOASTER_HEADER_STRUCT.size and record_stride == TOASTER_RECORD_STRUCT.size, "toaster stride mismatch")
    require(record_offset == align16(TOASTER_HEADER_STRUCT.size), "toaster record offset mismatch")
    require(file_size == align16(record_offset + record_count * record_stride), "toaster file has unexpected tables")
    require((reserved0, reserved1, reserved2, reserved3, reserved4) == (0, 0, 0, 0, 0), "toaster reserved fields not zero")
    require((zlib.crc32(binary[record_offset:file_size]) & UINT32_MASK) == payload_crc32, "toaster crc mismatch")
    require(binary_schema_hash == fnv1a32(TOASTER_BINARY_SCHEMA_ID), "toaster schema hash mismatch")
    require(profile_hash == fnv1a32(TOASTER_PROFILE_ID), "toaster profile hash mismatch")
    recipes = data["recipes"]
    require(record_count == len(recipes), "toaster recipe count mismatch")
    for recipe_index, recipe in enumerate(recipes):
        first_tool_hash = int(recipe["required_tools"][0]["item_hash32"]) if recipe["required_tools"] else 0
        expected_record = (
            int(recipe["recipe_hash32"]),
            int(recipe["result"]["item_hash32"]),
            int(recipe["category_hash32"]),
            1 if bool(recipe.get("starter_item", False)) else 0,
            pack_toaster_meta(recipe),
            int(round(float(recipe["input_mass_kg"]) * KG_TO_GRAMS)),
            int(round(float(recipe["output_mass_kg"]) * KG_TO_GRAMS)),
            int(round(float(recipe["baseline_cost_units"]) * VALUE_UNITS_TO_MILLI)),
            int(round(float(recipe["PowerCost_kWh"]) * KWH_TO_MILLIWATT_HOURS)),
            int(round(float(recipe["FabricationTimeSeconds"]) * SECONDS_TO_DECISECONDS)),
            int(round(float(recipe["deconstruct_reclaim_ratio"]) * RATIO_TO_BASIS_POINTS)),
            first_tool_hash,
        )
        require(TOASTER_RECORD_STRUCT.unpack_from(binary, record_offset + recipe_index * record_stride) == expected_record, f"toaster record mismatch {recipe['recipe_id']}")
    return {"bytes": len(binary), "payload_crc32": payload_crc32, "record_count": record_count, "sha256": hashlib.sha256(binary).hexdigest()}


def main() -> None:
    data = load_json(JSON_PATH)
    recipes = data["recipes"]
    require(len(recipes) == TARGET_RECIPE_COUNT, "recipe count must be 50")
    require(data["status_id"] == "economy.crafting_costs.balanced", "status mismatch")
    hash_pairs = collect_id_hashes(data)
    catalog = build_catalog(data)
    power_model = data["power_model"]
    require(power_model["model_id"] == "craft.power.material_process_energy.v2", "power model mismatch")
    for recipe in recipes:
        input_mass = sum(float(ingredient["total_mass_kg"]) for ingredient in recipe["ingredients"])
        require(abs(float(recipe["input_mass_kg"]) - input_mass) <= 0.001, f"mass input mismatch {recipe['recipe_id']}")
        require(abs(float(recipe["output_mass_kg"]) - input_mass) <= 0.001, f"mass output mismatch {recipe['recipe_id']}")
        energy_terms = compute_energy_terms(recipe, catalog, power_model)
        for key, expected in energy_terms.items():
            tolerance = 0.001 if key == "total_kwh" else 0.000001
            require(abs(float(recipe["physical_energy_terms_kwh"][key]) - expected) <= tolerance, f"energy term mismatch {recipe['recipe_id']} {key}")
    full_info = verify_full_binary(data)
    toaster_info = verify_toaster_binary(data)
    project_atlas = PROJECT_ATLAS_PATH.read_text(encoding="utf-8", errors="replace")
    require("Data Monolith" in project_atlas and "Crafting Fast-Fail Validator" in project_atlas and "DataSovereignty" in project_atlas, "PROJECT_ATLAS missing required data domains")
    HASH_AUDIT_PATH.write_text(json.dumps({"schema_id": "economy.crafting_hash_audit.v1", "data_path": "Data/Economy/Crafting_Costs.json", "binary_path": "Data/Economy/Crafting_Costs.h8bin", "toaster_binary_path": "Data/Economy/Crafting_Costs_Toaster.h8bin", "data_sha256": hashlib.sha256(JSON_PATH.read_bytes()).hexdigest(), "binary_sha256": full_info["sha256"], "toaster_binary_sha256": toaster_info["sha256"], "hash_algorithm": data["hash_algorithm"], "hash_pairs": len(hash_pairs), "collisions": 0, "status": "CRAFTING_HASHES_COLLISION_FREE"}, separators=(",", ":")) + "\n", encoding="utf-8")
    SOVEREIGNTY_AUDIT_PATH.write_text(json.dumps({"schema_id": "economy.crafting_data_sovereignty_audit.v1", "evidence_class": "STATIC_DOC+CLI_PYTHON", "runtime_authority_path": "Data Monolith parses Crafting_Costs.h8bin into native/static database records", "toaster_runtime_path": "Celeron/i3 ingest can parse Crafting_Costs_Toaster.h8bin fixed records without ingredient/tool/God-Mode tables", "lookup_shape": "stateless hash-indexed records", "private_runtime_state_required": False, "binary_alignment_bytes": 16, "residual_risk": "Unity Data Monolith import remains PENDING VERIFICATION"}, separators=(",", ":")) + "\n", encoding="utf-8")
    print("CRAFTING COST VERIFY OK")
    print(f"binary_bytes={full_info['bytes']} toaster_binary_bytes={toaster_info['bytes']} recipe_count={full_info['recipe_count']} ingredient_count={full_info['ingredient_count']} tool_count={full_info['tool_count']} godmode_visual_count={full_info['godmode_visual_count']}")
    print(f"alignment=16 endian=< payload_crc32={full_info['payload_crc32']}")
    print(f"hash_pairs={len(hash_pairs)} collisions=0")
    print("project_atlas=Data Monolith + Crafting Fast-Fail Validator")


if __name__ == "__main__":
    main()
