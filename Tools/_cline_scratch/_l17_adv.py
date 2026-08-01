# -*- coding: utf-8 -*-
import pathlib

sd = pathlib.Path(
    r"C:\hades\Hecton8\Assets\_Project\Scripts\Core\SystemDispatcher.cs"
).read_text(encoding="utf-8", errors="replace").splitlines()
out = []
for i, l in enumerate(sd):
    if "AdvanceDispatcherFrameId" in l:
        out.append(f"{i+1}|{l}")
for i, l in enumerate(sd):
    if "dispatcherFrameId" in l and 5150 < i < 5240:
        out.append(f"{i+1}|{l}")
for i, l in enumerate(sd):
    if "ReleaseAupPreShiftPause" in l:
        out.append(f"REL {i+1}|{l}")
# also where Advance is called from RunDispatcherUpdate
for i, l in enumerate(sd):
    if "AdvanceDispatcherFrameId()" in l:
        for j in range(max(0, i - 5), min(len(sd), i + 3)):
            out.append(f"CTX {j+1}|{sd[j]}")
        out.append("---")

dest = pathlib.Path(r"C:\hades\Hecton8\Tools\_cline_scratch\_l17_adv.txt")
dest.write_text("\n".join(out), encoding="utf-8")
print("WROTE", dest.stat().st_size, flush=True)
