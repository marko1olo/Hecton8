# -*- coding: utf-8 -*-
import os

base = r"C:\hades\Hecton8"
path = os.path.join(base, r"Assets\_Project\Scripts\UI\PauseMenuController.cs")
pl = open(path, encoding="utf-8", errors="replace").read().splitlines()
out = []

# dump open/close methods
for name in ("RegisterOpenMenu", "UnregisterOpenMenu", "Open(", "Close(", "ForceClose", "OpenMenu", "CloseMenu", "TryOpen", "TryClose"):
    for i, L in enumerate(pl):
        if name in L and ("void" in L or "bool" in L or "private" in L or "public" in L):
            out.append("=== hit %s @%d ===" % (name, i + 1))
            for j in range(i, min(len(pl), i + 50)):
                out.append("%d|%s" % (j + 1, pl[j][:200]))
                if j > i + 5 and pl[j].startswith("        }"):
                    break
            out.append("")

# all RegisterOpenMenu / UnregisterOpenMenu call sites
out.append("=== call sites ===")
for i, L in enumerate(pl):
    if "RegisterOpenMenu" in L or "UnregisterOpenMenu" in L:
        out.append("%d|%s" % (i + 1, L.rstrip()[:200]))

# OnDisable/OnDestroy
out.append("=== OnDisable/OnDestroy/OnEnable ===")
for i, L in enumerate(pl):
    if any(k in L for k in ("void OnDisable", "void OnDestroy", "void OnEnable", "void Awake")):
        for j in range(i, min(len(pl), i + 40)):
            out.append("%d|%s" % (j + 1, pl[j][:200]))
            if j > i + 5 and pl[j].startswith("        }"):
                break
        out.append("")

# also check GetState callers from pause_out rest
pause = os.path.join(base, r"Tools\_cline_scratch\_l10_hop2_pause_ascii.txt")
pl2 = open(pause, encoding="ascii", errors="replace").read().splitlines()
out.append("=== rest of pause ascii from GetState ===")
for i, L in enumerate(pl2):
    if "GetState callers" in L or L.startswith("Hecton") or "GetState" in L:
        out.append(L)

# PlayerPDA Open path - does it leave IsOpen stuck? has ResetStatic
# Check if domain reload disabled
out.append("=== Editor settings domain reload ===")
for root, dirs, files in os.walk(os.path.join(base, "ProjectSettings")):
    for f in files:
        p = os.path.join(root, f)
        t = open(p, encoding="utf-8", errors="replace").read()
        if "m_EnterPlayModeOptions" in t or "DomainReload" in t or "ReloadDomain" in t:
            out.append(f)
            for line in t.splitlines():
                if "PlayMode" in line or "Domain" in line or "Reload" in line:
                    out.append("  " + line.strip())

op = os.path.join(base, r"Tools\_cline_scratch\_l10_hop2_pause2_out.txt")
open(op, "w", encoding="utf-8").write("\n".join(out))
print("wrote", op, len(out))
