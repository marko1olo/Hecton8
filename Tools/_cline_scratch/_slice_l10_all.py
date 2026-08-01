# -*- coding: utf-8 -*-
import os, re

out_path = r"C:\hades\Hecton8\Tools\_cline_scratch\_l10_slices.txt"
chunks = []

def slice_file(path, ranges, label=None):
    lines = open(path, encoding="utf-8", errors="replace").read().splitlines()
    chunks.append("\n##### %s (%s lines) #####\n" % (label or path, len(lines)))
    for a, b in ranges:
        chunks.append("\n=== %s:%d-%d ===\n" % (os.path.basename(path), a, b))
        for i in range(a, min(b, len(lines)) + 1):
            chunks.append("%5d|%s\n" % (i, lines[i - 1]))

def find_lines(path, patterns):
    lines = open(path, encoding="utf-8", errors="replace").read().splitlines()
    chunks.append("\n##### HITS %s #####\n" % path)
    for i, line in enumerate(lines, 1):
        for p in patterns:
            if p.lower() in line.lower():
                chunks.append("%5d|%s\n" % (i, line.rstrip()))
                break

# Inventory
inv = r"C:\hades\Hecton8\Assets\_Project\Scripts\PlayerInventory.cs"
find_lines(inv, [
    "CanServiceItemAdds", "TryRecover", "TryBindRuntimeStorageCold",
    "RefreshPlayerInventoryVaultHandlesCold", "enabled = false",
    "InitializeSoaQueryEngine", "Awake", "OnEnable"
])
# get line numbers then slice key regions from hits later

# Input handler
ih = r"C:\hades\Hecton8\Assets\_Project\Scripts\Gameplay\HectonPlayerInputHandler.cs"
if os.path.isfile(ih):
    find_lines(ih, ["TryReadFrame", "GetState", "CurrentInputState", "MoveDelta"])
else:
    chunks.append("MISSING " + ih + "\n")
    # search
    root = r"C:\hades\Hecton8\Assets\_Project\Scripts"
    for dp, dns, fns in os.walk(root):
        for fn in fns:
            if "InputHandler" in fn and fn.endswith(".cs"):
                chunks.append("FOUND " + os.path.join(dp, fn) + "\n")

# PTM grant
ptm = r"C:\hades\Hecton8\Assets\_Project\Scripts\PlayerToolManager.cs"
find_lines(ptm, [
    "RetryRuntimeStartToolGrantIfPending", "TryGrantAssignedToolItemsOnRuntimeStart",
    "STARTERGRANT", "refusalMask", "0x1E", "_registeredToFixedTick"
])

# HPM binding / menu / vehicle
hpm = r"C:\hades\Hecton8\Assets\_Project\Scripts\HectonPlayerMovement.cs"
find_lines(hpm, [
    "ResolveInputManagerBinding", "IsGameplayInputBlockedByMenu",
    "IsAuthoritativeVehicleTransportActive", "LocomotionHold",
    "currentSuitData", "_juiceProcessor"
])

# InputDispatcher hop
inp = r"C:\hades\Hecton8\Assets\_Project\Scripts\Core\InputDispatcher.cs"
find_lines(inp, ["DiagRecordReadObservation", "GetState", "CurrentInputState", "readHop"])

open(out_path, "w", encoding="utf-8").writelines(chunks)
print("wrote", out_path, "chars", sum(len(c) for c in chunks))
