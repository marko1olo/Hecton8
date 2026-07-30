# -*- coding: utf-8 -*-
"""Launch P0 headless smoke after ecology-wait-clock fix. Scratch only."""
import os
import subprocess
import sys
import time
from pathlib import Path

root = Path(r"C:\hades\Hecton8")
os.chdir(root)

runner = root / "Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs"
boot = root / "Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs"
rt = runner.read_text(encoding="utf-8")
bt = boot.read_text(encoding="utf-8")
checks = {
    "BeginStartup": "BeginStartup" in rt,
    "TryCompleteDispatcherWait": "TryCompleteDispatcherWait" in rt,
    "_awaitingDispatcher": "_awaitingDispatcher" in rt,
    "_ecologyWaitStartRealtime": "_ecologyWaitStartRealtime" in rt,
    "TryArmEcologyWaitClock": "private void TryArmEcologyWaitClock()" in rt,
    "LogEcologyBootstrapTimeoutDiagnostics": "private void LogEcologyBootstrapTimeoutDiagnostics()" in rt,
    "ecology wait clock armed": "ecology wait clock armed (GameReady)" in rt,
    "budget starts at GameReady": "budget starts at GameReady" in rt,
    "TryFlushInitialSceneRebaseBeforeTicks": "TryFlushInitialSceneRebaseBeforeTicks" in rt,
    "PublishGameReady on bootstrap": "MarkMainMenuReached + PublishGameReady on bootstrap" in bt,
    "PublishGameReady(true) short-circuit": "BootstrapState.PublishGameReady(true);" in bt
    and "Headless SceneActivate short-circuit" in bt,
}
print("=== PATCH CHECKS ===")
for k, v in checks.items():
    print(f"  {k}: {v}")
if not all(checks.values()):
    sys.exit("PATCH INCOMPLETE")

r = subprocess.run(["tasklist"], capture_output=True, text=True, errors="replace")
if "Unity.exe" in r.stdout:
    print("Unity.exe RUNNING - abort launch")
    sys.exit(2)
print("Unity.exe: free")

log = root / "Docs/AgentLogs/headless_smoke_20260730_p0_ecology_clock.log"
result = root / "Docs/AgentLogs/HeadlessSimulationResult_HEADLESS_SIMULATION_RUNNER.json"
if result.exists():
    bak = result.with_suffix(".json.bak_pre_ecology_clock")
    try:
        result.replace(bak)
        print("backed up stale result JSON")
    except OSError:
        result.unlink()
        print("deleted stale result JSON")
if log.exists():
    log.unlink()
    print("deleted stale log")

gate = root / "Tools/RunUnityBatchGate.py"
# Batch timeout 900s wall; startupTimeout 600s POST-GameReady (ecology wait clock).
cmd = [
    sys.executable,
    str(gate),
    "--method",
    "Hecton8.QA.Headless.Editor.HeadlessSimulationBatchRunner.Run",
    "--log",
    str(log),
    "--timeout",
    "900",
    "--max-cpu",
    "85",
    "--",
    "-h8headless",
    "-h8headlessDays",
    "5",
    "-h8headlessDaySeconds",
    "60",
    "-h8headlessStartupTimeout",
    "600",
]
print("LAUNCH:", " ".join(cmd))

status = root / "Tools/_cline_scratch/p0_ecology_clock_launch_status.txt"
with status.open("w", encoding="utf-8") as sf:
    sf.write(f"launching at {time.strftime('%Y-%m-%d %H:%M:%S')}\n")
    sf.write(" ".join(cmd) + "\n")

gate_out = root / "Tools/_cline_scratch/p0_ecology_clock_gate_out.txt"
fout = gate_out.open("w", encoding="utf-8")
proc = subprocess.Popen(
    cmd,
    cwd=str(root),
    stdout=fout,
    stderr=subprocess.STDOUT,
    creationflags=subprocess.CREATE_NEW_PROCESS_GROUP if sys.platform == "win32" else 0,
)
(root / "Tools/_cline_scratch/p0_ecology_clock_pid.txt").write_text(str(proc.pid), encoding="utf-8")
print("gate pid", proc.pid)
print("log", log)
print("OK launched")
