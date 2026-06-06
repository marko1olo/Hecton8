# Audio Addressables P0 Synthesis - 2026-06-05

Status: `STATIC_SYNTHESIS / PENDING UNITY PROOF`
Evidence class: `SUBAGENT_STATIC_AUDIT + STATIC_DOC + STATIC_SOURCE + STATIC_ASSET_YAML`

No Unity, dotnet build, import, Play Mode, profiler, asset mutation, prefab mutation, mixer mutation, Addressables settings mutation, or raw YAML edit was performed by this synthesis.

## Verdict

Audio and Addressables are not production-ready. Current tables prove blocker maps, not runtime route acceptance.

## 2026-06-06 Static Recheck And Validator

Evidence class: `SUBAGENT_STATIC_SOURCE_YAML_RECHECK + STATIC_VALIDATOR`. No Unity, dotnet, import, Play Mode, profiler, screenshots, or asset mutation.

Refinement:

- `02_HECTON_WORLD` currently has exactly one static scene-local `[MUSIC_SYSTEM]` anchor with `HectonMusicDirectorAnchor` bound to `MusicDirectorConfig_Global.asset` GUID `3fe2e07be4fdac24cb6b2f12b438dcc3`. This is static YAML evidence only; the route is not accepted until Unity readback and runtime audio proof exist.
- `MusicDirectorConfig_Global` now has statically non-null `_musicMixerGroup` and `_stingerMixerGroup` refs. This is still not runtime proof; Unity readback and Play Mode mixer/listening proof are required.
- Current direct `Player.prefab` `AudioClip` refs are `24` P1 refs: `20` footstep rows and `4` UI rows. Static scan reports `0` direct `Underwater Ambient.wav` refs and `0` direct `dive_splash.wav` refs. This is source/YAML proof only; Unity prefab readback is still required.
- `Assets/AddressableAssetsData` has no active settings/groups/entries in this checkout. `AudioResidencyCache` can manage decoded clip data, but that is not Addressables handle ownership.
- `Tools/ValidateAudioSceneStaticRoute.py --no-fail` now reports current repo as `AUDIO_SCENE_STATIC_ROUTE_REJECTED blockers=1`: Addressables route absent; scene anchor is statically present but still pending Unity/runtime proof. Mixer nulls are fallback-required notes, and current player direct refs are P1 owner/lifecycle blockers, not current P0 ambient/splash blockers. Tests: `python -B -m unittest Tools.test_validate_audio_scene_static_route` ran 5 tests OK.
- `Tools/ValidateAudioAddressablesP0Synthesis.py` now guards this synthesis against stale blocker counts and stale direct-ref P0 claims. Latest static result: `AUDIO_ADDRESSABLES_P0_SYNTHESIS_OK blockers=1 direct_refs=24 p0=0 footsteps=20 ui=4 fallback_required=1`. Tests: `python -m unittest Tools/test_validate_audio_addressables_p0_synthesis.py` ran 5 tests OK.

## 2026-06-06 Static Cue Source Correction

Evidence class: `STATIC_SOURCE + STATIC_LEDGER`. No Unity, import, reimport, mixer, Addressables, prefab, or meta mutation.

- `AUDCUE-08` had a stale candidate path `Assets/_Project/Audio/Movement/swimming underwater.mp3`.
- Current source ledger and file probe show the actual swim loop candidate is `Assets/_Project/Audio/Movement/swimming - underwater.ogg`, duration `6.428s`, mono, Vorbis, ledger quality `0.45`, status `SOURCE_FILE_ONLY_NOT_IMPORT_OR_RUNTIME_PROOF`.
- `Docs/AssetAudit/AUDIO_CRITICAL_CUE_COVERAGE_MATRIX_20260605.csv` now points `AUDCUE-08` to the existing `.ogg`.
- `python -B -m unittest Tools.test_validate_audio_critical_cue_source_coverage` returned 6 tests OK.
- `python -B Tools\ValidateAudioCriticalCueSourceCoverage.py` now reports `AUDIO_CRITICAL_CUE_SOURCE_COVERAGE_OK blockers=0 rows=12 candidate_paths=28 ledger_matches=28 missing_source_rows=2 placeholder_rows=1`: `AUDCUE-12` remains `PLACEHOLDER_BLOCKED`, but the weak placeholder-boundary wording is fixed.
- This correction removes a fake missing-source blocker only. It does not prove playback, latency, mixer route, Addressables ownership, import settings, memory, listening, or `0 B/frame`.

