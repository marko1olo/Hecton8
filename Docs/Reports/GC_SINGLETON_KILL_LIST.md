# GC and Singleton Kill List

Date: 2026-05-07
Status: PENDING VERIFICATION
Scope: `Assets/_Project`, focused deep scan on `Assets/_Project/Scripts/World` and `Assets/_Project/Scripts/Fauna`
Requested status string: `ETA CODEX VERIFIED`
Actual status: PENDING VERIFICATION. `ETA CODEX VERIFIED` is not defensible because play-mode GC/frame-time was not measured and two local audit helper commands had PowerShell/rg syntax failures before corrected reruns.

## Evidence Inputs

- `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/`: 32 files, 574459 bytes processed.
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/PROJECT_ATLAS.md`: version `1.4.0`; document status is `PENDING VERIFICATION`.
- Mandates followed: `ARCH_Global_Registry_ServiceLocator_DI_Init`, `ARCH_Project_Bootstrap_Sequence_Init_Safety`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `OPT_Native_Memory_Collections_JobSystem_Protocol`, `DBG_Telemetry_Crash_Reporting_PostMortem`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits`.
- MCP console snapshot: first `read_console` returned 0 current error/warning entries for the requested slice. A final recheck failed with `Unity session not ready for 'read_console'`; MCP readiness is therefore unstable. This is not build proof, not play-mode proof, and not 60 FPS proof.

## Verdicts

| Domain | Verdict | Evidence |
|---|---:|---|
| `PROJECT_ATLAS.md` version | PASS | Version line is `1.4.0`. |
| GlobalRegistry singleton coverage | FAIL | 30 active first-party `public static Instance` owners plus 1 archive owner are not registered through `GlobalRegistry`. |
| Hot-path GC in `World/` and `Fauna/` | FAIL | 16 `Update`/`FixedUpdate`/`Tick` scan hits; 1 confirmed managed string interpolation risk, 14 struct-local `new` hits, 1 comment false positive. |
| `DontDestroyOnLoad` authority | FAIL | Multiple managers self-instantiate or persist outside a single sovereign bootstrap contract. |
| Native disposal guard | FAIL | Static scan found 4 files with `.Complete()` and no `.IsCompleted` text guard; 29 more persistent allocation owners dispose without an `IsCompleted` guard. |
| NativeQueue struct alignment | FAIL | Multiple queue payloads are not 16-byte sized; most are not 64-byte padded. |
| `GlobalTelemetryBus` zero-alloc design | PASS WITH WARNING | Uses `NativeArray<TelemetryEvent>` ring/snapshot buffers, no `List.Add`; payload is 32 bytes, not 64-byte cache-line padded. |
| Runtime `GameObject.Find` / `FindObjectOfType` | PASS | No non-editor first-party runtime hits found. |
| Literal entire-project find ban | FAIL | 53 editor-only authoring/validator hits remain. |
| 20 agent evidence logs | FAIL | No complete canonical set proving all 20 agents provided evidence logs and zero console errors was found. |
| 60 FPS budget | FAIL TO CERTIFY | No profiler or play-mode frame-time data was captured in this pass. |

## Illegal `public static Instance` Owners Not Registered In `GlobalRegistry`

Detection: case-sensitive search for `public static ... Instance`, excluding files that register or proxy through `GlobalRegistry`.

