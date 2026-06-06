#!/usr/bin/env python3
"""Validate Batch31 local PBR promotion-prep artifacts."""

from __future__ import annotations

import csv
import hashlib
import json
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
PROMO_ROOT = ROOT / "Docs/GeneratedAssets/Batch31_LocalPBR/PromotionPrep_20260605"
INDEX_JSON_PATH = PROMO_ROOT / "Batch31_PromotionPrep_INDEX.json"
STATIC_QA_PATH = PROMO_ROOT / "Batch31_PromotionPrep_static_QA.json"
DECISION_QUEUE_PATH = ROOT / "Docs/AssetAudit/BATCH31_CHANNEL_SEMANTICS_DECISION_QUEUE_20260605.csv"

EXPECTED_PACKAGES = (
    "TX_B31_WetBasaltShoreline_1429",
    "TX_B31_PhoticSeabedSubstrate_2102",
    "TX_B31_PhoticShellSandSubstrate_2102",
)
EXPECTED_GENERATED_KEYS = (
    "albedo",
    "normal",
    "mrao_candidate",
    "channel_debug",
    "albedo_2x2",
    "normal_2x2",
    "mrao_2x2",
)


def display_path(path: Path) -> str:
    try:
        return str(path.relative_to(ROOT))
    except ValueError:
        return str(path)


def load_json(path: Path) -> dict[str, Any]:
    if not path.exists():
        raise SystemExit(f"FAIL: missing JSON: {display_path(path)}")
    with path.open("r", encoding="utf-8") as handle:
        data = json.load(handle)
    if not isinstance(data, dict):
        raise SystemExit(f"FAIL: JSON root must be object: {display_path(path)}")
    return data


def load_decision_rows(path: Path = DECISION_QUEUE_PATH) -> list[dict[str, str]]:
    if not path.exists():
        raise SystemExit(f"FAIL: missing Batch31 decision queue: {display_path(path)}")
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def require_bool(data: dict[str, Any], key: str, expected: bool, label: str) -> None:
    actual = data.get(key)
    if actual is not expected:
        raise SystemExit(f"FAIL: {label} {key} expected {expected}, actual {actual}")


def validate_index_json(data: dict[str, Any], root: Path = ROOT) -> int:
    if data.get("evidence_class") != "STATIC_IMAGE_PREP_ONLY":
        raise SystemExit("FAIL: Batch31 index evidence_class must remain STATIC_IMAGE_PREP_ONLY")
    require_bool(data, "not_unity_imported", True, "Batch31 index")
    require_bool(data, "not_visual_acceptance", True, "Batch31 index")

    contact_sheet = root / str(data.get("contact_sheet", ""))
    if not contact_sheet.exists():
        raise SystemExit(f"FAIL: missing Batch31 contact sheet: {display_path(contact_sheet)}")

    packages = data.get("packages")
    if not isinstance(packages, list) or len(packages) != len(EXPECTED_PACKAGES):
        raise SystemExit("FAIL: Batch31 index must list exactly three packages")

    package_ids = tuple(str(package.get("id", "")) for package in packages)
    if package_ids != EXPECTED_PACKAGES:
        raise SystemExit(f"FAIL: unexpected Batch31 package order: {package_ids}")

    checked_files = 1
    for package in packages:
        package_id = str(package["id"])
        if package.get("semantic_status") != "BLOCKED_CHANNEL_SEMANTICS":
            raise SystemExit(f"FAIL: {package_id} semantic_status must remain BLOCKED_CHANNEL_SEMANTICS")
        if package.get("promotion_ready") is not False:
            raise SystemExit(f"FAIL: {package_id} promotion_ready must remain false")

        blocker = str(package.get("channel_semantics_blocker", "")).lower()
        if "shader" not in blocker or "material" not in blocker:
            raise SystemExit(f"FAIL: {package_id} blocker must name shader/material target proof")

        manifest_json = root / str(package.get("manifest_json", ""))
        manifest_md = root / str(package.get("manifest_md", ""))
        for manifest_path in (manifest_json, manifest_md):
            if not manifest_path.exists():
                raise SystemExit(f"FAIL: {package_id} missing manifest: {display_path(manifest_path)}")
            checked_files += 1

        generated = package.get("generated_files")
        hashes = package.get("sha256")
        if not isinstance(generated, dict) or not isinstance(hashes, dict):
            raise SystemExit(f"FAIL: {package_id} generated_files and sha256 must be objects")

        for key in EXPECTED_GENERATED_KEYS:
            relative_path = generated.get(key)
            expected_hash = hashes.get(key)
            if not isinstance(relative_path, str) or not isinstance(expected_hash, str):
                raise SystemExit(f"FAIL: {package_id} missing generated path/hash for {key}")
            file_path = root / relative_path
            if not file_path.exists():
                raise SystemExit(f"FAIL: {package_id} missing generated file: {relative_path}")
            actual_hash = sha256_file(file_path)
            if actual_hash.lower() != expected_hash.lower():
                raise SystemExit(f"FAIL: {package_id} sha256 mismatch for {key}")
            checked_files += 1
    return checked_files


