# -*- coding: utf-8 -*-
import sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

def dump(path, start, end):
    lines = open(path, encoding="utf-8", errors="replace").read().splitlines()
    print("===== %s %d-%d =====" % (path, start, end))
    for i in range(start, min(end, len(lines)) + 1):
        print("%6d|%s" % (i, lines[i - 1]))
    print()

hpm = r"C:\hades\Hecton8\Assets\_Project\Scripts\HectonPlayerMovement.cs"
# registration
dump(hpm, 5460, 5560)
# ProcessPlayerInputFrame + SampleGameplay
dump(hpm, 8085, 8260)
# FixedTick start
dump(hpm, 9910, 9970)
# ResolveRawInputIntentVector
dump(hpm, 2980, 3025)
# ResolveInputManagerBinding + IsGameplayInputBlockedByMenu
lines = open(hpm, encoding="utf-8", errors="replace").read().splitlines()
for name in ("ResolveInputManagerBinding", "IsGameplayInputBlockedByMenu", "IsAuthoritativeVehicleTransport"):
    for i, l in enumerate(lines, 1):
        if name in l and ("(" in l) and (l.strip().startswith("private") or l.strip().startswith("public") or l.strip().startswith("internal") or l.strip().startswith("bool") or " bool " in l):
            print("===== %s @ %d =====" % (name, i))
            for j in range(i, min(len(lines), i + 60) + 1):
                print("%6d|%s" % (j, lines[j - 1]))
                if j > i + 2 and lines[j - 1].strip() == "}" and lines[j - 1].startswith("        }"):
                    break
            print()
            break

# _registeredFixedTick field and early outs in TryRegisterToDispatchers
for i, l in enumerate(lines, 1):
    if "_registeredFixedTick" in l or "_registeredUpdatable" in l or "_registeredColdTick" in l:
        if i < 200 or "private bool" in l or "=" in l:
            print("FIELD %d: %s" % (i, l[:200]))

pi = r"C:\hades\Hecton8\Assets\_Project\Scripts\PlayerInventory.cs"
dump(pi, 2535, 2650)
# Awake bind area
dump(pi, 1650, 1750)

# InputDispatcher readHop / GetState
idisp = r"C:\hades\Hecton8\Assets\_Project\Scripts\Core\InputDispatcher.cs"
id_lines = open(idisp, encoding="utf-8", errors="replace").read().splitlines()
for i, l in enumerate(id_lines, 1):
    if "readHop" in l or "BumpReadHop" in l or "GetState(" in l and "public" in l:
        if i < 50 or "public" in l or "readHop" in l or "void" in l or "PlayerInputState" in l:
            print("IDISP %d: %s" % (i, l[:220]))

for i, l in enumerate(id_lines, 1):
    if "public PlayerInputState GetState" in l or "PlayerInputState GetState(" in l:
        print("===== GetState @ %d =====" % i)
        for j in range(i, min(len(id_lines), i + 40) + 1):
            print("%6d|%s" % (j, id_lines[j - 1]))
        break

for i, l in enumerate(id_lines, 1):
    if "readHop" in l.lower() or "ReadHop" in l:
        print("RH %d: %s" % (i, l[:220]))
