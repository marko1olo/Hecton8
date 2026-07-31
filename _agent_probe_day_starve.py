"""Probe Fast/Frost day-advance starve after ecology-ready BATCH_TIMEOUT."""
import os
import re
from pathlib import Path

os.chdir(r"C:\hades\Hecton8")
out = Path(r"C:\hades\Hecton8\_agent_probe_day_starve_out.txt")
lines = []

def p(msg=""):
    lines.append(str(msg))

# --- SystemDispatcher key symbols ---
disp = Path(r"Assets/_Project/Scripts/Core/SystemDispatcher.cs")
text = disp.read_text(encoding="utf-8", errors="replace")
symbols = [
    "RequestHeadlessTimeDilation",
    "ConsumeFrameTimeDilationScalar",
    "SimulationPaused",
    "IsOriginShiftBootstrapLocked",
    "IsOriginShiftFrameLockedForCurrentFrame",
    "RunDispatcherUpdate",
    "RunFrostTick",
    "RunFastTick",
    "FrostTickIntervalSeconds",
    "aupPreShiftPause",
    "_headlessTimeDilation",
    "Time.timeScale",
    "MaxCadenceSubstepsPerFrame",
]
p("=== SystemDispatcher symbol hits ===")
for s in symbols:
    hits = [(i + 1, ln.rstrip()) for i, ln in enumerate(text.splitlines()) if s in ln]
    p(f"--- {s} ({len(hits)}) ---")
    for n, ln in hits[:25]:
        p(f"{n}|{ln[:200]}")

# Extract RunDispatcherUpdate body roughly
p("\n=== RunDispatcherUpdate region ===")
m = re.search(r"private void RunDispatcherUpdate\b", text)
if not m:
    m = re.search(r"void RunDispatcherUpdate\b", text)
if m:
    start = text.rfind("\n", 0, m.start()) + 1
    chunk = text[start:start + 4500]
    for i, ln in enumerate(chunk.splitlines()[:120]):
        p(ln[:220])

p("\n=== ConsumeFrameTimeDilationScalar region ===")
m = re.search(r"ConsumeFrameTimeDilationScalar", text)
if m:
    start = max(0, text.rfind("\n", 0, m.start()) - 200)
    # find method start
    method = re.search(
        r"(private|internal|public).{0,80}ConsumeFrameTimeDilationScalar[\s\S]{0,1200}",
        text,
    )
    if method:
        for ln in method.group(0).splitlines()[:60]:
            p(ln[:220])

p("\n=== RequestHeadlessTimeDilation region ===")
method = re.search(
    r"(private|internal|public).{0,80}RequestHeadlessTimeDilation[\s\S]{0,1500}",
    text,
)
if method:
    for ln in method.group(0).splitlines()[:80]:
        p(ln[:220])

p("\n=== RunFrostTick region ===")
method = re.search(
    r"(private|internal|public).{0,40}void RunFrostTick[\s\S]{0,2000}",
    text,
)
if method:
    for ln in method.group(0).splitlines()[:80]:
        p(ln[:220])

# Batch runner timeout
p("\n=== HeadlessSimulationBatchRunner timeout ===")
br = Path(r"Assets/_Project/Scripts/QA/Headless/Editor/HeadlessSimulationBatchRunner.cs")
if br.exists():
    bt = br.read_text(encoding="utf-8", errors="replace")
    for pat in ["BATCH_TIMEOUT", "timeout", "Timeout", "maxWall", "Watchdog", "PollRunState"]:
        hits = [(i + 1, ln.rstrip()) for i, ln in enumerate(bt.splitlines()) if pat in ln]
        if hits:
            p(f"--- {pat} ({len(hits)}) ---")
            for n, ln in hits[:15]:
                p(f"{n}|{ln[:200]}")

# TickDispatcher interface
p("\n=== ITickDispatcher / RequestHeadlessTimeDilation callers ===")
for root, _, files in os.walk("Assets/_Project/Scripts"):
    for f in files:
        if not f.endswith(".cs"):
            continue
        fp = Path(root) / f
        try:
            t = fp.read_text(encoding="utf-8", errors="replace")
        except Exception:
            continue
        if "RequestHeadlessTimeDilation" in t or "SimulationPaused" in t:
            rel = str(fp).replace("\\", "/")
            for i, ln in enumerate(t.splitlines(), 1):
                if "RequestHeadlessTimeDilation" in ln or (
                    "SimulationPaused" in ln and ("=" in ln or "get" in ln or "set" in ln)
                ):
                    p(f"{rel}:{i}|{ln.strip()[:180]}")

out.write_text("\n".join(lines), encoding="utf-8")
print("WROTE", out, "bytes", out.stat().st_size)
