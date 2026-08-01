# -*- coding: utf-8 -*-
import os
ROOT = r"C:\hades\Hecton8"
OUT = os.path.join(ROOT, r"Tools\_cline_scratch\_l15_slices4.txt")

def slice_file(rel, ranges):
    p = os.path.join(ROOT, rel)
    with open(p, "r", encoding="utf-8", errors="replace") as fh:
        lines = fh.readlines()
    chunks = ["\n##### %s #####\n" % rel]
    for a, b in ranges:
        chunks.append("--- %d:%d ---\n" % (a, b))
        for i in range(a - 1, min(b, len(lines))):
            chunks.append("%d|%s" % (i + 1, lines[i]))
    return "".join(chunks)

def grep_file(rel, patterns, limit=80):
    p = os.path.join(ROOT, rel)
    with open(p, "r", encoding="utf-8", errors="replace") as fh:
        lines = fh.readlines()
    chunks = ["\n##### GREP %s #####\n" % rel]
    n = 0
    for i, l in enumerate(lines, 1):
        if any(pat.lower() in l.lower() for pat in patterns):
            chunks.append("%d|%s" % (i, l.rstrip()))
            n += 1
            if n >= limit:
                break
    return "".join(chunks)

parts = []
parts.append(slice_file(r"Assets\_Project\Scripts\Core\GlobalRegistry.cs", [
    (6541, 6565),
    (6840, 6890),
    (6400, 6565),  # TryRegisterUpdatable siblings for pattern
]))
parts.append(slice_file(r"Assets\_Project\Scripts\Core\RegistryBucket.cs", [
    (80, 120),
]))
parts.append(slice_file(r"Assets\_Project\Scripts\Core\SystemDispatcher.cs", [
    (1320, 1380),
    (1535, 1550),
]))
# Clear / reset of fixed lanes
parts.append(grep_file(r"Assets\_Project\Scripts\Core\SystemDispatcher.cs", [
    "_fixedPriorityLanes", "Clear(", "ResetStatic", "DisposeFixed", "lane.Clear",
]))
parts.append(grep_file(r"Assets\_Project\Scripts\Core\GlobalRegistry.cs", [
    "_fixedTickables", "ClearFixed", "TryEnsureDispatcherRegistration",
]))
# HPM reset sticky without unregister - who calls the method at 4731
parts.append(slice_file(r"Assets\_Project\Scripts\HectonPlayerMovement.cs", [
    (4680, 4740),
    (5000, 5060),
]))
# Check if dispatcher has Contains API
parts.append(grep_file(r"Assets\_Project\Scripts\Core\SystemDispatcher.cs", [
    "Contains", "IsRegistered", "GetFixedLane",
]))
parts.append(grep_file(r"Assets\_Project\Scripts\Core\RegistryBucket.cs", [
    "Contains", "Count",
]))

text = "".join(parts)
with open(OUT, "w", encoding="utf-8") as fh:
    fh.write(text)
print("WROTE", OUT, len(text))
