from pathlib import Path

root = Path(r"C:\hades\Hecton8")
out = []

def slice_file(rel, start, end, label=None):
    p = root / rel
    lines = p.read_text(encoding="utf-8", errors="replace").splitlines()
    out.append(f"==== {label or rel} {start}-{end} ====")
    for i in range(start - 1, min(end, len(lines))):
        out.append(f"{i+1}|{lines[i].rstrip()[:230]}")
    out.append("")

# Critical slices by known line numbers
slice_file(r"Assets\_Project\Scripts\Core\SystemDispatcher.cs", 1720, 1940, "SD lock/pause API")
slice_file(r"Assets\_Project\Scripts\Core\SystemDispatcher.cs", 1985, 2040, "SD DrainPause")
slice_file(r"Assets\_Project\Scripts\Core\SystemDispatcher.cs", 6175, 6240, "SD RunFixedStep head")
slice_file(r"Assets\_Project\Scripts\Core\SystemDispatcher.cs", 6540, 6680, "SD stepbounded")
slice_file(r"Assets\_Project\Scripts\HectonFloatingOrigin.cs", 300, 400, "FO TryFlush")

# Find ShouldSkipLane body
sd = (root / r"Assets\_Project\Scripts\Core\SystemDispatcher.cs").read_text(encoding="utf-8", errors="replace").splitlines()
for n, l in enumerate(sd, 1):
    if "ShouldSkipLaneDuringBootstrap" in l and ("bool" in l or "return" in l or "static" in l):
        out.append(f"SD ShouldSkip @{n}: {l.strip()[:200]}")
        for j in range(n, min(n + 25, len(sd) + 1)):
            out.append(f"{j}|{sd[j-1].rstrip()[:230]}")
        out.append("")

# Find runtimeGameplayBootstrapGate
for n, l in enumerate(sd, 1):
    if "_runtimeGameplayBootstrapGateActive" in l:
        out.append(f"SD gate @{n}: {l.strip()[:200]}")

# HeadlessSimulationRunner - search key methods
for p in (root / r"Assets\_Project\Scripts").rglob("HeadlessSimulationRunner.cs"):
    lines = p.read_text(encoding="utf-8", errors="replace").splitlines()
    out.append(f"==== HSR path {p} lines={len(lines)} ====")
    for n, l in enumerate(lines, 1):
        if any(k in l for k in (
            "EnsureHeadlessSimulationClock", "BootstrapLock", "OriginShift",
            "TryFlush", "CopyBootstrapDrain", "IsOriginShiftBootstrapLocked",
            "EnableStepBounded", "RequestHeadlessTimeDilation", "RequestSimulationPause",
            "DrainFloating", "WaitOrigin", "origin lock", "FO drain"
        )):
            out.append(f"HSR:{n}|{l.rstrip()[:220]}")
            # context
            for j in range(n, min(n + 40, len(lines) + 1)):
                if j == n:
                    continue
                out.append(f"HSR:{j}|{lines[j-1].rstrip()[:220]}")
                if lines[j-1].strip().startswith("}") and j > n + 5:
                    break
            out.append("--")

# Probe clock + FO
probe = root / r"Assets\_Project\Scripts\Editor\Diagnostics\H8_HeadlessPlayModeProbe.cs"
pl = probe.read_text(encoding="utf-8", errors="replace").splitlines()
out.append(f"==== PROBE lines={len(pl)} ====")
for n, l in enumerate(pl, 1):
    if any(k in l for k in (
        "EnsureProbeSimulationClock", "BootstrapLock", "OriginShift",
        "TryFlush", "CopyBootstrapDrain", "IsOriginShift",
        "EnableStepBounded", "RequestHeadlessTimeDilation", "RequestSimulationPause",
        "SIMCLOCK", "stepBound"
    )):
        out.append(f"PR:{n}|{l.rstrip()[:220]}")
        for j in range(n + 1, min(n + 45, len(pl) + 1)):
            out.append(f"PR:{j}|{pl[j-1].rstrip()[:220]}")
            if pl[j-1].strip() == "}" and j > n + 3:
                break
        out.append("--")

# InputDispatcher LateFrameTick
idisp = root / r"Assets\_Project\Scripts\Core\InputDispatcher.cs"
il = idisp.read_text(encoding="utf-8", errors="replace").splitlines()
for n, l in enumerate(il, 1):
    if "void LateFrameTick" in l or "DiagRecordLateFrame" in l or "void PreSimulationInputTick" in l:
        out.append(f"==== ID {l.strip()[:80]} @{n} ====")
        for j in range(n, min(n + 50, len(il) + 1)):
            out.append(f"{j}|{il[j-1].rstrip()[:220]}")
        out.append("")

# FO Request/Release lock call sites
fo = (root / r"Assets\_Project\Scripts\HectonFloatingOrigin.cs").read_text(encoding="utf-8", errors="replace").splitlines()
for n, l in enumerate(fo, 1):
    if any(k in l for k in (
        "RequestOriginShiftBootstrapLock", "ReleaseOriginShiftBootstrapLock",
        "_sceneRebaseTickLockHeld", "AcquireSceneRebase", "ReleaseSceneRebase",
        "ResumePhysicsAfterShift", "CompleteSceneRebaseBarrier"
    )):
        out.append(f"FO:{n}|{l.rstrip()[:220]}")
        for j in range(n + 1, min(n + 30, len(fo) + 1)):
            out.append(f"FO:{j}|{fo[j-1].rstrip()[:220]}")
            if fo[j-1].strip() == "}" and j > n + 2:
                break
        out.append("--")

# LOG only FO-related (tight)
log = (root / r"Docs\AgentLogs\h8_playprobe_v0_L16.log").read_text(encoding="utf-8", errors="replace").splitlines()
out.append("==== LOG tight ====")
for i, l in enumerate(log, 1):
    if any(k in l for k in (
        "FloatingOrigin", "OriginShift", "bootstrap lock", "BootstrapLock",
        "SceneRebase", "physics pause", "PhysicsPause", "TryFlush",
        "SIMCLOCK", "INPUTHOP", "SimulationHalted", "MOMENT", "SWIM"
    )):
        if "GameBootstrapper" in l and "Floating" not in l:
            continue
        out.append(f"L{i}|{l[:300]}")

outp = root / r"Tools\_cline_scratch\_l17_extract.txt"
text = "\n".join(out)
outp.write_text(text, encoding="utf-8")
print("wrote", outp, "lines", len(out), "chars", len(text))
# split
open(root / r"Tools\_cline_scratch\_l17_e1.txt", "w", encoding="utf-8").write(text[:20000])
open(root / r"Tools\_cline_scratch\_l17_e2.txt", "w", encoding="utf-8").write(text[20000:40000])
open(root / r"Tools\_cline_scratch\_l17_e3.txt", "w", encoding="utf-8").write(text[40000:60000])
open(root / r"Tools\_cline_scratch\_l17_e4.txt", "w", encoding="utf-8").write(text[60000:])
print("splits ok")
