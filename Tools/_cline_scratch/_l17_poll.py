# -*- coding: utf-8 -*-
"""Poll L17 LIVE playmode probe log for FODRAIN / INPUTHOP / Swim gates."""
from __future__ import annotations

import json
import os
import re
import time
from pathlib import Path

ROOT = Path(r"C:\hades\Hecton8")
LOG = ROOT / r"Docs\AgentLogs\h8_playprobe_v0_L17.log"
PIDFILE = ROOT / r"Tools\_cline_scratch\v0_L17_pid.txt"
OUT = ROOT / r"Tools\_cline_scratch\_l17_poll_out.txt"
STATE = ROOT / r"Tools\_cline_scratch\_l17_poll_state.json"
ARTIFACT = ROOT / r"Docs\AgentLogs\h8_playprobe_v0_L17.json"

MARKERS = (
    "FODRAIN",
    "SIMCLOCK",
    "INPUTHOP",
    "movementIntent01max",
    "WORLDDRIVER",
    "SWIM",
    "VERDICT",
    "ROUTE_DONE",
    "PLAYPROBE DONE",
    "hop2",
    "lateFrameTick",
    "pumpFired",
    "presimTick",
    "gameReady",
    "error CS",
    "Exception",
)


def pid_alive(pid: int) -> bool:
    if pid <= 0:
        return False
    try:
        import ctypes

        kernel32 = ctypes.windll.kernel32  # type: ignore[attr-defined]
        SYNCHRONIZE = 0x00100000
        handle = kernel32.OpenProcess(SYNCHRONIZE, False, pid)
        if not handle:
            return False
        kernel32.CloseHandle(handle)
        return True
    except Exception:
        return False


def read_pid() -> int:
    try:
        return int(PIDFILE.read_text(encoding="utf-8").strip().split()[0])
    except Exception:
        return 0


def tail_hits(text: str, limit: int = 80) -> list[str]:
    hits: list[str] = []
    for line in text.splitlines():
        u = line
        if any(m in u for m in MARKERS):
            hits.append(u.rstrip()[:400])
    return hits[-limit:]


def extract_metrics(text: str) -> dict:
    m: dict = {}
    fod = re.findall(r"FODRAIN[^\n]*", text)
    m["fodrain_count"] = len(fod)
    m["fodrain_last"] = fod[-1][:300] if fod else ""
    # lock still held?
    fo_lock = re.findall(r"foLock=([01])", text)
    disp_boot = re.findall(r"dispBoot=([01])", text)
    m["foLock_last"] = fo_lock[-1] if fo_lock else ""
    m["dispBoot_last"] = disp_boot[-1] if disp_boot else ""
    m["foLock_any1"] = "1" in fo_lock
    m["dispBoot_any1"] = "1" in disp_boot
    m["foLock_all0_after"] = bool(fo_lock) and fo_lock[-1] == "0"
    m["dispBoot_all0_after"] = bool(disp_boot) and disp_boot[-1] == "0"

    sim = re.findall(r"SIMCLOCK[^\n]*", text)
    m["simclock_count"] = len(sim)
    m["simclock_last"] = sim[-1][:300] if sim else ""
    step = re.findall(r"stepBoundAfter=([01])", text)
    m["stepBoundAfter_any1"] = "1" in step

    hops = re.findall(r"INPUTHOP[^\n]*", text)
    m["inputhop_count"] = len(hops)
    m["inputhop_samples"] = hops[-5:] if hops else []

    late = [int(x) for x in re.findall(r"lateFrameTick=(\d+)", text)]
    pump = [int(x) for x in re.findall(r"pumpFired=(\d+)", text)]
    presim = [int(x) for x in re.findall(r"presimTick=(\d+)", text)]
    m["lateFrameTick_vals"] = late[-6:]
    m["pumpFired_vals"] = pump[-6:]
    m["presimTick_vals"] = presim[-6:]
    m["lateFrame_unfrozen"] = len(set(late)) > 1 if late else False
    m["pump_unfrozen"] = len(set(pump)) > 1 if pump else False

    hop2_abs = len(re.findall(r"hop2=ABSENT|hop2 ABSENT|hop2=0\b", text, re.I))
    hop2_pres = len(re.findall(r"hop2=PRESENT|hop2 PRESENT|hop2=1\b", text, re.I))
    # also Diag style
    hop2_pres += len(re.findall(r"\bhop2\b[^\n]{0,40}present", text, re.I))
    m["hop2_absent_hits"] = hop2_abs
    m["hop2_present_hits"] = hop2_pres

    intents = [float(x) for x in re.findall(r"movementIntent01max=([0-9.]+)", text)]
    m["movementIntent01max_vals"] = intents[-8:]
    m["movementIntent01max_max"] = max(intents) if intents else None

    swim = re.findall(r"(SWIM|Swim)[^\n]{0,120}", text)
    m["swim_lines"] = swim[-6:]
    verdict = re.findall(r"VERDICT[^\n]*", text)
    m["verdict_lines"] = verdict[-4:]
    done = re.findall(r"(ROUTE_DONE|PLAYPROBE DONE|Probe complete|phase=Done)[^\n]*", text, re.I)
    m["done_lines"] = done[-4:]
    return m


def main() -> None:
    pid = read_pid()
    alive = pid_alive(pid)
    log_exists = LOG.exists()
    size = LOG.stat().st_size if log_exists else 0
    text = ""
    if log_exists and size > 0:
        # read last ~2MB to keep parse fast
        with LOG.open("rb") as f:
            if size > 2_000_000:
                f.seek(-2_000_000, os.SEEK_END)
            raw = f.read()
        text = raw.decode("utf-8", errors="replace")

    metrics = extract_metrics(text) if text else {}
    hits = tail_hits(text) if text else []

    state = {
        "ts": time.strftime("%Y-%m-%d %H:%M:%S"),
        "pid": pid,
        "alive": alive,
        "log_exists": log_exists,
        "log_bytes": size,
        "artifact_exists": ARTIFACT.exists(),
        "metrics": metrics,
        "hit_tail": hits[-40:],
    }
    STATE.write_text(json.dumps(state, indent=2), encoding="utf-8")

    lines = [
        f"ts={state['ts']}",
        f"pid={pid} alive={alive}",
        f"log_bytes={size} artifact={state['artifact_exists']}",
        f"fodrain={metrics.get('fodrain_count')} foLock_last={metrics.get('foLock_last')} dispBoot_last={metrics.get('dispBoot_last')}",
        f"simclock={metrics.get('simclock_count')} stepBound1={metrics.get('stepBoundAfter_any1')}",
        f"inputhop={metrics.get('inputhop_count')} lateUnfrozen={metrics.get('lateFrame_unfrozen')} pumpUnfrozen={metrics.get('pump_unfrozen')}",
        f"late={metrics.get('lateFrameTick_vals')} pump={metrics.get('pumpFired_vals')}",
        f"hop2_present={metrics.get('hop2_present_hits')} hop2_absent={metrics.get('hop2_absent_hits')}",
        f"intent_max={metrics.get('movementIntent01max_max')} vals={metrics.get('movementIntent01max_vals')}",
        f"swim={metrics.get('swim_lines')}",
        f"verdict={metrics.get('verdict_lines')}",
        f"done={metrics.get('done_lines')}",
        "--- hits tail ---",
    ]
    lines.extend(hits[-25:])
    OUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print("\n".join(lines[:20]))
    if not alive and size > 0:
        print("PROCESS_EXITED")
    elif alive:
        print("STILL_RUNNING")
    else:
        print("NO_PROCESS_YET")


if __name__ == "__main__":
    main()
