# scratch - do not commit
import sys, os, subprocess
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
os.chdir(r"C:\hades\Hecton8")

print("=== git log -3 ===")
r = subprocess.run(["git", "log", "-3", "--oneline"], capture_output=True, text=True)
print(r.stdout)
print("=== git show cementer stat ===")
r = subprocess.run(["git", "show", "--stat", "--oneline", "-1", "75c883fd4"], capture_output=True, text=True)
print(r.stdout[:2000])
print(r.stderr[:500])

print("=== unity procs detail ===")
r = subprocess.run(["wmic", "process", "where", "name='Unity.exe'", "get", "ProcessId,CommandLine,CreationDate", "/FORMAT:LIST"],
                   capture_output=True, text=True, encoding="utf-8", errors="replace")
print(r.stdout[:3000])

log = open("Docs/AgentLogs/headless_smoke_20260730_p0fix.log", encoding="utf-8", errors="replace").read().splitlines()
print("log lines", len(log), "size", os.path.getsize("Docs/AgentLogs/headless_smoke_20260730_p0fix.log"))

keys = (
    "HEADLESS", "HeadlessSimulation", "Ecology", "ecology", "Fauna", "TryMark",
    "BootstrapPhase", "Player", "PhaseStarted", "PhaseCompleted", "MarkMainMenu",
    "short-circuit", "TimeDilation", "DailyAudit", "Sampled", "Finish(",
    "FailAndQuit", "ECOLOGY", "BATCH", "started", "BootstrapStatus",
    "RegisterRuntime", "ColdTick", "startup", "ForceHeadless",
    "error CS", "Scripts have compiler", "All compiler errors",
)
print("=== key hits ===")
for i, l in enumerate(log):
    if any(k in l for k in keys):
        # skip stack noise
        if l.startswith("UnityEngine.") or l.startswith("System.") or l.startswith("Hecton8.") and ":" in l and " (" in l:
            continue
        if "(Filename:" in l:
            continue
        print(f"{i+1}|{l[:240]}")

# HeadlessSimulationRunner gates
hr = open(r"Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs", encoding="utf-8", errors="replace").read().splitlines()
print("==== runner ShouldRun / ecology / Mark / Player ====")
for i, l in enumerate(hr):
    if any(k in l for k in (
        "ShouldRun", "TryMarkEcology", "ecologySampled", "MarkMainMenu",
        "BootstrapStatus", "Player", "TimeDilation", "IsMainMenuReached",
        "ECOLOGY_UNAVAILABLE", "WaitForBootstrap", "Start(", "Awake", "OnEnable",
        "RegisterRuntime", "ExecuteDaily"
    )):
        print(f"{i+1}|{l[:220]}")

# BUILD_PLAYTEST excerpt around player skip
bp = open("BUILD_PLAYTEST_ISSUES.md", encoding="utf-8", errors="replace").read().splitlines()
print("==== BUILD_PLAYTEST headless section ====")
for i, l in enumerate(bp):
    if "h8headless" in l or "Player" in l and "ecology" in l.lower() or "structurally" in l:
        for j in range(max(0, i - 2), min(len(bp), i + 8)):
            print(f"{j+1}|{bp[j][:200]}")
        print("---")
