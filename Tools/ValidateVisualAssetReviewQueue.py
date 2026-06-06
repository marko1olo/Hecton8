#!/usr/bin/env python3
"""Validate the static visual asset review queue."""

from __future__ import annotations

import csv
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
QUEUE_PATH = ROOT / "Docs/AssetAudit/VISUAL_ASSET_REVIEW_QUEUE_20260605.csv"
COMPANION_PATH = ROOT / "Docs/AssetAudit/VISUAL_ASSET_REVIEW_QUEUE_20260605.md"

REQUIRED_COLUMNS = (
    "queue_order",
    "priority",
    "target",
    "source_or_asset",
    "route_context",
    "why_first",
    "required_visual_proof",
    "reject_condition",
    "status",
)
EXPECTED_ORDER = tuple(str(index) for index in range(1, 12))
EXPECTED_P0_COUNT = 2
PROOF_TERMS = ("readback", "screenshot", "Frame Debugger", "Stats", "proof", "Unity", "import", "LOD")
REJECT_TERMS = (
    "remain",
    "primitive",
    "unproved",
    "no ",
    "stale",
    "blurry",
    "random",
    "mask",
    "hide",
    "wrong",
    "low-poly",
    "silhouette",
    "clips",
    "inflate",
)
COMPANION_TERMS = (
    "No materials, prefabs, scenes, import settings, or files under `Assets` were changed.",
    "Bright surface/photic screenshots.",
    "Frame Debugger/Stats where visible route rendering changes.",
    "Final status: `PENDING_VERIFICATION`.",
)


@dataclass(frozen=True)
class VisualQueueRow:
    queue_order: str
    priority: str
    target: str
    source_or_asset: str
    route_context: str
    why_first: str
    required_visual_proof: str
    reject_condition: str
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


def load_rows(path: Path = QUEUE_PATH) -> list[VisualQueueRow]:
    if not path.exists():
        raise SystemExit(f"FAIL: missing queue: {display_path(path)}")

    rows: list[VisualQueueRow] = []
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        fieldnames = tuple(reader.fieldnames or ())
        missing = [column for column in REQUIRED_COLUMNS if column not in fieldnames]
        if missing:
            raise SystemExit(f"FAIL: visual_asset_review_queue missing column(s): {', '.join(missing)}")

        for row_number, row in enumerate(reader, start=2):
            values: dict[str, str] = {}
            for column in REQUIRED_COLUMNS:
                value = (row.get(column) or "").strip()
                if not value:
                    raise SystemExit(f"FAIL: visual_asset_review_queue empty {column} at row {row_number}")
                values[column] = value
            rows.append(VisualQueueRow(**values))
    return rows


def require_any(row: VisualQueueRow, field_name: str, value: str, terms: tuple[str, ...]) -> None:
    lowered = value.lower()
    if not any(term.lower() in lowered for term in terms):
        raise SystemExit(
            f"FAIL: visual_asset_review_queue row {row.queue_order} {field_name} "
            f"missing one of: {', '.join(terms)}"
        )


def source_parts(value: str) -> list[str]:
    return [part.strip() for part in value.split(";") if part.strip()]


def validate_source_paths(row: VisualQueueRow, root: Path = ROOT) -> None:
    has_checked_path = False
    for part in source_parts(row.source_or_asset):
        if not part.startswith(("Assets/", "Docs/")):
            continue

        has_checked_path = True
        if "*" in part:
            prefix = part.split("*", 1)[0].rstrip("/")
            directory = root / prefix
            if not directory.exists():
                raise SystemExit(f"FAIL: visual_asset_review_queue row {row.queue_order} missing wildcard root: {prefix}")
            if not any(directory.glob(part.split("/")[-1])):
                raise SystemExit(f"FAIL: visual_asset_review_queue row {row.queue_order} wildcard matched nothing: {part}")
            continue

        path = root / part
        if not path.exists():
            raise SystemExit(f"FAIL: visual_asset_review_queue row {row.queue_order} missing source path: {part}")

    if row.queue_order in {"1", "2", "7", "8", "9", "11"} and not has_checked_path:
        raise SystemExit(f"FAIL: visual_asset_review_queue row {row.queue_order} lacks checked project path")


def validate_queue_rows(rows: list[VisualQueueRow]) -> None:
    if len(rows) != 11:
        raise SystemExit(f"FAIL: expected 11 visual queue rows, got {len(rows)}")

    order = tuple(row.queue_order for row in rows)
    if order != EXPECTED_ORDER:
        raise SystemExit(f"FAIL: unexpected visual queue order: {', '.join(order)}")

    p0_count = sum(1 for row in rows if row.priority == "P0")
    if p0_count != EXPECTED_P0_COUNT:
        raise SystemExit(f"FAIL: expected {EXPECTED_P0_COUNT} P0 visual queue rows, got {p0_count}")

    for row in rows:
        if row.priority not in {"P0", "P1", "P2"}:
            raise SystemExit(f"FAIL: visual queue row {row.queue_order} invalid priority: {row.priority}")
        if row.status != "PENDING_VERIFICATION":
            raise SystemExit(f"FAIL: visual queue row {row.queue_order} status must remain PENDING_VERIFICATION")
        require_any(row, "required_visual_proof", row.required_visual_proof, PROOF_TERMS)
        require_any(row, "reject_condition", row.reject_condition, REJECT_TERMS)
        validate_source_paths(row)


def validate_companion_doc(path: Path = COMPANION_PATH) -> None:
    text = load_text(path)
    for term in COMPANION_TERMS:
        if term not in text:
            raise SystemExit(f"FAIL: visual asset review companion doc missing term: {term}")


def validate_visual_asset_review_queue() -> list[VisualQueueRow]:
    rows = load_rows()
    validate_queue_rows(rows)
    validate_companion_doc()
    return rows


def main() -> None:
    rows = validate_visual_asset_review_queue()
    p0_count = sum(1 for row in rows if row.priority == "P0")
    print(f"VISUAL_ASSET_REVIEW_QUEUE_OK rows={len(rows)} p0={p0_count}")


if __name__ == "__main__":
    main()
