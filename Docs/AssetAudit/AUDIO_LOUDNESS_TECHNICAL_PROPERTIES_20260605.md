# Audio Loudness Technical Properties - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence class: `AUDIO_SOURCE_PROBE`.
Scope: source audio rows under `Assets/_Project/Audio` from `AUDIO_FILE_TECHNICAL_PROPERTIES_20260605.csv`.

This file is not Unity import proof, DSPGraph proof, runtime mix proof, listening acceptance, Memory Profiler proof, or audio-thread safety proof. Source decode probes only show source-file signal facts for rows where the decode pass completed.

CSV companion: `Docs/AssetAudit/AUDIO_LOUDNESS_TECHNICAL_PROPERTIES_20260605.csv`.

## Method

- Tool: `C:/Users/danat/AppData/Local/Programs/Python/Python313/Scripts/ffmpeg.exe` with `volumedetect` for short/critical rows.
- Long `music` and `ambient` rows over 30 seconds are marked `NOT_MEASURED_LONG_BED_DEFERRED_TO_LISTENING_OWNER` to avoid pretending that source loudness equals route mix quality.
- Input matrix: `Docs/AssetAudit/AUDIO_FILE_TECHNICAL_PROPERTIES_20260605.csv`.

## Summary

- Audio rows covered: `138`.
- Measurement statuses: `LONG_BED_DEFERRED=93, MEASURED_FFMPEG_VOLUMEDETECT=45`.
- Near-0dB source peak flags: `7`.
- Very quiet source mean flags: `11`.
- Loud source mean flags: `0`.
- Long source context rows: `98`.
- Multichannel critical cue context rows: `19`.

## Required Future Gates

- Unity import readback for load type, compression, quality, force mono, sample rate, preload/background load, and platform overrides.
- Listening pass for breath, oxygen, pressure, warning, sonar, UI, tool, ambience, music, and silence windows.
- DSP route proof: no locks, waits, managed callbacks, dynamic allocations, or game-world queries on audio thread.
- Addressables lifecycle proof: owner, key/group, ref-count, release phase, active bank budget, pressure behavior.

## Rollback Conditions

- Import or route changes mask warnings, breath, oxygen, pressure, tool, sonar, threat, or UI cues.
- Long beds are admitted broadly without active-bank memory and listening proof.
- Source loudness probe is treated as route mix acceptance.
- Direct prefab refs or MusicDirector null output routes remain unresolved after attempted adoption.

## Continuous GlobalQualityWeight Consequences

- Low/compact: critical cues outrank decorative beds; secondary layers, prefetch breadth, and diagnostics shrink smoothly.
- Middle: allow current context plus likely-next context after memory and ducking proof.
- High: add wider transition prefetch, richer hydrophone detail, and stronger stinger variety after warning-priority proof.
- Ultra: add dense secondary beds and richer spatial/reverb detail only after critical cue readability remains proven.

## Regression Model

- CPU: static/source decode probe only; future risk is decode, streaming dispatch, crossfade, stinger scheduling, and DSP contention.
- GC: no runtime code touched; future risk is string cue lookup, managed callbacks, dynamic collections, and logging/UI side effects.
- Memory: no import/residency/bank state changed; long beds and stereo/high-rate clips remain source risks.
- Cadence: no runtime cadence changed.
- Correctness: source loudness is routing input only; import, mix, listening, DSP, and lifecycle remain `PENDING VERIFICATION`.

Final status: `PENDING VERIFICATION`.
