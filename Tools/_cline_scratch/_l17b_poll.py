# -*- coding: utf-8 -*-
"""Poll L17b LIVE probe log + process."""
from pathlib import Path
import re
import subprocess
import time

LOG = Path(r"C:\hades\Hecton8\Docs\AgentLogs\h8_playprobe_v0_L17b.log")
ART = Path(r"C:\hades\Hecton8\Docs\AgentLogs\h8_playprobe_v0_L17b.json")
PIDF = Path(r"C:\hades\Hecton8\Tools\_cline_scratch\v0_L17b_pid.txt")
OUT = Path(r"C:\hades\Hecton8\Tools\_cline_scratch\_l17b_poll.txt")

pid = int(PIDF.read_text(encoding="utf-8").strip()) if PIDF.exists() else None
alive = False
if pid:
    r = subprocess.run(
        ["tasklist", "/FI", f"PID eq {pid}", "/FO", "CSV", "/NH"],
        capture_output=True, text=True, encoding="utf-8", errors="replace",
    )
    alive = str(pid) in (r.stdout or "")

lines = []
size = 0
if LOG.exists():
    size = LOG.stat().st_size
    t = LOG.read_text(encoding="utf-8", errors="replace")
    lines = t.splitlines()
else:
    t = ""

keys = (
    "FODRAIN", "SIMCLOCK", "INPUTHOP", "H8_INPUTHOP", "WORLDDRIVER",
    "movementIntent", "SWIM", "VERDICT", "ROUTE_DONE", "ROUTE_FAIL",
    "gameReady", "hop2", "Crash!!!", "lateFrameTick", "PLAYMODE",
    "gameplay-window", "SampleObservables", "intent01",
)
hits = {k: 0 for k in keys}
for l in lines:
    for k in keys:
        if k in l:
            hits[k] += 1

# last interesting
interesting = []
for i, l in enumerate(lines):
    if any(k in l for k in (
        "FODRAIN", "SIMCLOCK", "INPUTHOP", "WORLDDRIVER", "movementIntent",
        "SWIM", "VERDICT", "ROUTE_", "Crash!!!", "gameReady=", "hop2",
        "PlayModeProbe", "H8_PROBE",
    )):
        interesting.append((i, l[:280]))

# hop2 / intent extract
hop2s = re.findall(r"hop2=\w+", t)
readhops = re.findall(r"readHop=\d+", t)
intents = re.findall(r"movementIntent01max[=:][^\s,|]+", t, re.I)
late = re.findall(r"lateFrameTick=(\d+)", t)
fodrain = [l[:220] for l in lines if "FODRAIN" in l][-5:]
simclock = [l[:220] for l in lines if "SIMCLOCK" in l][-4:]
inputhops = [l[:300] for l in lines if "INPUTHOP" in l][-3:]

out = []
out.append(f"ts={time.strftime('%Y-%m-%d %H:%M:%S')}")
out.append(f"pid={pid} alive={alive}")
out.append(f"log_bytes={size} log_lines={len(lines)}")
out.append(f"artifact_exists={ART.exists()} artifact_bytes={ART.stat().st_size if ART.exists() else 0}")
out.append(f"hits={hits}")
out.append(f"hop2_matches={hop2s[:10]}")
out.append(f"readHop_matches={readhops[:10]}")
out.append(f"intent_matches={intents[:10]}")
out.append(f"lateFrameTick_samples={late[-15:]}")
out.append("--- FODRAIN tail ---")
out.extend(fodrain or ["(none)"])
out.append("--- SIMCLOCK tail ---")
out.extend(simclock or ["(none)"])
out.append("--- INPUTHOP tail ---")
out.extend(inputhops or ["(none)"])
out.append("--- last 12 interesting ---")
for i, l in interesting[-12:]:
    out.append(f"{i}:{l}")
out.append("--- last 8 non-burst ---")
nb = [l for l in lines if "BurstCache" not in l and "SymType" not in l and l.strip()]
for l in nb[-8:]:
    out.append(l[:260])

OUT.write_text("\n".join(out) + "\n", encoding="utf-8")
print("\n".join(out))
