import re

with open('./Assets/_Project/Scripts/BoidSimulation.compute', 'r') as f:
    text = f.read()

match = re.search(r'// \s*──\s*2\.\s*ALIGNMENT.*?\{.*?\}', text, re.DOTALL | re.IGNORECASE)
if match:
    print(match.group(0))
