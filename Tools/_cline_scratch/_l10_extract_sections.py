# -*- coding: utf-8 -*-
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
p = r"C:\hades\Hecton8\Tools\_cline_scratch\_l10_inspect_out.txt"
lines = open(p, encoding="utf-8").read().splitlines()
out = r"C:\hades\Hecton8\Tools\_cline_scratch\_l10_sections_idx.txt"
idx = []
for i, l in enumerate(lines):
    if l.startswith("===") or l.startswith("--- hit") or l.startswith("=== HPM"):
        idx.append(f"{i}:{l[:160]}")
open(out, "w", encoding="utf-8").write("\n".join(idx) + "\n")
print("count", len(idx))
for x in idx:
    print(x)
