# DataVault Sovereignty Audit - VAULT_SOVEREIGNTY_ENFORCER

Schema: `hecton8.datavault_sovereignty_audit.v3`
Status: `FAIL_REGRESSION`
Source root: `Assets/_Project/Scripts`
Pattern: `\bnew\s+NativeArray\s*<`
Baseline: `Docs/AgentLogs/DataVaultSovereigntyBaselineCandidate_HFI_AUDIT.json`

## Summary

| Metric | Count |
|---|---:|
| Total direct `new NativeArray<T>` constructors | 1147 |
| Allowed allocator-internal constructors | 6 |
| Forbidden system constructors | 1141 |
| Files with forbidden constructors | 173 |
| Total field-like `NativeArray<T>` declarations | 5526 |
| Allowed DataVault/H8Memory declarations | 3807 |
| Forbidden system declarations | 1719 |
| Persistent owner native collection declarations | 1043 |
| Job input native collection declarations | 3788 |
| Burst job input native collection declarations | 3785 |
| Unknown struct native collection declarations | 695 |
| Files with forbidden declarations | 264 |

## Regression Findings

- Baseline schema mismatch: 'hecton8.datavault_sovereignty_baseline.v2'.
- Assets/_Project/Scripts/Editor/OfflineGeometryBaker/Shinobu213/OfflineGeometryBaker.cs: forbidden direct constructors increased from 15 to 18.
- Assets/_Project/Scripts/Editor/TextureChannelPacker/HectonArmTextureChannelPacker.cs: forbidden direct constructors increased from 0 to 16.
- Assets/_Project/Scripts/Editor/TextureChannelPacker/TextureChannelPackerWindow.cs: forbidden direct constructors increased from 0 to 4.
- Assets/_Project/Scripts/Gameplay/Editor/ScannerLoreDatabaseSyncTunerWindow.cs: forbidden direct constructors increased from 0 to 4.
- Assets/_Project/Scripts/Habitat/Deformation/Editor/DamageBake/HabitatDamageBakePipeline.cs: forbidden direct constructors increased from 9 to 11.
- Assets/_Project/Scripts/World/OfflineWreckageBaker/Editor/WreckageForgeWindow.cs: forbidden direct constructors increased from 16 to 17.
- Assets/_Project/Scripts/Audio/ProceduralAudioEvents.cs: forbidden NativeArray field declarations increased from 0 to 2.
- Assets/_Project/Scripts/Construction/ShinobuSocketConstructionData.cs: forbidden NativeArray field declarations increased from 12 to 19.
- Assets/_Project/Scripts/Core/MathGuard.cs: forbidden NativeArray field declarations increased from 0 to 2.
- Assets/_Project/Scripts/Editor/TextureChannelPacker/HectonArmTextureChannelPacker.cs: forbidden NativeArray field declarations increased from 0 to 1.
- Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentJobs.cs: forbidden NativeArray field declarations increased from 0 to 5.
- Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentRouterRuntime.cs: forbidden NativeArray field declarations increased from 0 to 12.
- Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs: forbidden NativeArray field declarations increased from 0 to 2.
- Assets/_Project/Scripts/Inventory/ItemTemplateRegistry.cs: forbidden NativeArray field declarations increased from 0 to 1.
- Assets/_Project/Scripts/ModdingAPI/IModResourceProxy.cs: forbidden NativeArray field declarations increased from 0 to 1.
- Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs: forbidden NativeArray field declarations increased from 0 to 3.
- Assets/_Project/Scripts/ModularEquipmentEngine.cs: forbidden NativeArray field declarations increased from 18 to 23.
- Assets/_Project/Scripts/PDA/PlayerExplorationTracker.cs: forbidden NativeArray field declarations increased from 0 to 1.
- Assets/_Project/Scripts/PrefabRegistry.cs: forbidden NativeArray field declarations increased from 0 to 1.
- Assets/_Project/Scripts/SeamRegistry.cs: forbidden NativeArray field declarations increased from 0 to 1.
- Assets/_Project/Scripts/Tools/UpgradeMatrixCompiler.cs: forbidden NativeArray field declarations increased from 0 to 12.
- Assets/_Project/Scripts/World/FaunaSpatialHashRegistry.cs: forbidden NativeArray field declarations increased from 0 to 1.
- Assets/_Project/Scripts/World/HectonSpatialHash.cs: forbidden NativeArray field declarations increased from 2 to 5.

## Regression Delta By Execution Surface

