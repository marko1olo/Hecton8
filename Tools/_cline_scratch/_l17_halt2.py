# L17 tight halt dig
from pathlib import Path

root = Path(r"C:\hades\Hecton8")
log = root / r"Docs\AgentLogs\h8_playprobe_v0_L16.log"
outp = root / r"Tools\_cline_scratch\_l17_halt2.txt"
src = root / r"Assets\_Project\Scripts"

lines = log.read_text(encoding="utf-8", errors="replace").splitlines()
out = []

# Exact SimulationHalted log lines
for i, l in enumerate(lines, 1):
    if "SimulationHalted" in l or "simulation halted" in l.lower() or "SafeHalt" in l or "SAFE HALT" in l:
        # context
        for j in range(max(0, i-3), min(len(lines), i+8)):
            out.append(f"HALTCTX{j+1}|{lines[j][:400]}")
        out.append("---")

# Full INPUTHOP lines untruncated
for i, l in enumerate(lines, 1):
    if "INPUTHOP" in l:
        out.append(f"HOP{i}|{l}")

# Swim moment full
for i, l in enumerate(lines, 1):
    if "MOMENT" in l and "Swim" in l:
        out.append(f"SWIM{i}|{l}")

# Source IsSimulationHalted property + set
out.append("\n==== SRC halt API ====")
for name in ("SignalBusRegistry.cs", "SignalCorridorRuntime.cs", "BootstrapStatus.cs", "SystemDispatcher.cs", "InputDispatcher.cs"):
    hits = list(src.rglob(name))
    for p in hits:
        t = p.read_text(encoding="utf-8", errors="replace").splitlines()
        for n, line in enumerate(t, 1):
            if any(k in line for k in (
                "IsSimulationHalted", "SimulationHalted", "SetSimulationHalt",
                "HaltSimulation", "_simulationHalted", "MarkSimulationHalted",
                "RequestSimulationHalt", "ClearSimulationHalt"
            )):
                out.append(f"{p.name}:{n}|{line.rstrip()[:240]}")

# pumpFired / lateFrameTick definition site with surrounding
out.append("\n==== SRC census counters ====")
for p in src.rglob("*.cs"):
    t = p.read_text(encoding="utf-8", errors="replace")
    if "pumpFired" not in t and "lateFrameTick" not in t:
        continue
    lines_t = t.splitlines()
    for n, line in enumerate(lines_t, 1):
        if "pumpFired" in line or "lateFrameTick" in line or "PumpFired" in line or "LateFrameTickCount" in line:
            # surround
            for j in range(max(0, n-2), min(len(lines_t), n+3)):
                out.append(f"{p.name}:{j+1}|{lines_t[j].rstrip()[:240]}")
            out.append("--")

# ConsumeFrameTimeDilationScalar body
out.append("\n==== SRC ConsumeFrameTimeDilationScalar ====")
sd = root / r"Assets\_Project\Scripts\Core\SystemDispatcher.cs"
t = sd.read_text(encoding="utf-8", errors="replace").splitlines()
for n, line in enumerate(t, 1):
    if "ConsumeFrameTimeDilationScalar" in line or "TimeDilationPausedEpsilon" in line:
        for j in range(max(0, n-1), min(len(t), n+40)):
            out.append(f"SD:{j+1}|{t[j].rstrip()[:240]}")
        out.append("--")

# RunFixedStepAccumulator body start
out.append("\n==== SRC RunFixedStepAccumulator head ====")
for n, line in enumerate(t, 1):
    if "void RunFixedStepAccumulator" in line:
        for j in range(n-1, min(len(t), n+80)):
            out.append(f"SD:{j+1}|{t[j].rstrip()[:240]}")
        break

# INPUTHOP emission site
out.append("\n==== SRC INPUTHOP emit ====")
for p in src.rglob("*.cs"):
    t2 = p.read_text(encoding="utf-8", errors="replace")
    if "INPUTHOP" not in t2:
        continue
    lines_t = t2.splitlines()
    for n, line in enumerate(lines_t, 1):
        if "INPUTHOP" in line:
            for j in range(max(0, n-30), min(len(lines_t), n+20)):
                out.append(f"{p.name}:{j+1}|{lines_t[j].rstrip()[:240]}")
            out.append("--")

outp.write_text("\n".join(out), encoding="utf-8")
print("wrote", outp, "n", len(out))
