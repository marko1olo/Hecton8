# Rationale 1603 - ORBITAL_REENTRY_SEQUENCE_DIRECTOR

Status: ACOUSTIC STRESS FRAME LATCH / NO DOTNET BUILD

## Decision 00 - Authority And Scope

Problem: Reentry task crosses sequence timing, VFX, camera, audio, telemetry, and scene handoff.
Solution: Primary write to `Narrative/Prologue/AwaitableDropSequenceDirector.cs`; one cross-domain `BufferID.PrologueReentryState` added so DataVault has a unique owner route for the 32-byte scalar state.
Rejected Alternatives: Direct scene loading, direct audio-source manipulation, direct material instance mutation, or polling GlobalRegistry from hot loops.
Scalability potential: Low uses scalar shader/audio fakes and damped trauma; Middle uses normal cadence; High and Ultra spend saved simulation cost on stronger presentation intensity.
Hardware Impact: i3/MX350 gains from deleting phase-local async waits and avoiding object/particle timing churn.

## Decision 01 - Mandate Selection

Problem: The prompt reads like physical simulation, but project law requires fake-first cinematic math.
Solution: Applied fake-first, zero-GC, execution phase, signal lane, ARM64 layout, DSP/SPSC, URP hot path, and black-box telemetry mandates.
Rejected Alternatives: Physics simulation of plasma/ablation; no gameplay truth needs it.
Scalability potential: Continuous `GlobalQualityWeight` governs trauma amplitude, cadence pressure, and optional VFX/audio intensity.
Hardware Impact: Low-end devices keep smooth motion; high-end devices can receive stronger sensory values from the same scalar.

## Decision 02 - Public Awaitable Contract

Problem: `IPrologueSequenceService` still exposes `Awaitable RunPrologueSequenceAsync(CancellationToken)`, and `PrologueSequenceRegistryBridge` awaits it to retain cancellation ownership.
Solution: Kept a minimal lifecycle await loop while moving all sequence timing decisions into `IUpdatable.Tick(float)` FSM.
Rejected Alternatives: Changing the public interface would break bootstrap/registry agents; `AwaitableCompletionSource` adds allocation/lifetime risk.
Scalability potential: The public contract is cold compatibility; the hot timeline is deterministic dispatcher state.
Hardware Impact: Removed six phase await loops and `DelayDilatedAsync`; remaining await does not calculate timing or publish phase events.

## Decision 03 - ReentryStateDTO And DataVault Route

Problem: The state scalar needed a stable unmanaged proof route, not just private fields.
Solution: Added `ReentryStateDTO` with offsets: `ElapsedTime` 0, `Progress01` 8, `HeatIntensity` 12, `TraumaScalar` 16, `CurrentPhaseEnum` 20, padding to 32. Added `BufferID.PrologueReentryState = 74011`.
Rejected Alternatives: Reusing `PrologueSequenceTelemetryRing` for a different type would corrupt DataVault ownership.
Scalability potential: Low/Middle/High/Ultra consumers read one 32-byte scalar row and decide their own fidelity.
Hardware Impact: One cache-line-friendly row replaces object graph state and string event coupling.

## Decision 04 - Curves And Trauma

Problem: Reentry must feel violent without frame tearing or nausea on weak hardware.
Solution: Heat is smoothstep rise/fall. Trauma peaks at Max Q around progress 0.8 and scales by `math.lerp(0.28f, 1f, GlobalQualityWeight)`, published to `CameraJuiceSignals` at 30 Hz maximum.
Rejected Alternatives: AnimationCurve, random shake, binary quality switch.
Scalability potential: Low receives damped shake; Middle receives authored feel; High/Ultra receive heavier camera trauma without changing gameplay truth.
Hardware Impact: No array lookup, no reference allocation, no per-frame direct camera search.

## Decision 05 - VFX And Audio Integration

Problem: The prompt asked for new shader/audio paths, but existing systems already own the correct unmanaged lanes.
Solution: Reused `OrbitalDropReentryVfxController` cached IDs and `Shader.SetGlobalVector` route; reused `PrologueAcousticOrchestrator` low-pass/splashdown route through `AudioTransitionState`.
Rejected Alternatives: Duplicate `ReentryAcousticStressSignal` without a registered consumer; direct mixer/material mutation.
Scalability potential: VFX/audio owners continue scaling by their own quality and signal pressure fields.
Hardware Impact: Avoids extra queues and avoids hot-path service discovery.

## Decision 06 - Fail Closed

Problem: Non-finite orbital/atmospheric data or dispatcher/DataVault churn must not crash or advance invalid state.
Solution: Validation failures record `Faulted`, dump black boxes, unregister update lane, and release input lock. DataVault compaction/allocation fences skip allocation or write.
Rejected Alternatives: Letting NaN propagate into hash/shader/audio paths.
Scalability potential: Weak devices under pressure can pause state publication instead of corrupting timeline.
Hardware Impact: Failure path is bounded; steady path remains one DTO write and bounded signal attempts.

## Decision 07 - Verification And Build Avoidance

Problem: Task 15 requested build, but user explicitly forbade `dotnet build` after small edits.
Solution: Used Unity MCP `validate_script` on modified scripts; skipped dotnet/MSBuild. H8Memory validation timed out in validator regex on the large file, so line scan and source hash were used for the enum addition.
Rejected Alternatives: Ignoring the user's CPU contention order.
Scalability potential: Keeps sibling agents from compiler starvation.
Hardware Impact: No MSBuild CPU spike; validation stayed local and targeted.

