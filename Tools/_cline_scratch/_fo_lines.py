from pathlib import Path

t = Path(r"Assets/_Project/Scripts/HectonFloatingOrigin.cs").read_text(encoding="utf-8")
out = Path(r"Tools/_cline_scratch/_fo_lines.txt")
keys = [
    "RequestSceneRebaseTickLock",
    "ReleaseSceneRebaseTickLock",
    "_sceneRebaseTickLockHeld",
    "ProcessPendingSceneSynchronization",
    "QueuePendingLoadedScene",
    "_pendingLoadedScenes",
    "AcquireSceneRebase",
]
lines_out = []
for k in keys:
    lines_out.append(f"{k} {t.count(k)}")
for i, l in enumerate(t.splitlines(), 1):
    if any(k in l for k in keys):
        lines_out.append(f"{i}:{l}")

# dump ProcessPendingSceneSynchronization method
idx = t.find("void ProcessPendingSceneSynchronization")
if idx < 0:
    idx = t.find("ProcessPendingSceneSynchronization(")
lines_out.append(f"ProcessPending idx={idx}")
if idx >= 0:
    # find method start going backward to signature
    start = t.rfind("\n", 0, idx)
    start = t.rfind("\n", 0, start - 1)
    lines_out.append(t[start : start + 2500])

# RequestSceneRebaseTickLock method
idx = t.find("RequestSceneRebaseTickLock")
# find definition
idx = t.find("void RequestSceneRebaseTickLock")
if idx < 0:
    idx = t.find("private void RequestSceneRebaseTickLock")
lines_out.append(f"RequestLock idx={idx}")
if idx >= 0:
    lines_out.append(t[idx : idx + 600])

# who calls RequestSceneRebaseTickLock
idx = 0
n = 0
while n < 10:
    j = t.find("RequestSceneRebaseTickLock(", idx)
    if j < 0:
        break
    ln = t.count("\n", 0, j) + 1
    lines_out.append(f"call L{ln}: {t[max(0,j-80):j+40]}")
    idx = j + 5
    n += 1

out.write_text("\n".join(lines_out), encoding="utf-8")
print("ok", len(lines_out))
