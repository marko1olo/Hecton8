# DataVault Sovereignty Audit - VAULT_SOVEREIGNTY_ENFORCER

Schema: `hecton8.datavault_sovereignty_audit.v3`
Status: `BLOCKED_BASELINE_MISSING`
Source root: `Assets/_Project/Scripts/World`
Pattern: `\bnew\s+NativeArray\s*<`
Baseline: `Docs/AgentLogs/DataVaultSovereigntyBaseline_VAULT_SOVEREIGNTY_ENFORCER.json`

## Summary

| Metric | Count |
|---|---:|
| Total direct `new NativeArray<T>` constructors | 161 |
| Allowed allocator-internal constructors | 136 |
| Forbidden system constructors | 25 |
| Runtime forbidden constructors | 21 |
| Editor/offline forbidden constructors | 4 |
| Editor/offline transient scratch constructors | 136 |
| Files with forbidden constructors | 8 |
| Editor/offline session scratch declarations | 0 |
| Editor/offline persistent preview declarations | 0 |
| Total field-like `NativeArray<T>` declarations | 1081 |
| Allowed DataVault/H8Memory declarations | 1037 |
| Forbidden system declarations | 44 |
| Persistent owner native collection declarations | 0 |
| Job input native collection declarations | 965 |
| Burst job input native collection declarations | 965 |
| Native view/payload/kernel struct declarations | 72 |
| Unknown struct native collection declarations | 44 |
| Files with forbidden declarations | 9 |

## Regression Findings

- Baseline missing; runtime no-regression gate fails closed.

## Current Forbidden Constructors By Execution Surface

| Surface | Count |
|---|---:|
| `Editor` | 4 |
| `Runtime` | 21 |

## Current Forbidden Constructors By Allocator

| Allocator | Count |
|---|---:|
| `Persistent` | 6 |
| `Temp` | 3 |
| `TempJob` | 5 |
| `Unknown` | 11 |

## Editor/Offline Forbidden Constructors By Allocator

| Allocator | Count |
|---|---:|
| `Persistent` | 4 |

## Top 40 Forbidden Files

| Count | Path | Lines |
|---:|---|---|
| 9 | `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs` | 1381, 1382, 2093, 4334, 4338, 4342, 4346, 4350, ... |
| 4 | `Assets/_Project/Scripts/World/BiotaDensityMapBaker/Editor/BiotaDensityBakePipeline.cs` | 202, 314, 447, 559 |
| 4 | `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs` | 5089, 6121, 6181, 7461 |
| 2 | `Assets/_Project/Scripts/World/TOOL_Procedural_Wreckage_Generator.cs` | 72, 74 |
| 2 | `Assets/_Project/Scripts/World/VegetationCapacityUtilities.cs` | 227, 252 |
| 2 | `Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs` | 1003, 1481 |
| 1 | `Assets/_Project/Scripts/World/HectonDistantLandmarkRenderer.cs` | 381 |
| 1 | `Assets/_Project/Scripts/World/HectonHLODRenderer.cs` | 271 |

## Top 40 Forbidden Declaration Files

