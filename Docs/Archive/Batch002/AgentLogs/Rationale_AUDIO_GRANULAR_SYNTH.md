# Rationale_AUDIO_GRANULAR_SYNTH

## 2026-05-11 - Bootstrap

Problem: The active batch path was not the root `C:\hades\Hecton8\CURRENT_BATCH.md`; the current prompt lives under `Docs/Tasks/CURRENT_BATCH.md`.
Solution: Used PowerShell raw file read plus a strict XML regex for `AUDIO_GRANULAR_SYNTH`, then discarded neighboring prompts from the working scope.
Rejected Alternatives: Root-only batch read failed; chat-only task memory violates batch isolation; MCP/basic file reader was unnecessary and risked truncation.
Scalability potential: Low/Middle/High/Ultra work must stay tiered; the audio system will treat procedural pressure groans as a deterministic fake, scaling from 4 voices on toaster hardware to richer overlapping voices on top hardware.
Hardware Impact: Initial parsing is cold-path only. Runtime design target remains 0 B/frame GC and audio DSP under the MX350/i3 budget.

Problem: The task demands granular pressure audio, but physical hull simulation truth is unnecessary for the player belief channel.
Solution: Use a cinematic audio fake: structural integrity/depth/impact scalars drive deterministic grain selection, pitch, envelope, and soft clipping.
Rejected Alternatives: Real-time metal deformation simulation, per-contact resonance truth, or default HRTF convolution are too expensive and not required for gameplay correctness.
Scalability potential: Low = 4 simple voices and linear envelope; Middle = 8-12 voices with parabolic envelope; High = 16 voices plus stronger overlap; Ultra = richer pitch modulation and longer grain clusters while preserving the same data contract.
Hardware Impact: Expected low-end gain versus repeated WAV one-shots is reduced managed call churn and no hot-path GC; exact microseconds remain PENDING VERIFICATION until profiling.

## 2026-05-11 - Tasks 1-5 Granular Core

Problem: The old pressure groan path used a small loop-style grain selector and power-of-two wrap mask, which could not satisfy the requested two-second raw grain bank or overlapping deterministic voices.
Solution: Rebased the metallic grain bank to `NativeArray<float>` capacity `44100 * 2` with persistent allocation and added fixed SOA voice arrays for active flag, elapsed sample, source start, length, seed, cursor, pitch, and gain. The scheduler runs from the existing player-critical audio renderer and consumes structural hull stress already resolved from `GlobalRegistry` / hull breach read models.
Rejected Alternatives: Managed `float[]` transfer, `AudioSource.PlayOneShot`, and per-voice classes were rejected because they allocate or hide lifecycle cost; real-time modal metal simulation was rejected because the mandate asks for pressure belief, not physics truth.
Scalability potential: Low = four voices, linear triangle window; Middle = eight voices and parabolic window; High = twelve voices with denser overlap; Ultra = sixteen voices with impact clusters and richer pitch spread.
Hardware Impact: Expected low-end gain is no managed sample buffer churn and no random allocation; exact microseconds remain PENDING VERIFICATION until Unity profiler evidence exists.

Problem: Grain selection needed random variation without nondeterminism or UnityEngine API calls on the audio path.
Solution: Added deterministic LCG progression (`1664525u + 1013904223u`) plus multiply-high range mapping for 10-50 ms source windows and voice pitch/gain variation.
Rejected Alternatives: `UnityEngine.Random`, `System.Random`, and time-seeded selection were rejected because they are non-deterministic and unsafe for DSP reproducibility.
Scalability potential: Same seed contract scales across all tiers; tiers only change voice count and window shape, not the deterministic identity of events.
Hardware Impact: LCG/range mapping is integer-only hot-path work; exact microseconds remain PENDING VERIFICATION.

Problem: True Hanning windows require trigonometric work per voice sample and waste DSP budget on low-end silicon.
Solution: Implemented the cinematic fake window: `1 - x*x` over the grain length, with a linear crossfade fallback for Low/MX350 tiers.
Rejected Alternatives: `math.cos`, FFT-domain smoothing, and sample-accurate convolution envelopes were rejected as too expensive for this perceptual cue.
Scalability potential: Low uses linear; Middle/High/Ultra use parabolic. The saved budget buys more overlapping pressure voices.
Hardware Impact: Expected MX350/i3 benefit is replacing trig with multiply/abs; exact microseconds remain PENDING VERIFICATION.