## P0 Blockers

1. Addressables route is absent. `Packages/manifest.json` declares `com.unity.addressables` `2.9.1`, but `Assets/AddressableAssetsData` contains no settings, groups, profiles, schemas, labels, entries, catalog, or release ledger. Static validator code expects settings/groups at `Assets/_Project/Scripts/Core/Content/Editor/ContentAuthorityBuildValidators.cs` around lines 378, 435, and 442.
2. `Assets/_Project/Data/Audio/Music/Configs/MusicDirectorConfig_Global.asset` has statically non-null dedicated mixer refs, but this still requires Unity readback, mixer output, and listening proof:
   - `_musicMixerGroup` is statically non-null.
   - `_stingerMixerGroup` is statically non-null.
3. `Assets/_Project/Prefabs/Audio/PFB_HectonMusicDirectorRoot.prefab` has null `OutputAudioMixerGroup` on:
   - `MusicVoice_0`
   - `MusicVoice_1`
   - `MusicStinger`
4. `Assets/_Project/Prefabs/Audio/PFB_SpatialAudioManagerRoot.prefab` has null SFX/interface/ambient/threat/bed/routing mixer refs around lines 553-558. `Assets/_Project/MasterMixer.mixer` exists with Music/SFX/Ambient groups, but static YAML shows the groups are not wired.
5. Current static `Assets/_Project/Prefabs/Player.prefab` scan reports `0` direct `Underwater Ambient.wav` refs. Prior `m_Resource` and `_driverClip` refs are source-cleared to `{fileID: 0}` in the working tree, but Unity prefab readback is mandatory before calling removal accepted.
6. `_driverClip` belongs to `DynamicMusicGranularSynthesizer`, and `Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs` uses managed `OnAudioFilterRead(float[] data, int channels)` around lines 775-823. Static source indicates transfer-only copying from a prepublished native buffer, but release acceptance still requires DSP profiler, underrun, no-blocking, and no-GC proof.
7. Current static `Assets/_Project/Prefabs/Player.prefab` scan reports `0` direct `dive_splash.wav` refs. Water entry/exit playback still needs owner route, latency, listening, import, and `0 B/frame` proof if retained through code/events.
8. The current prefab residency gate scans `AudioSource.clip` only in `Assets/_Project/Scripts/Audio/Editor/AudioImportDictator.cs` around lines 764-774. Serialized `AudioClip` fields bypass that gate, so remaining footstep/UI direct refs need a dedicated owner/residency proof route.
9. `Docs/AssetAudit/AUDIO_DIRECT_REF_DETAIL_20260605.csv` reports 24 direct `Player.prefab` clip refs: `0` P0, `20` footstep P1, and `4` UI P1.
10. `Assets/_Project/Audio/Underwater Ambient.wav.meta` conflicts with audit docs. Live meta serializes `loadType: 1`, `compressionFormat: 1`, `quality: 0.7`, `sampleRateOverride: 22050`; docs describe Streaming/Vorbis/Q0.45/193s. Unity import readback is mandatory before policy decisions.

## Required Future Readback

Through Unity only:

- `AddressableAssetSettingsDefaultObject.Settings`, required groups `Core`, `High_Res`, `Overkill`, and schemas using `RequestedAssetAndDependencies`;
- mixer groups for `MusicDirectorConfig_Global`, `PFB_HectonMusicDirectorRoot`, and `PFB_SpatialAudioManagerRoot`;
- runtime director prefab;
- MusicVoicePool sources;
- all `AudioSource.outputAudioMixerGroup`;
- Player prefab direct refs and owning component names, including serialized `AudioClip` fields that are not `AudioSource.clip`;
- clip import `loadType`, compression, quality, sample rate, force mono, preload, background load, platform overrides, duration/channels/imported size;
- Addressables settings, groups, schemas, labels, entries, load mode, owner key, ref count, release ledger, and active handle counts.

## Acceptance

