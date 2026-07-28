import os, re, time, json

t = time.time()
idx = {}
root = 'Assets/_Project'
pat = re.compile(r'guid:\s*([0-9a-fA-F]{32})')
for dp, dn, fn in os.walk(root):
    for f in fn:
        if not f.endswith('.meta'):
            continue
        p = os.path.join(dp, f)
        try:
            with open(p, 'r', encoding='utf-8', errors='ignore') as fh:
                head = fh.read(200)
        except Exception:
            continue
        m = pat.search(head)
        if m:
            idx[m.group(1)] = p[:-5].replace(os.sep, '/')
print('indexed', len(idx), 'in %.1fs' % (time.time() - t))

rows = [l.rstrip('\n').split('|') for l in open('/tmp/ext_guids.txt')]
keys = ('Resources/Nodes', 'Scavenging', 'ResourceNode', 'Pickup', 'Ore', 'Loot')
res = []
unknown = 0
for g, t2, pa in rows:
    p = idx.get(g)
    if p is None:
        unknown += 1
        continue
    if any(k in p for k in keys):
        res.append((g, t2, p))
print('externals total', len(rows), 'unresolved', unknown)
print('--- resource-related scene externals:', len(res))
for r in res:
    print(' ', r[1], r[2])
json.dump(idx, open('/tmp/guididx.json', 'w'))
