import os, re, time
import numpy as np

P = "Assets/_Project/Scenes/020_RENDER_SANDBOX.unity"
blob = open(P, "rb").read()
print("blob MB", round(len(blob) / 1e6, 1))

# 701-ish fake 16-byte patterns from real guids in meta files
GUID_RE = re.compile(rb"guid:\s*([a-f0-9]{32})")
guids = set()
for dp, dn, fns in os.walk("Assets/_Project/Scripts"):
    dn[:] = [d for d in dn if d not in ("Library", "Temp", "obj", ".git")]
    for fn in fns:
        if fn.endswith(".cs.meta"):
            m = GUID_RE.search(open(os.path.join(dp, fn), "rb").read())
            if m:
                guids.add(m.group(1).decode())
guids = sorted(guids)[:701]
print("guids", len(guids))


def swapped(g):
    out = bytearray()
    for i in range(0, 32, 2):
        out.append((int(g[i + 1], 16) << 4) | int(g[i], 16))
    return bytes(out)


pats = [swapped(g) for g in guids]

t = time.time()
hits = sum(1 for p in pats if blob.find(p) >= 0)
print("A naive find loop: %.2fs hits=%d" % (time.time() - t, hits))

# B: numpy two-stage, 4 aligned big-endian streams
t = time.time()
keys = np.array([int.from_bytes(p[:4], "big") for p in pats], dtype=np.uint64)
lut = np.zeros(1 << 16, dtype=bool)
lut[(keys >> np.uint64(16)).astype(np.uint32)] = True
skeys = np.sort(keys)
arr = np.frombuffer(blob, dtype=np.uint8)
cand = 0
found = set()
by4 = {}
for p in pats:
    by4.setdefault(p[:4], []).append(p)
for r in range(4):
    n = (arr.size - r) // 4
    if n <= 0:
        continue
    st = arr[r:r + 4 * n].view(">u4")
    hit = lut[st >> np.uint32(16)]
    idx = np.flatnonzero(hit)
    if idx.size:
        vals = st[idx].astype(np.uint64)
        pos = np.searchsorted(skeys, vals)
        pos[pos >= skeys.size] = 0
        ok = skeys[pos] == vals
        idx = idx[ok]
        cand += idx.size
        for i in idx.tolist():
            off = r + 4 * i
            k = blob[off:off + 4]
            for p in by4.get(k, ()):
                if blob[off:off + 16] == p:
                    found.add(p)
print("B numpy stream: %.2fs cand=%d hits=%d" % (time.time() - t, cand, len(found)))

# C: regex alternation over 16-byte literals
t = time.time()
rx = re.compile(b"|".join(re.escape(p) for p in pats))
found_c = {m.group(0) for m in rx.finditer(blob)}
print("C regex alt: %.2fs hits=%d" % (time.time() - t, len(found_c)))
