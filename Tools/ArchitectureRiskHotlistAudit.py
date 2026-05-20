#!/usr/bin/env python3
"""Prioritized static hotlist for HECTON-8 global architecture risk.

Evidence class: STATIC_SOURCE. This tool ranks files by review pressure across
global authority, DataVault migration, job barriers, platform-portability, and
hot-path hygiene. It does not mutate runtime code and does not prove frame cost.
"""

from __future__ import annotations

import argparse
import json
import re
from dataclasses import dataclass
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_SOURCE_ROOT = REPO_ROOT / "Assets" / "_Project" / "Scripts"
DEFAULT_REPORT_PATH = REPO_ROOT / "Docs" / "AgentLogs" / "ArchitectureRiskHotlist_HFI_AUDIT.md"
DEFAULT_JSON_PATH = REPO_ROOT / "Docs" / "AgentLogs" / "ArchitectureRiskHotlist_HFI_AUDIT.json"
SCHEMA = "hecton8.architecture_risk_hotlist.v2"

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
class Category:
    pattern: re.Pattern[str]
    score: int
    family: str
    token_hint: tuple[str, ...]


CATEGORIES: dict[str, Category] = {
    "globalRegistryDot": Category(re.compile(r"GlobalRegistry\."), 1, "authority", ("GlobalRegistry.",)),
    "globalRegistryGenericGet": Category(
        re.compile(r"GlobalRegistry\.(?:Get|TryGet)\s*<"), 100, "authority", ("GlobalRegistry.Get", "GlobalRegistry.TryGet")
    ),
    "signalBusPushTryPush": Category(
        re.compile(r"SignalBus\s*<[^>]+>\s*\.\s*(?:Push|TryPush)\b"), 2, "signals", ("SignalBus",)
    ),
    "globalSignalsPublish": Category(re.compile(r"GlobalSignals\.Publish\b"), 12, "signals", ("GlobalSignals.Publish",)),
    "hectonEventBusPubSub": Category(
        re.compile(r"HectonEventBus\.(?:Publish|Subscribe)\b"), 18, "signals", ("HectonEventBus.",)
    ),
    "localNumericBufferCast": Category(
        re.compile(r"\(\s*BufferID\s*\)\s*-?(?:0x[0-9A-Fa-f_]+|\d[\d_]*)"),
        10,
        "datavault",
        ("BufferID",),
    ),
    "nativeArrayCtor": Category(re.compile(r"\bnew\s+NativeArray\s*<"), 8, "datavault", ("new NativeArray",)),
    "privateNativeCollectionField": Category(
        re.compile(
            r"^\s*private\s+(?:static\s+|readonly\s+|volatile\s+|unsafe\s+)*"
            r"Native(?:Array|List|HashMap|ParallelHashMap|Queue)\s*<"
        ),
        12,
        "datavault",
        ("private Native", "private static Native", "private readonly Native", "private volatile Native", "private unsafe Native"),
    ),
    "jobHandleComplete": Category(re.compile(r"\.Complete\s*\("), 12, "jobs", (".Complete",)),
    "unityUpdateMethod": Category(
        re.compile(r"^\s*(?:private|protected|public|internal)?\s*(?:void|async\s+void)\s+(?:Update|FixedUpdate|LateUpdate)\s*\("),
        10,
        "hotpath",
        ("Update(", "FixedUpdate(", "LateUpdate("),
    ),
    "unityRandom": Category(
        re.compile(r"\bUnityEngine\.Random\b|\bRandom\.(?:Range|value|insideUnit)"),
        12,
        "determinism",
        ("Random.", "UnityEngine.Random"),
    ),
    "unityTimeCritical": Category(
        re.compile(r"\bTime\.(?:deltaTime|fixedDeltaTime|frameCount|time)\b"),
        4,
        "determinism",
        ("Time.",),
    ),
    "packOne": Category(
        re.compile(r"\[StructLayout[^\]]*\bPack\s*=\s*1\b"),
        100,
        "layout",
        ("StructLayout", "Pack"),
    ),
    "structAutoProperties": Category(
        re.compile(r"\{\s*get\s*;\s*(?:private\s+)?set\s*;"),
        6,
        "layout",
        ("get;", "set;"),
    ),
    "binaryHardwareSwitch": Category(
        re.compile(
            r"\b(?:isLowEnd|IsLowEnd|LowEnd|HighEnd|UltraTier|QualityTier|HardwareTier|DeviceTier|"
            r"StandaloneQuest|QuestOnly|PcOnly)\b"
        ),
        3,
        "platform",
        ("LowEnd", "HighEnd", "QualityTier", "HardwareTier", "DeviceTier", "Quest", "PcOnly"),
    ),
}

STRUCT_DECL_RE = re.compile(r"\b(?:public|private|internal|protected)?\s*(?:readonly\s+)?(?:partial\s+)?struct\s+\w+")


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


def likely_editor_path(path: str) -> bool:
    parts = path.replace("\\", "/").split("/")
    return "Editor" in parts or path.endswith("Editor.cs") or "/Editor/" in path


