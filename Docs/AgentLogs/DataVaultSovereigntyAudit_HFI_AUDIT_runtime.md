# DataVault Sovereignty Audit - VAULT_SOVEREIGNTY_ENFORCER

Schema: `hecton8.datavault_sovereignty_audit.v3`
Status: `PASS_NO_REGRESSION_WITH_LEGACY_DEBT`
Source root: `Assets/_Project/Scripts`
Pattern: `\bnew\s+NativeArray\s*<`
Baseline: `Docs/AgentLogs/DataVaultSovereigntyBaselineCandidate_HFI_AUDIT_v3.json`

## Summary

| Metric | Count |
|---|---:|
| Total direct `new NativeArray<T>` constructors | 1215 |
| Allowed allocator-internal constructors | 365 |
| Forbidden system constructors | 850 |
| Runtime forbidden constructors | 800 |
| Editor/offline forbidden constructors | 20 |
| Editor/offline transient scratch constructors | 359 |
| Files with forbidden constructors | 129 |
| Editor/offline session scratch declarations | 22 |
| Editor/offline persistent preview declarations | 4 |
| Total field-like `NativeArray<T>` declarations | 5742 |
| Allowed DataVault/H8Memory declarations | 4465 |
| Forbidden system declarations | 1277 |
| Persistent owner native collection declarations | 1022 |
| Job input native collection declarations | 3974 |
| Burst job input native collection declarations | 3971 |
| Native view/payload/kernel struct declarations | 448 |
| Unknown struct native collection declarations | 272 |
| Files with forbidden declarations | 211 |

## Current Forbidden Constructors By Execution Surface

| Surface | Count |
|---|---:|
| `Editor` | 20 |
| `Plugin` | 30 |
| `Runtime` | 800 |

## Current Forbidden Constructors By Allocator

| Allocator | Count |
|---|---:|
| `Persistent` | 707 |
| `Temp` | 35 |
| `TempJob` | 87 |
| `Unknown` | 21 |

## Editor/Offline Forbidden Constructors By Allocator

| Allocator | Count |
|---|---:|
| `Persistent` | 14 |
| `Unknown` | 6 |

## Top 40 Forbidden Files

