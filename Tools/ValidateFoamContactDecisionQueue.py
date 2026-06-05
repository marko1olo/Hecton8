#!/usr/bin/env python3
"""Validate static foam/contact source-role decisions for HECTON-8."""

from __future__ import annotations

import csv
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
QUEUE_PATH = ROOT / "Docs/AssetAudit/FOAM_CONTACT_SOURCE_ROLE_DECISION_QUEUE_20260605.csv"
COMPANION_PATH = ROOT / "Docs/AssetAudit/FOAM_CONTACT_SOURCE_ROLE_DECISION_QUEUE_20260605.md"

REQUIRED_COLUMNS = (
    "DecisionId",
    "Priority",
    "SourceArtifact",
    "Role",
    "Decision",
    "OwnerRoute",
    "RequiredBeforeImport",
    "RejectIf",
    "LowConsequence",
    "MiddleConsequence",
    "HighConsequence",
    "UltraConsequence",
    "Status",
)

EXPECTED_IDS = (
    "FOAMDEC-01",
    "FOAMDEC-02",
    "FOAMDEC-03",
    "FOAMDEC-04",
    "FOAMDEC-05",
    "FOAMDEC-06",
    "FOAMDEC-07",
    "FOAMDEC-08",
)

SOURCE_ONLY_STATUSES = {"SOURCE_ONLY_CANDIDATE", "SOURCE_REFERENCE_ONLY"}
BLOCKED_CHANNEL_STATUSES = {"BLOCKED_CHANNEL_SEMANTICS", "BLOCKED_CHANNEL_REWORK"}
OUT_OF_SCOPE_STATUS = "OUT_OF_SCOPE_FOR_FOAM_CONTACT"


@dataclass(frozen=True)
class FoamDecision:
    decision_id: str
    priority: str
    source_artifact: str
    role: str
    decision: str
    owner_route: str
    required_before_import: str
    reject_if: str
    low_consequence: str
    middle_consequence: str
    high_consequence: str
    ultra_consequence: str
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


def load_decisions(path: Path = QUEUE_PATH) -> list[FoamDecision]:
    if not path.exists():
        raise SystemExit(f"FAIL: missing queue: {display_path(path)}")

    with path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        missing_columns = [column for column in REQUIRED_COLUMNS if column not in (reader.fieldnames or ())]
        if missing_columns:
            raise SystemExit(f"FAIL: missing queue column(s): {', '.join(missing_columns)}")

        decisions: list[FoamDecision] = []
        for row_number, row in enumerate(reader, start=2):
            for column in REQUIRED_COLUMNS:
                if not (row.get(column) or "").strip():
                    raise SystemExit(f"FAIL: empty {column} at row {row_number}")
            decisions.append(
                FoamDecision(
                    decision_id=row["DecisionId"],
                    priority=row["Priority"],
                    source_artifact=row["SourceArtifact"],
                    role=row["Role"],
                    decision=row["Decision"],
                    owner_route=row["OwnerRoute"],
                    required_before_import=row["RequiredBeforeImport"],
                    reject_if=row["RejectIf"],
                    low_consequence=row["LowConsequence"],
                    middle_consequence=row["MiddleConsequence"],
                    high_consequence=row["HighConsequence"],
                    ultra_consequence=row["UltraConsequence"],
                    status=row["Status"],
                )
            )

    return decisions


def validate_source_paths(decisions: list[FoamDecision], root: Path = ROOT) -> int:
    checked = 0
    for decision in decisions:
        source = decision.source_artifact
        if not (source.startswith("Assets/") or source.startswith("Docs/")):
            continue

        checked += 1
        if not (root / source).exists():
            raise SystemExit(f"FAIL: missing source artifact for {decision.decision_id}: {source}")
    return checked


def require_contains(row: FoamDecision, field_name: str, needle: str) -> None:
    value = getattr(row, field_name)
    if needle.lower() not in value.lower():
        raise SystemExit(f"FAIL: {row.decision_id} {field_name} missing required text: {needle}")


