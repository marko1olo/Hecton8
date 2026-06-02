# DataVault Sovereignty Audit - VAULT_SOVEREIGNTY_ENFORCER

Schema: `hecton8.datavault_sovereignty_audit.v3`
Status: `BLOCKED_BASELINE_MISSING`
Source root: `Assets/_Project/Scripts`
Pattern: `\bnew\s+NativeArray\s*<`
Baseline: `Docs/AgentLogs/DataVaultSovereigntyBaseline_VAULT_SOVEREIGNTY_ENFORCER.json`

## Summary

| Metric | Count |
|---|---:|
| Total direct `new NativeArray<T>` constructors | 778 |
| Allowed allocator-internal constructors | 425 |
| Forbidden system constructors | 353 |
| Runtime forbidden constructors | 299 |
| Editor/offline forbidden constructors | 23 |
| Editor/offline transient scratch constructors | 419 |
| Files with forbidden constructors | 68 |
| Editor/offline session scratch declarations | 0 |
| Editor/offline persistent preview declarations | 0 |
| Total field-like `NativeArray<T>` declarations | 5780 |
| Allowed DataVault/H8Memory declarations | 5340 |
| Forbidden system declarations | 440 |
| Persistent owner native collection declarations | 273 |
| Job input native collection declarations | 4763 |
| Burst job input native collection declarations | 4763 |
| Native view/payload/kernel struct declarations | 558 |
| Unknown struct native collection declarations | 186 |
| Files with forbidden declarations | 95 |

## Current Forbidden Constructors By Execution Surface

| Surface | Count |
|---|---:|
| `Editor` | 23 |
| `Plugin` | 31 |
| `Runtime` | 299 |

## Current Forbidden Constructors By Allocator

| Allocator | Count |
|---|---:|
| `Persistent` | 160 |
| `Temp` | 40 |
| `TempJob` | 121 |
| `Unknown` | 32 |

## Editor/Offline Forbidden Constructors By Allocator

| Allocator | Count |
|---|---:|
| `Persistent` | 15 |
| `Unknown` | 8 |

## Top 80 Forbidden Files

