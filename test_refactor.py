import re
with open('./Assets/_Project/Scripts/SaveManager.cs', 'r') as f:
    content = f.read()
match = re.search(r'        private static bool TryAuditSaveSlotInternal\(string slotName, out SaveSlotAuditResult result\)\r?\n        \{.*?\n        \}', content, re.DOTALL)
if match:
    print(f"Found Match length: {len(match.group(0))}")
else:
    print("Match not found.")
