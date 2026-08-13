with open("Assets/_Project/Scripts/HectonCelestialEngine.cs", "r") as f:
    lines = f.readlines()
    for i in range(1000-5, 1070):
        print(f"{i+1}: {lines[i].rstrip()}")
