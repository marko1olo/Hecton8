# Rationale_UNKNOWN

## 2026-05-25 - Architecture Audit Setup

Problem: User requested a detailed, proof-backed audit of the last-three-day architecture direction, including what changed, what claims were made, what remains, and whether the direction is technically correct.

Solution: Treat this as a static-source architecture audit, not a build/noise review. Use git history/diff, project mandates, architecture docs, source grep, direct file reads, and independent read-only subagent audits. Record final evidence in `Docs/AgentLogs/LOG_UNKNOWN.md`.

Rejected Alternatives: 
- Chat-only summary: rejected because project protocol says CTO reads logs and user explicitly requested logs.
- Green-build verdict: rejected because user said build/noise is not the primary concern and AGENTS.md states runtime proof requires Unity/profiler/player artifacts.
- Guessing from ledger prose: rejected because ledger claims must be cross-checked against current source.

Scalability potential: Audit will distinguish allowed quality scaling for presentation/cadence/capacity from forbidden changes to gameplay truth, DTO layout, save identity, or authority routes.

Hardware Impact: No frame-time saving will be claimed without profiler proof. Static audit may identify risk surfaces that can cause spikes or correctness failures on low-end i3/MX350 class hardware.

## 2026-05-25 - Deep Audit Decisions

Problem: A shallow first pass risked answering from visible "cleanup" signals only. The user explicitly required a deeper proof-backed architecture audit covering what changed, which claims are stale, which objections remain, and whether the direction is sane.

Solution: Split evidence by class. Use git history for timeline/diff scale, source grep for current facts, docs for required proof standards, and independent read-only subagents for GlobalRegistry, SignalBus, DataVault, and timeline cross-checks. Append the final report to `Docs/AgentLogs/LOG_UNKNOWN.md`.

Rejected Alternatives:
- Counting green build or warning state as success: rejected because the user deprioritized build/noise and the project doctrine requires route/runtime proof for architecture GREEN.
- Trusting docs ledger closure lines: rejected because `?? GlobalRegistry` current source still has 3 hits and frame/quality closures are only partial.
- Treating new interfaces as automatic decoupling: rejected because several interfaces are command sinks, concrete leaks, managed callbacks, or mutating read models.
- Treating `TryAcquireWriteLock` as a job relocation pin: rejected because DataVault defrag/growth checks block lock bits/pins, while plain write locks do not prove stable addresses.

Problem: Earlier draft language overstated one HazardZone finding as mutable public read access.

Solution: Corrected the final log. Current `HazardZoneManager` public wrapper uses `TryReadOnlyHandle` at source line 359. The remaining RED finding is hot public read resolving DataVault state plus scheduled vault views held through write locks that are not relocation pins.

Rejected Alternatives: Leaving the stronger stale claim in place. It was not supported by current source.

Scalability potential:
- Low: must avoid lazy SignalBus allocation and DataVault relocation hazards because both can create unpredictable stalls/corruption on weak CPUs/GPUs.
- Middle: typed SignalBus and cached owner interfaces can scale if prewarmed and route-owned.
- High: continuous quality can buy extra visual/cadence work after truth ownership is invariant.
- Ultra: visual overkill is acceptable only after quality no longer changes save-visible/cartography/construction/AI truth.

Hardware Impact: 0 microseconds claimed. Static audit identified risk surfaces only. Any exact time-saving claim without profiler/player data would be fabricated.

## 2026-05-25 - Architecture Fix Loop 1

Problem: DataVault write locks were not enough to block arena relocation; owner/type checks were dev-only on read paths.

Solution: Move handle owner/type/length checks out of `ENABLE_UNITY_COLLECTIONS_CHECKS`; make `TryAcquireWriteLock` also set the block lock bit/refcount used by defrag/growth gates; release the block lock before clearing writer ownership.

Rejected Alternatives:
- Treating `ActiveWriterSystemID` as a relocation pin: rejected because defrag/growth checks `VaultArenaBlock.Reserved0/Reserved1`.
- Adding a second global pin table: rejected because the block already has the exact lock fields the arena movement code respects.