def validate_static_qa(data: dict[str, Any], root: Path = ROOT) -> None:
    if data.get("evidence_class") != "STATIC_IMAGE_PREP_ONLY":
        raise SystemExit("FAIL: Batch31 static QA evidence_class must remain STATIC_IMAGE_PREP_ONLY")
    require_bool(data, "not_unity_imported", True, "Batch31 static QA")
    require_bool(data, "not_visual_acceptance", True, "Batch31 static QA")
    if data.get("semantic_status") != "BLOCKED_CHANNEL_SEMANTICS":
        raise SystemExit("FAIL: Batch31 static QA semantic_status must remain BLOCKED_CHANNEL_SEMANTICS")
    require_bool(data, "promotion_ready", False, "Batch31 static QA")

    checks = data.get("checks")
    if not isinstance(checks, dict):
        raise SystemExit("FAIL: Batch31 static QA checks must be object")
    require_bool(checks, "dimensions_match_1024x1024", True, "Batch31 static QA checks")
    require_bool(checks, "canonical_mrao_layout_valid", False, "Batch31 static QA checks")
    require_bool(checks, "channel_semantics_blocked", True, "Batch31 static QA checks")
    require_bool(checks, "unity_import_safe_to_claim", False, "Batch31 static QA checks")

    file_list = data.get("file_list")
    if not isinstance(file_list, list) or len(file_list) < 30:
        raise SystemExit("FAIL: Batch31 static QA file_list must cover generated package files")
    for relative_path in file_list:
        file_path = root / str(relative_path)
        if not file_path.exists():
            raise SystemExit(f"FAIL: Batch31 static QA missing listed file: {relative_path}")

    packages = tuple(data.get("packages_validated", ()))
    if packages != EXPECTED_PACKAGES:
        raise SystemExit(f"FAIL: Batch31 static QA packages mismatch: {packages}")


def validate_decision_queue(rows: list[dict[str, str]]) -> int:
    if len(rows) != 7:
        raise SystemExit(f"FAIL: expected 7 Batch31 decision rows, got {len(rows)}")
    blocked = 0
    static_root_rows = 0
    for row in rows:
        decision_id = row.get("DecisionId", "").strip()
        status = row.get("Status", "").strip()
        package = row.get("Package", "").strip()
        artifact_set = row.get("ArtifactSet", "").strip()
        if not decision_id:
            raise SystemExit("FAIL: malformed Batch31 decision row")
        if decision_id == "B31DEC-07":
            if package != "PromotionPrep_20260605 root and manifests":
                raise SystemExit("FAIL: B31DEC-07 must remain the PromotionPrep root/manifests boundary row")
            if status != "STATIC_SOURCE_ONLY":
                raise SystemExit("FAIL: B31DEC-07 must remain STATIC_SOURCE_ONLY")
            if "Contact sheets" not in artifact_set:
                raise SystemExit("FAIL: B31DEC-07 must remain the contact-sheet/static-QA boundary row")
            static_root_rows += 1
            continue
        if package not in EXPECTED_PACKAGES:
            raise SystemExit("FAIL: malformed Batch31 decision row")
        if artifact_set == "MRAO Candidate":
            if status != "BLOCKED_CHANNEL_SEMANTICS":
                raise SystemExit(f"FAIL: {decision_id} MRAO row must remain BLOCKED_CHANNEL_SEMANTICS")
            blocked += 1
    if blocked != 3:
        raise SystemExit(f"FAIL: expected 3 blocked Batch31 MRAO rows, got {blocked}")
    if static_root_rows != 1:
        raise SystemExit(f"FAIL: expected one Batch31 static root boundary row, got {static_root_rows}")
    return blocked


def validate_batch31_promotion_prep_artifacts() -> tuple[int, int]:
    checked_files = validate_index_json(load_json(INDEX_JSON_PATH))
    validate_static_qa(load_json(STATIC_QA_PATH))
    blocked = validate_decision_queue(load_decision_rows())
    return checked_files, blocked


def main() -> None:
    checked_files, blocked = validate_batch31_promotion_prep_artifacts()
    print(f"BATCH31_PROMOTION_PREP_ARTIFACTS_OK packages=3 files={checked_files} blocked_masks={blocked}")


if __name__ == "__main__":
    main()
