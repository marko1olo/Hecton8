# -*- coding: utf-8 -*-
"""Print a line range of a file with 1-based numbers. Usage: _cline_slice.py path start end"""
from __future__ import annotations
import sys
from pathlib import Path

path = Path(sys.argv[1])
start = int(sys.argv[2])  # 1-based inclusive
end = int(sys.argv[3])    # 1-based inclusive
lines = path.read_text(encoding="utf-8").splitlines()
for i in range(start - 1, min(end, len(lines))):
    print(f"{i+1}|{lines[i]}")
