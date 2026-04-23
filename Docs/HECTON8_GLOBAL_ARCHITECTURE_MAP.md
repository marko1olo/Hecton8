# HECTON-8 Global Architecture Map

Generated: 2026-04-23
Status: PENDING VERIFICATION

Mandates followed:
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `PHYS_Physics_Integrity_Determinism_ForceMode.txt`
- `AGENTS.md`

## Scope

Full audit target: `Assets/_Project/Scripts/**/*.cs`

Evidence:
- Latest inventory count: `793` first-party `.cs` files scanned with `rg --files Assets/_Project/Scripts -g *.cs`
- Scan includes runtime, bootstrap, core, world, UI, and `Editor/` scripts under the first-party tree
- `Swap-With-Last` O(1) removal logic confirmed in `RegistryBucket<T>.Unregister()`: find index, copy last live slot into the hole, null the old tail, decrement count

## Executive Findings

1. The requested core stack is already present in first-party code. `GlobalRegistry`, `RegistryBucket<T>`, `SystemDispatcher`, `GameBootstrapper`, `PhysicsApplySystem`, and `PhysicsForceRouter` are not missing.
2. The migration is incomplete. `IUpdatable` has zero implementers. `SystemDispatcher` exists, but the live runtime is still dominated by `GameTickManager`.
3. `GlobalRegistry` service-slot adoption is partial. Only `SceneRuntimeService` and `PhysicsApplySystem` currently populate authoritative slots. `Input`, `Audio`, and `UI` slots are idle.
4. The requested rogue-force purge is already materially complete. First-party `Rigidbody.AddForce` / `AddTorque` writes outside `HectonPlayerMotor` route through `PhysicsForceRouter` into `PhysicsApplySystem`.
5. The requested warning and asmdef failures are stale relative to the current workspace. Unity console refresh returned `0` warnings and `0` errors. `Hecton8.Core.csproj` and `Hecton8.Editor.csproj` both build clean. The reported `MSB4006` editor circular dependency was not reproducible in the current repo state.

## Full Scan Evidence

Current folder distribution from the audited tree:

```text
root-level files  289
_MCPWarmup        0
AtlasSignal       4
Atmosphere        4
Audio             10
AudioLog          4
Bootstrap         6
BuildTools        1
Compatibility     6
Construction      11
Core              8
Data              1
Dev               5
Economy           6
Ecosystem         7
Editor            100
Fauna             7
Gameplay          84
Input             3
Interaction       7
Inventory         0
Items             1
Meta              9
ModdingAPI        16
Narrative         6
Networking        1
Optimization      21
PDA               6
Power             2
Progression       3
Quest             3
Tools             12
UI                66
VFX               4
Visor             14
World             73
```

## Core Runtime Ownership

### Bootstrap Chain

Observed runtime bootstrap path:

```text
BootstrapController
  -> GameBootstrapper.InitializeBootstrap()
     -> InitializeCoreLayer()
        -> SystemDispatcher.EnsureRuntimeInstance()
        -> SceneRuntimeService.EnsureRuntimeInstance().InitializeService()
     -> InitializeEnvironmentLayer()
        -> PhysicsApplySystem.EnsureRuntimeInstance().InitializeService()
     -> InitializePlayerLayer()   // currently stubbed
     -> InitializeUILayer()       // currently stubbed
```

This matches the mandated ordered phase progression `Core -> Environment -> Player -> UI`, but only Core and Environment currently bind concrete `GlobalRegistry` services.

### GlobalRegistry Slot Adoption

| Slot | Owner | Current state |
|---|---|---|
| `Input` | none | idle |
| `Physics` | `PhysicsApplySystem` | live |
| `Audio` | none | idle |
| `Scene` | `SceneRuntimeService` | live |
| `UI` | none | idle |
| `Updatables` | `RegistryBucket<IUpdatable>` | empty |
| `Renderables` | `RegistryBucket<IRenderable>` | available, not part of this audit focus |

### Dispatcher Reality

`SystemDispatcher` is structurally correct:
- fixed 4-lane array
- dense `RegistryBucket<IUpdatable>` backing storage
- O(N) indexed scan
- no per-frame allocation in the loop

But it is currently idle in production terms because:
- `IUpdatable` has zero implementers
- `GlobalRegistry.RegisterUpdatable()` has no first-party callsites outside the registry code itself

The real runtime loop remains `GameTickManager`.

## Physics Routing Audit

Direct force-write ownership found in first-party scripts:

- `HectonPlayerMotor` retains direct player-body `AddForce` / `AddTorque`
- `PhysicsApplySystem` is the deferred packet sink and retains direct rigidbody application during flush

