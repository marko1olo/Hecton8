# Rationale_BIOLUMINESCENCE_DIRECTOR

Date: 2026-05-13
Status: PENDING VERIFICATION

## Decision 0 - Prompt Boundary

Problem: The batch file contains prompts for multiple agents; neighboring tasks can contaminate architecture.
Solution: Extracted only `<AGENT_PROMPT id="BIOLUMINESCENCE_DIRECTOR">` using a CLI regex over `Docs/Tasks/CURRENT_BATCH.md`.
Rejected Alternatives: Reading adjacent prompts or using partial IDE context was rejected because it violates strict parsing and would mix domains.
Scalability potential: Low uses one global phase and no local ripple work. Middle adds limited ripples. High/Ultra can afford ripple detail and stronger glow response.
Hardware Impact: On i3/MX350 this prevents per-agent scope drift, not a direct runtime gain. Runtime target remains 0 B/frame and less than 0.1 ms suspicious threshold.

## Decision 1 - Mandate Selection

Problem: Bioluminescence touches lighting, shaders, global timing, AUP shifts, low-tier performance, and telemetry.
Solution: Loaded visual-fake-first, zero-GC, performance budgets, abyssal lighting, noir shader, URP hot path, crash telemetry, and AUP precision mandates.
Rejected Alternatives: Loading all mandates was rejected as noise. Loading only shader mandates was rejected because the prompt requires registry, telemetry, and AUP safety.
Scalability potential: Low disables touch ripple shader work. Middle keeps bounded 16 ripple inputs. High/Ultra use saved CPU from global pulse to buy stronger emissive/ripple visuals.
Hardware Impact: Expected MX350 gain comes from deleting per-object Update/MPB pulse paths and replacing them with one global shader state.

## Decision 2 - Bootstrap-Owned Director

Problem: The biolum manager exposed a static `Instance` facade and registered directly with `GlobalRegistry`, preserving singleton rot inside the lighting domain.
Solution: Removed the public `Instance` accessor and routed registration through `GameBootstrapper.RegisterBiolumDirector` / `UnregisterBiolumDirector`, with bootstrap persistence of the runtime component.
Rejected Alternatives: Leaving the `Instance` facade was rejected because it invites direct coupling. Creating a parallel service locator was rejected because `GlobalRegistry` already owns the project contract.
Scalability potential: Low/Middle/High/Ultra all use the same single director. Saved CPU becomes available for stronger shader-side glow on higher tiers.
Hardware Impact: On i3/MX350 this removes singleton lookup pressure and prevents future per-object manager discovery; estimated 15 us/frame risk avoided in dense scenes.

## Decision 3 - ASMDEF Isolation Block

Problem: The prompt requires `Hecton8.Lighting.asmdef` depending only on `Hecton8.Core.Contracts`, but the repo has no such asmdefs and biolum relies on internal `WorldSpatialHashGrid`/`SpatialQueryHit` plus concrete `GlobalRegistry.BiolumManager`.
Solution: Marked Task 3 blocked by dependency and kept the director inside the current Core assembly to preserve compile ownership.
Rejected Alternatives: Creating a new asmdef was rejected because it would strand internal world-grid access and cause a cross-assembly concrete-type cycle. Importing all Core into a new lighting asmdef was rejected because it violates the prompt.
Scalability potential: Assembly isolation has no direct Low/Middle/High/Ultra runtime delta; the safe path keeps functional runtime scalability intact.
Hardware Impact: No frame gain. Avoids a compile break that would stop all devices.

## Decision 4 - Wake Signal Adaptation

Problem: The task asks for `EntityWakeSignal`, but the codebase exposes `MovementAcousticSignal` as the existing AUP+velocity movement wake lane.
Solution: Consumed `GlobalSignals.TryDequeueMovementAcoustic`, converted AUP to runtime space, and packed velocity-scaled radius into the fixed `_BiolumTouchRipples` buffer.
Rejected Alternatives: Inventing a new signal was rejected because 20+ agents are running and direct dependency creation would break decoupling. Spawning ripple MonoBehaviours was rejected as GC/object churn.
Scalability potential: Low disables shader ripple reads. Middle keeps fixed 16 wake points. High/Ultra can run the same cap with stronger emissive flash without CPU growth.
Hardware Impact: On i3/MX350 this avoids spawned objects and keeps ripple staging at 0 B/frame; estimated 35 us/frame saved versus object ripples.

