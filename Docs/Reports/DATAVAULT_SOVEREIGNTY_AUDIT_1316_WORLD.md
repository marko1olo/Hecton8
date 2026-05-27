# DataVault Sovereignty Audit - VAULT_SOVEREIGNTY_ENFORCER

Schema: `hecton8.datavault_sovereignty_audit.v3`
Status: `BLOCKED_BASELINE_MISSING`
Source root: `Assets/_Project/Scripts/World`
Pattern: `\bnew\s+NativeArray\s*<`
Baseline: `Docs/AgentLogs/DataVaultSovereigntyBaseline_VAULT_SOVEREIGNTY_ENFORCER.json`

## Summary

| Metric | Count |
|---|---:|
| Total direct `new NativeArray<T>` constructors | 242 |
| Allowed allocator-internal constructors | 132 |
| Forbidden system constructors | 110 |
| Runtime forbidden constructors | 99 |
| Editor/offline forbidden constructors | 11 |
| Editor/offline transient scratch constructors | 132 |
| Files with forbidden constructors | 23 |
| Editor/offline session scratch declarations | 22 |
| Editor/offline persistent preview declarations | 4 |
| Total field-like `NativeArray<T>` declarations | 1218 |
| Allowed DataVault/H8Memory declarations | 1043 |
| Forbidden system declarations | 175 |
| Persistent owner native collection declarations | 73 |
| Job input native collection declarations | 942 |
| Burst job input native collection declarations | 942 |
| Native view/payload/kernel struct declarations | 77 |
| Unknown struct native collection declarations | 100 |
| Files with forbidden declarations | 35 |

## Current Forbidden Constructors By Execution Surface

| Surface | Count |
|---|---:|
| `Editor` | 11 |
| `Runtime` | 99 |

## Current Forbidden Constructors By Allocator

| Allocator | Count |
|---|---:|
| `Persistent` | 59 |
| `Temp` | 2 |
| `TempJob` | 11 |
| `Unknown` | 38 |

## Editor/Offline Forbidden Constructors By Allocator

| Allocator | Count |
|---|---:|
| `Persistent` | 11 |

## Top 80 Forbidden Files

| Count | Path | Lines |
|---:|---|---|
| 18 | `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 3772, 3781, 3789, 3797, 3805, 3813, 3821, 3829, ... |
| 15 | `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs` | 1968, 1993, 1996, 1999, 2005, 2008, 2011, 2014, ... |
| 10 | `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs` | 2053, 2881, 3766, 3767, 3902, 3903, 3904, 3905, ... |
| 9 | `Assets/_Project/Scripts/World/AbyssalThermalManager.cs` | 2692, 2703, 2714, 2725, 2735, 2746, 3117, 3127, ... |
| 8 | `Assets/_Project/Scripts/World/WorldSpatialHashGrid.cs` | 1729, 1730, 1731, 1831, 1832, 1833, 1834, 1951 |
| 7 | `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | 410, 419, 426, 442, 454, 462, 471 |
| 6 | `Assets/_Project/Scripts/World/VegetationTerrainHoleSynchronizer.cs` | 418, 430, 920, 1046, 1054, 1062 |
| 5 | `Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs` | 902, 1368, 2176, 2188, 2200 |
| 4 | `Assets/_Project/Scripts/World/BiotaDensityMapBaker/Editor/BiotaDensityBakePipeline.cs` | 202, 314, 447, 559 |
| 4 | `Assets/_Project/Scripts/World/OfflineHadalArchBaker/Editor/HadalArchBakePipeline.cs` | 557, 558, 559, 560 |
| 4 | `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs` | 3789, 4647, 4691, 5951 |
| 3 | `Assets/_Project/Scripts/World/ScatterEvaluator.cs` | 86, 92, 98 |
| 3 | `Assets/_Project/Scripts/World/WreckMaterialRegistry.cs` | 222, 251, 668 |
| 2 | `Assets/_Project/Scripts/World/OfflineHadalArchBaker/Editor/HadalStructureForgeWindow.cs` | 332, 333 |
| 2 | `Assets/_Project/Scripts/World/ScatterBackendBindingState.cs` | 92, 111 |
| 2 | `Assets/_Project/Scripts/World/TOOL_Procedural_Wreckage_Generator.cs` | 73, 75 |
| 2 | `Assets/_Project/Scripts/World/VegetationCapacityUtilities.cs` | 227, 252 |
| 1 | `Assets/_Project/Scripts/World/FloraRegrowthDirector.cs` | 943 |
| 1 | `Assets/_Project/Scripts/World/HectonDistantLandmarkRenderer.cs` | 381 |
| 1 | `Assets/_Project/Scripts/World/HectonHLODRenderer.cs` | 271 |
| 1 | `Assets/_Project/Scripts/World/OfflineWreckageBaker/Editor/OfflineWreckageBlackBox.cs` | 128 |
| 1 | `Assets/_Project/Scripts/World/ProxyLightRegistry.cs` | 168 |
| 1 | `Assets/_Project/Scripts/World/VegetationPredatorFearField.cs` | 127 |

