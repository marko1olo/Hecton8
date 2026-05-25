# Architecture Risk Hotlist

Evidence class: STATIC_SOURCE. This is a ranked review map, not compile, runtime, profiler, GC, memory, player-build, or device proof.

- Schema: `hecton8.architecture_risk_hotlist.v2`
- Source root: `Assets/_Project/Scripts`
- C# files: `2013`
- Scored files: `921`

## Family Totals

| Family | Matches |
|---|---:|
| `authority` | 6128 |
| `datavault` | 3311 |
| `determinism` | 1217 |
| `hotpath` | 7 |
| `jobs` | 102 |
| `layout` | 8 |
| `platform` | 102 |
| `signals` | 597 |

## Domain Pressure

| Rank | Domain | Score | Scored files | Family pressure | Top files |
|---:|---|---:|---:|---|---|
| 1 | `Root` | 12636 | 180 | authority:1504, signals:110, datavault:833, determinism:444, platform:6, jobs:12, layout:3 | Assets/_Project/Scripts/PlayerInventory.cs, Assets/_Project/Scripts/HectonFluidEngine.cs, Assets/_Project/Scripts/SpatialAudioManager.cs |
| 2 | `World` | 8239 | 102 | authority:768, signals:49, datavault:621, determinism:126, jobs:18, platform:15 | Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs, Assets/_Project/Scripts/World/DestructibleOrganicManager.cs, Assets/_Project/Scripts/World/OfflineHadalArchBaker/Editor/HadalArchBakePipeline.cs |
| 3 | `Core` | 4492 | 79 | authority:351, signals:130, datavault:266, determinism:160, platform:15, jobs:3 | Assets/_Project/Scripts/Core/GlobalSignals.cs, Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs, Assets/_Project/Scripts/Core/SystemDispatcher.cs |
| 4 | `Gameplay` | 3497 | 89 | authority:611, signals:81, datavault:161, determinism:96 | Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs, Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs, Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs |
| 5 | `Construction` | 2555 | 27 | authority:139, signals:19, datavault:210, determinism:26, platform:2 | Assets/_Project/Scripts/Construction/DroneFleetManager.cs, Assets/_Project/Scripts/Construction/HabitatGraphManager.cs, Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs |
| 6 | `Editor` | 2464 | 53 | datavault:191, jobs:61, authority:59, determinism:13, platform:3, signals:4, hotpath:3 | Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs, Assets/_Project/Scripts/Editor/OfflineGeometryBaker/Shinobu213/OfflineGeometryBaker.cs, Assets/_Project/Scripts/Editor/TextureChannelPacker/HectonArmTextureChannelPacker.cs |
| 7 | `UI` | 2156 | 86 | authority:630, datavault:105, determinism:75, signals:15 | Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs, Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs, Assets/_Project/Scripts/UI/VR/OpenXRManualOverrideLever.cs |
| 8 | `Audio` | 1595 | 16 | authority:199, signals:9, datavault:89, determinism:56, platform:12, jobs:2 | Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs, Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs, Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs |
| 9 | `Power` | 1416 | 11 | authority:28, datavault:128, signals:7, determinism:4 | Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs, Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs, Assets/_Project/Scripts/Power/PowerGridJacobiContracts.cs |
| 10 | `Atmosphere` | 1362 | 8 | authority:82, datavault:122, determinism:4, signals:5 | Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs, Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs, Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsTypes.cs |
| 11 | `Visor` | 695 | 27 | authority:101, signals:4, datavault:40, determinism:25, platform:10 | Assets/_Project/Scripts/Visor/SpectrumSystem.cs, Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs, Assets/_Project/Scripts/Visor/DynamicDecalVaultRuntime.cs |
| 12 | `Physics` | 686 | 24 | authority:90, datavault:50, signals:15, platform:6, hotpath:2 | Assets/_Project/Scripts/Physics/CablePhysicsSolver132.cs, Assets/_Project/Scripts/Physics/Vehicles/Automation/SubmarineAutopilotSdfNavigator.cs, Assets/_Project/Scripts/Physics/Cavitation/AbyssalCavitationContracts.cs |
| 13 | `Graphics` | 657 | 11 | datavault:56, authority:75, determinism:10, platform:6, signals:1 | Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs, Assets/_Project/Scripts/Graphics/Culling/AbyssalShadowCullingTypes.cs, Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonRuntime.cs |
| 14 | `Fauna` | 591 | 12 | authority:94, signals:17, determinism:17, datavault:19, platform:1, layout:4 | Assets/_Project/Scripts/Fauna/FaunaBrain.cs, Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs, Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs |
| 15 | `ModdingAPI` | 577 | 12 | authority:38, signals:15, datavault:20, determinism:12, platform:3 | Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs, Assets/_Project/Scripts/ModdingAPI/HectonAPI.cs, Assets/_Project/Scripts/ModdingAPI/ModEventProjectionBridge.cs |
| 16 | `QA` | 564 | 4 | authority:47, signals:14, datavault:32, determinism:16, platform:1 | Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs, Assets/_Project/Scripts/QA/Headless/Shinobu38QaWatchdogRuntime.cs, Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs |
| 17 | `Lighting` | 495 | 5 | datavault:42, authority:39, determinism:6, signals:1 | Assets/_Project/Scripts/Lighting/DynamicPointLightCulling/DynamicPointLightCullingContracts.cs, Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs, Assets/_Project/Scripts/Lighting/HectonGIRelaySystem.cs |
| 18 | `Tools` | 402 | 13 | datavault:26, authority:68, signals:14, determinism:4 | Assets/_Project/Scripts/Tools/LaserCutterDodContracts.cs, Assets/_Project/Scripts/Tools/UpgradeMatrixCompiler.cs, Assets/_Project/Scripts/Tools/ToolKinematics/ToolKinematicsRuntime.cs |
| 19 | `Physiology` | 372 | 7 | datavault:27, authority:36, signals:7 | Assets/_Project/Scripts/Physiology/ShinobuMetabolismData.cs, Assets/_Project/Scripts/Physiology/ShinobuRespawnData.cs, Assets/_Project/Scripts/Physiology/PlayerStressMetricsRuntime.cs |
| 20 | `Plugins` | 346 | 14 | datavault:30, jobs:4, authority:34, determinism:6 | Assets/_Project/Scripts/Plugins/MapMagic/HectonAnomalyMapMagicNode.cs, Assets/_Project/Scripts/Plugins/MapMagic/HectonSpaceEngine098MapMagicNodes.cs, Assets/_Project/Scripts/Plugins/MapMagic/HectonTerrainSplatmapMapMagicNode.cs |

