# -*- coding: utf-8 -*-
from pathlib import Path

out = Path(r"C:/hades/Hecton8/Docs/L19_VISION_NEXT_SCAN.txt")
lines = []
for name in ("VISION_LOCKS.md", "PROJECT_BIBLES.md"):
    p = Path(r"C:/hades/Hecton8") / name
    if not p.exists():
        lines.append("MISSING " + name)
        continue
    t = p.read_text(encoding="utf-8", errors="replace")
    lines.append("==== " + name + " ====")
    for i, l in enumerate(t.splitlines()[:200], 1):
        lines.append("%d|%s" % (i, l[:220]))
    lines.append("---MARK---")
    for i, l in enumerate(t.splitlines(), 1):
        s = l.strip()
        up = s.upper()
        if (
            s.startswith("- [ ]")
            or s.startswith("* [ ]")
            or "NEXT" in up[:80]
            or "P0" in up
            or "P1" in up
            or "UNLOCK" in up
            or "GAP" in up
            or "TODO" in up[:40]
            or "SHIP" in up[:40]
        ):
            lines.append("%d|%s" % (i, s[:240]))

out.write_text("\n".join(lines), encoding="utf-8")
print("wrote", str(out), "n", len(lines), "bytes", out.stat().st_size)