All other scanned first-party force writes route through `PhysicsForceRouter`:

- `FaunaBrain`
- `HarpoonLauncherTool`
- `GravTrap`
- `HarvestableOutcrop`
- `HarvestablePlant`
- `DeployableFlare`
- `DeployableBeacon`
- `Floater`
- `HeavyTowWinch`
- `HectonFluidEngine`
- `HectonItem`
- `MantaEmergencyWreck`
- `MountablePlayerTransport`
- `PickupItem`
- `PropulsionTool`
- `ResourceNode`
- `TetherInstance`
- `ThermalGeyser`
- `ToolHitUtility`
- `SargassumMicroFaunaBoids`

Conclusion: the requested force-routing rewrite is already present. The repository state does not contain the claimed mass of rogue `AddForce` callsites.

## System Topology

| Domain | Primary owners | Main upstream inputs | Main downstream outputs |
|---|---|---|---|
| `[PHYSICS]` | `PhysicsApplySystem`, `PhysicsForceRouter`, `HectonPlayerMotor`, `HectonPlayerMovement`, `BuoyancyObject`, `ThermalGeyser`, `TetherInstance` | bootstrap initialization, queued force packets, collision events, water/updraft state | rigidbody force application, collision trauma, transport impact damage |
| `[SURVIVAL]` | `HectonSurvivalSystem`, `HectonPlayerHealth`, `HectonPlayerEnvironmentHandler`, `HabitatIntegrityManager` | collision trauma, pressure exposure, fauna attacks, environment hazards | hull-stress state, injury states, notifications, HUD corruption inputs |
| `[LOGISTICS]` | `BaseLogisticsNetwork`, `LogisticsPipeNode`, `LogisticsSorterModule`, `Fabricator`, `BatteryCharger`, `BioReactor`, `ResourceRecyclerModule`, `TransportChargingStation`, `VehicleDockingModule` | power graph, inventory, interaction, construction state | fabrication/recycling flows, transport charging, UI events, world service wiring |
| `[VOXEL]` | `HectonVoxelEngine`, `HectonVoxelVolume`, `HectonVoxelStreamingBridge`, `WorldGenerativeGeologyVoxelBridgeDirector`, `WorldGenerativeGeologyIntegrationDirector`, `WorldGenerativeGeologySeamExecutionDirector` | world streaming, geology integration plans, crater persistence, MapMagic bridge data | runtime cave volumes, voxel seams, collider chunks, vegetation bridge structure registration |
| `[AI]` | `HectonDirectorAI`, `FaunaDirector`, `FaunaBrain`, `DeepPsychosisController`, `SargassumMicroFaunaBoids` | ecology state, player proximity, biome context, narrative signals | predator pressure, equipment glitch events, hull-stress requests, fauna steering, audio pressure |
| `[UI]` | `SuitHUDV4CanvasOverlay`, `VisorHUDController`, `SuitHUDPresentationController`, `HUDNotification`, `SurvivalHUDController`, `InteractionUI`, `HectonFabricatorUI`, `PDAIntrusionManager`, `PDADataLogTab`, `PDAShellChrome`, `SubtitleManager` | survival state, localization corruption, interaction hover, crafting events, director glitch events | visor glitch pulses, HUD corruption, prompts, overlays, PDA distortion, notifications |

### High-Value Cross-System Edges

- `[PHYSICS] -> [SURVIVAL]`: `HectonPlayerMovement.ProcessQueuedCollisionEvents()` applies transport collision damage and reports physical trauma into `HectonSurvivalSystem.ReportPhysicalTrauma(...)`.
- `[SURVIVAL] -> [UI]`: `LocalizationManager` reads `HectonPlayerMovement.CurrentHullStress01`, then `SuitHUDV4CanvasOverlay`, `HUDNotification`, `PDAShellChrome`, `PDADataLogTab`, and `SubtitleManager` consume the corruption state.
- `[AI] -> [SURVIVAL]`: `SargassumMicroFaunaBoids` calls `_playerMovement.RequestExternalHullStress(...)` and `_playerHealth.TakeDamage(...)`.
- `[AI] -> [UI]`: `HectonDirectorAI.OnRequestEquipmentGlitch` is consumed by `PDAIntrusionManager`.
- `[LOGISTICS] -> [UI]`: `Fabricator` raises `CraftingEvents.OnFabricatorOpened/Closed`; `HectonFabricatorUI` is the downstream surface.
- `[LOGISTICS] -> [SURVIVAL/UI]`: `InteractionUI` contains direct prompt builders for `BatteryCharger` and `BioReactor`, so logistics modules bypass a pure generic interaction presentation path.
- `[VOXEL] -> [PHYSICS]`: `HectonVoxelVolume` owns collider-chunk distribution and crater rebuild state; `SargassumCollapseChunk` explicitly branches on `VoxelRock`.
- `[VOXEL] -> [WORLD/VEGETATION]`: `HectonVoxelStreamingBridge` registers artificial structures with the vegetation bridge after spawning voxel cave volumes.
- `[VOXEL] -> [UI/GRAPHICS]`: `HectonVoxelSsaoFeature` and related visor/rendering code consume voxel-space depth/geometry for presentation.

