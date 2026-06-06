#!/usr/bin/env python3
"""Validate current Batch31 local PBR import-intent artifacts."""

from __future__ import annotations

import csv
import hashlib
import json
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
CSV_PATH = ROOT / "Docs/AssetAudit/BATCH31_LOCAL_PBR_IMPORT_INTENT_20260605.csv"
JSON_PATH = ROOT / "Docs/AssetAudit/BATCH31_LOCAL_PBR_IMPORT_INTENT_20260605.json"
MD_PATH = ROOT / "Docs/AssetAudit/BATCH31_LOCAL_PBR_IMPORT_INTENT_20260605.md"
STATIC_VALIDATION_PATH = ROOT / "Docs/AssetAudit/BATCH31_LOCAL_PBR_IMPORT_INTENT_STATIC_VALIDATION_20260605.md"

EXPECTED_PACKAGES = (
    "TX_B31_WetBasaltShoreline_1429",
    "TX_B31_PhoticSeabedSubstrate_2102",
    "TX_B31_PhoticShellSandSubstrate_2102",
)
EXPECTED_ROLE_KEYS = (
    "albedo",
    "height",
    "mrao",
    "normal",
    "normal_tile2x2",
    "source_crop",
    "tile2x2",
)
EXPECTED_COLUMNS = 38
EXPECTED_ROWS = 21
EXPECTED_EMPTY_CELLS = 36
EXPECTED_RUNTIME_ROLES = {"albedo", "normal"}
EXPECTED_REFERENCE_ROLES = {"normal_tile2x2", "source_crop", "tile2x2"}


def display_path(path: Path) -> str:
    try:
        return str(path.relative_to(ROOT))
    except ValueError:
        return str(path)


def load_csv_rows(path: Path = CSV_PATH) -> tuple[list[str], list[dict[str, str]], int]:
    if not path.exists():
        raise SystemExit(f"FAIL: missing Batch31 import-intent CSV: {display_path(path)}")

    empty_cells = 0
    rows: list[dict[str, str]] = []
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        headers = list(reader.fieldnames or ())
        if len(headers) != EXPECTED_COLUMNS:
            raise SystemExit(f"FAIL: Batch31 import-intent CSV expected {EXPECTED_COLUMNS} columns, got {len(headers)}")

        for row_number, row in enumerate(reader, start=2):
            clean: dict[str, str] = {}
            for header in headers:
                value = (row.get(header) or "").strip()
                if not value:
                    empty_cells += 1
                clean[header] = value
            rows.append(clean)

            if not clean["package_id"] or not clean["role_key"] or not clean["path"]:
                raise SystemExit(f"FAIL: malformed Batch31 CSV row {row_number}")

    if len(rows) != EXPECTED_ROWS:
        raise SystemExit(f"FAIL: expected {EXPECTED_ROWS} Batch31 import-intent rows, got {len(rows)}")
    if empty_cells != EXPECTED_EMPTY_CELLS:
        raise SystemExit(f"FAIL: expected {EXPECTED_EMPTY_CELLS} intentional sparse cells, got {empty_cells}")
    return headers, rows, empty_cells


def load_json(path: Path = JSON_PATH) -> dict[str, Any]:
    if not path.exists():
        raise SystemExit(f"FAIL: missing Batch31 import-intent JSON: {display_path(path)}")
    with path.open("r", encoding="utf-8") as handle:
        data = json.load(handle)
    if not isinstance(data, dict):
        raise SystemExit("FAIL: Batch31 import-intent JSON root must be an object")
    return data


def load_text(path: Path) -> str:
    if not path.exists():
        raise SystemExit(f"FAIL: missing Batch31 import-intent text artifact: {display_path(path)}")
    return path.read_text(encoding="utf-8")


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def require_int(row: dict[str, str], key: str, expected: int, row_id: str) -> None:
    try:
        actual = int(row[key])
    except ValueError as exc:
        raise SystemExit(f"FAIL: {row_id} {key} must be int: {row[key]}") from exc
    if actual != expected:
        raise SystemExit(f"FAIL: {row_id} {key} expected {expected}, actual {actual}")


