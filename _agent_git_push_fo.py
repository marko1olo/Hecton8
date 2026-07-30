# -*- coding: utf-8 -*-
from __future__ import annotations

import subprocess
from pathlib import Path

ROOT = Path(r"C:\hades\Hecton8")
OUT = ROOT / "_agent_git_push_fo_out.txt"
log: list[str] = []


def run(args: list[str], check: bool = True) -> subprocess.CompletedProcess:
    p = subprocess.run(
        args,
        cwd=str(ROOT),
        text=True,
        capture_output=True,
        encoding="utf-8",
        errors="replace",
    )
    log.append("$ " + " ".join(args))
    if p.stdout:
        log.append(p.stdout.rstrip())
    if p.stderr:
        log.append(p.stderr.rstrip())
    log.append(f"exit={p.returncode}")
    OUT.write_text("\n".join(log) + "\n", encoding="utf-8")
    if check and p.returncode != 0:
        raise SystemExit(f"fail {args} -> {p.returncode}")
    return p


msg = """fix(fo): drain scene-rebase bootstrap lock under physics pause (P0 ecology)

ProcessPending/TryPrepare no longer early-return on physics pause; TryFlush drives
ResumePhysicsAfterShift and stuck scene-rebase barrier completion so
IsOriginShiftBootstrapLocked can clear and FrostTick can mark ecology ready.
Headless wait-progress diag logs foLock/physicsPause/pendingScenes every 15s.
"""
msg_path = ROOT / "_agent_fo_drain_commit_msg.txt"
msg_path.write_text(msg, encoding="utf-8", newline="\n")
log.append(f"wrote msg bytes={msg_path.stat().st_size}")
OUT.write_text("\n".join(log) + "\n", encoding="utf-8")

run(["git", "rev-parse", "--show-toplevel"])
run(["git", "status", "-sb"])
run(
    [
        "git",
        "add",
        "--",
        "Assets/_Project/Scripts/HectonFloatingOrigin.cs",
        "Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs",
        "BACKLOG.md",
    ]
)
run(
    [
        "git",
        "add",
        "-f",
        "--",
        "Docs/AgentLogs/p0_fo_bootstrap_lock_drain_20260731.md",
    ]
)
names = [
    n
    for n in run(["git", "diff", "--cached", "--name-only"]).stdout.strip().splitlines()
    if n
]
allowed = {
    "Assets/_Project/Scripts/HectonFloatingOrigin.cs",
    "Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs",
    "BACKLOG.md",
    "Docs/AgentLogs/p0_fo_bootstrap_lock_drain_20260731.md",
}
bad = [n for n in names if n not in allowed]
if bad:
    run(["git", "reset", "HEAD"], check=False)
    raise SystemExit("pathspec pollution: " + repr(bad))

if not names:
    run(["git", "log", "-3", "--oneline"])
    run(["git", "status", "-sb"])
    log.append("nothing staged - check if already committed")
    OUT.write_text("\n".join(log) + "\n", encoding="utf-8")
else:
    run(["git", "diff", "--cached", "--stat"])
    run(["git", "commit", "-F", str(msg_path)])
    run(["git", "pull", "--ff-only", "origin", "main"])
    run(["git", "push", "origin", "main"])
    run(["git", "status", "-sb"])
    run(["git", "log", "-3", "--oneline"])
    run(["git", "rev-parse", "HEAD"])
    log.append("ALL_GIT_OK")
    OUT.write_text("\n".join(log) + "\n", encoding="utf-8")

print("DONE")
