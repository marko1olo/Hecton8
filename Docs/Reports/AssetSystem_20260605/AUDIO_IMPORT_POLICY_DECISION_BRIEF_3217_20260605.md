# Audio Import Policy Decision Brief - Asset Worker 3217 - 2026-06-05

Status: `PENDING HUMAN/CONTROLLER DECISION`
Evidence boundary: `STATIC_DOC` / `STATIC_SOURCE` only.
First-20 route moment: first exit, first surface/shallow read, photic shelf orientation, warning audibility, player breath/movement continuity.

This is not Unity import acceptance. This is not runtime mix acceptance. This is not Addressables residency proof. This is not 0 B/frame proof. This is not audio-thread safety proof.

## Sources Read

- `AGENTS.md`
- `audio.md`
- `streaming.md`
- `performance.md`
- `Docs/AssetAudit/ASSET_SYSTEM_INDEX_20260605.md`
- `Docs/Reports/AssetSystem_20260605/AUDIO_POLICY_CONFLICT_AND_CUE_DISPOSITION_3213_20260605.md`
- `Docs/Audio/audio_asset_ledger.csv`
- `.agents-skills/AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`

Mandates followed:

- `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC`: static policy cannot prove runtime mix, DSPGraph safety, underrun safety, or managed-callback acceptance.
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory`: clip load policy must be tied to Addressables ownership, ref-count, residency, release ledger, memory pressure, and proof.

## Evidence Boundary

- `STATIC_DOC`: authority text and prior report text.
- `STATIC_SOURCE`: CSV ledger fields and scraped import metadata.
- No Unity run.
- No AudioClip `.meta` edit.
- No Addressables group edit.
- No mixer, MusicDirector, prefab, scene, or asset mutation.
- No profiler, Memory Profiler, GCMonitor, audio-thread, or listening-pass proof.

## Conflict Evidence

`AGENTS.md` import/load text, source wording with punctuation normalized to ASCII:

> `[REQ] Audio: Vorbis Q70 ambient/music; ADPCM SFX<2s; Load: Compressed In Memory (ambient/music); Decompress On Load SFX<0.5s; Force To Mono all 3D SFX (-50% mem); 44100 Hz music; 22050 Hz SFX.`

`AGENTS.md` streaming/SFX boundary, source wording with punctuation normalized to ASCII:

> `[FORBID] Streaming SFX (latency) - streaming music only.`

`audio.md` does not define AudioClip import load types. It defines audio behavior and runtime proof constraints:

> `Audio systems must obey:`
> `- no managed allocation in hot paths;`
> `- pooled events and voices;`
> `- data-driven cue IDs;`
> `- priority and virtualization;`
> `- SPSC/ring buffers where mandated;`
> `- low-cadence environmental parameter updates;`
> `- mix snapshots tied to gameplay state;`
> `- no string cue lookup in runtime hot paths.`

`audio.md` release boundary:

> `Release audio must not synthesize, decode, mix, lock DataVault views, acquire mutation guards, run Stopwatch, or touch gameplay-owned state inside Unity managed OnAudioFilterRead(float[] data, int channels) callbacks.`

Root `streaming.md` assigns residency/load ownership and pressure behavior, but does not contain the exact duration table in the file read for this brief. Its relevant authority is:

> `Runtime asset access routes through owned async loading, residency ledgers, priority queues, and explicit release.`

and:

> `GlobalQualityWeight may scale prefetch distance, speculative load slots, HLOD residency radius, mip bias, decorative biome density, audio bank breadth, VFX support residency, and diagnostic ledger depth. It must not change save identity, gameplay truth, asset ownership, release order, collision truth, or required near-field survival assets.`

The exact duration-based AudioClip rule appears in `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`:

> `Streaming clips (duration > 10s): AudioClip.loadType = AudioClipLoadType.Streaming`
> `Short SFX (duration <= 2s):        AudioClip.loadType = AudioClipLoadType.DecompressOnLoad`
> `Medium (2-10s):                    AudioClip.loadType = AudioClipLoadType.CompressedInMemory`

Static conflict summary:

- `AGENTS.md` says ambient/music use Vorbis Q70 and ambient/music should load as `Compressed In Memory`, while allowing `streaming music only`.
- STRM duration policy sends all clips longer than 10 seconds to `Streaming`, which includes most music, most ambient, and several player loops.
- `audio.md` is not a load-type authority, but it blocks acceptance without runtime priority, mix, hot-path, and DSP/thread proof.
- Root `streaming.md` is residency authority, so any adopted load policy must preserve owner keys, ref-count, pressure behavior, and release ledgers.

## Current Static Ledger Surface

`Docs/Audio/audio_asset_ledger.csv` current class/load surface:

| Class | Rows | Current load types | Duration range |
|---|---:|---|---|
| `music` | 84 | `Streaming=84` | 26.436s to 479.96s |
| `ambient` | 12 | `CompressedInMemory=4`; `Streaming=8` | 63.336s to 193s |
| `player_loop` | 5 | `CompressedInMemory=1`; `Streaming=4` | 6.428s to 60s |
| `sfx` | 30 | `CompressedInMemory=8`; `DecompressOnLoad=22` | 0.375s to 2s |
| `ui` | 5 | `CompressedInMemory=3`; `DecompressOnLoad=2` | 0.172s to 1.673s |
| `voice` | 2 | `CompressedInMemory=1`; `DecompressOnLoad=1` | 1.341s to 1.341s |

Affected classes:

- `music`: all rows are currently `Streaming` and conflict with a strict reading of AGENTS ambient/music `Compressed In Memory`. Current Q0.7 and 44100 Hz align with AGENTS quality/sample-rate target in static metadata.
- `ambient`: eight rows are `Streaming` and four are `CompressedInMemory`; several streaming ambient rows are Q0.45, below the Q70 target. This is the highest direct policy conflict.
- `player_loop`: not generic SFX after 3213. Breath/suit/swim loops need priority and latency policy because they are player-layer continuity cues. Duration table permits Streaming for long rows, but first-person loop latency/stall risk may require exceptions.
- `sfx`: no generic SFX row is currently `Streaming`. Short SFX mostly align with `DecompressOnLoad`; several 2s-class rows are `CompressedInMemory` and need Unity import readback before edits.
- `ui`: current rows are short and should behave like tactile instrument feedback. `CompressedInMemory` on short UI rows may be acceptable only if latency, memory, and 0 B/frame proof passes; otherwise duration rule pushes them toward `DecompressOnLoad`.
- `voice`: current VO rows are placeholders only. They are short stubs and cannot determine final VO import policy, localization timing, or subtitle behavior.

## Route-Safe Options

### Option A - Strict AGENTS Compressed-In-Memory For Ambient/Music

Policy:

- Ambient and music use Vorbis Q70.
- Ambient and music load as `CompressedInMemory`, except explicitly allowed streaming music.
- SFX under 0.5s use `DecompressOnLoad`; 3D SFX force mono.

Tradeoffs:

- Pros: highest consistency with root `AGENTS.md` literal text; avoids disk-stream jitter for ambient if memory budget permits; simpler latency story for active beds.
- Cons: converts long ambient/music to resident compressed audio memory unless exempted; risks compact lane RAM pressure; conflicts with STRM duration rule; likely bad for 4h07m music library if broadly resident; still needs Addressables bank gating or it is not shippable.
- Route risk: high memory pressure during first-20 if ambient/music banks are not strictly one-active-bank and MusicDirector-gated.

Reject condition:

- Any broad conversion of all music/ambient to resident `CompressedInMemory` without Memory Profiler proof and Addressables residency budget.

### Option B - Duration-Based Streaming Rule

Policy:

- Clips over 10s use `Streaming`.
- Clips 2-10s use `CompressedInMemory`.
- Clips up to 2s use `DecompressOnLoad`.
- Generic SFX must not stream.

Tradeoffs:

- Pros: strongest alignment with STRM mandate; protects compact memory; fits current static state for all 84 music tracks and most long ambient/player loops; clean rule for automated import audits.
- Cons: too blunt for player breath/swim loops and first-person critical continuity; long ambient Q0.45 remains below AGENTS Q70 target; can trade memory risk for disk/IO/decode risk; does not by itself prove MusicDirector, ducking, warning audibility, or audio thread safety.
- Route risk: streaming player-layer loops or first-exit ambient without prewarm/buffer proof can create first-person continuity defects.

Reject condition:

- Treating `duration > 10s` as sufficient for player-critical breath/warning/loop cues without latency, prewarm, and mix proof.

### Option C - Hybrid Exception Table

Policy:

- Use duration-based rule as the default import/load classifier.
- Add class-specific route exceptions for first-20 player-critical and route-critical cues.
- Keep `AGENTS.md` quality/sample-rate/mono requirements.
- Record exceptions in stable docs and the audio ledger before import mutation.

Tradeoffs:

- Pros: preserves compact memory through streaming for long music and non-critical long ambience; protects first-person/player-critical loops from blind streaming; matches streaming ownership and audio behavior constraints; gives import owners an auditable rule.
- Cons: requires an exception ledger and Unity proof before edits; more complex than one table; must avoid exception creep.
- Route risk: manageable if exceptions are few, named, owner-backed, and Memory Profiler proven.

Reject condition:

- Exceptions based on taste only, with no first-20 route role, priority, owner, or proof target.

## Recommendation

Adopt Option C: hybrid exception table.

Recommended policy for first-20 HECTON-8:

- `music`: default `Streaming`, Vorbis Q70, 44100 Hz. MusicDirector must prefetch by profile/context and keep silence windows. Do not convert the whole music library to `CompressedInMemory`.
- `ambient`: default `Streaming` for loops over 10s, Vorbis Q70 target. Allow `CompressedInMemory` only for first-exit/shallow critical bed loops if Memory Profiler proves the active-bank budget and runtime mix proves no warning masking.
- `player_loop`: classify separately from SFX. Breath, suit, and swim loops are player-layer continuity cues. Default by duration, but allow `CompressedInMemory` for critical first-person loops where streaming latency, start jitter, or disk pressure would damage control/readability. Require one-owner exception rows.
- `sfx`: no generic SFX streaming. Duration rule stands: <=2s `DecompressOnLoad`, 2-10s `CompressedInMemory`, force mono for 3D SFX unless a specific stereo UI/non-3D exception is documented.
- `ui`: default short UI feedback to `DecompressOnLoad` unless import readback/memory proof keeps `CompressedInMemory` with no latency or heap risk. UI audio is instrument feedback, not music.
- `voice`: placeholder stubs remain non-authoritative. Final VO policy should use duration, localization, subtitle timing, memory budget, and playback latency proof.

This recommendation does not decide final imports. It defines a route-safe policy candidate for human/controller adoption before import edits.

## GlobalQualityWeight Consequences

GlobalQualityWeight must scale breadth and residency, not cue truth.

Low / compact:

- One active ambient bank max under pressure.
- Music streams; prefetch only current/next MusicDirector profile if memory proof allows.
- Critical breath/warning/player-loop cues stay low-latency and prewarmed.
- Generic SFX remain non-streaming and mono where 3D.
- No loss of warning, breath, threat, route, or UI feedback truth.

Middle:

- More ambient support layers can stay admitted if warning ducking and memory pressure remain green.
- Music profile prefetch can hold current plus likely next context.
- Player loops retain priority over music beds.
- Q70 ambient/music target remains; lower quality ambient imports need review before promotion.

High:

- Wider MusicDirector transition prefetch.
- Richer ambience and hydrophone/detail layers.
- More active reverb/occlusion support only after audio-thread and mix proof.
- Still no constant music bed that hides route, warning, or threat cues.

Ultra:

- Dense secondary beds, stronger reverb/convolution, and richer stinger layering are allowed only after critical cue audibility remains proven.
- Streaming remains valid for long music/library breadth; saved memory can buy richer mix layers rather than resident everything.
- Exceptions stay route-owned; GlobalQualityWeight does not change cue ownership, Addressables keys, DTO layout, save identity, or release order.

## Proposed Stable-Doc Patch Text

Do not edit these docs until controller/human adoption. Candidate text only.

### Candidate `AGENTS.md` Patch

Replace the current audio import/load sentence with:

```text
[REQ] Audio import policy: ambient/music target Vorbis Q70; music 44100 Hz; SFX 22050 Hz unless owner proof requires higher; all 3D SFX force mono. Default load type follows duration: clips >10s Streaming, 2-10s CompressedInMemory, <=2s DecompressOnLoad. Generic SFX must never stream. Route-critical first-person/player-loop or first-exit ambient exceptions may use CompressedInMemory only when named in the audio ledger with owner, route role, active-bank budget, Unity import readback, Memory Profiler proof, runtime mix proof, 0 B/frame proof, and audio-thread safety proof.
[FORBID] Streaming generic one-shot SFX. Streaming music and long non-critical ambience are allowed through Addressables-owned MusicDirector/ambient-bank routes with prefetch, ref-count, release ledger, and memory-pressure proof.
```

### Candidate `audio.md` Patch

Add under `## 8. Performance And Implementation`:

