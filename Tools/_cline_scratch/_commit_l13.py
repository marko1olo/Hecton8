# -*- coding: utf-8 -*-
import subprocess
import sys
from pathlib import Path

REPO = Path(r"C:\hades\Hecton8")
MSG = REPO / "Tools" / "_cline_scratch" / "_commit_msg_l13.txt"
files = [
    "Assets/_Project/Scripts/HectonPlayerMovement.cs",
    "Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessWorldDriver.cs",
    "Docs/V0_Playtest/V0_L13_FIXEDTICK_SAMPLE_BEFORE_SUIT.md",
    "Docs/V0_Playtest/NEXT_CHAT_L13.md",
]


def run(args):
    print(">>", " ".join(args))
    r = subprocess.run(
        args,
        cwd=str(REPO),
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    if r.stdout:
        print(r.stdout)
    if r.stderr:
        print(r.stderr, file=sys.stderr)
    print("rc=", r.returncode)
    return r.returncode


for f in files:
    p = REPO / f
    print("exists", f, p.exists(), "size", p.stat().st_size if p.exists() else 0)

# Drop accidental index junk from prior sessions; keep working tree.
run(["git", "reset", "HEAD", "--", "."])
run(["git", "add", "--"] + files)
run(["git", "status", "--short", "--"] + files)
rc = run(["git", "commit", "-F", str(MSG)])
run(["git", "log", "-1", "--oneline"])
run(["git", "status", "-sb"])
sys.exit(0 if rc == 0 else rc)
