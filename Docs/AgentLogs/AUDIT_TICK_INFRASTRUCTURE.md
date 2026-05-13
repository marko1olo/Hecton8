# AUDIT_TICK_INFRASTRUCTURE

STATUS: AUDIT VERIFIED
Agent: ARCHITECTURAL_RECON_TICK_LOGIC
Role: TECHNICAL_AUDITOR
Date: 2026-05-13
Evidence class: STATIC_SOURCE plus STATIC_DOC. No Play Mode, profiler, GCMonitor, or player-build proof claimed.

## Mandatory CLI Scan

Executed scope:
- `Assets/_Project/Scripts/Core`
- `Docs`
- Follow-up runtime adoption scan over `Assets/_Project/Scripts`

Required commands executed or source-equivalent:
- `rg "ITickable|ISlowTickable|IColdTickable|IFrostTickable"`: runtime contracts found in `Assets/_Project/Scripts/ITickable.cs`; Core registration and lane plumbing found in `GlobalRegistry.cs` and `SystemDispatcher.cs`. Docs contain heavy archive noise.
- `rg "SystemDispatcher|TickManager"`: `SystemDispatcher.cs` is the real dispatcher. `GameTickManager.cs` still exists as a legacy bridge registered into dispatcher lanes.
- `rg "%\s*N|frameCount\s*%"`: no broad runtime `(entityID + frameCount) % N` bucket scheduler found. Runtime modulo hits are mostly diagnostics, double buffers, ring buffers, readback cadence, and one distant biolum LOD skip.
- `cat Assets/_Project/Scripts/Core/SystemDispatcher.cs`: read by CLI equivalent `Get-Content -Raw`; dispatcher source is present at the expected path.

## Frequency Analysis

Source-present cadence lanes:
- `SystemDispatcher.cs:62`: `FastTickIntervalSeconds = 1.0 / 60.0`.
- `SystemDispatcher.cs:63`: `SlowTickIntervalSeconds = 0.1` (10 Hz).
- `SystemDispatcher.cs:64`: `ColdTickIntervalSeconds = 1.0` (1 Hz).
- `SystemDispatcher.cs:65`: `FrostTickIntervalSeconds = 5.0` (0.2 Hz).
- `SystemDispatcher.cs:267-272`: dedicated accumulators for fast, slow, cold, frost, unscaled fast, and fixed step.
- `SystemDispatcher.cs:2263-2453`: accumulator-driven dispatch for FastTick, UnscaledFastTick, SlowTick, ColdTick, and FrostTick.

Important correction:
- `GameTickManager.cs` still has legacy `slowTickInterval = 0.5f` at `GameTickManager.cs:116` and a separate `_slowTickAccumulator` path at `GameTickManager.cs:427-438`.
- That means the project has two slow-tick concepts: new dispatcher SlowTick at 10 Hz and legacy GameTickManager slow tick at 2 Hz. Treat docs claiming only one slow cadence as stale unless they name the owner.

## Bucketing Trace

True source-present work slicing exists, but it is not mainly the naive modulo pattern.

Real simulation throttling:
- `FoveatedSimulationManager.cs` is the strongest implementation. It owns opt-in `IFoveatedSimulationTarget` targets, per-target `_tickAccumulators`, `_tickIntervals`, and `FoveatedTickRate` values.
- `FoveatedTickRate` spans `Center60Hz`, `Focus30Hz`, `Periphery20Hz`, `Far10Hz`, `Rear5Hz`, `Rear1Hz`, and `CulledEcosystemOnly`.
- `FoveatedSimulationManager.TryResolveTick(...)` skips a target until its accumulator reaches the resolved interval, then passes accumulated delta to the target.
- Adoption found through `FaunaBrain.Foveated.cs`; broad non-fauna adoption was not found in this pass.

