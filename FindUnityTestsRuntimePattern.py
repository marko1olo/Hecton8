import os
for root, dirs, files in os.walk("Assets/_Project/Tests/Runtime"):
    if len(files) > 0:
        print(f"Found {len(files)} files in {root}")
