#!/usr/bin/env python3
"""Validate the asset owner packet index for HECTON-8."""

from __future__ import annotations

import csv
import re
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
INDEX_PATH = ROOT / "Docs/AssetAudit/ASSET_OWNER_PACKET_INDEX_20260605.csv"
COMPANION_PATH = ROOT / "Docs/AssetAudit/ASSET_OWNER_PACKET_INDEX_20260605.md"
TASKLOCAL_ROOT = ROOT / "taskslocal/asset_system_20260605"

REQUIRED_COLUMNS = (
    "OwnerId",
    "PacketPresence",
    "PacketFile",
    "Domain",
    "Title",
    "Status",
    "EvidenceClass",
    "LineCount",
    "UnityGateRequired",
    "UsesP0TargetTables",
    "PrimarySourceFiles",
    "AcceptanceBlockedBy",
    "Notes",
)

EXPECTED_OWNER_IDS = tuple(f"{index:02d}" for index in range(1, 38))
OUTPUT_ONLY_IDS = {"29", "30", "31", "32", "33"}
FORBIDDEN_STATUS_TERMS = ("ACCEPTED", "COMPLETE", "READY", "GREEN", "PASS")
OUTPUT_ONLY_PACKET_FILE = "NO_TASKLOCAL_PACKET_FILE_TARGET_TABLE_OUTPUT_ONLY"
OWNER_FILE_PATTERN = re.compile(r"ASSET_OWNER_(\d{2})")


@dataclass(frozen=True)
class OwnerPacketRow:
    owner_id: str
    packet_presence: str
    packet_file: str
    domain: str
    title: str
    status: str
    evidence_class: str
    line_count: int
    unity_gate_required: str
    uses_p0_target_tables: str
    primary_source_files: str
    acceptance_blocked_by: str
    notes: str


def display_path(path: Path) -> str:
    try:
        return str(path.relative_to(ROOT))
    except ValueError:
        return str(path)


def load_text(path: Path) -> str:
    if not path.exists():
        raise SystemExit(f"FAIL: missing file: {display_path(path)}")
    return path.read_text(encoding="utf-8")


def count_lines(path: Path) -> int:
    return len(load_text(path).splitlines())


def load_rows(path: Path = INDEX_PATH) -> list[OwnerPacketRow]:
    if not path.exists():
        raise SystemExit(f"FAIL: missing index: {display_path(path)}")

    rows: list[OwnerPacketRow] = []
    with path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        missing_columns = [column for column in REQUIRED_COLUMNS if column not in (reader.fieldnames or ())]
        if missing_columns:
            raise SystemExit(f"FAIL: missing index column(s): {', '.join(missing_columns)}")

        for row_number, row in enumerate(reader, start=2):
            for column in REQUIRED_COLUMNS:
                if not (row.get(column) or "").strip():
                    raise SystemExit(f"FAIL: empty {column} at row {row_number}")
            rows.append(
                OwnerPacketRow(
                    owner_id=row["OwnerId"],
                    packet_presence=row["PacketPresence"],
                    packet_file=row["PacketFile"],
                    domain=row["Domain"],
                    title=row["Title"],
                    status=row["Status"],
                    evidence_class=row["EvidenceClass"],
                    line_count=int(row["LineCount"]),
                    unity_gate_required=row["UnityGateRequired"],
                    uses_p0_target_tables=row["UsesP0TargetTables"],
                    primary_source_files=row["PrimarySourceFiles"],
                    acceptance_blocked_by=row["AcceptanceBlockedBy"],
                    notes=row["Notes"],
                )
            )
    return rows


def validate_id_sequence(rows: list[OwnerPacketRow]) -> None:
    ids = [row.owner_id for row in rows]
    if ids != list(EXPECTED_OWNER_IDS):
        raise SystemExit(f"FAIL: unexpected owner id sequence: {', '.join(ids)}")


def validate_status_language(row: OwnerPacketRow) -> None:
    status_upper = row.status.upper()
    for term in FORBIDDEN_STATUS_TERMS:
        if term in status_upper and "PENDING" not in status_upper and "NOT READY" not in status_upper:
            raise SystemExit(f"FAIL: {row.owner_id} status risks false completion: {row.status}")

    blocked = row.acceptance_blocked_by.lower()
    for term in ("unity", "runtime", "proof absent"):
        if term not in blocked:
            raise SystemExit(f"FAIL: {row.owner_id} acceptance boundary missing term: {term}")


