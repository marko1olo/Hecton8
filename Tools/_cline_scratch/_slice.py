# -*- coding: utf-8 -*-
"""Slice line ranges from large C# files for analysis."""
import os
import sys

slices = [
    (r"C:\hades\Hecton8\Assets\_Project\Scripts\HectonPlayerMovement.cs", [
        (1900, 1920),
        (2980, 3020),
        (8085, 8260),
        (9905, 9960),
        (10085, 10150),
    ]),
    (r"C:\hades\Hecton8\Assets\_Project\Scripts\PlayerToolManager.cs", [
        (40, 55),
        (160, 175),
        (320, 380),
        (385, 510),
        (940, 970),
        (1270, 1560),
    ]),
    (r"C:\hades\Hecton8\Assets\_Project\Scripts\PlayerInventory.cs", [
        (1540, 1720),
        (2370, 2650),
    ]),
    (r"C:\hades\Hecton8\Assets\_Project\Scripts\Gameplay\HectonPlayerInputHandler.cs", [
        (1, 80),
    ]),
]

# Also search LocomotionHold across Editor diagnostics
search_roots = [
    r"C:\hades\Hecton8\Assets\_Project\Scripts",
]
hold_hits = []
for root in search_roots:
    for dp, _, fs in os.walk(root):
        for f in fs:
            if not f.endswith(".cs"):
                continue
            path = os.path.join(dp, f)
            with open(path, "r", encoding="utf-8", errors="replace") as fh:
                for i, line in enumerate(fh, 1):
                    if "LocomotionHold" in line or "locomotionHold" in line:
                        hold_hits.append("%s:%d: %s" % (path, i, line.rstrip()[:240]))

out = []
out.append("=== LOCOMOTION HOLD HITS ===")
out.extend(hold_hits if hold_hits else ["(none)"])
out.append("")

for path, ranges in slices:
    out.append("===== %s =====" % path)
    if not os.path.isfile(path):
        out.append("MISSING")
        continue
    with open(path, "r", encoding="utf-8", errors="replace") as fh:
        lines = fh.readlines()
    for a, b in ranges:
        out.append("--- lines %d-%d ---" % (a, b))
        for i in range(a, min(b, len(lines)) + 1):
            out.append("%6d|%s" % (i, lines[i - 1].rstrip("\n")))
        out.append("")

# Also dump TryReadFrame from input handler via search
ih = r"C:\hades\Hecton8\Assets\_Project\Scripts\Gameplay\HectonPlayerInputHandler.cs"
if os.path.isfile(ih):
    with open(ih, "r", encoding="utf-8", errors="replace") as fh:
        lines = fh.readlines()
    out.append("===== HectonPlayerInputHandler full (%d lines) =====" % len(lines))
    for i, line in enumerate(lines, 1):
        if any(k in line for k in ("TryReadFrame", "MoveDelta", "class ", "GetState", "IInput", "readHop", "CurrentInput")):
            # print context window
            for j in range(max(1, i - 2), min(len(lines), i + 25) + 1):
                out.append("%6d|%s" % (j, lines[j - 1].rstrip("\n")))
            out.append(" ...")

text = "\n".join(out) + "\n"
outp = r"C:\hades\Hecton8\Tools\_cline_scratch\_slices_out.txt"
with open(outp, "w", encoding="utf-8") as fh:
    fh.write(text)
print("wrote", outp, "chars", len(text), "hold_hits", len(hold_hits))
# print first chunk
print(text[:25000])
