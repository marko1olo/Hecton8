#!/usr/bin/env python3
"""Static gate for HECTON-8 taskslocal lane contracts.

Use strict mode for new or materially rewritten serious batches:

    python -B Tools/Docs/TestTaskLocalLaneContracts.py taskslocal/<batch_name> --strict

Historical batches may be inspected with --allow-legacy. This tool intentionally
does not scan every old taskslocal folder by default.
"""

from __future__ import annotations

import argparse
import os
import re
import shutil
import sys
import uuid
from datetime import datetime, timezone
from pathlib import Path
from typing import NamedTuple


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
VALID_DELIVERABLE_CLASSES = {
    "SOURCE_CHANGE",
    "ASSET_CHANGE",
    "CONTENT_ARTIFACT",
    "FRESH_PROOF",
    "BLOCKER",
    "POLICY_DOC",
}
LANE_DELIVERABLE_CLASSES = {
    "GAME_VISUAL": {"SOURCE_CHANGE", "ASSET_CHANGE", "FRESH_PROOF", "BLOCKER"},
    "RUNTIME_SYSTEM": {"SOURCE_CHANGE", "ASSET_CHANGE", "FRESH_PROOF", "BLOCKER"},
    "ASSET_PIPELINE": {"SOURCE_CHANGE", "ASSET_CHANGE", "FRESH_PROOF", "BLOCKER"},
    "LORE_CONTENT": {"CONTENT_ARTIFACT", "SOURCE_CHANGE", "FRESH_PROOF", "BLOCKER"},
    "DOCS_RULES": {"POLICY_DOC", "SOURCE_CHANGE", "FRESH_PROOF", "BLOCKER"},
    "QA_PROOF": {"FRESH_PROOF", "SOURCE_CHANGE", "POLICY_DOC", "BLOCKER"},
    "ORCHESTRATION": {"POLICY_DOC", "SOURCE_CHANGE", "FRESH_PROOF", "BLOCKER"},
    "TOOLING_AUTOMATION": {"SOURCE_CHANGE", "FRESH_PROOF", "BLOCKER"},
}
LANE_PROOF_TERMS = {
    "GAME_VISUAL": ("capture", "screenshot", "reference", "frame debugger", "renderdoc", "unity", "material", "readback", "compact", "high"),
    "RUNTIME_SYSTEM": ("source", "compile", "import", "unity", "playmode", "player", "profiler", "gc", "save", "load", "telemetry", "dispatcher"),
    "ASSET_PIPELINE": ("asset", "import", "manifest", "validation", "screenshot", "lod", "collider", "material", "readback", "memory"),
    "LORE_CONTENT": ("packet", "locale", "export", "import", "audit", "appliedlore", "surface", "index", "binding", "translation", "locid"),
    "DOCS_RULES": ("rule", "routing", "diff", "check", "test", "generator", "grep", "source"),
    "QA_PROOF": ("command", "artifact", "evidence", "test", "audit", "proof", "check", "log"),
    "ORCHESTRATION": ("lane", "contract", "taskslocal", "status", "log", "proof", "batch", "agent"),
    "TOOLING_AUTOMATION": ("test", "validator", "negative", "fixture", "command", "tool", "source"),
}
LORE_APPLIED_CONTENT_TERMS = (
    "appliedlore",
    "applied lore",
    "grand library",
    "production source guard",
    "page exporter",
    "packet coverage",
    "publication surface",
    "data monolith",
    "route card",
    "binding map",
    "validategrandlibrarylorequality",
)
PROOF_ACTION_TERMS = ("run", "execute", "capture", "audit", "validate", "test", "check", "compile", "import", "export", "bake", "readback", "diff")
WEAK_PROOF_TERMS = ("report", "summary", "status", "rationale", "route card", "notes", "todo", "tbd", "n/a", "none")
REQUIRED_FIELDS = (
    "LANE_CLASS",
    "DELIVERABLE_CLASS",
    "VALID_COMPLETION",
    "INVALID_COMPLETION",
    "KILL_SWITCH",
    "PROOF_ROUTE",
    "EVIDENCE_BUDGET",
)
DEPENDENCY_GUARD_TERMS = ("same-wave", "sibling")
DEPENDENCY_OUTPUT_TERMS = ("dependency", "required output", "unverified output", "candidate", "blocked")
DEFAULT_HISTORICAL_CUTOFF = datetime(2026, 6, 1, tzinfo=timezone.utc)


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def writable_temp_root() -> Path:
    candidates = [
        os.environ.get("H8_TOOL_TMP"),
        str(ROOT / "Temp" / "ToolSelfTests"),
        "C:/tmp",
        os.environ.get("TMP"),
        os.environ.get("TEMP"),
    ]
    for raw_candidate in candidates:
        if not raw_candidate:
            continue
        candidate = Path(raw_candidate)
        try:
            candidate.mkdir(parents=True, exist_ok=True)
            probe_dir = candidate / ".h8_write_probe_dir"
            probe_dir.mkdir(exist_ok=True)
            probe = probe_dir / "probe.txt"
            probe.write_text("", encoding="utf-8")
            try:
                probe.unlink()
                probe_dir.rmdir()
            except OSError:
                pass
            return candidate
        except OSError:
            continue
    raise RuntimeError("No writable temp directory for lane-contract self-test")


