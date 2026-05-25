# Compile Wall X_003 Static Archaeology

Evidence class: STATIC_SOURCE. No Unity import, C# compile, runtime wiring, GC, profiler, or player build proof.

## Assembly Graph

- Asmdefs: `167`
- Runtime first-party asmdefs: `103`
- Edges: `392`
- Runtime concrete sibling refs: `91`
- `autoReferenced=true` first-party asmdefs: `0`
- Unresolved first-party refs: `0`
- Cycles: `0`

## Gravity Wells

| Assembly | Blast Radius | Direct Inbound | Outbound | First-Party Outbound |
|---|---:|---:|---:|---:|
| `Hecton8.Core.Contracts` | 135 | 99 | 0 | 0 |
| `Hecton8.Core.Memory` | 116 | 75 | 1 | 1 |
| `Hecton8.Bootstrap.Contracts` | 110 | 16 | 0 | 0 |
| `Hecton8.UI.Diegetic.Contracts` | 110 | 2 | 0 | 0 |
| `Hecton8.Tools.ToolKinematics.Contracts` | 109 | 3 | 1 | 1 |
| `Hecton8.Audio.Virtualization.Contracts` | 109 | 2 | 1 | 1 |
| `Hecton8.Core.Scheduling` | 109 | 2 | 2 | 2 |
| `Hecton8.Logistics.Grid.Contracts` | 109 | 2 | 1 | 1 |
| `Hecton8.Core.Bucketing` | 109 | 1 | 2 | 2 |
| `Hecton8.Core.Database` | 109 | 1 | 2 | 2 |
| `Hecton8.Core.Persistence.Paging` | 109 | 1 | 1 | 1 |
| `Hecton8.Inventory.Corrosion.Contracts` | 109 | 1 | 0 | 0 |

## DTO / Interface Extraction Candidates

| Type | Kind | Assembly | External Assemblies | External Domains | Path |
|---|---|---|---:|---:|---|
| `IDataVault` | `interface` | `Hecton8.Core.Memory` | 71 | 49 | `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:29` |
| `BufferID` | `enum` | `Hecton8.Core.Memory` | 67 | 49 | `Assets/_Project/Scripts/Core/Memory/H8Memory.cs:85` |
| `VaultGenerationHandle` | `struct` | `Hecton8.Core.Memory` | 66 | 48 | `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:224` |
| `IGlobalRegistryHotSwapListener` | `interface` | `Hecton8.Core` | 38 | 22 | `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:4114` |
| `ILateFrameTickable` | `interface` | `Hecton8.Core` | 33 | 20 | `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:75` |
| `IUpdatable` | `interface` | `Hecton8.Core` | 20 | 16 | `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:62` |
| `ISlowTickable` | `interface` | `Hecton8.Core` | 17 | 14 | `Assets/_Project/Scripts/ITickable.cs:110` |
| `IPlayerRuntimeContext` | `interface` | `Hecton8.Core` | 12 | 11 | `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:2241` |
| `Result` | `struct` | `Hecton8.Core` | 13 | 10 | `Assets/_Project/Scripts/World/PlanetaryCanvasSmokeTester.cs:14` |
| `CombatDamageSignal` | `struct` | `Hecton8.Core` | 11 | 10 | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1632` |
| `DispatcherTimingDTO` | `struct` | `Hecton8.Core` | 12 | 8 | `Assets/_Project/Scripts/Core/SystemDispatcherContracts.cs:48` |
| `IGlobalRegistryHotSwapRefListener` | `interface` | `Hecton8.Core` | 10 | 8 | `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:4131` |
| `AcousticPingSignal` | `struct` | `Hecton8.Core` | 10 | 8 | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:865` |
| `MockWorldSampler` | `struct` | `Hecton8.VFX.Debris` | 4 | 8 | `Assets/_Project/Scripts/VFX/Debris/ShinobuDeltaCrusherJobs.cs:69` |
| `MockWorldSampler` | `struct` | `Hecton8.AI.Cognition` | 4 | 8 | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainContracts.cs:211` |
| `IDispatcherSystem` | `interface` | `Hecton8.Core` | 10 | 7 | `Assets/_Project/Scripts/Core/SystemDispatcherContracts.cs:213` |
| `IOriginShiftListener` | `interface` | `Hecton8.Core` | 9 | 7 | `Assets/_Project/Scripts/IOriginShiftListener.cs:10` |
| `DebrisSpawnSignal` | `struct` | `Hecton8.Core` | 9 | 7 | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:387` |
| `PlayerRuntimePoseSnapshot` | `struct` | `Hecton8.Core` | 8 | 7 | `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:2113` |
| `ITickDispatcher` | `interface` | `Hecton8.Core` | 8 | 7 | `Assets/_Project/Scripts/ITickable.cs:62` |

