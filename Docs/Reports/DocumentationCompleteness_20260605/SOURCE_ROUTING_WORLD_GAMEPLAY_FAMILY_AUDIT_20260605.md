# Source Routing World Gameplay Family Audit

Date: 2026-06-05
Worker: Source Routing Audit Worker K
Status: PENDING VERIFICATION
Evidence class: STATIC_SOURCE / STATIC_DOC only
Write scope: this report only

## Evidence Boundary

This audit used static source listing, static source snippets, and static documentation reads only.

No Unity import, Unity Console, Play Mode, dotnet build, tests, profiler, GCMonitor, Memory Profiler, Frame Debugger, RenderDoc, shader import, scene validation, asset mutation, player build, or platform run was executed.

Static text proves text/source presence only. It does not prove compile health, runtime behavior, scene wiring, visual quality, GC, frame time, save/load, memory safety, player route behavior, or platform readiness.

First-20 route effect: this report removes a documentation routing blocker for world/gameplay/physics/survival-adjacent source families. It does not improve the route in-game and cannot claim first-20 route readiness.

## Mandates And Docs Read

Mandates loaded:

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/ARCH_Execution_Phases.txt`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/STRM_World_Streaming_Residency_Chunk_Management.txt`
- `.agents-skills/PHYS_Physics_Integrity_Determinism_ForceMode.txt`
- `.agents-skills/CORE_Abyss_Survival_Systems_O2_Pressure_Logic.txt`
- `.agents-skills/CORE_Submarine_Vehicles_Kinematics_AUP.txt`
- `.agents-skills/CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`

Stable and report docs read:

- `AGENTS.md`
- `Docs/Reports/DocumentationCompleteness_20260605/SOURCE_COVERAGE_REALITY_AUDIT_3223_20260605.md`
- `Docs/ARCHITECTURE/SOURCE_SYSTEMS_REALITY_MAP.md`
- `Docs/ARCHITECTURE/DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md`
- `world.md`
- `gameplay.md`
- `physics.md`
- `survival.md`
- `player.md`
- `vehicles.md`
- `construction.md`
- `inventory.md`
- `tools.md`
- `water.md`
- `terrain.md`
- `atmosphere.md`

## Method

Exact anchor rule used in this report:

- A covered anchor means the full normalized relative path string appears in `SOURCE_SYSTEMS_REALITY_MAP.md` or `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md`.
- Example counted form: `Assets/_Project/Scripts/World/TerrainChunkPagerRuntime.cs`.
- Short forms such as `World/Biomes/BiomeBoundarySdfRuntime.cs` were not counted as exact relative-path coverage.
- Folder anchors were not counted as exact source-owner anchors.

Commands / probes used:

- `rg --files Assets/_Project/Scripts -g '*.cs'`
- PowerShell grouping by requested folders.
- PowerShell full-path text comparison against the two routing docs.
- PowerShell loose-root family matching for `World`, `Player`, `Base`, `Module`, `Voxel`, `Cave`, `Thermal`, `Crafting`, `Fabricator`, `Submarine`, `Biome`, and `Tool`.
- `Select-String` source probes for namespaces, classes, `SystemID`, DataVault, Signal, job, and quality-weight markers in high-risk missing anchors.

## Folder Counts And Exact Anchor Coverage

| Folder | Scripts | Exact anchors in Source Map | Exact anchors in Matrix | Exact anchors in either | Missing exact anchors |
|---|---:|---:|---:|---:|---:|
| `Assets/_Project/Scripts/World` | 282 | 5 | 22 | 23 | 259 |
| `Assets/_Project/Scripts/Gameplay` | 169 | 0 | 18 | 18 | 151 |
| `Assets/_Project/Scripts/Physics` | 85 | 0 | 11 | 11 | 74 |
| `Assets/_Project/Scripts/Physiology` | 39 | 3 | 3 | 3 | 36 |
| `Assets/_Project/Scripts/Player` | 4 | 0 | 0 | 0 | 4 |
| `Assets/_Project/Scripts/Vehicles` | 11 | 0 | 1 | 1 | 10 |
| `Assets/_Project/Scripts/Construction` | 74 | 0 | 8 | 8 | 66 |
| `Assets/_Project/Scripts/Interaction` | 27 | 0 | 8 | 8 | 19 |
| `Assets/_Project/Scripts/Inventory` | 17 | 0 | 0 | 0 | 17 |
| `Assets/_Project/Scripts/Tools` | 31 | 0 | 0 | 0 | 31 |
| `Assets/_Project/Scripts/Power` | 22 | 0 | 5 | 5 | 17 |
| `Assets/_Project/Scripts/Scavenging` | 4 | 0 | 0 | 0 | 4 |
| `Assets/_Project/Scripts/Environment` | 9 | 0 | 1 | 1 | 8 |
| `Assets/_Project/Scripts/Atmosphere` | 26 | 0 | 5 | 5 | 21 |
| `Assets/_Project/Scripts/Thermodynamics` | 15 | 0 | 3 | 3 | 12 |
| Total inspected folders | 815 | 8 | 85 | 86 | 729 |

