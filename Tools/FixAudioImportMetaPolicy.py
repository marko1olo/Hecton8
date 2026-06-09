#!/usr/bin/env python3
"""Fix static AudioImporter .meta policy against the audio ledger."""

from __future__ import annotations

import re
import sys
from pathlib import Path

TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import ValidateAudioImportMetaPolicy as validator


def fix_meta_file(meta_path: Path, row: validator.LedgerRow) -> bool:
    if not meta_path.exists():
        print(f"WARN: Missing meta file: {meta_path}")
        return False

    text = meta_path.read_text(encoding="utf-8", errors="ignore")
    if "AudioImporter:" not in text:
        print(f"WARN: Meta file is not AudioImporter: {meta_path}")
        return False

    # Parse current settings using validator helper
    meta = validator.parse_audio_meta(meta_path)
    modified = False

    # 1. loadType: Update if mismatched (DecompressOnLoad = 0, CompressedInMemory = 1, Streaming = 2)
    load_type_val = {"DecompressOnLoad": "0", "CompressedInMemory": "1", "Streaming": "2"}[row.load_type]
    if meta.load_type != row.load_type:
        text = re.sub(r"^(\s*loadType:\s*)\d+", rf"\g<1>{load_type_val}", text, flags=re.MULTILINE)
        modified = True

    # 2. compressionFormat: Update if mismatched (PCM = 0, Vorbis = 1, ADPCM = 2)
    compression_val = {"PCM": "0", "Vorbis": "1", "ADPCM": "2"}[row.compression]
    if meta.compression != row.compression:
        text = re.sub(r"^(\s*compressionFormat:\s*)\d+", rf"\g<1>{compression_val}", text, flags=re.MULTILINE)
        modified = True

    # 3. quality: Update to match the exact value in the ledger
    if abs(meta.quality - row.quality) > validator.QUALITY_EPSILON:
        # Use formatting that matches ledger (e.g. 0.45 or 0.7)
        quality_str = str(row.quality)
        text = re.sub(r"^(\s*quality:\s*)[0-9.]+", rf"\g<1>{quality_str}", text, flags=re.MULTILINE)
        modified = True

    # 4. forceToMono: Set to 1 if row.audio_class is in FORCE_MONO_CLASSES and it is currently 0
    if row.audio_class in validator.FORCE_MONO_CLASSES and not meta.force_to_mono:
        text = re.sub(r"^(\s*forceToMono:\s*)0", r"\g<1>1", text, flags=re.MULTILINE)
        modified = True

    # 5. preloadAudioData and loadInBackground (Preload Policy):
    # If loadType is DecompressOnLoad (0), set preloadAudioData = 1 and loadInBackground = 0.
    # If loadType is CompressedInMemory (1) or Streaming (2), set preloadAudioData = 0 and loadInBackground = 1.
    target_preload = "1" if row.load_type == "DecompressOnLoad" else "0"
    target_background = "0" if row.load_type == "DecompressOnLoad" else "1"

    current_preload = "1" if meta.preload_audio_data else "0"
    current_background = "1" if meta.load_in_background else "0"

    if current_preload != target_preload:
        text = re.sub(r"^(\s*preloadAudioData:\s*)\d+", rf"\g<1>{target_preload}", text, flags=re.MULTILINE)
        modified = True

    if current_background != target_background:
        text = re.sub(r"^(\s*loadInBackground:\s*)\d+", rf"\g<1>{target_background}", text, flags=re.MULTILINE)
        modified = True

    # 6. sampleRateSetting & sampleRateOverride (Sample Rate Policy):
    # If sampleRateSetting is set to 2 (Override):
    # If row.audio_class is in MUSIC_AMBIENT_CLASSES (music, ambient), limit/set sampleRateOverride to 44100.
    # If row.audio_class is in FORCE_MONO_CLASSES or is ui, limit/set sampleRateOverride to 22050.
    if meta.sample_rate_setting == 2:
        target_sample_rate = None
        if row.audio_class in validator.MUSIC_AMBIENT_CLASSES:
            if meta.sample_rate_override > 44100:
                target_sample_rate = 44100
        elif row.audio_class in validator.FORCE_MONO_CLASSES or row.audio_class == "ui":
            if meta.sample_rate_override > 22050:
                target_sample_rate = 22050

        if target_sample_rate is not None:
            text = re.sub(r"^(\s*sampleRateOverride:\s*)\d+", rf"\g<1>{target_sample_rate}", text, flags=re.MULTILINE)
            modified = True

    if modified:
        meta_path.write_text(text, encoding="utf-8")
        return True

    return False


def main() -> int:
    ledger = validator.load_ledger_rows()
    fixed_count = 0
    for row in ledger:
        meta_path = validator.ROOT / f"{row.path}.meta"
        if fix_meta_file(meta_path, row):
            fixed_count += 1
            print(f"Fixed: {row.path}.meta")

    print(f"Total files fixed: {fixed_count}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
