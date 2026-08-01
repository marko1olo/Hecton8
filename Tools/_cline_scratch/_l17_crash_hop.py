# -*- coding: utf-8 -*-
from pathlib import Path
import re

LOG = Path(r"C:\hades\Hecton8\Docs\AgentLogs\h8_playprobe_v0_L17.log")
OUT = Path(r"C:\hades\Hecton8\Tools\_cline_scratch\_l17_crash_hop.txt")
t = LOG.read_text(encoding="utf-8", errors="replace")
lines = t.splitlines()

out = []
# Full INPUTHOP lines (may be long)
for i, l in enumerate(lines):
    if "INPUTHOP" in l or "H8_INPUTHOP" in l:
        out.append(f"HOP_LINE {i} len={len(l)}")
        # wrap every 200 chars
        for j in range(0, len(l), 200):
            out.append(f"  {l[j:j+200]}")

# hop2 explicit
for pat in [r"hop2=\w+", r"readHop=\d+", r"hop2[^\n]{0,80}"]:
    ms = re.findall(pat, t)
    out.append(f"PAT {pat}: {ms[:20]}")

# Crash / stack near end - search backward for Crash|Fatal|Stacktrace|Obtained|error
crash_keys = ("Crash!!!", "Fatal", "Stacktrace", "Obtained", "Received signal",
              "abort", "Access violation", "0xC000", "Segmentation", "Unity Editor",
              "Exiting batchmode", "executeMethod", "Batchmode quit", "Canceling",
              "DisplayProgressbar", "HandleCrash", "crash report")
idx = []
for i, l in enumerate(lines):
    low = l.lower()
    if any(k.lower() in low for k in crash_keys):
        idx.append(i)

out.append(f"CRASH_KEY_LINES count={len(idx)} last={idx[-10:] if idx else []}")
for i in idx[-30:]:
    out.append(f"{i}:{lines[i][:300]}")

# context around last crash-ish line
if idx:
    c = idx[-1]
    out.append(f"--- context around {c} ---")
    for j in range(max(0, c - 15), min(len(lines), c + 25)):
        out.append(f"{j}:{lines[j][:300]}")

# last non-burst lines
out.append("--- last 40 non-burst ---")
nb = [l for l in lines if "BurstCache" not in l and "SymType" not in l]
for l in nb[-40:]:
    out.append(l[:300])

OUT.write_text("\n".join(out) + "\n", encoding="utf-8")
print("\n".join(out[:100]))
print("...")
print("\n".join(out[-40:]))
