import os
os.chdir(r"C:\\hades\\Hecton8")
p = r"Assets/_Project/Scripts/Core/SystemDispatcher.cs"
lines = open(p, encoding="utf-8", errors="replace").read().splitlines()
out = []
# find method defs
keys = (
    "RequestHeadlessTimeDilation",
    "RequestSimulationPause",
    "ConsumeFrameTimeDilationScalar",
    "SetTimeDilationScalar",
    "RunDispatcherUpdate",
    "RunFrostTick",
    "RunFastTick",
    "ShouldSkipLaneDuringBootstrap",
    "SimulationPaused",
    "TimeDilationPausedEpsilon",
    "HeadlessTimeDilationMaximumScalar",
    "_prePauseTimeDilationScalar",
    "_timeDilationScalar",
    "_simulationPaused",
)
for i, l in enumerate(lines):
    if any(k in l for k in keys):
        out.append(f"{i+1}:{l}")

# dump method bodies for pause and dilation
ranges = []
for i, l in enumerate(lines):
    if "public void RequestHeadlessTimeDilation" in l or "public void RequestSimulationPause" in l or "private float ConsumeFrameTimeDilationScalar" in l or "private void SetTimeDilationScalar" in l:
        ranges.append(i)

chunks = []
for start in ranges:
    end = min(len(lines), start + 55)
    chunks.append(f"\n==== BODY @{start+1} ====")
    for i in range(start, end):
        chunks.append(f"{i+1}:{lines[i]}")

path = "_agent_scan_disp_out.txt"
open(path, "w", encoding="utf-8").write("\n".join(out) + "\n" + "\n".join(chunks))
print(f"WROTE {path} hits={len(out)} bodies={len(ranges)} lines={len(lines)}")
