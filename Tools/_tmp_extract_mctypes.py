# -*- coding: utf-8 -*-
"""Extract public MC Types region only from HectonVoxelEngine.cs — same green path as MCTables."""
from pathlib import Path

src = Path(r"Assets/_Project/Scripts/HectonVoxelEngine.cs")
backup = Path(r"Docs/AgentLogs/_backup_HectonVoxelEngine_pre_mctypes_extract.cs")
out = Path(r"Assets/_Project/Scripts/HectonVoxelEngine.MCTypes.cs")
mctables = Path(r"Assets/_Project/Scripts/HectonVoxelEngine.MCTables.cs")

raw = src.read_bytes()
text = raw.decode("utf-8")
lines = text.splitlines(keepends=True)

start = end = None
for i, l in enumerate(lines):
    if "#region MC Types" in l:
        start = i
    if start is not None and end is None and i > start and l.strip() == "#endregion":
        end = i
        break
if start is None or end is None:
    raise SystemExit(f"MC Types region not found start={start} end={end}")

region = lines[start : end + 1]
print(f"MC Types L{start+1}-L{end+1} count={end-start+1}")

# Reuse preamble from MCTables sibling (already proven compile-clean)
if mctables.exists():
    mt = mctables.read_text(encoding="utf-8")
    mt_lines = mt.splitlines()
    # take header+usings until blank line before #region or class
    preamble = []
    for l in mt_lines:
        if l.startswith("#region") or l.startswith("public static class MCTables"):
            break
        preamble.append(l)
    # rewrite header comment for MCTypes
    new_preamble = []
    for l in preamble:
        if "MCTables only" in l:
            new_preamble.append("// Extracted from HectonVoxelEngine.cs — MC Types only (no logic change)")
        elif "Slice A step-2" in l:
            new_preamble.append("// 2026-08-03 architecture Slice A step-2b")
        else:
            new_preamble.append(l)
    preamble = new_preamble
else:
    # fallback usings from src
    last_using = max(i for i, l in enumerate(lines) if l.startswith("using "))
    preamble = [l.rstrip("\r\n") for l in lines[: last_using + 1]]
    preamble = [
        "// =====================================================================",
        "// Extracted from HectonVoxelEngine.cs — MC Types only (no logic change)",
        "// 2026-08-03 architecture Slice A step-2b",
        "// =====================================================================",
        "",
    ] + preamble

region_text = "".join(l.replace("\r\n", "\n") for l in region)
if not region_text.endswith("\n"):
    region_text += "\n"

content = "\n".join(preamble).rstrip() + "\n\n" + region_text
# Ensure System.Runtime.InteropServices for StructLayout
if "StructLayout" in content and "using System.Runtime.InteropServices" not in content:
    # insert after first using block start
    parts = content.split("\n")
    insert_at = 0
    for i, l in enumerate(parts):
        if l.startswith("using "):
            insert_at = i + 1
    parts.insert(insert_at, "using System.Runtime.InteropServices;")
    content = "\n".join(parts)
    if not content.endswith("\n"):
        content += "\n"

# Unity.Mathematics for float3/float4
if ("float3" in content or "float4" in content) and "using Unity.Mathematics" not in content:
    parts = content.split("\n")
    insert_at = 0
    for i, l in enumerate(parts):
        if l.startswith("using "):
            insert_at = i + 1
    parts.insert(insert_at, "using Unity.Mathematics;")
    content = "\n".join(parts)
    if not content.endswith("\n"):
        content += "\n"

# Color32 needs UnityEngine
if "Color32" in content and "using UnityEngine" not in content:
    parts = content.split("\n")
    insert_at = 0
    for i, l in enumerate(parts):
        if l.startswith("using "):
            insert_at = i + 1
    parts.insert(insert_at, "using UnityEngine;")
    content = "\n".join(parts)
    if not content.endswith("\n"):
        content += "\n"

backup.write_bytes(raw)
out.write_text(content, encoding="utf-8", newline="\n")

# remove region from src
new_src_lines = lines[:start] + lines[end + 1 :]
new_src = "".join(new_src_lines).replace("\r\n", "\n")
src.write_text(new_src, encoding="utf-8", newline="\n")

print(f"wrote {out}")
print(f"src lines now {new_src.count(chr(10))+1}")
print("CubeDensities out", "struct CubeDensities" in content)
print("CubeDensities src", "struct CubeDensities" in new_src)
print("MCRawVertex out", "struct MCRawVertex" in content)
print("VoxelDensityJob src", "struct VoxelDensityJob" in new_src)
print("Interop", "InteropServices" in content)
print("Mathematics", "Unity.Mathematics" in content)
