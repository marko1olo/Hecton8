# Event Bus / Spatial Hash Compile Delta - 2026-05-01

Status: `PENDING VERIFICATION`

## Mandates Followed

- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `.agents-skills/MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`

## What Was Wrong

Unity had active compile failures from partial source migrations:

- `SargassumGlobalDragManager` was converted to a queue-backed listener registry, while consumers still compiled against old typed delegate registration names in stale import passes.
- `EmergencyServiceRelayEvents` was converted to listener registration, while `EmergencyServiceRelayDirector` compiled against removed `RegisterRelayActivated` / `UnregisterRelayActivated` symbols in stale import passes.
- `HectonSpatialHash.RebuildCellOccupancyJob` passed `NativeArray<T>` indexer output directly as `in`, which C# rejects because the indexer result is not a ref-return variable.
- Three Unity 6 warning sources still used obsolete `Object.GetInstanceID()`.

## What Changed

- `HectonSpatialHash` now copies `Entries[i]` into a local `SpatialEntry` before passing it by `in` to the Burst rebuild helper.
- Sargassum consumers now use `ISargassumGlobalDragEventListener` registration and unregister through `SargassumGlobalDragManager.Register(this)` / `Unregister(this)`.
- `EmergencyServiceRelayDirector` now uses `IEmergencyServiceRelayEventListener` registration through `EmergencyServiceRelayEvents.Register(this)` / `Unregister(this)`.
- Obsolete Unity 6 identity calls were replaced with `EntityId.ToULong(GetEntityId())` casts in:
  - `Assets/_Project/Scripts/World/EmergencyServiceRelayEvents.cs`
  - `Assets/_Project/Scripts/World/DepthZoneDirector.cs`
  - `Assets/_Project/Scripts/UI/DiegeticPanelController.cs`

## Queue Recheck

`Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/06_CRITICAL_ACTION_QUEUE.md` still names these open source-level physics mask risks:

- `AutonomousExtractorSystem`
- `WorldCaveDirector`
- `WorldProceduralFieldSampler`

Current source recheck found those exact query sites already narrowed:

- `Assets/_Project/Scripts/Construction/AutonomousExtractorSystem.cs` uses `HectonLayerMasks.StrictInteractionLayerMask`.
- `Assets/_Project/Scripts/WorldCaveDirector.cs` uses `HectonLayerMasks.TerrainLayerMask | HectonLayerMasks.VoxelCaveLayerMask`.
- `Assets/_Project/Scripts/WorldProceduralFieldSampler.cs` uses `HectonLayerMasks.TerrainLayerMask | HectonLayerMasks.VoxelCaveLayerMask`.

`HectonSpatialHash` queue risk also drifted from the older audit wording. Current source contains slot/generation handles, a queued-free duplicate guard, and current-handle validation. This is source evidence only; runtime handle churn and long-session correctness still need Play Mode profiling.

## Job Barrier Static Slice

The broad `.Complete()` sweep is still not clean. It is too large for blind mass-editing.

Concrete recheck of named queue items:

- `ProximityColliderSystem.Tick` checks `_jobHandle.IsCompleted` before `Complete()` and returns if the job is still running.
- `HectonFluidEngine.PostFixedTick` checks `_scheduledBuoyancyHandle.IsCompleted` before `Complete()` and applies forces only after completion.
- `SaveManager.Tick` checks `_integrityScanHandle.IsCompleted` before `Complete()`. `SaveManager.StageIntegrityPayload(...)` can still force-complete an active integrity scan when a new payload is staged.
- `HectonWorldGenerator` has deferred retirement for chunks whose PhysX bake is still in flight. The remaining forced path is `StopStreaming()` -> `CompletePendingPhysicsBakes()`, which completes scheduled/canceled physics bake handles before clearing the bake queue.

May 2 correction: the named `ProximityColliderSystem.Tick`, `HectonFluidEngine.PostFixedTick`, and `SaveManager.Tick` `.Complete()` statements above are stale by strict current source grep. Current `.Complete(` hits under `Assets/_Project/Scripts` are dispatcher request completion callbacks in `ItemCatalog.cs` / `Optimization/AssetLifecycleGovernor.cs` and one explicit `JobHandle.Complete()` in `World/DispatcherJobSwap.cs`.

Do not remove the `HectonWorldGenerator` teardown completion without adding a real deferred shutdown owner. The current forced complete can stall, but skipping it risks destroying or clearing mesh/collider state while `Physics.BakeMesh` is still running.

## Editor / MCP Evidence

This section is superseded for latest compile line numbers by:

- `Docs/Reports/2026-05-01_COMPILE_STABILIZATION_CONTINUATION.md`

The event-bus/spatial-hash pass did reach a clean MCP console read at the time of that pass.
Later continuation work restored `VegetationJobRecovery.cs.meta`, reached a newer `Editor.log` compile/reload success, and then found one stale MCP console internal-build entry that could not be used as global clean-console proof.

Evidence source:

```text
C:\Users\danat\AppData\Local\Unity\Editor\Editor.log
```

Latest local scan:

```text
time: 2026-05-01 16:39:42
editor_log_last_write: 2026-05-01 16:39:40
total_lines: 14863
latest Tundra build success: line 14575
latest Mono reload: line 14663
strict lines after latest success: 0
```

Latest superseding continuation scan:

```text
time: 2026-05-01 17:52
latest Tundra build success: line 103944
latest Mono reload: line 104086
strict lines after latest success: 0
MCP console: 0 error/warning entries after Bee/backend recovery
```

Strict line set:

- `error CS*`: `0`
- `warning CS*`: `0`
- `Burst error`: `0`
- `Exception:`: `0`
- `Resource ID out of range`: `0`

## Regression Model

CPU: no new per-frame loops or job barriers were added. The spatial-hash fix is one local struct copy per entry inside an already scheduled rebuild job.

GC: no managed allocations were added to hot paths. Listener registration uses existing registry buckets.

Memory: no native capacities, render buffers, scenes, or assets were changed.

Cadence: no dispatcher order, tick registration cadence, or event flush phase was changed.

Correctness: source now compiles after the listener API migrations. Runtime event ordering, Play Mode behavior, GC, and long-session memory retention remain unverified.

STATUS: PENDING VERIFICATION
