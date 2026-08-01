import pathlib 
p=pathlib.Path(r'C:/hades/Hecton8/Assets/_Project/Scripts/Core/SystemDispatcher.cs') 
lines=p.read_text(encoding='utf-8',errors='replace').splitlines() 
out=[] 
ranges=[(5180,5335),(5505,5545),(6185,6310),(7110,7150),(1715,1750)] 
for a,b in ranges: 
  out.append('==== %%d-%%d ===='%%(a,b)) 
  for i in range(a-1,min(b,len(lines))): 
