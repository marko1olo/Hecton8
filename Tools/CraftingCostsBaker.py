#!/usr/bin/env python3
"""Bake existing CRAFTING_COST_BALANCER JSON into CSV and aligned binaries."""

from __future__ import annotations

import csv
import hashlib
import json
import struct
import zlib
from pathlib import Path
from typing import Any


FNV_OFFSET = 2166136261
FNV_PRIME = 16777619
UINT32_MASK = 0xFFFFFFFF
UINT32_BITS = 32
ROOT = Path(__file__).resolve().parents[1]
ECONOMY_DIR = ROOT / "Data" / "Economy"
JSON_PATH = ECONOMY_DIR / "Crafting_Costs.json"
CSV_PATH = ECONOMY_DIR / "Crafting_Costs.csv"
BINARY_PATH = ECONOMY_DIR / "Crafting_Costs.h8bin"
MANIFEST_PATH = ECONOMY_DIR / "Crafting_Costs.manifest.json"
TOASTER_BINARY_PATH = ECONOMY_DIR / "Crafting_Costs_Toaster.h8bin"
TOASTER_MANIFEST_PATH = ECONOMY_DIR / "Crafting_Costs_Toaster.manifest.json"

HEADER_STRUCT = struct.Struct("<4sIIIIIIIIIIIIIIIIIII")
RECIPE_STRUCT = struct.Struct("<IIIIHHHHIIIIIIIIII")
INGREDIENT_STRUCT = struct.Struct("<IHHII")
TOOL_STRUCT = struct.Struct("<IIII")
GODMODE_VISUAL_STRUCT = struct.Struct("<IIII")
TOASTER_HEADER_STRUCT = struct.Struct("<4sIIIIIIIIIIIIIII")
TOASTER_RECORD_STRUCT = struct.Struct("<IIIIIIIIIIII")
HEADER_BYTES = HEADER_STRUCT.size
RECIPE_STRIDE = RECIPE_STRUCT.size
INGREDIENT_STRIDE = INGREDIENT_STRUCT.size
TOOL_STRIDE = TOOL_STRUCT.size
GODMODE_VISUAL_STRIDE = GODMODE_VISUAL_STRUCT.size
TOASTER_HEADER_BYTES = TOASTER_HEADER_STRUCT.size
TOASTER_RECORD_STRIDE = TOASTER_RECORD_STRUCT.size
MAGIC = b"H8CR"
TOASTER_MAGIC = b"H8CT"
VERSION = 2
TOASTER_VERSION = 1
ENDIAN_PROBE = 0x01020304
BINARY_ALIGNMENT_BYTES = 16
ALIGNMENT_MASK = BINARY_ALIGNMENT_BYTES - 1
RESERVED_U32 = 0
RAW_RESOURCE_TIER = 0
STARTER_FLAG_BIT = 1

ROUNDING_EPSILON = 0.0000001
KG_TO_GRAMS = 1000.0
KG_TO_MILLIGRAMS = KG_TO_GRAMS * 1000.0
VALUE_UNITS_TO_MILLI = 1000.0
KWH_TO_MILLIWATT_HOURS = 1_000_000.0
SECONDS_TO_DECISECONDS = 10.0
RATIO_TO_BASIS_POINTS = 10_000.0
SURFACE_AREA_VOLUME_EXPONENT = 2.0 / 3.0
TARGET_RECIPE_COUNT = 50
TOASTER_PROFILE_ID = "craft.profile.toaster.binary_minimal"
GOD_MODE_PROFILE_ID = "craft.profile.godmode.hydraulic_arc_overkill"
TOASTER_BINARY_SCHEMA_ID = "economy.crafting_costs.toaster_binary.v1"
TOASTER_MANIFEST_SCHEMA_ID = "economy.crafting_costs.toaster_manifest.v1"

