import os, re
os.chdir(r"C:\hades\Hecton8")
text = open(r"Assets\_Project\Scripts\Core\SystemDispatcher.cs", encoding="utf-8", errors="replace").read()
out = []
for pat, name in [
    (r"FrostTickIntervalSeconds\s*=", "FrostInterval"),
    (r"float ConsumeFrameTimeDilationScalar", "ConsumeDilation"),
    (r"void SetTimeDilationScalar", "SetDilation"),
    (r"TimeDilationPausedEpsilon", "PauseEps"),
    (r"void DrainSimulationPauseSignals", "DrainPause"),
    (r"_timeDilationScalar\s*=", "AssignDilation"),
    (r"_simulationPaused\s*=", "AssignPaused"),
    (r"HeadlessTimeDilationMaximumScalar", "HeadlessMax"),
    (r"ResolveDispatcherUnscaledDeltaTime", "ResolveUnscaled"),
    (r"IsOriginShiftFrameLockedForCurrentFrame", "FrameLock"),
    (r"RequestOriginShiftBootstrapLock", "ReqLock"),
    (r"ReleaseOriginShiftBootstrapLock", "RelLock"),
    (r"_originShiftBootstrapLockCount", "LockCount"),
    (r"_originShiftFrameLockFrame", "FrameLockField"),
]:
    for m in re.finditer(pat, text):
        l = text.count("\n", 0, m.start()) + 1
        out.append(f"{name} @{l}")

chunks = []
for pat, name, nlines in [
    (r"private float ConsumeFrameTimeDilationScalar", "ConsumeFrameTimeDilationScalar", 80),
    (r"private void SetTimeDilationScalar\(", "SetTimeDilationScalar", 80),
    (r"private float ResolveDispatcherUnscaledDeltaTime", "ResolveUnscaled", 60),
    (r"private const float FrostTickIntervalSeconds", "FrostInterval", 5),
    (r"private void DrainSimulationPauseSignals", "DrainPause", 80),
    (r"internal static void RequestOriginShiftBootstrapLock", "ReqBootstrapLock", 40),
    (r"internal static void ReleaseOriginShiftBootstrapLock", "RelBootstrapLock", 40),
    (r"private static void RequestOriginShiftBootstrapLock", "ReqBootstrapLock2", 40),
    (r"static void RequestOriginShiftBootstrapLock", "ReqBootstrapLock3", 40),
]:
    m = re.search(pat, text)
    if not m:
        chunks.append(f"=== {name} NOT FOUND ===")
        continue
    s = text.rfind("\n", 0, m.start()) + 1
    l0 = text.count("\n", 0, s) + 1
    ls = text[s : s + 6000].splitlines()[:nlines]
    chunks.append(f"=== {name} @{l0} ===")
    chunks.extend(f"{l0+i}|{ln}" for i, ln in enumerate(ls))

path = r"C:\hades\Hecton8\_agent_probe_dilation_out.txt"
with open(path, "w", encoding="utf-8") as w:
    w.write("\n".join(out) + "\n\n" + "\n".join(chunks))
print("wrote", path, "symbols", len(out))

log = r"C:\hades\Hecton8\Docs\AgentLogs\headless_smoke_20260731_p0_fo_lock_drain_20260730_213321.log"
hits = []
if os.path.isfile(log):
    keys = (
        "dilation",
        "pause",
        "Frost",
        "ecology ready",
        "ecology wait clock",
        "runtime lanes",
        "BOOTSTRAP",
        "timeScale",
        "SimulationPaused",
        "halt",
        "[HEADLESS]",
    )
    with open(log, encoding="utf-8", errors="replace") as f:
        for i, line in enumerate(f, 1):
            if "ecology wait progress" in line:
                continue
            low = line.lower()
            if any(k.lower() in low for k in keys):
                hits.append(f"{i}|{line.rstrip()[:220]}")
    with open(r"C:\hades\Hecton8\_agent_probe_log_hits.txt", "w", encoding="utf-8") as w:
        w.write("\n".join(hits[-250:]))
    print("log hits", len(hits))
else:
    print("no log")
