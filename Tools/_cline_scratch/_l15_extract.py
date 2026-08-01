# -*- coding: utf-8 -*-
import os
ROOT = r"C:\hades\Hecton8"
OUT = os.path.join(ROOT, r"Tools\_cline_scratch\_l15_grep_py.txt")
files = [
    r"Assets\_Project\Scripts\HectonPlayerMovement.cs",
    r"Assets\_Project\Scripts\Gameplay\HectonPlayerInputHandler.cs",
    r"Assets\_Project\Scripts\Editor\Diagnostics\H8_HeadlessWorldDriver.cs",
    r"Assets\_Project\Scripts\Core\SystemDispatcher.cs",
    r"Assets\_Project\Scripts\Core\RegistryBucket.cs",
    r"Assets\_Project\Scripts\Core\InputDispatcher.cs",
]
patterns = [
    "FixedTick", "SampleGameplayLocomotion", "ProcessPlayerInputFrame", "TryReadFrame", "GetState",
    "DiagMarkReadHop", "readHop", "EnsureDispatcherRegistration", "TryRegisterToDispatchers",
    "_registeredFixedTick", "EnsureGameplayLocomotion", "PublishLocomotion", "ShouldSkipLane",
    "CurrentMovementIntent", "ResolveRawInputIntent", "IFixedTick", "RegisterFixed",
    "hop2", "INPUTHOP", "ApplyAutomationOverride", "TryRegister", "Unregister", "PlayerLane",
    "blockGameplay", "IsGameReady", "SampleObservables", "movementIntent",
]
out = []
for f in files:
    p = os.path.join(ROOT, f)
    exists = os.path.isfile(p)
    size = os.path.getsize(p) if exists else 0
    out.append("===== %s exists=%s size=%d =====" % (f, exists, size))
    print("FILE", f, exists, size)
    if not exists:
        continue
    with open(p, "r", encoding="utf-8", errors="replace") as fh:
        lines = fh.readlines()
    out.append("total_lines=%d" % len(lines))
    for i, l in enumerate(lines, 1):
        low = l.lower()
        if any(pat.lower() in low for pat in patterns):
            out.append("%d|%s" % (i, l.rstrip()))
text = "\n".join(out)
with open(OUT, "w", encoding="utf-8") as fh:
    fh.write(text)
print("WROTE", OUT, "bytes", len(text), "lines", len(out))