Scalability potential: Low tier avoids corruption/stalls from moving a buffer under scheduled work. Ultra tier keeps DataVault defrag available when buffers are not actively owned.

Hardware Impact: 0 microseconds claimed. Expected value is correctness and fewer catastrophic stalls, not a measured frame saving.

Problem: Hot/static helpers and UI/build actions still used `GlobalRegistry` as a fallback route.

Solution: Cache route-owned service interfaces on registration/hot-swap (`MigrationDirector`, `PDAExchangeSystem`, `AutonomousExtractorSystem`, construction UI/weld target) and use runtime-local static owner references for legacy static helpers.

Rejected Alternatives:
- Leaving `?? GlobalRegistry` as a convenience fallback: rejected because it hides authority and can turn read/action paths into service-locator polling.
- Inventing new dependencies between systems: rejected; existing GlobalRegistry hot-swap interfaces were enough.

Scalability potential: Low tier avoids repeated managed lookup routes; higher tiers keep same truth route while spending saved budget on visuals.

Hardware Impact: 0 microseconds claimed without profiler.

Problem: `IEmergencyRelayRouteReadModel` mutated relay cache/discovery/guidance state inside read-named accessors.

Solution: Move relay registry refresh to owner/event paths via `EmergencyServiceRelayDirector.NotifyRelayRegistryChanged`; keep read model methods snapshot-only; move duplicate guidance suppression to `FirstHourDirector`, the publisher owner.

Rejected Alternatives:
- Keeping duplicate suppression in `TryBuildContextualGuidanceMessageSpan`: rejected because it made a read model mutate global guidance state.
- Rebuilding relay caches from `Has*`/`Get*`/`TryRead*`: rejected by read-accessor purity.

Scalability potential: Low tier gets predictable O(1-ish) reads from cached dictionaries; Ultra tier can add richer relay presentation without changing route truth.

Hardware Impact: 0 microseconds claimed.

## 2026-05-25 - Architecture Fix Loop 4 Closure

Problem: Build verification exposed several remaining contract leaks after the direct job-fence and quality cleanup. Gameplay/Core code was still crossing into physics facade helpers, bootstrap contracts were pointed at the runtime logger, `BaseModule` called `PhysicsApplySystem` statics after the `Hecton8.Physics` import was removed, and crash telemetry tried to call a pose method on the concrete `PlayerRuntimeContext` instead of the `IPlayerRuntimeContext` contract.

Solution: Replace gameplay consumers with `CoreDeterminismSignals`, build `KccVelocitySignal` explicitly for the core ABI, keep `BootstrapStatus` on `UnityEngine.Debug.LogError`, route breach vortex/implosion through new `IPhysicsService` methods implemented by `PhysicsApplySystem`, and use `PlayerRuntimeContextService.ActiveRuntimeContext` as `IPlayerRuntimeContext` for crash telemetry. The FirstHour cooldown write now resolves `SystemDispatcher.CurrentUnscaledTimeSeconds` inside the publisher method instead of using an out-of-scope caller local.

Rejected Alternatives:
- Re-adding `using Hecton8.Physics` to gameplay files: rejected because it restores the namespace wall breach that the compile failure exposed.
- Making bootstrap contracts depend on `H8Debug`: rejected because bootstrap contracts must compile without the runtime logger implementation.
- Calling `PhysicsApplySystem` statics from `BaseModule`: rejected because it creates a concrete route from gameplay into the physics implementation; `IPhysicsService` is the existing route owner.
- Using the concrete `PlayerRuntimeContext` for pose reads: rejected because the pose snapshot contract is exposed by `IPlayerRuntimeContext`.

Scalability potential:
- Low: fewer concrete cross-domain lookups and static implementation calls on hot gameplay paths.
- Middle: pressure/implosion visuals route through the same physics owner as deferred force packets.
- High: richer breach/implosion presentation can be expanded behind `IPhysicsService` without coupling construction/base code to physics internals.
- Ultra: visual overkill remains a physics-owner choice, not a gameplay module static call.

