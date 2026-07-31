"""Extract RunDispatcherUpdate barrier + dilation/pause write path."""
import os
import re
from pathlib import Path

os.chdir(r"C:\hades\Hecton8")
out = Path(r"C:\hades\Hecton8\_agent_probe_barrier_out.txt")
lines = []

def p(msg=""):
    lines.append(str(msg))

disp = Path(r"Assets/_Project/Scripts/Core/SystemDispatcher.cs")
text = disp.read_text(encoding="utf-8", errors="replace")
ls = text.splitlines()

def dump_range(a, b, title):
    p(f"\n=== {title} ({a}-{b}) ===")
    for i in range(max(0, a - 1), min(len(ls), b)):
        p(f"{i+1}|{ls[i][:220]}")

# Full barrier section of RunDispatcherUpdate
dump_range(5220, 5360, "RunDispatcherUpdate barrier+ticks")

# SetTimeDilationScalar
for i, ln in enumerate(ls):
    if "void SetTimeDilationScalar" in ln or "SetTimeDilationScalar(" in ln and "private" in ln:
        dump_range(i + 1, i + 80, "SetTimeDilationScalar vicinity " + str(i + 1))
        break

# Find method definition more carefully
m = re.search(r"private void SetTimeDilationScalar\([\s\S]{0,2500}", text)
if m:
    p("\n=== SetTimeDilationScalar method ===")
    for ln in m.group(0).splitlines()[:90]:
        p(ln[:220])

# DrainSimulationPauseSignals
m = re.search(r"(private|public).{0,40}DrainSimulationPauseSignals[\s\S]{0,2000}", text)
if m:
    p("\n=== DrainSimulationPauseSignals ===")
    for ln in m.group(0).splitlines()[:80]:
        p(ln[:220])

# _simulationPaused writes
p("\n=== _simulationPaused / _timeDilationScalar writes ===")
for i, ln in enumerate(ls, 1):
    if "_simulationPaused" in ln or "_timeDilationScalar" in ln:
        if any(x in ln for x in ["=", "Write", "+=", "-="]):
            p(f"{i}|{ln.strip()[:200]}")

# TimeDilation constants
p("\n=== TimeDilation constants ===")
for i, ln in enumerate(ls, 1):
    if "TimeDilation" in ln and ("const" in ln or "static readonly" in ln or "Epsilon" in ln or "Minimum" in ln or "Maximum" in ln or "Headless" in ln):
        p(f"{i}|{ln.strip()[:200]}")

# ShouldSkipLaneDuringBootstrap
m = re.search(r"ShouldSkipLaneDuringBootstrap[\s\S]{0,800}", text)
if m:
    p("\n=== ShouldSkipLaneDuringBootstrap ===")
    for ln in m.group(0).splitlines()[:40]:
        p(ln[:220])

# blockGameplayLanes assignment in RunDispatcherUpdate
p("\n=== blockGameplayLanes / master sim early returns ===")
for i, ln in enumerate(ls, 1):
    if "blockGameplayLanes" in ln or "aupBarrierActive" in ln or "aupPreShiftPause" in ln:
        if 5158 <= i <= 5400:
            p(f"{i}|{ln.rstrip()[:200]}")

# ResolveDispatcherUnscaledDeltaTime
m = re.search(r"ResolveDispatcherUnscaledDeltaTime[\s\S]{0,600}", text)
if m:
    p("\n=== ResolveDispatcherUnscaledDeltaTime ===")
    for ln in m.group(0).splitlines()[:30]:
        p(ln[:220])

# Also check if pause is set during bootstrap / main menu
p("\n=== Pause request sites across scripts ===")
patterns = [
    "RequestSimulationPause",
    "SetSimulationPaused",
    "_simulationPaused = true",
    "PublishSimulationPaused",
    "TryPush.*Pause",
    "SimulationPauseSignal",
]
for root, _, files in os.walk("Assets/_Project/Scripts"):
    for f in files:
        if not f.endswith(".cs"):
            continue
        fp = Path(root) / f
        try:
            t = fp.read_text(encoding="utf-8", errors="replace")
        except Exception:
            continue
        for pat in ["RequestSimulationPause", "SetSimulationPaused(", "_simulationPaused = true", "SimulationPauseRequested"]:
            if pat in t:
                rel = str(fp).replace("\\", "/")
                for i, ln in enumerate(t.splitlines(), 1):
                    if pat in ln:
                        p(f"{rel}:{i}|{ln.strip()[:180]}")

# HeadlessStressFractureBot dilation re-request pattern for comparison
bot = Path(r"Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs")
if bot.exists():
    bt = bot.read_text(encoding="utf-8", errors="replace")
    p("\n=== StressFractureBot dilation context ===")
    for i, ln in enumerate(bt.splitlines(), 1):
        if "RequestHeadlessTimeDilation" in ln or "TimeDilation" in ln or "ecologyReady" in ln or "SimulationPaused" in ln:
            p(f"{i}|{ln.rstrip()[:200]}")

# Batch runner comments about zero days
br = Path(r"Assets/_Project/Scripts/QA/Headless/Editor/HeadlessSimulationBatchRunner.cs")
bt = br.read_text(encoding="utf-8", errors="replace")
p("\n=== BatchRunner header comments 1-80 ===")
for ln in bt.splitlines()[:80]:
    p(ln[:220])
p("\n=== BatchRunner ResolveTimeout ===")
for i, ln in enumerate(bt.splitlines(), 1):
    if 55 <= i <= 90 or 240 <= i <= 320 or 600 <= i <= 640:
        p(f"{i}|{ln.rstrip()[:200]}")

out.write_text("\n".join(lines), encoding="utf-8")
print("WROTE", out, out.stat().st_size)
