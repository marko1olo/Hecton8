# Polish Mandate Static Audit

Evidence class: STATIC_SOURCE. No Unity import, compile, Play Mode, profiler, GC, memory, player build, or device proof was executed.

- Schema: `hecton8.polish_mandate_static_audit.v1`
- Source root: `Assets/_Project/Scripts`
- C# files: `2439`

## Counts

| Category | Matches | Files |
|---|---:|---:|
| `binaryHardwareSwitch` | 0 | 0 |
| `burstCompile` | 1566 | 360 |
| `burstMissingCompileSynchronously` | 0 | 0 |
| `burstMissingFloatMode` | 0 | 0 |
| `burstMissingFloatPrecision` | 0 | 0 |
| `globalQualityWeight` | 2480 | 560 |
| `jobHandleComplete` | 134 | 43 |
| `linqSurface` | 0 | 0 |
| `nativeApiExposureAmbiguousMutable` | 0 | 0 |
| `nativeApiExposureBuildEditorOnly` | 0 | 0 |
| `nativeApiExposureBuildPlayerRuntime` | 155 | 19 |
| `nativeApiExposureBuildQaDevProof` | 0 | 0 |
| `nativeApiExposureMutableReturn` | 129 | 7 |
| `nativeApiExposureOutRefMutable` | 26 | 15 |
| `nativeApiExposurePrivateNestedSuppressed` | 25 | 13 |
| `nativeApiRiskCoreVaultOrAllocatorSurface` | 8 | 2 |
| `nativeApiRiskEditorOrProofSurface` | 0 | 0 |
| `nativeApiRiskRuntimeAmbiguousMutableView` | 0 | 0 |
| `nativeApiRiskRuntimeDiagnosticNamedMutableView` | 10 | 6 |
| `nativeApiRiskRuntimeOutRefMutableView` | 14 | 10 |
| `nativeApiRiskRuntimeReturnMutableView` | 123 | 6 |
| `nativeCollectionPublicMutableApiExposure` | 155 | 19 |
| `noAlias` | 2492 | 241 |
| `packOne` | 0 | 0 |
| `privateNativeBuildEditorOnly` | 41 | 20 |
| `privateNativeBuildPlayerRuntime` | 556 | 121 |
| `privateNativeBuildQaDevProof` | 26 | 6 |
| `privateNativeCollectionBlackBoxTelemetry` | 47 | 34 |
| `privateNativeCollectionField` | 623 | 147 |
| `privateNativeCollectionOwnerLocalScratch` | 18 | 15 |
| `privateNativeCollectionStaticQueueLane` | 89 | 39 |
| `privateNativeCollectionUnclassified` | 469 | 98 |
| `privateNativeCollectionVaultAlias` | 0 | 0 |
| `privateNativeDeclarationAmbiguous` | 93 | 7 |
| `privateNativeDeclarationField` | 353 | 83 |
| `privateNativeDeclarationMethodReturn` | 177 | 68 |
| `privateNativeRiskEditorOrProofNativeState` | 46 | 15 |
| `privateNativeRiskJobStructNativeView` | 0 | 0 |
| `privateNativeRiskMethodReturningNativeCollection` | 177 | 68 |
| `privateNativeRiskOwnerLocalRuntimeNativeState` | 149 | 21 |
| `privateNativeRiskStaticGlobalNativeState` | 60 | 11 |
| `privateNativeRiskStaticSignalOrEventBridge` | 97 | 42 |
| `privateNativeRiskUnclassifiedNativeCollection` | 15 | 5 |
| `privateNativeRiskVaultAliasOrVaultResolver` | 79 | 3 |
| `structAutoProperties` | 0 | 0 |
| `unityRandom` | 0 | 0 |
| `unityTimeBuildEditorOnly` | 16 | 10 |
| `unityTimeBuildPlayerRuntime` | 12 | 7 |
| `unityTimeBuildQaDevProof` | 1 | 1 |
| `unityTimeCritical` | 29 | 18 |
| `unityTimeDelta` | 1 | 1 |
| `unityTimeFrameCount` | 24 | 15 |
| `unityTimeRiskCooldownOrPerfLog` | 4 | 2 |
| `unityTimeRiskEditorOrProof` | 17 | 11 |
| `unityTimeRiskFrameStampOrTelemetry` | 8 | 5 |
| `unityTimeRiskGameplayDelta` | 0 | 0 |
| `unityTimeRiskGameplayWallClock` | 0 | 0 |
| `unityTimeWallClock` | 4 | 2 |
| `unityUpdateMethod` | 15 | 15 |

## Top Files

