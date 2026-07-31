import os
from pathlib import Path

os.chdir(r"C:\hades\Hecton8")
out = Path("_agent_probe_pause_root_out.txt")
lines = []

def p(m=""):
    lines.append(str(m))

# Rest of pause_skip out
prev = Path("_agent_probe_pause_skip_out.txt")
if prev.exists():
    t = prev.read_text(encoding="utf-8", errors="replace")
    # from GameReady section
    i = t.find("GameReady")
    if i >= 0:
        p(t[i:i+8000])

# Search who pauses at boot
p("\n=== SimulationPauseSignal push sites ===")
for fp in Path("Assets/_Project/Scripts").rglob("*.cs"):
    try:
        t = fp.read_text(encoding="utf-8", errors="replace")
    except Exception:
        continue
    if "SimulationPauseSignal" not in t and "RequestSimulationPause(true" not in t and "RequestSimulationPause(true)" not in t:
        if "Paused = 1" not in t and "paused: true" not in t:
            continue
    rel = str(fp).replace("\\", "/")
    for j, ln in enumerate(t.splitlines(), 1):
        if any(x in ln for x in [
            "SimulationPauseSignal",
            "RequestSimulationPause(true",
            "RequestSimulationPause( true",
            "Paused = 1",
            "Paused=1",
        ]):
            p(f"{rel}:{j}|{ln.strip()[:200]}")

# GlobalRegistry.TickDispatcher type
p("\n=== TickDispatcher property ===")
for fp in Path("Assets/_Project/Scripts").rglob("GlobalRegistry*.cs"):
    t = fp.read_text(encoding="utf-8", errors="replace")
    for j, ln in enumerate(t.splitlines(), 1):
        if "TickDispatcher" in ln:
            p(f"{fp}:{j}|{ln.rstrip()[:180]}")

# ITickDispatcher has SimulationPaused and RequestSimulationPause?
p("\n=== ITickable pause API ===")
it = Path("Assets/_Project/Scripts/ITickable.cs")
if it.exists():
    for j, ln in enumerate(it.read_text(encoding="utf-8", errors="replace").splitlines(), 1):
        if j >= 60 and j <= 100:
            p(f"{j}|{ln}")

# Initial _simulationPaused and who sets pause on menu
p("\n=== PauseMenu open on start ===")
for fp in Path("Assets/_Project/Scripts").rglob("*PauseMenu*"):
    if fp.suffix != ".cs":
        continue
    t = fp.read_text(encoding="utf-8", errors="replace")
    for j, ln in enumerate(t.splitlines(), 1):
        if "Start(" in ln or "OnEnable" in ln or "RequestSimulationPause" in ln or "IsOpen" in ln or "Open(" in ln:
            p(f"{fp.name}:{j}|{ln.strip()[:180]}")

out.write_text("\n".join(lines), encoding="utf-8")
print("WROTE", out, out.stat().st_size)
