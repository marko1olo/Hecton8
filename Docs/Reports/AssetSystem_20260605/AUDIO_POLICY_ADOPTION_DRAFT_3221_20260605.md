# Audio Policy Adoption Draft - Asset Worker 3221 - 2026-06-05

Status: `PATCH BASIS DRAFT ONLY - NOT ADOPTED`
Evidence boundary: `STATIC_DOC` / `STATIC_SOURCE` only.
First-20 route moment: first exit, first surface/shallow read, photic shelf orientation, warning audibility, player breath/movement continuity.

This draft does not adopt policy into `AGENTS.md`, `audio.md`, `streaming.md`, or any stable authority file. It does not change imports, Addressables groups, mixer refs, prefabs, scenes, audio assets, or files under `Assets/`.

## Sources Read

- `AGENTS.md`
- `audio.md`
- `performance.md`
- `streaming.md`
- `Docs/Reports/AssetSystem_20260605/AUDIO_IMPORT_POLICY_DECISION_BRIEF_3217_20260605.md`
- `Docs/Reports/AssetSystem_20260605/AUDIO_POLICY_CONFLICT_AND_CUE_DISPOSITION_3213_20260605.md`
- `Docs/AssetAudit/AUDIO_ROUTING_REMEDIATION_SPEC_20260605.md`
- `Docs/Audio/audio_asset_ledger.csv`
- `Docs/Audio/audio_remediation_matrix_20260605.csv`
- `Docs/AssetAudit/ASSET_SYSTEM_INDEX_20260605.md`

Mandates followed:

- `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits`
- `QA_Evidence_Text_Filter_Audit`

## Evidence Boundary

Evidence class is static only:

- `STATIC_DOC`: root/domain bibles, reports, remediation spec, asset index.
- `STATIC_SOURCE`: CSV ledger fields, static import metadata fields, serialized-reference evidence summarized by prior reports.

Absent proof:

- No listening pass.
- No Unity import readback.
- No Unity run.
- No Play Mode.
- No Addressables build or group mutation.
- No Memory Profiler capture.
- No runtime mix proof.
- No DSPGraph/native audio proof.
- No audio-thread safety proof.
- No GCMonitor or `0 B/frame` proof.

All runtime, import, residency, mix, and audio-thread claims remain `PENDING VERIFICATION`.

## Conflict To Resolve

Static conflict:

- `AGENTS.md` says ambient/music target Vorbis Q70 and ambient/music load as `Compressed In Memory`, while also allowing streaming music only and forbidding streaming SFX.
- The streaming mandate gives a duration classifier: clips over 10 seconds stream, clips 2-10 seconds use `CompressedInMemory`, clips up to 2 seconds use `DecompressOnLoad`.
- `audio.md` does not own import load types. It owns cue purpose, mix priority, hot-path behavior, DSP/thread safety, and proof gates.
- `streaming.md` owns Addressables residency, ref-count, release order, memory pressure, active bank admission, and proof.

Proposed resolution:

- Adopt a hybrid exception-table policy.
- Duration is the default classifier.
- AGENTS quality/sample-rate/mono requirements are retained.
- Generic SFX never stream.
- Music and non-critical long ambience may stream only through owned Addressables/MusicDirector/ambient-bank routes.
- Player-critical loops are not generic SFX and need owner exception proof before import changes.
- Exceptions are few, route-owned, and blocked until proof exists.

## Proposed Policy Clause

Default policy is proposed, not applied:

| Class | Duration | Default load type | Default format/rate | Route rule |
|---|---:|---|---|---|
| `music` | `<=2s` | `DecompressOnLoad` | Vorbis Q70 or approved stinger format, 44100 Hz | MusicDirector stinger only; prove cooldown and ducking. |
| `music` | `>2s` and `<=10s` | `CompressedInMemory` | Vorbis Q70, 44100 Hz | MusicDirector transition/short cue; no constant bed claim without runtime proof. |
| `music` | `>10s` | `Streaming` | Vorbis Q70, 44100 Hz | MusicDirector profile prefetch, current/next context only unless memory proof expands it. |
| `ambient` | `<=2s` | `DecompressOnLoad` | Vorbis Q70 target or approved short bed format | Rare one-shot/transition ambience; must not mask warnings. |
| `ambient` | `>2s` and `<=10s` | `CompressedInMemory` | Vorbis Q70 target | Short bed/transition loop; active-bank owner required. |
| `ambient` | `>10s` | `Streaming` | Vorbis Q70 target | Long non-critical bed through ambient-bank Addressables route; first-exit exceptions require proof. |
| `sfx` | `<=2s` | `DecompressOnLoad` | ADPCM, 22050 Hz default; force mono for 3D | Generic one-shot SFX. Must not stream. |
| `sfx` | `>2s` and `<=10s` | `CompressedInMemory` | ADPCM or Vorbis only with owner proof; force mono for 3D | Medium non-loop cue. Must not stream. |
| `sfx` | `>10s` | `BLOCKED` | None | Invalid generic SFX taxonomy; split, shorten, or reclassify with owner proof. |
| `ui` | `<=2s` | `DecompressOnLoad` | ADPCM, 22050 Hz default; stereo only if UI owner proves need | Instrument feedback; low latency required. |
| `ui` | `>2s` and `<=10s` | `CompressedInMemory` | ADPCM/Vorbis with owner proof | Long UI feedback is suspicious and requires route role. |
| `ui` | `>10s` | `BLOCKED` | None | Reclassify as voice, music, tutorial bed, or reject. |
| `player_loop` | `<=2s` | `DecompressOnLoad` | ADPCM or Vorbis Q70 target, rate by owner proof | Breath/suit/movement continuity, not generic SFX. |
| `player_loop` | `>2s` and `<=10s` | `CompressedInMemory` | Vorbis Q70 target or ADPCM with proof | Default low-latency first-person loop route. |
| `player_loop` | `>10s` | `Streaming` by duration, but `BLOCKED UNTIL EXCEPTION PROOF` | Vorbis Q70 target | Long player loops may stream only after latency, prewarm, owner, and mix proof. |
| `voice` | `<=2s` | `DecompressOnLoad` | ADPCM, localized rate proof required | Final VO only; current VO stubs are not authority. |
| `voice` | `>2s` and `<=10s` | `CompressedInMemory` | ADPCM/Vorbis by localization owner proof | Subtitle timing, loudness, and memory proof required. |
| `voice` | `>10s` | `Streaming` by owner exception | Vorbis/voice-bank format by localization proof | Long audio logs only; subtitle/preload/memory proof required. |

