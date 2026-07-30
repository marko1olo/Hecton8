# -*- coding: utf-8 -*-
from pathlib import Path

root = Path(r"C:\hades\Hecton8")
log = root / "Docs/AgentLogs/headless_smoke_20260730_p0fix.log"
lines = log.read_text(encoding="utf-8", errors="replace").splitlines()
print("lines", len(lines), "bytes", log.stat().st_size)

needles = [
    "GameBootstrapper",
    "Headless",
    "headless",
    "ECOLOGY",
    "ecology",
    "Ecology",
    "BATCH",
    "dispatcher",
    "Dispatcher",
    "Fauna",
    "timeDilation",
    "TIMEOUT",
    "FailAndQuit",
    "SeedObserver",
    "biomass",
    "Biomass",
    "MarkMainMenu",
    "EXEMPT",
    "short-circuit",
    "SceneActivate",
    "SimulationRunner",
    "WaitFor",
    "dilation",
    "DailyAudit",
    "ecologySampled",
    "TryMarkEcology",
    "PlayMode",
    "EnterPlay",
    "isPlaying",
]

hits = []
for i, l in enumerate(lines, 1):
    if any(n in l for n in needles):
        # skip pure stack filename noise somewhat
        if l.strip().startswith("(Filename:"):
            continue
        hits.append((i, l[:240]))

print("hits", len(hits))
out = root / "Tools/_cline_scratch/p0_hits.txt"
with out.open("w", encoding="utf-8") as f:
    for n, l in hits:
        f.write(f"{n}: {l}\n")
print("wrote", out)

# also print key subset
print("---KEY---")
key2 = (
    "Headless",
    "headless",
    "ECOLOGY",
    "ecology",
    "BATCH_TIMEOUT",
    "FailAndQuit",
    "MarkMainMenu",
    "short-circuit",
    "SceneActivate",
    "dispatcher",
    "Fauna",
    "dilation",
    "TryMarkEcology",
    "ecologySampled",
    "EXEMPT",
    "SeedObserver",
    "biomass",
    "EnterPlay",
    "Play mode",
)
for n, l in hits:
    if any(k in l for k in key2):
        print(f"{n}: {l}")

# artifacts
for rel in [
    "Docs/AgentLogs/HeadlessSimulationBatchRunner_HEADLESS_SIMULATION_RUNNER.txt",
    "Docs/AgentLogs/HeadlessSimulationDaily_HEADLESS_SIMULATION_RUNNER.csv",
    "Docs/AgentLogs/HeadlessSimulationResult_HEADLESS_SIMULATION_RUNNER.json",
    "Docs/AgentLogs/H8_V0_PLAYTEST_SMOKE_GATE.json",
]:
    p = root / rel
    print("===", rel, "exists" if p.exists() else "MISSING", "===")
    if p.exists():
        print(p.read_text(encoding="utf-8", errors="replace")[:4000])