Interpretation:

- The requested folder set has live source coverage but sparse exact full-path routing coverage.
- `Inventory`, `Tools`, `Scavenging`, and `Player` have zero full-path anchors in the two routing docs.
- Some missing rows are mentioned in shortened form elsewhere. That is still not exact source-owner routing.

## Exact Anchors Already Covered

These are full relative paths found in at least one of the two routing docs.

`World`:

- `Assets/_Project/Scripts/World/EcosystemDirector.cs`
- `Assets/_Project/Scripts/World/FaunaSpatialHashRegistry.cs`
- `Assets/_Project/Scripts/World/HectonAnomalyEngine.cs`
- `Assets/_Project/Scripts/World/HectonHLODRenderer.cs`
- `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs`
- `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs`
- `Assets/_Project/Scripts/World/HectonOctahedralImpostorRenderer.cs`
- `Assets/_Project/Scripts/World/HectonVoxelStreamingBridge.cs`
- `Assets/_Project/Scripts/World/LODSystemManager.cs`
- `Assets/_Project/Scripts/World/OfflineHadalArchBaker/Editor/HadalArchBakePipeline.cs`
- `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchBakePipeline.cs`
- `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs`
- `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs`
- `Assets/_Project/Scripts/World/ResourceDistributionDirector.cs`
- `Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs`
- `Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs`
- `Assets/_Project/Scripts/World/SargassumCutManager.cs`
- `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs`
- `Assets/_Project/Scripts/World/TerrainChunkPagerRuntime.cs`
- `Assets/_Project/Scripts/World/VegetationChunkResidencyDirector.cs`
- `Assets/_Project/Scripts/World/VegetationPredatorFearField.cs`
- `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs`
- `Assets/_Project/Scripts/World/WorldReadabilityDirector.cs`

`Gameplay`:

- `Assets/_Project/Scripts/Gameplay/Combat/BallisticsRuntime.cs`
- `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs`
- `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime_StatusEffects.cs`
- `Assets/_Project/Scripts/Gameplay/Combat/HectonCombatRuntime_ArmorPenetration.cs`
- `Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs`
- `Assets/_Project/Scripts/Gameplay/Loot/LootMagnetSystem.cs`
- `Assets/_Project/Scripts/Gameplay/MessageTerminal.cs`
- `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs`
- `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime_HandIK.cs`
- `Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs`
- `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs`
- `Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs`
- `Assets/_Project/Scripts/Gameplay/ToxinHazard.cs`
- `Assets/_Project/Scripts/Gameplay/TraumaDispatcher.cs`
- `Assets/_Project/Scripts/Gameplay/VehicleMotor.cs`
- `Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs`
- `Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs`
- `Assets/_Project/Scripts/Gameplay/VRSomaticProvider.HorizonLock.cs`

`Physics`:

- `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsRuntime.cs`
- `Assets/_Project/Scripts/Physics/HabitatFluidIncursionDirector.cs`
- `Assets/_Project/Scripts/Physics/HarpoonTensionSolver328.cs`
- `Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs`
- `Assets/_Project/Scripts/Physics/Seaglide/SeaglideHydrodynamicsRuntime.cs`
- `Assets/_Project/Scripts/Physics/TetherAupVerletJobs.cs`
- `Assets/_Project/Scripts/Physics/Vehicles/Automation/DockingAutopilotService.cs`
- `Assets/_Project/Scripts/Physics/Vehicles/SubmarineBallastBuoyancyContracts.cs`
- `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs`
- `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime_Gyroscopes.cs`
- `Assets/_Project/Scripts/Physics/Vehicles/VehicleComponentDamageRuntime.cs`

