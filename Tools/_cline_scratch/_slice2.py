# -*- coding: utf-8 -*-
import os

out = []

def dump_range(path, a, b, label=None):
    out.append("===== %s %s =====" % (path, label or ("%d-%d" % (a, b))))
    with open(path, "r", encoding="utf-8", errors="replace") as fh:
        lines = fh.readlines()
    for i in range(a, min(b, len(lines)) + 1):
        out.append("%6d|%s" % (i, lines[i - 1].rstrip("\n")))
    out.append("")

def dump_matches(path, keys, ctx=8, max_hits=40):
    out.append("===== MATCH %s keys=%s =====" % (path, keys))
    with open(path, "r", encoding="utf-8", errors="replace") as fh:
        lines = fh.readlines()
    n = 0
    for i, line in enumerate(lines, 1):
        if any(k in line for k in keys):
            n += 1
            if n > max_hits:
                out.append("... truncated")
                break
            lo = max(1, i - ctx)
            hi = min(len(lines), i + ctx)
            out.append("--- hit L%d ---" % i)
            for j in range(lo, hi + 1):
                out.append("%6d|%s" % (j, lines[j - 1].rstrip("\n")))
            out.append("")

# Input handler
ih = r"C:\hades\Hecton8\Assets\_Project\Scripts\Gameplay\HectonPlayerInputHandler.cs"
if os.path.isfile(ih):
    with open(ih, "r", encoding="utf-8", errors="replace") as fh:
        lines = fh.readlines()
    out.append("===== HectonPlayerInputHandler (%d lines) FULL =====" % len(lines))
    for i, line in enumerate(lines, 1):
        out.append("%6d|%s" % (i, line.rstrip("\n")))
else:
    # find it
    for dp, _, fs in os.walk(r"C:\hades\Hecton8\Assets"):
        for f in fs:
            if f == "HectonPlayerInputHandler.cs":
                out.append("FOUND " + os.path.join(dp, f))

# World driver hold + intent sample
wd = r"C:\hades\Hecton8\Assets\_Project\Scripts\Editor\Diagnostics\H8_HeadlessWorldDriver.cs"
dump_matches(wd, ["LocomotionHold", "movementIntent", "CurrentMovementIntent", "MoveDelta", "waitingOn"], ctx=12, max_hits=25)

# Inventory vault bind / CanService / TryRecover
pi = r"C:\hades\Hecton8\Assets\_Project\Scripts\PlayerInventory.cs"
dump_range(pi, 1540, 1720)
dump_range(pi, 2370, 2650)

# HPM ResolveInputManagerBinding + IsGameplayInputBlockedByMenu
hpm = r"C:\hades\Hecton8\Assets\_Project\Scripts\HectonPlayerMovement.cs"
dump_matches(hpm, ["ResolveInputManagerBinding", "IsGameplayInputBlockedByMenu", "IsAuthoritativeVehicleTransport", "_inputManager"], ctx=15, max_hits=20)

# PTM unregister early-out hole confirmed
ptm = r"C:\hades\Hecton8\Assets\_Project\Scripts\PlayerToolManager.cs"
dump_range(ptm, 328, 380)

# STORAGE UNAVAILABLE in L09 log
log = r"C:\hades\Hecton8\Docs\AgentLogs\h8_playprobe_v0_L09.log"
out.append("===== L09 STORAGE / recover / grant giveup =====")
with open(log, "r", encoding="utf-8", errors="replace") as fh:
    for i, line in enumerate(fh, 1):
        if any(k in line for k in ("STORAGE UNAVAILABLE", "STARTERGRANT", "give up", "GiveUp", "TryRecover", "inventoryVersion", "stackLane", "BindRuntime", "gridBound", "CanService")):
            out.append("L%d: %s" % (i, line.rstrip()[:500]))

text = "\n".join(out) + "\n"
# ascii-safe write
outp = r"C:\hades\Hecton8\Tools\_cline_scratch\_slice2_out.txt"
with open(outp, "w", encoding="utf-8") as fh:
    fh.write(text)
print("wrote", outp, "chars", len(text))
