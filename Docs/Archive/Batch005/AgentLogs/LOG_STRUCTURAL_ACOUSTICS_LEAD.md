# STRUCTURAL_ACOUSTICS_LEAD Log

## 2026-05-13 - Depth Stress Granular Synthesis

Status: PENDING VERIFICATION - blocked by unrelated global compile dependencies.

What was wrong:
- Hull/base crush stress was still presented as health/state decay more than physically audible pressure derivative.
- Existing audio path had procedural metal grain generation, but no explicit `HullStressSignal(PressureDelta)` contract and no authored WAV PCM ingest path.
- Structural stress events did not carry portal attenuation, low-pass, delay, haptic drive, or origin-shift-safe source data.
- Low tier still allowed 4 granular voices, which is not acceptable for MX350 when the prompt says disable granular synthesis.

What was done:
- Added `HullStressSignal` with pressure delta, depth, AUP snapshot, portal transmission, low-pass, and delay.
- Extended `IAudioService` with `QueueHullStressSignal` and implemented it in `SpatialAudioManager`.
- Routed structural stress through existing `AcousticPortalPropagation` when tier allows it; otherwise it falls back to direct procedural routing.
- Added optional `metalStressGrainClip` cold PCM ingest into the existing `NativeArray<float>` metallic grain bank; procedural bank remains deterministic fallback.
- Added `Hecton8.Audio.Synthesis` asmdef and `DepthStressGranularSynthesisJob` with `BurstCompile(FloatMode.Fast)`.
- Fed pressure delta into structural velocity, granular spawn drive, structural snap, impact echo, and haptic envelope.
- Disabled granular voice count on Low/MX350/Unknown; existing pitched hull loop/SPSC path remains the fallback.
- Changed granular crash dump path to `Docs/AgentLogs/Dump_STRUCTURAL_ACOUSTICS_LEAD.bin`.
- Ran Omega polish: removed Burst sample wrap while-loops and made portal routing resolve from stored AUP.

Cinematic cheats used:
- Pressure derivative drives grain density instead of structural resonance simulation.
- Depth pitches grains down toward 0.52x instead of simulating material strain.
- Triangle grain windows fake tearing envelopes.
- Acoustic portal transmission/LPF/delay fakes corridor propagation without custom HRTF.
- Haptics reuse stress/pressure envelope instead of physical vibration simulation.

Exact microseconds saved:
- Low/MX350 granular disable: estimated 40-120 us per 512-sample block versus 4-16 active voices.
- Contract enqueue path: estimated under 8 us per stress event.
- Most-stressed room scan: cooldown path only; estimated under 10 us for normal habitat counts.
- Telemetry write: estimated 2 us per sampled telemetry entry, fixed 300-entry ring.
- Omega wrap-loop removal: small per-sample win, mainly removes worst-case cursor wrap branch cost.

Verification:
- `validate_script` zero diagnostics: `ProceduralAudioEvents.cs`, `HabitatGraphManager.cs`, `PlayerCriticalProceduralAudioRenderer.cs`, `DepthStressGranularSynthesisKernel.cs`.
- `SpatialAudioManager.cs` validated zero diagnostics before Omega AUP polish; after polish the validator regex timed out, while Unity compile reported no audio-path diagnostics.
- Unity compile is blocked by unrelated `EcosystemDirector`/entry-point errors.
- `dotnet build Hecton8.Core.csproj` remains non-authoritative for this repo state; it fails on unrelated asmdef/reference drift.

## 2026-05-13 - Patient Hardening Pass

What was wrong:
- New hull stress signal clamps could preserve NaN values before reaching the event queue.
- AUP-safe routing still converted through absolute `Vector3` in two places, causing avoidable precision loss.
- The isolated synthesis assembly rendered voices but had no standalone deterministic spawn job.
- Spawn accumulation could build backlog after a hitch and dump delayed grains across following frames.

