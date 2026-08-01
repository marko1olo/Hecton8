import pathlib, os
os.chdir(r"C:/hades/Hecton8")

def dump(path, start, end, outpath):
    lines = pathlib.Path(path).read_text(encoding="utf-8", errors="replace").splitlines()
    s = max(1, start); e = min(len(lines), end)
    out = []
    for i in range(s, e+1):
        out.append(str(i)+"|"+lines[i-1])
    pathlib.Path(outpath).write_text(chr(10).join(out), encoding="utf-8")
    print(path, s, e, len(out))

def grep(path, keys, outpath, limit=500):
    lines = pathlib.Path(path).read_text(encoding="utf-8", errors="replace").splitlines()
    out = []
    for i, l in enumerate(lines, 1):
        if any(k in l for k in keys):
            out.append(str(i)+"|"+l[:260])
            if len(out) >= limit:
                break
    pathlib.Path(outpath).write_text(chr(10).join(out), encoding="utf-8")
    print("grep", path, len(out))

#continue
