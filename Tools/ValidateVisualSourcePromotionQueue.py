#!/usr/bin/env python3
"""Validate visual source promotion queue proof gates for HECTON-8."""

from __future__ import annotations

import csv
import re
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
QUEUE_PATH = ROOT / "Docs/AssetAudit/VISUAL_SOURCE_PROMOTION_EXECUTION_QUEUE_20260605.csv"
COMPANION_PATH = ROOT / "Docs/AssetAudit/VISUAL_SOURCE_PROMOTION_EXECUTION_QUEUE_20260605.md"
VHSC_PATH = ROOT / "Docs/AssetAudit/VISUAL_HERO_SOURCE_COVERAGE_MATRIX_20260605.csv"
VREF_OWNER_PATH = ROOT / "Docs/AssetAudit/VISUAL_REFERENCE_OWNER_REQUIREMENT_MATRIX_20260605.csv"
OWNER_INDEX_PATH = ROOT / "Docs/AssetAudit/ASSET_OWNER_PACKET_INDEX_20260605.csv"
FOAM_QUEUE_PATH = ROOT / "Docs/AssetAudit/FOAM_CONTACT_SOURCE_ROLE_DECISION_QUEUE_20260605.csv"
BATCH31_QUEUE_PATH = ROOT / "Docs/AssetAudit/BATCH31_CHANNEL_SEMANTICS_DECISION_QUEUE_20260605.csv"

OWNER_PATTERN = re.compile(r"ASSET_OWNER_(\d{2})")
VREF_PATTERN = re.compile(r"VREF-\d{2}")
VHSC_PATTERN = re.compile(r"VHSC-\d{2}")

REQUIRED_COLUMNS = (
    "QueueId",
    "Priority",
    "RouteMoment",
    "VrefScope",
    "SourceCandidates",
    "SourceStatus",
    "BlockingGap",
    "OwnerRoute",
    "NextAction",
    "RequiredProof",
    "RejectIf",
    "LowConsequence",
    "MiddleConsequence",
    "HighUltraConsequence",
    "Status",
)

EXPECTED_IDS = (
    "VSPQ-01",
    "VSPQ-02",
    "VSPQ-03",
    "VSPQ-04",
    "VSPQ-05",
    "VSPQ-06",
    "VSPQ-07",
    "VSPQ-08",
    "VSPQ-09",
    "VSPQ-10",
)

EXPECTED_STATUSES = {
    "VSPQ-01": "PENDING_UNITY_SLOT_AND_VISUAL_PROOF",
    "VSPQ-02": "PENDING_CONTACT_CHANNEL_AND_CREST_PROOF",
    "VSPQ-03": "PENDING_CHANNEL_AND_TERRAIN_RECEIVER_PROOF",
    "VSPQ-04": "PENDING_PROXY_PURGE_AND_FINAL_MATERIAL_PROOF",
    "VSPQ-05": "PENDING_FLORA_CORAL_MATERIAL_AND_LOD_PROOF",
    "VSPQ-06": "PENDING_ATLAS_HUD_AND_PRODUCT_FACE_PROOF",
    "VSPQ-07": "PENDING_DEEP_ROUTE_VFX_AND_TELEMETRY_PROOF",
    "VSPQ-08": "PENDING_PRODUCT_FACE_MESH_MATERIAL_ROUTE_PROOF",
    "VSPQ-09": "PENDING_WATER_CEILING_AND_RECEIVER_PROOF",
    "VSPQ-10": "SOURCE_ONLY_BOUNDARY_ENFORCED",
}

EXPECTED_VHSC_ROWS = {
    "VSPQ-01": "VHSC-01",
    "VSPQ-02": "VHSC-02",
    "VSPQ-03": "VHSC-03",
    "VSPQ-07": "VHSC-06",
    "VSPQ-08": "VHSC-07",
    "VSPQ-09": "VHSC-08",
}

P0_IDS = {"VSPQ-01", "VSPQ-02", "VSPQ-03", "VSPQ-04"}
P1_IDS = {"VSPQ-05", "VSPQ-06", "VSPQ-07", "VSPQ-08", "VSPQ-09"}
P2_IDS = {"VSPQ-10"}
OCEAN_CONTACT_P0_IDS = {"VSPQ-01", "VSPQ-02", "VSPQ-03"}


