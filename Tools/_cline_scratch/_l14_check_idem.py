from pathlib import Path

root = Path(r"C:\hades\Hecton8\Assets\_Project\Scripts")
out = []

# Find RegistryBucket TryRegister body
for p in root.rglob("*.cs"):
    t = p.read_text(encoding="utf-8", errors="replace")
    if "struct RegistryBucket" in t or "class RegistryBucket" in t:
        out.append(f"BUCKET_TYPE {p}")
        # extract TryRegister method
        idx = 0
        while True:
            i = t.find("TryRegister", idx)
            if i < 0:
                break
            out.append(t[i : i + 700])
            out.append("---")
            idx = i + 10

# SystemDispatcher.Register(IFixedTickable
for p in [root / "Core" / "SystemDispatcher.cs", root / "Core" / "GlobalRegistry.cs"]:
    if not p.exists():
        continue
    t = p.read_text(encoding="utf-8", errors="replace")
    for key in [
        "public static bool Register(IFixedTickable",
        "public static bool Register(IUpdatable",
        "bool Register(IFixedTickable",
        "bool Register(IUpdatable",
        "RegisterFixed",
    ]:
        i = t.find(key)
        if i >= 0:
            out.append(f"FILE {p.name} key={key}")
            out.append(t[i : i + 900])
            out.append("====")

# Driver Ensure call sites
drv = root / "Editor" / "Diagnostics" / "H8_HeadlessWorldDriver.cs"
if drv.exists():
    t = drv.read_text(encoding="utf-8", errors="replace")
    lines = t.splitlines()
    for i, l in enumerate(lines):
        if "EnsureGameplayLocomotionInputReady" in l or "EnsureDispatcherRegistration" in l:
            out.append(f"DRV {i+1}|{l.strip()}")

path = Path(r"C:\hades\Hecton8\Tools\_cline_scratch\_l14_check_idem_out.txt")
path.write_text("\n".join(out), encoding="utf-8")
print("wrote", path, "n", len(out))
print("\n".join(out[:120]))
