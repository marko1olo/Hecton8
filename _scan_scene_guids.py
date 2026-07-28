import json, os

gm = json.load(open('_guidmap.json'))
inv = {}
for k, v in gm.items():
    inv[v.replace(chr(92), '/')] = k

targets = [
    'Assets/_Project/Scripts/HectonAtmosphereManager.cs',
    'Assets/_Project/Scripts/ObserverRelativeCelestialBody.cs',
    'Assets/_Project/Art/Materials/Sky/MAT_AegirSky_Master.mat',
    'Assets/_Project/Art/Materials/Sky/Hecton_AegirSky_Mat.mat',
    'Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs',
    'Assets/_Project/Art/Models/gasgiant.asset',
    'Assets/_Project/Art/TEXTURES/Aegir_storms.png',
    'Assets/_Project/Art/TEXTURES/clouds0_diff.png',
    'Assets/_Project/Prefabs/GasGiant_Aegir.prefab',
    'Assets/_Project/Scripts/HectonCelestialEngine.cs',
    'Assets/_Project/Art/Materials/Mat_HectonSky.mat',
]


def sw(h):
    r = bytes.fromhex(h)
    return bytes(((b & 0x0F) << 4) | ((b >> 4) & 0x0F) for b in r)


for scene in ['Assets/_Project/Scenes/02_HECTON_WORLD.unity',
              'Assets/_Project/Scenes/01_ORBIT.unity',
              'Assets/_Project/Scenes/00_BOOTSTRAP.unity']:
    data = open(scene, 'rb').read()
    is_text = data[:5] == b'%YAML'
    print('=== %s (%s) ===' % (scene, 'TEXT' if is_text else 'BINARY'))
    for t in targets:
        g = inv.get(t)
        if not g:
            print('    NO GUID  ' + t)
            continue
        n = data.count(g.encode()) if is_text else data.count(sw(g))
        print('    %3d  %s' % (n, t))
