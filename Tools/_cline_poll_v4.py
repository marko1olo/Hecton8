# -*- coding: utf-8 -*-
"""Poll v4 geology sweep until out file exists or timeout."""
from __future__ import annotations
import time
from pathlib import Path

ROOT = Path(r"C:\hades\Hecton8")
RUN = ROOT / "Tools" / "_cline_geo_retshape_v4_run.txt"
OUT = ROOT / "Tools" / "_cline_geo_retshape_v4_out.txt"

def tail(p: Path, n: int = 25) -> str:
    if not p.exists():
        return "(missing)"
    lines = p.read_text(encoding="utf-8", errors="replace").splitlines()
    return "\n".join(lines[-n:])

for i in range(120):  # up to ~60 min at 30s
    out_ok = OUT.exists() and OUT.stat().st_size > 200
    run_sz = RUN.stat().st_size if RUN.exists() else 0
    print(f"poll{i} out={out_ok} run_sz={run_sz}", flush=True)
    print(tail(RUN, 12), flush=True)
    print("---", flush=True)
    if out_ok and "BEST2048" in OUT.read_text(encoding="utf-8", errors="replace"):
        print("DONE", flush=True)
        print(tail(OUT, 40), flush=True)
        break
    time.sleep(30)
else:
    print("TIMEOUT waiting for v4", flush=True)
    print(tail(RUN, 30), flush=True)
