# -*- coding: utf-8 -*-
from pathlib import Path
import re

root = Path(r"C:\hades\Hecton8\Assets\_Project\Scripts")
patterns = [
    r"Dispatcher\s*=",
    r"RegisterDispatcher",
    r"SetDispatcher",
    r"GlobalRegistry\.Dispatcher",
    r"TickDispatcher",
]
compiled = [re.compile(p) for p in patterns]

hits = []
for p in root.rglob("*.cs"):
    try:
        text = p.read_text(encoding="utf-8", errors="replace")
    except Exception:
        continue
    for i, line in enumerate(text.splitlines(), 1):
        if any(c.search(line) for c in compiled):
            if "GlobalRegistry.Dispatcher" in line or "RegisterDispatcher" in line or "SetDispatcher" in line or re.search(r"Dispatcher\s*=", line) or "TickDispatcher" in line:
                rel = p.relative_to(root.parent.parent.parent)
                hits.append(f"{rel}:{i}:{line.strip()[:200]}")

out = Path(r"C:\hades\Hecton8\Tools\_cline_scratch\dispatcher_hits.txt")
out.write_text("\n".join(hits), encoding="utf-8")
print("hits", len(hits))
# print most relevant
for h in hits:
    if any(k in h for k in ("Register", "SetDispatcher", "Dispatcher =", "Dispatcher=", "new ", "bootstrap", "Bootstrap", "Ensure")):
        print(h)
print("--- sample GlobalRegistry ---")
for h in hits:
    if "GlobalRegistry" in h:
        print(h)
