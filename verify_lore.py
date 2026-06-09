import os
import re

base_dir = r"Docs/Lore/AppliedContent"
rs_dir = os.path.join(base_dir, "release_sets")
ext_dir = os.path.join(base_dir, "external_site/_draft_backlog")
qa_report = os.path.join(base_dir, "LORE_QA_REPORT_20260606.md")

banned_phrases = [
    "delve", "tapestry", "foster", "testament to", "rich tapestry", "bustling", "vibrant",
    "journey", "explore", "realm", "it is important to note", "in conclusion", "furthermore",
    "moreover", "as an ai", "i cannot", "seamless", "unlock", "empower", "unprecedented",
    "lorem ipsum"
]

def check_file(filepath):
    with open(filepath, "r", encoding="utf-8-sig") as f:
        content = f.read()

    issues = []
    
    # Check for banned phrases
    lower_content = content.lower()
    for phrase in banned_phrases:
        if phrase in lower_content:
            issues.append(f"Contains banned phrase: '{phrase}'")
            
    # Check word count of the file
    words = content.split()
    if len(words) < 200:
        issues.append(f"File too short: {len(words)} words.")
        
    # Check if packet contains expected sections
    if filepath.endswith("FACTORY_20260606.md"):
        if "### In-game codex entry" not in content:
            issues.append("Missing In-game codex entry")
        if "### Scanner short" not in content:
            issues.append("Missing Scanner short")
        if "### Terminal/memo/document surface" not in content:
            issues.append("Missing Terminal surface")
            
    return issues

print("Running deep QA audit on generated Lore...")

all_issues = {}

for filename in os.listdir(rs_dir):
    if filename.endswith(".md"):
        filepath = os.path.join(rs_dir, filename)
        issues = check_file(filepath)
        if issues:
            all_issues[filename] = issues

for filename in os.listdir(ext_dir):
    if filename.endswith(".md"):
        filepath = os.path.join(ext_dir, filename)
        issues = check_file(filepath)
        if issues:
            all_issues[filename] = issues

with open(qa_report, "w", encoding="utf-8") as f:
    f.write("# HECTON-8 Lore QA Audit Report\n\n")
    f.write("Date: 2026-06-06\n")
    f.write("Scope: RS113-RS124\n\n")
    
    if not all_issues:
        f.write("## Status: PASS\n")
        f.write("All generated lore packets meet word count, structural integrity, and pass the negative constraints (no AI-banned prose detected).\n")
    else:
        f.write("## Status: FAIL / WARNINGS\n")
        for fname, issues in all_issues.items():
            f.write(f"### {fname}\n")
            for issue in issues:
                f.write(f"- {issue}\n")

print(f"QA Audit complete. Report saved to {qa_report}")
