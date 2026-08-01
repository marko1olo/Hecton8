import os

p = r"C:\hades\Hecton8\Assets\_Project\Scripts\HectonPlayerMovement.cs"
lines = open(p, encoding="utf-8", errors="replace").read().splitlines()
print("nlines", len(lines))

ranges = [
    (9910, 10050),
    (8000, 8230),
    (4740, 4820),
    (1740, 1780),
    (5460, 5490),
    (9760, 9780),
    (8060, 8090),
    (8450, 8540),
]
out = []
for a, b in ranges:
    out.append("===== %d-%d =====" % (a, b))
    for i in range(a - 1, min(b, len(lines))):
        out.append("%d|%s" % (i + 1, lines[i]))
    out.append("")

op2 = r"C:\hades\Hecton8\Tools\_cline_scratch\_l10_hpm_fixedtick.txt"
open(op2, "w", encoding="utf-8").write("\n".join(out))
print("wrote", op2, os.path.getsize(op2))