Problem: The batch explicitly asks for a Burst audio job, while the project currently uses a native producer-thread audio architecture rather than Unity DSPGraph `IAudioOutputJob`.
Solution: Added `PlayerCriticalBufferJobs.GranularSynthesisBlockJob`, a Burst `IJob` that renders fixed SOA granular voices into a `NativeArray<float>` output block with parabolic/linear windowing and rational soft clipping. The live renderer keeps the producer-thread equivalent to avoid a new dependency on unavailable DSPGraph types.
Rejected Alternatives: Introducing a hard dependency on absent DSPGraph packages or moving game-world queries into the DSP job was rejected. `OnAudioFilterRead` managed synthesis was also rejected.
Scalability potential: The same SOA buffers can be executed either by the producer-thread equivalent or the Burst job, preserving Low/Middle/High/Ultra voice caps.
Hardware Impact: Burst compatibility is PENDING VERIFICATION; expected gain is SIMD-friendly SOA layout and no per-voice objects.

## 2026-05-11 - Tasks 6-10 Audio Thread Contract

Problem: Depth and acceleration must change the grain character without allocating or resampling offline clips.
Solution: Grain cursors advance by fractional playback rate. `depthParam` pushes pitch down toward 0.52x, structural stress derivative pushes it up during active damage, and Rigidbody velocity delta already cached as thruster acceleration supplies a small doppler-fake wobble.
Rejected Alternatives: Pre-baked pitch variants, AudioMixer pitch automation, and full doppler/HRTF calculations were rejected because they add managed asset/state cost or spend CPU on realism the player cannot inspect.
Scalability potential: Low keeps the same math but fewer voices; Ultra spends saved cycles on denser overlapping grains and stronger pitch spread.
Hardware Impact: Fractional index progression is one add plus clamp/wrap per active voice sample; exact microseconds remain PENDING VERIFICATION.

Problem: Collision audio must trigger immediately without coupling the DSP worker to physics objects or destructively stealing from global `ImpactSignal` queues used by other systems.
Solution: Reused the existing `IPhysicsImpactEventListener` / `PhysicsImpactSignal` bus path, which is fed by the physics impact queue, then copied impact intensity into the renderer's fixed SPSC-style `ImpactAudioEvent` bridge. The granular renderer injects high-gain, high-pitch clusters on that impulse.
Rejected Alternatives: Direct `GlobalSignals.TryDequeueImpact` in audio would consume shared events; direct physics component references from the audio worker would violate thread ownership.
Scalability potential: Low caps the cluster inside four voices; High/Ultra can use the full 16-voice SOA pool.
Hardware Impact: Impact enqueue is fixed-capacity and allocation-free after cold setup; exact microseconds remain PENDING VERIFICATION.

Problem: DSP state needs deterministic crash evidence, not after-the-fact guesses.
Solution: Added a fixed `NativeArray<GranularAudioTelemetryEntry>[300]` circular black box. Each granular sample records sample index, stress, derivative, depth, impact, mixed output, active voice count, voice limit, and flags. On NaN/non-finite detection it dumps `Docs/AgentLogs/Dump_AUDIO_GRANULAR_SYNTH.bin`.
Rejected Alternatives: Managed lists, strings, or deferred Debug.Log sampling were rejected because they allocate or drop state under failure.
Scalability potential: Same telemetry shape across Low/Middle/High/Ultra; only voice limit changes.
Hardware Impact: One fixed native write per granular sample is a suspicious cost but buys post-mortem traceability mandated by the black box rule; exact microseconds remain PENDING VERIFICATION.

## 2026-05-11 - Tasks 11-15 LOD, Recon, Verification