Other covered anchors:

- `Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs`
- `Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs`
- `Assets/_Project/Scripts/Physiology/ShinobuSuitIntegrityRuntime.cs`
- `Assets/_Project/Scripts/Vehicles/VFX/HullDentShaderController.cs`
- `Assets/_Project/Scripts/Construction/BulkheadContainmentRuntime.cs`
- `Assets/_Project/Scripts/Construction/BulkheadContainmentRuntime_HatchLocks.cs`
- `Assets/_Project/Scripts/Construction/DroneFleetManager.cs`
- `Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs`
- `Assets/_Project/Scripts/Construction/HabitatConstructionManager.cs`
- `Assets/_Project/Scripts/Construction/HabitatDeconstructionTransactionKernel.cs`
- `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs`
- `Assets/_Project/Scripts/Construction/SumpPumpPipeGridRuntime.cs`
- `Assets/_Project/Scripts/Interaction/EquipmentInteractionContracts.cs`
- `Assets/_Project/Scripts/Interaction/EquipmentInteractionHandler.cs`
- `Assets/_Project/Scripts/Interaction/InteractableRegistry.cs`
- `Assets/_Project/Scripts/Interaction/PhysicalHandController.cs`
- `Assets/_Project/Scripts/Interaction/PhysicalInteractionHandler.cs`
- `Assets/_Project/Scripts/Interaction/PlayerInteraction.cs`
- `Assets/_Project/Scripts/Interaction/VRInteractionKinematicBridge.cs`
- `Assets/_Project/Scripts/Interaction/VRLeakPatchWeldTarget.cs`
- `Assets/_Project/Scripts/Power/BatteryChargerLogistics/BatteryChargerLogisticsRuntime.cs`
- `Assets/_Project/Scripts/Power/Generators/RadioisotopeThermalGenerator.cs`
- `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs`
- `Assets/_Project/Scripts/Power/PowerRelayNode.cs`
- `Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs`
- `Assets/_Project/Scripts/Environment/GlobalWeatherDirector.cs`
- `Assets/_Project/Scripts/Atmosphere/BaseAtmosphereEngine.cs`
- `Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsRuntime.cs`
- `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs`
- `Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs`
- `Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs`
- `Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsSolver.cs`
- `Assets/_Project/Scripts/Thermodynamics/ReactorThermalGridJobs.cs`
- `Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.cs`

## Loose Root Family Counts

The loose root scan searched direct files under `Assets/_Project/Scripts/*.cs`. Counts can overlap when one filename matches multiple families.

Unique loose-root candidates across the requested family names: 165.

| Loose-root family | Matching scripts | Exact anchors in Source Map | Exact anchors in Matrix | Exact anchors in either | Missing exact anchors |
|---|---:|---:|---:|---:|---:|
| `World` | 70 | 2 | 4 | 5 | 65 |
| `Player` | 12 | 0 | 2 | 2 | 10 |
| `Base` | 4 | 0 | 2 | 2 | 2 |
| `Module` | 8 | 0 | 2 | 2 | 6 |
| `Voxel` | 10 | 0 | 3 | 3 | 7 |
| `Cave` | 13 | 1 | 0 | 1 | 12 |
| `Thermal` | 4 | 0 | 0 | 0 | 4 |
| `Crafting` | 4 | 0 | 1 | 1 | 3 |
| `Fabricator` / `Fabrication` | 7 | 0 | 1 | 1 | 6 |
| `Submarine` | 4 | 0 | 0 | 0 | 4 |
| `Biome` | 17 | 0 | 0 | 0 | 17 |
| `Tool` | 23 | 0 | 0 | 0 | 23 |

Loose-root covered anchors:

- `Assets/_Project/Scripts/HectonWorldGenerator.cs`
- `Assets/_Project/Scripts/WorldGeneratedPrimitiveFactory.cs`
- `Assets/_Project/Scripts/WorldGenerativeGeologyTerrainSeamApplier.cs`
- `Assets/_Project/Scripts/WorldGenerativeGeologyVoxelBridgeDirector.cs`
- `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs`
- `Assets/_Project/Scripts/PlayerInventory.cs`
- `Assets/_Project/Scripts/PlayerInventory_SoaQuery.cs`
- `Assets/_Project/Scripts/BaseModule.cs`
- `Assets/_Project/Scripts/BaseModuleTemplate.cs`
- `Assets/_Project/Scripts/HectonVoxelEngine.cs`
- `Assets/_Project/Scripts/VoxelDeltaProcessor.cs`
- `Assets/_Project/Scripts/CaveGraphGenerator.cs`
- `Assets/_Project/Scripts/CraftingSystem.cs`
- `Assets/_Project/Scripts/Fabricator.cs`

High-risk loose-root exact gaps include:

- `Assets/_Project/Scripts/SeafloorDrillTool.cs`
- `Assets/_Project/Scripts/PlayerTool.cs`
- `Assets/_Project/Scripts/PlayerToolManager.cs`
- `Assets/_Project/Scripts/HectonPlayerMovement.cs`
- `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs`
- `Assets/_Project/Scripts/SubmarineFluidDynamics.cs`
- `Assets/_Project/Scripts/SubmarineStructuralGrid.cs`
- `Assets/_Project/Scripts/WorldContentDirector.cs`
- `Assets/_Project/Scripts/WorldGenerativeGeologyIntegrationDirector.cs`
- `Assets/_Project/Scripts/WorldInterestDirector.cs`
- `Assets/_Project/Scripts/WorldPopulationDirector.cs`
- `Assets/_Project/Scripts/HectonBiomeRegistry.cs`
- `Assets/_Project/Scripts/BiomeMatrixDirector.cs`
- `Assets/_Project/Scripts/CraftingSystem.FastFail.cs`
- `Assets/_Project/Scripts/Fabricator.FastFail.cs`
- `Assets/_Project/Scripts/FabricatorPhysicalActuator.cs`
- `Assets/_Project/Scripts/ThermalUpdraftVolume.cs`
- `Assets/_Project/Scripts/GravityTetherTool.cs`
- `Assets/_Project/Scripts/HarpoonLauncherTool.cs`
- `Assets/_Project/Scripts/RepairTool.cs`
- `Assets/_Project/Scripts/ScannerTool.cs`

## Top 25 Missing Exact Anchors By Risk

All owner-bible assignments below are `CANDIDATE` unless the source snippet directly exposed a namespace or `SystemID`. Source-supported facts are still STATIC_SOURCE only.

