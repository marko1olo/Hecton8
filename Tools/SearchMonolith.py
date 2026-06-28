import re

with open('Assets/_Project/Scripts/FaunaDirector.cs', 'r') as f:
    content = f.read()

# search for Vector3[]
for i, line in enumerate(content.split('\n')):
    if 'Vector3[]' in line or 'float[]' in line:
        print(f"Line {i+1}: {line.strip()}")
