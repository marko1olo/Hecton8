# -*- coding: utf-8 -*-
import sys
path = r"C:\hades\Hecton8\Tools\Blender\h8forge\texture.py"
start = int(sys.argv[1]) if len(sys.argv) > 1 else 690
end = int(sys.argv[2]) if len(sys.argv) > 2 else 1120
with open(path, encoding="utf-8") as f:
    lines = f.read().splitlines()
for i in range(start - 1, min(end, len(lines))):
    print(f"{i+1}|{lines[i]}")