Problem: A single 16-voice granular path would waste cycles on low-end devices and still underuse high-end hardware.
Solution: `ResolveGranularMaxVoiceCount` maps Low/MX350/Unknown to 4 voices, Mid to 8, High to 12, Ultra to 16. Low tier also uses the linear envelope instead of the parabolic window.
Rejected Alternatives: Balanced one-size tuning and runtime allocations per quality tier were rejected.
Scalability potential: Low = stable and cheap; Middle = denser but still conservative; High = richer overlap; Ultra = visual-overkill audio density without changing data contracts.
Hardware Impact: Low/MX350 avoids 12 voice scans versus Ultra; exact microseconds remain PENDING VERIFICATION.

Problem: The prompt bans managed float transfers for this granular engine and requires PlayOneShot reconnaissance.
Solution: Granular buffers and voice lanes are `NativeArray<T>` only; the changed audio renderer/job files contain no `float[]`, `new float[]`, `UnityEngine.Random`, `Mathf.Tanh`, or `AudioSource.PlayOneShot`. Wrote `Docs/Tasks/RECON_AUDIO_GRANULAR_SYNTH.md`; project scan found no `AudioSource.PlayOneShot` offenders under `Assets`.
Rejected Alternatives: Migrating unrelated `HectonMusicDirector` managed music arrays was rejected as outside this granular synth prompt; consuming unrelated queues would increase integration risk.
Scalability potential: Native-only grain data can feed producer-thread or Burst job execution across all tiers.
Hardware Impact: Zero hot-path managed audio sample transfer in the granular engine; exact microseconds remain PENDING VERIFICATION.

Problem: Burst/Unity compile verification cannot be completed while other agents leave core/domain compile errors active.
Solution: Ran Unity refresh/compile attempts, MCP validation for `PlayerCriticalBufferJobs.cs`, editor log scans, `diff --check`, and targeted text audits. Audio-specific transient telemetry type error was fixed; latest compile groups show non-audio blockers only.
Rejected Alternatives: Editing unrelated world/core/player files would violate domain boundaries and risk sabotage; claiming Burst verified would be false.
Scalability potential: Once external compile blockers are removed, the Burst granular job can be verified without additional design changes.
Hardware Impact: Compile/Burst microsecond claims remain PENDING VERIFICATION until the project compiles and Burst emits the job.

## 2026-05-11 - OMEGA POLISH CHANGES

Problem: The first black-box implementation wrote granular telemetry every audio sample. That obeyed traceability but spent too much on a failure-only safety system.
Solution: Added a 64-sample bitmask stride (`sampleIndex & 63`) so normal telemetry writes are decimated while NaN/non-finite states still dump immediately. This is the required 1D/bitmask-style cheat from the Polish Mandate.
Rejected Alternatives: Keeping per-sample writes or removing black-box telemetry. Per-sample writes are wasteful; removing telemetry violates the black-box rule.
Scalability potential: Same trace format across tiers; Low/MX350 pays 1/64th of the normal telemetry write rate while Ultra keeps enough post-mortem state for pressure-groan debugging.
Hardware Impact: Estimated normal telemetry write reduction: ~98.4% versus per-sample writes. Exact microseconds remain PENDING VERIFICATION.

Problem: Cursor wrap used a loop where the pitch clamp guarantees at most one wrap in normal operation.
Solution: Replaced `while (cursor >= length)` with a single branch in the live renderer and Burst job. This is a deterministic audio fake, not a general arbitrary-cursor normalizer.
Rejected Alternatives: General modulo/while wrap in the hot path. It is more robust for corrupted data but slower for the controlled pitch range.
Scalability potential: All tiers benefit; high tiers spend the saved branch-loop risk on more voices.
Hardware Impact: Removes unbounded loop risk from active voice sample advancement. Exact microseconds remain PENDING VERIFICATION.

Final Git Diff:
- `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs`: persistent 88200-sample metallic grain bank, 16-lane SOA granular scheduler, depth/impact/doppler pitch controls, Math LOD voice caps, black-box telemetry dump, helper envelopes/LCG.
- `Assets/_Project/Scripts/Audio/PlayerCriticalBufferJobs.cs`: Burst `GranularSynthesisBlockJob` over native SOA voice state and output buffer.
- `Docs/Tasks/RECON_AUDIO_GRANULAR_SYNTH.md`: `AudioSource.PlayOneShot` reconnaissance, no offenders found under `Assets`.
- `Docs/Tasks/Status_AUDIO_GRANULAR_SYNTH.md`: task evidence and dependency-blocked compile status.
- `Docs/AgentLogs/Rationale_AUDIO_GRANULAR_SYNTH.md`: decision log and polish record.

