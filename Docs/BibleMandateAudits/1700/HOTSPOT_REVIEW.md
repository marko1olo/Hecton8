# Hotspot Review

Status: STATIC REVIEW PRIORITY LIST - NOT A DEFECT VERDICT
Date: 2026-06-02

This report lists files with the highest concentration of unresolved `REVIEW_*` risk lines across system reports. Read the containing methods before declaring a violation.

Total unresolved `REVIEW_*` lines: 699

## Top Files

| Count | File | Classes | Systems |
|---:|---|---|---|
| 42 | Assets\_Project\Scripts\World\VegetationNavGridSynchronizer.cs | REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 42 | 01_generated_assets, 05_physics_vehicles_water, 06_world_terrain_voxels_ecosystem |
| 36 | Assets\_Project\Scripts\World\VegetationFlowFieldIntegrator.cs | REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 36 | 01_generated_assets, 05_physics_vehicles_water, 06_world_terrain_voxels_ecosystem |
| 29 | Assets\_Project\Scripts\Core\Memory\GlobalDataVault.cs | REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 29 | 04_runtime_architecture_data_telemetry |
| 24 | Assets\_Project\Scripts\World\GroundPenetratingRadarRuntime.cs | REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 24 | 01_generated_assets, 05_physics_vehicles_water, 06_world_terrain_voxels_ecosystem |
| 20 | Assets\_Project\Scripts\UI\SettingsPanel.cs | REVIEW_CACHE_OR_INJECTION_REQUIRED: 20 | 02_ui_frontend_hud |
| 18 | Assets\_Project\Scripts\World\PersistentWorldRegistry.cs | REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 18 | 01_generated_assets, 05_physics_vehicles_water, 06_world_terrain_voxels_ecosystem |
| 18 | Assets\_Project\Scripts\World\ProceduralWreckGenerator.cs | REVIEW_RUNTIME_MESH_MATERIAL_PATH: 15; REVIEW_CACHE_OR_INJECTION_REQUIRED: 3 | 01_generated_assets, 05_physics_vehicles_water, 06_world_terrain_voxels_ecosystem |
| 17 | Assets\_Project\Scripts\Core\Memory\H8Memory.cs | REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 16; REVIEW_JOB_FENCE_REQUIRED: 1 | 04_runtime_architecture_data_telemetry |
| 16 | Assets\_Project\Scripts\Audio\NativeAudioFrameRingBuffer.cs | REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 16 | 08_ai_creatures_sonar_drones, 09_audio_narrative_presentation |
| 15 | Assets\_Project\Scripts\UI\FontAssetRecovery.cs | REVIEW_RUNTIME_MESH_MATERIAL_PATH: 15 | 02_ui_frontend_hud |
| 14 | Assets\_Project\Scripts\Scavenging\ScavengingLootOracle.cs | REVIEW_UNCLASSIFIED_STATIC_RISK: 7; REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 6; REVIEW_CACHE_OR_INJECTION_REQUIRED: 1 | 07_gameplay_construction_tools_inventory_combat |
| 12 | Assets\_Project\Scripts\World\VegetationChunkResidencyDirector.cs | REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 12 | 01_generated_assets, 05_physics_vehicles_water, 06_world_terrain_voxels_ecosystem |
| 12 | Assets\_Project\Scripts\World\HectonIndirectVegetationRenderer.cs | REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 12 | 01_generated_assets, 05_physics_vehicles_water, 06_world_terrain_voxels_ecosystem |
| 12 | Assets\_Project\Scripts\Visor\HectonBiolumSSGIFeature.cs | REVIEW_RUNTIME_MESH_MATERIAL_PATH: 12 | 02_ui_frontend_hud, 09_audio_narrative_presentation |
| 12 | Assets\_Project\Scripts\World\HectonMapMagicVegetationBridge.cs | REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 12 | 01_generated_assets, 05_physics_vehicles_water, 06_world_terrain_voxels_ecosystem |
| 11 | Assets\_Project\Scripts\Physics\GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs | REVIEW_RUNTIME_MESH_MATERIAL_PATH: 10; REVIEW_CACHE_OR_INJECTION_REQUIRED: 1 | 05_physics_vehicles_water |
| 11 | Assets\_Project\Scripts\UI\SuitHUDV4CanvasOverlay.cs | REVIEW_RUNTIME_MESH_MATERIAL_PATH: 11 | 02_ui_frontend_hud |
| 9 | Assets\_Project\Scripts\World\HectonWorldShellController1428.cs | REVIEW_CACHE_OR_INJECTION_REQUIRED: 6; REVIEW_HOT_PHASE_METHOD: 3 | 01_generated_assets, 05_physics_vehicles_water, 06_world_terrain_voxels_ecosystem |
| 9 | Assets\_Project\Scripts\World\VoxelDynamicNavGridRuntime.cs | REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 9 | 01_generated_assets, 05_physics_vehicles_water, 06_world_terrain_voxels_ecosystem |
| 9 | Assets\_Project\Scripts\World\TOOL_Procedural_Wreckage_Generator.cs | REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 9 | 01_generated_assets, 05_physics_vehicles_water, 06_world_terrain_voxels_ecosystem |
| 9 | Assets\_Project\Scripts\Fauna\FaunaBrain.cs | REVIEW_CACHE_OR_INJECTION_REQUIRED: 6; REVIEW_RUNTIME_MESH_MATERIAL_PATH: 3 | 01_generated_assets, 06_world_terrain_voxels_ecosystem, 08_ai_creatures_sonar_drones |
| 9 | Assets\_Project\Scripts\World\Outposts\MarauderOutpostGenerationService.cs | REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 6; REVIEW_RUNTIME_MESH_MATERIAL_PATH: 3 | 01_generated_assets, 05_physics_vehicles_water, 06_world_terrain_voxels_ecosystem |
| 8 | Assets\_Project\Scripts\Gameplay\SomaticKinematicsRuntime.cs | REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 8 | 07_gameplay_construction_tools_inventory_combat |
| 8 | Assets\_Project\Scripts\Ecosystem\EcosystemRuntimeInstaller.cs | REVIEW_CACHE_OR_INJECTION_REQUIRED: 8 | 06_world_terrain_voxels_ecosystem, 08_ai_creatures_sonar_drones |
| 8 | Assets\_Project\Scripts\Core\GlobalRegistry.cs | REVIEW_LOG_GUARD_REQUIRED: 8 | 04_runtime_architecture_data_telemetry |
| 7 | Assets\_Project\Scripts\UI\TopographicalSonar\TopographicalSonarSynthesizer.cs | REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 7 | 02_ui_frontend_hud |
| 6 | Assets\_Project\Scripts\Visor\HectonScooterVolumetricShaftsFeature.cs | REVIEW_RUNTIME_MESH_MATERIAL_PATH: 6 | 02_ui_frontend_hud, 09_audio_narrative_presentation |
| 6 | Assets\_Project\Scripts\Visor\HectonSonarPointCloudFeature.cs | REVIEW_RUNTIME_MESH_MATERIAL_PATH: 6 | 02_ui_frontend_hud, 09_audio_narrative_presentation |
| 6 | Assets\_Project\Scripts\Visor\HectonVolumetricParticulateFogFeature.cs | REVIEW_RUNTIME_MESH_MATERIAL_PATH: 6 | 02_ui_frontend_hud, 09_audio_narrative_presentation |
| 6 | Assets\_Project\Scripts\Construction\AutonomousExtractorSystem.cs | REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 6 | 07_gameplay_construction_tools_inventory_combat |
| 6 | Assets\_Project\Scripts\World\Biomes\BiomeBoundarySdfRuntime.cs | REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 6 | 01_generated_assets, 05_physics_vehicles_water, 06_world_terrain_voxels_ecosystem |
| 6 | Assets\_Project\Scripts\World\GPUScatterDirector.cs | REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 6 | 01_generated_assets, 05_physics_vehicles_water, 06_world_terrain_voxels_ecosystem |
| 6 | Assets\_Project\Scripts\World\ImpostorSystem.cs | REVIEW_CACHE_OR_INJECTION_REQUIRED: 6 | 01_generated_assets, 05_physics_vehicles_water, 06_world_terrain_voxels_ecosystem |
| 6 | Assets\_Project\Scripts\Atmosphere\ShinobuOceanSurfaceAtmosphereRuntime.cs | REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 6 | 03_rendering_visuals, 05_physics_vehicles_water, 06_world_terrain_voxels_ecosystem |
| 6 | Assets\_Project\Scripts\World\ResourceDistributionDirector.cs | REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 6 | 01_generated_assets, 05_physics_vehicles_water, 06_world_terrain_voxels_ecosystem |
| 6 | Assets\_Project\Scripts\World\SargassumMicroFaunaBoids.cs | REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 6 | 01_generated_assets, 05_physics_vehicles_water, 06_world_terrain_voxels_ecosystem |
| 6 | Assets\_Project\Scripts\Visor\VolumetricLightFeature.cs | REVIEW_RUNTIME_MESH_MATERIAL_PATH: 6 | 02_ui_frontend_hud, 09_audio_narrative_presentation |
| 6 | Assets\_Project\Scripts\Atmosphere\BaseAtmosphereLogisticsEditor.cs | REVIEW_UNCLASSIFIED_STATIC_RISK: 6 | 03_rendering_visuals, 05_physics_vehicles_water, 06_world_terrain_voxels_ecosystem |
| 6 | Assets\_Project\Scripts\Atmosphere\GasDynamicsSolver.cs | REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 6 | 03_rendering_visuals, 05_physics_vehicles_water, 06_world_terrain_voxels_ecosystem |
| 5 | Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs | REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 5 | 10_persistence_streaming_release_platform |

## First Review Targets

- Prioritize files with `REVIEW_RUNTIME_MESH_MATERIAL_PATH`, `REVIEW_SYNC_LOAD_VIOLATION_CANDIDATE`, `REVIEW_JOB_FENCE_REQUIRED`, and `REVIEW_CACHE_OR_INJECTION_REQUIRED` before generic log guard work.
- If a hotspot is setup-only, add explicit comments/guards or move it to an editor/bootstrap route so future audits do not keep flagging it as ambiguous.