| Count | Path | Lines |
|---:|---|---|
| 30 | `Assets/_Project/Scripts/SaveBinaryStorage.cs` | 589, 1070, 2641, 3771, 4135, 4248, 4323, 4324, ... |
| 22 | `Assets/_Project/Scripts/SaveManager.cs` | 1012, 1022, 1032, 1105, 1112, 1119, 1126, 1136, ... |
| 21 | `Assets/_Project/Scripts/SaveSystem/WalIntegrityFuzzerCore.cs` | 178, 221, 222, 223, 224, 340, 719, 720, ... |
| 15 | `Assets/_Project/Scripts/HectonWorldGenerator.cs` | 1213, 1591, 1592, 1593, 1594, 1595, 1596, 1597, ... |
| 14 | `Assets/_Project/Scripts/PlayerInventory.cs` | 3381, 3382, 3551, 3552, 3553, 3554, 3555, 3556, ... |
| 13 | `Assets/_Project/Scripts/EncounterDirector.cs` | 334, 335, 336, 337, 338, 339, 340, 341, ... |
| 12 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs` | 2294, 2298, 2305, 2309, 2313, 2317, 2333, 2337, ... |
| 12 | `Assets/_Project/Scripts/VFX/Debris/ShinobuVoxelSculptorWindow.cs` | 248, 249, 250, 251, 673, 674, 675, 676, ... |
| 11 | `Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonRuntime.cs` | 471, 472, 473, 474, 475, 476, 477, 478, ... |
| 11 | `Assets/_Project/Scripts/PowerGrid.cs` | 1311, 1314, 1317, 1320, 1323, 1326, 1329, 1339, ... |
| 10 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs` | 1622, 1631, 1640, 1649, 1658, 1667, 1676, 1685, ... |
| 10 | `Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs` | 411, 412, 413, 414, 452, 453, 454, 455, ... |
| 10 | `Assets/_Project/Scripts/Plugins/MapMagic/HectonAnomalyMapMagicNode.cs` | 221, 222, 223, 224, 225, 226, 227, 228, ... |
| 9 | `Assets/_Project/Scripts/InventoryGrid.cs` | 101, 102, 103, 104, 105, 106, 107, 108, ... |
| 9 | `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs` | 1368, 1369, 2077, 4306, 4310, 4314, 4318, 4322, ... |
| 8 | `Assets/_Project/Scripts/ConstructionManager.cs` | 1560, 1566, 1575, 1584, 1593, 1602, 1611, 1620 |
| 7 | `Assets/_Project/Scripts/Plugins/MapMagic/HectonSpaceEngine098MapMagicNodes.cs` | 108, 109, 258, 259, 260, 417, 418 |
| 7 | `Assets/_Project/Scripts/SaveSystem/WalIntegrityFuzzerCore_SHINOBU357.cs` | 204, 215, 226, 237, 242, 439, 501 |
| 7 | `Assets/_Project/Scripts/World/AbyssalThermalManager.cs` | 2685, 2696, 2707, 2718, 2728, 2739, 3122 |
| 6 | `Assets/_Project/Scripts/Plugins/MapMagic/HectonHydraulicErosionMapMagicNode.cs` | 272, 273, 274, 275, 276, 336 |
| 6 | `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs` | 2812, 2813, 2814, 2940, 2941, 2942 |
| 6 | `Assets/_Project/Scripts/WorldProceduralScatterWorkingMemory.cs` | 161, 167, 168, 169, 170, 573 |
| 5 | `Assets/_Project/Scripts/Core/UIStateStore.cs` | 156, 157, 158, 159, 160 |
| 5 | `Assets/_Project/Scripts/Editor/LSystemGenomeLabWindow.cs` | 267, 268, 269, 270, 271 |
| 5 | `Assets/_Project/Scripts/Quest/QuestStateManager.cs` | 191, 192, 193, 409, 414 |
| 5 | `Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs` | 903, 1382, 2171, 2183, 2195 |
| 4 | `Assets/_Project/Scripts/Plugins/MapMagic/HectonTerrainSplatmapMapMagicNode.cs` | 135, 136, 137, 138 |
| 4 | `Assets/_Project/Scripts/SaveSidecarStorage.cs` | 56, 110, 158, 227 |
| 4 | `Assets/_Project/Scripts/World/BiotaDensityMapBaker/Editor/BiotaDensityBakePipeline.cs` | 202, 314, 447, 559 |
| 4 | `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs` | 3862, 4759, 4803, 6063 |
| 4 | `Assets/_Project/Scripts/WorldCaveDirector.cs` | 871, 872, 873, 874 |
| 3 | `Assets/_Project/Scripts/FlowFieldVisualizer.cs` | 775, 776, 957 |
| 3 | `Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs` | 1175, 1179, 1183 |
| 3 | `Assets/_Project/Scripts/Input/ControlRemapper.cs` | 160, 256, 266 |
| 3 | `Assets/_Project/Scripts/WorldProceduralFieldSampler.cs` | 2529, 2530, 2531 |
| 2 | `Assets/_Project/Scripts/Core/ConnectionSplineBatchRenderer.cs` | 842, 857 |
| 2 | `Assets/_Project/Scripts/Core/GlobalTelemetryBus.cs` | 765, 776 |
| 2 | `Assets/_Project/Scripts/Editor/ChroniclerDiagnosticHeatmapWindow.cs` | 122, 124 |
| 2 | `Assets/_Project/Scripts/Editor/SignalTrafficMonitorWindow.cs` | 83, 85 |
| 2 | `Assets/_Project/Scripts/Habitat/Deformation/Editor/DamageBake/HabitatDamageBakePipeline.cs` | 693, 1418 |
| 2 | `Assets/_Project/Scripts/HectonVoxelVolume.cs` | 2085, 2094 |
| 2 | `Assets/_Project/Scripts/ModdingAPI/ModRuntimeState.cs` | 256, 321 |
| 2 | `Assets/_Project/Scripts/Plugins/MapMagic/HectonBiomeMatrixMapMagicPostProcessNode.cs` | 71, 72 |
| 2 | `Assets/_Project/Scripts/Plugins/MapMagic/HectonSandboxAbyssalShelfMapMagicNode.cs` | 103, 104 |
| 2 | `Assets/_Project/Scripts/SaveThumbnailSystem.cs` | 767, 793 |
| 2 | `Assets/_Project/Scripts/World/TOOL_Procedural_Wreckage_Generator.cs` | 72, 74 |
| 2 | `Assets/_Project/Scripts/World/VegetationCapacityUtilities.cs` | 227, 252 |
| 1 | `Assets/_Project/Scripts/Core/DodReplayRecorder.cs` | 942 |
| 1 | `Assets/_Project/Scripts/Core/JobFenceManager.cs` | 26 |
| 1 | `Assets/_Project/Scripts/Core/NativeRingBuffer.cs` | 33 |
| 1 | `Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureBakeBlackBox.cs` | 75 |
| 1 | `Assets/_Project/Scripts/Editor/AssemblyGuard/CompileWallXRayWindow.cs` | 1707 |
| 1 | `Assets/_Project/Scripts/Editor/Audio/OOP_AudioBridge_Scanner.cs` | 498 |
| 1 | `Assets/_Project/Scripts/Editor/DodReplayPressureMapWindow.cs` | 29 |
| 1 | `Assets/_Project/Scripts/Editor/GeographySanity/GeographySanityPipeline.cs` | 1236 |
| 1 | `Assets/_Project/Scripts/Editor/GeologyForge/TopographyForgeGenerator.cs` | 485 |
| 1 | `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/Shinobu213/OfflineGeometryBakeBlackBox.cs` | 124 |
| 1 | `Assets/_Project/Scripts/Editor/TextureChannelPacker/HectonArmTextureChannelPacker.cs` | 1408 |
| 1 | `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs` | 1183 |
| 1 | `Assets/_Project/Scripts/ModdingAPI/ModEventProjectionBridge.cs` | 144 |
| 1 | `Assets/_Project/Scripts/Optimization/PreInitAssetIdMap.cs` | 66 |
| 1 | `Assets/_Project/Scripts/QA/Headless/JacobiStressFuzzer/PowerGridJacobiStressFuzzer.cs` | 334 |
| 1 | `Assets/_Project/Scripts/Scavenging/ScavengingLootOracle.cs` | 1991 |
| 1 | `Assets/_Project/Scripts/World/FloraRegrowthDirector.cs` | 932 |
| 1 | `Assets/_Project/Scripts/World/HectonDistantLandmarkRenderer.cs` | 381 |
| 1 | `Assets/_Project/Scripts/World/HectonHLODRenderer.cs` | 271 |
| 1 | `Assets/_Project/Scripts/World/WreckMaterialRegistry.cs` | 443 |
| 1 | `Assets/_Project/Scripts/WorldGenerativeGeologyVoxelBridgeDirector.cs` | 1289 |

