#!/usr/bin/env python3
"""Static risk audit for HECTON-8 polish/portability mandates.

This tool is intentionally conservative: it reports broad source pressure
without pretending to prove runtime cost. Default exit is zero unless explicit
fail flags are supplied.
"""

from __future__ import annotations

import argparse
import json
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable


REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_SOURCE_ROOT = REPO_ROOT / "Assets" / "_Project" / "Scripts"
DEFAULT_REPORT_PATH = REPO_ROOT / "Docs" / "AgentLogs" / "PolishMandateStaticAudit_HFI_AUDIT.md"
DEFAULT_JSON_PATH = REPO_ROOT / "Docs" / "AgentLogs" / "PolishMandateStaticAudit_HFI_AUDIT.json"
SCHEMA = "hecton8.polish_mandate_static_audit.v1"

SKIP_DIR_NAMES = {
    ".git",
    ".vs",
    "__pycache__",
    "bin",
    "obj",
    "Library",
    "Temp",
}

LINE_PATTERNS: dict[str, re.Pattern[str]] = {
    "packOne": re.compile(r"\[StructLayout[^\]]*\bPack\s*=\s*1\b"),
    "privateNativeCollectionField": re.compile(
        r"^\s*private\s+(?:static\s+|readonly\s+|volatile\s+|unsafe\s+)*"
        r"Native(?:Array|List|HashMap|ParallelHashMap|Queue)\s*<"
    ),
    "jobHandleComplete": re.compile(r"\.Complete\s*\("),
    "unityUpdateMethod": re.compile(
        r"^\s*(?:private|protected|public|internal)?\s*"
        r"(?:void|async\s+void)\s+(?:Update|FixedUpdate|LateUpdate)\s*\("
    ),
    "unityRandom": re.compile(r"\bUnityEngine\.Random\b|\bRandom\.(?:Range|value|insideUnit)"),
    "unityTimeCritical": re.compile(r"\bTime\.(?:deltaTime|fixedDeltaTime|frameCount|time)\b"),
    "linqSurface": re.compile(r"\busing\s+System\.Linq\b|\.(?:Where|Select|Any|First|FirstOrDefault|ToList)\s*\("),
    "binaryHardwareSwitch": re.compile(
        r"\b(?:isLowEnd|IsLowEnd|LowEnd|HighEnd|UltraTier|QualityTier|HardwareTier|DeviceTier|"
        r"StandaloneQuest|QuestOnly|PcOnly)\b"
    ),
    "globalQualityWeight": re.compile(r"\bGlobalQualityWeight\b"),
    "noAlias": re.compile(r"\[NoAlias\]"),
}

BURST_ATTR_RE = re.compile(r"\[BurstCompile(?P<body>[^\]]*)\]", re.MULTILINE | re.DOTALL)
STRUCT_DECL_RE = re.compile(r"\b(?:public|private|internal|protected)?\s*(?:readonly\s+)?(?:partial\s+)?struct\s+\w+")
AUTO_PROPERTY_RE = re.compile(r"\{\s*get\s*;\s*(?:private\s+)?set\s*;")
BINARY_HARDWARE_CONTROL_RE = re.compile(r"^\s*(?:if|else\s+if|switch|case|return|while|for)\b|[?:]")
BINARY_HARDWARE_EXPLICIT_TOKEN_RE = re.compile(
    r"\b(?:isLowEnd|IsLowEnd|LowEnd|HighEnd|UltraTier|DeviceTier|StandaloneQuest|QuestOnly|PcOnly)\b"
)
BINARY_HARDWARE_TIER_TOKEN_RE = re.compile(r"\b(?:QualityTier|HardwareTier)\b")
BINARY_HARDWARE_TIER_COMPARISON_RE = re.compile(
    r"\b(?:QualityTier|HardwareTier)\b\s*(?<![=>])(?:==|!=|<=|>=|<|>)(?![=>])|"
    r"(?<![=>])(?:==|!=|<=|>=|<|>)(?![=>])\s*(?:\w+\.)*\b(?:QualityTier|HardwareTier)\b"
)
BINARY_HARDWARE_SWITCH_RE = re.compile(r"^\s*(?:switch|case)\b")
STRING_LITERAL_RE = re.compile(
    r"""
    (?:
        (?:\$?@|@\$)"(?:""|[^"])*"
        |
        \$?"(?:\\.|[^"\\])*"
    )
    """,
    re.VERBOSE,
)


