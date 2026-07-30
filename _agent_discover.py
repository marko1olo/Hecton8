# -*- coding: utf-8 -*-
from pathlib import Path

OUT = Path(r"C:\hades\Hecton8\_agent_discover_out.txt")
lines: list[str] = []
desk = Path(r"C:\Users\Admin\Desktop")
lines.append(f"desk_exists={desk.exists()}")
try:
    names = sorted(p.name for p in desk.iterdir() if p.name.startswith("_"))
    lines.append(f"desk_underscore_count={len(names)}")
    lines.extend(names[:100])
except Exception as exc:
    lines.append(f"desk_err={exc!r}")

root = Path(r"C:\hades\Hecton8")
for pat in (
    "**/*relaunch*",
    "**/*poll*",
    "**/*BatchRunner*",
    "**/*headless*smoke*",
    "Tools/**/*.py",
    "Docs/AgentLogs/*",
    "**/HeadlessSimulation*.cs",
):
    found = list(root.glob(pat))[:40]
    lines.append(f"PAT {pat} count={len(found)}")
    for item in found:
        try:
            rel = item.relative_to(root)
        except ValueError:
            rel = item
        lines.append(f"  {rel}")

# also check common unity editor path / batchmode scripts
for p in [
    root / "Tools",
    root / "Docs" / "AgentLogs",
    Path(r"C:\Users\Admin\Desktop"),
]:
    lines.append(f"LIST {p} exists={p.exists()}")
    if p.exists() and p.is_dir():
        try:
            kids = sorted(p.iterdir(), key=lambda x: x.name.lower())[:60]
            for k in kids:
                lines.append(f"  {k.name}{'/' if k.is_dir() else ''}")
        except Exception as exc:
            lines.append(f"  list_err={exc!r}")

OUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
print(f"WROTE {OUT} bytes={OUT.stat().st_size}")
