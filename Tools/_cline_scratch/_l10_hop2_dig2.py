# -*- coding: utf-8 -*-
import os, re

base = r"C:\hades\Hecton8"
outp = os.path.join(base, r"Tools\_cline_scratch\_l10_hop2_dig2_out.txt")
out = []

def scan(path, label, patterns, context=0, max_hits=200):
    if not os.path.isfile(path):
        out.append("MISSING %s" % path)
        return
    lines = open(path, encoding="utf-8", errors="replace").read().splitlines()
    out.append("===== %s (%d lines) =====" % (label, len(lines)))
    hits = 0
    for i, L in enumerate(lines):
        if any(re.search(p, L) for p in patterns):
            lo = max(0, i - context)
            hi = min(len(lines), i + 1 + context)
            for j in range(lo, hi):
                out.append("%s%d|%s" % (label[:1], j + 1, lines[j][:220]))
            out.append("--")
            hits += 1
            if hits >= max_hits:
                out.append("...truncated hits at %d" % max_hits)
                break
    out.append("hits=%d" % hits)
    out.append("")

# InputDispatcher: IsPlayerInputEnabled, GetState, override, hop
disp = os.path.join(base, r"Assets\_Project\Scripts\Core\InputDispatcher.cs")
scan(disp, "DISP", [
    r"IsPlayerInputEnabled",
    r"GetState\s*\(",
    r"CurrentInputState",
    r"overrideRejected|TryApplyOverride|ApplyOverride|Override",
    r"DiagRecordReadObservation|DiagEmitHopCensus|readHop",
    r"RegisteredInput|SetPlayerInputEnabled|EnablePlayerInput|_playerInputEnabled",
], context=2, max_hits=80)

# GlobalRegistry Input vs RegisteredInput
reg = os.path.join(base, r"Assets\_Project\Scripts\Core\GlobalRegistry.cs")
if not os.path.isfile(reg):
    # find it
    for root, dirs, files in os.walk(os.path.join(base, "Assets")):
        for f in files:
            if f == "GlobalRegistry.cs":
                reg = os.path.join(root, f)
                break
scan(reg, "REG", [
    r"RegisteredInput",
    r"\bInput\b",
    r"IInputService",
    r"NullInput|inputService",
], context=1, max_hits=60)

# Probe: movementIntent, LocomotionHold, waitingOn, Swim measure
probe = os.path.join(base, r"Assets\_Project\Scripts\Editor\Diagnostics\H8_HeadlessPlayModeProbe.cs")
scan(probe, "PROBE", [
    r"movementIntent",
    r"LocomotionHold",
    r"waitingOn",
    r"HoldInProgress",
    r"Intent01",
    r"immersionMax",
    r"readHop",
    r"INPUTHOP",
    r"EvaluateSwim|MeasureSwim|SwimMoment",
], context=3, max_hits=100)

# World driver override path
for root, dirs, files in os.walk(os.path.join(base, "Assets")):
    for f in files:
        if "HeadlessWorldDriver" in f or "WorldDriver" in f:
            wd = os.path.join(root, f)
            out.append("FOUND driver %s" % wd)
            scan(wd, "DRV", [
                r"override|Override|MoveDelta|publish|TryApply|LocomotionHold|waitingOn|Swim",
            ], context=2, max_hits=80)

# HPM SetSuit full + field init currentSuitData + who calls SetSuit
hpm = os.path.join(base, r"Assets\_Project\Scripts\HectonPlayerMovement.cs")
hl = open(hpm, encoding="utf-8", errors="replace").read().splitlines()
out.append("===== SetSuit body =====")
for i in range(1742, 1780):
    out.append("%d|%s" % (i + 1, hl[i]))
out.append("")
out.append("===== juice ensure call 4580-4590 =====")
for i in range(4575, 4600):
    out.append("%d|%s" % (i + 1, hl[i]))
out.append("")

# callers of SetSuit project-wide
out.append("===== SetSuit callers =====")
for root, dirs, files in os.walk(os.path.join(base, "Assets")):
    dirs[:] = [d for d in dirs if d not in ("Library", "Temp", "obj")]
    for f in files:
        if not f.endswith(".cs"):
            continue
        p = os.path.join(root, f)
        try:
            txt = open(p, encoding="utf-8", errors="replace").read()
        except Exception:
            continue
        if "SetSuit(" in txt:
            for i, L in enumerate(txt.splitlines()):
                if "SetSuit(" in L:
                    out.append("%s:%d|%s" % (p.replace(base+"\\",""), i+1, L.strip()[:180]))

# L09 full swim moment line + nearby waitingOn
log = os.path.join(base, r"Docs\AgentLogs\h8_playprobe_v0_L09.log")
ll = open(log, encoding="utf-8", errors="replace").read().splitlines()
out.append("===== L09 Swim full + nearby =====")
for i, L in enumerate(ll):
    if "MOMENT" in L and "Swim" in L:
        for j in range(max(0,i-2), min(len(ll), i+5)):
            out.append("L%d|%s" % (j+1, ll[j][:300]))
    if "waitingOn" in L or "LocomotionHold" in L:
        out.append("W%d|%s" % (i+1, L[:300]))
    if "INPUTHOP" in L:
        out.append("H%d|%s" % (i+1, L[:300]))

open(outp, "w", encoding="utf-8").write("\n".join(out))
print("wrote", outp, os.path.getsize(outp))
