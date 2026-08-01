# -*- coding: utf-8 -*-
import pathlib

root = pathlib.Path(r"C:\hades\Hecton8")
p = root / r"Assets\_Project\Scripts\HectonFloatingOrigin.cs"
lines = p.read_text(encoding="utf-8", errors="replace").splitlines()
out = []
keys = [
    "ReleaseSceneRebaseTickLock",
    "AcquireSceneRebaseTickLock",
    "ProcessPendingSceneSynchronization",
    "QueuePendingLoadedScene",
    "_sceneRebaseTickLockHeld",
    "ResumePhysicsAfterShift",
]
for k in keys:
    out.append("=== " + k + " ===")
    for i, l in enumerate(lines):
        if k in l:
            out.append(f"{i+1}|{l}")

start = None
for i, l in enumerate(lines):
    if "ProcessPendingSceneSynchronization" in l and ("void " in l or "private " in l):
        start = i
        break
if start is not None:
    out.append(f"==== ProcessPending from {start+1} ====")
    depth = 0
    started = False
    for j in range(start, min(start + 150, len(lines))):
        out.append(f"{j+1}|{lines[j]}")
        if "{" in lines[j]:
            depth += lines[j].count("{")
            started = True
        if "}" in lines[j]:
            depth -= lines[j].count("}")
            if started and depth <= 0:
                break

# Also dump QueuePendingLoadedScene
for i, l in enumerate(lines):
    if "void QueuePendingLoadedScene" in l:
        out.append(f"==== QueuePending from {i+1} ====")
        depth = 0
        started = False
        for j in range(i, min(i + 80, len(lines))):
            out.append(f"{j+1}|{lines[j]}")
            if "{" in lines[j]:
                depth += lines[j].count("{")
                started = True
            if "}" in lines[j]:
                depth -= lines[j].count("}")
                if started and depth <= 0:
                    break
        break

dest = root / r"Tools\_cline_scratch\_l17_fo_lock.txt"
dest.write_text("\n".join(out), encoding="utf-8")
print("WROTE", dest, dest.stat().st_size, flush=True)
