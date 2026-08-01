# L17 dig: extract critical code slices for FixedTick -> hop2 path
import re
import os

ROOT = r"C:\hades\Hecton8"
OUT = os.path.join(ROOT, r"Tools\_cline_scratch\_l17_core_slices.txt")

def read_lines(rel):
    path = os.path.join(ROOT, rel)
    with open(path, encoding="utf-8", errors="replace") as f:
        return f.read().splitlines()

def slice_lines(lines, start, end):
    # 1-based inclusive
    start = max(1, start)
    end = min(len(lines), end)
    out = []
    for i in range(start, end + 1):
        out.append(f"{i}|{lines[i-1]}")
    return out

def find_all(lines, pat, flags=0):
    rx = re.compile(pat, flags)
    return [(i + 1, lines[i]) for i in range(len(lines)) if rx.search(lines[i])]

sections = []

# --- SystemDispatcher critical bodies ---
sd = read_lines(r"Assets\_Project\Scripts\Core\SystemDispatcher.cs")
sections.append(f"==== SystemDispatcher.cs total={len(sd)} ====")

for name, pat in [
    ("RunFixedStepAccumulator", r"void RunFixedStepAccumulator\b"),
    ("DispatchFixedStep", r"void DispatchFixedStep\b"),
    ("ResolveDispatcherUnscaledDeltaTime", r"ResolveDispatcherUnscaledDeltaTime\b"),
    ("EnableStepBoundedTime", r"EnableStepBoundedTime\b"),
    ("ConsumeFrameTimeDilationScalar", r"ConsumeFrameTimeDilationScalar\b"),
    ("ShouldSkipLaneDuringBootstrap", r"ShouldSkipLaneDuringBootstrap\b"),
    ("RunDispatcherUpdate", r"void RunDispatcherUpdate\b"),
]:
    hits = find_all(sd, pat)
    sections.append(f"-- {name} hits: {[h[0] for h in hits[:8]]}")

# Dump RunDispatcherUpdate region around fixed step call
hits = find_all(sd, r"RunFixedStepAccumulator\(")
if hits:
    ln = hits[0][0]
    sections.append(f"==== SD RunFixedStepAccumulator call site ~{ln} ====")
    sections.extend(slice_lines(sd, ln - 80, ln + 40))

hits = find_all(sd, r"void RunFixedStepAccumulator\b")
if hits:
    ln = hits[0][0]
    sections.append(f"==== SD RunFixedStepAccumulator body ~{ln} ====")
    sections.extend(slice_lines(sd, ln, ln + 80))

hits = find_all(sd, r"void DispatchFixedStep\b")
if hits:
    ln = hits[0][0]
    sections.append(f"==== SD DispatchFixedStep body ~{ln} ====")
    sections.extend(slice_lines(sd, ln, ln + 60))

hits = find_all(sd, r"ResolveDispatcherUnscaledDeltaTime")
# find definition
for ln, t in hits:
    if "float" in t or "=>" in t or "{" in t or "private" in t or "internal" in t or "public" in t:
        if "ResolveDispatcherUnscaledDeltaTime" in t and "(" in t and "void" not in t.split("Resolve")[0][-20:]:
            sections.append(f"==== SD ResolveDispatcherUnscaledDeltaTime ~{ln} ====")
            sections.extend(slice_lines(sd, ln, ln + 40))
            break

hits = find_all(sd, r"EnableStepBoundedTime\s*\(")
for ln, t in hits:
    if "void" in t or "bool" in t or "public" in t or "internal" in t:
        sections.append(f"==== SD EnableStepBoundedTime ~{ln} ====")
        sections.extend(slice_lines(sd, ln, ln + 35))
        break

hits = find_all(sd, r"ConsumeFrameTimeDilationScalar")
for ln, t in hits:
    if "(" in t and (")" in t) and ("float" in t or "=>" in t or "private" in t):
        sections.append(f"==== SD ConsumeFrameTimeDilationScalar ~{ln} ====")
        sections.extend(slice_lines(sd, ln, ln + 25))
        break

hits = find_all(sd, r"ShouldSkipLaneDuringBootstrap")
for ln, t in hits:
    if "bool" in t or "static" in t:
        sections.append(f"==== SD ShouldSkipLaneDuringBootstrap ~{ln} ====")
        sections.extend(slice_lines(sd, ln, ln + 25))
        break

