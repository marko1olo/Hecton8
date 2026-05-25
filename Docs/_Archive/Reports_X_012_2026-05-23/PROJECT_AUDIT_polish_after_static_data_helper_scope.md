# [ARCHIVE] X_012 Historical Report

Archive date: 2026-05-23
Reason: removed from active documentation corpus by X_012; historical evidence only.
Active index: ../../Reports/README.md

# Polish Mandate Static Audit

Evidence class: STATIC_SOURCE. No Unity import, compile, Play Mode, profiler, GC, memory, player build, or device proof was executed.

- Schema: `hecton8.polish_mandate_static_audit.v1`
- Source root: `Assets/_Project/Scripts`
- C# files: `2198`

## Counts

| Category | Matches | Files |
|---|---:|---:|
| `binaryHardwareSwitch` | 0 | 0 |
| `burstCompile` | 1382 | 330 |
| `burstMissingCompileSynchronously` | 0 | 0 |
| `burstMissingFloatMode` | 0 | 0 |
| `burstMissingFloatPrecision` | 0 | 0 |
| `globalQualityWeight` | 1907 | 430 |
| `jobHandleComplete` | 112 | 31 |
| `linqSurface` | 0 | 0 |
| `nativeApiExposureAmbiguousMutable` | 0 | 0 |
| `nativeApiExposureBuildEditorOnly` | 0 | 0 |
| `nativeApiExposureBuildPlayerRuntime` | 34 | 10 |
| `nativeApiExposureBuildQaDevProof` | 0 | 0 |
| `nativeApiExposureMutableReturn` | 15 | 8 |
| `nativeApiExposureOutRefMutable` | 19 | 6 |
| `nativeApiExposurePrivateNestedSuppressed` | 0 | 0 |
| `nativeApiRiskCoreVaultOrAllocatorSurface` | 21 | 3 |
| `nativeApiRiskEditorOrProofSurface` | 0 | 0 |
| `nativeApiRiskRuntimeAmbiguousMutableView` | 0 | 0 |
| `nativeApiRiskRuntimeDiagnosticNamedMutableView` | 1 | 1 |
| `nativeApiRiskRuntimeOutRefMutableView` | 3 | 2 |
| `nativeApiRiskRuntimeReturnMutableView` | 9 | 5 |
| `nativeCollectionPublicMutableApiExposure` | 34 | 10 |
| `noAlias` | 2192 | 218 |
| `packOne` | 0 | 0 |
| `privateNativeBuildEditorOnly` | 50 | 20 |
| `privateNativeBuildPlayerRuntime` | 1275 | 209 |
| `privateNativeBuildQaDevProof` | 14 | 7 |
| `privateNativeCollectionBlackBoxTelemetry` | 84 | 60 |
| `privateNativeCollectionField` | 1339 | 236 |
| `privateNativeCollectionOwnerLocalScratch` | 80 | 50 |
| `privateNativeCollectionStaticQueueLane` | 210 | 59 |
| `privateNativeCollectionUnclassified` | 937 | 155 |
| `privateNativeCollectionVaultAlias` | 28 | 1 |
| `privateNativeDeclarationAmbiguous` | 2 | 2 |
| `privateNativeDeclarationField` | 1191 | 184 |
| `privateNativeDeclarationMethodReturn` | 146 | 62 |
| `privateNativeRiskEditorOrProofNativeState` | 47 | 18 |
| `privateNativeRiskJobStructNativeView` | 0 | 0 |
| `privateNativeRiskMethodReturningNativeCollection` | 146 | 62 |
| `privateNativeRiskOwnerLocalRuntimeNativeState` | 776 | 97 |
| `privateNativeRiskStaticGlobalNativeState` | 121 | 21 |
| `privateNativeRiskStaticSignalOrEventBridge` | 219 | 63 |
| `privateNativeRiskUnclassifiedNativeCollection` | 1 | 1 |
| `privateNativeRiskVaultAliasOrVaultResolver` | 29 | 2 |
| `structAutoProperties` | 0 | 0 |
| `unityRandom` | 0 | 0 |
| `unityTimeBuildEditorOnly` | 14 | 8 |
| `unityTimeBuildPlayerRuntime` | 830 | 230 |
| `unityTimeBuildQaDevProof` | 23 | 7 |
| `unityTimeCritical` | 867 | 245 |
| `unityTimeDelta` | 1 | 1 |
| `unityTimeFrameCount` | 832 | 236 |
| `unityTimeRiskCooldownOrPerfLog` | 34 | 13 |
| `unityTimeRiskEditorOrProof` | 37 | 15 |
| `unityTimeRiskFrameStampOrTelemetry` | 796 | 222 |
| `unityTimeRiskGameplayDelta` | 0 | 0 |
| `unityTimeRiskGameplayWallClock` | 0 | 0 |
| `unityTimeWallClock` | 34 | 13 |
| `unityUpdateMethod` | 11 | 11 |