@dataclass(frozen=True)
class Finding:
    path: str
    line: int
    text: str


def normalize_path(path: Path, repo_root: Path = REPO_ROOT) -> str:
    try:
        return path.resolve().relative_to(repo_root.resolve()).as_posix()
    except ValueError:
        return path.as_posix()


def should_skip(path: Path) -> bool:
    return any(part in SKIP_DIR_NAMES for part in path.parts)


def iter_cs_files(source_root: Path) -> list[Path]:
    return [
        path
        for path in sorted(source_root.rglob("*.cs"))
        if not should_skip(path.relative_to(source_root))
    ]


def strip_line_comment(line: str) -> str:
    return line.split("//", 1)[0]


def strip_string_literals(line: str) -> str:
    return STRING_LITERAL_RE.sub('""', line)


def is_binary_hardware_switch_line(scan_code: str) -> bool:
    if LINE_PATTERNS["binaryHardwareSwitch"].search(scan_code) is None:
        return False
    if BINARY_HARDWARE_SWITCH_RE.search(scan_code) is not None:
        return True
    if BINARY_HARDWARE_EXPLICIT_TOKEN_RE.search(scan_code) is not None:
        return BINARY_HARDWARE_CONTROL_RE.search(scan_code) is not None
    if BINARY_HARDWARE_TIER_TOKEN_RE.search(scan_code) is not None:
        return (
            BINARY_HARDWARE_CONTROL_RE.search(scan_code) is not None
            and BINARY_HARDWARE_TIER_COMPARISON_RE.search(scan_code) is not None
        )
    return False


def empty_results() -> dict[str, list[Finding]]:
    results: dict[str, list[Finding]] = {key: [] for key in LINE_PATTERNS}
    results.update(
        {
            "structAutoProperties": [],
            "burstCompile": [],
            "burstMissingCompileSynchronously": [],
            "burstMissingFloatMode": [],
            "burstMissingFloatPrecision": [],
        }
    )
    return results


def record_burst_attribute(
    results: dict[str, list[Finding]],
    rel: str,
    line: int,
    attribute_text: str,
) -> None:
    finding = Finding(rel, line, attribute_text.replace("\n", " ").strip())
    results["burstCompile"].append(finding)
    if "CompileSynchronously" not in attribute_text:
        results["burstMissingCompileSynchronously"].append(finding)
    if "FloatMode" not in attribute_text:
        results["burstMissingFloatMode"].append(finding)
    if "FloatPrecision" not in attribute_text:
        results["burstMissingFloatPrecision"].append(finding)


def record_line_patterns(
    results: dict[str, list[Finding]],
    rel: str,
    line_number: int,
    raw_line: str,
    code: str,
) -> None:
    scan_code = strip_string_literals(code)
    checks = (
        ("packOne", "Pack"),
        ("privateNativeCollectionField", "Native"),
        ("jobHandleComplete", ".Complete"),
        ("unityUpdateMethod", "Update"),
        ("unityRandom", "Random"),
        ("unityTimeCritical", "Time."),
        ("linqSurface", "Linq" if "Linq" in code else "."),
        ("globalQualityWeight", "GlobalQualityWeight"),
        ("noAlias", "[NoAlias]"),
    )
    for key, token in checks:
        if token in scan_code and LINE_PATTERNS[key].search(scan_code):
            results[key].append(Finding(rel, line_number, raw_line.strip()))

    if is_binary_hardware_switch_line(scan_code):
        results["binaryHardwareSwitch"].append(Finding(rel, line_number, raw_line.strip()))


