# -*- coding: utf-8 -*-
from pathlib import Path

d = Path(r"C:\hades\Hecton8\Logs\headless_ecology_fence_5day.log").read_text(
    encoding="utf-8", errors="replace"
)
keys = (
    "lifecycle",
    "Lifecycle",
    "ecology",
    "Ecology",
    "ECOLOGY",
    "timeDilation",
    "dilation",
    "BATCH",
    "timeout",
    "Timeout",
    "heartbeat",
    "Fauna",
    "PlayMode",
    "runner",
    "Runner",
    "sampled",
    "fence",
    "Frost",
    "Bootstrap complete",
    "bootstrap complete",
    "READY",
    "CompleteAfter",
    "PollRun",
    "H8Headless",
    "h8headless",
    "Day ",
    "daySeconds",
    "ecologySampled",
    "LogError",
    "H8Debug",
    "EXCEPTION",
    "Exception",
    "error:",
    "Error:",
)
lines = d.splitlines()
hits = []
for i, l in enumerate(lines):
    if any(k in l for k in keys):
        if "USB" in l or "Scanning for USB" in l:
            continue
        hits.append((i + 1, l.strip()))
print("hits", len(hits))
for n, l in hits:
    print(f"{n}:{l[:240]}")

print("---OUT---")
out = Path(r"C:\hades\Hecton8\Tools\_cline_geo_retshape_v3_out.txt")
run = Path(r"C:\hades\Hecton8\Tools\_cline_geo_retshape_v3_run.txt")
if out.exists():
    print(out.read_text(encoding="utf-8", errors="replace")[-3000:])
else:
    print("OUT_MISSING")
    print(run.read_text(encoding="utf-8", errors="replace")[-500:] if run.exists() else "")