VISUAL_PACK_SPARK_MASK = 0xFFF
VISUAL_PACK_STEAM_MASK = 0xFF
VISUAL_PACK_OCTAVE_MASK = 0xF
VISUAL_PACK_HYDRAULIC_MASK = 0xFF
VISUAL_PACK_STEAM_SHIFT = 12
VISUAL_PACK_OCTAVE_SHIFT = 20
VISUAL_PACK_HYDRAULIC_SHIFT = 24
GRADIENT_PACK_SAMPLE_MASK = 0xFFFF
GRADIENT_PACK_HEAT_MASK = 0xFF
GRADIENT_PACK_CHANNEL_MASK = 0xFF
GRADIENT_PACK_HEAT_SHIFT = 16
GRADIENT_PACK_CHANNEL_SHIFT = 24
NOISE_PACK_FREQ_MASK = 0xFFFF
NOISE_PACK_AMPLITUDE_MASK = 0xFF
NOISE_PACK_RIDGE_MASK = 0xFF
NOISE_PACK_AMPLITUDE_SHIFT = 16
NOISE_PACK_RIDGE_SHIFT = 24
TOASTER_PACK_TIER_BITS = 8
TOASTER_PACK_UI_CLASS_BITS = 8
TOASTER_PACK_INGREDIENT_COUNT_BITS = 8
TOASTER_PACK_TOOL_COUNT_BITS = 8
TOASTER_PACK_TIER_MASK = (1 << TOASTER_PACK_TIER_BITS) - 1
TOASTER_PACK_UI_CLASS_MASK = (1 << TOASTER_PACK_UI_CLASS_BITS) - 1
TOASTER_PACK_INGREDIENT_COUNT_MASK = (1 << TOASTER_PACK_INGREDIENT_COUNT_BITS) - 1
TOASTER_PACK_TOOL_COUNT_MASK = (1 << TOASTER_PACK_TOOL_COUNT_BITS) - 1
TOASTER_PACK_UI_CLASS_SHIFT = TOASTER_PACK_TIER_BITS
TOASTER_PACK_INGREDIENT_COUNT_SHIFT = TOASTER_PACK_UI_CLASS_SHIFT + TOASTER_PACK_UI_CLASS_BITS
TOASTER_PACK_TOOL_COUNT_SHIFT = TOASTER_PACK_INGREDIENT_COUNT_SHIFT + TOASTER_PACK_INGREDIENT_COUNT_BITS


def fnv1a32(value: str) -> int:
    hash_value = FNV_OFFSET
    for byte in value.encode("utf-16le"):
        hash_value ^= byte
        hash_value = (hash_value * FNV_PRIME) & UINT32_MASK
    return hash_value


def align16(value: int) -> int:
    return (value + ALIGNMENT_MASK) & ~ALIGNMENT_MASK


def load_json(path: Path = JSON_PATH) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def build() -> dict[str, Any]:
    return load_json()


def collect_id_hashes(data: Any) -> dict[int, str]:
    pairs: dict[int, str] = {}

    def visit(node: Any) -> None:
        if isinstance(node, dict):
            for key, value in node.items():
                if key.endswith("_id") and isinstance(value, str):
                    hash_key = key[:-3] + "_hash32"
                    if hash_key not in node:
                        raise ValueError(f"missing {hash_key} for {value}")
                    expected = fnv1a32(value)
                    actual = int(node[hash_key])
                    if actual != expected:
                        raise ValueError(f"hash mismatch for {value}: {actual} != {expected}")
                    previous = pairs.get(actual)
                    if previous is not None and previous != value:
                        raise ValueError(f"FNV collision: {previous} and {value}")
                    pairs[actual] = value
                visit(value)
        elif isinstance(node, list):
            for value in node:
                visit(value)

    visit(data)
    return pairs


def build_catalog(data: dict[str, Any]) -> dict[str, dict[str, Any]]:
    catalog: dict[str, dict[str, Any]] = {}
    for resource in data["raw_resources"]:
        catalog[resource["item_id"]] = {
            "tier": RAW_RESOURCE_TIER,
            "category_hash32": fnv1a32("item.category.raw_resource"),
            "physical_profile": resource["physical_profile"],
            "source": "raw",
        }
    for recipe in data["recipes"]:
        catalog[recipe["result"]["item_id"]] = {
            "tier": int(recipe["progression_tier"]),
            "category_hash32": int(recipe["category_hash32"]),
            "physical_profile": recipe["result"]["physical_profile"],
            "source": "crafted",
        }
    return catalog