## Top Files

### burstCompile

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs` | 42 |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 18 |
| `Assets/_Project/Scripts/Inventory/Routing/InventoryRoutingNetwork.cs` | 18 |
| `Assets/_Project/Scripts/Core/DistanceMath.cs` | 17 |
| `Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs` | 17 |

### globalQualityWeight

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs` | 27 |
| `Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs` | 26 |
| `Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs` | 25 |
| `Assets/_Project/Scripts/FabricationAssemblerRuntime.cs` | 25 |
| `Assets/_Project/Scripts/Habitat/Deformation/Editor/DamageBake/HabitatDamageBakePipeline.cs` | 24 |

### jobHandleComplete

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs` | 15 |
| `Assets/_Project/Scripts/QA/Headless/JacobiStressFuzzer/PowerGridJacobiStressFuzzer.cs` | 10 |
| `Assets/_Project/Scripts/World/OfflineHadalArchBaker/Editor/HadalArchBakePipeline.cs` | 9 |
| `Assets/_Project/Scripts/Editor/GeologyForge/GeologyForgeGenerator.cs` | 7 |
| `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/Shinobu213/OfflineGeometryBaker.cs` | 7 |

### nativeApiExposureBuildPlayerRuntime

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` | 9 |
| `Assets/_Project/Scripts/Core/HectonArenaAllocator.cs` | 6 |
| `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` | 6 |
| `Assets/_Project/Scripts/Core/GlobalSignals.cs` | 4 |
| `Assets/_Project/Scripts/Core/ThreadSafeCommandQueue.cs` | 3 |

### nativeApiExposureMutableReturn

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Core/GlobalSignals.cs` | 4 |
| `Assets/_Project/Scripts/Core/HectonArenaAllocator.cs` | 2 |
| `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` | 2 |
| `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` | 2 |
| `Assets/_Project/Scripts/Core/ThreadSafeCommandQueue.cs` | 2 |

### nativeApiExposureOutRefMutable

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` | 7 |
| `Assets/_Project/Scripts/Core/HectonArenaAllocator.cs` | 4 |
| `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` | 4 |
| `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 2 |
| `Assets/_Project/Scripts/Core/ThreadSafeCommandQueue.cs` | 1 |

### nativeApiRiskCoreVaultOrAllocatorSurface

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` | 9 |
| `Assets/_Project/Scripts/Core/HectonArenaAllocator.cs` | 6 |
| `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` | 6 |

### nativeApiRiskRuntimeDiagnosticNamedMutableView

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/SaveBinaryStorage.cs` | 1 |

### nativeApiRiskRuntimeOutRefMutableView

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 2 |
| `Assets/_Project/Scripts/Core/ThreadSafeCommandQueue.cs` | 1 |

### nativeApiRiskRuntimeReturnMutableView

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Core/GlobalSignals.cs` | 4 |
| `Assets/_Project/Scripts/Core/ThreadSafeCommandQueue.cs` | 2 |
| `Assets/_Project/Scripts/Core/NativeArenaAllocator.cs` | 1 |
| `Assets/_Project/Scripts/Core/NativeArenaArray.cs` | 1 |
| `Assets/_Project/Scripts/Core/NativeRingBuffer.cs` | 1 |

### nativeCollectionPublicMutableApiExposure

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` | 9 |
| `Assets/_Project/Scripts/Core/HectonArenaAllocator.cs` | 6 |
| `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` | 6 |
| `Assets/_Project/Scripts/Core/GlobalSignals.cs` | 4 |
| `Assets/_Project/Scripts/Core/ThreadSafeCommandQueue.cs` | 3 |

### noAlias

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` | 73 |
| `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs` | 60 |
| `Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs` | 57 |
| `Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorTypes.cs` | 42 |
| `Assets/_Project/Scripts/Cartography/CartographyGridJobs.cs` | 40 |

### privateNativeBuildEditorOnly

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/OfflineHadalArchBaker/Editor/HadalArchBakePipeline.cs` | 13 |
| `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchBakePipeline.cs` | 9 |
| `Assets/_Project/Scripts/Editor/GeologyForge/TopographyForgeGenerator.cs` | 5 |
| `Assets/_Project/Scripts/Editor/ChroniclerDiagnosticHeatmapWindow.cs` | 2 |
| `Assets/_Project/Scripts/Editor/HydraulicErosionForge/Shinobu242/HydraulicErosionForgeBaker.cs` | 2 |

### privateNativeBuildPlayerRuntime

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Core/GlobalSignals.cs` | 75 |
| `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` | 51 |
| `Assets/_Project/Scripts/PlayerInventory.cs` | 49 |
| `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | 49 |
| `Assets/_Project/Scripts/Construction/DroneFleetManager.cs` | 42 |

### privateNativeBuildQaDevProof

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs` | 6 |
| `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs` | 2 |
| `Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs` | 2 |
| `Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs` | 1 |
| `Assets/_Project/Scripts/Dev/HabitatStressSmokeTester.cs` | 1 |