@dataclass(frozen=True)
class PromotionRow:
    queue_id: str
    priority: str
    route_moment: str
    vref_scope: str
    source_candidates: str
    source_status: str
    blocking_gap: str
    owner_route: str
    next_action: str
    required_proof: str
    reject_if: str
    low_consequence: str
    middle_consequence: str
    high_ultra_consequence: str
    status: str


def display_path(path: Path) -> str:
    try:
        return str(path.relative_to(ROOT))
    except ValueError:
        return str(path)


def load_text(path: Path) -> str:
    if not path.exists():
        raise SystemExit(f"FAIL: missing file: {display_path(path)}")
    return path.read_text(encoding="utf-8")


def load_csv_rows(path: Path) -> list[dict[str, str]]:
    if not path.exists():
        raise SystemExit(f"FAIL: missing CSV: {display_path(path)}")
    with path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        if reader.fieldnames is None:
            raise SystemExit(f"FAIL: CSV has no header: {display_path(path)}")
        return list(reader)


def load_promotions(path: Path = QUEUE_PATH) -> list[PromotionRow]:
    if not path.exists():
        raise SystemExit(f"FAIL: missing queue: {display_path(path)}")
    with path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        missing_columns = [column for column in REQUIRED_COLUMNS if column not in (reader.fieldnames or ())]
        if missing_columns:
            raise SystemExit(f"FAIL: missing queue column(s): {', '.join(missing_columns)}")

        rows: list[PromotionRow] = []
        for row_number, row in enumerate(reader, start=2):
            for column in REQUIRED_COLUMNS:
                if not (row.get(column) or "").strip():
                    raise SystemExit(f"FAIL: empty {column} at row {row_number}")
            rows.append(
                PromotionRow(
                    queue_id=row["QueueId"],
                    priority=row["Priority"],
                    route_moment=row["RouteMoment"],
                    vref_scope=row["VrefScope"],
                    source_candidates=row["SourceCandidates"],
                    source_status=row["SourceStatus"],
                    blocking_gap=row["BlockingGap"],
                    owner_route=row["OwnerRoute"],
                    next_action=row["NextAction"],
                    required_proof=row["RequiredProof"],
                    reject_if=row["RejectIf"],
                    low_consequence=row["LowConsequence"],
                    middle_consequence=row["MiddleConsequence"],
                    high_ultra_consequence=row["HighUltraConsequence"],
                    status=row["Status"],
                )
            )
    return rows


def row_ids(rows: list[dict[str, str]], column: str) -> set[str]:
    ids: set[str] = set()
    for row in rows:
        value = row.get(column, "").strip()
        if value:
            ids.add(value)
    return ids


def load_owner_ids(path: Path = OWNER_INDEX_PATH) -> set[str]:
    ids: set[str] = set()
    for row in load_csv_rows(path):
        owner_id = row.get("OwnerId", "").strip()
        if owner_id:
            ids.add(f"ASSET_OWNER_{int(owner_id):02d}")
    return ids


def require_contains(row: PromotionRow, field_name: str, needle: str) -> None:
    value = getattr(row, field_name)
    if needle.lower() not in value.lower():
        raise SystemExit(f"FAIL: {row.queue_id} {field_name} missing required text: {needle}")


def validate_id_priority_status(rows: list[PromotionRow]) -> None:
    ids = [row.queue_id for row in rows]
    if ids != list(EXPECTED_IDS):
        raise SystemExit(f"FAIL: unexpected VSPQ id order: {', '.join(ids)}")

    for row in rows:
        expected_status = EXPECTED_STATUSES[row.queue_id]
        if row.status != expected_status:
            raise SystemExit(f"FAIL: {row.queue_id} status expected {expected_status}, got {row.status}")

        if row.queue_id in P0_IDS and row.priority != "P0":
            raise SystemExit(f"FAIL: {row.queue_id} must remain P0")
        if row.queue_id in P1_IDS and row.priority != "P1":
            raise SystemExit(f"FAIL: {row.queue_id} must remain P1")
        if row.queue_id in P2_IDS and row.priority != "P2":
            raise SystemExit(f"FAIL: {row.queue_id} must remain P2")


