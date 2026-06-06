import os
import json
import csv
from datetime import datetime

base_dir = r"C:\hades\Hecton8\Docs\Lore\AppliedContent"
loc_dir = r"C:\hades\Hecton8\Data\Localization"
os.makedirs(loc_dir, exist_ok=True)
in_json = os.path.join(base_dir, "runtime_evidence_graph.json")

out_loc_json = os.path.join(loc_dir, "en_US.json")
out_csv = os.path.join(loc_dir, "Deep_Lore_Loc_Matrix.csv")
out_dash = os.path.join(loc_dir, "LOCALIZATION_DASHBOARD.md")

print("Initializing Advanced Localization Exporter for Deep Lore...")

with open(in_json, "r", encoding="utf-8") as f:
    graph = json.load(f)

loc_db = {
    "schema": "HECTON8_LOC_V3_ADVANCED",
    "locale": "en_US",
    "last_updated": datetime.now().isoformat(),
    "strings": {}
}

csv_rows = [["StringID", "English Text", "LocContext (Translator Note)", "Character Limit", "Status"]]
warnings = []

for node in graph["nodes"]:
    nid = node["node_id"]
    
    # Title
    t_id = f"ns.lore.deep.{nid.lower()}.title"
    loc_db["strings"][t_id] = node["title"]
    status = "OK" if len(node["title"]) <= 50 else "WARN_OVERLENGTH"
    if status == "WARN_OVERLENGTH": warnings.append(f"{t_id}: Title exceeds 50 chars ({len(node['title'])})")
    csv_rows.append([t_id, node["title"], "Title of a recovered document. Keep it clinical and industrial.", "50", status])
    
    # Author
    a_id = f"ns.lore.deep.{nid.lower()}.author"
    loc_db["strings"][a_id] = node["author"]
    status = "OK" if len(node["author"]) <= 60 else "WARN_OVERLENGTH"
    if status == "WARN_OVERLENGTH": warnings.append(f"{a_id}: Author exceeds 60 chars ({len(node['author'])})")
    csv_rows.append([a_id, node["author"], "Name and title of the author. Do not translate names.", "60", status])
    
    # Text
    tx_id = f"ns.lore.deep.{nid.lower()}.text"
    loc_db["strings"][tx_id] = node["text"]
    
    # Add context based on tags
    context = "Industrial sci-fi tone. NASA-punk."
    tags = node.get("subject_tags", [])
    if "Audio Log" in tags:
        context += " Audio transcript. Keep sound tags like [STATIC] intact."
    if "Marauder" in tags:
        context += " Field note from a scavenger. Bitter, practical, cynical."
    if "Liability" in tags:
        context += " Corporate memo. Use dry, evasive legal/insurance language."
        
    status = "OK" if len(node["text"]) <= 1000 else "WARN_OVERLENGTH"
    if status == "WARN_OVERLENGTH": warnings.append(f"{tx_id}: Text exceeds 1000 chars ({len(node['text'])})")
    csv_rows.append([tx_id, node["text"], context, "1000", status])

# Write JSON
with open(out_loc_json, "w", encoding="utf-8") as f:
    json.dump(loc_db, f, indent=4)

# Write CSV
with open(out_csv, "w", newline='', encoding="utf-8") as f:
    writer = csv.writer(f)
    writer.writerows(csv_rows)

# Write Dashboard
with open(out_dash, "w", encoding="utf-8") as f:
    f.write("# HECTON-8 Localization Dashboard\n\n")
    f.write("> [!IMPORTANT]\n")
    f.write("> This dashboard tracks localization readiness for Deep Lore.\n\n")
    f.write(f"- **Total Strings:** {len(loc_db['strings'])}\n")
    f.write(f"- **Warnings:** {len(warnings)}\n\n")
    if warnings:
        f.write("## ⚠️ Validation Warnings\n")
        for w in warnings:
            f.write(f"- {w}\n")
    else:
        f.write("## ✅ All strings passed length validation.\n")

print(f"Exported {len(loc_db['strings'])} strings to {out_loc_json}")
print(f"Exported Translator Matrix to {out_csv}")
if warnings:
    print(f"Encountered {len(warnings)} length warnings! Check {out_dash} for details.")
