# L17 halt / lateFrame / pump dig — scratch only
from pathlib import Path
import re

root = Path(r"C:\hades\Hecton8")
log = root / r"Docs\AgentLogs\h8_playprobe_v0_L16.log"
outp = root / r"Tools\_cline_scratch\_l17_halt.txt"
src = root / r"Assets\_Project\Scripts"

lines = log.read_text(encoding="utf-8", errors="replace").splitlines()
out = []
out.append(f"LOG lines={len(lines)}")

# Find SimulationHalted and related
for i, l in enumerate(lines, 1):
    low = l.lower()
    if any(x in low for x in (
        "simulationhalted", "simulation halted", "safehalt", "safe halt",
        "halt simulation", "issimulationhalted", "signalcorridor",
        "pumpfired", "lateframetick", "fixedstep", "readhop=2",
        "originfshift", "bootstraplocked", "auppres", "rebase",
    )) or "IsSimulationHalted" in l or "Halted" in l:
        out.append(f"L{i}|{l[:450]}")

# Also grab SWIM full line and nearby
for i, l in enumerate(lines, 1):
    if "MOMENT" in l and "Swim" in l:
        for j in range(max(0, i - 5), min(len(lines), i + 15)):
            out.append(f"CTX{j+1}|{lines[j][:400]}")

# Source grep IsSimulationHalted
out.append("\n==== SOURCE IsSimulationHalted ====")
for p in src.rglob("*.cs"):
    try:
        t = p.read_text(encoding="utf-8", errors="replace")
    except Exception:
        continue
    if "IsSimulationHalted" not in t and "SimulationHalted" not in t:
        continue
    for n, line in enumerate(t.splitlines(), 1):
        if "IsSimulationHalted" in line or "SimulationHalted" in line or "SetSimulationHalted" in line or "HaltSimulation" in line:
            out.append(f"{p.name}:{n}|{line.strip()[:200]}")

# Source: pumpFired / lateFrameTick census fields
out.append("\n==== SOURCE pumpFired lateFrameTick ====")
for p in src.rglob("*.cs"):
    try:
        t = p.read_text(encoding="utf-8", errors="replace")
    except Exception:
        continue
    if "pumpFired" not in t and "lateFrameTick" not in t and "LateFrameTick" not in t:
        continue
    for n, line in enumerate(t.splitlines(), 1):
        if any(k in line for k in ("pumpFired", "lateFrameTick", "LateFrameTick", "PumpFired", "presimTick", "DiagRecord")):
            out.append(f"{p.name}:{n}|{line.strip()[:220]}")

outp.write_text("\n".join(out), encoding="utf-8")
print("wrote", outp, "lines", len(out))
