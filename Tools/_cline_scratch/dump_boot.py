# scratch - do not commit
import sys, os, glob
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
os.chdir(r"C:\hades\Hecton8")

lines = open(
    r"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs",
    encoding="utf-8",
    errors="replace",
).read().splitlines()

print("==== SceneActivate 3130-3250 ====")
for i in range(3129, min(3250, len(lines))):
    print(f"{i+1}|{lines[i]}")

print("==== headless skip 2420-2490 ====")
for i in range(2419, min(2490, len(lines))):
    print(f"{i+1}|{lines[i]}")

print("==== IsHeadlessBootRequested ====")
for i, l in enumerate(lines):
    if "IsHeadlessBootRequested" in l or "MarkMainMenuReached" in l:
        lo = max(0, i - 2)
        hi = min(len(lines), i + 25)
        for j in range(lo, hi):
            print(f"{j+1}|{lines[j]}")
        print("---")

print("==== ADM NextFrameAsync ====")
for root, ds, fs in os.walk("Assets"):
    for f in fs:
        if f == "AwaitableDebtMonitor.cs":
            p = os.path.join(root, f)
            print("PATH", p)
            ls = open(p, encoding="utf-8", errors="replace").read().splitlines()
            for i, l in enumerate(ls):
                if "NextFrameAsync" in l and ("static" in l or "async" in l or "Task" in l or "Awaitable" in l):
                    for j in range(max(0, i - 2), min(i + 60, len(ls))):
                        print(f"{j+1}|{ls[j]}")
                    print("---")

print("==== playprobe ====")
ps = sorted(
    glob.glob("Logs/**/h8_playprobe*.json", recursive=True),
    key=os.path.getmtime,
)
for p in ps[-5:]:
    print("FILE", p, os.path.getmtime(p))
    print(open(p, encoding="utf-8", errors="replace").read()[:2500])
    print("---")

print("==== screenshots ====")
caps = sorted(
    glob.glob("Logs/RouteCaptures/**/*.*", recursive=True),
    key=os.path.getmtime,
)
for p in caps[-15:]:
    print(p, os.path.getmtime(p), os.path.getsize(p))

print("==== BACKLOG P0/OPEN ====")
bl = open("BACKLOG.md", encoding="utf-8", errors="replace").read().splitlines()
for i, l in enumerate(bl[:120]):
    print(f"{i+1}|{l}")
print("---- filtered ----")
for i, l in enumerate(bl):
    low = l.lower()
    if any(
        k in low
        for k in (
            "headless",
            "ecology",
            "sceneactivate",
            "boot fail",
            "playprobe",
            "kinematic",
            "[ ]",
            "p0",
        )
    ):
        if "[x]" in low and "p0" not in low and "headless" not in low:
            continue
        print(f"{i+1}|{l}")

print("==== prior critical dump exists? ====")
for p in [
    "Docs/AgentLogs/_cline_fix2_out.txt",
    "Docs/AgentLogs/_cline_critical_out.txt",
    "Docs/AgentLogs/headless_smoke_20260730.log",
    "Docs/AgentLogs/HeadlessSimulationResult_HEADLESS_SIMULATION_RUNNER.json",
]:
    print(p, "YES" if os.path.isfile(p) else "NO", os.path.getsize(p) if os.path.isfile(p) else 0)
