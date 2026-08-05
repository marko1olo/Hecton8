with open("Assets/_Project/Scripts/HectonCelestialEngine.cs", "r") as f:
    lines = f.readlines()
    for i in range(8406-3, 8522):
        print(f"{i+1}: {lines[i].rstrip()}")
