# -*- coding: utf-8 -*-
import os
import re

ROOT = r"C:\hades\Hecton8"
LOG = os.path.join(ROOT, "Docs", "AgentLogs", "h8_playprobe_v0_L06.log")
SVC = os.path.join(ROOT, r"Assets\_Project\Scripts\Core\OceanKinematicsRuntimeService.cs")

print("=== OceanKinematicsRuntimeService key methods ===")
lines = open(SVC, encoding="utf-8", errors="replace").read().splitlines()
print("TOTAL", len(lines))
keys = (
    "InitializeService",
    "EnsureRuntimeInstance",
    "throw",
    "InvalidOperation",
    "Exception",
    "class Ocean",
    "void Awake",
    "Register",
    "GlobalRegistry",
    "IOceanKinematics",
    "EmergencyMock",
)
for i, l in enumerate(lines):
    if any(k in l for k in keys):
        print("%d|%s" % (i + 1, l[:200]))

# print InitializeService body
for i, l in enumerate(lines):
    if "void InitializeService" in l or "public void Initialize" in l:
        print("--- body from", i + 1, "---")
        for j in range(i, min(len(lines), i + 120)):
            print("%d|%s" % (j + 1, lines[j][:200]))
        break

print("=== log exceptions near Ocean fail (2000-2240) exception-like ===")
ll = open(LOG, encoding="utf-8", errors="replace").read().splitlines()
for i in range(1990, min(2245, len(ll))):
    s = ll[i]
    if any(x in s for x in (
        "Exception", "Error", "FAIL", "fail", "Ocean", "NativeFault",
        "Bootstrap dependency", "throw", "NullReference", "InvalidOperation",
        "Argument", "Missing", "Could not", "Unable", "blocked",
    )):
        # skip pure stack frames
        if s.startswith("UnityEngine.") or s.startswith("Hecton8.") or s.startswith("System.") or s.startswith("  at "):
            if "Exception" in s or "Error" in s:
                print("%d|%s" % (i + 1, s[:240]))
            continue
        print("%d|%s" % (i + 1, s[:240]))

print("=== first 30 Exception messages in whole log ===")
count = 0
for i, s in enumerate(ll):
    if re.match(r"^[A-Za-z.]*Exception:", s) or s.startswith("NullReferenceException") or s.startswith("InvalidOperationException") or s.startswith("ArgumentException") or s.startswith("System.Exception"):
        print("%d|%s" % (i + 1, s[:300]))
        # next non-stack content line
        for j in range(i + 1, min(i + 15, len(ll))):
            if ll[j].startswith("  at ") or ll[j].startswith("UnityEngine.") or ll[j].startswith("Hecton8.") or ll[j].startswith("System.") or ll[j].startswith("(Filename"):
                print("%d|%s" % (j + 1, ll[j][:240]))
            elif ll[j].strip() == "":
                continue
            else:
                break
        count += 1
        if count >= 25:
            break

print("=== bootstrap_blackbox / service.init ===")
for i, s in enumerate(ll):
    if "bootstrap.service" in s or "bootstrap_blackbox" in s or "OceanKinematicsRuntime" in s:
        print("%d|%s" % (i + 1, s[:240]))
