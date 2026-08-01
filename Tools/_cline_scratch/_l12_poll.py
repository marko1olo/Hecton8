# -*- coding: utf-8 -*-
"""L12 poll helper"""
import json
import os
import re
import subprocess
import time

REPO = r"C:\hades\Hecton8"
LOG = os.path.join(REPO, "Docs", "AgentLogs", "h8_playprobe_v0_L12.log")
PIDF = os.path.join(REPO, "Tools", "_cline_scratch", "v0_L12_pid.txt")
OUT = os.path.join(REPO, "Tools", "_cline_scratch", "_l12_poll_out.txt")
STATE = os.path.join(REPO, "Tools", "_cline_scratch", "_l12_poll_state.json")

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
        json.dump({"ts": time.strftime("%Y-%m-%d %H:%M:%S"), "log_bytes": log_sz, "pid": pid, "alive": bool(alive)}, open(STATE, "w", encoding="utf-8"), indent=2)
    except Exception:
        pass
    ts = time.strftime("%Y-%m-%d %H:%M:%S")
    lines.append(f"time={ts}")
    lines.append(f"pid={pid} alive={alive}")
    grow_s = stagnant if prev is not None else "n/a-first"
    lines.append(f"log_exists={log_exists} log_bytes={log_sz} prev={prev} growing={growing} stagnant={grow_s}")
    counts = {m: 0 for m in MARKERS}
    intent = None
    lom = None
    hops = set()
    hop2 = False
    swim_fail = 0
    swim_pass = 0
    result_fail = None
    depth_span = None
    samples = []
    if log_exists and log_sz > 0:
        with open(LOG, "rb") as fh:
            if log_sz > 2000000:
                fh.seek(-2000000, os.SEEK_END)
            data = fh.read().decode("utf-8", errors="replace")
        for m in MARKERS:
            counts[m] = data.lower().count(m.lower())
        for m in re.finditer("movementIntent01max=([0-9.]+)", data):
            intent = float(m.group(1))
        for m in re.finditer("lastOverrideMove=[(]([^)]+)[)]", data):
            lom = m.group(1)
        for m in re.finditer("readHop=([0-9]+)", data):
            hops.add(int(m.group(1)))
        hop2 = (2 in hops) or ("hop2" in data.lower())
        swim_fail = len(re.findall("MOMENT +FAIL +Swim|FAIL +Swim", data))
        swim_pass = len(re.findall("MOMENT +PASS +Swim|PASS +Swim", data))
        m = re.search("RESULT failures=([0-9]+)", data)
        if m:
            result_fail = int(m.group(1))
        m = re.search("span=([0-9.]+)m", data)
        if m:
            depth_span = float(m.group(1))
        key_re = re.compile("(movementIntent01max|INPUTHOP|hop2|Swim|FAIL|PASS|depth|lastOverride|PHASE|menu|readHop|RESULT|MOMENT)", re.I)
        for ln in data.splitlines():
            if key_re.search(ln):
                samples.append(ln[:300])
        samples = samples[-40:]
    lines.append(f"intent01max={intent} lastOverrideMove={lom} depth_span={depth_span}")
    lines.append(f"readHop_seen={sorted(hops)} hop2_present={hop2} Swim_FAIL={swim_fail} Swim_PASS={swim_pass} RESULT_failures={result_fail}")
    lines.append("MARKER_COUNTS:")
    for m, c in counts.items():
        if c:
            lines.append(f"  {m}: {c}")
    lines.append("SAMPLES_TAIL:")
    for s in samples:
        lines.append("  " + s)
    text = "".join([chr(10)])[0].join(lines) if False else chr(10).join(lines) + chr(10)
    with open(OUT, "w", encoding="utf-8") as fh:
        fh.write(text)
    print("=== L12 POLL COMPACT ===")
    print(f"ts={ts} pid={pid} alive={alive}")
    print(f"log_bytes={log_sz} prev={prev} growing={growing} stagnant={grow_s}")
    print(f"intent01max={intent} lastOverrideMove={lom} depth_span={depth_span}")
    print(f"readHop_seen={sorted(hops)} hop2_present={hop2} Swim_FAIL={swim_fail} Swim_PASS={swim_pass} RESULT_failures={result_fail}")
    for m, c in counts.items():
        if c:
            print(f"  marker {m}: {c}")
    print(f"WROTE {OUT}")
    return 0


if __name__ == "__main__":
    try:
        main()
    except Exception as e:
        print(f"POLL_ERR {type(e).__name__}: {e}")
    raise SystemExit(0)
