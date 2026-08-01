# -*- coding: utf-8 -*-
p = r"C:\hades\Hecton8\Tools\_cline_scratch\_l10_hop2_dig3_ascii.txt"
op = r"C:\hades\Hecton8\Tools\_cline_scratch\_l10_hop2_dig3_idx.txt"
lines = open(p, encoding="ascii", errors="replace").read().splitlines()
out = []
for i, L in enumerate(lines):
    if (
        L.startswith("=====")
        or L.startswith("FILE")
        or L.startswith("WD=")
        or L.startswith("SWIM")
        or L.startswith("J|")
        or "INPUTHOP" in L
        or "waitingOn" in L
        or "LocomotionHold" in L
        or "movementIntent" in L
        or "IsPlayerInputEnabled" in L
        or "PersistRuntime" in L
        or "DontDestroy" in L
        or "nativeInput" in L
        or "_nativeInputManager" in L
        or "GetState" in L
        or "NoOp" in L
        or "publishOk" in L
        or "readHop" in L
    ):
        out.append("%d|%s" % (i + 1, L[:220]))
open(op, "w", encoding="utf-8").write("\n".join(out))
print("idx", len(out), "lines")
