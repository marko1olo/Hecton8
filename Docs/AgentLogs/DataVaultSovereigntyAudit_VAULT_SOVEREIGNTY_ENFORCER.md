# DataVault Sovereignty Audit - VAULT_SOVEREIGNTY_ENFORCER

Schema: `hecton8.datavault_sovereignty_audit.v3`
Status: `BLOCKED_BASELINE_MISSING`
Source root: `Assets/_Project/Scripts`
Pattern: `\bnew\s+NativeArray\s*<`
Baseline: `Docs/AgentLogs/DataVaultSovereigntyBaseline_VAULT_SOVEREIGNTY_ENFORCER.json`

## Summary

| Metric | Count |
|---|---:|
| Total direct `new NativeArray<T>` constructors | 1251 |
| Allowed allocator-internal constructors | 370 |
| Forbidden system constructors | 881 |
| Runtime forbidden constructors | 826 |
| Editor/offline forbidden constructors | 25 |
| Editor/offline transient scratch constructors | 364 |
| Files with forbidden constructors | 134 |
| Editor/offline session scratch declarations | 22 |
| Editor/offline persistent preview declarations | 4 |
| Total field-like `NativeArray<T>` declarations | 5968 |
| Allowed DataVault/H8Memory declarations | 4670 |
| Forbidden system declarations | 1298 |
| Persistent owner native collection declarations | 1039 |
| Job input native collection declarations | 4144 |
| Burst job input native collection declarations | 4144 |
| Native view/payload/kernel struct declarations | 483 |
| Unknown struct native collection declarations | 276 |
| Files with forbidden declarations | 217 |

## Regression Findings

- Baseline missing; runtime no-regression gate fails closed.

## Current Forbidden Constructors By Execution Surface

| Surface | Count |
|---|---:|
| `Editor` | 25 |
| `Plugin` | 30 |
| `Runtime` | 826 |

## Current Forbidden Constructors By Allocator

| Allocator | Count |
|---|---:|
| `Persistent` | 610 |
| `Temp` | 39 |
| `TempJob` | 120 |
| `Unknown` | 112 |

## Editor/Offline Forbidden Constructors By Allocator

| Allocator | Count |
|---|---:|
| `Persistent` | 17 |
| `Unknown` | 8 |

## Top 40 Forbidden Files