## Category Totals

| Category | Matches |
|---|---:|
| `binaryHardwareSwitch` | 102 |
| `globalRegistryDot` | 6128 |
| `globalSignalsPublish` | 259 |
| `hectonEventBusPubSub` | 45 |
| `jobHandleComplete` | 102 |
| `localNumericBufferCast` | 798 |
| `nativeArrayCtor` | 1161 |
| `privateNativeCollectionField` | 1352 |
| `signalBusPushTryPush` | 293 |
| `structAutoProperties` | 8 |
| `unityRandom` | 2 |
| `unityTimeCritical` | 1215 |
| `unityUpdateMethod` | 7 |

## Top Review Files

| Rank | Score | File | Top categories |
|---:|---:|---|---|
| 1 | 1256 | `Assets/_Project/Scripts/PlayerInventory.cs` | globalRegistryDot:16, signalBusPushTryPush:2, globalSignalsPublish:6, hectonEventBusPubSub:2, nativeArrayCtor:63, privateNativeCollectionField:49, unityTimeCritical:9 |
| 2 | 1070 | `Assets/_Project/Scripts/Core/GlobalSignals.cs` | globalRegistryDot:7, signalBusPushTryPush:67, privateNativeCollectionField:75, unityTimeCritical:2, binaryHardwareSwitch:7 |
| 3 | 935 | `Assets/_Project/Scripts/HectonFluidEngine.cs` | globalRegistryDot:35, globalSignalsPublish:4, nativeArrayCtor:40, privateNativeCollectionField:40, unityTimeCritical:13 |
| 4 | 797 | `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | globalRegistryDot:1, nativeArrayCtor:26, privateNativeCollectionField:49 |
| 5 | 776 | `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs` | globalRegistryDot:26, signalBusPushTryPush:1, privateNativeCollectionField:54, unityTimeCritical:22, binaryHardwareSwitch:4 |
| 6 | 741 | `Assets/_Project/Scripts/SpatialAudioManager.cs` | globalRegistryDot:36, globalSignalsPublish:1, localNumericBufferCast:13, privateNativeCollectionField:36, unityTimeCritical:32, binaryHardwareSwitch:1 |
| 7 | 731 | `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs` | globalRegistryDot:15, nativeArrayCtor:33, privateNativeCollectionField:37, unityTimeCritical:2 |
| 8 | 703 | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs` | globalRegistryDot:21, signalBusPushTryPush:3, globalSignalsPublish:3, localNumericBufferCast:24, privateNativeCollectionField:29, unityTimeCritical:13 |
| 9 | 699 | `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | globalRegistryDot:19, nativeArrayCtor:32, privateNativeCollectionField:35, unityTimeCritical:1 |
| 10 | 670 | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs` | globalRegistryDot:15, signalBusPushTryPush:9, localNumericBufferCast:13, privateNativeCollectionField:40, unityTimeCritical:6, binaryHardwareSwitch:1 |
| 11 | 622 | `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` | globalRegistryDot:24, signalBusPushTryPush:1, nativeArrayCtor:5, privateNativeCollectionField:40, unityTimeCritical:19 |
| 12 | 604 | `Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs` | nativeArrayCtor:53, jobHandleComplete:15 |
| 13 | 482 | `Assets/_Project/Scripts/SaveManager.cs` | globalRegistryDot:38, signalBusPushTryPush:2, globalSignalsPublish:6, nativeArrayCtor:18, privateNativeCollectionField:13, unityTimeCritical:17 |
| 14 | 479 | `Assets/_Project/Scripts/HectonNarrativeDirector.cs` | globalRegistryDot:23, globalSignalsPublish:6, nativeArrayCtor:17, privateNativeCollectionField:18, unityTimeCritical:8 |
| 15 | 476 | `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs` | globalRegistryDot:5, globalSignalsPublish:1, nativeArrayCtor:23, privateNativeCollectionField:21, unityTimeCritical:5, binaryHardwareSwitch:1 |
| 16 | 396 | `Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs` | globalRegistryDot:8, globalSignalsPublish:3, nativeArrayCtor:17, privateNativeCollectionField:18 |
| 17 | 376 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` | globalRegistryDot:20, nativeArrayCtor:40, privateNativeCollectionField:1, unityTimeCritical:6 |
| 18 | 365 | `Assets/_Project/Scripts/SubmarineStructuralGrid.cs` | globalRegistryDot:17, globalSignalsPublish:3, nativeArrayCtor:15, privateNativeCollectionField:15, unityTimeCritical:3 |
| 19 | 354 | `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs` | globalRegistryDot:2, globalSignalsPublish:3, nativeArrayCtor:1, privateNativeCollectionField:25, unityTimeCritical:2 |
| 20 | 353 | `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs` | globalRegistryDot:7, localNumericBufferCast:15, privateNativeCollectionField:15, unityTimeCritical:4 |
| 21 | 350 | `Assets/_Project/Scripts/Fabricator.cs` | globalRegistryDot:18, globalSignalsPublish:5, nativeArrayCtor:12, privateNativeCollectionField:13, unityTimeCritical:5 |
| 22 | 328 | `Assets/_Project/Scripts/World/OfflineHadalArchBaker/Editor/HadalArchBakePipeline.cs` | nativeArrayCtor:8, privateNativeCollectionField:13, jobHandleComplete:9 |
| 23 | 324 | `Assets/_Project/Scripts/TetherInstance.cs` | globalSignalsPublish:2, privateNativeCollectionField:25 |
| 24 | 311 | `Assets/_Project/Scripts/World/FloraInteractionManager.cs` | globalRegistryDot:29, signalBusPushTryPush:2, localNumericBufferCast:5, nativeArrayCtor:7, privateNativeCollectionField:14, unityTimeCritical:1 |
| 25 | 306 | `Assets/_Project/Scripts/Core/SystemDispatcher.cs` | globalRegistryDot:32, signalBusPushTryPush:4, globalSignalsPublish:7, localNumericBufferCast:1, unityTimeCritical:43 |
| 26 | 289 | `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs` | globalRegistryDot:29, nativeArrayCtor:5, privateNativeCollectionField:17, unityTimeCritical:4 |
| 27 | 288 | `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs` | globalRegistryDot:16, nativeArrayCtor:15, privateNativeCollectionField:12, unityTimeCritical:2 |
| 28 | 275 | `Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs` | globalRegistryDot:7, signalBusPushTryPush:2, globalSignalsPublish:2, localNumericBufferCast:24 |
| 29 | 273 | `Assets/_Project/Scripts/EncounterDirector.cs` | globalRegistryDot:1, nativeArrayCtor:13, privateNativeCollectionField:14 |
| 30 | 268 | `Assets/_Project/Scripts/World/AbyssalThermalManager.cs` | globalRegistryDot:26, signalBusPushTryPush:1, globalSignalsPublish:4, nativeArrayCtor:9, privateNativeCollectionField:9, unityTimeCritical:3 |
| 31 | 266 | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs` | globalRegistryDot:28, signalBusPushTryPush:11, globalSignalsPublish:3, localNumericBufferCast:18 |
| 32 | 259 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs` | globalRegistryDot:3, nativeArrayCtor:12, privateNativeCollectionField:13, unityTimeCritical:1 |
| 33 | 256 | `Assets/_Project/Scripts/SaveBinaryStorage.cs` | nativeArrayCtor:32 |
| 34 | 253 | `Assets/_Project/Scripts/CrashTelemetryBuffer.cs` | globalRegistryDot:49, nativeArrayCtor:3, privateNativeCollectionField:3, unityTimeCritical:36 |
| 35 | 252 | `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/Shinobu213/OfflineGeometryBaker.cs` | nativeArrayCtor:18, privateNativeCollectionField:2, jobHandleComplete:7 |
| 36 | 241 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs` | globalRegistryDot:13, nativeArrayCtor:11, privateNativeCollectionField:11, unityTimeCritical:2 |
| 37 | 240 | `Assets/_Project/Scripts/Construction/SumpPumpPipeGridContracts.cs` | localNumericBufferCast:24 |
| 38 | 240 | `Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs` | localNumericBufferCast:16, nativeArrayCtor:10 |
| 39 | 236 | `Assets/_Project/Scripts/Fauna/FaunaBrain.cs` | globalRegistryDot:40, globalSignalsPublish:11, unityTimeCritical:16 |
| 40 | 235 | `Assets/_Project/Scripts/Core/DodReplayRecorder.cs` | globalRegistryDot:3, nativeArrayCtor:1, privateNativeCollectionField:15, unityTimeCritical:11 |