class SelfTestDirectory:
    def __init__(self, prefix: str) -> None:
        self.path = writable_temp_root() / f"{prefix}{uuid.uuid4().hex}"

    def __enter__(self) -> str:
        self.path.mkdir(parents=True, exist_ok=True)
        return str(self.path)

    def __exit__(self, exc_type, exc, tb) -> bool:
        shutil.rmtree(self.path, ignore_errors=True)
        return False


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


class Issue(NamedTuple):
    strict: bool
    message: str


def add_issue(issues: list[Issue], strict: bool, message: str) -> None:
    issues.append(Issue(strict=strict, message=message))


def format_issue(issue: Issue) -> str:
    prefix = "ERROR" if issue.strict else "WARN"
    return f"{prefix}: {issue.message}"


def issue_list_has_errors(issues: list[Issue]) -> bool:
    return any(issue.strict for issue in issues)


def parse_historical_date_from_name(path: Path) -> datetime | None:
    for part in (path.stem, path.parent.name):
        match = re.search(r"(?<!\d)(20\d{2})(\d{2})(\d{2})(?!\d)", part)
        if not match:
            continue
        try:
            return datetime(int(match.group(1)), int(match.group(2)), int(match.group(3)), tzinfo=timezone.utc)
        except ValueError:
            continue
    return None


def is_historical_file(path: Path, cutoff: datetime) -> bool:
    dated = parse_historical_date_from_name(path)
    return dated is not None and dated < cutoff


def has_field_label(text: str, field: str) -> bool:
    if re.search(rf"(?im)^\s*(?:[-*]\s*)?.*?`?{re.escape(field)}`?\s*:", text):
        return True
    return bool(re.search(rf"(?is)<{re.escape(field)}>.*?</{re.escape(field)}>", text))


def field_value(text: str, field: str) -> str:
    line_match = re.search(rf"(?im)^\s*(?:[-*]\s*)?.*?`?{re.escape(field)}`?\s*:\s*`?([^\r\n`]+)`?", text)
    if line_match:
        return line_match.group(1).strip()
    xml_match = re.search(rf"(?is)<{re.escape(field)}>(.*?)</{re.escape(field)}>", text)
    if xml_match:
        return re.sub(r"\s+", " ", xml_match.group(1)).strip()
    return ""


def check_required_fields(path: Path, text: str, strict: bool, issues: list[str]) -> None:
    for field in REQUIRED_FIELDS:
        if not has_field_label(text, field):
            add_issue(issues, strict, f"{rel(path)} missing {field}")


