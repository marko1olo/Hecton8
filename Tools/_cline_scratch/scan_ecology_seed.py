# scratch - do not commit
import sys, os
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
os.chdir(r"C:\hades\Hecton8")

gb = open(r"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs", encoding="utf-8", errors="replace").read().splitlines()
print("==== Player phase skip / headless phases 2380-2500 ====")
for i in range(2380, min(2520, len(gb))):
    print(f"{i+1}|{gb[i][:220]}")

print("==== EnsurePlayerSector / headless ecology seed mentions ====")
for i, l in enumerate(gb):
    if any(k in l for k in (
        "EnsurePlayerSector", "BootstrapPhase.Player", "headlessBootMode",
        "_headlessBootMode", "Player phase", "SkipPlayer", "SeedBiomass",
        "observer", "HeadlessEcology", "RegisterPlayerSector"
    )):
        print(f"{i+1}|{l[:220]}")

# dump method bodies around EnsurePlayerSector if in gb
for i, l in enumerate(gb):
    if "EnsurePlayerSector" in l and ("void" in l or "bool" in l or "static" in l):
        for j in range(i, min(i + 80, len(gb))):
            print(f"{j+1}|{gb[j][:220]}")
        print("---")

hr = open(r"Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs", encoding="utf-8", errors="replace").read().splitlines()
print("==== WaitForDispatcherAndStart full ====")
for i, l in enumerate(hr):
    if "WaitForDispatcherAndStart" in l and ("async" in l or "private" in l):
        for j in range(i, min(i + 90, len(hr))):
            print(f"{j+1}|{hr[j][:220]}")
        break

print("==== TryMarkEcologyReady full ====")
for i, l in enumerate(hr):
    if "void TryMarkEcologyReady" in l or "private void TryMarkEcologyReady" in l:
        for j in range(i, min(i + 50, len(hr))):
            print(f"{j+1}|{hr[j][:220]}")
        break

print("==== ExecuteDailyAudit ecology fail branch ====")
for i, l in enumerate(hr):
    if "ExecuteDailyAudit" in l and "void" in l:
        for j in range(i, min(i + 120, len(hr))):
            print(f"{j+1}|{hr[j][:220]}")
        break

print("==== Start method ====")
for i, l in enumerate(hr):
    if "private void Start()" in l or "void Start()" in l:
        for j in range(i, min(i + 50, len(hr))):
            print(f"{j+1}|{hr[j][:220]}")
        break

# EcosystemDirector EnsurePlayerSectorRegistered
print("==== EcosystemDirector EnsurePlayerSector / headless ====")
for root, ds, fs in os.walk("Assets/_Project/Scripts"):
    for f in fs:
        if f == "EcosystemDirector.cs":
            p = os.path.join(root, f)
            ls = open(p, encoding="utf-8", errors="replace").read().splitlines()
            print("PATH", p, "lines", len(ls))
            for i, l in enumerate(ls):
                if any(k in l for k in (
                    "EnsurePlayerSector", "RegisterSector", "TryGetGlobalBiomass",
                    "Seed", "headless", "Headless", "observer", "ActiveBiomass",
                    "_activeBiomassCellCount"
                )):
                    print(f"{i+1}|{l[:220]}")
            for i, l in enumerate(ls):
                if "EnsurePlayerSectorRegistered" in l and ("void" in l or "bool" in l):
                    for j in range(i, min(i + 80, len(ls))):
                        print(f"M{j+1}|{ls[j][:220]}")
                    print("---")
            for i, l in enumerate(ls):
                if "TryGetGlobalBiomassAudit" in l and ("bool" in l or "public" in l):
                    for j in range(i, min(i + 40, len(ls))):
                        print(f"A{j+1}|{ls[j][:220]}")
                    print("---")
