#!/usr/bin/env python3
"""Validate static audio listening and import-policy queues."""

from __future__ import annotations

import csv
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]

LISTENING_QUEUE = ROOT / "Docs/AssetAudit/AUDIO_LISTENING_PASS_QUEUE_20260605.csv"
IMPORT_POLICY_TABLE = ROOT / "Docs/AssetAudit/AUDIO_IMPORT_POLICY_EXCEPTION_TABLE_20260605.csv"

LISTENING_DOC = ROOT / "Docs/AssetAudit/AUDIO_LISTENING_PASS_QUEUE_20260605.md"
IMPORT_POLICY_DOC = ROOT / "Docs/AssetAudit/AUDIO_IMPORT_POLICY_EXCEPTION_TABLE_20260605.md"

FORBIDDEN_STATUS_TERMS = ("ACCEPTED", "COMPLETE", "READY", "GREEN", "PASS")
ALLOWED_STATUS_PREFIXES = ("PENDING", "PLACEHOLDER")
PROOF_TERMS = ("readback", "runtime", "listening", "proof", "0 B/frame", "memory", "ducking", "latency")
BOUNDARY_TERMS = ("not", "no ", "absent", "unproved", "blocked", "pending")


@dataclass(frozen=True)
class QueueSpec:
    name: str
    path: Path
    companion: Path
    id_column: str
    priority_column: str
    required_columns: tuple[str, ...]
    expected_ids: tuple[str, ...]
    expected_p0_count: int
    expected_row_count: int
    asset_path_columns: tuple[str, ...]
    proof_columns: tuple[str, ...]
    companion_terms: tuple[str, ...]


LISTENING_SPEC = QueueSpec(
    name="audio_listening_pass_queue",
    path=LISTENING_QUEUE,
    companion=LISTENING_DOC,
    id_column="queue_order",
    priority_column="priority",
    required_columns=(
        "queue_order",
        "priority",
        "target",
        "asset_or_config",
        "route_context",
        "why_first",
        "required_runtime_proof",
        "reject_condition",
        "status",
    ),
    expected_ids=tuple(str(index) for index in range(1, 14)),
    expected_p0_count=5,
    expected_row_count=13,
    asset_path_columns=("asset_or_config",),
    proof_columns=("required_runtime_proof",),
    companion_terms=(
        "No Unity import settings",
        "Runtime MusicDirector capture",
        "Runtime 0 B/frame proof",
        "Final status: `PENDING_VERIFICATION`.",
    ),
)

IMPORT_POLICY_SPEC = QueueSpec(
    name="audio_import_policy_exception_table",
    path=IMPORT_POLICY_TABLE,
    companion=IMPORT_POLICY_DOC,
    id_column="audio_class",
    priority_column="priority",
    required_columns=(
        "priority",
        "audio_class",
        "default_policy",
        "exception_policy",
        "owner_route",
        "proof_needed",
        "blockers",
        "disposition",
        "evidence_class",
    ),
    expected_ids=(
        "music_profiles",
        "long_non_critical_ambience",
        "player_loop_breath_suit_swim",
        "short_sfx",
        "ui_feedback",
        "voice_stubs",
        "MusicDirector_mixer_null_blocker",
        "Player_prefab_direct_clip_refs",
    ),
    expected_p0_count=4,
    expected_row_count=8,
    asset_path_columns=(),
    proof_columns=("proof_needed",),
    companion_terms=(
        "This is not stable authority adoption.",
        "Conflict disposition: `PENDING_AUTHORITY_DECISION`.",
        "Generic SFX rows do not stream",
        "Final disposition: `PENDING_AUTHORITY_DECISION`.",
    ),
)

ALL_SPECS = (LISTENING_SPEC, IMPORT_POLICY_SPEC)


def display_path(path: Path) -> str:
    try:
        return str(path.relative_to(ROOT))
    except ValueError:
        return str(path)


def load_text(path: Path) -> str:
    if not path.exists():
        raise SystemExit(f"FAIL: missing file: {display_path(path)}")
    return path.read_text(encoding="utf-8")