## Decision 08 - APEX Hot Lookup Flattening

Problem: `OrbitalDropReentryVfxController.LateFrameTick` called a dependency resolver that could run `cameraRoot.TryGetComponent` if `_camera` was missing.
Solution: Moved camera resolution to `ConfigureSceneBindings` and cold dependency setup; `LateFrameTick` now uses only the cached `_camera` reference and never searches the scene.
Rejected Alternatives: Keeping a hot self-heal lookup; scene component search during visual sync creates variable frame cost and violates phase purity.
Scalability potential: Low devices avoid unexpected late-frame spikes; Middle/High/Ultra preserve the same cached route while spending visual budget on shader/audio intensity.
Hardware Impact: On i3/MX350, worst-case late-frame camera lookup is removed entirely; expected gain is small per frame but removes a stall vector during plasma overlay framing.

## Decision 09 - Lock Flattening Proof

Problem: DataVault write locks must not overlap between reentry state, sequence black box, and VFX telemetry.
Solution: Verified each method owns at most one `TryAcquireWriteLock`, with release in `finally`: `PublishReentryStateNoThrow`, `RecordStage`, and `WriteTelemetry`.
Rejected Alternatives: Combining DTO and black-box writes under one nested critical section; this would increase deadlock surface and prolong write-lock ownership.
Scalability potential: Low devices keep bounded critical sections; High/Ultra can add visual consumers without changing lock topology.
Hardware Impact: Lock hold time remains one buffer write per method; no second lock can be held by the same thread in the verified domain.

## Decision 10 - Dedicated Reentry Acoustic Stress Lane

Problem: Audio previously inferred plasma stress from `AtmosphericReentrySignal`; this was functional but not a first-class proof route for low-pass, LFE, and granular hull stress.
Solution: Added `ReentryAcousticStressSignal` as a 32-byte explicit-layout SignalBus payload. The director publishes stress/filter/LFE/granular scalars from the FSM, and `PrologueAcousticOrchestrator` consumes it in `LateFrameTick`.
Rejected Alternatives: Adding a direct call into `IAudioService` from the director; that would couple simulation timing to audio implementation and bypass the signal lane.
Scalability potential: Low devices receive damped LFE/granular values via `GlobalQualityWeight`; Middle/High/Ultra can spend more DSP intensity without changing payload layout or authority route.
Hardware Impact: One unmanaged 32-byte SPSC payload replaces extra mixer polling and removes the need for audio-side curve reconstruction when the director already owns the reentry scalar.

## Decision 11 - Signal Lane Cold Contract Fallback

Problem: `PrologueReentrySignalLanes.Warm()` can run before `GlobalSignals.InitializeAllQueues`; without a default `SignalLanePolicyCache<T>` contract, the new lane could initialize with default capacity/hash and reject later configuration.
Solution: Added `ReentryAcousticStressSignal` to `SignalLanePolicyCache<T>.TryResolveDefaultContract`, then registered the lane in `GlobalSignals.RuntimeLifecycle`.
Rejected Alternatives: Trusting runtime initialization order; that is brittle under scene bootstrap and editor hot-reload.
Scalability potential: Capacity is 16/max 16/low-tier 4, preserving low-end budget while retaining enough cadence for 30 Hz stress publishing.
Hardware Impact: Prevents late reconfigure faults and keeps cold lane allocation deterministic.

## Decision 12 - Explicit Splashdown Fullscreen Flash

Problem: The VFX bridge produced whiteout through plasma opacity, but Task 11 required an explicit `_FullScreenFlash` absolute white upload at ocean impact.
Solution: Added a cold-cached `_FullScreenFlash` shader property ID, a one-frame hold scalar, and a shader global read. `TriggerImpactFlash()` runs only on `PrologueCompleteSignal.PhaseOceanHandoff` from the sequence director; the first `LateFrameTick` upload is `(1,1,1,1)`, and decay starts on the next visual-sync frame.
Rejected Alternatives: Adding a real fullscreen quad or particle blast; both add object lifecycle and fill-rate cost. Reusing opacity alone was cheaper but did not provide a separately provable impact impulse.
Scalability potential: Low devices get the same first-frame white impact, then a faster continuous decay. Middle/High/Ultra keep a longer impact bloom via `GlobalQualityWeight` without changing gameplay truth or signal layout.
Hardware Impact: One extra cached global vector upload only while flash changes; on i3/MX350 this is cheaper than any new renderer, material instance, or particle burst.

## Decision 13 - Flash Regression Validator

Problem: A flash scalar can regress silently if a future edit decays it before the first shader upload.
Solution: Extended `ReentrySequenceMetricValidator1603` with `FlashImpulseValid`: first simulated visual-sync upload must remain exactly `1.0`, second upload must be lower and still finite/unit-bounded.
Rejected Alternatives: Visual-only QA; the bug is temporal and can be missed by screenshots.
Scalability potential: The validator preserves the low/middle/high/ultra fade law while protecting the single-frame impact contract.
Hardware Impact: Validator is cold/source-level only. Runtime impact is zero except the intended scalar math already required by the flash.

## Decision 14 - Ablation State Global Vector

