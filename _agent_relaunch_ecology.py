# -*- coding: utf-8 -*-
"""Relaunch headless ecology smoke after FO lock-drain fix (411715153)."""
from __future__ import annotations

import json
import subprocess
import time
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(r"C:\hades\Hecton8")
LOG_DIR = ROOT / "Docs" / "AgentLogs"
UNITY = Path(r"C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe")
STAMP = datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")
LOG = LOG_DIR / f"headless_smoke_20260731_p0_fo_lock_drain_{STAMP}.log"
RESULT = LOG_DIR / "HeadlessSimulationResult_HEADLESS_SIMULATION_RUNNER.json"
STATUS = LOG_DIR / "HeadlessSimulationBatchRunner_HEADLESS_SIMULATION_RUNNER.txt"
OUT = ROOT / "_agent_relaunch_ecology_out.txt"
log: list[str] = []


def flush() -> None:
    OUT.write_text("\n".join(log) + "\n", encoding="utf-8")


def run(args: list[str], check: bool = False) -> subprocess.CompletedProcess:
    p = subprocess.run(
        args,
        cwd=str(ROOT),
        text=True,
        capture_output=True,
        encoding="utf-8",
        errors="replace",
    )
    log.append("$ " + " ".join(args))
    if p.stdout:
        log.append(p.stdout.rstrip())
    if p.stderr:
        log.append(p.stderr.rstrip())
    log.append(f"exit={p.returncode}")
    flush()
    if check and p.returncode != 0:
        raise SystemExit(f"fail {args} -> {p.returncode}")
    return p


log.append(f"HEAD={run(['git', 'rev-parse', 'HEAD']).stdout.strip()}")
log.append(f"status={run(['git', 'status', '-sb']).stdout.strip()}")
log.append(f"UNITY exists={UNITY.exists()}")
flush()

# stop prior headless unity if any (do not kill interactive editor blindly — only batchmode via tasklist filter later)
# Clear stale result so poll cannot read previous BATCH_TIMEOUT as current.
if RESULT.exists():
    bak = RESULT.with_suffix(RESULT.suffix + f".bak_pre_fo_drain_{STAMP}")
    RESULT.replace(bak)
    log.append(f"backed up result -> {bak.name}")
if STATUS.exists():
    STATUS.unlink()
    log.append("cleared runner status")
flush()

cmd = [
    str(UNITY),
    "-batchmode",
    "-projectPath",
    str(ROOT),
    "-logFile",
    str(LOG),
    "-nographics",
    "-executeMethod",
    "Hecton8.QA.Headless.Editor.HeadlessSimulationBatchRunner.Run",
    "-h8headless",
    "-h8headlessDays",
    "5",
    "-h8headlessDaySeconds",
    "60",
    "-h8headlessStartupTimeout",
    "600",
]
log.append("LAUNCH " + " ".join(cmd))
flush()

proc = subprocess.Popen(
    cmd,
    cwd=str(ROOT),
    stdout=subprocess.DEVNULL,
    stderr=subprocess.DEVNULL,
)
meta = {
    "pid": proc.pid,
    "log": str(LOG),
    "result": str(RESULT),
    "startedUtc": datetime.now(timezone.utc).isoformat(),
    "head": run(["git", "rev-parse", "HEAD"]).stdout.strip(),
}
(ROOT / "_agent_smoke_meta.json").write_text(
    json.dumps(meta, indent=2), encoding="utf-8"
)
log.append(f"LAUNCHED pid={proc.pid}")
log.append(f"log={LOG}")
log.append("SMOKE_LAUNCHED")
flush()
print("SMOKE_LAUNCHED", proc.pid, LOG)
