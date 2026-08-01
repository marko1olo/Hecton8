# -*- coding: utf-8 -*-
"""L15 poll helper — hop2 + movementIntent01max + depth after dual-register heal."""
import json
import os
import re
import subprocess
import time

REPO = r"C:\hades\Hecton8"
LOG = os.path.join(REPO, "Docs", "AgentLogs", "h8_playprobe_v0_L15.log")
PIDF = os.path.join(REPO, "Tools", "_cline_scratch", "v0_L15_pid.txt")
OUT = os.path.join(REPO, "Tools", "_cline_scratch", "_l15_poll_out.txt")
STATE = os.path.join(REPO, "Tools", "_cline_scratch", "_l15_poll_state.json")

MARKERS = [
    "movementIntent01max",
    "INPUTHOP",
    "hop2",
    "Swim",
    "FAIL",
    "PASS",
    "depth",
    "lastOverride",
    "PHASE",
    "menu",
    "currentStateMove",
    "readHop",
]


def proc_alive(pid):
    try:
        r = subprocess.run(
            ["tasklist", "/FI", f"PID eq {pid}", "/NH"],
            capture_output=True,
            text=True,
        )
        return str(pid) in (r.stdout or "")
    except Exception:
        return False


def main():
    lines = []
    pid = None
    if os.path.isfile(PIDF):
        try:
            pid = int(open(PIDF, encoding="utf-8").read().strip())
        except Exception:
            pid = None
    alive = proc_alive(pid) if pid else False
    log_exists = os.path.isfile(LOG)
    log_sz = os.path.getsize(LOG) if log_exists else 0
    prev = None
    if os.path.isfile(STATE):
        try:
            prev = json.load(open(STATE, encoding="utf-8")).get("log_bytes")
        except Exception:
            prev = None
    growing = prev is not None and log_sz > prev
    stagnant = prev is not None and log_sz == prev and log_sz > 0
    try:
        json.dump(
            {
                "ts": time.strftime("%Y-%m-%d %H:%M:%S"),
                "log_bytes": log_sz,
                "pid": pid,
                "alive": bool(alive),
            },
            open(STATE, "w", encoding="utf-8"),
            indent=2,
        )
    except Exception:
        pass
    ts = time.strftime("%Y-%m-%d %H:%M:%S")
    lines.append(f"time={ts}")
    lines.append(f"pid={pid} alive={alive}")
    grow_s = stagnant if prev is not None else "n/a-first"
    lines.append(
        f"log_exists={log_exists} log_bytes={log_sz} prev={prev} growing={growing} stagnant={grow_s}"
    )
    counts = {m: 0 for m in MARKERS}
    intent = None
    lom = None
    csm = None
    hops = set()
    hop2 = False
    swim_fail = 0
    swim_pass = 0
    result_fail = None
    depth_span = None
    immersion = None
    samples = []
    if log_exists and log_sz > 0:
        with open(LOG, "rb") as fh:
            if log_sz > 2500000:
                fh.seek(-2500000, os.SEEK_END)
            data = fh.read().decode("utf-8", errors="replace")
        for m in MARKERS:
            counts[m] = data.lower().count(m.lower())
        for m in re.finditer(r"movementIntent01max=([0-9.]+)", data):
            intent = float(m.group(1))
        for m in re.finditer(r"lastOverrideMove=[(]([^)]+)[)]", data):
            lom = m.group(1)
        for m in re.finditer(r"currentStateMove=[(]([^)]+)[)]", data):
            csm = m.group(1)
        for m in re.finditer(r"readHop=([0-9]+)", data):
            hops.add(int(m.group(1)))
        hop2 = (2 in hops) or ("hop2" in data.lower() and "hop2 absent" not in data.lower())
        # explicit ABSENT token
        if re.search(r"hop2\s*[:=]?\s*ABSENT|hop2\s+absent|readHop=1\b(?!.*readHop=2)", data, re.I):
            if 2 not in hops:
                hop2 = False
        swim_fail = len(re.findall(r"MOMENT\s+FAIL\s+Swim|FAIL\s+Swim", data))
        swim_pass = len(re.findall(r"MOMENT\s+PASS\s+Swim|PASS\s+Swim", data))
        m = re.search(r"RESULT failures=([0-9]+)", data)
        if m:
            result_fail = int(m.group(1))
        m = re.search(r"span=([0-9.]+)m", data)
        if m:
            depth_span = float(m.group(1))
        m = re.search(r"immersionMax=([0-9.]+)", data)
        if m:
            immersion = float(m.group(1))
        key_re = re.compile(
            r"(movementIntent01max|INPUTHOP|hop2|Swim|FAIL|PASS|depth|lastOverride|PHASE|menu|readHop|RESULT|MOMENT|currentStateMove|immersion)",
            re.I,
        )
        for ln in data.splitlines():
            if key_re.search(ln):
                samples.append(ln[:320])
        samples = samples[-50:]
    lines.append(
        f"intent01max={intent} lastOverrideMove={lom} currentStateMove={csm} depth_span={depth_span} immersionMax={immersion}"
    )
    lines.append(
        f"readHop_seen={sorted(hops)} hop2_present={hop2} Swim_FAIL={swim_fail} Swim_PASS={swim_pass} RESULT_failures={result_fail}"
    )
    # verdict draft
    if intent is not None and hop2 and intent > 0:
        verdict = "LIKELY_INTENT_OK"
    elif hop2 and (intent is None or intent == 0):
        verdict = "HOP2_OK_INTENT_ZERO"
    elif not hop2 and intent is not None and intent > 0:
        verdict = "INTENT_OK_HOP2_ABSENT"
    elif result_fail is not None or swim_fail or swim_pass:
        verdict = "ROUTE_HAS_VERDICT"
    else:
        verdict = "IN_PROGRESS"
    lines.append(f"verdict_draft={verdict}")
    lines.append("MARKER_COUNTS:")
    for m, c in counts.items():
        if c:
            lines.append(f"  {m}: {c}")
    lines.append("SAMPLES_TAIL:")
    for s in samples:
        lines.append("  " + s)
    text = "\n".join(lines) + "\n"
    with open(OUT, "w", encoding="utf-8") as fh:
        fh.write(text)
    print("=== L15 POLL COMPACT ===")
    print(f"ts={ts} pid={pid} alive={alive}")
    print(f"log_bytes={log_sz} prev={prev} growing={growing} stagnant={grow_s}")
    print(
        f"intent01max={intent} lastOverrideMove={lom} currentStateMove={csm} depth_span={depth_span} immersionMax={immersion}"
    )
    print(
        f"readHop_seen={sorted(hops)} hop2_present={hop2} Swim_FAIL={swim_fail} Swim_PASS={swim_pass} RESULT_failures={result_fail}"
    )
    print(f"verdict_draft={verdict}")
    if samples:
        print("--- last samples ---")
        for s in samples[-12:]:
            print(s[:240])
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
