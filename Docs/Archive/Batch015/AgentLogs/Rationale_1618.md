# Agent 1618 Rationale

Date: 2026-06-01
Status: STATIC VERIFIED / BUILD BLOCKED BY CONTENTION

## Decision 001: Source Reality Over Prompt File Names

Problem: Prompt names `Assets/_Project/Scripts/Audio/SpatialAudioManager.cs` and `UnderwaterAudioProcessor.cs`, but source scan shows `SpatialAudioManager.cs` at `Assets/_Project/Scripts/SpatialAudioManager.cs` and no `UnderwaterAudioProcessor.cs`.
Solution: Treat current source and `Docs/SYSTEMS_CONTRACTS.md` as authority. Work in existing audio ownership files: `SpatialAudioManager.cs`, `PlayerCriticalProceduralAudioRenderer.cs`, `PrologueAcousticOrchestrator.cs`, and audio editor tests.
Rejected Alternatives: Creating `UnderwaterAudioProcessor.cs` would invent a parallel authority route and risk duplicate underwater audio truth.
Scalability potential: Low uses existing one-pole/biquad fakes and bounded signal snapshots; middle/high/ultra can increase synthesis density through `GlobalQualityWeight` without changing gameplay truth.
Hardware Impact: Avoiding a new audio manager avoids extra scene objects, registration, and cold startup churn on i3/MX350; estimated hot-path gain versus duplicate manager polling is 5-20 microseconds per frame depending on service lookup frequency.

## Decision 002: No Managed Audio Callback Expansion

Problem: Mandates forbid managed `AudioSource.OnAudioFilterRead` as a primary DSP path, while existing source still has managed callbacks in isolated playback classes and a large procedural renderer.
Solution: Do not add new managed audio callbacks. Strengthen existing `PlayerCriticalProceduralAudioRenderer` block synthesis and signal bridge paths already present in the audio domain.
Rejected Alternatives: Adding `OnAudioFilterRead` to `SpatialAudioManager` would violate the DSP mandate and move work to a driver-owned thread through managed Unity callback.
Scalability potential: Low keeps cheap low-pass, sine sweep, pink-like noise, and sparse impulse fakes; middle/high/ultra can raise texture/synthesis richness through existing renderer parameters.
Hardware Impact: Reusing the existing renderer avoids another per-buffer callback; estimated saved CPU is one managed callback dispatch per audio buffer, roughly 2-8 microseconds before DSP work.

## Decision 003: Reuse Existing Biquad and Signal Snapshot Route

Problem: Reentry plasma needs pink-noise band-pass synthesis and vacuum filtering, but inventing a second unmanaged bridge would duplicate `AudioTransitionState` ownership.
Solution: Reuse `SignalBus<ReentryAcousticStressSignal>` -> `PrologueAcousticOrchestrator` -> `IAudioService.QueuePrologueAudioTransition` -> double-buffered `AudioParameterSnapshot`. Add bounded plasma DSP state to the existing renderer.
Rejected Alternatives: A new `UnderwaterAudioProcessor` or direct DataVault hot poll would create another owner for the same acoustic truth and increase callback risk.
Scalability potential: Low uses one pink source, one LFO, and one band-pass; middle/high/ultra can increase drive/range through continuous `GlobalQualityWeight` without changing DTO layout.
Hardware Impact: Reusing the snapshot route avoids hot registry reads and DataVault locks. Estimated low-end gain versus polling service/DataVault in audio path is 6-18 microseconds per block.

## Decision 004: Static Proof Over Heavy Build Loop

Problem: User explicitly forbids `dotnet build` after small DSP edits and project rules forbid build under CPU/compiler contention.
Solution: Use source-level smoke tests and AST-style scans as the primary proof until a critical syntax risk appears. Before any build attempt, sample CPU and compiler processes.
Rejected Alternatives: Rebuilding after every patch would steal CPU from the active multi-agent cluster and violate the request.
Scalability potential: No runtime effect. Developer cadence stays compatible with 20+ concurrent agents.
Hardware Impact: Avoids repeated MSBuild/csc spikes; host CPU time saved is measured in seconds, not microseconds.

## Decision 005: Cross-Domain Acoustic Route Card

