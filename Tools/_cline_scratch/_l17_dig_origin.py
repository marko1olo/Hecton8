# L17: origin lock / AUP / AdvanceStepBoundedClock / EnsureDispatcherRegistration / FixedStep max
import re, os
ROOT = r"C:\hades\Hecton8"
OUT = os.path.join(ROOT, r"Tools\_cline_scratch\_l17_origin.txt")

def rl(rel):
    with open(os.path.join(ROOT, rel), encoding="utf-8", errors="replace") as f:
        return f.read().splitlines()

def sl(L,a,b):
    a=max(1,a); b=min(len(L),b)
    return [f"{i}|{L[i-1]}" for i in range(a,b+1)]

def fa(L,pat,flags=0):
    rx=re.compile(pat,flags)
    return [(i+1,L[i]) for i in range(len(L)) if rx.search(L[i])]

o=[]
sd=rl(r"Assets\_Project\Scripts\Core\SystemDispatcher.cs")

# constants
for ln,t in fa(sd, r"MaxFixedSubstepsPerFrame|FixedStepSeconds|MaxStepBoundedDeltaSeconds|HeadlessTimeDilationMaximum"):
    if "const" in t or "=" in t and ("Max" in t or "Fixed" in t or "Headless" in t):
        o.append(f"CONST {ln}|{t.strip()}")

# AdvanceStepBoundedClock
for ln,t in fa(sd, r"AdvanceStepBoundedClock"):
    if "float" in t or "void" in t or "static" in t:
        o.append(f"==== AdvanceStepBoundedClock @{ln} ====")
        o.extend(sl(sd, ln, ln+40))
        break

# IsOriginShiftBootstrapLocked property / field
for ln,t in fa(sd, r"IsOriginShiftBootstrapLocked"):
    o.append(f"OSBL {ln}|{t.strip()[:160]}")
for ln,t in fa(sd, r"IsOriginShiftBootstrapLocked\s*=>|bool IsOriginShiftBootstrapLocked"):
    o.append(f"==== OSBL def @{ln} ====")
    o.extend(sl(sd, ln, ln+15))

for ln,t in fa(sd, r"IsOriginShiftFrameLockedForCurrentFrame"):
    if "=>" in t or "bool" in t and "IsOrigin" in t:
        o.append(f"==== OSFL def @{ln} ====")
        o.extend(sl(sd, ln, ln+20))
        break

# TryFlushInitialSceneRebaseBeforeTicks
for ln,t in fa(sd, r"TryFlushInitialSceneRebaseBeforeTicks"):
    o.append(f"FLUSHREF {ln}|{t.strip()[:140]}")

# Floating origin
fo_paths=[]
for root,ds,fs in os.walk(os.path.join(ROOT,"Assets")):
    for f in fs:
        if "FloatingOrigin" in f and f.endswith(".cs"):
            fo_paths.append(os.path.join(root,f))
o.append(f"FO files: {fo_paths}")
for p in fo_paths[:2]:
    L=open(p,encoding="utf-8",errors="replace").read().splitlines()
    o.append(f"==== {os.path.basename(p)} ====")
    for ln,t in fa(L, r"TryFlushInitialSceneRebaseBeforeTicks|BootstrapLocked|IsBootstrap"):
        o.append(f"  {ln}|{t.strip()[:150]}")
    for ln,t in fa(L, r"bool TryFlushInitialSceneRebaseBeforeTicks|TryFlushInitialSceneRebaseBeforeTicks\s*\("):
        if "bool" in t or "static" in t:
            o.extend(sl(L, ln, ln+50))
            break

# HPM EnsureDispatcherRegistration public API
hpm=rl(r"Assets\_Project\Scripts\HectonPlayerMovement.cs")
for ln,t in fa(hpm, r"EnsureDispatcherRegistration"):
    o.append(f"HPM EnsureDR {ln}|{t.strip()[:140]}")
for ln,t in fa(hpm, r"public\s+void\s+EnsureDispatcherRegistration|void\s+EnsureDispatcherRegistration"):
    o.append(f"==== HPM EnsureDispatcherRegistration @{ln} ====")
    o.extend(sl(hpm, ln, ln+25))
    break

# SystemDispatcher.Register for IFixedTickable
for ln,t in fa(sd, r"bool Register\(IFixedTickable|Register\(IFixedTickable"):
    o.append(f"==== SD Register Fixed @{ln} ====")
    o.extend(sl(sd, ln, ln+40))
    break
for ln,t in fa(sd, r"GetFixedLane"):
    if "RegistryBucket" in t or "=>" in t or "static" in t:
        o.append(f"GetFixedLane {ln}|{t.strip()[:140]}")

# L16 log origin / AUP / bootstrap
logp=os.path.join(ROOT,r"Docs\AgentLogs\h8_playprobe_v0_L16.log")
log=open(logp,encoding="utf-8",errors="replace").read().splitlines()
o.append("==== L16 origin/AUP/bootstrap lines ====")
keys=("origin","aup","bootstrap","rebase","floating","game ready","isgameready","simulation halted","safehalt","step-bounded determinism","STEP-BOUNDED")
n=0
for i,line in enumerate(log,1):
    low=line.lower()
    if any(k in low for k in keys):
        if "texture" in low or "shader" in low:
            continue
        o.append(f"L{i}:{line[:220]}")
        n+=1
        if n>=60:
            break
o.append(f"(showed {n} lines)")

# counts
text="\n".join(log)
for pat in [r"origin", r"AUP", r"Bootstrap", r"IsGameReady", r"GameReady", r"rebase", r"STEP-BOUNDED", r"SimulationHalted", r"SafeHalt", r"lateFrameTick=49", r"pumpFired"]:
    o.append(f"count {pat}={len(re.findall(pat,text,re.I))}")

# Full INPUTHOP lines
for i,line in enumerate(log,1):
    if "INPUTHOP" in line or "SIMCLOCK" in line:
        o.append(f"FULL L{i}:{line[:500]}")

# Check FixedStep vs step bound * dilation
# Probe: step=0.04 dil=100 -> dt=4.0; FixedStep=0.02 MaxSubsteps=?
for ln,t in fa(sd, r"MaxFixedSubstepsPerFrame\s*="):
    o.append(f"MAXSUB {ln}|{t.strip()}")

with open(OUT,"w",encoding="utf-8") as f:
    f.write("\n".join(o))
print("WROTE",OUT,len(o),os.path.getsize(OUT))
