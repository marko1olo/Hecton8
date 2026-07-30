import pathlib,re
root=pathlib.Path(r'C:/hades/Hecton8')
def grep(name,pats,lim=150):
    p=root/name
    t=p.read_text(encoding='utf-8',errors='replace')
    print('===',name,'lines',t.count(chr(10))+1)
    x=re.compile(pats,re.I)
    n=0
    for i,ln in enumerate(t.splitlines(),1):
        if x.search(ln):
            print(str(i)+':'+ln[:220])
            n+=1
            if n>=lim:
                print('...trunc')
                break
grep(r'Assets/_Project/Scripts/Editor/PlayModeSmokeTester.cs', r'WORLD|02_HECTON|LoadScene|MainMenu|EnterPlay|class |Sandbox|menu|scene')
grep(r'Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs', r'MarkMainMenuReached|AreAllSystemsReady|short-circuit|Headless|SceneActivate|allSystemsReady|gameReady|LoadScene|01_MAIN_MENU')
grep(r'Assets/_Project/Scripts/MainMenuController.cs', r'class |Awake|AreAllSystemsReady|ReadableStartNewGame|enabled|StartNewGame|disable')
