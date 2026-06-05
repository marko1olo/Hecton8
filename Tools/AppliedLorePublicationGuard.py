#!/usr/bin/env python3
import argparse
import fnmatch
import json
import re
import sys
from pathlib import Path

TARGET_LOCALES = (
    "en_US", "ru_RU", "ja_JP", "zh_CN", "fr_FR", "es_ES", "de_DE",
    "pl_PL", "uk_UA", "ar_SA", "id_ID", "ko_KR", "he_IL", "pt_BR", "nl_NL"
)

REQUIRED_KEYS = [
    "packet_id", "release_set_id", "article_id", "unlock_id",
    "localization_status", "localization_flags", "spoiler_tier",
    "source_voice", "title"
]

ANTI_AI_PHRASES = [
    "this entry explores",
    "serves as a reminder",
    "a testament to",
    "more than just",
    "at its core",
    "in a world where",
    "both beautiful and terrifying"
]

SPOILER_MARKERS = [
    "[SPOILER]",
    "<spoiler>",
    "spoiler_warning",
    "archive_spoilers"
]

READY_STATES = {"native_reviewed", "runtime_ready", "publication_ready"}

class Counters:
    def __init__(self):
        self.files = 0
        self.failures = 0
        self.warnings = 0
        self.locale_gaps = 0
        self.clone_failures = 0
        self.spoiler_failures = 0
        self.prose_failures = 0

def extract_frontmatter(content: str) -> tuple[dict, str]:
    content = content.lstrip()
    if not content.startswith("---"):
        return {}, content
    
    parts = content.split("---", 2)
    if len(parts) < 3:
        return {}, content
        
    frontmatter_text = parts[1]
    body = parts[2].strip()
    
    fm = {}
    for line in frontmatter_text.split("\n"):
        line = line.strip()
        if ":" in line:
            k, v = line.split(":", 1)
            fm[k.strip()] = v.strip().strip("'\"")
    return fm, body

def run_guard(root: Path, packet_glob: str, json_output: bool):
    counters = Counters()
    messages = []
    
    def log_fail(msg, counter_attr=None):
        if counter_attr:
            setattr(counters, counter_attr, getattr(counters, counter_attr) + 1)
        counters.failures += 1
        messages.append(f"FAIL: {msg}")
        
    def log_warn(msg):
        counters.warnings += 1
        messages.append(f"WARN: {msg}")

    wiki_path = root / "Docs" / "Lore" / "AppliedContent" / "in_game_wiki"
    site_path = root / "Docs" / "Lore" / "AppliedContent" / "external_site"
    
    files = []
    if wiki_path.exists():
        files.extend(list(wiki_path.rglob("*.md")))
    if site_path.exists():
        files.extend(list(site_path.rglob("*.md")))
        
    packets = {}
    
    for f in files:
        rel = f.relative_to(root)
        try:
            content = f.read_text(encoding="utf-8")
        except UnicodeDecodeError:
            log_fail(f"{rel}: Unicode decoding error")
            continue
            
        fm, body = extract_frontmatter(content)
        packet_id = fm.get("packet_id", "")
        
        if packet_glob and packet_id:
            matched = False
            for glob in packet_glob.split(','):
                if fnmatch.fnmatch(packet_id, glob.strip()):
                    matched = True
                    break
            if not matched:
                continue
            
        counters.files += 1
        
        missing_keys = [k for k in REQUIRED_KEYS if not fm.get(k)]
        if missing_keys:
            log_fail(f"{rel}: Missing or empty required keys: {missing_keys}")
            
        body_lower = body.lower()
        found_prose = [p for p in ANTI_AI_PHRASES if p in body_lower]
        if found_prose:
            log_fail(f"{rel}: Anti-AI prose detected: {found_prose}", "prose_failures")
            
        locale = f.parent.name
        status = fm.get("localization_status", "").lower()
        if locale != "en_US":
            if status in READY_STATES and not fm.get("proof_marker"):
                log_fail(f"{rel}: Non-English page claims ready status '{status}' without proof_marker")
        
        if "external_site" in str(rel):
            tier = fm.get("spoiler_tier", "0")
            if str(tier).isdigit() and int(tier) >= 3:
                has_marker = False
                for m in SPOILER_MARKERS:
                    if m in content:
                        has_marker = True
                        break
                if not has_marker:
                    log_fail(f"{rel}: external_site with spoiler tier {tier} missing spoiler marker", "spoiler_failures")
                    
        surface = "external_site" if "external_site" in str(rel) else "in_game_wiki"
        if packet_id:
            packets.setdefault(packet_id, {}).setdefault(locale, {})[surface] = (fm, body, rel)

    for packet_id, locales_map in packets.items():
        if "en_US" in locales_map:
            missing_locales = [l for l in TARGET_LOCALES if l not in locales_map]
            if missing_locales:
                log_fail(f"Packet {packet_id}: Missing TARGET_LOCALES: {missing_locales}", "locale_gaps")

    for packet_id, locales_map in packets.items():
        for locale, surfaces in locales_map.items():
            if "external_site" in surfaces and "in_game_wiki" in surfaces:
                site_fm, site_body, site_rel = surfaces["external_site"]
                wiki_fm, wiki_body, wiki_rel = surfaces["in_game_wiki"]
                
                if site_body and site_body == wiki_body:
                    is_draft = ("draft" in site_fm.get("localization_status", "").lower() or 
                                "draft" in wiki_fm.get("localization_status", "").lower())
                    if is_draft:
                        log_warn(f"Packet {packet_id} ({locale}): external_site and in_game_wiki bodies are exact clones (draft warning)")
                    else:
                        log_fail(f"Packet {packet_id} ({locale}): external_site and in_game_wiki bodies are exact clones in ready state", "clone_failures")

    if json_output:
        out = {
            "counts": vars(counters),
            "messages": messages
        }
        print(json.dumps(out, indent=2))
    else:
        for m in messages:
            print(m)
        print("\n--- Summary ---")
        for k, v in vars(counters).items():
            print(f"{k}: {v}")

    return 1 if counters.failures > 0 else 0

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", default=".", help="Repository root.")
    parser.add_argument("--packet-glob", default="", help="Glob to match packet_ids")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    
    sys.exit(run_guard(Path(args.root), args.packet_glob, args.json))

if __name__ == "__main__":
    main()