## Top 80 Forbidden Declaration Files

| Count | Path | Lines |
|---:|---|---|
| 22 | `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs` | 316, 318, 319, 320, 321, 322, 323, 324, ... |
| 20 | `Assets/_Project/Scripts/QA/Headless/JacobiStressFuzzer/PowerGridJacobiStressFuzzer.cs` | 551, 552, 553, 554, 555, 556, 557, 558, ... |
| 18 | `Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs` | 194, 195, 196, 197, 198, 199, 200, 201, ... |
| 16 | `Assets/_Project/Scripts/SaveSystem/VoxelDeltaCompressionArchitecture.cs` | 177, 178, 179, 180, 181, 182, 183, 184, ... |
| 15 | `Assets/_Project/Scripts/Tools/ToolKinematics/ToolKinematicsRuntime.cs` | 1326, 1327, 1328, 1329, 1330, 1331, 1332, 1333, ... |
| 14 | `Assets/_Project/Scripts/Core/DodReplayRecorder.cs` | 381, 382, 383, 384, 385, 386, 387, 388, ... |
| 13 | `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs` | 251, 252, 253, 254, 255, 256, 257, 258, ... |
| 13 | `Assets/_Project/Scripts/EncounterDirector.cs` | 278, 279, 280, 281, 282, 283, 284, 285, ... |
| 13 | `Assets/_Project/Scripts/SaveManager.cs` | 148, 168, 194, 195, 196, 197, 198, 199, ... |
| 13 | `Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs` | 172, 173, 174, 175, 176, 177, 178, 179, ... |
| 12 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs` | 112, 113, 114, 115, 116, 117, 118, 119, ... |
| 12 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs` | 1342, 1343, 1344, 1345, 1346, 1347, 1348, 1349, ... |
| 12 | `Assets/_Project/Scripts/World/GlobalWorldSampler.cs` | 189, 190, 191, 192, 193, 194, 195, 196, ... |
| 11 | `Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonRuntime.cs` | 44, 45, 46, 47, 48, 49, 50, 51, ... |
| 11 | `Assets/_Project/Scripts/SaveSystem/SaveStateMerkleTree.cs` | 192, 193, 194, 195, 196, 197, 198, 199, ... |
| 10 | `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs` | 1362, 1363, 1364, 1365, 1366, 1367, 1368, 1369, ... |
| 10 | `Assets/_Project/Scripts/WorldProceduralScatterWorkingMemory.cs` | 26, 27, 28, 29, 30, 62, 68, 69, ... |
| 9 | `Assets/_Project/Scripts/InventoryGrid.cs` | 54, 55, 56, 57, 58, 59, 60, 61, ... |
| 9 | `Assets/_Project/Scripts/PowerGrid.cs` | 178, 179, 180, 181, 182, 183, 184, 185, ... |
| 9 | `Assets/_Project/Scripts/SaveBinaryStorage.cs` | 1282, 1283, 1284, 1285, 1286, 1287, 1288, 1289, ... |
| 8 | `Assets/_Project/Scripts/ConstructionManager.cs` | 176, 177, 178, 179, 180, 181, 182, 183 |
| 8 | `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchBakePipeline.cs` | 246, 247, 248, 249, 250, 251, 252, 254 |
| 8 | `Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs` | 636, 637, 638, 639, 640, 641, 642, 643 |
| 7 | `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime_StatusEffects.cs` | 58, 59, 60, 61, 62, 63, 64 |
| 7 | `Assets/_Project/Scripts/HectonWorldGenerator.cs` | 688, 689, 690, 691, 692, 693, 694 |
| 7 | `Assets/_Project/Scripts/LocRegistry.cs` | 508, 509, 510, 511, 512, 513, 524 |
| 7 | `Assets/_Project/Scripts/World/AbyssalThermalManager.cs` | 670, 671, 672, 673, 674, 675, 676 |
| 5 | `Assets/_Project/Scripts/Core/Origin/AupOriginShiftCoordinator.cs` | 59, 60, 61, 62, 63 |
| 5 | `Assets/_Project/Scripts/Core/UIStateStore.cs` | 105, 106, 107, 108, 109 |
| 5 | `Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentJobs.cs` | 436, 437, 438, 439, 440 |
| 5 | `Assets/_Project/Scripts/Gameplay/HectonPlayerState.cs` | 172, 173, 174, 175, 176 |
| 5 | `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs` | 325, 326, 327, 328, 329 |
| 5 | `Assets/_Project/Scripts/Quest/QuestStateManager.cs` | 54, 55, 56, 57, 58 |
| 5 | `Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeVaultRuntime.cs` | 19, 20, 21, 22, 23 |
| 5 | `Assets/_Project/Scripts/World/FloraRegrowthDirector.cs` | 310, 312, 316, 319, 320 |
| 4 | `Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs` | 389, 390, 391, 392 |
| 4 | `Assets/_Project/Scripts/WorldProceduralScatterWorkingMemory.cs` | 59, 63, 64, 65 |
| 3 | `Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs` | 294, 295, 296 |
| 3 | `Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs` | 286, 287, 288 |
| 3 | `Assets/_Project/Scripts/Physics/KCC/Editor/Shinobu355KccSmokeEditorFacade.cs` | 523, 524, 525 |
| 3 | `Assets/_Project/Scripts/World/FloraRegrowthDirector.cs` | 311, 313, 317 |
| 2 | `Assets/_Project/Scripts/Core/ConnectionSplineBatchRenderer.cs` | 60, 61 |
| 2 | `Assets/_Project/Scripts/Core/GlobalTelemetryBus.cs` | 101, 102 |
| 2 | `Assets/_Project/Scripts/Core/NativeQuery.cs` | 166, 168 |
| 2 | `Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs` | 1608, 1619 |
| 2 | `Assets/_Project/Scripts/Core/SystemDispatcherContracts.cs` | 248, 249 |
| 2 | `Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs` | 312, 313 |
| 2 | `Assets/_Project/Scripts/Editor/ChroniclerDiagnosticHeatmapWindow.cs` | 45, 46 |
| 2 | `Assets/_Project/Scripts/Editor/SignalTrafficMonitorWindow.cs` | 19, 20 |
| 2 | `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs` | 444, 445 |
| 2 | `Assets/_Project/Scripts/Quest/QuestStateManager.cs` | 59, 60 |
| 2 | `Assets/_Project/Scripts/SaveThumbnailSystem.cs` | 144, 145 |
| 2 | `Assets/_Project/Scripts/World/TOOL_Procedural_Wreckage_Generator.cs` | 63, 65 |
| 1 | `Assets/_Project/Scripts/Construction/DroneFleetNavigationKernel.cs` | 1331 |
| 1 | `Assets/_Project/Scripts/Construction/DroneFleetNavigationKernel.cs` | 1427 |
| 1 | `Assets/_Project/Scripts/ConstructionManager.cs` | 174 |
| 1 | `Assets/_Project/Scripts/Core/BurstCallback.cs` | 46 |
| 1 | `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs` | 4865 |
| 1 | `Assets/_Project/Scripts/Core/JobFenceManager.cs` | 15 |
| 1 | `Assets/_Project/Scripts/Core/NativeQuery.cs` | 16 |
| 1 | `Assets/_Project/Scripts/Core/NativeQuery.cs` | 44 |
| 1 | `Assets/_Project/Scripts/Core/NativeQuery.cs` | 142 |
| 1 | `Assets/_Project/Scripts/Core/NativeQuery.cs` | 144 |
| 1 | `Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs` | 43 |
| 1 | `Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs` | 334 |
| 1 | `Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs` | 569 |
| 1 | `Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureBakeBlackBox.cs` | 16 |
| 1 | `Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureControlMapBaker.cs` | 959 |
| 1 | `Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureControlMapBaker.cs` | 974 |
| 1 | `Assets/_Project/Scripts/Editor/AssemblyGuard/CompileWallXRayWindow.cs` | 1614 |
| 1 | `Assets/_Project/Scripts/Editor/DodReplayPressureMapWindow.cs` | 18 |
| 1 | `Assets/_Project/Scripts/Editor/GeographySanity/GeographySanityProfileCsv.cs` | 13 |
| 1 | `Assets/_Project/Scripts/Editor/GeologyForge/TopographyForgeCsv.cs` | 13 |
| 1 | `Assets/_Project/Scripts/Editor/OOP_Trigger_Scanner.cs` | 170 |
| 1 | `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/InteriorClutterForgeSupport.cs` | 19 |
| 1 | `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/Shinobu213/OfflineGeometryBakeBlackBox.cs` | 23 |
| 1 | `Assets/_Project/Scripts/Editor/TextureChannelPacker/HectonArmTextureChannelPacker.cs` | 1301 |
| 1 | `Assets/_Project/Scripts/EncounterDirector.cs` | 288 |
| 1 | `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs` | 317 |
| 1 | `Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs` | 1062 |

