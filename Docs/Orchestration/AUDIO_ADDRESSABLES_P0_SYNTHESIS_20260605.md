# Audio Addressables P0 Synthesis - 2026-06-05

Status: `STATIC_SYNTHESIS / PENDING UNITY PROOF`
Evidence class: `SUBAGENT_STATIC_AUDIT + STATIC_DOC + STATIC_SOURCE + STATIC_ASSET_YAML`

No Unity, dotnet build, import, Play Mode, profiler, asset mutation, prefab mutation, mixer mutation, Addressables settings mutation, or raw YAML edit was performed by this synthesis.

## Verdict

Audio and Addressables are not production-ready. Current tables prove blocker maps, not runtime route acceptance.

## P0 Blockers

1. `Assets/_Project/Data/Audio/Music/Configs/MusicDirectorConfig_Global.asset` has null mixer refs:
   - `_musicMixerGroup: {fileID: 0}`
   - `_stingerMixerGroup: {fileID: 0}`
2. `Assets/_Project/Prefabs/Audio/PFB_HectonMusicDirectorRoot.prefab` has null `OutputAudioMixerGroup` on:
   - `MusicVoice_0`
   - `MusicVoice_1`
   - `MusicStinger`
3. `Assets/_Project/Prefabs/Player.prefab` has P0 direct refs to `Underwater Ambient.wav`:
   - AudioSource `m_Resource` around line 137
   - `_driverClip` around line 239
4. `_driverClip` belongs to `DynamicMusicGranularSynthesizer`, and `Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs` uses managed `OnAudioFilterRead(float[] data, int channels)` around line 775. Release acceptance requires an explicit exclusion, waiver, DSP/native bridge proof, no-GC proof, and underrun proof.
5. `Assets/_Project/Prefabs/Player.prefab` has P0 direct refs to `dive_splash.wav` around lines 1066-1067. Source queues them through fixed presentation audio events in `Assets/_Project/Scripts/HectonPlayerMovement.cs` around line 11069, but there is no Addressables/load/release proof.
6. `Docs/AssetAudit/AUDIO_DIRECT_REF_DETAIL_20260605.csv` reports 28 direct `Player.prefab` clip refs: 4 P0 plus 24 P1 footstep/UI refs.
7. Addressables package exists (`com.unity.addressables` `2.9.1`), but `Assets/AddressableAssetsData` has `RecursiveItemCount=0` and `NonMetaFileCount=0`. There are no settings, groups, profiles, schemas, labels, entries, catalog, or release ledger.
8. `Assets/_Project/Audio/Underwater Ambient.wav.meta` conflicts with audit docs. Live meta serializes `loadType: 1`, `compressionFormat: 1`, `quality: 0.7`, `sampleRateOverride: 22050`; docs describe Streaming/Vorbis/Q0.45/193s. Unity import readback is mandatory before policy decisions.

## Required Future Readback

Through Unity only:

- mixer groups;
- runtime director prefab;
- MusicVoicePool sources;
- all `AudioSource.outputAudioMixerGroup`;
- Player prefab direct refs and owning component names;
- clip import `loadType`, compression, quality, sample rate, force mono, preload, background load, platform overrides, duration/channels/imported size;
- Addressables settings, groups, schemas, labels, entries, load mode, owner key, ref count, release ledger, and active handle counts.

## Acceptance

- MusicDirector routes are non-null or explicitly bypassed by an owner-approved native/DSP route with runtime proof.
- Player direct refs each have owner, cue id/hash, Addressables key/group or fixed-lifetime exception, load phase, release/shutdown phase, playback route, fallback, mix priority, and `0 B/frame` proof.
- Addressables are accepted only after settings/groups/entries exist on disk and Unity/player proof shows load/release, memory, and compact pressure behavior.
- Runtime listening, memory, GC, mixer output, import, and Addressables readiness remain `PENDING VERIFICATION` until fresh proof exists.

## Low / Middle / High / Ultra

- Low: breath, warnings, UI, splash, and one owned ambience/music context max; no masking critical cues with long beds.
- Middle: controlled profile breadth only after lifecycle and ducking proof.
- High: richer transitions, stingers, reverb, and layers only through the same owner/ref-count/release truth.
- Ultra: wider density is allowed only after final warnings remain readable and no lifetime route regresses.

Final status: `P0 BLOCKED / STATIC ONLY`.
