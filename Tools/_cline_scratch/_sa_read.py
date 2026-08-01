import pathlib 
p=pathlib.Path(r'Assets/_Project/Scripts/HectonPlayerMovement.cs') 
lines=p.read_text(encoding='utf-8',errors='replace').splitlines() 
keys=['SampleGameplayLocomotionInputForFixedStep','ProcessPlayerInputFrame','void FixedTick','void Tick(','PrepareTransportAndFrameState','ResolveRawInputIntentVector','IsGameplayInputBlockedByMenu','ProcessWipeoutInputOverride','ResolveInputManagerBinding','HandleMenuBlockedInput'] 
for k in keys: 
  hits=[(i,l.strip()[:180]) for i,l in enumerate(lines,1) if k in l] 
  print('===',k,'count',len(hits)) 
  for i,l in hits[:20]: print(f'{i}: {l}') 
