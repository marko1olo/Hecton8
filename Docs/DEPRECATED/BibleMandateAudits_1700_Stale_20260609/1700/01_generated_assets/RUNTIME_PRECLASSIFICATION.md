# Runtime Preclassification - Generated Meshes, Textures, Materials, LOD, Collision

Status: HEURISTIC FIRST PASS - MANUAL REVIEW STILL REQUIRED
Date: 2026-06-02

This file groups static runtime suspects by a conservative heuristic. It can reduce review time, but it cannot prove a line is legal or illegal without reading the containing method and owner phase.

Raw generated runtime suspects: 250. Line-level reconciliation: 249 classified lines in `LINE_LEVEL_CLASSIFICATION.md`.

## Summary

- LEGAL_EDITOR_OR_DEV_GUARDED: 105
- REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 73
- LIKELY_LEGAL_COLD_OR_OWNER_LIFETIME_PATH: 50
- REVIEW_CACHE_OR_INJECTION_REQUIRED: 9
- REVIEW_RUNTIME_MESH_MATERIAL_PATH: 7
- LIKELY_LEGAL_COLD_OR_DIAGNOSTIC_PATH: 2
- REVIEW_HOT_PHASE_METHOD: 2
- REVIEW_LOG_GUARD_REQUIRED: 1
- LIKELY_LEGAL_COLD_LOOKUP: 1

## LEGAL_EDITOR_OR_DEV_GUARDED (105)

