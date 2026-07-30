from pathlib import Path
import time

out = Path(r"Tools/_cline_scratch/_scan_runner_out.txt")
lines = []

t = Path(r"Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs").read_text(encoding="utf-8")
for pat in [
    "_startupTimeoutSeconds",
    "TryMarkEcologyReady",
    "LateFrame",
    "timeDilation",
    "RegisterLane",
    "PriorityLayer",
    "ColdTick",
    "ecologyReady",
    "biomass",
    "_startupTime",
]:
    lines.append(f"== {pat} count {t.count(pat)}")

src_lines = t.splitlines()
for i, l in enumerate(src_lines, 1):
    if any(
        k in l
        for k in (
            "private void Update",
            "TryMarkEcologyReady",
            "LateFrame",
            "ColdTick",
            "_startupTimeout",
            "PriorityLayer.Player",
            "PriorityLayer.Environment",
            "RegisterRuntimeLanes",
            "timeDilation",
            "FailAndQuit",
            "TimeoutHash",
        )
    ):
        lines.append(f"{i}:{l}")

# print Update block fully
idx = t.find("private void Update()")
if idx >= 0:
    lines.append("---UPDATE_BLOCK---")
    lines.append(t[idx : idx + 900])

# LateFrame registration area
for key in ("PriorityLayer.Player", "RegisterLane", "OnLateFrame", "LateFrameTick"):
    j = 0
    while True:
        j = t.find(key, j)
        if j < 0:
            break
        lines.append(f"---CTX {key}@{j}---")
        lines.append(t[max(0, j - 120) : j + 200])
        j += len(key)

log = Path(r"Docs/AgentLogs/headless_smoke_20260730_p0_gameready.log")
if log.exists():
    st = log.stat()
    lines.append(f"LOG size={st.st_size} mtime={time.ctime(st.st_mtime)}")
else:
    lines.append("NO_LOG")

r = Path(r"Docs/AgentLogs/HeadlessSimulationResult_HEADLESS_SIMULATION_RUNNER.json")
lines.append("RESULT=" + (r.read_text(encoding="utf-8", errors="replace") if r.exists() else "NONE"))

# git show cementer what it committed
import subprocess

lines.append("HEAD=" + subprocess.check_output(["git", "rev-parse", "--short", "HEAD"], text=True).strip())
lines.append(
    "SHOWSTAT="
    + subprocess.check_output(
        ["git", "show", "--stat", "--oneline", "-1", "HEAD"], text=True, errors="replace"
    )
)
# product files in cementer?
diff = subprocess.check_output(
    ["git", "show", "--name-only", "--pretty=format:", "HEAD"], text=True, errors="replace"
)
lines.append("HEAD_FILES:")
lines.append(diff[:2000])

out.write_text("\n".join(lines), encoding="utf-8")
print("WROTE", out)
