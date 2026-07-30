# -*- coding: utf-8 -*-
import time
from pathlib import Path

ROOT = Path(r"C:\hades\Hecton8")
out = ROOT / "Tools" / "_cline_geo_retshape_v3_out.txt"
run = ROOT / "Tools" / "_cline_geo_retshape_v3_run.txt"
res = ROOT / "Docs" / "AgentLogs" / "HeadlessSimulationResult_HEADLESS_SIMULATION_RUNNER.json"
log = ROOT / "Logs" / "headless_ecology_fence_5day.log"

for i in range(24):
    print(
        f"poll{i} out={out.exists()} run_sz={run.stat().st_size if run.exists() else 0} "
        f"log_sz={log.stat().st_size if log.exists() else 0} res={res.exists()}",
        flush=True,
    )
    if out.exists():
        print(out.read_text(encoding="utf-8", errors="replace")[-3000:])
        break
    time.sleep(15)
else:
    print("TIMEOUT_WAIT")
    if run.exists():
        print(run.read_text(encoding="utf-8", errors="replace")[-800:])
