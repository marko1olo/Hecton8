# -*- coding: utf-8 -*-
import pathlib

root = pathlib.Path(r"C:\hades\Hecton8")
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


def dump_method(lines, name, tag, maxn=120):
    for i, l in enumerate(lines):
        if name not in l or "(" not in l:
            continue
        if not any(k in l for k in ("void ", "bool ", "static ", "private ", "public ", "internal ")):
            continue
        depth = 0
        started = False
        for j in range(i, min(i + maxn, len(lines))):
            out.append(f"{tag} {j+1}|{lines[j]}")
            depth += lines[j].count("{") - lines[j].count("}")
            if "{" in lines[j]:
                started = True
            if started and depth <= 0:
                break
        out.append("---")
        return True
    out.append(f"{tag} MISSING {name}")
    out.append("---")
    return False


dump_method(hsr, "MaybeLogPostReadyProgress", "HSR")
dump_method(hsr, "LogEcologyBootstrapTimeoutDiagnostics", "HSR")
dump_method(hsr, "MaybeLogEcologyWaitProgress", "HSR")
dump_method(hsr, "EnsureHeadlessSimulationClock", "HSR")
dump_method(probe, "EnsureProbeSimulationClock", "PR")
dump_method(probe, "MaybeEnsureProbeSimulationClockSustain", "PR")

out.append("==== Probe gameplay 730-850 ====")
for j in range(729, min(850, len(probe))):
    out.append(f"PR {j+1}|{probe[j]}")

out.append("==== FO TryFlush + CopyBootstrap ====")
dump_method(fo, "TryFlushInitialSceneRebaseBeforeTicks", "FO")
dump_method(fo, "CopyBootstrapDrainSnapshot", "FO")

for i, l in enumerate(fo):
    if "struct BootstrapDrainSnapshot" in l or "BootstrapDrainSnapshot" in l and "struct" in l:
        for j in range(max(0, i - 2), min(len(fo), i + 45)):
            out.append(f"FO {j+1}|{fo[j]}")
        out.append("---")
        break

# also search probe fields near clock
out.append("==== Probe fields near SIMCLOCK ====")
for i, l in enumerate(probe):
    if "SIMCLOCK" in l or "_probeSimClock" in l or "stepBounded" in l.lower() or "FODRAIN" in l:
        for j in range(max(0, i - 2), min(len(probe), i + 4)):
            out.append(f"PR {j+1}|{probe[j]}")
        out.append("---")

dest = root / r"Tools\_cline_scratch\_l17_hsr_probe.txt"
dest.write_text("\n".join(out), encoding="utf-8")
print("WROTE", dest.stat().st_size, flush=True)