## Collision To HUD Disturbance Flow

Direct `collision -> GlitchPulse()` wiring was not found as a single straight call chain in the audited runtime. The nearest verified escalation path is collision/trauma pressure on player state, then hull-stress / corruption surfaces. Dotted edges below denote indirect or inferred escalation rather than a direct local callsite.

```mermaid
flowchart LR
    A[Collision event\nHectonPlayerMovement queued collision] --> B[ProcessQueuedCollisionEvents()]
    B --> C[ApplyTransportCollisionImpact()\nMountablePlayerTransport / MantaScooter]
    B --> D[TryStartWipeoutFromCollision()]
    D --> E[ApplyPhysicalTrauma()]
    D --> F[HectonSurvivalSystem.ReportPhysicalTrauma()]
    C -. transport failure pressure exposure .-> G[HectonPlayerMovement.UpdateHullStress()]
    F -. injury escalation / danger state .-> G
    G --> H[LocalizationManager\nGetHullStressCorruptionIntensity()]
    H --> I[SuitHUDV4CanvasOverlay\nstress pulse + whisper corruption]
    H --> J[HUDNotification / PDA tabs / subtitles\nApplyHullStressCorruptionIfNeeded()]
    G -. fatal pressure branch only .-> K[VisorHUDController.GlitchPulse()]
```

Interpretation:
- Collision definitely feeds trauma and transport integrity damage.
- Hull-stress corruption definitely feeds HUD surfaces.
- Fatal visor glitch pulses are pressure-sequence driven, not exposed as a clean generic collision callback.

## Interface Registry

Registration semantics used in this map:
- `GameTickManager`: explicit or file-local `GameTickManager` registration path found
- `GlobalRegistry/SystemDispatcher`: explicit `IUpdatable` registration path found
- `No explicit registration found`: no manager registration was found in the implementing file

Important nuance:
- For `IInteractable`, `No explicit registration found` does not automatically mean unreachable. The interaction stack is query-driven through collider/interface discovery in `PlayerInteraction` and `PhysicalInteractionHandler`.

