#!/usr/bin/env python3
"""Scratch: diagnose SceneActivate hang + smoke status. DO NOT COMMIT."""
import os
import re
import time

ROOT = r"C:\hades\Hecton8"


def read_lines(rel):
    p = os.path.join(ROOT, rel)
    return open(p, encoding="utf-8", errors="replace").read().splitlines()


def show_range(rel, start, end):
    lines = read_lines(rel)
    print(f"\n######## {rel}:{start}-{end}")
    for i in range(start - 1, min(end, len(lines))):
        print(f"{i+1}|{lines[i]}")


def hits(rel, pats, max_per=30):
    lines = read_lines(rel)
    print(f"\n######## hits in {rel}")
    for pat in pats:
        found = [(i + 1, l.rstrip()) for i, l in enumerate(lines) if pat in l]
        print(f"=== {pat!r} n={len(found)}")
        for ln, line in found[:max_per]:
            print(f"  {ln}: {line[:220]}")


# smoke status
log = os.path.join(ROOT, r"Docs\AgentLogs\headless_smoke_20260730.log")
st = os.stat(log)
print("log size", st.st_size, "mtime", time.strftime("%H:%M:%S", time.localtime(st.st_mtime)),
      "age_s", int(time.time() - st.st_mtime))
result = os.path.join(ROOT, r"Docs\AgentLogs\HeadlessSimulationResult_HEADLESS_SIMULATION_RUNNER.json")
print("result", os.path.exists(result), os.path.getsize(result) if os.path.exists(result) else 0)
if os.path.exists(result):
    print(open(result, encoding="utf-8", errors="replace").read())

# log keywords for headless path
lines = open(log, encoding="utf-8", errors="replace").read().splitlines()
keys = re.compile(
    r"headless|HEADLESS|h8headless|_headless|MainMenu|LoadMainMenu|LoadGameplay|"
    r"SceneActivate|MarkMainMenu|Handoff|BOOTSTRAP_TIMEOUT|ECOLOGY|Ecosystem|"
    r"HeadlessSimulation|filterLogType|GameReady|01_MAIN|sandbox|REFUSED",
    re.I,
)
print("--- log key lines ---")
for i, l in enumerate(lines):
    if keys.search(l) and not re.search(r"^\s*\(at |StateMachineBox|DoMoveNext|MoveNext \(\)|Filename:", l):
        print(f"{i+1}|{l[:220]}")

# bootstrap symbols
hits(
    r"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs",
    [
        "_headlessBootMode",
        "HeadlessBoot",
        "h8headless",
        "LoadMainMenuAsync",
        "TryResolveBootstrapGameplayHandoffScene",
        "LoadGameplaySceneFromBootstrapHandoffAsync",
        "IsBootstrapScene",
        "MarkMainMenuReached",
    ],
)

# Demiurge fauna IsReady / heartbeat
hits(
    r"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs",
    [
        "class DemiurgeFaunaSimulationService",
        "IsReady",
        "IServiceHeartbeat",
        "Beat",
        "LastHeartbeat",
    ],
)

# Headless runner startup
hits(
    r"Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs",
    [
        "WaitForDispatcherAndStart",
        "RunStartupAsync",
        "Awake",
        "OnEnable",
        "Install",
        "Create",
        "DontDestroy",
        "BootstrapTimeout",
        "_bootstrapDeadline",
        "Dispatcher",
    ],
)
