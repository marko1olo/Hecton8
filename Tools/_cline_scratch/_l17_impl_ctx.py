# -*- coding: utf-8 -*-
import pathlib

root = pathlib.Path(r"C:\hades\Hecton8")
sd = (root / r"Assets\_Project\Scripts\Core\SystemDispatcher.cs").read_text(
    encoding="utf-8", errors="replace"
).splitlines()
hsr = (root / r"Assets\_Project\Scripts\QA\Headless\HeadlessSimulationRunner.cs").read_text(
    encoding="utf-8", errors="replace"
).splitlines()
probe = (
    root / r"Assets\_Project\Scripts\Editor\Diagnostics\H8_HeadlessPlayModeProbe.cs"
).read_text(encoding="utf-8", errors="replace").splitlines()
fo = (root / r"Assets\_Project\Scripts\HectonFloatingOrigin.cs").read_text(
    encoding="utf-8", errors="replace"
).splitlines()
out = []

out.append("==== AUP pause compare / set sites ====")
for i, l in enumerate(sd):
    if "_aupPreShiftPauseFrameId" in l:
        for j in range(max(0, i - 3), min(len(sd), i + 10)):
            out.append(f"{j+1}|{sd[j]}")
        out.append("---")

out.append("==== AdvanceDispatcherFrameId / _currentDispatcherFrameId ====")
for i, l in enumerate(sd):
    if "AdvanceDispatcherFrameId" in l or "_currentDispatcherFrameId" in l:
        if any(
            x in l
            for x in ("void ", "uint ", "=", "private", "public", "return", "Interlocked")
        ):
            for j in range(max(0, i - 2), min(len(sd), i + 25)):
                out.append(f"{j+1}|{sd[j]}")
            out.append("---")

out.append("==== RunDispatcherLateFrame start (120 lines) ====")
for i, l in enumerate(sd):
    if "RunDispatcherLateFrame" in l and ("void " in l or "(" in l):
        for j in range(i, min(i + 120, len(sd))):
            out.append(f"{j+1}|{sd[j]}")
            if j > i + 10 and sd[j].strip() == "}" and sd[j].startswith("        }"):
                # may be method end - keep going a bit
                pass
        break

out.append("==== RunDispatcherUpdate FO/AUP gates ====")
for i, l in enumerate(sd):
    if "IsOriginShiftBootstrapLocked" in l and 5100 < i < 5350:
        for j in range(max(0, i - 20), min(len(sd), i + 50)):
            out.append(f"{j+1}|{sd[j]}")
        out.append("---")
        break

out.append("==== FO TryFlush body ====")
for i, l in enumerate(fo):
    if "TryFlushInitialSceneRebaseBeforeTicks" in l and ("bool " in l or "public " in l):
        depth = 0
        started = False
        for j in range(i, min(i + 80, len(fo))):
            out.append(f"FO {j+1}|{fo[j]}")
            if "{" in fo[j]:
                depth += fo[j].count("{")
                started = True
            if "}" in fo[j]:
                depth -= fo[j].count("}")
                if started and depth <= 0:
                    break
        break

out.append("==== FO CopyBootstrapDrainSnapshot ====")
for i, l in enumerate(fo):
    if "CopyBootstrapDrainSnapshot" in l:
        depth = 0
        started = False
        for j in range(i, min(i + 50, len(fo))):
            out.append(f"FO {j+1}|{fo[j]}")
            if "{" in fo[j]:
                depth += fo[j].count("{")
                started = True
            if "}" in fo[j]:
                depth -= fo[j].count("}")
                if started and depth <= 0:
                    break
        break

out.append("==== HSR TryFlush / snapshot / clock ====")
keys = (
    "TryFlushInitialSceneRebase",
    "CopyBootstrapDrainSnapshot",
    "MaybeLogPostReady",
    "dispBootstrapLocked",
    "foPending",
    "EnsureHeadlessSimulationClock",
    "MaybeEnsureHeadless",
    "foLock",
    "foPhysics",
    "BootstrapDrain",
)
for i, l in enumerate(hsr):
    if any(x in l for x in keys):
        for j in range(max(0, i - 2), min(len(hsr), i + 8)):
            out.append(f"HSR {j+1}|{hsr[j]}")
        out.append("---")

out.append("==== HSR Update body FO drain region ====")
for i, l in enumerate(hsr):
    if "void Update()" in l or "private void Update()" in l or "protected void Update" in l:
        for j in range(i, min(i + 80, len(hsr))):
            out.append(f"HSR {j+1}|{hsr[j]}")
        break

out.append("==== Probe clock + gameplay ====")
pkeys = (
    "EnsureProbeSimulationClock",
    "MaybeEnsureProbeSimulationClock",
    "GameplayWarmup",
    "worlddriver-begin",
    "gameplay-window",
    "TryFlush",
    "CopyBootstrap",
    "void TickGameplay",
    "GameplaySeconds",
    "WorldDriver.Tick",
    "H8_HeadlessWorldDriver",
)
for i, l in enumerate(probe):
    if any(x in l for x in pkeys):
        for j in range(max(0, i - 1), min(len(probe), i + 6)):
            out.append(f"PR {j+1}|{probe[j]}")
        out.append("---")

out.append("==== Probe EnsureProbeSimulationClock full ====")
for i, l in enumerate(probe):
    if "EnsureProbeSimulationClock" in l and ("void " in l or "static " in l or "bool " in l):
        depth = 0
        started = False
        for j in range(i, min(i + 80, len(probe))):
            out.append(f"PR {j+1}|{probe[j]}")
            if "{" in probe[j]:
                depth += probe[j].count("{")
                started = True
            if "}" in probe[j]:
                depth -= probe[j].count("}")
                if started and depth <= 0:
                    break
        # continue for MaybeEnsure too
out.append("==== Probe MaybeEnsure full ====")
for i, l in enumerate(probe):
    if "MaybeEnsureProbeSimulationClockSustain" in l and ("void " in l or "static " in l):
        depth = 0
        started = False
        for j in range(i, min(i + 60, len(probe))):
            out.append(f"PR {j+1}|{probe[j]}")
            if "{" in probe[j]:
                depth += probe[j].count("{")
                started = True
            if "}" in probe[j]:
                depth -= probe[j].count("}")
                if started and depth <= 0:
                    break
        break

out.append("==== Probe gameplay loop region (search WorldDriver) ====")
for i, l in enumerate(probe):
    if "WorldDriver" in l or "gameplayElapsed" in l or "Gameplay phase" in l or "h8Gameplay" in l:
        for j in range(max(0, i - 2), min(len(probe), i + 5)):
            out.append(f"PR {j+1}|{probe[j]}")
        out.append("---")

dest = root / r"Tools\_cline_scratch\_l17_impl_ctx.txt"
dest.write_text("\n".join(out), encoding="utf-8")
print("WROTE", dest.stat().st_size, flush=True)
