# LOG_AUDIO_GRANULAR_SYNTH

## 2026-05-11 - DSP_ACOUSTIC_LEAD / AUDIO_GRANULAR_SYNTH

What was wrong:
- Hull pressure groan path was not a true overlapping granular scheduler. It used a small loop-style source window and repeated deterministic pressure-creak logic, which cannot produce enough variation under structural stress.
- Raw grain source capacity was not the requested `44100 * 2` persistent native buffer.
- No dedicated 16-lane SOA granular voice state existed for pressure groans.
- No Burst-compatible granular block job existed in the audio job helper file.
- No granular black-box dump existed for non-finite DSP state.

What was done:
- Changed `_metallicGrainBank` to persistent `NativeArray<float>` capacity `88200` with exact-size guard.
- Added fixed SOA voice arrays: active, elapsed, length, start, seed, cursor, playback rate, gain.
- Added deterministic LCG grain arming with 10-50 ms grain length selection.
- Added parabolic Hanning fake: `1 - x*x`; Low/MX350 tier uses linear window.
- Added depth pitch lowering, structural derivative pitch lift, and acceleration-based doppler wobble.
- Reused the existing physics impact event bridge to inject high-pitch grain clusters without consuming shared `GlobalSignals` queues.
- Added `PlayerCriticalBufferJobs.GranularSynthesisBlockJob`, Burst `IJob`, over native SOA voice buffers.
- Added `NativeArray<GranularAudioTelemetryEntry>[300]` black-box telemetry and `Docs/AgentLogs/Dump_AUDIO_GRANULAR_SYNTH.bin` dump on non-finite state.
- Wrote `Docs/Tasks/RECON_AUDIO_GRANULAR_SYNTH.md`; `AudioSource.PlayOneShot` scan under `Assets` found no offenders.

Cinematic cheats used:
- Metal stress is faked with deterministic micro-grain scrambling, not physical resonance simulation.
- Parabolic envelope replaces true trigonometric Hanning.
- Doppler is acceleration wobble from existing Rigidbody velocity delta, not full propagation physics.
- Low-tier Math LOD caps voices to 4 and uses linear window.
- Telemetry uses a 64-sample bitmask stride; non-finite samples still dump immediately.

Exact microseconds saved:
- Verified exact runtime savings: PENDING VERIFICATION. Unity compile is blocked by unrelated non-audio errors, so profiler/Burst timing cannot be trusted yet.
- Static savings applied: telemetry normal write rate reduced by 63/64 (~98.4%) after polish; Low tier avoids up to 12 voice scans per sample versus Ultra.

Verification:
- `PlayerCriticalBufferJobs.cs` MCP validation: 0 diagnostics.
- Unity compile: BLOCKED BY DEPENDENCY. Latest editor log groups show non-audio failures including `SubmarineFluidDynamics`, `HectonVisorUberPostFeature`, `MantaScooter`, `CombatDamageRuntime`, `DroneFleetManager`, `SaveBinaryStorage`, `SargassumMicroFaunaBoids`, and `AbyssalThermalManager`.
- Latest compile groups do not report errors in `PlayerCriticalProceduralAudioRenderer.cs` or `PlayerCriticalBufferJobs.cs`.

Status: PENDING VERIFICATION.

## 2026-05-12 - Honest R&D AAA Addendum

What was wrong:
- The saturated granular voice allocator could steal the oldest active pressure grain for routine structural texture. The replacement grain starts at zero envelope, but the old grain is hard-cut, so the pressure bed can click or collapse under dense stress.

What was done:
- Added `GranularImpactStealTailSamples = 96`.
- Changed `ArmGranularVoice` to request high-priority stealing only for impact/high-pitch clusters.
- Changed `ResolveGranularVoiceSlot` so normal stress grains drop when the pool is full; impact clusters may steal and prefer a nearly finished tail before the oldest lane.

Cinematic cheats used:
- Dropped excess routine grains instead of simulating another fade-out voice pool. The ear notices hard cuts more than it notices a missing random micro-grain under a full pressure bed.
- Collision clusters keep priority because impact masking hides the necessary steal better than slow pressure texture does.

Exact microseconds saved:
- Exact profiler savings: PENDING VERIFICATION.
- Static estimate: 0 B hot-path GC delta; no new arrays; no new per-sample branch. Added only scheduler-time integer tail comparisons during voice arming.