Status: PENDING VERIFICATION, not VERIFIED MASTER GRADE, because Unity compile is blocked by unrelated non-audio errors and Burst emission cannot be proven yet.

## 2026-05-12 - Honest R&D Addendum

Problem: The granular scheduler could overwrite the oldest active voice whenever the 16-lane pool was saturated. The new grain starts at zero envelope, but the stolen grain disappears instantly, which can produce a click or thin out the pressure bed under heavy stress.
Solution: Changed voice slot resolution so normal structural texture drops excess grain requests when all lanes are occupied. Collision/high-pitch impact clusters remain high priority and may steal only a nearly finished tail (`<= 96` samples remaining); if every lane is still audibly alive, the extra cluster grain is dropped.
Rejected Alternatives: A full retiring-voice fade pool was rejected because it requires extra SOA lanes and more hot-path mix work for a rare saturation edge. Standard Unity `AudioSource` voice virtualization was rejected because the prompt demands native zero-GC procedural synthesis.
Scalability potential: Low/MX350 keeps four lanes and avoids saturation clicks by dropping inaudible excess grains; Middle/High/Ultra retain denser overlap, with impacts still punching through as visual-overkill audio events.
Hardware Impact: Normal saturated scheduler now avoids overwrite churn and new state. Cost is two integer tail comparisons per occupied lane during voice arming only, not per mixed sample. Exact microseconds remain PENDING PROFILING.

Problem: Any R&D change after the original polish pass risks silently regressing the zero-GC audio contract.
Solution: Re-ran targeted audits against changed audio files for managed arrays, random APIs, `Mathf.Tanh`, and `AudioSource.PlayOneShot`; no offenders were found. Ran Unity refresh/compile; editor recovered after reconnect and reported only non-audio compiler errors.
Rejected Alternatives: Claiming verification from local text inspection alone was rejected. Fixing visor/world/construction/combat compile blockers was rejected as outside `AUDIO_GRANULAR_SYNTH`.
Scalability potential: The anti-click policy keeps the same Low/Middle/High/Ultra voice caps and data layout, so no tier-specific memory contract changes are introduced.
Hardware Impact: 0 B hot-path GC delta. Unity/Burst status remains PENDING VERIFICATION because global compile is still blocked outside audio.

## 2026-05-12 - Honest R&D Addendum B

Problem: The previous impact-priority path still had an oldest-lane fallback. In a saturated pressure bed, a collision could still hard-cut up to three active grains if no near-finished lane existed.
Solution: Removed the oldest fallback. `ResolveGranularVoiceSlot` now returns a lane for high-priority impact only when an inactive lane exists or the shortest remaining tail is within `GranularImpactStealTailSamples` (`96` samples). Otherwise the extra cluster grain is dropped; existing impact clang and non-granular transient layers still carry collision presence.
Rejected Alternatives: Keeping the oldest fallback was rejected because it preserves density by risking clicks. Adding a retiring fade pool was rejected because it adds SOA memory and per-sample mixing for a rare saturation edge.
Scalability potential: Low/MX350 remains stable with 4 lanes and fewer forced cuts; Middle/High/Ultra keep dense overlap without turning impact into a click source under full load.
Hardware Impact: Removes overwrite churn during saturated impacts; adds no buffers, no managed allocations, and no per-sample branch. Exact microseconds remain PENDING PROFILING.

Problem: Black-box telemetry counted active voices before clearing expired lanes. That made saturation flags less trustworthy in the exact cases where post-mortem evidence matters.
Solution: Moved `activeVoiceCount++` after elapsed/length validation, so telemetry counts voices that are actually mixed for the current sample.
Rejected Alternatives: Leaving the count approximate was rejected because crash traces must explain the state, not exaggerate it.
Scalability potential: Same telemetry format across Low/Middle/High/Ultra; only the count semantics become tighter.
Hardware Impact: Runtime cost is neutral; the increment moved after an existing branch. Better telemetry reduces diagnostic time, not frame time. Unity/Burst status remains PENDING VERIFICATION.