| Count | Path | Lines |
|---:|---|---|
| 63 | `Assets/_Project/Scripts/PlayerInventory.cs` | 665, 667, 669, 671, 673, 675, 677, 678, ... |
| 40 | `Assets/_Project/Scripts/HectonFluidEngine.cs` | 3715, 3724, 3733, 3742, 3751, 4617, 4619, 4621, ... |
| 40 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 129, 392, 3989, 3990, 3991, 3992, 3993, 5385, ... |
| 33 | `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs` | 3432, 3434, 3436, 3438, 3440, 3442, 3444, 3446, ... |
| 32 | `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 787, 788, 789, 790, 791, 792, 793, 794, ... |
| 32 | `Assets/_Project/Scripts/SaveBinaryStorage.cs` | 588, 1069, 2607, 3706, 3729, 4093, 4206, 4280, ... |
| 26 | `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | 1532, 1535, 1538, 1541, 1543, 1545, 1547, 1549, ... |
| 23 | `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs` | 4592, 4594, 4596, 4598, 4600, 4602, 4604, 4606, ... |
| 18 | `Assets/_Project/Scripts/SaveManager.cs` | 981, 991, 1001, 1067, 1074, 1081, 1088, 1098, ... |
| 18 | `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 3502, 3511, 3519, 3527, 3535, 3543, 3551, 3559, ... |
| 17 | `Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs` | 274, 275, 276, 277, 278, 279, 280, 281, ... |
| 17 | `Assets/_Project/Scripts/HectonNarrativeDirector.cs` | 461, 462, 463, 464, 465, 466, 467, 468, ... |
| 15 | `Assets/_Project/Scripts/HectonWorldGenerator.cs` | 1040, 1403, 1404, 1405, 1406, 1407, 1408, 1409, ... |
| 15 | `Assets/_Project/Scripts/SubmarineStructuralGrid.cs` | 1967, 1969, 1970, 1972, 1974, 1976, 1978, 1980, ... |
| 15 | `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs` | 1780, 1802, 1805, 1808, 1814, 1817, 1820, 1823, ... |
| 14 | `Assets/_Project/Scripts/VoxelDeltaProcessor.cs` | 1640, 3058, 3063, 3065, 3067, 3069, 3071, 3073, ... |
| 13 | `Assets/_Project/Scripts/EncounterDirector.cs` | 329, 330, 331, 332, 333, 334, 335, 336, ... |
| 12 | `Assets/_Project/Scripts/Fabricator.cs` | 1358, 1365, 1372, 1379, 1385, 1391, 1397, 1403, ... |
| 12 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs` | 2315, 2319, 2326, 2330, 2334, 2338, 2354, 2358, ... |
| 12 | `Assets/_Project/Scripts/VFX/Debris/ShinobuVoxelSculptorWindow.cs` | 247, 248, 249, 250, 608, 609, 610, 611, ... |
| 12 | `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs` | 1963, 2790, 2863, 3069, 3546, 3547, 3682, 3683, ... |
| 11 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs` | 1759, 1768, 1777, 1786, 1795, 1804, 1813, 1822, ... |
| 11 | `Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonRuntime.cs` | 475, 476, 477, 478, 479, 480, 481, 482, ... |
| 11 | `Assets/_Project/Scripts/PowerGrid.cs` | 1303, 1306, 1309, 1312, 1315, 1318, 1321, 1331, ... |
| 10 | `Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs` | 343, 344, 345, 346, 388, 389, 390, 391, ... |
| 10 | `Assets/_Project/Scripts/Plugins/MapMagic/HectonAnomalyMapMagicNode.cs` | 221, 222, 223, 224, 225, 226, 227, 228, ... |
| 9 | `Assets/_Project/Scripts/InventoryGrid.cs` | 96, 97, 98, 99, 100, 101, 102, 103, ... |
| 9 | `Assets/_Project/Scripts/World/AbyssalThermalManager.cs` | 2375, 2386, 2397, 2408, 2418, 2429, 2792, 2802, ... |
| 8 | `Assets/_Project/Scripts/World/WorldSpatialHashGrid.cs` | 1684, 1685, 1686, 1786, 1787, 1788, 1789, 1906 |
| 7 | `Assets/_Project/Scripts/Plugins/MapMagic/HectonSpaceEngine098MapMagicNodes.cs` | 108, 109, 256, 257, 258, 412, 413 |
| 7 | `Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs` | 794, 795, 796, 797, 798, 799, 800 |
| 7 | `Assets/_Project/Scripts/World/FloraInteractionManager.cs` | 1662, 1664, 1672, 1673, 1674, 6670, 6686 |
| 7 | `Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs` | 760, 761, 762, 763, 764, 765, 766 |
| 7 | `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | 370, 379, 386, 402, 414, 422, 431 |
| 6 | `Assets/_Project/Scripts/CaveGraphGenerator.cs` | 174, 178, 182, 186, 239, 552 |
| 6 | `Assets/_Project/Scripts/Construction/AutonomousExtractorSystem.cs` | 488, 489, 490, 491, 492, 493 |
| 6 | `Assets/_Project/Scripts/Gameplay/HazardZoneManager.cs` | 697, 698, 699, 700, 701, 702 |
| 6 | `Assets/_Project/Scripts/ProximityColliderSystem.cs` | 199, 201, 204, 258, 260, 263 |
| 6 | `Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs` | 343, 345, 347, 349, 351, 353 |
| 6 | `Assets/_Project/Scripts/Quest/QuestStateManager.cs` | 179, 180, 181, 396, 401, 784 |

## Top 40 Forbidden Declaration Files

