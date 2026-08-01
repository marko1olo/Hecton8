# -*- coding: utf-8 -*-
import re
import pathlib

p = pathlib.Path(r"C:/hades/Hecton8/Docs/AgentLogs/h8_playprobe_v0_L12.log")
out = pathlib.Path(r"C:/hades/Hecton8/Tools/_cline_scratch/_l12_extract.txt")
t = p.read_text(encoding="utf-8", errors="replace")
all_lines = t.splitlines()
lines = []
lines.append("bytes=%d lines=%d" % (len(t), len(all_lines)))


def add(title, items, n=20):
    items = list(items)
    lines.append("=== %s n=%d" % (title, len(items)))
    for x in items[-n:]:
        s = x if isinstance(x, str) else str(x)
        lines.append(s[:320])
    lines.append("")


add(
    "movementIntent",
    re.findall(r".{0,60}movementIntent01max.{0,160}", t, re.I),
)
add(
    "INPUTHOP_lines",
    [ln.strip() for ln in all_lines if "INPUTHOP" in ln.upper() or "readHop" in ln or "hop2" in ln.lower()],
)
add(
    "lastOverride",
    [
        ln.strip()
        for ln in all_lines
        if "lastOverride" in ln or "OverrideMove" in ln or "overrideMove" in ln or "overrides_published" in ln
    ],
)
add(
    "Swim_lines",
    [ln.strip() for ln in all_lines if re.search(r"Swim|SWIM", ln)],
    n=40,
)
add(
    "RESULT",
    [
        ln.strip()
        for ln in all_lines
        if "RESULT" in ln or "failures=" in ln or ("PASS" in ln and "Swim" in ln) or "V0_ROUTE" in ln
    ],
)
add(
    "depth",
    [
        ln.strip()
        for ln in all_lines
        if "depth" in ln.lower()
        and ("span" in ln.lower() or "min" in ln.lower() or "max" in ln.lower() or "Depth" in ln)
    ],
)
add(
    "PHASE_swim",
    [ln.strip() for ln in all_lines if re.search(r"SwimSurface|SwimDive|PHASE.*Swim|phase=Swim", ln)],
    n=50,
)
add(
    "menu",
    [
        ln.strip()
        for ln in all_lines
        if re.search(r"pdaOpen|fabOpen|pauseOpen|IsAnyOpen|menuOpen|ForceClose|EnsureGameplay", ln)
    ],
)
add(
    "errors",
    [
        ln.strip()
        for ln in all_lines
        if re.search(r"error CS|Exception|NullReference|ABORT|error CS\d", ln)
    ],
    n=40,
)
add(
    "intent_sample",
    [
        ln.strip()
        for ln in all_lines
        if re.search(r"movementIntent|MoveDelta|intent01|PublishLocomotion|nonzero", ln, re.I)
    ],
    n=30,
)

# Tail of log
lines.append("=== TAIL 80 ===")
lines.extend(all_lines[-80:])

out.write_text("\n".join(lines), encoding="utf-8")
print("WROTE", out, "size", out.stat().st_size)
