# -*- coding: utf-8 -*-
"""Extract L15 LIVE key lines from probe log."""
import os
import re
import subprocess
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

LOG = r"C:\hades\Hecton8\Docs\AgentLogs\h8_playprobe_v0_L15.log"
OUT = r"C:\hades\Hecton8\Tools\_cline_scratch\_l15_extract_final.txt"
PIDF = r"C:\hades\Hecton8\Tools\_cline_scratch\v0_L15_pid.txt"

pid = None
if os.path.isfile(PIDF):
    try:
        pid = int(open(PIDF, encoding="utf-8").read().strip())
    except Exception:
        pid = None

alive = False
if pid:
    r = subprocess.run(
        ["tasklist", "/FI", f"PID eq {pid}", "/NH"],
        capture_output=True,
        text=True,
        encoding="cp1251",
        errors="replace",
    )
    alive = str(pid) in (r.stdout or "")

sz = os.path.getsize(LOG) if os.path.isfile(LOG) else 0
# read full if < 8MB else tail
with open(LOG, "rb") as fh:
    if sz > 8_000_000:
        fh.seek(-8_000_000, os.SEEK_END)
    data = fh.read().decode("utf-8", errors="replace")

patterns = [
    r"movementIntent01max",
    r"INPUTHOP",
    r"hop2",
    r"readHop=",
    r"MOMENT",
    r"RESULT",
    r"Swim",
    r"immersion",
    r"span=",
    r"lastOverride",
    r"currentStateMove",
    r"DETERMINISM",
    r"dispatcherFrameId",
    r"FixedTick",
    r"registeredFixed",
    r"TryRegisterFixed",
    r"dual.register|L15|lane",
    r"EnsureGameplay",
    r"SampleGameplay",
    r"menu.*block|IsGameplayInputBlocked",
    r"HPM",
    r"HectonPlayerMovement",
    r"FAIL Swim|PASS Swim",
    r"depth",
    r"PHASE",
    r"VERBSWEEP",
    r"INPUTREFUSE",
    r"publishOk|overrideApplied",
]

key_re = re.compile("|".join(f"({p})" for p in patterns), re.I)
hits = []
for ln in data.splitlines():
    if key_re.search(ln):
        # skip pure DLL noise
        if "BurstCache" in ln or "SymType" in ln or ".dll:" in ln:
            continue
        if ln.strip().startswith("C:\\WINDOWS") or ln.strip().startswith("C:\\hades\\Hecton8\\Library"):
            continue
        hits.append(ln[:400])

# keep first 30 + last 80
if len(hits) > 120:
    selected = hits[:30] + ["...TRUNC..."] + hits[-80:]
else:
    selected = hits

# also pull explicit metrics
metrics = {}
for m in re.finditer(r"movementIntent01max=([0-9.]+)", data):
    metrics["intent"] = m.group(1)
for m in re.finditer(r"immersionMax=([0-9.]+)", data):
    metrics["immersion"] = m.group(1)
for m in re.finditer(r"span=([0-9.]+)m", data):
    metrics["span"] = m.group(1)
for m in re.finditer(r"lastOverrideMove=[(]([^)]+)[)]", data):
    metrics["lom"] = m.group(1)
for m in re.finditer(r"currentStateMove=[(]([^)]+)[)]", data):
    metrics["csm"] = m.group(1)
hops = sorted(set(int(x) for x in re.findall(r"readHop=([0-9]+)", data)))
metrics["hops"] = hops
metrics["swim_fail"] = len(re.findall(r"FAIL\s+Swim|MOMENT\s+FAIL\s+Swim", data))
metrics["swim_pass"] = len(re.findall(r"PASS\s+Swim|MOMENT\s+PASS\s+Swim", data))
rf = re.search(r"RESULT failures=([0-9]+)", data)
metrics["result_fail"] = rf.group(1) if rf else None

# hop2 ABSENT lines
hop2_lines = [ln[:300] for ln in data.splitlines() if re.search(r"hop2|ABSENT|INPUTHOP", ln, re.I) and "BurstCache" not in ln][-20:]

lines = []
lines.append(f"pid={pid} alive={alive} log_bytes={sz}")
lines.append(f"METRICS={metrics}")
lines.append("=== KEY HITS (selected) ===")
lines.extend(selected)
lines.append("=== HOP2/INPUTHOP TAIL ===")
lines.extend(hop2_lines)

# MOMENT / RESULT blocks
lines.append("=== MOMENT/RESULT ===")
for ln in data.splitlines():
    if re.search(r"MOMENT|RESULT failures|Required Route|Swim", ln) and "BurstCache" not in ln:
        if any(k in ln for k in ("MOMENT", "RESULT", "Swim", "intent", "hop", "depth", "immersion")):
            lines.append(ln[:400])

text = "\n".join(lines) + "\n"
open(OUT, "w", encoding="utf-8").write(text)
print(f"WROTE {OUT} lines={len(lines)}")
print(f"pid={pid} alive={alive} log_bytes={sz}")
print(f"METRICS={metrics}")
print("--- last 30 key ---")
for ln in selected[-30:]:
    print(ln[:240])