## Decision 5 - Predator Blackout As Global Fake

Problem: Per-plant predator fear checks would scale with flora count and break the 0.1 ms suspicion budget.
Solution: Query `WorldSpatialHashGrid` once around the camera, filter apex predators, run a Burst `IJobParallelFor` proximity score, and fade the global intensity to 0.1 over two seconds.
Rejected Alternatives: Per-coral or per-zone predator checks were rejected as O(flora). Physics overlaps were rejected because the spatial hash already owns broadphase intent.
Scalability potential: Low still gets the blackout through one global scalar. Middle/High/Ultra can spend saved cycles on higher emissive range and ripple visibility.
Hardware Impact: On i3/MX350 this shifts many plant checks into one bounded 16-entry job; estimated 50 us/frame saved in dense plant fields.

## Decision 6 - Shader Globalization And Math LOD

Problem: Coral materials owned pulse amplitude/frequency, causing desynced glow and material-state churn.
Solution: Removed per-material pulse props from `Hecton_CoralMaster.shader`; shader now reads `_BiolumMasterPhase`, `_BiolumIntensity`, and `_BiolumTouchRipples`, using `dot(diff,diff)` inverse-square flash and `_MATH_LOD_LOW` to skip the ripple loop.
Rejected Alternatives: Keeping authored pulse properties was rejected because it preserves material divergence. Using `distance()` was rejected due sqrt cost.
Scalability potential: Low uses global phase only. Middle uses bounded ripples. High/Ultra get synchronized pulse plus touch flashes up to 3.0x with no material churn.
Hardware Impact: On i3/MX350 low-tier path skips 16 ripple samples; estimated 60 us/frame GPU saved when dense coral is visible.

## Decision 7 - AUP Shift Safety

Problem: Ripple positions are runtime world-space; floating-origin shifts would otherwise leave luminous trails torn from their source.
Solution: Implemented `IOriginShiftListener` and subtract `OriginShiftEventData.ShiftOffset` from active ripple positions after completing any outstanding jobs.
Rejected Alternatives: Draining `AupShiftSignal` was rejected because another system already consumes that shared queue. Recomputing from stale transforms was rejected because wake sources may be gone.
Scalability potential: All tiers preserve ripple correctness; High/Ultra can keep stronger trails without visible origin tearing.
Hardware Impact: Negligible frame cost; prevents visual corruption after large-coordinate shifts on low-end silicon.

## Decision 8 - Telemetry And Compile Evidence

Problem: Biolum is now a critical visual system and must not fail as a black box. Compile proof is also required, but the repo has external missing-type blockers.
Solution: Added a 300-frame `NativeArray<BiolumTelemetryEntry>` ring, publish `ActiveBiolumRipples` through `GlobalTelemetryBus`, and dump to `Docs/AgentLogs/Dump_BIOLUMINESCENCE_DIRECTOR.bin` on invalid math/input. Ran two build passes and recorded external blockers.
Rejected Alternatives: Logging only to chat was rejected. Claiming a clean compile was rejected because `Hecton8.Bootstrap.Contracts` and unrelated Core files fail first.
Scalability potential: Low telemetry records the global-only path. Middle/High/Ultra record ripple count, blackout, daylight, and intensity for postmortem scaling decisions.
Hardware Impact: Telemetry is fixed-size persistent memory; runtime publish is bounded and avoids allocations. The compile wall is unrelated to MX350 runtime cost.

## Decision 9 - OMEGA Purge

