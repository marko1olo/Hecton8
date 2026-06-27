import re

with open('Assets/_Project/Scripts/FaunaDirector.cs', 'r') as f:
    content = f.read()

# search for "Calculate steering force vector pointing toward dense trail coordinates to follow migratory paths."
for i, line in enumerate(content.split('\n')):
    if 'Calculate steering' in line:
        print(f"Line {i+1}: {line.strip()}")