Problem: Reentry heat existed, but procedural ablation and glass stress had no first-class scalar contract and could only be faked indirectly through plasma opacity.
Solution: Added `_HectonReentryAblationState` as a single global vector: x plasma intensity, y ablation amount, z glass stress, w flash. `OrbitalDropReentryVfxController` computes these in `LateFrameTick` from cached state and uploads only when changed.
Rejected Alternatives: Setting `_PlasmaIntensity`, `_AblationAmount`, and `_GlassCrackIntensity` per material every frame; that creates renderer coupling and more upload calls. Direct sequence-to-material writes were rejected as cross-domain ownership violation.
Scalability potential: Low uses the same scalar route with damped glass stress through `GlobalQualityWeight`; Middle gets stable scorch; High/Ultra can keep longer flash and stronger ablation response without changing DTOs or gameplay truth.
Hardware Impact: On i3/MX350 the added cost is one extra `Shader.SetGlobalVector` only when values change. No renderer search, no material clone, no managed allocation.

## Decision 15 - Full-Trajectory Metric Sampling

Problem: The metric validator sampled `i * 1/60 / 30`, which covered only the first 13.3% of the 30-second reentry and could not prove peak heat or ablation.
Solution: Changed the fuzzer to sample normalized progress from `0..1` over 240 steps and added ablation/glass bounds. Local simulation now reaches max heat, trauma, ablation, and glass stress within unit bounds.
Rejected Alternatives: Raising frame count to 1800; it would still be cold, but the normalized sampler proves the same curve with less CPU and no new runtime dependency.
Scalability potential: The validator protects the continuous low/middle/high/ultra curve law instead of a binary tier path.
Hardware Impact: Cold validation cost stays trivial; no runtime impact.

## Decision 16 - Editor APEX Harness

Problem: The reentry refactor needed repeatable proof for APEX constraints without running project-wide MSBuild during another active `dotnet` workload.
Solution: Added an EditMode source-level harness that parses owned C# files, extracts named hot methods, rejects cold dependency lookups/timing APIs/managed containers in those methods, proves DataVault write locks are one-per-method and released in `finally`, validates `LateFrameTick` shader upload routing, checks unmanaged SignalBus usage, and samples authored timeline points.
Rejected Alternatives: A markdown-only proof or JSON artifact; neither prevents future regressions. A full `dotnet build` was also rejected because PID 31232 was active and the user explicitly prohibited compiler contention.
Scalability potential: Low/Middle/High/Ultra all keep the same deterministic scalar route. The harness protects fidelity scaling from becoming binary quality branching or hot dependency polling.
Hardware Impact: Runtime impact is zero. Editor proof cost is source parsing only; on i3/MX350 it avoids MSBuild CPU spikes while still catching the dependency, phase, lock, and allocation classes that would cause stalls.

## Decision 17 - Settled Phase State Publication

Problem: `AdvanceReentryState()` updated progress, heat, and trauma before the FSM `switch (_stage)` executed. Publishing the DTO there could expose fresh scalar values with the previous `CurrentPhaseEnum` for one visual-sync frame.
Solution: Removed DTO publication and phase stamping from `AdvanceReentryState()` and added `PublishFinalizedReentryStateNoThrow()`. `Tick(float)` now advances scalar state, executes the stage switch, then stamps `_reentryState.CurrentPhaseEnum = (uint)_stage` and publishes the final DTO. Cancellation and dev-skip exits publish through the same finalized route.
Rejected Alternatives: Letting consumers infer phase from progress; that spreads ownership and breaks the one fact -> one owner route. Deferring with a managed queue was rejected because a direct struct write after the switch is cheaper and deterministic.
Scalability potential: Low/Middle/High/Ultra consumers receive coherent phase and scalar state from the same 32-byte DTO without adding per-tier routes.
Hardware Impact: No extra allocation and no new lock topology. The added method is a scalar guard plus the existing single DataVault write; it removes a possible one-frame shader/audio mismatch under weak hardware stalls.

## Decision 18 - Async Lifecycle Exit Finalization

Problem: `RunPrologueSequenceAsync` is a cold compatibility wrapper, but its cancellation and exception exits could call `CompleteSequenceRun()` without publishing the final `Cancelled` or `Faulted` `ReentryStateDTO`.
Solution: After every cold lifecycle completion path in the wrapper, call `PublishFinalizedReentryStateNoThrow()` so the same finalized phase stamp and DataVault write route is used as the tick FSM.
Rejected Alternatives: Treating the wrapper as irrelevant because the hot FSM is correct; cancellation between ticks is still an externally visible lifecycle edge.
Scalability potential: Low/Middle/High/Ultra consumers receive a coherent terminal DTO even when cancellation is triggered between frames.
Hardware Impact: No steady-frame cost. Only cold cancellation/exception exits do one existing single-row DataVault write.

## Decision 19 - Presentation Signal Struct Initialization

Problem: VFX presentation methods used object-initializer syntax for unmanaged signal structs in methods reachable from `LateFrameTick`.
Solution: Converted `AcousticPingSignal`, `DebrisSpawnSignal`, `VisorDropletSignal`, and `ReentryVfxStateSignal` publishers to `default` plus direct field writes; expanded the Editor harness hot-method list and explicit bans for those initializers.
Rejected Alternatives: Leaving the syntax because structs do not allocate; the code still obscures the zero-GC contract and weakens static review.
Scalability potential: Low devices avoid hidden drift toward heap-style signal construction; High/Ultra keep the same signal payload routes while spending budget on visuals.
Hardware Impact: Runtime delta is tiny, but the hot path is now mechanically stricter and easier to audit for i3/MX350-class stalls.

