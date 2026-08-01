# -*- coding: utf-8 -*-
from pathlib import Path
import subprocess

out = Path(r"C:\hades\Hecton8\Tools\_cline_scratch\_l14_verify2_out.txt")
lines = []

t = Path(r"C:\hades\Hecton8\Assets\_Project\Scripts\HectonPlayerMovement.cs").read_text(encoding="utf-8")
s = Path(r"C:\hades\Hecton8\Assets\_Project\Scripts\Core\SystemDispatcher.cs").read_text(encoding="utf-8")

for name in ["TryRegisterToDispatchers", "EnsureDispatcherRegistration"]:
    lines.append(f"{name} idx={t.find(name)}")

i = t.find("// L14: sticky false")
lines.append("---REG BLOCK---")
lines.append(t[i : i + 900] if i >= 0 else "MISSING REG")

j = t.find("// L14: publish raw locomotion intent")
lines.append("---SAMPLE---")
lines.append(t[j : j + 400] if j >= 0 else "MISSING SAMPLE")

k = t.find("// L14: menu block zeros intent")
lines.append("---MENU ZERO---")
lines.append(t[k : k + 250] if k >= 0 else "MISSING MENU")

# thrash only bad inside sticky false block region
reg = t[i : i + 900] if i >= 0 else ""
lines.append(f"reg has Unregister thrash: {'UnregisterUpdatable' in reg or 'UnregisterFixedTickable' in reg}")
lines.append(f"file UnregisterUpdatable count: {t.count('UnregisterUpdatable')}")
lines.append(f"file UnregisterFixedTickable count: {t.count('UnregisterFixedTickable')}")

lines.append("---SD ShouldSkip---")
m = s.find("private static bool ShouldSkipLaneDuringBootstrap")
lines.append(s[m : m + 700] if m >= 0 else "MISSING SD")

# git diff stat
r = subprocess.run(
    [
        "git",
        "-C",
        r"C:\hades\Hecton8",
        "diff",
        "--stat",
        "--",
        "Assets/_Project/Scripts/HectonPlayerMovement.cs",
        "Assets/_Project/Scripts/Core/SystemDispatcher.cs",
    ],
    capture_output=True,
    text=True,
)
lines.append("---GIT DIFF STAT---")
lines.append(r.stdout)
lines.append(r.stderr)

r2 = subprocess.run(
    [
        "git",
        "-C",
        r"C:\hades\Hecton8",
        "diff",
        "--",
        "Assets/_Project/Scripts/HectonPlayerMovement.cs",
        "Assets/_Project/Scripts/Core/SystemDispatcher.cs",
    ],
    capture_output=True,
    text=True,
)
diff_path = Path(r"C:\hades\Hecton8\Tools\_cline_scratch\_l14_diff_final.txt")
diff_path.write_text(r2.stdout, encoding="utf-8")
lines.append(f"diff lines: {len(r2.stdout.splitlines())}")
lines.append(f"written {diff_path}")

ok = (
    i >= 0
    and j >= 0
    and k >= 0
    and "UnregisterUpdatable" not in reg
    and "return false;" in (s[m : m + 700] if m >= 0 else "")
    and "laneIndex == GetLaneIndex(PriorityLayer.Player)" not in (s[m : m + 700] if m >= 0 else "").split("Previously")[0]
)
lines.append(f"PRODUCT_OK={ok}")
out.write_text("\n".join(lines), encoding="utf-8")
print("WROTE", out)
print("PRODUCT_OK", ok)