| File:Line | Class | Drift Flags | Last Git Agent |
|---|---|---|---|
| `Assets/_Project/Scripts/AsyncLoadHelper.cs:80` | `AsyncLoadHelper` | self-instantiates | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/BeaconNetworkSystem.cs:44` | `BeaconNetworkSystem` | self-instantiates | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/Bootstrap/BootstrapController.cs:20` | `BootstrapController` | static authority outside registry | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/Economy/ScrapManager.cs:28` | `ScrapManager` | static authority outside registry | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/Ecosystem/FaunaGeneticsManager.cs:25` | `FaunaGeneticsManager` | static authority outside registry | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/FieldOperationLogSystem.cs:43` | `FieldOperationLogSystem` | static authority outside registry | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/FlowFieldVisualizer.cs:65` | `FlowFieldVisualizer` | static authority outside registry | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/Gameplay/MissionManager.cs:29` | `MissionManager` | static authority outside registry | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs:63` | `PDAExchangeSystem` | static authority outside registry | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/Gameplay/PlayerExpressionManager.cs:83` | `PlayerExpressionManager` | static authority outside registry | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs:56` | `SuitUpgradeManager` | static authority outside registry | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/HectonDiscoveryManager.cs:58` | `HectonDiscoveryManager` | static authority outside registry | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/Input/InputManager.cs:107` | `InputManager` | self-instantiates, DDOL | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/Input/RebindingManager.cs:44` | `RebindingManager` | self-instantiates, DDOL | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/Input/UserOptionsPersistence.cs:29` | `UserOptionsPersistence` | self-instantiates, DDOL | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/LocalizationManager.cs:94` | `LocalizationManager` | self-instantiates, DDOL | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/Meta/RunModifierController.cs:28` | `RunModifierController` | static authority outside registry | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/Narrative/LoreDatabaseManager.cs:234` | `LoreDatabaseManager` | static authority outside registry | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/Networking/HectonNetworkManager.cs:13` | `HectonNetworkManager` | DDOL | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/Optimization/RenderTexturePool.cs:22` | `RenderTexturePool` | static authority outside registry | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/PDA/PDAMarkerRegistry.cs:76` | `PDAMarkerRegistry` | static authority outside registry | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/PrefabRegistry.cs:70` | `PrefabRegistry` | self-instantiates, DDOL | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/RaycastBatchHelper.cs:63` | `RaycastBatchHelper` | DDOL | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/ScanLogSystem.cs:54` | `ScanLogSystem` | static authority outside registry | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/Tools/SceneTransitionVerifier.cs:30` | `SceneTransitionVerifier` | DDOL | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/Tools/StateRecoveryVerifier.cs:45` | `StateRecoveryVerifier` | DDOL | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/UI/SettingsManager.cs:64` | `SettingsManager` | self-instantiates, DDOL | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/World/EmergencyServiceRelayDirector.cs:24` | `EmergencyServiceRelayDirector` | self-resolves | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/World/EnvironmentalStrainManager.cs:46` | `EnvironmentalStrainManager` | static authority outside registry | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/WorldStateManager.cs:59` | `WorldStateManager` | DDOL | `marko1olo <barsukdana@gmail.com>` |

Archive residue:

| File:Line | Class | Last Git Agent |
|---|---|---|
| `Assets/_Project/_Archive/HectonWaterPhysics.cs:17` | `HectonWaterPhysics` | `marko1olo <barsukdana@gmail.com>` |

## Hot-Path Allocation Inquisition: `World/` and `Fauna/`

Detection: method-body scan for `Update`, `FixedUpdate`, `Tick`, and `FixedTick`; patterns `.ToString()`, `$"`, `new`, `.ToList()`, `.Where()`, `.Select()`, `.Any()`, `.FirstOrDefault()`, `Enumerable.`.

| File:Line | Method | Pattern | Classification | Last Git Agent |
|---|---|---|---|---|
| `Assets/_Project/Scripts/Fauna/ProceduralLeviathanSpineIK.cs:381` | `Tick` | `new` | struct job local, no managed GC by itself | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/Fauna/ProceduralLeviathanSpineIK.cs:393` | `Tick` | `new float3` | struct local, no managed GC by itself | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/World/Biolum/HectonBiolumDiffusionVolume.cs:160` | `Tick` | `new Vector4` | struct local, no managed GC by itself | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/World/Biolum/HectonBiolumDiffusionVolume.cs:161` | `Tick` | `new Vector4` | struct local, no managed GC by itself | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/World/Biolum/HectonBiolumDiffusionVolume.cs:166` | `Tick` | `new Vector4` | struct local, no managed GC by itself | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/World/FloraInteractionManager.cs:1113` | `Tick` | `new Vector4` | struct local, no managed GC by itself | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/World/FloraRegrowthDirector.cs:560` | `Tick` | `new Vector3` | struct local, no managed GC by itself | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/World/GPUScatterDirector.cs:191` | `Tick` | `new Vector4` | struct local, no managed GC by itself | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/World/GPUScatterDirector.cs:204` | `Tick` | `new Vector4` | struct local, no managed GC by itself | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/World/GPUScatterDirector.cs:222` | `Tick` | `new Bounds` | struct local, no managed GC by itself | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/World/GPUScatterDirector.cs:223` | `Tick` | `new Vector3` | struct local, no managed GC by itself | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/World/GPUScatterDirector.cs:224` | `Tick` | `new Vector3` | struct local, no managed GC by itself | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:1114` | `Tick` | `new Bounds` | struct local, no managed GC by itself | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/World/LODSystemManager.cs:353` | `Tick` | `new` | comment false positive: `Schedule new distance calculation job` | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs:867` | `Tick` | `new RenderParams` | struct local, no managed GC by itself | `marko1olo <barsukdana@gmail.com>` |
| `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:1529` | `Tick` | string interpolation | confirmed managed string allocation on compute-dispatch failure path | `marko1olo <barsukdana@gmail.com>` |

No `.ToString()`, LINQ, or `ToList()` hits were found inside the scanned hot-path methods.

## Singleton Authority Drift And Sovereign Bootstrap Refactor

Self-instantiating or DDOL managers outside a clean `GameBootstrapper` ownership contract:

- `InputManager`: `new GameObject("[InputManager]")`, `AddComponent<InputManager>()`, `DontDestroyOnLoad`.
- `RebindingManager`: self-instantiates and persists.
- `UserOptionsPersistence`: self-instantiates and persists.
- `LocalizationManager`: persists and mutates runtime service surface directly.
- `PrefabRegistry`: `EnsureRuntimeInstance()` creates `[PrefabRegistry]` and persists.
- `SettingsManager`: self-instantiates and persists.
- `WorldStateManager`: persists through DDOL.
- `HectonNetworkManager`: static instance plus DDOL.
- `RaycastBatchHelper`: static instance plus DDOL.
- `SceneTransitionVerifier` and `StateRecoveryVerifier`: tool singletons persisted through DDOL.

Mixed-authority registered services requiring follow-up review:

- `GlobalPhysicsStateManager`: registers with `GlobalRegistry` but also calls DDOL.
- `SystemDispatcher`: registers with `GlobalRegistry` but also calls DDOL.
- `SceneRuntimeService`, `PlayerInventoryManager`, `PlayerRuntimeContextService`, `PlayerSensoryManager`, and `OceanKinematicsRuntimeService`: bootstrap-style runtime roots with DDOL; acceptable only if `GameBootstrapper` is the sole creator.

Refactor proposal:

1. Define each persistent manager as a bootstrap-owned service step with explicit dependencies.
2. Move creation into the Sovereign Bootstrap sequence only; delete `Instance` lazy creation and `EnsureRuntimeInstance()` paths.
3. Register the service through `GlobalRegistry` once after construction and dependency injection.
4. Replace external `Instance` reads with `GlobalRegistry` contract properties or constructor/bootstrap injection.
5. Keep DDOL inside bootstrap-owned root only; service classes should not call DDOL themselves.

## NativeQueue Struct Alignment And Padding

Static size math uses Unity value-type sizes: `Vector3`/`float3` = 12, `Vector4`/`float4` = 16, `int`/`uint`/`float` = 4, `ushort`/`half` = 2, `byte` = 1. This is static proof, not `UnsafeUtility.SizeOf<T>()` runtime proof for every payload.

| Payload | Math Size | 16-byte | 64-byte | Verdict |
|---|---:|---:|---:|---|
| `GlobalTelemetryBus.TelemetryEvent` | 32 | PASS | FAIL | no `List.Add`, but not cache-line padded |
| `AtlasSignalEventPayload` | 24 | FAIL | FAIL | queue payload not 16-byte sized |
| `Atlas6EventPayload` | 20 | FAIL | FAIL | queue payload not 16-byte sized |
| `CraftingEventPayload` | 40 | FAIL | FAIL | queue payload not 16-byte sized |
| `InteractionEventPayload` | 12 | FAIL | FAIL | queue payload not 16-byte sized |
| `ForcePacket` | explicit 48 | PASS | FAIL | 16-byte multiple, not cache-line isolated |
| `PDAEventPayload` | 24 | FAIL | FAIL | queue payload not 16-byte sized |
| `ScanEventPayload` | 36 | FAIL | FAIL | queue payload not 16-byte sized |
| `NotificationEventPayload` | 8 | FAIL | FAIL | queue payload not 16-byte sized |
| `SplashEvent` | 52 | FAIL | FAIL | queue payload not 16-byte sized |
| `EntropyYieldJob.ItemDropData` | 28 | FAIL | FAIL | queued drop payload not 16-byte sized |
| `PersistentWorldRegistry.AbsoluteUniversePosition` | explicit 48 | PASS | FAIL | 16-byte transfer size, not 64 |
| `PersistentWorldRegistry.EntityDataRecord` | explicit 64 | PASS | PASS | cache-line sized |
| `PersistentWorldRegistry.PoolSlotData` | explicit 40 | FAIL | FAIL | compact save layout, not queue safe |
| `PersistentWorldRegistry.PersistentWorldItemRecord` | explicit 204 | FAIL | FAIL | not 16/64 aligned |
| `VoxelDynamicNavGridRuntime.DirtyVolumeRequest` | 8 | FAIL | FAIL | queued request not padded |
| `VoxelDynamicNavGridRuntime.DynamicObstacleClearRequest` | 24 | FAIL | FAIL | queued request not padded |

False sharing verdict: any payload consumed from arrays or parallel writer/reader staging without per-worker partitioning should be padded to 16-byte minimum, 64-byte when slots are written by adjacent workers. Current queue payload set fails that standard broadly.

## Native Collection Disposal Tracking

Broad textual scan across `Assets/_Project/Scripts`:

- `Allocator.Persistent` owners found with `HAS_ISCOMPLETED_GUARD`: 42.
- `COMPLETE_WITHOUT_ISCOMPLETED_GUARD`: 4.
- `NO_ISCOMPLETED_GUARD`: 29.
- `NO_TEARDOWN`: 44.

