# STRUCTURAL_ACOUSTICS_LEAD Rationale

Status: PENDING VERIFICATION

## Initial Mandate Selection

Problem: Structural acoustics crosses audio DSP, habitat stress, pressure damage, AUP, haptics, and crash telemetry.

Solution: Scope ownership to the audio synthesis runtime and use contracts/signals for habitat, pressure, haptics, and portal propagation. Use fixed buffers, NativeArray/struct payloads, and Burst-compatible kernels.

Rejected Alternatives: Direct HabitatGraphManager references from DSP, scene searches for stressed rooms, AudioSource.PlayOneShot creaking, and multiple authored creak clips. Those patterns violate decoupling, zero-GC, or low-tier audio-thread budget.

Scalability potential: Low uses a pitched fallback clip/low grain density and no expensive routing. Middle uses bounded granular density. High adds richer routing and higher grain concurrency. Ultra can spend saved CPU on denser grains and stronger modulation.

Hardware Impact: On i3/MX350, disabling full granular on Low is estimated to save 40-120 us per 512-sample block versus 16-32 active grains. Main-thread update target stays below 0.01 ms with fixed queue/snapshot updates.

## Decision 0 - State Files Before Code

Problem: Batch protocol requires persistent checklist and rationale before marking progress.

Solution: Created Status_STRUCTURAL_ACOUSTICS_LEAD.md and this rationale log before code edits.

Rejected Alternatives: Chat-only status. It is non-durable under context compression and violates the task protocol.

Scalability potential: No runtime impact.

Hardware Impact: No runtime impact.

## OMEGA POLISH CHANGES

Problem: Final audit required removal of honest/slow math and a hard check for hot-path allocations or domain leaks.

Solution: Replaced the synthesis kernel wraparound `while` loops with a floor/rcp wrap. Re-routed structural portal solving from the stored AUP snapshot instead of the original runtime position. Re-ran stable-random scan: no `UnityEngine.Random` usage in the renderer or synthesis kernel; granular voices use `HashUInt`/`NextLcg`.

Rejected Alternatives: Keeping while-loop wrapping in a Burst sample reader, trusting runtime position after origin shifts, or adding a separate custom HRTF path. The existing portal and binaural infrastructure already owns spatial presentation.

Scalability potential: Low/MX350 takes the zero-voice granular return and pitched fallback. Mid keeps bounded grain density. High/Ultra can spend the saved low-tier cycles on acoustic portal attenuation, richer grain density, and low-pass/delay propagation.

Hardware Impact: Removing wrap loops prevents pathological sample cursor cost; expected win is small per sample but removes a bad branch pattern from the Burst kernel. Low-tier granular disable remains the real saving: estimated 40-120 us per 512-sample block.

Cinematic Cheats Used: AUP-positioned stress emitters instead of physical hull deformation; pressure derivative as a grain-density proxy; depth-to-0.52x pitch lie; triangle grain window; acoustic portal transmission/LPF as corridor propagation fake; haptic envelope derived from pressure/stress instead of simulating structure resonance.

Final Git Diff: Scoped status shows modified `PlayerCriticalProceduralAudioRenderer.cs`, `ProceduralAudioEvents.cs`, `GlobalRegistryContracts.cs`, `SpatialAudioManager.cs`, this rationale log, status log, plus new `Assets/_Project/Scripts/Audio/Synthesis/`. `git diff --stat` for tracked scoped files reports 6 files, 994 insertions, 131 deletions; this includes concurrent pre-existing modifications in large shared files, so review by touched hunks and status notes, not raw file-level ownership.

Verification: `validate_script` is zero diagnostics for `ProceduralAudioEvents.cs`, `HabitatGraphManager.cs`, `PlayerCriticalProceduralAudioRenderer.cs`, and `DepthStressGranularSynthesisKernel.cs`. `SpatialAudioManager.cs` validated zero diagnostics before the final AUP-routing polish; after that polish, the MCP validator itself times out on regex, while Unity compile reports only unrelated `EcosystemDirector` / entry-point errors and no audio-path diagnostics. `dotnet build Hecton8.Core.csproj` remains non-authoritative and red due unrelated asmdef/reference drift.

## Decision 1 - Contracted Hull Stress Signal

Problem: Habitat pressure stress needed to drive audio without concrete audio singleton calls or per-frame polling.