### ITickable
| Class | File | Status |
|---|---|---|
| `AbyssalFluidDecalManager` | `Assets/_Project/Scripts/World/AbyssalFluidDecalManager.cs:14` | GameTickManager |
| `AbyssalThermalManager` | `Assets/_Project/Scripts/World/AbyssalThermalManager.cs:17` | GameTickManager |
| `AcousticEcholocationTranslator` | `Assets/_Project/Scripts/UI/AcousticEcholocationTranslator.cs:22` | GameTickManager |
| `AcousticZoneController` | `Assets/_Project/Scripts/AcousticZoneController.cs:67` | GameTickManager |
| `ActionProgressHUD` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:30` | GameTickManager |
| `AmbientWaterMotionManager` | `Assets/_Project/Scripts/AmbientWaterMotionManager.cs:25` | GameTickManager |
| `ARWaypointOverlay` | `Assets/_Project/Scripts/UI/ARWaypointOverlay.cs:17` | GameTickManager |
| `AssetLifecycleGovernor` | `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs:13` | GameTickManager |
| `AssetLoadDispatcher` | `Assets/_Project/Scripts/Optimization/AssetLoadDispatcher.cs:12` | GameTickManager |
| `AsyncLoadHelper` | `Assets/_Project/Scripts/AsyncLoadHelper.cs:62` | No explicit registration found |
| `AudioCaptionOverlay` | `Assets/_Project/Scripts/UI/AcousticEcholocationTranslator.cs:836` | GameTickManager |
| `AudioWaveformAnimator` | `Assets/_Project/Scripts/UI/AudioWaveformAnimator.cs:12` | GameTickManager |
| `BaseAirlock` | `Assets/_Project/Scripts/Gameplay/BaseAirlock.cs:46` | GameTickManager |
| `BatteryCharger` | `Assets/_Project/Scripts/Gameplay/BatteryCharger.cs:62` | GameTickManager |
| `BeaconHUDElement` | `Assets/_Project/Scripts/UI/BeaconHUDElement.cs:27` | GameTickManager |
| `BeaconRuntime` | `Assets/_Project/Scripts/BeaconRuntime.cs:7` | GameTickManager |
| `BioReactor` | `Assets/_Project/Scripts/Gameplay/BioReactor.cs:53` | GameTickManager |
| `BuilderStatusOverlay` | `Assets/_Project/Scripts/UI/BuilderStatusOverlay.cs:16` | GameTickManager |
| `CameraJuiceSystem` | `Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs:28` | GameTickManager |
| `CausticsProjectorManager` | `Assets/_Project/Scripts/Visor/CausticsProjectorManager.cs:15` | GameTickManager |
| `CaveBioRootsGenerator` | `Assets/_Project/Scripts/CaveBioRootsGenerator.cs:11` | GameTickManager |
| `CrashTelemetryBuffer` | `Assets/_Project/Scripts/CrashTelemetryBuffer.cs:25` | GameTickManager |
| `DeepPsychosisController` | `Assets/_Project/Scripts/Audio/DeepPsychosisController.cs:13` | GameTickManager |
| `DeployableBeacon` | `Assets/_Project/Scripts/Gameplay/DeployableBeacon.cs:35` | GameTickManager |
| `DeployableFlare` | `Assets/_Project/Scripts/Gameplay/DeployableFlare.cs:48` | GameTickManager |
| `DespawnTimer` | `Assets/_Project/Scripts/ObjectPoolManager.cs:723` | GameTickManager |
| `DynamicResolutionScaler` | `Assets/_Project/Scripts/World/DynamicResolutionScaler.cs:51` | GameTickManager |
| `EntityChangeManager` | `Assets/_Project/Scripts/EntityChangeDetector.cs:268` | GameTickManager |
| `EnvironmentalHazard` | `Assets/_Project/Scripts/Gameplay/EnvironmentalHazard.cs:33` | GameTickManager |
| `Fabricator` | `Assets/_Project/Scripts/Fabricator.cs:52` | GameTickManager |
| `FaunaBrain` | `Assets/_Project/Scripts/Fauna/FaunaBrain.cs:19` | GameTickManager |
| `FloraInteractionManager` | `Assets/_Project/Scripts/World/FloraInteractionManager.cs:21` | GameTickManager |
| `FloraProjectile` | `Assets/_Project/Scripts/Gameplay/FloraProjectile.cs:16` | GameTickManager |
| `FontStreamingManager` | `Assets/_Project/Scripts/UI/FontStreamingManager.cs:17` | GameTickManager |
| `GravTrap` | `Assets/_Project/Scripts/Gameplay/GravTrap.cs:34` | GameTickManager |
| `HarvestablePlant` | `Assets/_Project/Scripts/Gameplay/HarvestablePlant.cs:56` | GameTickManager |
| `HectonAtmosphereManager` | `Assets/_Project/Scripts/HectonAtmosphereManager.cs:65` | GameTickManager |
| `HectonBiolumManager` | `Assets/_Project/Scripts/World/Biolum/HectonBiolumManager.cs:24` | GameTickManager |
| `HectonBiolumZone` | `Assets/_Project/Scripts/World/Biolum/HectonBiolumZone.cs:66` | GameTickManager |
| `HectonBoidController` | `Assets/_Project/Scripts/HectonBoidController.cs:71` | GameTickManager |
| `HectonCelestialEngine` | `Assets/_Project/Scripts/HectonCelestialEngine.cs:105` | GameTickManager |
| `HectonDistantLandmarkRenderer` | `Assets/_Project/Scripts/World/HectonDistantLandmarkRenderer.cs:16` | GameTickManager |
| `HectonFabricatorUI` | `Assets/_Project/Scripts/HectonFabricatorUI.cs:92` | GameTickManager |
| `HectonFloatingOrigin` | `Assets/_Project/Scripts/HectonFloatingOrigin.cs:19` | GameTickManager |
| `HectonHazardSource` | `Assets/_Project/Scripts/Gameplay/HectonHazardSource.cs:16` | GameTickManager |
| `HectonHLODRenderer` | `Assets/_Project/Scripts/World/HectonHLODRenderer.cs:16` | GameTickManager |
| `HectonIndirectVegetationRenderer` | `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:18` | GameTickManager |
| `HectonInventoryUI` | `Assets/_Project/Scripts/HectonInventoryUI.cs:43` | GameTickManager |
| `HectonItem` | `Assets/_Project/Scripts/HectonItem.cs:31` | GameTickManager |
| `HectonMapMagicVegetationBridge` | `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs:23` | GameTickManager |
| `HectonMarineSnowRenderer` | `Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs:15` | GameTickManager |
| `HectonMusicDirector` | `Assets/_Project/Scripts/Audio/HectonMusicDirector.cs:16` | GameTickManager |
| `HectonOSBootManager` | `Assets/_Project/Scripts/UI/HectonOSBootManager.cs:17` | GameTickManager |
| `HectonPlayerCameraRig` | `Assets/_Project/Scripts/Gameplay/HectonPlayerCameraRig.cs:13` | GameTickManager |
| `HectonPlayerHealth` | `Assets/_Project/Scripts/Gameplay/HectonPlayerHealth.cs:18` | GameTickManager |
| `HectonPlayerMovement` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:43` | GameTickManager |
| `HectonScanMarkerSystem` | `Assets/_Project/Scripts/HectonScanMarkerSystem.cs:48` | GameTickManager |
| `HectonSuitHUD_v4` | `Assets/_Project/Scripts/HectonSuitHUD_v4.cs:14` | GameTickManager |
| `HectonSuitHUDExtensions` | `Assets/_Project/Scripts/HectonSuitHUDExtensions.cs:43` | GameTickManager |
| `HectonSurfaceWeatherDirector` | `Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs:20` | GameTickManager |
| `HectonSurvivalSystem` | `Assets/_Project/Scripts/HectonSurvivalSystem.cs:183` | GameTickManager |
| `HectonUIScaler` | `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:2401` | GameTickManager |
| `HectonUnderwaterVisuals` | `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:75` | GameTickManager |
| `HectonVoxelStreamingBridge` | `Assets/_Project/Scripts/World/HectonVoxelStreamingBridge.cs:15` | GameTickManager |
| `HectonWorldGenerator` | `Assets/_Project/Scripts/HectonWorldGenerator.cs:444` | GameTickManager |
| `HUDNotification` | `Assets/_Project/Scripts/HUDNotification.cs:19` | GameTickManager |
| `HUDQuickBar` | `Assets/_Project/Scripts/HUDQuickBar.cs:20` | GameTickManager |
| `ImpostorSystem` | `Assets/_Project/Scripts/World/ImpostorSystem.cs:69` | GameTickManager |
| `InteractionHighlighter` | `Assets/_Project/Scripts/InteractionHighlighter.cs:63` | GameTickManager |
| `InteractionUI` | `Assets/_Project/Scripts/UI/InteractionUI.cs:35` | GameTickManager |
| `ItemHighlight` | `Assets/_Project/Scripts/Gameplay/ItemHighlight.cs:37` | GameTickManager |
| `LandingImpactVFX` | `Assets/_Project/Scripts/LandingImpactVFX.cs:30` | GameTickManager |
| `LoadingScreenController` | `Assets/_Project/Scripts/UI/LoadingScreenController.cs:15` | GameTickManager |
| `LoadingTipsDisplay` | `Assets/_Project/Scripts/UI/LoadingTipsDisplay.cs:14` | GameTickManager |
| `LocalizedTextMadnessFx` | `Assets/_Project/Scripts/UI/LocalizedTextMadnessFx.cs:12` | GameTickManager |
| `LODSystemManager` | `Assets/_Project/Scripts/World/LODSystemManager.cs:76` | GameTickManager |
| `MainMenuController` | `Assets/_Project/Scripts/MainMenuController.cs:19` | GameTickManager |
| `MaintenanceStationModule` | `Assets/_Project/Scripts/Construction/MaintenanceStationModule.cs:20` | GameTickManager |
| `MantaScooter` | `Assets/_Project/Scripts/Gameplay/MantaScooter.cs:35` | GameTickManager |
| `MessageTerminal` | `Assets/_Project/Scripts/Gameplay/MessageTerminal.cs:70` | GameTickManager |
| `MountablePlayerTransport` | `Assets/_Project/Scripts/Gameplay/MountablePlayerTransport.cs:21` | GameTickManager |
| `ObserverRelativeCelestialBody` | `Assets/_Project/Scripts/ObserverRelativeCelestialBody.cs:23` | GameTickManager |
| `OxygenBubble` | `Assets/_Project/Scripts/Gameplay/OxygenBubble.cs:46` | GameTickManager |
| `OxygenPlant` | `Assets/_Project/Scripts/Gameplay/OxygenPlant.cs:33` | GameTickManager |
| `PauseMenuController` | `Assets/_Project/Scripts/UI/PauseMenuController.cs:20` | GameTickManager |
| `PauseSystemVerifier` | `Assets/_Project/Scripts/Tools/PauseSystemVerifier.cs:13` | GameTickManager |
| `PDAAtlasSignalTab` | `Assets/_Project/Scripts/UI/PDAAtlasSignalTab.cs:33` | GameTickManager |
| `PDAConstructionTab` | `Assets/_Project/Scripts/UI/PDAConstructionTab.cs:23` | GameTickManager |
| `PDADataLogTab` | `Assets/_Project/Scripts/UI/PDADataLogTab.cs:36` | GameTickManager |
| `PDADeathMemoryDump` | `Assets/_Project/Scripts/UI/PDADeathMemoryDump.cs:17` | GameTickManager |
| `PDAIntrusionManager` | `Assets/_Project/Scripts/UI/PDAIntrusionManager.cs:19` | GameTickManager |
| `PDAMarkerHUDElement` | `Assets/_Project/Scripts/PDA/PDAMarkerHUDElement.cs:15` | GameTickManager |
| `PDAShellChrome` | `Assets/_Project/Scripts/UI/PDAShellChrome.cs:15` | GameTickManager |
| `PerformanceBudgetController` | `Assets/_Project/Scripts/Tools/PerformanceBudgetController.cs:14` | GameTickManager |
| `PerformanceMonitor` | `Assets/_Project/Scripts/Tools/PerformanceMonitor.cs:15` | GameTickManager |
| `PerformanceMonitor` | `Assets/_Project/Scripts/PerformanceMonitor.cs:129` | GameTickManager |
| `PhysicalInteractionHandler` | `Assets/_Project/Scripts/Interaction/PhysicalInteractionHandler.cs:19` | GameTickManager |
| `PlayerAchievementRegistry` | `Assets/_Project/Scripts/Progression/PlayerAchievementRegistry.cs:18` | GameTickManager |
| `PlayerActionController` | `Assets/_Project/Scripts/Gameplay/PlayerActionController.cs:33` | GameTickManager |
| `PlayerCriticalProceduralAudioRenderer` | `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:27` | GameTickManager |
| `PlayerExplorationTracker` | `Assets/_Project/Scripts/PDA/PlayerExplorationTracker.cs:16` | GameTickManager |
| `PlayerFlashlight` | `Assets/_Project/Scripts/PlayerFlashlight.cs:70` | GameTickManager |
| `PlayerInteraction` | `Assets/_Project/Scripts/Interaction/PlayerInteraction.cs:56` | GameTickManager |
| `PlayerNoiseEmitter` | `Assets/_Project/Scripts/Gameplay/PlayerNoiseEmitter.cs:12` | GameTickManager |
| `PlayerPDA` | `Assets/_Project/Scripts/PlayerPDA.cs:87` | GameTickManager |
| `PlayerStressVFX` | `Assets/_Project/Scripts/Visor/PlayerStressVFX.cs:15` | GameTickManager |
| `PlayerSwimBlockoutRig` | `Assets/_Project/Scripts/Gameplay/PlayerSwimBlockoutRig.cs:13` | GameTickManager |
| `PlayerSwimPresentationController` | `Assets/_Project/Scripts/Gameplay/PlayerSwimPresentationController.cs:14` | GameTickManager |
| `PlayerThrusterAudio` | `Assets/_Project/Scripts/PlayerThrusterAudio.cs:15` | GameTickManager |
| `PlayerToolManager` | `Assets/_Project/Scripts/PlayerToolManager.cs:41` | GameTickManager |
| `ProximityColliderSystem` | `Assets/_Project/Scripts/ProximityColliderSystem.cs:46` | GameTickManager |
| `RelayHUDElement` | `Assets/_Project/Scripts/UI/RelayHUDElement.cs:17` | GameTickManager |
| `RepairDroneEntity` | `Assets/_Project/Scripts/Construction/RepairDroneEntity.cs:13` | GameTickManager |
| `ResourceRecyclerModule` | `Assets/_Project/Scripts/Economy/ResourceRecyclerModule.cs:21` | GameTickManager |
| `RuntimePerformanceProfiler` | `Assets/_Project/Scripts/RuntimePerformanceProfiler.cs:34` | GameTickManager |
| `SargassumCollapseChunk` | `Assets/_Project/Scripts/World/SargassumCollapseChunk.cs:16` | GameTickManager |
| `SargassumCrestDampingController` | `Assets/_Project/Scripts/World/SargassumCrestDampingController.cs:11` | GameTickManager |
| `SargassumCutManager` | `Assets/_Project/Scripts/World/SargassumCutManager.cs:14` | GameTickManager |
| `SargassumCutResponder` | `Assets/_Project/Scripts/Gameplay/SargassumCutResponder.cs:16` | GameTickManager |
| `SargassumDebrisParticleSystem` | `Assets/_Project/Scripts/World/SargassumDebrisParticleSystem.cs:12` | GameTickManager |
| `SargassumGlobalDragManager` | `Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs:16` | GameTickManager |
| `SargassumMicroFaunaBoids` | `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:23` | GameTickManager |
| `SaveSlotHoverPreview` | `Assets/_Project/Scripts/UI/SaveSlotHoverPreview.cs:18` | GameTickManager |
| `SealedDoor` | `Assets/_Project/Scripts/Gameplay/SealedDoor.cs:49` | GameTickManager |
| `SettingsComparisonView` | `Assets/_Project/Scripts/UI/SettingsComparisonView.cs:15` | GameTickManager |
| `SettingsLivePreview` | `Assets/_Project/Scripts/UI/SettingsLivePreview.cs:16` | GameTickManager |
| `SettingsPanelAnimator` | `Assets/_Project/Scripts/UI/SettingsPanelAnimator.cs:12` | GameTickManager |
| `SolarPanel` | `Assets/_Project/Scripts/Gameplay/SolarPanel.cs:34` | GameTickManager |
| `SonarHoloCompass` | `Assets/_Project/Scripts/UI/SonarHoloCompass.cs:15` | GameTickManager |
| `SpectrumSystem` | `Assets/_Project/Scripts/Visor/SpectrumSystem.cs:74` | GameTickManager |
| `StunTargetRuntime` | `Assets/_Project/Scripts/StunPistolTool.cs:532` | GameTickManager |
| `SubnauticaSystemsDebugUI` | `Assets/_Project/Scripts/UI/SubnauticaSystemsDebugUI.cs:22` | GameTickManager |
| `SubtitleManager` | `Assets/_Project/Scripts/UI/SubtitleManager.cs:18` | GameTickManager |
| `SuitHUDPresentationController` | `Assets/_Project/Scripts/Visor/SuitHUDPresentationController.cs:16` | GameTickManager |
| `SuitHUDScreenCompositor` | `Assets/_Project/Scripts/Visor/SuitHUDScreenCompositor.cs:15` | GameTickManager |
| `SuitHUDV4CanvasOverlay` | `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:23` | GameTickManager |
| `SurvivalHUDController` | `Assets/_Project/Scripts/UI/SurvivalHUDController.cs:28` | GameTickManager |
| `TerminalBootSequence` | `Assets/_Project/Scripts/UI/AcousticEcholocationTranslator.cs:514` | GameTickManager |
| `ThermalGeyser` | `Assets/_Project/Scripts/ThermalGeyser.cs:15` | GameTickManager |
| `TransportChargingStation` | `Assets/_Project/Scripts/Gameplay/TransportChargingStation.cs:18` | GameTickManager |
| `UIFadeTransition` | `Assets/_Project/Scripts/UI/UIFadeTransition.cs:13` | GameTickManager |
| `UIScreenShake` | `Assets/_Project/Scripts/UI/UIScreenShake.cs:13` | GameTickManager |
| `UITooltip` | `Assets/_Project/Scripts/UI/UITooltip.cs:16` | GameTickManager |
| `VehicleDockingModule` | `Assets/_Project/Scripts/Construction/VehicleDockingModule.cs:17` | GameTickManager |
| `VisorHUDController` | `Assets/_Project/Scripts/Visor/VisorHUDController.cs:22` | GameTickManager |
| `VRAMPressureMonitor` | `Assets/_Project/Scripts/Optimization/VRAMPressureMonitor.cs:12` | GameTickManager |
| `WorldGenerativeGeologyVoxelBridgeDirector` | `Assets/_Project/Scripts/WorldGenerativeGeologyVoxelBridgeDirector.cs:96` | GameTickManager |
| `WorldProceduralScatterDirector` | `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs:23` | GameTickManager |

