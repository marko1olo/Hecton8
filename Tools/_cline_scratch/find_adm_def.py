# -*- coding: utf-8 -*-
from pathlib import Path
root = Path(r"C:\hades\Hecton8\Assets\_Project\Scripts")
for p in root.rglob("*.cs"):
    text = p.read_text(encoding="utf-8", errors="replace")
    if "static class AwaitableDebtMonitor" in text or "class AwaitableDebtMonitor" in text:
        print("DEF", p)
        lines = text.splitlines()
        for i, line in enumerate(lines):
            if "AwaitableDebtMonitor" in line and "class" in line:
                start = max(0, i - 2)
                end = min(len(lines), i + 120)
                for j in range(start, end):
                    print(f"{j+1}|{lines[j]}")
                break