def validate_csv_rows(rows: list[dict[str, str]], root: Path = ROOT) -> tuple[int, int, int]:
    by_package: dict[str, set[str]] = {package_id: set() for package_id in EXPECTED_PACKAGES}
    runtime_rows = 0
    blocked_rows = 0
    pass_rows = 0

    for row in rows:
        package_id = row["package_id"]
        role_key = row["role_key"]
        row_id = f"{package_id}:{role_key}"
        if package_id not in by_package:
            raise SystemExit(f"FAIL: unexpected Batch31 package: {package_id}")
        if role_key not in EXPECTED_ROLE_KEYS:
            raise SystemExit(f"FAIL: {row_id} unexpected role")
        if role_key in by_package[package_id]:
            raise SystemExit(f"FAIL: duplicate Batch31 role: {row_id}")
        by_package[package_id].add(role_key)

        file_path = root / row["path"]
        if not file_path.exists():
            raise SystemExit(f"FAIL: {row_id} missing source file: {row['path']}")
        if sha256_file(file_path).lower() != row["sha256_expected"].lower():
            raise SystemExit(f"FAIL: {row_id} sha256_expected mismatch")
        if row["sha256_actual"].lower() != row["sha256_expected"].lower() or row["sha256_match"] != "1":
            raise SystemExit(f"FAIL: {row_id} recorded sha256 mismatch")

        require_int(row, "exists", 1, row_id)
        require_int(row, "width", 1024, row_id)
        require_int(row, "height", 1024, row_id)
        require_int(row, "read_write", 0, row_id)

        if role_key in EXPECTED_RUNTIME_ROLES:
            require_int(row, "runtime_import", 1, row_id)
            runtime_rows += 1
        else:
            require_int(row, "runtime_import", 0, row_id)

        if role_key == "albedo":
            require_int(row, "srgb", 1, row_id)
            if row["texture_type"] != "Default" or row["standalone_format"] != "BC7":
                raise SystemExit(f"FAIL: {row_id} albedo import role drift")
        elif role_key == "normal":
            require_int(row, "srgb", 0, row_id)
            if row["texture_type"] != "NormalMap" or row["standalone_format"] != "BC5":
                raise SystemExit(f"FAIL: {row_id} normal import role drift")
        elif role_key == "mrao":
            require_int(row, "srgb", 0, row_id)
            if row["verdict"] != "BLOCKED":
                raise SystemExit(f"FAIL: {row_id} MRAO must remain BLOCKED")
            if row["issues"] != "blocked_channel_semantics_mrao_vs_arm":
                raise SystemExit(f"FAIL: {row_id} MRAO issue must remain channel semantics block")
            if "mrao_R_channel_flat_review" not in row["warnings"]:
                raise SystemExit(f"FAIL: {row_id} MRAO warning must keep R-channel review")
            if "BLOCKED_CHANNEL_SEMANTICS" not in row["channel_contract"]:
                raise SystemExit(f"FAIL: {row_id} MRAO channel contract lost blocker")
            if "_MasterShadowParams.w=3" not in row["channel_contract"]:
                raise SystemExit(f"FAIL: {row_id} MRAO channel contract lost Hecton_Master_Lit layout requirement")
            blocked_rows += 1
        elif role_key in EXPECTED_REFERENCE_ROLES:
            if row["texture_type"] != "ReferenceOnly":
                raise SystemExit(f"FAIL: {row_id} reference preview must stay ReferenceOnly")
            if row["standalone_format"] != "DO_NOT_IMPORT_AS_RUNTIME_TEXTURE":
                raise SystemExit(f"FAIL: {row_id} reference preview import format drift")
            for max_key in ("max_size_low", "max_size_middle", "max_size_high", "max_size_ultra"):
                require_int(row, max_key, 0, row_id)

        if row["verdict"] == "PASS_STATIC":
            if row["issues"] or row["warnings"]:
                raise SystemExit(f"FAIL: {row_id} PASS_STATIC row must keep empty issues/warnings")
            pass_rows += 1

    for package_id, roles in by_package.items():
        missing = [role for role in EXPECTED_ROLE_KEYS if role not in roles]
        if missing:
            raise SystemExit(f"FAIL: {package_id} missing role(s): {', '.join(missing)}")

    if runtime_rows != 6 or blocked_rows != 3 or pass_rows != 18:
        raise SystemExit(
            f"FAIL: Batch31 counts drift runtime={runtime_rows} blocked={blocked_rows} pass={pass_rows}"
        )
    return runtime_rows, blocked_rows, pass_rows


