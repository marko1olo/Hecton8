# -*- coding: utf-8 -*-
import os

ROOT = r"C:\hades\Hecton8"
LOG = os.path.join(ROOT, "Docs", "AgentLogs", "h8_playprobe_v0_L06.log")

def dump_lines(path, a, b):
    lines = open(path, encoding="utf-8", errors="replace").read().splitlines()
    for i in range(a - 1, min(b, len(lines))):
        print("%d|%s" % (i + 1, lines[i][:220]))

print("=== log around Ocean init broader (1900-2150) messages only ===")
ll = open(LOG, encoding="utf-8", errors="replace").read().splitlines()
for i in range(1850, min(2150, len(ll))):
    s = ll[i]
    # message lines typically don't start with spaces or known stack prefixes
    if s.startswith("UnityEngine.") or s.startswith("Hecton8.") or s.startswith("System.") or s.startswith("  at ") or s.startswith("(Filename") or s.strip() == "":
        continue
    if s.startswith("UnityEditor.") or s.startswith("---") or s.startswith("Mono"):
        continue
    print("%d|%s" % (i + 1, s[:240]))

print("=== GameBootstrapper _headlessBootMode / h8headless ===")
gb = os.path.join(ROOT, r"Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs")
gl = open(gb, encoding="utf-8", errors="replace").read().splitlines()
for i, l in enumerate(gl):
    if "headless" in l.lower() or "h8headless" in l or "HeadlessBoot" in l:
        print("%d|%s" % (i + 1, l[:200]))

print("=== EnvironmentRuntimeContextService seismic creation ===")
for i, l in enumerate(gl):
    if "seismic" in l.lower() or "HectonSeismic" in l or "EnvironmentRuntimeContext" in l:
        if i > 5880 and i < 5930:
            print("%d|%s" % (i + 1, l[:200]))

print("=== Environment node case full ===")
dump_lines(gb, 5895, 5945)

print("=== RegisterOceanKinematicsService ===")
for dp, dns, fns in os.walk(os.path.join(ROOT, "Assets")):
    for fn in fns:
        if not fn.endswith(".cs"):
            continue
        p = os.path.join(dp, fn)
        try:
            txt = open(p, encoding="utf-8", errors="replace").read()
        except Exception:
            continue
        if "RegisterOceanKinematicsService" in txt or "void RegisterOceanKinematics" in txt:
            if "GlobalRegistry" not in p and "OceanKinematics" not in p and "GlobalRegistry" not in open(p, encoding="utf-8", errors="replace").read()[:200]:
                pass
            ls = txt.splitlines()
            hits = [i for i, l in enumerate(ls) if "RegisterOceanKinematics" in l]
            if hits:
                print("FILE", p)
                for hi in hits[:8]:
                    for j in range(max(0, hi - 2), min(len(ls), hi + 25)):
                        print("  %d|%s" % (j + 1, ls[j][:180]))
                    print("  ---")

print("=== AbyssalDeferredCausticsRuntime ===")
for dp, dns, fns in os.walk(os.path.join(ROOT, "Assets")):
    for fn in fns:
        if "Caustics" in fn and fn.endswith(".cs"):
            print("FILE", os.path.join(dp, fn))

print("=== Seismic WriteCelestialTelemetryEntry dump call ===")
se = os.path.join(ROOT, r"Assets\_Project\Scripts\Environment\HectonSeismicTideDirector.cs")
sl = open(se, encoding="utf-8", errors="replace").read().splitlines()
for i, l in enumerate(sl):
    if "DumpCelestialTelemetryOnce" in l or "WriteCelestialTelemetryEntry" in l:
        print("%d|%s" % (i + 1, l[:180]))
# dump WriteCelestialTelemetryEntry around 3352
dump_lines(se, 3320, 3365)
dump_lines(se, 3135, 3165)
dump_lines(se, 1375, 1400)

print("=== NativeMemoryTrackingBridgeInstaller full ===")
inst = os.path.join(ROOT, r"Assets\_Project\Scripts\Core\NativeMemoryTrackingBridgeInstaller.cs")
print(open(inst, encoding="utf-8", errors="replace").read())

print("=== CreateTransientPayload full + similar Try pattern elsewhere ===")
# look for IsInstalled checks near CreateTransient
cl = open(os.path.join(ROOT, r"Assets\_Project\Scripts\Core\Contracts\CoreLowLevelUtilities.cs"), encoding="utf-8", errors="replace").read().splitlines()
dump_lines(os.path.join(ROOT, r"Assets\_Project\Scripts\Core\Contracts\CoreLowLevelUtilities.cs"), 100, 250)
