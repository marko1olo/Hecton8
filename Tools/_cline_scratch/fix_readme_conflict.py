# -*- coding: utf-8 -*-
from pathlib import Path

p = Path(r"C:\hades\Hecton8\README.md")
t = p.read_text(encoding="utf-8")
start = t.find("<<<<<<<")
mid = t.find("=======", start)
end = t.find(">>>>>>>", mid)
if start < 0 or mid < 0 or end < 0:
    print("NO_MARKERS_OR_BAD", start, mid, end)
else:
    # end of >>>>>>> line
    end_line = t.find("\n", end)
    if end_line < 0:
        end_line = len(t)
    else:
        end_line += 1
    theirs = t[mid + len("=======") : end]
    if theirs.startswith("\r\n"):
        theirs = theirs[2:]
    elif theirs.startswith("\n"):
        theirs = theirs[1:]
    t2 = t[:start] + theirs + t[end_line:]
    t2 = t2.replace("barsukdana/Hecton8", "marko1olo/Hecton8")
    t2 = t2.replace("Unity%206000.4", "Unity%206000.5").replace("Unity 6000.4", "Unity 6000.5")
    p.write_text(t2, encoding="utf-8", newline="\n")
    print("FIXED")
    print("markers_left", "<<<<<<<" in t2, ">>>>>>>" in t2)
    print("len", len(t2))
