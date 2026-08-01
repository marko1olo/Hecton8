# -*- coding: utf-8 -*-
import sys
from pathlib import Path
sys.stdout.reconfigure(encoding="utf-8")
root = Path(r"C:/hades/Hecton8/Assets/_Project/Scripts")
out = []
hsr = root / "QA/Headless/HeadlessSimulationRunner.cs"
lines = hsr.read_text(encoding="utf-8", errors="replace").splitlines()
out.append(f"=== {hsr} lines={len(lines)} ===")
keys = [
    "EnsureHeadlessSimulationClock",
    "EnableStepBoundedTime",
    "RequestHeadlessTimeDilation",
    "RequestSimulationPause",
    "HeadlessStepBounded",
    "MaybeEnsureHeadless",
    "TimeDilationScalar",
    "IsStepBounded",
]
for i, l in enumerate(lines, 1):
    if any(k in l for k in keys):
        out.append(f"{i}: {l[:200]}")

sd = root / "Core/SystemDispatcher.cs"
sl = sd.read_text(encoding="utf-8", errors="replace").splitlines()
out.append(f"=== {sd} lines={len(sl)} ===")
sd_keys = [
    "EnableStepBoundedTime",
    "DisableStepBoundedTime",
    "IsStepBoundedTimeActive",
    "RequestHeadlessTimeDilation",
    "RequestSimulationPause",
    "MaxClampFreeStepSeconds",
    "SimulationPaused",
    "_stepBounded",
    "AdvanceStepBoundedClock",
]
for i, l in enumerate(sl, 1):
    if any(k in l for k in sd_keys):
        out.append(f"{i}: {l[:200]}")

# InternalsVisibleTo for Editor
for asm in Path(r"C:/hades/Hecton8/Assets").rglob("*.asmdef"):
    t = asm.read_text(encoding="utf-8", errors="replace")
    if "InternalsVisibleTo" in t or "Editor" in asm.name:
        if "Hecton" in str(asm) or "Project" in str(asm):
            out.append(f"ASMDEF {asm}")

# Find EnsureHeadlessSimulationClock method body range
for i, l in enumerate(lines, 1):
    if "void EnsureHeadlessSimulationClock" in l or "EnsureHeadlessSimulationClock(" in l and "{" not in l:
        start = i
        # dump next 80 lines
        out.append("--- EnsureHeadlessSimulationClock body ---")
        for j in range(start - 1, min(start + 80, len(lines))):
            out.append(f"{j+1}: {lines[j][:200]}")
        break

dest = Path(r"C:/hades/Hecton8/Tools/_cline_scratch/_l16_api_scan.txt")
dest.write_text("\n".join(out), encoding="utf-8")
print(f"WROTE {dest} n={len(out)}")