### IUpdatable
No implementations found.

### IInteractable
| Class | File | Status |
|---|---|---|
| `AudioLogPickup` | `Assets/_Project/Scripts/AudioLog/AudioLogPickup.cs:15` | No explicit registration found |
| `BaseAirlock` | `Assets/_Project/Scripts/Gameplay/BaseAirlock.cs:46` | GameTickManager |
| `BatteryCharger` | `Assets/_Project/Scripts/Gameplay/BatteryCharger.cs:62` | GameTickManager |
| `BioReactor` | `Assets/_Project/Scripts/Gameplay/BioReactor.cs:53` | GameTickManager |
| `BotanyPlanterModule` | `Assets/_Project/Scripts/Construction/BotanyPlanterModule.cs:16` | GameTickManager |
| `ClimbableLadder` | `Assets/_Project/Scripts/Gameplay/ClimbableLadder.cs:37` | No explicit registration found |
| `DeployableBeacon` | `Assets/_Project/Scripts/Gameplay/DeployableBeacon.cs:35` | GameTickManager |
| `EmergencyServiceRelay` | `Assets/_Project/Scripts/World/EmergencyServiceRelay.cs:24` | No explicit registration found |
| `EndingTerminalInteractable` | `Assets/_Project/Scripts/Gameplay/EndingTerminalInteractable.cs:26` | No explicit registration found |
| `Fabricator` | `Assets/_Project/Scripts/Fabricator.cs:52` | GameTickManager |
| `Floater` | `Assets/_Project/Scripts/Gameplay/Floater.cs:49` | GameTickManager |
| `HarvestableOutcrop` | `Assets/_Project/Scripts/Gameplay/HarvestableOutcrop.cs:40` | No explicit registration found |
| `HeavyCarryInteractable` | `Assets/_Project/Scripts/Interaction/HeavyCarryInteractable.cs:18` | No explicit registration found |
| `HectonItem` | `Assets/_Project/Scripts/HectonItem.cs:31` | GameTickManager |
| `MaintenanceStationModule` | `Assets/_Project/Scripts/Construction/MaintenanceStationModule.cs:20` | GameTickManager |
| `MessageTerminal` | `Assets/_Project/Scripts/Gameplay/MessageTerminal.cs:70` | GameTickManager |
| `MountablePlayerTransport` | `Assets/_Project/Scripts/Gameplay/MountablePlayerTransport.cs:21` | GameTickManager |
| `NarrativeDiscovery` | `Assets/_Project/Scripts/NarrativeDiscovery.cs:16` | No explicit registration found |
| `PickupItem` | `Assets/_Project/Scripts/Items/PickupItem.cs:19` | GameTickManager |
| `ResourceRecyclerModule` | `Assets/_Project/Scripts/Economy/ResourceRecyclerModule.cs:21` | GameTickManager |
| `SaveStation` | `Assets/_Project/Scripts/Interaction/SaveStation.cs:19` | No explicit registration found |
| `ScannableFragment` | `Assets/_Project/Scripts/Gameplay/ScannableFragment.cs:49` | No explicit registration found |
| `StorageCrate` | `Assets/_Project/Scripts/Gameplay/StorageCrate.cs:54` | No explicit registration found |

