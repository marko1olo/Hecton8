# -*- coding: utf-8 -*-
from pathlib import Path

LOG = Path(r"C:\hades\Hecton8\Docs\AgentLogs\h8_playprobe_v0_L17.log")
OUT = Path(r"C:\hades\Hecton8\Tools\_cline_scratch\_l17_compile_hits.txt")

t = LOG.read_text(encoding="utf-8", errors="replace") if LOG.exists() else ""
keys = (
    "error CS",
    "error:",
    "Exception",
    "FODRAIN",
    "DrainProbe",
    "Scripts have compiler",
    "Compilation failed",
    "executeMethod",
    "PLAYPROBE",
    "H8_PLAYPROBE",
    "SIMCLOCK",
)
out = []
for i, l in enumerate(t.splitlines()):
    low = l.lower()
    if any(k.lower() in low for k in keys):
        out.append(f"{i}:{l[:350]}")

OUT.write_text("\n".join(out[-120:]) + f"\n\nTOTAL={len(out)} bytes={len(t)}\n", encoding="utf-8")
print("TOTAL", len(out), "bytes", len(t))
for line in out[-40:]:
    print(line)
