# 2026-04-29 - CODEX Mandate Compliance Audit Phase 6
Date: 2026-04-29

Status: PENDING VERIFICATION
Author: Codex
Scope: static audit only

## Mandates Followed

- `AGENTS.md`
- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

## Method

- Audit focus: `JobHandle.Complete()` placement and native container disposal discipline.
- Started from repository-wide `Complete()` and `Dispose()` scans, then validated representative runtime systems in source.
- Separated likely valid end-of-frame/teardown sync points from gameplay/runtime tick-time sync points.
- No profiler or Unity runtime validation was performed.

## What Is Actually Aligned

### 1. Some systems do use deferred disposal correctly

Direct evidence:

- `Assets/_Project/Scripts/Gameplay/DebrisManager.cs`
  - `_frontStates.Dispose(_simulationHandle);`
  - `_backStates.Dispose(_simulationHandle);`
- `Assets/_Project/Scripts/SaveManager.cs`
  - `_integrityPayloadMirror.Dispose(_integrityScanHandle);`
  - `_integrityScanResult.Dispose(_integrityScanHandle);`
- `Assets/_Project/Scripts/Interaction/EquipmentInteractionHandler.cs`
  - `_scheduledCommands.Dispose(_scheduledRaycastHandle);`
  - `_scheduledHits.Dispose(_scheduledRaycastHandle);`
- `Assets/_Project/Scripts/Gameplay/VehicleMotor.cs`
  - `_scheduledSweepCommands.Dispose(_scheduledSweepHandle);`
  - `_scheduledSweepResults.Dispose(_scheduledSweepHandle);`

Assessment:

- The project does contain engineers who understand `Dispose(handle)` and use it.
- This is not a zero-knowledge codebase.

### 2. `SystemDispatcher` has a plausible end-of-frame completion window

Direct evidence:

- `Assets/_Project/Scripts/Core/SystemDispatcher.cs`
  - `Update()` schedules work
  - `LateUpdate()` calls `CompleteDispatcherRaycasts();`
  - `CompleteDispatcherRaycasts()` performs `_scheduledDispatcherRaycastHandle.Complete();`

Assessment:

- This is the closest thing in the current runtime to the mandated designated completion window.
- It does not prove full compliance project-wide.
- It proves the project already knows the correct shape.

### 3. Some cold sync points are explicitly documented as cold

Direct evidence:

- `Assets/_Project/Scripts/WorldProceduralScatterDirectorSamplingPipeline.cs`
  - bootstrap/editor prime paths use:
    - `// COLD SYNC JOB: editor preview and bootstrap prime require immediate scatter output before continuing.`

Assessment:

- Not every `Complete()` is automatically wrong.
- The problem is runtime gameplay usage, not every boot/editor synchronization point.

## Confirmed Findings

### 1. Mid-tick `Complete()` discipline is still violated in live gameplay/runtime systems

Repository evidence:

- `Complete()` match count in first-party shipping scripts from earlier scan: `121`

This raw count is not the finding by itself.
The finding is confirmed by representative runtime systems below.

### 2. `HazardZoneManager` completes jobs inside `Tick()`

Mandate conflict:

- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`: `Never call .Complete() in middle of Tick(). EVER.`
- `AGENTS.md`: `JobHandle.Complete() in mid-frame hot paths` is forbidden.

Direct source evidence:

- `Assets/_Project/Scripts/Gameplay/HazardZoneManager.cs`
  - `public void Tick(float deltaTime)`
  - `ConsumeCompletedJob();`
  - `ConsumeCompletedJob()` performs `_jobHandle.Complete();`

Representative lines:

- `Tick(float deltaTime)` at runtime cadence
- `if (!_jobRunning || !_jobHandle.IsCompleted) return;`
- `_jobHandle.Complete();`

Assessment:

- This is not teardown.
- This is not an explicit end-of-frame swap window.
- This is a runtime tick-time sync point.

### 3. `HectonPlayerMotor` completes scheduled sweep jobs inside gameplay consumption flow

Mandate conflict:

- same as above

Direct source evidence:

- `Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs`
  - scheduled sweep consumption path checks `IsCompleted`
  - then immediately calls `_scheduledSweepHandle.Complete();`

Representative lines:

- `if (!_scheduledSweepPending || !_scheduledSweepHandle.IsCompleted) return false;`
- `_scheduledSweepHandle.Complete();`

Assessment:

- This is a gameplay movement system.
- Completion is happening in the runtime resolve path, not a dispatcher-owned swap boundary.

### 4. `SubmarineFluidDynamics` completes transfer and mass-property jobs inside `FixedTick()`

Mandate conflict:

- `AGENTS.md`: `Schedule()+Complete() in same Tick/hot path method` is forbidden.
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`: completion is only allowed in designated swap windows or explicit cold/approved sync points.

