# World, Terrain, Voxels, Geology, Ecosystem, Celestial

Status: STATIC BIBLE/MANDATE/CODEBASE AUDIT - RUNTIME PROOF NOT RUN
Date: 2026-06-02
Verdict: YELLOW_RUNTIME_STATIC_RISK_REVIEW_REQUIRED

## Scope

This report compares the current root bible routes and selected mandate registry files against static codebase evidence. It does not prove Unity import health, Play Mode behavior, profiler cost, memory use, visual quality, or device performance.

## Bibles Checked

- OK world.md - 175 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK terrain.md - 109 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK voxels.md - 125 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK ecosystem.md - 97 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK atmosphere.md - 150 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK celestial.md - 95 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK streaming.md - 124 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK 3DMODEL_GEOLOGY_ROCKS.md - 123 lines; GlobalQualityWeight, proof, acceptance, rejection.

## Mandates Matched

- .agents-skills\AI_DYNAMIC_NAVGRID_SDF_INTEGRATION.txt
- .agents-skills\CORE_Weather_Abyssal_FlowField_Currents.txt
- .agents-skills\MATH_AUP_Determinism_Sync.txt
- .agents-skills\STRM_World_Streaming_Residency_Chunk_Management.txt
- .agents-skills\TOOL_Procedural_Wreckage_Generator.txt
- .agents-skills\VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt
- .agents-skills\VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt
- .agents-skills\VOX_Voxel_World_Logic_Carving_Persistence.txt

## Code/Asset Roots

- OK Assets\_Project\Scripts\World
- OK Assets\_Project\Scripts\Ecosystem
- OK Assets\_Project\Scripts\Fauna
- OK Assets\_Project\Scripts\Environment
- OK Assets\_Project\Editor\Generators
- OK Assets\_Project\Scripts\Cartography
- OK Assets\_Project\Scripts\Atmosphere
- MISSING Assets\_Project\Scripts\Celestial

## Static Evidence Found

Total matching files: 317. Showing first 80. Full list: _scans/06_world_terrain_voxels_ecosystem_evidence_files.txt.

