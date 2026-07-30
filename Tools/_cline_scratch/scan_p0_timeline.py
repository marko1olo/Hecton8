# -*- coding: utf-8 -*-
from pathlib import Path
log = Path(r"C:\hades\Hecton8\Docs\AgentLogs\headless_smoke_20260730_p0fix.log")
lines = log.read_text(encoding="utf-8", errors="replace").splitlines()
needles = (
    "SystemDispatcher",
    "waiting for dispatcher",
    "DISPATCHER",
    "runner installed",
    "RegisterService",
    "SceneActivate",
    "short-circuit",
    "MarkMainMenu",
    "FaunaSimulation",
    "allSystemsReady",
    "gameReady",
    "EnterPlay",
    "isPlaying",
    "PlayMode",
    "timeout",
    "TIMEOUT",
    "BATCH",
    "ecology",
    "ECOLOGY",
    "Bootstrap complete",
    "bootstrap complete",
    "InitializeSystemDispatcher",
    "EnsureSystemDispatcher",
    "Headless",
    "[HEADLESS]",
    "FailAndQuit",
    "ColdTick",
    "startup",
)
for i, l in enumerate(lines, 1):
    if any(n in l for n in needles) and not l.strip().startswith("(Filename"):
        # filter stack noise
        if "at Assets/" in l or "at ./Library" in l:
            continue
        if "UnityEngine." in l and "Debug" in l:
            continue
        if l.strip().startswith("UnityEngine") or l.strip().startswith("Hecton8.") and ":" in l and "(" in l:
            if not l.strip().startswith("["):
                continue
        print(f"{i}: {l[:220]}")

# also dump lines 680-750 and 2350-2550 and 2800-2913 for context
print("\n=== CONTEXT 680-760 ===")
for i in range(679, min(760, len(lines))):
    print(f"{i+1}: {lines[i][:200]}")
print("\n=== CONTEXT 2360-2520 ===")
for i in range(2359, min(2520, len(lines))):
    print(f"{i+1}: {lines[i][:200]}")
