import os
for root, dirs, files in os.walk("Assets/_Project/Scripts"):
    for f in files:
        if "HectonAtmosphereManager" in f:
            print(os.path.join(root, f))
