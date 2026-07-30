# scratch: scan runner/dispatcher/eco for post-GameReady stall
from pathlib import Path

ROOT = Path(r"C:\hades\Hecton8")

def scan(rel, keys):
    p = ROOT / rel
    lines = p.read_text(encoding="utf-8", errors="replace").splitlines()
    print(f"===== {rel} ({len(lines)} lines) =====")
    for i, l in enumerate(lines, 1):
        if any(k in l for k in keys):
            print(f"{i}:{l}")

scan(
    r"Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs",
    [
        "_startupTime",
        "StartupTimeout",
        "BOOTSTRAP_TIMEOUT",
        "_ecologyReady",
        "_started",
        "BeginStartup",
        "FrostTick",
        "LateFrameTick",
        "TryMarkEcologyReady",
        "void Update",
        "RegisterRuntimeLanes",
        "RequestHeadlessTimeDilation",
        "IsOriginShift",
        "FailAndQuit",
        "DefaultStartup",
        "h8headlessStartupTimeout",
        "TryCompleteDispatcherWait",
    ],
)

scan(
    r"Assets/_Project/Scripts/Core/SystemDispatcher.cs",
    [
        "IsOriginShiftBootstrapLocked",
        "RequestOriginShiftBootstrapLock",
        "ReleaseOriginShiftBootstrapLock",
        "_originShiftBootstrapLockCount",
        "blockGameplayLanes",
        "ShouldSkipLaneDuringBootstrap",
        "TryFlushInitialSceneRebaseBeforeTicks",
        "RunDispatcherLateFrame",
        "if (IsOriginShiftBootstrapLocked",
    ],
)

scan(
    r"Assets/_Project/Scripts/World/EcosystemDirector.cs",
    [
        "IsInitialized",
        "EnsureRuntimeInstance",
        "InitializeService",
        "IServiceHeartbeat",
        "HeartbeatState",
        "IsServiceReady",
        "_sector",
        "CreateBuffers",
        "EnsureBuffers",
    ],
)

# log timestamps around key events
log = ROOT / r"Docs/AgentLogs/headless_smoke_20260730_p0_gameready.log"
if log.exists():
    text = log.read_text(encoding="utf-8", errors="replace").splitlines()
    print(f"===== LOG size={len(text)} =====")
    needles = (
        "runner installed",
        "dispatcher acquired",
        "runtime lanes",
        "PublishGameReady",
        "MarkMainMenu",
        "short-circuit",
        "BOOTSTRAP_TIMEOUT",
        "EcosystemDirector",
        "HectonFloatingOrigin",
        "fail exitCode",
        "OriginShift",
        "bootstrap lock",
    )
    for i, l in enumerate(text, 1):
        low = l.lower()
        if any(n.lower() in low for n in needles):
            print(f"{i}:{l[:240]}")

# extract first/last timestamps if present
print("===== LOG HEAD/TAIL time-ish =====")
if log.exists():
    for i, l in enumerate(text[:30], 1):
        print(f"H{i}:{l[:200]}")
    print("---")
    for i, l in enumerate(text[-40:], len(text) - 39):
        print(f"T{i}:{l[:200]}")