def pack_recipe_visual(god_mode: dict[str, Any]) -> int:
    return (
        (int(god_mode["spark_particles"]) & VISUAL_PACK_SPARK_MASK)
        | ((int(god_mode["steam_puffs"]) & VISUAL_PACK_STEAM_MASK) << VISUAL_PACK_STEAM_SHIFT)
        | ((int(god_mode["harmonic_noise_octaves"]) & VISUAL_PACK_OCTAVE_MASK) << VISUAL_PACK_OCTAVE_SHIFT)
        | ((min(VISUAL_PACK_HYDRAULIC_MASK, int(god_mode["hydraulic_jolt_ms"])) & VISUAL_PACK_HYDRAULIC_MASK) << VISUAL_PACK_HYDRAULIC_SHIFT)
    )


def pack_gradient_visual(god_mode: dict[str, Any]) -> int:
    return (
        (int(god_mode["gradient_sample_count"]) & GRADIENT_PACK_SAMPLE_MASK)
        | ((int(god_mode["gradient_heat_bands"]) & GRADIENT_PACK_HEAT_MASK) << GRADIENT_PACK_HEAT_SHIFT)
        | ((int(god_mode["gradient_spectral_channels"]) & GRADIENT_PACK_CHANNEL_MASK) << GRADIENT_PACK_CHANNEL_SHIFT)
    )


def pack_harmonic_noise(god_mode: dict[str, Any]) -> int:
    return (
        (int(god_mode["harmonic_noise_frequency_millihz"]) & NOISE_PACK_FREQ_MASK)
        | ((int(god_mode["harmonic_noise_amplitude_u8"]) & NOISE_PACK_AMPLITUDE_MASK) << NOISE_PACK_AMPLITUDE_SHIFT)
        | ((int(god_mode["harmonic_noise_ridge_mix_u8"]) & NOISE_PACK_RIDGE_MASK) << NOISE_PACK_RIDGE_SHIFT)
    )


def pack_toaster_meta(recipe: dict[str, Any]) -> int:
    return (
        (int(recipe["progression_tier"]) & TOASTER_PACK_TIER_MASK)
        | ((int(recipe["scalability_profiles"]["toaster"]["ui_cost_class_u8"]) & TOASTER_PACK_UI_CLASS_MASK) << TOASTER_PACK_UI_CLASS_SHIFT)
        | ((len(recipe["ingredients"]) & TOASTER_PACK_INGREDIENT_COUNT_MASK) << TOASTER_PACK_INGREDIENT_COUNT_SHIFT)
        | ((len(recipe["required_tools"]) & TOASTER_PACK_TOOL_COUNT_MASK) << TOASTER_PACK_TOOL_COUNT_SHIFT)
    )


