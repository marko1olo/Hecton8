from pathlib import Path
root = Path(r"C:\hades\Hecton8")
# Only product Assets, not worktrees
assets = root / "Assets"
for p in assets.rglob("*"):
    if p.suffix.lower() not in {".cs", ".asmdef"}:
        continue
    try:
        t = p.read_text(encoding="utf-8", errors="replace")
    except Exception:
        continue
    if "InternalsVisibleTo" in t:
        for i, l in enumerate(t.splitlines(), 1):
            if "InternalsVisibleTo" in l:
                print(f"{p.relative_to(root)}:{i}:{l.strip()[:160]}")

# asmdefs for runner + core
for p in assets.rglob("*.asmdef"):
    name = p.name.lower()
    rel = str(p.relative_to(root)).replace("\\", "/")
    if any(k in rel.lower() for k in ("headless", "qa", "core", "world", "bootstrap", "project")):
        print("ASMDEF", rel)
        print(p.read_text(encoding="utf-8", errors="replace")[:800])
        print("---")

# How does runner already use internal FO APIs?
runner = (assets / "_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs").read_text(encoding="utf-8")
for needle in ("HectonFloatingOrigin", "internal", "GlobalRegistry"):
    pass
print("FO refs in runner:")
for i, l in enumerate(runner.splitlines(), 1):
    if "HectonFloatingOrigin" in l:
        print(f"{i}:{l}")