| # | Missing exact relative path | Static risk basis | Likely owner bible | Required proof class |
|---:|---|---|---|---|
| 1 | `Assets/_Project/Scripts/World/ShinobuStreamingRuntime.cs` | Source contains chunk residency DTOs, addressable request DTOs, HLOD/impostor DTOs, jobs, and `NativeArray` chunk state. Shortened docs mention it, but full path is not exact anchored. | CANDIDATE: `world.md`, `terrain.md`, `water.md` | UNITY_CONSOLE, PLAYMODE, PROFILER, GCMonitor, VRAM/memory, Addressables release proof, route streaming capture |
| 2 | `Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs` | Source says double-buffered voxel passability snapshots from cave SDF density, `SystemID.WorldStreaming`, DataVault buffer ids, native pools, profiler markers. | CANDIDATE: `terrain.md`, `world.md`, `physics.md` | Unity import, navgrid/SDF route run, deterministic path proof, profiler/GC, black-box/dump proof |
| 3 | `Assets/_Project/Scripts/World/Biomes/BiomeBoundarySdfRuntime.cs` | Source is `MonoBehaviour`, `ISlowTickable`, origin-shift listener, hot-swap listener, DataVault biome map/hash/telemetry buffers. | CANDIDATE: `terrain.md`, `world.md`, `atmosphere.md` | Unity scene wiring, biome boundary capture, route readability capture, profiler/GC, DataVault handle audit |
| 4 | `Assets/_Project/Scripts/World/VegetationMemorySovereigntyRuntime.cs` | Source is a partial `HectonMapMagicVegetationBridge`, caches DataVault, allocates telemetry dump payload, owns vegetation memory telemetry buffers. | CANDIDATE: `world.md`, `terrain.md`, `water.md` | MapMagic bridge audit, Unity import, vegetation runtime capture, memory/profiler/GC, DataVault relocation proof |
| 5 | `Assets/_Project/Scripts/World/ScatterHybridRuntimeEntryPoint.cs` | Source selects scatter backend plans and backend kinds. Scatter ownership impacts world density, route readability, perf, and deterministic scatter routing. | CANDIDATE: `world.md`, `terrain.md` | Backend parity run, compact/high visual capture, profiler/GC, scatter budget proof |
| 6 | `Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageVault.cs` | Source exposes DataVault handles and `NativeArray` buffers for wreck rules, grid, nodes, render matrices, loot requests, collision proxies, telemetry, tuning, CSV scratch. | CANDIDATE: `world.md`, `terrain.md`, `construction.md`, `inventory.md` | Offline bake artifact, Unity import, collision/proxy proof, loot route proof, save/load proof, profiler/GC |
| 7 | `Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyRuntime.cs` | Source is `IColdTickable`, `IUpdatable`, `ILateFrameTickable`, `ISlowTickable`, hot-swap listener; owns `SystemID.EndgameAnomaly`, many signal pins, telemetry ring. | CANDIDATE: `world.md`, `gameplay.md`, `atmosphere.md`, `survival.md` | Unity route run, radiation/radar/glitch gameplay proof, black-box dump, profiler/GC, save/load if state persists |
| 8 | `Assets/_Project/Scripts/Gameplay/AirlockPressurization/AirlockPressurizationRuntime.cs` | Source declares DataVault descriptors, VFX/acoustic signal handles, transient `NativeArray` views, jobs. Airlock pressure is first-20 survival/construction route relevant. | CANDIDATE: `gameplay.md`, `survival.md`, `construction.md`, `physics.md`, `water.md` | Play Mode airlock route, pressure/oxygen proof, save/load, UI/audio/VFX capture, profiler/GC |
| 9 | `Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs` | Source implements motor, seat lock, post-fixed, late-frame, inventory listener, hot-swap listener, DataVault service reaction, KCC velocity bridge. It can overlap player truth with KCC. | CANDIDATE: `player.md`, `gameplay.md`, `vehicles.md`, `physics.md` | Movement capture, controller matrix, KCC/vehicle handoff proof, profiler/GC, no hot registry audit |
| 10 | `Assets/_Project/Scripts/Gameplay/Hazards/ThermalVentRuntime.cs` | Source is `ILateFrameTickable` and reads `HomeostasisBrain.GlobalQualityWeight`; thermal hazards affect survival/atmosphere route truth. | CANDIDATE: `survival.md`, `atmosphere.md`, `gameplay.md`, `water.md` | Hazard readability capture, survival coupling proof, profiler/GC, quality-weight no-truth-change audit |
| 11 | `Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackRuntime.cs` | Source owns graphics buffers, DataVault, async readback state, shader ids, `NativeArray` readback data owners. GPU readback is high overclaim risk. | CANDIDATE: `physics.md`, `water.md`, `vehicles.md`, `rendering.md` | Frame Debugger/RenderGraph, AsyncGPUReadback runtime proof, profiler/GC, no synchronous readback audit |
| 12 | `Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementRuntime.cs` | Source is `IFixedTickable`, `IPostFixedTickable`, `ILateFrameTickable`, hot-swap/origin-shift listener, owns force packets, flow samples, telemetry, sleep telemetry, SDF density/config. | CANDIDATE: `physics.md`, `water.md`, `vehicles.md` | Force apply route proof, buoyancy gameplay run, profiler/GC, black-box, NaN/fallback proof |
| 13 | `Assets/_Project/Scripts/Physics/Cable132/CablePhysics132Service.cs` | Source bridges DataVault to cable schedule/fault dump services and takes `globalQualityWeight`. Tether/cable truth is physics-critical. | CANDIDATE: `physics.md`, `tools.md`, `vehicles.md` | Tether/cable gameplay proof, force ownership audit, black-box dump, profiler/GC, quality-weight cadence proof |
| 14 | `Assets/_Project/Scripts/Physics/Cavitation/AbyssalCavitationRuntime.cs` | Source owns `SystemID.VehiclesPhysics`, DataVault buffers for shockwave events/counters, entity snapshots, force packets, transport packets, visuals, telemetry, tuning, SDF descriptor/voxels. | CANDIDATE: `physics.md`, `vehicles.md`, `water.md` | Vehicle cavitation route, force packet proof, audio/VFX capture, profiler/GC, black-box |
| 15 | `Assets/_Project/Scripts/Physiology/ShinobuRadiationMutationRuntime.cs` | Source owns `SystemID.GameplayPlayer`, slow/late ticks, hot-swap, mutation state/tuning/telemetry/mock dose buffers, signal push drop count. | CANDIDATE: `survival.md`, `gameplay.md`, `atmosphere.md` | Radiation scenario, survival channel tests, save/load, UI warning capture, profiler/GC, black-box |
| 16 | `Assets/_Project/Scripts/Physiology/ShinobuRespawnReconciliationRuntime.cs` | Source owns respawn state/request/medical bay/fade/telemetry/tuning/penalty buffers and reads physiology vitals. Death recovery is high product risk. | CANDIDATE: `survival.md`, `player.md`, `gameplay.md`, `persistence.md` | Death/faint/respawn route, save/load recovery proof, UI/camera capture, profiler/GC, black-box |
| 17 | `Assets/_Project/Scripts/Player/Movement/ZeroGMovementRuntime.cs` | Source is `IFixedTickable`, `IPostFixedTickable`, `ILateFrameTickable`, hot-swap listener, DataVault guard masks, direct signal drop count, quality weight field. Player folder has zero exact anchors. | CANDIDATE: `player.md`, `physics.md`, `vehicles.md` | Movement capture, controller/device matrix, profiler/GC, DataVault/signal audit, compact readability |
| 18 | `Assets/_Project/Scripts/Vehicles/DropPod/DropPodAirlockController.cs` | Source is fixed/late tick, uses typed signals, command queues, physical hand paths, fail-closed status. Vehicles folder has one exact anchor only. | CANDIDATE: `vehicles.md`, `player.md`, `tools.md`, `survival.md` | Drop pod transfer capture, airlock failure proof, interaction proof, profiler/GC, save/load if persistent |
| 19 | `Assets/_Project/Scripts/Construction/SumpPumpPipeGridJobs.cs` | Source has CSR pipe graph jobs, drainage pointer comments, Vault locking invariants, scheduling invariant, external power/fluid snapshots. | CANDIDATE: `construction.md`, `water.md`, `physics.md` | Flood/drain gameplay proof, pipe graph route, power/fluid coupling, profiler/GC, save/load |
| 20 | `Assets/_Project/Scripts/Inventory/SoaInventoryQueryEngine.cs` | Source defines inventory query/mutation/capacity/telemetry DTOs, DataVault lanes/handles/buffers, and `GlobalQualityWeight` field. Inventory folder has zero exact anchors. | CANDIDATE: `inventory.md`, `construction.md` | Inventory query tests, UI snapshot, save/load, no string/hot allocation proof, profiler/GC |
| 21 | `Assets/_Project/Scripts/Tools/LaserCutterDodRuntime.cs` | Source is static runtime, owns DataVault handles, scheduled SDF/evaluation jobs, signal drop count, cached quality weight, transient/scheduler buffer guards. Tools folder has zero exact anchors. | CANDIDATE: `tools.md`, `physics.md`, `terrain.md` | Tool target route, SDF cut proof, VFX/audio/haptic capture, profiler/GC, save/world scar proof |
| 22 | `Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs` | Source includes power solver convergence math, continuous `GlobalQualityWeight` iteration/tolerance/sample-mask scaling, grid/node/edge/anchor DTOs. | CANDIDATE: `vehicles.md`, `construction.md`, `atmosphere.md`, `survival.md` | Power/thermal scenario, solver convergence artifact, profiler/GC, save/load, no truth change by quality |
| 23 | `Assets/_Project/Scripts/Atmosphere/StormPropagation/ShinobuStormPropagationRuntime.cs` | Source is `IUpdatable`, `ILateFrameTickable`, `SystemID.HabitatAtmosphere`, DataVault, staging `NativeArray`s, weather/tuning/profile/mock/write/telemetry buffers, jobs. | CANDIDATE: `atmosphere.md`, `water.md`, `world.md` | Storm route capture, depth impact proof, profiler/GC/GPU if visual, save/load if persistent |
| 24 | `Assets/_Project/Scripts/Environment/Fluids/OceanAdapterVaultRoute.cs` | Source declares ocean adapter DataVault lanes, buffer ids `72960` through `72965`, `SystemID.Fluid`, boot handles. Water bridge truth risk is high. | CANDIDATE: `water.md`, `terrain.md`, `physics.md`, `atmosphere.md` | Bridge route audit, Unity import, water sample correctness, profiler/GC, Frame Debugger if rendering affected |
| 25 | `Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.FileWorker.cs` | Source is partial hazard grid runtime with file IO, CSV worker bytes, `FileStream`, and config parsing. Runtime config/file-worker boundaries need exact routing. | CANDIDATE: `atmosphere.md`, `survival.md`, `physics.md` | Config load test, hazard route proof, save/load if persistent, profiler/GC, platform file-access audit |