## Decision 20 - Audio Publisher Harness Coverage

Problem: The Editor APEX harness listed a stale method name, `PublishAudioTransitionState`, while the real late-frame audio publisher is `PublishAudioTransition`.
Solution: Replaced the stale method name with `PublishAudioTransition` and added `AdvanceFilterSweep` so the harness covers the actual audio methods reachable from `LateFrameTick`.
Rejected Alternatives: Keeping only the ad-hoc PowerShell scan; the regression proof needs to live in the Unity EditMode harness, not only in session history.
Scalability potential: Low/Middle/High/Ultra audio state scaling remains covered by the same source-level hot-path bans.
Hardware Impact: Runtime impact is zero; proof quality improves by checking the real audio publication method before it can drift into allocations or dependency lookup.

## Decision 21 - Complete Phase Ambient Tail

Problem: After `HydratedFade` transitions to `Complete`, `HasActivePresentationState()` could return false once heat, opacity, flash, and audio were quiet, even if `_ambientBlend01` had not reached the ocean target.
Solution: Added `IsAmbientBlendSettledForComplete()` and included it in the complete-state activity predicate, so `LateFrameTick` continues until the ambient blend reaches `1.0 - ShaderEpsilon`.
Rejected Alternatives: Snapping ambient to ocean color on completion; that would hide the bug with a visual pop and break the authored fade.
Scalability potential: Low devices finish a cheap scalar color fade; High/Ultra keep the same fade while richer shader/audio work has already stopped.
Hardware Impact: Worst-case extra tail is bounded scalar math and one ambient global update path; no new allocation, scene search, or lock.

## Decision 22 - Post-Handoff Replay Fences

Problem: Late `AtmosphericReentrySignal` packets could rewrite VFX heat/opacity targets after `HydratedFade`, and duplicate sequence-owned `PhaseOceanHandoff` packets could replay the impact flash or regress `Complete` back into `HydratedFade`. Audio atmospheric/stress consumers also still read snapshots after `StageOceanHandoff`, even though portal/ocean sweep owns the terminal mix.
Solution: `OrbitalDropReentryVfxController.ConsumeAtmosphericSignals()` now returns before snapshot consumption once `_phase >= ReentryPhase.HydratedFade`. `ConsumePrologueCompleteSignals()` rejects `Complete` and duplicate sequence handoffs before `TriggerImpactFlash()`. `PrologueAcousticOrchestrator` now returns before atmospheric/stress snapshot consumption once `_stage == AudioTransitionState.StageOceanHandoff`.
Rejected Alternatives: Keeping late packets as a self-heal route. After ocean handoff, the only valid owner is the complete signal plus bounded presentation tail; accepting more plasma/stress packets creates visual and acoustic replay.
Scalability potential: Low devices avoid extra post-handoff shader/audio churn. Middle/High/Ultra keep the authored flash, ambient tail, and filter sweep, but no longer pay for replayed reentry response after the ocean transition.
Hardware Impact: On i3/MX350, the terminal guards skip two SignalBus snapshot loops and prevent duplicate flash uploads after handoff. Expected gain is small per frame but removes an unbounded replay/stall vector during the most expensive transition.

## Decision 23 - Single-Shot Ocean Handoff

Problem: `PrologueSequenceRegistryBridge.PublishOceanHandoff()` increments `Sequence` on every publish. The previous VFX duplicate guard rejected only the same sequence, and audio would restart `_sweepElapsedSeconds` for a later ocean handoff carrying a fresh sequence. A replayed terminal packet could therefore create a second flash or elongate the ocean low-pass sweep.
Solution: VFX now treats `_hasOceanHandoffSequence` as a consumed terminal latch: after the first sequence-owned ocean handoff, later sequence handoffs are ignored before `TriggerImpactFlash()`. Audio now ignores later sequence-owned ocean handoffs before resetting `_sweepElapsedSeconds`, `_sweepActive`, or `_currentLowPassCutoffHertz`.
Rejected Alternatives: Same-sequence filtering, frame-number filtering, or time-window filtering. They all depend on producer cadence instead of the stronger invariant: one reentry run has one accepted ocean ownership transfer.
Scalability potential: Low devices avoid repeated terminal shader/audio work. Middle/High/Ultra retain the exact first splashdown flash, ambient fade, and filter sweep, with no extra replay route or quality-tier branch.
Hardware Impact: On i3/MX350, worst-case replay packets are rejected by one boolean branch before expensive flash/sweep side effects. The steady path remains allocation-free and keeps one SignalBus route per domain.

## Decision 24 - Proof Harness And Handoff Hash Hygiene

Problem: The source-level EditMode harness contained a literal `StartCoroutine` token only as forbidden text, which made Unity MCP `validate_script` report a warning even though no coroutine call existed. Also, after the single-shot handoff fix, `_lastOceanHandoffSequence` was captured but not read by runtime state proof.
Solution: Split the forbidden token as `"Start" + "Coroutine"` so the harness still checks the same runtime string while avoiding false validation noise. Added `_lastOceanHandoffSequence` to `ResolveStateHash()` so the VFX black-box ring distinguishes which terminal handoff was accepted.
Rejected Alternatives: Suppressing the warning verbally or deleting sequence capture. Suppression leaves proof noise; deletion removes a useful crash-state discriminator.
Scalability potential: Low/Middle/High/Ultra paths keep identical runtime behavior; the only runtime change is one integer hash fold inside existing telemetry.
Hardware Impact: On i3/MX350 the hash fold is negligible and cold relative to telemetry cadence. It improves postmortem identity without adding allocation, scene lookup, or lock nesting.

