# -*- coding: utf-8 -*-
import pathlib

root = pathlib.Path(r"C:\hades\Hecton8")
sd = (root / r"Assets\_Project\Scripts\Core\SystemDispatcher.cs").read_text(
    encoding="utf-8", errors="replace"
).splitlines()
out = []

# RunDispatcherLateFrame method body around 5450-5560 from earlier notes
for start, end, label in [
    (5440, 5560, "LateFrame body"),
    (4660, 4720, "gate 4682 context"),
    (800, 830, "static reset lock"),
]:
    out.append(f"==== {label} {start}-{end} ====")
    for i in range(start - 1, min(end, len(sd))):
        out.append(f"{i+1}|{sd[i]}")

# _aupPreShiftPauseFrameId clear sites
out.append("==== aup pause field usage ====")
for i, l in enumerate(sd):
    if "_aupPreShiftPauseFrameId" in l or "ReleaseAupPreShiftPause" in l or "ResolveCurrentDispatcherFrameId" in l:
        out.append(f"{i+1}|{l}")

# BuildMasterDispatcherTiming FrameId
for i, l in enumerate(sd):
    if "BuildMasterDispatcherTiming" in l and "DispatcherTimingDTO" in l:
        out.append(f"==== BuildMasterDispatcherTiming from {i+1} ====")
        depth = 0
        started = False
        for j in range(i, min(i + 40, len(sd))):
            out.append(f"{j+1}|{sd[j]}")
            if "{" in sd[j]:
                depth += sd[j].count("{")
                started = True
            if "}" in sd[j]:
                depth -= sd[j].count("}")
                if started and depth <= 0:
                    break
        break

# AdvanceDispatcherFrameId
for i, l in enumerate(sd):
    if "void AdvanceDispatcherFrameId" in l or "AdvanceDispatcherFrameId()" in l and "void" in l:
        out.append(f"==== AdvanceDispatcherFrameId from {i+1} ====")
        depth = 0
        started = False
        for j in range(i, min(i + 30, len(sd))):
            out.append(f"{j+1}|{sd[j]}")
            if "{" in sd[j]:
                depth += sd[j].count("{")
                started = True
            if "}" in sd[j]:
                depth -= sd[j].count("}")
                if started and depth <= 0:
                    break
        if "void" in l:
            break

# INPUTHOP frameId emission in InputDispatcher
idpath = root / r"Assets\_Project\Scripts\Core\InputDispatcher.cs"
idlines = idpath.read_text(encoding="utf-8", errors="replace").splitlines()
out.append("==== INPUTHOP emission ====")
for i, l in enumerate(idlines):
    if "INPUTHOP" in l or "frameId=" in l and "Diag" in l:
        out.append(f"{i+1}|{l}")
for i, l in enumerate(idlines):
    if "H8_INPUTHOP" in l or "EmitInputHop" in l or "DiagMaybeEmit" in l or "INPUTHOP" in l:
        # dump surrounding
        for j in range(max(0, i - 2), min(len(idlines), i + 40)):
            out.append(f"{j+1}|{idlines[j]}")
        out.append("---")

# FO BeginShiftWorld - RequestOriginShiftFrameLock context
fo = (root / r"Assets\_Project\Scripts\HectonFloatingOrigin.cs").read_text(
    encoding="utf-8", errors="replace"
).splitlines()
for i, l in enumerate(fo):
    if "RequestOriginShiftFrameLock" in l:
        out.append(f"==== FO frame lock call {i+1} context ====")
        for j in range(max(0, i - 30), min(len(fo), i + 40)):
            out.append(f"{j+1}|{fo[j]}")

# L16 log: any FO / bootstrap / origin lock lines?
log = root / r"Docs\AgentLogs\h8_playprobe_v0_L16.log"
if log.exists():
    out.append("==== L16 log FO/lock/halt hits ====")
    keys = (
        "bootstrap",
        "BootstrapLocked",
        "OriginShift",
        "FO ",
        "floating",
        "scene rebase",
        "SceneRebase",
        "physics pause",
        "SimulationHalted",
        "AUP",
        "dispBoot",
        "TryFlush",
        "STEP-BOUNDED",
    )
    count = 0
    with log.open(encoding="utf-8", errors="replace") as f:
        for i, line in enumerate(f, 1):
            low = line.lower()
            if any(k.lower() in low for k in keys):
                out.append(f"L{i}|{line.rstrip()[:240]}")
                count += 1
                if count > 80:
                    out.append("...truncated...")
                    break
    out.append(f"total_hits_shown={count}")

dest = root / r"Tools\_cline_scratch\_l17_late_aup.txt"
dest.write_text("\n".join(out), encoding="utf-8")
print("WROTE", dest.stat().st_size, flush=True)
