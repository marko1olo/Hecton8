# System Interconnect Matrix

Date: `2026-05-01`
Status: `PENDING VERIFICATION`

Mandates followed:

- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `STRM_Persistent_Object_Registry.txt`
- `AUD_Acoustic_Sonar_Occlusion_Sensory_Simulation.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`

## Scope Correction

Current modding boundary:

- `Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs` is a blittable-only mod event bridge.
- `Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs` owns sandboxed command queues, AUP rebasing, raycast proxy requests, mod render matrix injection, command-flood throttling, spawn arbitration, and heap-quota eviction events.
- The first-party queue-backed nervous system remains the set of static event lanes flushed by `SystemDispatcher.LateUpdate()`.
- Mod persistent payloads use isolated protected 16 KB indexed sectors in `SaveBinaryStorage`; legacy `SaveData.CustomModData` is retained as a fallback index.

Current-state boundary:

- This matrix maps lane ownership and flush order.
- It is not runtime profiler proof. Code validators passed for the edited scripts; console/play-mode GC proof is separate.
- `Docs/Reports/DOOMSDAY_FLAW_REPORT.md` remains the historic risk authority for event cascade/depth concerns.

## Verified LateUpdate Flush Order

Source: `Assets/_Project/Scripts/Core/SystemDispatcher.cs`

1. `CompleteDispatcherRaycasts()`
2. `ILateFrameTickable.LateFrameTick()` lane pass
3. `ThreadSafeCommandQueue.DrainToMainThread()` if budget remains
4. `Hecton8.Modding.ModCommandDispatcher.DrainLateFrame()`
5. `NarrativeEvents.FlushPending()`
6. `Hecton8.Interaction.InteractionEvents.FlushPending()`
7. `Hecton8.Crafting.CraftingEvents.FlushPending()`
8. `ScanEvents.FlushPending()`
9. `SaveEvents.FlushPending()`
10. `QuestEvents.FlushPending()`
11. `AudioLogEvents.FlushPending()`
12. `HectonSubmarineOsEvents.FlushPending()`
13. `FlashlightEvents.FlushPending()`
14. `WeatherEvents.FlushPending()`
15. `Hecton8.AtlasSignal.AtlasSignalEvents.FlushPending()`
16. `Hecton8.UI.NotificationEvents.FlushPending()`
17. `Hecton8.UI.PDAEvents.FlushPending(MaxPdaEventsPerFrame)`
18. `Hecton8.Bootstrap.SceneBootstrap.FlushPendingEvents()`
19. `ObjectPoolDiagnostics.FlushPending()`
20. `Hecton8.AtlasSignal.Atlas6Events.FlushPending()`
21. `GlobalRegistry.FlushPendingServiceReboundEvents()`
22. `GlobalTelemetryBus.LateFrameUpdate(Time.unscaledTime)`
23. `WorldSpatialHashGrid.LateFrameMaintenance(Time.frameCount)`

## Queue-Backed Lanes

