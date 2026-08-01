# L17 log scan — scratch only, do not commit
from collections import Counter
from pathlib import Path

root = Path(r"C:\hades\Hecton8")
log = root / r"Docs\AgentLogs\h8_playprobe_v0_L16.log"
outp = root / r"Tools\_cline_scratch\_l17_loghits.txt"

lines = log.read_text(encoding="utf-8", errors="replace").splitlines()
print("lines", len(lines))

keys = (
    "SIMCLOCK",
    "INPUTHOP",
    "SimulationHalted",
    "STEP-BOUNDED",
    "SafeHalt",
    "DispatchFixed",
    "FixedTick",
    "KillSwitch",
    "originShift",
    "OriginShift",
    "lateFrameTick",
    "RequestSimulationPause",
    "SimulationPaused",
    "unpause",
    "dilated",
    "AUP",
    "bootstrap lock",
    "frame lock",
    "presimTick",
    "pumpFired",
    "SWIM",
    "movementIntent",
    "inputEnabled",
    "H8_WORLDDRIVER",
    "EnsureDispatcher",
    "fixed lane",
    "FIXEDSTEP",
)

out = []
c = Counter()
for i, l in enumerate(lines, 1):
    hit = False
    for k in keys:
        if k in l or k.lower() in l.lower():
            hit = True
            c[k] += 1
    if hit:
        out.append(f"{i}|{l[:400]}")

outp.write_text("\n".join(out), encoding="utf-8")
print("hits", len(out))
print("counts", dict(c))
print("wrote", outp)
