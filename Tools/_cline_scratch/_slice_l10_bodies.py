# -*- coding: utf-8 -*-
import os, re

out = []
def sl(path, ranges):
    lines = open(path, encoding="utf-8", errors="replace").read().splitlines()
    for a, b in ranges:
        out.append("\n=== %s:%d-%d ===\n" % (os.path.basename(path), a, b))
        for i in range(a, min(b, len(lines)) + 1):
            out.append("%5d|%s\n" % (i, lines[i - 1]))

sl(r"C:\hades\Hecton8\Assets\_Project\Scripts\HectonPlayerMovement.cs", [
    (8007, 8075),
    (6040, 6080),
    (9765, 9805),
    (8055, 8080),
])
sl(r"C:\hades\Hecton8\Assets\_Project\Scripts\Gameplay\HectonPlayerInputHandler.cs", [
    (1, 120),
])
sl(r"C:\hades\Hecton8\Assets\_Project\Scripts\PlayerToolManager.cs", [
    (1290, 1560),
    (1620, 1660),
])
sl(r"C:\hades\Hecton8\Assets\_Project\Scripts\PlayerInventory.cs", [
    (1659, 1750),
    (2465, 2630),
    (1240, 1320),
])

# L09 log key lines
logp = r"C:\hades\Hecton8\Docs\AgentLogs\h8_playprobe_v0_L09.log"
if os.path.isfile(logp):
    pats = re.compile(
        r"STARTERGRANT|INPUTHOP|readHop|Swim|movementIntent|LocomotionHold|"
        r"IsToolAvailable|INVENTORY|gridBound|refusalMask|FixedTick|"
        r"SampleGameplay|PlayerInventory|DISABLED at Awake|TryRecover|"
        r"VERBSWEEP|LANECENSUS|publishOk|waitingOn|blockMask",
        re.I,
    )
    out.append("\n##### L09 LOG HITS #####\n")
    with open(logp, encoding="utf-8", errors="replace") as f:
        for i, line in enumerate(f, 1):
            if pats.search(line):
                out.append("%d|%s" % (i, line if line.endswith("\n") else line + "\n"))
                if len(out) > 800:
                    out.append("...truncated hits...\n")
                    break

opath = r"C:\hades\Hecton8\Tools\_cline_scratch\_l10_bodies.txt"
open(opath, "w", encoding="utf-8").writelines(out)
print("wrote", opath, "parts", len(out))
