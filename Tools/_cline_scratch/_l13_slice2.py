# -*- coding: utf-8 -*-
from pathlib import Path

hpm = Path(r"C:/hades/Hecton8/Assets/_Project/Scripts/HectonPlayerMovement.cs")
drv = Path(r"C:/hades/Hecton8/Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessWorldDriver.cs")
lines = hpm.read_text(encoding="utf-8-sig", errors="replace").splitlines()
dlines = drv.read_text(encoding="utf-8-sig", errors="replace").splitlines()
out = Path(r"C:/hades/Hecton8/Tools/_cline_scratch/_l13_slice2.txt")
buf = []

def dump(ls, start, end, title):
    buf.append("=== %s %d-%d ===" % (title, start, end))
    for i in range(start - 1, min(end, len(ls))):
        buf.append("%d|%s" % (i + 1, ls[i]))

# ProcessPlayerInputFrame
for i, l in enumerate(lines):
    if "void ProcessPlayerInputFrame" in l:
        dump(lines, i + 1, i + 60, "ProcessPlayerInputFrame")
        break

# IsGameplayInputBlockedByMenu
for i, l in enumerate(lines):
    if "IsGameplayInputBlockedByMenu" in l and ("bool" in l or "private" in l):
        dump(lines, i + 1, i + 25, "IsGameplayInputBlockedByMenu")
        break

# currentSuitData property / field
for i, l in enumerate(lines):
    if "currentSuitData" in l and ("SuitData" in l or "get" in l or "=" in l):
        if i < 2000 or "SuitData currentSuitData" in l or "CurrentSuitData" in l:
            buf.append("SUIT %d: %s" % (i + 1, l[:160]))

# EnsureGameplayLocomotionInputReady in driver
for i, l in enumerate(dlines):
    if "EnsureGameplayLocomotionInputReady" in l:
        buf.append("DRV_HIT %d: %s" % (i + 1, l[:160]))
for i, l in enumerate(dlines):
    if "void EnsureGameplayLocomotionInputReady" in l or "EnsureGameplayLocomotionInputReady(" in l and "void" in l:
        dump(dlines, i + 1, i + 80, "EnsureGameplayLocomotionInputReady")
        break
# broader
for i, l in enumerate(dlines):
    if "EnsureGameplayLocomotion" in l and ("{" in l or "void" in l or "bool" in l):
        dump(dlines, max(1, i - 2), i + 100, "EnsureGameplay_ctx")
        break

# Tick order L12
for i, l in enumerate(dlines):
    if "AdvancePhase" in l and "PublishLocomotion" in "".join(dlines[max(0,i-5):i+5]):
        dump(dlines, max(1, i - 15), i + 25, "Tick_publish_order")
        break
for i, l in enumerate(dlines):
    if "void Tick(" in l or "public void Tick" in l:
        if "WorldDriver" in "".join(dlines[max(0,i-30):i]) or True:
            # first public Tick in driver class region
            pass
# find class method Tick of driver
for i, l in enumerate(dlines):
    if l.strip().startswith("public void Tick(") or l.strip() == "public void Tick()":
        dump(dlines, i + 1, i + 40, "Driver.Tick")
        break

# _registeredFixedTick usage / public ensure
for i, l in enumerate(lines):
    if "_registeredFixedTick" in l:
        buf.append("REGFLAG %d: %s" % (i + 1, l[:140]))

out.write_text("\n".join(buf), encoding="utf-8")
print("WROTE", out, out.stat().st_size)
