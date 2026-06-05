# Dispatch Pipeline

Date: 2026-06-02

Status: PENDING VERIFICATION

Owner domain: core dispatch / execution pipeline

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not dispatcher runtime health, profiler, GC, or player-build proof.

- `Assets/_Project/Scripts/Core/SystemDispatcher.cs`

- `Assets/_Project/Scripts/Core/SystemDispatcherContracts.cs`

- `Assets/_Project/Scripts/Core/GlobalRegistry.cs`

- `Assets/_Project/Scripts/Core/GlobalSignals.cs`

- `Assets/_Project/Scripts/World/DispatcherJobSwap.cs`

## Scope

Scope: `SystemDispatcher`, `PhysicsApplySystem`, late-frame job ownership recovery, and structural command draining.

## 2026-06-02 Source Reality

`SystemDispatcher` is a hybrid implementation in current source. It is not only a clean four-phase abstraction.

- Master phases exist: pre-simulation, simulation, post-simulation, visual sync, and fixed-simulation bridge.
- Legacy/priority tick lanes still exist through `GlobalRegistry` registration helpers: updatable, fast, fixed, slow, cold, frost, unscaled fast, late-frame, and post-fixed.
- Cadence constants exist in source: fast `1/60s`, slow `0.1s`, thermal-critical slow `0.2s`, cold `1.0s`, frost `5.0s`, plus emergency/homeostasis slow behavior.
- Dispatcher black boxes exist as 300-frame DataVault-backed rings for dispatcher/master dispatcher state.
- `RenderDispatcher`, `GlobalRenderContext`, `GraphicsBufferUploadUtility`, and `TimeSliceScheduler` live in the same source file, so dispatcher docs must consider render upload/presentation utilities too.

This is `STATIC_SOURCE` only. It does not prove compliance for every registered system.

Current-state boundary:

- Required dispatch contract only.

- It is not proof that all current sources comply.

- Older dated report call-site lists are historical and may be archived or absent.
- Use a fresh `.Complete()` grep plus `HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md` before claiming current dispatcher debt.
- Last documented strict grep: dispatcher callbacks in `ItemCatalog.cs` / `AssetLifecycleGovernor.cs`; explicit `JobHandle.Complete()` in `World/DispatcherJobSwap.cs`.
- Rerun and link command output, timestamp, and environment before calling that inventory current.

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

- A 300-frame dispatcher pipeline ring records PreSim, SimWait, PostSim, and VisualSync timings.
- Planned dump target: `Docs/AgentLogs/Dump_SYSTEM_DISPATCHER.bin`; absent until a dispatcher fault export writes it.
- Trigger: SimWait exceeds `8 ms`.
- No existing artifact is implied without timestamped trigger and output file.

- `Execution Pipeline X-Ray` is an Editor-only facade for phase bars and the 64-cell bucket grid.

## Core Rule

`Tick()` and `FixedTick()` may schedule jobs and read already-published front buffers.

They must not call `JobHandle.Complete()` in the middle of gameplay lanes.

Barrier recovery happens only inside explicit swap windows:

- `SystemDispatcher.LateUpdate()` is the current source method anchor for frame-job and end-of-frame readers; route-card phase authority remains `POST_SIMULATION` / `VISUAL_SYNC`.

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

Any dispatcher route using `GlobalRegistry`, `SignalBus<T>`, `GlobalSignals`, or `GlobalDataVault` remains `YELLOW`.

Required tuple: owner, producer/consumer phase, capacity/overflow behavior, failure/telemetry behavior, proof artifact. Static source visibility does not prove runtime dispatch health.

Cross-reference:

- `GLOBAL_AUTHORITY_BOUNDARIES.md`

- `GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`

- `SYSTEM_INTERCONNECT_MATRIX.md`

## Frame Order

The current source method order is an implementation anchor only; route-card phase authority is `PRE_SIMULATION`, `SIMULATION`, `POST_SIMULATION`, and `VISUAL_SYNC`:

1. `SystemDispatcher.Update()`

2. Main update lanes run by priority.

3. Update owners schedule next-frame jobs and deferred raycasts.

