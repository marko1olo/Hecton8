# -*- coding: utf-8 -*-
import sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

def dump_hits(path, keys, label=None):
    print("=" * 60)
    print(label or path)
    lines = open(path, encoding="utf-8", errors="replace").read().splitlines()
    print("total", len(lines))
    hits = []
    for i, l in enumerate(lines, 1):
        if any(k in l for k in keys):
            hits.append("%d: %s" % (i, l[:240]))
    print("hits", len(hits))
    for h in hits:
        print(h)
    return lines

hpm = r"C:\hades\Hecton8\Assets\_Project\Scripts\HectonPlayerMovement.cs"
lines = dump_hits(hpm, [
    "TryRegisterFixedTickable", "IFixedTickable", "RegisterFixed",
    "_registeredToFixed", "UnregisterFixed", "FixedTick(",
    "TryRegisterToTick", "IUpdatable", "ITickable", "IFixed",
    "class HectonPlayerMovement", "SampleGameplayLocomotion",
])

for i, l in enumerate(lines, 1):
    if "class HectonPlayerMovement" in l:
        print("--- class context ---")
        for j in range(max(1, i - 5), min(len(lines), i + 30) + 1):
            print("%d: %s" % (j, lines[j - 1][:240]))
        break

# Find registration methods near TryRegister
for i, l in enumerate(lines, 1):
    if "TryRegister" in l and ("void" in l or "bool" in l or "private" in l or "public" in l):
        print("--- TryRegister near %d ---" % i)
        for j in range(i, min(len(lines), i + 80) + 1):
            print("%d: %s" % (j, lines[j - 1][:240]))
            if j > i + 5 and lines[j - 1].strip() == "}" and j > i + 20:
                # crude end - keep going a bit
                pass
        # only first few
        break

# All TryRegister* method bodies - find line numbers of methods containing Register
print("\n--- ALL registration-related method signatures ---")
for i, l in enumerate(lines, 1):
    s = l.strip()
    if ("Register" in l or "Unregister" in l) and ("(" in l) and (
        s.startswith("private") or s.startswith("public") or s.startswith("protected") or s.startswith("internal")
    ):
        print("%d: %s" % (i, l[:240]))

ptm = r"C:\hades\Hecton8\Assets\_Project\Scripts\PlayerToolManager.cs"
dump_hits(ptm, [
    "TryRegisterToTickManager", "_registeredToFixed", "_registeredToTick",
    "_registeredToLateFrame", "IFixedTickable", "TryRegisterFixedTickable",
    "UnregisterFixedTickable", "FixedTick(",
])

# Print TryRegisterToTickManager full body
ptm_lines = open(ptm, encoding="utf-8", errors="replace").read().splitlines()
for i, l in enumerate(ptm_lines, 1):
    if "private void TryRegisterToTickManager" in l:
        print("--- PTM TryRegisterToTickManager ---")
        for j in range(i, min(len(ptm_lines), i + 50) + 1):
            print("%d: %s" % (j, ptm_lines[j - 1][:240]))
        break

for i, l in enumerate(ptm_lines, 1):
    if "private void UnregisterFromTickManager" in l or "void Unregister" in l and "Tick" in l:
        print("--- PTM Unregister near %d ---" % i)
        for j in range(i, min(len(ptm_lines), i + 40) + 1):
            print("%d: %s" % (j, ptm_lines[j - 1][:240]))
        break

pi = r"C:\hades\Hecton8\Assets\_Project\Scripts\PlayerInventory.cs"
pi_lines = open(pi, encoding="utf-8", errors="replace").read().splitlines()
print("\n" + "=" * 60)
print("PlayerInventory key methods")
for i, l in enumerate(pi_lines, 1):
    if any(k in l for k in (
        "CanServiceItemAdds", "TryRecoverRuntimeStorageCold", "DescribeAddRefusalMask",
        "BindRuntimeStorage", "STORAGE UNAVAILABLE", "_stackCounts", "_grid",
        "TryBind", "EnsureRuntimeStorage",
    )):
        if "bool " in l or "void " in l or "internal " in l or "public " in l or "STORAGE" in l or "private bool" in l:
            print("%d: %s" % (i, l[:240]))

# Dump CanService + TryRecover + DescribeAddRefusalMask bodies
for name in ("CanServiceItemAdds", "TryRecoverRuntimeStorageCold", "DescribeAddRefusalMask"):
    for i, l in enumerate(pi_lines, 1):
        if name in l and ("bool " in l or "uint " in l) and "(" in l:
            print("--- %s @ %d ---" % (name, i))
            for j in range(i, min(len(pi_lines), i + 80) + 1):
                print("%d: %s" % (j, pi_lines[j - 1][:240]))
                if j > i + 3 and pi_lines[j - 1].strip() == "}" and pi_lines[j - 1].startswith("        }"):
                    break
            break
