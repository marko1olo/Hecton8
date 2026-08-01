# -*- coding: utf-8 -*-
import sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
out = []

hpm = r"C:\hades\Hecton8\Assets\_Project\Scripts\HectonPlayerMovement.cs"
lines = open(hpm, encoding="utf-8", errors="replace").readlines()
out.append("=== 4520-4560 ===")
for i in range(4520, 4560):
    out.append("%d|%s" % (i + 1, lines[i].rstrip()))
out.append("=== method walkback ===")
for i in range(4731, 4400, -1):
    s = lines[i].strip()
    if s.startswith("private void ") or s.startswith("public void ") or s.startswith("protected void "):
        out.append("%d|%s" % (i + 1, lines[i].rstrip()))
        break

rb = open(r"C:\hades\Hecton8\Assets\_Project\Scripts\Core\RegistryBucket.cs", encoding="utf-8", errors="replace").readlines()
out.append("=== RegistryBucket ===")
for i, l in enumerate(rb):
    out.append("%d|%s" % (i + 1, l.rstrip()))

sd = open(r"C:\hades\Hecton8\Assets\_Project\Scripts\Core\SystemDispatcher.cs", encoding="utf-8", errors="replace").readlines()
out.append("=== SD lanes/register ===")
for i, l in enumerate(sd):
    if "GetUpdateLane" in l or "Register(IUpdatable" in l or "GetFixedLane" in l or "GetColdLane" in l:
        out.append("%d|%s" % (i + 1, l.rstrip()))
        for j in range(i, min(i + 12, len(sd))):
            out.append("  %d|%s" % (j + 1, sd[j].rstrip()))

gr = open(r"C:\hades\Hecton8\Assets\_Project\Scripts\Core\GlobalRegistry.cs", encoding="utf-8", errors="replace").readlines()
out.append("=== GR TryRegister* ===")
for i, l in enumerate(gr):
    if "public static bool TryRegister" in l:
        out.append("%d|%s" % (i + 1, l.rstrip()))

# soft-reset callers - who calls the method
# find method name first then grep
method_line = None
for i in range(4731, 4400, -1):
    s = lines[i].strip()
    if s.startswith("private void ") or s.startswith("public void ") or s.startswith("protected void "):
        method_line = i
        name = s.split("(")[0].split()[-1]
        out.append("METHOD_NAME=%s at %d" % (name, i + 1))
        # find callers
        for j, lj in enumerate(lines):
            if name + "(" in lj and j != i:
                out.append("CALLER %d|%s" % (j + 1, lj.rstrip()))
        break

p = r"C:\hades\Hecton8\Tools\_cline_scratch\_l15_apis2.txt"
open(p, "w", encoding="utf-8").write("\n".join(out))
print("WROTE", len(out), p)
