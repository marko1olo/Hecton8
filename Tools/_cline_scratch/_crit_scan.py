import pathlib,re 
root=pathlib.Path(r'C:/hades/Hecton8') 
pats=['*PlayModeSmoke*','*MainMenuController*','*GameBootstrapper*','*FaunaBrain*','*LifePod*','*DropPod*'] 
for pat in pats: 
  hits=list((root/'Assets').rglob(pat)) 
  print('GLOB',pat,len(hits)) 
  for h in hits[:40]: print(' ',h.relative_to(root)) 
p=root/'Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessPlayModeProbe.cs' 
print('PROBE_EXISTS',p.exists()) 
t=p.read_text(encoding='utf-8',errors='replace') 
print('probe_lines',t.count(chr(10))+1) 
rx=re.compile('forceMenuLoad|ForceMenu|hardTimeout|h8Timeout|nographics|ScreenCapture|CaptureScreenshot|MainMenuController|LoadingMenu|MarkMainMenu|startNewGame|h8StartGame|V0_Playtest|menuWait|MenuWait|BUDGET|Screenshot|PNG|EditorApplication',re.I) 
for i,l in enumerate(t.splitlines(),1): 
  if rx.search(l): print(f'{i}:{l[:240]}') 