### IDamageReceiver
| Class | File | Status |
|---|---|---|
| `HabitatIntegrityManager` | `Assets/_Project/Scripts/Gameplay/HabitatIntegrityManager.cs:79` | GameTickManager |

## Orphan Notes

Confirmed orphan candidate:
- `AsyncLoadHelper` implements `ITickable` but no explicit registration path was found in the file. The class is a disabled legacy compatibility shell, so this is a documentation-grade architecture mismatch, not proof of a live gameplay failure.

Query-driven non-registry `IInteractable` implementations:
- `AudioLogPickup`
- `ClimbableLadder`
- `EmergencyServiceRelay`
- `EndingTerminalInteractable`
- `HarvestableOutcrop`
- `HeavyCarryInteractable`
- `NarrativeDiscovery`
- `SaveStation`
- `ScannableFragment`
- `StorageCrate`

These are not automatically broken. They are discovered through collider/interface queries rather than explicit manager registration.

## Verification Log

Unity-backed checks executed during this audit:

- `refresh_unity(scope=scripts, compile=request, wait_for_ready=true)` issued against a live Unity session in `02_HECTON_WORLD`
- Unity console after refresh: `0` warnings, `0` errors
- `dotnet build Hecton8.Core.csproj`: `0` warnings, `0` errors
- `dotnet build Hecton8.Editor.csproj`: `0` warnings, `0` errors

GC proof for the new dispatcher loop:

- `RegistryBucket<T>` allocates once at construction time only
- `Register()` appends into a fixed dense array
- `Unregister()` uses swap-with-last O(1) tail compaction
- `SystemDispatcher.Update()` reads cached `Count`, cached `RawArray`, and invokes `Tick(deltaTime)` via indexed `for` loops
- No per-frame `new`, no collection growth, no `foreach`, no delegates, no closures, no LINQ, no boxing

Measured proof absent:
- No Profiler GC capture was taken for `SystemDispatcher.Update()` in this audit pass
- Static code proof supports `0 B/frame` for the loop body itself

## Final State

What was wrong:
- The request assumed the core registry/dispatcher/bootstrap/force-router stack was missing.

What the audit found:
- The core stack already exists.
- The actual architecture gap is incomplete adoption, not absent implementation.
- The authoritative runtime still routes almost everything through `GameTickManager`.

What remains pending:
- Migrate real systems from `ITickable` / `GameTickManager` to `IUpdatable` / `SystemDispatcher`, or formally retire the unused registry path.
- Decide whether `AsyncLoadHelper` should lose `ITickable` or gain a real registration path.
- Capture Profiler evidence if a numbers-backed GC claim is required beyond static proof.
