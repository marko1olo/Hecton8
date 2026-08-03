# -*- coding: utf-8 -*-
from __future__ import annotations
from pathlib import Path
import re

path = Path(r"Assets/_Project/Scripts/RaycastBatchHelper.cs")
text = path.read_text(encoding="utf-8")
original = text

old = """    public struct QueryResult
    {
        public bool hasHit;
        public InteractionSurfaceHit hit;

        public float distance => hasHit ? hit.distance : float.MaxValue;
        public Vector3 point => hasHit ? hit.point : Vector3.zero;
        public Collider collider => hasHit ? hit.collider : null;
    }"""

new = """    public struct QueryResult
    {
        // ARM64 layout law: no runtime bool on hot result DTO. 0 = miss, 1 = hit.
        public byte hasHit;
        public InteractionSurfaceHit hit;

        public float distance => hasHit != 0 ? hit.distance : float.MaxValue;
        public Vector3 point => hasHit != 0 ? hit.point : Vector3.zero;
        public Collider collider => hasHit != 0 ? hit.collider : null;
    }"""

if old not in text:
    raise SystemExit("struct block not found exact")
text = text.replace(old, new, 1)

# bool literal assigns
text = text.replace("hasHit = false", "hasHit = 0")
text = text.replace("hasHit = true", "hasHit = 1")

# remaining hasHit = <expr> where expr not 0/1
def repl(m: re.Match[str]) -> str:
    expr = m.group(1).strip()
    if expr in ("0", "1"):
        return m.group(0)
    # skip property bodies already handled
    return f"hasHit = (byte)(({expr}) ? 1 : 0)"

text = re.sub(r"hasHit\s*=\s*([^,;\n]+)", repl, text)

if text == original:
    raise SystemExit("no change")
path.write_text(text, encoding="utf-8", newline="\n")
print("Raycast OK")
for i, line in enumerate(text.splitlines(), 1):
    if "hasHit" in line or "QueryResult" in line:
        print(f"{i}|{line}")

# external assigns of QueryResult.hasHit = true/false
root = Path(r"Assets/_Project")
for p in root.rglob("*.cs"):
    if "Editor" in p.parts or p == path:
        continue
    t = p.read_text(encoding="utf-8")
    if "RaycastBatchHelper" not in t and "QueryResult" not in t:
        continue
    # only touch .hasHit = true/false when file references RaycastBatchHelper
    if "RaycastBatchHelper" not in t:
        continue
    t2 = re.sub(r"(\.hasHit\s*=\s*)true\b", r"\g<1>1", t)
    t2 = re.sub(r"(\.hasHit\s*=\s*)false\b", r"\g<1>0", t2)
    # also comparisons like hasHit == true rare
    if t2 != t:
        p.write_text(t2, encoding="utf-8", newline="\n")
        print("fixed external", p)
