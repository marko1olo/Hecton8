import os

directory = 'Assets/_Project/Scripts'

def find_in_files(directory):
    for root, dirs, files in os.walk(directory):
        for file in files:
            if file.endswith('.cs'):
                file_path = os.path.join(root, file)
                try:
                    with open(file_path, 'r', encoding='utf-8') as f:
                        lines = f.readlines()
                        for i, line in enumerate(lines):
                            if "Calculate steering force vector pointing toward dense trail coordinates to follow migratory paths." in line:
                                print(f"{file_path}:{i+1}: {line.strip()}")
                except Exception as e:
                    pass

find_in_files(directory)
