import subprocess
import time
from pathlib import Path

cwd = Path(r"C:\hades\Hecton8")
out = []

def run(args, shell=False):
    r = subprocess.run(args, capture_output=True, text=True, shell=shell)
    out.append("CMD " + (args if isinstance(args, str) else " ".join(str(a) for a in args)))
    out.append(r.stdout or "")
    if r.stderr:
        out.append("STDERR: " + r.stderr)
    out.append(f"rc={r.returncode}")
    out.append("")
    return r

unity_candidates = [
    r"C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe",
]
unity = None
for c in unity_candidates:
    if Path(c).exists():
        unity = c
        break
if unity is None:
    hub = Path(r"C:\Program Files\Unity\Hub\Editor")
    if hub.exists():
        for p in sorted(hub.glob("*/Editor/Unity.exe"), reverse=True):
            unity = str(p)
            break

out.append(f"UNITY={unity}")

for name in ["Unity.exe", "UnityCrashHandler64.exe", "UnityCrashHandler32.exe"]:
    run(["taskkill", "/F", "/IM", name])

time.sleep(2)
run('tasklist /FI "IMAGENAME eq Unity.exe"', shell=True)

log_path = cwd / "Docs" / "AgentLogs" / "h8_playprobe_v0_L18.log"
log_path.parent.mkdir(parents=True, exist_ok=True)
if log_path.exists():
    bak = log_path.with_name("h8_playprobe_v0_L18.log.bak_prev")
    try:
        if bak.exists():
            bak.unlink()
        log_path.rename(bak)
        out.append(f"renamed old log to {bak}")
    except Exception as e:
        out.append(f"rename old log failed: {e}")

if not unity:
    out.append("FATAL: Unity not found")
else:
    args = [
        unity,
        "-batchmode",
        "-projectPath", str(cwd),
        "-executeMethod", "Hecton8.EditorTools.Diagnostics.H8_HeadlessPlayModeProbe.Run",
        "-h8StartGame", "1",
        "-h8TimeoutSeconds", "900",
        "-h8MenuSeconds", "120",
        "-h8SettleSeconds", "180",
        "-h8GameplaySeconds", "90",
        "-logFile", str(log_path),
    ]
    out.append("LAUNCH: " + " ".join(args))
    creationflags = subprocess.DETACHED_PROCESS | subprocess.CREATE_NEW_PROCESS_GROUP
    proc = subprocess.Popen(
        args,
        cwd=str(cwd),
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        creationflags=creationflags,
        close_fds=True,
    )
    out.append(f"PID={proc.pid}")
    time.sleep(5)
    run('tasklist /FI "IMAGENAME eq Unity.exe"', shell=True)
    out.append(
        f"log_exists={log_path.exists()} size={log_path.stat().st_size if log_path.exists() else 0}"
    )

out_path = Path(r"C:\hades\Hecton8\Tools\_cline_scratch\l18_launch_out.txt")
out_path.write_text("\n".join(out), encoding="utf-8")
print("\n".join(out))