Problem: `AwaitableDropSequenceDirector` publishes `ReentryAcousticStressSignal` with `FlagAuthoritativeFilter`; its 400 Hz vacuum and 80 Hz splashdown constants override the audio bridge and violate 150/350 Hz acoustic targets.
Solution: Change only the acoustic constants in that signal publisher: vacuum 150 Hz, plasma 20000 Hz, splashdown 350 Hz. The narrative state machine, timings, DTO layout, and authority route remain unchanged.
Rejected Alternatives: Clamping inside the audio consumer would hide incorrect source truth and make telemetry lie about the authored transition.
Scalability potential: Low/middle/high/ultra all use the same acoustic truth; renderer richness still scales through `GlobalQualityWeight` and granular stress.
Hardware Impact: No measurable CPU change. Correct routing prevents extra corrective filters; estimated saved cost is 1-3 scalar clamps per transition publish.

## Decision 006: HapticRequest Instead Of Direct Pulse

Problem: The task asks for synchronized tactile rumble, but direct motor pulses are owned by input/haptic synthesis and should not be driven from the audio renderer.
Solution: Publish bounded `HapticRequest` signals from `PrologueAcousticOrchestrator` after a successful audio transition queue write. Splashdown emits one crush pulse; plasma peaks emit throttled micro-vibration.
Rejected Alternatives: Publishing `HapticPulseSignal` directly from audio would bypass the canonical input synthesis route and compete with controller ownership.
Scalability potential: Low uses one crush pulse and sparse peak pulses; high/ultra can make input synthesis richer downstream without changing audio DTOs.
Hardware Impact: Signal publish is visual-sync/cold relative to DSP, not on the audio path. Estimated cost is 2-5 microseconds only on transition frames, 0 us inside DSP blocks.

## Decision 007: Editor Source Tests As Proof Artifact

Problem: Runtime profiler/build proof is blocked by user build prohibition plus observed CPU/compiler contention, but the changes still need repeatable verification.
Solution: Add `AudioEnvironment1618EditTests` with source-level checks for cutoffs, pink/biquad plasma path, bounded ring routing, hot-method forbidden tokens, and mock low-pass biquad attenuation.
Rejected Alternatives: Running a profiler loop or build while CPU load is 91% with active dotnet processes would violate the compile gate.
Scalability potential: No runtime footprint; tests protect the continuous quality path and acoustic constants from regression.
Hardware Impact: Runtime cost is 0 microseconds. Editor-only static test cost is outside frame time.

## Decision 008: Gate Plasma Coefficients When Idle

Problem: The prologue mixer computed plasma band-pass coefficients once per block even when no plasma stress was active.
Solution: Initialize coefficient scalars to zero and compute `ComputeBandPassCoefficients` only when `blockProloguePlasmaDrive > HullNoiseFloor`.
Rejected Alternatives: Leaving the idle coefficient solve in place is harmless but wastes low-end CPU on every non-reentry block.
Scalability potential: Low/medium devices pay zero plasma coefficient cost outside reentry; high/ultra still receive the full pink-noise band-pass path when stress is active.
Hardware Impact: Saves one sine/cosine coefficient solve per audio block during normal ocean gameplay; estimated 1-4 microseconds per block on i3/MX350-class CPUs.

## Decision 009: Triangle Plasma LFO And Unsigned Haptic Cadence

Problem: Reentry plasma modulation was richer than required for a noise layer and plasma haptic cooldown used signed frame subtraction against an unsigned DTO frame.
Solution: Use a triangle phase fake for plasma amplitude movement and bind LFO depth/output gain to a block-local continuous quality scalar. Store last plasma haptic frame as `uint` and compare with `unchecked(state.Frame - _lastPlasmaHapticFrame)`.
Rejected Alternatives: Keeping the old sine-style LFO wastes per-sample scalar work for a non-tonal plasma bed. Adding a second haptic lane or changing `AudioTransitionState` would expand public surface without need.
Scalability potential: Low receives flatter, cheaper plasma texture; middle/high/ultra increase modulation depth continuously through `GlobalQualityWeight` without binary switches or DTO changes.
Hardware Impact: Removes a plasma LFO sine-approximation call per active sample and prevents long-session cadence drift; estimated low-end gain is 2-5 scalar ops/sample during reentry, 0 us outside reentry.

## Decision 010: Quality Weight Belongs In The Audio Snapshot

