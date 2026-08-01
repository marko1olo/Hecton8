# -*- coding: utf-8 -*-
"""L16 LIVE probe poller. Prefer metric fields over last-match prose."""
from __future__ import annotations

import json
import os
import re
import sys
import time
from datetime import datetime, timezone

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

REPO = r"C:\hades\Hecton8"
LOG = os.path.join(REPO, r"Docs\AgentLogs\h8_playprobe_v0_L16.log")
ARTIFACT = os.path.join(REPO, r"Docs\AgentLogs\h8_playprobe_v0_L16.json")
STATUS = os.path.join(REPO, r"Tools\_cline_scratch\v0_L16_launch_status.txt")
PIDFILE = os.path.join(REPO, r"Tools\_cline_scratch\v0_L16_pid.txt")
OUT = os.path.join(REPO, r"Tools\_cline_scratch\_l16_poll_out.txt")
POLL_INTERVAL = 30
MAX_WAIT_SEC = 1200


def _read(path: str) -> str:
    try:
        with open(path, "r", encoding="utf-8", errors="replace") as f:
            return f.read()
    except OSError:
        return ""


def _pid_alive(pid: int) -> bool:
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


def _first(pat: str, text: str, flags=0) -> str | None:
    m = re.search(pat, text, flags)
    return m.group(0) if m else None


def _all(pat: str, text: str, flags=0) -> list[str]:
    return re.findall(pat, text, flags)


def summarize(text: str) -> dict:
    s: dict = {}
    s["log_bytes"] = len(text.encode("utf-8", errors="replace"))
    s["log_lines"] = text.count("\n") + (1 if text and not text.endswith("\n") else 0)

    # SIMCLOCK
    sim = _all(r"\[H8_PLAYPROBE\] SIMCLOCK ensure reason=(\S+)[^\n]*stepBoundAfter=(\d+)", text)
    s["simclock_count"] = len(sim)
    s["simclock_reasons"] = sorted({r for r, _ in sim})
    s["simclock_stepBoundAfter_any1"] = any(v == "1" for _, v in sim)
    s["simclock_last"] = (
        f"reason={sim[-1][0]} stepBoundAfter={sim[-1][1]}" if sim else None
    )

    # INPUTHOP census — hop2
    hop2_lines = [
        ln
        for ln in text.splitlines()
        if "INPUTHOP" in ln and ("hop=2" in ln or "hop2" in ln.lower() or "GetState" in ln)
    ]
    s["hop2_line_count"] = len(hop2_lines)
    s["hop2_sample"] = hop2_lines[:3] + (["..."] if len(hop2_lines) > 3 else [])
    # DiagRecordReadObservation hop index 2
    hop2_obs = _all(r"hop[=:]?\s*2\b|DiagRecordReadObservation\(2\)|readHop=2|INPUTHOP.*\b2\b", text)
    s["hop2_obs_hits"] = len(hop2_obs)

    # hop1
    hop1_lines = [ln for ln in text.splitlines() if "INPUTHOP" in ln and ("hop=1" in ln or "hop1" in ln.lower())]
    s["hop1_line_count"] = len(hop1_lines)

    # movementIntent01max — prefer RESULT / metric field, not prose
    intent_vals = []
    for m in re.finditer(r"movementIntent01max\s*[=:]\s*([0-9.]+)", text):
        try:
            intent_vals.append(float(m.group(1)))
        except ValueError:
            pass
    s["movementIntent01max_values"] = intent_vals[-10:]
    s["movementIntent01max_peak"] = max(intent_vals) if intent_vals else None

    # currentStateMove metric only (not help prose) — look for vector pattern after key
    csm_vals = []
    for m in re.finditer(r"currentStateMove\s*[=:]\s*\(\s*([-\d.]+)\s*,\s*([-\d.]+)\s*\)", text):
        try:
            csm_vals.append((float(m.group(1)), float(m.group(2))))
        except ValueError:
            pass
    s["currentStateMove_count"] = len(csm_vals)
    s["currentStateMove_nonzero"] = any(abs(x) > 1e-6 or abs(y) > 1e-6 for x, y in csm_vals)
    s["currentStateMove_last"] = csm_vals[-1] if csm_vals else None
    # first nonzero
    nz = next(((x, y) for x, y in csm_vals if abs(x) > 1e-6 or abs(y) > 1e-6), None)
    s["currentStateMove_first_nonzero"] = nz

    # RESULT lines
    results = [ln.strip() for ln in text.splitlines() if "RESULT" in ln and "H8_PLAYPROBE" in ln]
    if not results:
        results = [ln.strip() for ln in text.splitlines() if re.search(r"\bRESULT\b", ln)]
    s["result_lines"] = results[-5:]
    s["has_result"] = bool(results)

    # PASS/FAIL markers
    s["swim_pass_mention"] = bool(re.search(r"Swim\s*PASS|ROUTE_PASS|pass=1", text, re.I))
    s["fail_mention"] = bool(re.search(r"Swim\s*FAIL|ROUTE_FAIL|FAIL residual", text, re.I))

    # WorldDriver / FixedTick signals
    s["worlddriver_ticks"] = len(re.findall(r"WORLDDRIVER", text))
    s["fixedtick_mentions"] = len(re.findall(r"FixedTick|DispatchFixedStep|step-bounded|StepBounded", text, re.I))
    s["gameplay_window"] = bool(re.search(r"gameplay-window|GameplayWarmup|GAMEPLAY", text, re.I))

    # Compile errors
    s["compile_error"] = bool(re.search(r"error CS\d+|Scripts have compiler errors", text))

    return s


