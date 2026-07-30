# scratch - do not commit
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
root = Path(r"C:\hades\Hecton8")

def show(path, needles):
    print("=" * 80)
    print(path.name, "lines", sum(1 for _ in path.open(encoding="utf-8", errors="replace")))
    lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
    for n in needles:
        hits = [(i + 1, lines[i].rstrip()[:200]) for i in range(len(lines)) if n in lines[i]]
        print(f"needle {n!r}: {len(hits)}")
        for i, l in hits[:30]:
            print(f"  {i}:{l}")

show(
    root / "Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessPlayModeProbe.cs",
    ["EnableDisabledPlacementOwnersInMemory"],
)
show(
    root / "Assets/_Project/Scripts/Editor/Diagnostics/H8_AirlockSceneAuthoring.cs",
    ["TryResolveVisualPrimitive"],
)

# find TryResolveVisualPrimitive definition sites
for p in root.joinpath("Assets").rglob("*.cs"):
    try:
        text = p.read_text(encoding="utf-8", errors="replace")
    except Exception:
        continue
    if "TryResolveVisualPrimitive" in text and "bool" in text:
        for i, line in enumerate(text.splitlines(), 1):
            if "TryResolveVisualPrimitive" in line and ("bool" in line or "static" in line or "(" in line):
                if "TryResolve" in line:
                    rel = p.relative_to(root)
                    print(f"DEF? {rel}:{i}:{line.strip()[:180]}")
