# -*- coding: utf-8 -*-
"""Kill prior headless Unity, clear result, relaunch ecology smoke on current HEAD."""
import os
import subprocess
import sys
import time
from datetime import datetime
from pathlib import Path

REPO = Path(r"C:\hades\Hecton8")
os.chdir(REPO)

UNITY = Path(r"C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe")
RESULT = REPO / "Docs" / "AgentLogs" / "HeadlessSimulationResult_HEADLESS_SIMULATION_RUNNER.json"
LOG_DIR = REPO / "Docs" / "AgentLogs"
LOG_DIR.mkdir(parents=True, exist_ok=True)

stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
log_path = LOG_DIR / f"headless_smoke_20260731_p0_ecology_ready_{stamp}.log"
meta_path = REPO / "_agent_relaunch_meta.txt"

# HEAD
head = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=REPO, text=True).strip()
print("HEAD", head)

# Kill Unity processes that hold this project (batchmode headless)
killed = []
try:
    out = subprocess.check_output(
        ["powershell", "-NoProfile", "-Command",
         "Get-CimInstance Win32_Process -Filter \"name='Unity.exe'\" | "
         "Select-Object ProcessId,CommandLine | ConvertTo-Json -Compress"],
        text=True,
        errors="replace",
    )
except subprocess.CalledProcessError as e:
    out = e.output or ""
    print("ps list err", e)

import json

procs = []
if out.strip():
    try:
        data = json.loads(out)
        if isinstance(data, dict):
            procs = [data]
        elif isinstance(data, list):
            procs = data
    except json.JSONDecodeError:
        print("json fail", out[:500])

for p in procs:
    cmd = p.get("CommandLine") or ""
    pid = p.get("ProcessId")
    if "Hecton8" in cmd or "-h8headless" in cmd or "HeadlessSimulation" in cmd:
        print(f"kill pid={pid}")
        subprocess.run(["taskkill", "/F", "/T", "/PID", str(pid)], check=False)
        killed.append(pid)

time.sleep(3)

# Clear stale result so poll does not read prior FAIL
if RESULT.exists():
    RESULT.unlink()
    print("removed stale result")

# Also clear CSV if present
csv = REPO / "Docs" / "AgentLogs" / "HeadlessSimulationDaily_HEADLESS_SIMULATION_RUNNER.csv"
if csv.exists():
    csv.unlink()
    print("removed stale csv")

args = [
    str(UNITY),
    "-batchmode",
    "-nographics",
    "-projectPath",
    str(REPO),
    "-logFile",
    str(log_path),
    "-executeMethod",
    "Hecton8.QA.Headless.Editor.HeadlessSimulationBatchRunner.Run",
    "-h8headless",
    "-h8headlessDays",
    "5",
    "-h8headlessDaySeconds",
    "60",
    "-h8headlessStartupTimeout",
    "600",
]

print("log", log_path)
print("launching...")
# DETACHED_PROCESS | CREATE_NEW_PROCESS_GROUP on Windows
CREATE_NEW_PROCESS_GROUP = 0x00000200
DETACHED_PROCESS = 0x00000008
CREATE_NO_WINDOW = 0x08000000
proc = subprocess.Popen(
    args,
    cwd=str(REPO),
    stdout=subprocess.DEVNULL,
    stderr=subprocess.DEVNULL,
    creationflags=DETACHED_PROCESS | CREATE_NEW_PROCESS_GROUP | CREATE_NO_WINDOW,
)
print("pid", proc.pid)

meta_path.write_text(
    f"head={head}\npid={proc.pid}\nlog={log_path}\nkilled={killed}\nstarted={stamp}\n",
    encoding="utf-8",
)
print("meta", meta_path)
print("OK relaunch")