| Surface | Delta | Direct constructor delta | Field declaration delta | Files |
|---|---:|---:|---:|---:|
| `Runtime` | 59 | 0 | 59 | 16 |
| `Editor` | 31 | 30 | 1 | 6 |

## Regression Delta By Domain

| Domain | Delta | Direct constructor delta | Field declaration delta | Files |
|---|---:|---:|---:|---:|
| `Editor` | 24 | 23 | 1 | 3 |
| `Equipment` | 17 | 0 | 17 | 2 |
| `Tools` | 12 | 0 | 12 | 1 |
| `Construction` | 7 | 0 | 7 | 1 |
| `Root` | 7 | 0 | 7 | 3 |
| `Gameplay` | 6 | 4 | 2 | 2 |
| `World` | 5 | 1 | 4 | 3 |
| `ModdingAPI` | 4 | 0 | 4 | 2 |
| `Audio` | 2 | 0 | 2 | 1 |
| `Core` | 2 | 0 | 2 | 1 |
| `Habitat` | 2 | 2 | 0 | 1 |
| `Inventory` | 1 | 0 | 1 | 1 |
| `PDA` | 1 | 0 | 1 | 1 |

## Regression Delta Details

| Kind | Surface | Domain | Baseline | Current | Delta | Path |
|---|---|---|---:|---:|---:|---|
| `directConstructor` | `Editor` | `Editor` | 0 | 16 | 16 | `Assets/_Project/Scripts/Editor/TextureChannelPacker/HectonArmTextureChannelPacker.cs` |
| `fieldDeclaration` | `Runtime` | `Equipment` | 0 | 12 | 12 | `Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentRouterRuntime.cs` |
| `fieldDeclaration` | `Runtime` | `Tools` | 0 | 12 | 12 | `Assets/_Project/Scripts/Tools/UpgradeMatrixCompiler.cs` |
| `fieldDeclaration` | `Runtime` | `Construction` | 12 | 19 | 7 | `Assets/_Project/Scripts/Construction/ShinobuSocketConstructionData.cs` |
| `fieldDeclaration` | `Runtime` | `Equipment` | 0 | 5 | 5 | `Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentJobs.cs` |
| `fieldDeclaration` | `Runtime` | `Root` | 18 | 23 | 5 | `Assets/_Project/Scripts/ModularEquipmentEngine.cs` |
| `directConstructor` | `Editor` | `Editor` | 0 | 4 | 4 | `Assets/_Project/Scripts/Editor/TextureChannelPacker/TextureChannelPackerWindow.cs` |
| `directConstructor` | `Editor` | `Gameplay` | 0 | 4 | 4 | `Assets/_Project/Scripts/Gameplay/Editor/ScannerLoreDatabaseSyncTunerWindow.cs` |
| `directConstructor` | `Editor` | `Editor` | 15 | 18 | 3 | `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/Shinobu213/OfflineGeometryBaker.cs` |
| `fieldDeclaration` | `Runtime` | `ModdingAPI` | 0 | 3 | 3 | `Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs` |
| `fieldDeclaration` | `Runtime` | `World` | 2 | 5 | 3 | `Assets/_Project/Scripts/World/HectonSpatialHash.cs` |
| `fieldDeclaration` | `Runtime` | `Audio` | 0 | 2 | 2 | `Assets/_Project/Scripts/Audio/ProceduralAudioEvents.cs` |
| `fieldDeclaration` | `Runtime` | `Core` | 0 | 2 | 2 | `Assets/_Project/Scripts/Core/MathGuard.cs` |
| `fieldDeclaration` | `Runtime` | `Gameplay` | 0 | 2 | 2 | `Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs` |
| `directConstructor` | `Editor` | `Habitat` | 9 | 11 | 2 | `Assets/_Project/Scripts/Habitat/Deformation/Editor/DamageBake/HabitatDamageBakePipeline.cs` |
| `fieldDeclaration` | `Editor` | `Editor` | 0 | 1 | 1 | `Assets/_Project/Scripts/Editor/TextureChannelPacker/HectonArmTextureChannelPacker.cs` |
| `fieldDeclaration` | `Runtime` | `Inventory` | 0 | 1 | 1 | `Assets/_Project/Scripts/Inventory/ItemTemplateRegistry.cs` |
| `fieldDeclaration` | `Runtime` | `ModdingAPI` | 0 | 1 | 1 | `Assets/_Project/Scripts/ModdingAPI/IModResourceProxy.cs` |
| `fieldDeclaration` | `Runtime` | `PDA` | 0 | 1 | 1 | `Assets/_Project/Scripts/PDA/PlayerExplorationTracker.cs` |
| `fieldDeclaration` | `Runtime` | `Root` | 0 | 1 | 1 | `Assets/_Project/Scripts/PrefabRegistry.cs` |
| `fieldDeclaration` | `Runtime` | `Root` | 0 | 1 | 1 | `Assets/_Project/Scripts/SeamRegistry.cs` |
| `fieldDeclaration` | `Runtime` | `World` | 0 | 1 | 1 | `Assets/_Project/Scripts/World/FaunaSpatialHashRegistry.cs` |
| `directConstructor` | `Editor` | `World` | 16 | 17 | 1 | `Assets/_Project/Scripts/World/OfflineWreckageBaker/Editor/WreckageForgeWindow.cs` |

