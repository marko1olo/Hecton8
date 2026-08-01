# -*- coding: utf-8 -*-
import os

paths = [
    r"C:\hades\Hecton8\Assets\_Project\Scripts\HectonPlayerMovement.cs",
    r"C:\hades\Hecton8\Assets\_Project\Scripts\Core\SystemDispatcher.cs",
    r"C:\hades\Hecton8\Assets\_Project\Scripts\Editor\Diagnostics\H8_HeadlessWorldDriver.cs",
    r"C:\hades\Hecton8\Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs",
]
slices = {
    paths[0]: [
        (1890, 1920),
        (2970, 3000),
        (5460, 5520),
        (8120, 8250),
        (9920, 9960),
        (10100, 10140),
        (13890, 13930),
        (14415, 14445),
    ],
    paths[1]: [(5260, 5330), (6170, 6280), (7100, 7130)],
    paths[2]: [(1620, 1650), (2640, 2710)],
    paths[3]: [(3145, 3180)],
}
out = r"C:\hades\Hecton8\Tools\_cline_scratch\_l14_code_slices.txt"
with open(out, "w", encoding="utf-8") as w:
    for p, rs in slices.items():
        w.write("==== " + p + " ====\n")
        if not os.path.isfile(p):
            w.write("MISSING\n")
            continue
        lines = open(p, encoding="utf-8", errors="replace").read().splitlines()
        for a, b in rs:
            w.write("--- %d-%d ---\n" % (a, b))
            for i in range(a - 1, min(b, len(lines))):
                w.write("%d|%s\n" % (i + 1, lines[i]))
print("OK", out, os.path.getsize(out))
