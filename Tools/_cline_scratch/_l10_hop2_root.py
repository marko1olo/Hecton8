# -*- coding: utf-8 -*-
import os
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
ROOT = r"C:\hades\Hecton8"
OUT = os.path.join(ROOT, r"Tools\_cline_scratch\_l10_hop2_root_out.txt")
r = []

# InputDispatcher GetState + CurrentInputState
p = os.path.join(ROOT, r"Assets\_Project\Scripts\Core\InputDispatcher.cs")
ls = open(p, encoding="utf-8").read().splitlines()
for i, l in enumerate(ls):
    if "GetState(" in l or "CurrentInputState" in l or "DiagRecordReadObservation" in l:
        r.append(f"ID {i+1}:{l}")
# dump GetState method
for i, l in enumerate(ls):
    if "public PlayerInputState GetState" in l:
        depth = 0
        started = False
        r.append("=== GetState ===")
        for j in range(i, min(len(ls), i + 40)):
            r.append(f"{j+1}:{ls[j]}")
            depth += ls[j].count("{") - ls[j].count("}")
            if "{" in ls[j]:
                started = True
            if started and depth <= 0:
                break
for i, l in enumerate(ls):
    if "CurrentInputState" in l and ("get" in l or "{" in l or "=>" in l or "property" in l.lower() or "PlayerInputState" in l):
        r.append(f"CUR {i+1}:{l}")
# property body
for i, l in enumerate(ls):
    if "PlayerInputState CurrentInputState" in l or "public PlayerInputState CurrentInputState" in l:
        for j in range(i, min(len(ls), i + 25)):
            r.append(f"{j+1}:{ls[j]}")

# HectonPlayerInputHandler full
p2 = os.path.join(ROOT, r"Assets\_Project\Scripts\Gameplay\HectonPlayerInputHandler.cs")
r.append("=== HectonPlayerInputHandler full ===")
r.extend(f"{i+1}:{l}" for i, l in enumerate(open(p2, encoding="utf-8").read().splitlines()))

# HPM: suit assignment, juice assignment, fixed registration
hpm = os.path.join(ROOT, r"Assets\_Project\Scripts\HectonPlayerMovement.cs")
hls = open(hpm, encoding="utf-8").read().splitlines()
for needle in (
    "TryRegisterFixed",
    "RegisterFixedTickable",
    "_registeredFixed",
    "registeredToFixed",
    "IFixedTickable",
    "currentSuitData =",
    "_juiceProcessor =",
    "ResolveInputManagerBinding",
    "IsGameplayInputBlockedByMenu",
    "LocomotionHold",
    "movementIntent01",
    "_inputManager",
):
    for i, l in enumerate(hls):
        if needle in l:
            r.append(f"HPM[{needle}] {i+1}:{l[:180]}")

# dump registration-related methods more carefully
for i, l in enumerate(hls):
    if "RegisterFixedTickable" in l or "TryRegisterFixed" in l or (
        "FixedTickable" in l and ("Register" in l or "Unregister" in l)
    ):
        for j in range(max(0, i - 5), min(len(hls), i + 15)):
            r.append(f"FXREG {j+1}:{hls[j]}")

for i, l in enumerate(hls):
    if "void ResolveInputManagerBinding" in l or "ResolveInputManagerBinding(" in l and "void" in l:
        depth = 0
        started = False
        r.append("=== ResolveInputManagerBinding ===")
        for j in range(i, min(len(hls), i + 50)):
            r.append(f"{j+1}:{hls[j]}")
            depth += hls[j].count("{") - hls[j].count("}")
            if "{" in hls[j]:
                started = True
            if started and depth <= 0:
                break
        break

for i, l in enumerate(hls):
    if "IsGameplayInputBlockedByMenu" in l and ("bool" in l or "void" in l):
        depth = 0
        started = False
        r.append("=== IsGameplayInputBlockedByMenu ===")
        for j in range(i, min(len(hls), i + 40)):
            r.append(f"{j+1}:{hls[j]}")
            depth += hls[j].count("{") - hls[j].count("}")
            if "{" in hls[j]:
                started = True
            if started and depth <= 0:
                break
        break

# suit null path - where currentSuitData set
for i, l in enumerate(hls):
    if "currentSuitData" in l and ("=" in l) and not l.strip().startswith("//"):
        if "if " in l and "==" in l:
            continue
        r.append(f"SUITSET {i+1}:{l[:200]}")

# OnEnable registration block
for i, l in enumerate(hls):
    if "void OnEnable" in l:
        depth = 0
        started = False
        r.append("=== OnEnable ===")
        for j in range(i, min(len(hls), i + 100)):
            r.append(f"{j+1}:{hls[j]}")
            depth += hls[j].count("{") - hls[j].count("}")
            if "{" in hls[j]:
                started = True
            if started and depth <= 0:
                break
        break

# search probe for LocomotionHold waitingOn
for dirpath, _, files in os.walk(os.path.join(ROOT, "Assets")):
    for f in files:
        if not f.endswith(".cs"):
            continue
        if "PlayProbe" not in f and "Playtest" not in f and "V0_" not in f and "Probe" not in f:
            continue
        pp = os.path.join(dirpath, f)
        try:
            t = open(pp, encoding="utf-8", errors="replace").read()
        except Exception:
            continue
        if "LocomotionHold" in t or "waitingOn" in t or "movementIntent01" in t or "readHop" in t:
            r.append(f"PROBE {pp}")
            pls = t.splitlines()
            for i, l in enumerate(pls):
                if any(
                    k in l
                    for k in (
                        "LocomotionHold",
                        "waitingOn",
                        "movementIntent",
                        "readHop",
                        "Swim",
                        "STARTERGRANT",
                        "ToolEquip",
                    )
                ):
                    r.append(f"  {i+1}:{l[:200]}")

# L09 full INPUTHOP lines and swim/tool moments
logp = os.path.join(ROOT, r"Docs\AgentLogs\h8_playprobe_v0_L09.log")
data = open(logp, encoding="utf-8", errors="replace").read().splitlines()
r.append("=== L09 key full lines ===")
for i, l in enumerate(data):
    if "H8_INPUTHOP" in l or "MOMENT" in l or "STARTERGRANT" in l or "LocomotionHold" in l or "TOOL" in l and "PLAYPROBE" in l:
        r.append(f"L{i+1}:{l[:400]}")

# L08 measured
l08 = os.path.join(ROOT, r"Docs\V0_Playtest\V0_L08_MEASURED.md")
r.append("=== L08 ===")
r.append(open(l08, encoding="utf-8", errors="replace").read())

open(OUT, "w", encoding="utf-8").write("\n".join(r) + "\n")
print(OUT, len(r))
