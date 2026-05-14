# Rationale_RESONANCE

Agent: CORE_RESONANCE_ORCHESTRATOR
Domain: SYSTEMS_ARCHITECT / Resonance Orchestration
Status: ACTIVE / PENDING VERIFICATION

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
