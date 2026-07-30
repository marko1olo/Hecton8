# scratch - do not commit
import sys, os, subprocess
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
os.chdir(r"C:\hades\Hecton8")

ls = open("Tools/RunUnityBatchGate.py", encoding="utf-8", errors="replace").read().splitlines()
print("lines", len(ls))
keys = ("argparse", "add_argument", "h8headless", "headless", "days", "dayseconds",
        "if __name__", "def main", "cpu", "preflight", "BUILD_GATE", "exit")
for i, l in enumerate(ls):
    low = l.lower()
    if any(k in low for k in keys):
        print(f"{i+1}|{l[:220]}")

print("==== help ====")
try:
    r = subprocess.run(
        [sys.executable, "Tools/RunUnityBatchGate.py", "--help"],
        capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=30
    )
    print(r.stdout[:3000])
    print(r.stderr[:1000])
except Exception as e:
    print("help fail", e)

print("==== CPU ====")
try:
    r = subprocess.run(
        ["powershell", "-NoProfile", "-Command",
         "(Get-Counter '\\Processor(_Total)\\% Processor Time').CounterSamples.CookedValue"],
        capture_output=True, text=True, timeout=15
    )
    print(r.stdout.strip())
except Exception as e:
    print("cpu fail", e)

# prior result JSON
for p in [
    "Docs/AgentLogs/HeadlessSimulationResult_HEADLESS_SIMULATION_RUNNER.json",
    "Docs/AgentLogs/headless_smoke_20260730.log",
]:
    print("exists", p, os.path.exists(p))
    if os.path.exists(p) and p.endswith(".json"):
        print(open(p, encoding="utf-8", errors="replace").read()[:800])
