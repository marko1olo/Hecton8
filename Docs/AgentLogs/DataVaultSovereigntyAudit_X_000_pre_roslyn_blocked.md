# DataVault Sovereignty Audit - VAULT_SOVEREIGNTY_ENFORCER

Schema: `hecton8.datavault_sovereignty_audit.v3`
Status: `BLOCKED_BASELINE_MISSING`
Source root: `Assets/_Project/Scripts`
Pattern: `\bnew\s+NativeArray\s*<`
Baseline: `Docs/AgentLogs/DataVaultSovereigntyBaseline_VAULT_SOVEREIGNTY_ENFORCER.json`

## Summary

| Metric | Count |
|---|---:|
| Total direct `new NativeArray<T>` constructors | 1276 |
| Allowed allocator-internal constructors | 408 |
| Forbidden system constructors | 868 |
| Runtime forbidden constructors | 809 |
| Editor/offline forbidden constructors | 29 |
| Editor/offline transient scratch constructors | 402 |
| Files with forbidden constructors | 132 |
| Editor/offline session scratch declarations | 22 |
| Editor/offline persistent preview declarations | 4 |
| Total field-like `NativeArray<T>` declarations | 6577 |
| Allowed DataVault/H8Memory declarations | 5263 |
| Forbidden system declarations | 1314 |
| Persistent owner native collection declarations | 1050 |
| Job input native collection declarations | 4651 |
| Burst job input native collection declarations | 4651 |
| Native view/payload/kernel struct declarations | 569 |
| Unknown struct native collection declarations | 281 |
| Files with forbidden declarations | 220 |

## Current Forbidden Constructors By Execution Surface

| Surface | Count |
|---|---:|
| `Editor` | 29 |
| `Plugin` | 30 |
| `Runtime` | 809 |

## Current Forbidden Constructors By Allocator

| Allocator | Count |
|---|---:|
| `Persistent` | 603 |
| `Temp` | 42 |
| `TempJob` | 115 |
| `Unknown` | 108 |

## Editor/Offline Forbidden Constructors By Allocator

| Allocator | Count |
|---|---:|
| `Persistent` | 21 |
| `Unknown` | 8 |

## Top 60 Forbidden Files

