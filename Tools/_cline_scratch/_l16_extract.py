# -*- coding: utf-8 -*-
import re
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

LOG = r"C:\hades\Hecton8\Docs\AgentLogs\h8_playprobe_v0_L16.log"
OUT = r"C:\hades\Hecton8\Tools\_cline_scratch\_l16_extract.txt"

with open(LOG, encoding="utf-8", errors="replace") as f:
    t = f.read()

lines_out = []
lines_out.append(f"bytes={len(t)} lines={t.count(chr(10))}")

def add(section, items):
    lines_out.append(f"--- {section} ---")
    if not items:
        lines_out.append("(none)")
    else:
        for x in items:
            lines_out.append(x[:400] if isinstance(x, str) else str(x)[:400])

add("SIMCLOCK", [ln for ln in t.splitlines() if "SIMCLOCK" in ln])
add("RESULT", [ln for ln in t.splitlines() if "RESULT" in ln and "H8_PLAYPROBE" in ln])
add("MOMENT", [ln for ln in t.splitlines() if "MOMENT" in ln])
add("RequiredRoute", [ln for ln in t.splitlines() if "Required Route" in ln or "RequiredRoute" in ln or "Swim" in ln])
add("movementIntent", [ln for ln in t.splitlines() if "movementIntent" in ln])
add("FAIL_H8", [ln for ln in t.splitlines() if "FAIL" in ln and ("H8_PLAYPROBE" in ln or "PLAYPROBE" in ln or "ROUTE" in ln)])

hops = [ln for ln in t.splitlines() if "INPUTHOP" in ln]
add(f"INPUTHOP count={len(hops)} head", hops[:20])
add("INPUTHOP tail", hops[-15:])

# hop census by hop id
hop_ids = {}
for ln in hops:
    m = re.search(r"hop[=:](\d+)", ln, re.I)
    if m:
        hop_ids[m.group(1)] = hop_ids.get(m.group(1), 0) + 1
add("hop_id_counts", [f"hop={k}: {v}" for k, v in sorted(hop_ids.items())])

# GetState / hop2
gs = [ln for ln in t.splitlines() if re.search(r"GetState|hop[=:]2\b|hop2|readHop=2|DiagRecordReadObservation", ln)]
add(f"GetState_hop2 count={len(gs)}", gs[:30])

# FixedTick / DispatchFixedStep
ft = [ln for ln in t.splitlines() if re.search(r"FixedTick|DispatchFixedStep|RunFixedStep|stepBound|StepBounded|dilatedDelta", ln, re.I)]
add(f"FixedTickish count={len(ft)}", ft[:40])

# menu block
mb = [ln for ln in t.splitlines() if re.search(r"menu|InputBlocked|GameplayInputBlocked|SampleGameplay|blocked", ln, re.I) and ("H8_" in ln or "INPUT" in ln or "PLAYPROBE" in ln or "HPM" in ln or "locomotion" in ln.lower())]
add(f"menu_blockish count={len(mb)}", mb[:40])

# WORLDDRIVER
wd = [ln for ln in t.splitlines() if "WORLDDRIVER" in ln or "WorldDriver" in ln]
add(f"WORLDDRIVER count={len(wd)}", wd[:15] + ["..."] + wd[-10:] if len(wd) > 25 else wd)

# currentStateMove metric lines
csm = [ln for ln in t.splitlines() if "currentStateMove" in ln]
add(f"currentStateMove count={len(csm)}", csm[:20] + csm[-10:])

# census / summary style lines from probe
for key in ["INPUT_CENSUS", "LOCOMOTION", "depthSpan", "playerLane", "HPM", "suitReady", "PublishLocomotion", "override"]:
    xs = [ln for ln in t.splitlines() if key in ln]
    if xs:
        add(f"key={key} n={len(xs)}", xs[:8] + (xs[-5:] if len(xs) > 8 else []))

text = "\n".join(lines_out) + "\n"
with open(OUT, "w", encoding="utf-8") as f:
    f.write(text)
print(text[:15000])
print(f"\nWROTE {OUT} total_chars={len(text)}")