## Overclaim And Proof-Risk Notes

- `SOURCE_SYSTEMS_REALITY_MAP.md` and `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` correctly state that source and docs are not runtime proof. No direct runtime readiness overclaim was found in those two docs.
- Exact full-path routing is still insufficient for this worker scope: 729 of 815 folder scripts lack exact anchors in the two routing docs.
- Shortened anchors reduce routing precision. They are acceptable as prose references but weak for controller integration, automated checks, and worker source targeting.
- Zero exact anchors in `Inventory`, `Tools`, `Scavenging`, and `Player` are route risks because those families carry first-hour item/tool/player truth.
- `AsyncBuoyancyReadbackRuntime`, ocean adapter, storm propagation, thermodynamics file worker, and Sargassum/ocean/water adjacent systems must not be reported as GPU/runtime clean from static text.
- `GlobalQualityWeight` appears in several missing exact anchors. Static text does not prove continuous scaling correctness. Proof must show quality changes do not change gameplay truth, save identity, DTO layout, or authority route.
- DataVault/SignalBus text hits do not prove data sovereignty. Reports must list buffer ids, owner system, generation handling, lock lifetime, overflow policy, and runtime proof when claiming integration.
- Editor scanners/tuners are numerous. They prove authoring surface only, not that audits were executed or passed.

