from pathlib import Path
import re

out = Path(r"Tools/_cline_scratch/_diag_fo_lock_out.txt")
lines = []

# Find all origin shift bootstrap lock touches
for path in Path("Assets").rglob("*.cs"):
    try:
        t = path.read_text(encoding="utf-8", errors="replace")
    except Exception:
        continue
    keys = (
        "OriginShiftBootstrapLock",
        "originShiftBootstrapLock",
        "_originShiftBootstrapLockCount",
        "AcquireOriginShiftBootstrap",
        "ReleaseOriginShiftBootstrap",
        "TryFlushInitialSceneRebaseBeforeTicks",
        "EnterOriginShiftBootstrap",
        "ExitOriginShiftBootstrap",
    )
    hits = [k for k in keys if k in t]
    if not hits:
        continue
    lines.append(f"FILE {path} hits={hits}")
    for k in hits:
        idx = 0
        n = 0
        while n < 6:
            j = t.find(k, idx)
            if j < 0:
                break
            # line number
            ln = t.count("\n", 0, j) + 1
            lines.append(f"  {k} L{ln}:")
            lines.append(t[max(0, j - 100) : j + 280].replace("\r", ""))
            idx = j + len(k)
            n += 1

# GameBootstrapper short-circuit context + any FO unlock nearby
bp = Path(r"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs").read_text(encoding="utf-8")
idx = bp.find("Headless SceneActivate short-circuit")
lines.append("---short-circuit ctx---")
lines.append(bp[max(0, idx - 400) : idx + 500])

# full path ExecuteSceneActivationAsync around PublishGameReady(true)
idx = bp.find("PublishGameReady(true)")
# find all
positions = []
start = 0
while True:
    j = bp.find("PublishGameReady(true)", start)
    if j < 0:
        break
    positions.append(j)
    start = j + 1
lines.append(f"PublishGameReady(true) count={len(positions)} pos={positions}")
for j in positions:
    ln = bp.count("\n", 0, j) + 1
    lines.append(f"---PublishGameReady(true) L{ln}---")
    lines.append(bp[max(0, j - 500) : j + 400])

# search for floating origin unlock in bootstrapper
for k in (
    "OriginShift",
    "FloatingOrigin",
    "BootstrapLock",
    "FlushInitial",
    "Rebase",
    "UnlockOrigin",
):
    lines.append(f"BP {k} count={bp.count(k)}")

out.write_text("\n".join(lines), encoding="utf-8")
print("WROTE", out, "n", len(lines))