Budgeted queue and drain slicing:
- `SystemDispatcher.cs`: late-frame event dispatch budget is `MaxLateFrameEventsPerFrame = 1000`; PDA event budget is `MaxPdaEventsPerFrame = 30`.
- `FoveatedSimulationManager.cs`: deferred raycast drain cap is `MaxDeferredRaycastCommandsPerFrame = 16`.
- `WorldChunkResidencyManager.cs`: tiered load dispatch budgets: low 1, middle 2, high 3, ultra 4 per processing pass.
- `ProceduralWreckGenerator.cs`: debris gravity is sliced with `_debrisGravityCursor` and `sliceCount`.
- `PersistentWorldRegistry.cs`: hydration has `MaxHydrationsPerFrame` and a frame-time deadline.
- `SargassumGlobalDragManager.cs`: pending event and debris petrification drains use bounded budgets per tick.

Naive modulo bucketing:
- No global `entityID + frameCount` modulo bucketing dispatcher was found.
- Runtime modulo hits include `HectonBoidController` double-buffer selection, `DodReplayRecorder` snapshot interval, physics/origin watchdog cadence, `GPUScatterDirector` readback stride, and `HectonBiolumZone` distant LOD skip every 3 frames.

## Interface Maturity

Managed timing interfaces found:
- `ITickDispatcher`: authoritative time/dilation service.
- `IFastTickable`: 60 Hz deterministic lane.
- `ITickable : IUpdatable`: per-frame managed tick lane.
- `IUpdatable`: zero-allocation update contract used by `SystemDispatcher`.
- `IFixedTickable`: fixed-step lane.
- `ISlowTickable`: 10 Hz dispatcher lane by current source, but legacy GameTickManager comments still describe older use.
- `IColdTickable`: 1 Hz lane.
- `IFrostTickable`: 0.2 Hz lane.
- `IUnscaledFastTickable`: 60 Hz unscaled UI/menu lane.
- `ILateFrameTickable`: end-of-frame swap/readback lane.
- `IPostFixedTickable`: post-fixed swap/recovery lane.
- Internal foveated interfaces: `IFoveatedDispatcher` and `IFoveatedSimulationTarget`.

Implementation maturity:
- These are managed C# interfaces dispatched through `RegistryBucket<T>` arrays.
- They are not Burst-compatible function pointers.
- Burst compatibility exists in the job layer, not the tick interface layer.
- Interface text hit counts across runtime source: `ITickable` 300, `ISlowTickable` 249, `IUpdatable` 231, `ILateFrameTickable` 99, `IFixedTickable` 72, `IPostFixedTickable` 32, `IFrostTickable` 21, `IFastTickable` 19, `IUnscaledFastTickable` 19, `IColdTickable` 17. These are references, not unique implementer counts.

## Job Admission Check

Agent 54 result is source-present:
- `JobAdmissionContracts.cs` defines six fixed admission lanes and `IJobAdmissionService`.
- `BurstTokenBucketJobAdmissionService.cs` implements token buckets with `NativeArray<float>` lane budgets, fixed EWMA cost table, critical-lane debt, VFX kill switch, AUP barrier, and a 300-entry blackbox ring.
- `GameBootstrapper.cs` constructs and registers `BurstTokenBucketJobAdmissionService` through `GlobalRegistry.JobAdmission`.
- `SystemDispatcher.Update()` calls `GlobalRegistry.JobAdmission?.Refill(...)` at the pre-simulation boundary.
- `JobAdmissionScheduleExtensions.cs` provides `TryScheduleAdmitted` and `TryScheduleParallelAdmitted`.

Adoption is partial:
- Admitted wrappers are used in `PredatorCognitionDomain.cs`, `HectonVoxelEngine.cs`, and `WorldChunkResidencyManager.cs`.
- Current source scan found 266 `.Schedule(` hits under `Assets/_Project/Scripts`; many remain naked schedules outside the admission wrapper.

Priority queue verdict:
- No general Burst job priority queue was found.
- The implemented mechanism is a fixed-lane token bucket, not a runtime priority queue.
- Other priority queues exist for unrelated gameplay/audio or content staging, not for global Burst job admission.

## Cross-Domain Adoption

AI / Fauna:
- `FaunaBrain` implements `IUpdatable`, `ITickable`, `IFixedTickable`, `ISlowTickable`, and `ILateFrameTickable`, but source scan found registration to `IUpdatable` and corpse late-frame only. Its foveated partial class is the real non-60Hz throttle path.
- `PredatorCognitionDomain` uses job admission wrappers for AI jobs.
- IK/Verlet fauna systems (`ProceduralLeviathanSpineIK`, `ProceduralCrabLegIKRuntime`, `LeviathanTentacleVerletSolver`) are still dispatcher per-frame plus late-frame owners. Not Unity `Update`, but still hot cadence.

