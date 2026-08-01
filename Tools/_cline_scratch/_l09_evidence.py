# -*- coding: utf-8 -*-
import re, os
logp = r"C:\hades\Hecton8\Docs\AgentLogs\h8_playprobe_v0_L09.log"
outp = r"C:\hades\Hecton8\Tools\_cline_scratch\_l09_evidence.txt"
pats = re.compile(
    r"STARTERGRANT|H8_INPUTHOP|readHop=|MOMENT|Swim|movementIntent|"
    r"LocomotionHold|IsToolAvailable|INVENTORY|gridBound|refusalMask|"
    r"PlayerInventory|STORAGE UNAVAILABLE|DISABLED at Awake|TryRecover|"
    r"VERBSWEEP|LANECENSUS|publishOk|waitingOn|blockMask|ToolEquip|"
    r"FixedTickable|IFixedTick|RegisteredInput|NoOpInput|"
    r"STARTERGRANT applied|ABANDONED|CanService|hop census|"
    r"currentStateMove|inputEnabled",
    re.I,
)
out = []
counts = {}
with open(logp, encoding="utf-8", errors="replace") as f:
    for i, line in enumerate(f, 1):
        if pats.search(line):
            key = "other"
            for k in ["STARTERGRANT", "H8_INPUTHOP", "readHop", "Swim", "INVENTORY",
                      "STORAGE", "VERBSWEEP", "LANECENSUS", "LocomotionHold",
                      "ToolEquip", "MOMENT", "IsToolAvailable"]:
                if k.lower() in line.lower():
                    key = k
                    break
            counts[key] = counts.get(key, 0) + 1
            # keep first 8 and last 4 per key via staging
            out.append((key, i, line.rstrip()[:400]))

# summarize
lines = ["COUNTS:\n"]
for k, v in sorted(counts.items(), key=lambda x: -x[1]):
    lines.append("  %s: %d\n" % (k, v))

# group
from collections import defaultdict
g = defaultdict(list)
for key, i, line in out:
    g[key].append((i, line))

for key in sorted(g.keys()):
    items = g[key]
    lines.append("\n==== %s (%d) ====\n" % (key, len(items)))
    show = items[:12]
    if len(items) > 16:
        show = items[:8] + [("...", "...truncated...")] + items[-6:]
    for i, line in show:
        lines.append("%s|%s\n" % (i, line))

# also check L08 measured + sa critique
for p in [
    r"C:\hades\Hecton8\Docs\V0_Playtest\V0_L08_MEASURED.md",
    r"C:\hades\Hecton8\Tools\_cline_scratch\_sa_critique_l09.md",
    r"C:\hades\Hecton8\Tools\_cline_scratch\_l09_deep_out.txt",
]:
    if os.path.isfile(p):
        lines.append("\n##### FILE %s #####\n" % p)
        t = open(p, encoding="utf-8", errors="replace").read()
        lines.append(t[:12000])
        if len(t) > 12000:
            lines.append("\n...[trunc]...\n")

open(outp, "w", encoding="utf-8").writelines(lines)
print("wrote", outp, "hitrows", len(out))
