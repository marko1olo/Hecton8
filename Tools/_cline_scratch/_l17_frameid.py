# -*- coding: utf-8 -*-
import pathlib

root = pathlib.Path(r"C:\hades\Hecton8")
sd = (root / r"Assets\_Project\Scripts\Core\SystemDispatcher.cs").read_text(
    encoding="utf-8", errors="replace"
).splitlines()
out = []

for i, l in enumerate(sd):
    if "CurrentFrameId" in l or "CurrentFrameIndex" in l or "_dispatcherFrameId" in l or "_frameIndex" in l:
        if any(
            x in l
            for x in (
                "static",
                "public",
                "private",
                "internal",
                "=>",
                "Advance",
                "Resolve",
                "Interlocked",
                "Volatile",
                "=",
            )
        ):
            out.append(f"{i+1}|{l}")

out.append("==== ResolveCurrentDispatcherFrameId body ====")
for i, l in enumerate(sd):
    if "uint ResolveCurrentDispatcherFrameId" in l or "ResolveCurrentDispatcherFrameId()" in l and "private" in l:
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
        if "uint Resolve" in l or "private" in l:
            break

out.append("==== AdvanceDispatcherFrameId body ====")
for i, l in enumerate(sd):
    if "void AdvanceDispatcherFrameId" in l:
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

# ReleaseAupPreShiftPause full + OnDisable clear at 2093
out.append("==== ReleaseAup and clear context ====")
for i, l in enumerate(sd):
    if "ReleaseAupPreShiftPause" in l or "_aupPreShiftPauseFrameId = 0" in l:
        for j in range(max(0, i - 5), min(len(sd), i + 25)):
            out.append(f"{j+1}|{sd[j]}")
        out.append("---")

# ShouldSkipLane full
out.append("==== ShouldSkipLane ====")
for i, l in enumerate(sd):
    if "bool ShouldSkipLaneDuringBootstrap" in l:
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

dest = root / r"Tools\_cline_scratch\_l17_frameid.txt"
dest.write_text("\n".join(out), encoding="utf-8")
print("WROTE", dest.stat().st_size, flush=True)
