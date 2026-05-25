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
| `Hecton8.Core.Contracts` | 134 | 98 | 0 | 0 |
| `Hecton8.Core.Memory` | 116 | 75 | 1 | 1 |
| `Hecton8.Bootstrap.Contracts` | 110 | 15 | 0 | 0 |
| `Hecton8.World.Contracts` | 110 | 6 | 1 | 1 |
| `Hecton8.UI.Diegetic.Contracts` | 110 | 2 | 0 | 0 |
| `Hecton8.Habitat.Deformation.Contracts` | 109 | 6 | 0 | 0 |
| `Hecton8.Tools.ToolKinematics.Contracts` | 109 | 3 | 1 | 1 |
| `Hecton8.Audio.Virtualization.Contracts` | 109 | 2 | 1 | 1 |
| `Hecton8.Core.Scheduling` | 109 | 2 | 2 | 2 |
| `Hecton8.Logistics.Grid.Contracts` | 109 | 2 | 1 | 1 |
| `Hecton8.Core.Bucketing` | 109 | 1 | 2 | 2 |
| `Hecton8.Core.Database` | 109 | 1 | 2 | 2 |

## DTO / Interface Extraction Candidates

| Type | Kind | Assembly | External Assemblies | External Domains | Path |
|---|---|---|---:|---:|---|
| `BufferID` | `enum` | `Hecton8.Core.Memory` | 70 | 50 | `Assets/_Project/Scripts/Core/Memory/H8Memory.cs:86` |
| `IDataVault` | `interface` | `Hecton8.Core.Memory` | 73 | 49 | `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:29` |
| `VaultGenerationHandle` | `struct` | `Hecton8.Core.Memory` | 69 | 49 | `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:224` |
| `IGlobalRegistryHotSwapListener` | `interface` | `Hecton8.Core` | 43 | 24 | `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:4210` |
| `ILateFrameTickable` | `interface` | `Hecton8.Core` | 34 | 20 | `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:79` |
| `IUpdatable` | `interface` | `Hecton8.Core` | 20 | 16 | `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:66` |
| `ISlowTickable` | `interface` | `Hecton8.Core` | 17 | 14 | `Assets/_Project/Scripts/ITickable.cs:115` |
| `IPlayerRuntimeContext` | `interface` | `Hecton8.Core` | 12 | 11 | `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:2281` |
| `Result` | `struct` | `Hecton8.Core` | 13 | 10 | `Assets/_Project/Scripts/World/PlanetaryCanvasSmokeTester.cs:14` |
| `CombatDamageSignal` | `struct` | `Hecton8.Core` | 11 | 10 | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1652` |
| `DispatcherTimingDTO` | `struct` | `Hecton8.Core` | 12 | 8 | `Assets/_Project/Scripts/Core/SystemDispatcherContracts.cs:48` |
| `IGlobalRegistryHotSwapRefListener` | `interface` | `Hecton8.Core` | 10 | 8 | `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:4227` |
| `AcousticPingSignal` | `struct` | `Hecton8.Core` | 10 | 8 | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:875` |
| `MockWorldSampler` | `struct` | `Hecton8.VFX.Debris` | 4 | 8 | `Assets/_Project/Scripts/VFX/Debris/ShinobuDeltaCrusherJobs.cs:77` |
| `MockWorldSampler` | `struct` | `Hecton8.AI.Cognition` | 4 | 8 | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainContracts.cs:227` |
| `IDispatcherSystem` | `interface` | `Hecton8.Core` | 10 | 7 | `Assets/_Project/Scripts/Core/SystemDispatcherContracts.cs:213` |
| `IOriginShiftListener` | `interface` | `Hecton8.Core` | 9 | 7 | `Assets/_Project/Scripts/IOriginShiftListener.cs:10` |
| `DebrisSpawnSignal` | `struct` | `Hecton8.Core` | 9 | 7 | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:387` |
| `PlayerRuntimePoseSnapshot` | `struct` | `Hecton8.Core` | 8 | 7 | `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:2153` |
| `ITickDispatcher` | `interface` | `Hecton8.Core` | 8 | 7 | `Assets/_Project/Scripts/ITickable.cs:67` |

## Hot-Path Lookup Findings

- Polling/search findings: `0`
- Registry mutation findings: `2`

| Kind | Method | Assembly | Path |
|---|---|---|---|

### Registry Mutation Notes

| Kind | Method | Assembly | Path |
|---|---|---|---|
| `GlobalRegistry` | `LateFrameTick` | `Hecton8.Core` | `Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:304` |
| `GlobalRegistry` | `LateFrameTick` | `Hecton8.Core` | `Assets/_Project/Scripts/Core/ConnectionSplineBatchRenderer.cs:357` |

