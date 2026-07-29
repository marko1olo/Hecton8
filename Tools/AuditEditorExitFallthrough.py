"""Audit every EditorApplication.Exit(n) for a missing return - a trap that has already damaged the tree.

EditorApplication.Exit DOES NOT UNWIND THE STACK. Execution continues past it. Two real instances,
both found 2026-07-29 and both fixed:

  Scripts/Editor/ApplyTerrainMaterial.cs - the null-material branch logged "Material not found!", called
  Exit(1), and had no return, so control fell through into `t.materialTemplate = null` for EVERY terrain in
  both shipped scenes, followed by SaveScene. The most likely failure mode (a moved material) was the one
  path that stripped the material off the world and wrote that to disk. Commit ce668d517.

  Scripts/Editor/CompileShader.cs - both failure paths called Exit and fell through, so the branch that
  correctly diagnosed a missing shader then crashed instead of reporting it. Commit 5317de85a.

USAGE, from the repo root:
    python -B Tools/AuditEditorExitFallthrough.py

Exit code 0 always; this reports candidates for a human to read. A candidate is NOT automatically a
defect - an Exit at the end of a method needs no return.

TWO FILTERS THAT DECIDE WHETHER THIS TOOL IS USEFUL AT ALL, both learned the hard way on its first run,
which reported 5 candidates of which 5 were false:
  1. SAME-LINE terminator. `if (bad) { EditorApplication.Exit(2); return; }` is correct, and looking only
     at the FOLLOWING line reports every one of those as a defect.
  2. STRING literals. Tools/H8_PlayModeScreenshotter.cs builds a log message containing the API name; it
     is not a call.

Baseline established 2026-07-29 after both fixes: 165 files scanned, 2 candidates, both an `#endif`
immediately before a method's closing brace (WorldGenRegistrySmokeTester.cs:261,
VolumetricBiomeSmokeTester.cs:321) - benign, nothing executes after them. So a run reporting anything
OTHER than those two is a regression worth reading.
"""
import io
import re
import subprocess

BS = chr(92)

files = subprocess.run(
    ["rg", "-l", r"EditorApplication\.Exit\(",
     "-g", "!Library", "-g", "!Temp", "-g", "!obj", "-g", "!.git",
     "Assets/_Project"], capture_output=True, text=True).stdout.split("\n")

EXIT = re.compile(r"EditorApplication\.Exit\s*\(")
# Anything that makes falling through impossible or harmless.
TERMINAL = re.compile(r"^\s*(return\b|throw\b|\}|break\b|continue\b|goto\b)")

candidates = []
scanned = 0
for raw in files:
    path = raw.strip().replace(BS, "/")
    if not path:
        continue
    try:
        lines = io.open(path, encoding="utf-8", errors="replace").read().split("\n")
    except OSError:
        continue
    scanned += 1
    for i, line in enumerate(lines):
        s = line.strip()
        if not EXIT.search(line):
            continue
        # Skip doc comments and commented-out code - the keyword-count lesson.
        if s.startswith("//") or s.startswith("///") or s.startswith("*"):
            continue
        # Skip a mention inside a STRING literal. H8_PlayModeScreenshotter builds a log message
        # containing the API name, which is not a call at all.
        before = line[: line.index("EditorApplication.Exit")]
        if before.count('"') % 2 == 1:
            continue
        # SAME-LINE terminator: `if (bad) { EditorApplication.Exit(2); return; }` is correct, and
        # checking only the following line reports every one of those as a defect. This was the
        # difference between 5 false positives and a usable result.
        after_same_line = line[line.index("EditorApplication.Exit"):]
        if re.search(r";\s*(return\b|throw\b)", after_same_line):
            continue
        # Find the next line that actually executes.
        nxt = ""
        for j in range(i + 1, min(i + 6, len(lines))):
            cand = lines[j].strip()
            if not cand or cand.startswith("//") or cand.startswith("///") or cand.startswith("*"):
                continue
            nxt = lines[j]
            break
        if not TERMINAL.match(nxt):
            candidates.append((path, i + 1, s[:90], nxt.strip()[:70]))

print("files scanned: %d" % scanned)
print("FALL-THROUGH CANDIDATES: %d" % len(candidates))
print()
for path, ln, cur, nxt in candidates:
    print("%s:%d" % (path.split("Assets/_Project/Scripts/")[-1], ln))
    print("    exit: %s" % cur)
    print("    next: %s" % nxt)
