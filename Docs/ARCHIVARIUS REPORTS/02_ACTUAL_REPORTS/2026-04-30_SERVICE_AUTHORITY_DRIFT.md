# SERVICE AUTHORITY DRIFT

Date: 2026-04-30
Status: PENDING VERIFICATION
Scope: current source-backed audit of active runtime service owners that still mix singleton ownership, `DontDestroyOnLoad`, and `GlobalRegistry` publication
Mandates followed: `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `STRM_Persistent_Object_Registry.txt`

## Purpose

The active docset already had:

- broad singleton inventory
- bootstrap authority truth
- registry authority matrix

What it did not have was one narrow current-source report for the uncomfortable middle state:

- active service owners that already publish into `GlobalRegistry`
- but still retain singleton ownership and often `DontDestroyOnLoad`
- with some services still using duplicate or mixed registration paths

This file exists to state that middle state directly.

## Method

Evidence in this pass came from:

1. direct source reads of current active service owners
2. line-level verification of `Awake`, `InitializeService`, `OnEnable`, `OnDisable`, and `GlobalRegistry.Register*` paths
3. recheck of one older native-lifetime accusation against `VoxelDynamicNavGridRuntime`
4. same-day reread after a narrow `InputDispatcher` lifecycle patch

This pass does not prove runtime malfunction.
It proves current initialization and authority shape.

## Findings

| ID | File | Current issue | Severity | Evidence |
|---|---|---|---|---|
| SAD-01 | `Assets/_Project/Scripts/Core/InputDispatcher.cs` | core input service still mixes singleton ownership, `DontDestroyOnLoad`, and registry publication; same-instance duplicate publish path was source-patched on `2026-04-30`, but live revalidation is still missing | MEDIUM | `_instance` ownership, `DontDestroyOnLoad(gameObject)`, and registry publication still coexist in the file; `TryRegisterInputService()` / `TryUnregisterInputService()` now gate duplicate publish/unpublish attempts |
| SAD-02 | `SceneRuntimeService.cs`, `PlayerRuntimeContextService.cs`, `EnvironmentRuntimeContextService.cs`, `ConstructionManager.cs`, `AbyssalThermalManager.cs`, `QuestManager.cs` | current active service owners still mix singleton ownership with `GlobalRegistry` service publication instead of one pure bootstrap-owned path | HIGH | each file keeps `Awake()` singleton ownership and separate registry publication path documented below |

## Detailed Evidence

### SAD-01 - InputDispatcher Still Has Mixed Input Authority

`InputDispatcher` is already part of the active registry-facing core shell.
It still keeps three different authority signals at once:

1. singleton ownership
2. `DontDestroyOnLoad`
3. registry publication through `GlobalRegistry`

Current source-backed shape:

- `Awake()` calls `EnsureSingletonOwnership()`
- `EnsureSingletonOwnership()` assigns `_instance = this`
- `InitializeService()` does:
  - `DontDestroyOnLoad(gameObject)`
  - `TryRegisterToDispatcher()`
  - `TryRegisterInputService()`
- `OnEnable()` also routes through `TryRegisterInputService()` when `_isInitialized`
- `TryRegisterInputService()` now short-circuits if the current instance is already the active `GlobalRegistry.Input`
- `TryUnregisterInputService()` now gates unpublish on the same local state and current registry owner

The duplicate same-instance publish path that existed earlier in the day was removed at source level on `2026-04-30`.
What remains is architecture drift, not the original duplicate-call shape:

- the service is still singleton-owned
- still persistent across scenes
- still registry-published rather than purely bootstrap-owned through one narrow path

### SAD-02 - Active Service Owners Still Live In Mixed Mode

The following current service owners all expose the same architecture drift pattern:

| File | Singleton ownership | Persistence | Registry publication |
|---|---|---|---|
| `Core/SceneRuntimeService.cs` | `_instance = this` in `Awake()` | `DontDestroyOnLoad(gameObject)` | `RegisterSceneService(this)` + `RegisterUpdatable(this)` |
| `Core/PlayerRuntimeContextService.cs` | `_instance = this` via `EnsureSingletonOwnership()` | `DontDestroyOnLoad(gameObject)` | `RegisterPlayerRuntimeContext(this)` + `RegisterUpdatable(this)` |
| `Core/EnvironmentRuntimeContextService.cs` | `_instance = this` via `EnsureSingletonOwnership()` | `DontDestroyOnLoad(gameObject)` | `RegisterEnvironmentRuntimeContext(this)` + `RegisterUpdatable(this)` |
| `ConstructionManager.cs` | `_instance = this` in `Awake()` | `DontDestroyOnLoad(gameObject)` | `RegisterLogisticsService(this)` + `RegisterUpdatable(this)` |
| `World/AbyssalThermalManager.cs` | `_instance = this` in `Awake()` | no `DontDestroyOnLoad` in inspected slice, but still singleton-owned runtime root | `RegisterThermodynamicsRuntime(this)` + tick registration |
| `Quest/QuestManager.cs` | `Instance = this` in `Awake()` | no `DontDestroyOnLoad` in inspected slice | `RegisterQuestRuntime(this)` |

Interpretation:

- the project is not purely singleton-driven anymore
- but these services are also not purely bootstrap-owned interface publishers
- the runtime authority model is mixed

That is already hinted at by older docs.
This report narrows it to currently active service owners with line-level proof.

## Rechecked Non-Findings

### ConstructionManager Save Registration Accusation Is Stale In Current Source

An earlier same-day audit slice said `ConstructionManager` still bypassed the registry-facing save contract.
Current source does not support that claim.

Current facts:

- `TryRegisterSaveParticipant()` resolves `ISaveService saveService = GlobalRegistry.Save`
- registration uses `saveService.Register(this)`
- `TryUnregisterSaveParticipant()` resolves `GlobalRegistry.Save` and uses `saveService.Unregister(this)`

This does not prove every save participant in the repo already migrated off `SaveManager.Instance`.
It does prove `ConstructionManager` no longer belongs in that accusation bucket.

### VoxelDynamicNavGridRuntime Is Not A Confirmed Native-Lifetime Hole In Current Source

An older atlas note said:

- `VoxelDynamicNavGridRuntime.cs` owns persistent arrays without an `OnDisable` or `OnDestroy` disposal path

Current source readback does not support that accusation.

Relevant facts:

- `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] ResetRuntime()` calls `DisposeAll()` at `:496-500`
- `DisposeAll()` releases global native containers like `_dirtyVolumes`, `_pendingObstacleClears`, and `_pendingObstacleClearSpill` at `:1099-1131`
- `UnregisterVolume(...)` calls `record.Dispose()` at `:1085-1096`
- `ReleaseVoxelBuffers(...)` disposes per-record voxel buffers at `:1265-1286`

This does not prove perfect runtime lifetime behavior.
It does prove the older specific accusation is stale and should not remain in the active truth layer.

## Current Reading

What looks good:

- several service owners now do unregister cleanly in `OnDisable`
- the registry-facing surface is real and not fake documentation
- the same-instance `InputDispatcher` publish path was narrowed with an idempotent lifecycle guard
- older native-lifetime accusation against `VoxelDynamicNavGridRuntime` does not hold after reread

What looks weak:

- core service owners still keep singleton state while also claiming registry-driven authority
- service publication is not yet uniformly routed through one bootstrap-owned path
- `InputDispatcher` still remains mixed-authority even after the same-instance registration guard patch

## What This Pass Did Not Prove

- no live post-fix replay of the `InputDispatcher` lifecycle was collected
- no play-mode regression was collected
- no profiler or GCMonitor numbers were collected
- no proof that mixed authority has already caused broken startup in the current session

## Regression Model

| Dimension | Impact |
|---|---|
| CPU | none measured in this pass |
| GC | none measured in this pass |
| Memory | mixed singleton/DDoL ownership increases lifecycle complexity, but no measured memory regression was collected |
| Cadence | startup and teardown paths remain harder to reason about because authority is split across singleton, scene object, and registry layers |
| Correctness | improved because stale accusations were removed and the current `InputDispatcher` fix plus remaining drift are now documented precisely |

## Verdict

Current runtime authority is still mixed in a concrete way:

1. active service owners are publishing into `GlobalRegistry`
2. many of those same owners still keep singleton ownership
3. several also keep `DontDestroyOnLoad`
4. `InputDispatcher` had a same-instance duplicate publish path earlier in the day, and that source-level path has now been guarded
5. active mixed-authority drift still remains even after that narrow fix

That is not a theoretical cleanup wish.
It is current source-backed architecture drift.

STATUS: PENDING VERIFICATION