def scan_all(files: Iterable[Path]) -> dict[str, list[Finding]]:
    results = empty_results()
    for path in files:
        rel = normalize_path(path)
        lines = path.read_text(encoding="utf-8", errors="ignore").splitlines()
        in_struct = False
        depth = 0
        line_count = len(lines)
        line_number = 1
        while line_number <= line_count:
            raw_line = lines[line_number - 1]
            code = strip_line_comment(raw_line)
            scan_code = strip_string_literals(code)

            record_line_patterns(results, rel, line_number, raw_line, code)

            if "[BurstCompile" in scan_code:
                attr_parts = [scan_code.strip()]
                cursor = line_number
                while "]" not in attr_parts[-1] and cursor < line_count and cursor < line_number + 8:
                    cursor += 1
                    attr_parts.append(strip_string_literals(strip_line_comment(lines[cursor - 1])).strip())
                record_burst_attribute(results, rel, line_number, " ".join(attr_parts))

            if not in_struct and STRUCT_DECL_RE.search(scan_code):
                in_struct = True
                depth = 0

            if in_struct and AUTO_PROPERTY_RE.search(scan_code):
                results["structAutoProperties"].append(Finding(rel, line_number, raw_line.strip()))

            if in_struct:
                depth += scan_code.count("{") - scan_code.count("}")
                if depth <= 0 and "}" in scan_code:
                    in_struct = False
                    depth = 0

            line_number += 1

    return results


def scan_lines(files: Iterable[Path]) -> dict[str, list[Finding]]:
    return {key: value for key, value in scan_all(files).items() if key in LINE_PATTERNS}


def scan_burst(files: Iterable[Path]) -> dict[str, list[Finding]]:
    results = scan_all(files)
    # Kept for compatibility with older tests; build_payload uses scan_all.
    return {
        key: results[key]
        for key in (
            "burstCompile",
            "burstMissingCompileSynchronously",
            "burstMissingFloatMode",
            "burstMissingFloatPrecision",
        )
    }


def scan_struct_properties(files: Iterable[Path]) -> list[Finding]:
    findings: list[Finding] = []
    for path in files:
        rel = normalize_path(path)
        lines = path.read_text(encoding="utf-8", errors="ignore").splitlines()
        in_struct = False
        depth = 0
        for line_number, raw_line in enumerate(lines, 1):
            code = strip_line_comment(raw_line)
            if not in_struct and STRUCT_DECL_RE.search(code):
                in_struct = True
                depth = 0

            if in_struct and AUTO_PROPERTY_RE.search(code):
                findings.append(Finding(rel, line_number, raw_line.strip()))

            if in_struct:
                depth += code.count("{") - code.count("}")
                if depth <= 0 and "}" in code:
                    in_struct = False
                    depth = 0
    return findings


def summarize_findings(findings: list[Finding]) -> dict[str, object]:
    by_file: dict[str, int] = {}
    for finding in findings:
        by_file[finding.path] = by_file.get(finding.path, 0) + 1
    top_files = [
        {"path": path, "count": count}
        for path, count in sorted(by_file.items(), key=lambda item: (-item[1], item[0]))[:10]
    ]
    examples = [
        {"path": f.path, "line": f.line, "text": f.text}
        for f in findings[:20]
    ]
    return {
        "matches": len(findings),
        "files": len(by_file),
        "topFiles": top_files,
        "examples": examples,
    }


def build_payload(source_root: Path) -> dict[str, object]:
    files = iter_cs_files(source_root)
    all_findings = scan_all(files)
    categories: dict[str, dict[str, object]] = {}
    for key, findings in all_findings.items():
        categories[key] = summarize_findings(findings)

    return {
        "schema": SCHEMA,
        "sourceRoot": normalize_path(source_root),
        "csFileCount": len(files),
        "categories": categories,
    }


