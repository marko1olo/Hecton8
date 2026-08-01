# -*- coding: utf-8 -*-
import re
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
OUT = r"C:\hades\Hecton8\Tools\_cline_scratch\_l17_enabled_dig.txt"
parts = []

def dump(path, start, end):
    with open(path, encoding="utf-8", errors="replace") as f:
        lines = f.readlines()
    parts.append(f"==== {path} L{start}-{end} ({len(lines)} total) ====")
    for i in range(start - 1, min(end, len(lines))):
        parts.append(f"{i+1}|{lines[i].rstrip()}")

def grep(path, pat, ctx=3, limit=30):
    with open(path, encoding="utf-8", errors="replace") as f:
        lines = f.readlines()
    parts.append(f"==== GREP /{pat}/ {path} ====")
    n = 0
    for i, ln in enumerate(lines):
        if re.search(pat, ln):
            n += 1
            if n <= limit:
                for j in range(max(0, i - ctx), min(len(lines), i + ctx + 1)):
                    m = ">>" if j == i else "  "
                    parts.append(f"{m}{j+1}|{lines[j].rstrip()[:220]}")
                parts.append("---")
    parts.append(f"(matches={n})")

probe = r"C:\hades\Hecton8\Assets\_Project\Scripts\Editor\Diagnostics\H8_HeadlessPlayModeProbe.cs"
driver = r"C:\hades\Hecton8\Assets\_Project\Scripts\Editor\Diagnostics\H8_HeadlessWorldDriver.cs"
hpm = r"C:\hades\Hecton8\Assets\_Project\Scripts\HectonPlayerMovement.cs"
idis = r"C:\hades\Hecton8\Assets\_Project\Scripts\Core\InputDispatcher.cs"
iman = r"C:\hades\Hecton8\Assets\_Project\Scripts\Input\InputManager.cs"

# Probe Swim metrics
grep(probe, r"inputEnabled|switchToPlayerInput|IsPlayerInputEnabled|movementIntent01")
grep(driver, r"inputEnabled|switchToPlayerInput|IsPlayerInputEnabled|SwitchToPlayerInput")

# ProcessPlayerInputFrame full
dump(hpm, 8145, 8205)
dump(hpm, 8223, 8260)
dump(hpm, 7980, 8080)

# InputManager SwitchToPlayerInput + Enable + TryGetActionMapEnabled
dump(iman, 1019, 1080)
dump(iman, 2985, 3040)
dump(iman, 450, 480)

# InputDispatcher SwitchToPlayerInput + CaptureState override path vs IsPlayerInputEnabled
dump(idis, 3230, 3260)
grep(idis, r"ApplyAutomationOverride|TryConsumeLatestInputOverride|IsPlayerInputEnabled")

# Does CaptureState require enabled map?
grep(idis, r"void CaptureState|CaptureState\(")
# find CaptureState method body start
with open(idis, encoding="utf-8", errors="replace") as f:
    lines = f.readlines()
for i, ln in enumerate(lines):
    if re.search(r"void CaptureState\b|private void CaptureState", ln):
        dump(idis, i + 1, i + 120)
        break

text = "\n".join(parts) + "\n"
with open(OUT, "w", encoding="utf-8") as f:
    f.write(text)
print(text[:18000])
print("WROTE", OUT, len(text))
