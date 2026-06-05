#!/usr/bin/env python3
"""Validate the current asset-front static CSV summary."""

from __future__ import annotations

import csv
import re
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SUMMARY_PATH = ROOT / "Docs/Reports/AssetSystem_20260605/ASSET_STATIC_VALIDATION_SUMMARY_20260605.md"


@dataclass(frozen=True)
class SummaryRow:
    file_path: str
    rows: int
    columns: int
    empty_cells: int


@dataclass(frozen=True)
class CsvStats:
    rows: int
    columns: int
    empty_cells: int


CURRENT_ROW_PATTERN = re.compile(
    r"^\| `(?P<path>[^`]+)` \| (?P<rows>\d+) \| (?P<columns>\d+) \| (?P<empty>\d+) \|$"
)
TOTAL_PATTERN = re.compile(r"Total current rows: `(?P<rows>\d+)`")


def parse_current_rows(summary_text: str) -> list[SummaryRow]:
    rows: list[SummaryRow] = []
    inside_current_set = False
    for line in summary_text.splitlines():
        if line == "## Current Static Parse Set":
            inside_current_set = True
            continue
        if line == "## Excluded Older/Sidecar CSV Boundary":
            inside_current_set = False
            continue
        if not inside_current_set:
            continue
        match = CURRENT_ROW_PATTERN.match(line)
        if match is None:
            continue
        rows.append(
            SummaryRow(
                file_path=match.group("path"),
                rows=int(match.group("rows")),
                columns=int(match.group("columns")),
                empty_cells=int(match.group("empty")),
            )
        )
    return rows


def parse_declared_total(summary_text: str) -> int:
    match = TOTAL_PATTERN.search(summary_text)
    if match is None:
        raise SystemExit("FAIL: summary missing Total current rows")
    return int(match.group("rows"))


def count_csv(path: Path) -> CsvStats:
    if not path.exists():
        raise SystemExit(f"FAIL: missing CSV: {path}")
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        if reader.fieldnames is None:
            raise SystemExit(f"FAIL: CSV has no header: {path}")
        rows = list(reader)
    empty_cells = 0
    for row in rows:
        for field_name in reader.fieldnames:
            value = row.get(field_name)
            if value is None or str(value).strip() == "":
                empty_cells += 1
    return CsvStats(rows=len(rows), columns=len(reader.fieldnames), empty_cells=empty_cells)


def validate_summary(summary_path: Path = SUMMARY_PATH, root: Path = ROOT) -> list[SummaryRow]:
    summary_text = summary_path.read_text(encoding="utf-8")
    rows = parse_current_rows(summary_text)
    if not rows:
        raise SystemExit("FAIL: no current static parse rows found")

    expected_sum = 0
    for row in rows:
        expected_sum += row.rows
        actual = count_csv(root / row.file_path)
        if actual.rows != row.rows:
            raise SystemExit(f"FAIL: {row.file_path} rows expected {row.rows}, actual {actual.rows}")
        if actual.columns != row.columns:
            raise SystemExit(f"FAIL: {row.file_path} columns expected {row.columns}, actual {actual.columns}")
        if actual.empty_cells != row.empty_cells:
            raise SystemExit(
                f"FAIL: {row.file_path} empty cells expected {row.empty_cells}, actual {actual.empty_cells}"
            )

    declared_total = parse_declared_total(summary_text)
    if expected_sum != declared_total:
        raise SystemExit(f"FAIL: total rows expected {declared_total}, table sum {expected_sum}")
    return rows


def main() -> None:
    rows = validate_summary()
    total = sum(row.rows for row in rows)
    print(f"ASSET_STATIC_VALIDATION_SUMMARY_OK files={len(rows)} rows={total}")


if __name__ == "__main__":
    main()