def parse_lane_value(path: Path, text: str, strict: bool, issues: list[str]) -> str:
    lane = field_value(text, "LANE_CLASS").split(" ", 1)[0].strip("`")
    if not lane:
        add_issue(issues, strict, f"{rel(path)} missing parseable LANE_CLASS value")
        return ""
    if lane not in VALID_LANES:
        add_issue(issues, strict, f"{rel(path)} invalid LANE_CLASS {lane}")
    return lane


def parse_deliverable_value(path: Path, text: str, strict: bool, issues: list[str]) -> str:
    deliverable = field_value(text, "DELIVERABLE_CLASS").split(" ", 1)[0].strip("`")
    if not deliverable:
        add_issue(issues, strict, f"{rel(path)} missing parseable DELIVERABLE_CLASS value")
        return ""
    if deliverable not in VALID_DELIVERABLE_CLASSES:
        add_issue(issues, strict, f"{rel(path)} invalid DELIVERABLE_CLASS {deliverable}")
    return deliverable


def check_lane_deliverable_pair(path: Path, lane: str, deliverable: str, strict: bool, issues: list[str]) -> None:
    if lane not in LANE_DELIVERABLE_CLASSES or deliverable not in VALID_DELIVERABLE_CLASSES:
        return
    if deliverable not in LANE_DELIVERABLE_CLASSES[lane]:
        allowed = ",".join(sorted(LANE_DELIVERABLE_CLASSES[lane]))
        add_issue(issues, strict, f"{rel(path)} DELIVERABLE_CLASS {deliverable} is not valid for LANE_CLASS {lane}; allowed={allowed}")


def check_proof_route(path: Path, text: str, lane: str, strict: bool, issues: list[str]) -> None:
    proof_route = field_value(text, "PROOF_ROUTE")
    if not proof_route:
        add_issue(issues, strict, f"{rel(path)} missing parseable PROOF_ROUTE value")
        return
    normalized = proof_route.lower()
    if len(normalized) < 24:
        add_issue(issues, strict, f"{rel(path)} PROOF_ROUTE too short to name a concrete proof route")
    if any(term in normalized for term in WEAK_PROOF_TERMS) and not any(term in normalized for term in PROOF_ACTION_TERMS):
        add_issue(issues, strict, f"{rel(path)} PROOF_ROUTE is report/status-only, not an executable proof route")
    lane_terms = LANE_PROOF_TERMS.get(lane, ())
    if lane_terms and not any(term in normalized for term in lane_terms):
        add_issue(issues, strict, f"{rel(path)} PROOF_ROUTE lacks lane-specific proof terms for {lane}")
    if lane == "LORE_CONTENT" and not any(term in normalized for term in LORE_APPLIED_CONTENT_TERMS):
        add_issue(issues, strict, f"{rel(path)} PROOF_ROUTE lacks AppliedLore/Grand Library export/import/coverage proof")
    if not any(term in normalized for term in PROOF_ACTION_TERMS):
        add_issue(issues, strict, f"{rel(path)} PROOF_ROUTE lacks an executable action term")


def check_index_lane_roster(index: Path, text: str, ids: dict[str, Path], strict: bool, issues: list[str]) -> None:
    lane_pattern = "|".join(sorted(VALID_LANES))
    for ident in ids:
        row_match = re.search(rf"(?m)^.*\b{re.escape(ident)}\b.*\b({lane_pattern})\b.*$", text)
        if not row_match:
            add_issue(issues, strict, f"{rel(index)} missing lane roster row with valid LANE_CLASS for {ident}")


def check_index_dependency_guard(index: Path, text: str, strict: bool, issues: list[str]) -> None:
    lower = text.lower()
    has_wave_scope = any(term in lower for term in DEPENDENCY_GUARD_TERMS)
    has_output_policy = any(term in lower for term in DEPENDENCY_OUTPUT_TERMS)
    if not (has_wave_scope and has_output_policy):
        add_issue(issues, strict, f"{rel(index)} missing same-wave/sibling dependency guard")


