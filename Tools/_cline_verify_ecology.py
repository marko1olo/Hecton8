from pathlib import Path 
t=Path(r'Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs').read_text(encoding='utf-8') 
checks={'_pendingDayAudits':t.count('_pendingDayAudits'),'DrainPendingDayAudits':t.count('DrainPendingDayAudits'),'PriorityLayer.Player':t.count('PriorityLayer.Player'),'FIX 2026-07-30':t.count('FIX 2026-07-30')} 
[print(k, v) for k,v in checks.items()] 
ok=all([checks['_pendingDayAudits']>=7, checks['DrainPendingDayAudits']>=2, checks['PriorityLayer.Player']>=3, checks['FIX 2026-07-30']>=1]) 
print('OK' if ok else 'FAIL') 
