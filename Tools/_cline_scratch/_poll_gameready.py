from pathlib import Path

p = Path(r"Docs/AgentLogs/headless_smoke_20260730_p0_gameready.log")
out = Path(r"Tools/_cline_scratch/_poll_out.txt")
t = p.read_text(encoding="utf-8", errors="replace") if p.exists() else ""
lines = []
lines.append(f"size {len(t)}")
keys = [
    "PublishGameReady on bootstrap",
    "MarkMainMenuReached + PublishGameReady",
    "dispatcher acquired",
    "runtime lanes",
    "ecologySampled",
    "BOOTSTRAP_TIMEOUT",
    "BATCH_TIMEOUT",
    "error CS",
    "IsGameReady",
    "PublishGameReady",
    "waiting for dispatcher",
    "Headless SceneActivate",
    "ECOLOGY",
    "timeDilation",
    "FailAndQuit",
    "ecology ready",
    "LateFrame",
    "lanes registered",
    "GameReady",
    "biomass",
]
for k in keys:
    lines.append(f"{k}: {t.count(k)}")
lines.append("---TAIL---")
lines.append(t[-2000:])
r = Path(r"Docs/AgentLogs/HeadlessSimulationResult_HEADLESS_SIMULATION_RUNNER.json")
lines.append("RESULT " + (r.read_text(encoding="utf-8", errors="replace") if r.exists() else "NONE"))
out.write_text("\n".join(lines), encoding="utf-8")
print("WROTE", out, "chars", len(t))