Critical crash-risk rows from the static scan:

| Risk | File | Persistent Lines | Last Git Agent |
|---|---|---|---|
| `.Complete()` without `.IsCompleted` text guard | `Assets/_Project/Scripts/Core/SystemDispatcher.cs` | 562, 567, 572 | `marko1olo <barsukdana@gmail.com>` |
| `.Complete()` without `.IsCompleted` text guard | `Assets/_Project/Scripts/Interaction/PhysicalHandController.cs` | 388, 390, 392, 394, 396 | `marko1olo <barsukdana@gmail.com>` |
| `.Complete()` without `.IsCompleted` text guard | `Assets/_Project/Scripts/PlayerInventory.cs` | 377-403 | `marko1olo <barsukdana@gmail.com>` |
| `.Complete()` without `.IsCompleted` text guard | `Assets/_Project/Scripts/WorldProceduralFieldSampler.cs` | 1938, 3687, 3697 | `marko1olo <barsukdana@gmail.com>` |

High-volume no-guard review list:

- `GlobalPhysicsStateManager.cs`
- `SpatialAudioManager.cs`
- `HectonSurvivalSystem.cs`
- `ContextualPhysicalIkRig.cs`
- `HectonDistantLandmarkRenderer.cs`
- `HectonHLODRenderer.cs`
- `SargassumCutManager.cs`
- `WreckMaterialRegistry.cs`
- `WorldGenerativeGeologyVoxelBridgeDirector.cs`

Known false-positive correction: `VoxelDynamicNavGridRuntime.cs` allocates queues and arrays, but lifecycle is split into `VoxelDynamicNavGridRuntimeLifecycle.cs`, which calls `VoxelDynamicNavGridRuntime.DisposeAll()` in both `OnDisable` and `OnDestroy`. The runtime implementation also checks `PendingDynamicUpdateHandle.IsCompleted` before record disposal. It should not be counted as an active navgrid disposal defect without a cross-file AST pass.

## GlobalTelemetryBus Validation

`Assets/_Project/Scripts/Core/GlobalTelemetryBus.cs`:

- Uses `NativeArray<TelemetryEvent> _ringBuffer`.
- Uses `NativeArray<TelemetryEvent> _snapshotBuffer`.
- Uses preallocated `byte[] _exportBytes`.
- `LateFrameUpdate()` copies ring data into the snapshot buffer by index.
- No `List<T>` or `List.Add` was found in the file.
- `TelemetryEvent` math size is 32 bytes: five 4-byte scalar fields plus one `float3` = 20 + 12.

Verdict: do not delete for `List.Add`; that accusation is false against current source. Required hardening is struct padding/alignment policy, not bus removal.

## Find Call Sweep

Runtime first-party result:

- `NO_RUNTIME_FIND_CALLS_OUTSIDE_EDITOR_PATHS`

Editor-only residue:

- 53 hits under `Assets/_Project/Editor` and `Assets/_Project/Scripts/Editor`.
- Representative files: `ObjectSpawner.cs`, `WorldRuntimeBootstrapAuthoring.cs`, `ResourceWorldBootstrapAuthoring.cs`, `RelayRouteAuthoringUtility.cs`, `WorldStreamingWiringValidator.cs`.

Verdict: runtime is clean for this pattern. Literal entire-project eradication is incomplete because editor authoring tools still use scene search.

## Liar Detection

Agents who claimed "0 GC" but left strings in audited hot paths:

- `marko1olo <barsukdana@gmail.com>`: `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:1529` uses string interpolation inside a `Tick` failure path.

No other `.ToString()`, LINQ, `ToList()`, or interpolation hits were found in the scanned `World/` and `Fauna/` hot-path methods.

## Regression Model

- CPU: singleton/DDOL drift increases boot ordering ambiguity; native disposal without completion guards can force stalls or crash if corrected with blocking `.Complete()`.
- GC: confirmed managed allocation risk is the string interpolation in `SargassumMicroFaunaBoids.Tick`; struct `new` hits are not managed GC by themselves.
- Memory: native persistent allocations have uneven teardown evidence; static event buses with `RuntimeInitializeOnLoadMethod` disposal need runtime validation under domain reload disabled.
- Cadence: unresolved DDOL owners bypass `GameBootstrapper` dependency ordering and can access services before registry authority is stable.
- Correctness: non-16-byte NativeQueue payloads can create ABI/layout mismatch risk when Burst jobs, memcpy, or GPU/native mirrors assume aligned records.

## Final Audit Status

Status: PENDING VERIFICATION

`ETA CODEX VERIFIED` is rejected as a project status. Current evidence proves a static audit was performed and one MCP console read returned an empty current error/warning slice before the final MCP recheck failed readiness. It does not prove 60 FPS, 0 B/frame, full agent evidence compliance, or complete bootstrap sovereignty.
