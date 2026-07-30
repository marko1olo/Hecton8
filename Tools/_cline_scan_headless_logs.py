# scratch - do not commit
from pathlib import Path

root = Path(r"C:\hades\Hecton8")
logs = [
    root / "Docs/AgentLogs/headless_run3.log",
    root / "Docs/AgentLogs/headless_run2.log",
    root / "Docs/AgentLogs/headless_run_unity.log",
]
keys = (
    "error CS",
    "error:",
    "ECOLOGY",
    "BATCH_TIMEOUT",
    "HEADLESS",
    "DebrisManager",
    "REFUSED",
    "Exception",
    "Bootstrap",
    "timeout",
    "IsInitialized",
    "EnsurePlayerSector",
    "HeadlessSimulation",
    "COMPILE",
    "error CS0",
    "Scripts have compiler errors",
    "waiting for dispatcher",
    "ecology",
    "FailAndQuit",
    "[HEADLESS]",
    "BOOTSTRAP_TIMEOUT",
    "DISPATCHER",
    "GHOST",
)
for p in logs:
    print("=" * 80)
    print(p.name, "exists" if p.exists() else "MISSING", "size", p.stat().st_size if p.exists() else 0)
    if not p.exists():
        continue
    text = p.read_text(encoding="utf-8", errors="replace")
    lines = text.splitlines()
    print("lines", len(lines))
    hits = []
    for i, line in enumerate(lines, 1):
        low = line.lower()
        if any(k.lower() in low for k in keys):
            hits.append((i, line[:300]))
    print("hits", len(hits))
    # first 20 and last 60
    show = hits[:20] + ([("...", "...")] if len(hits) > 80 else []) + hits[-60:]
    for i, line in show:
        print(f"{i}:{line}")
