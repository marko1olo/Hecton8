#!/usr/bin/env python3
"""Scratch: locate bootstrap/headless hang surfaces. DO NOT COMMIT."""
import os
import re

ROOT = r"C:\hades\Hecton8"


def show_hits(path, pats, max_per=20):
    full = os.path.join(ROOT, path)
    text = open(full, encoding="utf-8", errors="replace").read().splitlines()
    print(f"\n######## {path} lines={len(text)}")
    for pat in pats:
        hits = [(i + 1, l.rstrip()) for i, l in enumerate(text) if pat in l]
        print(f"=== {pat!r} count={len(hits)}")
        for ln, line in hits[:max_per]:
            print(f"  {ln}: {line[:200]}")


def show_range(path, start, end):
    full = os.path.join(ROOT, path)
    text = open(full, encoding="utf-8", errors="replace").read().splitlines()
    print(f"\n######## {path}:{start}-{end}")
    for i in range(start - 1, min(end, len(text))):
        print(f"{i+1}|{text[i]}")


# Bootstrap key areas
show_hits(
    r"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs",
    [
        "InitializeSceneActivatePhaseAsync",
        "Waiting for heartbeat",
        "EnsureFaunaSimulationRegistered",
        "ReportDebrisManagerBootstrapNodeState",
        "InitializeBootstrapLayerNodesAsync",
        "FaunaSimulation",
        "EXEMPT",
        "HeartbeatTimeout",
        "WaitForHeartbeat",
        "await Awaitable",
        "NextFrameAsync",
    ],
)

show_hits(
    r"Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs",
    [
        "TryMarkEcologyReady",
        "BOOTSTRAP_TIMEOUT",
        "ColdTick",
        "ecologySampled",
        "ECOLOGY_UNAVAILABLE",
        "IsInitialized",
        "EcosystemDirector",
        "RegisterRuntimeLanes",
        "InitializeColdState",
        "timeDilationDelivered",
        "MaxConsecutiveEcology",
        "Await",
    ],
)

show_hits(
    r"Assets/_Project/Scripts/QA/Headless/Editor/HeadlessSimulationBatchRunner.cs",
    [
        "BATCH_TIMEOUT",
        "WriteFallbackResult",
        "timeout",
        "h8headless",
        "watchdog",
        "PlayMode",
        "EditorApplication",
    ],
)
