# [ARCHIVE] X_012 Historical Report

Archive date: 2026-05-23
Reason: removed from active documentation corpus by X_012; historical evidence only.
Active index: ../../Reports/README.md

# SIGNAL DOMAIN HOT ROUTE AUDIT X_001

Generated: 2026-05-23 23:18:00 +04:00
Evidence class: STATIC SOURCE ONLY. `rg`-equivalent source scan of selected runtime domain folders, excluding Editor folders.

## Summary

- Domains scanned: 11.
- Legacy hot route hits (GlobalSignals.Publish/Push/TryDequeue/*Writer): 0.
- Central Core compatibility definitions are now compile-time banned and are not counted as domain route hits.
- Remaining non-hot GlobalSignals. helper/read/bootstrap hits in scanned domains: 110.

## Domain Matrix

| Domain path | Legacy hot hits | Non-hot helper/read hits |
|---|---:|---:|
| `Assets/_Project/Scripts/Power` | 0 | 2 |
| `Assets/_Project/Scripts/Habitat` | 0 | 1 |
| `Assets/_Project/Scripts/Environment` | 0 | 1 |
| `Assets/_Project/Scripts/Construction` | 0 | 12 |
| `Assets/_Project/Scripts/Gameplay` | 0 | 41 |
| `Assets/_Project/Scripts/Physics` | 0 | 3 |
| `Assets/_Project/Scripts/World` | 0 | 29 |
| `Assets/_Project/Scripts/Animation` | 0 | 0 |
| `Assets/_Project/Scripts/UI` | 0 | 16 |
| `Assets/_Project/Scripts/Audio` | 0 | 5 |
| `Assets/_Project/Scripts/Inventory` | 0 | 0 |

## Legacy Hot Hits

- None.

## Remaining Non-Hot GlobalSignals Access

These are not `Publish`, `Push`, destructive dequeue, or writer routes. They remain route-card/bootstrap/helper debt, not old event-queue traffic. First 200 listed.

- Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs:150: GlobalSignals.InitializeAllQueues();
- Assets/_Project/Scripts/Power/Generators/RadioisotopeThermalGenerator.cs:896: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:1903: originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:3941: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Construction/AutonomousExtractorSystem.cs:997: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Construction/BaseDegradationSystem.cs:513: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Construction/DroneFleetManager.cs:1560: GlobalSignals.InitializeAllQueues();
- Assets/_Project/Scripts/Construction/DroneFleetManager.cs:3516: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Construction/DroneFleetManager.cs:6337: return GlobalSignals.CurrentRuntimeOriginAup().ToAbsoluteDouble3();
- Assets/_Project/Scripts/Construction/DroneFleetManager.cs:6392: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:3483: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Construction/LogisticsPipeNode.cs:668: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Construction/RepairDroneHub.cs:154: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Construction/VehicleDockingModule.cs:1726: originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Construction/VRConstructionWeldTarget.cs:495: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Construction/WaterPumpModule.cs:370: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Gameplay/BatteryCharger.cs:750: Hecton8.World.AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Gameplay/BeaconRegistry.cs:139: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Gameplay/BioReactor.cs:800: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs:3408: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:1283: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Gameplay/DebrisManager.cs:944: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Gameplay/DeployableBeacon.cs:417: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Gameplay/EnvironmentalHazard.cs:464: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Gameplay/FloraProjectile.cs:83: uint source = GlobalSignals.FoldEntityIdToSourceId(EntityId.ToULong(gameObject.GetEntityId()));
- Assets/_Project/Scripts/Gameplay/HabitatIntegrityManager.cs:594: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Gameplay/HabitatIntegrityManager.cs:956: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Gameplay/HarvestableOutcrop.cs:575: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Gameplay/HarvestablePlant.cs:415: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Gameplay/HazardZoneManager.cs:1860: originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Gameplay/HectonHazardManager.cs:239: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs:1031: originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Gameplay/HectonPlayerState.cs:79: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Gameplay/HectonScanRenderRegistry.cs:280: AbsoluteUniversePosition runtimeOriginAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Gameplay/HectonScanRenderRegistry.cs:431: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Gameplay/HostileFlora.cs:142: _sourceEntityId = GlobalSignals.FoldEntityIdToSourceId(EntityId.ToULong(GetEntityId()));
- Assets/_Project/Scripts/Gameplay/HostileFlora.cs:521: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs:185: _signalSourceId = GlobalSignals.FoldEntityIdToSourceId(EntityId.ToULong(GetEntityId()));
- Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs:708: _scanLogSourceId = currentScanLog != null ? GlobalSignals.FoldEntityIdToSourceId(EntityId.ToULong(currentScanLog.GetEntityId())) : 0u;
- Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs:751: _signalSourceId = GlobalSignals.FoldEntityIdToSourceId(EntityId.ToULong(GetEntityId()));
- Assets/_Project/Scripts/Gameplay/PlayerNoiseEmitter.cs:261: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Gameplay/PlayerTransportCoordinator.cs:368: GlobalSignals.FoldEntityIdToSourceId(EntityId.ToULong(playerToolManager.gameObject.GetEntityId()));
- Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:2043: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:2390: AbsoluteUniversePosition origin = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:890: _sourceId = Hecton8.Core.GlobalSignals.FoldEntityIdToSourceId(EntityId.ToULong(GetEntityId()));
- Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:3197: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Gameplay/SubmarineStationKeepingController.cs:290: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Gameplay/VehicleMotor.cs:1024: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Gameplay/VehicleUpgradeModule.cs:184: _signalSourceId = GlobalSignals.FoldEntityIdToSourceId(EntityId.ToULong(GetEntityId()));
- Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs:3126: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Gameplay/Combat/BallisticsRuntime.cs:1532: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Gameplay/Combat/BallisticsRuntime.cs:1549: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs:1532: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Gameplay/Combat/HectonCombatRuntime_ArmorPenetration.cs:882: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Gameplay/Loot/LootMagnetSystem.cs:1394: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Gameplay/Mining/DeployableSdfDrillRuntime.cs:1642: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Gameplay/Mining/DeployableSdfDrillRuntime.cs:1876: return DeployableSdfDrillMath.Mix(DrillToolHash, Hecton8.Core.GlobalSignals.FoldEntityIdToSourceId(EntityId.ToULong(GetEntityId())));
- Assets/_Project/Scripts/Physics/HabitatFluidIncursionDirector.cs:91: _sourceBodyId = Hecton8.Core.GlobalSignals.FoldEntityIdToSourceId(EntityId.ToULong(GetEntityId()));
- Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsRuntime.cs:1720: var originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Physics/Vehicles/VehicleComponentDamageRuntime.cs:605: uint fallback = Hecton8.Core.GlobalSignals.FoldEntityIdToSourceId(EntityId.ToULong(GetEntityId()));
- Assets/_Project/Scripts/World/AbyssalThermalManager.cs:4093: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/World/AcousticOcclusionUtility.cs:757: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs:2288: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/World/DestructibleOrganicManager.cs:6262: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/World/EmergencyServiceRelay.cs:373: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/World/FaunaSpatialHashRegistry.cs:852: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/World/FloraInteractionManager.cs:8436: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs:926: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/World/HectonBrinePoolMeshGenerator.cs:323: AbsoluteUniversePosition runtimeOriginAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/World/HectonBrineToxicMudGrid.cs:514: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/World/HectonIndirectVegetationContracts.cs:240: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/World/HectonVoxelStreamingBridge.cs:546: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/World/ImpostorSystem.cs:892: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:91: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:180: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:5289: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:4813: originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:5271: originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/World/ResourceDistributionDirector.cs:2333: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs:3154: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:5770: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/World/VegetationNavGridSynchronizer.cs:1250: originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/World/WorldSpatialHashGrid.cs:1464: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/World/Biolum/HectonBiolumManager.cs:518: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/World/Biolum/HectonBiolumManager.cs:1663: _cachedCameraAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/World/Biolum/HectonBiolumZone.cs:555: : GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/World/Biolum/HectonBiolumZone.cs:584: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs:155: GlobalSignals.InitializeAllQueues();
- Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs:1631: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/UI/AcousticEcholocationTranslator.cs:2000: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/UI/BaseIntegrityHUD.cs:811: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/UI/BuilderStatusOverlay.cs:354: ? GlobalSignals.FoldEntityIdToSourceId(EntityId.ToULong(manager.gameObject.GetEntityId()))
- Assets/_Project/Scripts/UI/DiegeticPanelController.cs:1886: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/UI/DiegeticPDAController.cs:561: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/UI/PDAAtlasSignalTab.cs:819: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/UI/PDABarterTab.cs:177: _exchangeSourceId = GlobalSignals.FoldEntityIdToSourceId(EntityId.ToULong(current.GetEntityId()));
- Assets/_Project/Scripts/UI/PDAConstructionTab.cs:469: ? GlobalSignals.FoldEntityIdToSourceId(EntityId.ToULong(manager.gameObject.GetEntityId()))
- Assets/_Project/Scripts/UI/PDALoadoutTab.cs:465: ? GlobalSignals.FoldEntityIdToSourceId(EntityId.ToULong(manager.gameObject.GetEntityId()))
- Assets/_Project/Scripts/UI/PDAShellChrome.cs:652: ? GlobalSignals.FoldEntityIdToSourceId(EntityId.ToULong(manager.gameObject.GetEntityId()))
- Assets/_Project/Scripts/UI/PDASpectrumTab.cs:698: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/UI/PhysicalPanelButton.cs:627: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/UI/SuitAdvisoryController.cs:230: ? GlobalSignals.FoldEntityIdToSourceId(EntityId.ToULong(system.GetEntityId()))
- Assets/_Project/Scripts/UI/WristHologramHudRuntime_PdaScreenProjector.cs:628: return GlobalSignals.TryRuntimePositionToAup(runtimePosition, ref aup) && aup.IsFinite();
- Assets/_Project/Scripts/UI/WristHologramHudRuntime_PdaScreenProjector.cs:637: AbsoluteUniversePosition origin = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/UI/TopographicalSonar/TopographicalSonarSynthesizer.cs:1654: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:4473: originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Audio/ProceduralAudioEvents.cs:259: AbsoluteUniversePosition fallbackAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Audio/ProceduralAudioEvents.cs:269: AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
- Assets/_Project/Scripts/Audio/ProceduralAudioEvents.cs:851: GlobalSignals.InitializeAllQueues();
- Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:825: AbsoluteUniversePosition listenerAup = GlobalSignals.CurrentRuntimeOriginAup();
