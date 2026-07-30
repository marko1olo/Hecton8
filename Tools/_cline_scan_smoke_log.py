#!/usr/bin/env python3
"""Scratch: parse headless smoke log. DO NOT COMMIT."""
import os
import re
import time
import sys

p = sys.argv[1] if len(sys.argv) > 1 else r"Docs/AgentLogs/headless_smoke_20260730.log"
if not os.path.isabs(p):
    p = os.path.join(r"C:\hades\Hecton8", p)

st = os.stat(p)
print("size", st.st_size, "mtime", time.strftime("%H:%M:%S", time.localtime(st.st_mtime)))
lines = open(p, encoding="utf-8", errors="replace").read().splitlines()
print("lines", len(lines))

skip = re.compile(
    r"^\s*$|^\s*\(at |^\s*UnityEngine\.|^\s*System\.|^\(Filename:|"
    r"StackTraceUtility|DebugLogHandler|Logger:Log|ExtractStackTrace|"
    r"UnitySynchronizationContext|StateMachineBox|AwaitableAsync|"
    r"DoMoveNext \(\)|:MoveNext \(\)"
)
keys = re.compile(
    r"HEADLESS|ECOLOGY|BOOTSTRAP|Environment|SceneActivate|error|Error|"
    r"Exception|TIMEOUT|timeout|h8headless|Ecosystem|Fauna|Debris|"
    r"ColdTick|BATCH|InitializeEnvironment|fault|FAULT|Hang|hang|"
    r"Await|await|phase|Phase|Ready|ready|biomass|Biomass",
    re.I,
)

print("--- last 80 meaningful ---")
n = 0
for i in range(len(lines) - 1, -1, -1):
    l = lines[i]
    if skip.search(l):
        continue
    print(f"{i+1}|{l[:240]}")
    n += 1
    if n >= 80:
        break

print("--- key hits last 50 ---")
hits = [(i + 1, l) for i, l in enumerate(lines) if keys.search(l) and not skip.search(l)]
for i, l in hits[-50:]:
    print(f"{i}|{l[:240]}")

# result/csv/status
root = r"C:\hades\Hecton8\Docs\AgentLogs"
for name in (
    "HeadlessSimulationResult_HEADLESS_SIMULATION_RUNNER.json",
    "HeadlessSimulationDaily_HEADLESS_SIMULATION_RUNNER.csv",
    "HeadlessSimulationBatchRunner_HEADLESS_SIMULATION_RUNNER.txt",
):
    fp = os.path.join(root, name)
    print(f"--- {name} exists={os.path.exists(fp)} size={os.path.getsize(fp) if os.path.exists(fp) else 0}")
    if os.path.exists(fp):
        data = open(fp, encoding="utf-8", errors="replace").read()
        print(data[-1500:] if len(data) > 1500 else data)
