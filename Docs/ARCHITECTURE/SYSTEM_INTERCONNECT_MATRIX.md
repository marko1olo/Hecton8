# System Interconnect Matrix

Date: `2026-05-03`
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
5. `Hecton8.Bootstrap.BootstrapEvents.FlushPending()`
6. `NarrativeEvents.FlushPending()`
7. `Hecton8.Interaction.InteractionEvents.FlushPending()`
8. `Hecton8.Crafting.CraftingEvents.FlushPending()`
9. `ScanEvents.FlushPending()`
10. `SaveEvents.FlushPending()`
11. `QuestEvents.FlushPending()`
12. `FirstHourEvents.FlushPending()`
13. `EndingEvents.FlushPending()`
14. `AudioLogEvents.FlushPending()`
15. `AtmosphereEvents.FlushPending()`
16. `CelestialEvents.FlushPending()`
17. `EclipseGameplayEvents.FlushPending()`
18. `AcousticZoneEvents.FlushPending()`
19. `SpectrumEvents.FlushPending()`
20. `ProceduralAudioEvents.FlushPending()`
21. `HectonSubmarineOsEvents.FlushPending()`
22. `FlashlightEvents.FlushPending()`
23. `LaserCutterEvents.FlushPending()`
24. `PlayerSignalEvents.FlushPending()`
25. `MapMagicBiomeEvents.FlushPending()`
26. `BiomeMatrixEvents.FlushPending()`
27. `DirectorAIEvents.FlushPending()`
28. `WeatherEvents.FlushPending()`
29. `RandomEventEvents.FlushPending()`
30. `PowerGridTelemetryEvents.FlushPending()`
31. `ModuleStatusEvents.FlushPending()`
32. `DepthZoneEvents.FlushPending()`
33. `SoundscapeEvents.FlushPending()`
34. `EmergencyServiceRelayEvents.FlushPending()`
35. `SargassumGlobalDragManager.FlushPendingEvents()`
36. `Hecton8.AtlasSignal.AtlasSignalEvents.FlushPending()`
37. `InventoryEvents.FlushPending()`
38. `PlayerExpressionEvents.FlushPending()`
39. `Hecton8.UI.BaseIntegrityEvents.FlushPending()`
40. `Hecton8.UI.NotificationEvents.FlushPending()`
41. `Hecton8.UI.PDAIntrusionEvents.FlushPending()`
42. `Hecton8.UI.PDAEvents.FlushPending(MaxPdaEventsPerFrame)`
43. `Hecton8.Bootstrap.SceneBootstrap.FlushPendingEvents()`
44. `ObjectPoolDiagnostics.FlushPending()`
45. `PerformanceEvents.FlushPending()`
46. `Hecton8.AtlasSignal.Atlas6Events.FlushPending()`
47. `GlobalRegistry.FlushPendingServiceReboundEvents()`
48. `GlobalTelemetryBus.LateFrameUpdate(Time.unscaledTime)`
49. `WorldSpatialHashGrid.LateFrameMaintenance(Time.frameCount)`

## Queue-Backed Lanes

