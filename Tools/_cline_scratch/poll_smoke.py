# scratch - do not commit
import sys, os, subprocess, time
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
os.chdir(r"C:\hades\Hecton8")

print("=== procs ===")
r = subprocess.run(["tasklist"], capture_output=True, text=True, encoding="utf-8", errors="replace")
for line in r.stdout.splitlines():
    if "Unity" in line or "python" in line.lower():
        print(line)

print("=== bg log ===")
bg = r"C:\Users\Admin\AppData\Local\Temp\cline\background-1785410330346-1f8qt0a.log"
if os.path.exists(bg):
    print(open(bg, encoding="utf-8", errors="replace").read()[-2000:])
else:
    print("no bg log")

log = "Docs/AgentLogs/headless_smoke_20260730_p0fix.log"
print("=== log size ===", os.path.getsize(log) if os.path.exists(log) else "missing")
if os.path.exists(log):
    lines = open(log, encoding="utf-8", errors="replace").read().splitlines()
    print("lines", len(lines))
    print("=== tail 60 ===")
    for l in lines[-60:]:
        print(l[:240])
    print("=== keys ===")
    for i, l in enumerate(lines):
        if any(k in l for k in (
            "error CS", "MarkMainMenu", "SceneActivate", "short-circuit", "headless=",
            "BATCH", "ECOLOGY", "status", "Fatal", "Exception", "LoadGameplay",
            "ignoring stale", "Headless SceneActivate", "error :", "Scripts have compiler"
        )):
            print(f"{i+1}|{l[:240]}")

for p in [
    "Docs/AgentLogs/HeadlessSimulationResult_HEADLESS_SIMULATION_RUNNER.json",
    "Docs/AgentLogs/HeadlessSimulationBatchRunner_HEADLESS_SIMULATION_RUNNER.txt",
    "Docs/AgentLogs/HeadlessSimulationDaily_HEADLESS_SIMULATION_RUNNER.csv",
]:
    print("===", p, "exists", os.path.exists(p), "===")
    if os.path.exists(p):
        print(open(p, encoding="utf-8", errors="replace").read()[:1500])
