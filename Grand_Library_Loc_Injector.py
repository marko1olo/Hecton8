import os
import json

GRAND_LIBRARY_DIR = r"C:\hades\Hecton8\Docs\Lore\Grand_Library"
LOC_DIR = r"C:\hades\Hecton8\Data\Localization"

def inject_library():
    print("Starting Grand Library Localization Injection...")
    
    # Dynamically detect all languages from authored markdown filenames (e.g. *_ru_RU.md)
    langs = set()
    if os.path.exists(GRAND_LIBRARY_DIR):
        for filename in os.listdir(GRAND_LIBRARY_DIR):
            if filename.endswith(".md") and "_" in filename:
                parts = filename.replace(".md", "").split("_")
                if len(parts) >= 2:
                    lang = "_".join(parts[-2:])
                    if len(lang) == 5 and lang[2] == "_":
                        langs.add(lang)
    langs = sorted(list(langs))
    
    for lang in langs:
        loc_file = os.path.join(LOC_DIR, f"{lang}.json")
        if not os.path.exists(loc_file):
            print(f"Skipping {lang}, loc file not found.")
            continue
            
        with open(loc_file, "r", encoding="utf-8-sig") as f:
            loc_data = json.load(f)
            
        # Ensure strings dictionary exists
        if "strings" not in loc_data:
            loc_data["strings"] = {}
            
        # Parse all markdown files for this language
        for filename in os.listdir(GRAND_LIBRARY_DIR):
            if filename.endswith(f"{lang}.md"):
                file_path = os.path.join(GRAND_LIBRARY_DIR, filename)
                with open(file_path, "r", encoding="utf-8-sig") as md_file:
                    content = md_file.read()
                    
                # Create a key based on the filename, e.g., 01_ASTRONOMY_AND_HISTORY -> ns.lore.grand_library.astronomy
                base_name = filename.replace(f"_{lang}.md", "").lower()
                key = f"ns.lore.grand_library.{base_name}"
                
                loc_data["strings"][key] = content
                print(f"Injected {key} into {lang}.json (Length: {len(content)} chars)")
                
        # Save back to loc file
        with open(loc_file, "w", encoding="utf-8") as f:
            json.dump(loc_data, f, indent=4, ensure_ascii=False)
            
    print("Injection complete. In-game Codex updated.")

if __name__ == "__main__":
    inject_library()
