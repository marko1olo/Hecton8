# -*- coding: utf-8 -*-
"""Mechanical split of HectonVoxelEngine.cs into MCTables / Jobs / Runtime / Editor.
Zero logic change. Same assembly (Hecton8.Core) for runtime pieces.
Editor piece goes under Assets/_Project/Scripts/Editor/ if it uses UnityEditor.
"""
from __future__ import annotations
from pathlib import Path
import re
import shutil
from datetime import datetime

SRC = Path(r"Assets/_Project/Scripts/HectonVoxelEngine.cs")
BACKUP = Path(r"Docs/AgentLogs/_backup_HectonVoxelEngine_pre_split.cs")
OUT_DIR = Path(r"Assets/_Project/Scripts")
EDITOR_DIR = Path(r"Assets/_Project/Scripts/Editor")
REPORT = Path(r"Docs/AgentLogs/ARCH_VOXEL_SPLIT_A_2026-08-03.md")

def main() -> None:
    raw = SRC.read_bytes()
    text = raw.decode("utf-8")
    nl = "\r\n" if b"\r\n" in raw[:2000] else "\n"
    lines = text.splitlines()
    total = len(lines)
    print(f"total_lines={total}")

    # Find markers
    def find_line(pred, start=0):
        for i in range(start, total):
            if pred(lines[i]):
                return i
        return -1

    usings_end = 0
    for i, l in enumerate(lines):
        if l.startswith("using ") or l.startswith("//") or l.strip() == "" or l.startswith("/*") or l.startswith(" *") or l.startswith("*/"):
            usings_end = i
            if l.startswith("using "):
                continue
            # allow header comments before usings
            continue
        # first non-using non-comment content
        if not l.startswith("using "):
            # if we already saw usings, stop at first real code
            break
    # better: last using line
    last_using = max(i for i, l in enumerate(lines) if l.startswith("using "))
    header = lines[: last_using + 1]
    # include blank line after usings if present
    body_start = last_using + 1
    while body_start < total and lines[body_start].strip() == "":
        body_start += 1

    mctables_start = find_line(lambda l: "#region Marching Cubes Tables" in l or l.strip() == "public static class MCTables")
    if mctables_start < 0:
        raise SystemExit("MCTables start not found")
    # include region line if just before
    if mctables_start > 0 and "#region" in lines[mctables_start - 1]:
        mctables_start -= 1

    mctables_end_region = find_line(lambda l: l.strip() == "#endregion", mctables_start)
    # MCTables class ends at endregion or before next type
    jobs_start = find_line(
        lambda l: (
            "IJob" in l
            or "struct " in l and "Job" in l
            or "#region" in l and "Job" in l
            or "BurstCompile" in l
        ),
        mctables_end_region + 1 if mctables_end_region >= 0 else mctables_start + 1,
    )
    # Prefer first BurstCompile or job struct after MCTables endregion
    if mctables_end_region >= 0:
        scan = mctables_end_region + 1
    else:
        scan = mctables_start + 1
    jobs_start = -1
    for i in range(scan, total):
        s = lines[i].strip()
        if s.startswith("[BurstCompile") or s.startswith("public struct ") and "Job" in s or s.startswith("#region") and "Job" in s:
            jobs_start = i
            break
        if "public sealed class HectonVoxelEngine" in lines[i] or "public class HectonVoxelEngine" in lines[i]:
            break
    if jobs_start < 0:
        # fallback: line after endregion
        jobs_start = (mctables_end_region + 1) if mctables_end_region >= 0 else scan

    runtime_start = find_line(lambda l: "public sealed class HectonVoxelEngine" in l or re.search(r"public\s+class\s+HectonVoxelEngine\b", l))
    if runtime_start < 0:
        raise SystemExit("HectonVoxelEngine class not found")
    # include attributes immediately above
    rs = runtime_start
    while rs > 0 and (lines[rs - 1].strip().startswith("[") or lines[rs - 1].strip().startswith("//") or lines[rs - 1].strip() == "" or lines[rs - 1].strip().startswith("#")):
        # stop if we hit job code
        if "IJob" in lines[rs - 1] or "BurstCompile" in lines[rs - 1] and rs < runtime_start:
            # attributes for the class are OK; BurstCompile on class unlikely
            pass
        if lines[rs - 1].strip().startswith("public struct") or lines[rs - 1].strip().startswith("private struct"):
            break
        rs -= 1
        if rs < runtime_start - 30:
            rs = runtime_start
            break
    # more carefully: only pull [attributes] and comments directly above class
    rs = runtime_start
    while rs > 0:
        prev = lines[rs - 1].strip()
        if prev == "" or prev.startswith("//") or prev.startswith("[") or prev.startswith("#region") or prev.startswith("#if") or prev.startswith("#endif"):
            rs -= 1
            continue
        break

    editor_start = find_line(lambda l: "class HectonVoxelEngineEditor" in l)
    # include CustomEditor attribute above
    es = editor_start
    if es >= 0:
        while es > 0:
            prev = lines[es - 1].strip()
            if prev == "" or prev.startswith("//") or prev.startswith("[") or prev.startswith("#if") or prev.startswith("#region") or prev.startswith("#endif") or prev.startswith("#else"):
                es -= 1
                continue
            break

    print(f"last_using={last_using+1}")
    print(f"mctables_start={mctables_start+1}")
    print(f"mctables_end_region={mctables_end_region+1 if mctables_end_region>=0 else -1}")
    print(f"jobs_start={jobs_start+1}")
    print(f"runtime_start_attr={rs+1} class={runtime_start+1}")
    print(f"editor_start={es+1 if es>=0 else -1}")

    # Write structure report sample
    for label, idx in [("mctables", mctables_start), ("jobs", jobs_start), ("runtime", rs), ("editor", es if es>=0 else total-1)]:
        a = max(0, idx - 2)
        b = min(total, idx + 5)
        print(f"--- {label} context ---")
        for i in range(a, b):
            s = lines[i][:120]
            s = "".join(ch if (32 <= ord(ch) < 127 or ch in "\t") else "?" for ch in s)
            print(f"{i+1}|{s}")

    # Build file contents
    file_header_comment = [
        "// =====================================================================",
        "// MECHANICAL SPLIT from HectonVoxelEngine.cs — Slice A (no logic change)",
        f"// Date: {datetime.utcnow().strftime('%Y-%m-%d')} — architecture god-object reduction",
        "// Original single-file owner retained behavioral authority in HectonVoxelEngine",
        "// =====================================================================",
        "",
    ]

    def join(parts: list[str]) -> str:
        # normalize to LF for write; Unity accepts LF
        body = "\n".join(parts)
        if not body.endswith("\n"):
            body += "\n"
        return body

    usings_block = lines[: last_using + 1]

    # MCTables: from mctables_start through mctables_end_region (inclusive)
    if mctables_end_region < mctables_start:
        raise SystemExit("bad mctables range")
    mctables_body = lines[mctables_start : mctables_end_region + 1]

    # Jobs: from jobs_start to just before runtime attrs
    jobs_body = lines[jobs_start:rs]
    # trim trailing blanks
    while jobs_body and jobs_body[-1].strip() == "":
        jobs_body.pop()

    # Runtime: from rs to before editor (or end)
    runtime_end = es if es >= 0 else total
    runtime_body = lines[rs:runtime_end]
    while runtime_body and runtime_body[-1].strip() == "":
        runtime_body.pop()
    # if runtime ends with #endif for editor guard that belongs to editor, handle below

    editor_body = lines[es:] if es >= 0 else []

    # Detect if editor section wrapped in #if UNITY_EDITOR inside runtime
    # If runtime_body ends with #if UNITY_EDITOR and editor starts after, keep clean

    mctables_file = join(file_header_comment + usings_block + [""] + mctables_body + [""])
    jobs_file = join(file_header_comment + usings_block + [""] + jobs_body + [""])
    runtime_file = join(usings_block + [""] + runtime_body + [""])

    # Editor: needs UnityEditor using
    editor_usings = list(usings_block)
    if not any("using UnityEditor" in u for u in editor_usings):
        editor_usings.append("using UnityEditor;")
    # wrap editor in #if UNITY_EDITOR if not already
    ed = list(editor_body)
    ed_text = "\n".join(ed)
    if "#if UNITY_EDITOR" not in ed_text:
        ed = ["#if UNITY_EDITOR", ""] + ed + ["", "#endif"]
    editor_file = join(file_header_comment + editor_usings + [""] + ed + [""])

    # Sanity: line counts
    print(f"mctables_lines={len(mctables_body)} jobs_lines={len(jobs_body)} runtime_lines={len(runtime_body)} editor_lines={len(editor_body)}")
    print(f"sum_parts={len(mctables_body)+len(jobs_body)+len(runtime_body)+len(editor_body)} vs body={total - body_start}")

    # Backup original
    BACKUP.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(SRC, BACKUP)
    print(f"backup -> {BACKUP}")

    # Write outputs — keep runtime path as original file name for meta/GUID stability
    out_mctables = OUT_DIR / "HectonVoxelEngine.MCTables.cs"
    out_jobs = OUT_DIR / "HectonVoxelEngine.Jobs.cs"
    out_runtime = SRC  # overwrite original with runtime-only
    # Editor stays in Core with #if UNITY_EDITOR to avoid asmdef reference churn,
    # OR put in Editor folder. Prefer same folder with #if to keep type discoverability simple.
    out_editor = OUT_DIR / "HectonVoxelEngine.Editor.cs"

    out_mctables.write_text(mctables_file, encoding="utf-8", newline="\n")
    out_jobs.write_text(jobs_file, encoding="utf-8", newline="\n")
    out_runtime.write_text(runtime_file, encoding="utf-8", newline="\n")
    out_editor.write_text(editor_file, encoding="utf-8", newline="\n")

    report = f"""# VoxelEngine Slice A — mechanical split
Date: 2026-08-03
Evidence: SOURCE_CHANGE + pending Unity compile

## Split map
| File | Role | Approx lines |
|---|---|---:|
| `HectonVoxelEngine.MCTables.cs` | Marching Cubes tables | {len(mctables_body)} |
| `HectonVoxelEngine.Jobs.cs` | Burst/IJob pipeline types | {len(jobs_body)} |
| `HectonVoxelEngine.cs` | Runtime MonoBehaviour coordinator (same path/GUID) | {len(runtime_body)} |
| `HectonVoxelEngine.Editor.cs` | Inspector editor (#if UNITY_EDITOR) | {len(editor_body)} |

## Markers
- MCTables: L{mctables_start+1}..L{mctables_end_region+1}
- Jobs: L{jobs_start+1}..L{rs}
- Runtime: L{rs+1}..L{runtime_end}
- Editor: L{es+1 if es>=0 else -1}..EOF

## Rules honored
- No logic edits
- Same directory → same `Hecton8.Core` asmdef
- Original `HectonVoxelEngine.cs` path preserved (meta GUID / script reference stability)
- Backup: `{BACKUP.as_posix()}`

## Status
PENDING VERIFICATION — Unity batchmode compile required.
"""
    REPORT.write_text(report, encoding="utf-8")
    print("WROTE", out_mctables)
    print("WROTE", out_jobs)
    print("WROTE", out_runtime)
    print("WROTE", out_editor)
    print("REPORT", REPORT)

if __name__ == "__main__":
    main()
