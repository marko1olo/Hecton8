# Polish Mandate Static Audit

Evidence class: STATIC_SOURCE. No Unity import, compile, Play Mode, profiler, GC, memory, player build, or device proof was executed.

- Schema: `hecton8.polish_mandate_static_audit.v1`
- Source root: `Assets/_Project/Scripts/World`
- C# files: `276`

## Counts

| Category | Matches | Files |
|---|---:|---:|
| `binaryHardwareSwitch` | 0 | 0 |
| `burstCompile` | 308 | 74 |
| `burstMissingCompileSynchronously` | 0 | 0 |
| `burstMissingFloatMode` | 0 | 0 |
| `burstMissingFloatPrecision` | 0 | 0 |
| `globalQualityWeight` | 329 | 87 |
| `jobHandleComplete` | 45 | 13 |
| `linqSurface` | 0 | 0 |
| `nativeApiExposureAmbiguousMutable` | 0 | 0 |
| `nativeApiExposureBuildEditorOnly` | 0 | 0 |
| `nativeApiExposureBuildPlayerRuntime` | 26 | 3 |
| `nativeApiExposureBuildQaDevProof` | 0 | 0 |
| `nativeApiExposureMutableReturn` | 22 | 2 |
| `nativeApiExposureOutRefMutable` | 4 | 2 |
| `nativeApiExposurePrivateNestedSuppressed` | 10 | 3 |
| `nativeApiRiskCoreVaultOrAllocatorSurface` | 0 | 0 |
| `nativeApiRiskEditorOrProofSurface` | 0 | 0 |
| `nativeApiRiskRuntimeAmbiguousMutableView` | 0 | 0 |
| `nativeApiRiskRuntimeDiagnosticNamedMutableView` | 3 | 1 |
| `nativeApiRiskRuntimeOutRefMutableView` | 4 | 2 |
| `nativeApiRiskRuntimeReturnMutableView` | 19 | 2 |
| `nativeCollectionPublicMutableApiExposure` | 26 | 3 |
| `noAlias` | 493 | 44 |
| `packOne` | 0 | 0 |
| `privateNativeBuildEditorOnly` | 29 | 6 |
| `privateNativeBuildPlayerRuntime` | 99 | 24 |
| `privateNativeBuildQaDevProof` | 1 | 1 |
| `privateNativeCollectionBlackBoxTelemetry` | 8 | 8 |
| `privateNativeCollectionField` | 129 | 31 |
| `privateNativeCollectionOwnerLocalScratch` | 4 | 2 |
| `privateNativeCollectionStaticQueueLane` | 10 | 6 |
| `privateNativeCollectionUnclassified` | 107 | 24 |
| `privateNativeCollectionVaultAlias` | 0 | 0 |
| `privateNativeDeclarationAmbiguous` | 0 | 0 |
| `privateNativeDeclarationField` | 95 | 19 |
| `privateNativeDeclarationMethodReturn` | 34 | 12 |
| `privateNativeRiskEditorOrProofNativeState` | 25 | 4 |
| `privateNativeRiskJobStructNativeView` | 0 | 0 |
| `privateNativeRiskMethodReturningNativeCollection` | 34 | 12 |
| `privateNativeRiskOwnerLocalRuntimeNativeState` | 46 | 7 |
| `privateNativeRiskStaticGlobalNativeState` | 14 | 4 |
| `privateNativeRiskStaticSignalOrEventBridge` | 10 | 6 |
| `privateNativeRiskUnclassifiedNativeCollection` | 0 | 0 |
| `privateNativeRiskVaultAliasOrVaultResolver` | 0 | 0 |
| `structAutoProperties` | 0 | 0 |
| `unityRandom` | 0 | 0 |
| `unityTimeBuildEditorOnly` | 1 | 1 |
| `unityTimeBuildPlayerRuntime` | 3 | 2 |
| `unityTimeBuildQaDevProof` | 0 | 0 |
| `unityTimeCritical` | 4 | 3 |
| `unityTimeDelta` | 0 | 0 |
| `unityTimeFrameCount` | 2 | 2 |
| `unityTimeRiskCooldownOrPerfLog` | 2 | 1 |
| `unityTimeRiskEditorOrProof` | 1 | 1 |
| `unityTimeRiskFrameStampOrTelemetry` | 1 | 1 |
| `unityTimeRiskGameplayDelta` | 0 | 0 |
| `unityTimeRiskGameplayWallClock` | 0 | 0 |
| `unityTimeWallClock` | 2 | 1 |
| `unityUpdateMethod` | 0 | 0 |

## Top Files

### burstCompile

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs` | 15 |
| `Assets/_Project/Scripts/World/OfflineWreckageBaker/Editor/OfflineWreckageBakeJobs.cs` | 12 |
| `Assets/_Project/Scripts/World/VoxelTerrainSeamBinder/Editor/VoxelTerrainSeamBinderJobs.cs` | 12 |
| `Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs` | 11 |
| `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | 11 |