```text
### 8.2 Audio Import And Load Policy

AudioClip load type is owned jointly by Audio and Streaming. The default classifier is duration-based: >10s Streaming, 2-10s CompressedInMemory, <=2s DecompressOnLoad. Class behavior overrides blind duration only when the exception protects a player-readable route cue.

Music defaults to Streaming through MusicDirector profile prefetch. Ambient loops over 10s default to Streaming through an active-bank route. Player breath, suit, swim, warning, and other first-person continuity loops must be classified as `player_loop`, not generic SFX, and may use CompressedInMemory only with a ledgered owner exception and proof.

Generic one-shot SFX must not stream. UI feedback must stay low latency and may use DecompressOnLoad for short instrument cues. Voice policy follows final localization duration, subtitle timing, memory, and playback proof.
```

### Candidate `streaming.md` Patch

Add under `## Memory Pressure` or after `## GlobalQualityWeight Scaling`:

```text
## Audio Residency

AudioClip residency follows the project audio import table and the Addressables release ledger. Long music and non-critical long ambience may stream, but player-critical warning, breath, suit, movement, and route-continuity loops require explicit owner classification before import changes.

Under pressure, audio bank breadth can shrink continuously with GlobalQualityWeight and memory pressure: decorative layers first, secondary ambience next, music prefetch breadth next. Breath, warning, threat, route, and UI feedback cues remain admitted before decorative beds. Streaming owns handle lifetime, prefetch admission, active bank limits, release order, and residency evidence; Audio owns cue priority, mix, ducking, and player-readable behavior.
```