What was done:
- Sanitized hull stress positions, stress, pitch, pressure delta, depth, transmission, cutoff, and delay before clamp/max operations.
- Switched source runtime resolution to `AbsoluteUniversePosition.ToRuntimeFloat3()`.
- Added `DepthStressGranularSpawnJob` with fixed state, `Unity.Mathematics.Random`, pressure/depth density, and bounded voice stealing.
- Clamped spawn accumulator to `VoiceLimit` to avoid delayed burst clutter.

Cinematic cheats used:
- Pressure delta remains the audible proxy for hull load change.
- Depth still controls spawn density and playback pitch instead of simulating structure deformation.
- Voice stealing is intentional: audible intensity beats preserving physically exact grain history.

Exact microseconds saved:
- AUP direct runtime conversion removes one absolute `Vector3` conversion per structural stress route/dispatch.
- Spawn backlog clamp prevents worst-case post-hitch voice arming bursts; expected cap is `VoiceLimit` arms per spawn tick.
- NaN sanitation avoids blackbox dump churn and invalid DSP work; event-time cost is below measurement noise.

Verification:
- `ProceduralAudioEvents.cs`: Unity `validate_script` zero diagnostics.
- `DepthStressGranularSynthesisKernel.cs`: Unity `validate_script` zero diagnostics after spawn job and accumulator clamp.
- Unity console remains blocked by unrelated `SaveSystem/H8BinaryWorldPager.cs` errors and reports no audio-path diagnostics.

## 2026-05-13 - AUP And Burst Guard Recheck

What was wrong:
- Portal-rerouted hull stress signals preserved acoustic path values but recomputed the source AUP from a runtime `Vector3`.
- Renderer dispatch trusted the stored AUP without a local non-finite fallback.
- The isolated spawn job needed shared finite guards and continuous RNG state persistence.

What was done:
- Added a hull stress constructor overload that preserves the authoritative `AbsoluteUniversePosition` while changing transmission, low-pass, and delay.
- Updated portal routing to use that overload and updated renderer dispatch to fall back to the sanitized world position on non-finite AUP resolution.
- Moved finite guards into `DepthStressGranularMath`; spawn and synthesis jobs now sanitize stress, pressure delta, depth, output gain, voice cursor/gain/rate, existing output samples, voice length, and sample indices.
- Stored continuous `Unity.Mathematics.Random.state` and capped spawn delta time plus accumulator backlog to the active voice budget.

Cinematic cheats used:
- Still uses pressure delta and depth as the audible stress proxy.
- Still uses portal transmission/LPF/delay as corridor propagation instead of custom per-source HRTF.
- Voice stealing remains the bounded intensity cheat under full voice load.

Exact microseconds saved:
- AUP-preserving reroute avoids one `FromRuntimePosition` reconstruction per portal-routed stress event.
- Ring cursor wrap replaced modulo with one conditional subtract in the voice search path.
- Spawn delta clamp plus accumulator cap prevents post-hitch bursts beyond `VoiceLimit`; worst-case spawn work remains bounded by the tier voice budget.

Verification:
- Unity MCP is unavailable in this session: `mcpforunity://instances` reports `instance_count: 0`, and `validate_script` returns `no_unity_session`.
- `git diff --check` on touched audio files reports only existing CRLF warnings.
- Static `rg` confirms no `UnityEngine.Random` use in the renderer/synthesis kernel; the synthesis kernel uses `Unity.Mathematics.Random`.
- Manual Roslyn compile of `DepthStressGranularSynthesisKernel.cs` against Unity Mono/ScriptAssemblies passed.
- `dotnet build Hecton8.Core.csproj --no-restore` is still non-authoritative and fails on unrelated missing assemblies/types (`Hecton8.Environment.Fluids`, `Hecton8.Audio.Propagation`, `MacroSwarm`, `H8BinaryWorldPager`, acoustic reflection types) before this isolated synthesis asmdef is represented as a generated csproj.

