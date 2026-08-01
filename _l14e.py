import pathlib, os
os.chdir(r"C:/hades/Hecton8")

def dump(path, start, end, outpath):
    lines = pathlib.Path(path).read_text(encoding="utf-8", errors="replace").splitlines()
    s = max(1, start); e = min(len(lines), end)
    out = []
    for i in range(s, e+1):
        out.append(str(i)+"|"+lines[i-1])
    pathlib.Path(outpath).write_text(chr(10).join(out), encoding="utf-8")
    print(path, s, e, len(out))

def grep(path, keys, outpath, limit=400):
    lines = pathlib.Path(path).read_text(encoding="utf-8", errors="replace").splitlines()
    out = []
    for i, l in enumerate(lines, 1):
        if any(k in l for k in keys):
            out.append(str(i)+"|"+l[:240])
            if len(out) >= limit:
                break
    pathlib.Path(outpath).write_text(chr(10).join(out), encoding="utf-8")
    print("grep", path, len(out))

grep(r"Assets/_Project/Scripts/Core/InputDispatcher.cs", ["DiagRecordReadObservation","DiagEmitHopCensus","readHop","GetState","INPUTHOP","DiagRecord"], "_l14_idisp_grep.txt")
dump(r"Assets/_Project/Scripts/Core/InputDispatcher.cs", 1180, 1320, "_l14_idisp_hop.txt")
lines = pathlib.Path(r"Assets/_Project/Scripts/Core/InputDispatcher.cs").read_text(encoding="utf-8", errors="replace").splitlines()
for i, l in enumerate(lines, 1):
    if "GetState" in l and ("public" in l or "private" in l or "internal" in l):
        print("GS", i, l.strip()[:120])
        dump(r"Assets/_Project/Scripts/Core/InputDispatcher.cs", max(1,i-5), i+120, "_l14_idisp_gs_"+str(i)+".txt")
grep(r"Assets/_Project/Scripts/HectonPlayerMovement.cs", ["SampleGameplayLocomotionInputForFixedStep","TryReadFrame","FixedTick","EnsureDispatcher","_registeredFixedTick","IsAnyOpen","Locomotion","ProcessPlayerInput","GetState","inputService","blockGameplay","waitingOn"], "_l14_hpm_grep.txt")
dump(r"Assets/_Project/Scripts/HectonPlayerMovement.cs", 8000, 8300, "_l14_hpm_sample.txt")
dump(r"Assets/_Project/Scripts/HectonPlayerMovement.cs", 9900, 10150, "_l14_hpm_fixed.txt")
dump(r"Assets/_Project/Scripts/HectonPlayerMovement.cs", 4760, 5550, "_l14_hpm_reg.txt")
dump(r"Assets/_Project/Scripts/Gameplay/HectonPlayerInputHandler.cs", 1, 220, "_l14_hpih.txt")
grep(r"Assets/_Project/Scripts/Core/SystemDispatcher.cs", ["FixedTick","blockGameplay","GameplayLane","PriorityLayer.Player","RunFixed","DispatchFixed","Skip","IsPaused","Pause","_fixedPriority"], "_l14_sd_grep.txt")
