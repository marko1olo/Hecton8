# Editor Log Console Stabilization

Date: 2026-05-07
Status: PENDING VERIFICATION
Scope: local Unity `Editor.log` evidence after console-spam mitigation

## Mandates Followed

- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

## Verification Boundary

MCP was unavailable for this pass. Evidence came from:

- `C:\Users\danat\AppData\Local\Unity\Editor\Editor.log`
- current source diff under `Assets/_Project/Scripts`
- shader importer warnings in the same log

This report does not prove:

- clean Play Mode
- zero GC
- stable frame time
- memory retention
- save/load runtime safety
- scene or prefab wiring correctness

## Original Console Spam

The user-reported symptom was repeated Unity console spam:

```text
Resource ID out of range in SetResource: 1048576 (max is 1048575)
Resource ID out of range in SetResource: 1048577 (max is 1048575)
Resource ID out of range in SetResource: 1048578 (max is 1048575)
```

The important number is `1048576 = 2^20`, exactly one past Unity's reported maximum resource id.

## Root-Cause Class

Static source inspection found repeated first-party `BatchRendererGroup.SetBatchBuffer(...)` calls in render owners. Rebinding the same `GraphicsBuffer` every frame can keep allocating/registering Unity-side resource ids until the internal id range is exhausted.

Mitigation applied: cache the currently registered `GraphicsBuffer` per BRG owner and call `SetBatchBuffer(...)` only when the buffer reference changes.

Patched owners:

- `Assets/_Project/Scripts/World/HectonHLODRenderer.cs`
- `Assets/_Project/Scripts/World/HectonDistantLandmarkRenderer.cs`
- `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs`
- `Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs`
- `Assets/_Project/Scripts/World/WreckMaterialRegistry.cs`

## Secondary Console Noise Removed

The same local log showed compile warnings after the initial render mitigation:

- `CraftingEvents.cs`: obsolete `Object.GetInstanceID()`
- `InteractionEvents.cs`: obsolete `Object.GetInstanceID()`

Patched event-lane hash paths now use Unity 6 `GetEntityId()` through `EntityId.ToULong(...)`.

Files:

- `Assets/_Project/Scripts/CraftingEvents.cs`
- `Assets/_Project/Scripts/Interaction/InteractionEvents.cs`

A later import surfaced the same Unity 6 obsolete API warning class in editor-only third-party tooling:

- `Assets/Dynamic Decals/Scripts/ProjectionRenderer.cs`
- `Assets/Dynamic Decals/Scripts/Modifiers/NineSprite/NineSprite.cs`
- `Assets/Plugins/Demigiant/DOTweenPro/Editor/DOTweenPreviewManager.cs`

Those changes were limited to direct Unity API replacement:

- `GetInstanceID()` -> `EntityId.ToULong(GetEntityId())`, cast back to the existing serialized `int` field.
- `FindObjectsByType<T>(FindObjectsSortMode.None)` -> `FindObjectsByType<T>(FindObjectsInactive.Exclude)`.

No third-party render logic, material setup, package topology, or runtime wrapper was added.

The log also showed shader importer warnings for mixed line endings in first-party shader files. These files were normalized to CRLF only:

- `Assets/_Project/Art/Shaders/Hecton_AbyssalVoxelRock.shader`
- `Assets/_Project/Art/Shaders/Hecton_CoralMaster.shader`
- `Assets/_Project/Art/Shaders/Hecton_DryZoneLit.shader`
- `Assets/_Project/Art/Shaders/Hecton_IndirectVegetation.shader`
- `Assets/_Project/Art/Shaders/Hecton_KelpMaster.shader`
- `Assets/_Project/Art/Shaders/SuitVisor.shader`

No shader logic, keywords, material properties, or render settings were changed by the line-ending normalization.

## Continuation Compile Recovery

Later on `2026-05-01`, Unity recompiled after several split helper files had been written to disk without matching `.meta` files. The active compile failures were missing partial/helper symbols, not missing architecture:

- `VegetationFlowFieldIntegrator.cs`
- `VegetationNavGridSynchronizer.cs`
- `VegetationThermalSampler.cs`
- `PlayerSwimMotor.cs`
- `ScatterCandidateEvaluator.cs`

Recovery applied:

- Added missing `.meta` files for those new first-party helper files.
- Preserved the existing `HectonMapMagicVegetationBridge` partial split instead of adding fallback stubs.
- Updated `DroneFleetManager` snapshot publication to pass the expanded `HectonDroneFleetSnapshot` telemetry fields.
- Kept the later, source-present `ResolveDroneCullingKernels()` implementation and removed the duplicate early stub during compile-race cleanup.
- Normalized `Assets/_Project/Art/Shaders/Hecton_IndirectVegetation.shader` again after Unity reported a fresh mixed-line-ending import warning.

Mandates followed: `OPT_Zero_GC_Policy_AllocFree_Mandate`, `OPT_Native_Memory_Collections_JobSystem_Protocol`, `ARCH_Global_Registry_ServiceLocator_DI_Init`, `ARCH_Project_Bootstrap_Sequence_Init_Safety`, `DBG_Telemetry_Crash_Reporting_PostMortem`.

## Current Editor.log Snapshot

Recorded clean scan boundary after the continuation pass:

- total log lines: `73359`
- recorded latest `Tundra build success`: line `73134`
- recorded latest `Mono: successfully reloaded assembly`: line `73275`
- scan start: line `73135`

Fresh post-marker counts:

| Signal | Count |
|---|---:|
| `error CS` | `0` |
| `warning CS` | `0` |
| `Exception` | `0` |
| `Resource ID out of range in SetResource` | `0` |
| `There are inconsistent line endings` | `0` |
| TMP `m_AtlasTextures` unassigned exception | `0` |
| strict top-level exception lines | `0` |

Residual Editor reload noise after the same boundary:

- `not valid. Loading of assembly skipped`: package/test/transient assemblies under `Library/ScriptAssemblies`; source files were not present at those paths.
- `GetVirtualKey: Could not map char`: Unity editor keyboard mapping noise during domain reload.
- `Curl error 42: Callback aborted`: single editor/network callback abort during reload.

These residual lines are documented as editor reload noise, not current project C# compile errors.

## 2026-05-01 Late Compile/Burst Stabilization

Additional compile passes later surfaced source-level API drift from concurrent partial migrations. The current repair pass resolved the active blockers without changing scenes, prefabs, project settings, tags, layers, or URP settings.

Primary repairs:

- `SargassumCutResponder.cs`: removed an incomplete local cut-buffer/MPB path and routed cut impulses through the existing `SargassumCutManager` global cut mask.
- `HectonSubmarineOS.cs`: restored typed `OnSnapshotUpdated` / `OnLogRequested` compatibility on top of the `NativeQueue` flush path.
- `PlayerFlashlight.cs` and `SargassumMicroFaunaBoids.cs`: restored typed flashlight compatibility while preserving deferred `NativeQueue` dispatch; fixed the flicker `energyPercent` scope error.
- `ModAssetManager.cs`, `ModSettingsRegistry.cs`, `ModWorldPersistenceManager.cs`: completed `uint` hash/AUP migration surfaces that had been left half-applied.
- `SaveBinaryStorage.cs`: reconciled UTF-16 copy bounds, LZ4 return paths, dictionary scratch, and mod payload call signatures after partial save-code edits.
- `ModCommandDispatcher.cs`, `ModSpatialContracts.cs`, `GPUScatterDirector.cs`: aligned mod command spatial/render contracts with the source-present command path.
- `GlobalTelemetryBus.cs`: isolated managed `WaitCallback` initialization from Burst-visible static state and marked native-copy telemetry accounting with `[BurstDiscard]`.
- `GlobalRegistry.cs`: completed the `NativeQueue<RegistryEventPayload>` service-rebound lane and managed sidecar slot cleanup after a partial queue migration.
- `RebindingManager.cs`: moved the registry-backed input binding service into the `Hecton8.Core` assembly boundary and left a type-free tombstone at the old `Input` path to cover stale Unity/Bee source lists without reintroducing the old singleton.
- `HabitatGraphManager.cs`: current source now contains the pressure-buckling stress method referenced by the hydrodynamic stress pass after a compile-race import exposed the missing member.
- `AbyssalThermalManager.cs`: current source now contains the thermal-infiltration method referenced by `SlowTick`, so the habitat heat-transfer fields are no longer dead serialized fields.

