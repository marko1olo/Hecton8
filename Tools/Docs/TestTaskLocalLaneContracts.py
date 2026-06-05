#!/usr/bin/env python3
"""Static gate for HECTON-8 taskslocal lane contracts.

Use strict mode for new or materially rewritten serious batches:

    python -B Tools/Docs/TestTaskLocalLaneContracts.py taskslocal/<batch_name> --strict

Historical batches may be inspected with --allow-legacy. This tool intentionally
does not scan every old taskslocal folder by default.
"""

from __future__ import annotations

import argparse
import re
import sys
import tempfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
VALID_LANES = {
    "GAME_VISUAL",
    "RUNTIME_SYSTEM",
    "ASSET_PIPELINE",
    "LORE_CONTENT",
    "DOCS_RULES",
    "QA_PROOF",
    "ORCHESTRATION",
    "TOOLING_AUTOMATION",
}
REQUIRED_FIELDS = (
    "LANE_CLASS",
    "VALID_COMPLETION",
    "INVALID_COMPLETION",
    "KILL_SWITCH",
    "EVIDENCE_BUDGET",
)


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def rel(path: Path) -> str:
    try:
        return str(path.relative_to(ROOT))
    except ValueError:
        return str(path)


def task_id(path: Path) -> str:
    stem = path.stem
    numeric = re.match(r"^(\d+)(?:_|$)", stem)
    if numeric:
        return numeric.group(1)
    owner = re.match(r"^([A-Z]+_OWNER_\d+)(?:_|$)", stem)
    if owner:
        return owner.group(1)
    return stem.split("_", 1)[0]


def task_files(batch_dir: Path) -> list[Path]:
    files: list[Path] = []
    for pattern in ("*.txt", "*.md"):
        for path in batch_dir.glob(pattern):
            if path.name in {"BATCH_INDEX.txt", "README.md"}:
                continue
            files.append(path)
    return sorted(files)


def add_issue(issues: list[str], strict: bool, message: str) -> None:
    prefix = "ERROR" if strict else "WARN"
    issues.append(f"{prefix}: {message}")


def check_required_fields(path: Path, text: str, strict: bool, issues: list[str]) -> None:
    for field in REQUIRED_FIELDS:
        if f"{field}:" not in text and f"`{field}`" not in text and field not in text:
            add_issue(issues, strict, f"{rel(path)} missing {field}")


def check_lane_value(path: Path, text: str, strict: bool, issues: list[str]) -> None:
    match = re.search(r"(?m)^\s*(?:[-*]\s*)?`?LANE_CLASS`?\s*:\s*`?([A-Z_]+)`?", text)
    if not match:
        add_issue(issues, strict, f"{rel(path)} missing parseable LANE_CLASS value")
        return
    lane = match.group(1)
    if lane not in VALID_LANES:
        add_issue(issues, strict, f"{rel(path)} invalid LANE_CLASS {lane}")


def check_index_lane_roster(index: Path, text: str, ids: dict[str, Path], strict: bool, issues: list[str]) -> None:
    lane_pattern = "|".join(sorted(VALID_LANES))
    for ident in ids:
        row_match = re.search(rf"(?m)^.*\b{re.escape(ident)}\b.*\b({lane_pattern})\b.*$", text)
        if not row_match:
            add_issue(issues, strict, f"{rel(index)} missing lane roster row with valid LANE_CLASS for {ident}")


