#!/usr/bin/env python3
from pathlib import Path

p = Path(r"C:\hades\Hecton8\Docs\AgentLogs\worldroot_report_2026-07-30.log")
t = p.read_text(encoding="utf-8", errors="replace")
lines = t.splitlines()
print("size", len(t), "lines", len(lines))

# dump any line mentioning repair / world root / report
needles = (
    "H8_WorldRoot",
    "Graveyard",
    "GraveyardRepair",
    "ReportOnly",
    "Opening scene",
    "--- WORLD",
    "descendant",
    "activeRoot",
    "APPLY",
    "REPORT mode",
    "mode=",
    "unparent",
    "VERDICT",
    "SUMMARY",
    "No changes",
    "Saved Scene",
    "executeMethod",
    "Exiting batchmode",
    "Aborting batchmode",
    "Scripts have compiler errors",
    "error CS",
)

print("=== MATCHES ===")
for i, line in enumerate(lines, 1):
    if any(n in line for n in needles):
        print(f"{i}:{line[:320]}")

print("=== AROUND Opening scene ===")
for i, line in enumerate(lines):
    if "Opening scene" in line or "H8_WorldRoot" in line:
        start = max(0, i - 3)
        end = min(len(lines), i + 50)
        print(f"---- block at {i+1} ----")
        for j in range(start, end):
            print(f"{j+1}:{lines[j][:320]}")

# Also search historical report for comparison pattern
old = Path(r"C:\hades\Hecton8\Docs\AgentLogs\worldroot_report.log")
if old.exists():
    ot = old.read_text(encoding="utf-8", errors="replace")
    print("=== OLD REPORT sample matches ===")
    for i, line in enumerate(ot.splitlines(), 1):
        if any(n in line for n in ("H8_WorldRoot", "Graveyard", "REPORT", "descendant", "active")):
            if i < 2000:
                print(f"OLD {i}:{line[:240]}")