Physics:
- `GlobalPhysicsStateManager`, `PhysicsApplySystem`, `BuoyancyObject`, and `RaycastBatchHelper` are fixed/post-fixed/late-frame owners. That is appropriate for physics truth; no Slow/Cold/Frost adoption found in core physics scan.
- No first-party Unity `FixedUpdate` outside dispatcher was found.

Fluids / Gas / Pipes:
- `FluidPipeGraphRuntime` uses `ISlowTickable` plus `ILateFrameTickable`. This is the clearest non-60Hz fluid/logistics adoption.
- `GasDynamicsSolver`, `HectonFluidEngine`, and `SubmarineFluidDynamics` use fixed/post-fixed. Those are not Unity `FixedUpdate`, but they still run at physics cadence.

Voxel / World:
- `VoxelDynamicNavGridRuntimeLifecycle` uses `ISlowTickable`.
- `HectonVoxelStreamingBridge` uses `IUpdatable` plus `ISlowTickable`.
- `HectonVoxelEngine` uses late-frame drivers and one admitted voxel bake wrapper, but the main voxel job chain still has many naked schedule calls.
- `VoxelDeltaProcessor` uses `IUpdatable` plus `ILateFrameTickable`; this is a cadence pressure point if per-frame work is not player-visible.

Architectural leak scan:
- Direct Unity message loops found in first-party runtime source: only `SystemDispatcher.Update()` and `SystemDispatcher.LateUpdate()`.
- No gameplay `Update`, `FixedUpdate`, or `LateUpdate` methods were found outside `SystemDispatcher`.
- Therefore the current leak class is not Unity-message bypass; it is overuse of dispatcher per-frame lanes and incomplete adoption of job admission wrappers.

## Second-Pass Quantification

Scope:
- Runtime source under `Assets/_Project/Scripts`.
- Excluded `Library`, `Temp`, `Obj`, and `Assets/_Project/Scripts/Editor`.
- Counts are text hits, not profiler samples. They identify audit pressure, not measured frame cost.

Native Unity message loops:
- Actual method declarations found outside Editor: `SystemDispatcher.Update()` and `SystemDispatcher.LateUpdate()` only.
- No gameplay `Update`, `FixedUpdate`, or `LateUpdate` declaration was found outside dispatcher ownership.

Dispatcher registration pressure:
- `GlobalRegistry.RegisterUpdatable`: 135 hits.
- `GlobalRegistry.TryRegisterUpdatable`: 105 hits.
- `GlobalRegistry.RegisterSlowTickable`: 95 hits.
- `GlobalRegistry.TryRegisterSlowTickable`: 32 hits.
- `GlobalRegistry.RegisterLateFrameTickable`: 43 hits.
- `GlobalRegistry.TryRegisterLateFrameTickable`: 41 hits.
- `GlobalRegistry.RegisterFixedTickable`: 24 hits.
- `GlobalRegistry.TryRegisterFixedTickable`: 8 hits.
- `GlobalRegistry.RegisterPostFixedTickable`: 9 hits.
- `GlobalRegistry.TryRegisterPostFixedTickable`: 4 hits.
- `GlobalRegistry.RegisterFrostTickable`: 2 hits.
- `GlobalRegistry.TryRegisterFrostTickable`: 1 hit.
- `GlobalRegistry.RegisterFastTickable`: 1 hit.
- `GlobalRegistry.TryRegisterFastTickable`: 1 hit.
- `GlobalRegistry.RegisterColdTickable`: 0 hits.
- `GlobalRegistry.TryRegisterColdTickable`: 0 hits.