## 2026-05-12 - Honest R&D Addendum C

Problem: The latest Unity compile log showed two warnings inside `PlayerCriticalProceduralAudioRenderer.cs`: obsolete `GetInstanceID()` calls in the leviathan acoustic impulse source ID path. They were not fatal, but they added audio-domain noise to an already dependency-blocked compile.
Solution: Replaced those calls with the project-standard Unity 6 `GetEntityId()` path and converted through `EntityId.ToULong`, matching existing renderer ID usage.
Rejected Alternatives: Suppressing the warning or ignoring it was rejected because warning noise hides real audio regressions during blocked integration windows. Broader non-audio `GetEntityId()` sweeps were rejected as outside this prompt.
Scalability potential: Same source ID data contract across Low/Middle/High/Ultra; stable entity IDs improve telemetry/source tracking without tier-specific branches.
Hardware Impact: Runtime cost is equivalent and hot-path GC remains 0 B; compile warning reduction improves verification signal, not frame time.

Problem: Full Burst/Unity verification is still gated by other domains, but the audio pass needed fresh evidence after every code edit.
Solution: Ran Unity refresh/compile and console reads. Current errors are non-audio: `SargassumMicroFaunaBoids.PrewarmQueue`, `SaveBinaryStorage` Burst `catch` filter, and an MCP regex-timeout log entry. Warning console no longer reports the audio obsolete-ID warnings.
Rejected Alternatives: Claiming the granular job is verified was rejected. Fixing world/save/MCP issues was rejected as outside `AUDIO_GRANULAR_SYNTH`.
Scalability potential: The granular synth remains unchanged by this warning cleanup; the same Math LOD and tail-only steal behavior apply across tiers.
Hardware Impact: Audio status remains PENDING VERIFICATION; exact microseconds remain PENDING PROFILING until global compile and profiler runs are clean.

## 2026-05-12 - Honest R&D Addendum D

Problem: Granular voice count followed `GlobalRegistry.ScalabilityTier` immediately. If the scalability dictator flickers under load, the hull pressure bed can audibly pump between 4/8/12/16 voices.
Solution: Added `GranularVoiceUpgradeHysteresisSeconds = 2.5f` and `ResolveGranularMaxVoiceCountWithHysteresis`. Downgrades remain immediate to protect frame time; upgrades only apply after the requested richer tier remains stable for 2.5 seconds.
Rejected Alternatives: Immediate upgrades were rejected because they violate the State Hysteresis Mandate and can create audible density pops. Delaying downgrades was rejected because frame protection has priority on MX350/i3.
Scalability potential: Low/MX350 drops to 4 voices immediately when required. Mid/High/Ultra still get richer pressure texture, but only after stable headroom proves the player will not hear LOD churn.
Hardware Impact: Adds two scalar fields and one main-thread branch path per Tick; no audio-thread allocation and no new per-sample work. Exact microseconds remain PENDING PROFILING.

Problem: Compile verification remained unstable after the edit because Unity timed out during one MCP refresh wait.
Solution: Used local audits, `Editor.log` tail, and a recovered MCP `read_console` result. `git diff --check` passed except LF/CRLF warning; current relevant compiler blockers are non-audio (`NativeArenaArrayEditTests` missing Burst symbols and `SaveBinaryStorage` Burst `catch` filter). `Editor.log` also recorded that the audio renderer timestamp changed during an earlier Csc pass, so clean verification needs another pass after external blockers settle.
Rejected Alternatives: Claiming the granular hysteresis is build-verified was rejected. Editing memory-arena tests or save storage was rejected as outside `AUDIO_GRANULAR_SYNTH`.
Scalability potential: Hysteresis strengthens the Low/Middle/High/Ultra contract by preventing rapid tier oscillation.
Hardware Impact: 0 B hot-path GC delta. Unity/Burst status remains PENDING VERIFICATION.

## 2026-05-12 - Honest R&D Addendum E

