# -*- coding: utf-8 -*-
import os
import subprocess
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
out = []
lock = r"C:\hades\Hecton8\Temp\UnityLockfile"
out.append("lock_exists " + str(os.path.exists(lock)))
if os.path.exists(lock):
    out.append("lock_size " + str(os.path.getsize(lock)))
    out.append("lock_mtime " + str(os.path.getmtime(lock)))
    try:
        out.append("lock_content " + repr(open(lock, "rb").read()[:200]))
    except Exception as e:
        out.append("lock_err " + str(e))

r = subprocess.run(
    ["tasklist", "/FI", "IMAGENAME eq Unity.exe", "/FO", "CSV", "/NH"],
    capture_output=True,
    text=True,
    encoding="cp1251",
    errors="replace",
)
out.append("UNITY_CSV:")
out.append(r.stdout or "(none)")
out.append(r.stderr or "")

r2 = subprocess.run(
    [
        "wmic",
        "process",
        "where",
        'name="Unity.exe"',
        "get",
        "ProcessId,CommandLine",
        "/FORMAT:LIST",
    ],
    capture_output=True,
    text=True,
    encoding="cp1251",
    errors="replace",
)
out.append("WMIC:")
out.append((r2.stdout or "")[:4000])

for pf in [
    r"C:\hades\Hecton8\Tools\_cline_scratch\v0_L14_pid.txt",
    r"C:\hades\Hecton8\Tools\_cline_scratch\v0_L13_pid.txt",
    r"C:\hades\Hecton8\Tools\_cline_scratch\v0_L12_pid.txt",
]:
    if os.path.exists(pf):
        out.append(pf + "=" + open(pf, encoding="utf-8", errors="replace").read().strip())

path = r"C:\hades\Hecton8\Tools\_cline_scratch\_l15_lock_check.txt"
open(path, "w", encoding="utf-8").write("\n".join(out))
print("WROTE", path)
print("\n".join(out)[:2000])
