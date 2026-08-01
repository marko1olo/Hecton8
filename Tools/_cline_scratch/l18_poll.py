import re
import subprocess
import time
from pathlib import Path

log = Path(r"C:\hades\Hecton8\Docs\AgentLogs\h8_playprobe_v0_L18.log")
out_path = Path(r"C:\hades\Hecton8\Tools\_cline_scratch\l18_poll_out.txt")

# optional sleep seconds from argv
import sys
sleep_s = int(sys.argv[1]) if len(sys.argv) > 1 else 0
if sleep_s > 0:
    time.sleep(sleep_s)

lines = []
def add(s=""):
    lines.append(s)

# unity alive?
r = subprocess.run(
    'tasklist /FI "IMAGENAME eq Unity.exe" /FO CSV /NH',
    shell=True, capture_output=True, text=True,
)
unity_alive = "Unity.exe" in (r.stdout or "")
add(f"unity_alive={unity_alive}")
add(f"tasklist={r.stdout.strip()[:200]}")

if not log.exists():
    add("LOG_MISSING")
    out_path.write_text("\n".join(lines), encoding="utf-8")
    print("\n".join(lines))
    raise SystemExit(0)

size = log.stat().st_size
mtime = log.stat().st_mtime
add(f"log_size={size} mtime={time.ctime(mtime)}")

# read tail efficiently
with open(log, "rb") as f:
    if size > 400_000:
        f.seek(-400_000, 2)
    raw = f.read()
text = raw.decode("utf-8", errors="replace")
all_text = None
# full text only if small enough for needle counts
if size < 8_000_000:
    all_text = log.read_text(encoding="utf-8", errors="replace")
else:
    all_text = text  # approximate from tail

needles = [
    "INPUTHOP", "FODRAIN", "SIMCLOCK", "WORLDDRIVER", "hop2",
    "lateFrameTick", "movementIntent", "SWIM", "VERDICT",
    "Crash!!!", "gameReady", "dilAfter", "stepBoundAfter",
    "MapMagic", "IncrementalAABB", "PLAYMODE", "Probe complete",
    "executeMethod", "error CS", "Exception",
]
add("---NEEDLE COUNTS (full or tail)---")
for n in needles:
    add(f"  {n}: {all_text.count(n)}")

# extract key lines
add("---KEY LINES---")
patterns = [
    r".*SIMCLOCK.*",
    r".*FODRAIN.*",
    r".*INPUTHOP.*",
    r".*movementIntent.*",
    r".*SWIM.*",
    r".*VERDICT.*",
    r".*Crash!!!.*",
    r".*WORLDDRIVER.*",
]
seen = set()
src = all_text.splitlines()
for pat in patterns:
    rx = re.compile(pat, re.I)
    hits = [ln for ln in src if rx.search(ln)]
    add(f"[{pat}] count={len(hits)}")
    for ln in hits[-5:]:
        s = ln.strip()
        if s not in seen:
            seen.add(s)
            add("  " + s[:300])

# last 30 lines
add("---TAIL30---")
for ln in src[-30:]:
    add(ln[:300])

out_path.write_text("\n".join(lines), encoding="utf-8")
print("\n".join(lines[:120]))
print("... total lines", len(lines), "wrote", out_path)
