with open("Assets/_Project/Scripts/HectonAtmosphereManager.cs", "r") as f:
    lines = f.readlines()
    for i in range(2651-10, 2651+20):
        print(f"{i+1}: {lines[i].rstrip()}")
