import os
import re
from pathlib import Path

os.chdir(r"C:\hades\Hecton8")
out = Path("_agent_probe_pause_skip_out.txt")
lines = []

def p(m=""):
    lines.append(str(m))

disp = Path("Assets/_Project/Scripts/Core/SystemDispatcher.cs")
text = disp.read_text(encoding="utf-8", errors="replace")
ls = text.splitlines()

def dump(a, b, title):
    p(f"=== {title} ({a}-{b}) ===")
    for i in range(a - 1, min(len(ls), b)):
        p(f"{i+1}|{ls[i]}")

# RequestSimulationPause full
for i, ln in enumerate(ls):
    if "public void RequestSimulationPause" in ln:
        dump(i + 1, i + 50, "RequestSimulationPause")
        break

# ShouldSkipLaneDuringBootstrap full method
for i, ln in enumerate(ls):
    if "ShouldSkipLaneDuringBootstrap" in ln and ("bool" in ln or "static" in ln):
        dump(i + 1, i + 40, "ShouldSkipLaneDuringBootstrap def")
        break

# Also search definition patterns
m = re.search(
    r"(private|public|static|internal).{0,60}ShouldSkipLaneDuringBootstrap\s*\([\s\S]{0,900}",
    text,
)
if m:
    p("=== ShouldSkipLaneDuringBootstrap regex ===")
    for ln in m.group(0).splitlines()[:40]:
        p(ln)

# LateFrame path gates
for i, ln in enumerate(ls):
    if "RunDispatcherLateFrame" in ln or "void RunLateFrame" in ln or "LateFrameTick" in ln and "private void" in ln:
        p(f"late hit {i+1}|{ln.strip()}")

m = re.search(r"private void RunDispatcherLateFrame\([\s\S]{0,1200}", text)
if m:
    p("=== RunDispatcherLateFrame ===")
    for ln in m.group(0).splitlines()[:50]:
        p(ln)

m = re.search(r"private static void RunDispatcherLateFrameFromPlayerLoop[\s\S]{0,400}", text)
if m:
    p("=== LateFrameFromPlayerLoop ===")
    for ln in m.group(0).splitlines()[:20]:
        p(ln)

# Find all early returns related to bootstrap in late frame area
for i, ln in enumerate(ls):
    if "LateFrame" in ln and i > 5400 and i < 5600:
        pass
dump(5450, 5580, "LateFrame region ~5450")

# Headless short-circuit / GameReady publish
p("=== GameReady / headless short-circuit sites ===")
for root, _, files in os.walk("Assets/_Project/Scripts"):
    for f in files:
        if not f.endswith(".cs"):
            continue
        fp = Path(root) / f
        try:
            t = fp.read_text(encoding="utf-8", errors="replace")
        except Exception:
            continue
        if "PublishGameReady" in t or "IsGameReady" in t and "headless" in t.lower():
            rel = str(fp).replace("\\", "/")
            for j, l2 in enumerate(t.splitlines(), 1):
                if "PublishGameReady" in l2 or ("Headless" in l2 and "GameReady" in l2):
                    p(f"{rel}:{j}|{l2.strip()[:180]}")

# SceneRuntimeService unpause context
p("=== SceneRuntimeService pause context ===")
srs = Path("Assets/_Project/Scripts/Core/SceneRuntimeService.cs")
if srs.exists():
    st = srs.read_text(encoding="utf-8", errors="replace")
    sl = st.splitlines()
    for i, ln in enumerate(sl):
        if "RequestSimulationPause" in ln:
            dump_start = max(0, i - 15)
            for j in range(dump_start, min(len(sl), i + 20)):
                p(f"SRS{j+1}|{sl[j][:200]}")

# BootstrapState
p("=== BootstrapState ===")
for fp in Path("Assets/_Project/Scripts").rglob("*BootstrapState*"):
    p(str(fp))
    t = fp.read_text(encoding="utf-8", errors="replace")
    for j, ln in enumerate(t.splitlines(), 1):
        if "GameReady" in ln or "HasActiveInstance" in ln or "Publish" in ln:
            p(f"{j}|{ln.rstrip()[:200]}")

out.write_text("\n".join(lines), encoding="utf-8")
print("WROTE", out, out.stat().st_size)
