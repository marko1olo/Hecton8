import os,re  
root=r'Assets/_Project'  
pats=[re.compile(p,re.I) for p in [r'INPUTHOP',r'readHop',r'hop2',r'SampleGameplayLocomotionInputForFixedStep',r'TryReadFrame',r'FixedTick',r'GetState']]  
out=[]  
for dp,_,fs in os.walk(root):  
  for f in fs:  
    if not f.endswith('.cs'): continue  
    p=os.path.join(dp,f)  
    try: lines=open(p,encoding='utf-8',errors='ignore').read().splitlines()  
    except: continue  
    for i,l in enumerate(lines,1):  
      for pat in pats:  
        if pat.search(l):  
          out.append(f'{p}:{i}:{l.strip()[:200]}'); break  
open(r'_l14_scan_out.txt','w',encoding='utf-8').write(chr(10).join(out))  
print(len(out))  