Generic SFX clarification:

- Generic SFX must not stream.
- Current ledger has no `sfx` rows with `Streaming`.
- Long breath, suit, swim, and surface-swim rows are `player_loop`, not generic SFX.
- Current long `player_loop` streaming rows are blocked until owner exception proof exists. Duration alone is not enough.

## Exception Table Requirements

Any exception from the default table must be ledgered before import mutation.

| Required field | Acceptance requirement |
|---|---|
| Owner | Named system owner, not `PENDING_OWNER`. |
| Route role | First-20 moment or gameplay function: breath, warning, route cue, threat cue, MusicDirector context, ambience bank, VO/audio-log, UI feedback. |
| Active-bank budget | Explicit RAM/residency budget for active bank and max concurrently admitted banks. |
| Addressables key/group | Stable group and key, not `PENDING_ADDRESSABLES`; group load mode and release ledger route named. |
| Load phase | Bootstrap, scene-load gate, MusicDirector prefetch, ambient-bank swap, player-loop prewarm, or VO/audio-log request. |
| Release phase | Ordered release phase with owner, ref-count, disable/unsubscribe/stop order, and memory-pressure behavior. |
| Memory Profiler proof | Active resident memory, committed memory, streaming buffers, total reserved memory, compact-lane pressure response. |
| Runtime mix proof | Music/ambient ducking, warning audibility, silence windows, stinger cooldown, player-loop continuity, no constant-bed masking. |
| Audio-thread safety proof | No blocking, no managed decode/mix/synthesis in release callback, no unsafe `OnAudioFilterRead` production route, underrun proof where DSP/native route is used. |
| `0 B/frame` proof | GCMonitor/Profiler capture through cue changes, UI feedback, warnings, player loops, MusicDirector transitions, and ambient swaps. |
| Import readback | Unity readback of load type, compression, quality, sample rate, force mono, preload/background settings, and platform overrides. |
| Expiry/review | Exception expires if owner route, Addressables group, clip class, duration, or first-20 route role changes. |

## Current Ledger Blockers

Static ledger facts:

- `Docs/Audio/audio_asset_ledger.csv` has 138 rows.
- 138 rows still have `owner=PENDING_OWNER`.
- 138 rows still have `addressable_group=PENDING_ADDRESSABLES`.
- 138 rows still have `addressable_key=PENDING_ADDRESSABLES`.
- Class counts: 84 `music`, 30 `sfx`, 12 `ambient`, 5 `ui`, 5 `player_loop`, 2 `voice`.
- Current load surface: 84 music streaming; 8 ambient streaming; 4 ambient compressed; 4 player loops streaming; 1 player loop compressed; 0 generic SFX streaming.
- Eight ambient rows are Vorbis Q0.45 and conflict with the Q70 target.

Blockers from 3213, remediation spec, and matrix:

- MusicDirector mixer nulls: `MusicDirectorConfig_Global.asset` has null `_musicMixerGroup` and `_stingerMixerGroup` fields in static evidence.
- Player prefab direct refs: current static scan reports 24 direct `AudioClip` refs in `Assets/_Project/Prefabs/Player.prefab`; prior `dive_splash.wav` and `Underwater Ambient.wav` direct refs are source-cleared but still lack Unity prefab readback, owner/removal ledger, playback/absence proof, and 0 B proof.
- VO stubs placeholder: `VOSTUB_CHEN_LOG01_EN` and `VOSTUB_CHEN_LOG01_RU` are 1.341s placeholder rows and cannot prove final VO duration, localization, subtitle timing, loudness, or delivery policy.
- Player-loop risk: `BREATHING_BREATH_IN_AND_OUT_1`, `INSIDE_SUIT_SOUNDS_TOO_LOUD`, `SWIMMING_UNDERWATER`, and `SWIMMING_ONWATER` are streaming player-layer rows or long loop risks, not accepted by duration alone.
- Music/ambient mix risk: loud/dense beds, repeated stingers, and first-exit/shallow candidates require MusicDirector runtime proof, ducking, silence windows, warning audibility, and listening pass.

