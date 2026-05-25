#!/usr/bin/env python3
"""Static documentation structure validator for HECTON-8."""

from __future__ import annotations

import argparse
import json
import os
import re
from pathlib import Path
from typing import Iterable


ROOT_DOCS = {"AGENTS.md", "MASTER_RELEASE_WORK_PLAN.md", "BUILD_PLAYTEST_ISSUES.md"}
ARCHIVE_PARTS = {"DEPRECATED", "_Archive", "Archive", "AgentLogs", "Tasks"}
DOC_EXTENSIONS = {".md", ".txt", ".diff"}
TRANSIENT_REPORT_SUFFIXES = ("_stdout.txt", "_stderr.txt")


def repo_root() -> Path:
    return Path(__file__).resolve().parents[1]


def is_doc(path: Path) -> bool:
    return path.suffix.lower() in DOC_EXTENSIONS and not is_transient_report_output(path)


def is_transient_report_output(path: Path) -> bool:
    return (
        path.name.endswith(TRANSIENT_REPORT_SUFFIXES)
        and path.parent.name == "Reports"
        and path.parent.parent.name == "Docs"
    )


def is_active_docs_path(path: Path, root: Path) -> bool:
    try:
        rel = path.relative_to(root)
    except ValueError:
        return False

    if len(rel.parts) == 1:
        return path.name in ROOT_DOCS

    if rel.parts[0] != "Docs":
        return False

    return not any(part in ARCHIVE_PARTS for part in rel.parts[1:])


def iter_active_docs(root: Path) -> Iterable[Path]:
    for name in sorted(ROOT_DOCS):
        path = root / name
        if path.exists():
            yield path

    docs = root / "Docs"
    if not docs.exists():
        return

    for path in sorted(docs.rglob("*")):
        if path.is_file() and is_doc(path) and is_active_docs_path(path, root):
            yield path


def read_text(path: Path) -> str:
    data = path.read_bytes()
    for encoding in ("utf-8-sig", "utf-8", "cp1251"):
        try:
            return data.decode(encoding)
        except UnicodeDecodeError:
            continue
    return data.decode("utf-8", errors="replace")


def has_utf8_sig(path: Path) -> bool:
    return path.read_bytes().startswith(b"\xef\xbb\xbf")


def find_headers(text: str) -> list[str]:
    headers: list[str] = []
    for line in text.splitlines():
        if line.startswith("#"):
            stripped = line.strip()
            if re.match(r"^#{1,6}\s+\S", stripped):
                headers.append(stripped.lower())
    return headers


def duplicate_headers(headers: list[str]) -> list[str]:
    seen: set[str] = set()
    dupes: list[str] = []
    for header in headers:
        if header in seen and header not in dupes:
            dupes.append(header)
        seen.add(header)
    return dupes


def fence_issues(text: str) -> list[str]:
    issues: list[str] = []
    fence_count = 0
    in_fence = False
    for line_number, line in enumerate(text.splitlines(), start=1):
        stripped = line.strip()
        if stripped.startswith("```"):
            fence_count += 1
            if not in_fence and stripped == "```":
                issues.append(f"line {line_number}: fence has no language tag")
            in_fence = not in_fence
    if fence_count % 2:
        issues.append("unclosed fenced code block")
    return issues


LINK_RE = re.compile(r"(?<!!)\[[^\]]+\]\(([^)]+)\)")


def broken_links(path: Path, root: Path, text: str) -> list[str]:
    broken: list[str] = []
    for match in LINK_RE.finditer(text):
        target = match.group(1).strip()
        if not target or target.startswith(("#", "http://", "https://", "mailto:")):
            continue
        target = target.split("#", 1)[0].strip("<>")
        if not target:
            continue
        candidate = (path.parent / target).resolve()
        if not candidate.exists():
            broken.append(target)
    return broken


STALE_PATTERNS = {
    "signal_registry_256": re.compile(r"SignalBusRegistry[^.\n]*(capacity|LaneCapacity)[^.\n]*`?256`?", re.IGNORECASE),
    "data_monolith_absent": re.compile(r"static_data\.h8bin[^.\n]*(is|remains|currently)\s+(still\s+)?(absent|missing)|payload status[^.\n]*absent", re.IGNORECASE),
    "h8dm_header_16": re.compile(r"H8DM header (size )?`?16`?\s*bytes", re.IGNORECASE),
}


def stale_hits(text: str) -> dict[str, int]:
    return {name: len(pattern.findall(text)) for name, pattern in STALE_PATTERNS.items()}


def validate(root: Path) -> dict:
    files = list(iter_active_docs(root))
    root_docs = sorted(path.name for path in files if path.parent == root and path.name in ROOT_DOCS)
    result = {
        "schema": "hecton8.doc_structure.v1",
        "repoRoot": str(root),
        "activeDocCount": len(files),
        "rootTextDocs": root_docs,
        "rootTextDocCount": len(root_docs),
        "rootTextDocPolicyPass": root_docs == sorted(ROOT_DOCS),
        "duplicateHeaderFiles": [],
        "brokenLinkFiles": [],
        "fenceIssueFiles": [],
        "encodingWithoutUtf8Sig": [],
        "staleParameterFiles": [],
    }

    for path in files:
        rel = path.relative_to(root).as_posix()
        text = read_text(path)
        dupes = duplicate_headers(find_headers(text))
        links = broken_links(path, root, text)
        fences = fence_issues(text)
        stale = stale_hits(text)

        if dupes:
            result["duplicateHeaderFiles"].append({"path": rel, "headers": dupes[:20]})
        if links:
            result["brokenLinkFiles"].append({"path": rel, "links": links[:30]})
        if fences:
            result["fenceIssueFiles"].append({"path": rel, "issues": fences[:30]})
        if not has_utf8_sig(path):
            result["encodingWithoutUtf8Sig"].append(rel)
        if any(stale.values()):
            result["staleParameterFiles"].append({"path": rel, "hits": stale})

    result["pass"] = (
        result["rootTextDocPolicyPass"]
        and not result["duplicateHeaderFiles"]
        and not result["brokenLinkFiles"]
        and not result["fenceIssueFiles"]
        and not result["encodingWithoutUtf8Sig"]
        and not result["staleParameterFiles"]
    )
    return result


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--report", default="Docs/Reports/DOC_STRUCTURE_VALIDATION_X_012.json")
    args = parser.parse_args()
    root = repo_root()
    report = validate(root)
    output = root / args.report
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8-sig")
    print(json.dumps({
        "pass": report["pass"],
        "activeDocCount": report["activeDocCount"],
        "rootTextDocCount": report["rootTextDocCount"],
        "duplicateHeaderFiles": len(report["duplicateHeaderFiles"]),
        "brokenLinkFiles": len(report["brokenLinkFiles"]),
        "fenceIssueFiles": len(report["fenceIssueFiles"]),
        "staleParameterFiles": len(report["staleParameterFiles"]),
        "encodingWithoutUtf8Sig": len(report["encodingWithoutUtf8Sig"]),
        "report": str(output),
    }, ensure_ascii=False))
    return 0 if report["pass"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
