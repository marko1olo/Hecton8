# -*- coding: utf-8 -*-
import re
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
LOG = r"C:\hades\Hecton8\Docs\AgentLogs\h8_playprobe_v0_L16.log"
OUT = r"C:\hades\Hecton8\Tools\_cline_scratch\_l16_swim_full.txt"

with open(LOG, encoding="utf-8", errors="replace") as f:
    t = f.read()

out = []
for ln in t.splitlines():
    if "MOMENT" in ln and "Swim" in ln:
        out.append("SWIM_FULL:")
        out.append(ln)
        out.append("---")
for ln in t.splitlines():
    if "INPUTHOP" in ln:
        out.append("HOP_FULL:")
        out.append(ln)
        out.append("---")

keys = [
    "SampleGameplay",
    "IsGameplayInputBlocked",
    "FixedTick",
    "ProcessPlayerInput",
    "TryReadFrame",
    "HectonPlayerMovement",
    "no HPM",
    "HPM missing",
    "playerMovement",
    "locomotionIntent",
    "intent01",
    "movementIntent",
    "suitReady",
    "sticky",
    "dual-register",
    "GlobalRegistry",
    "DispatchFixed",
    "RunFixedStep",
    "accumulator",
    "stepBound",
    "presimSubsteps",
    "lateFrame",
    "readHop=",
    "hopAbsent",
    "INPUT_PATH",
    "locomotion sample",
    "blockedByMenu",
    "fadeActive",
    "cutscene",
    "playerLane",
    "registryLane",
]
for key in keys:
    xs = [ln for ln in t.splitlines() if key in ln]
    if xs:
        out.append(f"KEY[{key}] n={len(xs)}")
        for ln in xs[:5]:
            out.append("  " + ln[:350])

# Also dump SwimVerdict adjacent context: 5 lines before MOMENT FAIL Swim
idx = None
lines = t.splitlines()
for i, ln in enumerate(lines):
    if "MOMENT" in ln and "FAIL" in ln and "Swim" in ln:
        idx = i
        break
if idx is not None:
    out.append("SWIM_CONTEXT:")
    for ln in lines[max(0, idx - 30) : idx + 5]:
        out.append(ln[:400])

text = "\n".join(out) + "\n"
with open(OUT, "w", encoding="utf-8") as f:
    f.write(text)
print(text[:18000])
print("WROTE", OUT, "chars", len(text))
