import re

with open('Assets/_Project/Scripts/Editor/DataMonolith/H8AndroidAssetBridge1504StaticAudit.cs', 'r') as f:
    lines = f.readlines()

for i, line in enumerate(lines):
    if 'internal static void Run(string projectRoot)' in line:
        print(f"Start Run: {i+1}")
    if 'StringBuilder builder = new StringBuilder(4096);' in line:
        print(f"Start JSON: {i+1}")
