#!/usr/bin/env python3
"""One-shot screenshot + unity log probe. Not a mock; analysis only."""
from pathlib import Path
import subprocess
import sys

shots = [
    r"C:\hades\Hecton8\Docs\Screenshots\1428_02_HECTON_WORLD_gameview.png",
    r"C:\hades\Hecton8\Docs\Screenshots\1428_02_world_play_after_player_authoring.png",
    r"C:\hades\Hecton8\Docs\Screenshots\1428_menu_to_world_route_result.png",
    r"C:\hades\Hecton8\Docs\Screenshots\fresh_world_after_descend_1428.png",
    r"C:\hades\Hecton8\Docs\Screenshots\world_after_menu_descend_runtime_1428.png",
    r"C:\hades\Hecton8\Docs\Screenshots\1428_new_dive_route_world_clean_final.png",
    r"C:\hades\Hecton8\Docs\Screenshots\1428_playmode_descend_world.png",
    r"C:\hades\Hecton8\Docs\Screenshots\world_direct_descend_1428.png",
    r"C:\hades\Hecton8\Docs\Screenshots\1428_world_02_play_surface_runtime_after_compilefix.png",
    r"C:\hades\Hecton8\Docs\Screenshots\h8_02_world_after_water_cloud_01.png",
]

try:
    from PIL import Image
    has_pil = True
except Exception as e:
    has_pil = False
    print("PIL unavailable:", e)

print("=== SCREENSHOT ANALYSIS ===")
for p in shots:
    path = Path(p)
    if not path.exists():
        print("MISS", path.name)
        continue
    st = path.stat()
    from datetime import datetime
    mtime = datetime.fromtimestamp(st.st_mtime).strftime("%Y-%m-%d %H:%M")
    if not has_pil:
        print(f"OK {path.name} bytes={st.st_size} mtime={mtime}")
        continue
    im = Image.open(path).convert("RGB")
    w, h = im.size
    pts = [
        (w // 2, h // 2),
        (10, 10),
        (w - 10, 10),
        (10, h - 10),
        (w - 10, h - 10),
        (w // 2, int(h * 0.3)),
        (w // 2, int(h * 0.7)),
    ]
    lumas = []
    for x, y in pts:
        r, g, b = im.getpixel((x, y))
        lumas.append(0.299 * r + 0.587 * g + 0.114 * b)
    avg = sum(lumas) / len(lumas)
    dark = sum(1 for L in lumas if L < 15)
    bright = sum(1 for L in lumas if L > 240)
    step = max(1, min(w, h) // 20)
    vals = []
    for yy in range(0, h, step):
        for xx in range(0, w, step):
            vals.append(im.getpixel((xx, yy)))
    ar = sum(v[0] for v in vals) / len(vals)
    ag = sum(v[1] for v in vals) / len(vals)
    ab = sum(v[2] for v in vals) / len(vals)
    # classify rough content
    if avg < 12 and dark >= 5:
        kind = "NEAR_BLACK_EMPTY_OR_LOADING"
    elif bright >= 5 and avg > 230:
        kind = "NEAR_WHITE_BLOOM_OR_UI"
    elif ab > ar + 20 and ab > ag + 10:
        kind = "BLUE_WATER_OR_SKY_DOMINANT"
    elif ag > ar + 15 and ag > ab + 10:
        kind = "GREEN_TERRAIN_OR_BIOME"
    elif ar > 180 and ag > 150 and ab < 120:
        kind = "WARM_SURFACE_OR_SUN"
    else:
        kind = "MIXED_SCENE"
    # PLAYER proof cannot be claimed from stills alone
    print(
        f"OK {path.name} | {w}x{h} | {st.st_size/1024:.1f}KB | mtime={mtime} | "
        f"avgL={avg:.1f} dark={dark} bright={bright} meanRGB=({ar:.0f},{ag:.0f},{ab:.0f}) | {kind}"
    )

print("\n=== UNITY LOG TAIL ===")
log = Path(r"C:\hades\Hecton8\Docs\AgentLogs\worldroot_report_2026-07-30.log")
if log.exists():
    data = log.read_text(encoding="utf-8", errors="replace")
    print(f"LOG size={len(data)} mtime={datetime.fromtimestamp(log.stat().st_mtime)}")
    # key lines
    keys = (
        "ReportOnly",
        "WORLD",
        "Graveyard",
        "active",
        "descendant",
        "error",
        "Error",
        "Exception",
        "SUCCESS",
        "FAIL",
        "root",
        "executeMethod",
        "Batchmode",
        "quit",
    )
    hits = []
    for i, line in enumerate(data.splitlines(), 1):
        if any(k in line for k in keys):
            hits.append(f"{i}:{line[:240]}")
    print(f"key_hits={len(hits)}")
    for h in hits[-80:]:
        print(h)
    print("---TAIL30---")
    for line in data.splitlines()[-30:]:
        print(line[:240])
else:
    print("LOG missing")

print("\n=== UNITY PROCESSES ===")
try:
    out = subprocess.check_output(
        ["tasklist", "/FI", "IMAGENAME eq Unity.exe", "/FO", "CSV"],
        text=True,
        errors="replace",
    )
    print(out)
except Exception as e:
    print("tasklist failed", e)

print("\n=== V0 GATE FILE ===")
gate = Path(
    r"C:\hades\Hecton8\Assets\_Project\Scripts\Physics\KCC\Editor\H8_V0PlaytestSmokeGate.cs"
)
print("exists", gate.exists(), "bytes", gate.stat().st_size if gate.exists() else 0)

print("\n=== DONE ===")
