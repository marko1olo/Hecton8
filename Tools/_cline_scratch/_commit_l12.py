# -*- coding: utf-8 -*-
import subprocess
import sys
from pathlib import Path

REPO = Path(r"C:\hades\Hecton8")
MSG = REPO / "Tools" / "_cline_scratch" / "_commit_msg_l12.txt"
MSG.write_text(
    "fix(v0): driver Tick publish after AdvancePhase (L12)\n"
    "\n"
    "HeadlessWorldDriver published locomotion intent before AdvancePhase,\n"
    "so hold ticks shipped the prior frame intent (often zero) while phase\n"
    "bodies had already written MoveDelta. Consume is destructive; zero\n"
    "publishes poisoned CaptureState for Swim.\n"
    "\n"
    "- SampleObservables -> AdvancePhase -> PublishLocomotionIntent\n"
    "- PhaseAuthorsInputIntent post-publish clear (no exit-tick zero)\n"
    "- Drop SwimDive/ToolUse/VerbSweep clear-before-publish\n"
    "- VerbSweep two-step comment matches same-tick publish\n"
    "\n"
    "Probe verify pending (UnityLockfile). Docs: V0_L12_TICK_PUBLISH_ORDER.md\n",
    encoding="utf-8",
)

files = [
    "Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessWorldDriver.cs",
    "Docs/AgentLogs/V0_L12_TICK_PUBLISH_ORDER.md",
    "Docs/AgentLogs/NEXT_CHAT_L12.md",
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

run(["git", "add", "--"] + files)
run(["git", "status", "--short", "--"] + files)
rc = run(["git", "commit", "-F", str(MSG)])
run(["git", "log", "-1", "--oneline"])
run(["git", "status", "-sb"])
sys.exit(0 if rc == 0 else rc)