4. `SystemDispatcher.FixedUpdate()`

5. Fixed lanes run by priority.

6. Post-fixed lanes run by priority for fixed-step swap work.

7. `SystemDispatcher.LateUpdate()` as the source method anchor for `POST_SIMULATION` / `VISUAL_SYNC` recovery

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

`HectonWorldGenerator.StopStreaming()` is a teardown-only exception.

Pending chunk generation jobs complete before LUT/native buffer disposal. Pending PhysX bake jobs complete before chunk collider destruction.

These call sites stay annotated as `[BLOCKING_SYNC_POINT]`. They are not normal residency-retirement paths.

## Foundation Guard Inventory

May 3 source guard:

- Tool: `Tools/ReloadAudit/Scan-FoundationGuards.ps1`

- Missing historical output path: `Docs/Reports/2026-05-03_FOUNDATION_GUARD_SCAN.md`; not current proof until the scan is rerun or the artifact is restored.

- Legacy reported global registry self-registration inventory: `493`

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

`.Run(` sites are not automatic violations.

Treat them as migration candidates only after owner front/back buffer, publication window, and profiler evidence prove synchronous execution is a frame-time problem.

`.Complete(` text hits are not all `JobHandle.Complete()`.

- R27 source-counter inventory is historical.
- Rerun source-counter/source-grep before using the count as current truth.
- Capture separated `dispatcher.Complete(...)` callbacks from explicit `handle.Complete()` inside `DispatcherJobSwap.TryComplete(...)`.

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

- `ModRegistryEvents`,
- `BootstrapEvents`,
- `LocalizationEvents`,
- `InteractionEvents`,
- `CraftingEvents`,
- `ScanEvents`,
- `SaveEvents`,
- `InventoryEvents`,
- `WeatherEvents`,
- `QuestEvents`,
- `PowerGridTelemetryEvents`,
- `NarrativeEvents`,
- `NotificationEvents`,
- `FirstHourEvents`,
- `EndingEvents`,
- `AudioLogEvents`,
- `AtmosphereEvents`,
- `EclipseGameplayEvents`,
- `AcousticZoneEvents`,
- `PhysicsEventBus`,
- `CelestialEvents`,
- `FluidFeedbackEvents`,
- `RepairDroneTorchAcousticEvents`,
- `ElectrolysisAcousticEvents`,
- `AudioCaptionEvents`,
- `SpectrumEvents`,
- `ProceduralAudioEvents`,
- `HectonSubmarineOsEvents`,
- `LaserCutterEvents`,
- `MapMagicBiomeEvents`,
- `BiomeMatrixEvents`,
- `DirectorAIEvents`,
- `HectonDroneFleetEvents`,
- `FlashlightEvents`,
- `PlayerSignalEvents`,
- `HighPressureEvents`,
- `FatalPressureImplosionEvents`,
- `ModuleStatusEvents`,
- `DepthZoneEvents`,
- `SoundscapeEvents`,
- `EmergencyServiceRelayEvents`,
- `SargassumGlobalDragManager`,
- `AtlasSignalEvents`,
- `PlayerExpressionEvents`,
- `BaseIntegrityEvents`,
- `PDAIntrusionEvents`,
- `PDAEvents`,
- `SceneBootstrap`,
- `ObjectPoolDiagnostics`,
- `PerformanceEvents`,
- `RandomEventEvents`,
- `Atlas6Events`,
- and `GlobalRegistry` service rebound events are the current source-level references for generation-split queue flushing.

- Front queue: current generation drained by `SystemDispatcher.LateUpdate()`.

- Back queue: payloads raised by listeners during dispatch.

- Promotion: back queue becomes front only after the current front queue is empty.

- Budget trip: current front generation keeps priority; reentrant back-generation events wait.

- This prevents same-frame listener reenqueue from extending listed lanes until global late-frame budget trips.
- Other NativeQueue-backed lanes still need one-by-one migration.
- Play Mode proof is required before global generation-split event architecture claim.

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

This gate blocks scene activation until resident pooled world prefabs are addressable-ready and pool-ready, and committed origin shift reaches stable sync.

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