def check_batch(batch_dir: Path, strict: bool, strict_historical: bool = False, historical_cutoff: datetime = DEFAULT_HISTORICAL_CUTOFF) -> tuple[list[Issue], list[str]]:
    errors_or_warnings: list[Issue] = []
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
        index_strict = strict and (strict_historical or not is_historical_file(index, historical_cutoff))
        check_required_fields(index, index_text, index_strict, errors_or_warnings)
        index_lane = parse_lane_value(index, index_text, index_strict, errors_or_warnings)
        index_deliverable = parse_deliverable_value(index, index_text, index_strict, errors_or_warnings)
        check_lane_deliverable_pair(index, index_lane, index_deliverable, index_strict, errors_or_warnings)
        check_proof_route(index, index_text, index_lane, index_strict, errors_or_warnings)
        for ident in ids:
            if ident not in index_text:
                add_issue(errors_or_warnings, index_strict, f"{rel(index)} missing task id {ident}")
        check_index_lane_roster(index, index_text, ids, index_strict, errors_or_warnings)
        check_index_dependency_guard(index, index_text, index_strict, errors_or_warnings)

    for path in files:
        text = read_text(path)
        path_strict = strict and (strict_historical or not is_historical_file(path, historical_cutoff))
        check_required_fields(path, text, path_strict, errors_or_warnings)
        lane = parse_lane_value(path, text, path_strict, errors_or_warnings)
        deliverable = parse_deliverable_value(path, text, path_strict, errors_or_warnings)
        check_lane_deliverable_pair(path, lane, deliverable, path_strict, errors_or_warnings)
        check_proof_route(path, text, lane, path_strict, errors_or_warnings)

    notes.append(f"batch={rel(batch_dir)} tasks={len(files)} strict={strict} strict_historical={strict_historical} cutoff={historical_cutoff.date().isoformat()}")
    return errors_or_warnings, notes


