import os
os.chdir(r"C:\hades\Hecton8")
p = r"Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs"
lines = open(p, encoding="utf-8", errors="replace").read().splitlines()
keys = (
    "TryMarkEcologyReady",
    "RequestHeadlessTimeDilation",
    "RegisterRuntimeLanes",
    "FastTick",
    "FrostTick",
    "LateFrameTick",
    "_ecologyReady",
    "_dayAccumulator",
    "_simulatedSeconds",
    "DrainPendingDayAudits",
    "timeDilation",
    "TimeDilationScalar",
    "MaybeLogEcology",
    "SimulationPaused",
    "RequestSimulationPause",
    "TryArmEcology",
    "WriteResult",
    "ExecuteDailyAudit",
    "EnsureSimulation",
    "ApplyHeadless",
    "PublishGameReady",
    "IsGameReady",
    "void Update",
    "class Headless",
)
out = []
for i, l in enumerate(lines):
    if any(k in l for k in keys):
        out.append(f"{i+1}:{l}")
path = "_agent_scan_runner_out.txt"
open(path, "w", encoding="utf-8").write("\n".join(out))
print(f"WROTE {path} {len(out)} hits; file_lines={len(lines)}")
