# -*- coding: utf-8 -*-
from pathlib import Path

t = Path(r"Assets/_Project/Scripts/HectonPlayerMovement.cs").read_text(encoding="utf-8")
lines = t.splitlines()
out = []
keys = [
    "QueueExternalAcceleration",
    "QueueExternalVelocityChange",
    "ApplyPhysicalTrauma",
    "ApplyFaunaHypnosisPull",
    "ApplyExternalThermalUpdraft",
    "ApplyParasiteLatchInfluence",
    "ApplyTowCableSnapFeedback",
    "ApplyCuttingTensionAnchor",
    "ForceTransportBailout",
    "_queuedExternal",
    "QueuedExternal",
    "externalAcceleration",
    "externalVelocity",
    "DrainExternal",
    "ConsumeExternal",
]
for i, l in enumerate(lines, 1):
    if any(k in l for k in keys):
        out.append(f"{i}|{l}")

# windows around Queue methods
for needle in ["void QueueExternalAcceleration", "void QueueExternalVelocityChange", "void ApplyPhysicalTrauma", "void FixedTick"]:
    for i, l in enumerate(lines):
        if needle in l:
            out.append(f"\n--- WIN {needle} @{i+1} ---")
            for j in range(max(0, i - 2), min(len(lines), i + 80)):
                out.append(f"{j+1}|{lines[j]}")
            break

text = "\n".join(out)
text = "".join(c if ord(c) < 128 else "?" for c in text)
Path(r"Docs/AgentLogs/_pm_force_map.txt").write_text(text, encoding="ascii")
print("lines_out", len(out))

# MC Types region
v = Path(r"Assets/_Project/Scripts/HectonVoxelEngine.cs").read_text(encoding="utf-8")
vl = v.splitlines()
start = end = None
for i, l in enumerate(vl):
    if "#region MC Types" in l:
        start = i
    if start is not None and end is None and i > start and l.strip() == "#endregion":
        end = i
        break
print(f"MCTypes {start+1 if start is not None else None}-{end+1 if end is not None else None}")
if start is not None:
    chunk = "\n".join(vl[start:end+1])
    Path(r"Docs/AgentLogs/_mc_types_region.txt").write_text(
        "".join(c if ord(c) < 128 else "?" for c in chunk), encoding="ascii"
    )
    print("mctypes chars", len(chunk))
