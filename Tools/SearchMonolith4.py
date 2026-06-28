import re

with open('Assets/_Project/Scripts/FaunaDirector.cs', 'r') as f:
    content = f.read()

for i, line in enumerate(content.split('\n')):
    if 'migratory' in line.lower() or 'pheromone' in line.lower() or 'tracking' in line.lower() or 'steer' in line.lower():
        print(f"Line {i+1}: {line.strip()}")