| Lane Owner | Backing Queue | Listener Contract | Raise Surface | Flush Owner | Notes |
| --- | --- | --- | --- | --- | --- |
| `NarrativeEvents` | `NativeQueue<NarrativeEventPayload>` | `INarrativeEventListener` | `RaiseNarrativePOIRegistered`, `RaiseNarrativePOIDisposed`, `RaiseDiscoveryMade`, `RaiseDepthTierReached` | `SystemDispatcher.LateUpdate()` | Also has immediate `INarrativePointOfInterestListener` buckets outside the queue lane. |
| `InteractionEvents` | `NativeQueue<InteractionEventPayload>` | `IInteractionEventListener` | `RaiseItemCollected`, `RaiseInteractionStarted`, `RaiseHoverChanged` | `SystemDispatcher.LateUpdate()` | First-party interaction lane. |
| `CraftingEvents` | `NativeQueue<CraftingEventPayload>` | `ICraftingEventListener` | `RaiseCraftStarted`, `RaiseCraftCompleted`, `RaiseCraftCancelled` | `SystemDispatcher.LateUpdate()` | Crafting lane. |
| `ScanEvents` | `NativeQueue<ScanEventPayload>` | `IScanEventListener` | `RaiseScanTriggered`, `RaiseNodeFound`, `RaiseEntryDiscovered` | `SystemDispatcher.LateUpdate()` | Scanner lane for node discovery and radius scan traffic. |
| `SaveEvents` | `NativeQueue<SaveEventPayload>` | `ISaveEventListener` | `RaiseSaveStarted`, `RaiseSaveCompleted`, `RaiseSaveFailed`, `RaiseLoadStarted`, `RaiseLoadCompleted`, `RaiseLoadFailed`, `RaiseEmergencyBackupRestoreRequested` | `SystemDispatcher.LateUpdate()` | Uses `FixedString` payload fields for slot and failure text. |
| `QuestEvents` | `NativeQueue<QuestEventPayload>` | `IQuestEventListener` | `RaiseActivated`, `RaiseCompleted`, `RaiseFailed`, `RaiseRevertRequested` | `SystemDispatcher.LateUpdate()` | `FlushPending()` also forces `QuestGraphEvaluator.FlushPendingSignals()`. |
| `AudioLogEvents` | `NativeQueue<AudioLogEventPayload>` | `IAudioLogEventListener` | `RaiseLogDiscovered`, `RaisePlaybackStarted`, `RaisePlaybackStopped`, `RaisePlaybackCompleted` | `SystemDispatcher.LateUpdate()` | Audio-log lane only. |
| `AtlasSignalEvents` | `NativeQueue<AtlasSignalEventPayload>` | `IAtlasSignalEventListener` | `RaisePulse`, `RaiseDetected`, `RaiseStrengthChanged`, `RaiseDecoded` | `SystemDispatcher.LateUpdate()` | Holds decoded Atlas signal message IDs through a local lookup table. |
| `NotificationEvents` | `NativeQueue<NotificationEventPayload>` | `INotificationEventListener` | `PushInfo`, `PushWarning`, `PushCritical` | `SystemDispatcher.LateUpdate()` | UI notification lane. |
| `PDAEvents` | `NativeQueue<PDAEventPayload>` | `IPDAEventListener` | `RaiseOpened`, `RaiseClosed`, `RaiseTabChanged`, `RaiseMapChunkExplored`, `RaiseMarkerChanged`, `RaiseLogbookChanged` | `SystemDispatcher.LateUpdate()` | PDA open/tab/map/marker/logbook lane. |
| `SceneBootstrap` | `NativeQueue<SceneBootstrapEventPayload>` | `ISceneBootstrapEventListener` | private `RaiseGameReadyEvent`, private `RaiseBootstrapFailedEvent` | `SystemDispatcher.LateUpdate()` via `FlushPendingEvents()` | Queue producer is internal to bootstrap orchestration. |
| `Atlas6Events` | `NativeQueue<Atlas6EventPayload>` | `IAtlas6EventListener` | `RaisePlayerStatusChanged`, `RaiseDirectiveConflict`, `RaiseBarterAccepted`, `RaiseScarcityDirective` | `SystemDispatcher.LateUpdate()` | Declared inside `Atlas6DirectiveSystem.cs`; separate lane from `AtlasSignalEvents`. |
| `ModCommandDispatcher` | `NativeQueue<ModCommand>`, `NativeQueue<ModAupCommand>`, `NativeQueue<ModRenderInstanceCommand>` | `IModCommandKernel`, `IDispatcherRaycastReceiver`, `HectonEventBus` unmanaged result payloads | `Request`, `RequestAup`, `RequestRenderInstance` | `SystemDispatcher.LateUpdate()` before first-party event flushes | Commands are throttled to 128/mod/tick and drained after dispatcher raycasts complete. |

## Spatial Memory Lane

`WorldSpatialHashGrid.LateFrameMaintenance(Time.frameCount)` is not an event bus lane. It is an end-of-frame spatial maintenance lane:

- prunes expired transient spatial records
- rebuilds the 8x8x8 acoustic density map on cadence
- compacts oversized native hash buckets after the queue buses flush
- keeps stale handle validation in `HectonSpatialHash.IsCurrentHandle(...)`

Transient spatial records live in `NativeParallelMultiHashMap<uint, TransientEventRecord>` and are filtered by timestamp before consumers see them.

## Managed Bus Kept Separate

`Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs`