| Count | Path | Lines |
|---:|---|---|
| 63 | `Assets/_Project/Scripts/PlayerInventory.cs` | 665, 667, 669, 671, 673, 675, 677, 678, ... |
| 40 | `Assets/_Project/Scripts/HectonFluidEngine.cs` | 3733, 3742, 3751, 3760, 3769, 4635, 4637, 4639, ... |
| 40 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 130, 393, 3992, 3993, 3994, 3995, 3996, 5592, ... |
| 33 | `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs` | 3537, 3539, 3541, 3543, 3545, 3547, 3549, 3551, ... |
| 32 | `Assets/_Project/Scripts/SaveBinaryStorage.cs` | 589, 1070, 2609, 3708, 3731, 4095, 4208, 4282, ... |
| 31 | `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 799, 800, 801, 802, 803, 804, 805, 806, ... |
| 26 | `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | 1535, 1538, 1541, 1544, 1546, 1548, 1550, 1552, ... |
| 23 | `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs` | 4593, 4595, 4597, 4599, 4601, 4603, 4605, 4607, ... |
| 21 | `Assets/_Project/Scripts/SaveSystem/WalIntegrityFuzzerCore.cs` | 177, 220, 221, 222, 223, 339, 718, 719, ... |
| 18 | `Assets/_Project/Scripts/SaveManager.cs` | 981, 991, 1001, 1074, 1081, 1088, 1095, 1105, ... |
| 18 | `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 3533, 3542, 3550, 3558, 3566, 3574, 3582, 3590, ... |
| 17 | `Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs` | 275, 276, 277, 278, 279, 280, 281, 282, ... |
| 17 | `Assets/_Project/Scripts/HectonNarrativeDirector.cs` | 461, 462, 463, 464, 465, 466, 467, 468, ... |
| 15 | `Assets/_Project/Scripts/HectonWorldGenerator.cs` | 1071, 1434, 1435, 1436, 1437, 1438, 1439, 1440, ... |
| 15 | `Assets/_Project/Scripts/QA/Headless/JacobiStressFuzzer/PowerGridJacobiStressFuzzer.cs` | 149, 224, 225, 226, 227, 228, 229, 230, ... |
| 15 | `Assets/_Project/Scripts/SubmarineStructuralGrid.cs` | 1967, 1969, 1970, 1972, 1974, 1976, 1978, 1980, ... |
| 15 | `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs` | 1847, 1869, 1872, 1875, 1881, 1884, 1887, 1890, ... |
| 14 | `Assets/_Project/Scripts/VoxelDeltaProcessor.cs` | 1667, 3085, 3090, 3092, 3094, 3096, 3098, 3100, ... |
| 13 | `Assets/_Project/Scripts/EncounterDirector.cs` | 330, 331, 332, 333, 334, 335, 336, 337, ... |
| 12 | `Assets/_Project/Scripts/Fabricator.cs` | 1359, 1366, 1373, 1380, 1386, 1392, 1398, 1404, ... |
| 12 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs` | 2315, 2319, 2326, 2330, 2334, 2338, 2354, 2358, ... |
| 12 | `Assets/_Project/Scripts/VFX/Debris/ShinobuVoxelSculptorWindow.cs` | 248, 249, 250, 251, 666, 667, 668, 669, ... |
| 12 | `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs` | 1993, 2820, 2893, 3099, 3576, 3577, 3712, 3713, ... |
| 11 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs` | 1759, 1768, 1777, 1786, 1795, 1804, 1813, 1822, ... |
| 11 | `Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonRuntime.cs` | 460, 461, 462, 463, 464, 465, 466, 467, ... |
| 11 | `Assets/_Project/Scripts/PowerGrid.cs` | 1303, 1306, 1309, 1312, 1315, 1318, 1321, 1331, ... |
| 10 | `Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs` | 411, 412, 413, 414, 452, 453, 454, 455, ... |
| 10 | `Assets/_Project/Scripts/Plugins/MapMagic/HectonAnomalyMapMagicNode.cs` | 221, 222, 223, 224, 225, 226, 227, 228, ... |
| 9 | `Assets/_Project/Scripts/InventoryGrid.cs` | 100, 101, 102, 103, 104, 105, 106, 107, ... |
| 9 | `Assets/_Project/Scripts/World/AbyssalThermalManager.cs` | 2382, 2393, 2404, 2415, 2425, 2436, 2799, 2809, ... |
| 8 | `Assets/_Project/Scripts/World/WorldSpatialHashGrid.cs` | 1688, 1689, 1690, 1790, 1791, 1792, 1793, 1910 |
| 7 | `Assets/_Project/Scripts/Plugins/MapMagic/HectonSpaceEngine098MapMagicNodes.cs` | 108, 109, 256, 257, 258, 412, 413 |
| 7 | `Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs` | 794, 795, 796, 797, 798, 799, 800 |
| 7 | `Assets/_Project/Scripts/World/FloraInteractionManager.cs` | 1672, 1674, 1682, 1683, 1684, 6693, 6709 |
| 7 | `Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs` | 760, 761, 762, 763, 764, 765, 766 |
| 7 | `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | 370, 379, 386, 402, 414, 422, 431 |
| 6 | `Assets/_Project/Scripts/CaveGraphGenerator.cs` | 174, 178, 182, 186, 239, 552 |
| 6 | `Assets/_Project/Scripts/Construction/AutonomousExtractorSystem.cs` | 488, 489, 490, 491, 492, 493 |
| 6 | `Assets/_Project/Scripts/Gameplay/HazardZoneManager.cs` | 703, 704, 705, 706, 707, 708 |
| 6 | `Assets/_Project/Scripts/ProximityColliderSystem.cs` | 201, 203, 206, 260, 262, 265 |

## Top 40 Forbidden Declaration Files

