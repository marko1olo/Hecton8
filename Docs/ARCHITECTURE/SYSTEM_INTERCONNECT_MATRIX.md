# System Interconnect Matrix

Date: 2026-05-07
Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

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
- It is not runtime profiler proof. Code-validator status requires a linked artifact path, command, timestamp, and edited-script list; until then this matrix is `STATIC_DOC / STATIC_SOURCE` only.
- `Docs/Reports/DOOMSDAY_FLAW_REPORT.md` remains the historic risk authority for event cascade/depth concerns.

## Typed Signal-Lane Orientation

The old five-bucket bus summary is legacy shorthand only. Current ownership uses the 9-echelon / 85-domain map plus typed `SignalBus<T>` and `NativeQueue` lanes; Core/Env/Player/Base/AI are local reading buckets, not complete architecture coverage or exclusive cross-domain authority.

| Legacy bucket | Runtime scope | Representative lanes |
|---|---|---|
| Core | bootstrap, scene, registry, save/load, localization, telemetry, performance, object-pool diagnostics, mod registry | `BootstrapEvents`, `SceneBootstrap`, `GlobalRegistry`, `SaveEvents`, `LocalizationEvents`, `GlobalTelemetryBus`, `PerformanceEvents`, `ObjectPoolDiagnostics`, `ModRegistryEvents` |
| Env | weather, atmosphere, biome, celestial, acoustic, physics, fluid, pressure, depth, soundscape, random/seismic world pressure | `WeatherEvents`, `AtmosphereEvents`, `MapMagicBiomeEvents`, `BiomeMatrixEvents`, `CelestialEvents`, `EclipseGameplayEvents`, `AcousticZoneEvents`, `PhysicsEventBus`, `PhysicsEvents`, `FluidFeedbackEvents`, `HighPressureEvents`, `FatalPressureImplosionEvents`, `DepthZoneEvents`, `SoundscapeEvents`, `RandomEventEvents` |
| Player | input-facing interaction, crafting, scan, tools, PDA, inventory, player signal/expression, notifications, Atlas signal | `InteractionEvents`, `CraftingEvents`, `ScanEvents`, `FlashlightEvents`, `LaserCutterEvents`, `PDAEvents`, `PDAIntrusionEvents`, `InventoryEvents`, `PlayerSignalEvents`, `PlayerExpressionEvents`, `NotificationEvents`, `AtlasSignalEvents` |
| Base | base modules, airlocks, base integrity, submarine OS, power grid, emergency service relay, drone fleet telemetry | `ModuleStatusEvents`, `BaseAirlockEvents`, `BaseIntegrityEvents`, `HectonSubmarineOsEvents`, `PowerGridTelemetryEvents`, `EmergencyServiceRelayEvents`, `HectonDroneFleetEvents` |
| AI | encounter director, quest/progression, narrative/audio logs, first-hour, endings, Atlas-6 directives, ecosystem/fauna pressure | `DirectorAIEvents`, `QuestEvents`, `NarrativeEvents`, `AudioLogEvents`, `FirstHourEvents`, `EndingEvents`, `Atlas6Events`, `SargassumGlobalDragManager` |

## Static Source LateUpdate Flush Order

Source: `Assets/_Project/Scripts/Core/SystemDispatcher.cs`

