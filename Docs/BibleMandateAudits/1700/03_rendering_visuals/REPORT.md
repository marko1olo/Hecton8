# Rendering, Shaders, Lighting, VFX, Water Presentation

Status: STATIC BIBLE/MANDATE/CODEBASE AUDIT - RUNTIME PROOF NOT RUN
Date: 2026-06-02
Verdict: YELLOW_RUNTIME_STATIC_RISK_REVIEW_REQUIRED

## Scope

This report compares the current root bible routes and selected mandate registry files against static codebase evidence. It does not prove Unity import health, Play Mode behavior, profiler cost, memory use, visual quality, or device performance.

## Bibles Checked

- OK rendering.md - 166 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK shaders.md - 107 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK lighting.md - 101 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK vfx.md - 95 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK presentation.md - 160 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK water.md - 243 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK atmosphere.md - 150 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK platform.md - 218 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK performance.md - 157 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK compute.md - 144 lines; GlobalQualityWeight, proof, acceptance, rejection.

## Mandates Matched

- .agents-skills\CORE_Weather_Abyssal_FlowField_Currents.txt
- .agents-skills\GPU_Compute_Kernels_Kernels_Optimization_MX350.txt
- .agents-skills\GPU_Compute_Warp_Sizing_Mobile.txt
- .agents-skills\OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- .agents-skills\OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- .agents-skills\REND_Abyssal_Lighting_Voxel_Occlusion_Shadows.txt
- .agents-skills\REND_DescriptorBinding_Reality_Check.txt
- .agents-skills\REND_Foveated_Simulation_LOD.txt
- .agents-skills\REND_GPU_Driven_Animation_VAT.txt
- .agents-skills\REND_GPU_Occlusion_Culling_6000.txt
- .agents-skills\REND_GPU_Sovereignty.txt
- .agents-skills\REND_Instanced_Flora_Physics.txt
- .agents-skills\REND_Shader_Noir_Aesthetics_Dithering_Fog.txt
- .agents-skills\REND_Shader_Stutter_Linux_Vulkan.txt
- .agents-skills\REND_Terrain_VirtualTexturing.txt
- .agents-skills\REND_URP_Graphics_HotPath_Optimization_HLOD.txt
- .agents-skills\REND_VFX_Fluid_Aesthetics_Compute_Particles.txt
- .agents-skills\REND_VR_Stencil_Masking.txt
- .agents-skills\REND_VRS_MX350_Reality_Check.txt

## Code/Asset Roots

- OK Assets\_Project\Scripts\Rendering
- OK Assets\_Project\Scripts\Graphics
- OK Assets\_Project\Scripts\VFX
- OK Assets\_Project\Scripts\Lighting
- OK Assets\_Project\Scripts\Atmosphere
- OK Assets\_Project\Scripts\Environment
- OK Assets\_Project\Editor
- OK Data\Visuals
- OK Tools

## Static Evidence Found

Total matching files: 183. Showing first 80. Full list: _scans/03_rendering_visuals_evidence_files.txt.