def check_batch(batch_dir: Path, strict: bool) -> tuple[list[str], list[str]]:
    errors_or_warnings: list[str] = []
    notes: list[str] = []

    batch_dir = batch_dir.resolve()
    index = batch_dir / "BATCH_INDEX.txt"
    files = task_files(batch_dir)

    if not batch_dir.exists():
        add_issue(errors_or_warnings, True, f"{rel(batch_dir)} does not exist")
        return errors_or_warnings, notes
    if not files:
        add_issue(errors_or_warnings, strict, f"{rel(batch_dir)} has no task files")

    ids: dict[str, Path] = {}
    for path in files:
        ident = task_id(path)
        if ident in ids:
            add_issue(
                errors_or_warnings,
                strict,
                f"{rel(path)} duplicates task id {ident} from {rel(ids[ident])}",
            )
        ids[ident] = path

    if not index.exists():
        add_issue(errors_or_warnings, strict, f"{rel(batch_dir)} missing BATCH_INDEX.txt")
    else:
        index_text = read_text(index)
        check_required_fields(index, index_text, strict, errors_or_warnings)
        for ident in ids:
            if ident not in index_text:
                add_issue(errors_or_warnings, strict, f"{rel(index)} missing task id {ident}")
        check_index_lane_roster(index, index_text, ids, strict, errors_or_warnings)

    for path in files:
        text = read_text(path)
        check_required_fields(path, text, strict, errors_or_warnings)
        check_lane_value(path, text, strict, errors_or_warnings)

    notes.append(f"batch={rel(batch_dir)} tasks={len(files)} strict={strict}")
    return errors_or_warnings, notes


def run_self_test() -> int:
    with tempfile.TemporaryDirectory(prefix="h8_lane_contract_") as tmp:
        batch = Path(tmp) / "sample_batch"
        batch.mkdir()
        (batch / "BATCH_INDEX.txt").write_text(
            "\n".join(
                [
                    "Batch: sample_batch",
                    "Lane roster:",
                    "1001 OWNER LANE_CLASS: RUNTIME_SYSTEM",
                    "VALID_COMPLETION: code plus proof",
                    "INVALID_COMPLETION: report-only",
                    "KILL_SWITCH: repeated same blocker",
                    "EVIDENCE_BUDGET: one static proof and one runtime proof",
                ]
            ),
            encoding="utf-8",
        )
        (batch / "1001_RUNTIME_OWNER.txt").write_text(
            "\n".join(
                [
                    "LANE_CLASS: RUNTIME_SYSTEM",
                    "VALID_COMPLETION: code plus proof",
                    "INVALID_COMPLETION: report-only",
                    "KILL_SWITCH: repeated same blocker",
                    "EVIDENCE_BUDGET: one static proof and one runtime proof",
                ]
            ),
            encoding="utf-8",
        )
        issues, notes = check_batch(batch, strict=True)
        for note in notes:
            print(note)
        if issues:
            print("TASKLOCAL_LANE_CONTRACT_SELFTEST=FAIL")
            for issue in issues:
                print(f"- {issue}")
            return 1
    print("TASKLOCAL_LANE_CONTRACT_SELFTEST=PASS")
    return 0


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("batches", nargs="*", help="taskslocal batch directories to validate")
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--strict", action="store_true", help="fail on missing lane contracts")
    mode.add_argument("--allow-legacy", action="store_true", help="warn on missing lane contracts")
    parser.add_argument("--self-test", action="store_true", help="run an internal positive fixture")
    return parser.parse_args(argv)


def main(argv: list[str]) -> int:
    args = parse_args(argv)
    if args.self_test:
        return run_self_test()
    if not args.batches:
        print("TASKLOCAL_LANE_CONTRACT_CHECK=USAGE")
        print("Provide one or more taskslocal/<batch_name> paths.")
        print("Use --strict for new/reissued serious batches or --allow-legacy for historical inspection.")
        return 2

    strict = not args.allow_legacy
    all_issues: list[str] = []
    all_notes: list[str] = []

    for raw_batch in args.batches:
        batch = Path(raw_batch)
        if not batch.is_absolute():
            batch = ROOT / batch
        issues, notes = check_batch(batch, strict=strict)
        all_issues.extend(issues)
        all_notes.extend(notes)

    for note in all_notes:
        print(note)

    if all_issues:
        status = "FAIL" if strict else "WARN"
        print(f"TASKLOCAL_LANE_CONTRACT_CHECK={status}")
        for issue in all_issues:
            print(f"- {issue}")
        return 1 if strict else 0

    print("TASKLOCAL_LANE_CONTRACT_CHECK=PASS")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
