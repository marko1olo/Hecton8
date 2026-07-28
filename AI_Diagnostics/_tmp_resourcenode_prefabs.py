import os, re

RESOURCE_NODE_GUID = '1c03cda7357c1ab45be71074a063379e'
root = 'Assets'
hits = []
for dp, dn, fn in os.walk(root):
    for f in fn:
        if not f.endswith('.prefab'):
            continue
        p = os.path.join(dp, f)
        try:
            with open(p, 'r', encoding='utf-8', errors='ignore') as fh:
                txt = fh.read()
        except Exception:
            continue
        if RESOURCE_NODE_GUID in txt:
            tmpl = re.search(r'^  resourceTemplate:\s*(.+)$', txt, re.M)
            loot = re.search(r'^  lootPrefab:\s*(.+)$', txt, re.M)
            cnt = re.search(r'^  lootCount:\s*(.+)$', txt, re.M)
            marker = 'PoolItemMarker' in txt
            hits.append((p.replace(os.sep, '/'),
                         tmpl.group(1).strip() if tmpl else 'ABSENT',
                         loot.group(1).strip() if loot else 'ABSENT',
                         cnt.group(1).strip() if cnt else 'ABSENT',
                         marker))
print('prefabs containing ResourceNode:', len(hits))
for h in hits:
    print(' PATH:', h[0])
    print('   resourceTemplate:', h[1], '| lootPrefab:', h[2], '| lootCount:', h[3], '| PoolItemMarkerText:', h[4])

print()
print('=== ResourceNodeTemplate assets: lootPickupPrefab / extractorYieldItem presence ===')
tdir = 'Assets/_Project/Data/Scavenging/ResourceNodes'
for f in sorted(os.listdir(tdir)):
    if not f.endswith('.asset'):
        continue
    txt = open(os.path.join(tdir, f), 'r', encoding='utf-8', errors='ignore').read()
    lp = re.search(r'^  lootPickupPrefab:\s*(.+)$', txt, re.M)
    ex = re.search(r'^  extractorYieldItem:\s*(.+)$', txt, re.M)
    rc = re.search(r'^  requiredToolClass:\s*(.+)$', txt, re.M)
    dl = re.search(r'^  defaultLootCount:\s*(.+)$', txt, re.M)
    print('%-58s loot=%-58s extractor=%-22s toolClass=%-4s count=%s' % (
        f,
        lp.group(1).strip() if lp else 'ABSENT',
        ex.group(1).strip() if ex else 'ABSENT',
        rc.group(1).strip() if rc else 'ABSENT',
        dl.group(1).strip() if dl else 'ABSENT'))
