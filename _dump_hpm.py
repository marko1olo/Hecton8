import pathlib
lines=pathlib.Path(r"C:/hades/Hecton8/Assets/_Project/Scripts/HectonPlayerMovement.cs").read_text(encoding="utf-8",errors="replace").splitlines()
out=[]
idxs=[i for i,l in enumerate(lines) if "IsGameplayInputBlockedByMenu" in l]
out.append("idxs "+str([i+1 for i in idxs]))
for x in idxs:
  if "bool" in lines[x] or "private" in lines[x]:
    for j in range(x, min(x+40,len(lines))):
      out.append(str(j+1)+chr(124)+lines[j])
    break
out.append("---FIXED---")
for i,l in enumerate(lines):
  if "void FixedTick" in l or "FixedTick(float" in l:
    for j in range(i, min(i+100,len(lines))):
      out.append(str(j+1)+chr(124)+lines[j])
    break
out.append("---INTENT---")
for i,l in enumerate(lines):
  if "_lastPlayerKinematicsIntendedMovement" in l or "ResolveRawInputIntentVector" in l:
    out.append(str(i+1)+chr(124)+lines[i][:200])
pathlib.Path(r"C:/hades/Hecton8/.agent_mem/_sa_hpm_dump2.txt").write_text(chr(10).join(out),encoding="utf-8")
print("OK", len(out))