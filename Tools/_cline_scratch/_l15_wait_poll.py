# -*- coding: utf-8 -*-
"""Poll L15 probe until dead or max wall time. Writes compact snapshots."""
import os
import subprocess
import sys
import time

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

REPO = r"C:\hades\Hecton8"
POLL = os.path.join(REPO, "Tools", "_cline_scratch", "_l15_poll.py")
PIDF = os.path.join(REPO, "Tools", "_cline_scratch", "v0_L15_pid.txt")
OUT = os.path.join(REPO, "Tools", "_cline_scratch", "_l15_wait_out.txt")
MAX_SEC = 900  # 15 min wall
INTERVAL = 45


def alive(pid):
    try:
        r = subprocess.run(
            ["tasklist", "/FI", f"PID eq {pid}", "/NH"],
            capture_output=True,
            text=True,
            encoding="cp1251",
            errors="replace",
        )
        return str(pid) in (r.stdout or "")
    except Exception:
        return False


def main():
    t0 = time.time()
    snaps = []
    pid = None
    if os.path.isfile(PIDF):
        try:
            pid = int(open(PIDF, encoding="utf-8").read().strip())
        except Exception:
            pid = None
    n = 0
    while True:
        n += 1
        elapsed = int(time.time() - t0)
        subprocess.run(
            [sys.executable, POLL],
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
        )
        compact = ""
        pout = os.path.join(REPO, "Tools", "_cline_scratch", "_l15_poll_out.txt")
        if os.path.isfile(pout):
            compact = open(pout, encoding="utf-8", errors="replace").read()
        is_alive = alive(pid) if pid else False
        header = f"\n===== SNAP n={n} elapsed={elapsed}s pid={pid} alive={is_alive} =====\n"
        snaps.append(header + compact)
        # keep last 8 snaps
        snaps = snaps[-8:]
        open(OUT, "w", encoding="utf-8").write("".join(snaps))
        print(header.strip())
        for line in compact.splitlines()[:12]:
            print(line)
        # exit conditions
        if not is_alive and n > 1:
            print("PROBE_DEAD")
            open(OUT, "a", encoding="utf-8").write("\nPROBE_DEAD\n")
            return 0
        if "RESULT failures=" in compact or "MOMENT FAIL Swim" in compact or "MOMENT PASS Swim" in compact:
            # keep going until process dies so full log flushes
            pass
        if elapsed >= MAX_SEC:
            print("MAX_WALL")
            open(OUT, "a", encoding="utf-8").write("\nMAX_WALL\n")
            return 1
        time.sleep(INTERVAL)


if __name__ == "__main__":
    raise SystemExit(main())
