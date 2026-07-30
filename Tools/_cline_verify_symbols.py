# scratch - do not commit
from pathlib import Path

root = Path(r"C:\hades\Hecton8")
checks = [
    (
        root / "Assets/_Project/Scripts/Editor/Authoring/ForgeGeneratedMaterialAuthoring.cs",
        ["ApplyOrganicRole", "void ApplyOrganicRole"],
        [1200, 1230, 1540, 1570, 1700, 1740],
    ),
    (
        root / "Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessPlayModeProbe.cs",
        ["EnableDisabledPlacementOwnersInMemory"],
        [640, 670, 750, 770, 870, 900],
    ),
    (
        root / "Assets/_Project/Scripts/Editor/Diagnostics/H8_AirlockSceneAuthoring.cs",
        ["TryResolveVisualPrimitive"],
        [200, 240],
    ),
]

for path, needles, windows in checks:
    print("=" * 80)
    print(path.name, "exists" if path.exists() else "MISSING")
    if not path.exists():
        continue
    lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
    print("total lines", len(lines))
    for n in needles:
        hits = [(i + 1, lines[i].rstrip()) for i in range(len(lines)) if n in lines[i]]
        print(f"  needle {n!r}: {len(hits)} hits")
        for i, l in hits[:20]:
            print(f"    {i}:{l}")
    for start, end in zip(windows[::2], windows[1::2]):
        print(f"  --- window {start}-{end} ---")
        for i in range(max(0, start - 1), min(len(lines), end)):
            print(f"  {i+1}:{lines[i].rstrip()}")
