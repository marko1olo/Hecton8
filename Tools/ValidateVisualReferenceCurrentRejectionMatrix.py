#!/usr/bin/env python3
"""Validate visual-reference current rejection matrix stays rejection-only."""

from __future__ import annotations

import csv
import re
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MATRIX_PATH = ROOT / "Docs/Reports/AssetSystem_20260605/VISUAL_REFERENCE_VS_CURRENT_REJECTION_MATRIX_20260605.csv"
COMPANION_PATH = ROOT / "Docs/Reports/AssetSystem_20260605/VISUAL_REFERENCE_VS_CURRENT_REJECTION_MATRIX_20260605.md"
CRITIQUE_PATH = ROOT / "Docs/Reports/AssetSystem_20260605/VISUAL_REFERENCE_CRITIQUE_CHECKLIST_20260605.csv"

REQUIRED_COLUMNS = (
    "Area",
    "ReferenceDemand",
    "CurrentStaticEvidence",
    "CurrentMissingProof",
    "RejectionTrigger",
    "RequiredCapture",
    "Owner",
    "Status",
)

EXPECTED_AREAS = (
    "surface/sky/Aegir/moons/clouds",
    "ocean surface/waterline/foam/refraction",
    "coastline/exposed rock/shore terrain",
    "photic shallows/coral/flora",
    "medium-depth hero route/readability",
    "UI/visor/product face",
    "route landmarks/evidence/industrial traces",
    "runtime proof class h8_1475",
    "latest raw diagnostic surface captures",
    "surface water recovery probe 1914",
)

EXPECTED_STATUSES = {
    "surface/sky/Aegir/moons/clouds": "REJECTED_FOR_PROMOTION / H8_1475_CAPTURE_PENDING",
    "ocean surface/waterline/foam/refraction": "REJECTED_FOR_PROMOTION / CREST_WATER_READBACK_PENDING",
    "coastline/exposed rock/shore terrain": "REJECTED_FOR_PROMOTION / TERRAIN_READBACK_PENDING",
    "photic shallows/coral/flora": "REJECTED_FOR_PROMOTION / PHOTIC_ROUTE_CAPTURE_PENDING",
    "medium-depth hero route/readability": "REJECTED_FOR_PROMOTION / MEDIUM_DEPTH_CAPTURE_PENDING",
    "UI/visor/product face": "REJECTED_FOR_PROMOTION / PRODUCT_FACE_SOURCE_BLOCKED",
    "route landmarks/evidence/industrial traces": "PENDING_VERIFICATION / BASE_PROOF_BLOCKED",
    "runtime proof class h8_1475": "REJECTED / H8_1475_MISSING / PROCESS_GATE_RED",
    "latest raw diagnostic surface captures": "DIAGNOSTIC_REJECTED / NOT_ACCEPTANCE_PROOF",
    "surface water recovery probe 1914": "DIAGNOSTIC_REJECTED / NOT_ACCEPTANCE_PROOF",
}

PROOF_TERMS = ("canonical", "readback", "proof", "capture", "packet")
FORBIDDEN_STATUS_TERMS = ("ACCEPTED", "PROMOTED", "PASSED", "READY", "GREEN")
H8_ARTIFACT_PATTERN = re.compile(r"h8_1475_[A-Za-z0-9_{}]+(?:\.(?:png|md|json|txt|sha256))?")


@dataclass(frozen=True)
class RejectionRow:
    area: str
    reference_demand: str
    current_static_evidence: str
    current_missing_proof: str
    rejection_trigger: str
    required_capture: str
    owner: str
    status: str


def display_path(path: Path) -> str:
    try:
        return str(path.relative_to(ROOT))
    except ValueError:
        return str(path)


def load_csv(path: Path) -> list[dict[str, str]]:
    if not path.exists():
        raise SystemExit(f"FAIL: missing CSV: {display_path(path)}")
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        if reader.fieldnames is None:
            raise SystemExit(f"FAIL: CSV has no header: {display_path(path)}")
        return [{key: (value or "").strip() for key, value in row.items()} for row in reader]


def load_rejection_rows(path: Path = MATRIX_PATH) -> list[RejectionRow]:
    rows = load_csv(path)
    if rows:
        missing = [column for column in REQUIRED_COLUMNS if column not in rows[0]]
        if missing:
            raise SystemExit(f"FAIL: rejection matrix missing column(s): {', '.join(missing)}")

    parsed: list[RejectionRow] = []
    for row_index, row in enumerate(rows, start=2):
        for column in REQUIRED_COLUMNS:
            if not row[column]:
                raise SystemExit(f"FAIL: rejection matrix empty {column} at row {row_index}")
        parsed.append(
            RejectionRow(
                area=row["Area"],
                reference_demand=row["ReferenceDemand"],
                current_static_evidence=row["CurrentStaticEvidence"],
                current_missing_proof=row["CurrentMissingProof"],
                rejection_trigger=row["RejectionTrigger"],
                required_capture=row["RequiredCapture"],
                owner=row["Owner"],
                status=row["Status"],
            )
        )
    return parsed