# dilation / pause early outs near RunDispatcherUpdate start
hits = find_all(sd, r"void RunDispatcherUpdate\b")
if hits:
    ln = hits[0][0]
    sections.append(f"==== SD RunDispatcherUpdate head ~{ln} ====")
    sections.extend(slice_lines(sd, ln, ln + 200))

# --- HPM ---
hpm = read_lines(r"Assets\_Project\Scripts\HectonPlayerMovement.cs")
sections.append(f"==== HectonPlayerMovement.cs total={len(hpm)} ====")

for name, pat in [
    ("FixedTick", r"\bFixedTick\s*\("),
    ("SampleGameplay", r"SampleGameplayLocomotionInputForFixedStep"),
    ("ProcessPlayerInputFrame", r"ProcessPlayerInputFrame"),
    ("ResolveInputManagerBinding", r"ResolveInputManagerBinding"),
    ("TryRegisterToDispatchers", r"TryRegisterToDispatchers"),
    ("IsGameplayInputBlockedByMenu", r"IsGameplayInputBlockedByMenu"),
    ("CurrentMovementIntent01", r"CurrentMovementIntent01"),
]:
    hits = find_all(hpm, pat)
    sections.append(f"-- {name} hits: {[h[0] for h in hits[:15]]}")

# FixedTick method body
hits = find_all(hpm, r"public\s+void\s+FixedTick\s*\(|void\s+FixedTick\s*\(")
if hits:
    ln = hits[0][0]
    sections.append(f"==== HPM FixedTick ~{ln} ====")
    sections.extend(slice_lines(hpm, ln, ln + 120))

hits = find_all(hpm, r"void\s+SampleGameplayLocomotionInputForFixedStep\s*\(|private\s+void\s+SampleGameplay")
if not hits:
    hits = find_all(hpm, r"SampleGameplayLocomotionInputForFixedStep\s*\(")
# prefer definition
defs = [h for h in hits if "void" in h[1] or "(" in h[1] and "{" in h[1]]
if not defs:
    # find line that looks like method def
    defs = [h for h in find_all(hpm, r"SampleGameplayLocomotionInputForFixedStep") if "void" in h[1]]
if defs:
    ln = defs[0][0]
    sections.append(f"==== HPM SampleGameplay ~{ln} ====")
    sections.extend(slice_lines(hpm, ln, ln + 50))

hits = find_all(hpm, r"void\s+ProcessPlayerInputFrame")
if hits:
    ln = hits[0][0]
    sections.append(f"==== HPM ProcessPlayerInputFrame ~{ln} ====")
    sections.extend(slice_lines(hpm, ln, ln + 55))

hits = find_all(hpm, r"void\s+ResolveInputManagerBinding|IInputService\s+ResolveInputManagerBinding")
if not hits:
    hits = [h for h in find_all(hpm, r"ResolveInputManagerBinding") if "void" in h[1] or "(" in h[1]]
if hits:
    ln = hits[0][0]
    sections.append(f"==== HPM ResolveInputManagerBinding ~{ln} ====")
    sections.extend(slice_lines(hpm, ln, ln + 40))

hits = find_all(hpm, r"void\s+TryRegisterToDispatchers|bool\s+TryRegisterToDispatchers")
if not hits:
    hits = [h for h in find_all(hpm, r"TryRegisterToDispatchers") if "void" in h[1] or "bool" in h[1]]
if hits:
    ln = hits[0][0]
    sections.append(f"==== HPM TryRegisterToDispatchers ~{ln} ====")
    sections.extend(slice_lines(hpm, ln, ln + 50))

hits = find_all(hpm, r"bool\s+IsGameplayInputBlockedByMenu|IsGameplayInputBlockedByMenu\s*\(")
defs = [h for h in hits if "bool" in h[1] or "static" in h[1]]
if defs:
    ln = defs[0][0]
    sections.append(f"==== HPM IsGameplayInputBlockedByMenu ~{ln} ====")
    sections.extend(slice_lines(hpm, ln, ln + 30))

# --- InputHandler ---
ih = read_lines(r"Assets\_Project\Scripts\Gameplay\HectonPlayerInputHandler.cs")
sections.append(f"==== HectonPlayerInputHandler.cs total={len(ih)} ====")
sections.extend(slice_lines(ih, 1, min(80, len(ih))))

