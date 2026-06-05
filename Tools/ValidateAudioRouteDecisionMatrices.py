#!/usr/bin/env python3
"""Validate static audio route, mix-priority, and critical-cue matrices."""

from __future__ import annotations

import csv
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]

ROUTE_MATRIX = ROOT / "Docs/AssetAudit/AUDIO_ROUTE_OWNER_REQUIREMENT_MATRIX_20260605.csv"
MIX_QUEUE = ROOT / "Docs/AssetAudit/AUDIO_MIX_PRIORITY_DECISION_QUEUE_20260605.csv"
CUE_MATRIX = ROOT / "Docs/AssetAudit/AUDIO_CRITICAL_CUE_COVERAGE_MATRIX_20260605.csv"

ROUTE_DOC = ROOT / "Docs/AssetAudit/AUDIO_ROUTE_OWNER_REQUIREMENT_MATRIX_20260605.md"
MIX_DOC = ROOT / "Docs/AssetAudit/AUDIO_MIX_PRIORITY_DECISION_QUEUE_20260605.md"
CUE_DOC = ROOT / "Docs/AssetAudit/AUDIO_CRITICAL_CUE_COVERAGE_MATRIX_20260605.md"

FORBIDDEN_STATUS_TERMS = ("ACCEPTED", "COMPLETE", "READY", "PASS", "GREEN")
PROOF_TERMS = ("proof", "readback", "runtime", "listening", "capture", "memory", "0 B/frame", "ducking")


@dataclass(frozen=True)
class MatrixSpec:
    name: str
    path: Path
    companion: Path
    id_column: str
    owner_column: str
    proof_column: str
    required_columns: tuple[str, ...]
    expected_ids: tuple[str, ...]
    expected_p0_count: int
    companion_terms: tuple[str, ...]


ROUTE_SPEC = MatrixSpec(
    name="audio_route_owner_requirement_matrix",
    path=ROUTE_MATRIX,
    companion=ROUTE_DOC,
    id_column="RouteId",
    owner_column="PrimaryOwnerPackets",
    proof_column="RequiredRuntimeProof",
    required_columns=(
        "RouteId",
        "Priority",
        "Target",
        "AssetOrConfig",
        "PrimaryOwnerPackets",
        "SourceEvidence",
        "RequiredRuntimeProof",
        "RejectIf",
        "LowConsequence",
        "MiddleConsequence",
        "HighConsequence",
        "UltraConsequence",
        "Status",
    ),
    expected_ids=tuple(f"AUDROUTE-{index:02d}" for index in range(1, 14)),
    expected_p0_count=5,
    companion_terms=(
        "Runtime proof: absent.",
        "Unity proof: absent.",
        "Static CSVs are not proof.",
    ),
)

MIX_SPEC = MatrixSpec(
    name="audio_mix_priority_decision_queue",
    path=MIX_QUEUE,
    companion=MIX_DOC,
    id_column="DecisionId",
    owner_column="OwnerRoute",
    proof_column="RequiredProof",
    required_columns=(
        "DecisionId",
        "Priority",
        "CueLayer",
        "RepresentativeSources",
        "MixPosition",
        "OwnerRoute",
        "RequiredProof",
        "RejectIf",
        "LowConsequence",
        "MiddleConsequence",
        "HighConsequence",
        "UltraConsequence",
        "Status",
    ),
    expected_ids=tuple(f"AUDMIX-{index:02d}" for index in range(1, 11)),
    expected_p0_count=5,
    companion_terms=(
        "Runtime mix proof: absent.",
        "Listening pass: absent.",
        "This is not a mix result.",
    ),
)

CUE_SPEC = MatrixSpec(
    name="audio_critical_cue_coverage_matrix",
    path=CUE_MATRIX,
    companion=CUE_DOC,
    id_column="CoverageId",
    owner_column="OwnerRoute",
    proof_column="RequiredProof",
    required_columns=(
        "CoverageId",
        "Priority",
        "CueFamily",
        "PlayerDecisionRole",
        "StaticSourceEvidence",
        "CandidateSources",
        "MissingOrBlocked",
        "OwnerRoute",
        "RequiredProof",
        "RejectIf",
        "LowConsequence",
        "MiddleConsequence",
        "HighConsequence",
        "UltraConsequence",
        "Status",
    ),
    expected_ids=tuple(f"AUDCUE-{index:02d}" for index in range(1, 13)),
    expected_p0_count=8,
    companion_terms=(
        "Runtime mix proof: absent.",
        "Unity import/readback: absent.",
        "Music beds, pressure hums, and stingers are not warning-cue coverage.",
    ),
)

ALL_SPECS = (ROUTE_SPEC, MIX_SPEC, CUE_SPEC)