| Count | Path | Lines |
|---:|---|---|
| 52 | `Assets/_Project/Scripts/World/VegetationMemoryPool.cs` | 94, 95, 96, 97, 98, 99, 100, 101, ... |
| 49 | `Assets/_Project/Scripts/PlayerInventory.cs` | 519, 520, 521, 522, 523, 524, 525, 526, ... |
| 40 | `Assets/_Project/Scripts/HectonFluidEngine.cs` | 1382, 1383, 1384, 1385, 1386, 1387, 1388, 1389, ... |
| 40 | `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | 1455, 1456, 1457, 1458, 1459, 1460, 1466, 1467, ... |
| 37 | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs` | 674, 675, 676, 677, 678, 679, 680, 688, ... |
| 33 | `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs` | 1513, 1514, 1515, 1516, 1517, 1518, 1519, 1520, ... |
| 32 | `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 103, 104, 105, 106, 107, 108, 109, 110, ... |
| 30 | `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` | 508, 509, 510, 511, 512, 513, 514, 515, ... |
| 28 | `Assets/_Project/Scripts/SpatialAudioManager.cs` | 919, 920, 1023, 1024, 1025, 1026, 1027, 1028, ... |
| 25 | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` | 211, 212, 213, 214, 215, 216, 217, 218, ... |
| 25 | `Assets/_Project/Scripts/TetherInstance.cs` | 230, 231, 232, 233, 234, 235, 236, 237, ... |
| 22 | `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs` | 310, 312, 313, 314, 315, 316, 317, 318, ... |
| 22 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 3388, 3389, 3390, 3391, 3392, 3395, 3396, 3397, ... |
| 21 | `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs` | 272, 273, 274, 275, 276, 277, 278, 279, ... |
| 18 | `Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs` | 194, 195, 196, 197, 198, 199, 200, 201, ... |
| 18 | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs` | 848, 849, 856, 858, 859, 860, 861, 862, ... |
| 17 | `Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs` | 38, 39, 40, 41, 42, 43, 44, 45, ... |
| 17 | `Assets/_Project/Scripts/HectonNarrativeDirector.cs` | 183, 184, 185, 186, 187, 188, 189, 190, ... |
| 17 | `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` | 537, 538, 539, 540, 541, 542, 543, 544, ... |
| 16 | `Assets/_Project/Scripts/SaveSystem/VoxelDeltaCompressionArchitecture.cs` | 169, 170, 171, 172, 173, 174, 175, 176, ... |
| 15 | `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs` | 291, 292, 293, 294, 295, 296, 297, 298, ... |
| 15 | `Assets/_Project/Scripts/SubmarineStructuralGrid.cs` | 553, 554, 555, 556, 557, 558, 559, 560, ... |
| 15 | `Assets/_Project/Scripts/Tools/ToolKinematics/ToolKinematicsRuntime.cs` | 1356, 1357, 1358, 1359, 1360, 1361, 1362, 1363, ... |
| 14 | `Assets/_Project/Scripts/Core/DodReplayRecorder.cs` | 381, 382, 383, 384, 385, 386, 387, 388, ... |
| 13 | `Assets/_Project/Scripts/EncounterDirector.cs` | 278, 279, 280, 281, 282, 283, 284, 285, ... |
| 13 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs` | 110, 111, 112, 113, 114, 115, 116, 117, ... |
| 13 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs` | 1337, 1338, 1339, 1340, 1341, 1342, 1343, 1344, ... |
| 13 | `Assets/_Project/Scripts/SaveManager.cs` | 147, 167, 193, 194, 195, 196, 197, 198, ... |
| 13 | `Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs` | 165, 166, 167, 168, 169, 170, 171, 172, ... |
| 12 | `Assets/_Project/Scripts/Fabricator.cs` | 285, 286, 287, 288, 291, 292, 293, 294, ... |
| 12 | `Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs` | 80, 81, 82, 83, 84, 85, 86, 87, ... |
| 12 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 3266, 3267, 3268, 3269, 3270, 3271, 3272, 3273, ... |
| 12 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 3303, 3304, 3305, 3306, 3307, 3308, 3309, 3310, ... |
| 12 | `Assets/_Project/Scripts/World/GlobalWorldSampler.cs` | 189, 190, 191, 192, 193, 194, 195, 196, ... |
| 11 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs` | 1543, 1544, 1545, 1546, 1547, 1548, 1549, 1550, ... |
| 11 | `Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonRuntime.cs` | 43, 44, 45, 46, 47, 48, 49, 50, ... |
| 11 | `Assets/_Project/Scripts/SaveSystem/SaveStateMerkleTree.cs` | 192, 193, 194, 195, 196, 197, 198, 199, ... |
| 11 | `Assets/_Project/Scripts/World/FloraInteractionManager.cs` | 1465, 1466, 1490, 1508, 1509, 1510, 1511, 1512, ... |
| 10 | `Assets/_Project/Scripts/Construction/LogisticsPipeTransportScheduler.cs` | 29, 31, 33, 35, 36, 37, 38, 39, ... |
| 10 | `Assets/_Project/Scripts/WorldProceduralScatterWorkingMemory.cs` | 26, 27, 28, 29, 30, 62, 68, 69, ... |