Verification:
- `git diff --check` on `PlayerCriticalProceduralAudioRenderer.cs`: pass; only Git line-ending warning.
- Targeted audio audit: no `UnityEngine.Random`, `Mathf.Tanh`, `new float[]`, `float[]`, or `AudioSource.PlayOneShot` in changed granular renderer/job files.
- Unity refresh/compile: editor recovered and became ready, but compile remains BLOCKED BY DEPENDENCY with 8 non-audio errors in `SpectrumSystem`, `HectonVisorUberPostFeature`, `AbyssalThermalManager`, `DroneFleetManager`, and `CombatDamageRuntime`. No audio error was visible in the retrieved console errors.

Status: PENDING VERIFICATION.

## 2026-05-12 - Honest R&D AAA Addendum B

What was wrong:
- The impact-priority granular path still had one remaining hard-cut path: if the pool was full and no tail was near finished, it fell back to stealing the oldest active voice.
- Black-box `ActiveVoices` telemetry counted lanes before expired-lane cleanup, which could mark the pool as saturated when stale lanes were about to be cleared.

What was done:
- Removed the oldest-lane fallback from `ResolveGranularVoiceSlot`.
- Impact clusters now steal only inactive lanes or tails with `<= 96` samples remaining; otherwise the extra cluster grain is dropped.
- Moved `activeVoiceCount++` after elapsed/length validation so telemetry counts actually mixed voices.

Cinematic cheats used:
- Tail-only reuse substitutes for a full fade-retirement pool. It preserves the player-facing pressure bed without adding another voice class.
- Dropping excess impact grains is accepted because the existing impact clang/transient layers still carry collision presence.

Exact microseconds saved:
- Exact profiler savings: PENDING VERIFICATION.
- Static estimate: avoids up to 3 abrupt voice overwrites per saturated impact; 0 B hot-path GC delta; no new buffers; no new per-sample work.

Verification:
- `git diff --check` on changed audio/docs files: pass; only Git LF/CRLF warning.
- Targeted audit: no `UnityEngine.Random`, `Mathf.Tanh`, `new float[]`, `float[]`, `AudioSource.PlayOneShot`, `oldestVoiceIndex`, or `oldestElapsed` in changed granular audio files.
- Unity full compile remains BLOCKED BY DEPENDENCY from non-audio domains; Burst emission is still not honestly verifiable.

Status: PENDING VERIFICATION.

## 2026-05-12 - Honest R&D AAA Addendum C

What was wrong:
- Compile verification was still polluted by two audio-domain warnings from obsolete `GetInstanceID()` usage in the leviathan acoustic impulse source ID path.

What was done:
- Replaced `hit.Rigidbody.GetInstanceID()` and `hit.Transform.GetInstanceID()` with `EntityId.ToULong(...GetEntityId())` conversions.
- Re-ran Unity refresh/compile and console checks.

Cinematic cheats used:
- No new simulation. This is source-ID hygiene so post-mortem audio events resolve against Unity 6 entity IDs instead of deprecated object-instance IDs.

Exact microseconds saved:
- Runtime savings: effectively neutral.
- Verification value: warning noise removed from the audio renderer, making future compile checks less ambiguous.

Verification:
- `git diff --check`: pass; only Git LF/CRLF warning.
- Targeted audit: no `GetInstanceID()`, `UnityEngine.Random`, `Mathf.Tanh`, `new float[]`, `float[]`, `AudioSource.PlayOneShot`, `oldestVoiceIndex`, or `oldestElapsed` in changed granular audio files.
- Unity console errors after refresh: non-audio blockers only: `SargassumMicroFaunaBoids.PrewarmQueue`, `SaveBinaryStorage` Burst `catch` filter, and one MCP regex-timeout entry.
- Unity warning console no longer reports the audio obsolete `GetInstanceID()` warnings.

Status: PENDING VERIFICATION.

## 2026-05-12 - Honest R&D AAA Addendum D

What was wrong:
- Granular max voice count followed scalability tier changes immediately. Under hardware-tier flicker this can audibly pump the pressure bed between sparse and dense grain fields.

What was done:
- Added `GranularVoiceUpgradeHysteresisSeconds = 2.5f`.
- Added main-thread `ResolveGranularMaxVoiceCountWithHysteresis`.
- Downgrades remain immediate for frame protection; upgrades require 2.5 stable seconds before enabling denser voice counts.

Cinematic cheats used:
- Audio density stability is treated as the belief channel. Low tier is allowed to drop voices immediately; High/Ultra visual-overkill density returns only after stable headroom.

Exact microseconds saved:
- Exact profiler savings: PENDING VERIFICATION.
- Static impact: 0 B hot-path GC delta; no new NativeArrays; no audio per-sample work; one main-thread scalar hysteresis check per Tick.