## 2026-05-13 - Live Renderer Fault Containment Pass

What was wrong:
- The isolated Burst synthesis job was hardened, but the live `PlayerCriticalProceduralAudioRenderer` SOA granular renderer still trusted authored PCM and voice state.
- A corrupt metal-stress WAV sample or invalid voice field could push NaN into a block and rely on telemetry dumping as the first containment step.

What was done:
- Sanitized authored `metalStressGrainClip` samples with `FiniteOrZero` and clamped mono fold-down to [-1,1] before storing into `_metallicGrainBank`.
- Sanitized live granular stress, pressure derivative, depth, impact drive, acceleration pitch wobble, voice cursor, gain, playback rate, and grain length before sample mixing.
- Kept Low/MX350 granular voice count at zero; the guards affect only tiers where granular is enabled.

Cinematic cheats used:
- Authored WAV remains a single source bank, sliced and pitch-stretched instead of multiplying clip count.
- Voice state remains SOA and bounded; bad voices are clamped or expire instead of preserving physical continuity.

Exact microseconds saved:
- Cold PCM sanitation has no frame/audio-thread cost.
- Runtime guard cost is limited to the active voice loop, 0-16 voices. Preventing a NaN dump avoids a far more expensive cold telemetry write path.
- Low/MX350 remains the main saving: zero granular voices, estimated 40-120 us saved per 512-sample block.

Verification:
- Unity active instance: `Hecton8@5898b2fd69afdd2d`.
- `validate_script` zero diagnostics: `DepthStressGranularSynthesisKernel.cs`, `ProceduralAudioEvents.cs`, `SpatialAudioManager.cs`, `GlobalRegistryContracts.cs`.
- `PlayerCriticalProceduralAudioRenderer.cs` still times out in the MCP regex validator because of file size; touched renderer blocks were read back directly.
- Unity console filter for `Audio` returns zero errors. Current compile blockers are unrelated `Core/Memory/GlobalDataVault.cs` errors.

## 2026-05-13 - Authored PCM Coverage Recheck

What was wrong:
- The authored metal-stress clip loader used the full clip frame count for wrapping even when the fixed scratch buffer contained only part of an oversized or high-channel clip.
- A valid source asset could therefore produce silence in part of the two-second grain bank.

What was done:
- Resampled the readable first-two-second PCM source window into the fixed two-second grain bank.
- Folded down up to eight channels through a finite/clamped helper before writing the bank.
- Added an integer overflow guard around `clip.samples * clip.channels` before sizing the readable window.

Cinematic cheats used:
- Kept the one-bank slicing approach. Dense source coverage matters more than storing more clips.

Exact microseconds saved:
- Zero hot-path saving because this is cold ingest.
- Prevents active granular voices from spending their 0-16 voice budget on zero samples when content import is imperfect.

Verification:
- `TryLoadMetalStressGrainClip` and `ReadMetalStressClipMonoFrame` blocks read back after patch.
- `git diff --check -- PlayerCriticalProceduralAudioRenderer.cs` reports only the existing CRLF warning.
- Unity `validate_script` zero diagnostics: `DepthStressGranularSynthesisKernel.cs`, `ProceduralAudioEvents.cs`.
- Unity `PlayerCriticalProceduralAudioRenderer.cs` validator still times out inside the MCP regex engine; Unity console filter for `Audio` returns zero errors.
- Post-resample retry of `PlayerCriticalProceduralAudioRenderer.cs` validation disconnected the plugin session while awaiting result; follow-up audio-filter console remained clean and the synthesis kernel validator still passed.
- Current Unity console blocker is unrelated duplicate method definitions in `HectonUnderwaterVisuals.cs` plus entry-point discovery failure.

## 2026-05-13 - Low Tier Voice Gate Recheck

