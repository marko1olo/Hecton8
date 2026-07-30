# scratch - do not commit
import sys, os, subprocess
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
os.chdir(r"C:\hades\Hecton8")

r = subprocess.run(["tasklist"], capture_output=True, text=True, encoding="utf-8", errors="replace")
unity = [l for l in r.stdout.splitlines() if "Unity.exe" in l]
print("unity lines", len(unity))
for l in unity:
    print(l)

log = "Docs/AgentLogs/headless_smoke_20260730_p0fix.log"
print("log size", os.path.getsize(log) if os.path.exists(log) else None)
if os.path.exists(log):
    lines = open(log, encoding="utf-8", errors="replace").read().splitlines()
    print("log lines", len(lines))
    # last non-stack meaningful
    for l in lines[-30:]:
        if l.startswith("[") or "HEADLESS" in l or "error" in l.lower() or "Exception" in l or "Cement" in l or "Trim" in l:
            print("T|", l[:200])

for p in [
    "Docs/AgentLogs/HeadlessSimulationResult_HEADLESS_SIMULATION_RUNNER.json",
    "Docs/AgentLogs/HeadlessSimulationBatchRunner_HEADLESS_SIMULATION_RUNNER.txt",
    "Docs/AgentLogs/HeadlessSimulationDaily_HEADLESS_SIMULATION_RUNNER.csv",
]:
    print("---", p)
    if not os.path.exists(p):
        print("missing")
        continue
    t = open(p, encoding="utf-8", errors="replace").read()
    print(t[-800:] if len(t) > 800 else t)

# TryResolveSeedObserverAup comment prove in tree
ed = open(r"Assets/_Project/Scripts/World/EcosystemDirector.cs", encoding="utf-8", errors="replace").read().splitlines()
print("==== TryResolveSeedObserverAup body ====")
for i, l in enumerate(ed):
    if "bool TryResolveSeedObserverAup" in l:
        for j in range(i, min(i + 25, len(ed))):
            print(f"{j+1}|{ed[j][:200]}")
        break

print("==== git blame-ish: when fallback landed? show surrounding commit msg via log -S ====")
r = subprocess.run(
    ["git", "log", "-3", "--oneline", "-S", "TryResolveSeedObserverAup", "--", "Assets/_Project/Scripts/World/EcosystemDirector.cs"],
    capture_output=True, text=True, encoding="utf-8", errors="replace"
)
print(r.stdout)
print(r.stderr)
