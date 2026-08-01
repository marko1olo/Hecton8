import pathlib
lines=pathlib.Path(r"C:/hades/Hecton8/_l14_scan_out.txt").read_text(encoding="utf-8",errors="replace").splitlines()
keys=["INPUTHOP","readHop","ReadHop","hop2","Hop2","SampleGameplayLocomotion","TryReadFrame","HectonPlayerMovement","HectonPlayerInputHandler","SystemDispatcher","blockGameplayLanes","LocomotionHold","EnsureDispatcherRegistration","waitingOn"]
out=[]
for k in keys:
    hits=[l for l in lines if k.lower() in l.lower()]
    out.append("==== "+k+" count="+str(len(hits))+" ====")
    out.extend(hits[:100])
    out.append("")
pathlib.Path(r"C:/hades/Hecton8/_l14_filt_out.txt").write_text(chr(10).join(out),encoding="utf-8")
print("wrote",len(out))
