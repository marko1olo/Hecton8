# L17: dilated dt / step-bound / pause / HPM register evidence
import re, os
ROOT = r"C:\hades\Hecton8"
OUT = os.path.join(ROOT, r"Tools\_cline_scratch\_l17_dt_slices.txt")

def rl(rel):
    with open(os.path.join(ROOT, rel), encoding="utf-8", errors="replace") as f:
        return f.read().splitlines()

def sl(lines, a, b):
    a = max(1, a); b = min(len(lines), b)
    return [f"{i}|{lines[i-1]}" for i in range(a, b+1)]

def fa(lines, pat, flags=0):
    rx = re.compile(pat, flags)
    return [(i+1, lines[i]) for i in range(len(lines)) if rx.search(lines[i])]

o = []
sd = rl(r"Assets\_Project\Scripts\Core\SystemDispatcher.cs")
o.append(f"SD total={len(sd)}")

# ResolveDispatcherUnscaledDeltaTime definition
for ln, t in fa(sd, r"ResolveDispatcherUnscaledDeltaTime"):
    if re.search(r"(float|private|internal|static). *ResolveDispatcherUnscaledDeltaTime", t):
        o.append(f"==== ResolveDispatcherUnscaledDeltaTime def @{ln} ====")
        o.extend(sl(sd, ln, ln+50))
        break

# HeadlessTimeMode / step bound fields
for pat in [r"HeadlessTimeMode", r"_stepBoundedDeltaSeconds", r"IsStepBoundedTimeActive",
            r"FixedStepSeconds", r"MaxFixedSubstepsPerFrame", r"RequestHeadlessTimeDilation",
            r"RequestSimulationPause", r"DrainSimulationPauseSignals", r"_simulationPaused",
            r"TimeDilationPausedEpsilon", r"_timeDilationScalar\s*=", r"IsOriginShiftBootstrapLocked",
            r"IsOriginShiftFrameLocked"]:
    hits = fa(sd, pat)
    o.append(f"-- /{pat}/ count={len(hits)} first={[h[0] for h in hits[:8]]}")

for name, pat in [
    ("IsStepBoundedTimeActive", r"IsStepBoundedTimeActive"),
    ("RequestHeadlessTimeDilation", r"(void|bool)\s+RequestHeadlessTimeDilation|RequestHeadlessTimeDilation\s*\("),
    ("DrainSimulationPauseSignals", r"void\s+DrainSimulationPauseSignals"),
    ("RequestSimulationPause", r"(void|bool)\s+RequestSimulationPause"),
]:
    hits = fa(sd, pat)
    for ln, t in hits:
        if "void" in t or "bool" in t or "internal" in t or "public" in t or "=>" in t:
            o.append(f"==== {name} @{ln} ====")
            o.extend(sl(sd, ln, ln+45))
            break

# FixedStepSeconds const
for ln, t in fa(sd, r"FixedStepSeconds"):
    if "const" in t or "static" in t or "=" in t and "FixedStep" in t:
        o.append(f"fixedstep @{ln}: {t.strip()}")

# WorldDriver Ensure HPM register
wd = rl(r"Assets\_Project\Scripts\Editor\Diagnostics\H8_HeadlessWorldDriver.cs")
o.append(f"WD total={len(wd)}")
for pat in [r"TryRegisterToDispatchers|EnsurePlayer|HectonPlayerMovement|RegisterFixed|EnsureGameplay|_registeredFixed"]:
    hits = fa(wd, pat, re.I)
    o.append(f"-- WD /{pat}/ {[h[0] for h in hits[:20]]}")

for ln, t in fa(wd, r"EnsurePlayer|EnsureHpm|EnsureGameplayLocomotion|TryRegister"):
    if "void" in t or "bool" in t or "static" in t:
        o.append(f"==== WD method @{ln}: {t.strip()[:100]} ====")
        o.extend(sl(wd, ln, ln+60))

# Find where driver ensures HPM registration
for ln, t in fa(wd, r"HectonPlayerMovement|PlayerMovement|_playerMovement|EnsureLocomotionOwner|RegisterFixedTickable"):
    if ln < 3000:
        pass
hits = fa(wd, r"RegisterFixedTickable|TryRegisterFixed|Ensure.*Movement|sticky|_hpm|HPM")
o.append("WD register-related:")
for ln, t in hits[:40]:
    o.append(f"  {ln}|{t.strip()[:140]}")

# HeadlessSimulationRunner clock for comparison
hsr_paths = []
for root, dirs, files in os.walk(os.path.join(ROOT, "Assets")):
    for f in files:
        if "HeadlessSimulation" in f and f.endswith(".cs"):
            hsr_paths.append(os.path.join(root, f))
o.append(f"HeadlessSim files: {hsr_paths}")
for p in hsr_paths[:3]:
    lines = open(p, encoding="utf-8", errors="replace").read().splitlines()
    o.append(f"==== {p} ====")
    for ln, t in fa(lines, r"EnsureHeadlessSimulationClock|EnableStepBoundedTime|RequestHeadlessTimeDilation|TimeDilation"):
        o.append(f"  {ln}|{t.strip()[:140]}")
    for ln, t in fa(lines, r"void EnsureHeadless|EnsureHeadlessSimulationClock"):
        if "void" in t:
            o.extend(sl(lines, ln, ln+40))
            break

# L16 log: pause, dilation, origin, halt
logp = os.path.join(ROOT, r"Docs\AgentLogs\h8_playprobe_v0_L16.log")
if os.path.exists(logp):
    log = open(logp, encoding="utf-8", errors="replace").read()
    o.append("==== L16 log extra ====")
    for pat in [r"SimulationPaused|simulation pause|SIMPAUSE|RequestSimulationPause",
                r"TimeDilation|dilation",
                r"OriginShift|origin.shift|bootstrap.lock|AUP",
                r"IsGameReady|BootstrapState|blockGameplay",
                r"dual-register|lane heal|TryRegisterFixed|fixed lane",
                r"lateFrameTick",
                r"overrideRejected",
                r"stepBound",
                r"temporal compression|StepBoundedClamp"]:
        c = len(re.findall(pat, log, re.I))
        o.append(f"  count({pat})={c}")
    # sample lines
    for i, line in enumerate(log.splitlines(), 1):
        low = line.lower()
        if any(x in low for x in ("simpause", "simulationpaused", "time dilation", "originhift", "origin shift",
                                   "stepboundedclamp", "temporal compression", "game ready", "fixed lane",
                                   "dual-register", "lane heal")):
            o.append(f"  L{i}:{line[:200]}")
            if len([x for x in o if x.startswith('  L')]) > 40:
                break

# INPUTHOP full lines
for i, line in enumerate(open(logp, encoding="utf-8", errors="replace") if os.path.exists(logp) else [], 1):
    if "INPUTHOP" in line:
        o.append(f"HOP L{i}:{line.rstrip()[:400]}")

with open(OUT, "w", encoding="utf-8") as f:
    f.write("\n".join(o))
print("WROTE", OUT, "n", len(o), "bytes", os.path.getsize(OUT))
