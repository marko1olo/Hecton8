import re
with open('Assets/_Project/Scripts/FaunaDirector.cs', 'r') as f:
    lines = f.readlines()
    for i, line in enumerate(lines):
        if 'force' in line.lower() or 'steer' in line.lower() or 'trail' in line.lower() or 'pheromone' in line.lower():
            print(f"Line {i+1}: {line.strip()}")
