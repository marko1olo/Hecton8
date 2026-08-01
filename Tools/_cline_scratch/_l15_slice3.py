# -*- coding: utf-8 -*-
import os
ROOT = r"C:\hades\Hecton8"
OUT = os.path.join(ROOT, r"Tools\_cline_scratch\_l15_slices3.txt")

def slice_file(rel, ranges):
    p = os.path.join(ROOT, rel)
    with open(p, "r", encoding="utf-8", errors="replace") as fh:
        lines = fh.readlines()
    chunks = ["\n##### %s total=%d #####\n" % (rel, len(lines))]
    for a, b in ranges:
        chunks.append("--- %d:%d ---\n" % (a, b))
        for i in range(a - 1, min(b, len(lines))):
            chunks.append("%d|%s" % (i + 1, lines[i]))
    return "".join(chunks)

parts = []
# IsGameplayInputBlockedByMenu body
parts.append(slice_file(r"Assets\_Project\Scripts\HectonPlayerMovement.cs", [
    (9780, 9850),
]))
# GlobalRegistry TryRegisterFixedTickable
parts.append(slice_file(r"Assets\_Project\Scripts\Core\GlobalRegistry.cs", [
    (6520, 6580),
    (940, 980),
    (8610, 8480),  # noop - fix below
]))
parts.append(slice_file(r"Assets\_Project\Scripts\Core\GlobalRegistry.cs", [
    (8610, 8680),
]))
# RegistryBucket TryRegister full
parts.append(slice_file(r"Assets\_Project\Scripts\Core\RegistryBucket.cs", [
    (1, 180),
]))
# InputDispatcher CurrentInputState + IsPlayerInputEnabled
parts.append(slice_file(r"Assets\_Project\Scripts\Core\InputDispatcher.cs", [
    (400, 520),
]))
# Check PriorityLayer enum values
parts.append(slice_file(r"Assets\_Project\Scripts\Core\SystemDispatcher.cs", [
    (5268, 5290),
    (6181, 6230),
]))

# Also search for timeScale / fixedDeltaTime zero / pause simulation
for rel, pats in [
    (r"Assets\_Project\Scripts\Core\SystemDispatcher.cs", ["timeScale", "FixedStepSeconds", "SimulationHalted", "pause"]),
]:
    p = os.path.join(ROOT, rel)
    with open(p, "r", encoding="utf-8", errors="replace") as fh:
        lines = fh.readlines()
    parts.append("\n##### GREP time/halt %s #####\n" % rel)
    for i, l in enumerate(lines, 1):
        if any(x.lower() in l.lower() for x in pats) and i < 200 or ("FixedStepSeconds" in l) or ("IsSimulationHalted" in l and "if" in l):
            if any(x.lower() in l.lower() for x in ["FixedStepSeconds", "IsSimulationHalted", "timeScale", "_fixedStep"]):
                parts.append("%d|%s\n" % (i, l.rstrip()))

text = "".join(parts)
with open(OUT, "w", encoding="utf-8") as fh:
    fh.write(text)
print("WROTE", OUT, len(text))
