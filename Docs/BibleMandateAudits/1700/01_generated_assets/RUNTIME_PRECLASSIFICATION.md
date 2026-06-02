# Runtime Preclassification - Generated Meshes, Textures, Materials, LOD, Collision

Status: HEURISTIC FIRST PASS - MANUAL REVIEW STILL REQUIRED
Date: 2026-06-02

This file groups static runtime suspects by a conservative heuristic. It can reduce review time, but it cannot prove a line is legal or illegal without reading the containing method and owner phase.

Total runtime suspects: 249.

## Summary

- LEGAL_EDITOR_OR_DEV_GUARDED: 101
- REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 75
- LIKELY_LEGAL_COLD_OR_OWNER_LIFETIME_PATH: 50
- REVIEW_RUNTIME_MESH_MATERIAL_PATH: 9
- REVIEW_CACHE_OR_INJECTION_REQUIRED: 8
- LIKELY_LEGAL_COLD_OR_DIAGNOSTIC_PATH: 2
- REVIEW_HOT_PHASE_METHOD: 2
- REVIEW_LOG_GUARD_REQUIRED: 1
- LIKELY_LEGAL_COLD_LOOKUP: 1

## LEGAL_EDITOR_OR_DEV_GUARDED (101)

- Runtime debug logging | Assets\_Project\Scripts\World\BiomeTransitionSmokeTester.cs:45:                Hecton8.Core.H8Debug.Log(
- Runtime debug logging | Assets\_Project\Scripts\World\BioCableIK.cs:708:                Hecton8.Core.H8Debug.LogError("[BioCableIK] Missing cableMaterial asset. Runtime material creation is forbidden for cable rendering.", this);
- Runtime debug logging | Assets\_Project\Scripts\World\ChemicalInfluenceGrid.cs:2899:                Hecton8.Core.H8Debug.LogError("[SHINOBU_138] Chemical DTO layout validation failed. ARM64 padding contract broken.");
- Runtime debug logging | Assets\_Project\Scripts\World\BoidStructValidator.cs:58:                Hecton8.Core.H8Debug.LogError(failureMessage);
- Runtime debug logging | Assets\_Project\Scripts\World\BoidStructValidator.cs:67:                Hecton8.Core.H8Debug.Log(
- Runtime debug logging | Assets\_Project\Scripts\World\Biolum\HectonBiolumZone.cs:454:            if (_debugLogSpawn) H8Debug.Log("[Biolum] light spawned");
- Runtime debug logging | Assets\_Project\Scripts\World\Biomes\BiomeTransitionManagerRuntime.cs:1414:                Hecton8.Core.H8Debug.LogWarning("[BiomeTransitionManagerRuntime] CSV load failed: " + exception.Message, this);
- Runtime debug logging | Assets\_Project\Scripts\World\Biomes\BiomeTransitionManagerRuntime.cs:1969:                Hecton8.Core.H8Debug.LogError("[BiomeTransitionManagerRuntime] Black-box dump failed: " + exception.Message);
- Runtime debug logging | Assets\_Project\Scripts\World\Biolum\HectonBiolumManager.cs:412:            if (_debugLogUpdates) H8Debug.Log("[BiolumManager] Registered zone");
- Runtime debug logging | Assets\_Project\Scripts\World\Biolum\HectonBiolumManager.cs:428:            if (_debugLogUpdates) H8Debug.Log("[BiolumManager] Unregistered zone");
- Runtime debug logging | Assets\_Project\Scripts\World\Biolum\HectonBiolumManager.cs:656:            if (_debugLogUpdates) H8Debug.Log("[BiolumManager] Initialized");
- Runtime debug logging | Assets\_Project\Scripts\World\Biomes\BiomeBoundarySdfRuntimeBootstrap.cs:48:            Hecton8.Core.H8Debug.LogWarning(
- Runtime debug logging | Assets\_Project\Scripts\World\DynamicResolutionScaler.cs:249:                Hecton8.Core.H8Debug.LogWarning("[DynamicResolutionScaler] Duplicate instance detected. Destroying duplicate.");
- Runtime debug logging | Assets\_Project\Scripts\World\DynamicResolutionScaler.cs:262:                Hecton8.Core.H8Debug.LogError("[DynamicResolutionScaler] UniversalRenderPipeline.asset is null. Dynamic resolution disabled.");
- Runtime debug logging | Assets\_Project\Scripts\World\DynamicResolutionScaler.cs:291:            Hecton8.Core.H8Debug.Log("[DynamicResolutionScaler] Initialized.");
- Runtime debug logging | Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:1667:                Hecton8.Core.H8Debug.LogException(exception);
- Runtime debug logging | Assets\_Project\Scripts\World\FaunaSpatialHashRegistry.cs:670:                Hecton8.Core.H8Debug.LogError("[FaunaSpatialHashRegistry] Entry capacity exceeded. Runtime registry growth is forbidden.");
- Runtime debug logging | Assets\_Project\Scripts\World\DepthZoneDirector.cs:691:            H8Debug.Log("[DepthZone] Entered.");
- Runtime debug logging | Assets\_Project\Scripts\World\CullingManager.cs:206:                Hecton8.Core.H8Debug.LogWarning("[CullingManager] Duplicate instance detected. Destroying duplicate.");
- Runtime debug logging | Assets\_Project\Scripts\World\CullingManager.cs:213:            Hecton8.Core.H8Debug.Log("[CullingManager] Initialized.");
- Runtime debug logging | Assets\_Project\Scripts\World\CullingManager.cs:537:                Hecton8.Core.H8Debug.LogWarning("[CullingManager] Skipping registration for object without Renderer owner.", obj);
- Runtime debug logging | Assets\_Project\Scripts\World\CullingManager.cs:552:                    Hecton8.Core.H8Debug.LogWarning("[CullingManager] Registering object with disabled Renderer.", obj);
- Runtime debug logging | Assets\_Project\Scripts\World\CullingManager.cs:558:                    Hecton8.Core.H8Debug.LogWarning("[CullingManager] Registering object with externally force-culled Renderer.", obj);
- Runtime debug logging | Assets\_Project\Scripts\World\CullingManager.cs:645:                    Hecton8.Core.H8Debug.LogWarning("[CullingManager] Cannot apply layer cull distances: runtime camera is unresolved.");
- Runtime debug logging | Assets\_Project\Scripts\World\CullingManager.cs:668:            Hecton8.Core.H8Debug.Log("[CullingManager] Layer cull distances applied.");
- Runtime debug logging | Assets\_Project\Scripts\World\ImpostorSystem.cs:213:                Hecton8.Core.H8Debug.LogWarning("[ImpostorSystem] Duplicate registry owner detected. Destroying duplicate.");
- Runtime debug logging | Assets\_Project\Scripts\World\ImpostorSystem.cs:1179:                Hecton8.Core.H8Debug.Log($"[ImpostorSystem] Impostor shader found: {impostorShader.name}");
- Runtime debug logging | Assets\_Project\Scripts\World\ImpostorSystem.cs:1181:                Hecton8.Core.H8Debug.LogWarning("[ImpostorSystem] Amplify impostor package or shader not found.");
- Runtime debug logging | Assets\_Project\Scripts\World\ImpostorSystem.cs:1187:            Hecton8.Core.H8Debug.LogWarning("[ImpostorSystem] Amplify impostor bake preset creation is unavailable because the Amplify package is not installed.");
- Runtime debug logging | Assets\_Project\Scripts\World\ImpostorSystem.cs:1208:            Hecton8.Core.H8Debug.Log($"[ImpostorSystem] Batch bake scan complete. Candidates={bakedCount}");
- Runtime debug logging | Assets\_Project\Scripts\World\FloraInteractionManager.cs:8127:                Hecton8.Core.H8Debug.LogError("[FloraInteractionManager] Missing wake trail compute shader. Expected Hecton_VegetationWakeTrailSim.compute.", this);
- Runtime debug logging | Assets\_Project\Scripts\World\HectonSpatialHash.cs:1225:                Hecton8.Core.H8Debug.LogError("[HectonSpatialHash] Handle allocator exhausted.");
- Runtime debug logging | Assets\_Project\Scripts\World\PersistentWorldRegistry.cs:2296:            H8Debug.Log("[1325] Persistent world memory sovereignty layout validator passed.");
- Runtime debug logging | Assets\_Project\Scripts\World\PersistentWorldRegistry.cs:3506:                Hecton8.Core.H8Debug.LogError("[PersistentWorldRegistry] Duplicate registry owner detected. Disabling duplicate.");
- Runtime debug logging | Assets\_Project\Scripts\World\PersistentWorldRegistry.cs:5199:                    Hecton8.Core.H8Debug.LogError(string.Concat("[PersistentWorldRegistry] Indexed sector paging failed: ", error));
- Runtime debug logging | Assets\_Project\Scripts\World\PersistentWorldRegistry.cs:5210:                    Hecton8.Core.H8Debug.LogWarning(string.Concat("[PersistentWorldRegistry] Indexed sector paging recovered with quarantine: ", error));
- Runtime debug logging | Assets\_Project\Scripts\World\PersistentWorldRegistry.cs:5244:                    Hecton8.Core.H8Debug.LogError(string.Concat("[PersistentWorldRegistry] Sector override merge failed: ", overrideError));
- Runtime debug logging | Assets\_Project\Scripts\World\PersistentWorldRegistry.cs:5261:                    Hecton8.Core.H8Debug.LogError(string.Concat("[PersistentWorldRegistry] Sector entity-state restore failed: ", entityStateError));
- Runtime debug logging | Assets\_Project\Scripts\World\PersistentWorldRegistry.cs:6359:                Hecton8.Core.H8Debug.LogError(failureMessage);
- Runtime debug logging | Assets\_Project\Scripts\World\PersistentWorldRegistry.cs:6367:                Hecton8.Core.H8Debug.LogError(failureMessage);
- Additional lines omitted here: 61. Use `../_scans/01_generated_assets_runtime_risks.txt` for the full list.

## REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED (75)

- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\Biomes\BiomeBoundarySdfRuntime.cs:629:            _sampleScratch.Result = new NativeArray<BiomeBoundarySdfResult>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\Biomes\BiomeBoundarySdfRuntime.cs:631:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:690:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:695:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:700:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:705:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:710:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:715:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:720:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:1162:                    Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\GPUScatterDirector.cs:2257:            _visibleCountReadback.Data = new NativeArray<uint>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\GPUScatterDirector.cs:2259:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PersistentWorldRegistry.cs:2661:        private const Allocator DataVaultExemptPersistentRecordAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PersistentWorldRegistry.cs:2662:        private const Allocator DataVaultExemptPersistentDeltaAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PersistentWorldRegistry.cs:2663:        private const Allocator DataVaultExemptPersistentTombstoneAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PersistentWorldRegistry.cs:2664:        private const Allocator DataVaultExemptPersistentHydrationAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PersistentWorldRegistry.cs:2665:        private const Allocator DataVaultExemptPersistentStateAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PersistentWorldRegistry.cs:2666:        private const Allocator DataVaultExemptPersistentQueueAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\Resources\WorldRegrowthSimulation.cs:310:            Allocator laneAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\Resources\ProceduralOreSpawner.cs:254:                NativeArray<T> array = new NativeArray<T>(length, Allocator.Persistent, options);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonMapMagicVegetationBridge.cs:4785:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonMapMagicVegetationBridge.cs:5567:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonMapMagicVegetationBridge.cs:8567:            state.HeightReadbackData = new NativeArray<ushort>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonMapMagicVegetationBridge.cs:8569:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\Outposts\MarauderOutpostGenerationService.cs:202:                    scratch = new NativeArray<T>(length, Allocator.Persistent, options);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\Outposts\MarauderOutpostGenerationService.cs:2021:                payload = new NativeArray<byte>(payloadBytes, Allocator.Temp, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonIndirectVegetationRenderer.cs:2519:            NativeArray<MetadataValue> batchMetadata = new NativeArray<MetadataValue>(BrgMetadataPlaceholderCount, TransientVegetationCullingAllocator);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonIndirectVegetationRenderer.cs:4466:            _cullTelemetryReadback.Data = new NativeArray<uint>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonIndirectVegetationRenderer.cs:4468:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonIndirectVegetationRenderer.cs:5122:            NativeArray<byte> visibilityMask = new NativeArray<byte>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonHLODRenderer.cs:288:                using (NativeArray<MetadataValue> batchMetadata = new NativeArray<MetadataValue>(BrgMetadataPlaceholderCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonDistantLandmarkRenderer.cs:422:                using (NativeArray<MetadataValue> batchMetadata = new NativeArray<MetadataValue>(BrgMetadataPlaceholderCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\ResourceDistributionDirector.cs:421:                    Workspace = new NativeArray<PressureMetamorphismSample>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\ResourceDistributionDirector.cs:423:                        Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\SargassumMicroFaunaBoids.cs:7603:            _parasiteLatchReadback.Data = new NativeArray<int>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\SargassumMicroFaunaBoids.cs:7605:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\VegetationChunkResidencyDirector.cs:483:                    Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\VegetationChunkResidencyDirector.cs:789:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\VegetationChunkResidencyDirector.cs:794:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\VegetationChunkResidencyDirector.cs:799:                Allocator.Persistent,
- Additional lines omitted here: 35. Use `../_scans/01_generated_assets_runtime_risks.txt` for the full list.

## LIKELY_LEGAL_COLD_OR_OWNER_LIFETIME_PATH (50)

- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\BiomeTransitionSmokeTester.cs:94:                samples = new NativeArray<BiomeTransitionSample>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\BiomeTransitionSmokeTester.cs:95:                sources = new NativeArray<BiomeTransitionFogSource>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\BiomeTransitionSmokeTester.cs:99:                fromAup = new NativeArray<AbsoluteUniversePositionBlit128>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\BiomeTransitionSmokeTester.cs:100:                toAup = new NativeArray<AbsoluteUniversePositionBlit128>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\BiomeTransitionSmokeTester.cs:101:                playerAup = new NativeArray<AbsoluteUniversePositionBlit128>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\BiomeTransitionSmokeTester.cs:102:                results = new NativeArray<BiomeTransitionFogResult>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\ChemicalInfluenceGrid.cs:2132:                payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\Biomes\BiomeTransitionManagerRuntime.cs:1519:                using NativeArray<byte> payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\Biomes\BiomeTransitionManagerRuntime.cs:1947:                using NativeArray<byte> payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\Biolum\HectonBiolumManager.cs:1472:                NativeArray<byte> payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\Biomes\BiomeBoundarySdfRuntime.cs:828:                    payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonSandboxAbyssalShelfSmokeTester.cs:106:                positions = new NativeArray<AbsoluteUniversePosition>(SampleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonSandboxAbyssalShelfSmokeTester.cs:107:                samples = new NativeArray<HectonSandboxAbyssalShelfAuditSample>(SampleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonSandboxAbyssalShelfSmokeTester.cs:108:                reductions = new NativeArray<HectonSandboxAbyssalShelfSampleReduction>(SampleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonSandboxAbyssalShelfSmokeTester.cs:109:                summary = new NativeArray<HectonSandboxAbyssalShelfSmokeSummary>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PersistentWorldRegistry.cs:5319:                desiredSectorHashView = new NativeArray<long>(PagedSectorHashCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PersistentWorldRegistry.cs:6489:                    NativeArray<PersistentWorldDeltaRecord> sectorRecords = new NativeArray<PersistentWorldDeltaRecord>(bucketCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PersistentWorldRegistry.cs:6567:            NativeArray<EntityDataRecord> sectorStates = new NativeArray<EntityDataRecord>(stateCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PersistentWorldRegistry.cs:8045:            NativeArray<EntityDataRecord> sectorStates = new NativeArray<EntityDataRecord>(entityStates.Count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\Resources\WorldRegrowthSimulation.cs:631:                NativeArray<byte> payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PcieBandwidthGuard1411SelfTest.cs:37:            NativeArray<Matrix4x4> matrices = new NativeArray<Matrix4x4>(MatrixInstanceCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PcieBandwidthGuard1411SelfTest.cs:38:            NativeArray<byte> dirtyPages = new NativeArray<byte>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PcieBandwidthGuard1411SelfTest.cs:75:            NativeArray<byte> dirtyPages = new NativeArray<byte>(4, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PcieBandwidthGuard1411SelfTest.cs:103:            NativeArray<byte> matrixDirtyPages = new NativeArray<byte>(4, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PcieBandwidthGuard1411SelfTest.cs:104:            NativeArray<byte> metadataDirtyPages = new NativeArray<byte>(4, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PcieBandwidthGuard1411SelfTest.cs:146:            NativeArray<int> source = new NativeArray<int>(FuzzerInstanceCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PcieBandwidthGuard1411SelfTest.cs:147:            NativeArray<int> bufferA = new NativeArray<int>(FuzzerInstanceCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PcieBandwidthGuard1411SelfTest.cs:148:            NativeArray<int> bufferB = new NativeArray<int>(FuzzerInstanceCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PcieBandwidthGuard1411SelfTest.cs:150:            NativeArray<byte> dirtyA = new NativeArray<byte>(pageCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PcieBandwidthGuard1411SelfTest.cs:151:            NativeArray<byte> dirtyB = new NativeArray<byte>(pageCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\Resources\ProceduralOreSpawner.cs:3091:            NativeArray<byte> payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonIndirectVegetationRenderer.cs:1518:            NativeArray<Matrix4x4> matrices = new NativeArray<Matrix4x4>(count, TransientVegetationCullingAllocator, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonIndirectVegetationRenderer.cs:1519:            NativeArray<HectonVegetationInstanceData> instanceData = new NativeArray<HectonVegetationInstanceData>(count, TransientVegetationCullingAllocator, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PlanetaryCanvasSmokeTester.cs:36:                heights = new NativeArray<float>(CellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PlanetaryCanvasSmokeTester.cs:37:                sediment = new NativeArray<float>(CellCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PlanetaryCanvasSmokeTester.cs:38:                weights = new NativeArray<float4>(CellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PlanetaryCanvasSmokeTester.cs:39:                slopeWeights = new NativeArray<float>(CellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\TerrainChunkPagerRuntime.cs:2346:                NativeArray<byte> payload = new NativeArray<byte>(totalBytes, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\VegetationMemorySovereigntyRuntime.cs:510:                payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\VegetationNavGridSynchronizer.cs:2294:            NativeArray<byte> payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Additional lines omitted here: 10. Use `../_scans/01_generated_assets_runtime_risks.txt` for the full list.

## REVIEW_RUNTIME_MESH_MATERIAL_PATH (9)

- Runtime mesh/material mutation | Assets\_Project\Scripts\World\FloraDataTemplate.cs:186:        [Tooltip("Local-space bounds size used by proxy generation and primitive collider fitting. MeshColliders remain forbidden.")]
- Runtime mesh/material mutation | Assets\_Project\Scripts\World\Outposts\MarauderOutpostGenerationService.cs:2184:            mesh.RecalculateNormals();
- Runtime mesh/material mutation | Assets\_Project\Scripts\World\ProceduralWreckGenerator.cs:5646:                result = new Mesh();
- Runtime mesh/material mutation | Assets\_Project\Scripts\World\ProceduralWreckGenerator.cs:5849:                result = new Mesh();
- Runtime mesh/material mutation | Assets\_Project\Scripts\World\ProceduralWreckGenerator.cs:5968:                result = new Mesh();
- Unity scene lookup | Assets\_Project\Scripts\World\ProceduralWreckGenerator.cs:6834:            MeshCollider[] meshColliders = root.GetComponentsInChildren<MeshCollider>(true);
- Runtime mesh/material mutation | Assets\_Project\Scripts\World\ProceduralWreckGenerator.cs:6837:                MeshCollider meshCollider = meshColliders[i];
- Runtime mesh/material mutation | Assets\_Project\Scripts\World\SargassumGlobalDragManager.cs:4048:            mesh.RecalculateNormals();
- Runtime mesh/material mutation | Assets\_Project\Scripts\Fauna\FaunaBrain.cs:7756:            MeshCollider meshCollider = ComponentReferenceUtility.ResolveOwnedComponent<MeshCollider>(transform);

## REVIEW_CACHE_OR_INJECTION_REQUIRED (8)

- Unity scene lookup | Assets\_Project\Scripts\World\ImpostorSystem.cs:1012:            obj.transform.GetComponentsInChildren(true, _rendererScratch);
- Unity scene lookup | Assets\_Project\Scripts\World\ImpostorSystem.cs:1199:                if (prefab == null || !prefab.TryGetComponent<LODGroup>(out _))
- Unity scene lookup | Assets\_Project\Scripts\World\HectonWorldShellController1428.cs:25:            if (cameraRig == null && Camera.main != null)
- Unity scene lookup | Assets\_Project\Scripts\World\HectonWorldShellController1428.cs:26:                cameraRig = Camera.main.transform;
- Unity scene lookup | Assets\_Project\Scripts\World\ProceduralWreckGenerator.cs:6851:            MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
- Unity scene lookup | Assets\_Project\Scripts\Fauna\CreatureDamageManager.cs:229:            GetComponentsInChildren(true, _rendererScratch);
- Unity scene lookup | Assets\_Project\Scripts\Fauna\FaunaBrain.cs:4425:            GetComponentsInChildren(true, _biolumPresentationLightScratch);
- Unity scene lookup | Assets\_Project\Scripts\Fauna\FaunaBrain.cs:7434:            GetComponentsInChildren(true, _logicalLodColliderScratch);

## LIKELY_LEGAL_COLD_OR_DIAGNOSTIC_PATH (2)

- Runtime debug logging | Assets\_Project\Scripts\World\PcieBandwidthGuard1411SelfTest.cs:32:            Debug.Log("Agent 1411 PCIe bandwidth guard self test passed.");
- Runtime debug logging | Assets\_Project\Scripts\Rendering\Scatter\AbyssalScatterBrgDataVaultBootstrap.cs:606:            Debug.LogWarning(message, context);

## REVIEW_HOT_PHASE_METHOD (2)

- Hot Unity phase method | Assets\_Project\Scripts\World\HectonWorldShellVisualDriver1428.cs:28:        private void LateUpdate()
- Hot Unity phase method | Assets\_Project\Scripts\World\HectonWorldShellController1428.cs:33:        private void Update()

## REVIEW_LOG_GUARD_REQUIRED (1)

- Runtime debug logging | Assets\_Project\Scripts\Fauna\PredatorCognitionDomain_Steering.cs:1765:            Debug.Log("[OOP_Movement_Scanner] scanned Update scopes=" + updateScopes.ToString() +

## LIKELY_LEGAL_COLD_LOOKUP (1)

- Unity scene lookup | Assets\_Project\Scripts\World\WorldLODSceneBootstrap.cs:107:                root.GetComponentsInChildren(_includeInactiveLODGroups, _sceneLODGroupBuffer);