## Review Notes

### Assets/_Project/Scripts/PlayerInventory.cs

- Score: `1256`
- Families: `{'authority': 16, 'datavault': 112, 'determinism': 9, 'signals': 10}`
- L519 `privateNativeCollectionField`: `private NativeArray<uint> _itemHashes;`
- L520 `privateNativeCollectionField`: `private NativeArray<ushort> _stackCounts;`
- L521 `privateNativeCollectionField`: `private NativeArray<float> _itemCondition;`
- L522 `privateNativeCollectionField`: `private NativeArray<float> _itemDurability;`

### Assets/_Project/Scripts/Core/GlobalSignals.cs

- Score: `1070`
- Families: `{'authority': 7, 'datavault': 75, 'determinism': 2, 'platform': 7, 'signals': 67}`
- L479 `binaryHardwareSwitch`: `public byte QualityTier;`
- L1829 `privateNativeCollectionField`: `private static NativeQueue<T> _queue;`
- L2160 `globalRegistryDot`: `global::Hecton8.Core.GlobalRegistry.SetSystemKillSwitchBits(NonCriticalVfxKillSwitchMask, true);`
- L2559 `globalRegistryDot`: `vault = global::Hecton8.Core.GlobalRegistry.DataVault;`

### Assets/_Project/Scripts/HectonFluidEngine.cs

