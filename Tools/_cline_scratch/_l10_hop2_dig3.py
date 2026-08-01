# -*- coding: utf-8 -*-
import os, re

base = r"C:\hades\Hecton8"
outp = os.path.join(base, r"Tools\_cline_scratch\_l10_hop2_dig3_out.txt")
out = []

def dump_file_ranges(path, ranges, tag):
    lines = open(path, encoding="utf-8", errors="replace").read().splitlines()
    for a, b in ranges:
        out.append("===== %s %d-%d =====" % (tag, a, b))
        for i in range(a - 1, min(b, len(lines))):
            out.append("%d|%s" % (i + 1, lines[i][:240]))
        out.append("")

def grep(path, patterns, tag, ctx=2, limit=80):
    lines = open(path, encoding="utf-8", errors="replace").read().splitlines()
    out.append("===== GREP %s =====" % tag)
    n = 0
    for i, L in enumerate(lines):
        if any(re.search(p, L) for p in patterns):
            for j in range(max(0, i - ctx), min(len(lines), i + 1 + ctx)):
                out.append("%d|%s" % (j + 1, lines[j][:220]))
            out.append("--")
            n += 1
            if n >= limit:
                break
    out.append("n=%d\n" % n)

disp = os.path.join(base, r"Assets\_Project\Scripts\Core\InputDispatcher.cs")
# IsPlayerInputEnabled, native manager bind, GetState vs CurrentInputState difference
grep(disp, [
    r"_nativeInputManager",
    r"IsPlayerInputEnabled",
    r"NativeInputManager",
    r"SetPlayerInputEnabled|EnablePlayerInput|DisablePlayerInput",
    r"_currentState\s*=",
    r"_currentInputState\s*=",
], "DISP-native", ctx=1, limit=60)

# dump GetState area and CurrentInputState and PublishDeterministic
dump_file_ranges(disp, [(495, 545), (700, 780), (1330, 1380), (1680, 1780), (3660, 3760)], "DISP")

# NoOpInputService
for root, dirs, files in os.walk(os.path.join(base, "Assets")):
    for f in files:
        if "NoOpInput" in f or f == "IInputService.cs":
            p = os.path.join(root, f)
            out.append("FILE " + p)
            t = open(p, encoding="utf-8", errors="replace").read()
            out.append(t[:4000])
            out.append("")

# Probe movementIntent measurement - larger context around those lines
probe = os.path.join(base, r"Assets\_Project\Scripts\Editor\Diagnostics\H8_HeadlessPlayModeProbe.cs")
pl = open(probe, encoding="utf-8", errors="replace").read().splitlines()
out.append("===== PROBE intent-related full blocks =====")
for i, L in enumerate(pl):
    if any(k in L for k in ("movementIntent", "LocomotionHold", "waitingOn", "HoldInProgress", "Intent01", "immersionMax", "_inputH", "MoveDelta")):
        for j in range(max(0, i - 5), min(len(pl), i + 8)):
            out.append("P%d|%s" % (j + 1, pl[j][:220]))
        out.append("--")

# Find world driver
wd = None
for root, dirs, files in os.walk(os.path.join(base, "Assets")):
    for f in files:
        if f.endswith("H8_HeadlessWorldDriver.cs"):
            wd = os.path.join(root, f)
out.append("WD=" + str(wd))
if wd:
    wl = open(wd, encoding="utf-8", errors="replace").read().splitlines()
    out.append("wd lines %d" % len(wl))
    for i, L in enumerate(wl):
        if any(k in L for k in ("LocomotionHold", "waitingOn", "PublishInput", "TryPublish", "override", "MoveDelta", "movementIntent", "Swim")):
            for j in range(max(0, i - 2), min(len(wl), i + 4)):
                out.append("W%d|%s" % (j + 1, wl[j][:220]))
            out.append("--")

# L09: full INPUTHOP lines + swim reason waitingOn if any in json
log = os.path.join(base, r"Docs\AgentLogs\h8_playprobe_v0_L09.log")
ll = open(log, encoding="utf-8", errors="replace").read().splitlines()
out.append("===== ALL INPUTHOP =====")
for i, L in enumerate(ll):
    if "INPUTHOP" in L or "waitingOn" in L or "LocomotionHold" in L:
        out.append("L%d|%s" % (i + 1, L[:350]))

# swim moment full line
for i, L in enumerate(ll):
    if "FAIL" in L and "Swim" in L:
        out.append("SWIMFULL|%s" % L)

# json
jpath = os.path.join(base, r"Docs\AgentLogs\h8_playprobe_v0_L09.json")
if os.path.isfile(jpath):
    jt = open(jpath, encoding="utf-8", errors="replace").read()
    # extract swim-related snippets
    for m in re.finditer(r'.{0,80}(Swim|waitingOn|LocomotionHold|movementIntent|readHop).{0,120}', jt):
        out.append("J|%s" % m.group(0).replace("\n", " ")[:250])

# HPM: who sets IsGameplayInputBlockedByMenu - PDA open at start?
hpm = os.path.join(base, r"Assets\_Project\Scripts\HectonPlayerMovement.cs")
grep(hpm, [r"IsGameplayInputBlockedByMenu", r"PlayerPDA", r"IsMenuOpen", r"PauseMenu"], "HPM-menu", ctx=2, limit=20)

# ProcessPlayerInputFrame uses _inputManager - is it ever the NoOp?
# ResolveInputManagerBinding uses RegisteredInput (raw) not Input (noop fallback)
# If RegisteredInput is null, _inputManager null -> no hop2
# Comments said InputDispatcher destroyed on scene load - was that fixed with PersistRuntimeService?

grep(disp, [r"PersistRuntimeService", r"DontDestroyOnLoad", r"OnDestroy", r"TryRegisterInput", r"RegisterInputService"], "DISP-persist", ctx=3, limit=40)

open(outp, "w", encoding="utf-8").write("\n".join(out))
print("wrote", outp, os.path.getsize(outp))