| Lane Owner | Backing Queue | Listener Contract | Raise Surface | Flush Owner | Notes |
| --- | --- | --- | --- | --- | --- |
| `BootstrapEvents` | front/back `NativeQueue<BootstrapEventPayload>` | `IBootstrapEventListener` | `NotifyBootstrapComplete` | `SystemDispatcher.LateUpdate()` | Bootstrap-complete notification lane. Listener reenqueue writes to the next-frame queue; payload is unmanaged and capped at four pending events. |
| `NarrativeEvents` | front/back `NativeQueue<NarrativeEventPayload>` | `INarrativeEventListener` | `RaiseNarrativePOIRegistered`, `RaiseNarrativePOIDisposed`, `RaiseDiscoveryMade`, `RaiseDepthTierReached` | `SystemDispatcher.LateUpdate()` | Queue listener reenqueue writes to the next-frame queue. Immediate `INarrativePointOfInterestListener` POI buckets stay outside the queue lane. |
| `PhysicsEvents` | `GlobalPhysicsStateManager` owned `NativeQueue<PhysicsImpactEventData>` | `IPhysicsImpactEventListener` | internal `RaiseImpact` after native impact flush | `GlobalPhysicsStateManager.LateFrameTick()` during `ILateFrameTickable` pass | Fixed-step impact payloads are queued first, then listener-bucket dispatched after physics. |
| `InteractionEvents` | front/back `NativeQueue<InteractionEventPayload>` | `IInteractionEventListener` | `RaiseItemCollected`, `RaiseInteractionStarted`, `RaiseHoverChanged` | `SystemDispatcher.LateUpdate()` | First-party interaction lane. Listener reenqueue writes to the next-frame queue; same-frame generation split is source-present for this lane. |
| `CraftingEvents` | front/back `NativeQueue<CraftingEventPayload>` | `ICraftingEventListener` | `RaiseCraftStarted`, `RaiseCraftCompleted`, `RaiseCraftCancelled` | `SystemDispatcher.LateUpdate()` | Crafting lane. Listener reenqueue writes to the next-frame queue; sidecar references are released after listener dispatch or no-listener drain. |
| `ScanEvents` | front/back `NativeQueue<ScanEventPayload>` | `IScanEventListener` | `RaiseScanTriggered`, `RaiseNodeFound`, `RaiseEntryDiscovered` | `SystemDispatcher.LateUpdate()` | Scanner lane for node discovery and radius scan traffic. Listener reenqueue writes to the next-frame queue; metadata hash cache is retained outside the native payload. |
| `SaveEvents` | front/back `NativeQueue<SaveEventPayload>` | `ISaveEventListener` | `RaiseSaveStarted`, `RaiseSaveCompleted`, `RaiseSaveFailed`, `RaiseLoadStarted`, `RaiseLoadCompleted`, `RaiseLoadFailed`, `RaiseEmergencyBackupRestoreRequested` | `SystemDispatcher.LateUpdate()` | Save/load notification lane. Listener reenqueue writes to the next-frame queue; uses `FixedString` payload fields for slot and failure text. |
| `QuestEvents` | front/back `NativeQueue<QuestEventPayload>` | `IQuestEventListener` | `RaiseActivated`, `RaiseCompleted`, `RaiseFailed`, `RaiseRevertRequested` | `SystemDispatcher.LateUpdate()` | Listener reenqueue writes to the next-frame queue. `FlushPending()` still forces `QuestGraphEvaluator.FlushPendingSignals()` before quest event drain. |
| `FirstHourEvents` | front/back `NativeQueue<FirstHourEventPayload>` | `IFirstHourEventListener` | `RaiseMilestone` | `SystemDispatcher.LateUpdate()` | First-hour milestone lane. Listener reenqueue writes to the next-frame queue; replaces `FirstHourEvents.OnMilestoneReached` delegate. |
| `EndingEvents` | front/back `NativeQueue<EndingEventPayload>` | `IEndingEventListener` | `RaiseConditionMet`, `RaiseChosen`, `RaiseSequenceComplete` | `SystemDispatcher.LateUpdate()` | Atlas-6 ending terminal state lane. Listener reenqueue writes to the next-frame queue; replaces `EndingEvents.On*` delegates. |
| `AudioLogEvents` | `NativeQueue<AudioLogEventPayload>` | `IAudioLogEventListener` | `RaiseLogDiscovered`, `RaisePlaybackStarted`, `RaisePlaybackStopped`, `RaisePlaybackCompleted` | `SystemDispatcher.LateUpdate()` | Audio-log playback and subtitle timing lane; replaces direct `SubtitleEventBus.OnPlaybackEvent` delegate delivery. |
| `AtmosphereEvents` | front/back `NativeQueue<EnvironmentState>` | `IAtmosphereStateEventListener` | `RaiseStateChanged` | `SystemDispatcher.LateUpdate()` | Atmosphere state lane consumed by acoustic-zone context. Listener reenqueue writes to the next-frame queue; replaces `HectonAtmosphereManager.OnStateChanged`. |
| `CelestialEvents` | front/back `NativeQueue<CelestialEventPayload>` | `ICelestialEventListener` | `RaiseEclipseStarted`, `RaiseEclipseEnded`, `RaiseSunAngleChanged`, `RaisePlanetPhaseChanged` | `SystemDispatcher.LateUpdate()` | Celestial eclipse/sun/planet lane for eclipse gameplay and quest graph consumers. Listener reenqueue writes to the next-frame queue; high-frequency sun and phase payloads are coalesced. |
| `EclipseGameplayEvents` | front/back `NativeQueue<EclipseGameplayEventPayload>` | `IEclipseGameplayEventListener` | `RaisePhaseChanged`, `RaiseNightPredatorsRising`, `RaiseTemperatureDelta` | `SystemDispatcher.LateUpdate()` | Eclipse gameplay consequence lane for biolum and future predator/temperature consumers. Listener reenqueue writes to the next-frame queue; replaces `EclipseGameplayEvents.On*` delegates. |
| `AcousticZoneEvents` | front/back `NativeQueue<AcousticZoneChangedEvent>` | `IAcousticZoneEventListener` | `Raise` | `SystemDispatcher.LateUpdate()` | Acoustic zone edge changes for music/acoustic-context consumers. Listener reenqueue writes to the next-frame queue; replaces `AcousticZoneController.OnAcousticZoneChanged`. |
| `SpectrumEvents` | `NativeQueue<SpectrumMode>`, `NativeQueue<float>`, `NativeQueue<SpatialSonarSnapshot>`, `NativeQueue<AcousticEchoEvent>` | `ISpectrumModeEventListener`, `ISonarPulseEventListener`, `ISonarPingEventListener`, `ISonarSnapshotEventListener`, `IAcousticEchoEventListener` | `RaiseModeChanged`, `RaiseSonarPulse`, `RaiseSonarPingSent`, `RaiseSonarSnapshotUpdated`, `RaiseAcousticEchoReturned` | `SystemDispatcher.LateUpdate()` | Active sonar, sonar snapshot, echo-return, biolum, AI pressure, and HUD compass lane. |
| `ProceduralAudioEvents` | `NativeQueue<AudioPingTriggerInfo>`, `NativeQueue<StructuralStressAudioInfo>` | `IProceduralAudioEventListener` | `RaiseAudioPingTriggered`, `RaiseStructuralStressTriggered` | `SystemDispatcher.LateUpdate()` | Sample-accurate procedural audio and habitat stress lane. |
| `LaserCutterEvents` | `NativeQueue<LaserCutterEventPayload>` | `ILaserCutterEventListener` | `RaiseHeatChanged`, `RaiseBeamStateChanged` | `SystemDispatcher.LateUpdate()` | Laser cutter heat and beam-state lane for critical audio and abyssal cable cutting; live `Transform` resolution is sidecar-keyed by cutter instance id, not stored in the native payload. |
| `PlayerSignalEvents` | `NativeQueue<TraumaHudSignal>`, `NativeQueue<InteractionSignal>`, `NativeQueue<ToolDepletedSignal>` | `IPlayerSignalEventListener` | `RaiseTraumaHudSignal`, `RaiseInteractionSignal`, `RaiseToolDepletedSignal` | `SystemDispatcher.LateUpdate()` | HUD/stress/tool signal lane; replaces static `Action` subscriptions. |
| `MapMagicBiomeEvents` | front/back `NativeQueue<int>` | `IMapMagicBiomeEventListener` | `RaiseBiomeChanged` | `SystemDispatcher.LateUpdate()` | First-party MapMagic bridge lane for terrain biome index changes. Listener reenqueue writes to the next-frame queue; replaces `MapMagicBridge.OnBiomeChanged` without modifying MapMagic assets. |
| `BiomeMatrixEvents` | front/back `NativeQueue<BiomeMatrixEventPayload>` | `IBiomeMatrixEventListener` | `RaiseMatrixBiomeChanged`, `RaiseDepthTierChanged` | `SystemDispatcher.LateUpdate()` | Biome/depth matrix lane for atmosphere, visuals, music, PDA spectrum, celestial texture residency, and scatter sampling invalidation. Listener reenqueue writes to the next-frame queue; replaces `BiomeMatrixDirector.On*` delegates. |
| `DirectorAIEvents` | `NativeQueue<DirectorAIEventPayload>` | `IDirectorAIEventListener` | `RaiseSpawnHordeRequested`, `RaiseEquipmentGlitchRequested`, `RaiseRareDiscoveryRequested`, `RaiseWeatherShiftRequested`, `RaiseMissionTriggerRequested`, `RaisePredatorPressureChanged` | `SystemDispatcher.LateUpdate()` | Encounter-director output lane for music, PDA intrusion, narrative discovery, and mission bridge consumers; replaces `HectonDirectorAI.OnRequest*` delegates. |
| `WeatherEvents` | front/back `NativeQueue<WeatherEventPayload>` | `IWeatherEventListener` | `RaiseSnapshotUpdated` | `SystemDispatcher.LateUpdate()` | Weather/current snapshot lane. Listener reenqueue writes to the next-frame queue; payload remains unmanaged. |
| `RandomEventEvents` | `NativeQueue<RandomEventStartedPayload>`, `NativeQueue<RandomEventType>`, `NativeQueue<SeismicShockwaveEvent>` | `IRandomEventListener` | `RaiseStarted`, `RaiseEnded`, `RaiseSeismicShockwave` | `SystemDispatcher.LateUpdate()` | Random-event world state and seismic shockwave lane. |
| `PowerGridTelemetryEvents` | front/back `NativeQueue<PowerGridTelemetrySnapshot>` | `IPowerGridTelemetryListener` | `Raise` | `SystemDispatcher.LateUpdate()` | Aggregate logistics power telemetry lane. Listener reenqueue writes to the next-frame queue; replaces direct telemetry delegate dispatch. |
| `AtlasSignalEvents` | `NativeQueue<AtlasSignalEventPayload>` | `IAtlasSignalEventListener` | `RaisePulse`, `RaiseDetected`, `RaiseStrengthChanged`, `RaiseDecoded` | `SystemDispatcher.LateUpdate()` | Holds decoded Atlas signal message IDs through a local lookup table. |
| `InventoryEvents` | front/back `NativeQueue<InventoryEventPayload>` | `IInventoryEventListener` | `NotifyInventoryFull`, `NotifyInventoryChanged`, `NotifyEncumbranceChanged` | `SystemDispatcher.LateUpdate()` | Inventory-full, contents-changed, and encumbrance lane. Listener reenqueue writes to the next-frame queue; managed `ItemData` and `PlayerInventory` references are sidecar-resolved during dispatch and duplicate payloads are suppressed per frame by `(sourceId << 32) | eventType`. |
| `PlayerExpressionEvents` | `NativeQueue<PlayerExpressionEventPayload>` | `IPlayerExpressionEventListener` | `RaiseProfileChanged` | `SystemDispatcher.LateUpdate()` | Player-expression profile lane for PDA loadout refresh; managed `PlayerExpressionProfile` references are sidecar-resolved during dispatch. |
| `BaseIntegrityEvents` | `NativeQueue<BaseIntegrityEventPayload>` | `IBaseIntegrityEventListener` | `RaiseIntegrityWarning`, `RaiseBreached`, `RaiseEmergency`, `RaiseAirQualityWarning` | `SystemDispatcher.LateUpdate()` | Base-module structural and air-quality warning lane for suit and PDA advisory consumers; replaces four `BaseIntegrityEvents.On*` delegates. |
| `NotificationEvents` | front/back `NativeQueue<NotificationEventPayload>` | `INotificationEventListener` | `PushInfo`, `PushWarning`, `PushCritical` | `SystemDispatcher.LateUpdate()` | UI notification lane. Listener reenqueue writes to the next-frame queue; message text remains in the existing hash lookup. |
| `PDAIntrusionEvents` | `NativeQueue<PDAIntrusionEventPayload>` | `IPDAIntrusionEventListener` | `RaiseRebootCompleted` | `SystemDispatcher.LateUpdate()` | PDA intrusion reboot lane; replaces `PDAIntrusionManager.OnRebootCompleted` delegate and keeps Hecton-OS boot recovery behind the dispatcher budget. |
| `PDAEvents` | `NativeQueue<PDAEventPayload>` | `IPDAEventListener` | `RaiseOpened`, `RaiseClosed`, `RaiseTabChanged`, `RaiseMapChunkExplored`, `RaiseMarkerChanged`, `RaiseLogbookChanged` | `SystemDispatcher.LateUpdate()` | PDA open/tab/map/marker/logbook lane. |
| `SceneBootstrap` | `NativeQueue<SceneBootstrapEventPayload>` | `ISceneBootstrapEventListener` | private `RaiseGameReadyEvent`, private `RaiseBootstrapFailedEvent` | `SystemDispatcher.LateUpdate()` via `FlushPendingEvents()` | Queue producer is internal to bootstrap orchestration. |
| `PerformanceEvents` | `NativeQueue<PerformanceEventPayload>` | `IPerformanceEventListener` | `RaiseFrameTimeSpike`, `RaiseGCAllocExceeded`, `RaiseJobQueueBacklog` | `SystemDispatcher.LateUpdate()` | Performance threshold alert lane; replaces legacy `PerformanceMonitor` delegate alerts. |
| `Atlas6Events` | `NativeQueue<Atlas6EventPayload>` | `IAtlas6EventListener` | `RaisePlayerStatusChanged`, `RaiseDirectiveConflict`, `RaiseBarterAccepted`, `RaiseScarcityDirective` | `SystemDispatcher.LateUpdate()` | Declared inside `Atlas6DirectiveSystem.cs`; separate lane from `AtlasSignalEvents`. |
| `ModCommandDispatcher` | `NativeQueue<ModCommand>`, `NativeQueue<ModAupCommand>`, `NativeQueue<ModRenderInstanceCommand>` | `IModCommandKernel`, `IDispatcherRaycastReceiver`, `HectonEventBus` unmanaged result payloads | `Request`, `RequestAup`, `RequestRenderInstance` | `SystemDispatcher.LateUpdate()` before first-party event flushes | Commands are throttled to 128/mod/tick and drained after dispatcher raycasts complete. |

World-domain lanes use queue payloads with interface listener buckets, not delegate buckets. Current listener contracts include `IDepthZoneEventListener`, `ISoundscapeEventListener`, `IEmergencyServiceRelayEventListener`, and `ISargassumGlobalDragEventListener`.

## Immediate Pre-Mutation Routers

`ToolEffectEvents` is intentionally not a LateUpdate queue. It is an immediate fixed-bucket router for pre-repair weld modifiers:

- listener contract: `IToolEffectListener`
- current consumer: `HabitatIntegrityManager`
- producer order: `RepairTool` raises weld effect, then calls `BaseModule.Repair(amount)`
- reason: deferring the signal would restore the repair cap after the repair mutation and change gameplay math
- static `Action` delegates: none

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