Hardware Impact: 0 microseconds claimed. These are route/compile correctness fixes; no Unity Profiler/player capture was run.

Problem: Verification had to run in a shared dirty worktree with concurrent edits and strict CPU/compiler gates.

Solution: Rechecked CPU and `dotnet/csc` before each build. When stale/concurrent SpatialAudioManager errors appeared, current source was rechecked and `Hecton8.Core` was rerun before the final Assembly pass. Final verified state: Core pass 0/0, Bootstrap.Contracts pass 0/0, Assembly-CSharp pass 0 errors with the known two missing `Hecton8.Input.csproj` warnings.

Rejected Alternatives:
- Treating transient stale compile walls as final source state: rejected after current source scans showed the missing members already present.
- Running builds during 90-100% CPU windows: rejected by AGENTS build discipline.

Scalability potential: Build discipline protects shared-agent throughput; route fixes keep low-tier runtime from paying for accidental concrete owner searches.

Hardware Impact: 0 microseconds claimed.

## 2026-05-25 - Architecture Fix Loop 4

Problem: Remaining reviewed architecture risks included direct `JobHandle.Complete()` calls on teardown/finalization paths, binary `LowTier` quality in marauder outpost generation, and discrete quality switches in debug/sonar presentation. The direct completes were not all hot same-frame waits, but they bypassed the project's central fence helper and made future audit harder. The outpost `LowTier` route directly changed generated cell dimensions, room density, support height, descriptor flags, and material decay input.

Solution: Route verified job completions through `DispatcherJobFence` in outpost teardown, voxel GPU upload finalization/release, simulation bucket rebalance, H8Memory owner teardown, and voxel SDF async publish completion. Replace `MarauderOutpostSolveJob.LowTier` and `MarauderOutpostMatrixExtractionJob.LowTier` with `GlobalQualityWeight`, continuous smooth curves, and interpolated thresholds/caps. Cache `_generationQualityWeight01` at generation start and derive active dimensions with a continuous curve. Replace chemical debug draw step and submarine sonar refresh with continuous source curves.

Rejected Alternatives:
- Changing Flora genome hardware tier or public quality DTO layouts: rejected because the tier is stored in data/job payloads and needs a separate ABI migration proof.
- Rewriting Tether/Visor quality enums in this pass: rejected because current use is mostly telemetry/presentation and consumer shader/API expectations need proof first.
- Leaving `.Complete()` calls in local helper wrappers: rejected because the project already has `DispatcherJobFence` as the central proof point for forced versus completed-only finalization.
- Claiming exact frame savings: rejected because no Unity profiler or player capture has been run.

Scalability potential:
- Low: outpost generation now scales density/dimensions/support height from a continuous weight instead of snapping to a low/full binary shape.
- Middle: sonar/debug cadence/step changes remain smooth enough to avoid visible thresholds where possible.
- High: higher quality buys more generated presentation density without changing the ownership route.
- Ultra: visual overkill can increase outpost shell density while job completion remains auditable through one fence helper.

Hardware Impact: 0 microseconds claimed. Static source proof only. Build was not launched when sampled CPU was 88%, per the project build gate.

## 2026-05-25 - Architecture Fix Loop 3

Problem: After Loop 2, remaining dispatcher-owned frame stamps still existed in global registry signals, GC/memory sampling, platform scalability events, prologue signal consumption, mod sandbox quotas/dumps, QA determinism bots, voxel budgets, save compression throttles, inventory command signals, vegetation telemetry, impostor cache cadence, and world residency dispatch budgets. These were not render-only frames; they are owner cadence, telemetry, quotas, or black-box evidence.

Solution: Convert those verified owner/diagnostic frame stamps to `SystemDispatcher.CurrentFrameId` or `SystemDispatcher.CurrentFrameIndex` in 26 source files. Keep the one remaining `Time.frameCount` in the edited set only where it drives a brownout triangle-wave visual fake in `ScreenSpaceLightShaftRuntime`.

