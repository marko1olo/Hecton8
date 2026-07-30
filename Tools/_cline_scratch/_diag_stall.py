from pathlib import Path
import re

out = Path(r"Tools/_cline_scratch/_diag_stall_out.txt")
lines = []

# 1) SystemDispatcher ShouldSkipLaneDuringBootstrap + origin shift lock
sd = Path(r"Assets/_Project/Scripts/Core/SystemDispatcher.cs")
if not sd.exists():
    # find it
    cands = list(Path("Assets").rglob("SystemDispatcher.cs"))
    lines.append(f"SystemDispatcher cands={[str(c) for c in cands]}")
    sd = cands[0] if cands else None
else:
    lines.append(f"SystemDispatcher={sd}")

if sd and sd.exists():
    t = sd.read_text(encoding="utf-8", errors="replace")
    for key in (
        "ShouldSkipLaneDuringBootstrap",
        "IsGameReady",
        "IsOriginShiftBootstrapLocked",
        "PriorityLayer.Player",
        "RunDispatcherLateFrame",
        "BootstrapPresence",
        "IsBootstrapActive",
    ):
        lines.append(f"SD {key} count={t.count(key)}")
    # extract ShouldSkipLaneDuringBootstrap method
    m = re.search(
        r"(private|public|internal|protected).{0,40}ShouldSkipLaneDuringBootstrap[\s\S]{0,1200}?^\s*}",
        t,
        re.M,
    )
    if m:
        lines.append("---ShouldSkipLaneDuringBootstrap---")
        lines.append(m.group(0)[:1500])
    else:
        # looser
        idx = t.find("ShouldSkipLaneDuringBootstrap")
        if idx >= 0:
            lines.append("---ShouldSkip CTX---")
            lines.append(t[idx : idx + 800])

# 2) BootstrapState PublishGameReady / IsGameReady
for name in ("BootstrapState.cs", "BootstrapStatus.cs"):
    cands = list(Path("Assets").rglob(name))
    lines.append(f"{name} cands={[str(c) for c in cands[:5]]}")
    for c in cands[:1]:
        bt = c.read_text(encoding="utf-8", errors="replace")
        for key in ("PublishGameReady", "IsGameReady", "PublishBootstrapPresence", "IsBootstrap"):
            lines.append(f"  {key} count={bt.count(key)}")
        for key in ("PublishGameReady", "IsGameReady", "PublishBootstrapPresence"):
            idx = 0
            n = 0
            while n < 3:
                j = bt.find(key, idx)
                if j < 0:
                    break
                lines.append(f"---{c.name} {key}@{j}---")
                lines.append(bt[max(0, j - 80) : j + 350])
                idx = j + len(key)
                n += 1

# 3) Floating origin lock
fo_cands = list(Path("Assets").rglob("*FloatingOrigin*.cs"))
lines.append(f"FO cands={[str(c) for c in fo_cands[:8]]}")
for c in fo_cands[:3]:
    ft = c.read_text(encoding="utf-8", errors="replace")
    if "IsOriginShiftBootstrapLocked" in ft or "BootstrapLocked" in ft:
        lines.append(f"FO file {c}")
        idx = ft.find("IsOriginShiftBootstrapLocked")
        if idx < 0:
            idx = ft.find("BootstrapLocked")
        lines.append(ft[max(0, idx - 100) : idx + 500])

# 4) Runner: dilation application + TryMarkEcologyReady body
rt = Path(r"Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs").read_text(
    encoding="utf-8"
)
idx = rt.find("private void TryMarkEcologyReady()")
lines.append("---TryMarkEcologyReady---")
lines.append(rt[idx : idx + 1200] if idx >= 0 else "MISSING")

# dilation
for key in ("timeScale", "Time.timeScale", "dilation", "RequestDilation", "ApplyDilation", "_timeDilation"):
    if key.lower() in rt.lower() or key in rt:
        pass
for i, l in enumerate(rt.splitlines(), 1):
    if any(
        k in l
        for k in (
            "dilation",
            "Dilation",
            "timeScale",
            "Time.timeScale",
            "simulatedSeconds",
            "_daySeconds",
            "daySeconds",
        )
    ):
        lines.append(f"R{i}:{l}")

# 5) log: extract all HEADLESS and GameBootstrapper and dispatcher lines
logp = Path(r"Docs/AgentLogs/headless_smoke_20260730_p0_gameready.log")
if logp.exists():
    log = logp.read_text(encoding="utf-8", errors="replace")
    lines.append(f"LOG size={len(log)}")
    keep = []
    for i, line in enumerate(log.splitlines(), 1):
        s = line.strip()
        if not s:
            continue
        if s.startswith("UnityEngine.") or s.startswith("System.") or s.startswith("(Filename:"):
            continue
        if any(
            k in s
            for k in (
                "[HEADLESS]",
                "[GameBootstrapper",
                "dispatcher",
                "GameReady",
                "OriginShift",
                "origin",
                "ecology",
                "Ecology",
                "BOOTSTRAP",
                "lane",
                "Dilation",
                "dilation",
                "FailAndQuit",
                "IsGameReady",
                "SkipLane",
                "BootstrapLocked",
                "timeScale",
                "FrostTick",
                "LateFrame",
                "ColdTick",
            )
        ):
            keep.append(f"{i}:{s[:220]}")
    lines.append(f"log_hits={len(keep)}")
    lines.extend(keep[-120:])

out.write_text("\n".join(lines), encoding="utf-8")
print("WROTE", out, "lines", len(lines))
