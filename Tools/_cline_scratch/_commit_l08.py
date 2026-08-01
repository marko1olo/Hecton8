# -*- coding: utf-8 -*-
import os
import subprocess
import sys

REPO = r"C:\hades\Hecton8"
os.chdir(REPO)
OUT = os.path.join(REPO, "Tools", "_cline_scratch", "_commit_l08_out.txt")
lines = []


def log(s=""):
    lines.append(str(s))
    print(s)
    sys.stdout.flush()


def run(cmd, check=False):
    log("$ " + " ".join(cmd))
    p = subprocess.run(cmd, capture_output=True, text=True)
    if p.stdout:
        log(p.stdout.rstrip())
    if p.stderr:
        log(p.stderr.rstrip())
    log(f"exit={p.returncode}")
    if check and p.returncode != 0:
        raise SystemExit(p.returncode)
    return p


ALLOW = [
    "Assets/_Project/Scripts/HectonPlayerMovement.cs",
    "Assets/_Project/Scripts/PlayerToolManager.cs",
    "Docs/AgentLogs/V0_L08_MEASURED.md",
]

for f in ALLOW:
    path = os.path.join(REPO, f)
    log(f"exists {f}={os.path.isfile(path)} size={os.path.getsize(path) if os.path.isfile(path) else -1}")

run(["git", "rev-parse", "--show-toplevel"])
run(["git", "status", "-sb"])
run(["git", "remote", "-v"])

for f in ALLOW:
    p = run(["git", "add", "--", f])
    if p.returncode != 0:
        run(["git", "check-ignore", "-v", f])

run(["git", "diff", "--cached", "--stat"])
run(["git", "diff", "--cached", "--name-only"])

MSG = """fix(gameplay): sample locomotion input + starter grant on FixedTick (L08)

L08 measured movementIntent01max=0 and tool slots=0 despite publishOk>0.
Root cause: ProcessPlayerInputFrame and RetryRuntimeStartToolGrantIfPending
only ran on render IUpdatable.Tick; batchmode FixedTick path never consumed
published input or retried vault-gated grants.

- HectonPlayerMovement: SampleGameplayLocomotionInputForFixedStep at FixedTick
- PlayerToolManager: IFixedTickable + FixedTick grant retry
- Docs: V0_L08_MEASURED ledger (PASS/FAIL, gaps, L09 acceptance)

No mocks. HeadlessSimulationRunner and agent scratch not included.
"""

msg_path = os.path.join(REPO, "Tools", "_cline_scratch", "_commit_msg_l08.txt")
with open(msg_path, "w", encoding="utf-8", newline="\n") as fh:
    fh.write(MSG)

# only commit if something staged
p = run(["git", "diff", "--cached", "--quiet"])
if p.returncode == 0:
    log("NOTHING STAGED - abort commit")
else:
    p = run(["git", "commit", "-F", msg_path])
    if p.returncode == 0:
        run(["git", "log", "-1", "--oneline"])
        # push to whatever tracks main
        remotes = subprocess.run(["git", "remote"], capture_output=True, text=True).stdout.split()
        log(f"remotes={remotes}")
        pushed = False
        for r in ("origin", "gitlab"):
            if r in remotes:
                p2 = run(["git", "push", r, "main"])
                if p2.returncode == 0:
                    pushed = True
                    break
        if not pushed and remotes:
            run(["git", "push", remotes[0], "main"])
        run(["git", "pull", "--ff-only"])
        run(["git", "status", "-sb"])
        run(["git", "log", "-3", "--oneline"])

with open(OUT, "w", encoding="utf-8") as fh:
    fh.write("\n".join(lines) + "\n")
log(f"WROTE {OUT}")