### privateNativeCollectionBlackBoxTelemetry

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/HectonFluidEngine.cs` | 5 |
| `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs` | 3 |
| `Assets/_Project/Scripts/EncounterDirector.cs` | 3 |
| `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs` | 3 |
| `Assets/_Project/Scripts/SaveManager.cs` | 3 |

### privateNativeCollectionField

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Core/GlobalSignals.cs` | 75 |
| `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` | 51 |
| `Assets/_Project/Scripts/PlayerInventory.cs` | 49 |
| `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | 49 |
| `Assets/_Project/Scripts/Construction/DroneFleetManager.cs` | 42 |

### privateNativeCollectionOwnerLocalScratch

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Construction/DroneFleetManager.cs` | 5 |
| `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs` | 5 |
| `Assets/_Project/Scripts/SpatialAudioManager.cs` | 4 |
| `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` | 3 |
| `Assets/_Project/Scripts/ConstructionManager.cs` | 2 |

### privateNativeCollectionStaticQueueLane

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Core/GlobalSignals.cs` | 74 |
| `Assets/_Project/Scripts/Visor/SpectrumSystem.cs` | 12 |
| `Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs` | 7 |
| `Assets/_Project/Scripts/Gameplay/PlayerSignalEvents.cs` | 6 |
| `Assets/_Project/Scripts/Gameplay/RandomEventSystem.cs` | 6 |

### privateNativeCollectionUnclassified

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | 47 |
| `Assets/_Project/Scripts/PlayerInventory.cs` | 46 |
| `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` | 46 |
| `Assets/_Project/Scripts/Construction/DroneFleetManager.cs` | 35 |
| `Assets/_Project/Scripts/HectonFluidEngine.cs` | 34 |

### privateNativeCollectionVaultAlias

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/SpatialAudioManager.cs` | 28 |

### privateNativeDeclarationAmbiguous

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs` | 1 |
| `Assets/_Project/Scripts/Physics/CablePhysicsSolver132.cs` | 1 |

### privateNativeDeclarationField

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Core/GlobalSignals.cs` | 75 |
| `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` | 50 |
| `Assets/_Project/Scripts/PlayerInventory.cs` | 49 |
| `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | 49 |
| `Assets/_Project/Scripts/HectonFluidEngine.cs` | 40 |

### privateNativeDeclarationMethodReturn

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs` | 12 |
| `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | 9 |
| `Assets/_Project/Scripts/Core/Bucketing/ModuloSimulationBucketer.cs` | 8 |
| `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs` | 7 |
| `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs` | 6 |

### privateNativeRiskEditorOrProofNativeState

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/OfflineHadalArchBaker/Editor/HadalArchBakePipeline.cs` | 13 |
| `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchBakePipeline.cs` | 9 |
| `Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs` | 6 |
| `Assets/_Project/Scripts/Editor/ChroniclerDiagnosticHeatmapWindow.cs` | 2 |
| `Assets/_Project/Scripts/Editor/SignalTrafficMonitorWindow.cs` | 2 |

### privateNativeRiskMethodReturningNativeCollection

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs` | 12 |
| `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | 9 |
| `Assets/_Project/Scripts/Core/Bucketing/ModuloSimulationBucketer.cs` | 8 |
| `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs` | 7 |
| `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs` | 6 |

### privateNativeRiskOwnerLocalRuntimeNativeState

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` | 50 |
| `Assets/_Project/Scripts/PlayerInventory.cs` | 49 |
| `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | 49 |
| `Assets/_Project/Scripts/HectonFluidEngine.cs` | 40 |
| `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 34 |

### privateNativeRiskStaticGlobalNativeState

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Construction/DroneFleetManager.cs` | 37 |
| `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs` | 22 |
| `Assets/_Project/Scripts/Construction/LogisticsPipeTransportScheduler.cs` | 10 |
| `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` | 10 |
| `Assets/_Project/Scripts/World/WorldSpatialHashGrid.cs` | 9 |

### privateNativeRiskStaticSignalOrEventBridge

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Core/GlobalSignals.cs` | 74 |
| `Assets/_Project/Scripts/Visor/SpectrumSystem.cs` | 12 |
| `Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs` | 7 |
| `Assets/_Project/Scripts/Gameplay/PlayerSignalEvents.cs` | 6 |
| `Assets/_Project/Scripts/Gameplay/RandomEventSystem.cs` | 6 |

### privateNativeRiskUnclassifiedNativeCollection

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs` | 1 |

