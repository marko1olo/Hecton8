#!/usr/bin/env python3
"""Bake Data/Economy/Items.csv from generated economy JSON/CSV sources."""

from __future__ import annotations

import argparse
import csv
import json
from pathlib import Path
from typing import Any


FNV_OFFSET = 2166136261
FNV_PRIME = 16777619
RAW_CATEGORY_ID = "item.category.raw_resource"

HEADERS = (
    "item_id",
    "item_hash32",
    "display_name",
    "item_kind",
    "source_recipe_id",
    "source_recipe_hash32",
    "category_id",
    "category_hash32",
    "progression_tier",
    "baseline_value_units",
    "is_raw_resource",
    "resource_tier",
    "biome_count",
    "positive_spawn_biome_count",
)


def fnv1a32(value: str) -> int:
    hash_value = FNV_OFFSET
    for byte in value.encode("utf-16le"):
        hash_value ^= byte
        hash_value = (hash_value * FNV_PRIME) & 0xFFFFFFFF
    return hash_value


def load_json(path: Path) -> Any:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def load_resource_rows(path: Path) -> dict[str, dict[str, Any]]:
    resources: dict[str, dict[str, Any]] = {}
    with path.open("r", encoding="utf-8", newline="") as handle:
        for row in csv.DictReader(handle):
            resource_id = row["resource_id"]
            entry = resources.setdefault(
                resource_id,
                {
                    "display_name": row["resource_display_name"],
                    "tier": int(row["resource_tier"]),
                    "biomes": set(),
                    "positive_biomes": set(),
                },
            )
            entry["biomes"].add(row["biome_id"])
            if int(row["lcg_spawn_weight_u16"]) > 0:
                entry["positive_biomes"].add(row["biome_id"])
    return resources


def bake(root: Path) -> int:
    economy_dir = root / "Data" / "Economy"
    recipes_data = load_json(economy_dir / "Recipes.json")
    resources = load_resource_rows(economy_dir / "Resource_Distribution_Matrix.csv")
    recipes_by_result = {recipe["result"]["item_id"]: recipe for recipe in recipes_data["recipes"]}
    rows: list[dict[str, Any]] = []
    raw_category_hash = fnv1a32(RAW_CATEGORY_ID)

    for item in sorted(recipes_data["item_values"], key=lambda entry: entry["item_id"]):
        item_id = item["item_id"]
        recipe = recipes_by_result.get(item_id)
        resource = resources.get(item_id)
        is_raw = resource is not None
        if recipe is not None:
            source_recipe_id = recipe["recipe_id"]
            source_recipe_hash = recipe["recipe_hash32"]
            category_id = recipe["category_id"]
            category_hash = recipe["category_hash32"]
            tier = int(recipe["progression_tier"])
            display_name = recipe["display_name"]
            item_kind = "crafted"
        else:
            source_recipe_id = ""
            source_recipe_hash = ""
            category_id = RAW_CATEGORY_ID if is_raw else "item.category.external"
            category_hash = raw_category_hash if is_raw else fnv1a32(category_id)
            tier = int(resource["tier"]) if is_raw else 0
            display_name = resource["display_name"] if is_raw else item_id
            item_kind = "raw_resource" if is_raw else "external"

        rows.append(
            {
                "item_id": item_id,
                "item_hash32": item["item_hash32"],
                "display_name": display_name,
                "item_kind": item_kind,
                "source_recipe_id": source_recipe_id,
                "source_recipe_hash32": source_recipe_hash,
                "category_id": category_id,
                "category_hash32": category_hash,
                "progression_tier": tier,
                "baseline_value_units": f"{float(item['baseline_value_units']):.2f}",
                "is_raw_resource": "true" if is_raw else "false",
                "resource_tier": resource["tier"] if is_raw else "",
                "biome_count": len(resource["biomes"]) if is_raw else "",
                "positive_spawn_biome_count": len(resource["positive_biomes"]) if is_raw else "",
            }
        )

    output_path = economy_dir / "Items.csv"
    with output_path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=HEADERS, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)
    print(f"items_csv_rows={len(rows)} path={output_path}")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=".", help="Repository root.")
    args = parser.parse_args()
    return bake(Path(args.root))


if __name__ == "__main__":
    raise SystemExit(main())
