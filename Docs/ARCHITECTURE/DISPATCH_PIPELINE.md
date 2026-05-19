# Dispatch Pipeline

Date: 2026-05-07
Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.

R32 architecture R4/proof-wording correction is the latest artifact-backed local static DOC_GLOBAL boundary for architecture/root documentation. R31 remains the prior current-boundary propagation layer, R30 remains the prior internal-currentness layer, R29 remains the prior stale-gate/global-authority layer, R28 remains the prior interior-boundary layer, and R27 remains the latest source-counter/index snapshot until rerun.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Scope
This document is the authoritative handoff for future agents touching `SystemDispatcher`, `PhysicsApplySystem`, late-frame job ownership recovery, and structural command draining.

Current-state boundary:

- This document defines the required dispatch contract.
- It is not proof that all current sources comply.
- `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md` supersedes the older literal `.Complete()` call-site list. Last documented strict grep reported dispatcher request completion callbacks in `ItemCatalog.cs` / `AssetLifecycleGovernor.cs` and explicit `JobHandle.Complete()` calls inside `World/DispatcherJobSwap.cs`; rerun and link command output, timestamp, and environment before calling that inventory current.
- Any future edit must keep job barriers inside explicit dispatcher-owned swap windows or document why the owner is a permitted end-window.

## 2026-05-18 SHINOBU_40 Master Dispatcher Addendum

Evidence class: STATIC_SOURCE / CLI_COMPILE_BLOCKED_BY_EXTERNAL_DEPENDENCY.

`SystemDispatcher` now exposes a master-dispatcher contract for cross-domain integration without direct domain references:

- `IDispatcherSystem` registers through `GlobalRegistry.TryRegisterDispatcherSystem`.
- `IDispatcherFixedSystem` registers through `GlobalRegistry.TryRegisterDispatcherFixedSystem`.
- Boot topology uses Kahn sorting over stable system hashes and fails fast with `FatalArchitectureException` on cycles.
- Dispatcher timing is the 16-byte `DispatcherTimingDTO`: `FrameDelta`, `FixedDelta`, `TimeScale`, `ActiveBucketMask`.
- SIMULATION job handles are stored in DataVault-backed dispatcher buffers, combined once, and completed once at POST_SIMULATION start.
- 64-bucket time slicing uses `Time.frameCount & 63`; `byte.MaxValue` means always active.
- A 300-frame dispatcher pipeline ring records PreSim, SimWait, PostSim, and VisualSync timings and dumps `Docs/AgentLogs/Dump_SYSTEM_DISPATCHER.bin` when SimWait exceeds 8 ms.
- `Execution Pipeline X-Ray` is an Editor-only facade for phase bars and the 64-cell bucket grid.

