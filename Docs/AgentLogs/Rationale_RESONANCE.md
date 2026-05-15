# Rationale_RESONANCE

Agent: CORE_RESONANCE_ORCHESTRATOR
Domain: SYSTEMS_ARCHITECT / Resonance Orchestration
Status: ENGINE RESONATING / COMPILE BLOCKED BY DEPENDENCY

## Mandate Set

Selected mandates:

- `ARCH_Execution_Phases.txt`
- `ARCH_Signal_Lane_Segregation.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `AI_Flocking_Boids_Swarm_SpatialHash_Logic.txt`
- `CORE_Weather_Abyssal_FlowField_Currents.txt`
- `CORE_Submarine_Vehicles_Kinematics_AUP.txt`

## Decision 0 - Prompt Source

Problem: `Docs/Tasks/CURRENT_BATCH.md` does not contain this agent's XML block, but the user supplied the complete `<AGENT_PROMPT id="CORE_RESONANCE_ORCHESTRATOR">` in chat.

Solution: Treat the in-chat XML as the authoritative batch prompt for this run. Record the missing batch-file extraction as evidence instead of fabricating a prompt path.

Rejected Alternatives: Blocking on a missing batch-file block would stall the assignment. Reading neighboring prompts would violate strict parsing.

Scalability potential: Low tier gets no runtime change from this decision. High and Ultra tiers get cleaner architecture work because no unrelated prompt bleeds into the plan.

Hardware Impact: 0 microseconds at runtime. Documentation-only decision.

## Decision 1 - Rationale File Naming

Problem: Global protocol names `Rationale_[YourID].md`, while the XML prompt explicitly requires `Rationale_RESONANCE.md`.

Solution: Use `Rationale_RESONANCE.md` as the canonical rationale file because the task prompt names it directly. Maintain `Status_CORE_RESONANCE_ORCHESTRATOR.md` for the global state-machine protocol. Add `Rationale_CORE_RESONANCE_ORCHESTRATOR.md` as a pointer alias only, so the global anti-amnesia lookup resolves without duplicating journal content.

Rejected Alternatives: Creating only `Rationale_CORE_RESONANCE_ORCHESTRATOR.md` would miss the prompt-specific file. Duplicating full content into two rationale files would increase log drift risk.

Scalability potential: Low/Middle/High/Ultra unaffected at runtime.

Hardware Impact: 0 microseconds at runtime. Documentation-only decision.

## Decision 2 - Prompt Class Name Mismatch

Problem: The batch names `BoidController`, `AbyssalFlowField`, and `SubmarinePhysics`, but repo scan found no exact runtime classes with those names.

Solution: Bind the task to the actual owners: `SargassumMicroFaunaBoids` for GPU fauna, `HectonFluidEngine` plus `AbyssalFlowField.compute` for the 3D abyssal flow field, `HectonPlayerMovement` plus `PlayerKinematicsNativeState` for player movement native state, and `SubmarineFluidDynamics` for submarine physics/hydrodynamics.

Rejected Alternatives: Creating adapter classes with the prompt names would add fake authority and direct dependencies. Editing unrelated neighboring systems would violate domain boundaries.

Scalability potential: Low tier gets existing cached/fallback visuals. Middle/High/Ultra use the same owners with bucketed compute and richer active buckets.

Hardware Impact: 0 microseconds from naming. Prevents future integration waste by mapping to concrete code.

## Decision 3 - Fauna Bucketed Compute

Problem: Sargassum micro-fauna ran the main GPU simulation over every boid whenever a simulation step dispatched.

Solution: Feed `_SimulationBucketIndex` and `_SimulationBucketMask` from `ISimulationBucketer`/`GlobalRegistry.SimulationBucketer` and gate the main boid and PBD kernels to `index & 15`. Non-active boids copy their previous state to the write buffer to keep ping-pong buffers coherent. Renderer still receives `SimulationInterpolationAlpha`.

Rejected Alternatives: Rewriting spawn ownership or CPU-side boid lists would allocate and break the GPU-first architecture. Skipping whole simulation frames would be cheaper but not the required 1/16 boid slicing.

Scalability potential: Low uses 1/16 active boid math and ambient drift under VFX kill switch. Middle keeps bucketed full draw. High/Ultra spend saved compute on existing full LOD, PBD, leviathan/parasite behavior, and dense visual count.

Hardware Impact: Main boid/PBD math is reduced by roughly 93.75 percent per active frame slice. Estimated low-end i3/MX350 save: 180-550 microseconds depending active boid count and PBD density.

## Decision 4 - Abyssal Flow Bucketed Grid

Problem: The abyssal flow compute updated the full structured grid and full 32^3 texture volume per dispatch.

Solution: Add `_AbyssalFlowUpdateBucket` and `_AbyssalFlowUpdateBucketMask` and gate each flat voxel index to `index & 7`. Skipped texture voxels copy read to write so ping-pong textures do not stale-drop.

Rejected Alternatives: Reducing texture resolution globally would cheapen all tiers and kill high-tier overkill. CPU noise generation is slower and violates GPU ownership.

Scalability potential: Low updates 1/8 of flow voxels per frame. Middle/High retain wakes, splashdown, and thermocline detail. Ultra keeps full feature stack with time-sliced cost.

Hardware Impact: Flow noise/curl work reduced by roughly 87.5 percent per dispatch. Estimated i3/MX350 save: 120-320 microseconds when high-tier flow texture is active.

## Decision 5 - Kill Switch Degradation

Problem: Fauna and abyssal flow did not directly honor `SystemKillSwitchMask` VFX lane pressure.

Solution: Wire both systems to `GlobalRegistry.SystemKillSwitchLane4VfxMask`. Fauna drops to cached render/ambient drift by suppressing simulation dispatch. Abyssal flow ages impulse timers and leaves the previous published texture/buffer live instead of spending compute.

Rejected Alternatives: Disabling renderers would produce visible popping. Adding per-system globals would fork homeostasis authority.

Scalability potential: Low gets graceful cached motion. Middle recovers without visible hard off. High/Ultra resume full bucketed overkill once the mask clears.

Hardware Impact: Under kill switch, fauna GPU dispatch savings can exceed 250-700 microseconds, and abyssal flow dispatch savings can exceed 120-320 microseconds on low-end silicon.

## Decision 6 - DataVault Native Ownership

Problem: Player kinematic state, cinematic focus black box, and submarine hydrodynamic state still allocated persistent `NativeArray` blocks locally.

Solution: Extend stable `BufferID` and `SystemID` mappings, allocate those arrays from `IDataVault.GetBuffer<T>()`, and keep H8Memory local fallback only when the vault is unavailable. `OnDependencyInject()` now caches the DataVault pointer for player movement and player kinematic state.

Rejected Alternatives: Disposing vault-owned arrays from component teardown would corrupt global state. Leaving local arrays untouched would keep H-PHI Data Sovereignty bottleneck intact.

Scalability potential: Low avoids duplicate native blocks. Middle/High/Ultra gain unified buffer ownership for telemetry, player kinematics, and submarine hydrodynamic state.

Hardware Impact: Runtime loop savings are indirect: 0-20 microseconds from fewer cold-path ownership lookups, with memory fragmentation reduced by centralized vault allocation.

## Decision 7 - Renderer Interpolation Surface

Problem: Bucketed simulation can create visible stepping if render consumers cannot see bucket progress.

Solution: Expose `SimulationInterpolationAlpha` on fauna, push `_SimulationInterpolationAlpha` into the material property block, expose `GpuAbyssalFlowInterpolationAlpha` on fluids, and carry `AbyssalFlowInterpolationAlpha` in the fluid render-graph payload.

Rejected Alternatives: Smoothing boid positions on CPU would allocate or duplicate GPU state. Recomputing skipped flow voxels for interpolation would erase the point of bucketing.

Scalability potential: Low/Middle use cached alpha to hide bucket cadence. High/Ultra can bind the alpha in richer materials/compute consumers without a contract change.

Hardware Impact: 1-2 property writes when changed; under 3 microseconds estimated. Prevents visual jitter without restoring full simulation cost.

## Decision 8 - Compile Wall

Problem: Batch compile cannot reach this agent's edited Assembly-CSharp code because project compilation is already blocked upstream.

Solution: Record the dependency wall with exact failures: `Hecton8.Core.csproj` fails on `SaveMasterHashV10.cs` missing `xxHash3` and `PDAShellChrome.cs` missing `RefreshInventorySignalBinding`/`ConsumeInventoryChangedSignals`. `Assembly-CSharp` also cannot build in isolation because many generated dependency DLLs are absent unless the full project graph builds.

Rejected Alternatives: Editing unrelated save/UI dependencies would violate domain ownership. Reporting a green build would be fake.

Scalability potential: Runtime unaffected. Integration risk is transparent.

Hardware Impact: 0 microseconds. Verification blocker only.

## Decision 9 - Zero-GC Verification Scope

Problem: Runtime profiler evidence is unavailable because compile/playmode is blocked, but the new resonant loops still need static allocation proof.

Solution: Static-scan the edited hot paths for managed allocations, LINQ, `ToArray`, `ToList`, `FindObject`, and dynamic collection creation. New hot-path work is bitmask math, shader uniform writes, `Stopwatch.GetTimestamp`, and `RuntimeWatchdog.ReportSubsystemCost`; DataVault/H8Memory allocation helpers remain cold init/dispose paths.

Rejected Alternatives: Claiming profiler `0 B` without PlayMode proof would be fake. Moving cold allocations into hot paths would violate the Zero-GC mandate.

Scalability potential: Low avoids GC spikes during bucket pressure. Middle/High/Ultra preserve compute headroom for visuals instead of garbage collection.

Hardware Impact: 0 managed allocations in the edited loops by static proof. Estimated GC spike avoidance: unbounded frame hitch prevention rather than deterministic microsecond gain.

## Decision 10 - Signal Feedback Loop Audit

Problem: Recursive verification requires proving the resonance wiring did not create System A triggers System B triggers System A feedback loops.

Solution: Scan edited resonance files for `SignalBus<T>` writes and deferred-signal candidates. The touched systems contain frame-snapshot reads only in existing signal consumers. No new `SignalBus.Push` path was added, so no new loop requires `DeferredSignal`.

Rejected Alternatives: Adding deferred queues without a producer loop would add dead structure and future ambiguity. Claiming feedback safety without a grep-backed pass would be fake.

Scalability potential: Low/Middle avoid surprise frame spikes from recursive signal fan-out. High/Ultra retain deterministic signal flow while using saved compute for visuals.

Hardware Impact: 0 runtime microseconds added. Prevents unbounded signal cascade risk.

## Decision 11 - Omega Polish Mandate Absence

Problem: The protocol requires reading `<POLISH_MANDATE>` only after all core tasks are checked or blocked, but `CURRENT_BATCH.md` contains no such tag.

Solution: Record the missing tag as objective evidence and perform a manual anti-bloat pass against the edited systems, the loaded mandates, and the original in-chat task. No extra abstractions or cosmetic rewrites were added.

Rejected Alternatives: Inventing a polish mandate would violate strict parsing. Editing neighboring code for polish would cross domain boundaries.

Scalability potential: Low/Middle keep the smallest runtime surface. High/Ultra keep the same bucket contracts and can scale visual density without new dependency churn.

Hardware Impact: 0 runtime microseconds added. No bloat introduced during final pass.

## Decision 12 - Continued H-Phi Hardening Without Rebuild

Problem: Follow-up instruction required more honest H-Phi improvement while explicitly forbidding `dotnet build`. Static review found one remaining Sargassum population path reading `GlobalRegistry.EcosystemDirector` inside runtime population resolution.

Solution: Cache `IEcosystemDirectorService` beside the other Sargassum runtime services and resolve it through the existing dependency probe cadence. Hot population resolution now reads `_ecosystemDirector` directly.

Rejected Alternatives: Running `dotnet build` violated the user order. Moving ecosystem ownership into fauna would cross domain boundaries. Adding a new SignalBus lane for a private immediate query would violate signal discipline.

Scalability potential: Low/Middle avoid repeated service lookup in swarm budget refresh. High/Ultra keep the same ecosystem-driven population fidelity without extra hot-path registry traffic.

Hardware Impact: Estimated 1-4 microseconds saved during Sargassum population refresh on low-end silicon; 0 managed allocations added.

## Decision 13 - Submarine Cargo Mass Fallback Bucketing

Problem: `SubmarineFluidDynamics.FixedTick()` still called `RefreshCargoMassScalarFromGlobalCache()` every physics step, and that fallback read `GlobalRegistry.PlayerInventoryMassKg` even though the inventory system already publishes a queued event lane.

Solution: Keep the event lane authoritative. `EncumbranceChanged` now commits the payload mass directly, `InventoryChanged` forces one compatibility refresh because the coarse payload carries no mass, and the fixed-tick global fallback is limited to one out of sixteen frames after initial sync.

Rejected Alternatives: Removing the fallback entirely would break cargo mass if an inventory producer emits only coarse `InventoryChanged`. Polling `PlayerInventory` directly would reintroduce a concrete cross-domain dependency. Moving submarine cargo math into inventory would violate physics ownership.

Scalability potential: Low/Middle reduce global scalar traffic during submarine physics. High/Ultra keep full cargo buoyancy fidelity on event delivery and still have a bounded safety poll if an event is missed.

Hardware Impact: Estimated 1-3 microseconds saved per active submarine physics frame on i3/MX350 class hardware when inventory mass is stable; 0 managed allocations added.

## Decision 14 - Fluid Runtime Cache Teardown Hardening

Problem: `HectonFluidEngine` already cached its static runtime instance and actor contexts, but teardown only cleared DataVault and bucketer references. A stale fluid/player/submarine pointer after scene unload or domain reload would weaken H-Phi ownership and could route later static calls through a dead owner.

Solution: Clear `s_runtimeInstance` when the current fluid owner disables/destroys, and clear cached player/submarine runtime contexts beside the existing DataVault/bucketer nulling.

Rejected Alternatives: Re-reading `GlobalRegistry.Fluid` on every cavitation burst would fix stale owner risk but would put registry traffic back into the static hot path. Leaving teardown as-is relies on Unity object fake-null behavior instead of explicit ownership release.

Scalability potential: Low/Middle avoid stale fluid static routes during scene churn. High/Ultra preserve cached cavitation/static entrypoint behavior while keeping reload and duplicate-owner cleanup deterministic.

Hardware Impact: Runtime cost is 0 in active loops. Teardown adds four reference stores and two identity checks; hot-path benefit is preserving the existing cached static route without stale-owner risk.

## Decision 15 - Submarine Fluid Service and Math-LOD Cache

Problem: `SubmarineFluidDynamics` still resolved `GlobalRegistry.PowerGrid` during deep-freeze supply checks and read `GlobalRegistry.ScalabilityTier` while publishing flood-state math LOD. Both are small, repeated registry touches inside the physics/signal cadence.

Solution: Cache `IPowerGridService` through the existing runtime-context pattern, clear it with player/submarine/fluid service caches on teardown, and move flood-state math LOD behind a per-frame byte cache. The signal payload still publishes the same high/low math flag, but the publish path no longer samples scalability directly every call.

Rejected Alternatives: Registering `SubmarineFluidDynamics` on `ScalabilityEvents` would consume fixed listener capacity for one metadata byte. Polling the power grid every fixed step was simpler but keeps a direct service lookup in deep-freeze logic. Removing the fallback registry read entirely would break late service registration and scene reload.

Scalability potential: Low tier keeps the cheapest flood-state metadata and power-starvation fake while skipping repeated service lookups. Middle keeps deterministic event output. High and Ultra keep the same hydro/flood fidelity and spend saved registry traffic on visible fluid and damage work.

Hardware Impact: Estimated 1-3 microseconds saved in submarine flood/deep-freeze frames on i3/MX350 class hardware when the power grid and quality tier are stable. Active-loop GC impact remains 0 by static scan.

## Decision 16 - Editor Build Dependency Debt

Problem: `Hecton8.Editor.csproj` failed on `KinematicGhostDebugger` because the generated editor project did not carry `Unity.Mathematics`, while the editor window used `double3` and `math.lengthsq`.

Solution: Keep the diagnostic editor tool on existing Vector3 APIs: `HectonMapMagicVegetationBridge.ToUniverseSpace()` and `HectonFloatingOrigin.ToAbsoluteUniversePosition()`. This removes the stale generated-project dependency without touching generated `.csproj` files.

Rejected Alternatives: Editing generated `.csproj` files would be overwritten by Unity. Adding a new asmdef dependency for one editor visualization helper increases graph coupling. Touching plugin/package code to satisfy a Hecton editor tool would cross ownership.

Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged because the fix is editor-only. It reduces build graph coupling and keeps editor diagnostics available without adding runtime weight.

Hardware Impact: 0 runtime microseconds. Editor compile reliability improved; the tool still allocates only its existing cold history arrays.

## Decision 17 - Generated Graph vs Source Compile Gate

Problem: Full generated Unity project-reference traversal for `Assembly-CSharp.csproj` and later `Hecton8.Editor.csproj` exited `-1` with no compiler/MSBuild diagnostic after child-project traversal, sometimes leaving orphaned `dotnet` processes.

Solution: Build child outputs serially, then use `BuildProjectReferences=false` for source assembly gates. `Hecton8.Editor`, `Assembly-CSharp-firstpass`, and `Assembly-CSharp` compile green with `0 Warning(s)` and `0 Error(s)` under that deterministic gate.

Rejected Alternatives: Claiming the full graph green would be false. Continuing infinite full-graph retries wastes time and leaves stale processes. Editing vendor/generated project files would create churn outside the resonance domain.

Scalability potential: Build pipeline determinism protects all tiers by preventing broken editor tooling from masking runtime source status. Runtime H-Phi is unchanged by the build-gate tactic.

Hardware Impact: 0 runtime microseconds. Developer-loop impact is material: direct source gates finish in roughly 28-51 seconds each instead of non-diagnostic full-graph exits after minutes.
