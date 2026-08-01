# -*- coding: utf-8 -*-
import os
import sys

sys.stdout.reconfigure(encoding="utf-8")
OUT = r"C:\hades\Hecton8\Tools\_cline_scratch"

def write(name, text):
    path = os.path.join(OUT, name)
    with open(path, "w", encoding="utf-8") as f:
        f.write(text)
    print("wrote", name, len(text))

# HSR constants
hsr = r"C:\hades\Hecton8\Assets\_Project\Scripts\QA\Headless\HeadlessSimulationRunner.cs"
with open(hsr, encoding="utf-8") as f:
    lines = f.readlines()
hits = []
for i, l in enumerate(lines, 1):
    if any(k in l for k in (
        "TimeDilationScalar", "HeadlessStepBounded", "PostReadyClockEnsure",
        "RunnerHash", "_lastClockEnsure", "const float", "static readonly int",
        "static readonly float", "private const", "private static readonly",
    )):
        if i < 250 or any(k in l for k in (
            "TimeDilation", "StepBound", "ClockEnsure", "RunnerHash", "_lastClock"
        )):
            hits.append("%d|%s" % (i, l.rstrip()))
write("_l16_hsr_const2.txt", "\n".join(hits))

# first 120 lines of HSR for field decls
write("_l16_hsr_top120.txt", "".join("%d|%s\n" % (i, l.rstrip()) for i, l in enumerate(lines[:120], 1)))

# probe head
probe = r"C:\hades\Hecton8\Assets\_Project\Scripts\Editor\Diagnostics\H8_HeadlessPlayModeProbe.cs"
with open(probe, encoding="utf-8") as f:
    plines = f.readlines()
write("_l16_probe_head250.txt", "".join("%d|%s\n" % (i, l.rstrip()) for i, l in enumerate(plines[:250], 1)))

# probe usings + class + marker
u = []
for i, l in enumerate(plines[:100], 1):
    if l.startswith("using") or "namespace" in l or "class " in l or "Marker" in l or "const string" in l:
        u.append("%d|%s" % (i, l.rstrip()))
write("_l16_probe_usings.txt", "\n".join(u))

# ResetRunState full
for i, l in enumerate(plines, 1):
    if "void ResetRunState" in l:
        start = i - 1
        break
else:
    start = 3650
chunk = plines[start:start+90]
write("_l16_probe_reset.txt", "".join("%d|%s\n" % (start+j+1, l.rstrip()) for j, l in enumerate(chunk)))

# GameplayWarmup already known 727-817
write("_l16_probe_gw2.txt", "".join("%d|%s\n" % (i, plines[i-1].rstrip()) for i in range(720, 820)))

# Check EnableStepBoundedTime IVT and signature
sd = r"C:\hades\Hecton8\Assets\_Project\Scripts\Core\SystemDispatcher.cs"
with open(sd, encoding="utf-8") as f:
    sdlines = f.readlines()
sdhits = []
for i, l in enumerate(sdlines, 1):
    if "EnableStepBoundedTime" in l or "IsStepBoundedTimeActive" in l or "MaxClampFreeStepSeconds" in l or "MaxStepBoundedDeltaSeconds" in l:
        sdhits.append("%d|%s" % (i, l.rstrip()))
        # capture method body nearby
write("_l16_sd_clock_api.txt", "\n".join(sdhits[:80]))

# dump EnableStepBoundedTime method
for i, l in enumerate(sdlines, 1):
    if "static bool EnableStepBoundedTime" in l or "internal static bool EnableStepBoundedTime" in l:
        body = "".join("%d|%s\n" % (j, sdlines[j-1].rstrip()) for j in range(i, min(i+40, len(sdlines)+1)))
        write("_l16_sd_enable_body.txt", body)
        break

# AssemblyInfo IVT
ai = r"C:\hades\Hecton8\Assets\_Project\Scripts\AssemblyInfo.cs"
if os.path.isfile(ai):
    write("_l16_assemblyinfo.txt", open(ai, encoding="utf-8").read())

print("ALL_OK lines_probe", len(plines), "hsr", len(lines))
