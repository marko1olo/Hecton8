# DataVault Sovereignty Audit - VAULT_SOVEREIGNTY_ENFORCER

Schema: `hecton8.datavault_sovereignty_audit.v3`
Status: `BLOCKED_BASELINE_MISSING`
Source root: `Assets/_Project/Scripts`
Pattern: `\bnew\s+NativeArray\s*<`
Baseline: `Docs/AgentLogs/DataVaultSovereigntyBaseline_VAULT_SOVEREIGNTY_ENFORCER.json`

## Summary

| Metric | Count |
|---|---:|
| Total direct `new NativeArray<T>` constructors | 892 |
| Allowed allocator-internal constructors | 575 |
| Forbidden system constructors | 317 |
| Runtime forbidden constructors | 258 |
| Editor/offline forbidden constructors | 29 |
| Editor/offline transient scratch constructors | 569 |
| Files with forbidden constructors | 116 |
| Editor/offline session scratch declarations | 0 |
| Editor/offline persistent preview declarations | 0 |
| Total field-like `NativeArray<T>` declarations | 5796 |
| Allowed DataVault/H8Memory declarations | 5271 |
| Forbidden system declarations | 525 |
| Persistent owner native collection declarations | 253 |
| Job input native collection declarations | 4609 |
| Burst job input native collection declarations | 4609 |
| Native view/payload/kernel struct declarations | 640 |
| Unknown struct native collection declarations | 294 |
| Files with forbidden declarations | 122 |

## Current Forbidden Constructors By Execution Surface

| Surface | Count |
|---|---:|
| `Dev` | 2 |
| `Editor` | 27 |
| `Plugin` | 30 |
| `Runtime` | 258 |

## Current Forbidden Constructors By Allocator

| Allocator | Count |
|---|---:|
| `Persistent` | 152 |
| `Temp` | 63 |
| `TempJob` | 63 |
| `Unknown` | 39 |

## Editor/Offline Forbidden Constructors By Allocator

| Allocator | Count |
|---|---:|
| `Persistent` | 19 |
| `Unknown` | 10 |

## Top 40 Forbidden Files

