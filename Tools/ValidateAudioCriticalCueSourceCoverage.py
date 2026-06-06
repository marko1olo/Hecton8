#!/usr/bin/env python3
"""Validate audio critical cue candidate-source coverage against current ledgers."""

from __future__ import annotations

import argparse
import csv
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
CUE_MATRIX = ROOT / "Docs/AssetAudit/AUDIO_CRITICAL_CUE_COVERAGE_MATRIX_20260605.csv"
AUDIO_LEDGER = ROOT / "Docs/Audio/audio_asset_ledger.csv"
AUDIO_TECHNICAL = ROOT / "Docs/AssetAudit/AUDIO_FILE_TECHNICAL_PROPERTIES_20260605.csv"
NO_SOURCE_SENTINEL = "NONE_IN_CURRENT_AUDIO_LEDGER"


@dataclass(frozen=True)
class CoverageIssue:
    cue_id: str
    category: str
    detail: str


@dataclass(frozen=True)
class CoverageResult:
    rows: int
    candidate_paths: int
    ledger_matches: int
    missing_source_rows: int
    placeholder_rows: int
    issues: tuple[CoverageIssue, ...]

    @property
    def blockers(self) -> int:
        return len(self.issues)


def display_path(path: Path) -> str:
    try:
        return str(path.relative_to(ROOT)).replace("\\", "/")
    except ValueError:
        return str(path)


def load_csv(path: Path) -> list[dict[str, str]]:
    if not path.exists():
        raise SystemExit(f"FAIL: missing file: {display_path(path)}")
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        if reader.fieldnames is None:
            raise SystemExit(f"FAIL: CSV has no header: {display_path(path)}")
        return [{key: (value or "").strip() for key, value in row.items()} for row in reader]


def load_index(path: Path, key: str = "path") -> dict[str, dict[str, str]]:
    rows = load_csv(path)
    indexed: dict[str, dict[str, str]] = {}
    for row_number, row in enumerate(rows, start=2):
        value = (row.get(key) or "").strip()
        if not value:
            raise SystemExit(f"FAIL: {display_path(path)} empty {key} at row {row_number}")
        if value in indexed:
            raise SystemExit(f"FAIL: {display_path(path)} duplicate {key}: {value}")
        indexed[value] = row
    return indexed


def split_candidates(value: str) -> list[str]:
    if value == NO_SOURCE_SENTINEL:
        return []
    return [item.strip() for item in value.split(";") if item.strip()]


def validate_rows(
    cue_rows: list[dict[str, str]],
    ledger_by_path: dict[str, dict[str, str]],
    technical_by_path: dict[str, dict[str, str]],
    root: Path = ROOT,
) -> CoverageResult:
    issues: list[CoverageIssue] = []
    candidate_paths = 0
    ledger_matches = 0
    missing_source_rows = 0
    placeholder_rows = 0

    for row_number, row in enumerate(cue_rows, start=2):
        cue_id = (row.get("CoverageId") or "").strip()
        status = (row.get("Status") or "").strip()
        candidates_value = (row.get("CandidateSources") or "").strip()
        missing_or_blocked = (row.get("MissingOrBlocked") or "").strip()

        if not cue_id:
            issues.append(CoverageIssue(f"row{row_number}", "missing_cue_id", "CoverageId is empty"))
            continue
        if not candidates_value:
            issues.append(CoverageIssue(cue_id, "empty_candidate_sources", "CandidateSources is empty"))
            continue

        if candidates_value == NO_SOURCE_SENTINEL:
            missing_source_rows += 1
            if status != "MISSING_SOURCE_CANDIDATE":
                issues.append(CoverageIssue(cue_id, "missing_source_status", status))
            if "No dedicated" not in missing_or_blocked and "No " not in missing_or_blocked:
                issues.append(CoverageIssue(cue_id, "weak_missing_source_boundary", missing_or_blocked))
            continue

        candidates = split_candidates(candidates_value)
        candidate_paths += len(candidates)
        if status == "MISSING_SOURCE_CANDIDATE":
            issues.append(CoverageIssue(cue_id, "source_present_but_status_missing", candidates_value))

        placeholder_candidate_count = 0
        for candidate in candidates:
            path = root / candidate
            if not path.exists():
                issues.append(CoverageIssue(cue_id, "missing_candidate_file", candidate))
                continue

            ledger_row = ledger_by_path.get(candidate)
            if ledger_row is None:
                issues.append(CoverageIssue(cue_id, "missing_from_audio_ledger", candidate))
                continue
            ledger_matches += 1

            if candidate not in technical_by_path:
                issues.append(CoverageIssue(cue_id, "missing_from_audio_technical_properties", candidate))

            if ledger_row.get("placeholder_flag", "").lower() == "true":
                placeholder_candidate_count += 1

        if status == "PLACEHOLDER_BLOCKED":
            placeholder_rows += 1
            if placeholder_candidate_count == 0:
                issues.append(CoverageIssue(cue_id, "placeholder_status_without_placeholder_source", candidates_value))
            if "placeholder" not in missing_or_blocked.lower():
                issues.append(CoverageIssue(cue_id, "weak_placeholder_boundary", missing_or_blocked))

    return CoverageResult(
        rows=len(cue_rows),
        candidate_paths=candidate_paths,
        ledger_matches=ledger_matches,
        missing_source_rows=missing_source_rows,
        placeholder_rows=placeholder_rows,
        issues=tuple(issues),
    )


def validate_source_coverage() -> CoverageResult:
    return validate_rows(
        cue_rows=load_csv(CUE_MATRIX),
        ledger_by_path=load_index(AUDIO_LEDGER),
        technical_by_path=load_index(AUDIO_TECHNICAL),
    )


def print_result(result: CoverageResult) -> None:
    status = "AUDIO_CRITICAL_CUE_SOURCE_COVERAGE_OK"
    if result.blockers:
        status = "AUDIO_CRITICAL_CUE_SOURCE_COVERAGE_REJECTED"
    print(
        f"{status} blockers={result.blockers} rows={result.rows} "
        f"candidate_paths={result.candidate_paths} ledger_matches={result.ledger_matches} "
        f"missing_source_rows={result.missing_source_rows} placeholder_rows={result.placeholder_rows}"
    )
    for issue in result.issues:
        print(f"- {issue.cue_id}: {issue.category}: {issue.detail}")
    if result.blockers:
        print("+ evidence-class: STATIC_AUDIO_LEDGER_CANDIDATE_PATHS / PENDING SOURCE AUTHORING OR LEDGER PATCH")
        print("+ side-effects: no import/reimport/meta write/Addressables/build/Unity action performed")


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--no-fail", action="store_true", help="Print rejection status but return success.")
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    result = validate_source_coverage()
    print_result(result)
    if result.blockers and not args.no_fail:
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
