# -*- coding: utf-8 -*-
"""Background poll loop for ecology smoke. Writes snapshot every cycle."""
from __future__ import annotations

import json
import subprocess
import time
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(r"C:\hades\Hecton8")
LOG_DIR = ROOT / "Docs" / "AgentLogs"
RESULT = LOG_DIR / "HeadlessSimulationResult_HEADLESS_SIMULATION_RUNNER.json"
STATUS = LOG_DIR / "HeadlessSimulationBatchRunner_HEADLESS_SIMULATION_RUNNER.txt"
META = ROOT / "_agent_smoke_meta.json"
OUT = ROOT / "_agent_poll_loop_out.txt"
DONE = ROOT / "_agent_poll_loop_done.txt"

INTERVAL_S = 45
MAX_CYCLES = 80  # ~60 min


def snapshot(cycle: int) -> str:
    lines: list[str] = []
    lines.append(f"cycle={cycle} utc={datetime.now(timezone.utc).isoformat()}")
    if META.exists():
        try:
            meta = json.loads(META.read_text(encoding="utf-8"))
            lines.append(f"meta_pid={meta.get('pid')} head={meta.get('head')}")
            log_path = Path(meta.get("log", ""))
        except Exception as exc:
            lines.append(f"meta_err={exc!r}")
            log_path = None
    else:
        log_path = None
        lines.append("meta=MISSING")

    p = subprocess.run(
        ["tasklist", "/FI", "IMAGENAME eq Unity.exe", "/FO", "CSV", "/NH"],
        text=True,
        capture_output=True,
        encoding="utf-8",
        errors="replace",
    )
    unity_lines = [ln for ln in (p.stdout or "").splitlines() if "Unity.exe" in ln]
    lines.append(f"unity_count={len(unity_lines)}")
    for ln in unity_lines[:6]:
        lines.append("  " + ln.strip())

    lines.append(f"result_exists={RESULT.exists()}")
    terminal = False
    if RESULT.exists():
        raw = RESULT.read_text(encoding="utf-8", errors="replace").strip()
        lines.append("result=" + raw[:800])
        try:
            data = json.loads(raw)
            status = str(data.get("status", ""))
            days = data.get("ecologySampledDays", data.get("ecology_sampled_days"))
            dil = data.get("timeDilationDelivered", data.get("time_dilation_delivered"))
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
            lines.append(f"status={status} days={days} dil={dil} DOD_OK={dod_ok}")
            if dod_ok:
                lines.append("PASS")
                terminal = True
            elif status and status not in {"RUNNING", "STARTING", "IN_PROGRESS"}:
                # batch wrote a final status
                if any(
                    k in status
                    for k in (
                        "TIMEOUT",
                        "FAIL",
                        "ERROR",
                        "UNAVAILABLE",
                        "COMPLETE",
                        "OK",
                        "PASS",
                        "SUCCESS",
                    )
                ):
                    lines.append("TERMINAL_STATUS")
                    terminal = True
        except Exception as exc:
            lines.append(f"json_err={exc!r}")

    if STATUS.exists():
        lines.append(
            "runner_status="
            + STATUS.read_text(encoding="utf-8", errors="replace").strip()[:200]
        )

    # prefer meta log else latest drain log
    if log_path is None or not log_path.exists():
        logs = sorted(LOG_DIR.glob("headless_smoke_20260731_p0_fo_lock_drain_*.log"))
        log_path = logs[-1] if logs else None

    if log_path and log_path.exists():
        size = log_path.stat().st_size
        lines.append(f"log={log_path.name} size={size}")
        text = log_path.read_text(encoding="utf-8", errors="replace")
        keys = (
            "ecology",
            "Ecology",
            "GameReady",
            "BOOTSTRAP",
            "foLock",
            "physicsPause",
            "pendingScenes",
            "BATCH_TIMEOUT",
            "error CS",
            "EcologyWait",
            "wait-progress",
            "EcologyReady",
            "timeDilation",
            "HeadlessSimulation",
            "FrostTick",
            "OriginShiftBootstrap",
        )
        hits = []
        for i, line in enumerate(text.splitlines(), 1):
            if any(k in line for k in keys):
                hits.append(f"{i}|{line[:240]}")
        lines.append(f"key_hits={len(hits)}")
        for h in hits[-25:]:
            lines.append(h)
        tail = text.splitlines()[-15:]
        lines.append("=== tail ===")
        lines.extend(tail)

        # no unity and result exists => terminal
        if not unity_lines and RESULT.exists():
            lines.append("UNITY_GONE_WITH_RESULT")
            terminal = True
        if not unity_lines and size > 50000 and not RESULT.exists():
            lines.append("UNITY_GONE_NO_RESULT")
            terminal = True
    else:
        lines.append("log=NONE")

    return "\n".join(lines) + "\n", terminal


def main() -> None:
    for cycle in range(1, MAX_CYCLES + 1):
        body, terminal = snapshot(cycle)
        OUT.write_text(body, encoding="utf-8")
        if terminal:
            DONE.write_text(body + "\nDONE\n", encoding="utf-8")
            print("TERMINAL", cycle)
            return
        time.sleep(INTERVAL_S)
    body, _ = snapshot(MAX_CYCLES)
    DONE.write_text(body + "\nMAX_CYCLES\n", encoding="utf-8")
    print("MAX_CYCLES")


if __name__ == "__main__":
    main()