| Count | Path | Lines |
|---:|---|---|
| 30 | `Assets/_Project/Scripts/SaveBinaryStorage.cs` | 627, 1581, 3624, 4758, 5122, 5249, 5326, 5327, ... |
| 22 | `Assets/_Project/Scripts/HectonWorldGenerator.cs` | 742, 743, 744, 745, 746, 747, 748, 1389, ... |
| 13 | `Assets/_Project/Scripts/EncounterDirector.cs` | 366, 367, 368, 369, 370, 371, 372, 373, ... |
| 11 | `Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonRuntime.cs` | 509, 510, 511, 512, 513, 514, 515, 516, ... |
| 10 | `Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs` | 477, 478, 479, 480, 518, 519, 520, 521, ... |
| 10 | `Assets/_Project/Scripts/Plugins/MapMagic/HectonAnomalyMapMagicNode.cs` | 221, 222, 223, 224, 225, 226, 227, 228, ... |
| 9 | `Assets/_Project/Scripts/InventoryGrid.cs` | 94, 95, 96, 97, 98, 99, 100, 101, ... |
| 8 | `Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs` | 902, 907, 912, 917, 922, 927, 932, 937 |
| 7 | `Assets/_Project/Scripts/Plugins/MapMagic/HectonSpaceEngine098MapMagicNodes.cs` | 108, 109, 258, 259, 260, 417, 418 |
| 6 | `Assets/_Project/Scripts/Construction/AutonomousExtractorSystem.cs` | 144, 146, 148, 150, 152, 154 |
| 6 | `Assets/_Project/Scripts/Rendering/Scatter/AbyssalScatterBrgDataVaultBootstrap.cs` | 419, 420, 421, 582, 626, 627 |
| 6 | `Assets/_Project/Scripts/Scavenging/ScavengingLootOracle.cs` | 961, 966, 971, 976, 981, 986 |
| 6 | `Assets/_Project/Scripts/World/VegetationDensityQueryService.cs` | 158, 163, 168, 573, 581, 590 |
| 6 | `Assets/_Project/Scripts/WorldProceduralScatterWorkingMemory.cs` | 161, 167, 168, 169, 170, 580 |
| 5 | `Assets/_Project/Scripts/Core/UIStateStore.cs` | 156, 157, 158, 159, 160 |
| 5 | `Assets/_Project/Scripts/Editor/LSystemGenomeLabWindow.cs` | 288, 289, 290, 291, 292 |
| 5 | `Assets/_Project/Scripts/Plugins/MapMagic/HectonHydraulicErosionMapMagicNode.cs` | 272, 273, 274, 275, 276 |
| 5 | `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 5723, 5724, 5725, 5790, 8907 |
| 4 | `Assets/_Project/Scripts/Core/Contracts/CoreLowLevelUtilities.cs` | 115, 324, 325, 326 |
| 4 | `Assets/_Project/Scripts/Input/ControlRemapper.cs` | 142, 244, 370, 380 |
| 4 | `Assets/_Project/Scripts/Plugins/MapMagic/HectonTerrainSplatmapMapMagicNode.cs` | 135, 136, 137, 138 |
| 4 | `Assets/_Project/Scripts/SaveSidecarStorage.cs` | 56, 110, 158, 227 |
| 4 | `Assets/_Project/Scripts/World/BiotaDensityMapBaker/Editor/BiotaDensityBakePipeline.cs` | 202, 314, 447, 559 |
| 4 | `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs` | 1559, 1560, 2576, 4575 |
| 4 | `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs` | 5319, 6489, 6567, 8045 |
| 3 | `Assets/_Project/Scripts/FlowFieldVisualizer.cs` | 776, 777, 958 |
| 3 | `Assets/_Project/Scripts/Quest/QuestStateManager.cs` | 194, 410, 415 |
| 3 | `Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs` | 336, 3018, 3993 |
| 3 | `Assets/_Project/Scripts/WorldProceduralFieldSampler.cs` | 2577, 2578, 2579 |
| 2 | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs` | 1444, 5047 |
| 2 | `Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs` | 966, 967 |
| 2 | `Assets/_Project/Scripts/Core/ConnectionSplineBatchRenderer.cs` | 933, 948 |
| 2 | `Assets/_Project/Scripts/Core/GlobalTelemetryBus.cs` | 762, 773 |
| 2 | `Assets/_Project/Scripts/Editor/ChroniclerDiagnosticHeatmapWindow.cs` | 122, 124 |
| 2 | `Assets/_Project/Scripts/Editor/SignalTrafficMonitorWindow.cs` | 83, 85 |
| 2 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs` | 1886, 2793 |
| 2 | `Assets/_Project/Scripts/Habitat/Deformation/Editor/DamageBake/HabitatDamageBakePipeline.cs` | 693, 1418 |
| 2 | `Assets/_Project/Scripts/HectonFluidEngine.cs` | 374, 415 |
| 2 | `Assets/_Project/Scripts/ModdingAPI/ModRuntimeState.cs` | 307, 372 |
| 2 | `Assets/_Project/Scripts/Plugins/MapMagic/HectonBiomeMatrixMapMagicPostProcessNode.cs` | 71, 72 |

## Top 40 Forbidden Declaration Files

| Count | Path | Lines |
|---:|---|---|
| 20 | `Assets/_Project/Scripts/QA/Headless/JacobiStressFuzzer/PowerGridJacobiStressFuzzer.cs` | 551, 552, 553, 554, 555, 556, 557, 558, ... |
| 19 | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs` | 186, 187, 188, 189, 190, 191, 192, 193, ... |
| 19 | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs` | 120, 121, 122, 123, 124, 125, 126, 127, ... |
| 18 | `Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs` | 194, 195, 196, 197, 198, 199, 200, 201, ... |
| 17 | `Assets/_Project/Scripts/Inventory/InventoryDefragJob.cs` | 29, 30, 31, 32, 33, 34, 35, 36, ... |
| 16 | `Assets/_Project/Scripts/SaveSystem/VoxelDeltaCompressionArchitecture.cs` | 177, 178, 179, 180, 181, 182, 183, 184, ... |
| 15 | `Assets/_Project/Scripts/Tools/ToolKinematics/ToolKinematicsRuntime.cs` | 1523, 1524, 1525, 1526, 1527, 1528, 1529, 1530, ... |
| 14 | `Assets/_Project/Scripts/Core/DodReplayRecorder.cs` | 1879, 1880, 1881, 1882, 1883, 1884, 1885, 1886, ... |
| 13 | `Assets/_Project/Scripts/EncounterDirector.cs` | 310, 311, 312, 313, 314, 315, 316, 317, ... |
| 13 | `Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs` | 217, 218, 219, 220, 221, 222, 223, 224, ... |
| 12 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs` | 114, 115, 116, 117, 118, 119, 120, 121, ... |
| 12 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs` | 2994, 2995, 2996, 2997, 2998, 2999, 3000, 3001, ... |
| 12 | `Assets/_Project/Scripts/SaveManager.cs` | 229, 230, 231, 232, 234, 235, 236, 237, ... |
| 12 | `Assets/_Project/Scripts/World/GlobalWorldSampler.cs` | 232, 233, 234, 235, 236, 237, 238, 239, ... |
| 11 | `Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonRuntime.cs` | 75, 76, 77, 78, 79, 80, 81, 82, ... |
| 11 | `Assets/_Project/Scripts/SaveSystem/SaveStateMerkleTree.cs` | 192, 193, 194, 195, 196, 197, 198, 199, ... |
| 10 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs` | 1723, 1724, 1725, 1726, 1727, 1728, 1729, 1730, ... |
| 10 | `Assets/_Project/Scripts/WorldProceduralScatterWorkingMemory.cs` | 26, 27, 28, 29, 30, 62, 68, 69, ... |
| 9 | `Assets/_Project/Scripts/InventoryGrid.cs` | 53, 54, 55, 56, 57, 58, 59, 60, ... |
| 9 | `Assets/_Project/Scripts/SaveBinaryStorage.cs` | 2043, 2044, 2045, 2046, 2047, 2048, 2049, 2050, ... |
| 9 | `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 1986, 1987, 1988, 1989, 1990, 1991, 1992, 1993, ... |
| 8 | `Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs` | 872, 873, 874, 875, 876, 877, 878, 879 |
| 8 | `Assets/_Project/Scripts/SaveSystem/H8BinaryWorldPager.cs` | 3101, 3102, 3103, 3104, 3105, 3106, 3107, 3108 |
| 8 | `Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs` | 53, 54, 55, 56, 57, 58, 59, 60 |
| 8 | `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchBakePipeline.cs` | 246, 247, 248, 249, 250, 251, 252, 254 |
| 7 | `Assets/_Project/Scripts/HectonWorldGenerator.cs` | 688, 689, 690, 691, 692, 693, 694 |
| 7 | `Assets/_Project/Scripts/LocRegistry.cs` | 510, 511, 512, 513, 514, 515, 526 |
| 7 | `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 2063, 2065, 2066, 2067, 2068, 2069, 2070 |
| 6 | `Assets/_Project/Scripts/Construction/AutonomousExtractorSystem.cs` | 113, 114, 115, 116, 117, 118 |
| 6 | `Assets/_Project/Scripts/Core/Origin/AupOriginShiftCoordinator.cs` | 59, 60, 61, 62, 63, 64 |
| 6 | `Assets/_Project/Scripts/Scavenging/ScavengingLootOracle.cs` | 930, 931, 932, 933, 934, 935 |
| 6 | `Assets/_Project/Scripts/UI/TopographicalSonar/TopographicalSonarSynthesizer.cs` | 450, 451, 452, 453, 454, 455 |
| 5 | `Assets/_Project/Scripts/Core/UIStateStore.cs` | 105, 106, 107, 108, 109 |
| 5 | `Assets/_Project/Scripts/Editor/LSystemGenomeLabWindow.cs` | 17, 18, 19, 20, 21 |
| 5 | `Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentJobs.cs` | 437, 438, 439, 440, 441 |
| 5 | `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs` | 325, 326, 327, 328, 329 |
| 5 | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs` | 520, 521, 522, 523, 524 |
| 5 | `Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeVaultRuntime.cs` | 19, 20, 21, 22, 23 |
| 5 | `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 2016, 2017, 2018, 2019, 2020 |
| 4 | `Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs` | 455, 456, 457, 458 |

