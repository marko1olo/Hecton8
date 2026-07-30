# scratch — do not commit
from pathlib import Path

log_path = Path(r"C:\hades\Hecton8\Docs\AgentLogs\v0_kcc_gate_2026-07-30.log")
json_path = Path(r"C:\hades\Hecton8\Docs\AgentLogs\H8_V0_PLAYTEST_SMOKE_GATE.json")
out_path = Path(r"C:\hades\Hecton8\Tools\_cline_scratch\gate_log_hits.txt")

log = log_path.read_text(encoding="utf-8", errors="replace")
lines = log.splitlines()
o = [f"lines={len(lines)} size={len(log)}"]
needles = (
    "H8_V0",
    "FAIL",
    "PASS",
    "ErrorFlags",
    "0x00000042",
    "PrecisionDrift",
    "Shinobu",
    "Smoke",
    "EXCEPTION",
    "executeMethod",
    "ExecuteMethod",
    "Failure",
    "flags",
    "kcc",
    "KCC",
)
for i, L in enumerate(lines):
    if any(n in L for n in needles):
        if "UnityEngine." in L or "(at " in L or "Filename:" in L:
            continue
        o.append(f"{i+1}|{L[:320]}")

o.append("--- JSON ---")
if json_path.exists():
    o.append(json_path.read_text(encoding="utf-8", errors="replace"))

# decode 0x42 = bit1 + bit6 = 2 + 64
o.append("--- FLAG DECODE 0x42 ---")
o.append("bit0=1 bit1=2 bit2=4 bit3=8 bit4=16 bit5=32 bit6=64 bit7=128")
o.append("0x42 = 66 = bit1(2) + bit6(64)")

out_path.write_text("\n".join(o), encoding="utf-8")
print(f"WROTE {out_path} n={len(o)}")
