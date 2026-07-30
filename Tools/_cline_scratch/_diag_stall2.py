from pathlib import Path
import re

out = Path(r"Tools/_cline_scratch/_diag_stall2_out.txt")
lines = []

# SystemDispatcher: blockGameplayLanes usage
sd = Path(r"Assets/_Project/Scripts/Core/SystemDispatcher.cs").read_text(encoding="utf-8")
for key in ("blockGameplayLanes", "ShouldSkipLaneDuringBootstrap", "IsGameReady", "IsOriginShiftBootstrapLocked"):
    lines.append(f"{key}={sd.count(key)}")

# all contexts of blockGameplayLanes
idx = 0
n = 0
while n < 12:
    j = sd.find("blockGameplayLanes", idx)
    if j < 0:
        break
    lines.append(f"---blockGameplayLanes@{j}---")
    lines.append(sd[max(0, j - 200) : j + 400])
    idx = j + 10
    n += 1

# IsOriginShiftBootstrapLocked contexts
idx = 0
n = 0
while n < 8:
    j = sd.find("IsOriginShiftBootstrapLocked", idx)
    if j < 0:
        break
    lines.append(f"---IsOriginShiftBootstrapLocked@{j}---")
    lines.append(sd[max(0, j - 150) : j + 350])
    idx = j + 10
    n += 1

# RunDispatcherLateFrame body snippet
j = sd.find("RunDispatcherLateFrame")
if j >= 0:
    lines.append("---RunDispatcherLateFrame first---")
    lines.append(sd[j : j + 900])

# Floating origin lock definition
fo = Path(r"Assets/_Project/Scripts/HectonFloatingOrigin.cs").read_text(encoding="utf-8")
for key in ("IsOriginShiftBootstrapLocked", "BootstrapLocked", "SetBootstrap", "Unlock", "Lock"):
    if key in fo:
        lines.append(f"FO has {key}")
idx = fo.find("IsOriginShiftBootstrapLocked")
if idx >= 0:
    lines.append("---FO IsOriginShiftBootstrapLocked---")
    lines.append(fo[max(0, idx - 200) : idx + 800])
# also property definition
for m in re.finditer(r"IsOriginShiftBootstrapLocked[\s\S]{0,400}", fo):
    lines.append("---FO match---")
    lines.append(m.group(0)[:500])
    break

# Runner defaults
rt = Path(r"Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs").read_text(encoding="utf-8")
for i, l in enumerate(rt.splitlines(), 1):
    if any(
        k in l
        for k in (
            "DefaultStartupTimeout",
            "StartupTimeoutArg",
            "_startupTime",
            "BeginStartup",
            "RequestHeadlessTimeDilation",
        )
    ):
        lines.append(f"R{i}:{l}")

# BeginStartup body
idx = rt.find("void BeginStartup")
if idx < 0:
    idx = rt.find("BeginStartup()")
# find method
m = re.search(r"private void BeginStartup\(\)[\s\S]{0,1500}", rt)
if m:
    lines.append("---BeginStartup---")
    lines.append(m.group(0)[:1500])

# TryCompleteDispatcherWait
m = re.search(r"private void TryCompleteDispatcherWait\(\)[\s\S]{0,2000}", rt)
if m:
    lines.append("---TryCompleteDispatcherWait---")
    lines.append(m.group(0)[:2000])

# EcosystemDirector IsInitialized
eco_cands = list(Path("Assets").rglob("*EcosystemDirector*.cs"))
lines.append(f"eco_cands={[str(c) for c in eco_cands[:10]]}")
for c in eco_cands[:5]:
    t = c.read_text(encoding="utf-8", errors="replace")
    if "IsInitialized" in t:
        lines.append(f"---{c} IsInitialized---")
        idx = 0
        n = 0
        while n < 4:
            j = t.find("IsInitialized", idx)
            if j < 0:
                break
            lines.append(t[max(0, j - 80) : j + 250])
            idx = j + 10
            n += 1

# log timestamps around key events - look for time prefixes
log = Path(r"Docs/AgentLogs/headless_smoke_20260730_p0_gameready.log").read_text(
    encoding="utf-8", errors="replace"
)
# unity sometimes has timestamps like 0.0s or DateTime
for needle in (
    "runner installed",
    "waiting for dispatcher",
    "dispatcher acquired",
    "runtime lanes registered",
    "PublishGameReady on bootstrap",
    "BOOTSTRAP_TIMEOUT",
    "fail exitCode",
):
    j = log.find(needle)
    lines.append(f"log '{needle}' idx={j}")
    if j >= 0:
        # print surrounding 300 chars looking for time
        lines.append(repr(log[max(0, j - 120) : j + 80]))

out.write_text("\n".join(lines), encoding="utf-8")
print("WROTE", len(lines))