### burstCompile

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs` | 43 |
| `Assets/_Project/Scripts/Core/DistanceMath.cs` | 25 |
| `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs` | 18 |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 18 |
| `Assets/_Project/Scripts/Inventory/Routing/InventoryRoutingNetwork.cs` | 18 |

### globalQualityWeight

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs` | 34 |
| `Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs` | 26 |
| `Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs` | 26 |
| `Assets/_Project/Scripts/FabricationAssemblerRuntime.cs` | 25 |
| `Assets/_Project/Scripts/Habitat/Deformation/Editor/DamageBake/HabitatDamageBakePipeline.cs` | 24 |

### jobHandleComplete

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs` | 15 |
| `Assets/_Project/Scripts/Physics/KCC/Editor/Shinobu355KccSmokeEditorFacade.cs` | 8 |
| `Assets/_Project/Scripts/World/BiotaDensityMapBaker/Editor/BiotaDensityBakePipeline.cs` | 8 |
| `Assets/_Project/Scripts/Editor/GeologyForge/GeologyForgeGenerator.cs` | 7 |
| `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/Shinobu213/OfflineGeometryBaker.cs` | 7 |

### nativeApiExposureBuildPlayerRuntime

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 81 |
| `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` | 25 |
| `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs` | 20 |
| `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` | 4 |
| `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` | 4 |

### nativeApiExposureMutableReturn

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 81 |
| `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` | 25 |
| `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs` | 18 |
| `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` | 2 |
| `Assets/_Project/Scripts/Core/NativeArenaArray.cs` | 1 |

### nativeApiExposureOutRefMutable

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` | 4 |
| `Assets/_Project/Scripts/Gameplay/AirlockPressurization/AirlockPressurizationVault.cs` | 4 |
| `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` | 2 |
| `Assets/_Project/Scripts/CraftingSystem.FastFail.cs` | 2 |
| `Assets/_Project/Scripts/PlayerInventory.cs` | 2 |

### nativeApiExposurePrivateNestedSuppressed

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` | 5 |
| `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs` | 3 |
| `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs` | 2 |
| `Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs` | 2 |
| `Assets/_Project/Scripts/GlobalPhysicsStateManager.cs` | 2 |

### nativeApiRiskCoreVaultOrAllocatorSurface

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` | 4 |
| `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` | 4 |

### nativeApiRiskRuntimeDiagnosticNamedMutableView

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs` | 3 |
| `Assets/_Project/Scripts/CraftingSystem.FastFail.cs` | 2 |
| `Assets/_Project/Scripts/Gameplay/AirlockPressurization/AirlockPressurizationVault.cs` | 2 |
| `Assets/_Project/Scripts/ModularEquipmentEngine.cs` | 1 |
| `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` | 1 |

### nativeApiRiskRuntimeOutRefMutableView

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Gameplay/AirlockPressurization/AirlockPressurizationVault.cs` | 2 |
| `Assets/_Project/Scripts/PlayerInventory.cs` | 2 |
| `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 2 |
| `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs` | 2 |
| `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs` | 1 |

### nativeApiRiskRuntimeReturnMutableView

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 81 |
| `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` | 24 |
| `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs` | 15 |
| `Assets/_Project/Scripts/Core/NativeArenaArray.cs` | 1 |
| `Assets/_Project/Scripts/Inventory/SoaInventoryQueryEngine.cs` | 1 |

### nativeCollectionPublicMutableApiExposure

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 81 |
| `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` | 25 |
| `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs` | 20 |
| `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` | 4 |
| `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` | 4 |

### noAlias

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` | 73 |
| `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs` | 60 |
| `Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs` | 57 |
| `Assets/_Project/Scripts/PDA/CartographyGridJobs.cs` | 43 |
| `Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorTypes.cs` | 42 |

### privateNativeBuildEditorOnly

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchBakePipeline.cs` | 9 |
| `Assets/_Project/Scripts/Editor/GeologyForge/TopographyForgeGenerator.cs` | 5 |
| `Assets/_Project/Scripts/Physics/KCC/Editor/Shinobu355KccSmokeEditorFacade.cs` | 3 |
| `Assets/_Project/Scripts/World/BiotaDensityMapBaker/Editor/BiotaDensityBakePipeline.cs` | 3 |
| `Assets/_Project/Scripts/Editor/ChroniclerDiagnosticHeatmapWindow.cs` | 2 |

### privateNativeBuildPlayerRuntime

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | 46 |
| `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs` | 34 |
| `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 32 |
| `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs` | 25 |
| `Assets/_Project/Scripts/Core/DodReplayRecorder.cs` | 15 |

### privateNativeBuildQaDevProof

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/QA/Headless/JacobiStressFuzzer/PowerGridJacobiStressFuzzer.cs` | 20 |
| `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs` | 2 |
| `Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs` | 1 |
| `Assets/_Project/Scripts/Dev/HabitatStressSmokeTester.cs` | 1 |
| `Assets/_Project/Scripts/SaveSystem/WalIntegrityFuzzerCore_SHINOBU357.cs` | 1 |

### privateNativeCollectionBlackBoxTelemetry

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/EncounterDirector.cs` | 3 |
| `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs` | 3 |
| `Assets/_Project/Scripts/SaveManager.cs` | 3 |
| `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` | 2 |
| `Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs` | 2 |

