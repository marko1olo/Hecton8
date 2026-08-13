import re

with open("Assets/_Project/Scripts/Core/GlobalRegistry.cs", "r") as f:
    text = f.read()

# Let's see what property name celestial engine is registered under
matches = re.findall(r'public static HectonCelestialEngine \w+', text)
print("HectonCelestialEngine properties:", matches)

with open("Assets/_Project/Scripts/HectonCelestialEngine.cs", "r") as f:
    text = f.read()

matches2 = re.findall(r'public float DebugCelestialTimeScale', text)
print("DebugCelestialTimeScale properties:", matches2)
