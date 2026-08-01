# -*- coding: utf-8 -*-
"""Commit only L15 product files. Never print secrets."""
import subprocess
import sys
import os

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
REPO = r"C:\hades\Hecton8"
os.chdir(REPO)

def run(args, check=True):
    r = subprocess.run(args, capture_output=True, text=True, encoding="utf-8", errors="replace")
    out = (r.stdout or "") + (r.stderr or "")
    print(out)
    if check and r.returncode != 0:
        raise SystemExit("FAIL %s rc=%s" % (args, r.returncode))
    return r

# Unstage everything first (keep working tree)
run(["git", "reset", "HEAD"], check=False)

files = [
    "Assets/_Project/Scripts/Core/GlobalRegistry.cs",
    "Assets/_Project/Scripts/HectonPlayerMovement.cs",
    "Docs/V0_Playtest/V0_L14_LIVE_RESULTS.md",
    "Docs/V0_Playtest/V0_L15_DUAL_REGISTER_HEAL.md",
    "Docs/V0_Playtest/NEXT_CHAT_L15.md",
]
run(["git", "add", "--"] + files)
r = run(["git", "diff", "--cached", "--stat"])
r2 = run(["git", "diff", "--cached", "--name-only"])
names = [n.strip() for n in (r2.stdout or "").splitlines() if n.strip()]
print("STAGED_NAMES:", names)
allowed = set(files)
bad = [n for n in names if n.replace("\\", "/") not in allowed and n not in allowed]
if bad:
    print("REFUSING unexpected staged:", bad)
    raise SystemExit(2)

msg = (
    "fix(v0): heal dual-register desync so HPM FixedTick reaches hop2 (L15)\n\n"
    "L14 LIVE left hop2 ABSENT and movementIntent01max=0 despite healthy hop1 overrides.\n"
    "GlobalRegistry.TryRegisterFixedTickable/Updatable/Cold returned false when the global\n"
    "bucket already Contained the item, without healing a missing SystemDispatcher lane.\n"
    "HPM sticky flags trusted registration without verifying lane membership.\n\n"
    "Heal: if global Contains, still ensure dispatcher lane; HPM Ensure clears sticky when\n"
    "lane missing and re-TryRegisters. No Unregister thrash. Docs: L14 live results + L15.\n"
    "Swim PASS still requires LIVE probe (hop2 + intent>0)."
)
msg_path = os.path.join(REPO, "Tools", "_cline_scratch", "_commit_msg_l15.txt")
with open(msg_path, "w", encoding="utf-8", newline="\n") as f:
    f.write(msg)

r = subprocess.run(
    ["git", "commit", "-F", msg_path],
    capture_output=True,
    text=True,
    encoding="utf-8",
    errors="replace",
)
# Print commit result but redact token-like strings
text = (r.stdout or "") + (r.stderr or "")
import re
text = re.sub(r"glpat-[A-Za-z0-9_-]+", "glpat-REDACTED", text)
text = re.sub(r"://[^:@\s]+:[^@\s]+@", "://REDACTED@", text)
print(text)
print("commit_rc", r.returncode)
if r.returncode != 0:
    raise SystemExit(r.returncode)

r = run(["git", "rev-parse", "HEAD"])
r = run(["git", "log", "-1", "--oneline"])
print("COMMIT_OK")
