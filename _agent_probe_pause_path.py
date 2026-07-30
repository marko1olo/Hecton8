import os
os.chdir(r"C:\hades\Hecton8")
disp = open(r"Assets\_Project\Scripts\Core\SystemDispatcher.cs", encoding="utf-8", errors="replace").read().splitlines()
# dump regions
ranges = [
    (4660, 4720),
    (4988, 5020),
    (6695, 6760),
    (100, 120),
    (815, 830),
    (2080, 2110),
    (5575, 5610),
]
out = []
for a, b in ranges:
    out.append(f"=== {a}-{b} ===")
    for i in range(a - 1, min(b, len(disp))):
        out.append(f"{i+1}|{disp[i]}")
    out.append("")
open(r"C:\hades\Hecton8\_agent_probe_pause_path_out.txt", "w", encoding="utf-8").write("\n".join(out))
print("ok")

# search log for pause/dilation/timeScale
log = r"Docs\AgentLogs\headless_smoke_20260731_p0_fo_lock_drain_20260730_213321.log"
keys = ("TimeDilation", "time dilation", "SimulationPause", "paused", "dilation scalar", "timeScale", "Frost", "RunDispatcher")
hits = []
with open(log, encoding="utf-8", errors="replace") as f:
    for i, line in enumerate(f, 1):
        low = line.lower()
        if any(k.lower() in low for k in keys):
            hits.append(f"{i}|{line.rstrip()[:200]}")
open(r"C:\hades\Hecton8\_agent_probe_pause_log.txt", "w", encoding="utf-8").write("\n".join(hits[:100] + ["..."] + hits[-50:]))
print("pause log hits", len(hits))
