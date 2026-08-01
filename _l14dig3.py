import pathlib
def dump(path,a,b):
 L=pathlib.Path(path).read_text(encoding="utf-8",errors="replace").splitlines()
 print("FILE",path,a,b)
 for i in range(a,min(b,len(L))+1):
  print(str(i)+chr(124)+L[i-1][:240])
dump(r"Assets/_Project/Scripts/HectonPlayerMovement.cs",4760,4825)
dump(r"Assets/_Project/Scripts/HectonPlayerMovement.cs",5015,5040)
dump(r"Assets/_Project/Scripts/UI/PauseMenuController.cs",740,780)
dump(r"Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs",3155,3180)
dump(r"Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessWorldDriver.cs",2668,2705)
