# -*- coding: utf-8 -*-
import os
import re
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
OUT = r"C:\hades\Hecton8\Tools\_cline_scratch\_l17_gates.txt"
parts = []

def dump(path, start, end):
    with open(path, encoding="utf-8", errors="replace") as f:
        lines = f.readlines()
    parts.append(f"==== {path} {start}-{end} of {len(lines)} ====")
    for i in range(start - 1, min(end, len(lines))):
        parts.append(f"{i+1}|{lines[i].rstrip()}")

def grep(path, pat, ctx=2, limit=40):
    with open(path, encoding="utf-8", errors="replace") as f:
        lines = f.readlines()
    parts.append(f"==== GREP {pat} in {os.path.basename(path)} ====")
    n = 0
    for i, ln in enumerate(lines):
        if re.search(pat, ln):
            n += 1
            if n <= limit:
                a = max(0, i - ctx)
                b = min(len(lines), i + ctx + 1)
                for j in range(a, b):
                    mark = ">>" if j == i else "  "
                    parts.append(f"{mark}{j+1}|{lines[j].rstrip()[:200]}")
                parts.append("---")
    parts.append(f"(total matches {n})")

hpi = r"C:\hades\Hecton8\Assets\_Project\Scripts\Gameplay\HectonPlayerInputHandler.cs"
hpm = r"C:\hades\Hecton8\Assets\_Project\Scripts\HectonPlayerMovement.cs"
idis = r"C:\hades\Hecton8\Assets\_Project\Scripts\Core\InputDispatcher.cs"
iman = r"C:\hades\Hecton8\Assets\_Project\Scripts\Input\InputManager.cs"

dump(hpi, 1, 80)
grep(hpm, r"IsPlayerInputEnabled|TryReadFrame|ProcessPlayerInputFrame|SampleGameplayLocomotion")
grep(idis, r"IsPlayerInputEnabled|SwitchToPlayerInput|EnablePlayerInput|_playerInputEnabled|playerInputEnabled")
grep(iman, r"IsPlayerInputEnabled|SwitchToPlayerInput|EnablePlayerInput|_playerInputEnabled|playerActionMap\.enabled")

# property definitions
for p in [idis, iman, hpm]:
    grep(p, r"bool IsPlayerInputEnabled|IsPlayerInputEnabled\s*=>|IsPlayerInputEnabled\s*\{")

text = "\n".join(parts) + "\n"
with open(OUT, "w", encoding="utf-8") as f:
    f.write(text)
print(text[:20000])
print("WROTE", OUT, len(text))
