# -*- coding: utf-8 -*-
import os, re

base = r"C:\hades\Hecton8"
hpm = os.path.join(base, r"Assets\_Project\Scripts\HectonPlayerMovement.cs")
probe = os.path.join(base, r"Assets\_Project\Scripts\Editor\Diagnostics\H8_HeadlessPlayModeProbe.cs")
handler = os.path.join(base, r"Assets\_Project\Scripts\Gameplay\HectonPlayerInputHandler.cs")
log = os.path.join(base, r"Docs\AgentLogs\h8_playprobe_v0_L09.log")
outp = os.path.join(base, r"Tools\_cline_scratch\_l10_hop2_dig_out.txt")

lines = open(hpm, encoding="utf-8", errors="replace").read().splitlines()
out = []

def dump_range(a, b, label=None):
    out.append("===== %s %d-%d =====" % (label or "", a, b))
    for i in range(a - 1, min(b, len(lines))):
        out.append("%d|%s" % (i + 1, lines[i]))
    out.append("")

# suit assignment sites
out.append("=== currentSuitData assignments ===")
for i, L in enumerate(lines):
    if "currentSuitData" in L and ("=" in L or "SuitData" in L):
        if any(x in L for x in ("=", "null", "new", "return", "if")):
            out.append("%d|%s" % (i + 1, L.rstrip()))
out.append("")

# EnsureJuiceProcessor callers
out.append("=== EnsureJuiceProcessor / _juiceProcessor = ===")
for i, L in enumerate(lines):
    if "EnsureJuiceProcessor" in L or "_juiceProcessor =" in L or "_juiceProcessor==" in L:
        out.append("%d|%s" % (i + 1, L.rstrip()))
out.append("")

# TryRegisterToDispatchers / FixedTick register
out.append("=== registration related ===")
for i, L in enumerate(lines):
    if any(k in L for k in (
        "TryRegisterToDispatchers", "TryRegisterFixedTickable", "_registeredFixedTick",
        "RegisterFixed", "IFixedTickable", "EnsurePlayerRuntimeSubsystems"
    )):
        out.append("%d|%s" % (i + 1, L.rstrip()))
out.append("")

# LocomotionHold
out.append("=== LocomotionHold / HoldInProgress in HPM ===")
for i, L in enumerate(lines):
    if re.search(r"LocomotionHold|HoldInProgress|locomotionHold|_locomotionHold", L, re.I):
        out.append("%d|%s" % (i + 1, L.rstrip()))
out.append("")

# OnRegister / Awake suit
for needle in ("void Awake", "void OnRegister", "void OnDependencyInject", "ApplySuit", "SetSuit", "LoadDefaultSuit", "defaultSuit", "starterSuit"):
    for i, L in enumerate(lines):
        if needle in L:
            out.append("HIT %s @%d|%s" % (needle, i + 1, L.rstrip()))
out.append("")

# Dump Awake/OnRegister/suit apply blocks - find line numbers first
awake_ln = next((i+1 for i,L in enumerate(lines) if re.search(r"\bvoid Awake\s*\(", L)), None)
onreg_ln = next((i+1 for i,L in enumerate(lines) if re.search(r"\bvoid OnRegister\s*\(", L)), None)
ondi_ln = next((i+1 for i,L in enumerate(lines) if re.search(r"\bvoid OnDependencyInject\s*\(", L)), None)
ensrts_ln = next((i+1 for i,L in enumerate(lines) if "EnsurePlayerRuntimeSubsystems" in L and "void" in L), None)
tryreg_ln = next((i+1 for i,L in enumerate(lines) if "TryRegisterToDispatchers" in L and "void" in L), None)
out.append("awake=%s onreg=%s ondi=%s ensrts=%s tryreg=%s" % (awake_ln, onreg_ln, ondi_ln, ensrts_ln, tryreg_ln))

for ln, span, lab in [
    (awake_ln, 80, "Awake"),
    (onreg_ln, 60, "OnRegister"),
    (ondi_ln, 80, "OnDependencyInject"),
    (ensrts_ln, 80, "EnsurePlayerRuntimeSubsystems"),
    (tryreg_ln, 120, "TryRegisterToDispatchers"),
]:
    if ln:
        dump_range(ln, ln + span, lab)

# Probe LocomotionHold / movementIntent / waitingOn
if os.path.isfile(probe):
    pl = open(probe, encoding="utf-8", errors="replace").read().splitlines()
    out.append("=== PROBE LocomotionHold/waitingOn/movementIntent/hop ===")
    for i, L in enumerate(pl):
        if re.search(r"LocomotionHold|waitingOn|movementIntent|readHop|INPUTHOP|SampleGameplay|Swim", L, re.I):
            out.append("P%d|%s" % (i + 1, L.rstrip()[:200]))
    out.append("")

# Handler TryReadFrame
if os.path.isfile(handler):
    hl = open(handler, encoding="utf-8", errors="replace").read().splitlines()
    out.append("=== HectonPlayerInputHandler full ===")
    for i, L in enumerate(hl):
        out.append("H%d|%s" % (i + 1, L.rstrip()))
    out.append("")

# L09 log key lines
if os.path.isfile(log):
    ll = open(log, encoding="utf-8", errors="replace").read().splitlines()
    out.append("=== L09 key markers ===")
    keys = ("INPUTHOP", "readHop", "STARTERGRANT", "MOMENT", "Swim", "Tool BLOCKED",
            "LocomotionHold", "waitingOn", "suit", "juice", "FixedTick", "movementIntent",
            "IsPlayerInputEnabled", "overrideRejected", "publishOk")
    for i, L in enumerate(ll):
        if any(k in L for k in keys):
            # throttle: only interesting
            if any(k in L for k in ("INPUTHOP", "STARTERGRANT", "MOMENT", "LocomotionHold", "waitingOn", "Tool BLOCKED", "Swim FAIL", "RESULT")):
                out.append("L%d|%s" % (i + 1, L.rstrip()[:240]))
    out.append("log_lines=%d" % len(ll))

open(outp, "w", encoding="utf-8").write("\n".join(out))
print("wrote", outp, "chars", os.path.getsize(outp))
