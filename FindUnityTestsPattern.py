import os
for root, dirs, files in os.walk("Assets/_Project/Tests"):
    for file in files:
        if file.endswith("Tests.cs"):
            print(os.path.join(root, file))
            break
    if files:
        break