Solution: Added `HullStressSignal` and `IAudioService.QueueHullStressSignal`, with `SpatialAudioManager` forwarding through the existing zero-GC procedural event lane. The payload carries pressure delta, depth, portal attenuation, low-pass, delay, and an AUP snapshot.

Rejected Alternatives: Direct DSP reads of `HabitatGraphManager`, `FindObjectOfType`, and one-off `AudioSource.PlayOneShot` calls. Those options either couple domains or allocate/control Unity audio sources from stress code.

Scalability potential: Low ignores portal solving and granular density. Middle/High/Ultra can use `AcousticPortalPropagation` to make the same event travel through rooms and corridors.

Hardware Impact: Signal enqueue is estimated under 8 us on i3/MX350. AUP snapshot adds fixed payload bytes, not per-frame allocation.

## Decision 2 - Grain Source And DSP Path

Problem: The existing code had a procedural NativeArray grain bank, but the prompt required one authored two-second metal-stress WAV path.

Solution: Added an optional `metalStressGrainClip` cold-path PCM loader into the existing `NativeArray<float>` bank. If no readable clip is assigned, the deterministic procedural bank remains the fallback. Added isolated `Hecton8.Audio.Synthesis` asmdef and `DepthStressGranularSynthesisJob` with `FloatMode.Fast`.

Rejected Alternatives: Fifty authored clips, managed `List` grain voices, and Unity random grain positions. The runtime path keeps fixed SOA voice arrays, LCG/hash seeds, and no managed hot-path allocation.

Scalability potential: Low/MX350 disables granular voices and uses the existing pitched hull loop/SPSC path. Mid uses 8 voices, High 12, Ultra 16 plus portal attenuation.

Hardware Impact: Low tier saves roughly 40-120 us per audio block by returning before granular rendering. WAV import is cold only; hot path stays NativeArray reads.

## Decision 3 - Habitat Localization And AUP Safety

Problem: Base stress sounds were centered or edge-based, not explicitly tied to the most stressed room or origin-shift-safe data.

Solution: `HabitatGraphManager` now resolves the most stressed active room from joint shear, compression, and flood levels before emitting low-tier analytical feedback. Edge buckling sends compression delta and depth. `StructuralStressAudioInfo` stores `AbsoluteUniversePosition`, and the renderer resolves runtime position at dispatch.

Rejected Alternatives: Scene transform searches, raw runtime-position caches, and direct room audio emitters. Those fail AUP safety or add scene traversal.

Scalability potential: Low gets one positioned fallback rumble. High/Ultra can route the same AUP through acoustic portals and spend extra CPU on richer spatial presentation.

Hardware Impact: Most-stressed scan runs only on the existing cooldown path; expected cost is under 10 us for normal room counts.

## Decision 4 - Compile Gate Blocked Outside Audio Domain

Problem: Unity compile cannot currently complete because unrelated project errors exist outside this agent's domain.

Solution: Ran Unity compile/console and targeted script validation. `ProceduralAudioEvents.cs`, `SpatialAudioManager.cs`, `HabitatGraphManager.cs`, `PlayerCriticalProceduralAudioRenderer.cs`, and `DepthStressGranularSynthesisKernel.cs` each validate with zero diagnostics. Unity console is blocked by unrelated errors in `GlobalSignals.cs`, `InputDispatcher.cs`, `GlobalRegistry.NoOpInputService`, and earlier database/world terrain files.

Rejected Alternatives: Editing Core/Input/World systems from this audio task or reverting other agents' changes. That would violate domain ownership and dirty-worktree rules.

Scalability potential: No runtime impact until unrelated compile errors are cleared.

Hardware Impact: No runtime impact.

## Decision 5 - Patience Pass Hardening

Problem: Recheck found three concrete quality risks: pressure payload clamps could preserve NaN values, AUP-to-runtime conversion used an unnecessary absolute `Vector3` truncation, and the isolated synthesis assembly rendered voices but did not own deterministic pressure/depth spawning.

Solution: Sanitized all new hull-stress scalar and position inputs before clamp/max operations. Switched stress source runtime resolution to `AbsoluteUniversePosition.ToRuntimeFloat3()`. Added `DepthStressGranularSpawnJob` with fixed spawn state, `Unity.Mathematics.Random`, bounded pressure/depth spawn density, and accumulator clamping to the active voice budget.

