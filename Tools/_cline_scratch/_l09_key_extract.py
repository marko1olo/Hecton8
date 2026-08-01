# -*- coding: utf-8 -*-
import os
log = r"C:\hades\Hecton8\Docs\AgentLogs\h8_playprobe_v0_L09.log"
outp = r"C:\hades\Hecton8\Tools\_cline_scratch\_l09_key_lines.txt"
out = []
with open(log, "r", encoding="utf-8", errors="replace") as f:
    for i, l in enumerate(f, 1):
        keep = False
        if "MOMENT" in l and any(k in l for k in ("Swim", "Tool", "RESULT", "MOMENTS pass", "SaveLoad", "Boot", "WorldLoad")):
            keep = True
        if any(k in l for k in (
            "STORAGE UNAVAILABLE", "TryRecover", "INPUTHOP", "STARTERGRANT",
            "LANECENSUS", "VERBSWEEP complete", "LocomotionHold", "movementIntent",
            "CanService", "SampleGameplay", "readHop=",
        )):
            keep = True
        if keep:
            out.append("L%d: %s" % (i, l.rstrip()[:1000]))
text = "\n".join(out) + "\n"
with open(outp, "w", encoding="utf-8") as fh:
    fh.write(text)
print("wrote", outp, "n=", len(out), "chars=", len(text))
print(text[:14000])