def validate_vref_and_vhsc_links(
    rows: list[PromotionRow],
    vref_ids: set[str],
    vhsc_ids: set[str],
) -> None:
    for row in rows:
        if row.vref_scope.startswith("All VREF"):
            if row.queue_id != "VSPQ-10":
                raise SystemExit(f"FAIL: only VSPQ-10 may use all-VREF scope, got {row.queue_id}")
        else:
            scoped_vrefs = set(VREF_PATTERN.findall(row.vref_scope))
            if not scoped_vrefs:
                raise SystemExit(f"FAIL: {row.queue_id} has no VREF ids in scope")
            missing = sorted(scoped_vrefs - vref_ids)
            if missing:
                raise SystemExit(f"FAIL: {row.queue_id} references missing VREF ids: {', '.join(missing)}")

        expected_vhsc = EXPECTED_VHSC_ROWS.get(row.queue_id)
        if expected_vhsc is None:
            continue
        if expected_vhsc not in vhsc_ids:
            raise SystemExit(f"FAIL: expected coverage row missing from VHSC matrix: {expected_vhsc}")
        if expected_vhsc not in row.source_candidates:
            raise SystemExit(f"FAIL: {row.queue_id} source candidates missing {expected_vhsc}")


def validate_owner_routes(rows: list[PromotionRow], known_owner_ids: set[str]) -> None:
    for row in rows:
        owners = {f"ASSET_OWNER_{owner_id}" for owner_id in OWNER_PATTERN.findall(row.owner_route)}
        if not owners:
            raise SystemExit(f"FAIL: {row.queue_id} owner route has no ASSET_OWNER ids")
        missing = sorted(owners - known_owner_ids)
        if missing:
            raise SystemExit(f"FAIL: {row.queue_id} references unknown owner(s): {', '.join(missing)}")

        if "ASSET_OWNER_36" not in owners or "ASSET_OWNER_37" not in owners:
            raise SystemExit(f"FAIL: {row.queue_id} must include h8_1475 proof and anti-false-proof owners")
        if row.queue_id in OCEAN_CONTACT_P0_IDS and "ASSET_OWNER_20" not in owners:
            raise SystemExit(f"FAIL: {row.queue_id} P0 visual route must include ocean/contact owner 20")


def validate_common_proof_gates(rows: list[PromotionRow]) -> None:
    for row in rows:
        proof_text = row.required_proof.lower()
        if "readback" not in proof_text and row.queue_id != "VSPQ-10":
            raise SystemExit(f"FAIL: {row.queue_id} required proof missing readback gate")
        if "screenshot" not in proof_text and row.queue_id != "VSPQ-01":
            raise SystemExit(f"FAIL: {row.queue_id} required proof missing screenshot gate")
        if row.queue_id != "VSPQ-10" and "anti-false-proof gate" not in proof_text:
            raise SystemExit(f"FAIL: {row.queue_id} required proof missing anti-false-proof gate")
        if "proof" not in row.high_ultra_consequence.lower() and "globalqualityweight" not in row.high_ultra_consequence.lower():
            raise SystemExit(f"FAIL: {row.queue_id} high/ultra consequence missing proof/GQW boundary")

        low_text = row.low_consequence.lower()
        if (
            "flat fallback" in low_text
            and "no flat fallback" not in low_text
            and "without flat fallback" not in low_text
        ) or "dark fallback" in low_text:
            raise SystemExit(f"FAIL: {row.queue_id} low consequence risks cheap visual fallback")


