# DataVault Sovereignty Audit - VAULT_SOVEREIGNTY_ENFORCER

Schema: `hecton8.datavault_sovereignty_audit.v2`
Status: `PASS_NO_REGRESSION_WITH_LEGACY_DEBT`
Source root: `Assets/_Project/Scripts`
Pattern: `\bnew\s+NativeArray\s*<`
Baseline: `Docs/AgentLogs/DataVaultSovereigntyBaseline_VAULT_SOVEREIGNTY_ENFORCER.json`

## Summary

| Metric | Count |
|---|---:|
| Total direct `new NativeArray<T>` constructors | 1128 |
| Allowed allocator-internal constructors | 6 |
| Forbidden system constructors | 1122 |
| Files with forbidden constructors | 174 |
| Total field-like `NativeArray<T>` declarations | 2709 |
| Allowed DataVault/H8Memory declarations | 6 |
| Forbidden system declarations | 2703 |
| Files with forbidden declarations | 243 |

## Top 40 Forbidden Files

| Count | Path | Lines |
|---:|---|---|
| 63 | `Assets/_Project/Scripts/PlayerInventory.cs` | 652, 654, 656, 658, 660, 662, 664, 665, ... |
| 53 | `Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs` | 152, 153, 154, 319, 320, 396, 397, 398, ... |
| 44 | `Assets/_Project/Scripts/HectonFluidEngine.cs` | 3726, 3735, 3744, 3753, 3762, 3771, 3780, 3789, ... |
| 40 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 129, 392, 3963, 3964, 3965, 3966, 3967, 5304, ... |
| 33 | `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs` | 3316, 3318, 3320, 3322, 3324, 3326, 3328, 3330, ... |
| 32 | `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 754, 755, 756, 757, 758, 759, 760, 761, ... |
| 32 | `Assets/_Project/Scripts/SaveBinaryStorage.cs` | 586, 1067, 2337, 3400, 3423, 3787, 3900, 3974, ... |
| 27 | `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | 1436, 1439, 1442, 1445, 1447, 1449, 1451, 1453, ... |
| 23 | `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs` | 4549, 4551, 4553, 4555, 4557, 4559, 4561, 4563, ... |
| 18 | `Assets/_Project/Scripts/SaveManager.cs` | 944, 954, 964, 1030, 1037, 1044, 1051, 1061, ... |
| 18 | `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 3477, 3486, 3494, 3502, 3510, 3518, 3526, 3534, ... |
| 17 | `Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs` | 274, 275, 276, 277, 278, 279, 280, 281, ... |
| 17 | `Assets/_Project/Scripts/HectonNarrativeDirector.cs` | 434, 435, 436, 437, 438, 439, 440, 441, ... |
| 16 | `Assets/_Project/Scripts/VoxelDeltaProcessor.cs` | 630, 1589, 2926, 2931, 2933, 2935, 2937, 2939, ... |
| 15 | `Assets/_Project/Scripts/HectonWorldGenerator.cs` | 1036, 1371, 1372, 1373, 1374, 1375, 1376, 1377, ... |
| 15 | `Assets/_Project/Scripts/SubmarineStructuralGrid.cs` | 2042, 2044, 2045, 2047, 2049, 2051, 2053, 2055, ... |
| 15 | `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs` | 1764, 1786, 1789, 1792, 1798, 1801, 1804, 1807, ... |
| 14 | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs` | 1140, 1141, 1142, 1143, 1144, 1146, 1147, 1148, ... |
| 14 | `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs` | 1335, 1340, 1345, 1350, 1355, 1360, 1365, 1370, ... |
| 13 | `Assets/_Project/Scripts/EncounterDirector.cs` | 280, 281, 282, 283, 284, 285, 286, 287, ... |
| 12 | `Assets/_Project/Scripts/Editor/ErosionTestHarness.cs` | 82, 83, 84, 85, 86, 91, 265, 266, ... |
| 12 | `Assets/_Project/Scripts/Fabricator.cs` | 1300, 1307, 1314, 1321, 1341, 1347, 1353, 1359, ... |
| 12 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs` | 2212, 2216, 2223, 2227, 2231, 2235, 2251, 2255, ... |
| 11 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs` | 1738, 1747, 1756, 1765, 1774, 1783, 1792, 1801, ... |
| 11 | `Assets/_Project/Scripts/PowerGrid.cs` | 1293, 1296, 1299, 1302, 1305, 1308, 1311, 1321, ... |
| 11 | `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs` | 1372, 2047, 2120, 2524, 2525, 2755, 2769, 2794, ... |
| 10 | `Assets/_Project/Scripts/Dev/OmegaAutonomySmokeTester.cs` | 150, 151, 152, 153, 154, 155, 385, 386, ... |
| 10 | `Assets/_Project/Scripts/Plugins/MapMagic/HectonAnomalyMapMagicNode.cs` | 221, 222, 223, 224, 225, 226, 227, 228, ... |
| 10 | `Assets/_Project/Scripts/VoxelDeformationSmokeTester.cs` | 222, 223, 224, 272, 273, 320, 321, 322, ... |
| 9 | `Assets/_Project/Scripts/Editor/AnomalySmokeTester.cs` | 75, 76, 77, 78, 79, 80, 81, 189, ... |
| 9 | `Assets/_Project/Scripts/InventoryGrid.cs` | 96, 97, 98, 99, 100, 101, 102, 103, ... |
| 9 | `Assets/_Project/Scripts/ModularEquipmentEngine.cs` | 133, 144, 155, 179, 204, 216, 227, 249, ... |
| 9 | `Assets/_Project/Scripts/World/AbyssalThermalManager.cs` | 2395, 2406, 2417, 2428, 2438, 2449, 2744, 2754, ... |
| 8 | `Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs` | 2254, 2271, 2275, 2279, 2291, 2292, 2299, 2300 |
| 8 | `Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs` | 268, 269, 270, 271, 272, 273, 274, 275 |
| 8 | `Assets/_Project/Scripts/World/WorldSpatialHashGrid.cs` | 1623, 1624, 1625, 1725, 1726, 1727, 1728, 1845 |
| 7 | `Assets/_Project/Scripts/Construction/HabitatConstructionManager.cs` | 638, 640, 642, 644, 646, 648, 680 |
| 7 | `Assets/_Project/Scripts/Editor/HydraulicErosionSmokeTester.cs` | 207, 208, 209, 210, 211, 212, 213 |
| 7 | `Assets/_Project/Scripts/Gameplay/Mining/DeployableSdfDrillRuntime.cs` | 570, 571, 572, 573, 574, 575, 576 |
| 7 | `Assets/_Project/Scripts/Plugins/MapMagic/HectonSpaceEngine098MapMagicNodes.cs` | 108, 109, 256, 257, 258, 412, 413 |

