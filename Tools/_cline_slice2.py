import os

base = r"C:/hades/Hecton8"
path = os.path.join(base, r"Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs")
out = []
out.append("exists " + str(os.path.exists(path)))
lines = open(path, encoding="utf-8", errors="replace").read().splitlines()
out.append("total " + str(len(lines)))
keys = [
    "FrostTick",
    "LateFrameTick",
    "ExecuteDailyAudit",
    "ECOLOGY_UNAVAILABLE",
    "RunSlowTick",
    "RunFrostTick",
    "biomass",
    "Unsampled",
    "TryGetGlobalBiomass",
    "HasPending",
    "ScheduleSector",
    "DayBoundary",
    "_solveScheduled",
    "CompleteScheduled",
    "EnsurePlayerSector",
    "SeedObserver",
]
for i, l in enumerate(lines, 1):
    ll = l.lower()
    if any(k.lower() in ll for k in keys):
        out.append(f"{i}|{l}")
for start, end in [(1, 80), (250, 380), (580, 760)]:
    out.append(f"\n===== {start}-{end} =====")
    for i in range(start, min(end, len(lines)) + 1):
        out.append(f"{i}|{lines[i - 1]}")
open(os.path.join(base, "Tools/_cline_slice_out.txt"), "w", encoding="utf-8").write("\n".join(out))
print("done", len(out))