def main() -> int:
    started = time.time()
    last_bytes = -1
    rounds = 0
    while True:
        rounds += 1
        pid = 0
        try:
            pid = int(_read(PIDFILE).strip() or "0")
        except ValueError:
            pid = 0
        alive = _pid_alive(pid)
        text = _read(LOG)
        b = len(text.encode("utf-8", errors="replace"))
        summ = summarize(text) if text else {"log_bytes": 0, "log_lines": 0, "has_result": False}
        status = _read(STATUS).strip()
        art_exists = os.path.isfile(ARTIFACT)
        art_snip = ""
        if art_exists:
            try:
                raw = _read(ARTIFACT)
                art = json.loads(raw) if raw.strip() else {}
                # pull key metrics if present
                art_snip = {
                    k: art.get(k)
                    for k in (
                        "movementIntent01max",
                        "hop2",
                        "hop2Present",
                        "inputHop2",
                        "result",
                        "verdict",
                        "swim",
                        "pass",
                    )
                    if k in art
                }
                if not art_snip and isinstance(art, dict):
                    # shallow keys sample
                    art_snip = {"_keys": list(art.keys())[:40]}
            except Exception as e:
                art_snip = {"_parse_error": str(e)}

        elapsed = int(time.time() - started)
        report = {
            "ts": datetime.now(timezone.utc).isoformat(),
            "elapsed_sec": elapsed,
            "round": rounds,
            "pid": pid,
            "pid_alive": alive,
            "log_grew": b != last_bytes,
            "status_tail": status[-500:] if status else "",
            "summary": summ,
            "artifact_exists": art_exists,
            "artifact_snip": art_snip,
        }
        last_bytes = b

        # Verdict helpers
        peak = summ.get("movementIntent01max_peak")
        hop2_ok = (summ.get("hop2_line_count") or 0) > 0 or (summ.get("hop2_obs_hits") or 0) > 0
        clock_ok = bool(summ.get("simclock_stepBoundAfter_any1"))
        intent_ok = peak is not None and peak > 0
        done = bool(summ.get("has_result")) or (not alive and elapsed > 60 and b > 0)

        lines = [
            f"=== L16 POLL r={rounds} t={elapsed}s pid={pid} alive={alive} ===",
            f"log_bytes={summ.get('log_bytes')} lines={summ.get('log_lines')} grew={report['log_grew']}",
            f"SIMCLOCK count={summ.get('simclock_count')} stepBound1={clock_ok} last={summ.get('simclock_last')}",
            f"hop1_lines={summ.get('hop1_line_count')} hop2_lines={summ.get('hop2_line_count')} hop2_obs={summ.get('hop2_obs_hits')}",
            f"movementIntent01max_peak={peak} values_tail={summ.get('movementIntent01max_values')}",
            f"currentStateMove nonzero={summ.get('currentStateMove_nonzero')} first_nz={summ.get('currentStateMove_first_nonzero')} last={summ.get('currentStateMove_last')}",
            f"has_result={summ.get('has_result')} compile_err={summ.get('compile_error')}",
            f"result_lines={summ.get('result_lines')}",
            f"artifact={art_exists} snip={art_snip}",
            f"GATE clock_ok={clock_ok} hop2_ok={hop2_ok} intent_ok={intent_ok}",
            f"done={done}",
        ]
        body = "\n".join(lines) + "\n"
        with open(OUT, "w", encoding="utf-8") as f:
            f.write(body)
            f.write("\n--- raw_json ---\n")
            f.write(json.dumps(report, indent=2, ensure_ascii=False, default=str))
        print(body, flush=True)

        if done:
            # final exit code: 0 if hop2+intent, 2 if result but fail, 1 if no result
            if clock_ok and hop2_ok and intent_ok:
                print("LIVE_VERDICT=PASS_CANDIDATE hop2+intent+clock", flush=True)
                return 0
            if summ.get("has_result"):
                print("LIVE_VERDICT=FAIL_WITH_RESULT", flush=True)
                return 2
            print("LIVE_VERDICT=ENDED_NO_RESULT", flush=True)
            return 3

        if elapsed >= MAX_WAIT_SEC:
            print("LIVE_VERDICT=TIMEOUT", flush=True)
            return 4

        time.sleep(POLL_INTERVAL)


if __name__ == "__main__":
    raise SystemExit(main())
