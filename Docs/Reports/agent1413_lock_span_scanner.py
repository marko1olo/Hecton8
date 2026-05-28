from __future__ import annotations

import hashlib
import json
import re
import sys
import time
from bisect import bisect_right
from pathlib import Path


ROOT = Path(r"C:\hades\Hecton8\Assets\_Project\Scripts")
PROJECT_ROOT = Path(r"C:\hades\Hecton8")
OUTPUT = Path(r"C:\hades\Hecton8\Docs\Reports\LOCK_CONTENTION_SPAN_LEDGER_1413.json")

LOCK_RE = re.compile(r"TryAcquireWriteLock|TryLockBuffer")
METHOD_RE = re.compile(
    r"(?m)^\s*(?:public|private|protected|internal|static|sealed|virtual|override|async|unsafe|partial|extern|\s)+"
    r"\s*[\w<>\[\],\s\?\.]+\s+(?P<name>[A-Za-z_]\w*)\s*\([^;{}]*\)\s*(?:where\s+[^{]+)?\{"
)


def line_starts(text: str) -> list[int]:
    starts = [0]
    for idx, ch in enumerate(text):
        if ch == "\n" and idx + 1 < len(text):
            starts.append(idx + 1)
    return starts


def line_no(starts: list[int], index: int) -> int:
    return bisect_right(starts, index)


def matching_brace(text: str, open_index: int) -> int:
    if open_index < 0 or open_index >= len(text) or text[open_index] != "{":
        return -1

    depth = 0
    in_string = False
    in_char = False
    in_line_comment = False
    in_block_comment = False
    verbatim = False
    i = open_index
    length = len(text)

    while i < length:
        c = text[i]
        n = text[i + 1] if i + 1 < length else "\0"

        if in_line_comment:
            if c == "\n":
                in_line_comment = False
            i += 1
            continue
        if in_block_comment:
            if c == "*" and n == "/":
                in_block_comment = False
                i += 2
                continue
            i += 1
            continue
        if in_string:
            if verbatim:
                if c == '"' and n == '"':
                    i += 2
                    continue
                if c == '"':
                    in_string = False
                    verbatim = False
            else:
                if c == "\\":
                    i += 2
                    continue
                if c == '"':
                    in_string = False
            i += 1
            continue
        if in_char:
            if c == "\\":
                i += 2
                continue
            if c == "'":
                in_char = False
            i += 1
            continue

        if c == "/" and n == "/":
            in_line_comment = True
            i += 2
            continue
        if c == "/" and n == "*":
            in_block_comment = True
            i += 2
            continue
        if c == "@" and n == '"':
            in_string = True
            verbatim = True
            i += 2
            continue
        if c == '"':
            in_string = True
            i += 1
            continue
        if c == "'":
            in_char = True
            i += 1
            continue
        if c == "{":
            depth += 1
        elif c == "}":
            depth -= 1
            if depth == 0:
                return i
        i += 1

    return -1


def current_method(text: str, index: int) -> str:
    start = max(0, index - 12000)
    window = text[start:index]
    matches = list(METHOD_RE.finditer(window))
    if not matches:
        return "<unknown>"
    return matches[-1].group("name")


def in_loop(text: str, index: int) -> bool:
    start = max(0, index - 3500)
    return re.search(r"(?s)(for|foreach|while)\s*\([^)]*\)\s*\{(?:(?!\}).)*$", text[start:index]) is not None


def try_after_lock(text: str, index: int) -> dict[str, object] | None:
    window = text[index : index + 2500]
    m = re.search(r"\btry\s*\{", window)
    if not m:
        return None
    open_index = index + m.end() - 1
    close_index = matching_brace(text, open_index)
    if close_index < 0:
        return None
    after = text[close_index : close_index + 1200]
    body = text[open_index + 1 : close_index]
    return {
        "try_open": open_index,
        "try_close": close_index,
        "has_finally": re.match(r"\s*finally\s*\{", after) is not None,
        "release_after_try": re.search(r"ReleaseWriteLock|TryUnlockBuffer", after) is not None,
        "body": body,
    }


