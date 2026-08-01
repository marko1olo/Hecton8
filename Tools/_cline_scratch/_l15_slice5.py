# -*- coding: utf-8 -*-
import os
ROOT = r"C:\hades\Hecton8"
OUT = os.path.join(ROOT, r"Tools\_cline_scratch\_l15_slices5.txt")

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

def grep_file(rel, patterns, limit=60):
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
# Find method containing line 4731 - go upward for signature
parts.append(slice_file(r"Assets\_Project\Scripts\HectonPlayerMovement.cs", [
    (4550, 4745),
]))
# SystemDispatcher Clear / ResetStaticState
parts.append(slice_file(r"Assets\_Project\Scripts\Core\SystemDispatcher.cs", [
    (1660, 1720),
    (7260, 7320),
    (770, 830),
]))
# GlobalRegistry clear fixed
parts.append(slice_file(r"Assets\_Project\Scripts\Core\GlobalRegistry.cs", [
    (2880, 2940),
    (7000, 7120),
]))
# IsPlayerInputEnabled on InputDispatcher
parts.append(grep_file(r"Assets\_Project\Scripts\Core\InputDispatcher.cs", [
    "IsPlayerInputEnabled",
]))
# timeScale in pause / driver / probe
parts.append(grep_file(r"Assets\_Project\Scripts\UI", [
    "timeScale",
]))
parts.append(grep_file(r"Assets\_Project\Scripts\Editor\Diagnostics\H8_HeadlessWorldDriver.cs", [
    "timeScale", "Time.timeScale",
]))
parts.append(grep_file(r"Assets\_Project\Scripts\Editor\Diagnostics", [
    "timeScale",
]))
# TryRegisterLateFrameTickable pattern
parts.append(slice_file(r"Assets\_Project\Scripts\Core\GlobalRegistry.cs", [
    (6680, 6760),
]))

text = "".join(parts)
with open(OUT, "w", encoding="utf-8") as fh:
    fh.write(text)
print("WROTE", OUT, len(text))