| Count | Path | Lines |
|---:|---|---|
| 52 | `Assets/_Project/Scripts/World/VegetationMemoryPool.cs` | 94, 95, 96, 97, 98, 99, 100, 101, ... |
| 49 | `Assets/_Project/Scripts/PlayerInventory.cs` | 519, 520, 521, 522, 523, 524, 525, 526, ... |
| 40 | `Assets/_Project/Scripts/HectonFluidEngine.cs` | 1382, 1383, 1384, 1385, 1386, 1387, 1388, 1389, ... |
| 40 | `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | 1452, 1453, 1454, 1455, 1456, 1457, 1463, 1464, ... |
| 37 | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs` | 674, 675, 676, 677, 678, 679, 680, 688, ... |
| 33 | `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs` | 1505, 1506, 1507, 1508, 1509, 1510, 1511, 1512, ... |
| 32 | `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 102, 103, 104, 105, 106, 107, 108, 109, ... |
| 28 | `Assets/_Project/Scripts/SpatialAudioManager.cs` | 916, 917, 1020, 1021, 1022, 1023, 1024, 1025, ... |
| 25 | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` | 211, 212, 213, 214, 215, 216, 217, 218, ... |
| 25 | `Assets/_Project/Scripts/TetherInstance.cs` | 230, 231, 232, 233, 234, 235, 236, 237, ... |
| 22 | `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs` | 308, 310, 311, 312, 313, 314, 315, 316, ... |
| 22 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 3385, 3386, 3387, 3388, 3389, 3392, 3393, 3394, ... |
| 21 | `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs` | 272, 273, 274, 275, 276, 277, 278, 279, ... |
| 19 | `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` | 361, 362, 363, 364, 365, 366, 387, 389, ... |
| 18 | `Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs` | 194, 195, 196, 197, 198, 199, 200, 201, ... |
| 18 | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs` | 843, 844, 851, 853, 854, 855, 856, 857, ... |
| 17 | `Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs` | 37, 38, 39, 40, 41, 42, 43, 44, ... |
| 17 | `Assets/_Project/Scripts/HectonNarrativeDirector.cs` | 183, 184, 185, 186, 187, 188, 189, 190, ... |
| 17 | `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` | 367, 368, 369, 370, 371, 372, 373, 374, ... |
| 16 | `Assets/_Project/Scripts/SaveSystem/VoxelDeltaCompressionArchitecture.cs` | 169, 170, 171, 172, 173, 174, 175, 176, ... |
| 15 | `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs` | 290, 291, 292, 293, 294, 295, 296, 297, ... |
| 15 | `Assets/_Project/Scripts/SubmarineStructuralGrid.cs` | 553, 554, 555, 556, 557, 558, 559, 560, ... |
| 15 | `Assets/_Project/Scripts/Tools/ToolKinematics/ToolKinematicsRuntime.cs` | 1269, 1270, 1271, 1272, 1273, 1274, 1275, 1276, ... |
| 14 | `Assets/_Project/Scripts/Core/DodReplayRecorder.cs` | 381, 382, 383, 384, 385, 386, 387, 388, ... |
| 13 | `Assets/_Project/Scripts/EncounterDirector.cs` | 277, 278, 279, 280, 281, 282, 283, 284, ... |
| 13 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs` | 110, 111, 112, 113, 114, 115, 116, 117, ... |
| 13 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs` | 1337, 1338, 1339, 1340, 1341, 1342, 1343, 1344, ... |
| 13 | `Assets/_Project/Scripts/SaveManager.cs` | 147, 167, 193, 194, 195, 196, 197, 198, ... |
| 13 | `Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs` | 165, 166, 167, 168, 169, 170, 171, 172, ... |
| 12 | `Assets/_Project/Scripts/Fabricator.cs` | 285, 286, 287, 288, 291, 292, 293, 294, ... |
| 12 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 3263, 3264, 3265, 3266, 3267, 3268, 3269, 3270, ... |
| 12 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 3300, 3301, 3302, 3303, 3304, 3305, 3306, 3307, ... |
| 12 | `Assets/_Project/Scripts/World/GlobalWorldSampler.cs` | 189, 190, 191, 192, 193, 194, 195, 196, ... |
| 11 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs` | 1543, 1544, 1545, 1546, 1547, 1548, 1549, 1550, ... |
| 11 | `Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonRuntime.cs` | 43, 44, 45, 46, 47, 48, 49, 50, ... |
| 11 | `Assets/_Project/Scripts/SaveSystem/SaveStateMerkleTree.cs` | 192, 193, 194, 195, 196, 197, 198, 199, ... |
| 11 | `Assets/_Project/Scripts/World/FloraInteractionManager.cs` | 1456, 1457, 1481, 1499, 1500, 1501, 1502, 1503, ... |
| 10 | `Assets/_Project/Scripts/Construction/LogisticsPipeTransportScheduler.cs` | 29, 31, 33, 35, 36, 37, 38, 39, ... |
| 10 | `Assets/_Project/Scripts/WorldProceduralScatterWorkingMemory.cs` | 23, 24, 25, 26, 27, 59, 65, 66, ... |
| 9 | `Assets/_Project/Scripts/InventoryGrid.cs` | 54, 55, 56, 57, 58, 59, 60, 61, ... |