## Concrete Cast Findings

- Findings: `1001`
- Direct player concrete coupling findings: `0`
- AI/Physics/Physiology concrete cast findings: `0`
- AI/Physics/Physiology direct player concrete coupling findings: `0`

| Domain | Count |
|---|---:|
| `Hecton8.Gameplay` | 185 |
| `Hecton8.Core` | 177 |
| `Hecton8.UI` | 167 |
| `Hecton8.World` | 142 |
| `Hecton8.Construction` | 33 |
| `Hecton8.Systems` | 22 |
| `Hecton8.Graphics` | 21 |
| `Hecton8.Optimization` | 21 |
| `Hecton8.Visor` | 21 |
| `Hecton8.Bootstrap` | 16 |
| `Hecton8.Power` | 16 |
| `Hecton8.SaveSystem` | 15 |

| Kind | Type | Assembly | Path |
|---|---|---|---|
| `GetComponent` | `Camera` | `Hecton8.Core` | `Assets/_Project/Scripts/Animation/KineticCharacter/KineticCharacterAnimatorRuntime.cs:634` |
| `GetComponent` | `AcousticZoneController` | `Hecton8.Core` | `Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs:2558` |
| `explicit` | `IntPtr` | `Hecton8.Core` | `Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs:258` |
| `explicit` | `IntPtr` | `Hecton8.Core` | `Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs:259` |
| `explicit` | `IntPtr` | `Hecton8.Core` | `Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs:260` |
| `explicit` | `IntPtr` | `Hecton8.Core` | `Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs:261` |
| `as` | `MonoBehaviour` | `Hecton8.Core` | `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:6329` |
| `GetComponent` | `SubmarineAtmosphereSystem` | `Hecton8.Core` | `Assets/_Project/Scripts/BaseModule.cs:3829` |
| `GetComponent` | `HectonVoxelVolume` | `Hecton8.Core` | `Assets/_Project/Scripts/BaseModule.cs:3835` |
| `GetComponent` | `HectonSurvivalSystem` | `Hecton8.Core` | `Assets/_Project/Scripts/BaseModule.cs:4319` |
| `GetComponent` | `HectonSurvivalSystem` | `Hecton8.Core` | `Assets/_Project/Scripts/BaseModule.cs:4331` |
| `GetComponent` | `HectonSurvivalSystem` | `Hecton8.Core` | `Assets/_Project/Scripts/BaseModule.cs:4383` |
| `GetComponent` | `BioReactor` | `Hecton8.Core` | `Assets/_Project/Scripts/BaseModule.cs:5110` |
| `GetComponent` | `SubmarineAtmosphereSystem` | `Hecton8.Core` | `Assets/_Project/Scripts/BaseModule.cs:5567` |
| `as` | `BeaconNetworkSystem` | `Hecton8.Core` | `Assets/_Project/Scripts/BeaconDeployerTool.cs:684` |
| `is` | `BeaconNetworkSystem` | `Hecton8.Core` | `Assets/_Project/Scripts/BeaconNetworkSystem.cs:157` |
| `is` | `ModuloSimulationBucketer` | `Hecton8.Core` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:2993` |
| `is` | `BurstTokenBucketJobAdmissionService` | `Hecton8.Core` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:3019` |
| `as` | `Component` | `Hecton8.Core` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:3052` |
| `as` | `SaveManager` | `Hecton8.Core` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:3102` |
| `is` | `EquipmentInteractionHandler` | `Hecton8.Core` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:3167` |
| `is` | `InputDispatcher` | `Hecton8.Core` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:3406` |
| `is` | `PowerGridManager` | `Hecton8.Core` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:3447` |
| `is` | `ConstructionManager` | `Hecton8.Core` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:3463` |
| `is` | `SpatialAudioManager` | `Hecton8.Core` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:3488` |
| `GetComponent` | `Rigidbody` | `Hecton8.Core` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:4312` |
| `GetComponent` | `MonoBehaviour` | `Hecton8.Core` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:4315` |
| `as` | `SaveManager` | `Hecton8.Core` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:4342` |
| `as` | `SaveManager` | `Hecton8.Core` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:4396` |
| `GetComponent` | `Rigidbody` | `Hecton8.Core` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:4703` |
| `explicit` | `BootStateMarker` | `Hecton8.Core` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:5042` |
| `GetComponent` | `T` | `Hecton8.Core` | `Assets/_Project/Scripts/Bootstrap/HectonLoreSystemsRoot.cs:251` |
| `explicit` | `CavePreset` | `Hecton8.Core` | `Assets/_Project/Scripts/CaveTypes.cs:820` |
| `as` | `ResourceNode` | `Hecton8.Core` | `Assets/_Project/Scripts/Construction/AutonomousExtractorSystem.cs:1262` |
| `GetComponent` | `ResourceNode` | `Hecton8.Core` | `Assets/_Project/Scripts/Construction/AutonomousExtractorSystem.cs:1364` |
| `GetComponent` | `SubmarineAtmosphereSystem` | `Hecton8.Core` | `Assets/_Project/Scripts/Construction/BatteryBankModule.cs:192` |
| `GetComponent` | `BaseModule` | `Hecton8.Core` | `Assets/_Project/Scripts/Construction/BotanyPlanterModule.cs:48` |
| `is` | `UnauthorizedAccessException` | `Hecton8.Core` | `Assets/_Project/Scripts/Construction/BulkheadContainmentRuntime.cs:1735` |
| `is` | `ArgumentException` | `Hecton8.Core` | `Assets/_Project/Scripts/Construction/BulkheadContainmentRuntime.cs:1736` |
| `is` | `NotSupportedException` | `Hecton8.Core` | `Assets/_Project/Scripts/Construction/BulkheadContainmentRuntime.cs:1737` |
| `GetComponent` | `BaseModule` | `Hecton8.Core` | `Assets/_Project/Scripts/Construction/CultivationManager.cs:172` |
| `explicit` | `SubmarineEmergencyLevel` | `Hecton8.Core` | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:332` |
| `as` | `ConstructionManager` | `Hecton8.Core` | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:1509` |
| `is` | `FaunaBrain` | `Hecton8.Core` | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:4478` |
| `GetComponent` | `SubmarineAtmosphereSystem` | `Hecton8.Core` | `Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs:785` |
| `explicit` | `IntegrityFailureReasonCode` | `Hecton8.Core` | `Assets/_Project/Scripts/Construction/HabitatConstructionManager.cs:392` |
| `is` | `SocketKey` | `Hecton8.Core` | `Assets/_Project/Scripts/Construction/HabitatConstructionManager.cs:1466` |
| `explicit` | `LogisticsModuleStatusBits` | `Hecton8.Core` | `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:4625` |
| `is` | `SocketKey` | `Hecton8.Core` | `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:5193` |
| `GetComponent` | `PowerNode` | `Hecton8.Core` | `Assets/_Project/Scripts/Construction/LogisticsPipeNode.cs:133` |
| `GetComponent` | `SubmarineAtmosphereSystem` | `Hecton8.Core` | `Assets/_Project/Scripts/Construction/LogisticsPipeNode.cs:134` |
| `GetComponent` | `SubmarineAtmosphereSystem` | `Hecton8.Core` | `Assets/_Project/Scripts/Construction/LogisticsPipeNode.cs:143` |
| `as` | `PersistentWorldRegistry` | `Hecton8.Core` | `Assets/_Project/Scripts/Construction/LogisticsPipeNode.cs:243` |
| `GetComponent` | `StorageCrate` | `Hecton8.Core` | `Assets/_Project/Scripts/Construction/RepairDroneHub.cs:422` |
| `GetComponent` | `BaseAirlock` | `Hecton8.Core` | `Assets/_Project/Scripts/Construction/RepairDroneHub.cs:857` |
| `GetComponent` | `PowerNode` | `Hecton8.Core` | `Assets/_Project/Scripts/Construction/VehicleDockingModule.cs:244` |
| `GetComponent` | `BaseModule` | `Hecton8.Core` | `Assets/_Project/Scripts/Construction/VehicleDockingModule.cs:245` |
| `GetComponent` | `Rigidbody` | `Hecton8.Core` | `Assets/_Project/Scripts/Construction/VehicleDockingModule.cs:522` |
| `GetComponent` | `Rigidbody` | `Hecton8.Core` | `Assets/_Project/Scripts/Construction/VehicleDockingModule.cs:616` |
| `GetComponent` | `VehicleMotor` | `Hecton8.Core` | `Assets/_Project/Scripts/Construction/VehicleDockingModule.cs:641` |

