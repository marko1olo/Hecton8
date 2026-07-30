# scratch - do not commit
import sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
import os
os.chdir(r"C:\hades\Hecton8")

gb = open(
    r"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs",
    encoding="utf-8",
    errors="replace",
).read().splitlines()
print("==== around L900-950 headless clear ====")
for i in range(900, 960):
    print(f"{i+1}|{gb[i]}")

print("==== around L2670-2700 headless ====")
for i in range(2660, 2720):
    print(f"{i+1}|{gb[i]}")

print("==== around L3980-4020 ====")
for i in range(3980, 4030):
    print(f"{i+1}|{gb[i]}")

print("==== IsBootstrapDependencyNodeReady SystemDispatcher ====")
for i, l in enumerate(gb):
    if "IsBootstrapDependencyNodeReady" in l and "private" in l:
        for j in range(i, min(i + 100, len(gb))):
            print(f"{j+1}|{gb[j]}")
        break

print("==== WaitForBootstrapDependencyHeartbeatAsync ====")
for i, l in enumerate(gb):
    if "WaitForBootstrapDependencyHeartbeatAsync" in l and ("async" in l or "private" in l):
        for j in range(i, min(i + 50, len(gb))):
            print(f"{j+1}|{gb[j]}")
        break

# RegisterService - can it leave null?
gl = open(
    r"Assets/_Project/Scripts/Core/GlobalRegistry.cs",
    encoding="utf-8",
    errors="replace",
).read().splitlines()
print("==== RegisterService method ====")
for i, l in enumerate(gl):
    if "static void RegisterService" in l or "private static void RegisterService" in l:
        for j in range(i, min(i + 40, len(gl))):
            print(f"{j+1}|{gl[j]}")
        print("---")

print("==== smoke: SystemDispatcher after ensure, any Register fail ====")
log = open(
    "Docs/AgentLogs/headless_smoke_20260730.log", encoding="utf-8", errors="replace"
).read().splitlines()
for key in (
    "SystemDispatcher",
    "headless=",
    "Headless",
    "short-circuit",
    "PhaseStarted",
    "SceneActivate",
    "Fatal",
    "main-menu",
    "handoff",
    "REGISTER",
    "registry",
):
    pass
# print unique bootstrap lines only
for l in log:
    if "[GameBootstrapper" in l or "[HEADLESS]" in l or "BATCH" in l or "REFUSED" in l:
        print(l[:250])
