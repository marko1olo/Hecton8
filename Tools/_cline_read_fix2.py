#!/usr/bin/env python3
"""Scratch: post-SceneActivate + AwaitableDebtMonitor + ecology ready. DO NOT COMMIT."""
import os
import re

ROOT = r"C:\hades\Hecton8"


def show(rel, start, end):
    p = os.path.join(ROOT, rel)
    lines = open(p, encoding="utf-8", errors="replace").read().splitlines()
    print(f"\n######## {rel}:{start}-{end}")
    for i in range(max(0, start - 1), min(end, len(lines))):
        print(f"{i+1}|{lines[i]}")


def hits(rel, pats, max_per=25):
    p = os.path.join(ROOT, rel)
    lines = open(p, encoding="utf-8", errors="replace").read().splitlines()
    print(f"\n######## hits {rel}")
    for pat in pats:
        found = [(i + 1, l.rstrip()) for i, l in enumerate(lines) if pat in l]
        print(f"=== {pat!r} n={len(found)}")
        for ln, line in found[:max_per]:
            print(f"  {ln}: {line[:200]}")


# RunBootstrapStateMachineAsync around SceneActivate
show(r"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs", 2420, 2520)
# _headlessBootMode = false reset site
show(r"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs", 900, 940)
# TryMarkEcologyReady full
show(r"Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs", 515, 620)
# RegisterRuntimeLanes rest + _started
show(r"Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs", 420, 520)

# AwaitableDebtMonitor
for root, dirs, files in os.walk(os.path.join(ROOT, "Assets")):
    for f in files:
        if f.endswith("AwaitableDebtMonitor.cs"):
            rel = os.path.relpath(os.path.join(root, f), ROOT)
            print("FOUND", rel)
            hits(rel, ["NextFrameAsync", "batch", "Batch", "Task.Yield", "EditorApplication", "isBatchMode"])
            show(rel, 1, 200)

# Batch runner ResolveTimeoutSeconds
show(r"Assets/_Project/Scripts/QA/Headless/Editor/HeadlessSimulationBatchRunner.cs", 300, 380)
show(r"Assets/_Project/Scripts/QA/Headless/Editor/HeadlessSimulationBatchRunner.cs", 530, 640)

# EnsureEcosystemDirectorRegistered
hits(
    r"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs",
    ["EnsureEcosystemDirectorRegistered", "EcosystemDirector"],
)

# log: any GameBootstrapper after 2523, and any FailAndQuit / HEADLESS after waiting
log = open(os.path.join(ROOT, r"Docs\AgentLogs\headless_smoke_20260730.log"), encoding="utf-8", errors="replace").read().splitlines()
print("\n######## GameBootstrapper lines after 2400")
for i, l in enumerate(log):
    if i < 2400:
        continue
    if "GameBootstrapper" in l and "MoveNext" not in l and "StateMachineBox" not in l and "(at " not in l:
        print(f"{i+1}|{l[:220]}")

print("\n######## HEADLESS / Fail / BOOTSTRAP_TIMEOUT / DISPATCHER_TIMEOUT / ecology")
pat = re.compile(r"\[HEADLESS\]|FailAndQuit|BOOTSTRAP_TIMEOUT|DISPATCHER|ecologyReady|ECOLOGY|MarkMainMenu|PublishGameReady|Bootstrap complete|Bootstrap failed|phase failed|SceneActivate", re.I)
for i, l in enumerate(log):
    if pat.search(l) and "MoveNext" not in l and "StateMachineBox" not in l and "(at " not in l and "Filename:" not in l:
        print(f"{i+1}|{l[:220]}")
