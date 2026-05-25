# [ARCHIVE] X_012 Historical Report

Archive date: 2026-05-23
Reason: removed from active documentation corpus by X_012; historical evidence only.
Active index: ../../Reports/README.md

# Polish Mandate Static Audit

Evidence class: STATIC_SOURCE. No Unity import, compile, Play Mode, profiler, GC, memory, player build, or device proof was executed.

- Schema: `hecton8.polish_mandate_static_audit.v1`
- Source root: `Assets/_Project/Scripts`
- C# files: `2198`

## Counts

| Category | Matches | Files |
|---|---:|---:|
| `binaryHardwareSwitch` | 8 | 3 |
| `burstCompile` | 1380 | 330 |
| `burstMissingCompileSynchronously` | 0 | 0 |
| `burstMissingFloatMode` | 0 | 0 |
| `burstMissingFloatPrecision` | 0 | 0 |
| `globalQualityWeight` | 1914 | 424 |
| `jobHandleComplete` | 112 | 31 |
| `linqSurface` | 0 | 0 |
| `noAlias` | 2130 | 214 |
| `packOne` | 0 | 0 |
| `privateNativeCollectionField` | 1315 | 228 |
| `structAutoProperties` | 0 | 0 |
| `unityRandom` | 0 | 0 |
| `unityTimeCritical` | 964 | 261 |
| `unityUpdateMethod` | 11 | 11 |

## Top Files

### binaryHardwareSwitch

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` | 4 |
| `Assets/_Project/Scripts/Core/GlobalRegistry.cs` | 3 |
| `Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs` | 1 |

### burstCompile

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs` | 42 |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 18 |
| `Assets/_Project/Scripts/Inventory/Routing/InventoryRoutingNetwork.cs` | 18 |
| `Assets/_Project/Scripts/Core/DistanceMath.cs` | 17 |
| `Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs` | 17 |

### globalQualityWeight

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs` | 27 |
| `Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs` | 26 |
| `Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs` | 26 |
| `Assets/_Project/Scripts/FabricationAssemblerRuntime.cs` | 25 |
| `Assets/_Project/Scripts/Habitat/Deformation/Editor/DamageBake/HabitatDamageBakePipeline.cs` | 24 |

### jobHandleComplete

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs` | 15 |
| `Assets/_Project/Scripts/QA/Headless/JacobiStressFuzzer/PowerGridJacobiStressFuzzer.cs` | 10 |
| `Assets/_Project/Scripts/World/OfflineHadalArchBaker/Editor/HadalArchBakePipeline.cs` | 9 |
| `Assets/_Project/Scripts/Editor/GeologyForge/GeologyForgeGenerator.cs` | 7 |
| `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/Shinobu213/OfflineGeometryBaker.cs` | 7 |

### noAlias

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` | 73 |
| `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs` | 60 |
| `Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs` | 57 |
| `Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorTypes.cs` | 42 |
| `Assets/_Project/Scripts/Cartography/CartographyGridJobs.cs` | 40 |

### privateNativeCollectionField

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Core/GlobalSignals.cs` | 75 |
| `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` | 51 |
| `Assets/_Project/Scripts/PlayerInventory.cs` | 49 |
| `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` | 49 |
| `Assets/_Project/Scripts/Construction/DroneFleetManager.cs` | 40 |

### unityTimeCritical

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/CrashTelemetryBuffer.cs` | 36 |
| `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` | 33 |
| `Assets/_Project/Scripts/SpatialAudioManager.cs` | 32 |
| `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs` | 26 |
| `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs` | 22 |

### unityUpdateMethod

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Editor/FloraDearLieXRayWindow.cs` | 1 |
| `Assets/_Project/Scripts/Editor/SubmarineDynoTunerWindow.cs` | 1 |
| `Assets/_Project/Scripts/Editor/SumpPumpPipeGridTunerWindow.cs` | 1 |
| `Assets/_Project/Scripts/Editor/VerletTowTunerWindow.cs` | 1 |
| `Assets/_Project/Scripts/Physics/Buoyancy/AnalyticalWaveTunerWindow.Editor.cs` | 1 |

## Interpretation

- `Pack=1`, private persistent native collections, and Burst attribute drift are platform-portability risks until each hit is classified as cold file-format, owner-local scratch, or hot runtime.
- `jobHandleComplete`, Unity `Update` methods, `Time.*`, and `UnityEngine.Random` are not automatically defects, but they are mandatory review surfaces for gameplay/runtime code.
- Binary hardware switches are suspect unless they are presentation-only or build-time/platform setup. Runtime scalability should flow through continuous `GlobalQualityWeight` curves.
- This audit is a pressure map. It does not mutate code and does not prove frame cost.