## Allowed Allocator-Internal Sites

| Count | Path | Lines |
|---:|---|---|
| 53 | `Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs` | 152, 153, 154, 319, 320, 396, 397, 398, ... |
| 21 | `Assets/_Project/Scripts/World/BiomeWeightMapBaker/Editor/BiomeWeightMapBakePipeline.cs` | 176, 177, 178, 179, 180, 181, 183, 184, ... |
| 18 | `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/Shinobu213/OfflineGeometryBaker.cs` | 399, 459, 473, 475, 477, 479, 555, 668, ... |
| 17 | `Assets/_Project/Scripts/World/OfflineWreckageBaker/Editor/WreckageForgeWindow.cs` | 259, 260, 261, 262, 263, 311, 312, 313, ... |
| 12 | `Assets/_Project/Scripts/Editor/ErosionTestHarness.cs` | 82, 83, 84, 85, 86, 91, 265, 266, ... |
| 11 | `Assets/_Project/Scripts/World/StaticCaveSdfBaker/Editor/StaticCaveSdfBakePipeline.cs` | 107, 175, 176, 177, 178, 197, 198, 226, ... |
| 11 | `Assets/_Project/Scripts/World/VoxelTerrainSeamBinder/Editor/VoxelTerrainSeamBinderPipeline.cs` | 85, 126, 127, 128, 129, 334, 335, 338, ... |
| 10 | `Assets/_Project/Scripts/Dev/OmegaAutonomySmokeTester.cs` | 150, 151, 152, 153, 154, 155, 385, 386, ... |
| 10 | `Assets/_Project/Scripts/VoxelDeformationSmokeTester.cs` | 222, 223, 224, 272, 273, 320, 321, 322, ... |
| 9 | `Assets/_Project/Scripts/Editor/AnomalySmokeTester.cs` | 75, 76, 77, 78, 79, 80, 81, 189, ... |
| 9 | `Assets/_Project/Scripts/Editor/GeologyForge/GeologyForgeGenerator.cs` | 112, 174, 287, 313, 326, 342, 640, 667, ... |
| 9 | `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/InteriorClutterForge.cs` | 397, 399, 401, 404, 425, 427, 497, 1014, ... |
| 8 | `Assets/_Project/Scripts/World/OfflineWreckageBaker/Editor/OfflineWreckageMockBenchmark.cs` | 45, 46, 47, 48, 49, 50, 51, 52 |
| 7 | `Assets/_Project/Scripts/Editor/GeologyForge/TopographyForgeWindow.cs` | 188, 192, 194, 195, 196, 197, 198 |
| 7 | `Assets/_Project/Scripts/Editor/HydraulicErosionSmokeTester.cs` | 207, 208, 209, 210, 211, 212, 213 |
| 6 | `Assets/_Project/Scripts/AutomationOmegaSmokeTester.cs` | 51, 52, 53, 54, 55, 56 |
| 6 | `Assets/_Project/Scripts/AutomationSmokeTester.cs` | 39, 40, 41, 42, 43, 44 |
| 6 | `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` | 1941, 1943, 1947, 1949, 2029, 3243 |
| 6 | `Assets/_Project/Scripts/Dev/SpaceEngine098/SpaceEngine098TerrainSmokeTester.cs` | 136, 137, 138, 139, 140, 141 |
| 6 | `Assets/_Project/Scripts/ThermalMeltSmokeTester.cs` | 115, 116, 152, 153, 193, 194 |
| 6 | `Assets/_Project/Scripts/World/BiomeTransitionSmokeTester.cs` | 93, 94, 98, 99, 100, 101 |
| 5 | `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/InteriorClutterForgeSupport.cs` | 28, 150, 357, 359, 534 |
| 5 | `Assets/_Project/Scripts/Editor/ProceduralGen/BioForgeGenerator.cs` | 77, 204, 209, 256, 303 |
| 5 | `Assets/_Project/Scripts/FaunaRuntimeSmokeTester.cs` | 186, 187, 188, 260, 261 |
| 4 | `Assets/_Project/Scripts/Editor/TextureChannelPacker/TextureChannelPackerWindow.cs` | 399, 400, 401, 620 |
| 4 | `Assets/_Project/Scripts/Gameplay/Editor/ScannerLoreDatabaseSyncTunerWindow.cs` | 130, 131, 132, 133 |
| 4 | `Assets/_Project/Scripts/World/HectonSandboxAbyssalShelfSmokeTester.cs` | 106, 107, 108, 109 |
| 4 | `Assets/_Project/Scripts/World/PlanetaryCanvasSmokeTester.cs` | 36, 37, 38, 39 |
| 3 | `Assets/_Project/Scripts/Core/Data/Editor/CacheBTreeTopologyXRayWindow.cs` | 467, 468, 469 |
| 3 | `Assets/_Project/Scripts/Dev/BiomeBoundarySdfSmokeTester.cs` | 40, 41, 42 |
| 3 | `Assets/_Project/Scripts/Editor/HectonOctahedralImpostorBaker.cs` | 124, 128, 239 |
| 3 | `Assets/_Project/Scripts/SaveRecoverySmokeTester.cs` | 397, 401, 403 |
| 2 | `Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureMockMeshJobs.cs` | 153, 157 |
| 2 | `Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureProfileCsv.cs` | 62, 121 |
| 2 | `Assets/_Project/Scripts/Editor/Arm64MemoryAlignmentXRayWindow.cs` | 69, 70 |
| 2 | `Assets/_Project/Scripts/Editor/EconomyRecipeTunerWindow.cs` | 224, 263 |
| 2 | `Assets/_Project/Scripts/Editor/HlodImpostorForgeWindow.cs` | 134, 190 |
| 2 | `Assets/_Project/Scripts/Editor/WorldProceduralProxySceneBuilder.cs` | 273, 274 |
| 2 | `Assets/_Project/Scripts/SavePersistenceOmegaSmokeTester.cs` | 95, 96 |
| 2 | `Assets/_Project/Scripts/World/VoxelTerrainSeamBinder/Editor/VoxelTerrainSeamPreviewGizmo.cs` | 53, 54 |

