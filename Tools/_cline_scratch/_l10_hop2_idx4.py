# -*- coding: utf-8 -*-
p = r"C:\hades\Hecton8\Tools\_cline_scratch\_l10_hop2_dig4_ascii.txt"
op = r"C:\hades\Hecton8\Tools\_cline_scratch\_l10_hop2_dig4_idx.txt"
lines = open(p, encoding="ascii", errors="replace").read().splitlines()
out = []
for i, L in enumerate(lines):
    keep = (
        L.startswith("=====")
        or L.startswith("===")
        or L.startswith("FILE")
        or "_maxMovementIntent" in L
        or "LocomotionHold" in L
        or "IsPlayerInputEnabled" in L
        or "SwitchToPlayer" in L
        or "PersistRuntime" in L
        or "movementIntent" in L
        or "MoveDelta" in L
        or "GetState" in L
        or "immersion" in L.lower()
        or "UpdateWaterImmersion" in L
        or "waitingOn" in L
        or "intent01" in L
        or "SampleGameplay" in L
        or "class " in L[:30]
    )
    if keep:
        out.append("%d|%s" % (i + 1, L[:230]))
open(op, "w", encoding="utf-8").write("\n".join(out))
print(len(out))