### privateNativeRiskVaultAliasOrVaultResolver

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/SpatialAudioManager.cs` | 28 |
| `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` | 1 |

### unityTimeBuildEditorOnly

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Editor/CodexPlayModeLauncher.cs` | 5 |
| `Assets/_Project/Scripts/Editor/SignalTrafficMonitorWindow.cs` | 3 |
| `Assets/_Project/Scripts/Core/Diagnostics/Visuals/Editor/ArchitectEyeBlackBoxTimelineViewer.cs` | 1 |
| `Assets/_Project/Scripts/Editor/ConstructionSocketEditorTools.cs` | 1 |
| `Assets/_Project/Scripts/Editor/LSystemGenomeLabWindow.cs` | 1 |

### unityTimeBuildPlayerRuntime

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/CrashTelemetryBuffer.cs` | 36 |
| `Assets/_Project/Scripts/SpatialAudioManager.cs` | 30 |
| `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs` | 26 |
| `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs` | 20 |
| `Assets/_Project/Scripts/Core/InputDispatcher.cs` | 18 |

### unityTimeBuildQaDevProof

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs` | 6 |
| `Assets/_Project/Scripts/QA/QAEnduranceWatchdogBot.cs` | 6 |
| `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs` | 5 |
| `Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs` | 2 |
| `Assets/_Project/Scripts/QA/Headless/Shinobu38QaWatchdogRuntime.cs` | 2 |

### unityTimeCritical

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/CrashTelemetryBuffer.cs` | 36 |
| `Assets/_Project/Scripts/SpatialAudioManager.cs` | 30 |
| `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs` | 26 |
| `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs` | 20 |
| `Assets/_Project/Scripts/Core/InputDispatcher.cs` | 18 |

### unityTimeDelta

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Dev/CelestialTimeLapseDebugger.cs` | 1 |

### unityTimeFrameCount

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/CrashTelemetryBuffer.cs` | 36 |
| `Assets/_Project/Scripts/SpatialAudioManager.cs` | 30 |
| `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs` | 26 |
| `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs` | 20 |
| `Assets/_Project/Scripts/Core/InputDispatcher.cs` | 18 |

### unityTimeRiskCooldownOrPerfLog

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/UI/BaseIntegrityHUD.cs` | 8 |
| `Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs` | 4 |
| `Assets/_Project/Scripts/AtlasSignal/AtlasSignalSystem.cs` | 2 |
| `Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs` | 2 |
| `Assets/_Project/Scripts/Optimization/CameraRTManager.cs` | 2 |

### unityTimeRiskEditorOrProof

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs` | 6 |
| `Assets/_Project/Scripts/QA/QAEnduranceWatchdogBot.cs` | 6 |
| `Assets/_Project/Scripts/Editor/CodexPlayModeLauncher.cs` | 5 |
| `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs` | 5 |
| `Assets/_Project/Scripts/Editor/SignalTrafficMonitorWindow.cs` | 3 |

### unityTimeRiskFrameStampOrTelemetry

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/CrashTelemetryBuffer.cs` | 36 |
| `Assets/_Project/Scripts/SpatialAudioManager.cs` | 30 |
| `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs` | 26 |
| `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs` | 20 |
| `Assets/_Project/Scripts/Core/InputDispatcher.cs` | 18 |

### unityTimeWallClock

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/UI/BaseIntegrityHUD.cs` | 8 |
| `Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs` | 4 |
| `Assets/_Project/Scripts/AtlasSignal/AtlasSignalSystem.cs` | 2 |
| `Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs` | 2 |
| `Assets/_Project/Scripts/Optimization/CameraRTManager.cs` | 2 |

### unityUpdateMethod

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Editor/FloraDearLieXRayWindow.cs` | 1 |
| `Assets/_Project/Scripts/Editor/SubmarineDynoTunerWindow.cs` | 1 |
| `Assets/_Project/Scripts/Editor/SumpPumpPipeGridTunerWindow.cs` | 1 |
| `Assets/_Project/Scripts/Editor/VerletTowTunerWindow.cs` | 1 |
| `Assets/_Project/Scripts/Physics/Buoyancy/AnalyticalWaveTunerWindow.Editor.cs` | 1 |

## Interpretation

- `Pack=1`, private persistent native collections, and Burst attribute drift are platform-portability risks until each hit is classified as cold file-format, owner-local scratch, or hot runtime.
- `jobHandleComplete`, Unity `Update` methods, `Time.*`, and `UnityEngine.Random` are not automatically defects, but they are mandatory review surfaces for gameplay/runtime code.
- Binary hardware switches are suspect unless they are presentation-only or build-time/platform setup. Runtime scalability should flow through continuous `GlobalQualityWeight` curves.
- This audit is a pressure map. It does not mutate code and does not prove frame cost.
