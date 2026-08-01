# -*- coding: utf-8 -*-
"""Deep extract L17 LIVE log for Swim gates and early exit cause."""
from pathlib import Path
import re

LOG = Path(r"C:\hades\Hecton8\Docs\AgentLogs\h8_playprobe_v0_L17.log")
OUT = Path(r"C:\hades\Hecton8\Tools\_cline_scratch\_l17_live_extract.txt")

t = LOG.read_text(encoding="utf-8", errors="replace")
lines = t.splitlines()

keys = (
    "FODRAIN",
    "SIMCLOCK",
    "INPUTHOP",
    "movementIntent01max",
    "WORLDDRIVER",
    "SWIM",
    "VERDICT",
    "ROUTE",
    "PLAYPROBE",
    "hop2",
    "hop1",
    "lateFrameTick",
    "Abort",
    "FATAL",
    "Crash",
    "exiting",
    "Quit",
    "timeout",
    "TIMEOUT",
    "error CS",
    "Compilation",
    "executeMethod",
    "Batchmode",
    "Cancel",
    "killed",
    "phase=",
    "gameplay",
    "Gameplay",
    "FAIL",
    "PASS",
)

hits = []
for i, l in enumerate(lines):
    if any(k in l for k in keys):
        hits.append(f"{i}:{l[:420]}")

# specific blocks
fod = [l for l in lines if "FODRAIN" in l]
sim = [l for l in lines if "SIMCLOCK" in l]
hop = [l for l in lines if "INPUTHOP" in l]
intent = [l for l in lines if "movementIntent01max" in l]
wd = [l for l in lines if "WORLDDRIVER" in l]
verdict = [l for l in lines if "VERDICT" in l or "SWIM" in l or "Swim" in l]
play = [l for l in lines if "[H8_PLAYPROBE]" in l]

# last 80 lines of log
tail = lines[-80:]

# first/last playprobe
play_first = play[:15]
play_last = play[-30:]

sections = []
sections.append(f"LOG_BYTES={len(t)} LINES={len(lines)}")
sections.append(f"FODRAIN count={len(fod)}")
for x in fod:
    sections.append("  " + x[:400])
sections.append(f"SIMCLOCK count={len(sim)}")
for x in sim:
    sections.append("  " + x[:400])
sections.append(f"INPUTHOP count={len(hop)}")
for x in hop:
    sections.append("  " + x[:500])
sections.append(f"INTENT count={len(intent)}")
for x in intent:
    sections.append("  " + x[:300])
sections.append(f"WORLDDRIVER count={len(wd)}")
for x in wd[:20]:
    sections.append("  " + x[:300])
sections.append(f"VERDICT/SWIM count={len(verdict)}")
for x in verdict:
    sections.append("  " + x[:300])
sections.append("--- PLAYPROBE first ---")
for x in play_first:
    sections.append("  " + x[:350])
sections.append("--- PLAYPROBE last ---")
for x in play_last:
    sections.append("  " + x[:350])
sections.append("--- LOG TAIL ---")
for x in tail:
    sections.append(x[:350])
sections.append("--- KEY HITS last 60 ---")
for x in hits[-60:]:
    sections.append(x)

OUT.write_text("\n".join(sections) + "\n", encoding="utf-8")
print(f"wrote {OUT} fod={len(fod)} sim={len(sim)} hop={len(hop)} intent={len(intent)} play={len(play)}")
print("\n".join(sections[:80]))
