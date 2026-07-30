# scratch only — do not commit
from pathlib import Path

path = Path(r"C:\hades\Hecton8\Docs\AgentLogs\headless_smoke_20260730_p0fix.log")
out = Path(r"C:\hades\Hecton8\Tools\_cline_scratch\hl_analysis.txt")
data = path.read_text(encoding="utf-8", errors="replace")
lines = data.splitlines()
o = []
o.append(f"SIZE {len(data)}")
o.append(f"LINES {len(lines)}")
keys = [
    "Headless SceneActivate short-circuit",
    "MarkMainMenuReached",
    "HeadlessSimulation",
    "biomass",
    "BIOMASS",
    "Ecology",
    "ecology",
    "Exiting batchmode",
    "Batchmode",
    "h8headless",
    "GameBootstrapper",
    "02_HECTON",
    "Exception",
    "Fatal",
    "ERROR",
    "PASS",
    "FAIL",
    "leak",
    "Leak",
    "completed",
    "Complete",
    "SimulationBatch",
    "day",
    "Day",
]
for k in keys:
    c = data.count(k)
    if c:
        o.append(f"COUNT {k!r}: {c}")

o.append("--- TAIL 120 ---")
o.extend(L[:320] for L in lines[-120:])

needles = (
    "short-circuit",
    "HeadlessSimulation",
    "h8headless",
    "biomass",
    "BIOMASS",
    "Exiting batchmode",
    "SimulationBatch",
    "Headless ",
    "overallPass",
    "ecology day",
    "HeadlessEcology",
)
o.append("--- KEY HITS ---")
for i, L in enumerate(lines):
    if any(n in L for n in needles):
        o.append(f"{i+1}|{L[:300]}")

out.write_text("\n".join(o), encoding="utf-8")
print(f"WROTE {out} out_lines={len(o)}")
