# Runtime Preclassification - Rendering, Shaders, Lighting, VFX, Water Presentation

Status: HEURISTIC FIRST PASS - MANUAL REVIEW STILL REQUIRED
Date: 2026-06-02

This file groups static runtime suspects by a conservative heuristic. It can reduce review time, but it cannot prove a line is legal or illegal without reading the containing method and owner phase.

Total runtime suspects: 109.

## Summary

- LEGAL_EDITOR_OR_DEV_GUARDED: 50
- LIKELY_LEGAL_COLD_OR_OWNER_LIFETIME_PATH: 39
- REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 13
- REVIEW_UNCLASSIFIED_STATIC_RISK: 5
- LIKELY_LEGAL_COLD_OR_DIAGNOSTIC_PATH: 1
- REVIEW_RUNTIME_MESH_MATERIAL_PATH: 1

## LEGAL_EDITOR_OR_DEV_GUARDED (50)

- Runtime debug logging | Assets\_Project\Scripts\Rendering\LutArrayResolver.cs:501:            Hecton8.Core.H8Debug.LogWarning("[LutArrayResolver] Water_Extinction_Matrix.bin not found. Using analytical Beer-Lambert fallback.");
- Runtime debug logging | Assets\_Project\Scripts\Rendering\LutArrayResolver.cs:508:            Hecton8.Core.H8Debug.LogWarning("[LutArrayResolver] Failed to load Water_Extinction_Matrix.bin: " + exception.Message);
- Runtime debug logging | Assets\_Project\Scripts\Rendering\LutArrayResolver.cs:515:            Hecton8.Core.H8Debug.LogWarning("[LutArrayResolver] Invalid Water_Extinction_Matrix.bin byte count: " + byteCount);
- Runtime debug logging | Assets\_Project\Scripts\Rendering\LutArrayResolver.cs:522:            Hecton8.Core.H8Debug.LogWarning("[LutArrayResolver] Invalid extinction LUT texel count: " + texelCount);
- Runtime debug logging | Assets\_Project\Scripts\Rendering\LutArrayResolver.cs:529:            Hecton8.Core.H8Debug.LogWarning("[LutArrayResolver] R16G16B16A16_SFloat sampling is unsupported; packed R16 path remains active when available.");
- Runtime debug logging | Assets\_Project\Scripts\Rendering\LutArrayResolver.cs:536:            Hecton8.Core.H8Debug.LogWarning("[LutArrayResolver] StreamingAssets URI staging failed: " + error);
- Runtime debug logging | Assets\_Project\Scripts\Rendering\LutArrayResolver.cs:543:            Hecton8.Core.H8Debug.LogWarning("[LutArrayResolver] Portable or low-memory target detected. Using analytical Beer-Lambert fallback instead of streaming Water_Extinction_Matrix.bin.");
- Runtime debug logging | Assets\_Project\Scripts\Rendering\GlobalShaderDispatcher.cs:1394:                Hecton8.Core.H8Debug.LogWarning("[GlobalShaderDispatcher] CSV override parse failed: " + exception.Message);
- Runtime debug logging | Assets\_Project\Scripts\Rendering\BilateralDrs\HectonBilateralDrsUpscalerFeature.cs:389:                        Hecton8.Core.H8Debug.LogError("[13KRA] Bilateral DRS compute shader is missing a clear edge-mask kernel.");
- Runtime debug logging | Assets\_Project\Scripts\Rendering\BilateralDrs\HectonBilateralDrsUpscalerFeature.cs:422:                        Hecton8.Core.H8Debug.LogError("[13KRA] Bilateral DRS compute shader is missing one or more active upscaler kernels.");
- Runtime debug logging | Assets\_Project\Scripts\VFX\BiomeProfile.cs:68:                Hecton8.Core.H8Debug.LogWarning("[BiomeProfile] High AOIntensity may impact performance.");
- Runtime debug logging | Assets\_Project\Scripts\VFX\CameraJuiceSystem_CameraJuiceBurst.cs:184:                Hecton8.Core.H8Debug.LogError("[SHINOBU_354] Camera juice ABI violation.");
- Runtime debug logging | Assets\_Project\Scripts\VFX\CameraJuiceSystem.cs:861:            Hecton8.Core.H8Debug.LogError("[CameraJuiceSystem] Duplicate instance detected. Destroying duplicate.");
- Runtime debug logging | Assets\_Project\Scripts\VFX\CameraJuiceSystem.cs:869:            Hecton8.Core.H8Debug.LogError("[CameraJuiceSystem] MainCamera not found. System disabled.");
- Runtime debug logging | Assets\_Project\Scripts\VFX\CameraJuiceSystem.cs:877:            Hecton8.Core.H8Debug.LogError("[CameraJuiceSystem] URPVolume not found. Post-processing disabled.");
- Runtime debug logging | Assets\_Project\Scripts\VFX\CameraJuiceSystem.cs:885:            Hecton8.Core.H8Debug.Log("[CameraJuiceSystem] Vignette override not found in Volume profile. Health vignette presentation disabled.");
- Runtime debug logging | Assets\_Project\Scripts\VFX\CameraJuiceSystem.cs:893:            Hecton8.Core.H8Debug.Log("[CameraJuiceSystem] DepthOfField override not found in Volume profile. Depth-of-field presentation disabled.");
- Runtime debug logging | Assets\_Project\Scripts\VFX\CameraJuiceSystem.cs:901:            Hecton8.Core.H8Debug.LogError("[CameraJuiceSystem] Shake calculation failed.");
- Runtime debug logging | Assets\_Project\Scripts\VFX\CameraJuiceSystem.cs:909:            Hecton8.Core.H8Debug.LogError("[CameraJuiceSystem] TriggerShake called with null profile.");
- Runtime debug logging | Assets\_Project\Scripts\VFX\CameraJuiceSystem.cs:917:            Hecton8.Core.H8Debug.LogWarning("[CameraJuiceSystem] ShakeProfile MaxDisplacement out of range [0, 1]. Clamping.");
- Runtime debug logging | Assets\_Project\Scripts\VFX\CameraJuiceSystem.cs:925:            Hecton8.Core.H8Debug.LogWarning("[CameraJuiceSystem] ShakeProfile Duration invalid. Using default 0.5s.");
- Runtime debug logging | Assets\_Project\Scripts\VFX\CameraJuiceSystem.cs:933:            Hecton8.Core.H8Debug.LogWarning("[CameraJuiceSystem] TransitionToBiome called with null biome. Using default fallback.");
- Runtime debug logging | Assets\_Project\Scripts\VFX\CameraJuiceSystem.cs:941:            Hecton8.Core.H8Debug.LogError("[CameraJuiceSystem] FOV calculation failed.");
- Runtime debug logging | Assets\_Project\Scripts\VFX\CameraJuiceSystem.cs:949:            Hecton8.Core.H8Debug.LogError("[CameraJuiceSystem] Biome blend failed.");
- Runtime debug logging | Assets\_Project\Scripts\VFX\CameraJuiceSystem.cs:957:            Hecton8.Core.H8Debug.LogError("[CameraJuiceSystem] Interaction focus calculation failed.");
- Runtime debug logging | Assets\_Project\Scripts\VFX\CameraJuiceSystem.cs:965:            Hecton8.Core.H8Debug.LogWarning("[CameraJuiceSystem] Frame time exceeded budget.");
- Runtime debug logging | Assets\_Project\Scripts\VFX\CameraJuiceSystem.cs:973:            Hecton8.Core.H8Debug.LogError("[CameraJuiceSystem] Health post-processing failed.");
- Runtime debug logging | Assets\_Project\Scripts\VFX\HectonMarineSnowRenderer.cs:2845:            Hecton8.Core.H8Debug.LogError("HectonMarineSnowRenderer: compute kernel CSMain not found. Disabling compute marine snow.");
- Runtime debug logging | Assets\_Project\Scripts\VFX\HectonMarineSnowRenderer.cs:2853:            Hecton8.Core.H8Debug.LogError("HectonMarineSnowRenderer: compute kernel InitializeParticles not found. Disabling compute marine snow.");
- Runtime debug logging | Assets\_Project\Scripts\VFX\HectonMarineSnowRenderer.cs:2861:            Hecton8.Core.H8Debug.LogError("HectonMarineSnowRenderer: compute kernel ClearVisibleParticles not found. Disabling compute marine snow.");
- Runtime debug logging | Assets\_Project\Scripts\VFX\HectonMarineSnowRenderer.cs:2869:            Hecton8.Core.H8Debug.LogError("HectonMarineSnowRenderer: auxiliary compute kernels not found. Disabling compute marine snow.");
- Runtime debug logging | Assets\_Project\Scripts\VFX\HectonMarineSnowRenderer.cs:2877:            Hecton8.Core.H8Debug.LogError("HectonMarineSnowRenderer: propwash compute kernels not found. Disabling compute marine snow.");
- Runtime debug logging | Assets\_Project\Scripts\VFX\HectonMarineSnowRenderer.cs:2885:            Hecton8.Core.H8Debug.LogError("HectonMarineSnowRenderer: compute kernel thread-group contract is invalid. Disabling compute marine snow.");
- Runtime debug logging | Assets\_Project\Scripts\VFX\Bioluminescence\BiolumPulseSyncRuntime.cs:1472:            Hecton8.Core.H8Debug.LogError(message);
- Runtime debug logging | Assets\_Project\Scripts\VFX\ShakeProfile.cs:52:                Hecton8.Core.H8Debug.LogWarning("[ShakeProfile] Invalid Duration. Clamping to 0.5s.");
- Runtime debug logging | Assets\_Project\Scripts\VFX\Parasites\ParasiteSwarmGpuRuntime.cs:130:                Hecton8.Core.H8Debug.LogError("SHINOBU_313 ParasiteSwarm layout rejection code " + failureCode);
- Runtime debug logging | Assets\_Project\Scripts\Lighting\HectonGIRelaySystem.cs:966:                Hecton8.Core.H8Debug.LogException(exception, this);
- Runtime debug logging | Assets\_Project\Scripts\Lighting\HectonLightingRuntime_DayNightRelay.cs:608:                Hecton8.Core.H8Debug.LogException(exception, this);
- Runtime debug logging | Assets\_Project\Scripts\Lighting\InteriorGIProbeVolumeRuntime.cs:1693:                    Hecton8.Core.H8Debug.LogWarning("Interior GI CSV rejected rows: " + rowsRejected);
- Runtime debug logging | Assets\_Project\Scripts\Lighting\InteriorGIProbeVolumeRuntime.cs:1699:                Hecton8.Core.H8Debug.LogWarning("Interior GI CSV reload failed: " + ex.Message);
- Additional lines omitted here: 10. Use `../_scans/03_rendering_visuals_runtime_risks.txt` for the full list.

