# L17 root confirm: origin lock, late frame path, frameId, blockGameplay
from pathlib import Path

root = Path(r"C:\hades\Hecton8")
outp = root / r"Tools\_cline_scratch\_l17_root3.txt"
sd = (root / r"Assets\_Project\Scripts\Core\SystemDispatcher.cs").read_text(encoding="utf-8", errors="replace").splitlines()
fo = (root / r"Assets\_Project\Scripts\HectonFloatingOrigin.cs").read_text(encoding="utf-8", errors="replace").splitlines()
out = []

def slice_around(lines, pred, before=2, after=50, label=""):
    for n, line in enumerate(lines, 1):
        if pred(line):
            out.append(f"==== {label} @{n} ====")
            for j in range(max(0, n-1-before), min(len(lines), n-1+after)):
                out.append(f"{j+1}|{lines[j].rstrip()[:240]}")
            out.append("")

# Origin lock enter/exit
for n, line in enumerate(sd, 1):
    if any(k in line for k in (
        "_originShiftBootstrapLockCount", "BeginOriginShiftBootstrap", "EndOriginShiftBootstrap",
        "OriginShiftBootstrapLock", "_originShiftFrameLockFrame", "LockOriginShiftFrame",
        "_aupPreShiftPauseFrameId", "AupPreShift"
    )):
        out.append(f"SD:{n}|{line.rstrip()[:240]}")

out.append("")
# RunLateFrame
slice_around(sd, lambda l: "void RunLateFrame" in l or "LateFrameTick" in l and "void " in l, 0, 60, "LateFrame dispatch")
slice_around(sd, lambda l: "RunLateFrame" in l and ("private" in l or "public" in l or "void" in l), 0, 40, "RunLateFrame")

# BuildMasterDispatcherTiming FrameId
slice_around(sd, lambda l: "BuildMasterDispatcherTiming" in l and "DispatcherTimingDTO" in l, 0, 40, "BuildMasterTiming")
slice_around(sd, lambda l: "FrameId" in l and ("=" in l or "frameId" in l.lower()), 0, 3, "FrameId assigns")

# TryFlushInitialSceneRebaseBeforeTicks
slice_around(fo, lambda l: "TryFlushInitialSceneRebaseBeforeTicks" in l, 0, 80, "FO TryFlush")
for n, line in enumerate(fo, 1):
    if any(k in line for k in ("BootstrapLock", "bootstrapLock", "OriginShiftBootstrap", "BeginBootstrap", "EndBootstrap", "InitialSceneRebase")):
        out.append(f"FO:{n}|{line.rstrip()[:240]}")

# blockGameplay / IsGameReady / runtimeGameplayBootstrapGate
out.append("\n==== blockGameplay gates ====")
for n, line in enumerate(sd, 1):
    if any(k in line for k in (
        "_runtimeGameplayBootstrapGateActive", "blockGameplayLanes", "IsGameReady",
        "ShouldSkipLaneDuringBootstrap"
    )):
        out.append(f"SD:{n}|{line.rstrip()[:240]}")

slice_around(sd, lambda l: "bool ShouldSkipLaneDuringBootstrap" in l, 0, 30, "ShouldSkipLane")

# DispatchFixedStep Player lane skip
slice_around(sd, lambda l: "void DispatchFixedStep" in l or "void RunFixedStepAccumulator" in l, 0, 90, "Fixed path")

# RequestHeadlessTimeDilation + EnableStepBoundedTime
slice_around(sd, lambda l: "RequestHeadlessTimeDilation" in l and "void" in l, 0, 35, "RequestHeadlessTimeDilation")
slice_around(sd, lambda l: "EnableStepBoundedTime" in l, 0, 40, "EnableStepBoundedTime")

# Probe EnsureProbeSimulationClock
probe = (root / r"Assets\_Project\Scripts\Editor\Diagnostics\H8_HeadlessPlayModeProbe.cs").read_text(encoding="utf-8", errors="replace").splitlines()
slice_around(probe, lambda l: "EnsureProbeSimulationClock" in l, 0, 50, "Probe clock")

# SignalBusRegistry IsSimulationHalted
for p in (root / r"Assets\_Project\Scripts").rglob("*SignalBus*"):
    t = p.read_text(encoding="utf-8", errors="replace").splitlines()
    for n, line in enumerate(t, 1):
        if "SimulationHalt" in line or "IsSimulationHalted" in line:
            out.append(f"{p.name}:{n}|{line.rstrip()[:240]}")

outp.write_text("\n".join(out), encoding="utf-8")
print("wrote", outp, "lines", len(out), "chars", outp.stat().st_size)
