import os
import re

def fix_file(filepath):
    with open(filepath, 'rb') as f:
        content = f.read()

    # Convert all line endings to \r\n
    content = content.replace(b'\r\n', b'\n').replace(b'\r', b'\n').replace(b'\n', b'\r\n')
    
    # Remove non-breaking spaces (C2 A0) just in case
    content = content.replace(b'\xc2\xa0', b' ')

    # Convert to string to do regex replacements for [Tooltip(...)]
    try:
        text = content.decode('utf-8')
        # Remove [Tooltip("...")] or [Tooltip(...)]
        text = re.sub(r'\[\s*Tooltip\s*\([^\]]*\)\s*\]', '', text)
        content = text.encode('utf-8')
    except Exception as e:
        print(f"Skipping Tooltip regex for {filepath} due to decoding error: {e}")

    with open(filepath, 'wb') as f:
        f.write(content)

for root, dirs, files in os.walk('Assets'):
    for file in files:
        if file.endswith('.shader'):
            fix_file(os.path.join(root, file))

print('Done fixing shaders.')