## Allowed Allocator-Internal Sites

| Count | Path | Lines |
|---:|---|---|
| 79 | `Assets/_Project/Scripts/Player/Movement/Editor/ZeroGMovementEditTests1600.cs` | 33, 59, 83, 84, 85, 86, 87, 88, ... |
| 56 | `Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs` | 153, 154, 155, 320, 321, 397, 398, 399, ... |
| 21 | `Assets/_Project/Scripts/SaveSystem/WalIntegrityFuzzerCore.cs` | 178, 221, 222, 223, 224, 340, 719, 720, ... |
| 21 | `Assets/_Project/Scripts/World/BiomeWeightMapBaker/Editor/BiomeWeightMapBakePipeline.cs` | 176, 177, 178, 179, 180, 181, 183, 184, ... |
| 18 | `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/Shinobu213/OfflineGeometryBaker.cs` | 401, 461, 475, 477, 479, 481, 557, 670, ... |
| 17 | `Assets/_Project/Scripts/World/OfflineWreckageBaker/Editor/WreckageForgeWindow.cs` | 375, 376, 377, 378, 379, 453, 454, 455, ... |
| 13 | `Assets/_Project/Scripts/Editor/ErosionTestHarness.cs` | 84, 85, 86, 87, 88, 93, 94, 271, ... |
| 12 | `Assets/_Project/Scripts/VFX/Debris/ShinobuVoxelSculptorWindow.cs` | 248, 249, 250, 251, 679, 680, 681, 682, ... |
| 12 | `Assets/_Project/Scripts/World/StaticCaveSdfBaker/Editor/StaticCaveSdfBakePipeline.cs` | 154, 225, 226, 227, 228, 247, 248, 276, ... |
| 11 | `Assets/_Project/Scripts/World/VoxelTerrainSeamBinder/Editor/VoxelTerrainSeamBinderPipeline.cs` | 86, 127, 128, 129, 130, 335, 336, 339, ... |
| 10 | `Assets/_Project/Scripts/Dev/OmegaAutonomySmokeTester.cs` | 150, 151, 152, 153, 154, 155, 385, 386, ... |
| 10 | `Assets/_Project/Scripts/VoxelDeformationSmokeTester.cs` | 223, 224, 225, 273, 274, 321, 322, 323, ... |
| 10 | `Assets/_Project/Scripts/World/PcieBandwidthGuard1411SelfTest.cs` | 37, 38, 75, 103, 104, 146, 147, 148, ... |
| 9 | `Assets/_Project/Scripts/Audio/Editor/AcousticPortalMemorySovereigntyValidator.cs` | 125, 129, 133, 137, 141, 145, 149, 153, ... |
| 9 | `Assets/_Project/Scripts/Editor/AnomalySmokeTester.cs` | 75, 76, 77, 78, 79, 80, 81, 189, ... |
| 9 | `Assets/_Project/Scripts/Editor/GeologyForge/GeologyForgeGenerator.cs` | 200, 262, 376, 404, 419, 435, 756, 783, ... |
| 9 | `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/InteriorClutterForge.cs` | 397, 399, 401, 404, 425, 427, 497, 1014, ... |
| 9 | `Assets/_Project/Scripts/ThermalMeltSmokeTester.cs` | 118, 119, 120, 121, 122, 171, 172, 212, ... |
| 8 | `Assets/_Project/Scripts/Audio/Synthesis/Editor/AudioSynthesisMemorySovereigntyValidator.cs` | 499, 500, 501, 502, 503, 504, 505, 506 |
| 8 | `Assets/_Project/Scripts/World/OfflineWreckageBaker/Editor/OfflineWreckageMockBenchmark.cs` | 42, 43, 44, 45, 46, 47, 48, 49 |
| 7 | `Assets/_Project/Scripts/Editor/GeologyForge/TopographyForgeWindow.cs` | 188, 192, 194, 195, 196, 197, 198 |
| 7 | `Assets/_Project/Scripts/Editor/HydraulicErosionSmokeTester.cs` | 207, 208, 209, 210, 211, 212, 213 |
| 7 | `Assets/_Project/Scripts/SaveSystem/WalIntegrityFuzzerCore_SHINOBU357.cs` | 204, 215, 226, 237, 242, 439, 501 |
| 6 | `Assets/_Project/Scripts/AutomationOmegaSmokeTester.cs` | 51, 52, 53, 54, 55, 56 |
| 6 | `Assets/_Project/Scripts/AutomationSmokeTester.cs` | 39, 40, 41, 42, 43, 44 |
| 6 | `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` | 2625, 2627, 2631, 2633, 2792, 4394 |
| 6 | `Assets/_Project/Scripts/Dev/SpaceEngine098/SpaceEngine098TerrainSmokeTester.cs` | 136, 137, 138, 139, 140, 141 |
| 6 | `Assets/_Project/Scripts/World/BiomeTransitionSmokeTester.cs` | 94, 95, 99, 100, 101, 102 |
| 5 | `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/InteriorClutterForgeSupport.cs` | 28, 150, 357, 359, 534 |
| 5 | `Assets/_Project/Scripts/Editor/ProceduralGen/BioForgeGenerator.cs` | 79, 206, 211, 258, 305 |
| 5 | `Assets/_Project/Scripts/FaunaRuntimeSmokeTester.cs` | 186, 187, 188, 260, 261 |
| 4 | `Assets/_Project/Scripts/Editor/TextureChannelPacker/TextureChannelPackerWindow.cs` | 408, 409, 410, 629 |
| 4 | `Assets/_Project/Scripts/Gameplay/Editor/ScannerLoreDatabaseSyncTunerWindow.cs` | 130, 131, 132, 133 |
| 4 | `Assets/_Project/Scripts/World/HectonSandboxAbyssalShelfSmokeTester.cs` | 106, 107, 108, 109 |
| 4 | `Assets/_Project/Scripts/World/OfflineHadalArchBaker/Editor/HadalArchBakePipeline.cs` | 98, 99, 100, 101 |
| 4 | `Assets/_Project/Scripts/World/PlanetaryCanvasSmokeTester.cs` | 36, 37, 38, 39 |
| 3 | `Assets/_Project/Scripts/Core/Data/Editor/CacheBTreeTopologyXRayWindow.cs` | 467, 468, 469 |
| 3 | `Assets/_Project/Scripts/Dev/BiomeBoundarySdfSmokeTester.cs` | 40, 41, 42 |
| 3 | `Assets/_Project/Scripts/Editor/HectonOctahedralImpostorBaker.cs` | 126, 130, 241 |
| 3 | `Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs` | 1633, 1637, 1641 |

