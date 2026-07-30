# -*- coding: utf-8 -*-
from pathlib import Path
import subprocess

ROOT = Path(r"C:\hades\Hecton8")
paths = [
    ROOT / "Tools" / "_cline_geo_retshape_v3_run.txt",
    ROOT / "Tools" / "_cline_geo_retshape_v3_out.txt",
    ROOT / "Logs" / "headless_ecology_fence_5day.log",
    ROOT / "Docs" / "AgentLogs" / "HeadlessSimulationResult_HEADLESS_SIMULATION_RUNNER.json",
]
for p in paths:
    print("=" * 20, p.name, "=" * 20)
    if not p.exists():
        print("MISSING")
        continue
    data = p.read_text(encoding="utf-8", errors="replace")
    print(f"size={len(data)}")
    print(data[-3500:] if len(data) > 3500 else data)

print("=" * 20, "PROCESSES", "=" * 20)
for name in ("Unity.exe", "python.exe"):
    r = subprocess.run(
        ["tasklist", "/FI", f"IMAGENAME eq {name}", "/NH"],
        capture_output=True, text=True, errors="replace"
    )
    print(r.stdout.strip() or f"{name}: none")