## Allowed Allocator-Internal Sites

| Count | Path | Lines |
|---:|---|---|
| 56 | `Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs` | 153, 154, 155, 320, 321, 397, 398, 399, ... |
| 21 | `Assets/_Project/Scripts/World/BiomeWeightMapBaker/Editor/BiomeWeightMapBakePipeline.cs` | 176, 177, 178, 179, 180, 181, 183, 184, ... |
| 18 | `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/Shinobu213/OfflineGeometryBaker.cs` | 401, 461, 475, 477, 479, 481, 557, 670, ... |
| 17 | `Assets/_Project/Scripts/World/OfflineWreckageBaker/Editor/WreckageForgeWindow.cs` | 259, 260, 261, 262, 263, 311, 312, 313, ... |
| 13 | `Assets/_Project/Scripts/Editor/ErosionTestHarness.cs` | 84, 85, 86, 87, 88, 93, 94, 271, ... |
| 11 | `Assets/_Project/Scripts/World/StaticCaveSdfBaker/Editor/StaticCaveSdfBakePipeline.cs` | 107, 175, 176, 177, 178, 197, 198, 226, ... |
| 11 | `Assets/_Project/Scripts/World/VoxelTerrainSeamBinder/Editor/VoxelTerrainSeamBinderPipeline.cs` | 86, 127, 128, 129, 130, 335, 336, 339, ... |
| 10 | `Assets/_Project/Scripts/Dev/OmegaAutonomySmokeTester.cs` | 150, 151, 152, 153, 154, 155, 385, 386, ... |
| 10 | `Assets/_Project/Scripts/VoxelDeformationSmokeTester.cs` | 223, 224, 225, 273, 274, 321, 322, 323, ... |
| 9 | `Assets/_Project/Scripts/Editor/AnomalySmokeTester.cs` | 75, 76, 77, 78, 79, 80, 81, 189, ... |
| 9 | `Assets/_Project/Scripts/Editor/GeologyForge/GeologyForgeGenerator.cs` | 113, 175, 288, 314, 327, 343, 651, 678, ... |
| 9 | `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/InteriorClutterForge.cs` | 397, 399, 401, 404, 425, 427, 497, 1014, ... |
| 8 | `Assets/_Project/Scripts/Audio/Synthesis/Editor/AudioSynthesisMemorySovereigntyValidator.cs` | 499, 500, 501, 502, 503, 504, 505, 506 |
| 8 | `Assets/_Project/Scripts/World/OfflineWreckageBaker/Editor/OfflineWreckageMockBenchmark.cs` | 45, 46, 47, 48, 49, 50, 51, 52 |
| 7 | `Assets/_Project/Scripts/Editor/GeologyForge/TopographyForgeWindow.cs` | 188, 192, 194, 195, 196, 197, 198 |
| 7 | `Assets/_Project/Scripts/Editor/HydraulicErosionSmokeTester.cs` | 207, 208, 209, 210, 211, 212, 213 |
| 6 | `Assets/_Project/Scripts/AutomationOmegaSmokeTester.cs` | 51, 52, 53, 54, 55, 56 |
| 6 | `Assets/_Project/Scripts/AutomationSmokeTester.cs` | 39, 40, 41, 42, 43, 44 |
| 6 | `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` | 2482, 2484, 2488, 2490, 2648, 4000 |
| 6 | `Assets/_Project/Scripts/Dev/SpaceEngine098/SpaceEngine098TerrainSmokeTester.cs` | 136, 137, 138, 139, 140, 141 |
| 6 | `Assets/_Project/Scripts/ThermalMeltSmokeTester.cs` | 116, 117, 153, 154, 194, 195 |
| 6 | `Assets/_Project/Scripts/World/BiomeTransitionSmokeTester.cs` | 94, 95, 99, 100, 101, 102 |
| 5 | `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/InteriorClutterForgeSupport.cs` | 28, 150, 357, 359, 534 |
| 5 | `Assets/_Project/Scripts/Editor/ProceduralGen/BioForgeGenerator.cs` | 77, 204, 209, 256, 303 |
| 5 | `Assets/_Project/Scripts/FaunaRuntimeSmokeTester.cs` | 186, 187, 188, 260, 261 |
| 4 | `Assets/_Project/Scripts/Editor/TextureChannelPacker/TextureChannelPackerWindow.cs` | 408, 409, 410, 629 |
| 4 | `Assets/_Project/Scripts/Gameplay/Editor/ScannerLoreDatabaseSyncTunerWindow.cs` | 130, 131, 132, 133 |
| 4 | `Assets/_Project/Scripts/World/HectonSandboxAbyssalShelfSmokeTester.cs` | 106, 107, 108, 109 |
| 4 | `Assets/_Project/Scripts/World/OfflineHadalArchBaker/Editor/HadalArchBakePipeline.cs` | 96, 97, 98, 99 |
| 4 | `Assets/_Project/Scripts/World/PlanetaryCanvasSmokeTester.cs` | 36, 37, 38, 39 |
| 3 | `Assets/_Project/Scripts/Core/Data/Editor/CacheBTreeTopologyXRayWindow.cs` | 467, 468, 469 |
| 3 | `Assets/_Project/Scripts/Dev/BiomeBoundarySdfSmokeTester.cs` | 40, 41, 42 |
| 3 | `Assets/_Project/Scripts/Editor/HectonOctahedralImpostorBaker.cs` | 126, 130, 241 |
| 3 | `Assets/_Project/Scripts/SaveRecoverySmokeTester.cs` | 397, 401, 403 |
| 3 | `Assets/_Project/Scripts/World/OfflineHadalArchBaker/Editor/HadalStructureForgeWindow.cs` | 331, 332, 333 |
| 2 | `Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureMockMeshJobs.cs` | 161, 165 |
| 2 | `Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureProfileCsv.cs` | 63, 122 |
| 2 | `Assets/_Project/Scripts/Editor/Arm64MemoryAlignmentXRayWindow.cs` | 69, 70 |
| 2 | `Assets/_Project/Scripts/Editor/EconomyRecipeTunerWindow.cs` | 224, 263 |
| 2 | `Assets/_Project/Scripts/Editor/HlodImpostorForgeWindow.cs` | 135, 191 |
| 2 | `Assets/_Project/Scripts/Editor/WorldProceduralProxySceneBuilder.cs` | 271, 272 |
| 2 | `Assets/_Project/Scripts/SavePersistenceOmegaSmokeTester.cs` | 99, 100 |
| 2 | `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchForgeWindow.cs` | 278, 279 |
| 2 | `Assets/_Project/Scripts/World/VoxelTerrainSeamBinder/Editor/VoxelTerrainSeamPreviewGizmo.cs` | 53, 54 |
| 1 | `Assets/_Project/Scripts/Audio/Editor/AcousticPortalMemorySovereigntyValidator.cs` | 170 |
| 1 | `Assets/_Project/Scripts/Dev/HabitatStressSmokeTester.cs` | 504 |
| 1 | `Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureControlMapBaker.cs` | 271 |
| 1 | `Assets/_Project/Scripts/Editor/BaseModuleCatalogEditorTools.cs` | 250 |
| 1 | `Assets/_Project/Scripts/Editor/GeologyForge/GeologyForgeWindow.cs` | 219 |
| 1 | `Assets/_Project/Scripts/Editor/GeologyForge/GeologyProfileCsv.cs` | 47 |
| 1 | `Assets/_Project/Scripts/Editor/GeologyForge/TopographyForgeCsv.cs` | 95 |
| 1 | `Assets/_Project/Scripts/Editor/HydraulicErosionForge/Shinobu242/HydraulicErosionForgeBaker.cs` | 776 |
| 1 | `Assets/_Project/Scripts/Editor/HydraulicErosionForge/Shinobu242/HydraulicErosionWeatheringCsv.cs` | 39 |
| 1 | `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/InteriorClutterForgeJobs.cs` | 165 |
| 1 | `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/Shinobu213/OfflineOptimizationProfileCsv.cs` | 43 |
| 1 | `Assets/_Project/Scripts/Editor/PlanetaryCanvasSmokeTester.cs` | 55 |
| 1 | `Assets/_Project/Scripts/Editor/Shinobu132CablePhysicsTunerWindow.cs` | 157 |
| 1 | `Assets/_Project/Scripts/Physics/Buoyancy/Editor/AsyncBuoyancyReadbackLayoutValidator.cs` | 46 |
| 1 | `Assets/_Project/Scripts/SaveSystemRuntimeSmokeTester.cs` | 242 |
| 1 | `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchBakePipeline.cs` | 1048 |
| 1 | `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchMockBenchmark.cs` | 29 |
| 1 | `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/TectonicRiftProfileCsvParser.cs` | 54 |
| 1 | `Assets/_Project/Scripts/World/VolumetricBiomeSmokeTester.cs` | 327 |
| 1 | `Assets/_Project/Scripts/World/VoxelTerrainSeamBinder/Editor/VoxelTerrainSeamBinderWindow.cs` | 554 |

