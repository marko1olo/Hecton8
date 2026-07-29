#!/usr/bin/env python
"""Is an asset or script actually REFERENCED by any scene, prefab or asset? Text and binary, with a control.

WHY THIS EXISTS
---------------
A plain text search for a 32-hex GUID is COMPLETELY BLIND to a binary Unity scene, and three scenes in this
repo are binary - including the world scene. Measured here: of 400 sampled script GUIDs, the text form
matched the binary world scene 0 times while the correct binary form matched 11. So a text-only search
reports every binary-scene reference as absent, and this project has already had a headline retracted for
exactly that mistake.

Unity stores a GUID in a binary asset as 16 bytes with the NIBBLES SWAPPED within each byte relative to the
text form. Neither the ASCII text nor the plain 16 raw bytes will find it:

    text   "8766f3ea..."  -> 0 hits in a binary scene
    raw16  bytes.fromhex  -> 0 hits
    nibble-swapped        -> matches

MANDATORY POSITIVE CONTROL
--------------------------
The script always resolves a GUID that is KNOWN to be scene-referenced and reports whether it was found. If
the control returns 0, the method is broken and every negative result printed is meaningless - the script
says so rather than letting a false absence look like a finding. Pick a control that is SCENE-BOUND, not one
that is instantiated from code: SystemDispatcher was tried first and returns 0 legitimately, because
GameBootstrapper creates it with AddComponent and no scene ever names it.

USAGE, from the repo root:

    python -B Tools/AuditGuidReachability.py <path.meta> [more.meta ...]
    python -B Tools/AuditGuidReachability.py --fauna        # the 22 authored creature templates

A zero-reference result means only that nothing BINDS it. Code may still create it at runtime via
AddComponent, Resources.Load or Addressables - check for those before calling anything dead. The last time
something in this repo was declared dead on a reachability walk, a tool deleted a node from the authored
world graph.
"""
import glob
import os
import re
import sys

CONTROL_META = "Assets/_Project/Scripts/AtlasSignal/AtlasSignalSystem.cs.meta"

HAYSTACK_PATTERNS = (
    "Assets/_Project/Scenes/*.unity",
    "Assets/_Project/Prefabs/**/*.prefab",
    "Assets/_Project/Data/**/*.asset",
    "Assets/_Project/Resources/**/*",
)


def guid_of(meta_path):
    try:
        text = open(meta_path, encoding="utf-8", errors="replace").read()
    except OSError:
        return None
    match = re.search(r"guid:\s*([a-f0-9]{32})", text)
    return match.group(1) if match else None


def search_forms(guid):
    raw = bytes.fromhex(guid)
    nibble_swapped = bytes((((b & 0x0F) << 4) | ((b >> 4) & 0x0F)) for b in raw)
    return (guid.encode("ascii"), nibble_swapped)


def load_haystack():
    paths = []
    for pattern in HAYSTACK_PATTERNS:
        paths += [p for p in glob.glob(pattern, recursive=True)
                  if os.path.isfile(p) and not p.endswith(".meta")]
    blobs = []
    for path in paths:
        try:
            blobs.append((path, open(path, "rb").read()))
        except OSError:
            pass
    return blobs


def reach(blobs, guid):
    forms = search_forms(guid)
    hits = []
    for path, data in blobs:
        for form in forms:
            if form in data:
                hits.append(path)
                break
    return hits


def main():
    args = sys.argv[1:]
    if not args:
        print(__doc__)
        raise SystemExit(2)

    if args == ["--fauna"]:
        args = sorted(glob.glob("Assets/_Project/Data/Fauna/FaunaDataTemplate_*.asset.meta"))

    blobs = load_haystack()
    binary_scenes = []
    for path in glob.glob("Assets/_Project/Scenes/*.unity"):
        with open(path, "rb") as handle:
            if handle.read(9) == b"\x00" * 9:
                binary_scenes.append(os.path.basename(path))

    print("haystack: %d files" % len(blobs))
    print("BINARY scenes, where a text GUID search is blind: %s" % (", ".join(binary_scenes) or "none"))

    control_guid = guid_of(CONTROL_META)
    control_hits = reach(blobs, control_guid) if control_guid else []
    print("CONTROL %s -> %d hit(s): %s"
          % (os.path.basename(CONTROL_META), len(control_hits),
             "METHOD OK" if control_hits else "METHOD BROKEN, every negative below is MEANINGLESS"))
    print()

    unreached = []
    for meta in args:
        guid = guid_of(meta)
        name = os.path.basename(meta)
        if not guid:
            print("%-52s NO GUID IN META" % name[:52])
            continue
        hits = reach(blobs, guid)
        print("%-52s %d reference(s)" % (name[:52], len(hits)))
        for hit in hits[:3]:
            print("      %s" % hit)
        if not hits:
            unreached.append(name)

    print()
    print("UNREFERENCED by any scene/prefab/asset (text OR binary): %d of %d"
          % (len(unreached), len(args)))
    if unreached and not control_hits:
        print("...but the control failed, so treat that count as unknown, not as a finding.")


if __name__ == "__main__":
    main()
