import re

with open('Assets/_Project/Scripts/FaunaDirector.cs', 'r') as f:
    content = f.read()

matches = re.finditer(r'(?si)(trail.*?coord.*?strength)', content)
for m in matches:
    print(m.group(1))
