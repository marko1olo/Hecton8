# -*- coding: utf-8 -*-
"""Poll headless ecology smoke DoD once (safe to re-run)."""
from __future__ import annotations

import json
import re
import subprocess
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(r"C:\hades\Hecton8")
LOG_DIR = ROOT / "Docs" / "AgentLogs"
RESULT = LOG_DIR / "HeadlessSimulationResult_HEADLESS_SIMULATION_RUNNER.json"
STATUS = LOG_DIR / "HeadlessSimulationBatchRunner_HEADLESS_SIMULATION_RUNNER.txt"
META = ROOT / "_agent_smoke_meta.json"
OUT = ROOT / "_agent_poll_ecology_out.txt"
lines: list[str] = []


def add(msg: str) -> None:
    lines.append(msg)


add(f"utc={datetime.now(timezone.utc).isoformat()}")
if META.exists():
    add("meta=" + META.read_text(encoding="utf-8").strip())
else:
    add("meta=MISSING")

# processes
p = subprocess.run(
    ["tasklist", "/FI", "IMAGENAME eq Unity.exe"],
    text=True,
    capture_output=True,
    encoding="utf-8",
    errors="replace",
)
add("=== unity procs ===")
add((p.stdout or p.stderr or "").strip())

# result
add(f"result_exists={RESULT.exists()}")
if RESULT.exists():
    raw = RESULT.read_text(encoding="utf-8", errors="replace")
    add("result_raw=" + raw.strip())
    try:
        data = json.loads(raw)
        status = str(data.get("status", ""))
        days = data.get("ecologySampledDays", data.get("ecology_sampled_days"))
        dil = data.get("timeDilationDelivered", data.get("time_dilation_delivered"))
        add(f"status={status}")
        add(f"ecologySampledDays={days}")
        add(f"timeDilationDelivered={dil}")
        bad = status in {
            "ECOLOGY_UNAVAILABLE",
            "BATCH_TIMEOUT",
            "BOOTSTRAP_TIMEOUT",
            "",
        }
        dod_ok = (
            not bad
            and days is not None
            and float(days) > 0
            and dil is not None
            and float(dil) > 0
        )
        add(f"DOD_OK={dod_ok}")
        if dod_ok:
            add("PASS")
        else:
            add("FAIL_OR_INCOMPLETE")
    except Exception as exc:
        add(f"json_err={exc!r}")

if STATUS.exists():
    add("runner_status=" + STATUS.read_text(encoding="utf-8", errors="replace").strip())

# find latest fo drain log
logs = sorted(LOG_DIR.glob("headless_smoke_20260731_p0_fo_lock_drain_*.log"))
if logs:
    latest = logs[-1]
    add(f"latest_log={latest.name} size={latest.stat().st_size}")
    text = latest.read_text(encoding="utf-8", errors="replace")
    keys = [
        "ecology",
        "Ecology",
        "GameReady",
        "BOOTSTRAP",
        "foLock",
        "physicsPause",
        "pendingScenes",
        "OriginShift",
        "BATCH_TIMEOUT",
        "error CS",
        "EcologyWait",
        "wait-progress",
        "CopyBootstrap",
        "TryFlush",
        "ecology ready",
        "EcologyReady",
        "timeDilation",
    ]
    hits = []
    for i, line in enumerate(text.splitlines(), 1):
        if any(k in line for k in keys):
            hits.append(f"{i}|{line[:300]}")
    add(f"key_hits={len(hits)}")
    for h in hits[-40:]:
        add(h)
    # tail
    tail = text.splitlines()[-30:]
    add("=== log_tail ===")
    add("\n".join(tail))
else:
    add("latest_log=NONE")

OUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
print("POLL_WROTE", OUT, "lines", len(lines))
