import pathlib
out=[]
idpath=r"C:/hades/Hecton8/Assets/_Project/Scripts/Core/InputDispatcher.cs"
lines=pathlib.Path(idpath).read_text(encoding="utf-8",errors="replace").splitlines()
keys=["publishGuard","PublishInput","TryPublish","_currentState","override","ApplyWorld","WorldDriver","SetOverride","overrideRejected","publishOk","DiagRecord","IsPlayerInputEnabled","_playerInputEnabled","MoveDelta","refusalMask","blockMask","inputEnabled","lastOverride","currentStateMove"]
out.append("IDLINES "+str(len(lines)))
for i,l in enumerate(lines):
  if any(k in l for k in keys):
    out.append(str(i+1)+chr(124)+l[:200])
pathlib.Path(r"C:/hades/Hecton8/.agent_mem/_sa_id_hits2.txt").write_text(chr(10).join(out),encoding="utf-8")
print("OK",len(out))
