# scratch - do not commit
import sys
import os

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
os.chdir(r"C:\hades\Hecton8")

br_path = r"Assets/_Project/Scripts/QA/Headless/Editor/HeadlessSimulationBatchRunner.cs"
ls = open(br_path, encoding="utf-8", errors="replace").read().splitlines()
print("lines", len(ls))
print("==== markers ====")
keys = (
    "public static",
    "MenuItem",
    "static void",
    "executeMethod",
    "h8headless",
    "BATCH_TIMEOUT",
    "class Headless",
    "namespace ",
    "RunBatch",
    "Days",
)
for i, l in enumerate(ls):
    if any(k in l for k in keys):
        print(f"{i+1}|{l[:220]}")

print("==== top 130 ====")
for i in range(min(130, len(ls))):
    print(f"{i+1}|{ls[i][:220]}")

print("==== lines 200-320 ====")
for i in range(200, min(320, len(ls))):
    print(f"{i+1}|{ls[i][:220]}")

# prior smoke log keys only
logp = "Docs/AgentLogs/headless_smoke_20260730.log"
if os.path.exists(logp):
    log = open(logp, encoding="utf-8", errors="replace").read().splitlines()
    print("smoke log lines", len(log))
    print("==== smoke head 30 ====")
    for i, l in enumerate(log[:30]):
        print(f"{i+1}|{l[:220]}")
    print("==== smoke key ====")
    for i, l in enumerate(log):
        if any(
            k in l
            for k in (
                "executeMethod",
                "-h8",
                "BATCH",
                "error CS",
                "MarkMainMenu",
                "SceneActivate",
                "short-circuit",
                "ECOLOGY",
                "HeadlessSimulationBatch",
                "Command line",
                "Batchmode",
            )
        ):
            print(f"{i+1}|{l[:240]}")

# docs hits limited paths
for p in [
    "AGENTS.md",
    "README.md",
    "BUILD_PLAYTEST_ISSUES.md",
    "Docs/PLAYTEST",
]:
    if os.path.isfile(p):
        t = open(p, encoding="utf-8", errors="replace").read().splitlines()
        hits = [
            (i + 1, l)
            for i, l in enumerate(t)
            if "HeadlessSimulationBatchRunner" in l or "h8headless" in l
        ]
        if hits:
            print("HIT FILE", p)
            for i, l in hits[:40]:
                print(f"  {i}|{l[:200]}")
    elif os.path.isdir(p):
        for root, ds, fs in os.walk(p):
            for f in fs:
                fp = os.path.join(root, f)
                try:
                    t = open(fp, encoding="utf-8", errors="replace").read()
                except Exception:
                    continue
                if "HeadlessSimulationBatchRunner" in t or "h8headlessDays" in t:
                    print("HIT", fp)
                    for i, l in enumerate(t.splitlines()):
                        if "HeadlessSimulationBatchRunner" in l or "h8headless" in l:
                            print(f"  {i+1}|{l[:200]}")
