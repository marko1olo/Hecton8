# Runtime Preclassification - Physics, Vehicles, Pressure, Water Truth, Survival Physiology

Status: HEURISTIC FIRST PASS - MANUAL REVIEW STILL REQUIRED
Date: 2026-06-02

This file groups static runtime suspects by a conservative heuristic. It can reduce review time, but it cannot prove a line is legal or illegal without reading the containing method and owner phase.

Raw generated runtime suspects: 280. Line-level reconciliation: 281 classified lines in `LINE_LEVEL_CLASSIFICATION.md`.

## Summary

- LEGAL_EDITOR_OR_DEV_GUARDED: 96
- REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 78
- LIKELY_LEGAL_COLD_OR_OWNER_LIFETIME_PATH: 49
- REVIEW_RUNTIME_MESH_MATERIAL_PATH: 17
- REVIEW_UNCLASSIFIED_STATIC_RISK: 14
- LIKELY_LEGAL_COLD_PATH: 12
- REVIEW_CACHE_OR_INJECTION_REQUIRED: 7
- LIKELY_LEGAL_COLD_OR_DIAGNOSTIC_PATH: 3
- REVIEW_HOT_PHASE_METHOD: 3
- LIKELY_LEGAL_COLD_LOOKUP: 1

## LEGAL_EDITOR_OR_DEV_GUARDED (96)

