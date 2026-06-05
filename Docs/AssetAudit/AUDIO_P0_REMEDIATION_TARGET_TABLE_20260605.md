# Audio P0 Remediation Target Table - 2026-06-05

Status: `PENDING_VERIFICATION`.
Evidence class: `STATIC_DOC`, `STATIC_SOURCE`, `AUDIO_SOURCE_PROBE`.
CSV: `Docs/AssetAudit/AUDIO_P0_REMEDIATION_TARGET_TABLE_20260605.csv`.

No Unity run, no listening pass, no build, no import edit, no prefab edit, no mixer edit, no scene edit, no Addressables operation, no runtime proof, and no `Assets/` mutation were performed.

## Required Reads Completed

- `AGENTS.md`
- `audio.md`
- `Docs/AssetAudit/ASSET_SYSTEM_INDEX_20260605.md`
- `Docs/Audio/audio_remediation_matrix_20260605.csv`
- `Docs/AssetAudit/AUDIO_REMEDIATION_MATRIX_REVIEW_20260605.md`
- `Docs/AssetAudit/AUDIO_LOUDNESS_TECHNICAL_PROPERTIES_20260605.csv`
- `Docs/AssetAudit/AUDIO_DIRECT_REF_DETAIL_20260605.csv`
- `taskslocal/asset_system_20260605/ASSET_OWNER_28_AUDIO_REMEDIATION_EXECUTION_PACKET.md`

## Mandates Followed

- `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC`: static rows cannot prove DSPGraph/native route safety, underrun safety, managed callback safety, or 0 B/frame behavior.
- `AUD_Acoustic_Sonar_Occlusion_Sensory_Simulation`: future audio proof must preserve route/threat/warning information and avoid decorative, omniscient, or masking sound.
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory`: direct clip refs are not load/release/ref-count/Addressables proof; admitted long beds need active-bank and memory/residency evidence.

## Exact P0 Facts

The source remediation matrix and owner packet report `6` P0 rows.

| P0 | Target | Static fact | Current static state | Required owner |
|---|---|---|---|---|
| 1 | `Assets/_Project/Data/Audio/Music/Configs/MusicDirectorConfig_Global.asset` | `_stingerMixerGroup` is null. | MusicDirector config route blocked. | Audio/MusicDirector owner |
| 2 | `Assets/_Project/Data/Audio/Music/Configs/MusicDirectorConfig_Global.asset` | `_musicMixerGroup` is null. | MusicDirector config route blocked. | Audio/MusicDirector owner |
| 3 | `Assets/_Project/Audio/Movement/dive_splash.wav` | Direct `Player.prefab` `AudioClip` ref at line `1067`, field context `waterExitSplashClip`. | `CompressedInMemory`, `ADPCM`, `1.729s`; source probe only. | Player/audio lifecycle owner |
| 4 | `Assets/_Project/Audio/Movement/dive_splash.wav` | Direct `Player.prefab` `AudioClip` ref at line `1066`, field context `waterEntrySplashClip`. | `CompressedInMemory`, `ADPCM`, `1.729s`; source probe only. | Player/audio lifecycle owner |
| 5 | `Assets/_Project/Audio/Underwater Ambient.wav` | Direct `Player.prefab` `AudioClip` ref at line `137`, field context `m_Resource`. | `Streaming`, `Vorbis`, quality `0.45`, `193s`; long-bed loudness deferred. | Player/audio lifecycle owner |
| 6 | `Assets/_Project/Audio/Underwater Ambient.wav` | Direct `Player.prefab` `AudioClip` ref at line `239`, field context `_driverClip`. | `Streaming`, `Vorbis`, quality `0.45`, `193s`; long-bed loudness deferred. | Player/audio lifecycle owner |

Static direct prefab refs do not prove runtime failure. They block readiness because owner route, load/release path, Addressables status, playback path, mixer route, listening result, memory residency, and `0 B/frame` proof are absent.

## Required Future Proof

- Unity-read `MusicDirectorConfig_Global.asset`; capture `_musicMixerGroup` and `_stingerMixerGroup` state.
- Close the MusicDirector route through approved mixer groups or a documented owned DSP/native bypass.
- Runtime-capture MusicDirector profile entry/exit, crossfade, music/stinger path, warning ducking, silence windows, Console state, and managed callback safety.
- Unity-read `Player.prefab`; map all four P0 direct refs to owning components and serialized fields. Do not trust line numbers without readback.
- Unity import-read P0 clips: load type, compression, quality, sample rate, force mono, preload/background flags, loop flags, platform overrides, and source/import channel behavior.
- Classify each retained direct ref by owner, cue id/hash, load phase, release/shutdown phase, playback route, priority/ducking, fallback, Addressables group/key or fixed-startup exception, and hot-path allocation proof.
- Prove playback with runtime capture, listening notes in first-exit/shallow/warning-overlap contexts, Profiler/GCMonitor `0 B/frame`, and Memory Profiler or equivalent residency proof for retained long beds.

## What Not To Claim

- Do not claim Unity import correctness from CSV rows.
- Do not claim mixer routing from static MusicDirector profile/config refs.
- Do not claim runtime readiness from direct `Player.prefab` serialized refs.
- Do not claim Addressables ownership, release safety, or ref-count correctness from prefab serialization.
- Do not claim source loudness, waveform probes, or long-bed deferral as listening or mix acceptance.
- Do not claim `0 B/frame`, DSP/native safety, or managed callback safety without runtime profiler/audio evidence.
- Do not use MasterAudio strings, string cue routes, generic streaming SFX, or hot-path `AudioSource.PlayOneShot` acceptance as shortcuts.
- Do not mutate prefab/mixer/import YAML by text unless a future owner proves Unity API mutation is impossible and validates FileID/GUID/property alignment afterward.

## Continuous Quality Consequences

- Low/compact: keep breath, warning, UI/instrument feedback, route, sonar/threat, and water-entry/exit readability before decorative beds; admit one long ambience/music context only with memory proof.
- Middle: current plus likely-next context may be admitted only after owner route, ducking, and memory proof.
- High: spend headroom on cleaner MusicDirector transitions, stronger stinger discipline, richer hydrophone/reverb detail, and better silence-window control.
- Ultra: dense secondary beds and richer convolution/spatial detail are allowed only after critical cue readability, owner route, and lifecycle proof remain intact.

`GlobalQualityWeight` must not change cue truth, cue IDs, owner route, Addressables keys, release order, source-fact authority, DTO layout, save identity, or warning facts.

## Regression Model

- CPU: no runtime CPU changed. Future risk is decode, streaming dispatch, MusicDirector transition work, mixer/DSP load, and audio-thread contention.
- GC: no runtime GC changed. Future risk is string cue lookup, dynamic collections, logging, managed callbacks, UI text churn, and unpooled playback routes.
- Memory: no import/residency changed. Future risk is long-bed active banks, duplicate direct refs, streaming buffers, stereo short cues, broad prefetch, and missing release.
- Cadence: no runtime cadence changed. Future risk is constant ambience/music, repeated stingers, missing silence windows, warning masking, and player-loop start jitter.
- Correctness: P0 rows are target-mapped only. Unity readback, runtime playback, listening, profiler, memory, and route acceptance remain unproved.

Final disposition: `PENDING_VERIFICATION`.
