import os
import re

base = r"C:/hades/Hecton8"
out = []

# Find IEcosystemDirectorService
for root, dirs, files in os.walk(os.path.join(base, "Assets")):
    # skip junk
    dirs[:] = [d for d in dirs if d not in ("Library", "Temp", "obj", ".git")]
    for f in files:
        if f.endswith(".cs") and "Ecosystem" in f:
            p = os.path.join(root, f)
            try:
                t = open(p, encoding="utf-8", errors="replace").read()
            except Exception:
                continue
            if "interface IEcosystemDirectorService" in t:
                out.append("FOUND " + p)
                lines = t.splitlines()
                for i, l in enumerate(lines, 1):
                    if "interface IEcosystemDirectorService" in l or "TryGetGlobalBiomass" in l or "HasPending" in l or "IsInitialized" in l:
                        out.append(f"{i}|{l}")
                # dump interface body
                for i, l in enumerate(lines, 1):
                    if "interface IEcosystemDirectorService" in l:
                        for j in range(i, min(len(lines), i + 80) + 1):
                            out.append(f"{j}|{lines[j-1]}")
                        break

# PriorityLayer enum
for root, dirs, files in os.walk(os.path.join(base, "Assets")):
    dirs[:] = [d for d in dirs if d not in ("Library", "Temp", "obj", ".git")]
    for f in files:
        if not f.endswith(".cs"):
            continue
        p = os.path.join(root, f)
        try:
            t = open(p, encoding="utf-8", errors="replace").read()
        except Exception:
            continue
        if "enum PriorityLayer" in t:
            out.append("\nPRIORITY " + p)
            lines = t.splitlines()
            for i, l in enumerate(lines, 1):
                if "enum PriorityLayer" in l:
                    for j in range(i, min(len(lines), i + 40) + 1):
                        out.append(f"{j}|{lines[j-1]}")
                    break

# EcosystemDirector LateFrame registration priority
eco = os.path.join(base, r"Assets/_Project/Scripts/World/EcosystemDirector.cs")
el = open(eco, encoding="utf-8", errors="replace").read().splitlines()
out.append("\n=== eco register late ===")
for i, l in enumerate(el, 1):
    if "LateFrame" in l or "TryRegister" in l and "Tickable" in l:
        if "Register" in l or "LateFrame" in l or "Priority" in l:
            out.append(f"{i}|{l}")

# more targeted
for i, l in enumerate(el, 1):
    if "TryRegisterLateFrameTickable" in l or "RegisterTickables" in l or "PriorityLayer." in l:
        out.append(f"P {i}|{l}")

# SlowTick region where EnsurePlayerSectorRegistered is called
out.append("\n=== eco SlowTick seed region ===")
for i, l in enumerate(el, 1):
    if "EnsurePlayerSectorRegistered();" in l:
        for j in range(max(1, i - 30), min(len(el), i + 50) + 1):
            out.append(f"{j}|{el[j-1]}")
        break

# HasPendingSimulationJob body
out.append("\n=== HasPending body ===")
for i, l in enumerate(el, 1):
    if "bool HasPendingSimulationJob" in l:
        for j in range(i, min(len(el), i + 15) + 1):
            out.append(f"{j}|{el[j-1]}")
        break

# runner fields around ecology streak
runner = os.path.join(base, r"Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs")
rl = open(runner, encoding="utf-8", errors="replace").read().splitlines()
out.append("\n=== runner fields 100-180 ===")
for j in range(100, 181):
    out.append(f"{j}|{rl[j-1]}")

open(os.path.join(base, "Tools/_cline_slice4_out.txt"), "w", encoding="utf-8").write("\n".join(out))
print("done", len(out))
