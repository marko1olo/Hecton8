# -*- coding: utf-8 -*-
from pathlib import Path
import re

root = Path(r"C:\hades\Hecton8")
targets = [
    root / "Assets/_Project/Scripts/GlobalRegistry.cs",
    root / "Assets/_Project/Scripts/Core/GlobalRegistry.cs",
]
# find GlobalRegistry file
for p in (root / "Assets").rglob("GlobalRegistry.cs"):
    print("FOUND", p)

# search assignment patterns narrowly
assign_re = re.compile(
    r"(RegisterService.*Dispatcher|TryRegister.*Dispatcher|SetService.*Dispatcher|_dispatcher\s*=|Dispatcher\s*=\s*[^?;]|TickDispatcher\s*=)",
    re.I,
)
hits = []
for p in (root / "Assets/_Project/Scripts").rglob("*.cs"):
    text = p.read_text(encoding="utf-8", errors="replace")
    name = p.name
    if name in ("GlobalRegistry.cs",) or "TickDispatcher" in name or "GameBootstrapper" in name or "Bootstrap" in str(p):
        for i, line in enumerate(text.splitlines(), 1):
            if assign_re.search(line) or "RegisterService" in line and "Dispatcher" in line:
                hits.append(f"{p.relative_to(root)}:{i}:{line.strip()[:220]}")

# also any file that assigns to dispatcher field via GlobalRegistry API
api_re = re.compile(r"GlobalRegistry\.(Register|Set|Bind|TrySet|TryRegister)\w*\(.*Dispatcher")
for p in (root / "Assets/_Project/Scripts").rglob("*.cs"):
    text = p.read_text(encoding="utf-8", errors="replace")
    for i, line in enumerate(text.splitlines(), 1):
        if api_re.search(line) or ("RegisterService" in line and "Dispatcher" in line):
            h = f"{p.relative_to(root)}:{i}:{line.strip()[:220]}"
            if h not in hits:
                hits.append(h)

out = root / "Tools/_cline_scratch/disp_owner.txt"
out.write_text("\n".join(hits), encoding="utf-8")
print("hits", len(hits))
for h in hits[:80]:
    print(h)

# AwaitableDebtMonitor
for p in (root / "Assets").rglob("*AwaitableDebtMonitor*"):
    print("ADM", p)
for p in (root / "Assets").rglob("*TickDispatcher*.cs"):
    print("TD", p)
