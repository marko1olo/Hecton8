# Audio P0 Static Execution Refinement - 2026-06-05

Status: `PENDING_VERIFICATION`.
Evidence class: `STATIC_DOC`, `STATIC_SOURCE`, `AUDIO_SOURCE_PROBE`, `AUDIO_WAVEFORM_QA`.
Owned output: `Docs/Reports/AssetSystem_20260605/AUDIO_P0_STATIC_EXECUTION_REFINEMENT_20260605.md/.csv`.

No Unity run, build, Play Mode, import edit, prefab edit, mixer edit, scene edit, Addressables operation, listening pass, profiler capture, runtime mix proof, or `Assets/` mutation was performed.

## Mandates Followed

- `.agents-skills/AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt`
- `.agents-skills/AUDIO_Hrtf_Binaural_Spatialization.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/ARCH_Signal_Lane_Segregation.txt`
- `audio.md`, `streaming.md`, `performance.md`

## What Was Wrong

The six audio P0 rows were correctly identified but still too coarse for execution. They mixed different proof lanes:

- MusicDirector null `_musicMixerGroup` and `_stingerMixerGroup` need config/mixer route readback before any music or stinger taste judgment.
- `Player.prefab` direct refs need prefab component/field readback before unwiring, deletion, import edits, or route adoption.
- `dive_splash.wav` is a short player-contact cue. It needs contact SFX ownership and no-allocation playback proof, not generic streaming.
- `Underwater Ambient.wav` is a 193s long direct ref. It needs classification as ambient bank, player-loop exception, fixed startup/core exception, Addressables-owned long ambience, or removal/replacement candidate.
- Static source loudness, waveform facts, and prefab serialization are not listening proof, runtime proof, import proof, or Addressables proof.
- The import policy is still conflicted: root audio wording favors ambient/music compressed-in-memory, while streaming lifecycle rules put clips over 10s on streaming unless a bounded exception is proved. The only safe next action is row-level classification plus Unity import readback, not a broad preset.

First-20 route moment affected: first world load, first exit, shallow/photic read, breath/suit continuity, water entry/exit contact, warning/UI audibility, and music restraint.

## Refined Order

1. `ASSET_OWNER_10_MUSICDIRECTOR_AUDIO_ROUTING_PACKET.md`: Unity-read `MusicDirectorConfig_Global.asset`; close `_musicMixerGroup` and `_stingerMixerGroup` via approved mixer groups or documented owned native/DSP bypass.
2. `ASSET_OWNER_08_AUDIO_DIRECT_REF_UNWIRING_PACKET.md`: Unity-read `Player.prefab`; map the four P0 direct refs to exact owning components and fields. Do not trust YAML line numbers as final authority.
3. `ASSET_OWNER_19_AUDIO_IMPORT_AUTHORITY_ADOPTION_PACKET.md`: resolve the import/load policy conflict before any import mutation or exception promotion.
4. `ASSET_OWNER_23_AUDIO_SOURCE_TECHNICAL_REMEDIATION_PACKET.md`: perform Unity import readback for the P0 clips; source probe facts remain preflight only.
5. `ASSET_OWNER_15_ADDRESSABLES_ASSET_GROUP_EXECUTION_BLOCKERS_PACKET.md`: assign Addressables group/key or fixed-startup exception only after owner/classification is closed.
6. `ASSET_OWNER_03_AUDIO_LEDGER_LISTENING.md`: run listening proof only after P0 route blockers are closed. Listening before routing is false evidence.

CSV row actions are in `AUDIO_P0_STATIC_EXECUTION_REFINEMENT_20260605.csv`.

## Blockers

- `MusicDirectorConfig_Global.asset` has static null mixer refs for music and stingers. Runtime output is unjudged until Unity readback closes this.
- `Player.prefab` has four P0 direct refs: `Underwater Ambient.wav` at lines `137` and `239`, `dive_splash.wav` at lines `1066` and `1067`. Static YAML line evidence is not enough to mutate.
- `Underwater Ambient.wav` is long, currently reported as `Streaming`, `Vorbis`, quality `0.45`, duration `193s`. It cannot be accepted or removed until route class and lifecycle are named.
- `dive_splash.wav` is short, currently reported as `CompressedInMemory`, `ADPCM`, duration `1.729s`. It cannot be converted to streaming SFX.
- Addressables data/lifecycle proof is absent. Direct refs are not handle ownership.
- Runtime proof, listening proof, GC proof, DSP proof, and memory/residency proof are absent.

## No-Delete And No-Unwire Rules