What was wrong:
- Low/MX350 resolved granular voice budget to zero, but the audio block clamped the parameter back to four voices before rendering.
- This violated the Math LOD requirement and spent CPU on the tier that should use the fallback clip path.

What was done:
- Changed the audio-block clamp minimum from `GranularLowTierVoiceCapacity` to `GranularDisabledVoiceCapacity`.
- Read back the touched block and scanned for remaining `GranularMaxVoiceCount` to `GranularLowTierVoiceCapacity` promotion.

Cinematic cheats used:
- Low tier keeps the pitched hull fallback; granular metal overkill remains reserved for Mid/High/Ultra.

Exact microseconds saved:
- Restores the intended Low/MX350 saving: estimated 40-120 us per 512-sample block versus the accidental four-voice floor.

Verification:
- Static scan found no remaining disabled-to-low-tier promotion for `GranularMaxVoiceCount`.
- `git diff --check` on touched audio files reports only existing CRLF warnings.
- Unity compile remains blocked by unrelated `Core/Memory/GlobalDataVault.cs` and duplicate asmdef reference errors; no audio-path errors surfaced in the general console poll.

## 2026-05-13 - Tier Transition Voice Trim

What was wrong:
- Reducing granular voice budget could leave higher-index voices active but frozen.
- Raising quality later could resume those stale voices, creating old stress grains detached from the current pressure event.

What was done:
- Added `TrimGranularVoicesToBudget()` in the live renderer, called once per hull-stress block before per-sample rendering.
- Added `DepthStressGranularMath.TrimVoicesToBudget()` and called it in both Burst spawn and synthesis jobs.

Cinematic cheats used:
- Tier changes hard-kill excess grains instead of fading them. Predictable budget enforcement matters more than preserving hidden tails.

Exact microseconds saved:
- Prevents stale over-budget voices from re-entering the active mix after tier-up.
- Cost is bounded to at most sixteen scalar voice writes per block/job.

Verification:
- Touched renderer/kernel blocks read back.
- Manual Roslyn compile of `DepthStressGranularSynthesisKernel.cs` against Unity 6000.4.1f1/Burst/Collections/Mathematics passed.
- Unity MCP currently reports no active Unity instance; editor validation is unavailable.

## 2026-05-13 - Portal Acoustic State Composition

What was wrong:
- Portal-rerouted structural stress replaced previous acoustic transmission, low-pass, and delay instead of composing with it.
- Chained routing could make a signal louder, brighter, or earlier.

What was done:
- Multiplied existing transmission by portal transmission.
- Took the lower low-pass cutoff.
- Added non-negative prior and portal delay while preserving source AUP.

Cinematic cheats used:
- Corridor propagation remains portal transmission/LPF/delay, not a custom structural wave simulation.

Exact microseconds saved:
- No hot-path saving; this is event-time correctness.
- Cost is three scalar operations per routed stress event.

Verification:
- `SpatialAudioManager.QueueHullStressSignal` block read back.
- `git diff --check -- SpatialAudioManager.cs` reports only the existing CRLF warning.

## 2026-05-13 - Granular Slot Resolver Zero Guard

What was wrong:
- The granular slot resolver had an internal one-voice floor.
- Caller guards prevented active failure, but the disabled contract was not self-contained.

What was done:
- Changed the resolver clamp minimum to `GranularDisabledVoiceCapacity`.
- Return `-1` immediately when the resolved budget is zero.

Cinematic cheats used:
- None; this is budget enforcement.

Exact microseconds saved:
- No new saving beyond the restored Low/MX350 granular disable.
- Prevents future call-site drift from reintroducing one hidden voice.

Verification:
- `ResolveGranularVoiceSlot()` block read back.
- `git diff --check -- PlayerCriticalProceduralAudioRenderer.cs` reports only the existing CRLF warning.
- Final static sweep: no Unity/System random usage in touched audio/synthesis files, no remaining disabled-budget promotion, and no whitespace errors beyond existing CRLF warnings.
