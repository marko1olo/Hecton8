#!/usr/bin/env python3
"""Audit direct NativeArray constructors against DataVault sovereignty."""

from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable, Sequence


REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_SOURCE_ROOT = REPO_ROOT / "Assets" / "_Project" / "Scripts"
DEFAULT_BASELINE_PATH = (
    REPO_ROOT
    / "Docs"
    / "AgentLogs"
    / "DataVaultSovereigntyBaseline_VAULT_SOVEREIGNTY_ENFORCER.json"
)
DEFAULT_REPORT_PATH = (
    REPO_ROOT
    / "Docs"
    / "AgentLogs"
    / "DataVaultSovereigntyAudit_VAULT_SOVEREIGNTY_ENFORCER.md"
)
AUDIT_SCHEMA = "hecton8.datavault_sovereignty_audit.v1"
BASELINE_SCHEMA = "hecton8.datavault_sovereignty_baseline.v1"
NATIVE_ARRAY_CONSTRUCTOR_RE = re.compile(r"\bnew\s+NativeArray\s*<")
DEFAULT_ALLOWED_PATH_SUFFIXES = (
    "Assets/_Project/Scripts/Core/Memory/H8Memory.cs",
)
SKIP_DIR_NAMES = {
    ".git",
    ".vs",
    "__pycache__",
    "bin",
    "obj",
    "Library",
    "Temp",
}


@dataclass(frozen=True)
class FileFinding:
    path: str
    count: int
    lines: tuple[int, ...]
    allowed: bool


def normalize_path(path: Path, repo_root: Path = REPO_ROOT) -> str:
    try:
        relative = path.resolve().relative_to(repo_root.resolve())
    except ValueError:
        relative = path

    return relative.as_posix()


def is_allowed_path(relative_path: str, allowed_suffixes: Sequence[str]) -> bool:
    normalized = relative_path.replace("\\", "/")
    for suffix in allowed_suffixes:
        if normalized.endswith(suffix.replace("\\", "/")):
            return True

    return False


def should_skip(path: Path) -> bool:
    return any(part in SKIP_DIR_NAMES for part in path.parts)


def scan_source_tree(
    source_root: Path,
    repo_root: Path = REPO_ROOT,
    allowed_suffixes: Sequence[str] = DEFAULT_ALLOWED_PATH_SUFFIXES,
) -> list[FileFinding]:
    if not source_root.exists():
        raise FileNotFoundError(f"source root not found: {source_root}")

    findings: list[FileFinding] = []
    for path in sorted(source_root.rglob("*.cs")):
        relative_scan_path = path.relative_to(source_root)
        if should_skip(relative_scan_path):
            continue

        line_numbers: list[int] = []
        try:
            lines = path.read_text(encoding="utf-8", errors="ignore").splitlines()
        except OSError as exc:
            raise OSError(f"failed to read {path}") from exc

        for line_number, line in enumerate(lines, 1):
            if NATIVE_ARRAY_CONSTRUCTOR_RE.search(line):
                line_numbers.append(line_number)

        if not line_numbers:
            continue

        relative_path = normalize_path(path, repo_root)
        findings.append(
            FileFinding(
                path=relative_path,
                count=len(line_numbers),
                lines=tuple(line_numbers),
                allowed=is_allowed_path(relative_path, allowed_suffixes),
            )
        )

    return findings


def build_audit_payload(
    findings: Sequence[FileFinding],
    source_root: Path,
    repo_root: Path = REPO_ROOT,
    allowed_suffixes: Sequence[str] = DEFAULT_ALLOWED_PATH_SUFFIXES,
) -> dict[str, Any]:
    total_direct = sum(finding.count for finding in findings)
    allowed_direct = sum(finding.count for finding in findings if finding.allowed)
    forbidden = [finding for finding in findings if not finding.allowed]
    forbidden_direct = sum(finding.count for finding in forbidden)

    return {
        "schema": AUDIT_SCHEMA,
        "sourceRoot": normalize_path(source_root, repo_root),
        "pattern": NATIVE_ARRAY_CONSTRUCTOR_RE.pattern,
        "allowedPathSuffixes": list(allowed_suffixes),
        "totalDirectConstructors": total_direct,
        "allowedDirectConstructors": allowed_direct,
        "forbiddenDirectConstructors": forbidden_direct,
        "forbiddenFileCount": len(forbidden),
        "findingCount": len(findings),
        "findings": [
            {
                "path": finding.path,
                "count": finding.count,
                "lines": list(finding.lines),
                "allowed": finding.allowed,
            }
            for finding in findings
        ],
    }


