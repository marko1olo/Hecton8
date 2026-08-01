# -*- coding: utf-8 -*-
from pathlib import Path

p = Path(r"C:/hades/Hecton8/Assets/_Project/Scripts/HectonPlayerMovement.cs")
raw = p.read_bytes()
# detect encoding
text = raw.decode("utf-8-sig", errors="replace")
lines = text.splitlines()
out = Path(r"C:/hades/Hecton8/Tools/_cline_scratch/_l13_hpm_slice.txt")
buf = []
buf.append("TOTAL_LINES=%d BYTES=%d" % (len(lines), len(raw)))

for i, l in enumerate(lines):
    if "FixedTick" in l and ("void" in l or "public" in l or "private" in l):
        buf.append("HIT_FIXED %d: %s" % (i + 1, l[:160]))
    if "SampleGameplayLocomotionInputForFixedStep" in l:
        buf.append("HIT_SAMPLE %d: %s" % (i + 1, l[:160]))
    if "EnsureJuiceProcessor" in l and ("(" in l):
        buf.append("HIT_JUICE %d: %s" % (i + 1, l[:160]))
    if "TryRegisterToDispatchers" in l:
        buf.append("HIT_REG %d: %s" % (i + 1, l[:160]))

# dump FixedTick region - search around HIT
fixed_line = None
for i, l in enumerate(lines):
    if "public void FixedTick" in l or "void FixedTick(float" in l:
        fixed_line = i
        break
buf.append("FIXED_LINE=%s" % fixed_line)
if fixed_line is not None:
    start = max(0, fixed_line - 5)
    end = min(len(lines), fixed_line + 120)
    buf.append("=== FIXEDTICK %d-%d ===" % (start + 1, end))
    for i in range(start, end):
        buf.append("%d|%s" % (i + 1, lines[i]))

# SampleGameplay body
sample_line = None
for i, l in enumerate(lines):
    if "void SampleGameplayLocomotionInputForFixedStep" in l or "SampleGameplayLocomotionInputForFixedStep()" in l and "{" in lines[min(i+1,len(lines)-1)]:
        if "void " in l or i + 1 < len(lines) and "void" in lines[max(0,i-2)]:
            sample_line = i
# better search
for i, l in enumerate(lines):
    if "SampleGameplayLocomotionInputForFixedStep" in l and ("void" in l or (i > 0 and "void" in lines[i - 1])):
        sample_line = i
        break
# find method def
for i, l in enumerate(lines):
    strip = l.strip()
    if strip.startswith("private void SampleGameplay") or strip.startswith("void SampleGameplay") or strip.startswith("private void SampleGameplayLocomotion"):
        sample_line = i
        break
    if "SampleGameplayLocomotionInputForFixedStep()" in l and i > 0 and ("void" in lines[i] or "void" in lines[i - 1]):
        sample_line = i

buf.append("SAMPLE_LINE_GUESS")
for i, l in enumerate(lines):
    if "SampleGameplayLocomotionInputForFixedStep" in l:
        # show context if looks like definition
        window = "\n".join(lines[max(0, i - 2) : i + 3])
        if "void" in window and "(" in l:
            buf.append("DEF_CAND %d" % (i + 1))
            for j in range(max(0, i - 2), min(len(lines), i + 80)):
                buf.append("%d|%s" % (j + 1, lines[j]))
            break

# EnsureJuiceProcessor def
for i, l in enumerate(lines):
    if "EnsureJuiceProcessor" in l and "void" in l:
        buf.append("=== JUICE %d ===" % (i + 1))
        for j in range(i, min(len(lines), i + 40)):
            buf.append("%d|%s" % (j + 1, lines[j]))
        break

# TryRegisterToDispatchers
for i, l in enumerate(lines):
    if "void TryRegisterToDispatchers" in l or "bool TryRegisterToDispatchers" in l or "TryRegisterToDispatchers()" in l and "void" in l:
        buf.append("=== REG %d ===" % (i + 1))
        for j in range(i, min(len(lines), i + 50)):
            buf.append("%d|%s" % (j + 1, lines[j]))
        break

out.write_text("\n".join(buf), encoding="utf-8")
print("WROTE", out, "size", out.stat().st_size)