- publish surface: unmanaged `Publish<TPayload>(in TPayload payload)` and byte-span bridge APIs
- delivery model: immediate mod callback dispatch with watchdog and recursion depth guard
- internal shape: managed subscriber tables; payload contract is `unmanaged`
- queue lane: result queues are owned by `ModCommandDispatcher`, then published through this bridge
- ownership role: mod/API event surface, not a first-party gameplay `NativeQueue` bus

## Dependency Graph

`runtime producers`
-> `lane-specific Raise* / Push* methods`
-> `static NativeQueue payload buffer`
-> `SystemDispatcher.LateUpdate()`
-> `lane FlushPending()`
-> `listener bucket dispatch`

Side branch:

`mod producers`
-> `HectonAPI.Commands.RequestAup(...)` / `RequestRenderInstance(...)`
-> `ModCommandDispatcher` NativeQueue
-> late-frame security gate / AUP rebase / conflict arbitration
-> engine kernel or proxied dispatcher raycast lane
-> unmanaged result payload
-> `HectonEventBus.Publish(in payload)`

Save side branch:

`mod producers`
-> `HectonAPI.Persistence.SetModString(...)`
-> `ModSaveStateStore`
-> `SaveManager.ExecuteVerifiedSavePipeline(...)`
-> `SaveBinaryStorage.TryCommitModPayloadSubSector(...)`
-> indexed sector directory entry with `0x4D50` top-bit prefix

## Mod-MMF Paging Path

Source files:

- `Assets/_Project/Scripts/ModdingAPI/ModRuntimeState.cs`
- `Assets/_Project/Scripts/SaveManager.cs`
- `Assets/_Project/Scripts/SaveBinaryStorage.cs`
- `Docs/ARCHITECTURE/SAVE_PAGING_PROTOCOL.md`

Binary layout per isolated mod payload sector:

- sector directory key: `ComputeModPayloadSectorHash(modHash, pagedSectorHash)` with reserved top-bit prefix `0x4D50`
- raw protected block size: `16,384` bytes
- header: 32 bytes
- payload budget: `16,352` bytes
- header fields: `Magic:uint`, `Version:ushort`, `HeaderSize:ushort`, `ModHash:uint`, `PayloadLength:ushort`, `Flags:ushort`, `PagedSectorHash:long`, `PayloadChecksum:uint`, `Reserved:uint`
- compression: existing protected LZ4 indexed-sector writer
- corruption behavior: invalid header/checksum skips that mod sub-sector; base save metadata and first-party world sectors remain authoritative

Spatial mod record path:

`ModWorldPersistenceManager`
-> `ModWorldSpawnRecord` stores `SpawnHash:uint`, AUP `GridX/GridY/GridZ:int64`, AUP `LocalX/LocalY/LocalZ:float`, and legacy runtime `Position`
-> restore converts AUP through current floating-origin offset before pooled proxy spawn

## Architectural Findings

- There is no single queue-backed monolithic bus.
- The project uses multiple lane-specific buses with explicit listener contracts.
- `HectonEventBus`, `ModCommandDispatcher`, and the first-party static queue buses should not be merged conceptually in docs.
- `Atlas6Events` being nested inside `Atlas6DirectiveSystem.cs` increases discoverability risk; the lane exists, but the ownership surface is hidden in a gameplay file rather than a dedicated event file.

## Regression Model

- CPU: none added by this document.
- GC: none added by this document.
- Memory: no runtime change.
- Cadence: documentation drift risk is high if new queue lanes are added without updating this matrix and `PROJECT_ATLAS.md`.
- Correctness: low risk on queue ownership claims because every lane listed here was verified from source files and dispatcher flush order.

## Hot Path Impact

- none from the documentation change itself
- the actual runtime hot-path sensitivity remains in `SystemDispatcher.LateUpdate()` because every queue flush is serialized there

## Failure Modes

- new event lanes can be added outside this document and make it stale
- listener implementations were not exhaustively mapped here; this matrix covers queue ownership and dispatch lanes, not every subscriber class
- `HectonEventBus` may be incorrectly treated as queue-backed by future agents unless this scope note is preserved

## Why Kept

- the file separates verified queue lanes from managed mod bus traffic
- the dispatch order is anchored to `SystemDispatcher.LateUpdate()`
- no inferred listener graph was invented beyond what source ownership supports