def write_binary(data: dict[str, Any]) -> dict[str, Any]:
    recipes = data["recipes"]
    catalog = build_catalog(data)
    ingredient_count = sum(len(recipe["ingredients"]) for recipe in recipes)
    tool_count = sum(len(recipe["required_tools"]) for recipe in recipes)
    godmode_visual_count = len(recipes)
    recipe_offset = align16(HEADER_BYTES)
    ingredient_offset = align16(recipe_offset + len(recipes) * RECIPE_STRIDE)
    tool_offset = align16(ingredient_offset + ingredient_count * INGREDIENT_STRIDE)
    godmode_visual_offset = align16(tool_offset + tool_count * TOOL_STRIDE)
    file_size = align16(godmode_visual_offset + godmode_visual_count * GODMODE_VISUAL_STRIDE)
    blob = bytearray(file_size)
    ingredient_cursor = 0
    tool_cursor = 0
    for recipe_index, recipe in enumerate(recipes):
        ingredient_start = ingredient_cursor
        for ingredient in recipe["ingredients"]:
            INGREDIENT_STRUCT.pack_into(
                blob,
                ingredient_offset + ingredient_cursor * INGREDIENT_STRIDE,
                int(ingredient["item_hash32"]),
                int(ingredient["quantity"]),
                RESERVED_U32,
                int(round(float(ingredient["unit_mass_kg"]) * KG_TO_GRAMS)),
                int(round(float(ingredient["total_mass_kg"]) * KG_TO_GRAMS)),
            )
            ingredient_cursor += 1
        tool_start = tool_cursor
        for tool in recipe["required_tools"]:
            tool_record = catalog[tool["item_id"]]
            TOOL_STRUCT.pack_into(blob, tool_offset + tool_cursor * TOOL_STRIDE, int(tool["item_hash32"]), int(tool_record["tier"]), int(tool_record["category_hash32"]), RESERVED_U32)
            tool_cursor += 1
        god_mode = recipe["scalability_profiles"]["god_mode"]
        GODMODE_VISUAL_STRUCT.pack_into(
            blob,
            godmode_visual_offset + recipe_index * GODMODE_VISUAL_STRIDE,
            int(god_mode["arc_gradient_lut_hash32"]),
            int(god_mode["harmonic_noise_seed_hash32"]),
            pack_gradient_visual(god_mode),
            pack_harmonic_noise(god_mode),
        )
        RECIPE_STRUCT.pack_into(
            blob,
            recipe_offset + recipe_index * RECIPE_STRIDE,
            int(recipe["recipe_hash32"]),
            int(recipe["result"]["item_hash32"]),
            int(recipe["category_hash32"]),
            STARTER_FLAG_BIT if bool(recipe.get("starter_item", False)) else RESERVED_U32,
            int(recipe["progression_tier"]),
            len(recipe["ingredients"]),
            len(recipe["required_tools"]),
            int(recipe["scalability_profiles"]["toaster"]["ui_cost_class_u8"]),
            ingredient_start,
            tool_start,
            int(round(float(recipe["input_mass_kg"]) * KG_TO_GRAMS)),
            int(round(float(recipe["output_mass_kg"]) * KG_TO_GRAMS)),
            int(round(float(recipe["baseline_cost_units"]) * VALUE_UNITS_TO_MILLI)),
            int(round(float(recipe["PowerCost_kWh"]) * KWH_TO_MILLIWATT_HOURS)),
            int(round(float(recipe["FabricationTimeSeconds"]) * SECONDS_TO_DECISECONDS)),
            int(round(float(recipe["deconstruct_reclaim_ratio"]) * RATIO_TO_BASIS_POINTS)),
            int(god_mode["fabricator_fx_hash32"]),
            pack_recipe_visual(god_mode),
        )
    payload_crc32 = zlib.crc32(bytes(blob[recipe_offset:file_size])) & UINT32_MASK
    HEADER_STRUCT.pack_into(blob, RESERVED_U32, MAGIC, VERSION, ENDIAN_PROBE, HEADER_BYTES, len(recipes), RECIPE_STRIDE, ingredient_count, INGREDIENT_STRIDE, tool_count, TOOL_STRIDE, godmode_visual_count, GODMODE_VISUAL_STRIDE, recipe_offset, ingredient_offset, tool_offset, godmode_visual_offset, file_size, payload_crc32, RESERVED_U32, RESERVED_U32)
    BINARY_PATH.write_bytes(blob)
    json_bytes = JSON_PATH.read_bytes()
    manifest = {
        "schema_id": "economy.crafting_costs.binary_manifest.v1",
        "schema_hash32": fnv1a32("economy.crafting_costs.binary_manifest.v1"),
        "binary_path": "Data/Economy/Crafting_Costs.h8bin",
        "json_path": "Data/Economy/Crafting_Costs.json",
        "magic_ascii": MAGIC.decode("ascii"),
        "version": VERSION,
        "endianness": "little",
        "endian_probe_u32": ENDIAN_PROBE,
        "header_struct": HEADER_STRUCT.format,
        "recipe_struct": RECIPE_STRUCT.format,
        "ingredient_struct": INGREDIENT_STRUCT.format,
        "tool_struct": TOOL_STRUCT.format,
        "godmode_visual_struct": GODMODE_VISUAL_STRUCT.format,
        "header_bytes": HEADER_BYTES,
        "recipe_stride": RECIPE_STRIDE,
        "ingredient_stride": INGREDIENT_STRIDE,
        "tool_stride": TOOL_STRIDE,
        "godmode_visual_stride": GODMODE_VISUAL_STRIDE,
        "recipe_count": len(recipes),
        "ingredient_count": ingredient_count,
        "tool_count": tool_count,
        "godmode_visual_count": godmode_visual_count,
        "recipe_offset": recipe_offset,
        "ingredient_offset": ingredient_offset,
        "tool_offset": tool_offset,
        "godmode_visual_offset": godmode_visual_offset,
        "file_size_bytes": file_size,
        "alignment_bytes": BINARY_ALIGNMENT_BYTES,
        "aligned_offsets": [recipe_offset, ingredient_offset, tool_offset, godmode_visual_offset, file_size],
        "payload_crc32": payload_crc32,
        "binary_sha256": hashlib.sha256(blob).hexdigest(),
        "json_sha256": hashlib.sha256(json_bytes).hexdigest(),
    }
    MANIFEST_PATH.write_text(json.dumps(manifest, separators=(",", ":")) + "\n", encoding="utf-8")
    return manifest


