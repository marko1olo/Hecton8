# -*- coding: utf-8 -*-
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
t = open(
    r"C:\hades\Hecton8\Tools\_cline_scratch\_l10_hpm_core_out.txt", encoding="utf-8"
).read().splitlines()
out = []
for i, l in enumerate(t):
    if (
        l.startswith("=====")
        or l.startswith("FOUND")
        or l.startswith("SAMPLE")
        or l.startswith("LH ")
        or l.startswith("INPUT")
        or l.startswith("INV ")
        or l.startswith("PTM ")
        or l.startswith("L09")
        or l.startswith("--- ")
    ):
        out.append(f"{i}:{l[:200]}")
    elif l.startswith("LOG ") and any(
        k in l
        for k in (
            "readHop",
            "movementIntent",
            "STARTER",
            "refusal",
            "Locomotion",
            "waitingOn",
            "publishOk",
            "FAIL",
            "PASS",
            "SWIM",
            "TOOL",
            "intent",
        )
    ):
        out.append(f"{i}:{l[:220]}")

path = r"C:\hades\Hecton8\Tools\_cline_scratch\_l10_idx2_out.txt"
open(path, "w", encoding="utf-8").write("\n".join(out) + "\n")
print(path, len(out))
for x in out:
    print(x)