## Top 80 Forbidden Declaration Files

| Count | Path | Lines |
|---:|---|---|
| 47 | `Assets/_Project/Scripts/World/VegetationMemoryPool.cs` | 99, 100, 101, 102, 103, 104, 105, 106, ... |
| 13 | `Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs` | 172, 173, 174, 175, 176, 177, 178, 179, ... |
| 12 | `Assets/_Project/Scripts/World/GlobalWorldSampler.cs` | 189, 190, 191, 192, 193, 194, 195, 196, ... |
| 9 | `Assets/_Project/Scripts/World/AbyssalThermalManager.cs` | 664, 665, 671, 672, 673, 674, 675, 676, ... |
| 8 | `Assets/_Project/Scripts/World/VegetationMemoryPool.cs` | 24, 25, 26, 27, 28, 29, 30, 31 |
| 8 | `Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs` | 631, 632, 633, 634, 635, 636, 637, 638 |
| 8 | `Assets/_Project/Scripts/World/WorldSpatialHashGrid.cs` | 248, 249, 250, 254, 255, 256, 257, 262 |
| 7 | `Assets/_Project/Scripts/World/FloraRegrowthDirector.cs` | 309, 310, 311, 313, 316, 319, 320 |
| 7 | `Assets/_Project/Scripts/World/VegetationMemoryPool.cs` | 64, 65, 66, 67, 68, 69, 70 |
| 5 | `Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeVaultRuntime.cs` | 20, 21, 22, 23, 24 |
| 5 | `Assets/_Project/Scripts/World/HectonSpatialHash.cs` | 255, 266, 267, 268, 269 |
| 4 | `Assets/_Project/Scripts/World/FloraRegrowthDirector.cs` | 312, 314, 315, 317 |
| 4 | `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs` | 385, 435, 436, 485 |
| 4 | `Assets/_Project/Scripts/World/WreckMaterialRegistry.cs` | 174, 175, 176, 177 |
| 3 | `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 1477, 1478, 1479 |
| 3 | `Assets/_Project/Scripts/World/ScatterEvaluator.cs` | 51, 52, 53 |
| 3 | `Assets/_Project/Scripts/World/VegetationMemoryPool.cs` | 139, 141, 142 |
| 2 | `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs` | 487, 488 |
| 2 | `Assets/_Project/Scripts/World/HectonSpatialHash.cs` | 254, 258 |
| 2 | `Assets/_Project/Scripts/World/OfflineHadalArchBaker/Editor/HadalStructureForgeWindow.cs` | 315, 316 |
| 2 | `Assets/_Project/Scripts/World/ProxyLightRegistry.cs` | 139, 140 |
| 2 | `Assets/_Project/Scripts/World/ScatterBackendBindingState.cs` | 18, 19 |
| 2 | `Assets/_Project/Scripts/World/TOOL_Procedural_Wreckage_Generator.cs` | 64, 66 |
| 2 | `Assets/_Project/Scripts/World/WreckMaterialRegistry.cs` | 155, 178 |
| 1 | `Assets/_Project/Scripts/World/Contracts/ScatterSimulationContracts.cs` | 300 |
| 1 | `Assets/_Project/Scripts/World/FaunaSpatialHashRegistry.cs` | 57 |
| 1 | `Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeVaultRuntime.cs` | 127 |
| 1 | `Assets/_Project/Scripts/World/FloraRegrowthDirector.cs` | 318 |
| 1 | `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 1095 |
| 1 | `Assets/_Project/Scripts/World/HectonSpatialHash.cs` | 129 |
| 1 | `Assets/_Project/Scripts/World/OfflineWreckageBaker/Editor/OfflineWreckageBlackBox.cs` | 18 |
| 1 | `Assets/_Project/Scripts/World/ProxyLightRegistry.cs` | 141 |
| 1 | `Assets/_Project/Scripts/World/TOOL_Procedural_Wreckage_Generator.cs` | 67 |
| 1 | `Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs` | 99 |
| 1 | `Assets/_Project/Scripts/World/WorldSpatialHashGrid.cs` | 247 |

