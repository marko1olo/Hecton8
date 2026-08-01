# -*- coding: utf-8 -*-
from pathlib import Path

h = Path(r"C:/hades/Hecton8/Assets/_Project/Scripts/HectonPlayerMovement.cs").read_text(
    encoding="utf-8-sig"
)
d = Path(
    r"C:/hades/Hecton8/Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessWorldDriver.cs"
).read_text(encoding="utf-8-sig")
out = Path(r"C:/hades/Hecton8/Tools/_cline_scratch/_l13_verify.txt")
buf = []

idx = h.find("public void FixedTick(float fixedDeltaTime)")
buf.append("FIXED_IDX=%d" % idx)
chunk = h[idx : idx + 900]
buf.append(chunk)
buf.append("---")
buf.append("EnsureDispatcherRegistration in HPM: %s" % ("public void EnsureDispatcherRegistration()" in h))
buf.append("L13 comment in HPM: %s" % ("L13: Sample locomotion BEFORE" in h))
sample_pos = chunk.find("SampleGameplayLocomotionInputForFixedStep()")
suit_pos = chunk.find("if (suit == null)")
juice_early = "if (_juiceProcessor == null) return" in chunk
buf.append("sample_pos=%s suit_pos=%s sample_before_suit=%s juice_early_still=%s" % (
    sample_pos, suit_pos, sample_pos >= 0 and suit_pos >= 0 and sample_pos < suit_pos, juice_early
))
buf.append("---DRV---")
buf.append("EnsureDispatcherRegistration count=%d" % d.count("EnsureDispatcherRegistration"))
idx2 = d.find("EnsureDispatcherRegistration")
if idx2 >= 0:
    buf.append(d[idx2 - 300 : idx2 + 500])
# movement field type
for i, l in enumerate(d.splitlines()):
    if "_movement" in l and ("static" in l or "IPlayer" in l):
        if i < 400 or "static" in l:
            buf.append("MFIELD %d: %s" % (i + 1, l[:160]))

# usings for HectonPlayerMovement namespace
for i, l in enumerate(d.splitlines()[:50]):
    if l.startswith("using") or "namespace" in l:
        buf.append("U %d: %s" % (i + 1, l))

out.write_text("\n".join(buf), encoding="utf-8")
print("WROTE", out)
