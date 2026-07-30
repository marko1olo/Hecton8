# scratch - do not commit
import sys, os
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
os.chdir(r"C:\hades\Hecton8")

# AwaitableDebtMonitor in InputDispatcher.cs
p = r"Assets/_Project/Scripts/Core/InputDispatcher.cs"
ls = open(p, encoding="utf-8", errors="replace").read().splitlines()
print("==== InputDispatcher size", len(ls), "====")
for i, l in enumerate(ls):
    if "class AwaitableDebtMonitor" in l or "NextFrameAsync" in l or "batchmode" in l.lower() or "BatchMode" in l or "Task.Yield" in l or "isBatchMode" in l or "EditorApplication" in l:
        print(f"{i+1}|{l}")

print("==== NextFrameAsync full method windows ====")
for i, l in enumerate(ls):
    if "NextFrameAsync" in l and ("static" in l or "async" in l):
        for j in range(max(0, i - 5), min(len(ls), i + 80)):
            print(f"{j+1}|{ls[j]}")
        print("---")

# GlobalRegistry.Dispatcher setter
print("==== GlobalRegistry Dispatcher ====")
for root, ds, fs in os.walk("Assets"):
    for f in fs:
        if f == "GlobalRegistry.cs":
            gp = os.path.join(root, f)
            print("PATH", gp)
            gl = open(gp, encoding="utf-8", errors="replace").read().splitlines()
            for i, l in enumerate(gl):
                if "Dispatcher" in l and (
                    "static" in l or "get" in l or "set" in l or "Register" in l or "=" in l
                ):
                    if i < 400 or "Register" in l or "IInputDispatcher" in l or "Dispatcher {" in l or "Dispatcher =" in l:
                        print(f"{i+1}|{l}")
            # print property body
            for i, l in enumerate(gl):
                if "static" in l and "Dispatcher" in l and ("get" in l or "{" in l or "I" in l):
                    for j in range(i, min(i + 30, len(gl))):
                        print(f"P {j+1}|{gl[j]}")
                    break

print("==== GameStartContextHolder ====")
for root, ds, fs in os.walk("Assets"):
    for f in fs:
        if "GameStartContext" in f and f.endswith(".cs"):
            gp = os.path.join(root, f)
            print("PATH", gp)
            gl = open(gp, encoding="utf-8", errors="replace").read().splitlines()
            for i, l in enumerate(gl[:200]):
                print(f"{i+1}|{l}")

print("==== LoadMainMenuAsync full ====")
gb = open(
    r"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs",
    encoding="utf-8",
    errors="replace",
).read().splitlines()
for i in range(3274, min(3340, len(gb))):
    print(f"{i+1}|{gb[i]}")

print("==== smoke: last bootstrap lines before SceneActivate ====")
log = open(
    "Docs/AgentLogs/headless_smoke_20260730.log", encoding="utf-8", errors="replace"
).read().splitlines()
# find SceneActivate line index
idxs = [i for i, l in enumerate(log) if "InitializeSceneActivatePhaseAsync" in l]
print("SceneActivate log idxs", idxs)
if idxs:
    i = idxs[0]
    for j in range(max(0, i - 40), min(len(log), i + 15)):
        print(f"{j}|{log[j][:240]}")

print("==== Does log contain headlessBoot / MarkMain / LoadMain / Dispatcher register ====")
for key in (
    "MarkMainMenu",
    "headless",
    "LoadMainMenu",
    "LoadGameplay",
    "CoreReady",
    "LockReady",
    "RegisterDispatcher",
    "Dispatcher registered",
    "SystemDispatcher",
    "BOOTSTRAP_TIMEOUT",
    "BATCH_TIMEOUT",
    "ecology",
):
    hits = [l for l in log if key.lower() in l.lower()]
    print(f"KEY {key!r} count={len(hits)}")
    for h in hits[:5]:
        print(" ", h[:220])

print("==== BatchRunner watchdog / requires headless ====")
br = open(
    r"Assets/_Project/Scripts/QA/Headless/Editor/HeadlessSimulationBatchRunner.cs",
    encoding="utf-8",
    errors="replace",
).read().splitlines()
for i, l in enumerate(br):
    if any(
        k in l
        for k in (
            "BATCH_TIMEOUT",
            "h8headless",
            "EnterPlaymode",
            "commandLine",
            "Watchdog",
            "PlayMode",
            "WriteFallback",
            "args",
        )
    ):
        print(f"{i+1}|{l}")
