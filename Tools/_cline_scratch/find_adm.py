# -*- coding: utf-8 -*-
from pathlib import Path
root = Path(r"C:\hades\Hecton8\Assets")
for p in root.rglob("*AwaitableDebt*"):
    print("ADM", p)
for p in root.rglob("*SystemDispatcher*"):
    print("SD", p)
# also Shinobu38 wait pattern
for p in root.rglob("Shinobu38QaWatchdogRuntime.cs"):
    print("W", p)
