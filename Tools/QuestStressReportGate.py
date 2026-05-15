#!/usr/bin/env python3
"""Fail-fast gate for QuestStressTest JSON reports.

This is intentionally standalone so the validator can be used in CI without
coupling to Unity editor state or mutating authored quest assets.
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any, Iterable


DEFAULT_MIN_SEQUENCES = 1_000_000


def _walk_json(value: Any) -> Iterable[tuple[str, Any]]:
    if isinstance(value, dict):
        for key, child in value.items():
            yield key, child
            yield from _walk_json(child)
    elif isinstance(value, list):
        for child in value:
            yield from _walk_json(child)


def _normalize_key(key: str) -> str:
    return "".join(ch for ch in key.lower() if ch.isalnum())


def _first_int(report: Any, candidate_keys: tuple[str, ...]) -> int | None:
    normalized = {_normalize_key(key) for key in candidate_keys}
    for key, value in _walk_json(report):
        if _normalize_key(key) in normalized and isinstance(value, int):
            return value
    return None


def _first_int_containing(report: Any, required_tokens: tuple[str, ...]) -> int | None:
    normalized_tokens = tuple(_normalize_key(token) for token in required_tokens)
    for key, value in _walk_json(report):
        normalized_key = _normalize_key(key)
        if isinstance(value, int) and all(token in normalized_key for token in normalized_tokens):
            return value
    return None


def _contains_text(report: Any, needles: tuple[str, ...]) -> bool:
    lowered = tuple(needle.lower() for needle in needles)
    for _key, value in _walk_json(report):
        if isinstance(value, str):
            text = value.lower()
            for needle in lowered:
                if needle in text:
                    return True
    return False


def _list_count(report: Any, candidate_keys: tuple[str, ...]) -> int | None:
    normalized = {_normalize_key(key) for key in candidate_keys}
    for key, value in _walk_json(report):
        if _normalize_key(key) in normalized and isinstance(value, list):
            return len(value)
    return None


def _build_failures(report: Any, min_sequences: int) -> list[str]:
    failures: list[str] = []

    sequences = _first_int(
        report,
        (
            "sequences",
            "sequence_count",
            "requested_sequences",
            "simulated_sequences",
        ),
    )
    if sequences is None:
        failures.append("sequence count missing from report")
    elif sequences < min_sequences:
        failures.append(f"sequence count {sequences} below required {min_sequences}")

    no_active = _first_int(
        report,
        (
            "no_active_softlocks",
            "no_active_softlock_sequences",
            "softlocks_no_active_quest",
            "no_active_quest_softlocks",
        ),
    )
    if no_active is None:
        no_active = _first_int_containing(report, ("no", "active", "softlock"))
    if no_active is None:
        failures.append("no-active softlock count missing from report")
    elif no_active != 0:
        failures.append(f"no-active softlocks detected: {no_active}")

    end_completed = _first_int(
        report,
        (
            "end_completed",
            "end_completed_sequences",
            "end_game_completed_sequences",
            "completed_end_sequences",
        ),
    )
    if end_completed is None:
        end_completed = _first_int_containing(report, ("end", "completed"))
    if end_completed is None:
        failures.append("End Game completion count missing from report")
    elif end_completed == 0:
        failures.append("End Game never completed in simulated sequences")

    manual_stalls = _first_int(
        report,
        (
            "manual_no_completion_terminal_stalls",
            "manual_terminal_stalls",
            "terminal_manual_stalls",
            "no_completion_terminal_stalls",
        ),
    )
    if manual_stalls is not None and manual_stalls > 0:
        failures.append(f"manual/no-completion terminal stalls detected: {manual_stalls}")
    elif manual_stalls is None:
        manual_stalls = _first_int_containing(report, ("manual", "stall"))
        if manual_stalls is not None and manual_stalls > 0:
            failures.append(f"manual terminal stalls detected: {manual_stalls}")

    dead_end_count = _list_count(
        report,
        (
            "dead_end_findings",
            "dead_ends",
            "dead_end_quests",
            "no_complete_quests",
        ),
    )
    if dead_end_count is not None and dead_end_count > 0:
        failures.append(f"dead-end/no-complete findings detected: {dead_end_count}")

    impossible_count = _list_count(
        report,
        (
            "impossible_requirements",
            "impossible_or_external_requirements",
            "blocked_requirements",
        ),
    )
    if impossible_count is not None and impossible_count > 0:
        failures.append(f"impossible/external requirements detected: {impossible_count}")

    if _contains_text(report, ("critical", "soft-lock", "softlock")):
        failures.append("report contains critical/soft-lock finding text")

    return failures


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Validate a QuestStressTest JSON report for CI handoff."
    )
    parser.add_argument("report", type=Path, help="QuestStressTest JSON report path.")
    parser.add_argument(
        "--min-sequences",
        type=int,
        default=DEFAULT_MIN_SEQUENCES,
        help="Minimum simulated sequence count required for acceptance.",
    )
    args = parser.parse_args()

    if not args.report.is_file():
        print(f"QUEST_GATE_FAIL report missing: {args.report}", file=sys.stderr)
        return 2

    with args.report.open("r", encoding="utf-8") as handle:
        report = json.load(handle)

    failures = _build_failures(report, args.min_sequences)
    if failures:
        print("QUEST_GATE_FAIL")
        for failure in failures:
            print(f"- {failure}")
        return 1

    print("QUEST_GATE_PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