1. `CompleteDispatcherRaycasts()`
2. `ILateFrameTickable.LateFrameTick()` lane pass
3. `ThreadSafeCommandQueue.DrainToMainThread()` if budget remains
4. `ThreadSafeCommandQueue.FlushStorageReservationCommitResolvedEvents()`
5. `Hecton8.Modding.ModCommandDispatcher.DrainLateFrame()`
6. `Hecton8.Modding.ModRegistryEvents.FlushPending()`
7. `Hecton8.Bootstrap.BootstrapEvents.FlushPending()`
8. `Hecton.Localization.LocalizationEvents.FlushPending()`
9. `NarrativeEvents.FlushPending()`
10. `Hecton8.Interaction.InteractionEvents.FlushPending()`
11. `Hecton8.Crafting.CraftingEvents.FlushPending()`
12. `ScanEvents.FlushPending()`
13. `SaveEvents.FlushPending()`
14. `QuestEvents.FlushPending()`
15. `FirstHourEvents.FlushPending()`
16. `EndingEvents.FlushPending()`
17. `AudioLogEvents.FlushPending()`
18. `AtmosphereEvents.FlushPending()`
19. `HighPressureEvents.FlushPending()`
20. `FatalPressureImplosionEvents.FlushPending()`
21. `CelestialEvents.FlushPending()`
22. `EclipseGameplayEvents.FlushPending()`
23. `AcousticZoneEvents.FlushPending()`
24. `PhysicsEventBus.FlushPending()`
25. `FluidFeedbackEvents.FlushPending()`
26. `RepairDroneTorchAcousticEvents.FlushPending()`
27. `ElectrolysisAcousticEvents.FlushPending()`
28. `AudioCaptionEvents.FlushPending()`
29. `SpectrumEvents.FlushPending()`
30. `ProceduralAudioEvents.FlushPending()`
31. `HectonSubmarineOsEvents.FlushPending()`
32. `FlashlightEvents.FlushPending()`
33. `LaserCutterEvents.FlushPending()`
34. `PlayerSignalEvents.FlushPending()`
35. `MapMagicBiomeEvents.FlushPending()`
36. `BiomeMatrixEvents.FlushPending()`
37. `DirectorAIEvents.FlushPending()`
38. `HectonDroneFleetEvents.FlushPending()`
39. `WeatherEvents.FlushPending()`
40. `RandomEventEvents.FlushPending()`
41. `PowerGridTelemetryEvents.FlushPending()`
42. `ModuleStatusEvents.FlushPending()`
43. `BaseAirlockEvents.FlushPending()`
44. `DepthZoneEvents.FlushPending()`
45. `SoundscapeEvents.FlushPending()`
46. `EmergencyServiceRelayEvents.FlushPending()`
47. `SargassumGlobalDragManager.FlushPendingEvents()`
48. `Hecton8.AtlasSignal.AtlasSignalEvents.FlushPending()`
49. `InventoryEvents.FlushPending()`
50. `PlayerExpressionEvents.FlushPending()`
51. `Hecton8.UI.BaseIntegrityEvents.FlushPending()`
52. `Hecton8.UI.NotificationEvents.FlushPending()`
53. `Hecton8.UI.PDAIntrusionEvents.FlushPending()`
54. `Hecton8.UI.PDAEvents.FlushPending(MaxPdaEventsPerFrame)`
55. `Hecton8.Bootstrap.SceneBootstrap.FlushPendingEvents()`
56. `ObjectPoolDiagnostics.FlushPending()`
57. `PerformanceEvents.FlushPending()`
58. `Hecton8.AtlasSignal.Atlas6Events.FlushPending()`
59. `GlobalRegistry.FlushPendingServiceReboundEvents()`
60. `GlobalTelemetryBus.LateFrameUpdate(Time.unscaledTime)`
61. `WorldSpatialHashGrid.LateFrameMaintenance(Time.frameCount)`

## Queue-Backed Lanes

