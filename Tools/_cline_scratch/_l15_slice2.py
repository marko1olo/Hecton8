# -*- coding: utf-8 -*-
import os
ROOT = r"C:\hades\Hecton8"
OUT = os.path.join(ROOT, r"Tools\_cline_scratch\_l15_slices2.txt")

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

def grep_file(rel, patterns):
    p = os.path.join(ROOT, rel)
    with open(p, "r", encoding="utf-8", errors="replace") as fh:
        lines = fh.readlines()
    chunks = ["\n##### GREP %s #####\n" % rel]
    for i, l in enumerate(lines, 1):
        low = l.lower()
        if any(pat.lower() in low for pat in patterns):
            chunks.append("%d|%s" % (i, l.rstrip()))
    return "".join(chunks)

parts = []
parts.append(slice_file(r"Assets\_Project\Scripts\Editor\Diagnostics\H8_HeadlessWorldDriver.cs", [
    (2685, 2750),
]))
parts.append(grep_file(r"Assets\_Project\Scripts\HectonPlayerMovement.cs", [
    "IsGameplayInputBlockedByMenu", "bool IsGameplayInputBlocked",
]))
parts.append(grep_file(r"Assets\_Project\Scripts\Editor\Diagnostics\H8_HeadlessWorldDriver.cs", [
    "_movement", "FindFirstObject", "HectonPlayerMovement", "ResolveMovement", "BindMovement",
]))
parts.append(grep_file(r"Assets\_Project\Scripts\Core\InputDispatcher.cs", [
    "CurrentInputState", "DiagRecordReadObservation(1)", "DiagRecordReadObservation",
]))
parts.append(grep_file(r"Assets\_Project\Scripts\Core\GlobalRegistry.cs", [
    "TryRegisterFixedTickable", "RegisterFixedTickable", "RegisteredInput", "NoOpInput",
]))
parts.append(slice_file(r"Assets\_Project\Scripts\Core\SystemDispatcher.cs", [
    (7110, 7140),
    (6235, 6275),
]))

text = "".join(parts)
with open(OUT, "w", encoding="utf-8") as fh:
    fh.write(text)
print("WROTE", OUT, len(text))