### globalQualityWeight

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs` | 20 |
| `Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs` | 15 |
| `Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralJobs.cs` | 14 |
| `Assets/_Project/Scripts/World/OfflineWreckageBaker/Editor/OfflineWreckageBakeJobs.cs` | 10 |
| `Assets/_Project/Scripts/World/OfflineWreckageBaker/Editor/WreckageForgeWindow.cs` | 10 |

### jobHandleComplete

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/OfflineHadalArchBaker/Editor/HadalArchBakePipeline.cs` | 9 |
| `Assets/_Project/Scripts/World/BiotaDensityMapBaker/Editor/BiotaDensityBakePipeline.cs` | 8 |
| `Assets/_Project/Scripts/World/StaticCaveSdfBaker/Editor/StaticCaveSdfBakePipeline.cs` | 6 |
| `Assets/_Project/Scripts/World/BiomeWeightMapBaker/Editor/BiomeWeightMapBakePipeline.cs` | 4 |
| `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchBakePipeline.cs` | 3 |

### nativeApiExposureBuildPlayerRuntime

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs` | 20 |
| `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs` | 4 |
| `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 2 |

### nativeApiExposureMutableReturn

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs` | 18 |
| `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs` | 4 |

### nativeApiExposureOutRefMutable

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 2 |
| `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs` | 2 |

### nativeApiExposurePrivateNestedSuppressed

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` | 5 |
| `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs` | 3 |
| `Assets/_Project/Scripts/World/EcosystemDirector.cs` | 2 |

### nativeApiRiskRuntimeDiagnosticNamedMutableView

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs` | 3 |

### nativeApiRiskRuntimeOutRefMutableView

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 2 |
| `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs` | 2 |

### nativeApiRiskRuntimeReturnMutableView

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs` | 15 |
| `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs` | 4 |

### nativeCollectionPublicMutableApiExposure

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs` | 20 |
| `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs` | 4 |
| `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 2 |

### noAlias

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralJobs.cs` | 39 |
| `Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs` | 38 |
| `Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageJobs.cs` | 35 |
| `Assets/_Project/Scripts/World/OfflineWreckageBaker/Editor/OfflineWreckageBakeJobs.cs` | 33 |
| `Assets/_Project/Scripts/World/VoxelTerrainSeamBinder/Editor/VoxelTerrainSeamBinderJobs.cs` | 31 |

### privateNativeBuildEditorOnly

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/OfflineHadalArchBaker/Editor/HadalArchBakePipeline.cs` | 13 |
| `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchBakePipeline.cs` | 9 |
| `Assets/_Project/Scripts/World/BiotaDensityMapBaker/Editor/BiotaDensityBakePipeline.cs` | 3 |
| `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchForgeWindow.cs` | 2 |
| `Assets/_Project/Scripts/World/BiomeWeightMapBaker/Editor/BiomeWeightMapBakePipeline.cs` | 1 |

### privateNativeBuildPlayerRuntime

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/FloraRegrowthDirector.cs` | 12 |
| `Assets/_Project/Scripts/World/AbyssalThermalManager.cs` | 9 |
| `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | 9 |
| `Assets/_Project/Scripts/World/WorldSpatialHashGrid.cs` | 9 |
| `Assets/_Project/Scripts/World/HectonSpatialHash.cs` | 8 |

### privateNativeBuildQaDevProof

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/VolumetricBiomeSmokeTester.cs` | 1 |

### privateNativeCollectionBlackBoxTelemetry

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/AbyssalThermalManager.cs` | 1 |
| `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` | 1 |
| `Assets/_Project/Scripts/World/OfflineHadalArchBaker/Editor/HadalArchBakePipeline.cs` | 1 |
| `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchBakePipeline.cs` | 1 |
| `Assets/_Project/Scripts/World/OfflineWreckageBaker/Editor/OfflineWreckageBlackBox.cs` | 1 |

### privateNativeCollectionField

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/OfflineHadalArchBaker/Editor/HadalArchBakePipeline.cs` | 13 |
| `Assets/_Project/Scripts/World/FloraRegrowthDirector.cs` | 12 |
| `Assets/_Project/Scripts/World/AbyssalThermalManager.cs` | 9 |
| `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchBakePipeline.cs` | 9 |
| `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | 9 |

### privateNativeCollectionOwnerLocalScratch

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/FloraRegrowthDirector.cs` | 2 |
| `Assets/_Project/Scripts/World/HectonSpatialHash.cs` | 2 |

### privateNativeCollectionStaticQueueLane

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/DepthZoneDirector.cs` | 2 |
| `Assets/_Project/Scripts/World/EmergencyServiceRelayEvents.cs` | 2 |
| `Assets/_Project/Scripts/World/SoundscapeSystem.cs` | 2 |
| `Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs` | 2 |
| `Assets/_Project/Scripts/World/HectonIndirectVegetationContracts.cs` | 1 |

