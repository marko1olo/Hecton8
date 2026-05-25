#!/usr/bin/env python3
"""Static scanner for dynamic texture/material allocation debt."""

from __future__ import annotations

import argparse
import json
import re
import subprocess
from pathlib import Path
from time import perf_counter


HIGH_CONFIDENCE_PATTERNS = {
    "Resources.Load<TextureOrMaterial>": re.compile(
        r"\bResources\.Load(?:Async)?\s*<[^>]*(?:Texture|Texture2D|Cubemap|Material|Shader)[^>]*>\s*\("
    ),
    "new Material": re.compile(r"\bnew\s+Material\s*\("),
    "Renderer.material": re.compile(
        r"\b(?:renderer|meshRenderer|skinnedRenderer|targetRenderer|sourceRenderer|_renderer|_meshRenderer|_skinnedRenderer|"
        r"s_Renderer|m_Renderer)\s*\.\s*material\b",
        re.IGNORECASE,
    ),
    "Renderer.materials": re.compile(
        r"\b(?:renderer|meshRenderer|skinnedRenderer|targetRenderer|sourceRenderer|_renderer|_meshRenderer|_skinnedRenderer|"
        r"s_Renderer|m_Renderer)\s*\.\s*materials\b",
        re.IGNORECASE,
    ),
}
REVIEW_PATTERNS = {
    "Resources.Load(any)": re.compile(r"\bResources\.Load(?:Async)?\s*\("),
    "material member access": re.compile(r"\.materials?\b"),
}
SECTION_KEY = "shinobu_361_oop_texture_scanner"
EDITOR_PATH_TOKENS = ("/editor/", "/tests/editor/")
QUICK_TOKENS = (b"Resources.Load", b"new Material", b".material", b".materials")


def is_scannable(path: Path) -> bool:
    if path.suffix.lower() not in {".cs", ".shader", ".hlsl", ".compute"}:
        return False
    lowered = path.as_posix().lower()
    if "/library/" in lowered or "/temp/" in lowered or "/obj/" in lowered:
        return False
    return True


def is_editor_path(path: Path) -> bool:
    lowered = path.as_posix().lower()
    return any(token in lowered for token in EDITOR_PATH_TOKENS)


def strip_comments_and_strings(line: str) -> str:
    result: list[str] = []
    in_string = False
    in_char = False
    escaped = False
    index = 0
    while index < len(line):
        char = line[index]
        next_char = line[index + 1] if index + 1 < len(line) else ""
        if not in_string and not in_char and char == "/" and next_char == "/":
            break
        if in_string:
            if escaped:
                escaped = False
            elif char == "\\":
                escaped = True
            elif char == '"':
                in_string = False
            result.append(" ")
        elif in_char:
            if escaped:
                escaped = False
            elif char == "\\":
                escaped = True
            elif char == "'":
                in_char = False
            result.append(" ")
        else:
            if char == '"':
                in_string = True
                result.append(" ")
            elif char == "'":
                in_char = True
                result.append(" ")
            else:
                result.append(char)
        index += 1
    return "".join(result)


def make_finding(path: Path, project_root: Path, line_number: int, issue: str, snippet: str, context: str, severity: str) -> dict[str, object]:
    return {
        "path": path.relative_to(project_root).as_posix(),
        "line": line_number,
        "issue": issue,
        "context": context,
        "severity": severity,
        "snippet": snippet[:180],
    }