| Count | Path | Lines |
|---:|---|---|
| 63 | `Assets/_Project/Scripts/PlayerInventory.cs` | 665, 667, 669, 671, 673, 675, 677, 678, ... |
| 44 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 130, 393, 3632, 3633, 3634, 3635, 4015, 4016, ... |
| 40 | `Assets/_Project/Scripts/HectonFluidEngine.cs` | 3688, 3697, 3706, 3715, 3724, 4590, 4592, 4594, ... |
| 33 | `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs` | 3537, 3539, 3541, 3543, 3545, 3547, 3549, 3551, ... |
| 31 | `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 797, 798, 799, 800, 801, 802, 803, 804, ... |
| 30 | `Assets/_Project/Scripts/SaveBinaryStorage.cs` | 589, 1070, 2609, 3739, 4103, 4216, 4291, 4292, ... |
| 26 | `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | 1535, 1538, 1541, 1544, 1546, 1548, 1550, 1552, ... |
| 23 | `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs` | 4675, 4677, 4679, 4681, 4683, 4685, 4687, 4689, ... |
| 21 | `Assets/_Project/Scripts/SaveManager.cs` | 977, 987, 997, 1070, 1077, 1084, 1091, 1101, ... |
| 21 | `Assets/_Project/Scripts/SaveSystem/WalIntegrityFuzzerCore.cs` | 178, 221, 222, 223, 224, 340, 719, 720, ... |
| 18 | `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 3644, 3653, 3661, 3669, 3677, 3685, 3693, 3701, ... |
| 17 | `Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs` | 275, 276, 277, 278, 279, 280, 281, 282, ... |
| 15 | `Assets/_Project/Scripts/HectonWorldGenerator.cs` | 1071, 1434, 1435, 1436, 1437, 1438, 1439, 1440, ... |
| 15 | `Assets/_Project/Scripts/SubmarineStructuralGrid.cs` | 1994, 1996, 1997, 1999, 2001, 2003, 2005, 2007, ... |
| 15 | `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs` | 1847, 1869, 1872, 1875, 1881, 1884, 1887, 1890, ... |
| 13 | `Assets/_Project/Scripts/EncounterDirector.cs` | 330, 331, 332, 333, 334, 335, 336, 337, ... |
| 13 | `Assets/_Project/Scripts/VoxelDeltaProcessor.cs` | 3160, 3166, 3168, 3170, 3172, 3174, 3176, 3178, ... |
| 12 | `Assets/_Project/Scripts/Fabricator.cs` | 1360, 1367, 1374, 1381, 1387, 1393, 1399, 1405, ... |
| 12 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs` | 2315, 2319, 2326, 2330, 2334, 2338, 2354, 2358, ... |
| 12 | `Assets/_Project/Scripts/VFX/Debris/ShinobuVoxelSculptorWindow.cs` | 248, 249, 250, 251, 666, 667, 668, 669, ... |
| 12 | `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs` | 1993, 2820, 2893, 3099, 3576, 3577, 3712, 3713, ... |
| 11 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs` | 1760, 1769, 1778, 1787, 1796, 1805, 1814, 1823, ... |
| 11 | `Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonRuntime.cs` | 460, 461, 462, 463, 464, 465, 466, 467, ... |
| 11 | `Assets/_Project/Scripts/PowerGrid.cs` | 1303, 1306, 1309, 1312, 1315, 1318, 1321, 1331, ... |
| 10 | `Assets/_Project/Scripts/ConstructionManager.cs` | 1592, 1598, 1607, 1616, 1625, 1634, 1643, 1652, ... |
| 10 | `Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs` | 411, 412, 413, 414, 452, 453, 454, 455, ... |
| 10 | `Assets/_Project/Scripts/Plugins/MapMagic/HectonAnomalyMapMagicNode.cs` | 221, 222, 223, 224, 225, 226, 227, 228, ... |
| 9 | `Assets/_Project/Scripts/InventoryGrid.cs` | 100, 101, 102, 103, 104, 105, 106, 107, ... |
| 9 | `Assets/_Project/Scripts/World/AbyssalThermalManager.cs` | 2505, 2516, 2527, 2538, 2548, 2559, 2929, 2939, ... |
| 8 | `Assets/_Project/Scripts/World/WorldSpatialHashGrid.cs` | 1724, 1725, 1726, 1826, 1827, 1828, 1829, 1946 |
| 7 | `Assets/_Project/Scripts/Plugins/MapMagic/HectonSpaceEngine098MapMagicNodes.cs` | 108, 109, 258, 259, 260, 417, 418 |
| 7 | `Assets/_Project/Scripts/SaveSystem/WalIntegrityFuzzerCore_SHINOBU357.cs` | 204, 215, 226, 237, 242, 439, 501 |
| 7 | `Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs` | 766, 767, 768, 769, 770, 771, 772 |
| 7 | `Assets/_Project/Scripts/World/FloraInteractionManager.cs` | 1676, 1678, 1686, 1687, 1688, 6702, 6718 |
| 7 | `Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs` | 760, 761, 762, 763, 764, 765, 766 |
| 7 | `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | 410, 419, 426, 442, 454, 462, 471 |
| 6 | `Assets/_Project/Scripts/CaveGraphGenerator.cs` | 289, 293, 297, 301, 354, 667 |
| 6 | `Assets/_Project/Scripts/Construction/AutonomousExtractorSystem.cs` | 488, 489, 490, 491, 492, 493 |
| 6 | `Assets/_Project/Scripts/Gameplay/HazardZoneManager.cs` | 754, 755, 756, 757, 758, 759 |
| 6 | `Assets/_Project/Scripts/ProximityColliderSystem.cs` | 201, 203, 206, 260, 262, 265 |
| 6 | `Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs` | 343, 345, 347, 349, 351, 353 |
| 6 | `Assets/_Project/Scripts/World/VegetationTerrainHoleSynchronizer.cs` | 418, 430, 920, 1046, 1054, 1062 |
| 6 | `Assets/_Project/Scripts/WorldProceduralFieldSampler.cs` | 2442, 2443, 2444, 2703, 4938, 4951 |
| 6 | `Assets/_Project/Scripts/WorldProceduralScatterDirectorMigratorySargassum.cs` | 233, 235, 237, 239, 241, 243 |
| 6 | `Assets/_Project/Scripts/WorldProceduralScatterWorkingMemory.cs` | 161, 167, 168, 169, 170, 573 |
| 5 | `Assets/_Project/Scripts/Core/UIStateStore.cs` | 156, 157, 158, 159, 160 |
| 5 | `Assets/_Project/Scripts/Editor/LSystemGenomeLabWindow.cs` | 266, 267, 268, 269, 270 |
| 5 | `Assets/_Project/Scripts/FlowFieldVisualizer.cs` | 654, 669, 939, 940, 1121 |
| 5 | `Assets/_Project/Scripts/Interaction/PhysicalHandController.cs` | 1556, 1559, 1562, 1565, 1568 |
| 5 | `Assets/_Project/Scripts/Plugins/MapMagic/HectonHydraulicErosionMapMagicNode.cs` | 270, 271, 272, 273, 274 |
| 5 | `Assets/_Project/Scripts/Quest/QuestStateManager.cs` | 180, 181, 182, 397, 402 |
| 5 | `Assets/_Project/Scripts/UI/VR/OpenXRManualOverrideLever.cs` | 689, 690, 691, 692, 693 |
| 5 | `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` | 984, 2825, 2829, 3114, 6321 |
| 5 | `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs` | 1126, 2598, 3391, 3435, 4669 |
| 5 | `Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs` | 892, 1358, 2166, 2178, 2190 |
| 4 | `Assets/_Project/Scripts/Construction/LogisticsPipeTransportScheduler.cs` | 399, 421, 422, 457 |
| 4 | `Assets/_Project/Scripts/Plugins/MapMagic/HectonTerrainSplatmapMapMagicNode.cs` | 135, 136, 137, 138 |
| 4 | `Assets/_Project/Scripts/SaveSidecarStorage.cs` | 56, 110, 158, 227 |
| 4 | `Assets/_Project/Scripts/World/Biomes/BiomeBoundarySdfRuntime.cs` | 283, 284, 285, 286 |
| 4 | `Assets/_Project/Scripts/World/BiotaDensityMapBaker/Editor/BiotaDensityBakePipeline.cs` | 202, 314, 447, 559 |