## Allowed Allocator-Internal Sites

| Count | Path | Lines |
|---:|---|---|
| 21 | `Assets/_Project/Scripts/World/BiomeWeightMapBaker/Editor/BiomeWeightMapBakePipeline.cs` | 176, 177, 178, 179, 180, 181, 183, 184, ... |
| 17 | `Assets/_Project/Scripts/World/OfflineWreckageBaker/Editor/WreckageForgeWindow.cs` | 259, 260, 261, 262, 263, 311, 312, 313, ... |
| 11 | `Assets/_Project/Scripts/World/StaticCaveSdfBaker/Editor/StaticCaveSdfBakePipeline.cs` | 107, 175, 176, 177, 178, 197, 198, 226, ... |
| 11 | `Assets/_Project/Scripts/World/VoxelTerrainSeamBinder/Editor/VoxelTerrainSeamBinderPipeline.cs` | 86, 127, 128, 129, 130, 335, 336, 339, ... |
| 8 | `Assets/_Project/Scripts/World/OfflineWreckageBaker/Editor/OfflineWreckageMockBenchmark.cs` | 45, 46, 47, 48, 49, 50, 51, 52 |
| 6 | `Assets/_Project/Scripts/World/BiomeTransitionSmokeTester.cs` | 94, 95, 99, 100, 101, 102 |
| 4 | `Assets/_Project/Scripts/World/HectonSandboxAbyssalShelfSmokeTester.cs` | 106, 107, 108, 109 |
| 4 | `Assets/_Project/Scripts/World/PlanetaryCanvasSmokeTester.cs` | 36, 37, 38, 39 |
| 2 | `Assets/_Project/Scripts/World/VoxelTerrainSeamBinder/Editor/VoxelTerrainSeamPreviewGizmo.cs` | 53, 54 |
| 1 | `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchBakePipeline.cs` | 1064 |
| 1 | `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchMockBenchmark.cs` | 29 |
| 1 | `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/TectonicRiftProfileCsvParser.cs` | 54 |
| 1 | `Assets/_Project/Scripts/World/VolumetricBiomeSmokeTester.cs` | 327 |
| 1 | `Assets/_Project/Scripts/World/VoxelTerrainSeamBinder/Editor/VoxelTerrainSeamBinderWindow.cs` | 554 |

## Allowed DataVault/H8Memory Declaration Sites

