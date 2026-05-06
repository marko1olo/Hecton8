# Play Mode Deadlock Static Audit

Status: PENDING VERIFICATION

Date: 2026-05-07

Play Mode: NOT RUN. User explicitly forbade launching Play Mode for this pass.

MCP Console: unavailable during this pass. `read_console` and `validate_script` both failed because Unity did not answer the MCP ping.

## Mandates Followed

- `OPT_Native_Memory_Collections_JobSystem_Protocol`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety`
- `ARCH_Global_Registry_ServiceLocator_DI_Init`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory`
- `DATA_Save_Persistence_Binary_Delta_Checksum`

## Scan Scope

Static scan target:
- `Assets/_Project/Scripts/**/*.cs`

Observed script count under first-party scripts:
- 1002 C# files

Primary patterns searched:
- `JobHandle.Complete()`
- `WaitForCompletion()`
- `.Wait()`
- `.Result`
- `GetAwaiter().GetResult()`
- `Thread.Sleep`
- `ManualResetEvent`
- `AutoResetEvent`
- `SemaphoreSlim`
- `lock (...)`
- `while (true)`
- scene and Addressables async load loops

Raw barrier hit count:
- 174 matched lines before false-positive cleanup.

Important negative findings:
- No runtime `Addressables.WaitForCompletion()` hit found.
- No runtime `.Wait()` / `GetAwaiter().GetResult()` / `Thread.Sleep()` sync wait hit found.
- `.Result` hits were mostly Addressables handle result reads after status/isDone checks, not sync Task blocking.

## Fixed During This Pass

### `ProximityColliderSystem.Tick`

File:
- `Assets/_Project/Scripts/ProximityColliderSystem.cs`

Problem:
- `Tick()` unconditionally called `_jobHandle.Complete()` when `_jobScheduled` was true.
- That is a main-thread barrier in the hot path.
- If the distance job is late, Play Mode can look frozen even though the worker job is merely unfinished.
- The previous code also allocated `_prevStatusNative` as `Allocator.TempJob` every scheduled tick, then disposed it after completion. If completion was deferred beyond 4 frames, Unity's TempJob lifetime warning path could fire.

Change:
- `Tick()` now checks `_jobHandle.IsCompleted`.
- If the job is not complete, the system returns and does not schedule an overlapping job.
- `_prevStatusNative` is now a persistent buffer reused across schedules and disposed only during teardown/reinitialize.

Runtime impact model:
- CPU: removes a main-thread wait from `Tick`.
- GC: no managed allocation added.
- Native memory: keeps one persistent byte buffer of `pointCount` length instead of a TempJob allocation per job.
- Correctness: collider proxy updates can lag by extra frames if the job is slow; that is safer than freezing the main thread.
- Failure mode: if the job never completes, collider state stops advancing instead of locking the frame.

Verification:
- Static diff reviewed.
- `git diff --check` passed for the touched file except existing CRLF warning.
- MCP script validation could not run because Unity session was unavailable.

### Late-frame job barriers gated

Files:
- `Assets/_Project/Scripts/Construction/AutonomousExtractorSystem.cs`
- `Assets/_Project/Scripts/Tools/ToolDurabilitySystem.cs`
- `Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs`
- `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs`
- `Assets/_Project/Scripts/PhysicsApplySystem.cs`
- `Assets/_Project/Scripts/Fauna/ProceduralLeviathanSpineIK.cs`
- `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs`
- `Assets/_Project/Scripts/World/FloraRegrowthDirector.cs`

Problem:
- Several systems scheduled work earlier in cadence and then called `Complete()` from runtime cadence without checking `IsCompleted`.
- This is an end-of-frame swap lane, so it is less dangerous than mid-`Tick`, but still a main-thread stall point if the job misses the frame.
- Follow-up one-hop scan found the same pattern hidden behind private methods called from runtime cadence in weather, predator cognition, physics validation, leviathan spine IK, organic yield, and flora maturation.

Change:
- Added `IsCompleted` guards before completion.
- Existing scheduling already refuses to schedule another job while the previous one is active, so no overlap is introduced.
- Where teardown/origin-shift must own final state before disposal or transform shift, force completion remains explicit and outside normal frame cadence.

Runtime impact model:
- CPU: removes forced end-of-frame waiting.
- GC: no allocation added.
- Cadence: extractor/durability results can commit one or more frames later if a job is late.
- Correctness: old state remains authoritative until job completion; no partial result read.

Follow-up static result:
- Direct `Complete()` calls inside `Tick` / `FixedTick` / `SlowTick` / `LateFrameTick` / `PostFixedTick` / `Update` / `LateUpdate` / `FixedUpdate` were rescanned after the patch.
- Remaining 7 direct hot-path completions are now all `IsCompleted`-gated by local static inspection.
- After the extended pass, 8 direct hot-path completions are present and all are `IsCompleted`-gated.
- One-hop hot-path scan still reports `HectonFloatingOrigin.ShiftWorld` and `HectonFabricatorUI.CloseMenu` as intentionally unresolved risk sites; see below.