Problem: The synchronized biolum path must not smuggle slow math, material churn, per-object updates, or managed allocations back into the lighting domain after the functional tasks are complete.
Solution: Read the polish mandate only after all task boxes were checked/blocked. Audited touched files for `foreach`, LINQ, `ToString`, `distance`, `sqrt`, and `normalize`; removed the new predator fade division by using a compile-time reciprocal constant; kept shader ripple math on `dot(diff,diff)` plus `rcp`.
Rejected Alternatives: Broad refactoring of legacy zone/sonar code was rejected because it is outside this prompt and would risk unrelated behavior. Reintroducing material pulse properties was rejected because global synchronization is the core requirement.
Scalability potential: Low uses no ripple shader loop. Middle uses the fixed 16-ripple global buffer. High gets synchronized blackout/touch flashes. Ultra can raise visual response using the same bounded CPU path.
Hardware Impact: Low-end i3/MX350 path avoids per-material pulse updates, low-tier skips ripple sampling, and all tiers retain the fixed 300-frame blackbox. Ledgered estimate: 315 us/frame risk avoided versus the rejected per-object/material paths; measured player profiler data is unavailable until external compile blockers are cleared.

## Decision 10 - Continuation Audit Without Build

Problem: User requested another professional recheck and explicitly forbade `dotnet build`. Static review found real architecture debt left in the first pass: the ripple distance job did not feed nearest-first uploads, normal Tick completion used a helper that warns outside dispatcher swap windows, low-tier/count-zero frames still uploaded the touch buffer, and repeated NaN input could trigger repeated cold dump I/O.
Solution: Kept scope inside biolum. `RippleDistanceJob` completion now insertion-sorts fixed slot indices for nearest-first upload. Normal Tick uses `DispatcherJobSwap.TryFinalizeCompleted`; force completion remains only for teardown/origin-shift barriers. Touch ripples use two `GraphicsBuffer` instances and only upload on high-tier active ripple frames. Shader radius gating now uses `step(distSq, radiusSq)` before inverse-square flash. NaN paths sanitize rendering/telemetry writes and throttle dump export to one dump per 300 frames.
Rejected Alternatives: `dotnet build` was rejected by user instruction. A broad rewrite into new asmdefs or DataVault ownership was rejected because Task 3 is already dependency-blocked and would cross into integrator territory. Keeping the previous job as a decorative compliance job was rejected because it did not implement the "closest ripples" requirement.
Scalability potential: Low/MX350 publishes count zero and avoids ripple buffer uploads. Middle keeps fixed 16 ripples. High/Ultra get nearest-first touch flashes and double-buffered GPU writes without changing CPU complexity.
Hardware Impact: Low-end i3/MX350 avoids per-frame touch buffer upload when ripple shader work is disabled or empty. Estimated additional avoided GPU upload cost: 5-15 us/frame during low-tier idle/empty biolum frames. Measured proof absent because build/runtime profiling was forbidden in this pass.

## Decision 11 - Coral Variant Synchronization And Celestial Ownership

Problem: Continuation review found two concrete desync risks. `_BiolumIntensity.x` multiplied `CelestialRuntimeSnapshot.GlobalBiolumMultiplier` while the coral shader also multiplied `_HectonCelestialBiolumMultiplier`, squaring moon/eclipse boosts. The GPUInstancer coral shader still carried per-material pulse amplitude/frequency properties, so instanced coral would remain visually out of phase with the director.
Solution: Kept celestial brightness ownership in the celestial shader global and made `_BiolumIntensity.x` carry only director dimming: scalability scale, daylight mask, and predator blackout. Removed the stale `GlobalBiolumMultiplier` validity dependency from the director daylight/eclipse decision. Mirrored `_BiolumMasterPhase`, `_BiolumIntensity`, `_BiolumTouchRipples`, `_BiolumTouchRippleParams`, `_MATH_LOD_LOW`, and inverse-square ripple flash into `Hecton_CoralMaster_GPUI.shader`. Raised only the ForwardLit passes that bind the structured ripple buffer to shader target 4.5.
Rejected Alternatives: Leaving the doubled celestial multiplier was rejected because high-end eclipse/full-moon scenes would blow out authored coral balance and waste bloom budget. Ignoring the GPUI variant was rejected because dense coral fields are exactly where instancing matters. Replacing the structured buffer with material properties or vector arrays was rejected because the prompt requires `_BiolumTouchRipples` and the repo already has a `GraphicsBufferUploadUtility` path.
Scalability potential: Low/MX350 still publishes zero ripple count and compiles the low math branch. Middle uses global phase and bounded touch flashes. High/Ultra get synchronized instanced coral with the same visual overkill path as non-instanced coral, without per-material pulse divergence.
Hardware Impact: On i3/MX350 this removes the remaining instanced-coral material pulse path and prevents doubled celestial bloom from spending post-processing budget on overbright emissions. Estimated avoided cost: 5-20 us/frame in dense GPUI coral scenes plus avoided bloom overdraw spikes; measured profiler data remains unavailable because this pass intentionally did not launch `dotnet build`.