## Hot-Path Lookup Findings

- Polling/search findings: `0`
- Registry mutation findings: `2`

| Kind | Method | Assembly | Path |
|---|---|---|---|

### Registry Mutation Notes

| Kind | Method | Assembly | Path |
|---|---|---|---|
| `GlobalRegistry` | `LateFrameTick` | `Hecton8.Core` | `Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:293` |
| `GlobalRegistry` | `LateFrameTick` | `Hecton8.Core` | `Assets/_Project/Scripts/Core/ConnectionSplineBatchRenderer.cs:325` |

## Concrete Cast Findings

- Findings: `1108`
- Direct player concrete coupling findings: `0`
- AI/Physics/Physiology concrete cast findings: `0`
- AI/Physics/Physiology direct player concrete coupling findings: `0`

| Domain | Count |
|---|---:|
| `Hecton8.Gameplay` | 207 |
| `Hecton8.Core` | 182 |
| `Hecton8.UI` | 171 |
| `Hecton8.World` | 153 |
| `Hecton8.Construction` | 38 |
| `Hecton8.Optimization` | 34 |
| `Hecton8.Audio` | 25 |
| `Hecton8.Interaction` | 23 |
| `Hecton8.Visor` | 23 |
| `Hecton8.Systems` | 22 |
| `Hecton8.Graphics` | 21 |
| `Hecton8.Environment` | 20 |