Rejected Alternatives:
- Replace every raw frame use in the touched files: rejected because the light-shaft brownout phase is presentation-only visual fake, not authority.
- Leave mod/QA quota frames on Unity render frame: rejected because command flood, heap quota, crash, and determinism evidence must line up with dispatcher-owned telemetry.
- Add another frame abstraction wrapper: rejected because `SystemDispatcher` already exposes the project frame authority.

Scalability potential:
- Low: QA, mod, save, and telemetry throttles stay correlated even when systems shed work or cadence is reduced.
- Middle: owner caches and quotas use the same frame id as dispatcher snapshots.
- High: richer diagnostic and visual telemetry can be correlated without changing gameplay truth.
- Ultra: visual overkill can increase presentation density while authority and black-box stamps stay invariant.

Hardware Impact: 0 microseconds claimed. This is cadence/evidence consistency, not measured performance work.

Problem: Compile failed because `IPhysicsImpactMaterialProvider` is a contract in `Hecton8.Core.Contracts`, while several consumers in item/module/physics surfaces resolved the unqualified name toward the `Hecton8.Physics` runtime namespace. That makes the source fragile across assemblies and generated csproj order.

Solution: Explicitly bind implementations and runtime physics queries to `Hecton8.Core.Contracts.IPhysicsImpactMaterialProvider`. Restore/import `Hecton8.Physics` only for the actual `BuoyancyObject` type where item classes need it.

Rejected Alternatives:
- Duplicate `IPhysicsImpactMaterialProvider` under `Hecton8.Physics`: rejected because it creates two facts/two owners for one material metadata contract.
- Move the contract out of Core.Contracts: rejected because AI/audio/VFX consumers need the contract without depending on physics runtime implementation.
- Ignore the Core build failure as unrelated: rejected because compile failure blocks the current work regardless of source of breakage.

Scalability potential: Keeps impact material metadata as a stable cross-domain contract. Physics runtime remains implementation owner; consumers see only contract metadata.

Hardware Impact: 0 microseconds claimed. Compile correctness and contract ownership only.

Problem: Verification needed clean source evidence after touching 31 source files.

Solution: Ran targeted residual scan and builds under the CPU/dotnet gate. `rg -n "Time\.frameCount"` over the 31 edited source files reports only `ScreenSpaceLightShaftRuntime.cs:789`, the intentional visual triangle-wave stutter. `git diff --check` has no whitespace errors, only line-ending warnings. `Hecton8.Core.csproj` passed with 0 warnings/0 errors. `Assembly-CSharp.csproj` passed with 0 errors and the pre-existing two `Hecton8.Input.csproj` missing-reference warnings.

Rejected Alternatives:
- Claiming Unity/player/profiler verification: rejected because only dotnet/static proof exists in this loop.
- Claiming exact microseconds: rejected because no profiler/player capture was run.

Scalability potential: Static and compile evidence covers the edited source, but runtime scalability remains pending Unity/profiler proof.

Hardware Impact: 0 microseconds claimed.

## 2026-05-25 - Architecture Fix Loop 2 Closure

Problem: `Assembly-CSharp` verification initially failed because `PlayerCriticalProceduralAudioRenderer` still read named fields that are not present on `PhysicsEventPayload` (`Volume01`, `PitchScale`). The actual payload ABI stores those mapped values in scalar slots.

Solution: Read `Scalar1` for acoustic impulse volume and `Scalar2` for pitch scale, matching the producer-side mapping in `PhysicsApplySystem.TryNotifyAcousticImpulse`.

Rejected Alternatives:
- Adding new fields to `PhysicsEventPayload`: rejected because it would widen a shared event payload ABI for one consumer.
- Guessing another scalar mapping: rejected after checking producer code.

Scalability potential: Keeps the compact scalar payload route intact across Low, Middle, High, and Ultra without expanding event memory or changing gameplay authority.

Hardware Impact: 0 microseconds claimed. This was a compile/ABI correctness repair.

Problem: Verification had to obey the project build gate after CPU stayed above 50% and compiler processes were active.

