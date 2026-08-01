#!/usr/bin/env python3
"""L13.1: Fix CS0246 bare HectonPlayerMovement in WorldDriver EnsureGameplayLocomotionInputReady."""
from __future__ import annotations

import os
import re
import sys

DRIVER = r"C:\hades\Hecton8\Assets\_Project\Scripts\Editor\Diagnostics\H8_HeadlessWorldDriver.cs"
OUT = r"C:\hades\Hecton8\Tools\_cline_scratch\_l13_fqn_fix_out.txt"


def main() -> int:
    lines_out: list[str] = []
    if not os.path.isfile(DRIVER):
        lines_out.append(f"MISSING {DRIVER}")
        open(OUT, "w", encoding="utf-8").write("\n".join(lines_out))
        return 1

    raw = open(DRIVER, "rb").read()
    # Detect newline style
    nl = b"\r\n" if b"\r\n" in raw else b"\n"
    text = raw.decode("utf-8")
    lines_out.append(f"size_before={len(raw)} nl={'crlf' if nl==b'\\r\\n' else 'lf'}")

    # Target: bare type usages only in Ensure block (around EnsureDispatcherRegistration)
    bare_decl = "HectonPlayerMovement movement ="
    bare_as = "as HectonPlayerMovement"
    bare_find = "FindFirstObjectByType<HectonPlayerMovement>()"
    fqn = "Hecton8.Gameplay.HectonPlayerMovement"

    has_bare = bare_as in text or bare_find in text or (
        bare_decl in text and f"Hecton8.Gameplay.{bare_decl}" not in text.replace(f"{fqn} movement =", "")
    )
    lines_out.append(f"bare_as={bare_as in text}")
    lines_out.append(f"bare_find={bare_find in text}")
    lines_out.append(f"fqn_already={fqn + ' movement =' in text and bare_as not in text}")

    if bare_as not in text and bare_find not in text:
        # Check for bare decl without FQN near EnsureDispatcher
        idx = text.find("EnsureDispatcherRegistration")
        window = text[max(0, idx - 600) : idx + 200] if idx >= 0 else ""
        if "Hecton8.Gameplay.HectonPlayerMovement movement" in window:
            lines_out.append("ALREADY_FIXED")
            open(OUT, "w", encoding="utf-8").write("\n".join(lines_out))
            return 0
        lines_out.append("NO_BARE_FOUND_UNEXPECTED")
        lines_out.append(repr(window[:500]))
        open(OUT, "w", encoding="utf-8").write("\n".join(lines_out))
        return 2

    # Regex replace the 3-line block flexibly across CRLF/LF and whitespace
    pattern = re.compile(
        r"[ \t]*HectonPlayerMovement movement\s*=\s*\r?\n"
        r"[ \t]*_movement as HectonPlayerMovement\s*\r?\n"
        r"[ \t]*\?\?\s*UnityEngine\.Object\.FindFirstObjectByType<\s*HectonPlayerMovement\s*>\(\)\s*;",
        re.MULTILINE,
    )
    replacement = (
        "            // Fully-qualify type: editor asm has no using Hecton8.Gameplay (CS0246 on bare name).\n"
        "            Hecton8.Gameplay.HectonPlayerMovement movement =\n"
        "                _movement\n"
        "                ?? UnityEngine.Object.FindFirstObjectByType<Hecton8.Gameplay.HectonPlayerMovement>();"
    )
    # Normalize file to LF for rewrite consistency with rest of repo patterns if LF
    new_text, n = pattern.subn(replacement, text, count=1)
    lines_out.append(f"regex_subs={n}")

    if n != 1:
        # Fallback: line-based surgery around EnsureDispatcherRegistration
        file_lines = text.splitlines()
        ensure_i = None
        for i, line in enumerate(file_lines):
            if "movement.EnsureDispatcherRegistration()" in line:
                ensure_i = i
                break
        if ensure_i is None:
            lines_out.append("ENSURE_CALL_NOT_FOUND")
            open(OUT, "w", encoding="utf-8").write("\n".join(lines_out))
            return 3
        # Walk backward to find start of bare decl
        start = None
        for j in range(ensure_i - 1, max(0, ensure_i - 15), -1):
            if "HectonPlayerMovement movement" in file_lines[j] and "Hecton8.Gameplay" not in file_lines[j]:
                start = j
                break
        if start is None:
            lines_out.append(f"BARE_DECL_NOT_FOUND near ensure line {ensure_i+1}")
            for j in range(max(0, ensure_i - 12), ensure_i + 3):
                lines_out.append(f"{j+1}|{file_lines[j]}")
            open(OUT, "w", encoding="utf-8").write("\n".join(lines_out))
            return 4
        # Replace from start through line before if (movement != null)
        end = start
        while end < ensure_i and "FindFirstObjectByType" not in file_lines[end]:
            end += 1
        if end >= ensure_i:
            lines_out.append("FIND_LINE_NOT_FOUND")
            open(OUT, "w", encoding="utf-8").write("\n".join(lines_out))
            return 5
        new_block = [
            "            // Fully-qualify type: editor asm has no using Hecton8.Gameplay (CS0246 on bare name).",
            "            Hecton8.Gameplay.HectonPlayerMovement movement =",
            "                _movement",
            "                ?? UnityEngine.Object.FindFirstObjectByType<Hecton8.Gameplay.HectonPlayerMovement>();",
        ]
        file_lines[start : end + 1] = new_block
        # Preserve original newline style
        join_nl = "\r\n" if nl == b"\r\n" else "\n"
        new_text = join_nl.join(file_lines)
        if text.endswith("\n") or text.endswith("\r\n"):
            if not new_text.endswith("\n"):
                new_text += join_nl
        lines_out.append(f"line_surgery start={start+1} end={end+1}")
        n = 1

    if n != 1:
        lines_out.append("REPLACE_FAILED")
        open(OUT, "w", encoding="utf-8").write("\n".join(lines_out))
        return 6

    # Write back with original newline style if we used regex on LF-normalized
    if "line_surgery" not in "\n".join(lines_out):
        if nl == b"\r\n" and "\r\n" not in new_text:
            new_text = new_text.replace("\n", "\r\n")
        open(DRIVER, "w", encoding="utf-8", newline="").write(new_text)
    else:
        open(DRIVER, "w", encoding="utf-8", newline="").write(new_text)

    # Verify
    v = open(DRIVER, encoding="utf-8").read()
    lines_out.append(f"bare_as_after={('as HectonPlayerMovement' in v)}")
    lines_out.append(f"bare_find_after={('FindFirstObjectByType<HectonPlayerMovement>' in v)}")
    lines_out.append(
        f"fqn_find_after={('FindFirstObjectByType<Hecton8.Gameplay.HectonPlayerMovement>' in v)}"
    )
    lines_out.append(f"marker_after={('Fully-qualify type' in v)}")
    # Print relevant lines
    for i, line in enumerate(v.splitlines(), 1):
        if 2690 <= i <= 2710 and (
            "HectonPlayerMovement" in line or "Fully-qualify" in line or "EnsureDispatcher" in line
        ):
            lines_out.append(f"{i}|{line}")
    ok = (
        "as HectonPlayerMovement" not in v
        and "FindFirstObjectByType<HectonPlayerMovement>" not in v
        and "FindFirstObjectByType<Hecton8.Gameplay.HectonPlayerMovement>" in v
    )
    lines_out.append("OK" if ok else "VERIFY_FAIL")
    open(OUT, "w", encoding="utf-8").write("\n".join(lines_out) + "\n")
    print("\n".join(lines_out))
    return 0 if ok else 7


if __name__ == "__main__":
    sys.exit(main())