| Lane Owner | Backing Queue | Listener Contract | Raise Surface | Flush Owner | Notes |
| --- | --- | --- | --- | --- | --- |
| `ModRegistryEvents` | front/back `NativeQueue<ModRegistryEventPayload>` | `IModRegistryEventListener` | `NotifyRuntimeRegistryChanged`, `NotifySettingsRegistryChanged`, `NotifyRecipeRegistryChanged`, `NotifyBuildableRegistryChanged` | `SystemDispatcher.LateUpdate()` | Coalesced mod registry invalidation lane. Listener reenqueue writes to the next-frame queue; per-registry queued flags stay active until that payload is drained. |
| `BootstrapEvents` | front/back `NativeQueue<BootstrapEventPayload>` | `IBootstrapEventListener` | `NotifyBootstrapComplete` | `SystemDispatcher.LateUpdate()` | Bootstrap-complete notification lane. Listener reenqueue writes to the next-frame queue; payload is unmanaged and capped at four pending events. |
| `LocalizationEvents` | front/back `NativeQueue<LocalizationEventPayload>` | `ILocalizationLanguageChangedListener`, `ILocalizationCorruptionVisualStateListener` | `PublishLanguageChanged`, `PublishCorruptionVisualStateChanged` | `SystemDispatcher.LateUpdate()` | Localization language/corruption visual lane. Listener reenqueue writes to the next-frame queue; no-listener drain preserves the existing no-budget clear behavior. |
| `NarrativeEvents` | front/back `NativeQueue<NarrativeEventPayload>` | `INarrativeEventListener` | `RaiseNarrativePOIRegistered`, `RaiseNarrativePOIDisposed`, `RaiseDiscoveryMade`, `RaiseDepthTierReached` | `SystemDispatcher.LateUpdate()` | Queue listener reenqueue writes to the next-frame queue. Immediate `INarrativePointOfInterestListener` POI buckets stay outside the queue lane. |
| `PhysicsEvents` | `GlobalPhysicsStateManager` owned `NativeQueue<PhysicsImpactEventData>` | `IPhysicsImpactEventListener` | internal `RaiseImpact` after native impact flush | `GlobalPhysicsStateManager.LateFrameTick()` during `ILateFrameTickable` pass | Fixed-step impact payloads are queued first, then listener-bucket dispatched after physics. |
| `PhysicsEventBus` | front/back `NativeQueue<PhysicsEventPayload>` | `IPressureImpulseEventListener`, `IElectromagneticPulseEventListener`, `IAcousticPingEventListener` | `NotifyPressureImpulse`, `NotifyElectromagneticPulse`, `NotifyAcousticPing` | `SystemDispatcher.LateUpdate()` | Physics-domain pressure/EMP/acoustic signal lane. Listener reenqueue writes to the next-frame queue; overflow telemetry remains frame-throttled. |
| `InteractionEvents` | front/back `NativeQueue<InteractionEventPayload>` | `IInteractionEventListener` | `RaiseItemCollected`, `RaiseInteractionStarted`, `RaiseHoverChanged` | `SystemDispatcher.LateUpdate()` | First-party interaction lane. Listener reenqueue writes to the next-frame queue; same-frame generation split is source-present for this lane. |
| `CraftingEvents` | front/back `NativeQueue<CraftingEventPayload>` | `ICraftingEventListener` | `RaiseCraftStarted`, `RaiseCraftCompleted`, `RaiseCraftCancelled` | `SystemDispatcher.LateUpdate()` | Crafting lane. Listener reenqueue writes to the next-frame queue; sidecar references are released after listener dispatch or no-listener drain. |
| `ScanEvents` | front/back `NativeQueue<ScanEventPayload>` | `IScanEventListener` | `RaiseScanTriggered`, `RaiseNodeFound`, `RaiseEntryDiscovered` | `SystemDispatcher.LateUpdate()` | Scanner lane for node discovery and radius scan traffic. Listener reenqueue writes to the next-frame queue; metadata hash cache is retained outside the native payload. |
| `SaveEvents` | front/back `NativeQueue<SaveEventPayload>` | `ISaveEventListener` | `RaiseSaveStarted`, `RaiseSaveCompleted`, `RaiseSaveFailed`, `RaiseLoadStarted`, `RaiseLoadCompleted`, `RaiseLoadFailed`, `RaiseEmergencyBackupRestoreRequested` | `SystemDispatcher.LateUpdate()` | Save/load notification lane. Listener reenqueue writes to the next-frame queue; uses `FixedString` payload fields for slot and failure text. |
| `QuestEvents` | front/back `NativeQueue<QuestEventPayload>` | `IQuestEventListener` | `RaiseActivated`, `RaiseCompleted`, `RaiseFailed`, `RaiseRevertRequested` | `SystemDispatcher.LateUpdate()` | Listener reenqueue writes to the next-frame queue. `FlushPending()` still forces `QuestGraphEvaluator.FlushPendingSignals()` before quest event drain. |
| `FirstHourEvents` | front/back `NativeQueue<FirstHourEventPayload>` | `IFirstHourEventListener` | `RaiseMilestone` | `SystemDispatcher.LateUpdate()` | First-hour milestone lane. Listener reenqueue writes to the next-frame queue; replaces `FirstHourEvents.OnMilestoneReached` delegate. |
| `EndingEvents` | front/back `NativeQueue<EndingEventPayload>` | `IEndingEventListener` | `RaiseConditionMet`, `RaiseChosen`, `RaiseSequenceComplete` | `SystemDispatcher.LateUpdate()` | Atlas-6 ending terminal state lane. Listener reenqueue writes to the next-frame queue; replaces `EndingEvents.On*` delegates. |
| `AudioLogEvents` | front/back `NativeQueue<AudioLogEventPayload>` | `IAudioLogEventListener` | `RaiseLogDiscovered`, `RaisePlaybackStarted`, `RaisePlaybackStopped`, `RaisePlaybackCompleted` | `SystemDispatcher.LateUpdate()` | Audio-log playback and subtitle timing lane. Listener reenqueue writes to the next-frame queue; managed `AudioLogData` references are sidecar-resolved and released during dispatch/no-listener drain. |
| `AtmosphereEvents` | front/back `NativeQueue<EnvironmentState>` | `IAtmosphereStateEventListener` | `RaiseStateChanged` | `SystemDispatcher.LateUpdate()` | Atmosphere state lane consumed by acoustic-zone context. Listener reenqueue writes to the next-frame queue; replaces `HectonAtmosphereManager.OnStateChanged`. |
| `CelestialEvents` | front/back `NativeQueue<CelestialEventPayload>` | `ICelestialEventListener` | `RaiseEclipseStarted`, `RaiseEclipseEnded`, `RaiseSunAngleChanged`, `RaisePlanetPhaseChanged` | `SystemDispatcher.LateUpdate()` | Celestial eclipse/sun/planet lane for eclipse gameplay and quest graph consumers. Listener reenqueue writes to the next-frame queue; high-frequency sun and phase payloads are coalesced. |
| `EclipseGameplayEvents` | front/back `NativeQueue<EclipseGameplayEventPayload>` | `IEclipseGameplayEventListener` | `RaisePhaseChanged`, `RaiseNightPredatorsRising`, `RaiseTemperatureDelta` | `SystemDispatcher.LateUpdate()` | Eclipse gameplay consequence lane for biolum and future predator/temperature consumers. Listener reenqueue writes to the next-frame queue; replaces `EclipseGameplayEvents.On*` delegates. |
| `AcousticZoneEvents` | front/back `NativeQueue<AcousticZoneChangedEvent>` | `IAcousticZoneEventListener` | `Raise` | `SystemDispatcher.LateUpdate()` | Acoustic zone edge changes for music/acoustic-context consumers. Listener reenqueue writes to the next-frame queue; replaces `AcousticZoneController.OnAcousticZoneChanged`. |
| `HighPressureEvents` | front/back `NativeQueue<HighPressureEventPayload>` | `IHighPressureEventListener` | `Notify` | `SystemDispatcher.LateUpdate()` | Submarine high-pressure warning lane. Listener reenqueue writes to the next-frame queue; overflow telemetry remains frame-throttled. |
| `FatalPressureImplosionEvents` | front/back `NativeQueue<FatalPressureImplosionEventPayload>` | `IFatalPressureImplosionEventListener` | `Notify` | `SystemDispatcher.LateUpdate()` | Catastrophic pressure implosion lane. Listener reenqueue writes to the next-frame queue; overflow telemetry remains frame-throttled. |
| `FluidFeedbackEvents` | front/back `NativeQueue<SplashEvent>` | `IFluidSplashEventListener` | `PublishSplashQueued` | `SystemDispatcher.LateUpdate()` | Fluid splash presentation lane. Listener reenqueue writes to the next-frame queue; overflow telemetry remains frame-throttled. |
| `RepairDroneTorchAcousticEvents` | front/back `NativeQueue<RepairDroneTorchAcousticPayload>` | `IRepairDroneTorchAcousticListener` | `Notify` | `SystemDispatcher.LateUpdate()` | Repair-drone torch acoustic lane. Listener reenqueue writes to the next-frame queue; managed `AudioClip` sidecar slots are released during dispatch. |
| `ElectrolysisAcousticEvents` | front/back `NativeQueue<ElectrolysisAcousticPayload>` | `IElectrolysisAcousticEventListener` | `Notify` | `SystemDispatcher.LateUpdate()` | Electrolysis acoustic/threat lane. Listener reenqueue writes to the next-frame queue; overflow telemetry remains frame-throttled. |
| `AudioCaptionEvents` | front/back `NativeQueue<AudioCaptionPayload>` | `IAudioCaptionEventListener` | `Raise` | `SystemDispatcher.LateUpdate()` | Spatial audio caption lane. Listener reenqueue writes to the next-frame queue; managed caption text sidecar slots are released during dispatch. |
| `SpectrumEvents` | front/back `NativeQueue<SpectrumMode>`, `NativeQueue<float>`, `NativeQueue<SpatialSonarSnapshot>`, `NativeQueue<AcousticEchoEvent>` | `ISpectrumModeEventListener`, `ISonarPulseEventListener`, `ISonarPingEventListener`, `ISonarSnapshotEventListener`, `IAcousticEchoEventListener` | `RaiseModeChanged`, `RaiseSonarPulse`, `RaiseSonarPingSent`, `RaiseSonarSnapshotUpdated`, `RaiseAcousticEchoReturned` | `SystemDispatcher.LateUpdate()` | Active sonar, sonar snapshot, echo-return, biolum, AI pressure, and HUD compass lane. Listener reenqueue writes to next-frame queues per payload lane. |
| `ProceduralAudioEvents` | front/back `NativeQueue<AudioPingTriggerInfo>`, `NativeQueue<StructuralStressAudioInfo>` | `IProceduralAudioEventListener` | `RaiseAudioPingTriggered`, `RaiseStructuralStressTriggered` | `SystemDispatcher.LateUpdate()` | Sample-accurate procedural audio and habitat stress lane. Listener reenqueue writes to the next-frame queue; no-listener drain preserves budget behavior. |
| `HectonSubmarineOsEvents` | front/back `NativeQueue<SubmarineOsEventPayload>` | `ISubmarineOsEventListener` | `RaiseSnapshotUpdated`, `RaiseLogRequested` | `SystemDispatcher.LateUpdate()` | Submarine OS telemetry/log request lane. Listener reenqueue writes to the next-frame queue; snapshot/log builders remain payload-local and unmanaged. |
| `LaserCutterEvents` | front/back `NativeQueue<LaserCutterEventPayload>` | `ILaserCutterEventListener` | `RaiseHeatChanged`, `RaiseBeamStateChanged` | `SystemDispatcher.LateUpdate()` | Laser cutter heat and beam-state lane. Listener reenqueue writes to the next-frame queue; live `Transform` resolution stays sidecar-keyed by cutter instance id. |
| `FlashlightEvents` | front/back `NativeQueue<FlashlightEventPayload>` | `IFlashlightEventListener` | `RaiseToggled`, `RaiseBatteryDepleted`, `RaiseOverheat`, `RaiseFlickerStart` | `SystemDispatcher.LateUpdate()` | Player flashlight notification lane. Listener reenqueue writes to the next-frame queue; no-listener drain clears both generations. |
| `PlayerSignalEvents` | front/back `NativeQueue<TraumaHudSignal>`, `NativeQueue<InteractionSignal>`, `NativeQueue<ToolDepletedSignal>` | `IPlayerSignalEventListener` | `RaiseTraumaHudSignal`, `RaiseInteractionSignal`, `RaiseToolDepletedSignal` | `SystemDispatcher.LateUpdate()` | HUD/stress/tool signal lane; replaces static `Action` subscriptions. Listener reenqueue writes to next-frame queues; no-listener drain preserves budget behavior. |
| `MapMagicBiomeEvents` | front/back `NativeQueue<int>` | `IMapMagicBiomeEventListener` | `RaiseBiomeChanged` | `SystemDispatcher.LateUpdate()` | First-party MapMagic bridge lane for terrain biome index changes. Listener reenqueue writes to the next-frame queue; replaces `MapMagicBridge.OnBiomeChanged` without modifying MapMagic assets. |
| `BiomeMatrixEvents` | front/back `NativeQueue<BiomeMatrixEventPayload>` | `IBiomeMatrixEventListener` | `RaiseMatrixBiomeChanged`, `RaiseDepthTierChanged` | `SystemDispatcher.LateUpdate()` | Biome/depth matrix lane for atmosphere, visuals, music, PDA spectrum, celestial texture residency, and scatter sampling invalidation. Listener reenqueue writes to the next-frame queue; replaces `BiomeMatrixDirector.On*` delegates. |
| `DirectorAIEvents` | front/back `NativeQueue<DirectorAIEventPayload>` | `IDirectorAIEventListener` | `RaiseSpawnHordeRequested`, `RaiseEquipmentGlitchRequested`, `RaiseRareDiscoveryRequested`, `RaiseWeatherShiftRequested`, `RaiseMissionTriggerRequested`, `RaisePredatorPressureChanged` | `SystemDispatcher.LateUpdate()` | Encounter-director output lane for music, PDA intrusion, narrative discovery, and mission bridge consumers. Listener reenqueue writes to the next-frame queue; replaces `HectonDirectorAI.OnRequest*` delegates. |
| `HectonDroneFleetEvents` | front/back `NativeQueue<HectonDroneFleetSnapshotPayload>` | `IDroneFleetSnapshotEventListener` | `RaiseSnapshotUpdated` | `SystemDispatcher.LateUpdate()` | Drone fleet telemetry lane for submarine OS diagnostics. Listener reenqueue writes to the next-frame queue; overflow telemetry remains frame-throttled. |
| `WeatherEvents` | front/back `NativeQueue<WeatherEventPayload>` | `IWeatherEventListener` | `RaiseSnapshotUpdated` | `SystemDispatcher.LateUpdate()` | Weather/current snapshot lane. Listener reenqueue writes to the next-frame queue; payload remains unmanaged. |
| `RandomEventEvents` | front/back `NativeQueue<RandomEventStartedPayload>`, `NativeQueue<RandomEventType>`, `NativeQueue<SeismicShockwaveEvent>` | `IRandomEventListener` | `RaiseStarted`, `RaiseEnded`, `RaiseSeismicShockwave` | `SystemDispatcher.LateUpdate()` | Random-event world state and seismic shockwave lane. Listener reenqueue writes to next-frame queues; `RaiseSeismicShockwave` keeps the existing `PhysicsEventBus` acoustic side effect before enqueue. |
| `PowerGridTelemetryEvents` | front/back `NativeQueue<PowerGridTelemetrySnapshot>` | `IPowerGridTelemetryListener` | `Raise` | `SystemDispatcher.LateUpdate()` | Aggregate logistics power telemetry lane. Listener reenqueue writes to the next-frame queue; replaces direct telemetry delegate dispatch. |
| `ModuleStatusEvents` | front/back `NativeQueue<ModuleStatusEventPayload>` | `IModuleStatusEventListener` | `NotifyEnter`, `NotifyExit` | `SystemDispatcher.LateUpdate()` | Base module enter/exit status lane. Listener reenqueue writes to the next-frame queue; managed `BaseModule` references are sidecar-resolved and released during dispatch/no-listener drain. |
| `BaseAirlockEvents` | front/back `NativeQueue<BaseAirlockEventPayload>` | `IBaseAirlockEventListener` | `RaiseCycleStarted`, `RaiseCycleCompleted`, `RaiseEnvironmentChanged`, `RaiseEmergencyLockdownChanged`, `RaiseManualOverrideBlockedChanged`, `RaiseManualOverrideCompleted` | `SystemDispatcher.LateUpdate()` | Airlock transition and lockdown lane. `SystemDispatcher.InitializeService()` prewarms queue storage; listener reenqueue writes to the next-frame queue. Managed `BaseAirlock`/interactor references are sidecar-resolved and released during dispatch/no-listener drain. Legacy serialized UnityEvent hooks still exist for scene wiring and are not the runtime system bus. |
| `DepthZoneEvents` | front/back `NativeQueue<DepthZoneEventPayload>` | `IDepthZoneEventListener` | `RaiseZoneEntered`, `RaiseZoneExited` | `SystemDispatcher.LateUpdate()` | Depth-zone enter/exit lane. Listener reenqueue writes to the next-frame queue; profile lookup remains in the existing hash sidecar. |
| `SoundscapeEvents` | front/back `NativeQueue<SoundscapeEventPayload>` | `ISoundscapeEventListener` | `RaiseTierChanged` | `SystemDispatcher.LateUpdate()` | World soundscape tier lane. Listener reenqueue writes to the next-frame queue; producer still suppresses payloads when no listeners are registered. |
| `EmergencyServiceRelayEvents` | front/back `NativeQueue<RelayEventPayload>` | `IEmergencyServiceRelayEventListener` | `RaiseRelayActivated` | `SystemDispatcher.LateUpdate()` | Emergency relay activation lane. Listener reenqueue writes to the next-frame queue; relay lookup remains in the existing entity-id sidecar. |
| `SargassumGlobalDragManager` | front/back `NativeQueue<EntanglementStrainSignal>`, `NativeQueue<MassiveDisplacementSignal>` | `ISargassumGlobalDragEventListener` | `RaiseEntanglementStrain`, `RaiseMassiveDisplacement` | `SystemDispatcher.LateUpdate()` via `FlushPendingEvents()` | Floating sargassum entanglement/displacement lane. Listener reenqueue writes to next-frame queues; producer still suppresses payloads when no listeners are registered. |
| `AtlasSignalEvents` | front/back `NativeQueue<AtlasSignalEventPayload>` | `IAtlasSignalEventListener` | `RaisePulse`, `RaiseDetected`, `RaiseStrengthChanged`, `RaiseDecoded` | `SystemDispatcher.LateUpdate()` | Holds decoded Atlas signal message IDs through a local lookup table. Listener reenqueue writes to the next-frame queue. |
| `InventoryEvents` | front/back `NativeQueue<InventoryEventPayload>` | `IInventoryEventListener` | `NotifyInventoryFull`, `NotifyInventoryChanged`, `NotifyEncumbranceChanged` | `SystemDispatcher.LateUpdate()` | Inventory-full, contents-changed, and encumbrance lane. Listener reenqueue writes to the next-frame queue; managed `ItemData` and `PlayerInventory` references are sidecar-resolved during dispatch and duplicate payloads are suppressed per frame by `(sourceId << 32) | eventType`. |
| `PlayerExpressionEvents` | front/back `NativeQueue<PlayerExpressionEventPayload>` | `IPlayerExpressionEventListener` | `RaiseProfileChanged` | `SystemDispatcher.LateUpdate()` | Player-expression profile lane for PDA loadout refresh. Listener reenqueue writes to the next-frame queue; managed `PlayerExpressionProfile` references are sidecar-resolved and released during dispatch/no-listener drain. |
| `BaseIntegrityEvents` | front/back `NativeQueue<BaseIntegrityEventPayload>` | `IBaseIntegrityEventListener` | `RaiseIntegrityWarning`, `RaiseBreached`, `RaiseEmergency`, `RaiseAirQualityWarning` | `SystemDispatcher.LateUpdate()` | Base-module structural and air-quality warning lane. Listener reenqueue writes to the next-frame queue; replaces four `BaseIntegrityEvents.On*` delegates. |
| `NotificationEvents` | front/back `NativeQueue<NotificationEventPayload>` | `INotificationEventListener` | `PushInfo`, `PushWarning`, `PushCritical` | `SystemDispatcher.LateUpdate()` | UI notification lane. Listener reenqueue writes to the next-frame queue; message text remains in the existing hash lookup. |
| `PDAIntrusionEvents` | front/back `NativeQueue<PDAIntrusionEventPayload>` | `IPDAIntrusionEventListener` | `RaiseRebootCompleted` | `SystemDispatcher.LateUpdate()` | PDA intrusion reboot lane. Listener reenqueue writes to the next-frame queue; replaces `PDAIntrusionManager.OnRebootCompleted` delegate and keeps Hecton-OS boot recovery behind the dispatcher budget. |
| `PDAEvents` | front/back `NativeQueue<PDAEventPayload>` | `IPDAEventListener` | `RaiseOpened`, `RaiseClosed`, `RaiseTabChanged`, `RaiseMapChunkExplored`, `RaiseMarkerChanged`, `RaiseLogbookChanged` | `SystemDispatcher.LateUpdate()` | PDA open/tab/map/marker/logbook lane. Listener reenqueue writes to the next-frame queue; dedup keys remain frame-scoped and no-listener drain still applies simulation side effects. |
| `SceneBootstrap` | front/back `NativeQueue<SceneBootstrapEventPayload>` | `ISceneBootstrapEventListener` | private `RaiseGameReadyEvent`, private `RaiseBootstrapFailedEvent` | `SystemDispatcher.LateUpdate()` via `FlushPendingEvents()` | Scene bootstrap ready/failure lane. Listener reenqueue writes to the next-frame queue; queue producer remains internal to bootstrap orchestration. |
| `ObjectPoolDiagnostics` | front/back `NativeQueue<PoolDiagnosticsEventPayload>` | `IObjectPoolDiagnosticsListener` | `PublishDataBusDepth`, internal pool warnings | `SystemDispatcher.LateUpdate()` | Pool diagnostics/data-bus alert lane. Zero-depth samples are suppressed; no-listener drain drops bounded diagnostic payloads without consuming the shared event budget; listener reenqueue writes to the next-frame queue; pool-name hash lookup remains in the existing cold-path dictionary. |
| `PerformanceEvents` | front/back `NativeQueue<PerformanceEventPayload>` | `IPerformanceEventListener` | `RaiseFrameTimeSpike`, `RaiseGCAllocExceeded`, `RaiseJobQueueBacklog` | `SystemDispatcher.LateUpdate()` | Performance threshold alert lane. Listener reenqueue writes to the next-frame queue; no-listener drain preserves existing drop behavior and replaces legacy `PerformanceMonitor` delegate alerts. |
| `Atlas6Events` | front/back `NativeQueue<Atlas6EventPayload>` | `IAtlas6EventListener` | `RaisePlayerStatusChanged`, `RaiseDirectiveConflict`, `RaiseBarterAccepted`, `RaiseScarcityDirective` | `SystemDispatcher.LateUpdate()` | Declared inside `Atlas6DirectiveSystem.cs`; separate lane from `AtlasSignalEvents`. Listener reenqueue writes to the next-frame queue. |
| `GlobalRegistry` service rebound events | front/back `NativeQueue<RegistryEventPayload>` plus fixed sidecar slots | `IRegistryEventListener`, `IGlobalRegistryHotSwapListener` | service `Register*` / `Unregister*` rebound queueing | `SystemDispatcher.LateUpdate()` via `FlushPendingServiceReboundEvents()` | Service replacement/unregister notification lane. Listener-induced service changes write to the next-frame queue; managed service refs stay in fixed sidecar slots until dispatch releases them. |
| `ModCommandDispatcher` | `NativeQueue<ModCommand>`, `NativeQueue<ModAupCommand>`, `NativeQueue<ModRenderInstanceCommand>` | `IModCommandKernel`, `IDispatcherRaycastReceiver`, `HectonEventBus` unmanaged result payloads | `Request`, `RequestAup`, `RequestRenderInstance` | `SystemDispatcher.LateUpdate()` before first-party event flushes | Commands are throttled to 128/mod/tick and drained after dispatcher raycasts complete. |

World-domain lanes use queue payloads with interface listener buckets, not delegate buckets. Current listener contracts include `IBaseAirlockEventListener`, `IDepthZoneEventListener`, `ISoundscapeEventListener`, `IEmergencyServiceRelayEventListener`, and `ISargassumGlobalDragEventListener`.

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

## Mod FileStream Paging Path

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
- Correctness: low risk on queue ownership claims because every lane listed here was mapped from source files and dispatcher flush order.

## Hot Path Impact

- none from the documentation change itself
- the actual runtime hot-path sensitivity remains in `SystemDispatcher.LateUpdate()` because every queue flush is serialized there

## Failure Modes

- new event lanes can be added outside this document and make it stale
- listener implementations were not exhaustively mapped here; this matrix covers queue ownership and dispatch lanes, not every subscriber class
- `HectonEventBus` may be incorrectly treated as queue-backed by future agents unless this scope note is preserved

## Why Kept

- the file separates source-observed queue lanes from managed mod bus traffic
- the dispatch order is anchored to `SystemDispatcher.LateUpdate()`
- no inferred listener graph was invented beyond what source ownership supports