- Assets\_Project\Editor\Generators\Flora\FloraTopologyStudio1604.cs
- Assets\_Project\Editor\Generators\Geology\AbyssalGeologyStudio1606.cs
- Assets\_Project\Editor\Generators\Geology\Hecton8.AbyssalGeology1606.Editor.asmdef
- Assets\_Project\Editor\Generators\Graphics\BiomeTransitionPolisher.cs
- Assets\_Project\Editor\Generators\World\AbyssalScatterApexIntegratorVerifier1614.cs
- Assets\_Project\Editor\Generators\World\AbyssalScatterPolisherJobs.cs
- Assets\_Project\Editor\Generators\World\AbyssalScatterPolisherPipeline.cs
- Assets\_Project\Editor\Generators\World\AbyssalScatterPolisherSelfTests.cs
- Assets\_Project\Editor\Generators\World\AbyssalScatterPolisherWindow.cs
- Assets\_Project\Editor\Generators\World\Hecton8.AbyssalScatter1614.Editor.asmdef
- Assets\_Project\Scripts\Atmosphere\AtmosphereMemorySovereigntyValidator1324.cs
- Assets\_Project\Scripts\Atmosphere\AtmosphericLightingState.cs
- Assets\_Project\Scripts\Atmosphere\BaseAtmosphereLogisticsGizmo.cs
- Assets\_Project\Scripts\Atmosphere\BaseAtmosphereLogisticsJobs.cs
- Assets\_Project\Scripts\Atmosphere\BaseAtmosphereLogisticsRuntime.cs
- Assets\_Project\Scripts\Atmosphere\BaseAtmosphereLogisticsTypes.cs
- Assets\_Project\Scripts\Atmosphere\GasDynamicsSolver.cs
- Assets\_Project\Scripts\Atmosphere\HectonSurfaceWeatherDirector.cs
- Assets\_Project\Scripts\Atmosphere\ShinobuAtmosphereWaveTunerWindow.cs
- Assets\_Project\Scripts\Atmosphere\ShinobuOceanSurfaceAtmosphereContracts.cs
- Assets\_Project\Scripts\Atmosphere\ShinobuOceanSurfaceAtmosphereRuntime.cs
- Assets\_Project\Scripts\Atmosphere\StormPropagation\Editor\Weather_Event_Inquisition.cs
- Assets\_Project\Scripts\Atmosphere\StormPropagation\ShinobuStormPropagationContracts.cs
- Assets\_Project\Scripts\Atmosphere\StormPropagation\ShinobuStormPropagationJobs.cs
- Assets\_Project\Scripts\Atmosphere\StormPropagation\ShinobuStormPropagationRuntime.cs
- Assets\_Project\Scripts\Atmosphere\SurfaceWeatherMath.cs
- Assets\_Project\Scripts\Atmosphere\SurfaceWeatherProfile.cs
- Assets\_Project\Scripts\Atmosphere\SurfaceWeatherVfxRig.cs
- Assets\_Project\Scripts\Atmosphere\ToxicOutgassingChemistryRuntime.cs
- Assets\_Project\Scripts\Atmosphere\ToxicOutgassingChemistryTypes.cs
- Assets\_Project\Scripts\Cartography\Editor\OOP_Map_Scanner.cs
- Assets\_Project\Scripts\Cartography\Editor\SonarMapTunerWindow.cs
- Assets\_Project\Scripts\Ecosystem\CreatureGeneticsProfile.cs
- Assets\_Project\Scripts\Ecosystem\EcosystemHealthDirector.cs
- Assets\_Project\Scripts\Ecosystem\EcosystemMigrationProfile.cs
- Assets\_Project\Scripts\Ecosystem\EcosystemRuntimeInstaller.cs
- Assets\_Project\Scripts\Ecosystem\Editor\BiomassDecayTunerWindow.cs
- Assets\_Project\Scripts\Ecosystem\Editor\FaunaGeneticsTunerWindow.cs
- Assets\_Project\Scripts\Ecosystem\Editor\LiveRotDebugGizmo.cs
- Assets\_Project\Scripts\Ecosystem\Editor\MacroEcosystemTunerWindow.cs
- Assets\_Project\Scripts\Ecosystem\Editor\OOP_Boid_Scanner.cs
- Assets\_Project\Scripts\Ecosystem\Editor\OOP_Destroy_Scanner.cs
- Assets\_Project\Scripts\Ecosystem\Editor\OOP_Spawner_Scanner.cs
- Assets\_Project\Scripts\Ecosystem\Editor\OOP_Variant_Scanner.cs
- Assets\_Project\Scripts\Ecosystem\FaunaBiomeMutationDefinition.cs
- Assets\_Project\Scripts\Ecosystem\FaunaBrain.Ecosystem.cs
- Assets\_Project\Scripts\Ecosystem\FaunaGeneticsManager.cs
- Assets\_Project\Scripts\Ecosystem\FaunaGeneticTraits.cs
- Assets\_Project\Scripts\Ecosystem\FaunaGenome64.cs
- Assets\_Project\Scripts\Ecosystem\MacroEcosystemHeatmapGizmo.cs
- Assets\_Project\Scripts\Ecosystem\MacroEcosystemMathematicianRuntime.cs
- Assets\_Project\Scripts\Ecosystem\MacroEcosystemMathematicianRuntime_SHINOBU300_Audit.cs
- Assets\_Project\Scripts\Ecosystem\MigrationDirector.cs
- Assets\_Project\Scripts\Ecosystem\NutrientDriftRuntime.cs
- Assets\_Project\Scripts\Ecosystem\NutrientDriftRuntime_Carrion.cs
- Assets\_Project\Scripts\Environment\Fluids\Contracts\OceanAdapterContracts.cs
- Assets\_Project\Scripts\Environment\Fluids\EmergencyMockOceanKinematicsAdapter.cs
- Assets\_Project\Scripts\Environment\GlobalWeatherDirector.cs
- Assets\_Project\Scripts\Environment\HectonSeismicTideDirector.cs
- Assets\_Project\Scripts\Environment\WeatherProfile.cs
- Assets\_Project\Scripts\Fauna\ApexTerritoryProfile.cs
- Assets\_Project\Scripts\Fauna\FaunaBrain.Compatibility.cs
- Assets\_Project\Scripts\Fauna\FaunaBrain.cs
- Assets\_Project\Scripts\Fauna\FaunaBrain.Foveated.cs
- Assets\_Project\Scripts\Fauna\FaunaDataTemplate.cs
- Assets\_Project\Scripts\Fauna\FaunaKinematicsRuntime.cs
- Assets\_Project\Scripts\Fauna\FaunaPresentationService.cs
- Assets\_Project\Scripts\Fauna\FaunaSensorSuite.cs
- Assets\_Project\Scripts\Fauna\FaunaSimulationEngine.cs
- Assets\_Project\Scripts\Fauna\FaunaSpeciesProfile.cs
- Assets\_Project\Scripts\Fauna\FaunaTentacleConstrainedIk.cs
- Assets\_Project\Scripts\Fauna\FaunaTier1LodProxyRegistry.cs
- Assets\_Project\Scripts\Fauna\LeviathanTentacleVerletSolver.cs
- Assets\_Project\Scripts\Fauna\MesofaunaBehavioralStateMachine.cs
- Assets\_Project\Scripts\Fauna\PredatorCognitionDomain.AcousticSdf.cs
- Assets\_Project\Scripts\Fauna\PredatorCognitionDomain.cs
- Assets\_Project\Scripts\Fauna\PredatorCognitionDomain_Steering.cs
- Assets\_Project\Scripts\Fauna\ProceduralCrabLegIKRuntime.cs
- Assets\_Project\Scripts\Fauna\StressDrivenSpawnDirector.cs
- Assets\_Project\Scripts\Fauna\StressDrivenSpawnDirectorGizmo.cs