def build_baseline(payload: dict[str, Any]) -> dict[str, Any]:
    forbidden_by_file: dict[str, int] = {}
    allowed_by_file: dict[str, int] = {}
    for finding in payload["findings"]:
        target = allowed_by_file if finding["allowed"] else forbidden_by_file
        target[finding["path"]] = int(finding["count"])

    return {
        "schema": BASELINE_SCHEMA,
        "sourceRoot": payload["sourceRoot"],
        "pattern": payload["pattern"],
        "totalDirectConstructors": payload["totalDirectConstructors"],
        "allowedDirectConstructors": payload["allowedDirectConstructors"],
        "forbiddenDirectConstructors": payload["forbiddenDirectConstructors"],
        "forbiddenFileCount": payload["forbiddenFileCount"],
        "allowedPathSuffixes": payload["allowedPathSuffixes"],
        "forbiddenByFile": dict(sorted(forbidden_by_file.items())),
        "allowedByFile": dict(sorted(allowed_by_file.items())),
    }


def load_json(path: Path) -> dict[str, Any] | None:
    if not path.exists():
        return None

    with path.open("r", encoding="utf-8") as handle:
        data = json.load(handle)

    if not isinstance(data, dict):
        raise ValueError(f"baseline is not a JSON object: {path}")

    return data


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def forbidden_by_file(payload: dict[str, Any]) -> dict[str, int]:
    result: dict[str, int] = {}
    for finding in payload["findings"]:
        if finding["allowed"]:
            continue

        result[finding["path"]] = int(finding["count"])

    return result


def detect_regressions(payload: dict[str, Any], baseline: dict[str, Any] | None) -> list[str]:
    if baseline is None:
        return ["Baseline missing; no-regression gate fails closed."]

    errors: list[str] = []
    if baseline.get("schema") != BASELINE_SCHEMA:
        errors.append(f"Baseline schema mismatch: {baseline.get('schema')!r}.")

    current_total = int(payload["forbiddenDirectConstructors"])
    baseline_total = int(baseline.get("forbiddenDirectConstructors", -1))
    if current_total > baseline_total:
        errors.append(
            "Forbidden direct NativeArray constructors increased "
            f"from {baseline_total} to {current_total}."
        )

    baseline_by_file = baseline.get("forbiddenByFile", {})
    if not isinstance(baseline_by_file, dict):
        errors.append("Baseline forbiddenByFile is missing or invalid.")
        baseline_by_file = {}

    for path, count in sorted(forbidden_by_file(payload).items()):
        baseline_count = int(baseline_by_file.get(path, 0))
        if count > baseline_count:
            errors.append(
                f"{path}: forbidden direct constructors increased from {baseline_count} to {count}."
            )

    return errors


def top_findings(payload: dict[str, Any], allowed: bool, limit: int) -> list[dict[str, Any]]:
    findings = [
        finding
        for finding in payload["findings"]
        if bool(finding["allowed"]) == allowed
    ]
    return sorted(findings, key=lambda item: (-int(item["count"]), str(item["path"])))[:limit]


def format_line_samples(lines: Iterable[int], limit: int = 8) -> str:
    values = list(lines)
    sample = ", ".join(str(line) for line in values[:limit])
    if len(values) > limit:
        sample += ", ..."

    return sample


