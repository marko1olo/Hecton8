# Audio Remediation Matrix Review - 2026-06-05

Status: `PENDING_VERIFICATION`.
Evidence class: `STATIC_SOURCE` / `STATIC_DOC` / `AUDIO_WAVEFORM_QA`.

CSV source: `Docs/Audio/audio_remediation_matrix_20260605.csv`.

No Unity run, no audio import edit, no prefab edit, no build, no listening pass.

## Matrix Summary

- Total remediation rows: `58`.
- P0 rows: `6`.
- P1 rows: `44`.
- P2 rows: `8`.

Categories:

- `music_mixer_config`: 2 rows.
- `player_or_world_loop`: 4 rows.
- `short_sfx`: 20 rows.
- `ui_feedback`: 4 rows.
- `long_music_bed`: 7 rows.
- `repeated_stinger`: 11 rows.
- `import_or_placeholder_risk`: 10 rows.

## P0

1. `MusicDirectorConfig_Global.asset` has null `_musicMixerGroup`.
2. `MusicDirectorConfig_Global.asset` has null `_stingerMixerGroup`.
3. `Player.prefab` direct player/world-loop refs need owner/load/release/playback route:
   - `Underwater Ambient.wav`
   - movement/player loop refs flagged by the matrix.

Static direct prefab refs do not prove runtime failure. They block readiness because owner/release route, Addressables status, and zero-GC playback proof are absent.

## P1

- Long music beds need listening/runtime cadence proof, not just profile references.
- Repeated stingers must be either intentional reuse or profile-specialized.
- Short SFX direct refs need import/readback and playback-route proof.
- UI feedback refs need final mix/audibility proof.
- Streaming SFX/player-layer risks must be reclassified or rerouted.

## P2

- Ambient Q45 rows remain import-policy debt until the long-clip streaming vs compressed-in-memory authority conflict is resolved.

## Required Owner Order

1. Resolve/document MusicDirector mixer route.
2. Classify `Player.prefab` direct refs by owner and lifecycle.
3. Fill `Docs/Audio/audio_asset_ledger.csv` owner, Addressable group/key, and placeholder fields.
4. Perform listening pass for first-exit/shallow shelf, tension, silence windows, and stingers.
5. Only then perform Unity import edits through Unity API after process gate clears.

## Hard Rejections

- No generic streaming SFX.
- No runtime readiness from static profile rows.
- No constant music bed claim without MusicDirector runtime proof.
- No `AudioSource.PlayOneShot` hot-path acceptance without architecture proof.
- No Addressables readiness from prefab serialization.

Final status: `PENDING_VERIFICATION`.