- Do not delete, unwire, replace, or null any P0 direct ref from static tables alone.
- Do not raw-edit `.prefab`, `.asset`, mixer, import, or scene YAML.
- Do not remove `Underwater Ambient.wav` until Unity readback identifies the component role and the owner decides ambient bank, player-loop exception, fixed startup/core exception, Addressables route, or replacement.
- Do not remove `dive_splash.wav` until Unity readback confirms the entry/exit components and a replacement/player-contact route exists.
- Do not treat retained direct refs as valid exceptions without owner, cue id/hash, load phase, release/shutdown phase, playback route, priority/ducking rule, fallback, import readback, lifecycle proof, and hot-path allocation proof.

## Proof Boundary

Accepted future proof classes:

- `EDITOR_VERIFIED`: Unity config/prefab/import readback with Console state.
- `PLAYMODE_VERIFIED`: MusicDirector route, direct-ref replacement route, cue priority, and playback observed in scene.
- `PROFILER_VERIFIED`: Profiler/GCMonitor proof for `0 B/frame` under cue spam, profile transitions, and ambient/player-loop swaps.
- `MEMORY_VERIFIED`: Memory Profiler or equivalent resident/committed proof for retained long beds and active audio banks.
- `PLAYER_CAPTURE_VERIFIED`: capture/listening notes for first exit, shallow shelf, warning/UI overlap, breath/suit/swim continuity, silence windows, and stinger cadence.

Forbidden proof claims:

- `0 B/frame` from static scan.
- mixer routing from static profile refs.
- Addressables ownership from `Player.prefab` serialization.
- listening acceptance from source loudness or waveform probes.
- runtime readiness from this report.

## Static Next-Actions By Row

| P0Id | First owner | Static next action | Status |
|---|---|---|---|
| `AUDIO-P0-01` | Owner 10 MusicDirector | Unity-read `_stingerMixerGroup`; assign approved route or document native/DSP bypass. | `PENDING_MUSICDIRECTOR_CONFIG_READBACK` |
| `AUDIO-P0-02` | Owner 10 MusicDirector | Unity-read `_musicMixerGroup`; assign approved route or document native/DSP bypass. | `PENDING_MUSICDIRECTOR_CONFIG_READBACK` |
| `AUDIO-P0-03` | Owner 08 direct refs | Unity-read `waterExitSplashClip`; classify as player contact SFX route or scoped exception. | `PENDING_PLAYER_PREFAB_READBACK` |
| `AUDIO-P0-04` | Owner 08 direct refs | Unity-read `waterEntrySplashClip`; classify as player contact SFX route or scoped exception. | `PENDING_PLAYER_PREFAB_READBACK` |
| `AUDIO-P0-05` | Owner 08 direct refs | Unity-read `m_Resource`; classify `Underwater Ambient.wav` route before any mutation. | `PENDING_PLAYER_PREFAB_READBACK` |
| `AUDIO-P0-06` | Owner 08 direct refs | Unity-read `_driverClip`; classify `Underwater Ambient.wav` route before any mutation. | `PENDING_PLAYER_PREFAB_READBACK` |

## Continuous Quality Consequences

- Low/compact: admit breath, warnings, UI/instrument feedback, sonar/threat, route cues, and water entry/exit before decorative beds. One active long ambience/music context unless memory proof permits more.
- Middle: admit current plus likely-next context only after route ownership, ducking, player-loop continuity, and memory proof.
- High: spend headroom on MusicDirector transitions, profile-specific stingers, hydrophone/reverb detail, and silence-window control after warning priority is proved.
- Ultra: add dense secondary beds and richer spatial/reverb detail only after critical cue readability, lifecycle, and no-allocation proof remain intact.

`GlobalQualityWeight` may scale layer count, prefetch breadth, active-bank breadth, reverb cost, diagnostics, and update cadence. It must not change cue truth, cue IDs, owner route, Addressables keys, release order, warning facts, DTO layout, or save identity.

## Regression Model

- CPU: static report only. Future risk comes from decode, streaming dispatch, MusicDirector crossfades, stinger scheduling, player contact cue spam, and DSP contention.
- GC: static report only. Future risk comes from string cue lookup, dynamic collections, logging, managed callbacks, UI churn, direct event routes, and unpooled playback.
- Memory: static report only. Future risk comes from broad long-bed residency, duplicate direct refs, streaming buffers, stereo/high-rate short cues, and missing release.
- Cadence: static report only. Future risk comes from constant ambience/music, repeated stingers, missing silence windows, water-contact spam, and player-loop start jitter.
- Correctness: P0 rows are refined into execution handoffs only. Import state, prefab state, mixer state, runtime playback, listening result, DSP safety, Addressables lifecycle, and memory residency remain unproved.

Final disposition: `PENDING_VERIFICATION`.
