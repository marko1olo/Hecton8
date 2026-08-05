import os
for f in os.listdir("Assets/_Project/Tests/Editor/"):
    if "HectonCelestialEngine" in f and "EditTests" not in f:
        print(f)
