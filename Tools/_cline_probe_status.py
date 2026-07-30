# -*- coding: utf-8 -*-
import os, sys, glob, time
os.chdir(r"C:\hades\Hecton8")

print("=====SWEEP_TAIL=====")
p = r"Tools\_cline_geo_sweep_run.txt"
if os.path.isfile(p):
    with open(p, "r", encoding="utf-8", errors="replace") as f:
        lines = f.read().splitlines()
    for ln in lines[-35:]:
        print(ln)
else:
    print("no run yet")

print("=====OUT=====")
p2 = r"Tools\_cline_geo_sweep_out.txt"
if os.path.isfile(p2):
    with open(p2, "r", encoding="utf-8", errors="replace") as f:
        print(f.read())
else:
    print("no out yet")

print("=====CPU_UNITY=====")
try:
    import subprocess
    r = subprocess.run(
        ["powershell", "-NoProfile", "-Command",
         "(Get-CimInstance Win32_Processor | Measure-Object -Property LoadPercentage -Average).Average"],
        capture_output=True, text=True, timeout=15)
    cpu = (r.stdout or "").strip()
    print("CPU=" + cpu)
except Exception as e:
    print("CPU_ERR", e)

try:
    r2 = subprocess.run(["tasklist", "/FI", "IMAGENAME eq Unity.exe", "/NH"],
                        capture_output=True, text=True, timeout=10)
    out = (r2.stdout or "").strip()
    if "Unity.exe" in out:
        print("UNITY_ALIVE")
        print(out)
    else:
        print("UNITY_DEAD")
except Exception as e:
    print("UNITY_ERR", e)

print("=====RESULT=====")
rj = r"Docs\AgentLogs\HeadlessSimulationResult_HEADLESS_SIMULATION_RUNNER.json"
print("RESULT_EXISTS" if os.path.isfile(rj) else "RESULT_ABSENT")

print("=====PY=====")
try:
    r3 = subprocess.run(["tasklist", "/FI", "IMAGENAME eq python.exe", "/NH"],
                        capture_output=True, text=True, timeout=10)
    print((r3.stdout or "").strip() or "(none)")
except Exception as e:
    print("PY_ERR", e)

print("=====LOCK=====")
print("LOCK_OK" if os.path.isdir(r".agent-locks\ACTIVE\cline-orchestrator") else "LOCK_MISSING")
