#!/usr/bin/env python3
"""Validate the root asset action queue."""

from __future__ import annotations

import csv
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
QUEUE_PATH = ROOT / "Docs/AssetAudit/ASSET_ACTION_QUEUE_20260605.csv"
COMPANION_PATH = ROOT / "Docs/AssetAudit/ASSET_ACTION_QUEUE_20260605.md"
TASKLOCAL_ROOT = ROOT / "taskslocal/asset_system_20260605"

REQUIRED_COLUMNS = (
    "priority",
    "domain",
    "defect",
    "evidence",
    "affected_paths",
    "owner_packet",
    "required_action",
    "acceptance_proof",
    "status",
)
EXPECTED_DOMAINS = (
    "water_visual",
    "flora_materials",
    "audio_routing",
    "audio_lifecycle",
    "sky_aegir",
    "sky_slots",
    "terrain_pbr",
    "ui_sprite",
    "music_cadence",
    "texture_import",
    "scene_flow_context",
)
EXPECTED_PRIORITY_COUNTS = {"P0": 4, "P1": 5, "P2": 2}
PROOF_TERMS = ("Unity", "readback", "runtime", "screenshot", "Frame Debugger", "0 B/frame", "Console", "proof")
ACTION_TERMS = ("Read", "Author", "Classify", "Assign", "Use", "Listening", "Audit", "Do not", "Compose")
COMPANION_TERMS = (
    "It is not acceptance proof",
    "does not authorize raw YAML edits",
    "Last process gate before creating this queue",
    "Final status: `PENDING_VERIFICATION`.",
)


@dataclass(frozen=True)
class ActionQueueRow:
    priority: str
    domain: str
    defect: str
    evidence: str
    affected_paths: str
    owner_packet: str
    required_action: str
    acceptance_proof: str
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


def load_rows(path: Path = QUEUE_PATH) -> list[ActionQueueRow]:
    if not path.exists():
        raise SystemExit(f"FAIL: missing queue: {display_path(path)}")

    rows: list[ActionQueueRow] = []
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        fieldnames = tuple(reader.fieldnames or ())
        missing = [column for column in REQUIRED_COLUMNS if column not in fieldnames]
        if missing:
            raise SystemExit(f"FAIL: asset_action_queue missing column(s): {', '.join(missing)}")

        for row_number, row in enumerate(reader, start=2):
            values: dict[str, str] = {}
            for column in REQUIRED_COLUMNS:
                value = (row.get(column) or "").strip()
                if not value:
                    raise SystemExit(f"FAIL: asset_action_queue empty {column} at row {row_number}")
                values[column] = value
            rows.append(ActionQueueRow(**values))
    return rows


def require_any(row: ActionQueueRow, field_name: str, value: str, terms: tuple[str, ...]) -> None:
    lowered = value.lower()
    if not any(term.lower() in lowered for term in terms):
        raise SystemExit(
            f"FAIL: asset_action_queue {row.domain} {field_name} missing one of: {', '.join(terms)}"
        )


def validate_owner_packets(row: ActionQueueRow) -> None:
    for packet in (part.strip() for part in row.owner_packet.split(";")):
        if not packet:
            continue
        path = TASKLOCAL_ROOT / packet
        if not path.exists():
            raise SystemExit(f"FAIL: asset_action_queue {row.domain} missing owner packet: {packet}")


def validate_explicit_paths(row: ActionQueueRow) -> None:
    checked = 0
    for part in (item.strip() for item in row.affected_paths.split(";")):
        if not part.startswith(("Assets/", "Docs/", "ProjectSettings/")):
            continue
        checked += 1
        path = ROOT / part
        if not path.exists():
            raise SystemExit(f"FAIL: asset_action_queue {row.domain} missing affected path: {part}")

    if checked == 0 and row.domain not in {"audio_lifecycle", "music_cadence", "texture_import"}:
        raise SystemExit(f"FAIL: asset_action_queue {row.domain} has no checked affected project path")


def validate_rows(rows: list[ActionQueueRow]) -> None:
    if len(rows) != len(EXPECTED_DOMAINS):
        raise SystemExit(f"FAIL: expected {len(EXPECTED_DOMAINS)} action rows, got {len(rows)}")

    domains = tuple(row.domain for row in rows)
    if domains != EXPECTED_DOMAINS:
        raise SystemExit(f"FAIL: unexpected action queue domain order: {', '.join(domains)}")

    for priority, expected in EXPECTED_PRIORITY_COUNTS.items():
        actual = sum(1 for row in rows if row.priority == priority)
        if actual != expected:
            raise SystemExit(f"FAIL: expected {expected} {priority} action rows, got {actual}")

    for row in rows:
        if row.status != "PENDING_VERIFICATION":
            raise SystemExit(f"FAIL: asset_action_queue {row.domain} status must remain PENDING_VERIFICATION")
        validate_owner_packets(row)
        validate_explicit_paths(row)
        require_any(row, "required_action", row.required_action, ACTION_TERMS)
        require_any(row, "acceptance_proof", row.acceptance_proof, PROOF_TERMS)


def validate_companion_doc(path: Path = COMPANION_PATH) -> None:
    text = load_text(path)
    for term in COMPANION_TERMS:
        if term not in text:
            raise SystemExit(f"FAIL: asset action queue companion doc missing term: {term}")


def validate_asset_action_queue() -> list[ActionQueueRow]:
    rows = load_rows()
    validate_rows(rows)
    validate_companion_doc()
    return rows


def main() -> None:
    rows = validate_asset_action_queue()
    counts = {priority: sum(1 for row in rows if row.priority == priority) for priority in ("P0", "P1", "P2")}
    print(f"ASSET_ACTION_QUEUE_OK rows={len(rows)} p0={counts['P0']} p1={counts['P1']} p2={counts['P2']}")


if __name__ == "__main__":
    main()
