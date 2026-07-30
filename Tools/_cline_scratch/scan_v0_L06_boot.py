# -*- coding: utf-8 -*-
from pathlib import Path

p = Path(r"C:\hades\Hecton8\Docs\AgentLogs\h8_playprobe_v0_L06.log")
out = Path(r"C:\hades\Hecton8\Tools\_cline_scratch\v0_L06_boot_errors.txt")
keys = (
    "OceanKinematics",
    "Bootstrap dependency failed",
    "Bootstrap phase failed",
    "NativeFaultDump",
    "HectonSeismic",
    "allSystemsReady",
    "IsGameReady",
    "SceneActivate",
    "MarkMainMenu",
    "InvalidOperationException",
    "NullReferenceException",
    "phase failed",
    "dependency failed",
    "Environment",
    "MainMenuController",
    "activationStep",
    "AreAllSystemsReady",
    "gameReady",
    "BootstrapState",
    "TryInitializeBootstrap",
    "Exception",
    "Error",
)
skip_sub = ("waiting for the game", "H8_PLAYPROBE] waiting")
hits = []
with p.open("r", encoding="utf-8", errors="replace") as f:
    for i, line in enumerate(f, 1):
        low = line.lower()
        if any(s.lower() in low for s in skip_sub):
            continue
        if any(k.lower() in low for k in keys):
            # drop pure stack noise lines that are only "UnityEngine."
            hits.append(f"{i}:{line.rstrip()}")

with out.open("w", encoding="utf-8") as o:
    o.write(f"hits={len(hits)}\n")
    for h in hits[:500]:
        o.write(h + "\n")

print(f"hits={len(hits)} wrote={out}")
print("--- first 100 ---")
for h in hits[:100]:
    print(h[:400])
print("--- last 40 ---")
for h in hits[-40:]:
    print(h[:400])
