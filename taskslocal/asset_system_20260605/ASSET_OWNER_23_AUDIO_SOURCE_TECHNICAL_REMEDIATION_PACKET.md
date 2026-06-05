# Asset Owner 23 - Audio Source Technical Remediation Packet

Status: `PENDING_VERIFICATION`.
Evidence class: `STATIC_DOC`, `STATIC_SOURCE`, `AUDIO_PROBE`.
Scope: future-owner packet for source-file technical audio risk before Unity import, listening review, Addressables grouping, DSP routing, or runtime mix work.
Hard boundary: no Unity run, no import edit, no prefab edit, no scene edit, no Addressables operation, no mixer edit, no listening proof, no profiler, no runtime proof, and no `Assets/` mutation.

## Mandates Followed

- `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC`: source-file probes do not prove DSPGraph safety, SPSC correctness, audio-thread nonblocking behavior, managed callback exclusion, underrun behavior, or mix output.
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory`: clip admission needs owner, key/group, load phase, active-bank budget, ref-count, release phase, residency evidence, and pressure behavior.
- `QA_Evidence_Text_Filter_Audit`: text, CSV, and probe rows are evidence of source/document presence only.
- `AGENTS.md`, `audio.md`, and `performance.md`: static packets cannot prove runtime audio cost, hot-path allocation behavior, route audibility, import state, or player-facing result.

## Source Risk Summary

Input matrix: `Docs/AssetAudit/AUDIO_FILE_TECHNICAL_PROPERTIES_20260605.md` plus CSV companion.

- Source audio rows scanned: `138`.
- Long rows over `10s`: `98`.
- Multichannel `sfx` / `ui` / `player_loop` rows: `19`.
- `sfx` / `ui` source-rate rows above `22050 Hz`: `35`.
- `music` / `ambient` source-rate rows above `44100 Hz`: `11`.
- Ledger mismatches: `0`.
- Probe failures: `0`.

This reduces future-owner guessing only. It does not prove Unity import settings, compression, force-mono, load type, playback path, active-bank residency, cue priority, warning audibility, or first-20 route mix.

## Blockers Mapped

| Blocker | Static fact | Risk | Required next owner evidence |
|---|---|---|---|
| Long source rows | `98` rows exceed `10s`. | Long beds, music, ambience, and loops can create resident-memory, streaming-buffer, start-latency, and masking risk. | Import readback, class-specific load policy, active-bank budget, Memory Profiler or equivalent residency artifact, runtime MusicDirector/ambient-bank capture, listening notes. |
| Multichannel `sfx` / `ui` / `player_loop` | `19` rows are not mono at source. | 3D SFX and player-layer loops can waste channels/memory or spatialize incorrectly if force-mono policy is not proven. | Import readback for force-mono and platform overrides, role classification, playback route proof, direct-ref owner decision where applicable. |
| High-rate `sfx` / `ui` | `35` rows exceed `22050 Hz` source rate. | Short cue imports may carry unnecessary decode/memory cost unless higher rate is justified by role. | Import readback for sample-rate conversion, compression, quality, load type, latency check, and listening proof for UI/warning/tool clarity. |
| High-rate `music` / `ambient` | `11` rows exceed `44100 Hz` source rate. | Long beds can inflate streaming or resident footprint without proof that the extra rate benefits player-readable audio. | Import readback, music/ambient exception decision, active-bank memory budget, MusicDirector/ambient route capture, listening proof for masking and silence windows. |
| Ledger mismatch risk | `0` mismatches in current probe. | Path/source alignment is mapped, but owner, Addressables group, and route truth remain pending in many rows. | Ledger owner closure, Addressables group/key closure, route-use confirmation, lifecycle proof. |

## Import Readback Gates

Future owner must record Unity import readback for each remediated row:

- asset path and ledger row match;
- class: `music`, `ambient`, `player_loop`, `sfx`, `ui`, or `voice`;
- duration band and loop flag;
- load type;
- compression format and quality;
- imported sample rate;
- source channels and imported channel behavior;
- force-mono state for 3D SFX and spatialized player-layer cues;
- preload/background loading state;
- platform overrides;
- direct serialized ref state where the clip is used by a prefab or profile;
- evidence artifact path and timestamp.

Static policy text cannot close these gates.

## Mono, Rate, And Compression Policy Gates

- Generic one-shot SFX must remain non-streaming.
- Short SFX and UI rows require low-latency import decisions; any retained high-rate or stereo source behavior needs role justification plus import readback.
- `player_loop` rows are not generic SFX. Breath, suit, swim, and first-person movement loops need continuity, latency, and mix priority proof before load policy is admitted.
- Long music and ambience require a settled authority decision before broad import edits. Duration alone cannot force every long row into resident memory.
- Ambient/music rows below the target quality or above the target rate require row-level exception notes, not broad defaults.
- VO stubs do not define final VO policy. Final VO needs localization, subtitle timing, loudness, import, and accessibility proof.

## Listening Proof Gates

Future listening work must prove:

- first exit and shallow route cues remain readable;
- breath, oxygen, pressure, tool, sonar, UI, and warning cues are not masked by music or ambience;
- silence windows exist and are controlled by gameplay state;
- stingers have cooldown and do not spam;
- UI feedback feels like instrument feedback, not decorative app chrome;
- player loops have stable continuity and do not jitter at start;
- placeholder VO rows are not used to judge final speech quality.

Waveform or source metadata alone is not listening proof.

## DSP, Lifecycle, And Addressables Gates

DSP/audio-thread:

- no release-player dependency on managed synthesis, decode, or mix callbacks without explicit waiver and measured artifact;
- no locks, waits, sleeps, spin, scene lookup, string cue lookup, dynamic allocation, or gameplay queries on the audio thread;
- parameters cross by preallocated snapshots or SPSC routes only;
- underrun and corruption telemetry route named for runtime proof.

Lifecycle/Addressables:

- every admitted clip has owner, cue id, Addressables key/group or documented fixed-startup exception;
- handle ref-count and release phase are named;
- active music/ambient bank count is budgeted;
- pressure response defines which secondary beds, prefetches, and long banks shed first;
- direct `Player.prefab` clip refs remain blocked as lifecycle evidence until Unity-safe readback and owner classification are done;
- MusicDirector mixer nulls remain a route blocker unless an owned native/DSP alternate route is documented with proof target.

## Rollback Conditions

Rollback any future import, authority, or route change if:

- import readback contradicts the row policy or source matrix;
- owner, key/group, load phase, release phase, or pressure response stays pending for a promoted row;
- generic one-shot SFX are made streaming;
- player loops stream by duration alone and show start jitter, warning masking, or lifecycle ambiguity;
- MusicDirector output route remains open;
- direct prefab refs remain unclassified after attempted route adoption;
- memory/residency risk rises from broad long-bed admission without compact-lane evidence;
- warning, breath, oxygen, pressure, route, sonar, UI, tool, or threat cues are masked;
- runtime evidence contradicts the static source matrix or exception table.

Rollback target: restore prior import/authority clause, mark affected rows blocked, keep this source packet as static evidence only, and reassign to Audio plus Streaming owners.

## Continuous GlobalQualityWeight Consequences

- Low / compact: one active long ambience or music context unless memory evidence permits more. Breath, warnings, route cues, threat cues, suit/swim loops, and UI feedback outrank decorative beds. Cue breadth, prefetch breadth, secondary layers, reverb cost, and diagnostics shrink smoothly.
- Middle: current context plus likely-next profile and limited ambience support can be admitted after memory, ducking, and player-loop continuity proof.
- High: wider transition prefetch, stronger stinger variety, richer hydrophone detail, and richer reverb/occlusion can be added after cadence, warning-priority, and DSP proof.
- Ultra: dense secondary beds, broader music variety, convolution/reverb, and richer spatial detail can be admitted only after critical cue readability remains proven.

`GlobalQualityWeight` must not change cue truth, cue IDs, source-fact owner, Addressables keys, release order, save identity, DTO layout, warning facts, or playback authority route.

## Regression Model

- CPU: no runtime work changed by this packet. Future risk comes from decode, streaming dispatch, crossfade, stinger scheduling, mixer work, and DSP contention.
- GC: no runtime work changed by this packet. Future risk comes from string cue lookup, direct event routes, managed callbacks, dynamic collections, and logging/UI side effects.
- Memory: no import, residency, or bank state changed by this packet. Future risk comes from resident long beds, duplicate direct refs, expanded prefetch, stereo SFX/UI/player loops, and high-rate short cues.
- Cadence: no runtime cadence changed by this packet. Future risk comes from constant beds, repeated stingers, missing silence windows, player-loop start jitter, and warning masking.
- Correctness: source-file technical risk is mapped only. Import state, lifecycle, mixer routing, DSP route, listening result, and runtime behavior remain unproved.

Final status: `PENDING_VERIFICATION`.
