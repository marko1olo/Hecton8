import re

with open("Assets/_Project/Scripts/Input/UserOptionsPersistence.cs", "r") as f:
    text = f.read()

match = re.search(r'private bool TryApplyLegacyOptionsJson\(string json\)', text)
print('TryApplyLegacyOptionsJson found:', bool(match))