- Assets\_Project\Editor\Bakers\ApexIntegratorVerifier1605.cs
- Assets\_Project\Editor\Bakers\ProceduralTextureBaker.cs
- Assets\_Project\Editor\Bakers\TextureAtlasPacker.cs
- Assets\_Project\Editor\Generators\Fauna\AbyssalAnatomyStudio1610.cs
- Assets\_Project\Editor\Generators\Fauna\FaunaApexIntegratorVerifier1610.cs
- Assets\_Project\Editor\Generators\Flora\FloraTopologyStudio1604.cs
- Assets\_Project\Editor\Generators\Graphics\BiomeTransitionPolisher.cs
- Assets\_Project\Editor\Generators\Interiors\InteriorFinisherStudio1608.cs
- Assets\_Project\Editor\Generators\Structures\DeepReachStationContracts.cs
- Assets\_Project\Editor\Generators\Structures\DeepReachStationFabricator.cs
- Assets\_Project\Editor\Generators\Structures\DeepReachStationModuleLibrary.cs
- Assets\_Project\Editor\Generators\World\AbyssalScatterApexIntegratorVerifier1614.cs
- Assets\_Project\Editor\Generators\World\AbyssalScatterPolisherJobs.cs
- Assets\_Project\Editor\Generators\World\AbyssalScatterPolisherPipeline.cs
- Assets\_Project\Editor\Generators\World\AbyssalScatterPolisherWindow.cs
- Assets\_Project\Editor\HectonMeshGenerator.cs
- Assets\_Project\Editor\HectonPhysicsSkinGenerator.cs
- Assets\_Project\Editor\HectonSkyTools.cs
- Assets\_Project\Editor\HectonSphereGenerator.cs
- Assets\_Project\Editor\HectonSurfacePainter.cs
- Assets\_Project\Editor\HectonUIBuilder.cs
- Assets\_Project\Editor\HectonWaterGrid.cs
- Assets\_Project\Editor\Particle_System_Scanner.cs
- Assets\_Project\Editor\Physics\ColliderOptimizationEngine1609.cs
- Assets\_Project\Editor\PlayModePerformanceMonitor.cs
- Assets\_Project\Editor\PropwashGpuLayoutValidator.cs
- Assets\_Project\Editor\PropwashGpuTunerWindow.cs
- Assets\_Project\Editor\QA\SceneIntegrityValidator1627.cs
- Assets\_Project\Editor\ShorelineFoamGraftEditorTools.cs
- Assets\_Project\Editor\SinglePassOceanRendererFeatureInstaller.cs
- Assets\_Project\Editor\SinglePassOceanTunerWindow.cs
- Assets\_Project\Editor\UnityReloadAuditReport.cs
- Assets\_Project\Scripts\Atmosphere\AtmosphericLightingState.cs
- Assets\_Project\Scripts\Atmosphere\BaseAtmosphereEngine.cs
- Assets\_Project\Scripts\Atmosphere\BaseAtmosphereLogisticsJobs.cs
- Assets\_Project\Scripts\Atmosphere\BaseAtmosphereLogisticsRuntime.cs
- Assets\_Project\Scripts\Atmosphere\BaseAtmosphereLogisticsTypes.cs
- Assets\_Project\Scripts\Atmosphere\BaseAtmosphereMath.cs
- Assets\_Project\Scripts\Atmosphere\GasDynamicsSolver.cs
- Assets\_Project\Scripts\Atmosphere\HectonSurfaceWeatherDirector.cs
- Assets\_Project\Scripts\Atmosphere\ShinobuOceanSurfaceAtmosphereContracts.cs
- Assets\_Project\Scripts\Atmosphere\ShinobuOceanSurfaceAtmosphereRuntime.cs
- Assets\_Project\Scripts\Atmosphere\StormPropagation\Editor\ShinobuStormPropagationTunerWindow.cs
- Assets\_Project\Scripts\Atmosphere\StormPropagation\Editor\Weather_Event_Inquisition.cs
- Assets\_Project\Scripts\Atmosphere\StormPropagation\ShinobuStormPropagationContracts.cs
- Assets\_Project\Scripts\Atmosphere\StormPropagation\ShinobuStormPropagationDebugGizmo.cs
- Assets\_Project\Scripts\Atmosphere\StormPropagation\ShinobuStormPropagationJobs.cs
- Assets\_Project\Scripts\Atmosphere\StormPropagation\ShinobuStormPropagationRuntime.cs
- Assets\_Project\Scripts\Atmosphere\SurfaceWeatherMath.cs
- Assets\_Project\Scripts\Atmosphere\SurfaceWeatherProfile.cs
- Assets\_Project\Scripts\Atmosphere\SurfaceWeatherVfxRig.cs
- Assets\_Project\Scripts\Atmosphere\ToxicOutgassingChemistryRuntime.cs
- Assets\_Project\Scripts\Atmosphere\ToxicOutgassingChemistryTypes.cs
- Assets\_Project\Scripts\Environment\Fluids\Contracts\OceanAdapterContracts.cs
- Assets\_Project\Scripts\Environment\Fluids\EmergencyMockOceanKinematicsAdapter.cs
- Assets\_Project\Scripts\Environment\Fluids\OceanAdapterVaultRoute.cs
- Assets\_Project\Scripts\Environment\GlobalWeatherDirector.cs
- Assets\_Project\Scripts\Environment\HectonSeismicTideDirector.cs
- Assets\_Project\Scripts\Environment\WeatherEvents.cs
- Assets\_Project\Scripts\Environment\WeatherProfile.cs
- Assets\_Project\Scripts\Graphics\Caustics\AnalyticalCausticsService.cs
- Assets\_Project\Scripts\Graphics\Caustics\Hecton8.Graphics.Caustics.asmdef
- Assets\_Project\Scripts\Graphics\Culling\AbyssalShadowCullingJobs.cs
- Assets\_Project\Scripts\Graphics\Culling\AbyssalShadowCullingRuntime.cs
- Assets\_Project\Scripts\Graphics\Culling\AbyssalShadowCullingTypes.cs
- Assets\_Project\Scripts\Graphics\Culling\Editor\AbyssalShadowTunerWindow.cs
- Assets\_Project\Scripts\Graphics\Culling\Editor\TBDRPipelineTunerWindow.cs
- Assets\_Project\Scripts\Graphics\Culling\InstanceCullingService.cs
- Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonRuntime.cs
- Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonTypes.cs
- Assets\_Project\Scripts\Graphics\Materials\Editor\Hecton8.Graphics.Materials.Editor.asmdef
- Assets\_Project\Scripts\Graphics\Materials\Editor\UberNoirDegradationCsvBridge.cs
- Assets\_Project\Scripts\Graphics\Materials\Editor\UberNoirMaterialLabWindow.cs
- Assets\_Project\Scripts\Graphics\Materials\Editor\Visual_Aging_Inquisition.cs
- Assets\_Project\Scripts\Graphics\Materials\Editor\Visual_Material_Inquisition.cs
- Assets\_Project\Scripts\Graphics\Materials\Editor\VisualPressureAgingTunerWindow.cs
- Assets\_Project\Scripts\Graphics\Materials\H8ShaderIDs.cs
- Assets\_Project\Scripts\Graphics\Materials\Hecton8.Graphics.Materials.asmdef
- Assets\_Project\Scripts\Graphics\Materials\ProceduralFloraBiomeTintBridge.cs
- Assets\_Project\Scripts\Graphics\Materials\ShinobuMaterialResponseRuntime.cs

## Static Risk Suspects

These are suspects, not confirmed defects. Runtime suspects need code review. Editor/tool suspects are legal only if they cannot execute in gameplay/player hot paths.