### privateNativeCollectionField

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | 46 |
| `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs` | 34 |
| `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 32 |
| `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs` | 25 |
| `Assets/_Project/Scripts/QA/Headless/JacobiStressFuzzer/PowerGridJacobiStressFuzzer.cs` | 20 |

### privateNativeCollectionOwnerLocalScratch

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Core/BurstCallback.cs` | 2 |
| `Assets/_Project/Scripts/Fauna/FaunaSimulationEngine.cs` | 2 |
| `Assets/_Project/Scripts/SaveManager.cs` | 2 |
| `Assets/_Project/Scripts/ConstructionManager.cs` | 1 |
| `Assets/_Project/Scripts/Core/DodReplayRecorder.cs` | 1 |

### privateNativeCollectionStaticQueueLane

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs` | 7 |
| `Assets/_Project/Scripts/Gameplay/PlayerSignalEvents.cs` | 6 |
| `Assets/_Project/Scripts/Gameplay/RandomEventSystem.cs` | 6 |
| `Assets/_Project/Scripts/MapMagicBridge.cs` | 4 |
| `Assets/_Project/Scripts/AtlasSignal/Atlas6DirectiveSystem.cs` | 2 |

### privateNativeCollectionUnclassified

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | 45 |
| `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs` | 34 |
| `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 32 |
| `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs` | 21 |
| `Assets/_Project/Scripts/QA/Headless/JacobiStressFuzzer/PowerGridJacobiStressFuzzer.cs` | 17 |

### privateNativeDeclarationAmbiguous

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | 45 |
| `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs` | 33 |
| `Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs` | 6 |
| `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | 4 |
| `Assets/_Project/Scripts/Core/Scheduling/BurstTokenBucketJobAdmissionService.cs` | 3 |

### privateNativeDeclarationField

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs` | 24 |
| `Assets/_Project/Scripts/QA/Headless/JacobiStressFuzzer/PowerGridJacobiStressFuzzer.cs` | 20 |
| `Assets/_Project/Scripts/Core/DodReplayRecorder.cs` | 14 |
| `Assets/_Project/Scripts/EncounterDirector.cs` | 14 |
| `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs` | 13 |

### privateNativeDeclarationMethodReturn

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 32 |
| `Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs` | 12 |
| `Assets/_Project/Scripts/Core/Bucketing/ModuloSimulationBucketer.cs` | 8 |
| `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | 8 |
| `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs` | 6 |

### privateNativeRiskEditorOrProofNativeState

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/QA/Headless/JacobiStressFuzzer/PowerGridJacobiStressFuzzer.cs` | 20 |
| `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchBakePipeline.cs` | 9 |
| `Assets/_Project/Scripts/Physics/KCC/Editor/Shinobu355KccSmokeEditorFacade.cs` | 3 |
| `Assets/_Project/Scripts/Editor/ChroniclerDiagnosticHeatmapWindow.cs` | 2 |
| `Assets/_Project/Scripts/Editor/SignalTrafficMonitorWindow.cs` | 2 |

### privateNativeRiskMethodReturningNativeCollection

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 32 |
| `Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs` | 12 |
| `Assets/_Project/Scripts/Core/Bucketing/ModuloSimulationBucketer.cs` | 8 |
| `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | 8 |
| `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs` | 6 |

### privateNativeRiskOwnerLocalRuntimeNativeState

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Core/DodReplayRecorder.cs` | 14 |
| `Assets/_Project/Scripts/EncounterDirector.cs` | 14 |
| `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs` | 13 |
| `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs` | 13 |
| `Assets/_Project/Scripts/SaveManager.cs` | 12 |

### privateNativeRiskStaticGlobalNativeState

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs` | 22 |
| `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` | 10 |
| `Assets/_Project/Scripts/LocRegistry.cs` | 7 |
| `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime_StatusEffects.cs` | 6 |
| `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs` | 4 |

### privateNativeRiskStaticSignalOrEventBridge

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs` | 7 |
| `Assets/_Project/Scripts/Gameplay/PlayerSignalEvents.cs` | 6 |
| `Assets/_Project/Scripts/Gameplay/RandomEventSystem.cs` | 6 |
| `Assets/_Project/Scripts/MapMagicBridge.cs` | 4 |
| `Assets/_Project/Scripts/AtlasSignal/Atlas6DirectiveSystem.cs` | 2 |

