# -*- coding: utf-8 -*-
from pathlib import Path

ROOT = Path(r"C:\hades\Hecton8")
PROBE = ROOT / r"Assets\_Project\Scripts\Editor\Diagnostics\H8_HeadlessPlayModeProbe.cs"
SD = ROOT / r"Assets\_Project\Scripts\Core\SystemDispatcher.cs"
OUT = ROOT / r"Tools\_cline_scratch\_l17_verify.txt"

p = PROBE.read_text(encoding="utf-8")
s = SD.read_text(encoding="utf-8")

checks = [
    ("DrainProbeFloatingOriginBootstrap def", "private static void DrainProbeFloatingOriginBootstrap" in p),
    ("FODRAIN marker", "FODRAIN reason=" in p),
    ("_probeFoDrainCalls", "_probeFoDrainCalls" in p),
    ("gameplay-tick drain", 'DrainProbeFloatingOriginBootstrap("gameplay-tick")' in p),
    ("gameplay-window-start drain", 'DrainProbeFloatingOriginBootstrap("gameplay-window-start")' in p),
    ("worlddriver-begin drain", 'DrainProbeFloatingOriginBootstrap("worlddriver-begin")' in p),
    ("reset FO fields", "_probeFoDrainCleanCount = 0" in p),
    ("SD L17 LateFrame comment", "L17: parity with RunDispatcherUpdate" in s),
    ("SD LateFrame TryFlush block", "if (!HectonFloatingOrigin.TryFlushInitialSceneRebaseBeforeTicks())" in s),
    ("SD LateFrame bootstrap lock still checked", "if (IsOriginShiftBootstrapLocked)" in s),
]

lines = [f"{k}: {'OK' if v else 'MISSING'}" for k, v in checks]
lines.append("ALL=" + str(all(v for _, v in checks)))

# Snippet counts
lines.append(f"probe Drain calls count={p.count('DrainProbeFloatingOriginBootstrap')}")
lines.append(f"sd TryFlush count={s.count('TryFlushInitialSceneRebaseBeforeTicks')}")

OUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
print("WROTE", OUT)
for line in lines:
    print(line)