Runtime suspects:
Total runtime suspects: 109. Showing first 80. Full list: _scans/03_rendering_visuals_runtime_risks.txt.

- Assets\_Project\Scripts\Rendering\LutArrayResolver.cs:501:            Hecton8.Core.H8Debug.LogWarning("[LutArrayResolver] Water_Extinction_Matrix.bin not found. Using analytical Beer-Lambert fallback.");
- Assets\_Project\Scripts\Rendering\LutArrayResolver.cs:508:            Hecton8.Core.H8Debug.LogWarning("[LutArrayResolver] Failed to load Water_Extinction_Matrix.bin: " + exception.Message);
- Assets\_Project\Scripts\Rendering\LutArrayResolver.cs:515:            Hecton8.Core.H8Debug.LogWarning("[LutArrayResolver] Invalid Water_Extinction_Matrix.bin byte count: " + byteCount);
- Assets\_Project\Scripts\Rendering\LutArrayResolver.cs:522:            Hecton8.Core.H8Debug.LogWarning("[LutArrayResolver] Invalid extinction LUT texel count: " + texelCount);
- Assets\_Project\Scripts\Rendering\LutArrayResolver.cs:529:            Hecton8.Core.H8Debug.LogWarning("[LutArrayResolver] R16G16B16A16_SFloat sampling is unsupported; packed R16 path remains active when available.");
- Assets\_Project\Scripts\Rendering\LutArrayResolver.cs:536:            Hecton8.Core.H8Debug.LogWarning("[LutArrayResolver] StreamingAssets URI staging failed: " + error);
- Assets\_Project\Scripts\Rendering\LutArrayResolver.cs:543:            Hecton8.Core.H8Debug.LogWarning("[LutArrayResolver] Portable or low-memory target detected. Using analytical Beer-Lambert fallback instead of streaming Water_Extinction_Matrix.bin.");
- Assets\_Project\Scripts\Rendering\GlobalShaderDispatcher.cs:1394:                Hecton8.Core.H8Debug.LogWarning("[GlobalShaderDispatcher] CSV override parse failed: " + exception.Message);
- Assets\_Project\Scripts\Rendering\BilateralDrs\HectonBilateralDrsUpscalerRuntime.cs:2118:            NativeArray<byte> payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\Rendering\BilateralDrs\HectonBilateralDrsUpscalerFeature.cs:389:                        Hecton8.Core.H8Debug.LogError("[13KRA] Bilateral DRS compute shader is missing a clear edge-mask kernel.");
- Assets\_Project\Scripts\Rendering\BilateralDrs\HectonBilateralDrsUpscalerFeature.cs:422:                        Hecton8.Core.H8Debug.LogError("[13KRA] Bilateral DRS compute shader is missing one or more active upscaler kernels.");
- Assets\_Project\Scripts\Rendering\AbyssalCaustics\AbyssalDeferredCausticsRuntime.cs:1925:            NativeArray<byte> payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\Rendering\Scatter\GpuScatterLodManager.cs:1682:            _visibleCountReadback.Data = new NativeArray<uint>(
- Assets\_Project\Scripts\Rendering\Scatter\GpuScatterLodManager.cs:1684:                Allocator.Persistent,
- Assets\_Project\Scripts\Rendering\Scatter\AbyssalScatterBrgDataVaultBootstrap.cs:322:            payload.Matrices = new NativeArray<Matrix4x4>(header.MatrixCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\Rendering\Scatter\AbyssalScatterBrgDataVaultBootstrap.cs:323:            payload.Metadata = new NativeArray<GpuScatterFloraInstanceData>(header.MetadataCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\Rendering\Scatter\AbyssalScatterBrgDataVaultBootstrap.cs:324:            payload.QualityIndices = new NativeArray<int>(header.QualityIndexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\Rendering\Scatter\AbyssalScatterBrgDataVaultBootstrap.cs:486:            NativeArray<byte> seen = new NativeArray<byte>(count, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Rendering\Scatter\AbyssalScatterBrgDataVaultBootstrap.cs:606:            Debug.LogWarning(message, context);
- Assets\_Project\Scripts\Graphics\Culling\InstanceCullingService.cs:895:            _indirectArgsReadback.Data = new NativeArray<uint>(
- Assets\_Project\Scripts\Graphics\Culling\InstanceCullingService.cs:897:                Allocator.Persistent,
- Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonRuntime.cs:509:                _buffers.MockVisibleInstances = new NativeArray<PoiTransformDTO>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
- Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonRuntime.cs:510:                _buffers.SortScratch = new NativeArray<PoiTransformDTO>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
- Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonRuntime.cs:511:                _buffers.MeshVertexCounts = new NativeArray<uint>(256, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
- Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonRuntime.cs:512:                _buffers.RadixHistogram = new NativeArray<int>(RadixHistogramCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
- Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonRuntime.cs:513:                _buffers.VisibleCountOut = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
- Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonRuntime.cs:514:                _buffers.MockQualitySignal = new NativeArray<TBDRMockQualityWeightSignal>(1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
- Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonRuntime.cs:515:                _buffers.MockCamera = new NativeArray<MockCameraMatrix>(1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
- Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonRuntime.cs:516:                _buffers.SourceFrustumPlanes = new NativeArray<float4>(6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
- Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonRuntime.cs:517:                _buffers.SqueezedFrustumPlanes = new NativeArray<float4>(6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
- Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonRuntime.cs:518:                _buffers.HzbVisibilityMask = new NativeArray<int>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
- Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonRuntime.cs:519:                _buffers.IndirectDrawArgs = new NativeArray<TBDRIndirectDrawArgsDTO>(1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
- Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonTypes.cs:477:            VertexBudgetCounters = new NativeArray<TBDRVertexBudgetCounter64>(BudgetCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: NativeArray<TBDRVertexBudgetCounter64>[BudgetCount] - 64B budget caps - owner: SHINOBU_45_TBDR_PIPELINE
- Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonTypes.cs:478:            TileWarnings = new NativeArray<TileSpillWarningDTO>(WarningCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<TileSpillWarningDTO>[WarningCount] - tile-spill warning lane - owner: SHINOBU_45_TBDR_PIPELINE
- Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonTypes.cs:479:            TransparentQuadCount = new NativeArray<int>(TransparentCounterCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<int>[TransparentCounterCount] - transparent overdraw counters - owner: SHINOBU_45_TBDR_PIPELINE
- Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonTypes.cs:480:            TelemetryRing = new NativeArray<TBDRPipelineTelemetryEntry>(TelemetryCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<TBDRPipelineTelemetryEntry>[TelemetryCapacity] - 300-frame black-box ring - owner: SHINOBU_45_TBDR_PIPELINE
- Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonTypes.cs:518:                VertexBudgetCounters = new NativeArray<TBDRVertexBudgetCounter64>(BudgetCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
- Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonTypes.cs:519:                TileWarnings = new NativeArray<TileSpillWarningDTO>(WarningCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
- Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonTypes.cs:520:                TransparentQuadCount = new NativeArray<int>(TransparentCounterCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
- Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonTypes.cs:521:                TelemetryRing = new NativeArray<TBDRPipelineTelemetryEntry>(TelemetryCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
- Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonTypes.cs:1291:                SliceTable = new NativeArray<TextureStreamingSliceDTO>(SliceCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: fixed texture array residency table; production should pass GlobalDataVault
- Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonTypes.cs:1719:            Ring = new NativeArray<TBDRPipelineTelemetryEntry>(RingCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<TBDRPipelineTelemetryEntry>[300] - TBDR black-box ring - owner: SHINOBU_45_TBDR_TELEMETRY
- Assets\_Project\Scripts\VFX\BiomeProfile.cs:68:                Hecton8.Core.H8Debug.LogWarning("[BiomeProfile] High AOIntensity may impact performance.");
- Assets\_Project\Scripts\VFX\CameraJuiceSystem_CameraJuiceBurst.cs:184:                Hecton8.Core.H8Debug.LogError("[SHINOBU_354] Camera juice ABI violation.");
- Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:248:                densities = new NativeArray<sbyte>(GridCellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:249:                decompressed = new NativeArray<sbyte>(GridCellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:250:                stats = new NativeArray<int>(StatsLength, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:251:                writtenCount = new NativeArray<int>(CounterLength, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:679:            densities = new NativeArray<sbyte>(GridCellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:680:            decompressed = new NativeArray<sbyte>(GridCellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:681:            accumulator = new NativeArray<int>(GridCellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:682:            removedMass = new NativeArray<int>(CounterLength, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:683:            debrisCount = new NativeArray<int>(CounterLength, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:684:            stats = new NativeArray<int>(StatsLength, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:685:            writtenCount = new NativeArray<int>(CounterLength, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:686:            particles = new NativeArray<DebrisParticleDTO>(ShinobuDeltaCrusher.MaximumQualityDebrisCap, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\VFX\CameraJuiceSystem.cs:861:            Hecton8.Core.H8Debug.LogError("[CameraJuiceSystem] Duplicate instance detected. Destroying duplicate.");
- Assets\_Project\Scripts\VFX\CameraJuiceSystem.cs:869:            Hecton8.Core.H8Debug.LogError("[CameraJuiceSystem] MainCamera not found. System disabled.");
- Assets\_Project\Scripts\VFX\CameraJuiceSystem.cs:877:            Hecton8.Core.H8Debug.LogError("[CameraJuiceSystem] URPVolume not found. Post-processing disabled.");
- Assets\_Project\Scripts\VFX\CameraJuiceSystem.cs:885:            Hecton8.Core.H8Debug.Log("[CameraJuiceSystem] Vignette override not found in Volume profile. Health vignette presentation disabled.");
- Assets\_Project\Scripts\VFX\CameraJuiceSystem.cs:893:            Hecton8.Core.H8Debug.Log("[CameraJuiceSystem] DepthOfField override not found in Volume profile. Depth-of-field presentation disabled.");
- Assets\_Project\Scripts\VFX\CameraJuiceSystem.cs:901:            Hecton8.Core.H8Debug.LogError("[CameraJuiceSystem] Shake calculation failed.");
- Assets\_Project\Scripts\VFX\CameraJuiceSystem.cs:909:            Hecton8.Core.H8Debug.LogError("[CameraJuiceSystem] TriggerShake called with null profile.");
- Assets\_Project\Scripts\VFX\CameraJuiceSystem.cs:917:            Hecton8.Core.H8Debug.LogWarning("[CameraJuiceSystem] ShakeProfile MaxDisplacement out of range [0, 1]. Clamping.");
- Assets\_Project\Scripts\VFX\CameraJuiceSystem.cs:925:            Hecton8.Core.H8Debug.LogWarning("[CameraJuiceSystem] ShakeProfile Duration invalid. Using default 0.5s.");
- Assets\_Project\Scripts\VFX\CameraJuiceSystem.cs:933:            Hecton8.Core.H8Debug.LogWarning("[CameraJuiceSystem] TransitionToBiome called with null biome. Using default fallback.");
- Assets\_Project\Scripts\VFX\CameraJuiceSystem.cs:941:            Hecton8.Core.H8Debug.LogError("[CameraJuiceSystem] FOV calculation failed.");
- Assets\_Project\Scripts\VFX\CameraJuiceSystem.cs:949:            Hecton8.Core.H8Debug.LogError("[CameraJuiceSystem] Biome blend failed.");
- Assets\_Project\Scripts\VFX\CameraJuiceSystem.cs:957:            Hecton8.Core.H8Debug.LogError("[CameraJuiceSystem] Interaction focus calculation failed.");
- Assets\_Project\Scripts\VFX\CameraJuiceSystem.cs:965:            Hecton8.Core.H8Debug.LogWarning("[CameraJuiceSystem] Frame time exceeded budget.");
- Assets\_Project\Scripts\VFX\CameraJuiceSystem.cs:973:            Hecton8.Core.H8Debug.LogError("[CameraJuiceSystem] Health post-processing failed.");
- Assets\_Project\Scripts\VFX\Debris\CarveDebrisComputeRenderer.cs:2393:            mesh.RecalculateNormals();
- Assets\_Project\Scripts\VFX\HectonMarineSnowRenderer.cs:2845:            Hecton8.Core.H8Debug.LogError("HectonMarineSnowRenderer: compute kernel CSMain not found. Disabling compute marine snow.");
- Assets\_Project\Scripts\VFX\HectonMarineSnowRenderer.cs:2853:            Hecton8.Core.H8Debug.LogError("HectonMarineSnowRenderer: compute kernel InitializeParticles not found. Disabling compute marine snow.");
- Assets\_Project\Scripts\VFX\HectonMarineSnowRenderer.cs:2861:            Hecton8.Core.H8Debug.LogError("HectonMarineSnowRenderer: compute kernel ClearVisibleParticles not found. Disabling compute marine snow.");
- Assets\_Project\Scripts\VFX\HectonMarineSnowRenderer.cs:2869:            Hecton8.Core.H8Debug.LogError("HectonMarineSnowRenderer: auxiliary compute kernels not found. Disabling compute marine snow.");
- Assets\_Project\Scripts\VFX\HectonMarineSnowRenderer.cs:2877:            Hecton8.Core.H8Debug.LogError("HectonMarineSnowRenderer: propwash compute kernels not found. Disabling compute marine snow.");
- Assets\_Project\Scripts\VFX\HectonMarineSnowRenderer.cs:2885:            Hecton8.Core.H8Debug.LogError("HectonMarineSnowRenderer: compute kernel thread-group contract is invalid. Disabling compute marine snow.");
- Assets\_Project\Scripts\VFX\Bioluminescence\BiolumPulseSyncRuntime.cs:316:                    entries = new NativeArray<BiolumPulseTelemetryEntry>(
- Assets\_Project\Scripts\VFX\Bioluminescence\BiolumPulseSyncRuntime.cs:318:                        Allocator.Persistent,

Editor/tool/static suspects:
Total editor/tool/static suspects: 658. Showing first 80. Full list: _scans/03_rendering_visuals_editor_tool_risks.txt.

- Assets\_Project\Scripts\Rendering\AbyssalCaustics\Editor\AbyssalCausticsTunerWindow.cs:118:                _statusLabel.text = hasParameters ? RuntimeActiveLabel : RuntimeOfflineLabel;
- Assets\_Project\Scripts\Rendering\AbyssalCaustics\Editor\AbyssalCausticsTunerWindow.cs:124:                    _depthLabel.text = DepthUnavailableLabel;
- Assets\_Project\Scripts\Rendering\AbyssalCaustics\Editor\AbyssalCausticsTunerWindow.cs:125:                    _qualityLabel.text = QualityUnavailableLabel;
- Assets\_Project\Scripts\Rendering\AbyssalCaustics\Editor\AbyssalCausticsTunerWindow.cs:141:                _depthLabel.text = s_depthLabels[depthTenths];
- Assets\_Project\Scripts\Rendering\AbyssalCaustics\Editor\AbyssalCausticsTunerWindow.cs:151:                _qualityLabel.text = s_qualityLabels[qualityMillis];
- Assets\_Project\Scripts\Rendering\AbyssalCaustics\Editor\AbyssalCausticsTunerWindow.cs:170:            _csvStatusLabel.text = loaded ? ProfilesLoadedLabel : ProfilesFailedLabel;
- Assets\_Project\Scripts\Rendering\AbyssalCaustics\Editor\AbyssalCausticsLayoutAudit.cs:16:                Debug.Log("13KRA caustics DTO layout audit passed.");
- Assets\_Project\Scripts\Rendering\AbyssalCaustics\Editor\AbyssalCausticsLayoutAudit.cs:18:                Debug.LogError("13KRA caustics DTO layout audit failed.");
- Assets\_Project\Scripts\Rendering\WaterOptics\Editor\AbyssalOpticsTunerWindow.cs:139:        private void Update()
- Assets\_Project\Scripts\Rendering\WaterOptics\Editor\WaterOpticsLayoutValidator.cs:30:                Debug.Log(report);
- Assets\_Project\Scripts\Rendering\WaterOptics\Editor\WaterOpticsLayoutValidator.cs:34:            Debug.LogError(report);
- Assets\_Project\Scripts\Rendering\Editor\HectonUberNoirMaterialConsolidator.cs:48:            Debug.Log($"[UberNoir] Consolidated {converted} hard-surface materials into {TargetShaderName}.");
- Assets\_Project\Scripts\Rendering\WaterOptics\Editor\PostProcess_Fog_Scanner.cs:22:            Debug.Log(string.Concat("Water optics fog scanner wrote ", reportPath));
- Assets\_Project\Scripts\Graphics\Materials\Editor\VisualPressureAgingTunerWindow.cs:141:            _runtimeLabel.text = bound ? "Runtime: GlobalDataVault bound, active " + activeCount : "Runtime: Play Mode bridge pending";
- Assets\_Project\Scripts\Graphics\Materials\Editor\VisualPressureAgingTunerWindow.cs:142:            _uploadLabel.text = "Upload: " + uploadUs.ToString("0.0") + " us";
- Assets\_Project\Scripts\Graphics\Materials\Editor\VisualPressureAgingTunerWindow.cs:143:            _flagsLabel.text = "Flags: 0x" + flags.ToString("X8");
- Assets\_Project\Scripts\Graphics\Materials\Editor\VisualPressureAgingTunerWindow.cs:310:            int activeMaterialMutations = Count(baseDegradation, ".material") + Count(runtime, ".material") + Count(baseDegradation, "MaterialPropertyBlock");
- Assets\_Project\Scripts\Graphics\Materials\Editor\VisualPressureAgingTunerWindow.cs:323:            int projectMaterialMutationReferences = CountTokenInDirectory(root, "Assets/_Project/Scripts", "*.cs", ".material") +
- Assets\_Project\Scripts\Graphics\Materials\Editor\VisualPressureAgingTunerWindow.cs:330:            int legacyRendererMaterialSetFloat = CountTokenInDirectory(root, "Assets/_Project/Scripts/Rendering", "*.cs", "GetComponent<Renderer>().material.SetFloat") +
- Assets\_Project\Scripts\Graphics\Materials\Editor\VisualPressureAgingTunerWindow.cs:331:                CountTokenInDirectory(root, "Assets/_Project/Scripts/Construction", "*.cs", "GetComponent<Renderer>().material.SetFloat");
- Assets\_Project\Scripts\Graphics\Culling\Editor\AbyssalShadowTunerWindow.cs:112:            _statusLabel.text = _runtime != null ? "Status: runtime selected" : "Status: runtime missing";
- Assets\_Project\Scripts\Graphics\Culling\Editor\AbyssalShadowTunerWindow.cs:119:            _statusLabel.text = ok ? "Status: 50K mock pass executed" : "Status: no runtime/vault";
- Assets\_Project\Scripts\Graphics\Culling\Editor\AbyssalShadowTunerWindow.cs:126:            _statusLabel.text = ok ? "Status: Abyssal shadow DTO layouts valid" : "Status: Abyssal shadow DTO layout invalid";
- Assets\_Project\Scripts\Graphics\Culling\Editor\AbyssalShadowTunerWindow.cs:148:                _statusLabel.text = _runtime.LoadProfileCsv() ? "Status: CSV applied" : "Status: CSV not applied";
- Assets\_Project\Scripts\Graphics\Culling\Editor\AbyssalShadowTunerWindow.cs:153:                _statusLabel.text = AbyssalShadowCullingRuntime.LoadActiveProfileCsv() ? "Status: CSV applied" : "Status: CSV not applied";
- Assets\_Project\Scripts\Graphics\Culling\Editor\AbyssalShadowTunerWindow.cs:166:            _evaluatedLabel.text = "Evaluated: " + snapshot.EvaluatedCount;
- Assets\_Project\Scripts\Graphics\Culling\Editor\AbyssalShadowTunerWindow.cs:167:            _shadowCulledLabel.text = "Shadow culled: " + snapshot.ShadowCulledCount + " / dithered: " + snapshot.DitheredCount;
- Assets\_Project\Scripts\Graphics\Culling\Editor\AbyssalShadowTunerWindow.cs:168:            _darknessLabel.text = "Darkness culled: " + snapshot.DarknessCulledCount;
- Assets\_Project\Scripts\Graphics\Culling\Editor\AbyssalShadowTunerWindow.cs:169:            _pointLabel.text = "Point-light culled: " + snapshot.PointLightCulledCount;
- Assets\_Project\Scripts\Graphics\Culling\Editor\AbyssalShadowTunerWindow.cs:170:            _timingLabel.text = "Timing: " + snapshot.LastBurstWallTimeMs.ToString("0.000") + " ms / " + snapshot.LastUploadMicroseconds.ToString("0.0") + " us upload";
- Assets\_Project\Scripts\Graphics\Culling\Editor\AbyssalShadowTunerWindow.cs:194:            Debug.Log("Abyssal Shadow DTO layouts valid: state=32B, instance/counters/telemetry/runtime=64B, HZB=16B, indirect=32B.");
- Assets\_Project\Scripts\Graphics\Materials\Editor\Visual_Material_Inquisition.cs:47:            int activeMaterialMutations = Count(baseDegradation, ".material") + Count(runtime, ".material") + Count(baseDegradation, "MaterialPropertyBlock");
- Assets\_Project\Scripts\Graphics\Materials\Editor\Visual_Material_Inquisition.cs:49:            int legacyRendererMaterialSetFloat = CountTokenInDirectory(root, "Assets/_Project/Scripts/Rendering", "*.cs", "GetComponent<Renderer>().material.SetFloat") +
- Assets\_Project\Scripts\Graphics\Materials\Editor\Visual_Material_Inquisition.cs:50:                CountTokenInDirectory(root, "Assets/_Project/Scripts/Construction", "*.cs", "GetComponent<Renderer>().material.SetFloat");
- Assets\_Project\Scripts\VFX\Editor\CinematicTraumaTunerWindow.cs:111:            _traumaLabel.text = "Trauma: " + state.x.ToString("0.000");
- Assets\_Project\Scripts\VFX\Editor\CinematicTraumaTunerWindow.cs:112:            _translationLabel.text = "Max translation: " + state.y.ToString("0.0000") + " m";
- Assets\_Project\Scripts\VFX\Editor\CinematicTraumaTunerWindow.cs:113:            _signalsLabel.text = "Signals: " + ((int)state.z).ToString();
- Assets\_Project\Scripts\VFX\Editor\CinematicTraumaTunerWindow.cs:114:            _burstLabel.text = "Burst us: " + state.w.ToString("0.00");
- Assets\_Project\Scripts\VFX\Editor\CinematicTraumaTunerWindow.cs:122:            CameraJuiceSystem[] runtimes = Resources.FindObjectsOfTypeAll<CameraJuiceSystem>();
- Assets\_Project\Scripts\VFX\Editor\CinematicTraumaTunerWindow.cs:140:                _runtimeLabel.text = resolved ? "Runtime: CameraJuiceSystem" : "Runtime: unresolved";
- Assets\_Project\Scripts\VFX\Bioluminescence\Editor\BioluminescenceTunerWindow.cs:270:                    _speciesLabels[i].text = FormatSpeciesHash(tuning.SpeciesHash);
- Assets\_Project\Scripts\VFX\JacobianFoam\Editor\JacobianFoamTunerWindow.cs:181:                _statusLabel.text = value;
- Assets\_Project\Scripts\VFX\Editor\OOP_CameraShake_Scanner.cs:30:            json.AppendLine("  \"scanner\": \"Zero-dependency scoped source parser for Camera.main transform / AnimationClip / AnimationCurve / Cinemachine impulse-source / managed random camera-shake routes\",");
- Assets\_Project\Scripts\VFX\Editor\OOP_CameraShake_Scanner.cs:64:            json.AppendLine("  \"manualProof\": \"Scanner covers Camera.main.transform, CinemachineImpulse, AnimationClip, AnimationCurve, Random.insideUnitSphere, Random.Range, and hot transform.localPosition/localRotation/localEulerAngles writes. Runtime route is projection DTO output, not hierarchy mutation.\"");
- Assets\_Project\Scripts\VFX\Editor\OOP_CameraShake_Scanner.cs:68:            Debug.Log($"[SHINOBU_354] OOP camera shake scan wrote {report} with {findings} findings.");
- Assets\_Project\Scripts\VFX\Editor\OOP_CameraShake_Scanner.cs:79:            AppendIfFound(file, text, source, "Camera.main.transform", "DIRECT_CAMERA_MAIN_TRANSFORM", ref findings, ref first, json);
- Assets\_Project\Scripts\VFX\Parasites\Editor\Biological_Particle_Scanner.cs:43:                ParticleSystem[] particles = prefab.GetComponentsInChildren<ParticleSystem>(true);
- Assets\_Project\Scripts\VFX\Parasites\Editor\Biological_Particle_Scanner.cs:61:                MonoBehaviour[] behaviours = prefab.GetComponentsInChildren<MonoBehaviour>(true);
- Assets\_Project\Scripts\Lighting\Editor\DayNightGIRelayTunerWindow.cs:126:                _status.text = "Runtime: missing";
- Assets\_Project\Scripts\Lighting\Editor\DayNightGIRelayTunerWindow.cs:127:                _lighting.text = "EnvironmentLightingDTO: unavailable";
- Assets\_Project\Scripts\Lighting\Editor\DayNightGIRelayTunerWindow.cs:128:                _telemetry.text = "Telemetry: unavailable";
- Assets\_Project\Scripts\Lighting\Editor\DayNightGIRelayTunerWindow.cs:132:            _status.text = "Runtime: seq=" + _target.LastAppliedSequence +
- Assets\_Project\Scripts\Lighting\Editor\DayNightGIRelayTunerWindow.cs:139:                _lighting.text = "Ambient " + Format(dto.AmbientColor) +
- Assets\_Project\Scripts\Lighting\Editor\DayNightGIRelayTunerWindow.cs:152:                _telemetry.text = "Telemetry: ring=" + telemetry.Length + " cursor=" + cursor;
- Assets\_Project\Scripts\Lighting\Editor\AbyssalLightingTunerWindow.cs:120:                _status.text = "Runtime: missing";
- Assets\_Project\Scripts\Lighting\Editor\AbyssalLightingTunerWindow.cs:124:                _status.text = "Runtime: probes=" + tuning.ActiveProbeCount + " res=" + tuning.Resolution + " quality=" + tuning.GlobalQualityWeight.ToString("0.000");
- Assets\_Project\Scripts\Lighting\Editor\AbyssalLightingTunerWindow.cs:128:                _status.text = "Runtime: cold";
- Assets\_Project\Scripts\Lighting\Editor\AbyssalLightingTunerWindow.cs:138:            _layout.text = "CustomLightProbeDTO: " + size + " bytes lane0=" + lane0 + " lane6=" + lane6 + " b8=" + b8 + " valid=" + valid;
- Assets\_Project\Scripts\Lighting\Editor\AbyssalLightingTunerWindow.cs:147:                Renderer[] renderers = selected[i].GetComponentsInChildren<Renderer>(true);
- Assets\_Project\Scripts\Lighting\Editor\AbyssalLightingTunerWindow.cs:156:            Component[] components = Resources.FindObjectsOfTypeAll<Component>();
- Assets\_Project\Scripts\Lighting\Editor\AbyssalLightingTunerWindow.cs:169:            Debug.Log("[13KRA] Loaded-scene Unity probe group count: " + sceneGroups);
- Assets\_Project\Scripts\Lighting\Editor\OOP_Lighting_Scanner.cs:93:            Debug.Log("[13KRA] OOP lighting scanner wrote " + dedicatedReportPath);
- Assets\_Project\Scripts\Lighting\Editor\InteriorGITunerWindow.cs:164:                Renderer[] renderers = selected[i].GetComponentsInChildren<Renderer>(true);
- Assets\_Project\Scripts\Atmosphere\StormPropagation\Editor\ShinobuStormPropagationTunerWindow.cs:92:                    _status.text = "Vault state: tuning buffer unavailable";
- Assets\_Project\Scripts\Atmosphere\StormPropagation\Editor\ShinobuStormPropagationTunerWindow.cs:106:                _status.text = "Vault state: tuning buffer resolved. Graph: surface intensity vs attenuated depth energy.";
- Assets\_Project\Scripts\Atmosphere\StormPropagation\Editor\Weather_Event_Inquisition.cs:123:            Debug.Log("SHINOBU_234 Weather Event Inquisition wrote Docs/Reports/ENVIRONMENT_OPTIMIZATION_REPORT.json");
- Assets\_Project\Editor\GeminiWorldBuilder.cs:13:        Debug.Log("Gemini: Building Optimized Master-Grade World v3...");
- Assets\_Project\Editor\GeminiWorldBuilder.cs:162:        Debug.Log("Master-Grade v3 Build Complete! Organic, Spikeless 108-Biome Matrix deployed.");
- Assets\_Project\Editor\HectonLodGroupConflictResolver.cs:35:            Debug.Log($"{LogPrefix} prefabFixes={prefabFixCount}, loadedSceneFixes={loadedSceneFixCount}.");
- Assets\_Project\Editor\HectonLodGroupConflictResolver.cs:72:            LODGroup[] lodGroups = Resources.FindObjectsOfTypeAll<LODGroup>();
- Assets\_Project\Editor\HectonLodGroupConflictResolver.cs:136:            root.GetComponentsInChildren(true, s_RendererScratch);
- Assets\_Project\Editor\HectonLodGroupConflictResolver.cs:146:            Debug.LogWarning($"{LogPrefix} Missing expected renderer '{expectedName}' under '{root.name}'.");
- Assets\_Project\Editor\HectonMeshGenerator.cs:102:                Debug.Log(
- Assets\_Project\Editor\HectonMeshGenerator.cs:114:                Debug.Log(
- Assets\_Project\Editor\HectonMeshGenerator.cs:297:            Mesh mesh = new Mesh();
- Assets\_Project\Editor\Camera_Proliferation_Scanner.cs:33:            Debug.Log("Superfluous Cameras Eradicated: " + report.superfluousCamerasEradicated + " | Violations: " + report.violationCount);
- Assets\_Project\Editor\Camera_Proliferation_Scanner.cs:99:                Camera[] cameras = root.GetComponentsInChildren<Camera>(true);
- Assets\_Project\Editor\DataMonolith\DataMonolithBakerWindow.cs:28:                Debug.LogError("[DataMonolithBakerWindow] H8DataMonolithCompilerWindow.Open is missing.");
- Assets\_Project\Editor\DataMonolith\DataMonolithBakerWindow.cs:44:                Debug.LogError("[DataMonolithBakerWindow] H8DataMonolithCompiler.BakeAll is missing.");
- Assets\_Project\Editor\DataMonolith\DataMonolithBakerWindow.cs:53:                Debug.LogError("[DataMonolithBakerWindow] static_data.h8bin bake failed.");

## Exists / Missing / Required Proof

- Exists: bible routes exist and static implementation evidence was found.
- Partial: runtime static risk suspects need manual code review.
- Editor/tool: static suspects exist but may be legal if editor-only or cold-path.
- Required proof: Frame Debugger/RenderGraph capture, URP asset proof, shader variant count, material batching proof, compact/high screenshots, GPU/VRAM profiler captures.

## Next Audit Action

Classify each runtime suspect as cold-path/legal or runtime violation. Fix runtime violations before profiler proof.
