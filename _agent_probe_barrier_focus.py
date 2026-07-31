import os
from pathlib import Path

os.chdir(r"C:\hades\Hecton8")
text = Path("_agent_probe_barrier_out.txt").read_text(encoding="utf-8", errors="replace")
out = Path("_agent_probe_barrier_focus.txt")
keys = [
    "TimeDilation constants",
    "ShouldSkipLaneDuringBootstrap",
    "blockGameplayLanes",
    "RequestSimulationPause",
    "Pause request",
    "_simulationPaused",
    "_timeDilationScalar writes",
    "StressFractureBot",
    "ResolveDispatcherUnscaled",
    "DrainSimulationPause",
]
idx = []
for k in keys:
    i = text.find(k)
    if i >= 0:
        idx.append((i, k))
idx.sort()
chunks = []
for n, (i, k) in enumerate(idx):
    end = idx[n + 1][0] if n + 1 < len(idx) else min(len(text), i + 4000)
    chunks.append("######## " + k + "\n" + text[i:end][:4000] + "\n")
out.write_text("\n".join(chunks), encoding="utf-8")
print("WROTE", out, out.stat().st_size)
