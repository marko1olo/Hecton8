# -*- coding: utf-8 -*-
from pathlib import Path
import re

files = {
    "runtime": Path(r"Assets/_Project/Scripts/HectonVoxelEngine.cs"),
    "mctables": Path(r"Assets/_Project/Scripts/HectonVoxelEngine.MCTables.cs"),
    "jobs": Path(r"Assets/_Project/Scripts/HectonVoxelEngine.Jobs.cs"),
    "editor": Path(r"Assets/_Project/Scripts/HectonVoxelEngine.Editor.cs"),
    "backup": Path(r"Docs/AgentLogs/_backup_HectonVoxelEngine_pre_split.cs"),
}
out = []
for k, p in files.items():
    if not p.exists():
        out.append(f"MISSING {k}")
        continue
    t = p.read_text(encoding="utf-8", errors="replace")
    lines = t.splitlines()
    out.append(f"=== {k} {p.name} lines={len(lines)} bytes={p.stat().st_size}")
    # key symbols
    for pat in [
        r"class MCTables",
        r"class HectonVoxelEngine\b",
        r"class HectonVoxelEngineEditor",
        r"struct \w+Job",
        r"IJob",
        r"BurstCompile",
        r"namespace ",
        r"using ",
    ]:
        hits = len(re.findall(pat, t))
        if hits:
            out.append(f"  {pat}: {hits}")
    # first non-empty / class lines
    for i, l in enumerate(lines[:40], 1):
        if l.strip():
            out.append(f"  L{i}: {l[:100]}")
    # last 10
    out.append("  ...tail...")
    for i, l in enumerate(lines[-8:], len(lines) - 7):
        out.append(f"  L{i}: {l[:100]}")

# integrity: backup line count vs sum
if files["backup"].exists():
    b = files["backup"].read_text(encoding="utf-8", errors="replace").splitlines()
    # compare presence of unique markers
    markers = [
        "public static class MCTables",
        "public sealed class HectonVoxelEngine",
        "HectonVoxelEngineEditor",
        "IJobParallelFor",
        "DeferredVoxelPhysicsBakeTeardownDriver",
        "DeferredVoxelColliderUploadDriver",
    ]
    out.append("\n=== MARKER PLACEMENT ===")
    for m in markers:
        places = []
        for k, p in files.items():
            if k == "backup":
                continue
            if not p.exists():
                continue
            tt = p.read_text(encoding="utf-8", errors="replace")
            if m in tt:
                places.append(k)
        in_backup = m in files["backup"].read_text(encoding="utf-8", errors="replace")
        out.append(f"  {m}: {places} backup={in_backup}")

    # Check no duplicate type defs across split files
    out.append("\n=== DUPLICATE TYPE CHECK ===")
    type_pat = re.compile(r"\b(class|struct)\s+(\w+)")
    seen = {}
    for k, p in files.items():
        if k == "backup" or not p.exists():
            continue
        for m in type_pat.finditer(p.read_text(encoding="utf-8", errors="replace")):
            name = m.group(2)
            if name in ("var",):
                continue
            seen.setdefault(name, []).append(k)
    dups = {n: v for n, v in seen.items() if len(v) > 1}
    if dups:
        out.append(f"DUPLICATES: {dups}")
    else:
        out.append("No duplicate type names across split files")

# accessibility: MCTables public?
rt = files["runtime"].read_text(encoding="utf-8", errors="replace") if files["runtime"].exists() else ""
mt = files["mctables"].read_text(encoding="utf-8", errors="replace") if files["mctables"].exists() else ""
out.append(f"\nMCTables public static: {'public static class MCTables' in mt}")
out.append(f"Runtime refs MCTables: {rt.count('MCTables')}")
jt = files["jobs"].read_text(encoding="utf-8", errors="replace") if files["jobs"].exists() else ""
out.append(f"Jobs refs MCTables: {jt.count('MCTables')}")

# Check jobs file starts with valid C#
out.append(f"\nJobs starts with using: {jt.lstrip().startswith('//') or jt.lstrip().startswith('using')}")
out.append(f"Runtime has sealed class: {'sealed class HectonVoxelEngine' in rt}")

text = "\n".join(out)
# ascii
text = "".join(c if ord(c) < 128 else "?" for c in text)
Path(r"Docs/AgentLogs/_voxel_verify.txt").write_text(text, encoding="ascii")
print(text)
