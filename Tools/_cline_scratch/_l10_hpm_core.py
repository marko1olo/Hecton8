# -*- coding: utf-8 -*-
import os
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
ROOT = r"C:\hades\Hecton8"
OUT = os.path.join(ROOT, r"Tools\_cline_scratch\_l10_hpm_core_out.txt")
report = []

hpm_path = os.path.join(ROOT, r"Assets\_Project\Scripts\HectonPlayerMovement.cs")
lines = open(hpm_path, encoding="utf-8").read().splitlines()


def dump_range(start, end, title):
    report.append(f"===== {title} {start}-{end} =====")
    for i in range(start - 1, min(end, len(lines))):
        report.append(f"{i+1}:{lines[i]}")


def find_method(substr, require_void=True):
    for i, l in enumerate(lines):
        s = l.strip()
        if s.startswith("//"):
            continue
        if substr in l and "(" in l:
            if require_void and "void" not in l and "bool" not in l:
                continue
            # exclude fields/markers
            if "ProfilerMarker" in l or "readonly" in l and "new " in l:
                continue
            if "=" in l and "{" not in l and not l.rstrip().endswith(";"):
                # might still be sig
                pass
            if l.rstrip().endswith(";") and "{" not in l:
                continue
            return i + 1
    return None


# Key ranges from handoff
for title, a, b in [
    ("FixedTick area", 9900, 10050),
    ("SampleGameplayLocomotion", 8185, 8250),
    ("ProcessPlayerInputFrame", 8120, 8180),
    ("suit/juice early in tick?", 8055, 8135),
    ("registration", 1740, 2000),
    ("LocomotionHold refs search", 1, 1),
]:
    if a == 1 and b == 1:
        continue
    dump_range(a, b, title)

# Find real FixedTick method
for i, l in enumerate(lines):
    if "void FixedTick" in l or "public void FixedTick" in l or "void IFixedTickable.FixedTick" in l:
        report.append(f"FOUND FixedTick sig @ {i+1}: {l}")
        dump_range(i + 1, i + 120, "FixedTick method")

for i, l in enumerate(lines):
    if "TryRegister" in l and "Tick" in l and ("void" in l or "bool" in l):
        report.append(f"FOUND reg @ {i+1}: {l}")
        dump_range(i + 1, i + 80, "register")

for i, l in enumerate(lines):
    if "LocomotionHold" in l:
        report.append(f"LH {i+1}:{l}")

for i, l in enumerate(lines):
    if "SampleGameplayLocomotionInputForFixedStep" in l:
        report.append(f"SAMPLE_CALL {i+1}:{l}")

# Input path
for dirpath, _, files in os.walk(os.path.join(ROOT, "Assets")):
    for f in files:
        if f in (
            "HectonPlayerInputHandler.cs",
            "InputDispatcher.cs",
            "HectonInputDispatcher.cs",
            "PlayerInputManager.cs",
            "HectonPlayerInputManager.cs",
        ) or ("InputDispatcher" in f and f.endswith(".cs")):
            p = os.path.join(dirpath, f)
            report.append(f"INPUT_FILE {p}")
            ls = open(p, encoding="utf-8", errors="replace").read().splitlines()
            for i, l in enumerate(ls):
                if any(
                    k in l
                    for k in (
                        "TryReadFrame",
                        "GetState",
                        "hop",
                        "Hop",
                        "Publish",
                        "readHop",
                        "Gameplay",
                    )
                ):
                    report.append(f"  {i+1}:{l[:200]}")

# Inventory service
inv = None
for dirpath, _, files in os.walk(os.path.join(ROOT, "Assets")):
    for f in files:
        if f.endswith(".cs") and "Inventory" in f:
            p = os.path.join(dirpath, f)
            t = open(p, encoding="utf-8", errors="replace").read()
            if "CanServiceItemAdds" in t and "TryRecover" in t:
                inv = p
                report.append(f"INV {p}")
                ls = t.splitlines()
                for name in (
                    "CanServiceItemAdds",
                    "TryRecover",
                    "TryBind",
                    "RefreshVaultHandles",
                    "InitializeSoaQueryEngine",
                ):
                    for i, l in enumerate(ls):
                        if name in l and ("bool" in l or "void" in l or "private" in l or "public" in l):
                            if l.strip().startswith("//"):
                                continue
                            report.append(f"--- {name} @ {i+1} ---")
                            depth = 0
                            started = False
                            for j in range(i, min(len(ls), i + 60)):
                                report.append(f"{j+1}:{ls[j]}")
                                depth += ls[j].count("{") - ls[j].count("}")
                                if "{" in ls[j]:
                                    started = True
                                if started and depth <= 0:
                                    break
                            break

# PTM FixedTick body (real)
ptm = open(
    os.path.join(ROOT, r"Assets\_Project\Scripts\PlayerToolManager.cs"), encoding="utf-8"
).read().splitlines()
for i, l in enumerate(ptm):
    if "public void FixedTick" in l or "void FixedTick(float" in l:
        report.append(f"PTM FixedTick @ {i+1}")
        depth = 0
        started = False
        for j in range(i, min(len(ptm), i + 80)):
            report.append(f"{j+1}:{ptm[j]}")
            depth += ptm[j].count("{") - ptm[j].count("}")
            if "{" in ptm[j]:
                started = True
            if started and depth <= 0:
                break

# L09 key metrics from log
logp = os.path.join(ROOT, r"Docs\AgentLogs\h8_playprobe_v0_L09.log")
if os.path.isfile(logp):
    data = open(logp, encoding="utf-8", errors="replace").read().splitlines()
    report.append(f"L09 lines={len(data)}")
    keys = (
        "readHop",
        "movementIntent",
        "LocomotionHold",
        "STARTERGRANT",
        "refusalMask",
        "publishOk",
        "waitingOn",
        "SWIM",
        "TOOL",
        "V0_L09",
        "PASS",
        "FAIL",
        "intent01",
        "suit",
        "juice",
        "FixedTick",
        "SampleGameplay",
    )
    for i, l in enumerate(data):
        ll = l
        if any(k in ll for k in keys):
            report.append(f"LOG {i+1}:{ll[:300]}")

# L08 measured head
l08 = os.path.join(ROOT, r"Docs\V0_Playtest\V0_L08_MEASURED.md")
if os.path.isfile(l08):
    report.append("===== L08 MEASURED =====")
    report.append(open(l08, encoding="utf-8", errors="replace").read())

open(OUT, "w", encoding="utf-8").write("\n".join(report) + "\n")
print(OUT, "lines", len(report))
