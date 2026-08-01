# -*- coding: utf-8 -*-
import os
root = r"C:\hades\Hecton8\Assets"
outp = r"C:\hades\Hecton8\Tools\_cline_scratch\_code_hits.txt"
pats = [
    "LocomotionHold",
    "SampleGameplayLocomotion",
    "ProcessPlayerInputFrame",
    "CurrentMovementIntent01",
    "TryRecoverRuntimeStorageCold",
    "CanServiceItemAdds",
    "DescribeAddRefusalMask",
    "TryRegisterToTickManager",
    "_registeredToFixedTick",
    "STORAGE UNAVAILABLE",
    "IsToolAvailableInSlot",
    "ResolveRawInputIntentVector",
    "PrepareTransportAndFrameState",
]
hits = []
for dp, _, fs in os.walk(root):
    for f in fs:
        if not f.endswith(".cs"):
            continue
        path = os.path.join(dp, f)
        try:
            with open(path, "r", encoding="utf-8", errors="replace") as fh:
                for i, l in enumerate(fh, 1):
                    for p in pats:
                        if p in l:
                            rel = path[len(root) + 1 :]
                            hits.append("%s:%d: %s" % (rel, i, l.rstrip()[:220]))
                            break
        except Exception as e:
            hits.append("ERR %s %s" % (path, e))
text = "\n".join(hits) + "\n"
with open(outp, "w", encoding="utf-8") as fh:
    fh.write(text)
print("hits", len(hits), "chars", len(text))
print(text[:20000])