| Kind | Type | Assembly | Path |
|---|---|---|---|
| `as` | `SoundscapeSystem` | `Hecton8.Core` | `Assets/_Project/Scripts/AcousticZoneController.cs:913` |
| `explicit` | `BufferID` | `Hecton8.Core` | `Assets/_Project/Scripts/Animation/KineticCharacter/KineticCharacterAnimatorRuntime.cs:407` |
| `GetComponent` | `Camera` | `Hecton8.Core` | `Assets/_Project/Scripts/Animation/KineticCharacter/KineticCharacterAnimatorRuntime.cs:632` |
| `explicit` | `BufferID` | `Hecton8.Core` | `Assets/_Project/Scripts/Animation/KineticCharacter/KineticCharacterAnimatorRuntime.cs:988` |
| `explicit` | `BufferID` | `Hecton8.Core` | `Assets/_Project/Scripts/Animation/KineticCharacter/KineticCharacterAnimatorRuntime.cs:1036` |
| `as` | `HectonNarrativeDirector` | `Hecton8.Core` | `Assets/_Project/Scripts/AtlasSignal/AtlasSignalSystem.cs:640` |
| `explicit` | `BufferID` | `Hecton8.Core` | `Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsRuntime.cs:1151` |
| `explicit` | `BufferID` | `Hecton8.Core` | `Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsRuntime.cs:1156` |
| `GetComponent` | `AcousticZoneController` | `Hecton8.Core` | `Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs:2499` |
| `as` | `EnvironmentalStrainManager` | `Hecton8.Core` | `Assets/_Project/Scripts/Audio/DeepPsychosisController.cs:341` |
| `as` | `AcousticZoneController` | `Hecton8.Core` | `Assets/_Project/Scripts/Audio/DeepPsychosisController.cs:347` |
| `as` | `AcousticZoneController` | `Hecton8.Core` | `Assets/_Project/Scripts/Audio/HectonMusicDirector.cs:1006` |
| `as` | `DepthZoneDirector` | `Hecton8.Core` | `Assets/_Project/Scripts/Audio/HectonMusicDirector.cs:1014` |
| `as` | `HectonSurfaceWeatherDirector` | `Hecton8.Core` | `Assets/_Project/Scripts/Audio/HectonMusicDirector.cs:1018` |
| `as` | `FirstHourDirector` | `Hecton8.Core` | `Assets/_Project/Scripts/Audio/HectonMusicDirector.cs:1021` |
| `explicit` | `IntPtr` | `Hecton8.Core` | `Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs:258` |
| `explicit` | `IntPtr` | `Hecton8.Core` | `Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs:259` |
| `explicit` | `IntPtr` | `Hecton8.Core` | `Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs:260` |
| `explicit` | `IntPtr` | `Hecton8.Core` | `Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs:261` |
| `explicit` | `ItemAudioMaterialId` | `Hecton8.Core` | `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:4065` |
| `explicit` | `ItemAudioMaterialId` | `Hecton8.Core` | `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:4088` |
| `as` | `SpatialAudioManager` | `Hecton8.Core` | `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:4223` |
| `explicit` | `ItemAudioMaterialId` | `Hecton8.Core` | `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:6092` |
| `explicit` | `ItemAudioMaterialId` | `Hecton8.Core` | `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:6107` |
| `as` | `MonoBehaviour` | `Hecton8.Core` | `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:6318` |
| `explicit` | `GameLanguage` | `Hecton8.Core` | `Assets/_Project/Scripts/AudioLog/AudioLogPickup.cs:316` |
| `GetComponent` | `SubmarineAtmosphereSystem` | `Hecton8.Core` | `Assets/_Project/Scripts/BaseModule.cs:3750` |
| `GetComponent` | `HectonVoxelVolume` | `Hecton8.Core` | `Assets/_Project/Scripts/BaseModule.cs:3756` |
| `GetComponent` | `HectonSurvivalSystem` | `Hecton8.Core` | `Assets/_Project/Scripts/BaseModule.cs:4240` |
| `GetComponent` | `HectonSurvivalSystem` | `Hecton8.Core` | `Assets/_Project/Scripts/BaseModule.cs:4252` |
| `GetComponent` | `HectonSurvivalSystem` | `Hecton8.Core` | `Assets/_Project/Scripts/BaseModule.cs:4304` |
| `as` | `ObjectPoolManager` | `Hecton8.Core` | `Assets/_Project/Scripts/BaseModule.cs:4786` |
| `GetComponent` | `BioReactor` | `Hecton8.Core` | `Assets/_Project/Scripts/BaseModule.cs:5025` |
| `GetComponent` | `SubmarineAtmosphereSystem` | `Hecton8.Core` | `Assets/_Project/Scripts/BaseModule.cs:5482` |
| `as` | `BeaconNetworkSystem` | `Hecton8.Core` | `Assets/_Project/Scripts/BeaconDeployerTool.cs:684` |
| `as` | `ObjectPoolManager` | `Hecton8.Core` | `Assets/_Project/Scripts/BeaconNetworkSystem.cs:142` |
| `is` | `BeaconNetworkSystem` | `Hecton8.Core` | `Assets/_Project/Scripts/BeaconNetworkSystem.cs:157` |
| `as` | `ObjectPoolManager` | `Hecton8.Core` | `Assets/_Project/Scripts/BeaconRuntime.cs:171` |
| `as` | `AbyssalFluidDecalManager` | `Hecton8.Core` | `Assets/_Project/Scripts/BiomeMatrixDirector.cs:922` |
| `as` | `HectonFluidEngine` | `Hecton8.Core` | `Assets/_Project/Scripts/BiomeMatrixDirector.cs:925` |
| `as` | `MapMagicBridge` | `Hecton8.Core` | `Assets/_Project/Scripts/BiomeMatrixDirector.cs:928` |
| `is` | `ModuloSimulationBucketer` | `Hecton8.Core` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:2951` |
| `is` | `BurstTokenBucketJobAdmissionService` | `Hecton8.Core` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:2977` |
| `as` | `Component` | `Hecton8.Core` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:3010` |
| `as` | `SaveManager` | `Hecton8.Core` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:3060` |
| `is` | `EquipmentInteractionHandler` | `Hecton8.Core` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:3125` |
| `is` | `InputDispatcher` | `Hecton8.Core` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:3364` |
| `is` | `PowerGridManager` | `Hecton8.Core` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:3405` |
| `is` | `ConstructionManager` | `Hecton8.Core` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:3421` |
| `is` | `SpatialAudioManager` | `Hecton8.Core` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:3446` |
| `GetComponent` | `Rigidbody` | `Hecton8.Core` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:4270` |
| `GetComponent` | `MonoBehaviour` | `Hecton8.Core` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:4273` |
| `as` | `SaveManager` | `Hecton8.Core` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:4300` |
| `as` | `SaveManager` | `Hecton8.Core` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:4354` |
| `GetComponent` | `Rigidbody` | `Hecton8.Core` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:4661` |
| `explicit` | `BootStateMarker` | `Hecton8.Core` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:5000` |
| `GetComponent` | `T` | `Hecton8.Core` | `Assets/_Project/Scripts/Bootstrap/HectonLoreSystemsRoot.cs:251` |
| `as` | `VRAMPressureMonitor` | `Hecton8.Core` | `Assets/_Project/Scripts/Bootstrap/SceneInstantiationGate.cs:180` |
| `explicit` | `CavePreset` | `Hecton8.Core` | `Assets/_Project/Scripts/CaveTypes.cs:814` |
| `as` | `ResourceNode` | `Hecton8.Core` | `Assets/_Project/Scripts/Construction/AutonomousExtractorSystem.cs:947` |

## Source Using Domain Audit

- Cross-domain using edges: `571`
- Cross-domain using directives: `3635`
- Critical AI/Physics/UI/Audio findings: `0`

| Source Domain | Target Domain | Count |
|---|---|---:|
| `Hecton8.Gameplay` | `Hecton8.Core` | 272 |
| `Hecton8.World` | `Hecton8.Core` | 221 |
| `Hecton8.UI` | `Hecton8.Core` | 159 |
| `Hecton8.Physics` | `Hecton8.Core` | 138 |
| `Hecton8.AI` | `Hecton8.Core` | 105 |
| `Hecton8.Construction` | `Hecton8.Core` | 79 |
| `Hecton8.Gameplay` | `Hecton8.World` | 72 |
| `Hecton8.Visor` | `Hecton8.Core` | 59 |
| `Hecton8.Physiology` | `Hecton8.Core` | 52 |
| `Hecton8.Audio` | `Hecton8.Core` | 45 |
| `Hecton8.World` | `Hecton8.Environment` | 44 |
| `Hecton8.SaveSystem` | `Hecton8.Core` | 43 |
| `Hecton8.Atmosphere` | `Hecton8.Core` | 42 |
| `Hecton8.Power` | `Hecton8.Core` | 41 |
| `Hecton8.VFX` | `Hecton8.Core` | 39 |
| `Hecton8.Gameplay` | `Hecton8.Physics` | 37 |
| `Hecton8.World` | `Hecton8.Gameplay` | 36 |
| `Hecton8.Gameplay` | `Hecton8.Interaction` | 34 |
| `Hecton8.UI` | `Hecton8.Gameplay` | 32 |
| `Hecton8.Tools` | `Hecton8.Core` | 32 |
| `Hecton8.UI` | `Hecton8.World` | 32 |
| `Hecton8.Interaction` | `Hecton8.Core` | 29 |
| `Hecton8.Graphics` | `Hecton8.Core` | 28 |
| `Hecton8.AI` | `Hecton8.World` | 27 |
| `Hecton8.Construction` | `Hecton8.World` | 25 |
| `Hecton8.Physics` | `Hecton8.World` | 24 |
| `Hecton8.Construction` | `Hecton8.Gameplay` | 24 |
| `Hecton8.Gameplay` | `Hecton8.Items` | 22 |
| `Hecton8.Gameplay` | `Hecton8.UI` | 22 |
| `Hecton8.Gameplay` | `Hecton8.Tools` | 22 |

## Source Fully-Qualified Reference Audit

- Cross-domain reference edges: `128`
- Cross-domain references: `1057`
- Critical AI/Physics/UI/Audio findings: `0`

| Source Domain | Target Domain | Count |
|---|---|---:|
| `Hecton8.Gameplay` | `Hecton8.Core` | 132 |
| `Hecton8.UI` | `Hecton8.Core` | 106 |
| `Hecton8.Audio` | `Hecton8.Core` | 63 |
| `Hecton8.Bootstrap` | `Hecton8.Core` | 62 |
| `Hecton8.Dev` | `Hecton8.Core` | 48 |
| `Hecton8.World` | `Hecton8.Core` | 46 |
| `Hecton8.Gameplay` | `Hecton8.Physics` | 28 |
| `Hecton8.Physics` | `Hecton8.Core` | 25 |
| `Hecton8.Construction` | `Hecton8.Physics` | 24 |
| `Hecton8.Gameplay` | `Hecton8.Interaction` | 23 |
| `Hecton8.Interaction` | `Hecton8.Core` | 23 |
| `Hecton8.Construction` | `Hecton8.Core` | 22 |
| `Hecton8.AtlasSignal` | `Hecton8.Core` | 17 |
| `Hecton8.Gameplay` | `Hecton8.World` | 15 |
| `Hecton8.Core` | `Hecton8.UI` | 14 |
| `Hecton8.AI` | `Hecton8.Core` | 13 |
| `Hecton8.Environment` | `Hecton8.Core` | 13 |
| `Hecton8.UI` | `Hecton8.Gameplay` | 13 |
| `Hecton8.World` | `Hecton8.Environment` | 13 |
| `Hecton8.Atmosphere` | `Hecton8.Core` | 12 |
| `Hecton8.Narrative` | `Hecton8.Core` | 12 |
| `Hecton8.Power` | `Hecton8.Core` | 12 |
| `Hecton8.Bootstrap` | `Hecton8.Gameplay` | 10 |
| `Hecton8.Core` | `Hecton8.Physics` | 10 |
| `Hecton8.Core` | `Hecton8.Caves` | 10 |
| `Hecton8.Modding` | `Hecton8.UI` | 10 |
| `Hecton8.PDA` | `Hecton8.UI` | 10 |
| `Hecton8.Construction` | `Hecton8.Power` | 9 |
| `Hecton8.Tools` | `Hecton8.Core` | 9 |
| `Hecton8.PDA` | `Hecton8.Core` | 9 |

## Selected Blast Radius Baseline

| File | Assembly | Before Radius | Direct Inbound | Reaches UI | Reaches Audio |
|---|---|---:|---:|---|---|
| `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainVault.cs` | `Hecton8.AI.Cognition` | 2 | 1 | `False` | `False` |
| `Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault.cs` | `Hecton8.AI.Cognition` | 2 | 1 | `False` | `False` |
| `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs` | `Hecton8.Core` | 108 | 103 | `True` | `True` |
| `Assets/_Project/Scripts/Gameplay/HectonPlayerHealth.cs` | `Hecton8.Core` | 108 | 103 | `True` | `True` |
| `Assets/_Project/Scripts/Gameplay/HectonPlayerState.cs` | `Hecton8.Core` | 108 | 103 | `True` | `True` |
| `Assets/_Project/Scripts/Gameplay/HectonSubmarineOS.cs` | `Hecton8.Core` | 108 | 103 | `True` | `True` |
| `Assets/_Project/Scripts/HectonPlayerMovement.cs` | `Hecton8.Core` | 108 | 103 | `True` | `True` |
| `Assets/_Project/Scripts/Physics/Cable132/CablePhysicsSolver132.cs` | `Hecton8.Physics.Cable132` | 3 | 1 | `False` | `False` |
| `Assets/_Project/Scripts/Physics/HarpoonTensionSolver328.cs` | `Hecton8.Core` | 108 | 103 | `True` | `True` |
| `Assets/_Project/Scripts/Physics/Seaglide/SeaglideHydrodynamicsRuntime.cs` | `Hecton8.Core` | 108 | 103 | `True` | `True` |
| `Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs` | `Hecton8.Physiology` | 2 | 1 | `False` | `False` |