def write_toaster_binary(data: dict[str, Any]) -> dict[str, Any]:
    recipes = data["recipes"]
    record_offset = align16(TOASTER_HEADER_BYTES)
    file_size = align16(record_offset + len(recipes) * TOASTER_RECORD_STRIDE)
    blob = bytearray(file_size)
    for recipe_index, recipe in enumerate(recipes):
        first_tool_hash = int(recipe["required_tools"][0]["item_hash32"]) if recipe["required_tools"] else RESERVED_U32
        TOASTER_RECORD_STRUCT.pack_into(
            blob,
            record_offset + recipe_index * TOASTER_RECORD_STRIDE,
            int(recipe["recipe_hash32"]),
            int(recipe["result"]["item_hash32"]),
            int(recipe["category_hash32"]),
            STARTER_FLAG_BIT if bool(recipe.get("starter_item", False)) else RESERVED_U32,
            pack_toaster_meta(recipe),
            int(round(float(recipe["input_mass_kg"]) * KG_TO_GRAMS)),
            int(round(float(recipe["output_mass_kg"]) * KG_TO_GRAMS)),
            int(round(float(recipe["baseline_cost_units"]) * VALUE_UNITS_TO_MILLI)),
            int(round(float(recipe["PowerCost_kWh"]) * KWH_TO_MILLIWATT_HOURS)),
            int(round(float(recipe["FabricationTimeSeconds"]) * SECONDS_TO_DECISECONDS)),
            int(round(float(recipe["deconstruct_reclaim_ratio"]) * RATIO_TO_BASIS_POINTS)),
            first_tool_hash,
        )
    payload_crc32 = zlib.crc32(bytes(blob[record_offset:file_size])) & UINT32_MASK
    binary_schema_hash = fnv1a32(TOASTER_BINARY_SCHEMA_ID)
    profile_hash = fnv1a32(TOASTER_PROFILE_ID)
    TOASTER_HEADER_STRUCT.pack_into(blob, RESERVED_U32, TOASTER_MAGIC, TOASTER_VERSION, ENDIAN_PROBE, TOASTER_HEADER_BYTES, len(recipes), TOASTER_RECORD_STRIDE, record_offset, file_size, payload_crc32, binary_schema_hash, profile_hash, RESERVED_U32, RESERVED_U32, RESERVED_U32, RESERVED_U32, RESERVED_U32)
    TOASTER_BINARY_PATH.write_bytes(blob)
    json_bytes = JSON_PATH.read_bytes()
    manifest = {
        "schema_id": TOASTER_MANIFEST_SCHEMA_ID,
        "schema_hash32": fnv1a32(TOASTER_MANIFEST_SCHEMA_ID),
        "binary_schema_id": TOASTER_BINARY_SCHEMA_ID,
        "binary_schema_hash32": binary_schema_hash,
        "profile_id": TOASTER_PROFILE_ID,
        "profile_hash32": profile_hash,
        "binary_path": "Data/Economy/Crafting_Costs_Toaster.h8bin",
        "json_path": "Data/Economy/Crafting_Costs.json",
        "magic_ascii": TOASTER_MAGIC.decode("ascii"),
        "version": TOASTER_VERSION,
        "endianness": "little",
        "endian_probe_u32": ENDIAN_PROBE,
        "header_struct": TOASTER_HEADER_STRUCT.format,
        "record_struct": TOASTER_RECORD_STRUCT.format,
        "header_bytes": TOASTER_HEADER_BYTES,
        "record_stride": TOASTER_RECORD_STRIDE,
        "record_count": len(recipes),
        "record_offset": record_offset,
        "file_size_bytes": file_size,
        "alignment_bytes": BINARY_ALIGNMENT_BYTES,
        "aligned_offsets": [record_offset, file_size],
        "omitted_tables": ["ingredients", "required_tools", "godmode_visuals"],
        "payload_crc32": payload_crc32,
        "binary_sha256": hashlib.sha256(blob).hexdigest(),
        "json_sha256": hashlib.sha256(json_bytes).hexdigest(),
    }
    TOASTER_MANIFEST_PATH.write_text(json.dumps(manifest, separators=(",", ":")) + "\n", encoding="utf-8")
    return manifest