Direct source evidence:

- `Assets/_Project/Scripts/SubmarineFluidDynamics.cs`
  - `public void FixedTick(float fixedDeltaTime)`
  - begins by calling:
    - `ConsumeCompletedFluidTransfer();`
    - `ConsumeCompletedFloodMassProperties();`
  - those methods call:
    - `_fluidJobHandle.Complete();`
    - `_massPropertiesJobHandle.Complete();`

Assessment:

- The class has front/back buffer behavior.
- The actual completion still happens inside `FixedTick()`.
- That is runtime hot-path synchronization, not isolated end-of-frame swap ownership.

### 5. `HectonSurfaceWeatherDirector` completes weather math work during `SlowTick()`

Mandate conflict:

- `SlowTick()` is still runtime cadence, not a cold init path.

Direct source evidence:

- `Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs`
  - `SlowTick()` calls `TryCompleteWeatherMathJob();`
  - `TryCompleteWeatherMathJob()` performs `_weatherJobHandle.Complete();`

Assessment:

- Lower cadence does not make it a legal swap window.
- This is still a runtime service synchronizing work inside a gameplay-facing execution lane.

### 6. Runtime scatter pipeline completes sampling jobs inside active state-machine flow

Mandate conflict:

- same as above

Direct source evidence:

- `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs`
  - `public void Tick(float dt)` drives scatter runtime
- `Assets/_Project/Scripts/WorldProceduralScatterDirectorSamplingPipeline.cs`
  - in runtime sampling state:
    - if job is complete, `_samplingJobHandle.Complete();`
    - then `ProcessCompletedScatterSampling();`

Representative lines:

- `if (!_samplingJobHandle.IsCompleted) return true;`
- `_samplingJobHandle.Complete();`
- `_scatterState = ScatterState.Processing;`

Nuance:

- Separate bootstrap/editor prime paths are explicitly marked cold.
- The validated problem here is the runtime state-machine completion path.

Assessment:

- This is not only a bootstrap exception.
- The active world scatter runtime is still using tick-time completion.

## System-Level Assessment

Native lifetime discipline:

- Partially mature.
- Deferred disposal patterns exist and are used in several important systems.

Job completion discipline:

- Not mature enough.
- Multiple live runtime systems still use `if (handle.IsCompleted) handle.Complete();` directly inside `Tick`, `FixedTick`, or equivalent runtime consumption methods.

Architecture implication:

- The project understands job scheduling and buffer ownership.
- It has not finished the harder part: enforcing one legal completion boundary across systems.

## What The Project Objectively Missed In This Phase

- One enforced completion policy for runtime jobs.
- True swap-window ownership instead of per-system local completion habits.
- Elimination of tick-time `Complete()` calls from gameplay, world, and service systems.
- Broader propagation of already-correct `Dispose(handle)` discipline into all native-owner systems.

## Regression Model

CPU:

- Mid-tick `Complete()` serializes worker progress back onto main thread and converts job systems into disguised synchronous work.

GC:

- The primary risk here is not GC.
- The risk is stall-driven frame instability and hidden sync debt.

Memory:

- Mixed completion/disposal discipline raises use-after-free and stale-buffer risks when one system assumes a handle is effectively done while another still treats it as asynchronous.

Cadence:

- Teams are using multiple completion styles at once.
- That makes regression review slower and less reliable.

Correctness:

- Runtime systems can appear stable in light testing while still carrying sync-point spikes or dependency-order bugs under load.

## Verification Status

Static verification only.

Not performed:

- Profiler confirmation of stall locations
- Jobs Debugger validation
- frame-time capture on MX350 target hardware
- runtime swap-window instrumentation

Final status: PENDING VERIFICATION