def display_path(path: Path) -> str:
    try:
        return str(path.relative_to(ROOT))
    except ValueError:
        return str(path)


def load_rows(spec: MatrixSpec) -> list[dict[str, str]]:
    if not spec.path.exists():
        raise SystemExit(f"FAIL: missing matrix: {display_path(spec.path)}")

    with spec.path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        fieldnames = tuple(reader.fieldnames or ())
        missing = [column for column in spec.required_columns if column not in fieldnames]
        if missing:
            raise SystemExit(f"FAIL: {spec.name} missing column(s): {', '.join(missing)}")

        rows: list[dict[str, str]] = []
        for row_number, row in enumerate(reader, start=2):
            for column in spec.required_columns:
                if not (row.get(column) or "").strip():
                    raise SystemExit(f"FAIL: {spec.name} empty {column} at row {row_number}")
            rows.append({column: row[column].strip() for column in spec.required_columns})

    return rows


def require_contains(matrix: str, row_id: str, field_name: str, value: str, terms: tuple[str, ...]) -> None:
    lowered = value.lower()
    if not any(term.lower() in lowered for term in terms):
        raise SystemExit(f"FAIL: {matrix} {row_id} {field_name} missing one of: {', '.join(terms)}")


def validate_status(matrix: str, row_id: str, status: str) -> None:
    upper = status.upper()
    if any(term in upper for term in FORBIDDEN_STATUS_TERMS):
        raise SystemExit(f"FAIL: {matrix} {row_id} has proof-looking status: {status}")
    if not (upper.startswith("PENDING") or upper.startswith("MISSING") or upper.startswith("PLACEHOLDER")):
        raise SystemExit(f"FAIL: {matrix} {row_id} unsupported status boundary: {status}")


def validate_rows(spec: MatrixSpec, rows: list[dict[str, str]]) -> None:
    ids = [row[spec.id_column] for row in rows]
    if ids != list(spec.expected_ids):
        raise SystemExit(f"FAIL: {spec.name} unexpected id order: {', '.join(ids)}")

    p0_count = sum(1 for row in rows if row["Priority"] == "P0")
    if p0_count != spec.expected_p0_count:
        raise SystemExit(f"FAIL: {spec.name} expected P0 count {spec.expected_p0_count}, got {p0_count}")

    for row in rows:
        row_id = row[spec.id_column]
        priority = row["Priority"]
        owner_route = row[spec.owner_column]
        proof = row[spec.proof_column]
        reject_if = row["RejectIf"]

        validate_status(spec.name, row_id, row["Status"])
        require_contains(spec.name, row_id, spec.owner_column, owner_route, ("ASSET_OWNER", "SHINOBU_"))
        require_contains(spec.name, row_id, spec.proof_column, proof, PROOF_TERMS)
        require_contains(
            spec.name,
            row_id,
            "RejectIf",
            reject_if,
            (
                "mask",
                "absent",
                "unmanaged",
                "alloc",
                "missing",
                "too loud",
                "placeholder",
                "generic",
                "without",
                "treated",
                "unclear",
                "poor",
                "bypass",
                "repeat",
                "always-on",
                "wrong",
                "inaudible",
                "direct",
                "accepted",
                "silent",
                "faked",
                "borrowed",
                "badly",
                "constant",
                "fires",
            ),
        )

        if priority == "P0":
            require_contains(spec.name, row_id, spec.owner_column, owner_route, ("ASSET_OWNER",))
            require_contains(
                spec.name,
                row_id,
                spec.proof_column,
                proof,
                ("runtime", "readback", "proof", "0 B/frame", "capture", "listening", "owner", "mapping", "cooldown", "import"),
            )


def validate_companion(spec: MatrixSpec) -> None:
    if not spec.companion.exists():
        raise SystemExit(f"FAIL: missing companion doc: {display_path(spec.companion)}")
    text = spec.companion.read_text(encoding="utf-8-sig")
    for term in spec.companion_terms:
        if term not in text:
            raise SystemExit(f"FAIL: {spec.name} companion missing term: {term}")


def validate_spec(spec: MatrixSpec) -> list[dict[str, str]]:
    rows = load_rows(spec)
    validate_rows(spec, rows)
    validate_companion(spec)
    return rows


def main() -> None:
    totals: list[str] = []
    for spec in ALL_SPECS:
        rows = validate_spec(spec)
        p0_count = sum(1 for row in rows if row["Priority"] == "P0")
        totals.append(f"{spec.name}={len(rows)}:p0={p0_count}")
    print("AUDIO_ROUTE_DECISION_MATRICES_OK " + " ".join(totals))


if __name__ == "__main__":
    main()
