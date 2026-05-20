# DataVault Sovereignty Audit - VAULT_SOVEREIGNTY_ENFORCER

Schema: `hecton8.datavault_sovereignty_audit.v2`
Status: `BLOCKED_BASELINE_MISSING`
Source root: `Assets/_Project/Scripts`
Pattern: `\bnew\s+NativeArray\s*<`
Baseline: `Docs/AgentLogs/DataVaultSovereigntyBaseline_VAULT_SOVEREIGNTY_ENFORCER.json`

## Summary

| Metric | Count |
|---|---:|
| Total direct `new NativeArray<T>` constructors | 1057 |
| Allowed allocator-internal constructors | 6 |
| Forbidden system constructors | 1051 |
| Files with forbidden constructors | 160 |
| Total field-like `NativeArray<T>` declarations | 4704 |
| Allowed DataVault/H8Memory declarations | 6 |
| Forbidden system declarations | 4698 |
| Files with forbidden declarations | 327 |

## Regression Findings

- Baseline missing; no-regression gate fails closed.

## Top 40 Forbidden Files

| Count | Path | Lines |
|---:|---|---|
| 63 | `Assets/_Project/Scripts/PlayerInventory.cs` | 663, 665, 667, 669, 671, 673, 675, 676, ... |
| 53 | `Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs` | 152, 153, 154, 319, 320, 396, 397, 398, ... |
| 40 | `Assets/_Project/Scripts/HectonFluidEngine.cs` | 3433, 3442, 3451, 3460, 3469, 4335, 4337, 4339, ... |
| 40 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 129, 392, 3963, 3964, 3965, 3966, 3967, 5304, ... |
| 33 | `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs` | 3321, 3323, 3325, 3327, 3329, 3331, 3333, 3335, ... |
| 32 | `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 754, 755, 756, 757, 758, 759, 760, 761, ... |
| 32 | `Assets/_Project/Scripts/SaveBinaryStorage.cs` | 587, 1068, 2601, 3700, 3723, 4087, 4200, 4274, ... |
| 27 | `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | 1438, 1441, 1444, 1447, 1449, 1451, 1453, 1455, ... |
| 23 | `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs` | 4549, 4551, 4553, 4555, 4557, 4559, 4561, 4563, ... |
| 18 | `Assets/_Project/Scripts/SaveManager.cs` | 944, 954, 964, 1030, 1037, 1044, 1051, 1061, ... |
| 18 | `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 3495, 3504, 3512, 3520, 3528, 3536, 3544, 3552, ... |
| 17 | `Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs` | 274, 275, 276, 277, 278, 279, 280, 281, ... |
| 17 | `Assets/_Project/Scripts/HectonNarrativeDirector.cs` | 434, 435, 436, 437, 438, 439, 440, 441, ... |
| 15 | `Assets/_Project/Scripts/HectonWorldGenerator.cs` | 1036, 1371, 1372, 1373, 1374, 1375, 1376, 1377, ... |
| 15 | `Assets/_Project/Scripts/SubmarineStructuralGrid.cs` | 1901, 1903, 1904, 1906, 1908, 1910, 1912, 1914, ... |
| 15 | `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs` | 1764, 1786, 1789, 1792, 1798, 1801, 1804, 1807, ... |
| 14 | `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs` | 1335, 1340, 1345, 1350, 1355, 1360, 1365, 1370, ... |
| 14 | `Assets/_Project/Scripts/VoxelDeltaProcessor.cs` | 1636, 3054, 3059, 3061, 3063, 3065, 3067, 3069, ... |
| 13 | `Assets/_Project/Scripts/EncounterDirector.cs` | 280, 281, 282, 283, 284, 285, 286, 287, ... |
| 12 | `Assets/_Project/Scripts/Editor/ErosionTestHarness.cs` | 82, 83, 84, 85, 86, 91, 265, 266, ... |
| 12 | `Assets/_Project/Scripts/Fabricator.cs` | 1350, 1357, 1364, 1371, 1377, 1383, 1389, 1395, ... |
| 12 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs` | 2212, 2216, 2223, 2227, 2231, 2235, 2251, 2255, ... |
| 12 | `Assets/_Project/Scripts/VFX/Debris/ShinobuVoxelSculptorWindow.cs` | 247, 248, 249, 250, 608, 609, 610, 611, ... |
| 12 | `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs` | 1931, 2758, 2831, 3037, 3514, 3515, 3653, 3654, ... |
| 11 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs` | 1738, 1747, 1756, 1765, 1774, 1783, 1792, 1801, ... |
| 11 | `Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonRuntime.cs` | 474, 475, 476, 477, 478, 479, 480, 481, ... |
| 11 | `Assets/_Project/Scripts/PowerGrid.cs` | 1303, 1306, 1309, 1312, 1315, 1318, 1321, 1331, ... |
| 10 | `Assets/_Project/Scripts/Dev/OmegaAutonomySmokeTester.cs` | 150, 151, 152, 153, 154, 155, 385, 386, ... |
| 10 | `Assets/_Project/Scripts/Editor/LSystemGenomeLabWindow.cs` | 109, 130, 131, 132, 133, 227, 228, 229, ... |
| 10 | `Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs` | 255, 256, 257, 258, 300, 301, 302, 303, ... |
| 10 | `Assets/_Project/Scripts/Plugins/MapMagic/HectonAnomalyMapMagicNode.cs` | 221, 222, 223, 224, 225, 226, 227, 228, ... |
| 10 | `Assets/_Project/Scripts/VoxelDeformationSmokeTester.cs` | 222, 223, 224, 272, 273, 320, 321, 322, ... |
| 9 | `Assets/_Project/Scripts/Editor/AnomalySmokeTester.cs` | 75, 76, 77, 78, 79, 80, 81, 189, ... |
| 9 | `Assets/_Project/Scripts/InventoryGrid.cs` | 96, 97, 98, 99, 100, 101, 102, 103, ... |
| 9 | `Assets/_Project/Scripts/World/AbyssalThermalManager.cs` | 2363, 2374, 2385, 2396, 2406, 2417, 2712, 2722, ... |
| 8 | `Assets/_Project/Scripts/World/WorldSpatialHashGrid.cs` | 1623, 1624, 1625, 1725, 1726, 1727, 1728, 1845 |
| 7 | `Assets/_Project/Scripts/Construction/HabitatConstructionManager.cs` | 638, 640, 642, 644, 646, 648, 680 |
| 7 | `Assets/_Project/Scripts/Editor/HydraulicErosionSmokeTester.cs` | 207, 208, 209, 210, 211, 212, 213 |
| 7 | `Assets/_Project/Scripts/Plugins/MapMagic/HectonSpaceEngine098MapMagicNodes.cs` | 108, 109, 256, 257, 258, 412, 413 |
| 7 | `Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs` | 794, 795, 796, 797, 798, 799, 800 |

## Top 40 Forbidden Declaration Files

| Count | Path | Lines |
|---:|---|---|
| 118 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 43, 44, 462, 465, 466, 467, 468, 469, ... |
| 94 | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` | 209, 210, 211, 212, 213, 214, 215, 216, ... |
| 93 | `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs` | 700, 701, 702, 703, 704, 705, 706, 707, ... |
| 86 | `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | 203, 206, 207, 208, 319, 320, 321, 322, ... |
| 72 | `Assets/_Project/Scripts/HectonFluidEngine.cs` | 1250, 1251, 1252, 1253, 1254, 1255, 1256, 1257, ... |
| 68 | `Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs` | 193, 194, 195, 196, 197, 198, 199, 200, ... |
| 67 | `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 102, 103, 104, 105, 106, 107, 108, 109, ... |
| 67 | `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | 1133, 1134, 1135, 1136, 1137, 1138, 1140, 1590, ... |
| 67 | `Assets/_Project/Scripts/World/VegetationMemoryPool.cs` | 21, 22, 23, 24, 25, 26, 27, 28, ... |
| 66 | `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs` | 573, 4810, 4812, 4813, 4901, 4903, 4904, 4905, ... |
| 66 | `Assets/_Project/Scripts/PlayerInventory.cs` | 175, 176, 177, 178, 179, 212, 213, 214, ... |
| 65 | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs` | 1942, 1943, 1944, 1957, 1958, 1959, 1960, 1973, ... |
| 63 | `Assets/_Project/Scripts/Inventory/Routing/InventoryRoutingNetwork.cs` | 171, 172, 173, 174, 175, 176, 177, 178, ... |
| 60 | `Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs` | 837, 838, 868, 869, 870, 871, 873, 1054, ... |
| 59 | `Assets/_Project/Scripts/SaveSystem/VoxelDeltaCompressionArchitecture.cs` | 169, 170, 171, 172, 173, 174, 175, 176, ... |
| 59 | `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs` | 883, 884, 885, 886, 887, 888, 889, 890, ... |
| 57 | `Assets/_Project/Scripts/Cartography/CartographyGridJobs.cs` | 330, 331, 332, 333, 334, 335, 336, 337, ... |
| 52 | `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs` | 640, 642, 644, 646, 648, 650, 652, 654, ... |
| 51 | `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs` | 299, 301, 302, 303, 304, 305, 306, 307, ... |
| 51 | `Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs` | 132, 133, 134, 135, 136, 137, 138, 139, ... |
| 46 | `Assets/_Project/Scripts/SaveSystem/SaveStateMerkleTree.cs` | 192, 193, 194, 195, 196, 197, 198, 199, ... |
| 45 | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs` | 160, 161, 163, 292, 338, 339, 340, 341, ... |
| 44 | `Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs` | 308, 309, 528, 600, 601, 602, 638, 639, ... |
| 40 | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs` | 3343, 3377, 3401, 3402, 3403, 3404, 3405, 3406, ... |
| 39 | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs` | 170, 171, 609, 610, 611, 612, 613, 614, ... |
| 39 | `Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorTypes.cs` | 282, 283, 284, 285, 286, 287, 288, 314, ... |
| 39 | `Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralJobs.cs` | 134, 138, 141, 231, 235, 239, 242, 245, ... |
| 36 | `Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityTypes.cs` | 354, 355, 356, 435, 466, 467, 468, 545, ... |
| 36 | `Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs` | 2418, 2419, 2420, 2421, 2422, 2423, 2424, 2425, ... |
| 36 | `Assets/_Project/Scripts/VoxelDeltaProcessor.cs` | 4151, 4152, 4153, 4154, 4155, 4156, 4157, 4158, ... |
| 35 | `Assets/_Project/Scripts/World/BiomeTransitionFogBlendJobs.cs` | 351, 352, 353, 354, 355, 356, 432, 433, ... |
| 35 | `Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageJobs.cs` | 205, 209, 212, 298, 302, 306, 310, 313, ... |
| 34 | `Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs` | 97, 98, 99, 160, 161, 162, 163, 164, ... |
| 33 | `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 1044, 1045, 1046, 1080, 1275, 1282, 1405, 1406, ... |
| 32 | `Assets/_Project/Scripts/SubmarineStructuralGrid.cs` | 137, 138, 139, 141, 142, 143, 250, 251, ... |
| 31 | `Assets/_Project/Scripts/Fauna/MesofaunaBehavioralStateMachine.cs` | 271, 272, 273, 274, 275, 276, 277, 278, ... |
| 31 | `Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs` | 463, 587, 598, 609, 655, 656, 657, 658, ... |
| 29 | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs` | 1909, 1910, 1911, 1912, 1913, 1914, 1915, 2071, ... |
| 29 | `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsJobs.cs` | 13, 54, 57, 60, 63, 66, 70, 74, ... |
| 28 | `Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonJobs.cs` | 14, 53, 73, 74, 75, 136, 138, 139, ... |

## Allowed Allocator-Internal Sites

| Count | Path | Lines |
|---:|---|---|
| 6 | `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` | 1663, 1665, 1669, 1671, 1750, 2963 |

## Allowed DataVault/H8Memory Declaration Sites

| Count | Path | Lines |
|---:|---|---|
| 4 | `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` | 1533, 1534, 1536, 1537 |
| 2 | `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` | 496, 497 |

## Gate Commands

```powershell
python Tools\DataVaultSovereigntyAudit.py --fail-on-regression
python Tools\DataVaultSovereigntyAudit.py --fail-on-any
```

`--fail-on-regression` blocks any new or increased forbidden constructor or field-declaration count against the baseline.
`--fail-on-any` is the final zero-debt gate and currently fails until all legacy debt is migrated.
