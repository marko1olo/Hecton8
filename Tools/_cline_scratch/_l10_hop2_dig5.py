# -*- coding: utf-8 -*-
import os, re

base = r"C:\hades\Hecton8"
outp = os.path.join(base, r"Tools\_cline_scratch\_l10_hop2_dig5_out.txt")
out = []

hpm = os.path.join(base, r"Assets\_Project\Scripts\HectonPlayerMovement.cs")
hl = open(hpm, encoding="utf-8", errors="replace").read().splitlines()

# CurrentMovementIntent01 definition + uses of _inputH
out.append("=== CurrentMovementIntent01 / ResolveRawInput / _inputH ===")
for i, L in enumerate(hl):
    if any(k in L for k in (
        "CurrentMovementIntent01", "ResolveRawInputIntent", "_inputH", "_inputV", "_inputVertical",
        "movementIntent"
    )):
        out.append("%d|%s" % (i + 1, L.rstrip()[:220]))
out.append("")

# dump property body
for i, L in enumerate(hl):
    if "CurrentMovementIntent01" in L and ("=>" in L or "{" in L or "get" in L or "float" in L):
        for j in range(i, min(len(hl), i + 40)):
            out.append("I%d|%s" % (j + 1, hl[j][:220]))
            if j > i and hl[j].strip() == "}" and j > i + 2:
                break
        out.append("--")

# ResolveRawInputIntentVector
for i, L in enumerate(hl):
    if "ResolveRawInputIntentVector" in L and "void" not in L[:20]:
        pass
for i, L in enumerate(hl):
    if re.search(r"ResolveRawInputIntentVector\s*\(", L) and ("{" in L or L.strip().endswith(")")):
        # find method def
        if "private" in L or "public" in L or "protected" in L:
            for j in range(i, min(len(hl), i + 50)):
                out.append("R%d|%s" % (j + 1, hl[j][:220]))
                if j > i + 5 and hl[j].startswith("        }"):
                    break
            out.append("--")

# DiagEmitHopCensus full format - what fields after postMask
disp = os.path.join(base, r"Assets\_Project\Scripts\Core\InputDispatcher.cs")
dl = open(disp, encoding="utf-8", errors="replace").read().splitlines()
out.append("=== DiagEmitHopCensus full ===")
for i, L in enumerate(dl):
    if "DiagEmitHopCensus" in L and "void" in L:
        for j in range(i, min(len(dl), i + 80)):
            out.append("D%d|%s" % (j + 1, dl[j][:220]))
            if j > i + 10 and dl[j].startswith("        }"):
                break
        break

# L09 full INPUTHOP lines (uncut)
log = os.path.join(base, r"Docs\AgentLogs\h8_playprobe_v0_L09.log")
ll = open(log, encoding="utf-8", errors="replace").read().splitlines()
out.append("=== L09 INPUTHOP FULL ===")
for i, L in enumerate(ll):
    if "INPUTHOP" in L:
        out.append("L%d|%s" % (i + 1, L))

# L09 Swim full
out.append("=== L09 SWIM FULL ===")
for i, L in enumerate(ll):
    if "MOMENT" in L and "Swim" in L:
        out.append(L)

# L08 log hop if exists
for name in ("h8_playprobe_v0_L08.log", "h8_playprobe_v0_L07.log"):
    p = os.path.join(base, r"Docs\AgentLogs", name)
    if os.path.isfile(p):
        out.append("=== %s INPUTHOP ===" % name)
        for i, L in enumerate(open(p, encoding="utf-8", errors="replace")):
            if "INPUTHOP" in L or ("Swim" in L and "MOMENT" in L):
                out.append("%s|%s" % (name, L.rstrip()[:300]))

# InputManager IsPlayerInputEnabled / action map enable
im = os.path.join(base, r"Assets\_Project\Scripts\Input\InputManager.cs")
il = open(im, encoding="utf-8", errors="replace").read().splitlines()
out.append("=== InputManager player map ===")
for i, L in enumerate(il):
    if any(k in L for k in ("IsPlayerInputEnabled", "SwitchToPlayer", "SwitchToUI", "_playerActionMap", "TryGetActionMapEnabled", "PlayerMap")):
        for j in range(max(0, i - 1), min(len(il), i + 5)):
            out.append("M%d|%s" % (j + 1, il[j][:200]))
        out.append("--")

# Who calls SwitchToPlayerInput
out.append("=== SwitchToPlayerInput callers ===")
for root, dirs, files in os.walk(os.path.join(base, "Assets")):
    dirs[:] = [d for d in dirs if d not in ("Library", "Temp")]
    for f in files:
        if not f.endswith(".cs"):
            continue
        p = os.path.join(root, f)
        try:
            t = open(p, encoding="utf-8", errors="replace").read()
        except Exception:
            continue
        if "SwitchToPlayerInput" in t:
            for i, L in enumerate(t.splitlines()):
                if "SwitchToPlayerInput" in L:
                    out.append("%s:%d|%s" % (f, i + 1, L.strip()[:180]))

# HPM menu block implementation
out.append("=== IsGameplayInputBlockedByMenu ===")
for i, L in enumerate(hl):
    if "IsGameplayInputBlockedByMenu" in L:
        for j in range(i, min(len(hl), i + 15)):
            out.append("%d|%s" % (j + 1, hl[j][:200]))
        out.append("--")

open(outp, "w", encoding="utf-8").write("\n".join(out))
print("wrote", outp, os.path.getsize(outp))
