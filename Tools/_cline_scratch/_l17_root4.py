# L17 root4: FO lock lifecycle, Headless drain, probe parity, ShouldSkip, pause drain, InputDispatcher late frame
from pathlib import Path

root = Path(r"C:\hades\Hecton8")
outp = root / r"Tools\_cline_scratch\_l17_root4.txt"
out = []
src = root / r"Assets\_Project\Scripts"

def grab(path, preds, ctx=3, after=40):
    p = Path(path)
    if not p.exists():
        out.append(f"MISSING {path}")
        return
    lines = p.read_text(encoding="utf-8", errors="replace").splitlines()
    for n, line in enumerate(lines, 1):
        if any(pred(line) if callable(pred) else pred in line for pred in preds):
            out.append(f"==== {p.name}:{n} | {line.strip()[:120]} ====")
            for j in range(max(0, n-1-ctx), min(len(lines), n-1+after)):
                out.append(f"{j+1}|{lines[j].rstrip()[:230]}")
            out.append("")

# FO lock request/release
grab(root / r"Assets\_Project\Scripts\HectonFloatingOrigin.cs", [
    "RequestOriginShiftBootstrapLock",
    "ReleaseOriginShiftBootstrapLock",
    "SceneRebaseTickLock",
    "_sceneRebaseTickLockHeld",
    "CompleteSceneRebaseBarrier",
    "ResumePhysicsAfterShift",
], ctx=2, after=25)

grab(root / r"Assets\_Project\Scripts\Core\SystemDispatcher.cs", [
    "RequestOriginShiftBootstrapLock",
    "ReleaseOriginShiftBootstrapLock",
    "ShouldSkipLaneDuringBootstrap",
    "DrainSimulationPauseSignals",
    "_runtimeGameplayBootstrapGateActive",
], ctx=1, after=45)

# HeadlessSimulationRunner FO / clock / wait
hsr = list(src.rglob("HeadlessSimulationRunner.cs"))
for p in hsr:
    grab(p, [
        "BootstrapLock", "OriginShift", "TryFlush", "CopyBootstrapDrain",
        "EnsureHeadlessSimulationClock", "IsOriginShift", "WaitFor",
        "SimulationPaused", "stepBound", "EnableStepBounded",
    ], ctx=1, after=30)

# Probe FO / clock
grab(root / r"Assets\_Project\Scripts\Editor\Diagnostics\H8_HeadlessPlayModeProbe.cs", [
    "BootstrapLock", "OriginShift", "TryFlush", "CopyBootstrapDrain",
    "EnsureProbeSimulationClock", "IsOriginShift", "SimulationPaused",
    "EnableStepBounded", "RequestHeadlessTimeDilation",
], ctx=1, after=35)

# WorldDriver FO
grab(root / r"Assets\_Project\Scripts\Editor\Diagnostics\H8_HeadlessWorldDriver.cs", [
    "BootstrapLock", "OriginShift", "TryFlush", "IsOriginShift",
    "EnsureGameplayLocomotion", "EnsureDispatcherRegistration",
], ctx=1, after=25)

# InputDispatcher LateFrameTick + PreSim path
grab(root / r"Assets\_Project\Scripts\Core\InputDispatcher.cs", [
    "void LateFrameTick",
    "DiagRecordLateFrame",
    "PreSimulationInputTick",
    "TryRegisterToDispatcher",
    "frameId",
], ctx=1, after=35)

# Log: FO drain / lock lines
log = (root / r"Docs\AgentLogs\h8_playprobe_v0_L16.log").read_text(encoding="utf-8", errors="replace").splitlines()
out.append("==== LOG FO/lock/pause/halt ====")
for i, l in enumerate(log, 1):
    low = l.lower()
    if any(k in low for k in (
        "bootstraplock", "bootstrap lock", "originfshift", "origin shift",
        "scene rebase", "scenerebase", "physics pause", "physicspause",
        "floatingorigin", "floating origin", "tryflush", "drain snapshot",
        "fo lock", "fo:", "simulationhalted", "safe halt", "ispaused",
        "requestsimulationpause", "time dilation",
    )) or "FO " in l or "[FO]" in l or "HectonFloatingOrigin" in l:
        if "GameBootstrapper" in l and "Bootstrap" in l and "Floating" not in l:
            continue
        out.append(f"L{i}|{l[:350]}")

# L16 docs
for p in (root / r"Docs\V0_Playtest").glob("V0_L16*"):
    t = p.read_text(encoding="utf-8", errors="replace")
    out.append(f"\n==== DOC {p.name} (first 200 lines) ====")
    for j, line in enumerate(t.splitlines()[:200], 1):
        out.append(f"{j}|{line[:200]}")

outp.write_text("\n".join(out), encoding="utf-8")
print("wrote", outp, "lines", len(out), "bytes", outp.stat().st_size)