## Allowed DataVault/H8Memory Declaration Sites

| Count | Path | Lines |
|---:|---|---|
| 44 | `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | 620, 622, 623, 624, 625, 626, 627, 628, ... |
| 37 | `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs` | 5975, 5976, 5977, 5978, 5979, 5980, 5981, 5982, ... |
| 32 | `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs` | 1697, 1698, 1699, 1700, 1701, 1702, 1703, 1704, ... |
| 31 | `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs` | 2285, 2286, 2287, 2288, 2289, 2290, 2292, 2293, ... |
| 26 | `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs` | 1388, 1389, 1390, 1391, 1392, 1393, 1394, 1395, ... |
| 25 | `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime_VaultViews.cs` | 96, 97, 98, 99, 100, 101, 102, 103, ... |
| 22 | `Assets/_Project/Scripts/HectonFluidEngine.cs` | 9696, 9697, 9698, 9699, 9700, 9701, 9702, 9703, ... |
| 21 | `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 3530, 3531, 3532, 3533, 3534, 3535, 3536, 3537, ... |
| 21 | `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs` | 5633, 5635, 5636, 5637, 5638, 5639, 5640, 5641, ... |
| 21 | `Assets/_Project/Scripts/PDA/CartographyGridJobs.cs` | 511, 512, 513, 514, 515, 516, 517, 518, ... |
| 21 | `Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs` | 297, 298, 299, 300, 301, 302, 303, 304, ... |
| 20 | `Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs` | 372, 373, 374, 375, 376, 377, 378, 379, ... |
| 20 | `Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralVault.cs` | 67, 68, 69, 70, 71, 72, 73, 74, ... |
| 19 | `Assets/_Project/Scripts/Construction/FluidPipePressureJobs.cs` | 125, 126, 127, 128, 129, 130, 131, 132, ... |
| 19 | `Assets/_Project/Scripts/Construction/ShinobuSocketConstructionData.cs` | 314, 315, 316, 317, 318, 319, 320, 321, ... |
| 19 | `Assets/_Project/Scripts/Gameplay/Combat/HectonCombatRuntime_ArmorPenetration.cs` | 168, 169, 170, 171, 172, 173, 174, 175, ... |
| 18 | `Assets/_Project/Scripts/Quest/QuestDagRuntimeTypes.cs` | 364, 365, 366, 367, 368, 369, 370, 371, ... |
| 18 | `Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageVault.cs` | 63, 64, 65, 66, 67, 68, 69, 70, ... |
| 17 | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs` | 3146, 3147, 3148, 3149, 3150, 3151, 3152, 3153, ... |
| 17 | `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime_StatusEffects.cs` | 2023, 2024, 2025, 2026, 2027, 2028, 2029, 2030, ... |
| 17 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 734, 737, 738, 739, 740, 741, 742, 743, ... |
| 17 | `Assets/_Project/Scripts/Inventory/InventoryDefragJob.cs` | 375, 376, 377, 378, 379, 380, 381, 382, ... |
| 17 | `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs` | 1011, 1012, 1013, 1014, 1015, 1016, 1017, 1018, ... |
| 17 | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` | 2925, 2926, 2930, 2931, 2932, 2933, 2934, 2935, ... |
| 17 | `Assets/_Project/Scripts/QA/Headless/JacobiStressFuzzer/PowerGridJacobiStressFuzzer.cs` | 1495, 1496, 1497, 1498, 1499, 1500, 1501, 1502, ... |
| 17 | `Assets/_Project/Scripts/Quest/QuestDagResolverRuntime.cs` | 1229, 1230, 1231, 1232, 1233, 1234, 1235, 1236, ... |
| 17 | `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 6127, 6129, 6130, 6131, 6132, 6133, 6134, 6135, ... |
| 16 | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainJobs.cs` | 81, 82, 83, 84, 85, 86, 87, 88, ... |
| 16 | `Assets/_Project/Scripts/Construction/DroneCognitionJob.cs` | 311, 312, 319, 320, 321, 322, 323, 325, ... |
| 16 | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` | 2320, 2321, 2322, 2323, 2324, 2325, 2326, 2327, ... |
| 15 | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainVault.cs` | 92, 93, 94, 95, 96, 97, 98, 99, ... |
| 15 | `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs` | 793, 794, 795, 796, 797, 798, 799, 800, ... |
| 15 | `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs` | 1169, 1170, 1171, 1172, 1173, 1174, 1175, 1176, ... |
| 15 | `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsJobs.cs` | 174, 175, 176, 177, 178, 179, 180, 181, ... |
| 14 | `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 3415, 3416, 3417, 3418, 3419, 3420, 3421, 3422, ... |
| 14 | `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs` | 477, 478, 479, 480, 481, 482, 483, 484, ... |
| 14 | `Assets/_Project/Scripts/Inventory/SoaInventoryQueryEngine.CargoSync.cs` | 1020, 1021, 1022, 1023, 1024, 1025, 1026, 1027, ... |
| 14 | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` | 2372, 2373, 2374, 2375, 2376, 2377, 2378, 2379, ... |
| 14 | `Assets/_Project/Scripts/World/GlobalWorldSampler.cs` | 2369, 2370, 2371, 2372, 2373, 2374, 2375, 2376, ... |
| 14 | `Assets/_Project/Scripts/World/GlobalWorldSampler.cs` | 2455, 2456, 2457, 2458, 2459, 2460, 2461, 2462, ... |

## Gate Commands

```powershell
python Tools\DataVaultSovereigntyAudit.py --fail-on-regression
python Tools\DataVaultSovereigntyAudit.py --fail-on-any
```

`--fail-on-regression` blocks any new or increased forbidden constructor or field-declaration count against the baseline.
`--fail-on-any` is the final zero-debt gate and currently fails until all legacy debt is migrated.