## Decision 25 - Unity Lifecycle Cancel Finalization

Problem: `OnDisable()` and `Dispose()` cancelled an active sequence by setting `_running = false`, unregistering the update lane, and releasing input lock directly. That bypassed `RecordStage(Cancelled)` and `PublishFinalizedReentryStateNoThrow()`, so Unity lifecycle shutdown could leave the DataVault DTO at the previous phase.
Solution: Added `CancelActiveSequenceNoThrow(byte reason)`. Both `OnDisable()` and `Dispose()` now route active shutdown through `RecordStage(PrologueStage.Cancelled)`, `CompleteSequenceRun()`, and finalized DTO publication before any buffer release.
Rejected Alternatives: Waiting for the async wrapper `finally` or the next `Tick`. Unity disable/destroy can remove the update lane before either route runs, so the cancellation fact must be written synchronously in the lifecycle method.
Scalability potential: Low/Middle/High/Ultra consumers receive one coherent terminal DTO regardless of whether the sequence ends by authored ocean handoff, token cancel, disable, or destroy.
Hardware Impact: No steady-frame cost. The cold disable path adds one existing black-box write and one existing DTO write, with the same flat one-lock-per-method topology.

## Decision 26 - Fault Terminal DTO Finalization

Problem: `FailSequence()` recorded `Faulted` in the black-box ring and completed the sequence, but did not publish a finalized `ReentryStateDTO`. A tick exception, validation failure, or non-finite scalar could therefore leave consumers reading the previous DTO phase even though black-box telemetry said `Faulted`.
Solution: `FailSequence()` now sanitizes terminal scalar fields and calls `PublishFinalizedReentryStateNoThrow()` after `CompleteSequenceRun()`. `SanitizeReentryStateForTerminalPublish()` clamps progress/heat/trauma to unit bounds and replaces non-finite elapsed time with a finite fallback.
Rejected Alternatives: Publishing raw non-finite DTO fields or relying only on black-box telemetry. Raw publication can recurse into fault handling; black-box-only leaves visual/audio consumers with stale DTO truth.
Scalability potential: Low/Middle/High/Ultra consumers receive one coherent terminal fault state without introducing managed queues or per-tier recovery logic.
Hardware Impact: No steady-frame cost. Fault path adds a few scalar clamps before the existing single DTO write; lock topology remains flat.

## Decision 27 - Async Fault Route Unification

Problem: `RunPrologueSequenceAsync` still carried a manual `catch (Exception)` route that duplicated `RecordStage(Faulted)`, black-box dump, `CompleteSequenceRun()`, and final DTO publication. After adding fault sanitization to `FailSequence()`, this wrapper path could drift and publish unsanitized scalar state.
Solution: Replaced the manual async exception body with `FailSequence(PrologueCancelReasons.NonFinite)`. The Editor harness now proves the wrapper uses the canonical fault route and does not contain manual `RecordStage(PrologueStage.Faulted)` logic.
Rejected Alternatives: Duplicating the sanitizer into the wrapper. Two fault paths are unnecessary and raise the chance of future phase-order drift.
Scalability potential: Low/Middle/High/Ultra paths share one terminal fault owner, so consumers do not need tier-specific recovery behavior.
Hardware Impact: Cold exception path becomes shorter and uses the same flat write-lock sequence as every other fault route. No steady-frame cost.

## Decision 28 - Service Start Fault Closure

Problem: `TryBeginSequenceRun()` handled `TryRegisterUpdateLane()` failure with a local `RecordStage(Faulted)` and `_running = false`. That recorded black-box failure but bypassed `FailSequence()` sanitation and final `ReentryStateDTO` publication, leaving service consumers with stale DataVault phase truth after a cold start failure.
Solution: Route update-lane registration failure through `FailSequence(PrologueCancelReasons.NonFinite)` and extend the Editor harness to prove `TryBeginSequenceRun()` has no manual `RecordStage(PrologueStage.Faulted)` path.
Rejected Alternatives: Adding a separate start-failure helper or expanding `IPrologueSequenceService`; both create another terminal route or contract churn. The existing canonical fault owner is sufficient.
Scalability potential: Low/Middle/High/Ultra service consumers observe one terminal DTO route for start failure, tick failure, async wrapper failure, disable, and dispose.
Hardware Impact: No steady-frame cost. Cold failure does one existing black-box write and one sanitized DTO write; it avoids downstream stale-phase recovery on i3/MX350-class hardware.

## Decision 29 - Runtime Accessor Purity

