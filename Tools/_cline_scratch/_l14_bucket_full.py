from pathlib import Path

p = Path(r"C:\hades\Hecton8\Assets\_Project\Scripts\Core\RegistryBucket.cs")
t = p.read_text(encoding="utf-8", errors="replace")
# full TryRegister method
i = t.find("public bool TryRegister(T item)")
print(t[i : i + 1500])
print("====")
# Contains / IndexOf / Has
for key in ["Contains", "IndexOf", "Has(", "IsRegistered", "FindIndex"]:
    j = 0
    while True:
        k = t.find(key, j)
        if k < 0:
            break
        line_start = t.rfind("\n", 0, k) + 1
        line_end = t.find("\n", k)
        print(t[line_start:line_end])
        j = k + len(key)