## Top 60 Forbidden Declaration Files

| Count | Path | Lines |
|---:|---|---|
| 52 | `Assets/_Project/Scripts/World/VegetationMemoryPool.cs` | 94, 95, 96, 97, 98, 99, 100, 101, ... |
| 49 | `Assets/_Project/Scripts/PlayerInventory.cs` | 519, 520, 521, 522, 523, 524, 525, 526, ... |
| 40 | `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | 1455, 1456, 1457, 1458, 1459, 1460, 1466, 1467, ... |
| 39 | `Assets/_Project/Scripts/HectonFluidEngine.cs` | 1383, 1384, 1385, 1386, 1387, 1388, 1389, 1390, ... |
| 35 | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs` | 795, 796, 797, 798, 799, 800, 801, 809, ... |
| 33 | `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs` | 1513, 1514, 1515, 1516, 1517, 1518, 1519, 1520, ... |
| 32 | `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 104, 105, 106, 107, 108, 109, 110, 111, ... |
| 28 | `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` | 510, 511, 512, 513, 514, 515, 516, 517, ... |
| 27 | `Assets/_Project/Scripts/SpatialAudioManager.cs` | 918, 919, 1022, 1023, 1024, 1025, 1026, 1027, ... |
| 25 | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` | 211, 212, 213, 214, 215, 216, 217, 218, ... |
| 25 | `Assets/_Project/Scripts/TetherInstance.cs` | 230, 231, 232, 233, 234, 235, 236, 237, ... |
| 22 | `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs` | 318, 320, 321, 322, 323, 324, 325, 326, ... |
| 22 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 3388, 3389, 3390, 3391, 3392, 3395, 3396, 3397, ... |
| 21 | `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs` | 273, 274, 275, 276, 277, 278, 279, 280, ... |
| 20 | `Assets/_Project/Scripts/QA/Headless/JacobiStressFuzzer/PowerGridJacobiStressFuzzer.cs` | 541, 542, 543, 544, 545, 546, 547, 548, ... |
| 18 | `Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs` | 194, 195, 196, 197, 198, 199, 200, 201, ... |
| 18 | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs` | 851, 852, 859, 861, 862, 863, 864, 865, ... |
| 17 | `Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs` | 38, 39, 40, 41, 42, 43, 44, 45, ... |
| 17 | `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` | 539, 540, 541, 542, 543, 544, 545, 546, ... |
| 16 | `Assets/_Project/Scripts/SaveSystem/VoxelDeltaCompressionArchitecture.cs` | 169, 170, 171, 172, 173, 174, 175, 176, ... |
| 15 | `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs` | 292, 293, 294, 295, 296, 297, 298, 299, ... |
| 15 | `Assets/_Project/Scripts/SubmarineStructuralGrid.cs` | 555, 556, 557, 558, 559, 560, 561, 562, ... |
| 15 | `Assets/_Project/Scripts/Tools/ToolKinematics/ToolKinematicsRuntime.cs` | 1300, 1301, 1302, 1303, 1304, 1305, 1306, 1307, ... |
| 14 | `Assets/_Project/Scripts/Core/DodReplayRecorder.cs` | 381, 382, 383, 384, 385, 386, 387, 388, ... |
| 13 | `Assets/_Project/Scripts/EncounterDirector.cs` | 278, 279, 280, 281, 282, 283, 284, 285, ... |
| 13 | `Assets/_Project/Scripts/SaveManager.cs` | 147, 167, 193, 194, 195, 196, 197, 198, ... |
| 13 | `Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs` | 165, 166, 167, 168, 169, 170, 171, 172, ... |
| 12 | `Assets/_Project/Scripts/Fabricator.cs` | 285, 286, 287, 288, 291, 292, 293, 294, ... |
| 12 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs` | 111, 112, 113, 114, 115, 116, 117, 118, ... |
| 12 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs` | 1337, 1338, 1339, 1340, 1341, 1342, 1343, 1344, ... |
| 12 | `Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs` | 83, 84, 85, 86, 87, 88, 89, 90, ... |
| 12 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 3266, 3267, 3268, 3269, 3270, 3271, 3272, 3273, ... |
| 12 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 3303, 3304, 3305, 3306, 3307, 3308, 3309, 3310, ... |
| 12 | `Assets/_Project/Scripts/World/GlobalWorldSampler.cs` | 189, 190, 191, 192, 193, 194, 195, 196, ... |
| 11 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs` | 1543, 1544, 1545, 1546, 1547, 1548, 1549, 1550, ... |
| 11 | `Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonRuntime.cs` | 43, 44, 45, 46, 47, 48, 49, 50, ... |
| 11 | `Assets/_Project/Scripts/SaveSystem/SaveStateMerkleTree.cs` | 192, 193, 194, 195, 196, 197, 198, 199, ... |
| 11 | `Assets/_Project/Scripts/World/FloraInteractionManager.cs` | 1466, 1467, 1491, 1509, 1510, 1511, 1512, 1513, ... |
| 10 | `Assets/_Project/Scripts/Construction/LogisticsPipeTransportScheduler.cs` | 29, 31, 33, 35, 36, 37, 38, 39, ... |
| 10 | `Assets/_Project/Scripts/ConstructionManager.cs` | 171, 172, 173, 174, 175, 176, 177, 178, ... |
| 10 | `Assets/_Project/Scripts/WorldProceduralScatterWorkingMemory.cs` | 26, 27, 28, 29, 30, 62, 68, 69, ... |
| 9 | `Assets/_Project/Scripts/InventoryGrid.cs` | 53, 54, 55, 56, 57, 58, 59, 60, ... |
| 9 | `Assets/_Project/Scripts/PowerGrid.cs` | 174, 175, 176, 177, 178, 179, 180, 181, ... |
| 9 | `Assets/_Project/Scripts/SaveBinaryStorage.cs` | 1282, 1283, 1284, 1285, 1286, 1287, 1288, 1289, ... |
| 9 | `Assets/_Project/Scripts/VoxelDeltaProcessor.cs` | 4294, 4295, 4296, 4297, 4298, 4299, 4300, 4301, ... |
| 9 | `Assets/_Project/Scripts/World/AbyssalThermalManager.cs` | 662, 663, 669, 670, 671, 672, 673, 674, ... |
| 9 | `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs` | 1206, 1210, 1211, 1212, 1214, 1215, 1216, 1217, ... |
| 8 | `Assets/_Project/Scripts/Construction/DroneFleetManager_Transactions.cs` | 36, 37, 38, 39, 40, 41, 42, 43 |
| 8 | `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs` | 876, 880, 882, 888, 889, 890, 891, 892 |
| 8 | `Assets/_Project/Scripts/World/VegetationMemoryPool.cs` | 21, 22, 23, 24, 25, 26, 27, 28 |
| 8 | `Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs` | 621, 622, 623, 624, 625, 626, 627, 628 |
| 8 | `Assets/_Project/Scripts/World/WorldSpatialHashGrid.cs` | 243, 244, 245, 249, 250, 251, 252, 257 |
| 7 | `Assets/_Project/Scripts/Construction/LogisticsRouteScratchMemory.cs` | 18, 19, 20, 21, 22, 23, 24 |
| 7 | `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime_StatusEffects.cs` | 55, 56, 57, 58, 59, 60, 61 |
| 7 | `Assets/_Project/Scripts/HectonWorldGenerator.cs` | 666, 667, 668, 669, 670, 671, 672 |
| 7 | `Assets/_Project/Scripts/LocRegistry.cs` | 506, 507, 508, 509, 510, 511, 522 |
| 7 | `Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs` | 237, 238, 239, 240, 241, 242, 243 |
| 7 | `Assets/_Project/Scripts/World/FloraRegrowthDirector.cs` | 309, 310, 311, 313, 316, 319, 320 |
| 7 | `Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs` | 81, 83, 84, 85, 86, 87, 88 |
| 7 | `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs` | 873, 875, 881, 883, 885, 886, 894 |

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
| 9 | `Assets/_Project/Scripts/Editor/GeologyForge/GeologyForgeGenerator.cs` | 112, 174, 287, 313, 326, 342, 650, 677, ... |
| 9 | `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/InteriorClutterForge.cs` | 397, 399, 401, 404, 425, 427, 497, 1014, ... |
| 8 | `Assets/_Project/Scripts/World/OfflineWreckageBaker/Editor/OfflineWreckageMockBenchmark.cs` | 45, 46, 47, 48, 49, 50, 51, 52 |
| 7 | `Assets/_Project/Scripts/Editor/GeologyForge/TopographyForgeWindow.cs` | 187, 191, 193, 194, 195, 196, 197 |
| 7 | `Assets/_Project/Scripts/Editor/HydraulicErosionSmokeTester.cs` | 207, 208, 209, 210, 211, 212, 213 |
| 6 | `Assets/_Project/Scripts/AutomationOmegaSmokeTester.cs` | 51, 52, 53, 54, 55, 56 |
| 6 | `Assets/_Project/Scripts/AutomationSmokeTester.cs` | 39, 40, 41, 42, 43, 44 |
| 6 | `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` | 2175, 2177, 2181, 2183, 2337, 3560 |
| 6 | `Assets/_Project/Scripts/Dev/SpaceEngine098/SpaceEngine098TerrainSmokeTester.cs` | 136, 137, 138, 139, 140, 141 |
| 6 | `Assets/_Project/Scripts/ThermalMeltSmokeTester.cs` | 116, 117, 153, 154, 194, 195 |
| 6 | `Assets/_Project/Scripts/World/BiomeTransitionSmokeTester.cs` | 94, 95, 99, 100, 101, 102 |
| 5 | `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/InteriorClutterForgeSupport.cs` | 28, 150, 357, 359, 534 |
| 5 | `Assets/_Project/Scripts/Editor/ProceduralGen/BioForgeGenerator.cs` | 77, 204, 209, 256, 303 |
| 5 | `Assets/_Project/Scripts/FaunaRuntimeSmokeTester.cs` | 186, 187, 188, 260, 261 |
| 4 | `Assets/_Project/Scripts/Editor/TextureChannelPacker/TextureChannelPackerWindow.cs` | 399, 400, 401, 620 |
| 4 | `Assets/_Project/Scripts/Gameplay/Editor/ScannerLoreDatabaseSyncTunerWindow.cs` | 130, 131, 132, 133 |
| 4 | `Assets/_Project/Scripts/World/HectonSandboxAbyssalShelfSmokeTester.cs` | 106, 107, 108, 109 |
| 4 | `Assets/_Project/Scripts/World/PlanetaryCanvasSmokeTester.cs` | 36, 37, 38, 39 |
| 3 | `Assets/_Project/Scripts/Core/Data/Editor/CacheBTreeTopologyXRayWindow.cs` | 467, 468, 469 |
| 3 | `Assets/_Project/Scripts/Dev/BiomeBoundarySdfSmokeTester.cs` | 40, 41, 42 |
| 3 | `Assets/_Project/Scripts/Editor/HectonOctahedralImpostorBaker.cs` | 125, 129, 240 |
| 3 | `Assets/_Project/Scripts/SaveRecoverySmokeTester.cs` | 397, 401, 403 |
| 2 | `Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureMockMeshJobs.cs` | 153, 157 |
| 2 | `Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureProfileCsv.cs` | 62, 121 |
| 2 | `Assets/_Project/Scripts/Editor/Arm64MemoryAlignmentXRayWindow.cs` | 69, 70 |
| 2 | `Assets/_Project/Scripts/Editor/EconomyRecipeTunerWindow.cs` | 224, 263 |
| 2 | `Assets/_Project/Scripts/Editor/HlodImpostorForgeWindow.cs` | 135, 191 |
| 2 | `Assets/_Project/Scripts/Editor/WorldProceduralProxySceneBuilder.cs` | 273, 274 |
| 2 | `Assets/_Project/Scripts/SavePersistenceOmegaSmokeTester.cs` | 95, 96 |
| 2 | `Assets/_Project/Scripts/World/VoxelTerrainSeamBinder/Editor/VoxelTerrainSeamPreviewGizmo.cs` | 53, 54 |
| 1 | `Assets/_Project/Scripts/Dev/HabitatStressSmokeTester.cs` | 504 |
| 1 | `Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureControlMapBaker.cs` | 271 |
| 1 | `Assets/_Project/Scripts/Editor/BaseModuleCatalogEditorTools.cs` | 237 |
| 1 | `Assets/_Project/Scripts/Editor/GeologyForge/GeologyForgeWindow.cs` | 218 |
| 1 | `Assets/_Project/Scripts/Editor/GeologyForge/GeologyProfileCsv.cs` | 47 |
| 1 | `Assets/_Project/Scripts/Editor/GeologyForge/TopographyForgeCsv.cs` | 95 |
| 1 | `Assets/_Project/Scripts/Editor/HydraulicErosionForge/Shinobu242/HydraulicErosionForgeBaker.cs` | 776 |
| 1 | `Assets/_Project/Scripts/Editor/HydraulicErosionForge/Shinobu242/HydraulicErosionWeatheringCsv.cs` | 39 |
| 1 | `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/InteriorClutterForgeJobs.cs` | 165 |
| 1 | `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/Shinobu213/OfflineOptimizationProfileCsv.cs` | 43 |
| 1 | `Assets/_Project/Scripts/Editor/PlanetaryCanvasSmokeTester.cs` | 54 |
| 1 | `Assets/_Project/Scripts/Editor/Shinobu132CablePhysicsTunerWindow.cs` | 156 |
| 1 | `Assets/_Project/Scripts/Physics/Buoyancy/Editor/AsyncBuoyancyReadbackLayoutValidator.cs` | 44 |
| 1 | `Assets/_Project/Scripts/SaveSystemRuntimeSmokeTester.cs` | 242 |
| 1 | `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchBakePipeline.cs` | 1064 |
| 1 | `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchMockBenchmark.cs` | 29 |
| 1 | `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/TectonicRiftProfileCsvParser.cs` | 54 |
| 1 | `Assets/_Project/Scripts/World/VolumetricBiomeSmokeTester.cs` | 327 |
| 1 | `Assets/_Project/Scripts/World/VoxelTerrainSeamBinder/Editor/VoxelTerrainSeamBinderWindow.cs` | 554 |

