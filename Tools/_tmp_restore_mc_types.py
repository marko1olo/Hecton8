# -*- coding: utf-8 -*-
from pathlib import Path

backup = Path(r"Docs/AgentLogs/_backup_HectonVoxelEngine_pre_split.cs").read_text(encoding="utf-8")
blines = backup.splitlines()

# Find MC Types region
start = None
end = None
for i, l in enumerate(blines):
    if "#region MC Types" in l:
        start = i
    if start is not None and end is None and i > start and l.strip() == "#endregion":
        end = i
        break
if start is None or end is None:
    raise SystemExit(f"MC Types region not found start={start} end={end}")

region = blines[start : end + 1]
print(f"MC Types L{start+1}-L{end+1} count={len(region)}")

# Get usings from jobs file
jobs_path = Path(r"Assets/_Project/Scripts/HectonVoxelEngine.Jobs.cs")
jobs = jobs_path.read_text(encoding="utf-8")
jlines = jobs.splitlines()

# Ensure types not already present
if "public struct CubeDensities" in jobs:
    print("Already in jobs - skip")
else:
    # Insert after usings / header, before first job type
    # Find last using/#endif block
    insert_at = 0
    for i, l in enumerate(jlines):
        if l.startswith("using ") or l.strip() in ("#if UNITY_EDITOR", "#endif", "") or l.startswith("//"):
            insert_at = i + 1
            continue
        break
    # Build types file instead for clarity
    types_path = Path(r"Assets/_Project/Scripts/HectonVoxelEngine.MCTypes.cs")
    # header from jobs usings
    header = []
    for l in jlines:
        header.append(l)
        if l.startswith("using ") or l.strip() == "#endif" or l.startswith("//") or l.strip() == "" or l.strip() == "#if UNITY_EDITOR":
            continue
        # stop before first real code - actually collect only preamble
        break
    # better preamble: lines until first non-comment non-using non-preproc non-blank after mechanical header
    preamble = []
    seen_using = False
    for l in jlines:
        if l.startswith("using ") or l.strip() in ("#if UNITY_EDITOR", "#endif") or l.startswith("//") or l.strip() == "":
            preamble.append(l)
            if l.startswith("using "):
                seen_using = True
            continue
        if seen_using:
            break
        preamble.append(l)

    content = "\n".join(preamble).rstrip() + "\n\n" + "\n".join(region) + "\n"
    types_path.write_text(content, encoding="utf-8", newline="\n")
    print(f"WROTE {types_path} lines={content.count(chr(10))+1}")

# Also check for other #region blocks between MCTables end and first Burst job that might be missing
# scan backup 783-900 for regions
for i in range(780, min(950, len(blines))):
    if "#region" in blines[i] or "#endregion" in blines[i] or blines[i].strip().startswith("public struct") or blines[i].strip().startswith("[BurstCompile"):
        s = "".join(c if ord(c)<128 else "?" for c in blines[i][:100])
        print(f"B{i+1}|{s}")