## Recommended Patch Rows For Later Controller Integration

Do not apply these rows from this worker. They are recommendations for a later controller/shared-doc patch.

| Target doc | Add / sharpen source family | Exact anchors to include | Owner route / proof note |
|---|---|---|---|
| `SOURCE_SYSTEMS_REALITY_MAP.md` | World streaming exact path precision | `Assets/_Project/Scripts/World/ShinobuStreamingRuntime.cs`; `Assets/_Project/Scripts/World/HectonWorldStreamingTypes.cs`; `Assets/_Project/Scripts/World/Streaming/AssemblyInfo.cs` | Source present only. Requires Addressables/residency/VRAM/profiler route artifacts. |
| `SOURCE_SYSTEMS_REALITY_MAP.md` | Voxel/nav/biome SDF exact paths | `Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs`; `Assets/_Project/Scripts/World/Biomes/BiomeBoundarySdfRuntime.cs`; `Assets/_Project/Scripts/World/Biomes/BiomeTransitionManagerRuntime.cs` | Route through terrain/world/voxel docs. Requires nav/SDF/runtime capture and profiler proof. |
| `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` | Inventory and tool folders with exact anchors | `Assets/_Project/Scripts/Inventory/SoaInventoryQueryEngine.cs`; `Assets/_Project/Scripts/Tools/LaserCutterDodRuntime.cs`; `Assets/_Project/Scripts/Tools/ToolDurabilitySystem.cs` | Echelon 4 needs exact inventory/tool rows. Runtime proof remains pending. |
| `SOURCE_SYSTEMS_REALITY_MAP.md` | Loose-root tool inventory and crafting owners | `Assets/_Project/Scripts/SeafloorDrillTool.cs`; `Assets/_Project/Scripts/PlayerToolManager.cs`; `Assets/_Project/Scripts/CraftingSystem.FastFail.cs`; `Assets/_Project/Scripts/FabricatorPhysicalActuator.cs` | Prevent root script loss. Proof class: Play Mode route, save/load, profiler/GC. |
| `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` | Player folder exact route | `Assets/_Project/Scripts/Player/Movement/ZeroGMovementRuntime.cs`; `Assets/_Project/Scripts/Player/Movement/ZeroGMovementJobs.cs`; `Assets/_Project/Scripts/Player/Movement/ZeroGMovementContracts.cs` | Echelon 4 player movement. Requires device/control/runtime proof. |
| `SOURCE_SYSTEMS_REALITY_MAP.md` | Water-adjacent physics | `Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackRuntime.cs`; `Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementRuntime.cs`; `Assets/_Project/Scripts/Environment/Fluids/OceanAdapterVaultRoute.cs` | Proof must distinguish static source, GPU readback, water sample, and visual readiness. |
| `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` | Survival physiology exact route | `Assets/_Project/Scripts/Physiology/ShinobuRadiationMutationRuntime.cs`; `Assets/_Project/Scripts/Physiology/ShinobuRespawnReconciliationRuntime.cs`; `Assets/_Project/Scripts/Physiology/ShinobuSensoryImpairmentRuntime.cs` | Echelon 5 survival/physiology. Requires channel tests, death/recovery proof, save/load, profiler. |
| `SOURCE_SYSTEMS_REALITY_MAP.md` | Construction fluid/logistics job exact route | `Assets/_Project/Scripts/Construction/SumpPumpPipeGridJobs.cs`; `Assets/_Project/Scripts/Construction/FluidPipePressureJobs.cs`; `Assets/_Project/Scripts/Construction/LogisticsPipeRoutingKernel.cs`; `Assets/_Project/Scripts/Construction/LogisticsPipeTransportScheduler.cs` | Construction/water/power coupling. Requires graph, flow, save/load, profiler proof. |
| `SOURCE_SYSTEMS_REALITY_MAP.md` | Vehicle/drop-pod exact route | `Assets/_Project/Scripts/Vehicles/DropPod/DropPodAirlockController.cs`; `Assets/_Project/Scripts/Vehicles/DropPod/DropPodTransitSignals.cs`; `Assets/_Project/Scripts/Vehicles/Physics/Contracts/DynamicFloodMassContracts.cs` | Vehicle transfer and dynamic flood mass are source-present only. Requires movement/EVA/airlock proof. |
| `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` | Atmosphere/thermodynamics exact route | `Assets/_Project/Scripts/Atmosphere/StormPropagation/ShinobuStormPropagationRuntime.cs`; `Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.FileWorker.cs`; `Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs` | Echelon 7 plus survival/power coupling. Requires hazard route, file/config proof, profiler/GC. |
| `SOURCE_SYSTEMS_REALITY_MAP.md` | Procedural wreckage and vegetation exact route | `Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageVault.cs`; `Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageGpuUploadDispatcher.cs`; `Assets/_Project/Scripts/World/VegetationMemorySovereigntyRuntime.cs` | Source present only. Requires bake/import/runtime/profiler/visual capture. |

