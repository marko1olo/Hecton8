# -*- coding: utf-8 -*-
from pathlib import Path

lines = Path(r"C:\hades\Hecton8\Logs\headless_ecology_fence_5day.log").read_text(
    encoding="utf-8", errors="replace"
).splitlines()

# Print ranges with actual message content
for start, end in [(760, 870), (2450, 2620), (2810, 2870), (3140, 3185)]:
    print(f"===== {start}-{end} =====")
    for i in range(start - 1, min(end, len(lines))):
        print(f"{i+1}:{lines[i][:240]}")

out = Path(r"C:\hades\Hecton8\Tools\_cline_geo_retshape_v3_out.txt")
run = Path(r"C:\hades\Hecton8\Tools\_cline_geo_retshape_v3_run.txt")
print("===== V3 =====")
if out.exists():
    print(out.read_text(encoding="utf-8", errors="replace")[-3500:])
else:
    print("OUT_MISSING run_sz", run.stat().st_size if run.exists() else 0)
    print(run.read_text(encoding="utf-8", errors="replace")[-400:] if run.exists() else "")
