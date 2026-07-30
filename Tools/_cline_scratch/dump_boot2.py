# scratch - do not commit
import sys, os, re
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
os.chdir(r"C:\hades\Hecton8")

gb = open(
    r"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs",
    encoding="utf-8",
    errors="replace",
).read().splitlines()

needles = [
    "TryResolveBootstrapGameplayHandoffScene",
    "RunBootstrapPhaseAsync",
    "IsBootstrapScene",
    "ResolveBootstrapGameplaySceneName",
    "HectonHeadlessCommandLineArg",
    "HeadlessCommandLineArg",
    "_headlessBootMode",
    "LoadGameplaySceneFromBootstrapHandoffAsync",
    "SetSceneActivationStep",
    "WaitForScene",
    "NextFrameAsync",
]

print("==== GameBootstrapper symbol hits (bodies) ====")
for needle in needles:
    for i, l in enumerate(gb):
        if needle in l and (
            "private" in l or "public" in l or "static" in l or "const" in l or "bool " in l
        ):
            # print method-ish window
            print(f"--- {needle} @ {i+1} ---")
            for j in range(i, min(i + 80, len(gb))):
                print(f"{j+1}|{gb[j]}")
                # stop at next method at same indent roughly after body start
                if j > i + 5 and gb[j].startswith("        private ") and j > i + 10:
                    break
                if j > i + 5 and gb[j].startswith("        public ") and j > i + 10:
                    break
            break

print("==== Find AwaitableDebtMonitor ====")
for root, ds, fs in os.walk("Assets"):
    for f in fs:
        if "Awaitable" in f or "DebtMonitor" in f:
            print(os.path.join(root, f))

print("==== smoke log last 80 non-empty ====")
logp = "Docs/AgentLogs/headless_smoke_20260730.log"
if os.path.isfile(logp):
    lines = open(logp, encoding="utf-8", errors="replace").read().splitlines()
    nonempty = [l for l in lines if l.strip()]
    for l in nonempty[-80:]:
        print(l[:300])
    print("---- key greps ----")
    keys = (
        "SceneActivate",
        "HEADLESS",
        "dispatcher",
        "MainMenu",
        "handoff",
        "ECOLOGY",
        "Fatal",
        "Exception",
        "Bootstrapper-DEBUG",
        "waiting",
        "ColdTick",
        "MarkMain",
        "LoadGameplay",
        "LoadMain",
    )
    for l in nonempty:
        if any(k.lower() in l.lower() for k in keys):
            print(l[:320])

print("==== HeadlessSimulationRunner wait/cold/ecology ====")
hr = open(
    r"Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs",
    encoding="utf-8",
    errors="replace",
).read().splitlines()
for i, l in enumerate(hr):
    if any(
        k in l
        for k in (
            "WaitForDispatcherAndStart",
            "RegisterRuntimeLanes",
            "TryMarkEcologyReady",
            "ColdTick",
            "BOOTSTRAP_TIMEOUT",
            "Dispatcher",
            "InitializeColdState",
        )
    ):
        if "void " in l or "async " in l or "bool " in l or "static " in l or "private " in l or "IEnumerator" in l:
            print(f"--- @{i+1}: {l.strip()} ---")
            for j in range(i, min(i + 55, len(hr))):
                print(f"{j+1}|{hr[j]}")
            print()

print("==== playprobe failures detail ====")
import json, glob
p = "Logs/h8_playprobe_route.json"
if os.path.isfile(p):
    data = json.load(open(p, encoding="utf-8"))
    print("exitCode", data.get("exitCode"), "failures", data.get("failures"))
    # print rest of file for failures array
    raw = open(p, encoding="utf-8").read()
    print(raw[2000:5000])

print("==== capture_truth sample ====")
ct = r"Logs/RouteCaptures/playmode_20260729_091147_3/capture_truth.txt"
if os.path.isfile(ct):
    print(open(ct, encoding="utf-8", errors="replace").read())