## Static Risk Suspects

These are suspects, not confirmed defects. Runtime suspects need code review. Editor/tool suspects are legal only if they cannot execute in gameplay/player hot paths.

Runtime suspects:
Total runtime suspects: 254. Showing first 80. Full list: _scans/06_world_terrain_voxels_ecosystem_runtime_risks.txt.

- Assets\_Project\Scripts\World\AbyssalFluidDecalManager.cs:943:                    Hecton8.Core.H8Debug.LogError("[AbyssalFluidDecalManager] Missing decalMaterial asset. Runtime material creation is forbidden for this draw path.", this);
- Assets\_Project\Scripts\World\BioCableIK.cs:708:                Hecton8.Core.H8Debug.LogError("[BioCableIK] Missing cableMaterial asset. Runtime material creation is forbidden for cable rendering.", this);
- Assets\_Project\Scripts\World\AbyssalThermalManager.cs:796:                NativeArray<float> scratch = new NativeArray<float>(requiredLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\AbyssalThermalManager.cs:1058:                Hecton8.Core.H8Debug.LogError("[AbyssalThermalManager] Duplicate instance detected. Destroying the newer component.", this);
- Assets\_Project\Scripts\World\AbyssalThermalManager.cs:2304:                dumpBytes = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\AbyssalThermalManager.cs:4231:                H8Debug.LogError("[AbyssalThermalManager] Missing bioCableMaterial asset. Manager-created BioCableIK rigs are disabled until an authored material is assigned.", this);
- Assets\_Project\Scripts\World\DepthZoneDirector.cs:691:            H8Debug.Log("[DepthZone] Entered.");
- Assets\_Project\Scripts\World\CullingManager.cs:206:                Hecton8.Core.H8Debug.LogWarning("[CullingManager] Duplicate instance detected. Destroying duplicate.");
- Assets\_Project\Scripts\World\CullingManager.cs:213:            Hecton8.Core.H8Debug.Log("[CullingManager] Initialized.");
- Assets\_Project\Scripts\World\CullingManager.cs:537:                Hecton8.Core.H8Debug.LogWarning("[CullingManager] Skipping registration for object without Renderer owner.", obj);
- Assets\_Project\Scripts\World\CullingManager.cs:552:                    Hecton8.Core.H8Debug.LogWarning("[CullingManager] Registering object with disabled Renderer.", obj);
- Assets\_Project\Scripts\World\CullingManager.cs:558:                    Hecton8.Core.H8Debug.LogWarning("[CullingManager] Registering object with externally force-culled Renderer.", obj);
- Assets\_Project\Scripts\World\CullingManager.cs:645:                    Hecton8.Core.H8Debug.LogWarning("[CullingManager] Cannot apply layer cull distances: runtime camera is unresolved.");
- Assets\_Project\Scripts\World\CullingManager.cs:668:            Hecton8.Core.H8Debug.Log("[CullingManager] Layer cull distances applied.");
- Assets\_Project\Scripts\World\ChemicalInfluenceGrid.cs:2132:                payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\ChemicalInfluenceGrid.cs:2899:                Hecton8.Core.H8Debug.LogError("[SHINOBU_138] Chemical DTO layout validation failed. ARM64 padding contract broken.");
- Assets\_Project\Scripts\World\BoidStructValidator.cs:58:                Hecton8.Core.H8Debug.LogError(failureMessage);
- Assets\_Project\Scripts\World\BoidStructValidator.cs:67:                Hecton8.Core.H8Debug.Log(
- Assets\_Project\Scripts\World\Biolum\HectonBiolumZone.cs:454:            if (_debugLogSpawn) H8Debug.Log("[Biolum] light spawned");
- Assets\_Project\Scripts\World\Biolum\HectonBiolumManager.cs:412:            if (_debugLogUpdates) H8Debug.Log("[BiolumManager] Registered zone");
- Assets\_Project\Scripts\World\Biolum\HectonBiolumManager.cs:428:            if (_debugLogUpdates) H8Debug.Log("[BiolumManager] Unregistered zone");
- Assets\_Project\Scripts\World\Biolum\HectonBiolumManager.cs:656:            if (_debugLogUpdates) H8Debug.Log("[BiolumManager] Initialized");
- Assets\_Project\Scripts\World\Biolum\HectonBiolumManager.cs:1472:                NativeArray<byte> payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\DynamicResolutionScaler.cs:249:                Hecton8.Core.H8Debug.LogWarning("[DynamicResolutionScaler] Duplicate instance detected. Destroying duplicate.");
- Assets\_Project\Scripts\World\DynamicResolutionScaler.cs:262:                Hecton8.Core.H8Debug.LogError("[DynamicResolutionScaler] UniversalRenderPipeline.asset is null. Dynamic resolution disabled.");
- Assets\_Project\Scripts\World\DynamicResolutionScaler.cs:291:            Hecton8.Core.H8Debug.Log("[DynamicResolutionScaler] Initialized.");
- Assets\_Project\Scripts\World\BiomeTransitionSmokeTester.cs:45:                Hecton8.Core.H8Debug.Log(
- Assets\_Project\Scripts\World\BiomeTransitionSmokeTester.cs:94:                samples = new NativeArray<BiomeTransitionSample>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\World\BiomeTransitionSmokeTester.cs:95:                sources = new NativeArray<BiomeTransitionFogSource>(
- Assets\_Project\Scripts\World\BiomeTransitionSmokeTester.cs:99:                fromAup = new NativeArray<AbsoluteUniversePositionBlit128>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\World\BiomeTransitionSmokeTester.cs:100:                toAup = new NativeArray<AbsoluteUniversePositionBlit128>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\World\BiomeTransitionSmokeTester.cs:101:                playerAup = new NativeArray<AbsoluteUniversePositionBlit128>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\World\BiomeTransitionSmokeTester.cs:102:                results = new NativeArray<BiomeTransitionFogResult>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\World\FaunaSpatialHashRegistry.cs:670:                Hecton8.Core.H8Debug.LogError("[FaunaSpatialHashRegistry] Entry capacity exceeded. Runtime registry growth is forbidden.");
- Assets\_Project\Scripts\World\FloraDataTemplate.cs:186:        [Tooltip("Local-space bounds size used by proxy generation and primitive collider fitting. MeshColliders remain forbidden.")]
- Assets\_Project\Scripts\World\GPUScatterDirector.cs:2257:            _visibleCountReadback.Data = new NativeArray<uint>(
- Assets\_Project\Scripts\World\GPUScatterDirector.cs:2259:                Allocator.Persistent,
- Assets\_Project\Scripts\World\Biomes\BiomeBoundarySdfRuntime.cs:629:            _sampleScratch.Result = new NativeArray<BiomeBoundarySdfResult>(
- Assets\_Project\Scripts\World\Biomes\BiomeBoundarySdfRuntime.cs:631:                Allocator.Persistent,
- Assets\_Project\Scripts\World\Biomes\BiomeBoundarySdfRuntime.cs:828:                    payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\Biomes\BiomeBoundarySdfRuntimeBootstrap.cs:48:            Hecton8.Core.H8Debug.LogWarning(
- Assets\_Project\Scripts\World\Biomes\BiomeTransitionManagerRuntime.cs:1414:                Hecton8.Core.H8Debug.LogWarning("[BiomeTransitionManagerRuntime] CSV load failed: " + exception.Message, this);
- Assets\_Project\Scripts\World\Biomes\BiomeTransitionManagerRuntime.cs:1519:                using NativeArray<byte> payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\Biomes\BiomeTransitionManagerRuntime.cs:1947:                using NativeArray<byte> payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\Biomes\BiomeTransitionManagerRuntime.cs:1969:                Hecton8.Core.H8Debug.LogError("[BiomeTransitionManagerRuntime] Black-box dump failed: " + exception.Message);
- Assets\_Project\Scripts\World\FloraInteractionManager.cs:8127:                Hecton8.Core.H8Debug.LogError("[FloraInteractionManager] Missing wake trail compute shader. Expected Hecton_VegetationWakeTrailSim.compute.", this);
- Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:690:                Allocator.Persistent,
- Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:695:                Allocator.Persistent,
- Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:700:                Allocator.Persistent,
- Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:705:                Allocator.Persistent,
- Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:710:                Allocator.Persistent,
- Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:715:                Allocator.Persistent,
- Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:720:                Allocator.Persistent,
- Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:1162:                    Allocator.Persistent,
- Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs:1667:                Hecton8.Core.H8Debug.LogException(exception);
- Assets\_Project\Scripts\World\HectonDistantLandmarkRenderer.cs:422:                using (NativeArray<MetadataValue> batchMetadata = new NativeArray<MetadataValue>(BrgMetadataPlaceholderCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
- Assets\_Project\Scripts\World\HectonHLODRenderer.cs:288:                using (NativeArray<MetadataValue> batchMetadata = new NativeArray<MetadataValue>(BrgMetadataPlaceholderCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
- Assets\_Project\Scripts\World\HectonIndirectVegetationRenderer.cs:1518:            NativeArray<Matrix4x4> matrices = new NativeArray<Matrix4x4>(count, TransientVegetationCullingAllocator, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\HectonIndirectVegetationRenderer.cs:1519:            NativeArray<HectonVegetationInstanceData> instanceData = new NativeArray<HectonVegetationInstanceData>(count, TransientVegetationCullingAllocator, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\HectonIndirectVegetationRenderer.cs:1685:                Hecton8.Core.H8Debug.LogError("[HectonIndirectVegetationRenderer] Material is required and fallback shader resolution failed.", this);
- Assets\_Project\Scripts\World\HectonIndirectVegetationRenderer.cs:1707:                Hecton8.Core.H8Debug.LogError("[HectonIndirectVegetationRenderer] No near render mesh resolved.", this);
- Assets\_Project\Scripts\World\HectonIndirectVegetationRenderer.cs:2519:            NativeArray<MetadataValue> batchMetadata = new NativeArray<MetadataValue>(BrgMetadataPlaceholderCount, TransientVegetationCullingAllocator);
- Assets\_Project\Scripts\World\HectonIndirectVegetationRenderer.cs:4466:            _cullTelemetryReadback.Data = new NativeArray<uint>(
- Assets\_Project\Scripts\World\HectonIndirectVegetationRenderer.cs:4468:                Allocator.Persistent,
- Assets\_Project\Scripts\World\HectonIndirectVegetationRenderer.cs:5122:            NativeArray<byte> visibilityMask = new NativeArray<byte>(
- Assets\_Project\Scripts\World\HectonSpatialHash.cs:1225:                Hecton8.Core.H8Debug.LogError("[HectonSpatialHash] Handle allocator exhausted.");
- Assets\_Project\Scripts\World\HectonWorldShellVisualDriver1428.cs:28:        private void LateUpdate()
- Assets\_Project\Scripts\World\HectonSandboxAbyssalShelfSmokeTester.cs:106:                positions = new NativeArray<AbsoluteUniversePosition>(SampleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\HectonSandboxAbyssalShelfSmokeTester.cs:107:                samples = new NativeArray<HectonSandboxAbyssalShelfAuditSample>(SampleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\HectonSandboxAbyssalShelfSmokeTester.cs:108:                reductions = new NativeArray<HectonSandboxAbyssalShelfSampleReduction>(SampleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\HectonSandboxAbyssalShelfSmokeTester.cs:109:                summary = new NativeArray<HectonSandboxAbyssalShelfSmokeSummary>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\World\HectonWorldShellController1428.cs:25:            if (cameraRig == null && Camera.main != null)
- Assets\_Project\Scripts\World\HectonWorldShellController1428.cs:26:                cameraRig = Camera.main.transform;
- Assets\_Project\Scripts\World\HectonWorldShellController1428.cs:33:        private void Update()
- Assets\_Project\Scripts\World\LODSystemManager.cs:214:                Hecton8.Core.H8Debug.LogWarning("[LODSystemManager] Duplicate registry owner detected. Destroying duplicate.");
- Assets\_Project\Scripts\World\LODSystemManager.cs:234:            Hecton8.Core.H8Debug.Log("[LODSystemManager] Initialized. Max LOD groups: " + _maxLODGroupsPerFrame);
- Assets\_Project\Scripts\World\LODSystemManager.cs:565:                Hecton8.Core.H8Debug.LogWarning("[LODSystemManager] Invalid quality preset value. Using default (Medium).");
- Assets\_Project\Scripts\World\LODSystemManager.cs:598:                Hecton8.Core.H8Debug.LogWarning("[LODSystemManager] Registered LOD groups exceeds max capacity. Consider increasing capacity.");
- Assets\_Project\Scripts\World\HectonMapMagicVegetationBridge.cs:4785:                Allocator.Persistent,
- Assets\_Project\Scripts\World\HectonMapMagicVegetationBridge.cs:5361:            Hecton8.Core.H8Debug.Log(

Editor/tool/static suspects:
Total editor/tool/static suspects: 539. Showing first 80. Full list: _scans/06_world_terrain_voxels_ecosystem_editor_tool_risks.txt.

- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:163:                UnityEngine.Debug.LogError("[SHINOBU_308] Biota density bake failed: " + exception.GetType().Name + " " + exception.Message);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:202:                telemetry = new NativeArray<BiotaDensityBakeTelemetryEntry>(BiotaDensityBakeConstants.TelemetryFrames, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:217:                depth = new NativeArray<float>(result.PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:218:                silt = new NativeArray<float>(result.PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:219:                biome = new NativeArray<uint>(result.PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:220:                temperature = new NativeArray<float>(result.PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:221:                thermal = new NativeArray<float>(result.PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:222:                density = new NativeArray<byte>(result.RawByteCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:223:                nonFinite = new NativeArray<byte>(result.PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:227:                edgeWest = new NativeArray<float>(config.Height, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:228:                edgeEast = new NativeArray<float>(config.Height, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:229:                edgeSouth = new NativeArray<float>(config.Width, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:230:                edgeNorth = new NativeArray<float>(config.Width, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:231:                runCount = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:310:                countRleHandle.Complete();
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:314:                runs = new NativeArray<BiotaDensityRleRunDTO>(runCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:326:                rleHandle.Complete();
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:390:                    cleanupHandle.Complete();
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:447:                telemetry = new NativeArray<BiotaDensityBakeTelemetryEntry>(BiotaDensityBakeConstants.TelemetryFrames, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:462:                depth = new NativeArray<float>(result.PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:463:                silt = new NativeArray<float>(result.PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:464:                biome = new NativeArray<uint>(result.PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:465:                temperature = new NativeArray<float>(result.PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:466:                thermal = new NativeArray<float>(result.PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:467:                density = new NativeArray<byte>(result.RawByteCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:468:                nonFinite = new NativeArray<byte>(result.PixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:472:                edgeWest = new NativeArray<float>(config.Height, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:473:                edgeEast = new NativeArray<float>(config.Height, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:474:                edgeSouth = new NativeArray<float>(config.Width, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:475:                edgeNorth = new NativeArray<float>(config.Width, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:476:                runCount = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:555:                countRleHandle.Complete();
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:559:                runs = new NativeArray<BiotaDensityRleRunDTO>(runCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:571:                rleHandle.Complete();
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:636:                    cleanupHandle.Complete();
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:693:                depth = new NativeArray<float>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:694:                silt = new NativeArray<float>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:695:                biome = new NativeArray<uint>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:696:                temperature = new NativeArray<float>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:697:                thermal = new NativeArray<float>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:698:                density = new NativeArray<byte>(rawCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:699:                nonFinite = new NativeArray<byte>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:703:                edgeWest = new NativeArray<float>(config.Height, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:704:                edgeEast = new NativeArray<float>(config.Height, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:705:                edgeSouth = new NativeArray<float>(config.Width, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:706:                edgeNorth = new NativeArray<float>(config.Width, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:771:                handle.Complete();
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:796:                    cleanupHandle.Complete();
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:870:            NativeArray<BiotaSpawnRuleDTO> output = new NativeArray<BiotaSpawnRuleDTO>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:892:            NativeArray<BiotaRuleWeightDTO> output = new NativeArray<BiotaRuleWeightDTO>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:912:            NativeArray<BiotaThermalVentDTO> vents = new NativeArray<BiotaThermalVentDTO>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiotaDensityMapBaker\Editor\BiotaDensityBakePipeline.cs:1441:                UnityEngine.Debug.LogWarning("[SHINOBU_308] Blackbox dump failed closed: " + exception.GetType().Name);
- Assets\_Project\Scripts\World\Editor\VegetationMemorySovereigntyValidator1316.cs:25:            H8Debug.Log("[1316] Vegetation memory sovereignty layout validator passed.");
- Assets\_Project\Scripts\World\Editor\TerrainChunkPagerTunerWindow.cs:135:                _statusLabel.text = "No active TerrainChunkPagerRuntime.";
- Assets\_Project\Scripts\World\Editor\TerrainChunkPagerTunerWindow.cs:152:            _statusLabel.text = "Active. Effective radius " + tuning.EffectiveRingRadius.ToString("0.00", CultureInfo.InvariantCulture) +
- Assets\_Project\Scripts\World\Editor\TerrainChunkPagerTunerWindow.cs:188:            _graphLabel.text = "Latency " + counters.LatencyEwmaMs.ToString("0.0", CultureInfo.InvariantCulture) +
- Assets\_Project\Scripts\World\Editor\TerrainChunkPagerTunerWindow.cs:197:                _statusLabel.text = "No active TerrainChunkPagerRuntime.";
- Assets\_Project\Scripts\World\Editor\Synchronous_IO_Scanner.cs:20:            Debug.Log("Synchronous_IO_Scanner findings: " + findingCount + ". Report: " + ReportRelativePath);
- Assets\_Project\Scripts\World\Editor\SpawnZoneSdfValidationEditor.cs:167:                _stats.text = "Vault not active.";
- Assets\_Project\Scripts\World\Editor\SpawnZoneSdfValidationEditor.cs:209:            _stats.text = _builder.ToString();
- Assets\_Project\Scripts\World\Editor\SpawnZoneSdfValidationEditor.cs:329:            Debug.Log(RunScan());
- Assets\_Project\Scripts\World\BiomeWeightMapBaker\Editor\Terrain_Shader_Scanner.cs:38:            UnityEngine.Debug.Log("[SHINOBU_243] Terrain shader scanner wrote " + ReportPath + " offenders=" + offenders);
- Assets\_Project\Scripts\World\BiomeWeightMapBaker\Editor\BiomeSplatmapForgeWindow.cs:118:                _schemaLabel.text = "CSV Schema v" + BiomeSplatmapProfileCsvParser.CsvSchemaVersion + ": validation failed code " + validationCode.ToString();
- Assets\_Project\Scripts\World\BiomeWeightMapBaker\Editor\BiomeSplatmapForgeWindow.cs:119:                _statusLabel.text = "CSV rejected: " + CsvPath;
- Assets\_Project\Scripts\World\BiomeWeightMapBaker\Editor\BiomeSplatmapForgeWindow.cs:126:            _schemaLabel.text = "CSV Schema v" + BiomeSplatmapProfileCsvParser.CsvSchemaVersion + ": hash 0x" + schemaHash.ToString("X8", CultureInfo.InvariantCulture) + " | rows " + ruleCount + " | rule sets " + ruleSetCount;
- Assets\_Project\Scripts\World\BiomeWeightMapBaker\Editor\BiomeSplatmapForgeWindow.cs:127:            _statusLabel.text = "Loaded CSV rules: " + ruleCount + " | rule sets " + ruleSetCount + " | schema 0x" + schemaHash.ToString("X8", CultureInfo.InvariantCulture);
- Assets\_Project\Scripts\World\BiomeWeightMapBaker\Editor\BiomeSplatmapForgeWindow.cs:141:                _statusLabel.text = "Preview refreshed. R=Rock, G=Sand, B=Silt, A=Erosion.";
- Assets\_Project\Scripts\World\BiomeWeightMapBaker\Editor\BiomeSplatmapForgeWindow.cs:148:                _statusLabel.text = "Bake already running.";
- Assets\_Project\Scripts\World\BiomeWeightMapBaker\Editor\BiomeSplatmapForgeWindow.cs:158:                _statusLabel.text = baked
- Assets\_Project\Scripts\World\BiomeWeightMapBaker\Editor\BiomeWeightMapBakePipeline.cs:176:                heights = new NativeArray<float>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiomeWeightMapBaker\Editor\BiomeWeightMapBakePipeline.cs:177:                erosion = new NativeArray<float>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiomeWeightMapBaker\Editor\BiomeWeightMapBakePipeline.cs:178:                macros = new NativeArray<uint>(macroCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiomeWeightMapBaker\Editor\BiomeWeightMapBakePipeline.cs:179:                normals = new NativeArray<float3>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiomeWeightMapBaker\Editor\BiomeWeightMapBakePipeline.cs:180:                pixels = new NativeArray<Color32>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiomeWeightMapBaker\Editor\BiomeWeightMapBakePipeline.cs:181:                nonFiniteFlags = new NativeArray<byte>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiomeWeightMapBaker\Editor\BiomeWeightMapBakePipeline.cs:183:                edgeWest = new NativeArray<float>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiomeWeightMapBaker\Editor\BiomeWeightMapBakePipeline.cs:184:                edgeEast = new NativeArray<float>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiomeWeightMapBaker\Editor\BiomeWeightMapBakePipeline.cs:185:                edgeSouth = new NativeArray<float>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiomeWeightMapBaker\Editor\BiomeWeightMapBakePipeline.cs:186:                edgeNorth = new NativeArray<float>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\World\BiomeWeightMapBaker\Editor\BiomeWeightMapBakePipeline.cs:187:                telemetry = new NativeArray<BiomeSplatmapBakeTelemetryEntry>(BiomeWeightMapBakeConstants.TelemetryFrames, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

## Exists / Missing / Required Proof

- Exists: bible routes exist and static implementation evidence was found.
- Partial: runtime static risk suspects need manual code review.
- Editor/tool: static suspects exist but may be legal if editor-only or cold-path.
- Required proof: Seed manifest, terrain mask proof, voxel seam proof, chunk/residency proof, compact/high route screenshots, biome/ecosystem owner cadence proof.

## Next Audit Action

Classify each runtime suspect as cold-path/legal or runtime violation. Fix runtime violations before profiler proof.
