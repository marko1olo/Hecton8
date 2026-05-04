# Dispatch Pipeline

Date: `2026-05-03`
Status: `PENDING VERIFICATION`

## Scope
This document is the authoritative handoff for future agents touching `SystemDispatcher`, `PhysicsApplySystem`, late-frame job ownership recovery, and structural command draining.

Current-state boundary:

- This document defines the required dispatch contract.
- It is not proof that all current sources comply.
- `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md` supersedes the older literal `.Complete()` call-site list. Current strict grep finds dispatcher request completion callbacks in `ItemCatalog.cs` / `AssetLifecycleGovernor.cs` and one explicit `JobHandle.Complete()` in `World/DispatcherJobSwap.cs`.
- Any future edit must keep job barriers inside explicit dispatcher-owned swap windows or document why the owner is a permitted end-window.

## Core Rule
`Tick()` and `FixedTick()` may schedule jobs and read already-published front buffers.
They must not call `JobHandle.Complete()` in the middle of gameplay lanes.
Barrier recovery happens only inside explicit swap windows:

- `SystemDispatcher.LateUpdate()` for frame jobs and other end-of-frame readers.
- `SystemDispatcher` post-fixed lane for systems that need a fixed-step swap window.

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

`.Complete(` text hits are not all `JobHandle.Complete()`. Current source inventory still separates `dispatcher.Complete(...)` request callbacks from the explicit `handle.Complete()` inside `DispatcherJobSwap.TryComplete(...)`.

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

These are the reference before/after cases for future audits. If a system schedules work in a gameplay lane and consumes it in that same lane on a later frame, it still violates the contract.

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
