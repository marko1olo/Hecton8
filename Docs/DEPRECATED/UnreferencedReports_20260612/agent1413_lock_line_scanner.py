from __future__ import annotations

import hashlib
import json
import re
import time
from pathlib import Path


ROOT = Path(r"C:\hades\Hecton8\Assets\_Project\Scripts")
PROJECT_ROOT = Path(r"C:\hades\Hecton8")
OUTPUT = Path(r"C:\hades\Hecton8\Docs\Reports\LOCK_CONTENTION_SPAN_LEDGER_1413.json")

LOCK_RE = re.compile(r"TryAcquireWriteLock|TryLockBuffer")
METHOD_RE = re.compile(
    r"^\s*(?:public|private|protected|internal|static|sealed|virtual|override|async|unsafe|partial|extern|\s)+"
    r"\s*[\w<>\[\],\s\?\.]+\s+(?P<name>[A-Za-z_]\w*)\s*\([^;{}]*\)\s*(?:where\s+[^{]+)?\{?"
)
LOOP_RE = re.compile(r"\b(for|foreach|while)\s*\(")


def rel(path: Path) -> str:
    return str(path.relative_to(PROJECT_ROOT)).replace("\\", "/")


def file_hash(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def brace_delta(line: str) -> int:
    stripped = re.sub(r'@"(?:[^"]|"")*"', "", line)
    stripped = re.sub(r'"(?:\\.|[^"\\])*"', "", stripped)
    stripped = re.sub(r"'(?:\\.|[^'\\])'", "", stripped)
    stripped = stripped.split("//", 1)[0]
    return stripped.count("{") - stripped.count("}")


def method_at(lines: list[str], line_index: int) -> str:
    floor = max(0, line_index - 180)
    for i in range(line_index, floor - 1, -1):
        match = METHOD_RE.match(lines[i])
        if match:
            return match.group("name")
    return "<unknown>"


def inside_loop(lines: list[str], line_index: int) -> bool:
    depth = 0
    floor = max(0, line_index - 100)
    for i in range(line_index, floor - 1, -1):
        line = lines[i]
        depth -= brace_delta(line)
        if LOOP_RE.search(line) and depth <= 1:
            return True
    return False


def find_try_body(lines: list[str], lock_line_index: int) -> tuple[int, int, list[str]]:
    try_line = -1
    for i in range(lock_line_index, min(len(lines), lock_line_index + 35)):
        if re.search(r"\btry\b", lines[i]):
            try_line = i
            break
    if try_line < 0:
        return 0, 0, []

    depth = 0
    opened = False
    body_start = try_line
    for i in range(try_line, len(lines)):
        delta = brace_delta(lines[i])
        if "{" in lines[i] and not opened:
            opened = True
            body_start = i + 1
        depth += delta
        if opened and depth <= 0:
            return try_line + 1, i + 1, lines[body_start:i]
        if i - try_line > 900:
            return try_line + 1, i + 1, lines[body_start:i]

    return try_line + 1, len(lines), lines[body_start:]


def finally_release_shape(lines: list[str], try_end_line: int) -> bool:
    if try_end_line <= 0:
        return False
    search_end = min(len(lines), try_end_line + 80)
    finally_line = -1
    for i in range(try_end_line - 1, search_end):
        if re.search(r"\bfinally\b", lines[i]):
            finally_line = i
            break
    if finally_line < 0:
        return False
    depth = 0
    opened = False
    for i in range(finally_line, min(len(lines), finally_line + 140)):
        line = lines[i]
        if "ReleaseWriteLock" in line or "TryUnlockBuffer" in line:
            return True
        delta = brace_delta(line)
        if "{" in line:
            opened = True
        depth += delta
        if opened and depth <= 0 and i > finally_line:
            return False
    return False


def complexity(body: list[str]) -> dict[str, int]:
    text = "\n".join(body)
    return {
        "lineCount": len(body),
        "branchCount": len(re.findall(r"\b(if|switch|case|else)\b", text)),
        "loopCount": len(re.findall(r"\b(for|foreach|while)\s*\(", text)),
        "returnCount": len(re.findall(r"\breturn\b", text)),
        "newCount": len(re.findall(r"\bnew\s+[A-Za-z_]\w*", text)),
        "linqCount": len(re.findall(r"\.(Where|Select|Any|All|First|FirstOrDefault|ToList|Sum|OrderBy)\s*\(", text)),
        "mathCallCount": len(re.findall(r"\b(math|Mathf|Math)\s*\.", text)),
        "assignmentCount": len(re.findall(r"(?<![=!<>])=(?!=)", text)),
        "nestedLockCount": len(re.findall(r"TryAcquireWriteLock|TryLockBuffer", text)),
        "stringInterpolationCount": len(re.findall(r'\$"', text)),
    }


def scan_file(path: Path) -> list[dict[str, object]]:
    text = path.read_text(encoding="utf-8-sig", errors="replace")
    if "TryAcquireWriteLock" not in text and "TryLockBuffer" not in text:
        return []
    lines = text.splitlines()
    sha = file_hash(path)
    records: list[dict[str, object]] = []
    for idx, line in enumerate(lines):
        for match in LOCK_RE.finditer(line):
            try_start, try_end, body = find_try_body(lines, idx)
            comp = complexity(body)
            loop = inside_loop(lines, idx)
            release_shape = finally_release_shape(lines, try_end)
            guard_window = "\n".join(lines[max(0, idx - 2) : min(len(lines), idx + 4)])
            fail_closed = re.search(r"if\s*\([^;\n{]*!\s*[^)]*(TryAcquireWriteLock|TryLockBuffer)", guard_window) is not None
            priority = (
                comp["lineCount"] * 2
                + comp["branchCount"] * 4
                + comp["loopCount"] * 20
                + comp["mathCallCount"] * 6
                + comp["newCount"] * 10
                + comp["nestedLockCount"] * 30
                + (50 if not release_shape else 0)
                + (40 if loop else 0)
            )
            records.append(
                {
                    "file": rel(path),
                    "fileSha256": sha,
                    "method": method_at(lines, idx),
                    "api": match.group(0),
                    "line": idx + 1,
                    "lockLine": line.strip(),
                    "insideLoop": loop,
                    "failClosedGuardShape": fail_closed,
                    "hasTryAfterLock": try_start > 0,
                    "releaseInFinallyShape": release_shape,
                    "tryLine": try_start,
                    "tryBodyLines": comp["lineCount"],
                    "complexity": comp,
                    "priorityScore": priority,
                }
            )
    return records


def main() -> int:
    started = time.perf_counter_ns()
    files = list(ROOT.rglob("*.cs"))
    records: list[dict[str, object]] = []
    files_with_locks = 0
    for path in files:
        file_records = scan_file(path)
        if file_records:
            files_with_locks += 1
            records.extend(file_records)

    records.sort(key=lambda item: int(item["priorityScore"]), reverse=True)
    elapsed_us = (time.perf_counter_ns() - started) // 1000
    payload = {
        "summary": {
            "generatedUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
            "root": str(ROOT),
            "fileCount": len(files),
            "filesWithLocks": files_with_locks,
            "lockInvocationCount": len(records),
            "lockAcquireCount": sum(1 for r in records if r["api"] == "TryAcquireWriteLock"),
            "tryLockBufferCount": sum(1 for r in records if r["api"] == "TryLockBuffer"),
            "missingFinallyShapeCount": sum(1 for r in records if not r["releaseInFinallyShape"]),
            "insideLoopCount": sum(1 for r in records if r["insideLoop"]),
            "nestedLockCount": sum(1 for r in records if int(r["complexity"]["nestedLockCount"]) > 0),
            "scanMicroseconds": elapsed_us,
            "scanner": "Docs/Reports/agent1413_lock_line_scanner.py",
            "parserClass": "line-regex-brace-depth",
        },
        "records": records,
    }
    OUTPUT.write_text(json.dumps(payload, indent=2), encoding="utf-8")
    print(json.dumps(payload["summary"], indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
