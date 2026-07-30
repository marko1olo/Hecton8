#!/usr/bin/env python3
"""Scratch: critical hang surfaces only. DO NOT COMMIT."""
import os

ROOT = r"C:\hades\Hecton8"


def show(rel, start, end):
    p = os.path.join(ROOT, rel)
    lines = open(p, encoding="utf-8", errors="replace").read().splitlines()
    print(f"\n######## {rel}:{start}-{end}")
    for i in range(start - 1, min(end, len(lines))):
        print(f"{i+1}|{lines[i]}")


# Heartbeat wait + node ready
show(r"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs", 5440, 5525)
show(r"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs", 5550, 5605)
# headless shortcuts for nodes
show(r"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs", 5800, 5945)
# Demiurge fauna
show(r"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs", 9481, 9540)
# SceneActivate headless path again + LoadMainMenu
show(r"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs", 3133, 3178)
show(r"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs", 3275, 3330)
# Headless runner WaitForDispatcher + ColdTick + startup timeout
show(r"Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs", 1, 120)
show(r"Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs", 170, 430)
show(r"Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs", 560, 620)
# Batch timeout sizing
show(r"Assets/_Project/Scripts/QA/Headless/Editor/HeadlessSimulationBatchRunner.cs", 1, 80)
show(r"Assets/_Project/Scripts/QA/Headless/Editor/HeadlessSimulationBatchRunner.cs", 230, 340)
# status file + csv
for name in (
    "HeadlessSimulationResult_HEADLESS_SIMULATION_RUNNER.json",
    "HeadlessSimulationDaily_HEADLESS_SIMULATION_RUNNER.csv",
    "HeadlessSimulationBatchRunner_HEADLESS_SIMULATION_RUNNER.txt",
):
    p = os.path.join(ROOT, "Docs", "AgentLogs", name)
    print(f"\n--- {name} ---")
    if os.path.exists(p):
        print(open(p, encoding="utf-8", errors="replace").read()[-2000:])
    else:
        print("MISSING")

# Unity alive?
import subprocess
r = subprocess.run(["tasklist", "/FI", "IMAGENAME eq Unity.exe"], capture_output=True, text=True)
print("\n--- Unity processes ---")
print(r.stdout)