Rejected Alternatives: Trusting Unity clamp methods to absorb NaN, casting AUP double absolute coordinates through `Vector3`, or leaving spawn ownership only in the monolithic renderer. Those choices weaken determinism and reuse.

Scalability potential: Low can keep `VoiceLimit` at zero for immediate no-op. Mid/High/Ultra can share the same spawn contract with voice budgets 8/12/16 and higher pressure density.

Hardware Impact: NaN sanitation is event-time only. `ToRuntimeFloat3()` removes avoidable precision loss without adding allocation. Spawn accumulator clamp prevents hitch recovery from causing a multi-frame audio CPU burst.

## Decision 6 - AUP Preservation And Burst Finite Guards

Problem: Recheck found the portal-rerouted hull stress constructor recomputed `SourceAup` from a runtime `Vector3`, and the new spawn job needed finite guards that were available only inside the synthesis job scope. Rehashing the saved random state every tick also made the stream deterministic but less clean as a continuous PRNG sequence.

Solution: Added a `HullStressSignal(in AbsoluteUniversePosition, Vector3, ...)` overload so portal routing can preserve the original AUP while updating transmission/LPF/delay. Added renderer fallback to `WorldPosition` if a corrupted AUP resolves non-finite. Moved finite helpers into `DepthStressGranularMath`, clamped delta time to 250 ms for spawn debt control, normalized bad ring cursor/accumulator state, clamped corrupt voice lengths/sample indices to the grain bank, and persisted `Unity.Mathematics.Random.state` directly.

Rejected Alternatives: Rebuilding AUP from camera-relative runtime coordinates after every portal solve, duplicating finite helper code inside each job, or letting hitch recovery spawn more than the active voice budget. Those choices add precision drift, maintenance traps, or audio-thread burst risk.

Scalability potential: Low/MX350 still gets zero active granular voices. Mid/High/Ultra use the same deterministic state machine with larger voice budgets and richer acoustic routing, without changing payload shape.

Hardware Impact: Preserving AUP avoids one `FromRuntimePosition` conversion on rerouted stress events. Finite guards are branch-light and event/block scoped. Delta clamp plus accumulator clamp caps post-hitch spawn work to `VoiceLimit`, protecting the 512-sample block budget on i3/MX350.

## Decision 7 - Active Renderer PCM And Voice Sanitization

Problem: The isolated synthesis kernel was hardened, but the live `PlayerCriticalProceduralAudioRenderer` SOA granular path still trusted authored PCM samples and voice fields more than it should. A corrupt WAV sample, NaN stress derivative, bad cursor, or invalid playback rate could push NaN through `mixed` and force the blackbox path instead of producing controlled silence.

Solution: Sanitized `metalStressGrainClip` PCM samples with `FiniteOrZero` and clamped mono fold-down to [-1,1] before writing the `NativeArray<float>` grain bank. Sanitized stress, stress derivative, depth, impact drive, pitch wobble, voice gain, cursor, playback rate, and grain length before granular mixing. Existing blackbox dumping remains the final fault path.

Rejected Alternatives: Trusting authored content, relying on `FastSoftClip` to absorb NaN, or only hardening the new isolated kernel. The live renderer is the shipped path, so it needs the same fault containment.

Scalability potential: Low/MX350 still bypasses granular voices. Mid/High/Ultra get the richer authored bank without letting a bad asset poison the block.

Hardware Impact: PCM sanitation is cold-path import only. Runtime voice guards are scalar clamps in a 0-16 voice loop and cheaper than dumping telemetry or propagating invalid samples across the block.

## Decision 8 - Authored PCM Read Window Coverage

Problem: The metal-stress WAV cold loader originally wrapped target samples against the full `AudioClip.samples` count even though the fixed managed scratch buffer may contain only a prefix of an oversized or high-channel-count clip. A first fix avoided tail silence, but 48 kHz or longer-than-two-second source clips could still fail to fill the exact two-second 44.1 kHz grain bank with the intended source window.

Solution: Compute the readable frame count from the actual sample window copied into `_vwsClipManagedScratch`, guard the clip sample-count multiply, limit ingestion to the readable first two seconds of source audio, fold down up to eight channels, and linearly resample that source window into the fixed grain bank.