## Decision 12 - Biolum Shader Global Type Ownership

Problem: The new director owns `_BiolumIntensity` as a `float4`, but legacy `HectonBiolumController` still wrote the same name as a scalar and indirect vegetation culling read it with `Shader.GetGlobalFloat`. That can silently clobber the vector global after the director publishes it, breaking coral intensity, predator blackout, ripple count metadata, or daylight suppression depending on tick order.
Solution: Moved the legacy controller scalar publication to `_HectonLegacyBiolumIntensity` and left `_BiolumIntensity` exclusively under `HectonBiolumManager`. Updated `HectonIndirectVegetationRenderer` to resolve the scalar darkness-cull value from `Shader.GetGlobalVector(_BiolumIntensity).x` and pass that scalar explicitly into `FloraCulling.compute`.
Rejected Alternatives: Renaming the director vector was rejected because the batch prompt explicitly requires `_BiolumIntensity`. Letting both systems write the same property was rejected because global shader type collisions are order-dependent and hard to diagnose. Feeding legacy intensity into the culling fallback was rejected because it would bypass daytime shallow suppression.
Scalability potential: Low/MX350 culling now respects the same director dimming as coral rendering. Middle/High/Ultra keep synchronized vegetation visibility and emissive behavior without an extra global property lookup path.
Hardware Impact: No claimed CPU win. This prevents frame-order dependent visibility/emission errors and avoids wasted draw/bloom work when the director intentionally suppresses biolum in daylight shallows.

## Decision 13 - Abyssal Plant Pulse Unification

Problem: After coral synchronization, `Hecton_KelpMaster`, `Hecton_KelpMaster_GPUI`, and `Hecton_SargassumMaster` still carried `_BiolumPulseAmplitude` and `_BiolumPulseFrequency`. They would keep independent `_Time.y` material-authored pulse lanes while the director suppressed or blacked out coral, creating visible cross-species desync.
Solution: Removed the remaining material pulse properties and CBUFFER entries from those plant shaders. Kelp and GPUI kelp now derive their field/current wave from `_BiolumMasterPhase.x` with their existing spatial offsets and fixed authored amplitude. Sargassum derives bubble glow from `_BiolumMasterPhase.x` and multiplies final biolum by `_BiolumIntensity.x`. Touch ripple sampling was not added to these shaders in this pass to avoid extra buffer reads outside the coral task surface.
Rejected Alternatives: Leaving plant shaders untouched was rejected because the prompt explicitly describes corals and abyssal plants pulsing independently. Adding the 16-ripple structured buffer to every plant shader was rejected for this pass because kelp and sargassum are broader flora surfaces and would expand GPU cost beyond the verified coral path.
Scalability potential: Low/MX350 receives one global plant pulse and director dimming with no additional buffer loop. Middle gets synchronized kelp/sargassum glow. High/Ultra retain spatial phase variation while staying tied to the same global pulse.
Hardware Impact: Estimated 5-25 us/frame risk avoided in dense plant fields by removing residual material-authored pulse divergence and enabling daytime/predator suppression to affect plant emission consistently. Measured profiler data remains unavailable because `dotnet build` and runtime profiling were not launched.