- Runtime debug logging | Assets\_Project\Scripts\World\AbyssalFluidDecalManager.cs:943:                    Hecton8.Core.H8Debug.LogError("[AbyssalFluidDecalManager] Missing decalMaterial asset. Runtime material creation is forbidden for this draw path.", this);
- Runtime debug logging | Assets\_Project\Scripts\World\BioCableIK.cs:998:                Hecton8.Core.H8Debug.LogError("[BioCableIK] Missing cableMaterial asset. Runtime material creation is forbidden for cable rendering.", this);
- Runtime debug logging | Assets\_Project\Scripts\World\BiomeTransitionSmokeTester.cs:45:                Hecton8.Core.H8Debug.Log(
- Runtime debug logging | Assets\_Project\Scripts\World\BoidStructValidator.cs:58:                Hecton8.Core.H8Debug.LogError(failureMessage);
- Runtime debug logging | Assets\_Project\Scripts\World\BoidStructValidator.cs:67:                Hecton8.Core.H8Debug.Log(
- Runtime debug logging | Assets\_Project\Scripts\World\AbyssalThermalManager.cs:1068:                Hecton8.Core.H8Debug.LogError("[AbyssalThermalManager] Duplicate instance detected. Destroying the newer component.", this);
- Runtime debug logging | Assets\_Project\Scripts\World\AbyssalThermalManager.cs:4345:                H8Debug.LogError("[AbyssalThermalManager] Missing bioCableMaterial asset. BioCableIK rigs are disabled until an authored material is assigned.", this);
- Runtime debug logging | Assets\_Project\Scripts\World\DynamicResolutionScaler.cs:249:                Hecton8.Core.H8Debug.LogWarning("[DynamicResolutionScaler] Duplicate instance detected. Destroying duplicate.");
- Runtime debug logging | Assets\_Project\Scripts\World\DynamicResolutionScaler.cs:262:                Hecton8.Core.H8Debug.LogError("[DynamicResolutionScaler] UniversalRenderPipeline.asset is null. Dynamic resolution disabled.");
- Runtime debug logging | Assets\_Project\Scripts\World\DynamicResolutionScaler.cs:291:            Hecton8.Core.H8Debug.Log("[DynamicResolutionScaler] Initialized.");
- Runtime debug logging | Assets\_Project\Scripts\World\Biolum\HectonBiolumZone.cs:454:            if (_debugLogSpawn) H8Debug.Log("[Biolum] light spawned");
- Runtime debug logging | Assets\_Project\Scripts\World\Biomes\BiomeTransitionManagerRuntime.cs:1414:                Hecton8.Core.H8Debug.LogWarning("[BiomeTransitionManagerRuntime] CSV load failed: " + exception.Message, this);
- Runtime debug logging | Assets\_Project\Scripts\World\Biomes\BiomeTransitionManagerRuntime.cs:1969:                Hecton8.Core.H8Debug.LogError("[BiomeTransitionManagerRuntime] Black-box dump failed: " + exception.Message);
- Runtime debug logging | Assets\_Project\Scripts\World\Biomes\BiomeBoundarySdfRuntimeBootstrap.cs:48:            Hecton8.Core.H8Debug.LogWarning(
- Runtime debug logging | Assets\_Project\Scripts\World\DepthZoneDirector.cs:691:            H8Debug.Log("[DepthZone] Entered.");
- Runtime debug logging | Assets\_Project\Scripts\World\FaunaSpatialHashRegistry.cs:670:                Hecton8.Core.H8Debug.LogError("[FaunaSpatialHashRegistry] Entry capacity exceeded. Runtime registry growth is forbidden.");
- Runtime debug logging | Assets\_Project\Scripts\World\ChemicalInfluenceGrid.cs:2899:                Hecton8.Core.H8Debug.LogError("[SHINOBU_138] Chemical DTO layout validation failed. ARM64 padding contract broken.");
- Runtime debug logging | Assets\_Project\Scripts\World\CullingManager.cs:215:                Hecton8.Core.H8Debug.LogWarning("[CullingManager] Duplicate instance detected. Destroying duplicate.");
- Runtime debug logging | Assets\_Project\Scripts\World\CullingManager.cs:222:            Hecton8.Core.H8Debug.Log("[CullingManager] Initialized.");
- Runtime debug logging | Assets\_Project\Scripts\World\CullingManager.cs:552:                Hecton8.Core.H8Debug.LogWarning("[CullingManager] Skipping registration for object without Renderer owner.", obj);
- Runtime debug logging | Assets\_Project\Scripts\World\CullingManager.cs:569:                    Hecton8.Core.H8Debug.LogWarning("[CullingManager] Registering object with disabled Renderer.", obj);
- Runtime debug logging | Assets\_Project\Scripts\World\CullingManager.cs:575:                    Hecton8.Core.H8Debug.LogWarning("[CullingManager] Registering object with externally force-culled Renderer.", obj);
- Runtime debug logging | Assets\_Project\Scripts\World\CullingManager.cs:665:                    Hecton8.Core.H8Debug.LogWarning("[CullingManager] Cannot apply layer cull distances: runtime camera is unresolved.");
- Runtime debug logging | Assets\_Project\Scripts\World\CullingManager.cs:688:            Hecton8.Core.H8Debug.Log("[CullingManager] Layer cull distances applied.");
- Runtime debug logging | Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:1694:                Hecton8.Core.H8Debug.LogException(exception);
- Runtime debug logging | Assets\_Project\Scripts\World\HectonSpatialHash.cs:1225:                Hecton8.Core.H8Debug.LogError("[HectonSpatialHash] Handle allocator exhausted.");
- Runtime debug logging | Assets\_Project\Scripts\World\HectonMapMagicVegetationBridge.cs:5392:            Hecton8.Core.H8Debug.Log(
- Runtime debug logging | Assets\_Project\Scripts\World\HectonMapMagicVegetationBridge.cs:5402:            Hecton8.Core.H8Debug.LogError("[HectonMapMagicVegetationBridge] Loop guard hit.");
- Runtime debug logging | Assets\_Project\Scripts\World\FloraInteractionManager.cs:8127:                Hecton8.Core.H8Debug.LogError("[FloraInteractionManager] Missing wake trail compute shader. Expected Hecton_VegetationWakeTrailSim.compute.", this);
- Runtime debug logging | Assets\_Project\Scripts\World\HectonIndirectVegetationRenderer.cs:1688:                Hecton8.Core.H8Debug.LogError("[HectonIndirectVegetationRenderer] Material is required and fallback shader resolution failed.", this);
- Runtime debug logging | Assets\_Project\Scripts\World\HectonIndirectVegetationRenderer.cs:1712:                Hecton8.Core.H8Debug.LogError("[HectonIndirectVegetationRenderer] No near render mesh resolved.", this);
- Runtime debug logging | Assets\_Project\Scripts\World\LODSystemManager.cs:214:                Hecton8.Core.H8Debug.LogWarning("[LODSystemManager] Duplicate registry owner detected. Destroying duplicate.");
- Runtime debug logging | Assets\_Project\Scripts\World\LODSystemManager.cs:234:            Hecton8.Core.H8Debug.Log("[LODSystemManager] Initialized. Max LOD groups: " + _maxLODGroupsPerFrame);
- Runtime debug logging | Assets\_Project\Scripts\World\LODSystemManager.cs:565:                Hecton8.Core.H8Debug.LogWarning("[LODSystemManager] Invalid quality preset value. Using default (Medium).");
- Runtime debug logging | Assets\_Project\Scripts\World\LODSystemManager.cs:598:                Hecton8.Core.H8Debug.LogWarning("[LODSystemManager] Registered LOD groups exceeds max capacity. Consider increasing capacity.");
- Runtime debug logging | Assets\_Project\Scripts\World\ImpostorSystem.cs:261:                Hecton8.Core.H8Debug.LogError("[ImpostorSystem] DTO layout validation failed. Disabling impostor runtime.");
- Runtime debug logging | Assets\_Project\Scripts\World\ImpostorSystem.cs:271:                Hecton8.Core.H8Debug.LogWarning("[ImpostorSystem] Duplicate registry owner detected. Destroying duplicate.");
- Runtime debug logging | Assets\_Project\Scripts\World\ImpostorSystem.cs:1393:                Hecton8.Core.H8Debug.Log($"[ImpostorSystem] Impostor shader found: {impostorShader.name}");
- Runtime debug logging | Assets\_Project\Scripts\World\ImpostorSystem.cs:1395:                Hecton8.Core.H8Debug.LogWarning("[ImpostorSystem] Amplify impostor package or shader not found.");
- Runtime debug logging | Assets\_Project\Scripts\World\ImpostorSystem.cs:1401:            Hecton8.Core.H8Debug.LogWarning("[ImpostorSystem] Amplify impostor bake preset creation is unavailable because the Amplify package is not installed.");
- Additional lines omitted here: 65. Use `../_scans/01_generated_assets_runtime_risks.txt` for the full list.

## REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED (73)

- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\Biomes\BiomeBoundarySdfRuntime.cs:629:            _sampleScratch.Result = new NativeArray<BiomeBoundarySdfResult>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\Biomes\BiomeBoundarySdfRuntime.cs:631:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:687:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:692:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:697:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:702:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:707:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:712:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:717:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:1178:                    Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\GPUScatterDirector.cs:2257:            _visibleCountReadback.Data = new NativeArray<uint>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\GPUScatterDirector.cs:2259:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonMapMagicVegetationBridge.cs:4816:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonMapMagicVegetationBridge.cs:5598:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonMapMagicVegetationBridge.cs:8598:            state.HeightReadbackData = new NativeArray<ushort>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonMapMagicVegetationBridge.cs:8600:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonHLODRenderer.cs:288:                using (NativeArray<MetadataValue> batchMetadata = new NativeArray<MetadataValue>(BrgMetadataPlaceholderCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonDistantLandmarkRenderer.cs:422:                using (NativeArray<MetadataValue> batchMetadata = new NativeArray<MetadataValue>(BrgMetadataPlaceholderCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonIndirectVegetationRenderer.cs:2524:            NativeArray<MetadataValue> batchMetadata = new NativeArray<MetadataValue>(BrgMetadataPlaceholderCount, TransientVegetationCullingAllocator);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonIndirectVegetationRenderer.cs:4471:            _cullTelemetryReadback.Data = new NativeArray<uint>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonIndirectVegetationRenderer.cs:4473:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonIndirectVegetationRenderer.cs:5127:            NativeArray<byte> visibilityMask = new NativeArray<byte>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\VegetationFlowFieldIntegrator.cs:292:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\VegetationFlowFieldIntegrator.cs:691:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\VegetationFlowFieldIntegrator.cs:696:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\VegetationFlowFieldIntegrator.cs:701:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\VegetationFlowFieldIntegrator.cs:706:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\VegetationFlowFieldIntegrator.cs:711:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\VegetationFlowFieldIntegrator.cs:716:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\VegetationFlowFieldIntegrator.cs:997:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\VegetationFlowFieldIntegrator.cs:1038:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\VegetationFlowFieldIntegrator.cs:1134:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\VegetationFlowFieldIntegrator.cs:1139:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\VegetationFlowFieldIntegrator.cs:1246:                    Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\VegetationChunkResidencyDirector.cs:483:                    Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\VegetationChunkResidencyDirector.cs:789:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\VegetationChunkResidencyDirector.cs:794:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\VegetationChunkResidencyDirector.cs:799:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\TOOL_Procedural_Wreckage_Generator.cs:72:                Grid = new NativeArray<GridCell>(safeCellCount, allocator, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\TOOL_Procedural_Wreckage_Generator.cs:74:                RngState = new NativeArray<uint4>(1, allocator, NativeArrayOptions.ClearMemory);
- Additional lines omitted here: 33. Use `../_scans/01_generated_assets_runtime_risks.txt` for the full list.

## LIKELY_LEGAL_COLD_OR_OWNER_LIFETIME_PATH (50)

- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\BiomeTransitionSmokeTester.cs:94:                samples = new NativeArray<BiomeTransitionSample>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\BiomeTransitionSmokeTester.cs:95:                sources = new NativeArray<BiomeTransitionFogSource>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\BiomeTransitionSmokeTester.cs:99:                fromAup = new NativeArray<AbsoluteUniversePositionBlit128>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\BiomeTransitionSmokeTester.cs:100:                toAup = new NativeArray<AbsoluteUniversePositionBlit128>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\BiomeTransitionSmokeTester.cs:101:                playerAup = new NativeArray<AbsoluteUniversePositionBlit128>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\BiomeTransitionSmokeTester.cs:102:                results = new NativeArray<BiomeTransitionFogResult>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\AbyssalThermalManager.cs:806:                NativeArray<float> scratch = new NativeArray<float>(requiredLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\AbyssalThermalManager.cs:2316:                dumpBytes = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\Biomes\BiomeTransitionManagerRuntime.cs:1519:                using NativeArray<byte> payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\Biomes\BiomeTransitionManagerRuntime.cs:1947:                using NativeArray<byte> payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\Biomes\BiomeBoundarySdfRuntime.cs:828:                    payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\ChemicalInfluenceGrid.cs:2132:                payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonSandboxAbyssalShelfSmokeTester.cs:106:                positions = new NativeArray<AbsoluteUniversePosition>(SampleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonSandboxAbyssalShelfSmokeTester.cs:107:                samples = new NativeArray<HectonSandboxAbyssalShelfAuditSample>(SampleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonSandboxAbyssalShelfSmokeTester.cs:108:                reductions = new NativeArray<HectonSandboxAbyssalShelfSampleReduction>(SampleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonSandboxAbyssalShelfSmokeTester.cs:109:                summary = new NativeArray<HectonSandboxAbyssalShelfSmokeSummary>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonIndirectVegetationRenderer.cs:1521:            NativeArray<Matrix4x4> matrices = new NativeArray<Matrix4x4>(count, TransientVegetationCullingAllocator, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonIndirectVegetationRenderer.cs:1522:            NativeArray<HectonVegetationInstanceData> instanceData = new NativeArray<HectonVegetationInstanceData>(count, TransientVegetationCullingAllocator, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\Biolum\HectonBiolumManager.cs:1472:                NativeArray<byte> payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\TerrainChunkPagerRuntime.cs:2346:                NativeArray<byte> payload = new NativeArray<byte>(totalBytes, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\Resources\WorldRegrowthSimulation.cs:631:                NativeArray<byte> payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PlanetaryCanvasSmokeTester.cs:36:                heights = new NativeArray<float>(CellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PlanetaryCanvasSmokeTester.cs:37:                sediment = new NativeArray<float>(CellCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PlanetaryCanvasSmokeTester.cs:38:                weights = new NativeArray<float4>(CellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PlanetaryCanvasSmokeTester.cs:39:                slopeWeights = new NativeArray<float>(CellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\Resources\ProceduralOreSpawner.cs:3307:            NativeArray<byte> payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PersistentWorldRegistry.cs:5319:                desiredSectorHashView = new NativeArray<long>(PagedSectorHashCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PersistentWorldRegistry.cs:6489:                    NativeArray<PersistentWorldDeltaRecord> sectorRecords = new NativeArray<PersistentWorldDeltaRecord>(bucketCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PersistentWorldRegistry.cs:6567:            NativeArray<EntityDataRecord> sectorStates = new NativeArray<EntityDataRecord>(stateCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PersistentWorldRegistry.cs:8045:            NativeArray<EntityDataRecord> sectorStates = new NativeArray<EntityDataRecord>(entityStates.Count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\VegetationNavGridSynchronizer.cs:2433:            NativeArray<byte> payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\VegetationMemorySovereigntyRuntime.cs:510:                payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PcieBandwidthGuard1411SelfTest.cs:37:            NativeArray<Matrix4x4> matrices = new NativeArray<Matrix4x4>(MatrixInstanceCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PcieBandwidthGuard1411SelfTest.cs:38:            NativeArray<byte> dirtyPages = new NativeArray<byte>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PcieBandwidthGuard1411SelfTest.cs:75:            NativeArray<byte> dirtyPages = new NativeArray<byte>(4, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PcieBandwidthGuard1411SelfTest.cs:103:            NativeArray<byte> matrixDirtyPages = new NativeArray<byte>(4, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PcieBandwidthGuard1411SelfTest.cs:104:            NativeArray<byte> metadataDirtyPages = new NativeArray<byte>(4, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PcieBandwidthGuard1411SelfTest.cs:146:            NativeArray<int> source = new NativeArray<int>(FuzzerInstanceCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PcieBandwidthGuard1411SelfTest.cs:147:            NativeArray<int> bufferA = new NativeArray<int>(FuzzerInstanceCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PcieBandwidthGuard1411SelfTest.cs:148:            NativeArray<int> bufferB = new NativeArray<int>(FuzzerInstanceCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Additional lines omitted here: 10. Use `../_scans/01_generated_assets_runtime_risks.txt` for the full list.

## REVIEW_CACHE_OR_INJECTION_REQUIRED (9)

- Unity scene lookup | Assets\_Project\Scripts\World\HectonWorldShellController1428.cs:25:            if (cameraRig == null && Camera.main != null)
- Unity scene lookup | Assets\_Project\Scripts\World\HectonWorldShellController1428.cs:26:                cameraRig = Camera.main.transform;
- Unity scene lookup | Assets\_Project\Scripts\World\ImpostorSystem.cs:1215:            obj.transform.GetComponentsInChildren(true, _rendererScratch);
- Unity scene lookup | Assets\_Project\Scripts\World\ImpostorSystem.cs:1413:                if (prefab == null || !prefab.TryGetComponent<LODGroup>(out _))
- Unity scene lookup | Assets\_Project\Scripts\World\ProceduralWreckGenerator.cs:6897:            MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
- Unity scene lookup | Assets\_Project\Scripts\World\ResourceDistributionDirector.cs:1378:            if (_authoredOrePrefab != null && _authoredOrePrefab.GetComponent<ResourceNode>() == null)
- Unity scene lookup | Assets\_Project\Scripts\Fauna\CreatureDamageManager.cs:229:            GetComponentsInChildren(true, _rendererScratch);
- Unity scene lookup | Assets\_Project\Scripts\Fauna\FaunaBrain.cs:4438:            GetComponentsInChildren(true, _biolumPresentationLightScratch);
- Unity scene lookup | Assets\_Project\Scripts\Fauna\FaunaBrain.cs:7382:            GetComponentsInChildren(true, _logicalLodColliderScratch);

## REVIEW_RUNTIME_MESH_MATERIAL_PATH (7)

- Runtime mesh/material mutation | Assets\_Project\Scripts\World\FloraDataTemplate.cs:186:        [Tooltip("Local-space bounds size used by proxy generation and primitive collider fitting. MeshColliders remain forbidden.")]
- Runtime mesh/material mutation | Assets\_Project\Scripts\World\ProceduralWreckGenerator.cs:5660:                result = new Mesh();
- Runtime mesh/material mutation | Assets\_Project\Scripts\World\ProceduralWreckGenerator.cs:5866:                result = new Mesh();
- Runtime mesh/material mutation | Assets\_Project\Scripts\World\ProceduralWreckGenerator.cs:5988:                result = new Mesh();
- Unity scene lookup | Assets\_Project\Scripts\World\ProceduralWreckGenerator.cs:6880:            MeshCollider[] meshColliders = root.GetComponentsInChildren<MeshCollider>(true);
- Runtime mesh/material mutation | Assets\_Project\Scripts\World\ProceduralWreckGenerator.cs:6883:                MeshCollider meshCollider = meshColliders[i];
- Runtime mesh/material mutation | Assets\_Project\Scripts\Fauna\FaunaBrain.cs:7704:            MeshCollider meshCollider = ComponentReferenceUtility.ResolveOwnedComponent<MeshCollider>(transform);

## LIKELY_LEGAL_COLD_OR_DIAGNOSTIC_PATH (2)

- Runtime debug logging | Assets\_Project\Scripts\World\PcieBandwidthGuard1411SelfTest.cs:32:            Debug.Log("Agent 1411 PCIe bandwidth guard self test passed.");
- Runtime debug logging | Assets\_Project\Scripts\Rendering\Scatter\AbyssalScatterBrgDataVaultBootstrap.cs:606:            Debug.LogWarning(message, context);

## REVIEW_HOT_PHASE_METHOD (2)

- Hot Unity phase method | Assets\_Project\Scripts\World\HectonWorldShellVisualDriver1428.cs:28:        private void LateUpdate()
- Hot Unity phase method | Assets\_Project\Scripts\World\HectonWorldShellController1428.cs:33:        private void Update()

## REVIEW_LOG_GUARD_REQUIRED (1)

- Runtime debug logging | Assets\_Project\Scripts\Fauna\PredatorCognitionDomain_Steering.cs:2041:            Debug.Log("[OOP_Movement_Scanner] scanned Update scopes=" + updateScopes.ToString() +

## LIKELY_LEGAL_COLD_LOOKUP (1)

- Unity scene lookup | Assets\_Project\Scripts\World\WorldLODSceneBootstrap.cs:107:                root.GetComponentsInChildren(_includeInactiveLODGroups, _sceneLODGroupBuffer);