## LIKELY_LEGAL_COLD_OR_OWNER_LIFETIME_PATH (39)

- Native allocation or persistent lifetime | Assets\_Project\Scripts\Rendering\BilateralDrs\HectonBilateralDrsUpscalerRuntime.cs:2118:            NativeArray<byte> payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Rendering\AbyssalCaustics\AbyssalDeferredCausticsRuntime.cs:1925:            NativeArray<byte> payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Rendering\Scatter\AbyssalScatterBrgDataVaultBootstrap.cs:322:            payload.Matrices = new NativeArray<Matrix4x4>(header.MatrixCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Rendering\Scatter\AbyssalScatterBrgDataVaultBootstrap.cs:323:            payload.Metadata = new NativeArray<GpuScatterFloraInstanceData>(header.MetadataCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Rendering\Scatter\AbyssalScatterBrgDataVaultBootstrap.cs:324:            payload.QualityIndices = new NativeArray<int>(header.QualityIndexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Rendering\Scatter\AbyssalScatterBrgDataVaultBootstrap.cs:486:            NativeArray<byte> seen = new NativeArray<byte>(count, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonRuntime.cs:509:                _buffers.MockVisibleInstances = new NativeArray<PoiTransformDTO>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonRuntime.cs:510:                _buffers.SortScratch = new NativeArray<PoiTransformDTO>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonRuntime.cs:511:                _buffers.MeshVertexCounts = new NativeArray<uint>(256, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonRuntime.cs:512:                _buffers.RadixHistogram = new NativeArray<int>(RadixHistogramCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonRuntime.cs:513:                _buffers.VisibleCountOut = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonRuntime.cs:514:                _buffers.MockQualitySignal = new NativeArray<TBDRMockQualityWeightSignal>(1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonRuntime.cs:515:                _buffers.MockCamera = new NativeArray<MockCameraMatrix>(1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonRuntime.cs:516:                _buffers.SourceFrustumPlanes = new NativeArray<float4>(6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonRuntime.cs:517:                _buffers.SqueezedFrustumPlanes = new NativeArray<float4>(6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonRuntime.cs:518:                _buffers.HzbVisibilityMask = new NativeArray<int>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonRuntime.cs:519:                _buffers.IndirectDrawArgs = new NativeArray<TBDRIndirectDrawArgsDTO>(1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonTypes.cs:477:            VertexBudgetCounters = new NativeArray<TBDRVertexBudgetCounter64>(BudgetCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: NativeArray<TBDRVertexBudgetCounter64>[BudgetCount] - 64B budget caps - owner: SHINOBU_45_TBDR_PIPELINE
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonTypes.cs:478:            TileWarnings = new NativeArray<TileSpillWarningDTO>(WarningCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<TileSpillWarningDTO>[WarningCount] - tile-spill warning lane - owner: SHINOBU_45_TBDR_PIPELINE
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonTypes.cs:479:            TransparentQuadCount = new NativeArray<int>(TransparentCounterCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<int>[TransparentCounterCount] - transparent overdraw counters - owner: SHINOBU_45_TBDR_PIPELINE
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonTypes.cs:480:            TelemetryRing = new NativeArray<TBDRPipelineTelemetryEntry>(TelemetryCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<TBDRPipelineTelemetryEntry>[TelemetryCapacity] - 300-frame black-box ring - owner: SHINOBU_45_TBDR_PIPELINE
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonTypes.cs:518:                VertexBudgetCounters = new NativeArray<TBDRVertexBudgetCounter64>(BudgetCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonTypes.cs:519:                TileWarnings = new NativeArray<TileSpillWarningDTO>(WarningCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonTypes.cs:520:                TransparentQuadCount = new NativeArray<int>(TransparentCounterCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonTypes.cs:521:                TelemetryRing = new NativeArray<TBDRPipelineTelemetryEntry>(TelemetryCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonTypes.cs:1291:                SliceTable = new NativeArray<TextureStreamingSliceDTO>(SliceCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: fixed texture array residency table; production should pass GlobalDataVault
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Graphics\Culling\TBDRPipelineSurgeonTypes.cs:1719:            Ring = new NativeArray<TBDRPipelineTelemetryEntry>(RingCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<TBDRPipelineTelemetryEntry>[300] - TBDR black-box ring - owner: SHINOBU_45_TBDR_TELEMETRY
- Native allocation or persistent lifetime | Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:248:                densities = new NativeArray<sbyte>(GridCellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:249:                decompressed = new NativeArray<sbyte>(GridCellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:250:                stats = new NativeArray<int>(StatsLength, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:251:                writtenCount = new NativeArray<int>(CounterLength, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:679:            densities = new NativeArray<sbyte>(GridCellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:680:            decompressed = new NativeArray<sbyte>(GridCellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:681:            accumulator = new NativeArray<int>(GridCellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:682:            removedMass = new NativeArray<int>(CounterLength, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:683:            debrisCount = new NativeArray<int>(CounterLength, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:684:            stats = new NativeArray<int>(StatsLength, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:685:            writtenCount = new NativeArray<int>(CounterLength, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:686:            particles = new NativeArray<DebrisParticleDTO>(ShinobuDeltaCrusher.MaximumQualityDebrisCap, Allocator.TempJob, NativeArrayOptions.ClearMemory);

## REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED (13)

- Native allocation or persistent lifetime | Assets\_Project\Scripts\Rendering\Scatter\GpuScatterLodManager.cs:1682:            _visibleCountReadback.Data = new NativeArray<uint>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Rendering\Scatter\GpuScatterLodManager.cs:1684:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Graphics\Culling\InstanceCullingService.cs:895:            _indirectArgsReadback.Data = new NativeArray<uint>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Graphics\Culling\InstanceCullingService.cs:897:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\VFX\Bioluminescence\BiolumPulseSyncRuntime.cs:316:                    entries = new NativeArray<BiolumPulseTelemetryEntry>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\VFX\Bioluminescence\BiolumPulseSyncRuntime.cs:318:                        Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\VFX\PlasmaBeam\ShinobuPlasmaBeamRuntime.cs:1486:                payload = new NativeArray<byte>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Atmosphere\GasDynamicsSolver.cs:1746:            scratch = new NativeArray<GasDynamicsTelemetryEntry>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Atmosphere\GasDynamicsSolver.cs:1748:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Atmosphere\ShinobuOceanSurfaceAtmosphereRuntime.cs:1789:            data = new NativeArray<float4>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Atmosphere\ShinobuOceanSurfaceAtmosphereRuntime.cs:1791:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Atmosphere\StormPropagation\ShinobuStormPropagationRuntime.cs:144:                array = new NativeArray<T>(length, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Environment\WeatherEvents.cs:45:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;

## REVIEW_UNCLASSIFIED_STATIC_RISK (5)

- Uncategorized | Assets\_Project\Scripts\Atmosphere\BaseAtmosphereLogisticsEditor.cs:146:                _status.text =
- Uncategorized | Assets\_Project\Scripts\Atmosphere\BaseAtmosphereLogisticsEditor.cs:156:                _status.text = "No atmosphere telemetry yet.";
- Uncategorized | Assets\_Project\Scripts\Atmosphere\ShinobuAtmosphereWaveTunerWindow.cs:150:            _statusLabel.text = ShinobuOceanSurfaceAtmosphereRuntime.TryGetVaultSnapshot(out _, out _, out _)
- Uncategorized | Assets\_Project\Scripts\Environment\HectonSeismicTideDirector.cs:5362:                    _statusLabel.text = "Play Mode and GlobalDataVault required.";
- Uncategorized | Assets\_Project\Scripts\Environment\HectonSeismicTideDirector.cs:5410:                _statusLabel.text = "Vault live. Layout: CelestialStateDTO 64B, EnvironmentStateDTO 64B, CelestialTelemetryEntry 64B.";

## LIKELY_LEGAL_COLD_OR_DIAGNOSTIC_PATH (1)

- Runtime debug logging | Assets\_Project\Scripts\Rendering\Scatter\AbyssalScatterBrgDataVaultBootstrap.cs:606:            Debug.LogWarning(message, context);

## REVIEW_RUNTIME_MESH_MATERIAL_PATH (1)

- Runtime mesh/material mutation | Assets\_Project\Scripts\VFX\Debris\CarveDebrisComputeRenderer.cs:2393:            mesh.RecalculateNormals();
