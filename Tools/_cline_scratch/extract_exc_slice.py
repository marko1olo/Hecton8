# -*- coding: utf-8 -*-
import os
import sys

ROOT = r"C:\hades\Hecton8"
LOG = os.path.join(ROOT, "Docs", "AgentLogs", "h8_playprobe_v0_L06.log")
OUT = os.path.join(ROOT, "Tools", "_cline_scratch", "v0_L06_exc_slice.txt")
BOOT_ERR = os.path.join(ROOT, "Tools", "_cline_scratch", "v0_L06_boot_errors.txt")

def main():
    print("LOG_EXISTS", os.path.isfile(LOG), LOG)
    print("ROOT_EXISTS", os.path.isdir(ROOT))
    if not os.path.isfile(LOG):
        # locate any playprobe logs
        al = os.path.join(ROOT, "Docs", "AgentLogs")
        if os.path.isdir(al):
            for n in sorted(os.listdir(al)):
                if "playprobe" in n.lower() or "L06" in n:
                    p = os.path.join(al, n)
                    print("FOUND", n, os.path.getsize(p))
        sys.exit(2)

    with open(LOG, "r", encoding="utf-8", errors="replace") as f:
        lines = f.read().splitlines()
    print("TOTAL_LINES", len(lines))

    start, end = 2070, 2280
    with open(OUT, "w", encoding="utf-8") as out:
        for i in range(start - 1, min(end, len(lines))):
            out.write("%d|%s\n" % (i + 1, lines[i]))
    print("WROTE_SLICE", OUT)

    # Also dump all OceanKinematics / NativeFault / Bootstrap dependency lines with context
    keys = (
        "OceanKinematics",
        "NativeFaultDumpWriter",
        "NativeMemoryTrackingBridge",
        "Bootstrap dependency",
        "Bootstrap phase failed",
        "HectonSeismicTideDirector",
        "CreateTransientPayload",
        "InvalidOperationException",
        "Environment",
    )
    hits = []
    for i, line in enumerate(lines):
        if any(k in line for k in keys):
            hits.append(i)

    # unique windows of +/- 8 lines around hits
    windows = set()
    blocks = []
    for hi in hits:
        lo = max(0, hi - 8)
        hi2 = min(len(lines), hi + 12)
        key = (lo, hi2)
        if key in windows:
            continue
        windows.add(key)
        blocks.append((lo, hi2))

    err_path = os.path.join(ROOT, "Tools", "_cline_scratch", "v0_L06_exc_full.txt")
    with open(err_path, "w", encoding="utf-8") as out:
        out.write("HIT_COUNT %d UNIQUE_WINDOWS %d\n\n" % (len(hits), len(blocks)))
        for lo, hi2 in blocks[:40]:
            out.write("===== LINES %d-%d =====\n" % (lo + 1, hi2))
            for i in range(lo, hi2):
                out.write("%d|%s\n" % (i + 1, lines[i]))
            out.write("\n")
    print("WROTE_FULL", err_path, "hits", len(hits), "windows", len(blocks))

    # print slice to stdout
    print("---SLICE---")
    with open(OUT, "r", encoding="utf-8") as f:
        sys.stdout.write(f.read())

if __name__ == "__main__":
    main()
