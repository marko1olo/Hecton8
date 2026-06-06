# Audio Import Policy Exception Table - 2026-06-05

Status: `PENDING_AUTHORITY_DECISION`.
Evidence class: `STATIC_DOC` / `STATIC_SOURCE` only.
Scope: static exception table for music, ambient, player_loop, UI, SFX, VO, MusicDirector mixer blockers, and Player prefab direct clip refs.
First-20 route risk framed: reduces future audio/import owner guessing while first exit, shallow read, player breath/movement continuity, UI feedback, and warning audibility remain unproved.

This is not stable authority adoption. This is not Unity import acceptance. This is not runtime mix proof. This is not Addressables proof. This is not 0 B/frame proof. No import settings are fixed by this document.

## Required Reads

- `AGENTS.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `.agents-skills/AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt`
- `Docs/AssetAudit/ASSET_SYSTEM_INDEX_20260605.md`
- `taskslocal/asset_system_20260605/README.md`
- `Docs/Reports/AssetSystem_20260605/AUDIO_IMPORT_POLICY_DECISION_BRIEF_3217_20260605.md`
- `Docs/AssetAudit/AUDIO_ROUTING_REMEDIATION_SPEC_20260605.md`
- `Docs/AssetAudit/AUDIO_REMEDIATION_MATRIX_REVIEW_20260605.md`
- `Docs/Audio/audio_remediation_matrix_20260605.csv`
- `Docs/Audio/audio_asset_ledger.csv`
- `Docs/Audio/audio_profile_usage_20260605.csv`

Mandate followed:

- `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC`: static policy cannot prove DSPGraph safety, SPSC correctness, underrun safety, managed callback safety, runtime mix acceptance, or 0 B/frame behavior.

## Authority Conflict

`AGENTS.md` says ambient/music target Vorbis Q70 and `Compressed In Memory`, while also forbidding streaming SFX and allowing streaming music. The duration-based streaming rule referenced by the prior decision brief sends clips over 10 seconds to `Streaming`, clips from 2-10 seconds to `CompressedInMemory`, and clips up to 2 seconds to `DecompressOnLoad`.

Conflict disposition: `PENDING_AUTHORITY_DECISION`.

Static consequence:

- Music is currently 84/84 `Streaming`, Vorbis Q0.7, 44100 Hz, wired through MusicDirector profiles.
- Ambient is 8 `Streaming` and 4 `CompressedInMemory`; several ambient rows are Vorbis Q0.45, below the Q70 target.
- Player breath/suit/swim loops are classified as `player_loop`, not generic SFX.
- Generic SFX rows do not stream in the current static ledger.
- UI and VO stub rows are short, but final playback/import policy is unproved.

## Exception Table

| Priority | Audio class | Default policy | Exception policy | Owner route | Proof needed | Blockers | Disposition |
|---|---|---|---|---|---|---|---|
| P0 | music_profiles | Default unresolved. Duration rule favors `Streaming` for long music; AGENTS has ambient/music `CompressedInMemory` wording but explicitly permits streaming music. | Hybrid candidate only: keep long MusicDirector profile music streaming with owned prefetch/release if adopted; do not convert the 84-track library to resident compressed memory without Memory Profiler proof. | MusicDirector owner plus Streaming/Addressables owner. | Stable authority decision, Unity import readback, MusicDirector runtime capture, mixer route proof, memory/residency proof, 0 B/frame proof, listening pass for first exit/shallow/silence windows. | `_musicMixerGroup` and `_stingerMixerGroup` null; Addressables ownership/release unproved; runtime mix unproved. | `PENDING_AUTHORITY_DECISION` |
| P1 | long_non_critical_ambience | Duration rule favors `Streaming` for >10s ambience; AGENTS says ambient/music `CompressedInMemory` and Q70. | Long non-critical ambience may stream through an owned active-bank route if adopted. First-exit/shallow critical beds may use `CompressedInMemory` only as named exceptions with memory and mix proof. | Audio ambient-bank owner plus Streaming/Addressables owner. | Import readback, active-bank budget, warning ducking proof, listening pass, Memory Profiler, 0 B/frame proof. | Ambient Q0.45 rows below Q70; load-type conflict unresolved; no runtime warning-mask proof. | `PENDING_AUTHORITY_DECISION` |
| P0 | player_loop_breath_suit_swim | Default by duration: <=2s `DecompressOnLoad`, 2-10s `CompressedInMemory`, >10s `Streaming`. This default is insufficient for first-person continuity. | Breath, suit, swim, and player-layer movement loops may use low-latency `CompressedInMemory` or prewarmed owned routes when streaming causes start jitter, stall risk, or control-readability damage. They remain separate from generic SFX. | Player/audio lifecycle owner plus Audio import/lifecycle owner. | Player loop ledger rows, owner/load/release/playback route, prefab readback where referenced, latency proof, listening pass, runtime playback proof, 0 B/frame proof. | Five player_loop rows have unresolved policy; several are streaming Q0.45; owner and Addressables fields are pending. | `PENDING_AUTHORITY_DECISION` |
| P1 | short_sfx | Generic short SFX must not stream. Default <=2s `DecompressOnLoad`; 2-10s `CompressedInMemory`; 3D SFX force mono per AGENTS. | `CompressedInMemory` on short SFX may remain only after import readback and runtime memory/playback proof. No generic streaming SFX exception. | Player/audio lifecycle owner plus Audio import owner. | Prefab readback for direct refs, import readback, owner ledger, playback-route proof, 0 B/frame proof. | 20 short_sfx remediation rows are direct Player.prefab refs; Addressables/release route unproved. | `PENDING_VERIFICATION` |
| P1 | ui_feedback | Treat short UI as instrument feedback: low latency, non-streaming, normally `DecompressOnLoad` for <=2s. | `CompressedInMemory` may be kept for specific UI clips only if memory and latency proof passes. UI feedback must remain audible against music/ambient ducking. | Player/audio lifecycle owner plus UI/audio owner. | Prefab readback, UI feedback listening pass, warning/audibility proof, import readback, runtime 0 B/frame proof. | Four UI remediation rows are direct Player.prefab refs; current UI ledger has mixed `CompressedInMemory` and `DecompressOnLoad`; runtime audibility unproved. | `PENDING_VERIFICATION` |
| P1 | voice_stubs | Current VO stubs are short placeholder rows. Duration alone is not final VO policy. | Do not infer final VO import policy from stubs. Final VO requires localization, subtitle timing, loudness, memory, and playback proof. | Audio import/lifecycle owner plus localization/narrative owner. | Final VO duration set, localization/subtitle route, import readback, runtime memory/mix proof, accessibility proof. | Stub quality 0.22; placeholder flag/routing unresolved; final localization and subtitle timing absent. | `PLACEHOLDER_BLOCKED` |
| P0 | MusicDirector_mixer_null_blocker | MusicDirector profile routing cannot be accepted while mixer route fields are null. | If Unity mixer groups are intentionally bypassed by native/DSP routing, document the bypass as an owned route exception with phase and proof target. | MusicDirector owner. | Unity config readback, runtime MusicDirector route capture, Console state, explicit DSP/native bypass proof if used. | `_musicMixerGroup` null; `_stingerMixerGroup` null. | `PENDING_VERIFICATION` |
| P0 | Player_prefab_direct_clip_refs | Direct serialized AudioClip refs are not Addressables or lifecycle proof. | Temporary direct refs may exist only as documented owner exceptions with load/release/playback route and hot-path safety proof; they must not become readiness evidence. | Player/audio lifecycle owner. | Unity prefab readback, owner ledger, Addressables or documented exception, runtime playback route, 0 B/frame proof. | 24 current direct Player.prefab AudioClip refs: footsteps and UI feedback. Prior `Underwater Ambient.wav` and `dive_splash.wav` direct refs are source-cleared but pending Unity prefab readback. | `PENDING_VERIFICATION` |

## Low / Middle / High / Ultra Consequences

Low / compact:

- Music streams through the current profile only; next-profile prefetch is allowed only with memory proof.
- One active ambient bank maximum under pressure.
- Breath, suit, swim, warning, and UI feedback stay low-latency before decorative beds.
- Generic SFX remain non-streaming and mono where 3D.
- No cue truth, owner route, Addressables key, DTO layout, or save identity changes with `GlobalQualityWeight`.

Middle:

- Current plus likely-next MusicDirector profile prefetch can be admitted if memory proof is clean.
- Additional ambience layers can be admitted only after warnings, UI, breath, and route cues remain readable.
- Player loops retain priority over music and ambient beds.

High:

- Wider MusicDirector transition prefetch and richer stinger/cadence set are allowed after runtime capture.
- More ambience support layers and reverb/occlusion support can be added only after audio-thread and mix proof.
- Saved memory/CPU buys mix richness, not new gameplay truth.

Ultra:

- Dense secondary beds, broader music prefetch, stronger convolution/reverb, and richer stinger layering are allowed only if warning, breath, route, and UI audibility remain proven.
- Streaming remains valid for long library breadth; resident-everything is rejected without Memory Profiler proof.

## Regression Model

CPU:

- No runtime CPU changed by this document.
- Future import/load edits must prove decode, prefetch, mix, and playback routes stay within budget and do not block the audio thread.

GC:

- No runtime GC changed by this document.
- Future playback/import changes require 0 B/frame GCMonitor or profiler proof through first-exit/shallow cue changes, UI feedback, player loops, and MusicDirector transitions.

Memory:

- No import memory changed by this document.
- Broadly converting long music/ambient to resident compressed memory is rejected until compact-lane Memory Profiler proof exists.

Cadence:

- No runtime cadence changed by this document.
- Future prefetch/ducking/stinger behavior must be proven by MusicDirector runtime capture and listening notes.

Correctness:

- This table reduces guessing only.
- It does not prove runtime mix, import settings, Addressables ownership, warning audibility, DSP safety, or player-loop continuity.

Final disposition: `PENDING_AUTHORITY_DECISION`.