| Count | Path | Lines |
|---:|---|---|
| 21 | `Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs` | 168, 169, 170, 171, 172, 173, 174, 175, ... |
| 20 | `Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralVault.cs` | 66, 67, 68, 69, 70, 71, 72, 73, ... |
| 18 | `Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageVault.cs` | 62, 63, 64, 65, 66, 67, 68, 69, ... |
| 17 | `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 4897, 4899, 4900, 4901, 4902, 4903, 4904, 4905, ... |
| 14 | `Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs` | 3143, 3144, 3145, 3146, 3147, 3148, 3149, 3150, ... |
| 13 | `Assets/_Project/Scripts/World/BiotaDensityMapBaker/Editor/BiotaDensityBakeJobs.cs` | 274, 275, 276, 277, 278, 279, 280, 281, ... |
| 13 | `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | 2661, 2662, 2663, 2664, 2665, 2666, 2667, 2668, ... |
| 12 | `Assets/_Project/Scripts/World/EcosystemDirector.cs` | 596, 597, 598, 599, 600, 601, 602, 603, ... |
| 12 | `Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs` | 607, 608, 609, 610, 611, 612, 613, 614, ... |
| 11 | `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` | 2998, 2999, 3000, 3001, 3002, 3003, 3004, 3008, ... |
| 11 | `Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralJobs.cs` | 259, 263, 267, 270, 273, 276, 279, 282, ... |
| 11 | `Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs` | 764, 765, 766, 767, 768, 769, 770, 771, ... |
| 11 | `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsJobs.cs` | 57, 60, 63, 66, 69, 73, 77, 80, ... |
| 10 | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs` | 334, 335, 336, 337, 338, 339, 340, 341, ... |
| 10 | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs` | 480, 481, 482, 483, 484, 485, 486, 487, ... |
| 9 | `Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeJobs.cs` | 458, 459, 460, 465, 466, 469, 472, 473, ... |
| 9 | `Assets/_Project/Scripts/World/HectonAnomalyEngine.cs` | 1283, 1284, 1285, 1286, 1287, 1288, 1289, 1293, ... |
| 9 | `Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralJobs.cs` | 914, 918, 922, 926, 930, 934, 937, 940, ... |
| 9 | `Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageJobs.cs` | 314, 318, 322, 326, 330, 333, 336, 340, ... |
| 9 | `Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyJobs.cs` | 59, 60, 61, 62, 63, 64, 65, 66, ... |
| 8 | `Assets/_Project/Scripts/World/EcosystemDirector.cs` | 718, 719, 720, 721, 722, 723, 724, 725 |
| 8 | `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs` | 677, 678, 679, 680, 681, 682, 683, 684 |
| 8 | `Assets/_Project/Scripts/World/OfflineHadalArchBaker/Editor/HadalArchBakePipeline.cs` | 507, 508, 509, 510, 511, 512, 513, 514 |
| 8 | `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchBakePipeline.cs` | 291, 292, 293, 294, 295, 296, 297, 299 |
| 8 | `Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageJobs.cs` | 847, 851, 855, 859, 863, 867, 871, 874 |
| 8 | `Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs` | 853, 854, 855, 856, 857, 858, 859, 860 |
| 8 | `Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs` | 1363, 1364, 1365, 1366, 1367, 1368, 1369, 1370 |
| 8 | `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | 1819, 1820, 1821, 1822, 1823, 1825, 1826, 1827 |
| 7 | `Assets/_Project/Scripts/World/BiomeWeightMapBaker/Editor/BiomeWeightMapBakeJobs.cs` | 231, 232, 233, 234, 235, 236, 237 |
| 7 | `Assets/_Project/Scripts/World/HectonAnomalyEngine.cs` | 886, 890, 894, 898, 902, 906, 910 |
| 7 | `Assets/_Project/Scripts/World/Outposts/MarauderOutpostJobs.cs` | 304, 305, 306, 307, 308, 309, 310 |
| 7 | `Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs` | 2084, 2085, 2086, 2087, 2088, 2089, 2091 |
| 7 | `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | 1173, 1174, 1175, 1176, 1177, 1178, 1180 |
| 7 | `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | 2179, 2180, 2181, 2182, 2184, 2185, 2186 |
| 6 | `Assets/_Project/Scripts/World/BiomeTransitionFogBlendJobs.cs` | 351, 352, 353, 354, 355, 356 |
| 6 | `Assets/_Project/Scripts/World/BiomeTransitionFogBlendJobs.cs` | 536, 537, 538, 539, 540, 542 |
| 6 | `Assets/_Project/Scripts/World/BiomeWeightMapBaker/Editor/BiomeWeightMapBakeJobs.cs` | 161, 162, 163, 164, 165, 166 |
| 6 | `Assets/_Project/Scripts/World/EntropyYieldJob.cs` | 140, 141, 142, 143, 144, 145 |
| 6 | `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs` | 588, 589, 590, 591, 592, 593 |
| 6 | `Assets/_Project/Scripts/World/OfflineWreckageBaker/Editor/OfflineWreckageBakeJobs.cs` | 25, 29, 33, 37, 41, 47 |
| 6 | `Assets/_Project/Scripts/World/OfflineWreckageBaker/Editor/OfflineWreckageBakeJobs.cs` | 501, 505, 509, 513, 517, 521 |
| 6 | `Assets/_Project/Scripts/World/TerrainChunkPagerTypes.cs` | 420, 421, 423, 424, 425, 426 |
| 6 | `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | 2028, 2029, 2030, 2031, 2033, 2035 |
| 6 | `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | 3307, 3308, 3309, 3310, 3311, 3314 |
| 6 | `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsJobs.cs` | 897, 901, 905, 908, 911, 914 |
| 5 | `Assets/_Project/Scripts/World/BiomeTransitionFogBlendJobs.cs` | 807, 808, 809, 810, 811 |
| 5 | `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` | 2948, 2949, 2950, 2951, 2952 |
| 5 | `Assets/_Project/Scripts/World/EcosystemDirector.cs` | 799, 800, 801, 802, 803 |
| 5 | `Assets/_Project/Scripts/World/ErosionHarnessJobs.cs` | 170, 173, 176, 179, 183 |
| 5 | `Assets/_Project/Scripts/World/FloraInteractionManager.cs` | 190, 191, 192, 193, 198 |
| 5 | `Assets/_Project/Scripts/World/HybridTerrainSeamJobs.cs` | 86, 87, 88, 90, 91 |
| 5 | `Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralJobs.cs` | 784, 788, 792, 796, 799 |
| 5 | `Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs` | 966, 967, 968, 969, 971 |
| 5 | `Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs` | 2365, 2366, 2367, 2368, 2369 |
| 5 | `Assets/_Project/Scripts/World/SpaceEngine098/SpaceEngine098TerrainKernels.cs` | 663, 664, 665, 666, 667 |
| 5 | `Assets/_Project/Scripts/World/StaticCaveSdfBaker/Editor/StaticCaveSdfBakeJobs.cs` | 300, 303, 306, 309, 312 |
| 5 | `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | 1629, 1630, 1631, 1632, 1633 |
| 5 | `Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs` | 544, 545, 546, 547, 548 |
| 5 | `Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs` | 729, 730, 731, 732, 733 |
| 5 | `Assets/_Project/Scripts/World/VoxelTerrainSeamBinder/Editor/VoxelTerrainSeamBinderJobs.cs` | 37, 41, 45, 49, 55 |
| 5 | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs` | 181, 182, 183, 184, 185 |
| 4 | `Assets/_Project/Scripts/World/AbyssalThermalManager.cs` | 4795, 4796, 4797, 4799 |
| 4 | `Assets/_Project/Scripts/World/BiomeTransitionFogBlendJobs.cs` | 991, 992, 993, 994 |
| 4 | `Assets/_Project/Scripts/World/BiomeTransitionFogBlendJobs.cs` | 1019, 1020, 1021, 1022 |
| 4 | `Assets/_Project/Scripts/World/BiomeTransitionFogBlendJobs.cs` | 1059, 1060, 1061, 1062 |
| 4 | `Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeJobs.cs` | 278, 281, 282, 283 |
| 4 | `Assets/_Project/Scripts/World/HectonAnomalyEngine.cs` | 811, 815, 819, 823 |
| 4 | `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 1485, 1486, 1487, 1488 |
| 4 | `Assets/_Project/Scripts/World/HydraulicErosionJob.cs` | 296, 312, 328, 334 |
| 4 | `Assets/_Project/Scripts/World/HydraulicErosionJob.cs` | 1318, 1319, 1320, 1321 |
| 4 | `Assets/_Project/Scripts/World/HydraulicErosionMetricsJob.cs` | 85, 88, 91, 94 |
| 4 | `Assets/_Project/Scripts/World/OfflineHadalArchBaker/Editor/HadalArchBakePipeline.cs` | 504, 505, 506, 516 |
| 4 | `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchBakeJobs.cs` | 107, 108, 109, 110 |
| 4 | `Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralJobs.cs` | 1095, 1099, 1103, 1106 |
| 4 | `Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralJobs.cs` | 1151, 1155, 1159, 1162 |
| 4 | `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs` | 651, 652, 653, 654 |
| 4 | `Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageJobs.cs` | 736, 740, 744, 747 |
| 4 | `Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs` | 888, 889, 890, 891 |
| 4 | `Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs` | 1153, 1154, 1155, 1158 |
| 4 | `Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs` | 2255, 2256, 2257, 2260 |

## Gate Commands

```powershell
python Tools\DataVaultSovereigntyAudit.py --fail-on-regression
python Tools\DataVaultSovereigntyAudit.py --fail-on-any
```

`--fail-on-regression` blocks any new or increased forbidden constructor or field-declaration count against the baseline.
`--fail-on-any` is the final zero-debt gate and currently fails until all legacy debt is migrated.
