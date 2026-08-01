import pathlib,os
L=pathlib.Path(r"Assets/_Project/Scripts/UI/PauseMenuController.cs").read_text(encoding="utf-8",errors="replace").splitlines()
print("---PAUSE---")
for i,l in enumerate(L,1):
 if "_openMenuCount" in l or "IsAnyOpen" in l:
  print(str(i)+chr(124)+l.strip()[:220])