## Allowed DataVault/H8Memory Declaration Sites

| Count | Path | Lines |
|---:|---|---|
| 37 | `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs` | 5410, 5411, 5412, 5413, 5414, 5415, 5416, 5417, ... |
| 36 | `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | 436, 438, 439, 440, 441, 447, 448, 449, ... |
| 31 | `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs` | 1697, 1698, 1699, 1700, 1701, 1702, 1703, 1704, ... |
| 28 | `Assets/_Project/Scripts/ModularEquipmentEngine.cs` | 158, 159, 160, 161, 162, 163, 164, 165, ... |
| 26 | `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs` | 1792, 1793, 1795, 1797, 1798, 1799, 1801, 1803, ... |
| 26 | `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs` | 994, 995, 996, 997, 998, 999, 1000, 1001, ... |
| 22 | `Assets/_Project/Scripts/HectonFluidEngine.cs` | 7201, 7202, 7203, 7204, 7205, 7206, 7207, 7208, ... |
| 21 | `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 2189, 2190, 2191, 2192, 2193, 2194, 2195, 2196, ... |
| 21 | `Assets/_Project/Scripts/Cartography/CartographyGridJobs.cs` | 385, 386, 387, 388, 389, 390, 391, 392, ... |
| 21 | `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs` | 5069, 5071, 5072, 5073, 5074, 5075, 5076, 5077, ... |
| 21 | `Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs` | 165, 166, 167, 168, 169, 170, 171, 172, ... |
| 20 | `Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs` | 329, 330, 331, 332, 333, 334, 335, 336, ... |
| 20 | `Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralVault.cs` | 66, 67, 68, 69, 70, 71, 72, 73, ... |
| 19 | `Assets/_Project/Scripts/Construction/ShinobuSocketConstructionData.cs` | 251, 252, 253, 254, 255, 256, 257, 258, ... |
| 18 | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs` | 2248, 2249, 2250, 2251, 2252, 2253, 2254, 2255, ... |
| 18 | `Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageVault.cs` | 62, 63, 64, 65, 66, 67, 68, 69, ... |
| 18 | `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs` | 62, 63, 64, 65, 66, 67, 68, 69, ... |
| 17 | `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime_StatusEffects.cs` | 1511, 1512, 1513, 1514, 1515, 1516, 1517, 1518, ... |
| 17 | `Assets/_Project/Scripts/Inventory/Algorithms/InventoryDefragJob.cs` | 30, 31, 32, 33, 34, 35, 36, 37, ... |
| 17 | `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs` | 1011, 1012, 1013, 1014, 1015, 1016, 1017, 1018, ... |
| 17 | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` | 2700, 2701, 2705, 2706, 2707, 2708, 2709, 2710, ... |
| 17 | `Assets/_Project/Scripts/QA/Headless/JacobiStressFuzzer/PowerGridJacobiStressFuzzer.cs` | 1459, 1460, 1461, 1462, 1463, 1464, 1465, 1466, ... |
| 17 | `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 4769, 4771, 4772, 4773, 4774, 4775, 4776, 4777, ... |
| 16 | `Assets/_Project/Scripts/Construction/DroneCognitionJob.cs` | 310, 311, 318, 319, 320, 321, 322, 324, ... |
| 16 | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` | 2092, 2093, 2094, 2095, 2096, 2097, 2098, 2099, ... |
| 16 | `Assets/_Project/Scripts/Quest/QuestDagRuntimeTypes.cs` | 307, 308, 309, 310, 311, 312, 313, 314, ... |
| 15 | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainVault.cs` | 85, 86, 87, 88, 89, 90, 91, 92, ... |
| 15 | `Assets/_Project/Scripts/Construction/FoundationSnappingCalculatorData.cs` | 187, 188, 189, 190, 191, 192, 193, 194, ... |
| 15 | `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs` | 456, 457, 458, 459, 460, 461, 462, 463, ... |
| 15 | `Assets/_Project/Scripts/Logistics/FluidPipePressureJobs.cs` | 18, 19, 20, 21, 22, 23, 24, 25, ... |
| 15 | `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs` | 793, 794, 795, 796, 797, 798, 799, 800, ... |
| 15 | `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs` | 1169, 1170, 1171, 1172, 1173, 1174, 1175, 1176, ... |
| 15 | `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsJobs.cs` | 172, 173, 174, 175, 176, 177, 178, 179, ... |
| 14 | `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 2051, 2052, 2053, 2054, 2055, 2056, 2057, 2058, ... |
| 14 | `Assets/_Project/Scripts/Inventory/SoaInventoryQueryEngine.CargoSync.cs` | 786, 787, 788, 789, 790, 791, 792, 793, ... |
| 14 | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` | 2144, 2145, 2146, 2147, 2148, 2149, 2150, 2151, ... |
| 14 | `Assets/_Project/Scripts/Quest/QuestDagResolverRuntime.cs` | 713, 714, 715, 716, 717, 718, 719, 720, ... |
| 14 | `Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs` | 3113, 3114, 3115, 3116, 3117, 3118, 3119, 3120, ... |
| 13 | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainJobs.cs` | 80, 81, 82, 83, 84, 85, 86, 87, ... |
| 13 | `Assets/_Project/Scripts/AI/Pathfinding/VoxelAStarJobs.cs` | 343, 344, 345, 346, 347, 348, 349, 350, ... |
| 13 | `Assets/_Project/Scripts/Fauna/LeviathanTentacleVerletSolver.cs` | 556, 557, 558, 559, 560, 561, 562, 563, ... |
| 13 | `Assets/_Project/Scripts/Gameplay/AirlockPressurization/AirlockPressurizationRuntime.cs` | 69, 70, 71, 72, 73, 74, 75, 76, ... |
| 13 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 467, 470, 471, 472, 473, 474, 475, 477, ... |
| 13 | `Assets/_Project/Scripts/Inventory/Routing/InventoryRoutingNetwork.cs` | 167, 168, 169, 170, 171, 172, 173, 174, ... |
| 13 | `Assets/_Project/Scripts/Physics/KCC/HectonKccRuntime_SmokeTest.cs` | 290, 291, 292, 293, 294, 295, 296, 297, ... |
| 13 | `Assets/_Project/Scripts/World/BiotaDensityMapBaker/Editor/BiotaDensityBakeJobs.cs` | 274, 275, 276, 277, 278, 279, 280, 281, ... |
| 13 | `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | 2661, 2662, 2663, 2664, 2665, 2666, 2667, 2668, ... |
| 12 | `Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs` | 246, 247, 248, 249, 250, 251, 252, 253, ... |
| 12 | `Assets/_Project/Scripts/Audio/VocalWarningSystem.cs` | 172, 173, 174, 175, 176, 177, 178, 179, ... |
| 12 | `Assets/_Project/Scripts/Construction/HabitatDeconstructionTransactionKernel.cs` | 160, 161, 162, 163, 164, 165, 166, 167, ... |
| 12 | `Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentRouterRuntime.cs` | 1354, 1355, 1356, 1357, 1358, 1359, 1360, 1361, ... |
| 12 | `Assets/_Project/Scripts/Inventory/InventorySoAUtility.cs` | 289, 290, 291, 292, 293, 294, 295, 296, ... |
| 12 | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs` | 3180, 3181, 3182, 3183, 3184, 3185, 3186, 3187, ... |
| 12 | `Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementJobs.cs` | 103, 104, 105, 106, 107, 108, 109, 110, ... |
| 12 | `Assets/_Project/Scripts/Tools/UpgradeMatrixCompiler.cs` | 75, 76, 77, 78, 79, 80, 81, 82, ... |
| 12 | `Assets/_Project/Scripts/World/EcosystemDirector.cs` | 595, 596, 597, 598, 599, 600, 601, 602, ... |
| 12 | `Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs` | 600, 601, 602, 603, 604, 605, 606, 607, ... |
| 11 | `Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault.cs` | 64, 65, 66, 67, 68, 69, 70, 71, ... |
| 11 | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs` | 4154, 4155, 4156, 4157, 4158, 4159, 4160, 4161, ... |
| 11 | `Assets/_Project/Scripts/Construction/DroneFleetNavigationKernel.cs` | 1461, 1462, 1463, 1464, 1465, 1466, 1467, 1468, ... |

## Gate Commands

```powershell
python Tools\DataVaultSovereigntyAudit.py --fail-on-regression
python Tools\DataVaultSovereigntyAudit.py --fail-on-any
```

`--fail-on-regression` blocks any new or increased forbidden constructor or field-declaration count against the baseline.
`--fail-on-any` is the final zero-debt gate and currently fails until all legacy debt is migrated.