## Source Using Domain Audit

- Cross-domain using edges: `568`
- Cross-domain using directives: `3647`
- Critical AI/Physics/UI/Audio findings: `0`

| Source Domain | Target Domain | Count |
|---|---|---:|
| `Hecton8.Gameplay` | `Hecton8.Core` | 277 |
| `Hecton8.World` | `Hecton8.Core` | 226 |
| `Hecton8.UI` | `Hecton8.Core` | 163 |
| `Hecton8.Physics` | `Hecton8.Core` | 139 |
| `Hecton8.AI` | `Hecton8.Core` | 105 |
| `Hecton8.Construction` | `Hecton8.Core` | 80 |
| `Hecton8.Gameplay` | `Hecton8.World` | 72 |
| `Hecton8.Visor` | `Hecton8.Core` | 60 |
| `Hecton8.Physiology` | `Hecton8.Core` | 52 |
| `Hecton8.Audio` | `Hecton8.Core` | 45 |
| `Hecton8.World` | `Hecton8.Environment` | 44 |
| `Hecton8.SaveSystem` | `Hecton8.Core` | 43 |
| `Hecton8.Atmosphere` | `Hecton8.Core` | 42 |
| `Hecton8.Power` | `Hecton8.Core` | 41 |
| `Hecton8.VFX` | `Hecton8.Core` | 39 |
| `Hecton8.Gameplay` | `Hecton8.Physics` | 38 |
| `Hecton8.World` | `Hecton8.Gameplay` | 36 |
| `Hecton8.Gameplay` | `Hecton8.Interaction` | 34 |
| `Hecton8.UI` | `Hecton8.Gameplay` | 32 |
| `Hecton8.Tools` | `Hecton8.Core` | 32 |
| `Hecton8.UI` | `Hecton8.World` | 32 |
| `Hecton8.Interaction` | `Hecton8.Core` | 29 |
| `Hecton8.AI` | `Hecton8.World` | 27 |
| `Hecton8.Graphics` | `Hecton8.Core` | 27 |
| `Hecton8.Optimization` | `Hecton8.Core` | 26 |
| `Hecton8.Construction` | `Hecton8.World` | 25 |
| `Hecton8.Physics` | `Hecton8.World` | 24 |
| `Hecton8.Construction` | `Hecton8.Gameplay` | 24 |
| `Hecton8.Gameplay` | `Hecton8.Items` | 22 |
| `Hecton8.Gameplay` | `Hecton8.UI` | 22 |

