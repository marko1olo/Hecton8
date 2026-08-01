# -*- coding: utf-8 -*-
"""Deep extract L09 playprobe markers (exclude Burst DLL noise)."""
import os
import re

REPO = r"C:\hades\Hecton8"
LOG = os.path.join(REPO, "Docs", "AgentLogs", "h8_playprobe_v0_L09.log")
OUT = os.path.join(REPO, "Tools", "_cline_scratch", "_l09_deep_out.txt")

PATTERNS = [
    r"INPUTHOP",
    r"movementIntent",
    r"STARTERGRANT",
    r"IsToolAvailable",
    r"\[H8_Headless",
    r"H8_HeadlessPlayModeProbe",
    r"publishOk",
    r"publishGuard",
    r"readHop",
    r"refusalMask",
    r"0x1E",
    r"CanService",
    r"STORAGE",
    r"SaveLoad",
    r"Swim\b",
    r"LocomotionHold",
    r"MOMENT",
    r"V0-S0",
    r"screenshot",
    r"PNG",
    r"tool.?slot",
    r"available\s*=",
    r"available=",
    r"gridBound",
    r"inventoryVersion",
    r"fauna",
    r"death",
    r"ABORT",
    r"Exception",
    r"NullReference",
    r"RESULT\s*[:=]",
    r"PASS|FAIL|BLOCKED|PARTIAL",
    r"switchToPlayer",
    r"inputEnabled",
    r"blockMask",
    r"postMaskMove",
    r"CurrentMovement",
    r"FixedTick",
    r"SampleGameplay",
    r"TryRecover",
    r"vault",
    r"grant",
]


def is_noise(line: str) -> bool:
    l = line.lower()
    if "burstcache" in l or "symtype" in l:
        return True
    if "pdb:" in l and "dll" in l:
        return True
    if re.search(r"size:\s*\d+\s*\(result:", l):
        return True
    if "mono_crash" in l:
        return True
    return False


def main():
    if not os.path.isfile(LOG):
        open(OUT, "w", encoding="utf-8").write("LOG MISSING\n")
        print("LOG MISSING")
        return

    size = os.path.getsize(LOG)
    lines_out = [f"log_bytes={size}", f"path={LOG}"]
    compiled = [(p, re.compile(p, re.I)) for p in PATTERNS]
    hits = {p: [] for p in PATTERNS}

    # stream full file
    with open(LOG, "r", encoding="utf-8", errors="replace") as fh:
        for i, line in enumerate(fh, 1):
            if is_noise(line):
                continue
            for p, rx in compiled:
                if rx.search(line):
                    if len(hits[p]) < 30:
                        hits[p].append(f"L{i}: {line.rstrip()[:350]}")
                    elif len(hits[p]) == 30:
                        hits[p].append(f"... more truncated for {p}")

    lines_out.append("=== HIT COUNTS (non-noise) ===")
    for p, items in sorted(hits.items(), key=lambda x: -len([i for i in x[1] if not i.startswith("...")])):
        n = len([i for i in items if not i.startswith("...")])
        if n:
            lines_out.append(f"{p}: {n}")

    lines_out.append("\n=== KEY SAMPLES ===")
    priority = [
        "INPUTHOP",
        "movementIntent",
        "STARTERGRANT",
        "IsToolAvailable",
        "publishOk",
        "readHop",
        "refusalMask",
        "0x1E",
        "Swim\\b",
        "LocomotionHold",
        "SaveLoad",
        "STORAGE",
        "gridBound",
        "inventoryVersion",
        "V0-S0",
        "screenshot",
        "PASS|FAIL|BLOCKED|PARTIAL",
        "RESULT\\s*[:=]",
        "Exception",
        "NullReference",
        "ABORT",
        "H8_HeadlessPlayModeProbe",
        "\\[H8_Headless",
        "grant",
        "available=",
        "tool.?slot",
        "fauna",
        "death",
        "postMaskMove",
        "switchToPlayer",
        "blockMask",
    ]
    for p in priority:
        items = hits.get(p, [])
        if not items:
            continue
        lines_out.append(f"\n--- {p} ---")
        lines_out.extend(items[:20])

    # also grab last 80 non-noise lines containing H8 or probe keywords
    lines_out.append("\n=== TAIL PROBE LINES ===")
    tail = []
    with open(LOG, "r", encoding="utf-8", errors="replace") as fh:
        for i, line in enumerate(fh, 1):
            if is_noise(line):
                continue
            if re.search(r"H8_|probe|INPUTHOP|STARTER|Swim|RESULT|FAIL|PASS|BLOCKED|movementIntent|grant", line, re.I):
                tail.append(f"L{i}: {line.rstrip()[:350]}")
    lines_out.extend(tail[-80:])

    text = "\n".join(lines_out) + "\n"
    with open(OUT, "w", encoding="utf-8") as fh:
        fh.write(text)
    print(text[:15000])
    print(f"\nWROTE {OUT} total_chars={len(text)}")


if __name__ == "__main__":
    main()
