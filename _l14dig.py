import pathlib,os,json 
p=pathlib.Path(r'Assets/_Project/Scripts/UI/PauseMenuController.cs') 
L=p.read_text(encoding='utf-8',errors='replace').splitlines() 
h=[] 
for i,l in enumerate(L,1): 
  if '_openMenuCount' in l or 'IsAnyOpen' in l: 
   h.append(str(i)+chr(124)+l.strip()[:220]) 
pathlib.Path('_pause_cnt.txt').write_text(chr(10).join(h),encoding='utf-8') 
print('pause',len(h)) 