def load_known_h8_artifacts(path: Path = CRITIQUE_PATH) -> set[str]:
    artifacts: set[str] = set()
    for row in load_csv(path):
        for value in row.values():
            artifacts.update(H8_ARTIFACT_PATTERN.findall(value))
    if not artifacts:
        raise SystemExit(f"FAIL: no h8_1475 artifacts found in critique checklist: {display_path(path)}")
    return artifacts


def validate_rejection_rows(rows: list[RejectionRow], known_h8_artifacts: set[str]) -> None:
    areas = tuple(row.area for row in rows)
    if areas != EXPECTED_AREAS:
        raise SystemExit(f"FAIL: visual rejection matrix area order drift: {', '.join(areas)}")

    for row in rows:
        expected_status = EXPECTED_STATUSES[row.area]
        if row.status != expected_status:
            raise SystemExit(f"FAIL: {row.area} status expected {expected_status}, got {row.status}")

        for term in FORBIDDEN_STATUS_TERMS:
            if term in row.status:
                raise SystemExit(f"FAIL: {row.area} status contains promotion term: {term}")

        if "owner" not in row.owner.lower():
            raise SystemExit(f"FAIL: {row.area} owner route must name owner responsibility")
        if len(row.reference_demand) < 64 or len(row.rejection_trigger) < 64:
            raise SystemExit(f"FAIL: {row.area} reference demand or rejection trigger is too weak")

        missing_lower = row.current_missing_proof.lower()
        if not any(term in missing_lower for term in PROOF_TERMS):
            raise SystemExit(f"FAIL: {row.area} missing-proof field must name proof/readback/capture/packet need")

        if "Docs/Screenshots/MCP" in row.required_capture:
            raise SystemExit(f"FAIL: {row.area} required capture must not route to raw MCP screenshots")
        if "h8_1475" not in row.required_capture:
            raise SystemExit(f"FAIL: {row.area} required capture must name canonical h8_1475 proof")

        row_artifacts = set(H8_ARTIFACT_PATTERN.findall(row.required_capture))
        if row_artifacts and row.area != "runtime proof class h8_1475":
            unknown = sorted(artifact for artifact in row_artifacts if artifact not in known_h8_artifacts)
            if unknown:
                raise SystemExit(f"FAIL: {row.area} required capture references unknown h8_1475 artifact(s): {', '.join(unknown)}")

        if row.area.startswith("latest raw diagnostic") or row.area.startswith("surface water recovery"):
            if not row.status.startswith("DIAGNOSTIC_REJECTED"):
                raise SystemExit(f"FAIL: {row.area} diagnostic row must remain DIAGNOSTIC_REJECTED")
        elif row.area != "route landmarks/evidence/industrial traces" and "REJECTED" not in row.status:
            raise SystemExit(f"FAIL: {row.area} must remain rejected for promotion")


def validate_companion_doc(path: Path = COMPANION_PATH) -> None:
    if not path.exists():
        raise SystemExit(f"FAIL: missing companion doc: {display_path(path)}")
    text = path.read_text(encoding="utf-8")
    required_terms = (
        "No Unity run, build, Play Mode, import, profiler, Frame Debugger",
        "Current screenshots were not used as pass/fail proof.",
        "Raw diagnostic frames can only reject obvious failures.",
        "Final status: `STATIC_DOC_REVIEWED / REJECTED_FOR_PROMOTION / H8_1475_CAPTURE_PENDING`.",
    )
    for term in required_terms:
        if term not in text:
            raise SystemExit(f"FAIL: rejection matrix companion doc missing term: {term}")


def validate_visual_reference_current_rejection_matrix() -> list[RejectionRow]:
    rows = load_rejection_rows()
    known_h8_artifacts = load_known_h8_artifacts()
    validate_rejection_rows(rows, known_h8_artifacts)
    validate_companion_doc()
    return rows


def main() -> None:
    rows = validate_visual_reference_current_rejection_matrix()
    diagnostic_rows = sum(1 for row in rows if row.status.startswith("DIAGNOSTIC_REJECTED"))
    print(f"VISUAL_REFERENCE_CURRENT_REJECTION_MATRIX_OK rows={len(rows)} diagnostic_rejected={diagnostic_rows}")


if __name__ == "__main__":
    main()
