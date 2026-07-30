# scratch - do not commit
import sys, os
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
os.chdir(r"C:\hades\Hecton8")

sd = open(
    r"Assets/_Project/Scripts/Core/SystemDispatcher.cs",
    encoding="utf-8",
    errors="replace",
).read().splitlines()
print("==== SystemDispatcher InitializeService window ====")
for i, l in enumerate(sd):
    if "void InitializeService" in l:
        for j in range(i, min(i + 90, len(sd))):
            print(f"{j+1}|{sd[j]}")
        break

gb = open(
    r"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs",
    encoding="utf-8",
    errors="replace",
).read().splitlines()
print("==== InitializeCoreServices headless assign 2595-2640 ====")
for i in range(2595, 2645):
    print(f"{i+1}|{gb[i]}")

print("==== SceneActivate exact 3133-3180 ====")
for i in range(3132, 3180):
    print(f"{i+1}|{gb[i]}")

print("==== LoadGameplay first 120 lines of method ====")
for i, l in enumerate(gb):
    if "LoadGameplaySceneFromBootstrapHandoffAsync" in l and "private async" in l:
        for j in range(i, min(i + 120, len(gb))):
            print(f"{j+1}|{gb[j]}")
        break

print("==== BootstrapSceneLoadWatchdogSeconds value ====")
for i, l in enumerate(gb):
    if "BootstrapSceneLoadWatchdog" in l and ("const" in l or "=" in l) and i < 800:
        print(f"{i+1}|{l}")
