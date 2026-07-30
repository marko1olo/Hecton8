#!/usr/bin/env python3
"""Scratch: print source ranges. DO NOT COMMIT."""
import os
import sys

ROOT = r"C:\hades\Hecton8"


def show(rel, start, end):
    p = os.path.join(ROOT, rel)
    lines = open(p, encoding="utf-8", errors="replace").read().splitlines()
    print(f"\n######## {rel}:{start}-{end} (file has {len(lines)})")
    for i in range(start - 1, min(end, len(lines))):
        print(f"{i+1}|{lines[i]}")


# IsHeadlessBootRequested
show(r"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs", 6930, 6980)
# TryResolveBootstrapGameplayHandoffScene
show(r"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs", 3405, 3480)
# LoadMainMenuAsync
show(r"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs", 3270, 3340)
# heartbeat wait loop
show(r"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs", 5440, 5520)
# IsNodeReady / heartbeat checks
show(r"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs", 5550, 5620)
# headless node shortcuts
show(r"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs", 5800, 5940)
# Demiurge fauna heartbeat
show(r"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs", 9475, 9550)
# HeadlessSimulationRunner WaitForDispatcherAndStart + ColdTick
show(r"Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs", 170, 430)
# BatchRunner timeout resolve + PollRunState
show(r"Assets/_Project/Scripts/QA/Headless/Editor/HeadlessSimulationBatchRunner.cs", 220, 340)
show(r"Assets/_Project/Scripts/QA/Headless/Editor/HeadlessSimulationBatchRunner.cs", 300, 380)

# log tail after SceneActivate
log = os.path.join(ROOT, r"Docs\AgentLogs\headless_smoke_20260730.log")
lines = open(log, encoding="utf-8", errors="replace").read().splitlines()
print(f"\n######## log lines 2510-end ({len(lines)} total)")
for i in range(2509, len(lines)):
    print(f"{i+1}|{lines[i][:240]}")

# also look for HEADLESS after waiting for dispatcher
print("\n######## all HEADLESS / dispatcher / ecology / timeout lines")
import re
pat = re.compile(r"HEADLESS|dispatcher|DISPATCHER|ecology|Ecology|TIMEOUT|timeout|MarkMainMenu|GameReady|IsGameReady|LoadMainMenu|handoff|Handoff|headlessBoot|_headless", re.I)
for i, l in enumerate(lines):
    if pat.search(l) and "MoveNext" not in l and "StateMachineBox" not in l and "(at " not in l and "Filename:" not in l:
        print(f"{i+1}|{l[:240]}")