def write_csv(data: dict[str, Any]) -> None:
    fieldnames = ("recipe_id", "recipe_hash32", "result_item_id", "result_item_hash32", "display_name", "category_id", "progression_tier", "input_mass_kg", "output_mass_kg", "baseline_cost_units", "PowerCost_kWh", "FabricationTimeSeconds", "required_tools", "ingredient_summary")
    with CSV_PATH.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        for recipe in data["recipes"]:
            writer.writerow({
                "recipe_id": recipe["recipe_id"],
                "recipe_hash32": recipe["recipe_hash32"],
                "result_item_id": recipe["result"]["item_id"],
                "result_item_hash32": recipe["result"]["item_hash32"],
                "display_name": recipe["display_name"],
                "category_id": recipe["category_id"],
                "progression_tier": recipe["progression_tier"],
                "input_mass_kg": f"{recipe['input_mass_kg']:.3f}",
                "output_mass_kg": f"{recipe['output_mass_kg']:.3f}",
                "baseline_cost_units": f"{recipe['baseline_cost_units']:.3f}",
                "PowerCost_kWh": f"{recipe['PowerCost_kWh']:.3f}",
                "FabricationTimeSeconds": f"{recipe['FabricationTimeSeconds']:.1f}",
                "required_tools": "|".join(tool["item_id"] for tool in recipe["required_tools"]),
                "ingredient_summary": "|".join(f"{entry['item_id']}x{entry['quantity']}" for entry in recipe["ingredients"]),
            })


def main() -> None:
    data = build()
    collect_id_hashes(data)
    JSON_PATH.write_text(json.dumps(data, separators=(",", ":")) + "\n", encoding="utf-8")
    write_csv(data)
    manifest = write_binary(data)
    toaster_manifest = write_toaster_binary(data)
    medians = data["tier_medians"]
    print(f"CRAFTING_COSTS_BAKED recipes={len(data['recipes'])}")
    print(f"tier_cost_ratios={medians['tier2_to_tier1_cost_ratio']},{medians['tier3_to_tier2_cost_ratio']}")
    print(f"tier_power_ratios={medians['tier2_to_tier1_power_ratio']},{medians['tier3_to_tier2_power_ratio']}")
    print(f"starter_o2_minutes={data['starter_edge_guard']['total_minutes']}")
    print(f"binary_bytes={manifest['file_size_bytes']} payload_crc32={manifest['payload_crc32']} sha256={manifest['binary_sha256']}")
    print(f"toaster_binary_bytes={toaster_manifest['file_size_bytes']} payload_crc32={toaster_manifest['payload_crc32']} sha256={toaster_manifest['binary_sha256']}")


if __name__ == "__main__":
    main()
