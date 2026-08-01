# -*- coding: utf-8 -*-
from pathlib import Path
p = Path(r"C:/hades/Hecton8/Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessPlayModeProbe.cs")
lines = p.read_text(encoding="utf-8").splitlines()
keys = [
    "GameplayWarmup", "Phase.", "WorldDriver.Begin", "case Phase", "enum Phase",
    "TickDispatcher", "GlobalRegistry", "BeginGameplay", "class H8_Headless",
    "void Tick", "EditorApplication.update", "namespace ", "using Hecton8",
    "_worldDriver", "ForceGameplay", "Settle",
]
out = [f"lines={len(lines)}"]
for i, l in enumerate(lines, 1):
    if any(k in l for k in keys):
        out.append(f"{i}: {l[:180]}")
# dump GameplayWarmup region if found
for i, l in enumerate(lines):
    if "GameplayWarmup" in l and ("case" in l or "=" in l or "Phase" in l):
        start = max(0, i - 5)
        end = min(len(lines), i + 80)
        out.append(f"--- context around {i+1} ---")
        for j in range(start, end):
            out.append(f"{j+1}: {lines[j][:200]}")
        break
Path(r"C:/hades/Hecton8/Tools/_cline_scratch/_l16_probe_scan.txt").write_text(
    "\n".join(out), encoding="utf-8"
)
print("ok", len(out))
