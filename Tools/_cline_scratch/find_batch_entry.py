# scratch - do not commit
import sys, os, re
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
os.chdir(r"C:\hades\Hecton8")

br_path = r"Assets/_Project/Scripts/QA/Headless/Editor/HeadlessSimulationBatchRunner.cs"
ls = open(br_path, encoding="utf-8", errors="replace").read().splitlines()
print("==== BatchRunner public static / MenuItem / Run ====")
for i, l in enumerate(ls):
    if any(k in l for k in ("public static", "MenuItem", "static void Run", "executeMethod",
                             "h8headless", "BatchTimeout", "BATCH_TIMEOUT", "class ")):
        print(f"{i+1}|{l}")

print("==== first 120 lines ====")
for i in range(min(120, len(ls))):
    print(f"{i+1}|{ls[i]}")

print("==== prior smoke log tail / invoke hints ====")
logp = "Docs/AgentLogs/headless_smoke_20260730.log"
if os.path.exists(logp):
    log = open(logp, encoding="utf-8", errors="replace").read().splitlines()
    print("log lines", len(log))
    # first 40 and last 40 + any executeMethod / h8headless
    for i, l in enumerate(log[:50]):
        print(f"H{i+1}|{l[:220]}")
    print("...")
    for i, l in enumerate(log[-40:]):
        print(f"T{i+1}|{l[:220]}")
    print("==== key lines ====")
    for i, l in enumerate(log):
        if any(k in l for k in ("executeMethod", "h8headless", "BATCH", "HeadlessSimulation",
                                 "error CS", "MarkMainMenu", "SceneActivate", "short-circuit",
                                 "ECOLOGY", "status", "Fatal", "Exception")):
            if i < 200 or "CS" in l or "BATCH" in l or "status" in l or "MarkMain" in l:
                print(f"{i+1}|{l[:240]}")

# search for how agents invoke batch
print("==== AGENTS.md / docs invoke ====")
for root, ds, fs in os.walk("."):
    if any(x in root for x in (".git", "Library", "Temp", "obj", "node_modules", "Tools/_cline")):
        continue
    for f in fs:
        if f.endswith((".md", ".py", ".sh", ".ps1", ".bat", ".cmd")):
            p = os.path.join(root, f)
            try:
                t = open(p, encoding="utf-8", errors="replace").read()
            except Exception:
                continue
            if "HeadlessSimulationBatchRunner" in t or "h8headlessDays" in t:
                print("HIT", p)
                for i, line in enumerate(t.splitlines()):
                    if "HeadlessSimulationBatchRunner" in line or "h8headless" in line:
                        print(f"  {i+1}|{line[:200]}")