Mandates followed:

- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`
- `REND_Instanced_Flora_Physics.txt`

Latest local `Editor.log` boundary after this pass:

- total log lines: `137112`
- latest `Tundra build success`: line `136320`
- latest `Mono: successfully reloaded assembly`: line `136460`
- scan start: line `136461`

Fresh post-marker counts:

| Signal | Count |
|---|---:|
| `error CS` | `0` |
| `warning CS` | `0` |
| `Burst error` / `BC####` | `0` |
| `Exception` / `NullReferenceException` / `MissingReferenceException` | Present after the compile boundary; observed in MCP WebSocket/TLS transport stack, not C# compile output |
| `Resource ID out of range in SetResource` | `0` |
| strict project C# compile/runtime error lines | `0` |
| MCP WebSocket/TLS transport lines | Present after the compile boundary |

The final source cleanup in this pass removed the now-unused `Hecton8.Bootstrap.Contracts` reference from `Hecton8.Input.asmdef`; the subsequent compile/reload stayed free of C# compile errors after line `136460`. MCP transport noise did not stay clean: Unity logged `Curl error 35` and WebSocket close-handshake warnings from `com.coplaydev.unity-mcp` after the compile boundary.

This latest marker proves editor script compile only. It does not prove globally clean console, Play Mode, frame time, GC, memory retention, scene wiring, or save/load correctness.

## 2026-05-01 MapMagic Warning Cleanup Delta

Later local `Editor.log` scans surfaced two current `CS0414` warnings:

- `HectonMapMagicVegetationBridge.scatterSnapRaycastDistanceMeters`
- `HectonMapMagicVegetationBridge.scatterSnapRaycastElevationMeters`

The fields are serialized placement-tuning values retained for scene/prefab compatibility after scatter placement moved to resident terrain-cache sampling. The cleanup normalized both values in `Awake()` so the fields are source-used without reintroducing per-placement physics raycasts or deleting serialized data.

Latest local `Editor.log` boundary after this cleanup:

- total log lines: `19592`
- latest `Tundra build success`: line `19510`
- strict post-success signals (`error CS`, `warning CS`, `Burst error`, `Exception`, `Resource ID out of range`): `0`

Related source/docs evidence:

- `Docs/Reports/2026-05-01_HEADLESS_FAUNA_CONSOLE_DELTA.md`

## Regression Model

CPU: BRG buffer rebinding is reduced to buffer-change events instead of repeated same-buffer calls. Runtime frame impact is not measured.

GC: event hash API replacement and BRG buffer caching add no hot-path managed allocations. Shader line-ending normalization is source formatting only.

Memory: expected reduction in Unity-side transient resource-id churn. No Memory Profiler capture exists for before/after.

Cadence: no job scheduling, tick order, or dispatcher cadence was changed in this pass.

Correctness: render buffer registration now assumes `GraphicsBuffer` reference identity is the correct rebind boundary. If Unity requires rebind on hidden buffer handle mutation, this would need a stronger handle-generation guard.

## Remaining Risks

- Play Mode was not launched after the latest local log scan.
- MCP remains unavailable in this pass, so this report uses local `Editor.log` only. MCP transport noise can recur independently of project C# compile state.
- `SetResource` spam can recur if another BRG/graphics owner outside the patched set performs repeated resource rebinding.
- The clean compile is editor/script/import cleanliness, not gameplay certification.
- MCP console proof is absent in this pass.

STATUS: PENDING VERIFICATION