Interface implementation pressure:
- `IUpdatable` implementer-pattern hits: 200.
- `ITickable` implementer-pattern hits: 181.
- `ISlowTickable` implementer-pattern hits: 139.
- `ILateFrameTickable` implementer-pattern hits: 81.
- `IFixedTickable` implementer-pattern hits: 31.
- `IPostFixedTickable` implementer-pattern hits: 12.
- `IFrostTickable` implementer-pattern hits: 3.
- `IUnscaledFastTickable` implementer-pattern hits: 2.
- `IFastTickable` implementer-pattern hits: 1.
- `IColdTickable` implementer-pattern hits: 0.

Job admission pressure:
- `.Schedule(` hits outside Editor: 246.
- `TryScheduleAdmitted` / `TryScheduleParallelAdmitted` hits outside Editor: 9.
- Ratio is not acceptable for a codebase claiming broad job admission. The scheduler exists; adoption is not mature.

AAA remediation queue for implementation owners:
- Collapse the legacy 2 Hz `GameTickManager` slow cadence into named dispatcher lanes, or explicitly rename it as a legacy bridge cadence. Current naming allows wrong assumptions.
- Make `IColdTickable` useful or delete the dead lane from public expectations. Current registration and implementer hits are zero outside Core.
- Convert heavy non-player-visible `IUpdatable` owners to `ISlowTickable`, `IFrostTickable`, or foveated targets where visual continuity can be faked.
- Move heavy naked `.Schedule(` call sites into `TryScheduleAdmitted` wrappers by domain lane, starting with Voxel, World, Fauna IK, Atmosphere, UI spectrogram/radar, and Fluid jobs.
- Promote foveated simulation beyond fauna if the target has distance/frustum relevance and tolerates accumulated delta. This is the existing true bucketing primitive.
- Keep direct Unity message loops prohibited. This part is currently clean; do not regress it.

## [EXISTING]

- `SystemDispatcher` has real Fast/Slow/Cold/Frost cadence lanes with dedicated accumulators.
- `GlobalRegistry` exposes fixed registration APIs for fast, fixed, slow, cold, frost, unscaled fast, late-frame, and post-fixed lanes.
- Dispatcher lanes use preallocated `RegistryBucket<T>` arrays and reverse index iteration.
- Late-frame event flushing has a hard dispatch budget and telemetry path.
- Job admission token buckets exist, use fixed native arrays, and feed CrashTelemetry/GlobalSignals.
- Foveated simulation is a real per-target cadence system with Burst scoring, hysteresis, deferred raycasts, 300-frame blackbox telemetry, and opt-in target registration.

## [SKELETON]

- `IColdTickable` and `IFrostTickable` exist but have much thinner adoption than `ITickable` and `ISlowTickable`.
- `IColdTickable` is effectively dead in current runtime source: zero non-Editor registration hits and zero implementer-pattern hits in the second pass.
- `IFastTickable` and `IUnscaledFastTickable` exist, but most gameplay still uses `IUpdatable`/`ITickable`.
- `FaunaBrain` declares more tick interfaces than it appears to register directly; actual non-60Hz throttling comes through foveated dispatch, not broad `ISlowTickable` registration.
- Job admission wrappers exist but are not the default scheduling path across all heavy systems.

## [GAPS]

- `GameTickManager` legacy 2 Hz SlowTick still coexists with dispatcher 10 Hz SlowTick. This creates semantic drift.
- No universal simulation bucketing service exists for arbitrary entity groups.
- No global `(entityID + frameCount) % N` bucket distribution was found.
- Many heavy domains still own per-frame dispatcher lanes, especially world presentation, biolum, vegetation/scatter, fauna IK, and voxel delta processing.
- Job admission is incomplete: second pass found 246 non-Editor `.Schedule(` hits and only 9 admission wrapper hits.
- Timing interfaces are managed. Burst jobs cannot consume tick contracts directly without separate data-oriented scheduling.

## [VERDICT]

HECTON-8 has more than simple timers, but it does not have a universal simulation bucketing architecture.

Precise verdict:
- Slow/Cold/Frost lanes are simple accumulator timers.
- FoveatedSimulationManager is true simulation bucketing/time-slicing for opt-in targets.
- Late-frame/native queue drains are budgeted work slicing.
- Job admission is a source-present token-bucket scheduler, partially adopted.
- Broad entity bucketing by modulo is not present as a general architecture.

Net: true bucketing exists in specific systems, not as a global policy.
