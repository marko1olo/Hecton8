# -*- coding: utf-8 -*-
"""L10 inspect: PTM verify + HPM hop2 early-outs + inventory recover path."""
from __future__ import annotations

import os
import re
import sys

ROOT = r"C:\hades\Hecton8"
OUT = os.path.join(ROOT, r"Tools\_cline_scratch\_l10_inspect_out.txt")


def dump_method(lines, name, limit=80):
    rows = []
    for i, l in enumerate(lines):
        if name in l and ("void" in l or "bool" in l or "int" in l or "float" in l or "IEnumerator" in l):
            depth = 0
            started = False
            for j in range(i, min(len(lines), i + limit)):
                rows.append(f"{j+1}:{lines[j]}")
                depth += lines[j].count("{") - lines[j].count("}")
                if "{" in lines[j]:
                    started = True
                if started and depth <= 0:
                    break
            break
    return rows


def slice_around(lines, needle, before=5, after=40, max_hits=8):
    rows = []
    hits = 0
    for i, l in enumerate(lines):
        if needle in l:
            hits += 1
            rows.append(f"--- hit {hits} @ {i+1}: {needle} ---")
            for j in range(max(0, i - before), min(len(lines), i + after + 1)):
                rows.append(f"{j+1}:{lines[j]}")
            if hits >= max_hits:
                break
    return rows


