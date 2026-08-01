import os
os.chdir(r"C:\\hades\\Hecton8")

# Runner fields + LogRunnerLifecycle + constants near top
runner = r"Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs"
lines = open(runner, encoding="utf-8", errors="replace").read().splitlines()
chunks = []
chunks.append("==== RUNNER 1-220 ====")
for i in range(0, min(220, len(lines))):
    chunks.append(f"{i+1}:{lines[i]}")

# LogRunnerLifecycle
for i, l in enumerate(lines):
    if "void LogRunnerLifecycle" in l or "private void LogRunner" in l:
        for j in range(i, min(len(lines), i + 25)):
            chunks.append(f"{j+1}:{lines[j]}")
        break

# StressFracture ApplyHeadless
hits = []
for root, dirs, files in os.walk(r"Assets/_Project/Scripts"):
    for f in files:
        if not f.endswith(".cs"):
            continue
        path = os.path.join(root, f)
        try:
            txt = open(path, encoding="utf-8", errors="replace").read()
        except Exception:
            continue
        if "ApplyHeadlessTimeDilation" in txt or "RequestHeadlessTimeDilation" in txt:
            for n, line in enumerate(txt.splitlines(), 1):
                if "HeadlessTimeDilation" in line or "ApplyHeadless" in line or "RequestHeadlessTimeDilation" in line:
                    hits.append(f"{path}:{n}:{line.strip()}")

chunks.append("\n==== DILATION CALL SITES ====")
chunks.extend(hits[:80])

# BACKLOG ecology section
bl = "BACKLOG.md"
if os.path.exists(bl):
    bt = open(bl, encoding="utf-8", errors="replace").read().splitlines()
    chunks.append("\n==== BACKLOG ecology hits ====")
    for i, l in enumerate(bt):
        if any(k in l.lower() for k in ("ecolog", "headless", "day-advance", "dilation", "p0")):
            s = max(0, i - 1)
            e = min(len(bt), i + 3)
            for j in range(s, e):
                chunks.append(f"BL{j+1}:{bt[j]}")
            chunks.append("---")

# GameBootstrapper headless short-circuit context
gb = r"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs"
gl = open(gb, encoding="utf-8", errors="replace").read().splitlines()
chunks.append("\n==== BOOTSTRAP headless short-circuit ~3140-3180 ====")
for i in range(3135, min(3185, len(gl))):
    chunks.append(f"GB{i+1}:{gl[i]}")

open("_agent_scan_fix_ctx_out.txt", "w", encoding="utf-8").write("\n".join(chunks))
print("WROTE _agent_scan_fix_ctx_out.txt", len(chunks))