def extract_domain(path: str) -> str:
    parts = path.replace("\\", "/").split("/")
    if "Scripts" in parts:
        scripts_index = parts.index("Scripts")
        tail = parts[scripts_index + 1 :]
        if not tail:
            return "Root"
        first = tail[0]
        if first.endswith(".cs"):
            return "Root"
        return first or "Root"

    if len(parts) >= 2 and not parts[0].endswith(".cs"):
        return parts[0] or "Root"
    return "Root"


def line_has_hint(code: str, hints: tuple[str, ...]) -> bool:
    return any(hint in code for hint in hints)


def scan_source(rel: str, source: str) -> dict[str, object]:
    category_counts = {name: 0 for name in CATEGORIES}
    family_counts: dict[str, int] = {}
    examples: list[dict[str, object]] = []
    score = 0
    lines = source.splitlines()
    in_struct = False
    struct_depth = 0

    def record(name: str, line_number: int, raw_line: str) -> None:
        nonlocal score
        category = CATEGORIES[name]
        category_counts[name] += 1
        family_counts[category.family] = family_counts.get(category.family, 0) + 1
        score += category.score
        if len(examples) < 12:
            examples.append(
                {
                    "line": line_number,
                    "category": name,
                    "text": raw_line.strip(),
                }
            )

    for line_number, raw_line in enumerate(lines, 1):
        code = strip_line_comment(raw_line)
        if not in_struct and STRUCT_DECL_RE.search(code):
            in_struct = True
            struct_depth = 0

        for name, category in CATEGORIES.items():
            if name == "structAutoProperties":
                continue
            if name == "localNumericBufferCast" and rel.endswith("Assets/_Project/Scripts/Core/Memory/H8Memory.cs"):
                continue
            if category.token_hint and not line_has_hint(code, category.token_hint):
                continue
            if not category.pattern.search(code):
                continue
            record(name, line_number, raw_line)

        auto_property = CATEGORIES["structAutoProperties"]
        if in_struct and line_has_hint(code, auto_property.token_hint) and auto_property.pattern.search(code):
            record("structAutoProperties", line_number, raw_line)

        if in_struct:
            struct_depth += code.count("{") - code.count("}")
            if struct_depth <= 0 and "}" in code:
                in_struct = False
                struct_depth = 0
    return {
        "path": rel,
        "domain": extract_domain(rel),
        "score": score,
        "isEditorPath": likely_editor_path(rel),
        "categoryCounts": {key: value for key, value in category_counts.items() if value},
        "familyCounts": dict(sorted(family_counts.items())),
        "examples": examples,
    }


def scan_file(path: Path) -> dict[str, object]:
    rel = normalize_path(path)
    source = path.read_text(encoding="utf-8", errors="ignore")
    return scan_source(rel, source)


def aggregate_payload(source_root: Path, cs_file_count: int, scanned_rows: list[dict[str, object]]) -> dict[str, object]:
    rows = [row for row in scanned_rows if int(row["score"]) > 0]
    rows.sort(key=lambda item: (-int(item["score"]), str(item["path"])))

    category_totals = {name: 0 for name in CATEGORIES}
    family_totals: dict[str, int] = {}
    domain_totals: dict[str, dict[str, object]] = {}
    for row in rows:
        counts = row["categoryCounts"]
        if not isinstance(counts, dict):
            continue
        domain = str(row.get("domain") or "Root")
        domain_row = domain_totals.setdefault(
            domain,
            {"domain": domain, "score": 0, "files": 0, "familyCounts": {}, "topFiles": []},
        )
        domain_row["score"] = int(domain_row["score"]) + int(row["score"])
        domain_row["files"] = int(domain_row["files"]) + 1
        top_files = domain_row["topFiles"]
        if isinstance(top_files, list) and len(top_files) < 6:
            top_files.append({"path": row["path"], "score": row["score"]})

        for name, count in counts.items():
            category_totals[name] += int(count)
            family = CATEGORIES[name].family
            family_totals[family] = family_totals.get(family, 0) + int(count)
            family_counts = domain_row["familyCounts"]
            if isinstance(family_counts, dict):
                family_counts[family] = int(family_counts.get(family, 0)) + int(count)

    return {
        "schema": SCHEMA,
        "sourceRoot": normalize_path(source_root),
        "csFileCount": cs_file_count,
        "scoredFileCount": len(rows),
        "categoryTotals": {key: value for key, value in category_totals.items() if value},
        "familyTotals": dict(sorted(family_totals.items())),
        "domainTotals": sorted(
            domain_totals.values(),
            key=lambda item: (-int(item["score"]), str(item["domain"])),
        ),
        "topFiles": rows[:80],
    }


def build_payload(source_root: Path) -> dict[str, object]:
    files = iter_cs_files(source_root)
    rows = [scan_file(path) for path in files]
    return aggregate_payload(source_root, len(files), rows)