def main() -> int:
    report = []

    # --- PTM ---
    ptm_path = os.path.join(ROOT, r"Assets\_Project\Scripts\PlayerToolManager.cs")
    ptm = open(ptm_path, encoding="utf-8").read()
    ptm_lines = ptm.splitlines()
    report.append("=== PTM VERIFY ===")
    report.append(
        "reg_ok="
        + str(
            "(_registeredToTick && _registeredToLateFrame && _registeredToFixedTick)"
            in ptm
        )
    )
    report.append(
        "unreg_ok="
        + str(
            "!_registeredToTick && !_registeredToLateFrame && !_registeredToFixedTick"
            in ptm
        )
    )
    report.extend(dump_method(ptm_lines, "TryRegisterToTickManager"))
    report.extend(dump_method(ptm_lines, "TryUnregisterFromTickManager"))
    report.extend(dump_method(ptm_lines, "FixedTick"))
    report.extend(slice_around(ptm_lines, "TryRecover", before=2, after=25, max_hits=4))
    report.extend(slice_around(ptm_lines, "STARTERGRANT", before=2, after=20, max_hits=6))
    report.extend(slice_around(ptm_lines, "CanServiceItemAdds", before=2, after=15, max_hits=4))

    # --- HPM ---
    hpm_path = os.path.join(ROOT, r"Assets\_Project\Scripts\HectonPlayerMovement.cs")
    if not os.path.isfile(hpm_path):
        # search
        for dirpath, _, files in os.walk(os.path.join(ROOT, "Assets")):
            if "HectonPlayerMovement.cs" in files:
                hpm_path = os.path.join(dirpath, "HectonPlayerMovement.cs")
                break
    report.append(f"=== HPM path: {hpm_path} ===")
    hpm = open(hpm_path, encoding="utf-8").read()
    hpm_lines = hpm.splitlines()
    report.append(f"hpm_lines={len(hpm_lines)}")

    for name in (
        "FixedTick",
        "SampleGameplay",
        "ProcessPlayerInputFrame",
        "TryRegisterToTickManager",
        "TryUnregisterFromTickManager",
    ):
        # looser name match
        found = False
        for i, l in enumerate(hpm_lines):
            if name in l and ("(" in l) and (
                "void" in l or "bool" in l or "private" in l or "public" in l or "protected" in l
            ):
                # skip comments
                if l.strip().startswith("//"):
                    continue
                depth = 0
                started = False
                report.append(f"=== HPM {name} @ {i+1} ===")
                for j in range(i, min(len(hpm_lines), i + 120)):
                    report.append(f"{j+1}:{hpm_lines[j]}")
                    depth += hpm_lines[j].count("{") - hpm_lines[j].count("}")
                    if "{" in hpm_lines[j]:
                        started = True
                    if started and depth <= 0:
                        break
                found = True
                break
        if not found:
            report.append(f"=== HPM {name} NOT FOUND as method sig ===")
            report.extend(slice_around(hpm_lines, name, before=3, after=25, max_hits=3))

    report.extend(slice_around(hpm_lines, "currentSuitData", before=2, after=8, max_hits=10))
    report.extend(slice_around(hpm_lines, "_juiceProcessor", before=2, after=8, max_hits=10))
    report.extend(slice_around(hpm_lines, "LocomotionHold", before=2, after=10, max_hits=8))
    report.extend(slice_around(hpm_lines, "readHop", before=2, after=8, max_hits=6))
    report.extend(slice_around(hpm_lines, "movementIntent", before=2, after=8, max_hits=6))
    report.extend(slice_around(hpm_lines, "IsPlayerInputEnabled", before=2, after=8, max_hits=6))
    report.extend(slice_around(hpm_lines, "waitingOn", before=2, after=8, max_hits=6))

    # InputDispatcher hop2
    report.append("=== SEARCH InputDispatcher GetState hop ===")
    for dirpath, _, files in os.walk(os.path.join(ROOT, "Assets")):
        for f in files:
            if f.endswith(".cs") and ("InputDispatcher" in f or "PlayerInput" in f):
                p = os.path.join(dirpath, f)
                try:
                    t = open(p, encoding="utf-8", errors="replace").read()
                except Exception:
                    continue
                if "hop" in t.lower() or "GetState" in t or "TryReadFrame" in t:
                    report.append(f"FILE {p}")
                    ls = t.splitlines()
                    for needle in ("hop", "GetState", "TryReadFrame", "readHop", "Publish"):
                        for i, l in enumerate(ls):
                            if needle in l:
                                report.append(f"  {i+1}:{l.strip()[:160]}")

    # Inventory vault 0x1E
    report.append("=== INVENTORY / VAULT ===")
    for dirpath, _, files in os.walk(os.path.join(ROOT, "Assets")):
        for f in files:
            if not f.endswith(".cs"):
                continue
            if "Inventory" not in f and "Vault" not in f and "PlayerTool" not in f:
                continue
            p = os.path.join(dirpath, f)
            try:
                t = open(p, encoding="utf-8", errors="replace").read()
            except Exception:
                continue
            if "CanServiceItemAdds" in t or "TryRecover" in t or "refusalMask" in t or "0x1E" in t:
                report.append(f"FILE {p}")
                ls = t.splitlines()
                for needle in (
                    "CanServiceItemAdds",
                    "TryRecover",
                    "TryBind",
                    "RefreshVaultHandles",
                    "InitializeSoaQueryEngine",
                    "refusalMask",
                    "0x1E",
                ):
                    report.extend(
                        [
                            x
                            for x in slice_around(ls, needle, before=1, after=12, max_hits=2)
                        ]
                    )

    # L09 log summary
    log = os.path.join(ROOT, r"Docs\AgentLogs\h8_playprobe_v0_L09.log")
    report.append(f"=== L09 LOG exists={os.path.isfile(log)} ===")
    if os.path.isfile(log):
        # tail + key lines
        data = open(log, encoding="utf-8", errors="replace").read().splitlines()
        report.append(f"log_lines={len(data)}")
        keys = (
            "STARTERGRANT",
            "readHop",
            "movementIntent",
            "LocomotionHold",
            "publishOk",
            "refusalMask",
            "waitingOn",
            "V0_",
            "FAIL",
            "PASS",
            "swim",
            "FixedTick",
            "suit",
            "juice",
        )
        for i, l in enumerate(data):
            if any(k.lower() in l.lower() for k in keys):
                if i < 50 or i > len(data) - 80 or any(
                    k in l
                    for k in (
                        "STARTERGRANT",
                        "readHop",
                        "movementIntent",
                        "LocomotionHold",
                        "refusalMask",
                        "waitingOn",
                    )
                ):
                    report.append(f"L{i+1}:{l[:240]}")

    # L08 measured for format reference
    l08 = os.path.join(ROOT, r"Docs\V0_Playtest\V0_L08_MEASURED.md")
    report.append(f"=== L08 MEASURED exists={os.path.isfile(l08)} ===")
    if os.path.isfile(l08):
        report.append(open(l08, encoding="utf-8", errors="replace").read()[:4000])

    open(OUT, "w", encoding="utf-8").write("\n".join(report) + "\n")
    print(OUT)
    print(f"report_lines={len(report)}")
    # print head ascii-safe
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    for line in report[:80]:
        print(line[:200])
    return 0


if __name__ == "__main__":
    sys.exit(main())