Problem: `PrologueSequenceRegistryBridge.SurvivalProxyPressure01` recalculated pressure through a mutating hysteresis path, and `ShouldSkipPrologue` consumed input signals and immediate input state inside a property getter. The reentry FSM calls these through the runtime interface, so mutable work was hidden behind read accessors.
Solution: Added explicit `IPrologueSequenceRuntime.RefreshFrameState()` for per-frame skip input polling. `ShouldSkipPrologue` now returns cached `_skipRequested`; `SurvivalProxyPressure01` returns cached pressure only. Survival proxy cache refresh is explicit in `PrepareSequenceRun()` and `IsOceanSurfaceReady()`, with cached-ready hydration returning before pressure refresh.
Rejected Alternatives: Keeping hidden property side effects for API convenience, or adding a second skip property. Property side effects violate the global systems doctrine; a second property would duplicate route ownership.
Scalability potential: Low/Middle/High/Ultra all keep the same runtime interface, with explicit mutable refresh and pure cached reads. Weak devices avoid surprise input/signal scans inside property getters.
Hardware Impact: No new steady allocation. One explicit `RefreshFrameState()` call per tick replaces hidden work in `ShouldSkipPrologue`; survival pressure is refreshed only on sequence prepare and non-terminal ocean-readiness checks.

## Decision 30 - Hydration Read/Refresh Split

Problem: `IsOceanSurfaceReady(bool allowProxy)` consumed streaming residency and `SectorResidencyHydratedSignal` snapshots while also mutating `_observedHighResSurfaceReady` and `_observedProxySurfaceReady`. The method name is a read query, but the body performed owner-state updates.
Solution: Added `IPrologueSequenceRuntime.RefreshHydrationState(bool allowProxy)`. The director performs one proxy-aware refresh pass, then reads high-resolution readiness first and proxy readiness second. `IsOceanSurfaceReady()` now returns only cached readiness flags.
Rejected Alternatives: Renaming the existing method or leaving the side effects documented. Renaming still forces contract churn; leaving hidden mutation keeps a stall vector inside a read-looking API.
Scalability potential: Low/Middle/High/Ultra keep identical hydration decisions, but weak devices now skip residency scans whenever the caller only needs cached readiness.
Hardware Impact: On i3/MX350, the terminal hydration-ready path is a single cached boolean expression. Residency SignalBus scanning happens only in explicit refresh calls, and the director no longer does a two-pass cursor walk that can skip proxy fallback packets.

## Decision 31 - Dev-Skip Fault Route Canonicalization

Problem: `TryExecuteDevelopmentSkipHandoff()` had its own fault route: manual `RecordStage(Faulted)`, dump calls, and input release. That bypassed `FailSequence()` sanitation and could be followed by the outer tick branch publishing a second terminal DTO.
Solution: Route dev-skip handoff exceptions through `FailSequence(PrologueCancelReasons.DevSkip)` and guard the cancellation/dev-skip completion blocks with `_running` before calling `CompleteSequenceRun()` and `PublishFinalizedReentryStateNoThrow()`.
Rejected Alternatives: Leaving the duplicate dev-only route because it is rare. Rare terminal paths still define crash behavior, and a second `Faulted` owner violates the one-route doctrine.
Scalability potential: Low/Middle/High/Ultra all receive the same terminal DTO and black-box fault path; no tier-specific dev-skip or cancellation recovery exists.
Hardware Impact: No steady-frame cost. The fault edge saves downstream stale/duplicate terminal reconciliation and keeps the existing flat lock topology on i3/MX350-class hardware.

## Decision 32 - Hydration High-Resolution Priority

Problem: `RefreshHydrationState(true)` could mark standalone/impostor proxy readiness before checking whether `oceanSurfaceChunkId` was already resident. That let a visual fallback win over the real high-resolution ocean surface in the same refresh pass.
Solution: Move the high-resolution `streaming.IsChunkResident(oceanSurfaceChunkId)` check before proxy fallbacks and extend the EditMode harness with ordering assertions.
Rejected Alternatives: Treating proxy and high-res as equivalent because both complete the prologue. They are not equivalent visually; proxy is a fallback, not a preferred route.
Scalability potential: Low devices still get proxy when high-res is not resident. Middle/High/Ultra use the already resident high-res ocean surface without an artificial downgrade.
Hardware Impact: No extra steady-frame work. The branch order saves a possible proxy handoff visual downgrade on i3/MX350 and avoids unnecessary fallback presentation on stronger machines.

## Decision 33 - Cached Proxy Priority Hole

Problem: Even after moving high-res streaming checks above proxy fallbacks, the first line of `RefreshHydrationState(true)` still used `IsOceanSurfaceReady(true)`. A cached proxy observation therefore short-circuited the refresh before high-res could be probed on a later frame.
Solution: Fast-return only when `_observedHighResSurfaceReady` is true. Then probe `streaming.IsChunkResident(oceanSurfaceChunkId)` before accepting `_observedProxySurfaceReady` or new proxy fallbacks.
Rejected Alternatives: Keeping the cached proxy fast path for CPU savings. It saves a small branch/streaming check but can downgrade the visible ocean handoff.
Scalability potential: Low still exits through proxy when high-res is absent. Middle/High/Ultra can upgrade to high-res as soon as the real chunk is resident, even if proxy was observed earlier.
Hardware Impact: Adds only one explicit streaming residency check before proxy acceptance. On i3/MX350 the cost is bounded; on stronger machines it prevents unnecessary low-fidelity handoff.

## Decision 34 - Capsule Shader Continuous Math LOD

