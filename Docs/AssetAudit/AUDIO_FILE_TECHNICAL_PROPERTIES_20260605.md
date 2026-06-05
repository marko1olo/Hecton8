# Audio File Technical Properties - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence class: `STATIC_SOURCE` + `AUDIO_PROBE`.
Scope: source-file technical inventory for audio files under `Assets/_Project/Audio`.

This file is not Unity import proof, runtime mix proof, Addressables proof, listening proof, DSP proof, GC proof, or final audio acceptance. It only records source file metadata from filesystem plus `ffprobe`, then cross-checks `Docs/Audio/audio_asset_ledger.csv`.

CSV companion: `Docs/AssetAudit/AUDIO_FILE_TECHNICAL_PROPERTIES_20260605.csv`.

## Summary

- Source audio files scanned: `138`.
- Ledger rows matched by path: `138`.
- Missing ledger rows: `0`.
- Probe failures: `0`.
- Extension counts: `flac`=1, `mp3`=3, `ogg`=89, `wav`=45.
- Ledger class counts: `ambient`=12, `music`=84, `player_loop`=5, `sfx`=30, `ui`=5, `voice`=2.
- Long source files over 10 seconds: `98`.
- Multichannel `sfx`/`ui`/`player_loop` source rows: `19`.
- `sfx`/`ui` rows above 22050 Hz source rate: `35`.
- `music`/`ambient` rows above 44100 Hz source rate: `11`.

## Use

Use this matrix before audio import, Addressables, MusicDirector, Player prefab direct-ref, or listening-pass work. It tells the next owner which source files have technical risk before Unity import readback.

## Rejection Boundary

- Do not treat source codec/sample rate/channel count as Unity import settings.
- Do not treat source duration as runtime playback behavior.
- Do not treat matched ledger rows as ownership or Addressables readiness.
- Do not claim listening quality from this matrix.
- Do not claim `0 B/frame`; no runtime path was measured.

## Regression Model

- CPU: static probe only; no runtime CPU change.
- GC: no runtime code changed; no allocation proof.
- Memory: source byte size only; no imported size, resident memory, or audio bank proof.
- Cadence: no runtime cadence changed.
- Correctness: ledger/source mismatch risk is mapped because every current source path has a technical row; acceptance remains blocked by Unity import, listening, lifecycle, and runtime proof.

Final status: `PENDING VERIFICATION`.
