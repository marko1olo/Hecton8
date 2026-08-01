# -*- coding: utf-8 -*-
"""Poll L09 probe progress; extract key needles when log grows."""
import os
import re
import subprocess
import time

REPO = r"C:\hades\Hecton8"
LOG = os.path.join(REPO, "Docs", "AgentLogs", "h8_playprobe_v0_L09.log")
ART = os.path.join(REPO, "Docs", "AgentLogs", "h8_playprobe_v0_L09.json")
PIDF = os.path.join(REPO, "Tools", "_cline_scratch", "v0_L09_pid.txt")
OUT = os.path.join(REPO, "Tools", "_cline_scratch", "_l09_poll_out.txt")
SHOTS = os.path.join(REPO, "Docs", "Screenshots", "V0_Playtest")

NEEDLES = [
    r"INPUTHOP",
    r"movementIntent",
    r"STARTERGRANT",
    r"IsToolAvailable",
    r"RESULT",
    r"Swim",
    r"publishOk",
    r"readHop",
    r"refusalMask",
    r"0x1E",
    r"CanService",
    r"STORAGE",
    r"SaveLoad",
    r"fauna",
    r"death",
    r"PNG",
    r"screenshot",
    r"V0-S0",
    r"SampleGameplay",
    r"FixedTick",
    r"tool.?slot",
    r"available=",
    r"MOMENT",
    r"LocomotionHold",
    r"EXCEPTION",
    r"Error",
    r"ABORT",
]


def proc_alive(pid):
    p = subprocess.run(
        ["tasklist", "/FI", f"PID eq {pid}", "/NH"],
        capture_output=True,
        text=True,
    )
    return str(pid) in (p.stdout or "")


def main():
    lines = []
    pid = None
    if os.path.isfile(PIDF):
        try:
            pid = int(open(PIDF, encoding="utf-8").read().strip())
        except Exception:
            pid = None
    alive = proc_alive(pid) if pid else False
    log_sz = os.path.getsize(LOG) if os.path.isfile(LOG) else 0
    art_sz = os.path.getsize(ART) if os.path.isfile(ART) else 0
    shots = []
    if os.path.isdir(SHOTS):
        shots = sorted(os.listdir(SHOTS))

    lines.append(f"time={time.strftime('%Y-%m-%d %H:%M:%S')}")
    lines.append(f"pid={pid} alive={alive}")
    lines.append(f"log_bytes={log_sz} art_bytes={art_sz}")
    lines.append(f"shots={len(shots)} {shots[:20]}")

    hits = {n: 0 for n in NEEDLES}
    samples = []
    if log_sz > 0:
        # read tail up to 1.5MB
        with open(LOG, "rb") as fh:
            if log_sz > 1_500_000:
                fh.seek(-1_500_000, os.SEEK_END)
            data = fh.read().decode("utf-8", errors="replace")
        for n in NEEDLES:
            ms = list(re.finditer(n, data, re.I))
            hits[n] = len(ms)
        # last matching lines for key patterns
        key_re = re.compile(
            r"(INPUTHOP|movementIntent|STARTERGRANT|RESULT|publishOk|readHop|refusalMask|IsToolAvailable|available=|Swim|SaveLoad|V0-S0|EXCEPTION)",
            re.I,
        )
        for ln in data.splitlines():
            if key_re.search(ln):
                samples.append(ln[:300])
        samples = samples[-40:]

    lines.append("HITS:")
    for n, c in sorted(hits.items(), key=lambda x: -x[1]):
        if c:
            lines.append(f"  {n}: {c}")
    lines.append("SAMPLES_TAIL:")
    lines.extend("  " + s for s in samples)

    text = "\n".join(lines) + "\n"
    with open(OUT, "w", encoding="utf-8") as fh:
        fh.write(text)
    print(text)
    return 0 if alive else 1


if __name__ == "__main__":
    raise SystemExit(main())