# --- InputDispatcher GetState / CaptureState ---
idd = read_lines(r"Assets\_Project\Scripts\Core\InputDispatcher.cs")
sections.append(f"==== InputDispatcher.cs total={len(idd)} ====")
for name, pat in [
    ("GetState", r"PlayerInputState\s+GetState\s*\("),
    ("CurrentInputState", r"CurrentInputState"),
    ("CaptureState", r"void\s+CaptureState\b"),
    ("ApplyAutomationOverride", r"ApplyAutomationOverride"),
    ("DiagRecord", r"DiagRecordReadObservation"),
]:
    hits = find_all(idd, pat)
    sections.append(f"-- {name} hits: {[h[0] for h in hits[:10]]}")

hits = find_all(idd, r"PlayerInputState\s+GetState\s*\(")
if hits:
    ln = hits[0][0]
    sections.append(f"==== ID GetState ~{ln} ====")
    sections.extend(slice_lines(idd, ln, ln + 40))

hits = find_all(idd, r"void\s+CaptureState\b")
if hits:
    ln = hits[0][0]
    sections.append(f"==== ID CaptureState ~{ln} ====")
    sections.extend(slice_lines(idd, ln, ln + 80))

# --- GlobalRegistry TryRegisterFixedTickable ---
gr = read_lines(r"Assets\_Project\Scripts\Core\GlobalRegistry.cs")
sections.append(f"==== GlobalRegistry.cs total={len(gr)} ====")
hits = find_all(gr, r"TryRegisterFixedTickable")
sections.append(f"-- TryRegisterFixedTickable hits: {[h[0] for h in hits[:15]]}")
for ln, t in hits:
    if "bool" in t or "static" in t or "public" in t:
        sections.append(f"==== GR TryRegisterFixedTickable ~{ln} ====")
        sections.extend(slice_lines(gr, ln, ln + 50))
        break

# L15 dual-register heal markers
for pat in [r"HealDual", r"dual.register", r"sticky", r"FixedTickable", r"RegisterFixed"]:
    hits = find_all(gr, pat, re.I)
    if hits:
        sections.append(f"-- GR /{pat}/ first hits: {[(h[0], h[1].strip()[:100]) for h in hits[:8]]}")

# --- Probe SIMCLOCK / step bound ---
probe = read_lines(r"Assets\_Project\Scripts\Editor\Diagnostics\H8_HeadlessPlayModeProbe.cs")
sections.append(f"==== Probe total={len(probe)} ====")
for pat in [r"EnsureProbeSimulationClock", r"EnableStepBoundedTime", r"MaybeEnsureProbe", r"ProbeTimeDilation", r"stepBound"]:
    hits = find_all(probe, pat, re.I)
    sections.append(f"-- /{pat}/ hits: {[h[0] for h in hits[:12]]}")

hits = find_all(probe, r"EnsureProbeSimulationClock|void EnsureProbe")
defs = [h for h in hits if "void" in h[1] or "bool" in h[1]]
if defs:
    ln = defs[0][0]
    sections.append(f"==== Probe EnsureClock ~{ln} ====")
    sections.extend(slice_lines(probe, ln, ln + 60))

# --- L16 log: any fixed dispatch counters ---
log_path = os.path.join(ROOT, r"Docs\AgentLogs\h8_playprobe_v0_L16.log")
if os.path.exists(log_path):
    sections.append("==== L16 log greps ====")
    with open(log_path, encoding="utf-8", errors="replace") as f:
        log = f.read()
    for pat in [
        r"SIMCLOCK",
        r"readHop=2",
        r"hop2",
        r"INPUTHOP",
        r"FixedTick",
        r"fixedStep",
        r"DispatchFixed",
        r"movementIntent",
        r"blockGameplay",
        r"Player lane",
        r"fixedTickable",
        r"RegisterFixed",
        r"stepBound",
        r"dilatedDelta",
        r"presimTick",
        r"currentInputStateFrame",
    ]:
        c = len(re.findall(pat, log, re.I))
        sections.append(f"  count({pat})={c}")
    # extract a few SIMCLOCK and INPUTHOP and Swim lines
    for i, line in enumerate(log.splitlines(), 1):
        if "SIMCLOCK" in line or "INPUTHOP" in line or ("Swim" in line and "movementIntent" in line) or "FAIL Swim" in line or "MOMENT" in line and "Swim" in line:
            if any(x in line for x in ("SIMCLOCK", "INPUTHOP", "movementIntent", "FAIL Swim", "Swim")):
                sections.append(f"  L{i}:{line[:220]}")

with open(OUT, "w", encoding="utf-8") as f:
    f.write("\n".join(sections))
print("WROTE", OUT, "lines", len(sections), "bytes", os.path.getsize(OUT))
