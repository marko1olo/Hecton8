import os, re
os.chdir(r"C:\hades\Hecton8")
fo = open(r"Assets\_Project\Scripts\HectonFloatingOrigin.cs", encoding="utf-8", errors="replace").read()
disp = open(r"Assets\_Project\Scripts\Core\SystemDispatcher.cs", encoding="utf-8", errors="replace").read()
out = []
for label, text in [("FO", fo), ("DISP", disp)]:
    for pat in [
        r"RequestOriginShiftFrameLock",
        r"ReleaseOriginShiftFrameLock",
        r"_originShiftFrameLockFrame",
        r"IsOriginShiftFrameLocked",
        r"RequestAupPreShiftPause",
        r"_aupPreShiftPauseFrameId",
        r"SimulationPaused",
        r"RequestSimulationPause",
        r"TimeDilationMinimumScalar",
        r"HeadlessTimeDilationMaximumScalar",
        r"FrostTickIntervalSeconds",
        r"ResolveDispatcherUnscaledDeltaTime",
        r"_headlessTimeMode",
        r"HeadlessTimeMode",
    ]:
        for m in re.finditer(pat, text):
            l = text.count("\n", 0, m.start()) + 1
            line = text.splitlines()[l - 1].strip()
            out.append(f"{label} {pat} @{l}: {line[:180]}")

# dump FO regions around frame lock / bootstrap lock
for pat, name, n in [
    (r"RequestOriginShiftFrameLock\(", "FO call frame lock context", 30),
    (r"RequestOriginShiftBootstrapLock\(", "FO call boot lock context", 30),
    (r"TryFlushInitialSceneRebaseBeforeTicks", "FO TryFlush", 120),
    (r"AcquireSceneRebaseTickLock|ReleaseSceneRebaseTickLock|_physicsPauseActive", "FO physics pause", 40),
]:
    for m in re.finditer(pat, fo):
        s = fo.rfind("\n", 0, max(0, m.start() - 400)) + 1
        # better: start a bit before match
        s = fo.rfind("\n", 0, m.start()) + 1
        # go back ~15 lines
        for _ in range(15):
            prev = fo.rfind("\n", 0, s - 1)
            if prev < 0:
                break
            s = prev + 1
        l0 = fo.count("\n", 0, s) + 1
        ls = fo[s : s + 7000].splitlines()[:n]
        out.append(f"\n=== {name} @{l0} ===")
        out.extend(f"{l0+i}|{ln}" for i, ln in enumerate(ls))

# dispatcher unscaled + frost interval by line numbers from earlier
for line_no, n in [(75, 5), (6687, 80), (4701, 30), (111, 10)]:
    lines = disp.splitlines()
    start = max(0, line_no - 1)
    out.append(f"\n=== DISP lines {line_no}+ ===")
    for i in range(start, min(len(lines), start + n)):
        out.append(f"{i+1}|{lines[i]}")

path = r"C:\hades\Hecton8\_agent_probe_fo_framelock_out.txt"
open(path, "w", encoding="utf-8").write("\n".join(out))
print("wrote", path, "lines", len(out))
