import pathlib, os
os.chdir(r"C:/hades/Hecton8")
print("ok", os.getcwd()))
print("files", len(list(pathlib.Path("Assets/_Project/Scripts").glob("**/*.cs"))))