### privateNativeCollectionUnclassified

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/OfflineHadalArchBaker/Editor/HadalArchBakePipeline.cs` | 12 |
| `Assets/_Project/Scripts/World/FloraRegrowthDirector.cs` | 10 |
| `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | 9 |
| `Assets/_Project/Scripts/World/WorldSpatialHashGrid.cs` | 9 |
| `Assets/_Project/Scripts/World/AbyssalThermalManager.cs` | 8 |

### privateNativeDeclarationField

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/OfflineHadalArchBaker/Editor/HadalArchBakePipeline.cs` | 13 |
| `Assets/_Project/Scripts/World/FloraRegrowthDirector.cs` | 12 |
| `Assets/_Project/Scripts/World/AbyssalThermalManager.cs` | 9 |
| `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchBakePipeline.cs` | 9 |
| `Assets/_Project/Scripts/World/WorldSpatialHashGrid.cs` | 9 |

### privateNativeDeclarationMethodReturn

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | 9 |
| `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs` | 6 |
| `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs` | 5 |
| `Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs` | 4 |
| `Assets/_Project/Scripts/World/BiotaDensityMapBaker/Editor/BiotaDensityBakePipeline.cs` | 3 |

### privateNativeRiskEditorOrProofNativeState

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/OfflineHadalArchBaker/Editor/HadalArchBakePipeline.cs` | 13 |
| `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchBakePipeline.cs` | 9 |
| `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchForgeWindow.cs` | 2 |
| `Assets/_Project/Scripts/World/OfflineWreckageBaker/Editor/OfflineWreckageBlackBox.cs` | 1 |

### privateNativeRiskMethodReturningNativeCollection

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | 9 |
| `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs` | 6 |
| `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs` | 5 |
| `Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs` | 4 |
| `Assets/_Project/Scripts/World/BiotaDensityMapBaker/Editor/BiotaDensityBakePipeline.cs` | 3 |

### privateNativeRiskOwnerLocalRuntimeNativeState

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/FloraRegrowthDirector.cs` | 12 |
| `Assets/_Project/Scripts/World/AbyssalThermalManager.cs` | 9 |
| `Assets/_Project/Scripts/World/HectonSpatialHash.cs` | 8 |
| `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs` | 6 |
| `Assets/_Project/Scripts/World/WreckMaterialRegistry.cs` | 6 |

### privateNativeRiskStaticGlobalNativeState

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/WorldSpatialHashGrid.cs` | 9 |
| `Assets/_Project/Scripts/World/ProxyLightRegistry.cs` | 3 |
| `Assets/_Project/Scripts/World/FaunaSpatialHashRegistry.cs` | 1 |
| `Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs` | 1 |

### privateNativeRiskStaticSignalOrEventBridge

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/DepthZoneDirector.cs` | 2 |
| `Assets/_Project/Scripts/World/EmergencyServiceRelayEvents.cs` | 2 |
| `Assets/_Project/Scripts/World/SoundscapeSystem.cs` | 2 |
| `Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs` | 2 |
| `Assets/_Project/Scripts/World/HectonIndirectVegetationContracts.cs` | 1 |

### unityTimeBuildEditorOnly

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/SeedShipAnomaly/Editor/SeedShipAnomalyTunerWindow.cs` | 1 |

### unityTimeBuildPlayerRuntime

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/FloraBrain.cs` | 2 |
| `Assets/_Project/Scripts/World/FloraInteractionManager.cs` | 1 |

### unityTimeCritical

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/FloraBrain.cs` | 2 |
| `Assets/_Project/Scripts/World/FloraInteractionManager.cs` | 1 |
| `Assets/_Project/Scripts/World/SeedShipAnomaly/Editor/SeedShipAnomalyTunerWindow.cs` | 1 |

### unityTimeFrameCount

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/FloraInteractionManager.cs` | 1 |
| `Assets/_Project/Scripts/World/SeedShipAnomaly/Editor/SeedShipAnomalyTunerWindow.cs` | 1 |

### unityTimeRiskCooldownOrPerfLog

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/FloraBrain.cs` | 2 |

### unityTimeRiskEditorOrProof

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/SeedShipAnomaly/Editor/SeedShipAnomalyTunerWindow.cs` | 1 |

### unityTimeRiskFrameStampOrTelemetry

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/FloraInteractionManager.cs` | 1 |

### unityTimeWallClock

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/World/FloraBrain.cs` | 2 |

## Interpretation

- `Pack=1`, private persistent native collections, and Burst attribute drift are platform-portability risks until each hit is classified as cold file-format, owner-local scratch, or hot runtime.
- `jobHandleComplete`, Unity `Update` methods, `Time.*`, and `UnityEngine.Random` are not automatically defects, but they are mandatory review surfaces for gameplay/runtime code.
- Binary hardware switches are suspect unless they are presentation-only or build-time/platform setup. Runtime scalability should flow through continuous `GlobalQualityWeight` curves.
- This audit is a pressure map. It does not mutate code and does not prove frame cost.