## Allowed DataVault/H8Memory Declaration Sites

| Count | Path | Lines |
|---:|---|---|
| 39 | `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | 583, 585, 586, 587, 588, 589, 590, 591, ... |
| 37 | `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs` | 5422, 5423, 5424, 5425, 5426, 5427, 5428, 5429, ... |
| 32 | `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs` | 1697, 1698, 1699, 1700, 1701, 1702, 1703, 1704, ... |
| 28 | `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs` | 2026, 2027, 2029, 2031, 2032, 2033, 2035, 2037, ... |
| 28 | `Assets/_Project/Scripts/ModularEquipmentEngine.cs` | 160, 161, 162, 163, 164, 165, 166, 167, ... |
| 26 | `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs` | 1289, 1290, 1291, 1292, 1293, 1294, 1295, 1296, ... |
| 22 | `Assets/_Project/Scripts/HectonFluidEngine.cs` | 8360, 8361, 8362, 8363, 8364, 8365, 8366, 8367, ... |
| 21 | `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 3042, 3043, 3044, 3045, 3046, 3047, 3048, 3049, ... |
| 21 | `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs` | 5081, 5083, 5084, 5085, 5086, 5087, 5088, 5089, ... |
| 21 | `Assets/_Project/Scripts/PDA/CartographyGridJobs.cs` | 505, 506, 507, 508, 509, 510, 511, 512, ... |
| 21 | `Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs` | 168, 169, 170, 171, 172, 173, 174, 175, ... |
| 20 | `Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs` | 336, 337, 338, 339, 340, 341, 342, 343, ... |
| 20 | `Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralVault.cs` | 66, 67, 68, 69, 70, 71, 72, 73, ... |
| 19 | `Assets/_Project/Scripts/Construction/FluidPipePressureJobs.cs` | 21, 22, 23, 24, 25, 26, 27, 28, ... |
| 19 | `Assets/_Project/Scripts/Construction/ShinobuSocketConstructionData.cs` | 251, 252, 253, 254, 255, 256, 257, 258, ... |
| 19 | `Assets/_Project/Scripts/Gameplay/Combat/HectonCombatRuntime_ArmorPenetration.cs` | 167, 168, 169, 170, 171, 172, 173, 174, ... |
| 18 | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs` | 2330, 2331, 2332, 2333, 2334, 2335, 2336, 2337, ... |
| 18 | `Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageVault.cs` | 62, 63, 64, 65, 66, 67, 68, 69, ... |
| 17 | `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime_StatusEffects.cs` | 1715, 1716, 1717, 1718, 1719, 1720, 1721, 1722, ... |
| 17 | `Assets/_Project/Scripts/Inventory/InventoryDefragJob.cs` | 30, 31, 32, 33, 34, 35, 36, 37, ... |
| 17 | `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs` | 1011, 1012, 1013, 1014, 1015, 1016, 1017, 1018, ... |
| 17 | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` | 2841, 2842, 2846, 2847, 2848, 2849, 2850, 2851, ... |
| 17 | `Assets/_Project/Scripts/QA/Headless/JacobiStressFuzzer/PowerGridJacobiStressFuzzer.cs` | 1495, 1496, 1497, 1498, 1499, 1500, 1501, 1502, ... |
| 17 | `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 5022, 5024, 5025, 5026, 5027, 5028, 5029, 5030, ... |
| 16 | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainJobs.cs` | 81, 82, 83, 84, 85, 86, 87, 88, ... |
| 16 | `Assets/_Project/Scripts/Construction/DroneCognitionJob.cs` | 310, 311, 318, 319, 320, 321, 322, 324, ... |
| 16 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 710, 713, 714, 715, 716, 717, 718, 719, ... |
| 16 | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` | 2236, 2237, 2238, 2239, 2240, 2241, 2242, 2243, ... |
| 16 | `Assets/_Project/Scripts/Quest/QuestDagRuntimeTypes.cs` | 320, 321, 322, 323, 324, 325, 326, 327, ... |
| 15 | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainVault.cs` | 92, 93, 94, 95, 96, 97, 98, 99, ... |
| 15 | `Assets/_Project/Scripts/Construction/FoundationSnappingCalculatorData.cs` | 187, 188, 189, 190, 191, 192, 193, 194, ... |
| 15 | `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs` | 460, 461, 462, 463, 464, 465, 466, 467, ... |
| 15 | `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs` | 793, 794, 795, 796, 797, 798, 799, 800, ... |
| 15 | `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs` | 1169, 1170, 1171, 1172, 1173, 1174, 1175, 1176, ... |
| 15 | `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsJobs.cs` | 174, 175, 176, 177, 178, 179, 180, 181, ... |
| 15 | `Assets/_Project/Scripts/Quest/QuestDagResolverRuntime.cs` | 716, 717, 718, 719, 720, 721, 722, 723, ... |
| 14 | `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | 2922, 2923, 2924, 2925, 2926, 2927, 2928, 2929, ... |
| 14 | `Assets/_Project/Scripts/Inventory/SoaInventoryQueryEngine.CargoSync.cs` | 1017, 1018, 1019, 1020, 1021, 1022, 1023, 1024, ... |
| 14 | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` | 2288, 2289, 2290, 2291, 2292, 2293, 2294, 2295, ... |
| 14 | `Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs` | 3147, 3148, 3149, 3150, 3151, 3152, 3153, 3154, ... |
| 13 | `Assets/_Project/Scripts/AI/Pathfinding/VoxelAStarJobs.cs` | 343, 344, 345, 346, 347, 348, 349, 350, ... |
| 13 | `Assets/_Project/Scripts/Fauna/LeviathanTentacleVerletSolver.cs` | 556, 557, 558, 559, 560, 561, 562, 563, ... |
| 13 | `Assets/_Project/Scripts/Gameplay/AirlockPressurization/AirlockPressurizationRuntime.cs` | 70, 71, 72, 73, 74, 75, 76, 77, ... |
| 13 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 2790, 2791, 2792, 2793, 2794, 2795, 2796, 2797, ... |
| 13 | `Assets/_Project/Scripts/Inventory/Routing/InventoryRoutingNetwork.cs` | 263, 264, 265, 266, 267, 268, 269, 270, ... |
| 13 | `Assets/_Project/Scripts/Physics/KCC/HectonKccRuntime_SmokeTest.cs` | 301, 302, 303, 304, 305, 306, 307, 308, ... |
| 13 | `Assets/_Project/Scripts/World/BiotaDensityMapBaker/Editor/BiotaDensityBakeJobs.cs` | 274, 275, 276, 277, 278, 279, 280, 281, ... |
| 13 | `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | 3294, 3295, 3296, 3297, 3298, 3299, 3300, 3301, ... |
| 12 | `Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs` | 270, 271, 272, 273, 274, 275, 276, 277, ... |
| 12 | `Assets/_Project/Scripts/Audio/VocalWarningSystem.cs` | 238, 239, 240, 241, 242, 243, 244, 245, ... |
| 12 | `Assets/_Project/Scripts/Construction/HabitatDeconstructionTransactionKernel.cs` | 267, 268, 269, 270, 271, 272, 273, 274, ... |
| 12 | `Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentRouterRuntime.cs` | 1424, 1425, 1426, 1427, 1428, 1429, 1430, 1431, ... |
| 12 | `Assets/_Project/Scripts/Gameplay/Combat/HectonCombatRuntime_ArmorPenetration.cs` | 2181, 2182, 2183, 2184, 2185, 2186, 2187, 2189, ... |
| 12 | `Assets/_Project/Scripts/Inventory/InventorySoAUtility.cs` | 292, 293, 294, 295, 296, 297, 298, 299, ... |
| 12 | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs` | 3186, 3187, 3188, 3189, 3190, 3191, 3192, 3193, ... |
| 12 | `Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementJobs.cs` | 103, 104, 105, 106, 107, 108, 109, 110, ... |
| 12 | `Assets/_Project/Scripts/Tools/UpgradeMatrixCompiler.cs` | 75, 76, 77, 78, 79, 80, 81, 82, ... |
| 12 | `Assets/_Project/Scripts/World/EcosystemDirector.cs` | 596, 597, 598, 599, 600, 601, 602, 603, ... |
| 12 | `Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs` | 619, 620, 621, 622, 623, 624, 625, 626, ... |
| 11 | `Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault.cs` | 71, 72, 73, 74, 75, 76, 77, 78, ... |
| 11 | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs` | 4374, 4375, 4376, 4377, 4378, 4379, 4380, 4381, ... |
| 11 | `Assets/_Project/Scripts/Construction/DroneFleetNavigationKernel.cs` | 1600, 1601, 1602, 1603, 1604, 1605, 1606, 1607, ... |
| 11 | `Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs` | 983, 984, 985, 986, 987, 988, 989, 990, ... |
| 11 | `Assets/_Project/Scripts/Fauna/MesofaunaBehavioralStateMachine.cs` | 577, 578, 579, 580, 581, 582, 583, 584, ... |
| 11 | `Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs` | 2650, 2651, 2652, 2653, 2654, 2655, 2656, 2657, ... |
| 11 | `Assets/_Project/Scripts/Physics/Cavitation/AbyssalCavitationRuntime.cs` | 1488, 1489, 1490, 1491, 1492, 1493, 1494, 1495, ... |
| 11 | `Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs` | 927, 928, 929, 930, 931, 932, 933, 934, ... |
| 11 | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsContracts.cs` | 1571, 1572, 1573, 1574, 1575, 1576, 1577, 1578, ... |
| 11 | `Assets/_Project/Scripts/Physiology/ShinobuPhysiologyJobs.cs` | 1215, 1216, 1217, 1218, 1219, 1220, 1221, 1222, ... |
| 11 | `Assets/_Project/Scripts/Plugins/Crest/OceanKinematics/OceanKinematicsVaultRuntime.cs` | 33, 34, 35, 36, 37, 38, 39, 40, ... |
| 11 | `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` | 3596, 3597, 3598, 3599, 3600, 3601, 3602, 3606, ... |
| 11 | `Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralJobs.cs` | 259, 263, 267, 270, 273, 276, 279, 282, ... |
| 11 | `Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs` | 776, 777, 778, 779, 780, 781, 782, 783, ... |
| 11 | `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsJobs.cs` | 57, 60, 63, 66, 69, 73, 77, 80, ... |
| 10 | `Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs` | 255, 256, 257, 258, 259, 260, 261, 262, ... |
| 10 | `Assets/_Project/Scripts/Construction/HabitatConstructionManager.cs` | 1530, 1531, 1532, 1533, 1534, 1535, 1536, 1537, ... |
| 10 | `Assets/_Project/Scripts/Data/Monolith/H8CreatureSoAReconstructJob.cs` | 55, 59, 60, 61, 62, 63, 64, 65, ... |
| 10 | `Assets/_Project/Scripts/Fauna/LeviathanTentacleVerletSolver.cs` | 102, 103, 104, 105, 106, 107, 108, 109, ... |
| 10 | `Assets/_Project/Scripts/Fauna/MesofaunaBehavioralStateMachine.cs` | 271, 272, 273, 274, 275, 276, 277, 278, ... |
| 10 | `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.AcousticSdf.cs` | 1840, 1841, 1842, 1843, 1844, 1845, 1846, 1847, ... |

## Gate Commands

```powershell
python Tools\DataVaultSovereigntyAudit.py --fail-on-regression
python Tools\DataVaultSovereigntyAudit.py --fail-on-any
```

`--fail-on-regression` blocks any new or increased forbidden constructor or field-declaration count against the baseline.
`--fail-on-any` is the final zero-debt gate and currently fails until all legacy debt is migrated.