| Count | Path | Lines |
|---:|---|---|
| 13 | `Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs` | 172, 173, 174, 175, 176, 177, 178, 179, ... |
| 12 | `Assets/_Project/Scripts/World/GlobalWorldSampler.cs` | 230, 231, 232, 233, 234, 235, 236, 237, ... |
| 8 | `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchBakePipeline.cs` | 246, 247, 248, 249, 250, 251, 252, 254 |
| 5 | `Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeVaultRuntime.cs` | 19, 20, 21, 22, 23 |
| 2 | `Assets/_Project/Scripts/World/TOOL_Procedural_Wreckage_Generator.cs` | 63, 65 |
| 1 | `Assets/_Project/Scripts/World/Contracts/ScatterSimulationContracts.cs` | 300 |
| 1 | `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 948 |
| 1 | `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchBakePipeline.cs` | 253 |
| 1 | `Assets/_Project/Scripts/World/TOOL_Procedural_Wreckage_Generator.cs` | 66 |

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
| 4 | `Assets/_Project/Scripts/World/OfflineHadalArchBaker/Editor/HadalArchBakePipeline.cs` | 96, 97, 98, 99 |
| 4 | `Assets/_Project/Scripts/World/PlanetaryCanvasSmokeTester.cs` | 36, 37, 38, 39 |
| 3 | `Assets/_Project/Scripts/World/OfflineHadalArchBaker/Editor/HadalStructureForgeWindow.cs` | 331, 332, 333 |
| 2 | `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchForgeWindow.cs` | 278, 279 |
| 2 | `Assets/_Project/Scripts/World/VoxelTerrainSeamBinder/Editor/VoxelTerrainSeamPreviewGizmo.cs` | 53, 54 |
| 1 | `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchBakePipeline.cs` | 1048 |
| 1 | `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchMockBenchmark.cs` | 29 |
| 1 | `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/TectonicRiftProfileCsvParser.cs` | 54 |
| 1 | `Assets/_Project/Scripts/World/VolumetricBiomeSmokeTester.cs` | 327 |
| 1 | `Assets/_Project/Scripts/World/VoxelTerrainSeamBinder/Editor/VoxelTerrainSeamBinderWindow.cs` | 554 |

## Allowed DataVault/H8Memory Declaration Sites

| Count | Path | Lines |
|---:|---|---|
| 21 | `Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs` | 169, 170, 171, 172, 173, 174, 175, 176, ... |
| 20 | `Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralVault.cs` | 66, 67, 68, 69, 70, 71, 72, 73, ... |
| 18 | `Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageVault.cs` | 62, 63, 64, 65, 66, 67, 68, 69, ... |
| 17 | `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 4979, 4981, 4982, 4983, 4984, 4985, 4986, 4987, ... |
| 14 | `Assets/_Project/Scripts/World/GlobalWorldSampler.cs` | 2326, 2327, 2328, 2329, 2330, 2331, 2332, 2333, ... |
| 14 | `Assets/_Project/Scripts/World/GlobalWorldSampler.cs` | 2412, 2413, 2414, 2415, 2416, 2417, 2418, 2419, ... |
| 14 | `Assets/_Project/Scripts/World/GlobalWorldSampler.cs` | 2535, 2536, 2537, 2538, 2539, 2540, 2541, 2542, ... |
| 14 | `Assets/_Project/Scripts/World/GlobalWorldSampler.cs` | 2637, 2638, 2639, 2640, 2641, 2642, 2643, 2644, ... |
| 14 | `Assets/_Project/Scripts/World/GlobalWorldSampler.cs` | 2722, 2723, 2724, 2725, 2726, 2727, 2728, 2729, ... |
| 14 | `Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs` | 3166, 3167, 3168, 3169, 3170, 3171, 3172, 3173, ... |
| 13 | `Assets/_Project/Scripts/World/BiotaDensityMapBaker/Editor/BiotaDensityBakeJobs.cs` | 274, 275, 276, 277, 278, 279, 280, 281, ... |
| 13 | `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | 3386, 3387, 3388, 3389, 3390, 3391, 3392, 3393, ... |
| 12 | `Assets/_Project/Scripts/World/EcosystemDirector.cs` | 596, 597, 598, 599, 600, 601, 602, 603, ... |
| 12 | `Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs` | 619, 620, 621, 622, 623, 624, 625, 626, ... |
| 11 | `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` | 4195, 4196, 4197, 4198, 4199, 4200, 4201, 4205, ... |
| 11 | `Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralJobs.cs` | 259, 263, 267, 270, 273, 276, 279, 282, ... |
| 11 | `Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs` | 776, 777, 778, 779, 780, 781, 782, 783, ... |
| 11 | `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsJobs.cs` | 57, 60, 63, 66, 69, 73, 77, 80, ... |
| 10 | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs` | 334, 335, 336, 337, 338, 339, 340, 341, ... |
| 10 | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs` | 480, 481, 482, 483, 484, 485, 486, 487, ... |
| 9 | `Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeJobs.cs` | 458, 459, 460, 465, 466, 469, 472, 473, ... |
| 9 | `Assets/_Project/Scripts/World/HectonAnomalyEngine.cs` | 1283, 1284, 1285, 1286, 1287, 1288, 1289, 1293, ... |
| 9 | `Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralJobs.cs` | 914, 918, 922, 926, 930, 934, 937, 940, ... |
| 9 | `Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageJobs.cs` | 314, 318, 322, 326, 330, 333, 336, 340, ... |
| 9 | `Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyJobs.cs` | 59, 60, 61, 62, 63, 64, 65, 66, ... |
| 8 | `Assets/_Project/Scripts/World/EcosystemDirector.cs` | 718, 719, 720, 721, 722, 723, 724, 725 |
| 8 | `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs` | 663, 664, 665, 666, 667, 668, 669, 670 |
| 8 | `Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageJobs.cs` | 883, 887, 891, 895, 899, 903, 907, 910 |
| 8 | `Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs` | 865, 866, 867, 868, 869, 870, 871, 872 |
| 8 | `Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs` | 1363, 1364, 1365, 1366, 1367, 1368, 1369, 1370 |
| 8 | `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | 2481, 2482, 2483, 2484, 2485, 2487, 2488, 2489 |
| 8 | `Assets/_Project/Scripts/World/VegetationMemoryPool.cs` | 65, 66, 67, 68, 69, 70, 71, 72 |
| 7 | `Assets/_Project/Scripts/World/BiomeWeightMapBaker/Editor/BiomeWeightMapBakeJobs.cs` | 231, 232, 233, 234, 235, 236, 237 |
| 7 | `Assets/_Project/Scripts/World/HectonAnomalyEngine.cs` | 886, 890, 894, 898, 902, 906, 910 |
| 7 | `Assets/_Project/Scripts/World/Outposts/MarauderOutpostJobs.cs` | 304, 305, 306, 307, 308, 309, 310 |
| 7 | `Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs` | 2084, 2085, 2086, 2087, 2088, 2089, 2091 |
| 7 | `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | 1782, 1783, 1784, 1785, 1786, 1787, 1789 |
| 6 | `Assets/_Project/Scripts/World/BiomeTransitionFogBlendJobs.cs` | 351, 352, 353, 354, 355, 356 |
| 6 | `Assets/_Project/Scripts/World/BiomeTransitionFogBlendJobs.cs` | 536, 537, 538, 539, 540, 542 |
| 6 | `Assets/_Project/Scripts/World/BiomeWeightMapBaker/Editor/BiomeWeightMapBakeJobs.cs` | 161, 162, 163, 164, 165, 166 |

## Gate Commands

```powershell
python Tools\DataVaultSovereigntyAudit.py --fail-on-regression
python Tools\DataVaultSovereigntyAudit.py --fail-on-any
```

`--fail-on-regression` blocks any new or increased forbidden constructor or field-declaration count against the baseline.
`--fail-on-any` is the final zero-debt gate and currently fails until all legacy debt is migrated.
