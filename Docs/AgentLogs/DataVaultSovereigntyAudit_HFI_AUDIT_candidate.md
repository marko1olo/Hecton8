# DataVault Sovereignty Audit - VAULT_SOVEREIGNTY_ENFORCER

Schema: `hecton8.datavault_sovereignty_audit.v2`
Status: `FAIL_REGRESSION`
Source root: `Assets/_Project/Scripts`
Pattern: `\bnew\s+NativeArray\s*<`
Baseline: `Docs/AgentLogs/DataVaultSovereigntyBaselineCandidate_HFI_AUDIT.json`

## Summary

| Metric | Count |
|---|---:|
| Total direct `new NativeArray<T>` constructors | 1154 |
| Allowed allocator-internal constructors | 6 |
| Forbidden system constructors | 1148 |
| Files with forbidden constructors | 178 |
| Total field-like `NativeArray<T>` declarations | 5145 |
| Allowed DataVault/H8Memory declarations | 14 |
| Forbidden system declarations | 5131 |
| Files with forbidden declarations | 349 |

## Regression Findings

- Forbidden NativeArray field declarations increased from 5125 to 5131.
- Assets/_Project/Scripts/Construction/ShinobuSocketConstructionData.cs: forbidden NativeArray field declarations increased from 12 to 14.
- Assets/_Project/Scripts/Construction/ShinobuSocketConstructionJobs.cs: forbidden NativeArray field declarations increased from 26 to 27.
- Assets/_Project/Scripts/Construction/SumpPumpPipeGridJobs.cs: forbidden NativeArray field declarations increased from 38 to 40.
- Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs: forbidden NativeArray field declarations increased from 10 to 12.

## Regression Delta By Domain

| Domain | Delta | Direct constructor delta | Field declaration delta | Files |
|---|---:|---:|---:|---:|
| `Construction` | 5 | 0 | 5 | 3 |
| `Core` | 2 | 0 | 2 | 1 |

## Regression Delta Details

| Kind | Domain | Baseline | Current | Delta | Path |
|---|---|---:|---:|---:|---|
| `fieldDeclaration` | `Construction` | 12 | 14 | 2 | `Assets/_Project/Scripts/Construction/ShinobuSocketConstructionData.cs` |
| `fieldDeclaration` | `Construction` | 38 | 40 | 2 | `Assets/_Project/Scripts/Construction/SumpPumpPipeGridJobs.cs` |
| `fieldDeclaration` | `Core` | 10 | 12 | 2 | `Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs` |
| `fieldDeclaration` | `Construction` | 26 | 27 | 1 | `Assets/_Project/Scripts/Construction/ShinobuSocketConstructionJobs.cs` |

## Top 40 Forbidden Files

