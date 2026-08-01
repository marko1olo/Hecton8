# -*- coding: utf-8 -*-
import os
import subprocess
import time

REPO = r"C:\hades\Hecton8"
OUT = os.path.join(REPO, "Tools", "_cline_scratch", "_lock_check_out.txt")
lines = []


def log(s=""):
    lines.append(str(s))
    print(s)


lock = os.path.join(REPO, "Temp", "UnityLockfile")
log(f"lock exists={os.path.isfile(lock)}")
if os.path.isfile(lock):
    log(f"lock size={os.path.getsize(lock)} mtime={time.ctime(os.path.getmtime(lock))}")
    try:
        log("lock content=" + open(lock, encoding="utf-8", errors="replace").read()[:500])
    except Exception as e:
        log(f"lock read err={e}")

p = subprocess.run(
    [
        "powershell",
        "-NoProfile",
        "-Command",
        "Get-CimInstance Win32_Process -Filter \"Name = 'Unity.exe' OR Name = 'UnityHub.exe'\" | "
        "Select-Object ProcessId,Name,CommandLine | Format-List | Out-String -Width 400",
    ],
    capture_output=True,
    text=True,
)
log("PROCS:")
log((p.stdout or "")[:4000])
log((p.stderr or "")[:500])

st = os.path.join(REPO, "Tools", "_cline_scratch", "v0_L09_launch_status.txt")
if os.path.isfile(st):
    log("STATUS:\n" + open(st, encoding="utf-8", errors="replace").read())

# also check for leftover L08 pid
for name in ("v0_L08_pid.txt", "v0_L09_pid.txt", "v0_L07_pid.txt"):
    path = os.path.join(REPO, "Tools", "_cline_scratch", name)
    if os.path.isfile(path):
        log(f"{name}={open(path,encoding='utf-8',errors='replace').read().strip()}")

with open(OUT, "w", encoding="utf-8") as fh:
    fh.write("\n".join(lines) + "\n")
log(f"WROTE {OUT}")