def complexity(body: str) -> dict[str, int]:
    return {
        "lineCount": body.count("\n") + (1 if body else 0),
        "branchCount": len(re.findall(r"\b(if|switch|case|else)\b", body)),
        "loopCount": len(re.findall(r"\b(for|foreach|while)\s*\(", body)),
        "returnCount": len(re.findall(r"\breturn\b", body)),
        "newCount": len(re.findall(r"\bnew\s+[A-Za-z_]\w*", body)),
        "linqCount": len(re.findall(r"\.(Where|Select|Any|All|First|FirstOrDefault|ToList|Sum|OrderBy)\s*\(", body)),
        "mathCallCount": len(re.findall(r"\b(math|Mathf|Math)\s*\.", body)),
        "assignmentCount": len(re.findall(r"(?<![=!<>])=(?!=)", body)),
        "nestedLockCount": len(re.findall(r"TryAcquireWriteLock|TryLockBuffer", body)),
        "stringInterpolationCount": len(re.findall(r'\$"', body)),
    }


def lock_line(text: str, index: int) -> str:
    start = text.rfind("\n", 0, index) + 1
    end = text.find("\n", index)
    if end < 0:
        end = len(text)
    return text[start:end].strip()


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def main() -> int:
    started = time.perf_counter_ns()
    files = list(ROOT.rglob("*.cs"))
    records: list[dict[str, object]] = []
    touched_files: set[Path] = set()

    for path in files:
        try:
            text = path.read_text(encoding="utf-8-sig")
        except UnicodeDecodeError:
            text = path.read_text(encoding="utf-8", errors="replace")

        matches = list(LOCK_RE.finditer(text))
        if not matches:
            continue

        starts = line_starts(text)
        touched_files.add(path)
        file_hash = sha256(path)
        rel = str(path.relative_to(PROJECT_ROOT)).replace("\\", "/")

        for match in matches:
            try_block = try_after_lock(text, match.start())
            body = "" if try_block is None else str(try_block["body"])
            comp = complexity(body)
            inside_loop = in_loop(text, match.start())
            if_window = text[max(0, match.start() - 160) : match.start() + 260]
            fail_closed_guard = (
                re.search(r"if\s*\([^;\n{]*!\s*[^)]*(TryAcquireWriteLock|TryLockBuffer)", if_window) is not None
            )
            release_shape = bool(
                try_block is not None and try_block["has_finally"] and try_block["release_after_try"]
            )
            priority = (
                comp["lineCount"] * 2
                + comp["branchCount"] * 4
                + comp["loopCount"] * 20
                + comp["mathCallCount"] * 6
                + comp["newCount"] * 10
                + comp["nestedLockCount"] * 30
                + (50 if not release_shape else 0)
                + (40 if inside_loop else 0)
            )
            records.append(
                {
                    "file": rel,
                    "fileSha256": file_hash,
                    "method": current_method(text, match.start()),
                    "api": match.group(0),
                    "line": line_no(starts, match.start()),
                    "lockLine": lock_line(text, match.start()),
                    "insideLoop": inside_loop,
                    "failClosedGuardShape": fail_closed_guard,
                    "hasTryAfterLock": try_block is not None,
                    "releaseInFinallyShape": release_shape,
                    "tryLine": 0 if try_block is None else line_no(starts, int(try_block["try_open"])),
                    "tryBodyLines": comp["lineCount"],
                    "complexity": comp,
                    "priorityScore": priority,
                }
            )

    records.sort(key=lambda r: int(r["priorityScore"]), reverse=True)
    elapsed_us = (time.perf_counter_ns() - started) // 1000
    payload = {
        "summary": {
            "generatedUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
            "root": str(ROOT),
            "fileCount": len(files),
            "filesWithLocks": len(touched_files),
            "lockInvocationCount": len(records),
            "lockAcquireCount": sum(1 for r in records if r["api"] == "TryAcquireWriteLock"),
            "tryLockBufferCount": sum(1 for r in records if r["api"] == "TryLockBuffer"),
            "missingFinallyShapeCount": sum(1 for r in records if not r["releaseInFinallyShape"]),
            "insideLoopCount": sum(1 for r in records if r["insideLoop"]),
            "nestedLockCount": sum(1 for r in records if int(r["complexity"]["nestedLockCount"]) > 0),
            "scanMicroseconds": elapsed_us,
            "scanner": "Docs/Reports/agent1413_lock_span_scanner.py",
        },
        "records": records,
    }
    OUTPUT.write_text(json.dumps(payload, indent=2), encoding="utf-8")
    print(str(OUTPUT))
    print(json.dumps(payload["summary"], indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())
