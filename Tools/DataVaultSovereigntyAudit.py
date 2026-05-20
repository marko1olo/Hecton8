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
AUDIT_SCHEMA = "hecton8.datavault_sovereignty_audit.v2"
BASELINE_SCHEMA = "hecton8.datavault_sovereignty_baseline.v2"
REPORT_SCHEMA = "hecton8.datavault_sovereignty_audit_report.v1"
NATIVE_ARRAY_CONSTRUCTOR_RE = re.compile(r"\bnew\s+NativeArray\s*<")
NATIVE_ARRAY_DECLARATION_RE = re.compile(
    r"^\s*(?:\[[^\]]+\]\s*)*"
    r"(?:(?:public|private|protected|internal|static|readonly|volatile|unsafe|new)\s+)+"
    r"NativeArray\s*<[^>;]+>\s+[A-Za-z_]\w*(?:\s*;|\s*,|\s*=\s*(?!>))"
)
DEFAULT_ALLOWED_PATH_SUFFIXES = (
    "Assets/_Project/Scripts/Core/Memory/H8Memory.cs",
)
DEFAULT_ALLOWED_DECLARATION_PATH_SUFFIXES = (
    "Assets/_Project/Scripts/Core/Memory/H8Memory.cs",
    "Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs",
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


def strip_line_comment(line: str) -> str:
    return line.split("//", 1)[0]


def scan_native_array_declaration_tree(
    source_root: Path,
    repo_root: Path = REPO_ROOT,
    allowed_suffixes: Sequence[str] = DEFAULT_ALLOWED_DECLARATION_PATH_SUFFIXES,
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
            if NATIVE_ARRAY_DECLARATION_RE.search(strip_line_comment(line)):
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


def scan_source_tree_with_declarations(
    source_root: Path,
    repo_root: Path = REPO_ROOT,
    constructor_allowed_suffixes: Sequence[str] = DEFAULT_ALLOWED_PATH_SUFFIXES,
    declaration_allowed_suffixes: Sequence[str] = DEFAULT_ALLOWED_DECLARATION_PATH_SUFFIXES,
) -> tuple[list[FileFinding], list[FileFinding]]:
    if not source_root.exists():
        raise FileNotFoundError(f"source root not found: {source_root}")

    constructor_findings: list[FileFinding] = []
    declaration_findings: list[FileFinding] = []
    for path in sorted(source_root.rglob("*.cs")):
        relative_scan_path = path.relative_to(source_root)
        if should_skip(relative_scan_path):
            continue

        constructor_lines: list[int] = []
        declaration_lines: list[int] = []
        try:
            lines = path.read_text(encoding="utf-8", errors="ignore").splitlines()
        except OSError as exc:
            raise OSError(f"failed to read {path}") from exc

        for line_number, line in enumerate(lines, 1):
            if NATIVE_ARRAY_CONSTRUCTOR_RE.search(line):
                constructor_lines.append(line_number)
            if NATIVE_ARRAY_DECLARATION_RE.search(strip_line_comment(line)):
                declaration_lines.append(line_number)

        if not constructor_lines and not declaration_lines:
            continue

        relative_path = normalize_path(path, repo_root)
        if constructor_lines:
            constructor_findings.append(
                FileFinding(
                    path=relative_path,
                    count=len(constructor_lines),
                    lines=tuple(constructor_lines),
                    allowed=is_allowed_path(relative_path, constructor_allowed_suffixes),
                )
            )
        if declaration_lines:
            declaration_findings.append(
                FileFinding(
                    path=relative_path,
                    count=len(declaration_lines),
                    lines=tuple(declaration_lines),
                    allowed=is_allowed_path(relative_path, declaration_allowed_suffixes),
                )
            )

    return constructor_findings, declaration_findings


def build_audit_payload(
    findings: Sequence[FileFinding],
    source_root: Path,
    repo_root: Path = REPO_ROOT,
    allowed_suffixes: Sequence[str] = DEFAULT_ALLOWED_PATH_SUFFIXES,
    declaration_findings: Sequence[FileFinding] | None = None,
    declaration_allowed_suffixes: Sequence[str] = DEFAULT_ALLOWED_DECLARATION_PATH_SUFFIXES,
) -> dict[str, Any]:
    total_direct = sum(finding.count for finding in findings)
    allowed_direct = sum(finding.count for finding in findings if finding.allowed)
    forbidden = [finding for finding in findings if not finding.allowed]
    forbidden_direct = sum(finding.count for finding in forbidden)
    declaration_findings = tuple(declaration_findings or ())
    total_declarations = sum(finding.count for finding in declaration_findings)
    allowed_declarations = sum(finding.count for finding in declaration_findings if finding.allowed)
    forbidden_declarations = [finding for finding in declaration_findings if not finding.allowed]
    forbidden_declaration_count = sum(finding.count for finding in forbidden_declarations)

    return {
        "schema": AUDIT_SCHEMA,
        "sourceRoot": normalize_path(source_root, repo_root),
        "pattern": NATIVE_ARRAY_CONSTRUCTOR_RE.pattern,
        "declarationPattern": NATIVE_ARRAY_DECLARATION_RE.pattern,
        "allowedPathSuffixes": list(allowed_suffixes),
        "declarationAllowedPathSuffixes": list(declaration_allowed_suffixes),
        "totalDirectConstructors": total_direct,
        "allowedDirectConstructors": allowed_direct,
        "forbiddenDirectConstructors": forbidden_direct,
        "forbiddenFileCount": len(forbidden),
        "totalNativeArrayDeclarations": total_declarations,
        "allowedNativeArrayDeclarations": allowed_declarations,
        "forbiddenNativeArrayDeclarations": forbidden_declaration_count,
        "declarationFileCount": len(forbidden_declarations),
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
        "declarationFindings": [
            {
                "path": finding.path,
                "count": finding.count,
                "lines": list(finding.lines),
                "allowed": finding.allowed,
            }
            for finding in declaration_findings
        ],
    }


def build_baseline(payload: dict[str, Any]) -> dict[str, Any]:
    forbidden_by_file: dict[str, int] = {}
    allowed_by_file: dict[str, int] = {}
    for finding in payload["findings"]:
        target = allowed_by_file if finding["allowed"] else forbidden_by_file
        target[finding["path"]] = int(finding["count"])

    forbidden_declarations_by_file: dict[str, int] = {}
    allowed_declarations_by_file: dict[str, int] = {}
    for finding in payload.get("declarationFindings", []):
        target = allowed_declarations_by_file if finding["allowed"] else forbidden_declarations_by_file
        target[finding["path"]] = int(finding["count"])

    return {
        "schema": BASELINE_SCHEMA,
        "sourceRoot": payload["sourceRoot"],
        "pattern": payload["pattern"],
        "declarationPattern": payload.get("declarationPattern", NATIVE_ARRAY_DECLARATION_RE.pattern),
        "totalDirectConstructors": payload["totalDirectConstructors"],
        "allowedDirectConstructors": payload["allowedDirectConstructors"],
        "forbiddenDirectConstructors": payload["forbiddenDirectConstructors"],
        "forbiddenFileCount": payload["forbiddenFileCount"],
        "totalNativeArrayDeclarations": payload.get("totalNativeArrayDeclarations", 0),
        "allowedNativeArrayDeclarations": payload.get("allowedNativeArrayDeclarations", 0),
        "forbiddenNativeArrayDeclarations": payload.get("forbiddenNativeArrayDeclarations", 0),
        "declarationFileCount": payload.get("declarationFileCount", 0),
        "allowedPathSuffixes": payload["allowedPathSuffixes"],
        "declarationAllowedPathSuffixes": payload.get(
            "declarationAllowedPathSuffixes",
            list(DEFAULT_ALLOWED_DECLARATION_PATH_SUFFIXES),
        ),
        "forbiddenByFile": dict(sorted(forbidden_by_file.items())),
        "allowedByFile": dict(sorted(allowed_by_file.items())),
        "forbiddenDeclarationsByFile": dict(sorted(forbidden_declarations_by_file.items())),
        "allowedDeclarationsByFile": dict(sorted(allowed_declarations_by_file.items())),
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


def forbidden_declarations_by_file(payload: dict[str, Any]) -> dict[str, int]:
    result: dict[str, int] = {}
    for finding in payload.get("declarationFindings", []):
        if finding["allowed"]:
            continue

        result[finding["path"]] = int(finding["count"])

    return result


def extract_domain(relative_path: str) -> str:
    normalized = relative_path.replace("\\", "/")
    prefix = "Assets/_Project/Scripts/"
    if not normalized.startswith(prefix):
        return "External"

    remainder = normalized[len(prefix) :]
    if "/" not in remainder:
        return "Root"

    return remainder.split("/", 1)[0] or "Root"


def collect_regression_details(
    payload: dict[str, Any],
    baseline: dict[str, Any] | None,
) -> tuple[list[str], list[dict[str, Any]]]:
    if baseline is None:
        return ["Baseline missing; no-regression gate fails closed."], []

    errors: list[str] = []
    details: list[dict[str, Any]] = []
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
            delta = count - baseline_count
            errors.append(
                f"{path}: forbidden direct constructors increased from {baseline_count} to {count}."
            )
            details.append(
                {
                    "kind": "directConstructor",
                    "domain": extract_domain(path),
                    "path": path,
                    "baseline": baseline_count,
                    "current": count,
                    "delta": delta,
                }
            )

    if "forbiddenNativeArrayDeclarations" in payload or "declarationFindings" in payload:
        current_declaration_total = int(payload.get("forbiddenNativeArrayDeclarations", 0))
        baseline_declaration_total = int(baseline.get("forbiddenNativeArrayDeclarations", -1))
        if current_declaration_total > baseline_declaration_total:
            errors.append(
                "Forbidden NativeArray field declarations increased "
                f"from {baseline_declaration_total} to {current_declaration_total}."
            )

        baseline_declarations_by_file = baseline.get("forbiddenDeclarationsByFile", {})
        if not isinstance(baseline_declarations_by_file, dict):
            errors.append("Baseline forbiddenDeclarationsByFile is missing or invalid.")
            baseline_declarations_by_file = {}

        for path, count in sorted(forbidden_declarations_by_file(payload).items()):
            baseline_count = int(baseline_declarations_by_file.get(path, 0))
            if count > baseline_count:
                delta = count - baseline_count
                errors.append(
                    f"{path}: forbidden NativeArray field declarations increased from {baseline_count} to {count}."
                )
                details.append(
                    {
                        "kind": "fieldDeclaration",
                        "domain": extract_domain(path),
                        "path": path,
                        "baseline": baseline_count,
                        "current": count,
                        "delta": delta,
                    }
                )

    return errors, details


def detect_regressions(payload: dict[str, Any], baseline: dict[str, Any] | None) -> list[str]:
    errors, _ = collect_regression_details(payload, baseline)
    return errors


def aggregate_regression_details(details: Sequence[dict[str, Any]]) -> list[dict[str, Any]]:
    aggregate: dict[str, dict[str, Any]] = {}
    for detail in details:
        domain = str(detail.get("domain", "Unknown"))
        entry = aggregate.setdefault(
            domain,
            {
                "domain": domain,
                "delta": 0,
                "directConstructorDelta": 0,
                "fieldDeclarationDelta": 0,
                "fileCount": 0,
                "files": set(),
            },
        )
        delta = int(detail.get("delta", 0))
        entry["delta"] += delta
        if detail.get("kind") == "directConstructor":
            entry["directConstructorDelta"] += delta
        elif detail.get("kind") == "fieldDeclaration":
            entry["fieldDeclarationDelta"] += delta
        path = str(detail.get("path", ""))
        if path:
            entry["files"].add(path)

    rows: list[dict[str, Any]] = []
    for entry in aggregate.values():
        files = sorted(entry["files"])
        rows.append(
            {
                "domain": entry["domain"],
                "delta": entry["delta"],
                "directConstructorDelta": entry["directConstructorDelta"],
                "fieldDeclarationDelta": entry["fieldDeclarationDelta"],
                "fileCount": len(files),
                "files": files,
            }
        )

    return sorted(rows, key=lambda item: (-int(item["delta"]), str(item["domain"])))


def build_report_payload(
    payload: dict[str, Any],
    baseline_path: Path,
    baseline: dict[str, Any] | None,
    regression_errors: Sequence[str],
    regression_details: Sequence[dict[str, Any]],
) -> dict[str, Any]:
    return {
        "schema": REPORT_SCHEMA,
        "audit": payload,
        "baselinePath": normalize_path(baseline_path),
        "baselineSchema": None if baseline is None else baseline.get("schema"),
        "regressionErrors": list(regression_errors),
        "regressionDetails": list(regression_details),
        "regressionByDomain": aggregate_regression_details(regression_details),
    }


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
    regression_details: Sequence[dict[str, Any]],
    top_limit: int,
) -> None:
    status = "PASS_NO_REGRESSION_WITH_LEGACY_DEBT"
    if baseline is None:
        status = "BLOCKED_BASELINE_MISSING"
    elif regression_errors:
        status = "FAIL_REGRESSION"
    elif (
        int(payload["forbiddenDirectConstructors"]) == 0
        and int(payload.get("forbiddenNativeArrayDeclarations", 0)) == 0
    ):
        status = "PASS_ZERO_FORBIDDEN_NATIVEARRAY_DEBT"

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
        f"| Total field-like `NativeArray<T>` declarations | {payload.get('totalNativeArrayDeclarations', 0)} |",
        f"| Allowed DataVault/H8Memory declarations | {payload.get('allowedNativeArrayDeclarations', 0)} |",
        f"| Forbidden system declarations | {payload.get('forbiddenNativeArrayDeclarations', 0)} |",
        f"| Files with forbidden declarations | {payload.get('declarationFileCount', 0)} |",
        "",
    ]

    if regression_errors:
        lines.extend(["## Regression Findings", ""])
        for error in regression_errors:
            lines.append(f"- {error}")
        lines.append("")

    domain_regressions = aggregate_regression_details(regression_details)
    if domain_regressions:
        lines.extend(
            [
                "## Regression Delta By Domain",
                "",
                "| Domain | Delta | Direct constructor delta | Field declaration delta | Files |",
                "|---|---:|---:|---:|---:|",
            ]
        )
        for item in domain_regressions:
            lines.append(
                f"| `{item['domain']}` | {item['delta']} | "
                f"{item['directConstructorDelta']} | {item['fieldDeclarationDelta']} | "
                f"{item['fileCount']} |"
            )
        lines.append("")

        lines.extend(
            [
                "## Regression Delta Details",
                "",
                "| Kind | Domain | Baseline | Current | Delta | Path |",
                "|---|---|---:|---:|---:|---|",
            ]
        )
        for item in sorted(
            regression_details,
            key=lambda detail: (
                -int(detail.get("delta", 0)),
                str(detail.get("domain", "")),
                str(detail.get("path", "")),
            ),
        ):
            lines.append(
                f"| `{item['kind']}` | `{item['domain']}` | "
                f"{item['baseline']} | {item['current']} | {item['delta']} | "
                f"`{item['path']}` |"
            )
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
            f"## Top {top_limit} Forbidden Declaration Files",
            "",
            "| Count | Path | Lines |",
            "|---:|---|---|",
        ]
    )
    for finding in top_findings(
        {"findings": payload.get("declarationFindings", [])},
        allowed=False,
        limit=top_limit,
    ):
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
            "## Allowed DataVault/H8Memory Declaration Sites",
            "",
            "| Count | Path | Lines |",
            "|---:|---|---|",
        ]
    )
    allowed_declaration_findings = top_findings(
        {"findings": payload.get("declarationFindings", [])},
        allowed=True,
        limit=top_limit,
    )
    if allowed_declaration_findings:
        for finding in allowed_declaration_findings:
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
            "`--fail-on-regression` blocks any new or increased forbidden constructor or field-declaration count against the baseline.",
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
    parser.add_argument("--audit-json", type=Path, default=None, help="Optional JSON report path.")
    parser.add_argument("--write-baseline", action="store_true", help="Overwrite the baseline with the current audit.")
    parser.add_argument("--fail-on-regression", action="store_true", help="Exit nonzero if forbidden constructor debt increases.")
    parser.add_argument("--fail-on-any", action="store_true", help="Exit nonzero if any forbidden constructors remain.")
    parser.add_argument("--no-report", action="store_true", help="Do not write the Markdown report.")
    parser.add_argument("--top", type=int, default=40, help="Number of findings to include in the report.")
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(sys.argv[1:] if argv is None else argv)
    findings, declaration_findings = scan_source_tree_with_declarations(args.root)
    payload = build_audit_payload(findings, args.root, declaration_findings=declaration_findings)
    baseline = load_json(args.baseline)

    if args.write_baseline:
        baseline = build_baseline(payload)
        write_json(args.baseline, baseline)

    if args.fail_on_regression:
        regression_errors, regression_details = collect_regression_details(payload, baseline)
    else:
        regression_errors, regression_details = [], []
    if not args.no_report:
        write_markdown_report(
            args.report,
            payload,
            args.baseline,
            baseline,
            regression_errors,
            regression_details,
            max(args.top, 1),
        )
    if args.audit_json is not None:
        write_json(
            args.audit_json,
            build_report_payload(payload, args.baseline, baseline, regression_errors, regression_details),
        )

    failure_reasons = list(regression_errors)
    if args.fail_on_any and int(payload["forbiddenDirectConstructors"]) > 0:
        failure_reasons.append(
            f"{payload['forbiddenDirectConstructors']} forbidden direct NativeArray constructors remain."
        )
    if args.fail_on_any and int(payload.get("forbiddenNativeArrayDeclarations", 0)) > 0:
        failure_reasons.append(
            f"{payload['forbiddenNativeArrayDeclarations']} forbidden NativeArray field declarations remain."
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
        f"files={payload['forbiddenFileCount']}, "
        f"declarations={payload.get('totalNativeArrayDeclarations', 0)}, "
        f"forbiddenDeclarations={payload.get('forbiddenNativeArrayDeclarations', 0)}, "
        f"declarationFiles={payload.get('declarationFileCount', 0)}"
    )
    for reason in failure_reasons:
        print(f"ERROR: {reason}", file=sys.stderr)

    return 1 if failure_reasons else 0


if __name__ == "__main__":
    raise SystemExit(main())