Problem: `Hecton_CapsuleReentryPlasmaFake.shader` still used binary `_H8OrbitalMathLod` thresholds for plasma flicker and overkill intensity. That violates continuous quality scaling and can pop during the reentry shot.
Solution: Normalize `_H8OrbitalMathLod` into `mathLod01`, then drive detail flicker and overkill boost with `smoothstep` weights. Added EditMode source assertions that reject the old `_H8OrbitalMathLod` branch patterns.
Rejected Alternatives: Keeping binary shader branches because they are GPU-side. Visual pops are still visible route violations; the continuous lerp costs a few scalar ops and keeps low/high behavior on one curve.
Scalability potential: Low gets stable cheap flicker near the authored constant. Middle blends into procedural surface noise. High/Ultra smoothly reach the 1.25 overkill emissive/scorch response with no tier jump.
Hardware Impact: On i3/MX350 this removes branch divergence and quality popping. The added scalar lerps are cheaper than presentation repair and do not touch CPU, DataVault, SignalBus, or managed memory.

## Decision 35 - Orbit Prologue Shader Continuous Math LOD

Problem: `Hecton_OrbitalPlanetRelativityFake.shader` and `Hecton_OrbitalCloudWhiteoutFake.shader` still used binary `_H8OrbitalMathLod` thresholds. The capsule plasma surface was continuous, but the surrounding orbit/whiteout visuals could still pop between cheap and detailed math during the same reentry corridor.
Solution: Replaced planet curvature, planet atmospheric/detail intensity, cloud whiteout noise, and cloud overkill boost with `smoothstep`-weighted `mathLod01` blends. Extended `ReentrySequence1603EditTests` so capsule, planet, and cloud prologue shaders all reject threshold branches.
Rejected Alternatives: Leaving the two adjacent shaders alone because the primary prompt named the capsule. They are first-viewport reentry presentation surfaces, and binary LOD jumps here are visible continuity defects.
Scalability potential: Low gets cheap stable planet color/curvature and steady whiteout alpha. Middle blends in continents/cloud bands/noise without a tier jump. High/Ultra smoothly reach stronger rim and whiteout overkill values.
Hardware Impact: On i3/MX350 this removes quality-threshold branch pops from the prologue shader set. The cost is scalar interpolation in shaders only; no CPU route, managed allocation, DataVault lock, SignalBus lane, scene lookup, or material clone was added.

## Decision 36 - Continuous Producer For Shader Math LOD

Problem: After shader threshold removal, `OrbitalRelativityDirector` still uploaded `_mathLod` as a discrete byte band to `_H8OrbitalMathLod`. That meant the shader could be continuous internally but still receive stepped 0/1/2/3 inputs.
Solution: Added `_mathLodShader` and `ResolveContinuousMathLod(float distance)`. The method blends distance mesh continuity, quality mesh continuity, high detail, and ultra detail into a clamped 0..3 scalar. Existing byte `_mathLod` remains for telemetry/hysteresis only.
Rejected Alternatives: Replacing telemetry byte layout or deleting hysteresis. The telemetry band is a diagnostic field and changing its layout would be unnecessary contract churn.
Scalability potential: Low remains near 0..1 based on quality and distance, Middle glides through mesh/high detail, High/Ultra approach 3.0 without a producer-side tier jump.
Hardware Impact: Runtime cost is scalar math in existing presentation assembly. No managed allocation, no registry polling, no DataVault lock, no SignalBus lane, no renderer/material clone, and no extra shader upload call were introduced.

## Decision 37 - Continuous Acoustic Quality Curve

Problem: `PrologueAcousticOrchestrator.ResolveQualityCurve01()` derived the DSP quality curve from `_qualityTierByte`. The output was 256-step quantized, so granular hull stress could make audible micro-steps while the visual path already consumed continuous `GlobalQualityWeight`.
Solution: Store `_qualityWeight` as a continuous float in `RefreshQualityPolicy()` and drive `ResolveQualityCurve01()` from `math.smoothstep(0f, 1f, math.saturate(_qualityWeight))`. Keep `_qualityTierByte` only for `AudioTransitionState.QualityTier`, publish-dirty comparison, and telemetry compatibility.
Rejected Alternatives: Removing the byte field or changing `AudioTransitionState` layout. That would churn a cross-domain audio DTO when only the working curve source was wrong.
Scalability potential: Low receives smoothly damped granular stress, Middle blends without step noise, High/Ultra reach full DSP intensity without a tier jump.
Hardware Impact: Same hot-path operation count class: one cached float assignment per late-frame refresh and one smoothstep in existing audio publish math. No managed allocation, registry polling, SignalBus lane, DataVault lock, or audio source creation was added.

## Decision 38 - Orbit Producer Harness Coverage

Problem: `OrbitalRelativityDirector.cs` was modified for continuous shader Math LOD, but the broad EditMode hot-path harness still parsed only the reentry director, VFX bridge, audio bridge, and registry bridge.
Solution: Add `OrbitDirectorPath` to `ReentryCSharpFiles`, so source parse, hot dependency bans, managed timing bans, and DataVault write-lock flattening checks cover the orbit producer too.
Rejected Alternatives: Keeping only the specific `OrbitalDirectorUploadsContinuousShaderMathLod()` proof. That verifies the scalar upload route, not the general hot-loop invariants.
Scalability potential: Low/Middle/High/Ultra all rely on the same orbit producer; widening the harness prevents future per-device quality work from adding hot scene lookups or nested DataVault locks.
Hardware Impact: Runtime impact is zero. Editor proof expands source scanning only; no build, no scene mutation, no managed runtime route.

## Decision 39 - Acoustic Cadence Decoupling