def run_self_test() -> int:
    with SelfTestDirectory(prefix="h8_lane_contract_") as tmp:
        batch = Path(tmp) / "sample_batch"
        batch.mkdir()
        (batch / "BATCH_INDEX.txt").write_text(
            "\n".join(
                [
                    "Batch: sample_batch",
                    "Lane roster:",
                    "1001 OWNER LANE_CLASS: RUNTIME_SYSTEM",
                    "DELIVERABLE_CLASS: SOURCE_CHANGE",
                    "VALID_COMPLETION: code plus proof",
                    "INVALID_COMPLETION: report-only",
                    "KILL_SWITCH: repeated same blocker",
                    "PROOF_ROUTE: source audit, compile/import proof, and runtime proof when process gate is clear",
                    "EVIDENCE_BUDGET: one static proof and one runtime proof",
                    "Dependency guard: no same-wave sibling task output may be required; unverified outputs are CANDIDATE or BLOCKED.",
                ]
            ),
            encoding="utf-8",
        )
        (batch / "1001_RUNTIME_OWNER.txt").write_text(
            "\n".join(
                [
                    "LANE_CLASS: RUNTIME_SYSTEM",
                    "DELIVERABLE_CLASS: SOURCE_CHANGE",
                    "VALID_COMPLETION: code plus proof",
                    "INVALID_COMPLETION: report-only",
                    "KILL_SWITCH: repeated same blocker",
                    "PROOF_ROUTE: source audit, compile/import proof, and runtime proof when process gate is clear",
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
                print(f"- {format_issue(issue)}")
            return 1

        weak_batch = Path(tmp) / "weak_batch"
        weak_batch.mkdir()
        (weak_batch / "BATCH_INDEX.txt").write_text(
            "\n".join(
                [
                    "Batch: weak_batch",
                    "Lane roster:",
                    "1002 OWNER LANE_CLASS: RUNTIME_SYSTEM",
                    "DELIVERABLE_CLASS: SOURCE_CHANGE",
                    "VALID_COMPLETION: code plus proof",
                    "INVALID_COMPLETION: report-only",
                    "KILL_SWITCH: repeated same blocker",
                    "PROOF_ROUTE: source audit, compile/import proof, and runtime proof when process gate is clear",
                    "EVIDENCE_BUDGET: one static proof and one runtime proof",
                    "Dependency note: dependency exists.",
                ]
            ),
            encoding="utf-8",
        )
        (weak_batch / "1002_RUNTIME_OWNER.txt").write_text(
            "\n".join(
                [
                    "LANE_CLASS: RUNTIME_SYSTEM",
                    "DELIVERABLE_CLASS: SOURCE_CHANGE",
                    "VALID_COMPLETION: code plus proof",
                    "INVALID_COMPLETION: report-only",
                    "KILL_SWITCH: repeated same blocker",
                    "PROOF_ROUTE: source audit, compile/import proof, and runtime proof when process gate is clear",
                    "EVIDENCE_BUDGET: one static proof and one runtime proof",
                ]
            ),
            encoding="utf-8",
        )
        weak_issues, _ = check_batch(weak_batch, strict=True)
        if not any("same-wave/sibling dependency guard" in issue.message for issue in weak_issues):
            print("TASKLOCAL_LANE_CONTRACT_SELFTEST=FAIL")
            print("- weak dependency wording passed without same-wave/sibling output policy")
            return 1

        invalid_deliverable_batch = Path(tmp) / "invalid_deliverable_batch"
        invalid_deliverable_batch.mkdir()
        (invalid_deliverable_batch / "BATCH_INDEX.txt").write_text(
            "\n".join(
                [
                    "Batch: invalid_deliverable_batch",
                    "Lane roster:",
                    "1003 OWNER LANE_CLASS: RUNTIME_SYSTEM",
                    "DELIVERABLE_CLASS: REPORT_ONLY",
                    "VALID_COMPLETION: code plus proof",
                    "INVALID_COMPLETION: report-only",
                    "KILL_SWITCH: repeated same blocker",
                    "PROOF_ROUTE: source audit and proof",
                    "EVIDENCE_BUDGET: one static proof and one runtime proof",
                    "Dependency guard: no same-wave sibling task output may be required; unverified outputs are CANDIDATE or BLOCKED.",
                ]
            ),
            encoding="utf-8",
        )
        (invalid_deliverable_batch / "1003_RUNTIME_OWNER.txt").write_text(
            "\n".join(
                [
                    "LANE_CLASS: RUNTIME_SYSTEM",
                    "DELIVERABLE_CLASS: REPORT_ONLY",
                    "VALID_COMPLETION: code plus proof",
                    "INVALID_COMPLETION: report-only",
                    "KILL_SWITCH: repeated same blocker",
                    "PROOF_ROUTE: source audit and proof",
                    "EVIDENCE_BUDGET: one static proof and one runtime proof",
                ]
            ),
            encoding="utf-8",
        )
        invalid_deliverable_issues, _ = check_batch(invalid_deliverable_batch, strict=True)
        if not any("invalid DELIVERABLE_CLASS REPORT_ONLY" in issue.message for issue in invalid_deliverable_issues):
            print("TASKLOCAL_LANE_CONTRACT_SELFTEST=FAIL")
            print("- invalid deliverable class passed strict validation")
            return 1

        incompatible_deliverable_batch = Path(tmp) / "incompatible_deliverable_batch"
        incompatible_deliverable_batch.mkdir()
        (incompatible_deliverable_batch / "BATCH_INDEX.txt").write_text(
            "\n".join(
                [
                    "Batch: incompatible_deliverable_batch",
                    "Lane roster:",
                    "1004 OWNER LANE_CLASS: RUNTIME_SYSTEM",
                    "DELIVERABLE_CLASS: POLICY_DOC",
                    "VALID_COMPLETION: runtime owner source plus proof",
                    "INVALID_COMPLETION: report-only",
                    "KILL_SWITCH: repeated same blocker",
                    "PROOF_ROUTE: run source audit and compile/import proof",
                    "EVIDENCE_BUDGET: one static proof and one runtime proof",
                    "Dependency guard: no same-wave sibling task output may be required; unverified outputs are CANDIDATE or BLOCKED.",
                ]
            ),
            encoding="utf-8",
        )
        (incompatible_deliverable_batch / "1004_RUNTIME_OWNER.txt").write_text(
            "\n".join(
                [
                    "LANE_CLASS: RUNTIME_SYSTEM",
                    "DELIVERABLE_CLASS: POLICY_DOC",
                    "VALID_COMPLETION: runtime owner source plus proof",
                    "INVALID_COMPLETION: report-only",
                    "KILL_SWITCH: repeated same blocker",
                    "PROOF_ROUTE: run source audit and compile/import proof",
                    "EVIDENCE_BUDGET: one static proof and one runtime proof",
                ]
            ),
            encoding="utf-8",
        )
        incompatible_issues, _ = check_batch(incompatible_deliverable_batch, strict=True)
        if not any("not valid for LANE_CLASS RUNTIME_SYSTEM" in issue.message for issue in incompatible_issues):
            print("TASKLOCAL_LANE_CONTRACT_SELFTEST=FAIL")
            print("- incompatible deliverable class passed strict validation")
            return 1

        weak_proof_batch = Path(tmp) / "weak_proof_batch"
        weak_proof_batch.mkdir()
        (weak_proof_batch / "BATCH_INDEX.txt").write_text(
            "\n".join(
                [
                    "Batch: weak_proof_batch",
                    "Lane roster:",
                    "1005 OWNER LANE_CLASS: LORE_CONTENT",
                    "DELIVERABLE_CLASS: CONTENT_ARTIFACT",
                    "VALID_COMPLETION: useful packet files plus proof",
                    "INVALID_COMPLETION: report-only",
                    "KILL_SWITCH: repeated same blocker",
                    "PROOF_ROUTE: final report summary",
                    "EVIDENCE_BUDGET: one export/import proof",
                    "Dependency guard: no same-wave sibling task output may be required; unverified outputs are CANDIDATE or BLOCKED.",
                ]
            ),
            encoding="utf-8",
        )
        (weak_proof_batch / "1005_LORE_OWNER.txt").write_text(
            "\n".join(
                [
                    "LANE_CLASS: LORE_CONTENT",
                    "DELIVERABLE_CLASS: CONTENT_ARTIFACT",
                    "VALID_COMPLETION: useful packet files plus proof",
                    "INVALID_COMPLETION: report-only",
                    "KILL_SWITCH: repeated same blocker",
                    "PROOF_ROUTE: final report summary",
                    "EVIDENCE_BUDGET: one export/import proof",
                ]
            ),
            encoding="utf-8",
        )
        weak_proof_issues, _ = check_batch(weak_proof_batch, strict=True)
        if not any("PROOF_ROUTE is report/status-only" in issue.message for issue in weak_proof_issues):
            print("TASKLOCAL_LANE_CONTRACT_SELFTEST=FAIL")
            print("- report-only proof route passed strict validation")
            return 1

        generic_lore_batch = Path(tmp) / "generic_lore_batch"
        generic_lore_batch.mkdir()
        (generic_lore_batch / "BATCH_INDEX.txt").write_text(
            "\n".join(
                [
                    "Batch: generic_lore_batch",
                    "Lane roster:",
                    "1006 OWNER LANE_CLASS: LORE_CONTENT",
                    "DELIVERABLE_CLASS: CONTENT_ARTIFACT",
                    "VALID_COMPLETION: useful packet files plus proof",
                    "INVALID_COMPLETION: report-only",
                    "KILL_SWITCH: repeated same blocker",
                    "PROOF_ROUTE: run packet audit and locale translation check",
                    "EVIDENCE_BUDGET: one export/import proof",
                    "Dependency guard: no same-wave sibling task output may be required; unverified outputs are CANDIDATE or BLOCKED.",
                ]
            ),
            encoding="utf-8",
        )
        (generic_lore_batch / "1006_LORE_OWNER.txt").write_text(
            "\n".join(
                [
                    "LANE_CLASS: LORE_CONTENT",
                    "DELIVERABLE_CLASS: CONTENT_ARTIFACT",
                    "VALID_COMPLETION: useful packet files plus proof",
                    "INVALID_COMPLETION: report-only",
                    "KILL_SWITCH: repeated same blocker",
                    "PROOF_ROUTE: run packet audit and locale translation check",
                    "EVIDENCE_BUDGET: one export/import proof",
                ]
            ),
            encoding="utf-8",
        )
        generic_lore_issues, _ = check_batch(generic_lore_batch, strict=True)
        if not any("lacks AppliedLore/Grand Library" in issue.message for issue in generic_lore_issues):
            print("TASKLOCAL_LANE_CONTRACT_SELFTEST=FAIL")
            print("- generic lore proof route passed strict validation")
            return 1
    print("TASKLOCAL_LANE_CONTRACT_SELFTEST=PASS")
    return 0


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("batches", nargs="*", help="taskslocal batch directories to validate")
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--strict", action="store_true", help="fail on missing lane contracts for files modified on or after the historical cutoff")
    mode.add_argument("--allow-legacy", action="store_true", help="warn on missing lane contracts")
    parser.add_argument("--strict-historical", action="store_true", help="with --strict, enforce historical files before the cutoff as errors too")
    parser.add_argument("--historical-cutoff", default="2026-06-01", help="UTC YYYY-MM-DD cutoff for historical task files; default: 2026-06-01")
    parser.add_argument("--self-test", action="store_true", help="run an internal positive fixture")
    return parser.parse_args(argv)


def main(argv: list[str]) -> int:
    args = parse_args(argv)
    if args.self_test:
        return run_self_test()
    try:
        historical_cutoff = datetime.strptime(args.historical_cutoff, "%Y-%m-%d").replace(tzinfo=timezone.utc)
    except ValueError:
        print("TASKLOCAL_LANE_CONTRACT_CHECK=USAGE")
        print("--historical-cutoff must use YYYY-MM-DD")
        return 2

    if not args.batches:
        print("TASKLOCAL_LANE_CONTRACT_CHECK=USAGE")
        print("Provide one or more taskslocal/<batch_name> paths.")
        print("Use --strict for new/reissued serious batches or --allow-legacy for historical inspection.")
        return 2

    strict = not args.allow_legacy
    all_issues: list[Issue] = []
    all_notes: list[str] = []

    for raw_batch in args.batches:
        batch = Path(raw_batch)
        if not batch.is_absolute():
            batch = ROOT / batch
        issues, notes = check_batch(
            batch,
            strict=strict,
            strict_historical=args.strict_historical,
            historical_cutoff=historical_cutoff,
        )
        all_issues.extend(issues)
        all_notes.extend(notes)

    for note in all_notes:
        print(note)

    if all_issues:
        has_errors = issue_list_has_errors(all_issues)
        status = "FAIL" if has_errors else "WARN"
        print(f"TASKLOCAL_LANE_CONTRACT_CHECK={status}")
        for issue in all_issues:
            print(f"- {format_issue(issue)}")
        return 1 if has_errors else 0

    print("TASKLOCAL_LANE_CONTRACT_CHECK=PASS")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
