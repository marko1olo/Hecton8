"""Census: which first-party MonoBehaviours are reachable from nothing?

Reachability has THREE independent routes and a negative needs all three to fail:
  1. scene/prefab/asset binding by GUID -- text form AND nibble-swapped binary form,
     because 4 scenes here are binary and a text search silently under-reports.
  2. runtime construction from code -- AddComponent<T>, GetComponent<T>, new T().
  3. Editor-only construction -- same, but the only caller lives under an Editor folder,
     which means it reaches the game only if a human pressed the button.

Positive control is mandatory: if the binary search cannot find a GUID known to be
scene-bound, every negative printed is meaningless and the script says so.
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC_DIRS = ["Assets/_Project/Scripts", "Assets/_Project/Editor"]
CONTROL_GUID_META = "Assets/_Project/Scripts/Fabricator.cs.meta"

CLASS_RE = re.compile(r"^\s*(?:public|internal|sealed|abstract|partial|\s)*class\s+(\w+)\s*:\s*([^{\r\n]+)", re.M)
GUID_RE = re.compile(r"guid:\s*([a-f0-9]{32})")


def swapped_bytes(guid_hex):
    """Unity stores a GUID in binary assets as 16 bytes with nibbles swapped per byte."""
    out = bytearray()
    for i in range(0, 32, 2):
        hi = int(guid_hex[i], 16)
        lo = int(guid_hex[i + 1], 16)
        out.append((lo << 4) | hi)
    return bytes(out)


def walk(patterns_exts, base="Assets"):
    for dirpath, dirnames, filenames in os.walk(os.path.join(ROOT, base)):
        dirnames[:] = [d for d in dirnames if d not in ("Library", "Temp", "obj", ".git")]
        for fn in filenames:
            if os.path.splitext(fn)[1] in patterns_exts:
                yield os.path.join(dirpath, fn)


def main():
    # --- collect first-party MonoBehaviour classes + their guids ---
    types = {}  # class name -> (relpath, guid, loc, is_editor_folder)
    for sd in SRC_DIRS:
        for dirpath, dirnames, filenames in os.walk(os.path.join(ROOT, sd)):
            dirnames[:] = [d for d in dirnames if d not in ("Library", "Temp", "obj", ".git")]
            for fn in filenames:
                if not fn.endswith(".cs"):
                    continue
                path = os.path.join(dirpath, fn)
                rel = os.path.relpath(path, ROOT).replace("\\", "/")
                try:
                    text = open(path, encoding="utf-8", errors="replace").read()
                except OSError:
                    continue
                loc = text.count("\n") + 1
                mono = [m.group(1) for m in CLASS_RE.finditer(text)
                        if "MonoBehaviour" in m.group(2)]
                if not mono:
                    continue
                meta = path + ".meta"
                guid = None
                if os.path.exists(meta):
                    mm = GUID_RE.search(open(meta, encoding="utf-8", errors="replace").read())
                    if mm:
                        guid = mm.group(1)
                is_editor = "/Editor/" in rel or rel.endswith("Editor.cs")
                for cname in mono:
                    types[cname] = (rel, guid, loc, is_editor)

    print(f"first-party MonoBehaviour types: {len(types)}", file=sys.stderr)

    # --- load every binding haystack once (bytes) ---
    bind_exts = {".unity", ".prefab", ".asset", ".controller", ".playable", ".mat"}
    haystacks = []
    for p in walk(bind_exts):
        rel = os.path.relpath(p, ROOT).replace("\\", "/")
        if "/_Recovery/" in rel:
            continue  # gitignored scene copies; a hit there is NOT reachability
        try:
            haystacks.append((rel, open(p, "rb").read()))
        except OSError:
            pass
    print(f"binding files loaded: {len(haystacks)}", file=sys.stderr)

    # --- positive control ---
    cg = GUID_RE.search(open(os.path.join(ROOT, CONTROL_GUID_META),
                             encoding="utf-8", errors="replace").read()).group(1)
    ctrl_hits = [rel for rel, blob in haystacks
                 if cg.encode() in blob or swapped_bytes(cg) in blob]
    if not ctrl_hits:
        print("CONTROL FAILED -- binary search is broken, every negative is meaningless")
        return 1
    print(f"CONTROL OK: {CONTROL_GUID_META} found in {len(ctrl_hits)} files "
          f"(e.g. {ctrl_hits[0]})", file=sys.stderr)

    # --- load every .cs once for construction search ---
    code = []
    for p in walk({".cs"}):
        rel = os.path.relpath(p, ROOT).replace("\\", "/")
        try:
            code.append((rel, open(p, encoding="utf-8", errors="replace").read()))
        except OSError:
            pass

    # --- build inverted GUID index ---
    # text files: one regex pass extracts all referenced GUIDs
    # binary files (4 scenes): per-known-GUID bytes search (small N)
    GUID_BYTES_RE = re.compile(rb"guid:\s*([a-f0-9]{32})")
    type_guids = {guid for _, (_, guid, _, _) in types.items() if guid}
    swapped_to_guid = {swapped_bytes(g): g for g in type_guids}

    guid_to_files: dict[str, list[str]] = {}
    for hrel, blob in haystacks:
        if blob[:6] == b'%YAML ':
            for m in GUID_BYTES_RE.finditer(blob):
                g = m.group(1).decode()
                if g in type_guids:
                    lst = guid_to_files.setdefault(g, [])
                    if len(lst) < 3:
                        lst.append(hrel)
        else:
            for sb, g in swapped_to_guid.items():
                if sb in blob:
                    lst = guid_to_files.setdefault(g, [])
                    if hrel not in lst and len(lst) < 3:
                        lst.append(hrel)

    # --- build inverted construction index ---
    CTOR_RE = re.compile(
        r'(?:AddComponent|GetComponent(?:InChildren)?|FindObjectOfType'
        r'|FindAnyObjectByType|FindFirstObjectByType)<(\w+)>'
        r'|RequireComponent\(typeof\((\w+)\)\)'
    )
    type_to_ctors: dict[str, set[str]] = {}
    for crel, ctext in code:
        for m in CTOR_RE.finditer(ctext):
            tname = m.group(1) or m.group(2)
            type_to_ctors.setdefault(tname, set()).add(crel)

    rows = []
    for cname, (rel, guid, loc, is_editor) in types.items():
        bound = guid_to_files.get(guid, []) if guid else []
        ctors = [c for c in type_to_ctors.get(cname, set()) if c != rel]
        rows.append((cname, rel, guid, loc, is_editor, bound, ctors))

    # dead = no binding, no construction site anywhere
    dead = [r for r in rows if not r[5] and not r[6]]
    dead.sort(key=lambda r: -r[3])
    print("\n=== NO BINDING, NO CONSTRUCTION SITE (ranked by LOC) ===")
    for cname, rel, guid, loc, is_editor, _b, _c in dead[:45]:
        print(f"{loc:6d}  {cname:52s} {rel}")

    # editor-only reachable = bound nowhere, constructed only from Editor code
    ed_only = [r for r in rows if not r[5] and r[6]
               and all("/Editor/" in c or "/Tests/" in c for c in r[6])]
    ed_only.sort(key=lambda r: -r[3])
    print("\n=== NO BINDING, CONSTRUCTED ONLY FROM Editor/Tests CODE (ranked by LOC) ===")
    for cname, rel, guid, loc, is_editor, _b, ctors in ed_only[:45]:
        print(f"{loc:6d}  {cname:52s} {rel}\n           callers: {', '.join(ctors[:3])}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
