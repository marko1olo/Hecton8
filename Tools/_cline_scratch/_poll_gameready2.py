from pathlib import Path
import os
import time

log = Path(r"Docs/AgentLogs/headless_smoke_20260730_p0_gameready.log")
out = Path(r"Tools/_cline_scratch/_poll_out2.txt")
lines = []
if log.exists():
    st = log.stat()
    lines.append(f"mtime={time.ctime(st.st_mtime)} size={st.st_size}")
    t = log.read_text(encoding="utf-8", errors="replace")
else:
    t = ""
    lines.append("NO_LOG")

# extract key lines containing interesting tokens
needles = [
    "PublishGameReady",
    "dispatcher acquired",
    "runtime lanes",
    "lanes registered",
    "waiting for dispatcher",
    "BOOTSTRAP",
    "ecology",
    "Ecology",
    "ECOLOGY",
    "OriginShift",
    "origin-shift",
    "IsOriginShift",
    "ShouldSkipLane",
    "PriorityLayer",
    "LateFrame",
    "ColdTick",
    "timeDilation",
    "FailAndQuit",
    "BATCH",
    "GameReady",
    "biomass",
    "HeadlessSimulation",
    "ERROR",
    "Exception",
    "NullReference",
]
hits = []
for i, line in enumerate(t.splitlines(), 1):
    if any(n in line for n in needles):
        # skip stack frames noise somewhat
        if line.strip().startswith("UnityEngine.") or line.strip().startswith("System."):
            continue
        if "(at Assets/" in line and not line.strip().startswith("["):
            continue
        hits.append(f"{i}:{line[:240]}")

lines.append(f"hit_count={len(hits)}")
lines.extend(hits[-80:])  # last 80 interesting
r = Path(r"Docs/AgentLogs/HeadlessSimulationResult_HEADLESS_SIMULATION_RUNNER.json")
lines.append("RESULT=" + (r.read_text(encoding="utf-8", errors="replace") if r.exists() else "NONE"))
gate = Path(r"Tools/_cline_scratch/p0_gameready_gate_out.txt")
lines.append("GATE=" + (gate.read_text(encoding="utf-8", errors="replace")[-800:] if gate.exists() else "NONE"))
out.write_text("\n".join(lines), encoding="utf-8")
print("ok", st.st_size if log.exists() else 0)