def load_rows(spec: QueueSpec) -> list[dict[str, str]]:
    if not spec.path.exists():
        raise SystemExit(f"FAIL: missing queue: {display_path(spec.path)}")

    rows: list[dict[str, str]] = []
    with spec.path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        fieldnames = tuple(reader.fieldnames or ())
        missing = [column for column in spec.required_columns if column not in fieldnames]
        if missing:
            raise SystemExit(f"FAIL: {spec.name} missing column(s): {', '.join(missing)}")

        for row_number, row in enumerate(reader, start=2):
            current: dict[str, str] = {}
            for column in spec.required_columns:
                value = (row.get(column) or "").strip()
                if not value:
                    raise SystemExit(f"FAIL: {spec.name} empty {column} at row {row_number}")
                current[column] = value
            rows.append(current)

    return rows


def validate_status(matrix: str, row_id: str, status: str) -> None:
    upper = status.upper()
    if any(term in upper for term in FORBIDDEN_STATUS_TERMS) and not upper.startswith("PENDING"):
        raise SystemExit(f"FAIL: {matrix} {row_id} has proof-looking status: {status}")
    if not upper.startswith(ALLOWED_STATUS_PREFIXES):
        raise SystemExit(f"FAIL: {matrix} {row_id} unsupported status boundary: {status}")


def require_any(matrix: str, row_id: str, field_name: str, value: str, terms: tuple[str, ...]) -> None:
    lowered = value.lower()
    if not any(term.lower() in lowered for term in terms):
        raise SystemExit(f"FAIL: {matrix} {row_id} {field_name} missing one of: {', '.join(terms)}")


def validate_existing_paths(spec: QueueSpec, row: dict[str, str]) -> None:
    row_id = row[spec.id_column]
    for column in spec.asset_path_columns:
        for raw_part in row[column].split(";"):
            value = raw_part.strip()
            if not value.startswith(("Assets/", "Docs/")):
                continue
            path = ROOT / value
            if not path.exists():
                raise SystemExit(f"FAIL: {spec.name} {row_id} missing referenced path: {value}")


def validate_rows(spec: QueueSpec, rows: list[dict[str, str]]) -> None:
    if len(rows) != spec.expected_row_count:
        raise SystemExit(f"FAIL: {spec.name} expected {spec.expected_row_count} rows, got {len(rows)}")

    ids = [row[spec.id_column] for row in rows]
    if ids != list(spec.expected_ids):
        raise SystemExit(f"FAIL: {spec.name} unexpected id order: {', '.join(ids)}")

    p0_count = sum(1 for row in rows if row[spec.priority_column] == "P0")
    if p0_count != spec.expected_p0_count:
        raise SystemExit(f"FAIL: {spec.name} expected P0 count {spec.expected_p0_count}, got {p0_count}")

    for row in rows:
        row_id = row[spec.id_column]
        priority = row[spec.priority_column]
        if priority not in {"P0", "P1", "P2"}:
            raise SystemExit(f"FAIL: {spec.name} {row_id} invalid priority: {priority}")

        status = row.get("status") or row.get("disposition") or ""
        validate_status(spec.name, row_id, status)
        validate_existing_paths(spec, row)

        for proof_column in spec.proof_columns:
            require_any(spec.name, row_id, proof_column, row[proof_column], PROOF_TERMS)

        boundary_text = " ".join(row.values())
        require_any(spec.name, row_id, "boundary_text", boundary_text, BOUNDARY_TERMS)


def validate_companion_doc(spec: QueueSpec) -> None:
    text = load_text(spec.companion)
    for term in spec.companion_terms:
        if term not in text:
            raise SystemExit(f"FAIL: {spec.name} companion doc missing term: {term}")


def validate_spec(spec: QueueSpec) -> list[dict[str, str]]:
    rows = load_rows(spec)
    validate_rows(spec, rows)
    validate_companion_doc(spec)
    return rows


def validate_audio_listening_policy_queues() -> dict[str, list[dict[str, str]]]:
    return {spec.name: validate_spec(spec) for spec in ALL_SPECS}


def main() -> None:
    results = validate_audio_listening_policy_queues()
    listening = results[LISTENING_SPEC.name]
    policy = results[IMPORT_POLICY_SPEC.name]
    listening_p0 = sum(1 for row in listening if row["priority"] == "P0")
    policy_p0 = sum(1 for row in policy if row["priority"] == "P0")
    print(
        "AUDIO_LISTENING_POLICY_QUEUES_OK "
        f"listening={len(listening)}:p0={listening_p0} "
        f"import_policy={len(policy)}:p0={policy_p0}"
    )


if __name__ == "__main__":
    main()
