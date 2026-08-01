import re
from pathlib import Path
root=Path(r"C:/hades/Hecton8")
out=root/"_eco_hits2.txt"
needles=["RequestHeadlessTimeDilation","TimeDilationScalar","_timeDilation","SimulationPaused","RequestSimulationPause","IsOriginShiftBootstrapLocked","TryFlushInitialSceneRebase","CopyBootstrapDrainSnapshot","_physicsPauseActive","ResumePhysicsAfterShift","PublishGameReady","IsGameReady","RunFrostTick","RunDispatcherUpdate","Fast-Frost","FastFrost","timeScale","HeadlessTimeDilationMaximum","ProcessPending","SceneRebaseTickLock","ecology ready","ecology wait","TryMarkEcologyReady","Player LateFrame","PriorityLayer.Player"]
def hit(line):
 return any(n in line for n in needles)
files=[]
for rel in ["Assets/_Project/Scripts/GameTickManager.cs","Assets/_Project/Scripts/ITickable.cs","Assets/_Project/Scripts/HectonFloatingOrigin.cs","Assets/_Project/Scripts/Core/SystemDispatcher.cs","Assets/_Project/Scripts/Core/BootstrapContracts/BootstrapState.cs","Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs","Assets/_Project/Scripts/QA/Headless/Editor/HeadlessSimulationBatchRunner.cs","BACKLOG.md","Docs/AgentLogs/p0_fo_bootstrap_lock_drain_20260731.md","Docs/AgentLogs/p0_ecology_ready_frost_starve_20260731.md","Docs/AgentLogs/HANDOFF_p0_ecology_clock_20260731.md","Docs/AgentLogs/HeadlessSimulationResult_HEADLESS_SIMULATION_RUNNER.json","Docs/AgentLogs/HeadlessSimulationResult_HEADLESS_SIMULATION_RUNNER.json.bak_pre_ecology_clock"]:
 files.append(root/rel)
for pth in (root/"Assets/_Project/Scripts").rglob("*.cs"):
 n=pth.name.lower()
 if any(x in n for x in ["bootstrap","pause","dilation","ecosystemdirector","fauna"]):
  if pth not in files: files.append(pth)
lines=[]
for fp in files:
 if not fp.exists():
  lines.append("MISSING "+str(fp)); continue
 rel=str(fp.relative_to(root)).replace(chr(92),"/")
 t=fp.read_text(encoding="utf-8",errors="replace").splitlines()
 hits=[]
 for i,l in enumerate(t,1):
  if hit(l): hits.append(str(i)+":"+l.rstrip()[:220])
 if hits:
  lines.append("==== "+rel+" ("+str(len(hits))+") ====")
  lines.extend(hits[:150])
  if len(hits)>150: lines.append("... "+str(len(hits)-150)+" more")
out.write_text(chr(10).join(lines),encoding="utf-8")
print("wrote",out,out.stat().st_size,"files",len(files))
