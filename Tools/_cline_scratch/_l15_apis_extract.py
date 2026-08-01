# -*- coding: utf-8 -*-
import sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
out = []

hpm = r"C:\hades\Hecton8\Assets\_Project\Scripts\HectonPlayerMovement.cs"
lines = open(hpm, encoding="utf-8", errors="replace").readlines()

out.append("=== method head 4480-4520 ===")
for i in range(4480, 4520):
    out.append("%d|%s" % (i + 1, lines[i].rstrip()))

out.append("=== TryRegisterToDispatchers 5470-5540 ===")
for i in range(5469, min(5545, len(lines))):
    out.append("%d|%s" % (i + 1, lines[i].rstrip()))

out.append("=== UnregisterGlobal 5012-5060 ===")
for i in range(5011, min(5060, len(lines))):
    out.append("%d|%s" % (i + 1, lines[i].rstrip()))

out.append("=== Ensure path 4790-4825 ===")
for i in range(4789, min(4825, len(lines))):
    out.append("%d|%s" % (i + 1, lines[i].rstrip()))

gr = r"C:\hades\Hecton8\Assets\_Project\Scripts\Core\GlobalRegistry.cs"
gl = open(gr, encoding="utf-8", errors="replace").readlines()
out.append("=== GR marks ===")
for i, l in enumerate(gl):
    if "TryRegisterFixedTickable" in l or "UnregisterFixedTickable" in l or (
        "TryRegisterUpdatable" in l and "public static" in l
    ):
        out.append("MARK %d|%s" % (i + 1, l.rstrip()))

out.append("=== GR TryRegisterFixedTickable body ===")
for i in range(6535, min(6585, len(gl))):
    out.append("%d|%s" % (i + 1, gl[i].rstrip()))

out.append("=== GR UnregisterFixed body ===")
for i in range(6850, min(6895, len(gl))):
    out.append("%d|%s" % (i + 1, gl[i].rstrip()))

# Find TryRegisterUpdatable
for i, l in enumerate(gl):
    if "TryRegisterUpdatable" in l and "public static" in l:
        out.append("=== GR TryRegisterUpdatable body ===")
        for j in range(i, min(i + 35, len(gl))):
            out.append("%d|%s" % (j + 1, gl[j].rstrip()))
        break

# Find all dual-register methods pattern
out.append("=== GR dual-register pattern sites ===")
for i, l in enumerate(gl):
    if "if (!_fixedTickables.TryRegister" in l or "if (!_updatables.TryRegister" in l:
        out.append("%d|%s" % (i + 1, l.rstrip()))
        for j in range(max(0, i - 8), min(i + 20, len(gl))):
            out.append("  %d|%s" % (j + 1, gl[j].rstrip()))

sd = r"C:\hades\Hecton8\Assets\_Project\Scripts\Core\SystemDispatcher.cs"
sl = open(sd, encoding="utf-8", errors="replace").readlines()
out.append("=== SD Register Fixed marks ===")
for i, l in enumerate(sl):
    if "Register(IFixedTickable" in l or "GetFixedLane" in l or "IsFixedTickableRegistered" in l:
        out.append("MARK %d|%s" % (i + 1, l.rstrip()))

out.append("=== SD Register Fixed body ~1360 ===")
for i in range(1355, min(1450, len(sl))):
    out.append("%d|%s" % (i + 1, sl[i].rstrip()))

# Contains on lanes
out.append("=== SD Contains / lane membership ===")
for i, l in enumerate(sl):
    if "Contains(" in l and ("Fixed" in l or "fixed" in l or "IFixed" in l):
        out.append("%d|%s" % (i + 1, l.rstrip()))
    if "public static bool" in l and ("Registered" in l or "Contains" in l):
        out.append("API %d|%s" % (i + 1, l.rstrip()))

rb = r"C:\hades\Hecton8\Assets\_Project\Scripts\Core\RegistryBucket.cs"
rl = open(rb, encoding="utf-8", errors="replace").readlines()
out.append("=== RegistryBucket full relevant ===")
for i, l in enumerate(rl):
    out.append("%d|%s" % (i + 1, l.rstrip()))

p = r"C:\hades\Hecton8\Tools\_cline_scratch\_l15_apis.txt"
open(p, "w", encoding="utf-8").write("\n".join(out))
print("WROTE", p, "lines", len(out))
