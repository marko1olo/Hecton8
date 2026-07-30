# scratch scanner - do not commit
from pathlib import Path

files = [
    Path(r"Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs"),
    Path(r"Assets/_Project/Scripts/QA/Headless/Editor/HeadlessSimulationBatchRunner.cs"),
    Path(r"Tools/RunUnityBatchGate.py"),
    Path(r"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs"),
    Path(r"Assets/_Project/Scripts/Gameplay/DebrisManager.cs"),
]
keys = (
    "ecology", "Ecology", "ECOLOGY", "TryMark", "timeDil", "Fauna",
    "Debris", "BATCH", "Sampled", "heartbeat", "ExecuteDaily",
    "h8headless", "TimeDilation", "EnsureRuntime", "EXEMPT",
    "ReportDebris", "ColdTick", "RegisterRuntime",
)
root = Path(r"C:\hades\Hecton8")
for rel in files:
    p = root / rel
    print("=" * 80)
    print(p, "exists" if p.exists() else "MISSING", "lines", sum(1 for _ in p.open(encoding="utf-8", errors="replace")) if p.exists() else 0)
    if not p.exists():
        continue
    for i, line in enumerate(p.open(encoding="utf-8", errors="replace"), 1):
        if any(k in line for k in keys):
            print(f"{i}:{line.rstrip()}")
