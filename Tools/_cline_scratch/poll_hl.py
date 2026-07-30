# -*- coding: utf-8 -*-
from pathlib import Path
import time, subprocess

hl = Path(r"C:\hades\Hecton8\Docs\AgentLogs\headless_smoke_20260730_p0fix.log")
out = Path(r"C:\hades\Hecton8\Tools\_cline_scratch\hl_tail.txt")
lines = []
if hl.exists():
    st = hl.stat()
    lines.append(f"SIZE={st.st_size} MTIME={time.ctime(st.st_mtime)}")
    text = hl.read_text(encoding="utf-8", errors="replace").splitlines()
    lines.append(f"TOTAL_LINES={len(text)}")
    keys = (
        "short-circuit", "biomass", "Day ", "PASS", "FAIL", "Headless",
        "ecology", "Exception", "Exiting", "Complete", "SECTOR",
        "MarkMainMenu", "Bootstrap", "ERROR",
    )
    hits = [ln for ln in text if any(k in ln for k in keys)]
    lines.append(f"HIT_COUNT={len(hits)}")
    lines.append("---LAST_HITS---")
    lines.extend(hits[-40:])
    lines.append("---TAIL20---")
    lines.extend(text[-20:])
else:
    lines.append("NO_LOG")
try:
    r = subprocess.run(
        ["tasklist", "/FI", "IMAGENAME eq Unity.exe", "/FO", "CSV", "/NH"],
        capture_output=True, text=True, timeout=15,
    )
    lines.append("---UNITY---")
    lines.append(r.stdout.strip() or "(none)")
except Exception as e:
    lines.append(f"UNITY_ERR={e}")
out.write_text("\n".join(lines), encoding="utf-8")
print("ok", out)