Disposition: adoption can be drafted, but import changes remain blocked.

## GlobalQualityWeight Consequences

`GlobalQualityWeight` scales breadth, cadence, prefetch, active bank count, diagnostic depth, and secondary mix density. It must not change gameplay truth, cue IDs, Addressables keys, owner route, save identity, DTO layout, warning facts, or release order.

| Lane | Consequence |
|---|---|
| Low / compact, low `GlobalQualityWeight` | One active ambient bank maximum under pressure. Music streams current context only. Player breath, warning, threat, route, and UI feedback stay admitted and low-latency. Decorative layers and speculative music prefetch shrink first. Generic SFX remain non-streaming. |
| Middle | Current plus likely next MusicDirector profile may prefetch if memory proof is green. More ambience layers can be admitted only after warning ducking and player-loop continuity remain proven. |
| High | Wider MusicDirector transition prefetch, richer ambience, stronger hydrophone/detail layers, and more reverb/occlusion support are allowed after audio-thread and runtime mix proof. Critical cue audibility still wins over beds. |
| Ultra | Dense secondary beds, convolution/reverb, stinger variety, and richer dynamic mix can be admitted with proof. Long library music may still stream; saved memory buys richer mix layers, not blind resident conversion. |

No binary quality switch is proposed. These are scalar consequences driven by `GlobalQualityWeight`, memory pressure, active route, and proof.

## Regression Model

- CPU: Draft changes no runtime CPU. Future import/policy adoption risks decode spikes, streaming dispatch overhead, mixer work, and DSP/thread contention. Profiler proof required before acceptance.
- GC: Draft changes no runtime GC. Future route must prove `0 B/frame` through cue changes, UI feedback, warnings, player loops, ambient swaps, and MusicDirector transitions.
- Memory: Draft changes no runtime memory. Future adoption can increase resident audio memory if exceptions use `CompressedInMemory`; Memory Profiler proof and active-bank budgets are mandatory.
- Cadence: Draft changes no runtime cadence. Future streaming/prefetch must prove no first-exit/shallow stalls, no warning delay, no player-loop start jitter, and no audio-thread underruns.
- Correctness: Draft resolves policy direction only. Current owner, Addressables group/key, mixer refs, prefab direct refs, VO placeholder state, import readback, mix, and listening proof remain unresolved.

## Proposed Stable Doc Patch Snippet - NOT APPLIED

Candidate text for future controller/human adoption only:

```text
[REQ] Audio import/load policy: ambient/music target Vorbis Q70; music defaults to 44100 Hz; generic 3D SFX default to ADPCM 22050 Hz and force mono unless owner proof requires otherwise. Default load type is duration-based: clips >10s Streaming, clips >2s and <=10s CompressedInMemory, clips <=2s DecompressOnLoad.
[FORBID] Streaming generic one-shot SFX.
[REQ] Music and long non-critical ambience may stream only through owned Addressables/MusicDirector/ambient-bank routes with prefetch, ref-count, release ledger, active-bank budget, memory-pressure behavior, runtime mix proof, audio-thread safety proof, and 0 B/frame proof.
[REQ] Breath, suit, swim, warning, and other first-person continuity loops are `player_loop`, not generic SFX. Long player-loop rows remain blocked until a ledgered owner exception proves latency, prewarm, mix priority, Memory Profiler, audio-thread safety, and 0 B/frame behavior.
```

Candidate ledger governance text:

```text
Every audio ledger row must declare class, route use, owner, Addressables group/key, intended load type, import proof status, and exception status. Exception status may not be accepted while owner, group, key, Memory Profiler proof, runtime mix proof, audio-thread safety proof, or 0 B/frame proof is missing.
```

## Adoption Gate

This draft can be used as the patch basis for stable docs only after a controller/human decision. Even after adoption, import mutation remains blocked until:

- 138 pending owner fields are resolved.
- 138 pending Addressables groups are resolved.
- 138 pending Addressables keys are resolved.
- MusicDirector mixer nulls are resolved or documented as a DSP/native bypass exception.
- Player prefab direct refs are rerouted or explicitly ledgered with owner/load/release/playback proof.
- VO stubs are replaced or kept explicitly as placeholders with final localization proof pending.
- Unity import readback, Memory Profiler, runtime mix, audio-thread safety, listening pass, and `0 B/frame` proof exist.

Final disposition: proposed policy direction is available for review. Runtime/import acceptance remains `PENDING VERIFICATION`.