### privateNativeRiskUnclassifiedNativeCollection

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs` | 6 |
| `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | 4 |
| `Assets/_Project/Scripts/Core/Scheduling/BurstTokenBucketJobAdmissionService.cs` | 3 |
| `Assets/_Project/Scripts/Core/Bucketing/ModuloSimulationBucketer.cs` | 1 |
| `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 1 |

### privateNativeRiskVaultAliasOrVaultResolver

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | 45 |
| `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs` | 33 |
| `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` | 1 |

### unityTimeBuildEditorOnly

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Editor/CodexPlayModeLauncher.cs` | 5 |
| `Assets/_Project/Scripts/Editor/SignalTrafficMonitorWindow.cs` | 3 |
| `Assets/_Project/Scripts/Core/Diagnostics/Visuals/Editor/ArchitectEyeBlackBoxTimelineViewer.cs` | 1 |
| `Assets/_Project/Scripts/Editor/ConstructionSocketEditorTools.cs` | 1 |
| `Assets/_Project/Scripts/Editor/CraftingFastFailXRayWindow_SHINOBU317.cs` | 1 |

### unityTimeBuildPlayerRuntime

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` | 2 |
| `Assets/_Project/Scripts/Fabricator.cs` | 2 |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 2 |
| `Assets/_Project/Scripts/Plugins/MapMagic/MapMagicRuntimeBridge.cs` | 2 |
| `Assets/_Project/Scripts/World/FloraBrain.cs` | 2 |

### unityTimeBuildQaDevProof

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Dev/CelestialTimeLapseDebugger.cs` | 1 |

### unityTimeCritical

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Editor/CodexPlayModeLauncher.cs` | 5 |
| `Assets/_Project/Scripts/Editor/SignalTrafficMonitorWindow.cs` | 3 |
| `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` | 2 |
| `Assets/_Project/Scripts/Fabricator.cs` | 2 |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 2 |

### unityTimeDelta

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Dev/CelestialTimeLapseDebugger.cs` | 1 |

### unityTimeFrameCount

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Editor/CodexPlayModeLauncher.cs` | 5 |
| `Assets/_Project/Scripts/Editor/SignalTrafficMonitorWindow.cs` | 3 |
| `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` | 2 |
| `Assets/_Project/Scripts/Fabricator.cs` | 2 |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 2 |

### unityTimeRiskCooldownOrPerfLog

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Plugins/MapMagic/MapMagicRuntimeBridge.cs` | 2 |
| `Assets/_Project/Scripts/World/FloraBrain.cs` | 2 |

### unityTimeRiskEditorOrProof

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Editor/CodexPlayModeLauncher.cs` | 5 |
| `Assets/_Project/Scripts/Editor/SignalTrafficMonitorWindow.cs` | 3 |
| `Assets/_Project/Scripts/Core/Diagnostics/Visuals/Editor/ArchitectEyeBlackBoxTimelineViewer.cs` | 1 |
| `Assets/_Project/Scripts/Dev/CelestialTimeLapseDebugger.cs` | 1 |
| `Assets/_Project/Scripts/Editor/ConstructionSocketEditorTools.cs` | 1 |

### unityTimeRiskFrameStampOrTelemetry

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` | 2 |
| `Assets/_Project/Scripts/Fabricator.cs` | 2 |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 2 |
| `Assets/_Project/Scripts/Input/ControlRemapper.cs` | 1 |
| `Assets/_Project/Scripts/World/FloraInteractionManager.cs` | 1 |

### unityTimeWallClock

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Plugins/MapMagic/MapMagicRuntimeBridge.cs` | 2 |
| `Assets/_Project/Scripts/World/FloraBrain.cs` | 2 |

### unityUpdateMethod

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/AI/Cognition/Editor/AIAnxietyTunerWindow.cs` | 1 |
| `Assets/_Project/Scripts/AI/Cognition/Editor/CognitionUtilityTunerWindow.cs` | 1 |
| `Assets/_Project/Scripts/Editor/FloraDearLieXRayWindow.cs` | 1 |
| `Assets/_Project/Scripts/Editor/SubmarineDynoTunerWindow.cs` | 1 |
| `Assets/_Project/Scripts/Editor/SumpPumpPipeGridTunerWindow.cs` | 1 |

## Interpretation

- `Pack=1`, private persistent native collections, and Burst attribute drift are platform-portability risks until each hit is classified as cold file-format, owner-local scratch, or hot runtime.
- `jobHandleComplete`, Unity `Update` methods, `Time.*`, and `UnityEngine.Random` are not automatically defects, but they are mandatory review surfaces for gameplay/runtime code.
- Binary hardware switches are suspect unless they are presentation-only or build-time/platform setup. Runtime scalability should flow through continuous `GlobalQualityWeight` curves.
- This audit is a pressure map. It does not mutate code and does not prove frame cost.
