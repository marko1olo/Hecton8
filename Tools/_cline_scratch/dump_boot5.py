# scratch - do not commit
import sys, os
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
os.chdir(r"C:\hades\Hecton8")

sd = open(
    r"Assets/_Project/Scripts/Core/SystemDispatcher.cs",
    encoding="utf-8",
    errors="replace",
).read().splitlines()
print("==== SystemDispatcher InitializeService / Register ====")
for i, l in enumerate(sd):
    if any(
        k in l
        for k in (
            "InitializeService",
            "RegisterSystemDispatcher",
            "OnServiceShutdown",
            "ActiveRuntimeInstance",
            "_dispatcher",
        )
    ):
        print(f"{i+1}|{l}")
for i, l in enumerate(sd):
    if "void InitializeService" in l or "InitializeService(" in l and "void" in l:
        for j in range(i, min(i + 80, len(sd))):
            print(f"{j+1}|{sd[j]}")
        break

gb = open(
    r"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs",
    encoding="utf-8",
    errors="replace",
).read().splitlines()
print("==== HasWatchdogElapsed / BootstrapSceneLoadWatchdog ====")
for i, l in enumerate(gb):
    if "HasWatchdogElapsed" in l or "BootstrapSceneLoadWatchdog" in l or "BootstrapGameplayHandoffStall" in l:
        print(f"{i+1}|{l}")
for i, l in enumerate(gb):
    if "static bool HasWatchdogElapsed" in l or "bool HasWatchdogElapsed" in l:
        for j in range(i, min(i + 25, len(gb))):
            print(f"{j+1}|{gb[j]}")
        break

print("==== _headlessBootMode assignments ====")
for i, l in enumerate(gb):
    if "_headlessBootMode" in l:
        print(f"{i+1}|{l}")

print("==== WaitForBootstrapActivationGatesAsync ====")
for i, l in enumerate(gb):
    if "WaitForBootstrapActivationGatesAsync" in l:
        print(f"{i+1}|{l}")
for i, l in enumerate(gb):
    if "WaitForBootstrapActivationGatesAsync" in l and ("async" in l or "private" in l):
        for j in range(i, min(i + 60, len(gb))):
            print(f"{j+1}|{gb[j]}")
        break

print("==== HeadlessSimulationRunner ForceHeadless / startup timeout ====")
hr = open(
    r"Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs",
    encoding="utf-8",
    errors="replace",
).read().splitlines()
for i, l in enumerate(hr):
    if any(
        k in l
        for k in (
            "_startupTimeout",
            "ForceHeadless",
            "TimeDilation",
            "TryMarkEcology",
            "ecologySampled",
            "Finish(",
            "FailAndQuit",
        )
    ):
        print(f"{i+1}|{l}")

print("==== BatchRunner comments 80-100 ====")
br = open(
    r"Assets/_Project/Scripts/QA/Headless/Editor/HeadlessSimulationBatchRunner.cs",
    encoding="utf-8",
    errors="replace",
).read().splitlines()
for i in range(70, 160):
    print(f"{i+1}|{br[i]}")
