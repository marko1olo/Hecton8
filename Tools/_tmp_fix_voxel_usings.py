# -*- coding: utf-8 -*-
"""Fix missing #endif after #if UNITY_EDITOR using UnityEditor in voxel split files."""
from pathlib import Path

files = [
    Path(r"Assets/_Project/Scripts/HectonVoxelEngine.cs"),
    Path(r"Assets/_Project/Scripts/HectonVoxelEngine.MCTables.cs"),
    Path(r"Assets/_Project/Scripts/HectonVoxelEngine.Jobs.cs"),
    Path(r"Assets/_Project/Scripts/HectonVoxelEngine.Editor.cs"),
]

for p in files:
    t = p.read_text(encoding="utf-8")
    original = t
    # Pattern: #if UNITY_EDITOR\nusing UnityEditor;\n without #endif before next content
    bad = "#if UNITY_EDITOR\nusing UnityEditor;\n"
    good = "#if UNITY_EDITOR\nusing UnityEditor;\n#endif\n"
    bad2 = "#if UNITY_EDITOR\r\nusing UnityEditor;\r\n"
    good2 = "#if UNITY_EDITOR\r\nusing UnityEditor;\r\n#endif\r\n"
    if bad in t and "#if UNITY_EDITOR\nusing UnityEditor;\n#endif" not in t:
        t = t.replace(bad, good, 1)
    elif bad2 in t and "#if UNITY_EDITOR\r\nusing UnityEditor;\r\n#endif" not in t:
        t = t.replace(bad2, good2, 1)
    # Also handle case where #endif exists later incorrectly
    # Check for double #endif after our fix - editor file may already wrap body in #if
    if t != original:
        p.write_text(t, encoding="utf-8", newline="\n")
        print(f"FIXED {p.name}")
    else:
        # report current state around UNITY_EDITOR
        lines = t.splitlines()
        for i, l in enumerate(lines):
            if "UNITY_EDITOR" in l or "using UnityEditor" in l:
                print(f"{p.name}:{i+1}|{l}")

# Also check jobs still contain only jobs - any leftover nested types that need runtime
jobs = Path(r"Assets/_Project/Scripts/HectonVoxelEngine.Jobs.cs").read_text(encoding="utf-8")
rt = Path(r"Assets/_Project/Scripts/HectonVoxelEngine.cs").read_text(encoding="utf-8")
print("jobs MCTables refs", jobs.count("MCTables"))
print("runtime MCTables refs", rt.count("MCTables"))
print("runtime job structs", sum(1 for l in rt.splitlines() if "struct " in l and "Job" in l and ("IJob" in l or True)))
# list job-like structs still in runtime
import re
for m in re.finditer(r"(public|private|internal)\s+(partial\s+)?struct\s+(\w*Job\w*)", rt):
    print("RUNTIME JOB STRUCT", m.group(3), "at", rt[:m.start()].count("\n")+1)
for m in re.finditer(r"(public|private|internal)\s+(partial\s+)?struct\s+(\w*Job\w*)", jobs):
    pass
print("jobs job structs count", len(re.findall(r"struct\s+\w*Job\w*", jobs)))