| Count | Path | Lines |
|---:|---|---|
| 63 | `Assets/_Project/Scripts/PlayerInventory.cs` | 665, 667, 669, 671, 673, 675, 677, 678, ... |
| 53 | `Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs` | 152, 153, 154, 319, 320, 396, 397, 398, ... |
| 40 | `Assets/_Project/Scripts/HectonFluidEngine.cs` | 3567, 3576, 3585, 3594, 3603, 4469, 4471, 4473, ... |
| 40 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 129, 392, 3963, 3964, 3965, 3966, 3967, 5304, ... |
| 33 | `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs` | 3321, 3323, 3325, 3327, 3329, 3331, 3333, 3335, ... |
| 32 | `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 757, 758, 759, 760, 761, 762, 763, 764, ... |
| 32 | `Assets/_Project/Scripts/SaveBinaryStorage.cs` | 587, 1068, 2601, 3700, 3723, 4087, 4200, 4274, ... |
| 26 | `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | 1478, 1481, 1484, 1487, 1489, 1491, 1493, 1495, ... |
| 23 | `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs` | 4530, 4532, 4534, 4536, 4538, 4540, 4542, 4544, ... |
| 18 | `Assets/_Project/Scripts/SaveManager.cs` | 944, 954, 964, 1030, 1037, 1044, 1051, 1061, ... |
| 18 | `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 3495, 3504, 3512, 3520, 3528, 3536, 3544, 3552, ... |
| 17 | `Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs` | 274, 275, 276, 277, 278, 279, 280, 281, ... |
| 17 | `Assets/_Project/Scripts/HectonNarrativeDirector.cs` | 434, 435, 436, 437, 438, 439, 440, 441, ... |
| 16 | `Assets/_Project/Scripts/Editor/HectonArmTextureChannelPacker.cs` | 145, 146, 147, 148, 181, 182, 337, 356, ... |
| 16 | `Assets/_Project/Scripts/World/OfflineWreckageBaker/Editor/WreckageForgeWindow.cs` | 258, 259, 260, 261, 262, 310, 311, 312, ... |
| 15 | `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/Shinobu213/OfflineGeometryBaker.cs` | 319, 367, 377, 379, 443, 535, 537, 592, ... |
| 15 | `Assets/_Project/Scripts/HectonWorldGenerator.cs` | 1042, 1404, 1405, 1406, 1407, 1408, 1409, 1410, ... |
| 15 | `Assets/_Project/Scripts/SubmarineStructuralGrid.cs` | 1901, 1903, 1904, 1906, 1908, 1910, 1912, 1914, ... |
| 15 | `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs` | 1777, 1799, 1802, 1805, 1811, 1814, 1817, 1820, ... |
| 14 | `Assets/_Project/Scripts/VoxelDeltaProcessor.cs` | 1636, 3054, 3059, 3061, 3063, 3065, 3067, 3069, ... |
| 13 | `Assets/_Project/Scripts/EncounterDirector.cs` | 280, 281, 282, 283, 284, 285, 286, 287, ... |
| 12 | `Assets/_Project/Scripts/Editor/ErosionTestHarness.cs` | 82, 83, 84, 85, 86, 91, 265, 266, ... |
| 12 | `Assets/_Project/Scripts/Fabricator.cs` | 1350, 1357, 1364, 1371, 1377, 1383, 1389, 1395, ... |
| 12 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs` | 2212, 2216, 2223, 2227, 2231, 2235, 2251, 2255, ... |
| 12 | `Assets/_Project/Scripts/VFX/Debris/ShinobuVoxelSculptorWindow.cs` | 247, 248, 249, 250, 608, 609, 610, 611, ... |
| 12 | `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs` | 1933, 2760, 2833, 3039, 3516, 3517, 3652, 3653, ... |
| 11 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs` | 1747, 1756, 1765, 1774, 1783, 1792, 1801, 1810, ... |
| 11 | `Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonRuntime.cs` | 474, 475, 476, 477, 478, 479, 480, 481, ... |
| 11 | `Assets/_Project/Scripts/PowerGrid.cs` | 1303, 1306, 1309, 1312, 1315, 1318, 1321, 1331, ... |
| 10 | `Assets/_Project/Scripts/Dev/OmegaAutonomySmokeTester.cs` | 150, 151, 152, 153, 154, 155, 385, 386, ... |
| 10 | `Assets/_Project/Scripts/Editor/LSystemGenomeLabWindow.cs` | 109, 130, 131, 132, 133, 227, 228, 229, ... |
| 10 | `Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs` | 255, 256, 257, 258, 300, 301, 302, 303, ... |
| 10 | `Assets/_Project/Scripts/Plugins/MapMagic/HectonAnomalyMapMagicNode.cs` | 221, 222, 223, 224, 225, 226, 227, 228, ... |
| 10 | `Assets/_Project/Scripts/VoxelDeformationSmokeTester.cs` | 222, 223, 224, 272, 273, 320, 321, 322, ... |
| 9 | `Assets/_Project/Scripts/Editor/AnomalySmokeTester.cs` | 75, 76, 77, 78, 79, 80, 81, 189, ... |
| 9 | `Assets/_Project/Scripts/Editor/GeologyForge/GeologyForgeGenerator.cs` | 54, 107, 166, 190, 202, 218, 436, 461, ... |
| 9 | `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/InteriorClutterForge.cs` | 352, 354, 356, 359, 380, 382, 447, 817, ... |
| 9 | `Assets/_Project/Scripts/Habitat/Deformation/Editor/DamageBake/HabitatDamageBakePipeline.cs` | 625, 878, 987, 988, 989, 990, 991, 1150, ... |
| 9 | `Assets/_Project/Scripts/InventoryGrid.cs` | 96, 97, 98, 99, 100, 101, 102, 103, ... |
| 9 | `Assets/_Project/Scripts/World/AbyssalThermalManager.cs` | 2363, 2374, 2385, 2396, 2406, 2417, 2780, 2790, ... |

## Top 40 Forbidden Declaration Files

| Count | Path | Lines |
|---:|---|---|
| 118 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 43, 44, 462, 465, 466, 467, 468, 469, ... |
| 94 | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` | 211, 212, 213, 214, 215, 216, 217, 218, ... |
| 93 | `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs` | 700, 701, 702, 703, 704, 705, 706, 707, ... |
| 85 | `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | 184, 187, 188, 189, 327, 328, 329, 330, ... |
| 80 | `Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs` | 194, 195, 196, 197, 198, 199, 200, 201, ... |
| 72 | `Assets/_Project/Scripts/HectonFluidEngine.cs` | 1382, 1383, 1384, 1385, 1386, 1387, 1388, 1389, ... |
| 67 | `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 102, 103, 104, 105, 106, 107, 108, 109, ... |
| 67 | `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | 1134, 1135, 1136, 1137, 1138, 1139, 1141, 1591, ... |
| 67 | `Assets/_Project/Scripts/World/VegetationMemoryPool.cs` | 21, 22, 23, 24, 25, 26, 27, 28, ... |
| 66 | `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs` | 573, 4819, 4821, 4822, 4914, 4916, 4917, 4918, ... |
| 66 | `Assets/_Project/Scripts/PlayerInventory.cs` | 176, 177, 178, 179, 180, 213, 214, 215, ... |
| 65 | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs` | 1943, 1944, 1945, 1958, 1959, 1960, 1961, 1974, ... |
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
| 44 | `Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs` | 309, 310, 529, 601, 602, 603, 639, 640, ... |
| 40 | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs` | 3360, 3394, 3418, 3419, 3420, 3421, 3422, 3423, ... |
| 40 | `Assets/_Project/Scripts/Construction/SumpPumpPipeGridJobs.cs` | 16, 17, 18, 19, 20, 21, 22, 137, ... |
| 39 | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs` | 170, 171, 609, 610, 611, 612, 613, 614, ... |
| 39 | `Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorTypes.cs` | 209, 210, 211, 212, 213, 214, 215, 241, ... |
| 39 | `Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralJobs.cs` | 134, 138, 141, 231, 235, 239, 242, 245, ... |
| 36 | `Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityTypes.cs` | 354, 355, 356, 435, 466, 467, 468, 545, ... |
| 36 | `Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs` | 2415, 2416, 2417, 2418, 2419, 2420, 2421, 2422, ... |
| 36 | `Assets/_Project/Scripts/VoxelDeltaProcessor.cs` | 4150, 4151, 4152, 4153, 4154, 4155, 4156, 4157, ... |
| 35 | `Assets/_Project/Scripts/World/BiomeTransitionFogBlendJobs.cs` | 351, 352, 353, 354, 355, 356, 432, 433, ... |
| 35 | `Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageJobs.cs` | 205, 209, 212, 298, 302, 306, 310, 313, ... |
| 35 | `Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs` | 154, 155, 156, 157, 158, 159, 160, 161, ... |
| 34 | `Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs` | 113, 114, 115, 176, 177, 178, 179, 180, ... |
| 33 | `Assets/_Project/Scripts/Physics/Buoyancy/BuoyancySimdVectorization.cs` | 92, 93, 94, 149, 150, 151, 182, 183, ... |
| 33 | `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 1044, 1045, 1046, 1080, 1275, 1282, 1405, 1406, ... |
| 32 | `Assets/_Project/Scripts/SubmarineStructuralGrid.cs` | 137, 138, 139, 141, 142, 143, 250, 251, ... |
| 31 | `Assets/_Project/Scripts/Fauna/MesofaunaBehavioralStateMachine.cs` | 271, 272, 273, 274, 275, 276, 277, 278, ... |
| 31 | `Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs` | 462, 586, 597, 608, 654, 655, 656, 657, ... |

## Allowed Allocator-Internal Sites

| Count | Path | Lines |
|---:|---|---|
| 6 | `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` | 1740, 1742, 1746, 1748, 1827, 3040 |

## Allowed DataVault/H8Memory Declaration Sites

| Count | Path | Lines |
|---:|---|---|
| 10 | `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` | 646, 649, 650, 651, 4506, 4519, 4530, 4560, ... |
| 4 | `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` | 1610, 1611, 1613, 1614 |

## Gate Commands

```powershell
python Tools\DataVaultSovereigntyAudit.py --fail-on-regression
python Tools\DataVaultSovereigntyAudit.py --fail-on-any
```

`--fail-on-regression` blocks any new or increased forbidden constructor or field-declaration count against the baseline.
`--fail-on-any` is the final zero-debt gate and currently fails until all legacy debt is migrated.