def write_markdown_report(
    path: Path,
    payload: dict[str, Any],
    baseline_path: Path,
    baseline: dict[str, Any] | None,
    regression_errors: Sequence[str],
    top_limit: int,
) -> None:
    status = "PASS_NO_REGRESSION_WITH_LEGACY_DEBT"
    if baseline is None:
        status = "BLOCKED_BASELINE_MISSING"
    elif regression_errors:
        status = "FAIL_REGRESSION"
    elif int(payload["forbiddenDirectConstructors"]) == 0:
        status = "PASS_ZERO_FORBIDDEN_DIRECT_CONSTRUCTORS"

    lines: list[str] = [
        "# DataVault Sovereignty Audit - VAULT_SOVEREIGNTY_ENFORCER",
        "",
        f"Schema: `{AUDIT_SCHEMA}`",
        f"Status: `{status}`",
        f"Source root: `{payload['sourceRoot']}`",
        f"Pattern: `{payload['pattern']}`",
        f"Baseline: `{normalize_path(baseline_path)}`",
        "",
        "## Summary",
        "",
        "| Metric | Count |",
        "|---|---:|",
        f"| Total direct `new NativeArray<T>` constructors | {payload['totalDirectConstructors']} |",
        f"| Allowed allocator-internal constructors | {payload['allowedDirectConstructors']} |",
        f"| Forbidden system constructors | {payload['forbiddenDirectConstructors']} |",
        f"| Files with forbidden constructors | {payload['forbiddenFileCount']} |",
        "",
    ]

    if regression_errors:
        lines.extend(["## Regression Findings", ""])
        for error in regression_errors:
            lines.append(f"- {error}")
        lines.append("")

    lines.extend(
        [
            f"## Top {top_limit} Forbidden Files",
            "",
            "| Count | Path | Lines |",
            "|---:|---|---|",
        ]
    )
    for finding in top_findings(payload, allowed=False, limit=top_limit):
        lines.append(
            f"| {finding['count']} | `{finding['path']}` | {format_line_samples(finding['lines'])} |"
        )

    lines.extend(
        [
            "",
            "## Allowed Allocator-Internal Sites",
            "",
            "| Count | Path | Lines |",
            "|---:|---|---|",
        ]
    )
    allowed_findings = top_findings(payload, allowed=True, limit=top_limit)
    if allowed_findings:
        for finding in allowed_findings:
            lines.append(
                f"| {finding['count']} | `{finding['path']}` | {format_line_samples(finding['lines'])} |"
            )
    else:
        lines.append("| 0 | none | |")

    lines.extend(
        [
            "",
            "## Gate Commands",
            "",
            "```powershell",
            "python Tools\\DataVaultSovereigntyAudit.py --fail-on-regression",
            "python Tools\\DataVaultSovereigntyAudit.py --fail-on-any",
            "```",
            "",
            "`--fail-on-regression` blocks any new or increased forbidden constructor count against the baseline.",
            "`--fail-on-any` is the final zero-debt gate and currently fails until all legacy debt is migrated.",
            "",
        ]
    )

    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines), encoding="utf-8")


def parse_args(argv: Sequence[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=DEFAULT_SOURCE_ROOT, help="Source tree to scan.")
    parser.add_argument("--baseline", type=Path, default=DEFAULT_BASELINE_PATH, help="No-regression baseline JSON.")
    parser.add_argument("--report", type=Path, default=DEFAULT_REPORT_PATH, help="Markdown report path.")
    parser.add_argument("--write-baseline", action="store_true", help="Overwrite the baseline with the current audit.")
    parser.add_argument("--fail-on-regression", action="store_true", help="Exit nonzero if forbidden constructor debt increases.")
    parser.add_argument("--fail-on-any", action="store_true", help="Exit nonzero if any forbidden constructors remain.")
    parser.add_argument("--no-report", action="store_true", help="Do not write the Markdown report.")
    parser.add_argument("--top", type=int, default=40, help="Number of findings to include in the report.")
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(sys.argv[1:] if argv is None else argv)
    findings = scan_source_tree(args.root)
    payload = build_audit_payload(findings, args.root)
    baseline = load_json(args.baseline)

    if args.write_baseline:
        baseline = build_baseline(payload)
        write_json(args.baseline, baseline)

    regression_errors = detect_regressions(payload, baseline) if args.fail_on_regression else []
    if not args.no_report:
        write_markdown_report(args.report, payload, args.baseline, baseline, regression_errors, max(args.top, 1))

    failure_reasons = list(regression_errors)
    if args.fail_on_any and int(payload["forbiddenDirectConstructors"]) > 0:
        failure_reasons.append(
            f"{payload['forbiddenDirectConstructors']} forbidden direct NativeArray constructors remain."
        )

    status = "PASS"
    if failure_reasons:
        status = "FAIL"

    print(
        "DataVault sovereignty audit: "
        f"status={status}, "
        f"direct={payload['totalDirectConstructors']}, "
        f"allowed={payload['allowedDirectConstructors']}, "
        f"forbidden={payload['forbiddenDirectConstructors']}, "
        f"files={payload['forbiddenFileCount']}"
    )
    for reason in failure_reasons:
        print(f"ERROR: {reason}", file=sys.stderr)

    return 1 if failure_reasons else 0


if __name__ == "__main__":
    raise SystemExit(main())
