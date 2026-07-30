import os

base = r"C:/hades/Hecton8"
out = []

# Find RunDispatcherLateFrame and how priority is sorted
for root, dirs, files in os.walk(os.path.join(base, "Assets")):
    dirs[:] = [d for d in dirs if d not in ("Library", "Temp", "obj", ".git")]
    for f in files:
        if f != "SystemDispatcher.cs":
            continue
        p = os.path.join(root, f)
        lines = open(p, encoding="utf-8", errors="replace").read().splitlines()
        out.append("FOUND " + p + " lines " + str(len(lines)))
        for i, l in enumerate(lines, 1):
            if any(
                k in l
                for k in (
                    "RunDispatcherLateFrame",
                    "LateFrame",
                    "PriorityLayer",
                    "Sort",
                    "_lateFrame",
                    "RegisterLateFrame",
                )
            ):
                out.append(f"{i}|{l}")
        # dump late frame runner
        for i, l in enumerate(lines, 1):
            if "RunDispatcherLateFrame" in l and ("void" in l or "private" in l or "public" in l):
                for j in range(i, min(len(lines), i + 60) + 1):
                    out.append(f"D {j}|{lines[j-1]}")
                break
        # dump registration sort
        for i, l in enumerate(lines, 1):
            if "TryRegisterLateFrameTickable" in l or "RegisterLateFrame" in l:
                if "bool" in l or "void" in l:
                    for j in range(max(1, i - 2), min(len(lines), i + 80) + 1):
                        out.append(f"R {j}|{lines[j-1]}")
                    break

# Also GlobalRegistry late frame
for root, dirs, files in os.walk(os.path.join(base, "Assets")):
    dirs[:] = [d for d in dirs if d not in ("Library", "Temp", "obj", ".git")]
    for f in files:
        if "GlobalRegistry" not in f or not f.endswith(".cs"):
            continue
        p = os.path.join(root, f)
        t = open(p, encoding="utf-8", errors="replace").read()
        if "LateFrame" not in t:
            continue
        lines = t.splitlines()
        out.append("\nGR " + p)
        for i, l in enumerate(lines, 1):
            if "LateFrame" in l or ("priority" in l.lower() and "tick" in l.lower()):
                out.append(f"{i}|{l}")

open(os.path.join(base, "Tools/_cline_slice5_out.txt"), "w", encoding="utf-8").write("\n".join(out))
print("done", len(out))
