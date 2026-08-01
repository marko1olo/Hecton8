# -*- coding: utf-8 -*-
import os, re

base = r"C:\hades\Hecton8"
outp = os.path.join(base, r"Tools\_cline_scratch\_l10_hop2_menu_out.txt")
out = []

def dump_isopen(path, class_hints):
    if not os.path.isfile(path):
        out.append("MISSING " + path)
        return
    lines = open(path, encoding="utf-8", errors="replace").read().splitlines()
    out.append("===== %s =====" % os.path.basename(path))
    for i, L in enumerate(lines):
        if any(k in L for k in ("IsOpen", "IsMenuOpen", "IsAnyOpen", "static bool", "_isOpen", "SetOpen", "Open(", "Close(")):
            if any(h in L for h in class_hints + ["IsOpen", "IsMenuOpen", "IsAnyOpen", "_isOpen", "static"]):
                out.append("%d|%s" % (i + 1, L.rstrip()[:200]))
    out.append("")

# find files
for root, dirs, files in os.walk(os.path.join(base, "Assets")):
    for f in files:
        if f in ("PlayerPDA.cs", "HectonFabricatorUI.cs", "PauseMenuController.cs"):
            dump_isopen(os.path.join(root, f), [f.replace(".cs", "")])

# full IsOpen property bodies
for fname in ("PlayerPDA.cs", "HectonFabricatorUI.cs", "PauseMenuController.cs"):
    path = None
    for root, dirs, files in os.walk(os.path.join(base, "Assets")):
        if fname in files:
            path = os.path.join(root, fname)
            break
    if not path:
        continue
    lines = open(path, encoding="utf-8", errors="replace").read().splitlines()
    out.append("=== %s IsOpen-ish bodies ===" % fname)
    for i, L in enumerate(lines):
        if re.search(r"(IsOpen|IsMenuOpen|IsAnyOpen)\s*(=>|\{)", L) or re.search(r"public static bool (IsOpen|IsMenuOpen|IsAnyOpen)", L):
            for j in range(max(0, i - 2), min(len(lines), i + 25)):
                out.append("%d|%s" % (j + 1, lines[j][:200]))
            out.append("--")

# L09 log menu/pda/pause/fabricator
log = os.path.join(base, r"Docs\AgentLogs\h8_playprobe_v0_L09.log")
ll = open(log, encoding="utf-8", errors="replace").read().splitlines()
out.append("=== L09 menu markers ===")
for i, L in enumerate(ll):
    if any(k in L for k in ("PlayerPDA", "PDA", "Fabricator", "PauseMenu", "IsOpen", "menu open", "MenuOpen", "UI input", "SwitchToUI", "SwitchToPlayer")):
        if any(k in L for k in ("PDA", "Fabricator", "Pause", "SwitchTo", "menu", "Menu", "IsOpen")):
            out.append("L%d|%s" % (i + 1, L[:220]))

# Also check where _lastPlayerKinematicsIntendedMovement is set relative to SampleGameplay
hpm = os.path.join(base, r"Assets\_Project\Scripts\HectonPlayerMovement.cs")
hl = open(hpm, encoding="utf-8", errors="replace").read().splitlines()
out.append("=== _lastPlayerKinematicsIntendedMovement writes ===")
for i, L in enumerate(hl):
    if "_lastPlayerKinematicsIntendedMovement" in L:
        out.append("%d|%s" % (i + 1, L.rstrip()[:200]))

# PrepareTransportAndFrameState - when intent snapshotted
out.append("=== PrepareTransportAndFrameState intent ===")
for i, L in enumerate(hl):
    if "PrepareTransportAndFrameState" in L and ("void" in L or "PlayerTransport" in L):
        for j in range(i, min(len(hl), i + 80)):
            out.append("%d|%s" % (j + 1, hl[j][:200]))
            if j > i + 10 and hl[j].startswith("        }"):
                break
        out.append("--")

# L08 MEASURED residual section about hop2
l08 = os.path.join(base, r"Docs\V0_Playtest\V0_L08_MEASURED.md")
if os.path.isfile(l08):
    t = open(l08, encoding="utf-8", errors="replace").read()
    out.append("=== L08 full ===")
    out.append(t[:8000])

open(outp, "w", encoding="utf-8").write("\n".join(out))
print("wrote", outp, os.path.getsize(outp))
