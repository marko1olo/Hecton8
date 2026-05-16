# DataVault Sovereignty Audit - VAULT_SOVEREIGNTY_ENFORCER

Schema: `hecton8.datavault_sovereignty_audit.v1`
Status: `PASS_NO_REGRESSION_WITH_LEGACY_DEBT`
Source root: `Assets/_Project/Scripts`
Pattern: `\bnew\s+NativeArray\s*<`
Baseline: `Docs/AgentLogs/DataVaultSovereigntyBaseline_VAULT_SOVEREIGNTY_ENFORCER.json`

## Summary

| Metric | Count |
|---|---:|
| Total direct `new NativeArray<T>` constructors | 1256 |
| Allowed allocator-internal constructors | 6 |
| Forbidden system constructors | 1250 |
| Files with forbidden constructors | 192 |

## Top 40 Forbidden Files

| Count | Path | Lines |
|---:|---|---|
| 63 | `Assets/_Project/Scripts/PlayerInventory.cs` | 651, 653, 655, 657, 659, 661, 663, 664, ... |
| 53 | `Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs` | 152, 153, 154, 319, 320, 396, 397, 398, ... |
| 49 | `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs` | 5386, 5387, 5388, 5389, 5390, 5391, 5392, 5393, ... |
| 44 | `Assets/_Project/Scripts/HectonFluidEngine.cs` | 3727, 3736, 3745, 3754, 3763, 3772, 3781, 3790, ... |
| 40 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 129, 392, 3963, 3964, 3965, 3966, 3967, 5304, ... |
| 33 | `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs` | 3315, 3317, 3319, 3321, 3323, 3325, 3327, 3329, ... |
| 32 | `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 751, 752, 753, 754, 755, 756, 757, 758, ... |
| 32 | `Assets/_Project/Scripts/SaveBinaryStorage.cs` | 586, 1067, 2337, 3400, 3423, 3787, 3900, 3974, ... |
| 27 | `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | 1436, 1439, 1442, 1445, 1447, 1449, 1451, 1453, ... |
| 23 | `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs` | 4548, 4550, 4552, 4554, 4556, 4558, 4560, 4562, ... |
| 18 | `Assets/_Project/Scripts/SaveManager.cs` | 944, 954, 964, 1030, 1037, 1044, 1051, 1061, ... |
| 18 | `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 3477, 3486, 3494, 3502, 3510, 3518, 3526, 3534, ... |
| 17 | `Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs` | 274, 275, 276, 277, 278, 279, 280, 281, ... |
| 17 | `Assets/_Project/Scripts/HectonNarrativeDirector.cs` | 434, 435, 436, 437, 438, 439, 440, 441, ... |
| 16 | `Assets/_Project/Scripts/VoxelDeltaProcessor.cs` | 654, 1613, 2950, 2955, 2957, 2959, 2961, 2963, ... |
| 15 | `Assets/_Project/Scripts/HectonWorldGenerator.cs` | 1036, 1371, 1372, 1373, 1374, 1375, 1376, 1377, ... |
| 15 | `Assets/_Project/Scripts/SubmarineStructuralGrid.cs` | 2041, 2043, 2044, 2046, 2048, 2050, 2052, 2054, ... |
| 15 | `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs` | 1764, 1786, 1789, 1792, 1798, 1801, 1804, 1807, ... |
| 14 | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs` | 1130, 1131, 1132, 1133, 1134, 1135, 1136, 1137, ... |
| 14 | `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs` | 1335, 1340, 1345, 1350, 1355, 1360, 1365, 1370, ... |
| 13 | `Assets/_Project/Scripts/EncounterDirector.cs` | 280, 281, 282, 283, 284, 285, 286, 287, ... |
| 12 | `Assets/_Project/Scripts/Editor/ErosionTestHarness.cs` | 82, 83, 84, 85, 86, 91, 265, 266, ... |
| 12 | `Assets/_Project/Scripts/Fabricator.cs` | 1300, 1307, 1314, 1321, 1341, 1347, 1353, 1359, ... |
| 12 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs` | 2212, 2216, 2223, 2227, 2231, 2235, 2251, 2255, ... |
| 12 | `Assets/_Project/Scripts/SpatialAudioManager.cs` | 5904, 5917, 6005, 6015, 6025, 6037, 6043, 6049, ... |
| 11 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs` | 1737, 1746, 1755, 1764, 1773, 1782, 1791, 1800, ... |
| 11 | `Assets/_Project/Scripts/PowerGrid.cs` | 1293, 1296, 1299, 1302, 1305, 1308, 1311, 1321, ... |
| 11 | `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs` | 1372, 2047, 2120, 2524, 2525, 2755, 2769, 2794, ... |
| 10 | `Assets/_Project/Scripts/Dev/OmegaAutonomySmokeTester.cs` | 150, 151, 152, 153, 154, 155, 385, 386, ... |
| 10 | `Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs` | 873, 882, 891, 900, 909, 918, 927, 936, ... |
| 10 | `Assets/_Project/Scripts/Plugins/MapMagic/HectonAnomalyMapMagicNode.cs` | 221, 222, 223, 224, 225, 226, 227, 228, ... |
| 10 | `Assets/_Project/Scripts/VoxelDeformationSmokeTester.cs` | 222, 223, 224, 272, 273, 320, 321, 322, ... |
| 9 | `Assets/_Project/Scripts/Editor/AnomalySmokeTester.cs` | 75, 76, 77, 78, 79, 80, 81, 189, ... |
| 9 | `Assets/_Project/Scripts/InventoryGrid.cs` | 96, 97, 98, 99, 100, 101, 102, 103, ... |
| 9 | `Assets/_Project/Scripts/ModularEquipmentEngine.cs` | 133, 144, 155, 179, 204, 216, 227, 249, ... |
| 9 | `Assets/_Project/Scripts/World/AbyssalThermalManager.cs` | 2395, 2406, 2417, 2428, 2438, 2449, 2744, 2754, ... |
| 8 | `Assets/_Project/Scripts/Gameplay/Mining/DeployableSdfDrillRuntime.cs` | 569, 570, 571, 572, 573, 574, 575, 576 |
| 8 | `Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs` | 2253, 2270, 2274, 2278, 2290, 2291, 2298, 2299 |
| 8 | `Assets/_Project/Scripts/HectonSurvivalSystem.cs` | 2200, 2465, 2466, 2467, 2468, 2469, 3181, 3256 |
| 8 | `Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs` | 268, 269, 270, 271, 272, 273, 274, 275 |

## Allowed Allocator-Internal Sites

| Count | Path | Lines |
|---:|---|---|
| 6 | `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` | 718, 720, 724, 726, 801, 1820 |

## Gate Commands

```powershell
python Tools\DataVaultSovereigntyAudit.py --fail-on-regression
python Tools\DataVaultSovereigntyAudit.py --fail-on-any
```

`--fail-on-regression` blocks any new or increased forbidden constructor count against the baseline.
`--fail-on-any` is the final zero-debt gate and currently fails until all legacy debt is migrated.
