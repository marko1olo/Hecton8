# -*- coding: utf-8 -*-
import pathlib
import sys

root = pathlib.Path(r"C:\hades\Hecton8")
out = root / r"Tools\_cline_scratch\_l17_slices.txt"
parts = []

def dump(rel, ranges, label_prefix):
    p = root / rel
    if not p.exists():
        parts.append(f"MISSING {rel}")
        return
    lines = p.read_text(encoding="utf-8", errors="replace").splitlines()
    parts.append(f"==== {label_prefix} {rel} lines={len(lines)} ====")
    for start, end in ranges:
        parts.append(f"---- {start}-{end} ----")
        for i in range(start - 1, min(end, len(lines))):
            parts.append(f"{i+1}|{lines[i]}")

dump(
    r"Assets\_Project\Scripts\Core\SystemDispatcher.cs",
    [(5150, 5350), (1715, 1935), (1790, 1855), (7110, 7155), (6175, 6280)],
    "SD",
)
dump(
    r"Assets\_Project\Scripts\HectonFloatingOrigin.cs",
    [(300, 400), (1185, 1260), (1535, 1585), (2220, 2275)],
    "FO",
)
dump(
    r"Assets\_Project\Scripts\Editor\Diagnostics\H8_HeadlessPlayModeProbe.cs",
    [(760, 830), (3670, 3765)],
    "PR",
)
dump(
    r"Assets\_Project\Scripts\QA\Headless\HeadlessSimulationRunner.cs",
    [(270, 320), (815, 890)],
    "HSR",
)

out.write_text("\n".join(parts), encoding="utf-8")
print("WROTE", out, "bytes", out.stat().st_size, flush=True)
