# -*- coding: utf-8 -*-
import json
import os
import subprocess
import sys
import time
from pathlib import Path

root = Path(r"C:\hades\Hecton8")
os.chdir(root)

runner = root / "Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs"
t = runner.read_text(encoding="utf-8")
checks = {
    "BeginStartup": "BeginStartup" in t,
    "TryCompleteDispatcherWait": "TryCompleteDispatcherWait" in t,
    "_awaitingDispatcher": "_awaitingDispatcher" in t,
    "dispatcher acquired": "dispatcher acquired" in t,
    "private void Update()": "private void Update()" in t,
    "RunStartupAsync GONE": "RunStartupAsync" not in t,
    "WaitForDispatcherAndStart GONE": "WaitForDispatcherAndStart" not in t,
}
print("=== PATCH CHECKS ===")
for k, v in checks.items():
    print(f"  {k}: {v}")
if not all(checks.values()):
    sys.exit("PATCH INCOMPLETE")

# Unity free?
r = subprocess.run(["tasklist"], capture_output=True, text=True, errors="replace")
if "Unity.exe" in r.stdout:
    print("Unity.exe RUNNING - abort launch")
    sys.exit(2)
print("Unity.exe: free")

log = root / "Docs/AgentLogs/headless_smoke_20260730_p0_dispfix.log"
result = root / "Docs/AgentLogs/HeadlessSimulationResult_HEADLESS_SIMULATION_RUNNER.json"
if result.exists():
    result.unlink()
    print("deleted stale result JSON")

# Launch via RunUnityBatchGate
gate = root / "Tools/RunUnityBatchGate.py"
cmd = [
    sys.executable,
    str(gate),
    "--method",
    "Hecton8.QA.Headless.Editor.HeadlessSimulationBatchRunner.Run",
    "--log",
    str(log),
    "--timeout",
    "600",
    "--max-cpu",
    "50",
    "--",
    "-h8headless",
    "-h8headlessDays",
    "5",
    "-h8headlessDaySeconds",
    "60",
]
print("LAUNCH:", " ".join(cmd))
# Start detached so this script can return; parent will poll
creationflags = 0
if sys.platform == "win32":
    creationflags = subprocess.CREATE_NEW_PROCESS_GROUP | subprocess.DETACHED_PROCESS

# Actually run in background with stdout to status file
status = root / "Tools/_cline_scratch/p0_dispfix_launch_status.txt"
with status.open("w", encoding="utf-8") as sf:
    sf.write(f"launching at {time.strftime('%Y-%m-%d %H:%M:%S')}\n")
    sf.write(" ".join(cmd) + "\n")

# Non-detached: user agent will poll; use Popen with log of gate stdout
gate_out = root / "Tools/_cline_scratch/p0_dispfix_gate_out.txt"
fout = gate_out.open("w", encoding="utf-8")
proc = subprocess.Popen(
    cmd,
    cwd=str(root),
    stdout=fout,
    stderr=subprocess.STDOUT,
    creationflags=subprocess.CREATE_NEW_PROCESS_GROUP if sys.platform == "win32" else 0,
)
(root / "Tools/_cline_scratch/p0_dispfix_pid.txt").write_text(str(proc.pid), encoding="utf-8")
print("gate pid", proc.pid)
print("log", log)
print("OK launched")
