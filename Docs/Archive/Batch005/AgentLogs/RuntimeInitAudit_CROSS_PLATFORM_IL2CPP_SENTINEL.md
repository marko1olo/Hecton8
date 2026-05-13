# Runtime Init Audit - CROSS_PLATFORM_IL2CPP_SENTINEL

Scope: static scan of `Assets/_Project/Scripts` excluding `Editor` folders.
Pattern: `[RuntimeInitializeOnLoadMethod]` with `SubsystemRegistration`, `BeforeSceneLoad`, or `AfterAssembliesLoaded`, followed within 24 lines by allocation/search/log patterns.
Status: audit found pre-existing suspects outside this agent's platform files. Not fixed here to avoid unauthorized cross-domain rewrites.

## Suspects

- `Bootstrap/BootstrapRouteEnforcer.cs:16` - `Debug.Log`
- `Bootstrap/GameBootstrapper.cs:4963` - `Debug.Log`
- `Core/CameraJuiceSignals.cs:102` - `new NativeQueue`
- `Core/EnvironmentRuntimeContextService.cs:60` - `new GameObject`
- `Core/GCMonitor.cs:28` - `new GameObject`
- `Core/MathGuard.cs:27` - `new NativeQueue`
- `Core/OceanKinematicsRuntimeService.cs:42` - `new GameObject`
- `Core/PlayerRuntimeContextService.cs:280` - `new GameObject`
- `Core/PlayerSensoryManager.cs:110` - `new GameObject`
- `Core/ThreadSafeCommandQueue.cs:229` - `new NativeQueue`
- `Environment/HectonSeismicTideDirector.cs:117` - `new GameObject`
- `Gameplay/VehicleCommandSignals.cs:131` - `new NativeQueue`
- `ModdingAPI/ModCommandDispatcher.cs:278` - `new NativeQueue`
- `ModdingAPI/ModLoader.cs:70` - `Debug.Log`
- `ModdingAPI/ModRuntimeState.cs:86` - `Debug.Log`
- `ModularEquipmentEngine.cs:89` - `new GameObject`
- `Optimization/PreInitAssetIdMap.cs:36` - `new NativeArray`
- `Physiology/PlayerStressMetricsRuntime.cs:77` - `new GameObject`
- `Plugins/MapMagic/HectonRockOutput.cs:77` - `new Dictionary`
- `Power/Generators/RadioisotopeThermalGenerator.cs:348` - `new NativeArray`
- `PrefabRegistry.cs:56` - `new Dictionary`
- `Quest/QuestGraphEvaluator.cs:47` - `new NativeQueue`
- `TerrainChunkGeneratedEvents.cs:68` - `new NativeQueue`
- `TerrainChunkGeneratedEvents.cs:74` - `new NativeQueue`
- `UI/SubtitleManager.cs:254` - `new GameObject`
- `Visor/InternalFloodWaterlineRuntime.cs:99` - `FindAnyObject`
- `VoxelChunkModifiedEvents.cs:113` - `new NativeQueue`
- `VoxelChunkModifiedEvents.cs:119` - `new NativeQueue`
- `World/ChemicalInfluenceGrid.cs:87` - `new GameObject`
- `World/ImpostorSystem.cs:191` - `Debug.Log`
- `World/LODSystemManager.cs:181` - `Debug.Log`
- `World/ProxyLightRegistry.cs:131` - `new NativeArray`
- `WorldProceduralScatterDirector.cs:500` - `new List`

## `UNITY_EDITOR` Recon

Static scan of runtime folders found many `#if UNITY_EDITOR` blocks. The scan is noisy because it catches debug return paths and editor-only diagnostics, but it confirms gameplay-adjacent blocks exist in runtime assemblies. No platform pipeline file uses `UNITY_EDITOR` to hide gameplay data; newly added crash bridge compiles release-safe code instead of stripping a whole runtime type.

Runtime-folder `#if UNITY_EDITOR` occurrence count from `rg`, excluding `Editor` directories: 1648.
