# -*- coding: utf-8 -*-
from pathlib import Path
root = Path(r"C:\hades\Hecton8\Assets\_Project\Scripts")
needle = "class AwaitableDebtMonitor"
needle2 = "NextFrameAsync"
hits = []
for p in root.rglob("*.cs"):
    text = p.read_text(encoding="utf-8", errors="replace")
    if "AwaitableDebtMonitor" in text:
        for i, line in enumerate(text.splitlines(), 1):
            if "AwaitableDebtMonitor" in line or (needle2 in line and "static" in line):
                hits.append(f"{p.name}:{i}:{line.strip()[:200]}")
out = Path(r"C:\hades\Hecton8\Tools\_cline_scratch\adm_hits.txt")
out.write_text("\n".join(hits), encoding="utf-8")
print("hits", len(hits))
for h in hits[:60]:
    print(h)