Problem: The plasma DSP block consumed continuous quality through `_cachedAudioQualityWeight01` after the snapshot had already been acquired, leaving a small phase-proof gap even though the read was scalar and allocation-free.
Solution: Reuse padding in the explicit 256-byte `AudioParameterSnapshot` at offset 220 for `GlobalQualityWeight`, publish it in `PublishAudioParameterSnapshot`, and consume `parameters.GlobalQualityWeight` inside `MixAndFilterBlock`.
Rejected Alternatives: Expanding `AudioTransitionState` would change a public 64-byte DTO. Adding a new signal lane for quality would create unnecessary first-party broadcast surface for one existing owner.
Scalability potential: Low/middle/high/ultra all receive the same immutable per-block quality scalar; plasma richness still breathes continuously without binary switches.
Hardware Impact: No new allocation, no snapshot size growth, no extra DataVault lock. The gain is correctness: one less cross-phase mutable field read in the audio mix path.

## Decision 011: Snapshot Publisher Owns Final Quality Sanitation

Problem: `_cachedAudioQualityWeight01` is already sanitized when cached, but the snapshot writer was still trusting upstream state during the final handoff into the audio producer thread.
Solution: Sanitize `GlobalQualityWeight` inside `PublishAudioParameterSnapshot` immediately before the inactive snapshot slot is swapped with `Interlocked.Exchange`. Add editor proof for the 256-byte explicit layout and offset-220 field.
Rejected Alternatives: Relying on `CacheAudioQualityPolicy` only leaves a weaker proof boundary. Adding a new quality DTO or signal lane would add surface area without changing audio truth.
Scalability potential: Low devices still receive clipped continuous quality without binary mode jumps; middle/high/ultra can raise plasma modulation depth through the same immutable scalar.
Hardware Impact: One saturate/select on snapshot publication, not in the DSP sample loop. Runtime allocation remains 0 bytes; audio-thread cost remains 0 microseconds.

## Decision 012: Lock Proof Must Include Reentry State Publisher

Problem: The audio renderer lock proof covered the DSP/ring routes, but `AwaitableDropSequenceDirector` also writes reentry state and black-box stage telemetry through DataVault locks.
Solution: Extend `AudioEnvironment1618EditTests.DataVaultMutationGuardsAreSingleRouteAndFinallyReleased` to prove `PublishReentryStateNoThrow` and `RecordStage` each use one `TryAcquireWriteLock`, one `ReleaseWriteLock`, and `finally`, with no mutual method call that could nest locks.
Rejected Alternatives: Treating director writes as out-of-domain would hide the acoustic source route that feeds the prologue audio bridge.
Scalability potential: Low/middle/high/ultra devices get the same lock topology; richer audio does not create additional DataVault write ownership.
Hardware Impact: Runtime cost is 0 microseconds because this is source proof only. The avoided failure class is deadlock/stall during prologue reentry publication.

## Decision 013: Splashdown Snap Must Survive Same-Frame Complete Signals

Problem: `PrologueAcousticOrchestrator.LateFrameTick` consumes reentry acoustic stress before complete signals. A same-frame `PrologueCompleteSignal` could set ocean handoff and restart the filter sweep from vacuum 150 Hz, erasing the intended 350 Hz splashdown impact cutoff before the audio transition was queued.
Solution: Add a local 350 Hz splashdown cutoff owner in the prologue audio bridge. Ocean handoff sets both current cutoff and sweep start to 350 Hz, then `_sweepSnapHeldForPublish` prevents the sweep from advancing until after the first impact snapshot is accepted by `QueuePrologueAudioTransition`.
Rejected Alternatives: Trusting the director's `ReentryAcousticStressSignal` alone is not sufficient because consumer ordering can overwrite it locally. Starting the sweep immediately or clearing the hold before queue acceptance creates a real violation of the "instant 350 Hz" impact contract under queue backpressure.
Scalability potential: Low devices get the same exact impact snap with no richer DSP cost; middle/high/ultra keep the same first-frame truth and can spend quality on plasma texture and downstream reverb.
Hardware Impact: One branch and several scalar assignments on ocean handoff only. Audio DSP thread cost remains 0 microseconds and managed allocation remains 0 bytes.

## Decision 014: Warm Every Prologue Audio Signal Lane Before LateFrame