Problem: `PublishContinuousCameraTrauma()` published `ReentryAcousticStressSignal` only after the 30 Hz camera trauma accumulator gate. That made low-pass cutoff, LFE, and granular stress share a comfort throttle meant for camera shake.
Solution: Compute `trauma01` once, publish the acoustic stress signal immediately for every settled reentry tick, then apply the 30 Hz accumulator only to `CameraJuiceSignals.TryPublishImpact`.
Rejected Alternatives: Leaving audio under the camera cadence, or adding a second managed timer. The first creates audible pressure lag; the second adds unnecessary timing state when the simulation tick is already the owner.
Scalability potential: Low receives the same bounded one-signal-per-frame route with quality-scaled amplitude; Middle/High/Ultra get tighter plasma-to-filter alignment without changing DTO layout, lane capacity, or presentation authority.
Hardware Impact: On i3/MX350, worst-case added work is one existing unmanaged `SignalBus<ReentryAcousticStressSignal>.TryPushTracked` attempt per active reentry frame. No allocation, registry lookup, DataVault lock, scene query, or audio source creation was added.

## Decision 40 - Metric Result Byte Flags

Problem: `ReentrySequenceMetricResult` used explicit layout but stored proof flags as `bool`, which is a weak example for runtime-facing validation structs and can drift from the byte-flag discipline used by unmanaged signal/DTO contracts.
Solution: Replace proof flags with `byte`, convert boolean expressions through `ToByte(bool)`, and make the EditMode harness reject `public bool` proof fields while confirming byte flag names.
Rejected Alternatives: Keeping `bool` because the validator is cold. Cold proof code still teaches future runtime patterns, and explicit layout should stay scalar and ABI-obvious.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged; proof output remains compact and deterministic without introducing managed layout ambiguity.
Hardware Impact: Runtime impact is zero. Cold validator output becomes byte-stable and avoids future accidental boolean layout assumptions on i3/MX350-class IL2CPP targets.

## Decision 41 - Orbit Post FX Zero-Weight Gate

Problem: `PrologueOrbitSceneBootstrap` enabled camera post-processing and Bloom even when `GlobalQualityWeight` produced zero bloom weight. This violates the compact/minimum lane Bloom ban and can still activate a URP post-processing pass for no image gain.
Solution: Resolve `cameraData` cold, compute continuous `bloomWeight = quality * quality`, and route `Volume.enabled`, `Bloom.active`, and `cameraData.renderPostProcessing` through `postProcessingEnabled = bloomWeight > 0f`. Added the missing canonical cold alloc marker for the static scene-root scratch list and an EditMode source assertion.
Rejected Alternatives: Keeping Bloom active with zero intensity, or adding a binary low/high tier switch. The accepted route disables only the exact zero-weight survival lane; every non-zero weight still scales continuously.
Scalability potential: Low/minimum has no Bloom pass. Middle ramps in subtle reentry optics. High/Ultra reach stronger bloom scatter/intensity through the same continuous scalar, without a hard quality tier.
Hardware Impact: On i3/MX350 and compact UMA lanes this removes a potential post-processing pass at weight zero. Runtime hot path impact is none; the setup is scene bootstrap only.

## Decision 42 - World Handoff Scene Service Only

Problem: `PrologueWorldHandoffSceneLoader` exposed `useDirectSingleSceneLoad`, which could call `SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single)` directly after whiteout. That bypasses `ISceneService`, loading-screen policy, and the scene owner route.
Solution: Remove the direct load option and route every successful handoff through `ISceneService.LoadScene(sceneName)` after `Awaitable.NextFrameAsync(destroyCancellationToken)`.
Rejected Alternatives: Keeping direct load as an inspector fallback, or setting `allowSceneActivation=false` manually. Both would split scene authority; the existing scene service is the owner.
Scalability potential: Low/Middle/High/Ultra all use the same cold scene transition owner. The prologue whiteout remains a visual mask while the actual load path stays centralized.
Hardware Impact: On i3/MX350 this prevents accidental direct scene activation stalls outside the scene service. Runtime hot path is unchanged; the change only affects the cold handoff transition.

## Decision 43 - Acoustic Stress Frame Latch

Problem: `PrologueAcousticOrchestrator` kept `_hasStressOverride`, `_acousticStress01`, `_stressLfeGain01`, and `_stressGranularStress01` until another valid stress packet arrived. If the unmanaged stress lane dropped or skipped a visual-sync frame, stale Max-Q pressure could continue boosting LFE/granular output after the frame it belonged to.
Solution: In `ConsumeReentryAcousticStressSignals()`, keep the ocean-handoff guard first, then clear the stress override latch and cached stress scalars before reading `SignalBus<ReentryAcousticStressSignal>.GetFrameSnapshot()`. A fresh packet must re-arm the override in the same frame.
Rejected Alternatives: Decaying the stale values over time, or trusting the director to publish every frame. Decay hides ownership drift; trusting a packet every frame fails under bounded SignalBus pressure.
Scalability potential: Low devices get deterministic fallback to base vacuum/plasma DSP if a stress packet is missing. Middle/High/Ultra get full granular/LFE pressure only from fresh frame-local stress data, not stale queue history.
Hardware Impact: On i3/MX350 this adds four scalar assignments in one late-frame method and removes stale-audio recovery work. No allocation, registry lookup, scene query, DataVault lock, audio source creation, or DTO layout change was introduced.