def write_json(path: Path, payload: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def write_markdown(path: Path, payload: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    categories = payload["categories"]
    if not isinstance(categories, dict):
        raise TypeError("categories payload malformed")

    lines = [
        "# Polish Mandate Static Audit",
        "",
        "Evidence class: STATIC_SOURCE. No Unity import, compile, Play Mode, profiler, GC, memory, player build, or device proof was executed.",
        "",
        f"- Schema: `{payload['schema']}`",
        f"- Source root: `{payload['sourceRoot']}`",
        f"- C# files: `{payload['csFileCount']}`",
        "",
        "## Counts",
        "",
        "| Category | Matches | Files |",
        "|---|---:|---:|",
    ]

    for key in sorted(categories):
        item = categories[key]
        if not isinstance(item, dict):
            continue
        lines.append(f"| `{key}` | {item.get('matches', 0)} | {item.get('files', 0)} |")

    lines.extend(["", "## Top Files", ""])
    for key in sorted(categories):
        item = categories[key]
        if not isinstance(item, dict):
            continue
        top_files = item.get("topFiles") or []
        if not top_files:
            continue
        lines.append(f"### {key}")
        lines.append("")
        lines.append("| Path | Count |")
        lines.append("|---|---:|")
        for top in top_files[:5]:
            if isinstance(top, dict):
                lines.append(f"| `{top.get('path')}` | {top.get('count')} |")
        lines.append("")

    lines.extend(
        [
            "## Interpretation",
            "",
            "- `Pack=1`, private persistent native collections, and Burst attribute drift are platform-portability risks until each hit is classified as cold file-format, owner-local scratch, or hot runtime.",
            "- `jobHandleComplete`, Unity `Update` methods, `Time.*`, and `UnityEngine.Random` are not automatically defects, but they are mandatory review surfaces for gameplay/runtime code.",
            "- Binary hardware switches are suspect unless they are presentation-only or build-time/platform setup. Runtime scalability should flow through continuous `GlobalQualityWeight` curves.",
            "- This audit is a pressure map. It does not mutate code and does not prove frame cost.",
            "",
        ]
    )
    path.write_text("\n".join(lines), encoding="utf-8")


def hard_failures(payload: dict[str, object], args: argparse.Namespace) -> list[str]:
    categories = payload["categories"]
    if not isinstance(categories, dict):
        raise TypeError("categories payload malformed")

    failures: list[str] = []
    if args.fail_on_pack_one and int(categories["packOne"]["matches"]) > 0:
        failures.append(f"Pack=1 hits: {categories['packOne']['matches']}")
    if args.fail_on_missing_burst_flags:
        for key in ("burstMissingCompileSynchronously", "burstMissingFloatMode", "burstMissingFloatPrecision"):
            if int(categories[key]["matches"]) > 0:
                failures.append(f"{key}: {categories[key]['matches']}")
    return failures


def print_text(payload: dict[str, object], failures: list[str]) -> None:
    categories = payload["categories"]
    print("Polish mandate static audit")
    print(f"schema={payload['schema']}")
    print(f"sourceRoot={payload['sourceRoot']}")
    print(f"csFiles={payload['csFileCount']}")
    if isinstance(categories, dict):
        for key in sorted(categories):
            item = categories[key]
            if isinstance(item, dict):
                print(f"{key}={item['matches']} files={item['files']}")
    if failures:
        print("status=FAIL")
        for failure in failures:
            print(f"failure={failure}")
    else:
        print("status=PASS_WITH_WARNINGS")


def run(args: argparse.Namespace) -> int:
    payload = build_payload(Path(args.source_root))
    write_json(Path(args.json_path), payload)
    write_markdown(Path(args.report_path), payload)
    failures = hard_failures(payload, args)
    if args.json:
        print(json.dumps(payload, indent=2, sort_keys=True))
    else:
        print_text(payload, failures)
    return 1 if failures else 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", default=str(DEFAULT_SOURCE_ROOT))
    parser.add_argument("--report-path", default=str(DEFAULT_REPORT_PATH))
    parser.add_argument("--json-path", default=str(DEFAULT_JSON_PATH))
    parser.add_argument("--json", action="store_true", help="Print JSON payload to stdout.")
    parser.add_argument("--fail-on-pack-one", action="store_true")
    parser.add_argument("--fail-on-missing-burst-flags", action="store_true")
    return run(parser.parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
