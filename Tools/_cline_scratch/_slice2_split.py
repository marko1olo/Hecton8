# -*- coding: utf-8 -*-
p = r"C:\hades\Hecton8\Tools\_cline_scratch\_slice2_out.txt"
with open(p, encoding="utf-8") as f:
    text = f.read()
# split by =====
parts = text.split("=====")
base = r"C:\hades\Hecton8\Tools\_cline_scratch"
idx = 0
index_lines = []
for part in parts:
    if not part.strip():
        continue
    idx += 1
    header = part.strip().split("\n", 1)[0][:80]
    fn = os_path := ("%s\\_s2_%02d.txt" % (base, idx))
    import os
    body = "=====" + part
    # keep under 40k
    with open(fn, "w", encoding="utf-8") as fh:
        fh.write(body[:60000])
    index_lines.append("%02d %s chars=%d file=%s" % (idx, header, len(body), fn))
with open(base + "\\_s2_index.txt", "w", encoding="utf-8") as fh:
    fh.write("\n".join(index_lines))
print("\n".join(index_lines))