Solution: Waited for `dotnet/csc` to reach zero and CPU to fall below 50%, then ran a single `dotnet build Assembly-CSharp.csproj --no-restore -m:1 -v:minimal`. Result: PASS, 0 errors, 2 pre-existing warnings for missing `Hecton8.Input.csproj` references.

Rejected Alternatives:
- Starting a build during the 72-97% CPU window: rejected by AGENTS build discipline.
- Treating the two `Hecton8.Input.csproj` warnings as loop regressions: rejected because the same warnings existed before this pass and the compile output produced `Assembly-CSharp.dll`.

Scalability potential: Build discipline protects the shared 20+ agent environment from compile contention.

Hardware Impact: 0 microseconds claimed.

Problem: A blind replacement of all remaining raw Unity frame reads would corrupt local render/origin-shift semantics.

Solution: Reran targeted static grep over the 36 edited source files. Remaining `Time.frameCount` hits are intentionally limited to `InputDispatcher` input-to-render latency pairing and `FaunaBrain` same-frame pause around floating-origin shift state. Both are local Unity-frame semantics, not dispatcher-owned black-box/signal/telemetry stamps.

Rejected Alternatives:
- Replacing those four remaining references with `SystemDispatcher.CurrentFrameId`: rejected because input latency is measured against render frame, and floating-origin locks currently publish Unity frame identity.
- Declaring global closure of all raw frame use: rejected because this loop only closed the verified dispatcher-owned subset.

Scalability potential: Low devices keep diagnostic/simulation frame evidence aligned to dispatcher frames while render-latency and origin-shift locks remain semantically local. Higher tiers can add visual telemetry without changing authority frame ownership.

Hardware Impact: 0 microseconds claimed.

## 2026-05-25 - Architecture Fix Loop 2

Problem: Loop 1 removed many `Time.frameCount` stamps, but dispatcher-adjacent telemetry, cache throttles, overflow guards, and black-box writers still mixed Unity frame count with `SystemDispatcher.CurrentFrameId/CurrentFrameIndex`. That makes postmortem evidence and signal de-duplication hard to compare across dispatcher buckets, QA bots, runtime caches, and visual systems.

Solution: Convert only verified dispatcher-owned frame stamps to `Hecton8.Core.SystemDispatcher.CurrentFrameId` or `CurrentFrameIndex`. Scope includes construction logistics/drone/docking, ItemCatalog LRU frames, raycast batch reset, QA endurance input/telemetry, atmosphere/weather/celestial warnings, fluid splashdown/advection/ocean dumps, fauna cognition/cache cadence, Sargassum and biolum telemetry/caches, destructible organic black-box/job cadence, LOD/scatter/debris refresh, material decay acoustic frames, VR somatic black box, input dispatcher cache/retry frames, runtime/frame watchdogs, DOD replay, noise/scan event overflow telemetry, submarine queues, voxel delta budgets, player kinematic cadence, scatter helper cache, and performance monitor snapshots.

Rejected Alternatives:
- Replacing every raw `Time.frameCount`: rejected because XR input latency uses input-to-render frame semantics and Fauna origin-shift pause still mirrors `HectonFloatingOrigin`/`SystemDispatcher` Unity-frame shift locks.
- Adding a helper wrapper: rejected because `SystemDispatcher` already exposes explicit public frame authority and a wrapper would add another route without need.
- Claiming performance savings: rejected because this is consistency/correctness work; no profiler or player capture has been run.

Scalability potential:
- Low: black-box and throttled telemetry stay comparable even when dispatcher buckets shed work on MX350-class hardware.
- Middle: cache refresh and overflow guards use the same owner cadence as simulation consumers.
- High: richer visual telemetry can be correlated with authoritative frame ids.
- Ultra: sensory overkill can add more visual evidence without making gameplay truth depend on Unity render frame cadence.

Hardware Impact: 0 microseconds claimed. Expected value is deterministic diagnostics and fewer first-use/cadence mismatches, not measured frame-time reduction.

Problem: Build verification cannot legally run while `dotnet`/`csc` are already active and CPU is over the 50% gate.

Solution: Hold compile until the AGENTS build gate clears; continue static review and targeted fixes meanwhile.

