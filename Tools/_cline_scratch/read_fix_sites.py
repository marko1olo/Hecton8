# -*- coding: utf-8 -*-
import os

ROOT = r"C:\hades\Hecton8"

def dump(path, ranges):
    lines = open(path, encoding="utf-8", errors="replace").read().splitlines()
    print("====", path, "total", len(lines))
    for a, b in ranges:
        print("--- %d-%d ---" % (a, b))
        for i in range(a - 1, min(b, len(lines))):
            print("%d|%s" % (i + 1, lines[i]))

# Ocean service readiness + refresh
svc = os.path.join(ROOT, r"Assets\_Project\Scripts\Core\OceanKinematicsRuntimeService.cs")
dump(svc, [(220, 350), (380, 515)])

# Bootstrap: IsBootstrapDependencyNodeReady, TryEnsureDeferredCaustics, exception log, layer init
gb = os.path.join(ROOT, r"Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs")
lines = open(gb, encoding="utf-8", errors="replace").read().splitlines()
print("==== searching bootstrap markers ====")
for i, l in enumerate(lines):
    if any(k in l for k in (
        "IsBootstrapDependencyNodeReady",
        "TryEnsureDeferredCaustics",
        "Bootstrap dependency exception",
        "InitializeBootstrapLayerNodesAsync",
        "LogBootstrapDependencyFailure",
        "ValidateOceanKinematicsPluginContract",
        "PersistRuntimeService",
    )):
        print("%d|%s" % (i + 1, l[:180]))

# dump key methods
for i, l in enumerate(lines):
    if "private static bool IsBootstrapDependencyNodeReady" in l:
        dump(gb, [(i + 1, i + 80)])
    if "TryEnsureDeferredCausticsRegistered" in l and ("bool" in l or "void" in l) and "(" in l:
        dump(gb, [(i + 1, i + 60)])
    if "private static void ValidateOceanKinematicsPluginContract" in l:
        dump(gb, [(i + 1, i + 60)])
    if "InitializeBootstrapLayerNodesAsync" in l and "Awaitable" in l:
        dump(gb, [(i + 1, i + 50)])

# NativeMemoryTrackingBridgeInstaller full
inst = os.path.join(ROOT, r"Assets\_Project\Scripts\Core\NativeMemoryTrackingBridgeInstaller.cs")
dump(inst, [(1, 120)])

# NativeMemoryTrackingBridge RegisterNativeArrayInstance
br = os.path.join(ROOT, r"Assets\_Project\Scripts\Core\Contracts\NativeMemoryTrackingBridge.cs")
dump(br, [(1, 150)])

# Other CreateTransientPayload callers - do they catch?
print("==== CreateTransientPayload callers ====")
for dp, dns, fns in os.walk(os.path.join(ROOT, "Assets")):
    for fn in fns:
        if not fn.endswith(".cs"):
            continue
        p = os.path.join(dp, fn)
        try:
            txt = open(p, encoding="utf-8", errors="replace").read()
        except Exception:
            continue
        if "CreateTransientPayload" in txt:
            print(p)
            ls = txt.splitlines()
            for i, l in enumerate(ls):
                if "CreateTransientPayload" in l:
                    lo = max(0, i - 5)
                    hi = min(len(ls), i + 8)
                    for j in range(lo, hi):
                        print("  %d|%s" % (j + 1, ls[j][:160]))
                    print()
