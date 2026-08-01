# -*- coding: utf-8 -*-
"""Patch PlayerToolManager TryRegister/TryUnregister early-outs to include FixedTick flag."""
from __future__ import annotations

import os
import sys

ROOT = r"C:\hades\Hecton8"
PTM = os.path.join(ROOT, r"Assets\_Project\Scripts\PlayerToolManager.cs")
OUT = os.path.join(ROOT, r"Tools\_cline_scratch\_fix_ptm_reg_out.txt")

OLD_REG = "if ((_registeredToTick && _registeredToLateFrame) || !Application.isPlaying)"
NEW_REG = "if ((_registeredToTick && _registeredToLateFrame && _registeredToFixedTick) || !Application.isPlaying)"

OLD_UNREG = "if (!_registeredToTick && !_registeredToLateFrame)"
NEW_UNREG = "if (!_registeredToTick && !_registeredToLateFrame && !_registeredToFixedTick)"


def main() -> int:
    raw = open(PTM, "rb").read()
    nl = b"\r\n" if b"\r\n" in raw else b"\n"
    text = raw.decode("utf-8")

    report = []
    report.append(f"PTM path: {PTM}")
    report.append(f"bytes: {len(raw)}")
    report.append(f"old_reg present: {OLD_REG in text}")
    report.append(f"new_reg present: {NEW_REG in text}")
    report.append(f"old_unreg present: {OLD_UNREG in text}")
    report.append(f"new_unreg present: {NEW_UNREG in text}")

    changed = False
    if OLD_REG in text and NEW_REG not in text:
        text = text.replace(OLD_REG, NEW_REG, 1)
        changed = True
        report.append("APPLIED: register early-out includes _registeredToFixedTick")
    elif NEW_REG in text:
        report.append("SKIP: register already patched")
    else:
        report.append("FAIL: register pattern missing")

    if OLD_UNREG in text and NEW_UNREG not in text:
        # Only first occurrence should be TryUnregisterFromTickManager early-out
        text = text.replace(OLD_UNREG, NEW_UNREG, 1)
        changed = True
        report.append("APPLIED: unregister early-out includes !_registeredToFixedTick")
    elif NEW_UNREG in text:
        report.append("SKIP: unregister already patched")
    else:
        report.append("FAIL: unregister pattern missing")

    if changed:
        data = text.encode("utf-8")
        # preserve original newline style if we decoded mixed; write as-is from text
        # text still has original newlines from decode
        open(PTM, "wb").write(data)
        report.append("WROTE PTM")
    else:
        report.append("NO WRITE")

    v = open(PTM, encoding="utf-8").read()
    report.append(f"VERIFY new_reg: {NEW_REG in v}")
    report.append(f"VERIFY old_reg gone: {OLD_REG not in v}")
    report.append(f"VERIFY new_unreg: {NEW_UNREG in v}")
    report.append(f"VERIFY old_unreg gone: {OLD_UNREG not in v}")

    # dump method bodies
    lines = v.splitlines()
    for name in ("TryRegisterToTickManager", "TryUnregisterFromTickManager"):
        for i, l in enumerate(lines):
            if f"void {name}" in l or f"private void {name}" in l:
                report.append(f"--- {name} @ {i+1} ---")
                for j in range(i, min(len(lines), i + 45)):
                    report.append(f"{j+1}:{lines[j]}")
                    if j > i and lines[j].strip() == "}" and j + 1 < len(lines):
                        # end of method when we hit closing at method indent - simple: break after first lone }
                        # better: break when brace depth returns
                        pass
                break

    # tighter dump using brace depth
    report.append("--- PRECISE METHOD DUMP ---")
    for name in ("TryRegisterToTickManager", "TryUnregisterFromTickManager"):
        for i, l in enumerate(lines):
            if f"{name}(" in l and "void" in l:
                depth = 0
                started = False
                report.append(f"=== {name} ===")
                for j in range(i, len(lines)):
                    report.append(f"{j+1}:{lines[j]}")
                    depth += lines[j].count("{") - lines[j].count("}")
                    if "{" in lines[j]:
                        started = True
                    if started and depth <= 0:
                        break
                break

    open(OUT, "w", encoding="utf-8").write("\n".join(report) + "\n")
    print("\n".join(report))
    ok = NEW_REG in v and NEW_UNREG in v and OLD_REG not in v and OLD_UNREG not in v
    return 0 if ok else 2


if __name__ == "__main__":
    sys.exit(main())
