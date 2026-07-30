# -*- coding: utf-8 -*-
from pathlib import Path
import subprocess

root = Path(r"C:\hades\Hecton8")
run = root / "Tools/_cline_geo_sweep_run.txt"
out = root / "Tools/_cline_geo_sweep_out.txt"
print("=== RUN TAIL ===")
if run.exists():
    lines = run.read_text(encoding="utf-8", errors="replace").splitlines()
    print(f"lines={len(lines)}")
    print("\n".join(lines[-40:]))
else:
    print("missing")
print("=== OUT MATCHES ===")
if out.exists():
    text = out.read_text(encoding="utf-8", errors="replace")
    for ln in text.splitlines():
        if any(k in ln for k in ("BEST", "PASSING", "cfg count", "ship2048", "none fully")):
            print(ln)
else:
    print("no out yet")
print("=== PYTHON PROCS ===")
p = subprocess.run(
    ["powershell", "-NoProfile", "-Command",
     "Get-Process python -ErrorAction SilentlyContinue | Select-Object Id,CPU,@{N='WS_MB';E={[int]($_.WS/1MB)}} | Format-Table -AutoSize | Out-String -Width 200"],
    capture_output=True, text=True, encoding="utf-8", errors="replace",
)
print(p.stdout or "none")
print("=== BG SWEEP LOG ===")
bg = Path(r"C:\Users\Admin\AppData\Local\Temp\cline\background-1785398166857-4sbd8ps.log")
if bg.exists():
    t = bg.read_text(encoding="utf-8", errors="replace")
    print(t[-1500:])
else:
    print("no bg")