WakeRequestSignal source-symbol boundary: R27 static source recheck preserves the R26 finding that `WakeRequestSignal`, `GlobalPhysicsStateManager.WakeRequests.cs`, and the `SignalBus<WakeRequestSignal>` lane in `GlobalSignals.cs` exist, so the older missing-symbol blocker is historical. Global DOC_GLOBAL root/architecture boundary is R32; R31 is the prior current-boundary propagation correction, R30 is the prior internal-currentness correction, R29 is the prior stale-gate/global-authority correction, R28 is the prior interior-boundary correction, and R27 is retained as source-counter/source-symbol orientation. Current static gates: `Tools\AtlasCheck.py` remains red on `59` missing refs (RealtimeCSG vendor refs plus absent `VaultXRayWindow.cs` and `HectonMapMagicVegetationBridgeFloraCollisionProxies.cs`); `Docs\Modding\Validate_Mod_API_Static.ps1` now passes (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`) as static-tool orientation only; do not treat PASS as current proof without artifact path, command, timestamp, environment, and output. No current compile, Play Mode, profiler, GC, or runtime proof is claimed until a fresh artifact links command, timestamp, environment, and output.

## Core Rule
`Tick()` and `FixedTick()` may schedule jobs and read already-published front buffers.
They must not call `JobHandle.Complete()` in the middle of gameplay lanes.
Barrier recovery happens only inside explicit swap windows:

- `SystemDispatcher.LateUpdate()` for frame jobs and other end-of-frame readers.
- `SystemDispatcher` post-fixed lane for systems that need a fixed-step swap window.

## 2026-05-19 Global Authority Dispatch Boundary

Dispatcher phases are not a license to query global state live.

Rules:

- Systems registered with `SystemDispatcher` must cache `GlobalRegistry`
  dependencies before hot dispatch.
- If a dispatcher system needs live configuration, it consumes a cached
  DataVault snapshot or typed changed signal, not a per-frame registry poll.
- First-party cross-domain broadcasts raised by dispatcher phases use typed
  `SignalBus<T>` lanes or documented NativeQueue bridge lanes.
- `HectonEventBus` traffic inside dispatcher phases is allowed only for mod/API
  results after `ModCommandDispatcher` isolation. It is not a gameplay bus.
- Dispatcher-owned completion windows are the only accepted place for job
  ownership recovery unless a cold teardown path is explicitly annotated.

Cross-reference:

- `GLOBAL_AUTHORITY_BOUNDARIES.md`
- `GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`
- `SYSTEM_INTERCONNECT_MATRIX.md`

## Frame Order
The current runtime order is:

1. `SystemDispatcher.Update()`
2. Main update lanes run by priority.
3. Update owners schedule next-frame jobs and deferred raycasts.
4. `SystemDispatcher.FixedUpdate()`
5. Fixed lanes run by priority.
6. Post-fixed lanes run by priority for fixed-step swap work.
7. `SystemDispatcher.LateUpdate()`
8. Dispatcher-owned raycast handle completes.
9. Foveated simulation manager completes its scheduled jobs.
10. All `ILateFrameTickable` owners recover their published job results.
11. `ThreadSafeCommandQueue` drains structural commands on the main thread.
12. Deferred event buses flush.
13. `WorldSpatialHashGrid` late-frame maintenance runs.
14. `NativeArenaAllocator.Reset()` invalidates the transient scratch arena for the next frame.

## Double Buffering
The pattern is front-buffer read, back-buffer write.

- Front buffer: the only buffer gameplay code may consume this frame.
- Back buffer: private write target for jobs scheduled this frame.
- Swap window: the only point where a writer may recover ownership, complete its job handle, and publish the back buffer as the next front buffer.

If a system writes into the same data it reads before the swap window, it is broken.

## PhysicsApplySystem
`PhysicsApplySystem` is the reference implementation for the late-frame swap window.

### Fixed Phase
- `FixedTick()` gathers force packets into `_backPackets`.
- `FixedTick()` may flush only the previously validated `_frontPackets`.
- If validation for the current front buffer is still scheduled, `FixedTick()` returns without swapping.
- If no validation is pending, `FixedTick()` swaps buffers and schedules packet validation for the new front buffer.

### Late-Frame Phase
- `LateFrameTick()` calls `CompleteFrontPacketValidationInLateFrameSwapWindow()`.
- That method completes `_packetValidationHandle`.
- Only after completion does `_frontBufferValidationReady` become true.
- The next `FixedTick()` is then allowed to flush the validated front buffer into live rigidbodies.

This is deliberate one-step latency. The main thread never blocks mid-step for packet validation.

## Dispatcher-Owned Completion
`SystemDispatcher` currently owns these explicit late-frame barriers:

- Dispatcher deferred raycasts via `_scheduledDispatcherRaycastHandle.Complete()`.
- Foveated simulation completion through `FoveatedSimulationManager.CompleteFrameJobs()`.
- `DispatcherJobSwap.BeginLateFrameSwapWindow()` / `EndLateFrameSwapWindow()` wrap the `ILateFrameTickable` recovery lane and static late-frame recovery helpers.
- `DispatcherJobSwap.BeginPostFixedSwapWindow()` / `EndPostFixedSwapWindow()` wrap the `IPostFixedTickable` recovery lane.

Both are profiled. If either barrier takes more than `1.0ms`, the dispatcher emits a warning naming the subsystem that stalled and publishes the stall to `GlobalTelemetryBus`.

In editor/development builds, non-forced `DispatcherJobSwap.TryComplete(...)` calls outside those dispatcher-owned swap windows emit a throttled warning. This is diagnostic only; release builds do not log.

`HectonWorldGenerator.StopStreaming()` is a teardown-only exception: pending chunk generation jobs are completed before LUT/native buffer disposal, and pending PhysX bake jobs are completed before chunk collider destruction. These call sites must stay annotated as `[BLOCKING_SYNC_POINT]` and must not be used as a normal residency-retirement path.

## Foundation Guard Inventory
May 3 source guard:

- Tool: `Tools/ReloadAudit/Scan-FoundationGuards.ps1`
- Output: `Docs/Reports/2026-05-03_FOUNDATION_GUARD_SCAN.md`
- Global registry self-registration inventory: `493`
- Blind registry flag drift: `0`
- Origin shift listener blind flag drift: `0`
- Synchronous `.Run(` sites: `0`
- Hot-path synchronous `.Run(` review sites: `0`
- Completion `.Complete(` text hits: `1`
- Direct raw-array listener dispatch: `0`
- `GlobalRegistry.Input` nullable misuse: `0`
- Direct `InputManager.Instance` sites: `28`
- Hot-path direct `InputManager.Instance` review sites: `0`
- Optimization singleton residue: `0`
- Unauthorized Unity loop methods: `0`
- Legacy coroutine sites: `0`
- Forbidden runtime asset API sites: `0`
- Broad physics layer masks outside Editor: `0`
- Runtime Find API text hits outside Editor folder: `0`

`.Run(` sites are not automatic violations. Treat them as migration candidates only after the owner has a front/back buffer, a late-frame or post-fixed publication window, and profiler evidence that synchronous execution is a real frame-time problem.

`.Complete(` text hits are not all `JobHandle.Complete()`. The R27 source-counter inventory still separates `dispatcher.Complete(...)` request callbacks from the explicit `handle.Complete()` inside `DispatcherJobSwap.TryComplete(...)`; rerun before using the count as current source truth.

## ThreadSafeCommandQueue
`ThreadSafeCommandQueue` exists for structural intent only.

- Jobs enqueue `EntityCommand` through `NativeQueue<EntityCommand>.ParallelWriter`.
- `EntityCommand` is blittable. No managed references are allowed.
- Jobs never despawn or destroy `GameObject` instances directly.
- Main thread resolves queue target tokens and applies the structural mutation in `SystemDispatcher.LateUpdate()`.

Current supported commands:

- `SpawnGameObject`
- `DespawnGameObject`
- `DestroyGameObject`
- `SetGameObjectActive`
- `ModifyVoxel`

If a future system needs another structural mutation, add a new opcode and keep the payload blittable.

## NativeQueue Event Generations
`ModRegistryEvents`, `BootstrapEvents`, `LocalizationEvents`, `InteractionEvents`, `CraftingEvents`, `ScanEvents`, `SaveEvents`, `InventoryEvents`, `WeatherEvents`, `QuestEvents`, `PowerGridTelemetryEvents`, `NarrativeEvents`, `NotificationEvents`, `FirstHourEvents`, `EndingEvents`, `AudioLogEvents`, `AtmosphereEvents`, `EclipseGameplayEvents`, `AcousticZoneEvents`, `PhysicsEventBus`, `CelestialEvents`, `FluidFeedbackEvents`, `RepairDroneTorchAcousticEvents`, `ElectrolysisAcousticEvents`, `AudioCaptionEvents`, `SpectrumEvents`, `ProceduralAudioEvents`, `HectonSubmarineOsEvents`, `LaserCutterEvents`, `MapMagicBiomeEvents`, `BiomeMatrixEvents`, `DirectorAIEvents`, `HectonDroneFleetEvents`, `FlashlightEvents`, `PlayerSignalEvents`, `HighPressureEvents`, `FatalPressureImplosionEvents`, `ModuleStatusEvents`, `DepthZoneEvents`, `SoundscapeEvents`, `EmergencyServiceRelayEvents`, `SargassumGlobalDragManager`, `AtlasSignalEvents`, `PlayerExpressionEvents`, `BaseIntegrityEvents`, `PDAIntrusionEvents`, `PDAEvents`, `SceneBootstrap`, `ObjectPoolDiagnostics`, `PerformanceEvents`, `RandomEventEvents`, `Atlas6Events`, and `GlobalRegistry` service rebound events are the current source-level references for generation-split queue flushing.

- Front queue: current generation drained by `SystemDispatcher.LateUpdate()`.
- Back queue: payloads raised by listeners during dispatch.
- Promotion: back queue becomes front only after the current front queue is empty.
- Budget trip: current front generation keeps priority; reentrant back-generation events wait.

This prevents same-frame listener reenqueue from extending the listed lanes until the global late-frame budget trips. Other NativeQueue-backed lanes still need one-by-one migration and Play Mode proof before claiming generation-split event architecture globally.

## Shame List
These systems were explicitly moved off mid-tick `Complete()` patterns and into dispatcher-controlled swap windows:

- `VoxelDeltaProcessor`: carve job commit moved from `Tick()` to `LateFrameTick()`.
- `DebrisManager`: debris simulation swap moved from `Tick()` to `LateFrameTick()`.
- `HectonFluidEngine`: buoyancy force readback moved from `FixedTick()` to `PostFixedTick()`.
- `LODSystemManager`: distance-job completion moved from `Tick()` to `LateFrameTick()`.


2026-05-01 caveat:

- `PostFixedTick()` is a valid swap-window concept only when owned and bounded by dispatcher cadence.
- A local owner calling `.Complete()` in `PostFixedTick()` is still a review target until the wait is proven non-stalling or moved behind a dispatcher-owned completion policy.

## Scene Activation Gate
`SceneRuntimeService.LoadSceneAsync()` now performs guarded async scene loads.

- Scene load starts with `allowSceneActivation = false`.
- The service monitors `AsyncOperation.progress`.
- Activation is allowed only when progress reaches `0.9`, `PersistentWorldRegistry.AreResidentWorldPrefabPoolsReady()` returns true, and the floating-origin service is not inside a shift or post-shift physics pause window.
- If no live `PersistentWorldRegistry` exists for the transition, the gate falls through to avoid deadlock.

This gate is intended to prevent a scene from activating before resident pooled world prefabs are both addressable-ready and pool-ready, and before a committed origin shift has reached a stable synchronization point.

## Multi-Scene Rebase Sync
`HectonFloatingOrigin` owns additive-scene synchronization as well as normal global shifts.

- `sceneLoaded` marks the shift-target cache dirty and queues the loaded scene for synchronization.
- Once the floating-origin service is out of the shift/physics-pause window, it subtracts the current committed `TotalOffset` from every root in the newly loaded scene.
- After that rebase, it rebuilds the cached `TransformAccessArray` so the next atomic world shift includes the new scene roots.

This prevents newly activated additive content from entering the world at stale pre-shift coordinates.

## Core Awaitable Audit
`Assets/_Project/Scripts/Core/` currently has no `IEnumerator`, `StartCoroutine`, or `yield return` usage.
Core dispatch timing is already on `Awaitable`/dispatcher state-machine ownership. No architecture docs in `Docs/ARCHITECTURE/` currently describe an active coroutine-based Core loop.

## Failure Modes
- Calling `Complete()` from `Tick()` or `FixedTick()` reintroduces frame stalls and breaks the contract.
- Reusing a front buffer before its validation/readback gate completes causes torn state.
- Enqueuing managed references into `ThreadSafeCommandQueue` violates Burst compatibility.
- Activating a scene before pool prewarm confirmation creates spawn failures and hydration churn on first frame.

## Agent Rules
- Do not add new coroutines for core dispatch timing.
- Do not add new mid-frame `Complete()` calls.
- Do not bypass `ThreadSafeCommandQueue` for job-authored structural changes.
- If a system needs main-thread mutation after a job, give it a late-frame or post-fixed swap window and document the owner.