## Top 40 Forbidden Files

| Count | Path | Lines |
|---:|---|---|
| 63 | `Assets/_Project/Scripts/PlayerInventory.cs` | 665, 667, 669, 671, 673, 675, 677, 678, ... |
| 53 | `Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs` | 152, 153, 154, 319, 320, 396, 397, 398, ... |
| 40 | `Assets/_Project/Scripts/HectonFluidEngine.cs` | 3660, 3669, 3678, 3687, 3696, 4562, 4564, 4566, ... |
| 40 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 129, 392, 3989, 3990, 3991, 3992, 3993, 5342, ... |
| 33 | `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs` | 3342, 3344, 3346, 3348, 3350, 3352, 3354, 3356, ... |
| 32 | `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 757, 758, 759, 760, 761, 762, 763, 764, ... |
| 32 | `Assets/_Project/Scripts/SaveBinaryStorage.cs` | 588, 1069, 2607, 3706, 3729, 4093, 4206, 4280, ... |
| 26 | `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | 1491, 1494, 1497, 1500, 1502, 1504, 1506, 1508, ... |
| 23 | `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs` | 4573, 4575, 4577, 4579, 4581, 4583, 4585, 4587, ... |
| 18 | `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/Shinobu213/OfflineGeometryBaker.cs` | 338, 395, 406, 408, 410, 412, 485, 595, ... |
| 18 | `Assets/_Project/Scripts/SaveManager.cs` | 981, 991, 1001, 1067, 1074, 1081, 1088, 1098, ... |
| 18 | `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 3502, 3511, 3519, 3527, 3535, 3543, 3551, 3559, ... |
| 17 | `Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs` | 274, 275, 276, 277, 278, 279, 280, 281, ... |
| 17 | `Assets/_Project/Scripts/HectonNarrativeDirector.cs` | 453, 454, 455, 456, 457, 458, 459, 460, ... |
| 17 | `Assets/_Project/Scripts/World/OfflineWreckageBaker/Editor/WreckageForgeWindow.cs` | 259, 260, 261, 262, 263, 311, 312, 313, ... |
| 16 | `Assets/_Project/Scripts/Editor/TextureChannelPacker/HectonArmTextureChannelPacker.cs` | 145, 146, 147, 148, 181, 182, 338, 358, ... |
| 15 | `Assets/_Project/Scripts/HectonWorldGenerator.cs` | 1040, 1402, 1403, 1404, 1405, 1406, 1407, 1408, ... |
| 15 | `Assets/_Project/Scripts/SubmarineStructuralGrid.cs` | 1938, 1940, 1941, 1943, 1945, 1947, 1949, 1951, ... |
| 15 | `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs` | 1780, 1802, 1805, 1808, 1814, 1817, 1820, 1823, ... |
| 14 | `Assets/_Project/Scripts/VoxelDeltaProcessor.cs` | 1640, 3058, 3063, 3065, 3067, 3069, 3071, 3073, ... |
| 13 | `Assets/_Project/Scripts/EncounterDirector.cs` | 329, 330, 331, 332, 333, 334, 335, 336, ... |
| 12 | `Assets/_Project/Scripts/Editor/ErosionTestHarness.cs` | 82, 83, 84, 85, 86, 91, 265, 266, ... |
| 12 | `Assets/_Project/Scripts/Fabricator.cs` | 1354, 1361, 1368, 1375, 1381, 1387, 1393, 1399, ... |
| 12 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs` | 2221, 2225, 2232, 2236, 2240, 2244, 2260, 2264, ... |
| 12 | `Assets/_Project/Scripts/VFX/Debris/ShinobuVoxelSculptorWindow.cs` | 247, 248, 249, 250, 608, 609, 610, 611, ... |
| 12 | `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs` | 1963, 2790, 2863, 3069, 3546, 3547, 3682, 3683, ... |
| 11 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs` | 1759, 1768, 1777, 1786, 1795, 1804, 1813, 1822, ... |
| 11 | `Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonRuntime.cs` | 475, 476, 477, 478, 479, 480, 481, 482, ... |
| 11 | `Assets/_Project/Scripts/Habitat/Deformation/Editor/DamageBake/HabitatDamageBakePipeline.cs` | 690, 952, 1073, 1074, 1075, 1076, 1077, 1078, ... |
| 11 | `Assets/_Project/Scripts/PowerGrid.cs` | 1303, 1306, 1309, 1312, 1315, 1318, 1321, 1331, ... |
| 10 | `Assets/_Project/Scripts/Dev/OmegaAutonomySmokeTester.cs` | 150, 151, 152, 153, 154, 155, 385, 386, ... |
| 10 | `Assets/_Project/Scripts/Editor/LSystemGenomeLabWindow.cs` | 109, 130, 131, 132, 133, 227, 228, 229, ... |
| 10 | `Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs` | 343, 344, 345, 346, 388, 389, 390, 391, ... |
| 10 | `Assets/_Project/Scripts/Plugins/MapMagic/HectonAnomalyMapMagicNode.cs` | 221, 222, 223, 224, 225, 226, 227, 228, ... |
| 10 | `Assets/_Project/Scripts/VoxelDeformationSmokeTester.cs` | 222, 223, 224, 272, 273, 320, 321, 322, ... |
| 9 | `Assets/_Project/Scripts/Editor/AnomalySmokeTester.cs` | 75, 76, 77, 78, 79, 80, 81, 189, ... |
| 9 | `Assets/_Project/Scripts/Editor/GeologyForge/GeologyForgeGenerator.cs` | 112, 174, 275, 300, 312, 328, 623, 649, ... |
| 9 | `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/InteriorClutterForge.cs` | 397, 399, 401, 404, 428, 430, 498, 1018, ... |
| 9 | `Assets/_Project/Scripts/InventoryGrid.cs` | 96, 97, 98, 99, 100, 101, 102, 103, ... |
| 9 | `Assets/_Project/Scripts/World/AbyssalThermalManager.cs` | 2375, 2386, 2397, 2408, 2418, 2429, 2792, 2802, ... |

## Top 40 Forbidden Declaration Files

| Count | Path | Lines |
|---:|---|---|
| 52 | `Assets/_Project/Scripts/World/VegetationMemoryPool.cs` | 94, 95, 96, 97, 98, 99, 100, 101, ... |
| 49 | `Assets/_Project/Scripts/PlayerInventory.cs` | 519, 520, 521, 522, 523, 524, 525, 526, ... |
| 40 | `Assets/_Project/Scripts/HectonFluidEngine.cs` | 1380, 1381, 1382, 1383, 1384, 1385, 1386, 1387, ... |
| 40 | `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | 1411, 1412, 1413, 1414, 1415, 1416, 1422, 1423, ... |
| 37 | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs` | 643, 644, 645, 646, 647, 648, 649, 657, ... |
| 33 | `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs` | 1415, 1416, 1417, 1418, 1419, 1420, 1421, 1422, ... |
| 32 | `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 102, 103, 104, 105, 106, 107, 108, 109, ... |
| 28 | `Assets/_Project/Scripts/SpatialAudioManager.cs` | 916, 917, 1020, 1021, 1022, 1023, 1024, 1025, ... |
| 25 | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` | 211, 212, 213, 214, 215, 216, 217, 218, ... |
| 25 | `Assets/_Project/Scripts/TetherInstance.cs` | 230, 231, 232, 233, 234, 235, 236, 237, ... |
| 23 | `Assets/_Project/Scripts/ModularEquipmentEngine.cs` | 133, 134, 135, 136, 137, 138, 139, 140, ... |
| 22 | `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs` | 307, 309, 310, 311, 312, 313, 314, 315, ... |
| 22 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 3385, 3386, 3387, 3388, 3389, 3392, 3393, 3394, ... |
| 21 | `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs` | 272, 273, 274, 275, 276, 277, 278, 279, ... |
| 21 | `Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs` | 161, 162, 163, 164, 165, 166, 167, 168, ... |
| 20 | `Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs` | 329, 330, 331, 332, 333, 334, 335, 336, ... |
| 20 | `Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralVault.cs` | 61, 62, 63, 64, 65, 66, 67, 68, ... |
| 19 | `Assets/_Project/Scripts/Construction/ShinobuSocketConstructionData.cs` | 245, 246, 247, 248, 249, 250, 251, 252, ... |
| 19 | `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` | 361, 362, 363, 364, 365, 366, 387, 389, ... |
| 18 | `Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs` | 194, 195, 196, 197, 198, 199, 200, 201, ... |
| 18 | `Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageVault.cs` | 57, 58, 59, 60, 61, 62, 63, 64, ... |
| 18 | `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs` | 57, 58, 59, 60, 61, 62, 63, 64, ... |
| 18 | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs` | 843, 844, 851, 853, 854, 855, 856, 857, ... |
| 17 | `Assets/_Project/Scripts/Cartography/CartographyGridJobs.cs` | 330, 331, 332, 333, 334, 335, 336, 337, ... |
| 17 | `Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs` | 37, 38, 39, 40, 41, 42, 43, 44, ... |
| 17 | `Assets/_Project/Scripts/HectonNarrativeDirector.cs` | 183, 184, 185, 186, 187, 188, 189, 190, ... |
| 17 | `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` | 367, 368, 369, 370, 371, 372, 373, 374, ... |
| 16 | `Assets/_Project/Scripts/Quest/QuestDagRuntimeTypes.cs` | 301, 302, 303, 304, 305, 306, 307, 308, ... |
| 16 | `Assets/_Project/Scripts/SaveSystem/VoxelDeltaCompressionArchitecture.cs` | 169, 170, 171, 172, 173, 174, 175, 176, ... |
| 15 | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainVault.cs` | 79, 80, 81, 82, 83, 84, 85, 86, ... |
| 15 | `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs` | 290, 291, 292, 293, 294, 295, 296, 297, ... |
| 15 | `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs` | 455, 456, 457, 458, 459, 460, 461, 462, ... |
| 15 | `Assets/_Project/Scripts/SubmarineStructuralGrid.cs` | 553, 554, 555, 556, 557, 558, 559, 560, ... |
| 15 | `Assets/_Project/Scripts/Tools/ToolKinematics/ToolKinematicsRuntime.cs` | 1269, 1270, 1271, 1272, 1273, 1274, 1275, 1276, ... |
| 14 | `Assets/_Project/Scripts/Core/DodReplayRecorder.cs` | 381, 382, 383, 384, 385, 386, 387, 388, ... |
| 13 | `Assets/_Project/Scripts/EncounterDirector.cs` | 277, 278, 279, 280, 281, 282, 283, 284, ... |
| 13 | `Assets/_Project/Scripts/Fauna/LeviathanTentacleVerletSolver.cs` | 548, 549, 550, 551, 552, 553, 554, 555, ... |
| 13 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs` | 110, 111, 112, 113, 114, 115, 116, 117, ... |
| 13 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs` | 1337, 1338, 1339, 1340, 1341, 1342, 1343, 1344, ... |
| 13 | `Assets/_Project/Scripts/Inventory/Routing/InventoryRoutingNetwork.cs` | 160, 161, 162, 163, 164, 165, 166, 167, ... |

## Allowed Allocator-Internal Sites

| Count | Path | Lines |
|---:|---|---|
| 6 | `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` | 1794, 1796, 1800, 1802, 1881, 3094 |

## Allowed DataVault/H8Memory Declaration Sites

| Count | Path | Lines |
|---:|---|---|
| 41 | `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs` | 5409, 5410, 5411, 5412, 5413, 5414, 5415, 5416, ... |
| 36 | `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | 416, 418, 419, 420, 421, 427, 428, 429, ... |
| 29 | `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs` | 1495, 1496, 1497, 1498, 1499, 1500, 1501, 1502, ... |
| 26 | `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs` | 902, 903, 904, 905, 906, 907, 908, 909, ... |
| 22 | `Assets/_Project/Scripts/HectonFluidEngine.cs` | 7117, 7118, 7119, 7120, 7121, 7122, 7123, 7124, ... |
| 21 | `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 2157, 2158, 2159, 2160, 2161, 2162, 2163, 2164, ... |
| 21 | `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs` | 5068, 5070, 5071, 5072, 5073, 5074, 5075, 5076, ... |
| 18 | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs` | 2161, 2162, 2163, 2164, 2165, 2166, 2167, 2168, ... |
| 18 | `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs` | 1740, 1742, 1743, 1744, 1745, 1746, 1747, 1748, ... |
| 17 | `Assets/_Project/Scripts/Construction/DroneCognitionJob.cs` | 310, 311, 312, 313, 314, 315, 316, 318, ... |
| 17 | `Assets/_Project/Scripts/Inventory/Algorithms/InventoryDefragJob.cs` | 30, 31, 32, 33, 34, 35, 36, 37, ... |
| 17 | `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs` | 918, 919, 920, 921, 922, 923, 924, 925, ... |
| 17 | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` | 2671, 2672, 2676, 2677, 2678, 2679, 2680, 2681, ... |
| 17 | `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 4627, 4629, 4630, 4631, 4632, 4633, 4634, 4635, ... |
| 16 | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` | 2063, 2064, 2065, 2066, 2067, 2068, 2069, 2070, ... |
| 15 | `Assets/_Project/Scripts/Logistics/FluidPipePressureJobs.cs` | 18, 19, 20, 21, 22, 23, 24, 25, ... |
| 15 | `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs` | 700, 701, 702, 703, 704, 705, 706, 707, ... |
| 15 | `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs` | 1076, 1077, 1078, 1079, 1080, 1081, 1082, 1083, ... |
| 14 | `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 2019, 2020, 2021, 2022, 2023, 2024, 2025, 2026, ... |
| 14 | `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsJobs.cs` | 16, 17, 18, 19, 20, 21, 22, 23, ... |
| 14 | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` | 2115, 2116, 2117, 2118, 2119, 2120, 2121, 2122, ... |
| 14 | `Assets/_Project/Scripts/Quest/QuestDagResolverRuntime.cs` | 634, 635, 636, 637, 638, 639, 640, 641, ... |
| 14 | `Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs` | 2671, 2672, 2673, 2674, 2675, 2676, 2677, 2678, ... |
| 13 | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainJobs.cs` | 80, 81, 82, 83, 84, 85, 86, 87, ... |
| 13 | `Assets/_Project/Scripts/Fauna/MesofaunaBehavioralStateMachine.cs` | 576, 577, 578, 579, 580, 581, 582, 583, ... |
| 13 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 466, 469, 470, 471, 472, 473, 474, 476, ... |
| 13 | `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | 2621, 2622, 2623, 2624, 2625, 2626, 2627, 2628, ... |
| 12 | `Assets/_Project/Scripts/Inventory/InventorySoAUtility.cs` | 289, 290, 291, 292, 293, 294, 295, 296, ... |
| 12 | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs` | 3091, 3092, 3093, 3094, 3095, 3096, 3097, 3098, ... |
| 12 | `Assets/_Project/Scripts/World/EcosystemDirector.cs` | 569, 570, 571, 572, 573, 574, 575, 576, ... |
| 12 | `Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs` | 600, 601, 602, 603, 604, 605, 606, 607, ... |
| 11 | `Assets/_Project/Scripts/Atmosphere/StormPropagation/ShinobuStormPropagationJobs.cs` | 46, 47, 48, 49, 50, 51, 52, 53, ... |
| 11 | `Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs` | 943, 944, 945, 946, 947, 948, 949, 950, ... |
| 11 | `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs` | 2078, 2079, 2080, 2081, 2082, 2083, 2084, 2085, ... |
| 11 | `Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs` | 2441, 2442, 2443, 2444, 2445, 2446, 2447, 2448, ... |
| 11 | `Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralJobs.cs` | 231, 235, 239, 242, 245, 248, 251, 254, ... |
| 11 | `Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs` | 757, 758, 759, 760, 761, 762, 763, 764, ... |
| 11 | `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsJobs.cs` | 55, 58, 61, 64, 67, 71, 75, 78, ... |
| 10 | `Assets/_Project/Scripts/Construction/DroneFleetNavigationKernel.cs` | 1153, 1154, 1155, 1156, 1157, 1158, 1159, 1160, ... |
| 10 | `Assets/_Project/Scripts/Data/Monolith/H8CreatureSoAReconstructJob.cs` | 55, 59, 60, 61, 62, 63, 64, 65, ... |

## Gate Commands

```powershell
python Tools\DataVaultSovereigntyAudit.py --fail-on-regression
python Tools\DataVaultSovereigntyAudit.py --fail-on-any
```

`--fail-on-regression` blocks any new or increased forbidden constructor or field-declaration count against the baseline.
`--fail-on-any` is the final zero-debt gate and currently fails until all legacy debt is migrated.
