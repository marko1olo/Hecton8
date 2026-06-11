import re
import sys

scene_path = r"C:\hades\Hecton8\Assets\_Project\Scenes\010_TEST.unity"

with open(scene_path, 'r', encoding='utf-8') as f:
    content = f.read()

# 1. MapMagicObject GameObject is 832834226. Let's make it active.
content = re.sub(r'(--- !u!1 &832834226\r?\nGameObject:.*?m_IsActive: )0', r'\g<1>1', content, flags=re.DOTALL)

# 2. MapMagicObject Transform is 832834227. Let's set its parent to 0 (root).
content = re.sub(r'(--- !u!4 &832834227\r?\nTransform:.*?m_Father: \{fileID: )1160546699\}', r'\g<1>0}', content, flags=re.DOTALL)

with open(scene_path, 'w', encoding='utf-8') as f:
    f.write(content)

print("Patched 010_TEST YAML")
