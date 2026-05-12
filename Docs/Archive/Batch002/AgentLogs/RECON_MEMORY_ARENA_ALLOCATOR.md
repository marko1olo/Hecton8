# RECON_MEMORY_ARENA_ALLOCATOR

Status: PENDING VERIFICATION

Command:
`Select-String -Path Assets/_Project/Scripts/*.cs,Assets/_Project/Scripts/**/*.cs,Assets/_Project/Tests/*.cs,Assets/_Project/Tests/**/*.cs -Pattern 'Allocator\.(TempJob|Temp)\b' -AllMatches`

Totals:
- `Allocator.Temp`: 80 hits.
- `Allocator.TempJob`: 203 hits.
- Total: 283 hits across 44 files.

Migration rule:
- Hot frame scratch arrays that do not outlive the dispatcher frame boundary are Arena candidates.
- Unity-owned memory contracts, direct draw output buffers, editor smoke harnesses, save/load blocking IO buffers, and queues/lists requiring `Dispose()` need owner-specific migration proof before replacement.

Offenders:
- `Assets/_Project/Scripts/AutomationOmegaSmokeTester.cs` (6): 51, 52, 53, 54, 55, 56
- `Assets/_Project/Scripts/AutomationSmokeTester.cs` (6): 39, 40, 41, 42, 43, 44
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` (2): 4192, 4551
- `Assets/_Project/Scripts/CaveGraphGenerator.cs` (6): 148, 155, 159, 164, 237, 549
- `Assets/_Project/Scripts/Core/NativeMemorySentinel.cs` (2): 1034, 1036
- `Assets/_Project/Scripts/CraftingRuntimeSmokeTester.cs` (1): 55
- `Assets/_Project/Scripts/Dev/HabitatStressSmokeTester.cs` (1): 504
- `Assets/_Project/Scripts/Dev/OmegaAutonomySmokeTester.cs` (10): 150, 151, 152, 153, 154, 155, 385, 386, 387, 388
- `Assets/_Project/Scripts/Dev/SpaceEngine098TerrainSmokeTester.cs` (6): 135, 136, 137, 138, 139, 140
- `Assets/_Project/Scripts/Editor/AnomalySmokeTester.cs` (9): 75, 76, 77, 78, 79, 80, 81, 189, 190
- `Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs` (59): 152, 153, 154, 319, 320, 396, 397, 398, 399, 400, 401, 402, 466, 467, 468, 469, 470, 471, 472, 533, 534, 535, 536, 537, 538, 539, 541, 543, 545, 654, 655, 656, 657, 658, 659, 660, 662, 664, 666, 809, 810, 811, 812, 813, 814, 815, 817, 819, 821, 900, 901, 970, 971, 972, 1037, 1038, 1090, 1091, 1092
- `Assets/_Project/Scripts/Editor/ErosionTestHarness.cs` (13): 82, 83, 84, 85, 86, 90, 91, 265, 266, 447, 480, 520, 521
- `Assets/_Project/Scripts/Editor/HectonSpatialHashEditorSelfTests.cs` (3): 20, 80, 110
- `Assets/_Project/Scripts/Editor/HydraulicErosionSmokeTester.cs` (7): 207, 208, 209, 210, 211, 212, 213
- `Assets/_Project/Scripts/Editor/PlanetaryCanvasSmokeTester.cs` (1): 56
- `Assets/_Project/Scripts/Editor/WorldProceduralProxySceneBuilder.cs` (2): 273, 274
- `Assets/_Project/Scripts/FaunaRuntimeSmokeTester.cs` (5): 183, 184, 185, 257, 258
- `Assets/_Project/Scripts/FlowFieldVisualizer.cs` (3): 939, 940, 941
- `Assets/_Project/Scripts/HectonSurvivalSystem.cs` (2): 2895, 2970
- `Assets/_Project/Scripts/HectonVoxelEngine.cs` (8): 5676, 6275, 6312, 6352, 6389, 7307, 7308, 7309
- `Assets/_Project/Scripts/HectonWorldGenerator.cs` (7): 2587, 2588, 2589, 2590, 2591, 2592, 2593
- `Assets/_Project/Scripts/ModdingAPI/ModRuntimeState.cs` (2): 257, 322
- `Assets/_Project/Scripts/PlayerInventory.cs` (3): 1605, 1606, 1607
- `Assets/_Project/Scripts/SaveBinaryStorage.cs` (38): 2257, 3327, 3341, 3705, 3778, 3818, 3892, 3893, 3977, 4074, 4075, 4076, 4077, 4078, 4079, 4080, 4081, 4082, 4291, 4477, 4480, 4582, 4670, 4773, 5029, 5626, 5824, 5825, 5834, 5838, 6597, 6598, 6599, 6600, 6668, 6669, 6670, 6671
- `Assets/_Project/Scripts/SaveManager.cs` (3): 2692, 2703, 2714
- `Assets/_Project/Scripts/SavePersistenceOmegaSmokeTester.cs` (2): 95, 96
- `Assets/_Project/Scripts/SaveRecoverySmokeTester.cs` (3): 397, 401, 403
- `Assets/_Project/Scripts/SaveSidecarStorage.cs` (4): 56, 110, 158, 227
- `Assets/_Project/Scripts/SaveSystemRuntimeSmokeTester.cs` (2): 242, 243
- `Assets/_Project/Scripts/ThermalMeltSmokeTester.cs` (7): 115, 116, 117, 152, 153, 193, 194
- `Assets/_Project/Scripts/VoxelDeformationSmokeTester.cs` (10): 203, 204, 207, 253, 254, 301, 302, 303, 304, 305
- `Assets/_Project/Scripts/World/BiomeTransitionSmokeTester.cs` (6): 93, 96, 98, 99, 100, 101
- `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` (2): 1194, 1488
- `Assets/_Project/Scripts/World/HectonBatchRendererGroupUtility.cs` (3): 167, 175, 183
- `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs` (6): 2339, 2353, 2378, 2379, 2380, 2381
- `Assets/_Project/Scripts/World/HectonSandboxAbyssalShelfSmokeTester.cs` (4): 106, 107, 108, 109
- `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs` (3): 2838, 2882, 4109
- `Assets/_Project/Scripts/World/PlanetaryCanvasSmokeTester.cs` (4): 36, 37, 38, 39
- `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs` (6): 2081, 2082, 2083, 2196, 2197, 2198
- `Assets/_Project/Scripts/World/VolumetricBiomeSmokeTester.cs` (1): 327
- `Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs` (3): 1004, 1006, 1151
- `Assets/_Project/Scripts/WorldCaveDirector.cs` (1): 790
- `Assets/_Project/Scripts/WorldProceduralFieldSampler.cs` (3): 2270, 2271, 2272
- `Assets/_Project/Tests/Editor/BaseAtmosphereMathEditTests.cs` (8): 102, 103, 104, 146, 147, 148, 197, 198

Priority calls:
- Kinematic capsulecast buffers are not currently listed as `TempJob` offenders in the scan; existing persistent buffers must not be force-migrated without job-lifetime proof.
- BRG direct draw output in `HectonBatchRendererGroupUtility.cs` is a Unity Graphics-owned contract; replacing with arena memory may violate Unity's deallocation expectations.
- `HectonIndirectVegetationRenderer.cs` visibility and headlight staging arrays are plausible arena candidates after culling job lifetime audit.
- No convolution reverb FFT/delay `TempJob` offenders were found by allocator scan; audio migration needs domain owner confirmation before touching DSP state.
