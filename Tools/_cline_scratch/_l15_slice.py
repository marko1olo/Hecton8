# -*- coding: utf-8 -*-
import os
ROOT = r"C:\hades\Hecton8"
OUT = os.path.join(ROOT, r"Tools\_cline_scratch\_l15_slices.txt")

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
parts.append(slice_file(r"Assets\_Project\Scripts\HectonPlayerMovement.cs", [
    (7980, 8280),
    (9920, 10180),
    (5460, 5560),
    (4720, 4820),
    (2970, 3020),
    (1890, 1920),
]))
parts.append(slice_file(r"Assets\_Project\Scripts\Editor\Diagnostics\H8_HeadlessWorldDriver.cs", [
    (1560, 1700),
    (1960, 2100),
    (2630, 2900),
]))
parts.append(slice_file(r"Assets\_Project\Scripts\Core\SystemDispatcher.cs", [
    (5260, 5330),
    (6180, 6270),
    (7105, 7140),
]))
parts.append(slice_file(r"Assets\_Project\Scripts\Core\RegistryBucket.cs", [
    (70, 180),
]))
# CurrentInputState hop1
parts.append(slice_file(r"Assets\_Project\Scripts\Core\InputDispatcher.cs", [
    (700, 760),
    (1355, 1370),
]))

text = "".join(parts)
with open(OUT, "w", encoding="utf-8") as fh:
    fh.write(text)
print("WROTE", OUT, len(text))
