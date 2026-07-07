import os
from collections import defaultdict

counts = defaultdict(list)
for root, _, files in os.walk('C:/hades/Hecton8/Assets'):
    for f in files:
        if f.endswith('.asmdef'):
            counts[f].append(os.path.join(root, f))

dupes = {k: v for k, v in counts.items() if len(v) > 1}
if dupes:
    for k, v in dupes.items():
        print(f"Duplicate {k}:")
        for path in v:
            print(f"  {path}")
else:
    print("No duplicates found.")
