# scratch - do not commit
import subprocess
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
root = Path(r"C:\hades\Hecton8")
bg = Path(r"C:\Users\Admin\AppData\Local\Temp\cline\background-1785405046809-rk7qu2l.log")
log = root / "Docs/AgentLogs/headless_smoke_20260730.log"
result = root / "Docs/AgentLogs/HeadlessSimulationResult_HEADLESS_SIMULATION_RUNNER.json"
status = root / "Docs/AgentLogs/HeadlessSimulationBatchRunner_HEADLESS_SIMULATION_RUNNER.txt"
csv = root / "Docs/AgentLogs/HeadlessSimulationDaily_HEADLESS_SIMULATION_RUNNER.csv"

print("bg exists", bg.exists(), "size", bg.stat().st_size if bg.exists() else 0)
if bg.exists() and bg.stat().st_size:
    print("--- bg tail ---")
    print("\n".join(bg.read_text(encoding="utf-8", errors="replace").splitlines()[-40:]))

print("log exists", log.exists(), "size", log.stat().st_size if log.exists() else 0)
if log.exists() and log.stat().st_size:
    lines = log.read_text(encoding="utf-8", errors="replace").splitlines()
    print("log lines", len(lines))
    keys = ("error CS", "Scripts have compiler", "HEADLESS", "ECOLOGY", "BATCH", "waiting for", "GameBootstrapper", "REFUSED", "exception", "Fail")
    hits = [(i + 1, l[:220]) for i, l in enumerate(lines) if any(k.lower() in l.lower() for k in keys)]
    print("interesting hits", len(hits))
    for i, l in hits[-30:]:
        print(f"{i}:{l}")
    print("--- log tail ---")
    for l in lines[-20:]:
        print(l[:220])

print("result exists", result.exists())
if result.exists():
    print(result.read_text(encoding="utf-8", errors="replace")[:2000])
print("status exists", status.exists())
if status.exists():
    print(status.read_text(encoding="utf-8", errors="replace")[-500:])
print("csv exists", csv.exists(), "size", csv.stat().st_size if csv.exists() else 0)
if csv.exists():
    print(csv.read_text(encoding="utf-8", errors="replace")[:1000])

proc = subprocess.run(["cmd", "/c", "tasklist"], capture_output=True, text=True, encoding="utf-8", errors="replace")
for line in proc.stdout.splitlines():
    if "Unity" in line or "python" in line.lower():
        print("PROC", line)