- Score: `935`
- Families: `{'authority': 35, 'datavault': 80, 'determinism': 13, 'signals': 4}`
- L275 `globalRegistryDot`: `private const uint AbyssalFlowKillSwitchMask = GlobalRegistry.SystemKillSwitchLane4VfxMask;`
- L586 `globalRegistryDot`: `instance = GlobalRegistry.Fluid;`
- L1382 `privateNativeCollectionField`: `private NativeArray<float3>         _positions;`
- L1383 `privateNativeCollectionField`: `private NativeArray<float3>         _previousPositions;`

### Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs

- Score: `797`
- Families: `{'authority': 1, 'datavault': 75}`
- L1400 `privateNativeCollectionField`: `private NativeArray<LogisticsNode> _nodeBuffer;`
- L1401 `privateNativeCollectionField`: `private NativeArray<int> _edgeOffsets;`
- L1402 `privateNativeCollectionField`: `private NativeArray<int> _edgeDestinations;`
- L1403 `privateNativeCollectionField`: `private NativeArray<float> _edgeConductance;`

### Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs

- Score: `776`
- Families: `{'authority': 26, 'datavault': 54, 'determinism': 22, 'platform': 4, 'signals': 1}`
- L640 `privateNativeCollectionField`: `private NativeArray<float> _hullScratch;`
- L642 `privateNativeCollectionField`: `private NativeArray<float> _sonarScratch;`
- L644 `privateNativeCollectionField`: `private NativeArray<float> _impactEchoScratch;`
- L646 `privateNativeCollectionField`: `private NativeArray<float> _thrusterScratch;`

### Assets/_Project/Scripts/SpatialAudioManager.cs

