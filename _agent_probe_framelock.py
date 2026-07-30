import os, re
os.chdir(r"C:\hades\Hecton8")
text = open(r"Assets\_Project\Scripts\Core\SystemDispatcher.cs", encoding="utf-8", errors="replace").read()
path = r"C:\hades\Hecton8\_agent_probe_framelock_out.txt"
chunks = []
for pat, name, n in [
    (r"_originShiftFrameLockFrame\s*=", "assignFrameLock", 3),
    (r"RequestOriginShiftFrameLock|LockOriginShiftFrame|SetOriginShiftFrameLock", "frameLockApi", 40),
    (r"internal static void RequestOriginShiftBootstrapLock", "reqBoot", 50),
    (r"internal static void ReleaseOriginShiftBootstrapLock", "relBoot", 40),
    (r"static void RequestOriginShiftBootstrapLock", "reqBoot2", 50),
    (r"private const float FrostTickIntervalSeconds", "frostConst", 5),
    (r"FrostTickIntervalSeconds", "frostAny", 3),
    (r"private static float ResolveDispatcherUnscaledDeltaTime|static float ResolveDispatcherUnscaledDeltaTime|float ResolveDispatcherUnscaledDeltaTime", "unscaled", 80),
    (r"HeadlessTimeMode|_headlessTimeMode|StepBounded", "headlessTime", 5),
]:
    for m in re.finditer(pat, text):
        s = text.rfind("\n", 0, m.start()) + 1
        l0 = text.count("\n", 0, s) + 1
        ls = text[s : s + 5000].splitlines()[:n]
        chunks.append(f"=== {name} @{l0} ===")
        chunks.extend(f"{l0+i}|{ln}" for i, ln in enumerate(ls))
        chunks.append("")

# FO frame lock calls
fo = open(r"Assets\_Project\Scripts\HectonFloatingOrigin.cs", encoding="utf-8", errors="replace").read()
for pat, name in [
    (r"FrameLock|BootstrapLock|RequestOrigin", "foLocks"),
]:
    for m in re.finditer(pat, fo):
        l = fo.count("\n", 0, m.start()) + 1
        line = fo.splitlines()[l - 1]
        chunks.append(f"FO {name} @{l}: {line.strip()[:160]}")

with open(path, "w", encoding="utf-8") as w:
    w.write("\n".join(chunks[:400]))
print("wrote", path)

# HEADLESS lines only from log
log = r"C:\hades\Hecton8\Docs\AgentLogs\headless_smoke_20260731_p0_fo_lock_drain_20260730_213321.log"
heads = []
with open(log, encoding="utf-8", errors="replace") as f:
    for i, line in enumerate(f, 1):
        if "[HEADLESS]" in line:
            heads.append(f"{i}|{line.rstrip()[:240]}")
with open(r"C:\hades\Hecton8\_agent_probe_headless_only.txt", "w", encoding="utf-8") as w:
    w.write("\n".join(heads))
print("headless lines", len(heads))
for h in heads:
    print(h[:200])