## GlobalQualityWeight Consequence

This audit did not change runtime quality logic. Documentation patching later must preserve continuous `GlobalQualityWeight` semantics:

- Low/compact: exact routing must name the minimum proof needed for readable route silhouettes, survival warning clarity, tool target clarity, and stable gameplay truth.
- Middle: exact routing must show default owner, phase, DataVault/Signal route, and proof artifact type without forcing broad doc reads.
- High: exact routing may add richer visual/profiler proof rows, but not separate gameplay truth.
- Ultra: proof depth and visual overkill may expand, but authority routes, DTO layout, save identity, survival formulas, and collision truth remain unchanged.

## Regression Model

CPU: No game runtime CPU path changed. Shell text scans only.

GC: No Unity runtime GC path changed. No 0 B/frame claim is made.

Memory: No Unity runtime memory path changed. No Memory Profiler proof exists from this worker.

Cadence: No dispatcher, tick, job, signal, or quality cadence changed.

Correctness: Static report reduces controller routing ambiguity. Residual risk is false confidence if later workers treat exact path anchors as runtime proof.

## Final Status

PENDING VERIFICATION.

The requested world/gameplay/physics/survival-adjacent source families are source-present and partially route-documented. The two routing docs are adequate for broad read-order routing but incomplete for exact source-owner routing in this scope. Controller integration should add exact full relative paths for the missing high-risk families before assigning route-card or runtime-proof work from those docs alone.
