import os, re, sys
os.chdir(r"C:\hades\Hecton8")
print("cwd", os.getcwd())
disp = None
for root, ds, fs in os.walk(r"Assets\_Project\Scripts"):
    for f in fs:
        if f == "SystemDispatcher.cs":
            disp = os.path.join(root, f)
print("DISP", disp)
if not disp:
    sys.exit(1)
text = open(disp, encoding="utf-8", errors="replace").read()
keys = [
    "IsOriginShiftBootstrapLocked",
    "RunFrostTick",
    "RunDispatcherUpdate",
    "RunFastTick",
    "RequestHeadlessTimeDilation",
    "_originShiftBootstrapLock",
    "BootstrapLocked",
    "MasterSim",
    "GameReady",
    "RunSlowTick",
    "_frost",
    "IFrostTickable",
]
for k in keys:
    idxs = [m.start() for m in re.finditer(re.escape(k), text)]
    print(k, "count", len(idxs))
    for i in idxs[:12]:
        line = text.count("\n", 0, i) + 1
        print(" ", line)

# dump RunDispatcherUpdate region
m = re.search(r"void RunDispatcherUpdate\b", text)
if m:
    start = text.rfind("\n", 0, m.start()) + 1
    line0 = text.count("\n", 0, start) + 1
    # print ~200 lines
    chunk = text[start : start + 12000]
    lines = chunk.splitlines()[:220]
    out = r"C:\hades\Hecton8\_agent_probe_frost_out.txt"
    with open(out, "w", encoding="utf-8") as w:
        w.write(f"=== RunDispatcherUpdate around line {line0} ===\n")
        for i, ln in enumerate(lines):
            w.write(f"{line0+i}|{ln}\n")
        # also find RunFrostTick body
        for pat, name in [
            (r"void RunFrostTick\b", "RunFrostTick"),
            (r"internal static bool IsOriginShiftBootstrapLocked", "IsOriginShiftBootstrapLocked"),
            (r"static bool IsOriginShiftBootstrapLocked", "IsOriginShiftBootstrapLocked2"),
            (r"RequestHeadlessTimeDilation", "RequestHeadlessTimeDilation"),
            (r"bool.*MasterSim", "MasterSim"),
        ]:
            mm = re.search(pat, text)
            if not mm:
                w.write(f"\n=== {name} NOT FOUND ===\n")
                continue
            s = text.rfind("\n", 0, mm.start()) + 1
            l0 = text.count("\n", 0, s) + 1
            ch = text[s : s + 8000]
            ls = ch.splitlines()[:120]
            w.write(f"\n=== {name} around line {l0} ===\n")
            for i, ln in enumerate(ls):
                w.write(f"{l0+i}|{ln}\n")
    print("wrote", out)
