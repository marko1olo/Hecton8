# -*- coding: utf-8 -*-
from pathlib import Path
import re

backup = Path(r"Docs/AgentLogs/_backup_HectonVoxelEngine_pre_split.cs").read_text(encoding="utf-8")
lines = backup.splitlines()
names = ["CubeDensities", "MCRawVertex", "VoxelSurfaceVertex", "VoxelModifiedCellEntry"]
for name in names:
    for i, l in enumerate(lines):
        if re.search(rf"\b(struct|class)\s+{name}\b", l):
            print(f"{name} DEF line {i+1}: {l.strip()}")
            # print surrounding 3 lines
            for j in range(max(0,i-2), min(len(lines), i+15)):
                print(f"  {j+1}|{lines[j][:120]}")
            print("---")

# Also check current files
for p in Path(r"Assets/_Project/Scripts").glob("HectonVoxelEngine*.cs"):
    t = p.read_text(encoding="utf-8")
    for name in names:
        if re.search(rf"\b(struct|class)\s+{name}\b", t):
            print(f"FOUND {name} in {p.name}")
        elif name in t:
            print(f"REF-only {name} in {p.name}")
