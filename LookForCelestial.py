import os
for root, dirs, files in os.walk("Assets/_Project/Tests/PlayMode"):
    for f in files:
        if "Celestial" in f:
            print(os.path.join(root, f))
