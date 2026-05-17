#!/usr/bin/env python3
"""Verify CRAFTING_COST_BALANCER source contracts and literal hygiene."""

from __future__ import annotations

import json
import re
from pathlib import Path

from CraftingCostsBaker import (
    BINARY_ALIGNMENT_BYTES,
    BINARY_PATH,
    CSV_PATH,
    GODMODE_VISUAL_STRUCT,
    HEADER_STRUCT,
    INGREDIENT_STRUCT,
    JSON_PATH,
    KG_TO_GRAMS,
    KG_TO_MILLIGRAMS,
    KWH_TO_MILLIWATT_HOURS,
    MANIFEST_PATH,
    RATIO_TO_BASIS_POINTS,
    RECIPE_STRUCT,
    SECONDS_TO_DECISECONDS,
    SURFACE_AREA_VOLUME_EXPONENT,
    TARGET_RECIPE_COUNT,
    TOOL_STRUCT,
    TOASTER_BINARY_PATH,
    TOASTER_BINARY_SCHEMA_ID,
    TOASTER_HEADER_STRUCT,
    TOASTER_MANIFEST_PATH,
    TOASTER_MANIFEST_SCHEMA_ID,
    TOASTER_PROFILE_ID,
    TOASTER_RECORD_STRUCT,
    TOASTER_VERSION,
    UINT32_BITS,
    UINT32_MASK,
    VALUE_UNITS_TO_MILLI,
    VERSION,
    fnv1a32,
)


ROOT = Path(__file__).resolve().parents[1]
REPORT_PATH = ROOT / "Docs" / "AgentLogs" / "Crafting_SourceContract_Audit.json"
OWNED_CONSUMER_FILES = (
    ROOT / "Tools" / "VerifyCraftingCosts.py",
    ROOT / "Tools" / "CraftingEconomyMonteCarlo.py",
)
FORBIDDEN_LITERAL_PATTERNS = (
    re.compile(r"\*\s*1000\.0"),
    re.compile(r"\*\s*1_000_000\.0"),
    re.compile(r"\*\s*10\.0"),
    re.compile(r"\*\s*10_000\.0"),
    re.compile(r"2\.0\s*/\s*3\.0"),
    re.compile(r"0\.0000001"),
    re.compile(r"%\s*16"),
)


def fail(message: str) -> None:
    raise SystemExit(f"CRAFTING SOURCE CONTRACT VERIFY FAILED: {message}")


def require(condition: bool, message: str) -> None:
    if not condition:
        fail(message)


def load_json(path: Path) -> dict[str, object]:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def scan_forbidden_literals() -> list[dict[str, object]]:
    hits: list[dict[str, object]] = []
    for path in OWNED_CONSUMER_FILES:
        for line_number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            for pattern in FORBIDDEN_LITERAL_PATTERNS:
                if pattern.search(line):
                    hits.append({"file": str(path.relative_to(ROOT)).replace("\\", "/"), "line": line_number, "pattern": pattern.pattern, "text": line.strip()})
    return hits