def validate_present_packet(row: OwnerPacketRow, root: Path = ROOT) -> None:
    if row.packet_presence != "PRESENT":
        raise SystemExit(f"FAIL: {row.owner_id} expected PRESENT, got {row.packet_presence}")
    if not row.packet_file.startswith("taskslocal/asset_system_20260605/ASSET_OWNER_"):
        raise SystemExit(f"FAIL: {row.owner_id} present packet path is not tasklocal owner packet")

    packet_path = root / row.packet_file
    if not packet_path.exists():
        raise SystemExit(f"FAIL: {row.owner_id} packet file missing: {row.packet_file}")

    actual_lines = count_lines(packet_path)
    if actual_lines != row.line_count:
        raise SystemExit(f"FAIL: {row.owner_id} line count expected {row.line_count}, actual {actual_lines}")

    if "Task packet file exists" not in row.notes:
        raise SystemExit(f"FAIL: {row.owner_id} notes must say the packet file exists")
    validate_status_language(row)


def validate_output_only_packet(row: OwnerPacketRow, tasklocal_root: Path = TASKLOCAL_ROOT) -> None:
    if row.packet_presence != "OUTPUT_ONLY_NO_PACKET_FILE":
        raise SystemExit(f"FAIL: {row.owner_id} expected output-only presence, got {row.packet_presence}")
    if row.packet_file != OUTPUT_ONLY_PACKET_FILE:
        raise SystemExit(f"FAIL: {row.owner_id} output-only row has wrong packet file sentinel")
    if row.line_count != 0:
        raise SystemExit(f"FAIL: {row.owner_id} output-only row must have line count 0")
    if "target-table worker output" not in row.domain and "target-table worker output" not in row.title.lower():
        raise SystemExit(f"FAIL: {row.owner_id} output-only row must identify target-table worker output")
    if "Docs/AssetAudit/" not in row.primary_source_files:
        raise SystemExit(f"FAIL: {row.owner_id} output-only row must point at source artifacts")
    if "No packet file exists" not in row.acceptance_blocked_by:
        raise SystemExit(f"FAIL: {row.owner_id} output-only boundary must say no packet file exists")

    pattern = f"ASSET_OWNER_{row.owner_id}_*.md"
    if any(tasklocal_root.glob(pattern)):
        raise SystemExit(f"FAIL: {row.owner_id} has a tasklocal packet file but is marked output-only")


def validate_route_flags(row: OwnerPacketRow) -> None:
    if row.unity_gate_required not in {"YES", "CONDITIONAL"}:
        raise SystemExit(f"FAIL: {row.owner_id} invalid UnityGateRequired: {row.unity_gate_required}")
    if row.uses_p0_target_tables not in {"YES", "NO"}:
        raise SystemExit(f"FAIL: {row.owner_id} invalid UsesP0TargetTables: {row.uses_p0_target_tables}")
    if not row.domain or not row.title:
        raise SystemExit(f"FAIL: {row.owner_id} missing domain/title")


def validate_companion_doc(path: Path = COMPANION_PATH) -> None:
    text = load_text(path)
    for term in (
        "Indexed owner IDs: 37.",
        "Present packet files: 32.",
        "Output-only IDs without tasklocal packet files: 5",
        "Do not infer completion from packet presence.",
        "Final status: `PENDING VERIFICATION`.",
    ):
        if term not in text:
            raise SystemExit(f"FAIL: companion doc missing term: {term}")


def validate_asset_owner_packet_index(
    path: Path = INDEX_PATH,
    root: Path = ROOT,
    tasklocal_root: Path = TASKLOCAL_ROOT,
) -> list[OwnerPacketRow]:
    rows = load_rows(path)
    validate_id_sequence(rows)

    present_count = 0
    output_count = 0
    for row in rows:
        validate_route_flags(row)
        if row.owner_id in OUTPUT_ONLY_IDS:
            validate_output_only_packet(row, tasklocal_root=tasklocal_root)
            output_count += 1
        else:
            validate_present_packet(row, root=root)
            present_count += 1

    if present_count != 32 or output_count != 5:
        raise SystemExit(f"FAIL: expected 32 present and 5 output-only rows, got {present_count}/{output_count}")

    validate_companion_doc()
    return rows


def main() -> None:
    rows = validate_asset_owner_packet_index()
    present_count = sum(1 for row in rows if row.packet_presence == "PRESENT")
    output_count = sum(1 for row in rows if row.packet_presence == "OUTPUT_ONLY_NO_PACKET_FILE")
    print(f"ASSET_OWNER_PACKET_INDEX_OK rows={len(rows)} present={present_count} output_only={output_count}")


if __name__ == "__main__":
    main()
