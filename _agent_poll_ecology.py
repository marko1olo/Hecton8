# -*- coding: utf-8 -*-
"""Poll headless ecology smoke until result JSON or timeout."""
import json
import os
import sys
import time
from pathlib import Path

REPO = Path(r"C:\hades\Hecton8")
os.chdir(REPO)

RESULT = REPO / "Docs" / "AgentLogs" / "HeadlessSimulationResult_HEADLESS_SIMULATION_RUNNER.json"
META = REPO / "_agent_relaunch_meta.txt"
OUT = REPO / "_agent_poll_ecology_out.txt"

max_wait = int(sys.argv[1]) if len(sys.argv) > 1 else 900
interval = int(sys.argv[2]) if len(sys.argv) > 2 else 20

meta = META.read_text(encoding="utf-8") if META.exists() else ""
log = None
pid = None
for line in meta.splitlines():
    if line.startswith("log="):
        log = Path(line[4:].strip())
    if line.startswith("pid="):
        try:
            pid = int(line[4:].strip())
        except ValueError:
            pass

start = time.time()
lines_out = []


def emit(msg):
    print(msg, flush=True)
    lines_out.append(msg)


emit(f"poll start max_wait={max_wait}s interval={interval}s pid={pid} log={log}")

while True:
    elapsed = time.time() - start
    unity_alive = False
    if pid:
        try:
            import ctypes

            k = ctypes.windll.kernel32
            h = k.OpenProcess(0x1000, False, pid)  # PROCESS_QUERY_LIMITED_INFORMATION
            if h:
                code = ctypes.c_ulong()
                if k.GetExitCodeProcess(h, ctypes.byref(code)):
                    unity_alive = code.value == 259  # STILL_ACTIVE
                k.CloseHandle(h)
        except Exception as e:
            emit(f"pid check err {e}")

    result_exists = RESULT.exists()
    log_size = log.stat().st_size if log and log.exists() else 0
    hits = {}
    tail = ""
    if log and log.exists():
        try:
            t = log.read_text(encoding="utf-8", errors="replace")
            for key in [
                "error CS",
                "[HEADLESS]",
                "ecology ready",
                "ecology wait clock armed",
                "ecology wait progress",
                "BOOTSTRAP_TIMEOUT",
                "runtime lanes registered",
                "GameReady",
                "Compilation failed",
                "fail exitCode",
                "complete exitCode",
            ]:
                hits[key] = t.count(key)
            tail = "\n".join(t.splitlines()[-15:])
        except OSError as e:
            emit(f"log read err {e}")

    status_line = f"t={elapsed:.0f}s unity_alive={unity_alive} result={result_exists} log_sz={log_size} hits={hits}"
    emit(status_line)

    if result_exists:
        try:
            raw = RESULT.read_text(encoding="utf-8")
            emit("RESULT_RAW " + raw[:2000])
            data = json.loads(raw)
            emit("RESULT_JSON " + json.dumps(data, indent=2)[:2000])
            st = data.get("status", "")
            days = data.get("ecologySampledDays", 0)
            dil = data.get("timeDilationDelivered", 0)
            dod_ok = (
                st not in ("ECOLOGY_UNAVAILABLE", "BATCH_TIMEOUT", "BOOTSTRAP_TIMEOUT", "[BOOTSTRAP_TIMEOUT]", "[ECOLOGY_UNAVAILABLE]")
                and "BOOTSTRAP_TIMEOUT" not in str(st)
                and "ECOLOGY_UNAVAILABLE" not in str(st)
                and "BATCH_TIMEOUT" not in str(st)
                and int(days or 0) > 0
                and float(dil or 0) > 0
            )
            emit(f"DoD={'PASS' if dod_ok else 'FAIL'} status={st} ecologySampledDays={days} timeDilationDelivered={dil}")
        except Exception as e:
            emit(f"result parse err {e}")
        break

    if elapsed >= max_wait:
        emit("POLL_TIMEOUT")
        emit("---LOG_TAIL---")
        emit(tail)
        break

    if not unity_alive and elapsed > 60 and not result_exists:
        emit("UNITY_DEAD_NO_RESULT")
        emit("---LOG_TAIL---")
        emit(tail)
        # keep waiting a bit more for file flush
        if elapsed > 90:
            break

    time.sleep(interval)

OUT.write_text("\n".join(lines_out) + "\n", encoding="utf-8")
emit(f"wrote {OUT}")
