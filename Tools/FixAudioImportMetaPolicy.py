import os
import sys
from pathlib import Path
import csv
import re

ROOT = Path(__file__).resolve().parents[1]
LEDGER_PATH = ROOT / "Docs/Audio/audio_asset_ledger.csv"
TECHNICAL_PATH = ROOT / "Docs/AssetAudit/AUDIO_FILE_TECHNICAL_PROPERTIES_20260605.csv"

def get_audio_info():
    info = {}
    if LEDGER_PATH.exists():
        with open(LEDGER_PATH, 'r', encoding='utf-8') as f:
            reader = csv.DictReader(f)
            for row in reader:
                path = row.get("Path") or row.get("path")
                if path:
                    info[path] = row

    if TECHNICAL_PATH.exists():
        with open(TECHNICAL_PATH, 'r', encoding='utf-8') as f:
            reader = csv.DictReader(f)
            for row in reader:
                path = row.get("Path") or row.get("path")
                if path and path in info:
                    info[path].update(row)
                elif path:
                    info[path] = row
    return info

def parse_and_fix_meta(filepath, audio_info=None):
    content = filepath.read_text(encoding='utf-8', errors='ignore')
    fixed = False

    # Defaults
    duration = 0.0
    audio_class = ""

    try:
        rel_path = filepath.relative_to(ROOT).as_posix()
    except ValueError:
        rel_path = filepath.name
    rel_path_no_meta = rel_path[:-5] # remove .meta

    if audio_info and rel_path_no_meta in audio_info:
        info = audio_info[rel_path_no_meta]
        # try to parse duration
        d = info.get("DurationSec") or info.get("duration_sec") or info.get("Duration")
        if d:
            try:
                duration = float(d)
            except ValueError:
                pass

        audio_class = (info.get("AudioClass") or info.get("audio_class") or "").lower()

    lines = content.split('\n')
    out_lines = []

    for line in lines:
        # Force to Mono for 3D sounds (we assume 3D sound if it's 'sfx' or 'player_loop')
        if 'forceToMono:' in line and audio_class in {"sfx", "player_loop"}:
            if 'forceToMono: 0' in line:
                line = line.replace('forceToMono: 0', 'forceToMono: 1')
                fixed = True

        # Audio compression format:
        # 0 = PCM, 1 = Vorbis, 2 = ADPCM
        if 'compressionFormat:' in line:
            # Vorbis Q70 for clips >2s, ADPCM for short SFX
            if duration > 2.0 or audio_class not in {"sfx", "ui"}:
                if not 'compressionFormat: 1' in line:
                    line = re.sub(r'compressionFormat:\s*\d+', 'compressionFormat: 1', line)
                    fixed = True
            elif duration > 0.0 and duration <= 2.0 and audio_class in {"sfx"}:
                if not 'compressionFormat: 2' in line:
                    line = re.sub(r'compressionFormat:\s*\d+', 'compressionFormat: 2', line)
                    fixed = True

        # Quality for Vorbis (Q70 = 0.7)
        if 'quality:' in line:
            # check if compression is Vorbis (1) (would need context normally but applying 0.7 is safe for Vorbis)
            if 'quality: 1' in line or 'quality: 0.1' in line or 'quality: 0.5' in line: # if not 0.7
                if duration > 2.0 or audio_class not in {"sfx", "ui"}:
                    line = re.sub(r'quality:\s*[\d.]+', 'quality: 0.7', line)
                    fixed = True

        out_lines.append(line)

    if fixed:
        filepath.write_text('\n'.join(out_lines), encoding='utf-8')
        return True
    return False

def main():
    assets_dir = ROOT / "Assets"
    if not assets_dir.exists():
        assets_dir = Path(".")

    meta_files = list(assets_dir.rglob("*.wav.meta")) + list(assets_dir.rglob("*.mp3.meta"))
    fixes = 0

    audio_info = get_audio_info()

    for f in meta_files:
        if parse_and_fix_meta(f, audio_info):
            fixes += 1
            print(f"Fixed: {f}")

    print(f"Total files fixed: {fixes}")

if __name__ == '__main__':
    main()
