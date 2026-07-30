import os
import re

base = r"C:/hades/Hecton8"
out = []

# HeadlessSimulationRunner: TryMarkEcologyReady and fields
runner = os.path.join(base, r"Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs")
lines = open(runner, encoding="utf-8", errors="replace").read().splitlines()
out.append("=== RUNNER symbols ===")
for i, l in enumerate(lines, 1):
    if any(
        k in l
        for k in (
            "TryMarkEcologyReady",
            "_ecologyReady",
            "_ecologySample",
            "EnsurePlayer",
            "SeedObserver",
            "RegisterRuntime",
            "PriorityLayer",
            "_pendingDay",
            "deferred",
        )
    ):
        out.append(f"{i}|{l}")

# dump TryMarkEcologyReady region
for i, l in enumerate(lines, 1):
    if "TryMarkEcologyReady" in l and "void" in l:
        start = max(1, i - 2)
        end = min(len(lines), i + 80)
        out.append(f"\n===== runner {start}-{end} =====")
        for j in range(start, end + 1):
            out.append(f"{j}|{lines[j-1]}")
        break

eco = os.path.join(base, r"Assets/_Project/Scripts/World/EcosystemDirector.cs")
el = open(eco, encoding="utf-8", errors="replace").read().splitlines()
out.append(f"\neco total {len(el)}")
patterns = [
    "TryGetGlobalBiomassAudit",
    "HasPendingSimulationJob",
    "EnsurePlayerSectorRegistered",
    "TryResolveSeedObserverAup",
    "_solveScheduled",
    "CompleteScheduledSolve",
    "_activeBiomassCellCount",
    "ScheduleSectorSolve",
]
for i, l in enumerate(el, 1):
    if any(p in l for p in patterns):
        out.append(f"{i}|{l}")

# dump method bodies by finding signatures
def dump_method(name, window=120):
    for i, l in enumerate(el, 1):
        if name in l and ("bool " in l or "void " in l or "public " in l or "private " in l):
            # skip comments
            if l.strip().startswith("//") or l.strip().startswith("*"):
                continue
            start = max(1, i - 5)
            end = min(len(el), i + window)
            out.append(f"\n===== eco {name} @{i} {start}-{end} =====")
            for j in range(start, end + 1):
                out.append(f"{j}|{el[j-1]}")
            return
    out.append(f"\n===== eco {name} NOT FOUND =====")

for n in [
    "TryGetGlobalBiomassAudit",
    "HasPendingSimulationJob",
    "EnsurePlayerSectorRegistered",
    "TryResolveSeedObserverAup",
]:
    dump_method(n, 80)

# LateFrameTick in eco
for i, l in enumerate(el, 1):
    if "void LateFrameTick" in l or "public void LateFrameTick" in l:
        start = i
        end = min(len(el), i + 40)
        out.append(f"\n===== eco LateFrameTick {start}-{end} =====")
        for j in range(start, end + 1):
            out.append(f"{j}|{el[j-1]}")
        break

open(os.path.join(base, "Tools/_cline_slice3_out.txt"), "w", encoding="utf-8").write("\n".join(out))
print("done", len(out))