def validate_json(data: dict[str, Any], rows: list[dict[str, str]]) -> None:
    if data.get("evidenceClass") != "STATIC_SOURCE":
        raise SystemExit("FAIL: Batch31 import-intent JSON evidenceClass must remain STATIC_SOURCE")
    if data.get("evidenceScope") != "STATIC_IMAGE_IMPORT_INTENT":
        raise SystemExit("FAIL: Batch31 import-intent JSON evidenceScope must remain STATIC_IMAGE_IMPORT_INTENT")
    if data.get("notUnityImported") is not True or data.get("notVisualAcceptance") is not True:
        raise SystemExit("FAIL: Batch31 import-intent JSON must keep no-Unity/no-visual boundary")

    summary = data.get("summary")
    if not isinstance(summary, dict):
        raise SystemExit("FAIL: Batch31 import-intent JSON summary must be object")
    expected_summary = {
        "packages": 3,
        "rows": 21,
        "runtimeRows": 6,
        "blockedRows": 3,
        "errorRows": 0,
        "reviewRows": 0,
        "passStaticRows": 18,
        "channelContractBlockedPackages": 3,
    }
    for key, expected in expected_summary.items():
        if summary.get(key) != expected:
            raise SystemExit(f"FAIL: Batch31 import-intent JSON summary {key} expected {expected}, actual {summary.get(key)}")

    packages = data.get("packages")
    if not isinstance(packages, list) or len(packages) != len(EXPECTED_PACKAGES):
        raise SystemExit("FAIL: Batch31 import-intent JSON must list exactly three packages")
    for package in packages:
        if package.get("id") not in EXPECTED_PACKAGES:
            raise SystemExit(f"FAIL: unexpected Batch31 JSON package: {package.get('id')}")
        if package.get("verdict") != "BLOCKED":
            raise SystemExit(f"FAIL: Batch31 JSON package must remain BLOCKED: {package.get('id')}")
        warnings = package.get("warnings")
        if not isinstance(warnings, list) or "blocked_channel_semantics_mrao_vs_arm" not in warnings:
            raise SystemExit(f"FAIL: Batch31 JSON package lost channel-semantics warning: {package.get('id')}")

    json_rows = data.get("rows")
    if not isinstance(json_rows, list) or len(json_rows) != len(rows):
        raise SystemExit("FAIL: Batch31 import-intent JSON row count mismatch")
    csv_keys = {(row["package_id"], row["role_key"], row["sha256_expected"]) for row in rows}
    json_keys = {(str(row.get("package_id")), str(row.get("role_key")), str(row.get("sha256_expected"))) for row in json_rows}
    if csv_keys != json_keys:
        raise SystemExit("FAIL: Batch31 import-intent JSON rows do not match CSV package/role/hash keys")


def validate_docs() -> None:
    md_text = load_text(MD_PATH)
    validation_text = load_text(STATIC_VALIDATION_PATH)
    required_md_terms = (
        "Unity was not run.",
        "Do not import Batch31 `MRAOSource` as `_MaskMap` by name alone.",
        "Static checksum and image-channel inspection do not prove Unity import settings",
    )
    required_validation_terms = (
        "STATIC_VALIDATION_ONLY / BLOCKED BEFORE UNITY PROMOTION",
        "Command:",
        "python -m unittest Tools/test_batch31_local_pbr_import_intent.py",
        "Final status: `STATIC_VALIDATION_ONLY / BLOCKED BEFORE UNITY PROMOTION`.",
    )
    for term in required_md_terms:
        if term not in md_text:
            raise SystemExit(f"FAIL: Batch31 import-intent markdown missing term: {term}")
    for term in required_validation_terms:
        if term not in validation_text:
            raise SystemExit(f"FAIL: Batch31 static validation markdown missing term: {term}")


def validate_batch31_local_pbr_import_intent_artifacts() -> tuple[int, int, int, int]:
    _headers, rows, empty_cells = load_csv_rows()
    runtime_rows, blocked_rows, pass_rows = validate_csv_rows(rows)
    validate_json(load_json(), rows)
    validate_docs()
    return len(rows), empty_cells, runtime_rows, blocked_rows


def main() -> None:
    rows, empty_cells, runtime_rows, blocked_rows = validate_batch31_local_pbr_import_intent_artifacts()
    print(
        "BATCH31_LOCAL_PBR_IMPORT_INTENT_OK "
        f"rows={rows} empty_cells={empty_cells} runtime_rows={runtime_rows} blocked_masks={blocked_rows}"
    )


if __name__ == "__main__":
    main()