### `HectonWorldGenerator` pending chunk cancellation

File:
- `Assets/_Project/Scripts/HectonWorldGenerator.cs`

Problem:
- `RefreshChunks()` force-completed pending chunk generation when the desired chunk set changed.
- This violates the streaming mandate: runtime streaming must not block the main thread waiting for background chunk work.

Change:
- Added `PendingChunk.cancelRequested`.
- Undesired pending chunks are now marked canceled, not completed.
- `ProcessPendingChunks()` disposes canceled chunk buffers only after `combinedHandle.IsCompleted`.
- Normal `StopStreaming()` teardown still force-completes because shutdown owns all remaining native buffers.

Runtime impact model:
- CPU: removes a chunk-cancellation main-thread barrier.
- GC: no managed collection added.
- Native memory: canceled pending chunks remain resident until their generation jobs finish.
- Correctness: canceled chunks no longer finalize into active terrain; buffers are disposed after safe job completion.

## High-Risk Remaining Candidates

### 1. `HectonWorldGenerator` active chunk physics-bake cancellation

Files / lines observed:
- `Assets/_Project/Scripts/HectonWorldGenerator.cs:1386`
- `Assets/_Project/Scripts/HectonWorldGenerator.cs:1419`
- `Assets/_Project/Scripts/HectonWorldGenerator.cs:1441`

Why it matters:
- `Tick()` calls `RefreshChunks()`, `ProcessQueue()`, `ProcessPendingChunks()`, and `BakePhysicsBatch()`.
- Pending chunk generation cancellation is now deferred.
- Active chunk destruction can still call `CancelPendingPhysicsBake()`, which can force-complete an in-flight PhysX bake before the source mesh is destroyed.

Deadlock/stall classification:
- STALL RISK, not proven deadlock.
- It can freeze the main thread if a chunk is evicted while its PhysX bake is still running.

Recommended next fix:
- Convert active chunk destruction into deferred retirement when a physics bake is in flight.
- Hide renderer and remove active dictionary entry immediately.
- Destroy mesh/game object only after bake handle completes.
- Do not free source mesh while PhysX bake can still reference it.

### 2. `SceneRuntimeService.LoadSceneAsync` activation wait has no watchdog

File / lines observed:
- `Assets/_Project/Scripts/Core/SceneRuntimeService.cs:111+`

Code behavior:
- Loads scene async with `allowSceneActivation = false`.
- Wait loop yields every frame.
- Activation waits on:
- load progress >= 0.9
- `ArePersistentWorldPoolsReadyForSceneActivation()`
- `IsFloatingOriginStableForSceneActivation()`

Deadlock/stall classification:
- INFINITE ASYNC WAIT RISK.
- Not CPU spin because it awaits next frame.
- It can still create a "Play Mode hangs on loading" failure if either readiness predicate never flips.

Recommended next fix:
- Add frame/time watchdog with explicit failure reason.
- Write one throttled diagnostic when blocked.
- Do not auto-activate if prerequisites are invalid unless architect approves fallback policy.

### 3. `HectonVoxelEngine.AcquireStreamingScratchLeaseAsync` has no watchdog

File / lines observed:
- `Assets/_Project/Scripts/HectonVoxelEngine.cs:3699+`

Code behavior:
- `while (true)` repeatedly tries to acquire a scratch lease.
- It yields with `Awaitable.NextFrameAsync`.
- It respects cancellation.

Deadlock/stall classification:
- INFINITE ASYNC WAIT RISK.
- If all scratch slots stay `InUse` because a pipeline was canceled/leaked before release, the caller waits forever.

Recommended next fix:
- Add wait-frame watchdog and diagnostic owner state.
- On timeout, abort voxel pipeline with clean lease-state report rather than spin-yielding forever.

### 4. `GameBootstrapper.WaitForJobCompletionAsync` and voxel/wreck async job waits lack timeout

