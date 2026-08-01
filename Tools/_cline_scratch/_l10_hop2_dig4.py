# -*- coding: utf-8 -*-
import os, re

base = r"C:\hades\Hecton8"
outp = os.path.join(base, r"Tools\_cline_scratch\_l10_hop2_dig4_out.txt")
out = []

def dump(path, a, b, tag):
    lines = open(path, encoding="utf-8", errors="replace").read().splitlines()
    out.append("===== %s %d-%d =====" % (tag, a, b))
    for i in range(a - 1, min(b, len(lines))):
        out.append("%d|%s" % (i + 1, lines[i][:240]))
    out.append("")

# HPM immersion writes
hpm = os.path.join(base, r"Assets\_Project\Scripts\HectonPlayerMovement.cs")
hl = open(hpm, encoding="utf-8", errors="replace").read().splitlines()
out.append("=== immersion / waterImmersion assignments ===")
for i, L in enumerate(hl):
    if re.search(r"_waterImmersionRatio|_smoothedImmersionRatio|immersion", L) and ("=" in L or "UpdateWater" in L):
        if any(x in L for x in ("=", "void ", "float ", "bool ")):
            out.append("%d|%s" % (i + 1, L.rstrip()[:200]))
out.append("")

# Driver: how _maxMovementIntent is updated + LocomotionHold logic
wd = os.path.join(base, r"Assets\_Project\Scripts\Editor\Diagnostics\H8_HeadlessWorldDriver.cs")
wl = open(wd, encoding="utf-8", errors="replace").read().splitlines()
out.append("=== driver movementIntent / LocomotionHold blocks ===")
for i, L in enumerate(wl):
    if any(k in L for k in ("_maxMovementIntent", "MovementIntent", "LocomotionHold", "ResolveRawInput", "inputH", "GetState", "CurrentInputState", "MoveDelta", "intent01")):
        for j in range(max(0, i - 3), min(len(wl), i + 10)):
            out.append("W%d|%s" % (j + 1, wl[j][:220]))
        out.append("--")

# specifically dump around 2700-2820 and 1100-1200
dump(wd, 2680, 2850, "WD-loco")
dump(wd, 1120, 1200, "WD-sample")
dump(wd, 4650, 4820, "WD-wait")

# InputDispatcher IsPlayerInputEnabled gate - when is native null?
# SwitchToPlayerInput / when enabled
disp = os.path.join(base, r"Assets\_Project\Scripts\Core\InputDispatcher.cs")
dl = open(disp, encoding="utf-8", errors="replace").read().splitlines()
out.append("=== SwitchToPlayer / BindNative / IsPlayerInputEnabled consumers ===")
for i, L in enumerate(dl):
    if any(k in L for k in ("SwitchToPlayerInput", "SwitchToUIInput", "BindNative", "SetNative", "_nativeInputManager =", "IsPlayerInputEnabled")):
        out.append("%d|%s" % (i + 1, L.rstrip()[:200]))
out.append("")
dump(disp, 530, 580, "DISP-enabled")
dump(disp, 3220, 3310, "DISP-switch")
dump(disp, 3688, 3720, "DISP-persist")

# Find NativeInputManager IsPlayerInputEnabled implementation
for root, dirs, files in os.walk(os.path.join(base, "Assets")):
    for f in files:
        if not f.endswith(".cs"):
            continue
        p = os.path.join(root, f)
        try:
            t = open(p, encoding="utf-8", errors="replace").read()
        except Exception:
            continue
        if "IsPlayerInputEnabled" in t and ("class" in t or "interface" in t):
            if "NativeInput" in f or "InputManager" in f or "INative" in f or "NoOp" in f:
                out.append("FILE " + p.replace(base + "\\", ""))
                for i, L in enumerate(t.splitlines()):
                    if "IsPlayerInputEnabled" in L or "class " in L or "interface " in L:
                        out.append("  %d|%s" % (i + 1, L[:180]))

# L08 measured for comparison path
l08 = os.path.join(base, r"Docs\V0_Playtest\V0_L08_MEASURED.md")
if os.path.isfile(l08):
    out.append("=== L08 MEASURED head ===")
    out.append(open(l08, encoding="utf-8", errors="replace").read()[:5000])

# Prior hop2 residual notes in architect answers
arch = os.path.join(base, r"Tools\_cline_scratch\architect_answers.md")
if os.path.isfile(arch):
    out.append("=== architect_answers ===")
    out.append(open(arch, encoding="utf-8", errors="replace").read()[:3000])

open(outp, "w", encoding="utf-8").write("\n".join(out))
print("wrote", outp, os.path.getsize(outp))