- Runtime debug logging | Assets\_Project\Scripts\Physics\Cavitation\AbyssalCavitationRuntime.cs:1103:                Hecton8.Core.H8Debug.LogWarning("[SHINOBU_248] Ordnance CSV load failed.");
- Runtime debug logging | Assets\_Project\Scripts\Physics\GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:307:                H8Debug.LogError("[1337] Physics culling DTO layout violation.");
- Runtime debug logging | Assets\_Project\Scripts\Physics\Vehicles\VehicleComponentDamageRuntime.cs:116:                Hecton8.Core.H8Debug.LogError(layoutError, this);
- Runtime debug logging | Assets\_Project\Scripts\Atmosphere\SurfaceWeatherVfxRig.cs:215:                    Hecton8.Core.H8Debug.Log("[SurfaceWeatherVfxRig] Missing authored LineRenderer. Lightning presentation disabled until authoredBoltRenderer is assigned.", this);
- Runtime debug logging | Assets\_Project\Scripts\Atmosphere\SurfaceWeatherVfxRig.cs:252:                Hecton8.Core.H8Debug.Log("[SurfaceWeatherVfxRig] Missing lightningBoltMaterial asset. Lightning presentation disabled until material is assigned.", this);
- Runtime debug logging | Assets\_Project\Scripts\Atmosphere\BaseAtmosphereLogisticsEditor.cs:28:                Hecton8.Core.H8Debug.LogError("[SHINOBU_221] Base atmosphere logistics layout mismatch.");
- Runtime debug logging | Assets\_Project\Scripts\Atmosphere\StormPropagation\ShinobuStormPropagationContracts.cs:722:                Hecton8.Core.H8Debug.LogError("SHINOBU_234 StormPropagationDTO layout validation failed.");
- Runtime debug logging | Assets\_Project\Scripts\World\AbyssalFluidDecalManager.cs:943:                    Hecton8.Core.H8Debug.LogError("[AbyssalFluidDecalManager] Missing decalMaterial asset. Runtime material creation is forbidden for this draw path.", this);
- Runtime debug logging | Assets\_Project\Scripts\World\BioCableIK.cs:998:                Hecton8.Core.H8Debug.LogError("[BioCableIK] Missing cableMaterial asset. Runtime material creation is forbidden for cable rendering.", this);
- Runtime debug logging | Assets\_Project\Scripts\World\Biomes\BiomeBoundarySdfRuntimeBootstrap.cs:48:            Hecton8.Core.H8Debug.LogWarning(
- Runtime debug logging | Assets\_Project\Scripts\World\BiomeTransitionSmokeTester.cs:45:                Hecton8.Core.H8Debug.Log(
- Runtime debug logging | Assets\_Project\Scripts\World\Biolum\HectonBiolumZone.cs:454:            if (_debugLogSpawn) H8Debug.Log("[Biolum] light spawned");
- Runtime debug logging | Assets\_Project\Scripts\World\Biomes\BiomeTransitionManagerRuntime.cs:1414:                Hecton8.Core.H8Debug.LogWarning("[BiomeTransitionManagerRuntime] CSV load failed: " + exception.Message, this);
- Runtime debug logging | Assets\_Project\Scripts\World\Biomes\BiomeTransitionManagerRuntime.cs:1969:                Hecton8.Core.H8Debug.LogError("[BiomeTransitionManagerRuntime] Black-box dump failed: " + exception.Message);
- Runtime debug logging | Assets\_Project\Scripts\World\FaunaSpatialHashRegistry.cs:670:                Hecton8.Core.H8Debug.LogError("[FaunaSpatialHashRegistry] Entry capacity exceeded. Runtime registry growth is forbidden.");
- Runtime debug logging | Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:1694:                Hecton8.Core.H8Debug.LogException(exception);
- Runtime debug logging | Assets\_Project\Scripts\World\CullingManager.cs:215:                Hecton8.Core.H8Debug.LogWarning("[CullingManager] Duplicate instance detected. Destroying duplicate.");
- Runtime debug logging | Assets\_Project\Scripts\World\CullingManager.cs:222:            Hecton8.Core.H8Debug.Log("[CullingManager] Initialized.");
- Runtime debug logging | Assets\_Project\Scripts\World\CullingManager.cs:552:                Hecton8.Core.H8Debug.LogWarning("[CullingManager] Skipping registration for object without Renderer owner.", obj);
- Runtime debug logging | Assets\_Project\Scripts\World\CullingManager.cs:569:                    Hecton8.Core.H8Debug.LogWarning("[CullingManager] Registering object with disabled Renderer.", obj);
- Runtime debug logging | Assets\_Project\Scripts\World\CullingManager.cs:575:                    Hecton8.Core.H8Debug.LogWarning("[CullingManager] Registering object with externally force-culled Renderer.", obj);
- Runtime debug logging | Assets\_Project\Scripts\World\CullingManager.cs:665:                    Hecton8.Core.H8Debug.LogWarning("[CullingManager] Cannot apply layer cull distances: runtime camera is unresolved.");
- Runtime debug logging | Assets\_Project\Scripts\World\CullingManager.cs:688:            Hecton8.Core.H8Debug.Log("[CullingManager] Layer cull distances applied.");
- Runtime debug logging | Assets\_Project\Scripts\World\Biolum\HectonBiolumManager.cs:412:            if (_debugLogUpdates) H8Debug.Log("[BiolumManager] Registered zone");
- Runtime debug logging | Assets\_Project\Scripts\World\Biolum\HectonBiolumManager.cs:428:            if (_debugLogUpdates) H8Debug.Log("[BiolumManager] Unregistered zone");
- Runtime debug logging | Assets\_Project\Scripts\World\Biolum\HectonBiolumManager.cs:656:            if (_debugLogUpdates) H8Debug.Log("[BiolumManager] Initialized");
- Runtime debug logging | Assets\_Project\Scripts\World\DepthZoneDirector.cs:691:            H8Debug.Log("[DepthZone] Entered.");
- Runtime debug logging | Assets\_Project\Scripts\World\HectonSpatialHash.cs:1225:                Hecton8.Core.H8Debug.LogError("[HectonSpatialHash] Handle allocator exhausted.");
- Runtime debug logging | Assets\_Project\Scripts\World\FloraInteractionManager.cs:8127:                Hecton8.Core.H8Debug.LogError("[FloraInteractionManager] Missing wake trail compute shader. Expected Hecton_VegetationWakeTrailSim.compute.", this);
- Runtime debug logging | Assets\_Project\Scripts\World\ChemicalInfluenceGrid.cs:2899:                Hecton8.Core.H8Debug.LogError("[SHINOBU_138] Chemical DTO layout validation failed. ARM64 padding contract broken.");
- Runtime debug logging | Assets\_Project\Scripts\World\BoidStructValidator.cs:58:                Hecton8.Core.H8Debug.LogError(failureMessage);
- Runtime debug logging | Assets\_Project\Scripts\World\BoidStructValidator.cs:67:                Hecton8.Core.H8Debug.Log(
- Runtime debug logging | Assets\_Project\Scripts\World\DynamicResolutionScaler.cs:249:                Hecton8.Core.H8Debug.LogWarning("[DynamicResolutionScaler] Duplicate instance detected. Destroying duplicate.");
- Runtime debug logging | Assets\_Project\Scripts\World\DynamicResolutionScaler.cs:262:                Hecton8.Core.H8Debug.LogError("[DynamicResolutionScaler] UniversalRenderPipeline.asset is null. Dynamic resolution disabled.");
- Runtime debug logging | Assets\_Project\Scripts\World\DynamicResolutionScaler.cs:291:            Hecton8.Core.H8Debug.Log("[DynamicResolutionScaler] Initialized.");
- Runtime debug logging | Assets\_Project\Scripts\World\HectonMapMagicVegetationBridge.cs:5392:            Hecton8.Core.H8Debug.Log(
- Runtime debug logging | Assets\_Project\Scripts\World\HectonMapMagicVegetationBridge.cs:5402:            Hecton8.Core.H8Debug.LogError("[HectonMapMagicVegetationBridge] Loop guard hit.");
- Runtime debug logging | Assets\_Project\Scripts\World\HectonIndirectVegetationRenderer.cs:1688:                Hecton8.Core.H8Debug.LogError("[HectonIndirectVegetationRenderer] Material is required and fallback shader resolution failed.", this);
- Runtime debug logging | Assets\_Project\Scripts\World\HectonIndirectVegetationRenderer.cs:1712:                Hecton8.Core.H8Debug.LogError("[HectonIndirectVegetationRenderer] No near render mesh resolved.", this);
- Runtime debug logging | Assets\_Project\Scripts\World\ScatterEvaluator.cs:93:                Hecton8.Core.H8Debug.LogError("[ScatterEvaluator] Not initialized. Call Initialize() first.");
- Additional lines omitted here: 56. Use `../_scans/05_physics_vehicles_water_runtime_risks.txt` for the full list.

## REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED (78)

- Native allocation or persistent lifetime | Assets\_Project\Scripts\Physics\Buoyancy\AsyncReadback\AsyncBuoyancyReadbackRuntime.cs:1555:            data = new NativeArray<ReadbackRequestDTO>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Physics\Buoyancy\AsyncReadback\AsyncBuoyancyReadbackRuntime.cs:1557:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Atmosphere\GasDynamicsSolver.cs:1746:            scratch = new NativeArray<GasDynamicsTelemetryEntry>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Atmosphere\GasDynamicsSolver.cs:1748:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Atmosphere\StormPropagation\ShinobuStormPropagationRuntime.cs:144:                array = new NativeArray<T>(length, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Atmosphere\ShinobuOceanSurfaceAtmosphereRuntime.cs:1789:            data = new NativeArray<float4>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Atmosphere\ShinobuOceanSurfaceAtmosphereRuntime.cs:1791:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\Biomes\BiomeBoundarySdfRuntime.cs:629:            _sampleScratch.Result = new NativeArray<BiomeBoundarySdfResult>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\Biomes\BiomeBoundarySdfRuntime.cs:631:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonHLODRenderer.cs:288:                using (NativeArray<MetadataValue> batchMetadata = new NativeArray<MetadataValue>(BrgMetadataPlaceholderCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:687:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:692:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:697:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:702:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:707:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:712:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:717:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:1178:                    Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonDistantLandmarkRenderer.cs:422:                using (NativeArray<MetadataValue> batchMetadata = new NativeArray<MetadataValue>(BrgMetadataPlaceholderCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\GPUScatterDirector.cs:2257:            _visibleCountReadback.Data = new NativeArray<uint>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\GPUScatterDirector.cs:2259:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonMapMagicVegetationBridge.cs:4816:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonMapMagicVegetationBridge.cs:5598:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonMapMagicVegetationBridge.cs:8598:            state.HeightReadbackData = new NativeArray<ushort>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonMapMagicVegetationBridge.cs:8600:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonIndirectVegetationRenderer.cs:2524:            NativeArray<MetadataValue> batchMetadata = new NativeArray<MetadataValue>(BrgMetadataPlaceholderCount, TransientVegetationCullingAllocator);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonIndirectVegetationRenderer.cs:4471:            _cullTelemetryReadback.Data = new NativeArray<uint>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonIndirectVegetationRenderer.cs:4473:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonIndirectVegetationRenderer.cs:5127:            NativeArray<byte> visibilityMask = new NativeArray<byte>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\SargassumMicroFaunaBoids.cs:7603:            _parasiteLatchReadback.Data = new NativeArray<int>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\SargassumMicroFaunaBoids.cs:7605:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\SpawnZoneSdfValidation.cs:1022:            NativeArray<byte> payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PersistentWorldRegistry.cs:2661:        private const Allocator DataVaultExemptPersistentRecordAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PersistentWorldRegistry.cs:2662:        private const Allocator DataVaultExemptPersistentDeltaAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PersistentWorldRegistry.cs:2663:        private const Allocator DataVaultExemptPersistentTombstoneAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PersistentWorldRegistry.cs:2664:        private const Allocator DataVaultExemptPersistentHydrationAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PersistentWorldRegistry.cs:2665:        private const Allocator DataVaultExemptPersistentStateAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PersistentWorldRegistry.cs:2666:        private const Allocator DataVaultExemptPersistentQueueAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\Outposts\MarauderOutpostGenerationService.cs:188:                    scratch = new NativeArray<T>(length, Allocator.Persistent, options);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\Outposts\MarauderOutpostGenerationService.cs:1973:                payload = new NativeArray<byte>(payloadBytes, Allocator.Temp, NativeArrayOptions.ClearMemory);
- Additional lines omitted here: 38. Use `../_scans/05_physics_vehicles_water_runtime_risks.txt` for the full list.

## LIKELY_LEGAL_COLD_OR_OWNER_LIFETIME_PATH (49)

- Native allocation or persistent lifetime | Assets\_Project\Scripts\Physics\GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:2200:            NativeArray<byte> payload = new NativeArray<byte>((int)totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Physics\Vehicles\VehicleComponentDamageRuntime.cs:946:                stagedGrid = new NativeArray<VehicleGridCellDTO>(cellCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Physics\HarpoonTensionSolver328.cs:1410:                payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Thermodynamics\AbyssalThermodynamicsSolver.ReactorBridge.cs:960:            NativeArray<byte> payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Thermodynamics\ThermodynamicsHazardGridRuntime.cs:1322:                payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\Biomes\BiomeBoundarySdfRuntime.cs:828:                    payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\BiomeTransitionSmokeTester.cs:94:                samples = new NativeArray<BiomeTransitionSample>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\BiomeTransitionSmokeTester.cs:95:                sources = new NativeArray<BiomeTransitionFogSource>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\BiomeTransitionSmokeTester.cs:99:                fromAup = new NativeArray<AbsoluteUniversePositionBlit128>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\BiomeTransitionSmokeTester.cs:100:                toAup = new NativeArray<AbsoluteUniversePositionBlit128>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\BiomeTransitionSmokeTester.cs:101:                playerAup = new NativeArray<AbsoluteUniversePositionBlit128>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\BiomeTransitionSmokeTester.cs:102:                results = new NativeArray<BiomeTransitionFogResult>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\Biomes\BiomeTransitionManagerRuntime.cs:1519:                using NativeArray<byte> payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\Biomes\BiomeTransitionManagerRuntime.cs:1947:                using NativeArray<byte> payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\Biolum\HectonBiolumManager.cs:1472:                NativeArray<byte> payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonSandboxAbyssalShelfSmokeTester.cs:106:                positions = new NativeArray<AbsoluteUniversePosition>(SampleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonSandboxAbyssalShelfSmokeTester.cs:107:                samples = new NativeArray<HectonSandboxAbyssalShelfAuditSample>(SampleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonSandboxAbyssalShelfSmokeTester.cs:108:                reductions = new NativeArray<HectonSandboxAbyssalShelfSampleReduction>(SampleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonSandboxAbyssalShelfSmokeTester.cs:109:                summary = new NativeArray<HectonSandboxAbyssalShelfSmokeSummary>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\ChemicalInfluenceGrid.cs:2132:                payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonIndirectVegetationRenderer.cs:1521:            NativeArray<Matrix4x4> matrices = new NativeArray<Matrix4x4>(count, TransientVegetationCullingAllocator, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\HectonIndirectVegetationRenderer.cs:1522:            NativeArray<HectonVegetationInstanceData> instanceData = new NativeArray<HectonVegetationInstanceData>(count, TransientVegetationCullingAllocator, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\AbyssalThermalManager.cs:806:                NativeArray<float> scratch = new NativeArray<float>(requiredLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\AbyssalThermalManager.cs:2316:                dumpBytes = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PlanetaryCanvasSmokeTester.cs:36:                heights = new NativeArray<float>(CellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PlanetaryCanvasSmokeTester.cs:37:                sediment = new NativeArray<float>(CellCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PlanetaryCanvasSmokeTester.cs:38:                weights = new NativeArray<float4>(CellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PlanetaryCanvasSmokeTester.cs:39:                slopeWeights = new NativeArray<float>(CellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PersistentWorldRegistry.cs:5319:                desiredSectorHashView = new NativeArray<long>(PagedSectorHashCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PersistentWorldRegistry.cs:6489:                    NativeArray<PersistentWorldDeltaRecord> sectorRecords = new NativeArray<PersistentWorldDeltaRecord>(bucketCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PersistentWorldRegistry.cs:6567:            NativeArray<EntityDataRecord> sectorStates = new NativeArray<EntityDataRecord>(stateCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PersistentWorldRegistry.cs:8045:            NativeArray<EntityDataRecord> sectorStates = new NativeArray<EntityDataRecord>(entityStates.Count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PcieBandwidthGuard1411SelfTest.cs:37:            NativeArray<Matrix4x4> matrices = new NativeArray<Matrix4x4>(MatrixInstanceCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PcieBandwidthGuard1411SelfTest.cs:38:            NativeArray<byte> dirtyPages = new NativeArray<byte>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PcieBandwidthGuard1411SelfTest.cs:75:            NativeArray<byte> dirtyPages = new NativeArray<byte>(4, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PcieBandwidthGuard1411SelfTest.cs:103:            NativeArray<byte> matrixDirtyPages = new NativeArray<byte>(4, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PcieBandwidthGuard1411SelfTest.cs:104:            NativeArray<byte> metadataDirtyPages = new NativeArray<byte>(4, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PcieBandwidthGuard1411SelfTest.cs:146:            NativeArray<int> source = new NativeArray<int>(FuzzerInstanceCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PcieBandwidthGuard1411SelfTest.cs:147:            NativeArray<int> bufferA = new NativeArray<int>(FuzzerInstanceCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\World\PcieBandwidthGuard1411SelfTest.cs:148:            NativeArray<int> bufferB = new NativeArray<int>(FuzzerInstanceCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Additional lines omitted here: 9. Use `../_scans/05_physics_vehicles_water_runtime_risks.txt` for the full list.

## REVIEW_RUNTIME_MESH_MATERIAL_PATH (17)

- Runtime mesh/material mutation | Assets\_Project\Scripts\Physics\GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:1329:                    bodyState.MeshColliderStripActive != 0;
- Runtime mesh/material mutation | Assets\_Project\Scripts\Physics\GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:1731:                bodyState.MeshColliderStripActive != 0;
- Runtime mesh/material mutation | Assets\_Project\Scripts\Physics\GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:2625:            public float MeshColliderStripDistanceMeters;
- Runtime mesh/material mutation | Assets\_Project\Scripts\Physics\GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:2626:            public float MeshColliderRestoreDistanceMeters;
- Runtime mesh/material mutation | Assets\_Project\Scripts\Physics\GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:2704:                bool meshStripActive = (currentState & CullingStateMeshColliderStripped) != 0;
- Runtime mesh/material mutation | Assets\_Project\Scripts\Physics\GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:2706:                float stripSq = MeshColliderStripDistanceMeters * MeshColliderStripDistanceMeters;
- Runtime mesh/material mutation | Assets\_Project\Scripts\Physics\GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:2707:                float restoreSq = MeshColliderRestoreDistanceMeters * MeshColliderRestoreDistanceMeters;
- Runtime mesh/material mutation | Assets\_Project\Scripts\Physics\GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:2708:                bool shouldStripMeshColliders = hasHeavyCollider &
- Runtime mesh/material mutation | Assets\_Project\Scripts\Physics\GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:2720:                command |= math.select(0, (int)CullingCommandStripMeshColliders, shouldStripMeshColliders & commandExtensionsEnabled);
- Runtime mesh/material mutation | Assets\_Project\Scripts\Physics\GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:2751:                command |= math.select(0, (int)CullingCommandStripMeshColliders, (currentState & CullingStateMeshColliderStripped) != 0);
- Runtime mesh/material mutation | Assets\_Project\Scripts\Physics\Vehicles\Automation\SubmarineAutopilotSdfNavigator.cs:538:        // A Unity physics/MeshCollider path was rejected as CPU-heavy and nondeterministic for the 100km AUP world.
- Runtime mesh/material mutation | Assets\_Project\Scripts\World\FloraDataTemplate.cs:186:        [Tooltip("Local-space bounds size used by proxy generation and primitive collider fitting. MeshColliders remain forbidden.")]
- Runtime mesh/material mutation | Assets\_Project\Scripts\World\ProceduralWreckGenerator.cs:5660:                result = new Mesh();
- Runtime mesh/material mutation | Assets\_Project\Scripts\World\ProceduralWreckGenerator.cs:5866:                result = new Mesh();
- Runtime mesh/material mutation | Assets\_Project\Scripts\World\ProceduralWreckGenerator.cs:5988:                result = new Mesh();
- Unity scene lookup | Assets\_Project\Scripts\World\ProceduralWreckGenerator.cs:6880:            MeshCollider[] meshColliders = root.GetComponentsInChildren<MeshCollider>(true);
- Runtime mesh/material mutation | Assets\_Project\Scripts\World\ProceduralWreckGenerator.cs:6883:                MeshCollider meshCollider = meshColliders[i];

## REVIEW_UNCLASSIFIED_STATIC_RISK (14)

- Uncategorized | Assets\_Project\Scripts\Physics\Buoyancy\AnalyticalWaveTunerWindow.Editor.cs:203:                _statusLabel.text = "Analytical wave telemetry is not allocated.";
- Uncategorized | Assets\_Project\Scripts\Physics\Buoyancy\AnalyticalWaveTunerWindow.Editor.cs:224:            _statusLabel.text = _builder.ToString();
- Uncategorized | Assets\_Project\Scripts\Physics\Buoyancy\BuoyancyDisplacementRuntime.cs:2462:            label.text = new string(_simdGizmoLabelBuffer, 0, write);
- Uncategorized | Assets\_Project\Scripts\Thermodynamics\AbyssalHeatTunerWindow.cs:104:                _statusLabel.text = "Runtime: offline";
- Uncategorized | Assets\_Project\Scripts\Thermodynamics\AbyssalHeatTunerWindow.cs:109:            _statusLabel.text = "Runtime: online";
- Uncategorized | Assets\_Project\Scripts\Thermodynamics\AbyssalHeatTunerWindow.cs:121:                _telemetryLabel.text =
- Uncategorized | Assets\_Project\Scripts\Thermodynamics\AbyssalHeatTunerWindow.cs:137:                _reactorTelemetryLabel.text =
- Uncategorized | Assets\_Project\Scripts\Thermodynamics\SubmarineThermodynamicsTunerWindow.cs:87:                _status.text = "Runtime unavailable";
- Uncategorized | Assets\_Project\Scripts\Thermodynamics\SubmarineThermodynamicsTunerWindow.cs:91:            _status.text = runtime.TryReadReactorTelemetry(0, out ReactorThermalTelemetryEntry entry)
- Uncategorized | Assets\_Project\Scripts\Thermodynamics\SubmarineThermodynamicsTunerWindow.cs:107:                _status.text = "Runtime unavailable";
- Uncategorized | Assets\_Project\Scripts\Thermodynamics\SubmarineThermodynamicsTunerWindow.cs:116:            _status.text = runtime.TryWriteReactorTuning(tuning) ? "Applied" : "Write rejected: solver job pending";
- Uncategorized | Assets\_Project\Scripts\Atmosphere\BaseAtmosphereLogisticsEditor.cs:146:                _status.text =
- Uncategorized | Assets\_Project\Scripts\Atmosphere\BaseAtmosphereLogisticsEditor.cs:156:                _status.text = "No atmosphere telemetry yet.";
- Uncategorized | Assets\_Project\Scripts\Atmosphere\ShinobuAtmosphereWaveTunerWindow.cs:150:            _statusLabel.text = ShinobuOceanSurfaceAtmosphereRuntime.TryGetVaultSnapshot(out _, out _, out _)

## LIKELY_LEGAL_COLD_PATH (12)

- Runtime mesh/material mutation | Assets\_Project\Scripts\Physics\RuntimePhysicsBaker1609.cs:23:        [Tooltip("MeshCollider that must already reference the offline COL_ mesh before play.")]
- Runtime mesh/material mutation | Assets\_Project\Scripts\Physics\RuntimePhysicsBaker1609.cs:24:        [SerializeField] private MeshCollider targetCollider;
- Runtime mesh/material mutation | Assets\_Project\Scripts\Physics\RuntimePhysicsBaker1609.cs:33:        [SerializeField] private MeshColliderCookingOptions cookingOptions =
- Runtime mesh/material mutation | Assets\_Project\Scripts\Physics\RuntimePhysicsBaker1609.cs:34:            MeshColliderCookingOptions.CookForFasterSimulation |
- Runtime mesh/material mutation | Assets\_Project\Scripts\Physics\RuntimePhysicsBaker1609.cs:35:            MeshColliderCookingOptions.EnableMeshCleaning |
- Runtime mesh/material mutation | Assets\_Project\Scripts\Physics\RuntimePhysicsBaker1609.cs:36:            MeshColliderCookingOptions.WeldColocatedVertices;
- Runtime mesh/material mutation | Assets\_Project\Scripts\Physics\RuntimePhysicsBaker1609.cs:47:        public MeshCollider TargetCollider => targetCollider;
- Runtime mesh/material mutation | Assets\_Project\Scripts\Physics\RuntimePhysicsBaker1609.cs:56:        public MeshColliderCookingOptions CookingOptions => cookingOptions;
- Runtime mesh/material mutation | Assets\_Project\Scripts\Physics\RuntimePhysicsBaker1609.cs:74:            MeshCollider collider,
- Runtime mesh/material mutation | Assets\_Project\Scripts\Physics\RuntimePhysicsBaker1609.cs:77:            MeshColliderCookingOptions bakeCookingOptions)
- Runtime mesh/material mutation | Assets\_Project\Scripts\Physics\RuntimePhysicsBaker1609.cs:102:            out MeshColliderCookingOptions bakeCookingOptions,
- Runtime mesh/material mutation | Assets\_Project\Scripts\Physics\RuntimePhysicsBaker1609.cs:103:            out MeshCollider collider)

## REVIEW_CACHE_OR_INJECTION_REQUIRED (7)

- Unity scene lookup | Assets\_Project\Scripts\Physics\GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:1395:            body.GetComponentsInChildren(false, _sleepColliderScratch);
- Unity scene lookup | Assets\_Project\Scripts\World\HectonWorldShellController1428.cs:25:            if (cameraRig == null && Camera.main != null)
- Unity scene lookup | Assets\_Project\Scripts\World\HectonWorldShellController1428.cs:26:                cameraRig = Camera.main.transform;
- Unity scene lookup | Assets\_Project\Scripts\World\ImpostorSystem.cs:1215:            obj.transform.GetComponentsInChildren(true, _rendererScratch);
- Unity scene lookup | Assets\_Project\Scripts\World\ImpostorSystem.cs:1413:                if (prefab == null || !prefab.TryGetComponent<LODGroup>(out _))
- Unity scene lookup | Assets\_Project\Scripts\World\ResourceDistributionDirector.cs:1378:            if (_authoredOrePrefab != null && _authoredOrePrefab.GetComponent<ResourceNode>() == null)
- Unity scene lookup | Assets\_Project\Scripts\World\ProceduralWreckGenerator.cs:6897:            MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);

## LIKELY_LEGAL_COLD_OR_DIAGNOSTIC_PATH (3)

- Runtime debug logging | Assets\_Project\Scripts\Physics\TetherVerletJobs.cs:622:                UnityEngine.Debug.Log("[1303] Tether memory sovereignty validator passed.");
- Runtime debug logging | Assets\_Project\Scripts\Physics\TetherVerletJobs.cs:624:                UnityEngine.Debug.LogError("[1303] Tether memory sovereignty validator failed.");
- Runtime debug logging | Assets\_Project\Scripts\World\PcieBandwidthGuard1411SelfTest.cs:32:            Debug.Log("Agent 1411 PCIe bandwidth guard self test passed.");

## REVIEW_HOT_PHASE_METHOD (3)

- Hot Unity phase method | Assets\_Project\Scripts\Physics\Buoyancy\AnalyticalWaveTunerWindow.Editor.cs:117:        private void Update()
- Hot Unity phase method | Assets\_Project\Scripts\World\HectonWorldShellVisualDriver1428.cs:28:        private void LateUpdate()
- Hot Unity phase method | Assets\_Project\Scripts\World\HectonWorldShellController1428.cs:33:        private void Update()

## LIKELY_LEGAL_COLD_LOOKUP (1)

- Unity scene lookup | Assets\_Project\Scripts\World\WorldLODSceneBootstrap.cs:107:                root.GetComponentsInChildren(_includeInactiveLODGroups, _sceneLODGroupBuffer);