## Source Fully-Qualified Reference Audit

- Cross-domain reference edges: `131`
- Cross-domain references: `1206`
- Critical AI/Physics/UI/Audio findings: `0`

| Source Domain | Target Domain | Count |
|---|---|---:|
| `Hecton8.Gameplay` | `Hecton8.Core` | 151 |
| `Hecton8.UI` | `Hecton8.Core` | 108 |
| `Hecton8.Dev` | `Hecton8.Core` | 90 |
| `Hecton8.World` | `Hecton8.Core` | 77 |
| `Hecton8.Bootstrap` | `Hecton8.Core` | 67 |
| `Hecton8.Audio` | `Hecton8.Core` | 66 |
| `Hecton8.Gameplay` | `Hecton8.Physics` | 28 |
| `Hecton8.Physics` | `Hecton8.Core` | 25 |
| `Hecton8.Construction` | `Hecton8.Physics` | 24 |
| `Hecton8.Gameplay` | `Hecton8.Interaction` | 23 |
| `Hecton8.Construction` | `Hecton8.Core` | 23 |
| `Hecton8.Interaction` | `Hecton8.Core` | 23 |
| `Hecton8.Modding` | `Hecton8.Core` | 20 |
| `Hecton8.Tools` | `Hecton8.Core` | 20 |
| `Hecton8.AtlasSignal` | `Hecton8.Core` | 17 |
| `Hecton8.AI` | `Hecton8.Core` | 15 |
| `Hecton8.Gameplay` | `Hecton8.World` | 15 |
| `Hecton8.Atmosphere` | `Hecton8.Core` | 14 |
| `Hecton8.Narrative` | `Hecton8.Core` | 14 |
| `Hecton8.Environment` | `Hecton8.Core` | 14 |
| `Hecton8.Core` | `Hecton8.UI` | 14 |
| `Hecton8.UI` | `Hecton8.Gameplay` | 13 |
| `Hecton8.World` | `Hecton8.Environment` | 13 |
| `Hecton8.Power` | `Hecton8.Core` | 12 |
| `Hecton8.SaveSystem` | `Hecton8.Core` | 11 |
| `Hecton8.Bootstrap` | `Hecton8.Gameplay` | 10 |
| `Hecton8.Core` | `Hecton8.Physics` | 10 |
| `Hecton8.Core` | `Hecton8.Caves` | 10 |
| `Hecton8.Modding` | `Hecton8.UI` | 10 |
| `Hecton8.PDA` | `Hecton8.UI` | 10 |

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
