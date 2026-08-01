import os
from pathlib import Path

root = Path(r"C:\hades\Hecton8\Assets\_Project\Scripts")
out_path = Path(r"C:\hades\Hecton8\Tools\_cline_scratch\_l14_reg_body.txt")
chunks = []

for p in root.rglob("*.cs"):
    try:
        text = p.read_text(encoding="utf-8", errors="replace")
    except OSError:
        continue
    if "TryRegisterFixedTickable" not in text:
        continue
    # Prefer definition sites
    lines = text.splitlines()
    for i, line in enumerate(lines):
        if "TryRegisterFixedTickable" not in line:
            continue
        # only method defs or bodies near GlobalRegistry/SystemDispatcher
        if p.name not in ("GlobalRegistry.cs", "SystemDispatcher.cs") and "bool TryRegisterFixedTickable" not in line:
            if "TryRegisterFixedTickable" in line and ("public" in line or "static" in line or "private" in line):
                pass
            else:
                continue
        start = max(0, i - 2)
        end = min(len(lines), i + 55)
        chunks.append(f"FILE {p}")
        for j in range(start, end):
            chunks.append(f"{j+1}|{lines[j]}")
        chunks.append("====")

# Also search for IsRegistered / Contains on fixed lanes
for p in root.rglob("*.cs"):
    if p.name not in ("GlobalRegistry.cs", "SystemDispatcher.cs", "RegistryBucket.cs"):
        continue
    try:
        text = p.read_text(encoding="utf-8", errors="replace")
    except OSError:
        continue
    lines = text.splitlines()
    for i, line in enumerate(lines):
        if any(k in line for k in ("Contains(", "IsRegistered", "TryAdd", "RegisterFixed", "UnregisterFixed")):
            chunks.append(f"{p.name}:{i+1}|{line.strip()[:200]}")

out_path.write_text("\n".join(chunks[:500]), encoding="utf-8")
print(f"wrote {out_path} lines={min(len(chunks),500)} total={len(chunks)}")

# Print key snippet to stdout
print("---STDOUT---")
print("\n".join(chunks[:80]))
