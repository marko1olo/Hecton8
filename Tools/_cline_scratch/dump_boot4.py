# scratch - do not commit
import sys, os
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
os.chdir(r"C:\hades\Hecton8")

# GameStartContextHolder Reset + TryConsume
for root, ds, fs in os.walk("Assets"):
    for f in fs:
        if f.endswith(".cs") and "GameStartContext" in f:
            p = os.path.join(root, f)
            ls = open(p, encoding="utf-8", errors="replace").read().splitlines()
            print("====", p, "====")
            for i, l in enumerate(ls):
                if any(
                    k in l
                    for k in (
                        "Reset",
                        "TryConsume",
                        "PendingTarget",
                        "PlayerPrefs",
                        "Persist",
                    )
                ):
                    print(f"{i+1}|{l}")
            # full methods
            for i, l in enumerate(ls):
                if "static void Reset" in l or "TryConsumePending" in l or "static void SetPending" in l or "Restore" in l:
                    for j in range(i, min(i + 60, len(ls))):
                        print(f"{j+1}|{ls[j]}")
                    print("---")

print("==== RegisterSystemDispatcher body ====")
gl = open(
    r"Assets/_Project/Scripts/Core/GlobalRegistry.cs",
    encoding="utf-8",
    errors="replace",
).read().splitlines()
for i, l in enumerate(gl):
    if "RegisterSystemDispatcher" in l:
        for j in range(i, min(i + 40, len(gl))):
            print(f"{j+1}|{gl[j]}")
        print("---")

print("==== SystemDispatcher register self ====")
for root, ds, fs in os.walk("Assets"):
    for f in fs:
        if f == "SystemDispatcher.cs":
            p = os.path.join(root, f)
            print("PATH", p)
            ls = open(p, encoding="utf-8", errors="replace").read().splitlines()
            for i, l in enumerate(ls):
                if "RegisterSystemDispatcher" in l or "Awake" in l or "OnEnable" in l or "Bootstrap" in l:
                    if i < 200 or "Register" in l:
                        print(f"{i+1}|{l}")

print("==== How bootstrap creates SystemDispatcher ====")
gb = open(
    r"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs",
    encoding="utf-8",
    errors="replace",
).read().splitlines()
for i, l in enumerate(gb):
    if "SystemDispatcher" in l and (
        "Ensure" in l or "new " in l or "Register" in l or "Find" in l or "Create" in l
    ):
        print(f"{i+1}|{l}")
# print Ensure method if any
for i, l in enumerate(gb):
    if "SystemDispatcher" in l and ("private" in l or "static" in l) and (
        "Ensure" in l or "void" in l or "bool" in l
    ):
        for j in range(i, min(i + 50, len(gb))):
            print(f"{j+1}|{gb[j]}")
        print("---")

print("==== BootstrapStatus MarkMainMenuReached ====")
for root, ds, fs in os.walk("Assets"):
    for f in fs:
        if "BootstrapStatus" in f and f.endswith(".cs"):
            p = os.path.join(root, f)
            print("PATH", p)
            ls = open(p, encoding="utf-8", errors="replace").read().splitlines()
            for i, l in enumerate(ls):
                if "MarkMainMenu" in l or "MainMenu" in l or "class " in l:
                    print(f"{i+1}|{l}")

print("==== RunUnityBatchGate headless args ====")
py = open("Tools/RunUnityBatchGate.py", encoding="utf-8", errors="replace").read().splitlines()
for i, l in enumerate(py):
    if any(
        k in l.lower()
        for k in ("h8headless", "batch", "execute", "arg", "headless", "day")
    ):
        print(f"{i+1}|{l}")

print("==== prior reality map commit doc? ====")
for p in [
    "Docs/PLAYTEST",
    "BUILD_PLAYTEST_ISSUES.md",
    "README.md",
]:
    print(p, os.path.exists(p))
if os.path.isdir("Docs/PLAYTEST"):
    for root, ds, fs in os.walk("Docs/PLAYTEST"):
        for f in fs:
            print(os.path.join(root, f))
