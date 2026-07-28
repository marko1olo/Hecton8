import json, sys, time

gm = json.load(open('_guidmap.json'))


def sw(h):
    r = bytes.fromhex(h)
    return bytes(((b & 0x0F) << 4) | ((b >> 4) & 0x0F) for b in r)


lut = {}
for g, path in gm.items():
    try:
        lut[sw(g)] = (g, path)
    except ValueError:
        pass

scene = sys.argv[1] if len(sys.argv) > 1 else 'Assets/_Project/Scenes/02_HECTON_WORLD.unity'
data = open(scene, 'rb').read()
t0 = time.time()
found = {}
mv = memoryview(data)
n = len(data)
for i in range(n - 16):
    key = bytes(mv[i:i + 16])
    hit = lut.get(key)
    if hit is not None:
        found.setdefault(hit[1], []).append(i)
print('scanned %d bytes in %.1fs, distinct referenced assets: %d' % (n, time.time() - t0, len(found)))
for path in sorted(found):
    offs = found[path]
    print('  x%-3d %s' % (len(offs), path))
