import os
import subprocess
import time
from pathlib import Path

os.chdir(r"C:\hades\Hecton8")
out = Path(r"C:\hades\Hecton8\_agent_status_probe_out.txt")
lines = []

def p(msg=""):
    lines.append(str(msg))

p("utc " + time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()))
try:
    h = subprocess.check_output(["git", "rev-parse", "HEAD"], text=True).strip()
    p("rev " + h)
    st = subprocess.check_output(["git", "status", "-sb"], text=True).strip()
    p("status " + st)
    logl = subprocess.check_output(["git", "log", "-5", "--oneline"], text=True).strip()
    p("log\n" + logl)
except Exception as e:
    p("git err " + repr(e))

try:
    tl = subprocess.check_output(["tasklist", "/FI", "IMAGENAME eq Unity.exe"], text=True, errors="replace")
    p("unity\n" + tl)
except Exception as e:
    p("tasklist err " + repr(e))

log = Path(r"Docs/AgentLogs/headless_smoke_20260731_p0_ecology_ready_20260731_014953.log")
res = Path(r"Docs/AgentLogs/HeadlessSimulationResult_HEADLESS_SIMULATION_RUNNER.json")
csv = Path(r"Docs/AgentLogs/HeadlessSimulationDaily_HEADLESS_SIMULATION_RUNNER.csv")
poll = Path(r"_agent_poll_ecology_stdout.txt")
meta = Path(r"_agent_relaunch_meta.txt")

for label, path in [("log", log), ("res", res), ("csv", csv), ("poll", poll), ("meta", meta)]:
    if path.exists():
        st = path.stat()
        p(f"{label}_exists sz={st.st_size} mtime={time.ctime(st.st_mtime)}")
    else:
        p(f"{label}_missing")

if meta.exists():
    p("META\n" + meta.read_text(encoding="utf-8", errors="replace")[:1000])

if csv.exists():
    t = csv.read_text(encoding="utf-8", errors="replace")
    p("csv_lines " + str(len(t.splitlines())))
    p(t[:800])

if res.exists():
    p("RESULT\n" + res.read_text(encoding="utf-8", errors="replace")[:2000])

if poll.exists():
    pl = poll.read_text(encoding="utf-8", errors="replace").splitlines()
    p("poll_lines " + str(len(pl)))
    p("---poll_tail---")
    p("\n".join(pl[-20:]))

if log.exists():
    text = log.read_text(encoding="utf-8", errors="replace")
    keys = [
        "[HEADLESS]", "ecology ready", "ecology wait", "BOOTSTRAP",
        "fail exitCode", "complete exitCode", "error CS", "GameReady",
        "BATCH_TIMEOUT", "runtime lanes",
    ]
    for k in keys:
        p(f"hit[{k}]={text.count(k)}")
    hl = [ln for ln in text.splitlines() if "[HEADLESS]" in ln]
    p("HEADLESS_COUNT " + str(len(hl)))
    for ln in hl[-30:]:
        p(ln)
    # tail of log
    tail = text.splitlines()[-40:]
    p("---log_tail---")
    for ln in tail:
        p(ln[:300])

out.write_text("\n".join(lines), encoding="utf-8")
print("WROTE", out, "bytes", out.stat().st_size)
