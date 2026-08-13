import os

with open("Assets/_Project/Scripts/HectonCelestialEngine.cs", "r") as f:
    lines = f.readlines()

# Let's find public methods we can test
public_methods = []
for i, line in enumerate(lines):
    if "public void " in line or "public float " in line or "public bool " in line or "public Vector3 " in line:
        if "class " not in line and "struct " not in line:
            public_methods.append((i+1, line.strip()))

for pm in public_methods:
    print(f"{pm[0]}: {pm[1]}")