def write_json(path: Path, payload: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def write_markdown(path: Path, payload: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    category_totals = payload["categoryTotals"]
    family_totals = payload["familyTotals"]
    domain_totals = payload["domainTotals"]
    top_files = payload["topFiles"]
    if (
        not isinstance(category_totals, dict)
        or not isinstance(family_totals, dict)
        or not isinstance(domain_totals, list)
        or not isinstance(top_files, list)
    ):
        raise TypeError("payload malformed")

    lines = [
        "# Architecture Risk Hotlist",
        "",
        "Evidence class: STATIC_SOURCE. This is a ranked review map, not compile, runtime, profiler, GC, memory, player-build, or device proof.",
        "",
        f"- Schema: `{payload['schema']}`",
        f"- Source root: `{payload['sourceRoot']}`",
        f"- C# files: `{payload['csFileCount']}`",
        f"- Scored files: `{payload['scoredFileCount']}`",
        "",
        "## Family Totals",
        "",
        "| Family | Matches |",
        "|---|---:|",
    ]
    for family, count in family_totals.items():
        lines.append(f"| `{family}` | {count} |")

    lines.extend(
        [
            "",
            "## Domain Pressure",
            "",
            "| Rank | Domain | Score | Scored files | Family pressure | Top files |",
            "|---:|---|---:|---:|---|---|",
        ]
    )
    for index, row in enumerate(domain_totals[:20], 1):
        if not isinstance(row, dict):
            continue
        families = row.get("familyCounts") or {}
        family_text = ", ".join(f"{key}:{value}" for key, value in families.items()) if isinstance(families, dict) else ""
        files = row.get("topFiles") or []
        file_text = ", ".join(str(item.get("path")) for item in files[:3] if isinstance(item, dict))
        lines.append(
            f"| {index} | `{row.get('domain')}` | {row.get('score')} | {row.get('files')} | {family_text} | {file_text} |"
        )

    lines.extend(["", "## Category Totals", "", "| Category | Matches |", "|---|---:|"])
    for name, count in sorted(category_totals.items()):
        lines.append(f"| `{name}` | {count} |")

    lines.extend(
        [
            "",
            "## Top Review Files",
            "",
            "| Rank | Score | File | Top categories |",
            "|---:|---:|---|---|",
        ]
    )
    for index, row in enumerate(top_files[:40], 1):
        if not isinstance(row, dict):
            continue
        counts = row.get("categoryCounts") or {}
        category_text = ", ".join(f"{key}:{value}" for key, value in counts.items()) if isinstance(counts, dict) else ""
        lines.append(f"| {index} | {row.get('score')} | `{row.get('path')}` | {category_text} |")

    lines.extend(["", "## Review Notes", ""])
    for row in top_files[:12]:
        if not isinstance(row, dict):
            continue
        lines.append(f"### {row.get('path')}")
        lines.append("")
        lines.append(f"- Score: `{row.get('score')}`")
        lines.append(f"- Families: `{row.get('familyCounts')}`")
        examples = row.get("examples") or []
        for example in examples[:4]:
            if isinstance(example, dict):
                lines.append(
                    f"- L{example.get('line')} `{example.get('category')}`: `{example.get('text')}`"
                )
        lines.append("")

    lines.extend(
        [
            "## Interpretation",
            "",
            "- This hotlist is for ordering review, not for automatic refactor.",
            "- Editor-only files can score high and still be acceptable; runtime/hot-path files need stricter treatment.",
            "- A high score means multiple architectural pressure surfaces overlap in one file: registry, signals, native ownership, job barriers, deterministic time/random, layout, or platform-tier logic.",
            "- Domain pressure is for owner slicing: fix one domain route at a time with a route card and proof artifact instead of broad repository churn.",
            "- Do not use score movement as H-Phi proof. It is a triage input for targeted owner-domain passes.",
            "",
        ]
    )
    path.write_text("\n".join(lines), encoding="utf-8")


def print_text(payload: dict[str, object]) -> None:
    print("Architecture risk hotlist")
    print(f"schema={payload['schema']}")
    print(f"sourceRoot={payload['sourceRoot']}")
    print(f"csFiles={payload['csFileCount']}")
    print(f"scoredFiles={payload['scoredFileCount']}")
    print(f"familyTotals={payload['familyTotals']}")
    top_files = payload["topFiles"]
    if isinstance(top_files, list):
        for row in top_files[:10]:
            if isinstance(row, dict):
                print(f"top={row['score']} {row['path']} {row['categoryCounts']}")
    print("status=PASS_WITH_WARNINGS")


def run(args: argparse.Namespace) -> int:
    payload = build_payload(Path(args.source_root))
    write_json(Path(args.json_path), payload)
    write_markdown(Path(args.report_path), payload)
    if args.json:
        print(json.dumps(payload, indent=2, sort_keys=True))
    else:
        print_text(payload)
    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", default=str(DEFAULT_SOURCE_ROOT))
    parser.add_argument("--report-path", default=str(DEFAULT_REPORT_PATH))
    parser.add_argument("--json-path", default=str(DEFAULT_JSON_PATH))
    parser.add_argument("--json", action="store_true", help="Print JSON payload to stdout.")
    return parser


def main() -> int:
    return run(build_parser().parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