def scan(root: Path, project_root: Path) -> dict[str, object]:
    start_time = perf_counter()
    findings: list[dict[str, object]] = []
    review_findings: list[dict[str, object]] = []
    scanned = 0
    candidate_files = 0
    for path in root.rglob("*"):
        if not is_scannable(path):
            continue
        scanned += 1
        context = "EDITOR" if is_editor_path(path.relative_to(project_root)) else "RUNTIME"
        try:
            raw = path.read_bytes()
        except OSError:
            continue
        if not any(token in raw for token in QUICK_TOKENS):
            continue
        candidate_files += 1
        lines = raw.decode("utf-8", errors="ignore").splitlines()
        for line_number, line in enumerate(lines, start=1):
            stripped = line.strip()
            if stripped.startswith("//"):
                continue
            code = strip_comments_and_strings(line)
            high_confidence_hit = False
            for name, pattern in HIGH_CONFIDENCE_PATTERNS.items():
                if pattern.search(code):
                    high_confidence_hit = True
                    severity = "BLOCKER" if context == "RUNTIME" else "EDITOR_REVIEW"
                    findings.append(make_finding(path, project_root, line_number, name, stripped, context, severity))
            if high_confidence_hit:
                continue
            for name, pattern in REVIEW_PATTERNS.items():
                if pattern.search(code):
                    review_findings.append(make_finding(path, project_root, line_number, name, stripped, context, "REVIEW_ONLY"))
                    break
    runtime_findings = [finding for finding in findings if finding["context"] == "RUNTIME"]
    editor_findings = [finding for finding in findings if finding["context"] == "EDITOR"]
    return {
        "schema": "hecton8.oop_texture_scanner.v1",
        "agent": "SHINOBU_361",
        "evidenceClass": "STATIC_SOURCE",
        "filesScanned": scanned,
        "candidateFilesScanned": candidate_files,
        "elapsedMs": round((perf_counter() - start_time) * 1000.0, 3),
        "findings": findings,
        "findingCount": len(findings),
        "runtimeFindingCount": len(runtime_findings),
        "editorFindingCount": len(editor_findings),
        "reviewFindings": review_findings,
        "reviewFindingCount": len(review_findings),
        "oopTextureAllocationsEradicated": len(runtime_findings) == 0,
        "status": "PASS_STATIC_RUNTIME_PENDING" if len(runtime_findings) == 0 else "PENDING_REMEDIATION",
        "note": "Static scan only. High-confidence findings target Resources.Load<Texture/Material>, new Material(), and likely Renderer.material access. Review-only material member hits are separated to avoid false renderer.clone claims.",
    }


def load_json_object(path: Path) -> dict[str, object]:
    if not path.exists():
        return {}
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return {}
    return data if isinstance(data, dict) else {}


def load_git_head_json(project_root: Path, output_path: Path) -> dict[str, object]:
    try:
        rel_path = output_path.relative_to(project_root).as_posix()
    except ValueError:
        return {}
    try:
        completed = subprocess.run(
            ["git", "show", f"HEAD:{rel_path}"],
            cwd=project_root,
            check=False,
            capture_output=True,
            text=True,
            encoding="utf-8",
        )
    except OSError:
        return {}
    if completed.returncode != 0 or not completed.stdout.strip():
        return {}
    try:
        data = json.loads(completed.stdout)
    except json.JSONDecodeError:
        return {}
    return data if isinstance(data, dict) else {}


def choose_shared_base(existing: dict[str, object], project_root: Path, output_path: Path) -> dict[str, object]:
    if existing.get("schema") == "hecton8.oop_texture_scanner.v1" and existing.get("agent") == "SHINOBU_361":
        head = load_git_head_json(project_root, output_path)
        return head if head else {}
    return existing


def write_shared_report(output_path: Path, project_root: Path, report: dict[str, object]) -> None:
    existing = load_json_object(output_path)
    shared = choose_shared_base(existing, project_root, output_path)
    shared[SECTION_KEY] = report
    output_path.write_text(json.dumps(shared, indent=2, sort_keys=False) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description="Scan dynamic texture/material allocation debt.")
    parser.add_argument("--project-root", default=".", help="Unity project root.")
    parser.add_argument("--root", default="Assets/_Project", help="Source root to scan.")
    parser.add_argument("--out", default="Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json", help="Report output path.")
    args = parser.parse_args()
    project_root = Path(args.project_root).resolve()
    root = (project_root / args.root).resolve()
    output_path = (project_root / args.out).resolve()
    output_path.parent.mkdir(parents=True, exist_ok=True)
    report = scan(root, project_root)
    write_shared_report(output_path, project_root, report)
    print("OOP_TEXTURE_SCANNER")
    print(f"files_scanned={report['filesScanned']}")
    print(f"candidate_files_scanned={report['candidateFilesScanned']}")
    print(f"elapsed_ms={report['elapsedMs']}")
    print(f"finding_count={report['findingCount']}")
    print(f"runtime_finding_count={report['runtimeFindingCount']}")
    print(f"editor_finding_count={report['editorFindingCount']}")
    print(f"review_finding_count={report['reviewFindingCount']}")
    print(f"status={report['status']}")
    print(f"section={SECTION_KEY}")
    print(f"report={output_path.relative_to(project_root).as_posix()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