- Score: `741`
- Families: `{'authority': 36, 'datavault': 49, 'determinism': 32, 'platform': 1, 'signals': 1}`
- L156 `unityTimeCritical`: `entry.LastUseFrame = Time.frameCount;`
- L278 `unityTimeCritical`: `LastUseFrame = Time.frameCount,`
- L499 `localNumericBufferCast`: `private const BufferID SpatialAudioVirtualVoiceTuningBufferId = (BufferID)70015;`
- L500 `localNumericBufferCast`: `private const BufferID SpatialAudioVirtualVoiceWritePoolBufferId = (BufferID)70016;`

### Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs

- Score: `731`
- Families: `{'authority': 15, 'datavault': 70, 'determinism': 2}`
- L142 `privateNativeCollectionField`: `private static NativeQueue<HighPressureEventPayload> _pendingEvents;`
- L143 `privateNativeCollectionField`: `private static NativeQueue<HighPressureEventPayload> _nextFrameEvents;`
- L333 `unityTimeCritical`: `int frame = Time.frameCount;`
- L410 `privateNativeCollectionField`: `private static NativeQueue<FatalPressureImplosionEventPayload> _pendingEvents;`

### Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs

- Score: `703`
- Families: `{'authority': 21, 'datavault': 53, 'determinism': 13, 'signals': 6}`
- L662 `localNumericBufferCast`: `private const BufferID ChunkResidencyVaultBufferId = (BufferID)70560;`
- L663 `localNumericBufferCast`: `private const BufferID AddressablesRequestVaultBufferId = (BufferID)70561;`
- L664 `localNumericBufferCast`: `private const BufferID HlodImpostorVaultBufferId = (BufferID)70562;`
- L665 `localNumericBufferCast`: `private const BufferID StreamingTuningVaultBufferId = (BufferID)70563;`

### Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs

- Score: `699`
- Families: `{'authority': 19, 'datavault': 67, 'determinism': 1}`
- L102 `privateNativeCollectionField`: `private NativeArray<float> RoomO2;`
- L103 `privateNativeCollectionField`: `private NativeArray<float> RoomCO2;`
- L104 `privateNativeCollectionField`: `private NativeArray<float> RoomPressure;`
- L105 `privateNativeCollectionField`: `private NativeArray<float> _roomO2Back;`

### Assets/_Project/Scripts/Construction/DroneFleetManager.cs

- Score: `670`
- Families: `{'authority': 15, 'datavault': 53, 'determinism': 6, 'platform': 1, 'signals': 9}`
- L161 `localNumericBufferCast`: `private const BufferID PendingEventBufferId = (BufferID)70271;`
- L162 `localNumericBufferCast`: `private const BufferID NextFrameEventBufferId = (BufferID)70272;`
- L170 `privateNativeCollectionField`: `private static NativeArray<HectonDroneFleetSnapshotPayload> _pendingEvents;`
- L171 `privateNativeCollectionField`: `private static NativeArray<HectonDroneFleetSnapshotPayload> _nextFrameEvents;`

### Assets/_Project/Scripts/World/DestructibleOrganicManager.cs

- Score: `622`
- Families: `{'authority': 24, 'datavault': 45, 'determinism': 19, 'signals': 1}`
- L342 `privateNativeCollectionField`: `private NativeArray<uint> _surfaceInstanceUids;`
- L343 `privateNativeCollectionField`: `private NativeArray<uint> _underwaterInstanceUids;`
- L344 `privateNativeCollectionField`: `private NativeArray<byte> _surfaceMaterialClasses;`
- L345 `privateNativeCollectionField`: `private NativeArray<byte> _underwaterMaterialClasses;`

### Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs

- Score: `604`
- Families: `{'datavault': 53, 'jobs': 15}`
- L152 `nativeArrayCtor`: `basinMask = new NativeArray<byte>(4, Allocator.TempJob, NativeArrayOptions.ClearMemory);`
- L153 `nativeArrayCtor`: `basinRecords = new NativeArray<AnomalyBasinRecord>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);`
- L154 `nativeArrayCtor`: `bounds = new NativeArray<AnomalyBrinePoolBounds>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);`
- L183 `jobHandleComplete`: `handle.Complete();`

## Interpretation

- This hotlist is for ordering review, not for automatic refactor.
- Editor-only files can score high and still be acceptable; runtime/hot-path files need stricter treatment.
- A high score means multiple architectural pressure surfaces overlap in one file: registry, signals, native ownership, job barriers, deterministic time/random, layout, or platform-tier logic.
- Domain pressure is for owner slicing: fix one domain route at a time with a route card and proof artifact instead of broad repository churn.
- Do not use score movement as H-Phi proof. It is a triage input for targeted owner-domain passes.
