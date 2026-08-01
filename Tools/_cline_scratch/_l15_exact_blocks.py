# -*- coding: utf-8 -*-
import sys, re
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
out = []
gr = open(r"C:\hades\Hecton8\Assets\_Project\Scripts\Core\GlobalRegistry.cs", encoding="utf-8", errors="replace").read()
for name in ["TryRegisterUpdatable", "TryRegisterFixedTickable", "TryRegisterColdTickable", "TryRegisterLateFrameTickable"]:
    m = re.search(r"public static bool %s\(.*?\n        \}" % name, gr, re.S)
    if m:
        out.append("==== GR %s ====" % name)
        out.append(m.group(0))
        out.append("")
    else:
        out.append("MISS GR %s" % name)

sd = open(r"C:\hades\Hecton8\Assets\_Project\Scripts\Core\SystemDispatcher.cs", encoding="utf-8", errors="replace").read()
for name in ["IUpdatable", "IFixedTickable", "IColdTickable", "ILateFrameTickable"]:
    m = re.search(r"public static bool Register\(%s item, PriorityLayer layer\)\n        \{.*?\n        \}" % name, sd, re.S)
    if m:
        out.append("==== SD Register %s ====" % name)
        out.append(m.group(0))
        out.append("")
    else:
        out.append("MISS SD %s" % name)

# HPM TryRegister block
hpm = open(r"C:\hades\Hecton8\Assets\_Project\Scripts\HectonPlayerMovement.cs", encoding="utf-8", errors="replace").read()
m = re.search(r"public void EnsureDispatcherRegistration\(\)\n        \{.*?\n        \}\n\n        private void TryRegisterToDispatchers\(\)\n        \{.*?\n        \}", hpm, re.S)
if m:
    out.append("==== HPM Ensure+TryRegister ====")
    out.append(m.group(0))
else:
    out.append("MISS HPM")

p = r"C:\hades\Hecton8\Tools\_cline_scratch\_l15_exact_blocks.txt"
open(p, "w", encoding="utf-8").write("\n".join(out))
print("WROTE", len(out), "parts to", p)