Rejected Alternatives: Requiring strict mono/stereo 44.1 kHz two-second source assets, wrapping raw source frames directly, or falling back to the procedural bank whenever the clip exceeds scratch capacity. Those options make authored content brittle and push avoidable work back onto content discipline.

Scalability potential: Low/MX350 still bypasses granular synthesis. Mid/High/Ultra keep a dense authored source bank even if content is delivered as multi-channel, 48 kHz, or longer-than-needed source audio.

Hardware Impact: No runtime DSP cost; the fix is cold ingest only. It prevents silent-bank regions that would waste active grain voices on zeros.

## Decision 9 - Low-Tier Granular Voice Gate

Problem: Recheck found that `ResolveGranularMaxVoiceCount()` correctly returned zero for Low/MX350/Unknown, but the audio-block render path clamped the published parameter back up to `GranularLowTierVoiceCapacity` before calling the granular renderer. That silently re-enabled four granular voices on the exact tier that should bypass them.

Solution: Changed the block-local `GranularMaxVoiceCount` clamp minimum to `GranularDisabledVoiceCapacity`, preserving a zero-voice request through to `RenderStructuralGranularVoices()`.

Rejected Alternatives: Keeping four "cheap" granular voices on Low or special-casing the renderer return later. The prompt explicitly requires disabling granular synthesis on MX350, and preserving zero at the parameter boundary is the simplest enforceable gate.

Scalability potential: Low/MX350 now takes the intended pitched fallback/SPSC path. Mid/High/Ultra still receive 8/12/16 voice budgets through the existing hysteresis path.

Hardware Impact: Restores the intended 40-120 us saving per 512-sample block on Low/MX350 by avoiding the four-voice floor.

## Decision 10 - Tier Transition Voice Trim

Problem: After fixing the zero-voice gate, existing granular voices could still remain active above the current budget if the tier dropped from High/Ultra to Mid/Low. Those stale voices were inaudible while the budget was lower, but could resume later when the tier rose again.

Solution: Added block-level renderer trimming and equivalent Burst kernel trimming. Voices at indices greater than or equal to the active voice limit are deactivated once per block/job, with cursor/gain reset for the live renderer.

Rejected Alternatives: Letting old voices expire only when their index is rendered again or clearing voices inside every per-sample early return. The first creates stale audio on tier-up; the second wastes work on the audio path.

Scalability potential: Low/MX350 clears all active granular voices when disabled. Mid keeps the first eight, High the first twelve, Ultra the full sixteen.

Hardware Impact: Worst-case trim is sixteen scalar writes once per audio block/job. That is cheaper than resuming stale voices or evaluating silent grains after a tier drop.

## Decision 11 - Portal Acoustic State Composition

Problem: Portal rerouting replaced `HullStressSignal` acoustic transmission, low-pass, and delay with the newly resolved path values. If an upstream source had already attenuated or delayed the signal, rerouting could accidentally make it louder, brighter, or earlier.

Solution: Compose acoustic state monotonically: multiply transmission, use the lower low-pass cutoff, and add non-negative delay before rebuilding the routed hull stress signal while preserving the original AUP.

Rejected Alternatives: Treating portal output as authoritative replacement or adding a custom structural HRTF pass. Replacement breaks chained routing; a separate HRTF path duplicates the existing acoustic portal owner.

Scalability potential: Low can still bypass portal solving. Mid/High/Ultra get stable corridor propagation where every extra route can only darken, delay, or attenuate the stress sound.

Hardware Impact: Three scalar operations per routed stress event. No per-sample cost.

## Decision 12 - Granular Slot Resolver Zero Guard

Problem: `ResolveGranularVoiceSlot()` still clamped its input to at least one voice. Existing callers returned before passing zero, but this made the disabled voice budget depend on a fragile caller-side invariant.

Solution: Changed the resolver to clamp from `GranularDisabledVoiceCapacity` and return `-1` when the safe budget is zero.

Rejected Alternatives: Relying on upstream checks forever. A hot audio utility should enforce its own disabled contract.

Scalability potential: Low/MX350 now has redundant zero-budget enforcement at parameter, render, trim, and slot-allocation layers.

Hardware Impact: One branch in the voice allocator; no cost when granular synthesis is disabled because the renderer already returns before spawn.
