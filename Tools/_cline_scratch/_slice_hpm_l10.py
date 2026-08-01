# -*- coding: utf-8 -*-
p = r"C:\hades\Hecton8\Assets\_Project\Scripts\HectonPlayerMovement.cs"
out = r"C:\hades\Hecton8\Tools\_cline_scratch\_hpm_slices_l10.txt"
lines = open(p, encoding="utf-8", errors="replace").read().splitlines()
ranges = [
    (5466, 5520),
    (8080, 8125),
    (8130, 8235),
    (9921, 9975),
    (4760, 4820),
]
with open(out, "w", encoding="utf-8") as f:
    for a, b in ranges:
        f.write("\n=== %d-%d ===\n" % (a, b))
        for i in range(a, min(b, len(lines)) + 1):
            f.write("%5d|%s\n" % (i, lines[i - 1]))
print("wrote", out, "lines", len(lines))
