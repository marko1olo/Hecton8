import re
import os

def search_files():
    for root, dirs, files in os.walk('Assets/_Project/Scripts'):
        for file in files:
            if file.endswith('.cs'):
                filepath = os.path.join(root, file)
                try:
                    with open(filepath, 'r') as f:
                        lines = f.readlines()
                        for i, line in enumerate(lines):
                            if 'Calculate' in line and 'Vector3[]' in line and 'float[]' in line:
                                print(f"{filepath}:{i+1}: {line.strip()}")
                except Exception:
                    pass

search_files()