Rejected Alternatives:
- Launching another `dotnet build`: rejected by explicit project rule and because previous loop already recorded the cost of violating that discipline.

Scalability potential: Avoids stealing CPU from active compiles/agents on the shared machine.

Hardware Impact: 0 microseconds claimed.

Problem: Some typed SignalBus lanes still allocated lazily on first publish.

Solution: Prewarm core scanner lanes in `GlobalSignals.InitializeAllQueues`; prewarm domain-local `PlayVoiceOverSignal` and `ThermalUpdraftSignal` in their cold owner initialization paths.

Rejected Alternatives:
- Pulling thermodynamics types into Core bootstrap: rejected because it would couple Core to a domain assembly.
- Accepting first-publish allocation: rejected because signal lanes are hot broadcast paths.

Scalability potential: Low tier avoids first-use allocation spikes; Ultra tier can increase visual/event density through configured lane budgets.

Hardware Impact: 0 microseconds claimed.

Problem: `GlobalQualityWeight` affected save/gameplay-visible truth in cartography discovery, foundation snapping solver accuracy, and habitat flood traversal/pressure approximation.

Solution: Keep quality for visual cadence/decimation/flare, but make discovery masks, sonar SDF shell, foundation ray/march/interpolation, graph flood budget, and pressure root lookup deterministic across quality levels.

Rejected Alternatives:
- Treating continuous quality usage as always compliant: rejected because doctrine forbids quality changing gameplay truth, DTO identity, save identity, or authority routes.
- Low/Ultra binary branches for flood math: rejected because truth must be invariant, while visuals can scale separately.

Scalability potential: Low, Middle, High, Ultra now receive the same core construction/cartography/habitat truth; extra hardware should buy presentation, not different legality or flood outcomes.

Hardware Impact: 0 microseconds claimed; deterministic correctness was prioritized over pretending a perf win.

Problem: Multiple telemetry/signal/black-box payloads still stamped `Time.frameCount` directly.

Solution: Replace targeted payload frame stamps with `SystemDispatcher.CurrentFrameId` in ladder IK, foveated sim, docking, drone, lighting, scatter, abyssal thermal, biome SDF, vegetation sync, marine snow, fluid, submarine hydro, habitat flood, and QA endurance entries.

Rejected Alternatives:
- Replacing every local Unity frame timer blindly: rejected because some entries are local render/probe cooldowns and need separate ownership review.
- Leaving black-box frames on Unity frameCount: rejected because crash evidence should use the project frame authority.

Scalability potential: All tiers produce comparable telemetry frame IDs; Ultra load no longer changes crash timeline semantics.

Hardware Impact: 0 microseconds claimed.

Problem: Verification needed compile evidence, but the full solution build is contaminated by missing package/editor `project.assets.json` files and I briefly violated the local build discipline by launching two narrow dotnet builds in parallel through multi-tool.

Solution: Stop launching new builds until `dotnet/csc` dropped to zero; rerun only one gated runtime build at a time. `Hecton8.Core.csproj` passed with 0 warnings/0 errors. `Assembly-CSharp.csproj` passed with 0 errors and only the existing `Hecton8.Input.csproj` missing-reference warnings.

Rejected Alternatives:
- Claiming the `Hecton8.slnx` failure was caused by source edits: rejected because it failed on missing `project.assets.json` for editor/package projects after Core and Assembly-CSharp had compiled.
- Hiding the parallel-build mistake: rejected because the protocol requires honest evidence, not a clean-looking story.

Scalability potential: Compile evidence now covers the touched runtime/Core assemblies without adding package restore churn or another agent's build load.

Hardware Impact: 0 microseconds claimed.

## 2026-05-25 - Architecture Fix Loop 4 Closure

Problem: Final compile verification exposed concrete-route leaks after the quality and job-fence fixes. Gameplay/Core code was still using physics facade helpers, bootstrap contracts were pointed at the runtime logger, `BaseModule` called `PhysicsApplySystem` statics after the physics namespace import was removed, FirstHour guidance used an out-of-scope time local, and crash telemetry called a pose method on concrete `PlayerRuntimeContext` instead of `IPlayerRuntimeContext`.