## Allowed DataVault/H8Memory Declaration Sites

| Count | Path | Lines |
|---:|---|---|
| 41 | `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs` | 5476, 5477, 5478, 5479, 5480, 5481, 5482, 5483, ... |
| 36 | `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | 436, 438, 439, 440, 441, 447, 448, 449, ... |
| 31 | `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs` | 1690, 1691, 1692, 1693, 1694, 1695, 1696, 1697, ... |
| 26 | `Assets/_Project/Scripts/ModularEquipmentEngine.cs` | 141, 142, 143, 144, 145, 146, 147, 148, ... |
| 26 | `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs` | 994, 995, 996, 997, 998, 999, 1000, 1001, ... |
| 22 | `Assets/_Project/Scripts/HectonFluidEngine.cs` | 7296, 7297, 7298, 7299, 7300, 7301, 7302, 7303, ... |
| 21 | `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 2202, 2203, 2204, 2205, 2206, 2207, 2208, 2209, ... |
| 21 | `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs` | 5135, 5137, 5138, 5139, 5140, 5141, 5142, 5143, ... |
| 21 | `Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs` | 165, 166, 167, 168, 169, 170, 171, 172, ... |
| 20 | `Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs` | 333, 334, 335, 336, 337, 338, 339, 340, ... |
| 20 | `Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralVault.cs` | 66, 67, 68, 69, 70, 71, 72, 73, ... |
| 19 | `Assets/_Project/Scripts/Construction/ShinobuSocketConstructionData.cs` | 251, 252, 253, 254, 255, 256, 257, 258, ... |
| 18 | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs` | 2267, 2268, 2269, 2270, 2271, 2272, 2273, 2274, ... |
| 18 | `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs` | 1808, 1810, 1812, 1813, 1814, 1816, 1818, 1820, ... |
| 18 | `Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageVault.cs` | 62, 63, 64, 65, 66, 67, 68, 69, ... |
| 18 | `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs` | 62, 63, 64, 65, 66, 67, 68, 69, ... |
| 17 | `Assets/_Project/Scripts/Cartography/CartographyGridJobs.cs` | 335, 336, 337, 338, 339, 340, 341, 342, ... |
| 17 | `Assets/_Project/Scripts/Construction/DroneCognitionJob.cs` | 310, 311, 312, 313, 314, 315, 316, 318, ... |
| 17 | `Assets/_Project/Scripts/Inventory/Algorithms/InventoryDefragJob.cs` | 30, 31, 32, 33, 34, 35, 36, 37, ... |
| 17 | `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs` | 1004, 1005, 1006, 1007, 1008, 1009, 1010, 1011, ... |
| 17 | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` | 2700, 2701, 2705, 2706, 2707, 2708, 2709, 2710, ... |
| 17 | `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 4658, 4660, 4661, 4662, 4663, 4664, 4665, 4666, ... |
| 16 | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` | 2092, 2093, 2094, 2095, 2096, 2097, 2098, 2099, ... |
| 16 | `Assets/_Project/Scripts/Quest/QuestDagRuntimeTypes.cs` | 301, 302, 303, 304, 305, 306, 307, 308, ... |
| 15 | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainVault.cs` | 85, 86, 87, 88, 89, 90, 91, 92, ... |
| 15 | `Assets/_Project/Scripts/Construction/FoundationSnappingCalculatorData.cs` | 187, 188, 189, 190, 191, 192, 193, 194, ... |
| 15 | `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs` | 456, 457, 458, 459, 460, 461, 462, 463, ... |
| 15 | `Assets/_Project/Scripts/Logistics/FluidPipePressureJobs.cs` | 18, 19, 20, 21, 22, 23, 24, 25, ... |
| 15 | `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs` | 786, 787, 788, 789, 790, 791, 792, 793, ... |
| 15 | `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs` | 1162, 1163, 1164, 1165, 1166, 1167, 1168, 1169, ... |
| 15 | `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsJobs.cs` | 155, 156, 157, 158, 159, 160, 161, 162, ... |
| 14 | `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 2064, 2065, 2066, 2067, 2068, 2069, 2070, 2071, ... |
| 14 | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` | 2144, 2145, 2146, 2147, 2148, 2149, 2150, 2151, ... |
| 14 | `Assets/_Project/Scripts/Quest/QuestDagResolverRuntime.cs` | 634, 635, 636, 637, 638, 639, 640, 641, ... |
| 14 | `Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs` | 3113, 3114, 3115, 3116, 3117, 3118, 3119, 3120, ... |
| 13 | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainJobs.cs` | 80, 81, 82, 83, 84, 85, 86, 87, ... |
| 13 | `Assets/_Project/Scripts/Fauna/LeviathanTentacleVerletSolver.cs` | 553, 554, 555, 556, 557, 558, 559, 560, ... |
| 13 | `Assets/_Project/Scripts/Fauna/MesofaunaBehavioralStateMachine.cs` | 576, 577, 578, 579, 580, 581, 582, 583, ... |
| 13 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 467, 470, 471, 472, 473, 474, 475, 477, ... |
| 13 | `Assets/_Project/Scripts/Inventory/Routing/InventoryRoutingNetwork.cs` | 167, 168, 169, 170, 171, 172, 173, 174, ... |

## Gate Commands

```powershell
python Tools\DataVaultSovereigntyAudit.py --fail-on-regression
python Tools\DataVaultSovereigntyAudit.py --fail-on-any
```

`--fail-on-regression` blocks any new or increased forbidden constructor or field-declaration count against the baseline.
`--fail-on-any` is the final zero-debt gate and currently fails until all legacy debt is migrated.