Problem: `PrologueReentrySignalLanes.Warm()` only initialized `ReentryAcousticStressSignal`, while `PrologueAcousticOrchestrator.LateFrameTick` also consumes `AtmosphericReentrySignal` and `PrologueCompleteSignal` and publishes `HapticRequest`.
Solution: Extend the warmup method to initialize all four lanes after `SignalCorridorRuntime.EnsureInitialized()`. Add editor source proof that the warmup covers every lane touched by the prologue audio bridge.
Rejected Alternatives: Depending on broad global initialization is weaker and makes the bridge's explicit warmup misleading. Lazy initialization during `LateFrameTick` would violate the phase-safety intent even if it only occurs once.
Scalability potential: Weak devices avoid a cold presentation hitch; middle/high/ultra keep identical signal topology without adding runtime branches.
Hardware Impact: Cold-start cost only. Runtime/audio-thread cost remains 0 microseconds and 0 managed allocations.

## Decision 015: Guard Deferred Music And Managed Callback Surfaces Before Rewriting Them

Problem: The broader audio-domain audit found `HectonMusicDirector` defers slow music context work into `LateFrameTick`, and legacy dynamic-music/vocal-bank classes still expose managed `OnAudioFilterRead` transfer bridges. A blind rewrite would be high blast radius under active compiler contention.
Solution: Add source guards proving the current hot/deferred music bodies use cached dependencies only, while managed callback bodies contain no direct `GlobalRegistry`, component lookup, file I/O, sleep, explicit lock, allocation, or string conversion tokens. Leave runtime routing unchanged because no direct lookup violation was present.
Rejected Alternatives: Refactoring dynamic music and vocal bank DSP ownership during CPU 99% and active `dotnet` would risk a compile wall without an identified direct dependency violation. Treating the surfaces as unaudited would leave an APEX proof gap.
Scalability potential: Low devices keep existing bounded transfer bridges and avoid new runtime work; middle/high/ultra can keep richer dynamic music synthesis behind the same guarded callback boundary.
Hardware Impact: Runtime cost is 0 microseconds. The saved cost is avoided architectural churn; the proof prevents future hot registry/component/file regressions in music and callback bridge bodies.

## Decision 016: Spatial Listener Basis Uses Cached Runtime Context

Problem: `SpatialAudioManager.ResolveListenerBasis` is reached from spatial audio tick/deferred routes and previously called `PlayerRuntimeContextService.TryGetActiveRuntimeContext`, a hidden global runtime-context probe despite `SpatialAudioManager` already caching `IPlayerRuntimeContext` through GlobalRegistry hot-swap.
Solution: Convert `ResolveListenerBasis` to an instance helper that reads `_cachedPlayerRuntimeContext` and uses pure snapshot methods `TryGetLookRuntimeState` and `TryGetMovementRuntimeState` for listener forward derivation.
Rejected Alternatives: Leaving the static lookup in place would be cheap but architecturally wrong. Rebinding through `GlobalRegistry` inside the helper would be worse. Passing full player context through every call would churn signatures without improving ownership.
Scalability potential: Low devices avoid a repeated global service probe in spatial audio. Middle/high/ultra keep the same basis math and can spend saved cycles on richer acoustic radar/virtual voice work.
Hardware Impact: Estimated 0.5-2 microseconds saved per active spatial audio frame depending on listener-basis call count. No allocations, no DTO layout change, no phase change.

## Decision 017: Player-Critical Audio Binder Uses Cached Runtime Context Only

Problem: `PlayerCriticalProceduralAudioRenderer.Tick`, `SlowTick`, sonar, and echo helper paths called a binder that fell back to `PlayerRuntimeContextService.ActiveRuntimeContext` when `_playerRuntimeContext` was stale. The direct hot method bodies looked clean, but the transitive helper still violated cold identity ownership.
Solution: Replace `TryBindFromBootstrap` with `TryBindFromCachedRuntimeContext`. It reads `_playerRuntimeContext` only, fails closed when the cache is absent, and relies on `CacheColdRegistryReferences` plus `IGlobalRegistryHotSwapListener` callbacks to refresh the cached runtime context.
Rejected Alternatives: Keeping the fallback because it is rare is still hot-chain polling. Calling `GlobalRegistry.Player` from the binder is worse. Adding a signal for a single dependency would inflate the route without need.
Scalability potential: Low devices avoid stale-cache global probes during player-critical audio cadence; middle/high/ultra keep the same player-relative audio math and can spend saved budget on richer procedural layers.
Hardware Impact: Estimated 0.5-2 microseconds saved on frames where the old cache was stale or player bootstrap raced audio registration. Runtime allocations remain 0 bytes; no DTO layout or signal route changed.
