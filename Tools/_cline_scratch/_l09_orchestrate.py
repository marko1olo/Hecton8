# -*- coding: utf-8 -*-
"""Relocate L08 ledger to tracked Docs path, push gitlab, create L09 bat, launch probe."""
import os
import re
import shutil
import subprocess
import sys
import time

REPO = r"C:\hades\Hecton8"
os.chdir(REPO)
OUT = os.path.join(REPO, "Tools", "_cline_scratch", "_l09_orchestrate_out.txt")
lines = []


def log(s=""):
    lines.append(str(s))
    try:
        print(s)
        sys.stdout.flush()
    except Exception:
        pass


def run(cmd, check=False, timeout=None):
    log("$ " + " ".join(cmd) if isinstance(cmd, list) else "$ " + str(cmd))
    p = subprocess.run(
        cmd,
        capture_output=True,
        text=True,
        timeout=timeout,
        shell=isinstance(cmd, str),
    )
    if p.stdout:
        log(p.stdout.rstrip()[:8000])
    if p.stderr:
        log(p.stderr.rstrip()[:4000])
    log(f"exit={p.returncode}")
    return p


# --- 1. gitignore / Docs layout ---
gi = open(".gitignore", encoding="utf-8").read().splitlines()
for i, l in enumerate(gi):
    if any(k in l for k in ("Docs", "AgentLog", "PLAYTEST", "Screenshot", "Ledger", "V0")):
        log(f"gi{i+1}:{l}")

docs = os.listdir("Docs") if os.path.isdir("Docs") else []
log("Docs entries: " + ", ".join(docs[:60]))

# Prefer tracked path under Docs that is NOT AgentLogs
candidates = [
    "Docs/V0_Playtest",
    "Docs/QA",
    "Docs/Architecture",
    "Docs/Playtest",
]
ledger_dir = None
for c in candidates:
    # create if needed; check ignore
    os.makedirs(c, exist_ok=True)
    test = os.path.join(c, "_ignore_probe.md")
    with open(test, "w", encoding="utf-8") as fh:
        fh.write("probe\n")
    p = run(["git", "check-ignore", "-v", test.replace("\\", "/")])
    try:
        os.remove(test)
    except OSError:
        pass
    if p.returncode != 0:
        # not ignored
        ledger_dir = c
        log(f"LEDGER_DIR={c} (not ignored)")
        break
    else:
        log(f"ignored: {c}")

if ledger_dir is None:
    # force under Docs root file (might work if only AgentLogs ignored)
    ledger_dir = "Docs"
    log("fallback ledger_dir=Docs")

src = os.path.join(REPO, "Docs", "AgentLogs", "V0_L08_MEASURED.md")
dst = os.path.join(REPO, ledger_dir, "V0_L08_MEASURED.md")
if os.path.isfile(src):
    shutil.copy2(src, dst)
    log(f"copied ledger {src} -> {dst}")
else:
    log(f"SRC MISSING {src}")

# also write short pointer in Docs if different
if os.path.isfile(dst):
    run(["git", "add", "-f", "--", dst.replace("\\", "/")] if "AgentLogs" in dst else ["git", "add", "--", dst.replace("\\", "/")])
    # if still ignored, force
    p = run(["git", "diff", "--cached", "--name-only"])
    if "V0_L08_MEASURED" not in (p.stdout or ""):
        run(["git", "add", "-f", "--", dst.replace("\\", "/")])

run(["git", "diff", "--cached", "--stat"])
p = run(["git", "diff", "--cached", "--quiet"])
if p.returncode != 0:
    msg_path = os.path.join(REPO, "Tools", "_cline_scratch", "_commit_msg_ledger.txt")
    with open(msg_path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(
            "docs(v0): L08 MEASURED ledger (swim/tools FixedTick root causes)\n\n"
            "Records L08 PASS/FAIL matrix, product fixes in 5b8c23aba, L09 acceptance.\n"
        )
    run(["git", "commit", "-F", msg_path])
    run(["git", "log", "-1", "--oneline"])
else:
    log("no ledger staged")

# --- 2. push gitlab + origin ---
run(["git", "push", "origin", "main"])
run(["git", "push", "gitlab", "main"])
# pull with stash of local dirt
run(["git", "stash", "push", "-u", "-m", "agent-temp-before-pull", "--", 
     "Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs"])
# stash may fail on untracked; try pull
p = run(["git", "pull", "--ff-only", "origin", "main"])
if p.returncode != 0:
    run(["git", "fetch", "origin"])
    run(["git", "status", "-sb"])
run(["git", "log", "-3", "--oneline"])
run(["git", "status", "-sb"])

# --- 3. L08 bat -> L09 ---
bat_l08 = os.path.join(REPO, "Tools", "_cline_scratch", "launch_v0_L08_inputfix_probe.bat")
bat_l09 = os.path.join(REPO, "Tools", "_cline_scratch", "launch_v0_L09_fixedtick_probe.bat")
if os.path.isfile(bat_l08):
    text = open(bat_l08, encoding="utf-8", errors="replace").read()
    log(f"L08 bat bytes={len(text)}")
    # show key lines
    for i, line in enumerate(text.splitlines()):
        if any(k in line.lower() for k in ("l08", "probe", "unity", "log", "screenshot", "batch", "execute", "playmode", "h8_")):
            log(f"batL{i+1}: {line}")
    text2 = text
    text2 = text2.replace("L08", "L09")
    text2 = text2.replace("l08", "l09")
    text2 = text2.replace("inputfix", "fixedtick")
    # ensure log name
    text2 = re.sub(r"h8_playprobe_v0_L0\d", "h8_playprobe_v0_L09", text2, flags=re.I)
    with open(bat_l09, "w", encoding="utf-8", newline="\r\n") as fh:
        fh.write(text2)
    log(f"WROTE {bat_l09}")
else:
    log(f"L08 bat MISSING: {bat_l08}")
    # list scratch
    scratch = os.path.join(REPO, "Tools", "_cline_scratch")
    if os.path.isdir(scratch):
        for n in sorted(os.listdir(scratch))[:80]:
            log(f"scratch: {n}")

# --- 4. Find unity and how probes launch ---
# search for launch scripts
for root, dirs, files in os.walk(os.path.join(REPO, "Tools")):
    dirs[:] = [d for d in dirs if d not in (".git", "Library", "Temp")]
    for f in files:
        if f.lower().endswith((".bat", ".ps1", ".cmd")) and any(
            k in f.lower() for k in ("launch", "probe", "v0", "play")
        ):
            log(f"toolscript: {os.path.join(root, f)}")

with open(OUT, "w", encoding="utf-8") as fh:
    fh.write("\n".join(lines) + "\n")
log(f"WROTE {OUT}")