## Allowed Allocator-Internal Sites

| Count | Path | Lines |
|---:|---|---|
| 53 | `Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs` | 152, 153, 154, 319, 320, 396, 397, 398, ... |
| 21 | `Assets/_Project/Scripts/World/BiomeWeightMapBaker/Editor/BiomeWeightMapBakePipeline.cs` | 176, 177, 178, 179, 180, 181, 183, 184, ... |
| 18 | `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/Shinobu213/OfflineGeometryBaker.cs` | 399, 459, 473, 475, 477, 479, 555, 668, ... |
| 17 | `Assets/_Project/Scripts/World/OfflineWreckageBaker/Editor/WreckageForgeWindow.cs` | 259, 260, 261, 262, 263, 311, 312, 313, ... |
| 12 | `Assets/_Project/Scripts/Editor/ErosionTestHarness.cs` | 82, 83, 84, 85, 86, 91, 265, 266, ... |
| 11 | `Assets/_Project/Scripts/World/StaticCaveSdfBaker/Editor/StaticCaveSdfBakePipeline.cs` | 104, 172, 173, 174, 175, 194, 195, 222, ... |
| 11 | `Assets/_Project/Scripts/World/VoxelTerrainSeamBinder/Editor/VoxelTerrainSeamBinderPipeline.cs` | 85, 126, 127, 128, 129, 334, 335, 338, ... |
| 10 | `Assets/_Project/Scripts/Dev/OmegaAutonomySmokeTester.cs` | 150, 151, 152, 153, 154, 155, 385, 386, ... |
| 10 | `Assets/_Project/Scripts/VoxelDeformationSmokeTester.cs` | 222, 223, 224, 272, 273, 320, 321, 322, ... |
| 9 | `Assets/_Project/Scripts/Editor/AnomalySmokeTester.cs` | 75, 76, 77, 78, 79, 80, 81, 189, ... |
| 9 | `Assets/_Project/Scripts/Editor/GeographySanity/GeographySanityPipeline.cs` | 267, 268, 269, 270, 271, 272, 273, 274, ... |
| 9 | `Assets/_Project/Scripts/Editor/GeologyForge/GeologyForgeGenerator.cs` | 112, 174, 287, 313, 326, 342, 640, 667, ... |
| 9 | `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/InteriorClutterForge.cs` | 397, 399, 401, 404, 425, 427, 497, 1014, ... |
| 8 | `Assets/_Project/Scripts/World/OfflineWreckageBaker/Editor/OfflineWreckageMockBenchmark.cs` | 45, 46, 47, 48, 49, 50, 51, 52 |
| 7 | `Assets/_Project/Scripts/Editor/GeologyForge/TopographyForgeWindow.cs` | 182, 186, 188, 189, 190, 191, 192 |
| 7 | `Assets/_Project/Scripts/Editor/HydraulicErosionSmokeTester.cs` | 207, 208, 209, 210, 211, 212, 213 |
| 6 | `Assets/_Project/Scripts/AutomationOmegaSmokeTester.cs` | 51, 52, 53, 54, 55, 56 |
| 6 | `Assets/_Project/Scripts/AutomationSmokeTester.cs` | 39, 40, 41, 42, 43, 44 |
| 6 | `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` | 1831, 1833, 1837, 1839, 1919, 3133 |
| 6 | `Assets/_Project/Scripts/Dev/SpaceEngine098/SpaceEngine098TerrainSmokeTester.cs` | 135, 136, 137, 138, 139, 140 |
| 6 | `Assets/_Project/Scripts/ThermalMeltSmokeTester.cs` | 115, 116, 152, 153, 193, 194 |
| 6 | `Assets/_Project/Scripts/World/BiomeTransitionSmokeTester.cs` | 93, 94, 98, 99, 100, 101 |
| 5 | `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/InteriorClutterForgeSupport.cs` | 28, 150, 357, 359, 534 |
| 5 | `Assets/_Project/Scripts/Editor/ProceduralGen/BioForgeGenerator.cs` | 77, 204, 209, 256, 303 |
| 5 | `Assets/_Project/Scripts/FaunaRuntimeSmokeTester.cs` | 183, 184, 185, 257, 258 |
| 4 | `Assets/_Project/Scripts/Editor/TextureChannelPacker/TextureChannelPackerWindow.cs` | 399, 400, 401, 620 |
| 4 | `Assets/_Project/Scripts/Gameplay/Editor/ScannerLoreDatabaseSyncTunerWindow.cs` | 130, 131, 132, 133 |
| 4 | `Assets/_Project/Scripts/World/HectonSandboxAbyssalShelfSmokeTester.cs` | 106, 107, 108, 109 |
| 4 | `Assets/_Project/Scripts/World/PlanetaryCanvasSmokeTester.cs` | 36, 37, 38, 39 |
| 3 | `Assets/_Project/Scripts/Core/Data/Editor/CacheBTreeTopologyXRayWindow.cs` | 459, 460, 461 |
| 3 | `Assets/_Project/Scripts/Dev/BiomeBoundarySdfSmokeTester.cs` | 40, 41, 42 |
| 3 | `Assets/_Project/Scripts/Editor/HectonOctahedralImpostorBaker.cs` | 124, 128, 239 |
| 3 | `Assets/_Project/Scripts/SaveRecoverySmokeTester.cs` | 397, 401, 403 |
| 2 | `Assets/_Project/Scripts/Editor/Arm64MemoryAlignmentXRayWindow.cs` | 69, 70 |
| 2 | `Assets/_Project/Scripts/Editor/EconomyRecipeTunerWindow.cs` | 224, 263 |
| 2 | `Assets/_Project/Scripts/Editor/HlodImpostorForgeWindow.cs` | 134, 190 |
| 2 | `Assets/_Project/Scripts/Editor/WorldProceduralProxySceneBuilder.cs` | 273, 274 |
| 2 | `Assets/_Project/Scripts/SavePersistenceOmegaSmokeTester.cs` | 95, 96 |
| 2 | `Assets/_Project/Scripts/World/VoxelTerrainSeamBinder/Editor/VoxelTerrainSeamPreviewGizmo.cs` | 53, 54 |
| 1 | `Assets/_Project/Scripts/Dev/HabitatStressSmokeTester.cs` | 504 |

