# -*- coding: utf-8 -*-
import os, re

base = r"C:\hades\Hecton8"
outp = os.path.join(base, r"Tools\_cline_scratch\_l10_hop2_pause_out.txt")
out = []

# PauseMenuController _openMenuCount
path = None
for root, dirs, files in os.walk(os.path.join(base, "Assets")):
    if "PauseMenuController.cs" in files:
        path = os.path.join(root, "PauseMenuController.cs")
        break

pl = open(path, encoding="utf-8", errors="replace").read().splitlines()
out.append("path=" + path)
out.append("=== _openMenuCount / ResetStatic / open/close ===")
for i, L in enumerate(pl):
    if any(k in L for k in (
        "_openMenuCount", "ResetStatic", "RuntimeInitializeOnLoad",
        "IsAnyOpen", "IncrementOpen", "DecrementOpen", "_openMenu"
    )):
        for j in range(max(0, i - 2), min(len(pl), i + 8)):
            out.append("%d|%s" % (j + 1, pl[j][:200]))
        out.append("--")

# also search open increment patterns
out.append("=== ++ / -- open count ===")
for i, L in enumerate(pl):
    if "_openMenuCount" in L:
        out.append("%d|%s" % (i + 1, L.rstrip()[:200]))

# HPM ResolveInputManagerBinding full
hpm = os.path.join(base, r"Assets\_Project\Scripts\HectonPlayerMovement.cs")
hl = open(hpm, encoding="utf-8", errors="replace").read().splitlines()
out.append("=== ResolveInputManagerBinding full ===")
for i, L in enumerate(hl):
    if "ResolveInputManagerBinding" in L and "void" in L:
        for j in range(i, min(len(hl), i + 40)):
            out.append("%d|%s" % (j + 1, hl[j][:200]))
            if j > i + 3 and hl[j].startswith("        }"):
                break
        break

# _inputManager field and assignments
out.append("=== _inputManager ===")
for i, L in enumerate(hl):
    if "_inputManager" in L:
        out.append("%d|%s" % (i + 1, L.rstrip()[:200]))

# L08 MEASURED residual - read from menu_ascii rest
menu = os.path.join(base, r"Tools\_cline_scratch\_l10_hop2_menu_ascii.txt")
ml = open(menu, encoding="ascii", errors="replace").read().splitlines()
out.append("=== L08 from menu dump (tail) ===")
# find L08 full section
start = next((i for i, L in enumerate(ml) if "L08 full" in L), None)
if start is not None:
    for L in ml[start:start+120]:
        out.append(L)

# Check git log for SampleGameplay introduction and any hop2 notes
# Also check if ProcessPlayerInputFrame could use wrong overload
out.append("=== ProcessPlayerInputFrame + SampleGameplay disk verify ===")
for i in range(8129, 8227):
    out.append("%d|%s" % (i + 1, hl[i][:200]))

# Who else calls GetState?
out.append("=== GetState callers project ===")
for root, dirs, files in os.walk(os.path.join(base, "Assets")):
    dirs[:] = [d for d in dirs if d not in ("Library", "Temp", "obj")]
    for f in files:
        if not f.endswith(".cs"):
            continue
        p = os.path.join(root, f)
        try:
            t = open(p, encoding="utf-8", errors="replace").read()
        except Exception:
            continue
        if ".GetState()" in t or "GetState()" in t:
            for i, L in enumerate(t.splitlines()):
                if "GetState()" in L:
                    out.append("%s:%d|%s" % (f, i + 1, L.strip()[:160]))

open(outp, "w", encoding="utf-8").write("\n".join(out))
print("wrote", outp, os.path.getsize(outp))