### Candidate Ledger Rule

Add to `Docs/Audio/audio_asset_ledger.csv` governance notes or the next stable audio routing spec:

```text
Every audio row must declare class, route use, owner, Addressables group/key, intended load type, import proof status, and exception status. Exception status must be one of: NONE, PLAYER_CRITICAL_LOW_LATENCY, FIRST_EXIT_AMBIENT_BED, WARNING_OR_UI_CRITICAL, LOCALIZATION_PENDING, PLACEHOLDER_BLOCKED. Exception rows require proof before import mutation.
```

## Proof Required Before Import Edits

Required proof packet before any mass AudioClip import/load mutation:

- Unity import readback for representative and changed clips: load type, compression format, quality, sample rate, force mono, preload/background settings, platform overrides.
- Addressables owner proof: group, key, ref-count, load mode, release ledger, active-bank limits, prefetch route, and failure fallback.
- Memory Profiler/residency proof: active music/ambient/player-loop resident memory, committed memory, streaming buffers, total reserved memory, pressure response, and compact-lane behavior.
- Runtime MusicDirector/mix proof: current/next profile prefetch, silence windows, stinger cooldowns, warning ducking, ambient ducking, no constant-bed masking.
- 0 B/frame proof: GCMonitor/profiler capture through first-exit/shallow route with cue changes, warnings, UI feedback, breath/swim loops, and music transitions.
- Audio thread safety proof: no blocking, no managed decode/mix/synthesis in release callbacks, no `OnAudioFilterRead` production route unless waived with DSP profiler proof, no underruns over a measured run.
- Listening pass: first exit, first surface/shallow read, photic shelf, storm/tension, warning audibility, UI click, breath/suit/swim continuity.
- Regression model: CPU, GC, memory, cadence, correctness, and failure behavior before/after import edits.

## Non-Claims

- No runtime/mix/import acceptance is made by this brief.
- No current clip import is declared correct.
- No current MusicDirector route is declared production-ready.
- No Addressables route is declared resident-safe.
- No Q0.45 ambient row is accepted against Q70 target.
- No player-loop streaming row is accepted until first-person continuity proof exists.
- No VO placeholder row is accepted for final localization, subtitles, timing, or loudness.

## Final Disposition

The safest policy route is a hybrid exception table: duration-based default, AGENTS quality targets retained, generic SFX streaming forbidden, music streaming allowed through MusicDirector, long non-critical ambience streaming allowed through active-bank residency, and player-critical exceptions ledgered and proved before import edits.

Final authority still requires human/controller adoption in stable docs. Until then, all import edits remain blocked by policy conflict and proof gaps.