## Allowed DataVault/H8Memory Declaration Sites

| Count | Path | Lines |
|---:|---|---|
| 41 | `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs` | 5430, 5431, 5432, 5433, 5434, 5435, 5436, 5437, ... |
| 36 | `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | 436, 438, 439, 440, 441, 447, 448, 449, ... |
| 29 | `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs` | 1495, 1496, 1497, 1498, 1499, 1500, 1501, 1502, ... |
| 26 | `Assets/_Project/Scripts/ModularEquipmentEngine.cs` | 137, 138, 139, 140, 141, 142, 143, 144, ... |
| 26 | `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs` | 992, 993, 994, 995, 996, 997, 998, 999, ... |
| 22 | `Assets/_Project/Scripts/HectonFluidEngine.cs` | 7183, 7184, 7185, 7186, 7187, 7188, 7189, 7190, ... |
| 21 | `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 2187, 2188, 2189, 2190, 2191, 2192, 2193, 2194, ... |
| 21 | `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs` | 5089, 5091, 5092, 5093, 5094, 5095, 5096, 5097, ... |
| 21 | `Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs` | 165, 166, 167, 168, 169, 170, 171, 172, ... |
| 20 | `Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs` | 333, 334, 335, 336, 337, 338, 339, 340, ... |
| 20 | `Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralVault.cs` | 61, 62, 63, 64, 65, 66, 67, 68, ... |
| 19 | `Assets/_Project/Scripts/Construction/ShinobuSocketConstructionData.cs` | 249, 250, 251, 252, 253, 254, 255, 256, ... |
| 18 | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs` | 2215, 2216, 2217, 2218, 2219, 2220, 2221, 2222, ... |
| 18 | `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs` | 1799, 1801, 1802, 1803, 1804, 1805, 1806, 1807, ... |
| 18 | `Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageVault.cs` | 57, 58, 59, 60, 61, 62, 63, 64, ... |
| 18 | `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs` | 57, 58, 59, 60, 61, 62, 63, 64, ... |
| 17 | `Assets/_Project/Scripts/Cartography/CartographyGridJobs.cs` | 335, 336, 337, 338, 339, 340, 341, 342, ... |
| 17 | `Assets/_Project/Scripts/Construction/DroneCognitionJob.cs` | 310, 311, 312, 313, 314, 315, 316, 318, ... |
| 17 | `Assets/_Project/Scripts/Inventory/Algorithms/InventoryDefragJob.cs` | 30, 31, 32, 33, 34, 35, 36, 37, ... |
| 17 | `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs` | 918, 919, 920, 921, 922, 923, 924, 925, ... |
| 17 | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` | 2666, 2667, 2671, 2672, 2673, 2674, 2675, 2676, ... |
| 17 | `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 4627, 4629, 4630, 4631, 4632, 4633, 4634, 4635, ... |
| 16 | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` | 2058, 2059, 2060, 2061, 2062, 2063, 2064, 2065, ... |
| 16 | `Assets/_Project/Scripts/Quest/QuestDagRuntimeTypes.cs` | 301, 302, 303, 304, 305, 306, 307, 308, ... |
| 15 | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainVault.cs` | 85, 86, 87, 88, 89, 90, 91, 92, ... |
| 15 | `Assets/_Project/Scripts/Construction/FoundationSnappingCalculatorData.cs` | 186, 187, 188, 189, 190, 191, 192, 193, ... |
| 15 | `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs` | 455, 456, 457, 458, 459, 460, 461, 462, ... |
| 15 | `Assets/_Project/Scripts/Logistics/FluidPipePressureJobs.cs` | 18, 19, 20, 21, 22, 23, 24, 25, ... |
| 15 | `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs` | 700, 701, 702, 703, 704, 705, 706, 707, ... |
| 15 | `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs` | 1076, 1077, 1078, 1079, 1080, 1081, 1082, 1083, ... |
| 14 | `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 2049, 2050, 2051, 2052, 2053, 2054, 2055, 2056, ... |
| 14 | `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsJobs.cs` | 16, 17, 18, 19, 20, 21, 22, 23, ... |
| 14 | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` | 2110, 2111, 2112, 2113, 2114, 2115, 2116, 2117, ... |
| 14 | `Assets/_Project/Scripts/Quest/QuestDagResolverRuntime.cs` | 634, 635, 636, 637, 638, 639, 640, 641, ... |
| 14 | `Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs` | 3107, 3108, 3109, 3110, 3111, 3112, 3113, 3114, ... |
| 13 | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainJobs.cs` | 80, 81, 82, 83, 84, 85, 86, 87, ... |
| 13 | `Assets/_Project/Scripts/Fauna/LeviathanTentacleVerletSolver.cs` | 553, 554, 555, 556, 557, 558, 559, 560, ... |
| 13 | `Assets/_Project/Scripts/Fauna/MesofaunaBehavioralStateMachine.cs` | 576, 577, 578, 579, 580, 581, 582, 583, ... |
| 13 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 466, 469, 470, 471, 472, 473, 474, 476, ... |
| 13 | `Assets/_Project/Scripts/Inventory/Routing/InventoryRoutingNetwork.cs` | 160, 161, 162, 163, 164, 165, 166, 167, ... |

## Gate Commands

```powershell
python Tools\DataVaultSovereigntyAudit.py --fail-on-regression
python Tools\DataVaultSovereigntyAudit.py --fail-on-any
```

`--fail-on-regression` blocks any new or increased forbidden constructor or field-declaration count against the baseline.
`--fail-on-any` is the final zero-debt gate and currently fails until all legacy debt is migrated.