- MusicDirector routes are non-null or explicitly bypassed by an owner-approved native/DSP route with runtime proof.
- SpatialAudioManager routing groups are non-null or explicitly bypassed by an owner-approved native/DSP route with runtime proof.
- Remaining Player direct refs each have owner, cue id/hash, Addressables key/group or fixed-lifetime exception, load phase, release/shutdown phase, playback route, fallback, mix priority, and `0 B/frame` proof.
- Audio prefab validators scan serialized `AudioClip` fields or a separate approved residency validator covers them.
- Addressables are accepted only after settings/groups/entries exist on disk and Unity/player proof shows load/release, memory, and compact pressure behavior.
- Runtime listening, memory, GC, mixer output, import, and Addressables readiness remain `PENDING VERIFICATION` until fresh proof exists.

## Low / Middle / High / Ultra

- Low: breath, warnings, UI, splash, and one owned ambience/music context max; no masking critical cues with long beds.
- Middle: controlled profile breadth only after lifecycle and ducking proof.
- High: richer transitions, stingers, reverb, and layers only through the same owner/ref-count/release truth.
- Ultra: wider density is allowed only after final warnings remain readable and no lifetime route regresses.

Final status: `ADDRESSABLES_P0_BLOCKED / PLAYER_DIRECT_P0_STATIC_CLEARED / STATIC_ONLY / PENDING UNITY PROOF`.

## 2026-06-06 Locke Static Blocker Map

Evidence class: `STATIC_FILE_YAML_META / STATIC_VALIDATOR`. No edits, Unity, import, build, Play Mode, profiler, Addressables mutation, mixer mutation, prefab mutation, `.meta` mutation, or raw YAML edit.

Locke confirmed the current blockers are not theoretical:

1. Addressables absent:
   - `Assets/AddressableAssetsData` exists but has settings/groups/entries count `0`.
   - `Packages/manifest.json` declares `com.unity.addressables` `2.9.1`.
   - `Assets/_Project/Scripts/Core/Content/Editor/ContentAuthorityBuildValidators.cs` expects settings/groups around lines `380`, `386`, `435`, `437`, and `439`.
   - Future Unity owner must create or restore Addressables settings, profiles, schemas, required base groups, labels, entries, and load mode. Readback-only cannot pass because there is no route to read.
2. Mixer fallbacks:
   - `Assets/_Project/Data/Audio/Music/Configs/MusicDirectorConfig_Global.asset:25` has `_musicMixerGroup: {fileID: 0}`.
   - `Assets/_Project/Data/Audio/Music/Configs/MusicDirectorConfig_Global.asset:26` has `_stingerMixerGroup: {fileID: 0}`.
   - `Assets/_Project/Prefabs/Audio/PFB_HectonMusicDirectorRoot.prefab:44`, `:173`, and `:302` have null `OutputAudioMixerGroup` on `MusicVoice_0`, `MusicVoice_1`, and `MusicStinger`.
   - `Assets/_Project/Prefabs/Audio/PFB_SpatialAudioManagerRoot.prefab:553-558` has null `_sfxGroup`, `_interfaceGroup`, `_ambientGroup`, `_threatGroup`, `_bedGroup`, and `_routingMixer`.
3. `AUDCUE-12`:
   - Matrix row `Docs/AssetAudit/AUDIO_CRITICAL_CUE_COVERAGE_MATRIX_20260605.csv:13`.
   - Placeholder candidates: `Assets/_Project/Audio/VO/Stubs/VOStub_Chen_Log01_EN.wav` and `Assets/_Project/Audio/VO/Stubs/VOStub_Chen_Log01_RU.wav`.
   - This stays blocked until final VO bank, localization, subtitle timing, import readback, and runtime dispatch proof exist.
4. Import meta policy:
   - Current validator reports `AUDIO_IMPORT_META_POLICY_REJECTED blockers=41 rows=138 missing_meta=0 load_mismatch=27 compression_mismatch=0 quality_mismatch=14`.
   - 29 unique assets are involved.
   - Long ambient/player loops currently conflict with expected `Streaming / 0.45`; short SFX/UI/VO classes conflict with `DecompressOnLoad` and/or quality policy.

Safe next actions:

1. When Unity/process gate is green and mutation is explicitly allowed, create or restore Addressables settings/groups through Unity APIs, not raw YAML.
2. Unity-readback mixer fields for `MusicDirectorConfig_Global`, `PFB_HectonMusicDirectorRoot`, and `PFB_SpatialAudioManagerRoot`; wire approved mixer groups or document a native/DSP bypass with proof target.
3. Keep `AUDCUE-12` placeholder-blocked; do not mark warning VO final from stubs.
4. Do not raw-edit `.meta`, `.prefab`, `.asset`, `.mixer`, or Addressables YAML. Import policy changes require Unity importer mutation and readback.
5. After controlled Unity owner work, rerun `python -B Tools\RunAssetStaticValidators.py`, then collect Unity Console/import, Play Mode, Profiler/GC, Memory, and listening proof.