Solution: Use `CoreDeterminismSignals` and explicit `KccVelocitySignal` payload construction, keep bootstrap contract logging on `UnityEngine.Debug.LogError`, add `IPhysicsService.QueueDepressurizationVortex` and `QueueImplosionImpulse`, route `BaseModule` through the physics service, resolve FirstHour cooldown time inside the publisher method, and read crash telemetry pose through `PlayerRuntimeContextService.ActiveRuntimeContext` as `IPlayerRuntimeContext`.

Rejected Alternatives:
- Re-adding direct `Hecton8.Physics` dependencies to gameplay: rejected because it reopens the namespace wall.
- Making bootstrap contracts depend on runtime `H8Debug`: rejected because that breaks contract assembly independence.
- Leaving `PhysicsApplySystem` static calls in `BaseModule`: rejected because gameplay should route through `IPhysicsService`.
- Claiming profiler savings: rejected because only static and dotnet compile proof exists.

Scalability potential:
- Low: fewer concrete cross-domain paths and fewer accidental static owner calls.
- Middle: breach/implosion presentation now uses the same physics owner route as force packets.
- High: richer physics-owner visual effects can be added without changing gameplay module dependencies.
- Ultra: visual overkill remains behind the physics service, not hardwired into base modules.

Hardware Impact: 0 microseconds claimed. Final proof is compile/static only: Core 0/0, Bootstrap.Contracts 0/0, Assembly-CSharp 0 errors with two known missing `Hecton8.Input.csproj` warnings.

## 2026-05-25 - Architecture Fix Loop 5 Closure

Problem: Runtime code still had residual architecture drift after the previous pass: dispatcher-owned frame evidence was mixed with Unity render frames, KCC velocity publication still had physics-facade and frame-source leftovers, some quality payloads wrote binary low-tier/tier values instead of continuous quality pressure, and QA headless code still had direct forced `JobHandle.Complete()` calls outside the first-party fence route.

Solution: Converted verified runtime frame stamps to `SystemDispatcher.CurrentFrameId/CurrentFrameIndex`, moved KCC velocity publication to `CoreDeterminismSignals` with explicit `KccVelocitySignal` payloads, encoded continuous quality as Q8 where ABI fields had to remain byte-sized, smoothed tether quality-driven visual iteration/damping weights, mapped mod/synthetic payload quality from continuous weight instead of hardcoded tier literals, and routed Jacobi headless forced job completions through `DispatcherJobFence`.

Rejected Alternatives:
- Replacing ABI field names globally: rejected because `LowTier`/`QualityTier` names exist in legacy DTOs and changing layouts without a route card risks save/mod breakage.
- Leaving the `SdfSqueezeJob.cs` missing from `Hecton8.Core.csproj`: rejected because the compile wall proved `PlayerKinematicsRuntime` consumes `SdfSqueezeResult/SdfSqueezeJob`.
- Launching builds during high CPU or active `dotnet/csc`: rejected by AGENTS build discipline; one Assembly-CSharp build timed out but was allowed to finish before rerunning with a longer timeout.
- Claiming profiler savings: rejected because this pass has static and dotnet compile proof, not Unity player/profiler captures.

Scalability potential:
- Low: runtime diagnostics, black-box evidence, cooldowns, and fuzzer completion paths use one owner route and avoid first-order frame-source ambiguity on weak devices.
- Middle: continuous quality pressure can reduce work/cadence without mutating gameplay truth or DTO size.
- High: tether/KCC/thermal presentation can increase fidelity smoothly instead of jumping across binary low/high switches.
- Ultra: visual overkill remains a presentation/cadence budget, not a gameplay authority route.

Hardware Impact: 0 microseconds claimed. Build/static proof only: `Hecton8.Core.csproj` passed 0 warnings/0 errors; `Assembly-CSharp.csproj` passed 0 errors with two pre-existing missing `Hecton8.Input.csproj` warnings.