Files / lines observed:
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:1076`
- `Assets/_Project/Scripts/HectonVoxelEngine.cs:3304`
- `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:1093`

Code behavior:
- These loops poll `handle.IsCompleted`.
- They yield every frame and then call `Complete()`.

Deadlock/stall classification:
- LOW CPU, INFINITE WAIT RISK.
- This is better than tight blocking, but still no timeout or fail-fast path.

Recommended next fix:
- Add watchdogs per owner with failure mode:
- bootstrap: fail the bootstrap phase with overlay/log.
- voxel: cancel pipeline and release lease.
- wreck generator: abort generation and leave no half-owned mesh/native buffers.

### 5. `PlayerCriticalProceduralAudioRenderer` custom thread lifecycle

Files / lines observed:
- `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:387`
- `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:1088`

Code behavior:
- Background producer thread waits on `ManualResetEventSlim`.
- Shutdown signals and joins with timeout.

Deadlock/stall classification:
- MAIN-THREAD DEADLOCK NOT PROVEN.
- Join has a timeout, so it should not hard-lock the main thread indefinitely.
- Risk remains: orphan producer thread or repeated warnings if teardown cannot stop cleanly.

Recommended next fix:
- Audit start/stop ownership and all paths that set `_audioProducerRunning`.
- Ensure native buffers cannot be disposed while producer still reads them.

### 6. Editor Play Mode retry gate

File:
- `Assets/_Project/Scripts/Editor/ShellVerificationPlayModeCompileGate.cs`

Code behavior:
- Can stop and restart Play Mode from editor callbacks.
- It is not `[InitializeOnLoad]`; it registers only through menu.

Deadlock/stall classification:
- CONDITIONAL EDITOR CHURN RISK.
- If enabled during compile/import churn, it can make Play Mode entry look unstable.
- It is not currently proven active.

Recommended next fix:
- Add visible enabled-state marker or `SessionState` flag report before using it.
- Do not use it during manual deadlock diagnosis.

### 7. Synchronous `.Run()` jobs on runtime paths

Files observed:
- `Assets/_Project/Scripts/CraftingSystem.cs`
- `Assets/_Project/Scripts/PlayerInventory.cs`
- `Assets/_Project/Scripts/TetherInstance.cs`
- `Assets/_Project/Scripts/World/FloraInteractionManager.cs`

Deadlock/stall classification:
- MAIN-THREAD STALL RISK, not deadlock.
- `.Run()` does not wait for a worker thread; it executes the job synchronously on the calling thread.
- This can still hurt frame time, especially in visual or SlowTick paths, but it is not the same failure mode as an unfinished scheduled job.

Recommended next fix:
- Profile before conversion.
- Convert only bounded heavy paths to scheduled jobs with `IsCompleted` consumption.
- Do not convert tiny availability checks just to satisfy style.

## Things That Looked Suspicious But Are Not Primary Deadlock Candidates

### `HectonPlayerSpawner.SpawnPlayerAsync`

It contains `while (!terrainReady)`, but has:
- cancellation checks
- global timeout
- fallback spawn
- awaited retry delay

Risk:
- log spam and slow spawn failure, not a tight deadlock.

### Infinite loops in heap/CAS code

Observed:
- `DroneFleetManager.TryPopTask`
- `ModularEquipmentEngine.ConsumeBatteryAbsolute`
- `PlayerCriticalProceduralAudioRenderer.TryEnqueueImpactAudioEvent`
- disabled `GameTickManager` coroutine under `#if false`

Assessment:
- heap loops have deterministic break conditions.
- CAS loop has success/retry semantics.
- audio enqueue loop has watchdog.
- disabled coroutine is not compiled.

## Editor Log Reality

Current Editor.log evidence did not show a clean Play Mode deadlock root.

Observed editor-side stall signals:
- domain reloads taking 13s to 29s
- one asset pipeline refresh taking 366.557s
- latest project load tail showed 539.334s total load, including 511.881s asset database refresh
- later domain reloads still showed 35s to 46s reloads and 36s to 49s forced refreshes
- repeated compile/import churn
- MCP session not responding to console/script validation requests
- latest log scan still contains unrelated compile errors in files outside this job-barrier patch set; Unity verification remains blocked until those are resolved or proven stale by a clean compile.

Interpretation:
- The reported "12% CPU deadlock" can be editor import/compile/domain reload churn, not necessarily Play Mode runtime code.
- Runtime deadlock is still possible, but not proven by this static pass.

## Regression Model

CPU:
- Fixed one hot-path job barrier in `ProximityColliderSystem`.
- Remaining risk is mostly world streaming/chunk cancellation and scene readiness waits.

GC:
- No managed allocation added by the fix.
- Static scan did not prove GC-induced deadlock.

Native memory:
- `ProximityColliderSystem` now retains one persistent byte buffer.
- Memory increase equals `pointCount` bytes.
- Existing persistent state already includes `NativeArray<float3>` and `NativeArray<byte>` result buffers, so this is small relative to current owner memory.

Cadence:
- Proximity collider updates can defer while the job is incomplete.
- This is an intentional fail-soft behavior.

Correctness:
- No gameplay API changed.
- No public contract changed.
- No scene/prefab/settings changed by this fix.

## Next Actions

Priority order:

1. Fix `HectonWorldGenerator` active chunk physics-bake retirement without force-completing in `Tick`.
2. Add watchdog diagnostics to `SceneRuntimeService.LoadSceneAsync`.
3. Add scratch-lease wait watchdog to `HectonVoxelEngine`.
4. Continue classifying all `.Complete()` hits into:
- completed-gated hot path
- forced hot path
- teardown-only
- editor-only
- false positive
5. Profile synchronous `.Run()` jobs before converting them.
6. Reattempt MCP console and script validation only after Unity session responds.
