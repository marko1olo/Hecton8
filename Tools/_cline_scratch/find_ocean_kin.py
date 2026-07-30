# -*- coding: utf-8 -*-
import os

ROOT = r"C:\hades\Hecton8"
ASSETS = os.path.join(ROOT, "Assets")

print("=== OceanKinematics files ===")
for dp, dns, fns in os.walk(ASSETS):
    for fn in fns:
        if "OceanKinematics" in fn and fn.endswith(".cs"):
            print(os.path.join(dp, fn))

print("=== GameBootstrapper OceanKinematics lines ===")
gb = os.path.join(ROOT, r"Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs")
lines = open(gb, encoding="utf-8", errors="replace").read().splitlines()
for i, l in enumerate(lines):
    if "OceanKinematics" in l:
        print("%d|%s" % (i + 1, l[:220]))

print("=== NativeMemoryTrackingBridge IsInstalled ===")
for dp, dns, fns in os.walk(ASSETS):
    for fn in fns:
        if "NativeMemoryTracking" in fn and fn.endswith(".cs"):
            path = os.path.join(dp, fn)
            print("FILE", path)
            ls = open(path, encoding="utf-8", errors="replace").read().splitlines()
            for i, l in enumerate(ls):
                if "IsInstalled" in l or "class NativeMemoryTrackingBridge" in l or "Install" in l:
                    print("  %d|%s" % (i + 1, l[:200]))

print("=== InitializeBootstrapDependencyNode switch Ocean ===")
# find method and surrounding cases
for i, l in enumerate(lines):
    if "InitializeBootstrapDependencyNode(" in l and "bool" in l:
        print("METHOD", i + 1, l.strip()[:120])
    if "case BootstrapDependencyNode.OceanKinematicsRuntimeService" in l:
        # print next 30 lines of context around handler
        lo = max(0, i - 2)
        hi = min(len(lines), i + 40)
        print("--- case context %d ---" % (i + 1))
        for j in range(lo, hi):
            print("%d|%s" % (j + 1, lines[j][:200]))