Problem: Low/MX350 Math LOD reduced granular voices to 4 but also scaled final granular output linearly by voice ratio. That made the cheapest tier only 25% as loud as Ultra before the master hull mix, so weak devices paid less CPU but also lost the pressure fantasy.
Solution: Added `GranularMinimumVoiceDensityOutputScale = 0.5f` and changed final granular density gain to a 0.5..1.0 compensation curve. Low/MX350 now run 4 voices but keep 62.5% output density, Mid 75%, High 87.5%, Ultra 100%.
Rejected Alternatives: Keeping the linear 4/16 gain was rejected because it makes toaster hardware audibly flat. Giving Low more voices was rejected because voice scans are the actual CPU knob. A limiter-only fix was rejected because it hides clipping after losing the intended source energy.
Scalability potential: Low = fewer voices but still credible hull pressure; Middle = denser texture; High = richer overlap; Ultra = full voice bed and impact clusters without changing the data contract.
Hardware Impact: Adds two scalar multiplies/adds at block setup, not per voice. 0 B hot-path GC delta. Exact microseconds remain PENDING PROFILING.

Problem: Grain sampling helpers still used unbounded `while` loops to normalize cursor/source indices even though the voice cursor contract already keeps normal values within one grain. Corrupted native state could turn a defensive wrap into a hot-path stall.
Solution: Replaced those defensive loops in the live renderer and Burst granular job with bounded clamp guards. Valid cursors still interpolate the same window; invalid negative/oversized cursors clamp to a safe edge sample.
Rejected Alternatives: Keeping generic wrap loops was rejected because a corrupt cursor should degrade to a bounded click-safe sample, not spend unbounded CPU. Integer modulo was rejected because the valid path does not need it and negative modulo semantics add extra correction.
Scalability potential: All tiers get deterministic bounded sampling; Ultra spends saved stall risk on richer voice density rather than general-purpose index normalization.
Hardware Impact: Removes two unbounded loop sites from the granular hot path in both renderer and Burst job. Normal-path microseconds remain PENDING PROFILING; worst-case stall risk is lower.

## 2026-05-12 - Honest R&D Addendum F

Problem: After low-tier density compensation, High/Ultra still read granular windows with the same linear interpolation as MX350. That leaves saved high-tier CPU unused and can produce rougher micro-grain edges during pitch-shifted pressure beds.
Solution: Added a gated 4-tap Catmull-Rom/Hermite grain sampler. The live renderer enables it only when the hysteresis-stabilized voice cap is `>= 12` (High/Ultra). Low/MX350 and Mid keep the cheaper linear sampler. The Burst granular job now has an explicit `HermiteInterpolation` flag for equivalent scheduled execution.
Rejected Alternatives: Always-on Hermite was rejected because Low/MX350 must not pay extra taps. A new high-quality grain bank was rejected because it would add memory and authoring churn. FFT/resampling quality was rejected because the player belief channel only needs smoother pressure texture, not physical restoration.
Scalability potential: Low = 4 linear voices; Mid = 8 linear/parabolic voices; High = 12 voices with Hermite grain reads; Ultra = 16 voices with Hermite reads and full overlap density.
Hardware Impact: Low/MX350 cost unchanged. High/Ultra adds two extra grain taps and Catmull-Rom math per active voice sample. Exact microseconds remain PENDING PROFILING; 0 B hot-path GC delta.

Problem: Compile verification after the Hermite pass was delayed by a long Unity script compile and still failed outside the audio domain.
Solution: Waited for editor readiness, then read the console. Current errors are non-audio `NativeArenaArrayEditTests.cs` missing Burst symbols (`BurstCompileAttribute`, `FloatMode`, `FloatPrecision`) plus `SaveBinaryStorage.cs` Burst `catch` filter `BC1007`. `PlayerCriticalBufferJobs.cs` MCP validation passed with 0 diagnostics and local audits found no forbidden audio patterns.
Rejected Alternatives: Fixing Native Arena test assembly references or save-storage Burst code was rejected as outside `AUDIO_GRANULAR_SYNTH`. Claiming the audio path is build-verified was rejected because global compile is still red.
Scalability potential: The new Hermite gate strengthens tier separation without changing public audio interfaces.
Hardware Impact: Unity/Burst status remains PENDING VERIFICATION.
