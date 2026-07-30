"""Probe geology constants + headless status. Do not commit."""
from __future__ import annotations

import pathlib
import re
import subprocess
import sys

ROOT = pathlib.Path(r"C:\hades\Hecton8")
OUT = ROOT / "Tools" / "_cline_probe_out.txt"


def main() -> int:
    lines: list[str] = []
    tex = (ROOT / "Tools/Blender/h8forge/texture.py").read_text(encoding="utf-8")
    law = (ROOT / "Tools/Blender/h8forge/law.py").read_text(encoding="utf-8")
    pats = [
        r"^SPALL_SCAR_COUNT\s*=.*",
        r"^SPALL_WIDTH_.*",
        r"^SPALL_PACKAGE_.*",
        r"^SPALL_OFFSET_.*",
        r"^GEOLOGY_LAMINA_MAX_RUN_FRACTION\s*=.*",
        r"^GEOLOGY_MIN_EROSIONAL_COVERAGE\s*=.*",
    ]
    lines.append("=== CONSTANTS ===")
    for src_name, src in (("texture.py", tex), ("law.py", law)):
        for pat in pats:
            for m in re.finditer(pat, src, re.M):
                lines.append(f"{src_name}: {m.group(0)}")

    lines.append("=== HEADLESS BG LOG ===")
    bg = pathlib.Path(r"C:\Users\Admin\AppData\Local\Temp\cline\background-1785392074941-vbtq52f.log")
    if bg.exists():
        t = bg.read_text(encoding="utf-8", errors="replace")
        lines.append(t[-4000:].encode("ascii", "replace").decode("ascii"))
    else:
        lines.append("bg log missing")

    res = ROOT / "Docs/AgentLogs/HeadlessSimulationResult_HEADLESS_SIMULATION_RUNNER.json"
    lines.append("=== RESULT ===")
    lines.append(f"exists={res.exists()}")
    if res.exists():
        lines.append(res.read_text(encoding="utf-8", errors="replace")[:2000])

    log = ROOT / "Logs/headless_ecology_fence_5day.log"
    lines.append("=== UNITY LOG MARKERS ===")
    if log.exists():
        t = log.read_text(encoding="utf-8", errors="replace")
        lines.append(f"size={log.stat().st_size}")
        for m in (
            "Scripts have compiler errors",
            "error CS",
            "[HEADLESS]",
            "ECOLOGY_UNAVAILABLE",
            "ECOLOGY_NEVER_SAMPLED",
            "SUCCESS",
            "ExecuteMethod",
            "Batchmode quit",
            "exiting",
        ):
            c = t.count(m)
            if c:
                lines.append(f"  {m!r}: {c}")
        # last non-empty lines ascii
        tail = [ln for ln in t.splitlines() if ln.strip()][-30:]
        lines.append("--- tail ---")
        lines.extend(x.encode("ascii", "replace").decode("ascii") for x in tail)
    else:
        lines.append("log missing")

    p = subprocess.run(
        ["tasklist", "/FI", "IMAGENAME eq Unity.exe", "/FO", "CSV", "/NH"],
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    tl = p.stdout or ""
    lines.append(f"unity_alive={'Unity.exe' in tl}")

    body = "\n".join(lines) + "\n"
    OUT.write_text(body, encoding="utf-8")
    sys.stdout.buffer.write(f"wrote {OUT}\n".encode("ascii"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
