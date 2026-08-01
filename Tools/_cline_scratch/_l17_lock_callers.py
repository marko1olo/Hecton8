# -*- coding: utf-8 -*-
import pathlib
import re

root = pathlib.Path(r"C:\hades\Hecton8")
out = []

# Find all RequestOriginShiftBootstrapLock / ReleaseOriginShiftBootstrapLock callers
patterns = [
    "RequestOriginShiftBootstrapLock",
    "ReleaseOriginShiftBootstrapLock",
    "RequestOriginShiftFrameLock",
    "IsOriginShiftBootstrapLocked",
    "_originShiftBootstrapLockCount",
]
for rel in [
    r"Assets\_Project\Scripts",
]:
    base = root / rel
    for p in base.rglob("*.cs"):
        text = p.read_text(encoding="utf-8", errors="replace")
        if not any(k in text for k in patterns):
            continue
        lines = text.splitlines()
        hits = []
        for i, l in enumerate(lines):
            for k in patterns:
                if k in l:
                    hits.append(f"{i+1}|{l.strip()}")
                    break
        if hits:
            out.append(f"==== {p.relative_to(root)} ====")
            out.extend(hits)

# TryPrepareShiftTargets body
fo = (root / r"Assets\_Project\Scripts\HectonFloatingOrigin.cs").read_text(
    encoding="utf-8", errors="replace"
).splitlines()
for i, l in enumerate(fo):
    if "void TryPrepareShiftTargets" in l or "bool TryPrepareShiftTargets" in l:
        out.append(f"==== TryPrepareShiftTargets from {i+1} ====")
        depth = 0
        started = False
        for j in range(i, min(i + 100, len(fo))):
            out.append(f"{j+1}|{fo[j]}")
            if "{" in fo[j]:
                depth += fo[j].count("{")
                started = True
            if "}" in fo[j]:
                depth -= fo[j].count("}")
                if started and depth <= 0:
                    break
        break

# FO.Tick - does it call ProcessPending / ResumePhysics?
for i, l in enumerate(fo):
    if re.search(r"\bvoid Tick\b|\bpublic void Tick\b|\bvoid IUpdatable", l) or (
        "public void Tick(" in l
    ):
        out.append(f"==== FO Tick candidate {i+1}|{l} ====")

for i, l in enumerate(fo):
    if "public void Tick(" in l or "void Tick(float" in l:
        out.append(f"==== FO.Tick from {i+1} ====")
        depth = 0
        started = False
        for j in range(i, min(i + 80, len(fo))):
            out.append(f"{j+1}|{fo[j]}")
            if "{" in fo[j]:
                depth += fo[j].count("{")
                started = True
            if "}" in fo[j]:
                depth -= fo[j].count("}")
                if started and depth <= 0:
                    break
        break

# LateFrame path - does it also gate on bootstrap lock?
sd = (root / r"Assets\_Project\Scripts\Core\SystemDispatcher.cs").read_text(
    encoding="utf-8", errors="replace"
).splitlines()
for i, l in enumerate(sd):
    if "RunDispatcherLateFrame" in l and "void" in l:
        out.append(f"==== RunDispatcherLateFrame from {i+1} ====")
        depth = 0
        started = False
        for j in range(i, min(i + 80, len(sd))):
            out.append(f"{j+1}|{sd[j]}")
            if "{" in sd[j]:
                depth += sd[j].count("{")
                started = True
            if "}" in sd[j]:
                depth -= sd[j].count("}")
                if started and depth <= 0:
                    break
        break

# IsOriginShiftBootstrapLocked property
for i, l in enumerate(sd):
    if "IsOriginShiftBootstrapLocked" in l and ("bool" in l or "=>" in l or "get" in l):
        out.append(f"{i+1}|{l}")
        for j in range(i, min(i + 8, len(sd))):
            out.append(f"{j+1}|{sd[j]}")

dest = root / r"Tools\_cline_scratch\_l17_lock_callers.txt"
dest.write_text("\n".join(out), encoding="utf-8")
print("WROTE", dest.stat().st_size, flush=True)
