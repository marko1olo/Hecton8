# Asset Owner 19 - Audio Import Authority Adoption Packet

Status: `PENDING_VERIFICATION`.
Evidence class: `STATIC_DOC`, `STATIC_SOURCE`.
Scope: future-owner packet for adopting audio import/load policy into stable authority after human/controller decision.
Hard boundary: no Unity run, no import edit, no prefab edit, no scene edit, no Addressables operation, no mixer edit, no listening proof, no profiler, no runtime proof, and no `Assets/` mutation.

## Mandates Followed

- `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC`: static documents cannot prove DSPGraph route safety, SPSC correctness, no blocking on the audio thread, underrun behavior, or release callback safety.
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory`: clip load policy must name Addressables owner, key/group, ref-count, active-bank budget, release order, residency evidence, and pressure behavior.
- `QA_Evidence_Text_Filter_Audit`: text and CSV rows are evidence of text/source presence only.
- `AGENTS.md`, `audio.md`, and `performance.md`: no runtime claim from static packets; `GlobalQualityWeight` scales breadth/cadence/residency, not cue truth, owner identity, DTO layout, save identity, or release order.

## What The Draft Inputs Mean

- The 3217 decision brief is conflict analysis plus a route-safe recommendation for a hybrid exception table. It is not adopted policy.
- The 3221 adoption draft is a patch basis only. It does not modify `AGENTS.md`, `audio.md`, `streaming.md`, imports, clips, profiles, prefabs, or groups.
- The exception table and CSV are candidate blocker rows. They reduce guessing but do not promote any exception.
- The listening queue is execution order only. It is not a taste, mix, import, or route proof artifact.
- The profile route matrix is a static risk map for MusicDirector profiles, null mixer fields, repeated stingers, long beds, player-loop risks, and direct prefab refs.
- Asset Owner 08 owns the future direct `Player.prefab` audio ref unwiring route.
- Asset Owner 10 owns the future MusicDirector profile/mixer routing route.

## What Cannot Be Adopted From Docs Alone

- No clip import setting can be changed from static policy text alone.
- No Addressables group, key, label, load mode, catalog, or release route can be created from this packet.
- No direct `Player.prefab` clip ref can be treated as load/lifecycle ownership.
- No MusicDirector profile ref can be treated as mixer output or runtime cadence proof.
- No waveform/static listening note can prove warning audibility, route readability, silence windows, or player-loop continuity.
- No stable authority patch can bypass human/controller decision, route owner naming, proof target naming, and conflict disposition.
- No broad conversion of long music or ambience to resident compressed memory is defensible without compact-lane memory evidence.

## Stable-Doc Patch Prerequisites

Before any stable authority patch:

1. Human/controller chooses the conflict disposition: strict root wording, duration classifier, or hybrid exception table.
2. Patch targets are named exactly: likely `AGENTS.md`, `audio.md`, and the stable streaming/asset lifecycle authority if the policy changes load ownership.
3. The patch text states class, duration band, format/rate, mono/stereo rule, default load type, and exception rule without weakening runtime proof gates.
4. The patch keeps generic one-shot SFX non-streaming.
5. The patch defines `player_loop` separately from generic SFX for breath, suit, swim, warning, and first-person continuity loops.
6. The patch names ownership split: Audio owns cue priority/mix/cadence; Streaming owns handle lifetime, group/key, active-bank admission, pressure response, and release order.
7. If a new or changed global route is introduced, the authority route fields required by `AGENTS.md` must exist: owner, phase, cadence, failure mode, telemetry/proof target, shutdown/release behavior, and review disposition.
8. The patch explicitly says static docs do not prove imports, runtime mix, memory, hot-path allocation behavior, DSP route safety, or listening result.

## Exception Table Promotion Gates

Promote an exception row only when all fields are closed:

- Owner is named and not pending.
- Route role names a first-20 or gameplay function: breath, warning, route cue, threat cue, MusicDirector context, active ambience bank, UI feedback, or VO/audio-log.
- Addressables group/key and load phase are named, or a scoped fixed-startup exception is documented.
- Release phase names owner, ref-count behavior, stop/disable/unsubscribe order, and pressure response.
- Unity import readback records load type, compression, quality, sample rate, force-mono, preload/background settings, loop flags, and platform overrides.
- Active-bank budget records admitted bank count, resident memory target, streaming buffer cost, and pressure shed behavior.
- Runtime mix proof records warning audibility, ducking, silence windows, stinger cooldown, player-loop continuity, and non-masking behavior.
- DSP/audio-thread proof records no locks, waits, sleeps, spin, managed decode/mix/synthesis in release callbacks, or unsafe `OnAudioFilterRead` production dependency.
- Hot-path allocation proof exists for repeated cue changes, warnings, UI feedback, player loops, ambient bank swaps, and MusicDirector transitions.
- Expiry rule exists: exception is re-reviewed when owner route, group/key, clip class, duration, first-20 role, or platform policy changes.

## Direct `Player.prefab` Ref Policy

- Direct serialized `AudioClip` refs are not ownership, not release proof, not Addressables proof, and not playback-route proof.
- Future edits must use Unity-safe prefab workflow. Raw YAML mutation is blocked.
- Every direct ref must be classified as routed clip, player-loop exception, UI short cue exception, fixed startup/core exception, or removal/replacement.
- Retained direct refs require owner, cue id, load phase, release/shutdown phase, playback route, priority/ducking rule, fallback, import readback, lifecycle proof, and hot-path allocation proof.
- `Underwater Ambient.wav` and `dive_splash.wav` remain P0 examples until prefab readback maps the serialized fields to owning components and route decisions.

## MusicDirector Profile And Mixer Proof Gates

- Null `_musicMixerGroup` and `_stingerMixerGroup` fields block MusicDirector route judgment.
- Future owner must assign approved mixer groups through Unity or document an owned native/DSP bypass with owner, phase, cadence, failure mode, readback path, and proof target.
- Static profile references do not prove output, crossfade, silence windows, stinger cooldown, warning priority, or profile entry/exit.
- Long beds need runtime cadence proof before taste judgment.
- Repeated stingers need intentional reuse marker or replacement plan, plus cooldown and warning-priority proof.
- Fallback profile needs legal trigger, exit route, and non-interference rule before it can be used as a hidden support bed.

## Listening Review Gates

Execution order for future listening work:

1. Close MusicDirector route evidence before judging music beds.
2. Prove or reroute direct `Player.prefab` refs before broader ambience judgment.
3. Review breath, suit, swim, and movement loops as player-loop banks, not generic SFX.
4. Review long ambience and music beds for masking, cadence, silence windows, and tension ownership.
5. Review stingers for cooldown, warning priority, and spam suppression.
6. Review UI feedback and VO stubs last; UI needs audibility proof, VO stubs do not define final VO policy.

Required scenes/contexts: first exit, shallow read, photic shelf, storm/tension, warning/UI overlap, breath/suit/swim continuity, silence windows, and profile transitions.

## Memory, GC, DSP, And Lifecycle Evidence Required

- Memory: Memory Profiler or equivalent resident/committed evidence for active music, ambient, player-loop, UI, VO, streaming buffers, total reserved memory, and pressure response on compact lane.
- GC: profiler/GC monitor artifact for repeated playback routes and transitions; static source scans are only preflight.
- DSP: output route proof for DSPGraph/native path or documented transition shim; no release-player dependency on managed synthesis/decode/mix callbacks without explicit waiver and measured artifact.
- Lifecycle: Addressables handle ledger, group/key, ref-count, load phase, release phase, active-bank cap, fallback route, and orphan-handle audit.
- Cadence: MusicDirector capture for current/next profile prefetch, stinger cooldown, silence windows, ambient ducking, warning priority, and transition spacing.
- Correctness: source fact owner exists for every cue that implies pressure, oxygen, hull breach, creature proximity, route signal, tool state, or archive corruption.

## Rejection And Rollback Gates

Reject future adoption or revert the patch if:

- Stable docs still conflict after patch text.
- Owner, group/key, load phase, or release phase remains pending for promoted rows.
- Generic one-shot SFX are allowed to stream.
- Long player loops are streamed by duration alone without latency, prewarm, mix, and lifecycle proof.
- MusicDirector mixer route remains null without an owned DSP/native bypass.
- Direct `Player.prefab` refs remain unclassified.
- Broad long music/ambient residency increases memory risk without compact-lane proof.
- Music, ambience, or stingers mask breath, oxygen, pressure, sonar, UI, tool, or threat cues.
- Placeholder VO rows are used to decide final localization/import policy.
- Runtime evidence contradicts the static policy, import table, or exception row.

Rollback target: restore the prior stable-doc clause, mark affected exception rows blocked, keep static reports as non-authority evidence, and reassign the route to Audio plus Streaming owners.

## Continuous `GlobalQualityWeight` Consequences

- Low / compact: one active ambient/music context unless memory proof permits more. Breath, warning, threat, route, suit, swim, and UI feedback remain admitted before decorative beds. Prefetch breadth, layer count, reverb cost, and diagnostics shrink smoothly.
- Middle: current plus likely-next MusicDirector profile and limited ambience layers can be admitted after memory, ducking, and player-loop continuity proof.
- High: wider transition prefetch, richer stinger palette, stronger hydrophone detail, and richer reverb/occlusion can be added after cooldown, warning-priority, and DSP proof.
- Ultra: dense secondary beds, convolution/reverb, broader music variety, and richer spatial detail can be admitted only after critical cue readability remains proven.

`GlobalQualityWeight` must not change cue truth, cue IDs, owner route, Addressables keys, release order, save identity, DTO layout, warning facts, or source-fact authority.

## Future Owner Action List

1. Secure human/controller policy decision.
2. Draft exact stable-doc patch text with conflict disposition and proof boundary.
3. Close owner/group/key fields for audio ledger rows before import edits.
4. Close MusicDirector mixer route or native/DSP bypass route.
5. Execute direct prefab ref classification and Unity-safe unwiring route.
6. Run import readback, lifecycle, memory, DSP, hot-path allocation, and listening review proof in that order.
7. Promote only exception rows that meet every gate.

## Regression Model

- CPU: no runtime work changed by this packet. Future risk comes from decode, streaming dispatch, crossfade, stinger scheduling, mixer work, and DSP contention.
- GC: no runtime work changed by this packet. Future risk comes from string cue lookup, direct event routes, managed callbacks, dynamic collections, and UI/log side effects.
- Memory: no import or residency changed by this packet. Future risk comes from resident long beds, duplicate direct refs, expanded prefetch, and unowned active banks.
- Cadence: no runtime cadence changed by this packet. Future risk comes from constant beds, repeated stingers, missing silence windows, player-loop start jitter, and warning masking.
- Correctness: this packet clarifies adoption blockers only. Runtime behavior, import state, lifecycle, mixer routing, and listening result remain unproved.

Final status: `PENDING_VERIFICATION`.
