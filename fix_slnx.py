import os

with open('Hecton8.slnx', 'r', encoding='utf-8-sig') as f:
    lines = f.readlines()

new_lines = []
for line in lines:
    if '<Project Path="' in line:
        start_idx = line.find('<Project Path="') + 15
        end_idx = line.find('"', start_idx)
        path = line[start_idx:end_idx]
        path_check = path.replace('\\', '/')
        if os.path.exists(path_check):
            new_lines.append(line)
    else:
        new_lines.append(line)

with open('Hecton8.slnx', 'w', encoding='utf-8-sig') as f:
    f.writelines(new_lines)