def validate_domain_specific_rows(rows: list[PromotionRow]) -> None:
    by_id = {row.queue_id: row for row in rows}

    foam = by_id["VSPQ-02"]
    require_contains(foam, "source_candidates", "FoamContact source role queue")
    require_contains(foam, "blocking_gap", "Rejected foam")
    require_contains(foam, "required_proof", "channel proof")
    require_contains(foam, "reject_if", "material clone/wrapper")

    terrain = by_id["VSPQ-03"]
    require_contains(terrain, "source_candidates", "Batch31")
    require_contains(terrain, "blocking_gap", "MRAO/ARM")
    require_contains(terrain, "required_proof", "seam/tile proof")
    require_contains(terrain, "reject_if", "below Subnautica floor")

    proxy = by_id["VSPQ-04"]
    require_contains(proxy, "source_status", "STATIC_SOURCE_CONTAMINATION_REACHABLE")
    require_contains(proxy, "reject_if", "Proxy material")
    require_contains(proxy, "required_proof", "LOD/VAT/static fallback")

    ui = by_id["VSPQ-06"]
    require_contains(ui, "required_proof", "0 B/frame UI proof")
    require_contains(ui, "reject_if", "decorative overlay")

    deep = by_id["VSPQ-07"]
    require_contains(deep, "required_proof", "deterministic dump artifact")
    require_contains(deep, "high_ultra_consequence", "GlobalQualityWeight")

    boundary = by_id["VSPQ-10"]
    require_contains(boundary, "source_status", "SOURCE_ONLY_NOT_IMPORT_READY")
    require_contains(boundary, "required_proof", "Addressables ownership")
    require_contains(boundary, "reject_if", "source pack is treated as final route art")


def validate_source_decision_inputs(
    foam_path: Path = FOAM_QUEUE_PATH,
    batch31_path: Path = BATCH31_QUEUE_PATH,
) -> tuple[int, int]:
    foam_rows = load_csv_rows(foam_path)
    batch31_rows = load_csv_rows(batch31_path)

    foam_rejected = [row for row in foam_rows if row.get("Status") == "REJECTED_VISIBLE_SUPPORT"]
    foam_blocked = [row for row in foam_rows if str(row.get("Status", "")).startswith("BLOCKED_CHANNEL")]
    if len(foam_rows) != 8 or len(foam_rejected) != 1 or len(foam_blocked) < 2:
        raise SystemExit("FAIL: foam/contact input queue no longer supports VSPQ-02 rejection gates")

    batch31_blocked = [row for row in batch31_rows if row.get("Status") == "BLOCKED_CHANNEL_SEMANTICS"]
    if len(batch31_rows) != 7 or len(batch31_blocked) != 3:
        raise SystemExit("FAIL: Batch31 input queue no longer supports VSPQ-03 channel gates")

    return len(foam_rows), len(batch31_rows)


def validate_companion_doc(path: Path = COMPANION_PATH) -> None:
    text = load_text(path)
    for term in (
        "Generated and cleanup source packs are source-only",
        "Reject any screenshot using diagnostic/editor-mutating",
        "Reject source-pack/contact-sheet promotion without Unity import role",
        "Reject Crest material clones",
        "Final status: `PENDING_VERIFICATION`",
    ):
        if term not in text:
            raise SystemExit(f"FAIL: companion doc missing hard gate: {term}")


def validate_visual_source_promotion_queue(
    queue_path: Path = QUEUE_PATH,
    vhsc_path: Path = VHSC_PATH,
    vref_owner_path: Path = VREF_OWNER_PATH,
    owner_index_path: Path = OWNER_INDEX_PATH,
) -> list[PromotionRow]:
    rows = load_promotions(queue_path)
    validate_id_priority_status(rows)

    vref_ids = row_ids(load_csv_rows(vref_owner_path), "VrefId")
    vhsc_ids = row_ids(load_csv_rows(vhsc_path), "MatrixId")
    known_owner_ids = load_owner_ids(owner_index_path)

    validate_vref_and_vhsc_links(rows, vref_ids, vhsc_ids)
    validate_owner_routes(rows, known_owner_ids)
    validate_common_proof_gates(rows)
    validate_domain_specific_rows(rows)
    validate_source_decision_inputs()
    validate_companion_doc()
    return rows


def main() -> None:
    rows = validate_visual_source_promotion_queue()
    p0_count = sum(1 for row in rows if row.priority == "P0")
    p1_count = sum(1 for row in rows if row.priority == "P1")
    p2_count = sum(1 for row in rows if row.priority == "P2")
    print(
        "VISUAL_SOURCE_PROMOTION_QUEUE_OK "
        f"rows={len(rows)} p0={p0_count} p1={p1_count} p2={p2_count} "
        f"vhsc_links={len(EXPECTED_VHSC_ROWS)}"
    )


if __name__ == "__main__":
    main()