Verification:
- `git diff --check`: pass; only Git LF/CRLF warning.
- Targeted audit confirmed the hysteresis path exists and did not reintroduce forbidden audio patterns.
- MCP refresh timed out during one compile wait, then `read_console` recovered and reported 7 non-audio errors: `NativeArenaArrayEditTests` missing Burst symbols and `SaveBinaryStorage` Burst `catch` filter. `Editor.log` also recorded an earlier audio renderer timestamp change during a Csc pass, so another compile pass is required after external blockers settle.

Status: PENDING VERIFICATION.

## 2026-05-12 - Honest R&D AAA Addendum E

What was wrong:
- Low/MX350 Math LOD reduced granular voice count correctly, but final output was scaled by raw voice ratio. Four voices meant 25% output against Ultra, making the cheap tier too thin.
- Grain sampling helpers still used unbounded defensive wrap loops for cursor/source normalization in the live renderer and Burst job.

What was done:
- Added `GranularMinimumVoiceDensityOutputScale = 0.5f`.
- Replaced raw voice-ratio gain with a 0.5..1.0 density compensation curve: Low/MX350 = 62.5%, Mid = 75%, High = 87.5%, Ultra = 100%.
- Replaced unbounded grain sampler wrap loops with bounded clamp guards in `PlayerCriticalProceduralAudioRenderer.cs` and `PlayerCriticalBufferJobs.cs`.

Cinematic cheats used:
- Low tier buys CPU by reducing voice scans, not by deleting the audible hull-pressure fantasy.
- Invalid cursor/source state collapses to a bounded edge sample instead of a generic wrap simulation.

Exact microseconds saved:
- Exact profiler savings: PENDING VERIFICATION.
- Static impact: +2 scalar block-setup ops for density compensation; removed two unbounded wrap-loop sites from each granular sampler; 0 B hot-path GC delta.

Verification:
- `git diff --check`: pass; only Git LF/CRLF warnings.
- Targeted audit: no `UnityEngine.Random`, `Random.Range`, `System.Random`, `Mathf.Tanh`, `new float[]`, `float[]`, `AudioSource.PlayOneShot`, or old `while` grain wrap loops in changed granular audio files.
- MCP `validate_script` passed for `PlayerCriticalBufferJobs.cs` with 0 diagnostics.
- MCP `validate_script` timed out on the large renderer file; not counted as clean validation.
- Unity compile completed but remains BLOCKED BY DEPENDENCY with 2 non-audio errors in `Assets/_Project/Scripts/Input/UserOptionsPersistence.cs` for missing `HectonPersistentPathPolicy`.

Status: PENDING VERIFICATION.

## 2026-05-12 - Honest R&D AAA Addendum F

What was wrong:
- High/Ultra granular pressure beds still used the same linear grain interpolation as Low/MX350.
- That left top-tier audio density richer in voice count, but not smoother in pitch-shifted micro-grain reads.

What was done:
- Added `GranularHighQualityInterpolationVoiceThreshold = 12`.
- Added bounded 4-tap Hermite/Catmull-Rom grain sampling to the live renderer.
- Gated Hermite reads to High/Ultra voice caps only; Low/MX350 and Mid stay on linear reads.
- Added `HermiteInterpolation` flag and matching Hermite sampler to `PlayerCriticalBufferJobs.GranularSynthesisBlockJob`.

Cinematic cheats used:
- This is not physical metal resonance. It is a better interpolation fake for the same deterministic micro-grains.
- High-tier spends extra taps on smoother pressure texture; Low-tier buys frame time with linear reads and fewer voices.

Exact microseconds saved:
- Exact profiler savings: PENDING VERIFICATION.
- Static impact: Low/MX350 unchanged; High/Ultra pay two extra grain taps and Catmull-Rom math per active voice sample; 0 B hot-path GC delta.

Verification:
- `git diff --check`: pass; only Git LF/CRLF warnings.
- Targeted forbidden audit: no `UnityEngine.Random`, `Random.Range`, `System.Random`, `Mathf.Tanh`, `new float[]`, `float[]`, `AudioSource.PlayOneShot`, or old unbounded grain wrap loops in changed granular audio files.
- Expected Hermite symbols present in renderer and Burst job.
- MCP `validate_script` passed for `PlayerCriticalBufferJobs.cs` with 0 diagnostics.
- Unity compile completed after a long wait but remains BLOCKED BY DEPENDENCY with non-audio errors in `Assets/_Project/Tests/Editor/NativeArenaArrayEditTests.cs` for missing Burst symbols and `Assets/_Project/Scripts/SaveBinaryStorage.cs` Burst `catch` filter `BC1007`.

Status: PENDING VERIFICATION.