def validate_legacy_rejection(row: FoamDecision) -> None:
    if row.status != "REJECTED_VISIBLE_SUPPORT":
        raise SystemExit(f"FAIL: FOAMDEC-01 status must reject visible legacy foam, got {row.status}")
    if not row.source_artifact.endswith("foam.png"):
        raise SystemExit("FAIL: FOAMDEC-01 must point at the legacy foam.png source")

    require_contains(row, "decision", "Reject")
    require_contains(row, "required_before_import", "replacement source")
    require_contains(row, "required_before_import", "material proof")
    require_contains(row, "reject_if", "Turquoise")
    require_contains(row, "low_consequence", "Never use")


def validate_source_only(row: FoamDecision) -> None:
    if row.status == "SOURCE_ONLY_CANDIDATE":
        require_contains(row, "decision", "source")
        require_contains(row, "required_before_import", "readback")
        evidence_terms = ("proof", "screenshot", "preview", "review")
        if not any(term in row.required_before_import.lower() for term in evidence_terms):
            raise SystemExit(f"FAIL: {row.decision_id} required_before_import missing evidence term")
        if row.decision_id == "FOAMDEC-02":
            require_contains(row, "required_before_import", "tile proof")
        require_contains(row, "reject_if", "Imported directly")
    if row.status == "SOURCE_REFERENCE_ONLY":
        require_contains(row, "decision", "Reference")
        require_contains(row, "reject_if", "never bind")


def validate_blocked_channel(row: FoamDecision) -> None:
    require_contains(row, "required_before_import", "channel")
    require_contains(row, "required_before_import", "readback")
    require_contains(row, "reject_if", "channel")
    require_contains(row, "reject_if", "false-color")


def validate_out_of_scope(row: FoamDecision) -> None:
    require_contains(row, "decision", "Not a water-contact material candidate")
    require_contains(row, "reject_if", "shoreline")


def validate_companion_doc(path: Path = COMPANION_PATH) -> None:
    text = load_text(path)
    for term in (
        "old turquoise `foam.png` remains rejected",
        "Cleanup albedo and normal are useful source direction only",
        "Cleanup MRAO/RGBA/contact channels remain too harsh",
        "Low/compact: use fewer proven contact layers",
    ):
        if term not in text:
            raise SystemExit(f"FAIL: companion doc missing decision term: {term}")


def validate_queue(path: Path = QUEUE_PATH, root: Path = ROOT) -> list[FoamDecision]:
    decisions = load_decisions(path)
    ids = [decision.decision_id for decision in decisions]
    if ids != list(EXPECTED_IDS):
        raise SystemExit(f"FAIL: unexpected decision id order: {', '.join(ids)}")

    checked_paths = validate_source_paths(decisions, root=root)
    if checked_paths != len(decisions):
        raise SystemExit(f"FAIL: expected all decision source artifacts to be path-checked, got {checked_paths}")

    by_id = {decision.decision_id: decision for decision in decisions}
    validate_legacy_rejection(by_id["FOAMDEC-01"])

    for decision in decisions:
        if decision.priority == "P0":
            require_contains(decision, "owner_route", "ASSET_OWNER")
            if "proof" not in decision.required_before_import.lower() and "prove" not in decision.required_before_import.lower():
                raise SystemExit(f"FAIL: {decision.decision_id} required_before_import missing proof/prove language")

        if decision.status in SOURCE_ONLY_STATUSES:
            validate_source_only(decision)
        elif decision.status in BLOCKED_CHANNEL_STATUSES:
            validate_blocked_channel(decision)
        elif decision.status == OUT_OF_SCOPE_STATUS:
            validate_out_of_scope(decision)
        elif decision.status != "REJECTED_VISIBLE_SUPPORT":
            raise SystemExit(f"FAIL: unsupported status {decision.status} in {decision.decision_id}")

    validate_companion_doc()
    return decisions


def main() -> None:
    decisions = validate_queue()
    p0_count = sum(1 for decision in decisions if decision.priority == "P0")
    statuses = sorted({decision.status for decision in decisions})
    print(
        "FOAM_CONTACT_DECISION_QUEUE_OK "
        f"rows={len(decisions)} p0={p0_count} source_paths={len(decisions)} statuses={','.join(statuses)}"
    )


if __name__ == "__main__":
    main()