## Top 40 Forbidden Declaration Files

| Count | Path | Lines |
|---:|---|---|
| 118 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 43, 44, 462, 465, 466, 467, 468, 469, ... |
| 104 | `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs` | 470, 471, 472, 473, 474, 475, 476, 477, ... |
| 86 | `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | 203, 206, 207, 208, 319, 320, 321, 322, ... |
| 79 | `Assets/_Project/Scripts/HectonFluidEngine.cs` | 362, 363, 364, 1308, 1309, 1310, 1311, 1312, ... |
| 67 | `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 102, 103, 104, 105, 106, 107, 108, 109, ... |
| 67 | `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | 1133, 1134, 1135, 1136, 1137, 1138, 1140, 1590, ... |
| 67 | `Assets/_Project/Scripts/World/VegetationMemoryPool.cs` | 21, 22, 23, 24, 25, 26, 27, 28, ... |
| 66 | `Assets/_Project/Scripts/PlayerInventory.cs` | 175, 176, 177, 178, 179, 212, 213, 214, ... |
| 59 | `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs` | 882, 883, 884, 885, 886, 887, 888, 889, ... |
| 52 | `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs` | 629, 631, 633, 635, 637, 639, 641, 643, ... |
| 51 | `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs` | 299, 301, 302, 303, 304, 305, 306, 307, ... |
| 51 | `Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs` | 132, 133, 134, 135, 136, 137, 138, 139, ... |
| 43 | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs` | 130, 131, 220, 266, 267, 268, 269, 270, ... |
| 38 | `Assets/_Project/Scripts/VoxelDeltaProcessor.cs` | 170, 179, 3901, 3902, 3903, 3904, 3905, 3906, ... |
| 34 | `Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs` | 97, 98, 99, 160, 161, 162, 163, 164, ... |
| 33 | `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 1026, 1027, 1028, 1062, 1257, 1264, 1387, 1388, ... |
| 32 | `Assets/_Project/Scripts/SubmarineStructuralGrid.cs` | 136, 137, 138, 140, 141, 142, 249, 250, ... |
| 27 | `Assets/_Project/Scripts/HectonNarrativeDirector.cs` | 55, 56, 57, 58, 59, 60, 61, 62, ... |
| 27 | `Assets/_Project/Scripts/Inventory/InventorySoAUtility.cs` | 184, 185, 186, 187, 188, 189, 190, 191, ... |
| 27 | `Assets/_Project/Scripts/World/EcosystemDirector.cs` | 479, 480, 569, 570, 571, 572, 573, 574, ... |
| 27 | `Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs` | 107, 108, 120, 121, 160, 161, 162, 214, ... |
| 26 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs` | 101, 102, 103, 104, 105, 106, 107, 108, ... |
| 26 | `Assets/_Project/Scripts/SubmarineFluidDynamics.cs` | 811, 812, 900, 901, 902, 903, 905, 906, ... |
| 24 | `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs` | 244, 245, 246, 247, 248, 249, 250, 251, ... |
| 24 | `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs` | 78, 79, 80, 81, 82, 83, 84, 130, ... |
| 24 | `Assets/_Project/Scripts/SaveBinaryStorage.cs` | 23, 91, 1273, 1274, 1275, 1276, 1277, 1278, ... |
| 23 | `Assets/_Project/Scripts/Fauna/LeviathanTentacleVerletSolver.cs` | 82, 83, 84, 85, 86, 87, 88, 89, ... |
| 23 | `Assets/_Project/Scripts/Physics/TetherVerletJobs.cs` | 38, 39, 40, 41, 43, 44, 139, 140, ... |
| 22 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs` | 189, 190, 382, 383, 384, 385, 386, 387, ... |
| 22 | `Assets/_Project/Scripts/TetherInstance.cs` | 216, 217, 218, 219, 220, 221, 222, 223, ... |
| 21 | `Assets/_Project/Scripts/Construction/HabitatStressJobs.cs` | 45, 46, 47, 48, 51, 52, 240, 241, ... |
| 21 | `Assets/_Project/Scripts/HectonWorldGenerator.cs` | 295, 297, 298, 299, 300, 301, 396, 397, ... |
| 21 | `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs` | 443, 444, 577, 578, 579, 631, 632, 633, ... |
| 20 | `Assets/_Project/Scripts/EncounterDirector.cs` | 228, 229, 230, 231, 232, 233, 234, 235, ... |
| 19 | `Assets/_Project/Scripts/Audio/PlayerCriticalBufferJobs.cs` | 19, 20, 21, 61, 62, 63, 64, 65, ... |
| 19 | `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` | 342, 343, 344, 345, 346, 347, 368, 370, ... |
| 19 | `Assets/_Project/Scripts/World/FloraInteractionManager.cs` | 136, 137, 138, 139, 144, 216, 223, 224, ... |
| 19 | `Assets/_Project/Scripts/World/HectonAnomalyEngine.cs` | 749, 752, 755, 758, 821, 824, 827, 830, ... |
| 18 | `Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs` | 1851, 1852, 1853, 1872, 1873, 1874, 1893, 1894, ... |
| 17 | `Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs` | 37, 38, 39, 40, 41, 42, 43, 44, ... |

## Allowed Allocator-Internal Sites

| Count | Path | Lines |
|---:|---|---|
| 6 | `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` | 899, 901, 905, 907, 986, 2178 |

## Allowed DataVault/H8Memory Declaration Sites

| Count | Path | Lines |
|---:|---|---|
| 4 | `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` | 769, 770, 772, 773 |
| 2 | `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` | 331, 332 |

## Gate Commands

```powershell
python Tools\DataVaultSovereigntyAudit.py --fail-on-regression
python Tools\DataVaultSovereigntyAudit.py --fail-on-any
```

`--fail-on-regression` blocks any new or increased forbidden constructor or field-declaration count against the baseline.
`--fail-on-any` is the final zero-debt gate and currently fails until all legacy debt is migrated.