def main() -> None:
    data = load_json(JSON_PATH)
    manifest = load_json(MANIFEST_PATH)
    toaster_manifest = load_json(TOASTER_MANIFEST_PATH)
    binary_contract = data["binary_contract"]
    toaster_contract = data["toaster_binary_contract"]
    power_model = data["power_model"]
    binary_scale_model = data["binary_scale_model"]
    literal_hits = scan_forbidden_literals()
    require(not literal_hits, f"raw verifier/simulator literals remain: {literal_hits[:3]}")
    require(int(binary_contract["version"]) == VERSION, "binary version drift")
    require(int(binary_contract["header_bytes"]) == HEADER_STRUCT.size, "header size drift")
    require(int(binary_contract["recipe_stride"]) == RECIPE_STRUCT.size, "recipe stride drift")
    require(int(binary_contract["ingredient_stride"]) == INGREDIENT_STRUCT.size, "ingredient stride drift")
    require(int(binary_contract["tool_stride"]) == TOOL_STRUCT.size, "tool stride drift")
    require(int(binary_contract["godmode_visual_stride"]) == GODMODE_VISUAL_STRUCT.size, "godmode stride drift")
    require(int(binary_contract["alignment_bytes"]) == BINARY_ALIGNMENT_BYTES, "alignment drift")
    require(int(binary_contract["recipe_count"]) == TARGET_RECIPE_COUNT, "recipe count drift")
    require(int(toaster_contract["version"]) == TOASTER_VERSION, "toaster version drift")
    require(int(toaster_contract["header_bytes"]) == TOASTER_HEADER_STRUCT.size, "toaster header drift")
    require(int(toaster_contract["record_stride"]) == TOASTER_RECORD_STRUCT.size, "toaster record drift")
    require(int(toaster_contract["schema_hash32"]) == fnv1a32(TOASTER_BINARY_SCHEMA_ID), "toaster schema hash drift")
    require(int(toaster_manifest["schema_hash32"]) == fnv1a32(TOASTER_MANIFEST_SCHEMA_ID), "toaster manifest hash drift")
    require(int(toaster_manifest["profile_hash32"]) == fnv1a32(TOASTER_PROFILE_ID), "toaster profile hash drift")
    require(float(binary_scale_model["kg_to_grams"]) == KG_TO_GRAMS, "kg scale drift")
    require(float(binary_scale_model["value_units_to_milli"]) == VALUE_UNITS_TO_MILLI, "value scale drift")
    require(float(binary_scale_model["kwh_to_milliwatt_hours"]) == KWH_TO_MILLIWATT_HOURS, "kWh scale drift")
    require(float(binary_scale_model["seconds_to_deciseconds"]) == SECONDS_TO_DECISECONDS, "time scale drift")
    require(float(binary_scale_model["ratio_to_basis_points"]) == RATIO_TO_BASIS_POINTS, "ratio scale drift")
    require(abs(float(power_model["surface_area_volume_exponent"]) - SURFACE_AREA_VOLUME_EXPONENT) <= float(power_model.get("rounding_epsilon", 0.0000001)), "surface exponent drift")
    constants = {
        "UINT32_MASK": UINT32_MASK,
        "UINT32_BITS": UINT32_BITS,
        "KG_TO_MILLIGRAMS": KG_TO_MILLIGRAMS,
    }
    report = {
        "agent": "CRAFTING_COST_BALANCER",
        "domain": "DATA/ECONOMY",
        "status": "CRAFTING_SOURCE_CONTRACT_VERIFIED",
        "json_path": str(JSON_PATH.relative_to(ROOT)).replace("\\", "/"),
        "csv_path": str(CSV_PATH.relative_to(ROOT)).replace("\\", "/"),
        "binary_path": str(BINARY_PATH.relative_to(ROOT)).replace("\\", "/"),
        "manifest_path": str(MANIFEST_PATH.relative_to(ROOT)).replace("\\", "/"),
        "toaster_binary_path": str(TOASTER_BINARY_PATH.relative_to(ROOT)).replace("\\", "/"),
        "toaster_manifest_path": str(TOASTER_MANIFEST_PATH.relative_to(ROOT)).replace("\\", "/"),
        "manifest_json_sha256": manifest["json_sha256"],
        "manifest_binary_sha256": manifest["binary_sha256"],
        "toaster_manifest_json_sha256": toaster_manifest["json_sha256"],
        "toaster_manifest_binary_sha256": toaster_manifest["binary_sha256"],
        "literal_hit_count": len(literal_hits),
        "consumer_files_checked": [str(path.relative_to(ROOT)).replace("\\", "/") for path in OWNED_CONSUMER_FILES],
        "exported_constants": constants,
    }
    REPORT_PATH.write_text(json.dumps(report, indent=2, sort_keys=True), encoding="utf-8")
    print("CRAFTING SOURCE CONTRACT VERIFY OK")
    print(f"literal_hit_count={len(literal_hits)}")
    print(f"report={REPORT_PATH.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