Updated status: `ADDRESSABLES_ROUTE_ABSENT / AUDIO_MIXER_IMPORT_BLOCKED / STATIC_ONLY`.

## 2026-06-06 Scene Anchor Regression Recheck

Evidence class: `STATIC_ASSET_YAML / STATIC_VALIDATOR`. No Unity, import, build, Play Mode, profiler, Addressables mutation, mixer mutation, prefab mutation, `.meta` mutation, scene mutation, or raw YAML edit was performed.

Fresh static validation now rejects the audio scene route harder than the previous Locke snapshot:

- `python -B Tools\ValidateAudioAddressablesP0Synthesis.py` fails with `scene-anchor-count: expected exactly one active [MUSIC_SYSTEM] / HectonMusicDirectorAnchor, found 0`.
- `python -B Tools\RunAssetStaticValidators.py` now fails `audio_addressables_p0_synthesis` for the same reason plus `addressables-absent`.
- `rg` finds `[MUSIC_SYSTEM]` / `HectonMusicDirectorAnchor` in `Assets/_Project/Scenes/01_MAIN_MENU.unity`, but not in current `Assets/_Project/Scenes/02_HECTON_WORLD.unity`.
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity` is modified in the working tree. Static diff did not expose a current `[MUSIC_SYSTEM]` row, so this must be treated as unresolved scene-owner state, not as an accepted removal.

Current hard blockers:

1. `02_HECTON_WORLD` lacks the expected active scene-local `[MUSIC_SYSTEM]` anchor.
2. `Assets/AddressableAssetsData` still has settings/groups/entries count `0`.
3. `AUDCUE-12` remains a placeholder VO boundary.
4. Audio import meta policy remains rejected with 41 blockers.

Future Unity owner repair order:

1. Use Unity API readback to confirm whether `[MUSIC_SYSTEM]` was intentionally removed or lost from `02_HECTON_WORLD`.
2. If required, recreate or restore the scene anchor through Unity API only, binding `HectonMusicDirectorAnchor` to `MusicDirectorConfig_Global`.
3. Verify exactly one active runtime anchor/director in `02_HECTON_WORLD`; reject duplicates and null config.
4. Only after scene anchor proof, create/restore Addressables settings/groups/entries through Unity APIs.
5. Keep mixer/import/VO proof separate; do not claim audio route readiness from scene anchor restoration alone.

Updated status: `AUDIO_SCENE_ANCHOR_MISSING / ADDRESSABLES_ROUTE_ABSENT / STATIC_ONLY / PENDING UNITY OWNER REPAIR`.

## 2026-06-06 Peirce Static Audio Route Refinement

Evidence class: `STATIC_SOURCE / STATIC_ASSET_YAML / STATIC_AUDIO_IMPORTER_META / STATIC_AUDIO_LEDGER_CANDIDATE_PATHS`.

Peirce confirmed the scene-anchor regression and refined the future repair order. No files were edited by the sidecar.

Confirmed assets:

- Addressables package exists: `com.unity.addressables@2.9.1` in `Packages/manifest.json` and `packages-lock.json`.
- Addressables data folder exists but is empty: `Assets/AddressableAssetsData` has settings `0`, groups `0`, entries `0`.
- Mixer asset exists: `Assets/_Project/MasterMixer.mixer`, with `Master`, `Music`, `SFX`, `Ambient`, `Surface`, `Underwater`, `BaseInterior`, `SurfaceRain`, and `SurfaceStorm`.

Importer blocker assets:

- Load + quality mismatch: `spaceship sounds - ambient.mp3`, `Atmos 1 Loop.wav`, `Atmos 2 Loop.wav`, `Atmos 2.wav`, `Atmos 3.wav`, `Atmos 5 Loop.wav`, `Atmos 5.wav`, `breathing breath in and out 1.mp3`, `inside suit sounds (too loud).wav`, `swimming - underwater.ogg`, `swimming -onwater.wav`, `Underwater Ambient.wav`.
- Load mismatch: `metal step (1).wav`, `metal step (2).wav`, `rock step (1).wav`, `sand step  (2).wav`, `sand step  (3).wav`, `sand step  (4).wav`, `wet step (1).wav`, `wet step (3).wav`, `wet step (4).wav`, `bubble sound (1).wav`, `water energe  - bulb.wav`, `servo_motor.wav`, `thrust continous.wav`, `electro (nope) sound.flac`, `VOStub_Chen_Log01_RU.wav`.
- Quality-only mismatch: `bubble sound (3).wav`, `bubble sound (4).wav`.

Future Unity-owner sequence:

1. Exclusive Unity owner opens the project. First pass is readback only: Addressables settings, mixer refs, scene anchor, importer settings, and prefab AudioSources.
2. Restore Addressables through editor APIs only: settings under `Assets/AddressableAssetsData`, active profile and local build/load paths, groups `Core`, `High_Res`, and `Overkill`, bundled schemas, `AssetLoadMode.RequestedAssetAndDependencies`, ledger-derived labels, and GUID entries via `AddressableAssetSettings.CreateOrMoveEntry`.
3. Repair mixer refs through Unity APIs: resolve `MasterMixer.mixer` groups, assign `MusicDirectorConfig_Global` music/stinger groups to `Music` unless a stinger subgroup is authored, assign music prefab voice/stinger sources to `Music`, and assign `PFB_SpatialAudioManagerRoot` routing refs. If `Interface`, `Threat`, or `Bed` groups are absent, map interface/threat to `SFX` and bed/ambient to `Ambient` only with owner approval.
4. Repair `02_HECTON_WORLD` scene anchor through Unity scene APIs: exactly one active `[MUSIC_SYSTEM]` with `HectonMusicDirectorAnchor` bound to `MusicDirectorConfig_Global.asset`.
5. Keep `AUDCUE-12` blocked until final warning VO source, subtitle/localization IDs, cue timing, loudness target, and language variants exist. Do not promote `VOStub_Chen_Log01_*` as final.
6. Resolve import-policy conflict before mass reimport. `AudioImportDictator.cs` and `ValidateAudioImportMetaPolicy.py`/ledger expectations differ for ambient/player loops and sub-2s SFX; the Unity owner must pick source of truth, mutate through `AudioImporter`, `SaveAndReimport`, and read back all `138` ledger rows.
7. Re-run gates in order: `ValidateAudioSceneStaticRoute.py --no-fail`, `ValidateAudioCriticalCueSourceCoverage.py --no-fail`, `ValidateAudioImportMetaPolicy.py --no-fail`, then Unity import/Console proof, then Play Mode/profiler/GC/memory/listening proof.

Scaling consequence:

- Low: critical UI, footsteps, warning VO, ambient/music ownership must be loaded, bounded, and readable first.
- Middle: broader ambience/player loops only after Addressables lifecycle proof.
- High: richer stingers, routing, and spatial beds only after mixer/import proof.
- Ultra: wider density only after ref-count, memory, GC, and warning readability remain proven.

## 2026-06-06 Fresh Asset Static Gate Snapshot

Evidence class: `STATIC_VALIDATOR / STATIC_GIT_STATUS / STATIC_AUDIO_LEDGER / STATIC_ASSET_YAML`. No Unity, import, build, Play Mode, profiler, scene, prefab, mixer, Addressables, `.meta`, raw YAML, delete, restore, stage, or commit action was performed.

Fresh commands:

- `python -B Tools\ValidateAudioAddressablesP0Synthesis.py` fails with `scene-anchor-count: expected exactly one active [MUSIC_SYSTEM] / HectonMusicDirectorAnchor, found 0`.
- `python -B Tools\RunAssetStaticValidators.py` fails `audio_addressables_p0_synthesis`.
- Proof harness passes inside the same run: `VISUAL_PROOF_CAPTURE_GUARDRAILS_OK risks=27 asset_refs=21`.

Current audio hard blockers:

1. `scene-anchor-count`: `02_HECTON_WORLD` has `0` active `[MUSIC_SYSTEM] / HectonMusicDirectorAnchor`.
2. `addressables-absent`: `Assets/AddressableAssetsData` has settings `0`, groups `0`, entries `0`.
3. `AUDCUE-12`: warning VO remains `PLACEHOLDER_BLOCKED`; the static source-coverage weak-boundary issue is fixed, but final VO source/bank/localization/subtitle/runtime/listening proof is absent.
4. `AUDIO_IMPORT_META_POLICY_REJECTED blockers=41 rows=138`, with `load_mismatch=27` and `quality_mismatch=14`.

Important non-audio blockers seen in the same run:

- Mass deletion dirty set is still rejected: status rows `11549`, tracked deletions `11233`, tracked modifications `212`, untracked `104`, staged `0`.
- High-risk deletion classes still include `Assets=84`, `Assets/_Project=38`, `.cs=50`, `.asset=11`, `.unity=18`, `Docs/Tasks/POLISH.txt` deleted.

Updated status: `AUDIO_SCENE_ANCHOR_MISSING / ADDRESSABLES_ROUTE_ABSENT / AUDCUE_12_BLOCKED / IMPORT_META_POLICY_REJECTED / PENDING UNITY OWNER REPAIR`.

## 2026-06-06 Kuhn Final Static Repair Packet

Evidence class: `STATIC_SOURCE / STATIC_ASSET_YAML / STATIC_AUDIO_ROUTE_REPAIR_PACKET`. No Unity, import, build, Play Mode, profiler, scene, prefab, mixer, Addressables, `.meta`, raw YAML, delete, restore, stage, or commit action was performed.

Kuhn confirmed the repair packet paths:

- `Assets/_Project/Prefabs/Audio/PFB_HectonMusicDirectorRoot.prefab`
- `Assets/_Project/Prefabs/Audio/PFB_SpatialAudioManagerRoot.prefab`
- `Assets/_Project/Data/Audio/Music/Configs/MusicDirectorConfig_Global.asset`
- `Assets/_Project/MasterMixer.mixer`

Hard blockers remain:

1. `02_HECTON_WORLD` has `0` active `[MUSIC_SYSTEM] / HectonMusicDirectorAnchor`.
2. `Assets/AddressableAssetsData` has settings `0`, groups `0`, entries `0`.
3. `AUDCUE-12` is still a placeholder VO boundary.
4. Importer policy remains rejected until Unity importer mutation/readback resolves the 41 blockers.

Unity-owner packet:

1. Open `02_HECTON_WORLD` only after process gate is green.
2. Restore exactly one active `[MUSIC_SYSTEM]` scene anchor with enabled `HectonMusicDirectorAnchor`.
3. Bind the anchor `_config` to `MusicDirectorConfig_Global.asset` GUID `3fe2e07be4fdac24cb6b2f12b438dcc3`.
4. Keep runtime director prefab GUID `7a86aa3ad745a104d84c2f6622d12430` as the authored runtime route.
5. Restore Addressables through editor APIs: `AddressableAssetSettingsDefaultObject.GetSettings(true)`, groups `Core`, `High_Res`, `Overkill`, and `BundledAssetGroupSchema.AssetLoadMode = RequestedAssetAndDependencies`.
6. Wire mixer refs from `MasterMixer.mixer`: `Music` for music/stinger unless a stinger subgroup is authored, `SFX` and `Ambient` for spatial fallback groups where dedicated groups are absent.
7. Rerun static validators, then Unity Console/import, Play Mode, profiler/GC/memory, and listening proof.

Updated status: `AUDIO_SCENE_ANCHOR_MISSING / ADDRESSABLES_ROUTE_ABSENT / STATIC_REPAIR_PACKET_READY / PENDING UNITY OWNER REPAIR`.

## 2026-06-06 Scene Anchor Current-State Correction

Evidence class: `STATIC_ASSET_YAML / STATIC_VALIDATOR`. No Unity, import, build, Play Mode, profiler, scene, prefab, mixer, Addressables, `.meta`, raw YAML, delete, restore, stage, or commit action was performed.

The later static readback supersedes the earlier `scene-anchor-count=0` snapshots:

- `Tools/ValidateAudioSceneStaticRoute.py --no-fail` now reports `AUDIO_SCENE_STATIC_ROUTE_REJECTED blockers=1`.
- The single hard audio scene-route blocker is `addressables-absent`: `Assets/AddressableAssetsData` still has settings `0`, groups `0`, entries `0`.
- Static YAML sees exactly one active `02_HECTON_WORLD` `[MUSIC_SYSTEM] / HectonMusicDirectorAnchor` bound to `MusicDirectorConfig_Global.asset` GUID `3fe2e07be4fdac24cb6b2f12b438dcc3`.
- `MusicDirectorConfig_Global` mixer refs are statically non-null; `PFB_HectonMusicDirectorRoot.prefab` still has 3 null `AudioSource OutputAudioMixerGroup` refs.
- This does not prove runtime readiness. Unity readback, Console/import, Play Mode, profiler/GC/memory, and listening proof are still required.
- `AUDCUE-12` final VO proof, audio import meta policy blockers, mixer fallback proof, player-footstep/UI lifecycle proof, and Addressables creation remain separate blockers.

Updated status: `ADDRESSABLES_ROUTE_ABSENT / SCENE_ANCHOR_STATIC_ONLY / AUDIO_RUNTIME_PENDING_PROOF`.

## 2026-06-06 Chandrasekhar Current Blocker Consolidation

Evidence class: `STATIC_VALIDATOR / STATIC_ASSET_YAML / STATIC_AUDIO_LEDGER`. No Unity, import, build, Play Mode, profiler, scene, prefab, mixer, Addressables, `.meta`, raw YAML, delete, restore, stage, or commit action was performed.

Fresh current checks:

- `python -B Tools\ValidateAudioSceneStaticRoute.py --no-fail` reports `AUDIO_SCENE_STATIC_ROUTE_REJECTED blockers=1`.
- `python -B Tools\ValidateAudioAddressablesP0Synthesis.py` reports `AUDIO_ADDRESSABLES_P0_SYNTHESIS_OK blockers=1 direct_refs=24 p0=0 footsteps=20 ui=4 fallback_required=1`.
- `python -B Tools\ValidateAudioCriticalCueSourceCoverage.py` reports `AUDIO_CRITICAL_CUE_SOURCE_COVERAGE_OK blockers=0 rows=12 candidate_paths=28 ledger_matches=28 missing_source_rows=2 placeholder_rows=1`.
- `python -B Tools\ValidateAudioImportMetaPolicy.py --no-fail` reports `AUDIO_IMPORT_META_POLICY_REJECTED blockers=41 rows=138`.

Current hard static blocker:

1. `addressables-absent`: `Assets/AddressableAssetsData` has settings `0`, groups `0`, entries `0`.

Current non-acceptance debts:

- `02_HECTON_WORLD` has one active `[MUSIC_SYSTEM] / HectonMusicDirectorAnchor` bound to `MusicDirectorConfig_Global.asset` GUID `3fe2e07be4fdac24cb6b2f12b438dcc3`; this is static YAML evidence only, not Unity/runtime proof.
- `MusicDirectorConfig_Global.asset` has statically non-null `_musicMixerGroup` and `_stingerMixerGroup`.
- `PFB_HectonMusicDirectorRoot.prefab` still has 3 null `AudioSource.OutputAudioMixerGroup` refs.
- `PFB_SpatialAudioManagerRoot.prefab` still has null `_sfxGroup`, `_interfaceGroup`, `_ambientGroup`, `_threatGroup`, `_bedGroup`, and `_routingMixer`.
- `Player.prefab` has 24 current static direct P1 audio refs: 20 footstep refs and 4 UI refs. Current P0 direct refs to `Underwater Ambient.wav` and `dive_splash.wav` are 0, pending Unity prefab readback.
- `AUDCUE-12` remains blocked because `VOStub_Chen_Log01_EN/RU` cannot prove final warning VO loudness, timing, subtitles, localization, or route priority.
- Import policy remains rejected until Unity `AudioImporter` mutation/readback resolves 41 static mismatches.

Reject these stale claims:

- `scene-anchor-count=0`, `AUDIO_SCENE_ANCHOR_MISSING`, or any instruction to restore the current world music anchor before readback.
- `MusicDirectorConfig_Global` mixer refs are currently null.
- `28` current `Player.prefab` direct audio refs.
- Current P0 direct `Underwater Ambient.wav` / `dive_splash.wav` refs.
- Addressables package is missing. The package exists; the Addressables data route is absent.
- Runtime audio readiness from static non-null config refs.

Updated status: `ADDRESSABLES_ROUTE_ABSENT / MIXER_PREFAB_FALLBACKS_PENDING / AUDCUE_12_BLOCKED / IMPORT_META_POLICY_REJECTED / AUDIO_RUNTIME_PENDING_UNITY_PROOF`.
